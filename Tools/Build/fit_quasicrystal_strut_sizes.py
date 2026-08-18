#!/usr/bin/env python3
"""Fit the quasicrystal flora's strut prisms to its own lattice.

    python3 Tools/Build/fit_quasicrystal_strut_sizes.py            # measure + report
    python3 Tools/Build/fit_quasicrystal_strut_sizes.py --check    # gate: shipped assets match
    python3 Tools/Build/fit_quasicrystal_strut_sizes.py --write    # author the element assets

THE FIT
-------
A prism is one EDGE of the icosahedral quasilattice (QuasicrystalLatticeData):
a strut of authored cross-section, spanning the edge minus a tip inset at each
end. Every edge has exactly the same length and struts meet at 63.43 / 116.57
degrees (the icosahedral vertex axes), so whether tips touch is an exact OBB
question over the measured patch - fitted here per element with a separating-
axis test, exactly as fit_schwarz_p_leaf_sizes.py fits that species' plates.
Per Docs/ECOSYSTEM.md 34.10, spacing (edgeLength / LatticeScale) and prism
size are INDEPENDENT dials: this script fits the PRISM and never touches the
lattice.

For each element the CROSS-SECTION is the authored identity and the LENGTH is
binary-searched to the largest flush value (rounded DOWN, so the zero-overlap
result stays true of the shipped number):

  * Time   - the reference slender strut.
  * Mass   - the heavy element: a thick strut, shorter by consequence (a fat
             tip needs more clearance at a 63-degree node).
  * Space  - reach: LatticeScale 2 doubles the edge; the strut spans it.
  * Charge - ARMOURED (Flora.ResolveShieldPeriod floors every Charge flora at
             a 1s cadence): the shield is the octahedron CIRCUMSCRIBING the
             box at 3x the half-extents (Docs/ECOSYSTEM.md 35), so the Charge
             strut is Time's fit shrunk UNIFORMLY (aspect is identity) to the
             octahedron touch scale plus 10% clearance. NOTE:
             Tools/Build/fit_shield_clearance.py does NOT know this species -
             THIS script owns the Charge fit, and says so, because two fitters
             must never own one asset field (Docs/ECOSYSTEM.md 35.3).

THE HEART SEAT
--------------
A strut with a HEART at one end holds back by QuasicrystalAssembler's
heartSeatInset so the plant's crystal sits clear inside its twelve-ray star.
The heart's crystal rides the platform's one level curve (world scale 3.5 at
level 1 to 4.25 at level 5, apparent radius <= ~0.6 x scale - the Schwarz P
flat-point seat shipped 4.2u across for the same crystals), so the seat must
clear ~2.55u at level 5. This script measures the ACTUAL clear radius at every
heart of the patch - including non-incident struts passing nearby - and gates
on it.
"""
from __future__ import annotations

import argparse
import math
import pathlib
import re
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from verify_icosahedral_quasilattice_tables import Lattice, parse_cs  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parents[2]
LIFEFORMS = ROOT / "Assets/_SO_Assets/Lifeforms"
BLOCK_PREFAB = ROOT / "Assets/_Prefabs/Trails/QuasicrystalBlock Variant.prefab"
FLORA_PREFAB = ROOT / "Assets/_Prefabs/FloraAndFauna/QuasicrystalFlora.prefab"

EDGE = 12.0          # QuasicrystalBlock Variant's authored edgeLength
HEART_SEAT = 2.6     # QuasicrystalAssembler.heartSeatInset - read back off the prefab below
CRYSTAL_NEED = 2.55  # level-5 heart apparent radius (4.25 world x ~0.6)
SHIELD_SCALE = 3.0   # OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE on box HALF-extents
CLEARANCE = 1.10     # ship at touch / 1.10, the fit_shield_clearance convention

# Authored identities: cross-section (y, z) and, for Space, the lattice scale.
# Lengths are FITTED below. Space's strut is DELIBERATELY long and slender - the
# element's identity across all three lattice species (gyroid 40x1x1, Schwarz P
# 30x0.5x0.5) - and like theirs it is fitted flush rather than interpenetrating,
# because a strut species' struts MEET at nodes instead of crossing mid-air.
ELEMENTS = {
    "Time":   dict(cross=(0.8, 0.8), lattice_scale=1.0),
    "Mass":   dict(cross=(1.7, 1.7), lattice_scale=1.0),
    "Space":  dict(cross=(0.7, 0.7), lattice_scale=2.0),
    # Charge: derived from Time by uniform shrink - see fit_charge().
}

PATCH_VERTS = 9000   # struts for the pair census; every angle class appears many times


class Failures(list):
    def check(self, ok, label, detail=""):
        tag = "[ok  ]" if ok else "[FAIL]"
        print(f"{tag} {label}" + (f" - {detail}" if detail else ""))
        if not ok:
            self.append(label)
        return ok


# ---------------------------------------------------------------------------
# Geometry
# ---------------------------------------------------------------------------

def obb_axes(t):
    """Strut frame per edge axis: X along the edge, Y = tangent, Z = normal
    (LookRotation(forward: normal, up: tangent) => x = cross(up, forward))."""
    frames = []
    for i in range(6):
        n = t["strut_normal"][i]
        tg = t["strut_tangent"][i]
        x = np.cross(tg, n)
        frames.append((x / np.linalg.norm(x), tg / np.linalg.norm(tg), n / np.linalg.norm(n)))
    return frames


def census_classes(lat, max_verts):
    """The lattice is locally finite: every strut pair within the cutoff falls into
    one of a small set of RELATIVE CONFIGURATION CLASSES - (axisA, axisB, integer
    offset between minus-ends, endpoint heartness) - so the SAT runs once per class
    per candidate length instead of once per pair (hundreds vs. hundreds of
    thousands). The same trick serves the heart-seat census: every (heart, nearby
    strut) relation is a class of (axis, integer offset, heartness).

    Returns (pair_classes, seat_classes, edge_count, heart_count)."""
    seed = (0,) * 6
    patch = lat.bfs_patch(seed, max_verts)
    vset = set(patch)
    edges = []          # (v6, axis, heartA, heartB)
    hearts = []
    for v in patch:
        if lat.is_heart(v):
            hearts.append(v)
        for i in range(6):
            e = [0] * 6
            e[i] = 1
            u = tuple(a + b for a, b in zip(v, e))
            if u in vset:
                edges.append((v, i, lat.is_heart(v), lat.is_heart(u)))

    mids = np.array([lat.par_of(v) + 0.5 * lat.par_of(tuple(
        1 if k == i else 0 for k in range(6))) for v, i, _, _ in edges])
    pair_classes = set()
    cutoff = 1.2   # lattice units; struts are 1 edge long, so this catches every contact class
    for a in range(len(edges)):
        d2 = ((mids[a + 1:] - mids[a]) ** 2).sum(1)
        for off in np.nonzero(d2 < cutoff * cutoff)[0]:
            b = a + 1 + int(off)
            va, ia, ha1, hb1 = edges[a]
            vb, ib, ha2, hb2 = edges[b]
            delta = tuple(x - y for x, y in zip(vb, va))
            pair_classes.add((ia, ib, delta, ha1, hb1, ha2, hb2))

    seat_classes = set()
    hp = np.array([lat.par_of(h) for h in hearts])
    for k, h in enumerate(hearts):
        d2 = ((mids - hp[k]) ** 2).sum(1)
        for idx in np.nonzero(d2 < 1.0)[0]:
            v, i, ha, hb = edges[int(idx)]
            delta = tuple(x - y for x, y in zip(v, h))
            seat_classes.add((i, delta, ha, hb))
    return pair_classes, seat_classes, len(edges), len(hearts)


def class_struts(lat, cls, edge_world, plain_len, heart_seat):
    """Both struts of a pair class, in world units, minus-end A at the origin."""
    ia, ib, delta, ha1, hb1, ha2, hb2 = cls
    plain_inset = max(0.0, (edge_world - plain_len) * 0.5)

    def span(origin, axis, heart_a, heart_b):
        tip_a = max(heart_seat, plain_inset) if heart_a else plain_inset
        tip_b = max(heart_seat, plain_inset) if heart_b else plain_inset
        length = edge_world - tip_a - tip_b
        frac = (tip_a + (edge_world - tip_b)) / (2 * edge_world)
        e = tuple(1 if k == axis else 0 for k in range(6))
        center = origin + lat.par_of(e) * (frac * edge_world)
        return center, length

    ca, la = span(np.zeros(3), ia, ha1, hb1)
    cb, lb = span(lat.par_of(delta) * edge_world, ib, ha2, hb2)
    return (ca, ia, la), (cb, ib, lb)


def obb_touch_scale(cA, hA, fA, cB, hB, fB):
    """Largest uniform scale of both half-extent sets at which a separating axis
    still exists (SAT over faces + edge cross products; both boxes centred)."""
    d = cB - cA
    axes = list(fA) + list(fB)
    for a in fA:
        for b in fB:
            c = np.cross(a, b)
            n = np.linalg.norm(c)
            if n > 1e-9:
                axes.append(c / n)
    best = 0.0
    for u in axes:
        du = abs(float(d @ u))
        r = sum(h * abs(float(u @ ax)) for h, ax in zip(hA, fA)) + \
            sum(h * abs(float(u @ ax)) for h, ax in zip(hB, fB))
        if r < 1e-12:
            continue
        best = max(best, du / r)
    return best   # scale < best => separated on some axis


def octa_touch_scale(cA, rA, fA, cB, rB, fB):
    """Touch scale for two shield octahedra (vertices on the frame axes at the
    given reaches). Candidate axes: both octahedra's face normals + edge cross
    products; support(u) = max_i r_i |u . axis_i|."""
    d = cB - cA

    def support(u, r, f):
        return max(r[k] * abs(float(u @ f[k])) for k in range(3))

    def faces(r, f):
        out = []
        for sx in (1, -1):
            for sy in (1, -1):
                for sz in (1, -1):
                    n = sx / r[0] * f[0] + sy / r[1] * f[1] + sz / r[2] * f[2]
                    out.append(n / np.linalg.norm(n))
        return out

    def edges(r, f):
        vs = [r[0] * f[0], -r[0] * f[0], r[1] * f[1], -r[1] * f[1], r[2] * f[2], -r[2] * f[2]]
        out = []
        for a in range(6):
            for b in range(6):
                if a // 2 == b // 2:
                    continue
                e = vs[a] - vs[b]
                n = np.linalg.norm(e)
                if n > 1e-9:
                    out.append(e / n)
        return out

    axes = faces(rA, fA) + faces(rB, fB)
    for ea in edges(rA, fA):
        for eb in edges(rB, fB):
            c = np.cross(ea, eb)
            n = np.linalg.norm(c)
            if n > 1e-9:
                axes.append(c / n)
    best = 0.0
    for u in axes:
        du = abs(float(u @ d))
        r = support(u, rA, fA) + support(u, rB, fB)
        if r > 1e-12:
            best = max(best, du / r)
    return best


def pair_census(lat, classes, frames, edge_world, plain_len, heart_seat, half_y, half_z):
    """Touch scales over every configuration class (min = the species' flush scale)."""
    worst = 10.0
    overlapping = 0
    for cls in classes:
        (ca, ia, la), (cb, ib, lb) = class_struts(lat, cls, edge_world, plain_len, heart_seat)
        s = obb_touch_scale(ca, (la / 2, half_y, half_z), frames[ia],
                            cb, (lb / 2, half_y, half_z), frames[ib])
        if s < 1.0:
            overlapping += 1
        worst = min(worst, s)
    return worst, len(classes), overlapping


def fit_length(lat, classes, frames, cross, lattice_scale, f):
    """Binary-search the largest flush plain strut length for this cross-section."""
    edge_world = EDGE * lattice_scale
    half_y, half_z = cross[0] / 2, cross[1] / 2
    lo, hi = edge_world * 0.5, edge_world
    for _ in range(22):
        mid = (lo + hi) / 2
        _, _, over = pair_census(lat, classes, frames, edge_world, mid, HEART_SEAT, half_y, half_z)
        if over == 0:
            lo = mid
        else:
            hi = mid
    fitted = math.floor(lo * 100) / 100
    worst, n, over = pair_census(lat, classes, frames, edge_world, fitted, HEART_SEAT, half_y, half_z)
    f.check(over == 0, f"fitted length {fitted:.2f} is flush (zero overlapping pairs)",
            f"cross {cross}, {n} pair classes, worst touch scale {worst:.3f}")
    return fitted


def measure_heart_seat(lat, seat_classes, frames, plain_len, half_y, half_z,
                       edge_world, n_hearts, f, label):
    """Clear radius at a heart: distance from the vertex to the nearest strut OBB
    over every (heart, nearby strut) configuration class - incident struts hold
    back by construction; this also catches any non-incident strut passing near."""
    worst = 1e9
    for i, delta, ha, hb in seat_classes:
        (c, _, ln), _ = class_struts(lat, (i, i, (0,) * 6, ha, hb, ha, hb),
                                     edge_world, plain_len, HEART_SEAT)
        origin = lat.par_of(delta) * edge_world   # strut minus-end relative to the heart
        c = origin + c
        x, y, z = frames[i]
        rel = -c   # heart sits at the origin of this class frame
        q = np.array([abs(float(rel @ x)) - ln / 2,
                      abs(float(rel @ y)) - half_y,
                      abs(float(rel @ z)) - half_z])
        outside = np.maximum(q, 0.0)
        dist = float(np.linalg.norm(outside)) if outside.any() else 0.0
        worst = min(worst, dist)
    f.check(worst >= CRYSTAL_NEED,
            f"{label}: every heart's crystal seat is clear",
            f"min clear radius {worst:.2f}u >= level-5 need {CRYSTAL_NEED}u "
            f"(seat inset {HEART_SEAT}u, {len(seat_classes)} seat classes, "
            f"{n_hearts} hearts in patch)")


def fit_charge(lat, classes, frames, time_leaf, f):
    """Time's strut shrunk uniformly to the shield-octahedron touch scale / 1.1."""
    edge_world = EDGE
    length, cy, cz = time_leaf
    worst = 10.0
    for cls in classes:
        (ca, ia, la), (cb, ib, lb) = class_struts(lat, cls, edge_world, length, HEART_SEAT)
        # Octahedron reach: SHIELD_SCALE x the box half-extent per axis (35's 1.5 x leaf)
        rA = (SHIELD_SCALE * la / 2, SHIELD_SCALE * cy / 2, SHIELD_SCALE * cz / 2)
        rB = (SHIELD_SCALE * lb / 2, SHIELD_SCALE * cy / 2, SHIELD_SCALE * cz / 2)
        s = octa_touch_scale(ca, rA, frames[ia], cb, rB, frames[ib])
        worst = min(worst, s)
    shrink = math.floor(worst / CLEARANCE * 10000) / 10000
    leaf = (math.floor(length * shrink * 100) / 100,
            math.floor(cy * shrink * 100) / 100,
            math.floor(cz * shrink * 100) / 100)
    f.check(shrink < 1.0, "Charge shields NEED the shrink (octahedra of the flush strut fuse)",
            f"octahedron touch scale {worst:.4f} -> uniform shrink x{shrink:.4f}")
    print(f"       Charge leaf: {length} x {cy} x {cz} -> "
          f"{leaf[0]} x {leaf[1]} x {leaf[2]} (aspect preserved)")
    return leaf


# ---------------------------------------------------------------------------
# Authoring
# ---------------------------------------------------------------------------

def write_leaf(name, leaf, lattice_scale, failures):
    path = LIFEFORMS / f"Quasicrystal Flora {name}.asset"
    if not path.exists():
        failures.check(False, f"missing asset {path.name}")
        return
    text = path.read_text()
    new_leaf = f"    LeafSize: {{x: {leaf[0]:g}, y: {leaf[1]:g}, z: {leaf[2]:g}}}"
    text, n = re.subn(r"^    LeafSize: \{[^}]*\}$", new_leaf, text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit(f"{path.name}: LeafSize not found exactly once")
    if lattice_scale != 1.0:
        text, n = re.subn(r"^    LatticeScale: [-\d.]+$",
                          f"    LatticeScale: {lattice_scale:g}", text, count=1, flags=re.M)
        if n != 1:
            raise SystemExit(f"{path.name}: LatticeScale not found exactly once")
    path.write_text(text)
    print(f"  wrote {path.name}: LeafSize {leaf}, LatticeScale {lattice_scale:g}")


def shipped_leaf(name):
    path = LIFEFORMS / f"Quasicrystal Flora {name}.asset"
    if not path.exists():
        return None
    m = re.search(r"^    LeafSize: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}$",
                  path.read_text(), re.M)
    return tuple(float(g) for g in m.groups()) if m else None


def shipped_heart_seat():
    if not BLOCK_PREFAB.exists():
        return None
    m = re.search(r"^  heartSeatInset: ([-\d.]+)$", BLOCK_PREFAB.read_text(), re.M)
    return float(m.group(1)) if m else None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="gate: shipped assets match the fit")
    ap.add_argument("--write", action="store_true", help="author the element assets")
    args = ap.parse_args()

    print("=" * 78)
    print("Quasicrystal strut fit (SAT over the measured patch, per element)")
    print("=" * 78)

    f = Failures()
    t = parse_cs()
    lat = Lattice(t)
    frames = obb_axes(t)

    seat = shipped_heart_seat()
    if seat is not None:
        f.check(abs(seat - HEART_SEAT) < 1e-6,
                "block prefab's heartSeatInset matches this fit's constant",
                f"prefab {seat} vs fit {HEART_SEAT}")

    print("censusing configuration classes over the patch ...")
    pair_classes, seat_classes, n_edges, n_hearts = census_classes(lat, PATCH_VERTS)
    print(f"  {len(pair_classes)} strut-pair classes, {len(seat_classes)} heart-seat classes "
          f"({n_edges} edges, {n_hearts} hearts)")

    fits = {}
    for name, spec in ELEMENTS.items():
        print(f"---- {name} " + "-" * (72 - len(name)))
        length = fit_length(lat, pair_classes, frames, spec["cross"], spec["lattice_scale"], f)
        leaf = (length, spec["cross"][0], spec["cross"][1])
        fits[name] = (leaf, spec["lattice_scale"])
        measure_heart_seat(lat, seat_classes, frames, length, spec["cross"][0] / 2,
                           spec["cross"][1] / 2, EDGE * spec["lattice_scale"], n_hearts, f, name)
        vol = leaf[0] * leaf[1] * leaf[2]
        print(f"       leaf {leaf[0]:g} x {leaf[1]:g} x {leaf[2]:g}  volume {vol:.1f}/strut  "
              f"(~59 struts/plant -> {vol * 59:.0f}/plant)")

    print("---- Charge " + "-" * 66)
    charge_leaf = fit_charge(lat, pair_classes, frames, fits["Time"][0], f)
    fits["Charge"] = (charge_leaf, 1.0)

    print("-" * 78)
    if args.write:
        for name, (leaf, scale) in fits.items():
            write_leaf(name, leaf, scale, f)
    else:
        for name, (leaf, scale) in fits.items():
            shipped = shipped_leaf(name)
            if args.check:
                f.check(shipped is not None and all(abs(a - b) < 1e-6 for a, b in zip(shipped, leaf)),
                        f"shipped {name} leaf matches the fit",
                        f"shipped {shipped} vs fit {leaf}")
            else:
                state = "SHIPPED" if shipped == leaf else f"shipped {shipped}"
                print(f"  {name:<7} fit {leaf}  lattice x{scale:g}  ({state})")

    if f:
        print(f"FAILED {len(f)} check(s):")
        for label in f:
            print(f"  - {label}")
        return 1
    print("fit complete")
    return 0


if __name__ == "__main__":
    sys.exit(main())
