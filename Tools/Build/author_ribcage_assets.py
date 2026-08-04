#!/usr/bin/env python3
"""
Authors every serialized asset the Ribcage game mode needs (GameModes.Ribcage = 39).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_ribcage_assets.py [--check]

--check validates without writing (CI / pre-commit use).

See Assets/_Scripts/Controller/Arcade/RIBCAGE.md for what these numbers mean.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "SpawnableRibcage":        guid("script/SpawnableRibcage"),
    "RibcageController":       guid("script/RibcageController"),
    "RibcagePrismTurnMonitor": guid("script/RibcagePrismTurnMonitor"),
    "RibcageScoringRuleSO":    guid("script/RibcageScoringRuleSO"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "SpawnableRibcage.prefab":   guid("asset/SpawnableRibcage.prefab"),
    "ArcadeGameRibcage":         guid("asset/ArcadeGameRibcage"),
    "RibcageScoringRule":        guid("asset/RibcageScoringRule"),
    "RibcageCellConfig":         guid("asset/RibcageCellConfig"),
    "RibcageSpawnProfile":       guid("asset/RibcageSpawnProfile"),
    "RibcageTadpoleFauna":       guid("asset/RibcageTadpoleFauna"),
    "RibcageQuadFishFauna":      guid("asset/RibcageQuadFishFauna"),
    "RibcageClawfishFauna":      guid("asset/RibcageClawfishFauna"),
    "RibcageBrittlestarFauna":   guid("asset/RibcageBrittlestarFauna"),
    "RibcageSharkFauna":         guid("asset/RibcageSharkFauna"),
    "MinigameRibcage.unity":     guid("asset/MinigameRibcage.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    # script types
    "SO_ArcadeGame":        "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":     "01f934d50526431a9392a6ceca1dc33d",
    "SpawnProfileSO":       "e8d8aa5d835249798a256e18f2f7d912",
    "FaunaConfigurationSO": "c778cfbe4dfc4c5c8401e40c17802311",
    # donor scene scripts to swap out
    "RampageController":       "e11ff862e6844a89a951292673243625",
    "RampagePrismTurnMonitor": "694b571734fe4a55a57f6cc672c7fcc2",
    "RampageCellConfig":       "c6959b0e548d4f26bdde820ca48ac26e",
    "RampageScoringRule":      "7d1bfbd4091c4a12a12c730553bf293a",
    # shared content
    "Prism_prefab":       "ed9defc56162b4b4588e61c20984b6d9",
    "Membrane_prefab":    "6e330f85972faf843b8a128e7166f7b5",
    "Cytoplasm_prefab":   "9cacd903fcf4643459f5f14ac811bb20",
    "CellIcon":           "6aa1c06e11b265744a5f9fa8858ac72a",
    "Vessel_Rhino":       "ec97e344adb08f847a8f7649ab79088e",
    # arcade card art (shared with Rampage - the destruction family)
    "IconActive":         "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":       "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":     "587d2203114c8004c9985d0112c89585",
    "PreviewClip":        "4396864d799a6154bb82e5346ac0093b",
    # fauna prefabs + element palettes (cloned from the Blob species definitions)
    "TadpolePrefab":      "c7fd418d426de8740ac888dcc23a5d24",
    "QuadFishPrefab":     "19615ed0c903b1041973d70593d4b0a3",
    "ClawfishPrefab":     "a525483096f54bc44a73646161623bf5",
    "BrittlestarPrefab":  "c719f00ea7596c24185379994f7dc824",
    "SharkPrefab":        "a67ba7ddaecf6624ab37cd9f5f2210a6",
    "TadpoleBodyMat":     "5140ec1c42866e849927f442d5965f7f",
    # the cell's CellRuntimeDataSO - the spawn ring resolves its Cell through this
    "RuntimeCellData":    "8d4e8398eedc76c4dadb8604f89b9e1b",
}

# Fauna COMPONENT fileIDs (FaunaConfigurationSO.FaunaPrefab is typed Fauna, so it points at
# the MonoBehaviour inside the prefab, never the GameObject).
FAUNA_FILEID = {
    "Tadpole":     5945480239701989318,   # Boid,       herbivore
    "QuadFish":    4652232322436628206,   # LightFauna, herbivore
    "Clawfish":     369859875180954115,   # QuadFish,   herbivore
    "Brittlestar": 5351160486092638538,   # LightFauna, herbivore
    "Shark":       5351160486092638538,   # LightFauna, PREDATOR
}

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
TADPOLE_FILEID = 5945480239701989318
SHARK_FILEID = 5351160486092638538
PREVIEW_FILEID = 241334157148977051

# Blob element-palette sibling configs, reused verbatim (read-only species identity assets).
TADPOLE_PALETTE = ["ede43cd3ab5943c58c646065c1f57a1f", "28c9a96388684fa0b3b10b9dbea56c70",
                   "72fa98519b534214b89e9c29c44b89da", "62a30981533145a5b66304c04e7c50e0"]
SHARK_PALETTE = ["58835b82ea284255855af2649ef185a5", "a690f25bf21e486ba0e500563b90f1ea",
                 "eaf56c14345740849f35fc84467059e9", "78ce842bb8554d748af1e96abf430137"]

# ── Measured cage baseline (Tools/Build/ribcage_budget.py; analytic, exact) ───
# Cage geometry (SpawnableRibcage.cs — keep in sync; Tools/Build/ribcage_budget.py models it)
CAGE_RADIUS = 360         # +20% arena
SPAWN_RING_RADIUS = 576   # 1.6x the cage, well inside the 1200u membrane
HERBIVORE_RING = 200      # inside the pen (338) so the brood hatches within the bone
PREDATOR_RING = 250
CAGE_PRISMS = 3175
CAGE_VOLUME = 1265194
# PhaseThresholds = measured baseline + the standard Blob deltas (Docs/ECOSYSTEM.md §18).
BLOB_DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)

_HEADER_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __GUID__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def HEADER_FOR(script_guid: str, name: str) -> str:
    return _HEADER_TMPL.replace("__GUID__", script_guid).replace("__NAME__", name)


def meta(g: str, folder: bool = False) -> str:
    if folder:
        return f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def prefab_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


# ── 1. .cs.meta for the four new scripts ─────────────────────────────────────
SCRIPT_PATHS = {
    "SpawnableRibcage":        "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableRibcage.cs",
    "RibcageController":       "Assets/_Scripts/Controller/Arcade/RibcageController.cs",
    "RibcagePrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/RibcagePrismTurnMonitor.cs",
    "RibcageScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Scoring/RibcageScoringRuleSO.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. SpawnableRibcage.prefab (donor-cloned from SpawnableGeode.prefab) ─────
emit("Assets/_Prefabs/Spawnables/SpawnableRibcage.prefab", f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5260000000000201
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5260000000000202}}
  - component: {{fileID: 5260000000000203}}
  m_Layer: 0
  m_Name: SpawnableRibcage
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5260000000000202
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000201}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5260000000000203
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000201}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableRibcage']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: 39
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: 1
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
""")
emit("Assets/_Prefabs/Spawnables/SpawnableRibcage.prefab.meta",
     prefab_meta(G_ASSET["SpawnableRibcage.prefab"]))


# ── 3. Scoring rule ──────────────────────────────────────────────────────────
emit("Assets/_SO_Assets/Scoring Rules/RibcageScoringRule.asset",
     HEADER_FOR(G_SCRIPT["RibcageScoringRuleSO"], "RibcageScoringRule") +
     "  metric: 5\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/RibcageScoringRule.asset.meta",
     asset_meta(G_ASSET["RibcageScoringRule"]))


# ── 4. Arcade game config ────────────────────────────────────────────────────
emit("Assets/_SO_Assets/Games/ArcadeGameRibcage.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameRibcage") + f"""  Mode: 39
  IsMultiplayer: 1
  DisplayName: Ribcage
  Description: A hollow cage of shielded bone pens the cell's brood. Ram it, crack
    it, break out - and the domain in front wears the swarm's colours, so every
    beast you free hunts the teams behind you.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  PreviewClip: {{fileID: {PREVIEW_FILEID}, guid: {EXISTING['PreviewClip']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameRibcage
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Rhino']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 404
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: 0.03
""")
emit("Assets/_SO_Assets/Games/ArcadeGameRibcage.asset.meta", asset_meta(G_ASSET["ArcadeGameRibcage"]))


# ── 5b. The brood: five species, table-driven ───────────────────────────────
#
# The cage is meant to look FULL and read as dangerous, so the caged tier carries four
# herbivore species (a dense tadpole shoal plus three larger, slower bodies for silhouette
# variety) and the predator joins at 50%. Seeds are what hatch immediately; MaxLive is the
# per-species performance backstop the food web works under.
#
#   species      tier  seed  MaxLive   role
#   Tadpole        0     16     30      the shoal - fast, numerous, the "swarm" read
#   QuadFish       0      8     14      mid-size rovers
#   Clawfish       0      6     10      heavier, slower, most threatening silhouette
#   Brittlestar    0      5      8      drifting arms - fills the volume
#   Shark          1      2      4      the 50% predator (eats HERBIVORES, not prisms)
#                       ---    ---
#   caged totals          35     62     (+4 sharks once the pack rung lands)
FAUNA_SPECIES = [
    dict(key="Tadpole",     asset="RibcageTadpoleFauna",     tier=0, seed=16, cap=30, initial=16,
         element=2, center=0.15, prefab="TadpolePrefab", palette="TADPOLE",
         variant=dict(scale=0.4, prism="{x: 0.8, y: 0.8, z: 7}", mat="TadpoleBodyMat",
                      starve=90, forager=1, cohesion=50, tick=1.2, reach=22, goalw=3,
                      minspd=12, maxspd=18)),
    dict(key="QuadFish",    asset="RibcageQuadFishFauna",    tier=0, seed=8,  cap=14, initial=8,
         element=1, center=0.25, prefab="QuadFishPrefab", palette="TADPOLE", variant=None),
    dict(key="Clawfish",    asset="RibcageClawfishFauna",    tier=0, seed=6,  cap=10, initial=6,
         element=3, center=0.3,  prefab="ClawfishPrefab", palette="TADPOLE", variant=None),
    dict(key="Brittlestar", asset="RibcageBrittlestarFauna", tier=0, seed=5,  cap=8,  initial=5,
         element=4, center=0.35, prefab="BrittlestarPrefab", palette="TADPOLE", variant=None),
    dict(key="Shark",       asset="RibcageSharkFauna",       tier=1, seed=2,  cap=4,  initial=1,
         element=0, center=0.2,  prefab="SharkPrefab", palette="SHARK", variant=None),
]

PALETTES = {"TADPOLE": TADPOLE_PALETTE, "SHARK": SHARK_PALETTE}


# ── 5. Cell config + spawn profile + fauna ───────────────────────────────────
emit("Assets/_SO_Assets/Cell Configs/Ribcage Cell.meta", meta(guid("folder/RibcageCell"), folder=True))

emit("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config.asset",
     HEADER_FOR(EXISTING["CellConfigDataSO"], "Ribcage Cell Config") + f"""  CellName: Ribcage
  Description: The cage cell - a hollow sphere of shielded prism bone penning the
    brood. NO NUCLEUS by design - a nucleus control zone would switch herbivores to
    the spatial 'eat anything outside the nucleus' diet, and Ribcage needs the legacy
    opposing-domain diet so the leader's swarm hunts the trailing teams only.
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: 2
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['Membrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['RibcageSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {{fileID: 5260000000000203, guid: {G_ASSET['SpawnableRibcage.prefab']}, type: 3}}
  EnvironmentIntensity: 1
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {CAGE_PRISMS + BLOB_DELTAS['re']}
    RestlessExit: {CAGE_PRISMS + BLOB_DELTAS['rx']}
    FrenzyEnter: {CAGE_PRISMS + BLOB_DELTAS['fe']}
    FrenzyExit: {CAGE_PRISMS + BLOB_DELTAS['fx']}
    RestlessEnterVolume: {CAGE_VOLUME + BLOB_DELTAS['rev']}
    RestlessExitVolume: {CAGE_VOLUME + BLOB_DELTAS['rxv']}
    FrenzyEnterVolume: {CAGE_VOLUME + BLOB_DELTAS['fev']}
    FrenzyExitVolume: {CAGE_VOLUME + BLOB_DELTAS['fxv']}
""")
emit("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config.asset.meta",
     asset_meta(G_ASSET["RibcageCellConfig"]))

emit("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset",
     HEADER_FOR(EXISTING["SpawnProfileSO"], "Ribcage Spawn Profile") + f"""  FloraExcludeLocalDomain: 0
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
  HerbivoreSpawnPointCount: 4
  HerbivoreSpawnRadius: {HERBIVORE_RING}
  PredatorSpawnPointCount: 2
  PredatorSpawnRadius: {PREDATOR_RING}
  SupportedFaunas:
""" + "".join(
    f"  - {{fileID: 11400000, guid: {G_ASSET[sp['asset']]}, type: 2}}\n"
    for sp in FAUNA_SPECIES))
emit("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset.meta",
     asset_meta(G_ASSET["RibcageSpawnProfile"]))


for sp in FAUNA_SPECIES:
    body = HEADER_FOR(EXISTING["FaunaConfigurationSO"], f"Ribcage {sp['key']} Fauna Config Data")
    body += (f"  FaunaPrefab: {{fileID: {FAUNA_FILEID[sp['key']]}, "
             f"guid: {EXISTING[sp['prefab']]}, type: 3}}\n")
    body += f"  InitialSpawnCount: {sp['initial']}\n"
    body += f"  PopulationSize: {sp['seed']}\n"
    body += "  SpawnProbability: 1\n"
    # Reproduction ON for the grazers (the food web drives the population once an intruder
    # feeds them); the predator keeps Blob's slower cadence.
    body += "  FeedsPerOffspring: 20\n" if sp["tier"] == 0 else "  FeedsPerOffspring: 6\n"
    body += "  OffspringPerBirth: 1\n"
    body += "  ReproductionCooldownSeconds: 10\n" if sp["tier"] == 0 else "  ReproductionCooldownSeconds: 30\n"
    body += f"  MaxLivePopulation: {sp['cap']}\n"
    body += f"  ReleaseTier: {sp['tier']}\n"
    body += f"  CenterFocusBias: {sp['center']}\n"
    if sp["element"]:
        body += f"  Element: {sp['element']}\n"
    body += "  InitialLevel: 1\n  BodyScalePerLevel: 1.15\n  CrystalScalePerLevel: 1.2\n  LevelGrowSeconds: 1\n"
    v = sp["variant"]
    if v:
        body += (f"  Variant:\n    Enabled: 1\n    BaseBodyScale: {v['scale']}\n"
                 f"    BodyPrismScale: {v['prism']}\n"
                 f"    BodyMaterial: {{fileID: 2100000, guid: {EXISTING[v['mat']]}, type: 2}}\n"
                 f"    StarvationSeconds: {v['starve']}\n    Forager: {v['forager']}\n"
                 f"    CohesionRadius: {v['cohesion']}\n    BehaviorUpdateRate: {v['tick']}\n"
                 f"    TrailBlockInteractionRadius: {v['reach']}\n    GoalWeight: {v['goalw']}\n"
                 f"    MinSpeed: {v['minspd']}\n    MaxSpeed: {v['maxspd']}\n"
                 "    OverrideAudio: 0\n    AudioLoopEvent:\n      Guid:\n        Data1: 0\n"
                 "        Data2: 0\n        Data3: 0\n        Data4: 0\n      Path:\n"
                 "    AudioMinDistance: -1\n    AudioMaxDistance: -1\n")
    body += "  SpreadElements: 1\n  ElementPalette:\n"
    for g in PALETTES[sp["palette"]]:
        body += f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n"
    body += "  Levels:\n    Enabled: 1\n    MinLevel: 1\n    MaxLevel: 5\n    RarityFalloff: 2\n"

    path = f"Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage {sp['key']} Fauna Config Data.asset"
    emit(path, body)
    emit(path + ".meta", asset_meta(G_ASSET[sp["asset"]]))


# ── 6. Scene: clone MinigameRampage, swap the mode-specific wiring ───────────
DONOR_SCENE = os.path.join(ROOT, "Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity")
with open(DONOR_SCENE, encoding="utf-8") as fh:
    scene = fh.read()

# 6a. turn monitor script swap (field set is identical - base TurnMonitor fields only)
scene, n = re.subn(EXISTING["RampagePrismTurnMonitor"], G_SCRIPT["RibcagePrismTurnMonitor"], scene)
assert n == 1, f"turn monitor guid appeared {n} times"

# 6b. controller script swap + its serialized field block
scene, n = re.subn(EXISTING["RampageController"], G_SCRIPT["RibcageController"], scene)
assert n == 1, f"controller guid appeared {n} times"

OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  aiRetargetSeconds: 1.5
"""
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['RibcageScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  broodReleaseFraction: 0.25
  packReleaseFraction: 0.5
  ladderSampleSeconds: 0.5
  aiRetargetSeconds: 2
  aiCageRadiusOverride: 0
"""
assert OLD_FIELDS in scene, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

# 6c. cell config swap
scene, n = re.subn(EXISTING["RampageCellConfig"], G_ASSET["RibcageCellConfig"], scene)
assert n == 1, f"cell config guid appeared {n} times"

# 6d. Spawn OUTSIDE the cage. The donor's four authored transforms sit at +/-50 - deep inside
# the 300u cage, so players started penned in with the brood. Switch the initializer to the
# computed cell spawn ring (CellSpawnFormation: symmetric, all facing the cell) with a radius
# FLOOR, because this cell has no nucleus for the ring to measure off. 480u sits well outside
# the cage (300) and well inside the membrane (1200), giving a clear run at the bone.
OLD_SPAWN = """  playerSpawnPoints:
  - {fileID: 1468661147}
  - {fileID: 1074736317}
  - {fileID: 1323644424}
  - {fileID: 1564881929}
  preSpawnDelayMs: 200
"""
NEW_SPAWN = f"""  playerSpawnPoints:
  - {{fileID: 1468661147}}
  - {{fileID: 1074736317}}
  - {{fileID: 1323644424}}
  - {{fileID: 1564881929}}
  arrangeSpawnPointsAroundCell: 1
  spawnDistanceOutsideNucleus: 40
  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}
  cellData: {{fileID: 11400000, guid: {EXISTING['RuntimeCellData']}, type: 2}}
  preSpawnDelayMs: 200
"""
assert OLD_SPAWN in scene, "donor spawn-point block not found"
scene = scene.replace(OLD_SPAWN, NEW_SPAWN)

emit("Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity.meta",
     scene_meta(G_ASSET["MinigameRibcage.unity"]))


# ── 7. Register the card in the party-games list ─────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
with open(os.path.join(ROOT, LIST_PATH), encoding="utf-8") as fh:
    games = fh.read()
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameRibcage']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 8. Always-unlocked so the card is clickable on a fresh account ───────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
with open(os.path.join(ROOT, PROG_PATH), encoding="utf-8") as fh:
    prog = fh.read()
if re.search(r"^  alwaysUnlockedModes:\n(  - \d+\n)*  - 39\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 39\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 9. Build settings ────────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
with open(os.path.join(ROOT, BUILD_PATH), encoding="utf-8") as fh:
    build = fh.read()
if "MinigameRibcage.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRampage\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Rampage scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity\n"
                          f"    guid: {G_ASSET['MinigameRibcage.unity']}\n")
emit(BUILD_PATH, build)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

# every GUID we mint must be unique and not already used anywhere in the repo
all_new = list(G_SCRIPT.values()) + list(G_ASSET.values()) + [guid("folder/RibcageCell")]
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

# .meta files THIS script owns are excluded from the collision sweep - otherwise a second
# run flags its own (byte-identical) output as a collision and the script stops being
# idempotent. Everything else in the project is fair game for the uniqueness check.
owned_metas = {os.path.normpath(os.path.join(ROOT, rel)) for rel in files if rel.endswith(".meta")}

existing_guids = set()
for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in filenames:
        if not fn.endswith(".meta"):
            continue
        full = os.path.normpath(os.path.join(dirpath, fn))
        if full in owned_metas:
            continue
        try:
            with open(full, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
            if m:
                existing_guids.add(m.group(1))
        except OSError:
            pass
for g in all_new:
    if g in existing_guids:
        errors.append(f"minted GUID {g} collides with an asset this script does not own")

# every referenced existing GUID must resolve to a real asset
for name, g in EXISTING.items():
    if g not in existing_guids:
        errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")

# the scene must no longer mention the donor's mode-specific guids
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity"]
for name in ("RampageController", "RampagePrismTurnMonitor", "RampageCellConfig", "RampageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("RibcageController", "RibcagePrismTurnMonitor"):
    if G_SCRIPT[name if name in G_SCRIPT else name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["RibcageCellConfig"] not in sc or G_ASSET["RibcageScoringRule"] not in sc:
    errors.append("cloned scene missing Ribcage cell config / scoring rule reference")

# serialized MonoBehaviour keys must exist on the C# class (asset-surgery §3)
def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    for m in re.finditer(r"(?:\[SerializeField\]\s*)?(?:public|protected|private|internal)\s+"
                         r"(?:readonly\s+)?[\w<>,\[\]\?\.]+\s+(\w+)\s*(?:=|;|\{)", src):
        out.add(m.group(1))
    return out

CHECKS = [
    ("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config.asset",
     "Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs"),
    ("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset",
     "Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs"),
    ("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Tadpole Fauna Config Data.asset",
     "Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs"),
    ("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Shark Fauna Config Data.asset",
     "Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs"),
    ("Assets/_SO_Assets/Games/ArcadeGameRibcage.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs"),
]
SO_BASE = {"CellName", "Description", "Icon", "Difficulty", "CellEndGameScore", "Mode",
           "IsMultiplayer", "DisplayName", "IconActive", "IconInactive", "CardBackground",
           "PreviewClip", "GolfScoring", "SceneName"}
for asset_path, cs_path in CHECKS:
    keys = set(re.findall(r"^  (\w+):", files[asset_path], re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = cs_fields(cs_path) | SO_BASE
    # SO_Game base fields live in the parent class
    for extra in ("Assets/_Scripts/ScriptableObjects/SO_Game.cs",):
        if os.path.exists(os.path.join(ROOT, extra)):
            known |= cs_fields(extra)
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on {os.path.basename(cs_path)}: {sorted(unknown)}")

if errors:
    print("VALIDATION FAILED — nothing written:")
    for e in errors:
        print("  ✗", e)
    sys.exit(1)

print(f"Validation passed ({len(files)} files).")
for rel in sorted(files):
    print("  ", rel)

if CHECK_ONLY:
    print("\n--check: no files written.")
    sys.exit(0)

for rel, content in files.items():
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
print(f"\nWrote {len(files)} files.")
