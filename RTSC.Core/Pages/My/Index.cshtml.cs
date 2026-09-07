using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.Personalization;

namespace RTSC.Core.Pages.My;

[Authorize]
public sealed class IndexModel(AppDbContext db, RecommendationService recommendationService) : PageModel
{
    public string DisplayName { get; private set; } = string.Empty;
    public DashboardStats Stats { get; private set; } = new(0, 0, 0, 0, 0, 0);
    public bool HasInterests { get; private set; }
    public IReadOnlyList<RecommendationVm> Recommendations { get; private set; } = [];
    public IReadOnlyList<ParticipationVm> Upcoming { get; private set; } = [];
    public IReadOnlyList<ParticipationVm> Past { get; private set; } = [];
    public IReadOnlyList<PerformerVm> PerformerAssignments { get; private set; } = [];
    public IReadOnlyList<CommunityVm> ManagedCommunities { get; private set; } = [];
    public IReadOnlyList<ManagedEventVm> ManagedEvents { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AccessControlService.GetUserId(User);
        if (userId is null) return Challenge();

        var now = DateTimeOffset.UtcNow;

        DisplayName = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => x.DisplayName)
            .SingleOrDefaultAsync() ?? User.Identity?.Name ?? "Пользователь";

        HasInterests = await db.UserCategoryInterests.AsNoTracking().AnyAsync(x => x.UserId == userId.Value)
            || await db.UserTagInterests.AsNoTracking().AnyAsync(x => x.UserId == userId.Value);

        if (HasInterests)
        {
            Recommendations = (await recommendationService.GetForUserAsync(userId.Value, 8))
                .Select(x => new RecommendationVm(
                    x.EventId,
                    x.Title,
                    x.CommunityName,
                    x.StartAt,
                    x.Place,
                    EventCategoryCatalog.Label(x.Category),
                    x.Capacity,
                    x.Registered,
                    x.Reason))
                .ToList();
        }

        var participationQuery = db.EventParticipants.AsNoTracking()
            .Where(x => x.UserId == userId.Value && x.Status != ParticipantStatus.Cancelled);

        Upcoming = await participationQuery
            .Where(x => x.Event.StartAt >= now && x.Event.Status == EventStatus.Published)
            .OrderBy(x => x.Event.StartAt)
            .Take(12)
            .Select(x => new ParticipationVm(
                x.EventId,
                x.Event.Title,
                x.Event.Community.Name,
                x.Event.StartAt,
                x.Event.Place,
                x.Status.ToString(),
                x.Event.Latitude,
                x.Event.Longitude))
            .ToListAsync();

        Past = await participationQuery
            .Where(x => x.Event.StartAt < now || x.Event.Status == EventStatus.Completed)
            .OrderByDescending(x => x.Event.StartAt)
            .Take(12)
            .Select(x => new ParticipationVm(
                x.EventId,
                x.Event.Title,
                x.Event.Community.Name,
                x.Event.StartAt,
                x.Event.Place,
                x.Status.ToString(),
                x.Event.Latitude,
                x.Event.Longitude))
            .ToListAsync();

        PerformerAssignments = await db.EventPerformers.AsNoTracking()
            .Where(x => x.UserId == userId.Value && x.Event.StartAt >= now && x.Event.Status == EventStatus.Published)
            .OrderBy(x => x.Event.StartAt)
            .Take(8)
            .Select(x => new PerformerVm(
                x.EventId,
                x.Event.Title,
                x.Event.StartAt,
                x.Event.Place,
                x.Role,
                x.Event.Community.Name))
            .ToListAsync();

        ManagedCommunities = await db.CommunityMembers.AsNoTracking()
            .Where(x => x.UserId == userId.Value && (x.Role == CommunityMemberRole.Owner || x.Role == CommunityMemberRole.Admin))
            .OrderBy(x => x.Community.Name)
            .Select(x => new CommunityVm(
                x.CommunityId,
                x.Community.Name,
                x.Role.ToString(),
                x.Community.Status.ToString(),
                x.Community.Events.Count,
                x.Community.Members.Count))
            .ToListAsync();

        var managedCommunityIds = ManagedCommunities.Select(x => x.Id).ToArray();
        if (managedCommunityIds.Length > 0)
        {
            ManagedEvents = await db.Events.AsNoTracking()
                .Where(x => managedCommunityIds.Contains(x.CommunityId) && x.StartAt >= now && x.Status != EventStatus.Archived && x.Status != EventStatus.Cancelled)
                .OrderBy(x => x.StartAt)
                .Take(12)
                .Select(x => new ManagedEventVm(
                    x.Id,
                    x.Title,
                    x.Community.Name,
                    x.StartAt,
                    x.Status.ToString(),
                    x.Participants.Count(p => p.Status != ParticipantStatus.Cancelled),
                    x.Participants.Count(p => p.Status == ParticipantStatus.Attended),
                    x.Capacity))
                .ToListAsync();
        }

        var unreadNotifications = await db.Notifications.AsNoTracking()
            .CountAsync(x => x.UserId == userId.Value && x.ReadAt == null);
        var totalAttended = await db.EventParticipants.AsNoTracking()
            .CountAsync(x => x.UserId == userId.Value && x.Status == ParticipantStatus.Attended);
        var totalRegistrations = await db.EventParticipants.AsNoTracking()
            .CountAsync(x => x.UserId == userId.Value && x.Status != ParticipantStatus.Cancelled);
        var performerTotal = await db.EventPerformers.AsNoTracking()
            .CountAsync(x => x.UserId == userId.Value);

        Stats = new DashboardStats(
            Upcoming.Count,
            totalRegistrations,
            totalAttended,
            ManagedCommunities.Count,
            performerTotal,
            unreadNotifications);

        return Page();
    }

    public sealed record DashboardStats(int Upcoming, int Registrations, int Attended, int ManagedCommunities, int PerformerAssignments, int UnreadNotifications);
    public sealed record RecommendationVm(Guid EventId, string Title, string CommunityName, DateTimeOffset StartAt, string Place, string Category, int? Capacity, int Registered, string Reason);
    public sealed record ParticipationVm(Guid EventId, string Title, string CommunityName, DateTimeOffset StartAt, string Place, string Status, double? Latitude, double? Longitude);
    public sealed record PerformerVm(Guid EventId, string Title, DateTimeOffset StartAt, string Place, string Role, string CommunityName);
    public sealed record CommunityVm(Guid Id, string Name, string Role, string Status, int EventCount, int MemberCount);
    public sealed record ManagedEventVm(Guid Id, string Title, string CommunityName, DateTimeOffset StartAt, string Status, int Registered, int Attended, int? Capacity);
}
