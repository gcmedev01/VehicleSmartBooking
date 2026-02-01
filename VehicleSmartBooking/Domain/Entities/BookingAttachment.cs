using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;
public sealed class BookingAttachment
{
    public long AttachmentId { get; set; }
    public long BookingId { get; set; }

    public string FileName { get; set; } = null!;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = null!;

    public int UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; }

    public Booking Booking { get; set; } = null!;
    public User Uploader { get; set; } = null!;
}