using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages.Events;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CommunityId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Place { get; set; }
    [BindProperty(SupportsGet = true)] public string? From { get; set; }
    [BindProperty(SupportsGet = true)] public string? To { get; set; }
    [BindProperty(SupportsGet = true)] public bool WithLocation { get; set; }

    public IReadOnlyList<Item> Items { get; private set; } = [];
    public IReadOnlyList<CommunityVm> Communities { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        var query = db.Events.AsNoTracking().Where(x => x.Status == EventStatus.Published && x.StartAt >= cutoff);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = $"%{Q.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, term) ||
                EF.Functions.ILike(x.Description, term) ||
                EF.Functions.ILike(x.Place, term) ||
                EF.Functions.ILike(x.Community.Name, term));
        }

        if (CommunityId is not null)
            query = query.Where(x => x.CommunityId == CommunityId.Value);

        if (!string.IsNullOrWhiteSpace(Place))
        {
            var placeTerm = $"%{Place.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Place, placeTerm));
        }

        if (TryDateBoundary(From, false, out var from))
            query = query.Where(x => x.StartAt >= from);

        if (TryDateBoundary(To, true, out var toExclusive))
            query = query.Where(x => x.StartAt < toExclusive);

        if (WithLocation)
            query = query.Where(x => x.Latitude != null && x.Longitude != null);

        Items = await query
            .OrderBy(x => x.StartAt)
            .Select(x => new Item(
                x.Id,
                x.Title,
                x.Description,
                x.Place,
                x.StartAt,
                x.Community.Name,
                x.Capacity,
                x.Participants.Count(p => p.Status != ParticipantStatus.Cancelled),
                x.Latitude != null && x.Longitude != null))
            .ToListAsync();

        Communities = await db.Communities.AsNoTracking()
            .Where(x => x.Status == CommunityStatus.Published)
            .OrderBy(x => x.Name)
            .Select(x => new CommunityVm(x.Id, x.Name))
            .ToListAsync();
    }

    private static bool TryDateBoundary(string? value, bool exclusiveEnd, out DateTimeOffset result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value) || !DateTime.TryParse(value, out var parsed)) return false;
        var local = DateTime.SpecifyKind(parsed.Date.AddDays(exclusiveEnd ? 1 : 0), DateTimeKind.Local);
        result = new DateTimeOffset(local);
        return true;
    }

    public sealed record Item(
        Guid Id,
        string Title,
        string Description,
        string Place,
        DateTimeOffset StartAt,
        string CommunityName,
        int? Capacity,
        int Registered,
        bool HasLocation);

    public sealed record CommunityVm(Guid Id, string Name);
}
