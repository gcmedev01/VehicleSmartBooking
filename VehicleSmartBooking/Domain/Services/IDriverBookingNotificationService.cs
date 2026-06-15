namespace VehicleBooking.Web.Domain.Services;

public interface IDriverBookingNotificationService
{
    Task NotifyNewAssignmentAsync(long bookingId, int driverId);
    Task NotifyAdminReassignedToNewDriverAsync(long bookingId, int? oldDriverId, int newDriverId);
    Task NotifyAdminReassignedAwayFromOldDriverAsync(long bookingId, int oldDriverId, int newDriverId);
    Task NotifyBookingCancelledAsync(long bookingId, int driverId);
    Task NotifyBookingUpdatedAsync(long bookingId, int driverId);
}
