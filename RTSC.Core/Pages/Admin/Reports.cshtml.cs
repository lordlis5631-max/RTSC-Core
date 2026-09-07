using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Admin;

public sealed class ReportsModel(AppDbContext db) : PageModel
{
    public int Users { get; private set; }
    public int ActiveUsers { get; private set; }
    public int Communities { get; private set; }
    public int PublishedCommunities { get; private set; }
    public int Events { get; private set; }
    public int PublishedEvents { get; private set; }
    public int Registrations { get; private set; }
    public int Attended { get; private set; }
    public int NoShow { get; private set; }
    public decimal AttendanceRate { get; private set; }
    public IReadOnlyList<EventRow> TopEvents { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Users = await db.Users.CountAsync();
        ActiveUsers = await db.Users.CountAsync(x => x.Status == UserStatus.Active);
        Communities = await db.Communities.CountAsync();
        PublishedCommunities = await db.Communities.CountAsync(x => x.Status == CommunityStatus.Published);
        Events = await db.Events.CountAsync();
        PublishedEvents = await db.Events.CountAsync(x => x.Status == EventStatus.Published || x.Status == EventStatus.Completed);
        Registrations = await db.EventParticipants.CountAsync(x => x.Status != ParticipantStatus.Cancelled);
        Attended = await db.EventParticipants.CountAsync(x => x.Status == ParticipantStatus.Attended);
        NoShow = await db.EventParticipants.CountAsync(x => x.Status == ParticipantStatus.NoShow);

        var decided = Attended + NoShow;
        AttendanceRate = decided == 0 ? 0 : Math.Round((decimal)Attended * 100m / decided, 1);

        TopEvents = await db.Events
            .AsNoTracking()
            .Where(x => x.Status == EventStatus.Published || x.Status == EventStatus.Completed)
            .Select(x => new EventRow(
                x.Id,
                x.Title,
                x.StartAt,
                x.Community.Name,
                x.Participants.Count(p => p.Status != ParticipantStatus.Cancelled),
                x.Participants.Count(p => p.Status == ParticipantStatus.Attended),
                x.Participants.Count(p => p.Status == ParticipantStatus.NoShow)))
            .OrderByDescending(x => x.Registered)
            .ThenByDescending(x => x.StartAt)
            .Take(20)
            .ToListAsync();
    }

    public sealed record EventRow(
        Guid Id,
        string Title,
        DateTimeOffset StartAt,
        string CommunityName,
        int Registered,
        int Attended,
        int NoShow)
    {
        public decimal AttendanceRate => Attended + NoShow == 0
            ? 0
            : Math.Round((decimal)Attended * 100m / (Attended + NoShow), 1);
    }
}
