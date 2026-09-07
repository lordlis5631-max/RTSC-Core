namespace RTSC.Core.Domain;

public sealed class PerformerRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid PerformerUserId { get; set; }
    public Guid AuthorUserId { get; set; }
    public int Score { get; set; }
    public Guid? CommentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ParticipantRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid ParticipantUserId { get; set; }
    public Guid AuthorUserId { get; set; }
    public int Score { get; set; }
    public Guid? CommentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorUserId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public CommentStatus Status { get; set; } = CommentStatus.Published;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
