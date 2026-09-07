using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Events;

[Authorize]
public sealed class AnalyticsModel(AppDbContext db, AccessControlService access) : PageModel
{
    public EventVm? Event { get; private set; }
    public int TotalRecords { get; private set; }
    public int ActiveParticipants { get; private set; }
    public int Registered { get; private set; }
    public int Confirmed { get; private set; }
    public int Attended { get; private set; }
    public int NoShow { get; private set; }
    public int Cancelled { get; private set; }
    public double ConfirmationRate { get; private set; }
    public double AttendanceRate { get; private set; }
    public double CancellationRate { get; private set; }
    public double? CapacityUse { get; private set; }
    public int PerformerRatingCount { get; private set; }
    public double? PerformerRatingAverage { get; private set; }
    public int ParticipantRatingCount { get; private set; }
    public double? ParticipantRatingAverage { get; private set; }
    public IReadOnlyList<RegistrationDayVm> RegistrationsByDay { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await access.CanManageEventAsync(id, User)) return Forbid();

        Event = await db.Events.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new EventVm(x.Id, x.Title, x.StartAt, x.Status.ToString(), x.Capacity))
            .SingleOrDefaultAsync();
        if (Event is null) return NotFound();

        var participants = await db.EventParticipants.AsNoTracking()
            .Where(x => x.EventId == id)
            .Select(x => new ParticipantSnapshot(x.Status, x.RegisteredAt))
            .ToListAsync();

        TotalRecords = participants.Count;
        Registered = participants.Count(x => x.Status == ParticipantStatus.Registered);
        Confirmed = participants.Count(x => x.Status == ParticipantStatus.Confirmed);
        Attended = participants.Count(x => x.Status == ParticipantStatus.Attended);
        NoShow = participants.Count(x => x.Status == ParticipantStatus.NoShow);
        Cancelled = participants.Count(x => x.Status == ParticipantStatus.Cancelled);
        ActiveParticipants = TotalRecords - Cancelled;

        ConfirmationRate = ActiveParticipants > 0
            ? (Confirmed + Attended) * 100d / ActiveParticipants
            : 0d;

        var attendanceBase = Attended + NoShow;
        AttendanceRate = attendanceBase > 0
            ? Attended * 100d / attendanceBase
            : ActiveParticipants > 0 ? Attended * 100d / ActiveParticipants : 0d;

        CancellationRate = TotalRecords > 0 ? Cancelled * 100d / TotalRecords : 0d;
        CapacityUse = Event.Capacity is > 0 ? ActiveParticipants * 100d / Event.Capacity.Value : null;

        var performerScores = await db.PerformerRatings.AsNoTracking()
            .Where(x => x.EventId == id)
            .Select(x => x.Score)
            .ToListAsync();
        PerformerRatingCount = performerScores.Count;
        PerformerRatingAverage = performerScores.Count > 0 ? performerScores.Average() : null;

        var participantScores = await db.ParticipantRatings.AsNoTracking()
            .Where(x => x.EventId == id)
            .Select(x => x.Score)
            .ToListAsync();
        ParticipantRatingCount = participantScores.Count;
        ParticipantRatingAverage = participantScores.Count > 0 ? participantScores.Average() : null;

        RegistrationsByDay = participants
            .GroupBy(x => x.RegisteredAt.ToLocalTime().Date)
            .OrderBy(x => x.Key)
            .Select(x => new RegistrationDayVm(x.Key, x.Count()))
            .TakeLast(30)
            .ToList();

        return Page();
    }

    private sealed record ParticipantSnapshot(ParticipantStatus Status, DateTimeOffset RegisteredAt);
    public sealed record EventVm(Guid Id, string Title, DateTimeOffset StartAt, string Status, int? Capacity);
    public sealed record RegistrationDayVm(DateTime Date, int Count);
}
