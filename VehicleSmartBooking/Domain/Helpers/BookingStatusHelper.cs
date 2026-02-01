using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Helpers
{
    public static class BookingStatusHelper
    {
        public static bool IsTerminalStatus(BookingStatus status)
        {
            return status == BookingStatus.Cancelled
                || status == BookingStatus.Rejected
                || status == BookingStatus.Completed
                || status == BookingStatus.Rated;
        }
    }
}
