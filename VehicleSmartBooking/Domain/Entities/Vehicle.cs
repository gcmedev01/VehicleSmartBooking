using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;

public sealed class Vehicle
{
    public int VehicleId { get; set; }
    public string PlateNo { get; set; } = null!;
    public VehicleType VehicleType { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Driver? Driver { get; set; } // 1:1
}
