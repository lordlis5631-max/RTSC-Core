using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Events;

public sealed class MapModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<MapItem> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        Items = await db.Events.AsNoTracking()
            .Where(x => x.Status == EventStatus.Published && x.StartAt >= cutoff && x.Latitude != null && x.Longitude != null)
            .OrderBy(x => x.StartAt)
            .Select(x => new MapItem(
                x.Id,
                x.Title,
                x.Place,
                x.StartAt,
                x.Community.Name,
                x.Latitude!.Value,
                x.Longitude!.Value))
            .ToListAsync();
    }

    public sealed record MapItem(
        Guid Id,
        string Title,
        string Place,
        DateTimeOffset StartAt,
        string CommunityName,
        double Latitude,
        double Longitude);
}
