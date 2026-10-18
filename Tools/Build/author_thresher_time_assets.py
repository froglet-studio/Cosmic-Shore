#!/usr/bin/env python3
"""
Authors every serialized asset the Thresher Time game mode needs; the mode id is READ from
GameModes.cs rather than hardcoded (parallel branches have collided on "the next free id" six
times - GrizzlyCharge alone moved 42 -> 44 -> 54 -> 62).

Built on arcade_mode_lib: deterministic guids (md5("CosmicShore/<stable name>")), the whole
result validated in memory before a byte is written, and --check diffs every file.

Run from the repo root:  python3 Tools/Build/author_thresher_time_assets.py [--check]

WHAT THIS MODE IS. Thresher Time is the Thresher-only CIRCUIT race: a closed loop of switch rings
cut through the cell, flown in LAPS, and the first DOMAIN whose LEAD RUNNER threads the last gate
of the last lap wins (golf: finish time). Every corner is cut against the Thresher's FULL-SLING
circle - the ball's 479 u/s ceiling on its flat 120 deg/s turn, ~229 u - so a corner is one
question: how much of a sling (plant the ball, orbit it, wind the chain in, let go) is it worth?
See Assets/_Scripts/Controller/Arcade/THRESHERTIME.md.

THE DONOR IS GRIZZLY TIME, which is itself Redline's scene: a lapped gate race in the barren race
cell with an EQUATORIAL spawn ring (gate 0 on its pole - the fairness rule), on the shared
GateRaceController with a RaceGateTurnMonitor, a RaceGateObjectiveProvider and the generic
GateRaceScoringRuleSO. The clone swaps FIVE things and inherits the rest:

  1. the controller script (ThresherTimeController), keeping the donor's `laps` field (3).
  2. its scoring rule ASSET (another asset on the same GateRaceScoringRuleSO script).
  3. the AI approach numbers and the plausible-speed clamp - the approach sized to the only
     circle an AI Thresher flies (cruise: it never slings), the clamp to cover a boosted aimed launch.
  4. the scene's Netcode GlobalObjectIdHash values, minted fresh so no in-scene NetworkObject
     shares an id with its donor.
  5. the arcade card's Vessels list: Thresher, not Grizzly.

THE CIRCUIT GENERATOR IS SHARED, NOT CLONED. ThresherTimeCourse supplies the Thresher's settings to
HeadlongCircuit.Generate; ThresherTimeCourseTests asserts the ladder over 400 seeds.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

ROOT = lib.ROOT

# ── Mode id: READ, never hardcoded ───────────────────────────────────────────
with open(os.path.join(ROOT, "Assets/_Scripts/Data/Enums/GameModes.cs"), encoding="utf-8") as _fh:
    _m = re.search(r"^\s*ThresherTime\s*=\s*(\d+)\s*,", _fh.read(), re.M)
assert _m, "GameModes.ThresherTime not found - has the member been renamed?"
MODE_ID = int(_m.group(1))

g = lib.Generator(MODE_ID, "ThresherTime")
guid = lib.guid

# ── Paths ────────────────────────────────────────────────────────────────────
MODE_DIR = "Assets/_Scripts/Controller/Arcade/ThresherTime"
SCRIPT_PATHS = {
    "ThresherTimeController":  f"{MODE_DIR}/ThresherTimeController.cs",
    "ThresherTimeCourse":      f"{MODE_DIR}/ThresherTimeCourse.cs",
    "ThresherTimeCourseTests": "Assets/_Scripts/Tests/Editor/ThresherTimeCourseTests.cs",
}
DOC = "Assets/_Scripts/Controller/Arcade/THRESHERTIME.md"
CARD = "Assets/_SO_Assets/Games/ArcadeGameThresherTime.asset"
RULE = "Assets/_SO_Assets/Scoring Rules/ThresherTimeScoringRule.asset"
PREVIEW = "Assets/_SO_Assets/Mode Previews/ModePreview_ThresherTime.asset"
TOASTS = "Assets/_SO_Assets/Game Toasts/GameToastConfig_ThresherTime.asset"
SCENE_NAME = "MinigameThresherTime"
SCENE = f"{lib.SCENES_DIR}/{SCENE_NAME}.unity"
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameGrizzlyTime.unity"
THRESHER_PREFAB = "Assets/_Prefabs/Spacevessels/Thresher.prefab"
THRESHER_CONFIG = "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset"

# ── Minted GUIDs ─────────────────────────────────────────────────────────────
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_CARD = guid("asset/ArcadeGameThresherTime")
G_RULE = guid("asset/ThresherTimeScoringRule")
G_PREVIEW = guid("asset/ModePreview_ThresherTime")
G_TOASTS = guid("asset/GameToastConfig_ThresherTime")
G_SCENE = guid("asset/MinigameThresherTime.unity")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":            lib.SCRIPT_GUIDS["SO_ArcadeGame"],
    "ModePreviewDefinitionSO":  lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"],
    "GameToastConfigSO":        lib.SCRIPT_GUIDS["GameToastConfigSO"],
    # the shared gate-race scoring rule SCRIPT (one class, one asset per mode)
    "GateRaceScoringRuleSO":    "349cc0c9402590262de23356775d43cc",
    # donor scene wiring to swap out - Grizzly Time's controller and rule, minted by ITS generator
    "GrizzlyTimeController":    guid("script/GrizzlyTimeController"),
    "GrizzlyTimeScoringRule":   guid("asset/GrizzlyTimeScoringRule"),
    # shared content
    "Vessel_Thresher":          lib.VESSELS["Thresher"],
    "BarrenRaceCell":           "8d4e8398eedc76c4dadb8604f89b9e1b",
    "PreviewCell":              "52de45c2fab54ea0a35bc37a311d80e6",
    "IconActive":               lib.CARD_ART["IconActive"],
    "IconInactive":             lib.CARD_ART["IconInactive"],
}
assert lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_Thresher.asset") == EXISTING["Vessel_Thresher"], \
    "arcade_mode_lib.VESSELS['Thresher'] is not SO_Class_Thresher.asset's guid"

# ── Tuning ───────────────────────────────────────────────────────────────────

# The RACE LENGTH in gate threadings - LAPS x RINGS. One number: the controller divides by LAPS
# to size the circuit and the turn monitor asks the controller for the target, so the finish line
# and the course cannot be different lengths. Kept in sync with
# EndConditionOverridesSO.DefaultThresherTimeGateTarget.
LAPS = 3
RINGS_PER_LAP = 8                 # ThresherTimeCourse.GatesPerLap
GATE_TARGET = LAPS * RINGS_PER_LAP
BASE_RADIUS = 800                 # ThresherTimeCourse.BaseRadius

# AI approach geometry. An AI Thresher NEVER SLINGS (AIPilot writes the sticks and the throttle and
# no trigger, and the Thresher's sling is LT + RT), so it races at cruise - at most 0.9 throttle,
# 114 u/s, on a <= 60 u circle - and the approach is sized to THAT circle, not to the 229 u
# full-sling circle the course is cut against: Slingshot's ratios (commit 2.2x, lead 2.5x, through
# 1.7x its 72 u circle) on the Thresher's 60 u cruise circle. Grizzly Time's 380 / 420 / 285 were
# sized for an AI that rides its own launches.
CRUISE_RADIUS = 125.0 / (120.0 * 3.141592653589793 / 180.0)   # ThresherTimeCourse.CruiseRadius
AI_COMMIT_DISTANCE = 135
AI_APPROACH_LEAD = 150
AI_THROUGH_DISTANCE = 100

# Detection clamp: a frame's motion longer than this x dt x 2 + 5 is read as a respawn. The fastest a
# Thresher can leave a sling is the ball's 479 u/s ceiling x the 4.8 launch boost EVERY aimed launch
# takes = 2299 u/s (ThresherTimeCourse.BoostedSlingSpeed; until 2026-10-13 it was Time L5's x1.6
# Perfect Release, 766 u/s, and the clamp was 1600); the skim boost is a separate, slower channel (5x
# the 110 throttle span, 565 u/s). 4600 covers twice the boosted sling - a step of ~158 u at 60 fps,
# still under a respawn's jump onto an 800 u circuit.
MAX_PLAUSIBLE_SPEED = 4600
BOOSTED_SLING_SPEED = 2600 * 70 / 380 * 4.8

# The comeback strength, a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`): at 0.3 a
# quarter-of-race deficit (6 gates) buys 1.8 element levels, Grizzly Time's number on the same
# 24-gate race. The Thresher's Time element scales the Plant's spin-up rate and cap (x1 -> x1.75),
# so a trailing pilot winds a sling up to the ceiling sooner - the buff lands on the mode's axis.
COMEBACK_RATE = 0.3

# Fresh Netcode ids for the clone's in-scene NetworkObjects (rule 4 above).
HASH_SALT = "MinigameThresherTime"


def header(script_guid: str, name: str) -> str:
    return lib.header_for(script_guid, name)


# ── 1. Metas for the scripts, the mode folder and the doc ────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(MODE_DIR)
g.text_meta(DOC)

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.SwitchesThreaded, REUSED as Headlong, Redline and Grizzly Time reuse it:
# the metric, the BestByDomain fold, the goal-stack row and the objective icon all come free. Golf:
# the winning domain's pilots carry a finish time, everyone else a sentinel.
g.emit_asset(RULE, G_RULE, header(EXISTING["GateRaceScoringRuleSO"], "ThresherTimeScoringRule") +
             "  metric: 9\n  golfRules: 1\n")

# ── 3. Arcade card ───────────────────────────────────────────────────────────
# THRESHER ONLY: one entry in Vessels drives all three enforcement layers (launcher clamp,
# server-side spawn clamp, AI clamp). 2 / 2 minimum: a race needs a rival.
g.emit_asset(CARD, G_CARD, header(EXISTING["SO_ArcadeGame"], "ArcadeGameThresherTime") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Thresher Time
  Description: Threshers only, against the clock. Your speed is your sling - pull LT all
    the way to plant the ball and swing round it, pull RT to wind the chain in and spin up,
    let go to fly off at up to nearly four times cruise. A sling is your own momentum, so
    every corner asks how much of it you can carry. Three laps. Fastest domain home wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background('ThresherTime')}, type: 3}}
  GolfScoring: 1
  SceneName: {SCENE_NAME}
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Thresher']}, type: 2}}
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

# ── 4. Scene: clone MinigameGrizzlyTime ──────────────────────────────────────
OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['GrizzlyTimeScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: {EXISTING['BarrenRaceCell']}, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: 3
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 380
  aiApproachLead: 420
  aiThroughDistance: 285
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
    scene = lib.swap_guid(scene, EXISTING["GrizzlyTimeController"], G_SCRIPT["ThresherTimeController"],
                          "controller script")
    scene = lib.replace_block(scene, OLD_FIELDS, NEW_FIELDS, "controller field block")

    # Everything ELSE the donor authored is what this mode wants, stated rather than left as an
    # absence: the barren race cell (intensity is the COURSE), the EQUATORIAL spawn ring (gate 0
    # on its pole), one cell config. The AI roster's vesselClass is irrelevant - the card's
    # Vessels list clamps every AI to the Thresher (GameDataSO.ClampVesselToGame).
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
g.emit_asset(PREVIEW, G_PREVIEW, header(EXISTING["ModePreviewDefinitionSO"], "ModePreview_ThresherTime") + f"""  Mode: {MODE_ID}
  Notes: 'OPEN-ENDED, shell-only: Headlong''s solver cut to the Thresher''s full-sling circle, laid
    as rings at match start, so a preview has no gates. It teaches the Barren cell and the
    plant - wind in - let go sling. One cell serves every intensity.'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['PreviewCell']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {lib.VESSEL_CLASS_ID['Thresher']}
  ObjectiveText: Plant, wind in, let go - sling down the straights
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

# ── 5b. Toasts ───────────────────────────────────────────────────────────────
# Redline's set, word for word: the shared race beats GateRaceController already ticks through
# DomainRaceToasts (129 halfway, 130 lead change, 131 home stretch, 132 final lap) and the comeback
# notice. No per-mode situation and no idle hint: a hint would need a situation block of its own,
# and the sling's controls are on the HUD's ability row.
g.emit_asset(TOASTS, G_TOASTS,
             header(EXISTING["GameToastConfigSO"], "GameToastConfig_ThresherTime") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(129, "{0} is halfway home - gate {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - gate {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(131, "{0} is on the home stretch - gate {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(132, "{0} is on the final lap", tint_domain=1, domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_TOASTS)

# ── 6. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_CARD)
g.register_always_unlocked()
g.register_build_scene("MinigameWarpline", SCENE_NAME, G_SCENE)
g.set_end_condition("thresherTimeGateTarget", after="warplineGateTarget", value=GATE_TARGET)


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


_course = read_or_empty(SCRIPT_PATHS["ThresherTimeCourse"])
for const, want in (("GatesPerLap", RINGS_PER_LAP), ("BaseRadius", BASE_RADIUS)):
    m = re.search(rf"public const (?:int|float) {const} = (\d+)f?;", _course)
    if not m or int(m.group(1)) != want:
        errors.append(f"ThresherTimeCourse.{const} is not {want}")

_end = read_or_empty("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
for pat, what in ((r"DefaultThresherTimeGateTarget\s*=\s*(\d+);", "DefaultThresherTimeGateTarget"),
                  (r"public int thresherTimeGateTarget\s*=\s*(\d+);", "thresherTimeGateTarget initializer"),
                  (r"public int thresherTimeGateTargetBuild\s*=\s*(\d+);", "thresherTimeGateTargetBuild initializer")):
    m = re.search(pat, _end)
    if not m or int(m.group(1)) != GATE_TARGET:
        errors.append(f"EndConditionOverridesSO.{what} is not {GATE_TARGET}")
if "GameModes.ThresherTime" not in _end:
    errors.append("EndConditionOverridesSO.TryGetAuthoredTurnTarget has no ThresherTime row")

_ctrl = read_or_empty(SCRIPT_PATHS["ThresherTimeController"])
# Literal, or one hop into the course source's const (the RaceCourseSource refactor).
_laps = lib.cs_field_default(_ctrl, "laps")
if _laps is None or int(float(_laps)) != LAPS:
    errors.append(f"ThresherTimeController.laps default is not {LAPS}")

_tests = read_or_empty(SCRIPT_PATHS["ThresherTimeCourseTests"])
for const, want in (("Inner", 480.0), ("Outer", 1080.0)):
    m = re.search(rf"const float {const} = ([0-9.]+)f;", _tests)
    if not m or float(m.group(1)) != want:
        errors.append(f"ThresherTimeCourseTests.{const} is not {want} (the scene's course shell)")

# The HUD must name the mode's objective provider, or the arrow to YOUR next gate never appears.
if "case GameModes.ThresherTime:" not in read_or_empty("Assets/_Scripts/UI/MiniGameHUD.cs"):
    errors.append("MiniGameHUD.CreateObjectiveProviderForGameMode has no ThresherTime case")

# The scene must carry the Thresher Time controller, not the donor's, and its network ids must be
# unique across the project.
if G_SCRIPT["ThresherTimeController"] not in scene or EXISTING["GrizzlyTimeController"] in scene:
    errors.append("scene does not carry exactly the ThresherTimeController script")
_taken = project_network_hashes()
for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene):
    if int(h) != 0 and int(h) in _taken:
        errors.append(f"scene GlobalObjectIdHash {h} is also carried by another scene or prefab")

# The detection clamp must cover the fastest thing the hull can fly, or a boosted aimed launch reads
# as a respawn and the gate it threads is never credited.
if MAX_PLAUSIBLE_SPEED < 2 * BOOSTED_SLING_SPEED:
    errors.append(f"maxPlausibleSpeed {MAX_PLAUSIBLE_SPEED} is under twice the boosted sling "
                  f"({BOOSTED_SLING_SPEED:.0f} u/s)")
# The AI approach is sized to the cruise circle an AI Thresher actually flies; it must stay
# outside that circle or the pilot orbits its own aim point (AIPilot has no arrive-and-stop).
if not (AI_THROUGH_DISTANCE > CRUISE_RADIUS and AI_COMMIT_DISTANCE > 2 * CRUISE_RADIUS
        and AI_APPROACH_LEAD > AI_COMMIT_DISTANCE):
    errors.append("AI approach geometry is not sized outside the Thresher's cruise circle "
                  f"(commit {AI_COMMIT_DISTANCE} / lead {AI_APPROACH_LEAD} / through "
                  f"{AI_THROUGH_DISTANCE} against {CRUISE_RADIUS:.0f} u)")

# The AI must actually be able to drive this hull: an AIPilot serialized disabled never runs
# Update, so an AI Thresher would not steer (the shape Grizzly.prefab shipped in until Grizzly Time).
_thresher = read_or_empty(THRESHER_PREFAB)
_ai = re.search(r"  m_Enabled: (\d)\n  m_EditorHideFlags: 0\n  m_Script: \{fileID: 11500000, "
                r"guid: a58bf4fb65afa704194fe9e28e67d58d, type: 3\}", _thresher)
if not _ai or _ai.group(1) != "1":
    errors.append("Thresher.prefab's AIPilot is not enabled - an AI Thresher cannot race")
# The sling's ceiling (the number the whole cut rests on) must still be the ball's: maxBall in the
# config, scaled by gameCruise / sandboxCruise. ThresherTimeCourseTests reads it too; this names it
# at generation time.
_cfg = read_or_empty(THRESHER_CONFIG)
for key, want in (("maxBall", 2600.0), ("gameCruise", 70.0), ("sandboxCruise", 380.0)):
    m = re.search(rf"^    {key}: ([0-9.]+)$", _cfg, re.M)
    if not m or float(m.group(1)) != want:
        errors.append(f"ThresherConfig.asset dials.{key} is not {want} - the sling ceiling moved; "
                      f"re-cut ThresherTimeCourse (THRESHERTIME.md §3)")

# The toybox must offer the hull this mode is built for.
_roster = read_or_empty("Assets/_Scripts/Controller/Toys/ToyVesselRoster.cs")
if "VesselClassType.Thresher" not in _roster:
    errors.append("ToyVesselRoster.Default does not offer the Thresher")

minted = list(G_SCRIPT.values()) + [G_CARD, G_RULE, G_PREVIEW, G_SCENE, G_TOASTS,
                                    guid(f"folder/{MODE_DIR}"), guid(f"text/{DOC}")]
g.finish(errors, referenced=EXISTING, minted=minted)
