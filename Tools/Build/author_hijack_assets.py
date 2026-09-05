#!/usr/bin/env python3
"""
Authors every serialized asset the Hijack game mode needs (GameModes.Hijack = 45).

Hijack is the Urchin-only heist race through the SWITCHYARD - three great-circle rails ringing a
hollow core, meeting at spiny burrs of raw prism where the rings cross. Unlike Salvo (which reuses
Dog Fight's Boneyard verbatim), this mode brings a genuinely new arena, so this script authors the
whole stack: the spawnable variants, the per-intensity cell configs, the spawn profile, the card,
the scoring rule, the scene, and the registrations.

  - four SpawnableSwitchyard prefab variants (intensity = burr mass, nothing else)
  - four CellConfigDataSOs, each carrying ITS OWN PhaseThresholds
  - one spawn profile that authors NOTHING (no food web - see HIJACK.md "Why no fauna")
  - the arcade card + scoring rule + objective-icon entry for the new metric
  - the scene (cloned from MinigamePeelTheCage, mode wiring swapped)
  - the registrations (game list, progression, build settings, end-condition target)

THE NUMBERS ARE NOT IN THIS FILE. Every count and threshold is imported from
`Tools/Build/hijack_budget.py`, which is a MIRROR of SpawnableSwitchyard.cs rather than an
estimate - the generator is closed form (no System.Random draw anywhere in BuildEnvironment), so
the model reproduces it exactly. That is what makes the cell's PhaseThresholds unable to drift
from the arena that has to satisfy them. Run the budget script directly to see the table and the
geometry proofs.

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_hijack_assets.py [--check]

--check validates without writing (CI / pre-commit use).

See Assets/_Scripts/Controller/Arcade/HIJACK.md for what these numbers mean.
"""
import hashlib
import importlib.util
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv

# The mirror of SpawnableSwitchyard.cs. Imported, never copied.
_spec = importlib.util.spec_from_file_location(
    "hijack_budget", os.path.join(os.path.dirname(os.path.abspath(__file__)), "hijack_budget.py"))
budget = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(budget)


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


INTENSITIES = list(range(1, len(budget.INTENSITIES) + 1))

# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "SpawnableSwitchyard":     guid("script/SpawnableSwitchyard"),
    "HijackYard":              guid("script/HijackYard"),
    "HijackController":        guid("script/HijackController"),
    "HijackObjectiveProvider": guid("script/HijackObjectiveProvider"),
    "HijackScoringRuleSO":     guid("script/HijackScoringRuleSO"),
    "HijackStealTurnMonitor":  guid("script/HijackStealTurnMonitor"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameHijack":       guid("asset/ArcadeGameHijack"),
    "HijackScoringRule":      guid("asset/HijackScoringRule"),
    "SwitchyardSpawnProfile": guid("asset/SwitchyardSpawnProfile"),
    "MinigameHijack.unity":   guid("asset/MinigameHijack.unity"),
    "HIJACK.md":              guid("asset/HIJACK.md"),
}
for _i in INTENSITIES:
    G_ASSET[f"SpawnableSwitchyard{_i}.prefab"] = guid(f"asset/SpawnableSwitchyard{_i}.prefab")
    G_ASSET[f"SwitchyardCellConfig{_i}"] = guid(f"asset/SwitchyardCellConfig{_i}")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    # script types
    "SO_ArcadeGame":    "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO": "01f934d50526431a9392a6ceca1dc33d",
    "SpawnProfileSO":   "e8d8aa5d835249798a256e18f2f7d912",
    # donor scene wiring to swap out (minted deterministically by author_ribcage_assets.py)
    "PeelTheCageController":       guid("script/RibcageController"),
    "PeelTheCagePrismTurnMonitor": guid("script/RibcagePrismTurnMonitor"),
    "PeelTheCageScoringRule":      guid("asset/RibcageScoringRule"),
    # shared content
    "Prism_prefab":     "ed9defc56162b4b4588e61c20984b6d9",
    "Membrane_prefab":  "6e330f85972faf843b8a128e7166f7b5",
    "Cytoplasm_prefab": "9cacd903fcf4643459f5f14ac811bb20",
    "CellIcon":         "6aa1c06e11b265744a5f9fa8858ac72a",
    "Vessel_Urchin":    "bde48fa4833b6364b93111a55ba90958",
    "RuntimeCellData":  "8d4e8398eedc76c4dadb8604f89b9e1b",
    # arcade card art - shared with the other aggression party games
    "IconActive":     "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":   "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground": "587d2203114c8004c9985d0112c89585",
    "PreviewClip":    "4396864d799a6154bb82e5346ac0093b",
    # the objective glyph for the new metric, authored by author_objective_icons.py
    "ObjectiveIconPrismsStolen": "d1145b060398fbc980160019ca18f99d",
}

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
PREVIEW_FILEID = 241334157148977051
SPRITE_FILEID = 21300000

# ── Arena constants: IMPORTED from hijack_budget, never copied ───────────────
SPAWN_RING_RADIUS = int(budget.SPAWN_RING_RADIUS)

# The steal target - the race metric. A domain must flip this many prisms between them.
# Explicitly UNMEASURED (the Salvo precedent): sized against the intensity-1 yard, which holds
# 2,772 prisms of which ~1,848 are hostile to any one domain, for a 3-5 minute race. One editor
# field (FrogletTools > Game Modes > End Game Conditions) is the dial.
# Kept in sync with EndConditionOverridesSO.DefaultHijackStealTarget.
HIJACK_STEAL_TARGET = 1500

# The comeback strength - a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`), which is the
# trap Dog Fight, The Bends and Wildlife Liberation have now all recorded independently: a rate
# inherited from a mode with a different target is silently worth a fraction of an element level.
# At 0.008 a quarter-of-target deficit (375 steals) buys 3.0 levels, matching Wildlife
# Liberation's curve. The assert below fails the build if a retune ever breaks that.
COMEBACK_RATE = 0.008

# One omni crystal idling in the hollow core. NOT the objective - the mode's arrow points at
# burrs (HijackObjectiveProvider) - it is an elemental pickup a pilot may take in passing. The
# cell has NO NUCLEUS, so noNucleusSpawnRadius must be authored or the crystal falls through to
# its own SphereRadius and respawns on the arena's exact centre (the Dog Fight lesson).
OMNI_SPAWN_RADIUS = 300

# ScoreDifferenceSource.PrismsStolen - the LIVE stat the comeback layer reads a deficit from.
#
# This has to be authored INTO THE SCENE, not left to ElementalComebackSystem.DefaultSourceFor:
# EnsureExists respects a scene-authored instance as-is (it only calls Bind), so the per-mode
# default is never consulted in a scene that already carries the component - and the donor's is
# PrismsDestroyed, which in a mode where nothing is destroyed is a flat zero forever. The
# comeback layer would have been silently inert, which is exactly the shape of defect the
# generator's other scene assertions exist to catch.
COMEBACK_SOURCE = 8

# The nucleus-less cell has nothing for the AI's sense radius to measure off.
SENSE_RADIUS = 1200

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
        return (f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n"
                f"  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def prefab_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def text_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nTextScriptImporter:\n  externalObjects: {{}}\n"
            f"  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


# ── 1. .cs.meta for the six new scripts ──────────────────────────────────────
SCRIPT_PATHS = {
    "SpawnableSwitchyard":     "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableSwitchyard.cs",
    "HijackYard":              "Assets/_Scripts/Controller/Environment/MiniGameObjects/HijackYard.cs",
    "HijackController":        "Assets/_Scripts/Controller/Arcade/HijackController.cs",
    "HijackObjectiveProvider": "Assets/_Scripts/Controller/Arcade/HijackObjectiveProvider.cs",
    "HijackScoringRuleSO":     "Assets/_Scripts/Controller/Arcade/Scoring/HijackScoringRuleSO.cs",
    "HijackStealTurnMonitor":  "Assets/_Scripts/Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))

# The mode reference doc is an ASSET too - without a .meta Unity mints a fresh GUID on first
# import, which is a different GUID on every machine and breaks any reference to it.
emit("Assets/_Scripts/Controller/Arcade/HIJACK.md.meta", text_meta(G_ASSET["HIJACK.md"]))


# ── 2. SpawnableSwitchyard prefabs - ONE VARIANT PER INTENSITY ──────────────
# INTENSITY IS BURR MASS AND NOTHING ELSE. The 24-rail network, the ring radius, the launch gaps
# and the spawn ring are identical at every level, so the arena's shape, its aiming and its spawn
# geometry never move - a bigger yard is a LONGER, more contested match at a fixed target, not a
# scarcer one. Same script, same seed, only the two shell counts differ; BuildParameterHash keeps
# their caches distinct.
for i in INTENSITIES:
    big_shells, small_shells = budget.INTENSITIES[i - 1]
    row = budget.budget(big_shells, small_shells)
    emit(f"Assets/_Prefabs/Spawnables/SpawnableSwitchyard{i}.prefab", f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5260000000000301
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5260000000000302}}
  - component: {{fileID: 5260000000000303}}
  m_Layer: 0
  m_Name: SpawnableSwitchyard{i}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5260000000000302
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000301}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5260000000000303
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000301}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableSwitchyard']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {budget.SEED}
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
  ringRadius: {budget.RING_RADIUS:g}
  stationsPerRing: {budget.STATIONS_PER_RING}
  railHalfGapDegrees: {budget.RAIL_HALF_GAP_DEG:g}
  railPrisms: {budget.RAIL_PRISMS}
  prismScale: {{x: {budget.PRISM_SCALE[0]:g}, y: {budget.PRISM_SCALE[1]:g}, z: {budget.PRISM_SCALE[2]:g}}}
  bigBurrShells: {big_shells}
  smallBurrShells: {small_shells}
  shellPitch: {budget.SHELL_PITCH:g}
  yawDegrees: {budget.YAW_DEGREES:g}
""")
    emit(f"Assets/_Prefabs/Spawnables/SpawnableSwitchyard{i}.prefab.meta",
         prefab_meta(G_ASSET[f"SpawnableSwitchyard{i}.prefab"]))


# ── 3. Scoring rule ─────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.PrismsStolen. Golf: the winning domain's pilots carry a finish time,
# everyone else a remaining-steals sentinel, so lower is better - the Rampage shape, which this
# class inherits and overrides only for wording.
emit("Assets/_SO_Assets/Scoring Rules/HijackScoringRule.asset",
     HEADER_FOR(G_SCRIPT["HijackScoringRuleSO"], "HijackScoringRule") +
     "  metric: 9\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/HijackScoringRule.asset.meta",
     asset_meta(G_ASSET["HijackScoringRule"]))


# ── 4. Arcade game config ───────────────────────────────────────────────────
# URCHIN ONLY: a single entry in Vessels drives all three enforcement layers (the launcher clamp,
# the server-side spawn clamp, and the AI clamp).
#
# MinDomainsAllowed 2 because a heist needs somebody to steal FROM: the metric is ownership, and
# a lobby that launched with everyone on one colour would race to flip mass nobody was defending.
emit("Assets/_SO_Assets/Games/ArcadeGameHijack.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameHijack") + f"""  Mode: 45
  IsMultiplayer: 1
  DisplayName: Hijack
  Description: Urchins only. Latch onto a rail and grind it - fast where it wears your
    colour, a stealing crawl where it does not - spike the road ahead to make it yours,
    then fly off the open end straight into the burr it points at and rake the whole
    cluster. Nothing is destroyed here; it only changes hands. First domain to steal
    the target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  PreviewClip: {{fileID: {PREVIEW_FILEID}, guid: {EXISTING['PreviewClip']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameHijack
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Urchin']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: {len(INTENSITIES)}
  CallToActionTargetType: 404
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameHijack.asset.meta", asset_meta(G_ASSET["ArcadeGameHijack"]))


# ── 5. Cell configs (ONE PER INTENSITY) + spawn profile ─────────────────────
#
# NO NUCLEUS: this cell has no control zone and nothing reads DominantDomain here. The mode's
# territory is the mass itself, which is the whole point - a nucleus would add a sanctuary rule
# to an arena whose every prism is meant to be takeable (Docs/ECOSYSTEM.md 25.1).
#
# Each intensity gets its OWN CellConfigDataSO because PhaseThresholds must ride ITS OWN
# baseline: the yard opens at 2,772 prisms at intensity 1 and 9,930 at 4, so a shared threshold
# block would put three of the four arenas in the wrong phase from frame one. Cell.AssignConfig
# picks by CellTypeChoiceOptions.IntensityWise (index = intensity - 1).
emit("Assets/_SO_Assets/Cell Configs/Switchyard Cell.meta", meta(guid("folder/SwitchyardCell"), folder=True))

for i in INTENSITIES:
    big_shells, small_shells = budget.INTENSITIES[i - 1]
    row = budget.budget(big_shells, small_shells)
    th = budget.phase_thresholds(row["total"], row["volume"])
    emit(f"Assets/_SO_Assets/Cell Configs/Switchyard Cell/Switchyard Cell Config {i}.asset",
         HEADER_FOR(EXISTING["CellConfigDataSO"], f"Switchyard Cell Config {i}") + f"""  CellName: Switchyard
  Description: The Switchyard at intensity {i} - 24 rails of {budget.RAIL_PRISMS} prisms on three
    great circles of radius {budget.RING_RADIUS:g}, plus 6 big burrs of {row['big_each']} and 12 small burrs of
    {row['small_each']}, {row['total']} prisms in total. NO NUCLEUS and no food web by design.
    PhaseThresholds ride THIS intensity's own baseline; regenerate with
    Tools/Build/author_hijack_assets.py after any geometry change rather than hand-editing.
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['Membrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['SwitchyardSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {{fileID: 5260000000000303, guid: {G_ASSET[f'SpawnableSwitchyard{i}.prefab']}, type: 3}}
  EnvironmentIntensity: {i}
  SenseRadiusOverride: {SENSE_RADIUS}
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
    emit(f"Assets/_SO_Assets/Cell Configs/Switchyard Cell/Switchyard Cell Config {i}.asset.meta",
         asset_meta(G_ASSET[f"SwitchyardCellConfig{i}"]))

# One spawn profile, shared by all four configs: it authors NOTHING to spawn.
#
# The reason is the COMEBACK, and it is worth stating rather than treating as an omission. In a
# nucleus-less cell herbivores eat OPPOSING-domain mass, and the leader's colour is by definition
# the most abundant - so a swarm would preferentially eat whatever the TRAILING team had just
# stolen. An anti-comeback current is the wrong current in a mode whose whole economy is
# contested ownership. This asset is the one-file door if it is ever wanted.
emit("Assets/_SO_Assets/Cell Configs/Switchyard Cell/Switchyard Spawn Profile.asset",
     HEADER_FOR(EXISTING["SpawnProfileSO"], "Switchyard Spawn Profile") + """  FloraExcludeLocalDomain: 0
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
emit("Assets/_SO_Assets/Cell Configs/Switchyard Cell/Switchyard Spawn Profile.asset.meta",
     asset_meta(G_ASSET["SwitchyardSpawnProfile"]))


# ── 6. Scene: clone MinigamePeelTheCage, swap the mode-specific wiring ──────
# The donor is the closest structural match in the project: a nucleus-less arena cell on
# IntensityWise configs, players spawned on a computed EQUATORIAL ring outside the structure
# (which is what this mode wants too - the yard's rails ring the core, so a tetrahedral spread
# would drop two of four players on a pole where no rail passes), four AI templates, and one
# omni crystal. The clone swaps the mode identity, the arena, the hull and the spawn radius.
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigamePeelTheCage.unity")

# 6a. turn monitor script swap (field set is identical - base TurnMonitor fields only)
scene, n = re.subn(EXISTING["PeelTheCagePrismTurnMonitor"], G_SCRIPT["HijackStealTurnMonitor"], scene)
assert n == 1, f"turn monitor guid appeared {n} times"

# 6b. controller script swap + its serialized field block
scene, n = re.subn(EXISTING["PeelTheCageController"], G_SCRIPT["HijackController"], scene)
assert n == 1, f"controller guid appeared {n} times"

OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['PeelTheCageScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  aiRetargetSeconds: 2
  aiCageRadiusOverride: 0
"""
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['HijackScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  aiRetargetSeconds: 3
  aiRailApproachLead: 60
  aiRailCommitDistance: 80
  aiThroughDistance: 200
  aiSpikeIntervalSeconds: 2
  aiMinSpikeAmmo: 0.15
  aiStuckSeconds: 6
  aiParkedSpeed: 6
"""
assert OLD_FIELDS in scene, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

# 6c. The ARENA: swap the donor's five cage configs for the four Switchyard ones. The choice
# mode is already IntensityWise, which is the platform's own way to vary a cell by intensity.
old_cell = re.search(r"  CellConfigs:\n(?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+"
                     r"  cellTypeChoiceOptions: 1\n", scene)
assert old_cell, "donor Cell config block not found"
NEW_CELL = "  CellConfigs:\n" + "".join(
    f"  - {{fileID: 11400000, guid: {G_ASSET[f'SwitchyardCellConfig{i}']}, type: 2}}\n"
    for i in INTENSITIES) + "  cellTypeChoiceOptions: 1\n"
scene = scene.replace(old_cell.group(0), NEW_CELL)

# 6d. Spawn ring: OUTSIDE the yard. The donor's floor is sized for its cage; this arena's
# outermost mass reaches ~985u, so the ring goes to the budget model's own SPAWN_RING_RADIUS
# (1120) - clear of the rails and still inside the 1200u membrane. Both numbers are asserted
# against each other by hijack_budget.prove_extent().
scene, n = re.subn(r"  spawnRingRadiusFloor: \d+\n",
                   f"  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}\n", scene)
assert n == 1, f"spawnRingRadiusFloor appeared {n} times"

# 6e. THE HULL: four AI templates, Rhino (3) -> Urchin (4). The platform clamps humans to the
# card's single Vessels entry, but the AI's class is scene-authored, so this is the third
# enforcement layer's data (ServerPlayerVesselInitializerWithAI re-clamps it anyway).
scene, n = re.subn(r"  - vesselClass: 3\n", "  - vesselClass: 4\n", scene)
assert n == 4, f"expected 4 AI vessel templates, found {n}"

# 6f. The core crystal. This cell has NO NUCLEUS, so without an explicit radius every omni
# crystal falls through to its own SphereRadius and respawns on the arena's exact centre - the
# defect Dog Fight recorded, where a big faceted sphere at the origin was mistaken for the
# objective. One crystal, loose in the hollow core, is an elemental pickup and nothing more.
scene, n = re.subn(r"  noNucleusSpawnRadius: \d+\n",
                   f"  noNucleusSpawnRadius: {OMNI_SPAWN_RADIUS}\n", scene)
assert n == 1, f"noNucleusSpawnRadius appeared {n} times"

# 6g. The comeback's LIVE STAT. See COMEBACK_SOURCE - the donor's PrismsDestroyed is a flat
# zero in a mode that destroys nothing, so the whole comeback layer would never fire.
scene, n = re.subn(r"  differenceSource: \d+\n",
                   f"  differenceSource: {COMEBACK_SOURCE}\n", scene)
assert n == 1, f"differenceSource appeared {n} times"

emit("Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity.meta",
     scene_meta(G_ASSET["MinigameHijack.unity"]))


# ── 7. The objective glyph for the new metric ───────────────────────────────
# A new METRIC is the only thing that ever needs new objective art (ObjectiveIconSetSO is keyed
# on ScoringMetric, never on the mode). SET semantics, so a re-run repairs a hand-edit.
ICON_PATH = "Assets/Resources/ObjectiveIconSet.asset"
icons = read(ICON_PATH)
ENTRY = (f"  - metric: 9\n"
         f"    icon: {{fileID: {SPRITE_FILEID}, guid: {EXISTING['ObjectiveIconPrismsStolen']}, type: 3}}\n"
         f"    label: Steal prisms\n")
existing_entry = re.search(r"  - metric: 9\n(?:    .*\n)*", icons)
if existing_entry:
    icons = icons.replace(existing_entry.group(0), ENTRY, 1)
else:
    assert icons.endswith("\n")
    icons = icons + ENTRY
emit(ICON_PATH, icons)


# ── 8. The launch panel's own metric icon ──────────────────────────────────
# A SECOND metric -> sprite table, read by the arcade card's objective box and its micro toast
# (ModeControlsLibrarySO.IconForMetric). It is separate from ObjectiveIconSet, which the in-game
# goal stack reads, and a metric missing from it "draws text alone, which is honest rather than
# broken" - but the editor's Mode Map window flags the gap, and this metric has purpose-drawn art
# already. Points at the same glyph as the goal row, so the card and the HUD cannot disagree
# about what the objective looks like. SET semantics, so a re-run repairs a hand-edit.
CONTROLS_PATH = "Assets/Resources/ModeControlsLibrary.asset"
controls = read(CONTROLS_PATH)
METRIC_ICON = (f"  - Metric: 9\n"
               f"    Icon: {{fileID: {SPRITE_FILEID}, guid: {EXISTING['ObjectiveIconPrismsStolen']}, type: 3}}\n")
existing_icon = re.search(r"  - Metric: 9\n    Icon: \{[^}]*\}\n", controls)
if existing_icon:
    controls = controls.replace(existing_icon.group(0), METRIC_ICON, 1)
else:
    anchor = re.search(r"(  ObjectiveIcons:\n(?:  - Metric: \d+\n    Icon: \{[^}]*\}\n)+)", controls)
    assert anchor, "ObjectiveIcons block not found in ModeControlsLibrary.asset"
    controls = controls.replace(anchor.group(1), anchor.group(1) + METRIC_ICON, 1)
emit(CONTROLS_PATH, controls)


# ── 9. Register the card in the party-games list ────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameHijack']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 10. Always-unlocked so the card is clickable on a fresh account ──────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 45\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 45\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 11. Build settings ──────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameHijack.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigamePeelTheCage\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "PeelTheCage scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity\n"
                          f"    guid: {G_ASSET['MinigameHijack.unity']}\n")
emit(BUILD_PATH, build)


# ── 12. End-game condition target ───────────────────────────────────────────
# SET semantics, not add-if-absent (the Dog Fight generator's lesson: an insert-only key left
# the asset on a stale number after a target retune).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for anchor_key, new_key in (("salvoPrismTarget", "hijackStealTarget"),
                            ("salvoPrismTargetBuild", "hijackStealTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {HIJACK_STEAL_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {anchor_key}: (\d+)\n", endcond, re.M)
    assert m, f"{anchor_key} not found in {END_PATH} - run author_salvo_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {HIJACK_STEAL_TARGET}\n", 1)
emit(END_PATH, endcond)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

all_new = list(G_SCRIPT.values()) + list(G_ASSET.values())
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

# .meta files THIS script owns are excluded from the collision sweep - otherwise a second run
# flags its own (byte-identical) output as a collision and the script stops being idempotent.
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

for name, g in EXISTING.items():
    if g not in existing_guids:
        errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")

# the scene must no longer mention the donor's mode-specific guids
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity"]
for name in ("PeelTheCageController", "PeelTheCagePrismTurnMonitor", "PeelTheCageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("HijackController", "HijackStealTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["HijackScoringRule"] not in sc:
    errors.append("cloned scene missing the scoring rule reference")
if "  cellTypeChoiceOptions: 1\n" not in sc:
    errors.append("scene Cell is not on CellTypeChoiceOptions.IntensityWise")
for i in INTENSITIES:
    if G_ASSET[f"SwitchyardCellConfig{i}"] not in sc:
        errors.append(f"scene does not reference Switchyard Cell Config {i}")
if sc.count("vesselClass: 4") != 4:
    errors.append("scene does not author 4 Urchin AI templates")
if "vesselClass: 3" in sc:
    errors.append("scene still authors a Rhino AI template")
if f"  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}\n" not in sc:
    errors.append("scene does not author the Switchyard spawn ring radius")
if "  spawnFormation: 1\n" not in sc:
    errors.append("scene is not on CellSpawnFormation.EquatorialRing - the yard's rails ring "
                  "the core, so a polar spawn slot faces no rail")
if f"  differenceSource: {COMEBACK_SOURCE}\n" not in sc:
    errors.append("scene does not author ScoreDifferenceSource.PrismsStolen - a scene-authored "
                  "ElementalComebackSystem is respected as-is, so the donor's PrismsDestroyed "
                  "would leave the comeback layer reading a flat zero deficit all match")
if f"  noNucleusSpawnRadius: {OMNI_SPAWN_RADIUS}\n" not in sc:
    errors.append("scene lost noNucleusSpawnRadius - this cell has no nucleus, so the omni "
                  "crystal would respawn on the arena's exact centre")

# The arena must fit inside the spawn ring, which must fit inside the membrane. This is the
# same assertion hijack_budget.prove_extent() makes; repeated here because the scene's spawn
# radius is authored HERE and the two must agree.
_extent = budget.prove_extent()
if _extent >= SPAWN_RING_RADIUS:
    errors.append(f"the yard reaches {_extent:.0f}u but players spawn at {SPAWN_RING_RADIUS}u")
if SPAWN_RING_RADIUS >= budget.MEMBRANE_RADIUS:
    errors.append(f"the spawn ring {SPAWN_RING_RADIUS}u is outside the "
                  f"{budget.MEMBRANE_RADIUS:.0f}u membrane")

ctrl = files[CONTROLS_PATH]
if ctrl.count("  - Metric: 9\n") != 1:
    errors.append("ModeControlsLibrary must carry exactly one Metric 9 objective icon - the "
                  "arcade card's objective box reads it")

# Urchin-only must be a SINGLE entry, or the clamps let another hull through
arcade = files["Assets/_SO_Assets/Games/ArcadeGameHijack.asset"]
vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", arcade, re.M)
if not vessels or vessels.group(1).count("- {fileID") != 1:
    errors.append("ArcadeGameHijack must author EXACTLY ONE vessel (Urchin)")
elif EXISTING["Vessel_Urchin"] not in vessels.group(1):
    errors.append("ArcadeGameHijack's single vessel is not Urchin")
if "MinDomainsAllowed: 2" not in arcade:
    errors.append("ArcadeGameHijack must require at least TWO domains - a one-domain lobby has "
                  "nobody to steal from")

# The comeback rate only means anything relative to the TARGET. Dog Fight, The Bends and
# Wildlife Liberation have each been bitten by a rate inherited across a re-target; this is the
# gate that stops it happening a fourth time.
_quarter_deficit_levels = (HIJACK_STEAL_TARGET * 0.25) * COMEBACK_RATE
if _quarter_deficit_levels < 1.0:
    errors.append(f"ComebackRatePerScoreDeficit {COMEBACK_RATE} is too small for a "
                  f"{HIJACK_STEAL_TARGET}-steal target: a quarter-of-target deficit buys only "
                  f"{_quarter_deficit_levels:.2f} element levels, which is invisible")

# The objective glyph must exist on disk - the entry this script writes into ObjectiveIconSet
# points at a sprite, and a dangling reference draws nothing with no error.
_icon_png = os.path.join(ROOT, "Assets/_Graphics/UI/Objectives/objective_prisms_stolen.png")
if not os.path.exists(_icon_png):
    errors.append("objective_prisms_stolen.png is missing - run "
                  "Tools/Build/author_objective_icons.py first")


# serialized MonoBehaviour keys must exist on the C# class (asset-surgery 3)
def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    MODS = (r"(?:(?:public|protected|private|internal|static|const|readonly|new|virtual|"
            r"override|abstract|sealed|partial)\s+)+")
    # No "(" alternative: that matches METHOD declarations, and a method name in the
    # known set is a name the asset may then use for a field that does not exist.
    for m in re.finditer(MODS + TYPE + r"\s+(\w+)\s*(?:=|;|\{|=>)", src):
        out.add(m.group(1))
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    for m in re.finditer(r"^\s{4,}" + TYPE + r"\s+(\w+)\s*\{\s*get;", src, re.M):
        out.add(m.group(1))
    return out


SO_BASE = {"Mode", "IsMultiplayer", "DisplayName", "Description", "IconActive", "IconInactive",
           "CardBackground", "PreviewClip", "GolfScoring", "SceneName"}

# (asset, its own class, the BASE classes whose fields it also inherits).
#
# The extras are scoped PER ASSET rather than unioned across all of them. A shared `known` set
# is a checker that cannot fail: pooling SO_Game + ScoringRuleSO + RampageScoringRuleSO into
# every check let 25 foreign names through on a cell config alone - including a scoring rule's
# METHOD names, which the field regex also matches. The whole point of this check is that a key
# the class does not have is silently dropped by Unity, and a permissive `known` set is
# indistinguishable from not checking at all.
CHECKS = [
    ("Assets/_SO_Assets/Games/ArcadeGameHijack.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs",
     ["Assets/_Scripts/ScriptableObjects/SO_Game.cs"]),
    ("Assets/_SO_Assets/Scoring Rules/HijackScoringRule.asset",
     "Assets/_Scripts/Controller/Arcade/Scoring/HijackScoringRuleSO.cs",
     ["Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs",
      "Assets/_Scripts/Controller/Arcade/Scoring/RampageScoringRuleSO.cs"]),
    ("Assets/_SO_Assets/Cell Configs/Switchyard Cell/Switchyard Cell Config 1.asset",
     "Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs", []),
    ("Assets/_SO_Assets/Cell Configs/Switchyard Cell/Switchyard Spawn Profile.asset",
     "Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs", []),
]
for asset_path, cs_path, extras in CHECKS:
    if not os.path.exists(os.path.join(ROOT, cs_path)):
        errors.append(f"cannot validate {os.path.basename(asset_path)}: {cs_path} not found")
        continue
    keys = set(re.findall(r"^  (\w+):", files[asset_path], re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = cs_fields(cs_path)
    if asset_path.endswith("ArcadeGameHijack.asset"):
        known |= SO_BASE
    for extra in extras:
        if not os.path.exists(os.path.join(ROOT, extra)):
            errors.append(f"cannot validate {os.path.basename(asset_path)}: {extra} not found")
            continue
        known |= cs_fields(extra)
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on "
                      f"{os.path.basename(cs_path)}: {sorted(unknown)}")

# every serialized key the spawnable prefabs author must exist on SpawnableSwitchyard.cs (or
# one of its bases) - a key the class does not have is silently dropped by Unity, which is how
# a "tuned" arena ships at its defaults.
SPAWNABLE_KEYS = {"ringRadius", "stationsPerRing", "railHalfGapDegrees", "railPrisms",
                  "prismScale", "bigBurrShells", "smallBurrShells", "shellPitch", "yawDegrees"}
BASE_KEYS = {"seed", "domain", "children", "leafPrefab", "layAcrossFrames", "layBudgetMsPerFrame",
             "intensityLevel", "prism", "density", "spawnClearRadius", "spawnClearPoints"}
spawnable_known = cs_fields(SCRIPT_PATHS["SpawnableSwitchyard"])
for base in ("Assets/_Scripts/Controller/Environment/Spawning/CellEnvironmentSpawnableBase.cs",
             "Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs"):
    spawnable_known |= cs_fields(base)
missing = (SPAWNABLE_KEYS | BASE_KEYS) - spawnable_known
if missing:
    errors.append(f"SpawnableSwitchyard.cs is missing serialized fields the prefabs author: "
                  f"{sorted(missing)}")

# every serialized key the scene's controller block authors must exist on HijackController.cs
controller_keys = {"rule", "arenaCell", "aiRetargetSeconds", "aiRailApproachLead",
                   "aiRailCommitDistance", "aiThroughDistance", "aiSpikeIntervalSeconds",
                   "aiMinSpikeAmmo", "aiStuckSeconds", "aiParkedSpeed"}
missing = controller_keys - cs_fields(SCRIPT_PATHS["HijackController"])
if missing:
    errors.append(f"HijackController.cs is missing serialized fields the scene authors: "
                  f"{sorted(missing)}")

# the C# default target and this script's must agree, or the tool window's "(default)" lies
endcond_cs = read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultHijackStealTarget = (\d+);", endcond_cs)
if not m:
    errors.append("EndConditionOverridesSO.cs has no DefaultHijackStealTarget")
elif int(m.group(1)) != HIJACK_STEAL_TARGET:
    errors.append(f"DefaultHijackStealTarget ({m.group(1)}) != this script's "
                  f"HIJACK_STEAL_TARGET ({HIJACK_STEAL_TARGET}) - the two must move together")

# The comeback source ordinal must match the C# enum, or the scene points at another stat.
comeback_cs = read("Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs")
m = re.search(r"PrismsStolen = (\d+),", comeback_cs)
if not m:
    errors.append("ElementalComebackSystem.ScoreDifferenceSource.PrismsStolen has no explicit "
                  "value - the scene serializes an ordinal and cannot be checked against it")
elif int(m.group(1)) != COMEBACK_SOURCE:
    errors.append(f"ScoreDifferenceSource.PrismsStolen is {m.group(1)} but the scene authors "
                  f"{COMEBACK_SOURCE} - the comeback would read a different stat")

# GameModes.Hijack and ScoringMetric.PrismsStolen must exist with the values authored here
gamemodes_cs = read("Assets/_Scripts/Data/Enums/GameModes.cs")
if not re.search(r"^\s*Hijack = 45,", gamemodes_cs, re.M):
    errors.append("GameModes.cs has no 'Hijack = 45' - the card would launch nothing")
metric_cs = read("Assets/_Scripts/Data/Enums/ScoringMetric.cs")
if not re.search(r"^\s*PrismsStolen = 9,", metric_cs, re.M):
    errors.append("ScoringMetric.cs has no 'PrismsStolen = 9' - the scoring rule authors metric 9")

if errors:
    print("VALIDATION FAILED - nothing written:")
    for e in errors:
        print("  x", e)
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
