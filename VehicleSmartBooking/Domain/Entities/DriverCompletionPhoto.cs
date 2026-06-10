using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;

public sealed class DriverCompletionPhoto
{
    public int DriverCompletionPhotoId { get; set; }
    public long BookingId { get; set; }
    public DriverCompletionPhotoGroup PhotoGroup { get; set; }
    public string OriginalFileName { get; set; } = null!;
    public string StoredFileName { get; set; } = null!;
    public string RelativePath { get; set; } = null!;
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public int? UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; }

    public Booking Booking { get; set; } = null!;
}
