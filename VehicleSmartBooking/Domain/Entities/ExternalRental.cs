using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;
public sealed class ExternalRental
{
    public long ExternalRentalId { get; set; }
    public long BookingId { get; set; }

    public string? VendorName { get; set; }
    public decimal? QuotedPrice { get; set; }
    public DateTime? QuoteSentAtUtc { get; set; }

    public ExternalUserDecision UserDecision { get; set; } = ExternalUserDecision.Pending;
    public DateTime? UserDecisionAtUtc { get; set; }

    public string? Note { get; set; } // vendor note / admin note

    public string? RentalPlateNo { get; set; }
    public string? RentalDriverName { get; set; }
    public string? RentalDriverPhone { get; set; }

    public DateTime? AdminClosedAtUtc { get; set; }

    public Booking Booking { get; set; } = null!;
}