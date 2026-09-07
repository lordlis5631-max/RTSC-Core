using RTSC.Core.Domain;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Integrations.Telegram;

public sealed class TelegramNotificationSender(TelegramApiClient client) : INotificationChannelSender
{
    public NotificationChannel Channel => NotificationChannel.Telegram;

    public async Task<NotificationSendResult> SendAsync(
        ExternalAccount account,
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(notification.Title)) parts.Add(notification.Title.Trim());
        if (!string.IsNullOrWhiteSpace(notification.Body)) parts.Add(notification.Body.Trim());

        var url = client.BuildAbsoluteUrl(notification.TargetUrl);
        if (!string.IsNullOrWhiteSpace(url)) parts.Add(url);

        var result = await client.SendTextAsync(account.ExternalUserId, string.Join("\n\n", parts), cancellationToken);
        return result.IsSuccess
            ? NotificationSendResult.Success(result.ExternalMessageId)
            : NotificationSendResult.Failure(result.Error ?? "telegram_send_failed");
    }
}
