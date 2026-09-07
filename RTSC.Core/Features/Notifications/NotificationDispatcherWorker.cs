using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Features.Notifications;

public sealed class NotificationDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDispatcherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                    await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification dispatcher iteration failed");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var processed = 0;

        for (var i = 0; i < 20; i++)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var senders = scope.ServiceProvider.GetServices<INotificationChannelSender>()
                .ToDictionary(x => x.Channel);

            var now = DateTimeOffset.UtcNow;
            var staleSendingBefore = now.AddMinutes(-5);

            var delivery = await db.NotificationDeliveries
                .Include(x => x.Notification)
                    .ThenInclude(x => x.User)
                        .ThenInclude(x => x.ExternalAccounts)
                .Where(x =>
                    ((x.Status == NotificationDeliveryStatus.Pending || x.Status == NotificationDeliveryStatus.Failed) &&
                     x.NextAttemptAt <= now &&
                     x.Attempts < MaxAttempts) ||
                    (x.Status == NotificationDeliveryStatus.Sending &&
                     x.LastAttemptAt != null &&
                     x.LastAttemptAt < staleSendingBefore))
                .OrderBy(x => x.NextAttemptAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (delivery is null)
                break;

            processed++;
            delivery.Status = NotificationDeliveryStatus.Sending;
            delivery.Attempts++;
            delivery.LastAttemptAt = now;
            await db.SaveChangesAsync(cancellationToken);

            var provider = NotificationService.ToProvider(delivery.Channel);
            var account = provider is null
                ? null
                : delivery.Notification.User.ExternalAccounts.FirstOrDefault(x =>
                    x.Provider == provider.Value && x.NotificationsEnabled);

            if (account is null)
            {
                delivery.Status = NotificationDeliveryStatus.Dead;
                delivery.Error = "linked_account_not_found_or_notifications_disabled";
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (!senders.TryGetValue(delivery.Channel, out var sender))
            {
                delivery.Status = NotificationDeliveryStatus.Dead;
                delivery.Error = "channel_sender_not_registered";
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                var result = await sender.SendAsync(account, delivery.Notification, cancellationToken);
                if (result.IsSuccess)
                {
                    delivery.Status = NotificationDeliveryStatus.Sent;
                    delivery.SentAt = DateTimeOffset.UtcNow;
                    delivery.ExternalMessageId = result.ExternalMessageId;
                    delivery.Error = null;
                }
                else
                {
                    MarkFailure(delivery, result.Error ?? "send_failed");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification delivery {DeliveryId} failed", delivery.Id);
                MarkFailure(delivery, ex.Message);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }

    private static void MarkFailure(NotificationDelivery delivery, string error)
    {
        delivery.Error = error.Length <= 2000 ? error : error[..2000];

        if (delivery.Attempts >= MaxAttempts)
        {
            delivery.Status = NotificationDeliveryStatus.Dead;
            return;
        }

        delivery.Status = NotificationDeliveryStatus.Failed;
        var delaySeconds = Math.Min(1800, 30 * (int)Math.Pow(2, Math.Max(0, delivery.Attempts - 1)));
        delivery.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
    }
}
