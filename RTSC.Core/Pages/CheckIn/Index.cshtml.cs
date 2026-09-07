using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.CheckIn;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Pages.CheckIn;

[Authorize]
public sealed class IndexModel(AppDbContext db, AccessControlService access, CheckInTokenService tokens, NotificationService notifications) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Token { get; set; } = string.Empty;
    public TicketVm? Ticket { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!tokens.TryRead(Token, out var ticket)) { Error = "Ссылка повреждена или срок действия QR истёк."; return Page(); }
        if (!await access.CanManageEventAsync(ticket.EventId, User)) return Forbid();
        var participant = await db.EventParticipants
            .Include(x => x.Event)
            .SingleOrDefaultAsync(x => x.EventId == ticket.EventId && x.UserId == ticket.UserId);
        if (participant is null || participant.Status == ParticipantStatus.Cancelled) { Error = "Регистрация участника не найдена или отменена."; return Page(); }

        var wasAttended = participant.Status == ParticipantStatus.Attended;
        participant.Status = ParticipantStatus.Attended;
        participant.ConfirmedAt ??= DateTimeOffset.UtcNow;
        participant.AttendedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        if (!wasAttended)
        {
            await notifications.CreateAsync(
                ticket.UserId,
                "event.checkin",
                "Посещение отмечено",
                $"Ваше посещение мероприятия «{participant.Event.Title}» подтверждено.",
                $"/Events/{ticket.EventId}");
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        if (!tokens.TryRead(Token, out var ticket)) { Error = "Ссылка повреждена или срок действия QR истёк."; return; }
        if (!await access.CanManageEventAsync(ticket.EventId, User)) { Error = "У вас нет прав отмечать участников этого мероприятия."; return; }
        Ticket = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == ticket.EventId && x.UserId == ticket.UserId)
            .Select(x => new TicketVm(x.Event.Title, x.User.DisplayName, x.User.Email, x.Status.ToString()))
            .SingleOrDefaultAsync();
        if (Ticket is null) Error = "Участник по этому QR не найден.";
    }

    public sealed record TicketVm(string EventTitle, string UserName, string UserEmail, string Status);
}
