namespace RTSC.Core.Domain;

public enum EventCategory
{
    Other = 0,
    Education = 1,
    Career = 2,
    Technology = 3,
    Science = 4,
    Culture = 5,
    Sports = 6,
    Volunteering = 7,
    Games = 8,
    Creativity = 9,
    Entrepreneurship = 10,
    Health = 11
}

public sealed class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public List<EventTag> Events { get; set; } = [];
    public List<UserTagInterest> InterestedUsers { get; set; } = [];
}

public sealed class EventTag
{
    public Guid EventId { get; set; }
    public Guid TagId { get; set; }

    public Event Event { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

public sealed class UserTagInterest
{
    public Guid UserId { get; set; }
    public Guid TagId { get; set; }

    public User User { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

public sealed class UserCategoryInterest
{
    public Guid UserId { get; set; }
    public EventCategory Category { get; set; }

    public User User { get; set; } = null!;
}
