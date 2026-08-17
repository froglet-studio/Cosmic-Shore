#!/usr/bin/env python3
"""Rampage's four intensities: the arena model, and the assets it authors.

Rampage is a demolition race in a cell whose prisms are nowhere near nominal size (a cactus
leaf is 5x5x3 = 75 volume, 4.7x the 16 the platform's `count x 16` threshold derivation
assumes), and the level spread multiplies that again. So its phase ladder CANNOT be inherited
- every intensity has to author `*EnterVolume` / `*ExitVolume` from its own arithmetic
(Docs/ECOSYSTEM.md 27.4). Doing that by hand four times is how the four ladders drift apart.

WHAT INTENSITY MEANS HERE (changed 2026-08-14). It used to thin the FOREST: intensity 1 grew
half the plants of intensity 4. It no longer does - every intensity now grows intensity 4's
arena, prism for prism, so the ladder is:

    intensity  ->  FEWER CRYSTALS  +  MORE WILDLIFE

  * Crystals: 2x players / 1x players / players-1 / exactly 1. The crystal is the Dolphin's
    only blast trigger, so this scales how CONTESTED the discharge is, which is the mode's
    actual difficulty axis. Authored in the SCENE (CrystalManager.IntensityScaled), not here.
  * Wildlife: 1x / 2x / 3x / 4x the authored population, via this script's FAUNA_SCALES ->
    SpawnProfileSO.FaunaPopulationScale.

The forest model below is therefore still the whole reason this script exists - the phase
ladder still has to be derived from real prism volume - it just produces ONE ladder that all
four configs share, and the self-test still pins it to the shipped intensity-4 arena.

This script is the model. It computes the seeded prism count and full-grown volume from the
same numbers the game reads, derives the thresholds from those, and emits the cell configs and
spawn profiles. Re-run it after any tuning change; do not hand-edit the generated assets.

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
#
# FLAT at 1.0/1.0 across all four: the forest is no longer what intensity varies (see the
# module docstring). 1.0 is intensity 4's shipped, play-tested arena, so every intensity is
# now that arena and the collider envelope is the one that was already measured - the ladder
# does not run up from it in this dimension at all. Kept as a per-intensity table rather than
# a constant because it is the natural place for a future forest ladder to come back, and
# because the emitter and the self-test both read it.
SCALES = [(1.00, 1.00), (1.00, 1.00), (1.00, 1.00), (1.00, 1.00)]

# SpawnProfileSO.FaunaPopulationScale per intensity, 1-indexed: intensity N carries N times
# the authored wildlife. Intensity 1 is 1.0 - the exact Blob-authored population Rampage has
# always run at every level - so the ladder is anchored on a shipped point and climbs from
# there, in the CHEAP dimension: a tadpole is one body prism plus its heart and a shark is a
# small spindled body, so 4x the population is tens of prisms against a 9,830-prism forest
# (the flora ladder had to be anchored at its TOP for the opposite reason).
#
# The scalar scales the seed floors AND MaxLivePopulation together, because the cap is what
# actually bounds a standing population - see SpawnProfileSO.FaunaPopulationScale. It gates
# PRODUCTION only; nothing is ever culled to meet it (Docs/ECOSYSTEM.md 0).
FAUNA_SCALES = [1.0, 2.0, 3.0, 4.0]

# The two species Rampage's profiles reference, as authored in the SHARED Blob assets
# (_SO_Assets/Cell Configs/Blob Cell/*). Reproduced here ONLY so the printed report can show
# what each intensity's population works out to - the game reads the assets, not this table.
# They are shared, which is exactly why the ladder is a profile-level scalar and not an edit
# to them: forking two species four ways would be eight assets differing by three ints.
#                 name        initial  floor  cap
FAUNA_SPECIES = [("Tadpole",  4,       4,     6),
                 ("Shark",    1,       1,     2)]

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
    """Expected VOLUME multiplier of one prism across the level band.

    Level L has weight falloff^-(L-1) and linear scale s^(L-1), hence volume s^(3(L-1)).

    WHAT THIS MODELS CHANGED (Docs/ECOSYSTEM.md §33). It used to be the SPAWN-TIME
    distribution: `LifeformLevelSpread` rolled each plant a level at seeding, so the arena
    was born at this multiplier and stayed there. The spread is retired - every plant now
    seeds at level 1 and earns a level per birth - so:

      * the SEEDED arena is this multiplier = 1.0 (a fresh Rampage forest is ~4.3x lighter in
        volume than the number below at s=1.30, f=1.6, which is the shipped Rampage cactus);
      * this figure is now a MATURE-forest CEILING - where a breeding, grazed forest tends
        once its plants have reproduced a few times, weighted the same way because a plant's
        chance of having reached level L falls off similarly.

    That is why the ladder below is left as play-tested rather than re-derived: it now
    describes the arena's settled state instead of its opening state, and booting lighter
    is the safe direction (Frenzy - which freezes planting - arrives later, not sooner).
    It DOES need an in-editor re-measure: see the note printed at the bottom of this tool.
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


def fauna(intensity: int):
    """[(name, initial, floor, cap)] for a 1-indexed intensity, after FaunaPopulationScale.

    Mirrors SpawnProfileSO.ScaleFaunaPopulation exactly: 0 passes through (uncapped stays
    uncapped), everything else rounds half UP and floors at 1.
    """
    scale = FAUNA_SCALES[intensity - 1]

    def s(authored: int) -> int:
        if authored <= 0:
            return authored
        if scale <= 0 or abs(scale - 1.0) < 1e-6:
            return authored
        return max(1, round_half_up(authored * scale))

    return [(name, s(i), s(f), s(c)) for name, i, f, c in FAUNA_SPECIES]


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

CROWD_WORDS = ["a quiet arena you have mostly to yourself",
               "a stirring arena",
               "a busy arena",
               "a teeming arena"]

CRYSTAL_WORDS = ["twice as many crystals as players",
                 "one crystal per player",
                 "one crystal fewer than players",
                 "a single contested crystal"]


def cell_config_yaml(i: int) -> str:
    rows, plants, prisms, volume = forest(i)
    t = thresholds(prisms, volume)
    fauna_scale = FAUNA_SCALES[i - 1]
    desc = (
        f"Demolition arena cell, intensity {i} of 4 - {CROWD_WORDS[i - 1]}: {plants} seeded plants "
        f"totalling ~{prisms} prisms of cacti, spires, pines, rosettes and coral, filling the "
        "volume from just outside the nucleus out to the membrane, across all three domains at "
        "levels 1-5. The FOREST IS THE SAME AT EVERY INTENSITY (this is the shipped, play-tested "
        f"arena); intensity carries {fauna_scale:g}x the authored wildlife and - authored in the "
        f"scene, not here - {CRYSTAL_WORDS[i - 1]}. The nucleus stays clear; it is the crystals' "
        f"contested ground. PhaseThresholds ride the forest's real volume (~{int(volume):,} at "
        "full growth) - regenerate with Tools/Build/rampage_intensity.py rather than hand-editing."
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
    fauna_pop = FAUNA_SCALES[i - 1]
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
  FaunaPopulationScale: {fauna_pop}
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

    # The forest is shared by all four intensities, so print it ONCE - printing four
    # identical tables would hide the fact that they are meant to be identical.
    rows, plants, prisms, volume = forest(4)
    t = thresholds(prisms, volume)
    print(f"THE FOREST (identical at every intensity): {plants} plants, {prisms} prisms, "
          f"{int(volume):,} volume at full growth")
    for name, n, b, p, v in rows:
        print(f"    {name:<9}{n:>3} plants x {b:>3} budget = {p:>5} prisms  {int(v):>10,} vol")
    print(f"    ladder    frenzy {t['FrenzyEnterVolume']:,} / {t['FrenzyExitVolume']:,} vol, "
          f"restless {t['RestlessEnterVolume']:,} / {t['RestlessExitVolume']:,} vol, "
          f"{t['FrenzyEnter']:,} count backstop")

    print("\nWHAT INTENSITY ACTUALLY CHANGES")
    print(f"{'':10}{'fauna':>7}   wildlife (seed batch / floor / cap)"
          f"{'':17}crystals [authored in the scene]")
    for i in range(1, 5):
        pops = fauna(i)
        cap_total = sum(c for _, _, _, c in pops)
        detail = "  ".join(f"{name} {ini}/{flr}/{cap}" for name, ini, flr, cap in pops)
        print(f"intensity {i}{FAUNA_SCALES[i - 1]:>6.1f}x   {detail:<38} -> {cap_total:>2} at cap"
              f"   {CRYSTAL_WORDS[i - 1]}")

    # Regression 1: the model must reproduce the SHIPPED intensity-4 ladder exactly. If this
    # ever fails, the model drifted from the arena that was actually play-tested.
    _, _, prisms4, volume4 = forest(4)
    t4 = thresholds(prisms4, volume4)
    expected4 = {"RestlessEnter": 700, "RestlessExit": 500, "FrenzyEnter": 10000, "FrenzyExit": 8000,
                 "RestlessEnterVolume": 113000, "RestlessExitVolume": 81000,
                 "FrenzyEnterVolume": 1630000, "FrenzyExitVolume": 1260000}
    assert prisms4 == 9830, f"intensity 4 prism count drifted: {prisms4} != 9830"
    assert t4 == expected4, f"intensity 4 ladder drifted:\n  {t4}\n  {expected4}"

    # Regression 2: the forest is now FLAT, so all four intensities must land on that same
    # play-tested ladder. This is the assert that catches a half-finished edit - one that
    # changes SCALES for some intensities and not others, leaving a cell whose thresholds no
    # longer match the forest it actually grows.
    for i in range(1, 4):
        _, _, prisms_i, volume_i = forest(i)
        assert prisms_i == prisms4, f"intensity {i} forest drifted from 4: {prisms_i} != {prisms4}"
        assert thresholds(prisms_i, volume_i) == t4, f"intensity {i} ladder drifted from 4"

    # Regression 3: the fauna ladder must be monotonically increasing and start at the
    # authored population - "increasing intensity increases the wildlife" is the spec.
    assert FAUNA_SCALES[0] == 1.0, "intensity 1 must be the authored population (scale 1.0)"
    assert all(b > a for a, b in zip(FAUNA_SCALES, FAUNA_SCALES[1:])), \
        f"fauna ladder is not strictly increasing: {FAUNA_SCALES}"

    print("\nself-test OK: all four intensities reproduce the shipped, play-tested intensity-4 "
          "ladder, and the fauna ladder climbs from the authored population")

    print("\nOPEN - RE-MEASURE THIS LADDER IN-EDITOR (Docs/ECOSYSTEM.md §33). The spawn-time "
          "level spread\nis retired: every plant now seeds at level 1 and earns a level per "
          "birth, so the volumes\nabove are the MATURE forest, and a freshly-seeded arena is "
          "~4.3x lighter (the cactus runs\ns=1.30, f=1.6). Booting lighter is the safe "
          "direction - Frenzy, which freezes planting,\narrives later, not sooner - so the "
          "ladder is deliberately left as play-tested. Confirm with\nFrogletTools > Ecology > "
          "Measure Cell Environment Baselines and retune Restless if the arena\nnow reads Calm "
          "for too long after the whistle.")

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
