using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.Moderation;

namespace RTSC.Core.Features.Ratings;

public static class RatingEndpoints
{
    public static IEndpointRouteBuilder MapRatingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ratings").WithTags("Ratings");

        group.MapPost("/performers", async (PerformerRatingRequest request, HttpContext http, AppDbContext db, CommentModerationService moderation) =>
        {
            var authorId = AccessControlService.GetUserId(http.User);
            if (authorId is null) return Results.Unauthorized();
            if (request.Score is < 1 or > 5) return Results.BadRequest(new { error = "score must be 1..5" });

            var eventCompleted = await db.Events.AnyAsync(x => x.Id == request.EventId && x.Status == EventStatus.Completed);
            var participated = await db.EventParticipants.AnyAsync(x => x.EventId == request.EventId && x.UserId == authorId.Value && x.Status == ParticipantStatus.Attended);
            var performerExists = await db.EventPerformers.AnyAsync(x => x.EventId == request.EventId && x.UserId == request.PerformerUserId);
            if (!eventCompleted || !participated || !performerExists) return Results.Forbid();
            if (await db.PerformerRatings.AnyAsync(x => x.EventId == request.EventId && x.PerformerUserId == request.PerformerUserId && x.AuthorUserId == authorId.Value))
                return Results.Conflict(new { error = "rating already exists" });

            var commentId = AddCommentIfAny(request.Comment, authorId.Value, request.EventId, request.PerformerUserId, db, moderation);
            db.PerformerRatings.Add(new PerformerRating
            {
                EventId = request.EventId,
                PerformerUserId = request.PerformerUserId,
                AuthorUserId = authorId.Value,
                Score = request.Score,
                CommentId = commentId
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { status = "saved" });
        }).RequireAuthorization();

        group.MapPost("/participants", async (ParticipantRatingRequest request, HttpContext http, AppDbContext db, AccessControlService access, CommentModerationService moderation) =>
        {
            var authorId = AccessControlService.GetUserId(http.User);
            if (authorId is null) return Results.Unauthorized();
            if (request.Score is < 1 or > 5) return Results.BadRequest(new { error = "score must be 1..5" });
            if (!await access.CanManageEventAsync(request.EventId, http.User)) return Results.Forbid();
            if (!await db.Events.AnyAsync(x => x.Id == request.EventId && x.Status == EventStatus.Completed)) return Results.BadRequest(new { error = "event is not completed" });

            var attended = await db.EventParticipants.AnyAsync(x => x.EventId == request.EventId && x.UserId == request.ParticipantUserId && x.Status == ParticipantStatus.Attended);
            if (!attended) return Results.BadRequest(new { error = "participant has not attended this event" });
            if (await db.ParticipantRatings.AnyAsync(x => x.EventId == request.EventId && x.ParticipantUserId == request.ParticipantUserId && x.AuthorUserId == authorId.Value))
                return Results.Conflict(new { error = "rating already exists" });

            var commentId = AddCommentIfAny(request.Comment, authorId.Value, request.EventId, request.ParticipantUserId, db, moderation);
            db.ParticipantRatings.Add(new ParticipantRating
            {
                EventId = request.EventId,
                ParticipantUserId = request.ParticipantUserId,
                AuthorUserId = authorId.Value,
                Score = request.Score,
                CommentId = commentId
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { status = "saved" });
        }).RequireAuthorization();

        group.MapGet("/performers/{userId:guid}", async (Guid userId, AppDbContext db) =>
            Results.Ok(await BuildRatingSummaryAsync(userId, db.PerformerRatings.Where(x => x.PerformerUserId == userId).Select(x => new RatingProjection(x.EventId, x.Score, x.CreatedAt)))));

        group.MapGet("/participants/{userId:guid}", async (Guid userId, AppDbContext db) =>
            Results.Ok(await BuildRatingSummaryAsync(userId, db.ParticipantRatings.Where(x => x.ParticipantUserId == userId).Select(x => new RatingProjection(x.EventId, x.Score, x.CreatedAt)))));

        return app;
    }

    private static Guid? AddCommentIfAny(string? text, Guid authorId, Guid eventId, Guid targetUserId, AppDbContext db, CommentModerationService moderation)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var clean = text.Trim();
        var comment = new Comment
        {
            AuthorUserId = authorId,
            EventId = eventId,
            TargetUserId = targetUserId,
            Text = clean,
            Status = moderation.GetInitialStatus(clean)
        };
        db.Comments.Add(comment);
        return comment.Id;
    }

    private static async Task<object> BuildRatingSummaryAsync(Guid userId, IQueryable<RatingProjection> projected)
    {
        var latestTwentyEventIds = await projected
            .GroupBy(x => x.EventId)
            .Select(g => new { EventId = g.Key, Latest = g.Max(x => x.CreatedAt) })
            .OrderByDescending(x => x.Latest)
            .Take(20)
            .Select(x => x.EventId)
            .ToListAsync();

        var ratings = await projected.Where(x => latestTwentyEventIds.Contains(x.EventId)).Select(x => x.Score).ToListAsync();
        return new
        {
            userId,
            average = ratings.Count == 0 ? (double?)null : Math.Round(ratings.Average(), 2),
            ratings = ratings.Count,
            events = latestTwentyEventIds.Count
        };
    }

    private sealed record RatingProjection(Guid EventId, int Score, DateTimeOffset CreatedAt);
    public sealed record PerformerRatingRequest(Guid EventId, Guid PerformerUserId, int Score, string? Comment);
    public sealed record ParticipantRatingRequest(Guid EventId, Guid ParticipantUserId, int Score, string? Comment);
}
