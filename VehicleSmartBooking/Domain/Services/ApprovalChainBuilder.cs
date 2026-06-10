using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;

namespace VehicleBooking.Web.Domain.Services;

public sealed class ApprovalChainBuilder
{
    private readonly VehicleBookingDbContext _db;

    public ApprovalChainBuilder(VehicleBookingDbContext db)
    {
        _db = db;
    }

    public async Task<ApprovalChainResult> BuildAsync(int requesterUserId, ApprovalScenario scenario)
    {
        var requester = await _db.Users.AsNoTracking().SingleAsync(u => u.UserId == requesterUserId && u.IsActive);
        var requesterRole = GetApproverRole(requester.PositionEN);

        if (scenario == ApprovalScenario.None || scenario == ApprovalScenario.ElectricSingleDay)
        {
            return new ApprovalChainResult([], true, false);
        }

        if (requesterRole is ApproverRole.DMD or ApproverRole.MD)
        {
            return new ApprovalChainResult([], true, false);
        }

        var requiredRoles = GetRequiredRoles(scenario, requesterRole);
        if (requiredRoles.Count == 0)
        {
            return new ApprovalChainResult([], true, false);
        }

        var approvals = new List<ApprovalCandidate>();
        var current = requester;
        var usedUserIds = new HashSet<int> { requester.UserId };

        foreach (var role in requiredRoles)
        {
            var approver = await FindNextApproverAsync(current, role, usedUserIds);
            if (approver is null)
            {
                continue;
            }

            approvals.Add(new ApprovalCandidate(approver, approvals.Count + 1, role));
            current = approver;
        }

        return approvals.Count == 0
            ? new ApprovalChainResult([], true, false)
            : new ApprovalChainResult(approvals, false, false);
    }

    public async Task<IReadOnlyList<ApprovalCandidate>> BuildPreviewAsync(int requesterUserId)
    {
        var result = await BuildAsync(requesterUserId, ApprovalScenario.SpecialOccasion);
        return result.Approvers;
    }

    private async Task<User?> FindNextApproverAsync(User startFrom, ApproverRole targetRole, HashSet<int> usedUserIds)
    {
        var current = startFrom;

        while (current.LineManagerId.HasValue)
        {
            var manager = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == current.LineManagerId.Value && u.IsActive);
            if (manager is null)
            {
                return null;
            }

            current = manager;
            if (!usedUserIds.Add(manager.UserId))
            {
                return null;
            }

            if (GetApproverRole(manager.PositionEN) == targetRole)
            {
                return manager;
            }
        }

        return null;
    }

    private static List<ApproverRole> GetRequiredRoles(ApprovalScenario scenario, ApproverRole requesterRole)
    {
        var roles = scenario switch
        {
            ApprovalScenario.OutProvinceGeneral => new List<ApproverRole> { ApproverRole.DM },
            ApprovalScenario.PersonalVehicle => new List<ApproverRole> { ApproverRole.DM, ApproverRole.VP },
            ApprovalScenario.SpecialOccasion => new List<ApproverRole> { ApproverRole.DM, ApproverRole.VP, ApproverRole.DMD },
            ApprovalScenario.ElectricMultiDay => new List<ApproverRole> { ApproverRole.DM },
            _ => []
        };

        return roles.Where(role => role > requesterRole && role < ApproverRole.MD).ToList();
    }

    public static ApproverRole GetApproverRole(string? positionEn)
    {
        var position = (positionEn ?? string.Empty).Trim();

        return position switch
        {
            "Division Manager" or "Acting Division Manager" => ApproverRole.DM,
            "Vice President" or "Acting Vice President" => ApproverRole.VP,
            "Deputy Managing Director" or "Acting Deputy Managing Director" => ApproverRole.DMD,
            "Managing Director" or "Acting Managing Director" => ApproverRole.MD,
            "Section Manager" or "Acting Section Manager" => ApproverRole.SectionManager,
            _ => ApproverRole.Staff
        };
    }
}

public sealed record ApprovalCandidate(User User, int LevelNo, ApproverRole Role);
public sealed record ApprovalChainResult(List<ApprovalCandidate> Approvers, bool IsAutoApproved, bool IsAdminActionRequired);

public enum ApprovalScenario
{
    None = 0,
    OutProvinceGeneral = 1,
    PersonalVehicle = 2,
    SpecialOccasion = 3,
    ElectricSingleDay = 4,
    ElectricMultiDay = 5
}

public enum ApproverRole
{
    Staff = 0,
    SectionManager = 1,
    DM = 2,
    VP = 3,
    DMD = 4,
    MD = 5
}
