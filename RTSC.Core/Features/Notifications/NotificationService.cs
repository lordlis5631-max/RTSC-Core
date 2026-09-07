using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Features.Notifications;

public sealed class NotificationService(AppDbContext db)
{
    public async Task<Notification> CreateAsync(
        Guid userId,
        string type,
        string title,
        string body,
        string? targetUrl = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = Normalize(type, 100, "system"),
            Title = Normalize(title, 250, "RTSC"),
            Body = Normalize(body, 4000, string.Empty),
            TargetUrl = NormalizeOptional(targetUrl, 2000)
        };

        db.Notifications.Add(notification);

        var accounts = await db.ExternalAccounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.NotificationsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            var channel = ToChannel(account.Provider);
            if (channel is null) continue;

            notification.Deliveries.Add(new NotificationDelivery
            {
                Channel = channel.Value,
                NextAttemptAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.Notifications
            .Where(x => x.UserId == userId && x.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, (DateTimeOffset?)now), cancellationToken);
    }

    public static NotificationChannel? ToChannel(ExternalProvider provider) => provider switch
    {
        ExternalProvider.Max => NotificationChannel.Max,
        ExternalProvider.Telegram => NotificationChannel.Telegram,
        ExternalProvider.Vk => NotificationChannel.Vk,
        _ => null
    };

    public static ExternalProvider? ToProvider(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Max => ExternalProvider.Max,
        NotificationChannel.Telegram => ExternalProvider.Telegram,
        NotificationChannel.Vk => ExternalProvider.Vk,
        _ => null
    };

    private static string Normalize(string? value, int maxLength, string fallback)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
