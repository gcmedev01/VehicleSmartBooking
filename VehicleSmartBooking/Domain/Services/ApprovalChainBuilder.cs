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
            var effectiveRole = role;
            var approver = await FindApproverByRoleAsync(current, role);

            // Fallback: any scenario that requires a DM but whose requester has no Division Manager
            // in the line-manager chain uses the VP instead (when a VP exists higher up).
            if (approver is null && role == ApproverRole.DM)
            {
                var vpApprover = await FindApproverByRoleAsync(current, ApproverRole.VP);
                if (vpApprover is not null)
                {
                    effectiveRole = ApproverRole.VP;
                    approver = vpApprover;
                }
            }

            if (approver is null)
            {
                continue;
            }

            // Never add the same approver twice — e.g. a DM step fell back to VP and the scenario
            // also contains a VP step that resolves to the same person (avoids VP, VP).
            if (!usedUserIds.Add(approver.UserId))
            {
                continue;
            }

            approvals.Add(new ApprovalCandidate(approver, approvals.Count + 1, effectiveRole));
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

    // Walks up the requester's line-manager chain and returns the first active manager whose role
    // matches targetRole. Pure lookup: it does not mutate any shared state, so it can be called
    // again for a fallback role (DM -> VP) without poisoning the later search.
    private async Task<User?> FindApproverByRoleAsync(User startFrom, ApproverRole targetRole)
    {
        var current = startFrom;
        var visited = new HashSet<int> { startFrom.UserId };

        while (current.LineManagerId.HasValue)
        {
            var manager = await _db.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.UserId == current.LineManagerId.Value && u.IsActive);
            if (manager is null)
            {
                return null;
            }

            if (!visited.Add(manager.UserId))
            {
                return null; // cycle guard
            }

            current = manager;

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
