PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS users (
    user_id       TEXT    NOT NULL PRIMARY KEY,
    login_id      TEXT    NOT NULL COLLATE NOCASE,
    password_hash TEXT,
    name          TEXT    NOT NULL,
    email         TEXT    NOT NULL COLLATE NOCASE,
    department    TEXT    NOT NULL,
    role          INTEGER NOT NULL CHECK (role IN (0, 1)),
    CONSTRAINT uq_users_login_id UNIQUE (login_id),
    CONSTRAINT uq_users_email UNIQUE (email)
);

CREATE TABLE IF NOT EXISTS facilities (
    facility_id   TEXT NOT NULL PRIMARY KEY,
    facility_name TEXT NOT NULL,
    address       TEXT,
    description   TEXT,
    CONSTRAINT uq_facilities_name UNIQUE (facility_name)
);

CREATE TABLE IF NOT EXISTS rooms (
    room_id      TEXT    NOT NULL PRIMARY KEY,
    facility_id  TEXT    NOT NULL,
    room_name    TEXT    NOT NULL,
    capacity     INTEGER NOT NULL CHECK (capacity > 0),
    hourly_rate  INTEGER NOT NULL CHECK (hourly_rate >= 0 AND hourly_rate % 4 = 0),
    equipment    TEXT,
    description  TEXT,
    CONSTRAINT fk_rooms_facility
        FOREIGN KEY (facility_id) REFERENCES facilities (facility_id) ON DELETE RESTRICT,
    CONSTRAINT uq_rooms_facility_name UNIQUE (facility_id, room_name)
);

CREATE TABLE IF NOT EXISTS reservations (
    reservation_id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id         TEXT    NOT NULL,
    room_id         TEXT    NOT NULL,
    start_datetime  TEXT    NOT NULL,
    end_datetime    TEXT    NOT NULL,
    title            TEXT,
    remarks          TEXT,
    status           INTEGER NOT NULL DEFAULT 0 CHECK (status IN (0, 1, 2)),
    total_price      INTEGER,
    CONSTRAINT ck_reservations_time_range
        CHECK (start_datetime < end_datetime),
    CONSTRAINT ck_reservations_confirmed_title
        CHECK (status <> 1 OR (title IS NOT NULL AND length(trim(title)) > 0)),
    CONSTRAINT ck_reservations_confirmed_price
        CHECK (status <> 1 OR total_price IS NOT NULL),
    CONSTRAINT ck_reservations_non_negative_price
        CHECK (total_price IS NULL OR total_price >= 0),
    CONSTRAINT fk_reservations_user
        FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE RESTRICT,
    CONSTRAINT fk_reservations_room
        FOREIGN KEY (room_id) REFERENCES rooms (room_id) ON DELETE RESTRICT
);

-- Reservation date-times are stored as JST local time in ISO-8601 format.
CREATE INDEX IF NOT EXISTS ix_reservations_room_time_status
    ON reservations (room_id, start_datetime, end_datetime, status);

CREATE INDEX IF NOT EXISTS ix_reservations_user_status
    ON reservations (user_id, status, start_datetime);

CREATE TABLE IF NOT EXISTS participants (
    reservation_id   INTEGER NOT NULL,
    participant_name TEXT    NOT NULL,
    company_name     TEXT    NOT NULL DEFAULT '',
    is_private       INTEGER NOT NULL DEFAULT 0 CHECK (is_private IN (0, 1)),
    CONSTRAINT pk_participants
        PRIMARY KEY (reservation_id, participant_name, company_name),
    CONSTRAINT fk_participants_reservation
        FOREIGN KEY (reservation_id) REFERENCES reservations (reservation_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_rooms_facility_capacity
    ON rooms (facility_id, capacity);

INSERT OR IGNORE INTO users
    (user_id, login_id, password_hash, name, email, department, role)
VALUES
    ('U001', 'sales01', NULL, '営業 一郎', 'sales01@example.local', '営業部', 0),
    ('U002', 'general01', NULL, '総務 花子', 'general01@example.local', '総務部', 1);

INSERT OR IGNORE INTO facilities
    (facility_id, facility_name, address, description)
VALUES
    ('F001', '東京本社', '東京都千代田区', '東京本社オフィス'),
    ('F002', '大阪支社', '大阪府大阪市', '大阪支社オフィス');

INSERT OR IGNORE INTO rooms
    (room_id, facility_id, room_name, capacity, hourly_rate, equipment, description)
VALUES
    ('R001', 'F001', '会議室A', 8, 4000, 'モニター、ホワイトボード', '標準会議室'),
    ('R002', 'F001', '会議室B', 16, 6000, 'プロジェクター、ホワイトボード', '大会議室'),
    ('R003', 'F002', '会議室C', 6, 3200, 'モニター', '小会議室');

COMMIT;
