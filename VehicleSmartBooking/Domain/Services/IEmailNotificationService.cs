using VehicleBooking.Web.Domain.Entities;

namespace VehicleBooking.Web.Domain.Services;

public interface IEmailNotificationService
{
    Task NotifyStatusChangedAsync(Booking booking, IEnumerable<string> adminEmails, string? ownerEmail, DateTime? statusChangedAtUtc = null, string? relativeUrl = null);
    Task NotifyActionRequiredAsync(Booking booking, IEnumerable<string> actionEmails, string actionLabel, string? relativeUrl = null);
}
