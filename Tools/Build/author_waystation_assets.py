#!/usr/bin/env python3
"""Author every asset Waystation adds - the Tiger Moth's Time race, the Drop Tower course.

Waystation is a GATE RACE (GateRaceController, shared with Switchback, Headlong, Breakwater,
Skein and Redline), so it adds no metric, no turn monitor and no scoring class: the rule is a
SECOND ASSET on GateRaceScoringRuleSO and the monitor is the platform's, which asks the
controller. What is genuinely its own is the COURSE - Garrett's clusters of rings you weave, with a
Drop Tower of short hops through floor plates in every gap but the last (Docs/Studios/TIGER_MOTH_TIME.md)
- and that is proved offline by Tools/Build/waystation_tower_course.py (over Garrett's
waystation_course.py), which this script IMPORTS rather than retyping, so the numbers the model verified
are the numbers the game ships.

Since 2026-11-01 the Drop Tower race IS Waystation (Shombith: "make Waystation Prototype as Waystation").
It was built as Waystation Prototype (GameModes 76) beside this mode; this script now authors what that
mode's generator did and RETIRES the prototype's assets and registrations (RETIRED below), so a re-run can
never bring them back.

THE DONOR IS SWITCHBACK, because it is the platform's other OPEN chain (Headlong and Redline lap a
closed circuit and Breakwater carries a lead-in gate). The clone swaps the controller script, the
scoring rule and the four AI hulls (Dolphin -> Tiger Moth), removes Switchback's own fields, and adds
the tower walls' prism (`wallPrefab`).

Run with --check to validate and diff without writing.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib          # noqa: E402
import waystation_course as course     # noqa: E402
import waystation_tower_course as tower  # noqa: E402

MODE_ID = 58
CLUSTERS = tower.CLUSTERS   # clusters a race lays; was a 15-ring target before the towers (2026-11-01)
g = lib.Generator(mode_id=MODE_ID, mode_name="Waystation")
errors = []


def sq(text):
    """A single-quoted YAML scalar doubles its apostrophes. lib.toast and the preview's Notes wrap their text in
    '...' without escaping it, so "The tower's rings" ends the scalar mid-word and the asset does not parse - which a
    text diff (--check) cannot see; the YAML parse at the end can (REFACTOR_BACKLOG S93)."""
    return text.replace("'", "''")

# ── guids read from the repo, never invented ────────────────────────────────
EXISTING = {
    "SO_ArcadeGame":          lib.SCRIPT_GUIDS["SO_ArcadeGame"],
    "GameToastConfigSO":      lib.SCRIPT_GUIDS["GameToastConfigSO"],
    "ModePreviewDefinitionSO": lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"],
    "GateRaceScoringRuleSO":  "349cc0c9402590262de23356775d43cc",
    "SwitchbackController":   "232714002ed12bc27251e8ac09358ff0",
    "SwitchbackScoringRule":  "b67719b27d5ca31d65b90aec65674945",
    "SkimRaceCellConfig":     "52de45c2fab54ea0a35bc37a311d80e6",
    "Butterfly":              lib.VESSELS["TigerMoth"],
    "SpawnablePrism":         lib.existing_guid("Assets/_Prefabs/Trails/SpawnablePrism.prefab"),
}
SPAWNABLE_PRISM_FILE_ID = 4563009547826722997   # the Prism component on SpawnablePrism.prefab

SCRIPTS = {
    "WaystationController": "Assets/_Scripts/Controller/Arcade/Waystation/WaystationController.cs",
    "WaystationCourse":     "Assets/_Scripts/Controller/Arcade/Waystation/WaystationCourse.cs",
    "WaystationTower":      "Assets/_Scripts/Controller/Arcade/Waystation/WaystationTower.cs",
    "WaystationTowerTests": "Assets/_Scripts/Tests/Editor/WaystationTowerTests.cs",
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

if not tower.run(seeds=60, target=CLUSTERS, quiet=True):
    errors.append("waystation_tower_course.py does not pass its own checks - the towers are not shippable")

# ── the comeback rate is a FUNCTION OF THE TARGET ───────────────────────────
# bonusLevels = deficit x rate. The finish line is every ring the course lays: at least the cluster rings (5 clusters
# x 3..6), up to ~15 tower rings more. Sized on the SHORTEST race (I1's 15 cluster rings) so a quarter down always
# buys at least one element level; 0.4 x 15/4 = 1.5 there, 0.4 x 39/4 = 3.9 at the longest. Was 0.64 on 15 rings.
COMEBACK_RATE = 0.4
shortest = CLUSTERS * int(T["RingsPerCluster"][0])
if shortest * 0.25 * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys only {shortest * 0.25 * COMEBACK_RATE:.2f} element levels "
                  "a quarter of the way down the shortest race")

# ── .cs.meta for the scripts, plus the folder ───────────────────────
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
DESCRIPTION = lib.wrap_yaml_scalar(
    "Tiger Moths only. Weave each cluster of rings, then climb down the Drop Tower to the next: short hops through "
    "floor plates, each ring just past its plate. Tap too short and you land on the plate; hold too long and the "
    "pipe ahead meets you before the Drop is back. Plates are mostly plain at low intensity, more of them danger as "
    "it climbs. The last gap is one long drop, and it leaves a wormhole for whoever follows.")
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
      # Whatever the /cardart renderer wrote onto the card, else the placeholder - re-emitting the
      # placeholder unconditionally would undo a rendered background on every run.
      f"  CardBackground: {{fileID: 21300000, guid: {lib.card_background('Waystation')}, type: 3}}\n"
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

# ── toasts: two idle hints in Drop words, and the shared race beats ────────────
# The idle hints need no poster - the toast system fires them off idleSeconds. The race beats
# (DomainRaceHalf / LeadChanged / HomeStretch = 129-131) are posted by GateRaceController's
# DomainRaceToasts for every gate race; a beat this config does not author shows nothing. Worded
# in the open-chain gate races' voice (Skein says "ring" too): {0} = leading domain, {1} = its
# lead runner's rings, {2} = the ring target. No Quarter (128, no gate race authors it) and no
# FinalLap (132): Waystation is one pass of clusters, LapsPerRace 1, so that beat never fires.
g.emit_asset(
    "Assets/_SO_Assets/Game Toasts/GameToastConfig_Waystation.asset",
    G_ASSET["GameToastConfig_Waystation"],
    lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Waystation")
    + f"  gameMode: {MODE_ID}\n  toasts:\n"
    + lib.toast(117, sq("The tower's rings hang under the plates - fly over one and TAP the Drop through it"),
                tint_domain=0, domain_names=0, every_n=1, idle=1, idle_seconds=25)
    + lib.toast(118, sq("Short lands on the plate, long meets the pipe - a quick tap, then roll for the next"),
                tint_domain=0, domain_names=0, every_n=1, idle=1, idle_seconds=40)
    + lib.toast(129, "{0} is halfway home - ring {1}/{2}", tint_domain=1, domain_names=0)
    + lib.toast(130, "{0} takes the lead - ring {1}/{2}", tint_domain=1, domain_names=0)
    + lib.toast(131, "{0} is on the home stretch - ring {1}/{2}", tint_domain=1, domain_names=0))

# ── the preview: the cell to look at, the rings to fly ───────────────────────
NOTES = (
    "The LOOKING phase is the Skim Race cell it runs in (Switchback's). The FLIGHT phase stands the course's rings "
    "(ModePreviewGateCourse, off the same RaceCourseSource the controller builds from): the clusters and the Drop "
    "Towers' rings. The towers' WALLS are laid by the match controller only, so the preview shows the rings without "
    "their plates. Intensity is the COURSE, not the arena.")
g.emit_asset(
    "Assets/_SO_Assets/Mode Previews/ModePreview_Waystation.asset",
    G_ASSET["ModePreview_Waystation"],
    lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Waystation")
    + f"  Mode: {MODE_ID}\n"
      f"  Notes: '{sq(NOTES)}'\n"
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
# Switchback's feature layer (I3 skim walls, I4 danger prisms; SwitchbackController.featuresPrefab, 2026-10-25)
# is also Switchback's own: WaystationController does not declare it. Removed when the donor carries it, so the
# clone stays right whether or not the donor has the field.
scene = re.sub(r"^  featuresPrefab: \{[^\n]*\}\n", "", scene, count=1, flags=re.M)

# The tower walls' prism: the environment prism Serpent Time's cover uses (WaystationController.wallPrefab).
WALL_FIELD = (f"  wallPrefab: {{fileID: {SPAWNABLE_PRISM_FILE_ID}, guid: {EXISTING['SpawnablePrism']}, type: 3}}\n")
scene = lib.replace_block(scene, "  reportResyncSeconds: 3\n", "  reportResyncSeconds: 3\n" + WALL_FIELD,
                          "the controller block's last field")

# The four AI seats fly the Tiger Moth.
old_ai = "".join(f"  - vesselClass: 2\n    PlayerName: AI {i}\n    AvatarId: {i}\n"
                 "    IsAI: 1\n    AllowSpawning: 1\n" for i in range(4))
new_ai = "".join(f"  - vesselClass: {lib.VESSEL_CLASS_ID['TigerMoth']}\n    PlayerName: AI {i}\n"
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

# The Tiger Moth's drop is TRAVEL at 900 u/s (R_VesselActions/TIGER_MOTH.md), 15 u a frame at 60 fps against the
# donor's 400 u/s guard (18 u): a hitch would decline a ring the drop really crossed. 1000 keeps a respawn declined.
scene = scene.replace("  maxPlausibleSpeed: 400\n", "  maxPlausibleSpeed: 1000\n", 1)

# Switchback's feature-layer prefab: a donor-only asset, so a leak of its field fails here by name.
SWITCHBACK_FEATURES_PREFAB = "29ec5eb8fa7cb3fd1d816c63b5b39687"
for stale in (EXISTING["SwitchbackController"], EXISTING["SwitchbackScoringRule"], SWITCHBACK_FEATURES_PREFAB):
    if stale in scene:
        errors.append(f"donor guid {stale} survived the clone")

g.emit_scene("MinigameWaystation", G_ASSET["MinigameWaystation.unity"], scene)

# ── registrations ──────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameWaystation"])
g.register_always_unlocked()
g.register_build_scene("MinigameSkein", "MinigameWaystation",
                       G_ASSET["MinigameWaystation.unity"])
g.set_end_condition("waystationClusterTarget", after="switchbackGateTarget", value=CLUSTERS)
g.register_toast_config(G_ASSET["GameToastConfig_Waystation"])
g.register_preview(G_ASSET["ModePreview_Waystation"])

# ── RETIRED: Waystation Prototype (GameModes 76), folded into this mode on 2026-11-01 ─────────
# Its generator is gone, so its assets and registrations are removed HERE, every run, so that nothing can re-add
# them: the files go (Generator.stale), and every registry this script reads drops their guids and the id 76.
RETIRED = {name: lib.guid(f"asset/{name}") for name in (
    "WaystationPrototypeScoringRule", "ArcadeGameWaystationPrototype", "GameToastConfig_WaystationPrototype",
    "ModePreview_WaystationPrototype")}
RETIRED["MinigameWaystationPrototype.unity"] = lib.guid("scene/MinigameWaystationPrototype")
for rel in ("Assets/_SO_Assets/Scoring Rules/WaystationPrototypeScoringRule.asset",
            "Assets/_SO_Assets/Games/ArcadeGameWaystationPrototype.asset",
            "Assets/_SO_Assets/Game Toasts/GameToastConfig_WaystationPrototype.asset",
            "Assets/_SO_Assets/Mode Previews/ModePreview_WaystationPrototype.asset",
            f"{lib.SCENES_DIR}/MinigameWaystationPrototype.unity",
            "Assets/_Graphics/ARCADE/CardBackgrounds/WaystationPrototype.png"):
    g.stale += [rel, rel + ".meta"]
for registry in ("Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset",
                 "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset",
                 "Assets/Resources/ModePreviewLibrary.asset",
                 "Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset",
                 "ProjectSettings/EditorBuildSettings.asset"):
    text = g.read_current(registry)
    kept = "".join(ln for ln in text.splitlines(keepends=True)
                   if not any(guid in ln for guid in RETIRED.values()))
    # a build-scene entry is three lines; drop its orphaned `- enabled` / `path` lines with the guid line
    kept = re.sub(r"  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameWaystationPrototype\.unity\n",
                  "", kept)
    if kept != text:
        g.emit(registry, kept)
progression = g.read_current("Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset")
if re.search(r"^  - 76\n", progression, re.M):
    g.emit("Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset",
           re.sub(r"^  - 76\n", "", progression, count=1, flags=re.M))
endcond = g.read_current("Assets/Resources/EndConditionOverrides.asset")
cleaned = re.sub(r"^  (waystationRingTarget|waystationPrototypeClusterTarget)(Build)?: \d+\n", "", endcond, flags=re.M)
if cleaned != endcond:
    g.emit("Assets/Resources/EndConditionOverrides.asset", cleaned)
for registry in ("Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset",
                 "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset",
                 "Assets/Resources/ModePreviewLibrary.asset",
                 "Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset",
                 "ProjectSettings/EditorBuildSettings.asset"):
    if any(guid in g.read_current(registry) for guid in RETIRED.values()):
        errors.append(f"{registry} still names a retired Waystation Prototype asset")


def yaml_errors(rel, content):
    """Parse one Unity .asset's documents (the %YAML / %TAG directives and the `--- !u!N &id` tags stripped)."""
    import yaml
    body = "\n".join(ln for ln in content.splitlines() if not ln.startswith("%"))
    out = []
    for doc in re.split(r"^--- !u!\d+ &-?\d+.*$", body, flags=re.M):
        if not doc.strip():
            continue
        try:
            yaml.safe_load(doc)
        except yaml.YAMLError as e:
            out.append(f"{rel} does not parse as YAML: {' '.join(str(e).split())[:160]}")
    return out


# Every authored .asset must PARSE, not only match its own text: --check is a diff, and a diff of a broken file
# against the same broken file is clean.
try:
    import yaml  # noqa: F401
    for rel, content in g.files.items():
        if rel.endswith(".asset"):
            errors += yaml_errors(rel, content)
except ImportError:
    print("note: PyYAML is not installed, so the authored assets were not parsed")

g.finish(errors, referenced=EXISTING, minted=list(G_SCRIPT.values()) + list(G_ASSET.values()))
