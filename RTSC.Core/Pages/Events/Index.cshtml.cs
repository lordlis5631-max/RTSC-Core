using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Events;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<Item> Items { get; private set; } = [];
    public async Task OnGetAsync()
    {
        Items = await db.Events.AsNoTracking().Where(x => x.Status == EventStatus.Published && x.StartAt >= DateTimeOffset.UtcNow.AddDays(-1)).OrderBy(x => x.StartAt).Select(x => new Item(x.Id, x.Title, x.Description, x.Place, x.StartAt, x.Community.Name)).ToListAsync();
    }
    public sealed record Item(Guid Id, string Title, string Description, string Place, DateTimeOffset StartAt, string CommunityName);
}
