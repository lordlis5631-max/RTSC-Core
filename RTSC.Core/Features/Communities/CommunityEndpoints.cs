using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Features.Communities;

public static class CommunityEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/communities").WithTags("Communities");

        group.MapGet("/", async (AppDbContext db, string? q) =>
        {
            var query = db.Communities.AsNoTracking().Where(x => x.Status == CommunityStatus.Published);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x => EF.Functions.ILike(x.Name, $"%{q.Trim()}%"));

            return Results.Ok(await query.OrderBy(x => x.Name).Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.LogoUrl
            }).ToListAsync());
        });

        group.MapGet("/{communityId:guid}", async (Guid communityId, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            var item = await db.Communities.AsNoTracking()
                .Where(x => x.Id == communityId)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Description,
                    x.LogoUrl,
                    x.Status,
                    memberCount = x.Members.Count,
                    eventCount = x.Events.Count
                })
                .SingleOrDefaultAsync();

            if (item is null) return Results.NotFound();
            if (item.Status != CommunityStatus.Published && !await access.CanManageCommunityAsync(communityId, http.User))
                return Results.NotFound();

            return Results.Ok(item);
        });

        group.MapPost("/", async (CreateCommunityRequest request, HttpContext http, AppDbContext db) =>
        {
            var userId = AccessControlService.GetUserId(http.User);
            if (userId is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "name is required" });

            var community = new Community
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                LogoUrl = NormalizeOptional(request.LogoUrl),
                Status = CommunityStatus.Draft
            };

            db.Communities.Add(community);
            db.CommunityMembers.Add(new CommunityMember
            {
                CommunityId = community.Id,
                UserId = userId.Value,
                Role = CommunityMemberRole.Owner
            });

            await db.SaveChangesAsync();
            return Results.Created($"/api/communities/{community.Id}", new { community.Id, community.Name, status = community.Status.ToString() });
        }).RequireAuthorization();

        group.MapPut("/{communityId:guid}", async (Guid communityId, UpdateCommunityRequest request, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            if (!await access.CanManageCommunityAsync(communityId, http.User)) return Results.Forbid();
            var community = await db.Communities.SingleOrDefaultAsync(x => x.Id == communityId);
            if (community is null) return Results.NotFound();
            if (community.Status == CommunityStatus.Archived) return Results.BadRequest(new { error = "community is archived" });
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "name is required" });

            community.Name = request.Name.Trim();
            community.Description = request.Description?.Trim() ?? string.Empty;
            community.LogoUrl = NormalizeOptional(request.LogoUrl);
            if (community.Status == CommunityStatus.Rejected) community.Status = CommunityStatus.Draft;
            await db.SaveChangesAsync();
            return Results.Ok(new { status = community.Status.ToString() });
        }).RequireAuthorization();

        group.MapPost("/{communityId:guid}/submit", async (Guid communityId, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            if (!await access.CanManageCommunityAsync(communityId, http.User)) return Results.Forbid();
            var community = await db.Communities.SingleOrDefaultAsync(x => x.Id == communityId);
            if (community is null) return Results.NotFound();
            if (community.Status is not (CommunityStatus.Draft or CommunityStatus.Rejected))
                return Results.BadRequest(new { error = "community cannot be submitted in current status" });

            community.Status = CommunityStatus.Moderation;
            await db.SaveChangesAsync();
            return Results.Ok(new { status = community.Status.ToString() });
        }).RequireAuthorization();

        group.MapPost("/{communityId:guid}/members", async (Guid communityId, AddMemberRequest request, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            if (!await access.CanManageCommunityAsync(communityId, http.User)) return Results.Forbid();
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Status == UserStatus.Active);
            if (user is null) return Results.NotFound(new { error = "user not found" });
            if (request.Role == CommunityMemberRole.Owner) return Results.BadRequest(new { error = "owner role cannot be assigned here" });

            var member = await db.CommunityMembers.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.UserId == user.Id);
            if (member is null)
            {
                db.CommunityMembers.Add(new CommunityMember { CommunityId = communityId, UserId = user.Id, Role = request.Role });
            }
            else
            {
                member.Role = request.Role;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { user.Id, user.DisplayName, role = request.Role.ToString() });
        }).RequireAuthorization();

        return app;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record CreateCommunityRequest(string Name, string? Description, string? LogoUrl);
    public sealed record UpdateCommunityRequest(string Name, string? Description, string? LogoUrl);
    public sealed record AddMemberRequest(string Email, CommunityMemberRole Role);
}
