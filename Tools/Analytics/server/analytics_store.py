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
    country TEXT,                -- two-letter code from the sender's IP at ingest; the IP is never stored
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
    country TEXT,
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
    flight_time_seconds REAL, duration_seconds REAL,
    won INTEGER,                 -- player_won from game_completed; NULL if not completed
    pause_seconds REAL           -- duration_seconds - flight_time_seconds: paused or away from the controls
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
CREATE TABLE players_by_platform(  -- distinct players per platform seen in the last N days
    build_kind TEXT, platform TEXT, days INTEGER, players INTEGER
);
CREATE TABLE players_by_country(   -- distinct players per country (from IP at ingest) seen in the last N days
    build_kind TEXT, country TEXT, days INTEGER, players INTEGER
);
CREATE TABLE funnel(             -- players by the day of their first event, and how far they got (ever)
    first_day TEXT, build_kind TEXT, players INTEGER, reached_menu INTEGER, started_match INTEGER,
    finished_match INTEGER, finished_ftue INTEGER, came_back INTEGER
);
CREATE TABLE ftue_steps(         -- players (by first day) who completed each FTUE quest-graph node
    first_day TEXT, build_kind TEXT, quest_id TEXT, phase_index INTEGER, step_id TEXT, step_name TEXT,
    players INTEGER, seconds_sum REAL, seconds_n INTEGER   -- seconds since launch when it was completed
);
CREATE TABLE launches(           -- time from app launch to the menu (menu_ready), per platform, last N days
    build_kind TEXT, platform TEXT, days INTEGER, launches INTEGER, p50_seconds REAL, p90_seconds REAL
);
CREATE TABLE session_ends(day TEXT, build_kind TEXT, reason TEXT, count INTEGER);
CREATE TABLE economy(            -- crystals by day: kind is earned | spent | blocked | unlock
    day TEXT, build_kind TEXT, kind TEXT, source TEXT, events INTEGER, amount INTEGER
);
CREATE TABLE double_payouts(     -- crystals_earned paid twice: same player, source and amount within 1 s
    day TEXT, build_kind TEXT, source TEXT, count INTEGER, amount INTEGER
);
CREATE TABLE balances(           -- players active in the last 30 days by their latest crystal balance
    build_kind TEXT, bucket TEXT, bucket_order INTEGER, players INTEGER
);
CREATE TABLE unlocks(day TEXT, build_kind TEXT, kind TEXT, name TEXT, count INTEGER);  -- vessel | mode | intensity
CREATE TABLE mode_signals(       -- per mode per day: favourites added/removed, repeated-fail events, shares
    day TEXT, build_kind TEXT, game_mode TEXT, favorited INTEGER, unfavorited INTEGER, fails INTEGER, shares INTEGER
);
CREATE TABLE settings(           -- each player's latest value of each setting (last 90 days), counted
    build_kind TEXT, setting TEXT, value TEXT, players INTEGER
);
CREATE TABLE cloud_save_failures(day TEXT, build_kind TEXT, key TEXT, count INTEGER);
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
        for table in ("events", "people"):        # databases made before the country column
            columns = {row[1] for row in self.db.execute(f"PRAGMA table_info({table})")}
            if "country" not in columns:
                self.db.execute(f"ALTER TABLE {table} ADD COLUMN country TEXT")
        self.db.commit()

    # ---------------------------------------------------------------- ingest

    def _reject(self, day, reason, event):
        self.db.execute(
            "INSERT INTO rejects(day, reason, event, count) VALUES(?,?,?,1) "
            "ON CONFLICT(day, reason, event) DO UPDATE SET count = count + 1",
            (day, reason, (event or "")[:MAX_EVENT_NAME]))

    def ingest(self, batch, received=None, country=None):
        """Store a PostHog-shaped batch ([{event, properties, timestamp}]). Returns counts per outcome.
        Nothing raises for one bad event: it is counted under rejects and the rest of the batch is stored."""
        received = received or now_ms()
        today = day_of(received)
        result = {"accepted": 0, "rejected": 0, "resent": 0, "near_duplicates": 0, "erased": 0}
        with self.lock:
            for item in batch:
                outcome = self._ingest_one(item, received, today, country)
                result[outcome] = result.get(outcome, 0) + 1
            self.db.commit()
        return result

    def _ingest_one(self, item, received, today, country=None):
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
            return self._identify(distinct_id, props, received, country)

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
            " app_version, platform, country, schema_version, props, content_hash, shape_hash, near_dup)"
            " VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (received, ts, day_of(ts), event, distinct_id, props.get("install_id"), match_id, build_kind_of(props),
             props.get("app_version"), props.get("platform"), country, as_int(props.get("schema_version")), props_json,
             content_hash, shape_hash, 1 if near else 0))
        if cur.rowcount == 0:
            # The same event sent again (the client re-sends a batch whose response it never saw): stored once.
            self.db.execute("INSERT INTO resent(day, count) VALUES(?,1) "
                            "ON CONFLICT(day) DO UPDATE SET count = count + 1", (day_of(received),))
            return "resent"
        return "near_duplicates" if near else "accepted"

    def _identify(self, distinct_id, props, received, country=None):
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
            "INSERT INTO people(distinct_id, props, country, first_seen_ms, last_seen_ms) VALUES(?,?,?,?,?) "
            "ON CONFLICT(distinct_id) DO UPDATE SET props=excluded.props, last_seen_ms=excluded.last_seen_ms, "
            "country=COALESCE(excluded.country, people.country)",
            (distinct_id, json.dumps(merged, sort_keys=True), country, received, received))
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

    # ---------------------------------------------------------------- players (raw data; profile readers only)

    def _read(self):
        db = sqlite3.connect(f"file:{self.raw_path}?mode=ro", uri=True)
        db.row_factory = sqlite3.Row
        return db

    def people_list(self, build=None, days=90, search="", limit=500):
        """One row per player seen in the last `days`: who, where, on what, how much. Newest first."""
        since = day_of(now_ms() - (days - 1) * 86400 * 1000)
        db = self._read()
        where, args = "day >= ?", [since]
        if build:
            where += " AND build_kind = ?"
            args.append(build)
        rows = db.execute(
            "SELECT distinct_id, MIN(ts_ms) AS first_ms, MAX(ts_ms) AS last_ms, COUNT(*) AS events, "
            "COUNT(DISTINCT day) AS days_active, "
            "COUNT(DISTINCT CASE WHEN event='game_started' THEN match_id END) AS matches, "
            "COUNT(DISTINCT CASE WHEN event='game_completed' THEN match_id END) AS finished, "
            "GROUP_CONCAT(DISTINCT platform) AS platforms, GROUP_CONCAT(DISTINCT country) AS countries, "
            "GROUP_CONCAT(DISTINCT build_kind) AS builds, MAX(app_version) AS app_version "
            f"FROM events WHERE {where} GROUP BY distinct_id", args).fetchall()
        people = {r["distinct_id"]: (json.loads(r["props"]), r["country"]) for r in db.execute(
            "SELECT distinct_id, props, country FROM people")}
        db.close()
        needle = (search or "").strip().lower()
        out = []
        for r in rows:
            props, _ = people.get(r["distinct_id"], ({}, None))
            name = str(props.get("display_name") or "")
            if needle and needle not in r["distinct_id"].lower() and needle not in name.lower():
                continue
            out.append({**dict(r), "display_name": name, "crystal_balance": props.get("crystal_balance"),
                        "platforms": sorted(filter(None, (r["platforms"] or "").split(","))),
                        "countries": sorted(filter(None, (r["countries"] or "").split(",")))})
        out.sort(key=lambda p: p["last_ms"], reverse=True)
        return {"days": days, "build": build, "total": len(out), "people": out[:limit]}

    def person(self, distinct_id):
        """Everything stored about one player, arranged for their profile page."""
        db = self._read()
        events = db.execute("SELECT ts_ms, day, event, match_id, build_kind, app_version, platform, country, "
                            "install_id, near_dup, props FROM events WHERE distinct_id=? ORDER BY ts_ms",
                            (distinct_id,)).fetchall()
        row = db.execute("SELECT props, country, first_seen_ms, last_seen_ms FROM people WHERE distinct_id=?",
                         (distinct_id,)).fetchone()
        db.close()
        if not events and not row:
            return None
        props_of = [json.loads(e["props"]) for e in events]
        count = lambda key: _counts(e[key] for e in events if e[key])
        starts, dones = {}, {}
        for e, p in zip(events, props_of):
            if e["event"] == "game_started" and e["match_id"] and e["match_id"] not in starts:
                starts[e["match_id"]] = (e, p)
            elif e["event"] == "game_completed" and e["match_id"] and e["match_id"] not in dones:
                dones[e["match_id"]] = (e, p)
        ordered = sorted(starts.items(), key=lambda kv: kv[1][0]["ts_ms"])
        matches = []
        for i, (match_id, (e, p)) in enumerate(ordered):
            done = dones.get(match_id)
            dp = done[1] if done else {}
            following = ordered[i + 1][1][0]["ts_ms"] if i + 1 < len(ordered) else None
            matches.append({
                "ts_ms": e["ts_ms"], "build": e["build_kind"], "game_mode": p.get("game_mode"),
                "intensity": as_int(p.get("intensity")), "vessel_class": p.get("vessel_class"),
                "play_type": play_type_of(p), "humans": as_int(p.get("player_count_human")),
                "ai": as_int(p.get("player_count_ai")), "finished": bool(done),
                "won": dp.get("player_won") if done else None,
                "flight_time_seconds": as_float(dp.get("flight_time_seconds")),
                "rematched": bool(done and following is not None
                                  and 0 <= (following - done[0]["ts_ms"]) / 1000 <= REMATCH_GAP_S),
            })
        economy, unlocks, ftue, settings, social, ends, launches = [], [], [], {}, {}, {}, []
        for e, p in zip(events, props_of):
            name = e["event"]
            if name in ("crystals_earned", "crystals_spent", "crystal_spend_blocked", "vessel_unlocked"):
                economy.append({"ts_ms": e["ts_ms"], "event": name,
                                "amount": as_int(p.get("amount") if name != "vessel_unlocked" else p.get("cost")),
                                "source": p.get("source") or p.get("item") or p.get("vessel"),
                                "balance": as_int(p.get("balance"))})
            if name in ("vessel_unlocked", "mode_unlocked", "intensity_unlocked"):
                unlocks.append({"ts_ms": e["ts_ms"], "kind": name.split("_")[0],
                                "name": p.get("vessel") or " ".join(str(x) for x in (p.get("game_mode"), p.get("intensity")) if x)})
            elif name == "ftue_step_completed":
                ftue.append({"ts_ms": e["ts_ms"], "phase_index": as_int(p.get("phase_index")),
                             "step": p.get("step_name") or p.get("step_id"),
                             "seconds_since_launch": as_float(p.get("seconds_since_launch"))})
            elif name == "ftue_completed":
                ftue.append({"ts_ms": e["ts_ms"], "phase_index": None, "step": "FTUE completed",
                             "seconds_since_launch": as_float(p.get("seconds_since_launch"))})
            elif name == "setting_changed":
                settings[str(p.get("setting"))] = {"value": p.get("value"), "ts_ms": e["ts_ms"]}
            elif name in ("friend_request_sent", "friend_request_received", "friend_added", "party_invite_sent",
                          "party_invite_received", "party_joined", "share_triggered", "minigame_favorited"):
                entry = social.setdefault(name, {"count": 0, "with": []})
                entry["count"] += 1
                other = p.get("friend_id") or p.get("target_id") or p.get("from_id") or p.get("host_id") \
                    or p.get("game_mode")
                if other and other not in entry["with"]:
                    entry["with"].append(other)
            elif name == "session_ended":
                key = str(p.get("reason") or "unknown")
                ends[key] = ends.get(key, 0) + 1
            elif name == "menu_ready" and as_float(p.get("seconds_since_launch")) is not None:
                launches.append(as_float(p.get("seconds_since_launch")))
        per_day = {}
        for e in events:
            d = per_day.setdefault(e["day"], {"day": e["day"], "events": 0, "matches": 0})
            d["events"] += 1
            d["matches"] += 1 if e["event"] == "game_started" and e["match_id"] in starts \
                and starts[e["match_id"]][0]["ts_ms"] == e["ts_ms"] else 0
        person_props = json.loads(row["props"]) if row else {}
        return {
            "player_id": distinct_id,
            "display_name": person_props.get("display_name") or "",
            "person": person_props,
            "first_ms": events[0]["ts_ms"] if events else (row["first_seen_ms"] if row else None),
            "last_ms": events[-1]["ts_ms"] if events else (row["last_seen_ms"] if row else None),
            "where": {"countries": count("country"), "platforms": count("platform"),
                      "app_versions": count("app_version"), "builds": count("build_kind"),
                      "installs": count("install_id"), "latest_country": (row["country"] if row else None)
                      or next((e["country"] for e in reversed(events) if e["country"]), None)},
            "activity": {"events": len(events), "days_active": len(per_day), "per_day": list(per_day.values())[-90:],
                         "events_by_name": _counts(e["event"] for e in events),
                         "flagged_duplicates": sum(e["near_dup"] for e in events),
                         "session_ends": ends,
                         "launch_to_menu_p50": _percentile(launches, 0.5)},
            "matches": {"total": len(matches), "finished": sum(m["finished"] for m in matches),
                        "won": sum(1 for m in matches if m["won"] in (True, "true")),
                        "flight_seconds": round(sum(m["flight_time_seconds"] or 0 for m in matches), 1),
                        "by_mode": _counts(m["game_mode"] for m in matches if m["game_mode"]),
                        "by_vessel": _counts(m["vessel_class"] for m in matches if m["vessel_class"]),
                        "by_play_type": _counts(m["play_type"] for m in matches),
                        "recent": list(reversed(matches))[:100]},
            "economy": {"earned": sum(x["amount"] or 0 for x in economy if x["event"] == "crystals_earned"),
                        "spent": sum(x["amount"] or 0 for x in economy if x["event"] in ("crystals_spent", "vessel_unlocked")),
                        "balance": next((x["balance"] for x in reversed(economy) if x["balance"] is not None),
                                        person_props.get("crystal_balance")),
                        "recent": list(reversed(economy))[:60], "unlocks": unlocks},
            "ftue": ftue, "settings": settings, "social": social,
            "recent_events": [{"ts_ms": e["ts_ms"], "event": e["event"], "build": e["build_kind"],
                               "platform": e["platform"], "country": e["country"],
                               "detail": _summary(e["event"], p)} for e, p in
                              list(zip(events, props_of))[::-1][:80]],
        }

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
                flight = as_float(done_props.get("flight_time_seconds"))
                duration = as_float(done_props.get("duration_seconds"))
                won = done_props.get("player_won")
                rows.append((match_id, day_of(ts), ts, build, platform, version, props.get("game_mode"),
                             as_int(props.get("intensity")), props.get("vessel_class"), play_type_of(props),
                             as_int(props.get("player_count_human")), as_int(props.get("player_count_ai")),
                             1 if done else 0, rematched, flight, duration,
                             (1 if won is True or won == "true" else 0) if done and won is not None else None,
                             max(0.0, duration - flight) if flight is not None and duration is not None else None))
        out.executemany("INSERT INTO plays VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", rows)

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
        _rebuild_details(reader, out, first_day, days_seen)
        out.executemany("INSERT INTO rejects VALUES(?,?,?,?)",
                        reader.execute("SELECT day, reason, event, count FROM rejects").fetchall())
        out.executemany("INSERT INTO meta VALUES(?,?)", [("built_ms", str(now_ms()))])
        out.commit()
        out.close()
        reader.close()
        os.replace(tmp, self.agg_path)
        return len(rows)


DETAIL_EVENTS = ("game_first_launched", "menu_ready", "game_started", "game_completed", "ftue_step_completed",
                 "ftue_completed", "session_ended", "crystals_earned", "crystals_spent", "crystal_spend_blocked",
                 "vessel_unlocked", "mode_unlocked", "intensity_unlocked", "crystal_balance_snapshot",
                 "minigame_favorited", "repeated_game_fail", "share_triggered", "setting_changed", "cloud_save_failed")
BALANCE_BUCKETS = ((0, 0, "0"), (1, 99, "1-99"), (100, 499, "100-499"), (500, 1999, "500-1,999"),
                   (2000, 10 ** 12, "2,000+"))
DOUBLE_PAYOUT_MS = 1000


def _counts(values):
    out = {}
    for v in values:
        out[v] = out.get(v, 0) + 1
    return dict(sorted(out.items(), key=lambda kv: -kv[1]))


SUMMARY_KEYS = ("game_mode", "intensity", "vessel_class", "amount", "source", "balance", "setting", "value", "reason",
                "step_name", "action_type", "action_label", "vessel", "quest", "key", "display_name")


def _summary(event, props):
    """A short readable line for the event timeline on a profile."""
    bits = [f"{k}={props[k]}" for k in SUMMARY_KEYS if k in props and props[k] not in (None, "")]
    if event == "$identify" and isinstance(props.get("$set"), dict):
        bits = [f"{k}={v}" for k, v in list(props["$set"].items())[:6]]
    return ", ".join(bits)[:160]


def _percentile(values, q):
    values = sorted(values)
    if not values:
        return None
    k = (len(values) - 1) * q
    lo, hi = int(k), min(int(k) + 1, len(values) - 1)
    return round(values[lo] + (values[hi] - values[lo]) * (k - lo), 1)


def _add(table, key, *values):
    row = table.setdefault(key, [0] * len(values))
    for i, v in enumerate(values):
        row[i] += v


def _rebuild_details(reader, out, first_day, days_seen):
    """Everything past plays and retention, from one ordered pass over the events these pages read. Player ids are
    used here only to count each player once; nothing written to `out` carries one."""
    now = now_ms()
    since = {n: day_of(now - (n - 1) * 86400 * 1000) for n in WINDOWS}
    recent30, recent90 = since[30], since[90]
    platform_seen, reached, ftue, launch_times = {}, {}, {}, {}
    session_ends, economy, doubles, last_balance, unlock_rows = {}, {}, {}, {}, {}
    signals, setting_last, save_failures, last_earn = {}, {}, {}, {}
    marks = ("menu_ready", "game_started", "game_completed", "ftue_completed")
    placeholders = ",".join("?" * len(DETAIL_EVENTS))
    country_seen = {}
    for player, build, day, country in reader.execute(
            "SELECT distinct_id, build_kind, MAX(day), country FROM events GROUP BY distinct_id, build_kind, country"):
        country_seen.setdefault((build, country or "unknown"), {})[player] = day
    for player, build, day, ts, event, platform, raw in reader.execute(
            f"SELECT distinct_id, build_kind, day, ts_ms, event, platform, props FROM events "
            f"WHERE event IN ({placeholders}) ORDER BY ts_ms", DETAIL_EVENTS):
        props = json.loads(raw)
        who = (player, build)
        if platform:
            platform_seen.setdefault((build, platform), {})[player] = max(
                day, platform_seen.get((build, platform), {}).get(player, day))
        if event in marks:
            reached.setdefault(who, set()).add(event)
        if event == "menu_ready" and as_float(props.get("seconds_since_launch")) is not None:
            launch_times.setdefault((build, platform or "unknown"), []).append(
                (day, as_float(props.get("seconds_since_launch"))))
        elif event == "ftue_step_completed":
            key = (who, props.get("quest_id") or "unknown", as_int(props.get("phase_index")),
                   props.get("step_id") or "unknown")
            if key not in ftue:
                ftue[key] = (props.get("step_name") or props.get("step_id") or "unknown",
                             as_float(props.get("seconds_since_launch")))
        elif event == "session_ended":
            _add(session_ends, (day, build, str(props.get("reason") or "unknown")), 1)
        elif event in ("crystals_earned", "crystals_spent", "crystal_spend_blocked", "vessel_unlocked"):
            kind = {"crystals_earned": "earned", "crystals_spent": "spent", "crystal_spend_blocked": "blocked",
                    "vessel_unlocked": "unlock"}[event]
            amount = as_int(props.get("amount") if event != "vessel_unlocked" else props.get("cost")) or 0
            source = str(props.get("source") or props.get("item") or props.get("vessel") or "unknown")
            _add(economy, (day, build, kind, source), 1, amount)
            if event == "crystals_earned":
                previous = last_earn.get(who)
                if previous and previous[1] == source and previous[2] == amount and ts - previous[0] <= DOUBLE_PAYOUT_MS:
                    _add(doubles, (day, build, source), 1, amount)
                last_earn[who] = (ts, source, amount)
        if event in ("crystals_earned", "crystals_spent", "crystal_spend_blocked", "vessel_unlocked",
                     "crystal_balance_snapshot") and as_int(props.get("balance")) is not None:
            last_balance[who] = (day, as_int(props.get("balance")))
        if event == "vessel_unlocked":
            _add(unlock_rows, (day, build, "vessel", str(props.get("vessel") or "unknown")), 1)
        elif event == "mode_unlocked":
            _add(unlock_rows, (day, build, "mode", str(props.get("game_mode") or "unknown")), 1)
        elif event == "intensity_unlocked":
            _add(unlock_rows, (day, build, "intensity",
                               f"{props.get('game_mode') or 'unknown'} {as_int(props.get('intensity')) or '?'}"), 1)
        elif event == "minigame_favorited":
            fav = props.get("favorited") in (True, "true", "True", 1)
            _add(signals, (day, build, str(props.get("game_mode") or "unknown")), 1 if fav else 0, 0 if fav else 1, 0, 0)
        elif event == "repeated_game_fail":
            _add(signals, (day, build, str(props.get("game_mode") or "unknown")), 0, 0, 1, 0)
        elif event == "share_triggered":
            _add(signals, (day, build, str(props.get("game_mode") or "unknown")), 0, 0, 0, 1)
        elif event == "setting_changed" and day >= recent90:
            setting_last[(who, str(props.get("setting") or "unknown"))] = str(props.get("value"))
        elif event == "cloud_save_failed":
            _add(save_failures, (day, build, str(props.get("key") or "unknown")), 1)

    rows = []
    for (build, platform), players in platform_seen.items():
        for n in WINDOWS:
            count = sum(1 for last in players.values() if last >= since[n])
            if count:
                rows.append((build, platform, n, count))
    out.executemany("INSERT INTO players_by_platform VALUES(?,?,?,?)", rows)
    rows = []
    for (build, country), players in country_seen.items():
        for n in WINDOWS:
            count = sum(1 for last in players.values() if last >= since[n])
            if count:
                rows.append((build, country, n, count))
    out.executemany("INSERT INTO players_by_country VALUES(?,?,?,?)", rows)

    cohorts = {}
    for who, day in first_day.items():
        got = reached.get(who, set())
        _add(cohorts, (day, who[1]), 1, int("menu_ready" in got), int("game_started" in got),
             int("game_completed" in got), int("ftue_completed" in got), int(len(days_seen.get(who, ())) > 1))
    out.executemany("INSERT INTO funnel VALUES(?,?,?,?,?,?,?,?)", [k + tuple(v) for k, v in sorted(cohorts.items())])

    steps = {}
    for (who, quest, phase, step), (name, seconds) in ftue.items():
        key = (first_day.get(who, "unknown"), who[1], quest, phase, step)
        entry = steps.setdefault(key, [name, 0, 0.0, 0])
        entry[1] += 1
        if seconds is not None:
            entry[2] += seconds
            entry[3] += 1
    out.executemany("INSERT INTO ftue_steps VALUES(?,?,?,?,?,?,?,?,?)",
                    [k[:4] + (k[4], v[0], v[1], v[2], v[3]) for k, v in sorted(steps.items(), key=lambda kv: str(kv[0]))])

    rows = []
    for (build, platform), samples in launch_times.items():
        for n in WINDOWS:
            values = [sec for day, sec in samples if day >= since[n]]
            if values:
                rows.append((build, platform, n, len(values), _percentile(values, 0.5), _percentile(values, 0.9)))
    out.executemany("INSERT INTO launches VALUES(?,?,?,?,?,?)", rows)

    out.executemany("INSERT INTO session_ends VALUES(?,?,?,?)", [k + tuple(v) for k, v in session_ends.items()])
    out.executemany("INSERT INTO economy VALUES(?,?,?,?,?,?)", [k + tuple(v) for k, v in economy.items()])
    out.executemany("INSERT INTO double_payouts VALUES(?,?,?,?,?)", [k + tuple(v) for k, v in doubles.items()])
    buckets = {}
    for (player, build), (day, balance) in last_balance.items():
        if day < recent30:
            continue
        for order, (lo, hi, label) in enumerate(BALANCE_BUCKETS):
            if lo <= balance <= hi:
                _add(buckets, (build, label, order), 1)
    out.executemany("INSERT INTO balances VALUES(?,?,?,?)", [k + tuple(v) for k, v in buckets.items()])
    out.executemany("INSERT INTO unlocks VALUES(?,?,?,?,?)", [k + tuple(v) for k, v in unlock_rows.items()])
    out.executemany("INSERT INTO mode_signals VALUES(?,?,?,?,?,?,?)", [k + tuple(v) for k, v in signals.items()])
    counted = {}
    for ((player, build), setting), value in setting_last.items():
        _add(counted, (build, setting, value), 1)
    out.executemany("INSERT INTO settings VALUES(?,?,?,?)", [k + tuple(v) for k, v in counted.items()])
    out.executemany("INSERT INTO cloud_save_failures VALUES(?,?,?,?)", [k + tuple(v) for k, v in save_failures.items()])
