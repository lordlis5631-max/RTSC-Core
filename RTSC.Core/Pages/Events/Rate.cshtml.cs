using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.Moderation;

namespace RTSC.Core.Pages.Events;

[Authorize]
public sealed class RateModel(AppDbContext db, CommentModerationService moderation) : PageModel
{
    public Guid Id { get; private set; }
    public string EventTitle { get; private set; } = string.Empty;
    [TempData] public string? Message { get; set; }
    public IReadOnlyList<PerformerVm> Performers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await CanRateAsync(id)) return Forbid();
        await LoadAsync(id); return Page();
    }

    public async Task<IActionResult> OnPostRateAsync(Guid id, Guid performerUserId, int score, string? comment)
    {
        var uid = AccessControlService.GetUserId(User); if (uid is null) return Challenge(); if (!await CanRateAsync(id)) return Forbid();
        if (score is < 1 or > 5 || !await db.EventPerformers.AnyAsync(x => x.EventId == id && x.UserId == performerUserId)) return BadRequest();
        var rating = await db.PerformerRatings.SingleOrDefaultAsync(x => x.EventId == id && x.PerformerUserId == performerUserId && x.AuthorUserId == uid.Value);
        if (rating is null) { rating = new PerformerRating { EventId = id, PerformerUserId = performerUserId, AuthorUserId = uid.Value, Score = score }; db.PerformerRatings.Add(rating); } else rating.Score = score;
        await SaveCommentAsync(rating, uid.Value, id, performerUserId, comment);
        await db.SaveChangesAsync(); Message = "Оценка сохранена."; return RedirectToPage(new { id });
    }

    private async Task SaveCommentAsync(PerformerRating rating, Guid authorId, Guid eventId, Guid targetId, string? text)
    {
        var clean = text?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return;
        Comment? item = rating.CommentId is null ? null : await db.Comments.SingleOrDefaultAsync(x => x.Id == rating.CommentId.Value);
        if (item is null) { item = new Comment { AuthorUserId = authorId, EventId = eventId, TargetUserId = targetId }; db.Comments.Add(item); rating.CommentId = item.Id; }
        item.Text = clean; item.Status = moderation.GetInitialStatus(clean);
    }

    private async Task<bool> CanRateAsync(Guid eventId)
    {
        var uid = AccessControlService.GetUserId(User); if (uid is null) return false;
        var completed = await db.Events.AnyAsync(x => x.Id == eventId && x.Status == EventStatus.Completed);
        return completed && await db.EventParticipants.AnyAsync(x => x.EventId == eventId && x.UserId == uid.Value && x.Status == ParticipantStatus.Attended);
    }

    private async Task LoadAsync(Guid id)
    {
        Id = id; EventTitle = await db.Events.Where(x => x.Id == id).Select(x => x.Title).SingleOrDefaultAsync() ?? string.Empty;
        var uid = AccessControlService.GetUserId(User)!.Value;
        var baseItems = await db.EventPerformers.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.User.DisplayName).Select(x => new { x.UserId, x.User.DisplayName, x.Role }).ToListAsync();
        var ratings = await db.PerformerRatings.AsNoTracking().Where(x => x.EventId == id && x.AuthorUserId == uid).ToListAsync();
        var commentIds = ratings.Where(x => x.CommentId != null).Select(x => x.CommentId!.Value).ToList();
        var comments = await db.Comments.AsNoTracking().Where(x => commentIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Text);
        Performers = baseItems.Select(x => { var r = ratings.SingleOrDefault(y => y.PerformerUserId == x.UserId); return new PerformerVm(x.UserId, x.DisplayName, x.Role, r?.Score, r?.CommentId is Guid cid && comments.TryGetValue(cid, out var t) ? t : null); }).ToList();
    }

    public sealed record PerformerVm(Guid UserId, string Name, string Role, int? CurrentScore, string? CurrentComment);
}
