using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Access;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Features.Events;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        group.MapGet("/", async (AppDbContext db) => Results.Ok(await db.Events
            .AsNoTracking()
            .Where(x => x.Status == EventStatus.Published && x.StartAt >= DateTimeOffset.UtcNow.AddDays(-1))
            .OrderBy(x => x.StartAt)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.StartAt,
                x.EndAt,
                x.Place,
                x.Capacity,
                x.CommunityId,
                communityName = x.Community.Name,
                registered = x.Participants.Count(p => p.Status != ParticipantStatus.Cancelled)
            })
            .ToListAsync()));

        group.MapGet("/{eventId:guid}", async (Guid eventId, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            var item = await db.Events.AsNoTracking().Where(x => x.Id == eventId)
                .Select(x => new
                {
                    x.Id, x.Title, x.Description, x.StartAt, x.EndAt, x.Place, x.Capacity,
                    x.Status, x.RegistrationStartAt, x.RegistrationEndAt, x.ImageUrl,
                    x.CommunityId, communityName = x.Community.Name,
                    registered = x.Participants.Count(p => p.Status != ParticipantStatus.Cancelled),
                    performers = x.Performers.Select(p => new { p.UserId, p.User.DisplayName, p.Role }).ToList()
                }).SingleOrDefaultAsync();

            if (item is null) return Results.NotFound();
            if (item.Status != EventStatus.Published && !await access.CanManageEventAsync(eventId, http.User)) return Results.NotFound();
            return Results.Ok(item);
        });

        group.MapPost("/", async (CreateEventRequest request, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            if (!await access.CanManageCommunityAsync(request.CommunityId, http.User)) return Results.Forbid();
            var error = ValidateEvent(request.Title, request.StartAt, request.EndAt, request.Capacity, request.RegistrationStartAt, request.RegistrationEndAt);
            if (error is not null) return Results.BadRequest(new { error });

            var evt = new Event
            {
                CommunityId = request.CommunityId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                StartAt = request.StartAt,
                EndAt = request.EndAt,
                Place = request.Place?.Trim() ?? string.Empty,
                Capacity = request.Capacity,
                RegistrationStartAt = request.RegistrationStartAt,
                RegistrationEndAt = request.RegistrationEndAt,
                ImageUrl = NormalizeOptional(request.ImageUrl),
                Status = EventStatus.Draft
            };

            db.Events.Add(evt);
            await db.SaveChangesAsync();
            return Results.Created($"/api/events/{evt.Id}", new { evt.Id, evt.Title, status = evt.Status.ToString() });
        }).RequireAuthorization();

        group.MapPut("/{eventId:guid}", async (Guid eventId, UpdateEventRequest request, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            if (!await access.CanManageEventAsync(eventId, http.User)) return Results.Forbid();
            var evt = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId);
            if (evt is null) return Results.NotFound();
            if (evt.Status is EventStatus.Completed or EventStatus.Archived) return Results.BadRequest(new { error = "event cannot be edited" });
            var error = ValidateEvent(request.Title, request.StartAt, request.EndAt, request.Capacity, request.RegistrationStartAt, request.RegistrationEndAt);
            if (error is not null) return Results.BadRequest(new { error });

            evt.Title = request.Title.Trim();
            evt.Description = request.Description?.Trim() ?? string.Empty;
            evt.StartAt = request.StartAt;
            evt.EndAt = request.EndAt;
            evt.Place = request.Place?.Trim() ?? string.Empty;
            evt.Capacity = request.Capacity;
            evt.RegistrationStartAt = request.RegistrationStartAt;
            evt.RegistrationEndAt = request.RegistrationEndAt;
            evt.ImageUrl = NormalizeOptional(request.ImageUrl);
            if (evt.Status == EventStatus.Cancelled) evt.Status = EventStatus.Draft;
            await db.SaveChangesAsync();
            return Results.Ok(new { status = evt.Status.ToString() });
        }).RequireAuthorization();

        group.MapPost("/{eventId:guid}/submit", async (Guid eventId, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            if (!await access.CanManageEventAsync(eventId, http.User)) return Results.Forbid();
            var evt = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId);
            if (evt is null) return Results.NotFound();
            if (evt.Status != EventStatus.Draft) return Results.BadRequest(new { error = "only draft can be submitted" });
            evt.Status = EventStatus.Moderation;
            await db.SaveChangesAsync();
            return Results.Ok(new { status = evt.Status.ToString() });
        }).RequireAuthorization();

        group.MapPost("/{eventId:guid}/register", async (Guid eventId, HttpContext http, AppDbContext db, NotificationService notifications) =>
        {
            var userId = AccessControlService.GetUserId(http.User);
            if (userId is null) return Results.Unauthorized();

            var evt = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.Status == EventStatus.Published);
            if (evt is null) return Results.NotFound();
            var now = DateTimeOffset.UtcNow;
            if (evt.RegistrationStartAt is not null && evt.RegistrationStartAt > now)
                return Results.BadRequest(new { error = "registration has not started" });
            if (evt.RegistrationEndAt is not null && evt.RegistrationEndAt < now)
                return Results.BadRequest(new { error = "registration is closed" });

            var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId.Value);
            if (participant is not null && participant.Status != ParticipantStatus.Cancelled)
                return Results.Conflict(new { error = "already registered" });

            if (evt.Capacity is int capacity)
            {
                var activeCount = await db.EventParticipants.CountAsync(x => x.EventId == eventId && x.Status != ParticipantStatus.Cancelled);
                if (activeCount >= capacity) return Results.Conflict(new { error = "event capacity reached" });
            }

            if (participant is null)
                db.EventParticipants.Add(new EventParticipant { EventId = eventId, UserId = userId.Value });
            else
            {
                participant.Status = ParticipantStatus.Registered;
                participant.RegisteredAt = now;
                participant.ConfirmedAt = null;
                participant.AttendedAt = null;
            }

            await db.SaveChangesAsync();

            await notifications.CreateAsync(
                userId.Value,
                "event.registration",
                "Вы зарегистрированы",
                $"Регистрация на мероприятие «{evt.Title}» сохранена.",
                $"/Events/{eventId}");

            return Results.Ok(new { status = "registered" });
        }).RequireAuthorization();

        group.MapPost("/{eventId:guid}/cancel-registration", async (Guid eventId, HttpContext http, AppDbContext db, NotificationService notifications) =>
        {
            var userId = AccessControlService.GetUserId(http.User);
            if (userId is null) return Results.Unauthorized();
            var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId.Value);
            if (participant is null) return Results.NotFound();
            participant.Status = ParticipantStatus.Cancelled;
            await db.SaveChangesAsync();

            var title = await db.Events.Where(x => x.Id == eventId).Select(x => x.Title).SingleOrDefaultAsync() ?? "мероприятие";
            await notifications.CreateAsync(
                userId.Value,
                "event.registration-cancelled",
                "Регистрация отменена",
                $"Вы отменили участие в мероприятии «{title}».",
                $"/Events/{eventId}");

            return Results.Ok(new { status = "cancelled" });
        }).RequireAuthorization();

        group.MapGet("/{eventId:guid}/participants", async (Guid eventId, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            if (!await access.CanManageEventAsync(eventId, http.User)) return Results.Forbid();
            var items = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == eventId)
                .OrderBy(x => x.User.DisplayName)
                .Select(x => new { x.UserId, x.User.DisplayName, x.User.Email, x.Status, x.RegisteredAt, x.AttendedAt })
                .ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization();

        group.MapPost("/{eventId:guid}/participants/{userId:guid}/status", async (Guid eventId, Guid userId, ParticipantStatusRequest request, HttpContext http, AppDbContext db, AccessControlService access, NotificationService notifications) =>
        {
            if (!await access.CanManageEventAsync(eventId, http.User)) return Results.Forbid();
            var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);
            if (participant is null) return Results.NotFound();
            participant.Status = request.Status;
            participant.ConfirmedAt = request.Status is ParticipantStatus.Confirmed or ParticipantStatus.Attended ? participant.ConfirmedAt ?? DateTimeOffset.UtcNow : participant.ConfirmedAt;
            participant.AttendedAt = request.Status == ParticipantStatus.Attended ? DateTimeOffset.UtcNow : null;
            await db.SaveChangesAsync();

            if (request.Status is ParticipantStatus.Confirmed or ParticipantStatus.Attended or ParticipantStatus.NoShow)
            {
                var evt = await db.Events.AsNoTracking().Where(x => x.Id == eventId).Select(x => new { x.Title }).SingleAsync();
                var body = request.Status switch
                {
                    ParticipantStatus.Confirmed => $"Ваше участие в «{evt.Title}» подтверждено.",
                    ParticipantStatus.Attended => $"Посещение «{evt.Title}» отмечено.",
                    ParticipantStatus.NoShow => $"Для «{evt.Title}» отмечен статус «не пришёл».",
                    _ => string.Empty
                };

                await notifications.CreateAsync(
                    userId,
                    $"event.participant-{request.Status.ToString().ToLowerInvariant()}",
                    "Статус участия изменён",
                    body,
                    $"/Events/{eventId}");
            }

            return Results.Ok(new { status = participant.Status.ToString() });
        }).RequireAuthorization();

        group.MapPost("/{eventId:guid}/performers", async (Guid eventId, AddPerformerRequest request, HttpContext http, AppDbContext db, AccessControlService access, NotificationService notifications) =>
        {
            if (!await access.CanManageEventAsync(eventId, http.User)) return Results.Forbid();
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Status == UserStatus.Active);
            if (user is null) return Results.NotFound(new { error = "user not found" });

            var performer = await db.EventPerformers.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == user.Id);
            if (performer is null)
                db.EventPerformers.Add(new EventPerformer { EventId = eventId, UserId = user.Id, Role = NormalizeRole(request.Role) });
            else
                performer.Role = NormalizeRole(request.Role);

            await db.SaveChangesAsync();

            var eventTitle = await db.Events.AsNoTracking().Where(x => x.Id == eventId).Select(x => x.Title).SingleAsync();
            await notifications.CreateAsync(
                user.Id,
                "event.performer-assigned",
                "Вы назначены исполнителем",
                $"Вы назначены исполнителем мероприятия «{eventTitle}». Роль: {NormalizeRole(request.Role)}.",
                $"/Events/{eventId}");

            return Results.Ok(new { user.Id, user.DisplayName, role = NormalizeRole(request.Role) });
        }).RequireAuthorization();

        group.MapDelete("/{eventId:guid}/performers/{userId:guid}", async (Guid eventId, Guid userId, HttpContext http, AppDbContext db, AccessControlService access) =>
        {
            if (!await access.CanManageEventAsync(eventId, http.User)) return Results.Forbid();
            var performer = await db.EventPerformers.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);
            if (performer is null) return Results.NotFound();
            db.EventPerformers.Remove(performer);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    private static string? ValidateEvent(string title, DateTimeOffset startAt, DateTimeOffset? endAt, int? capacity, DateTimeOffset? registrationStartAt, DateTimeOffset? registrationEndAt)
    {
        if (string.IsNullOrWhiteSpace(title)) return "title is required";
        if (endAt is not null && endAt <= startAt) return "endAt must be after startAt";
        if (capacity is <= 0) return "capacity must be positive";
        if (registrationStartAt is not null && registrationEndAt is not null && registrationEndAt <= registrationStartAt)
            return "registrationEndAt must be after registrationStartAt";
        return null;
    }

    private static string NormalizeRole(string? role) => string.IsNullOrWhiteSpace(role) ? "Исполнитель" : role.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record CreateEventRequest(Guid CommunityId, string Title, string? Description, DateTimeOffset StartAt, DateTimeOffset? EndAt, string? Place, int? Capacity, DateTimeOffset? RegistrationStartAt, DateTimeOffset? RegistrationEndAt, string? ImageUrl);
    public sealed record UpdateEventRequest(string Title, string? Description, DateTimeOffset StartAt, DateTimeOffset? EndAt, string? Place, int? Capacity, DateTimeOffset? RegistrationStartAt, DateTimeOffset? RegistrationEndAt, string? ImageUrl);
    public sealed record ParticipantStatusRequest(ParticipantStatus Status);
    public sealed record AddPerformerRequest(string Email, string? Role);
}
