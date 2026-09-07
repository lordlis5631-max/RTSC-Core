using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Pages.Admin;

public sealed class CommunitiesModel(AppDbContext db, NotificationService notifications) : PageModel
{
    public IReadOnlyList<ItemVm> Items { get; private set; } = [];
    public async Task OnGetAsync() => Items = await db.Communities.AsNoTracking().OrderBy(x => x.Status == CommunityStatus.Moderation ? 0 : 1).ThenByDescending(x => x.CreatedAt).Select(x => new ItemVm(x.Id, x.Name, x.Status.ToString(), x.CreatedAt)).ToListAsync();
    public async Task<IActionResult> OnPostStatusAsync(Guid id, CommunityStatus status)
    {
        if (status is not (CommunityStatus.Published or CommunityStatus.Rejected or CommunityStatus.Archived))
            return BadRequest();

        var item = await db.Communities.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();

        item.Status = status;

        var ownerUserIds = await db.CommunityMembers
            .Where(x => x.CommunityId == id && x.Role == CommunityMemberRole.Owner)
            .Select(x => x.UserId)
            .ToListAsync();

        await db.SaveChangesAsync();

        foreach (var userId in ownerUserIds)
        {
            var title = status switch
            {
                CommunityStatus.Published => "Сообщество опубликовано",
                CommunityStatus.Rejected => "Сообщество отклонено",
                CommunityStatus.Archived => "Сообщество архивировано",
                _ => "Статус сообщества изменён"
            };

            await notifications.CreateAsync(
                userId,
                $"community.moderation-{status.ToString().ToLowerInvariant()}",
                title,
                $"Статус сообщества «{item.Name}» изменён: {status}.",
                $"/Communities/{item.Id}");
        }

        return RedirectToPage();
    }
    public sealed record ItemVm(Guid Id, string Name, string Status, DateTimeOffset CreatedAt);
}
