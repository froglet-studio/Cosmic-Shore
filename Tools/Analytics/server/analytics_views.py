"""What the dashboard shows (Frogma Rev 50 B, the Console), read from aggregates.db only - never from the raw events.

Each function returns plain JSON for one page of the console: overview, retention, matches, data health.
`run_readonly_sql` is the one door the Ask box has into the data: SELECT-only, aggregates only, row and time capped.
Standard library only.
"""
import os
import re
import sqlite3
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


def overview(agg_path, build="release", days=90):
    if days not in WINDOWS:
        raise ValueError(f"days must be one of {WINDOWS}")
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    since = _since(days)
    q = lambda sql, *a: db.execute(sql, a).fetchall()
    window = q("SELECT players FROM players_in_window WHERE build_kind=? AND days=?", build, days)
    distinct_players = window[0][0] if window else 0
    plays = q("SELECT COUNT(*) AS n, SUM(completed) AS done, COUNT(DISTINCT match_id) AS matches, "
              "SUM(CASE WHEN humans>=2 THEN 1 ELSE 0 END) AS multi FROM plays WHERE build_kind=? AND day>=?",
              build, since)[0]
    multi_matches = q("SELECT COUNT(DISTINCT match_id) FROM plays WHERE build_kind=? AND day>=? AND humans>=2",
                      build, since)[0][0]
    rpp = q("SELECT SUM(rows), SUM(plays) FROM daily_plays WHERE build_kind=? AND day>=?", build, since)[0]
    by_type = retention_by_play_type(db, build, since)
    solo_ai = next((r for r in by_type if r["play_type"] == "solo_vs_ai"), None)
    health = q("SELECT build_kind, COUNT(*), SUM(completed) FROM plays WHERE day>=? GROUP BY build_kind", since)
    rpp_by_build = {r[0]: (round(1.0 * r[1] / r[2], 2) if r[2] else None) for r in
                    q("SELECT build_kind, SUM(rows), SUM(plays) FROM daily_plays WHERE day>=? GROUP BY build_kind",
                      since)}
    yesterday = (datetime.now(timezone.utc) - timedelta(days=1)).strftime("%Y-%m-%d")
    events_24h = [dict(r) for r in db.execute(
        "SELECT event, SUM(rows) AS rows, SUM(near_dup_rows) AS near_duplicates FROM daily_events "
        "WHERE build_kind=? AND day>=? GROUP BY event ORDER BY rows DESC LIMIT 12", (build, yesterday))]
    result = {
        "build": build, "days": days, "built_ms": _built_ms(db),
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
    }
    db.close()
    return result


def retention_by_play_type(db, build, since):
    rows = db.execute(
        "SELECT play_type, COUNT(*) AS plays, COUNT(DISTINCT match_id) AS matches, SUM(completed) AS completed, "
        "SUM(CASE WHEN rematched=1 THEN 1 ELSE 0 END) AS rematches FROM plays "
        "WHERE build_kind=? AND day>=? GROUP BY play_type", (build, since)).fetchall()
    returns = {r[0]: r for r in db.execute(
        "SELECT play_type, SUM(players), SUM(returned_d1), SUM(returned_d7) FROM returns "
        "WHERE build_kind=? AND first_day>=? GROUP BY play_type", (build, since))}
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


def retention(agg_path, build="release", days=90):
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    since = _since(days)
    result = {"build": build, "days": days, "built_ms": _built_ms(db),
              "by_play_type": retention_by_play_type(db, build, since),
              "cohorts": [dict(r) for r in db.execute(
                  "SELECT first_day, play_type, players, returned_d1, returned_d7 FROM returns "
                  "WHERE build_kind=? AND first_day>=? ORDER BY first_day", (build, since))]}
    db.close()
    return result


def _every_day(since, days, by_day):
    """One row per day of the range, zeros where nothing was played, so a chart's axis is time and not just data."""
    start = datetime.strptime(since, "%Y-%m-%d")
    out = []
    for n in range(days):
        day = (start + timedelta(days=n)).strftime("%Y-%m-%d")
        out.append(by_day.get(day, {"day": day, "matches": 0, "plays": 0, "completed": 0}))
    return out


def matches(agg_path, build="release", days=90):
    db = _open(agg_path)
    if db is None:
        return _empty(build, days)
    since = _since(days)
    result = {
        "build": build, "days": days, "built_ms": _built_ms(db),
        "per_day": _every_day(since, days, {r["day"]: dict(r) for r in db.execute(
            "SELECT day, COUNT(DISTINCT match_id) AS matches, COUNT(*) AS plays, SUM(completed) AS completed "
            "FROM plays WHERE build_kind=? AND day>=? GROUP BY day ORDER BY day", (build, since))}),
        "by_mode": [dict(r) for r in db.execute(
            "SELECT game_mode, COUNT(*) AS plays, SUM(completed) AS completed, "
            "ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct, "
            "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, "
            "ROUND(AVG(flight_time_seconds), 1) AS avg_flight_s FROM plays "
            "WHERE build_kind=? AND day>=? GROUP BY game_mode ORDER BY plays DESC", (build, since))],
        "by_vessel": [dict(r) for r in db.execute(
            "SELECT vessel_class, COUNT(*) AS plays, ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct, "
            "ROUND(100.0 * SUM(won) / NULLIF(COUNT(won), 0), 1) AS win_pct, ROUND(AVG(flight_time_seconds), 1) AS avg_flight_s "
            "FROM plays WHERE build_kind=? AND day>=? GROUP BY vessel_class ORDER BY plays DESC", (build, since))],
        "signals": [dict(r) for r in db.execute(
            "SELECT game_mode, SUM(favorited) AS favorited, SUM(unfavorited) AS unfavorited, SUM(fails) AS fails, "
            "SUM(shares) AS shares FROM mode_signals WHERE build_kind=? AND day>=? GROUP BY game_mode "
            "ORDER BY favorited - unfavorited DESC", (build, since))],
        "pause": dict(db.execute(
            "SELECT ROUND(AVG(pause_seconds), 1) AS avg_pause_s, ROUND(AVG(duration_seconds), 1) AS avg_duration_s, "
            "ROUND(100.0 * SUM(CASE WHEN pause_seconds > 30 THEN 1 ELSE 0 END) / NULLIF(COUNT(pause_seconds), 0), 1) "
            "AS paused_over_30s_pct FROM plays WHERE build_kind=? AND day>=?", (build, since)).fetchone()),
        "by_intensity": [dict(r) for r in db.execute(
            "SELECT intensity, COUNT(*) AS plays, ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct "
            "FROM plays WHERE build_kind=? AND day>=? GROUP BY intensity ORDER BY intensity", (build, since))],
    }
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
            "SELECT day, build_kind, rows, plays, rows_per_play FROM daily_plays WHERE day>=? ORDER BY day", (since,))],
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
    for r in db.execute(f"SELECT day, event, rows FROM daily_events WHERE build_kind=? AND day>=? "
                        f"AND event IN ('party_invite_sent', 'party_joined', 'friend_added')", (build, since)):
        per_day.setdefault(r["day"], {"day": r["day"], "party_invite_sent": 0, "party_joined": 0, "friend_added": 0})[r["event"]] = r["rows"]
    db.close()
    return {"build": build, "days": days, "built_ms": None, "totals": {e: get(e) for e in SOCIAL_EVENTS},
            "invite_join_pct": _pct(get("party_joined"), get("party_invite_received")),
            "friend_accept_pct": _pct(get("friend_added"), get("friend_request_received")),
            "multi_human_matches": overview(agg_path, build, days)["tiles"]["multi_human_matches"] if days in WINDOWS else None,
            "per_day": sorted(per_day.values(), key=lambda r: r["day"])}


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


def run_readonly_sql(agg_path, sql):
    text = check_sql(sql)
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
        rows = [list(r) for r in cur.fetchmany(MAX_ASK_ROWS + 1)]
    except sqlite3.DatabaseError as exc:
        raise UnsafeQuery(f"the query failed: {exc}") from None
    finally:
        db.close()
    return {"columns": columns, "rows": rows[:MAX_ASK_ROWS], "truncated": len(rows) > MAX_ASK_ROWS}
