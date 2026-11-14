#!/usr/bin/env python3
"""
Authors every serialized asset the Redline game mode needs; the mode id is READ from
GameModes.cs rather than hardcoded (Headlong's moved 48 -> 49 once; Bloomrush's 45 -> 52).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_redline_assets.py [--check]

--check validates without writing (CI / pre-commit use).

WHAT THIS MODE IS. Redline is the Manta-only CIRCUIT race: a closed loop of switch rings cut
through the cell, flown in LAPS by every pilot, and the first DOMAIN whose LEAD RUNNER threads
the last gate of the last lap wins. Every corner is cut against the Manta's FULL-BOOST turn
radius - the 237u circle it holds with both triggers flat - so a corner is one question: how
much Soar is it worth? Since 2026-11-04 it teaches the Time ability (Soar) ALONE: no corner
needs the Yawstery pivot, the cell's omni crystal is off (the scene's CrystalManager spawns
none), and the only pickups are the Time crystals RedlineController lays from the course
(RedlineTimeCrystals). See Assets/_Scripts/Controller/Arcade/REDLINE.md.

THE DONOR IS HEADLONG, and that is the point rather than a shortcut. Headlong is already a lapped
gate race in the barren race cell with an EQUATORIAL spawn ring (gate 0 on its pole - the
fairness rule), on the shared GateRaceController with a RaceGateTurnMonitor, a
RaceGateObjectiveProvider and the generic GateRaceScoringRuleSO. The clone swaps FOUR things and
inherits the rest:

  1. the controller script (RedlineController), keeping Headlong's `laps` field.
  2. its scoring rule ASSET (a second asset on the same GateRaceScoringRuleSO script).
  3. the AI approach numbers and the plausible-speed clamp: a Manta arrives at 720-936 u/s
     against a Dolphin's 347 (the numbers Headlong inherited), so the commit/lead/through
     distances are sized to the Manta's 237u full-boost circle, and maxPlausibleSpeed is
     raised so a Time-10 Manta's frame step is never rejected as a teleport (at 400 the 30 fps
     step of a 936 u/s vessel - 31.2u - sits within a unit of the rejection line).
  4. the arcade card's Vessels list: Manta, not Rhino.

THE CIRCUIT GENERATOR IS SHARED, NOT CLONED. RedlineCourse supplies the Manta's settings to
HeadlongCircuit.Generate; the only change to the solver is an absolute corner floor field.

THE 2026-11-11 UPGRADE (REDLINE.md section 0). Since 2026-11-07 Soar is the Manta's Time LEVEL-5 upgrade, and the card
seeded nothing, so every Manta raced at 180 u/s with Soar dark. This script now writes the card's StartingElements (Time
6 at every intensity, READ from RedlineCourse.StartingTimeLevelByIntensity), the race at 8 laps (64 gates: measured on
the flown hull, manta_redline_sim.py --reading), and owns the mode's toast config (GameToastConfig_Redline, its guid
kept): the shared race beats word for word, plus Redline's heat toasts 360-366 (RedlineController.Heat).
"""
import hashlib
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as aml  # noqa: E402  - card background + retired-key checks

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "RedlineController":  guid("script/RedlineController"),
    "RedlineCourse":      guid("script/RedlineCourse"),
    "RedlineCourseTests": guid("script/RedlineCourseTests"),
    "RedlineTimeCrystals":      guid("script/RedlineTimeCrystals"),
    "RedlineTimeCrystalsTests": guid("script/RedlineTimeCrystalsTests"),
    "RedlineHeat":              guid("script/RedlineHeat"),
    "RedlineHeatTests":         guid("script/RedlineHeatTests"),
    "RedlineControllerHeat":    guid("script/RedlineController.Heat"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameRedline":     guid("asset/ArcadeGameRedline"),
    "RedlineScoringRule":    guid("asset/RedlineScoringRule"),
    "MinigameRedline.unity": guid("asset/MinigameRedline.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":          "fe040efad3307fb449b6b72ad15362da",
    # the shared gate-race scoring rule SCRIPT (one class, one asset per mode)
    "GateRaceScoringRuleSO":  "349cc0c9402590262de23356775d43cc",
    # donor scene wiring to swap out - Headlong's controller and rule, minted by ITS generator
    "HeadlongController":     hashlib.md5(b"CosmicShore/script/HeadlongController").hexdigest(),
    "HeadlongScoringRule":    hashlib.md5(b"CosmicShore/asset/HeadlongScoringRule").hexdigest(),
    # shared content
    "Vessel_Manta":           "b0e6ec5495dbfb6419332830d585f364",
    # arcade card art - shared with the other pure-race cards
    "IconActive":             "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":           "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":         "587d2203114c8004c9985d0112c89585",
}

# ── Tuning ───────────────────────────────────────────────────────────────────

# The RACE LENGTH in gate threadings - LAPS x RINGS. One number: the controller divides by LAPS
# to size the circuit and the turn monitor asks the controller for the target, so the finish
# line and the course cannot be different lengths. Kept in sync with
# EndConditionOverridesSO.DefaultRedlineGateTarget.
# 8 laps since 2026-11-11: with Soar lit from the gun a lap takes a person ~5 s at I1 and ~9-11 s at I4 (it was ~28 s
# at 180 u/s), so 5 laps ran 24-53 s; 8 run ~38-85 s for the model's ideal pilot and ~50-110 s for a person.
LAPS = 8
RINGS_PER_LAP = 8                 # RedlineCourse.ForIntensity's GateCount
GATE_TARGET = LAPS * RINGS_PER_LAP

# The circuit's base circle. A regular octagon at 820 has 628u legs, so one lap is ~5k units
# and the race ~15k: a Manta that holds Soar covers it in about 21 s at 720 u/s; one that
# gives it up at every corner spends two seconds winning each one back.
BASE_RADIUS = 820

# AI approach geometry, sized to the Manta rather than inherited from a Dolphin at 347 u/s:
# the commit distance sits comfortably outside the 237u full-boost circle so a bot that is
# still lining up has room to turn, the lead is about two circles back along the axis, and
# the through point is far enough past the mouth that the bot does not swing early.
AI_COMMIT_DISTANCE = 420
AI_APPROACH_LEAD = 480
AI_THROUGH_DISTANCE = 320

# Detection clamp: a single frame's motion longer than this x dt x 2 + 5 is treated as a
# respawn. The flown hull (2026-11-11): a Time-10 Manta at full Soar is 1404 u/s (270 cruise x 4 x 1.3) and an
# overcharged one (Time 15) ~1827, both inside 1400 x 2. The inherited 400 rejected even the old 936 u/s step.
MAX_PLAUSIBLE_SPEED = 1400
FLOWN_TOP_SPEED = 1827            # RedlineCourse.SoarSpeed at the overcharge cap (Time 15)

# The comeback strength, and it is a FUNCTION OF THE TARGET - `bonusLevels = deficit x rate` -
# so a rate only means anything next to the scale of deficits the mode produces (the trap
# DOGFIGHT.md, BENDS.md, WILDLIFE_LIBERATION.md, SWITCHBACK.md and HEADLONG.md each record).
# At 0.21: ten gates behind (a lap and a quarter of the 8-lap race) buys 2.1 element levels (0.35 on the
# old three-lap, 24-gate race; raced to ~90 s, 2026-10-26) - and on THIS hull
# a Time level is speed outright (+5% cruise, +3% Soar), so the buff lands on the mode's axis.
COMEBACK_RATE = 0.21


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
    "RedlineController":  "Assets/_Scripts/Controller/Arcade/Redline/RedlineController.cs",
    "RedlineCourse":      "Assets/_Scripts/Controller/Arcade/Redline/RedlineCourse.cs",
    "RedlineCourseTests": "Assets/_Scripts/Tests/Editor/RedlineCourseTests.cs",
    "RedlineTimeCrystals":      "Assets/_Scripts/Controller/Arcade/Redline/RedlineTimeCrystals.cs",
    "RedlineTimeCrystalsTests": "Assets/_Scripts/Tests/Editor/RedlineTimeCrystalsTests.cs",
    "RedlineHeat":              "Assets/_Scripts/Controller/Arcade/Redline/RedlineHeat.cs",
    "RedlineHeatTests":         "Assets/_Scripts/Tests/Editor/RedlineHeatTests.cs",
    "RedlineControllerHeat":    "Assets/_Scripts/Controller/Arcade/Redline/RedlineController.Heat.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))

# The mode doc is an ASSET too - without a .meta Unity mints a fresh GUID on every machine.
emit("Assets/_Scripts/Controller/Arcade/REDLINE.md.meta",
     f"fileFormatVersion: 2\nguid: {guid('doc/REDLINE.md')}\nTextScriptImporter:\n"
     f"  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

emit("Assets/_Scripts/Controller/Arcade/Redline.meta",
     f"fileFormatVersion: 2\nguid: {guid('folder/Redline')}\nfolderAsset: yes\n"
     f"DefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n"
     f"  assetBundleVariant:\n")


# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.SwitchesThreaded, REUSED rather than added, exactly as Headlong
# reused it: the metric, the BestByDomain fold, the goal-stack row and the objective icon all
# come for free (CLAUDE.md: keyed on the METRIC, never on the game mode). Golf: the winning
# domain's pilots carry a finish time, everyone else a sentinel, so lower is better.
emit("Assets/_SO_Assets/Scoring Rules/RedlineScoringRule.asset",
     HEADER_FOR(EXISTING["GateRaceScoringRuleSO"], "RedlineScoringRule") +
     "  metric: 9\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/RedlineScoringRule.asset.meta",
     asset_meta(G_ASSET["RedlineScoringRule"]))


# ── Mode id: READ, never hardcoded ───────────────────────────────────────────
_ENUM = read("Assets/_Scripts/Data/Enums/GameModes.cs")
_m = re.search(r"^\s*Redline\s*=\s*(\d+)\s*,", _ENUM, re.M)
assert _m, "GameModes.Redline not found - has the member been renamed?"
MODE_ID = int(_m.group(1))


# ── The race's starting Time level: READ from RedlineCourse, never typed twice ──
# StartingTimeLevelByIntensity { 6, 6, 6, 6 }: Soar (the Time level-5 upgrade) is lit from the gun with two petals of
# headroom over its relock (below 4). A card row's levels are normalised (level / 10), one row per intensity, for the
# Manta (VesselClassType.Manta = 1) - Headlong's StartingElements shape.
_COURSE_SRC = read("Assets/_Scripts/Controller/Arcade/Redline/RedlineCourse.cs")
_m = re.search(r"StartingTimeLevelByIntensity = \{ ([0-9, ]+) \}", _COURSE_SRC)
assert _m, "RedlineCourse.StartingTimeLevelByIntensity not found"
START_LEVELS = [int(x) for x in _m.group(1).split(",")]
_m = re.search(r"MantaSoarUnlockLevel = (\d+);", _COURSE_SRC)
assert _m, "RedlineCourse.MantaSoarUnlockLevel not found"
SOAR_UNLOCK = int(_m.group(1))
MANTA_CLASS = 1
STARTING_ELEMENTS = "  StartingElements:\n" + "".join(
    f"  - Class: {MANTA_CLASS}\n    Intensity: {i + 1}\n    Levels:\n      Mass: 0\n      Charge: 0\n      Space: 0\n"
    f"      Time: {lv / 10:g}\n" for i, lv in enumerate(START_LEVELS) if lv > 0)

# ── 3. Arcade game config ────────────────────────────────────────────────────
# MANTA ONLY: a single entry in Vessels drives all three enforcement layers (the launcher clamp,
# the server-side spawn clamp, and the AI clamp).
#
# MinPlayersAllowed 2 / MinDomainsAllowed 2 because a race needs a rival: with one domain the
# objective is reached the moment anyone finishes and there is nobody to have beaten.
emit("Assets/_SO_Assets/Games/ArcadeGameRedline.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameRedline") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Redline
  Description: Mantas only, flat out. Soar is lit from the gun - bury both triggers for
    over a thousand units a second and a turning circle you can hold through most of the
    lap. Thread every ring through its heart to keep the streak alive, and ease both
    triggers only where a ring would get away from you. Time crystals make it faster
    still, all the way to the redline. Eight laps.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {aml.card_background('Redline')}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameRedline
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Manta']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
{STARTING_ELEMENTS}  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameRedline.asset.meta",
     asset_meta(G_ASSET["ArcadeGameRedline"]))


# ── 4. Scene: clone MinigameHeadlong, swap the controller ────────────────────
# 4b. the controller's serialized field block: the rule asset, the AI numbers and the speed clamp move; the
# shell, the lap count and the course seed stay.
OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['HeadlongScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: 3
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
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['RedlineScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: {LAPS}
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: {AI_COMMIT_DISTANCE}
  aiApproachLead: {AI_APPROACH_LEAD}
  aiThroughDistance: {AI_THROUGH_DISTANCE}
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: {MAX_PLAUSIBLE_SPEED}
  reportResyncSeconds: 3
"""
CONTROLLER_SCRIPT_LINE = f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['RedlineController']}, type: 3}}\n"

# 4c. Everything ELSE the donor authored is already what this mode wants, stated rather than
# left as an absence:
#   - the CELL is the barren race cell (one config) - intensity is the COURSE here too.
#   - the SPAWN RING is EQUATORIAL at 150 outside the nucleus: gate 0 is rotated onto its POLE,
#     and only a point on the axis is equidistant from every pilot.
#   - (the comeback source is no longer authored: it reads the scoring rule, 2026-09)
#   - the AI roster's vesselClass is irrelevant: the card's Vessels list clamps every AI to the
#     Manta (ServerPlayerVesselInitializerWithAI -> GameDataSO.ClampVesselToGame).
INHERITED_PROBES = ((r"^  spawnFormation: 1$", "equatorial spawn ring"),
                    (r"^  cellTypeChoiceOptions: 0$", "single race cell"))


def clone_scene() -> str:
    scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameHeadlong.unity")
    # 4a. controller script swap
    scene, n = re.subn(EXISTING["HeadlongController"], G_SCRIPT["RedlineController"], scene)
    assert n == 1, f"controller guid appeared {n} times in the donor scene"
    assert scene.count(OLD_FIELDS) == 1, "controller field block not found in donor scene"
    scene = scene.replace(OLD_FIELDS, NEW_FIELDS)
    for probe, why in INHERITED_PROBES:
        assert re.search(probe, scene, re.M), f"donor no longer provides: {why}"
    return scene


# The clone is a one-shot: once MinigameRedline.unity is committed the Editor owns its fileIDs
# and Netcode GlobalObjectIdHash values, and a re-clone would revert them to Headlong's. The
# committed scene is adopted, the blocks this script authors must still be in it, and the donor
# asserts STAND DOWN when Headlong moves on (CLAUDE.md "spent one-shot"). See aml.committed_scene.
scene, _scene_errors = aml.committed_scene(
    "Assets/_Scenes/Multiplayer Scenes/MinigameRedline.unity", clone_scene,
    authored_blocks=(CONTROLLER_SCRIPT_LINE,))
# The lap count is RE-TUNED in place (5 -> 8, 2026-11-11), found by the shell fields around it, then the whole authored
# field block must read exactly NEW_FIELDS once.
scene, _n_laps = re.subn(r"(  innerRadiusNucleusFactor: 1\.22\n)  laps: \d+\n(  gateBloomSeconds: )",
                         rf"\g<1>  laps: {LAPS}\n\g<2>", scene)
if _n_laps != 1:
    _scene_errors.append(f"the controller's laps field was found {_n_laps} times, not once")
if scene.count(NEW_FIELDS) != 1:
    _scene_errors.append("MinigameRedline.unity: the controller field block is not the authored one:\n" + NEW_FIELDS)
# What the mode INHERITED from the donor is still what it needs - checked on the scene it ships.
_scene_errors += [f"MinigameRedline.unity no longer provides: {why}"
                  for probe, why in INHERITED_PROBES if not re.search(probe, scene, re.M)]

# 4d. TIME ONLY (2026-11-04): the cell's omni crystal feeds all four elements and fires the Manta's omni crystal
# effects, so the scene's CrystalManager spawns NONE (CrystalCountMode.FixedCount = 0, a count of 0). The only pickups
# are the Time crystals RedlineController lays from the course. Authored on the committed scene, idempotent: the block
# is found by the omni crystal prefab it points at, and must read 0 / 0 exactly once afterwards.
OMNI_CRYSTAL = "54802b89e00a0ed4281025fa5e770811"
_mgr = re.compile(r"(  crystalPrefab: \{fileID: 5535990081244205891, guid: " + OMNI_CRYSTAL +
                  r",\n    type: 3\}\n(?:  [^\n]*\n)*?)  crystalCountMode: \d+\n  fixedCrystalCount: \d+\n")
scene, _n_mgr = _mgr.subn(r"\g<1>  crystalCountMode: 0\n  fixedCrystalCount: 0\n", scene)
if _n_mgr != 1:
    _scene_errors.append(f"the scene's CrystalManager block (crystalPrefab = the omni Crystal) was found {_n_mgr} times, not once")
elif len(re.findall(r"^  crystalCountMode: 0\n  fixedCrystalCount: 0\n", scene, re.M)) != 1:
    _scene_errors.append("the scene's CrystalManager does not read crystalCountMode 0 / fixedCrystalCount 0")

emit("Assets/_Scenes/Multiplayer Scenes/MinigameRedline.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameRedline.unity.meta",
     scene_meta(G_ASSET["MinigameRedline.unity"]))


# ── 5. Register the card in BOTH rosters ────────────────────────────────────
# Docs/HomeHub/ARCHITECTURE.md §3.1: OrganicRematchGames is the MASTER (the injected list a
# guest resolves the host's card through), and ArcadeGames is the Arcade grid's
# rosterOverride — "the master minus the arena cards". A card registered in the master alone
# launches for a guest and is invisible in the arcade, which is exactly how Redline shipped
# the first time (and how Breakwater / Skein / Bloomrush arrived from their own branches).
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameRedline']}, type: 2}}\n"
for LIST_PATH in ("Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset",
                  "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"):
    games = read(LIST_PATH)
    if entry not in games:
        assert games.endswith("\n")
        games = games + entry
    assert games.count(entry) == 1, f"the Redline card is listed more than once in {LIST_PATH}"
    emit(LIST_PATH, games)


# ── 6. Always-unlocked so the card is clickable on a fresh account ──────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(rf"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - {MODE_ID}\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", rf"\g<1>  - {MODE_ID}\n",
                      prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 7. Build settings ───────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameRedline.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameBloomrush\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Bloomrush scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRedline.unity\n"
                          f"    guid: {G_ASSET['MinigameRedline.unity']}\n")
emit(BUILD_PATH, build)


# ── 8. End-game condition target ────────────────────────────────────────────
# SET rather than insert-if-absent, so a re-run after a retune actually moves the number the
# game reads (the Dog Fight generator's lesson). Inserted after the Headlong keys, matching the
# C# declaration order.
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("headlongGateTarget", "redlineGateTarget"),
                          ("headlongGateTargetBuild", "redlineGateTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {GATE_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH} - run author_headlong_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {GATE_TARGET}\n", 1)
emit(END_PATH, endcond)

# The goal-stack row and the launch panel's objective icon are keyed on metric 9, which
# author_switchback_assets.py already authored. Nothing to do here, and deliberately so.


# ── 9. The toast config (owned since 2026-11-11; its guid is kept) ──────────
# The shared race beats (DomainRaceToasts 129-132) and the comeback line (30) word for word as a human authored them,
# then Redline's heat (GameToastSituation 360-366, RedlineController.Heat), each posted to the LOCAL pilot only. Every
# line is a human-owned template: an empty one shows nothing (CLAUDE.md, player-facing text).
TOAST_PATH = "Assets/_SO_Assets/Game Toasts/GameToastConfig_Redline.asset"
TOAST_SCRIPT = "86d1715b8f104fcc87cb60e015d4b563"          # GameToastConfigSO
REDLINE_TOASTS = [
    (360, "Soar is lit from the gun. Bury both triggers, thread every ring through its heart, and ease both "
          "together only where a ring would get away from you"),
    (361, "{0} clean in a row"),
    (362, "Lap {0} - {1}s"),
    (363, "Best lap yet - lap {0} in {1}s"),
    (364, "Clean lap {0} - every ring through its heart"),
    (365, "Soar is dark - a Time crystal lights it again"),
    (366, "REDLINE - {0} u/s"),
]
toast_text = (HEADER_FOR(TOAST_SCRIPT, "GameToastConfig_Redline") + f"  gameMode: {MODE_ID}\n  toasts:\n"
              + aml.toast(129, "{0} is halfway home - gate {1}/{2}", tint_domain=1, domain_names=0)
              + aml.toast(130, "{0} takes the lead - gate {1}/{2}", tint_domain=1, domain_names=0)
              + aml.toast(131, "{0} is on the home stretch - gate {1}/{2}", tint_domain=1, domain_names=0)
              + aml.toast(132, "{0} is on the final lap", tint_domain=1, domain_names=0)
              + aml.toast(30, "Comeback system is on", tint_domain=0, domain_names=0, alpha=0.9)
              + "".join(aml.toast(sid, text, tint_domain=0, domain_names=0) for sid, text in REDLINE_TOASTS))
emit(TOAST_PATH, toast_text)
emit(TOAST_PATH + ".meta", aml.asset_meta_text(aml.existing_guid(TOAST_PATH)))


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = list(_scene_errors)

if 0.25 * GATE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(
        f"comeback rate {COMEBACK_RATE} is dead against target {GATE_TARGET}: a "
        f"quarter-of-target deficit buys {0.25 * GATE_TARGET * COMEBACK_RATE:.2f} element "
        f"levels (< 1). Rescale the rate with the target.")

if GATE_TARGET % LAPS != 0:
    errors.append(f"race target {GATE_TARGET} is not a whole number of {LAPS} laps")

_course = read("Assets/_Scripts/Controller/Arcade/Redline/RedlineCourse.cs")
m = re.search(r"GateCount\s*=\s*(\d+),", _course)
if not m or int(m.group(1)) != RINGS_PER_LAP:
    errors.append(f"RedlineCourse.ForIntensity GateCount is not {RINGS_PER_LAP}")
m = re.search(r"BaseRadius\s*=\s*(\d+)f,", _course)
if not m or int(m.group(1)) != BASE_RADIUS:
    errors.append(f"RedlineCourse.ForIntensity BaseRadius is not {BASE_RADIUS}")

_end = read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultRedlineGateTarget\s*=\s*(\d+);", _end)
if not m or int(m.group(1)) != GATE_TARGET:
    errors.append(f"EndConditionOverridesSO.DefaultRedlineGateTarget is not {GATE_TARGET}")

_ctrl = read("Assets/_Scripts/Controller/Arcade/Redline/RedlineController.cs")
# Literal, or one hop into the course source's const (the RaceCourseSource refactor).
_laps = aml.cs_field_default(_ctrl, "laps")
if _laps is None or int(float(_laps)) != LAPS:
    errors.append(f"RedlineController.laps default is not {LAPS}")

for field, want in (("courseOuterRadius", "1080"), ("courseInnerRadiusFallback", "480")):
    if f"  {field}: {want}\n" not in NEW_FIELDS:
        errors.append(f"scene shell {field} is not {want}")
_tests = read("Assets/_Scripts/Tests/Editor/RedlineCourseTests.cs")
for const, want in (("Inner", "480"), ("Outer", "1080")):
    m = re.search(rf"const float {const} = ([0-9.]+)f;", _tests)
    if not m or float(m.group(1)) != float(want):
        errors.append(f"RedlineCourseTests.{const} is not {want}")

# The speed clamp must clear a Time-10 Manta at 30 fps with the clamp's own margin removed:
# maxStep = speed x dt x 2 + 5 must exceed 936 x dt.
if MAX_PLAUSIBLE_SPEED * 2 <= FLOWN_TOP_SPEED:
    errors.append("maxPlausibleSpeed does not clear an overcharged Manta at full Soar")

# Soar must be lit from the gun at every intensity, with headroom over its relock.
if len(START_LEVELS) != 4 or any(lv < SOAR_UNLOCK for lv in START_LEVELS):
    errors.append(f"RedlineCourse.StartingTimeLevelByIntensity {START_LEVELS} leaves Soar dark at some intensity "
                  f"(it unlocks at Time {SOAR_UNLOCK})")
_ability_map = read("Assets/Resources/ElementalAbilityMaps/Manta.asset")
if not re.search(r"UpgradeLabel: Soar", _ability_map):
    errors.append("Manta.asset no longer labels a Soar upgrade - re-read where Soar unlocks")

# The toast ids Redline posts are its own band, and every one has a line.
_toast_enum = read("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
for sid, _text in REDLINE_TOASTS:
    if not 360 <= sid <= 369:
        errors.append(f"toast {sid} is outside Redline's band 360-369")
    if not re.search(rf"\bRedline\w+ = {sid},", _toast_enum):
        errors.append(f"GameToastSituation has no Redline member = {sid}")

# GUID hygiene: nothing this script mints may collide with an asset it does not own.
all_new = set(G_SCRIPT.values()) | set(G_ASSET.values()) | {guid("doc/REDLINE.md"), guid("folder/Redline")}
# Normalised on both sides: on Windows os.path.join(ROOT, "Assets/...") keeps the forward slashes while os.walk
# yields backslashes, so without it every meta this script owns read as a foreign asset and --check reported
# eight false "collisions" there (2026-11-04).
_norm = lambda p: os.path.normcase(os.path.normpath(p))
owned_metas = {_norm(os.path.join(ROOT, p)) for p in files if p.endswith(".meta")}
existing_guids = set()
for dirpath, _dirs, fnames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in fnames:
        if not fn.endswith(".meta"):
            continue
        full = os.path.join(dirpath, fn)
        if _norm(full) in owned_metas:
            continue
        try:
            with open(full, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
            if m:
                existing_guids.add(m.group(1))
        except OSError:
            pass
for g in all_new:
    if g in existing_guids:
        errors.append(f"minted GUID {g} collides with an asset this script does not own")
for name, g in EXISTING.items():
    if g not in existing_guids:
        errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")
for k, p in SCRIPT_PATHS.items():
    if not os.path.exists(os.path.join(ROOT, p)):
        errors.append(f"script {p} does not exist")

# The card's CardBackground is the /cardart render and no retired key rides on it - the
# shared check every arcade generator runs (arcade_mode_lib.card_errors).
errors += aml.check_cards(files)

if errors:
    print("VALIDATION FAILED:")
    for e in errors:
        print("  -", e)
    sys.exit(1)

# ══ WRITE / CHECK ═══════════════════════════════════════════════════════════
if CHECK_ONLY:
    differing = []
    for rel, content in sorted(files.items()):
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full):
            differing.append(rel + "  (missing)")
            continue
        with open(full, encoding="utf-8") as fh:
            if fh.read() != content:
                differing.append(rel)
    if differing:
        print(f"--check: {len(differing)} file(s) differ from the authored output:")
        for d in differing:
            print("  -", d)
        sys.exit(1)
    print(f"--check: OK ({len(files)} files match)")
    sys.exit(0)

for rel, content in sorted(files.items()):
    full = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8") as fh:
        fh.write(content)
    print("wrote", rel)
print(f"\nRedline: {len(files)} files authored. Race = {LAPS} laps x {RINGS_PER_LAP} rings "
      f"= {GATE_TARGET} gates.")
