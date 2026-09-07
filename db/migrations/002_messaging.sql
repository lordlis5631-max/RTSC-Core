-- Upgrade RTSC-Core v0.2.x -> v0.3.x.
-- Run once against an existing v0.2 database before starting the v0.3 application.

BEGIN;

ALTER TABLE external_accounts
    ADD COLUMN IF NOT EXISTS "NotificationsEnabled" boolean NOT NULL DEFAULT true;

ALTER TABLE external_accounts
    ADD COLUMN IF NOT EXISTS "LastSeenAt" timestamptz;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM external_accounts
        GROUP BY "UserId", "Provider"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'RTSC-Core v0.3 migration: duplicate external_accounts found for the same UserId + Provider. Resolve duplicates before retrying migration.';
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_external_accounts_user_provider
    ON external_accounts("UserId", "Provider");

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

COMMIT;
