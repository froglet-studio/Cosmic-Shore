#!/usr/bin/env python3
"""
Authors every serialized asset the REGATTA game mode needs (GameModes.Regatta - the id is READ
out of the enum, never transcribed).

Idempotent and deterministic (arcade_mode_lib): every GUID is md5("CosmicShore/<stable name>"),
the whole result is validated in memory, and only then is anything written.

    python3 Tools/Build/author_regatta_assets.py [--check]

WHAT THIS MODE IS. Regatta is the ARENA race: every playable hull on the same closed circuit of
eight switch rings, with three super-shielded rails - one per playable domain - braided along
the racing line, so an Urchin grinds it and a Squirrel skims it while a Manta, a Rhino, a
Scarab or a Sparrow flies beside it. First DOMAIN whose LEAD RUNNER threads the last gate of
the last lap wins. See Assets/_Scripts/Controller/Arcade/REGATTA.md.

WHAT IS FORKED AND WHY. Four cell configs, because the arena is an AUTHORED environment (the
rails, one SpawnableRegattaRails prefab variant per intensity) and its volume ladder has to
sit above ~2,000 super-shielded (6,6,8) prisms - 288 volume each - that the count x 16
derivation would price at a seventeenth. The spawn profile is the Barren cell's, REFERENCED:
no flora, no fauna (shielded mass is never food anyway, and fauna are per-peer).

WHAT IS REFERENCED. The scene is a clone of MinigameRedline (itself Headlong's): the gate-race
platform, the RaceGateTurnMonitor, the generic GateRaceScoringRuleSO (a second asset), the
equatorial spawn ring (overridden at runtime by the controller's own start line behind gate 0),
the crystal manager, the cell network sync. The scoring metric, the objective icon, the goal
row and the comeback source are SwitchesThreaded's, reused.

THE GRID IS BALANCED BY THE CARD. SO_ArcadeGame.StartingElements - the per-hull starting
element table this mode introduced to the platform - is authored here from
Tools/Build/regatta_balance.py, an offline lap-time model over the MEASURED circuits
(Tools/Build/regatta_course_measurements.json, produced by running the real C# through
Tools/Build/regatta_course_harness/run.sh). The residual spread the model reports is asserted
below and stated in the doc: what an element cannot close is not hidden by this script.
"""
import hashlib
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import regatta_balance as RB   # noqa: E402

guid = lib.guid


def _mode_id():
    src = lib.Generator(0, "probe").read("Assets/_Scripts/Data/Enums/GameModes.cs")
    m = re.search(r"^\s*Regatta\s*=\s*(\d+)\s*,", src, re.M)
    assert m, "GameModes.Regatta not found - has the member been renamed?"
    return int(m.group(1))


MODE_ID = _mode_id()
g = lib.Generator(MODE_ID, "Regatta")

# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
SCRIPT_PATHS = {
    "RegattaController":         "Assets/_Scripts/Controller/Arcade/Regatta/RegattaController.cs",
    "RegattaCourse":             "Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourse.cs",
    "SpawnableRegattaRails":     "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableRegattaRails.cs",
    "VesselStartingElements":    "Assets/_Scripts/Data/Structs/VesselStartingElements.cs",
    "RegattaCourseTests":        "Assets/_Scripts/Tests/Editor/RegattaCourseTests.cs",
    "VesselStartingElementsTests": "Assets/_Scripts/Tests/Editor/VesselStartingElementsTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameRegatta":      guid("asset/ArcadeGameRegatta"),
    "RegattaScoringRule":     guid("asset/RegattaScoringRule"),
    "GameToastConfigRegatta": guid("asset/GameToastConfigRegatta"),
    "ModePreviewRegatta":     guid("asset/ModePreviewRegatta"),
    "MinigameRegatta.unity":  guid("asset/MinigameRegatta.unity"),
}
for _i in range(1, 5):
    G_ASSET[f"CellConfig{_i}"] = guid(f"asset/Regatta Cell Config {_i}")
    G_ASSET[f"SpawnableRegattaRails{_i}"] = guid(f"prefab/SpawnableRegattaRails{_i}")

CELL_DIR = "Assets/_SO_Assets/Cell Configs/Regatta Cell"
PREFAB_DIR = "Assets/_Prefabs/Spawnables"
SCRIPTS_DIR = "Assets/_Scripts/Controller/Arcade/Regatta"
DOC = "Assets/_Scripts/Controller/Arcade/REGATTA.md"

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CELL_VISUALS)
EXISTING.update(lib.CARD_ART)
for _h, _g in lib.VESSELS.items():
    EXISTING[f"Vessel_{_h}"] = _g
EXISTING["GateRaceScoringRuleSO"] = lib.existing_guid("Assets/_Scripts/Controller/Arcade/Scoring/GateRaceScoringRuleSO.cs")
EXISTING["RedlineController"] = lib.existing_guid("Assets/_Scripts/Controller/Arcade/Redline/RedlineController.cs")
EXISTING["RedlineScoringRule"] = lib.existing_guid("Assets/_SO_Assets/Scoring Rules/RedlineScoringRule.asset")
EXISTING["SkimRaceCellConfig"] = lib.existing_guid("Assets/_SO_Assets/Cell Configs/Skim Race Cell/Skim Race Cell Config.asset")
EXISTING["BarrenSpawnProfile"] = lib.existing_guid("Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Spawn Profile.asset")
EXISTING["RailPrism"] = lib.existing_guid("Assets/_Prefabs/Trails/SpawnablePrism.prefab")   # Skein's rail prism
EXISTING["RuntimeCellData"] = "8d4e8398eedc76c4dadb8604f89b9e1b"

# ── The race ─────────────────────────────────────────────────────────────────
RINGS_PER_LAP = 8              # RegattaCourse.RingsPerLap - the arena lays exactly this many
LAPS = 3
GATE_TARGET = LAPS * RINGS_PER_LAP   # kept in sync with EndConditionOverridesSO.DefaultRegattaGateTarget

# Comeback strength - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate): six gates behind
# (a quarter of the race) buys 2.1 levels of every element, which on this grid is the second
# balancer after the card's starting elements - Time is speed on five of the eight hulls.
COMEBACK_RATE = 0.35

# The rails' geometry, mirrored from RegattaCourse.DefaultRails / SpawnableRegattaRails and
# asserted against the C# below so the prefab and the code cannot drift.
PRISM_SCALE = (6.0, 6.0, 8.0)
LANE_OFFSET = 22.0
LANE_TWIST = 1.0
PRISM_SPACING = 8.0
PRISM_VOLUME = PRISM_SCALE[0] * PRISM_SCALE[1] * PRISM_SCALE[2]

# The balance the model can reach, and the gate that says so. The residual is the Rhino - the
# one hull no element reaches - against a Time-10 Sparrow; what closes it is not this script.
MAX_TUNED_SPREAD = 6.1

# ── The measured course, and the guard on its provenance ─────────────────────
COURSE = RB.read_course()
_src_hash = hashlib.sha256()
for _s in COURSE["sources"]:
    _src_hash.update(open(os.path.join(lib.ROOT, _s), "rb").read())
COURSE_STALE = _src_hash.hexdigest() != COURSE.get("sourceHash")

CONSTANTS = RB.read_constants()
BALANCE = RB.solve(CONSTANTS, COURSE, laps=LAPS)


def prisms_at(i: int) -> int:
    return int(COURSE["intensities"][str(i)]["prismCount"])


def phase_thresholds(prisms: int) -> dict:
    """Ride the rails' own measured volume plus a trail band. Nearly inert in a cell with no
    flora and no fauna (the ladder gates production, and nothing here produces), but a cell
    whose prisms are 18x nominal volume must never inherit the count x 16 derivation, which
    would pin it at Frenzy from frame one. Restless +60,000 / Frenzy +400,000 volume above the
    rails: a four-hull grid laying trail for a ninety-second race, with room."""
    v = prisms * int(PRISM_VOLUME)
    return {
        "RestlessEnter": prisms + 700, "RestlessExit": prisms + 500,
        "FrenzyEnter": prisms + 3600, "FrenzyExit": prisms + 3000,
        "RestlessEnterVolume": v + 60_000, "RestlessExitVolume": v + 40_000,
        "FrenzyEnterVolume": v + 400_000, "FrenzyExitVolume": v + 320_000,
    }


# ── 1. .cs.meta for the scripts, the doc, the folders ───────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(SCRIPTS_DIR)
g.text_meta(DOC)
g.folder_meta(CELL_DIR)

# ── 2. Scoring rule: the generic gate-race rule, a second asset ─────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/RegattaScoringRule.asset", G_ASSET["RegattaScoringRule"],
             lib.header_for(EXISTING["GateRaceScoringRuleSO"], "RegattaScoringRule") +
             "  metric: 9\n  golfRules: 1\n")

# ── 3. The arena: one SpawnableRegattaRails prefab per intensity ────────────
def spawnable_prefab(i: int) -> str:
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5260000000000301
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5260000000000302}}
  - component: {{fileID: 5260000000000303}}
  m_Layer: 0
  m_Name: SpawnableRegattaRails{i}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5260000000000302
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000301}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5260000000000303
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000301}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableRegattaRails']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: 0
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 1
  layBudgetMsPerFrame: 8
  intensityLevel: {i}
  prism: {{fileID: 4563009547826722997, guid: {EXISTING['RailPrism']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  courseIntensity: {i}
  courseSeed: 0
  prismScale: {{x: {lib.num(PRISM_SCALE[0])}, y: {lib.num(PRISM_SCALE[1])}, z: {lib.num(PRISM_SCALE[2])}}}
  laneOffset: {lib.num(LANE_OFFSET)}
  laneTwistTurnsPerLap: {lib.num(LANE_TWIST)}
  prismSpacing: {lib.num(PRISM_SPACING)}
"""


for _i in range(1, 5):
    g.emit_prefab(f"{PREFAB_DIR}/SpawnableRegattaRails{_i}.prefab", G_ASSET[f"SpawnableRegattaRails{_i}"],
                  spawnable_prefab(_i))

# ── 4. Cell configs, one per intensity ──────────────────────────────────────
for _i in range(1, 5):
    c = COURSE["intensities"][str(_i)]
    t = phase_thresholds(prisms_at(_i))
    desc = (f"Regatta at intensity {_i} of 4 (CellTypeChoiceOptions.IntensityWise, list order = "
            f"intensity): a closed circuit of {c['rings']} rings with three super-shielded rails - one "
            f"per playable domain - braided along it, {c['prismCount']} (6,6,8) prisms on a "
            f"{c['spineLength']:.0f}u spine, mouths {c['ringRadius']:.0f}u, no corner under "
            f"{c['cornerFloor']:.0f}u. The standard nucleus is the crystal respawn volume and the "
            f"inner shell; the spawn profile is the Barren cell's (no flora, no fauna - shielded mass "
            f"is never food, and a race whose obstacles differ per peer is not a race). "
            f"PhaseThresholds ride the rails' MEASURED volume (Tools/Build/regatta_course_measurements.json) "
            f"plus a trail band - regenerate with Tools/Build/author_regatta_assets.py, never by hand; "
            f"the count x 16 derivation is 18x low here because a (6,6,8) prism is 288 volume.")
    g.emit_asset(f"{CELL_DIR}/Regatta Cell Config {_i}.asset", G_ASSET[f"CellConfig{_i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Regatta Cell Config {_i}") + f"""  CellName: Regatta
  Description: {desc}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {_i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: 346633111830028674, guid: {EXISTING['MembranePrefab']}, type: 3}}
  NucleusPrefab: {{fileID: 7555898194514117247, guid: {EXISTING['NucleusPrefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: 639495419069806261, guid: {EXISTING['CytoplasmPrefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {EXISTING['BarrenSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {{fileID: 5260000000000303, guid: {G_ASSET[f'SpawnableRegattaRails{_i}']}, type: 3}}
  EnvironmentIntensity: {_i}
  PhaseThresholds:
""" + "".join(f"    {k}: {v}\n" for k, v in t.items()))

# ── 5. The arcade card: every playable hull, and the starting-element table ──
HULL_ORDER = ["Manta", "Dolphin", "Rhino", "Urchin", "Squirrel", "Serpent", "Sparrow", "Scarab"]
VESSEL_ROWS = "".join(f"  - {{fileID: 11400000, guid: {EXISTING[f'Vessel_{h}']}, type: 2}}\n" for h in HULL_ORDER)


def starting_element_rows() -> str:
    """One row per (hull, intensity) the model tunes off rest; a hull at rest everywhere gets no
    row (the platform default IS rest, and a row of zeros would say the card decided it)."""
    rows = []
    for i in range(1, 5):
        for h in HULL_ORDER:
            lv = BALANCE[i]["levels"][h]
            if lv == 0:
                continue
            rows.append(f"  - Class: {lib.VESSEL_CLASS_ID[h]}\n    Intensity: {i}\n    Levels:\n"
                        f"      Mass: 0\n      Charge: 0\n      Space: 0\n      Time: {lib.num(lv / 10.0)}\n")
    return "".join(rows) if rows else " []\n"


STARTING = starting_element_rows()
g.emit_asset("Assets/_SO_Assets/Games/ArcadeGameRegatta.asset", G_ASSET["ArcadeGameRegatta"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameRegatta") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Regatta
  Description: Every hull, one circuit, three laps. Three rails run the racing line in the
    three team colours - an Urchin grinds the one in its colour and a Squirrel skims it,
    while a Manta, a Rhino, a Scarab or a Sparrow flies beside it. Thread the rings in
    order; first team to put a pilot through the last one takes it. Pick the hull you fly
    best - the card hands the slow ones a head start in Time.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameRegatta
  Vessels:
{VESSEL_ROWS}  MinPlayersAllowed: 2
  MaxPlayersAllowed: 6
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ArenaRules: 1
  Tips:
  - The rail in YOUR colour is the racing line. Urchins latch onto it, Squirrels skim it.
  - A rail cannot be shot away. Only an energised Rhino sword opens a hole, and riders bridge holes.
  - Rhino and Manta pilots: every corner is a price. Lift for the tight ones, wind back up on the straight.
  - Slow hulls start with Time already high - it is your boost. Fast hulls start with it low.
  ViewUserAction: 0
  PlayUserAction: 0
  StartingElements:
{STARTING}  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 6. Toasts: two idle hints and the comeback line ─────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Regatta.asset", G_ASSET["GameToastConfigRegatta"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Regatta") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(110, "The rail in your colour is the racing line - and it cannot be shot away", idle=1, idle_seconds=25) +
             lib.toast(111, "Urchins: latch on and ride. Squirrels: skim it for boost. Everyone else: fly beside it", idle=1, idle_seconds=50) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigRegatta"])

# ── 7. Mode preview ──────────────────────────────────────────────────────────
PREVIEW_CELLS = "".join(f"  - {{fileID: 11400000, guid: {G_ASSET[f'CellConfig{i}']}, type: 2}}\n" for i in range(1, 5))
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Regatta.asset", G_ASSET["ModePreviewRegatta"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Regatta") + f"""  Mode: {MODE_ID}
  Notes: 'Every intensity is a different circuit (its own seed, mouth and corner floor), so the
    scale model rebuilds when the intensity row moves. An arena card: the flight preview
    flies whichever hull the pilot picked in the carousel. The rings are laid by the
    controller and are not in the scale model; the rails are.'
  PreviewCell: {{fileID: 11400000, guid: {G_ASSET['CellConfig1']}, type: 2}}
  PreviewCellsByIntensity:
{PREVIEW_CELLS}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: -1
  ObjectiveText: Thread the rings in order, laps of the circuit
  ObjectiveMetric: 9
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 150
  SpawnRingRadiusFloor: 0
  SpawnFormation: 1
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewRegatta"])

# ── 8. Scene: clone MinigameRedline, swap the mode-specific wiring ───────────
scene = g.read(f"{lib.SCENES_DIR}/MinigameRedline.unity")
scene = lib.swap_guid(scene, EXISTING["RedlineController"], G_SCRIPT["RegattaController"], "controller")

OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['RedlineScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: {EXISTING['RuntimeCellData']}, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: 3
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 420
  aiApproachLead: 480
  aiThroughDistance: 320
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: 1400
  reportResyncSeconds: 3
"""
# The lap count is DERIVED (target / rings) so `laps` goes; the start line and the rail aim
# are this mode's own. Everything else is the platform's, verbatim: the AI numbers are sized to
# a Manta's full-boost circle, which is the tightest line any hull here holds at speed, and the
# 1400 speed clamp clears a Rhino's 1200 u/s (40 u per 30 fps step against a 98 u line).
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['RegattaScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: {EXISTING['RuntimeCellData']}, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 420
  aiApproachLead: 480
  aiThroughDistance: 320
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: 1400
  reportResyncSeconds: 3
  arenaCell: {{fileID: 1700000065}}
  startLineStandoff: 260
  startLineRadius: 120
  aiRailLeadDistance: 260
"""
scene = lib.replace_block(scene, OLD_FIELDS, NEW_FIELDS, "controller fields")

# THE CELL becomes INTENSITY-WISE over the four rail arenas.
scene = lib.replace_block(scene,
    f"  CellConfigs:\n  - {{fileID: 11400000, guid: {EXISTING['SkimRaceCellConfig']}, type: 2}}\n  cellTypeChoiceOptions: 0\n",
    "  CellConfigs:\n" + "".join(f"  - {{fileID: 11400000, guid: {G_ASSET[f'CellConfig{i}']}, type: 2}}\n" for i in range(1, 5))
    + "  cellTypeChoiceOptions: 1\n",
    "cell config")

# THE AI ROSTER draws its hull from the CARD: vesselClass 0 (Random) makes
# ServerPlayerVesselInitializerWithAI.PickAIVesselType roll uniformly over the card's Vessels,
# so a bot grid is a mixed grid too. (The donor's 2 = Dolphin, Headlong's inheritance from
# Switchback, was clamped to the Manta by Redline's card; here it would pin every bot to one hull.)
scene, n = re.subn(r"^  - vesselClass: 2\n(    PlayerName: AI \d)", r"  - vesselClass: 0\n\1", scene, flags=re.M)
assert n == 4, f"AI templates swapped {n} times (expected 4)"

# Sanity: everything else the donor authored is what this mode wants.
for probe, why in ((r"^  spawnFormation: 1$", "equatorial spawn ring (overridden by the start line)"),):
    assert re.search(probe, scene, re.M), f"donor no longer provides: {why}"
g.emit_scene("MinigameRegatta", G_ASSET["MinigameRegatta.unity"], scene)

# ── 9-12. The shared registries ──────────────────────────────────────────────
g.register_arena_card(G_ASSET["ArcadeGameRegatta"])
g.register_always_unlocked()
g.register_build_scene("MinigameRedline", "MinigameRegatta", G_ASSET["MinigameRegatta.unity"])
g.set_end_condition("regattaGateTarget", after="redlineGateTarget", value=GATE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

if COURSE_STALE:
    errors.append("regatta_course_measurements.json is STALE: its sourceHash does not match the pure course "
                  "files. Re-run Tools/Build/regatta_course_harness/run.sh and commit the JSON.")

if 0.25 * GATE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {GATE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * GATE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
if GATE_TARGET % RINGS_PER_LAP != 0:
    errors.append(f"race target {GATE_TARGET} is not a whole number of {RINGS_PER_LAP}-ring laps")

_end = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultRegattaGateTarget\s*=\s*(\d+);", _end)
if not m or int(m.group(1)) != GATE_TARGET:
    errors.append(f"EndConditionOverridesSO.DefaultRegattaGateTarget is not {GATE_TARGET}")

# The rails' geometry must be ONE set of numbers in the prefab, the C# and this script.
_course = g.read(SCRIPT_PATHS["RegattaCourse"])
for name, want in (("RingsPerLap", RINGS_PER_LAP),):
    m = re.search(rf"public const int {name} = (\d+);", _course)
    if not m or int(m.group(1)) != want:
        errors.append(f"RegattaCourse.{name} is not {want}")
for name, want in (("LaneOffset", LANE_OFFSET), ("TwistTurnsPerLap", LANE_TWIST), ("PrismSpacing", PRISM_SPACING)):
    m = re.search(rf"{name} = ([0-9.]+)f,", _course)
    if not m or abs(float(m.group(1)) - want) > 1e-6:
        errors.append(f"RegattaCourse.DefaultRails.{name} is not {want}")
_rails = g.read(SCRIPT_PATHS["SpawnableRegattaRails"])
m = re.search(r"prismScale = new\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)", _rails)
if not m or tuple(float(x) for x in m.groups()) != PRISM_SCALE:
    errors.append(f"SpawnableRegattaRails.prismScale default is not {PRISM_SCALE}")

# The measured course must be the shipped one: eight rings per intensity, lanes inside the
# tightest mouth with a prism to spare, lanes equal to 3%, the ladder ordered.
prev_prisms = None
for i in range(1, 5):
    c = COURSE["intensities"][str(i)]
    if c["rings"] != RINGS_PER_LAP:
        errors.append(f"intensity {i}: measured {c['rings']} rings, expected {RINGS_PER_LAP}")
    if LANE_OFFSET + PRISM_SCALE[0] > c["ringRadius"]:
        errors.append(f"intensity {i}: lanes at {LANE_OFFSET}u do not clear the {c['ringRadius']}u mouth by a prism")
    lo, hi = min(c["laneLengths"]), max(c["laneLengths"])
    if (hi - lo) / lo > 0.03:
        errors.append(f"intensity {i}: lane lengths spread {(hi - lo) / lo:.1%} (> 3%) - a domain races shorter")
    if c["minLaneSeparation"] < 3 * PRISM_SCALE[0]:
        errors.append(f"intensity {i}: lanes come within {c['minLaneSeparation']:.1f}u - fused super-shield cables")
    t = phase_thresholds(prisms_at(i))
    if not (t["RestlessExit"] < t["RestlessEnter"] < t["FrenzyExit"] < t["FrenzyEnter"]):
        errors.append(f"intensity {i}: count ladder is not ordered")
    if not (t["RestlessExitVolume"] < t["RestlessEnterVolume"] < t["FrenzyExitVolume"] < t["FrenzyEnterVolume"]):
        errors.append(f"intensity {i}: volume ladder is not ordered")
    if t["RestlessEnterVolume"] <= prisms_at(i) * PRISM_VOLUME:
        errors.append(f"intensity {i}: Restless sits under the rails' own volume")

# The balance the model reaches, asserted so a vessel retune that breaks it is caught here.
for i in range(1, 5):
    b = BALANCE[i]
    if b["spread"] > MAX_TUNED_SPREAD:
        errors.append(f"intensity {i}: tuned lap-time spread {b['spread']:.2f}x exceeds {MAX_TUNED_SPREAD}x - "
                      "re-derive the starting elements or the course (regatta_balance.py)")
    if b["spread"] > b["restSpread"] + 1e-9:
        errors.append(f"intensity {i}: tuning made the spread WORSE ({b['restSpread']:.2f}x -> {b['spread']:.2f}x)")
    for h, lv in b["levels"].items():
        if not (RB.MIN_LEVEL <= lv <= RB.MAX_LEVEL):
            errors.append(f"intensity {i}: {h} tuned to level {lv}, outside the resource band")

# The cloned scene must no longer mention the donor's wiring, and must mention ours.
sc = g.files[f"{lib.SCENES_DIR}/MinigameRegatta.unity"]
for name in ("RedlineController", "RedlineScoringRule", "SkimRaceCellConfig"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
if G_SCRIPT["RegattaController"] not in sc:
    errors.append("cloned scene missing RegattaController")
if sc.count("  - vesselClass: 0\n") != 4:
    errors.append("cloned scene does not carry 4 Random-hull AI templates")
if sc.count("  cellTypeChoiceOptions: 1\n") != 1:
    errors.append("cloned scene is not IntensityWise")

# The card lists every playable hull exactly once, and every StartingElements row names one of them.
card = g.files["Assets/_SO_Assets/Games/ArcadeGameRegatta.asset"]
for h in HULL_ORDER:
    if card.count(EXISTING[f"Vessel_{h}"]) != 1:
        errors.append(f"card does not list {h} exactly once")
for m in re.finditer(r"^  - Class: (\d+)$", card, re.M):
    if int(m.group(1)) not in {lib.VESSEL_CLASS_ID[h] for h in HULL_ORDER}:
        errors.append(f"StartingElements names class {m.group(1)}, which the card does not seat")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/" + SCRIPTS_DIR), guid("text/" + DOC), guid("folder/" + CELL_DIR)])

if not lib.CHECK_ONLY:
    print()
    print(RB.describe(BALANCE))
