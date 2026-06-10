using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Options;

namespace VehicleBooking.Web.Domain.Services;

public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly SmtpSettings _settings;
    private readonly EmailNotificationOptions _notificationOptions;

    public EmailNotificationService(IOptions<SmtpSettings> settings, IOptions<EmailNotificationOptions> notificationOptions)
    {
        _settings = settings.Value;
        _notificationOptions = notificationOptions.Value;
    }

    public async Task NotifyStatusChangedAsync(Booking booking, IEnumerable<string> adminEmails, string? ownerEmail, DateTime? statusChangedAtUtc = null, string? relativeUrl = null)
    {
        var recipients = CollectRecipients(adminEmails, ownerEmail);
        if (!recipients.Any()) return;

        var statusText = GetStatusText(booking.Status);
        var requesterName = GetRequesterName(booking);
        var changeTime = (statusChangedAtUtc ?? booking.UpdatedAtUtc).ToLocalTime();
        var detailLink = BuildLink(booking, relativeUrl);

        var subject = $"Booking #{booking.BookingId} - สถานะเปลี่ยนเป็น {statusText}";
        var body = BuildHtmlBody(
            booking,
            requesterName,
            statusText,
            changeTime,
            actionLabel: null,
            detailLink);

        await SendAsync(subject, body, recipients);
    }

    public async Task NotifyActionRequiredAsync(Booking booking, IEnumerable<string> actionEmails, string actionLabel, string? relativeUrl = null)
    {
        var recipients = CollectRecipients(actionEmails, null);
        if (!recipients.Any()) return;

        var statusText = GetStatusText(booking.Status);
        var requesterName = GetRequesterName(booking);
        var detailLink = BuildLink(booking, relativeUrl);

        var subject = $"Booking #{booking.BookingId} - ต้องดำเนินการ: {actionLabel}";
        var body = BuildHtmlBody(
            booking,
            requesterName,
            statusText,
            changeTime: null,
            actionLabel,
            detailLink);

        await SendAsync(subject, body, recipients);
    }

    private async Task SendAsync(string subject, string body, IReadOnlyCollection<string> recipients)
    {
        using var client = new SmtpClient(_settings.Server, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
        };

        if (!string.IsNullOrWhiteSpace(_settings.UserName))
        {
            client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.From),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        await client.SendMailAsync(message);
    }

    private static string BuildHtmlBody(Booking booking, string requesterName, string statusText, DateTime? changeTime, string? actionLabel, string? detailLink)
    {
        var safeRequester = WebUtility.HtmlEncode(requesterName);
        var safeStatus = WebUtility.HtmlEncode(statusText);
        var safeAction = string.IsNullOrWhiteSpace(actionLabel) ? null : WebUtility.HtmlEncode(actionLabel);
        var safeLink = string.IsNullOrWhiteSpace(detailLink) ? null : WebUtility.HtmlEncode(detailLink);

        var sb = new StringBuilder()
    .AppendLine("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:1.6;\">")
    .AppendLine($"<div><strong>Booking:</strong> #{booking.BookingId}</div>")
    .AppendLine($"<div><strong>ผู้ขอใช้:</strong> {safeRequester}</div>")
    .AppendLine($"<div><strong>สถานะ:</strong> {safeStatus}</div>");

        if (changeTime.HasValue)
        {
            sb.AppendLine($"<div><strong>เวลาอัปเดต:</strong> {changeTime.Value:dd/MM/yyyy HH:mm}</div>");
        }

        if (!string.IsNullOrWhiteSpace(safeAction))
        {
            sb.AppendLine($"<div><strong>รายการที่ต้องดำเนินการ:</strong> {safeAction}</div>");
        }

        sb.AppendLine($"<div><strong>ช่วงเวลาเดินทาง:</strong> {booking.StartAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} - {booking.EndAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}</div>");

        if (!string.IsNullOrWhiteSpace(safeLink))
        {
            sb.AppendLine($"<div><strong>ลิ้งก์:</strong> <a href=\"{safeLink}\">คลิกที่นี่</a></div>");
        }

        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private IReadOnlyCollection<string> CollectRecipients(IEnumerable<string> primary, string? optional)
    {
        if (_notificationOptions.TestMode)
        {
            return string.IsNullOrWhiteSpace(_notificationOptions.TestRecipient)
                ? Array.Empty<string>()
                : new[] { _notificationOptions.TestRecipient };
        }

        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var email in primary ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                emails.Add(email.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(optional))
        {
            emails.Add(optional.Trim());
        }

        return emails.ToList();
    }

    private string? BuildLink(Booking booking, string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(_notificationOptions.BaseUrl)) return null;
        if (string.IsNullOrWhiteSpace(relativeUrl)) return null;

        var resolved = relativeUrl.Replace("{id}", booking.BookingId.ToString());
        resolved = resolved.StartsWith('/') ? resolved : $"/{resolved}";

        return $"{_notificationOptions.BaseUrl.TrimEnd('/')}{resolved}";
    }

    private static string GetRequesterName(Booking booking)
    {
        if (!string.IsNullOrWhiteSpace(booking.Requester?.UsernameTH)) return booking.Requester.UsernameTH;
        if (!string.IsNullOrWhiteSpace(booking.Requester?.UsernameEN)) return booking.Requester.UsernameEN;
        return booking.RequesterUserId.ToString();
    }

    private static string GetStatusText(BookingStatus status) => status switch
    {
        BookingStatus.Draft => "ร่าง",
        BookingStatus.Submitted => "ส่งคำขอแล้ว",
        BookingStatus.WaitingApproval => "รออนุมัติ",
        BookingStatus.WaitingDriverAccept => "รอพนักงานขับรถตอบรับ",
        BookingStatus.DriverAccepted => "พนักงานขับรถตอบรับ",
        BookingStatus.WaitingAdminVendorQuotation => "รอเสนอราคาผู้ให้บริการ",
        BookingStatus.WaitingUserVendorAccept => "รอผู้ขอใช้ยอมรับผู้ให้บริการ",
        BookingStatus.WaitingAdminVendorConfirm => "รอยืนยันผู้ให้บริการ",
        BookingStatus.VendorRejectedByUser => "ผู้ขอใช้ปฏิเสธผู้ให้บริการ",
        BookingStatus.WaitingAdminPersonal => "รอผู้ดูแลอนุมัติรถส่วนตัว",
        BookingStatus.Completed => "เสร็จสิ้น",
        BookingStatus.Rated => "ให้คะแนนแล้ว",
        BookingStatus.Rejected => "ถูกปฏิเสธ",
        BookingStatus.Cancelled => "ยกเลิก",
        BookingStatus.AdminActionRequired => "รอผู้ดูแลดำเนินการ",
        _ => status.ToString()
    };
}
