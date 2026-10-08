#!/usr/bin/env python3
"""
Authors every serialized asset the Grizzly Time game mode needs; the mode id is READ from
GameModes.cs rather than hardcoded (GrizzlyCharge alone moved 42 -> 44 -> 54 -> 62).

Built on arcade_mode_lib: deterministic guids (md5("CosmicShore/<stable name>")), the whole
result validated in memory before a byte is written, and --check diffs every file.

Run from the repo root:  python3 Tools/Build/author_grizzly_time_assets.py [--check]

WHAT THIS MODE IS. Grizzly Time is the Grizzly-only CIRCUIT race: a closed loop of switch rings
cut through the cell, flown in LAPS, and the first DOMAIN whose LEAD RUNNER threads the last gate
of the last lap wins (golf: finish time). Every corner is cut against the Grizzly's FULL-LAUNCH
circle - 350 u/s on its 95 deg/s turn, ~211 u - so a corner is one question: how much of a launch
(riding your own trigger-bomb blast) is it worth? See Assets/_Scripts/Controller/Arcade/GRIZZLYTIME.md.

THE DONOR IS REDLINE, and that is the point rather than a shortcut. Redline is already a lapped
gate race in the barren race cell with an EQUATORIAL spawn ring (gate 0 on its pole - the fairness
rule), on the shared GateRaceController with a RaceGateTurnMonitor, a RaceGateObjectiveProvider
and the generic GateRaceScoringRuleSO. The clone swaps FIVE things and inherits the rest:

  1. the controller script (GrizzlyTimeController), keeping Redline's `laps` field (3, as Redline's).
  2. its scoring rule ASSET (another asset on the same GateRaceScoringRuleSO script).
  3. the AI approach numbers and the plausible-speed clamp, scaled to the Grizzly's 211 u circle
     (Redline's were sized to the Manta's 237 u).
  4. the scene's Netcode GlobalObjectIdHash values, minted fresh so no in-scene NetworkObject
     shares an id with its donor (the rule the Grizzly Charge scene rebuild adopted).
  5. the arcade card's Vessels list: Grizzly, not Manta.

THE CIRCUIT GENERATOR IS SHARED, NOT CLONED. GrizzlyTimeCourse supplies the Grizzly's settings to
HeadlongCircuit.Generate; GrizzlyTimeCourseTests asserts the ladder over 400 seeds.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

ROOT = lib.ROOT

# ── Mode id: READ, never hardcoded ───────────────────────────────────────────
with open(os.path.join(ROOT, "Assets/_Scripts/Data/Enums/GameModes.cs"), encoding="utf-8") as _fh:
    _m = re.search(r"^\s*GrizzlyTime\s*=\s*(\d+)\s*,", _fh.read(), re.M)
assert _m, "GameModes.GrizzlyTime not found - has the member been renamed?"
MODE_ID = int(_m.group(1))

g = lib.Generator(MODE_ID, "GrizzlyTime")
guid = lib.guid

# ── Paths ────────────────────────────────────────────────────────────────────
MODE_DIR = "Assets/_Scripts/Controller/Arcade/GrizzlyTime"
SCRIPT_PATHS = {
    "GrizzlyTimeController":  f"{MODE_DIR}/GrizzlyTimeController.cs",
    "GrizzlyTimeCourse":      f"{MODE_DIR}/GrizzlyTimeCourse.cs",
    "GrizzlyTimeCourseTests": "Assets/_Scripts/Tests/Editor/GrizzlyTimeCourseTests.cs",
}
DOC = "Assets/_Scripts/Controller/Arcade/GRIZZLYTIME.md"
CARD = "Assets/_SO_Assets/Games/ArcadeGameGrizzlyTime.asset"
RULE = "Assets/_SO_Assets/Scoring Rules/GrizzlyTimeScoringRule.asset"
PREVIEW = "Assets/_SO_Assets/Mode Previews/ModePreview_GrizzlyTime.asset"
SCENE_NAME = "MinigameGrizzlyTime"
SCENE = f"{lib.SCENES_DIR}/{SCENE_NAME}.unity"
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameRedline.unity"

# ── Minted GUIDs ─────────────────────────────────────────────────────────────
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_CARD = guid("asset/ArcadeGameGrizzlyTime")
G_RULE = guid("asset/GrizzlyTimeScoringRule")
G_PREVIEW = guid("asset/ModePreview_GrizzlyTime")
G_SCENE = guid("asset/MinigameGrizzlyTime.unity")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":            lib.SCRIPT_GUIDS["SO_ArcadeGame"],
    "ModePreviewDefinitionSO":  lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"],
    # the shared gate-race scoring rule SCRIPT (one class, one asset per mode)
    "GateRaceScoringRuleSO":    "349cc0c9402590262de23356775d43cc",
    # donor scene wiring to swap out - Redline's controller and rule, minted by ITS generator
    "RedlineController":        guid("script/RedlineController"),
    "RedlineScoringRule":       guid("asset/RedlineScoringRule"),
    # shared content
    "Vessel_Grizzly":           lib.VESSELS["Grizzly"],
    "BarrenRaceCell":           "8d4e8398eedc76c4dadb8604f89b9e1b",
    "PreviewCell":              "52de45c2fab54ea0a35bc37a311d80e6",
    "IconActive":               lib.CARD_ART["IconActive"],
    "IconInactive":             lib.CARD_ART["IconInactive"],
}

# ── Tuning ───────────────────────────────────────────────────────────────────

# The RACE LENGTH in gate threadings - LAPS x RINGS. One number: the controller divides by LAPS
# to size the circuit and the turn monitor asks the controller for the target, so the finish line
# and the course cannot be different lengths. Kept in sync with
# EndConditionOverridesSO.DefaultGrizzlyTimeGateTarget.
LAPS = 3
RINGS_PER_LAP = 8                 # GrizzlyTimeCourse.GatesPerLap (14 until the launch was tripled)
GATE_TARGET = LAPS * RINGS_PER_LAP
BASE_RADIUS = 800                 # GrizzlyTimeCourse.BaseRadius

# AI approach geometry, sized to the Grizzly's 211 u full-launch circle the way Redline's were
# sized to the Manta's 237 u (420 / 480 / 320 there - 1.8x, 2x and 1.35x the circle).
AI_COMMIT_DISTANCE = 380
AI_APPROACH_LEAD = 420
AI_THROUGH_DISTANCE = 285

# Detection clamp: a frame's motion longer than this x dt x 2 + 5 is read as a respawn. A launching
# Grizzly makes 350 u/s (cruise + the bomb launch's own 300 u/s ceiling; Rush and the cannon stay
# under the shared 100), so 1400 is four times anything the hull can fly and still far under a
# respawn's jump.
MAX_PLAUSIBLE_SPEED = 1400

# The comeback strength, a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`): at 0.3 a
# quarter-of-race deficit (6 gates) buys 1.8 element levels - the 1.75 it bought on the old
# 28-gate race. The Grizzly's Time element scales Rush, its burst onto a straight, so the buff
# lands on the mode's axis.
COMEBACK_RATE = 0.3

# Fresh Netcode ids for the clone's in-scene NetworkObjects (rule 4 above).
HASH_SALT = "MinigameGrizzlyTime"


def header(script_guid: str, name: str) -> str:
    return lib.header_for(script_guid, name)


# ── 1. Metas for the scripts, the mode folder and the doc ────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(MODE_DIR)
g.text_meta(DOC)

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.SwitchesThreaded, REUSED as Headlong and Redline reuse it: the metric,
# the BestByDomain fold, the goal-stack row and the objective icon all come free. Golf: the
# winning domain's pilots carry a finish time, everyone else a sentinel.
g.emit_asset(RULE, G_RULE, header(EXISTING["GateRaceScoringRuleSO"], "GrizzlyTimeScoringRule") +
             "  metric: 9\n  golfRules: 1\n")

# ── 3. Arcade card ───────────────────────────────────────────────────────────
# GRIZZLY ONLY: one entry in Vessels drives all three enforcement layers (launcher clamp,
# server-side spawn clamp, AI clamp). 2 / 2 minimum: a race needs a rival.
g.emit_asset(CARD, G_CARD, header(EXISTING["SO_ArcadeGame"], "ArcadeGameGrizzlyTime") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Grizzly Time
  Description: Grizzlies only, against the clock. Your speed is your bombs - squeeze LT or
    RT to fire, pull again to freeze it, let go to blow it and ride the blast at three times
    cruise. A launch keeps going the way you were pointed, so every corner asks how much
    launch it is worth. Two laps. Fastest domain home wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background('GrizzlyTime')}, type: 3}}
  GolfScoring: 1
  SceneName: {SCENE_NAME}
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Grizzly']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")

# ── 4. Scene: clone MinigameRedline ──────────────────────────────────────────
OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['RedlineScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: {EXISTING['BarrenRaceCell']}, type: 2}}
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
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_RULE}, type: 2}}
  cellData: {{fileID: 11400000, guid: {EXISTING['BarrenRaceCell']}, type: 2}}
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


def project_network_hashes() -> "set[int]":
    """Every GlobalObjectIdHash any scene or prefab already carries."""
    seen = set()
    for dirpath, _dirs, files in os.walk(os.path.join(ROOT, "Assets")):
        for fn in files:
            if not fn.endswith((".unity", ".prefab")):
                continue
            full = os.path.join(dirpath, fn)
            if os.path.normpath(full) == os.path.normpath(os.path.join(ROOT, SCENE)):
                continue
            with open(full, encoding="utf-8", errors="ignore") as fh:
                seen.update(int(h) for h in re.findall(r"GlobalObjectIdHash: (\d+)", fh.read()))
    return seen


def clone_scene() -> str:
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, EXISTING["RedlineController"], G_SCRIPT["GrizzlyTimeController"],
                          "controller script")
    scene = lib.replace_block(scene, OLD_FIELDS, NEW_FIELDS, "controller field block")

    # Everything ELSE the donor authored is what this mode wants, stated rather than left as an
    # absence: the barren race cell (intensity is the COURSE), the EQUATORIAL spawn ring (gate 0
    # on its pole), one cell config. The AI roster's vesselClass is irrelevant - the card's
    # Vessels list clamps every AI to the Grizzly (GameDataSO.ClampVesselToGame).
    for probe, why in ((r"^  spawnFormation: 1$", "equatorial spawn ring"),
                       (r"^  cellTypeChoiceOptions: 0$", "single race cell")):
        assert re.search(probe, scene, re.M), f"donor no longer provides: {why}"

    # Fresh, deterministic, project-unique Netcode ids.
    taken = project_network_hashes()
    donor_hashes = sorted({int(h) for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene)} - {0})
    assert donor_hashes, "donor scene has no in-scene NetworkObjects - has it changed shape?"
    for i, old in enumerate(donor_hashes):
        n = int(guid(f"{HASH_SALT}/hash/{i}")[:8], 16)
        while n == 0 or n in taken:
            n = (n * 1103515245 + 12345) & 0xFFFFFFFF
        taken.add(n)
        scene = lib.swap_guid(scene, f"GlobalObjectIdHash: {old}\n", f"GlobalObjectIdHash: {n}\n",
                              f"network hash {old}", expected=scene.count(f"GlobalObjectIdHash: {old}\n"))
    return scene


scene, scene_errors = lib.committed_scene(
    SCENE, clone_scene,
    authored_blocks=(NEW_FIELDS, "  spawnDistanceOutsideNucleus: 150\n", "  spawnFormation: 1\n"))
g.emit_scene(SCENE_NAME, G_SCENE, scene)

# ── 5. Preview (a card without one offers no Test Flight) ────────────────────
g.emit_asset(PREVIEW, G_PREVIEW, header(EXISTING["ModePreviewDefinitionSO"], "ModePreview_GrizzlyTime") + f"""  Mode: {MODE_ID}
  Notes: 'OPEN-ENDED, shell-only: Headlong''s solver cut to the Grizzly''s full-launch circle, laid
    as rings at match start, so a preview has no gates. It teaches the Barren cell and the
    LT/RT bomb-jump. One cell serves every intensity.'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['PreviewCell']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {lib.VESSEL_CLASS_ID['Grizzly']}
  ObjectiveText: Ride your blasts down the straights
  ObjectiveMetric: 9
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 150
  SpawnRingRadiusFloor: 0
  SpawnFormation: 1
  SpawnPoints: []
""")
g.register_preview(G_PREVIEW)

# ── 6. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_CARD)
g.register_always_unlocked()
g.register_build_scene("MinigameRedline", SCENE_NAME, G_SCENE)
g.set_end_condition("grizzlyTimeGateTarget", after="redlineGateTarget", value=GATE_TARGET)


# ══ VALIDATE ════════════════════════════════════════════════════════════════
errors = list(scene_errors)

if 0.25 * GATE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(
        f"comeback rate {COMEBACK_RATE} is dead against target {GATE_TARGET}: a quarter-of-target "
        f"deficit buys {0.25 * GATE_TARGET * COMEBACK_RATE:.2f} element levels (< 1).")
if GATE_TARGET % LAPS != 0:
    errors.append(f"race target {GATE_TARGET} is not a whole number of {LAPS} laps")


def read_or_empty(rel):
    full = os.path.join(ROOT, rel)
    return open(full, encoding="utf-8").read() if os.path.exists(full) else ""


_course = read_or_empty(SCRIPT_PATHS["GrizzlyTimeCourse"])
for const, want in (("GatesPerLap", RINGS_PER_LAP), ("BaseRadius", BASE_RADIUS)):
    m = re.search(rf"public const (?:int|float) {const} = (\d+)f?;", _course)
    if not m or int(m.group(1)) != want:
        errors.append(f"GrizzlyTimeCourse.{const} is not {want}")

_end = read_or_empty("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
for pat, what in ((r"DefaultGrizzlyTimeGateTarget\s*=\s*(\d+);", "DefaultGrizzlyTimeGateTarget"),
                  (r"public int grizzlyTimeGateTarget\s*=\s*(\d+);", "grizzlyTimeGateTarget initializer"),
                  (r"public int grizzlyTimeGateTargetBuild\s*=\s*(\d+);", "grizzlyTimeGateTargetBuild initializer")):
    m = re.search(pat, _end)
    if not m or int(m.group(1)) != GATE_TARGET:
        errors.append(f"EndConditionOverridesSO.{what} is not {GATE_TARGET}")

_ctrl = read_or_empty(SCRIPT_PATHS["GrizzlyTimeController"])
m = re.search(r"int laps\s*=\s*(\d+);", _ctrl)
if not m or int(m.group(1)) != LAPS:
    errors.append(f"GrizzlyTimeController.laps default is not {LAPS}")

_tests = read_or_empty(SCRIPT_PATHS["GrizzlyTimeCourseTests"])
for const, want in (("Inner", 480.0), ("Outer", 1080.0)):
    m = re.search(rf"const float {const} = ([0-9.]+)f;", _tests)
    if not m or float(m.group(1)) != want:
        errors.append(f"GrizzlyTimeCourseTests.{const} is not {want} (the scene's course shell)")

# The scene must carry the Grizzly Time controller, not the donor's, and its network ids must be
# unique across the project.
if G_SCRIPT["GrizzlyTimeController"] not in scene or EXISTING["RedlineController"] in scene:
    errors.append("scene does not carry exactly the GrizzlyTimeController script")
_taken = project_network_hashes()
for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene):
    if int(h) != 0 and int(h) in _taken:
        errors.append(f"scene GlobalObjectIdHash {h} is also carried by another scene or prefab")

# The AI must actually be able to drive this hull: an AIPilot serialized disabled never runs
# Update, so an AI Grizzly would not steer (the shape Grizzly.prefab shipped in until this mode).
_grizzly = read_or_empty("Assets/_Prefabs/Spacevessels/Grizzly.prefab")
_ai = re.search(r"  m_Enabled: (\d)\n  m_EditorHideFlags: 0\n  m_Script: \{fileID: 11500000, "
                r"guid: a58bf4fb65afa704194fe9e28e67d58d, type: 3\}", _grizzly)
if not _ai or _ai.group(1) != "1":
    errors.append("Grizzly.prefab's AIPilot is not enabled - an AI Grizzly cannot race")
_bombs = read_or_empty("Assets/_SO_Assets/VesselActions/Grizzly/GrizzlyTriggerBombConfig.asset")
m = re.search(r"^  aiFireStickBand: ([0-9.]+)$", _bombs, re.M)
if not m or float(m.group(1)) <= 0:
    errors.append("GrizzlyTriggerBombConfig.aiFireStickBand is 0 or missing - an AI Grizzly never bombs, "
                  "so it is never launched and races at cruise")
# The launch only happens if the hull is INSIDE its own blast when the autopilot detonates.
_freeze = re.search(r"^  aiFreezeDistance: ([0-9.]+)$", _bombs, re.M)
_blast = re.search(r"^  maxBlastScale: ([0-9.]+)$", _bombs, re.M)
if not _freeze or not _blast or float(_freeze.group(1)) >= float(_blast.group(1)) * 0.5:
    errors.append("GrizzlyTriggerBombConfig.aiFreezeDistance is not inside the blast radius (maxBlastScale / 2) - "
                  "an AI Grizzly's launch would miss its own hull")

# The toybox must offer the hull this mode is built for (ToyVesselRosterCoverageTests asserts the
# general rule; this names it for the mode that needs it).
_roster = read_or_empty("Assets/_Scripts/Controller/Toys/ToyVesselRoster.cs")
if "VesselClassType.Grizzly" not in _roster:
    errors.append("ToyVesselRoster.Default does not offer the Grizzly")

minted = list(G_SCRIPT.values()) + [G_CARD, G_RULE, G_PREVIEW, G_SCENE,
                                    guid(f"folder/{MODE_DIR}"), guid(f"text/{DOC}")]
g.finish(errors, referenced=EXISTING, minted=minted)
