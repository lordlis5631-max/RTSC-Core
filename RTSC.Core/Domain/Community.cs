namespace RTSC.Core.Domain;

public sealed class Community
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public CommunityStatus Status { get; set; } = CommunityStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CommunityMember> Members { get; set; } = [];
    public List<Event> Events { get; set; } = [];
}

public sealed class CommunityMember
{
    public Guid CommunityId { get; set; }
    public Guid UserId { get; set; }
    public CommunityMemberRole Role { get; set; } = CommunityMemberRole.Member;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public Community Community { get; set; } = null!;
    public User User { get; set; } = null!;
}
