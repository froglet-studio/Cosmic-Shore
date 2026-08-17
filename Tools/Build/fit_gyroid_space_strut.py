#!/usr/bin/env python3
"""
Fit the SPACE gyroid's strut to its own lattice spacing, and author it.

    python3 Tools/Build/fit_gyroid_space_strut.py            # measure + report
    python3 Tools/Build/fit_gyroid_space_strut.py --write    # + author the Space configs

WHY
---
Space is the skeletal element: the longest, thinnest prism of the four. Its shipped
20 x 1 x 1 at separationDistance 3 spans 2.56 of the gyroid's 7.83-unit prism spacing,
so 99% of its prisms run through a neighbour - measured, up to 1.14u of penetration.

A longer prism cannot be made to clear its neighbours by thinning it. Walking the
shipped bond table and binary-searching the longest ZERO-overlap strut gives 12.48u at
thickness 1.0 and only 13.37u at 0.5 - the bound is the neighbour sitting along the
strut's own axis, and thinness does not move it. The lever is the LATTICE:

    max clear strut  ~=  1.75 x prism spacing,  and  spacing scales with separationDistance

So a strut that is both longer and clear needs a wider lattice, which is exactly what
FloraVariantTuning.SeparationDistance now buys per element.

    sep 3.0  ->  spacing  7.83  ->  clear 13.37 at t 0.5   (the shipped 20 overlaps)
    sep 4.5  ->  spacing 11.75  ->  clear 20.50
    sep 5.0  ->  spacing 13.05  ->  clear 22.97 at t 0.45  <- authored
    sep 6.0  ->  spacing 15.66  ->  clear 27.63

THE CONSEQUENCE THIS FILE EXISTS TO FLAG
----------------------------------------
Every distance in GyroidOctagonData - the ring radius, the neighbouring octagon centres
and seed positions, the territory radius, the membership and dedupe tolerances - was
MEASURED at separationDistance 3. An element that widens its lattice must widen all of
them by the same ratio, or its founder computes a ring centre that does not exist and
its ownership gate refuses its own sites. That ratio is AssembledFlora.LatticeScale, and
it is 1 for every element that keeps the prefab's spacing.

This walks the SHIPPED bond table (GyroidBondMateDataContainer.cs) rather than a copy of
it, so the fit is against the lattice the game actually grows.
"""

from __future__ import annotations

import argparse
import collections
import math
import pathlib
import re
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from fit_schwarz_p_leaf_sizes import obb_penetration  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parents[2]
BONDS = ROOT / "Assets/_Scripts/Controller/Assemblers/GyroidBondMateDataContainer.cs"

MEASURED_SEPARATION = 3.0          # what GyroidOctagonData's distances were measured at
SPACE_LATTICE_SCALE = 5.0 / 3.0    # FloraVariantTuning.LatticeScale for Space
SPACE_SEPARATION = MEASURED_SEPARATION * SPACE_LATTICE_SCALE
SPACE_THICKNESS = 0.45

# The strut is DOUBLE the longest zero-overlap length (22.96), by design sign-off: a strut
# that clears every neighbour is a strut that reaches none of them, and the lattice read as
# disconnected bars. The crossings are what close it up. They are also bounded - see
# report_closure(), which is run on every invocation so the cost stays measured, not assumed.
SPACE_LENGTH = 45.92

# Measured prism spacing at SPACE_SEPARATION. Asserted against the walk on every run, so the
# Schwarz P fit - which derives its own Space prism from these proportions - can import a
# number that cannot silently drift from the lattice it describes.
SPACE_SPACING = 13.05
SITES = ("TopRight", "TopLeft", "BottomLeft", "BottomRight")

# Every config whose Element is Space (3) and whose FloraPrefab is GyroidFlora.
SPACE_CONFIGS = (
    "Assets/_SO_Assets/Lifeforms/Gyroid Flora Space.asset",
    "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Space Gyroid Flora Config Data.asset",
    "Assets/_SO_Assets/Cell Configs/WildLife Blitz Cells/Cell 4/"
    "Wildlife Cell 4 Space Gyroid Flora Config Data.asset",
)


def parse_bond_table():
    src = BONDS.read_text(encoding="utf-8-sig")
    pat = re.compile(r"\(GyroidBlockType\.(\w+),\s*CornerSiteType\.(\w+)\),\s*"
                     r"new GyroidBondMateData\s*\{(.*?)\}\s*\}", re.S)

    def vec(body, name):
        m = re.search(name + r"\s*=\s*new Vector3\(([^)]*)\)", body)
        return [float(x.strip().rstrip("f")) for x in m.group(1).split(",")]

    table = {}
    for m in pat.finditer(src):
        bt, site, body = m.groups()
        e = {f: vec(body, f) for f in ("DeltaPosition", "DeltaUp", "DeltaForward")}
        e["BlockType"] = re.search(r"BlockType\s*=\s*GyroidBlockType\.(\w+)", body).group(1)
        table[(bt, site)] = e
    if len(table) != 48:
        raise SystemExit(f"expected 48 bond entries, parsed {len(table)}")
    return table


def look_rotation(f, u):
    f = np.asarray(f, float)
    n = np.linalg.norm(f)
    if n < 1e-9:
        return None
    f = f / n
    r = np.cross(u, f)
    rn = np.linalg.norm(r)
    if rn < 1e-9:
        return None
    r = r / rn
    return np.column_stack([r, np.cross(f, r), f])   # columns: local x, y, z


def walk(table, separation, budget=700):
    """Grow the lattice exactly as GyroidAssembler does, at the given separation."""
    tol = 2.5 * separation / MEASURED_SEPARATION
    nodes = [(np.zeros(3), np.eye(3), "AB")]
    grid = {}

    def key(p):
        return tuple(np.floor(p / tol).astype(int))

    def seen(p):
        k = key(p)
        for d in np.ndindex(3, 3, 3):
            for q in grid.get(tuple(k[i] + d[i] - 1 for i in range(3)), ()):
                if np.linalg.norm(q - p) < tol:
                    return True
        return False

    grid.setdefault(key(nodes[0][0]), []).append(nodes[0][0])
    frontier = collections.deque([0])
    while frontier and len(nodes) < budget:
        i = frontier.popleft()
        pos, R, bt = nodes[i]
        for s in SITES:
            e = table.get((bt, s))
            if e is None:
                continue
            child = pos + R.dot(np.array(e["DeltaPosition"]) * separation)
            Rc = look_rotation(R.dot(np.array(e["DeltaForward"]) + [0, 0, 1.0]),
                               R.dot(np.array(e["DeltaUp"]) + [0, 1.0, 0]))
            if Rc is None or seen(child) or len(nodes) >= budget:
                continue
            grid.setdefault(key(child), []).append(child)
            nodes.append((child, Rc, e["BlockType"]))
            frontier.append(len(nodes) - 1)
    return nodes


def clear_strut(table, separation, thickness):
    """Prism spacing, and the longest ZERO-overlap strut, at this separation."""
    nodes = walk(table, separation)
    P = np.array([n[0] for n in nodes])
    B = np.array([n[1].T for n in nodes])          # rows = local axes
    D = np.linalg.norm(P[:, None, :] - P[None, :, :], axis=2)
    np.fill_diagonal(D, np.inf)
    spacing = float(D.min(axis=1).mean())

    # Only prisms well inside the walked patch: a boundary prism has fewer neighbours
    # and would flatter the fit.
    radius = np.linalg.norm(P - P.mean(axis=0), axis=1)
    core = np.where(radius < np.percentile(radius, 45))[0]

    def pairs(length):
        half = np.array([length, thickness, thickness]) * 0.5
        reach = float(np.linalg.norm(half)) * 2.2
        n = 0
        for i in core:
            for j in np.where(D[i] < reach)[0]:
                if obb_penetration(P[i], B[i], half, P[j], B[j], half) > 1e-6:
                    n += 1
        return n

    lo, hi = 1.0, 80.0
    if pairs(lo) > 0:
        return spacing, None
    for _ in range(20):
        mid = 0.5 * (lo + hi)
        if pairs(mid) == 0:
            lo = mid
        else:
            hi = mid
    return spacing, math.floor(lo * 100) / 100


def level_spread(table, separation, thickness, length, per_level, levels=(1, 3, 5)):
    """Overlap count at each level, if LeafScalePerLevel multiplies the prism.

    Flora.ApplyLevel scales leafSize by bodyScalePerLevel^(Level-1) and touches
    NOTHING else - separationDistance, and therefore the lattice, is fixed. So a
    per-level scale above 1 grows the prism through a lattice that stays put.
    """
    nodes = walk(table, separation)
    P = np.array([n[0] for n in nodes])
    B = np.array([n[1].T for n in nodes])
    D = np.linalg.norm(P[:, None, :] - P[None, :, :], axis=2)
    np.fill_diagonal(D, np.inf)
    radius = np.linalg.norm(P - P.mean(axis=0), axis=1)
    core = np.where(radius < np.percentile(radius, 45))[0]

    out = []
    for lv in levels:
        s = per_level ** (lv - 1)
        half = np.array([length, thickness, thickness]) * 0.5 * s
        reach = float(np.linalg.norm(half)) * 2.2
        n = 0
        for i in core:
            for j in np.where(D[i] < reach)[0]:
                if obb_penetration(P[i], B[i], half, P[j], B[j], half) > 1e-6:
                    n += 1
        out.append((lv, length * s, n))
    return out


def report_closure(table, separation, thickness, lengths):
    """What CLOSING the lattice costs: the crossings a long strut makes, and their depth.

    Kept as a first-class report rather than a pass/fail gate, because zero-overlap is not
    the objective here - a strut short enough to clear every neighbour reaches none of them,
    and the structure reads as disconnected bars. What matters is that the cost is BOUNDED,
    which it is: past ~2.6 spans the strut runs down an empty channel and hits nothing new.
    """
    nodes = walk(table, separation)
    P = np.array([n[0] for n in nodes])
    B = np.array([n[1].T for n in nodes])
    D = np.linalg.norm(P[:, None, :] - P[None, :, :], axis=2)
    np.fill_diagonal(D, np.inf)
    spacing = float(D.min(axis=1).mean())
    radius = np.linalg.norm(P - P.mean(axis=0), axis=1)
    core = np.where(radius < np.percentile(radius, 45))[0]

    rows = []
    for length in lengths:
        half = np.array([length, thickness, thickness]) * 0.5
        reach = float(np.linalg.norm(half)) * 2.2
        hits, worst = 0, 0.0
        for i in core:
            for j in np.where(D[i] < reach)[0]:
                d = obb_penetration(P[i], B[i], half, P[j], B[j], half)
                if d > 1e-6:
                    hits += 1
                    worst = max(worst, d)
        rows.append((length, length / spacing, hits, worst))
    return spacing, rows


def author(leaf, scale):
    """Set LeafSize + LatticeScale on every Space gyroid config."""
    written = []
    for rel in SPACE_CONFIGS:
        path = ROOT / rel
        if not path.exists():
            raise SystemExit(
                f"SPACE_CONFIGS names a path that does not exist: {rel}\n"
                "A skipped config is a config that ships with the OLD strut against the NEW "
                "lattice scale, which is exactly the state this fit exists to prevent.")
        text = path.read_text()
        new, n = re.subn(r"^    LeafSize: \{[^}]*\}$",
                         f"    LeafSize: {{x: {leaf[0]:g}, y: {leaf[1]:g}, z: {leaf[2]:g}}}",
                         text, count=1, flags=re.M)
        if n != 1:
            raise SystemExit(f"no LeafSize row in {path.name}")
        # The previous pass authored an absolute SeparationDistance; retire any it left.
        new = re.sub(r"^    SeparationDistance: .*\n", "", new, flags=re.M)
        if re.search(r"^    LatticeScale: .*$", new, flags=re.M):
            new = re.sub(r"^    LatticeScale: .*$",
                         f"    LatticeScale: {scale:g}", new, count=1, flags=re.M)
        else:
            # Insert after GrowPeriod, matching FloraVariantTuning's declaration order.
            new, n = re.subn(r"^(    GrowPeriod: .*)$",
                             rf"\1\n    LatticeScale: {scale:g}",
                             new, count=1, flags=re.M)
            if n != 1:
                raise SystemExit(f"no GrowPeriod anchor in {path.name}")
        # The fit is the LATTICE's size, not the plant's: LeafScalePerLevel scales the
        # prism and leaves separationDistance alone, so anything above 1 grows a fitted
        # strut straight through its neighbours (measured below).
        new, n = re.subn(r"^  LeafScalePerLevel: .*$", "  LeafScalePerLevel: 1",
                         new, count=1, flags=re.M)
        if n != 1:
            raise SystemExit(f"no LeafScalePerLevel row in {path.name}")
        path.write_text(new)
        written.append(path)
    return written


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true", help="author the Space configs")
    args = ap.parse_args()

    table = parse_bond_table()
    print("=" * 78)
    print("Gyroid SPACE strut fit")
    print("=" * 78)
    print(f"{'sep':>5} {'spacing':>9} {'clear len':>10} {'spans':>6} {'aspect':>9}")

    measured_spacing = None
    for sep in (MEASURED_SEPARATION, 4.0, SPACE_SEPARATION, 6.0):
        spacing, length = clear_strut(table, sep, SPACE_THICKNESS)
        mark = ""
        if abs(sep - SPACE_SEPARATION) < 1e-6:
            measured_spacing = spacing
            mark = "   <- Space's lattice"
        if length is None:
            print(f"{sep:>5.1f} {spacing:>9.2f}   (no clear fit){mark}")
            continue
        print(f"{sep:>5.1f} {spacing:>9.2f} {length:>10.2f} {length / spacing:>6.2f} "
              f"{length / SPACE_THICKNESS:>7.0f}:1{mark}")

    # The Schwarz P fit imports SPACE_SPACING to derive its own Space prism from these
    # proportions, so a drift between the constant and the lattice would silently mis-size
    # a different species. Fail here instead.
    if abs(measured_spacing - SPACE_SPACING) > 0.01:
        raise SystemExit(f"SPACE_SPACING is {SPACE_SPACING}, the walk measures "
                         f"{measured_spacing:.4f} - update the constant")

    leaf = (SPACE_LENGTH, SPACE_THICKNESS, SPACE_THICKNESS)
    print(f"\n  Space  {leaf[0]:g} x {leaf[1]:g} x {leaf[2]:g}   "
          f"LatticeScale {SPACE_LATTICE_SCALE:.4f} (separationDistance "
          f"{MEASURED_SEPARATION:g} -> {SPACE_SEPARATION:g}, spacing {measured_spacing:.2f})")
    print(f"  {SPACE_LENGTH / measured_spacing:.2f} spans, aspect "
          f"{SPACE_LENGTH / SPACE_THICKNESS:.0f}:1")

    print("\nCLOSURE - zero-overlap is NOT the objective; a bounded crossing count is")
    spacing, rows = report_closure(table, SPACE_SEPARATION, SPACE_THICKNESS,
                                   (22.96, 26.0, 34.0, SPACE_LENGTH, 60.0))
    print(f"  {'length':>8} {'spans':>6} {'crossings':>10} {'worst':>7}")
    for length, spans, hits, worst in rows:
        mark = "   <- authored" if abs(length - SPACE_LENGTH) < 0.01 else ""
        print(f"  {length:>8.2f} {spans:>6.2f} {hits:>10d} {worst:>7.3f}{mark}")
    saturated = rows[-1][2] <= rows[-2][2]
    print(f"  crossings stop growing past ~2.6 spans: {'yes' if saturated else 'NO - investigate'}")
    if not saturated:
        raise SystemExit("the crossing count is still climbing at 60u - the strut is not "
                         "running down an empty channel and this fit is wrong")

    print("\nLEVEL SPREAD - the Blob cell rolls these at Levels 1..5")
    for per_level in (1.15, 1.0):
        row = level_spread(table, SPACE_SEPARATION, SPACE_THICKNESS, SPACE_LENGTH, per_level)
        cells = "   ".join(
            f"L{lv}: {n} crossings ({length:.1f}u)" for lv, length, n in row)
        print(f"  LeafScalePerLevel {per_level:<6g} {cells}")
    print("  -> pinned at 1: the prism size is the LATTICE's, not the plant's")

    if args.write:
        print()
        for p in author(leaf, SPACE_LATTICE_SCALE):
            print(f"  wrote {p.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
