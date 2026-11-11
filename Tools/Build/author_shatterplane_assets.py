#!/usr/bin/env python3
"""
Authors every serialized asset the Shatterplane game mode needs (GameModes.Shatterplane = 90; it took 88 first, which Harvest landed with).

Shatterplane is the Brittle Star's SPACE game (Shombith, 2026-11-10; Docs/MassAndSpaceGames/BRITTLE_STAR_SPACE.md): wide,
thin round PLATES of neutral prisms hang in the cell. Square up to a plate and the thrown ring's flat blast erases it
whole; a few degrees off, it cuts one band across it; the guns chew what the ring misses. Every crystal floats on a
plate's axis in front of it, so taking one lines a pilot up. The score is mass taken off the board
(ScoringMetric.PrismsDestroyed, Rampage's rule), so the card reads Space. First DOMAIN to the per-intensity target wins,
golf-timed.

THE DECISIONS (Shombith, 2026-11-10):
  D1 plates (the alternative, a gun race on wreckage clumps, was rendered and dropped: a ring takes ~11% of a clump);
  D2 the ring's reach "a little bigger": the arena's nucleus is NUCLEUS_RADIUS (470 u against a standard cell's ~392);
     the ring's disk is as wide as the nucleus it blows in, so the mode sizes its nucleus, never the vessel;
  D3 no wildlife at all; D4 the name; D5 straight to Unity.
  Then (Shombith, 2026-11-10): "for intensities 3 make elemental crystals of space just for teams present there and in
  i4 it has live elemental faunas" - I3's crystal field is Space, one crystal per team present at every site, each
  collectable by its team alone (ElementalCrystalImpactor.RestrictTo, shared with Harvest); I4's cell carries live elemental fauna (its own spawn
  profile: the Blob tadpole and shark, each creature rolling one of four elements), and I1-I3 still carry none.

THE LADDER (one SpawnableShatterplane prefab and one cell config per intensity; shatterplane_budget.py is the census):
  I1 Billboards - ten plates facing straight out at the spawn ring.     Can you square up at all?
  I2 Gallery    - twelve plates at mixed angles.                        Can you swing round to square up?
  I3 Shelves    - five stacks of three parallel plates 70 u apart.      Front plate first.
  I4 Reliquary  - eight ribbed, danger-rimmed plates, half behind grilles.  Find the square line through the frame.
Nothing the arena lays comes within SPAWN_CLEAR of the spawn ring.

NOTHING ABOUT THE BRITTLE STAR CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the arena
(the plates and the nucleus), the crystal field and the AI (a squarer that steers; the hull's own autopilot keeps every
trigger, mode and ring press).

The scene is cloned from Flatline's (the Brittle Star's other arena, whose controller declares settings / rule /
arenaCell and whose AI templates are already four Brittle Stars).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_shatterplane_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import shatterplane_budget as sb  # noqa: E402

MODE_ID = 90
g = lib.Generator(MODE_ID, "Shatterplane")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
MG = "Assets/_Scripts/Controller/Environment/MiniGameObjects"
SCRIPT_PATHS = {
    "ShatterplaneController":       f"{AR}/Shatterplane/ShatterplaneController.cs",
    "ShatterplaneSettingsSO":       f"{AR}/Shatterplane/ShatterplaneSettingsSO.cs",
    "ShatterplaneScoringRuleSO":    f"{AR}/Shatterplane/ShatterplaneScoringRuleSO.cs",
    "ShatterplaneSquarer":          f"{AR}/Shatterplane/ShatterplaneSquarer.cs",
    "ShatterplanePrismTurnMonitor": f"{AR}/TurnMonitors/ShatterplanePrismTurnMonitor.cs",
    "SpawnableShatterplane":        f"{MG}/SpawnableShatterplane.cs",
    "ShatterplanePlates":           f"{MG}/ShatterplanePlates.cs",
    "ShatterplaneTests":            "Assets/_Scripts/Tests/Editor/ShatterplaneTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Shatterplane Cell"
G_ASSET = {
    "ArcadeGameShatterplane":      guid("asset/ArcadeGameShatterplane"),
    "ShatterplaneScoringRule":     guid("asset/ShatterplaneScoringRule"),
    "ShatterplaneSettings":        guid("asset/ShatterplaneSettings"),
    "GameToastConfigShatterplane": guid("asset/GameToastConfigShatterplane"),
    "ModePreviewShatterplane":     guid("asset/ModePreviewShatterplane"),
    "MinigameShatterplane.unity":  guid("asset/MinigameShatterplane.unity"),
    "ShatterplaneSpawnProfile":    guid("asset/ShatterplaneSpawnProfile"),
    "ShatterplaneSpawnProfile4":   guid("asset/ShatterplaneSpawnProfile4"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"ShatterplaneCellConfig{i}"] = guid(f"asset/ShatterplaneCellConfig{i}")
    G_ASSET[f"prefab/SpawnableShatterplane{i}"] = guid(f"asset/SpawnableShatterplane{i}.prefab")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
VESSEL_BRITTLESTAR = lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_BrittleStar.asset")
EXISTING["Vessel_BrittleStar"] = VESSEL_BRITTLESTAR
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"           # SpawnablePrism.prefab (window 0.5-100)
EXISTING["Membrane_prefab"] = lib.CELL_VISUALS["MembranePrefab"]         # the standard shell, radius 1200
EXISTING["Nucleus_prefab"] = lib.CELL_VISUALS["NucleusPrefab"]           # the standard nucleus, resized by the controller
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
BRITTLESTAR_CONFIG = "Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset"
EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarExecutor.cs"
HULL_FORM = "Assets/_Scripts/Controller/Vessel/BrittleStarHullForm.cs"
MODE_GENRE = "Assets/_Scripts/Data/ModeGenre.cs"

# The donor scene's identity, minted by author_flatline_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameFlatline.unity"
DONOR = {
    "FlatlineController":       guid("script/FlatlineController"),
    "FlatlinePointTurnMonitor": guid("script/FlatlinePointTurnMonitor"),
    "FlatlineSettings":         guid("asset/FlatlineSettings"),
    "FlatlineScoringRule":      guid("asset/FlatlineScoringRule"),
}
DONOR_CELLS = [guid(f"asset/FlatlineCellConfig{i}") for i in (1, 2, 3, 4)]
BRITTLESTAR_ID = lib.VESSEL_CLASS_ID["BrittleStar"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
NUCLEUS_FILEID = 7555898194514117247
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 8800000000000201, 8800000000000202, 8800000000000203

# ── The race ─────────────────────────────────────────────────────────────────
METRIC_PRISMS_DESTROYED = 5   # ScoringMetric.PrismsDestroyed
# The end target: about TARGET_SHARE of a rung's breakable plate mass, rounded to 100. Three domains race over one
# contested field, so the first to a third of it wins well before the plates run out. An ESTIMATE until a timed match
# (P-modes-02: an experienced player finishes in about 90 s); the per-intensity list lets each rung be re-tuned alone.
TARGET_SHARE = 0.30
CENSUS = {r["i"]: sb.census(r) for r in sb.RUNGS}
TARGET_BY_INTENSITY = [int(round(TARGET_SHARE * CENSUS[i]["breakable"] / 100.0)) * 100 for i in (1, 2, 3, 4)]
TARGET_SCALAR = TARGET_BY_INTENSITY[0]   # the fallback for a rung that authors nothing
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the smallest target behind buys
# more than one element level.
COMEBACK_RATE = 0.005

# ── The arena (the standard 1,200 u shell at every rung) ─────────────────────
MEMBRANE_RADIUS = lib.MEMBRANE_RADIUS
CYTOPLASM_SHARD_DISTANCE = round(120 * MEMBRANE_RADIUS / 1200)
SPAWN_RING = int(sb.SPAWN_RING)
NUCLEUS_RADIUS = sb.NUCLEUS_RADIUS
STANDARD_NUCLEUS = 392.0  # a standard cell's nucleus (the Rampage I2 cell), the radius D2 makes "a little bigger"
OMNI_RADIUS = 200         # the scene's crystal manager's no-nucleus radius, kept from the donor (the cell has one now)

RUNGS = sb.RUNGS
for r in RUNGS:
    r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableShatterplane{r['i']}.prefab"

# Cleave's ladder deltas: a cell's phase rides ITS arena's own baseline. No fauna here, so the phase only matters as the
# cell's own state, but it must not start a match in the wrong one.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameShatterplane.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Shatterplane")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/SHATTERPLANE.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/ShatterplaneScoringRule.asset", G_ASSET["ShatterplaneScoringRule"],
             lib.header_for(G_SCRIPT["ShatterplaneScoringRuleSO"], "ShatterplaneScoringRule") +
             f"""  metric: {METRIC_PRISMS_DESTROYED}
  golfRules: 1
  prismsLeftTemplate: '{{count}} Prisms Left'
  breakdownTemplate: '{{count}} Prisms'
  victoryHeader: VICTORY
  victoryLabel: SHATTERPLANE TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":    ([2.4, 1.8, 1.2, 0.8], "down"),
    "aiApproachDistance":   ([260, 300, 340, 380], "up"),
    "aiSquareDegrees":      ([25, 18, 12, 8], "down"),
    "aiCrystalDetourRange": ([300, 400, 500, 650], "up"),
}
FIELD_REGROW_SECONDS = 8
FIELD_SEED = 88
# The crystal field per intensity (index = intensity - 1): the element (0 None = a random draw per site, 3 Space) and
# whether each site holds one crystal per team present, collectable by that team alone.
ELEMENT_SPACE = 3
FIELD_ELEMENT_BY_INTENSITY = [0, 0, ELEMENT_SPACE, 0]
FIELD_PER_TEAM_BY_INTENSITY = [0, 0, 1, 0]
TEAM_CRYSTAL_SPACING = 30
# I4's live elemental fauna: Rampage's own two species, referenced READ-ONLY (shared Blob assets: the amount lives on
# the profile, never on them), each spawning creatures that roll one of the four elements from its ElementPalette.
FAUNA_INTENSITIES = (4,)
FAUNA_SPECIES = {
    "Tadpole": "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Tadpole Fauna Config Data.asset",
    "Shark":   "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset",
}
FAUNA_POPULATION_SCALE = 2.0


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/ShatterplaneSettings.asset", G_ASSET["ShatterplaneSettings"],
             lib.header_for(G_SCRIPT["ShatterplaneSettingsSO"], "ShatterplaneSettings") +
             f"""  nucleusRadius: {lib.num(NUCLEUS_RADIUS)}
  progressSampleSeconds: 0.5
  fieldRegrowSeconds: {FIELD_REGROW_SECONDS}
  fieldSeed: {FIELD_SEED}
  fieldElementByIntensity:
{"".join(f"  - {e}\n" for e in FIELD_ELEMENT_BY_INTENSITY)}  fieldPerTeamByIntensity:
{"".join(f"  - {b}\n" for b in FIELD_PER_TEAM_BY_INTENSITY)}  teamCrystalSpacing: {TEAM_CRYSTAL_SPACING}
""" + "".join(ladder_yaml(n) for n in LADDERS))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 2 is a RULE: a race against the arena (Sirocco's shape) - a solo pilot races the AI domain.
DESCRIPTION = ("Brittle Stars only - break the plates hanging in the cell. Your guns chip any plate from any angle, "
               "but square up to a plate and throw your ring, and its flat blast erases the whole plate. A few degrees "
               "off, it only cuts a stripe. Every crystal is a spare ring and floats on the axis of a plate, so taking "
               "one lines you up. First team to break the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameShatterplane"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameShatterplane") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Shatterplane
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameShatterplane
  Vessels:
  - {{fileID: 11400000, guid: {VESSEL_BRITTLESTAR}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Shatterplane.asset", G_ASSET["GameToastConfigShatterplane"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Shatterplane") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has broken {1} prisms", tint_domain=1, domain_names=0, every_n=200) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(165, "Square up to a plate and throw your ring - its blast erases the whole plate. Crystals "
                            "on a plate's axis line you up", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigShatterplane"])

# ── 6. The arena: one environment prefab per rung, four cell configs, one spawn profile ──
for r in RUNGS:
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableShatterplane{r['i']}"], f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{SPAWNABLE_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {SPAWNABLE_TR}}}
  - component: {{fileID: {SPAWNABLE_MB}}}
  m_Layer: 0
  m_Name: SpawnableShatterplane{r['i']}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{SPAWNABLE_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{SPAWNABLE_MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableShatterplane']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {r['seed']}
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: {r['i']}
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  innerRadius: {lib.num(r['innerRadius'])}
  outerRadius: {lib.num(r['outerRadius'])}
  structureCount: {r['structureCount']}
  plateRadius: {r['plateRadius']}
  platesPerStack: {r['platesPerStack']}
  shelfGap: {lib.num(r['shelfGap'])}
  faceOut: {r['faceOut']}
  ribEvery: {r['ribEvery']}
  dangerRim: {r['dangerRim']}
  screenEvery: {r['screenEvery']}
  screenRadius: {r['screenRadius']}
  screenOffset: {lib.num(r['screenOffset'])}
  screenRibEvery: {r['screenRibEvery']}
  crystalCount: {r['crystalCount']}
""")

FIELD_NOTE = {i: (" (Space crystals, one per team present at every site, each collectable by its team alone)"
                  if FIELD_PER_TEAM_BY_INTENSITY[i - 1] else "") for i in (1, 2, 3, 4)}
FAUNA_NOTE = {i: ("live elemental fauna (the Blob tadpole and shark, each creature rolling one of four elements, at "
                  f"{FAUNA_POPULATION_SCALE:g}x their authored populations)" if i in FAUNA_INTENSITIES else "no fauna")
              for i in (1, 2, 3, 4)}
for r in RUNGS:
    i = r["i"]
    c = CENSUS[i]
    th = thresholds(c["total"], round(c["volume"]))
    profile = G_ASSET["ShatterplaneSpawnProfile4"] if i in FAUNA_INTENSITIES else G_ASSET["ShatterplaneSpawnProfile"]
    env = f"{{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableShatterplane{i}']}, type: 3}}"
    desc = (f"{r['label']} - intensity {i} of Shatterplane: {r['blurb']}. {c['total']} prisms, all neutral "
            f"({c['breakable']} breakable, {c['super']} super-shielded, {c['danger']} danger). Membrane radius "
            f"{int(MEMBRANE_RADIUS)}, spawn ring {SPAWN_RING}, the standard nucleus resized to "
            f"{int(NUCLEUS_RADIUS)} u by the controller (the ring blast is as wide as the nucleus; play geometry, not a "
            f"claim), the crystal field laid by the controller on the plates' axes{FIELD_NOTE[i]}. No flora; {FAUNA_NOTE[i]}. Authored by "
            f"Tools/Build/author_shatterplane_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Shatterplane Cell Config {i}.asset", G_ASSET[f"ShatterplaneCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Shatterplane Cell Config {i}") + f"""  CellName: Shatterplane
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['Membrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS_FILEID}, guid: {EXISTING['Nucleus_prefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: {CYTOPLASM_SHARD_DISTANCE}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {profile}, type: 2}}
  EnvironmentPrefab: {env}
  EnvironmentIntensity: {i}
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {th['RestlessEnter']}
    RestlessExit: {th['RestlessExit']}
    FrenzyEnter: {th['FrenzyEnter']}
    FrenzyExit: {th['FrenzyExit']}
    RestlessEnterVolume: {th['RestlessEnterVolume']}
    RestlessExitVolume: {th['RestlessExitVolume']}
    FrenzyEnterVolume: {th['FrenzyEnterVolume']}
    FrenzyExitVolume: {th['FrenzyExitVolume']}
""")

# The spawn profile I1-I3 share: it spawns nothing (decision D3, kept for those rungs).
g.emit_asset(f"{CELL_DIR}/Shatterplane Spawn Profile.asset", G_ASSET["ShatterplaneSpawnProfile"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Shatterplane Spawn Profile") + """  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras: []
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 0
  InitialFaunaReleaseTier: 0
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 15
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 0
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0.5
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 0
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas: []
""")

# I4's own profile: live elemental fauna, no flora. The species are referenced, never edited; the amount is this
# profile's FaunaPopulationScale. Fauna spawn on the densest sensed mass (no spawn-point ring), which is a plate.
FAUNA_GUIDS = {name: lib.existing_guid(path) for name, path in FAUNA_SPECIES.items()}
EXISTING.update({f"Fauna_{name}": gd for name, gd in FAUNA_GUIDS.items()})
g.emit_asset(f"{CELL_DIR}/Shatterplane Spawn Profile 4.asset", G_ASSET["ShatterplaneSpawnProfile4"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Shatterplane Spawn Profile 4") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras: []
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 10
  InitialFaunaReleaseTier: 2147483647
  FaunaSpawnVolumeThreshold: 1
  FaunaPopulationScale: {lib.num(FAUNA_POPULATION_SCALE)}
  BaseFaunaSpawnTime: 30
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 5
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 1
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 0
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas:
""" + "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in FAUNA_GUIDS.values()))

CELLS = [G_ASSET[f"ShatterplaneCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {SPAWN_RING}\n  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 680\n  spawnFormation: 0\n")
NO_NUCLEUS = f"  noNucleusSpawnRadius: {OMNI_RADIUS}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Shatterplane.asset", G_ASSET["ModePreviewShatterplane"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Shatterplane") + f"""  Mode: {MODE_ID}
  Notes: 'The Shatterplane cell at each intensity - billboards, the gallery, the shelves and the reliquary. A race to a
    prism target; a solo preview teaches squaring up to a plate and the ring. Vessel pinned to BrittleStar({BRITTLESTAR_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {BRITTLESTAR_ID}
  ObjectiveText: Square up to a plate and throw your ring
  ObjectiveMetric: {METRIC_PRISMS_DESTROYED}
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewShatterplane"])

# ── 8. Scene: Flatline's, with the identity, the cells and the spawn ring swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["FlatlinePointTurnMonitor"], G_SCRIPT["ShatterplanePrismTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["FlatlineController"], G_SCRIPT["ShatterplaneController"], "controller")
    scene = lib.swap_guid(scene, DONOR["FlatlineSettings"], G_ASSET["ShatterplaneSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["FlatlineScoringRule"], G_ASSET["ShatterplaneScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, NO_NUCLEUS]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameShatterplane.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameShatterplane", G_ASSET["MinigameShatterplane.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameShatterplane"])
g.register_always_unlocked()
g.register_build_scene("MinigameFlatline", "MinigameShatterplane", G_ASSET["MinigameShatterplane.unity"])
g.set_end_condition("shatterplanePrismTarget", after="coronaBloomTarget", value=TARGET_SCALAR)

# The per-intensity list rides beside each scalar (Cleave's shape; set_end_condition authors scalars only).
endcond = g.read_current(lib.END_CONDITIONS)
_target_list = "".join(f"  - {t}\n" for t in TARGET_BY_INTENSITY)
for scalar_key, list_key in (("shatterplanePrismTarget", "shatterplanePrismTargetByIntensity"),
                             ("shatterplanePrismTargetBuild", "shatterplanePrismTargetByIntensityBuild")):
    endcond, n = re.subn(rf"  {list_key}:(?: \[\]\n|\n(?:  - \d+\n)+)", f"  {list_key}:\n{_target_list}",
                         endcond, count=1)
    if n == 0:
        m = re.search(rf"^  {scalar_key}: \d+\n", endcond, re.M)
        if not m:
            errors.append(f"{scalar_key} not found in {lib.END_CONDITIONS}")
            continue
        endcond = endcond.replace(m.group(0), m.group(0) + f"  {list_key}:\n{_target_list}", 1)
g.emit(lib.END_CONDITIONS, endcond)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * min(TARGET_BY_INTENSITY) * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {min(TARGET_BY_INTENSITY)}: a "
                  f"quarter-of-target deficit buys {0.25 * min(TARGET_BY_INTENSITY) * COMEBACK_RATE:.2f} levels (< 1)")
for i, t in zip((1, 2, 3, 4), TARGET_BY_INTENSITY):
    share = t / CENSUS[i]["breakable"]
    if not 0.2 <= share <= 0.4:
        errors.append(f"I{i}: the target {t} is {share:.0%} of the breakable plate mass - three domains share one field")

# The C# defaults match the authored ones.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultShatterplanePrismTarget = {TARGET_SCALAR};" not in eco:
    errors.append(f"EndConditionOverridesSO.DefaultShatterplanePrismTarget is not {TARGET_SCALAR}")
if f"public int shatterplanePrismTarget = {TARGET_SCALAR};" not in eco or \
        f"public int shatterplanePrismTargetBuild = {TARGET_SCALAR};" not in eco:
    errors.append(f"EndConditionOverridesSO's shatterplane scalar initializers are not {TARGET_SCALAR}")
_init = "new() { " + ", ".join(str(t) for t in TARGET_BY_INTENSITY) + " }"
if eco.count(f"shatterplanePrismTargetByIntensity = {_init};") != 1 or \
        eco.count(f"shatterplanePrismTargetByIntensityBuild = {_init};") != 1:
    errors.append(f"EndConditionOverridesSO's shatterplane per-intensity initializers are not {_init}")
tests_cs = g.read(SCRIPT_PATHS["ShatterplaneTests"])
for i, t in zip((1, 2, 3, 4), TARGET_BY_INTENSITY):
    if f"Assert.AreEqual({t}, eco.GetShatterplanePrismTarget({i}));" not in tests_cs:
        errors.append(f"ShatterplaneTests does not pin I{i}'s target {t}")

# The genre: PrismsDestroyed is a Space metric, and no mode row overrides it.
genre = g.read(MODE_GENRE)
if not re.search(r"case ScoringMetric\.PrismsDestroyed:[\s\S]{0,400}?Element\.Space", genre):
    errors.append("ModeGenre no longer reads PrismsDestroyed as Space - the card would not be the Space game")
if "GameModes.Shatterplane" in genre:
    errors.append("ModeGenre has a Shatterplane row - the mode reads Space through its metric; delete the row")

# The vessel the arena is built around, read from its shipped config and code (never changed here).
bs_config = g.read(BRITTLESTAR_CONFIG)
for needle in ("  ringBlastThicknessScale: 4\n", "  ringLaunchSpeed: 240\n", "  ringFuseSeconds: 1.1\n"):
    if needle not in bs_config:
        errors.append(f"BrittleStarConfig no longer has '{needle.strip()}' - re-check shatterplane_budget.py's ring model")
if abs(sb.RING_THICKNESS_SCALE - 4.0) > 1e-6:
    errors.append("shatterplane_budget.RING_THICKNESS_SCALE disagrees with the config")
executor = g.read(EXECUTOR)
for needle in ("float r = cell ? cell.NucleusVisualWorldRadius : 0f;", "BrittleStarRules.DiskReaches(d, wave.Normal"):
    if needle not in executor:
        errors.append(f"BrittleStarExecutor no longer has '{needle}' - the ring may no longer read the nucleus "
                      "or blast as a disk; re-check the whole design")
if "StrutThickness = 0.22f" not in g.read(HULL_FORM) and "strutThickness = 0.22f" not in g.read(HULL_FORM):
    errors.append("BrittleStarHullForm's strut thickness moved off 0.22 - re-check the ring's half-thickness")
if NUCLEUS_RADIUS <= STANDARD_NUCLEUS or NUCLEUS_RADIUS > STANDARD_NUCLEUS * 1.3:
    errors.append(f"the nucleus {NUCLEUS_RADIUS} is not 'a little bigger' than a standard cell's {STANDARD_NUCLEUS}")

# The ring is the point: square takes a whole plate on every rung, five degrees off takes under half.
for r in RUNGS:
    full = sb.ring_yield(r, 0.0)
    plate = sb.census(dict(r, structureCount=1, platesPerStack=1, screenEvery=0))
    if full != plate["breakable"]:
        errors.append(f"I{r['i']}: a square ring takes {full} of a plate's {plate['breakable']} breakable tiles")
    if sb.ring_yield(r, 5.0) * 2 >= full:
        errors.append(f"I{r['i']}: five degrees off square still takes half a plate - squaring up would not matter")
    if (r["plateRadius"] + 0.5) * sb.TILE * 2 > NUCLEUS_RADIUS:
        errors.append(f"I{r['i']}: a plate is wider than the ring reaches from its edge")

# The ladders: monotone toward harder; the C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["ShatterplaneSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for needle in (f"nucleusRadius = {lib.num(NUCLEUS_RADIUS)}f;", f"fieldRegrowSeconds = {FIELD_REGROW_SECONDS}f;",
               f"fieldSeed = {FIELD_SEED};",
               "fieldElementByIntensity = { " + ", ".join(
                   {0: "Element.None", 3: "Element.Space"}[e] for e in FIELD_ELEMENT_BY_INTENSITY) + " };",
               "fieldPerTeamByIntensity = { " + ", ".join(
                   "true" if b else "false" for b in FIELD_PER_TEAM_BY_INTENSITY) + " };",
               f"teamCrystalSpacing = {TEAM_CRYSTAL_SPACING}f;"):
    if needle not in settings_cs:
        errors.append(f"ShatterplaneSettingsSO's default is not '{needle}'")
squarer = g.read(SCRIPT_PATHS["ShatterplaneSquarer"])
if "SelectedIntensity" in squarer or "AIDifficultyFor(" not in squarer:
    errors.append("ShatterplaneSquarer must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
for banned in ("PerformShipControllerActions", "InputStatus", "StopShipControllerActions"):
    if banned in squarer:
        errors.append(f"ShatterplaneSquarer uses {banned} - the Brittle Star's own autopilot owns its presses")
controller = g.read(SCRIPT_PATHS["ShatterplaneController"])
for banned in ("BrittleStarConfig", "RingBlastFallbackRadius", "PerformShipControllerActions", "SetSpawnerPaused"):
    if banned in controller:
        errors.append(f"ShatterplaneController touches {banned} - the mode never changes the vessel")
if "SetNucleusWorldRadius(settings.nucleusRadius)" not in controller or "NucleusIsControlZone = false" not in controller:
    errors.append("ShatterplaneController no longer sizes the nucleus (the ring's reach, decision D2)")

# The C# twin of this census stays the C#: its constants and rules must still be the ones counted here.
plates_cs = g.read(SCRIPT_PATHS["ShatterplanePlates"])
for needle in (f"const float Tile = {lib.num(sb.TILE)}f;", f"const float Fill = {sb.FILL}f;",
               f"const float Thickness = {lib.num(sb.THICKNESS)}f;", f"const float Gap = {lib.num(sb.GAP)}f;",
               f"const int MaxAttempts = {sb.MAX_ATTEMPTS};",
               f"const float CrystalOffset = {lib.num(sb.CRYSTAL_OFFSET)}f;",
               f"const float CrystalStep = {lib.num(sb.CRYSTAL_STEP)}f;",
               f"int key = s.Seed * {sb.KEY_SEED} + k * {sb.KEY_STRUCTURE} + t * {sb.KEY_ATTEMPT};",
               "if (Vector3.Dot(n, c) < 0f) n = -n;",
               "!rim && s.RibEvery > 0 && (a % s.RibEvery == 0 || b % s.RibEvery == 0)",
               "if (a % e != 0 && b % e != 0) continue;",
               "Mathf.Abs(n.x) < 0.9f ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 1f, 0f)"):
    if needle not in plates_cs:
        errors.append(f"ShatterplanePlates no longer declares '{needle}' - update shatterplane_budget.py")
spawnable_cs = g.read(SCRIPT_PATHS["SpawnableShatterplane"])
for field in ("innerRadius", "outerRadius", "structureCount", "plateRadius", "platesPerStack", "shelfGap", "faceOut",
              "ribEvery", "dangerRim", "screenEvery", "screenRadius", "screenOffset", "screenRibEvery", "crystalCount"):
    if not re.search(rf"\] (?:float|int) {field} = ", spawnable_cs):
        errors.append(f"SpawnableShatterplane no longer serializes '{field}' - the prefabs write it")
for r in RUNGS:
    c = CENSUS[r["i"]]
    want = f"[TestCase({r['i']}, {c['total']}, {c['danger']}, {c['super']})]"
    if want not in tests_cs:
        errors.append(f"ShatterplaneTests does not pin I{r['i']}'s census {want} - the C# and the twin may have drifted")

# I4's fauna are ELEMENTAL and live: each species spreads its creatures over the four elements, and only I4 carries them.
for name, path in FAUNA_SPECIES.items():
    text = g.read(path)
    if "  SpreadElements: 1\n" not in text:
        errors.append(f"{name}: {path} no longer spreads its creatures over the elements - I4's fauna would be one element")
    palette = re.search(r"  ElementPalette:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    if not palette or palette.group(1).count("guid:") != 4:
        errors.append(f"{name}: {path}'s ElementPalette is not the four elements")
if FAUNA_INTENSITIES != (4,):
    errors.append("live elemental fauna belong to I4 alone (Shombith, 2026-11-10)")
if FIELD_ELEMENT_BY_INTENSITY[2] != ELEMENT_SPACE or not FIELD_PER_TEAM_BY_INTENSITY[2]:
    errors.append("I3's crystals are Space, one per team present (Shombith, 2026-11-10)")
if any(FIELD_PER_TEAM_BY_INTENSITY[k] for k in (0, 1, 3)):
    errors.append("the per-team crystal field is I3's alone")
if TEAM_CRYSTAL_SPACING * 2 >= sb.CRYSTAL_STEP:
    errors.append("three team crystals side by side would reach the next crystal round on the axis")
impactor_cs = g.read("Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs")
if "internal void RestrictTo(Domains domain)" not in impactor_cs or \
        "skimmerImpactor.Skimmer.VesselStatus.Domain != _onlyFor.Value) return;" not in impactor_cs:
    errors.append("ElementalCrystalImpactor's RestrictTo gate (Harvest's) no longer refuses another team's skimmer")
if "impactor.RestrictTo(slot.Owner);" not in controller or "slot.Owner != team" not in controller:
    errors.append("ShatterplaneController no longer mints team crystals or keeps its AI off other teams' crystals")

# The geometry the race promises: every structure placed with a gap, clear of the spawn ring, crystals on the axes.
for r in RUNGS:
    c = CENSUS[r["i"]]
    if c["missed"]:
        errors.append(f"I{r['i']}: {c['missed']} structures found no room - widen the field or lay fewer")
    if c["reach"] > SPAWN_RING - sb.SPAWN_CLEAR:
        errors.append(f"I{r['i']}: structures reach {c['reach']:.0f} u, inside the {sb.SPAWN_CLEAR:.0f} u clear zone "
                      f"of the spawn ring {SPAWN_RING}")
    structs, _ = sb.layout(r)
    # A float32 boundary can flip a placement between the twin and the C#: keep every accepted gap clear by a margin.
    for a in range(len(structs)):
        for b in range(a + 1, len(structs)):
            d = float(sum((structs[a]["c"][k] - structs[b]["c"][k]) ** 2 for k in range(3)) ** 0.5)
            margin = d - float(structs[a]["r"] + structs[b]["r"]) - sb.GAP
            if margin < 0.05:
                errors.append(f"I{r['i']}: structures {a} and {b} clear the gap by only {margin:.3f} u")
    sites = sb.crystal_sites(structs, r, r["crystalCount"])
    if len(sites) != r["crystalCount"]:
        errors.append(f"I{r['i']}: {len(sites)} crystal sites for {r['crystalCount']} wanted")
    for st in sites:
        d = sum(x * x for x in st) ** 0.5
        if d > SPAWN_RING - 100:
            errors.append(f"I{r['i']}: a crystal site {d:.0f} u out sits within 100 u of the spawn ring")
    if len({tuple(round(x, 1) for x in st) for st in sites}) != len(sites):
        errors.append(f"I{r['i']}: two crystal sites coincide")
if not (RUNGS[0]["faceOut"] and not any(r["faceOut"] for r in RUNGS[1:])):
    errors.append("only I1's plates face straight out")
if not (RUNGS[2]["platesPerStack"] > 1 and RUNGS[2]["shelfGap"] >= 60):
    errors.append("I3 stacks its plates at least 60 u apart (closer reads as one blob)")
if not (RUNGS[3]["ribEvery"] >= 6 and RUNGS[3]["dangerRim"] and RUNGS[3]["screenEvery"]):
    errors.append("I4 has ribs on every sixth line or sparser, danger rims and screens")
if any(CENSUS[i]["danger"] or CENSUS[i]["super"] for i in (1, 2, 3)):
    errors.append("danger and super-shielded mass belong to I4 only")
counts = [CENSUS[i]["total"] for i in (1, 2, 3, 4)]
if max(counts) > 12000:
    errors.append(f"a rung lays {max(counts)} prisms - keep the arena under Flatline's envelope")
if not (SPAWN_RING + 400 <= MEMBRANE_RADIUS):
    errors.append("the spawn ring is too close to the membrane")
if NUCLEUS_RADIUS + 40 >= SPAWN_RING:
    errors.append("the nucleus would push the spawn ring out past its floor")

# The scene: every new identity present, every donor one gone, four Brittle Star AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("ShatterplaneController", "ShatterplanePrismTurnMonitor")},
                     "ShatterplaneSettings": G_ASSET["ShatterplaneSettings"],
                     "ShatterplaneScoringRule": G_ASSET["ShatterplaneScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Flatline cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {BRITTLESTAR_ID}\n") != 4:
        errors.append("scene does not carry exactly 4 Brittle Star AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Shatterplane"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/SHATTERPLANE.md")])
