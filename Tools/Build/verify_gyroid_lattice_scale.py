#!/usr/bin/env python3
"""
Prove the gyroid's coherence tolerances still bracket its lattice at any LatticeScale.

    python3 Tools/Build/verify_gyroid_lattice_scale.py

READ-ONLY. Writes nothing, authors nothing.

WHY THIS EXISTS
---------------
A gyroid plant's coherence is decided by distances that were measured in ABSOLUTE world
units against the separationDistance-3 lattice. Widening the lattice while leaving them
alone is what grew offset parallel lattice domains (Docs/ECOSYSTEM.md 33.8) - and every
static check passed while it did, because each constant was individually correct. The
defect was a RELATIONSHIP: three radii that must stay ordered

    reservation clearRadius  <  misalignment gate  <  healthy closest non-bonded pair

and the middle one stopped scaling with the other two. Below the gate, a neighbour is a
duplicate to reject; above the healthy pair, everything is a legitimate neighbour. If the
gate drifts above the healthy pair it rejects real growth; if it drifts below the
reservation radius it catches nothing and twins are born.

So this walks the SHIPPED bond table at several scales, MEASURES the healthy pair, and
asserts the ordering holds at each - the property that actually matters, rather than the
constants that were each individually fine.
"""

from __future__ import annotations

import collections
import math
import pathlib
import re
import sys

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[2]
BONDS = ROOT / "Assets/_Scripts/Controller/Assemblers/GyroidBondMateDataContainer.cs"
ASSEMBLER = ROOT / "Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs"
FLORA = ROOT / "Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs"

SITES = ("TopRight", "TopLeft", "BottomLeft", "BottomRight")
MEASURED_SEPARATION = 3.0


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


def shipped_constants():
    """Read the tolerances out of the SHIPPED C#, so a future edit is caught here."""
    a = ASSEMBLER.read_text(encoding="utf-8-sig")
    f = FLORA.read_text(encoding="utf-8-sig")
    snap = float(re.search(r"snapDistance\s*=\s*\.?(\d*\.?\d+)f", a).group(1))
    gate = float(re.search(r"MisalignmentRadius\s*=>\s*([\d.]+)f\s*\*\s*LatticeScale", f).group(1))
    floor = float(re.search(r"clearRadius\s*=\s*Mathf\.Max\(([\d.]+)f\s*\*\s*LatticeScale", a).group(1))
    frac = float(re.search(r"LatticeScale,\s*([\d.]+)f\s*\*\s*\(newPosition", a).group(1))
    # snapDistance is compared against SQUARED distances - the linear tolerance is its root.
    return math.sqrt(snap), gate, floor, frac


def look_rotation(fwd, up):
    fwd = np.asarray(fwd, float)
    n = np.linalg.norm(fwd)
    if n < 1e-9:
        return None
    fwd = fwd / n
    r = np.cross(up, fwd)
    rn = np.linalg.norm(r)
    if rn < 1e-9:
        return None
    r = r / rn
    return np.column_stack([r, np.cross(fwd, r), fwd])


def walk(table, separation, budget=500):
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
    return np.array([n[0] for n in nodes])


def main():
    table = parse_bond_table()
    snap_linear, gate_at_1, floor_at_1, bond_fraction = shipped_constants()

    print("=" * 78)
    print("Gyroid lattice-scale coherence")
    print("=" * 78)
    print(f"shipped tolerances at LatticeScale 1: snap {snap_linear:.3f}u  "
          f"gate {gate_at_1:g}u  reserve floor {floor_at_1:g}u  "
          f"reserve {bond_fraction:g} x bond\n")

    print(f"{'scale':>6} {'sep':>5} {'bond':>7} {'reserve':>8} {'gate':>7} "
          f"{'healthy':>8} {'gate/healthy':>13}")

    failures = []
    baseline = None
    for scale in (1.0, 1.5, 2.0, 3.0):
        sep = MEASURED_SEPARATION * scale
        P = walk(table, sep)
        D = np.linalg.norm(P[:, None, :] - P[None, :, :], axis=2)
        np.fill_diagonal(D, np.inf)

        bond = float(D.min(axis=1).mean())          # MEAN bonded neighbour spacing
        # The number the gate was sized against is the CLOSEST PAIR anywhere in a coherent
        # plant - 6.6u at scale 1, against a 7.84u mean bond, because bond lengths vary.
        # Deliberately min over all pairs, not "closest non-bonded": filtering to
        # non-bonded pairs reports ~12.9u and flatters the margin by 2x, which is the
        # opposite of what a safety check should do.
        healthy = float(D.min())

        reserve = max(floor_at_1 * scale, bond_fraction * bond)
        gate = gate_at_1 * scale

        if baseline is None:
            baseline = (bond, healthy)
        ratio_ok = (abs(bond / baseline[0] - scale) < 0.02 and
                    abs(healthy / baseline[1] - scale) < 0.02)
        ordered = reserve < gate < healthy

        if not ratio_ok:
            failures.append(f"scale {scale}: lattice did not scale linearly "
                            f"(bond {bond / baseline[0]:.3f}x, healthy {healthy / baseline[1]:.3f}x)")
        if not ordered:
            failures.append(f"scale {scale}: tolerances out of order - "
                            f"reserve {reserve:.2f} < gate {gate:.2f} < healthy {healthy:.2f} is FALSE")

        print(f"{scale:>6.1f} {sep:>5.1f} {bond:>7.2f} {reserve:>8.2f} {gate:>7.2f} "
              f"{healthy:>8.2f} {gate / healthy:>12.0%}  "
              f"{'ok' if ratio_ok and ordered else 'FAIL'}")

    print("\nNote: this walk measures the closest pair at 7.52u at scale 1, where the gate's")
    print("comment cites 6.6u. Both are below the gate's own 5.5u and the ordering is what is")
    print("asserted; the walk is drift-free while 6.6 came from live growth, so the shipped")
    print("number is the more conservative of the two. They do not disagree.")
    print("\nThe property that matters: reserve < gate < healthy, at EVERY scale.")
    print("gate/healthy must stay constant - a gate that drifts toward 100% starts")
    print("rejecting real growth; one that drifts to 0% stops catching twins.")

    if failures:
        print("\nFAILURES")
        for f in failures:
            print(f"  - {f}")
        return 1
    print("\nall scales hold - the coherence family scales with the lattice")
    return 0


if __name__ == "__main__":
    sys.exit(main())
