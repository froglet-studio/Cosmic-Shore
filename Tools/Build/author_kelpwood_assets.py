#!/usr/bin/env python3
"""
Authors every serialized asset the Kelpwood game mode needs (GameModes.Kelpwood = 86).

Kelpwood is the Angler's CHARGE game, picked by Shombith on 2026-11-09 from three drawn ideas (the others were Grazing
Grounds, drifting beds as the only ammo, and Wellspring, omni crystals refilling in a central well): a 0G ball of kelp
strands radiating from a core in every direction. Sweep the eraser onto a rival to LOCK them - one creature leaves
your school and hunts them (AnglerCommandExecutor.UpdateBeamLock, ANGLER.md §1e) - and every BITE, already a
Strike-class combat hit, is one point. The first DOMAIN to the target wins.

THE LADDER (one SpawnableKelpwood prefab, cell config, spawn profile and herd config per intensity; numbers in
kelpwood_budget.RUNGS):
  I1 Kelp Bed     - 28 strands, eight herds, three omni crystals.
  I2 Kelp Forest  - 44 strands, six herds, two omni crystals.
  I3 Kelp Tangle  - 64 strands, every third tipped in danger; five herds, two omni crystals.
  I4 Kelp Snare   - 88 strands, danger tips and danger collars; four herds, one omni crystal.
The kelp's census, volume and PhaseThresholds come from kelpwood_budget.py, an exact mirror of the C#.

NOTHING ABOUT THE ANGLER CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the kelp, the
herds and omni crystals in it (the cell's own spawner and CrystalManager) and the AI hunters. The scene is cloned from
Jackstraw's (the newest CombatPoints duel, whose controller declares settings / rule / arenaCell as this one does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_kelpwood_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import kelpwood_budget as budget  # noqa: E402

MODE_ID = 86
TOAST_HINT = 161
g = lib.Generator(MODE_ID, "Kelpwood")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "KelpwoodController":      f"{AR}/Kelpwood/KelpwoodController.cs",
    "KelpwoodSettingsSO":      f"{AR}/Kelpwood/KelpwoodSettingsSO.cs",
    "KelpwoodScoringRuleSO":   f"{AR}/Kelpwood/KelpwoodScoringRuleSO.cs",
    "KelpwoodHunter":          f"{AR}/Kelpwood/KelpwoodHunter.cs",
    "KelpwoodBiteTurnMonitor": f"{AR}/TurnMonitors/KelpwoodBiteTurnMonitor.cs",
    "SpawnableKelpwood":       "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableKelpwood.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Kelpwood Cell"
G_ASSET = {
    "ArcadeGameKelpwood":      guid("asset/ArcadeGameKelpwood"),
    "KelpwoodScoringRule":     guid("asset/KelpwoodScoringRule"),
    "KelpwoodSettings":        guid("asset/KelpwoodSettings"),
    "GameToastConfigKelpwood": guid("asset/GameToastConfigKelpwood"),
    "ModePreviewKelpwood":     guid("asset/ModePreviewKelpwood"),
    "MinigameKelpwood.unity":  guid("asset/MinigameKelpwood.unity"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"KelpwoodCellConfig{i}"] = guid(f"asset/KelpwoodCellConfig{i}")
    G_ASSET[f"KelpwoodSpawnProfile{i}"] = guid(f"asset/KelpwoodSpawnProfile{i}")
    G_ASSET[f"KelpwoodTadpole{i}"] = guid(f"asset/KelpwoodTadpole{i}")
    G_ASSET[f"prefab/SpawnableKelpwood{i}"] = guid(f"asset/SpawnableKelpwood{i}.prefab")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Angler"] = "ea422bbe0d1aef61993692999cee50ea"           # SO_Class_Angler.asset
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"            # SpawnablePrism.prefab (window 0.5-100)
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"   # the standard 1,200 u cell (the Boneyard's)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
# The herd: a Kelpwood-owned copy of the Thresher Time I1 tadpole (shipped, play-run), so tuning the herds re-tunes
# no other mode. Tadpoles are herbivores and in the Angler's omniFauna list: tameable.
TADPOLE_DONOR = "Assets/_SO_Assets/Cell Configs/Thresher Time Cell/Thresher Time I1 Tadpole Fauna Config Data.asset"
EXISTING["FaunaConfigurationSO"] = "c778cfbe4dfc4c5c8401e40c17802311"
EXISTING["Tadpole_prefab"] = "c7fd418d426de8740ac888dcc23a5d24"
ANGLER_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/AnglerCommandExecutor.cs"
ANGLER_PREFAB = "Assets/_Prefabs/Spacevessels/Angler.prefab"   # the executor's onCombatHitLanded is wired here
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
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
FAUNA_FILEID = 5945480239701989318
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 8600000000000201, 8600000000000202, 8600000000000203

# ── The hunt ─────────────────────────────────────────────────────────────────
BITE_TARGET = 12          # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultKelpwoodBiteTarget
BITE_POINTS = 1
STRIKE_CLASS = 5          # CombatHitClass.Strike - what AnglerCommandExecutor.Bite reports
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (3 bites) buys
# 1.2 element levels; on the Angler, Time is the one that changes a number in this fight (faunaSpeed: hunters swim
# faster), so the comeback reaches the chase.
COMEBACK_RATE = 0.4

MEMBRANE_RADIUS = 1200.0
OMNI_BALL = 200           # the omni crystals spawn in a ball this wide round the core (CrystalManager.noNucleusSpawnRadius)

RUNGS = budget.RUNGS
for r in RUNGS:
    r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableKelpwood{r['i']}.prefab"
    r["laid"] = budget.build(r)

# Cleave / Serpent Gulag / Boneyard / Jackstraw ladder deltas: a cell's phase rides ITS arena's own baseline.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameKelpwood.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Kelpwood")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/KELPWOOD.md")

# ── 2. Scoring rule (Jackstraw's, inherited; Kelpwood's own words) ───────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/KelpwoodScoringRule.asset", G_ASSET["KelpwoodScoringRule"],
             lib.header_for(G_SCRIPT["KelpwoodScoringRuleSO"], "KelpwoodScoringRule") +
             f"""  metric: 8
  golfRules: 1
  strikePoints: {BITE_POINTS}
  otherHitPoints: 0
  strikesLeftTemplate: '{{count}} Bites Left'
  strikesLandedTemplate: '{{count}} bites'
  victoryHeader: VICTORY
  victoryLabel: HUNT TIME
  defeatHeader: DEFEAT
  defeatLabel: BITES LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":            ([1.2, 0.9, 0.6, 0.4], "down"),
    "aiHuntRange":                  ([400, 550, 700, 900], "up"),
    "aiLeadSeconds":                ([0, 0.1, 0.2, 0.3], "up"),
    "aiAimError":                   ([16, 9, 4, 1], "down"),
    "aiLockGateDegrees":            ([6, 4, 2.5, 1.5], "down"),
    "aiLockIntervalSeconds":        ([3, 2, 1.3, 0.8], "down"),
    "aiMaxHuntersOut":              ([1, 2, 3, 4], "up"),
    "aiBreakOffWhenSilencedChance": ([0, 0.5, 1, 1], "up"),
    "aiSilencedPreyFocus":          ([1, 1, 1.5, 2], "up"),
}
SHARED = {"progressSampleSeconds": 0.5, "aiLockHoldSeconds": 0.3, "aiRefuelBelowSchool": 2, "aiHumanFocus": 2}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/KelpwoodSettings.asset", G_ASSET["KelpwoodSettings"],
             lib.header_for(G_SCRIPT["KelpwoodSettingsSO"], "KelpwoodSettings") +
             f"  progressSampleSeconds: {lib.num(SHARED['progressSampleSeconds'])}\n"
             + "".join(ladder_yaml(n) for n in LADDERS)
             + f"  aiLockHoldSeconds: {lib.num(SHARED['aiLockHoldSeconds'])}\n"
             + f"  aiRefuelBelowSchool: {lib.num(SHARED['aiRefuelBelowSchool'])}\n"
             + f"  aiHumanFocus: {lib.num(SHARED['aiHumanFocus'])}\n")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a lock never takes a team-mate, so a solo lobby has nobody to bite.
DESCRIPTION = ("Anglers only - a hunt through a ball of kelp, strands reaching out from the core in every direction. "
               "Sweep your eraser onto a rival pilot to lock them - one creature leaves your school and hunts them, and "
               "every bite is a point that silences them. Tame the herds grazing the kelp, or take an omni crystal, to "
               "grow your school; dash to your other group to shake a hunter. Higher intensities thicken the kelp, thin "
               "the herds and crystals, and add danger tips and collars. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameKelpwood"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameKelpwood") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Kelpwood
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameKelpwood
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Angler']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Kelpwood.asset", G_ASSET["GameToastConfigKelpwood"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Kelpwood") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(86, "<b>{0}</b> drew blood - a hunter bit a rival!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} bites", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} bites", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Hold the eraser on a rival to lock - a creature hunts them. Tame the herds in the "
                       "kelp for more", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigKelpwood"])

# ── 6. The arena: one environment prefab, herd, spawn profile and cell config per rung ──
for r in RUNGS:
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableKelpwood{r['i']}"], f"""%YAML 1.1
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
  m_Name: SpawnableKelpwood{r['i']}
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
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableKelpwood']}, type: 3}}
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
  kelpRadius: {r['kelpRadius']}
  strandCount: {r['strandCount']}
  dangerTipEveryNth: {r['dangerTipEveryNth']}
  collarEveryNth: {r['collarEveryNth']}
""")

# The herd: the donor tadpole with the rung's population. Only the population lines change; everything the creature
# IS (its body, diet, starvation, speed, element spread) stays the shipped tadpole's.
tadpole_donor = g.read(TADPOLE_DONOR)
for r in RUNGS:
    herd = r["herdPoints"] * r["herdsPerPoint"]
    body = tadpole_donor
    for field, value in (("InitialSpawnCount", herd), ("PopulationSize", herd), ("MaxLivePopulation", herd + herd // 2)):
        body, n = re.subn(rf"^  {field}: \d+$", f"  {field}: {value}", body, flags=re.M)
        assert n == 1, f"{field} not found once in {TADPOLE_DONOR}"
    body = body.replace("  m_Name: Thresher Time I1 Tadpole Fauna Config Data\n",
                        f"  m_Name: Kelpwood I{r['i']} Tadpole Fauna Config Data\n")
    fields = body.split("  m_EditorClassIdentifier:\n", 1)[1]   # everything the creature IS, below the header
    g.emit_asset(f"{CELL_DIR}/Kelpwood I{r['i']} Tadpole Fauna Config Data.asset", G_ASSET[f"KelpwoodTadpole{r['i']}"],
                 lib.header_for(EXISTING["FaunaConfigurationSO"], f"Kelpwood I{r['i']} Tadpole Fauna Config Data")
                 + fields)

    g.emit_asset(f"{CELL_DIR}/Kelpwood Spawn Profile {r['i']}.asset", G_ASSET[f"KelpwoodSpawnProfile{r['i']}"],
                 lib.header_for(EXISTING["SpawnProfileSO"], f"Kelpwood Spawn Profile {r['i']}") + f"""  FloraExcludeLocalDomain: 0
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
  HerbivoreSpawnPointCount: {r['herdPoints']}
  HerbivoreSpawnRadius: {round(r['kelpRadius'] * 0.55)}
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas:
  - {{fileID: 11400000, guid: {G_ASSET[f"KelpwoodTadpole{r['i']}"]}, type: 2}}
""")

for r in RUNGS:
    i = r["i"]
    c = r["laid"]["census"]
    th = thresholds(c["prisms"], round(c["volume"]))
    env = f"{{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableKelpwood{i}']}, type: 3}}"
    desc = (f"{r['label']} - intensity {i} of Kelpwood: {r['blurb']}. {c['prisms']} prisms, all neutral "
            f"({c['danger']} danger), kelp radius {r['kelpRadius']} inside the standard {int(MEMBRANE_RADIUS)} u cell, "
            f"spawn ring {r['spawnRing']}. {r['herdPoints'] * r['herdsPerPoint']} tameable tadpoles graze the kelp; "
            f"{r['omni']} omni crystals float round the core (the scene's CrystalManager). NO NUCLEUS, no flora. "
            f"Authored by Tools/Build/author_kelpwood_assets.py from kelpwood_budget.py - re-run it rather than "
            f"hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Kelpwood Cell Config {i}.asset", G_ASSET[f"KelpwoodCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Kelpwood Cell Config {i}") + f"""  CellName: Kelpwood
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'KelpwoodSpawnProfile{i}']}, type: 2}}
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

CELLS = [G_ASSET[f"KelpwoodCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


RINGS = [r["spawnRing"] for r in RUNGS]
SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {RINGS[0]}\n  spawnRingRadiusFloorByIntensity:\n"
         + "".join(f"  - {v}\n" for v in RINGS) + "  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 450\n  spawnRingRadiusFloorByIntensity:\n"
               "  - 450\n  - 560\n  - 660\n  - 770\n  spawnFormation: 0\n")
OMNI = (f"  noNucleusSpawnRadius: {OMNI_BALL}\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
        "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
        + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {r['omni']}\n" for r in RUNGS))
DONOR_OMNI = ("  noNucleusSpawnRadius: 50\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
              "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
              "  - CrystalsPerPlayer: 2\n    ExtraCrystals: 0\n  - CrystalsPerPlayer: 1\n    ExtraCrystals: 0\n"
              "  - CrystalsPerPlayer: 1\n    ExtraCrystals: -1\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 1\n")

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Kelpwood.asset", G_ASSET["ModePreviewKelpwood"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Kelpwood") + f"""  Mode: {MODE_ID}
  Notes: 'The kelp ball at each intensity - more strands, fewer herds, then danger tips and collars. OPEN-ENDED -
    it scores on biting rival pilots and a solo preview has none; what it teaches is the kelp, the herds
    to tame and the omni crystals at the core. Vessel pinned to Angler({ANGLER_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {ANGLER_ID}
  ObjectiveText: Hold the eraser on a rival pilot to set a hunter on them
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
g.register_preview(G_ASSET["ModePreviewKelpwood"])

# ── 8. Scene: Jackstraw's, with the identity, the cells, the AI hulls, the spawn ring and the crystals swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["JackstrawStrikeTurnMonitor"], G_SCRIPT["KelpwoodBiteTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["JackstrawController"], G_SCRIPT["KelpwoodController"], "controller")
    scene = lib.swap_guid(scene, DONOR["JackstrawSettings"], G_ASSET["KelpwoodSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["JackstrawScoringRule"], G_ASSET["KelpwoodScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {THRESHER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "omni crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, OMNI]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameKelpwood.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameKelpwood", G_ASSET["MinigameKelpwood.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameKelpwood"])
g.register_always_unlocked()
g.register_build_scene("MinigameJackstraw", "MinigameKelpwood", G_ASSET["MinigameKelpwood.unity"])
g.set_end_condition("kelpwoodBiteTarget", after="jackstrawStrikeTarget", value=BITE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * BITE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {BITE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * BITE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions: the Angler still locks from the left trigger, still spends a creature per lock, and
# still reports a bite as a Strike through TryAdmit on the scoring channel.
executor = g.read(ANGLER_EXECUTOR)
for needle in ("input.LeftTriggerAnalog", "void UpdateBeamLock(bool erasing", "SpendOn(inBeam)",
               # the bite cooldown is element-scaled since #1934 (claude/angler-kit); Kelpwood reads no cooldown,
               # only that a bite is still admitted as a Strike
               "CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Strike, config.BiteCooldownFor(_status)",
               "HitClass = CombatHitClass.Strike", "public int TamedCount", "public int HunterCount"):
    if needle not in executor:
        errors.append(f"AnglerCommandExecutor no longer has '{needle}' - Kelpwood's rule pays a lock's bites and its "
                      f"hunter presses the left trigger; re-check how the Angler locks and bites")
if "onCombatHitLanded: {fileID: 11400000, guid: 45fb43a0cddb5cc3cc4dc448df994152" not in g.read(ANGLER_PREFAB):
    errors.append("Angler.prefab's AnglerCommandExecutor.onCombatHitLanded is no longer the game's combat-hit channel - bites would score nothing")
if not g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs").count(f"Strike = {STRIKE_CLASS},"):
    errors.append(f"CombatHitClass.Strike is no longer {STRIKE_CLASS}")
if f"KelpwoodHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.KelpwoodHint is not {TOAST_HINT}")
if f"Kelpwood = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Kelpwood is not {MODE_ID}")

# The herd is tameable: a tadpole (in the Angler's omniFauna list) and a herbivore forager.
if f"FaunaPrefab: {{fileID: {FAUNA_FILEID}, guid: {EXISTING['Tadpole_prefab']}, type: 3}}" not in tadpole_donor:
    errors.append("the donor tadpole config no longer names the tadpole prefab")
if "    Forager: 1\n" not in tadpole_donor:
    errors.append("the donor tadpole is no longer a forager - it would not graze the kelp")

# The C# default target matches the authored one.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultKelpwoodBiteTarget = {BITE_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultKelpwoodBiteTarget differs from BITE_TARGET")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["KelpwoodSettingsSO"])
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
        errors.append(f"KelpwoodSettingsSO.{field} initializer differs from the authored {value}")
# The hunter reads the seat's difficulty, never the intensity, and presses only the eraser (the Angler's own trigger).
hunter = g.read(SCRIPT_PATHS["KelpwoodHunter"])
if "SelectedIntensity" in hunter or "AIDifficultyFor(" not in hunter:
    errors.append("KelpwoodHunter must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
if "PerformShipControllerActions" in hunter or "RightTriggerAnalog" in hunter:
    errors.append("KelpwoodHunter presses something other than the eraser - the hull's leapfrog policy owns the rest")
if "LeftTriggerAnalog" not in hunter:
    errors.append("KelpwoodHunter no longer presses the eraser - an AI Angler would never lock")

# The kelp the drawing promised, as the C# lays it.
spawnable = g.read(SCRIPT_PATHS["SpawnableKelpwood"])
for needle in (f"StrandPrisms = {budget.STRAND_PRISMS};", f"Section = {lib.num(budget.SECTION)}f;",
               f"StrandInner = {lib.num(budget.STRAND_INNER)}f;",
               f"SwayMin = {lib.num(budget.SWAY_MIN)}f, SwaySpan = {lib.num(budget.SWAY_SPAN)}f;",
               f"SwayTurns = {lib.num(budget.SWAY_TURNS)}f;", f"TipPrisms = {budget.TIP_PRISMS};",
               f"CollarPrisms = {budget.COLLAR_PRISMS};", f"CollarRadius = {lib.num(budget.COLLAR_RADIUS)}f;",
               f"CollarSection = {lib.num(budget.COLLAR_SECTION)}f, CollarLength = {lib.num(budget.COLLAR_LENGTH)}f;",
               f"CollarAt = {budget.COLLAR_AT};", f"Flush = {budget.FLUSH}f;",
               "StrandKeyBase = 3_000_000;", f"DefaultSeed => {budget.DEFAULT_SEED};",
               "unchecked(LayoutSeed * 7919 + family + slot * 4 + draw)",
               "var side = u + v * 0.5f;", "float off = amp * Mathf.Sin(phase + SwayTurns * t) * t;"):
    if needle not in spawnable:
        errors.append(f"SpawnableKelpwood no longer declares '{needle}' - update kelpwood_budget.py with it")

counts = []
for r in RUNGS:
    c = r["laid"]["census"]
    counts.append(c["prisms"])
    if c["minStrandGap"] < budget.STRAND_GAP:
        errors.append(f"I{r['i']}: two strands come {c['minStrandGap']:.2f} u apart (< {budget.STRAND_GAP}) - they touch")
    if c["worstFlushSlack"] < budget.MIN_FLUSH_SLACK:
        errors.append(f"I{r['i']}: a strand bends too hard for its prisms ({c['worstFlushSlack']:.3f} u of air) - "
                      f"they would interpenetrate")
    if r["collarEveryNth"] == 1:
        errors.append(f"I{r['i']}: collarEveryNth 1 marks nothing (slot % 1 is never 1)")
    if not (r["kelpRadius"] + 60 <= r["spawnRing"] <= MEMBRANE_RADIUS - 300):
        errors.append(f"I{r['i']}: spawn ring {r['spawnRing']} must sit just outside the kelp ({r['kelpRadius']}) and "
                      f"well inside the {int(MEMBRANE_RADIUS)} u membrane")
    if OMNI_BALL >= r["kelpRadius"]:
        errors.append(f"I{r['i']}: the omni crystals' {OMNI_BALL} u ball reaches out of the kelp")
    if budget.COLLAR_RADIUS - 1.5 * 2 ** 0.5 - budget.COLLAR_SECTION * 0.5 * 2 ** 0.5 <= 0:
        errors.append("a collar prism touches its strand")
for name, ladder, direction in (("prisms", counts, "up"),
                                ("herds", [r["herdPoints"] * r["herdsPerPoint"] for r in RUNGS], "down"),
                                ("omni crystals", [r["omni"] for r in RUNGS], "down")):
    pairs = list(zip(ladder, ladder[1:]))
    if direction == "up" and any(b < a for a, b in pairs) or direction == "down" and any(b > a for a, b in pairs):
        errors.append(f"the {name} ladder must run {direction} with intensity ({ladder})")
for a, b in zip(counts, counts[1:]):
    if b < a * 1.25:
        errors.append(f"an intensity step adds under 25 % more kelp ({a} -> {b}): intensity would not be felt")
if counts[-1] > 4000:
    errors.append(f"I4 lays {counts[-1]} prisms - keep the hunt light")

# The scene: every new identity present, every donor one gone, four Angler AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("KelpwoodController", "KelpwoodBiteTurnMonitor")},
                     "KelpwoodSettings": G_ASSET["KelpwoodSettings"],
                     "KelpwoodScoringRule": G_ASSET["KelpwoodScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Jackstraw cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {ANGLER_ID}\n") != 4 or f"  - vesselClass: {THRESHER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Angler AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Kelpwood"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/KELPWOOD.md")])
