"""Tandava's swarm body plans, built procedurally (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3).

The four elemental plans the Swarm cell grows (Tools/Build/swarm_plans.py) come from the research's designer pages.
Tandava's forms are new animals with no research target, so this module GENERATES them - the same JSON schema
SwarmPlanJson reads, in the same research units (voxels; SwarmFaunaConfigSO.UnitScale converts at the boundary), with
the same element identity clamp (swarm_plans.identity) every research plan went through. Every form is a list of plan
UNITS: a position per animation frame, a facing, an element, a prism, a Charge tier, a spindle, and a region slot
(slot 0 = back, slot 1 = belly - what the lineages own, Docs/SWARM_FAUNA.md §17).

  serpent_s / serpent_m / serpent_l   the Serpent at three lengths. The head and hood are identical across the three,
                                      and each longer body adds stations at the TAIL, so a S -> M -> L commit reads as
                                      the snake growing longer. A lateral travelling wave (8 frames, a loop) carries it.
  bull                                the Bull: a hollow Mass body with a hump, a Charge brow and two horns, four Time
                                      legs on a diagonal gait, a Space neck bell and tail.

Shares follow the design (Mass ~70 / Charge ~8 / Space ~10 / Time ~12 for the serpent; Mass ~55 / Charge ~25 /
Time ~15 / Space ~5 for the bull), and `validate()` asserts them, the identity clamp, and the spacing the sort core's
collision radius needs (SortSpacing 2.25: no two units closer than MIN_GAP in ANY frame).

    python3 Tools/Build/tandava_plans.py              # print every plan's census and spacing; exit 1 on a failed check
    python3 Tools/Build/tandava_plans.py --write DIR  # also write SwarmPlan_tandava_<kind>.json there (the harness)
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from swarm_plans import identity  # noqa: E402  (the research's element identity clamp, one copy)

CHARGE, MASS, SPACE, TIME = 0, 1, 2, 3
FRAMES = 8
FRAME_STEPS = 6
MIN_GAP = 2.1          # voxels; the sort core's collision radius is 2.25 (members settle a touch apart)
SPACING = 2.65         # target neighbour spacing (the whale's median nearest neighbour is 2.67)

# per element: prism half-extents [along the facing, across, across], spindle (len, bend)
LOOK = {
    MASS: ([1.0, 0.8, 0.8], (0.4, 0.0)),
    SPACE: ([2.2, 0.38, 0.38], (0.6, 0.1)),
    TIME: ([0.9, 0.45, 0.4], (0.3, 0.0)),
    CHARGE: ([0.45, 1.2, 1.1], (0.5, 0.0)),   # broad plates: a hood / a brow
}

# ── tiny vector helpers (no numpy: the generator runs anywhere python3 does) ──


def add(a, b): return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
def sub(a, b): return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
def mul(a, s): return [a[0] * s, a[1] * s, a[2] * s]
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def norm(a): return math.sqrt(dot(a, a))


def unit(a):
    n = norm(a)
    return [a[0] / n, a[1] / n, a[2] / n] if n > 1e-9 else [1.0, 0.0, 0.0]


class Unit:
    """One plan unit: its element, slot, tier, look, and a position + facing per frame."""

    def __init__(self, elem, slot, tier=0, half=None, sp=None):
        self.elem, self.slot, self.tier = elem, slot, tier
        look = LOOK[elem]
        self.half = list(half or look[0])
        self.sp = sp or look[1]
        self.pos = [None] * FRAMES
        self.face = [None] * FRAMES


# ───────────────────────────────────────────────────────────────── the serpent

SERPENT_SIZES = {
    # body stations (4-ring), tail stations (2-ring, Time), hood units (Charge)
    "serpent_s": (14, 4, 5),
    "serpent_m": (22, 6, 8),
    "serpent_l": (32, 9, 11),
}
SERPENT_RING_R = 1.95      # 4 units at 45/135/225/315 deg: neighbours 2.76 apart
SERPENT_TAIL_R = 1.4       # 2 units, top and bottom: 2.8 apart
SERPENT_WAVE = 34.0        # wavelength (voxels) of the lateral travelling wave
SERPENT_AMP_HEAD, SERPENT_AMP_TAIL = 0.25, 3.0


def _serpent_spine(s, length, f):
    """Spine point at arc distance s behind the head, frame f: the head leads along +x, a lateral (z) wave travels
    tailward and grows toward the tail (the head stays steady, so the hood reads)."""
    t = min(1.0, s / max(length, 1e-6))
    amp = SERPENT_AMP_HEAD + (SERPENT_AMP_TAIL - SERPENT_AMP_HEAD) * t * t
    phase = 2 * math.pi * s / SERPENT_WAVE - 2 * math.pi * f / FRAMES
    return [-s, 0.0, amp * math.sin(phase)]


def _serpent_frame(s, length, f):
    """(point, tangent toward the head, side) of the spine at s, frame f."""
    p = _serpent_spine(s, length, f)
    q = _serpent_spine(s + 0.25, length, f)
    toward_head = unit(sub(p, q))
    side = unit(cross(toward_head, [0.0, 1.0, 0.0]))
    return p, toward_head, side


def serpent(kind):
    nb, nt, nh = SERPENT_SIZES[kind]
    stations = nb + nt
    length = (stations - 1) * SPACING
    units = []
    # every 4th body station (from the 3rd) wears Space rods along its back: the scale shimmer
    shimmer = {k for k in range(2, nb, 4)}
    for k in range(stations):
        s = k * SPACING
        if k < nb:
            ring = [(45, 0), (135, 0), (225, 1), (315, 1)]   # angle from the side axis, slot (back 0 / belly 1)
            r = SERPENT_RING_R
        else:
            ring = [(90, 0), (270, 1)]
            r = SERPENT_TAIL_R
        for ang, slot in ring:
            if k >= nb:
                elem = TIME                         # the tail rattle
            elif k in shimmer and slot == 0:
                elem = SPACE                        # the shimmer band along the back
            else:
                elem = MASS                         # the coils: bulk, food store, what grows
            u = Unit(elem, slot)
            a = math.radians(ang)
            for f in range(FRAMES):
                p, fwd, side = _serpent_frame(s, length, f)
                off = add(mul(side, r * math.cos(a)), mul([0.0, 1.0, 0.0], r * math.sin(a)))
                u.pos[f] = add(p, off)
                # Space rods lie along the back (the facing is the spine); Time rattles point tailward
                u.face[f] = mul(fwd, -1.0) if elem == TIME else fwd
            units.append(u)
    # the hood: a fan of Charge plates flared round the top and sides of the head, between stations 0 and 1,
    # far enough out to clear the head ring. Shield tier: the hood is what blocks fire from the front.
    hood_r = max(4.3, SPACING * nh / (math.radians(240.0)))
    for q in range(nh):
        ang = math.radians(-30.0 + 240.0 * (q + 0.5) / nh)   # -30 .. 210 deg: over the top, open underneath
        u = Unit(CHARGE, 0, tier=2)
        for f in range(FRAMES):
            p, fwd, side = _serpent_frame(1.3, length, f)
            off = add(mul(side, hood_r * math.cos(ang)), mul([0.0, 1.0, 0.0], hood_r * math.sin(ang)))
            u.pos[f] = add(p, off)
            u.face[f] = fwd
        units.append(u)
    return units


# ───────────────────────────────────────────────────────────────── the bull

def _ellipsoid_candidates(c, r, n, upper_only_above=None):
    pts = []
    golden = math.pi * (3.0 - math.sqrt(5.0))
    for i in range(n):
        y = 1.0 - 2.0 * (i + 0.5) / n
        rad = math.sqrt(max(0.0, 1.0 - y * y))
        th = golden * i
        p = [c[0] + r[0] * rad * math.cos(th), c[1] + r[1] * y, c[2] + r[2] * rad * math.sin(th)]
        if upper_only_above is not None and p[1] < upper_only_above:
            continue
        pts.append(p)
    return pts


def _poisson(cands, taken, gap, limit=None):
    """Greedy Poisson-disk pick: keep a candidate only when it clears every point kept so far by `gap`."""
    out = []
    for p in cands:
        if all(norm(sub(p, q)) >= gap for q in taken) and all(norm(sub(p, q)) >= gap for q in out):
            out.append(p)
            if limit and len(out) >= limit:
                break
    return out


BULL_BODY_C, BULL_BODY_R = [0.0, 0.0, 0.0], [11.0, 5.6, 5.0]
BULL_HUMP_C, BULL_HUMP_R = [5.0, 5.2, 0.0], [4.0, 2.6, 3.4]
BULL_HEAD_X = 14.2
BULL_LEGS = [(7.0, 3.2, 0.0), (7.0, -3.2, math.pi), (-7.0, 3.2, math.pi), (-7.0, -3.2, 0.0)]   # x, z, gait phase
BULL_LEG_UNITS = 6
BULL_HORN_STATIONS = 8


def bull():
    units = []
    rest = []          # frame-0 rest positions of every unit placed so far (the spacing check while building)

    def place(elem, slot, base, face, tier=0, half=None, anim=None):
        u = Unit(elem, slot, tier, half)
        for f in range(FRAMES):
            ph = 2 * math.pi * f / FRAMES
            p = list(base)
            if anim:
                p = add(p, anim(ph))
            u.pos[f] = p
            u.face[f] = unit(face(ph) if callable(face) else face)
        units.append(u)
        rest.append(base)

    def bob(ph): return [0.0, 0.35 * math.sin(2 * ph), 0.0]

    # the body: a hollow Mass shell (the whale's construction), then the hump over the shoulders
    for p in _poisson(_ellipsoid_candidates(BULL_BODY_C, BULL_BODY_R, 4000), rest, SPACING):
        place(MASS, 0 if p[1] > 0.5 else 1, p, [1.0, 0.0, 0.0], anim=bob)
    for p in _poisson(_ellipsoid_candidates(BULL_HUMP_C, BULL_HUMP_R, 1500, upper_only_above=BULL_HUMP_C[1] - 0.4), rest, SPACING):
        place(MASS, 0, p, [1.0, 0.0, 0.0], anim=bob)

    def nod(ph): return add(bob(ph), [0.0, 0.5 * math.sin(ph), 0.0])

    # the brow: a block of Charge plates across the face (front of the head)
    for iy in range(3):
        for iz in range(4):
            p = [BULL_HEAD_X, -1.6 + iy * SPACING, -3.975 + iz * SPACING]
            if all(norm(sub(p, q)) >= SPACING * 0.9 for q in rest):
                place(CHARGE, 0 if p[1] > 0.5 else 1, p, [1.0, 0.0, 0.0], anim=nod)
    # the horns: from the sides of the head, out, up and forward - stations every SPACING of arc length, a pair of
    # plates across each station
    def horn(side, t):
        return [BULL_HEAD_X - 1.0 + 6.0 * t ** 3, 3.5 + 9.5 * t * t, side * (4.6 + 13.0 * t)]
    for side in (1.0, -1.0):
        samples = [horn(side, i / 400.0) for i in range(401)]
        stations, run = [], 0.0
        for i in range(1, len(samples)):
            run += norm(sub(samples[i], samples[i - 1]))
            if run >= SPACING * (len(stations) + 1) * 0.85:
                stations.append(i)
        for i in stations[:BULL_HORN_STATIONS]:
            c = samples[i]
            tan = unit(sub(samples[min(i + 1, 400)], samples[i - 1]))
            across = unit(cross(tan, [0.0, 1.0, 0.0]))
            for j in (-1.0, 1.0):
                p = add(c, mul(across, 1.3 * j))
                if all(norm(sub(p, q)) >= MIN_GAP for q in rest):
                    place(CHARGE, 0, p, tan, half=[1.1, 0.7, 0.6], anim=nod)
    # the legs: four Time columns, diagonal pairs on opposite phases (a walk); the foot swings and lifts most
    for lx, lz, phase in BULL_LEGS:
        for j in range(BULL_LEG_UNITS):
            base = [lx, -BULL_BODY_R[1] - 2.4 - j * SPACING, lz]
            depth = (j + 1) / BULL_LEG_UNITS

            def gait(ph, depth=depth, phase=phase):
                return add(bob(ph), [2.4 * depth * math.sin(ph + phase), 0.9 * depth * max(0.0, math.cos(ph + phase)), 0.0])
            place(TIME, 1, base, [0.0, -1.0, 0.0], anim=gait)
    # the neck bell: Space rods hanging under the throat; the tail: rods from the rump, swishing
    for k in range(3):
        base = [11.5 + (k - 1) * 0.2, -BULL_BODY_R[1] - 2.2 - k * 0.1, (k - 1) * SPACING]
        place(SPACE, 1, base, [0.0, -1.0, 0.0], anim=nod)
    for k in range(5):
        base = [-BULL_BODY_R[0] - 2.4 - k * SPACING * 0.75, 1.5 - k * SPACING * 0.65, 0.0]
        reach = (k + 1) / 5.0

        def swish(ph, reach=reach):
            return add(bob(ph), [0.0, 0.0, 2.2 * reach * math.sin(ph)])
        place(SPACE, 1, base, [-0.7, -0.6, 0.0], anim=swish)
    return units


# ───────────────────────────────────────────────────────────────── bake + validate

PLANS = {
    "serpent_s": ("Serpent (small)", lambda: serpent("serpent_s")),
    "serpent_m": ("Serpent (medium)", lambda: serpent("serpent_m")),
    "serpent_l": ("Serpent (large)", lambda: serpent("serpent_l")),
    "bull": ("Bull", bull),
}
# the shares each form must hold (fraction of its units, +- SHARE_TOL) - the design's table
SHARES = {
    "serpent_s": {MASS: 0.70, CHARGE: 0.08, SPACE: 0.10, TIME: 0.12},
    "serpent_m": {MASS: 0.70, CHARGE: 0.08, SPACE: 0.10, TIME: 0.12},
    "serpent_l": {MASS: 0.70, CHARGE: 0.08, SPACE: 0.10, TIME: 0.12},
    "bull": {MASS: 0.55, CHARGE: 0.25, SPACE: 0.05, TIME: 0.15},
}
SHARE_TOL = 0.035
MAJOR = MASS   # every phase-A form is Mass-majority (the director, not the census, picks the form)


def _r(x):
    return round(float(x), 3)


def bake(kind):
    name, build = PLANS[kind]
    units = build()
    n = len(units)
    c = [sum(u.pos[0][a] for u in units) / n for a in range(3)]   # centred on frame 0's centroid (swarm_plans)
    pos, face, half_f, tier_f, sp_f = [], [], [], [], []
    for f in range(FRAMES):
        for u in units:
            pos.extend(_r(u.pos[f][a] - c[a]) for a in range(3))
            face.extend(_r(x) for x in unit(u.face[f]))
            h, t = identity(u.elem, u.half, u.tier)
            half_f.extend(_r(x) for x in h)
            tier_f.append(t)
            sp_f.extend((_r(u.sp[0]), _r(u.sp[1])))
    half0 = []
    for u in units:
        h, _ = identity(u.elem, u.half, u.tier)
        half0.extend(_r(x) for x in h)
    return {
        "kind": kind, "name": name, "major": MAJOR, "n": n, "frames": FRAMES, "frameSteps": FRAME_STEPS,
        "order": list(range(FRAMES)),   # every Tandava form loops (no ping-pong)
        "elem": [u.elem for u in units], "tier": [u.tier if u.elem == CHARGE else 0 for u in units], "half": half0,
        "pos": pos, "face": face, "swimAxis": [1, 0, 0], "upAxis": [0, 1, 0],
        "slot": [u.slot for u in units], "halfF": half_f, "tierF": tier_f, "sp": sp_f,
    }


def bake_all():
    return {k: bake(k) for k in PLANS}


def census(plan):
    m = [0, 0, 0, 0]
    for e in plan["elem"]:
        m[e] += 1
    return m


def min_gap(plan, f):
    n = plan["n"]
    P = [plan["pos"][3 * (f * n + i):3 * (f * n + i) + 3] for i in range(n)]
    best = float("inf")
    for i in range(n):
        for j in range(i + 1, n):
            d = norm(sub(P[i], P[j]))
            if d < best:
                best = d
    return best


def validate(plans):
    """Every promise this module makes, as a list of failure strings (empty = all hold)."""
    errors = []
    for kind, p in plans.items():
        n = p["n"]
        m = census(p)
        for e, want in SHARES[kind].items():
            got = m[e] / n
            if abs(got - want) > SHARE_TOL:
                errors.append(f"{kind}: element {e} share {got:.3f}, the design wants {want:.2f} +- {SHARE_TOL}")
        if max(range(4), key=lambda e: m[e]) != p["major"]:
            errors.append(f"{kind}: the major element {p['major']} is not the census majority {m}")
        for f in range(FRAMES):
            g = min_gap(p, f)
            if g < MIN_GAP:
                errors.append(f"{kind}: frame {f} has two units {g:.2f} apart (< {MIN_GAP})")
        if any(s not in (0, 1) for s in p["slot"]) or not (0 in p["slot"] and 1 in p["slot"]):
            errors.append(f"{kind}: every form needs both regions (back 0, belly 1) for the lineages")
        if any(t != 0 for e, t in zip(p["elem"], p["tier"]) if e != CHARGE):
            errors.append(f"{kind}: a non-Charge unit carries a tier (only Charge changes state)")
        for k in range(n):
            h = p["half"][3 * k:3 * k + 3]
            e = p["elem"][k]
            if e == MASS and max(h) > 1.6 * min(h) + 1e-6:
                errors.append(f"{kind}: Mass unit {k} is not near-cubic {h}")
            if e == SPACE and max(h[1], h[2]) > h[0] / 4 + 1e-6:
                errors.append(f"{kind}: Space unit {k} is not a rod {h}")
        if len(p["pos"]) != 3 * FRAMES * n or len(p["face"]) != 3 * FRAMES * n or len(p["sp"]) != 2 * FRAMES * n:
            errors.append(f"{kind}: frame arrays are the wrong length")
    # the serpent grows at the TAIL: the head and hood are the same units at the same place in every size
    s, l = plans["serpent_s"], plans["serpent_l"]
    if not (s["n"] < plans["serpent_m"]["n"] < l["n"]):
        errors.append("serpent sizes do not grow S < M < L")
    return errors


def dumps(plan):
    return json.dumps(plan, separators=(",", ":"))


def main():
    plans = bake_all()
    if len(sys.argv) > 2 and sys.argv[1] == "--write":   # harness use: write the JSONs to a scratch dir
        os.makedirs(sys.argv[2], exist_ok=True)
        for kind, p in plans.items():
            with open(os.path.join(sys.argv[2], f"SwarmPlan_tandava_{kind}.json"), "w", encoding="utf-8") as fh:
                fh.write(dumps(p))
    for kind, p in plans.items():
        m = census(p)
        n = p["n"]
        gaps = [min_gap(p, f) for f in range(FRAMES)]
        print(f"{kind:10s} n={n:3d}  C/M/S/T {m[0]:3d}/{m[1]:3d}/{m[2]:3d}/{m[3]:3d} "
              f"({100 * m[0] / n:.0f}/{100 * m[1] / n:.0f}/{100 * m[2] / n:.0f}/{100 * m[3] / n:.0f}%)  "
              f"min gap {min(gaps):.2f} (frame 0 {gaps[0]:.2f})  slots back/belly {p['slot'].count(0)}/{p['slot'].count(1)}")
    errors = validate(plans)
    for e in errors:
        print("FAIL:", e)
    print("tandava plans: " + ("OK" if not errors else f"{len(errors)} failure(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
