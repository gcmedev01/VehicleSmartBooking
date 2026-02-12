using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Helpers
{
    public static class BookingStatusHelper
    {
        public static readonly BookingStatus[] TerminalStatuses =
        {
            BookingStatus.Completed,
            BookingStatus.Rated,
            BookingStatus.Rejected,
            BookingStatus.Cancelled,
            BookingStatus.VendorRejectedByUser
        };
    }
}
