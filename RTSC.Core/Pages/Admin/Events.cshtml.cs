using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Pages.Admin;

public sealed class EventsModel(AppDbContext db, NotificationService notifications) : PageModel
{
    public IReadOnlyList<ItemVm> Items { get; private set; } = [];
    public async Task OnGetAsync() => Items = await db.Events.AsNoTracking().OrderBy(x => x.Status == EventStatus.Moderation ? 0 : 1).ThenByDescending(x => x.CreatedAt).Select(x => new ItemVm(x.Id, x.Title, x.Community.Name, x.StartAt, x.Status.ToString())).ToListAsync();
    public async Task<IActionResult> OnPostStatusAsync(Guid id, EventStatus status)
    {
        if (status is not (EventStatus.Published or EventStatus.Draft or EventStatus.Cancelled or EventStatus.Archived))
            return BadRequest();

        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();

        item.Status = status;

        var managerUserIds = await db.CommunityMembers
            .Where(x => x.CommunityId == item.CommunityId &&
                        (x.Role == CommunityMemberRole.Owner || x.Role == CommunityMemberRole.Admin))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        await db.SaveChangesAsync();

        if (status is EventStatus.Published or EventStatus.Draft or EventStatus.Cancelled)
        {
            var title = status switch
            {
                EventStatus.Published => "Мероприятие опубликовано",
                EventStatus.Draft => "Мероприятие возвращено на доработку",
                EventStatus.Cancelled => "Мероприятие отменено",
                _ => "Статус мероприятия изменён"
            };

            foreach (var userId in managerUserIds)
            {
                await notifications.CreateAsync(
                    userId,
                    $"event.moderation-{status.ToString().ToLowerInvariant()}",
                    title,
                    $"Статус мероприятия «{item.Title}» изменён: {status}.",
                    $"/Events/{item.Id}");
            }
        }

        return RedirectToPage();
    }
    public sealed record ItemVm(Guid Id, string Title, string CommunityName, DateTimeOffset StartAt, string Status);
}
