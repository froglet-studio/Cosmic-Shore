#!/usr/bin/env python3
"""The offline authority for WAYSTATION's Drop Towers (Docs/Studios/TIGER_MOTH_TIME.md).

It MIRRORS Assets/_Scripts/Controller/Arcade/Waystation/WaystationTower.cs (the tower rings, the plates and
the tile kinds) on top of Garrett's own course model, `waystation_course.py`, which it IMPORTS and never edits: the
towers stand between Garrett's clusters, which are unchanged. The tower tables are read out of the C#, and the hull's numbers out
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
             autopilot (WaystationController) stands down on towers and the hop autopilot (same controller) takes them
  danger     the danger tiles take their share (within 0.03), none at share 0 and all at share 1
  radius     a tower ring's radius is never a cluster ring's at the same intensity

Lantern Towers (2026-11-11; WAYSTATION.md "Lantern Towers", Docs/Studios/TIGER_MOTH_TIME.md section 6), mirroring
WaystationTower's shelf, lanterns and crown gems and WaystationHopPlan:

  shelf      every hop's depth read back from the broadcast course is the depth Lay laid it for; a landing at the
             SHELF is recharged and rolled exactly at the next press point; the hash draws the authored tight share
  past       a landing 5 u past a hop's far edge cannot reach the next ring
  lantern    every post segment clears every corporeal flight line (hull + 4 u + its reach), every ring's mouth and
             every plate, and stays inside the cell
  gem        crown gems only at I4, one a tower, reachable by a short plunge and skipped by the deepest, clear of tiles
  aibudget   the autopilot's target, with a 30 fps frame and the live-speed carry, stays 2 u inside the plate and the
             far edge on every hop and seat
  (and the hull radius is asserted against TigerMoth.prefab's Hull collider beside the other hull numbers)

`--sweep` races fixed-depth pilots (centre, deep, the best constant depth, role-aware) against the lantern reader and
the autopilot's four seats through every tower, and prints what reading the lanterns saves against the BEST fixed plan.
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
CS_TOWER = ROOT / "Assets/_Scripts/Controller/Arcade/Waystation/WaystationTower.cs"
CS_PLAN = ROOT / "Assets/_Scripts/Controller/Arcade/Waystation/WaystationHopPlan.cs"
TIGER_MOTH = ROOT / "Assets/_Prefabs/Spacevessels/TigerMoth.prefab"
DROP_ASSET = ROOT / "Assets/_SO_Assets/VesselActions/TigerMoth/TigerMothDropAction.asset"

# Waystation's long-drop threshold, read off WaystationController so a retune moves the standdown check with it
def _ai_fold_min_distance():
    src = (ROOT / "Assets/_Scripts/Controller/Arcade/Waystation/WaystationController.cs").read_text(encoding="utf-8")
    m = re.search(r"float aiFoldMinDistance = ([0-9.]+)f;", src)
    assert m, "aiFoldMinDistance not found in WaystationController.cs"
    return float(m.group(1))


AI_FOLD_MIN_DISTANCE = _ai_fold_min_distance()
CLUSTERS = 5

add, sub, mul, dot, mag, cross, safe, perp = wc.add, wc.sub, wc.mul, wc.dot, wc.mag, wc.cross, wc.safe, wc.perp


def norm(v, fb):
    return safe(v, fb)


def project_on_plane(v, n):
    d = dot(n, n)
    return v if d < 1e-12 else sub(v, mul(n, dot(v, n) / d))


# ---------------------------------------------------------------- the tables, read from the C#
TABLES = ("HopRingRadius", "WindowCentre", "WindowHalfWidth", "RollGateShare", "DangerShare",
          # Lantern Towers (2026-11-11): the shelf, the deep band, the crown gem and the autopilot's seat error
          "ShelfShare", "ShelfCutMin", "ShelfCutMax", "DeepBand", "CrownGem", "AiErrorU")


def read_tower():
    src = CS_TOWER.read_text(encoding="utf-8")
    T = {}
    for field in TABLES:
        m = re.search(rf"{field}\s*=\s*new\[\]\s*\{{([^}}]*)\}}", src)
        assert m, f"{field} array not found in {CS_TOWER.name}"
        T[field] = [float(x.strip().rstrip("f")) for x in m.group(1).split(",")]
        assert len(T[field]) == 4, f"{field} is not four rungs"
    for name in ("Cruise", "DropSpeed", "DropEaseSeconds", "LandSeconds", "FullCooldownSeconds",
                 "FullCooldownDistance", "RollDegreesPerSecond", "HullRadius", "LandClear", "RingPastPlate",
                 "PipeClear", "ReactSeconds", "FinalMin", "PlungeCap", "ShellPad", "MembraneReach", "PlateMargin", "TileThickness",
                 "LongGapMin", "LanternBack", "LanternGap", "LanternDeepGap", "LanternSeg", "LanternSide", "GemSkip"):
        m = re.search(rf"public const float {name}\s*=\s*([0-9.]+)f;", src)
        assert m, f"const {name} not found in {CS_TOWER.name}"
        T[name] = float(m.group(1))
    for name in ("TilesPerSide", "SeedTries"):
        m = re.search(rf"public const int {name}\s*=\s*([0-9]+);", src)
        assert m, f"const {name} not found in {CS_TOWER.name}"
        T[name] = int(m.group(1))
    T["Lead"] = T["Cruise"] * (90.0 / T["RollDegreesPerSecond"] + T["ReactSeconds"])
    plan = CS_PLAN.read_text(encoding="utf-8")
    for name in ("AiGemShort", "AiSafety"):
        m = re.search(rf"public const float {name} = ([0-9.]+)f;", plan)
        assert m, f"{name} not found in {CS_PLAN.name}"
        T[name] = float(m.group(1))
    m = re.search(r"public const int TierOne = (\d+), TierTwo = (\d+), TierThree = (\d+);", plan)
    assert m, f"the streak tiers are not found in {CS_PLAN.name}"
    T["Tiers"] = tuple(int(x) for x in m.groups())
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
    hull = re.search(r"m_Name: Hull\n.*?--- !u!135 &\d+\nSphereCollider:.*?m_Radius: ([0-9.]+)", prefab, re.S)
    assert hull, "TigerMoth.prefab: the Hull SphereCollider is not found"
    assert abs(float(hull.group(1)) - T["HullRadius"]) < 1e-3, \
        f"TigerMoth's Hull radius is {hull.group(1)}, the tower says {T['HullRadius']}"
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
    s = {k: T[k][i] for k in TABLES}
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


def travel(T, hold, cruise=None):
    t = max(0.0, hold)
    release = T["DropSpeed"] * ease(t / T["DropEaseSeconds"])
    return fall(T, t) + T["LandSeconds"] * (release + max(0.0, T["Cruise"] if cruise is None else cruise)) * 0.5


def hold_for_travel(T, tr, cruise=None):
    lo, hi = 0.0, 4.0
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if travel(T, mid, cruise) < tr:
            lo = mid
        else:
            hi = mid
    return 0.5 * (lo + hi)


def recharge(T, tr):
    return T["FullCooldownSeconds"] * max(0.0, min(1.0, fall(T, hold_for_travel(T, tr)) / T["FullCooldownDistance"]))


def roll_s(T, deg):
    return abs(deg) / T["RollDegreesPerSecond"]


def runway(T, s, roll_deg):
    return runway_after(T, s["high"], roll_deg)


def runway_after(T, tr, roll_deg):
    """WaystationTower.RunwayAfter: the recharge after a hop of travel `tr` or the roll, whichever is longer, plus the
    reaction, as cruise distance."""
    return T["Cruise"] * (max(recharge(T, tr), roll_s(T, roll_deg)) + T["ReactSeconds"])


def is_tight_hop(gate, s):
    return s["ShelfShare"] > 0 and hash01(gate, 7, 0) < s["ShelfShare"]


def shelf_cut(gate, s):
    return s["ShelfCutMin"] + (s["ShelfCutMax"] - s["ShelfCutMin"]) * hash01(gate, 7, 1)


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


def seg_distance(a, b, c, d):
    """WaystationTower.SegmentDistance: the closed-form shortest distance between segments ab and cd."""
    u, v, w = sub(b, a), sub(d, c), sub(a, c)
    A, B, C, D, E = dot(u, u), dot(u, v), dot(v, v), dot(u, w), dot(v, w)
    # a point against a segment: the general case treats a zero-length segment as parallel and answers from a's end
    if C < 1e-9:
        return seg_point_distance(a, b, c)
    if A < 1e-9:
        return seg_point_distance(c, d, a)
    den = A * C - B * B
    sD = tD = den
    if den < 1e-6:
        sN, sD, tN, tD = 0.0, 1.0, E, C
    else:
        sN = B * E - C * D
        tN = A * E - B * D
        if sN < 0:
            sN, tN, tD = 0.0, E, C
        elif sN > sD:
            sN, tN, tD = sD, E + B, C
    if tN < 0:
        tN = 0.0
        if -D < 0:
            sN = 0.0
        elif -D > A:
            sN = sD
        else:
            sN, sD = -D, A
    elif tN > tD:
        tN = tD
        if -D + B < 0:
            sN = 0.0
        elif -D + B > A:
            sN = sD
        else:
            sN, sD = -D + B, A
    sc = 0.0 if abs(sN) < 1e-6 or sD < 1e-12 else sN / sD
    tc = 0.0 if abs(tN) < 1e-6 or tD < 1e-12 else tN / tD
    return mag(sub(add(w, mul(u, sc)), mul(v, tc)))


def post_pad(T):
    return T["LanternSeg"] * 0.5 + T["LanternSide"]


def post_clear(T):
    return T["HullRadius"] + 4.0 + post_pad(T)


def post_hits_plate(T, a, b, pl, pad):
    if seg_distance(a, b, pl["c"], pl["c"]) > pl["half"] * 1.415 + pad + T["TileThickness"]:
        return False
    return seg_hits_plate(T, a, b, pl, pad)


def post_hits_ring_seg(a, b, ring, pad):
    gp, gax, gr = ring
    if seg_distance(a, b, gp, gp) > gr + pad:
        return False
    # through the mouth: where the post crosses (or runs within pad of) the ring's plane, inside its radius
    da, db = dot(sub(a, gp), gax), dot(sub(b, gp), gax)
    if da * db <= 0 and abs(da - db) > 1e-6:
        x = add(a, mul(sub(b, a), da / (da - db)))
        if mag(project_on_plane(sub(x, gp), gax)) < gr + pad:
            return True
    for e, de in ((a, da), (b, db)):
        if abs(de) < pad and mag(project_on_plane(sub(e, gp), gax)) < gr + pad:
            return True
    u = perp(gax)
    w = cross(gax, u)
    for k in range(17):
        p = gp if k == 16 else add(gp, add(mul(u, gr * math.cos(k * math.pi / 8)), mul(w, gr * math.sin(k * math.pi / 8))))
        if seg_distance(a, b, p, p) < pad:
            return True
    return False


def add_posts(T, s, press, axis, arrive, end, into):
    """WaystationTower.AddPosts: Lay's copy of a hop's two lantern posts, near edge to foot."""
    if end < s["low"]:
        return
    across = safe(cross(axis, arrive), perp(axis))
    off = s["HopRingRadius"] + T["PlateMargin"]
    for sign in (1.0, -1.0):
        post = add(press, mul(across, sign * off))
        into.append((add(post, mul(axis, s["low"])), add(post, mul(axis, end))))


def deepest_landing_span(T, span, roll, spare, lo, hi):
    if _fits(T, span, hi, roll, spare):
        return hi
    if not _fits(T, span, lo, roll, spare):
        return lo
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if _fits(T, span, mid, roll, spare):
            lo = mid
        else:
            hi = mid
    return lo


def elements_clear(T, plates, segs, obst, inner_limit, outer_limit, ignore=False, posts=None):
    """WaystationTower.ElementsClear: new plates inside the cell and clear of every obstacle; new path clear of
    every obstacle plate; new lantern posts inside the cell and clear of every line, ring and plate, and new lines and
    plates clear of every post already standing."""
    if ignore:
        return True
    pad, pclear = post_pad(T), post_clear(T)
    for a, b in posts or ():
        if min(mag(a), mag(b)) < inner_limit or max(mag(a), mag(b)) > outer_limit:
            return False
        for c, d in obst["segs"] + segs:
            if seg_distance(a, b, c, d) < pclear:
                return False
        for ring in obst["rings"]:
            if post_hits_ring_seg(a, b, ring, pad):
                return False
        for pl in obst["plates"] + plates:
            if post_hits_plate(T, a, b, pl, pad):
                return False
    for a, b in obst["posts"]:
        for c, d in segs:
            if seg_distance(a, b, c, d) < pclear:
                return False
        for pl in plates:
            if post_hits_plate(T, a, b, pl, pad):
                return False
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
        no_cancel=False, ignore_obstacles=False, loose=False, tight_from_high=False, no_posts=False, **_):
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
        d_gate = len(out) + len(rings)
        arrive = heading if lateral == 1 else last_side
        shelf = high - shelf_cut(d_gate, s) if (is_tight_hop(d_gate, s) and not loose) else high
        chosen = None
        for side, roll in options:
            run = 0.0 if no_runway else runway_after(T, shelf, roll)
            if tight_from_high and shelf < high:
                run = runway(T, s, roll)            # self-test: a tight hop's runway sized from the far edge
            step = shelf + run
            if used + step + high > avail:
                continue
            lpress = add(press, mul(down, step))
            nxt = add(lpress, mul(side, lateral_stride))
            new_plates = [make_plate(add(lpress, mul(down, pipe)), down, side, half),
                          make_plate(add(lpress, mul(side, s["plate"])), side, down, half),
                          make_plate(add(nxt, mul(side, pipe)), side, down, half),
                          make_plate(add(nxt, mul(down, s["plate"])), down, side, half)]
            new_segs = [(add(press, mul(down, low)), lpress), (add(lpress, mul(side, low)), nxt)] + finals(nxt)
            new_posts = []
            if not no_posts:
                reach = high + FAR_EDGE_REACH
                add_posts(T, s, press, down, arrive,
                          deepest_landing_span(T, step, roll, 0.0, low, reach) - T["LanternBack"], new_posts)
                add_posts(T, s, lpress, side, down,
                          deepest_landing_span(T, lateral_stride, 180.0, 0.0, low, reach) - T["LanternBack"], new_posts)
            if elements_clear(T, new_plates, new_segs, obst, inner_limit, outer_limit, ignore_obstacles, new_posts):
                chosen = (side, roll, step, lpress, nxt, new_plates, new_segs, new_posts)
                break
        if chosen is None:
            break
        side, roll, step, lpress, nxt, new_plates, new_segs, new_posts = chosen
        obst["posts"].extend(new_posts)
        rings.append((add(press, mul(down, s["ring"])), down, s["HopRingRadius"]))
        steps.append(dict(press=press, x=down, roll=90.0 if d_hops == 1 else 180.0, shelf=shelf, gate=d_gate,
                          tight=shelf < high))
        rings.append((add(lpress, mul(side, s["ring"])), side, s["HopRingRadius"]))
        steps.append(dict(press=lpress, x=side, roll=roll, shelf=high, gate=d_gate + 1, tight=False))
        obst["plates"].extend(new_plates)
        obst["segs"].extend(new_segs[:2])
        obst["rings"].extend(rings[-2:])
        final_segs = new_segs[2:]
        press = nxt
        used += step
        last_side = side
        belly = mul(side, -1)
        d_hops += 1
        lateral += 1
    plunge = max(0.0, min(T["PlungeCap"], avail - (used + high)))
    rings.append((add(press, mul(down, s["ring"] + plunge)), down, s["HopRingRadius"]))
    steps.append(dict(press=press, x=down, roll=90.0 if d_hops == 1 else 180.0, plunge=plunge,
                      shelf=None, gate=len(out) + len(rings) - 1, tight=False))
    out.extend(rings)
    obst["segs"].extend(final_segs)
    obst["rings"].append(rings[-1])
    # the plunge's landing stretch (and the crown gem on it): a later tower's plate there would be landed in
    obst["segs"].append((add(press, mul(down, low + plunge)), add(press, mul(down, high + plunge))))
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
    obst = dict(segs=[], plates=[], rings=list(clusters), posts=[])
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



# ---------------------------------------------------------------- the shelf, lanterns and crown gems (Lantern Towers)
def angle(a, b):
    return wc.angle(a, b)


def has_shelf(course, i, s):
    """WaystationTower.HasShelf: every tower ring but a tower's last (the plunge)."""
    return 1 <= i and i + 1 < len(course) and is_tower_ring(course[i], s) and is_tower_ring(course[i + 1], s)


def is_plunge(course, i, s):
    return i >= 1 and is_tower_ring(course[i], s) and (i + 1 >= len(course) or not is_tower_ring(course[i + 1], s))


def roll_before(course, i):
    return angle(mul(course[i - 1][1], -1), course[i + 1][1])


SHELF_TOL = 0.05


FAR_EDGE_REACH = 400.0


def _fits(T, span, landing, roll, spare):
    return span - landing + SHELF_TOL >= T["Cruise"] * (max(recharge(T, landing), roll_s(T, roll)) + spare)


def deepest_landing(T, course, i, s, spare, lo, hi):
    """WaystationTower.DeepestLanding."""
    span = dot(sub(press_of(T, course, i + 1, s), press_of(T, course, i, s)), course[i][1])
    roll = roll_before(course, i)
    if _fits(T, span, hi, roll, spare):
        return hi
    if not _fits(T, span, lo, roll, spare):
        return lo
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if _fits(T, span, mid, roll, spare):
            lo = mid
        else:
            hi = mid
    return lo


def shelf_depth(T, course, i, s):
    """WaystationTower.ShelfDepth, from the broadcast course alone: recharged and rolled exactly at the next press."""
    if not has_shelf(course, i, s):
        return s["high"]
    return deepest_landing(T, course, i, s, 0.0, s["low"], s["high"] + FAR_EDGE_REACH)


def laid_depth(T, course, i, s):
    """WaystationTower.LaidDepth: the depth Lay sized the hop for (the shelf with the reaction kept)."""
    if not has_shelf(course, i, s):
        return s["high"]
    return deepest_landing(T, course, i, s, T["ReactSeconds"], s["low"], s["high"])


def far_edge_of(T, course, i, s):
    """WaystationTower.FarEdge: no reaction left, the press late by the next mouth's radius less the hull's."""
    if not has_shelf(course, i, s):
        return float("inf")
    late = (course[i + 1][2] - T["HullRadius"]) / T["Cruise"]
    return deepest_landing(T, course, i, s, -late, s["low"], s["high"] + FAR_EDGE_REACH)


def lantern_end(T, course, i, s):
    return shelf_depth(T, course, i, s) - T["LanternBack"]


def plunge_of(T, course, i, s):
    return dot(sub(course[i][0], press_of(T, course, i, s)), course[i][1]) - s["ring"]


def gem_depth(T, course, i, s):
    return s["high"] + plunge_of(T, course, i, s) - T["GemSkip"]


def lanterns(T, course, s):
    """WaystationTower.Lanterns: (position, gate, deep) for every post segment, in the C#'s order."""
    out = []
    offset = s["HopRingRadius"] + T["PlateMargin"]
    for i in range(1, len(course)):
        if not has_shelf(course, i, s):
            continue
        end = lantern_end(T, course, i, s)
        if end < s["low"]:
            continue
        axis = course[i][1]
        press = press_of(T, course, i, s)
        across = safe(cross(axis, course[i - 1][1]), perp(axis))
        deep_from = max(s["low"], end - s["DeepBand"])
        for side in (offset, -offset):
            post = add(press, mul(across, side))
            d = end
            while d >= deep_from - 1e-3:
                out.append((add(post, mul(axis, d)), i, True))
                d -= T["LanternDeepGap"]
            d -= T["LanternGap"] - T["LanternDeepGap"]
            while d >= s["low"] - 1e-3:
                out.append((add(post, mul(axis, d)), i, False))
                d -= T["LanternGap"]
    return out


def crown_gems(T, course, s):
    if s["CrownGem"] <= 0:
        return []
    return [(add(press_of(T, course, i, s), mul(course[i][1], gem_depth(T, course, i, s))), i)
            for i in range(1, len(course)) if is_plunge(course, i, s)]


# ---------------------------------------------------------------- the hop plan (WaystationHopPlan.cs)
SHORT, CLEAN, DEEP, PAST = "Short", "Clean", "Deep", "Past"


def grade(T, depth, shelf, far, s):
    """WaystationHopPlan.Grade."""
    if depth < s["low"]:
        return SHORT
    if depth > far:
        return PAST
    return DEEP if depth >= shelf - T["LanternBack"] - s["DeepBand"] else CLEAN


def grade_on(T, course, i, depth, s):
    """WaystationHopPlan.GradeOn."""
    if is_plunge(course, i, s):
        return grade(T, depth - plunge_of(T, course, i, s), s["high"], float("inf"), s)
    return grade(T, depth, shelf_depth(T, course, i, s), far_edge_of(T, course, i, s), s)


def ai_error(seat, gate, s):
    return (hash01(seat + 1, gate, 4243) * 2 - 1) * s["AiErrorU"]


def ai_target(T, course, i, s, seat):
    if is_plunge(course, i, s):
        if s["CrownGem"] > 0:
            return gem_depth(T, course, i, s) - T["AiGemShort"]
        return s["WindowCentre"] + plunge_of(T, course, i, s)
    aim = lantern_end(T, course, i, s) - s["DeepBand"] * 0.5 + ai_error(seat, i, s)
    return max(s["low"] + T["AiSafety"], min(far_edge_of(T, course, i, s) - T["AiSafety"], aim))


def streak_step(T, streak, g, missed):
    if missed or g in (SHORT, PAST):
        return 0
    return streak + (1 if g == DEEP else 0)


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
        tr = hold_travel if st.get("shelf") is None else min(hold_travel, st["shelf"])
        land = add(st["press"], mul(st["x"], tr))
        nxt = steps[k + 1]["press"] if k + 1 < len(steps) else plan["next_ring"]
        worst = min(worst, segment_clear(plates, land, nxt, clearance, thick))
        if k + 1 < len(steps):
            cruise = dot(sub(nxt, land), st["x"])
            t_avail = cruise / T["Cruise"]
            need = max(recharge(T, tr), roll_s(T, steps[k + 1]["roll"]))
            waits.append(hold_for_travel(T, tr) + T["LandSeconds"] + t_avail)
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


# ---------------------------------------------------------------- the Lantern Towers checks
AI_FRAME_U = 15.0     # one 60 fps frame of a 900 u/s fall, or half a 30 fps frame once the release is on the nearest frame
AI_CARRY_U = 8.0      # the landing carry at live speed against the 67 u/s the plan assumes (wall slowdowns, Afterburn)
POST_CLEAR = 4.0      # a post's clearance (u) from the hull on every corporeal line, beyond the hull's radius


def seg_point_distance(a, b, p):
    ab = sub(b, a)
    L2 = dot(ab, ab)
    t = 0.0 if L2 < 1e-12 else max(0.0, min(1.0, dot(sub(p, a), ab) / L2))
    return mag(sub(p, add(a, mul(ab, t))))


def flight_lines(T, s, plan):
    """Every corporeal line a pilot flies through one tower, landing at the window's near edge, its centre or its deepest
    legal landing (the shelf), into the next press point and on into the next cluster."""
    lines = []
    steps = plan["steps"]
    lines.append((plan["exit"][0], steps[0]["press"]))
    for k, st in enumerate(steps):
        nxt = steps[k + 1]["press"] if k + 1 < len(steps) else plan["next_ring"]
        cap = st["shelf"] if st.get("shelf") is not None else s["high"]
        for d in (s["low"], min(s["WindowCentre"], cap), cap):
            lines.append((add(st["press"], mul(st["x"], d)), nxt))
    return lines


def post_hits_ring(p, ring, pad):
    c, ax, r = ring
    d = sub(p, c)
    along = dot(d, ax)
    radial = mag(sub(d, mul(ax, along)))
    return abs(along) < pad and radial < r + pad


# ---------------------------------------------------------------- measure
def measure(intensity, T, CT, seeds, target, **kw):
    s = rung(T, intensity)
    i = intensity - 1
    m = dict(window=True, runway=True, path=True, edges=True, mouths=True, shell=True, towers=True, long=True,
             standdown=True, worst_clear=float("inf"), max_link=0.0, min_long=1e9, drops=[], waits=[], times=[],
             tiles=0, d_hops=[], tower_rings=0, short_long=0,
             shelf=True, past=True, lantern=True, gem=True, aibudget=True, tight=0, d_total=0, posts=0, gems=0,
             worst_post=float("inf"), worst_budget=float("inf"), shelf_err=0.0)
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
            # Lantern Towers: the shelf the course says is the shelf Lay sized; a landing at it presses in time, one
            # just past the hard far edge cannot reach the next ring; the autopilot's landing budget fits between.
            steps = pl_plan["steps"]
            for k, st in enumerate(steps):
                g = st["gate"]
                if st.get("shelf") is None:
                    # the plunge: the autopilot lands past the ring and inside the window, with its frame and carry
                    if not kw.get("ignore_ai"):
                        tgt = ai_target(T, laid, g, s, 0)
                        lo_m = tgt - AI_FRAME_U - AI_CARRY_U - (s["low"] + plunge_of(T, laid, g, s))
                        m["worst_budget"] = min(m["worst_budget"], lo_m)
                        if lo_m < 2.0 - 1e-6:
                            m["aibudget"] = False
                    continue
                if k % 2 == 0:
                    m["d_total"] += 1
                    m["tight"] += 1 if st["tight"] else 0
                ld = laid_depth(T, laid, g, s)
                sd = shelf_depth(T, laid, g, s)
                m["shelf_err"] = max(m["shelf_err"], abs(ld - st["shelf"]))
                if abs(ld - st["shelf"]) > 0.5:
                    m["shelf"] = False   # the course does not say what Lay laid
                fe = far_edge_of(T, laid, g, s)
                if overshoot(T, s, steps, k, ld) > -T["ReactSeconds"] * T["Cruise"] + 0.5:
                    m["shelf"] = False   # a landing at the laid depth does not keep the reaction
                if abs(overshoot(T, s, steps, k, sd)) > 0.5 or not (ld - 0.5 <= sd <= fe + 0.5):
                    m["shelf"] = False   # the shelf is not "recharged exactly at the next press"
                if overshoot(T, s, steps, k, fe + 5.0) <= steps[k + 1].get("r", s["HopRingRadius"]) - T["HullRadius"]:
                    m["past"] = False
                for seat in range(4):
                    tgt = ai_target(T, laid, g, s, seat) if not kw.get("ignore_ai") else s["WindowCentre"]
                    lo_m = tgt - AI_FRAME_U - AI_CARRY_U - s["low"]
                    hi_m = fe - (tgt + AI_FRAME_U + AI_CARRY_U)
                    m["worst_budget"] = min(m["worst_budget"], lo_m, hi_m)
                    if lo_m < 2.0 - 1e-6 or hi_m < 2.0 - 1e-6:
                        m["aibudget"] = False
        # the lanterns: clear of every corporeal line, ring mouth and plate, and inside the cell
        posts = lanterns(T, laid, s) if not kw.get("posts_on_axis") else [
            (add(press_of(T, laid, g, s), mul(laid[g][1], d0)), g, dp)
            for (p0, g, dp) in lanterns(T, laid, s) for d0 in [dot(sub(p0, press_of(T, laid, g, s)), laid[g][1])]]
        m["posts"] = max(m["posts"], len(posts))
        lines = [ln for pl_plan in plan for ln in flight_lines(T, s, pl_plan)]
        pad = T["LanternSeg"] * 0.5 + T["LanternSide"]
        for p0, g, _ in posts:
            near = min(seg_point_distance(a, b, p0) for a, b in lines) if lines else float("inf")
            m["worst_post"] = min(m["worst_post"], near)
            if near < T["HullRadius"] + POST_CLEAR + pad:
                m["lantern"] = False
            if any(post_hits_ring(p0, ring, pad) for ring in laid):
                m["lantern"] = False
            if any(plate_distance(pl, p0, T["TileThickness"]) < pad for pl in plates):
                m["lantern"] = False
            if mag(p0) > wc.COURSE_OUTER + T["MembraneReach"] or mag(p0) < wc.INNER / 1.22 + T["ShellPad"]:
                m["lantern"] = False
        # the crown gems: I4 only, one a tower, on the plunge's line past its ring, clear of every tile and mouth
        gems = crown_gems(T, laid, s) if not kw.get("gem_at_low") else [
            (add(press_of(T, laid, g, s), mul(laid[g][1], s["low"] + plunge_of(T, laid, g, s))), g)
            for (_, g) in crown_gems(T, laid, s)]
        m["gems"] = max(m["gems"], len(gems))
        if s["CrownGem"] <= 0 and gems:
            m["gem"] = False
        if s["CrownGem"] > 0 and len(gems) != sum(1 for t in towers if t >= 1):
            m["gem"] = False
        for gp, g in gems:
            depth = dot(sub(gp, press_of(T, laid, g, s)), laid[g][1])
            pl_depth = plunge_of(T, laid, g, s)
            if depth < s["low"] + pl_depth + GEM_PICKUP or depth > s["high"] + pl_depth - GEM_PICKUP:
                m["gem"] = False   # a short landing must reach it, a max-depth landing must skip it
            if any(plate_distance(pl, gp, T["TileThickness"]) < T["HullRadius"] + 1.0 for pl in plates):
                m["gem"] = False
            if any(post_hits_ring(gp, ring, GEM_PICKUP) for ring in laid):
                m["gem"] = False
    m["danger"] = danger_share_ok(s["DangerShare"])
    # The hash draws the authored share (a tight hop is shorter, so it fits a gap more often: the LAID share runs
    # higher, and is reported, not gated)
    hashed = sum(1 for g in range(4000) if is_tight_hop(g, s)) / 4000.0
    if abs(hashed - s["ShelfShare"]) > 0.03:
        m["shelf"] = False
    m["tight_share"] = m["tight"] / m["d_total"] if m["d_total"] else 0.0
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


CHECKS = ("window", "runway", "path", "edges", "mouths", "shell", "towers", "long", "standdown", "danger", "radius",
          "shelf", "past", "lantern", "gem", "aibudget")
GEM_PICKUP = 12.0     # the crown gem's reach (u): a landing within it, or a line through it, collects it


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
        print(f"    lanterns: tight D hops {m.get('tight_share', 0):.2f} (authored {s['ShelfShare']:.2f}), shelf drift "
              f"{m['shelf_err']:.3f} u; up to {m['posts']} post segments a course, nearest {m['worst_post']:.1f} u to a "
              f"flight line; up to {m['gems']} crown gems; autopilot budget margin {m['worst_budget']:.1f} u")
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
        ("the long-drop autopilot back at its pre-tower 420 u", dict(standdown=420.0), ("standdown",)),
        # Lantern Towers (2026-11-11)
        ("a tight hop's runway sized from the window's far edge", dict(kw=dict(tight_from_high=True)), ("shelf",)),
        ("towers laid without keeping clear of the lantern posts", dict(kw=dict(no_posts=True)), ("lantern",)),
        ("lantern posts on the drop line itself", dict(kw=dict(posts_on_axis=True)), ("lantern",)),
        ("the crown gem at the plunge's near edge (a short plunge lands in it, so there is no choice)",
         dict(kw=dict(gem_at_low=True)), ("gem",)),
        ("an autopilot that ignores the lanterns (window centre on every hop)", dict(kw=dict(ignore_ai=True)),
         ("aibudget",)),
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
    print("self-test: a hull radius the prefab does not have (7)")
    try:
        assert_hull_matches(variant(HullRadius=7.0))
        raise AssertionError("assert_hull_matches accepted a hull radius of 7")
    except AssertionError as e:
        if "accepted" in str(e):
            raise
    print("  pass\n")
    print("self-test: a danger share the hash does not honour")
    assert not danger_share_ok(0.35, n=6400) or danger_share_ok(0.35), "unreachable"
    assert danger_share_ok(0.0) and danger_share_ok(1.0), "share 0 / 1 must give none / all"
    assert not _share_close(0.35, 0.10), "the share check cannot tell 0.35 from 0.10"
    print("  pass\n")
    print("SELF-TEST PASS")
    return True


# ---------------------------------------------------------------- the sweep: does reading the lanterns pay?
MISS_SECONDS = 2 * math.pi * 85.3 / 67.0   # a missed tower ring costs one turning circle on the 45 deg/s hull (8.0 s)
PLAIN_HIT_SECONDS = 0.3                    # a plain tile: x0.5..0.9 throttle easing back over 1 s
DANGER_HIT_SECONDS = 1.5                   # a danger tile: x0 easing back over 3 s (and a permanent burn, not priced)


def tower_clock(T, s, laid, plan, depth_of, rng, noise_u):
    """One pilot through one tower: seconds from the first press to the next cluster's first ring, misses, short
    landings, crown gems, and the grades it landed. `depth_of(k, step)` is the depth the pilot aims for on step k."""
    steps = plan["steps"]
    t = 0.0
    misses = shorts = gems = 0
    grades = []
    gem_pts = {g: p for p, g in crown_gems(T, laid, s)}
    for k, st in enumerate(steps):
        aim = depth_of(k, st)
        tr = aim + rng.uniform(-noise_u, noise_u) + rng.uniform(-AI_CARRY_U, AI_CARRY_U)
        g = st["gate"]
        last = k + 1 >= len(steps)
        floor = s["low"] + (st.get("plunge", 0.0) if last else 0.0)
        if tr < floor:
            shorts += 1
            t += (1 - s["DangerShare"]) * PLAIN_HIT_SECONDS + s["DangerShare"] * DANGER_HIT_SECONDS
            tr = floor
        grades.append(grade_on(T, laid, g, tr, s))
        t += hold_for_travel(T, tr) + T["LandSeconds"]
        land = add(st["press"], mul(st["x"], tr))
        if last:
            if g in gem_pts and dot(sub(gem_pts[g], land), st["x"]) >= -GEM_PICKUP:
                gems += 1
            t += mag(sub(plan["next_ring"], land)) / T["Cruise"]
            break
        cruise = dot(sub(steps[k + 1]["press"], land), st["x"]) / T["Cruise"]
        need = max(recharge(T, tr), roll_s(T, steps[k + 1]["roll"]))
        late = (need - cruise) * T["Cruise"]
        if late > s["HopRingRadius"] - T["HullRadius"]:
            misses += 1
            t += max(cruise, 0.0) + MISS_SECONDS
        else:
            t += max(cruise, need)
    return t, misses, shorts, gems, grades


def cluster_clock(T, laid, s):
    """The race outside the towers, the same for every pilot: cluster legs at cruise (plus a turn allowance at 45
    deg/s), each exit gate to its tower's first press (the roll), and the long last gap as one long drop."""
    t = 0.0
    n = len(laid)
    last_gap = None
    for i in range(1, n):
        a, b = laid[i - 1], laid[i]
        if is_tower_ring(a, s) or is_tower_ring(b, s):
            if is_tower_ring(b, s) and not is_tower_ring(a, s):
                t += T["Lead"] / T["Cruise"]
            continue
        d = mag(sub(b[0], a[0]))
        if d >= T["LongGapMin"] and (last_gap is None or i > last_gap):
            last_gap = i
    for i in range(1, n):
        a, b = laid[i - 1], laid[i]
        if is_tower_ring(a, s) or is_tower_ring(b, s):
            continue
        d = mag(sub(b[0], a[0]))
        if i == last_gap:
            t += d / T["DropSpeed"] + T["LandSeconds"] + 1.0
        else:
            t += d / T["Cruise"] + angle(a[1], b[1]) / 45.0 * 0.5
    return t


def sweep(seeds, target, fps=60):
    """Race fixed-depth pilots against the lantern reader and the autopilot's four seats through every tower, and
    report what reading the lanterns saves (strict: against the BEST fixed plan, not against today's centre pilot)."""
    import random
    import statistics
    T = read_tower()
    CT = wc.read_tables()
    q = 0.5 * T["DropSpeed"] / fps      # a release on the nearest frame
    print(f"sweep: {seeds} seeds x 4 intensities, {target} clusters, release noise +-{q:.1f} u ({fps} fps) and carry "
          f"+-{AI_CARRY_U:.0f} u; a miss costs {MISS_SECONDS:.1f} s, a short landing "
          f"{PLAIN_HIT_SECONDS}/{DANGER_HIT_SECONDS} s (plain/danger)")
    rows = []
    for i in (1, 2, 3, 4):
        s = rung(T, i)
        courses = []
        for seed in range(1, seeds + 1):
            plan = []
            laid, best, per, towers, gap = build(T, CT, seed, i, target, plan)
            courses.append((seed, laid, plan))

        def run(policy, name, seat=None):
            tower_t, race_t, miss, short, gem = [], [], 0, 0, 0
            deep = total = 0
            for seed, laid, plan in courses:
                rng = random.Random(seed * 7919 + i * 104729 + (seat or 0) * 31 + len(name))
                tt = 0.0
                for pl in plan:
                    t, mi, sh, ge, gr = tower_clock(T, s, laid, pl, lambda k, st: policy(laid, k, st), rng, q)
                    tt += t
                    miss += mi
                    short += sh
                    gem += ge
                    deep += sum(1 for x in gr if x == DEEP)
                    total += len(gr)
                tower_t.append(tt)
                race_t.append(tt + cluster_clock(T, laid, s))
            return dict(name=name, tower=tower_t, race=race_t, miss=miss / len(courses), short=short / len(courses),
                        gems=gem / len(courses), deep=deep / max(1, total))

        def plunge_deep(laid, st):
            return s["high"] + st.get("plunge", 0.0) - T["LanternBack"]

        centre = run(lambda laid, k, st: s["WindowCentre"] + st.get("plunge", 0.0) if st.get("shelf") is None
                     else s["WindowCentre"], "centre (today's AI)")
        deep_p = run(lambda laid, k, st: plunge_deep(laid, st) if st.get("shelf") is None else s["high"] - T["LanternBack"],
                     "deep (far edge - 15)")
        best_c = None
        for c in range(int(s["low"]) + 5, int(s["high"]) + 1, 5):
            r = run(lambda laid, k, st, c=c: plunge_deep(laid, st) if st.get("shelf") is None else float(c), f"constant {c}")
            if best_c is None or statistics.mean(r["race"]) < statistics.mean(best_c["race"]):
                best_c = r
        c_best = float(best_c["name"].split()[1])
        role = run(lambda laid, k, st: plunge_deep(laid, st) if st.get("shelf") is None
                   else (s["high"] - T["LanternBack"] if k % 2 == 1 else c_best), f"role-aware (deep sideways, {c_best:.0f} down)")
        reader = run(lambda laid, k, st: plunge_deep(laid, st) if st.get("shelf") is None
                     else lantern_end(T, laid, st["gate"], s), "reader (the lanterns' foot)")
        expert = run(lambda laid, k, st: plunge_deep(laid, st) if st.get("shelf") is None
                     else min(s["high"] + 50.0, far_edge_of(T, laid, st["gate"], s)) - q - AI_CARRY_U - 1.0,
                     "expert (the hard edge less its noise)")
        seats = [run(lambda laid, k, st, seat=seat: ai_target(T, laid, st["gate"], s, seat), f"autopilot seat {seat}", seat)
                 for seat in range(4)]
        fixed = min((best_c, role), key=lambda r: statistics.mean(r["tower"]))
        mean = statistics.mean
        strict_tower = (mean(fixed["tower"]) - mean(reader["tower"])) / mean(fixed["tower"]) if mean(fixed["tower"]) else 0
        strict_race = (mean(fixed["race"]) - mean(reader["race"])) / mean(fixed["race"])
        strict_expert = (mean(fixed["tower"]) - mean(expert["tower"])) / mean(fixed["tower"])
        naive = (mean(centre["tower"]) - mean(reader["tower"])) / mean(centre["tower"]) if mean(centre["tower"]) else 0
        print(f"\ni{i}: towers {mean(r_len(courses, s)):.1f} rings a race")
        for r in (centre, deep_p, best_c, role, reader, expert, *seats):
            print(f"    {r['name']:<36} towers {mean(r['tower']):6.1f} s  race {statistics.median(r['race']):6.1f} s "
                  f"(median)  misses {r['miss']:.2f}  short {r['short']:.2f}  deep {100 * r['deep']:4.0f}%  gems {r['gems']:.2f}")
        print(f"    reading the lanterns saves {100 * strict_tower:.1f}% of tower time and {100 * strict_race:.1f}% of the "
              f"race against the best fixed plan ({fixed['name']}); {100 * naive:.1f}% of tower time against today's AI; "
              f"an expert at the hard edge saves {100 * strict_expert:.1f}%")
        ai_race = [statistics.median(r["race"]) for r in seats]
        rows.append(dict(i=i, strict_tower=strict_tower, strict_race=strict_race, naive=naive,
                         race=statistics.median(reader["race"]), ai_miss=max(r["miss"] for r in seats),
                         ai_race=max(ai_race), centre_miss=centre["miss"]))
    print("\nsummary   strict tower   strict race   vs today's AI   reader race   AI misses/race   today's AI misses/race")
    for r in rows:
        print(f"  i{r['i']}      {100 * r['strict_tower']:6.1f}%       {100 * r['strict_race']:6.1f}%       "
              f"{100 * r['naive']:6.1f}%      {r['race']:6.1f} s      {r['ai_miss']:.2f}             {r['centre_miss']:.2f}")
    return rows


def r_len(courses, s):
    return [sum(1 for g in laid if is_tower_ring(g, s)) for _, laid, _ in courses]


HARNESS = ROOT / "Tools/Build/waystation_tower_harness"
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
    <Compile Include="../../../Assets/_Scripts/Controller/Arcade/Waystation/WaystationTower.cs" />
    <Compile Include="../../../Assets/_Scripts/Controller/Arcade/Waystation/WaystationHopPlan.cs" />
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
    hold = "(distance - landing) / dropSpeed + easeSeconds * 0.5f"
    assert hold in ease and hold in stubs, "Stubs.cs HoldSecondsFor drifted"

    dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
    if not Path(dotnet).exists():
        print("--compile needs a dotnet SDK (see .claude/skills/asset-surgery). SKIPPED, not passed.")
        return False
    csproj = HARNESS / "waystation_tower_harness.csproj"
    if not csproj.exists() or csproj.read_text(encoding="utf-8") != HARNESS_CSPROJ:
        csproj.write_text(HARNESS_CSPROJ, encoding="utf-8")
    made = subprocess.run([dotnet, "build", "-v", "q", "--nologo"], cwd=HARNESS, capture_output=True, text=True)
    if made.returncode != 0:
        print(made.stdout[-4000:] or made.stderr[-4000:])
        print("the shipped course files DO NOT COMPILE")
        return False
    dll = HARNESS / "bin/Debug/net8.0/waystation_tower_harness.dll"
    out = subprocess.run([dotnet, str(dll), str(seeds), str(clusters)], capture_output=True, text=True)
    if out.returncode != 0:
        print(out.stderr[-4000:])
        return False

    gates, tiles, posts, shelves, gems = {}, {}, {}, {}, {}
    for line in out.stdout.splitlines():
        f = line.split()
        key = (int(f[1]), int(f[2]))
        if f[0] == "G":
            gates.setdefault(key, []).append(tuple(float(x) for x in f[4:]))
        elif f[0] == "T":
            tiles[key] = (int(f[3]), int(f[4]), int(f[5]))
        elif f[0] == "L":
            posts.setdefault(key, []).append((int(f[3]), tuple(float(x) for x in f[4:7]), f[7] == "1"))
        elif f[0] == "S":
            shelves.setdefault(key, []).append((int(f[3]),) + tuple(float(x) for x in f[4:8]))
        elif f[0] == "C":
            gems.setdefault(key, []).append(tuple(float(x) for x in f[3:6]))

    T = read_tower()
    CT = wc.read_tables()
    worst_pos = worst_axis = worst_post = worst_shelf = worst_gem = 0.0
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
            # Lantern Towers: the posts, each hop's shelf / laid depth / far edge / autopilot target, the gems
            mp = lanterns(T, laid, s)
            cp = posts.get((i, seed), [])
            if len(cp) != len(mp) or any(g != cg or dp != cd for (p, g, dp), (cg, cpos, cd) in zip(mp, cp)):
                mismatched.append(f"i{i} seed {seed}: C# laid {len(cp)} lantern segments, the model {len(mp)}")
            else:
                for (p, g, dp), (cg, cpos, cd) in zip(mp, cp):
                    worst_post = max(worst_post, mag(sub(p, cpos)))
            for gi, c_shelf, c_laid, c_far, c_ai in shelves.get((i, seed), []):
                worst_shelf = max(worst_shelf, abs(c_shelf - shelf_depth(T, laid, gi, s)),
                                  abs(c_laid - laid_depth(T, laid, gi, s)),
                                  abs(min(c_far, 1e6) - min(far_edge_of(T, laid, gi, s), 1e6)),
                                  abs(c_ai - ai_target(T, laid, gi, s, gi % 4)))
            if len(shelves.get((i, seed), [])) != sum(1 for gi in range(1, len(laid)) if has_shelf(laid, gi, s)):
                mismatched.append(f"i{i} seed {seed}: the C# and the model disagree on which rings have a shelf")
            mg = crown_gems(T, laid, s)
            cg = gems.get((i, seed), [])
            if len(mg) != len(cg):
                mismatched.append(f"i{i} seed {seed}: C# stood {len(cg)} crown gems, the model {len(mg)}")
            else:
                for (p, _), c in zip(mg, cg):
                    worst_gem = max(worst_gem, mag(sub(p, c)))
            mt = walls(T, laid, s)
            used, n_tiles, n_danger = tiles[(i, seed)]
            if n_tiles != len(mt) or n_danger != sum(1 for _, d in mt if d):
                mismatched.append(f"i{i} seed {seed}: C# laid {n_tiles} tiles ({n_danger} danger), the model "
                                  f"{len(mt)} ({sum(1 for _, d in mt if d)})")
    ok = (not mismatched and worst_pos <= COMPILE_POS_TOLERANCE and worst_axis <= COMPILE_AXIS_TOLERANCE
          and worst_post <= COMPILE_POS_TOLERANCE and worst_shelf <= COMPILE_POS_TOLERANCE * 4
          and worst_gem <= COMPILE_POS_TOLERANCE)
    print(f"compiled and RAN the shipped C#: {compared} gates compared over {seeds} seeds x 4 intensities")
    print(f"  worst position disagreement {worst_pos:.6f} u, axis {worst_axis:.6f} deg")
    print(f"  lantern posts {worst_post:.6f} u, shelves / laid depths / far edges / autopilot targets {worst_shelf:.6f} u, "
          f"crown gems {worst_gem:.6f} u")
    for line in mismatched[:12]:
        print("  " + line)
    print("COMPILE MATCH" if ok else "COMPILE MISMATCH")
    return ok


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="exit 1 on any failing check")
    ap.add_argument("--self-test", action="store_true", help="prove the checks can fail")
    ap.add_argument("--compile", action="store_true", help="build and RUN the shipped C# and compare it to the model")
    ap.add_argument("--sweep", action="store_true", help="race fixed-depth pilots against the lantern reader and the AI")
    ap.add_argument("--fps", type=int, default=60, help="--sweep: the frame rate a release is quantised to")
    ap.add_argument("--seeds", type=int, default=200)
    ap.add_argument("--target", type=int, default=CLUSTERS, help="clusters per race (the authored target)")
    a = ap.parse_args()
    if a.self_test:
        return 0 if self_test(min(a.seeds, 40), a.target) else 1
    if a.sweep:
        sweep(a.seeds, a.target, a.fps)
        return 0
    if a.compile:
        return 0 if compile_and_compare(min(a.seeds, 40), a.target) else 1
    return 0 if run(a.seeds, a.target) else (1 if a.check else 0)


if __name__ == "__main__":
    sys.exit(main())
