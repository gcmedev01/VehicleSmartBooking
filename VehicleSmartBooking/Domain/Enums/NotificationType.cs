namespace VehicleBooking.Web.Domain.Enums;

public enum NotificationType : int
{
    TestNotification = 0,
    NewDriverAssignment = 1,
    AdminDriverReassignedToYou = 2,
    AdminDriverReassignedAwayFromYou = 3,
    BookingCancelled = 4,
    BookingUpdated = 5,
    DriverAcceptanceRequired = 6,
}
