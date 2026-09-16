#!/usr/bin/env python3
"""
Authors every serialized asset the Cleave game mode needs (GameModes.Cleave = 39).

Cleave is the Rhino-only slicing race. Its FOUR intensities are four DIFFERENT PLACES to put a
sword through rather than four sizes of one - angled panes, wide wavy roads, the nested
cage, twisted Mobius ribbons - so this script authors one arena prefab and one CellConfigDataSO
per intensity and puts the Cell on CellTypeChoiceOptions.IntensityWise.

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_cleave_assets.py [--check]

--check compares every file it would write against what is on disk and FAILS on any drift. It
is a real diff, not a dry run: an earlier version of this script only re-ran its in-memory
validation and printed "no files written", which passed whatever the assets actually said.

See Assets/_Scripts/Controller/Arcade/CLEAVE.md for what these numbers mean.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv

# Baselines are MEASURED, never copied: cleave_budget reads the JSON that
# Tools/Build/cleave_arena_harness produces by compiling and RUNNING the shipped arena
# generators, and re-hashes every source that could move a count. PhaseThresholds therefore
# cannot drift from the geometry behind a stale constant - and if they could, this import
# raises instead of writing.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cleave_budget as budget  # noqa: E402

MEASURED, ARENAS = budget.ladder()


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family).

    **The seed is IDENTITY, not a name, and it never changes.** The guid is md5 of this string,
    so editing a seed mints a DIFFERENT guid - and this script would then write .meta files that
    no scene, prefab or asset in the project still points at. Nothing would fail: the assets
    would simply be re-authored as strangers, and every reference to them would resolve to
    nothing.

    That is why the seeds below still read `Ribcage` after TWO renames (Ribcage -> Peel the Cage
    in 2026-09, Peel the Cage -> Cleave immediately after). The dictionary KEYS and the file
    PATHS moved with the mode; the seeds stayed on the name the assets were minted under. Only
    the genuinely NEW assets - the three new arenas - carry seeds that read like their own names.
    `Tools/Build/rename_game_modes.py` excludes this file for the same reason it excludes its
    own map.
    """
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


# ── Script GUIDs (the .cs.meta files this script also writes) ────────────────
G_SCRIPT = {
    "SliceArenaGeometry":       guid("script/SliceArenaGeometry"),
    "SpawnablePanes":           guid("script/SpawnablePanes"),
    "SpawnableSwell":           guid("script/SpawnableSwell"),
    "SpawnableRibcage":         guid("script/SpawnableRibcage"),
    "SpawnableTwistbands":      guid("script/SpawnableTwistbands"),
    "CleaveController":         guid("script/RibcageController"),
    "CleavePrismTurnMonitor":   guid("script/RibcagePrismTurnMonitor"),
    "CleaveScoringRuleSO":      guid("script/RibcageScoringRuleSO"),
}

SCRIPT_PATHS = {
    "SliceArenaGeometry":     "Assets/_Scripts/Controller/Environment/MiniGameObjects/SliceArenaGeometry.cs",
    "SpawnablePanes":         "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnablePanes.cs",
    "SpawnableSwell":         "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableSwell.cs",
    "SpawnableRibcage":       "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableRibcage.cs",
    "SpawnableTwistbands":    "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableTwistbands.cs",
    "CleaveController":       "Assets/_Scripts/Controller/Arcade/CleaveController.cs",
    "CleavePrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/CleavePrismTurnMonitor.cs",
    "CleaveScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Scoring/CleaveScoringRuleSO.cs",
}

# ── Per-arena authoring: the prefab each measured arena is wired into ────────
#
# Everything NUMERIC about an arena - its seed, its shell count, its prism count and volume -
# comes from the measurement, so the prefab's seed can never disagree with the seed the counts
# were taken under. What lives here is only what the measurement has no opinion about: where the
# prefab goes, which guid seed it was minted under, and how to describe the place.
ARENA_AUTHORING = {
    "panes": dict(
        prefab="Assets/_Prefabs/Spawnables/SpawnablePanes.prefab",
        guid_seed="asset/SpawnablePanes.prefab",
        blurb="nine flat slabs cutting the cell at nine angles, each a corduroy of parallel "
              "ribs with its own grain, beams down every pane-pair intersection"),
    "swell": dict(
        prefab="Assets/_Prefabs/Spawnables/SpawnableSwell.prefab",
        guid_seed="asset/SpawnableSwell.prefab",
        blurb="five wide wavy roads meandering closed circuits through the cell, crown painted "
              "gold and verges jade so the carriageway reads across the arena"),
    "cage": dict(
        # The path lost its index when the other four shell variants were retired; the SEED
        # keeps it, because the seed is this prefab's identity and the scene points at it.
        prefab="Assets/_Prefabs/Spawnables/SpawnableRibcage.prefab",
        guid_seed="asset/SpawnableRibcage2.prefab",
        blurb="three concentric rinds of prism bone at radius 360 / 295 / 230, each tilted onto "
              "its own axis, tightening as you peel inward"),
    "twistbands": dict(
        prefab="Assets/_Prefabs/Spawnables/SpawnableTwistbands.prefab",
        guid_seed="asset/SpawnableTwistbands.prefab",
        blurb="three interlocked one-sided Mobius ribbons carrying a plated deck that rolls out "
              "from under the blade as you fly it"),
}

# Fold the authoring entries onto the measured arenas, so everything about an arena is reachable
# from one record and a measured arena with no authoring entry fails HERE rather than as a
# KeyError halfway through writing a cell config.
for _a in ARENAS:
    _auth = ARENA_AUTHORING.get(_a["key"])
    if _auth is None:
        raise SystemExit(f"measured arena '{_a['key']}' has no ARENA_AUTHORING entry")
    _a.update(_auth)

G_ASSET = {
    "ArcadeGameCleave":     guid("asset/ArcadeGameRibcage"),
    "CleaveScoringRule":    guid("asset/RibcageScoringRule"),
    "CleaveSpawnProfile":   guid("asset/RibcageSpawnProfile"),
    "MinigameCleave.unity": guid("asset/MinigameRibcage.unity"),
    "CleaveCellFolder":     guid("folder/RibcageCell"),
}
for _a in ARENAS:
    G_ASSET[f"prefab/{_a['key']}"] = guid(_a["guid_seed"])
    G_ASSET[f"CleaveCellConfig{_a['intensity']}"] = guid(f"asset/RibcageCellConfig{_a['intensity']}")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":        "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":     "01f934d50526431a9392a6ceca1dc33d",
    "SpawnProfileSO":       "e8d8aa5d835249798a256e18f2f7d912",
    # donor scene scripts to swap out (bring-up only - see section 6)
    "RampageController":       "e11ff862e6844a89a951292673243625",
    "RampagePrismTurnMonitor": "694b571734fe4a55a57f6cc672c7fcc2",
    "RampageCellConfig":       "c6959b0e548d4f26bdde820ca48ac26e",
    "RampageScoringRule":      "7d1bfbd4091c4a12a12c730553bf293a",
    # shared content
    "Prism_prefab":       "ed9defc56162b4b4588e61c20984b6d9",
    "Membrane_prefab":    "6e330f85972faf843b8a128e7166f7b5",
    # A x3 similarity of the above (radius 3600, capsuleScale (24,180,15)), for the two rungs
    # whose arena is 2,160 - the standard 1,200 membrane would be INSIDE their mass. Only the
    # two WORLD-UNIT fields differ: subdivisions is a capsule COUNT, the jitters are fractions
    # of the radius, noiseFrequency samples a UNIT direction and noiseAmplitude is in DEGREES,
    # so scaling any of those would change how the membrane LOOKS rather than how big it is.
    "CleaveMembrane_prefab": "6eb45784f9024cbb879f94c23ae46bd9",
    "Cytoplasm_prefab":   "9cacd903fcf4643459f5f14ac811bb20",
    "CellIcon":           "6aa1c06e11b265744a5f9fa8858ac72a",
    "Vessel_Rhino":       "ec97e344adb08f847a8f7649ab79088e",
    # arcade card art (shared with Rampage - the destruction family)
    "IconActive":         "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":       "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":     "587d2203114c8004c9985d0112c89585",
    # the cell's CellRuntimeDataSO - the spawn ring resolves its Cell through this
    "RuntimeCellData":    "8d4e8398eedc76c4dadb8604f89b9e1b",
}

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO_FILEID = 5260000000000201
SPAWNABLE_TR_FILEID = 5260000000000202
SPAWNABLE_MB_FILEID = 5260000000000203

# PER INTENSITY, from the budget model, which asserts each rung's
# arena < AI station < spawn ring < membrane ordering. Intensity 1 is element 0.
SPAWN_RING_BY_INTENSITY = [round(budget.SPAWN_RING[i]) for i in (1, 2, 3, 4)]
# The scalar fallback stays authored for a rung the list does not cover; the widest is the safe
# one, since a ring that is too big only parks a pilot further out while one that is too small
# spawns them inside the mass.
SPAWN_RING_RADIUS = max(SPAWN_RING_BY_INTENSITY)

# Which membrane each rung wears. The 2,160-radius arenas need the x3 shell; the 720 ones keep
# the standard 1,200 one every other cell uses.
MEMBRANE_GUID_BY_INTENSITY = {
    i: EXISTING["CleaveMembrane_prefab"] if budget.MEMBRANE_RADIUS[i] > 1200 else EXISTING["Membrane_prefab"]
    for i in (1, 2, 3, 4)
}

# Destruction target - the race metric, PER INTENSITY. The 25%/50% milestone rungs are fractions
# of whichever applies. Rungs 1 and 2 are vast open arenas holding about a third of the mass of
# 3 and 4, so a shared 2000 would make them the longest matches in the mode. Owned by the budget
# model, which asserts each rung's arena holds a comparable multiple of its own target.
CLEAVE_PRISM_TARGET_BY_INTENSITY = [budget.TARGET_BY_INTENSITY[i] for i in (1, 2, 3, 4)]
CLEAVE_PRISM_TARGET = max(CLEAVE_PRISM_TARGET_BY_INTENSITY)

# The comeback buff is `bonusLevels = deficit x rate`, so THE RATE IS A FUNCTION OF THE TARGET and
# re-targeting a mode silently kills it - the trap Dog Fight, The Bends, Wildlife Liberation and
# Tollway each record, hit here the moment the ladder went per-intensity. This mode's SMALLEST
# target is what binds: at a quarter-of-target deficit the trailing domain must still buy a whole
# element level, or the buff is a rounding error in exactly the match it exists to rescue.
COMEBACK_RATE = 0.0125
_worst = min(CLEAVE_PRISM_TARGET_BY_INTENSITY)
assert _worst / 4 * COMEBACK_RATE >= 1.0, (
    f"a quarter-of-target deficit on the {_worst}-prism rungs buys "
    f"{_worst / 4 * COMEBACK_RATE:.2f} element levels - under one whole level. Raise "
    "COMEBACK_RATE with the target, or the comeback stops doing anything on those rungs.")

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


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


# ── 1. .cs.meta for every script this mode owns ──────────────────────────────
for _k, _p in SCRIPT_PATHS.items():
    emit(_p + ".meta", meta(G_SCRIPT[_k]))


# ── 2. One arena prefab per intensity ────────────────────────────────────────
# Same shape for all four; only the component guid, the seed and (for the cage) the shell count
# differ. `seed` and `shellCount` come from the MEASUREMENT, so the prefab is guaranteed to
# build the arena whose prism count section 5 turns into PhaseThresholds.
for a in ARENAS:
    extra = f"  shellCount: {a['shellCount']}\n" if a["shellCount"] else ""
    emit(a["prefab"], f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{SPAWNABLE_GO_FILEID}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {SPAWNABLE_TR_FILEID}}}
  - component: {{fileID: {SPAWNABLE_MB_FILEID}}}
  m_Layer: 0
  m_Name: {a['type']}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{SPAWNABLE_TR_FILEID}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO_FILEID}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{SPAWNABLE_MB_FILEID}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO_FILEID}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT[a['type']]}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {a['seed']}
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: {a['intensity']}
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
{extra}""")
    emit(a["prefab"] + ".meta", prefab_meta(G_ASSET[f"prefab/{a['key']}"]))


# ── 3. Scoring rule ──────────────────────────────────────────────────────────
emit("Assets/_SO_Assets/Scoring Rules/CleaveScoringRule.asset",
     HEADER_FOR(G_SCRIPT["CleaveScoringRuleSO"], "CleaveScoringRule") +
     "  metric: 5\n  golfRules: 1\n")   # 5 = ScoringMetric.PrismsDestroyed (the race metric)
emit("Assets/_SO_Assets/Scoring Rules/CleaveScoringRule.asset.meta",
     asset_meta(G_ASSET["CleaveScoringRule"]))


# ── 4. Arcade game config ────────────────────────────────────────────────────
# NOTE: no `PreviewClip` and no `CallToActionTargetType`. Both are RETIRED keys - neither is
# declared on SO_ArcadeGame or SO_Game any more - and a generator is the second place a schema
# change has to land. Emitting one re-introduces a key the type no longer owns.
emit("Assets/_SO_Assets/Games/ArcadeGameCleave.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameCleave") + f"""  Mode: 39
  IsMultiplayer: 1
  DisplayName: Cleave
  Description: Four arenas, one blade. Angled panes, wide wavy roads, a cage of
    nested shells, and twisted ribbons that roll out from under you - every intensity
    is a different place to cut, not a bigger version of the last. Danger prisms ride
    the rims and the outside of the bends, so read the place before you commit.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameCleave
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Rhino']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameCleave.asset.meta", asset_meta(G_ASSET["ArcadeGameCleave"]))


# ── 5. Cell configs (ONE PER INTENSITY) + spawn profile ──────────────────────
#
# NO FAUNA. The brood was removed from this level on request (2026-08); the cell keeps its
# membrane / cytoplasm / phase machinery because the Cell owns the environment, but it authors
# no species, so SupportedFaunas is empty and nothing hatches.
#
# Each intensity gets its OWN CellConfigDataSO because PhaseThresholds must ride ITS OWN
# baseline: the arenas run 11,021 to 16,423 prisms and 2.43M to 4.06M volume, so a shared
# threshold block would put three of the four cells in the wrong phase from frame one.
# Cell.AssignConfig picks by CellTypeChoiceOptions.IntensityWise (index = intensity-1).
emit("Assets/_SO_Assets/Cell Configs/Cleave Cell.meta", meta(G_ASSET["CleaveCellFolder"], folder=True))

for a in ARENAS:
    i, th = a["intensity"], a["thresholds"]
    emit(f"Assets/_SO_Assets/Cell Configs/Cleave Cell/Cleave Cell Config {i}.asset",
         HEADER_FOR(EXISTING["CellConfigDataSO"], f"Cleave Cell Config {i}") + f"""  CellName: Cleave
  Description: '{a['label']} - intensity {i} of Cleave: {a['blurb']}.
    {a['prisms']} prisms, {a['byKind']['Danger']} of them danger traps. NO NUCLEUS by design,
    and no fauna. PhaseThresholds ride THIS arena''s own MEASURED baseline; regenerate with
    Tools/Build/author_cleave_assets.py after any geometry change rather than hand-editing.'
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {MEMBRANE_GUID_BY_INTENSITY[i]}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['CleaveSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {{fileID: {SPAWNABLE_MB_FILEID}, guid: {G_ASSET[f"prefab/{a['key']}"]}, type: 3}}
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
    emit(f"Assets/_SO_Assets/Cell Configs/Cleave Cell/Cleave Cell Config {i}.asset.meta",
         asset_meta(G_ASSET[f"CleaveCellConfig{i}"]))

# One spawn profile, shared by all four configs: it authors nothing to spawn.
emit("Assets/_SO_Assets/Cell Configs/Cleave Cell/Cleave Spawn Profile.asset",
     HEADER_FOR(EXISTING["SpawnProfileSO"], "Cleave Spawn Profile") + """  FloraExcludeLocalDomain: 0
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
emit("Assets/_SO_Assets/Cell Configs/Cleave Cell/Cleave Spawn Profile.asset.meta",
     asset_meta(G_ASSET["CleaveSpawnProfile"]))


# ── 6. The scene ─────────────────────────────────────────────────────────────
#
# Cloning MinigameRampage was a ONE-SHOT bring-up. The scene is committed now, so the clone
# STANDS DOWN rather than asserting: a donor that has since moved on would abort this script and
# take every check below it with it, which is exactly the shape that left four sibling mode
# generators silently validating nothing. Once the scene exists, the job is to keep the blocks
# this script owns correct, idempotently, and to leave everything else in it alone.
SCENE_PATH = "Assets/_Scenes/Multiplayer Scenes/MinigameCleave.unity"
DONOR_PATH = "Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity"
scene_notes = []

if os.path.exists(os.path.join(ROOT, SCENE_PATH)):
    scene = read(SCENE_PATH)
    scene_notes.append("patched the committed scene (the Rampage clone is spent)")
else:
    scene = read(DONOR_PATH)
    scene_notes.append("CLONED from MinigameRampage (first bring-up)")
    for old, new in (("RampagePrismTurnMonitor", "CleavePrismTurnMonitor"),
                     ("RampageController", "CleaveController")):
        scene, n = re.subn(EXISTING[old], G_SCRIPT[new], scene)
        assert n == 1, f"{old} guid appeared {n} times in the donor"
    OLD_FIELDS = (f"  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}\n"
                  "  arenaCell: {fileID: 1700000065}\n  aiRetargetSeconds: 1.5\n")
    assert OLD_FIELDS in scene, "controller field block not found in donor scene"
    scene = scene.replace(OLD_FIELDS,
        f"  rule: {{fileID: 11400000, guid: {G_ASSET['CleaveScoringRule']}, type: 2}}\n"
        "  arenaCell: {fileID: 1700000065}\n"
        "  firstMilestoneFraction: 0.25\n  secondMilestoneFraction: 0.5\n"
        "  progressSampleSeconds: 0.5\n  aiRetargetSeconds: 2\n  aiCageRadiusOverride: 0\n")
    OLD_SPAWN = ("  playerSpawnPoints:\n  - {fileID: 1468661147}\n  - {fileID: 1074736317}\n"
                 "  - {fileID: 1323644424}\n  - {fileID: 1564881929}\n  preSpawnDelayMs: 200\n")
    assert OLD_SPAWN in scene, "donor spawn-point block not found"
    scene = scene.replace(OLD_SPAWN,
        "  playerSpawnPoints:\n  - {fileID: 1468661147}\n  - {fileID: 1074736317}\n"
        "  - {fileID: 1323644424}\n  - {fileID: 1564881929}\n"
        "  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
        "  spawnFormation: 1\n  spawnRingRadiusFloor: 0\n"
        f"  cellData: {{fileID: 11400000, guid: {EXISTING['RuntimeCellData']}, type: 2}}\n"
        "  preSpawnDelayMs: 200\n")

# 6a. The Cell's per-intensity config list. Rewritten wholesale every run, so retiring an
# intensity (the dead fifth cage variant) is one edit here rather than a hand-edit in the scene.
# cellTypeChoiceOptions 1 = IntensityWise; without it the per-intensity configs are never picked.
config_lines = []
for a in ARENAS:
    g = G_ASSET["CleaveCellConfig" + str(a["intensity"])]
    config_lines.append("  - {fileID: 11400000, guid: " + g + ", type: 2}\n")
new_cell = "  CellConfigs:\n" + "".join(config_lines) + "  cellTypeChoiceOptions: 1\n"
scene, n = re.subn(
    r"  CellConfigs:\n(?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+"
    r"  cellTypeChoiceOptions: \d+\n", new_cell, scene, count=1)
assert n == 1, "scene Cell config list not found"

# 6ab. The controller's serialized field names. These were authored by the one-shot clone, so
# nothing would keep them in step with the C# after a field rename - and Unity drops an unknown
# key SILENTLY, leaving the field on its initializer with no error anywhere. Idempotent.
for _old_key, _new_key in (("aiCageRadiusOverride", "aiArenaRadiusOverride"),):
    scene = scene.replace(f"  {_old_key}: ", f"  {_new_key}: ")

# 6b. The spawn ring, PER INTENSITY. This cell has NO nucleus, so the computed ring would
# collapse to the cell centre without a floor; the floors are owned by cleave_budget, which
# asserts each sits outside its own rung's AI stations and inside its own membrane. The scalar
# stays authored as the fallback for a rung the list does not cover.
scene, n = re.subn(r"  spawnRingRadiusFloor: \d+\n",
                   f"  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}\n", scene, count=1)
assert n == 1, "scene spawnRingRadiusFloor not found"

_ring_list = "".join(f"  - {r}\n" for r in SPAWN_RING_BY_INTENSITY)
scene, n = re.subn(
    r"  spawnRingRadiusFloorByIntensity:(?: \[\]\n|\n(?:  - \d+\n)+)",
    f"  spawnRingRadiusFloorByIntensity:\n{_ring_list}", scene, count=1)
if n == 0:
    # First run after the field was added: Unity has not written the key yet, so append it
    # directly under the scalar it overrides.
    scene, n = re.subn(rf"  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}\n",
                       f"  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}\n"
                       f"  spawnRingRadiusFloorByIntensity:\n{_ring_list}", scene, count=1)
assert n == 1, "scene spawnRingRadiusFloorByIntensity could not be written"

emit(SCENE_PATH, scene)
emit(SCENE_PATH + ".meta", scene_meta(G_ASSET["MinigameCleave.unity"]))


# ── 7. Register the card in the party-games list ─────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameCleave']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 8. Always-unlocked so the card is clickable on a fresh account ───────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(r"^  alwaysUnlockedModes:\n(  - \d+\n)*  - 39\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 39\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 9. Build settings ────────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameCleave.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRampage\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Rampage scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameCleave.unity\n"
                          f"    guid: {G_ASSET['MinigameCleave.unity']}\n")
emit(BUILD_PATH, build)


# ── 10. End-game condition target ────────────────────────────────────────────
# The shared overrides asset is what FrogletTools > Game Modes > End Game Conditions edits.
# A missing key would silently fall back to the C# field initializer, so author the live and the
# build-baseline value explicitly, next to Rampage's.
#
# The scalar is authored AND the per-intensity ladder over it, because the ladder is what the
# mode actually races to and the scalar is only the fallback for a rung the list does not cover.
# The ladder is REWRITTEN every run rather than only-if-missing: it is owned by cleave_budget,
# which asserts each rung's arena holds a comparable multiple of its own target, so a hand-edit
# here is a number nothing proved.
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("rampagePrismTarget", "cleavePrismTarget"),
                          ("rampagePrismTargetBuild", "cleavePrismTargetBuild")):
    # Authored every run, not only-if-missing. The scalar was left alone once, and it went on
    # saying 2000 for a ladder whose heaviest rung had come down to 1500 - inert, because the list
    # covers every intensity and GetCleavePrismTarget(intensity) clamps into it, but a stale
    # fallback is a number the End Game Conditions window shows a human as if it were live.
    endcond, n = re.subn(rf"^  {new_key}: \d+\n", f"  {new_key}: {CLEAVE_PRISM_TARGET}\n",
                         endcond, count=1, flags=re.M)
    if n:
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH}"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {CLEAVE_PRISM_TARGET}\n", 1)

_target_list = "".join(f"  - {t}\n" for t in CLEAVE_PRISM_TARGET_BY_INTENSITY)
for scalar_key, list_key in (("cleavePrismTarget", "cleavePrismTargetByIntensity"),
                             ("cleavePrismTargetBuild", "cleavePrismTargetByIntensityBuild")):
    endcond, n = re.subn(rf"  {list_key}:(?: \[\]\n|\n(?:  - \d+\n)+)",
                         f"  {list_key}:\n{_target_list}", endcond, count=1)
    if n == 0:
        m = re.search(rf"^  {scalar_key}: \d+\n", endcond, re.M)
        assert m, f"{scalar_key} not found in {END_PATH}"
        endcond = endcond.replace(m.group(0), m.group(0) + f"  {list_key}:\n{_target_list}", 1)
emit(END_PATH, endcond)


def cs_fields(path):
    """Serialized field names declared on a C# type (asset-surgery §3)."""
    src = read(path)
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    for m in re.finditer(r"(?:public|protected|private|internal)\s+"
                         r"(?:readonly\s+)?" + TYPE + r"\s+(\w+)\s*(?:=|;|\{)", src):
        out.add(m.group(1))
    # Modifier-less [SerializeField] fields - the house style - including attribute lists.
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    return out


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

# The scene must carry this mode's wiring and none of the donor's.
sc = files[SCENE_PATH]
for name in ("RampageController", "RampagePrismTurnMonitor", "RampageCellConfig", "RampageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"scene still references {name}")
for name in ("CleaveController", "CleavePrismTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"scene missing {name}")
if G_ASSET["CleaveScoringRule"] not in sc:
    errors.append("scene missing the Cleave scoring rule reference")
if "  cellTypeChoiceOptions: 1\n" not in sc:
    errors.append("scene Cell is not on CellTypeChoiceOptions.IntensityWise - "
                  "the per-intensity configs would never be selected")
if f"  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}\n" not in sc:
    errors.append(f"scene spawn ring floor is not {SPAWN_RING_RADIUS}")
_want_rings = "  spawnRingRadiusFloorByIntensity:\n" + "".join(f"  - {r}\n" for r in SPAWN_RING_BY_INTENSITY)
if _want_rings not in sc:
    errors.append(f"scene per-intensity spawn ring floors are not {SPAWN_RING_BY_INTENSITY}")
# Every serialized key on the controller must still exist on the class - a renamed field is
# dropped silently by Unity, which reads as "the override stopped working" and nothing else.
_ctrl_fields = cs_fields("Assets/_Scripts/Controller/Arcade/CleaveController.cs")
for _k in ("rule", "arenaCell", "firstMilestoneFraction", "secondMilestoneFraction",
           "progressSampleSeconds", "aiRetargetSeconds", "aiArenaRadiusOverride"):
    if f"  {_k}: " not in sc:
        errors.append(f"scene controller block is missing '{_k}'")
    elif _k not in _ctrl_fields:
        errors.append(f"scene authors '{_k}' but CleaveController.cs has no such field")
for a in ARENAS:
    if G_ASSET[f"CleaveCellConfig{a['intensity']}"] not in sc:
        errors.append(f"scene missing Cleave cell config {a['intensity']}")
# The retired fifth cage variant must be gone from the scene as well as from disk.
dead = guid("asset/RibcageCellConfig5")
if dead in sc:
    errors.append("scene still lists the retired fifth cell config")

# The ladder the cell configs describe must be the ladder that was measured.
if len(ARENAS) != 4:
    errors.append(f"expected four arenas, measured {len(ARENAS)}")
for a in ARENAS:
    if a["key"] not in ARENA_AUTHORING:
        errors.append(f"measured arena '{a['key']}' has no authoring entry")
    if a["type"] not in G_SCRIPT:
        errors.append(f"measured arena type '{a['type']}' has no script guid")


CHECKS = [(f"Assets/_SO_Assets/Cell Configs/Cleave Cell/Cleave Cell Config {a['intensity']}.asset",
           "Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs") for a in ARENAS] + [
    ("Assets/_SO_Assets/Cell Configs/Cleave Cell/Cleave Spawn Profile.asset",
     "Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs"),
    ("Assets/_SO_Assets/Games/ArcadeGameCleave.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs"),
]
SO_BASE = {"CellName", "Description", "Icon", "Difficulty", "CellEndGameScore", "Mode",
           "IsMultiplayer", "DisplayName", "IconActive", "IconInactive", "CardBackground",
           "GolfScoring", "SceneName"}
for asset_path, cs_path in CHECKS:
    keys = set(re.findall(r"^  (\w+):", files[asset_path], re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = cs_fields(cs_path) | SO_BASE | cs_fields("Assets/_Scripts/ScriptableObjects/SO_Game.cs")
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on "
                      f"{os.path.basename(cs_path)}: {sorted(unknown)}")

# The cage is the one arena with a shell count, and it must be the THREE-rind variant - the one
# the mode kept when the other three nested-shell intensities were replaced.
cage = next((a for a in ARENAS if a["key"] == "cage"), None)
if cage is None:
    errors.append("the cage arena is missing from the ladder")
elif cage["shellCount"] != 3:
    errors.append(f"the cage is {cage['shellCount']} shells - the kept arena is the THREE-rind one")

if errors:
    print("VALIDATION FAILED - nothing written:")
    for e in errors:
        print("  x", e)
    sys.exit(1)


# ══ WRITE, or DIFF ══════════════════════════════════════════════════════════
if CHECK_ONLY:
    drifted = []
    for rel, content in sorted(files.items()):
        path = os.path.join(ROOT, rel)
        if not os.path.exists(path):
            drifted.append((rel, "missing on disk"))
            continue
        with open(path, encoding="utf-8") as fh:
            actual = fh.read()
        if actual != content:
            drifted.append((rel, f"{len(actual)} bytes on disk vs {len(content)} authored"))
    if drifted:
        print(f"CHECK FAILED - {len(drifted)} of {len(files)} files differ from what this "
              "script would author:")
        for rel, why in drifted:
            print(f"  x {rel}  ({why})")
        print("\nRe-run without --check to bring them back into line.")
        sys.exit(1)
    print(f"--check: all {len(files)} authored files match what is on disk.")
    print(f"  scene: {scene_notes[0]}")
    sys.exit(0)

print(f"Validation passed ({len(files)} files). Scene: {scene_notes[0]}")
for rel in sorted(files):
    print("  ", rel)
for rel, content in files.items():
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
print(f"\nWrote {len(files)} files.")
