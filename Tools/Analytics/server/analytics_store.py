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
import re
import secrets
import sqlite3
import threading
import time
from datetime import date, datetime, timedelta, timezone

NEAR_DUPLICATE_MS = 50          # the facade double-send lands 1-3 ms apart (Ian, 2026-10-29); 50 ms is his query's window
REMATCH_GAP_S = 600             # a rematch: the same player starts another match within 10 min of finishing one
MAX_PROPS_BYTES = 16 * 1024
MAX_EVENT_NAME = 64
OLDEST_ACCEPTED_DAYS = 90       # an offline queue can hold old events; older than this is rejected
FUTURE_SLACK_MS = 24 * 3600 * 1000
WINDOWS = (1, 7, 14, 30, 90, 365)
BUILD_KINDS = ("release", "development", "editor", "port")
TIMESTAMP_KEYS = ("timestamp_utc_ms", "timestamp_utc_iso", "completed_utc_ms", "completed_utc_iso")
SOURCES = ("any", "imported", "live")   # imported = PostHog history: stored by ingest(trusted=True), received_ms == ts_ms
PLAY_GROUP_OF = {"solo_vs_ai": "solo_vs_ai", "solo_alone": "solo"}  # every other play type is "multi"
ACTIVE_DAYS_MAX = 120

# Eras (Rev 65; Shombith: "Old Posthog data", "Pre Beta Editor testing results", "a plan chart"): named date ranges an
# admin sets. Made once, on the first start that finds none; after that only PUT /v1/eras changes them.
ID_PATTERN = re.compile(r"^[a-z0-9_]{1,40}$")
ERA_COLOURS = ("jade", "ruby", "gold", "blue")
ERA_KINDS = ("data", "planned")
MAX_ERAS = 30
DEFAULT_ERAS = [
    {"id": "posthog", "name": "Old PostHog data", "from": None, "to": "2026-11-05", "source": "imported",
     "kind": "data", "builds": [], "colour": "blue",
     "note": "PostHog import: every event that arrived with its own old timestamp"},
    {"id": "pre_beta", "name": "Pre-beta editor testing", "from": "2026-11-06", "to": "2026-12-15", "source": "live",
     "kind": "data", "builds": [], "colour": "jade",
     "note": "Froglet Analytics, editor and test builds, until the beta starts"},
    {"id": "closed_beta", "name": "Closed beta, mobile", "from": "2027-01-11", "to": "2027-02-26", "source": "live",
     "kind": "planned", "builds": [], "colour": "gold", "note": "starts when activation is ready"},
    {"id": "steam", "name": "Steam with keys", "from": "2027-03-01", "to": "2027-03-31", "source": "live",
     "kind": "planned", "builds": [], "colour": "ruby", "note": "by March 2027, before GDC"},
]
DEFAULT_POSTHOG_FROM = "2026-08-08"
DEFAULT_MILESTONES = [{"name": "Server live, PostHog off", "day": "2026-11-06"},
                      {"name": "Activation ready", "day": "2027-01-11"}, {"name": "GDC", "day": "2027-03-16"}]
QUERY_CHARTS = ("bars", "line", "donut", "gantt", "table", "number")
VISIBILITIES = ("me", "readers", "admins")
BUILTIN_TILES = ("new_players", "d1_return", "ftue_finished", "rows_per_play", "matches_per_day", "play_mix",
                 "freestyle_entries")
MAX_SAVED = 200
MAX_TILES = 24
MAX_FILTERS_BYTES = 8 * 1024
# Saved views ("presets", Shombith 2026-11-06: "save our own view as well and make it public"): a chart's switches and
# the page's filters under a name. Ownership is by key name; a private one is its owner's alone.
PRESET_PAGES = ("overview", "players", "cohorts", "eras", "playtypes", "retention", "matches", "ftue", "economy",
                "social", "health", "query", "dashboards")
PRESET_SETTINGS = ("form", "measure", "by", "over")
PRESET_FILTERS = ("build", "days", "play", "era")
PRESET_PLAYS = ("all", "solo_vs_ai", "solo", "multi")
MAX_PRESETS = 500
CHART_ID = re.compile(r"^[A-Za-z0-9_.:-]{1,80}$")
SETTING_VALUE = re.compile(r"^[a-z0-9_]{1,40}$")

# Elements (Shombith, 2026-11-06: "the matches over time should also have element support"). A match's element is
# the mode's genre (ModeGenre.TryElementsFor in the game). Builds from 2026-11-06 send it on game_started; for every
# match before that (the PostHog history included) it comes from mode_elements.json, generated from the game's files.
ELEMENTS = ("Charge", "Mass", "Space", "Time", "Omni")
MODE_ELEMENTS_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mode_elements.json")
_mode_elements = {"stamp": None, "table": {}}


def mode_elements(path=MODE_ELEMENTS_PATH):
    """{game_mode: (primary, secondary|None)} from mode_elements.json, re-read when the file changes."""
    try:
        stamp = os.stat(path).st_mtime_ns
    except OSError:
        return {}
    if stamp != _mode_elements["stamp"]:
        import mode_elements as M
        _mode_elements.update(stamp=stamp, table=M.load(path))
    return _mode_elements["table"]


def _element_name(value):
    if not isinstance(value, str):
        return None
    return next((e for e in ELEMENTS if e.lower() == value.strip().lower()), None)


def element_of(props, table=None):
    """(primary, secondary, source) for a game_started's properties: the event's own element when it sent one
    ("event"), else the mode's row in mode_elements.json ("table"), else (None, None, None)."""
    own = _element_name(props.get("element"))
    if own:
        second = _element_name(props.get("element_secondary"))
        return own, (second if second != own else None), "event"
    found = (mode_elements() if table is None else table).get(str(props.get("game_mode") or ""))
    if found:
        return found[0], found[1], "table"
    return None, None, None


class Invalid(ValueError):
    """A request the server refuses with 400 and this reason."""


def iso_day(value, field):
    if not isinstance(value, str) or not re.match(r"^\d{4}-\d{2}-\d{2}$", value):
        raise Invalid(f"{field} must be a date like 2026-11-06")
    try:
        date.fromisoformat(value)
    except ValueError:
        raise Invalid(f"{field} is not a real date: {value}") from None
    return value


def week_of(day):
    """The Monday of the ISO week a day falls in."""
    d = date.fromisoformat(day)
    return (d - timedelta(days=d.weekday())).isoformat()


def _text(value, field, limit=80, required=True):
    if value is None and not required:
        return ""
    if not isinstance(value, str) or (required and not value.strip()) or len(value) > limit:
        raise Invalid(f"{field} must be text of 1 to {limit} characters")
    return value.strip()


def validate_eras(eras, milestones=None):
    """The checked list PUT /v1/eras stores. Raises Invalid with the reason."""
    if not isinstance(eras, list) or len(eras) > MAX_ERAS:
        raise Invalid(f"eras must be a list of at most {MAX_ERAS}")
    out, seen = [], set()
    for i, era in enumerate(eras):
        if not isinstance(era, dict):
            raise Invalid(f"era {i} must be an object")
        era_id = era.get("id")
        if not isinstance(era_id, str) or not ID_PATTERN.match(era_id):
            raise Invalid(f"era {i}: id must match [a-z0-9_]{{1,40}}")
        if era_id in seen:
            raise Invalid(f"era id {era_id} is used twice")
        seen.add(era_id)
        start, end = iso_day(era.get("from"), f"{era_id}.from"), iso_day(era.get("to"), f"{era_id}.to")
        if start > end:
            raise Invalid(f"{era_id}: from must be on or before to")
        source = era.get("source", "any")
        if source not in SOURCES:
            raise Invalid(f"{era_id}.source must be one of {list(SOURCES)}")
        kind = era.get("kind", "data")
        if kind not in ERA_KINDS:
            raise Invalid(f"{era_id}.kind must be one of {list(ERA_KINDS)}")
        builds = era.get("builds") or []
        if not isinstance(builds, list) or any(b not in BUILD_KINDS for b in builds):
            raise Invalid(f"{era_id}.builds must be a list of {list(BUILD_KINDS)} (empty = every build)")
        colour = era.get("colour", "blue")
        if colour not in ERA_COLOURS:
            raise Invalid(f"{era_id}.colour must be one of {list(ERA_COLOURS)}")
        out.append({"id": era_id, "name": _text(era.get("name"), f"{era_id}.name"), "from": start, "to": end,
                    "source": source, "kind": kind, "builds": sorted(set(builds)), "colour": colour,
                    "note": _text(era.get("note"), f"{era_id}.note", 200, required=False)})
    checked_milestones = None
    if milestones is not None:
        if not isinstance(milestones, list) or len(milestones) > MAX_ERAS:
            raise Invalid(f"milestones must be a list of at most {MAX_ERAS}")
        checked_milestones = []
        for i, mark in enumerate(milestones):
            if not isinstance(mark, dict):
                raise Invalid(f"milestone {i} must be an object")
            checked_milestones.append({"name": _text(mark.get("name"), f"milestone {i}.name"),
                                       "day": iso_day(mark.get("day"), f"milestone {i}.day")})
    return out, checked_milestones


def _new_id(prefix):
    return prefix + secrets.token_hex(5)


def _visible(visibility, owner, name, admin):
    return owner == name or visibility == "readers" or (admin and visibility == "admins")

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
CREATE TABLE IF NOT EXISTS store_meta(key TEXT PRIMARY KEY, value TEXT);
-- What people make in the console (Rev 65). None of it is player data; it stays here, never in aggregates.db.
CREATE TABLE IF NOT EXISTS eras(
    id TEXT PRIMARY KEY, position INTEGER NOT NULL, name TEXT NOT NULL, from_day TEXT NOT NULL, to_day TEXT NOT NULL,
    source TEXT NOT NULL, kind TEXT NOT NULL, builds TEXT NOT NULL, colour TEXT NOT NULL, note TEXT NOT NULL,
    owner TEXT NOT NULL, created_ms INTEGER NOT NULL, updated_ms INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS milestones(
    position INTEGER PRIMARY KEY, name TEXT NOT NULL, day TEXT NOT NULL, owner TEXT NOT NULL,
    created_ms INTEGER NOT NULL, updated_ms INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS segments(
    id TEXT PRIMARY KEY, name TEXT NOT NULL, owner TEXT NOT NULL, shared INTEGER NOT NULL, filters TEXT NOT NULL,
    created_ms INTEGER NOT NULL, updated_ms INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS saved_queries(
    id TEXT PRIMARY KEY, name TEXT NOT NULL, sql TEXT NOT NULL, chart TEXT NOT NULL, x TEXT, y TEXT,
    visibility TEXT NOT NULL, as_table INTEGER NOT NULL, owner TEXT NOT NULL,
    created_ms INTEGER NOT NULL, updated_ms INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS dashboards(
    id TEXT PRIMARY KEY, name TEXT NOT NULL, shared TEXT NOT NULL, tiles TEXT NOT NULL, email TEXT,
    owner TEXT NOT NULL, created_ms INTEGER NOT NULL, updated_ms INTEGER NOT NULL
);
-- Every $identify, kept (2026-11-06): its $set carries selected_vessel, preferred_vessel and
-- total_flight_time_seconds, so the identify history is where the Toybox vessel picks before freestyle_vessel_changed
-- live. One row per (player, time, content): re-importing the PostHog history adds it once.
CREATE TABLE IF NOT EXISTS person_snapshots(
    id INTEGER PRIMARY KEY, distinct_id TEXT NOT NULL, ts_ms INTEGER NOT NULL, day TEXT NOT NULL,
    imported INTEGER NOT NULL, selected_vessel TEXT, preferred_vessel TEXT, total_flight_time_seconds REAL,
    props TEXT NOT NULL, content_hash TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS person_snapshots_key ON person_snapshots(distinct_id, ts_ms, content_hash);
CREATE TABLE IF NOT EXISTS presets(
    id TEXT PRIMARY KEY, name TEXT NOT NULL, owner TEXT NOT NULL, public INTEGER NOT NULL, page TEXT NOT NULL,
    chart_id TEXT NOT NULL, series INTEGER NOT NULL, settings TEXT NOT NULL, filters TEXT NOT NULL,
    created_ms INTEGER NOT NULL, updated_ms INTEGER NOT NULL
);
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
    pause_seconds REAL,          -- duration_seconds - flight_time_seconds: paused or away from the controls
    imported INTEGER,            -- 1: PostHog history (import_posthog.py, stored with its own time); 0: sent here live
    country TEXT,                -- two-letter code of the game_started event (from IP at ingest); NULL if unknown
    element TEXT,                -- the mode's genre element (Charge | Mass | Space | Time | Omni); NULL: the mode has none
    element_secondary TEXT,      -- the second element of a two-element mode (Brood Rush: Space); else NULL
    element_source TEXT,         -- event: the match sent it (builds from 2026-11-06) | table: mode_elements.json by game_mode
    setup TEXT,                  -- the match setup from this player's side (analytics_store.setup_of): competitive |
                                 -- teams | teams_vs_ai | multi_unrecorded (2+ humans, no domain fields: older builds) |
                                 -- solo_alone | solo_vs_1_ai | solo_vs_2_ai | solo_vs_3plus_ai | solo_with_ai_team
    humans_mine INTEGER, humans_other INTEGER,   -- humans on this player's domain (them included) / on every other;
    ai_mine INTEGER, ai_other INTEGER            -- AI on it / on every other. NULL: the build did not send it (before 2026-11-07)
);
CREATE TABLE daily_events(       -- rows received per day, build, event and source (imported 1 = PostHog history)
    day TEXT, build_kind TEXT, event TEXT, rows INTEGER, near_dup_rows INTEGER, players INTEGER, imported INTEGER
);
CREATE TABLE daily_plays(        -- game_started rows against distinct (player, match) pairs: 1.0 is healthy
    day TEXT, build_kind TEXT, rows INTEGER, plays INTEGER, rows_per_play REAL, imported INTEGER
);
CREATE TABLE daily_players(day TEXT, build_kind TEXT, players INTEGER, new_players INTEGER);
CREATE TABLE players_in_window(  -- distinct players seen in the last N days (N = 1, 7, 14, 30, 90, 365)
    build_kind TEXT, days INTEGER, players INTEGER
);
CREATE TABLE returns(            -- players by the play type of their first play; only cohorts 7+ days old
    first_day TEXT, build_kind TEXT, play_type TEXT, players INTEGER, returned_d1 INTEGER, returned_d7 INTEGER,
    imported INTEGER             -- the source of that first play (1 = PostHog history)
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
CREATE TABLE player_counts(      -- distinct players per span, counted once each; NO player id
    span TEXT,                   -- day | week | window:<N> (last N days) | era:<id> | era_week:<id>
    x TEXT,                      -- the day, or the week's Monday, for day and week spans; '' otherwise
    build_kind TEXT,             -- or '*': every build, each player once
    source TEXT,                 -- any | imported (PostHog history) | live; an era's spans carry the era's source
    dim TEXT, key TEXT,          -- none/'' | play_group | play_type | platform | country | game_mode | vessel_class | intensity
                                 -- | element | setup (the match setup) | freestyle/'entered' (entered the Toybox)
                                 -- | freestyle_vessel (a vessel flown there)
    players INTEGER,             -- players active in the span with that key (a player on two platforms counts in both)
    new_players INTEGER          -- of those, players whose first day (in this source) falls inside the span
);
CREATE INDEX player_counts_span ON player_counts(span, source, dim);
CREATE TABLE cohort_weeks(       -- players by the week of their first day, active k weeks later (week_index 0 = that week)
    cohort TEXT, build_kind TEXT, source TEXT, play_group TEXT, week_index INTEGER, players INTEGER
);
CREATE TABLE cohort_days(        -- players by first day and lifespan (first day to last, both counted)
    first_day TEXT, build_kind TEXT, source TEXT,
    play_group TEXT,             -- all | solo_vs_ai | solo | multi (played at least one match of that group)
    lifespan_days INTEGER, players INTEGER, returned_d1 INTEGER, returned_d7 INTEGER,
    lapsed_14d INTEGER           -- not seen in the last 14 days
);
CREATE TABLE freestyle_vessels(  -- Freestyle (the Toybox) per day, build, source and vessel; NO player id
    day TEXT, build_kind TEXT, imported INTEGER, vessel_class TEXT,   -- 'unknown' when the event named none
    entries INTEGER,             -- freestyle_entered (in this vessel: builds from 2026-11-06 name it; older read unknown)
    first_entries INTEGER,       -- of those, is_first (the first entry of that app session)
    stretches INTEGER,           -- freestyle_ended ending in this vessel (builds from 2026-11-06 only)
    seconds REAL,                -- freestyle seconds of those stretches (the vessel at the end of the stretch)
    vessels_flown INTEGER,       -- sum of vessels_flown over those stretches
    switches_to INTEGER,         -- freestyle_vessel_changed into this vessel (builds from 2026-11-06 only)
    switches_from INTEGER,       -- freestyle_vessel_changed out of this vessel
    recovered_to INTEGER,        -- recovered switches into this vessel (the identify history; see freestyle_switches)
    recovered_from INTEGER       -- recovered switches out of this vessel
);
CREATE TABLE freestyle_switches( -- Toybox vessel switches by day, build, source, from and to; NO player id
    day TEXT, build_kind TEXT, imported INTEGER, from_vessel_class TEXT, vessel_class TEXT, switches INTEGER,
    origin TEXT                  -- event: freestyle_vessel_changed (builds after 2026-11-06, exact time) | recovered:
                                 -- selected_vessel changed between two $identify of the player (builds since
                                 -- 2026-10-12; time approximate, when the profile was sent), only before that
                                 -- player's first freestyle_vessel_changed, so nothing counts twice
);
CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT);
-- Saved query tables (Query Studio, "as table") are added after these as t_<name>, rebuilt with the rest.
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


# Match setups (Shombith, 2026-11-07: "a sliced grouping in overview with competitive, teams, teams vs ai as per the
# domains in both multiplayer and for solo like the same how many AIs"). One setup per play, from the LOCAL player's
# side of game_started; play_type above stays as it is. (id, group, label), in the order the console draws them.
SETUPS = (("competitive", "multiplayer", "Competitive"), ("teams", "multiplayer", "Teams"),
          ("teams_vs_ai", "multiplayer", "Teams vs AI"), ("multi_unrecorded", "multiplayer", "Teams not recorded"),
          ("solo_alone", "solo", "Alone"), ("solo_vs_1_ai", "solo", "vs 1 AI"), ("solo_vs_2_ai", "solo", "vs 2 AI"),
          ("solo_vs_3plus_ai", "solo", "vs 3+ AI"), ("solo_with_ai_team", "solo", "With AI teammates"))
SETUP_IDS = tuple(s[0] for s in SETUPS)
DOMAINS_FROM = "2026-11-07"     # the first builds that send domain, humans_on_*_domain(s) and ai_on_*_domain(s)


def _count(value):
    """A head count from an event: a whole number 0 or more, else None (missing, text, negative)."""
    if isinstance(value, bool):
        return None
    n = as_int(value)
    return n if n is not None and n >= 0 else None


def setup_of(props):
    """(setup, humans_mine, humans_other, ai_mine, ai_other) for a game_started's properties.

    Multiplayer is 2+ humans (player_count_human, as play_type_of reads it):
      competitive       humans_on_my_domain == 1 and humans_on_other_domains >= 1 (every human for themselves, 1v1)
      teams             humans_on_my_domain >= 2 and humans_on_other_domains >= 1 (humans together against humans)
      teams_vs_ai       humans_on_other_domains == 0 (every human on one domain; the opponents are AI)
      multi_unrecorded  the domain fields are missing (builds before 2026-11-07 and the PostHog history), or
                        humans_on_other_domains >= 1 without humans_on_my_domain: never guessed
    Solo is 1 human (or none recorded). The opponents are ai_on_other_domains when the build sent it, else
    player_count_ai, else ai_count, else 0, so old data has these too:
      solo_with_ai_team ai_on_my_domain >= 1 (AI teammates; only builds from 2026-11-07 can say so) - a slice of
                        its own, whatever the opponents, so the pie stays one level
      solo_alone        0 AI;  solo_vs_1_ai, solo_vs_2_ai;  solo_vs_3plus_ai  3 or more"""
    humans = as_int(props.get("player_count_human")) or 0
    mine, other = _count(props.get("humans_on_my_domain")), _count(props.get("humans_on_other_domains"))
    ai_mine, ai_other = _count(props.get("ai_on_my_domain")), _count(props.get("ai_on_other_domains"))
    counts = (mine, other, ai_mine, ai_other)
    if humans >= 2:
        if other is None:
            return ("multi_unrecorded",) + counts
        if other == 0:
            return ("teams_vs_ai",) + counts
        if mine is None or mine < 1:
            return ("multi_unrecorded",) + counts
        return ("competitive" if mine == 1 else "teams",) + counts
    if ai_mine is not None and ai_mine >= 1:
        return ("solo_with_ai_team",) + counts
    opponents = next((n for n in (ai_other, _count(props.get("player_count_ai")), _count(props.get("ai_count")))
                      if n is not None), 0)
    return (("solo_alone", "solo_vs_1_ai", "solo_vs_2_ai")[opponents] if opponents < 3 else "solo_vs_3plus_ai",) + counts


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
        self._seed_eras()

    def _seed_eras(self):
        """The four eras of the launch plan, written once. A later empty list is the admins' choice and stays."""
        with self.lock:
            if self.db.execute("SELECT 1 FROM store_meta WHERE key='eras_seeded'").fetchone():
                return
            first = self.db.execute("SELECT MIN(day) FROM events WHERE received_ms = ts_ms").fetchone()[0]
            eras = [dict(e, **({"from": first or DEFAULT_POSTHOG_FROM} if e["from"] is None else {}))
                    for e in DEFAULT_ERAS]
            if eras[0]["from"] > eras[0]["to"]:
                eras[0]["from"] = eras[0]["to"]
            self._write_eras(eras, DEFAULT_MILESTONES, "default")
            self.db.execute("INSERT INTO store_meta(key, value) VALUES('eras_seeded', ?)", (str(now_ms()),))
            self.db.commit()

    # ---------------------------------------------------------------- eras, segments, queries, dashboards (Rev 65)

    def _write_eras(self, eras, milestones, owner):
        now = now_ms()
        before = {r[0]: (r[1], r[2]) for r in self.db.execute("SELECT id, owner, created_ms FROM eras")}
        self.db.execute("DELETE FROM eras")
        for i, era in enumerate(eras):
            made_by, made = before.get(era["id"], (owner, now))
            self.db.execute("INSERT INTO eras VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",
                            (era["id"], i, era["name"], era["from"], era["to"], era["source"], era["kind"],
                             json.dumps(era["builds"]), era["colour"], era.get("note") or "", made_by, made, now))
        if milestones is not None:
            self.db.execute("DELETE FROM milestones")
            for i, mark in enumerate(milestones):
                self.db.execute("INSERT INTO milestones VALUES(?,?,?,?,?,?)", (i, mark["name"], mark["day"], owner, now, now))

    def set_eras(self, eras, milestones, owner):
        checked, marks = validate_eras(eras, milestones)
        with self.lock:
            self._write_eras(checked, marks, owner)
            self.db.commit()
        return self.eras()

    def eras(self):
        with self.lock:
            rows = self.db.execute("SELECT id, name, from_day, to_day, source, kind, builds, colour, note, owner, "
                                   "created_ms, updated_ms FROM eras ORDER BY position").fetchall()
        return [{"id": r[0], "name": r[1], "from": r[2], "to": r[3], "source": r[4], "kind": r[5],
                 "builds": json.loads(r[6]), "colour": r[7], "note": r[8], "owner": r[9], "created_ms": r[10],
                 "updated_ms": r[11]} for r in rows]

    def era(self, era_id):
        """One era by id; Invalid for a malformed id, None for one that does not exist."""
        if not isinstance(era_id, str) or not ID_PATTERN.match(era_id):
            raise Invalid("era must be an era id ([a-z0-9_]{1,40})")
        return next((e for e in self.eras() if e["id"] == era_id), None)

    def milestones(self):
        with self.lock:
            return [{"name": r[0], "day": r[1]} for r in
                    self.db.execute("SELECT name, day FROM milestones ORDER BY position")]

    def segments(self, name, admin):
        with self.lock:
            rows = self.db.execute("SELECT id, name, owner, shared, filters, created_ms, updated_ms FROM segments "
                                   "WHERE owner = ? OR shared = 1 ORDER BY name", (name,)).fetchall()
        return [{"id": r[0], "name": r[1], "owner": r[2], "shared": bool(r[3]), "filters": json.loads(r[4]),
                 "created_ms": r[5], "updated_ms": r[6]} for r in rows]

    def save_segment(self, body, name, admin):
        if not isinstance(body, dict):
            raise Invalid("send {name, filters, shared?}")
        label = _text(body.get("name"), "name")
        filters = body.get("filters")
        if not isinstance(filters, dict):
            raise Invalid("filters must be an object")
        raw = json.dumps(filters, sort_keys=True)
        if len(raw.encode()) > MAX_FILTERS_BYTES:
            raise Invalid(f"filters must be under {MAX_FILTERS_BYTES // 1024} KB")
        shared = body.get("shared", False)
        if not isinstance(shared, bool):
            raise Invalid("shared must be true or false")
        if shared and not admin:
            raise PermissionError("Only an admin key can share a segment.")
        now = now_ms()
        with self.lock:
            if self.db.execute("SELECT COUNT(*) FROM segments").fetchone()[0] >= MAX_SAVED:
                raise Invalid(f"at most {MAX_SAVED} segments; delete one first")
            seg_id = _new_id("s_")
            self.db.execute("INSERT INTO segments VALUES(?,?,?,?,?,?,?)",
                            (seg_id, label, name, int(shared), raw, now, now))
            self.db.commit()
        return {"id": seg_id, "name": label, "owner": name, "shared": shared, "filters": filters,
                "created_ms": now, "updated_ms": now}

    def delete_segment(self, seg_id, name, admin):
        """True when deleted; None when no such segment; PermissionError when it is someone else's."""
        with self.lock:
            row = self.db.execute("SELECT owner FROM segments WHERE id=?", (seg_id,)).fetchone()
            if not row:
                return None
            if row[0] != name and not admin:
                raise PermissionError("Only its owner or an admin can delete a segment.")
            self.db.execute("DELETE FROM segments WHERE id=?", (seg_id,))
            self.db.commit()
        return True

    def _query_row(self, r):
        return {"id": r[0], "name": r[1], "sql": r[2], "chart": r[3], "x": r[4], "y": r[5], "visibility": r[6],
                "as_table": bool(r[7]), "table": ("t_" + r[1]) if r[7] else None, "owner": r[8],
                "created_ms": r[9], "updated_ms": r[10]}

    def queries(self, name=None, admin=True):
        """Saved queries: every one for an admin; shared and own for a reader."""
        with self.lock:
            rows = self.db.execute("SELECT id, name, sql, chart, x, y, visibility, as_table, owner, created_ms, "
                                   "updated_ms FROM saved_queries ORDER BY name").fetchall()
        out = [self._query_row(r) for r in rows]
        return out if admin and name is None else [q for q in out if admin or _visible(q["visibility"], q["owner"], name, False)]

    def query(self, query_id):
        return next((q for q in self.queries() if q["id"] == query_id), None)

    def save_query(self, body, owner, check):
        """Validate and store a saved query. `check(sql)` runs it through the read-only guard (raises on failure)."""
        if not isinstance(body, dict):
            raise Invalid("send {name, sql, chart, x, y, visibility, as_table}")
        label = _text(body.get("name"), "name")
        as_table = body.get("as_table", False)
        if not isinstance(as_table, bool):
            raise Invalid("as_table must be true or false")
        if as_table and not ID_PATTERN.match(label):
            raise Invalid("a query saved as a table needs a name matching [a-z0-9_]{1,40} (the table is t_<name>)")
        chart = body.get("chart", "table")
        if chart not in QUERY_CHARTS:
            raise Invalid(f"chart must be one of {list(QUERY_CHARTS)}")
        visibility = body.get("visibility", "me")
        if visibility not in VISIBILITIES:
            raise Invalid(f"visibility must be one of {list(VISIBILITIES)}")
        x, y = (_text(body.get(k), k, 80, required=False) or None for k in ("x", "y"))
        sql = body.get("sql")
        if not isinstance(sql, str) or not sql.strip() or len(sql) > 8000:
            raise Invalid("sql must be one SELECT of at most 8,000 characters")
        check(sql)
        query_id = body.get("id")
        if query_id is not None and (not isinstance(query_id, str) or not re.match(r"^[a-z0-9_]{1,40}$", query_id)):
            raise Invalid("id must match [a-z0-9_]{1,40}")
        now = now_ms()
        with self.lock:
            existing = self.db.execute("SELECT owner, created_ms FROM saved_queries WHERE id=?",
                                       (query_id,)).fetchone() if query_id else None
            if as_table and self.db.execute("SELECT 1 FROM saved_queries WHERE as_table=1 AND name=? AND id IS NOT ?",
                                            (label, query_id)).fetchone():
                raise Invalid(f"another saved query already makes the table t_{label}")
            if not existing and self.db.execute("SELECT COUNT(*) FROM saved_queries").fetchone()[0] >= MAX_SAVED:
                raise Invalid(f"at most {MAX_SAVED} saved queries; delete one first")
            query_id = query_id or _new_id("q_")
            made_by, made = existing if existing else (owner, now)
            self.db.execute("INSERT OR REPLACE INTO saved_queries VALUES(?,?,?,?,?,?,?,?,?,?,?)",
                            (query_id, label, sql.strip(), chart, x, y, visibility, int(as_table), made_by, made, now))
            self.db.commit()
        return self.query(query_id)

    def delete_query(self, query_id):
        with self.lock:
            gone = self.db.execute("DELETE FROM saved_queries WHERE id=?", (query_id,)).rowcount
            self.db.commit()
        return bool(gone)

    def dashboards(self, name, admin):
        with self.lock:
            rows = self.db.execute("SELECT id, name, shared, tiles, email, owner, created_ms, updated_ms "
                                   "FROM dashboards ORDER BY name").fetchall()
        out = [{"id": r[0], "name": r[1], "shared": r[2], "tiles": json.loads(r[3]),
                "email": json.loads(r[4]) if r[4] else None, "owner": r[5], "created_ms": r[6], "updated_ms": r[7]}
               for r in rows]
        return [d for d in out if _visible(d["shared"], d["owner"], name, admin)]

    def save_dashboard(self, body, owner):
        """`owner` is the saving key's name; a preset:<id> tile must name a saved view that key can see."""
        if not isinstance(body, dict):
            raise Invalid("send {id?, name, shared, tiles}")
        label = _text(body.get("name"), "name")
        shared = body.get("shared", "me")
        if shared not in VISIBILITIES:
            raise Invalid(f"shared must be one of {list(VISIBILITIES)}")
        tiles = body.get("tiles")
        if not isinstance(tiles, list) or len(tiles) > MAX_TILES:
            raise Invalid(f"tiles must be a list of at most {MAX_TILES}")
        known = {q["id"] for q in self.queries()}
        views = {p["id"] for p in self.presets(owner) if p["series"]}
        checked, ids = [], set()
        for i, tile in enumerate(tiles):
            if not isinstance(tile, dict):
                raise Invalid(f"tile {i} must be an object")
            tile_id = tile.get("id")
            if not isinstance(tile_id, str) or not re.match(r"^[A-Za-z0-9_-]{1,40}$", tile_id) or tile_id in ids:
                raise Invalid(f"tile {i}: id must be unique text matching [A-Za-z0-9_-]{{1,40}}")
            ids.add(tile_id)
            source = tile.get("source")
            if not isinstance(source, str) or not (
                    (source.startswith("builtin:") and source[8:] in BUILTIN_TILES)
                    or (source.startswith("query:") and source[6:] in known)
                    or (source.startswith("preset:") and source[7:] in views)):
                raise Invalid(f"tile {tile_id}: source must be query:<saved query id>, preset:<id of a saved chart "
                              f"view you can see> or builtin:<one of {', '.join(BUILTIN_TILES)}>")
            chart = tile.get("chart", "bars")
            if chart not in QUERY_CHARTS:
                raise Invalid(f"tile {tile_id}: chart must be one of {list(QUERY_CHARTS)}")
            w, h = tile.get("w", 1), tile.get("h", 1)
            if not (isinstance(w, int) and not isinstance(w, bool) and 1 <= w <= 4) or \
                    not (isinstance(h, int) and not isinstance(h, bool) and 1 <= h <= 3):
                raise Invalid(f"tile {tile_id}: w must be 1 to 4 and h 1 to 3")
            checked.append({"id": tile_id, "source": source, "title": _text(tile.get("title"), f"tile {tile_id}.title",
                                                                             80, required=False),
                            "chart": chart, "w": w, "h": h})
        email = body.get("email")
        if email is not None and not (isinstance(email, str) and len(email) <= 40):
            raise Invalid("email is a schedule like \"mondays 09:00\" (stored only; nothing is sent yet)")
        dash_id = body.get("id")
        if dash_id is not None and (not isinstance(dash_id, str) or not re.match(r"^[a-z0-9_]{1,40}$", dash_id)):
            raise Invalid("id must match [a-z0-9_]{1,40}")
        now = now_ms()
        with self.lock:
            existing = self.db.execute("SELECT owner, created_ms FROM dashboards WHERE id=?",
                                       (dash_id,)).fetchone() if dash_id else None
            if not existing and self.db.execute("SELECT COUNT(*) FROM dashboards").fetchone()[0] >= MAX_SAVED:
                raise Invalid(f"at most {MAX_SAVED} dashboards; delete one first")
            dash_id = dash_id or _new_id("d_")
            made_by, made = existing if existing else (owner, now)
            self.db.execute("INSERT OR REPLACE INTO dashboards VALUES(?,?,?,?,?,?,?,?)",
                            (dash_id, label, shared, json.dumps(checked),
                             json.dumps({"email": email}) if email else None, made_by, made, now))
            self.db.commit()
        return {"id": dash_id, "name": label, "shared": shared, "tiles": checked,
                "email": {"email": email} if email else None, "owner": made_by, "created_ms": made, "updated_ms": now}

    def delete_dashboard(self, dash_id):
        with self.lock:
            gone = self.db.execute("DELETE FROM dashboards WHERE id=?", (dash_id,)).rowcount
            self.db.commit()
        return bool(gone)

    # ---------------------------------------------------------------- saved views (presets)

    @staticmethod
    def _preset_row(r):
        return {"id": r[0], "name": r[1], "owner": r[2], "public": bool(r[3]), "page": r[4], "chart_id": r[5],
                "series": bool(r[6]), "settings": json.loads(r[7]), "filters": json.loads(r[8]), "created_ms": r[9],
                "updated_ms": r[10]}

    def presets(self, name):
        """The saved views this key name may see: its own and every public one (never someone else's private one,
        whoever asks)."""
        with self.lock:
            rows = self.db.execute("SELECT id, name, owner, public, page, chart_id, series, settings, filters, "
                                   "created_ms, updated_ms FROM presets WHERE owner = ? OR public = 1 "
                                   "ORDER BY page, chart_id, name", (name,)).fetchall()
        return [self._preset_row(r) for r in rows]

    def preset(self, preset_id, name):
        """One saved view this key name may see, or None (a private one of someone else's is None too)."""
        return next((p for p in self.presets(name) if p["id"] == preset_id), None)

    def save_preset(self, body, owner, eras=()):
        """Create, or (with the id of one's own) replace, a saved view. Raises Invalid (400) or PermissionError (403)."""
        if not isinstance(body, dict):
            raise Invalid("send {id?, name, page, chart_id, settings, filters, public}")
        unknown = set(body) - {"id", "name", "page", "chart_id", "settings", "filters", "public", "series"}
        if unknown:
            raise Invalid(f"unknown fields: {', '.join(sorted(unknown))}")
        label = _text(body.get("name"), "name")
        page = body.get("page")
        if page not in PRESET_PAGES:
            raise Invalid(f"page must be one of {list(PRESET_PAGES)}")
        chart_id = body.get("chart_id")
        if not isinstance(chart_id, str) or not CHART_ID.match(chart_id):
            raise Invalid("chart_id must match [A-Za-z0-9_.:-]{1,80}")
        settings = body.get("settings")
        if not isinstance(settings, dict) or set(settings) - set(PRESET_SETTINGS):
            raise Invalid(f"settings must be an object with only {list(PRESET_SETTINGS)}")
        for k, v in settings.items():
            if v is not None and (not isinstance(v, str) or not SETTING_VALUE.match(v)):
                raise Invalid(f"settings.{k} must be null or match [a-z0-9_]{{1,40}}")
        filters = body.get("filters", {})
        if not isinstance(filters, dict) or set(filters) - set(PRESET_FILTERS):
            raise Invalid(f"filters must be an object with only {list(PRESET_FILTERS)}")
        if "build" in filters and filters["build"] not in BUILD_KINDS:
            raise Invalid(f"filters.build must be one of {list(BUILD_KINDS)}")
        if "days" in filters and (not isinstance(filters["days"], int) or isinstance(filters["days"], bool)
                                  or filters["days"] not in WINDOWS):
            raise Invalid(f"filters.days must be one of {list(WINDOWS)}")
        if "play" in filters and filters["play"] not in PRESET_PLAYS:
            raise Invalid(f"filters.play must be one of {list(PRESET_PLAYS)}")
        if filters.get("era"):
            if not isinstance(filters["era"], str) or not ID_PATTERN.match(filters["era"]):
                raise Invalid("filters.era must be an era id or empty")
            if eras and filters["era"] not in {e["id"] for e in eras}:
                raise Invalid(f"no era with id {filters['era']}")
        public = body.get("public", False)
        series = body.get("series", False)
        if not isinstance(public, bool) or not isinstance(series, bool):
            raise Invalid("public and series must be true or false")
        preset_id = body.get("id")
        if preset_id is not None and (not isinstance(preset_id, str) or not ID_PATTERN.match(preset_id)):
            raise Invalid("id must match [a-z0-9_]{1,40}")
        now = now_ms()
        with self.lock:
            existing = self.db.execute("SELECT owner, public, created_ms FROM presets WHERE id=?",
                                       (preset_id,)).fetchone() if preset_id else None
            if preset_id and not existing:
                raise Invalid("no saved view with that id; leave id out to make a new one")
            if existing and existing[0] != owner:
                if not existing[1]:
                    raise Invalid("no saved view with that id; leave id out to make a new one")   # private: not shown
                raise PermissionError("Only its owner can change a saved view; save it under a new name instead.")
            if not existing and self.db.execute("SELECT COUNT(*) FROM presets").fetchone()[0] >= MAX_PRESETS:
                raise Invalid(f"at most {MAX_PRESETS} saved views; delete one first")
            preset_id = preset_id or _new_id("p_")
            made = existing[2] if existing else now
            clean_filters = {k: filters[k] for k in PRESET_FILTERS if k in filters and filters[k] not in (None, "")}
            self.db.execute("INSERT OR REPLACE INTO presets VALUES(?,?,?,?,?,?,?,?,?,?,?)",
                            (preset_id, label, owner, int(public), page, chart_id, int(series),
                             json.dumps(settings, sort_keys=True), json.dumps(clean_filters, sort_keys=True), made, now))
            self.db.commit()
        return self.preset(preset_id, owner)

    def delete_preset(self, preset_id, name, moderator=False):
        """True when deleted. None when this key may not see it (no such id, or someone else's private view).
        PermissionError for someone else's public view, unless `moderator` (a key in admin_keys)."""
        with self.lock:
            row = self.db.execute("SELECT owner, public FROM presets WHERE id=?", (preset_id,)).fetchone()
            if not row or (row[0] != name and not row[1]):
                return None
            if row[0] != name and not moderator:
                raise PermissionError("Only its owner (or a key in admin_keys) can delete a saved view.")
            self.db.execute("DELETE FROM presets WHERE id=?", (preset_id,))
            self.db.commit()
        return True

    # ---------------------------------------------------------------- ingest

    def _reject(self, day, reason, event):
        self.db.execute(
            "INSERT INTO rejects(day, reason, event, count) VALUES(?,?,?,1) "
            "ON CONFLICT(day, reason, event) DO UPDATE SET count = count + 1",
            (day, reason, (event or "")[:MAX_EVENT_NAME]))

    def ingest(self, batch, received=None, country=None, trusted=False, max_age_days=OLDEST_ACCEPTED_DAYS):
        """Store a batch in the game's wire format ([{event, properties, timestamp}]). Returns counts per outcome.
        Nothing raises for one bad event: it is counted under rejects and the rest of the batch is stored.

        `trusted` is for the server's own import of history (import_posthog.py), never for HTTP: each item may then
        carry its own "country", its receive time is its event time, and events up to `max_age_days` old are kept."""
        received = received or now_ms()
        today = day_of(received)
        result = {"accepted": 0, "rejected": 0, "resent": 0, "near_duplicates": 0, "erased": 0}
        with self.lock:
            for item in batch:
                item_country = (item.get("country") or country) if trusted and isinstance(item, dict) else country
                outcome = self._ingest_one(item, received, today, item_country, trusted, max_age_days)
                result[outcome] = result.get(outcome, 0) + 1
            self.db.commit()
        return result

    def _ingest_one(self, item, received, today, country=None, trusted=False, max_age_days=OLDEST_ACCEPTED_DAYS):
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
        if ts < received - max_age_days * 86400 * 1000 or ts > received + FUTURE_SLACK_MS:
            self._reject(today, "bad_timestamp", event)
            return "rejected"
        if trusted:
            received = ts                   # history keeps its own time, so "last event received" stays true

        if event == "$identify":
            return self._identify(distinct_id, props, received, country, ts, trusted)

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

    def _identify(self, distinct_id, props, received, country=None, ts=None, trusted=False):
        person = props.get("$set") if isinstance(props.get("$set"), dict) else {}
        if person.get("gdpr_deletion_requested") is True:
            # The in-game "delete my data" button flags the person (PostHogAnalyticsSink.RequestDataDeletion).
            # PostHog could not finish that from the client; here it is finished at once.
            self._erase_locked(distinct_id, "in_game_request", received)
            return "erased"
        self._snapshot(distinct_id, person, ts or received, trusted)
        row = self.db.execute("SELECT props FROM people WHERE distinct_id=?", (distinct_id,)).fetchone()
        merged = json.loads(row[0]) if row else {}
        merged.update(person)
        self.db.execute(
            "INSERT INTO people(distinct_id, props, country, first_seen_ms, last_seen_ms) VALUES(?,?,?,?,?) "
            "ON CONFLICT(distinct_id) DO UPDATE SET props=excluded.props, last_seen_ms=excluded.last_seen_ms, "
            "country=COALESCE(excluded.country, people.country)",
            (distinct_id, json.dumps(merged, sort_keys=True), country, received, received))
        return "accepted"

    def _snapshot(self, distinct_id, person, ts, imported):
        """Keep this $identify as one row of the player's history (the people table holds only the latest values)."""
        raw = json.dumps(person, sort_keys=True, separators=(",", ":"), default=str)
        if len(raw.encode()) > MAX_PROPS_BYTES:
            return
        text = lambda k: str(person[k]).strip()[:64] if isinstance(person.get(k), str) and person[k].strip() else None
        self.db.execute(
            "INSERT OR IGNORE INTO person_snapshots(distinct_id, ts_ms, day, imported, selected_vessel, preferred_vessel,"
            " total_flight_time_seconds, props, content_hash) VALUES(?,?,?,?,?,?,?,?,?)",
            (distinct_id, ts, day_of(ts), 1 if imported else 0, text("selected_vessel"), text("preferred_vessel"),
             as_float(person.get("total_flight_time_seconds")), raw, hashlib.sha256(raw.encode()).hexdigest()))

    # ---------------------------------------------------------------- erasure, retention, backup

    def erase(self, distinct_id, source):
        with self.lock:
            deleted = self._erase_locked(distinct_id, source, now_ms())
            self.db.commit()
        return deleted

    def _erase_locked(self, distinct_id, source, requested):
        deleted = self.db.execute("DELETE FROM events WHERE distinct_id=?", (distinct_id,)).rowcount
        deleted += self.db.execute("DELETE FROM people WHERE distinct_id=?", (distinct_id,)).rowcount
        deleted += self.db.execute("DELETE FROM person_snapshots WHERE distinct_id=?", (distinct_id,)).rowcount
        # The log keeps a salted hash, never the id itself, so the record of a deletion is not a record of the player.
        self.db.execute("INSERT INTO erasures(player_hash, requested_ms, done_ms, rows_deleted, source)"
                        " VALUES(?,?,?,?,?)", (player_hash(distinct_id), requested, now_ms(), deleted, source))
        return deleted

    def apply_retention(self, retention_days):
        cutoff = day_of(now_ms() - retention_days * 86400 * 1000)
        with self.lock:
            gone = self.db.execute("DELETE FROM events WHERE day < ?", (cutoff,)).rowcount
            self.db.execute("DELETE FROM person_snapshots WHERE day < ?", (cutoff,))
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

    @staticmethod
    def _range_where(build, since, until, source, builds):
        where, args = "day >= ?", [since]
        if until:
            where += " AND day <= ?"
            args.append(until)
        if build:
            where += " AND build_kind = ?"
            args.append(build)
        if builds:
            where += f" AND build_kind IN ({','.join('?' * len(builds))})"
            args += list(builds)
        if source == "imported":
            where += " AND received_ms = ts_ms"
        elif source == "live":
            where += " AND received_ms <> ts_ms"
        return where, args

    def people_list(self, build=None, days=90, search="", limit=500, play="all", since=None, until=None,
                    source="any", builds=None):
        """One row per player seen in the range (the last `days`, or since..until for an era): who, where, on what,
        how much, and the days they were active (at most the latest 120). `play` keeps players who played at least
        one match of that group. Newest first."""
        since = since or day_of(now_ms() - (days - 1) * 86400 * 1000)
        where, args = self._range_where(build, since, until, source, builds)
        db = self._read()
        rows = db.execute(
            "SELECT distinct_id, MIN(ts_ms) AS first_ms, MAX(ts_ms) AS last_ms, COUNT(*) AS events, "
            "COUNT(DISTINCT day) AS days_active, "
            "COUNT(DISTINCT CASE WHEN event='game_started' THEN match_id END) AS matches, "
            "COUNT(DISTINCT CASE WHEN event='game_completed' THEN match_id END) AS finished, "
            # each player's play-type mix (play_type_of: 2+ humans is multiplayer, else 1+ AI is solo vs AI)
            "COUNT(DISTINCT CASE WHEN event='game_started' AND CAST(json_extract(props, '$.player_count_human') "
            "AS INTEGER) >= 2 THEN match_id END) AS multi, "
            "COUNT(DISTINCT CASE WHEN event='game_started' AND COALESCE(CAST(json_extract(props, "
            "'$.player_count_human') AS INTEGER), 0) < 2 AND CAST(json_extract(props, '$.player_count_ai') AS INTEGER) "
            ">= 1 THEN match_id END) AS solo_vs_ai, "
            "COUNT(DISTINCT CASE WHEN event='game_completed' AND json_extract(props, '$.player_won') IN (1, 'true') "
            "THEN match_id END) AS won, "
            "GROUP_CONCAT(DISTINCT platform) AS platforms, GROUP_CONCAT(DISTINCT country) AS countries, "
            "GROUP_CONCAT(DISTINCT build_kind) AS builds, MAX(app_version) AS app_version, "
            "GROUP_CONCAT(DISTINCT day) AS active_days, "
            # Freestyle (the Toybox): entries since the PostHog history; seconds from builds that send freestyle_ended
            "SUM(CASE WHEN event='freestyle_entered' AND near_dup=0 THEN 1 ELSE 0 END) AS freestyle_entries, "
            "ROUND(SUM(CASE WHEN event='freestyle_ended' AND near_dup=0 THEN COALESCE(CAST(json_extract(props, "
            "'$.seconds') AS REAL), 0) ELSE 0 END), 1) AS freestyle_seconds "
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
            row = dict(r)
            row["solo"] = max(0, (row["matches"] or 0) - (row["multi"] or 0) - (row["solo_vs_ai"] or 0))
            if play != "all" and not row.get(play):
                continue
            row["active_days"] = sorted(filter(None, (r["active_days"] or "").split(",")))[-ACTIVE_DAYS_MAX:]
            out.append({**row, "display_name": name, "crystal_balance": props.get("crystal_balance"),
                        "platforms": sorted(filter(None, (r["platforms"] or "").split(","))),
                        "countries": sorted(filter(None, (r["countries"] or "").split(",")))})
        out.sort(key=lambda p: p["last_ms"], reverse=True)
        return {"days": days, "build": build, "play": play, "since": since, "until": until, "total": len(out),
                "people": out[:limit]}

    def lifespans(self, build, since, until=None, source="any", builds=None, play="all", limit=500):
        """Cohorts page, profile readers only: each player whose first day (in this build and source) falls in the
        range, with their first and last day, active days and the play group they played most."""
        where, args = self._range_where(build, "0000-00-00", None, source, builds)
        db = self._read()
        rows = db.execute(
            "SELECT distinct_id, MIN(day) AS first_day, MAX(day) AS last_day, GROUP_CONCAT(DISTINCT day) AS days, "
            "COUNT(DISTINCT CASE WHEN event='game_started' AND CAST(json_extract(props, '$.player_count_human') "
            "AS INTEGER) >= 2 THEN match_id END) AS multi, "
            "COUNT(DISTINCT CASE WHEN event='game_started' AND COALESCE(CAST(json_extract(props, "
            "'$.player_count_human') AS INTEGER), 0) < 2 AND CAST(json_extract(props, '$.player_count_ai') AS INTEGER) "
            ">= 1 THEN match_id END) AS solo_vs_ai, "
            "COUNT(DISTINCT CASE WHEN event='game_started' THEN match_id END) AS matches "
            f"FROM events WHERE {where} GROUP BY distinct_id", args).fetchall()
        names = {r[0]: json.loads(r[1]).get("display_name") or "" for r in
                 db.execute("SELECT distinct_id, props FROM people")}
        db.close()
        out = []
        for r in rows:
            if r["first_day"] < since or (until and r["first_day"] > until):
                continue
            mix = {"multi": r["multi"] or 0, "solo_vs_ai": r["solo_vs_ai"] or 0,
                   "solo": max(0, (r["matches"] or 0) - (r["multi"] or 0) - (r["solo_vs_ai"] or 0))}
            if play != "all" and not mix[play]:
                continue
            mostly = max(("solo_vs_ai", "solo", "multi"), key=lambda g: mix[g]) if r["matches"] else None
            out.append({"distinct_id": r["distinct_id"], "display_name": names.get(r["distinct_id"], ""),
                        "first_day": r["first_day"], "last_day": r["last_day"],
                        "lifespan_days": (date.fromisoformat(r["last_day"]) - date.fromisoformat(r["first_day"])).days + 1,
                        "active_days": sorted((r["days"] or "").split(","))[-ACTIVE_DAYS_MAX:], "mostly": mostly})
        out.sort(key=lambda p: (p["first_day"], p["distinct_id"]))
        return out[:limit]

    def person(self, distinct_id):
        """Everything stored about one player, arranged for their profile page."""
        db = self._read()
        events = db.execute("SELECT ts_ms, day, event, match_id, build_kind, app_version, platform, country, "
                            "install_id, near_dup, props FROM events WHERE distinct_id=? ORDER BY ts_ms",
                            (distinct_id,)).fetchall()
        row = db.execute("SELECT props, country, first_seen_ms, last_seen_ms FROM people WHERE distinct_id=?",
                         (distinct_id,)).fetchone()
        recovered = recovered_switches(db, distinct_id).get(distinct_id, [])
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
                "duration_seconds": as_float(dp.get("duration_seconds")),
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
        by_hour_weekday = [[0] * 24 for _ in range(7)]          # match starts, weekday 0 = Monday, UTC hours
        for e, _ in starts.values():
            at = datetime.fromtimestamp(e["ts_ms"] / 1000, tz=timezone.utc)
            by_hour_weekday[at.weekday()][at.hour] += 1
        earned_by = {}
        for x in economy:
            if x["event"] == "crystals_earned":
                earned_by[str(x["source"] or "unknown")] = earned_by.get(str(x["source"] or "unknown"), 0) + (x["amount"] or 0)
        lifespan = ((date.fromisoformat(events[-1]["day"]) - date.fromisoformat(events[0]["day"])).days + 1
                    if events else None)
        freestyle = _freestyle_profile(events, props_of, recovered)
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
                         "launch_to_menu_p50": _percentile(launches, 0.5),
                         "by_hour_weekday": by_hour_weekday},
            "lifespan_days": lifespan,
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
                        "recent": list(reversed(economy))[:60], "unlocks": unlocks,
                        "by_source": [{"source": k, "earned": v} for k, v in
                                      sorted(earned_by.items(), key=lambda kv: -kv[1])]},
            "freestyle": freestyle,
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
        # imported: PostHog history is exactly the rows ingest(trusted=True) stored with received_ms == ts_ms
        starts = {}
        for distinct_id, match_id, ts, build, platform, version, props, imported, country in reader.execute(
                "SELECT distinct_id, match_id, ts_ms, build_kind, platform, app_version, props, received_ms = ts_ms, "
                "country FROM events WHERE event='game_started' AND match_id IS NOT NULL ORDER BY ts_ms"):
            key = (distinct_id, match_id)
            if key not in starts:
                starts[key] = (ts, build, platform, version, json.loads(props), imported, country)
        dones = {}
        for distinct_id, match_id, ts, props in reader.execute(
                "SELECT distinct_id, match_id, ts_ms, props FROM events "
                "WHERE event='game_completed' AND match_id IS NOT NULL ORDER BY ts_ms"):
            dones.setdefault((distinct_id, match_id), (ts, json.loads(props)))

        table = mode_elements()
        by_player = {}
        for (distinct_id, match_id), start in starts.items():
            by_player.setdefault(distinct_id, []).append((start[0], match_id))
        rows = []
        for distinct_id, plays in by_player.items():
            plays.sort()
            for index, (start_ts, match_id) in enumerate(plays):
                ts, build, platform, version, props, imported, country = starts[(distinct_id, match_id)]
                done = dones.get((distinct_id, match_id))
                rematched = None
                if done:
                    following = plays[index + 1][0] if index + 1 < len(plays) else None
                    rematched = 1 if following is not None and 0 <= (following - done[0]) / 1000 <= REMATCH_GAP_S else 0
                done_props = done[1] if done else {}
                flight = as_float(done_props.get("flight_time_seconds"))
                duration = as_float(done_props.get("duration_seconds"))
                won = done_props.get("player_won")
                element, second, element_source = element_of(props, table)
                rows.append((match_id, day_of(ts), ts, build, platform, version, props.get("game_mode"),
                             as_int(props.get("intensity")), props.get("vessel_class"), play_type_of(props),
                             as_int(props.get("player_count_human")), as_int(props.get("player_count_ai")),
                             1 if done else 0, rematched, flight, duration,
                             (1 if won is True or won == "true" else 0) if done and won is not None else None,
                             max(0.0, duration - flight) if flight is not None and duration is not None else None,
                             imported, country, element, second, element_source) + setup_of(props))
        out.executemany(f"INSERT INTO plays VALUES({','.join('?' * 28)})", rows)
        # which modes had no element (old names, modes with no genre): Data health lists them
        missing = {}
        for r in rows:
            if r[20] is None:
                entry = missing.setdefault(str(r[6] or "unknown"), [0, 0])
                entry[0] += 1
                entry[1] += 1 if r[18] else 0
        out.execute("INSERT INTO meta VALUES('modes_without_element', ?)", (json.dumps(
            [{"game_mode": k, "plays": v[0], "imported_plays": v[1]} for k, v in
             sorted(missing.items(), key=lambda kv: (-kv[1][0], kv[0]))]),))

        out.executemany("INSERT INTO daily_events VALUES(?,?,?,?,?,?,?)", reader.execute(
            "SELECT day, build_kind, event, COUNT(*), SUM(near_dup), COUNT(DISTINCT distinct_id), received_ms = ts_ms "
            "FROM events GROUP BY day, build_kind, event, received_ms = ts_ms").fetchall())
        out.executemany("INSERT INTO daily_plays VALUES(?,?,?,?,?,?)", reader.execute(
            "SELECT day, build_kind, COUNT(*), COUNT(DISTINCT distinct_id || '|' || match_id), "
            "ROUND(1.0 * COUNT(*) / COUNT(DISTINCT distinct_id || '|' || match_id), 2), received_ms = ts_ms FROM events "
            "WHERE event='game_started' AND match_id IS NOT NULL GROUP BY day, build_kind, received_ms = ts_ms").fetchall())

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
            first_play.setdefault((distinct_id, start[1]), (day_of(start[0]), play_type_of(start[4]), start[5]))
        mature = day_of(now_ms() - 7 * 86400 * 1000)
        cohorts = {}
        for (distinct_id, build), (day, play_type, imported) in first_play.items():
            if day > mature:
                continue
            seen = days_seen.get((distinct_id, build), set())
            start = datetime.strptime(day, "%Y-%m-%d")
            next_day = (start + timedelta(days=1)).strftime("%Y-%m-%d")
            week = {(start + timedelta(days=n)).strftime("%Y-%m-%d") for n in range(1, 8)}
            entry = cohorts.setdefault((day, build, play_type, imported), [0, 0, 0])
            entry[0] += 1
            entry[1] += 1 if next_day in seen else 0
            entry[2] += 1 if seen & week else 0
        out.executemany("INSERT INTO returns VALUES(?,?,?,?,?,?,?)",
                        [(d, b, p, v[0], v[1], v[2], i) for (d, b, p, i), v in sorted(cohorts.items())])
        _rebuild_details(reader, out, first_day, days_seen)
        _rebuild_freestyle(reader, out)
        eras = [dict(id=r[0], start=r[1], end=r[2], source=r[3], builds=json.loads(r[4])) for r in reader.execute(
            "SELECT id, from_day, to_day, source, builds FROM eras ORDER BY position")]
        _rebuild_spans(reader, out, starts, eras, table)
        out.executemany("INSERT INTO rejects VALUES(?,?,?,?)",
                        reader.execute("SELECT day, reason, event, count FROM rejects").fetchall())
        out.executemany("INSERT INTO meta VALUES(?,?)", [("built_ms", str(now_ms()))])
        out.commit()
        saved = reader.execute("SELECT name, sql FROM saved_queries WHERE as_table=1 ORDER BY name").fetchall()
        reader.close()
        _materialise_saved(tmp, out, saved)
        out.close()
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


SUMMARY_KEYS = ("game_mode", "intensity", "vessel_class", "from_vessel_class", "element", "seconds", "vessels_flown",
                "amount", "source", "balance", "setting", "value", "reason", "step_name", "action_type", "action_label",
                "vessel", "quest", "key", "display_name")
FREESTYLE_NOTE = ("Entries go back to the PostHog history (August 2026). Minutes and exact switches exist only from "
                  "builds that send freestyle_ended and freestyle_vessel_changed (after 2026-11-06). Switches marked "
                  "approx. are recovered from the identify history (builds since 2026-10-12): the selected vessel "
                  "changed between two profile updates, at about that time.")


def _freestyle_profile(events, props_of, recovered=()):
    """One player's Freestyle (Toybox) block for their profile: visits, minutes, the vessels flown there, and every
    switch in order (when, from, to, seconds into the stretch). `recovered` switches (from the identify history) are
    marked approx: their time is when the profile was sent, not the click."""
    entries = first = stretches = 0
    seconds, flown, switched_to, switches, recent, per_day = 0.0, {}, {}, [], [], {}
    first_ms = last_ms = first_minutes_ms = None
    for e, p in zip(events, props_of):
        name = e["event"]
        if name not in ("freestyle_entered", "freestyle_ended", "freestyle_vessel_changed") or e["near_dup"]:
            continue
        first_ms = first_ms or e["ts_ms"]
        last_ms = e["ts_ms"]
        day = per_day.setdefault(e["day"], {"day": e["day"], "entries": 0, "seconds": 0.0, "switches": 0})
        vessel = p.get("vessel_class") if isinstance(p.get("vessel_class"), str) and p.get("vessel_class") else None
        if name == "freestyle_entered":
            entries += 1
            day["entries"] += 1
            first += 1 if p.get("is_first") in (True, "true", "True", 1) else 0
            if vessel:
                flown[vessel] = flown.get(vessel, 0) + 1
        elif name == "freestyle_ended":
            stretches += 1
            sec = max(0.0, as_float(p.get("seconds")) or 0.0)
            seconds += sec
            day["seconds"] = round(day["seconds"] + sec, 1)
            first_minutes_ms = first_minutes_ms or e["ts_ms"]
            recent.append({"ts_ms": e["ts_ms"], "seconds": round(sec, 1), "vessel_class": vessel,
                           "vessels_flown": as_int(p.get("vessels_flown"))})
        else:
            day["switches"] += 1
            if vessel:
                flown[vessel] = flown.get(vessel, 0) + 1
                switched_to[vessel] = switched_to.get(vessel, 0) + 1
            switches.append({"ts_ms": e["ts_ms"], "from": p.get("from_vessel_class") or None, "to": vessel,
                             "seconds_in_freestyle": as_float(p.get("seconds_in_freestyle")), "approx": False})
    for ts, source, vessel, _imported in recovered:
        switched_to[vessel] = switched_to.get(vessel, 0) + 1
        flown[vessel] = flown.get(vessel, 0) + 1
        switches.append({"ts_ms": ts, "from": source, "to": vessel, "seconds_in_freestyle": None, "approx": True})
        first_ms = min(first_ms or ts, ts)
        last_ms = max(last_ms or ts, ts)
    switches.sort(key=lambda x: x["ts_ms"])
    return {"entries": entries, "first_entries": first, "stretches": stretches, "seconds": round(seconds, 1),
            "minutes": round(seconds / 60.0, 1), "vessels_flown": _counts_dict(flown),
            "switched_to": _counts_dict(switched_to), "switch_count": len(switches),
            "recovered_switches": sum(1 for x in switches if x["approx"]),
            "switches": switches[-200:], "stretches_recent": list(reversed(recent))[:50],
            "per_day": sorted(per_day.values(), key=lambda d: d["day"])[-90:],
            "first_ms": first_ms, "last_ms": last_ms, "first_minutes_ms": first_minutes_ms, "note": FREESTYLE_NOTE}


def _counts_dict(counts):
    return dict(sorted(counts.items(), key=lambda kv: (-kv[1], kv[0])))


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


RECOVERED_NOTE = ("recovered switches come from the identify history: the player's selected vessel changed between two "
                  "$identify (the Toybox's Vessel Changer sets it, builds since 2026-10-12); the time is when the "
                  "profile was sent, not the click, and several switches between two identifies read as one")


def derive_switches(snapshots):
    """[(ts_ms, from, to, imported)] from one player's [(ts_ms, selected_vessel, imported)] in time order: a switch
    wherever the selected vessel differs from the last one seen. Empty values are skipped, not a change."""
    out, last = [], None
    for ts, vessel, imported in snapshots:
        if not vessel:
            continue
        if last is not None and vessel != last:
            out.append((ts, last, vessel, imported))
        last = vessel
    return out


def recovered_switches(reader, distinct_id=None):
    """{player: [(ts_ms, from, to, imported)]} recovered from person_snapshots, only before the player's first live
    freestyle_vessel_changed (from then on the event itself counts)."""
    where, args = ("WHERE distinct_id = ?", (distinct_id,)) if distinct_id else ("", ())
    live = dict(reader.execute(f"SELECT distinct_id, MIN(ts_ms) FROM events {'WHERE distinct_id = ? AND' if distinct_id else 'WHERE'} "
                               "event = 'freestyle_vessel_changed' GROUP BY distinct_id", args).fetchall())
    per = {}
    for player, ts, vessel, imported in reader.execute(
            f"SELECT distinct_id, ts_ms, selected_vessel, imported FROM person_snapshots {where} ORDER BY distinct_id, ts_ms, id",
            args):
        per.setdefault(player, []).append((ts, vessel, imported))
    out = {}
    for player, snaps in per.items():
        cut = live.get(player)
        found = [x for x in derive_switches(snaps) if cut is None or x[0] < cut]
        if found:
            out[player] = found
    return out


def _rebuild_freestyle(reader, out):
    """freestyle_vessels and freestyle_switches: the Toybox counted per day, build, source and vessel. Near-duplicate
    rows (the October double-send) are left out. Recovered switches (derive_switches over the identify history) carry
    origin 'recovered'. Player ids are used only to group a player's history; nothing written to `out` carries one."""
    vessels, switches = {}, {}
    for day, build, imported, event, raw in reader.execute(
            "SELECT day, build_kind, received_ms = ts_ms, event, props FROM events WHERE event IN ('freestyle_entered', "
            "'freestyle_ended', 'freestyle_vessel_changed') AND near_dup = 0"):
        props = json.loads(raw)
        vessel = str(props.get("vessel_class") or "unknown")
        if event == "freestyle_entered":
            first = props.get("is_first") in (True, "true", "True", 1)
            _add(vessels, (day, build, imported, vessel), 1, int(first), 0, 0.0, 0, 0, 0, 0, 0)
        elif event == "freestyle_ended":
            seconds = max(0.0, as_float(props.get("seconds")) or 0.0)
            flown = max(1, as_int(props.get("vessels_flown")) or 1)
            _add(vessels, (day, build, imported, vessel), 0, 0, 1, seconds, flown, 0, 0, 0, 0)
        else:
            source = str(props.get("from_vessel_class") or "unknown")
            _add(vessels, (day, build, imported, vessel), 0, 0, 0, 0.0, 0, 1, 0, 0, 0)
            _add(vessels, (day, build, imported, source), 0, 0, 0, 0.0, 0, 0, 1, 0, 0)
            _add(switches, (day, build, imported, source, vessel, "event"), 1)
    # A snapshot has no build of its own: it takes the build its player sent most events from that day (else ever).
    builds_by_day, builds_ever = {}, {}
    for player, day, build, n in reader.execute(
            "SELECT distinct_id, day, build_kind, COUNT(*) FROM events GROUP BY distinct_id, day, build_kind"):
        if n > builds_by_day.get((player, day), ("", 0))[1]:
            builds_by_day[(player, day)] = (build, n)
        _add(builds_ever, (player, build), n)
    best = {}
    for (player, build), (n,) in builds_ever.items():
        if n > best.get(player, ("", 0))[1]:
            best[player] = (build, n)
    for player, found in recovered_switches(reader).items():
        for ts, source, vessel, imported in found:
            day = day_of(ts)
            build = (builds_by_day.get((player, day)) or best.get(player) or ("release", 0))[0]
            _add(vessels, (day, build, imported, vessel), 0, 0, 0, 0.0, 0, 0, 0, 1, 0)
            _add(vessels, (day, build, imported, source), 0, 0, 0, 0.0, 0, 0, 0, 0, 1)
            _add(switches, (day, build, imported, source, vessel, "recovered"), 1)
    out.executemany("INSERT INTO freestyle_vessels VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    [k + (v[0], v[1], v[2], round(v[3], 1), v[4], v[5], v[6], v[7], v[8]) for k, v in sorted(vessels.items())])
    out.executemany("INSERT INTO freestyle_switches VALUES(?,?,?,?,?,?,?)",
                    [k[:5] + (v[0], k[5]) for k, v in sorted(switches.items())])


def play_group_of(play_type):
    return PLAY_GROUP_OF.get(play_type, "multi")


def _rebuild_spans(reader, out, starts, eras, table=None):
    """player_counts, cohort_weeks and cohort_days: distinct players per day, week, last-N-days window and era, split
    by platform, country, play type, mode, vessel and intensity, for every source (any / imported / live). Player ids
    are used here only to count each player once; nothing written to `out` carries one."""
    since = {n: day_of(now_ms() - (n - 1) * 86400 * 1000) for n in WINDOWS}
    act = {}   # player -> build -> day -> imported flag -> {(dim, key)}
    for player, build, day, flag, platform, country in reader.execute(
            "SELECT distinct_id, build_kind, day, received_ms = ts_ms, platform, country FROM events "
            "GROUP BY distinct_id, build_kind, day, received_ms = ts_ms, platform, country"):
        act.setdefault(player, {}).setdefault(build, {}).setdefault(day, {}).setdefault(flag, set()).update(
            (("none", ""), ("platform", platform or "unknown"), ("country", country or "unknown")))
    for (player, _match), (ts, build, _platform, _version, props, flag, _country) in starts.items():
        play_type = play_type_of(props)
        intensity = as_int(props.get("intensity"))
        act.setdefault(player, {}).setdefault(build, {}).setdefault(day_of(ts), {}).setdefault(flag, set()).update(
            (("play_type", play_type), ("play_group", play_group_of(play_type)),
             ("game_mode", str(props.get("game_mode") or "unknown")),
             ("vessel_class", str(props.get("vessel_class") or "unknown")),
             ("intensity", str(intensity) if intensity is not None else "unknown"),
             ("element", element_of(props, table)[0] or "none"), ("setup", setup_of(props)[0])))
    # Freestyle (the Toybox): who entered it, and which vessels they flew there (entered in, ended in, switched to)
    for player, build, day, flag, event, vessel, from_vessel in reader.execute(
            "SELECT distinct_id, build_kind, day, received_ms = ts_ms, event, json_extract(props, '$.vessel_class'), "
            "json_extract(props, '$.from_vessel_class') FROM events WHERE event IN ('freestyle_entered', "
            "'freestyle_ended', 'freestyle_vessel_changed') AND near_dup = 0"):
        keys = {("freestyle", "entered")} if event == "freestyle_entered" else set()
        for v in (vessel, from_vessel):
            if isinstance(v, str) and v:
                keys.add(("freestyle_vessel", v))
        if keys:
            act.setdefault(player, {}).setdefault(build, {}).setdefault(day, {}).setdefault(flag, set()).update(keys)

    counts, weeks_out, days_out = {}, {}, {}

    def bump(span, x, build, source, keys, new):
        for dim, key in keys:
            row = counts.setdefault((span, x, build, source, dim, key), [0, 0])
            row[0] += 1
            row[1] += new

    def view(builds, by_build, source):
        per_day = {}
        for build in builds:
            for day, flags in by_build[build].items():
                for flag, keys in flags.items():
                    if source == "any" or (source == "imported") == bool(flag):
                        per_day.setdefault(day, set()).update(keys)
        return per_day

    for player, by_build in act.items():
        for build in list(by_build) + ["*"]:
            builds = [build] if build != "*" else list(by_build)
            for source in SOURCES:
                per_day = view(builds, by_build, source)
                if not per_day:
                    continue
                first, last = min(per_day), max(per_day)
                weeks = {}
                for day, keys in per_day.items():
                    bump("day", day, build, source, keys, int(day == first))
                    weeks.setdefault(week_of(day), set()).update(keys)
                for week, keys in weeks.items():
                    bump("week", week, build, source, keys, int(week == week_of(first)))
                for n, start in since.items():
                    keys = set().union(*(k for d, k in per_day.items() if d >= start))
                    if keys:
                        bump(f"window:{n}", "", build, source, keys, int(first >= start))
                if build == "*":
                    continue
                cohort = week_of(first)
                indexes = {(date.fromisoformat(w) - date.fromisoformat(cohort)).days // 7 for w in weeks}
                groups = {"all"} | {k for keys in per_day.values() for dim, k in keys if dim == "play_group"}
                start = date.fromisoformat(first)
                d1 = int((start + timedelta(days=1)).isoformat() in per_day)
                d7 = int(any((start + timedelta(days=n)).isoformat() in per_day for n in range(1, 8)))
                life = (date.fromisoformat(last) - start).days + 1
                for group in groups:
                    for k in indexes:
                        weeks_out[(cohort, build, source, group, k)] = weeks_out.get((cohort, build, source, group, k), 0) + 1
                    _add(days_out, (first, build, source, group, life), 1, d1, d7, int(last < since[14]))
        for era in eras:
            allowed = [b for b in by_build if not era["builds"] or b in era["builds"]]
            for build in allowed + (["*"] if allowed else []):
                per_day = view([build] if build != "*" else allowed, by_build, era["source"])
                inside = {d: k for d, k in per_day.items() if era["start"] <= d <= era["end"]}
                if not inside:
                    continue
                first = min(per_day)
                new = era["start"] <= first <= era["end"]
                bump("era:" + era["id"], "", build, era["source"], set().union(*inside.values()), int(new))
                weeks = {}
                for day, keys in inside.items():
                    weeks.setdefault(week_of(day), set()).update(keys)
                for week, keys in weeks.items():
                    bump("era_week:" + era["id"], week, build, era["source"], keys, int(new and week == week_of(first)))
    out.executemany("INSERT INTO player_counts VALUES(?,?,?,?,?,?,?,?)", [k + tuple(v) for k, v in counts.items()])
    out.executemany("INSERT INTO cohort_weeks VALUES(?,?,?,?,?,?)", [k + (v,) for k, v in weeks_out.items()])
    out.executemany("INSERT INTO cohort_days VALUES(?,?,?,?,?,?,?,?,?)", [k + tuple(v) for k, v in days_out.items()])


MAX_TABLE_ROWS = 10_000


def _materialise_saved(tmp_path, out, saved):
    """Query Studio's "save as table": each such saved query runs through the same read-only guard as the Ask box,
    against the fresh aggregates, and its rows become t_<name> beside them. A query that fails is skipped and named
    in meta (saved_table_errors), so one bad query never stops the rebuild."""
    import analytics_views as V
    errors = {}
    for name, sql in saved:
        if not ID_PATTERN.match(name):
            errors[name] = "the name is not a table name"
            continue
        try:
            result = V.run_readonly_sql(tmp_path, sql, max_rows=MAX_TABLE_ROWS)
        except V.UnsafeQuery as exc:
            errors[name] = str(exc)
            continue
        columns, used = [], set()
        for i, col in enumerate(result["columns"]):
            label = re.sub(r"[^A-Za-z0-9_]", "_", str(col)).strip("_")[:40] or f"c{i}"
            while label.lower() in used:
                label += "_"
            used.add(label.lower())
            columns.append(label)
        out.execute(f'CREATE TABLE "t_{name}"({", ".join(chr(34) + c + chr(34) for c in columns)})')
        if result["rows"]:
            out.executemany(f'INSERT INTO "t_{name}" VALUES({",".join("?" * len(columns))})', result["rows"])
    out.execute("INSERT OR REPLACE INTO meta VALUES('saved_table_errors', ?)", (json.dumps(errors),))
    out.commit()
