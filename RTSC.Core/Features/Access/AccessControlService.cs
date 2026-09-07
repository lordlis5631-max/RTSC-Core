using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Features.Access;

public sealed class AccessControlService(AppDbContext db)
{
    public static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static bool IsGlobalAdmin(ClaimsPrincipal principal) =>
        principal.IsInRole(GlobalRole.Admin.ToString()) || principal.IsInRole(GlobalRole.SuperAdmin.ToString());

    public async Task<bool> CanManageCommunityAsync(Guid communityId, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (IsGlobalAdmin(principal)) return true;
        var userId = GetUserId(principal);
        if (userId is null) return false;

        return await db.CommunityMembers.AnyAsync(x =>
            x.CommunityId == communityId &&
            x.UserId == userId.Value &&
            (x.Role == CommunityMemberRole.Owner || x.Role == CommunityMemberRole.Admin), cancellationToken);
    }

    public async Task<bool> CanManageEventAsync(Guid eventId, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (IsGlobalAdmin(principal)) return true;
        var communityId = await db.Events.Where(x => x.Id == eventId).Select(x => (Guid?)x.CommunityId).SingleOrDefaultAsync(cancellationToken);
        return communityId is not null && await CanManageCommunityAsync(communityId.Value, principal, cancellationToken);
    }
}
