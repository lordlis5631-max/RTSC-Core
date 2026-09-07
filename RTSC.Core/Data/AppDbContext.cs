using Microsoft.EntityFrameworkCore;
using RTSC.Core.Domain;
using RTSC.Core.Features.Reminders;

namespace RTSC.Core.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalAccount> ExternalAccounts => Set<ExternalAccount>();
    public DbSet<ExternalLinkToken> ExternalLinkTokens => Set<ExternalLinkToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<CommunityMember> CommunityMembers => Set<CommunityMember>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<EventPerformer> EventPerformers => Set<EventPerformer>();
    public DbSet<PerformerRating> PerformerRatings => Set<PerformerRating>();
    public DbSet<ParticipantRating> ParticipantRatings => Set<ParticipantRating>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<EventReminder> EventReminders => Set<EventReminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(50);
        });

        modelBuilder.Entity<ExternalAccount>(entity =>
        {
            entity.ToTable("external_accounts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Provider, x.ExternalUserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
            entity.Property(x => x.ExternalUserId).HasMaxLength(200);
            entity.Property(x => x.Username).HasMaxLength(200);
            entity.HasOne(x => x.User).WithMany(x => x.ExternalAccounts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExternalLinkToken>(entity =>
        {
            entity.ToTable("external_link_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Provider, x.ExpiresAt });
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(100);
            entity.Property(x => x.Title).HasMaxLength(250);
            entity.Property(x => x.Body).HasMaxLength(4000);
            entity.Property(x => x.TargetUrl).HasMaxLength(2000);
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasIndex(x => new { x.UserId, x.ReadAt });
            entity.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.ToTable("notification_deliveries");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
            entity.HasIndex(x => new { x.NotificationId, x.Channel }).IsUnique();
            entity.Property(x => x.ExternalMessageId).HasMaxLength(500);
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasOne(x => x.Notification).WithMany(x => x.Deliveries).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Community>(entity =>
        {
            entity.ToTable("communities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(250);
            entity.Property(x => x.LogoUrl).HasMaxLength(1000);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<CommunityMember>(entity =>
        {
            entity.ToTable("community_members");
            entity.HasKey(x => new { x.CommunityId, x.UserId });
            entity.HasOne(x => x.Community).WithMany(x => x.Members).HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("events", table =>
            {
                table.HasCheckConstraint("ck_events_latitude", "\"Latitude\" IS NULL OR (\"Latitude\" BETWEEN -90 AND 90)");
                table.HasCheckConstraint("ck_events_longitude", "\"Longitude\" IS NULL OR (\"Longitude\" BETWEEN -180 AND 180)");
                table.HasCheckConstraint("ck_events_coordinate_pair", "(\"Latitude\" IS NULL) = (\"Longitude\" IS NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300);
            entity.Property(x => x.Place).HasMaxLength(500);
            entity.Property(x => x.ImageUrl).HasMaxLength(1000);
            entity.HasOne(x => x.Community).WithMany(x => x.Events).HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.Status, x.StartAt });
            entity.HasIndex(x => new { x.Latitude, x.Longitude });
        });

        modelBuilder.Entity<EventParticipant>(entity =>
        {
            entity.ToTable("event_participants");
            entity.HasKey(x => new { x.EventId, x.UserId });
            entity.HasOne(x => x.Event).WithMany(x => x.Participants).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventPerformer>(entity =>
        {
            entity.ToTable("event_performers");
            entity.HasKey(x => new { x.EventId, x.UserId });
            entity.Property(x => x.Role).HasMaxLength(100);
            entity.HasOne(x => x.Event).WithMany(x => x.Performers).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PerformerRating>(entity =>
        {
            entity.ToTable("performer_ratings", t => t.HasCheckConstraint("ck_performer_rating_score", "\"Score\" BETWEEN 1 AND 5"));
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EventId, x.PerformerUserId, x.AuthorUserId }).IsUnique();
            entity.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.PerformerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Comment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ParticipantRating>(entity =>
        {
            entity.ToTable("participant_ratings", t => t.HasCheckConstraint("ck_participant_rating_score", "\"Score\" BETWEEN 1 AND 5"));
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EventId, x.ParticipantUserId, x.AuthorUserId }).IsUnique();
            entity.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ParticipantUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Comment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EventReminder>(entity =>
        {
            entity.ToTable("event_reminders");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EventId, x.UserId, x.Kind }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("comments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).HasMaxLength(4000);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
