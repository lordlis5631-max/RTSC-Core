using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;

namespace RTSC.Core.Pages.Communities;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<Item> Items { get; private set; } = [];
    public IReadOnlyList<Item> Managed { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await db.Communities.AsNoTracking().Where(x => x.Status == CommunityStatus.Published).OrderBy(x => x.Name).Select(x => new Item(x.Id, x.Name, x.Description, x.Status.ToString())).ToListAsync();
        var uid = AccessControlService.GetUserId(User);
        if (AccessControlService.IsGlobalAdmin(User))
            Managed = await db.Communities.AsNoTracking().OrderBy(x => x.Name).Select(x => new Item(x.Id, x.Name, x.Description, x.Status.ToString())).ToListAsync();
        else if (uid is not null)
            Managed = await db.CommunityMembers.AsNoTracking().Where(x => x.UserId == uid.Value && (x.Role == CommunityMemberRole.Owner || x.Role == CommunityMemberRole.Admin)).OrderBy(x => x.Community.Name).Select(x => new Item(x.CommunityId, x.Community.Name, x.Community.Description, x.Community.Status.ToString())).ToListAsync();
    }
    public sealed record Item(Guid Id, string Name, string Description, string Status);
}
