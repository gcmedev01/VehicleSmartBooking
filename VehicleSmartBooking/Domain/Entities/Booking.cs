using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;

public sealed class Booking
{
    public long BookingId { get; set; }

    public int RequesterUserId { get; set; }
    public TripType TripType { get; set; }
    public VehicleType VehicleTypeRequested { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public string PickupLocation { get; set; } = null!;
    public string DestinationLocation { get; set; } = null!;
    public string? RequesterPhone { get; set; }
    public string? Purpose { get; set; }
    public int? PassengerCount { get; set; }
    public string? DetailNote { get; set; }

    public string? CostCenter { get; set; }
    public string? JobNo { get; set; }
    public string? SONo { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Draft;

    public int? AssignedVehicleId { get; set; }
    public int? AssignedDriverId { get; set; }

    public bool IsExternalRental { get; set; }
    public bool IsPersonal { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation
    public User Requester { get; set; } = null!;
    public Vehicle? AssignedVehicle { get; set; }
    public Driver? AssignedDriver { get; set; }

    public ICollection<BookingApproval> Approvals { get; set; } = new List<BookingApproval>();
    public ICollection<BookingDispatchLog> DispatchLogs { get; set; } = new List<BookingDispatchLog>();
    public ExternalRental? ExternalRental { get; set; }
    public DriverRating? Rating { get; set; }
    public ICollection<BookingAttachment> Attachments { get; set; } = new List<BookingAttachment>();
}
