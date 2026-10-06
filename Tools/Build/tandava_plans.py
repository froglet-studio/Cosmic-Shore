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
# The Bull FLIES (the HyperSea has no ground to walk on): its four legs are FINS swept back AND out from the body's
# lower flanks like a manta's - the fore pair spreading wide, the hind pair trailing past the rump - rippling together in
# one stroke from root to tip, never a gait. Swept back alone they read as two hanging columns from the front (the
# 2026-10-06 artifact screenshot); spread sideways they read as fins from every side. (x, y, z) of each root, and the
# unit direction it sweeps (x, y, z), z mirrored per side.
BULL_LEGS = [((7.0, -4.2, 4.4), (-0.55, -0.12, 0.83)), ((7.0, -4.2, -4.4), (-0.55, -0.12, -0.83)),
             ((-7.0, -3.6, 4.2), (-0.83, -0.06, 0.56)), ((-7.0, -3.6, -4.2), (-0.83, -0.06, -0.56))]
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
    # the legs: four Time FINS swept back (a bull in flight), all four on one stroke - a wave that travels from the
    # root to the tip, flaring the tips a little outward on the downstroke
    for root, d in BULL_LEGS:
        for j in range(BULL_LEG_UNITS):
            base = add(root, mul(list(d), (j + 1) * SPACING))
            depth = (j + 1) / BULL_LEG_UNITS

            def stroke(ph, depth=depth):
                return add(bob(ph), [0.0, 1.1 * depth * math.sin(ph - 1.6 * depth), 0.0])   # the fin beats up and down
            place(TIME, 1, base, list(d), anim=stroke)
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


# ───────────────────────────────────────────────────────────────── the ascension: the Lord of the Dance

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


def _thin(units):
    """Drop any unit that comes closer than MIN_GAP to one kept before it, in ANY frame (strokes cross at joints)."""
    kept = []
    for u in units:
        if all(min(norm(sub(u.pos[f], k.pos[f])) for f in range(FRAMES)) >= MIN_GAP for k in kept):
            kept.append(u)
    return kept


DANCER_SCALE = 1.15        # the figure's size in plan voxels per sketch unit
DANCER_TARGET = 112        # statue units before thinning (x density 3 in game)
DANCER_RING = (40.0, -2.0) # the ring of fire: radius and centre height, sketch units (the mode draws it as 12 flames)
DANCER_PACKS = 4           # attendant packs patrolling outside the ring
DANCER_PACK_OUT = 12.0     # their orbit, plan voxels outside the ring
DANCER_PACK_UNITS = 7      # a centre and six round it
DANCER_FRAME_STEPS = 40    # sim steps per frame: 8 frames x 40 steps at 10 Hz = one patrol turn per 32 s (the statue
                           # itself barely moves between frames, so a slow loop is only the attendants' pace)


def dancer():
    """THE LORD OF THE DANCE (the Nataraja) as a sculpture of tadpoles, which the swarm assembles at the ascension.

    The figure stands in the body's (z across, y up) plane and faces -x, back down the course toward the pilots: the
    right leg on the prone dwarf, the left leg raised and swung across, four arms (the drum, the fire, the open palm,
    and the arm across the chest pointing at the lifted foot), the crown, and two locks of hair flying out each side.
    The limbs are double strokes so the pose reads at a distance. Outside the ring of fire, four ATTENDANT packs of
    Time units (the bestiary's pack hunters) orbit the ring across the eight frames - two each way, one turn per loop -
    so the sort core carries them round with no new API, and whichever flame a pack is over is guarded."""
    s = DANCER_SCALE
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
        (MASS, 0, [(6, 11), (15, 13), (17, 20)], 'thick'),                                               # fire arm
        (CHARGE, 2, [(17, 20.5), (15.5, 23), (17, 27.5), (18.5, 23), (17, 20.5)], 'flame'),              # the fire
        (MASS, 0, [(-5, 9), (-11, 3), (-9.5, 9.5)], 'thick'), (TIME, 0, _arc(-9.5, 11, 1.4, 0, 360, 6), ''),  # palm
        (MASS, 0, [(5, 9), (1, 5), (-5, 3), (-7, -1)], 'thick'),                                         # across the chest
    ]
    for side in (-1, 1):                                                                                  # the hair
        for end_v, wob in ((22.0, 0.4), (14.0, 1.2)):
            pts = [(side * (3 + 25 * k / 12), 18 + (end_v - 18) * k / 12 + 1.4 * math.sin(k / 12 * 7 + wob)) for k in range(13)]
            parts.append((SPACE, 0, pts, 'hair'))
    total = sum(_poly_len(p[2]) * (2 if p[3] == 'thick' else 1) for p in parts) * s
    step = max(2.35, total / DANCER_TARGET)
    units = []
    for elem, tier, pts, kind in parts:
        for (u0, v0, tu, tv) in _sample([(u * s, v * s) for u, v in pts], step):
            for off in ((-1.1, 1.1) if kind == 'thick' else (0.0,)):
                u_, v_ = u0 - tv * off * s, v0 + tu * off * s
                depth = 0.0 if kind == 'thick' or elem != MASS or abs(u_) >= 6 * s else (0.9 if len(units) % 2 else -0.9)
                un = Unit(elem, 0 if v_ > -5 * s else 1, tier)
                for f in range(FRAMES):
                    ph = 2 * math.pi * f / FRAMES
                    v = v_
                    if kind == 'hair':
                        v += 0.9 * s * math.sin(ph + abs(u_) * 0.12) * abs(u_) / (28 * s)
                    if kind == 'flame':
                        v += 0.5 * math.sin(2 * ph + u_)
                    un.pos[f] = [depth, v, u_]
                    un.face[f] = unit([0.0, tv, tu])
                units.append(un)
    # the attendants: four packs orbiting the ring in its plane, two each way, one full turn per frame loop
    rr, vc = DANCER_RING[0] * s, DANCER_RING[1] * s
    orbit = rr + DANCER_PACK_OUT
    shell = [(0, 0, 0)] + [(MIN_GAP * 1.15 * a, MIN_GAP * 1.15 * b, MIN_GAP * 1.15 * c)
                           for a, b, c in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
    for k in range(DANCER_PACKS):
        a0, sense = 2 * math.pi * (k + 0.5) / DANCER_PACKS, (1 if k % 2 == 0 else -1)
        for (ox, oy, oz) in shell[:DANCER_PACK_UNITS]:
            un = Unit(TIME, 0 if k % 2 == 0 else 1)
            for f in range(FRAMES):
                a = a0 + sense * 2 * math.pi * f / FRAMES
                un.pos[f] = [(4.0 if k % 2 else -4.0) + ox, vc + orbit * math.cos(a) + oy, orbit * math.sin(a) + oz]
                un.face[f] = unit([0.0, -math.sin(a) * sense, math.cos(a) * sense])
            units.append(un)
    return _thin(units)


# ───────────────────────────────────────────────────────────────── the final form: the Winged Lion

LION_SCALE = 1.2
LION_TARGET = 155
LION_FLAP = 0.35           # the wingbeat's half-angle (radians)
LION_FRAME_STEPS = 12      # 8 frames x 12 steps at 10 Hz = a 9.6 s wingbeat. The feathers are Space, the slowest
                           # tadpoles (0.8 voxels/step): at the shared 6 steps and a 0.6 rad stroke the tips swept
                           # ~3 voxels/step and the live wings smeared above the body (harness picture, 2026-10-06)


def lion():
    """THE WINGED LION: the final form, and the only form the exit lets through. A lion's body under a Charge mane,
    two great feathered wings beating across the eight frames, a long tail - and NO legs: it flies, so two flame
    ribbons stream from its haunches instead."""
    s, N, units = LION_SCALE, LION_TARGET, []
    ga = math.pi * (3 - math.sqrt(5))

    def add_unit(elem, tier, at, face, slot):
        un = Unit(elem, slot, tier)
        for f in range(FRAMES):
            un.pos[f] = at(f)
            un.face[f] = unit(face(f))
        units.append(un)

    nb = round(N * 0.33)
    for i in range(nb):                                                     # the body: a shell on an ellipsoid
        y = 1 - 2 * (i + 0.5) / nb
        rr, th = math.sqrt(1 - y * y), ga * i
        x, z = math.cos(th) * rr, math.sin(th) * rr
        add_unit(MASS, 0, lambda f, x=x, y=y, z=z: [x * 13 * s, (y * 5.5 + 0.3 * math.sin(2 * math.pi * f / FRAMES + x)) * s, z * 4.8 * s],
                 lambda f: [1, 0, 0], 0 if y > 0 else 1)
    nh = round(N * 0.06)
    for i in range(nh):                                                     # the head
        y = 1 - 2 * (i + 0.5) / nh
        rr, th = math.sqrt(1 - y * y), ga * i
        add_unit(MASS, 0, lambda f, y=y, rr=rr, th=th: [(16 + math.cos(th) * rr * 3.6) * s, (4 + y * 3.4) * s, math.sin(th) * rr * 3.4 * s],
                 lambda f: [1, 0, 0], 0)
    nm = round(N * 0.12)
    for i in range(nm):                                                     # the mane: two rings of shield plates
        outer, a = i % 2, 2 * math.pi * i / nm
        r = 8.5 if outer else 6.0
        add_unit(CHARGE, 1, lambda f, a=a, r=r, outer=outer: [(10.5 if outer else 12) * s,
                 (4 + math.cos(a) * r * (1 + 0.04 * math.sin(2 * math.pi * f / FRAMES))) * s, math.sin(a) * r * s],
                 lambda f: [1, 0, 0], 0)
    nwf = max(3, round(N * 0.22 / 10))
    for side in (-1, 1):                                                    # the wings: five feathers each, flapping
        for k in range(5):
            root, tip = [6 - 2.4 * k, 5.5, 4 * side], [2 - 5 * k, 9 - 1.5 * k, (34 - 3 * k) * side]
            for j in range(nwf):
                t = (j + 0.5) / nwf

                def at(f, t=t, root=root, tip=tip, side=side):
                    flap = LION_FLAP * math.sin(2 * math.pi * f / FRAMES - t * 0.8)
                    x = root[0] + (tip[0] - root[0]) * t
                    y0 = root[1] + (tip[1] - root[1]) * t + 2.5 * math.sin(t * math.pi)
                    z0 = root[2] + (tip[2] - root[2]) * t
                    dy, dz = y0 - root[1], z0 - root[2]
                    c, sn = math.cos(flap * side), math.sin(flap * side)
                    return [x * s, (root[1] + dy * c + dz * sn) * s, (root[2] + dz * c - dy * sn) * s]
                add_unit(SPACE, 0, at, lambda f, root=root, tip=tip: sub(tip, root), 0)
    # no legs (it FLIES - the HyperSea has no ground): two flame RIBBONS of Time stream from its haunches, back past
    # the rump on either side of its tail like a comet's, rippling, each ending in a Charge flame tip. They start at the
    # body's mid-height, never hang below the belly, and spread into a swallow-tail V: from a chase camera behind it
    # they read as two contrails, not as legs dangling toward the viewer
    nr = max(6, round(N * 0.16 / 2))

    def ribbon_at(t, side, f):
        ph = 2 * math.pi * f / FRAMES
        return [(-11.0 - 26.0 * t) * s, (-2.0 + 1.2 * t + 2.0 * t * math.sin(ph - 3.0 * t)) * s,
                side * (4.6 + 9.0 * t + 1.2 * t * math.sin(ph - 3.0 * t + 1.0)) * s]
    for side in (-1, 1):
        for j in range(nr):
            t = (j + 0.5) / nr
            add_unit(TIME, 0, lambda f, t=t, side=side: ribbon_at(t, side, f), lambda f: [-1, -0.05, 0], 1)
        add_unit(CHARGE, 1, lambda f, side=side: ribbon_at(1.0 + 1.0 / nr, side, f), lambda f: [-1, 0, 0], 1)
    nt = max(5, round(N * 0.05))
    for k in range(1, nt + 1):                                              # the tail and its tuft
        add_unit(SPACE, 0, lambda f, k=k: [(-13 - 2.4 * k) * s, (1 + 0.9 * k + 0.1 * k * k) * s,
                 1.5 * math.sin(2 * math.pi * f / FRAMES + k * 0.5) * s], lambda f: [-1, 0.5, 0], 1)
    e = nt + 1
    for k in range(3):
        add_unit(CHARGE, 1, lambda f, k=k: [(-13 - 2.4 * e) * s - k * 1.4, (1 + 0.9 * e + 0.1 * e * e) * s + (k - 1) * 1.6,
                 1.5 * math.sin(2 * math.pi * f / FRAMES + e * 0.5) * s], lambda f: [-1, 0, 0], 1)
    return _thin(units)


def ring_of(c):
    """The ring of fire in the dancer plan's own (re-centred) voxels, before the density upsample: its centre [x, y, z]
    (the plan is baked centred on frame 0's centroid <c>), its radius, and the radius the attendant packs patrol at
    (each flame's GUARD POST sits there, straight out from the flame)."""
    return {"centre": [_r(-c[0]), _r(DANCER_RING[1] * DANCER_SCALE - c[1]), _r(-c[2])],
            "radius": _r(DANCER_RING[0] * DANCER_SCALE), "guardOrbit": _r(DANCER_RING[0] * DANCER_SCALE + DANCER_PACK_OUT)}


def dancer_ring(plan):
    """Where the mode draws the ring of fire, in this plan's own (re-centred) voxels: (centre [x, y, z], radius, the
    attendants' patrol radius). The plan JSON carries the same as "ring" (the harness reads it there)."""
    r = plan["ring"]
    return r["centre"], r["radius"], r["guardOrbit"]


# ───────────────────────────────────────────────────────────────── bake + validate

PLANS = {
    "serpent_s": ("Serpent (small)", lambda: serpent("serpent_s")),
    "serpent_m": ("Serpent (medium)", lambda: serpent("serpent_m")),
    "serpent_l": ("Serpent (large)", lambda: serpent("serpent_l")),
    "bull": ("Bull", bull),
    "dancer": ("Lord of the Dance", dancer),
    "lion": ("Winged Lion", lion),
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
        "kind": kind, "name": name, "major": MAJOR, "n": n, "frames": FRAMES,
        "frameSteps": {"dancer": DANCER_FRAME_STEPS, "lion": LION_FRAME_STEPS}.get(kind, FRAME_STEPS),
        "order": list(range(FRAMES)),   # every Tandava form loops (no ping-pong)
        "elem": [u.elem for u in units], "tier": [u.tier if u.elem == CHARGE else 0 for u in units], "half": half0,
        "pos": pos, "face": face, "swimAxis": [1, 0, 0], "upAxis": [0, 1, 0],
        "slot": [u.slot for u in units], "halfF": half_f, "tierF": tier_f, "sp": sp_f,
        "centroid": [_r(x) for x in c],
        **({"ring": ring_of(c)} if kind == "dancer" else {}),
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
        for e, want in SHARES.get(kind, {}).items():
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
