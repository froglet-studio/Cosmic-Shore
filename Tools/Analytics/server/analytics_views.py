"""What the dashboard shows (Frogma Rev 50 B, the Console), read from aggregates.db only - never from the raw events.

Each function returns plain JSON for one page of the console: overview, retention, matches, data health.
`run_readonly_sql` is the one door the Ask box has into the data: SELECT-only, aggregates only, row and time capped.
Standard library only.
"""
import json
import os
import re
import sqlite3
import time
from datetime import datetime, timedelta, timezone

from analytics_store import BUILD_KINDS, DEVICE_GROUPS, DOMAINS_FROM, SETUP_IDS, SETUPS, difficulty_rank

PLAY_TYPES = ("solo_vs_ai", "solo_alone", "coop", "adversarial", "mixed", "multi_human_unknown")
MAX_ASK_ROWS = 200
MAX_ASK_STEPS = 5_000_000       # sqlite progress-handler ticks: a runaway query is stopped, not waited on


def _open(agg_path):
    if not os.path.exists(agg_path):
        return None
    db = sqlite3.connect(f"file:{agg_path}?mode=ro", uri=True)
    db.row_factory = sqlite3.Row
    return db


def _since(days):
    return (datetime.now(timezone.utc) - timedelta(days=days - 1)).strftime("%Y-%m-%d")


def _pct(part, whole):
    return round(100.0 * part / whole, 1) if whole else None


def _empty(build, days):
    return {"build": build, "days": days, "built_ms": None, "empty": True}


def _built_ms(db):
    row = db.execute("SELECT value FROM meta WHERE key='built_ms'").fetchone()
    return int(row[0]) if row else None


WINDOWS = (1, 7, 14, 30, 90, 365)
# The console's Play filter (Shombith, 2026-11-06: "need features to filter solo vs ai, multiplayer"): a group of play
# types, as SQL on the plays / returns tables' play_type. Tables without a play type (players_in_window, daily_plays,
# daily_events) are not filtered; a page lists those as "unfiltered" so the console can say so.
PLAYS = {"all": "", "solo_vs_ai": "play_type = 'solo_vs_ai'", "solo": "play_type = 'solo_alone'",
         "multi": "play_type IN ('coop', 'adversarial', 'mixed', 'multi_human_unknown')"}


def _play(play):
    if play not in PLAYS:
        raise ValueError(f"play must be one of {list(PLAYS)}")
    return f" AND ({PLAYS[play]})" if PLAYS[play] else ""


# The Build filter (Shombith, 2026-11-07: "Can we have a all button too?"): one build kind, or all of them together.
# Rows (plays, events) sum across the kinds; distinct players read the rebuild's '*' rows, so nobody counts twice.
BUILD_FILTERS = ("all",) + BUILD_KINDS
# The Device filter (Shombith, 2026-11-07: "platform specific views like iOS iPad android etc"): a group of stored
# devices, as SQL on a table's device column (analytics_store.DEVICE_GROUPS). Distinct players come from the *_device
# twins of the player tables, counted once inside each group. Tables with no device (economy, mode_signals, invites,
# the Players / FTUE / Economy / Social pages' tables) are not filtered; a page lists those in "unfiltered".
DEVICES = {"all": "", **{g: "device IN (" + ", ".join(f"'{d}'" for d in members) + ")"
                         for g, members in DEVICE_GROUPS.items()}}
DEVICE_NOTE = ("iPhone and iPad, and Android phone and tablet, are told apart only from builds that send device "
               "(from 2026-11-07); older iOS builds read iOS (device not recorded), older Android Android (size not "
               "recorded) and older web builds Web (desktop or mobile not recorded), so iPhone, iPad, Phones and "
               "Tablets hold only the newer builds")


def _device(device):
    if device not in DEVICES:
        raise BadRequest(f"device must be one of {list(DEVICES)}")
    return f" AND ({DEVICES[device]})" if DEVICES[device] else ""


def build_where(build, column="build_kind"):
    """SQL and arguments for a page that reads one build's rows, or every build's (build=all)."""
    if build == "all":
        return f"{column} IN ({','.join('?' * len(BUILD_KINDS))})", list(BUILD_KINDS)
    return f"{column} = ?", [build]


def distinct_build(build):
    """The build_kind to read from a table of distinct players: the build, or '*' (every build, each player once)."""
    return "*" if build == "all" else build


class BadRequest(ValueError):
    """A question these views cannot answer as asked; the server answers 400 with the reason."""


def _today():
    return datetime.now(timezone.utc).strftime("%Y-%m-%d")


class Scope:
    """What a page counts: one build (or several), a day range and a source, from the days window or an era.
    An era (Rev 65) replaces the days window with its own range (to today at most), counts only its source
    (imported = PostHog history, live = sent here) and, when it names builds, only those."""

    def __init__(self, build, days=90, play="all", era=None, builds=None, device="all"):
        if days not in WINDOWS:
            raise BadRequest(f"days must be one of {list(WINDOWS)}")
        if build not in BUILD_FILTERS:
            raise BadRequest(f"build must be one of {list(BUILD_FILTERS)}")
        _play(play)
        _device(device)
        self.build, self.days, self.play, self.era, self.device = build, days, play, era, device
        if era:
            self.since, self.until = era["from"], min(era["to"], _today())
            self.source = era["source"]
            self.span_none, self.span_week = "era:" + era["id"], "era_week:" + era["id"]
        else:
            self.since, self.until, self.source = _since(days), _today(), "any"
            self.span_none, self.span_week = f"window:{days}", "week"
        chosen = list(builds) if builds is not None else list(BUILD_KINDS) if build == "all" else [build]
        if era and era.get("builds"):
            chosen = [b for b in chosen if b in era["builds"]]
        self.builds = chosen
        # build=all reads distinct players from the '*' rows (each player once). player_counts' '*' era rows hold the
        # era's own builds; the other player tables' '*' rows hold every build, so an era that names builds sums them.
        everyone = build == "all" and builds is None
        self.pc_builds = ["*"] if everyone else chosen
        self.distinct_builds = ["*"] if everyone and not (era and era.get("builds")) else chosen

    def with_builds(self, builds):
        return Scope(self.build, self.days, self.play, self.era, builds, self.device)

    def counted(self, table):
        """A table of distinct players for this Device filter: the table itself (every device) or its _device twin,
        and the SQL that keeps the group's rows."""
        if self.device == "all":
            return table, ""
        return table + "_device", f" AND device = '{self.device}'"

    def where(self, day="day", sourced=True, play=True, builds=None, device=True, distinct=False):
        """SQL (and its arguments) for these builds, days, source, play group and device. `sourced` is False for a
        table without an imported column; `play` False for one without a play type; `device` False for one without a
        device; `distinct` True for a table of distinct players (build=all then reads its '*' rows)."""
        chosen = builds if builds is not None else self.distinct_builds if distinct else self.builds
        sql = f"build_kind IN ({','.join('?' * len(chosen)) or 'NULL'}) AND {day} BETWEEN ? AND ?"
        args = [*chosen, self.since, self.until]
        if sourced and self.source != "any":
            sql += " AND imported = ?"
            args.append(1 if self.source == "imported" else 0)
        if play:
            sql += _play(self.play)
        if device:
            sql += _device(self.device)
        return sql, args

    def unfiltered(self, play_ignores=(), source_ignores=(), era_ignores=(), device_ignores=()):
        """The names a page lists as ignoring the play filter, the era or the device filter."""
        out = list(play_ignores) if self.play != "all" else []
        if self.era:
            out += [n for n in era_ignores if n not in out]
            if self.source != "any":
                out += [n for n in source_ignores if n not in out]
        if self.device != "all":
            out += [n for n in device_ignores if n not in out]
        return out

    def describe(self):
        return {"from": self.since, "to": self.until, "source": self.source,
                "era": self.era["id"] if self.era else None, "device": self.device}


def _days_between(since, until):
    start, end = datetime.strptime(since, "%Y-%m-%d"), datetime.strptime(until, "%Y-%m-%d")
    return [(start + timedelta(days=n)).strftime("%Y-%m-%d") for n in range(max(0, (end - start).days + 1))]


def _player_count(db, sc, dim="none", key="", span=None, builds=None):
    """Distinct players in the scope from player_counts (one build at a time is exact; '*' is every build), or from
    player_counts_device inside the Device filter's group."""
    chosen = sc.pc_builds if builds is None else builds
    if not chosen:
        return 0
    table, dx = sc.counted("player_counts")
    row = db.execute(
        f"SELECT SUM(players) FROM {table} WHERE span=? AND source=? AND dim=? AND key=? AND "
        f"build_kind IN ({','.join('?' * len(chosen))}){dx}",
        (span or sc.span_none, sc.source, dim, key, *chosen)).fetchone()
    return row[0] or 0


def overview(agg_path, build="release", days=90, play="all", era=None, device="all"):
    sc = Scope(build, days, play, era, device=device)
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    q = lambda sql, *a: db.execute(sql, a).fetchall()
    w, a = sc.where()
    wd, ad = sc.where(play=False)
    if era or device != "all":
        distinct_players = _player_count(db, sc)
    else:
        window = q("SELECT players FROM players_in_window WHERE build_kind=? AND days=?", distinct_build(build), days)
        distinct_players = window[0][0] if window else 0
    plays = q("SELECT COUNT(*) AS n, SUM(completed) AS done, COUNT(DISTINCT match_id) AS matches, "
              "SUM(CASE WHEN humans>=2 THEN 1 ELSE 0 END) AS multi FROM plays WHERE " + w, *a)[0]
    multi_matches = q("SELECT COUNT(DISTINCT match_id) FROM plays WHERE humans>=2 AND " + w, *a)[0][0]
    rpp = q("SELECT SUM(rows), SUM(plays) FROM daily_plays WHERE " + wd, *ad)[0]
    by_type = retention_by_play_type(db, build, sc.since, play, scope=sc)
    solo_ai = next((r for r in by_type if r["play_type"] == "solo_vs_ai"), None)
    every = sc.with_builds(BUILD_KINDS)
    wh, ah = every.where()
    health = q("SELECT build_kind, COUNT(*), SUM(completed) FROM plays WHERE " + wh + " GROUP BY build_kind", *ah)
    wr, ar = every.where(play=False)
    rpp_by_build = {r[0]: (round(1.0 * r[1] / r[2], 2) if r[2] else None) for r in
                    q("SELECT build_kind, SUM(rows), SUM(plays) FROM daily_plays WHERE " + wr + " GROUP BY build_kind",
                      *ar)}
    yesterday = (datetime.now(timezone.utc) - timedelta(days=1)).strftime("%Y-%m-%d")
    bw, ba = build_where(build)
    events_24h = [dict(r) for r in db.execute(
        "SELECT event, SUM(rows) AS rows, SUM(near_dup_rows) AS near_duplicates FROM daily_events "
        f"WHERE {bw} AND day>=?{_device(device)} GROUP BY event ORDER BY rows DESC LIMIT 12", (*ba, yesterday))]
    # Rev 65: the tiles marketing and business read (counts only)
    marks = ",".join("?" * len(sc.builds)) or "NULL"
    pmarks = ",".join("?" * len(sc.pc_builds)) or "NULL"
    pct_table, dx = sc.counted("player_counts")
    new_by_day = {r[0]: r[1] for r in q(
        f"SELECT x, SUM(new_players) FROM {pct_table} WHERE span='day' AND source=? AND dim='none' "
        f"AND build_kind IN ({pmarks}) AND x BETWEEN ? AND ?{dx} GROUP BY x", sc.source, *sc.pc_builds, sc.since,
        sc.until)}
    split = lambda dim: [{dim: r[0], "players": r[1]} for r in q(
        f"SELECT key, SUM(players) AS n FROM {pct_table} WHERE span=? AND source=? AND dim=? "
        f"AND build_kind IN ({pmarks}){dx} GROUP BY key ORDER BY n DESC, key", sc.span_none, sc.source, dim,
        *sc.pc_builds)]
    event_rows = lambda name: q("SELECT COALESCE(SUM(rows), 0) FROM daily_events WHERE event=? AND " + wd,
                                name, *ad)[0][0]
    crystals = {r[0]: r[1] or 0 for r in q(
        f"SELECT kind, SUM(amount) FROM economy WHERE build_kind IN ({marks}) AND day BETWEEN ? AND ? GROUP BY kind",
        *sc.builds, sc.since, sc.until)}
    whales = q("SELECT COALESCE(SUM(players), 0) FROM balances WHERE build_kind=? AND bucket='2,000+'",
               distinct_build(build))[0][0]
    setups, setup_history = _setups(db, sc)
    active, _per_day = _active(db, sc)
    session_tiles, _seconds = _session_tiles(db, sc)
    result = {
        "build": build, "days": days, "play": play, "built_ms": _built_ms(db), **sc.describe(),
        "unfiltered": sc.unfiltered(["players", "rows_per_play", "events_24h", "marketing", "business", "sessions"],
                                    ["business.crystals_earned", "business.crystals_spent"],
                                    ["events_24h", "business.whales_2000"], ["business"]),
        "tiles": {
            "players": distinct_players or 0,
            "matches": plays["matches"] or 0,
            "finished_pct": _pct(plays["done"] or 0, plays["n"] or 0),
            "rematch_solo_vs_ai_pct": solo_ai["rematch_pct"] if solo_ai else None,
            "rows_per_play": round(rpp[0] / rpp[1], 2) if rpp[1] else None,
            "multi_human_matches": multi_matches or 0,
        },
        "rematch_by_play_type": by_type,
        "match_health_by_build": [
            {"build": b, "starts": n, "finished_pct": _pct(done or 0, n), "rows_per_play": rpp_by_build.get(b)}
            for b, n, done in health],
        "events_24h": events_24h,
        "marketing": {
            "new_players_per_day": [{"day": d, "new_players": new_by_day.get(d, 0)}
                                    for d in _days_between(sc.since, sc.until)],
            "by_country": split("country"), "by_platform": split("platform"), "by_device": split("device"),
            "shares": event_rows("share_triggered"), "invites_sent": event_rows("party_invite_sent")},
        "business": {"crystals_earned": crystals.get("earned", 0),
                     "crystals_spent": crystals.get("spent", 0) + crystals.get("unlock", 0),
                     "whales_2000": whales},
        # Match setups (Shombith, 2026-11-07): multiplayer first, then solo, every setup listed (zeros included)
        "setups": setups, "setup_history": setup_history, "setup_notes": SETUP_NOTES,
        # Sessions (round 67): DAU / WAU / MAU from any event; sessions and their length from builds after 2026-11-07
        "sessions": {**active, **session_tiles},
        "device_note": DEVICE_NOTE,
    }
    db.close()
    return result


def _setups(db, sc):
    """Overview's match setups in SETUPS order: matches, plays, finished / rematch / won % and players for each, and
    what history the multiplayer ones have. Players come from player_counts (a player in two setups is in both)."""
    w, a = sc.where()
    got = {r[0]: r for r in db.execute(
        "SELECT COALESCE(setup, 'multi_unrecorded'), COUNT(DISTINCT match_id), COUNT(*), SUM(completed), "
        "SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END), SUM(won), COUNT(won) FROM plays WHERE " + w + " GROUP BY 1", a)}
    players = {}
    if sc.pc_builds:
        marks = ",".join("?" * len(sc.pc_builds))
        table, dx = sc.counted("player_counts")
        players = dict(db.execute(
            f"SELECT key, SUM(players) FROM {table} WHERE span=? AND source=? AND dim='setup' "
            f"AND build_kind IN ({marks}){dx} GROUP BY key", (sc.span_none, sc.source, *sc.pc_builds)).fetchall())
    allowed = set(PLAY_SETUPS.get(sc.play, SETUP_IDS))      # the Play filter: a setup outside its group has no one
    out = []
    for setup, group, label in SETUPS:
        r = got.get(setup) or (setup, 0, 0, 0, 0, 0, 0)
        out.append({"setup": setup, "label": label, "group": group, "matches": r[1] or 0, "plays": r[2] or 0,
                    "finished_pct": _pct(r[3] or 0, r[2] or 0), "rematch_pct": _pct(r[4] or 0, r[3] or 0),
                    "win_pct": _pct(r[5] or 0, r[6] or 0),
                    "players": (players.get(setup) or 0) if setup in allowed else 0})
    multi = [s for s in out if s["group"] == "multiplayer"]
    first = db.execute("SELECT MIN(day) FROM plays WHERE humans_other IS NOT NULL AND build_kind IN "
                       f"({','.join('?' * len(sc.builds)) or 'NULL'})", sc.builds).fetchone()[0]
    history = {"domains_from": DOMAINS_FROM, "first_domain_day": first,
               "multi_plays": sum(s["plays"] for s in multi),
               "multi_unrecorded_plays": next(s["plays"] for s in multi if s["setup"] == "multi_unrecorded")}
    history["multi_unrecorded_pct"] = _pct(history["multi_unrecorded_plays"], history["multi_plays"])
    return out, history


def retention_by_play_type(db, build, since, play="all", scope=None):
    if scope is None:
        pf = _play(play)
        where, args = "build_kind=? AND day>=?" + pf, [build, since]
        rwhere, rargs = "build_kind=? AND first_day>=?" + pf, [build, since]
    else:
        where, args = scope.where()
        rwhere, rargs = scope.where(day="first_day", distinct=True)
    rows = db.execute(
        "SELECT play_type, COUNT(*) AS plays, COUNT(DISTINCT match_id) AS matches, SUM(completed) AS completed, "
        "SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END) AS rematches FROM plays "
        "WHERE " + where + " GROUP BY play_type", args).fetchall()
    returns = {r[0]: r for r in db.execute(
        "SELECT play_type, SUM(players), SUM(returned_d1), SUM(returned_d7) FROM returns "
        "WHERE " + rwhere + " GROUP BY play_type", rargs)}
    out = []
    for r in rows:
        ret = returns.get(r["play_type"])
        out.append({
            "play_type": r["play_type"], "matches": r["matches"], "completed": r["completed"] or 0,
            "rematches": r["rematches"] or 0, "rematch_pct": _pct(r["rematches"] or 0, r["completed"] or 0),
            "cohort_players": ret[1] if ret else 0,
            "d1_pct": _pct(ret[2], ret[1]) if ret else None, "d7_pct": _pct(ret[3], ret[1]) if ret else None,
        })
    out.sort(key=lambda r: PLAY_TYPES.index(r["play_type"]) if r["play_type"] in PLAY_TYPES else 99)
    return out


def retention(agg_path, build="release", days=90, play="all", era=None, device="all"):
    sc = Scope(build, days, play, era, device=device)
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    rw, ra = sc.where(day="first_day", distinct=True)
    result = {"build": build, "days": days, "play": play, "built_ms": _built_ms(db), **sc.describe(),
              "unfiltered": [],
              "by_play_type": retention_by_play_type(db, build, sc.since, play, scope=sc),
              "cohorts": [dict(r) for r in db.execute(
                  "SELECT first_day, play_type, SUM(players) AS players, SUM(returned_d1) AS returned_d1, "
                  "SUM(returned_d7) AS returned_d7 FROM returns WHERE " + rw +
                  " GROUP BY first_day, play_type ORDER BY first_day", ra)]}
    db.close()
    return result


def _every_day(since, days, by_day, until=None):
    """One row per day of the range, zeros where nothing was played, so a chart's axis is time and not just data."""
    until = until or (datetime.strptime(since, "%Y-%m-%d") + timedelta(days=days - 1)).strftime("%Y-%m-%d")
    return [by_day.get(day, {"day": day, "matches": 0, "plays": 0, "completed": 0})
            for day in _days_between(since, until)]


def matches(agg_path, build="release", days=90, play="all", era=None, device="all"):
    sc = Scope(build, days, play, era, device=device)
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    w, a = sc.where()
    result = {
        "build": build, "days": days, "play": play, "built_ms": _built_ms(db), **sc.describe(),
        "unfiltered": sc.unfiltered(["signals"], ["signals"], (), ["signals"]),
        "per_day": _every_day(sc.since, days, {r["day"]: dict(r) for r in db.execute(
            "SELECT day, COUNT(DISTINCT match_id) AS matches, COUNT(*) AS plays, SUM(completed) AS completed "
            "FROM plays WHERE " + w + " GROUP BY day ORDER BY day", a)}, sc.until),
        "by_mode": [dict(r) for r in db.execute(
            "SELECT game_mode, COUNT(*) AS plays, SUM(completed) AS completed, "
            "ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct, "
            "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, "
            "ROUND(AVG(flight_time_seconds), 1) AS avg_flight_s FROM plays "
            "WHERE " + w + " GROUP BY game_mode ORDER BY plays DESC", a)],
        "by_device": [dict(r) for r in db.execute(
            "SELECT COALESCE(device, 'other') AS device, COUNT(*) AS plays, COUNT(DISTINCT match_id) AS matches, "
            "ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct, "
            "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, ROUND(AVG(flight_time_seconds), 1) AS avg_flight_s "
            "FROM plays WHERE " + w + " GROUP BY 1 ORDER BY plays DESC", a)],
        "device_note": DEVICE_NOTE,
        "by_vessel": [dict(r) for r in db.execute(
            "SELECT vessel_class, COUNT(*) AS plays, ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct, "
            "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, ROUND(AVG(flight_time_seconds), 1) AS avg_flight_s "
            "FROM plays WHERE " + w + " GROUP BY vessel_class ORDER BY plays DESC", a)],
        "signals": [dict(r) for r in db.execute(
            "SELECT game_mode, SUM(favorited) AS favorited, SUM(unfavorited) AS unfavorited, SUM(fails) AS fails, "
            "SUM(shares) AS shares FROM mode_signals WHERE " + sc.where(sourced=False, play=False, device=False)[0] +
            " GROUP BY game_mode ORDER BY favorited - unfavorited DESC",
            sc.where(sourced=False, play=False, device=False)[1])],
        "pause": dict(db.execute(
            "SELECT ROUND(AVG(pause_seconds), 1) AS avg_pause_s, ROUND(AVG(duration_seconds), 1) AS avg_duration_s, "
            "ROUND(100.0 * SUM(CASE WHEN pause_seconds > 30 THEN 1 ELSE 0 END) / NULLIF(COUNT(pause_seconds), 0), 1) "
            "AS paused_over_30s_pct FROM plays WHERE " + w, a).fetchone()),
        "by_intensity": [dict(r) for r in db.execute(
            "SELECT intensity, COUNT(*) AS plays, ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct "
            "FROM plays WHERE " + w + " GROUP BY intensity ORDER BY intensity", a)],
        # the match's element: its own (builds from 2026-11-06) or its mode's genre (mode_elements.json); a
        # two-element mode is under its first, and its second is listed in element_secondary, never added in
        "by_element": sorted([dict(r) for r in db.execute(
            "SELECT COALESCE(element, 'none') AS element, COUNT(*) AS plays, COUNT(DISTINCT match_id) AS matches, "
            "ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct, "
            "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, "
            "SUM(CASE WHEN element_source = 'event' THEN 1 ELSE 0 END) AS sent_by_build, "
            "SUM(CASE WHEN element_source = 'table' THEN 1 ELSE 0 END) AS from_mode_table "
            "FROM plays WHERE " + w + " GROUP BY 1", a)],
            key=lambda r: ELEMENT_ORDER.index(r["element"]) if r["element"] in ELEMENT_ORDER else 99),
        "element_secondary": [dict(r) for r in db.execute(
            "SELECT element_secondary AS element, COUNT(*) AS plays, COUNT(DISTINCT match_id) AS matches FROM plays "
            "WHERE element_secondary IS NOT NULL AND " + w + " GROUP BY 1 ORDER BY plays DESC", a)],
    }
    for row in result["by_mode"]:
        found = db.execute("SELECT element, element_secondary FROM plays WHERE COALESCE(game_mode, '') = ? AND "
                           "element IS NOT NULL ORDER BY start_ms DESC LIMIT 1", (row["game_mode"] or "",)).fetchone()
        row["element"], row["element_secondary"] = (found[0], found[1]) if found else (None, None)
    db.close()
    return result


def health(agg_path, store_facts, days=30):
    db = _open(agg_path)
    if db is None:
        return {"days": days, "built_ms": None, "empty": True, **store_facts}
    since = _since(days)
    result = {
        "days": days, "built_ms": _built_ms(db),
        "rows_per_play": [dict(r) for r in db.execute(
            "SELECT day, build_kind, SUM(rows) AS rows, SUM(plays) AS plays, "
            "ROUND(1.0 * SUM(rows) / NULLIF(SUM(plays), 0), 2) AS rows_per_play FROM daily_plays WHERE day>=? "
            "GROUP BY day, build_kind ORDER BY day", (since,))],
        "near_duplicates": [dict(r) for r in db.execute(
            "SELECT day, SUM(rows) AS rows, SUM(near_dup_rows) AS near_duplicates FROM daily_events "
            "WHERE day>=? GROUP BY day ORDER BY day", (since,))],
        "rejects": [dict(r) for r in db.execute(
            "SELECT reason, event, SUM(count) AS count FROM rejects WHERE day>=? GROUP BY reason, event "
            "ORDER BY count DESC LIMIT 20", (since,))],
        "editor_share_pct": None,
        "cloud_save_failures": [dict(r) for r in db.execute(
            "SELECT key, SUM(count) AS count, MAX(day) AS last_day FROM cloud_save_failures WHERE day>=? "
            "GROUP BY key ORDER BY count DESC", (since,))],
        "double_payouts": [dict(r) for r in db.execute(
            "SELECT day, build_kind, source, count, amount FROM double_payouts WHERE day>=? ORDER BY day DESC",
            (since,))],
        **store_facts,
    }
    split = db.execute("SELECT build_kind, COUNT(DISTINCT match_id) FROM plays WHERE day>=? GROUP BY build_kind",
                       (since,)).fetchall()
    total = sum(n for _, n in split)
    editor = sum(n for b, n in split if b == "editor")
    result["editor_share_pct"] = _pct(editor, total)
    # Every build kind (the web build included, zeros listed) and every device seen: events in the window, last day.
    by_build = {r[0]: r[1:] for r in db.execute(
        "SELECT build_kind, SUM(CASE WHEN day>=? THEN rows ELSE 0 END), MAX(day) FROM daily_events GROUP BY 1", (since,))}
    result["events_by_build"] = [{"build_kind": b, "events": (by_build.get(b) or (0, None))[0] or 0,
                                  "last_day": (by_build.get(b) or (0, None))[1]} for b in BUILD_KINDS]
    result["events_by_device"] = [{"device": r[0], "events": r[1] or 0, "last_day": r[2]} for r in db.execute(
        "SELECT COALESCE(device, 'other'), SUM(CASE WHEN day>=? THEN rows ELSE 0 END) AS n, MAX(day) FROM daily_events "
        "GROUP BY 1 ORDER BY n DESC, 1", (since,))]
    result["device_note"] = DEVICE_NOTE
    meta = db.execute("SELECT value FROM meta WHERE key='modes_without_element'").fetchone()
    result["modes_without_element"] = json.loads(meta[0]) if meta else []      # every match ever, by game_mode
    meta = db.execute("SELECT value FROM meta WHERE key='new_events'").fetchone()
    result["new_events"] = json.loads(meta[0]) if meta else []                 # this round's events: last received
    db.close()
    return result


def audience(agg_path, build="release", days=90):
    """Who plays: active and new players per day, platforms, countries, the first-day funnel, launch time, how
    sessions end."""
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    since = _since(days)
    one = distinct_build(build)                  # tables of distinct players: build=all reads '*' (each player once)
    bw, ba = build_where(build)                  # tables of rows: build=all sums every build
    funnel = db.execute(
        "SELECT SUM(players), SUM(reached_menu), SUM(started_match), SUM(finished_match), SUM(finished_ftue), "
        "SUM(came_back) FROM funnel WHERE build_kind=? AND first_day>=?", (one, since)).fetchone()
    steps = ["New players", "Reached the menu", "Started a match", "Finished a match", "Finished the FTUE",
             "Came back another day"]
    result = {
        "build": build, "days": days, "built_ms": _built_ms(db),
        "per_day": _every_day_players(since, days, {r["day"]: dict(r) for r in db.execute(
            "SELECT day, players, new_players FROM daily_players WHERE build_kind=? AND day>=?", (one, since))}),
        "platforms": [dict(r) for r in db.execute(
            "SELECT platform, players FROM players_by_platform WHERE build_kind=? AND days=? ORDER BY players DESC",
            (one, days))],
        "countries": [dict(r) for r in db.execute(
            "SELECT country, players FROM players_by_country WHERE build_kind=? AND days=? ORDER BY players DESC",
            (one, days))],
        "funnel": [{"step": name, "players": funnel[i] or 0} for i, name in enumerate(steps)] if funnel[0] else [],
        "launches": [dict(r) for r in db.execute(
            "SELECT platform, launches, p50_seconds, p90_seconds FROM launches WHERE build_kind=? AND days=? "
            "ORDER BY launches DESC", (one, days))],
        "session_ends": [dict(r) for r in db.execute(
            f"SELECT reason, SUM(count) AS count FROM session_ends WHERE {bw} AND day>=? GROUP BY reason "
            "ORDER BY count DESC", (*ba, since))],
    }
    db.close()
    return result


def _every_day_players(since, days, by_day):
    start = datetime.strptime(since, "%Y-%m-%d")
    return [by_day.get(d, {"day": d, "players": 0, "new_players": 0})
            for d in ((start + timedelta(days=n)).strftime("%Y-%m-%d") for n in range(days))]


def ftue(agg_path, build="release", days=90):
    """The FTUE quest graph as a funnel: how many of the players who started in the range completed each node."""
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    since = _since(days)
    one = distinct_build(build)
    cohort = db.execute("SELECT SUM(players), SUM(finished_ftue) FROM funnel WHERE build_kind=? AND first_day>=?",
                        (one, since)).fetchone()
    steps = [dict(r) for r in db.execute(
        "SELECT quest_id, phase_index, step_id, MAX(step_name) AS step_name, SUM(players) AS players, "
        "ROUND(SUM(seconds_sum) / NULLIF(SUM(seconds_n), 0), 0) AS avg_seconds FROM ftue_steps "
        "WHERE build_kind=? AND first_day>=? GROUP BY quest_id, phase_index, step_id "
        "ORDER BY quest_id, phase_index, avg_seconds", (one, since))]
    for step in steps:
        step["pct_of_new"] = _pct(step["players"], cohort[0] or 0)
    db.close()
    return {"build": build, "days": days, "built_ms": None,
            "new_players": cohort[0] or 0, "finished_ftue": cohort[1] or 0,
            "finished_pct": _pct(cohort[1] or 0, cohort[0] or 0), "steps": steps}


def economy(agg_path, build="release", days=90):
    """Crystals: earned and spent per day, where they come from and go, blocked spends, unlocks, balances, and
    payouts made twice."""
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    since = _since(days)
    bw, ba = build_where(build)
    by_day = {}
    for r in db.execute(f"SELECT day, kind, SUM(amount) AS amount FROM economy WHERE {bw} AND day>=? "
                        "GROUP BY day, kind", (*ba, since)):
        by_day.setdefault(r["day"], {})[r["kind"]] = r["amount"]
    start = datetime.strptime(since, "%Y-%m-%d")
    per_day = []
    for n in range(days):
        day = (start + timedelta(days=n)).strftime("%Y-%m-%d")
        entry = by_day.get(day, {})
        per_day.append({"day": day, "earned": entry.get("earned", 0), "spent": entry.get("spent", 0) + entry.get("unlock", 0)})
    totals = {r["kind"]: dict(r) for r in db.execute(
        f"SELECT kind, SUM(events) AS events, SUM(amount) AS amount FROM economy WHERE {bw} AND day>=? "
        "GROUP BY kind", (*ba, since))}
    result = {
        "build": build, "days": days, "built_ms": _built_ms(db), "per_day": per_day, "totals": totals,
        "sources": [dict(r) for r in db.execute(
            "SELECT kind, source, SUM(events) AS events, SUM(amount) AS amount FROM economy "
            f"WHERE {bw} AND day>=? AND kind IN ('earned', 'spent', 'unlock') GROUP BY kind, source "
            "ORDER BY kind, amount DESC", (*ba, since))],
        "blocked": [dict(r) for r in db.execute(
            "SELECT source AS item, SUM(events) AS events, SUM(amount) AS short_by FROM economy "
            f"WHERE {bw} AND day>=? AND kind='blocked' GROUP BY source ORDER BY events DESC", (*ba, since))],
        "unlocks": [dict(r) for r in db.execute(
            f"SELECT kind, name, SUM(count) AS count FROM unlocks WHERE {bw} AND day>=? GROUP BY kind, name "
            "ORDER BY kind, count DESC", (*ba, since))],
        "balances": [dict(r) for r in db.execute(
            "SELECT bucket, players FROM balances WHERE build_kind=? ORDER BY bucket_order", (distinct_build(build),))],
        "double_payouts": dict(db.execute(
            "SELECT COALESCE(SUM(count), 0) AS count, COALESCE(SUM(amount), 0) AS amount FROM double_payouts "
            f"WHERE {bw} AND day>=?", (*ba, since)).fetchone()),
    }
    db.close()
    return result


SOCIAL_EVENTS = ("friend_request_sent", "friend_request_received", "friend_added", "party_invite_sent",
                 "party_invite_received", "party_joined", "share_triggered")


def social(agg_path, build="release", days=90):
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    since = _since(days)
    bw, ba = build_where(build)
    marks = ",".join("?" * len(SOCIAL_EVENTS))
    totals = {r["event"]: dict(r) for r in db.execute(
        f"SELECT event, SUM(rows) AS count, MAX(players) AS peak_daily_players FROM daily_events "
        f"WHERE {bw} AND day>=? AND event IN ({marks}) GROUP BY event", (*ba, since, *SOCIAL_EVENTS))}
    get = lambda e: (totals.get(e) or {}).get("count") or 0
    per_day = {}
    for r in db.execute(f"SELECT day, event, SUM(rows) AS rows FROM daily_events WHERE {bw} AND day>=? "
                        f"AND event IN ('party_invite_sent', 'party_joined', 'friend_added') GROUP BY day, event",
                        (*ba, since)):
        per_day.setdefault(r["day"], {"day": r["day"], "party_invite_sent": 0, "party_joined": 0, "friend_added": 0})[r["event"]] = r["rows"]
    db.close()
    return {"build": build, "days": days, "built_ms": None, "totals": {e: get(e) for e in SOCIAL_EVENTS},
            "invite_join_pct": _pct(get("party_joined"), get("party_invite_received")),
            "friend_accept_pct": _pct(get("friend_added"), get("friend_request_received")),
            "multi_human_matches": overview(agg_path, build, days)["tiles"]["multi_human_matches"] if days in WINDOWS else None,
            "per_day": sorted(per_day.values(), key=lambda r: r["day"])}


# ---------------------------------------------------------------- Rev 65: switchable charts, cohorts, play types, eras

FREESTYLE_MEASURES = ("freestyle_entries", "freestyle_players", "freestyle_minutes", "freestyle_switches")
SERIES_MEASURES = ("matches", "players", "finished_pct", "rematch_pct", "flight_min", "new_players", "events") + \
    FREESTYLE_MEASURES + ("score", "placement", "win_pct", "abandon_pct")
MATCH_BY = ("none", "play_type", "era", "platform", "country", "game_mode", "vessel_class", "build_kind", "intensity",
            "element", "setup", "ai_difficulty", "device", "os")
SERIES_BY = MATCH_BY + ("from_vessel_class", "switch_source")
SERIES_OVER = ("day", "week", "none")
# score, placement, win_pct, abandon_pct (round 67): the Balance and Quits pages' measures, from the plays table
BALANCE_MEASURES = ("score", "placement", "win_pct", "abandon_pct")
PLAY_MEASURES = ("matches", "finished_pct", "rematch_pct", "flight_min") + BALANCE_MEASURES
COUNT_MEASURES = ("matches", "players", "new_players", "events") + FREESTYLE_MEASURES
ELEMENT_ORDER = ("Charge", "Mass", "Space", "Time", "Omni", "none")
# What each measure can be split by. events come from daily_events (day, build, event, source): no player, mode or
# platform on them, so only these three splits are answerable; the console hides the rest. Freestyle (the Toybox) is
# not a match: no mode, play type or element; its vessel comes from builds after 2026-11-06.
FREESTYLE_BY = ("none", "era", "build_kind", "vessel_class")
# device: every table these measures read has one. os (the OS family and version, from builds that send os) is on the
# plays only; players are not counted by it.
SERIES_ANSWERS = {**{m: MATCH_BY for m in PLAY_MEASURES},
                  **{m: tuple(b for b in MATCH_BY if b != "os") for m in ("players", "new_players")},
                  "events": ("none", "era", "build_kind", "device"),
                  "freestyle_entries": FREESTYLE_BY + ("device",), "freestyle_players": FREESTYLE_BY,
                  "freestyle_minutes": FREESTYLE_BY + ("device",),
                  "freestyle_switches": FREESTYLE_BY + ("from_vessel_class", "switch_source", "device")}
FREESTYLE_NOTES = {
    "freestyle_entries": "entries go back to the PostHog history; an entry names its vessel only from builds after "
                         "2026-11-06 (older ones read unknown)",
    "freestyle_players": "players who entered the Toybox; by vessel: players who flew it there (named only from builds "
                         "after 2026-11-06)",
    "freestyle_minutes": "minutes exist only from builds that send freestyle_ended (after 2026-11-06); by vessel: the "
                         "vessel at the end of each stretch",
    "freestyle_switches": "event switches come from freestyle_vessel_changed (builds after 2026-11-06, exact time); "
                          "recovered ones from the identify history (builds since 2026-10-12, time approximate, several "
                          "switches between two profile updates read as one)",
}
PLAYS_KEY = {"play_type": "play_type", "setup": "COALESCE(setup, 'multi_unrecorded')",
             "platform": "COALESCE(platform, 'unknown')",
             "element": "COALESCE(element, 'none')",
             "country": "COALESCE(country, 'unknown')", "game_mode": "COALESCE(game_mode, 'unknown')",
             "vessel_class": "COALESCE(vessel_class, 'unknown')",
             "intensity": "COALESCE(CAST(intensity AS TEXT), 'unknown')", "build_kind": "build_kind",
             "ai_difficulty": "COALESCE(ai_difficulty, 'unknown')", "device": "COALESCE(device, 'other')",
             "os": "COALESCE(os_family, 'not recorded')"}
GROUP_TYPES = {"solo_vs_ai": ("solo_vs_ai",), "solo": ("solo_alone",),
               "multi": ("coop", "adversarial", "mixed", "multi_human_unknown")}
# The setups inside each Play filter group (a play's setup and its play type read the same player_count_human and
# player_count_ai, so they agree; only a build that sent ai_count without player_count_ai can differ).
SETUP_GROUP = {s[0]: s[1] for s in SETUPS}
SETUP_LABEL = {s[0]: s[2] for s in SETUPS}
PLAY_SETUPS = {"solo": ("solo_alone",), "solo_vs_ai": ("solo_vs_1_ai", "solo_vs_2_ai", "solo_vs_3plus_ai", "solo_with_ai_team"),
               "multi": tuple(s[0] for s in SETUPS if s[1] == "multiplayer")}
SETUP_NOTES = [
    "a match's setup is read from each player's side of game_started: Competitive is one human on their domain against "
    "other humans, Teams is 2+ humans on their domain against other humans, Teams vs AI is every human on one domain",
    "the multiplayer setups need the domain fields (humans_on_my_domain, humans_on_other_domains) that builds "
    "made from bleeding-edge since " + DOMAINS_FROM + " send; older multiplayer matches and the PostHog history read "
    "Teams not recorded, never guessed",
    "the solo setups count the AI from player_count_ai (ai_count when it is missing), so old matches and the PostHog "
    "history have them too; with the domain fields, vs N AI counts only the AI on other domains, and With AI teammates "
    "(ai_on_my_domain 1+) exists only from builds that send the domain fields",
    "an uneven match (2 humans against 1) is Teams from the pair's side and Competitive from the one's, so its match "
    "counts in both; plays and finished, rematch and won percentages count each player once",
]
SETUP_SERIES_NOTE = ("a match's setup is read from each player's side; multiplayer from builds that do not send the "
                     "domain fields (the PostHog history included) reads Teams not recorded, and With AI teammates "
                     "exists only from builds that do; the solo AI counts go back to the start")
DIFFICULTY_NOTE = ("the AI difficulty comes from game_completed (match_abandoned for a quit), sent by builds after "
                   "2026-11-07; older matches and the PostHog history read unknown")
BALANCE_NOTES = {
    "score": "the player's own final score, from game_completed (builds after 2026-11-07); modes score on their own "
             "scales, so compare a mode with itself",
    "placement": "1 is best, among every player in the match, AI included (builds after 2026-11-07)",
    "win_pct": "won is player_won from game_completed, of the finished matches that sent it",
    "abandon_pct": "a quit is match_abandoned, over the plays of builds that send it (from the first day a build and version sent "
                   "match_abandoned or session_started); older builds never said when a player left",
}
MAX_KEYS = 8
WEEK_SQL = "date({col}, '-6 days', 'weekday 1')"


def check_series(measure, by, over):
    if measure not in SERIES_MEASURES:
        raise BadRequest(f"measure must be one of {list(SERIES_MEASURES)}")
    if by not in SERIES_BY:
        raise BadRequest(f"by must be one of {list(SERIES_BY)}")
    if over not in SERIES_OVER:
        raise BadRequest(f"over must be one of {list(SERIES_OVER)}")
    if by not in SERIES_ANSWERS[measure]:
        raise BadRequest(f"{measure} cannot be split by {by}: the aggregates hold {measure} only by "
                         f"{', '.join(SERIES_ANSWERS[measure])}")


def _x_of(over, col="day"):
    return {"day": col, "week": WEEK_SQL.format(col=col), "none": "NULL"}[over]


def _plays_cells(db, sc, measure, by, over, era_key):
    """(x, key) -> [numerator, denominator] for one scope, from the plays table."""
    key = f"'{era_key}'" if era_key else PLAYS_KEY.get(by, "NULL")
    w, a = sc.where()
    rows = db.execute(
        f"SELECT {_x_of(over)} AS x, {key} AS key, COUNT(DISTINCT match_id), COUNT(*), SUM(completed), "
        f"SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END), SUM(flight_time_seconds), COUNT(flight_time_seconds), "
        f"SUM(score), COUNT(score), SUM(placement), COUNT(placement), SUM(won), COUNT(won), "
        f"SUM(CASE WHEN quit_tracked=1 THEN abandoned ELSE 0 END), SUM(quit_tracked) "
        f"FROM plays WHERE {w} GROUP BY x, key", a).fetchall()
    out = {}
    for (x, k, matches_n, plays_n, done, rematches, flight, flown, score, scored, place, placed, won, judged,
         quit, tracked) in rows:
        num, den = {"matches": (matches_n, None), "finished_pct": (done or 0, plays_n),
                    "rematch_pct": (rematches or 0, done or 0), "flight_min": ((flight or 0) / 60.0, flown),
                    "score": (score or 0, scored), "placement": (place or 0, placed),
                    "win_pct": (won or 0, judged), "abandon_pct": (quit or 0, tracked or 0)}[measure]
        out[(x, k)] = [num, den]
    return out


def _player_cells(db, sc, measure, by, over, era_key):
    """(x, key) -> [count, None] from player_counts. Distinct players need the span's own row (a week or a window is
    not the sum of its days); new players are additive, so they sum."""
    column = "players" if measure in ("players", "freestyle_players") else "new_players"
    dim, keys = ("none", None) if by in ("none", "era", "build_kind") else (by, None)
    if measure == "freestyle_players":
        dim, keys = ("freestyle_vessel", None) if by == "vessel_class" else ("freestyle", ["entered"])
    elif by == "none" and sc.play != "all":
        dim, keys = "play_group", [sc.play]                 # players who played that group
    elif by == "play_type" and sc.play != "all":
        keys = list(GROUP_TYPES[sc.play])
    elif by == "setup" and sc.play != "all":
        keys = list(PLAY_SETUPS[sc.play])
    if column == "players" and over == "week":
        span, x_col = sc.span_week, "x"
    elif column == "players" and over == "none":
        span, x_col = sc.span_none, "NULL"
    else:
        span, x_col = "day", _x_of(over, "x")
    marks = ",".join("?" * len(sc.pc_builds)) or "NULL"
    table, dx = sc.counted("player_counts")
    sql = (f"SELECT {x_col} AS xx, {('build_kind' if by == 'build_kind' else 'key')} AS kk, SUM({column}) "
           f"FROM {table} WHERE span=? AND source=? AND dim=? AND build_kind IN ({marks}){dx}")
    args = [span, sc.source, dim, *sc.pc_builds]
    if span == "day":
        sql += " AND x BETWEEN ? AND ?"
        args += [sc.since, sc.until]
    elif span.startswith("era_week:") or span == "week":
        sql += " AND x BETWEEN ? AND ?"
        args += [_week(sc.since), sc.until]
    if keys:
        sql += f" AND key IN ({','.join('?' * len(keys))})"
        args += keys
    out = {}
    for x, k, n in db.execute(sql + " GROUP BY xx, kk", args):
        out[(x, era_key or (k if by != "none" else None))] = [n or 0, None]
    return out


def _freestyle_cells(db, sc, measure, by, over, era_key):
    """(x, key) -> [count, None] for the Toybox measures, from freestyle_vessels / freestyle_switches."""
    w, a = sc.where(play=False)
    if measure == "freestyle_switches":
        table, value = "freestyle_switches", "switches"
        keys = {"vessel_class": "vessel_class", "from_vessel_class": "from_vessel_class", "switch_source": "origin",
                "device": "device"}
    else:
        table = "freestyle_vessels"
        value = {"freestyle_entries": "entries", "freestyle_minutes": "seconds / 60.0"}[measure]
        keys = {"vessel_class": "vessel_class", "device": "device"}
    key = f"'{era_key}'" if era_key else ("build_kind" if by == "build_kind" else keys.get(by, "NULL"))
    return {(x, k): [n or 0, None] for x, k, n in db.execute(
        f"SELECT {_x_of(over)} AS x, {key} AS key, SUM({value}) FROM {table} WHERE {w} GROUP BY x, key", a)}


def _event_cells(db, sc, over, by, era_key):
    w, a = sc.where(play=False)
    key = f"'{era_key}'" if era_key else {"build_kind": "build_kind", "device": "device"}.get(by, "NULL")
    return {(x, k): [n or 0, None] for x, k, n in db.execute(
        f"SELECT {_x_of(over)} AS x, {key} AS key, SUM(rows) FROM daily_events WHERE {w} GROUP BY x, key", a)}


def _week(day):
    d = datetime.strptime(day, "%Y-%m-%d")
    return (d - timedelta(days=d.weekday())).strftime("%Y-%m-%d")


def _value(measure, num, den):
    if measure == "freestyle_minutes":
        return round(num, 1)
    if measure in COUNT_MEASURES:
        return num
    if not den:
        return None
    if measure == "placement":
        return round(num / den, 2)
    return round(num / den, 1) if measure in ("flight_min", "score") else round(100.0 * num / den, 1)


def series(agg_path, measure="matches", by="none", over="day", build="release", days=90, play="all", era=None,
           eras=(), device="all"):
    """One switchable chart (Rev 65): a measure over time (day / week / none), split by one dimension. Points carry
    x (a day, a week's Monday or null) and key (a split value or null); keys are the split's top 8 and "other"."""
    check_series(measure, by, over)
    sc = Scope(build, days, play, era, BUILD_KINDS if by == "build_kind" else None, device)
    db = _open(agg_path)
    base = {"measure": measure, "by": by, "over": over, "build": build, "days": days, "play": play, **sc.describe(),
            # the splits this measure can take, so the console offers only those (no 400 to find out)
            "answerable_by": list(SERIES_ANSWERS[measure])}
    if db is None:
        return {**base, "points": [], "keys": [], "unfiltered": [], "empty": True}
    if by == "era":
        parts = [(e["id"], Scope(build, days, play, e, BUILD_KINDS if by == "build_kind" else None, device))
                 for e in ([era] if era else eras)]
    else:
        parts = [(None, sc)]
    cells = {}
    for era_key, scope in parts:
        if measure in PLAY_MEASURES:
            got = _plays_cells(db, scope, measure, by, over, era_key)
        elif measure in ("freestyle_entries", "freestyle_minutes", "freestyle_switches"):
            got = _freestyle_cells(db, scope, measure, by, over, era_key)
        elif measure == "events":
            got = _event_cells(db, scope, over, by, era_key)
        else:
            got = _player_cells(db, scope, measure, by, over, era_key)
        for k, v in got.items():
            cell = cells.setdefault(k, [0, 0 if v[1] is not None else None])
            cell[0] += v[0]
            if v[1] is not None:
                cell[1] = (cell[1] or 0) + v[1]
    db.close()
    # order the split keys: eras as listed, intensity and play type in their own order, the rest by volume
    totals = {}
    for (x, k), (num, den) in cells.items():
        totals[k] = totals.get(k, 0) + (den if den is not None and measure not in COUNT_MEASURES else num)
    if by == "none":
        keys = [None]
    elif by == "era":
        keys = [k for k, _ in parts]
    elif by == "play_type":
        keys = sorted(totals, key=lambda k: PLAY_TYPES.index(k) if k in PLAY_TYPES else 99)
    elif by == "intensity":
        keys = sorted(totals, key=lambda k: (not str(k).isdigit(), int(k) if str(k).isdigit() else 0, str(k)))
    elif by == "element":
        keys = sorted(totals, key=lambda k: (ELEMENT_ORDER.index(k) if k in ELEMENT_ORDER else 99, str(k)))
    elif by == "setup":
        keys = sorted(totals, key=lambda k: (SETUP_IDS.index(k) if k in SETUP_IDS else 99, str(k)))
    elif by == "ai_difficulty":
        keys = sorted(totals, key=difficulty_rank)
    else:
        keys = sorted(totals, key=lambda k: (-totals[k], str(k)))
    if measure in COUNT_MEASURES and by not in ("none", "era"):
        keys = [k for k in keys if totals.get(k)]          # a vessel with no entries is not a part of the entries
    shown = keys if len(keys) <= MAX_KEYS + 1 else keys[:MAX_KEYS]
    folded = {}
    for (x, k), (num, den) in cells.items():
        target = k if k in shown else "other"
        cell = folded.setdefault((x, target), [0, None if den is None else 0])
        cell[0] += num
        if den is not None:
            cell[1] += den
    out_keys = shown + (["other"] if len(shown) < len(keys) else [])
    if over == "day":
        xs = _days_between(sc.since, sc.until) if by != "era" else sorted({x for x, _ in folded})
    elif over == "week":
        xs = sorted({_week(d) for d in _days_between(sc.since, sc.until)}) if by != "era" else sorted({x for x, _ in folded})
    else:
        xs = [None]
    points = []
    for x in xs:
        for k in out_keys:
            cell = folded.get((x, k))
            if cell is None:
                if by == "era":
                    continue                      # an era has no points outside its own range
                points.append({"x": x, "key": k, "value": 0 if measure in COUNT_MEASURES else None})
            else:
                points.append({"x": x, "key": k, "value": _value(measure, *cell)})
    secondary = None
    if by == "element" and measure in PLAY_MEASURES:
        secondary = _secondary_elements(agg_path, sc, parts)
    unfiltered = []
    if play != "all" and (measure in ("events",) + FREESTYLE_MEASURES or (measure in ("players", "new_players")
                                                                          and by not in ("none", "play_type", "setup"))):
        unfiltered.append("play")
    if by == "build_kind":
        unfiltered.append("build")
    if by == "era" and not era:
        unfiltered.append("days")
    notes = []
    if measure in ("players", "new_players") and by not in ("none", "era", "build_kind"):
        notes.append("a player with two values that day (two platforms, two modes) counts under both")
    if "other" in out_keys and measure in ("players", "freestyle_players"):
        notes.append("other adds up the players of the smaller keys, so a player in two of them counts twice")
    if by == "element":
        notes.append("a match's element is the one its build sent (from 2026-11-06), else its mode's genre from the "
                     "game's ModeGenre (mode_elements.json), so older matches and the PostHog history have one too; a "
                     "two-element mode counts under its first (Brood Rush: Mass, and Space as its second); a "
                     "mode with no genre or an old mode name reads No element (key none)")
    if by == "setup":
        notes.append(SETUP_SERIES_NOTE)
    if by == "ai_difficulty":
        notes.append(DIFFICULTY_NOTE)
    if by in ("device", "os") or device != "all":
        notes.append(DEVICE_NOTE if by != "os" else
                     "the OS family and version come from builds that send os (from 2026-11-07); older matches read "
                     "not recorded")
    if measure in BALANCE_NOTES:
        notes.append(BALANCE_NOTES[measure])
    if measure in FREESTYLE_NOTES:
        notes.append(FREESTYLE_NOTES[measure])
    out = {**base, "points": points, "keys": out_keys, "unfiltered": unfiltered, "notes": notes}
    if secondary is not None:
        out["secondary"] = secondary
    return out


def _secondary_elements(agg_path, sc, parts):
    """Matches per second element (two-element modes), for an element split: listed beside it, never added in."""
    db = _open(agg_path)
    found = {}
    for _era_key, scope in parts:
        w, a = scope.where()
        for k, n in db.execute("SELECT element_secondary, COUNT(DISTINCT match_id) FROM plays WHERE "
                               "element_secondary IS NOT NULL AND " + w + " GROUP BY element_secondary", a):
            found[k] = found.get(k, 0) + n
    db.close()
    return [{"key": k, "value": n} for k, n in sorted(found.items(), key=lambda kv: -kv[1])]


def cohorts(agg_path, build="release", days=90, play="all", era=None, device="all"):
    """Weekly cohorts (Rev 65): players by the week of their first day, the share active each week after, and the
    lifespan tiles. Player lifespans themselves come from the raw store (profile readers only)."""
    sc = Scope(build, days, play, era, device=device)
    db = _open(agg_path)
    if db is None:
        return {**_empty(build, days), "weekly": [], "tiles": {}}
    group = play if play != "all" else "all"
    marks = ",".join("?" * len(sc.distinct_builds)) or "NULL"
    weeks_table, dx = sc.counted("cohort_weeks")
    days_table, _dx = sc.counted("cohort_days")
    first_week = _week(sc.since)
    sizes, active = {}, {}
    for cohort, k, n in db.execute(
            f"SELECT cohort, week_index, SUM(players) FROM {weeks_table} WHERE build_kind IN ({marks}) AND source=? "
            f"AND play_group=? AND cohort BETWEEN ? AND ?{dx} GROUP BY cohort, week_index",
            (*sc.distinct_builds, sc.source, group, first_week, sc.until)):
        active.setdefault(cohort, {})[k] = n
        if k == 0:
            sizes[cohort] = n
    today = _today()
    this_week = _week(today)
    span = max(((datetime.strptime(this_week, "%Y-%m-%d") - datetime.strptime(c, "%Y-%m-%d")).days // 7 + 1
                for c in sizes), default=0)
    span = min(span, 53)
    weekly = []
    for cohort in sorted(sizes):
        start = datetime.strptime(cohort, "%Y-%m-%d")
        row = []
        for k in range(span):
            if (start + timedelta(days=7 * k)).strftime("%Y-%m-%d") > today:
                row.append(None)
            else:
                row.append(_pct(active[cohort].get(k, 0), sizes[cohort]))
        weekly.append({"cohort": cohort, "players": sizes[cohort], "weeks": row})
    hist, players, d1, d1_base, d7, d7_base, lapsed = {}, 0, 0, 0, 0, 0, 0
    yesterday = (datetime.now(timezone.utc) - timedelta(days=1)).strftime("%Y-%m-%d")
    week_ago = (datetime.now(timezone.utc) - timedelta(days=7)).strftime("%Y-%m-%d")
    for first_day, life, n, r1, r7, gone in db.execute(
            f"SELECT first_day, lifespan_days, players, returned_d1, returned_d7, lapsed_14d FROM {days_table} "
            f"WHERE build_kind IN ({marks}) AND source=? AND play_group=? AND first_day BETWEEN ? AND ?{dx}",
            (*sc.distinct_builds, sc.source, group, sc.since, sc.until)):
        hist[life] = hist.get(life, 0) + n
        players += n
        lapsed += gone
        if first_day <= yesterday:
            d1, d1_base = d1 + r1, d1_base + n
        if first_day <= week_ago:
            d7, d7_base = d7 + r7, d7_base + n
    median = None
    if players:
        seen, half = 0, (players + 1) / 2
        for life in sorted(hist):
            seen += hist[life]
            if seen >= half:
                median = life
                break
    db.close()
    return {"build": build, "days": days, "play": play, **sc.describe(), "unfiltered": [],
            "weekly": weekly,
            "tiles": {"median_lifespan_days": median, "d1_pct": _pct(d1, d1_base), "d7_pct": _pct(d7, d7_base),
                      "lapsed_14d": lapsed, "players": players},
            "definitions": {"cohort": "the week (Monday) of a player's first day in this build and source",
                            "weeks": "share of the cohort active in week k after its first week (k = 0 is that "
                                     "week, so 100); null where the week has not begun",
                            "lifespan_days": "first day to last day, both counted (one day = 1)",
                            "play": "a play filter keeps players who played at least one match of that group"}}


FUNNEL_NOTE = ("Party invites sent: party_invite_sent events. Accepted: party_joined events. Started a match together: "
               "matches with 2+ humans. Finished together: those where 2+ of the humans finished. Played again "
               "together: those after which 2+ of its humans started another match within 10 minutes (not "
               "necessarily the same one).")


def playtypes(agg_path, build="release", days=90, era=None, device="all"):
    """Play types page (Rev 65): solo vs AI, solo and multiplayer side by side, and the party funnel."""
    sc = Scope(build, days, "all", era, device=device)
    db = _open(agg_path)
    if db is None:
        return {**_empty(build, days), "groups": {}, "share": {}, "funnel": []}
    groups = {}
    for group in GROUP_TYPES:
        gs = Scope(build, days, group, era, device=device)
        w, a = gs.where()
        row = db.execute(
            "SELECT COUNT(DISTINCT match_id), COUNT(*), SUM(completed), SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END), "
            "AVG(flight_time_seconds) FROM plays WHERE " + w, a).fetchone()
        rw, ra = gs.where(day="first_day", distinct=True)
        ret = db.execute("SELECT SUM(players), SUM(returned_d1), SUM(returned_d7) FROM returns WHERE " + rw,
                         ra).fetchone()
        per_day = {d: n for d, n in db.execute(
            "SELECT day, COUNT(DISTINCT match_id) FROM plays WHERE " + w + " GROUP BY day", a)}
        groups[group] = {
            "matches": row[0] or 0,
            "per_day": [{"day": d, "matches": per_day.get(d, 0)} for d in _days_between(gs.since, gs.until)],
            "finished_pct": _pct(row[2] or 0, row[1] or 0), "rematch_pct": _pct(row[3] or 0, row[2] or 0),
            "d1_pct": _pct(ret[1] or 0, ret[0] or 0), "d7_pct": _pct(ret[2] or 0, ret[0] or 0),
            "avg_flight_min": round(row[4] / 60.0, 1) if row[4] is not None else None,
            "top_modes": [{"game_mode": m, "matches": n} for m, n in db.execute(
                "SELECT COALESCE(game_mode, 'unknown'), COUNT(DISTINCT match_id) AS n FROM plays WHERE " + w +
                " GROUP BY 1 ORDER BY n DESC LIMIT 5", a)]}
    wd, ad = sc.where(play=False)
    events = lambda name: db.execute("SELECT COALESCE(SUM(rows), 0) FROM daily_events WHERE event=? AND " + wd,
                                     (name, *ad)).fetchone()[0]
    w, a = sc.where()
    together = db.execute(
        "SELECT COUNT(*), SUM(CASE WHEN done >= 2 THEN 1 ELSE 0 END), SUM(CASE WHEN again >= 2 THEN 1 ELSE 0 END) "
        "FROM (SELECT match_id, SUM(completed) AS done, SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END) AS again "
        "FROM plays WHERE humans >= 2 AND " + w + " GROUP BY match_id)", a).fetchone()
    funnel = [{"step": "Party invites sent", "count": events("party_invite_sent")},
              {"step": "Accepted", "count": events("party_joined")},
              {"step": "Started a match together", "count": together[0] or 0},
              {"step": "Finished together", "count": together[1] or 0},
              {"step": "Played again together", "count": together[2] or 0}]
    db.close()
    return {"build": build, "days": days, **sc.describe(),
            "unfiltered": [],
            "groups": groups, "share": {g: groups[g]["matches"] for g in groups},
            "funnel": funnel, "funnel_note": FUNNEL_NOTE}


def _era_scope(era, build, device="all"):
    """An era's scope for the eras pages: one build, or every build (players then from '*', each player once)."""
    return Scope(build or "release", 90, "all", era, None if build else BUILD_KINDS, device)


def eras_page(agg_path, eras, milestones, build=None, device="all"):
    """Eras page (Rev 65): each era with its events, players and matches; events per week coloured by era (an event
    belongs to the first era, in list order, whose range, source and builds hold it); the milestones."""
    db = _open(agg_path)
    out = []
    for era in eras:
        sc = _era_scope(era, build, device)
        counts = {"events": 0, "players": 0, "matches": 0}
        if db is not None and sc.since <= sc.until:
            wd, ad = sc.where(play=False)
            w, a = sc.where()
            counts = {"events": db.execute("SELECT COALESCE(SUM(rows), 0) FROM daily_events WHERE " + wd,
                                           ad).fetchone()[0],
                      "players": _player_count(db, sc, builds=[build] if build else ["*"]),
                      "matches": db.execute("SELECT COUNT(DISTINCT match_id) FROM plays WHERE " + w, a).fetchone()[0]}
        out.append({**era, **counts})
    weekly = {}
    if db is not None:
        where, args = ("build_kind = ?", [build]) if build else ("1", [])
        where += _device(device)
        for day, imported, n in db.execute(
                f"SELECT day, imported, SUM(rows) FROM daily_events WHERE {where} GROUP BY day, imported", args):
            src = "imported" if imported else "live"
            owner = next((e["id"] for e in eras if e["from"] <= day <= e["to"] and e["source"] in ("any", src)
                          and (not build or not e["builds"] or build in e["builds"])), None)
            key = (_week(day), owner)
            weekly[key] = weekly.get(key, 0) + n
        db.close()
    return {"build": build, "device": device, "eras": out, "milestones": milestones,
            "weekly": [{"week": w, "era": e, "events": n} for (w, e), n in
                       sorted(weekly.items(), key=lambda kv: (kv[0][0], str(kv[0][1])))],
            "notes": ["an era's days run from its from to its to (today at most); imported = PostHog history, "
                      "live = sent to this server; era null in weekly = events in no era"]}


def eras_compare(agg_path, a, b, build=None, device="all"):
    db = _open(agg_path)

    def one(era):
        sc = _era_scope(era, build, device)
        if db is None or sc.since > sc.until:
            return {"id": era["id"], "name": era["name"], "players": 0, "new_players": 0, "matches": 0,
                    "editor_share_pct": None, "finished_pct": None, "rematch_pct": None, "d1_pct": None,
                    "rows_per_play": None}
        who = [build] if build else ["*"]
        w, args = sc.where()
        row = db.execute("SELECT COUNT(DISTINCT match_id), COUNT(*), SUM(completed), "
                         "SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END) FROM plays WHERE " + w, args).fetchone()
        every = sc.with_builds(BUILD_KINDS)
        we, ae = every.where()
        split = dict(db.execute("SELECT build_kind, COUNT(DISTINCT match_id) FROM plays WHERE " + we +
                                " GROUP BY build_kind", ae).fetchall())
        rw, ra = sc.where(day="first_day", builds=[build] if build else ["*"] if not era.get("builds") else None)
        ret = db.execute("SELECT SUM(players), SUM(returned_d1) FROM returns WHERE " + rw, ra).fetchone()
        wd, ad = sc.where(play=False)
        rpp = db.execute("SELECT SUM(rows), SUM(plays) FROM daily_plays WHERE " + wd, ad).fetchone()
        table, dx = sc.counted("player_counts")
        new = db.execute(f"SELECT COALESCE(SUM(new_players), 0) FROM {table} WHERE span=? AND source=? AND "
                         f"dim='none' AND build_kind=?{dx}", (sc.span_none, sc.source, who[0])).fetchone()[0]
        return {"id": era["id"], "name": era["name"], **sc.describe(),
                "players": _player_count(db, sc, builds=who), "new_players": new, "matches": row[0] or 0,
                "editor_share_pct": _pct(split.get("editor", 0), sum(split.values())),
                "finished_pct": _pct(row[2] or 0, row[1] or 0), "rematch_pct": _pct(row[3] or 0, row[2] or 0),
                "d1_pct": _pct(ret[1] or 0, ret[0] or 0), "rows_per_play": round(rpp[0] / rpp[1], 2) if rpp[1] else None}
    result = {"build": build, "device": device, "a": one(a), "b": one(b),
              "notes": ["editor_share_pct counts every build; the rest count the build asked for (none = every build)"]}
    if db is not None:
        db.close()
    return result


def builtin_tile(agg_path, name, build="release", days=90, play="all", era=None, device="all"):
    """A Dashboards built-in tile as {columns, rows}."""
    sc = Scope(build, days, play, era, device=device)
    db = _open(agg_path)
    if db is None:
        return {"columns": [], "rows": [], "unfiltered": [], "empty": True}
    marks = ",".join("?" * len(sc.distinct_builds)) or "NULL"
    pmarks = ",".join("?" * len(sc.pc_builds)) or "NULL"
    unfiltered = []
    if name == "new_players":
        table, dx = sc.counted("player_counts")
        got = dict(db.execute(f"SELECT x, SUM(new_players) FROM {table} WHERE span='day' AND source=? AND "
                              f"dim='none' AND build_kind IN ({pmarks}) AND x BETWEEN ? AND ?{dx} GROUP BY x",
                              (sc.source, *sc.pc_builds, sc.since, sc.until)).fetchall())
        columns, rows = ["day", "new_players"], [[d, got.get(d, 0)] for d in _days_between(sc.since, sc.until)]
        unfiltered = sc.unfiltered(["new_players"])
    elif name == "d1_return":
        rw, ra = sc.where(day="first_day", distinct=True)
        columns = ["day", "players", "d1_pct"]
        rows = [[d, n, _pct(r1 or 0, n)] for d, n, r1 in db.execute(
            "SELECT first_day, SUM(players), SUM(returned_d1) FROM returns WHERE " + rw +
            " GROUP BY first_day ORDER BY first_day", ra)]
    elif name == "ftue_finished":
        columns = ["day", "new_players", "finished_ftue", "finished_pct"]
        rows = [[d, n, f, _pct(f or 0, n)] for d, n, f in db.execute(
            f"SELECT first_day, SUM(players), SUM(finished_ftue) FROM funnel WHERE build_kind IN ({marks}) AND "
            f"first_day BETWEEN ? AND ? GROUP BY first_day ORDER BY first_day", (*sc.distinct_builds, sc.since, sc.until))]
        unfiltered = sc.unfiltered(["ftue_finished"], ["ftue_finished"], (), ["ftue_finished"])
    elif name == "rows_per_play":
        wd, ad = sc.where(play=False)
        columns = ["day", "rows_per_play"]
        rows = [[d, round(r / p, 2) if p else None] for d, r, p in db.execute(
            "SELECT day, SUM(rows), SUM(plays) FROM daily_plays WHERE " + wd + " GROUP BY day ORDER BY day", ad)]
        unfiltered = sc.unfiltered(["rows_per_play"])
    elif name == "matches_per_day":
        w, a = sc.where()
        got = dict(db.execute("SELECT day, COUNT(DISTINCT match_id) FROM plays WHERE " + w + " GROUP BY day",
                              a).fetchall())
        columns, rows = ["day", "matches"], [[d, got.get(d, 0)] for d in _days_between(sc.since, sc.until)]
    elif name == "freestyle_entries":
        wd, ad = sc.where(play=False)
        got = dict(db.execute("SELECT day, SUM(entries) FROM freestyle_vessels WHERE " + wd + " GROUP BY day",
                              ad).fetchall())
        columns, rows = ["day", "freestyle_entries"], [[d, got.get(d, 0)] for d in _days_between(sc.since, sc.until)]
        unfiltered = sc.unfiltered(["freestyle_entries"])
    elif name == "play_mix":
        w, a = sc.where()
        columns = ["play_type", "matches"]
        rows = [list(r) for r in db.execute("SELECT play_type, COUNT(DISTINCT match_id) AS n FROM plays WHERE " + w +
                                            " GROUP BY play_type ORDER BY n DESC", a)]
    else:
        db.close()
        raise BadRequest(f"no built-in tile called {name}")
    db.close()
    return {"columns": columns, "rows": rows, **sc.describe(), "unfiltered": unfiltered}


def freestyle(agg_path, build="release", days=90, play="all", era=None, device="all"):
    """Freestyle (the Toybox), Shombith 2026-11-06: who went in and flew vessels there. Entries go back to the PostHog
    history; minutes and stretches come from freestyle_ended, exact switches from freestyle_vessel_changed (builds
    after 2026-11-06), recovered switches from the identify history (builds since 2026-10-12, approximate time)."""
    sc = Scope(build, days, "all", era, device=device)
    db = _open(agg_path)
    base = {"build": build, "days": days, "play": play, **sc.describe(), "unfiltered": ["play"] if play != "all" else []}
    if db is None:
        return {**base, "empty": True, "tiles": {}, "per_day": [], "vessels": [], "switch_pairs": [], "history": {}}
    w, a = sc.where(play=False)
    t = db.execute("SELECT COALESCE(SUM(entries), 0), COALESCE(SUM(first_entries), 0), COALESCE(SUM(stretches), 0), "
                   "COALESCE(SUM(seconds), 0), COALESCE(SUM(vessels_flown), 0) FROM freestyle_vessels WHERE " + w,
                   a).fetchone()
    sw = dict(db.execute("SELECT origin, SUM(switches) FROM freestyle_switches WHERE " + w + " GROUP BY origin",
                         a).fetchall())
    per = {}
    for d, entries, sec in db.execute("SELECT day, SUM(entries), SUM(seconds) FROM freestyle_vessels WHERE " + w +
                                      " GROUP BY day", a):
        per[d] = {"day": d, "entries": entries or 0, "minutes": round((sec or 0) / 60.0, 1), "switches": 0,
                  "recovered_switches": 0}
    for d, origin, n in db.execute("SELECT day, origin, SUM(switches) FROM freestyle_switches WHERE " + w +
                                   " GROUP BY day, origin", a):
        row = per.setdefault(d, {"day": d, "entries": 0, "minutes": 0.0, "switches": 0, "recovered_switches": 0})
        row["recovered_switches" if origin == "recovered" else "switches"] += n or 0
    pc_table, dx = sc.counted("player_counts")
    players_by_day = {x: n for x, n in db.execute(
        f"SELECT x, SUM(players) FROM {pc_table} WHERE span='day' AND source=? AND dim='freestyle' AND key='entered' "
        f"AND build_kind IN ({','.join('?' * len(sc.pc_builds)) or 'NULL'}) AND x BETWEEN ? AND ?{dx} GROUP BY x",
        (sc.source, *sc.pc_builds, sc.since, sc.until))}
    per_day = []
    for d in _days_between(sc.since, sc.until):
        row = per.get(d, {"day": d, "entries": 0, "minutes": 0.0, "switches": 0, "recovered_switches": 0})
        per_day.append({**row, "players": players_by_day.get(d, 0)})
    vessels = [dict(r) for r in db.execute(
        "SELECT vessel_class, SUM(entries) AS entries, SUM(stretches) AS stretches, ROUND(SUM(seconds) / 60.0, 1) "
        "AS minutes, SUM(switches_to) AS switches_to, SUM(switches_from) AS switches_from, SUM(recovered_to) AS "
        "recovered_to, SUM(recovered_from) AS recovered_from FROM freestyle_vessels WHERE " + w +
        " GROUP BY vessel_class ORDER BY SUM(switches_to) + SUM(recovered_to) + SUM(stretches) DESC, entries DESC", a)]
    pairs = [dict(r) for r in db.execute(
        "SELECT from_vessel_class AS from_vessel, vessel_class AS to_vessel, SUM(switches) AS switches, "
        "SUM(CASE WHEN origin = 'recovered' THEN switches ELSE 0 END) AS recovered FROM freestyle_switches WHERE " + w +
        " GROUP BY 1, 2 ORDER BY switches DESC, 1, 2 LIMIT 20", a)]
    hw, ha = build_where(build)                                          # history: all time, this build
    hw += _device(device)
    first = lambda col, table="freestyle_vessels", extra="": db.execute(
        f"SELECT MIN(day) FROM {table} WHERE {hw} AND {col} > 0 {extra}", ha).fetchone()[0]
    history = {"first_entry_day": first("entries"),
               "first_imported_entry_day": first("entries", extra="AND imported = 1"),
               "last_imported_entry_day": db.execute(f"SELECT MAX(day) FROM freestyle_vessels WHERE {hw} AND entries > 0 "
                                                     f"AND imported = 1", ha).fetchone()[0],
               "first_minutes_day": first("stretches"),
               "first_vessel_entry_day": first("entries", extra="AND vessel_class <> 'unknown'"),
               "first_switch_day": first("switches", "freestyle_switches", "AND origin = 'event'"),
               "first_recovered_switch_day": first("switches", "freestyle_switches", "AND origin = 'recovered'")}
    players = _player_count(db, sc, "freestyle", "entered")
    db.close()
    minutes = round((t[3] or 0) / 60.0, 1)
    return {**base, "built_ms": None,
            "tiles": {"entries": t[0], "first_entries": t[1], "players": players, "stretches": t[2], "minutes": minutes,
                      "avg_stretch_min": round(minutes / t[2], 1) if t[2] else None,
                      "avg_vessels_flown": round(t[4] / t[2], 2) if t[2] else None,
                      "switches": sw.get("event", 0) or 0, "recovered_switches": sw.get("recovered", 0) or 0},
            "per_day": per_day, "vessels": vessels, "switch_pairs": pairs, "history": history,
            "notes": [FREESTYLE_NOTES["freestyle_entries"], FREESTYLE_NOTES["freestyle_minutes"],
                      FREESTYLE_NOTES["freestyle_switches"]]}


def preset_tile(agg_path, preset, era=None, eras=()):
    """A dashboard tile from a saved view: the preset's own series (its measure, split and over, and its own build,
    days, play and era), as {columns, rows} and the raw `series` the console draws."""
    settings, filters = preset["settings"], preset["filters"]
    if not preset.get("series") or settings.get("measure") not in SERIES_MEASURES:
        raise BadRequest("this saved view is not a series chart, so it cannot be a tile")
    out = series(agg_path, settings["measure"], settings.get("by") or "none", settings.get("over") or "day",
                 filters.get("build", "release"), filters.get("days", 90), filters.get("play", "all"), era, eras,
                 filters.get("device", "all"))
    if out["by"] == "none":
        columns, rows = ["x", out["measure"]], [[p["x"], p["value"]] for p in out["points"]]
    else:
        columns, rows = ["x", out["by"], out["measure"]], [[p["x"], p["key"], p["value"]] for p in out["points"]]
    return {"columns": columns, "rows": rows, "series": out, "preset": {"id": preset["id"], "name": preset["name"]},
            "form": settings.get("form"), "unfiltered": ["build", "days", "play", "era", "device"]}


def schema(agg_path, saved_tables=()):
    """Query Studio's table list: every table in aggregates.db with its columns and row count."""
    db = _open(agg_path)
    if db is None:
        return {"tables": [], "empty": True}
    tables = []
    for (name,) in db.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name").fetchall():
        quoted = '"' + name.replace('"', '""') + '"'
        columns = [r[1] for r in db.execute(f"PRAGMA table_info({quoted})")]
        tables.append({"name": name, "columns": columns, "saved": name.startswith("t_") and name in saved_tables,
                       "rows": db.execute(f"SELECT COUNT(*) FROM {quoted}").fetchone()[0]})
    db.close()
    return {"tables": tables}


# ---------------------------------------------------------------- round 67: sessions, balance, quits, invites, errors
# Shombith, 2026-11-07: "1 sessions/DAU-WAU-MAU, 2 balance: score/placement/AI difficulty, 3 mid-match quits/frustration",
# invite attribution, and the client_exception known gap. Every function reads aggregates.db only.

SESSION_BUCKETS = ((0, 60, "under 1 min"), (60, 300, "1-5 min"), (300, 900, "5-15 min"), (900, 1800, "15-30 min"),
                   (1800, 3600, "30-60 min"), (3600, 10 ** 9, "60+ min"))
ELAPSED_BUCKETS = ((0, 30, "under 30 s"), (30, 60, "30-60 s"), (60, 120, "1-2 min"), (120, 300, "2-5 min"),
                   (300, 600, "5-10 min"), (600, 10 ** 9, "10+ min"))
SESSION_NOTES = [
    "DAU, WAU and MAU count players with any event that day, the 7 days to it and the 30 days to it, so they go back "
    "to the PostHog history; DAU on the tiles is the average day of the range (today, still running, left out)",
    "stickiness is DAU over MAU, averaged over the range's last 30 full days",
    "sessions, their length and sessions per player come only from session_started and session_ended, which builds "
    "after 2026-11-07 send; a session is one app run, and a return after 30+ minutes in the background starts another",
]
QUIT_NOTES = [
    "a quit is match_abandoned: the player left a running match (to the menu, closing the app, or a disconnect); builds "
    "after 2026-11-07 send it, so the rate counts only the plays of builds and versions that do",
    "the frustration chain follows each player-day with a repeated_game_fail: a quit within 1 hour after it, and then "
    "no event at all for the 7 days after the quit (counted once those 7 days have passed)",
]
INVITE_NOTES = [
    "builds after 2026-11-07 carry an invite id on the sent, received and joined events, so an invite is followed "
    "exactly; older invites are matched by time (received within 10 minutes naming the sender, joined within 30)",
    "played together: inviter and invitee started the same match within 24 hours of the join; day 7: the invitee was "
    "seen on any of the 7 days after the join, for joins at least 7 days old",
    "inviters are counted from their first invite that someone joined, invitees from their first join; everyone is "
    "the first-match cohorts of the same range",
]
ERROR_NOTES = [
    "a client error (client_exception) carries no message text, only a short hash of it with the digits taken out, the exception type, "
    "the first project frame and the scene; builds after 2026-11-07 send it, at most 20 a session, each error once",
    "occurrences fold in the repeats the game counted; players affected counts each player once in the range",
]


def _range_days(sc):
    return _days_between(sc.since, sc.until)


def _active(db, sc):
    """DAU / WAU / MAU per day from active_rolling, and the tiles."""
    marks = ",".join("?" * len(sc.distinct_builds)) or "NULL"
    table, dx = sc.counted("active_rolling")
    got = {r[0]: r[1:] for r in db.execute(
        f"SELECT day, SUM(dau), SUM(wau), SUM(mau) FROM {table} WHERE build_kind IN ({marks}) AND source=? "
        f"AND day BETWEEN ? AND ?{dx} GROUP BY day", (*sc.distinct_builds, sc.source, sc.since, sc.until))}
    per_day = []
    for d in _range_days(sc):
        dau, wau, mau = got.get(d, (0, 0, 0))
        per_day.append({"day": d, "dau": dau or 0, "wau": wau or 0, "mau": mau or 0,
                        "stickiness_pct": _pct(dau or 0, mau or 0)})
    full = [r for r in per_day if r["day"] < _today()] or per_day
    last = per_day[-1] if per_day else {"wau": 0, "mau": 0}
    recent = [r for r in full[-30:] if r["mau"]]
    tiles = {"dau": round(sum(r["dau"] for r in full) / len(full), 1) if full else 0,
             "wau": last["wau"], "mau": last["mau"],
             "stickiness_pct": round(sum(100.0 * r["dau"] / r["mau"] for r in recent) / len(recent), 1) if recent else None}
    return tiles, per_day


def _session_tiles(db, sc):
    w, a = sc.where(play=False)
    seconds = [r[0] for r in db.execute("SELECT seconds FROM sessions WHERE seconds IS NOT NULL AND " + w, a)]
    n = db.execute("SELECT COUNT(*) FROM sessions WHERE " + w, a).fetchone()[0]
    table, dx = sc.counted("session_players")
    pw, pa = sc.where(play=False, device=False, distinct=True)
    player_days = db.execute(f"SELECT COALESCE(SUM(players), 0) FROM {table} WHERE {pw}{dx}", pa).fetchone()[0]
    med, p90 = _percentile(seconds, 0.5), _percentile(seconds, 0.9)
    return {"sessions": n, "ended": len(seconds),
            "sessions_per_player_day": round(n / player_days, 2) if player_days else None,
            "median_session_min": round(med / 60.0, 1) if med is not None else None,
            "p90_session_min": round(p90 / 60.0, 1) if p90 is not None else None}, seconds


def _percentile(values, q):
    values = sorted(values)
    if not values:
        return None
    k = (len(values) - 1) * q
    lo, hi = int(k), min(int(k) + 1, len(values) - 1)
    return values[lo] + (values[hi] - values[lo]) * (k - lo)


def sessions(agg_path, build="release", days=90, play="all", era=None, device="all"):
    """Sessions page: DAU, WAU, MAU and stickiness per day (any event, so the PostHog history too), and from builds
    that send session_started / session_ended: sessions, their length, sessions per player per day."""
    sc = Scope(build, days, "all", era, device=device)
    db = _open(agg_path)
    base = {"build": build, "days": days, "play": play, **sc.describe(), "unfiltered": ["page"] if play != "all" else [],
            "notes": SESSION_NOTES}
    if db is None:
        return {**base, "empty": True}
    active, per_day = _active(db, sc)
    stiles, seconds = _session_tiles(db, sc)
    w, a = sc.where(play=False)
    by_day = {}
    for d, kind, n in db.execute("SELECT day, launch_kind, COUNT(*) FROM sessions WHERE " + w + " GROUP BY 1, 2", a):
        by_day.setdefault(d, {})[kind] = n
    sessions_per_day = [{"day": d, "cold": by_day.get(d, {}).get("cold", 0), "resume": by_day.get(d, {}).get("resume", 0),
                         "unknown": by_day.get(d, {}).get("unknown", 0)} for d in _range_days(sc)]
    lengths = [{"bucket": label, "order": i, "sessions": sum(1 for v in seconds if lo <= v < hi)}
               for i, (lo, hi, label) in enumerate(SESSION_BUCKETS)]
    platforms = []
    for platform, in db.execute("SELECT DISTINCT platform FROM sessions WHERE " + w, a).fetchall():
        vals = [r[0] for r in db.execute("SELECT seconds FROM sessions WHERE platform=? AND seconds IS NOT NULL AND " + w,
                                         (platform, *a))]
        count = db.execute("SELECT COUNT(*) FROM sessions WHERE platform=? AND " + w, (platform, *a)).fetchone()[0]
        med = _percentile(vals, 0.5)
        platforms.append({"platform": platform, "sessions": count,
                          "median_session_min": round(med / 60.0, 1) if med is not None else None})
    platforms.sort(key=lambda r: -r["sessions"])
    first = db.execute(f"SELECT MIN(day) FROM sessions WHERE build_kind IN ({','.join('?' * len(sc.builds)) or 'NULL'})",
                       sc.builds).fetchone()[0]
    out = {**base, "built_ms": _built_ms(db), "tiles": {**active, **stiles}, "per_day": per_day,
           "sessions_per_day": sessions_per_day, "lengths": lengths, "platforms": platforms,
           "history": {"first_session_day": first}}
    db.close()
    return out


def _difficulty_sorted(rows, field="ai_difficulty"):
    return sorted(rows, key=lambda r: difficulty_rank(r[field]))


def balance(agg_path, build="release", days=90, play="all", era=None, device="all"):
    """Balance page: score and placement, wins against AI difficulty, the intensity x difficulty grid. From
    game_completed's score, placement, players_in_match, ai_difficulty and behind_by (builds after 2026-11-07)."""
    sc = Scope(build, days, play, era, device=device)
    db = _open(agg_path)
    base = {"build": build, "days": days, "play": play, **sc.describe(), "unfiltered": []}
    if db is None:
        return {**base, "empty": True}
    w, a = sc.where()
    q = lambda sql, *args: [dict(r) for r in db.execute(sql, args)]
    quit_cols = ("ROUND(100.0 * SUM(CASE WHEN quit_tracked=1 THEN abandoned ELSE 0 END) / NULLIF(SUM(quit_tracked), 0), 1) "
                 "AS quit_pct")
    common = ("COUNT(*) AS plays, SUM(completed) AS finished, COUNT(score) AS scored, ROUND(AVG(score), 1) AS avg_score, "
              "ROUND(AVG(placement), 2) AS avg_placement, ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, "
              "ROUND(AVG(CASE WHEN won = 0 THEN behind_by END), 1) AS avg_behind_by, "
              "ROUND(AVG(players_in_match), 1) AS avg_players, " + quit_cols)
    t = db.execute("SELECT COUNT(*), SUM(completed), COUNT(score), ROUND(AVG(placement), 2), "
                   "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1), COUNT(ai_difficulty), COUNT(placement) "
                   "FROM plays WHERE " + w, a).fetchone()
    by_difficulty = _difficulty_sorted(q(f"SELECT COALESCE(ai_difficulty, 'unknown') AS ai_difficulty, {common} "
                                         f"FROM plays WHERE {w} GROUP BY 1", *a))
    grid = q("SELECT COALESCE(CAST(intensity AS TEXT), 'unknown') AS intensity, COALESCE(ai_difficulty, 'unknown') "
             "AS ai_difficulty, COUNT(*) AS plays, SUM(completed) AS finished, "
             "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, ROUND(AVG(placement), 2) AS avg_placement, "
             f"{quit_cols} FROM plays WHERE {w} GROUP BY 1, 2", *a)
    placements = q("SELECT placement, COUNT(*) AS plays FROM plays WHERE placement IS NOT NULL AND " + w +
                   " GROUP BY placement ORDER BY placement", *a)
    by_mode = q(f"SELECT COALESCE(game_mode, 'unknown') AS game_mode, {common} FROM plays WHERE {w} GROUP BY 1 "
                "ORDER BY plays DESC", *a)
    marks = ",".join("?" * len(sc.builds)) or "NULL"
    hist = db.execute(f"SELECT MIN(CASE WHEN score IS NOT NULL THEN day END), MIN(CASE WHEN ai_difficulty IS NOT NULL "
                      f"THEN day END), MIN(CASE WHEN placement IS NOT NULL THEN day END) FROM plays "
                      f"WHERE build_kind IN ({marks})", sc.builds).fetchone()
    out = {**base, "built_ms": _built_ms(db),
           "tiles": {"plays": t[0] or 0, "finished": t[1] or 0, "scored": t[2] or 0, "avg_placement": t[3],
                     "win_pct": t[4], "with_difficulty": t[5] or 0, "placed": t[6] or 0},
           "by_difficulty": by_difficulty,
           "grid": sorted(grid, key=lambda r: ((not r["intensity"].isdigit(), int(r["intensity"]) if r["intensity"].isdigit() else 0),
                                               difficulty_rank(r["ai_difficulty"]))),
           "placements": placements, "by_mode": by_mode,
           "history": {"first_score_day": hist[0], "first_difficulty_day": hist[1], "first_placement_day": hist[2]},
           "notes": [BALANCE_NOTES["score"], BALANCE_NOTES["placement"], DIFFICULTY_NOTE]}
    db.close()
    return out


def quits(agg_path, build="release", days=90, play="all", era=None, device="all"):
    """Quits page: mid-match quits (match_abandoned) by reason and by how far in, the rate over the plays of builds
    that send it, and the frustration chain (repeated fails -> a quit -> gone 7 days), counts only."""
    sc = Scope(build, days, play, era, device=device)
    db = _open(agg_path)
    base = {"build": build, "days": days, "play": play, **sc.describe(), "notes": QUIT_NOTES,
            # the chain is per player-day, not per play, and has no device
            "unfiltered": ["chain"] if play != "all" or device != "all" else []}
    if db is None:
        return {**base, "empty": True}
    w, a = sc.where()
    t = db.execute("SELECT SUM(quit_tracked), SUM(CASE WHEN quit_tracked=1 THEN abandoned ELSE 0 END), "
                   "SUM(CASE WHEN quit_tracked=1 THEN completed ELSE 0 END), COUNT(*) FROM plays WHERE " + w, a).fetchone()
    elapsed = [r[0] for r in db.execute("SELECT abandon_elapsed FROM plays WHERE abandoned=1 AND abandon_elapsed IS NOT "
                                        "NULL AND " + w, a)]
    reasons = [dict(r) for r in db.execute("SELECT COALESCE(abandon_reason, 'unknown') AS reason, COUNT(*) AS quits "
                                           "FROM plays WHERE abandoned=1 AND " + w + " GROUP BY 1 ORDER BY quits DESC", a)]
    by_elapsed = [{"bucket": label, "order": i, "quits": sum(1 for v in elapsed if lo <= v < hi)}
                  for i, (lo, hi, label) in enumerate(ELAPSED_BUCKETS)]
    unknown_elapsed = db.execute("SELECT COUNT(*) FROM plays WHERE abandoned=1 AND abandon_elapsed IS NULL AND " + w,
                                 a).fetchone()[0]
    if unknown_elapsed:
        by_elapsed.append({"bucket": "not sent", "order": len(by_elapsed), "quits": unknown_elapsed})

    def by(key):
        col = PLAYS_KEY.get(key, key)
        rows = [dict(r) for r in db.execute(
            f"SELECT {col} AS key, SUM(quit_tracked) AS plays, SUM(CASE WHEN quit_tracked=1 THEN abandoned ELSE 0 END) "
            f"AS quits FROM plays WHERE {w} GROUP BY 1 HAVING SUM(quit_tracked) > 0 ORDER BY quits DESC, plays DESC", a)]
        for r in rows:
            r["quit_pct"] = _pct(r["quits"] or 0, r["plays"] or 0)
        return rows
    fw, fa = sc.where(play=False, device=False)
    c = db.execute("SELECT COALESCE(SUM(failed), 0), COALESCE(SUM(quit_after), 0), COALESCE(SUM(quit_mature), 0), "
                   "COALESCE(SUM(gone_7d), 0), COALESCE(SUM(failed_mature), 0), COALESCE(SUM(failed_gone_7d), 0) "
                   "FROM frustration WHERE " + fw, fa).fetchone()
    marks = ",".join("?" * len(sc.builds)) or "NULL"
    first = db.execute(f"SELECT MIN(day) FROM plays WHERE abandoned=1 AND build_kind IN ({marks})", sc.builds).fetchone()[0]
    med = _percentile(elapsed, 0.5)
    out = {**base, "built_ms": _built_ms(db),
           "tiles": {"tracked_plays": t[0] or 0, "quits": t[1] or 0, "quit_pct": _pct(t[1] or 0, t[0] or 0),
                     "finished_pct": _pct(t[2] or 0, t[0] or 0), "plays": t[3] or 0,
                     "median_quit_s": round(med, 1) if med is not None else None},
           "reasons": reasons, "by_elapsed": by_elapsed,
           "by_mode": by("game_mode"), "by_vessel": by("vessel_class"), "by_intensity": by("intensity"),
           "by_setup": by("setup"),
           "chain": {"failed": c[0], "quit_after": c[1], "quit_mature": c[2], "gone_7d": c[3],
                     "failed_mature": c[4], "failed_gone_7d": c[5],
                     "quit_after_pct": _pct(c[1], c[0]), "gone_after_quit_pct": _pct(c[3], c[2]),
                     "gone_after_fail_pct": _pct(c[5], c[4])},
           "history": {"first_quit_day": first}}
    db.close()
    return out


def invites(agg_path, build="release", days=90, play="all", era=None, device="all"):
    """Invites: sent -> received -> joined -> played together -> invitee back within 7 days; inviters' and invitees'
    D1 / D7 against the first-match cohorts of the same range. Counts only: who invited whom stays in events.db."""
    sc = Scope(build, days, "all", era, device=device)
    db = _open(agg_path)
    base = {"build": build, "days": days, "play": play, **sc.describe(), "notes": INVITE_NOTES,
            # invites carry no device: the Device filter reaches only the Everyone row (from the first-match cohorts)
            "unfiltered": ["page"] if play != "all" else ["funnel", "by_method", "per_day", "roles"] if device != "all"
            else []}
    if db is None:
        return {**base, "empty": True}
    w, a = sc.where(play=False, device=False)
    totals = {r[0]: r[1:] for r in db.execute(
        "SELECT method, SUM(sent), SUM(received), SUM(joined), SUM(played_together), SUM(joined_mature), "
        "SUM(invitee_d7) FROM invites WHERE " + w + " GROUP BY method", a)}
    allm = [sum((v[i] or 0) for v in totals.values()) for i in range(6)]
    funnel = [{"step": "sent", "label": "Sent", "count": allm[0]},
              {"step": "received", "label": "Received", "count": allm[1]},
              {"step": "joined", "label": "Joined", "count": allm[2]},
              {"step": "played_together", "label": "Played together", "count": allm[3]},
              {"step": "invitee_d7", "label": "Back by day 7", "count": allm[5],
               "of": allm[4], "note": "of joins at least 7 days old"}]
    per = {}
    for d, sent, joined in db.execute("SELECT day, SUM(sent), SUM(joined) FROM invites WHERE " + w + " GROUP BY day", a):
        per[d] = {"day": d, "sent": sent or 0, "joined": joined or 0}
    roles = []
    for role, label in (("inviter", "Inviters"), ("invitee", "Invitees")):
        r = db.execute("SELECT COALESCE(SUM(players), 0), COALESCE(SUM(mature), 0), COALESCE(SUM(returned_d1), 0), "
                       "COALESCE(SUM(returned_d7), 0) FROM invite_roles WHERE role=? AND " + w, (role, *a)).fetchone()
        roles.append({"role": role, "label": label, "players": r[0], "mature": r[1], "d1_pct": _pct(r[2], r[1]),
                      "d7_pct": _pct(r[3], r[1])})
    rw, ra = sc.where(day="first_day", play=False, distinct=True)
    r = db.execute("SELECT COALESCE(SUM(players), 0), COALESCE(SUM(returned_d1), 0), COALESCE(SUM(returned_d7), 0) "
                   "FROM returns WHERE " + rw, ra).fetchone()
    roles.append({"role": "everyone", "label": "Everyone (first match)", "players": r[0], "mature": r[0],
                  "d1_pct": _pct(r[1], r[0]), "d7_pct": _pct(r[2], r[0])})
    marks = ",".join("?" * len(sc.builds)) or "NULL"
    first = db.execute(f"SELECT MIN(day) FROM invites WHERE method='invite_id' AND build_kind IN ({marks})",
                       sc.builds).fetchone()[0]
    out = {**base, "built_ms": _built_ms(db), "funnel": funnel,
           "by_method": [{"method": m, "sent": v[0] or 0, "received": v[1] or 0, "joined": v[2] or 0,
                          "played_together": v[3] or 0} for m, v in sorted(totals.items())],
           "per_day": [per.get(d, {"day": d, "sent": 0, "joined": 0}) for d in _range_days(sc)],
           "roles": roles, "history": {"first_invite_id_day": first}}
    db.close()
    return out


def errors(agg_path, build="release", days=90, play="all", era=None, device="all"):
    """Errors page: client_exception by top frame, type, build, platform and version, what is new this week, and the
    players affected (a count)."""
    sc = Scope(build, days, "all", era, device=device)
    db = _open(agg_path)
    base = {"build": build, "days": days, "play": play, **sc.describe(), "notes": ERROR_NOTES,
            "unfiltered": ["page"] if play != "all" else []}
    if db is None:
        return {**base, "empty": True}
    w, a = sc.where(play=False)
    week = (datetime.now(timezone.utc) - timedelta(days=6)).strftime("%Y-%m-%d")
    first = {(r[0], r[1]): r[2] for r in db.execute(
        "SELECT exception_type, top_frame, MIN(first_day) FROM client_error_signatures GROUP BY 1, 2")}
    sig_new = {(r[0], r[1], r[2]) for r in db.execute(
        "SELECT exception_type, top_frame, message_hash FROM client_error_signatures WHERE first_day >= ?", (week,))}
    t = db.execute("SELECT COALESCE(SUM(events), 0), COALESCE(SUM(occurrences), 0) FROM client_errors WHERE " + w,
                   a).fetchone()
    sigs = db.execute("SELECT DISTINCT exception_type, top_frame, message_hash FROM client_errors WHERE " + w, a).fetchall()
    pc_table, dx = sc.counted("player_counts")
    frame_players = dict(db.execute(
        f"SELECT key, SUM(players) FROM {pc_table} WHERE span=? AND source=? AND dim='error_frame' AND build_kind IN "
        f"({','.join('?' * len(sc.pc_builds)) or 'NULL'}){dx} GROUP BY key",
        (sc.span_none, sc.source, *sc.pc_builds)).fetchall())
    frames = []
    for r in db.execute("SELECT top_frame, exception_type, SUM(events) AS events, SUM(occurrences) AS occurrences, "
                        "MAX(day) AS last_day, COUNT(DISTINCT message_hash) AS messages FROM client_errors WHERE " + w +
                        " GROUP BY 1, 2 ORDER BY occurrences DESC LIMIT 50", a):
        f0 = first.get((r["exception_type"], r["top_frame"]))
        frames.append({**dict(r), "first_day": f0, "new": bool(f0 and f0 >= week),
                       "players": frame_players.get(r["top_frame"], 0)})

    def by(col, scope=sc):
        sw, sa = scope.where(play=False)
        return [dict(r) for r in db.execute(
            f"SELECT {col} AS key, SUM(events) AS events, SUM(occurrences) AS occurrences FROM client_errors "
            f"WHERE {sw} GROUP BY 1 ORDER BY occurrences DESC LIMIT 30", sa)]
    per = dict(db.execute("SELECT day, SUM(occurrences) FROM client_errors WHERE " + w + " GROUP BY day", a).fetchall())
    out = {**base, "built_ms": _built_ms(db),
           "tiles": {"events": t[0], "occurrences": t[1], "signatures": len(sigs),
                     "players": _player_count(db, sc, "error", "any"),
                     "new_this_week": sum(1 for x in sigs if tuple(x) in sig_new)},
           "by_frame": frames, "by_type": by("exception_type"), "by_platform": by("platform"), "by_device": by("device"),
           "by_version": by("app_version"), "by_scene": by("scene"),
           "by_build": by("build_kind", sc.with_builds(BUILD_KINDS)),
           "per_day": [{"day": d, "occurrences": per.get(d, 0)} for d in _range_days(sc)],
           "history": {"first_error_day": db.execute("SELECT MIN(first_day) FROM client_error_signatures").fetchone()[0]}}
    db.close()
    return out


def digest_facts(agg_path):
    """The aggregates Claude's overnight read is written from: totals, last 7 days against the 7 before, health.
    Numbers only, no ids of any kind (match ids included)."""
    facts = {}
    for build in ("release", "editor", "port", "development"):
        week = overview(agg_path, build, 7)
        prior = overview(agg_path, build, 14)
        if week.get("empty"):
            return {"empty": True}
        facts[build] = {"last_7_days": week["tiles"], "last_14_days": prior["tiles"],
                        "rematch_by_play_type_90d": overview(agg_path, build, 90)["rematch_by_play_type"]}
    facts["match_health_90d"] = overview(agg_path, "release", 90)["match_health_by_build"]
    facts["players_90d"] = {"funnel": audience(agg_path, "release", 90)["funnel"],
                            "launch_to_menu": audience(agg_path, "release", 30)["launches"],
                            "countries": audience(agg_path, "release", 30)["countries"][:8]}
    eco = economy(agg_path, "release", 30)
    facts["economy_30d"] = {"totals": eco["totals"], "double_payouts": eco["double_payouts"],
                            "blocked": eco["blocked"][:5]}
    h = health(agg_path, {}, 14)
    facts["health_14d"] = {"rows_per_play": h["rows_per_play"][-14:], "rejects": h["rejects"],
                           "editor_share_pct": h["editor_share_pct"]}
    return facts


# ---------------------------------------------------------------- the Ask box's one door

_FORBIDDEN = re.compile(r"\b(attach|detach|pragma|insert|update|delete|drop|alter|create|replace|vacuum|reindex|"
                        r"analyze|load_extension|readfile|writefile)\b", re.IGNORECASE)


class UnsafeQuery(ValueError):
    pass


def check_sql(sql):
    """A single SELECT (or WITH ... SELECT) over the aggregates. Raises UnsafeQuery with the reason otherwise.
    The authorizer below is the real guard; this gives a clear message before the query is tried."""
    text = (sql or "").strip().rstrip(";").strip()
    if not text:
        raise UnsafeQuery("empty query")
    if ";" in text:
        raise UnsafeQuery("one statement only")
    if not re.match(r"^(select|with)\b", text, re.IGNORECASE):
        raise UnsafeQuery("only SELECT queries are run")
    if _FORBIDDEN.search(text):
        raise UnsafeQuery("the query uses a word that is not allowed: " + _FORBIDDEN.search(text).group(0))
    return text


def run_readonly_sql(agg_path, sql, max_rows=MAX_ASK_ROWS):
    text = check_sql(sql)
    started = time.monotonic()
    db = _open(agg_path)
    if db is None:
        raise UnsafeQuery("there is no data yet")

    def authorizer(action, arg1, arg2, dbname, source):
        allowed = {sqlite3.SQLITE_SELECT, sqlite3.SQLITE_READ, sqlite3.SQLITE_FUNCTION}
        if hasattr(sqlite3, "SQLITE_RECURSIVE"):
            allowed.add(sqlite3.SQLITE_RECURSIVE)
        if action == sqlite3.SQLITE_FUNCTION and str(arg2).lower() in ("load_extension", "readfile", "writefile"):
            return sqlite3.SQLITE_DENY
        return sqlite3.SQLITE_OK if action in allowed else sqlite3.SQLITE_DENY

    steps = {"n": 0}

    def progress():
        steps["n"] += 1
        return 1 if steps["n"] * 1000 > MAX_ASK_STEPS else 0

    db.set_authorizer(authorizer)
    db.set_progress_handler(progress, 1000)
    try:
        cur = db.execute(text)
        columns = [d[0] for d in cur.description or []]
        rows = [list(r) for r in cur.fetchmany(max_rows + 1)]
    except sqlite3.DatabaseError as exc:
        raise UnsafeQuery(f"the query failed: {exc}") from None
    finally:
        db.close()
    return {"columns": columns, "rows": rows[:max_rows], "truncated": len(rows) > max_rows,
            "ms": round((time.monotonic() - started) * 1000)}
