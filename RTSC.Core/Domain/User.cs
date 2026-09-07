namespace RTSC.Core.Domain;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public GlobalRole Role { get; set; } = GlobalRole.User;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ExternalAccount> ExternalAccounts { get; set; } = [];
    public List<Notification> Notifications { get; set; } = [];
    public List<UserTagInterest> TagInterests { get; set; } = [];
    public List<UserCategoryInterest> CategoryInterests { get; set; } = [];
}

public sealed class ExternalAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ExternalProvider Provider { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }

    public User User { get; set; } = null!;
}
