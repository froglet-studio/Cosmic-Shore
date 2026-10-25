#!/usr/bin/env python3
"""regatta_rival_report.py - how each AI hull really raced in Regatta, read from the RIVALS in people's editor flights.

Every hand-flown Regatta race is recorded by HumanFlightRecorder (cosmic-shore.flight/3) with each rival's position and
ring count ten times a second (`rivals`: per sample, [x, y, z, rings] per rival, in `rivalHulls` order) and the course's
rings (`ringList`). So every stored flight is also a race of up to seven AIs in the real game, and this reads them:

- rings at the end and rings per minute, per hull and difficulty;
- **circling**: time spent within ORBIT_RANGE of the ring it is trying to thread, past ORBIT_GRACE, without threading it (the orbit the
  offline drive sims cannot show, `rhino_drive_sim.py` "no orbit break-off");
- **stalls**: a ring the AI spent more than STALL_SECONDS on, and which ring index it was (a course-specific mouth);
- its flying speed (from position steps; the recorder does not store rivals' speed separately);
- **what the AI was doing** (flights recorded since 2026-10-25, `rivalState`): the share of the race it was breaking off
  an orbit, translation-restricted or boosted above x1, and its boost driver's statuses by share of the race
  (`rivalBoostStatuses`; e.g. a Rhino "cruising - waiting for a straight that pays the wind-up back" all race).

It changes nothing; it is the editor measurement the Regatta AI roadmap asks for (REGATTA_AI_ROADMAP.md §3), read from
races already flown. The flights live on origin/bugledger/<user> under BugLedger/shared/reports/<user>/flights/.

    python3 Tools/Build/regatta_rival_report.py --dir <folder of flight_Regatta_*.json> [--hull scarab] [--since 2026-10-17]
    python3 Tools/Build/regatta_rival_report.py --self-test
"""
import argparse, glob, json, math, os, statistics, sys

ORBIT_RANGE = 250.0     # u from the ring centre: "near the ring it wants"
STALL_SECONDS = 8.0     # one ring taking longer than this is a stall
STUCK_SPEED = 20.0      # u/s: below this the AI is not moving
ORBIT_GRACE = 2.0       # s near the ring before it counts as circling (a pass through the zone takes about one)


def dist(a, b):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))


def rival_tracks(flight):
    """[(hull, [(t, (x, y, z), rings), ...]), ...] for every rival in the flight."""
    hulls = flight.get("rivalHulls") or []
    rows = flight.get("rivals") or []
    tcol = flight["columns"].index("t")
    times = [s[tcol] for s in flight["samples"]]
    out = []
    for j, h in enumerate(hulls):
        tr = []
        for t, r in zip(times, rows):
            if len(r) >= 4 * j + 4:
                tr.append((t, (r[4 * j], r[4 * j + 1], r[4 * j + 2]), int(r[4 * j + 3])))
        out.append((h, tr))
    return out


def analyse(track, rings, target):
    """One rival's race: rings, seconds, rings/min, circling seconds, stalls [(ring index, seconds)], mean speed."""
    if len(track) < 2 or not rings:
        return None
    n = len(rings)
    circling = 0.0
    stuck = 0.0
    stalls = []
    since = track[0][0]
    last_rings = track[0][2]
    speeds = []
    near = 0.0
    for (t0, p0, k0), (t1, p1, k1) in zip(track, track[1:]):
        dt = t1 - t0
        if dt <= 0:
            continue
        v = dist(p0, p1) / dt
        speeds.append(v)
        if v < STUCK_SPEED:
            stuck += dt
        if k1 != last_rings:
            if t1 - since > STALL_SECONDS:
                stalls.append((last_rings % n, round(t1 - since, 1)))
            since, last_rings, near = t1, k1, 0.0
            continue
        if k0 >= target:
            continue
        want = rings[k0 % n]
        if dist(p1, want) <= ORBIT_RANGE and dist(p0, want) <= ORBIT_RANGE:
            near += dt
            if near > ORBIT_GRACE:
                circling += dt
        else:
            near = 0.0
    end_t = track[-1][0]
    if track[-1][2] < target and end_t - since > STALL_SECONDS:
        stalls.append((track[-1][2] % n, round(end_t - since, 1)))
    secs = end_t - track[0][0]
    k = track[-1][2]
    return {"rings": k, "seconds": round(secs, 1), "rpm": round(60 * k / max(secs, 1), 1),
            "circling": round(circling, 1), "stuck": round(stuck, 1), "stalls": stalls,
            "speed": round(statistics.median(speeds), 0) if speeds else 0.0}


def rival_states(flight):
    """{rival index: summary} from `rivalState` (HumanFlightRecorder.RecordRivalState), or {} for an older flight: the share
    of samples breaking off, restricted and boosted (BoostMultiplier > 1.01), and each boost-driver status's share."""
    rows = flight.get("rivalState") or []
    cols = flight.get("rivalStateColumns") or []
    if not rows or not cols:
        return {}
    bits = flight.get("rivalStateBits") or {}
    names = flight.get("rivalBoostStatuses") or []
    w = len(cols)
    si, bi, ti = cols.index("state"), cols.index("boost"), cols.index("boostStatus")
    acc = {}
    for row in rows:
        for j in range(len(row) // w):
            v = row[j * w:(j + 1) * w]
            a = acc.setdefault(j, {"n": 0, "breakingOff": 0, "restricted": 0, "boosted": 0, "statuses": {}})
            a["n"] += 1
            st = int(v[si])
            a["breakingOff"] += bool(st & bits.get("breakingOff", 128))
            a["restricted"] += bool(st & bits.get("restricted", 256))
            a["boosted"] += v[bi] > 1.01
            k = int(v[ti])
            label = names[k] if 0 <= k < len(names) else "(no AI boost driver)"
            a["statuses"][label] = a["statuses"].get(label, 0) + 1
    out = {}
    for j, a in acc.items():
        n = max(1, a["n"])
        out[j] = {"breakingOff": a["breakingOff"] / n, "restricted": a["restricted"] / n, "boosted": a["boosted"] / n,
                  "statuses": sorted(((c / n, s) for s, c in a["statuses"].items()), reverse=True)}
    return out


def load(paths, since=None):
    out = []
    for p in paths:
        try:
            with open(p, encoding="utf-8") as fh:
                f = json.load(fh)
        except (OSError, ValueError):
            continue
        if f.get("mode") != "Regatta" or not f.get("ringList"):
            continue
        if since and str(f.get("date", "")) < since:
            continue
        cols = f.get("ringColumns") or ["index", "x", "y", "z"]
        xi, yi, zi = cols.index("x"), cols.index("y"), cols.index("z")
        rings = [(r[xi], r[yi], r[zi]) for r in f["ringList"]]
        target = int(f.get("raceTarget") or 3 * len(rings))
        states = rival_states(f)
        for j, (h, tr) in enumerate(rival_tracks(f)):
            a = analyse(tr, rings, target)
            if a:
                a.update(hull=h.lower(), difficulty=f.get("rivalDifficulty") or "?", intensity=f.get("intensity"),
                         target=target, file=os.path.basename(p), date=str(f.get("date", ""))[:10],
                         state=states.get(j))
                out.append(a)
    return out


def report(races, hull=None):
    by = {}
    for r in races:
        if hull and r["hull"] != hull:
            continue
        by.setdefault(r["hull"], []).append(r)
    print(f"{'hull':10} {'races':>5} {'rings':>6} {'r/min':>6} {'u/s':>5} {'circling':>9} {'stuck':>6} {'finished':>8}  "
          f"worst rings (index: stalls)")
    rank = sorted(by.items(), key=lambda kv: -statistics.median(x["rpm"] for x in kv[1]))
    for h, rs in rank:
        med = lambda key: statistics.median(x[key] for x in rs)
        fin = sum(1 for x in rs if x["rings"] >= x["target"])
        share = sum(x["circling"] for x in rs) / max(1, sum(x["seconds"] for x in rs))
        stuck = sum(x["stuck"] for x in rs) / max(1, sum(x["seconds"] for x in rs))
        stall_count = {}
        for x in rs:
            for i, s in x["stalls"]:
                stall_count[i] = stall_count.get(i, 0) + 1
        worst = ", ".join(f"{i}: {c}" for i, c in sorted(stall_count.items(), key=lambda kv: -kv[1])[:4])
        print(f"{h:10} {len(rs):5d} {med('rings'):6.0f} {med('rpm'):6.1f} {med('speed'):5.0f} {share:8.0%} "
              f"{stuck:6.0%} {fin:4d}/{len(rs):<3d}  {worst}")
    if hull:
        print()
        for x in sorted(by.get(hull, []), key=lambda x: x["date"]):
            print(f"  {x['date']} I{x['intensity']} {x['difficulty']:6} rings {x['rings']:2d}/{x['target']} in "
                  f"{x['seconds']:5.1f} s, {x['speed']:4.0f} u/s, circling {x['circling']:5.1f} s, stuck {x['stuck']:5.1f} s, "
                  f"stalls {x['stalls']}  {x['file']}")
            st = x.get("state")
            if st:
                print(f"      breaking off {st['breakingOff']:.0%}, restricted {st['restricted']:.0%}, boosted {st['boosted']:.0%}; "
                      + "; ".join(f"{share:.0%} {label}" for share, label in st["statuses"][:3]))


def self_test():
    rings = [(0, 0, 0), (1000, 0, 0)]
    # a rival that threads ring 0 at t=1, then circles ring 1 for 20 s without threading it
    tr = [(0.0, (-500, 0, 0), 0), (1.0, (0, 0, 0), 1)]
    for i in range(1, 201):
        a = i * 0.3
        tr.append((1.0 + i * 0.1, (1000 + 150 * math.cos(a), 150 * math.sin(a), 0), 1))
    a = analyse(tr, rings, 6)
    bad = []
    if a["rings"] != 1: bad.append("rings")
    if not 17 <= a["circling"] <= 18.1: bad.append(f"circling {a['circling']}")
    if not a["stalls"] or a["stalls"][-1][0] != 1: bad.append(f"stall {a['stalls']}")
    straight = [(i * 0.1, (i * 30.0, 0, 0), i // 10) for i in range(100)]
    b = analyse(straight, [(x * 300.0, 0, 0) for x in range(10)], 9)
    if b["circling"] > 0 or b["stalls"]: bad.append("a clean straight run counted circling or a stall")
    if abs(b["speed"] - 300) > 1: bad.append(f"speed {b['speed']}")
    # rivalState: one rival, 4 samples - 2 waiting (status 0), 1 ramping x3 (status 1), 1 breaking off with no driver status
    fl = {"rivalStateColumns": ["state", "boost", "boostStatus", "phase", "courseNose"],
          "rivalStateBits": {"ai": 64, "breakingOff": 128, "restricted": 256},
          "rivalBoostStatuses": ["Rhino: cruising", "Rhino: ramping (# u/s)"],
          "rivalState": [[64, 1, 0, 1, 0], [64, 1, 0, 1, 0], [65, 3, 1, 1, 2], [192, 1, -1, 1, 40]]}
    rs = rival_states(fl).get(0)
    if not rs or rs["breakingOff"] != 0.25 or rs["boosted"] != 0.25 or rs["statuses"][0] != (0.5, "Rhino: cruising"):
        bad.append(f"rivalState summary {rs}")
    if rival_states({}) != {}: bad.append("an older flight without rivalState must read as none")
    print("regatta_rival_report self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir")
    ap.add_argument("--hull")
    ap.add_argument("--since")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        sys.exit(self_test())
    if not a.dir:
        ap.error("--dir is required")
    report(load(sorted(glob.glob(os.path.join(a.dir, "flight_Regatta_*.json"))), a.since), a.hull and a.hull.lower())
