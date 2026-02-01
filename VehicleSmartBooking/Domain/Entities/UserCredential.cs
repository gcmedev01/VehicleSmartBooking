namespace VehicleBooking.Web.Domain.Entities;

public sealed class UserCredential
{
    public int CredentialId { get; set; }
    public int UserId { get; set; }

    public string LoginUsername { get; set; } = null!;
    public byte[] PasswordHash { get; set; } = null!;
    public byte[] PasswordSalt { get; set; } = null!;

    public string PasswordAlgo { get; set; } = "PBKDF2-HMACSHA256";
    public int Iterations { get; set; } = 600000;

    public bool IsLocked { get; set; }
    public int FailedCount { get; set; }
    public DateTime? LastFailedAtUtc { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
