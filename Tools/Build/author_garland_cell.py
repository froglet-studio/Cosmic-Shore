#!/usr/bin/env python3
"""
Author the GARLAND cell - the freestyle world cut for the HOME SCREEN camera.

Why a script and not a folder of hand-typed assets: this cell's PhaseThresholds have to ride
what its generator actually emits and what its roster actually grows, and a hand-typed ladder
drifts the first time either moves. This file holds

  1. an exact offline mirror of Assets/_Scripts/.../SpawnableGarland.cs (count, volume,
     per-domain volume, radial extremes),
  2. the invariants that mirror has to prove, with a NEGATIVE CONTROL so a check nobody has
     watched fail is not mistaken for a check that works,
  3. the lifeform roster model, and
  4. the ladder derived from 1 + 3,

and emits every asset. See Docs/ECOSYSTEM.md §18/§48 and the /ecology skill §4.5 / §7.

THE MIRROR IS EXACT, NOT ESTIMATED
----------------------------------
SpawnableGarland draws NOTHING from the base class's shared System.Random (`RangeF`/`Jit`) and
nothing from value noise - every wobble is `Hash01` of the emitting index, which is an integer
hash this file reproduces bit for bit. That is why the numbers below are measurements rather
than estimates, and it is the whole reason the generator was written that way.

The one estimated quantity is the GROWN flora's volume: PhyllotacticFlora sizes its prisms by
ROLE (a stem spans its segment, a leaf spans its reach), so there is no authored field to read.
It lives in CALIBRATION below with its derivation, and it is deliberately the least load-bearing
number here - the flora is ~7% of this cell's volume, so even a 2x error moves the volume ladder
by less than a seventh of its trail band. The flora COUNT, which is what the Frenzy COUNT
backstop reads, is exact (per-element budgets are authored ints).

Usage:
    python3 Tools/Build/author_garland_cell.py            # write + print the table
    python3 Tools/Build/author_garland_cell.py --check    # verify, exit 1 on drift
    python3 Tools/Build/author_garland_cell.py --report   # print the table, write nothing
"""

import collections
import hashlib
import json
import math
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SO = os.path.join(REPO, "Assets", "_SO_Assets")
CELL_DIR = os.path.join(SO, "Cell Configs", "Garland Cell")
PREFAB = os.path.join(REPO, "Assets", "_Prefabs", "Spawnables", "SpawnableGarland.prefab")
SOURCE = os.path.join(REPO, "Assets", "_Scripts", "Controller", "Environment",
                      "MiniGameObjects", "SpawnableGarland.cs")
HARNESS = os.path.join(REPO, "Tools", "Build", "garland_harness")
MEASURED = os.path.join(REPO, "Tools", "Build", "garland_measurements.json")

# ─────────────────────────────────────────────────────────────────────────────
# 1. The mirror - transliterated from SpawnableGarland.cs, family by family
# ─────────────────────────────────────────────────────────────────────────────

GOLDEN = 2.39996323

NUCLEUS_R, MEMBRANE_R, CAM_R = 392.0, 1200.0, 686.0
NUCLEUS_MARGIN = 16.0
SHORE_R, SHORE_BAND_STEP, SHORE_BANDS = 430.0, 21.0, 3
BOUGH_P, BOUGH_Q, BOUGH_MAJOR, BOUGH_MINOR, BOUGH_STEP = 2, 3, 700.0, 170.0, 22.0
VINE_P, VINE_Q, VINE_MAJOR, VINE_MINOR, VINE_STEP = 3, 2, 555.0, 85.0, 26.0
BLOSSOMS, VINE_BLOSSOMS = 9, 5
BLOSSOM_PETALS, VINE_BLOSSOM_PETALS = 84, 46
BLOSSOM_RADIUS, VINE_BLOSSOM_RADIUS = 92.0, 40.0
FALLS, CROWNS, TERRACES, SKIRTS, MOTES = 16, 16, 5, 34, 260
KNOT_AXIS = (0.36, 0.88, 0.31)

# Every constant above is read back out of the C# on --check, so the two cannot drift silently.
MIRRORED_CONSTS = {
    "NucleusR": NUCLEUS_R, "MembraneR": MEMBRANE_R, "CamR": CAM_R,
    "NucleusMargin": NUCLEUS_MARGIN, "ShoreR": SHORE_R, "ShoreBandStep": SHORE_BAND_STEP,
    "ShoreBands": SHORE_BANDS, "BoughP": BOUGH_P, "BoughQ": BOUGH_Q,
    "BoughMajor": BOUGH_MAJOR, "BoughMinor": BOUGH_MINOR, "BoughStep": BOUGH_STEP,
    "VineP": VINE_P, "VineQ": VINE_Q, "VineMajor": VINE_MAJOR, "VineMinor": VINE_MINOR,
    "VineStep": VINE_STEP, "Blossoms": BLOSSOMS, "VineBlossoms": VINE_BLOSSOMS,
    "BlossomPetals": BLOSSOM_PETALS, "VineBlossomPetals": VINE_BLOSSOM_PETALS,
    "BlossomRadius": BLOSSOM_RADIUS, "VineBlossomRadius": VINE_BLOSSOM_RADIUS,
    "Falls": FALLS, "Crowns": CROWNS, "Terraces": TERRACES, "Skirts": SKIRTS, "Motes": MOTES,
}


def hash01(n):
    """CellEnvironmentSpawnableBase.Hash01 - unchecked uint arithmetic, reproduced exactly."""
    h = n & 0xFFFFFFFF
    h = ((h ^ 61) ^ (h >> 16)) & 0xFFFFFFFF
    h = (h * 9) & 0xFFFFFFFF
    h ^= h >> 4
    h = (h * 0x27D4EB2D) & 0xFFFFFFFF
    h ^= h >> 15
    return (h & 0xFFFFFF) / float(0x1000000)


def hjit(s, n, amt):
    k = 1.0 + (hash01(n) * 2.0 - 1.0) * amt
    return tuple(max(0.5, c * k) for c in s)


def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, k): return (a[0] * k, a[1] * k, a[2] * k)
def mag(a): return math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def lerp(a, b, t): return a + (b - a) * t


def norm(a):
    m = mag(a)
    return (0.0, 0.0, 0.0) if m < 1e-9 else (a[0] / m, a[1] / m, a[2] / m)


FORWARD, UP, RIGHT = (0.0, 0.0, 1.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)
_W = norm(KNOT_AXIS)
_U = norm(cross(_W, FORWARD))
_V = cross(_W, _U)


def to_world(x, y, z):
    return add(add(mul(_U, x), mul(_V, y)), mul(_W, z))


def knot_point(t, p, q, major, minor):
    r = major + minor * math.cos(q * t)
    return to_world(r * math.cos(p * t), r * math.sin(p * t), minor * math.sin(q * t))


def hub_point(t, p, major):
    return to_world(major * math.cos(p * t), major * math.sin(p * t), 0.0)


def sample_knot(p, q, major, minor, step):
    SUB = 4096
    arc = [0.0] * (SUB + 1)
    prev = knot_point(0.0, p, q, major, minor)
    for i in range(1, SUB + 1):
        cur = knot_point(2.0 * math.pi * i / SUB, p, q, major, minor)
        arc[i] = arc[i - 1] + mag(sub(cur, prev))
        prev = cur
    total = arc[SUB]
    n = max(8, math.floor(total / step + 0.5))
    out, cursor = [], 0
    for k in range(n):
        want = total * k / n
        while cursor < SUB and arc[cursor + 1] < want:
            cursor += 1
        span = arc[cursor + 1] - arc[cursor]
        f = (want - arc[cursor]) / span if span > 1e-6 else 0.0
        t = 2.0 * math.pi * (cursor + f) / SUB
        pos = knot_point(t, p, q, major, minor)
        H = 1e-3
        tangent = norm(sub(knot_point(t + H, p, q, major, minor),
                           knot_point(t - H, p, q, major, minor)))
        outward = norm(sub(pos, hub_point(t, p, major)))
        out.append((pos, tangent, outward))
    return out, total


class Build:
    def __init__(self):
        self.lays = []
        self.family = "?"

    def emit(self, pos, scale, dom, kind="Plain"):
        self.lays.append((pos, scale, dom, kind, self.family))


def build_environment(vine_blossom_radius=VINE_BLOSSOM_RADIUS):
    """vine_blossom_radius is a parameter ONLY so the negative control can widen it and watch
    the nucleus assertion fire. Every real call takes the authored value."""
    b = Build()
    bough, bough_len = sample_knot(BOUGH_P, BOUGH_Q, BOUGH_MAJOR, BOUGH_MINOR, BOUGH_STEP)
    vine, vine_len = sample_knot(VINE_P, VINE_Q, VINE_MAJOR, VINE_MINOR, VINE_STEP)

    b.family = "bough"
    for i, (pos, _t, _o) in enumerate(bough):
        b.emit(pos, hjit((16.0, 7.0, 26.0), i * 13 + 5, 0.12), "Gold" if i % 9 == 0 else "Jade")

    b.family = "vine"
    for i, (pos, _t, _o) in enumerate(vine):
        b.emit(pos, hjit((8.0, 4.0, 30.0), i * 7 + 19, 0.12), "Gold" if i % 11 == 0 else "Jade")

    def lay_blossoms(knot, count, petals, radius, petal, salt_base, fam):
        b.family = fam
        for bi in range(count):
            pos, tan, out = knot[bi * len(knot) // count]
            axis, u_ax = tan, out
            v_ax = cross(axis, u_ax)
            for i in range(petals):
                salt = salt_base + bi * 131 + i
                a = i * GOLDEN
                rr = radius * math.sqrt((i + 0.5) / petals)
                outward = add(mul(u_ax, math.cos(a)), mul(v_ax, math.sin(a)))
                scale = hjit(petal, salt, 0.16)
                d = norm(add(outward, mul(axis, 0.34 * rr / radius)))
                b.emit(add(pos, mul(d, rr + scale[2] * 0.5)), scale,
                       "Ruby" if hash01(salt * 3) < 0.16 else "Gold")
            for c in range(5):
                a = c * GOLDEN
                outward = add(mul(u_ax, math.cos(a)), mul(v_ax, math.sin(a)))
                b.emit(add(pos, mul(outward, 7.0)), (7.0, 7.0, 7.0), "Gold", "SuperShielded")

    lay_blossoms(bough, BLOSSOMS, BLOSSOM_PETALS, BLOSSOM_RADIUS, (9.0, 2.6, 20.0), 1000, "blossoms")
    lay_blossoms(vine, VINE_BLOSSOMS, VINE_BLOSSOM_PETALS, vine_blossom_radius,
                 (6.0, 2.2, 13.0), 2000, "vine blossoms")

    b.family = "skirts"
    for k in range(SKIRTS):
        pos, tan, out = bough[k * len(bough) // SKIRTS]
        hang = mul(out, -1.0)
        side = cross(tan, hang)
        for i in range(9):
            salt = 3000 + k * 97 + i
            a = (i - 4) * 0.30
            d = norm(add(mul(hang, math.cos(a)), mul(side, math.sin(a))))
            scale = hjit((6.0, 2.0, 15.0), salt, 0.22)
            b.emit(add(pos, mul(d, 9.0 + scale[2] * 0.5)), scale, "Jade")

    for f in range(FALLS):
        pos, tan, _o = bough[f * len(bough) // FALLS]
        r0 = mag(pos)
        n0 = mul(pos, 1.0 / r0)
        drift = norm(sub(tan, mul(n0, dot(tan, n0))))
        turn = 0.30 + 0.16 * hash01(4000 + f * 17)
        r_end = SHORE_R + 8.0
        steps = 40
        prev, land = pos, pos
        b.family = "falls"
        for i in range(1, steps + 1):
            t = i / float(steps)
            r = lerp(r0, r_end, t ** 1.4)
            ang = turn * 2.0 * math.pi * t
            d = norm(add(mul(n0, math.cos(ang)), mul(drift, math.sin(ang))))
            p = mul(d, r)
            stepv = sub(p, prev)
            if dot(stepv, stepv) > 1e-4:
                salt = 5000 + f * 211 + i
                b.emit(add(prev, mul(stepv, 0.5)),
                       hjit((4.5, 4.5, mag(stepv) * 1.08), salt, 0.14),
                       "Gold" if i > steps - 6 else "Jade")
            prev, land = p, p

        b.family = "shore patches"
        salt0 = 6000 + f * 53
        n = norm(land)
        u = norm(cross(n, RIGHT)) if dot(cross(n, UP), cross(n, UP)) < 1e-4 else norm(cross(n, UP))
        v = cross(n, u)
        for i in range(7):
            a = i * GOLDEN
            rr = 0.0 if i == 0 else 26.0 * math.sqrt(i / 6.0)
            p = add(mul(n, SHORE_R), mul(add(mul(u, math.cos(a)), mul(v, math.sin(a))), rr))
            b.emit(p, hjit((24.0, 3.0, 24.0), salt0 + i, 0.18),
                   "Gold" if hash01(salt0 + i * 5) < 0.25 else "Jade")

    b.family = "shore bands"
    for bi in range(SHORE_BANDS):
        tilt = bi * GOLDEN
        axis = norm((math.cos(tilt), 0.42, math.sin(tilt)))
        u = norm(cross(axis, FORWARD))
        v = cross(axis, u)
        n = max(8, math.floor(2.0 * math.pi * SHORE_R / SHORE_BAND_STEP + 0.5))
        for i in range(n):
            salt = 7000 + bi * 977 + i
            if hash01(salt * 11) < 0.20:
                continue
            a = 2.0 * math.pi * i / n
            d = add(mul(u, math.cos(a)), mul(v, math.sin(a)))
            b.emit(mul(d, SHORE_R), hjit((22.0, 3.0, 26.0), salt, 0.18),
                   "Blue" if bi == 1 else "Jade")

    for c in range(CROWNS):
        idx = (c * len(bough) // CROWNS + len(bough) // (2 * CROWNS)) % len(bough)
        pos, tan, _o = bough[idx]
        r0 = mag(pos)
        n0 = mul(pos, 1.0 / r0)
        drift = norm(sub(tan, mul(n0, dot(tan, n0))))
        r_end = 900.0 + 150.0 * hash01(8000 + c * 29)
        turn = 0.10 + 0.10 * hash01(8100 + c * 31)
        steps = 20
        prev, tip_dir = pos, n0
        b.family = "crown"
        for i in range(1, steps + 1):
            t = i / float(steps)
            r = lerp(r0, r_end, t)
            ang = turn * 2.0 * math.pi * t
            d = norm(add(mul(n0, math.cos(ang)), mul(drift, math.sin(ang))))
            p = mul(d, r)
            stepv = sub(p, prev)
            salt = 8200 + c * 173 + i
            taper = lerp(1.0, 0.45, t)
            b.emit(add(prev, mul(stepv, 0.5)),
                   hjit((7.0 * taper, 7.0 * taper, mag(stepv) * 1.08), salt, 0.14), "Jade")
            prev, tip_dir = p, d

        b.family = "crown tufts"
        tu = (norm(cross(tip_dir, RIGHT)) if dot(cross(tip_dir, UP), cross(tip_dir, UP)) < 1e-4
              else norm(cross(tip_dir, UP)))
        tv = cross(tip_dir, tu)
        for i in range(14):
            salt = 8400 + c * 191 + i
            a = i * GOLDEN
            outward = norm(add(add(mul(tu, math.cos(a)), mul(tv, math.sin(a))), mul(tip_dir, 0.45)))
            scale = hjit((7.0, 2.4, 17.0), salt, 0.2)
            b.emit(add(prev, mul(outward, 10.0 + scale[2] * 0.5)), scale,
                   "Gold" if hash01(salt * 7) < 0.22 else "Jade")

    b.family = "terraces"
    for k in range(TERRACES):
        idx = (k * len(bough) // TERRACES + len(bough) // (3 * TERRACES)) % len(bough)
        pos, tan, out = bough[idx]
        n, u = out, tan
        v = cross(n, u)
        c = add(pos, mul(n, 14.0))
        ring, radius = [6, 12, 18, 24], [13.0, 26.0, 39.0, 51.0]
        for r in range(4):
            for i in range(ring[r]):
                salt = 9000 + k * 331 + r * 37 + i
                a = 2.0 * math.pi * i / ring[r] + r * 0.4
                outward = add(mul(u, math.cos(a)), mul(v, math.sin(a)))
                b.emit(add(c, mul(outward, radius[r])), hjit((16.0, 3.5, 15.0), salt, 0.16),
                       "Gold" if hash01(salt * 3) < 0.3 else "Blue")
        for m in range(3):
            a = m * 2.0944 + 0.6
            outward = add(mul(u, math.cos(a)), mul(v, math.sin(a)))
            base = add(c, mul(outward, 34.0))
            for i in range(9):
                salt = 9500 + k * 419 + m * 53 + i
                h = 9.0 + i * 13.0
                taper = lerp(1.0, 0.5, i / 8.0)
                b.emit(add(base, mul(n, h)), hjit((5.5 * taper, 5.5 * taper, 13.0), salt, 0.12), "Blue")
        b.emit(add(c, mul(n, 6.0)), (11.0, 11.0, 11.0), "Gold", "SuperShielded")

    b.family = "motes"
    for i in range(MOTES):
        z = 1.0 - 2.0 * (i + 0.5) / MOTES
        rho = math.sqrt(max(0.0, 1.0 - z * z))
        a = i * GOLDEN
        d = (rho * math.cos(a), z, rho * math.sin(a))
        u = hash01(11000 + i * 61)
        r = lerp(860.0 ** 3, 1150.0 ** 3, u) ** (1.0 / 3.0)
        salt = 11500 + i * 71
        b.emit(mul(d, r), hjit((6.0, 6.0, 6.0), salt, 0.35),
               "Gold" if hash01(salt * 13) < 0.18 else "Blue")

    return b


def measure(b):
    fams = collections.OrderedDict()
    per_domain = collections.Counter()
    kinds = collections.Counter()
    near_min, far_max, worst = 1e9, 0.0, None
    for pos, s, dom, kind, fam in b.lays:
        vol = s[0] * s[1] * s[2]
        e = fams.setdefault(fam, [0, 0.0])
        e[0] += 1
        e[1] += vol
        per_domain[dom] += vol
        kinds[kind] += 1
        r = mag(pos)
        half = 0.5 * math.sqrt(s[0] ** 2 + s[1] ** 2 + s[2] ** 2)
        if r - half < near_min:
            near_min, worst = r - half, (fam, r, half)
        far_max = max(far_max, r + half)
    return dict(
        families=fams,
        count=sum(v[0] for v in fams.values()),
        volume=sum(v[1] for v in fams.values()),
        per_domain=per_domain, kinds=kinds,
        nearest=near_min, farthest=far_max, worst=worst,
        max_axis=max(max(s) for _p, s, _d, _k, _f in b.lays),
        min_axis=min(min(s) for _p, s, _d, _k, _f in b.lays),
    )


# ─────────────────────────────────────────────────────────────────────────────
# 2. The roster - what this cell GROWS on top of what it lays
# ─────────────────────────────────────────────────────────────────────────────
#
# Four flora and three fauna, each picked for silhouette at the menu camera's distance rather
# than for detail. Every species uses the CANONICAL per-element assets as its ElementPalette, so
# an element keeps its own identity (leaf shape, budget, grow tempo, heart size) and this cell
# only says HOW MANY and WHERE - the split FloraConfigurationSO's cell-level overrides exist for.

Flora = collections.namedtuple(
    "Flora", "name prefab_guid palette floor cap budgets band_min band_max")
Fauna = collections.namedtuple("Fauna", "name prefab_guid prefab_fid palette floor cap")

FLORA_COMPONENT_FID = 7514956980722975813   # PhyllotacticFlora, shared by the four species

FLORA = [
    # Budgets are MaxTotalSpawnedObjects per element (Charge, Mass, Space, Time), read off the
    # shipped Lifeforms assets. The cap-worst-case below takes the MAX: a roll can hand every
    # plant the heaviest element, and a ladder authored against the mean is a ladder that
    # occasionally freezes the garden.
    Flora("Arbor",   "38c135e3fee4b466a5ec7e946ee3415d",
          ["8142092b395852d6b70857945eddf051", "789fa0009f601288c6868e7d32c734f9",
           "e800b105afc6ed81fbbe4da2fc221c5d", "636374a00e41c98762163631b2e60477"],
          3, 5, (221, 312, 182, 260), 0.36, 0.62),
    Flora("Tendril", "6a74ca1c30e05f9e311977a264ec5a55",
          ["5b64944b81691f465735bd0a969e4d3a", "a3180b4a6df35fe482db094cd9418b6b",
           "c408a1b8caed658f2d12c55d7bb235a7", "000b83422a748f6434eaeeecc2be7400"],
          4, 6, (102, 144, 84, 120), 0.40, 0.70),
    Flora("Lantern", "02d6411760b77441c06bd767aae879cf",
          ["5f677807a75e4c3dd300c74c2c08845c", "f9c6de2b9c6c7bac6b64b1ae759b74c1",
           "6a2756de008a569c5f02ba30dff53835", "f3d94c9041756fb024d75b4adce9dc66"],
          4, 7, (60, 84, 49, 70), 0.36, 0.56),
    Flora("Spire",   "becb04107104ecb6768ae2d6766e681e",
          ["25e619766581c4807de09c87fddf877c", "10859a22ceb9349219eabe30541f5341",
           "f46966ea66e278b0ad293ccecc672c1d", "6771e101fe9fa81ae0161ecbffc4c901"],
          2, 3, (144, 204, 119, 170), 0.45, 0.78),
]

FAUNA = [
    # Brittlestar and QuadFish are herbivores (diet 0), the shark is the predator (diet 1) - the
    # whole food web this cell needs, at sixteen animals. Note the component fileID differs
    # between the two fauna prefab families; it is copied per species from the shipped asset,
    # because a wrong one resolves to no component at all and spawns nothing, silently.
    Fauna("Brittlestar", "c719f00ea7596c24185379994f7dc824", 5351160486092638538,
          ["503de8d514bf4001a067b76f07c246c5", "26691ece54c94157aba5b832451ec2a2",
           "eb3b0459459a4ee0b1212c181ce80a11", "135c28565d034815adaadd2e66233711"], 3, 6),
    Fauna("QuadFish", "19615ed0c903b1041973d70593d4b0a3", 4652232322436628206,
          ["4053ff006892420d8ca5efa51365570c", "5697aa8685514f2ca9b9de9638fce1a1",
           "3107bcc776d54a74b110b883f10fba61", "414bce89d4dc495f87bbccfb02e5b847"], 4, 8),
    Fauna("Shark", "a67ba7ddaecf6624ab37cd9f5f2210a6", 5351160486092638538,
          ["58835b82ea284255855af2649ef185a5", "a690f25bf21e486ba0e500563b90f1ea",
           "eaf56c14345740849f35fc84467059e9", "78ce842bb8554d748af1e96abf430137"], 1, 2),
]

CALIBRATION = {
    # ESTIMATED, and the only estimate in this file. PhyllotacticFlora sizes prisms by ROLE:
    #   stem = (LeafSize.xy x stemScale.xy x depthTaper^d,  segment x stemScale.z)
    #   leaf = (LeafSize.x x leafScale.x x taper, LeafSize.y x leafScale.y x taper, reach x leafScale.z)
    # Evaluated for Arbor's Mass element at 0.6 x maxDepth (branching weights the population
    # toward the deeper nodes) and mixed 38% stem / 62% leaf gives ~52; x E[j^3] = 1.04 for the
    # prefab's 0.2 prism jitter gives ~54. 80 is carried instead - deliberately HIGH, because an
    # overstated forest volume makes Frenzy arrive LATER, which is the safe direction
    # (Docs/ECOSYSTEM.md §27.4), and because these four species' leaves differ.
    #
    # E[k^3] for k ~ U(1-a, 1+a) is ((1+a)^4 - (1-a)^4) / (8a) = 1.04 at a = 0.2. The 8a is
    # load-bearing: ribcage_budget.py used 4a, got 2.08, and every volume threshold derived from
    # it described a cell twice as heavy as the one that exists (see CLAUDE.md, Cleave).
    "flora_volume_per_prism": 80.0,
    # A creature's body is ~4 prisms (Wildlife Liberation measured 4,155 body prisms over a
    # 1,198 cap). SKELETONS persist after a death and are grazed rather than despawned
    # (Docs/ECOSYSTEM.md §26.7), so the standing allowance is several times the live bodies.
    "fauna_prisms_each": 4,
    "fauna_skeleton_allowance": 200,
    "fauna_volume_per_prism": 16.0,
}

# The trail band: how much VESSEL trail the ladder leaves above the mature cell before the phase
# moves. These are the Blob deltas every freestyle cell authors (Docs/ECOSYSTEM.md §18), kept so
# a pilot has to fly the same amount here as in the other seven worlds to move the phase.
TRAIL_BAND = dict(restless_hyst_count=200, restless_hyst_volume=3200,
                  frenzy_count=3600, frenzy_exit_count=600,
                  frenzy_volume=57600, frenzy_exit_volume=9600)

# Where RESTLESS sits: the fauna must start hunting while the garden is still filling in, or the
# food web is dormant for the whole of the cell's growth and the equilibrium never starts
# breathing (the /ecology skill §7: "put Restless somewhere the fauna start hunting a
# partly-grown cell"). A third of the way up the planting budget.
RESTLESS_AT_FLORA_FRACTION = 0.35

# SpawnablePrism.prefab's serialized PrismScaleAnimator window - read, not assumed.
PRISM_MIN_AXIS, PRISM_MAX_AXIS = 0.5, 100.0


def roster_model(env):
    flora_cap_count = sum(f.cap * max(f.budgets) for f in FLORA)
    flora_floor_count = sum(f.floor * max(f.budgets) for f in FLORA)
    flora_cap_vol = flora_cap_count * CALIBRATION["flora_volume_per_prism"]

    fauna_cap_plants = sum(f.cap for f in FAUNA)
    fauna_count = (fauna_cap_plants * CALIBRATION["fauna_prisms_each"]
                   + CALIBRATION["fauna_skeleton_allowance"])
    fauna_vol = fauna_count * CALIBRATION["fauna_volume_per_prism"]

    mature_count = env["count"] + flora_cap_count + fauna_count
    mature_vol = env["volume"] + flora_cap_vol + fauna_vol

    restless_count = env["count"] + int(flora_cap_count * RESTLESS_AT_FLORA_FRACTION)
    restless_vol = env["volume"] + flora_cap_vol * RESTLESS_AT_FLORA_FRACTION

    t = TRAIL_BAND
    ladder = dict(
        RestlessEnter=restless_count,
        RestlessExit=restless_count - t["restless_hyst_count"],
        FrenzyEnter=mature_count + t["frenzy_count"],
        FrenzyExit=mature_count + t["frenzy_count"] - t["frenzy_exit_count"],
        RestlessEnterVolume=round(restless_vol),
        RestlessExitVolume=round(restless_vol) - t["restless_hyst_volume"],
        FrenzyEnterVolume=round(mature_vol) + t["frenzy_volume"],
        FrenzyExitVolume=round(mature_vol) + t["frenzy_volume"] - t["frenzy_exit_volume"],
    )
    return dict(ladder=ladder, flora_cap_count=flora_cap_count,
                flora_floor_count=flora_floor_count, flora_cap_vol=flora_cap_vol,
                fauna_count=fauna_count, fauna_vol=fauna_vol,
                mature_count=mature_count, mature_vol=mature_vol,
                heart_colliders=sum(f.cap for f in FLORA) + fauna_cap_plants)


# ─────────────────────────────────────────────────────────────────────────────
# 3. Invariants
# ─────────────────────────────────────────────────────────────────────────────

def check_invariants(env, roster, problems):
    def need(ok, msg):
        if not ok:
            problems.append(msg)

    # The one that has shipped broken before (Caldera laid 89% of its mass inside the nucleus):
    # asserted on each prism's NEAREST CORNER, not its lay point.
    need(env["nearest"] >= NUCLEUS_R + NUCLEUS_MARGIN,
         f"a prism reaches {env['nearest']:.1f} from the centre - inside NucleusR {NUCLEUS_R} "
         f"+ margin {NUCLEUS_MARGIN} (worst: {env['worst']}). The nucleus interior is the "
         f"territorial claim; laying in it pre-awards node control (Docs/ECOSYSTEM.md §13).")

    need(env["farthest"] <= MEMBRANE_R,
         f"a prism reaches {env['farthest']:.1f}, outside the membrane {MEMBRANE_R} - "
         f"Cell.ContainsPosition rejects it and neither the volume ladder nor the fauna "
         f"density grids can see it.")

    # The ORDERING the composition is built on, asserted rather than the values (the /ecology
    # skill's rule for absolute-distance tolerances).
    need(BOUGH_MAJOR - BOUGH_MINOR >= NUCLEUS_R + BLOSSOM_RADIUS + NUCLEUS_MARGIN,
         "the bough's inner radius does not clear a blossom sitting at its closest approach.")
    need(VINE_MAJOR - VINE_MINOR >= NUCLEUS_R + VINE_BLOSSOM_RADIUS + NUCLEUS_MARGIN,
         "the vine's inner radius does not clear a vine blossom at its closest approach.")
    need(VINE_MAJOR + VINE_MINOR <= BOUGH_MAJOR + BOUGH_MINOR,
         "the vine is not inside the bough's envelope - it stops being the inner runner.")
    need(BOUGH_MAJOR - BOUGH_MINOR < CAM_R < BOUGH_MAJOR + BOUGH_MINOR,
         f"the camera orbit {CAM_R} is no longer inside the bough band - the bough stops being "
         f"the subject and becomes a shell the camera looks at from outside.")

    # Collider budget. Shielded and super-shielded prisms carry ALWAYS-ON convex MeshColliders;
    # everything else rides the phase-LOD BoxCollider. Yggdra ships 225; this cell is a menu
    # world and holds itself to a third of that.
    always_on = env["kinds"].get("Shielded", 0) + env["kinds"].get("SuperShielded", 0)
    need(always_on <= 80, f"{always_on} always-on MeshCollider prisms - over this cell's 80 budget.")

    # Nothing may state a size outside the shared prism prefab's serialized scale window, because
    # PrismScaleAnimator clamps per axis INSIDE its setter with no log and no return value
    # (Docs/ECOSYSTEM.md §34.9) - an over-range axis is not an error, it is a different arena
    # that looks authored. This generator deliberately does NOT set AdmitsAuthoredPrismScale, so
    # SpawnablePrism.prefab's own window is what binds and every size here has to fit inside it.
    need(PRISM_MIN_AXIS <= env["min_axis"] and env["max_axis"] <= PRISM_MAX_AXIS,
         f"a prism axis lands at {env['min_axis']:.2f}..{env['max_axis']:.2f}, outside "
         f"SpawnablePrism.prefab's [{PRISM_MIN_AXIS}, {PRISM_MAX_AXIS}] scale window - "
         f"PrismScaleAnimator would silently clamp it.")

    # The volume ladder must leave the mature cell room to exist.
    L = roster["ladder"]
    need(L["FrenzyEnterVolume"] > roster["mature_vol"],
         "FrenzyEnterVolume sits at or below the mature cell - planting and growth would freeze "
         "with the garden still bare (Docs/ECOSYSTEM.md §5.1).")
    need(L["FrenzyEnter"] > roster["mature_count"],
         "the Frenzy COUNT backstop sits at or below the mature cell's prism count.")
    need(L["RestlessEnterVolume"] < L["FrenzyEnterVolume"], "Restless is not below Frenzy.")
    need(L["RestlessExitVolume"] < L["RestlessEnterVolume"], "Restless has no hysteresis band.")
    need(L["FrenzyExitVolume"] < L["FrenzyEnterVolume"], "Frenzy has no hysteresis band.")
    need(L["RestlessEnterVolume"] > env["volume"],
         "Restless fires at or below the bare baseline - the cell would boot already hunting.")


def check_source_constants(problems):
    """Every constant the mirror holds is read back out of the C# - a mirror that can drift from
    its source is an estimate wearing a measurement's clothes."""
    if not os.path.exists(SOURCE):
        problems.append(f"missing {os.path.relpath(SOURCE, REPO)}")
        return
    src = open(SOURCE, encoding="utf-8").read()
    for name, want in MIRRORED_CONSTS.items():
        m = re.search(r"\b%s\s*=\s*(-?[\d.]+)f?" % re.escape(name), src)
        if not m:
            problems.append(f"constant {name} not found in SpawnableGarland.cs")
            continue
        got = float(m.group(1))
        if abs(got - float(want)) > 1e-4:
            problems.append(f"{name}: C# says {got}, the model says {want}")
    if "AdmitsAuthoredPrismScale" in src:
        problems.append("SpawnableGarland opts into AdmitsAuthoredPrismScale - the prism scale "
                        "window no longer binds, so the max-axis check below is vacuous.")


# ── The harness cross-check: this model vs. what the SHIPPED generator actually emits ──
#
# Tools/Build/garland_harness/run.sh compiles SpawnableGarland.cs against a Unity shim and RUNS
# it. Everything below is asserted against that run, so the two hand-written implementations
# cannot drift apart silently and the ladder cannot ship on a number the engine does not produce.
#
# TOLERANCES. Counts, prism KINDS and per-domain domains are asserted EXACT - they are integers
# and enum members, and a difference there is a transliteration bug, not arithmetic. The float
# quantities carry a tolerance because the engine is float32 and this model is float64: measured,
# the worst positional disagreement is 0.055u on a crown tuft at radius ~1050 (5e-5 relative),
# which is dominated by `Mathf.PI` and `GoldenAngle` being float32 literals multiplied by angles
# up to ~200 radians. 0.25u is that, with room, and is four hundred times smaller than the 17.8u
# nucleus margin it has to protect - so no float difference can turn a passing clearance into a
# failing one or vice versa.
RADIUS_TOLERANCE = 0.25
VOLUME_TOLERANCE = 1e-6

# The shim's transcriptions of PROJECT signatures. A transcription is evidence about the shipped
# code only if something pins it to its source, so each is asserted VERBATIM in the file named.
SHIM_CLAIMS = [
    ("Assets/_Scripts/Controller/Environment/Spawning/CellEnvironmentSpawnableBase.cs", [
        "protected const float GoldenAngle = 2.39996323f;",
        "protected abstract int DefaultSeed { get; }",
        "protected virtual int LayCapacity => 40000;",
        "protected abstract void BuildEnvironment();",
        "protected virtual bool AdmitsAuthoredPrismScale => false;",
        "protected abstract int BuildParameterHash();",
        "protected void Emit(Vector3 pos, Quaternion rot, Vector3 scale, Domains dom, PrismKind kind = PrismKind.Plain)",
        "public abstract class CellEnvironmentSpawnableBase : SpawnableBase",
        # Hash01's BODY, not just its signature: the harness RUNS, and every wobble in the
        # generator is a hash of the emitting index.
        "h = (h ^ 61u) ^ (h >> 16);",
        "h *= 0x27d4eb2du;",
    ]),
    ("Assets/_Scripts/Controller/Environment/Spawning/SpawnPoint.cs", [
        "public static Quaternion LookRotation(Vector3 forward, Vector3 up)",
    ]),
]


def check_shim_fidelity(problems):
    for rel, claims in SHIM_CLAIMS:
        path = os.path.join(REPO, rel)
        if not os.path.exists(path):
            problems.append(f"the harness shim transcribes {rel}, which is missing")
            continue
        src = open(path, encoding="utf-8").read()
        for claim in claims:
            if claim not in src:
                problems.append(f"harness shim claims `{claim.strip()}` from {rel} - not verbatim "
                                f"there any more, so the harness is measuring a different class.")


def check_against_harness(env, problems):
    if not os.path.exists(MEASURED):
        problems.append("no Tools/Build/garland_measurements.json - run "
                        "`bash Tools/Build/garland_harness/run.sh` (needs a dotnet 8 SDK). The "
                        "ladder below is derived from this file's model and is UNVERIFIED "
                        "against the shipped generator until that runs.")
        return
    doc = json.load(open(MEASURED, encoding="utf-8"))

    for rel, want in doc.get("sources", {}).items():
        path = os.path.join(REPO, rel)
        if not os.path.exists(path):
            problems.append(f"the measurement hashes {rel}, which is gone")
        elif hashlib.sha256(open(path, "rb").read()).hexdigest() != want:
            problems.append(f"{rel} has changed since the measurement was taken - re-run "
                            f"Tools/Build/garland_harness/run.sh.")

    def same(label, got, want, tol, rel=False):
        d = abs(got - want)
        if (d / max(1e-9, abs(want)) if rel else d) > tol:
            problems.append(f"the model and the shipped generator disagree on {label}: "
                            f"model {got}, engine {want}.")

    same("prism count", env["count"], doc["count"], 0)
    same("total volume", env["volume"], doc["volume"], VOLUME_TOLERANCE, rel=True)
    same("nearest prism corner", env["nearest"], doc["nearest"], RADIUS_TOLERANCE)
    same("farthest prism corner", env["farthest"], doc["farthest"], RADIUS_TOLERANCE)
    same("smallest prism axis", env["min_axis"], doc["min_axis"], RADIUS_TOLERANCE)
    same("largest prism axis", env["max_axis"], doc["max_axis"], RADIUS_TOLERANCE)
    if dict(env["kinds"]) != doc["kinds"]:
        problems.append(f"prism KINDS differ: model {dict(env['kinds'])}, engine {doc['kinds']}.")
    for dom, vol in doc["per_domain"].items():
        same(f"{dom} volume", env["per_domain"].get(dom, 0.0), vol, VOLUME_TOLERANCE, rel=True)
    if set(env["per_domain"]) != set(doc["per_domain"]):
        problems.append("the model and the engine lay different DOMAINS.")


# ── Every serialized key this script writes must be DECLARED by the class that reads it ──
#
# A key a type no longer declares is not an error in Unity: it deserializes to nothing, the asset
# still loads, and the cell quietly runs on the field's default. That is the same shape as the 40
# SO_ArcadeGame assets still carrying a retired `PreviewClip` (Docs/LAUNCH_BLOCKER_INDEX.md E2) -
# so the templates below are checked against the live C# rather than assumed to have kept up.
SERIALIZED_OWNERS = {
    "Garland Cell Config.asset": ["CellConfigDataSO.cs", "CellPhaseThresholds.cs"],
    "Garland Cell Spawn Profile.asset": ["SpawnProfileSO.cs"],
    "Garland %s Flora Config Data.asset": ["FloraConfigurationSO.cs"],
    "Garland %s Fauna Config Data.asset": ["FaunaConfigurationSO.cs"],
}
UNITY_KEYS = {"m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
              "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script",
              "m_Name", "m_EditorClassIdentifier"}


def _declared_fields(basenames, problems):
    import glob
    known = set()
    for base in basenames:
        hits = glob.glob(os.path.join(REPO, "Assets", "**", base), recursive=True)
        if not hits:
            problems.append(f"cannot find {base} to validate serialized keys against")
            continue
        src = open(hits[0], encoding="utf-8").read()
        known |= set(re.findall(r"public\s+(?:readonly\s+)?[\w<>,\[\]\.\? ]+?\s+(\w+)\s*(?:=|;)", src))
    return known


def check_serialized_keys(files, problems):
    for path, text in files.items():
        if not path.endswith(".asset"):
            continue
        name = os.path.basename(path)
        owners = None
        for pattern, o in SERIALIZED_OWNERS.items():
            if "%s" in pattern:
                head, tail = pattern.split("%s")
                if name.startswith(head) and name.endswith(tail):
                    owners = o
            elif name == pattern:
                owners = o
        if owners is None:
            problems.append(f"{name}: no class recorded to validate its serialized keys against")
            continue
        known = _declared_fields(owners, problems)
        for line in text.splitlines():
            m = re.match(r"^\s{2,4}(\w+):", line)
            if m and m.group(1) not in UNITY_KEYS and m.group(1) not in known:
                problems.append(f"{name} writes `{m.group(1)}`, which {'/'.join(owners)} "
                                f"does not declare - it would deserialize to nothing.")


def self_test():
    """Negative control: the nucleus assertion is the one that has shipped broken before, so
    prove it FIRES. A check nobody has watched fail is a check nobody should trust."""
    env = measure(build_environment(vine_blossom_radius=VINE_BLOSSOM_RADIUS + 90.0))
    problems = []
    check_invariants(env, roster_model(env), problems)
    hit = [p for p in problems if "inside NucleusR" in p]
    if not hit:
        print("SELF-TEST FAILED: widening the vine blossoms by 90u did not trip the nucleus "
              "clearance assertion.")
        return 1
    print("self-test OK - nucleus clearance assertion fires on a deliberately over-wide blossom:")
    print("   ", hit[0].split(" (worst")[0])
    return 0


# ─────────────────────────────────────────────────────────────────────────────
# 4. Emitting the assets
# ─────────────────────────────────────────────────────────────────────────────
#
# Guids are FIXED here rather than generated per run: an asset's guid is its identity, so a
# re-run that minted fresh ones would dangle every reference the scene and the configs hold.

GUID = {
    "script":            "d7c41a9e5b3f4e0a8c62f19d4a73b508",   # SpawnableGarland.cs
    "prefab":            "b21f6c8d4e9a47d3ab05e7c2f8143d6b",
    "config":            "3e8a0d5c71b24f96b8e3a4d70c519f2a",
    "profile":           "9c74b2e03fa8425db61ef50a8d3c76e1",
    "Arbor":             "1f5d38a2c604473e9ab7d20e68f4c913",
    "Tendril":           "4a90e6b7d2f34c18ae53b901c7d62e84",
    "Lantern":           "7b23c9f05d8e4a61b04fe8237a95d1c6",
    "Spire":             "c6e14b83a70d495f82d9c1e64b307af5",
    "Brittlestar":       "2d87f41069b34c0ea5931cd7f28e6b40",
    "QuadFish":          "8e05a72cb94f41d6970b3e8a52d1c7f9",
    "Shark":             "5a3bd08e17c6429fb2604ae9d81f3572",
}

# Shared, already-shipped references this cell composes from - the Blob-family membrane, nucleus,
# cytoplasm and modifiers, so Garland is a sibling of the freestyle seven rather than a fork.
REF = dict(
    membrane=(346633111830028674, "6e330f85972faf843b8a128e7166f7b5"),
    nucleus=(7555898194514117247, "b9cf1833fa2493d4b8724ccb6740fb3a"),
    cytoplasm=(639495419069806261, "9cacd903fcf4643459f5f14ac811bb20"),
    modifier=(8058406376250941529, "daa37ae0e7af4b04383c1c4e6e76817d"),
    prism=(4563009547826722997, "ed9defc56162b4b4588e61c20984b6d9"),
    # Shared with Yggdra and Ourobor. A Garland-specific card image is an ART task, and a
    # placeholder that looks authored is worse than one that is visibly borrowed.
    icon=(21300000, "6aa1c06e11b265744a5f9fa8858ac72a"),
)

SCRIPT_GUID = dict(
    cell_config="01f934d50526431a9392a6ceca1dc33d",
    spawn_profile="e8d8aa5d835249798a256e18f2f7d912",
    flora_config="a32a297a7606432885f4d3e1f83bea9a",
    fauna_config="c778cfbe4dfc4c5c8401e40c17802311",
)

def head(script_guid, name):
    """A ScriptableObject asset's preamble. Built by concatenation rather than %-formatting
    because the YAML directives are themselves percent signs."""
    return ("%YAML 1.1\n"
            "%TAG !u! tag:unity3d.com,2011:\n"
            "--- !u!114 &11400000\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            f"  m_Name: {name}\n"
            "  m_EditorClassIdentifier: \n")


def meta(guid, kind="asset"):
    if kind == "script":
        return f"fileFormatVersion: 2\nguid: {guid}\n"
    if kind == "prefab":
        return (f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n"
                "  externalObjects: {}\n  userData: \n  assetBundleName: \n"
                "  assetBundleVariant: \n")
    return (f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n")


def palette_lines(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in guids)


def flora_asset(f):
    name = f"Garland {f.name} Flora Config Data"
    return head(SCRIPT_GUID["flora_config"], name) + f"""  FloraPrefab: {{fileID: {FLORA_COMPONENT_FID}, guid: {f.prefab_guid}, type: 3}}
  NetworkSynced: 0
  SpawnProbability: 1
  InitialSpawnCount: {f.floor}
  OverrideDefaultPlantPeriod: 0
  NewPlantPeriod: 9999999
  PopulationSize: {f.floor}
  MaxLivePopulation: {f.cap}
  GrowthPerOffspring: {int(round(max(f.budgets) * 0.35))}
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 5
  MaturityFraction: 0.5
  OffspringSpread: 90
  PreferredSites: 0
  Element: 0
  Variant:
    Enabled: 0
  SpreadElements: 1
  ElementPalette:
{palette_lines(f.palette)}  PlantRadiusCellFractionMaxOverride: {f.band_max}
  PlantRadiusCellFractionMinOverride: {f.band_min}
  MaxTotalSpawnedObjectsOverride: -1
"""


def fauna_asset(f):
    name = f"Garland {f.name} Fauna Config Data"
    return head(SCRIPT_GUID["fauna_config"], name) + f"""  FaunaPrefab: {{fileID: {f.prefab_fid}, guid: {f.prefab_guid}, type: 3}}
  InitialSpawnCount: {f.floor}
  PopulationSize: {f.floor}
  SpawnProbability: 1
  NetworkSynced: 0
  FeedsPerOffspring: 3
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 20
  MaxLivePopulation: {f.cap}
  ReleaseTier: 0
  BandInnerRadius: 0
  BandOuterRadius: 0
  CenterFocusBias: 0
  Element: 0
  Variant:
    Enabled: 0
  SpreadElements: 1
  ElementPalette:
{palette_lines(f.palette)}"""


def profile_asset():
    floras = "".join(f"  - {{fileID: 11400000, guid: {GUID[f.name]}, type: 2}}\n" for f in FLORA)
    faunas = "".join(f"  - {{fileID: 11400000, guid: {GUID[f.name]}, type: 2}}\n" for f in FAUNA)
    return head(SCRIPT_GUID["spawn_profile"], "Garland Cell Spawn Profile") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 12000
  FloraInitialDelaySeconds: 3
  FloraSpawnIntervalSeconds: 0.5
  FloraPopulationScale: 1
  FloraPlantBudgetScale: 1
  FloraPrismScale: 1
  SupportedFloras:
{floras}  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 20
  InitialFaunaReleaseTier: 2147483647
  FaunaSpawnVolumeThreshold: 1
  FaunaPopulationScale: 1
  BaseFaunaSpawnTime: 30
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 2000
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0
  HerbivoreSpawnPointCount: 3
  HerbivoreSpawnRadius: 560
  PredatorSpawnPointCount: 2
  PredatorSpawnRadius: 760
  SupportedFaunas:
{faunas}"""


def config_asset(ladder):
    mf, mg = REF["membrane"]
    nf, ng = REF["nucleus"]
    cf, cg = REF["cytoplasm"]
    kf, kg = REF["modifier"]
    inf, ing = REF["icon"]
    return head(SCRIPT_GUID["cell_config"], "Garland Cell Config") + f"""  CellName: Garland
  Description: A flowering bough wound twice around the seed of the world
  Icon: {{fileID: {inf}, guid: {ing}, type: 3}}
  Difficulty: 1
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {mf}, guid: {mg}, type: 3}}
  NucleusPrefab: {{fileID: {nf}, guid: {ng}, type: 3}}
  CytoplasmPrefab: {{fileID: {cf}, guid: {cg}, type: 3}}
  CellModifiers:
  - {{fileID: {kf}, guid: {kg}, type: 3}}
  SpawnProfile: {{fileID: 11400000, guid: {GUID['profile']}, type: 2}}
  EnvironmentPrefab: {{fileID: 5260000000000103, guid: {GUID['prefab']}, type: 3}}
  EnvironmentIntensity: 1
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {ladder['RestlessEnter']}
    RestlessExit: {ladder['RestlessExit']}
    FrenzyEnter: {ladder['FrenzyEnter']}
    FrenzyExit: {ladder['FrenzyExit']}
    RestlessEnterVolume: {ladder['RestlessEnterVolume']}
    RestlessExitVolume: {ladder['RestlessExitVolume']}
    FrenzyEnterVolume: {ladder['FrenzyEnterVolume']}
    FrenzyExitVolume: {ladder['FrenzyExitVolume']}
"""


def prefab_asset():
    pf, pg = REF["prism"]
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5260000000000101
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5260000000000102}}
  - component: {{fileID: 5260000000000103}}
  m_Layer: 0
  m_Name: SpawnableGarland
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5260000000000102
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000101}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5260000000000103
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000101}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID['script']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  seed: 73
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: 1
  prism: {{fileID: {pf}, guid: {pg}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
"""


def planned_files(ladder):
    files = {
        SOURCE + ".meta": meta(GUID["script"], "script"),
        PREFAB: prefab_asset(),
        PREFAB + ".meta": meta(GUID["prefab"], "prefab"),
        os.path.join(CELL_DIR, "Garland Cell Config.asset"): config_asset(ladder),
        os.path.join(CELL_DIR, "Garland Cell Config.asset.meta"): meta(GUID["config"]),
        os.path.join(CELL_DIR, "Garland Cell Spawn Profile.asset"): profile_asset(),
        os.path.join(CELL_DIR, "Garland Cell Spawn Profile.asset.meta"): meta(GUID["profile"]),
    }
    for f in FLORA:
        base = os.path.join(CELL_DIR, f"Garland {f.name} Flora Config Data.asset")
        files[base] = flora_asset(f)
        files[base + ".meta"] = meta(GUID[f.name])
    for f in FAUNA:
        base = os.path.join(CELL_DIR, f"Garland {f.name} Fauna Config Data.asset")
        files[base] = fauna_asset(f)
        files[base + ".meta"] = meta(GUID[f.name])
    return files


# ─────────────────────────────────────────────────────────────────────────────
# 5. Report + entry point
# ─────────────────────────────────────────────────────────────────────────────

def report(env, roster):
    print("\n═══ GARLAND ─ the environment as laid ═══")
    print(f"{'family':<16}{'count':>8}{'share':>8}{'volume':>14}{'vol/prism':>11}")
    for fam, (c, v) in env["families"].items():
        print(f"{fam:<16}{c:>8}{100*c/env['count']:>7.1f}%{v:>14,.0f}{v/c:>11,.0f}")
    print(f"{'TOTAL':<16}{env['count']:>8}{100.0:>7.1f}%{env['volume']:>14,.0f}"
          f"{env['volume']/env['count']:>11,.0f}")
    print(f"\nalways-on MeshCollider prisms : {env['kinds'].get('SuperShielded', 0)} "
          f"super-shielded, {env['kinds'].get('Shielded', 0)} shielded  (budget 80)")
    print(f"danger prisms                 : {env['kinds'].get('Danger', 0)}  "
          f"(zero by design - the autopilot flies this world)")
    print(f"prism axis range              : {env['min_axis']:.2f} .. {env['max_axis']:.2f}  "
          f"(prefab window {PRISM_MIN_AXIS} .. {PRISM_MAX_AXIS})")
    print(f"nearest prism corner          : {env['nearest']:.1f}  "
          f"(nucleus {NUCLEUS_R:.0f} + margin {NUCLEUS_MARGIN:.0f} = {NUCLEUS_R+NUCLEUS_MARGIN:.0f})")
    print(f"farthest prism corner         : {env['farthest']:.1f}  (membrane {MEMBRANE_R:.0f})")
    print("per-domain volume             : " + ", ".join(
        f"{d} {v:,.0f}" for d, v in env["per_domain"].most_common()))

    print("\n═══ what it GROWS ═══")
    print(f"{'species':<14}{'floor':>7}{'cap':>6}{'budget/plant':>14}{'prisms at cap':>15}")
    for f in FLORA:
        print(f"{f.name:<14}{f.floor:>7}{f.cap:>6}{max(f.budgets):>14}{f.cap*max(f.budgets):>15}")
    for f in FAUNA:
        print(f"{f.name:<14}{f.floor:>7}{f.cap:>6}{CALIBRATION['fauna_prisms_each']:>14}"
              f"{f.cap*CALIBRATION['fauna_prisms_each']:>15}")
    print(f"\nflora at cap   : {roster['flora_cap_count']:,} prisms "
          f"(~{roster['flora_cap_vol']:,.0f} volume, ESTIMATED - see CALIBRATION)")
    print(f"fauna + bones  : {roster['fauna_count']:,} prisms (~{roster['fauna_vol']:,.0f} volume)")
    print(f"MATURE CELL    : {roster['mature_count']:,} prisms, ~{roster['mature_vol']:,.0f} volume")
    print(f"always-on heart colliders at cap: {roster['heart_colliders']} "
          f"(one per live lifeform - Blob 171, Lattice 1,080)")

    L = roster["ladder"]
    print("\n═══ the ladder ═══")
    print(f"  Restless  volume {L['RestlessEnterVolume']:>12,} / exit {L['RestlessExitVolume']:>12,}"
          f"   count {L['RestlessEnter']:>7,} / {L['RestlessExit']:>7,}")
    print(f"  Frenzy    volume {L['FrenzyEnterVolume']:>12,} / exit {L['FrenzyExitVolume']:>12,}"
          f"   count {L['FrenzyEnter']:>7,} / {L['FrenzyExit']:>7,}")
    print("  (Restless is VOLUME-only - CellPhaseRules.Compute never reads the Restless counts;\n"
          "   the count pair is authored for consistency and for the zero-volume derivation path.)")


def main(argv):
    if "--self-test" in argv:
        return self_test()

    env = measure(build_environment())
    roster = roster_model(env)
    ladder = roster["ladder"]

    problems = []
    check_source_constants(problems)
    check_shim_fidelity(problems)
    check_against_harness(env, problems)
    check_invariants(env, roster, problems)

    files = planned_files(ladder)
    check_serialized_keys(files, problems)

    report(env, roster)

    if problems:
        print("\nFAILED:")
        for p in problems:
            print("  -", p)
        return 1

    if "--report" in argv:
        print("\n--report: wrote nothing.")
        return 0

    if "--check" in argv:
        drift = []
        for path, want in files.items():
            if not os.path.exists(path):
                drift.append(f"missing {os.path.relpath(path, REPO)}")
            elif open(path, encoding="utf-8").read() != want:
                drift.append(f"drifted {os.path.relpath(path, REPO)}")
        if drift:
            print("\nFAILED - the assets on disk are not what this model authors:")
            for d in drift:
                print("  -", d)
            return 1
        print(f"\n--check OK: {len(files)} files match the model.")
        return 0

    os.makedirs(CELL_DIR, exist_ok=True)
    os.makedirs(os.path.dirname(PREFAB), exist_ok=True)
    for path, text in files.items():
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(text)
    print(f"\nwrote {len(files)} files.")
    print("Still to do by hand (both are one-line edits this script deliberately does not make,\n"
          "because they live in files it does not own):\n"
          "  1. Menu_Main.unity - add the config to the freestyle Cell's CellConfigs list.\n"
          "  2. author_flora_populations.py - OWNED_ELSEWHERE['Garland '] = this script.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
