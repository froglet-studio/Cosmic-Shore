#!/usr/bin/env python3
"""
Authors every serialized asset the Switchback game mode needs (GameModes.Switchback = 45).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_switchback_assets.py [--check]

--check validates without writing (CI / pre-commit use).

WHAT THIS MODE IS. Switchback is the Dolphin-only gate race: a course of randomly placed and
randomly ORIENTED switch rings scattered through the cell, flown in ORDER by every pilot, and
the first DOMAIN whose LEAD RUNNER threads the last gate wins. See
Assets/_Scripts/Controller/Arcade/SWITCHBACK.md.

THE ARENA IS A REFERENCE, NOT A FORK. The scene is cloned from MinigameRampage (which already
authors an all-Dolphin AI roster and a cell-relative spawn ring) and then pointed at the SKIM
RACE cell - the one config in the project whose own description is "barren race cell with a
trail-grazing food web". That is "the Cell owns the environment" applied to a whole world: the
gates are the content, so the arena wants to be legible rather than dense, and a cell that
already ships a race is the honest choice over a new one.

TWO THINGS THE CLONE CHANGES BEYOND THE USUAL CONTROLLER/MONITOR/RULE SWAP, and both are
load-bearing rather than cosmetic:

  1. The CELL becomes the single Skim Race config (cellTypeChoiceOptions Random over a one-entry
     list, exactly as MinigameSkimRace wires it). Rampage's four cactus-forest configs are an
     IntensityWise ladder, and Switchback's intensity is the COURSE, not the arena - keeping
     them would have intensity mean two contradictory things at once.

  2. The spawn formation becomes EQUATORIAL RING. This is the mode's fairness rule: the course's
     first gate sits on that ring's POLE, so every pilot is exactly sqrt(spawnRadius^2 + d^2)
     from it. Under the donor's Symmetric (tetrahedral) formation there is no such point and
     whoever spawned nearest gate 1 starts the race ahead.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "SwitchbackController":       guid("script/SwitchbackController"),
    "SwitchbackCourse":           guid("script/SwitchbackCourse"),
    "SwitchbackGateRing":         guid("script/SwitchbackGateRing"),
    "SwitchbackObjectiveProvider": guid("script/SwitchbackObjectiveProvider"),
    "SwitchThreadScoring":        guid("script/SwitchThreadScoring"),
    "SwitchbackScoringRuleSO":    guid("script/SwitchbackScoringRuleSO"),
    "SwitchbackGateTurnMonitor":  guid("script/SwitchbackGateTurnMonitor"),
    "SwitchbackCourseTests":      guid("script/SwitchbackCourseTests"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameSwitchback":   guid("asset/ArcadeGameSwitchback"),
    "SwitchbackScoringRule":  guid("asset/SwitchbackScoringRule"),
    "MinigameSwitchback.unity": guid("asset/MinigameSwitchback.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":           "fe040efad3307fb449b6b72ad15362da",
    # donor scene wiring to swap out
    "RampageController":       "e11ff862e6844a89a951292673243625",
    "RampagePrismTurnMonitor": "694b571734fe4a55a57f6cc672c7fcc2",
    "RampageScoringRule":      "7d1bfbd4091c4a12a12c730553bf293a",
    # the four cactus-forest configs the clone drops
    "RampageCell1":            "fc20698b2b983f9e1e9c20733ea92760",
    "RampageCell2":            "398abbfc433510307e154b76aa4191b4",
    "RampageCell3":            "789e13b3381f614d997ba4b7a830ff22",
    "RampageCell4":            "c6959b0e548d4f26bdde820ca48ac26e",
    # ...for the one it flies in
    "SkimRaceCellConfig":      "52de45c2fab54ea0a35bc37a311d80e6",
    # shared content
    "Vessel_Dolphin":          "c0f30e9f09616874780edc0a375ce686",
    # arcade card art - shared with the other pure-race cards
    "IconActive":              "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":            "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":          "587d2203114c8004c9985d0112c89585",
}

# ── Tuning ───────────────────────────────────────────────────────────────────

# Gates in the course. This is BOTH the end-game target and the number of rings laid - the
# controller and the turn monitor read the same EndConditionOverridesSO getter - so a course can
# never be a different length from the thing counting it. 20 gates at the shipped leg lengths is
# ~8.9-10.2k units of flying; at a Dolphin's 68 u/s cruise and 347 u/s boost that is a 2-3 minute
# race with room for misses. Kept in sync with EndConditionOverridesSO.DefaultSwitchbackGateTarget.
GATE_TARGET = 20

# The comeback strength, and it is a FUNCTION OF THE TARGET - `bonusLevels = deficit x rate` - so
# a rate only means anything next to the scale of deficits the mode produces. This is the trap
# DOGFIGHT.md, BENDS.md and WILDLIFE_LIBERATION.md have now each recorded independently, so the
# assert below fails the build rather than trusting the number.
#
# At 0.5: five gates behind (a quarter of the course) buys 2.5 element levels, ten behind buys 5.
# It matters here because the deficit is measured on the LEAD RUNNER - a trailing domain is
# genuinely behind on the same course, with no teammate's progress to hide in.
COMEBACK_RATE = 0.5

# The donor's Cell block, and the one the clone replaces it with. Rampage runs four cactus-forest
# configs as an IntensityWise ladder (cellTypeChoiceOptions: 1); Switchback runs ONE barren race
# cell and spends intensity on the course instead (cellTypeChoiceOptions: 0, the same wiring
# MinigameSkimRace uses).
DONOR_CELL_BLOCK = """  CellConfigs:
  - {{fileID: 11400000, guid: {c1}, type: 2}}
  - {{fileID: 11400000, guid: {c2}, type: 2}}
  - {{fileID: 11400000, guid: {c3}, type: 2}}
  - {{fileID: 11400000, guid: {c4}, type: 2}}
  cellTypeChoiceOptions: 1
""".format(c1=EXISTING["RampageCell1"], c2=EXISTING["RampageCell2"],
           c3=EXISTING["RampageCell3"], c4=EXISTING["RampageCell4"])

NEW_CELL_BLOCK = """  CellConfigs:
  - {{fileID: 11400000, guid: {c}, type: 2}}
  cellTypeChoiceOptions: 0
""".format(c=EXISTING["SkimRaceCellConfig"])

# The donor's CRYSTAL supply, and the flat one that replaces it. This is the same "intensity is
# the COURSE, not the arena" rule as the Cell block above, and it is the half that is easy to
# miss: a crystal is the Dolphin's only blast trigger, so Rampage spends intensity on crystal
# SCARCITY (2xplayers / players / players-1 / 1 - an 8x swing at four pilots). Inheriting that
# would make intensity mean two contradictory things at once here, and would make the mode's
# whole interference layer ~8x rarer at exactly the level the course gets tighter.
#
# PlayerCountPlusExtra with +1 instead: constant at every intensity, one spare so a crystal is
# available without being a scramble. The four-row ladder is flattened to the same answer rather
# than deleted, so flipping the mode back to IntensityScaled some day still says "the arena does
# not change" instead of silently restoring Rampage's tuning.
DONOR_CRYSTAL_BLOCK = """  crystalCountMode: 2
  fixedCrystalCount: 1
  extraCrystalsToSpawnBeyondPlayerCount: 0
  crystalCountByIntensity:
  - CrystalsPerPlayer: 2
    ExtraCrystals: 0
  - CrystalsPerPlayer: 1
    ExtraCrystals: 0
  - CrystalsPerPlayer: 1
    ExtraCrystals: -1
  - CrystalsPerPlayer: 0
    ExtraCrystals: 1
"""

NEW_CRYSTAL_BLOCK = """  crystalCountMode: 1
  fixedCrystalCount: 1
  extraCrystalsToSpawnBeyondPlayerCount: 1
  crystalCountByIntensity:
  - CrystalsPerPlayer: 1
    ExtraCrystals: 1
  - CrystalsPerPlayer: 1
    ExtraCrystals: 1
  - CrystalsPerPlayer: 1
    ExtraCrystals: 1
  - CrystalsPerPlayer: 1
    ExtraCrystals: 1
"""

_HEADER_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __GUID__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def HEADER_FOR(script_guid: str, name: str) -> str:
    return _HEADER_TMPL.replace("__GUID__", script_guid).replace("__NAME__", name)


def meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files = {}


def emit(rel, content):
    files[rel] = content


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


# ── 1. .cs.meta for the new scripts ──────────────────────────────────────────
SCRIPT_PATHS = {
    "SwitchbackController":        "Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs",
    "SwitchbackCourse":            "Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackCourse.cs",
    "SwitchbackGateRing":          "Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackGateRing.cs",
    "SwitchbackObjectiveProvider": "Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackObjectiveProvider.cs",
    "SwitchThreadScoring":         "Assets/_Scripts/Controller/Arcade/Switchback/SwitchThreadScoring.cs",
    "SwitchbackScoringRuleSO":     "Assets/_Scripts/Controller/Arcade/Scoring/SwitchbackScoringRuleSO.cs",
    "SwitchbackGateTurnMonitor":   "Assets/_Scripts/Controller/Arcade/TurnMonitors/SwitchbackGateTurnMonitor.cs",
    "SwitchbackCourseTests":       "Assets/_Scripts/Tests/Editor/SwitchbackCourseTests.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))

# The mode doc is an ASSET too - without a .meta Unity mints a fresh GUID on every machine and
# validate_project.py flags it.
emit("Assets/_Scripts/Controller/Arcade/SWITCHBACK.md.meta",
     f"fileFormatVersion: 2\nguid: {guid('doc/SWITCHBACK.md')}\nTextScriptImporter:\n"
     f"  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

# The new folder needs its own .meta or Unity mints one with a fresh GUID on every machine.
emit("Assets/_Scripts/Controller/Arcade/Switchback.meta",
     f"fileFormatVersion: 2\nguid: {guid('folder/Switchback')}\nfolderAsset: yes\n"
     f"DefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n"
     f"  assetBundleVariant:\n")


# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.SwitchesThreaded. Golf: the winning domain's pilots carry a finish
# time, everyone else a sentinel, so lower is better.
emit("Assets/_SO_Assets/Scoring Rules/SwitchbackScoringRule.asset",
     HEADER_FOR(G_SCRIPT["SwitchbackScoringRuleSO"], "SwitchbackScoringRule") +
     "  metric: 9\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/SwitchbackScoringRule.asset.meta",
     asset_meta(G_ASSET["SwitchbackScoringRule"]))


# ── 3. Arcade game config ────────────────────────────────────────────────────
# DOLPHIN ONLY: a single entry in Vessels drives all three enforcement layers (the launcher
# clamp, the server-side spawn clamp, and the AI clamp).
#
# MinPlayersAllowed 2 / MinDomainsAllowed 2 because a race needs a rival: with one domain the
# objective is reached the moment anyone finishes and there is nobody to have beaten.
emit("Assets/_SO_Assets/Games/ArcadeGameSwitchback.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameSwitchback") + f"""  Mode: 45
  IsMultiplayer: 1
  DisplayName: Switchback
  Description: Dolphins only, through a course of rings nobody placed twice the same
    way. Every gate is somewhere new and facing somewhere new, and you take them in
    order - drift the corners, boost the straights, and go back for the one you missed.
    First team to put a pilot through the last gate takes it.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameSwitchback
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Dolphin']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 404
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameSwitchback.asset.meta",
     asset_meta(G_ASSET["ArcadeGameSwitchback"]))


# ── 4. Scene: clone MinigameRampage, swap the mode-specific wiring ───────────
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity")

# 4a. turn monitor script swap. Field set is identical (base TurnMonitor fields only) - both
# monitors read their target from EndConditionOverridesSO rather than a serialized field.
scene, n = re.subn(EXISTING["RampagePrismTurnMonitor"], G_SCRIPT["SwitchbackGateTurnMonitor"], scene)
assert n == 1, f"turn monitor guid appeared {n} times"

# 4b. controller script swap + its serialized field block
scene, n = re.subn(EXISTING["RampageController"], G_SCRIPT["SwitchbackController"], scene)
assert n == 1, f"controller guid appeared {n} times"

OLD_FIELDS = f"  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}\n"
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['SwitchbackScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  firstGateDistance: 620
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 260
  aiApproachLead: 300
  aiThroughDistance: 220
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: 400
  reportResyncSeconds: 3
"""
assert scene.count(OLD_FIELDS) == 1, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

# 4c. the CELL: one barren race cell instead of four cactus forests (see the module docstring).
assert scene.count(DONOR_CELL_BLOCK) == 1, "donor cell block not found - has Rampage re-authored its cell?"
scene = scene.replace(DONOR_CELL_BLOCK, NEW_CELL_BLOCK)

assert scene.count(DONOR_CRYSTAL_BLOCK) == 1, \
    "donor crystal block not found - has Rampage re-authored its crystal supply?"
scene = scene.replace(DONOR_CRYSTAL_BLOCK, NEW_CRYSTAL_BLOCK)

# 4d. the SPAWN RING: equatorial, so the pole the first gate sits on is equidistant from every
# pilot. Distance comes down from Rampage's 500 because this cell's nucleus is the full-size
# Nucleus.prefab (391.9u) rather than Rampage's HalfNucleus (196u) - keeping 500 would put the
# ring at 892u, three quarters of the way to the membrane.
scene, n = re.subn(r"^  spawnDistanceOutsideNucleus: 500$", "  spawnDistanceOutsideNucleus: 150",
                   scene, count=1, flags=re.M)
assert n == 1, "spawnDistanceOutsideNucleus not found"
scene, n = re.subn(r"^  spawnFormation: 0$", "  spawnFormation: 1", scene, count=1, flags=re.M)
assert n == 1, "spawnFormation not found"

# 4e. the COMEBACK SOURCE. A cloned scene carries the DONOR's serialized settings, and
# ElementalComebackSystem.EnsureExists respects a scene-authored instance as-is (it only fills in
# gameData) - DefaultSourceFor runs on the AddComponent branch alone. So leaving Rampage's
# PrismsDestroyed (3) here would make Switchback's comeback read a stat no pilot in this mode ever
# moves, and every Switchback case in that file would be unreachable dead code. The enum is
# explicitly numbered for exactly this reason: SwitchesThreaded = 8.
scene, n = re.subn(r"^  differenceSource: 3$", "  differenceSource: 8", scene, count=1, flags=re.M)
assert n == 1, "donor differenceSource not found"
# Dead while the source is SwitchesThreaded (which is always higher-is-better), but Switchback IS
# golf-scored, so this matches what EnsureExists would have configured had the scene not authored
# one - a later flip to the Score source then reads the right way round.
scene, n = re.subn(r"^  useGolfRules: 0$", "  useGolfRules: 1", scene, count=1, flags=re.M)
assert n == 1, "donor useGolfRules not found"

emit("Assets/_Scenes/Multiplayer Scenes/MinigameSwitchback.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameSwitchback.unity.meta",
     scene_meta(G_ASSET["MinigameSwitchback.unity"]))


# ── 5. Register the card in the party-games list ─────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameSwitchback']}, type: 2}}\n"
# A reference row has nothing inside it to drift, so presence IS content here - but assert the
# count, because a duplicate would list the card twice in the party-games picker.
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
assert games.count(entry) == 1, "the Switchback card is listed more than once in OrganicRematchGames"
emit(LIST_PATH, games)


# ── 6. Always-unlocked so the card is clickable on a fresh account ──────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 45\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 45\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 7. Build settings ───────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameSwitchback.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameSalvo\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Salvo scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameSwitchback.unity\n"
                          f"    guid: {G_ASSET['MinigameSwitchback.unity']}\n")
emit(BUILD_PATH, build)


# ── 8. End-game condition target ────────────────────────────────────────────
# SET rather than insert-if-absent, so a re-run after a retune actually moves the number the
# game reads (the Dog Fight generator's lesson).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("salvoPrismTarget", "switchbackGateTarget"),
                          ("salvoPrismTargetBuild", "switchbackGateTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {GATE_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH} - run author_salvo_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {GATE_TARGET}\n", 1)
emit(END_PATH, endcond)


# ── 9. The goal-stack row: metric 9's icon + label ──────────────────────────
# Keyed on the METRIC, never the mode - a future mode reusing SwitchesThreaded gets this free.
ICON_PATH = "Assets/Resources/ObjectiveIconSet.asset"
icons = read(ICON_PATH)
GLYPH_GUID = guid("ObjectiveIcons/objective_switches_threaded")
row = (f"  - metric: 9\n    icon: {{fileID: 21300000, guid: {GLYPH_GUID}, type: 3}}\n"
       f"    label: Thread switches\n")
# SET, not insert-if-absent: with insert-if-absent the emitted buffer equals the disk buffer on
# every run after the first, so --check would pass whatever the row had drifted to and retuning
# the label or the glyph here would be a silent no-op. Same rule as the EndConditionOverrides
# block below.
existing = re.search(r"^  - metric: 9\n    icon: \{[^}]*\}\n    label: .*\n", icons, re.M)
if existing:
    icons = icons.replace(existing.group(0), row, 1)
else:
    assert icons.endswith("\n")
    icons = icons + row
emit(ICON_PATH, icons)

# The launch panel's objective box reads a SECOND metric->icon table. Same glyph.
LIB_PATH = "Assets/Resources/ModeControlsLibrary.asset"
lib = read(LIB_PATH)
lib_row = f"  - Metric: 9\n    Icon: {{fileID: 21300000, guid: {GLYPH_GUID}, type: 3}}\n"
# SET, for the same reason as the icon set above - this row's only content is the glyph guid,
# which is exactly the thing a re-bake of the icon would change.
existing = re.search(r"^  - Metric: 9\n    Icon: \{[^}]*\}\n", lib, re.M)
if existing:
    lib = lib.replace(existing.group(0), lib_row, 1)
else:
    m = re.search(r"^(  - Metric: 8\n    Icon: \{[^}]*\}\n)", lib, re.M)
    assert m, "Metric 8 row not found in ModeControlsLibrary"
    lib = lib.replace(m.group(1), m.group(1) + lib_row, 1)
emit(LIB_PATH, lib)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

# The comeback rate is meaningless without the target next to it - see COMEBACK_RATE.
if 0.25 * GATE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(
        f"comeback rate {COMEBACK_RATE} is dead against target {GATE_TARGET}: a "
        f"quarter-of-target deficit buys {0.25 * GATE_TARGET * COMEBACK_RATE:.2f} element "
        f"levels (< 1). Rescale the rate with the target.")

# A course with fewer than two gates is not a course, and the generator refuses it anyway.
if GATE_TARGET < 2:
    errors.append("gate target below 2 - SwitchbackCourse.Generate returns null")

# The COURSE SHELL is written here, defaulted in C#, and swept by the 400-seed test - three
# copies of four numbers. Nothing forces them to agree, so retuning the scene silently leaves
# the sweep proving the old geometry and the C# defaults describing a course nobody flies.
# Tie them here: a shell that cannot generate surfaces at runtime only as a CSDebug.LogError
# and a race with no finish line.
SHELL = {"courseOuterRadius": "1080", "courseInnerRadiusFallback": "480",
         "firstGateDistance": "620"}
for field, want in SHELL.items():
    if f"  {field}: {want}\n" not in NEW_FIELDS:
        errors.append(f"scene shell {field} is not {want} - update SHELL and the two readers below")
    m = re.search(rf"{field}\s*=\s*([0-9.]+)f;", read("Assets/_Scripts/Controller/Arcade/Switchback/"
                                                       "SwitchbackController.cs"))
    if not m or float(m.group(1)) != float(want):
        errors.append(f"SwitchbackController.{field} default disagrees with the scene ({want})")

_tests = read("Assets/_Scripts/Tests/Editor/SwitchbackCourseTests.cs")
for const, want in (("Inner", SHELL["courseInnerRadiusFallback"]),
                    ("Outer", SHELL["courseOuterRadius"])):
    m = re.search(rf"const float {const} = ([0-9.]+)f;", _tests)
    if not m or float(m.group(1)) != float(want):
        errors.append(f"SwitchbackCourseTests.{const} disagrees with the scene shell ({want})")
if re.search(rf"s\.FirstGateDistance = {SHELL['firstGateDistance']}f;", _tests) is None:
    errors.append("SwitchbackCourseTests sweeps a different FirstGateDistance than the scene")
m = re.search(r"const int Gates = (\d+);", _tests)
if not m or int(m.group(1)) != GATE_TARGET:
    errors.append(f"SwitchbackCourseTests.Gates disagrees with GATE_TARGET ({GATE_TARGET})")

all_new = (list(G_SCRIPT.values()) + list(G_ASSET.values())
           + [GLYPH_GUID, guid("folder/Switchback"), guid("doc/SWITCHBACK.md")])
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

owned_metas = {os.path.normpath(os.path.join(ROOT, rel)) for rel in files if rel.endswith(".meta")}
existing_guids = set()
for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in filenames:
        if not fn.endswith(".meta"):
            continue
        full = os.path.normpath(os.path.join(dirpath, fn))
        if full in owned_metas:
            continue
        try:
            with open(full, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
            if m:
                existing_guids.add(m.group(1))
        except OSError:
            pass

# The glyph's .meta is authored by author_objective_icons.py, not here - so it is EXPECTED to
# already exist, and its guid must match what that script mints for the same name.
for g in all_new:
    if g == GLYPH_GUID:
        continue
    if g in existing_guids:
        errors.append(f"minted GUID {g} collides with an asset this script does not own")

for name, g in EXISTING.items():
    if g not in existing_guids:
        errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")

for k, p in SCRIPT_PATHS.items():
    if not os.path.exists(os.path.join(ROOT, p)):
        errors.append(f"script {p} does not exist")

# The glyph must have been authored before this runs, or the goal row points at nothing.
glyph_png = os.path.join(ROOT, "Assets/_Graphics/UI/Objectives/objective_switches_threaded.png")
if not os.path.exists(glyph_png):
    errors.append("objective_switches_threaded.png missing - run Tools/Build/author_objective_icons.py first")
elif os.path.exists(glyph_png + ".meta"):
    with open(glyph_png + ".meta", encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
    if not m or m.group(1) != GLYPH_GUID:
        errors.append(
            f"glyph guid mismatch: the icon generator minted {m.group(1) if m else '?'} but this "
            f"script points the catalogue at {GLYPH_GUID}")

# The cloned scene must no longer mention the donor's mode-specific guids, and must mention ours.
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameSwitchback.unity"]
for name in ("RampageController", "RampagePrismTurnMonitor", "RampageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("SwitchbackController", "SwitchbackGateTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["SwitchbackScoringRule"] not in sc:
    errors.append("cloned scene missing the scoring rule reference")

# The arena swap is the whole reason this is not "Rampage with rings".
for name in ("RampageCell1", "RampageCell2", "RampageCell3", "RampageCell4"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name} - the cactus forest survived the swap")
if EXISTING["SkimRaceCellConfig"] not in sc:
    errors.append("cloned scene is not pointed at the Skim Race cell")
if "  cellTypeChoiceOptions: 1\n" in sc:
    errors.append("cloned scene still selects its cell IntensityWise - intensity here is the COURSE")

# The fairness rule: an equatorial spawn ring is what makes a first gate on the pole equidistant.
if "  spawnFormation: 1\n" not in sc:
    errors.append("cloned scene is not on the equatorial spawn formation - gate 1 would not be fair")
if "  arrangeSpawnPointsAroundCell: 1\n" not in sc:
    errors.append("cloned scene lost the cell-relative spawn ring")

# A scene-authored ElementalComebackSystem is used AS AUTHORED, so a stale donor source is a
# silent no-op comeback layer, not a fallback to DefaultSourceFor.
# Intensity is the COURSE. Rampage spends it on crystal scarcity, and a crystal is the Dolphin's
# only blast trigger, so inheriting that ladder makes intensity mean two contradictory things.
if "  crystalCountMode: 1\n" not in sc:
    errors.append("cloned scene kept Rampage's IntensityScaled crystal supply - intensity here "
                  "is the COURSE, and the blast is the interference layer at every level")
if re.search(r"^  - CrystalsPerPlayer: 2\n", sc, re.M) or "    ExtraCrystals: -1\n" in sc:
    errors.append("cloned scene still carries Rampage's crystal scarcity ladder")

if "  differenceSource: 8\n" not in sc:
    errors.append("cloned scene kept the donor's comeback source - Switchback must read "
                  "ScoreDifferenceSource.SwitchesThreaded (8)")
if "  useGolfRules: 1\n" not in sc:
    errors.append("cloned scene did not take the golf-rules flag off the donor")

# Dolphin only, and the donor's four AI templates are already Dolphins.
if sc.count("  - vesselClass: 2\n") != 4:
    errors.append("cloned scene does not carry 4 Dolphin AI templates")

if errors:
    print("VALIDATION FAILED:")
    for e in errors:
        print("  -", e)
    sys.exit(1)

if CHECK_ONLY:
    changed = []
    for rel, content in files.items():
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full) or read(rel) != content:
            changed.append(rel)
    if changed:
        print(f"--check: {len(changed)} file(s) differ from the authored output:")
        for c in sorted(changed):
            print("  -", c)
        sys.exit(1)
    print(f"--check: OK, {len(files)} file(s) match.")
    sys.exit(0)

for rel, content in files.items():
    full = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
print(f"Wrote {len(files)} file(s).")
for rel in sorted(files):
    print("  ", rel)
