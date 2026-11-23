#!/usr/bin/env python3
"""
Analytic prism budget for Flatline - the Brittle Star's charge arena (GameModes.Flatline = 84).

This is the model of `FlatlinePanes.cs` (and so of `SpawnableFlatline.cs`), and it is a MIRROR, not an estimate
(boneyard_budget.py's contract): `author_flatline_assets.py` imports it, so the arena's PhaseThresholds come from the
same numbers that build the arena. The arena is an OPEN field of thin-paned structures scattered through the cell
(Shombith, 2026-11-09: "scattered or open designed where we can go inside"): loose panes, tunnels and windowed cages.

It is exact because the C# was written to make it exact:
  * which tiles a structure has is decided by integers only (its half-size, the grid indices, the hole hash), so the
    tile count is exact as long as every structure is PLACED - and this file reports any structure it could not place
    (the generator fails on it; the C# tests assert the same counts);
  * placement draws are `Hash01` of integer keys, ported verbatim, and the float maths runs in float32 in the C#'s order
    (each Mathf call as the double function cast to float), so the positions agree too.

A change to any constant, the basis rule, the placement rule, the wall rule or the hole hash in FlatlinePanes.cs must
land here too; `author_flatline_assets.py --check` fails when the C# constants move.

    python3 Tools/Build/flatline_budget.py            # the table that belongs in FLATLINE.md
    python3 Tools/Build/flatline_budget.py --sites    # plus each rung's crystal sites

CONFIRM IN EDITOR: FrogletTools > Ecology > Measure Cell Environment Baselines. If the measurer disagrees with this
table, the C# and this file have drifted - fix both, do not paper over it in the asset.
"""
import math
import sys

import numpy as np

f32 = np.float32

# ── Constants: must match FlatlinePanes.cs ────────────────────────────────────
TILE = 10.0
THICKNESS = 2.0
FLUSH = 0.995
GAP = 40.0
MAX_ATTEMPTS = 64
CRYSTAL_OFFSET = 25.0
WINDOW_HALF = 1
HOLE_SALT = 5
KEY_SEED, KEY_STRUCTURE, KEY_ATTEMPT = 104729, 1031, 7

PANE, TUNNEL, CAGE = 0, 1, 2
PLAIN, DANGER, SHIELDED, SUPER = 0, 1, 2, 3

# ── The ladder (the generator authors these onto the four prefabs) ────────────
RUNGS = [
    dict(i=1, label="Scattered Panes", seed=8401, innerRadius=80.0, outerRadius=400.0,
         paneCount=24, paneHalfMin=3, paneHalfMax=4, tunnelCount=0, cageCount=0, roomHalf=4,
         ribEvery=4, dangerPaneEvery=0, dangerRoomEvery=0, holeShare=0.0, holeCellTiles=2, crystalCount=6,
         blurb="twenty-four loose panes scattered through open space, room to fly round every one, six crystals "
               "floating in front of panes"),
    dict(i=2, label="Tunnels", seed=8402, innerRadius=80.0, outerRadius=420.0,
         paneCount=20, paneHalfMin=3, paneHalfMax=4, tunnelCount=5, cageCount=0, roomHalf=4,
         ribEvery=4, dangerPaneEvery=0, dangerRoomEvery=0, holeShare=0.0, holeCellTiles=2, crystalCount=6,
         blurb="twenty loose panes and five open-ended tunnels to fly through, a crystal inside every tunnel"),
    dict(i=3, label="Cages", seed=8403, innerRadius=80.0, outerRadius=440.0,
         paneCount=18, paneHalfMin=3, paneHalfMax=4, tunnelCount=4, cageCount=4, roomHalf=4,
         ribEvery=4, dangerPaneEvery=3, dangerRoomEvery=0, holeShare=0.0, holeCellTiles=2, crystalCount=6,
         blurb="four windowed cages to fly into, four tunnels and eighteen panes, danger rims on every third pane, "
               "the crystals inside the rooms"),
    dict(i=4, label="Maze", seed=8404, innerRadius=60.0, outerRadius=460.0,
         paneCount=22, paneHalfMin=2, paneHalfMax=4, tunnelCount=4, cageCount=6, roomHalf=4,
         ribEvery=4, dangerPaneEvery=2, dangerRoomEvery=3, holeShare=0.2, holeCellTiles=2, crystalCount=5,
         blurb="six cages, four tunnels and twenty-two holed panes, danger on every other pane and every third room, "
               "five crystals deep inside the rooms"),
]


def hash01(n):
    """CellEnvironmentSpawnableBase.Hash01 / FlatlinePanes.Hash01, verbatim (uint32 wraparound)."""
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


def radius(kind, half):
    if kind == PANE:
        return f32(f32(f32(f32(half + 0.5) * f32(TILE)) * f32(1.4142135)) + f32(TILE))
    return f32(f32(f32(f32(half + 1.0) * f32(TILE)) * f32(1.7320508)) + f32(TILE))


def layout(r):
    """[dict(kind, half, index, c, n, u, w, danger)] and the count it could not place."""
    out, missed = [], 0
    r0 = f32(f32(f32(r["innerRadius"]) * f32(r["innerRadius"])) * f32(r["innerRadius"]))
    r1 = f32(f32(f32(r["outerRadius"]) * f32(r["outerRadius"])) * f32(r["outerRadius"]))
    total = r["cageCount"] + r["tunnelCount"] + r["paneCount"]
    span = max(1, r["paneHalfMax"] - r["paneHalfMin"] + 1)
    for k in range(total):
        if k < r["cageCount"]:
            kind, half, local = CAGE, r["roomHalf"], k
        elif k < r["cageCount"] + r["tunnelCount"]:
            kind, half, local = TUNNEL, r["roomHalf"], k - r["cageCount"]
        else:
            local = k - r["cageCount"] - r["tunnelCount"]
            kind, half = PANE, r["paneHalfMin"] + local % span
        every = r["dangerPaneEvery"] if kind == PANE else r["dangerRoomEvery"]
        danger = every > 0 and local % every == every - 1
        rad = radius(kind, half)
        placed = False
        for t in range(MAX_ATTEMPTS):
            key = i32(r["seed"] * KEY_SEED + k * KEY_STRUCTURE + t * KEY_ATTEMPT)
            rr = f32(math.pow(float(f32(r0 + f32(hash01(key) * f32(r1 - r0)))), float(f32(1) / f32(3))))
            c = (unit_from(hash01(key + 1), hash01(key + 2)) * rr).astype(np.float32)
            n = unit_from(hash01(key + 3), hash01(key + 4))
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
            out.append(dict(kind=kind, half=half, index=k, c=c, n=n, u=u, w=w, danger=danger, r=rad))
            placed = True
            break
        if not placed:
            missed += 1
    return out, missed


def floor_div(a, b):
    return a // b


def wall(st, centre, normal, a, b, h, window, r, holes, keep):
    out = []
    for i in range(-h, h + 1):
        for j in range(-h, h + 1):
            if window and abs(i) <= WINDOW_HALF and abs(j) <= WINDOW_HALF:
                continue
            rim = abs(i) == h or abs(j) == h
            if st["danger"] and rim:
                kind = DANGER
            elif r["ribEvery"] > 0 and ((math.fmod(i, r["ribEvery"]) == 0 and abs(i) < h)
                                       or (math.fmod(j, r["ribEvery"]) == 0 and abs(j) < h)):
                kind = SUPER
            else:
                if holes and r["holeShare"] > 0 and r["holeCellTiles"] > 0:
                    cx, cy = floor_div(i, r["holeCellTiles"]), floor_div(j, r["holeCellTiles"])
                    if hash01(i32(st["index"] * 7919 + cx * 131 + cy * 17 + HOLE_SALT)) < f32(r["holeShare"]):
                        continue
                kind = PLAIN
            pos = None
            if keep:
                pos = (centre + (a * f32(i * TILE)).astype(np.float32) + (b * f32(j * TILE)).astype(np.float32))
            out.append((st["index"], kind, pos, normal))
    return out


def tiles(structs, r, keep=False):
    out = []
    for st in structs:
        if st["kind"] == PANE:
            out += wall(st, st["c"], st["n"], st["u"], st["w"], st["half"], False, r, True, keep)
            continue
        d = f32(f32(st["half"] + 0.5) * f32(TILE))
        wh = st["half"] - 1
        win = st["kind"] == CAGE
        c, n, u, w = st["c"], st["n"], st["u"], st["w"]
        faces = [((c + u * d), u, w, n), ((c - u * d), -u, w, n), ((c + w * d), w, n, u), ((c - w * d), -w, n, u)]
        if win:
            faces += [((c + n * d), n, u, w), ((c - n * d), -n, u, w)]
        for centre, normal, a, b in faces:
            out += wall(st, centre.astype(np.float32), normal, a, b, wh, win, r, False, keep)
    return out


def crystal_sites(structs, count):
    sites = [tuple(float(x) for x in s["c"]) for s in structs if s["kind"] != PANE][:count]
    for s in structs:
        if len(sites) >= count:
            break
        if s["kind"] == PANE:
            sites.append(tuple(float(x) for x in (s["c"] + s["n"] * f32(CRYSTAL_OFFSET))))
    return sites


TILE_VOLUME = (TILE * FLUSH) ** 2 * THICKNESS


def census(r):
    structs, missed = layout(r)
    ts = tiles(structs, r)
    kinds = [t[1] for t in ts]
    reach = max(float(np.linalg.norm(s["c"])) + float(s["r"]) for s in structs)
    return dict(total=len(ts), plain=kinds.count(PLAIN), danger=kinds.count(DANGER), super=kinds.count(SUPER),
                volume=len(ts) * TILE_VOLUME, structures=len(structs), missed=missed, reach=reach)


if __name__ == "__main__":
    print(f"{'rung':18} {'total':>6} {'plain':>6} {'danger':>6} {'super':>6} {'placed':>7} {'missed':>6} {'reach':>6}")
    for r in RUNGS:
        c = census(r)
        print(f"I{r['i']} {r['label']:15} {c['total']:6,} {c['plain']:6,} {c['danger']:6,} {c['super']:6,} "
              f"{c['structures']:7} {c['missed']:6} {c['reach']:6.0f}")
        if "--sites" in sys.argv:
            structs, _ = layout(r)
            for s in crystal_sites(structs, r["crystalCount"]):
                print(f"     site ({s[0]:.2f}, {s[1]:.2f}, {s[2]:.2f})")
