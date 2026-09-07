using RTSC.Core.Domain;

namespace RTSC.Core.Features.Notifications;

public interface INotificationChannelSender
{
    NotificationChannel Channel { get; }

    Task<NotificationSendResult> SendAsync(
        ExternalAccount account,
        Notification notification,
        CancellationToken cancellationToken = default);
}

public sealed record NotificationSendResult(bool IsSuccess, string? ExternalMessageId, string? Error)
{
    public static NotificationSendResult Success(string? externalMessageId = null) => new(true, externalMessageId, null);
    public static NotificationSendResult Failure(string error) => new(false, null, error);
}
