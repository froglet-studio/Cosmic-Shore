#!/usr/bin/env python3
"""Author every asset Waystation adds - the Butterfly-only migration race.

Waystation is a GATE RACE (GateRaceController, shared with Switchback, Headlong, Breakwater,
Skein and Redline), so it adds no metric, no turn monitor and no scoring class: the rule is a
SECOND ASSET on GateRaceScoringRuleSO and the monitor is the platform's, which asks the
controller. What is genuinely its own is the COURSE - clusters of rings you weave, laid a FOLD
apart - and that is proved offline by Tools/Build/waystation_course.py, which this script IMPORTS
rather than retyping, so the numbers the model verified are the numbers the game ships.

THE DONOR IS SWITCHBACK, because it is the platform's other OPEN chain (Headlong and Redline lap a
closed circuit and Breakwater carries a lead-in gate). The clone swaps four things and nothing
else: the controller script, the scoring rule, the four AI hulls (Dolphin -> Butterfly) and the
removal of Switchback's own `firstGateDistance`, which Waystation deliberately does not have.

Run with --check to validate and diff without writing.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib          # noqa: E402
import waystation_course as course     # noqa: E402

MODE_ID = 58
RING_TARGET = 24
g = lib.Generator(mode_id=MODE_ID, mode_name="Waystation")
errors = []

# ── guids read from the repo, never invented ────────────────────────────────
EXISTING = {
    "SO_ArcadeGame":          lib.SCRIPT_GUIDS["SO_ArcadeGame"],
    "GameToastConfigSO":      lib.SCRIPT_GUIDS["GameToastConfigSO"],
    "ModePreviewDefinitionSO": lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"],
    "GateRaceScoringRuleSO":  "349cc0c9402590262de23356775d43cc",
    "SwitchbackController":   "232714002ed12bc27251e8ac09358ff0",
    "SwitchbackScoringRule":  "b67719b27d5ca31d65b90aec65674945",
    "BarrenCellConfig":       "52de45c2fab54ea0a35bc37a311d80e6",
    "Butterfly":              lib.VESSELS["Butterfly"],
}

SCRIPTS = {
    "WaystationController": "Assets/_Scripts/Controller/Arcade/Waystation/WaystationController.cs",
    "WaystationCourse":     "Assets/_Scripts/Controller/Arcade/Waystation/WaystationCourse.cs",
}
G_SCRIPT = {name: lib.guid(f"script/{name}") for name in SCRIPTS}
G_ASSET = {
    "WaystationScoringRule":       lib.guid("asset/WaystationScoringRule"),
    "ArcadeGameWaystation":        lib.guid("asset/ArcadeGameWaystation"),
    "GameToastConfig_Waystation":  lib.guid("asset/GameToastConfig_Waystation"),
    "ModePreview_Waystation":      lib.guid("asset/ModePreview_Waystation"),
    "MinigameWaystation.unity":    lib.guid("scene/MinigameWaystation"),
}

# ── the course model is the authority on the mode's own numbers ─────────────
# Imported, never retyped: the tables live in WaystationCourse.cs and the model READS them, so a
# retune moves the C#, the model and this script's assertions together or fails all three.
T = course.read_tables()
HULL_TOP, HULL_OMEGA, HULL_RADIUS = course.read_vessel()
FOLD_REACH = course.read_fold_reach()

if not course.run(seeds=200, target=RING_TARGET, quiet=True):
    errors.append("waystation_course.py does not pass its own checks - the course is not shippable")

# The ring target must be a whole number of clusters at EVERY intensity, or the turn monitor
# publishes a finish line naming a ring the course never laid.
for i in (1, 2, 3, 4):
    per = int(T["RingsPerCluster"][i - 1])
    laid = course.math.ceil(RING_TARGET / per) * per
    if laid < RING_TARGET:
        errors.append(f"intensity {i} lays {laid} rings for a target of {RING_TARGET}")

# ── the comeback rate is a FUNCTION OF THE TARGET ───────────────────────────
# bonusLevels = deficit x rate, so a rate that survives a re-target silently stops meaning
# anything. Sized against Switchback's curve (target 20 at 0.5 buys 2.5 levels a quarter down).
COMEBACK_RATE = 0.4
quarter = RING_TARGET * 0.25
if quarter * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys only "
                  f"{quarter * COMEBACK_RATE:.2f} element levels a quarter of the way down")

# ── .cs.meta for the two new scripts, plus the folder ───────────────────────
g.folder_meta("Assets/_Scripts/Controller/Arcade/Waystation")
for name, rel in SCRIPTS.items():
    g.script_meta(rel, G_SCRIPT[name])

# The mode's doc lives inside Assets/, so Unity imports it and wants a meta. 24 of the 25 docs
# beside it have one; a doc without it gets a fresh random guid on every machine that opens the
# project, which is a spurious diff in everybody's working tree forever.
g.text_meta("Assets/_Scripts/Controller/Arcade/WAYSTATION.md")

# ── the scoring rule: a SECOND ASSET on GateRaceScoringRuleSO ───────────────
g.emit_asset(
    "Assets/_SO_Assets/Scoring Rules/WaystationScoringRule.asset",
    G_ASSET["WaystationScoringRule"],
    lib.header_for(EXISTING["GateRaceScoringRuleSO"], "WaystationScoringRule")
    + "  metric: 9\n  golfRules: 1\n")

# ── the arcade card ─────────────────────────────────────────────────────────
DESCRIPTION = (
    "Butterflies only. The rings come in clusters - weave the coil, line up on the gate at the "
    "far end, then FOLD across the gap to the next one. The fold has one degree of freedom, the "
    "heading you leave on, so the exit gate is the aiming device: thread it on the right line "
    "and the jump is free. First team to put a pilot through the last ring takes it.")
g.emit_asset(
    "Assets/_SO_Assets/Games/ArcadeGameWaystation.asset",
    G_ASSET["ArcadeGameWaystation"],
    lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameWaystation")
    + f"  Mode: {MODE_ID}\n"
      "  IsMultiplayer: 1\n"
      "  DisplayName: Waystation\n"
      f"  Description: {DESCRIPTION}\n"
      f"  IconActive: {{fileID: 21300000, guid: {lib.CARD_ART['IconActive']}, type: 3}}\n"
      f"  IconInactive: {{fileID: 21300000, guid: {lib.CARD_ART['IconInactive']}, type: 3}}\n"
      f"  CardBackground: {{fileID: 21300000, guid: {lib.CARD_ART['CardBackground']}, type: 3}}\n"
      "  GolfScoring: 1\n"
      "  SceneName: MinigameWaystation\n"
      "  Vessels:\n"
      f"  - {{fileID: 11400000, guid: {EXISTING['Butterfly']}, type: 2}}\n"
      "  MinPlayersAllowed: 2\n"
      "  MaxPlayersAllowed: 4\n"
      "  MinDomainsAllowed: 2\n"
      "  MaxDomainsAllowed: 3\n"
      "  MinIntensity: 1\n"
      "  MaxIntensity: 4\n"
      "  ViewUserAction: 0\n"
      "  PlayUserAction: 0\n"
      f"  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}\n")

# ── toasts: TWO IDLE HINTS AND NOTHING ELSE ─────────────────────────────────
# The gate-race platform has no gate-threaded hook, so a milestone situation here would have no
# poster. An idle hint needs none - the toast system fires it off idleSeconds.
g.emit_asset(
    "Assets/_SO_Assets/Game Toasts/GameToastConfig_Waystation.asset",
    G_ASSET["GameToastConfig_Waystation"],
    lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Waystation")
    + f"  gameMode: {MODE_ID}\n  toasts:\n"
    + lib.toast(117, "Thread every ring around you - they only count in order",
                tint_domain=0, domain_names=0, every_n=1, idle=1, idle_seconds=25)
    + lib.toast(118, "Out of rings? HOLD the fold and aim where you want to land",
                tint_domain=0, domain_names=0, every_n=1, idle=1, idle_seconds=40))

# ── the preview: shell-only, like every gate race ──────────────────────────
NOTES = (
    "OPEN-ENDED and shell-only, like Switchback and Regatta: the course is generated by the "
    "controller at match start and broadcast as geometry, so a preview arena has no rings to "
    "thread. What it honestly teaches is the Barren cell it runs in and the Butterfly''s two-thumb "
    "flight. Intensity is the COURSE, not the arena, so one cell serves every rung. A "
    "StructurePrefab of one standing cluster is the recorded gap.")
g.emit_asset(
    "Assets/_SO_Assets/Mode Previews/ModePreview_Waystation.asset",
    G_ASSET["ModePreview_Waystation"],
    lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Waystation")
    + f"  Mode: {MODE_ID}\n"
      f"  Notes: '{NOTES}'\n"
      f"  PreviewCell: {{fileID: 11400000, guid: {EXISTING['BarrenCellConfig']}, type: 2}}\n"
      "  PreviewCellsByIntensity: []\n"
      "  StructurePrefab: {fileID: 0}\n"
      "  TrackSpawnablesByIntensity: []\n"
      "  PreviewFauna: {fileID: 0}\n"
      "  PreviewFaunaCount: 4\n"
      f"  Vessel: {lib.VESSEL_CLASS_ID['Butterfly']}\n"
      "  ObjectiveText: Weave the cluster, then fold\n"
      "  ObjectiveMetric: 9\n"
      "  ObjectiveTarget: 0\n"
      "  DurationSeconds: 60\n"
      "  SpawnFromCellRing: 1\n"
      "  SpawnDistanceOutsideNucleus: 150\n"
      "  SpawnRingRadiusFloor: 0\n"
      "  SpawnFormation: 1\n"
      "  SpawnPoints: []\n")

# ── the scene, cloned from Switchback ──────────────────────────────────────
DONOR = f"{lib.SCENES_DIR}/MinigameSwitchback.unity"
scene = g.read(DONOR)

scene = lib.swap_guid(scene, EXISTING["SwitchbackController"],
                      G_SCRIPT["WaystationController"], "the controller script")
scene = lib.swap_guid(scene, EXISTING["SwitchbackScoringRule"],
                      G_ASSET["WaystationScoringRule"], "the scoring rule")

# Switchback's own field, which Waystation deliberately does not declare. Unity would keep the
# line as an unresolvable modification forever, so it is removed rather than left.
scene = lib.replace_block(scene, "  firstGateDistance: 620\n", "",
                          "Switchback's firstGateDistance")

# The four AI seats fly the Butterfly.
old_ai = "".join(f"  - vesselClass: 2\n    PlayerName: AI {i}\n    AvatarId: {i}\n"
                 "    IsAI: 1\n    AllowSpawning: 1\n" for i in range(4))
new_ai = "".join(f"  - vesselClass: {lib.VESSEL_CLASS_ID['Butterfly']}\n    PlayerName: AI {i}\n"
                 f"    AvatarId: {i}\n    IsAI: 1\n    AllowSpawning: 1\n" for i in range(4))
scene = lib.replace_block(scene, old_ai, new_ai, "the four AI seats")

# What the donor must still provide. A donor that moves fails LOUDLY here rather than shipping a
# half-wired scene.
for probe, why in (
    (r"^  spawnFormation: 1$", "an EquatorialRing spawn formation (the polar first cluster is "
                               "only fair against one)"),
    (r"^  arrangeSpawnPointsAroundCell: 1$", "cell-relative spawn points"),
    (r"^  courseOuterRadius: 1080$", "the course shell the model verified containment against"),
    (r"^  innerRadiusNucleusFactor: 1\.22$", "the inner shell derivation"),
    (r"^  maxPlausibleSpeed: 400$", "the step guard (a teleport is declined by its COUNTER, not "
                                    "by this, but a respawn is still declined by this)"),
):
    if not course.re.search(probe, scene, course.re.M):
        errors.append(f"the donor scene no longer provides: {why}")

for stale in (EXISTING["SwitchbackController"], EXISTING["SwitchbackScoringRule"]):
    if stale in scene:
        errors.append(f"donor guid {stale} survived the clone")

g.emit_scene("MinigameWaystation", G_ASSET["MinigameWaystation.unity"], scene)

# ── registrations ──────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameWaystation"])
g.register_always_unlocked()
g.register_build_scene("MinigameSkein", "MinigameWaystation",
                       G_ASSET["MinigameWaystation.unity"])
g.set_end_condition("waystationRingTarget", after="switchbackGateTarget", value=RING_TARGET)
g.register_toast_config(G_ASSET["GameToastConfig_Waystation"])
g.register_preview(G_ASSET["ModePreview_Waystation"])

g.finish(errors, referenced=EXISTING, minted=list(G_SCRIPT.values()) + list(G_ASSET.values()))
