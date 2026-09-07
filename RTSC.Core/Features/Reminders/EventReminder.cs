namespace RTSC.Core.Features.Reminders;

public enum EventReminderKind
{
    DayBefore = 1,
    TwoHoursBefore = 2
}

public sealed class EventReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public EventReminderKind Kind { get; set; }
    public Guid NotificationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
