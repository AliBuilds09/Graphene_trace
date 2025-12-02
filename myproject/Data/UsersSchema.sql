-- Users table schema for local SQLite database
-- Ensures Admin-only registration and strict role values

CREATE TABLE IF NOT EXISTS Users (
    user_id              TEXT PRIMARY KEY,                 -- Guid stored as text
    username             TEXT NOT NULL UNIQUE,             -- unique username
    password_hash        TEXT NOT NULL,                    -- hex-encoded SHA256 hash
    role                 TEXT NOT NULL CHECK (role IN ('Admin','Clinician','Patient')),
    created_at           TEXT NOT NULL,                    -- ISO 8601 UTC timestamp
    is_active            INTEGER NOT NULL DEFAULT 1,       -- 1=true, 0=false
    created_by_admin_id  TEXT NULL,                        -- Guid of admin who created
    last_password_reset  TEXT NULL                         -- ISO 8601 UTC timestamp
);

-- Index to accelerate lookups by username
CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Username ON Users(username);

-- Example insert (will fail if role not one of allowed values)
-- INSERT INTO Users (user_id, username, password_hash, role, created_at, is_active)
-- VALUES ('00000000-0000-0000-0000-000000000001', 'admin1', '...SHA256HEX...', 'Admin', '2025-11-21T12:00:00Z', 1);