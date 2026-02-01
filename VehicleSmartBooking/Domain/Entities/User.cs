using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Entities;

public sealed class User
{
    public int UserId { get; set; }

    public string UserCode { get; set; } = null!;
    public string UsernameTH { get; set; } = null!;
    public string? UsernameEN { get; set; }

    public string? FunctionTH { get; set; }
    public string? FunctionEN { get; set; }
    public string? FunctionAbbr { get; set; }

    public string? DeptTH { get; set; }
    public string? DeptEN { get; set; }
    public string? DeptAbbr { get; set; }

    public string? DivTH { get; set; }
    public string? DivEN { get; set; }
    public string? DivAbbr { get; set; }

    public string? PositionTH { get; set; }
    public string? PositionEN { get; set; }

    public string? Email { get; set; }

    public int RoleFlags { get; set; }
    public bool IsActive { get; set; } = true;

    public int? LineManagerId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation
    public User? LineManager { get; set; }
    public ICollection<User> DirectReports { get; set; } = new List<User>();

    public UserCredential? Credential { get; set; } // 1:1 (optional)
    public Driver? DriverProfile { get; set; }       // 1:1 (optional)
}
