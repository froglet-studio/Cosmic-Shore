#!/usr/bin/env python3
"""Author every asset WAYSTATION PROTOTYPE adds - the Tiger Moth's Time race rebuilt around the Drop Tower.

A PROTOTYPE beside Waystation (58), which stays as Garrett made it until he approves a replacement (Shombith,
2026-10-31; Docs/Studios/TIGER_MOTH_TIME.md). So this script READS Waystation's files and never writes one: its scene
is cloned from MinigameWaystation.unity, its controller INHERITS WaystationController, its course CALLS
WaystationCourse.Generate and adds the towers (WaystationTower.cs). The tower numbers are proved offline by
Tools/Build/waystation_prototype_course.py, which this script imports rather than retyping.

The clone changes four things in the scene: the controller script (WaystationPrototypeController), the scoring rule,
the wall prism (SpawnablePrism, the environment prism Serpent Time's cover uses) and Waystation's long-drop threshold,
raised from its 420 u default to 700 u so Garrett's autopilot takes only the long last gap and the clusters while the
prototype's hop autopilot takes the towers (the model's `standdown` check proves every tower link is under it).

Run with --check to validate and diff without writing.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib                  # noqa: E402
import waystation_prototype_course as tower    # noqa: E402

MODE_ID = 76
CLUSTERS = tower.CLUSTERS
AI_FOLD_MIN_DISTANCE = int(tower.AI_FOLD_MIN_DISTANCE)
g = lib.Generator(mode_id=MODE_ID, mode_name="WaystationPrototype")
errors = []

# ── guids read from the repo, never invented ────────────────────────────────
EXISTING = {
    "SO_ArcadeGame":           lib.SCRIPT_GUIDS["SO_ArcadeGame"],
    "GameToastConfigSO":       lib.SCRIPT_GUIDS["GameToastConfigSO"],
    "ModePreviewDefinitionSO": lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"],
    "GateRaceScoringRuleSO":   lib.existing_guid("Assets/_Scripts/Controller/Arcade/Scoring/GateRaceScoringRuleSO.cs"),
    "WaystationController":    lib.existing_guid("Assets/_Scripts/Controller/Arcade/Waystation/WaystationController.cs"),
    "WaystationScoringRule":   lib.existing_guid("Assets/_SO_Assets/Scoring Rules/WaystationScoringRule.asset"),
    "SkimRaceCellConfig":      lib.existing_guid("Assets/_SO_Assets/Cell Configs/Skim Race Cell/Skim Race Cell Config.asset"),
    "SpawnablePrism":          lib.existing_guid("Assets/_Prefabs/Trails/SpawnablePrism.prefab"),
    "TigerMoth":               lib.VESSELS["TigerMoth"],
}
SPAWNABLE_PRISM_FILE_ID = 4563009547826722997   # the Prism component on SpawnablePrism.prefab

FOLDER = "Assets/_Scripts/Controller/Arcade/WaystationPrototype"
SCRIPTS = {
    "WaystationTower":                 f"{FOLDER}/WaystationTower.cs",
    "WaystationPrototypeCourseSource": f"{FOLDER}/WaystationPrototypeCourseSource.cs",
    "WaystationPrototypeController":   f"{FOLDER}/WaystationPrototypeController.cs",
    "WaystationTowerTests":            "Assets/_Scripts/Tests/Editor/WaystationTowerTests.cs",
}
G_SCRIPT = {name: lib.guid(f"script/{name}") for name in SCRIPTS}
G_ASSET = {
    "WaystationPrototypeScoringRule":      lib.guid("asset/WaystationPrototypeScoringRule"),
    "ArcadeGameWaystationPrototype":       lib.guid("asset/ArcadeGameWaystationPrototype"),
    "GameToastConfig_WaystationPrototype": lib.guid("asset/GameToastConfig_WaystationPrototype"),
    "ModePreview_WaystationPrototype":     lib.guid("asset/ModePreview_WaystationPrototype"),
    "MinigameWaystationPrototype.unity":   lib.guid("scene/MinigameWaystationPrototype"),
}

# ── the tower model is the authority on the mode's own numbers ──────────────
if not tower.run(seeds=60, target=CLUSTERS, quiet=True):
    errors.append("waystation_prototype_course.py does not pass its own checks - the towers are not shippable")

# ── the comeback rate is a FUNCTION OF THE TARGET ───────────────────────────
# bonusLevels = deficit x rate. The finish line is every ring the course lays: at least the cluster rings (5 clusters
# x 3..6), up to ~15 tower rings more. Sized on the SHORTEST race (I1's 15 cluster rings) so a quarter down always
# buys at least one element level; 0.4 x 15/4 = 1.5 there, 0.4 x 39/4 = 3.9 at the longest.
COMEBACK_RATE = 0.4
shortest = CLUSTERS * 3
if shortest * 0.25 * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys only {shortest * 0.25 * COMEBACK_RATE:.2f} element levels "
                  "a quarter of the way down the shortest race")

# ── .cs.meta for the new scripts, plus the folder and the doc ───────────────
g.folder_meta(FOLDER)
for name, rel in SCRIPTS.items():
    g.script_meta(rel, G_SCRIPT[name])
g.text_meta(f"{FOLDER}/WAYSTATION_PROTOTYPE.md")

# ── the scoring rule: a third asset on GateRaceScoringRuleSO ────────────────
g.emit_asset(
    "Assets/_SO_Assets/Scoring Rules/WaystationPrototypeScoringRule.asset",
    G_ASSET["WaystationPrototypeScoringRule"],
    lib.header_for(EXISTING["GateRaceScoringRuleSO"], "WaystationPrototypeScoringRule")
    + "  metric: 9\n  golfRules: 1\n")

# ── the arcade card ─────────────────────────────────────────────────────────
DESCRIPTION = lib.wrap_yaml_scalar(
    "Tiger Moths only - a prototype of Waystation. Weave each cluster, then climb down the Drop Tower to the next: "
    "short hops through floor plates, each ring just past its plate. Tap too short and you land on the plate; hold "
    "too long and the pipe ahead meets you before the Drop is back. Plates are mostly plain at low intensity, more "
    "of them danger as it climbs. The last gap is one long drop, and it leaves a wormhole for whoever follows.")
g.emit_asset(
    "Assets/_SO_Assets/Games/ArcadeGameWaystationPrototype.asset",
    G_ASSET["ArcadeGameWaystationPrototype"],
    lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameWaystationPrototype")
    + f"  Mode: {MODE_ID}\n"
      "  IsMultiplayer: 1\n"
      "  DisplayName: Waystation Prototype\n"
      f"  Description: {DESCRIPTION}\n"
      f"  IconActive: {{fileID: 21300000, guid: {lib.CARD_ART['IconActive']}, type: 3}}\n"
      f"  IconInactive: {{fileID: 21300000, guid: {lib.CARD_ART['IconInactive']}, type: 3}}\n"
      f"  CardBackground: {{fileID: 21300000, guid: {lib.card_background('WaystationPrototype')}, type: 3}}\n"
      "  GolfScoring: 1\n"
      "  SceneName: MinigameWaystationPrototype\n"
      "  Vessels:\n"
      f"  - {{fileID: 11400000, guid: {EXISTING['TigerMoth']}, type: 2}}\n"
      "  MinPlayersAllowed: 2\n"
      "  MaxPlayersAllowed: 4\n"
      "  MinDomainsAllowed: 2\n"
      "  MaxDomainsAllowed: 3\n"
      "  MinIntensity: 1\n"
      "  MaxIntensity: 4\n"
      "  ViewUserAction: 0\n"
      "  PlayUserAction: 0\n"
      f"  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}\n")

# ── toasts: Waystation's two idle-hint situations (117, 118) in Drop words, and the shared race beats ──────────
# No new GameToastSituation: the two idle hints are situations the toast system fires off idleSeconds for whatever
# config the mode carries, so this config gives them the prototype's own words, and the race beats (129-131) are
# posted by GateRaceController for every gate race.
g.emit_asset(
    "Assets/_SO_Assets/Game Toasts/GameToastConfig_WaystationPrototype.asset",
    G_ASSET["GameToastConfig_WaystationPrototype"],
    lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_WaystationPrototype")
    + f"  gameMode: {MODE_ID}\n  toasts:\n"
    + lib.toast(117, "The tower's rings hang under the plates - fly over one and TAP the Drop through it",
                tint_domain=0, domain_names=0, every_n=1, idle=1, idle_seconds=25)
    + lib.toast(118, "Short lands on the plate, long meets the pipe - a quick tap, then roll for the next",
                tint_domain=0, domain_names=0, every_n=1, idle=1, idle_seconds=40)
    + lib.toast(129, "{0} is halfway home - ring {1}/{2}", tint_domain=1, domain_names=0)
    + lib.toast(130, "{0} takes the lead - ring {1}/{2}", tint_domain=1, domain_names=0)
    + lib.toast(131, "{0} is on the home stretch - ring {1}/{2}", tint_domain=1, domain_names=0))

# ── the preview: the cell to look at, the rings to fly ───────────────────────
NOTES = (
    "The LOOKING phase is the Skim Race cell Waystation runs in. The FLIGHT phase stands the course's rings "
    "(ModePreviewGateCourse, off the same RaceCourseSource the controller builds from): the clusters and the Drop "
    "Towers' rings. The towers' WALLS are laid by the match controller only, so the preview shows the rings without "
    "their plates. Intensity is the COURSE, not the arena.")
g.emit_asset(
    "Assets/_SO_Assets/Mode Previews/ModePreview_WaystationPrototype.asset",
    G_ASSET["ModePreview_WaystationPrototype"],
    lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_WaystationPrototype")
    + f"  Mode: {MODE_ID}\n"
      f"  Notes: '{NOTES}'\n"
      f"  PreviewCell: {{fileID: 11400000, guid: {EXISTING['SkimRaceCellConfig']}, type: 2}}\n"
      "  PreviewCellsByIntensity: []\n"
      "  StructurePrefab: {fileID: 0}\n"
      "  TrackSpawnablesByIntensity: []\n"
      "  PreviewFauna: {fileID: 0}\n"
      "  PreviewFaunaCount: 4\n"
      f"  Vessel: {lib.VESSEL_CLASS_ID['TigerMoth']}\n"
      "  ObjectiveText: Weave the cluster, hop the tower\n"
      "  ObjectiveMetric: 9\n"
      "  ObjectiveTarget: 0\n"
      "  DurationSeconds: 60\n"
      "  SpawnFromCellRing: 1\n"
      "  SpawnDistanceOutsideNucleus: 150\n"
      "  SpawnRingRadiusFloor: 0\n"
      "  SpawnFormation: 1\n"
      "  SpawnPoints: []\n")

# ── the scene, cloned from Waystation's (read, never written) ──────────────
SCENE_REL = f"{lib.SCENES_DIR}/MinigameWaystationPrototype.unity"
FIELD_ANCHOR = "  maxPlausibleSpeed: 1000\n  reportResyncSeconds: 3\n"
PROTOTYPE_FIELDS = (
    f"  aiFoldMinDistance: {AI_FOLD_MIN_DISTANCE}\n"
    f"  wallPrefab: {{fileID: {SPAWNABLE_PRISM_FILE_ID}, guid: {EXISTING['SpawnablePrism']}, type: 3}}\n")


def build_clone():
    scene = g.read(f"{lib.SCENES_DIR}/MinigameWaystation.unity")
    scene = lib.swap_guid(scene, EXISTING["WaystationController"], G_SCRIPT["WaystationPrototypeController"],
                          "the controller script")
    scene = lib.swap_guid(scene, EXISTING["WaystationScoringRule"], G_ASSET["WaystationPrototypeScoringRule"],
                          "the scoring rule")
    assert scene.count(FIELD_ANCHOR) == 1, "Waystation's controller block no longer ends at reportResyncSeconds"
    return scene.replace(FIELD_ANCHOR, FIELD_ANCHOR + PROTOTYPE_FIELDS, 1)


scene, scene_errors = lib.committed_scene(SCENE_REL, build_clone, authored_blocks=(PROTOTYPE_FIELDS,))
errors += scene_errors

for stale in (EXISTING["WaystationController"], EXISTING["WaystationScoringRule"]):
    if stale in scene:
        errors.append(f"Waystation's guid {stale} survived the clone - the prototype would run Waystation itself")
for probe in ("  courseOuterRadius: 1080\n", "  innerRadiusNucleusFactor: 1.22\n", "  maxPlausibleSpeed: 1000\n",
              f"  - vesselClass: {lib.VESSEL_CLASS_ID['TigerMoth']}\n"):
    if probe not in scene:
        errors.append(f"the scene no longer carries {probe.strip()!r}, which the tower model assumes")

g.emit_scene("MinigameWaystationPrototype", G_ASSET["MinigameWaystationPrototype.unity"], scene)

# ── registrations ──────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameWaystationPrototype"])
g.register_always_unlocked()
g.register_build_scene("MinigameWaystation", "MinigameWaystationPrototype",
                       G_ASSET["MinigameWaystationPrototype.unity"])
g.set_end_condition("waystationPrototypeClusterTarget", after="burrowRunGateTarget", value=CLUSTERS)
g.register_toast_config(G_ASSET["GameToastConfig_WaystationPrototype"])
g.register_preview(G_ASSET["ModePreview_WaystationPrototype"])

g.finish(errors, referenced=EXISTING, minted=list(G_SCRIPT.values()) + list(G_ASSET.values()))
