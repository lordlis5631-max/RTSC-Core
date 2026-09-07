-- RTSC-Core v0.8 -> v0.9
-- Categories, tags and explicit user interests.

ALTER TABLE events
    ADD COLUMN IF NOT EXISTS "Category" integer NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS ix_events_status_category_start
    ON events("Status", "Category", "StartAt");

CREATE TABLE IF NOT EXISTS tags (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(80) NOT NULL,
    "Slug" varchar(80) NOT NULL UNIQUE,
    "IsActive" boolean NOT NULL DEFAULT true
);
CREATE INDEX IF NOT EXISTS ix_tags_active ON tags("IsActive");

CREATE TABLE IF NOT EXISTS event_tags (
    "EventId" uuid NOT NULL REFERENCES events("Id") ON DELETE CASCADE,
    "TagId" uuid NOT NULL REFERENCES tags("Id") ON DELETE CASCADE,
    PRIMARY KEY ("EventId", "TagId")
);
CREATE INDEX IF NOT EXISTS ix_event_tags_tag ON event_tags("TagId");

CREATE TABLE IF NOT EXISTS user_tag_interests (
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "TagId" uuid NOT NULL REFERENCES tags("Id") ON DELETE CASCADE,
    PRIMARY KEY ("UserId", "TagId")
);
CREATE INDEX IF NOT EXISTS ix_user_tag_interests_tag ON user_tag_interests("TagId");

CREATE TABLE IF NOT EXISTS user_category_interests (
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "Category" integer NOT NULL,
    PRIMARY KEY ("UserId", "Category")
);

INSERT INTO tags ("Id", "Name", "Slug", "IsActive") VALUES
('00000000-0000-0000-0000-000000000101', 'Студенчество', 'students', true),
('00000000-0000-0000-0000-000000000102', 'Карьера', 'career', true),
('00000000-0000-0000-0000-000000000103', 'IT', 'it', true),
('00000000-0000-0000-0000-000000000104', 'Наука', 'science', true),
('00000000-0000-0000-0000-000000000105', 'Технологии', 'technology', true),
('00000000-0000-0000-0000-000000000106', 'Инженерия', 'engineering', true),
('00000000-0000-0000-0000-000000000107', 'Культура', 'culture', true),
('00000000-0000-0000-0000-000000000108', 'История', 'history', true),
('00000000-0000-0000-0000-000000000109', 'Башкортостан', 'bashkortostan', true),
('00000000-0000-0000-0000-000000000110', 'Волонтёрство', 'volunteering', true),
('00000000-0000-0000-0000-000000000111', 'Спорт', 'sport', true),
('00000000-0000-0000-0000-000000000112', 'Настольные игры', 'board-games', true),
('00000000-0000-0000-0000-000000000113', 'Разработка игр', 'game-dev', true),
('00000000-0000-0000-0000-000000000114', 'Предпринимательство', 'entrepreneurship', true),
('00000000-0000-0000-0000-000000000115', 'Творчество', 'creativity', true),
('00000000-0000-0000-0000-000000000116', 'Образование', 'education', true),
('00000000-0000-0000-0000-000000000117', 'Экология', 'ecology', true),
('00000000-0000-0000-0000-000000000118', 'Медиа', 'media', true),
('00000000-0000-0000-0000-000000000119', 'Психология', 'psychology', true),
('00000000-0000-0000-0000-000000000120', 'Здоровье', 'health', true)
ON CONFLICT ("Slug") DO UPDATE
SET "Name" = EXCLUDED."Name", "IsActive" = EXCLUDED."IsActive";
