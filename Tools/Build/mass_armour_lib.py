#!/usr/bin/env python3
"""The numbers every fit of an ARMOURED (Mass) lifeform shares (Docs/ECOSYSTEM.md 65).

    python3 Tools/Build/mass_armour_lib.py --self-test

Mass is armoured: its prisms are shielded, and a shield is the octahedron CIRCUMSCRIBING the prism's box
(OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE = 3 on the HALF-extents, a semi-axis of 1.5 x the prism along each
local axis). Mass has SIZE: a Mass lifeform is grown until its heart's full SWING clears its body - for a creature
its spindle and prisms (Docs/ECOSYSTEM.md 64.8), for a plant its octahedra. One rule, one set of numbers:

  * MASS_AXIS - the lattice law's Mass leaf volume over the neutral one, cube-rooted: how much bigger a Mass plate is
    than the anchor's (Time's) on every axis, its shape kept (FloraElementalForm.MassLeafVolume / NeutralLeafVolume).
  * SWING_RADIUS - how far the Mass crystal's depth prisms reach at the tops of their columns at the one crystal
    size (Docs/ECOSYSTEM.md 64.8: 5.0 u seated, 11.85 u at full reach, 2.37x). Measured off the mesh by the
    content-library dumper; asserted here against the material and the crystal size it was measured at, so a retune
    of either fails every fit instead of quietly invalidating it.
  * SWING_MARGIN - the 5% the Mass fauna were shipped with (x 1.05 on "clears the swing").
  * octahedron_distance - EXACT distance from a point to a shield, for the heart's clearance.
"""
from __future__ import annotations

import itertools
import math
import pathlib
import re
import sys

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[2]
assert (ROOT / "Assets").is_dir(), ROOT
FORM_CS = ROOT / "Assets/_Scripts/Utility/DataContainers/FloraElementalForm.cs"
CRYSTAL_SET_CS = ROOT / "Assets/_Scripts/ScriptableObjects/ElementalCrystalSetSO.cs"
DEPTH_MAT = ROOT / "Assets/_Graphics/Materials/CrystalMaterials/MassDepthPrisms.mat"
OCTAHEDRON_CS = ROOT / "Assets/_Scripts/Utility/OctahedronMeshGenerator.cs"

SWING_RADIUS = 11.85
SWING_MARGIN = 1.05
# What SWING_RADIUS was measured against (Docs/ECOSYSTEM.md 64.8). Change any of these and the swing must be re-measured.
_MEASURED_AT = {"_Reach": 2.5, "_PlateScaleStart": 0.6, "_FaceSplit": 0.25, "_SplitUnit": 0.302105}
_MEASURED_CRYSTAL = 3.55


def _const(path, name):
    m = re.search(rf"{name}\s*=\s*([\d.]+)f", path.read_text(encoding="utf-8-sig"))
    if not m:
        raise SystemExit(f"{name} not found in {path}")
    return float(m.group(1))


def mass_axis():
    """(MassLeafVolume / NeutralLeafVolume)^(1/3) - read from the C#."""
    return (_const(FORM_CS, "MassLeafVolume") / _const(FORM_CS, "NeutralLeafVolume")) ** (1.0 / 3.0)


def shield_semi_axis():
    """A shield's semi-axis as a multiple of the prism's FULL size: CIRCUMSCRIBING_SCALE x 1/2."""
    return _const(OCTAHEDRON_CS, "CIRCUMSCRIBING_SCALE") / 2.0


def swing_need():
    """The clear radius a Mass heart needs: its full swing x the fauna's margin - after proving the swing still
    describes the shipped crystal."""
    text = DEPTH_MAT.read_text()
    for key, want in _MEASURED_AT.items():
        m = re.search(rf"- {key}: ([-\d.]+)", text)
        if not m or abs(float(m.group(1)) - want) > 1e-6:
            raise SystemExit(f"MassDepthPrisms.mat {key} is {m and m.group(1)}, the swing ({SWING_RADIUS} u) was "
                             f"measured at {want}: re-measure it (Docs/ECOSYSTEM.md 64.8) before fitting")
    size = _const(CRYSTAL_SET_CS, "CrystalWorldScale")
    if abs(size - _MEASURED_CRYSTAL) > 1e-6:
        raise SystemExit(f"CrystalWorldScale is {size}, the swing was measured at {_MEASURED_CRYSTAL}: re-measure it")
    return SWING_RADIUS * SWING_MARGIN


def octahedron_distance(p, centre, axes, semi):
    """EXACT distance from point p to the octahedron conv{+-semi_i a_i} (0 inside).

    axes: (3, 3) with COLUMNS the prism's local axes (unit). In the prism frame with |q| folded into the positive
    octant, the body is {y >= 0, sum y_i/s_i <= 1}; the nearest point lies on the facet sum y_i/s_i = 1 restricted to
    some subset F of free coordinates (the rest at 0) - project onto each such face and keep the feasible minimum."""
    q = np.abs(axes.T @ (np.asarray(p, float) - np.asarray(centre, float)))
    s = np.asarray(semi, float)
    if float(np.sum(q / s)) <= 1.0:
        return 0.0
    best = math.inf
    for k in (3, 2, 1):
        for F in itertools.combinations(range(3), k):
            F = list(F)
            w = 1.0 / s[F]
            t = (float(np.dot(w, q[F])) - 1.0) / float(np.dot(w, w))
            yF = q[F] - t * w
            if np.any(yF < -1e-12):
                continue
            y = np.zeros(3)
            y[F] = yF
            best = min(best, float(np.linalg.norm(q - y)))
    return best


def heart_clearance(heart, centres, frames, sizes):
    """The heart's clear radius against every shield: min over prisms of the exact distance. `frames` are (n, 3, 3)
    with COLUMNS the local axes, `sizes` (n, 3) the FULL prism sizes."""
    k = shield_semi_axis()
    d = np.linalg.norm(np.asarray(centres) - heart, axis=1)
    reach = k * np.asarray(sizes).max(axis=1)
    near = np.nonzero(d - reach < 4.0 * SWING_RADIUS)[0]
    if len(near) == 0:
        return math.inf
    return min(octahedron_distance(heart, centres[i], frames[i], k * np.asarray(sizes[i])) for i in near)


def self_test():
    I = np.eye(3)
    # on an axis: a unit octahedron's vertex is at 1, so a point at x=3 is 2 away
    assert abs(octahedron_distance([3, 0, 0], np.zeros(3), I, (1, 1, 1)) - 2.0) < 1e-12
    # along the diagonal the FACE is what is nearest: support 1/sqrt(3)
    p = np.ones(3) * 2 / math.sqrt(3)
    assert abs(octahedron_distance(p, np.zeros(3), I, (1, 1, 1)) - (2 - 1 / math.sqrt(3))) < 1e-9
    # inside
    assert octahedron_distance([0.2, 0.2, 0.2], np.zeros(3), I, (1, 1, 1)) == 0.0
    # an EDGE nearest: (2, 2, -?) folded - point (1, 1, 0) * 2 is nearest the edge midpoint (0.5, 0.5, 0)
    assert abs(octahedron_distance([2, 2, 0], np.zeros(3), I, (1, 1, 1)) - math.dist((2, 2, 0), (0.5, 0.5, 0))) < 1e-9
    # rotated and anisotropic agree with a brute-force sample
    rng = np.random.default_rng(1)
    for _ in range(20):
        A, _ = np.linalg.qr(rng.normal(size=(3, 3)))
        semi = rng.uniform(0.5, 4, 3)
        c = rng.normal(size=3)
        pt = c + rng.normal(size=3) * 6
        u = rng.uniform(size=(20000, 3)); u = u / u.sum(1, keepdims=True)
        sgn = rng.choice((-1, 1), size=(20000, 3))
        surf = c + (u * semi * sgn) @ A.T
        brute = np.linalg.norm(surf - pt, axis=1).min()
        exact = octahedron_distance(pt, c, A, semi)
        if np.sum(np.abs(A.T @ (pt - c)) / semi) <= 1.0:
            assert exact == 0.0
            continue
        assert exact <= brute + 1e-9 and brute - exact < 0.05 * max(1.0, exact), (exact, brute)
    assert abs(mass_axis() - 0.64) < 1e-3   # fitted under Space (Docs/ECOSYSTEM.md 65)
    assert shield_semi_axis() == 1.5
    swing_need()
    print("mass_armour_lib self-test: ok")


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        self_test()
    else:
        print(f"MASS_AXIS {mass_axis():.4f}  shield semi-axis {shield_semi_axis():g} x size  "
              f"heart needs {swing_need():.3f} u ({SWING_RADIUS} x {SWING_MARGIN})")
