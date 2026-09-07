using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.Moderation;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Pages.Events;

[Authorize]
public sealed class ManageModel(AppDbContext db, AccessControlService access, CommentModerationService moderation, NotificationService notifications) : PageModel
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    [BindProperty] public string PerformerEmail { get; set; } = string.Empty;
    [BindProperty] public string? PerformerRole { get; set; }
    [TempData] public string? Message { get; set; }
    public IReadOnlyList<ParticipantVm> Participants { get; private set; } = [];
    public IReadOnlyList<PerformerVm> Performers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id) { if (!await access.CanManageEventAsync(id, User)) return Forbid(); await LoadAsync(id); return Page(); }

    public async Task<IActionResult> OnPostSubmitAsync(Guid id)
    {
        if (!await access.CanManageEventAsync(id, User)) return Forbid(); var evt = await db.Events.SingleOrDefaultAsync(x => x.Id == id); if (evt is null) return NotFound();
        if (evt.Status == EventStatus.Draft) { evt.Status = EventStatus.Moderation; await db.SaveChangesAsync(); Message = "Мероприятие отправлено на модерацию."; }
        return RedirectToPage(new { id });
    }


    public async Task<IActionResult> OnPostCompleteAsync(Guid id)
    {
        if (!await access.CanManageEventAsync(id, User)) return Forbid();
        var evt = await db.Events.SingleOrDefaultAsync(x => x.Id == id); if (evt is null) return NotFound();
        if (evt.Status == EventStatus.Published)
        {
            evt.Status = EventStatus.Completed;
            var attendedUserIds = await db.EventParticipants
                .Where(x => x.EventId == id && x.Status == ParticipantStatus.Attended)
                .Select(x => x.UserId)
                .ToListAsync();

            await db.SaveChangesAsync();

            foreach (var participantUserId in attendedUserIds)
            {
                await notifications.CreateAsync(
                    participantUserId,
                    "event.completed",
                    "Мероприятие завершено",
                    $"«{evt.Title}» завершено. Теперь вы можете оценить исполнителей.",
                    $"/Events/{id}/Rate");
            }

            Message = "Мероприятие завершено. Участники теперь могут оценивать исполнителей.";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRateParticipantAsync(Guid id, Guid userId, int score, string? comment)
    {
        if (!await access.CanManageEventAsync(id, User)) return Forbid(); if (score is < 1 or > 5) return BadRequest();
        if (!await db.Events.AnyAsync(x => x.Id == id && x.Status == EventStatus.Completed)) return BadRequest();
        var authorId = AccessControlService.GetUserId(User); if (authorId is null) return Challenge();
        var attended = await db.EventParticipants.AnyAsync(x => x.EventId == id && x.UserId == userId && x.Status == ParticipantStatus.Attended); if (!attended) return BadRequest();
        var rating = await db.ParticipantRatings.SingleOrDefaultAsync(x => x.EventId == id && x.ParticipantUserId == userId && x.AuthorUserId == authorId.Value);
        if (rating is null) { rating = new ParticipantRating { EventId = id, ParticipantUserId = userId, AuthorUserId = authorId.Value, Score = score }; db.ParticipantRatings.Add(rating); } else rating.Score = score;
        if (!string.IsNullOrWhiteSpace(comment))
        {
            var clean = comment.Trim(); Comment? c = rating.CommentId is null ? null : await db.Comments.SingleOrDefaultAsync(x => x.Id == rating.CommentId.Value);
            if (c is null) { c = new Comment { AuthorUserId = authorId.Value, EventId = id, TargetUserId = userId }; db.Comments.Add(c); rating.CommentId = c.Id; }
            c.Text = clean; c.Status = moderation.GetInitialStatus(clean);
        }
        await db.SaveChangesAsync();
        var eventTitle = await db.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Title).SingleAsync();
        await notifications.CreateAsync(
            userId,
            "rating.participant-received",
            "Новая оценка",
            $"Организатор мероприятия «{eventTitle}» поставил вам оценку {score}/5.",
            $"/Ratings/Participant/{userId}");
        Message = "Оценка участника сохранена.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddPerformerAsync(Guid id)
    {
        if (!await access.CanManageEventAsync(id, User)) return Forbid(); var email = PerformerEmail.Trim().ToLowerInvariant(); var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Status == UserStatus.Active);
        if (user is null) { Message = "Пользователь с таким email не найден."; return RedirectToPage(new { id }); }
        var item = await db.EventPerformers.SingleOrDefaultAsync(x => x.EventId == id && x.UserId == user.Id); var role = string.IsNullOrWhiteSpace(PerformerRole) ? "Исполнитель" : PerformerRole.Trim();
        if (item is null) db.EventPerformers.Add(new EventPerformer { EventId = id, UserId = user.Id, Role = role }); else item.Role = role;
        await db.SaveChangesAsync();
        var eventTitle = await db.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Title).SingleAsync();
        await notifications.CreateAsync(
            user.Id,
            "event.performer-assigned",
            "Вы назначены исполнителем",
            $"Вы назначены исполнителем мероприятия «{eventTitle}». Роль: {role}.",
            $"/Events/{id}");
        Message = "Исполнитель сохранён.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemovePerformerAsync(Guid id, Guid userId)
    {
        if (!await access.CanManageEventAsync(id, User)) return Forbid(); var item = await db.EventPerformers.SingleOrDefaultAsync(x => x.EventId == id && x.UserId == userId); if (item is not null) { db.EventPerformers.Remove(item); await db.SaveChangesAsync(); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostParticipantStatusAsync(Guid id, Guid userId, ParticipantStatus status)
    {
        if (!await access.CanManageEventAsync(id, User)) return Forbid(); var item = await db.EventParticipants.SingleOrDefaultAsync(x => x.EventId == id && x.UserId == userId); if (item is null) return NotFound();
        item.Status = status;
        if (status is ParticipantStatus.Confirmed or ParticipantStatus.Attended) item.ConfirmedAt ??= DateTimeOffset.UtcNow;
        item.AttendedAt = status == ParticipantStatus.Attended ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync();

        if (status is ParticipantStatus.Confirmed or ParticipantStatus.Attended or ParticipantStatus.NoShow)
        {
            var title = await db.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Title).SingleAsync();
            var body = status switch
            {
                ParticipantStatus.Confirmed => $"Ваше участие в «{title}» подтверждено.",
                ParticipantStatus.Attended => $"Посещение «{title}» отмечено.",
                ParticipantStatus.NoShow => $"Для «{title}» отмечен статус «не пришёл».",
                _ => string.Empty
            };

            await notifications.CreateAsync(
                userId,
                $"event.participant-{status.ToString().ToLowerInvariant()}",
                "Статус участия изменён",
                body,
                $"/Events/{id}");
        }

        return RedirectToPage(new { id });
    }

    private async Task LoadAsync(Guid id)
    {
        Id = id; var evt = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id); if (evt is null) return; Title = evt.Title; Status = evt.Status.ToString();
        Participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.User.DisplayName).Select(x => new ParticipantVm(x.UserId, x.User.DisplayName, x.User.Email, x.Status.ToString())).ToListAsync();
        Performers = await db.EventPerformers.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.User.DisplayName).Select(x => new PerformerVm(x.UserId, x.User.DisplayName, x.User.Email, x.Role)).ToListAsync();
    }

    public sealed record ParticipantVm(Guid UserId, string Name, string Email, string Status);
    public sealed record PerformerVm(Guid UserId, string Name, string Email, string Role);
}
