"""Froglet Analytics storage: the raw event store, the aggregates rebuild, erasure, retention and backups.

Two SQLite files under the data directory:

    events.db       every accepted event, as sent (player ids included). Only this module reads it.
    aggregates.db   rebuilt from events.db every few minutes: per-play rows, daily counts, return cohorts.
                    It holds NO player ids, so the dashboard and Claude's Ask box read only this file.

Standard library only. See Tools/Analytics/server/README.md for the whole service.
"""
import hashlib
import json
import os
import sqlite3
import threading
import time
from datetime import datetime, timedelta, timezone

NEAR_DUPLICATE_MS = 50          # the facade double-send lands 1-3 ms apart (Ian, 2026-10-29); 50 ms is his query's window
REMATCH_GAP_S = 600             # a rematch: the same player starts another match within 10 min of finishing one
MAX_PROPS_BYTES = 16 * 1024
MAX_EVENT_NAME = 64
OLDEST_ACCEPTED_DAYS = 90       # an offline queue can hold old events; older than this is rejected
FUTURE_SLACK_MS = 24 * 3600 * 1000
WINDOWS = (1, 7, 14, 30, 90, 365)
BUILD_KINDS = ("release", "development", "editor", "port")
TIMESTAMP_KEYS = ("timestamp_utc_ms", "timestamp_utc_iso", "completed_utc_ms", "completed_utc_iso")

RAW_SCHEMA = """
CREATE TABLE IF NOT EXISTS events(
    id INTEGER PRIMARY KEY,
    received_ms INTEGER NOT NULL,
    ts_ms INTEGER NOT NULL,
    day TEXT NOT NULL,
    event TEXT NOT NULL,
    distinct_id TEXT NOT NULL,
    install_id TEXT,
    match_id TEXT,
    build_kind TEXT NOT NULL,
    app_version TEXT,
    platform TEXT,
    schema_version INTEGER,
    props TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    shape_hash TEXT NOT NULL,
    near_dup INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS events_content ON events(content_hash);
CREATE INDEX IF NOT EXISTS events_day ON events(day);
CREATE INDEX IF NOT EXISTS events_player ON events(distinct_id, ts_ms);
CREATE INDEX IF NOT EXISTS events_shape ON events(distinct_id, event, shape_hash, ts_ms);
CREATE TABLE IF NOT EXISTS people(
    distinct_id TEXT PRIMARY KEY,
    props TEXT NOT NULL,
    first_seen_ms INTEGER NOT NULL,
    last_seen_ms INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS rejects(
    day TEXT NOT NULL, reason TEXT NOT NULL, event TEXT NOT NULL, count INTEGER NOT NULL,
    PRIMARY KEY(day, reason, event)
);
CREATE TABLE IF NOT EXISTS resent(day TEXT PRIMARY KEY, count INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS erasures(
    id INTEGER PRIMARY KEY,
    player_hash TEXT NOT NULL,
    requested_ms INTEGER NOT NULL,
    done_ms INTEGER NOT NULL,
    rows_deleted INTEGER NOT NULL,
    source TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS digests(
    day TEXT PRIMARY KEY, written_ms INTEGER NOT NULL, author TEXT NOT NULL, notes TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS asks(
    id INTEGER PRIMARY KEY, asked_ms INTEGER NOT NULL, reader TEXT NOT NULL,
    question TEXT NOT NULL, sql TEXT, ok INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS jobs(name TEXT PRIMARY KEY, last_ms INTEGER NOT NULL, detail TEXT);
"""

# The aggregates the dashboard and the Ask box read. Kept in one string so Claude is shown exactly this schema.
AGG_SCHEMA = """
CREATE TABLE plays(              -- one row per (player, match) from game_started; NO player id
    match_id TEXT, day TEXT, start_ms INTEGER, build_kind TEXT, platform TEXT, app_version TEXT,
    game_mode TEXT, intensity INTEGER, vessel_class TEXT,
    play_type TEXT,              -- solo_alone | solo_vs_ai | coop | adversarial | mixed | multi_human_unknown
    humans INTEGER, ai INTEGER,
    completed INTEGER,           -- 1 when the same player's game_completed arrived for this match
    rematched INTEGER,           -- 1 when they started another match within 600 s of completing this one; NULL if not completed
    flight_time_seconds REAL, duration_seconds REAL
);
CREATE TABLE daily_events(       -- rows received per day, build and event
    day TEXT, build_kind TEXT, event TEXT, rows INTEGER, near_dup_rows INTEGER, players INTEGER
);
CREATE TABLE daily_plays(        -- game_started rows against distinct (player, match) pairs: 1.0 is healthy
    day TEXT, build_kind TEXT, rows INTEGER, plays INTEGER, rows_per_play REAL
);
CREATE TABLE daily_players(day TEXT, build_kind TEXT, players INTEGER, new_players INTEGER);
CREATE TABLE players_in_window(  -- distinct players seen in the last N days (N = 1, 7, 14, 30, 90, 365)
    build_kind TEXT, days INTEGER, players INTEGER
);
CREATE TABLE returns(            -- players by the play type of their first play; only cohorts 7+ days old
    first_day TEXT, build_kind TEXT, play_type TEXT, players INTEGER, returned_d1 INTEGER, returned_d7 INTEGER
);
CREATE TABLE rejects(day TEXT, reason TEXT, event TEXT, count INTEGER);
CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT);
"""


def now_ms():
    return int(time.time() * 1000)


def day_of(ms):
    return datetime.fromtimestamp(ms / 1000, tz=timezone.utc).strftime("%Y-%m-%d")


def parse_timestamp(value):
    """PostHog-style ISO-8601 ('2026-11-05T06:00:00.123Z') or epoch ms -> epoch ms, or None."""
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return int(value)
    if not isinstance(value, str) or not value:
        return None
    text = value.strip()
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        stamp = datetime.fromisoformat(text)
    except ValueError:
        return None
    if stamp.tzinfo is None:
        stamp = stamp.replace(tzinfo=timezone.utc)
    return int(stamp.timestamp() * 1000)


def player_hash(distinct_id):
    return hashlib.sha256(("froglet-erasure:" + distinct_id).encode()).hexdigest()


def build_kind_of(props):
    """Release / development / editor / port. The client's own build_kind wins; until it sends one, the build is read
    from the platform (…Editor) and the version (…-port), exactly as Ian's queries split them."""
    stated = props.get("build_kind")
    if isinstance(stated, str) and stated in BUILD_KINDS:
        return stated
    platform = str(props.get("platform") or "")
    version = str(props.get("app_version") or "")
    if platform.endswith("Editor"):
        return "editor"
    if version.endswith("-port"):
        return "port"
    return "release"


def as_int(value):
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return None


def as_float(value):
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def play_type_of(props):
    """Ian's classification (Docs/Analytics/queries/replay_retention.sql), with the team fields when they are sent."""
    humans = as_int(props.get("player_count_human")) or 0
    ai = as_int(props.get("player_count_ai")) or 0
    if humans >= 2:
        others = as_int(props.get("humans_on_other_domains"))
        mine = as_int(props.get("humans_on_my_domain"))
        if others is None:
            return "multi_human_unknown"
        if others == 0:
            return "coop"
        if mine is not None and mine >= 2:
            return "mixed"
        return "adversarial"
    return "solo_vs_ai" if ai >= 1 else "solo_alone"


class Store:
    """The raw store. One writer lock; SQLite in WAL mode so the aggregates rebuild can read while ingest writes."""

    def __init__(self, data_dir, schema_events, extra_events=()):
        self.data_dir = data_dir
        os.makedirs(data_dir, exist_ok=True)
        self.raw_path = os.path.join(data_dir, "events.db")
        self.agg_path = os.path.join(data_dir, "aggregates.db")
        self.allowed_events = set(schema_events) | {"$identify"} | set(extra_events)
        self.lock = threading.Lock()
        self.db = sqlite3.connect(self.raw_path, check_same_thread=False)
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.executescript(RAW_SCHEMA)
        self.db.commit()

    # ---------------------------------------------------------------- ingest

    def _reject(self, day, reason, event):
        self.db.execute(
            "INSERT INTO rejects(day, reason, event, count) VALUES(?,?,?,1) "
            "ON CONFLICT(day, reason, event) DO UPDATE SET count = count + 1",
            (day, reason, (event or "")[:MAX_EVENT_NAME]))

    def ingest(self, batch, received=None):
        """Store a PostHog-shaped batch ([{event, properties, timestamp}]). Returns counts per outcome.
        Nothing raises for one bad event: it is counted under rejects and the rest of the batch is stored."""
        received = received or now_ms()
        today = day_of(received)
        result = {"accepted": 0, "rejected": 0, "resent": 0, "near_duplicates": 0, "erased": 0}
        with self.lock:
            for item in batch:
                outcome = self._ingest_one(item, received, today)
                result[outcome] = result.get(outcome, 0) + 1
            self.db.commit()
        return result

    def _ingest_one(self, item, received, today):
        if not isinstance(item, dict):
            self._reject(today, "not_an_object", "")
            return "rejected"
        event = item.get("event")
        props = item.get("properties")
        if not isinstance(event, str) or not event or len(event) > MAX_EVENT_NAME:
            self._reject(today, "bad_event_name", str(event or ""))
            return "rejected"
        if event not in self.allowed_events:
            self._reject(today, "unknown_event", event)
            return "rejected"
        if not isinstance(props, dict):
            self._reject(today, "no_properties", event)
            return "rejected"
        distinct_id = props.get("distinct_id") or item.get("distinct_id")
        if not isinstance(distinct_id, str) or not distinct_id:
            self._reject(today, "no_player_id", event)
            return "rejected"
        ts = parse_timestamp(item.get("timestamp")) or parse_timestamp(props.get("timestamp_utc_ms")) or received
        if ts < received - OLDEST_ACCEPTED_DAYS * 86400 * 1000 or ts > received + FUTURE_SLACK_MS:
            self._reject(today, "bad_timestamp", event)
            return "rejected"

        if event == "$identify":
            return self._identify(distinct_id, props, received)

        props_json = json.dumps(props, sort_keys=True, separators=(",", ":"), default=str)
        if len(props_json.encode()) > MAX_PROPS_BYTES:
            self._reject(today, "too_large", event)
            return "rejected"
        content_hash = hashlib.sha256(f"{event}|{distinct_id}|{ts}|{props_json}".encode()).hexdigest()
        shape = {k: v for k, v in props.items() if k not in TIMESTAMP_KEYS}
        shape_hash = hashlib.sha256(
            f"{event}|{json.dumps(shape, sort_keys=True, separators=(',', ':'), default=str)}".encode()).hexdigest()

        near = self.db.execute(
            "SELECT 1 FROM events WHERE distinct_id=? AND event=? AND shape_hash=? AND ts_ms BETWEEN ? AND ? LIMIT 1",
            (distinct_id, event, shape_hash, ts - NEAR_DUPLICATE_MS, ts + NEAR_DUPLICATE_MS)).fetchone()
        match_id = props.get("match_id") if isinstance(props.get("match_id"), str) else None
        cur = self.db.execute(
            "INSERT OR IGNORE INTO events(received_ms, ts_ms, day, event, distinct_id, install_id, match_id, build_kind,"
            " app_version, platform, schema_version, props, content_hash, shape_hash, near_dup)"
            " VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (received, ts, day_of(ts), event, distinct_id, props.get("install_id"), match_id, build_kind_of(props),
             props.get("app_version"), props.get("platform"), as_int(props.get("schema_version")), props_json,
             content_hash, shape_hash, 1 if near else 0))
        if cur.rowcount == 0:
            # The same event sent again (the client re-sends a batch whose response it never saw): stored once.
            self.db.execute("INSERT INTO resent(day, count) VALUES(?,1) "
                            "ON CONFLICT(day) DO UPDATE SET count = count + 1", (day_of(received),))
            return "resent"
        return "near_duplicates" if near else "accepted"

    def _identify(self, distinct_id, props, received):
        person = props.get("$set") if isinstance(props.get("$set"), dict) else {}
        if person.get("gdpr_deletion_requested") is True:
            # The in-game "delete my data" button flags the person (PostHogAnalyticsSink.RequestDataDeletion).
            # PostHog could not finish that from the client; here it is finished at once.
            self._erase_locked(distinct_id, "in_game_request", received)
            return "erased"
        row = self.db.execute("SELECT props FROM people WHERE distinct_id=?", (distinct_id,)).fetchone()
        merged = json.loads(row[0]) if row else {}
        merged.update(person)
        self.db.execute(
            "INSERT INTO people(distinct_id, props, first_seen_ms, last_seen_ms) VALUES(?,?,?,?) "
            "ON CONFLICT(distinct_id) DO UPDATE SET props=excluded.props, last_seen_ms=excluded.last_seen_ms",
            (distinct_id, json.dumps(merged, sort_keys=True), received, received))
        return "accepted"

    # ---------------------------------------------------------------- erasure, retention, backup

    def erase(self, distinct_id, source):
        with self.lock:
            deleted = self._erase_locked(distinct_id, source, now_ms())
            self.db.commit()
        return deleted

    def _erase_locked(self, distinct_id, source, requested):
        deleted = self.db.execute("DELETE FROM events WHERE distinct_id=?", (distinct_id,)).rowcount
        deleted += self.db.execute("DELETE FROM people WHERE distinct_id=?", (distinct_id,)).rowcount
        # The log keeps a salted hash, never the id itself, so the record of a deletion is not a record of the player.
        self.db.execute("INSERT INTO erasures(player_hash, requested_ms, done_ms, rows_deleted, source)"
                        " VALUES(?,?,?,?,?)", (player_hash(distinct_id), requested, now_ms(), deleted, source))
        return deleted

    def apply_retention(self, retention_days):
        cutoff = day_of(now_ms() - retention_days * 86400 * 1000)
        with self.lock:
            gone = self.db.execute("DELETE FROM events WHERE day < ?", (cutoff,)).rowcount
            self.db.execute("DELETE FROM rejects WHERE day < ?", (cutoff,))
            self.db.execute("DELETE FROM resent WHERE day < ?", (cutoff,))
            self.db.commit()
        return gone

    def backup(self, keep=14):
        folder = os.path.join(self.data_dir, "backups")
        os.makedirs(folder, exist_ok=True)
        target = os.path.join(folder, "events-" + datetime.now(timezone.utc).strftime("%Y%m%d") + ".db")
        dest = sqlite3.connect(target)
        with self.lock:
            self.db.backup(dest)
        dest.close()
        os.chmod(target, 0o600)
        old = sorted(f for f in os.listdir(folder) if f.startswith("events-") and f.endswith(".db"))
        for name in old[:-keep]:
            os.remove(os.path.join(folder, name))
        return target

    def mark_job(self, name, detail=""):
        with self.lock:
            self.db.execute("INSERT INTO jobs(name, last_ms, detail) VALUES(?,?,?) ON CONFLICT(name) DO UPDATE SET "
                            "last_ms=excluded.last_ms, detail=excluded.detail", (name, now_ms(), detail))
            self.db.commit()

    def job(self, name):
        row = self.db.execute("SELECT last_ms, detail FROM jobs WHERE name=?", (name,)).fetchone()
        return {"last_ms": row[0], "detail": row[1]} if row else None

    # ---------------------------------------------------------------- digests and asks

    def save_digest(self, day, author, notes):
        with self.lock:
            self.db.execute("INSERT OR REPLACE INTO digests(day, written_ms, author, notes) VALUES(?,?,?,?)",
                            (day, now_ms(), author, json.dumps(notes)))
            self.db.commit()

    def latest_digest(self):
        row = self.db.execute("SELECT day, written_ms, author, notes FROM digests ORDER BY day DESC LIMIT 1").fetchone()
        if not row:
            return None
        return {"day": row[0], "written_ms": row[1], "author": row[2], "notes": json.loads(row[3])}

    def log_ask(self, reader, question, sql, ok):
        with self.lock:
            self.db.execute("INSERT INTO asks(asked_ms, reader, question, sql, ok) VALUES(?,?,?,?,?)",
                            (now_ms(), reader, question, sql, 1 if ok else 0))
            self.db.commit()

    def asks_today(self, reader):
        start = int(datetime.now(timezone.utc).replace(hour=0, minute=0, second=0, microsecond=0).timestamp() * 1000)
        return self.db.execute("SELECT COUNT(*) FROM asks WHERE reader=? AND asked_ms>=?", (reader, start)).fetchone()[0]

    def health_facts(self):
        last = self.db.execute("SELECT MAX(received_ms) FROM events").fetchone()[0]
        erasures = self.db.execute("SELECT COUNT(*), MAX(done_ms) FROM erasures").fetchone()
        return {"last_event_ms": last, "erasures_done": erasures[0], "last_erasure_ms": erasures[1],
                "backup": self.job("backup"), "nightly": self.job("nightly"), "rebuild": self.job("rebuild")}

    # ---------------------------------------------------------------- aggregates

    def rebuild_aggregates(self):
        """Recompute aggregates.db from events.db and swap it in atomically. Cheap at today's volume (thousands of
        rows); the day it is not, make it incremental by day."""
        reader = sqlite3.connect(f"file:{self.raw_path}?mode=ro", uri=True)
        tmp = self.agg_path + ".tmp"
        if os.path.exists(tmp):
            os.remove(tmp)
        out = sqlite3.connect(tmp)
        out.executescript(AGG_SCHEMA)

        # plays: one per (player, match) from game_started, first start wins (duplicates collapse here)
        starts = {}
        for distinct_id, match_id, ts, build, platform, version, props in reader.execute(
                "SELECT distinct_id, match_id, ts_ms, build_kind, platform, app_version, props FROM events "
                "WHERE event='game_started' AND match_id IS NOT NULL ORDER BY ts_ms"):
            key = (distinct_id, match_id)
            if key not in starts:
                starts[key] = (ts, build, platform, version, json.loads(props))
        dones = {}
        for distinct_id, match_id, ts, props in reader.execute(
                "SELECT distinct_id, match_id, ts_ms, props FROM events "
                "WHERE event='game_completed' AND match_id IS NOT NULL ORDER BY ts_ms"):
            dones.setdefault((distinct_id, match_id), (ts, json.loads(props)))

        by_player = {}
        for (distinct_id, match_id), start in starts.items():
            by_player.setdefault(distinct_id, []).append((start[0], match_id))
        rows = []
        for distinct_id, plays in by_player.items():
            plays.sort()
            for index, (start_ts, match_id) in enumerate(plays):
                ts, build, platform, version, props = starts[(distinct_id, match_id)]
                done = dones.get((distinct_id, match_id))
                rematched = None
                if done:
                    following = plays[index + 1][0] if index + 1 < len(plays) else None
                    rematched = 1 if following is not None and 0 <= (following - done[0]) / 1000 <= REMATCH_GAP_S else 0
                done_props = done[1] if done else {}
                rows.append((match_id, day_of(ts), ts, build, platform, version, props.get("game_mode"),
                             as_int(props.get("intensity")), props.get("vessel_class"), play_type_of(props),
                             as_int(props.get("player_count_human")), as_int(props.get("player_count_ai")),
                             1 if done else 0, rematched, as_float(done_props.get("flight_time_seconds")),
                             as_float(done_props.get("duration_seconds"))))
        out.executemany("INSERT INTO plays VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", rows)

        out.executemany("INSERT INTO daily_events VALUES(?,?,?,?,?,?)", reader.execute(
            "SELECT day, build_kind, event, COUNT(*), SUM(near_dup), COUNT(DISTINCT distinct_id) FROM events "
            "GROUP BY day, build_kind, event").fetchall())
        out.executemany("INSERT INTO daily_plays VALUES(?,?,?,?,?)", reader.execute(
            "SELECT day, build_kind, COUNT(*), COUNT(DISTINCT distinct_id || '|' || match_id), "
            "ROUND(1.0 * COUNT(*) / COUNT(DISTINCT distinct_id || '|' || match_id), 2) FROM events "
            "WHERE event='game_started' AND match_id IS NOT NULL GROUP BY day, build_kind").fetchall())

        first_day = {}
        days_seen = {}
        for distinct_id, build, day in reader.execute(
                "SELECT distinct_id, build_kind, day FROM events GROUP BY distinct_id, build_kind, day ORDER BY day"):
            first_day.setdefault((distinct_id, build), day)
            days_seen.setdefault((distinct_id, build), set()).add(day)
        players = {}
        for (distinct_id, build), seen in days_seen.items():
            for day in seen:
                entry = players.setdefault((day, build), [0, 0])
                entry[0] += 1
                if first_day[(distinct_id, build)] == day:
                    entry[1] += 1
        out.executemany("INSERT INTO daily_players VALUES(?,?,?,?)",
                        [(d, b, v[0], v[1]) for (d, b), v in sorted(players.items())])

        windows = []
        for n in WINDOWS:
            since = day_of(now_ms() - (n - 1) * 86400 * 1000)
            counts = {}
            for (distinct_id, build), seen in days_seen.items():
                if max(seen) >= since:
                    counts[build] = counts.get(build, 0) + 1
            windows += [(b, n, c) for b, c in counts.items()]
        out.executemany("INSERT INTO players_in_window VALUES(?,?,?)", windows)

        first_play = {}
        for (distinct_id, match_id), start in sorted(starts.items(), key=lambda kv: kv[1][0]):
            first_play.setdefault((distinct_id, start[1]), (day_of(start[0]), play_type_of(start[4])))
        mature = day_of(now_ms() - 7 * 86400 * 1000)
        cohorts = {}
        for (distinct_id, build), (day, play_type) in first_play.items():
            if day > mature:
                continue
            seen = days_seen.get((distinct_id, build), set())
            start = datetime.strptime(day, "%Y-%m-%d")
            next_day = (start + timedelta(days=1)).strftime("%Y-%m-%d")
            week = {(start + timedelta(days=n)).strftime("%Y-%m-%d") for n in range(1, 8)}
            entry = cohorts.setdefault((day, build, play_type), [0, 0, 0])
            entry[0] += 1
            entry[1] += 1 if next_day in seen else 0
            entry[2] += 1 if seen & week else 0
        out.executemany("INSERT INTO returns VALUES(?,?,?,?,?,?)",
                        [(d, b, p, v[0], v[1], v[2]) for (d, b, p), v in sorted(cohorts.items())])
        out.executemany("INSERT INTO rejects VALUES(?,?,?,?)",
                        reader.execute("SELECT day, reason, event, count FROM rejects").fetchall())
        out.executemany("INSERT INTO meta VALUES(?,?)", [("built_ms", str(now_ms()))])
        out.commit()
        out.close()
        reader.close()
        os.replace(tmp, self.agg_path)
        return len(rows)
