using RTSC.Core.Domain;
using RTSC.Core.Features.Notifications;

namespace RTSC.Core.Integrations.Max;

public sealed class MaxNotificationSender(MaxApiClient maxClient) : INotificationChannelSender
{
    public NotificationChannel Channel => NotificationChannel.Max;

    public async Task<NotificationSendResult> SendAsync(
        ExternalAccount account,
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        var text = BuildText(notification);
        var result = await maxClient.SendTextAsync(account.ExternalUserId, text, cancellationToken);

        return result.IsSuccess
            ? NotificationSendResult.Success(result.ExternalMessageId)
            : NotificationSendResult.Failure(result.Error ?? "max_send_failed");
    }

    private string BuildText(Notification notification)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(notification.Title))
            parts.Add(notification.Title.Trim());

        if (!string.IsNullOrWhiteSpace(notification.Body))
            parts.Add(notification.Body.Trim());

        var targetUrl = maxClient.BuildAbsoluteUrl(notification.TargetUrl);
        if (!string.IsNullOrWhiteSpace(targetUrl))
            parts.Add(targetUrl);

        return string.Join("\n\n", parts);
    }
}
