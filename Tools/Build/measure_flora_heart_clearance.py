#!/usr/bin/env python3
"""Every GROWTH-RULE plant keeps its heart clear (Docs/ECOSYSTEM.md 65) - measured on the plant the game grows.

    python3 Tools/Build/measure_flora_heart_clearance.py            # the report
    python3 Tools/Build/measure_flora_heart_clearance.py --check    # fail on a heart that is not clear
    python3 Tools/Build/measure_flora_heart_clearance.py --self-test

A plant grows outward from its crystal, and the crystal must be SEEN and must not be swallowed:
  * an ARMOURED (Mass) plant keeps its heart's full SWING clear of every OCTAHEDRON its prisms wear
    (ElementalArmour.HeartSwingClearance: the Mass crystal's depth prisms at 11.85 u, x1.05);
  * every other plant keeps the crystal's SEAT clear of every prism BOX (ElementalArmour.HeartSeatClearance, 2.55 u).

The growth-rule families (PhyllotacticFlora, BranchingFlora) hold it by RULE, not by a fit: the phyllotactic root collar
stands up its axis by the clearance on a stalk limb (Flora.HeartSeatDistance), and a shoot, whorl leaf or branch leaf
that would curl back into the clearance is not grown (Flora.IntrudesOnHeart). Until 2026-10-28 every phyllotactic
collar was centred ON its crystal (clear radius 0 on all 32 configs) - the existing error this gate was written for.
The lattice and Borromean species state their clearance in their own fits (fit_shield_clearance.py,
fit_quasicrystal_strut_sizes.py, measure_borromean_minimal_surface.py) and are reported here, not gated.

The plants come from Tools/Build/flora_heart_harness - the SHIPPED Flora.TryPreviewGrowth run inside Amoebius's content
runtime on each canonical config (the real prefab, its Variant, its element), one seed, so the gate reads what the game
lays rather than a transcription of it. Shield fusion between a plant's OWN prisms is reported beside it: a growth-rule
plant's prisms meet by design at its joints (a stalk is overlapping segments, a whorl is dense around its node), and an
octahedron reaches 3x its box, so an armoured stalk's shields fuse where its boxes touch - as Charge's did when Charge
wore the armour. That is a statement about the species' shape, not a defect this gate can move.
"""
from __future__ import annotations

import argparse
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys

import numpy as np

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import mass_armour_lib as armour  # noqa: E402

LIFEFORMS = ROOT / "Assets/_SO_Assets/Lifeforms"
HARNESS = HERE / "flora_heart_harness"
ARMOUR_CS = ROOT / "Assets/_Scripts/Utility/DataContainers/ElementalArmour.cs"
GATED = ("PhyllotacticFlora", "BranchingFlora")
REPORTED = ("NestedGyroidFlora",)
SPECIES = ("Arbor", "Coral", "Frond", "Lantern", "Reed", "Rosette", "Spire", "Tendril",
           "Branching", "Cacti", "Nerve", "Pine", "Nested Gyroid")
ELEMENTS = ("Charge", "Mass", "Space", "Time")
BUDGET, SEED = 5000, 1


def seat_clearance():
    """ElementalArmour.HeartSeatClearance, read from the C# so the gate and the rule cannot disagree."""
    m = re.search(r"HeartSeatClearance\s*=\s*([\d.]+)f", ARMOUR_CS.read_text())
    if not m:
        raise SystemExit("HeartSeatClearance not found in ElementalArmour.cs")
    return float(m.group(1))


def swing_clearance():
    """ElementalArmour.HeartSwingClearance against mass_armour_lib.swing_need() - one number, two readers."""
    m = re.search(r"HeartSwingClearance\s*=\s*([\d.]+)f\s*\*\s*([\d.]+)f", ARMOUR_CS.read_text())
    if not m:
        raise SystemExit("HeartSwingClearance not found in ElementalArmour.cs")
    cs = float(m.group(1)) * float(m.group(2))
    lib = armour.swing_need()
    if abs(cs - lib) > 1e-4:
        raise SystemExit(f"ElementalArmour.HeartSwingClearance {cs} != mass_armour_lib.swing_need() {lib}")
    return cs


def quat_mat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def box_distance(p, c, R, size):
    q = np.abs(R.T @ (np.asarray(p) - c)) - np.asarray(size) / 2
    return float(np.linalg.norm(np.maximum(q, 0.0)))


def dotnet():
    """A dotnet whose SDK can build the harness (it targets net10.0, as Amoebius's Port does) - DOTNET_ROOT may point at
    the dotnet 8 SDK the other build gates (flora_form_harness, swarm_core_harness) compile with, so it is skipped then."""
    for root in (os.environ.get("DOTNET_ROOT"), "/opt/dotnet", str(pathlib.Path.home() / ".dotnet")):
        if not root or not (pathlib.Path(root) / "dotnet").exists():
            continue
        sdks = [d.name for d in (pathlib.Path(root) / "sdk").glob("*") if d.is_dir()]
        if any(int(s.split(".")[0]) >= 10 for s in sdks if s.split(".")[0].isdigit()):
            return str(pathlib.Path(root) / "dotnet"), root
    exe = shutil.which("dotnet")
    if exe:
        return exe, str(pathlib.Path(exe).resolve().parent)
    raise SystemExit("no dotnet SDK found (DOTNET_ROOT, /opt/dotnet, ~/.dotnet or PATH) - the harness needs one")


def grow(configs, out):
    exe, root = dotnet()
    env = dict(os.environ, DOTNET_ROOT=root, DOTNET_CLI_TELEMETRY_OPTOUT="1")
    build = subprocess.run([exe, "build", str(HARNESS / "harness.csproj"), "-c", "Release", "-v", "q", "-nologo"],
                           capture_output=True, text=True, env=env)
    if build.returncode != 0:
        sys.stderr.write(build.stdout[-4000:] + build.stderr[-4000:])
        raise SystemExit("flora_heart_harness did not build")
    dll = next((HARNESS / "bin/Release").glob("*/harness.dll"))
    run = subprocess.run([exe, str(dll), str(out), str(BUDGET), str(SEED), *map(str, configs)],
                         capture_output=True, text=True, env=env, cwd=ROOT)
    if run.returncode != 0:
        sys.stderr.write(run.stdout[-4000:] + run.stderr[-4000:])
        raise SystemExit("flora_heart_harness failed")
    return json.loads(pathlib.Path(out).read_text())


def measure(plant, seat, swing):
    A = np.asarray(plant["prisms"], float).reshape(-1, 10)
    if len(A) == 0:
        return None
    P, S = A[:, :3], np.abs(A[:, 7:10])
    R = np.array([quat_mat(q) for q in A[:, 3:7]])
    heart = np.asarray(plant["heart"], float)
    if plant["element"] == "Mass":
        frames = np.transpose(R, (0, 1, 2))
        clear = armour.heart_clearance(heart, P, frames, S)
        return clear, swing, "octahedra", len(P)
    clear = min(box_distance(heart, P[i], R[i], S[i]) for i in range(len(P)))
    return clear, seat, "boxes", len(P)


def self_test():
    I = np.eye(3)
    # a 2-unit cube centred 5 u up: its near face is 4 u from the origin
    assert abs(box_distance([0, 0, 0], np.array([0, 5.0, 0]), I, (2, 2, 2)) - 4.0) < 1e-9
    plant = {"element": "Time", "heart": [0, 0, 0], "prisms": [[0, 5, 0, 0, 0, 0, 1, 2, 2, 2]]}
    assert abs(measure(plant, 2.55, 12.44)[0] - 4.0) < 1e-9
    # the same prism armoured: its octahedron reaches 1.5 x 2 = 3 u down, so it is 2 u from the heart
    plant["element"] = "Mass"
    assert abs(measure(plant, 2.55, 12.44)[0] - 2.0) < 1e-9
    # negative control: the shipped seat rule (collar centred on the heart) would be swallowed
    plant = {"element": "Time", "heart": [0, 0, 0], "prisms": [[0, 0, 0, 0, 0, 0, 1, 4, 4, 4]]}
    assert measure(plant, 2.55, 12.44)[0] == 0.0
    assert abs(seat_clearance() - 2.55) < 1e-6 and swing_clearance() > seat_clearance()
    print("measure_flora_heart_clearance self-test: ok")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        self_test()
        return 0
    seat, swing = seat_clearance(), swing_clearance()
    configs = [LIFEFORMS / f"{sp} Flora {e}.asset" for sp in SPECIES for e in ELEMENTS]
    configs = [c for c in configs if c.exists()]
    out = HARNESS / "obj" / "plants.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    plants = grow([c.relative_to(ROOT) for c in configs], out)
    fails = []
    print(f"heart clearance on the plant the game grows (seed {SEED}): armoured hearts need {swing:.2f} u to their "
          f"octahedra, every other heart {seat:.2f} u to its boxes")
    for name, plant in plants.items():
        if plant["cls"] not in GATED + REPORTED:
            continue
        got = measure(plant, seat, swing)
        if got is None:
            fails.append(f"{name}: the preview grew nothing")
            continue
        clear, need, against, n = got
        ok = clear >= need - 1e-3
        gated = plant["cls"] in GATED
        tag = "ok  " if ok else ("FAIL" if gated else "note")
        print(f"  [{tag}] {name:30} {plant['cls']:18} {n:4d} prisms  heart clear {clear:6.2f} u of its {against} "
              f"(needs {need:.2f})")
        if gated and not ok:
            fails.append(f"{name}: heart {clear:.2f} u from its {against}, needs {need:.2f}")
    if fails:
        print("\nFAIL:\n  " + "\n  ".join(fails))
        return 1 if a.check else 0
    print("\nevery growth-rule heart is clear")
    return 0


if __name__ == "__main__":
    sys.exit(main())
