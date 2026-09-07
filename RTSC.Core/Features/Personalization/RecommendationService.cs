using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Features.Personalization;

public sealed class RecommendationService(AppDbContext db)
{
    public async Task<IReadOnlyList<Recommendation>> GetForUserAsync(Guid userId, int take = 8, CancellationToken cancellationToken = default)
    {
        var categories = await db.UserCategoryInterests.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Category)
            .ToListAsync(cancellationToken);

        var tagIds = await db.UserTagInterests.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.TagId)
            .ToListAsync(cancellationToken);

        if (categories.Count == 0 && tagIds.Count == 0)
            return [];

        var registeredEventIds = await db.EventParticipants.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status != ParticipantStatus.Cancelled)
            .Select(x => x.EventId)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var candidates = await db.Events.AsNoTracking()
            .Where(x => x.Status == EventStatus.Published && x.StartAt >= now && !registeredEventIds.Contains(x.Id))
            .OrderBy(x => x.StartAt)
            .Take(100)
            .Select(x => new Candidate(
                x.Id,
                x.Title,
                x.Community.Name,
                x.StartAt,
                x.Place,
                x.Category,
                x.Capacity,
                x.Participants.Count(p => p.Status != ParticipantStatus.Cancelled)))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return [];

        var candidateIds = candidates.Select(x => x.Id).ToArray();
        var matchingTags = tagIds.Count == 0
            ? []
            : await db.EventTags.AsNoTracking()
                .Where(x => candidateIds.Contains(x.EventId) && tagIds.Contains(x.TagId))
                .Select(x => new TagMatch(x.EventId, x.TagId, x.Tag.Name))
                .ToListAsync(cancellationToken);

        var tagsByEvent = matchingTags
            .GroupBy(x => x.EventId)
            .ToDictionary(x => x.Key, x => x.OrderBy(t => t.Name).ToList());

        var categorySet = categories.ToHashSet();
        var result = candidates
            .Select(candidate =>
            {
                var categoryMatch = categorySet.Contains(candidate.Category);
                var tagMatches = tagsByEvent.GetValueOrDefault(candidate.Id, []);
                var score = (categoryMatch ? 4 : 0) + Math.Min(tagMatches.Count, 4) * 2;

                var reasons = new List<string>();
                if (categoryMatch)
                    reasons.Add($"интерес: {EventCategoryCatalog.Label(candidate.Category)}");
                if (tagMatches.Count > 0)
                    reasons.Add($"теги: {string.Join(", ", tagMatches.Select(x => x.Name).Take(4))}");

                return new Recommendation(
                    candidate.Id,
                    candidate.Title,
                    candidate.CommunityName,
                    candidate.StartAt,
                    candidate.Place,
                    candidate.Category,
                    candidate.Capacity,
                    candidate.Registered,
                    score,
                    string.Join(" · ", reasons));
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.StartAt)
            .Take(Math.Clamp(take, 1, 20))
            .ToList();

        return result;
    }

    private sealed record Candidate(
        Guid Id,
        string Title,
        string CommunityName,
        DateTimeOffset StartAt,
        string Place,
        EventCategory Category,
        int? Capacity,
        int Registered);

    private sealed record TagMatch(Guid EventId, Guid TagId, string Name);

    public sealed record Recommendation(
        Guid EventId,
        string Title,
        string CommunityName,
        DateTimeOffset StartAt,
        string Place,
        EventCategory Category,
        int? Capacity,
        int Registered,
        int Score,
        string Reason);
}
