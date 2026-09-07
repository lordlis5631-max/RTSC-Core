-- RTSC-Core v0.6 -> v0.7
-- Adds optional event coordinates for the public map.

ALTER TABLE events ADD COLUMN IF NOT EXISTS "Latitude" double precision;
ALTER TABLE events ADD COLUMN IF NOT EXISTS "Longitude" double precision;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_events_latitude') THEN
        ALTER TABLE events ADD CONSTRAINT ck_events_latitude
            CHECK ("Latitude" IS NULL OR "Latitude" BETWEEN -90 AND 90);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_events_longitude') THEN
        ALTER TABLE events ADD CONSTRAINT ck_events_longitude
            CHECK ("Longitude" IS NULL OR "Longitude" BETWEEN -180 AND 180);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_events_coordinate_pair') THEN
        ALTER TABLE events ADD CONSTRAINT ck_events_coordinate_pair
            CHECK (("Latitude" IS NULL) = ("Longitude" IS NULL));
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_events_coordinates ON events("Latitude", "Longitude");
