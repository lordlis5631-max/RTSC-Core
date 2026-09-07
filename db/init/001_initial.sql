-- Initial schema bootstrap for RTSC-Core.
-- Fresh installations use this file. Upgrade scripts for previous snapshots live in db/migrations.

CREATE TABLE IF NOT EXISTS users (
    "Id" uuid PRIMARY KEY,
    "DisplayName" varchar(200) NOT NULL,
    "Email" varchar(320) NOT NULL UNIQUE,
    "Phone" varchar(50),
    "PasswordHash" text NOT NULL,
    "Role" integer NOT NULL DEFAULT 1,
    "Status" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS external_accounts (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Provider" integer NOT NULL,
    "ExternalUserId" varchar(200) NOT NULL,
    "Username" varchar(200),
    "NotificationsEnabled" boolean NOT NULL DEFAULT true,
    "LinkedAt" timestamptz NOT NULL,
    "LastSeenAt" timestamptz,
    UNIQUE ("Provider", "ExternalUserId"),
    UNIQUE ("UserId", "Provider")
);

CREATE TABLE IF NOT EXISTS external_link_tokens (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Provider" integer NOT NULL,
    "TokenHash" varchar(64) NOT NULL UNIQUE,
    "CreatedAt" timestamptz NOT NULL,
    "ExpiresAt" timestamptz NOT NULL,
    "UsedAt" timestamptz
);
CREATE INDEX IF NOT EXISTS ix_external_link_tokens_user_provider_expiry
    ON external_link_tokens("UserId", "Provider", "ExpiresAt");

CREATE TABLE IF NOT EXISTS notifications (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Type" varchar(100) NOT NULL,
    "Title" varchar(250) NOT NULL,
    "Body" varchar(4000) NOT NULL,
    "TargetUrl" varchar(2000),
    "CreatedAt" timestamptz NOT NULL,
    "ReadAt" timestamptz
);
CREATE INDEX IF NOT EXISTS ix_notifications_user_created
    ON notifications("UserId", "CreatedAt");
CREATE INDEX IF NOT EXISTS ix_notifications_user_read
    ON notifications("UserId", "ReadAt");

CREATE TABLE IF NOT EXISTS notification_deliveries (
    "Id" uuid PRIMARY KEY,
    "NotificationId" uuid NOT NULL REFERENCES notifications("Id") ON DELETE CASCADE,
    "Channel" integer NOT NULL,
    "Status" integer NOT NULL DEFAULT 1,
    "Attempts" integer NOT NULL DEFAULT 0,
    "NextAttemptAt" timestamptz NOT NULL,
    "LastAttemptAt" timestamptz,
    "SentAt" timestamptz,
    "ExternalMessageId" varchar(500),
    "Error" varchar(2000),
    UNIQUE ("NotificationId", "Channel")
);
CREATE INDEX IF NOT EXISTS ix_notification_deliveries_status_next
    ON notification_deliveries("Status", "NextAttemptAt");

CREATE TABLE IF NOT EXISTS communities (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(250) NOT NULL,
    "Description" text NOT NULL,
    "LogoUrl" varchar(1000),
    "Status" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_communities_status ON communities("Status");

CREATE TABLE IF NOT EXISTS community_members (
    "CommunityId" uuid NOT NULL REFERENCES communities("Id") ON DELETE CASCADE,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Role" integer NOT NULL DEFAULT 1,
    "JoinedAt" timestamptz NOT NULL,
    PRIMARY KEY ("CommunityId", "UserId")
);

CREATE TABLE IF NOT EXISTS events (
    "Id" uuid PRIMARY KEY,
    "CommunityId" uuid NOT NULL REFERENCES communities("Id") ON DELETE CASCADE,
    "Title" varchar(300) NOT NULL,
    "Description" text NOT NULL,
    "StartAt" timestamptz NOT NULL,
    "EndAt" timestamptz,
    "Place" varchar(500) NOT NULL,
    "Capacity" integer,
    "Status" integer NOT NULL DEFAULT 1,
    "RegistrationStartAt" timestamptz,
    "RegistrationEndAt" timestamptz,
    "ImageUrl" varchar(1000),
    "CreatedAt" timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_events_status_start ON events("Status", "StartAt");

CREATE TABLE IF NOT EXISTS event_participants (
    "EventId" uuid NOT NULL REFERENCES events("Id") ON DELETE CASCADE,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Status" integer NOT NULL DEFAULT 1,
    "RegisteredAt" timestamptz NOT NULL,
    "ConfirmedAt" timestamptz,
    "AttendedAt" timestamptz,
    PRIMARY KEY ("EventId", "UserId")
);

CREATE TABLE IF NOT EXISTS event_performers (
    "EventId" uuid NOT NULL REFERENCES events("Id") ON DELETE CASCADE,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Role" varchar(100) NOT NULL,
    PRIMARY KEY ("EventId", "UserId")
);

CREATE TABLE IF NOT EXISTS comments (
    "Id" uuid PRIMARY KEY,
    "AuthorUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "EventId" uuid REFERENCES events("Id") ON DELETE CASCADE,
    "TargetUserId" uuid REFERENCES users("Id") ON DELETE RESTRICT,
    "Text" varchar(4000) NOT NULL,
    "Status" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_comments_status_created ON comments("Status", "CreatedAt");

CREATE TABLE IF NOT EXISTS performer_ratings (
    "Id" uuid PRIMARY KEY,
    "EventId" uuid NOT NULL REFERENCES events("Id") ON DELETE CASCADE,
    "PerformerUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "AuthorUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "Score" integer NOT NULL CHECK ("Score" BETWEEN 1 AND 5),
    "CommentId" uuid REFERENCES comments("Id") ON DELETE SET NULL,
    "CreatedAt" timestamptz NOT NULL,
    UNIQUE ("EventId", "PerformerUserId", "AuthorUserId")
);

CREATE TABLE IF NOT EXISTS participant_ratings (
    "Id" uuid PRIMARY KEY,
    "EventId" uuid NOT NULL REFERENCES events("Id") ON DELETE CASCADE,
    "ParticipantUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "AuthorUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "Score" integer NOT NULL CHECK ("Score" BETWEEN 1 AND 5),
    "CommentId" uuid REFERENCES comments("Id") ON DELETE SET NULL,
    "CreatedAt" timestamptz NOT NULL,
    UNIQUE ("EventId", "ParticipantUserId", "AuthorUserId")
);

CREATE TABLE IF NOT EXISTS event_reminders (
    "Id" uuid PRIMARY KEY,
    "EventId" uuid NOT NULL REFERENCES events("Id") ON DELETE CASCADE,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Kind" integer NOT NULL,
    "NotificationId" uuid NOT NULL REFERENCES notifications("Id") ON DELETE CASCADE,
    "CreatedAt" timestamptz NOT NULL,
    UNIQUE ("EventId", "UserId", "Kind")
);
CREATE INDEX IF NOT EXISTS ix_event_reminders_created_at ON event_reminders("CreatedAt");
