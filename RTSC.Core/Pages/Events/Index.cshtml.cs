using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Personalization;

namespace RTSC.Core.Pages.Events;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CommunityId { get; set; }
    [BindProperty(SupportsGet = true)] public EventCategory? Category { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? TagId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Place { get; set; }
    [BindProperty(SupportsGet = true)] public string? From { get; set; }
    [BindProperty(SupportsGet = true)] public string? To { get; set; }
    [BindProperty(SupportsGet = true)] public bool WithLocation { get; set; }

    public IReadOnlyList<Item> Items { get; private set; } = [];
    public IReadOnlyList<CommunityVm> Communities { get; private set; } = [];
    public IReadOnlyList<CategoryVm> Categories { get; private set; } = [];
    public IReadOnlyList<TagVm> Tags { get; private set; } = [];

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
                EF.Functions.ILike(x.Community.Name, term) ||
                x.Tags.Any(t => EF.Functions.ILike(t.Tag.Name, term)));
        }

        if (CommunityId is not null)
            query = query.Where(x => x.CommunityId == CommunityId.Value);

        if (Category is not null)
            query = query.Where(x => x.Category == Category.Value);

        if (TagId is not null)
            query = query.Where(x => x.Tags.Any(t => t.TagId == TagId.Value));

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

        var rows = await query
            .OrderBy(x => x.StartAt)
            .Select(x => new Item(
                x.Id,
                x.Title,
                x.Description,
                x.Place,
                x.StartAt,
                x.Community.Name,
                EventCategoryCatalog.Label(x.Category),
                x.Capacity,
                x.Participants.Count(p => p.Status != ParticipantStatus.Cancelled),
                x.Latitude != null && x.Longitude != null,
                []))
            .ToListAsync();

        var eventIds = rows.Select(x => x.Id).ToArray();
        var tagRows = eventIds.Length == 0
            ? []
            : await db.EventTags.AsNoTracking()
                .Where(x => eventIds.Contains(x.EventId) && x.Tag.IsActive)
                .OrderBy(x => x.Tag.Name)
                .Select(x => new EventTagVm(x.EventId, x.Tag.Name))
                .ToListAsync();
        var tagsByEvent = tagRows.GroupBy(x => x.EventId).ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Select(t => t.Name).ToList());
        Items = rows.Select(x => x with { Tags = tagsByEvent.GetValueOrDefault(x.Id, []) }).ToList();

        Communities = await db.Communities.AsNoTracking()
            .Where(x => x.Status == CommunityStatus.Published)
            .OrderBy(x => x.Name)
            .Select(x => new CommunityVm(x.Id, x.Name))
            .ToListAsync();

        Categories = EventCategoryCatalog.All
            .Select(x => new CategoryVm(x, EventCategoryCatalog.Label(x)))
            .ToList();

        Tags = await db.Tags.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new TagVm(x.Id, x.Name))
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
        string Category,
        int? Capacity,
        int Registered,
        bool HasLocation,
        IReadOnlyList<string> Tags);

    public sealed record CommunityVm(Guid Id, string Name);
    public sealed record CategoryVm(EventCategory Value, string Label);
    public sealed record TagVm(Guid Id, string Name);
    private sealed record EventTagVm(Guid EventId, string Name);
}
