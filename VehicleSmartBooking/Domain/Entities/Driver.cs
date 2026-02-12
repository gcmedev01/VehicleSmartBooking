namespace VehicleBooking.Web.Domain.Entities;

public sealed class Driver
{
    public int DriverId { get; set; }
    public int UserId { get; set; }
    public int VehicleId { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastAssignedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}
