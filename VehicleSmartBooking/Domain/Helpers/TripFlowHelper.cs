using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

public static class TripFlowHelper
{
    public static bool IsInProvinceNoApprover(Booking booking)
    {
        return booking.TripType == TripType.InProvince;
    }

    public static bool CanDriverAccept(Booking booking)
    {
        return booking.Status == BookingStatus.WaitingDriverAccept;
    }

    public static bool CanDriverComplete(Booking booking)
    {
        return booking.Status == BookingStatus.DriverAccepted;
    }
}
