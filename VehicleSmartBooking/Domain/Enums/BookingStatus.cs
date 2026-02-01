namespace VehicleBooking.Web.Domain.Enums;
public enum BookingStatus : int
{
    Draft = 1,
    Submitted = 2,

    WaitingApproval = 10,
    WaitingDriverAccept = 11,
    DriverAccepted = 12,

    WaitingAdminVendorQuotation = 20,
    WaitingUserVendorAccept = 21,
    VendorRejectedByUser = 22,
    WaitingAdminVendorConfirm = 23,

    Completed = 30,
    Rated = 31,

    Rejected = 90,
    Cancelled = 91,
    AdminActionRequired = 92
}
