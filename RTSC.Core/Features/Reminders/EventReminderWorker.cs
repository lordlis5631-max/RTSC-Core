using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Features.Reminders;

public sealed class EventReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EventReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event reminder iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var now = DateTimeOffset.UtcNow;
        var latest = now.AddHours(24);

        var events = await db.Events
            .AsNoTracking()
            .Where(x => x.Status == EventStatus.Published && x.StartAt > now && x.StartAt <= latest)
            .OrderBy(x => x.StartAt)
            .Select(x => new { x.Id, x.Title, x.StartAt, x.Place })
            .ToListAsync(cancellationToken);

        foreach (var item in events)
        {
            var kind = item.StartAt <= now.AddHours(2)
                ? EventReminderKind.TwoHoursBefore
                : EventReminderKind.DayBefore;

            var userIds = await db.EventParticipants
                .AsNoTracking()
                .Where(x => x.EventId == item.Id &&
                    (x.Status == ParticipantStatus.Registered || x.Status == ParticipantStatus.Confirmed))
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);

            foreach (var userId in userIds)
            {
                var alreadySent = await db.EventReminders.AsNoTracking()
                    .AnyAsync(x => x.EventId == item.Id && x.UserId == userId && x.Kind == kind, cancellationToken);
                if (alreadySent) continue;

                var title = kind == EventReminderKind.TwoHoursBefore
                    ? "Мероприятие скоро начнётся"
                    : "Напоминание о мероприятии";

                var when = item.StartAt.ToString("dd.MM.yyyy HH:mm zzz");
                var body = kind == EventReminderKind.TwoHoursBefore
                    ? $"«{item.Title}» начнётся менее чем через 2 часа: {when}. Место: {item.Place}."
                    : $"Вы зарегистрированы на «{item.Title}». Начало: {when}. Место: {item.Place}.";

                try
                {
                    await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

                    var notification = await notificationService.CreateAsync(
                        userId,
                        kind == EventReminderKind.TwoHoursBefore ? "event.reminder-2h" : "event.reminder-24h",
                        title,
                        body,
                        $"/Events/{item.Id}",
                        cancellationToken);

                    db.EventReminders.Add(new EventReminder
                    {
                        EventId = item.Id,
                        UserId = userId,
                        Kind = kind,
                        NotificationId = notification.Id
                    });

                    await db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    logger.LogDebug(ex, "Reminder already created concurrently for event {EventId}, user {UserId}, kind {Kind}", item.Id, userId, kind);
                    db.ChangeTracker.Clear();
                }
            }
        }
    }
}
