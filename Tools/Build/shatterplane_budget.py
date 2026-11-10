#!/usr/bin/env python3
"""
Analytic prism budget and ring-yield model for Shatterplane - the Brittle Star's Space arena (GameModes.Shatterplane).

This is the model of `ShatterplanePlates.cs` (and so of `SpawnableShatterplane.cs`), and it is a MIRROR, not an
estimate (flatline_budget.py's contract): `author_shatterplane_assets.py` imports it, so the arena's PhaseThresholds,
the targets and the generator's checks come from the same numbers that build the arena.

It is exact because the C# was written to make it exact:
  * which tiles a plate has is decided by integers only (its radius in tiles and the grid indices), so the tile count
    is exact as long as every structure is PLACED - and this file reports any structure it could not place (the
    generator fails on it; the C# tests assert the same counts);
  * placement draws are `Hash01` of integer keys, ported verbatim, and the float maths runs in float32 in the C#'s order
    (each Mathf call as the double function cast to float), so the positions agree too.

The RING model (`ring_yield`) is the Brittle Star's own rule, read from its shipped config: the blast is a flat disk in
the plane of the thrown ring, as wide as the cell's nucleus, and it takes a prism when the prism's half-extent along
the disk's normal overlaps the disk (`BrittleStarRules.DiskReaches`). Nothing about the vessel is changed here; the
mode only chooses the nucleus (decision D2, Shombith 2026-11-10: "you can make it a little bigger").

    python3 Tools/Build/shatterplane_budget.py            # the census table that belongs in SHATTERPLANE.md
    python3 Tools/Build/shatterplane_budget.py --ring     # plus the ring's yield against tilt on each rung's plate
    python3 Tools/Build/shatterplane_budget.py --sites    # plus each rung's crystal sites

CONFIRM IN EDITOR: FrogletTools > Ecology > Measure Cell Environment Baselines. If the measurer disagrees with this
table, the C# and this file have drifted - fix both, do not paper over it in the asset.
"""
import math
import sys

import numpy as np

f32 = np.float32

# ── Constants: must match ShatterplanePlates.cs ───────────────────────────────
TILE = 8.0
FILL = 0.625
THICKNESS = 2.0
GAP = 40.0
MAX_ATTEMPTS = 64
CRYSTAL_OFFSET = 200.0
CRYSTAL_STEP = 120.0
KEY_SEED, KEY_STRUCTURE, KEY_ATTEMPT = 104729, 1031, 7

PLAIN, DANGER, SHIELDED, SUPER = 0, 1, 2, 3

# ── The arena (decision D2: the nucleus a little bigger than the standard ~392 u) ─
NUCLEUS_RADIUS = 470.0     # = the ring's disk radius in this cell (BrittleStarExecutor.NucleusRadiusAt)
SPAWN_RING = 780.0         # the pilots start here, facing in
SPAWN_CLEAR = 250.0        # nothing the arena lays comes within this of the spawn ring

# ── The ladder (the generator authors these onto the four prefabs) ────────────
RUNGS = [
    dict(i=1, label="Billboards", seed=8802, innerRadius=200.0, outerRadius=380.0,
         structureCount=10, plateRadius=13, platesPerStack=1, shelfGap=0.0, faceOut=1,
         ribEvery=0, dangerRim=0, screenEvery=0, screenRadius=0, screenOffset=0.0, screenRibEvery=1, crystalCount=10,
         question="Can you square up at all?",
         blurb="ten plates facing straight out at the spawn ring, a crystal on every plate's axis"),
    dict(i=2, label="Gallery", seed=8821, innerRadius=140.0, outerRadius=390.0,
         structureCount=12, plateRadius=12, platesPerStack=1, shelfGap=0.0, faceOut=0,
         ribEvery=0, dangerRim=0, screenEvery=0, screenRadius=0, screenOffset=0.0, screenRibEvery=1, crystalCount=8,
         question="Can you swing round to square up?",
         blurb="twelve plates at mixed angles, most needing a turn to face square, eight crystals on their axes"),
    dict(i=3, label="Shelves", seed=8831, innerRadius=170.0, outerRadius=380.0,
         structureCount=5, plateRadius=12, platesPerStack=3, shelfGap=70.0, faceOut=0,
         ribEvery=0, dangerRim=0, screenEvery=0, screenRadius=0, screenOffset=0.0, screenRibEvery=1, crystalCount=5,
         question="Front plate first.",
         blurb="five stacks of three parallel plates 70 u apart - a square ring peels the front one and the guns eat "
               "it from the face - five crystals on their axes"),
    dict(i=4, label="Reliquary", seed=8846, innerRadius=150.0, outerRadius=360.0,
         structureCount=8, plateRadius=13, platesPerStack=1, shelfGap=0.0, faceOut=0,
         ribEvery=6, dangerRim=1, screenEvery=2, screenRadius=7, screenOffset=60.0, screenRibEvery=3, crystalCount=4,
         question="Find the square line through the frame.",
         blurb="eight plates braced by super-shielded ribs on every sixth line with danger rims, every other one half "
               "behind a super-shielded grille, four crystals"),
]

def hash01(n):
    """CellEnvironmentSpawnableBase.Hash01 / ShatterplanePlates.Hash01, verbatim (uint32 wraparound)."""
    h = n & 0xFFFFFFFF
    h = ((h ^ 61) ^ (h >> 16)) & 0xFFFFFFFF
    h = (h * 9) & 0xFFFFFFFF
    h ^= h >> 4
    h = (h * 0x27D4EB2D) & 0xFFFFFFFF
    h ^= h >> 15
    return f32(f32(h & 0xFFFFFF) / f32(0x1000000))


def i32(x):
    """C# int arithmetic wraps at 32 bits."""
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x >= 1 << 31 else x


def v3(x, y, z):
    return np.array([x, y, z], dtype=np.float32)


def dot(a, b):
    return f32(f32(f32(a[0] * b[0]) + f32(a[1] * b[1])) + f32(a[2] * b[2]))


def cross(a, b):
    return v3(f32(a[1] * b[2]) - f32(a[2] * b[1]), f32(a[2] * b[0]) - f32(a[0] * b[2]),
              f32(a[0] * b[1]) - f32(a[1] * b[0]))


def normalized(a):
    m = f32(math.sqrt(float(dot(a, a))))
    return (a / m).astype(np.float32) if m > f32(1e-5) else v3(0, 0, 0)


def basis(n):
    t = v3(1, 0, 0) if abs(float(n[0])) < 0.9 else v3(0, 1, 0)
    u = normalized(cross(n, t))
    return u, cross(n, u)


def unit_from(h1, h2):
    z = f32(f32(2) * h1 - f32(1))
    ring = f32(math.sqrt(float(max(f32(0), f32(f32(1) - f32(z * z))))))
    phi = f32(f32(f32(2) * f32(math.pi)) * h2)
    return v3(f32(ring * f32(math.cos(float(phi)))), f32(ring * f32(math.sin(float(phi)))), z)


def half_depth(r):
    return f32(f32(max(1, r["platesPerStack"]) - 1) * f32(0.5)) * f32(r["shelfGap"])


def screen_shift(r):
    return f32(f32(r["plateRadius"] * TILE) * f32(0.5))


def bounding_radius(r, screen):
    rp = f32(f32(r["plateRadius"] + 0.5) * f32(TILE))
    hd = f32(half_depth(r) + f32(THICKNESS))
    rad = f32(math.sqrt(float(f32(f32(rp * rp) + f32(hd * hd)))))
    if screen:
        front = f32(half_depth(r) + f32(r["screenOffset"]))
        shift = screen_shift(r)
        rs = f32(f32(math.sqrt(float(f32(f32(front * front) + f32(shift * shift)))))
                 + f32(f32(r["screenRadius"] + 0.5) * f32(TILE)))
        rad = max(rad, rs)
    return f32(rad + f32(TILE))


def layout(r):
    """[dict(index, c, n, u, w, r, screen)] and the count it could not place."""
    out, missed = [], 0
    r0 = f32(f32(f32(r["innerRadius"]) * f32(r["innerRadius"])) * f32(r["innerRadius"]))
    r1 = f32(f32(f32(r["outerRadius"]) * f32(r["outerRadius"])) * f32(r["outerRadius"]))
    for k in range(r["structureCount"]):
        screen = r["screenEvery"] > 0 and k % r["screenEvery"] == r["screenEvery"] - 1
        rad = bounding_radius(r, screen)
        placed = False
        for t in range(MAX_ATTEMPTS):
            key = i32(r["seed"] * KEY_SEED + k * KEY_STRUCTURE + t * KEY_ATTEMPT)
            rr = f32(math.pow(float(f32(r0 + f32(hash01(key) * f32(r1 - r0)))), float(f32(1) / f32(3))))
            c = (unit_from(hash01(key + 1), hash01(key + 2)) * rr).astype(np.float32)
            n = normalized(c) if r["faceOut"] else unit_from(hash01(key + 3), hash01(key + 4))
            if dot(n, c) < f32(0):
                n = (-n).astype(np.float32)
            ok = True
            for o in out:
                need = f32(f32(rad + o["r"]) + f32(GAP))
                d = (o["c"] - c).astype(np.float32)
                if dot(d, d) < f32(need * need):
                    ok = False
                    break
            if not ok:
                continue
            u, w = basis(n)
            out.append(dict(index=k, c=c, n=n, u=u, w=w, r=rad, screen=screen))
            placed = True
            break
        if not placed:
            missed += 1
    return out, missed


def plate(st, centre, r, keep):
    out = []
    rad = r["plateRadius"]
    rim_inside, outside = (rad - 1) * (rad - 1), rad * rad
    for a in range(-rad, rad + 1):
        for b in range(-rad, rad + 1):
            d2 = a * a + b * b
            if d2 > outside:
                continue
            rim = d2 > rim_inside
            if r["dangerRim"] and rim:
                kind = DANGER
            elif not rim and r["ribEvery"] > 0 and (a % r["ribEvery"] == 0 or b % r["ribEvery"] == 0):
                kind = SUPER
            else:
                kind = PLAIN
            pos = None
            if keep:
                pos = (centre + (st["u"] * f32(a * TILE)).astype(np.float32)
                       + (st["w"] * f32(b * TILE)).astype(np.float32))
            out.append((st["index"], kind, pos))
    return out


def screen(st, centre, r, keep):
    out = []
    rad, e = r["screenRadius"], max(1, r["screenRibEvery"])
    for a in range(-rad, rad + 1):
        for b in range(-rad, rad + 1):
            if a * a + b * b > rad * rad:
                continue
            if a % e != 0 and b % e != 0:
                continue
            pos = None
            if keep:
                pos = (centre + (st["u"] * f32(a * TILE)).astype(np.float32)
                       + (st["w"] * f32(b * TILE)).astype(np.float32))
            out.append((st["index"], SUPER, pos))
    return out


def plate_centres(st, r):
    hd = half_depth(r)
    return [(st["c"] + (st["n"] * f32(f32(p * r["shelfGap"]) - hd)).astype(np.float32)).astype(np.float32)
            for p in range(max(1, r["platesPerStack"]))]


def screen_centre(st, r):
    return (st["c"] + (st["n"] * f32(half_depth(r) + f32(r["screenOffset"]))).astype(np.float32)
            + (st["u"] * screen_shift(r)).astype(np.float32)).astype(np.float32)


def tiles(structs, r, keep=False):
    out = []
    for st in structs:
        for centre in plate_centres(st, r):
            out += plate(st, centre, r, keep)
        if st["screen"]:
            out += screen(st, screen_centre(st, r), r, keep)
    return out


def crystal_sites(structs, r, count):
    sites = []
    if not structs:
        return sites
    front = half_depth(r)
    k = 0
    while len(sites) < count:
        st = structs[k % len(structs)]
        rnd = k // len(structs)
        off = f32(f32(front + f32(CRYSTAL_OFFSET)) + f32(rnd * CRYSTAL_STEP))
        sites.append(tuple(float(x) for x in (st["c"] + (st["n"] * off).astype(np.float32))))
        k += 1
    return sites


TILE_VOLUME = (TILE * FILL) ** 2 * THICKNESS


def census(r):
    structs, missed = layout(r)
    ts = tiles(structs, r)
    kinds = [t[1] for t in ts]
    reach = max(float(np.linalg.norm(s["c"])) + float(s["r"]) for s in structs) if structs else 0.0
    breakable = kinds.count(PLAIN) + kinds.count(DANGER)
    per_plate = len(plate({"index": 0, "u": None, "w": None}, None, r, False))
    return dict(total=len(ts), plain=kinds.count(PLAIN), danger=kinds.count(DANGER), super=kinds.count(SUPER),
                breakable=breakable, volume=len(ts) * TILE_VOLUME, structures=len(structs), missed=missed,
                reach=reach, per_plate=per_plate)


# ── The ring: the Brittle Star's disk, from its shipped config ─────────────────
RING_HALF_THICKNESS = 0.22   # BrittleStarHullBuilder.RingHalfThickness
RING_THICKNESS_SCALE = 4.0   # BrittleStarConfig ringBlastThicknessScale
BLAST_SCALE_CHARGE10 = 4.0   # BrittleStarConfig blastScale at Charge 10


def disk_half(charge10=False):
    return RING_HALF_THICKNESS * RING_THICKNESS_SCALE * (BLAST_SCALE_CHARGE10 if charge10 else 1.0)


def ring_yield(r, tilt_deg, charge10=False):
    """Breakable prisms one ring takes from one plate of this rung, blown at the plate's centre `tilt_deg` off square.
    A prism is taken when its half-extent along the disk normal (a tile lies flat in the plate) overlaps the disk."""
    half = disk_half(charge10)
    t = math.radians(tilt_deg)
    dn = (math.sin(t), 0.0, math.cos(t))     # the disk's normal; the plate's normal is +z
    taken = 0
    rad = r["plateRadius"]
    for a in range(-rad, rad + 1):
        for b in range(-rad, rad + 1):
            d2 = a * a + b * b
            if d2 > rad * rad:
                continue
            rim = d2 > (rad - 1) * (rad - 1)
            if not (r["dangerRim"] and rim) and not rim and r["ribEvery"] > 0 \
                    and (a % r["ribEvery"] == 0 or b % r["ribEvery"] == 0):
                continue                     # a rib: super-shielded, the disk cannot break it
            p = (a * TILE, b * TILE, 0.0)
            along = abs(p[0] * dn[0] + p[2] * dn[2])
            # the tile's half-extent along the disk normal: half its edge x |sin|, half its thickness x |cos|
            ext = 0.5 * TILE * FILL * abs(dn[0]) + 0.5 * THICKNESS * abs(dn[2])
            radial = math.sqrt(max(0.0, a * a * TILE * TILE + b * b * TILE * TILE - along * along))
            if along <= half + ext and radial <= NUCLEUS_RADIUS:
                taken += 1
    return taken


def breakable_per_plate(r):
    return ring_yield(r, 0.0)


if __name__ == "__main__":
    print(f"{'rung':16} {'total':>6} {'break':>6} {'danger':>6} {'super':>6} {'plate':>6} {'placed':>7} {'missed':>6} "
          f"{'reach':>6}")
    for r in RUNGS:
        c = census(r)
        print(f"I{r['i']} {r['label']:13} {c['total']:6,} {c['breakable']:6,} {c['danger']:6,} {c['super']:6,} "
              f"{c['per_plate']:6,} {c['structures']:7} {c['missed']:6} {c['reach']:6.0f}")
        if "--ring" in sys.argv:
            ys = [(t, ring_yield(r, t)) for t in (0, 1, 2, 5, 10, 20, 45)]
            print("     ring  " + "  ".join(f"{t:>2}deg {y:5}" for t, y in ys)
                  + f"   (Charge 10 at 5deg: {ring_yield(r, 5, True)})")
        if "--sites" in sys.argv:
            structs, _ = layout(r)
            for s in crystal_sites(structs, r, r["crystalCount"]):
                print(f"     site ({s[0]:.2f}, {s[1]:.2f}, {s[2]:.2f})  |{math.sqrt(sum(x * x for x in s)):.0f}|")
