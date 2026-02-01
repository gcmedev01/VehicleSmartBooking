using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;
public sealed class BookingDispatchLog
{
    public long LogId { get; set; }
    public long BookingId { get; set; }

    public int AttemptNo { get; set; }
    public int VehicleId { get; set; }
    public int DriverId { get; set; }

    public DateTime DispatchedAtUtc { get; set; }

    public DriverAction? DriverAction { get; set; }
    public DateTime? DriverActionAtUtc { get; set; }
    public string? DeclineReason { get; set; }

    public Booking Booking { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
}