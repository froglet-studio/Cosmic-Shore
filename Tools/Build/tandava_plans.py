"""Tandava's swarm body plans, built procedurally (Assets/_Scripts/Controller/Arcade/TANDAVA.md §2).

The four elemental plans the Swarm cell grows (Tools/Build/swarm_plans.py) come from the research's designer pages.
Tandava's forms are new animals with no research target, so this module GENERATES them - the same JSON schema
SwarmPlanJson reads, in the same research units (voxels; SwarmFaunaConfigSO.UnitScale converts at the boundary), with
the same element identity clamp (swarm_plans.identity) every research plan went through. Every form is a list of plan
UNITS: a position per animation frame, a facing, an element, a prism, a Charge tier, a spindle, and a region slot
(slot 0 = back, slot 1 = belly - what the lineages own, Docs/SWARM_FAUNA.md §17).

FOUR forms, in order - and a match draws ONE of three VARIANTS of each, so no two matches meet the same animal:

  great_serpent_1/2/3    the Great Serpent: it hatches as this. Long and slender / longer with a narrow hood / short
                         and thick with a broad hood. A lateral travelling wave carries it.
  many_headed_5/7/10     the Many-Headed Serpent: five, seven or ten necks fanned from a collar at the front of a
                         serpent's body (the ten-headed one has two tails).
  dancer_1/2/3           the Lord of the Dance (the ascension): the figure, its halo and four attendant packs; the
                         second is the mirror pose, the third flies its hair wider.
  sea_lion_1/2/3         the Sea Lion: the final form. A lion's head and mane on a short body, great fore-fins, and a
                         fish's tail with a fluke. NO legs - every form FLIES (the HyperSea has no ground).

Every feeding form has a FEED twin (<key>_feed), the pose it eats in: the same units, the body settled, and its Charge
plates (the hood, the mane, the fluke) gone OUT to orbit its mouth as a ring of DANGER-tier guards - the protectors.
Same element counts as the travel plan, so the commit between them is lossless and needs no molt. The twin bakes its
"mouth" (where the director puts the food) and its guard ring.

`validate()` asserts the identity clamp, the spacing the sort core's collision radius needs (SortSpacing 2.25: no two
units closer than MIN_GAP in ANY frame), Mass majority (the director, not the census, picks the form), both regions,
that every feed twin carries exactly its travel plan's element counts, and that the forms only ever grow: every variant
of a form is at least as big as the biggest variant of the form before it.

    python3 Tools/Build/tandava_plans.py              # print every plan's census and spacing; exit 1 on a failed check
    python3 Tools/Build/tandava_plans.py --write DIR  # also write SwarmPlan_tandava_<key>.json there (the harness)
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
FEED_FRAME_STEPS = 40  # a feed twin barely moves; its guards orbit one turn per 8 x 40 steps = 32 s (Charge, the
                       # slowest-turning tadpoles, have to keep up: ~0.3 voxels/step at the ring)
MIN_GAP = 2.1          # voxels; the sort core's collision radius is 2.25 (members settle a touch apart)
SPACING = 2.65         # target neighbour spacing (the whale's median nearest neighbour is 2.67)

# per element: prism half-extents [along the facing, across, across], spindle (len, bend)
LOOK = {
    MASS: ([1.0, 0.8, 0.8], (0.4, 0.0)),
    SPACE: ([2.2, 0.38, 0.38], (0.6, 0.1)),
    TIME: ([0.9, 0.45, 0.4], (0.3, 0.0)),
    CHARGE: ([0.45, 1.2, 1.1], (0.5, 0.0)),   # broad plates: a hood / a mane / a fluke
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


def _unit_at(elem, slot, tier, at, face, half=None):
    u = Unit(elem, slot, tier, half)
    for f in range(FRAMES):
        u.pos[f] = list(at(f))
        u.face[f] = unit(face(f))
    return u


def _thin(units):
    """Drop any unit that comes closer than MIN_GAP to one kept before it, in ANY frame (strokes cross at joints)."""
    return [units[i] for i in _keep([units])]


def _keep(poses):
    """The indices to keep when several POSES of one body (a travel plan and its feed twin: equal-length unit lists in
    the same order) must be thinned TOGETHER - a unit is kept only if it clears every unit kept before it in every
    frame of every pose, so the twins keep the same members and the commit between them stays lossless."""
    kept = []
    for i in range(len(poses[0])):
        if all(min(norm(sub(pose[i].pos[f], pose[j].pos[f])) for pose in poses for f in range(FRAMES)) >= MIN_GAP + 0.01
               for j in kept):   # + 0.01: the bake rounds to 3 decimals, and validate() reads the rounded plan
            kept.append(i)
    return kept


# ───────────────────────────────────────────────────────────────── the protectors (every feed twin)

GUARD_ROWS = (-1.5, 1.5)   # the guard ring is two staggered rows, this far either side of its plane


def _guard_ring(charge_units, centre, axis, radius, sense):
    """Re-place a form's Charge units as its feeding GUARD: evenly round a ring of `radius` about `axis` through
    `centre`, staggered in two rows, DANGER tier, orbiting one turn per loop in `sense`. Returns new units (same
    elements and slots), so the feed twin keeps the travel plan's census exactly."""
    axis = unit(axis)
    e1 = unit(cross(axis, [0.0, 1.0, 0.0])) if abs(axis[1]) < 0.9 else unit(cross(axis, [1.0, 0.0, 0.0]))
    e2 = cross(axis, e1)
    n = len(charge_units)
    out = []
    for q, src in enumerate(charge_units):
        a0, row = 2 * math.pi * q / n, GUARD_ROWS[q % 2]

        def at(f, a0=a0, row=row):
            a = a0 + sense * 2 * math.pi * f / FRAMES
            return add(add(centre, mul(axis, row)), add(mul(e1, radius * math.cos(a)), mul(e2, radius * math.sin(a))))

        def face(f, a0=a0):
            a = a0 + sense * 2 * math.pi * f / FRAMES
            return add(mul(e1, -math.sin(a) * sense), mul(e2, math.cos(a) * sense))
        out.append(_unit_at(CHARGE, src.slot, 1, at, face))
    return out


# ───────────────────────────────────────────────────────────────── the serpent's body (two forms use it)

def _spine(s, length, f, wave, amp_head, amp_tail, settle=1.0):
    """Spine point at arc distance s behind the head, frame f: the head leads along +x, a lateral (z) wave travels
    tailward and grows toward the tail (the head stays steady, so the hood and the necks read). `settle` < 1 calms it
    (the feed twin)."""
    t = min(1.0, s / max(length, 1e-6))
    amp = (amp_head + (amp_tail - amp_head) * t * t) * settle
    phase = 2 * math.pi * s / wave - 2 * math.pi * f / FRAMES
    return [-s, 0.0, amp * math.sin(phase)]


def _spine_frame(s, length, f, wv):
    p = _spine(s, length, f, *wv)
    q = _spine(s + 0.25, length, f, *wv)
    toward_head = unit(sub(p, q))
    side = unit(cross(toward_head, [0.0, 1.0, 0.0]))
    return p, toward_head, side


def _wave_frame(length, wv):
    """The swimming (and the strike pose's settled) spine as a frame function: (point, toward the head, side, up)."""
    def frame(s, f):
        p, fwd, side = _spine_frame(s, length, f, wv)
        return p, fwd, side, [0.0, 1.0, 0.0]
    return frame


def _serpent_body(nb, nt, ring_r, tail_r, shimmer, frame, tails=1, tail_spread=0.0, fan_up=False):
    """The serpent's body along `frame(s, f)` -> (point, toward the head, side, up): nb 4-unit rings (Mass, with Space
    rods along the back at the `shimmer` stations), then nt 2-unit Time rings (the tail rattle) - one tail, or `tails`
    tails fanning out by `tail_spread` per station, along the side (or, `fan_up`, the up: rolled up, the side points
    into the curve, and a forked tail fanned that way bunches on the inside of the bend)."""
    units = []
    length = (nb + nt - 1) * SPACING
    for k in range(nb):
        s = k * SPACING
        for ang, slot in ((45, 0), (135, 0), (225, 1), (315, 1)):
            elem = SPACE if (k in shimmer and slot == 0) else MASS
            a = math.radians(ang)

            def at(f, s=s, a=a):
                p, fwd, side, up = frame(s, f)
                return add(p, add(mul(side, ring_r * math.cos(a)), mul(up, ring_r * math.sin(a))))

            def face(f, s=s):
                return frame(s, f)[1]
            units.append(_unit_at(elem, slot, 0, at, face))
    for j in range(tails):
        lane = (j - (tails - 1) / 2.0)
        for k in range(nb, nb + nt):
            s = k * SPACING
            fan = lane * (1.8 + tail_spread * (k - nb)) if tails > 1 else 0.0   # forked tails start apart
            for ang, slot in ((90, 0), (270, 1)):
                a = math.radians(ang)

                def at(f, s=s, a=a, fan=fan):
                    p, fwd, side, up = frame(s, f)
                    across, ring = (up, side) if fan_up else (side, up)
                    return add(add(p, mul(across, fan)), mul(ring, tail_r * math.sin(a)))

                def face(f, s=s):
                    return mul(frame(s, f)[1], -1.0)
                units.append(_unit_at(TIME, slot, 0, at, face))
    return units, length


# ───────────────────────────────────────────────────────────────── the coils (the serpents' meals)
#
# A serpent eats ROLLED UP, and in a different formation each meal (the prompter, 2026-10-06: "make the serpent roll up
# like a snake in different formations when eating"). Each formation is a curve the body lies along, head first:
#
#   coil    a flat spiral, the head on the inner turn - a resting snake's coil
#   wrap    a helix, two turns stacked up the axis - a constrictor round its prey
#   eight   a figure-eight, its two passes crossing one above the other - the lemniscate
#
# The Great Serpent coils ROUND the plant (the plant is the formation's centre - its mouth), so far more of its body is
# in reach of the food than the strike pose's head ever was: a bite is a member within BiteRadius of a leaf
# (SwarmFauna.Feed), and the formations differ in how much of the body that is - which is why a meal is faster, and why
# it is not equally fast in every formation. The Many-Headed Serpent's heads already close round the plant (its strike
# pose's ring), so its body coils BEHIND the collar instead. Every coil keeps the strike pose's units in the same order
# (thinned together with it), so a commit between any two poses is a re-sort, never a molt.

COILS = ("coil", "wrap", "eight")
COIL_PITCH = 6.0           # voxels between neighbouring turns' spines: the 4-unit rings (1.38 out) and a forked
                           # tail's lanes clear MIN_GAP
COIL_BREATHE = 0.025       # the coil squeezes and loosens round its meal, this share of its radius, once a loop
COIL_FRAME_STEPS = 12      # 8 frames x 12 steps at 10 Hz = a squeeze, and a turn of its guards, every 9.6 s
COIL_GUARD_LIFT = 6.5      # a flat formation's guards ride a crown this far above its plane
EIGHT_RISE = 3.6           # the figure-eight's passes cross this far above and below its centre
EIGHT_SHARE = 0.9          # the body covers at most this share of the eight (the tail stops short of the head)
EIGHT_MIN_R = 9.0          # the figure-eight's two round lobes are never tighter than this (a lemniscate's lobe tips
                           # bend at a third of its size: one long enough for the body bunches its rings at the tips)
COIL_TAIL_SPREAD = 0.5     # a forked tail fans this much per station in a coil (less than swimming: a coil is curved)


def _polyline(fn, t0, t1, n=4000):
    return [fn(t0 + (t1 - t0) * i / n) for i in range(n + 1)]


def _arc_table(pts):
    acc = [0.0]
    for i in range(1, len(pts)):
        acc.append(acc[-1] + norm(sub(pts[i], pts[i - 1])))
    return acc


def _along(pts, acc, s):
    """The point at arc length s along a dense polyline (clamped to its ends)."""
    s = max(0.0, min(acc[-1], s))
    lo, hi = 0, len(acc) - 1
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if acc[mid] <= s:
            lo = mid
        else:
            hi = mid
    span = acc[hi] - acc[lo]
    w = (s - acc[lo]) / span if span > 1e-12 else 0.0
    return [pts[lo][q] + (pts[hi][q] - pts[lo][q]) * w for q in range(3)]


def _coil_shape(kind, length, inner):
    """A formation's curve in its own frame, head end first, at least `length` long, round the origin (its centre),
    axis +y: (points, guard ring (centre, axis, radius)). `inner` is the innermost spine radius."""
    P = COIL_PITCH
    if kind == "coil":
        turns = 0.0
        while True:   # enough turns for the body
            pts = _polyline(lambda th: [(inner + P * th / (2 * math.pi)) * math.cos(th), 0.0,
                                        (inner + P * th / (2 * math.pi)) * math.sin(th)], 0.0, 2 * math.pi * (turns + 0.25))
            if _arc_table(pts)[-1] >= length + 1.0:
                break
            turns += 0.25
        return pts, ([0.0, COIL_GUARD_LIFT, 0.0], [0.0, 1.0, 0.0], inner + 0.5 * P + 1.5)
    if kind == "wrap":
        per_turn = math.hypot(2 * math.pi * inner, P)
        turns = length / per_turn + 0.05
        h = P * turns
        pts = _polyline(lambda th: [inner * math.cos(th), h / 2 - P * th / (2 * math.pi), inner * math.sin(th)],
                        0.0, 2 * math.pi * turns)
        return pts, ([0.0, 0.0, 0.0], [0.0, 1.0, 0.0], inner + 7.5)
    if kind == "eight":
        # two round lobes of radius r either side of the centre, joined by the straights that cross there at right
        # angles: out along +45 deg, three quarters round the far lobe, back through the centre, three quarters round
        # the near lobe, home. The two passes cross EIGHT_RISE above and below the centre.
        r = max(EIGHT_MIN_R, length / ((4.0 + 3.0 * math.pi) * EIGHT_SHARE))
        total = (4.0 + 3.0 * math.pi) * r
        pts, x, z, h, s = [], 0.0, 0.0, math.pi / 4, 0.0
        step = 0.02
        for seg_len, turn in ((r, 0.0), (1.5 * math.pi * r, -1.0), (2.0 * r, 0.0), (1.5 * math.pi * r, 1.0), (r, 0.0)):
            for _ in range(int(round(seg_len / step))):
                pts.append([x, EIGHT_RISE * math.cos(2 * math.pi * s / total), z])
                h += turn * step / r
                x += step * math.cos(h); z += step * math.sin(h); s += step
        pts.append([x, EIGHT_RISE * math.cos(2 * math.pi * s / total), z])
        return pts, ([0.0, EIGHT_RISE + COIL_GUARD_LIFT, 0.0], [0.0, 1.0, 0.0], 1.25 * r)
    raise ValueError(kind)


def _coil_frame(pts, centre):
    """A frame function along a formation's curve, breathing about `centre`'s vertical axis (a uniform squeeze, so no
    two turns ever close on each other)."""
    acc = _arc_table(pts)

    def at(s, f):
        q = _along(pts, acc, s)
        k = 1.0 + COIL_BREATHE * math.sin(2 * math.pi * f / FRAMES)
        return [centre[0] + (q[0] - centre[0]) * k, q[1], centre[2] + (q[2] - centre[2]) * k]

    def frame(s, f):
        p, q = at(s, f), at(s + 0.25, f)
        fwd = unit(sub(p, q))
        side = unit(cross(fwd, [0.0, 1.0, 0.0]))
        return p, fwd, side, cross(side, fwd)
    return frame


def _placed_behind(pts, run, drop, bend_r):
    """The formation turned and moved so it leaves the collar (the origin) backward and DOWN: an S-bend of two arcs of
    radius `bend_r` dropping `drop` (level again at its foot), a straight `run` on along -x, then the curve, entered
    along its own start tangent."""
    out, x, y, a = [], 0.0, 0.0, 0.0                            # a: the heading's dip below -x
    th = math.acos(1.0 - drop / (2.0 * bend_r))
    step = 0.05
    for turn in (1.0, -1.0):
        for _ in range(int(bend_r * th / step)):
            out.append([x, y, 0.0])
            a += turn * step / bend_r
            x -= step * math.cos(a); y -= step * math.sin(a)
    for _ in range(int(run / step)):
        out.append([x, y, 0.0])
        x -= step
    tx, tz = pts[1][0] - pts[0][0], pts[1][2] - pts[0][2]
    rot = math.atan2(tz, tx) - math.pi                          # turn the start tangent onto -x
    c, s_ = math.cos(-rot), math.sin(-rot)
    lead = len(out)
    for q in pts:
        dx, dz = q[0] - pts[0][0], q[2] - pts[0][2]
        out.append([x + dx * c - dz * s_, y + q[1] - pts[0][1], dx * s_ + dz * c])
    return out, lead


# ───────────────────────────────────────────────────────────────── form 1: the Great Serpent

GREAT_SERPENT = {
    # body stations, tail stations, hood plates, ring r, wave (wavelength, head amp, tail amp), shimmer every n,
    # hood span (deg), name
    1: dict(nb=32, nt=9, nh=11, ring_r=1.95, wave=(34.0, 0.25, 3.0), shimmer=4, span=240.0),
    2: dict(nb=36, nt=8, nh=9, ring_r=1.9, wave=(42.0, 0.2, 3.8), shimmer=3, span=200.0),
    3: dict(nb=29, nt=10, nh=14, ring_r=2.05, wave=(30.0, 0.3, 2.6), shimmer=3, span=290.0),
}
SERPENT_TAIL_R = 1.4
FEED_SETTLE = 0.3          # a striking body's wave, as a share of its swimming one
COIL_INNER = 10.5          # the Great Serpent's innermost turn round its plant (a plant is ~28 u = ~9.7 voxels across)


def great_serpent(v, pose="travel"):
    """THE GREAT SERPENT: Mass coils (bulk, food store, what regrows), Space rods along the back (the shimmer), a Time
    tail rattle, and a fan of Charge plates flared round the head - the hood, shield tier. Striking (`feed`, the lunge
    pose), the hood plates go out to orbit its jaws as danger-tier guards and the body lies still behind them. Eating
    (a pose in COILS), it rolls up round the plant in that formation, its guards on a ring outside the coil."""
    p = GREAT_SERPENT[v]
    settle = FEED_SETTLE if pose != "travel" else 1.0
    wv = (p["wave"][0], p["wave"][1], p["wave"][2], settle)
    shimmer = set(range(2, p["nb"], p["shimmer"]))
    length = (p["nb"] + p["nt"] - 1) * SPACING
    if pose in COILS:
        pts, (gc, ga, gr) = _coil_shape(pose, length, COIL_INNER)
        frame = _coil_frame(pts, [0.0, 0.0, 0.0])
    else:
        frame = _wave_frame(length, wv)
    units, length = _serpent_body(p["nb"], p["nt"], p["ring_r"], SERPENT_TAIL_R, shimmer, frame)
    hood_r = max(4.3, SPACING * p["nh"] / math.radians(p["span"]))
    hood = []
    for q in range(p["nh"]):
        ang = math.radians(90.0 - p["span"] / 2 + p["span"] * (q + 0.5) / p["nh"])   # centred over the top

        def at(f, ang=ang):
            c, fwd, side = _spine_frame(1.3, length, f, wv)
            return add(c, add(mul(side, hood_r * math.cos(ang)), [0.0, hood_r * math.sin(ang), 0.0]))
        hood.append(_unit_at(CHARGE, 0, 2, at, lambda f: _spine_frame(1.3, length, f, wv)[1]))
    if pose == "feed":
        mouth = [3.0, 0.0, 0.0]
        hood = _guard_ring(hood, mouth, [1.0, 0.0, 0.0], hood_r + 6.5, 1 if v % 2 else -1)
        return units + hood, mouth
    if pose in COILS:
        return units + _guard_ring(hood, gc, ga, gr, 1 if v % 2 else -1), [0.0, 0.0, 0.0]   # the plant at its centre
    return units + hood, None


# ───────────────────────────────────────────────────────────────── form 2: the Many-Headed Serpent

MANY_HEADED = {
    # heads, body stations, tail stations, tails, fan (deg), neck stations, name
    5: dict(heads=5, nb=31, nt=7, tails=1, fan=150.0, neck=4),
    7: dict(heads=7, nb=29, nt=7, tails=1, fan=160.0, neck=4),
    # the ten-headed, two-tailed one (the prompter's): its heads sit 8 voxels apart at the fan, so its hood plates flare
    # 2.0 either side, not 2.6 - neighbouring heads sway out of phase and the wider pair touched
    10: dict(heads=10, nb=24, nt=6, tails=2, fan=176.0, neck=5, plate=2.0),
}
HEAD_WORDS = {5: "Five", 7: "Seven", 10: "Ten"}


MH_COIL_INNER = 10.5       # the Many-Headed Serpent's innermost turn (its coil lies under its heads, round nothing)
MH_COIL_DROP = 7.0         # its coil lies this far below the collar: the necks rise from it like a rearing cobra's
MH_BEND_R = 9.0            # the S-bend down to it (its arcs no tighter than the rings allow)
MH_COIL_REACH = 9.0        # ...and the coil may come this far forward of the collar UNDER the necks (its heads ring the
                           # food ~17 voxels out, its guards ~16 round them - the space below the fan is free)


def _many_headed_coil(kind, length):
    """The Many-Headed Serpent's body rolled up UNDER its raised heads (which ring the food, in front): an S-bend down
    from the collar, then the formation - the flat coil entered at its OUTER end, so no turn passes back over the bend.
    The run on from the bend is as short as keeps every turn behind the collar's line wherever it rises past the space
    under the necks."""
    th = math.acos(1.0 - MH_COIL_DROP / (2.0 * MH_BEND_R))
    bend = 2.0 * MH_BEND_R * th
    for run in [0.5 * k for k in range(80)]:
        shape = _coil_shape(kind, length - bend - run + 2.0, MH_COIL_INNER)[0]
        pts, lead = _placed_behind(shape[::-1] if kind == "coil" else shape, run, MH_COIL_DROP, MH_BEND_R)
        if all(q[0] <= (MH_COIL_REACH if q[1] <= -MH_COIL_DROP + 3.5 else -4.0) for q in pts[lead:]):
            break
    xs, zs = [q[0] for q in pts[lead:]], [q[2] for q in pts[lead:]]
    return _coil_frame(pts, [(min(xs) + max(xs)) / 2, 0.0, (min(zs) + max(zs)) / 2])


def many_headed(v, pose="travel"):
    """THE MANY-HEADED SERPENT: a serpent's body whose front widens into a collar from which N necks (Space rods)
    rise in a fan, each ending in a head (two Mass units, the snout ahead) with two Charge hood plates behind it - the
    classic many-hooded serpent. The ten-headed one has two tails.

    Feeding, every head DIPS to the food: the necks sweep forward and the heads close into a ring round the mouth, each
    snout pointing in - so the tadpoles that bite are where the plant is (a fan held up behind the food bit nothing:
    the 2026-10-06 harness run ended 20 of 24 meals bare). The hood plates leave the heads to orbit outside them as
    danger-tier guards."""
    p = MANY_HEADED[v]
    n = p["heads"]
    feed = pose != "travel"   # the heads close round the food in the strike pose and in every coil
    settle = FEED_SETTLE if feed else 1.0
    wv = (36.0, 0.2, 3.0, settle)
    length = (p["nb"] + p["nt"] - 1) * SPACING
    frame = _wave_frame(length, wv)
    if pose in COILS:
        frame = _many_headed_coil(pose, length)
    coiled = pose in COILS
    units, length = _serpent_body(p["nb"], p["nt"], 1.95, SERPENT_TAIL_R, set(range(2, p["nb"], 4)), frame,
                                  tails=p["tails"], tail_spread=COIL_TAIL_SPREAD if coiled else 1.2, fan_up=coiled)
    fan = math.radians(p["fan"])
    dphi = fan / (n - 1)
    collar = max(3.4, 2.4 / (2 * math.sin(dphi / 2)))   # roots on an arc wide enough to keep neighbouring necks apart
    sway = 0.0 if feed else 0.07
    reach = collar + (p["neck"] + 1.2) * SPACING
    mouth = [reach * 0.8 + 3.0, reach * 0.3, 0.0]        # the feed pose's: in front of the collar, a little up
    head_ring = max(6.0, MIN_GAP * 1.3 * n / (2 * math.pi) + 2.4)   # the heads' ring round it (snouts 2.4 inside)
    hoods = []
    for k in range(n):
        phi0 = -fan / 2 + dphi * k                       # 0 = straight up; the fan spreads across the top
        theta = 2 * math.pi * k / n                      # feeding: its place on the ring round the food

        def radial(f, phi0=phi0, k=k):
            phi = phi0 + sway * math.sin(2 * math.pi * f / FRAMES + 0.7 * k)
            return [0.0, math.cos(phi), math.sin(phi)]

        def travel_pt(f, j, phi0=phi0, k=k):
            r = radial(f, phi0, k)
            c = _spine_frame(0.0, length, f, wv)[0]   # the collar (the head end's settled wave, in every feeding pose)
            d = unit(add([0.55, 0.0, 0.0], r))
            return add(add(c, mul(r, collar)), add(mul(d, j * SPACING), [0.12 * j * j, 0.0, 0.0]))

        def feed_pt(f, j, phi0=phi0, k=k, theta=theta):
            # a quadratic curve from the neck's root on the collar, bowed out along its travel direction, to its head's
            # place on the ring round the food
            root = travel_pt(f, 0.0)
            ctrl = travel_pt(f, 0.55 * (p["neck"] + 1.2))
            end = add(mouth, [0.0, head_ring * math.cos(theta), head_ring * math.sin(theta)])
            t = min(1.0, j / (p["neck"] + 1.2))
            a, b_, c_ = (1 - t) ** 2, 2 * (1 - t) * t, t * t
            return [a * root[q] + b_ * ctrl[q] + c_ * end[q] for q in range(3)]

        neck_pt = feed_pt if feed else travel_pt
        for j in range(1, p["neck"] + 1):
            units.append(_unit_at(SPACE, 0, 0, lambda f, j=j, neck_pt=neck_pt: neck_pt(f, j),
                                  lambda f, j=j, neck_pt=neck_pt: sub(neck_pt(f, j + 0.5), neck_pt(f, j))))
        tip = p["neck"] + 1.2

        def head(f, neck_pt=neck_pt):
            return neck_pt(f, tip)

        def fwd(f, neck_pt=neck_pt, head=head):
            if feed:
                return unit(sub(mouth, head(f)))           # every snout points at the food
            return unit(add(sub(neck_pt(f, tip), neck_pt(f, tip - 1)), [1.2, 0.0, 0.0]))
        units.append(_unit_at(MASS, 0, 0, head, fwd))
        units.append(_unit_at(MASS, 1, 0, lambda f, head=head, fwd=fwd: add(head(f), mul(fwd(f), 2.4)), fwd))
        # two hood plates per head, flared either side of its neck's fan direction (a cobra's hood, in pairs)
        spread = p.get("plate", 2.6)
        for flare in (-1.0, 1.0):
            def plate(f, k=k, flare=flare, phi0=phi0):
                h = travel_pt(f, tip)
                r = radial(f)
                fw = unit(add(sub(travel_pt(f, tip), travel_pt(f, tip - 1)), [1.2, 0.0, 0.0]))
                across = unit(cross(fw, r))
                return add(add(h, mul(fw, -1.3)), add(mul(r, 2.6), mul(across, spread * flare)))
            hoods.append(_unit_at(CHARGE, 0, 2, plate,
                                  lambda f, k=k: unit(add(sub(travel_pt(f, tip, phi0=-fan / 2 + dphi * k, k=k),
                                                              travel_pt(f, tip - 1, phi0=-fan / 2 + dphi * k, k=k)),
                                                          [1.2, 0.0, 0.0]))))
    if feed:
        hoods = _guard_ring(hoods, mouth, [1.0, 0.0, 0.0], head_ring + 7.0, 1 if v == 7 else -1)
        return units + hoods, mouth
    return units + hoods, None


# ───────────────────────────────────────────────────────────────── form 3: the Lord of the Dance (the ascension)

def _poly_len(pts):
    return sum(math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]) for i in range(len(pts) - 1))


def _sample(pts, step):
    """Points every `step` along a polyline of (u, v) pairs, each with its unit tangent."""
    out, carry = [], 0.0
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        if L < 1e-9:
            continue
        tu, tv = (b[0] - a[0]) / L, (b[1] - a[1]) / L
        d = carry
        while d <= L:
            out.append((a[0] + tu * d, a[1] + tv * d, tu, tv))
            d += step
        carry = d - L
    return out


def _arc(cu, cv, r, a0, a1, n):
    return [(cu + r * math.sin(math.radians(a0 + (a1 - a0) * k / n)), cv + r * math.cos(math.radians(a0 + (a1 - a0) * k / n)))
            for k in range(n + 1)]


DANCER_SCALE = 1.45        # the figure's size in plan voxels per sketch unit
DANCER_TARGET = 200        # statue units before thinning (x density 3 in game): every form is at least as big as the
                           # one before it, so a commit only ever GROWS the body (eggs, paid from the bank) - a smaller
                           # next form would leave its surplus crowding the new shape's wells
DANCER_RING = (40.0, -2.0) # the HALO: radius and centre height, sketch units (the mode draws it as twelve rings)
DANCER_PACKS = 4           # attendant packs patrolling outside the halo
DANCER_PACK_OUT = 12.0     # their orbit, plan voxels outside the halo
DANCER_PACK_UNITS = 7      # a centre and six round it
DANCER_FRAME_STEPS = 40    # sim steps per frame: 8 frames x 40 steps at 10 Hz = one patrol turn per 32 s (the figure
                           # itself barely moves between frames, so a slow loop is only the attendants' pace)
DANCER = {
    1: dict(mirror=1, hair=25.0, sense=1),
    2: dict(mirror=-1, hair=25.0, sense=-1),   # the mirror pose: the other foot raised, the drum in the other hand
    3: dict(mirror=1, hair=30.0, sense=-1),    # the hair flown wider
}


def dancer(v, pose="travel"):
    """THE LORD OF THE DANCE (the Nataraja) as a sculpture of tadpoles, which the swarm assembles at the ascension.

    The figure stands in the body's (z across, y up) plane and faces -x: the right leg on the prone dwarf, the left
    leg raised and swung across, four arms (the drum, a gem held high, the open palm, and the arm across the chest
    pointing at the lifted foot), the crown, and two locks of hair flying out each side. The limbs are double strokes
    so the pose reads at a distance. Outside the HALO (twelve rings the mode draws), four ATTENDANT packs of Time units
    (the bestiary's pack hunters) orbit across the eight frames - two each way, one turn per loop - so the sort core
    carries them round with no new API, and whichever halo ring a pack is over is guarded. Nothing here is fire: the
    raised hand holds a Charge gem."""
    p = DANCER[v]
    s, m = DANCER_SCALE, p["mirror"]
    parts = [  # (elem, tier, polyline in sketch units, kind)
        (MASS, 0, [(-18, -40), (18, -40)], ''), (MASS, 0, [(-14, -38.5), (14, -38.5)], ''),            # the pedestal
        (TIME, 0, [(-13, -35), (8, -35)], ''), (TIME, 0, [(-12, -33.2), (7, -33.2)], ''),                # the dwarf
        (TIME, 0, _arc(10.5, -34, 2, 0, 360, 8), ''),
        (MASS, 0, [(-2, -6), (-8, -17), (-3, -31)], 'thick'), (MASS, 0, [(-6, -32), (0.5, -32)], ''),    # standing leg
        (MASS, 0, [(3, -6), (15, -10), (3, -16)], 'thick'), (MASS, 0, [(3, -16), (-1.5, -18)], ''),      # raised leg
        (MASS, 0, [(-3.5, -5), (-2.5, 1), (-4.5, 10)], ''), (MASS, 0, [(0, -5), (0, 11)], ''),           # torso
        (MASS, 0, [(3.5, -5), (2.5, 1), (4.5, 10)], ''), (MASS, 0, [(-6, 11), (6, 11)], ''),
        (CHARGE, 1, [(-4, -4), (4, -4)], ''),                                                             # sash
        (MASS, 0, _arc(0, 16, 3.2, 0, 360, 10), ''), (MASS, 0, [(0, 11), (0, 13)], ''),                  # head, neck
        (CHARGE, 1, [(0, 19.5), (0, 29)], ''), (CHARGE, 1, [(0, 19.5), (-4, 28)], ''),                   # the crown
        (CHARGE, 1, [(0, 19.5), (4, 28)], ''), (CHARGE, 1, [(0, 19.5), (-7, 25.5)], ''),
        (CHARGE, 1, [(0, 19.5), (7, 25.5)], ''),
        (MASS, 0, [(-6, 11), (-15, 13), (-17, 20)], 'thick'),                                            # drum arm
        (CHARGE, 1, [(-19.5, 25.5), (-14.5, 25.5), (-17, 23), (-19.5, 20.5), (-14.5, 20.5), (-17, 23)], ''),  # drum
        (MASS, 0, [(6, 11), (15, 13), (17, 20)], 'thick'),                                               # the raised arm
        (CHARGE, 2, [(17, 21), (15.4, 23.2), (17, 25.4), (18.6, 23.2), (17, 21)], 'gem'),                # the gem
        (MASS, 0, [(-5, 9), (-11, 3), (-9.5, 9.5)], 'thick'), (TIME, 0, _arc(-9.5, 11, 1.4, 0, 360, 6), ''),  # palm
        (MASS, 0, [(5, 9), (1, 5), (-5, 3), (-7, -1)], 'thick'),                                         # across the chest
    ]
    for side in (-1, 1):                                                                                  # the hair
        for end_v, wob in ((22.0, 0.4), (14.0, 1.2)):
            pts = [(side * (3 + p["hair"] * k / 12), 18 + (end_v - 18) * k / 12 + 1.4 * math.sin(k / 12 * 7 + wob))
                   for k in range(13)]
            parts.append((SPACE, 0, pts, 'hair'))
    total = sum(_poly_len(q[2]) * (2 if q[3] == 'thick' else 1) for q in parts) * s
    step = max(2.35, total / DANCER_TARGET)
    units = []
    for elem, tier, pts, kind in parts:
        for (u0, v0, tu, tv) in _sample([(m * u * s, v * s) for u, v in pts], step):
            for off in ((-1.1, 1.1) if kind == 'thick' else (0.0,)):
                u_, v_ = u0 - tv * off * s, v0 + tu * off * s
                depth = 0.0 if kind == 'thick' or elem != MASS or abs(u_) >= 6 * s else (0.9 if len(units) % 2 else -0.9)
                un = Unit(elem, 0 if v_ > -5 * s else 1, tier)
                for f in range(FRAMES):
                    ph = 2 * math.pi * f / FRAMES
                    vv = v_
                    if kind == 'hair':
                        vv += 0.9 * s * math.sin(ph + abs(u_) * 0.12) * abs(u_) / (28 * s)
                    un.pos[f] = [depth, vv, u_]
                    un.face[f] = unit([0.0, tv, tu])
                units.append(un)
    # the attendants: four packs orbiting the halo in its plane, two each way, one full turn per frame loop
    rr, vc = DANCER_RING[0] * s, DANCER_RING[1] * s
    orbit = rr + DANCER_PACK_OUT
    shell = [(0, 0, 0)] + [(MIN_GAP * 1.15 * a, MIN_GAP * 1.15 * b, MIN_GAP * 1.15 * c)
                           for a, b, c in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
    for k in range(DANCER_PACKS):
        a0, sense = 2 * math.pi * (k + 0.5) / DANCER_PACKS, (1 if k % 2 == 0 else -1) * p["sense"]
        for (ox, oy, oz) in shell[:DANCER_PACK_UNITS]:
            un = Unit(TIME, 0 if k % 2 == 0 else 1)
            for f in range(FRAMES):
                a = a0 + sense * 2 * math.pi * f / FRAMES
                un.pos[f] = [(4.0 if k % 2 else -4.0) + ox, vc + orbit * math.cos(a) + oy, orbit * math.sin(a) + oz]
                un.face[f] = unit([0.0, -math.sin(a) * sense, math.cos(a) * sense])
            units.append(un)
    return _thin(units), None


# ───────────────────────────────────────────────────────────────── form 4: the Sea Lion

SEA_LION_SCALE = 1.45      # the final form is the biggest animal of the four
SEA_LION_FRAME_STEPS = 12  # 8 frames x 12 steps at 10 Hz = a 9.6 s stroke: fins are Space, the slowest tadpoles
SEA_LION = {
    # fore-fin rays per side, ray length, mane rings, tail stations, fluke (crescent | forked)
    1: dict(rays=4, ray=13.0, mane=2, tail=11, fluke="crescent"),
    2: dict(rays=5, ray=10.5, mane=3, tail=9, fluke="forked"),
    3: dict(rays=3, ray=16.0, mane=2, tail=13, fluke="crescent"),
}


def sea_lion(v, pose="travel"):
    """THE SEA LION: the final form - the heraldic sea-lion, a lion's forepart on a fish's tail. A Mass body and head,
    a Charge mane in rings round the neck, two great fore-fins of Space rays sculling, and a Time fish's tail that
    beats up and down (the HyperSea's swimmers fly, they do not walk) ending in a Charge fluke. No legs. Feeding, the
    mane and the fluke leave the body to orbit its mouth as danger-tier guards."""
    p = SEA_LION[v]
    s = SEA_LION_SCALE
    feed = pose == "feed"
    calm = FEED_SETTLE if feed else 1.0
    ga = math.pi * (3 - math.sqrt(5))
    units, plates = [], []

    def stroke(f, lag=0.0):
        return math.sin(2 * math.pi * f / FRAMES - lag)

    nb = 150                                                                   # the body: a shell on an ellipsoid
    for i in range(nb):
        y = 1 - 2 * (i + 0.5) / nb
        rr, th = math.sqrt(1 - y * y), ga * i
        x, z = math.cos(th) * rr, math.sin(th) * rr
        units.append(_unit_at(MASS, 0 if y > 0 else 1, 0,
                              lambda f, x=x, y=y, z=z: [x * 10.5 * s, (y * 5.0 + 0.3 * calm * stroke(f, x)) * s, z * 4.6 * s],
                              lambda f: [1, 0, 0]))
    for i in range(18):                                                        # the head
        y = 1 - 2 * (i + 0.5) / 18
        rr, th = math.sqrt(1 - y * y), ga * i
        units.append(_unit_at(MASS, 0, 0,
                              lambda f, y=y, rr=rr, th=th: [(13.6 + math.cos(th) * rr * 3.4) * s, (3.6 + y * 3.2) * s,
                                                            math.sin(th) * rr * 3.2 * s], lambda f: [1, 0, 0]))
    nm = 9 * p["mane"]                                                         # the mane: rings of shield plates
    for i in range(nm):
        ring, a = i % p["mane"], 2 * math.pi * (i // p["mane"]) / (nm // p["mane"]) + 0.35 * (i % p["mane"])
        r = 5.8 + 2.4 * ring
        plates.append(_unit_at(CHARGE, 0, 2,
                               lambda f, a=a, r=r, ring=ring: [(10.8 - 1.3 * ring) * s,
                                                               (3.6 + math.cos(a) * r * (1 + 0.04 * stroke(f))) * s,
                                                               math.sin(a) * r * s], lambda f: [1, 0, 0]))
    for side in (-1, 1):                                                       # the fore-fins: rays sculling
        for k in range(p["rays"]):
            for j in range(max(3, round(p["ray"] / SPACING))):
                t = (j + 0.6) / max(3, round(p["ray"] / SPACING))
                # swept BACK along the flank and tilted a little up, like a fish's pectoral fins: a fin that
                # reaches down reads as a leg from a three-quarter view (the 2026-10-06 contact sheet)
                root = [6.2 - 1.9 * k, -1.0, 4.2 * side]
                tip = [root[0] - 0.6 * p["ray"] - 1.4 * k, 1.2, side * (4.2 + 0.72 * p["ray"] - 0.8 * k)]

                def at(f, t=t, root=root, tip=tip, side=side):
                    sc = (0.15 if feed else 0.32) * stroke(f, 0.8 * t)
                    x = root[0] + (tip[0] - root[0]) * t
                    y0 = root[1] + (tip[1] - root[1]) * t
                    z0 = root[2] + (tip[2] - root[2]) * t
                    dy, dz = y0 - root[1], z0 - root[2]
                    c, sn = math.cos(sc * side), math.sin(sc * side)
                    fold = 0.55 if feed else 1.0                               # feeding, the fins fold back
                    return [x * s, (root[1] + dy * c + dz * sn) * s, (root[2] + fold * (dz * c - dy * sn)) * s]
                units.append(_unit_at(SPACE, 1, 0, at, lambda f, root=root, tip=tip: sub(tip, root)))
    n_tail = p["tail"]                                                         # the fish's tail: beats up and down
    for k in range(n_tail):
        t = (k + 1) / n_tail
        r = (2.0 - 0.8 * t)

        def spine(f, t=t, k=k):
            amp = (0.4 + 3.2 * t * t) * calm
            return [(-10.5 - k * SPACING / s) * s, (0.4 + amp * stroke(f, 2.2 * t)) * s, 0.0]
        for ang, slot in ((90, 0), (270, 1)):
            units.append(_unit_at(TIME, slot, 0,
                                  lambda f, spine=spine, ang=ang, r=r: add(spine(f), [0.0, r * math.sin(math.radians(ang)), 0.0]),
                                  lambda f: [-1, 0, 0]))
    tail_end = (-10.5 - n_tail * SPACING / s) * s
    lobes = [(z, 0.0) for z in (-6.0, -3.6, -1.2, 1.2, 3.6, 6.0)] if p["fluke"] == "crescent" else \
            [(z, 1.0) for z in (-7.0, -4.6, -2.3)] + [(z, 1.0) for z in (2.3, 4.6, 7.0)]
    for z, forked in lobes:                                                    # the fluke: Charge plates, horizontal
        back = 0.08 * z * z + (0.6 * abs(z) if forked else 0.0)

        def at(f, z=z, back=back):
            amp = 3.6 * calm
            return [tail_end - back, (0.4 + amp * stroke(f, 2.4)) * s, z * s]
        plates.append(_unit_at(CHARGE, 1, 2, at, lambda f: [0.0, 1.0, 0.0]))
    if feed:
        mouth = [18.5 * s, 3.0 * s, 0.0]
        plates = _guard_ring(plates, mouth, [1.0, 0.0, 0.0], 9.5 * s, 1 if v != 2 else -1)
        return units + plates, mouth
    return units + plates, None


# ───────────────────────────────────────────────────────────────── the catalogue

FORM_NAMES = ["Great Serpent", "Many-Headed Serpent", "Lord of the Dance", "Sea Lion"]


def _variants():
    """Every plan the swarm config lists, in order: (key, form index, display name, builder, feeds). A feeding form has a
    strike pose (`_feed`); the two serpents also eat in each of the COILS (`_coil`, `_wrap`, `_eight`)."""
    out = []
    for v in (1, 2, 3):
        out.append((f"great_serpent_{v}", 0, "Great Serpent", lambda pose, v=v: great_serpent(v, pose), True))
    for v in (5, 7, 10):
        out.append((f"many_headed_{v}", 1, f"{HEAD_WORDS[v]}-Headed Serpent", lambda pose, v=v: many_headed(v, pose), True))
    for v in (1, 2, 3):
        out.append((f"dancer_{v}", 2, "Lord of the Dance", lambda pose, v=v: dancer(v, pose), False))
    for v in (1, 2, 3):
        out.append((f"sea_lion_{v}", 3, "Sea Lion", lambda pose, v=v: sea_lion(v, pose), True))
    return out


VARIANTS = _variants()


def coils_of(form):
    """The meal formations a form eats in (none: it eats in its strike pose)."""
    return COILS if form in (0, 1) else ()


def plan_keys():
    """Every plan key in swarm-config order: each variant's travel plan, then its strike pose (`_feed`) when it eats,
    then its coils."""
    keys = []
    for key, form, name, build, feeds in VARIANTS:
        keys.append(key)
        if feeds:
            keys.append(key + "_feed")
            keys += [f"{key}_{c}" for c in coils_of(form)]
    return keys


def split_key(key):
    """(variant key, pose): pose is "travel", "feed" or one of COILS."""
    for pose in ("feed",) + COILS:
        if key.endswith("_" + pose):
            return key[:-len(pose) - 1], pose
    return key, "travel"


def ring_of(c):
    """The halo in the dancer plan's own (re-centred) voxels, before the density upsample: its centre [x, y, z] (the
    plan is baked centred on frame 0's centroid <c>), its radius, and the radius the attendant packs patrol at (each
    halo ring's GUARD POST sits there, straight out from it)."""
    return {"centre": [_r(-c[0]), _r(DANCER_RING[1] * DANCER_SCALE - c[1]), _r(-c[2])],
            "radius": _r(DANCER_RING[0] * DANCER_SCALE), "guardOrbit": _r(DANCER_RING[0] * DANCER_SCALE + DANCER_PACK_OUT)}


def dancer_ring(plan):
    """Where the mode draws the halo, in this plan's own (re-centred) voxels: (centre [x, y, z], radius, the
    attendants' patrol radius). The plan JSON carries the same as "ring" (the harness reads it there)."""
    r = plan["ring"]
    return r["centre"], r["radius"], r["guardOrbit"]


# ───────────────────────────────────────────────────────────────── bake + validate

MAJOR = MASS   # every form is Mass-majority (the director, not the census, picks the form)


def _r(x):
    return round(float(x), 3)


def _frame_steps(key):
    if split_key(key)[1] in COILS:
        return COIL_FRAME_STEPS
    if key.endswith("_feed") or key.startswith("dancer"):
        return FEED_FRAME_STEPS if key.endswith("_feed") else DANCER_FRAME_STEPS
    return SEA_LION_FRAME_STEPS if key.startswith("sea_lion") else FRAME_STEPS


_BUILT = {}


def _build_variant(base):
    """Every pose of one variant - travel, and (for a feeding form) its strike pose and coils - built together and
    THINNED together, so all keep the same members in the same order."""
    if base not in _BUILT:
        entry = next(e for e in VARIANTS if e[0] == base)
        names = ["travel"] + (["feed"] + list(coils_of(entry[1])) if entry[4] else [])
        built = {name: entry[3](name) for name in names}
        n = len(built["travel"][0])
        for name, (units, _) in built.items():
            assert len(units) == n, f"{base}: the {name} pose builds {len(units)} units, the travel pose {n}"
        keep = _keep([built[name][0] for name in names])
        _BUILT[base] = (entry, {name: ([units[i] for i in keep], mouth) for name, (units, mouth) in built.items()})
    return _BUILT[base]


def _build(key):
    base, pose = split_key(key)
    entry, poses = _build_variant(base)
    units, mouth = poses[pose]
    if pose == "travel" and "feed" in poses:
        mouth = poses["feed"][1]   # the travel plan's mouth is the strike pose's, in its own frame: how it lines up on a plant
    return entry, units, mouth


def bake(key):
    (base, form, name, _, feeds), units, mouth = _build(key)
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
    out = {
        "kind": key, "name": name, "form": form, "major": MAJOR, "n": n, "frames": FRAMES,
        "frameSteps": _frame_steps(key),
        "order": list(range(FRAMES)),   # every Tandava form loops (no ping-pong)
        "elem": [u.elem for u in units], "tier": [u.tier if u.elem == CHARGE else 0 for u in units], "half": half0,
        "pos": pos, "face": face, "swimAxis": [1, 0, 0], "upAxis": [0, 1, 0],
        "slot": [u.slot for u in units], "halfF": half_f, "tierF": tier_f, "sp": sp_f,
        "centroid": [_r(x) for x in c],
    }
    if form == 2:
        out["ring"] = ring_of(c)
    if mouth is not None:
        out["mouth"] = [_r(mouth[a] - c[a]) for a in range(3)]   # where the director puts the food, plan voxels
    return out


def bake_all():
    return {k: bake(k) for k in plan_keys()}


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
        base, pose = split_key(kind)
        if pose != "travel":
            twin = plans.get(base)
            if twin is None or census(twin) != m:
                errors.append(f"{kind}: a feed twin must carry its travel plan's element counts exactly "
                              f"({m} vs {census(twin) if twin else 'missing'}) - the commit is a re-sort, never a molt")
            if not any(t == 1 for e, t in zip(p["elem"], p["tier"]) if e == CHARGE):
                errors.append(f"{kind}: a feed twin deploys no danger-tier guards")
            if "mouth" not in p:
                errors.append(f"{kind}: a feed twin bakes no mouth")
    forms = sorted({p["form"] for p in plans.values()})
    if forms != [0, 1, 2, 3]:
        errors.append(f"the plans make forms {forms}, not the four")
    for form in range(4):
        if sum(1 for p in plans.values() if p["form"] == form and split_key(p["kind"])[1] == "travel") != 3:
            errors.append(f"form {form} does not have three variants")
    # every form is at least as big as EVERY variant of the one before it (a match draws the variants independently):
    # a commit then only ever GROWS the body - eggs, paid from the bank - and never leaves a surplus crowding the new
    # shape's wells into a blob
    for form in range(1, 4):
        prev = max(p["n"] for p in plans.values() if p["form"] == form - 1)
        least = min(p["n"] for p in plans.values() if p["form"] == form)
        if least < prev:
            errors.append(f"form {form}'s smallest variant ({least} units) is smaller than form {form - 1}'s biggest ({prev})")
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
        print(f"{kind:22s} n={n:3d}  C/M/S/T {m[0]:3d}/{m[1]:3d}/{m[2]:3d}/{m[3]:3d} "
              f"({100 * m[0] / n:.0f}/{100 * m[1] / n:.0f}/{100 * m[2] / n:.0f}/{100 * m[3] / n:.0f}%)  "
              f"min gap {min(gaps):.2f}  slots {p['slot'].count(0)}/{p['slot'].count(1)}  steps {p['frameSteps']}")
    errors = validate(plans)
    for e in errors:
        print("FAIL:", e)
    print("tandava plans: " + ("OK" if not errors else f"{len(errors)} failure(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
