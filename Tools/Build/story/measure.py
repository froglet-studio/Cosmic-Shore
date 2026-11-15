#!/usr/bin/env python3
"""Measure what players come back to and where they have fun, from the Froglet Analytics aggregates.

The first stage of the daily story (Docs/StoryFlywheel/README.md). Reads only `aggregates.db` (no player ids), either
a copy of the file or through the analytics server's guarded SQL endpoint, and writes `measure.json`: one row per
activity (a game mode, a vessel, the Toybox, a Toybox vessel) with the evidence a story may quote.

    python3 Tools/Build/story/measure.py --db aggregates.db --out measure.json [--days 28] [--build '*']
    FROGLET_ANALYTICS_KEY=... python3 Tools/Build/story/measure.py --api https://analytics.<vm> --out measure.json
    python3 Tools/Build/story/measure.py --self-test

The signals, and why each is one:
- came back (activity_returns): players who did it on 2+ distinct days. The strongest sign of fun we can count:
  nobody returns to a chore. A week apart is stronger still.
- rematch (plays.rematched): started another match within 10 minutes of finishing one. "One more go".
- finished vs abandoned (plays.completed / abandoned): left mid-match is the opposite signal.
- time (plays.flight_time_seconds, freestyle_vessels.seconds): where the hours went.

`pull` ranks activities: the smoothed come-back rate (a prior of 1 in 4 so one player is not 100 %), times a
confidence that grows with the number of players, plus small rematch and finish terms. It is a ranking for picking a
story, not a KPI; every number a story says on screen comes from the raw counts beside it, never from `pull`.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import math
import os
import sqlite3
import sys
import urllib.request

SCHEMA = "cosmic-shore.story-measure/1"
PRIOR_RETURN, PRIOR_WEIGHT = 0.25, 4.0


class Source:
    """Runs read-only SQL against aggregates.db: a local copy, or the server's /v1/query (admin key)."""

    def __init__(self, db=None, api=None, key=None):
        self.db, self.api, self.key = db, api.rstrip("/") if api else None, key
        self.label = db or api

    def q(self, sql, args=()):
        if self.db:
            con = sqlite3.connect(f"file:{self.db}?mode=ro", uri=True)
            try:
                return [list(r) for r in con.execute(sql, args)]
            except sqlite3.OperationalError as e:
                if "no such table" in str(e):
                    return []
                raise
            finally:
                con.close()
        for a in args:  # the endpoint takes SQL text only: inline the few literals this file uses
            sql = sql.replace("?", "'" + str(a).replace("'", "''") + "'", 1)
        req = urllib.request.Request(self.api + "/v1/query", data=json.dumps({"sql": sql}).encode(),
                                     headers={"Authorization": "Bearer " + (self.key or ""),
                                              "Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.load(r)["rows"]


def _since(days, today=None):
    today = today or dt.date.today()
    return (today - dt.timedelta(days=days - 1)).isoformat()


def measure(src: Source, days=28, build="*", today=None):
    since = _since(days, today)
    half = _since(days // 2, today)
    plays_build = "build_kind <> 'editor'" if build == "*" else "build_kind = ?"
    pargs = () if build == "*" else (build,)

    acts = {}

    def act(kind, name):
        return acts.setdefault((kind, name), {"kind": kind, "name": name})

    for kind, name, players, two, three, week, uses, first, last in src.q(
            "SELECT kind, name, players, players_2_days, players_3_days, players_week_apart, uses, first_day, last_day "
            "FROM activity_returns WHERE build_kind = ?", (build,)):
        a = act(kind, name)
        a.update(players=players, came_back=two, came_back_3=three, week_apart=week, uses=uses,
                 first_day=first, last_day=last)

    for col, kind in (("game_mode", "mode"), ("vessel_class", "vessel")):
        for name, plays, done, rem, quit_, tracked, hours, multi, recent, earlier in src.q(
                f"SELECT {col}, COUNT(*), SUM(completed), SUM(COALESCE(rematched, 0)), SUM(abandoned), "
                f"SUM(quit_tracked), SUM(COALESCE(flight_time_seconds, 0)) / 3600.0, SUM(humans >= 2), "
                f"SUM(day >= '{half}'), SUM(day < '{half}') "
                f"FROM plays WHERE day >= '{since}' AND {plays_build} GROUP BY 1", pargs):
            a = act(kind, str(name or "unknown"))
            a.update(window_plays=plays, finished=done or 0, rematched=rem or 0, abandoned=quit_ or 0,
                     quit_tracked=tracked or 0, hours=round(hours or 0, 2), with_friends=multi or 0,
                     recent_plays=recent or 0, earlier_plays=earlier or 0)

    for vessel, entries, seconds, recent in src.q(
            f"SELECT vessel_class, SUM(entries), SUM(seconds), SUM(CASE WHEN day >= '{half}' THEN entries ELSE 0 END) "
            f"FROM freestyle_vessels WHERE day >= '{since}' AND {plays_build} GROUP BY 1", pargs):
        a = act("toybox_vessel", str(vessel))
        a.update(window_entries=entries or 0, hours=round((seconds or 0) / 3600.0, 2), recent_entries=recent or 0)
        t = act("toybox", "Toybox")
        t["window_entries"] = t.get("window_entries", 0) + (entries or 0)
        t["hours"] = round(t.get("hours", 0) + (seconds or 0) / 3600.0, 2)

    sessions = src.q(f"SELECT COUNT(*), SUM(seconds) / 3600.0 FROM sessions WHERE day >= '{since}' AND {plays_build}",
                     pargs)
    players = src.q("SELECT players FROM players_in_window WHERE build_kind = ? AND days = ?",
                    (build, 30 if days > 14 else 14 if days > 7 else 7))

    rows = []
    for a in acts.values():
        if a["name"] in ("unknown", "None", "") or not a.get("players") and not a.get("window_plays"):
            continue
        n, back = a.get("players", 0), a.get("came_back", 0)
        rate = (back + PRIOR_RETURN * PRIOR_WEIGHT) / (n + PRIOR_WEIGHT)
        confidence = 1 - math.exp(-n / 5.0)
        finished, plays = a.get("finished", 0), a.get("window_plays", 0)
        rematch = a.get("rematched", 0) / finished if finished else 0.0
        quit_rate = a.get("abandoned", 0) / a["quit_tracked"] if a.get("quit_tracked") else 0.0
        a["return_rate"] = round(back / n, 3) if n else None
        a["rematch_rate"] = round(rematch, 3) if finished else None
        a["quit_rate"] = round(quit_rate, 3) if a.get("quit_tracked") else None
        a["trend"] = _trend(a)
        a["pull"] = round(rate * confidence + 0.25 * rematch * min(1.0, finished / 10) - 0.2 * quit_rate, 4)
        rows.append(a)
    rows.sort(key=lambda r: -r["pull"])
    return {
        "schema": SCHEMA, "made_at": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat(),
        "source": src.label, "build": build, "window_days": days, "since": since,
        "totals": {"players": players[0][0] if players else None,
                   "sessions": sessions[0][0] if sessions else None,
                   "session_hours": round(sessions[0][1] or 0, 1) if sessions else None,
                   "match_plays": sum(r.get("window_plays", 0) for r in rows if r["kind"] == "mode"),
                   "toybox_entries": next((r.get("window_entries", 0) for r in rows if r["kind"] == "toybox"), 0)},
        "activities": rows,
    }


def _trend(a):
    recent = a.get("recent_plays", a.get("recent_entries"))
    earlier = a.get("earlier_plays")
    if recent is None or earlier is None or recent + earlier < 4:
        return None
    return "rising" if recent > 1.5 * earlier else "falling" if earlier > 1.5 * recent else "steady"


def top(measured, kind=None, n=5, min_players=2):
    return [a for a in measured["activities"]
            if (kind is None or a["kind"] == kind) and a.get("players", 0) >= min_players][:n]


def report(m):
    t = m["totals"]
    lines = [f"{m['window_days']} days since {m['since']} ({m['source']}, build {m['build']}): "
             f"{t['players']} players, {t['match_plays']} matches, {t['toybox_entries']} Toybox entries"]
    for a in m["activities"][:15]:
        lines.append(f"  {a['pull']:.3f}  {a['kind']:13s} {a['name']:22s} players {a.get('players', 0):3d}  "
                     f"came back {a.get('came_back', 0):3d}  rematch {a.get('rematch_rate')}  quit {a.get('quit_rate')}  "
                     f"{a.get('hours', 0)} h  {a.get('trend') or ''}")
    return "\n".join(lines)


# ---- self-test: a tiny aggregates.db with the server's own schema ----

def _fixture(path):
    here = os.path.dirname(os.path.abspath(__file__))
    sys.path.insert(0, os.path.join(here, "..", "..", "Analytics", "server"))
    import analytics_store as S  # noqa: E402
    con = sqlite3.connect(path)
    con.executescript(S.AGG_SCHEMA)
    today = dt.date.today()
    d = lambda n: (today - dt.timedelta(days=n)).isoformat()
    con.executemany("INSERT INTO activity_returns VALUES(?,?,?,?,?,?,?,?,?,?)", [
        ("*", "mode", "SkimRace", 12, 7, 4, 2, 60, d(20), d(0)),
        ("*", "mode", "Regatta", 30, 4, 1, 0, 50, d(20), d(0)),
        ("*", "mode", "Joust", 1, 1, 1, 1, 9, d(20), d(0)),         # one fan is not a story
        ("*", "toybox", "Toybox", 20, 11, 6, 3, 80, d(20), d(0)),
    ])
    cols = [c[1] for c in con.execute("PRAGMA table_info(plays)")]
    def play(mode, day, done, rem, quit_):
        row = dict.fromkeys(cols)
        row.update(game_mode=mode, day=d(day), build_kind="release", completed=done, rematched=rem, abandoned=quit_,
                   quit_tracked=1, flight_time_seconds=120.0, humans=1)
        return [row[c] for c in cols]
    rows = [play("SkimRace", n % 20, 1, 1 if n % 2 else 0, 0) for n in range(40)]
    rows += [play("Regatta", n % 20, 0 if n % 3 == 0 else 1, 0, 1 if n % 3 == 0 else 0) for n in range(40)]
    rows += [play("Regatta", 1, 1, 0, 0, ) for _ in range(3)]
    con.executemany(f"INSERT INTO plays VALUES({','.join('?' * len(cols))})", rows)
    con.commit()
    con.close()


def self_test() -> int:
    import tempfile
    tmp = tempfile.mkdtemp()
    db = os.path.join(tmp, "aggregates.db")
    _fixture(db)
    m = measure(Source(db=db), days=28)
    names = [a["name"] for a in m["activities"]]
    assert names.index("SkimRace") < names.index("Regatta"), names       # came back more, rematches, no quits
    assert names.index("Toybox") < names.index("Regatta"), names
    assert names[-1] in ("Joust", "Regatta"), names
    sk = next(a for a in m["activities"] if a["name"] == "SkimRace")
    assert sk["return_rate"] == round(7 / 12, 3) and sk["rematch_rate"] == 0.5 and sk["quit_rate"] == 0.0, sk
    assert not any(a["name"] == "Joust" for a in top(m)), "one player never makes a beat"
    print("measure: self-test OK")
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--db")
    ap.add_argument("--api")
    ap.add_argument("--days", type=int, default=28)
    ap.add_argument("--build", default="*", help="'*' every build but the editor's plays; or release, development, web")
    ap.add_argument("--out")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if not (a.db or a.api):
        ap.error("--db or --api")
    m = measure(Source(a.db, a.api, os.environ.get("FROGLET_ANALYTICS_KEY")), a.days, a.build)
    if a.out:
        json.dump(m, open(a.out, "w", encoding="utf-8"), indent=1)
    print(report(m))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
