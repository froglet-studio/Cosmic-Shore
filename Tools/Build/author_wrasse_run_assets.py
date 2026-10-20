#!/usr/bin/env python3
"""
Authors every serialized asset the Wrasse Run game mode needs; the mode id is READ from GameModes.cs
rather than hardcoded (parallel branches have collided on "the next free id" six times; 67 is
SerpentCharge's on an in-flight branch, so Wrasse Run took 68).

Built on arcade_mode_lib: deterministic guids (md5("CosmicShore/<stable name>")), the whole result
validated in memory before a byte is written, and --check diffs every file.

Run from the repo root:  python3 Tools/Build/author_wrasse_run_assets.py [--check]

WHAT THIS MODE IS (Garrett, 2026-10-20: "make a wrasse and grouper racing game based around the wrasse
collecting rings and trying to favor time crystals (among others) to give the pilot the boost needed to
get to the next ring collector and align the wrasse to collect them"). Wrasse Run is the Wrasse-only
CIRCUIT race: a closed loop of switch rings cut through the cell, flown in LAPS, first DOMAIN whose LEAD
RUNNER threads the last gate of the last lap wins (golf: finish time). A ring counts only when the
WRASSE - the navigator's kite, the hull's IRingThreadProbe - threads it; the Grouper (the hull) carries
the kite's plane through the next ring, and the Wrasse's Time loops save the boosts that get it there.
See Assets/_Scripts/Controller/Arcade/WRASSERUN.md.

THE DONOR IS THRESHER TIME (a lapped gate race in the barren race cell with an EQUATORIAL spawn ring,
on the shared GateRaceController, RaceGateTurnMonitor, RaceGateObjectiveProvider and the generic
GateRaceScoringRuleSO), whose rings are already threaded by a second body. The clone swaps:

  1. the controller script (WrasseRunController), keeping the donor's `laps` field (now 2).
  2. its scoring rule ASSET (another asset on the same GateRaceScoringRuleSO script).
  3. the AI approach numbers, sized to the Grouper's 32 u cruise circle (an AI crew is TWO AIs: the
     hull's AIPilot flies the Grouper through the ring, a navigator AI slings the kite through its
     mouth), and the plausible-speed clamp, sized to the kite: the
     probe's world step is the hull's boosted 260 u/s plus the slung kite's 520 u/s plus the board's turn.
  4. the scene's Netcode GlobalObjectIdHash values, minted fresh.
  5. the arcade card's Vessels list: the Wrasse.

THE CIRCUIT GENERATOR IS SHARED, NOT CLONED. WrasseRunCourse supplies the Wrasse's settings to
HeadlongCircuit.Generate; WrasseRunCourseTests asserts them over a seed sweep.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

ROOT = lib.ROOT

# ── Mode id: READ, never hardcoded ───────────────────────────────────────────
with open(os.path.join(ROOT, "Assets/_Scripts/Data/Enums/GameModes.cs"), encoding="utf-8") as _fh:
    _m = re.search(r"^\s*WrasseRun\s*=\s*(\d+)\s*,", _fh.read(), re.M)
assert _m, "GameModes.WrasseRun not found - has the member been renamed?"
MODE_ID = int(_m.group(1))

g = lib.Generator(MODE_ID, "WrasseRun")
guid = lib.guid

# ── Paths ────────────────────────────────────────────────────────────────────
MODE_DIR = "Assets/_Scripts/Controller/Arcade/WrasseRun"
SCRIPT_PATHS = {
    "WrasseRunController":   f"{MODE_DIR}/WrasseRunController.cs",
    "WrasseRunCourse":       f"{MODE_DIR}/WrasseRunCourse.cs",
    "WrasseRunCourseSource": f"{MODE_DIR}/WrasseRunCourseSource.cs",
    "WrasseRunCourseTests":  "Assets/_Scripts/Tests/Editor/WrasseRunCourseTests.cs",
    "KiteNavigatorSlingTests": "Assets/_Scripts/Tests/Editor/KiteNavigatorSlingTests.cs",
}
DOC = "Assets/_Scripts/Controller/Arcade/WRASSERUN.md"
CARD = "Assets/_SO_Assets/Games/ArcadeGameWrasseRun.asset"
RULE = "Assets/_SO_Assets/Scoring Rules/WrasseRunScoringRule.asset"
PREVIEW = "Assets/_SO_Assets/Mode Previews/ModePreview_WrasseRun.asset"
TOASTS = "Assets/_SO_Assets/Game Toasts/GameToastConfig_WrasseRun.asset"
SCENE_NAME = "MinigameWrasseRun"
SCENE = f"{lib.SCENES_DIR}/{SCENE_NAME}.unity"
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameThresherTime.unity"
WRASSE_PREFAB = "Assets/_Prefabs/Spacevessels/Wrasse.prefab"
KITE_CONFIG = "Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset"

# ── Minted GUIDs ─────────────────────────────────────────────────────────────
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_CARD = guid("asset/ArcadeGameWrasseRun")
G_RULE = guid("asset/WrasseRunScoringRule")
G_PREVIEW = guid("asset/ModePreview_WrasseRun")
G_TOASTS = guid("asset/GameToastConfig_WrasseRun")
G_SCENE = guid("asset/MinigameWrasseRun.unity")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":            lib.SCRIPT_GUIDS["SO_ArcadeGame"],
    "ModePreviewDefinitionSO":  lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"],
    "GameToastConfigSO":        lib.SCRIPT_GUIDS["GameToastConfigSO"],
    "GateRaceScoringRuleSO":    "349cc0c9402590262de23356775d43cc",
    # donor scene wiring to swap out - Thresher Time's controller and rule, minted by ITS generator
    "ThresherTimeController":   guid("script/ThresherTimeController"),
    "ThresherTimeScoringRule":  guid("asset/ThresherTimeScoringRule"),
    "Vessel_Wrasse":            lib.VESSELS["Wrasse"],
    "BarrenRaceCell":           "8d4e8398eedc76c4dadb8604f89b9e1b",
    "PreviewCell":              "52de45c2fab54ea0a35bc37a311d80e6",
    "IconActive":               lib.CARD_ART["IconActive"],
    "IconInactive":             lib.CARD_ART["IconInactive"],
}
assert lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_Wrasse.asset") == EXISTING["Vessel_Wrasse"], \
    "arcade_mode_lib.VESSELS['Wrasse'] is not SO_Class_Wrasse.asset's guid"

# ── Tuning ───────────────────────────────────────────────────────────────────

# The RACE LENGTH in gate threadings - LAPS x RINGS. Kept in sync with
# EndConditionOverridesSO.DefaultWrasseRunGateTarget and WrasseRunCourseSource.DefaultLaps.
LAPS = 2
RINGS_PER_LAP = 8                 # WrasseRunCourse.GatesPerLap
GATE_TARGET = LAPS * RINGS_PER_LAP
BASE_RADIUS = 640                 # WrasseRunCourse.BaseRadius

# The Grouper: Wrasse.prefab MinimumSpeed 10 + ThrottleScaler 50 = 60 u/s on a 100 deg/s turn that grows
# 0.1 deg/s per u/s (RotationThrottleScaler) - a ~32 u circle. A saved boost is x5 throttle: 260 u/s.
CRUISE_SPEED = 60.0
BOOST_SPEED = 260.0
CRUISE_RADIUS = CRUISE_SPEED / ((CRUISE_SPEED * 0.1 + 100.0) * 3.141592653589793 / 180.0)

# AI approach geometry, Slingshot's ratios (commit 2.2x, lead 2.5x, through 1.7x) on the 32 u cruise
# circle: the AI Grouper flies its hull through each ring - which puts the ring in the kite's plane, on
# its board - and the AI navigator slings the kite through the mouth (KiteNavigatorConfig
# aiCrewNavigates). The Grouper spends the boosts the Wrasse saves while the next ring is far ahead.
AI_COMMIT_DISTANCE = 72
AI_APPROACH_LEAD = 80
AI_THROUGH_DISTANCE = 55

# Detection clamp: a frame's motion longer than this x dt x 2 + 5 is read as a respawn - for the hull AND
# for the probe (the kite). The kite's world step is the hull's boosted 260 u/s + the slung kite's
# 520 u/s (KiteNavigatorConfig slingMaxSpeed) + the board turning under it (the hull's ~126 deg/s at boost
# over the 160 u board, ~350 u/s at the rim): ~1130 u/s. 2400 covers twice that - a ~85 u step at 60 fps,
# still under a respawn's jump onto a 640 u circuit.
MAX_PLAUSIBLE_SPEED = 2400
KITE_WORST_SPEED = BOOST_SPEED + 520 + 126 * 3.141592653589793 / 180.0 * 160

# The comeback strength, a FUNCTION OF THE TARGET: at 0.5 a quarter-of-race deficit (4 gates) buys 2
# element levels. The buff lands on the mode's axis: a trailing hull's Time petals make Time motes come
# more often (KiteNavigator.ElementWeight), so the trailing kite loops more boosts.
COMEBACK_RATE = 0.5

HASH_SALT = "MinigameWrasseRun"


def header(script_guid: str, name: str) -> str:
    return lib.header_for(script_guid, name)


# ── 1. Metas for the scripts, the mode folder and the doc ────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(MODE_DIR)
g.text_meta(DOC)

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.SwitchesThreaded, REUSED: the BestByDomain fold, the goal-stack row and the
# objective icon come free. Golf: the winning domain's pilots carry a finish time.
g.emit_asset(RULE, G_RULE, header(EXISTING["GateRaceScoringRuleSO"], "WrasseRunScoringRule") +
             "  metric: 9\n  golfRules: 1\n")

# ── 3. Arcade card ───────────────────────────────────────────────────────────
# WRASSE ONLY: one entry in Vessels drives all three enforcement layers. 2 / 2 minimum: a race needs a
# rival. The card reads "Wrasse and Grouper" wherever the hull is named (SO_Vessel.CrewNames).
g.emit_asset(CARD, G_CARD, header(EXISTING["SO_ArcadeGame"], "ArcadeGameWrasseRun") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Wrasse Run
  Description: The Wrasse collects the rings. Grouper, fly your plane through the lit
    ring so the Wrasse can swing through it - the hull alone does not count. Wrasse,
    loop Time motes to bank boosts the Grouper spends to reach the next ring. Two laps.
    Fastest crew home wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background('WrasseRun')}, type: 3}}
  GolfScoring: 1
  SceneName: {SCENE_NAME}
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Wrasse']}, type: 2}}
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

# ── 4. Scene: clone MinigameThresherTime ─────────────────────────────────────
OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['ThresherTimeScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: {EXISTING['BarrenRaceCell']}, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: 3
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 135
  aiApproachLead: 150
  aiThroughDistance: 100
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: 4600
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
    scene = lib.swap_guid(scene, EXISTING["ThresherTimeController"], G_SCRIPT["WrasseRunController"],
                          "controller script")
    scene = lib.replace_block(scene, OLD_FIELDS, NEW_FIELDS, "controller field block")
    for probe, why in ((r"^  spawnFormation: 1$", "equatorial spawn ring"),
                       (r"^  cellTypeChoiceOptions: 0$", "single race cell")):
        assert re.search(probe, scene, re.M), f"donor no longer provides: {why}"

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
g.emit_asset(PREVIEW, G_PREVIEW, header(EXISTING["ModePreviewDefinitionSO"], "ModePreview_WrasseRun") + f"""  Mode: {MODE_ID}
  Notes: 'OPEN-ENDED, shell-only: Headlong''s solver cut to the Wrasse''s boosted circle, laid as
    rings at match start, so a preview has no gates. It teaches the Barren cell, the sling and
    the Time loop. One cell serves every intensity.'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['PreviewCell']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {lib.VESSEL_CLASS_ID['Wrasse']}
  ObjectiveText: Loop Time motes to bank boosts, then swing the Wrasse through the rings
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

# ── 5b. Toasts: the shared race beats GateRaceController already ticks, and the comeback notice ──
g.emit_asset(TOASTS, G_TOASTS,
             header(EXISTING["GameToastConfigSO"], "GameToastConfig_WrasseRun") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(129, "{0} is halfway home - ring {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - ring {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(131, "{0} is on the home stretch - ring {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(132, "{0} is on the final lap", tint_domain=1, domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_TOASTS)

# ── 6. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_CARD)
g.register_always_unlocked()
g.register_build_scene("MinigameThresherTime", SCENE_NAME, G_SCENE)
g.set_end_condition("wrasseRunGateTarget", after="thresherTimeGateTarget", value=GATE_TARGET)


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


_course = read_or_empty(SCRIPT_PATHS["WrasseRunCourse"])
for const, want in (("GatesPerLap", RINGS_PER_LAP), ("BaseRadius", BASE_RADIUS)):
    m = re.search(rf"public const (?:int|float) {const} = (\d+)f?;", _course)
    if not m or int(m.group(1)) != want:
        errors.append(f"WrasseRunCourse.{const} is not {want}")

_end = read_or_empty("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
for pat, what in ((r"DefaultWrasseRunGateTarget\s*=\s*(\d+);", "DefaultWrasseRunGateTarget"),
                  (r"public int wrasseRunGateTarget\s*=\s*(\d+);", "wrasseRunGateTarget initializer"),
                  (r"public int wrasseRunGateTargetBuild\s*=\s*(\d+);", "wrasseRunGateTargetBuild initializer")):
    m = re.search(pat, _end)
    if not m or int(m.group(1)) != GATE_TARGET:
        errors.append(f"EndConditionOverridesSO.{what} is not {GATE_TARGET}")
if "GameModes.WrasseRun" not in _end:
    errors.append("EndConditionOverridesSO.TryGetAuthoredTurnTarget has no WrasseRun row")

_src = read_or_empty(SCRIPT_PATHS["WrasseRunCourseSource"])
m = re.search(r"public const int DefaultLaps = (\d+);", _src)
if not m or int(m.group(1)) != LAPS:
    errors.append(f"WrasseRunCourseSource.DefaultLaps is not {LAPS}")
_ctrl = read_or_empty(SCRIPT_PATHS["WrasseRunController"])
if "protected override bool HullThreadsWhileProbeLive => false;" not in _ctrl:
    errors.append("WrasseRunController does not make the kite the ring collector (HullThreadsWhileProbeLive)")

if "case GameModes.WrasseRun:" not in read_or_empty("Assets/_Scripts/UI/MiniGameHUD.cs"):
    errors.append("MiniGameHUD.CreateObjectiveProviderForGameMode has no WrasseRun case")

if G_SCRIPT["WrasseRunController"] not in scene or EXISTING["ThresherTimeController"] in scene:
    errors.append("scene does not carry exactly the WrasseRunController script")
_taken = project_network_hashes()
for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene):
    if int(h) != 0 and int(h) in _taken:
        errors.append(f"scene GlobalObjectIdHash {h} is also carried by another scene or prefab")

# The detection clamp must cover the kite (the probe), or a slung kite's step reads as a respawn and the
# ring it threads is never credited.
if MAX_PLAUSIBLE_SPEED < 2 * KITE_WORST_SPEED:
    errors.append(f"maxPlausibleSpeed {MAX_PLAUSIBLE_SPEED} is under twice the kite's worst world speed "
                  f"({KITE_WORST_SPEED:.0f} u/s)")
if not (AI_THROUGH_DISTANCE > CRUISE_RADIUS and AI_COMMIT_DISTANCE > 2 * CRUISE_RADIUS
        and AI_APPROACH_LEAD > AI_COMMIT_DISTANCE):
    errors.append("AI approach geometry is not sized outside the Grouper's cruise circle "
                  f"(commit {AI_COMMIT_DISTANCE} / lead {AI_APPROACH_LEAD} / through "
                  f"{AI_THROUGH_DISTANCE} against {CRUISE_RADIUS:.0f} u)")

# The hull this mode is built for: its AI must drive it (an AI crew threads with the hull), it must
# carry the kite on its root, and its numbers must be the ones the course is cut against.
_wrasse = read_or_empty(WRASSE_PREFAB)
_ai = re.search(r"  m_Enabled: (\d)\n  m_EditorHideFlags: 0\n  m_Script: \{fileID: 11500000, "
                r"guid: a58bf4fb65afa704194fe9e28e67d58d, type: 3\}", _wrasse)
if not _ai or _ai.group(1) != "1":
    errors.append("Wrasse.prefab's AIPilot is not enabled - an AI crew cannot race")
for key, want in (("MinimumSpeed", 10.0), ("ThrottleScaler", 50.0), ("boostMultiplier", 5.0)):
    m = re.search(rf"^  {key}: ([0-9.]+)$", _wrasse, re.M)
    if not m or float(m.group(1)) != want:
        errors.append(f"Wrasse.prefab {key} is not {want} - re-cut WrasseRunCourse (WRASSERUN.md)")
_kite = read_or_empty(KITE_CONFIG)
for key, want in (("control", 1.0), ("slingMaxSpeed", 520.0), ("boardRadius", 160.0), ("aiCrewNavigates", 1.0)):
    m = re.search(rf"^  {key}: ([0-9.]+)$", _kite, re.M)
    if not m or float(m.group(1)) != want:
        errors.append(f"KiteNavigatorConfig.asset {key} is not {want} - re-check the clamp and the course")

_roster = read_or_empty("Assets/_Scripts/Controller/Toys/ToyVesselRoster.cs")
if "VesselClassType.Wrasse" not in _roster:
    errors.append("ToyVesselRoster.Default does not offer the Wrasse")

minted = list(G_SCRIPT.values()) + [G_CARD, G_RULE, G_PREVIEW, G_SCENE, G_TOASTS,
                                    guid(f"folder/{MODE_DIR}"), guid(f"text/{DOC}")]
g.finish(errors, referenced=EXISTING, minted=minted)
