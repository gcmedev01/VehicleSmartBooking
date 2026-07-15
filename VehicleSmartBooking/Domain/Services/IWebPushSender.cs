namespace VehicleBooking.Web.Domain.Services;

public interface IWebPushSender
{
    bool IsEnabled { get; }

    /// <summary>Sends a push notification to all active subscriptions for a user. Silently skips if not enabled.
    /// Returns the number of subscriptions the notification was successfully delivered to.</summary>
    Task<int> SendToUserAsync(int userId, string title, string message, string? url);
}
