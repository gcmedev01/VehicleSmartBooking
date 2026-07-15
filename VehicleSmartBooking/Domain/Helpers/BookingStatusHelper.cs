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

        /// <summary>
        /// Returns the CSS status-badge class for a booking status. Each workflow stage gets a
        /// visually distinct color so different statuses are not shown with the same color.
        /// </summary>
        public static string StatusCssClass(BookingStatus status) => status switch
        {
            BookingStatus.Draft => "vb-status-draft",
            BookingStatus.Submitted => "vb-status-submitted",
            BookingStatus.WaitingApproval => "vb-status-pending",
            BookingStatus.WaitingDriverAccept => "vb-status-driverwait",
            BookingStatus.DriverAccepted => "vb-status-driveraccepted",
            BookingStatus.ApprovedSelfDrive => "vb-status-approved",
            BookingStatus.WaitingAdminVendorQuotation => "vb-status-vendor",
            BookingStatus.WaitingUserVendorAccept => "vb-status-vendoruser",
            BookingStatus.WaitingAdminVendorConfirm => "vb-status-vendorconfirm",
            BookingStatus.WaitingAdminPersonal => "vb-status-personal",
            BookingStatus.AdminActionRequired => "vb-status-adminaction",
            BookingStatus.Completed => "vb-status-completed",
            BookingStatus.Rated => "vb-status-rated",
            BookingStatus.Rejected => "vb-status-rejected",
            BookingStatus.Cancelled => "vb-status-cancelled",
            BookingStatus.VendorRejectedByUser => "vb-status-rejected",
            _ => "vb-status-pending"
        };
    }
}
