#!/usr/bin/env python3
"""Rampage's four intensities: the forest model, and the assets it authors.

Rampage is a demolition race in a cell whose prisms are nowhere near nominal size (a cactus
leaf is 5x5x3 = 75 volume, 4.7x the 16 the platform's `count x 16` threshold derivation
assumes), and the level spread multiplies that again. So its phase ladder CANNOT be inherited
- every intensity has to author `*EnterVolume` / `*ExitVolume` from its own arithmetic
(Docs/ECOSYSTEM.md 27.4). Doing that by hand four times is how the four ladders drift apart.

This script is the model. It computes each intensity's seeded prism count and full-grown
volume from the same numbers the game reads, derives the thresholds from those, and emits the
cell configs and spawn profiles. Re-run it after any tuning change; do not hand-edit the
generated assets.

    python3 Tools/Build/rampage_intensity.py            # print the table, self-test
    python3 Tools/Build/rampage_intensity.py --write    # (re)generate the 8 assets
    python3 Tools/Build/rampage_intensity.py --check    # fail if the assets are stale

Deliberately a plain Python asset generator and not a FrogletTools [MenuItem]: it needs no
editor, and it must be runnable in CI. Docs/TOOLING.md's ship contract still applies in
spirit - its OUTPUT is the deliverable and has to be on the branch, not just this file.
"""

from __future__ import annotations

import argparse
import math
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CELL_DIR = os.path.join(REPO, "Assets", "_SO_Assets", "Cell Configs", "Rampage Cell")

# ---------------------------------------------------------------------------------------
# The forest
# ---------------------------------------------------------------------------------------

# leaf_vol: volume of ONE live prism at level 1.
#   Branching flora lay their authored leafSize directly, so theirs are exact products.
#   Phyllotactic flora shape prisms BY ROLE (a stem spans its segment, a leaf spans its
#   reach), so leafSize is NOT the prism volume - these are the effective averages the
#   shipped ladder was built against. They are the one soft number here; see CALIBRATION.
SPECIES = [
    # name       plants budget  leaf_vol  leaf_scale_per_level  band(min,max)
    ("Cacti",    26,    160,    75.0,     1.30,                 (0.10, 0.95)),
    ("Spire",    10,    190,    15.0,     1.25,                 (0.30, 0.97)),
    ("Pine",     10,    150,    16.0,     1.25,                 (0.14, 0.90)),
    ("Rosette",   7,    170,    17.0,     1.25,                 (0.40, 0.96)),
    ("Coral",     6,    180,    10.6,     1.25,                 (0.10, 0.80)),
]

RARITY_FALLOFF = 1.6   # Levels {1..5} on every Rampage flora config
MIN_LEVEL, MAX_LEVEL = 1, 5

# (FloraPopulationScale, FloraPlantBudgetScale) per intensity, 1-indexed.
# Intensity 4 is 1.0/1.0 BY DESIGN: it is the arena that already ships and has been played,
# so the ladder runs DOWN from it. Rampage is already documented at 2.8x the Blob collider
# envelope as deliberate headroom - scaling UP from there would land the top intensity
# somewhere nobody has measured.
SCALES = [(0.50, 0.70), (0.70, 0.80), (0.85, 0.90), (1.00, 1.00)]

# Corrections to the phyllotactic leaf_vol estimates, filled in from an in-editor
# Cell.LiveVolume measurement. All four ladders move together, so one measurement
# recalibrates the whole set - which is the entire reason this is a script.
CALIBRATION: dict[str, float] = {}


def round_half_up(x: float) -> int:
    """Mirror the C# `Mathf.FloorToInt(x + 0.5f)` used by both density scalars.

    NOT Python's round() and NOT Mathf.RoundToInt(): both are banker's rounding, which would
    send an authored 10 x 0.85 to 8 on one species and 9 on the next for no stated reason.
    """
    return math.floor(x + 0.5)


def level_volume_multiplier(scale_per_level: float, falloff: float) -> float:
    """Expected VOLUME multiplier of one prism under the level spread.

    Level L has weight falloff^-(L-1) and linear scale s^(L-1), hence volume s^(3(L-1)).
    """
    weights = [falloff ** -(l - MIN_LEVEL) for l in range(MIN_LEVEL, MAX_LEVEL + 1)]
    vols = [scale_per_level ** (3 * (l - MIN_LEVEL)) for l in range(MIN_LEVEL, MAX_LEVEL + 1)]
    return sum(w * v for w, v in zip(weights, vols)) / sum(weights)


def forest(intensity: int):
    """(rows, total_plants, total_prisms, total_volume) for a 1-indexed intensity."""
    pop_scale, budget_scale = SCALES[intensity - 1]
    rows, plants_total, prisms_total, volume_total = [], 0, 0, 0.0

    for name, plants, budget, leaf_vol, leaf_scale, _band in SPECIES:
        leaf_vol = CALIBRATION.get(name, leaf_vol)
        # Both scalars floor at 1 in C# (Mathf.Max(1, ...)), so a small species never vanishes.
        n = max(1, round_half_up(plants * pop_scale))
        b = max(1, round_half_up(budget * budget_scale))
        prisms = n * b
        volume = prisms * leaf_vol * level_volume_multiplier(leaf_scale, RARITY_FALLOFF)
        rows.append((name, n, b, prisms, volume))
        plants_total += n
        prisms_total += prisms
        volume_total += volume

    return rows, plants_total, prisms_total, volume_total


def thresholds(prisms: int, volume: float) -> dict[str, int]:
    """The cell's phase ladder for a forest of this size.

    VOLUME is the spine: Frenzy sits just above the full-grown forest so planting and growth
    freeze exactly when the arena is full (a GROWTH gate - nothing is ever culled), and the
    exit band reopens it once roughly a quarter has been destroyed. Restless sits low so fauna
    hunt from early on, at the same proportion Blob uses.

    The COUNT pair is only the perf backstop, and RestlessEnter/Exit stay at the platform's
    700/500 at every intensity precisely because volume - not count - is what governs.
    """
    def r(x, unit):
        return int(round(x / unit) * unit)

    frenzy_enter_v = r(volume * 1.009, 10_000)
    frenzy_exit_v = r(frenzy_enter_v * 0.773, 10_000)
    restless_enter_v = r(frenzy_enter_v * 0.0693, 1_000)
    restless_exit_v = r(restless_enter_v * 0.7168, 1_000)
    frenzy_enter_c = int(math.ceil(prisms * 1.017 / 250.0) * 250)

    return {
        "RestlessEnter": 700,
        "RestlessExit": 500,
        "FrenzyEnter": frenzy_enter_c,
        "FrenzyExit": int(frenzy_enter_c * 0.8),
        "RestlessEnterVolume": restless_enter_v,
        "RestlessExitVolume": restless_exit_v,
        "FrenzyEnterVolume": frenzy_enter_v,
        "FrenzyExitVolume": frenzy_exit_v,
    }


# ---------------------------------------------------------------------------------------
# Asset emission
# ---------------------------------------------------------------------------------------

CELL_SCRIPT = "01f934d50526431a9392a6ceca1dc33d"
PROFILE_SCRIPT = "e8d8aa5d835249798a256e18f2f7d912"

# Intensity 4 keeps the guids the shipped assets already have, so every existing reference
# (and the scene) survives the rename. 1-3 are new.
CELL_GUIDS = ["fc20698b2b983f9e1e9c20733ea92760",
              "398abbfc433510307e154b76aa4191b4",
              "789e13b3381f614d997ba4b7a830ff22",
              "c6959b0e548d4f26bdde820ca48ac26e"]
PROFILE_GUIDS = ["6c4ce02092bf0e13a8320e0e3b5b669c",
                 "24608d019659e510a69b1e7f9c4cbe98",
                 "764e6444e039928d9dd0cc9decf7e444",
                 "99aeb55c27514418a22722ba477c0a82"]

FLORA_GUIDS = ["f9232fe099904e69b63d12f1b0e28717",   # Cacti
               "5b18cd2b2ac647e48b78dd3e8e155f02",   # Spire
               "77428610f484433586d594663b70385a",   # Pine
               "8514fbd281c347f0bea06cbc1db3a9c4",   # Rosette
               "c189b76be353421a9d7efae8ecb6cee0"]   # Coral
FAUNA_GUIDS = ["178e4d83e2fd4a4bae1ab253d7766ea7",   # Blob tadpole
               "fb217959401746e1b09cac81ffce665b"]   # Blob shark

ICON_GUID = "6aa1c06e11b265744a5f9fa8858ac72a"
MEMBRANE = ("346633111830028674", "6e330f85972faf843b8a128e7166f7b5")   # CapsuleMembrane r=1200
NUCLEUS = ("7555898194514117247", "1d3d15a174cc41388679c1487f53bced")   # HalfNucleus  r=100
CYTOPLASM = ("639495419069806261", "9cacd903fcf4643459f5f14ac811bb20")  # SnowChanger

META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

DENSITY_WORDS = ["a sparse grove you can cross at speed",
                 "a stocked wood",
                 "a thick forest",
                 "a dense thicket"]


def cell_config_yaml(i: int) -> str:
    rows, plants, prisms, volume = forest(i)
    t = thresholds(prisms, volume)
    desc = (
        f"Demolition arena cell, intensity {i} of 4 - {DENSITY_WORDS[i - 1]}: {plants} seeded "
        f"plants totalling ~{prisms} prisms of cacti, spires, pines, rosettes and coral, filling "
        "the volume from just outside the nucleus out to the membrane, across all three domains "
        "at levels 1-5. Every intensity shares one membrane, one nucleus and one crystal - only "
        "the forest changes. The nucleus stays clear; it is the crystal's contested ground. "
        "PhaseThresholds ride THIS intensity's own forest volume "
        f"(~{int(volume):,} at full growth) - regenerate with Tools/Build/rampage_intensity.py "
        "rather than hand-editing."
    )
    wrapped = _wrap_yaml_scalar(desc)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {CELL_SCRIPT}, type: 3}}
  m_Name: Rampage Cell Config {i}
  m_EditorClassIdentifier:
  CellName: Rampage
  Description: {wrapped}
  Icon: {{fileID: 21300000, guid: {ICON_GUID}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE[0]}, guid: {MEMBRANE[1]},
    type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS[0]}, guid: {NUCLEUS[1]},
    type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM[0]}, guid: {CYTOPLASM[1]},
    type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {PROFILE_GUIDS[i - 1]}, type: 2}}
  PhaseThresholds:
    RestlessEnter: {t['RestlessEnter']}
    RestlessExit: {t['RestlessExit']}
    FrenzyEnter: {t['FrenzyEnter']}
    FrenzyExit: {t['FrenzyExit']}
    RestlessEnterVolume: {t['RestlessEnterVolume']}
    RestlessExitVolume: {t['RestlessExitVolume']}
    FrenzyEnterVolume: {t['FrenzyEnterVolume']}
    FrenzyExitVolume: {t['FrenzyExitVolume']}
"""


def spawn_profile_yaml(i: int) -> str:
    pop, budget = SCALES[i - 1]
    floras = "\n".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}" for g in FLORA_GUIDS)
    faunas = "\n".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}" for g in FAUNA_GUIDS)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {PROFILE_SCRIPT}, type: 3}}
  m_Name: Rampage Spawn Profile {i}
  m_EditorClassIdentifier:
  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 12000
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  FloraPopulationScale: {pop}
  FloraPlantBudgetScale: {budget}
  SupportedFloras:
{floras}
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 10
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 30
  FaunaFoodFloor: 5
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 1
  SupportedFaunas:
{faunas}
"""


def _wrap_yaml_scalar(text: str, width: int = 88, indent: str = "    ") -> str:
    """Unity's own folded-scalar style: first line inline, continuations indented."""
    words, lines, cur = text.split(), [], ""
    for w in words:
        cand = f"{cur} {w}".strip()
        if len(cand) > width and cur:
            lines.append(cur)
            cur = w
        else:
            cur = cand
    if cur:
        lines.append(cur)
    return ("\n" + indent).join(lines)


def emit(write: bool) -> list[str]:
    stale = []
    for i in range(1, 5):
        for path, body, guid in (
            (os.path.join(CELL_DIR, f"Rampage Cell Config {i}.asset"), cell_config_yaml(i), CELL_GUIDS[i - 1]),
            (os.path.join(CELL_DIR, f"Rampage Spawn Profile {i}.asset"), spawn_profile_yaml(i), PROFILE_GUIDS[i - 1]),
        ):
            existing = open(path).read() if os.path.exists(path) else None
            if existing != body:
                stale.append(os.path.relpath(path, REPO))
                if write:
                    with open(path, "w") as f:
                        f.write(body)
            if write:
                with open(path + ".meta", "w") as f:
                    f.write(META.format(guid=guid))
    return stale


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true", help="(re)generate the 8 assets")
    ap.add_argument("--check", action="store_true", help="exit 1 if any asset is stale")
    args = ap.parse_args()

    print(f"{'':10}{'plants':>7}{'prisms':>8}{'volume':>12}   thresholds (frenzy enter/exit volume, count)")
    for i in range(1, 5):
        rows, plants, prisms, volume = forest(i)
        t = thresholds(prisms, volume)
        print(f"intensity {i}{plants:>7}{prisms:>8}{int(volume):>12,}   "
              f"{t['FrenzyEnterVolume']:,} / {t['FrenzyExitVolume']:,} / {t['FrenzyEnter']:,}")
        for name, n, b, p, v in rows:
            print(f"    {name:<9}{n:>3} plants x {b:>3} budget = {p:>5} prisms  {int(v):>10,} vol")

    # Regression: the model must reproduce the SHIPPED intensity-4 ladder exactly. If this
    # ever fails, the model drifted from the arena that was actually play-tested.
    _, _, prisms4, volume4 = forest(4)
    t4 = thresholds(prisms4, volume4)
    expected4 = {"RestlessEnter": 700, "RestlessExit": 500, "FrenzyEnter": 10000, "FrenzyExit": 8000,
                 "RestlessEnterVolume": 113000, "RestlessExitVolume": 81000,
                 "FrenzyEnterVolume": 1630000, "FrenzyExitVolume": 1260000}
    assert prisms4 == 9830, f"intensity 4 prism count drifted: {prisms4} != 9830"
    assert t4 == expected4, f"intensity 4 ladder drifted:\n  {t4}\n  {expected4}"
    print("\nself-test OK: intensity 4 reproduces the shipped, play-tested ladder exactly")

    stale = emit(write=args.write)
    if args.write:
        print(f"wrote 8 assets + 8 meta files to {os.path.relpath(CELL_DIR, REPO)}")
    elif args.check and stale:
        print("\nSTALE (re-run with --write):")
        for s in stale:
            print("  " + s)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
