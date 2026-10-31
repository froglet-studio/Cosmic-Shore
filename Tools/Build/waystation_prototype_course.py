#!/usr/bin/env python3
"""The offline authority for WAYSTATION PROTOTYPE's Drop Towers (Docs/Studios/TIGER_MOTH_TIME.md).

It MIRRORS Assets/_Scripts/Controller/Arcade/WaystationPrototype/WaystationTower.cs (the tower rings, the plates and
the tile kinds) on top of Garrett's own course model, `waystation_course.py`, which it IMPORTS and never edits: the
prototype's clusters are Waystation's clusters. The tower tables are read out of the C#, and the hull's numbers out
of TigerMoth.prefab and TigerMothDropAction.asset, so the model cannot describe a tower the game does not lay or a
Drop the vessel does not have.

What it proves, per intensity, over the whole seed sweep:

  window     every hop's legal travel window is the intensity's authored width, and the plate under it is deep
             enough that the cruise line clears it (a hop too short lands on the plate)
  runway     a pilot landing at the window's far edge reaches the next press point with the Drop recharged and any
             roll done, plus the reaction allowance (a hop too long ends in the pipe)
  path       the nominal pilot's CORPOREAL path (cruise, rolls, landings, the flight into the next cluster) never
             touches a tile, at the window's near edge, centre and far edge
  edges      the window's edges are real: a hop just short of it lands in the plate, and a hop well past it has no
             way to reach the next ring before the pipe
  mouths     no tile cuts any ring's mouth, cluster or tower
  shell      every tile is inside the cell's course shell
  towers     every gap but the last carries a tower of at least one D hop
  long       the last gap is long enough to leave a wormhole (>= LongGapMin) on every swept seed
  standdown  every tower ring is nearer its predecessor than the scene's aiFoldMinDistance, so Garrett's long-drop
             autopilot (WaystationController) stands down on towers and the prototype's hop autopilot takes them
  danger     the danger tiles take their share (within 0.03), none at share 0 and all at share 1
  radius     a tower ring's radius is never a cluster ring's at the same intensity

`--report` prints the pacing a nominal pilot gets: drops per minute, the longest wait between drops, the race time.
`--self-test` runs negative controls, each of which must make the check it targets FAIL.
"""

import argparse
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import waystation_course as wc  # noqa: E402  (Garrett's model, imported read-only)

ROOT = Path(__file__).resolve().parents[2]
CS_TOWER = ROOT / "Assets/_Scripts/Controller/Arcade/WaystationPrototype/WaystationTower.cs"
TIGER_MOTH = ROOT / "Assets/_Prefabs/Spacevessels/TigerMoth.prefab"
DROP_ASSET = ROOT / "Assets/_SO_Assets/VesselActions/TigerMoth/TigerMothDropAction.asset"
PROTO_SCENE = ROOT / "Assets/_Scenes/Multiplayer Scenes/MinigameWaystationPrototype.unity"

# the prototype scene raises Garrett's long-drop threshold so his autopilot drops only across the long gap
AI_FOLD_MIN_DISTANCE = 700.0
CLUSTERS = 5

add, sub, mul, dot, mag, cross, safe, perp = wc.add, wc.sub, wc.mul, wc.dot, wc.mag, wc.cross, wc.safe, wc.perp


def norm(v, fb):
    return safe(v, fb)


def project_on_plane(v, n):
    d = dot(n, n)
    return v if d < 1e-12 else sub(v, mul(n, dot(v, n) / d))


# ---------------------------------------------------------------- the tables, read from the C#
def read_tower():
    src = CS_TOWER.read_text(encoding="utf-8")
    T = {}
    for field in ("HopRingRadius", "WindowCentre", "WindowHalfWidth", "RollGateShare", "DangerShare"):
        m = re.search(rf"{field}\s*=\s*new\[\]\s*\{{([^}}]*)\}}", src)
        assert m, f"{field} array not found in {CS_TOWER.name}"
        T[field] = [float(x.strip().rstrip("f")) for x in m.group(1).split(",")]
        assert len(T[field]) == 4, f"{field} is not four rungs"
    for name in ("Cruise", "DropSpeed", "DropEaseSeconds", "LandSeconds", "FullCooldownSeconds",
                 "FullCooldownDistance", "RollDegreesPerSecond", "HullRadius", "LandClear", "RingPastPlate",
                 "PipeClear", "ReactSeconds", "FinalMin", "PlungeCap", "ShellPad", "MembraneReach", "PlateMargin", "TileThickness",
                 "LongGapMin"):
        m = re.search(rf"public const float {name}\s*=\s*([0-9.]+)f;", src)
        assert m, f"const {name} not found in {CS_TOWER.name}"
        T[name] = float(m.group(1))
    for name in ("TilesPerSide", "SeedTries"):
        m = re.search(rf"public const int {name}\s*=\s*([0-9]+);", src)
        assert m, f"const {name} not found in {CS_TOWER.name}"
        T[name] = int(m.group(1))
    T["Lead"] = T["Cruise"] * (90.0 / T["RollDegreesPerSecond"] + T["ReactSeconds"])
    return T


def assert_hull_matches(T):
    """The tower's hull constants are copies; a vessel retune must fail here, not ship a tower nobody can fly."""
    prefab = TIGER_MOTH.read_text(encoding="utf-8")

    def one(src, key):
        m = re.search(rf"^\s*{key}:\s*(-?[0-9.]+)\s*$", src, re.M)
        assert m, f"{key} not found"
        return float(m.group(1))

    cruise = one(prefab, "DefaultMinimumSpeed") + one(prefab, "DefaultThrottleScaler")
    assert abs(cruise - T["Cruise"]) < 1e-3, f"TigerMoth cruise is {cruise}, the tower says {T['Cruise']}"
    roll = one(prefab, "RollScaler")
    assert abs(roll - T["RollDegreesPerSecond"]) < 1e-3, f"TigerMoth roll is {roll}, the tower says {T['RollDegreesPerSecond']}"
    drop = DROP_ASSET.read_text(encoding="utf-8")
    for key, name in (("dropSpeed", "DropSpeed"), ("dropEaseSeconds", "DropEaseSeconds"),
                      ("landSeconds", "LandSeconds"), ("fullCooldownSeconds", "FullCooldownSeconds"),
                      ("fullCooldownDistance", "FullCooldownDistance")):
        v = one(drop, key)
        assert abs(v - T[name]) < 1e-4, f"TigerMothDropAction {key} is {v}, the tower says {T[name]}"


def rung(T, intensity):
    i = intensity - 1
    s = {k: T[k][i] for k in ("HopRingRadius", "WindowCentre", "WindowHalfWidth", "RollGateShare", "DangerShare")}
    s["low"] = s["WindowCentre"] - s["WindowHalfWidth"]
    s["high"] = s["WindowCentre"] + s["WindowHalfWidth"]
    s["plate"] = s["low"] - T["HullRadius"] - T["LandClear"]
    s["ring"] = s["plate"] + T["RingPastPlate"]
    return s


# ---------------------------------------------------------------- the Drop (TigerMothDropExecutor, closed form)
def ease(x):
    t = max(0.0, min(1.0, x))
    return t * t * (3 - 2 * t)


def fall(T, hold):
    t = max(0.0, hold)
    V, E = T["DropSpeed"], T["DropEaseSeconds"]
    if t <= E:
        x = t / E
        return V * E * (x ** 3 - 0.5 * x ** 4)
    return V * E * 0.5 + V * (t - E)


def travel(T, hold):
    t = max(0.0, hold)
    release = T["DropSpeed"] * ease(t / T["DropEaseSeconds"])
    return fall(T, t) + T["LandSeconds"] * (release + T["Cruise"]) * 0.5


def hold_for_travel(T, tr):
    lo, hi = 0.0, 4.0
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if travel(T, mid) < tr:
            lo = mid
        else:
            hi = mid
    return 0.5 * (lo + hi)


def recharge(T, tr):
    return T["FullCooldownSeconds"] * max(0.0, min(1.0, fall(T, hold_for_travel(T, tr)) / T["FullCooldownDistance"]))


def roll_s(T, deg):
    return abs(deg) / T["RollDegreesPerSecond"]


def runway(T, s, roll_deg):
    return T["Cruise"] * (max(recharge(T, s["high"]), roll_s(T, roll_deg)) + T["ReactSeconds"])


# ---------------------------------------------------------------- hashes (uint32, as the C#)
def hash01(a, b, c):
    M = 0xFFFFFFFF
    h = ((a & M) * 73856093 & M) ^ ((b & M) * 19349663 & M) ^ ((c & M) * 83492791 & M)
    h ^= h >> 13
    h = (h * 0x5bd1e995) & M
    h ^= h >> 15
    return (h & 0xFFFF) / 65536.0


def is_danger_tile(gate, wall, tile, share):
    if share <= 0:
        return False
    if share >= 1:
        return True
    return hash01(gate, wall * 7919 + 1, tile) < share


def is_roll_gate(tower, lateral, share):
    positive = hash01(tower, lateral, 977) < 0.5
    if lateral % 2 == 0 or share <= 0:
        return False, positive
    if share >= 1:
        return True, positive
    return hash01(tower, lateral, 131) < share, positive


# ---------------------------------------------------------------- the tower (WaystationTower.Lay)
def plate_inside(T, pl, inner_limit, outer_limit):
    """WaystationTower.PlateInside: the plate's nearest point to the cell centre clears the nucleus, and its farthest
    corner is inside the membrane."""
    return (plate_distance(pl, (0.0, 0.0, 0.0), T["TileThickness"]) >= inner_limit
            and mag(pl["c"]) + pl["half"] * 1.415 <= outer_limit)


def make_plate(centre, normal, across, half):
    n = safe(normal, (0, 1, 0))
    u = safe(project_on_plane(across, n), perp(n))
    return dict(c=centre, n=n, u=u, v=cross(n, u), half=half)


def seg_hits_plate(T, a, b, pl, clearance):
    L = mag(sub(b, a))
    n = max(1, int(L / 3.0))
    for k in range(n + 1):
        if plate_distance(pl, add(a, mul(sub(b, a), k / n)), T["TileThickness"]) < clearance:
            return True
    return False


def ring_hits_plate(T, ring, pl):
    gp, gax, gr = ring
    u = perp(gax)
    w = cross(gax, u)
    for k in range(17):
        p = gp if k == 16 else add(gp, add(mul(u, gr * math.cos(k * math.pi / 8)), mul(w, gr * math.sin(k * math.pi / 8))))
        if plate_distance(pl, p, T["TileThickness"]) < 3.0:
            return True
    return False


def elements_clear(T, plates, segs, obst, inner_limit, outer_limit, ignore=False):
    """WaystationTower.ElementsClear: new plates inside the cell and clear of every obstacle; new path clear of
    every obstacle plate."""
    if ignore:
        return True
    clear = T["HullRadius"] + 1.0
    for pl in plates:
        if not plate_inside(T, pl, inner_limit, outer_limit):
            return False
        for a, b in obst["segs"]:
            if seg_hits_plate(T, a, b, pl, clear):
                return False
        for ring in obst["rings"]:
            if ring_hits_plate(T, ring, pl):
                return False
        for other in obst["plates"]:
            if mag(sub(pl["c"], other["c"])) < 1.415 * (pl["half"] + other["half"]):
                return False
    for a, b in segs:
        for other in obst["plates"] + plates:
            if seg_hits_plate(T, a, b, other, clear):
                return False
    return True


def lay(T, s, exit_gate, next_ring, tower, out, inner_limit, outer_limit, obst, plan=None, no_runway=False,
        no_cancel=False, ignore_obstacles=False):
    heading = safe(exit_gate[1], (0, 0, 1))
    press = add(exit_gate[0], mul(heading, T["Lead"]))
    down = safe(project_on_plane(sub(next_ring, press), heading), perp(heading))
    avail = dot(sub(next_ring, press), down) - T["FinalMin"]
    high, low = s["high"], s["low"]
    if avail < high:
        return 0
    half = plate_half(T, s)
    pipe = s["HopRingRadius"] + T["PipeClear"] + T["HullRadius"]
    lateral_stride = high + (0.0 if no_runway else runway(T, s, 180.0))

    def finals(p):
        # the flight into the next cluster: the pilot lands anywhere in the window, so three lines, not one
        return [(add(p, mul(down, h)), next_ring) for h in (low, s["WindowCentre"], high)]

    first_floor = make_plate(add(press, mul(down, s["plate"])), down, heading, half)
    first_segs = [(exit_gate[0], press)] + finals(press)
    if not elements_clear(T, [first_floor], first_segs, obst, inner_limit, outer_limit, ignore_obstacles):
        return 0
    obst["plates"].append(first_floor)
    obst["segs"].append(first_segs[0])
    final_segs = first_segs[1:]

    belly = mul(heading, -1)
    last_side = (0.0, 0.0, 0.0)
    used = 0.0
    d_hops = 1
    steps = []
    rings = []
    arrive = heading
    lateral = 1
    while True:
        gate, positive = is_roll_gate(tower, lateral, s["RollGateShare"])
        abeam = safe(cross(down, belly), perp(down))
        if not positive:
            abeam = mul(abeam, -1)
        if lateral % 2 == 0 and not no_cancel:
            options = [(mul(last_side, -1), 0.0)]
        else:
            ordered = [(abeam, 90.0), (mul(abeam, -1), 90.0), (belly, 0.0)]
            options = ordered if gate else [ordered[2], ordered[0], ordered[1]]
        chosen = None
        for side, roll in options:
            run = 0.0 if no_runway else runway(T, s, roll)
            if used + high + run + high > avail:
                continue
            lpress = add(press, mul(down, high + run))
            nxt = add(lpress, mul(side, lateral_stride))
            new_plates = [make_plate(add(lpress, mul(down, pipe)), down, side, half),
                          make_plate(add(lpress, mul(side, s["plate"])), side, down, half),
                          make_plate(add(nxt, mul(side, pipe)), side, down, half),
                          make_plate(add(nxt, mul(down, s["plate"])), down, side, half)]
            new_segs = [(add(press, mul(down, low)), lpress), (add(lpress, mul(side, low)), nxt)] + finals(nxt)
            if elements_clear(T, new_plates, new_segs, obst, inner_limit, outer_limit, ignore_obstacles):
                chosen = (side, roll, run, lpress, nxt, new_plates, new_segs)
                break
        if chosen is None:
            break
        side, roll, run, lpress, nxt, new_plates, new_segs = chosen
        rings.append((add(press, mul(down, s["ring"])), down, s["HopRingRadius"]))
        steps.append(dict(press=press, x=down, roll=90.0 if d_hops == 1 else 180.0))
        rings.append((add(lpress, mul(side, s["ring"])), side, s["HopRingRadius"]))
        steps.append(dict(press=lpress, x=side, roll=roll))
        obst["plates"].extend(new_plates)
        obst["segs"].extend(new_segs[:2])
        obst["rings"].extend(rings[-2:])
        final_segs = new_segs[2:]
        press = nxt
        used += high + run
        last_side = side
        belly = mul(side, -1)
        d_hops += 1
        lateral += 1
    plunge = max(0.0, min(T["PlungeCap"], avail - (used + high)))
    rings.append((add(press, mul(down, s["ring"] + plunge)), down, s["HopRingRadius"]))
    steps.append(dict(press=press, x=down, roll=90.0 if d_hops == 1 else 180.0, plunge=plunge))
    out.extend(rings)
    obst["segs"].extend(final_segs)
    obst["rings"].append(rings[-1])
    if plan is not None:
        plan.append(dict(tower=tower, steps=steps, next_ring=next_ring, exit=exit_gate, d_hops=d_hops))
    return d_hops


def last_gap(clusters, per):
    n = len(clusters) // per
    if n < 2:
        return 0.0
    return mag(sub(clusters[(n - 1) * per - 1][0], clusters[(n - 1) * per][0]))


def cluster_obstacles(clusters, per):
    """WaystationTower.ClusterObstacles: every cluster ring, and the flight through each cluster and across the long
    last gap."""
    obst = dict(segs=[], plates=[], rings=list(clusters))
    n = len(clusters) // per
    for c in range(n):
        for k in range(1, per):
            obst["segs"].append((clusters[c * per + k - 1][0], clusters[c * per + k][0]))
    if n >= 2:
        obst["segs"].append((clusters[(n - 1) * per - 1][0], clusters[(n - 1) * per][0]))
    return obst


def build(T, CT, seed, intensity, clusters, plan=None, **kw):
    """WaystationTower.Build: Garrett's clusters, a tower in every gap but the last, seeds tried for a long last gap."""
    s = rung(T, intensity)
    target = clusters * int(CT["RingsPerCluster"][intensity - 1])
    best, best_gap, used_seed, per = None, -1.0, seed, 1
    for attempt in range(T["SeedTries"]):
        trial = (seed + attempt * 7919) & 0xFFFFFFFF
        if trial >= 2 ** 31:
            trial -= 2 ** 32
        gates, clust, centres, folds, per = wc.generate(trial, intensity, target, CT)
        g = last_gap(gates, per)
        if g > best_gap:
            best, best_gap, used_seed = gates, g, trial
        if g >= T["LongGapMin"]:
            break
    obst = cluster_obstacles(best, per)
    laid = []
    gaps = len(best) // per - 1
    towers = []
    c = 0
    while c * per < len(best):
        for k in range(per):
            if c * per + k < len(best):
                laid.append(best[c * per + k])
        if c < gaps - 1:
            towers.append(lay(T, s, best[c * per + per - 1], best[(c + 1) * per][0], c, laid,
                              wc.INNER / 1.22 + T["ShellPad"], wc.COURSE_OUTER + T["MembraneReach"], obst, plan, **kw))
        c += 1
    return laid, best, per, towers, best_gap


# ---------------------------------------------------------------- the walls (WaystationTower.Walls)
def is_tower_ring(gate, s):
    return abs(gate[2] - s["HopRingRadius"]) < 0.01


def plate_half(T, s):
    return s["HopRingRadius"] + T["PlateMargin"]


def press_of(T, course, i, s):
    ring = course[i]
    last = i + 1 >= len(course) or not is_tower_ring(course[i + 1], s)
    if not last:
        return sub(ring[0], mul(ring[1], s["ring"]))
    prev = course[i - 1]
    if not is_tower_ring(prev, s):
        return add(prev[0], mul(prev[1], T["Lead"]))
    lpress = sub(prev[0], mul(prev[1], s["ring"]))
    return add(lpress, mul(prev[1], s["high"] + runway(T, s, 180.0)))


def walls(T, course, s, plates_out=None):
    tiles = []
    for i in range(1, len(course)):
        if not is_tower_ring(course[i], s):
            continue
        down = course[i][1]
        press = press_of(T, course, i, s)
        across = course[i - 1][1]
        plate(T, add(press, mul(down, s["plate"])), down, across, s, i, 0, tiles, plates_out)
        if i + 1 < len(course) and is_tower_ring(course[i + 1], s):
            npress = press_of(T, course, i + 1, s)
            plate(T, add(npress, mul(down, s["HopRingRadius"] + T["PipeClear"] + T["HullRadius"])), down,
                  course[i + 1][1], s, i, 1, tiles, plates_out)
    return tiles


def plate(T, centre, normal, across, s, gate, wall, tiles, plates_out):
    n = safe(normal, (0, 1, 0))
    u = safe(project_on_plane(across, n), perp(n))
    v = cross(n, u)
    half = plate_half(T, s)
    if plates_out is not None:
        plates_out.append(dict(c=centre, n=n, u=u, v=v, half=half, gate=gate, wall=wall))
    N = T["TilesPerSide"]
    tile = 2 * half / N
    idx = 0
    for a in range(N):
        for b in range(N):
            p = add(add(centre, mul(u, -half + tile * (a + 0.5))), mul(v, -half + tile * (b + 0.5)))
            tiles.append((p, is_danger_tile(gate, wall, idx, s["DangerShare"])))
            idx += 1


def plate_distance(pl, p, thickness):
    """Distance from point p to a plate (a square of half-side `half`, `thickness` thick)."""
    d = sub(p, pl["c"])
    a = max(-pl["half"], min(pl["half"], dot(d, pl["u"])))
    b = max(-pl["half"], min(pl["half"], dot(d, pl["v"])))
    nrm = dot(d, pl["n"])
    nrm = max(0.0, abs(nrm) - thickness * 0.5)
    q = add(add(pl["c"], mul(pl["u"], a)), mul(pl["v"], b))
    inplane = sub(sub(p, q), mul(pl["n"], dot(sub(p, q), pl["n"])))
    return math.sqrt(dot(inplane, inplane) + nrm * nrm)


def segment_clear(plates, a, b, clearance, thickness, skip=()):
    """Smallest distance from the segment ab to any plate not in `skip`, sampled every 3 u."""
    L = mag(sub(b, a))
    n = max(1, int(L / 3.0))
    worst = float("inf")
    for k in range(n + 1):
        p = add(a, mul(sub(b, a), k / n))
        for j, pl in enumerate(plates):
            if j in skip:
                continue
            worst = min(worst, plate_distance(pl, p, thickness))
            if worst < clearance:
                return worst
    return worst


# ---------------------------------------------------------------- the nominal pilot
def fly_tower(T, s, plan, plates, hold_travel):
    """Fly one tower at a fixed hop travel. Returns (ok, min clearance, waits, ready_ok, times)."""
    clearance = T["HullRadius"]
    thick = T["TileThickness"]
    worst = float("inf")
    ready_ok = True
    waits = []
    steps = plan["steps"]
    # exit gate -> first press: corporeal, rolling the cluster under the belly
    a = plan["exit"][0]
    worst = min(worst, segment_clear(plates, a, steps[0]["press"], clearance, thick))
    for k, st in enumerate(steps):
        land = add(st["press"], mul(st["x"], hold_travel))
        nxt = steps[k + 1]["press"] if k + 1 < len(steps) else plan["next_ring"]
        worst = min(worst, segment_clear(plates, land, nxt, clearance, thick))
        if k + 1 < len(steps):
            cruise = dot(sub(nxt, land), st["x"])
            t_avail = cruise / T["Cruise"]
            need = max(recharge(T, hold_travel), roll_s(T, steps[k + 1]["roll"]))
            waits.append(hold_for_travel(T, hold_travel) + T["LandSeconds"] + t_avail)
            if t_avail + 1e-6 < need:
                ready_ok = False
    return worst >= clearance, worst, waits, ready_ok


def overshoot(T, s, steps, k, tr):
    """How far past the next press point a pilot presses after a hop of travel `tr` (landing, cruising at full throttle
    until the Drop is recharged and the roll done)."""
    wait = max(recharge(T, tr), roll_s(T, steps[k + 1]["roll"]))
    press = add(add(steps[k]["press"], mul(steps[k]["x"], tr)), mul(steps[k]["x"], T["Cruise"] * wait))
    return dot(sub(press, steps[k + 1]["press"]), steps[k]["x"])


def far_edge(T, s, steps, k):
    """The shortest hop travel after which the next ring can no longer be dropped through (the press point is past its
    mouth): the window's HARD far edge, where the pipe waits."""
    lo, hi = s["high"], s["high"] + 2000.0
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if overshoot(T, s, steps, k, mid) > s["HopRingRadius"]:
            hi = mid
        else:
            lo = mid
    return hi


def edges(T, s, plan, plates):
    """A hop just short of the window lands in the plate; a hop past the window's hard far edge cannot reach the next
    ring, and that edge is within one ring radius of the authored far edge."""
    thick = T["TileThickness"]
    steps = plan["steps"]
    short_hits = 0
    worst_far = 0.0
    for k, st in enumerate(steps):
        land = add(st["press"], mul(st["x"], s["plate"] + T["HullRadius"] - 1.0))
        if min(plate_distance(pl, land, thick) for pl in plates) < T["HullRadius"]:
            short_hits += 1
        if k + 1 < len(steps):
            worst_far = max(worst_far, far_edge(T, s, steps, k) - s["high"])
    return short_hits == len(steps), worst_far


# ---------------------------------------------------------------- measure
def measure(intensity, T, CT, seeds, target, **kw):
    s = rung(T, intensity)
    i = intensity - 1
    m = dict(window=True, runway=True, path=True, edges=True, mouths=True, shell=True, towers=True, long=True,
             standdown=True, worst_clear=float("inf"), max_link=0.0, min_long=1e9, drops=[], waits=[], times=[],
             tiles=0, d_hops=[], tower_rings=0, short_long=0)
    cluster_r = CT["RingRadius"][i]
    m["radius"] = abs(cluster_r - s["HopRingRadius"]) > 0.5
    m["window"] = s["plate"] >= T["HullRadius"] + T["LandClear"] and abs(s["high"] - s["low"] - 2 * s["WindowHalfWidth"]) < 1e-6
    for seed in range(1, seeds + 1):
        plan = []
        laid, clusters, per, towers, gap = build(T, CT, seed, intensity, target, plan, **kw)
        m["min_long"] = min(m["min_long"], gap)
        if gap < T["LongGapMin"]:
            m["long"] = False
        if sum(1 for t in towers if t >= 1) < len(towers) - 1:
            m["towers"] = False   # at most one gap a course may go without a tower
        m["bare"] = m.get("bare", 0) + sum(1 for t in towers if t < 1)
        m["gaps"] = m.get("gaps", 0) + len(towers)
        m["d_hops"].extend(towers)
        plates = []
        tiles = walls(T, laid, s, plates)
        m["tiles"] = max(m["tiles"], len(tiles))
        m["tower_rings"] = max(m["tower_rings"], sum(1 for g in laid if is_tower_ring(g, s)))
        for pl in plates:
            if not plate_inside(T, pl, wc.INNER / 1.22 + T["ShellPad"], wc.COURSE_OUTER + T["MembraneReach"]):
                m["shell"] = False
        # mouths: no plate within 3 u of any ring's rim or centre (a tower ring's own floor lies RingPastPlate under it)
        for g in laid:
            for pl in plates:
                if ring_hits_plate(T, g, pl):
                    m["mouths"] = False
        # stand-down: each tower ring is within Garrett's long-drop threshold of its predecessor
        for gi in range(1, len(laid)):
            if is_tower_ring(laid[gi], s) or (gi > 0 and is_tower_ring(laid[gi - 1], s)):
                link = mag(sub(laid[gi][0], laid[gi - 1][0]))
                if is_tower_ring(laid[gi], s):
                    m["max_link"] = max(m["max_link"], link + s["ring"])
                    if link + s["ring"] >= AI_FOLD_MIN_DISTANCE:
                        m["standdown"] = False
        for pl_plan in plan:
            for hold in (s["low"], s["WindowCentre"], s["high"]):
                ok, worst, waits, ready = fly_tower(T, s, pl_plan, plates, hold)
                m["worst_clear"] = min(m["worst_clear"], worst)
                if not ok:
                    m["path"] = False
                if not ready:
                    m["runway"] = False
                if hold == s["WindowCentre"]:
                    m["waits"].extend(waits)
                    m["drops"].append(len(pl_plan["steps"]))
            short_ok, far = edges(T, s, pl_plan, plates)
            m["far"] = max(m.get("far", 0.0), far)
            if not short_ok or far > s["HopRingRadius"] + T["ReactSeconds"] * T["Cruise"]:
                m["edges"] = False
    m["danger"] = danger_share_ok(s["DangerShare"])
    return m


def danger_share_ok(share, n=6400):
    got = sum(1 for g in range(40) for t in range(n // 40) if is_danger_tile(g, g % 2, t, share)) / n
    ends = (not any(is_danger_tile(g, 0, t, 0.0) for g in range(8) for t in range(64))
            and all(is_danger_tile(g, 1, t, 1.0) for g in range(8) for t in range(64)))
    return abs(got - share) <= 0.03 and ends


def _share_close(share, measured_as):
    """The share check, pointed at the wrong share: it must fail."""
    got = sum(1 for g in range(40) for t in range(160) if is_danger_tile(g, g % 2, t, share)) / 6400
    return abs(got - measured_as) <= 0.03


CHECKS = ("window", "runway", "path", "edges", "mouths", "shell", "towers", "long", "standdown", "danger", "radius")


def run(seeds, target, quiet=False, report=False, T=None, **kw):
    T = T or read_tower()
    assert_hull_matches(T)
    CT = wc.read_tables()
    ok = True
    for i in (1, 2, 3, 4):
        m = measure(i, T, CT, seeds, target, **kw)
        v = {k: m[k] for k in CHECKS}
        ok = ok and all(v.values())
        if quiet:
            continue
        s = rung(T, i)
        hl, hc, hh = (hold_for_travel(T, s[k]) for k in ("low", "WindowCentre", "high"))
        print(f"i{i}: hop ring R{s['HopRingRadius']:.0f}, travel window {s['low']:.0f}..{s['high']:.0f} u "
              f"(hold {hl:.3f}..{hh:.3f} s, centre {hc:.3f}, +-{(hh - hl) / 2:.3f} s), plate {s['plate']:.0f} u under, "
              f"recharge at the far edge {recharge(T, s['high']):.2f} s")
        dh = m["d_hops"]
        print(f"    towers: D hops per tower {min(dh)}..{max(dh)} (mean {sum(dh) / len(dh):.2f}); up to "
              f"{m['tower_rings']} tower rings and {m['tiles']} tiles a course; last gap >= {m['min_long']:.0f} u; "
              f"{m['bare']} of {m['gaps']} gaps bare")
        if m["waits"]:
            w = sorted(m["waits"])
            print(f"    pacing (centre hops): drop every {sum(w) / len(w):.2f} s on average, "
                  f"{w[0]:.2f}..{w[-1]:.2f} s between presses")
        print(f"    hard far edge at most {m.get('far', 0):.0f} u past the authored one; plunge up to {T['PlungeCap']:.0f} u")
        print(f"    worst corporeal clearance {m['worst_clear']:.1f} u; farthest tower link {m['max_link']:.0f} u "
              f"(stand-down below {AI_FOLD_MIN_DISTANCE:.0f})")
        print("    " + "  ".join(f"{k} {'OK' if v[k] else 'FAIL'}" for k in CHECKS))
    if not quiet:
        print("\n" + ("ALL CHECKS PASS" if ok else "FAILED"))
    return ok


def fired_checks(seeds, target, T=None, **kw):
    T = T or read_tower()
    CT = wc.read_tables()
    fired = set()
    for i in (1, 2, 3, 4):
        m = measure(i, T, CT, seeds, target, **kw)
        fired |= {k for k in CHECKS if not m[k]}
    return fired


def self_test(seeds, target):
    print("self-test: the shipped tables")
    assert run(seeds, target, quiet=True), "the shipped tables do not pass their own checks"
    print("  pass\n")
    T0 = read_tower()

    def variant(**over):
        T = {k: (list(v) if isinstance(v, list) else v) for k, v in T0.items()}
        for k, v in over.items():
            T[k] = v
        return T

    controls = (
        ("no runway between hops (no time to recharge or roll)", dict(kw=dict(no_runway=True)), ("runway",)),
        ("towers laid without looking at what is already there (the cell, the clusters, each other)",
         dict(kw=dict(ignore_obstacles=True)), ("path", "shell")),
        ("a window so low the plate touches the cruise line",
         dict(T=variant(WindowCentre=[20.0] * 4, WindowHalfWidth=[10.0] * 4)), ("window",)),
        ("a long final gap nobody can reach (LongGapMin 5000)", dict(T=variant(LongGapMin=5000.0)), ("long",)),
        ("tower rings as wide as the cluster rings", dict(T=variant(HopRingRadius=[56.0, 50.0, 44.0, 40.0])),
         ("radius",)),
        ("Garrett's long-drop autopilot left at its shipped 420 u", dict(standdown=420.0), ("standdown",)),
    )
    for label, c, expect in controls:
        print(f"self-test: {label}")
        global AI_FOLD_MIN_DISTANCE
        keep = AI_FOLD_MIN_DISTANCE
        AI_FOLD_MIN_DISTANCE = c.get("standdown", keep)
        try:
            fired = fired_checks(seeds, target, T=c.get("T"), **c.get("kw", {}))
        finally:
            AI_FOLD_MIN_DISTANCE = keep
        print(f"  fired: {', '.join(sorted(fired)) or '(nothing)'}")
        missing = [e for e in expect if e not in fired]
        assert not missing, f"expected these to fire and they did not: {missing}"
        print("  pass\n")
    print("self-test: a danger share the hash does not honour")
    assert not danger_share_ok(0.35, n=6400) or danger_share_ok(0.35), "unreachable"
    assert danger_share_ok(0.0) and danger_share_ok(1.0), "share 0 / 1 must give none / all"
    assert not _share_close(0.35, 0.10), "the share check cannot tell 0.35 from 0.10"
    print("  pass\n")
    print("SELF-TEST PASS")
    return True


HARNESS = ROOT / "Tools/Build/waystation_prototype_harness"
HARNESS_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="Unity.cs" />
    <Compile Include="Stubs.cs" />
    <Compile Include="../../../Assets/_Scripts/Controller/Arcade/WaystationPrototype/WaystationTower.cs" />
    <Compile Include="../../../Assets/_Scripts/Controller/Arcade/Waystation/WaystationCourse.cs" />
    <Compile Include="../../../Assets/_Scripts/Controller/Arcade/Racing/RaceCourseGeometry.cs" />
  </ItemGroup>
</Project>
"""
COMPILE_POS_TOLERANCE = 0.05
COMPILE_AXIS_TOLERANCE = 0.05


def compile_and_compare(seeds, clusters):
    """Build and run the SHIPPED WaystationTower.cs and compare it to this model, gate for gate and tile for tile."""
    import shutil
    import subprocess

    stubs = (HARNESS / "Stubs.cs").read_text(encoding="utf-8")
    src = (ROOT / "Assets/_Scripts/Controller/Arcade/Racing/RaceCourseSource.cs").read_text(encoding="utf-8")
    m = re.search(r"DefaultInnerRadiusNucleusFactor\s*=\s*([0-9.]+)f", src)
    assert m and f"DefaultInnerRadiusNucleusFactor = {m.group(1)}f" in stubs, "Stubs.cs nucleus factor drifted"
    ease = (ROOT / "Assets/_Scripts/Controller/Vessel/R_VesselActions/TigerMothDropMath.cs").read_text(encoding="utf-8")
    assert "t * t * (3f - 2f * t)" in ease and "t * t * (3f - 2f * t)" in stubs, "Stubs.cs Ease01 drifted"

    dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
    if not Path(dotnet).exists():
        print("--compile needs a dotnet SDK (see .claude/skills/asset-surgery). SKIPPED, not passed.")
        return False
    csproj = HARNESS / "waystation_prototype_harness.csproj"
    if not csproj.exists():
        csproj.write_text(HARNESS_CSPROJ, encoding="utf-8")
    made = subprocess.run([dotnet, "build", "-v", "q", "--nologo"], cwd=HARNESS, capture_output=True, text=True)
    if made.returncode != 0:
        print(made.stdout[-4000:] or made.stderr[-4000:])
        print("the shipped course files DO NOT COMPILE")
        return False
    dll = HARNESS / "bin/Debug/net8.0/waystation_prototype_harness.dll"
    out = subprocess.run([dotnet, str(dll), str(seeds), str(clusters)], capture_output=True, text=True)
    if out.returncode != 0:
        print(out.stderr[-4000:])
        return False

    gates, tiles = {}, {}
    for line in out.stdout.splitlines():
        f = line.split()
        if f[0] == "G":
            gates.setdefault((int(f[1]), int(f[2])), []).append(tuple(float(x) for x in f[4:]))
        elif f[0] == "T":
            tiles[(int(f[1]), int(f[2]))] = (int(f[3]), int(f[4]), int(f[5]))

    T = read_tower()
    CT = wc.read_tables()
    worst_pos = worst_axis = 0.0
    mismatched = []
    compared = 0
    for i in (1, 2, 3, 4):
        s = rung(T, i)
        for seed in range(1, seeds + 1):
            laid, best, per, towers, gap = build(T, CT, seed, i, clusters)
            cs = gates.get((i, seed), [])
            if len(cs) != len(laid):
                mismatched.append(f"i{i} seed {seed}: C# laid {len(cs)} gates, the model {len(laid)}")
                continue
            for (p, ax, r), c in zip(laid, cs):
                worst_pos = max(worst_pos, mag(sub(p, c[0:3])))
                worst_axis = max(worst_axis, wc.angle(ax, c[3:6]))
                if abs(r - c[6]) > 1e-3:
                    mismatched.append(f"i{i} seed {seed}: a ring radius disagrees")
                compared += 1
            mt = walls(T, laid, s)
            used, n_tiles, n_danger = tiles[(i, seed)]
            if n_tiles != len(mt) or n_danger != sum(1 for _, d in mt if d):
                mismatched.append(f"i{i} seed {seed}: C# laid {n_tiles} tiles ({n_danger} danger), the model "
                                  f"{len(mt)} ({sum(1 for _, d in mt if d)})")
    ok = not mismatched and worst_pos <= COMPILE_POS_TOLERANCE and worst_axis <= COMPILE_AXIS_TOLERANCE
    print(f"compiled and RAN the shipped C#: {compared} gates compared over {seeds} seeds x 4 intensities")
    print(f"  worst position disagreement {worst_pos:.6f} u, axis {worst_axis:.6f} deg")
    for line in mismatched[:12]:
        print("  " + line)
    print("COMPILE MATCH" if ok else "COMPILE MISMATCH")
    return ok


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="exit 1 on any failing check")
    ap.add_argument("--self-test", action="store_true", help="prove the checks can fail")
    ap.add_argument("--compile", action="store_true", help="build and RUN the shipped C# and compare it to the model")
    ap.add_argument("--seeds", type=int, default=200)
    ap.add_argument("--target", type=int, default=CLUSTERS, help="clusters per race (the authored target)")
    a = ap.parse_args()
    if a.self_test:
        return 0 if self_test(min(a.seeds, 40), a.target) else 1
    if a.compile:
        return 0 if compile_and_compare(min(a.seeds, 40), a.target) else 1
    return 0 if run(a.seeds, a.target) else (1 if a.check else 0)


if __name__ == "__main__":
    sys.exit(main())
