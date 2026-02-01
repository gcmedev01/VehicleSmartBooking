using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;
public sealed class DriverRating
{
    public long RatingId { get; set; }
    public long BookingId { get; set; }
    public int DriverId { get; set; }

    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Booking Booking { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
}
