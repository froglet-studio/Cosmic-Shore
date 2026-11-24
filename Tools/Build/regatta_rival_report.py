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

**--grid** (2026-11-06, "why each AI loses in a grid"): the same flights read race by race instead of file by file, and
deeper per rival. Two things the plain table gets wrong are fixed there:
- **a multiplayer race is recorded once per human**, from each machine (three records of one race on 2026-11-05), and
  every record lists the OTHER humans as rivals. Records of one race are merged (same circuit, same race length, the
  same rivals threading their first rings at the same moments), and a rival is an AI only when the flight says so:
  every rival when `rivalDifficulties` names as many AIs as there are rivals, else the host record's AI bit, else every
  seat but the recorders' own. A seat it cannot place is `?` and is left out.
- **per rival mechanics**: a MISSED mouth (crossing the plane of the ring it chases outside the ring's radius, from the
  ring's axis and radius in `ringList`) and the seconds until it then threads; RAIL RAMS (the `slowed` state rising - a
  prism's throttle debuff - next to a rail prism) and the share of the race slowed; the share of the race within 40 u of
  another vessel (traffic); its median split per ring; for the Urchin, which colour of rail it rode and how fast; and
  three faults read from the record: an Urchin riding a RIVAL's rail, an Urchin breaking off its own rail mid-leg, and
  an AI Squirrel whose platform autopilot (AIPilot, with a boost driver) was flying it instead of the Skim Race pilot.

    python3 Tools/Build/regatta_rival_report.py --dir <folder of flight_Regatta_*.json> [--hull scarab] [--since 2026-10-17]
    python3 Tools/Build/regatta_rival_report.py --dir <folder> --mode Switchback     # any gate race's flights (a minigame)
    python3 Tools/Build/regatta_rival_report.py --dir <folder> --grid [--hull manta] [--since 2026-11-02] [--json out.json]
    python3 Tools/Build/regatta_rival_report.py --self-test

A folder may hold several people's flights: name each `<flight>__<user>.json`, or keep the
`reports/<user>/flights/` layout, so two records of one race are known to come from two machines.
"""
import argparse, collections, glob, json, math, os, re, statistics, sys

ORBIT_RANGE = 250.0     # u from the ring centre: "near the ring it wants"
STALL_SECONDS = 8.0     # one ring taking longer than this is a stall
STUCK_SPEED = 20.0      # u/s: below this the AI is not moving
CLOSING_SHARE = 0.5     # a step that brings it this share of its length nearer the ring is an approach, not an orbit
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
        # near the ring and NOT closing on it: a slow hull flying straight in spends seconds inside ORBIT_RANGE too
        # (Breakwater's Sparrows, 37 u/s on 350 u legs: about 5 s a station), and that is not an orbit
        closing = dist(p0, want) - dist(p1, want) > CLOSING_SHARE * dist(p0, p1)
        if dist(p1, want) <= ORBIT_RANGE and dist(p0, want) <= ORBIT_RANGE and not closing:
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
    of samples breaking off, restricted and boosted (BoostMultiplier > 1.01 while the boosting or discharging bit is
    set: the Sparrow's multiplier rests at x5 whether it boosts or not), and each boost-driver status's share."""
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
            a["boosted"] += v[bi] > 1.01 and bool(st & (bits.get("boosting", 1) | bits.get("discharging", 2)))
            k = int(v[ti])
            label = names[k] if 0 <= k < len(names) else "(no AI boost driver)"
            a["statuses"][label] = a["statuses"].get(label, 0) + 1
    out = {}
    for j, a in acc.items():
        n = max(1, a["n"])
        out[j] = {"breakingOff": a["breakingOff"] / n, "restricted": a["restricted"] / n, "boosted": a["boosted"] / n,
                  "statuses": sorted(((c / n, s) for s, c in a["statuses"].items()), reverse=True)}
    return out


def rival_kites(flight, rings):
    """{rival index: summary} from `rivalKite` (HumanFlightRecorder.RecordKite: a crew vessel's kite, 2026-10-26), or {}
    for a flight without it: the share of samples with a kite seated, and per ring index chased the hull's and the kite's
    closest pass - a kite that came within the mouth and still did not thread says the miss is the navigator's."""
    rows, tracks = flight.get("rivalKite") or [], flight.get("rivals") or []
    if not rows:
        return {}
    n = len(rings)
    acc = {}
    for kr, rr in zip(rows, tracks):
        for j in range(min(len(kr) // 4, len(rr) // 4)):
            a = acc.setdefault(j, {"n": 0, "seated": 0, "closest": {}})
            a["n"] += 1
            hull, k = (rr[4 * j], rr[4 * j + 1], rr[4 * j + 2]), int(rr[4 * j + 3]) % n
            c = a["closest"].setdefault(k, [1e9, 1e9])
            c[0] = min(c[0], dist(hull, rings[k]))
            if kr[4 * j + 3] >= 0:
                a["seated"] += 1
                c[1] = min(c[1], dist((kr[4 * j], kr[4 * j + 1], kr[4 * j + 2]), rings[k]))
    return {j: {"seated": a["seated"] / max(1, a["n"]),
                "closest": {k: (round(h), None if kt >= 1e9 else round(kt)) for k, (h, kt) in sorted(a["closest"].items())}}
            for j, a in acc.items()}


def load(paths, since=None, mode="Regatta"):
    out = []
    for p in paths:
        try:
            with open(p, encoding="utf-8") as fh:
                f = json.load(fh)
        except (OSError, ValueError):
            continue
        if f.get("mode") != mode or not f.get("ringList"):
            continue
        if since and str(f.get("date", "")) < since:
            continue
        cols = f.get("ringColumns") or ["index", "x", "y", "z"]
        xi, yi, zi = cols.index("x"), cols.index("y"), cols.index("z")
        rings = [(r[xi], r[yi], r[zi]) for r in f["ringList"]]
        target = int(f.get("raceTarget") or 3 * len(rings))
        states = rival_states(f)
        kites = rival_kites(f, rings)
        for j, (h, tr) in enumerate(rival_tracks(f)):
            a = analyse(tr, rings, target)
            if a:
                a.update(hull=h.lower(), difficulty=f.get("rivalDifficulty") or "?", intensity=f.get("intensity"),
                         target=target, file=os.path.basename(p), date=str(f.get("date", ""))[:10],
                         state=states.get(j), kite=kites.get(j))
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
            kt = x.get("kite")
            if kt:
                stalled = {i for i, _ in x["stalls"]}
                print(f"      kite seated {kt['seated']:.0%}; closest pass to each stalled ring (hull / kite, u): "
                      + (", ".join(f"ring {i}: {kt['closest'][i][0]} / {kt['closest'][i][1]}" for i in sorted(stalled)
                                   if i in kt["closest"]) or "no stalls"))


# ----------------------------------------------------------------------------------------------------------- --grid
NEAR_SHIP = 40.0        # u: another vessel this close is traffic
RAM_RAIL = 25.0         # u: a slow-down that starts this near a rail prism is a rail ram
MISS_REACH = 4.0        # x ring radius: a plane crossing farther out than this is not an attempt at the mouth
THREAD_LAG = 3          # samples: the ring count can land a sample or two after the crossing
RAIL_CELL = 40.0
SLOWED, ATTACHED, AI_BIT, BREAKING_OFF = 8, 16, 64, 128


class RailIndex:
    """Nearest rail prism to a point, by a voxel hash over the flight's `rails` (one list of prisms per colour)."""

    def __init__(self, rails):
        self.cells = collections.defaultdict(list)
        for r in rails or []:
            for p in r.get("prisms") or []:
                self.cells[(int(p[0] // RAIL_CELL), int(p[1] // RAIL_CELL), int(p[2] // RAIL_CELL))].append(
                    (p[0], p[1], p[2], r.get("domain", "?")))

    def nearest(self, p, reach=2):
        """(distance, colour) of the nearest prism within reach cells, or (inf, None)."""
        cx, cy, cz = int(p[0] // RAIL_CELL), int(p[1] // RAIL_CELL), int(p[2] // RAIL_CELL)
        best, dom = float("inf"), None
        for dx in range(-reach, reach + 1):
            for dy in range(-reach, reach + 1):
                for dz in range(-reach, reach + 1):
                    for q in self.cells.get((cx + dx, cy + dy, cz + dz), ()):
                        d = (q[0] - p[0]) ** 2 + (q[1] - p[1]) ** 2 + (q[2] - p[2]) ** 2
                        if d < best:
                            best, dom = d, q[3]
        return math.sqrt(best), dom


def ring_geometry(flight):
    """[(centre, axis or None, radius or None)] from `ringList` (axis and radius since the 2026-10 recorder)."""
    cols = flight.get("ringColumns") or ["index", "x", "y", "z"]
    out = []
    for r in flight.get("ringList") or []:
        c = tuple(r[cols.index(k)] for k in ("x", "y", "z"))
        a = tuple(r[cols.index(k)] for k in ("ax", "ay", "az")) if "ax" in cols else None
        out.append((c, a, r[cols.index("radius")] if "radius" in cols else None))
    return out


def plane_crossing(p0, p1, centre, axis):
    """(lateral offset from the ring centre, forward?) where the step p0 -> p1 crosses the ring's plane, else None."""
    s0 = sum((p0[q] - centre[q]) * axis[q] for q in range(3))
    s1 = sum((p1[q] - centre[q]) * axis[q] for q in range(3))
    if not (s0 < 0 <= s1 or s1 < 0 <= s0) or s0 == s1:
        return None
    u = s0 / (s0 - s1)
    x = [p0[q] + u * (p1[q] - p0[q]) for q in range(3)]
    s = sum((x[q] - centre[q]) * axis[q] for q in range(3))
    return math.sqrt(max(0.0, sum((x[q] - centre[q]) ** 2 for q in range(3)) - s * s)), s0 < 0


def mechanics(flight, j, rail=None):
    """One rival's race, read deeper than analyse(): see the module doc's --grid paragraph."""
    hulls = flight.get("rivalHulls") or []
    rows, vel = flight.get("rivals") or [], flight.get("rivalVel") or []
    st, w = flight.get("rivalState") or [], len(flight.get("rivalStateColumns") or []) or 5
    names = flight.get("rivalBoostStatuses") or []
    cols = flight["columns"]
    T = [s[cols.index("t")] for s in flight["samples"]]
    me = [tuple(s[cols.index(k)] for k in ("x", "y", "z")) for s in flight["samples"]]
    rings = ring_geometry(flight)
    n = len(rings)
    target = int(flight.get("raceTarget") or 3 * n)
    rail = rail or RailIndex(flight.get("rails"))
    m = len(hulls)
    pos = [[(r[4 * o], r[4 * o + 1], r[4 * o + 2]) if len(r) >= 4 * o + 4 else None for r in rows] for o in range(m)]
    P = pos[j]
    K = [int(r[4 * j + 3]) if len(r) >= 4 * j + 4 else 0 for r in rows]
    N = min(len(T), len(P))
    if N < 2:
        return None
    d = {"hull": hulls[j], "rings": K[N - 1], "target": target, "seconds": round(T[N - 1] - T[0], 1)}
    fin = next((T[i] for i in range(N) if K[i] >= target), None)
    d["finish"] = round(fin - T[0], 1) if fin is not None else None
    d["projected"] = d["finish"] or (round(d["seconds"] * target / d["rings"], 1) if d["rings"] else None)
    splits, since, last = collections.defaultdict(list), T[0], K[0]
    misses, lost, open_at = [], 0.0, None
    for i in range(1, N):
        k0 = K[i - 1]
        if K[i] != k0:
            splits[k0 % n].append(round(T[i] - since, 1))
            since = T[i]
            if open_at is not None:
                lost, open_at = lost + T[i] - open_at, None
            continue
        c, a, R = rings[k0 % n]
        if k0 >= target or a is None or not R or P[i - 1] is None or P[i] is None:
            continue
        x = plane_crossing(P[i - 1], P[i], c, a)
        if not x or any(K[min(N - 1, i + z)] > k0 for z in range(1, THREAD_LAG + 1)):
            continue
        lat, forward = x
        if R < lat < MISS_REACH * R:          # inside the radius and not credited is a recorder lag, not a miss
            misses.append({"ring": k0 % n, "off": round(lat / R, 2), "forward": forward, "t": round(T[i] - T[0], 1)})
            open_at = T[i] if open_at is None else open_at
    if open_at is not None:
        lost += T[N - 1] - open_at
    d.update(splits={k: v for k, v in sorted(splits.items())}, misses=misses, lostAfterMiss=round(lost, 1))
    speed = []
    for i in range(N):
        v = vel[i][3 * j:3 * j + 3] if i < len(vel) and len(vel[i]) >= 3 * j + 3 else None
        if v is not None:
            speed.append(math.sqrt(sum(x * x for x in v)))
        elif i:
            speed.append(dist(P[i - 1], P[i]) / max(1e-6, T[i] - T[i - 1]))
    d["speed"] = round(statistics.median(speed)) if speed else 0
    acc = collections.Counter()
    statuses, ridden, prev_slowed = collections.Counter(), collections.defaultdict(list), False
    for i in range(N):
        others = [pos[o][i] for o in range(m) if o != j and i < len(pos[o]) and pos[o][i]] + ([me[i]] if i < len(me) else [])
        near = min((dist(P[i], q) for q in others), default=float("inf"))
        acc["near"] += near < NEAR_SHIP
        s = st[i][w * j:w * (j + 1)] if i < len(st) and len(st[i]) >= w * (j + 1) else None
        if not s:
            continue
        acc["state"] += 1
        bits = int(s[0])
        slowed = bool(bits & SLOWED)
        acc["slowed"] += slowed
        if slowed and not prev_slowed:
            acc["rams"] += 1
            if rail.nearest(P[i])[0] < RAM_RAIL:
                acc["railRams"] += 1
            if near < RAM_RAIL:
                acc["shipRams"] += 1
        prev_slowed = slowed
        acc["ai"] += bool(bits & AI_BIT)
        acc["breakingOff"] += bool(bits & BREAKING_OFF)
        if bits & ATTACHED:
            _, colour = rail.nearest(P[i], 3)
            ridden[colour].append(speed[min(i, len(speed) - 1)] if speed else 0)
        k = int(s[2])
        statuses[names[k] if 0 <= k < len(names) else "-"] += 1
    ns = max(1, acc["state"])
    d.update(rams=acc["rams"], railRams=acc["railRams"], shipRams=acc["shipRams"], slowedShare=round(acc["slowed"] / ns, 3),
             nearShipShare=round(acc["near"] / N, 3), aiBitShare=round(acc["ai"] / ns, 3) if acc["state"] else None,
             breakingOffShare=round(acc["breakingOff"] / ns, 3), hasState=bool(acc["state"]),
             statuses={k: round(v / ns, 2) for k, v in statuses.most_common(3)},
             ridden={c: {"share": round(len(v) / ns, 2), "speed": round(statistics.median(v))} for c, v in ridden.items() if c})
    return d


def _user_of(path):
    m = re.search(r"__([^/\\]+)\.json$", path) or re.search(r"reports[/\\]([^/\\]+)[/\\]flights", path)
    return m.group(1) if m else ""


def _first_threads(flight, j, upto=3):
    tcol = flight["columns"].index("t")
    out, last = [], None
    for s, r in zip(flight["samples"], flight.get("rivals") or []):
        if len(r) < 4 * j + 4:
            break
        k = int(r[4 * j + 3])
        if last is not None and k != last:
            out.append(s[tcol])
            if len(out) >= upto:
                break
        last = k
    return out


def records(paths, since=None, mode="Regatta"):
    out = []
    for p in paths:
        try:
            with open(p, encoding="utf-8") as fh:
                f = json.load(fh)
        except (OSError, ValueError):
            continue
        if f.get("mode") != mode or not f.get("ringList") or not f.get("rivalHulls"):
            continue
        if since and str(f.get("date", "")) < since:
            continue
        rc = f["columns"].index("rings") if "rings" in f["columns"] else None
        own = ((f.get("hullKey") or f.get("hull") or "?").lower(), int(f["samples"][-1][rc]) if rc is not None else 0)
        finals = [(h, int(f["rivals"][-1][4 * j + 3]) if f.get("rivals") and len(f["rivals"][-1]) >= 4 * j + 4 else 0)
                  for j, h in enumerate(f["rivalHulls"])]
        firsts = {h: _first_threads(f, j) for j, h in enumerate(f["rivalHulls"])}
        out.append({"path": p, "flight": f, "user": _user_of(p), "own": own, "finals": finals, "firsts": firsts})
    return out


def same_race(a, b):
    """Two records of one multiplayer race: two machines, one circuit, one race length, the same seats, and the rivals
    they share threaded their first rings at the same moments (two races in a row by one person differ there)."""
    fa, fb = a["flight"], b["flight"]
    if a["path"] == b["path"] or (a["user"] and a["user"] == b["user"]):
        return False
    if fa.get("courseIntensity") != fb.get("courseIntensity") or fa.get("ringList") != fb.get("ringList"):
        return False
    if abs(float(fa.get("raceSeconds") or 0) - float(fb.get("raceSeconds") or 0)) > 0.6:
        return False
    sa, sb = sorted([a["own"]] + a["finals"]), sorted([b["own"]] + b["finals"])
    if [h for h, _ in sa] != [h for h, _ in sb] or any(abs(x[1] - y[1]) > 1 for x, y in zip(sa, sb)):
        return False
    shared = [h for h in a["firsts"] if h in b["firsts"] and a["firsts"][h] and b["firsts"][h]]
    gaps = [abs(x - y) for h in shared for x, y in zip(a["firsts"][h], b["firsts"][h])]
    return bool(gaps) and statistics.median(gaps) < 0.5


def races(paths, since=None, mode="Regatta"):
    """One entry per race (records of one multiplayer race merged), each rival read by mechanics() and labelled `ai`,
    `human` or `?` (module doc, --grid)."""
    groups = []
    for r in records(paths, since, mode):
        for g in groups:
            if all(same_race(r, o) for o in g):
                g.append(r)
                break
        else:
            groups.append([r])
    out = []
    for g in groups:
        def bits_of(rec):
            st, n = rec["flight"].get("rivalState") or [], len(rec["finals"])
            return [bool(st) and sum(1 for x in st if len(x) >= 5 * (j + 1) and int(x[5 * j]) & AI_BIT) > 0.5 * len(st)
                    for j in range(n)]
        # the host's record: only the host runs the AIs, so only its AI bits and rival speeds are first-hand
        best = max(g, key=lambda rec: (sum(bits_of(rec)), bool(rec["flight"].get("rivalState"))))
        f = best["flight"]
        n_ai = len(f.get("rivalDifficulties") or {}) or None
        bits = bits_of(best)
        rail = RailIndex(f.get("rails"))
        if (n_ai is None and len(g) == 1) or n_ai == len(best["finals"]):
            how, labels = "every rival is an AI", ["ai"] * len(best["finals"])
        elif n_ai is not None and sum(bits) == n_ai:
            how, labels = "the host record's AI bit", ["ai" if b else "human" for b in bits]
        else:
            humans = collections.Counter(rec["own"] for rec in g)
            humans.subtract([best["own"]])
            labels = []
            for seat in best["finals"]:
                if humans[seat] > 0:
                    humans[seat] -= 1
                    labels.append("human")
                else:
                    labels.append("ai")
            how = "the recorders are the humans"
            if n_ai is not None and labels.count("ai") != n_ai:
                how, labels = "unresolved", ["ai" if b else "?" for b in bits]
        rivals = []
        for j, label in enumerate(labels):
            d = mechanics(f, j, rail)
            if d:
                d["pilot"] = label
                rivals.append(d)
        rings = ring_geometry(f)
        out.append({"files": [os.path.basename(rec["path"]) for rec in [best] + [x for x in g if x is not best]], "date": str(f.get("date", ""))[:16],
                    "intensity": f.get("courseIntensity") or f.get("intensity"), "mouth": rings[0][2] if rings else None,
                    "railColours": sorted({r.get("domain", "?") for r in f.get("rails") or []}),
                    "own": best["own"][0], "seats": len(best["finals"]) + 1, "humans": len(g), "attribution": how,
                    "raceSeconds": f.get("raceSeconds"), "rivals": rivals})
    return out


def faults(d):
    """The record's own evidence of an AI fault, as short strings."""
    out = []
    if d["hull"] == "urchin":
        team = max(d["ridden"].items(), key=lambda kv: kv[1]["share"])[0] if d["ridden"] else None
        slow = [c for c, v in d["ridden"].items() if v["share"] > 0.2 and v["speed"] < 100]
        if slow:
            out.append(f"rode the {'/'.join(slow)} rail at {d['ridden'][slow[0]]['speed']} u/s for "
                       f"{d['ridden'][slow[0]]['share']:.0%} of the race (a rival's colour, or its own blocked)")
        if d["breakingOffShare"] > 0.01:
            out.append(f"broke off its rail ({d['breakingOffShare']:.0%} of the race breaking off)")
        _ = team
    if d["hull"] == "squirrel" and (d.get("aiBitShare") or 0) > 0.25:
        out.append(f"AIPilot flew it, not the Skim Race pilot (AI bit on {d['aiBitShare']:.0%} of the race"
                   + (f", {next(iter(d['statuses']))}" if d["statuses"] and next(iter(d["statuses"])) != "-" else "") + ")")
    return out


def grid_report(all_races, hull=None):
    rows = [dict(d, intensity=r["intensity"], seats=r["seats"], date=r["date"], files=r["files"], humans=r["humans"])
            for r in all_races for d in r["rivals"] if d["pilot"] == "ai" and d["rings"] and d["seconds"] >= 20]
    print(f"{len(all_races)} races ({sum(1 for r in all_races if r['humans'] > 1)} multiplayer, recorded "
          f"{sum(len(r['files']) for r in all_races)} times); {len(rows)} AI rival races. "
          f"Human rivals left out: {sum(1 for r in all_races for d in r['rivals'] if d['pilot'] == 'human')}, "
          f"unplaced: {sum(1 for r in all_races for d in r['rivals'] if d['pilot'] == '?')}.")
    by = collections.defaultdict(list)
    for d in rows:
        if not hull or d["hull"] == hull:
            by[(d["hull"], d["intensity"], "grid" if d["seats"] >= 5 else "duel")].append(d)
    med = lambda xs: statistics.median(xs) if xs else float("nan")
    print(f"{'hull':9}{'I':>2} {'seats':5}{'n':>3} {'proj s':>7} {'u/s':>4} {'miss/ring':>9} {'s/miss':>6} {'rams':>5} "
          f"{'slowed':>6} {'traffic':>7}  slowest ring (median s)")
    for (h, i, kind), rs in sorted(by.items()):
        nm = sum(len(r["misses"]) for r in rs)
        st = [r for r in rs if r["hasState"]]
        split = collections.defaultdict(list)
        for r in rs:
            for k, v in r["splits"].items():
                split[k] += v
        worst = max(split.items(), key=lambda kv: med(kv[1])) if split else (None, [0])
        print(f"{h:9}{i:>2} {kind:5}{len(rs):>3} {med([r['projected'] for r in rs]):7.0f} {med([r['speed'] for r in rs]):4.0f} "
              f"{nm / max(1, sum(r['rings'] for r in rs)):9.2f} {sum(r['lostAfterMiss'] for r in rs) / max(1, nm):6.1f} "
              + (f"{med([r['rams'] for r in st]):5.1f} {med([r['slowedShare'] for r in st]):6.0%}" if st else f"{'-':>5} {'-':>6}")
              + f" {med([r['nearShipShare'] for r in rs]):7.1%}  ring {worst[0]}: {med(worst[1]):.1f}")
    flagged = [(r, d, faults(d)) for r in all_races for d in r["rivals"] if d["pilot"] == "ai"]
    flagged = [x for x in flagged if x[2] and (not hull or x[1]["hull"] == hull)]
    if flagged:
        print("\nfaults the records show:")
        for r, d, fs in flagged:
            print(f"  {r['date']} I{r['intensity']} {d['hull']}: {'; '.join(fs)}  ({d['rings']}/{d['target']} rings in "
                  f"{d['seconds']} s; {r['files'][0]})")
    if hull:
        print()
        for d in sorted((d for d in rows if d["hull"] == hull), key=lambda d: d["date"]):
            print(f"  {d['date']} I{d['intensity']} {d['seats']} seats: {d['rings']:2d}/{d['target']} in {d['seconds']:5.1f} s "
                  f"(proj {d['projected']}), {d['speed']} u/s, misses {[(x['ring'], x['off']) for x in d['misses']]}, "
                  f"lost {d['lostAfterMiss']} s, rams {d['rams']} ({d['railRams']} at a rail), slowed {d['slowedShare']:.0%}, "
                  f"{'; '.join(f'{v:.0%} {k}' for k, v in d['statuses'].items())}  {d['files'][0]}")


def grid_self_test():
    bad = []
    # a ring at the origin facing +x, radius 50; a rival crosses its plane 120 u out (a miss), comes back and threads
    f = {"columns": ["t", "x", "y", "z", "rings"], "raceTarget": 2, "rivalHulls": ["manta"],
         "ringColumns": ["index", "x", "y", "z", "ax", "ay", "az", "radius"],
         "ringList": [[0, 0, 0, 0, 1, 0, 0, 50], [1, 1000, 0, 0, 1, 0, 0, 50]],
         "rails": [{"domain": "Jade", "prisms": [[-5, 300, 0, 1, 0, 0]]}], "samples": [], "rivals": [],
         "rivalStateColumns": ["state", "boost", "boostStatus", "phase", "courseNose"], "rivalState": []}
    path = [(-100, 120, 0, 0), (100, 120, 0, 0), (100, 0, 0, 0), (-100, 0, 0, 0), (-100, 10, 0, 0), (100, 10, 0, 1),
            (900, 0, 0, 1), (1100, 0, 0, 2)]
    for i, (x, y, z, k) in enumerate(path):
        f["samples"].append([i * 1.0, 5000, 5000, 5000, 0])
        f["rivals"].append([x, y, z, k])
        f["rivalState"].append([64 | (8 if i == 3 else 0), 1, -1, 1, 0])
    d = mechanics(f, 0)
    if len(d["misses"]) != 1 or d["misses"][0]["off"] != 2.4 or d["rings"] != 2 or d["finish"] != 7.0:
        bad.append(f"miss/thread reading {d['misses']} rings {d['rings']} finish {d['finish']}")
    # (the backward crossing at t 2->3 is inside the radius, uncredited, so it is a recorder lag, not a miss)
    if d["lostAfterMiss"] != 4.0:
        bad.append(f"seconds lost after the miss {d['lostAfterMiss']} (crossed at 1 s, threaded at 5 s)")
    if d["rams"] != 1 or d["railRams"] != 0:
        bad.append(f"rams {d['rams']} / at a rail {d['railRams']} (the slow-down at (-100,0,0) is 300 u from the rail)")
    f["rails"][0]["prisms"][0] = [-100, 5, 0, 1, 0, 0]
    if mechanics(f, 0)["railRams"] != 1:
        bad.append("a slow-down 5 u from a rail prism must count as a rail ram")
    # an Urchin attached to a Jade rail at 24 u/s for the whole race: a rival's colour, flagged
    u = {"hull": "urchin", "ridden": {"Jade": {"share": 0.9, "speed": 24}}, "breakingOffShare": 0.0}
    if not faults(u):
        bad.append("an Urchin riding a rail at 24 u/s must be flagged")
    if faults({"hull": "urchin", "ridden": {"Jade": {"share": 0.9, "speed": 300}}, "breakingOffShare": 0.0}):
        bad.append("an Urchin riding its rail at 300 u/s must not be flagged")
    if not faults({"hull": "squirrel", "aiBitShare": 1.0, "statuses": {"-": 1.0}}):
        bad.append("an AI Squirrel flown by AIPilot must be flagged")
    if faults({"hull": "squirrel", "aiBitShare": 0.0, "statuses": {"-": 1.0}}):
        bad.append("an AI Squirrel flown by the Skim Race pilot must not be flagged")
    # one multiplayer race recorded on two machines, and two races in a row by one person: merge only the first
    def rec(user, own, first):
        return {"path": user + own, "user": user, "own": (own, 10), "finals": [("urchin", 24)],
                "firsts": {"urchin": [first, first + 2]},
                "flight": {"courseIntensity": 4, "ringList": [[0]], "raceSeconds": 62.0}}
    if not same_race(rec("gradi", "dolphin", 4.9), rec("belac", "dolphin", 5.0)):
        bad.append("one race on two machines must merge")
    if same_race(rec("gradi", "dolphin", 4.9), rec("gradi", "dolphin", 5.0)):
        bad.append("two records by one person are two races")
    if same_race(rec("gradi", "dolphin", 4.9), rec("belac", "dolphin", 7.5)):
        bad.append("rivals threading at other moments are another race")
    return bad


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
    # a slow hull (37 u/s) flying straight down 350 u legs: inside ORBIT_RANGE for about 7 s a ring, never circling
    slow = [(i * 0.1, (i * 3.7, 0, 0), int(i * 3.7 // 350)) for i in range(1000)]
    c = analyse(slow, [(350.0 * (x + 1), 0, 0) for x in range(12)], 12)
    if c["circling"] > 0: bad.append(f"a slow straight approach counted {c['circling']} s circling")
    # rivalState: one rival, 4 samples - 2 waiting (status 0; the first at a resting x5, not boosting), 1 ramping x3 (status 1), 1 breaking off with no driver status
    fl = {"rivalStateColumns": ["state", "boost", "boostStatus", "phase", "courseNose"],
          "rivalStateBits": {"ai": 64, "breakingOff": 128, "restricted": 256},
          "rivalBoostStatuses": ["Rhino: cruising", "Rhino: ramping (# u/s)"],
          "rivalState": [[64, 5, 0, 1, 0], [64, 1, 0, 1, 0], [65, 3, 1, 1, 2], [192, 1, -1, 1, 40]]}
    rs = rival_states(fl).get(0)
    if not rs or rs["breakingOff"] != 0.25 or rs["boosted"] != 0.25 or rs["statuses"][0] != (0.5, "Rhino: cruising"):
        bad.append(f"rivalState summary {rs}")
    if rival_states({}) != {}: bad.append("an older flight without rivalState must read as none")
    # rivalKite: one rival chasing ring 0 at (0,0,0); its hull passes 80 u off, its kite 10 u off; one sample unseated
    kf = {"rivals": [[80, 0, 0, 0], [90, 0, 0, 0], [100, 0, 0, 0]],
          "rivalKite": [[10, 0, 0, 2], [0, 0, 0, -1], [30, 0, 0, 2]]}
    kt = rival_kites(kf, [(0, 0, 0), (1000, 0, 0)]).get(0)
    if not kt or abs(kt["seated"] - 2 / 3) > 1e-6 or kt["closest"][0] != (80, 10):
        bad.append(f"rivalKite summary {kt}")
    if rival_kites({}, [(0, 0, 0)]) != {}: bad.append("an older flight without rivalKite must read as none")
    bad += grid_self_test()
    print("regatta_rival_report self-test: " + ("ok" if not bad else "FAILED " + repr(bad)))
    return 0 if not bad else 1


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir")
    ap.add_argument("--hull")
    ap.add_argument("--since")
    ap.add_argument("--mode", default="Regatta", help="the GameModes name the flights were raced in (Switchback, ...)")
    ap.add_argument("--grid", action="store_true", help="race by race: merge multiplayer records, drop human rivals, "
                                                        "misses, rail rams, traffic and faults (module doc)")
    ap.add_argument("--json", help="with --grid: write every race and rival read to this file")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        sys.exit(self_test())
    if not a.dir:
        ap.error("--dir is required")
    if a.grid:
        rs = races(sorted(glob.glob(os.path.join(a.dir, f"flight_{a.mode}_*.json"))), a.since, a.mode)
        grid_report(rs, a.hull and a.hull.lower())
        if a.json:
            with open(a.json, "w", encoding="utf-8") as fh:
                json.dump(rs, fh, indent=1)
        sys.exit(0)
    report(load(sorted(glob.glob(os.path.join(a.dir, f"flight_{a.mode}_*.json"))), a.since, a.mode), a.hull and a.hull.lower())
