#!/usr/bin/env python3
"""
Authors every serialized asset the Drumfire game mode needs (GameModes.Drumfire = 45).

Drumfire is the Dolphin-only rhythm range: a great porous DRUM of prisms at the cell centre,
and one firing lane per pilot - a line of evenly spaced crystals struck through their own spawn
slot that passes the drum instead of running into it. The Dolphin's jaw blast is armed by
skimming and fired by touching a crystal, so the lane is the trigger track and the drum is
always off to one side: fly, aim, shoot, repeat. TIME ends it, VOLUME destroyed is the score.

What this script authors:

  - the arcade card + scoring rule (ScoringMetric.VolumeDestroyed, points not golf)
  - the DRUM: a SpawnableDrum prefab, plus the cell config and spawn profile that carry it
    (no nucleus - the drum IS this cell's core; no flora, no fauna - a clean range)
  - the scene, cloned from MinigameRampage (the other Dolphin-only mode, and already wired for
    a cell-relative spawn ring, Dolphin AI templates and a crystal manager) with the mode
    identity, the arena and the crystal LANES swapped in
  - the registrations (game list, progression, build settings, match clock)

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output. Validates the whole result in memory and only then writes.

    python3 Tools/Build/author_drumfire_assets.py [--check]

The arena's numbers are MEASURED, not guessed - Tools/Build/drumfire_arena.py counts the drum
and checks the lane geometry, and this script asserts the two agree. See
Assets/_Scripts/Controller/Arcade/DRUMFIRE.md.
"""
import hashlib
import math
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
    "DrumfireController":       guid("script/DrumfireController"),
    "DrumfireTimeTurnMonitor":  guid("script/DrumfireTimeTurnMonitor"),
    "DrumfireScoringRuleSO":    guid("script/DrumfireScoringRuleSO"),
    "SpawnableDrum":            guid("script/SpawnableDrum"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameDrumfire":       guid("asset/ArcadeGameDrumfire"),
    "DrumfireScoringRule":      guid("asset/DrumfireScoringRule"),
    "MinigameDrumfire.unity":   guid("asset/MinigameDrumfire.unity"),
    "SpawnableDrum.prefab":     guid("asset/SpawnableDrum.prefab"),
    "DrumfireCellConfig":       guid("asset/DrumfireCellConfig"),
    "DrumfireSpawnProfile":     guid("asset/DrumfireSpawnProfile"),
    "DrumfireCellFolder":       guid("folder/DrumfireCell"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":            "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":         "01f934d50526431a9392a6ceca1dc33d",
    "SpawnProfileSO":           "e8d8aa5d835249798a256e18f2f7d912",
    # donor scene wiring to swap out
    "RampageController":        "e11ff862e6844a89a951292673243625",
    "RampagePrismTurnMonitor":  "694b571734fe4a55a57f6cc672c7fcc2",
    "RampageScoringRule":       "7d1bfbd4091c4a12a12c730553bf293a",
    # shared content
    "Vessel_Dolphin":           "c0f30e9f09616874780edc0a375ce686",
    "CapsuleMembrane":          "6e330f85972faf843b8a128e7166f7b5",
    "Cytoplasm":                "9cacd903fcf4643459f5f14ac811bb20",
    "CellIcon":                 "6aa1c06e11b265744a5f9fa8858ac72a",
    "EnvironmentPrism":         "ed9defc56162b4b4588e61c20984b6d9",
    # arcade card art - shared with the other aggression party games
    "IconActive":               "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":             "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":           "587d2203114c8004c9985d0112c89585",
    "PreviewClip":              "4396864d799a6154bb82e5346ac0093b",
}

PREVIEW_FILEID = 241334157148977051
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
PRISM_FILEID = 4563009547826722997

# The prefab's own internal fileIDs, matching the Spawnables family (SpawnableOrrery,
# SpawnableRibcage1..5 all use this triple).
PF_GO, PF_TR, PF_MB = 5230000000000101, 5230000000000102, 5230000000000103

# ── The DRUM, and the LANE. Both measured by Tools/Build/drumfire_arena.py ───
DRUM_SEED = 45
DRUM_OUTER_RADIUS = 320
DRUM_SHELLS = 5
DRUM_OUTER_POINTS = 14074
DRUM_GAP_THRESHOLD = 0.25
DRUM_GAP_FREQ = 0.012
DRUM_PANE = (8, 8, 0.7)
DRUM_RIBS, DRUM_PANES_PER_RIB, DRUM_RIB_PANE = 3, 72, (14, 5, 2.4)
DRUM_CORE_PANES, DRUM_CORE_RADIUS, DRUM_CORE_PANE = 24, 34, (9, 9, 3)
DRUM_STUDS, DRUM_STUD = 120, (7, 7, 5)

# Measured baseline (drumfire_arena.py). PhaseThresholds ride it plus the Blob deltas, per
# Docs/ECOSYSTEM.md section 18 - the ladder is inert in a cell with no flora and no fauna, but
# an environment-bearing config that boots straight into Frenzy is a trap for the next mode
# that copies this one.
DRUM_BASELINE_COUNT = 28350
DRUM_BASELINE_VOLUME = 1373051
BLOB_DELTAS_COUNT = (700, 500, 3600, 3000)
BLOB_DELTAS_VOLUME = (11200, 8000, 57600, 48000)

SPAWN_RING_RADIUS = 1120        # ServerPlayerVesselInitializer.spawnRingRadiusFloor
LANE_OFFSET = 420               # closest approach of a lane to the cell centre
LANE_LEAD = 640                 # spawn -> first crystal
LANE_LENGTH = 800               # first crystal -> last
SLOTS_BY_INTENSITY = (5, 6, 7, 8)   # MORE crystals is a TIGHTER rhythm, so it climbs
MATCH_SECONDS = 75              # EndConditionOverridesSO.DefaultDrumfireSeconds

# The comeback rate is a FUNCTION OF THE SCORE SCALE - the trap Dog Fight, The Bends and
# Wildlife Liberation each hit from a different direction (`bonusLevels = deficit x rate`).
# Drumfire's deficits are measured in VOLUME and run five to six figures where every other
# mode's run two or three, so the rate is correspondingly tiny. Sized so a deficit of a
# quarter of one typical winning score buys ~2 element levels; asserted below.
TYPICAL_WINNING_VOLUME = 300000
COMEBACK_RATE = 0.000027

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


def meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def prefab_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def folder_meta(g: str) -> str:
    # A directory under Assets/ is itself an asset. Without this Unity mints a fresh guid on
    # every machine and the folder shows as an untracked change forever (asset-surgery 3).
    return (f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n"
            f"  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def v3(t):
    return "{x: %g, y: %g, z: %g}" % t


# ── 1. .cs.meta for the new scripts ─────────────────────────────────────────
SCRIPT_PATHS = {
    "DrumfireController":      "Assets/_Scripts/Controller/Arcade/DrumfireController.cs",
    "DrumfireTimeTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/DrumfireTimeTurnMonitor.cs",
    "DrumfireScoringRuleSO":   "Assets/_Scripts/Controller/Arcade/Scoring/DrumfireScoringRuleSO.cs",
    "SpawnableDrum":           "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableDrum.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. Scoring rule ─────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.VolumeDestroyed. golfRules 0: this is a POINTS mode, most volume
# wins, so the raw metric is already the ranking and no sentinel encoding is needed.
emit("Assets/_SO_Assets/Scoring Rules/DrumfireScoringRule.asset",
     HEADER_FOR(G_SCRIPT["DrumfireScoringRuleSO"], "DrumfireScoringRule") +
     "  metric: 9\n  golfRules: 0\n")
emit("Assets/_SO_Assets/Scoring Rules/DrumfireScoringRule.asset.meta",
     asset_meta(G_ASSET["DrumfireScoringRule"]))


# ── 3. The DRUM prefab ──────────────────────────────────────────────────────
# A flat copy of the Spawnables family's shape (GameObject + Transform + the generator
# MonoBehaviour), exactly like SpawnableOrrery and the five SpawnableRibcage variants.
# domain: 3 = Domains.Blue - the "no team" sentinel, which StatsManager treats as hostile to
# every domain, so every pilot is shooting at the same target.
emit("Assets/_Prefabs/Spawnables/SpawnableDrum.prefab", f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{PF_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {PF_TR}}}
  - component: {{fileID: {PF_MB}}}
  m_Layer: 0
  m_Name: SpawnableDrum
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{PF_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {PF_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{PF_MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {PF_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableDrum']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {DRUM_SEED}
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: 1
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['EnvironmentPrism']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  outerRadius: {DRUM_OUTER_RADIUS}
  shellCount: {DRUM_SHELLS}
  outerShellPoints: {DRUM_OUTER_POINTS}
  gapThreshold: {DRUM_GAP_THRESHOLD}
  gapNoiseFrequency: {DRUM_GAP_FREQ}
  paneSize: {v3(DRUM_PANE)}
  ribCount: {DRUM_RIBS}
  panesPerRib: {DRUM_PANES_PER_RIB}
  ribPaneSize: {v3(DRUM_RIB_PANE)}
  corePanes: {DRUM_CORE_PANES}
  coreRadius: {DRUM_CORE_RADIUS}
  corePaneSize: {v3(DRUM_CORE_PANE)}
  dangerStuds: {DRUM_STUDS}
  studSize: {v3(DRUM_STUD)}
""")
emit("Assets/_Prefabs/Spawnables/SpawnableDrum.prefab.meta",
     prefab_meta(G_ASSET["SpawnableDrum.prefab"]))


# ── 4. The cell: a clean range with the drum as its core ────────────────────
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Drumfire Cell"
emit(CELL_DIR + ".meta", folder_meta(G_ASSET["DrumfireCellFolder"]))

# NO flora and NO fauna: Drumfire is a firing range, and every prism a pilot destroys should be
# the drum. It also keeps the whole collider budget in one measured place.
emit(f"{CELL_DIR}/Drumfire Spawn Profile.asset",
     HEADER_FOR(EXISTING["SpawnProfileSO"], "Drumfire Spawn Profile") + """  FloraExcludeLocalDomain: 1
  FloraSpawnVolumeCeiling: 4000
  FloraInitialDelaySeconds: 0.5
  FloraSpawnIntervalSeconds: 5
  SupportedFloras: []
  FaunaExcludeLocalDomain: 1
  InitialFaunaSpawnWaitTime: 10
  FaunaSpawnVolumeThreshold: 5000
  BaseFaunaSpawnTime: 30
  FaunaInitialDelaySeconds: 0.5
  FaunaSpawnIntervalSeconds: 5
  SupportedFaunas: []
""")
emit(f"{CELL_DIR}/Drumfire Spawn Profile.asset.meta", asset_meta(G_ASSET["DrumfireSpawnProfile"]))

TH = {
    "RestlessEnter": DRUM_BASELINE_COUNT + BLOB_DELTAS_COUNT[0],
    "RestlessExit": DRUM_BASELINE_COUNT + BLOB_DELTAS_COUNT[1],
    "FrenzyEnter": DRUM_BASELINE_COUNT + BLOB_DELTAS_COUNT[2],
    "FrenzyExit": DRUM_BASELINE_COUNT + BLOB_DELTAS_COUNT[3],
    "RestlessEnterVolume": DRUM_BASELINE_VOLUME + BLOB_DELTAS_VOLUME[0],
    "RestlessExitVolume": DRUM_BASELINE_VOLUME + BLOB_DELTAS_VOLUME[1],
    "FrenzyEnterVolume": DRUM_BASELINE_VOLUME + BLOB_DELTAS_VOLUME[2],
    "FrenzyExitVolume": DRUM_BASELINE_VOLUME + BLOB_DELTAS_VOLUME[3],
}

# NucleusPrefab is deliberately EMPTY. The drum IS this cell's core, and a nucleus would be a
# second sphere in the same place - plus the nucleus is the platform's crystal respawn volume
# (CLAUDE.md), which Drumfire replaces with per-player lanes. A cell with no nucleus is the
# documented case PeelTheCage and the Boneyard already occupy; the spawn ring reads
# ServerPlayerVesselInitializer.spawnRingRadiusFloor instead of a nucleus radius.
emit(f"{CELL_DIR}/Drumfire Cell Config.asset",
     HEADER_FOR(EXISTING["CellConfigDataSO"], "Drumfire Cell Config") + f"""  CellName: Drumfire
  Description: A firing range. One great porous drum of prisms hangs in the middle -
    about 28,350 panes over five nested shells, braced with shielded ribs, studded with
    danger, and built around a super-shielded core that no blast can touch so the drum
    always leaves a marker. No nucleus (the drum is the core), no flora and no fauna
    (every prism a pilot destroys should be the drum). Numbers are measured by
    Tools/Build/drumfire_arena.py - regenerate with author_drumfire_assets.py rather
    than hand-editing.
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: 1
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['DrumfireSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {{fileID: {PF_MB}, guid: {G_ASSET['SpawnableDrum.prefab']}, type: 3}}
  EnvironmentIntensity: 1
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {TH['RestlessEnter']}
    RestlessExit: {TH['RestlessExit']}
    FrenzyEnter: {TH['FrenzyEnter']}
    FrenzyExit: {TH['FrenzyExit']}
    RestlessEnterVolume: {TH['RestlessEnterVolume']}
    RestlessExitVolume: {TH['RestlessExitVolume']}
    FrenzyEnterVolume: {TH['FrenzyEnterVolume']}
    FrenzyExitVolume: {TH['FrenzyExitVolume']}
""")
emit(f"{CELL_DIR}/Drumfire Cell Config.asset.meta", asset_meta(G_ASSET["DrumfireCellConfig"]))


# ── 5. Arcade game config ───────────────────────────────────────────────────
# DOLPHIN ONLY: a single entry in Vessels drives all three enforcement layers (the launcher
# clamp, the server-side spawn clamp, and the AI clamp).
#
# MinDomainsAllowed 2: volume sums per DOMAIN, so a one-colour lobby would be a co-op timer.
# GolfScoring 0: most volume wins.
emit("Assets/_SO_Assets/Games/ArcadeGameDrumfire.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameDrumfire") + f"""  Mode: 45
  IsMultiplayer: 1
  DisplayName: Drumfire
  Description: Dolphins only, on a firing range. A great drum of prisms hangs in the
    middle and your own line of crystals runs PAST it, so the target is always off to
    one side. Drift to hold your line, swing the nose onto the drum, and take the next
    crystal to let the jaws go - fly, aim, shoot, repeat. Graze the drum on the way by
    and the next blast opens wider. Most volume torn out when the clock stops wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  PreviewClip: {{fileID: {PREVIEW_FILEID}, guid: {EXISTING['PreviewClip']}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameDrumfire
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Dolphin']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 404
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameDrumfire.asset.meta",
     asset_meta(G_ASSET["ArcadeGameDrumfire"]))


# ── 6. Scene: clone MinigameRampage, swap the mode wiring ───────────────────
# The donor is the other DOLPHIN-only mode: Dolphin AI templates, a cell-relative spawn ring,
# an IntensityWise cell and a NetworkCrystalManager on IntensityScaled counts. What changes is
# the mode identity, the arena, the crystal PLACEMENT (anchors -> lanes) and the clock.
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity")

# 6a. turn monitor script swap. Both carry only the base TurnMonitor fields, so the serialized
# block is identical; the duration is resolved from EndConditionOverridesSO at StartMonitor.
scene, n = re.subn(EXISTING["RampagePrismTurnMonitor"], G_SCRIPT["DrumfireTimeTurnMonitor"], scene)
assert n == 1, f"turn monitor guid appeared {n} times"

# 6b. controller script + its rule reference
scene, n = re.subn(EXISTING["RampageController"], G_SCRIPT["DrumfireController"], scene)
assert n == 1, f"controller guid appeared {n} times"
OLD_RULE = f"  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}\n"
NEW_RULE = f"  rule: {{fileID: 11400000, guid: {G_ASSET['DrumfireScoringRule']}, type: 2}}\n"
assert scene.count(OLD_RULE) == 1, "controller rule reference not found in donor scene"
scene = scene.replace(OLD_RULE, NEW_RULE)

# 6c. THE ARENA. One config, not four: the drum is identical at every intensity (intensity is
# the crystal count - the RHYTHM - which the crystal manager owns). CellTypeChoiceOptions.Random
# over a single-entry list resolves to that entry on every peer.
OLD_CELL = re.search(r"  CellConfigs:\n(?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+"
                     r"  cellTypeChoiceOptions: 1\n", scene)
assert OLD_CELL, "donor Cell config list not found"
scene = scene.replace(OLD_CELL.group(0),
                      f"  CellConfigs:\n"
                      f"  - {{fileID: 11400000, guid: {G_ASSET['DrumfireCellConfig']}, type: 2}}\n"
                      f"  cellTypeChoiceOptions: 0\n")

# 6d. THE LANES - the mode's own geometry, and the one thing the donor has no equivalent of.
# placementMode 1 = CrystalPlacementMode.ApproachLanes. The per-intensity table now authors
# crystals PER LANE (CrystalsPerPlayer 0 + ExtraCrystals n gives a flat n), and the total is
# that times the number of lanes.
OLD_CRYSTALS = ("  listOfCrystalPositions: []\n  anchorJitterRadius: 35\n"
                "  noNucleusSpawnRadius: 0\n  crystalCountMode: 2\n"
                "  fixedCrystalCount: 1\n  extraCrystalsToSpawnBeyondPlayerCount: 0\n"
                "  crystalCountByIntensity:\n"
                "  - CrystalsPerPlayer: 2\n    ExtraCrystals: 0\n"
                "  - CrystalsPerPlayer: 1\n    ExtraCrystals: 0\n"
                "  - CrystalsPerPlayer: 1\n    ExtraCrystals: -1\n"
                "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 1\n"
                "  spawnCrystalWithPlayerDomain: 0\n")
NEW_CRYSTALS = ("  listOfCrystalPositions: []\n  anchorJitterRadius: 35\n"
                "  noNucleusSpawnRadius: 0\n  crystalCountMode: 2\n"
                "  fixedCrystalCount: 1\n  extraCrystalsToSpawnBeyondPlayerCount: 0\n"
                "  crystalCountByIntensity:\n"
                + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {s}\n"
                          for s in SLOTS_BY_INTENSITY) +
                "  placementMode: 1\n"
                f"  laneRingRadius: {SPAWN_RING_RADIUS}\n"
                f"  laneOffsetFromCenter: {LANE_OFFSET}\n"
                f"  laneLeadDistance: {LANE_LEAD}\n"
                f"  laneLength: {LANE_LENGTH}\n"
                "  laneFormation: 0\n"
                "  spawnCrystalWithPlayerDomain: 0\n")
assert OLD_CRYSTALS in scene, "donor crystal block not found"
scene = scene.replace(OLD_CRYSTALS, NEW_CRYSTALS, 1)

# 6e. THE SPAWN RING. This cell has no nucleus, so ExpectedNucleusWorldRadius is 0 and the ring
# would collapse to spawnDistanceOutsideNucleus from the CELL CENTRE - inside the drum. The
# floor is what PeelTheCage added for exactly this case, and it must equal laneRingRadius or
# the lanes stop passing through the players' spawn points.
OLD_RING = "  spawnRingRadiusFloor: 0\n"
assert scene.count(OLD_RING) == 1, "donor spawn ring floor not found"
scene = scene.replace(OLD_RING, f"  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}\n")

# 6f. The comeback source: 7 = ScoreDifferenceSource.VolumeDestroyed (3 = PrismsDestroyed on
# the donor). Score lands only at game end here, so the live metric is the honest source.
OLD_SRC = "  differenceSource: 3\n"
assert scene.count(OLD_SRC) == 1, "donor comeback source not found"
scene = scene.replace(OLD_SRC, "  differenceSource: 7\n")

emit("Assets/_Scenes/Multiplayer Scenes/MinigameDrumfire.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameDrumfire.unity.meta",
     scene_meta(G_ASSET["MinigameDrumfire.unity"]))


# ── 7. Register the card in the party-games list ────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameDrumfire']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 8. Always-unlocked so the card is clickable on a fresh account ──────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 45\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 45\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 9. Build settings ──────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameDrumfire.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRampage\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Rampage scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameDrumfire.unity\n"
                          f"    guid: {G_ASSET['MinigameDrumfire.unity']}\n")
emit(BUILD_PATH, build)


# ── 10. The match clock ────────────────────────────────────────────────────
# SET semantics, not add-if-absent (the Dog Fight generator's lesson: an insert-only key left
# the asset on a stale number after a retune).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("salvoPrismTarget", "drumfireSeconds"),
                          ("salvoPrismTargetBuild", "drumfireSecondsBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {MATCH_SECONDS}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH} - run author_salvo_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {MATCH_SECONDS}\n", 1)
emit(END_PATH, endcond)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

all_new = list(G_SCRIPT.values()) + list(G_ASSET.values())
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

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
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameDrumfire.unity"]
for name in ("RampageController", "RampagePrismTurnMonitor", "RampageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("DrumfireController", "DrumfireTimeTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["DrumfireScoringRule"] not in sc:
    errors.append("cloned scene missing the scoring rule reference")
if G_ASSET["DrumfireCellConfig"] not in sc:
    errors.append("cloned scene does not point the Cell at the Drumfire config")
if sc.count("vesselClass: 2") != 4:
    errors.append("scene does not author 4 Dolphin AI templates")
if "  placementMode: 1\n" not in sc:
    errors.append("scene is not on CrystalPlacementMode.ApproachLanes - crystals would spawn in "
                  "a nucleus this cell does not have, which falls through to the crystal's own "
                  "SphereRadius and stacks every one of them on the arena's exact centre")
if f"  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}\n" not in sc:
    errors.append("scene lost the spawn ring floor - this cell has no nucleus, so every player "
                  "would spawn inside the drum")
if f"  laneRingRadius: {SPAWN_RING_RADIUS}\n" not in sc:
    errors.append("lane ring radius missing")
if "  differenceSource: 7\n" not in sc:
    errors.append("scene does not read the comeback deficit from VolumeDestroyed")

# THE LANE AND THE SPAWN RING MUST AGREE. This is the one cross-component invariant the mode
# rests on: lane k is struck THROUGH spawn slot k, so if the two radii ever drift the crystals
# stop being anybody's line.
m_floor = re.search(r"^  spawnRingRadiusFloor: (\d+)\n", sc, re.M)
m_lane = re.search(r"^  laneRingRadius: (\d+)\n", sc, re.M)
if m_floor and m_lane and m_floor.group(1) != m_lane.group(1):
    errors.append(f"spawnRingRadiusFloor ({m_floor.group(1)}) != laneRingRadius "
                  f"({m_lane.group(1)}) - the lanes no longer pass through the spawn points")
m_form_spawn = re.search(r"^  spawnFormation: (\d+)\n", sc, re.M)
m_form_lane = re.search(r"^  laneFormation: (\d+)\n", sc, re.M)
if m_form_spawn and m_form_lane and m_form_spawn.group(1) != m_form_lane.group(1):
    errors.append("spawnFormation != laneFormation - lane k would not pass through spawn slot k")

# The lane must clear the drum, and it must not clear it by so much that leaning in to graze
# the skin for jaw energy stops being an option. Both are geometry, both are cheap to check.
if LANE_OFFSET <= DRUM_OUTER_RADIUS:
    errors.append(f"lane offset {LANE_OFFSET} does not clear the drum ({DRUM_OUTER_RADIUS})")
if LANE_OFFSET - DRUM_OUTER_RADIUS > DRUM_OUTER_RADIUS * 0.5:
    errors.append("the lane stands too far off the drum to graze it on the way past")

# Dolphin only, or the mode stops being about one hull's rhythm.
arcade = files["Assets/_SO_Assets/Games/ArcadeGameDrumfire.asset"]
vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", arcade, re.M)
if not vessels or vessels.group(1).count("- {fileID") != 1:
    errors.append("ArcadeGameDrumfire must author EXACTLY ONE vessel (Dolphin)")
elif EXISTING["Vessel_Dolphin"] not in vessels.group(1):
    errors.append("ArcadeGameDrumfire's single vessel is not Dolphin")
if "MinDomainsAllowed: 2" not in arcade:
    errors.append("ArcadeGameDrumfire must require at least TWO domains")
if "GolfScoring: 0" not in arcade:
    errors.append("Drumfire is a POINTS mode - most volume wins - so GolfScoring must be 0")

# The comeback rate is a function of the SCORE SCALE, and this mode's scale is volume.
_quarter_deficit_levels = (TYPICAL_WINNING_VOLUME * 0.25) * COMEBACK_RATE
if _quarter_deficit_levels < 1.0:
    errors.append(f"ComebackRatePerScoreDeficit {COMEBACK_RATE} is too small against a "
                  f"~{TYPICAL_WINNING_VOLUME} volume score: a quarter-of-score deficit buys "
                  f"{_quarter_deficit_levels:.2f} element levels, which is invisible")
if _quarter_deficit_levels > 5.0:
    errors.append(f"ComebackRatePerScoreDeficit {COMEBACK_RATE} hands the trailing side "
                  f"{_quarter_deficit_levels:.1f} element levels for a quarter-of-score deficit")

# The phase ladder must sit ABOVE the drum, or the cell boots into Frenzy.
if TH["RestlessEnterVolume"] <= DRUM_BASELINE_VOLUME:
    errors.append("PhaseThresholds do not clear the drum's own volume")


def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    MODS = (r"(?:(?:public|protected|private|internal|static|const|readonly|new|virtual|"
            r"override|abstract|sealed|partial)\s+)+")
    for m in re.finditer(MODS + TYPE + r"\s+(\w+)\s*(?:=|;|\{|=>|\()", src):
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
CHECKS = [
    ("Assets/_SO_Assets/Games/ArcadeGameDrumfire.asset",
     ["Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs",
      "Assets/_Scripts/ScriptableObjects/SO_Game.cs"]),
    ("Assets/_SO_Assets/Scoring Rules/DrumfireScoringRule.asset",
     ["Assets/_Scripts/Controller/Arcade/Scoring/DrumfireScoringRuleSO.cs",
      "Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs"]),
    (f"{CELL_DIR}/Drumfire Cell Config.asset",
     ["Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs"]),
    (f"{CELL_DIR}/Drumfire Spawn Profile.asset",
     ["Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs"]),
    ("Assets/_Prefabs/Spawnables/SpawnableDrum.prefab",
     ["Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableDrum.cs",
      "Assets/_Scripts/Controller/Environment/Spawning/CellEnvironmentSpawnableBase.cs",
      "Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs"]),
]
for asset_path, cs_paths in CHECKS:
    body = files[asset_path]
    # Only the MonoBehaviour document's own top-level keys (a prefab also holds a GameObject
    # and a Transform, whose keys belong to Unity).
    mb = body[body.index("MonoBehaviour:"):] if "MonoBehaviour:" in body else body
    keys = set(re.findall(r"^  (\w+):", mb, re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = set(SO_BASE)
    for cs in cs_paths:
        full = os.path.join(ROOT, cs)
        if os.path.exists(full):
            known |= cs_fields(cs)
        else:
            errors.append(f"{cs} not found - cannot validate {os.path.basename(asset_path)}")
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on its script(s): "
                      f"{sorted(unknown)}")

# the C# default clock and this script's must agree, or the tool window's "(default)" lies
endcond_cs = read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultDrumfireSeconds = (\d+);", endcond_cs)
if not m:
    errors.append("EndConditionOverridesSO.cs has no DefaultDrumfireSeconds")
elif int(m.group(1)) != MATCH_SECONDS:
    errors.append(f"DefaultDrumfireSeconds ({m.group(1)}) != this script's MATCH_SECONDS "
                  f"({MATCH_SECONDS}) - the two must move together")

# GameModes.Drumfire must exist with the value this card authors
gamemodes_cs = read("Assets/_Scripts/Data/Enums/GameModes.cs")
if not re.search(r"^\s*Drumfire = 45,", gamemodes_cs, re.M):
    errors.append("GameModes.cs has no 'Drumfire = 45' - the card would launch nothing")

# the drum prefab's numbers and the measurement script's must be the same numbers
arena_py = read("Tools/Build/drumfire_arena.py")
for const, value in (("OUTER_RADIUS", DRUM_OUTER_RADIUS), ("SHELL_COUNT", DRUM_SHELLS),
                     ("OUTER_SHELL_POINTS", DRUM_OUTER_POINTS), ("SEED", DRUM_SEED),
                     ("LANE_RING_RADIUS", SPAWN_RING_RADIUS), ("LANE_OFFSET", LANE_OFFSET),
                     ("LANE_LEAD", LANE_LEAD), ("LANE_LENGTH", LANE_LENGTH),
                     ("MATCH_SECONDS", MATCH_SECONDS)):
    m = re.search(rf"^{const} = ([0-9.]+)", arena_py, re.M)
    if not m:
        errors.append(f"drumfire_arena.py has no {const}")
    elif float(m.group(1)) != float(value):
        errors.append(f"drumfire_arena.py {const} = {m.group(1)} but this script authors {value} "
                      f"- the measurement would describe a different arena than the one that ships")
m = re.search(r"^SLOTS_BY_INTENSITY = \(([^)]*)\)", arena_py, re.M)
if not m:
    errors.append("drumfire_arena.py has no SLOTS_BY_INTENSITY")
elif tuple(int(x) for x in m.group(1).split(",")) != SLOTS_BY_INTENSITY:
    errors.append("drumfire_arena.py SLOTS_BY_INTENSITY differs from the scene's crystal table")

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
