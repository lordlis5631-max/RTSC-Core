using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Ratings;

public sealed class ParticipantModel(AppDbContext db) : PageModel
{
    public ProfileVm? Profile { get; private set; }
    public IReadOnlyList<EventVm> Events { get; private set; } = [];
    public IReadOnlyList<CommentVm> Comments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid userId)
    {
        var name = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.DisplayName).SingleOrDefaultAsync(); if (name is null) return NotFound();
        var eventGroups = await db.ParticipantRatings.AsNoTracking().Where(x => x.ParticipantUserId == userId)
            .GroupBy(x => x.EventId).Select(g => new { EventId = g.Key, Last = g.Max(x => x.CreatedAt), Average = g.Average(x => x.Score), Ratings = g.Count() })
            .OrderByDescending(x => x.Last).Take(20).ToListAsync();
        var ids = eventGroups.Select(x => x.EventId).ToList(); var eventInfo = await db.Events.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var totalRatings = eventGroups.Sum(x => x.Ratings); var weighted = totalRatings == 0 ? (double?)null : eventGroups.Sum(x => x.Average * x.Ratings) / totalRatings;
        Profile = new ProfileVm(name, weighted, totalRatings, eventGroups.Count);
        Events = eventGroups.Where(x => eventInfo.ContainsKey(x.EventId)).Select(x => new EventVm(x.EventId, eventInfo[x.EventId].Title, eventInfo[x.EventId].StartAt, x.Average, x.Ratings)).ToList();
        var commentIds = await db.ParticipantRatings.AsNoTracking().Where(x => x.ParticipantUserId == userId && x.CommentId != null).Select(x => x.CommentId!.Value).ToListAsync();
        Comments = await db.Comments.AsNoTracking().Where(x => commentIds.Contains(x.Id) && x.Status == CommentStatus.Published).OrderByDescending(x => x.CreatedAt).Take(50).Select(x => new CommentVm(x.Text, x.CreatedAt)).ToListAsync();
        return Page();
    }

    public sealed record ProfileVm(string Name, double? Average, int Ratings, int Events);
    public sealed record EventVm(Guid EventId, string Title, DateTimeOffset Date, double Average, int Ratings);
    public sealed record CommentVm(string Text, DateTimeOffset CreatedAt);
}
