CREATE TABLE IF NOT EXISTS schema_version (
    version INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS processed_messages (
    uidl                  TEXT PRIMARY KEY,
    message_id            TEXT NULL,
    first_seen_at         TEXT NOT NULL,
    classification        TEXT NOT NULL DEFAULT 'unclassified',
    parse_attempts        INTEGER NOT NULL DEFAULT 0,
    deleted_from_mailbox  INTEGER NOT NULL DEFAULT 0,
    deleted_at            TEXT NULL
);

CREATE TABLE IF NOT EXISTS bounce_records (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    uidl                TEXT NOT NULL REFERENCES processed_messages(uidl),
    original_from       TEXT NULL,
    final_recipient     TEXT NOT NULL,
    original_recipient  TEXT NULL,
    action              TEXT NULL,
    status_code         TEXT NULL,
    diagnostic_code     TEXT NULL,
    remote_mta          TEXT NULL,
    reporting_mta       TEXT NULL,
    arrival_date        TEXT NULL,
    bounce_timestamp    TEXT NOT NULL,
    webhook_route       TEXT NOT NULL,
    status              TEXT NOT NULL DEFAULT 'pending',
    created_at          TEXT NOT NULL,
    reported_at         TEXT NULL
);
CREATE INDEX IF NOT EXISTS idx_bounce_records_route_status ON bounce_records(webhook_route, status);
CREATE INDEX IF NOT EXISTS idx_bounce_records_uidl ON bounce_records(uidl);

CREATE TABLE IF NOT EXISTS sender_backoff (
    webhook_route   TEXT PRIMARY KEY,
    failure_count   INTEGER NOT NULL DEFAULT 0,
    next_retry_at   TEXT NULL,
    last_error      TEXT NULL,
    updated_at      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS audit_log (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    uidl          TEXT NOT NULL,
    reason        TEXT NOT NULL,
    subject       TEXT NULL,
    from_header   TEXT NULL,
    message_date  TEXT NULL,
    detail        TEXT NULL,
    logged_at     TEXT NOT NULL
);
