namespace RTSC.Core.Domain;

public sealed class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommunityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string Place { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public DateTimeOffset? RegistrationStartAt { get; set; }
    public DateTimeOffset? RegistrationEndAt { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Community Community { get; set; } = null!;
    public List<EventParticipant> Participants { get; set; } = [];
    public List<EventPerformer> Performers { get; set; } = [];
}

public sealed class EventParticipant
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Registered;
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? AttendedAt { get; set; }

    public Event Event { get; set; } = null!;
    public User User { get; set; } = null!;
}

public sealed class EventPerformer
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Performer";

    public Event Event { get; set; } = null!;
    public User User { get; set; } = null!;
}
