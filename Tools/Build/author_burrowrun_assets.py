#!/usr/bin/env python3
"""
Authors every serialized asset the Burrow Run game mode needs; the mode id is READ from GameModes.cs.

Built on arcade_mode_lib: deterministic guids (md5("CosmicShore/<stable name>")), the whole result
validated in memory before a byte is written, and --check diffs every file.

Run from the repo root:  python3 Tools/Build/author_warpline_assets.py [--check]

WHAT THIS MODE IS. Burrow Run is the Meerkat's TIME race, the Vessel Studio's Meerkat race
(Docs/Studios/VesselStudio/meerkat.html) in the game: a closed loop of EIGHT switch rings cut through the
barren race cell, flown in TWO laps, and the first DOMAIN whose LEAD RUNNER threads the last gate of the
last lap wins (golf: finish time). The Meerkat flies one-thumbed at cruise and its only speed past that is
its WORMHOLE (R_VesselActions/MEERKAT.md): the right stick bends the reticle toward the next ring, RT forms
the tunnel and dives through it at up to 2.5x the entry speed. See Assets/_Scripts/Controller/Arcade/BURROWRUN.md.

THE DONOR IS WARPLINE (the Stoat's time race - the Meerkat was the Stoat's Gate mode). The clone swaps:

  1. the controller script (BurrowRunController), keeping Warpline's `laps` field (2).
  2. its scoring rule ASSET (another asset on the shared GateRaceScoringRuleSO script).
  3. maxPlausibleSpeed for the ride (2.5 x a 60 u/s cruise = 150 u/s at the throat; Warpline's 1400 is kept, the
     clamp only reads a respawn).
  4. the scene's Netcode GlobalObjectIdHash values, minted fresh.
  5. the arcade card's Vessels list: the Meerkat.

THE CIRCUIT GENERATOR IS SHARED, NOT CLONED: BurrowRunCourseSource hands SlingshotCourse.ForIntensity's
settings to HeadlongCircuit.Generate with target/laps rings - Slingshot's eight-ring circle, the studio's.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

ROOT = lib.ROOT

# ── Mode id: READ, never hardcoded ───────────────────────────────────────────
with open(os.path.join(ROOT, "Assets/_Scripts/Data/Enums/GameModes.cs"), encoding="utf-8") as _fh:
    _m = re.search(r"^\s*BurrowRun\s*=\s*(\d+)\s*,", _fh.read(), re.M)
assert _m, "GameModes.BurrowRun not found - has the member been renamed?"
MODE_ID = int(_m.group(1))

g = lib.Generator(MODE_ID, "BurrowRun")
guid = lib.guid

# ── Paths ────────────────────────────────────────────────────────────────────
MODE_DIR = "Assets/_Scripts/Controller/Arcade/BurrowRun"
SCRIPT_PATHS = {
    "BurrowRunController": f"{MODE_DIR}/BurrowRunController.cs",
    "BurrowRunCourseSource": f"{MODE_DIR}/BurrowRunCourseSource.cs",
}
DOC = "Assets/_Scripts/Controller/Arcade/BURROWRUN.md"
CARD = "Assets/_SO_Assets/Games/ArcadeGameBurrowRun.asset"
RULE = "Assets/_SO_Assets/Scoring Rules/BurrowRunScoringRule.asset"
PREVIEW = "Assets/_SO_Assets/Mode Previews/ModePreview_BurrowRun.asset"
SCENE_NAME = "MinigameBurrowRun"
SCENE = f"{lib.SCENES_DIR}/{SCENE_NAME}.unity"
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameWarpline.unity"

# ── Minted GUIDs ─────────────────────────────────────────────────────────────
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_CARD = guid("asset/ArcadeGameBurrowRun")
G_RULE = guid("asset/BurrowRunScoringRule")
G_PREVIEW = guid("asset/ModePreview_BurrowRun")
G_SCENE = guid("asset/MinigameBurrowRun.unity")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":            lib.SCRIPT_GUIDS["SO_ArcadeGame"],
    "ModePreviewDefinitionSO":  lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"],
    "GateRaceScoringRuleSO":    "349cc0c9402590262de23356775d43cc",
    # donor scene wiring to swap out - Warpline's controller and rule, minted by ITS generator
    "WarplineController":       guid("script/WarplineController"),
    "WarplineScoringRule":      guid("asset/WarplineScoringRule"),
    "Vessel_Meerkat":           lib.VESSELS["Meerkat"],
    "BarrenRaceCell":           "8d4e8398eedc76c4dadb8604f89b9e1b",
    "PreviewCell":              "52de45c2fab54ea0a35bc37a311d80e6",
    "IconActive":               lib.CARD_ART["IconActive"],
    "IconInactive":             lib.CARD_ART["IconInactive"],
}

# ── Tuning ───────────────────────────────────────────────────────────────────

# The RACE LENGTH in gate threadings - LAPS x RINGS. Kept in sync with
# EndConditionOverridesSO.DefaultBurrowRunGateTarget. The studio's race: eight rings, two laps.
LAPS = 2
RINGS_PER_LAP = 8                 # Slingshot's 600 u circle, the studio's eight rings
GATE_TARGET = LAPS * RINGS_PER_LAP

# AI approach geometry: Warpline's (the Meerkat turns at 120 deg/s on one thumb - tighter than the Stoat).
AI_COMMIT_DISTANCE = 160
AI_APPROACH_LEAD = 180
AI_THROUGH_DISTANCE = 120

# Detection clamp: a frame's motion longer than this x dt x 2 + 5 is read as a respawn. Warpline's, kept: the
# Meerkat's ride tops out at 2.5 x its entry speed, a few hundred u/s.
MAX_PLAUSIBLE_SPEED = 1400

# The comeback strength, a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`): at 0.3 a quarter-of-race
# deficit (4 gates) buys 1.2 element levels.
COMEBACK_RATE = 0.3

HASH_SALT = "MinigameBurrowRun"


def header(script_guid: str, name: str) -> str:
    return lib.header_for(script_guid, name)


# ── 1. Metas for the scripts, the mode folder and the doc ────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(MODE_DIR)
g.text_meta(DOC)

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.SwitchesThreaded, REUSED as every gate circuit reuses it. Golf.
g.emit_asset(RULE, G_RULE, header(EXISTING["GateRaceScoringRuleSO"], "BurrowRunScoringRule") +
             "  metric: 9\n  golfRules: 1\n")

# ── 3. Arcade card ───────────────────────────────────────────────────────────
g.emit_asset(CARD, G_CARD, header(EXISTING["SO_ArcadeGame"], "ArcadeGameBurrowRun") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Burrow Run
  Description: Meerkats only, and time is everything. The reticle on your nose is the tunnel
    you would dig - bend it toward the next ring with the right stick, shorten it with LT,
    and pull RT to ripple it open and dive through, out the far end with the speed you went
    in with. Eight rings a lap, two laps.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background('BurrowRun')}, type: 3}}
  GolfScoring: 1
  SceneName: {SCENE_NAME}
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Meerkat']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 1
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")

# ── 4. Scene: clone MinigameWarpline ─────────────────────────────────────────
OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['WarplineScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: {EXISTING['BarrenRaceCell']}, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: 2
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 160
  aiApproachLead: 180
  aiThroughDistance: 120
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
    """Every GlobalObjectIdHash any scene or prefab already carries (this mode's own scene aside)."""
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
    scene = lib.swap_guid(scene, EXISTING["WarplineController"], G_SCRIPT["BurrowRunController"],
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
g.emit_asset(PREVIEW, G_PREVIEW, header(EXISTING["ModePreviewDefinitionSO"], "ModePreview_BurrowRun") + f"""  Mode: {MODE_ID}
  Notes: 'OPEN-ENDED, shell-only: Slingshot''s eight-ring circuit, laid as rings at match start, so a
    preview has no gates. It teaches the Barren cell, the Meerkat''s reticle (the right stick bends it,
    LT shortens it) and RT''s tunnel ride. One cell serves every intensity.'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['PreviewCell']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {lib.VESSEL_CLASS_ID['Meerkat']}
  ObjectiveText: Bend the reticle, pull RT and ride the tunnel
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
g.register_build_scene("MinigameWarpline", SCENE_NAME, G_SCENE)
g.set_end_condition("burrowRunGateTarget", after="wrasseRunGateTarget", value=GATE_TARGET)


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


_end = read_or_empty("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
for pat, what in ((r"DefaultBurrowRunGateTarget\s*=\s*(\d+);", "DefaultBurrowRunGateTarget"),
                  (r"public int burrowRunGateTarget\s*=\s*(\d+);", "burrowRunGateTarget initializer"),
                  (r"public int burrowRunGateTargetBuild\s*=\s*(\d+);", "burrowRunGateTargetBuild initializer")):
    m = re.search(pat, _end)
    if not m or int(m.group(1)) != GATE_TARGET:
        errors.append(f"EndConditionOverridesSO.{what} is not {GATE_TARGET}")

_course = read_or_empty(SCRIPT_PATHS["BurrowRunCourseSource"])
_m = re.search(r"DefaultLaps\s*=\s*(\d+);", _course)
if not _m or int(_m.group(1)) != LAPS:
    errors.append(f"BurrowRunCourseSource.DefaultLaps is not {LAPS}")
_cut = read_or_empty("Assets/_Scripts/Controller/Arcade/Racing/TimeRaceCourseConfigSO.cs")
if "TimeRaceCourseConfigSO.Circuit(Mode, request.Intensity)" not in _course or \
        "var settings = SlingshotCourse.ForIntensity(intensity);" not in _cut:
    errors.append("BurrowRunCourseSource no longer cuts Slingshot's circuit (TimeRaceCourseConfigSO.Circuit over SlingshotCourse.ForIntensity)")

if G_SCRIPT["BurrowRunController"] not in scene or EXISTING["WarplineController"] in scene:
    errors.append("scene does not carry exactly the BurrowRunController script")
_taken = project_network_hashes()
for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene):
    if int(h) != 0 and int(h) in _taken:
        errors.append(f"scene GlobalObjectIdHash {h} is also carried by another scene or prefab")

# The AI must actually be able to drive this hull AND use its verb (the arcade rule: never assume an AI can use a
# human's input): the wormhole's autopilot, through the replicated press path, RT bound on the prefab.
_meerkat = read_or_empty("Assets/_Prefabs/Spacevessels/Meerkat.prefab")
_ai = re.search(r"  m_Enabled: (\d)\n  m_EditorHideFlags: 0\n  m_Script: \{fileID: 11500000, "
                r"guid: a58bf4fb65afa704194fe9e28e67d58d, type: 3\}", _meerkat)
if not _ai or _ai.group(1) != "1":
    errors.append("Meerkat.prefab's AIPilot is not enabled - an AI Meerkat cannot race")
_wh = read_or_empty("Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/MeerkatSolitonExecutor.cs")
if "PerformShipControllerActionsReplicated" not in _wh:
    errors.append("MeerkatSolitonExecutor's autopilot does not go through the replicated press path")
_cfg = read_or_empty("Assets/_SO_Assets/VesselActions/Meerkat/MeerkatSolitonConfig.asset")
if not re.search(r"^  aiWormhole: 1$", _cfg, re.M):
    errors.append("MeerkatSolitonConfig.aiWormhole is off or missing - an AI Meerkat forms no tunnel")
_meta = read_or_empty("Assets/_SO_Assets/VesselActions/Meerkat/MeerkatSolitonFormAction.asset.meta")
_mg = re.search(r"^guid: ([0-9a-f]{32})", _meta, re.M)
if not _mg or _mg.group(1) not in _meerkat:
    errors.append("Meerkat.prefab does not bind MeerkatSolitonFormAction - run author_meerkat_assets.py")

if not os.path.exists(os.path.join(ROOT, "Assets/_SO_Assets/Classes/SO_Class_Meerkat.asset")):
    errors.append("SO_Class_Meerkat.asset is missing - run author_meerkat_assets.py")

minted = list(G_SCRIPT.values()) + [G_CARD, G_RULE, G_PREVIEW, G_SCENE,
                                    guid(f"folder/{MODE_DIR}"), guid(f"text/{DOC}")]
g.finish(errors, referenced=EXISTING, minted=minted)
