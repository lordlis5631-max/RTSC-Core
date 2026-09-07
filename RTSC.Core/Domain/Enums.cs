namespace RTSC.Core.Domain;

public enum UserStatus
{
    Active = 1,
    Blocked = 2
}

public enum GlobalRole
{
    User = 1,
    Admin = 2,
    SuperAdmin = 3
}

public enum ExternalProvider
{
    Max = 1,
    Telegram = 2,
    Vk = 3,
    Rusoil = 4
}

public enum CommunityStatus
{
    Draft = 1,
    Moderation = 2,
    Published = 3,
    Rejected = 4,
    Archived = 5
}

public enum CommunityMemberRole
{
    Member = 1,
    Admin = 2,
    Owner = 3
}

public enum EventStatus
{
    Draft = 1,
    Moderation = 2,
    Published = 3,
    Completed = 4,
    Cancelled = 5,
    Archived = 6
}

public enum ParticipantStatus
{
    Registered = 1,
    Confirmed = 2,
    Attended = 3,
    Cancelled = 4,
    NoShow = 5
}

public enum CommentStatus
{
    Published = 1,
    Moderation = 2,
    Hidden = 3,
    Deleted = 4
}

public enum NotificationChannel
{
    Max = 1,
    Telegram = 2,
    Vk = 3,
    Email = 4
}

public enum NotificationDeliveryStatus
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    Dead = 5
}
