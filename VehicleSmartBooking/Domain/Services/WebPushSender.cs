using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Options;

namespace VehicleBooking.Web.Domain.Services;

public sealed class WebPushSender : IWebPushSender
{
    private readonly VehicleBookingDbContext _db;
    private readonly VapidOptions _options;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(
        VehicleBookingDbContext db,
        IOptions<VapidOptions> options,
        ILogger<WebPushSender> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.IsConfigured;

    public async Task<int> SendToUserAsync(int userId, string title, string message, string? url)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Web Push skipped for user {UserId}: VAPID not configured", userId);
            return 0;
        }

        var subscriptions = await _db.PushSubscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        if (subscriptions.Count == 0) return 0;

        var payload = JsonSerializer.Serialize(new { title, body = message, url });

        WebPush.VapidDetails vapidDetails;
        try
        {
            // VAPID subject must be a "mailto:" address or an absolute URL, otherwise the
            // WebPush library rejects it. Normalize defensively so a bare e-mail in config
            // does not silently break every push send.
            vapidDetails = new WebPush.VapidDetails(NormalizeSubject(_options.Subject!), _options.PublicKey!, _options.PrivateKey!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web Push disabled for this send: VAPID configuration is invalid (check Vapid:Subject/PublicKey/PrivateKey).");
            return 0;
        }

        var now = DateTime.UtcNow;
        var sent = 0;

        using var client = new WebPush.WebPushClient();

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, vapidDetails);
                sub.LastUsedAtUtc = now;
                sent++;
            }
            catch (WebPush.WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                sub.IsActive = false;
                sub.DeactivatedAtUtc = now;
                _logger.LogInformation("Push subscription {Id} deactivated — endpoint gone ({Status})", sub.PushSubscriptionId, (int)ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send push notification to subscription {Id}", sub.PushSubscriptionId);
            }
        }

        try { await _db.SaveChangesAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to persist push subscription state after send for user {UserId}", userId); }

        return sent;
    }

    // Ensures the VAPID subject is a valid "mailto:" address or absolute URL as required by the spec.
    private static string NormalizeSubject(string subject)
    {
        var s = (subject ?? string.Empty).Trim();
        if (s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return s;
        if (Uri.IsWellFormedUriString(s, UriKind.Absolute)) return s;
        return "mailto:" + s;
    }
}
