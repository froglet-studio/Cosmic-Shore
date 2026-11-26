#!/usr/bin/env python3
"""
Authors every serialized asset the Burrow Run game mode needs; the mode id is READ from GameModes.cs.

Built on arcade_mode_lib: deterministic guids (md5("CosmicShore/<stable name>")), the whole result
validated in memory before a byte is written, and --check diffs every file.

Run from the repo root:  python3 Tools/Build/author_burrowrun_assets.py [--check]

WHAT THIS MODE IS. Burrow Run is the JEWEL WASP's TIME race (Yash, 2026-11-22: the Meerkat keeps one Time game,
Warpline, and Burrow Run moves to a hull that had none): a closed loop of EIGHT switch rings cut through the
barren race cell, flown in TWO laps, and the first DOMAIN whose LEAD RUNNER threads the last gate of the
last lap wins (golf: finish time). It was the Meerkat's race (the Vessel Studio's Meerkat race) until that day.
See Assets/_Scripts/Controller/Arcade/BURROWRUN.md, which says what the re-seat left open (the cell has no
creature for the wasp to ride yet).

NOTHING HERE EDITS THE WASP (the LOCKED vessel rule, Shombith 2026-10-31).

THE DONOR IS WARPLINE (the Meerkat's time race). The clone swaps:

  1. the controller script (BurrowRunController), keeping Warpline's `laps` field (2).
  2. its scoring rule ASSET (another asset on the shared GateRaceScoringRuleSO script).
  3. maxPlausibleSpeed: Warpline's 1400 is kept (it must cover the wasp's sting flight, checked below).
  4. the scene's Netcode GlobalObjectIdHash values, minted fresh.
  5. the arcade card's Vessels list: the Jewel Wasp.

THE CIRCUIT GENERATOR IS SHARED, NOT CLONED: BurrowRunCourseSource hands SlingshotCourse.ForIntensity's
settings to HeadlongCircuit.Generate with target/laps rings - Slingshot's eight-ring circle, the studio's.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import wasp_games_common as wasp  # noqa: E402  (the wasp's identity; read-only)

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
    "BurrowRunController.Ruins": f"{MODE_DIR}/BurrowRunController.Ruins.cs",
}
RUINS_TESTS = "Assets/_Scripts/Tests/Editor/BurrowRunRuinsTests.cs"
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
    "Vessel_JewelWasp":         wasp.WASP_VESSEL,
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

# AI approach geometry: Warpline's (Slingshot's), the gate race family's untrained shape. The Meerkat's searched
# genes (42 / 702 / 74, run1 2026-11-17) were that hull's and left with it (MEERKAT_AI, the one-shot below); no
# search has flown a Jewel Wasp here yet.
AI_COMMIT_DISTANCE = 160
AI_APPROACH_LEAD = 180
AI_THROUGH_DISTANCE = 120
MEERKAT_AI = (42, 702, 74)

# Detection clamp: a frame's motion longer than this x dt x 2 + 5 is read as a respawn. Warpline's, kept: it
# must not be under the wasp's sting flight, or a sting through a ring's mouth would not count.
MAX_PLAUSIBLE_SPEED = 1400
STING_SPEED_FIELD = "stingSpeed"       # JewelWaspConfig: how fast a sting flies the hull to its host

# The comeback strength, a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`): at 0.3 a quarter-of-race
# deficit (4 gates) buys 1.2 element levels.
COMEBACK_RATE = 0.3

# The I4 garden ruins (BURROWRUN.md section 4a): laid from the environment prism, as Spearline's seals and ruins are.
SPAWNABLE_PRISM = lib.existing_guid("Assets/_Prefabs/Trails/SpawnablePrism.prefab")
RUIN_FIELD = f"  ruinPrefab: {{fileID: 4563009547826722997, guid: {SPAWNABLE_PRISM}, type: 3}}\n"

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
  Description: Jewel Wasps only, and time is everything. Thread the rings in order, eight a
    lap, two laps. The first domain whose lead runner clears the last ring wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background('BurrowRun')}, type: 3}}
  GolfScoring: 1
  SceneName: {SCENE_NAME}
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_JewelWasp']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 9
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
{RUIN_FIELD}  gateBloomSeconds: 0.9
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


# The scene's crystal manager, as the donor (Warpline) authors it and as this race needs it: FixedCount (0), zero crystals.
DONOR_CRYSTALS = "  crystalCountMode: 1\n  fixedCrystalCount: 1\n  extraCrystalsToSpawnBeyondPlayerCount: 1\n"
NO_CRYSTALS = "  crystalCountMode: 0\n  fixedCrystalCount: 0\n  extraCrystalsToSpawnBeyondPlayerCount: 1\n"


def crystal_errors(scene_text: str) -> "list[str]":
    """Why this scene's race could spawn a crystal, or []. An omni crystal hatches a creature under a Jewel Wasp, and an
    AI wasp leaves the race for any creature in the cell, so 'the barren cell has no creature' holds only while the
    scene's crystal manager spawns none: exactly one manager, in FixedCount mode, with a count of 0."""
    modes = re.findall(r"^  crystalCountMode: (\d+)$", scene_text, re.M)
    counts = re.findall(r"^  fixedCrystalCount: (\d+)$", scene_text, re.M)
    if len(modes) != 1 or len(counts) != 1:
        return [f"{SCENE_NAME}.unity: expected one crystal manager, found {len(modes)} crystalCountMode and "
                f"{len(counts)} fixedCrystalCount lines - re-read how this scene spawns crystals"]
    if modes[0] != "0" or counts[0] != "0":
        return [f"{SCENE_NAME}.unity: the crystal manager can spawn a crystal (crystalCountMode {modes[0]}, "
                f"fixedCrystalCount {counts[0]}; the race needs FixedCount with a count of 0). An omni crystal hatches a "
                f"predator under a Jewel Wasp and every AI wasp then rides creatures instead of racing "
                f"(BURROWRUN.md section 2)"]
    return []


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
    authored_blocks=("  spawnDistanceOutsideNucleus: 150\n", "  spawnFormation: 1\n"))
# One-shot (2026-11-22, the re-seat on the Jewel Wasp): the committed scene carried the Meerkat's searched approach
# genes. Stands down once the scene carries this generator's block; the check under it never stands down.
_MEERKAT_FIELDS = (NEW_FIELDS
                   .replace(f"aiCommitDistance: {AI_COMMIT_DISTANCE}\n", f"aiCommitDistance: {MEERKAT_AI[0]}\n")
                   .replace(f"aiApproachLead: {AI_APPROACH_LEAD}\n", f"aiApproachLead: {MEERKAT_AI[1]}\n")
                   .replace(f"aiThroughDistance: {AI_THROUGH_DISTANCE}\n", f"aiThroughDistance: {MEERKAT_AI[2]}\n"))
if NEW_FIELDS not in scene and scene.count(_MEERKAT_FIELDS) == 1:
    scene = scene.replace(_MEERKAT_FIELDS, NEW_FIELDS)
# One-shot (2026-11-24, the I4 ruins): the committed scene's controller had no ruin prefab. Stands down once it has.
_NO_RUIN_FIELDS = NEW_FIELDS.replace(RUIN_FIELD, "")
if NEW_FIELDS not in scene and scene.count(_NO_RUIN_FIELDS) == 1:
    scene = scene.replace(_NO_RUIN_FIELDS, NEW_FIELDS)
if NEW_FIELDS not in scene:
    scene_errors.append(f"{SCENE_NAME}.unity: authored block no longer in the committed scene:\n{NEW_FIELDS}")
# The race spawns NO crystal (2026-11-23). The donor's crystal manager spawns players + 1 omni crystals in the nucleus,
# and an omni crystal hatches a predator under a Jewel Wasp (JewelWaspHatchByCrystalEffectSO -> HatchPredator), so the
# cell stopped being barren 40 game seconds into every race and every AI wasp stung and rode creatures until the
# clock ran out: measured with the mode smoke test, 600 game seconds, no finisher, 16 to 49 creatures alive. A fixed
# count of 0 is Redline's setting. One-shot on the donor's block; the check in the validation section never stands down.
if NO_CRYSTALS not in scene and scene.count(DONOR_CRYSTALS) == 1:
    scene = scene.replace(DONOR_CRYSTALS, NO_CRYSTALS)
g.emit_scene(SCENE_NAME, G_SCENE, scene)

# ── 5. Preview (a card without one offers no Test Flight) ────────────────────
g.emit_asset(PREVIEW, G_PREVIEW, header(EXISTING["ModePreviewDefinitionSO"], "ModePreview_BurrowRun") + f"""  Mode: {MODE_ID}
  Notes: 'OPEN-ENDED, shell-only: Slingshot''s eight-ring circuit, laid as rings at match start, so a
    preview has no gates. It teaches the Barren cell and the Jewel Wasp''s free flight. One cell serves
    every intensity.'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['PreviewCell']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {wasp.WASP_ID}
  ObjectiveText: Thread the rings in order
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

# The AI must actually be able to drive this hull. The gate race's own steering flies it (AIPilot, through
# SetExternalTargetProvider). The wasp's own AI stings a creature whenever the cell has one and then flies the host
# at a rival, not at a ring (JewelWaspExecutor.StepAIFree / StepAIHost), so an AI wasp races only while the cell
# has no creature. The barren race cell has none, and that is checked here: seeding creatures for the wasp to ride
# (BURROWRUN.md "Open") fails by name until an AI wasp can ride for a ring.
_wasp = read_or_empty("Assets/_Prefabs/Spacevessels/JewelWasp.prefab")
_ai = re.search(r"  m_Enabled: (\d)\n  m_EditorHideFlags: 0\n  m_Script: \{fileID: 11500000, "
                r"guid: a58bf4fb65afa704194fe9e28e67d58d, type: 3\}", _wasp)
if not _ai or _ai.group(1) != "1":
    errors.append("JewelWasp.prefab's AIPilot is not enabled - an AI Jewel Wasp cannot race")
if f"JewelWasp = {wasp.WASP_ID}," not in read_or_empty(wasp.VESSEL_CLASS_CS):
    errors.append(f"VesselClassType.JewelWasp is no longer {wasp.WASP_ID}")
if lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_JewelWasp.asset") != wasp.WASP_VESSEL:
    errors.append("SO_Class_JewelWasp.asset's guid is not the one wasp_games_common names")
_executor = read_or_empty(wasp.WASP_EXECUTOR)
for _needle in ("if (IsAI) StepAIFree();", "if (f) Sting(f);",
                "var rival = NearestRival(host.PossessionNestPoint, _frame * Vector3.forward, 400f, 180f);"):
    if _needle not in _executor:
        errors.append(f"JewelWaspExecutor no longer has '{_needle}' - re-read how an AI wasp flies before trusting "
                      f"Burrow Run's 'no creature in the cell, so the AI races' rule")
errors += crystal_errors(scene)
if EXISTING["BarrenRaceCell"] not in NEW_FIELDS:
    errors.append("the race cell is no longer the barren one - an AI wasp stings any creature in the cell and "
                  "leaves the race (see the note above)")
_m = re.search(r"^  %s: ([\d.]+)$" % STING_SPEED_FIELD, read_or_empty(wasp.WASP_CONFIG), re.M)
if not _m:
    errors.append(f"JewelWaspConfig has no {STING_SPEED_FIELD} - the detection clamp cannot be checked against it")
elif float(_m.group(1)) > MAX_PLAUSIBLE_SPEED:
    errors.append(f"JewelWaspConfig.{STING_SPEED_FIELD} {_m.group(1)} is over maxPlausibleSpeed "
                  f"{MAX_PLAUSIBLE_SPEED}: a sting through a ring would read as a respawn")

# The mode carries nothing of the hull it left (the Meerkat, until 2026-11-22) and presses nothing on the wasp.
_controller = read_or_empty(SCRIPT_PATHS["BurrowRunController"])
for _word in ("Meerkat", "Omnicrystal", "PerformShipControllerActions", "InputStatus"):
    if _word in _controller:
        errors.append(f"BurrowRunController names '{_word}' - the mode seats the Jewel Wasp and presses nothing")
if lib.VESSELS["Meerkat"] in g.files.get(CARD, ""):
    errors.append("the card still seats the Meerkat - its only Time game is Warpline")

# The I4 ruins are Spearline's generator, CALLED from this mode's own hook: never a copy, and tested on this lap.
_ruins = read_or_empty(SCRIPT_PATHS["BurrowRunController.Ruins"])
if "SpearlineRuins.Lay(_course, SpearlineRuins.ForIntensity(Intensity)" not in _ruins:
    errors.append("BurrowRunController.Ruins no longer lays Spearline's ruins from this race's rings")
for _own in ("static void LayPergola", "static void LayWheel", "PerLapByRung"):
    if any(_own in read_or_empty(_p) for _p in SCRIPT_PATHS.values()):
        errors.append(f"Burrow Run carries '{_own}' - the ruin generator is Spearline's, called, not copied")
if "LayRuins();" not in _controller or "TickRuins();" not in _controller or _controller.count("ClearRuins();") < 2:
    errors.append("BurrowRunController no longer lays, turns and clears its ruins (OnCourseRaised, LateUpdate, OnNetworkDespawn)")
if RUIN_FIELD not in scene:
    errors.append(f"{SCENE_NAME}.unity: the controller has no ruin prefab - the I4 ruins would not be laid")
for _word in ("Vessel", "InputStatus", "ElementalFloat"):
    if _word in _ruins:
        errors.append(f"BurrowRunController.Ruins names '{_word}' - the ruins are scenery and read or write no vessel")
if "SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(intensity)" not in read_or_empty(RUINS_TESTS):
    errors.append(f"{RUINS_TESTS} is missing or no longer lays the ruins on this mode's lap")

minted = list(G_SCRIPT.values()) + [G_CARD, G_RULE, G_PREVIEW, G_SCENE,
                                    guid(f"folder/{MODE_DIR}"), guid(f"text/{DOC}")]
g.finish(errors, referenced=EXISTING, minted=minted)
