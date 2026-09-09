#!/usr/bin/env python3
"""Rampage's four intensities: the arena model, and the assets it authors.

Rampage is a demolition race in a cell whose prisms are nowhere near nominal size (a cactus
leaf is 5x5x3 = 75 volume, 4.7x the 16 the platform's `count x 16` threshold derivation
assumes). So its phase ladder CANNOT be inherited - every intensity has to author
`*EnterVolume` / `*ExitVolume` from its own arithmetic (Docs/ECOSYSTEM.md 27.4). Doing that by
hand four times is how the four ladders drift apart.

WHAT INTENSITY MEANS HERE (rev 2026-09). Intensity 4 IS the shipped, play-tested arena and
nothing about it moves; intensity 1 is the same arena made BIGGER, DENSER AND EASIER TO HIT,
and 2-3 interpolate. Five axes, all pointing the same way:

    intensity  ->  LESS FLORA + SMALLER PRISMS + SMALLER NUCLEUS + FEWER CRYSTALS
                   + MORE WILDLIFE

  * Flora: 5.00x / 3.67x / 2.33x / 1.00x the authored PLANT COUNT, via this script's SCALES ->
    SpawnProfileSO.FloraPopulationScale. Intensity 1 grows five times intensity 4's forest -
    295 plants and 49,150 prisms against 59 and 9,830 - so there is simply more to shoot,
    everywhere. **This is the one axis that costs COLLIDERS**, and it is gated on two cells
    the game already ships (see assert_collider_budget). The per-plant BUDGET stays 1.0: more
    flora means more PLANTS, not bigger ones.
  * Prisms: 1.60x / 1.40x / 1.20x / 1.00x the authored leaf, via this script's PRISM_SCALES ->
    SpawnProfileSO.FloraPrismScale. A fatter leaf is an easier target for the cone and an
    easier surface to skim, and it is FREE in colliders - the same prisms, larger.
  * Nucleus: prefab scale 500 / 400 / 300 / 200, via each cell config's NucleusPrefab. A core
    size is authored as a config pointing at a resized prefab, never a scene override or a
    localScale tweak on a shared prefab (Docs/ECOSYSTEM.md 13.1).
  * Crystals: 2x players / 1x players / players-1 / exactly 1. The crystal is the Dolphin's
    only blast trigger, so this scales how CONTESTED the discharge is. Authored in the SCENE
    (CrystalManager.IntensityScaled), not here.
  * Wildlife: 1x / 2x / 3x / 4x the authored population, via FAUNA_SCALES ->
    SpawnProfileSO.FaunaPopulationScale.

VOLUME IS THE SPINE, so both forest axes land straight on the phase ladder and the four cells
do not share one. The exponent is PER FAMILY and this is the whole reason the species table
below carries a `family` column: a BranchingFlora lays leafSize on all three axes (volume goes
as s^3), while a PhyllotacticFlora reads only leafSize.x/y and takes its lengths from its own
structure - segment and reach - so its prisms get THICKER, not longer, and volume goes as s^2.
Getting that wrong overstates intensity 1's forest by 1.6x.

Each intensity's volume ladder is the shipped intensity-4 ladder times that intensity's forest
ratio, so every cell keeps the SAME relationship between its forest and its gates (Frenzy at
4.11x the mature forest, Restless at 28.5% of it) and intensity 4 reproduces the play-tested
numbers to the digit. The COUNT ladder is derived from the prism count and therefore moves with
it - and because Frenzy freezes planting AND growth, that gate is what actually bounds the
prism-collider envelope: intensity 1 tops out at 50,000 prisms, not at the ~73,000 its plant
cap would otherwise want.

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

# leaf_vol: volume of ONE live prism - which is its volume for the whole of that plant's
# life, since lifeform LEVELS are retired and nothing scales a leaf after it is laid.
#   Branching flora lay their authored leafSize directly, so theirs are exact products.
#   Phyllotactic flora shape prisms BY ROLE (a stem spans its segment, a leaf spans its
#   reach), so leafSize is NOT the prism volume - these are the effective averages the
#   shipped ladder was built against. They are the one soft number here; see CALIBRATION.
# family: which Flora subclass the species' prefab carries. It decides the PRISM-SCALE
# EXPONENT and nothing else, and it is a measured property of the code rather than a taste:
#   BRANCHING     - BranchingFlora lays LeafSize on all three axes (BranchingFlora.cs, the
#                   `leafScale = LeafSize` read), so a uniform s scales volume by s^3.
#   PHYLLOTACTIC  - PhyllotacticFlora reads LeafSize.x/y as a CROSS-SECTION and takes its two
#                   long axes from its own structure (StemPrismScale spans `segment`,
#                   LeafPrismScale spans `reach`), so s thickens a strut without lengthening
#                   it and volume scales by s^2. Its own header says so: "LENGTHS here are
#                   structural ... does NOT read LeafSize.z".
BRANCHING, PHYLLOTACTIC = 3, 2

# `cap` is that species' authored MaxLivePopulation, reproduced here ONLY so the report can
# print the always-on heart-collider line (one crystal collider per live plant). It is OWNED BY
# `Tools/Build/author_flora_populations.py` and this script never writes it - same rule, and the
# same reason, as FAUNA_SPECIES below.
SPECIES = [
    # name       plants budget  leaf_vol  band(min,max)  family        cap
    ("Cacti",    26,    160,    75.0,     (0.10, 0.95),  BRANCHING,    39),
    ("Spire",    10,    190,    15.0,     (0.30, 0.97),  PHYLLOTACTIC, 15),
    ("Pine",     10,    150,    16.0,     (0.14, 0.90),  BRANCHING,    15),
    ("Rosette",   7,    170,    17.0,     (0.40, 0.96),  PHYLLOTACTIC, 10),
    ("Coral",     6,    180,    10.6,     (0.10, 0.80),  PHYLLOTACTIC,  9),
]

# (FloraPopulationScale, FloraPlantBudgetScale) per intensity, 1-indexed.
#
# HOW MANY PLANTS. Intensity 1 grows FIVE TIMES intensity 4's forest and 2-3 interpolate
# linearly (the ladder is 1 + 4*(4-i)/3, to 2dp - the numbers are written out rather than
# computed so the asset states exactly what the game reads). Intensity 4 stays at 1.0: the
# shipped, play-tested arena, untouched.
#
# **This is the axis that costs COLLIDERS**, and it is the only one that does. Prism size,
# nucleus size and crystal count are all free in that currency; plant count is not, because
# every plant is prisms (LOD-cullable box colliders, disabled at Frenzy) plus ONE always-on
# heart crystal collider that no phase culls. The report prints both lines and
# `assert_collider_budget` fails the build if either leaves the envelope this cell has
# precedent for - see it for the two reference points and why the COUNT backstop, not the cap,
# is what actually bounds the prism side.
#
# The plant BUDGET stays 1.0 at every intensity: "five times the flora" is five times as many
# plants, not five times as big a plant. Growing the budget instead would multiply prisms
# without multiplying the thing the player reads (how much forest there is), and it would
# compound with FloraPrismScale on the same prisms.
SCALES = [(5.00, 1.00), (3.67, 1.00), (2.33, 1.00), (1.00, 1.00)]

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

# SpawnProfileSO.FloraPrismScale per intensity, 1-indexed: the multiplier on every plant's
# authored leafSize. Intensity 4 is EXACTLY 1.0 - the shipped, play-tested arena, untouched -
# and the ladder climbs from there, so the easiest level is the one with the fattest prisms.
#
# A fatter leaf is a bigger target for the Dolphin's cone and a bigger surface to skim, and it
# costs NOTHING in colliders: the prism count is identical at every intensity, the prisms are
# just larger. What it does cost is VOLUME, which is why `thresholds` re-derives a ladder per
# intensity - see the module docstring for the per-family exponent, which is the part that is
# easy to get wrong.
#
# Nothing else in the plant scales: branch step, segment length, whorl reach and the growth
# reservation radius (PhyllotacticFlora.Claim, keyed on `spacing`) are all structural and
# untouched, so a plant still reaches its authored budget and still occupies its own volume.
PRISM_SCALES = [1.60, 1.40, 1.20, 1.00]

# CellConfigDataSO.NucleusPrefab per intensity, 1-indexed: (prefab scale, fileID, guid).
# Intensity 4 keeps HalfNucleus, the nucleus Rampage has always run; 1-3 step up from it.
#
# A cell's core size is authored as a config pointing at a RESIZED PREFAB - never a scene
# override, never a localScale tweak on a shared prefab, never Cell.nucleusScaleMultiplier
# (which is a scene component field and so cannot differ per intensity anyway). The whole
# family shares one root fileID because they were all made by copying; only the guid separates
# them, which is what makes a (fileID, guid) reference unambiguous.
#
# The nucleus is load-bearing three times over in this cell, all of it automatic:
#   * it is the CRYSTAL RESPAWN VOLUME (CrystalManager.GetAnchorlessSpawnRadius), so a bigger
#     core spreads the contested crystals wider. Intensity 1 carries 2x-players crystals, so
#     there are more of them to spread, and RampageObjectiveProvider points at the nearest one
#     - a wider spread costs flight time, never findability.
#   * it CLAMPS the flora planting band from the inside (Flora.ResolvePlantRadius takes
#     max(authored inner, ExpectedNucleusWorldRadius)), so no plant is ever laid inside it at
#     any intensity - which matters because nucleus mass is the territorial claim and is
#     excluded from the fauna targeting grids.
#   * it is the player SPAWN RING's basis where a scene opts in.
# World radius is 0.9798 x the prefab scale (Node2.fbx half-extent), so 490 / 392 / 294 / 196
# against a 1200 membrane - every species keeps a real planting band at every intensity, which
# assert_ladders() checks rather than assumes.
NUCLEI = [
    (500, "7555898194514117247", "e0bc9d35bb0df4f0a1d69a4062d8e01f"),   # Nucleus500
    (400, "7555898194514117247", "b9cf1833fa2493d4b8724ccb6740fb3a"),   # Nucleus
    (300, "7555898194514117247", "a5417c1d3b5798f2864e48d67a393d44"),   # Nucleus300
    (200, "7555898194514117247", "1d3d15a174cc41388679c1487f53bced"),   # HalfNucleus
]

# Node2.fbx's half-extent about the prefab root, the factor Cell.MeasurePrefabRadius applies to
# the authored scale. Docs/CameraMigrationReview.md derives it; 0.9798 x 400 = 392 is the
# nucleus radius quoted throughout the ecology docs.
NUCLEUS_MESH_HALF_EXTENT = 0.9798

# CapsuleMembrane.prefab's authored radius - the number every planting band is a fraction of.
MEMBRANE_RADIUS = 1200.0

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


def forest(intensity: int):
    """(rows, total_plants, total_prisms, total_volume) for a 1-indexed intensity."""
    pop_scale, budget_scale = SCALES[intensity - 1]
    prism_scale = PRISM_SCALES[intensity - 1]
    rows, plants_total, prisms_total, volume_total = [], 0, 0, 0.0

    for name, plants, budget, leaf_vol, _band, family, _cap in SPECIES:
        leaf_vol = CALIBRATION.get(name, leaf_vol)
        # Both scalars floor at 1 in C# (Mathf.Max(1, ...)), so a small species never vanishes.
        n = max(1, round_half_up(plants * pop_scale))
        b = max(1, round_half_up(budget * budget_scale))
        prisms = n * b
        # A prism's volume is its authored leafSize times the CELL's FloraPrismScale, and never
        # anything else: lifeform LEVELS are retired, so nothing multiplies a leaf after it is
        # laid (Docs/ECOSYSTEM.md 40) and the cell's scale is applied once at Flora.Initialize.
        #
        # `family` is the exponent, NOT a constant 3 - see the module docstring. Assuming s^3
        # everywhere overstates intensity 1's forest by 1.6x, which would push its authored
        # Frenzy gate up by the same factor and delay the freeze that bounds its planting.
        leaf_vol *= prism_scale ** family
        volume = prisms * leaf_vol
        rows.append((name, n, b, prisms, volume))
        plants_total += n
        prisms_total += prisms
        volume_total += volume

    return rows, plants_total, prisms_total, volume_total


def nucleus_world_radius(intensity: int) -> float:
    """World radius of this intensity's nucleus, as Cell.MeasurePrefabRadius computes it."""
    return NUCLEI[intensity - 1][0] * NUCLEUS_MESH_HALF_EXTENT


def flora_cap(intensity: int) -> int:
    """Live PLANT ceiling for this intensity - and therefore its always-on crystal count.

    Every live plant carries exactly one heart crystal, whose collider no phase LOD culls, so
    this number IS the always-on collider line for the flora half of the cell
    (Docs/ECOSYSTEM.md 32.7). It is `MaxLivePopulation` through `Cell.ResolveFloraCap`, i.e.
    through the same FloraPopulationScale the seed floors get - the scalar moves floor AND cap
    together, which is the whole reason it bounds a standing population at all.
    """
    pop_scale, _budget = SCALES[intensity - 1]
    return sum(max(1, round_half_up(cap * pop_scale))
               for _n, _p, _b, _lv, _bd, _fam, cap in SPECIES)


# The two collider reference points this cell is held against. Both are SHIPPED elsewhere in
# the game, which is the point - they are precedent, not opinion.
#
#   PRISMS: Scurry's intensity-4 Atlantis, ~69,000 laid prisms, the heaviest authored
#     environment in the game (CLAUDE.md, Cell environments). Rampage's prisms are LOD-cullable
#     boxes exactly as Atlantis' are.
#   CRYSTALS: the freestyle Lattice cell, 1,080 plants at cap - "the largest collider budget of
#     any cell" (Docs/ECOSYSTEM.md 36), and every one of those is an always-on heart collider.
#
# A ladder that stays inside both is inside ground the engine has already been shown to hold.
PRISM_CEILING = 69_000
CRYSTAL_CEILING = 1_080


def assert_collider_budget() -> None:
    """The hard gate. Flora POPULATION is the one axis of this ladder that costs colliders.

    Two separate lines, because they are culled differently and bounded differently:

      * PRISMS are LOD-cullable and are bounded by the cell's own COUNT backstop, not by the
        plant cap - `FrenzyEnter` freezes planting AND growth, and it is derived from the
        seeded forest, so it scales with the ladder automatically. That gate, not the cap, is
        the number to compare against Atlantis: at the cap the forest would want ~1.5x more
        prisms than the gate ever lets it lay.
      * CRYSTALS are NOT cullable - one heart per live plant, always on - so the cap IS the
        line, and it is compared against the heaviest cell the game already ships.
    """
    for i in range(1, 5):
        _rows, plants, prisms, volume = forest(i)
        gate = thresholds(prisms, volume)["FrenzyEnter"]
        crystals = flora_cap(i)
        assert gate <= PRISM_CEILING, (
            f"intensity {i} freezes planting at {gate:,} prisms, past the {PRISM_CEILING:,} "
            f"of Atlantis - the heaviest authored environment the game ships. Lower "
            f"SCALES[{i - 1}]'s population scale.")
        assert crystals <= CRYSTAL_CEILING, (
            f"intensity {i} reaches {crystals:,} live plants, i.e. {crystals:,} ALWAYS-ON "
            f"heart-crystal colliders, past the {CRYSTAL_CEILING:,} of the Lattice cell - the "
            f"largest collider budget in the game. Lower SCALES[{i - 1}]'s population scale.")
        assert prisms < gate, (
            f"intensity {i}'s seeded forest ({prisms:,} prisms) already meets its own count "
            f"backstop ({gate:,}) - it would boot into Frenzy with planting frozen.")


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


# The SHIPPED, play-tested volume ladder. AUTHORED, not derived - and that is a deliberate
# change (2026-08-26), recorded here rather than in a commit message because the next person to
# retune the forest needs it.
#
# It used to be derived: `derived_volume_ladder(volume)` below sat Frenzy just above the
# full-grown forest, and the forest's volume was the seeded prism volume TIMES a level-spread
# multiplier (4.31x on the cactus, 3.21x on the phyllotactics). Lifeform LEVELS are now retired
# outright - a lifeform is its species and its element, and a prism's volume is its authored
# leafSize forever - so that multiplier collapses to exactly 1.0 and the forest this script
# models drops 1,615,853 -> 396,178 volume. Re-deriving would have taken FrenzyEnterVolume
# 1,630,000 -> 400,000, i.e. frozen planting at a quarter of the mass the arena was play-tested
# at. Frenzy arriving LATER is the safe direction, so the shipped numbers are held.
#
# What that costs, stated plainly: the flora alone can no longer reach Frenzy at all (a mature
# forest tops out at 396,178 against a 1,630,000 gate), so in this cell Frenzy is now reachable
# only with player trail mass on top. Restless is unaffected in kind - it still fires at ~28.5%
# of the mature forest (113,000 of 396,178) - so fauna still hunt from early on.
#
# OPEN: re-measure in-editor (FrogletTools > Ecology > Measure Cell Environment Baselines) and
# retune these four numbers against the real Cell.LiveVolume. Docs/ECOSYSTEM.md 27.4, 39.
SHIPPED_VOLUME_LADDER = {
    "RestlessEnterVolume":   113_000,
    "RestlessExitVolume":     81_000,
    "FrenzyEnterVolume":   1_630_000,
    "FrenzyExitVolume":    1_260_000,
}

# The intensity-4 forest volume the SHIPPED ladder above belongs to. Every other intensity's
# ladder is that ladder scaled by its own forest / this number, so intensity 4 comes out
# unchanged and the others keep the same forest-to-gate relationship (see `thresholds`).
#
# It is a CONSTANT rather than a call to forest(4) on purpose: it is the number the shipped
# ladder was authored against, and pinning it means a future forest retune shows up as a
# self-test failure asking for a re-author, instead of silently sliding all four ladders to
# follow the forest and calling that "unchanged".
REFERENCE_FOREST_VOLUME = 396_178.0


def derived_volume_ladder(volume: float) -> dict[str, int]:
    """What THIS forest's volume would ask for, by the original derivation.

    Kept live (and printed, and asserted against) rather than deleted: it is the only thing
    that can tell you the authored ladder above has drifted away from the arena it gates.
    """
    def r(x, unit):
        return int(round(x / unit) * unit)

    frenzy_enter_v = r(volume * 1.009, 10_000)
    frenzy_exit_v = r(frenzy_enter_v * 0.773, 10_000)
    restless_enter_v = r(frenzy_enter_v * 0.0693, 1_000)
    restless_exit_v = r(restless_enter_v * 0.7168, 1_000)
    return {
        "RestlessEnterVolume": restless_enter_v,
        "RestlessExitVolume": restless_exit_v,
        "FrenzyEnterVolume": frenzy_enter_v,
        "FrenzyExitVolume": frenzy_exit_v,
    }


def thresholds(prisms: int, volume: float) -> dict[str, int]:
    """The cell's phase ladder for a forest of this size.

    The COUNT pair is DERIVED and is IDENTICAL at every intensity, because the prism count is:
    intensity scales how big a prism is, never how many there are. That is also the statement
    that the collider budget is flat across the ladder. RestlessEnter/Exit stay at the
    platform's 700/500 everywhere precisely because volume - not count - is what governs.

    The VOLUME pair is the SHIPPED, play-tested intensity-4 ladder scaled by this forest's
    ratio to the intensity-4 forest. Two things that buys:

      * intensity 4 reproduces the play-tested numbers TO THE DIGIT (its ratio is exactly 1),
        so the arena a human already approved is not re-authored by this change;
      * every intensity keeps the SAME relationship between its forest and its gates - Frenzy
        at 4.11x the mature forest, Restless at 28.5% of it - so "the cell is crowded" means
        the same thing at each level even though the absolute volumes differ by 3.9x.

    Scaling rather than re-deriving is deliberate. `derived_volume_ladder` sits Frenzy just
    ABOVE full growth, which is not what this cell ships: the authored gate is ~4x the mature
    forest, held from before lifeform levels were retired, so flora alone never freezes
    planting here and only player trail mass on top can. Re-deriving would quietly change that
    for all four cells at once. See SHIPPED_VOLUME_LADDER.
    """
    frenzy_enter_c = int(math.ceil(prisms * 1.017 / 250.0) * 250)
    ratio = volume / REFERENCE_FOREST_VOLUME

    def scaled(key: str, unit: int) -> int:
        return int(round(SHIPPED_VOLUME_LADDER[key] * ratio / unit) * unit)

    return {
        "RestlessEnter": 700,
        "RestlessExit": 500,
        "FrenzyEnter": frenzy_enter_c,
        "FrenzyExit": int(frenzy_enter_c * 0.8),
        "RestlessEnterVolume": scaled("RestlessEnterVolume", 1_000),
        "RestlessExitVolume": scaled("RestlessExitVolume", 1_000),
        "FrenzyEnterVolume": scaled("FrenzyEnterVolume", 10_000),
        "FrenzyExitVolume": scaled("FrenzyExitVolume", 10_000),
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
    prism_scale = PRISM_SCALES[i - 1]
    pop_scale = SCALES[i - 1][0]
    frenzy_count = t["FrenzyEnter"]
    crystals = flora_cap(i)
    nuc_scale, nucleus_file_id, nucleus_guid = NUCLEI[i - 1]
    desc = (
        f"Demolition arena cell, intensity {i} of 4 - {CROWD_WORDS[i - 1]}: {plants} seeded plants "
        f"totalling ~{prisms} prisms of cacti, spires, pines, rosettes and coral, filling the "
        "volume from just outside the nucleus out to the membrane, across all three domains, "
        "each plant one of the four elemental variations. Intensity 4 is the shipped, "
        "play-tested arena EXACTLY; 1 is that same arena made bigger, denser and easier to "
        f"hit: {pop_scale:g}x the plant count, prisms at {prism_scale:g}x the authored leaf "
        "(bigger is an easier target for the cone and an easier surface to skim), a nucleus at "
        f"prefab scale {nuc_scale} "
        f"(~{int(round(nuc_scale * NUCLEUS_MESH_HALF_EXTENT))}u radius), {fauna_scale:g}x the "
        f"authored wildlife, and - authored in the scene, not here - {CRYSTAL_WORDS[i - 1]}. "
        "The nucleus stays clear (the planting band is clamped outside it) and is the crystals' "
        f"contested ground. The mature forest is ~{int(volume):,} volume, and this cell's VOLUME "
        "thresholds are the play-tested intensity-4 ladder scaled by that - so Frenzy sits the "
        f"same 4.11x above the forest at every level. Collider envelope: up to {frenzy_count:,} "
        f"prisms (LOD-cullable, and the count backstop freezes growth there) plus {crystals} "
        "always-on heart-crystal colliders at the plant cap. Pending an in-editor re-measure; "
        "regenerate with Tools/Build/rampage_intensity.py rather than hand-editing."
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
  NucleusPrefab: {{fileID: {nucleus_file_id}, guid: {nucleus_guid},
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
    prism = PRISM_SCALES[i - 1]
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
  FloraPrismScale: {prism}
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

    # The forest's SHAPE (plants, budgets, prism count) is shared by all four intensities, so
    # print it ONCE from intensity 4 - the shipped, play-tested arena. What differs per
    # intensity is prism SIZE, and that gets its own table below.
    rows, plants, prisms, volume = forest(4)
    t = thresholds(prisms, volume)
    print(f"THE FOREST at intensity 4 (the shipped, play-tested arena): {plants} plants, "
          f"{prisms} prisms, {int(volume):,} volume at full growth")
    for name, n, b, pr, v in rows:
        fam = next(f for nm, _p, _b, _lv, _bd, f, _c in SPECIES if nm == name)
        print(f"    {name:<9}{n:>3} plants x {b:>3} budget = {pr:>5} prisms  {int(v):>10,} vol"
              f"   (prism volume scales as s^{fam})")
    print(f"    ladder    frenzy {t['FrenzyEnterVolume']:,} / {t['FrenzyExitVolume']:,} vol, "
          f"restless {t['RestlessEnterVolume']:,} / {t['RestlessExitVolume']:,} vol, "
          f"{t['FrenzyEnter']:,} count backstop")

    d = derived_volume_ladder(volume)
    print(f"    (the volume pair is the AUTHORED play-tested one - this forest would DERIVE "
          f"frenzy {d['FrenzyEnterVolume']:,} / {d['FrenzyExitVolume']:,}, restless "
          f"{d['RestlessEnterVolume']:,} / {d['RestlessExitVolume']:,};")
    print(f"     holding the shipped numbers puts Frenzy "
          f"{t['FrenzyEnterVolume'] / volume:.2f}x above the mature forest, so flora alone "
          f"never freezes planting)")

    print("\nWHAT INTENSITY CHANGES - bigger and easier at 1, the shipped arena at 4")
    print(f"{'':11}{'flora':>7}{'plants':>8}{'prisms':>9}{'prism':>7}{'nucleus':>10}"
          f"{'fauna':>7}   crystals [authored in the scene]")
    for i in range(1, 5):
        _, plants_i, prisms_i, _volume_i = forest(i)
        nuc = NUCLEI[i - 1][0]
        print(f"intensity {i}{SCALES[i - 1][0]:>6.2f}x{plants_i:>8,}{prisms_i:>9,}"
              f"{PRISM_SCALES[i - 1]:>6.2f}x{nuc:>6} ({int(round(nucleus_world_radius(i))):>3}u)"
              f"{FAUNA_SCALES[i - 1]:>6.1f}x   {CRYSTAL_WORDS[i - 1]}")

    print("\nWHAT THAT LANDS ON - the ladders derived from it")
    print(f"{'':11}{'forest vol':>13}{'frenzy vol':>13}{'restless vol':>14}"
          f"{'frenzy count':>14}{'crystal colliders':>19}")
    for i in range(1, 5):
        _, _, prisms_i, volume_i = forest(i)
        t_i = thresholds(prisms_i, volume_i)
        print(f"intensity {i}{int(volume_i):>13,}{t_i['FrenzyEnterVolume']:>13,}"
              f"{t_i['RestlessEnterVolume']:>14,}{t_i['FrenzyEnter']:>14,}"
              f"{flora_cap(i):>19,}")
    print(f"    COLLIDERS: the prism side is bounded by the count backstop, not by the plant "
          f"cap -\n    Frenzy freezes planting AND growth, so intensity 1 tops out at "
          f"{thresholds(*forest(1)[2:])['FrenzyEnter']:,} prisms against\n    Atlantis' "
          f"{PRISM_CEILING:,}. The crystal side is NOT cullable (one always-on heart per live "
          f"plant),\n    so the cap IS the line: {flora_cap(1):,} at intensity 1 against the "
          f"Lattice cell's {CRYSTAL_CEILING:,}.\n    Both are asserted; population is the ONE "
          f"axis of this ladder that costs colliders at all.")

    print("\nWILDLIFE (seed batch / floor / cap, after FaunaPopulationScale)")
    for i in range(1, 5):
        pops = fauna(i)
        cap_total = sum(c for _, _, _, c in pops)
        detail = "  ".join(f"{name} {ini}/{flr}/{cap}" for name, ini, flr, cap in pops)
        print(f"intensity {i}{FAUNA_SCALES[i - 1]:>6.1f}x   {detail:<38} -> {cap_total:>2} at cap")

    # Regression 1: the emitted intensity-4 ladder must still be the SHIPPED, play-tested one.
    # The COUNT half is derived and never depended on a prism's size, so retiring lifeform
    # levels left it alone; the VOLUME half is now authored, so this pins the authored values.
    _, _, prisms4, volume4 = forest(4)
    t4 = thresholds(prisms4, volume4)
    expected4 = {"RestlessEnter": 700, "RestlessExit": 500, "FrenzyEnter": 10000, "FrenzyExit": 8000,
                 "RestlessEnterVolume": 113000, "RestlessExitVolume": 81000,
                 "FrenzyEnterVolume": 1630000, "FrenzyExitVolume": 1260000}
    assert prisms4 == 9830, f"intensity 4 prism count drifted: {prisms4} != 9830"
    assert t4 == expected4, f"intensity 4 ladder drifted:\n  {t4}\n  {expected4}"

    # Regression 1b: the invariant that REPLACED "the model reproduces the shipped ladder".
    # It no longer does - levels are retired, so the forest is 4.08x lighter than the arena the
    # ladder was play-tested against - and holding the shipped numbers is only safe while the
    # forest fits UNDER them. A future forest retune that grows past the authored gate must
    # fail here rather than silently freezing planting mid-match.
    assert abs(volume4 - 396_178) < 1.0, \
        f"the level-free forest volume drifted: {volume4:,.0f} != 396,178 - re-derive the " \
        f"authored SHIPPED_VOLUME_LADDER against the new forest and re-measure in-editor"
    assert derived_volume_ladder(volume4)["FrenzyEnterVolume"] \
        <= t4["FrenzyEnterVolume"], \
        "the forest now wants a HIGHER Frenzy gate than the authored one - holding the " \
        "shipped ladder would freeze planting before the arena is full. Re-author it."

    # Regression 2: EVERY ladder runs the same way - strictly decreasing from intensity 1 to
    # intensity 4, which is the whole spec ("1 is the easiest, 4 is the shipped arena"). The
    # count ladder is in here now: it used to be flat, because intensity scaled prism SIZE
    # only; five times the FLORA at intensity 1 makes the prism count a ladder too, and with
    # it the collider envelope - see assert_collider_budget, which is the gate that claim now
    # answers to. A volume ladder that did NOT move with the forest would leave the heavier
    # intensities freezing planting early, which is the Docs/ECOSYSTEM.md 4.6 trap this script
    # exists for.
    ladders = [thresholds(*forest(i)[2:]) for i in range(1, 5)]
    for key in ("RestlessEnterVolume", "RestlessExitVolume",
                "FrenzyEnterVolume", "FrenzyExitVolume",
                "FrenzyEnter", "FrenzyExit"):
        seq = [l[key] for l in ladders]
        assert all(a > b for a, b in zip(seq, seq[1:])), \
            f"{key} is not strictly decreasing across the intensity ladder: {seq}"

    for key in ("plants", "prisms"):
        seq = [forest(i)[1 if key == "plants" else 2] for i in range(1, 5)]
        assert all(a > b for a, b in zip(seq, seq[1:])), \
            f"the {key} ladder is not strictly decreasing: {seq}"

    assert forest(1)[1] == 5 * forest(4)[1], \
        f"intensity 1 must grow FIVE TIMES intensity 4's plants, got {forest(1)[1]} vs " \
        f"{forest(4)[1]} - that is the spec, not a tuning value"

    # THE HARD GATE. Population is the one axis here that costs colliders, so it is checked
    # against two cells the game already ships rather than against a number invented here.
    assert_collider_budget()

    # Regression 2b: every intensity keeps the SAME relationship between its forest and its
    # gates - that is the whole reason the ladder is SCALED from intensity 4 rather than
    # re-derived. Rounding to 10k/1k moves the ratio a little, hence the tolerance.
    for i in range(1, 5):
        _, _, prisms_i, volume_i = forest(i)
        t_i = thresholds(prisms_i, volume_i)
        headroom = t_i["FrenzyEnterVolume"] / volume_i
        assert abs(headroom - t4["FrenzyEnterVolume"] / volume4) < 0.02, \
            f"intensity {i} Frenzy sits {headroom:.2f}x above its forest, intensity 4 sits " \
            f"{t4['FrenzyEnterVolume'] / volume4:.2f}x - the ladder no longer means the same " \
            f"thing at every level"
        assert volume_i < t_i["FrenzyEnterVolume"], \
            f"intensity {i}'s mature forest ({volume_i:,.0f}) reaches its own Frenzy gate " \
            f"({t_i['FrenzyEnterVolume']:,}) - planting would freeze before the arena fills"

    # Regression 2c: the two NEW ladders. Both must END on the shipped, play-tested arena -
    # intensity 4 is the thing a human already approved and this change must not move it - and
    # both must climb monotonically from there, because "intensity 1 is the easiest" is the spec.
    assert SCALES[-1] == (1.0, 1.0), \
        "intensity 4 must grow the AUTHORED forest (population and budget scales exactly 1.0)"
    assert all(a[0] > b[0] for a, b in zip(SCALES, SCALES[1:])), \
        f"population ladder is not strictly decreasing: {[x[0] for x in SCALES]}"
    assert all(x[1] == 1.0 for x in SCALES), \
        "the per-plant BUDGET is deliberately flat - 'more flora' is more PLANTS, and growing " \
        "the budget would compound with FloraPrismScale on the very same prisms"
    assert PRISM_SCALES[-1] == 1.0, \
        "intensity 4 must lay the AUTHORED leaf (prism scale exactly 1.0) - it is the arena " \
        "that was play-tested and nothing here may resize it"
    assert all(a > b for a, b in zip(PRISM_SCALES, PRISM_SCALES[1:])), \
        f"prism ladder is not strictly decreasing: {PRISM_SCALES}"
    assert NUCLEI[-1][2] == "1d3d15a174cc41388679c1487f53bced", \
        "intensity 4 must keep HalfNucleus, the nucleus Rampage has always run"
    assert all(a[0] > b[0] for a, b in zip(NUCLEI, NUCLEI[1:])), \
        f"nucleus ladder is not strictly decreasing: {[n[0] for n in NUCLEI]}"
    assert len({n[2] for n in NUCLEI}) == 4, "two intensities share a nucleus prefab guid"

    # Regression 2d: the SHIPPED prefabs really are what this table claims. The transcription
    # from a model to an asset is the step neither the model nor code review can see, so read
    # the assets back - the same rule the lattice table verifiers follow.
    for i, (nuc_scale, _file_id, guid) in enumerate(NUCLEI, start=1):
        matches = [os.path.join(root, f)
                   for root, _dirs, files in os.walk(os.path.join(REPO, "Assets", "_Prefabs"))
                   for f in files if f.endswith(".prefab.meta")
                   and ("guid: " + guid) in open(os.path.join(root, f)).read()]
        assert len(matches) == 1, \
            f"intensity {i}'s nucleus guid {guid} names {len(matches)} prefabs, expected 1"
        body = open(matches[0][: -len(".meta")]).read()
        want = "m_LocalScale: {x: %d, y: %d, z: %d}" % (nuc_scale, nuc_scale, nuc_scale)
        assert want in body, \
            f"intensity {i}'s nucleus prefab {os.path.basename(matches[0])[:-5]} is not " \
            f"scale {nuc_scale} - the model and the shipped asset disagree"

    # Regression 2e: a bigger nucleus eats the planting band from the inside
    # (Flora.ResolvePlantRadius clamps the inner edge to ExpectedNucleusWorldRadius), so no
    # intensity may grow one large enough to collapse a species onto a single shell - that
    # would silently turn a volume-uniform band into one degenerate ring of plants.
    for i in range(1, 5):
        r = nucleus_world_radius(i)
        assert r < MEMBRANE_RADIUS, f"intensity {i}'s nucleus ({r:.0f}u) reaches the membrane"
        for name, _p, _b, _lv, (_inner, outer), _fam, _cap in SPECIES:
            assert r < outer * MEMBRANE_RADIUS, \
                f"intensity {i}'s nucleus ({r:.0f}u) reaches {name}'s outer planting radius " \
                f"({outer * MEMBRANE_RADIUS:.0f}u) - that species would collapse to one shell"

    # Regression 3: the fauna ladder must be monotonically increasing and start at the
    # authored population - "increasing intensity increases the wildlife" is the spec.
    assert FAUNA_SCALES[0] == 1.0, "intensity 1 must be the authored population (scale 1.0)"
    assert all(b > a for a, b in zip(FAUNA_SCALES, FAUNA_SCALES[1:])), \
        f"fauna ladder is not strictly increasing: {FAUNA_SCALES}"

    print("\nself-test OK: intensity 4 reproduces the shipped, play-tested ladder to the digit; "
          "the flora,\nprism, nucleus, volume, count and fauna ladders are all monotonic and "
          "end on it; intensity 1\ngrows exactly 5x intensity 4's plants; every intensity's "
          "forest fits under its own Frenzy gate\nat the same 4.11x; every nucleus guid names a "
          "shipped prefab at the scale this model claims;\nand the collider budget clears both "
          "shipped reference cells.")

    print("\nOPEN - RE-MEASURE THIS LADDER IN-EDITOR (Docs/ECOSYSTEM.md §40, §27.4).\n\n"
          "The intensity-4 volume ladder is AUTHORED, not derived: it is held at the "
          "play-tested\nnumbers rather than re-derived down to 400,000 / 310,000 / 28,000 / "
          "20,000 (lifeform LEVELS\nare retired, so a prism's volume is its authored leafSize "
          "forever and the level-spread\nmultiplier this model used to apply - 4.31x on the "
          "cactus - is gone). Frenzy arriving LATER\nis the safe direction. The cost, unchanged "
          "by this pass: flora alone cannot reach Frenzy in\nthis cell - only player trail mass "
          "on top can - while Restless still fires at ~28.5% of the\nmature forest, so fauna "
          "hunt as before.\n\n"
          "TWO THINGS TO CONFIRM IN THE EDITOR, both new with the prism ladder:\n"
          "  1. The phyllotactic leaf volumes (Spire 15.0, Rosette 17.0, Coral 10.6) are "
          "ESTIMATES -\n     those species shape prisms by ROLE, so there is no authored field "
          "to read. They now\n     carry a per-intensity s^2 factor, so an error in them is "
          "amplified 2.56x at intensity 1.\n     One Cell.LiveVolume measurement per intensity "
          "fixes all four ladders via CALIBRATION.\n"
          "  2. Intensity 1 lays a 1.6x leaf: a cactus at 8 x 8 x 4.8 against the authored "
          "5 x 5 x 3.\n     Branch step, segment length and whorl reach are structural and do "
          "NOT scale, so plants\n     get chunkier rather than larger - confirm that reads as "
          "'easy to hit' and not as 'fused'.\n\n"
          "Measure with FrogletTools > Ecology > Measure Cell Environment Baselines, then "
          "re-author\nSHIPPED_VOLUME_LADDER (and REFERENCE_FOREST_VOLUME with it) against the "
          "real Cell.LiveVolume.")

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
