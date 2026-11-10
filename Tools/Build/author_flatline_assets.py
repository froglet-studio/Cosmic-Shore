#!/usr/bin/env python3
"""
Authors every serialized asset the Flatline game mode needs (GameModes.Flatline = 84).

Flatline is the Brittle Star's CHARGE game (Shombith, 2026-11-09: "Go with Flatline, build all four intensities ...
only the bullets and chords should score points"; 2026-11-10: "These shapes should be scattered or open designed where
we can go inside ... make the score to win at 90"). An OPEN field of thin-paned structures is scattered through the
cell: loose panes, tunnels to fly through and windowed cages to fly into. A pane stops a round, the cords cut straight
through it, and a ring thrown face-on into it bursts flat along it. Only hull-on-hull hits score, at the
fleet price (CombatHitDrain.PointsFor, ten points to the petal): a ROUND on a rival hull is 1 (Bullet), a CORD or a
thrown saber across one is 8 (Strike). The ring blast scores NOTHING (it still bursts, breaks panes and takes petals:
the mode never changes the vessel). First DOMAIN to the per-intensity target wins, golf-timed: 90 points at every intensity.

THE LADDER (one SpawnableFlatline prefab and one cell config per intensity; flatline_budget.py is the census):
  I1 Scattered Panes - twenty-four loose panes scattered through open space, crystals in front of panes.
  I2 Tunnels         - twenty panes and five open-ended tunnels to fly through, a crystal inside every tunnel.
  I3 Cages           - four windowed cages to fly into, four tunnels, eighteen panes, danger rims on every third pane.
  I4 Maze            - six cages, four tunnels and twenty-two holed panes, danger on every other pane and every
                       third room, five crystals deep inside the rooms.
No two structures touch: each stands at least FlatlinePanes.Gap clear of every other, so there is always a way round.
Every wall's every Nth interior grid line is a super-shielded RIB, so a wall the match wears down stays a lattice.

NOTHING ABOUT THE BRITTLE STAR CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the arena,
the scoring and the AI (a hunter that steers; the hull's own autopilot keeps the triggers).

The scene was cloned from Serpent Gulag's (the CombatPoints duel whose controller declares settings / rule / arenaCell),
which Hood (#1865) rebuilt in place under its old guids; the committed scene stands, so the clone stands down.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_flatline_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import flatline_budget as fb  # noqa: E402

MODE_ID = 84
g = lib.Generator(MODE_ID, "Flatline")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
MG = "Assets/_Scripts/Controller/Environment/MiniGameObjects"
SCRIPT_PATHS = {
    "FlatlineController":       f"{AR}/Flatline/FlatlineController.cs",
    "FlatlineSettingsSO":       f"{AR}/Flatline/FlatlineSettingsSO.cs",
    "FlatlineScoringRuleSO":    f"{AR}/Flatline/FlatlineScoringRuleSO.cs",
    "FlatlineHunter":           f"{AR}/Flatline/FlatlineHunter.cs",
    "FlatlinePointTurnMonitor": f"{AR}/TurnMonitors/FlatlinePointTurnMonitor.cs",
    "SpawnableFlatline":        f"{MG}/SpawnableFlatline.cs",
    "FlatlinePanes":            f"{MG}/FlatlinePanes.cs",
    "FlatlineTests":            "Assets/_Scripts/Tests/Editor/FlatlineTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Flatline Cell"
G_ASSET = {
    "ArcadeGameFlatline":      guid("asset/ArcadeGameFlatline"),
    "FlatlineScoringRule":     guid("asset/FlatlineScoringRule"),
    "FlatlineSettings":        guid("asset/FlatlineSettings"),
    "GameToastConfigFlatline": guid("asset/GameToastConfigFlatline"),
    "ModePreviewFlatline":     guid("asset/ModePreviewFlatline"),
    "MinigameFlatline.unity":  guid("asset/MinigameFlatline.unity"),
    "FlatlineSpawnProfile":    guid("asset/FlatlineSpawnProfile"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"FlatlineCellConfig{i}"] = guid(f"asset/FlatlineCellConfig{i}")
    G_ASSET[f"prefab/SpawnableFlatline{i}"] = guid(f"asset/SpawnableFlatline{i}.prefab")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
VESSEL_BRITTLESTAR = lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_BrittleStar.asset")
EXISTING["Vessel_BrittleStar"] = VESSEL_BRITTLESTAR
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"           # SpawnablePrism.prefab (window 0.5-100)
EXISTING["Membrane_prefab"] = lib.CELL_VISUALS["MembranePrefab"]         # the standard shell, radius 1200
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
BRITTLESTAR_CONFIG = "Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset"
BULLET_EFFECT = "Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByBullet.asset"
FULLAUTO_CONTAINER = "Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/SparrowFullAutoProjectileImpactContainer.asset"
SPARROW_ROUND = "Assets/_Prefabs/Projectile/SparrowProjectile.prefab"
BRITTLESTAR_PREFAB = "Assets/_Prefabs/Spacevessels/BrittleStar.prefab"
EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarExecutor.cs"
FLEET_PRICE = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/CombatHitDrain.cs"

# The donor scene's identity, minted by author_serpent_gulag_assets.py with the same guid function.
# Hood (GameModes 77, was Serpent Gulag) kept every guid's Serpent Gulag seed when it renamed the mode (#1865).
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameSerpentHood.unity"
DONOR = {
    "SerpentGulagController":       guid("script/SerpentGulagController"),
    "SerpentGulagSnipeTurnMonitor": guid("script/SerpentGulagSnipeTurnMonitor"),
    "SerpentGulagSettings":         guid("asset/SerpentGulagSettings"),
    "SerpentGulagScoringRule":      guid("asset/SerpentGulagScoringRule"),
}
DONOR_CELLS = [guid(f"asset/SerpentGulagCellConfig{i}") for i in (1, 2, 3, 4)]
SERPENT_ID = lib.VESSEL_CLASS_ID["Serpent"]
BRITTLESTAR_ID = lib.VESSEL_CLASS_ID["BrittleStar"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 8400000000000201, 8400000000000202, 8400000000000203

# ── The duel ─────────────────────────────────────────────────────────────────
ROUND_POINTS = 1          # CombatHitDrain.PointsFor(Bullet)
STRIKE_POINTS = 8         # CombatHitDrain.PointsFor(Strike)
BULLET_CLASS, STRIKE_CLASS = 0, 5
# The end target: 90 at every intensity (Shombith, 2026-11-10: "make the score to win at 90"). The per-intensity list
# stays (Cleave's shape) so a rung can be re-tuned on its own once timed matches say so.
TARGET_BY_INTENSITY = [90, 90, 90, 90]
TARGET_SCALAR = 90        # the fallback for a rung that authors nothing; kept in sync with DefaultFlatlinePointTarget
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the target behind (22.5 points,
# about three cord strikes) buys 1.125 element levels.
COMEBACK_RATE = 0.05

# ── The arena (the standard 1,200 u shell at every rung) ─────────────────────
MEMBRANE_RADIUS = lib.MEMBRANE_RADIUS
CYTOPLASM_SHARD_DISTANCE = round(120 * MEMBRANE_RADIUS / 1200)
SPAWN_RING = 680          # outside every structure (I1-I4 reach 549 at most), facing the centre
OMNI_RADIUS = 200         # the cell's own omni crystal spawns inside this (noNucleusSpawnRadius)
CORD_REACH_WHIP, CORD_REACH_SABER = 120.0, 80.0

RUNGS = fb.RUNGS
for r in RUNGS:
    r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableFlatline{r['i']}.prefab"

# Cleave's ladder deltas: a cell's phase rides ITS arena's own baseline. No fauna here, so the phase only matters as the
# cell's own state, but it must not start a match in the wrong one (the Serpent Gulag numbers).
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameFlatline.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Flatline")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/FLATLINE.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/FlatlineScoringRule.asset", G_ASSET["FlatlineScoringRule"],
             lib.header_for(G_SCRIPT["FlatlineScoringRuleSO"], "FlatlineScoringRule") +
             f"""  metric: 8
  golfRules: 1
  roundPoints: {ROUND_POINTS}
  strikePoints: {STRIKE_POINTS}
  otherHitPoints: 0
  pointsLeftTemplate: '{{count}} Points Left'
  breakdownTemplate: '{{strikes}} strikes, {{rounds}} rounds'
  victoryHeader: VICTORY
  victoryLabel: FLATLINE TIME
  defeatHeader: DEFEAT
  defeatLabel: POINTS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":    ([1.6, 1.1, 0.7, 0.4], "down"),
    "aiLeadSeconds":        ([0.0, 0.25, 0.5, 0.75], "up"),
    "aiCrystalDetourRange": ([120, 180, 260, 340], "up"),
}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/FlatlineSettings.asset", G_ASSET["FlatlineSettings"],
             lib.header_for(G_SCRIPT["FlatlineSettingsSO"], "FlatlineSettings") +
             """  progressSampleSeconds: 0.5
  fieldRegrowSeconds: 10
  fieldSeed: 84
""" + "".join(ladder_yaml(n) for n in LADDERS) + """  aiHumanFocus: 1.5
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: rounds and cords never score on a team-mate, so a solo lobby scores nothing.
DESCRIPTION = ("Brittle Stars only - a duel through thin panes of prisms. A pane stops your rounds, but your cords "
               "cut straight through it, so nobody is safe behind one. Fly round the loose panes, through the tunnels and "
               "into the cages. Throw your ring face-on into a pane and it "
               "bursts flat along it - get off a pane when a ring comes spinning at you. A round on a rival is one "
               "point and a cord strike is eight; the ring scores nothing. Crystals inside the rooms give you spare "
               "rings. First team to 90 wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameFlatline"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameFlatline") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Flatline
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameFlatline
  Vessels:
  - {{fileID: 11400000, guid: {VESSEL_BRITTLESTAR}, type: 2}}
  MinPlayersAllowed: 2
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Flatline.asset", G_ASSET["GameToastConfigFlatline"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Flatline") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(86, "<b>{0}</b> cut a rival with the cords!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} points", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} points", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(159, "Panes stop your rounds - your cords cut through them. A ring bursts flat along a pane, "
                            "so get off it", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigFlatline"])

# ── 6. The arena: one environment prefab per rung, four cell configs, one spawn profile ──
for r in RUNGS:
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableFlatline{r['i']}"], f"""%YAML 1.1
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
  m_Name: SpawnableFlatline{r['i']}
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
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableFlatline']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {8400 + r['i']}
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
  paneCount: {r['paneCount']}
  paneHalfMin: {r['paneHalfMin']}
  paneHalfMax: {r['paneHalfMax']}
  tunnelCount: {r['tunnelCount']}
  cageCount: {r['cageCount']}
  roomHalf: {r['roomHalf']}
  ribEvery: {r['ribEvery']}
  dangerPaneEvery: {r['dangerPaneEvery']}
  dangerRoomEvery: {r['dangerRoomEvery']}
  holeShare: {lib.num(r['holeShare'])}
  holeCellTiles: {r['holeCellTiles']}
  crystalCount: {r['crystalCount']}
""")

CENSUS = {}
for r in RUNGS:
    i = r["i"]
    c = fb.census(r)
    CENSUS[i] = c
    th = thresholds(c["total"], round(c["volume"]))
    env = f"{{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableFlatline{i}']}, type: 3}}"
    desc = (f"{r['label']} - intensity {i} of Flatline: {r['blurb']}. {c['total']} prisms, all neutral "
            f"({c['super']} super-shielded ribs, {c['danger']} danger). Membrane radius {int(MEMBRANE_RADIUS)}, "
            f"spawn ring {SPAWN_RING}, the crystal field laid by the controller inside the rooms and in front of panes. NO NUCLEUS, no flora, "
            f"no fauna. Authored by Tools/Build/author_flatline_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Flatline Cell Config {i}.asset", G_ASSET[f"FlatlineCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Flatline Cell Config {i}") + f"""  CellName: Flatline
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['Membrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: {CYTOPLASM_SHARD_DISTANCE}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['FlatlineSpawnProfile']}, type: 2}}
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

# One spawn profile, shared by all four configs: it spawns nothing (no flora, no fauna).
g.emit_asset(f"{CELL_DIR}/Flatline Spawn Profile.asset", G_ASSET["FlatlineSpawnProfile"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Flatline Spawn Profile") + """  FloraExcludeLocalDomain: 0
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

CELLS = [G_ASSET[f"FlatlineCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {SPAWN_RING}\n  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 2200\n  spawnFormation: 0\n")
NO_NUCLEUS = f"  noNucleusSpawnRadius: {OMNI_RADIUS}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Flatline.asset", G_ASSET["ModePreviewFlatline"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Flatline") + f"""  Mode: {MODE_ID}
  Notes: 'The Flatline cell at each intensity - scattered panes, then tunnels, cages, and the maze. OPEN-ENDED - it scores on rounds and cord strikes on rival pilots and a solo preview has none;
    what it teaches is the panes, the cords and the ring. Vessel pinned to BrittleStar({BRITTLESTAR_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {BRITTLESTAR_ID}
  ObjectiveText: Cut a rival through a pane with your cords
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewFlatline"])

# ── 8. Scene: Serpent Gulag's, with the identity, the cells, the AI hulls and the spawn ring swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["SerpentGulagSnipeTurnMonitor"], G_SCRIPT["FlatlinePointTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["SerpentGulagController"], G_SCRIPT["FlatlineController"], "controller")
    scene = lib.swap_guid(scene, DONOR["SerpentGulagSettings"], G_ASSET["FlatlineSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["SerpentGulagScoringRule"], G_ASSET["FlatlineScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {SERPENT_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {BRITTLESTAR_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, "  noNucleusSpawnRadius: 380\n", NO_NUCLEUS, "crystal volume")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, NO_NUCLEUS]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameFlatline.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
# The spawn ring is a block this generator OWNS: when the arena is re-laid (2026-11-10, the open scattered field reaches
# 549 u, so the ring moved out from 420), the committed scene's block is re-tuned in place, and nothing else in it.
for old_ring in (420,):
    old = SPAWN.replace(f"spawnRingRadiusFloor: {SPAWN_RING}\n", f"spawnRingRadiusFloor: {old_ring}\n")
    if scene is not None and old != SPAWN and scene.count(old) == 1:
        scene = scene.replace(old, SPAWN)
        scene_errs = [e for e in scene_errs if SPAWN not in e]
errors += scene_errs
g.emit_scene("MinigameFlatline", G_ASSET["MinigameFlatline.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameFlatline"])
g.register_always_unlocked()
g.register_build_scene("MinigameSerpentHood", "MinigameFlatline", G_ASSET["MinigameFlatline.unity"])
g.set_end_condition("flatlinePointTarget", after="serpentHoodSnipeTarget", value=TARGET_SCALAR)

# The per-intensity list rides beside each scalar (Cleave's shape; set_end_condition authors scalars only).
endcond = g.read_current(lib.END_CONDITIONS)
_target_list = "".join(f"  - {t}\n" for t in TARGET_BY_INTENSITY)
for scalar_key, list_key in (("flatlinePointTarget", "flatlinePointTargetByIntensity"),
                             ("flatlinePointTargetBuild", "flatlinePointTargetByIntensityBuild")):
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
if len(TARGET_BY_INTENSITY) != 4 or any(t <= 0 for t in TARGET_BY_INTENSITY):
    errors.append("the target ladder needs four positive rungs")

# The C# defaults match the authored ones.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultFlatlinePointTarget = {TARGET_SCALAR};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultFlatlinePointTarget differs from TARGET_SCALAR")
_init = "new() { " + ", ".join(str(t) for t in TARGET_BY_INTENSITY) + " }"
if eco.count(f"flatlinePointTargetByIntensity = {_init};") != 1 or \
        eco.count(f"flatlinePointTargetByIntensityBuild = {_init};") != 1:
    errors.append(f"EndConditionOverridesSO's flatline per-intensity initializers are not {_init}")

# The load-bearing assumptions: the prices are the fleet's, the round reports a Bullet, the cords a Strike.
fleet = g.read(FLEET_PRICE)
for cls, pts in (("Bullet", ROUND_POINTS), ("Strike", STRIKE_POINTS)):
    if not re.search(rf"CombatHitClass\.{cls}\s+=> {pts},", fleet):
        errors.append(f"CombatHitDrain's fleet price for {cls} is no longer {pts} - re-price the rule and the targets")
rule_cs = g.read(SCRIPT_PATHS["FlatlineScoringRuleSO"])
for needle in (f"int roundPoints = {ROUND_POINTS};", f"int strikePoints = {STRIKE_POINTS};", "int otherHitPoints = 0;"):
    if needle not in rule_cs:
        errors.append(f"FlatlineScoringRuleSO's C# default is not '{needle}'")
if f"  hitClass: {BULLET_CLASS}\n" not in g.read(BULLET_EFFECT):
    errors.append("VesselCombatHitByBullet no longer reports a Bullet - the Brittle Star's rounds would not pay")
if lib.existing_guid(BULLET_EFFECT) not in g.read(FULLAUTO_CONTAINER):
    errors.append("the Sparrow full-auto container no longer carries VesselCombatHitByBullet")
_round_guid = lib.existing_guid(SPARROW_ROUND)
if _round_guid not in g.read(BRITTLESTAR_PREFAB):
    errors.append("the Brittle Star no longer fires SparrowProjectile - re-check what its rounds report")
if lib.existing_guid(FULLAUTO_CONTAINER) not in g.read(SPARROW_ROUND):
    errors.append("SparrowProjectile no longer carries the full-auto impact container")
executor = g.read(EXECUTOR)
if "const CombatHitClass hitClass = CombatHitClass.Strike;" not in executor:
    errors.append("the Brittle Star's cords no longer report a Strike - re-check FlatlineScoringRuleSO")
bs_config = g.read(BRITTLESTAR_CONFIG)
if f"  cordLength: {int(CORD_REACH_SABER)}\n" not in bs_config:
    errors.append(f"the Brittle Star's cordLength moved off {CORD_REACH_SABER} - re-check the pane spacing")

# The ladders: monotone toward harder; the C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["FlatlineSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
hunter = g.read(SCRIPT_PATHS["FlatlineHunter"])
if "SelectedIntensity" in hunter or "AIDifficultyFor(" not in hunter:
    errors.append("FlatlineHunter must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
if "PerformShipControllerActions" in hunter:
    errors.append("FlatlineHunter presses a control - the Brittle Star's own autopilot owns its triggers")

# The C# twin of this census stays the C#: its constants and rules must still be the ones counted here.
panes_cs = g.read(SCRIPT_PATHS["FlatlinePanes"])
for needle in (f"const float Tile = {lib.num(fb.TILE)}f;", f"const float Thickness = {lib.num(fb.THICKNESS)}f;",
               f"const float Flush = {fb.FLUSH}f;", f"const float Gap = {lib.num(fb.GAP)}f;",
               f"const int MaxAttempts = {fb.MAX_ATTEMPTS};", f"const float CrystalOffset = {lib.num(fb.CRYSTAL_OFFSET)}f;",
               f"const int WindowHalf = {fb.WINDOW_HALF};", f"const int HoleSalt = {fb.HOLE_SALT};",
               f"int key = s.Seed * {fb.KEY_SEED} + k * {fb.KEY_STRUCTURE} + t * {fb.KEY_ATTEMPT};",
               "Hash01(st.Index * 7919 + cx * 131 + cy * 17 + HoleSalt)",
               "i % s.RibEvery == 0 && Mathf.Abs(i) < h || j % s.RibEvery == 0 && Mathf.Abs(j) < h",
               "Mathf.Abs(n.x) < 0.9f ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 1f, 0f)"):
    if needle not in panes_cs:
        errors.append(f"FlatlinePanes no longer declares '{needle}' - update flatline_budget.py")
spawnable_cs = g.read(SCRIPT_PATHS["SpawnableFlatline"])
for field in ("innerRadius", "outerRadius", "paneCount", "paneHalfMin", "paneHalfMax", "tunnelCount", "cageCount",
              "roomHalf", "ribEvery", "dangerPaneEvery", "dangerRoomEvery", "holeShare", "holeCellTiles",
              "crystalCount"):
    if not re.search(rf"\] (?:float|int) {field} = ", spawnable_cs):
        errors.append(f"SpawnableFlatline no longer serializes '{field}' - the prefabs write it")
tests_cs = g.read(SCRIPT_PATHS["FlatlineTests"])
for r in RUNGS:
    c = CENSUS[r["i"]]
    want = f"[TestCase({r['i']}, {c['total']}, {c['danger']}, {c['super']})]"
    if want not in tests_cs:
        errors.append(f"FlatlineTests does not pin I{r['i']}'s census {want} - the C# and the twin may have drifted")

# The geometry the duel promises: an OPEN field (every structure placed, a gap round each), rooms you can fly into,
# crystals inside it and clear of the spawn ring.
for r in RUNGS:
    c = CENSUS[r["i"]]
    if c["missed"]:
        errors.append(f"I{r['i']}: {c['missed']} structures found no room - widen the field or lay fewer")
    if c["reach"] > SPAWN_RING - 100:
        errors.append(f"I{r['i']}: structures reach {c['reach']:.0f} u, within 100 u of the spawn ring {SPAWN_RING}")
    structs, _ = fb.layout(r)
    sites = fb.crystal_sites(structs, r["crystalCount"])
    if len(sites) != r["crystalCount"]:
        errors.append(f"I{r['i']}: {len(sites)} crystal sites for {r['crystalCount']} wanted")
    for st in sites:
        d = sum(x * x for x in st) ** 0.5
        if d > SPAWN_RING - 100:
            errors.append(f"I{r['i']}: a crystal site {d:.0f} u out sits within 100 u of the spawn ring")
    if len({tuple(round(x, 1) for x in st) for st in sites}) != len(sites):
        errors.append(f"I{r['i']}: two crystal sites coincide")
    if r["paneHalfMax"] * 2 * fb.TILE < CORD_REACH_SABER * 0.5:
        errors.append(f"I{r['i']}: the largest pane is too small to hide behind")
    # A room's opening (the inside of a tunnel, a cage's window) must take a hull: 2 x (half - 1) tiles of clear bore.
    if (r["tunnelCount"] or r["cageCount"]) and (2 * fb.WINDOW_HALF + 1) * fb.TILE < 25:
        errors.append(f"I{r['i']}: a cage window is too narrow to fly through")
if fb.GAP < 30:
    errors.append(f"the gap between structures ({fb.GAP} u) is too tight to fly between")
if CENSUS[1]["structures"] and any(s["kind"] != fb.PANE for s in fb.layout(RUNGS[0])[0]):
    errors.append("I1 is loose panes only")
if not (RUNGS[1]["tunnelCount"] and RUNGS[2]["cageCount"] and RUNGS[3]["cageCount"]):
    errors.append("the ladder adds tunnels at I2 and cages at I3")
counts = [CENSUS[i]["total"] for i in (1, 2, 3, 4)]
if max(counts) > 12000:
    errors.append(f"a rung lays {max(counts)} prisms - keep the duel arena light")
if CENSUS[1]["danger"] or CENSUS[2]["danger"] or not (CENSUS[3]["danger"] and CENSUS[4]["danger"]):
    errors.append("danger belongs to I3 and I4 only (the rims)")
if any(CENSUS[i]["super"] == 0 for i in (1, 2, 3, 4)):
    errors.append("every rung needs its super-shielded ribs, or the panes wear to nothing")
if not (SPAWN_RING + 400 < MEMBRANE_RADIUS):
    errors.append("the spawn ring is too close to the membrane")

# The scene: every new identity present, every donor one gone, four Brittle Star AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("FlatlineController", "FlatlinePointTurnMonitor")},
                     "FlatlineSettings": G_ASSET["FlatlineSettings"],
                     "FlatlineScoringRule": G_ASSET["FlatlineScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Gulag cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {BRITTLESTAR_ID}\n") != 4 or f"  - vesselClass: {SERPENT_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Brittle Star AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Flatline"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/FLATLINE.md")])
