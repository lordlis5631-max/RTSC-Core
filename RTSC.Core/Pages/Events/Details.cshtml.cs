using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Events;

public sealed class DetailsModel(AppDbContext db, AccessControlService access) : PageModel
{
    public ItemVm? Item { get; private set; }
    public bool CanManage { get; private set; }
    public string? ParticipantStatus { get; private set; }
    public IReadOnlyList<PerformerVm> Performers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        CanManage = await access.CanManageEventAsync(id, User);
        Item = await db.Events.AsNoTracking().Where(x => x.Id == id).Select(x => new ItemVm(x.Id, x.Title, x.Description, x.StartAt, x.Place, x.Capacity, x.Status.ToString(), x.Community.Name, x.Participants.Count(p => p.Status != global::RTSC.Core.Domain.ParticipantStatus.Cancelled))).SingleOrDefaultAsync();
        if (Item is null) return NotFound();
        var uid = AccessControlService.GetUserId(User);
        var isParticipant = uid is not null && await db.EventParticipants.AnyAsync(x => x.EventId == id && x.UserId == uid.Value && x.Status != global::RTSC.Core.Domain.ParticipantStatus.Cancelled);
        if (Item.Status != EventStatus.Published.ToString() && !CanManage && !(Item.Status == EventStatus.Completed.ToString() && isParticipant)) return NotFound();
        if (uid is not null) ParticipantStatus = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == id && x.UserId == uid.Value).Select(x => x.Status.ToString()).SingleOrDefaultAsync();
        Performers = await db.EventPerformers.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.User.DisplayName).Select(x => new PerformerVm(x.UserId, x.User.DisplayName, x.Role)).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRegisterAsync(Guid id)
    {
        var uid = AccessControlService.GetUserId(User); if (uid is null) return Challenge();
        var evt = await db.Events.SingleOrDefaultAsync(x => x.Id == id && x.Status == EventStatus.Published); if (evt is null) return NotFound();
        var now = DateTimeOffset.UtcNow;
        if (evt.RegistrationStartAt is not null && evt.RegistrationStartAt > now || evt.RegistrationEndAt is not null && evt.RegistrationEndAt < now) return BadRequest();
        var existing = await db.EventParticipants.SingleOrDefaultAsync(x => x.EventId == id && x.UserId == uid.Value);
        if (existing is not null && existing.Status != global::RTSC.Core.Domain.ParticipantStatus.Cancelled) return RedirectToPage(new { id });
        if (evt.Capacity is int cap && await db.EventParticipants.CountAsync(x => x.EventId == id && x.Status != global::RTSC.Core.Domain.ParticipantStatus.Cancelled) >= cap) return StatusCode(StatusCodes.Status409Conflict);
        if (existing is null) db.EventParticipants.Add(new EventParticipant { EventId = id, UserId = uid.Value }); else { existing.Status = global::RTSC.Core.Domain.ParticipantStatus.Registered; existing.RegisteredAt = now; existing.AttendedAt = null; }
        await db.SaveChangesAsync(); return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        var uid = AccessControlService.GetUserId(User); if (uid is null) return Challenge();
        var item = await db.EventParticipants.SingleOrDefaultAsync(x => x.EventId == id && x.UserId == uid.Value); if (item is not null) { item.Status = global::RTSC.Core.Domain.ParticipantStatus.Cancelled; await db.SaveChangesAsync(); }
        return RedirectToPage(new { id });
    }

    public sealed record ItemVm(Guid Id, string Title, string Description, DateTimeOffset StartAt, string Place, int? Capacity, string Status, string CommunityName, int Registered);
    public sealed record PerformerVm(Guid UserId, string Name, string Role);
}
