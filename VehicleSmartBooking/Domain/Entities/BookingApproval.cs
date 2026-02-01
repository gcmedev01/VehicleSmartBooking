using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;

public sealed class BookingApproval
{
    public long ApprovalId { get; set; }
    public long BookingId { get; set; }
    public int ApproverUserId { get; set; }

    public int LevelNo { get; set; } = 1;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public DateTime? ActionAtUtc { get; set; }
    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Booking Booking { get; set; } = null!;
    public User Approver { get; set; } = null!;
}
