using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Communities;

public sealed class DetailsModel(AppDbContext db, AccessControlService access) : PageModel
{
    public ItemVm? Item { get; private set; }
    public bool CanManage { get; private set; }
    public IReadOnlyList<EventVm> Events { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        CanManage = await access.CanManageCommunityAsync(id, User);
        Item = await db.Communities.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ItemVm(x.Id, x.Name, x.Description, x.Status.ToString(), x.Members.Count))
            .SingleOrDefaultAsync();
        if (Item is null || (Item.Status != CommunityStatus.Published.ToString() && !CanManage)) return NotFound();

        var query = db.Events.AsNoTracking().Where(x => x.CommunityId == id);
        if (!CanManage) query = query.Where(x => x.Status == EventStatus.Published);
        Events = await query.OrderBy(x => x.StartAt).Select(x => new EventVm(x.Id, x.Title, x.StartAt, x.Place, x.Status.ToString())).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync(Guid id)
    {
        if (!await access.CanManageCommunityAsync(id, User)) return Forbid();
        var item = await db.Communities.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        if (item.Status is CommunityStatus.Draft or CommunityStatus.Rejected)
        {
            item.Status = CommunityStatus.Moderation;
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { id });
    }

    public sealed record ItemVm(Guid Id, string Name, string Description, string Status, int MemberCount);
    public sealed record EventVm(Guid Id, string Title, DateTimeOffset StartAt, string Place, string Status);
}
