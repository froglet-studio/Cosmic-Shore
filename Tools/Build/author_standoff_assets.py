#!/usr/bin/env python3
"""
Authors every serialized asset the Standoff game mode needs (GameModes.Standoff = 125).

Standoff is the Grizzly's CHARGE game (Garrett, 2026-11-15: "make all the missing games you can"; row 35 of
Docs/ElementalLoop/GAME_KIT_ROSTER.md, which reserved GameModes 125 and hint toast 200 for it so the roster's
parallel sessions never collide). Square POSTS of prism tiles stand on a sphere round an open centre. A trigger bomb
that touches a rival hull freezes on it (GRIZZLY_TRIGGER_BOMBS.md); the next pull and release blows it. Every rival a
blast of yours catches - already a MissileBlast-class combat hit (VesselCombatHitByGrizzlyBlast) - is one point. Dig
In behind a post to aim and recharge. The first DOMAIN to the target wins.

THE LADDER (one SpawnableStandoff prefab and cell config per intensity; numbers in standoff_budget.RUNGS):
  I1 Open Ground    - eight posts round an open centre, three omni crystals.
  I2 Earthworks     - twelve wider posts, two omni crystals.
  I3 Redoubts       - fourteen posts with shielded rims and an inner ring of four; two omni crystals.
  I4 Killing Field  - eighteen big posts and an inner six, shielded rims and danger corners; one omni crystal.
The posts' census, volume and PhaseThresholds come from standoff_budget.py, an exact mirror of the C#.

NOTHING ABOUT THE GRIZZLY CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the posts, the
omni crystals at the centre (the scene's CrystalManager) and the AI gunners, which only steer: the hull's own
autopilot bomb-jump is the AI's weapon. The scene is cloned from Corona's (the newest CombatPoints duel, whose
controller declares settings / rule / arenaCell as this one does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_standoff_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import standoff_budget as budget  # noqa: E402

MODE_ID = 125             # reserved by Docs/ElementalLoop/GAME_KIT_ROSTER.md row 35 - never "the next free id"
TOAST_HINT = 200          # reserved by the same row
g = lib.Generator(MODE_ID, "Standoff")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "StandoffController":       f"{AR}/Standoff/StandoffController.cs",
    "StandoffSettingsSO":       f"{AR}/Standoff/StandoffSettingsSO.cs",
    "StandoffScoringRuleSO":    f"{AR}/Standoff/StandoffScoringRuleSO.cs",
    "StandoffGunner":           f"{AR}/Standoff/StandoffGunner.cs",
    "StandoffBlastTurnMonitor": f"{AR}/TurnMonitors/StandoffBlastTurnMonitor.cs",
    "SpawnableStandoff":        "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableStandoff.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Standoff Cell"
G_ASSET = {
    "ArcadeGameStandoff":      guid("asset/ArcadeGameStandoff"),
    "StandoffScoringRule":     guid("asset/StandoffScoringRule"),
    "StandoffSettings":        guid("asset/StandoffSettings"),
    "GameToastConfigStandoff": guid("asset/GameToastConfigStandoff"),
    "ModePreviewStandoff":     guid("asset/ModePreviewStandoff"),
    "MinigameStandoff.unity":  guid("asset/MinigameStandoff.unity"),
    "StandoffSpawnProfile":    guid("asset/StandoffSpawnProfile"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"StandoffCellConfig{i}"] = guid(f"asset/StandoffCellConfig{i}")
    G_ASSET[f"prefab/SpawnableStandoff{i}"] = guid(f"asset/SpawnableStandoff{i}.prefab")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Grizzly"] = lib.VESSELS["Grizzly"]                      # SO_Class_Grizzly.asset
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"            # SpawnablePrism.prefab (window 0.5-100)
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"   # the standard 1,200 u cell (the Boneyard's)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
# The blast: what makes a Grizzly's blast a scored, petal-taking hit (read to prove the rule pays something real).
BLAST_CONTAINER = ("Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/"
                   "GrizzlyExplosionImpactorDataContainer.asset")
BLAST_PREFAB = "Assets/_Prefabs/Projectile/AOEGrizzlyExplosion.prefab"
COMBAT_HIT_EFFECT = "Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByGrizzlyBlast.asset"
COMBAT_HIT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"                    # Event_CombatHitStats.asset
BOMB_CONFIG = "Assets/_SO_Assets/VesselActions/Grizzly/GrizzlyTriggerBombConfig.asset"
CANNON = "Assets/_SO_Assets/VesselActions/Grizzly/GrizzlyChargedShotAction.asset"
BOMB_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GrizzlyTriggerBombExecutor.cs"
# The donor scene's identity, minted by author_corona_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameCorona.unity"
DONOR = {
    "CoronaController":       guid("script/CoronaController"),
    "CoronaBloomTurnMonitor": guid("script/CoronaBloomTurnMonitor"),
    "CoronaSettings":         guid("asset/CoronaSettings"),
    "CoronaScoringRule":      guid("asset/CoronaScoringRule"),
}
DONOR_CELLS = [guid(f"asset/CoronaCellConfig{i}") for i in (1, 2, 3, 4)]
MANTA_ID = lib.VESSEL_CLASS_ID["Manta"]
GRIZZLY_ID = lib.VESSEL_CLASS_ID["Grizzly"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 12500000000000201, 12500000000000202, 12500000000000203

# ── The race ─────────────────────────────────────────────────────────────────
BLAST_TARGET = 10         # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultStandoffBlastTarget
BLAST_POINTS = 1
MISSILE_BLAST_CLASS = 3   # CombatHitClass.MissileBlast - what VesselCombatHitByGrizzlyBlast reports
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (2.5 blasts) buys
# 1.0 element level; on the Grizzly, Charge (Dig In's Energy regen), Space (the cannon's blast size) and Time (Rush)
# all move a number in this fight.
COMEBACK_RATE = 0.4

MEMBRANE_RADIUS = 1200.0
OMNI_BALL = 80            # the omni crystals spawn in a ball this wide at the open centre (CrystalManager.noNucleusSpawnRadius)
FULL_BLAST_RADIUS = 100   # a full trigger bomb's blast radius (GrizzlyTriggerBombConfig maxBlastScale 200 / 2)

RUNGS = budget.RUNGS
for r in RUNGS:
    r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableStandoff{r['i']}.prefab"
    r["laid"] = budget.build(r)

# Cleave / Serpent Gulag / Boneyard / Jackstraw / Kelpwood / Corona ladder deltas: a cell's phase rides ITS baseline.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameStandoff.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Standoff")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/STANDOFF.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/StandoffScoringRule.asset", G_ASSET["StandoffScoringRule"],
             lib.header_for(G_SCRIPT["StandoffScoringRuleSO"], "StandoffScoringRule") +
             f"""  metric: 8
  golfRules: 1
  blastPoints: {BLAST_POINTS}
  otherHitPoints: 0
  blastsLeftTemplate: '{{count}} Blasts Left'
  blastsLandedTemplate: '{{count}} blasts'
  victoryHeader: VICTORY
  victoryLabel: STANDOFF TIME
  defeatHeader: DEFEAT
  defeatLabel: BLASTS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":            ([1.4, 1, 0.7, 0.5], "down"),
    "aiHuntRange":                  ([300, 420, 560, 720], "up"),
    "aiLeadSeconds":                ([0, 0.2, 0.4, 0.6], "up"),
    "aiPassOffset":                 ([70, 55, 40, 28], "down"),
    "aiBreakOffWhenSilencedChance": ([0, 0.5, 1, 1], "up"),
    "aiSilencedPreyFocus":          ([1, 1, 1.5, 2], "up"),
}
SHARED = {"aiHumanFocus": 2, "aiCoverStandoff": 30, "aiPostReachedDistance": 60}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/StandoffSettings.asset", G_ASSET["StandoffSettings"],
             lib.header_for(G_SCRIPT["StandoffSettingsSO"], "StandoffSettings")
             + "".join(ladder_yaml(n) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a blast never scores on a team-mate, so a solo lobby has nobody to catch.
# No colons in the description: a plain YAML scalar would start a new key at one (the Serpentine trap).
DESCRIPTION = ("Grizzlies only - square posts of prisms stand round an open centre, cover to dig in behind. Fire a "
               "trigger bomb at a rival - it flies through the posts, and when it touches their hull it sticks. Pull "
               "and release again to blow it. Every rival a blast of yours catches is a point that silences them. Dig "
               "in behind a post to aim and recharge, and charge the cannon for a bigger blast. Higher intensities add "
               "posts, an inner ring, shielded rims and danger corners. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameStandoff"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameStandoff") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Standoff
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameStandoff
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Grizzly']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 9
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Standoff.asset", G_ASSET["GameToastConfigStandoff"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Standoff") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(81, "<b>{0}</b> caught a rival in a blast!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} blasts", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} blasts", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "A trigger bomb that touches a rival sticks - pull and release again to blow it. "
                       "Dig in behind a post to aim and recharge", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigStandoff"])

# ── 6. The arena: one environment prefab and cell config per rung, one empty spawn profile ──
for r in RUNGS:
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableStandoff{r['i']}"], f"""%YAML 1.1
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
  m_Name: SpawnableStandoff{r['i']}
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
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableStandoff']}, type: 3}}
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
  postCount: {r['postCount']}
  postRadius: {r['postRadius']}
  postGrid: {r['postGrid']}
  innerPostCount: {r['innerPostCount']}
  innerPostRadius: {r['innerPostRadius']}
  innerPostGrid: {r['innerPostGrid']}
  shieldedRim: {r['shieldedRim']}
  dangerCorners: {r['dangerCorners']}
""")

# One spawn profile, shared by all four configs: it spawns nothing (no flora, no fauna). The fight is pilot on pilot;
# a creature in a blast would only soak it.
g.emit_asset(f"{CELL_DIR}/Standoff Spawn Profile.asset", G_ASSET["StandoffSpawnProfile"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Standoff Spawn Profile") + """  FloraExcludeLocalDomain: 0
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

for r in RUNGS:
    i = r["i"]
    c = r["laid"]["census"]
    th = thresholds(c["prisms"], round(c["volume"]))
    env = f"{{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableStandoff{i}']}, type: 3}}"
    desc = (f"{r['label']} - intensity {i} of Standoff: {r['blurb']}. {c['prisms']} prisms in {c['posts']} posts, "
            f"all neutral ({c['shielded']} shielded, {c['danger']} danger), outer posts at {r['postRadius']} u inside "
            f"the standard {int(MEMBRANE_RADIUS)} u cell, spawn ring {r['spawnRing']}. {r['omni']} omni crystals "
            f"float at the open centre (the scene's CrystalManager). NO NUCLEUS, no flora, no fauna. Authored by "
            f"Tools/Build/author_standoff_assets.py from standoff_budget.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Standoff Cell Config {i}.asset", G_ASSET[f"StandoffCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Standoff Cell Config {i}") + f"""  CellName: Standoff
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['StandoffSpawnProfile']}, type: 2}}
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

CELLS = [G_ASSET[f"StandoffCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


def spawn_block(rings):
    return ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
            f"  spawnRingRadiusFloor: {rings[0]}\n  spawnRingRadiusFloorByIntensity:\n"
            + "".join(f"  - {v}\n" for v in rings) + "  spawnFormation: 0\n")


def omni_block(ball, counts):
    return (f"  noNucleusSpawnRadius: {ball}\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
            "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
            + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {n}\n" for n in counts))


RINGS = [r["spawnRing"] for r in RUNGS]
SPAWN = spawn_block(RINGS)
OMNI = omni_block(OMNI_BALL, [r["omni"] for r in RUNGS])

# Corona's own blocks, read from its generator's source of truth rather than retyped.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corona_budget  # noqa: E402
DONOR_SPAWN = spawn_block([r["spawnRing"] for r in corona_budget.RUNGS])
DONOR_OMNI = omni_block(corona_budget.HEART_RADIUS, [r["omni"] for r in corona_budget.RUNGS])

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Standoff.asset", G_ASSET["ModePreviewStandoff"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Standoff") + f"""  Mode: {MODE_ID}
  Notes: 'The posts at each intensity - more and wider posts, an inner ring, then shielded rims and danger
    corners, and fewer crystals at the centre. OPEN-ENDED - it scores on blasts catching rival pilots and a
    solo preview has none; what it teaches is the posts to dig in behind. Vessel pinned to Grizzly({GRIZZLY_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {GRIZZLY_ID}
  ObjectiveText: Stick a trigger bomb on a rival pilot, then blow it
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {RINGS[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in RINGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewStandoff"])

# ── 8. Scene: Corona's, with the identity, the cells, the AI hulls, the spawn ring and the crystals swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["CoronaBloomTurnMonitor"], G_SCRIPT["StandoffBlastTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["CoronaController"], G_SCRIPT["StandoffController"], "controller")
    scene = lib.swap_guid(scene, DONOR["CoronaSettings"], G_ASSET["StandoffSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["CoronaScoringRule"], G_ASSET["StandoffScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {MANTA_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {GRIZZLY_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "omni crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, OMNI]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameStandoff.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameStandoff", G_ASSET["MinigameStandoff.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameStandoff"])
g.register_always_unlocked()
g.register_build_scene("MinigameCorona", "MinigameStandoff", G_ASSET["MinigameStandoff.unity"])
g.set_end_condition("standoffBlastTarget", after="coronaBloomTarget", value=BLAST_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * BLAST_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {BLAST_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * BLAST_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")


def meta_guid(rel):
    return g.read(rel + ".meta").split("guid: ")[1].split()[0]


# The load-bearing assumptions about the Grizzly, read from the shipped kit (never edited here):
# a blast is a scored MissileBlast hit on the game's combat channel...
container = g.read(BLAST_CONTAINER)
if meta_guid(COMBAT_HIT_EFFECT) not in container:
    errors.append("GrizzlyExplosionImpactorDataContainer no longer carries VesselCombatHitByGrizzlyBlast - a blast "
                  "would take no petals and score nothing")
if meta_guid(BLAST_CONTAINER) not in g.read(BLAST_PREFAB):
    errors.append("AOEGrizzlyExplosion.prefab no longer uses GrizzlyExplosionImpactorDataContainer")
hit = g.read(COMBAT_HIT_EFFECT)
if (f"hitClass: {MISSILE_BLAST_CLASS}\n" not in hit or
        f"onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_CHANNEL}" not in hit):
    errors.append("VesselCombatHitByGrizzlyBlast is no longer a MissileBlast-class hit on the game's combat-hit channel")
if not g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs").count(f"MissileBlast = {MISSILE_BLAST_CLASS},"):
    errors.append(f"CombatHitClass.MissileBlast is no longer {MISSILE_BLAST_CLASS}")
# ...both the trigger bombs and the cannon blow up as that blast...
blast_guid = meta_guid(BLAST_PREFAB)
for path, what in ((BOMB_CONFIG, "the trigger bombs"), (CANNON, "the charged cannon")):
    if blast_guid not in g.read(path):
        errors.append(f"{what} ({path}) no longer blow up as AOEGrizzlyExplosion - their blasts would not score")
bomb_cfg = g.read(BOMB_CONFIG)
if "  maxBlastScale: 200\n" not in bomb_cfg:
    errors.append(f"a full trigger bomb's blast is no longer scale 200 ({FULL_BLAST_RADIUS} u): re-check the AI's "
                  f"pass offsets, which must sit inside it")
if "  aiFireStickBand: 0\n" in bomb_cfg or "aiFireStickBand:" not in bomb_cfg:
    errors.append("the Grizzly's autopilot bomb-jump is off (aiFireStickBand 0) - an AI Grizzly would never fire, "
                  "and StandoffGunner only steers")
# ...and a bomb that touches a rival hull stops there (the sticky bomb the whole game is about).
executor = g.read(BOMB_EXECUTOR)
for needle in ("s.Shot.VesselStruck += left ? HandleLeftVesselStruck : HandleRightVesselStruck;",
               "void HandleVesselStruck(Slot s, Projectile p, VesselImpactor vessel)",
               "void AutopilotDrive(IInputStatus input)",
               "AffectSelfOverride  = false,"):
    if needle not in executor:
        errors.append(f"GrizzlyTriggerBombExecutor no longer has '{needle}' - re-check how a bomb sticks to a rival "
                      f"and how an AI Grizzly fires")
if f"StandoffHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.StandoffHint is not {TOAST_HINT}")
if f"Standoff = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Standoff is not {MODE_ID}")

# The C# default target matches the authored one.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultStandoffBlastTarget = {BLAST_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultStandoffBlastTarget differs from BLAST_TARGET")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["StandoffSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for field, value in SHARED.items():
    if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
        errors.append(f"StandoffSettingsSO.{field} initializer differs from the authored {value}")
if max(LADDERS["aiPassOffset"][0]) >= FULL_BLAST_RADIUS:
    errors.append(f"an AI pass offset is at or past a full blast's {FULL_BLAST_RADIUS} u reach - its passes would miss")

# The gunner reads the seat's difficulty, never the intensity, and presses NOTHING (the hull's own autopilot bomb-jump
# is its weapon; a second presser would fight it).
gunner = g.read(SCRIPT_PATHS["StandoffGunner"])
if "SelectedIntensity" in gunner or "AIDifficultyFor(" not in gunner:
    errors.append("StandoffGunner must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
for banned in ("PerformShipControllerActions", "InputStatus", "TriggerAnalog", "StopShipControllerActions"):
    if banned in gunner:
        errors.append(f"StandoffGunner uses {banned} - it steers only; the hull's autopilot bomb-jump fires")
if "SetExternalTargetProvider" not in gunner or "PostDirection" not in gunner:
    errors.append("StandoffGunner no longer steers round the arena's posts")

# The posts, as the C# lays them.
spawnable = g.read(SCRIPT_PATHS["SpawnableStandoff"])
for needle in (f"TileWidth = {lib.num(budget.TILE_WIDTH)}f;", f"TileThickness = {lib.num(budget.TILE_THICKNESS)}f;",
               f"TilePitch = {lib.num(budget.TILE_PITCH)}f;",
               f"InnerTwistDegrees = {lib.num(budget.INNER_TWIST_DEGREES)}f;",
               "InnerTwistAxis = new Vector3(" + ", ".join(lib.num(v) + "f" for v in budget.INNER_TWIST_AXIS) + ");",
               f"DefaultSeed => {budget.DEFAULT_SEED};", f"LayCapacity => {budget.LAY_CAPACITY};",
               "Quaternion.AngleAxis(InnerTwistDegrees, InnerTwistAxis.normalized) *",
               "var u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;",
               "var kind = dangerCorners && edgeA && edgeB ? PrismKind.Danger",
               ": shieldedRim && (edgeA || edgeB) ? PrismKind.Shielded",
               "Emit(centre + u * ((a - half) * TilePitch) + v * ((b - half) * TilePitch), rot, size, Domains.Blue, kind);"):
    if needle not in spawnable:
        errors.append(f"SpawnableStandoff no longer declares '{needle}' - update standoff_budget.py with it")
if budget.TILE_PITCH <= budget.TILE_WIDTH:
    errors.append("a post's tiles touch (pitch <= width)")

counts = []
for r in RUNGS:
    c = r["laid"]["census"]
    counts.append(c["prisms"])
    if c["leastPostGap"] <= budget.TILE_DIAGONAL + 20:
        errors.append(f"I{r['i']}: two posts' tiles come {c['leastPostGap']:.1f} u apart - posts must stand clear "
                      f"of each other with room for a hull between")
    if r["innerPostCount"] and r["innerPostRadius"] - 0.5 * (r["innerPostGrid"] * budget.TILE_PITCH) < OMNI_BALL + 20:
        errors.append(f"I{r['i']}: the inner posts crowd the {OMNI_BALL} u crystal ball at the centre")
    if r["innerPostCount"] and r["postRadius"] - r["innerPostRadius"] < 120:
        errors.append(f"I{r['i']}: the inner ring is under 120 u inside the outer one - no room to fly between")
    if not (r["postRadius"] + 80 <= r["spawnRing"] <= MEMBRANE_RADIUS - 300):
        errors.append(f"I{r['i']}: spawn ring {r['spawnRing']} must sit outside the posts ({r['postRadius']}) and "
                      f"well inside the {int(MEMBRANE_RADIUS)} u membrane")
    if c["prisms"] > budget.LAY_CAPACITY:
        errors.append(f"I{r['i']}: {c['prisms']} prisms is over SpawnableStandoff.LayCapacity ({budget.LAY_CAPACITY})")
for name, ladder, direction in (("prisms", counts, "up"),
                                ("posts", [r["postCount"] + r["innerPostCount"] for r in RUNGS], "up"),
                                ("omni crystals", [r["omni"] for r in RUNGS], "down")):
    pairs = list(zip(ladder, ladder[1:]))
    if direction == "up" and any(b < a for a, b in pairs) or direction == "down" and any(b > a for a, b in pairs):
        errors.append(f"the {name} ladder must run {direction} with intensity ({ladder})")
for a, b in zip(counts, counts[1:]):
    if b < a * 1.25:
        errors.append(f"an intensity step adds under 25 % more cover ({a} -> {b}): intensity would not be felt")

# The scene: every new identity present, every donor one gone, four Grizzly AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("StandoffController", "StandoffBlastTurnMonitor")},
                     "StandoffSettings": G_ASSET["StandoffSettings"],
                     "StandoffScoringRule": G_ASSET["StandoffScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Corona cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {GRIZZLY_ID}\n") != 4 or f"  - vesselClass: {MANTA_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Grizzly AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Standoff"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/STANDOFF.md")])
