namespace VehicleBooking.Web.Domain.Entities;

public sealed class PushSubscription
{
    public long PushSubscriptionId { get; set; }
    public int UserId { get; set; }
    public int? DriverId { get; set; }
    public string Endpoint { get; set; } = null!;
    public string P256dh { get; set; } = null!;
    public string Auth { get; set; } = null!;
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
