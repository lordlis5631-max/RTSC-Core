-- RTSC-Core v0.3 -> v0.4
-- Telegram/VK reuse existing external_accounts and notification_deliveries tables.
-- Only scheduled event reminders need a new idempotency table.

BEGIN;

CREATE TABLE IF NOT EXISTS event_reminders (
    "Id" uuid PRIMARY KEY,
    "EventId" uuid NOT NULL REFERENCES events("Id") ON DELETE CASCADE,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Kind" integer NOT NULL,
    "NotificationId" uuid NOT NULL REFERENCES notifications("Id") ON DELETE CASCADE,
    "CreatedAt" timestamptz NOT NULL,
    UNIQUE ("EventId", "UserId", "Kind")
);

CREATE INDEX IF NOT EXISTS ix_event_reminders_created_at
    ON event_reminders("CreatedAt");

COMMIT;
