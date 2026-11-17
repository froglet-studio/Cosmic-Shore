"""The Funnel builder and Release compare (Frogma Rev 66 A, Docs/MENU_REDESIGN_B.md section 142; picked by Shombith,
2026-11-17). Both are worked out per player over events.db when they are asked for, and return counts only: no player
id, display name or country of one player leaves this module. Readers may ask them.

Funnel: any events or FTUE steps in order, within 1 / 7 / 30 days of the step before (or of the first step, in any
order), for the players whose first step falls in a range; conversion split by platform, device or country, the time
between steps, and where the ones who dropped at the biggest drop went instead.

Release compare: app_version A against app_version B over the first 7 days of each (or the same last 7 calendar days):
finished, exceptions per 100 sessions, launch to menu (first menu of each app run only), D1 / D7 of each version's new
players, rematch within 10 minutes, rows per play, matches per player, each with a read (better, worse, same, or too
early when either side has under 30 in its base); D1 by platform, who is on which version, finished by mode,
exceptions by day, the launch histogram, the return curve and the top exceptions (type and frame, never text)."""
import json
from datetime import date, timedelta

import analytics_store as S

SECOND_DAY = "second_day"                 # a pseudo-step: any event on a later day than the player's first step
SPECIAL_STEPS = {SECOND_DAY: "A second day with events"}
WITHIN_DAYS = (1, 7, 30)
ORDERS = ("strict", "any")
SPLITS = ("none", "platform", "device", "country")
MAX_STEPS = 8
MIN_STEPS = 2
TOO_EARLY = 30
# Where the ones who dropped went: the app's own bookkeeping is not somewhere a player went
NOT_A_CHOICE = {"session_started", "menu_ready", "game_first_launched", "crystal_balance_snapshot", "cloud_save_failed",
                "client_exception"}                            # a read needs at least this many in each side's base
RANGE_MAX_DAYS = 400
DAY_MS = 86400 * 1000
REMATCH_MS = 10 * 60 * 1000
LAUNCH_BUCKETS = ((0, 4), (4, 6), (6, 8), (8, 10), (10, 14), (14, 20), (20, None))
FAMILY_OF_DEVICE = {"iphone": "iOS", "ipad": "iOS", "ios": "iOS", "android_phone": "Android",
                    "android_tablet": "Android", "android": "Android", "web_desktop": "Web", "web_mobile": "Web",
                    "web": "Web", "windows": "Desktop", "mac": "Desktop", "linux": "Desktop"}
COLUMNS = "distinct_id, ts_ms, day, event, platform, device, country, app_version, near_dup, props"


def _today():
    return S.day_of(S.now_ms())


def _shift(day, n):
    return (date.fromisoformat(day) + timedelta(days=n)).isoformat()


def _pct(part, whole):
    return round(100.0 * part / whole, 1) if whole else None


def _percentile(values, q):
    if not values:
        return None
    values = sorted(values)
    at = q * (len(values) - 1)                 # linear between the two nearest, so the median of two is their mean
    lo = int(at)
    hi = min(lo + 1, len(values) - 1)
    return round(values[lo] + (values[hi] - values[lo]) * (at - lo), 2)


def _truthy(value):
    return value in (True, 1, "1", "true", "True")


def _props(row):
    try:
        return json.loads(row["props"]) if row["props"] else {}
    except ValueError:
        return {}


def _filters(build, devices, since=None, until=None):
    where, args = ["1=1"], []
    if since:
        where.append("day >= ?")
        args.append(since)
    if until:
        where.append("day <= ?")
        args.append(until)
    if build:
        where.append("build_kind = ?")
        args.append(build)
    if devices:
        where.append(f"device IN ({','.join('?' * len(devices))})")
        args += list(devices)
    return " AND ".join(where), args


def _by_player(rows):
    """Rows ordered by player then time, grouped: [(player, [rows])]."""
    out, current, bucket = [], None, []
    for r in rows:
        if r["distinct_id"] != current:
            if bucket:
                out.append((current, bucket))
            current, bucket = r["distinct_id"], []
        bucket.append(r)
    if bucket:
        out.append((current, bucket))
    return out


# ---------------------------------------------------------------- the funnel

def _parse_steps(steps, allowed):
    if not isinstance(steps, list) or not MIN_STEPS <= len(steps) <= MAX_STEPS:
        raise S.Invalid(f"steps must be a list of {MIN_STEPS} to {MAX_STEPS} steps")
    out = []
    for i, step in enumerate(steps):
        if isinstance(step, str):
            step = {"event": step}
        if not isinstance(step, dict) or not isinstance(step.get("event"), str):
            raise S.Invalid("each step is {\"event\": <name>, \"step_id\"?: <FTUE node id>, \"label\"?: <text>}")
        event = step["event"]
        if event not in allowed and event not in SPECIAL_STEPS:
            raise S.Invalid(f"step {i + 1}: no event named {event!r}")
        if event == SECOND_DAY and i == 0:
            raise S.Invalid("a second day cannot be the first step")
        step_id = step.get("step_id")
        if step_id is not None and (not isinstance(step_id, str) or event != "ftue_step_completed" or len(step_id) > 80):
            raise S.Invalid(f"step {i + 1}: step_id is an FTUE node id, only on ftue_step_completed")
        label = step.get("label")
        label = S._label(label) if isinstance(label, str) and label.strip() else \
            SPECIAL_STEPS.get(event) or (f"{event}: {step_id}" if step_id else event)
        out.append({"event": event, "step_id": step_id, "label": label})
    return out


def _range(body):
    today = _today()
    until = S.iso_day(body["until"], "until") if body.get("until") else today
    since = S.iso_day(body["since"], "since") if body.get("since") else _shift(until, -29)
    if since > until:
        raise S.Invalid("since is after until")
    if (date.fromisoformat(until) - date.fromisoformat(since)).days >= RANGE_MAX_DAYS:
        raise S.Invalid(f"the range is at most {RANGE_MAX_DAYS} days")
    return since, until


def funnel(db, body, allowed, build=None, devices=None):
    """POST /v1/funnel. `db` is a read connection to events.db with sqlite3.Row rows."""
    if not isinstance(body, dict):
        raise S.Invalid("send {\"steps\": [...], \"within_days\"?, \"order\"?, \"since\"?, \"until\"?, \"by\"?}")
    steps = _parse_steps(body.get("steps"), allowed)
    within = body.get("within_days", 7)
    if within not in WITHIN_DAYS:
        raise S.Invalid(f"within_days must be one of {list(WITHIN_DAYS)}")
    order = body.get("order") or "strict"
    if order not in ORDERS:
        raise S.Invalid(f"order must be one of {list(ORDERS)}")
    split = body.get("by") or "platform"
    if split not in SPLITS:
        raise S.Invalid(f"by must be one of {list(SPLITS)}")
    since, until = _range(body)
    reach_days = within * (len(steps) - 1 if order == "strict" else 1) + 1
    where, args = _filters(build, devices, since, _shift(until, reach_days))
    rows = db.execute(f"SELECT {COLUMNS} FROM events WHERE {where} AND near_dup = 0 AND event <> '$identify' "
                      "ORDER BY distinct_id, ts_ms, id", args).fetchall()
    window = within * DAY_MS
    n = len(steps)

    def hits(step, row, start_day):
        if step["event"] == SECOND_DAY:
            return row["day"] > start_day
        if row["event"] != step["event"]:
            return False
        return step["step_id"] is None or _props(row).get("step_id") == step["step_id"]

    reached = [0] * n
    groups = {}
    gaps = [[] for _ in range(n)]
    dropped_next = {}                     # per step k: {next event: players} for those who reached k and not k + 1
    for _, events in _by_player(rows):
        start = next((i for i, r in enumerate(events) if since <= r["day"] <= until and hits(steps[0], r, "")), None)
        if start is None:
            continue
        s_row = events[start]
        times, at = [s_row["ts_ms"]], [start]
        for k in range(1, n):
            lo = at[-1] if order == "strict" else start
            ref = times[-1] if order == "strict" else s_row["ts_ms"]
            found = next((i for i in range(lo + 1, len(events))
                          if events[i]["ts_ms"] - ref <= window and hits(steps[k], events[i], s_row["day"])), None)
            if found is None:
                break
            at.append(found)
            times.append(events[found]["ts_ms"])
        depth = len(at)
        for k in range(depth):
            reached[k] += 1
            if k:
                gaps[k].append((times[k] - (times[k - 1] if order == "strict" else times[0])) / 1000.0)
        key = "all" if split == "none" else (s_row[split] or "unknown")
        g = groups.setdefault(key, [0] * n)
        for k in range(depth):
            g[k] += 1
        if depth < n:
            last = at[-1]
            nxt = next((events[i]["event"] for i in range(last + 1, len(events))
                        if events[i]["event"] != events[last]["event"] and events[i]["event"] not in NOT_A_CHOICE), None)
            bucket = dropped_next.setdefault(depth - 1, {})
            label = nxt or "No later event"
            bucket[label] = bucket.get(label, 0) + 1

    starters = reached[0]
    out_steps = []
    for k, step in enumerate(steps):
        prev = reached[k - 1] if k else None
        out_steps.append({"label": step["label"], "event": step["event"], "step_id": step["step_id"],
                          "players": reached[k], "pct_of_start": _pct(reached[k], starters),
                          "drop_pct": None if prev is None else _pct(prev - reached[k], prev)})
    drops = [(reached[k] - reached[k + 1], k) for k in range(n - 1) if reached[k]]
    worst = max(drops)[1] if drops and max(drops)[0] > 0 else None
    instead = None
    if worst is not None:
        counts = sorted(dropped_next.get(worst, {}).items(), key=lambda kv: (-kv[1], kv[0]))
        instead = {"after_step": worst + 1, "label": steps[worst]["label"],
                   "players": reached[worst] - reached[worst + 1],
                   "next": [{"event": e, "players": c} for e, c in counts[:8]]}
    ranked = sorted(groups.items(), key=lambda kv: (-kv[1][0], kv[0]))
    return {"steps": out_steps, "starters": starters, "within_days": within, "order": order, "since": since,
            "until": until, "by": split,
            "groups": [{"group": key, "players": counts, "pct_of_start": [_pct(c, counts[0]) for c in counts]}
                       for key, counts in ranked],
            "times": [{"from": steps[k - 1]["label"] if order == "strict" else steps[0]["label"],
                       "to": steps[k]["label"], "players": len(gaps[k]),
                       "median_s": _percentile(gaps[k], 0.5), "p75_s": _percentile(gaps[k], 0.75)}
                      for k in range(1, n)],
            "instead": instead}


def funnel_steps(db, allowed, build=None, devices=None):
    """GET /v1/funnel/steps: what a step can be. Every event the schema takes, with players who sent it in the last
    90 days; every FTUE node seen, by its id and latest name; and the second-day pseudo-step."""
    where, args = _filters(build, devices, _shift(_today(), -89))
    seen = dict(db.execute(f"SELECT event, COUNT(DISTINCT distinct_id) FROM events WHERE {where} AND near_dup = 0 "
                           "GROUP BY event", args).fetchall())
    nodes = {}
    for r in db.execute(f"SELECT distinct_id, ts_ms, props FROM events WHERE {where} AND near_dup = 0 AND "
                        "event = 'ftue_step_completed' ORDER BY ts_ms", args):
        p = _props(r)
        sid = p.get("step_id")
        if not isinstance(sid, str):
            continue
        node = nodes.setdefault(sid, {"step_id": sid, "name": sid, "phase_index": None, "who": set()})
        node["name"] = S._label(p.get("step_name") or sid)
        node["phase_index"] = S.as_int(p.get("phase_index"))
        node["who"].add(r["distinct_id"])
    events = [{"event": e, "players": seen.get(e, 0)} for e in sorted(allowed) if e != "$identify"]
    events.sort(key=lambda e: (-e["players"], e["event"]))
    return {"events": events, "special": [{"event": k, "label": v} for k, v in SPECIAL_STEPS.items()],
            "ftue_nodes": sorted(({"step_id": n["step_id"], "name": n["name"], "phase_index": n["phase_index"],
                                   "players": len(n["who"])} for n in nodes.values()),
                                 key=lambda n: (n["phase_index"] if n["phase_index"] is not None else 999,
                                                -n["players"], n["step_id"]))}


# ---------------------------------------------------------------- release compare

MEASURES = (  # key, label, higher is better
    ("finished_pct", "Finished", True),
    ("exceptions_per_100_sessions", "Exceptions per 100 sessions", False),
    ("launch_p50_s", "Launch to menu, p50", False),
    ("launch_p90_s", "Launch to menu, p90", False),
    ("d1_pct", "Back next day (D1)", True),
    ("d7_pct", "Back within 7 days (D7)", True),
    ("rematch_pct", "Rematch within 10 min", True),
    ("rows_per_play", "Rows per play", None),        # 1.00 is healthy; neither side is better for being higher
    ("matches_per_player", "Matches per player", True),
)


def _versions(rows, today):
    by = {}
    first_of = {}
    for player, events in _by_player(rows):
        first_of[player] = (events[0]["day"], events[0]["app_version"])
        for r in events:
            v = r["app_version"]
            if not v:
                continue
            e = by.setdefault(v, {"version": v, "first_day": r["day"], "last_day": r["day"], "who": set(),
                                  "new_players": 0})
            e["first_day"] = min(e["first_day"], r["day"])
            e["last_day"] = max(e["last_day"], r["day"])
            e["who"].add(player)
    for day, v in first_of.values():
        if v in by:
            by[v]["new_players"] += 1
    out = [{"version": e["version"], "first_day": e["first_day"], "last_day": e["last_day"],
            "players": len(e["who"]), "new_players": e["new_players"]} for e in by.values()]
    out.sort(key=lambda e: (e["first_day"], e["version"]), reverse=True)
    return out, first_of


def _side(per_player, first_of, version, w0, w1, today):
    pairs, raw_starts, completed_pairs, players, started_players = set(), 0, set(), set(), set()
    sessions, exceptions = 0, 0
    exc_by_day = [[0, 0] for _ in range((date.fromisoformat(w1) - date.fromisoformat(w0)).days + 1)]
    launches, rematch_base, rematches = [], 0, 0
    modes = {}
    top = {}
    for player, events in per_player:
        mine = [r for r in events if r["app_version"] == version and w0 <= r["day"] <= w1]
        if not mine:
            continue
        players.add(player)
        starts_at = []
        for r in mine:
            ev, p = r["event"], None
            if ev == "game_started":
                raw_starts += 1
                if r["near_dup"]:
                    continue
                p = _props(r)
                match = p.get("match_id") or f"{r['ts_ms']}"
                if (player, match) not in pairs:
                    pairs.add((player, match))
                    mode = S._label(p.get("game_mode") or "unknown")
                    modes.setdefault(mode, [set(), set()])[0].add((player, match))
                started_players.add(player)
                starts_at.append(r["ts_ms"])
                continue
            if r["near_dup"]:
                continue
            idx = (date.fromisoformat(r["day"]) - date.fromisoformat(w0)).days
            if ev == "game_completed":
                p = _props(r)
                completed_pairs.add((player, p.get("match_id") or f"{r['ts_ms']}"))
            elif ev == "session_started":
                sessions += 1
                exc_by_day[idx][1] += 1
            elif ev == "client_exception":
                p = _props(r)
                count = max(1, S.as_int(p.get("count")) or 1)
                exceptions += count
                exc_by_day[idx][0] += count
                sig = (S._label(p.get("exception_type") or "unknown"), S._label(p.get("top_frame") or "unknown", 120))
                top[sig] = top.get(sig, 0) + count
            elif ev == "menu_ready":
                p = _props(r)
                secs = S.as_float(p.get("seconds_since_launch"))
                if _truthy(p.get("is_first")) and secs is not None and secs >= 0:
                    launches.append(secs)
        for r in mine:
            if r["event"] == "game_completed" and not r["near_dup"]:
                rematch_base += 1
                if any(0 < t - r["ts_ms"] <= REMATCH_MS for t in starts_at):
                    rematches += 1
    for mode, sets in modes.items():
        sets[1] = {pair for pair in sets[0] if pair in completed_pairs}
    finished = len([pair for pair in pairs if pair in completed_pairs])

    # each version's new players: first event (in this build and device filter) on this version, in the window
    cohort = [(player, day) for player, (day, v) in first_of.items() if v == version and w0 <= day <= w1]
    days_of = {player: {r["day"] for r in events} for player, events in per_player}
    family_of = {player: FAMILY_OF_DEVICE.get(events[0]["device"] or "", "Other") for player, events in per_player}
    curve = []
    for n in range(0, 8):
        base = [(p, d) for p, d in cohort if _shift(d, n) <= today]
        back = sum(1 for p, d in base if n == 0 or any(x >= _shift(d, n) for x in days_of[p]))
        curve.append({"day": n, "players": len(base), "pct": _pct(back, len(base))})
    d1_base = [(p, d) for p, d in cohort if _shift(d, 1) <= today]
    d1 = sum(1 for p, d in d1_base if _shift(d, 1) in days_of[p])
    d7_base = [(p, d) for p, d in cohort if _shift(d, 7) <= today]
    d7 = sum(1 for p, d in d7_base if any(_shift(d, k) in days_of[p] for k in range(1, 8)))
    d1_family = {}
    for p, d in d1_base:
        f = d1_family.setdefault(family_of[p], [0, 0])
        f[1] += 1
        f[0] += int(_shift(d, 1) in days_of[p])
    hist = []
    for lo, hi in LAUNCH_BUCKETS:
        hist.append({"bucket": f"{lo}-{hi}" if hi else f"{lo}+",
                     "launches": sum(1 for s in launches if s >= lo and (hi is None or s < hi))})
    values = {
        "finished_pct": _pct(finished, len(pairs)),
        "exceptions_per_100_sessions": round(100.0 * exceptions / sessions, 2) if sessions else None,
        "launch_p50_s": _percentile(launches, 0.5), "launch_p90_s": _percentile(launches, 0.9),
        "d1_pct": _pct(d1, len(d1_base)), "d7_pct": _pct(d7, len(d7_base)),
        "rematch_pct": _pct(rematches, rematch_base),
        "rows_per_play": round(raw_starts / len(pairs), 2) if pairs else None,
        "matches_per_player": round(len(pairs) / len(started_players), 2) if started_players else None,
    }
    bases = {"finished_pct": len(pairs), "exceptions_per_100_sessions": sessions, "launch_p50_s": len(launches),
             "launch_p90_s": len(launches), "d1_pct": len(d1_base), "d7_pct": len(d7_base),
             "rematch_pct": rematch_base, "rows_per_play": len(pairs), "matches_per_player": len(started_players)}
    return {"version": version, "from": w0, "to": w1, "players": len(players), "new_players": len(cohort),
            "sessions": sessions, "values": values, "bases": bases,
            "d1_by_platform": {f: {"players": b, "pct": _pct(a, b)} for f, (a, b) in sorted(d1_family.items())},
            "finished_by_mode": {m: {"started": len(s[0]), "finished": len(s[1]), "pct": _pct(len(s[1]), len(s[0]))}
                                 for m, s in modes.items()},
            "exceptions_by_day": [{"day": i, "exceptions": e, "sessions": s,
                                   "per_100": round(100.0 * e / s, 2) if s else None}
                                  for i, (e, s) in enumerate(exc_by_day)],
            "launch_histogram": hist, "return_curve": curve,
            "top_exceptions": [{"exception_type": t, "top_frame": f, "count": c}
                               for (t, f), c in sorted(top.items(), key=lambda kv: (-kv[1], kv[0]))[:5]]}


def _read_of(key, better_high, a, b, base_a, base_b):
    if a is None or b is None:
        return "no data"
    if min(base_a, base_b) < TOO_EARLY:
        return "too early"
    if a == b or better_high is None:
        return "same" if a == b else "changed"
    return "better" if (b > a) == better_high else "worse"


def releases(db, a=None, b=None, span="first7", build="release", devices=None):
    """GET /v1/releases. Without a and b: the versions to pick from. With both: B read against A."""
    if span not in ("first7", "calendar"):
        raise S.Invalid("span must be first7 or calendar")
    today = _today()
    where, args = _filters(build, devices)
    rows = db.execute(f"SELECT {COLUMNS} FROM events WHERE {where} AND event <> '$identify' "
                      "ORDER BY distinct_id, ts_ms, id", args).fetchall()
    versions, first_of = _versions(rows, today)
    known = {v["version"]: v for v in versions}
    # who is on which version: each player active in the last 7 days, by the version of their latest event
    latest = {}
    for player, events in _by_player(rows):
        if events[-1]["day"] >= _shift(today, -6) and events[-1]["app_version"]:
            latest[events[-1]["app_version"]] = latest.get(events[-1]["app_version"], 0) + 1
    active = sum(latest.values())
    mix = [{"version": v, "players": n, "pct": _pct(n, active)}
           for v, n in sorted(latest.items(), key=lambda kv: (-kv[1], kv[0]))]
    page = {"versions": versions, "on_version_now": mix, "span": span, "today": today}
    if not a and not b:
        return page
    if not a or not b:
        raise S.Invalid("name two versions: ?a=<app_version>&b=<app_version>")
    for v in (a, b):
        if v not in known:
            raise S.Invalid(f"no events from app_version {v!r} in this build and device filter")
    per_player = _by_player(rows)
    sides = []
    for v in (a, b):
        if span == "calendar":
            w0, w1 = _shift(today, -6), today
        else:
            w0 = known[v]["first_day"]
            w1 = min(_shift(w0, 6), today)
        sides.append(_side(per_player, first_of, v, w0, w1, today))
    A, B = sides
    reads = []
    for key, label, high in MEASURES:
        va, vb = A["values"][key], B["values"][key]
        read = _read_of(key, high, va, vb, A["bases"][key], B["bases"][key])
        change = None if va is None or vb is None else round(vb - va, 2)
        reads.append({"measure": key, "label": label, "a": va, "b": vb, "change": change, "read": read,
                      "base_a": A["bases"][key], "base_b": B["bases"][key]})
    tally = {k: sum(1 for r in reads if r["read"] == k) for k in ("better", "worse", "too early", "same")}
    top_modes = sorted(set(A["finished_by_mode"]) | set(B["finished_by_mode"]),
                       key=lambda m: -(A["finished_by_mode"].get(m, {}).get("started", 0)
                                       + B["finished_by_mode"].get(m, {}).get("started", 0)))[:6]
    page.update({"a": A, "b": B, "reads": reads, "tally": tally, "top_modes": top_modes})
    return page
