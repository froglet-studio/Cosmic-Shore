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


BUILD_KINDS = ("release", "development", "editor", "port")


class BadRequest(ValueError):
    """A question these views cannot answer as asked; the server answers 400 with the reason."""


def _today():
    return datetime.now(timezone.utc).strftime("%Y-%m-%d")


class Scope:
    """What a page counts: one build (or several), a day range and a source, from the days window or an era.
    An era (Rev 65) replaces the days window with its own range (to today at most), counts only its source
    (imported = PostHog history, live = sent here) and, when it names builds, only those."""

    def __init__(self, build, days=90, play="all", era=None, builds=None):
        if days not in WINDOWS:
            raise BadRequest(f"days must be one of {list(WINDOWS)}")
        _play(play)
        self.build, self.days, self.play, self.era = build, days, play, era
        if era:
            self.since, self.until = era["from"], min(era["to"], _today())
            self.source = era["source"]
            self.span_none, self.span_week = "era:" + era["id"], "era_week:" + era["id"]
        else:
            self.since, self.until, self.source = _since(days), _today(), "any"
            self.span_none, self.span_week = f"window:{days}", "week"
        chosen = list(builds) if builds is not None else [build]
        if era and era.get("builds"):
            chosen = [b for b in chosen if b in era["builds"]]
        self.builds = chosen

    def with_builds(self, builds):
        return Scope(self.build, self.days, self.play, self.era, builds)

    def where(self, day="day", sourced=True, play=True, builds=None):
        """SQL (and its arguments) for these builds, days, source and play group. `sourced` is False for a table
        without an imported column; `play` False for one without a play type."""
        chosen = self.builds if builds is None else builds
        sql = f"build_kind IN ({','.join('?' * len(chosen)) or 'NULL'}) AND {day} BETWEEN ? AND ?"
        args = [*chosen, self.since, self.until]
        if sourced and self.source != "any":
            sql += " AND imported = ?"
            args.append(1 if self.source == "imported" else 0)
        if play:
            sql += _play(self.play)
        return sql, args

    def unfiltered(self, play_ignores=(), source_ignores=(), era_ignores=()):
        """The names a page lists as ignoring the play filter or the era."""
        out = list(play_ignores) if self.play != "all" else []
        if self.era:
            out += [n for n in era_ignores if n not in out]
            if self.source != "any":
                out += [n for n in source_ignores if n not in out]
        return out

    def describe(self):
        return {"from": self.since, "to": self.until, "source": self.source,
                "era": self.era["id"] if self.era else None}


def _days_between(since, until):
    start, end = datetime.strptime(since, "%Y-%m-%d"), datetime.strptime(until, "%Y-%m-%d")
    return [(start + timedelta(days=n)).strftime("%Y-%m-%d") for n in range(max(0, (end - start).days + 1))]


def _player_count(db, sc, dim="none", key="", span=None, builds=None):
    """Distinct players in the scope from player_counts (one build at a time is exact; '*' is every build)."""
    chosen = sc.builds if builds is None else builds
    if not chosen:
        return 0
    row = db.execute(
        f"SELECT SUM(players) FROM player_counts WHERE span=? AND source=? AND dim=? AND key=? AND "
        f"build_kind IN ({','.join('?' * len(chosen))})",
        (span or sc.span_none, sc.source, dim, key, *chosen)).fetchone()
    return row[0] or 0


def overview(agg_path, build="release", days=90, play="all", era=None):
    sc = Scope(build, days, play, era)
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    q = lambda sql, *a: db.execute(sql, a).fetchall()
    w, a = sc.where()
    wd, ad = sc.where(play=False)
    if era:
        distinct_players = _player_count(db, sc)
    else:
        window = q("SELECT players FROM players_in_window WHERE build_kind=? AND days=?", build, days)
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
    events_24h = [dict(r) for r in db.execute(
        "SELECT event, SUM(rows) AS rows, SUM(near_dup_rows) AS near_duplicates FROM daily_events "
        "WHERE build_kind=? AND day>=? GROUP BY event ORDER BY rows DESC LIMIT 12", (build, yesterday))]
    # Rev 65: the tiles marketing and business read (counts only)
    marks = ",".join("?" * len(sc.builds)) or "NULL"
    new_by_day = {r[0]: r[1] for r in q(
        f"SELECT x, SUM(new_players) FROM player_counts WHERE span='day' AND source=? AND dim='none' "
        f"AND build_kind IN ({marks}) AND x BETWEEN ? AND ? GROUP BY x", sc.source, *sc.builds, sc.since, sc.until)}
    split = lambda dim: [{dim: r[0], "players": r[1]} for r in q(
        f"SELECT key, SUM(players) AS n FROM player_counts WHERE span=? AND source=? AND dim=? "
        f"AND build_kind IN ({marks}) GROUP BY key ORDER BY n DESC, key", sc.span_none, sc.source, dim, *sc.builds)]
    event_rows = lambda name: q("SELECT COALESCE(SUM(rows), 0) FROM daily_events WHERE event=? AND " + wd,
                                name, *ad)[0][0]
    crystals = {r[0]: r[1] or 0 for r in q(
        f"SELECT kind, SUM(amount) FROM economy WHERE build_kind IN ({marks}) AND day BETWEEN ? AND ? GROUP BY kind",
        *sc.builds, sc.since, sc.until)}
    whales = q("SELECT COALESCE(SUM(players), 0) FROM balances WHERE build_kind=? AND bucket='2,000+'", build)[0][0]
    result = {
        "build": build, "days": days, "play": play, "built_ms": _built_ms(db), **sc.describe(),
        "unfiltered": sc.unfiltered(["players", "rows_per_play", "events_24h", "marketing", "business"],
                                    ["business.crystals_earned", "business.crystals_spent"],
                                    ["events_24h", "business.whales_2000"]),
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
            "by_country": split("country"), "by_platform": split("platform"),
            "shares": event_rows("share_triggered"), "invites_sent": event_rows("party_invite_sent")},
        "business": {"crystals_earned": crystals.get("earned", 0),
                     "crystals_spent": crystals.get("spent", 0) + crystals.get("unlock", 0),
                     "whales_2000": whales},
    }
    db.close()
    return result


def retention_by_play_type(db, build, since, play="all", scope=None):
    if scope is None:
        pf = _play(play)
        where, args = "build_kind=? AND day>=?" + pf, [build, since]
        rwhere, rargs = "build_kind=? AND first_day>=?" + pf, [build, since]
    else:
        where, args = scope.where()
        rwhere, rargs = scope.where(day="first_day")
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


def retention(agg_path, build="release", days=90, play="all", era=None):
    sc = Scope(build, days, play, era)
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    rw, ra = sc.where(day="first_day")
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


def matches(agg_path, build="release", days=90, play="all", era=None):
    sc = Scope(build, days, play, era)
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    w, a = sc.where()
    result = {
        "build": build, "days": days, "play": play, "built_ms": _built_ms(db), **sc.describe(),
        "unfiltered": sc.unfiltered(["signals"], ["signals"]),
        "per_day": _every_day(sc.since, days, {r["day"]: dict(r) for r in db.execute(
            "SELECT day, COUNT(DISTINCT match_id) AS matches, COUNT(*) AS plays, SUM(completed) AS completed "
            "FROM plays WHERE " + w + " GROUP BY day ORDER BY day", a)}, sc.until),
        "by_mode": [dict(r) for r in db.execute(
            "SELECT game_mode, COUNT(*) AS plays, SUM(completed) AS completed, "
            "ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct, "
            "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, "
            "ROUND(AVG(flight_time_seconds), 1) AS avg_flight_s FROM plays "
            "WHERE " + w + " GROUP BY game_mode ORDER BY plays DESC", a)],
        "by_vessel": [dict(r) for r in db.execute(
            "SELECT vessel_class, COUNT(*) AS plays, ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct, "
            "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, ROUND(AVG(flight_time_seconds), 1) AS avg_flight_s "
            "FROM plays WHERE " + w + " GROUP BY vessel_class ORDER BY plays DESC", a)],
        "signals": [dict(r) for r in db.execute(
            "SELECT game_mode, SUM(favorited) AS favorited, SUM(unfavorited) AS unfavorited, SUM(fails) AS fails, "
            "SUM(shares) AS shares FROM mode_signals WHERE " + sc.where(sourced=False, play=False)[0] +
            " GROUP BY game_mode ORDER BY favorited - unfavorited DESC", sc.where(sourced=False, play=False)[1])],
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
    meta = db.execute("SELECT value FROM meta WHERE key='modes_without_element'").fetchone()
    result["modes_without_element"] = json.loads(meta[0]) if meta else []      # every match ever, by game_mode
    db.close()
    return result


def audience(agg_path, build="release", days=90):
    """Who plays: active and new players per day, platforms, countries, the first-day funnel, launch time, how
    sessions end."""
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    since = _since(days)
    funnel = db.execute(
        "SELECT SUM(players), SUM(reached_menu), SUM(started_match), SUM(finished_match), SUM(finished_ftue), "
        "SUM(came_back) FROM funnel WHERE build_kind=? AND first_day>=?", (build, since)).fetchone()
    steps = ["New players", "Reached the menu", "Started a match", "Finished a match", "Finished the FTUE",
             "Came back another day"]
    result = {
        "build": build, "days": days, "built_ms": _built_ms(db),
        "per_day": _every_day_players(since, days, {r["day"]: dict(r) for r in db.execute(
            "SELECT day, players, new_players FROM daily_players WHERE build_kind=? AND day>=?", (build, since))}),
        "platforms": [dict(r) for r in db.execute(
            "SELECT platform, players FROM players_by_platform WHERE build_kind=? AND days=? ORDER BY players DESC",
            (build, days))],
        "countries": [dict(r) for r in db.execute(
            "SELECT country, players FROM players_by_country WHERE build_kind=? AND days=? ORDER BY players DESC",
            (build, days))],
        "funnel": [{"step": name, "players": funnel[i] or 0} for i, name in enumerate(steps)] if funnel[0] else [],
        "launches": [dict(r) for r in db.execute(
            "SELECT platform, launches, p50_seconds, p90_seconds FROM launches WHERE build_kind=? AND days=? "
            "ORDER BY launches DESC", (build, days))],
        "session_ends": [dict(r) for r in db.execute(
            "SELECT reason, SUM(count) AS count FROM session_ends WHERE build_kind=? AND day>=? GROUP BY reason "
            "ORDER BY count DESC", (build, since))],
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
    cohort = db.execute("SELECT SUM(players), SUM(finished_ftue) FROM funnel WHERE build_kind=? AND first_day>=?",
                        (build, since)).fetchone()
    steps = [dict(r) for r in db.execute(
        "SELECT quest_id, phase_index, step_id, MAX(step_name) AS step_name, SUM(players) AS players, "
        "ROUND(SUM(seconds_sum) / NULLIF(SUM(seconds_n), 0), 0) AS avg_seconds FROM ftue_steps "
        "WHERE build_kind=? AND first_day>=? GROUP BY quest_id, phase_index, step_id "
        "ORDER BY quest_id, phase_index, avg_seconds", (build, since))]
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
    by_day = {}
    for r in db.execute("SELECT day, kind, SUM(amount) AS amount FROM economy WHERE build_kind=? AND day>=? "
                        "GROUP BY day, kind", (build, since)):
        by_day.setdefault(r["day"], {})[r["kind"]] = r["amount"]
    start = datetime.strptime(since, "%Y-%m-%d")
    per_day = []
    for n in range(days):
        day = (start + timedelta(days=n)).strftime("%Y-%m-%d")
        entry = by_day.get(day, {})
        per_day.append({"day": day, "earned": entry.get("earned", 0), "spent": entry.get("spent", 0) + entry.get("unlock", 0)})
    totals = {r["kind"]: dict(r) for r in db.execute(
        "SELECT kind, SUM(events) AS events, SUM(amount) AS amount FROM economy WHERE build_kind=? AND day>=? "
        "GROUP BY kind", (build, since))}
    result = {
        "build": build, "days": days, "built_ms": _built_ms(db), "per_day": per_day, "totals": totals,
        "sources": [dict(r) for r in db.execute(
            "SELECT kind, source, SUM(events) AS events, SUM(amount) AS amount FROM economy "
            "WHERE build_kind=? AND day>=? AND kind IN ('earned', 'spent', 'unlock') GROUP BY kind, source "
            "ORDER BY kind, amount DESC", (build, since))],
        "blocked": [dict(r) for r in db.execute(
            "SELECT source AS item, SUM(events) AS events, SUM(amount) AS short_by FROM economy "
            "WHERE build_kind=? AND day>=? AND kind='blocked' GROUP BY source ORDER BY events DESC", (build, since))],
        "unlocks": [dict(r) for r in db.execute(
            "SELECT kind, name, SUM(count) AS count FROM unlocks WHERE build_kind=? AND day>=? GROUP BY kind, name "
            "ORDER BY kind, count DESC", (build, since))],
        "balances": [dict(r) for r in db.execute(
            "SELECT bucket, players FROM balances WHERE build_kind=? ORDER BY bucket_order", (build,))],
        "double_payouts": dict(db.execute(
            "SELECT COALESCE(SUM(count), 0) AS count, COALESCE(SUM(amount), 0) AS amount FROM double_payouts "
            "WHERE build_kind=? AND day>=?", (build, since)).fetchone()),
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
    marks = ",".join("?" * len(SOCIAL_EVENTS))
    totals = {r["event"]: dict(r) for r in db.execute(
        f"SELECT event, SUM(rows) AS count, MAX(players) AS peak_daily_players FROM daily_events "
        f"WHERE build_kind=? AND day>=? AND event IN ({marks}) GROUP BY event", (build, since, *SOCIAL_EVENTS))}
    get = lambda e: (totals.get(e) or {}).get("count") or 0
    per_day = {}
    for r in db.execute(f"SELECT day, event, SUM(rows) AS rows FROM daily_events WHERE build_kind=? AND day>=? "
                        f"AND event IN ('party_invite_sent', 'party_joined', 'friend_added') GROUP BY day, event",
                        (build, since)):
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
    FREESTYLE_MEASURES
MATCH_BY = ("none", "play_type", "era", "platform", "country", "game_mode", "vessel_class", "build_kind", "intensity",
            "element")
SERIES_BY = MATCH_BY + ("from_vessel_class", "switch_source")
SERIES_OVER = ("day", "week", "none")
PLAY_MEASURES = ("matches", "finished_pct", "rematch_pct", "flight_min")
COUNT_MEASURES = ("matches", "players", "new_players", "events") + FREESTYLE_MEASURES
ELEMENT_ORDER = ("Charge", "Mass", "Space", "Time", "Omni", "none")
# What each measure can be split by. events come from daily_events (day, build, event, source): no player, mode or
# platform on them, so only these three splits are answerable; the console hides the rest. Freestyle (the Toybox) is
# not a match: no mode, play type or element; its vessel comes from builds after 2026-11-06.
FREESTYLE_BY = ("none", "era", "build_kind", "vessel_class")
SERIES_ANSWERS = {**{m: MATCH_BY for m in PLAY_MEASURES + ("players", "new_players")},
                  "events": ("none", "era", "build_kind"),
                  "freestyle_entries": FREESTYLE_BY, "freestyle_players": FREESTYLE_BY,
                  "freestyle_minutes": FREESTYLE_BY,
                  "freestyle_switches": FREESTYLE_BY + ("from_vessel_class", "switch_source")}
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
PLAYS_KEY = {"play_type": "play_type", "platform": "COALESCE(platform, 'unknown')",
             "element": "COALESCE(element, 'none')",
             "country": "COALESCE(country, 'unknown')", "game_mode": "COALESCE(game_mode, 'unknown')",
             "vessel_class": "COALESCE(vessel_class, 'unknown')",
             "intensity": "COALESCE(CAST(intensity AS TEXT), 'unknown')", "build_kind": "build_kind"}
GROUP_TYPES = {"solo_vs_ai": ("solo_vs_ai",), "solo": ("solo_alone",),
               "multi": ("coop", "adversarial", "mixed", "multi_human_unknown")}
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
        f"SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END), SUM(flight_time_seconds), COUNT(flight_time_seconds) "
        f"FROM plays WHERE {w} GROUP BY x, key", a).fetchall()
    out = {}
    for x, k, matches_n, plays_n, done, rematches, flight, flown in rows:
        num, den = {"matches": (matches_n, None), "finished_pct": (done or 0, plays_n),
                    "rematch_pct": (rematches or 0, done or 0), "flight_min": ((flight or 0) / 60.0, flown)}[measure]
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
    if column == "players" and over == "week":
        span, x_col = sc.span_week, "x"
    elif column == "players" and over == "none":
        span, x_col = sc.span_none, "NULL"
    else:
        span, x_col = "day", _x_of(over, "x")
    marks = ",".join("?" * len(sc.builds)) or "NULL"
    sql = (f"SELECT {x_col} AS xx, {('build_kind' if by == 'build_kind' else 'key')} AS kk, SUM({column}) "
           f"FROM player_counts WHERE span=? AND source=? AND dim=? AND build_kind IN ({marks})")
    args = [span, sc.source, dim, *sc.builds]
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
        keys = {"vessel_class": "vessel_class", "from_vessel_class": "from_vessel_class", "switch_source": "origin"}
    else:
        table = "freestyle_vessels"
        value = {"freestyle_entries": "entries", "freestyle_minutes": "seconds / 60.0"}[measure]
        keys = {"vessel_class": "vessel_class"}
    key = f"'{era_key}'" if era_key else ("build_kind" if by == "build_kind" else keys.get(by, "NULL"))
    return {(x, k): [n or 0, None] for x, k, n in db.execute(
        f"SELECT {_x_of(over)} AS x, {key} AS key, SUM({value}) FROM {table} WHERE {w} GROUP BY x, key", a)}


def _event_cells(db, sc, over, by, era_key):
    w, a = sc.where(play=False)
    key = f"'{era_key}'" if era_key else ("build_kind" if by == "build_kind" else "NULL")
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
    return round(num / den, 1) if measure == "flight_min" else round(100.0 * num / den, 1)


def series(agg_path, measure="matches", by="none", over="day", build="release", days=90, play="all", era=None,
           eras=()):
    """One switchable chart (Rev 65): a measure over time (day / week / none), split by one dimension. Points carry
    x (a day, a week's Monday or null) and key (a split value or null); keys are the split's top 8 and "other"."""
    check_series(measure, by, over)
    sc = Scope(build, days, play, era, BUILD_KINDS if by == "build_kind" else None)
    db = _open(agg_path)
    base = {"measure": measure, "by": by, "over": over, "build": build, "days": days, "play": play, **sc.describe(),
            # the splits this measure can take, so the console offers only those (no 400 to find out)
            "answerable_by": list(SERIES_ANSWERS[measure])}
    if db is None:
        return {**base, "points": [], "keys": [], "unfiltered": [], "empty": True}
    if by == "era":
        parts = [(e["id"], Scope(build, days, play, e, BUILD_KINDS if by == "build_kind" else None))
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
                                                                          and by not in ("none", "play_type"))):
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
                     "two-element mode counts under its first (Brood Rush: Mass, and Space as its second); none: a "
                     "mode with no genre or an old mode name")
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


def cohorts(agg_path, build="release", days=90, play="all", era=None):
    """Weekly cohorts (Rev 65): players by the week of their first day, the share active each week after, and the
    lifespan tiles. Player lifespans themselves come from the raw store (profile readers only)."""
    sc = Scope(build, days, play, era)
    db = _open(agg_path)
    if db is None:
        return {**_empty(build, days), "weekly": [], "tiles": {}}
    group = play if play != "all" else "all"
    marks = ",".join("?" * len(sc.builds)) or "NULL"
    first_week = _week(sc.since)
    sizes, active = {}, {}
    for cohort, k, n in db.execute(
            f"SELECT cohort, week_index, SUM(players) FROM cohort_weeks WHERE build_kind IN ({marks}) AND source=? "
            f"AND play_group=? AND cohort BETWEEN ? AND ? GROUP BY cohort, week_index",
            (*sc.builds, sc.source, group, first_week, sc.until)):
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
            f"SELECT first_day, lifespan_days, players, returned_d1, returned_d7, lapsed_14d FROM cohort_days "
            f"WHERE build_kind IN ({marks}) AND source=? AND play_group=? AND first_day BETWEEN ? AND ?",
            (*sc.builds, sc.source, group, sc.since, sc.until)):
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


def playtypes(agg_path, build="release", days=90, era=None):
    """Play types page (Rev 65): solo vs AI, solo and multiplayer side by side, and the party funnel."""
    sc = Scope(build, days, "all", era)
    db = _open(agg_path)
    if db is None:
        return {**_empty(build, days), "groups": {}, "share": {}, "funnel": []}
    groups = {}
    for group in GROUP_TYPES:
        gs = Scope(build, days, group, era)
        w, a = gs.where()
        row = db.execute(
            "SELECT COUNT(DISTINCT match_id), COUNT(*), SUM(completed), SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END), "
            "AVG(flight_time_seconds) FROM plays WHERE " + w, a).fetchone()
        rw, ra = gs.where(day="first_day")
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


def _era_scope(era, build):
    """An era's scope for the eras pages: one build, or every build (players then from '*', each player once)."""
    return Scope(build or "release", 90, "all", era, None if build else BUILD_KINDS)


def eras_page(agg_path, eras, milestones, build=None):
    """Eras page (Rev 65): each era with its events, players and matches; events per week coloured by era (an event
    belongs to the first era, in list order, whose range, source and builds hold it); the milestones."""
    db = _open(agg_path)
    out = []
    for era in eras:
        sc = _era_scope(era, build)
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
        for day, imported, n in db.execute(
                f"SELECT day, imported, SUM(rows) FROM daily_events WHERE {where} GROUP BY day, imported", args):
            src = "imported" if imported else "live"
            owner = next((e["id"] for e in eras if e["from"] <= day <= e["to"] and e["source"] in ("any", src)
                          and (not build or not e["builds"] or build in e["builds"])), None)
            key = (_week(day), owner)
            weekly[key] = weekly.get(key, 0) + n
        db.close()
    return {"build": build, "eras": out, "milestones": milestones,
            "weekly": [{"week": w, "era": e, "events": n} for (w, e), n in
                       sorted(weekly.items(), key=lambda kv: (kv[0][0], str(kv[0][1])))],
            "notes": ["an era's days run from its from to its to (today at most); imported = PostHog history, "
                      "live = sent to this server; era null in weekly = events in no era"]}


def eras_compare(agg_path, a, b, build=None):
    db = _open(agg_path)

    def one(era):
        sc = _era_scope(era, build)
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
        rw, ra = sc.where(day="first_day")
        ret = db.execute("SELECT SUM(players), SUM(returned_d1) FROM returns WHERE " + rw, ra).fetchone()
        wd, ad = sc.where(play=False)
        rpp = db.execute("SELECT SUM(rows), SUM(plays) FROM daily_plays WHERE " + wd, ad).fetchone()
        new = db.execute(f"SELECT COALESCE(SUM(new_players), 0) FROM player_counts WHERE span=? AND source=? AND "
                         f"dim='none' AND build_kind=?", (sc.span_none, sc.source, who[0])).fetchone()[0]
        return {"id": era["id"], "name": era["name"], **sc.describe(),
                "players": _player_count(db, sc, builds=who), "new_players": new, "matches": row[0] or 0,
                "editor_share_pct": _pct(split.get("editor", 0), sum(split.values())),
                "finished_pct": _pct(row[2] or 0, row[1] or 0), "rematch_pct": _pct(row[3] or 0, row[2] or 0),
                "d1_pct": _pct(ret[1] or 0, ret[0] or 0), "rows_per_play": round(rpp[0] / rpp[1], 2) if rpp[1] else None}
    result = {"build": build, "a": one(a), "b": one(b),
              "notes": ["editor_share_pct counts every build; the rest count the build asked for (none = every build)"]}
    if db is not None:
        db.close()
    return result


def builtin_tile(agg_path, name, build="release", days=90, play="all", era=None):
    """A Dashboards built-in tile as {columns, rows}."""
    sc = Scope(build, days, play, era)
    db = _open(agg_path)
    if db is None:
        return {"columns": [], "rows": [], "unfiltered": [], "empty": True}
    marks = ",".join("?" * len(sc.builds)) or "NULL"
    unfiltered = []
    if name == "new_players":
        got = dict(db.execute(f"SELECT x, SUM(new_players) FROM player_counts WHERE span='day' AND source=? AND "
                              f"dim='none' AND build_kind IN ({marks}) AND x BETWEEN ? AND ? GROUP BY x",
                              (sc.source, *sc.builds, sc.since, sc.until)).fetchall())
        columns, rows = ["day", "new_players"], [[d, got.get(d, 0)] for d in _days_between(sc.since, sc.until)]
        unfiltered = sc.unfiltered(["new_players"])
    elif name == "d1_return":
        rw, ra = sc.where(day="first_day")
        columns = ["day", "players", "d1_pct"]
        rows = [[d, n, _pct(r1 or 0, n)] for d, n, r1 in db.execute(
            "SELECT first_day, SUM(players), SUM(returned_d1) FROM returns WHERE " + rw +
            " GROUP BY first_day ORDER BY first_day", ra)]
    elif name == "ftue_finished":
        columns = ["day", "new_players", "finished_ftue", "finished_pct"]
        rows = [[d, n, f, _pct(f or 0, n)] for d, n, f in db.execute(
            f"SELECT first_day, SUM(players), SUM(finished_ftue) FROM funnel WHERE build_kind IN ({marks}) AND "
            f"first_day BETWEEN ? AND ? GROUP BY first_day ORDER BY first_day", (*sc.builds, sc.since, sc.until))]
        unfiltered = sc.unfiltered(["ftue_finished"], ["ftue_finished"])
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


def freestyle(agg_path, build="release", days=90, play="all", era=None):
    """Freestyle (the Toybox), Shombith 2026-11-06: who went in and flew vessels there. Entries go back to the PostHog
    history; minutes and stretches come from freestyle_ended, exact switches from freestyle_vessel_changed (builds
    after 2026-11-06), recovered switches from the identify history (builds since 2026-10-12, approximate time)."""
    sc = Scope(build, days, "all", era)
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
    players_by_day = {x: n for x, n in db.execute(
        f"SELECT x, SUM(players) FROM player_counts WHERE span='day' AND source=? AND dim='freestyle' AND key='entered' "
        f"AND build_kind IN ({','.join('?' * len(sc.builds)) or 'NULL'}) AND x BETWEEN ? AND ? GROUP BY x",
        (sc.source, *sc.builds, sc.since, sc.until))}
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
    hw, ha = "build_kind = ?", [build]                                   # history: all time, this build
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
                 filters.get("build", "release"), filters.get("days", 90), filters.get("play", "all"), era, eras)
    if out["by"] == "none":
        columns, rows = ["x", out["measure"]], [[p["x"], p["value"]] for p in out["points"]]
    else:
        columns, rows = ["x", out["by"], out["measure"]], [[p["x"], p["key"], p["value"]] for p in out["points"]]
    return {"columns": columns, "rows": rows, "series": out, "preset": {"id": preset["id"], "name": preset["name"]},
            "form": settings.get("form"), "unfiltered": ["build", "days", "play", "era"]}


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
