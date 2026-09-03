#!/usr/bin/env python3
"""
Authors every serialized asset the PeelTheCage game mode needs (GameModes.PeelTheCage = 39).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_ribcage_assets.py [--check]

--check validates without writing (CI / pre-commit use).

See Assets/_Scripts/Controller/Arcade/PEEL_THE_CAGE.md for what these numbers mean.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv

# The cage baseline is IMPORTED, never copied: ribcage_budget.py mirrors the C# generator's
# loops exactly, so PhaseThresholds cannot drift from the geometry behind a stale constant.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ribcage_budget as budget  # noqa: E402


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family).

    **The seed is IDENTITY, not a name, and it never changes.** The guid is md5 of this string,
    so editing a seed mints a DIFFERENT guid - and this script would then write .meta files that
    no scene, prefab or asset in the project still points at. Nothing would fail: the assets
    would simply be re-authored as strangers, and every reference to them would resolve to
    nothing.

    That is why the seeds below still read `Ribcage` after the mode was renamed to Peel the Cage
    (the 2026-09 naming pass). The dictionary KEYS and the file PATHS moved with the mode; the
    seeds stayed on the name the assets were minted under. `Tools/Build/rename_game_modes.py`
    excludes this file for the same reason it excludes its own map.
    """
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "SpawnableRibcage":        guid("script/SpawnableRibcage"),
    "PeelTheCageController":       guid("script/RibcageController"),
    "PeelTheCagePrismTurnMonitor": guid("script/RibcagePrismTurnMonitor"),
    "PeelTheCageScoringRuleSO":    guid("script/RibcageScoringRuleSO"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
# One prefab variant and one CellConfigDataSO per intensity - that is how the layered orange
# is authored (Cell picks by CellTypeChoiceOptions.IntensityWise; see Cell.cs AssignConfig).
INTENSITIES = list(range(1, budget.MAX_SHELLS + 1))

G_ASSET = {
    "ArcadeGamePeelTheCage":         guid("asset/ArcadeGameRibcage"),
    "PeelTheCageScoringRule":        guid("asset/RibcageScoringRule"),
    "RibcageSpawnProfile":       guid("asset/RibcageSpawnProfile"),
    "MinigamePeelTheCage.unity":     guid("asset/MinigameRibcage.unity"),
}
for _i in INTENSITIES:
    G_ASSET[f"SpawnableRibcage{_i}.prefab"] = guid(f"asset/SpawnableRibcage{_i}.prefab")
    G_ASSET[f"RibcageCellConfig{_i}"] = guid(f"asset/RibcageCellConfig{_i}")

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

# ── Cage baseline: IMPORTED from ribcage_budget, never copied ────────────────
CAGE_RADIUS = budget.OUTER_R
SPAWN_RING_RADIUS = round(budget.OUTER_R * 1.6)  # outside the cage, inside the 1200u membrane
# Destruction target - the race metric. The 25%/50% milestone rungs are fractions of this,
# so moving it moves the whole progress ladder. Matches Rampage's 2000.
RIBCAGE_PRISM_TARGET = 2000

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
    "PeelTheCageController":       "Assets/_Scripts/Controller/Arcade/PeelTheCageController.cs",
    "PeelTheCagePrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/PeelTheCagePrismTurnMonitor.cs",
    "PeelTheCageScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Scoring/PeelTheCageScoringRuleSO.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. SpawnableRibcage prefabs - ONE VARIANT PER INTENSITY ─────────────────
# The layered orange: variant for intensity i builds SHELLS_FOR_INTENSITY[i] concentric
# rinds inward from the fixed outer shell (2/3/4/5 - intensity 1 is ALREADY layered, since
# one shell cannot reach the ~10k prism budget without closing the outer weave). Same script,
# same seed, only shellCount differs, and BuildParameterHash keeps their caches distinct.
for i in INTENSITIES:
    emit(f"Assets/_Prefabs/Spawnables/SpawnableRibcage{i}.prefab", f"""%YAML 1.1
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
  intensityLevel: {i}
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  shellCount: {budget.shells_for_intensity(i)}
""")
    emit(f"Assets/_Prefabs/Spawnables/SpawnableRibcage{i}.prefab.meta",
         prefab_meta(G_ASSET[f"SpawnableRibcage{i}.prefab"]))


# ── 3. Scoring rule ──────────────────────────────────────────────────────────
emit("Assets/_SO_Assets/Scoring Rules/PeelTheCageScoringRule.asset",
     HEADER_FOR(G_SCRIPT["PeelTheCageScoringRuleSO"], "PeelTheCageScoringRule") +
     "  metric: 5\n  golfRules: 1\n")   # 5 = ScoringMetric.PrismsDestroyed (the race metric)
emit("Assets/_SO_Assets/Scoring Rules/PeelTheCageScoringRule.asset.meta",
     asset_meta(G_ASSET["PeelTheCageScoringRule"]))


# ── 4. Arcade game config ────────────────────────────────────────────────────
emit("Assets/_SO_Assets/Games/ArcadeGamePeelTheCage.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGamePeelTheCage") + f"""  Mode: 39
  IsMultiplayer: 1
  DisplayName: Peel the Cage
  Description: A layered orange of prism bone, and you are the blade. Scrape one rind
    away and the next is waiting behind it - intensity is how many you have to peel.
    Danger bars are salted through the weave, so read before you ram.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  PreviewClip: {{fileID: {PREVIEW_FILEID}, guid: {EXISTING['PreviewClip']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigamePeelTheCage
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
  ComebackRatePerScoreDeficit: 0.01
""")
emit("Assets/_SO_Assets/Games/ArcadeGamePeelTheCage.asset.meta", asset_meta(G_ASSET["ArcadeGamePeelTheCage"]))


# ── 5. Cell configs (ONE PER INTENSITY) + spawn profile ──────────────────────
#
# NO FAUNA. The brood was removed from this level on request (2026-08); the cell keeps its
# membrane / cytoplasm / phase machinery because the Cell owns the environment, but it
# authors no species, so SupportedFaunas is empty and nothing hatches. The platform fauna
# capabilities the old ladder used (Cell.FaunaReleaseTier / FaunaContainmentRadius /
# ModePhaseFloor / SetModeControlOverride, SpawnProfileSO.InitialFaunaReleaseTier) are all
# still there - re-adding the brood is a data change here, not a code change.
#
# Each intensity gets its OWN CellConfigDataSO because PhaseThresholds must ride ITS OWN
# baseline: a five-rind cage starts at ~20.2k prisms and a two-rind cage at ~10.6k, so a
# shared threshold block would put three of the four arenas in the wrong phase from frame
# one. Cell.AssignConfig picks by CellTypeChoiceOptions.IntensityWise (index = intensity-1).
emit("Assets/_SO_Assets/Cell Configs/Ribcage Cell.meta", meta(guid("folder/RibcageCell"), folder=True))

for i in INTENSITIES:
    n, v, danger = budget.cumulative(i)
    th = budget.phase_thresholds(n, v)
    shells = budget.shells_for_intensity(i)
    radii = " / ".join(f"{budget.shell_radius(k):.0f}" for k in range(shells))
    emit(f"Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config {i}.asset",
         HEADER_FOR(EXISTING["CellConfigDataSO"], f"Ribcage Cell Config {i}") + f"""  CellName: PeelTheCage
  Description: The cage cell at intensity {i} - {shells} concentric rinds of prism bone at
    radius {radii}, {n} prisms in total. NO NUCLEUS by design, and no fauna. PhaseThresholds
    ride THIS intensity's own baseline; regenerate with Tools/Build/author_ribcage_assets.py
    after any geometry change rather than hand-editing.
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['Membrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['RibcageSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {{fileID: 5260000000000203, guid: {G_ASSET[f'SpawnableRibcage{i}.prefab']}, type: 3}}
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
    emit(f"Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config {i}.asset.meta",
         asset_meta(G_ASSET[f"RibcageCellConfig{i}"]))

# One spawn profile, shared by all four configs: it authors nothing to spawn.
emit("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset",
     HEADER_FOR(EXISTING["SpawnProfileSO"], "Ribcage Spawn Profile") + """  FloraExcludeLocalDomain: 0
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
emit("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset.meta",
     asset_meta(G_ASSET["RibcageSpawnProfile"]))


# ── 6. Scene: clone MinigameRampage, swap the mode-specific wiring ───────────
DONOR_SCENE = os.path.join(ROOT, "Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity")
with open(DONOR_SCENE, encoding="utf-8") as fh:
    scene = fh.read()

# 6a. turn monitor script swap (field set is identical - base TurnMonitor fields only)
scene, n = re.subn(EXISTING["RampagePrismTurnMonitor"], G_SCRIPT["PeelTheCagePrismTurnMonitor"], scene)
assert n == 1, f"turn monitor guid appeared {n} times"

# 6b. controller script swap + its serialized field block
scene, n = re.subn(EXISTING["RampageController"], G_SCRIPT["PeelTheCageController"], scene)
assert n == 1, f"controller guid appeared {n} times"

OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  aiRetargetSeconds: 1.5
"""
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['PeelTheCageScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  aiRetargetSeconds: 2
  aiCageRadiusOverride: 0
"""
assert OLD_FIELDS in scene, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

# 6c. Cell: swap the donor's single config for the FOUR per-intensity configs and flip the
# choice mode to IntensityWise, which is the platform's own way to vary a cell by intensity
# (Cell.AssignConfig: index = SelectedIntensity - 1, clamped). The donor scene lists exactly
# one config under Random(0); replacing that pair is the whole change.
OLD_CELL = f"""  CellConfigs:
  - {{fileID: 11400000, guid: {EXISTING['RampageCellConfig']}, type: 2}}
  cellTypeChoiceOptions: 0
"""
NEW_CELL = "  CellConfigs:\n" + "".join(
    f"  - {{fileID: 11400000, guid: {G_ASSET[f'RibcageCellConfig{i}']}, type: 2}}\n"
    for i in INTENSITIES) + "  cellTypeChoiceOptions: 1\n"
assert OLD_CELL in scene, "donor Cell config block not found"
scene = scene.replace(OLD_CELL, NEW_CELL)

# 6d. Spawn OUTSIDE the cage. The donor's four authored transforms sit at +/-50 - deep inside
# the cage, so players started penned in with the brood. Switch the initializer to the
# computed cell spawn ring (CellSpawnFormation: symmetric, all facing the cell) with a radius
# FLOOR, because this cell has no nucleus for the ring to measure off. spawnFormation 1 =
# EquatorialRing: everyone on ONE horizontal circle like Joust, so nobody is dropped on a pole -
# a latitude-hoop cage is densest where the ribs converge, and the default tetrahedral spread
# would hand two of four players a much harder approach. SPAWN_RING_RADIUS sits
# well outside the cage (CAGE_RADIUS) and well inside the membrane (1200), giving a clear run
# at the bone.
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
  spawnFormation: 1
  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}
  cellData: {{fileID: 11400000, guid: {EXISTING['RuntimeCellData']}, type: 2}}
  preSpawnDelayMs: 200
"""
assert OLD_SPAWN in scene, "donor spawn-point block not found"
scene = scene.replace(OLD_SPAWN, NEW_SPAWN)

emit("Assets/_Scenes/Multiplayer Scenes/MinigamePeelTheCage.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigamePeelTheCage.unity.meta",
     scene_meta(G_ASSET["MinigamePeelTheCage.unity"]))


# ── 7. Register the card in the party-games list ─────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
with open(os.path.join(ROOT, LIST_PATH), encoding="utf-8") as fh:
    games = fh.read()
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGamePeelTheCage']}, type: 2}}\n"
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
if "MinigamePeelTheCage.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRampage\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Rampage scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigamePeelTheCage.unity\n"
                          f"    guid: {G_ASSET['MinigamePeelTheCage.unity']}\n")
emit(BUILD_PATH, build)


# ── 10. End-game condition target ────────────────────────────────────────────
# The shared overrides asset is what FrogletTools > Game Modes > End Game Conditions edits.
# A missing key would silently fall back to the C# field initializer, so author both the live
# and the build-baseline value explicitly, next to Rampage's (same 2000 destruction target).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
with open(os.path.join(ROOT, END_PATH), encoding="utf-8") as fh:
    endcond = fh.read()
for live_key, new_key in (("rampagePrismTarget", "ribcagePrismTarget"),
                          ("rampagePrismTargetBuild", "ribcagePrismTargetBuild")):
    if f"\n  {new_key}: " in endcond:
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH}"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {RIBCAGE_PRISM_TARGET}\n", 1)
emit(END_PATH, endcond)


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
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigamePeelTheCage.unity"]
for name in ("RampageController", "RampagePrismTurnMonitor", "RampageCellConfig", "RampageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("PeelTheCageController", "PeelTheCagePrismTurnMonitor"):
    if G_SCRIPT[name if name in G_SCRIPT else name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["PeelTheCageScoringRule"] not in sc:
    errors.append("cloned scene missing PeelTheCage scoring rule reference")
for i in INTENSITIES:
    if G_ASSET[f"RibcageCellConfig{i}"] not in sc:
        errors.append(f"cloned scene missing PeelTheCage cell config {i}")
if "  cellTypeChoiceOptions: 1\n" not in sc:
    errors.append("scene Cell is not on CellTypeChoiceOptions.IntensityWise - "
                  "the per-intensity configs would never be selected")

# serialized MonoBehaviour keys must exist on the C# class (asset-surgery §3)
def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    # (1) fields with an explicit access modifier
    for m in re.finditer(r"(?:public|protected|private|internal)\s+"
                         r"(?:readonly\s+)?" + TYPE + r"\s+(\w+)\s*(?:=|;|\{)", src):
        out.add(m.group(1))
    # (2) modifier-less [SerializeField] fields - the house style ("[SerializeField] with
    #     private fields"), including attribute lists like [SerializeField, Range(1, 4)].
    #     Without this the extractor silently reports a field as MISSING and the caller
    #     concludes the C# is wrong when it is the regex that is too narrow.
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    return out

CHECKS = [
    (f"Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Cell Config {i}.asset",
     "Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs") for i in INTENSITIES
] + [
    ("Assets/_SO_Assets/Cell Configs/Ribcage Cell/Ribcage Spawn Profile.asset",
     "Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs"),
    ("Assets/_SO_Assets/Games/ArcadeGamePeelTheCage.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs"),
]
# The prefabs are NOT run through CHECKS: a prefab file carries GameObject/Transform blocks
# and every field inherited from SpawnableBase / CellEnvironmentSpawnableBase, none of which
# live on SpawnableRibcage.cs. Only the one key this script actually introduces is checked.
if "shellCount" not in cs_fields(
        "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableRibcage.cs"):
    errors.append("SpawnableRibcage.cs has no 'shellCount' field - the per-intensity prefab "
                  "variants would all build one shell")
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
