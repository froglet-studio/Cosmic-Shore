#!/usr/bin/env python3
"""
Authors every serialized asset the Harvest game mode needs (GameModes.Harvest = 88).

Harvest is the Thresher's SPACE game (Docs/MassAndSpaceGames/PLAN.md, THRESHER_SPACE.md), plan A picked by Shombith on
2026-11-10: a field of SHEAVES floating in 0G round a clear centre. A sheaf is a core of blocks with stalks standing
out of it in every direction. The Thresher's chain cuts stalks, and every cut banks energy in the ball; the ball's next
hit on a rival prism spends the bank as one blast. So the loop is: mow a sheaf's rim with the chain, then whip the ball
into its core. Hostile prisms destroyed score (ScoringMetric.PrismsDestroyed, so the card reads Space); the first
DOMAIN to the target wins, golf-timed.

THE LADDER (one SpawnableHarvest prefab and cell config per intensity; numbers in harvest_budget.RUNGS):
  I1 Stubble - 8 big neutral sheaves, dense stalks, 2 omni crystals a pilot.
  I2 Sheaves - 12 sheaves, shielded cores, one in four in a domain colour.
  I3 Stooks  - 16 sheaves, a quarter each Jade, Ruby, Gold, neutral; sparser stalks.
  I4 Granary - 22 small sheaves, every fourth stalk danger, one crystal for the lobby.
The census, volume and PhaseThresholds come from harvest_budget.py, an exact mirror of the C#.

NOTHING ABOUT THE THRESHER CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the sheaves,
the omni crystals in the field's clear centre and the AI reapers (HarvestReaper presses A once to leave Mass Mode, the
hull's own button through the replicated path - Shombith's yes, 2026-11-10; everything else is steering). The scene is
cloned from Jackstraw's (the Thresher's Charge game: four Thresher AI seats in the standard cell, a controller that
declares settings / rule / arenaCell as this one does, and the crystal ladder 2 / 1 / 1-1 / 0+1 per pilot).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_harvest_assets.py [--check]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import harvest_budget as budget  # noqa: E402

MODE_ID = 88
g = lib.Generator(MODE_ID, "Harvest")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "HarvestController":       f"{AR}/Harvest/HarvestController.cs",
    "HarvestSettingsSO":       f"{AR}/Harvest/HarvestSettingsSO.cs",
    "HarvestScoringRuleSO":    f"{AR}/Harvest/HarvestScoringRuleSO.cs",
    "HarvestReaper":           f"{AR}/Harvest/HarvestReaper.cs",
    "HarvestPrismTurnMonitor": f"{AR}/TurnMonitors/HarvestPrismTurnMonitor.cs",
    "SpawnableHarvest":        "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableHarvest.cs",
    "HarvestTests":            "Assets/_Scripts/Tests/Editor/HarvestTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Harvest Cell"
G_ASSET = {
    "ArcadeGameHarvest":      guid("asset/ArcadeGameHarvest"),
    "HarvestScoringRule":     guid("asset/HarvestScoringRule"),
    "HarvestSettings":        guid("asset/HarvestSettings"),
    "GameToastConfigHarvest": guid("asset/GameToastConfigHarvest"),
    "ModePreviewHarvest":     guid("asset/ModePreviewHarvest"),
    "MinigameHarvest.unity":  guid("asset/MinigameHarvest.unity"),
    "HarvestSpawnProfile":    guid("asset/HarvestSpawnProfile"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"HarvestCellConfig{i}"] = guid(f"asset/HarvestCellConfig{i}")
    G_ASSET[f"prefab/SpawnableHarvest{i}"] = guid(f"asset/SpawnableHarvest{i}.prefab")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Thresher"] = lib.VESSELS["Thresher"]
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"           # SpawnablePrism.prefab (window 0.5-100)
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"  # the standard 1,200 u cell (the Boneyard's)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
THRESHER_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ThresherExecutor.cs"
THRESHER_ACTION = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/ThresherActionSO.cs"
EXPLOSION_IMPACTOR = "Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs"
# The donor scene's identity, minted by author_jackstraw_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameJackstraw.unity"
DONOR = {
    "JackstrawController":        guid("script/JackstrawController"),
    "JackstrawStrikeTurnMonitor": guid("script/JackstrawStrikeTurnMonitor"),
    "JackstrawSettings":          guid("asset/JackstrawSettings"),
    "JackstrawScoringRule":       guid("asset/JackstrawScoringRule"),
}
DONOR_CELLS = [guid(f"asset/JackstrawCellConfig{i}") for i in (1, 2, 3, 4)]
THRESHER_ID = lib.VESSEL_CLASS_ID["Thresher"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 8800000000000201, 8800000000000202, 8800000000000203

# ── The race ─────────────────────────────────────────────────────────────────
PRISM_TARGET = 750        # kept in sync with EndConditionOverridesSO.DefaultHarvestPrismTarget
MAX_DOMAINS = 3
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (187.5 prisms) buys
# 2.25 element levels: Wrecking Ball's rate (0.014 against 650) scaled to this target. On this hull a Space level is a
# bigger largest blast.
COMEBACK_RATE = 0.012

MEMBRANE_RADIUS = 1200.0
CHAIN_MAX = 140.0           # the Thresher's chain let out (THRESHER.md §4, maxLen)
BLAST_RADIUS_FULL_SPACE = 180.0   # 45 u diameter x 8 (energyMaxDiameterScale at full Space) / 2 - THRESHER.md §2a
BLAST_RADIUS_REST = 90.0          # x 4 at rest Space
CRYSTAL_RADIUS = 150        # the CrystalManager's noNucleusSpawnRadius: the omni crystals spawn inside the clearing
CORE_PRISM_CAP = 250        # one blast at the rest cap never asks the damage pass for more than a core
# The fuller field (2026-11-10, "its too empty right now"): up from Jackstraw's 3,600 to 5,000. The prism system draws
# ~100k prisms at 4.8 ms; what needs a bound is one blast's reach (CORE_PRISM_CAP) and the colliders, which this holds.
ARENA_PRISM_CAP = 5000

RUNGS = budget.RUNGS
for r in RUNGS:
    r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableHarvest{r['i']}.prefab"
    r["laid"] = budget.build(r)

# Jackstraw's ladder deltas: a cell's phase rides ITS arena's own baseline. No flora here and few creatures (I4 only), so
# the phase only matters as the cell's own state, but it must not start a match in the wrong one.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameHarvest.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Harvest")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/HARVEST.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/HarvestScoringRule.asset", G_ASSET["HarvestScoringRule"],
             lib.header_for(G_SCRIPT["HarvestScoringRuleSO"], "HarvestScoringRule") +
             """  metric: 5
  golfRules: 1
  prismsLeftTemplate: '{count} Prisms Left'
  prismsDestroyedTemplate: '{count} Prisms'
  victoryHeader: VICTORY
  victoryLabel: HARVEST TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiMowSeconds":     ([2, 3, 4, 5], "up"),
    "aiMowStandoff":    ([0, -10, -20, -25], "down"),
    "aiCrystalReach":   ([250, 400, 550, 700], "up"),
    "aiRevisitSeconds": ([8, 12, 16, 20], "up"),
}
SHARED = {"aiMowLeadDegrees": 40, "aiStrikeOvershoot": 120, "aiStrikeSeconds": 2.5, "aiClimbSeconds": 3,
          "aiAxleArrive": 60, "aiAxleReachFactor": 1.4, "aiMassModeCheckSeconds": 1.5}  # the SO's field order
# The team Space crystals (I3 only, Shombith 2026-11-10: "for intensities 3 make elemental crystals of mass just for teams
# present there", corrected the same day: "crystals of space I meant"): per team, per intensity, and how they sit and
# grow back.
TEAM_CRYSTALS_PER_TEAM = [0, 0, 4, 0]
CRYSTAL_FIELDS = {"teamCrystalClearance": 30, "teamCrystalRegrowSeconds": 12, "crystalCheckSeconds": 0.5}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/HarvestSettings.asset", G_ASSET["HarvestSettings"],
             lib.header_for(G_SCRIPT["HarvestSettingsSO"], "HarvestSettings") +
             "  teamCrystalElement: 3\n"
             "  teamCrystalsPerTeamByIntensity:\n" + "".join(f"  - {v}\n" for v in TEAM_CRYSTALS_PER_TEAM) +
             "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in CRYSTAL_FIELDS.items()) +
             "".join(ladder_yaml(n) for n in LADDERS) +
             "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinDomains 2: a race against another team (Wrecking Ball's); a solo pilot gets AI backfill on the other side.
DESCRIPTION = ("Threshers only - a field of sheaves floating in every direction, each a core of blocks bristling "
               "with stalks. Press A to leave Mass Mode and break. Mow the stalks with your chain to charge the "
               "ball, then whip it into the core and the charge goes off as one blast. Only prisms that are not "
               "your colour count. Crystals in the clear centre windmill your red-hot ball round you. Higher "
               "intensities shield the cores, colour the sheaves by team and add danger stalks. First team to the "
               "harvest target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameHarvest"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameHarvest") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Harvest
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameHarvest
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Thresher']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: {MAX_DOMAINS}
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Harvest.asset", G_ASSET["GameToastConfigHarvest"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Harvest") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has harvested {1} prisms ({1}/{3})", tint_domain=1, domain_names=0,
                       every_n=150) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(163, "Press A to break - mow the stalks with your chain, then smash the ball into the core",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigHarvest"])

# ── 6. The arena: one environment prefab per rung, four cell configs, one spawn profile ──
for r in RUNGS:
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableHarvest{r['i']}"], f"""%YAML 1.1
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
  m_Name: SpawnableHarvest{r['i']}
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
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableHarvest']}, type: 3}}
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
  fieldRadius: {r['fieldRadius']}
  centreClearingRadius: {r['centreClearingRadius']}
  sheafCount: {r['sheafCount']}
  coreGap: {r['coreGap']}
  coreRadius: {r['coreRadius']}
  coreBlocks: {r['coreBlocks']}
  shieldedCores: {1 if r['shieldedCores'] else 0}
  stalkCount: {r['stalkCount']}
  stalkPrisms: {r['stalkPrisms']}
  wheelStalks: {1 if r['wheelStalks'] else 0}
  dangerStalkEveryNth: {r['dangerStalkEveryNth']}
  chaffClouds: {r['chaffClouds']}
  chaffCloudRadius: {r['chaffCloudRadius']}
  strawsPerCloud: {r['strawsPerCloud']}
  baleCount: {r['baleCount']}
  baleSize: {r['baleSize']}
  colourPattern: {r['colourPattern']}
""")

# ── 6b. I4's creatures: Harvest's OWN copies of four existing species, with only the name and the numbers changed (so
# no other mode's creature is touched: Serpentine's pattern). Each source has SpreadElements on, so every creature rolls
# one of the four elements, shows it, and drops that element's crystal as its heart (the lifeform->crystal invariant).
# The grazers (tadpole, piranha, brittlestar) eat opposing environment prisms - the sheaves - and the shark hunts them;
# their bodies and hearts are there to take. Seeded by the cell's own spawner and kept by reproduction and starvation:
# no mode-side culling (Docs/ECOSYSTEM.md §6). Live caps sum to 23 (Serpentine I4's roster).
FAUNA_SOURCES = {
    "Tadpole": "Assets/_SO_Assets/Cell Configs/Skim Race Cell/Skim Race Tadpole Fauna Config Data.asset",
    "Brittlestar": "Assets/_SO_Assets/Cell Configs/Skim Race Cell/Skim Race Brittlestar Fauna Config Data.asset",
    "Shark": "Assets/_SO_Assets/Cell Configs/Garland Cell/Garland Shark Fauna Config Data.asset",
    "Piranha": "Assets/_SO_Assets/Cell Configs/Scarab Scramble Cell/Scarab Scramble Piranha Fauna Config Data.asset",
}
# (creature, InitialSpawnCount, PopulationSize, MaxLivePopulation)
FAUNA_I4 = [("Shark", 2, 2, 3), ("Piranha", 6, 6, 10), ("Brittlestar", 2, 2, 4), ("Tadpole", 4, 4, 6)]
FAUNA_CAP = 25


pre_errors = []


def fauna_assets():
    import re
    out = []
    for creature, initial, population, cap in FAUNA_I4:
        src = FAUNA_SOURCES[creature]
        text = g.read(src)
        for key_, value in (("InitialSpawnCount", initial), ("PopulationSize", population), ("MaxLivePopulation", cap)):
            text, n = re.subn(rf"^  {key_}: \d+$", f"  {key_}: {value}", text, flags=re.M)
            if n != 1:
                pre_errors.append(f"{src}: {key_} not found once - the creature copy would carry the source's number")
        name = f"Harvest I4 {creature} Fauna Config Data"
        text = re.sub(r"^  m_Name: .*$", f"  m_Name: {name}", text, count=1, flags=re.M)
        out.append((creature, name, guid(f"asset/{name}"), text, src))
    return out


FAUNA = fauna_assets()
for creature, name, gd, text, src in FAUNA:
    G_ASSET[f"fauna/{creature}"] = gd
    g.emit_asset(f"{CELL_DIR}/{name}.asset", gd, text)
G_ASSET["HarvestSpawnProfileI4"] = guid("asset/HarvestSpawnProfileI4")
g.emit_asset(f"{CELL_DIR}/Harvest I4 Spawn Profile.asset", G_ASSET["HarvestSpawnProfileI4"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Harvest I4 Spawn Profile") + """  FloraExcludeLocalDomain: 1
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras: []
  FaunaExcludeLocalDomain: 1
  InitialFaunaSpawnWaitTime: 5
  InitialFaunaReleaseTier: 0
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 30
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 10
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 1
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 0
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas:
""" + "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for _, _, gd, _, _ in FAUNA))

for r in RUNGS:
    i = r["i"]
    c = r["laid"]["census"]
    th = thresholds(c["prisms"], round(c["volume"]))
    env = f"{{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableHarvest{i}']}, type: 3}}"
    life = (f"creatures from Harvest I4 Spawn Profile ({', '.join(n for n, *_ in FAUNA_I4)}), no flora"
            if i == 4 else "no flora, no fauna")
    desc = (f"{r['label']} - intensity {i} of Harvest: {r['blurb']}. {c['prisms']} prisms - {c['sheaves']} wheel "
            f"sheaves ({c['core']} core, {c['stalk']} stalk), {c['clouds']} chaff clouds ({c['chaff']}), {c['bales']} "
            f"bales ({c['bale']}); {c['shielded']} shielded, {c['danger']} danger; field radius {r['fieldRadius']} inside "
            f"the standard {int(MEMBRANE_RADIUS)} u cell, spawn ring {r['spawnRing']}. The omni crystals spawn in the "
            f"clear centre. NO NUCLEUS, {life}. Authored by Tools/Build/author_harvest_assets.py from harvest_budget.py "
            f"- re-run it rather than hand-editing.")
    profile = G_ASSET["HarvestSpawnProfileI4"] if i == 4 else G_ASSET["HarvestSpawnProfile"]
    g.emit_asset(f"{CELL_DIR}/Harvest Cell Config {i}.asset", G_ASSET[f"HarvestCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Harvest Cell Config {i}") + f"""  CellName: Harvest
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
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

# I1-I3 share a spawn profile that spawns nothing: the race is the field. I4 has its own (below): live elemental creatures
# (Shombith, 2026-11-10: "in i4 it has live elemental faunas").
g.emit_asset(f"{CELL_DIR}/Harvest Spawn Profile.asset", G_ASSET["HarvestSpawnProfile"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Harvest Spawn Profile") + """  FloraExcludeLocalDomain: 0
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

CELLS = [G_ASSET[f"HarvestCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


RINGS = [r["spawnRing"] for r in RUNGS]
SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {RINGS[0]}\n  spawnRingRadiusFloorByIntensity:\n"
         + "".join(f"  - {v}\n" for v in RINGS) + "  spawnFormation: 0\n")
DONOR_RINGS = [450, 560, 660, 770]
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               f"  spawnRingRadiusFloor: {DONOR_RINGS[0]}\n  spawnRingRadiusFloorByIntensity:\n"
               + "".join(f"  - {v}\n" for v in DONOR_RINGS) + "  spawnFormation: 0\n")
NO_NUCLEUS = f"  noNucleusSpawnRadius: {CRYSTAL_RADIUS}\n"
# The crystal ladder the donor already carries and this mode keeps: 2 a pilot at I1, 1 at I2, one fewer than the
# roster at I3, a single crystal for the lobby at I4 (8 / 4 / 3 / 1 for a full lobby of four).
CRYSTAL_LADDER = """  crystalCountMode: 2
  fixedCrystalCount: 1
  extraCrystalsToSpawnBeyondPlayerCount: 0
  crystalCountByIntensity:
  - CrystalsPerPlayer: 2
    ExtraCrystals: 0
  - CrystalsPerPlayer: 1
    ExtraCrystals: 0
  - CrystalsPerPlayer: 1
    ExtraCrystals: -1
  - CrystalsPerPlayer: 0
    ExtraCrystals: 1
"""

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Harvest.asset", G_ASSET["ModePreviewHarvest"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Harvest") + f"""  Mode: {MODE_ID}
  Notes: 'The field at each intensity - big neutral sheaves, then shielded cores, team colours and
    danger stalks. What it teaches is the mow-then-smash - circle a sheaf so the chain cuts the
    stalks, then whip the ball into the core. Vessel pinned to Thresher({THRESHER_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {THRESHER_ID}
  ObjectiveText: Mow the stalks with your chain, then smash the ball into the core
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {RINGS[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in RINGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewHarvest"])

# ── 8. Scene: Jackstraw's, with the identity, the cells, the spawn ring and the crystal volume swapped ──
errors = list(pre_errors)


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["JackstrawStrikeTurnMonitor"], G_SCRIPT["HarvestPrismTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["JackstrawController"], G_SCRIPT["HarvestController"], "controller")
    scene = lib.swap_guid(scene, DONOR["JackstrawSettings"], G_ASSET["HarvestSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["JackstrawScoringRule"], G_ASSET["HarvestScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, "  noNucleusSpawnRadius: 50\n", NO_NUCLEUS, "crystal volume")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, NO_NUCLEUS,
                   CRYSTAL_LADDER]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameHarvest.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameHarvest", G_ASSET["MinigameHarvest.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameHarvest"])
g.register_always_unlocked()
g.register_build_scene("MinigameJackstraw", "MinigameHarvest", G_ASSET["MinigameHarvest.unity"])
g.set_end_condition("harvestPrismTarget", after="coronaBloomTarget", value=PRISM_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions: every way the Thresher destroys a prism names its pilot, so PrismsDestroyed counts it;
# the chain banks energy per kill; the ball spends the bank on a rival hit; Mass Mode is the A toggle the AI presses.
executor = g.read(THRESHER_EXECUTOR)
for needle in ("prism.Damage(impact, _status.Domain, _status.PlayerName",
               "prism.Slice(v * config.ChainDebrisRestitution, _status.Domain, _status.PlayerName",
               "AnnonymousExplosion = false,", "public bool InMassMode => _massMode;",
               "_massMode = true;"):
    if needle not in executor:
        errors.append(f"ThresherExecutor no longer has '{needle}' - Harvest scores the Thresher's chain, ball and "
                      f"blast kills and its AI leaves Mass Mode; re-check how the hull destroys a prism")
if "prism.Damage(impactVector, shipStatus.Domain, shipStatus.Player.Name" not in g.read(EXPLOSION_IMPACTOR):
    errors.append("ExplosionImpactor no longer credits a named blast to its pilot - the Thresher's blasts would score "
                  "nothing")
if "case ThresherControl.MassMode: thresher.ToggleMassMode(); break;" not in g.read(THRESHER_ACTION):
    errors.append("ThresherActionSO's MassMode press no longer toggles Mass Mode - HarvestReaper's A press would not "
                  "leave it")

# The C# default target matches the authored one.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultHarvestPrismTarget = {PRISM_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultHarvestPrismTarget differs from PRISM_TARGET")
for field in (f"harvestPrismTarget = {PRISM_TARGET};", f"harvestPrismTargetBuild = {PRISM_TARGET};"):
    if field not in eco:
        errors.append(f"EndConditionOverridesSO initializer '{field}' differs from PRISM_TARGET")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["HarvestSettingsSO"])
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
        errors.append(f"HarvestSettingsSO.{field} initializer differs from the authored {value}")
# The reaper reads the seat's difficulty, never the intensity (AIDifficulty.cs), and presses exactly one thing of the
# hull's: Mass Mode, through the replicated path.
reaper = g.read(SCRIPT_PATHS["HarvestReaper"])
if "SelectedIntensity" in reaper or "AIDifficultyFor(" not in reaper:
    errors.append("HarvestReaper must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
if reaper.count("PerformShipControllerActionsReplicated(") != 1 or "ThresherControl.MassMode" not in reaper:
    errors.append("HarvestReaper must press exactly one input - the hull's Mass Mode (A) - through the replicated path")
if "PerformShipControllerActions(" in reaper or "InputStatus" in reaper:
    errors.append("HarvestReaper presses a vessel input outside the replicated Mass Mode press")
controller = g.read(SCRIPT_PATHS["HarvestController"])
for touch in ("ThresherConfig", "ThresherExecutor", "FuseSecondsOverride"):
    if touch in controller:
        errors.append(f"HarvestController reaches into the vessel ({touch}) - a mode never changes the vessel")

# The field the drawing promised, as the C# lays it.
spawnable = g.read(SCRIPT_PATHS["SpawnableHarvest"])
for needle in (f"Block = {lib.num(budget.BLOCK)}f;", f"BalePitch = {lib.num(budget.BALE_PITCH)}f;",
               f"Segment = {lib.num(budget.SEGMENT)}f, Section = {lib.num(budget.SECTION)}f;",
               f"StalkAir = {lib.num(budget.STALK_AIR)}f;", f"PieceAir = {lib.num(budget.PIECE_AIR)}f;",
               f"StrawGap = {lib.num(budget.STRAW_GAP)}f;", f"Tries = {budget.TRIES};",
               f"Flush = {budget.FLUSH}f;", f"DefaultSeed => {budget.DEFAULT_SEED};",
               f"SheafKeyBase = {budget.SHEAF_KEY_BASE:_}, CloudKeyBase = {budget.CLOUD_KEY_BASE:_}, "
               f"StrawKeyBase = {budget.STRAW_KEY_BASE:_},",
               f"BaleKeyBase = {budget.BALE_KEY_BASE:_};", f"Key({budget.OPEN_KEY_BASE:_} + stream * 10_000,",
               "BaleDomainFor(int pattern, int slot) => pattern == 2 ? DomainFor(2, slot) : Domains.Blue;",
               "(0.5f * (baleSize - 1) * BalePitch + 0.5f * Block) * 1.7320508f",
               "unchecked(LayoutSeed * 7919 + family + (slot * Tries + attempt) * 4 + draw)",
               "1 => slot % 4 == 0 ? Playable[(slot / 4) % 3] : Domains.Blue,",
               "2 => slot % 4 == 3 ? Domains.Blue : Playable[slot % 4],",
               "Mathf.Max(2f * Reach + StalkAir, 2f * coreRadius + coreGap)",
               "public float StalkRoot => coreRadius + Block;"):
    if needle not in spawnable:
        errors.append(f"SpawnableHarvest no longer declares '{needle}' - update harvest_budget.py with it")

for r in RUNGS:
    c = r["laid"]["census"]
    tag = f"I{r['i']}"
    for fam in ("Sheaves", "Clouds", "Bales"):
        if c[f"skipped{fam}"]:
            errors.append(f"{tag}: {c[f'skipped{fam}']} {fam.lower()} found no room - the field is fuller than its "
                          f"radius allows")
    if c["perBale"] > CORE_PRISM_CAP:
        errors.append(f"{tag}: a bale holds {c['perBale']} prisms (> {CORE_PRISM_CAP})")
    if c["minMargin"] < budget.MIN_MARGIN:
        errors.append(f"{tag}: a sheaf sits {c['minMargin']:.5f} u from a rejection boundary - float32 could lay a "
                      f"different field than this census (re-seed the rung)")
    if c["perCore"] > CORE_PRISM_CAP:
        errors.append(f"{tag}: a core holds {c['perCore']} prisms (> {CORE_PRISM_CAP}) - one blast would ask too much")
    if r["coreRadius"] + budget.BLOCK > BLAST_RADIUS_REST:
        errors.append(f"{tag}: a core ({r['coreRadius']} u) is wider than a rest-Space full blast ({BLAST_RADIUS_REST} "
                      f"u) - a full bank would not clear it")
    if r["coreGap"] < BLAST_RADIUS_FULL_SPACE:
        errors.append(f"{tag}: coreGap {r['coreGap']} is under the full-Space blast radius - one blast takes two cores")
    if c["prisms"] > ARENA_PRISM_CAP:
        errors.append(f"{tag}: the field lays {c['prisms']} prisms (> {ARENA_PRISM_CAP})")
    if c["coreNeighbour"] < budget.BLOCK * math.sqrt(3):
        errors.append(f"{tag}: two core cubes sit {c['coreNeighbour']:.1f} u apart - closer than a cube's diagonal, "
                      f"so they could interpenetrate")
    if c["stalkRootNeighbour"] < budget.SECTION * 3:
        errors.append(f"{tag}: two stalk roots sit {c['stalkRootNeighbour']:.1f} u apart - closer than three sections")
    if budget.reach(r) - budget.stalk_root(r) < CHAIN_MAX * 0.5:
        errors.append(f"{tag}: the stalks are much shorter than the chain - a pass cannot mow them")
    if r["centreClearingRadius"] < CRYSTAL_RADIUS + 30:
        errors.append(f"{tag}: the crystals' {CRYSTAL_RADIUS} u spawn ball does not fit the {r['centreClearingRadius']} "
                      f"u clearing")
    if not (r["fieldRadius"] + 60 <= r["spawnRing"] <= MEMBRANE_RADIUS - 100):
        errors.append(f"{tag}: spawn ring {r['spawnRing']} must sit just outside the field ({r['fieldRadius']}) and "
                      f"inside the {int(MEMBRANE_RADIUS)} u membrane")
    # The sheaves never grow back: three domains' worth of the target must fit in the smallest hostile pool, so the
    # race can always end. A domain's pool is everything not in its own colour.
    own = max(c["prismsByDomain"].get(d, 0) for d in ("jade", "ruby", "gold"))
    if MAX_DOMAINS * PRISM_TARGET > c["prisms"] - own:
        errors.append(f"{tag}: {MAX_DOMAINS} domains x {PRISM_TARGET} > the smallest hostile pool "
                      f"({c['prisms'] - own}) - the field could run out before anyone reaches the target")
    playable = [c["byDomain"][d] for d in ("jade", "ruby", "gold")]
    if r["colourPattern"] and max(playable) - min(playable) > 1:
        errors.append(f"{tag}: the domain colours are unfair ({c['byDomain']})")

# The bake the scenes load must be the field the mirror counts (bake_prism_layouts.py writes it).
import json  # noqa: E402
bakes = {b["prefab"].rsplit("/", 1)[-1]: b for b in
         json.loads(g.read("Tools/Build/prism_layout_bakes.json")).get("bakes", [])}
for r in RUNGS:
    b = bakes.get(f"SpawnableHarvest{r['i']}.prefab")
    laid = r["laid"]["census"]["prisms"]
    rel = next(k for k in g.files if k.endswith(f"/SpawnableHarvest{r['i']}.prefab"))
    if not os.path.exists(os.path.join(lib.ROOT, rel)) or g.read(rel) != g.files[rel]:
        print(f"I{r['i']}: the field changes in this run - re-run bake_prism_layouts.py, then this script")
        continue
    if b is None or b.get("prisms") != laid:
        errors.append(f"I{r['i']}: the baked layout has {b and b.get('prisms')} prisms, the mirror {laid} - run "
                      f"python3 Tools/Build/bake_prism_layouts.py")

# I3's team crystals and I4's creatures.
for field, value in CRYSTAL_FIELDS.items():
    if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
        errors.append(f"HarvestSettingsSO.{field} initializer differs from the authored {value}")
if "teamCrystalsPerTeamByIntensity = { " + ", ".join(str(v) for v in TEAM_CRYSTALS_PER_TEAM) + " };" not in settings_cs:
    errors.append("HarvestSettingsSO.teamCrystalsPerTeamByIntensity differs from TEAM_CRYSTALS_PER_TEAM")
if "teamCrystalElement = Element.Space;" not in settings_cs:
    errors.append("HarvestSettingsSO.teamCrystalElement is no longer Space (asset authors 3)")
if "RestrictTo(" not in controller or "GetPrefab(settings.teamCrystalElement)" not in controller:
    errors.append("HarvestController no longer mints team-only Space crystals")
impactor = g.read("Assets/_Scripts/Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs")
if "_onlyFor" not in impactor:
    errors.append("ElementalCrystalImpactor lost its team gate - I3's team crystals would feed every domain")
if sum(cap for *_, cap in FAUNA_I4) > FAUNA_CAP:
    errors.append(f"I4's creatures cap at {sum(cap for *_, cap in FAUNA_I4)} live (> {FAUNA_CAP})")
for creature, name, gd, text, src in FAUNA:
    if "  SpreadElements: 1\n" not in text:
        errors.append(f"{name}: SpreadElements is off - I4's creatures must roll elements and drop elemental hearts")

# The scene: every new identity present, every donor one gone, four Thresher AIs, the crystal ladder kept.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("HarvestController", "HarvestPrismTurnMonitor")},
                     "HarvestSettings": G_ASSET["HarvestSettings"],
                     "HarvestScoringRule": G_ASSET["HarvestScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Jackstraw cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {THRESHER_ID}\n") != 4:
        errors.append("scene does not carry exactly 4 Thresher AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Harvest"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/HARVEST.md")])
