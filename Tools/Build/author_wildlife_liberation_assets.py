#!/usr/bin/env python3
"""
Authors every serialized asset the Wildlife Liberation game mode needs
(GameModes.WildlifeLiberation = 40).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_wildlife_liberation_assets.py [--check]
                         python3 Tools/Build/author_wildlife_liberation_assets.py --population

--check validates without writing (CI / pre-commit use).

--population re-authors ONLY the layer that gets retuned - the fauna configs, the spawn
profiles, the cell configs and the kill target - and skips the one-shot BRING-UP sections that
clone the donor Rampage scene and register the arcade card. Use it for every roster, band or
target change.

  Why it exists: sections 7-10 rebuild MinigameWildlifeLiberation.unity from the CURRENT
  MinigameRampage.unity. That donor has moved on since bring-up (its controller field block no
  longer matches, so a full run asserts out), and even if it did match, re-cloning would
  overwrite this mode's scene with a fresh copy of somebody else's. A generator that authors
  both a one-time scene and a re-tunable data layer has to be able to run just the second half.

The arena geometry, the fauna bands and the wildlife roster are IMPORTED from
wildlife_cage_budget.py - which mirrors the C# generator's loops exactly - so the cage walls,
the pens that hold each tier of creature inside them, and the PhaseThresholds can never drift
apart behind a stale constant here.

See Assets/_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md for what these numbers mean.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv
# Re-tunable data layer only: fauna configs, spawn profiles, cell configs, kill target.
POPULATION_ONLY = "--population" in sys.argv

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wildlife_cage_budget as budget  # noqa: E402


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


INTENSITIES = [1, 2, 3, 4]

# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "SpawnableWildlifeCage":          guid("script/SpawnableWildlifeCage"),
    "WildlifeLiberationController":   guid("script/WildlifeLiberationController"),
    "WildlifeKillTurnMonitor":        guid("script/WildlifeKillTurnMonitor"),
    "WildlifeLiberationScoringRuleSO": guid("script/WildlifeLiberationScoringRuleSO"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameWildlifeLiberation":   guid("asset/ArcadeGameWildlifeLiberation"),
    "WildlifeLiberationScoringRule":  guid("asset/WildlifeLiberationScoringRule"),
    "MinigameWildlifeLiberation.unity": guid("asset/MinigameWildlifeLiberation.unity"),
    "Event_FaunaKilled":              guid("asset/Event_FaunaKilled"),
}
for _i in INTENSITIES:
    G_ASSET[f"SpawnableWildlifeCage{_i}.prefab"] = guid(f"asset/SpawnableWildlifeCage{_i}.prefab")
    G_ASSET[f"WildlifeCellConfig{_i}"] = guid(f"asset/WildlifeCellConfig{_i}")
    G_ASSET[f"WildlifeSpawnProfile{_i}"] = guid(f"asset/WildlifeSpawnProfile{_i}")

# One FaunaConfigurationSO per (species, intensity) - the spawner runs one loop per config.
# Every one carries the SAME arena-wide roam band.
#
# It used to be per (species, LEVEL, intensity). Lifeform levels are retired (Docs/ECOSYSTEM.md
# 39), so the two rows of one species that a level used to separate are one row now - see
# wildlife_cage_budget.ROSTER, where they were merged by summing their populations. The
# per-species population, and therefore the collider budget, is unchanged.
for _i in INTENSITIES:
    for _species, _seed, _cap, _prisms in budget.ROSTER:
        G_ASSET[f"Fauna/{_species}/{_i}"] = guid(f"asset/WildlifeFauna_{_species}_{_i}")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    # script types
    "SO_ArcadeGame":        "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":     "01f934d50526431a9392a6ceca1dc33d",
    "SpawnProfileSO":       "e8d8aa5d835249798a256e18f2f7d912",
    "FaunaConfigurationSO": "c778cfbe4dfc4c5c8401e40c17802311",
    "CellRuntimeDataSO":    "7ee853b7a8af463d97a65225b3a26674",
    "ScriptableEventString": "d3f39f579066605409b539400e8d7b94",
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
    "Vessel_Sparrow":     "7b7053dd065edb54baa3b831b90f4985",
    # arcade card art - shared with the Wildlife Blitz family (this is its multiplayer cousin)
    "IconActive":         "576d21301c622e9489beb58263f393cb",
    "IconInactive":       "ebb26aeda98ffe840ad60e7ab88c8a28",
    "CardBackground":     "325ba2d27b2b2c24c9a6b06681eecbe5",
    "PreviewClip":        "4fb5927c0dce75b4298b94514abc0150",
    # fauna prefabs
    "TadpolePrefab":      "c7fd418d426de8740ac888dcc23a5d24",
    "QuadFishPrefab":     "19615ed0c903b1041973d70593d4b0a3",
    "BrittlestarPrefab":  "c719f00ea7596c24185379994f7dc824",
    "SharkPrefab":        "a67ba7ddaecf6624ab37cd9f5f2210a6",
    "WormColonyPrefab":   "8f79c97ef2bd4624a730a96900e4daaa",
    "TadpoleBodyMat":     "5140ec1c42866e849927f442d5965f7f",
    # the cell's CellRuntimeDataSO - the spawn ring resolves its Cell through this, and it now
    # also owns the fauna-kill SOAP channel this mode scores on
    "RuntimeCellData":    "8d4e8398eedc76c4dadb8604f89b9e1b",
}

# Fauna COMPONENT fileIDs (FaunaConfigurationSO.FaunaPrefab is typed Fauna, so it points at
# the MonoBehaviour inside the prefab, never the GameObject).
FAUNA_FILEID = {
    "Tadpole":     5945480239701989318,   # Boid,       herbivore, 1 body prism
    "QuadFish":    4652232322436628206,   # LightFauna, herbivore, 1 body prism
    "Brittlestar": 5351160486092638538,   # LightFauna, herbivore, 10 body prisms
    "Shark":       5351160486092638538,   # LightFauna, PREDATOR,  11 body prisms
    "WormColony":                  1002,  # WormFauna,  apex omnivore colony
}
FAUNA_PREFAB = {
    "Tadpole":     EXISTING["TadpolePrefab"],
    "QuadFish":    EXISTING["QuadFishPrefab"],
    "Brittlestar": EXISTING["BrittlestarPrefab"],
    "Shark":       EXISTING["SharkPrefab"],
    "WormColony":  EXISTING["WormColonyPrefab"],
}

# Per-element sibling configs, reused verbatim (read-only species identity assets) so every
# species spreads across the full elemental palette - one base prefab, four identities.
PALETTE = {
    "Tadpole":     ["ede43cd3ab5943c58c646065c1f57a1f", "28c9a96388684fa0b3b10b9dbea56c70",
                    "72fa98519b534214b89e9c29c44b89da", "62a30981533145a5b66304c04e7c50e0"],
    "QuadFish":    ["4053ff006892420d8ca5efa51365570c", "5697aa8685514f2ca9b9de9638fce1a1",
                    "3107bcc776d54a74b110b883f10fba61", "414bce89d4dc495f87bbccfb02e5b847"],
    "Brittlestar": ["503de8d514bf4001a067b76f07c246c5", "26691ece54c94157aba5b832451ec2a2",
                    "eb3b0459459a4ee0b1212c181ce80a11", "135c28565d034815adaadd2e66233711"],
    "Shark":       ["58835b82ea284255855af2649ef185a5", "a690f25bf21e486ba0e500563b90f1ea",
                    "eaf56c14345740849f35fc84467059e9", "78ce842bb8554d748af1e96abf430137"],
    "WormColony":  ["c1a7e2b45f0d4c1e8a6b9d3f2e7c5a10", "a3d59c8e71b24f6a9c0e4d8b5f172c33",
                    "e7f1b3a2c9d84e5fb6a08c7d4e392b55", "b9c4d7e2a1f34b8cd5e6f0a39b8d1c77"],
}

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
PREVIEW_FILEID = 241334157148977051

# The kill target - the race metric, summed PER DOMAIN. The 25%/50% milestone rungs are
# fractions of this (so 8 and 15), and moving it moves the whole progress ladder.
#
# 30, down from the 250 this mode shipped with (requested 2026-08). That is a ~8x shorter match,
# which is worth naming beside the roam band below: the cage is grazeable now, and how far it
# erodes is a function of how long a match lasts.
WILDLIFE_KILL_TARGET = 30

# `ElementalComebackSystem`: bonusLevels = deficit x rate, so THE RATE IS A FUNCTION OF THE
# TARGET and re-targeting a mode silently kills it. That trap is recorded twice already (Dog
# Fight, then The Bends 20x harder) and this is its third outing: the card inherited Rampage's
# 0.01 against a target 8x smaller, so a quarter-of-target deficit only ever bought 0.625 of a
# level, and 250 -> 30 would have taken that to 0.075 - a comeback system that does nothing.
#
# 0.35 puts a quarter-of-target deficit (7.5 kills) at 2.6 levels, which is Dog Fight's curve
# (90 x 0.12 = 2.7) - the nearest sibling by structure: same vessel, same "many small
# increments" race. The shipped family spans 1.25 (Scarab Scramble) to 5.0 (Rampage/Ribcage).
# The assert at the bottom FAILS the build if this ever drops back under one whole level.
COMEBACK_RATE = 0.35

SPAWN_RING_RADIUS = budget.SPAWN_RING_RADIUS
# The cell must SENSE mass across the whole roam band (0..1180) so a creature anywhere in the
# arena can find a player's trail; the membrane visual is 1200, so sensing the whole arena is
# free of any visual change. Without this the density grids would only cover the membrane's own
# radius.
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


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


# ── 1. .cs.meta for the four new scripts ─────────────────────────────────────
SCRIPT_PATHS = {
    "SpawnableWildlifeCage":          "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableWildlifeCage.cs",
    "WildlifeLiberationController":   "Assets/_Scripts/Controller/Arcade/WildlifeLiberationController.cs",
    "WildlifeKillTurnMonitor":        "Assets/_Scripts/Controller/Arcade/TurnMonitors/WildlifeKillTurnMonitor.cs",
    "WildlifeLiberationScoringRuleSO": "Assets/_Scripts/Controller/Arcade/Scoring/WildlifeLiberationScoringRuleSO.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. The fauna-kill SOAP channel + its wiring on the cell runtime SO ───────
# Fauna.Die publishes an attributed kill here; StatsManager (which already holds this runtime
# SO) subscribes in code and turns it into IRoundStats.LifeformsKilled. A ScriptableEvent
# rather than a static event, per the SOAP policy - and on the runtime SO rather than on each
# fauna prefab, so no creature prefab needs a new wire.
emit("Assets/_SO_Assets/Event Channels/Event_FaunaKilled.asset",
     HEADER_FOR(EXISTING["ScriptableEventString"], "Event_FaunaKilled") +
     "  CategoryIndex: 0\n  Description: Raised with the KILLER'S NAME when a fauna dies to an\n"
     "    attributed force (body prisms shot out, or a crystal joust). Ecology-internal deaths\n"
     "    (starvation, predation) are deliberately not published.\n"
     "  _debugLogEnabled: 0\n  _debugValue:\n")
emit("Assets/_SO_Assets/Event Channels/Event_FaunaKilled.asset.meta",
     asset_meta(G_ASSET["Event_FaunaKilled"]))

RUNTIME_CELL_PATH = "Assets/_SO_Assets/Cell Data/Runtime Cell Data.asset"
with open(os.path.join(ROOT, RUNTIME_CELL_PATH), encoding="utf-8") as fh:
    runtime_cell = fh.read()
if "OnFaunaKilled:" not in runtime_cell:
    anchor = re.search(r"^  OnFaunaHeartsChanged: \{[^}]*\}\n", runtime_cell, re.M)
    assert anchor, "OnFaunaHeartsChanged not found in the cell runtime SO"
    runtime_cell = runtime_cell.replace(
        anchor.group(0),
        anchor.group(0) +
        f"  OnFaunaKilled: {{fileID: 11400000, guid: {G_ASSET['Event_FaunaKilled']}, type: 2}}\n", 1)
emit(RUNTIME_CELL_PATH, runtime_cell)


# ── 3. SpawnableWildlifeCage prefabs - ONE VARIANT PER INTENSITY ─────────────
# Same script, same seed, only intensityTier differs; BuildParameterHash keeps their generation
# caches distinct. The tier picks a row of SpawnableWildlifeCage.ShellPlans - which cage is a
# geodesic sphere and which is a boxing ring, and how tightly each is woven. The SHELL COUNT is
# never varied: each shell walls in one tier of wildlife.
for i in INTENSITIES:
    emit(f"Assets/_Prefabs/Spawnables/SpawnableWildlifeCage{i}.prefab", f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5260000000000401
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5260000000000402}}
  - component: {{fileID: 5260000000000403}}
  m_Layer: 0
  m_Name: SpawnableWildlifeCage
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5260000000000402
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000401}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5260000000000403
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000401}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableWildlifeCage']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: 40
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
  intensityTier: {i}
""")
    emit(f"Assets/_Prefabs/Spawnables/SpawnableWildlifeCage{i}.prefab.meta",
         prefab_meta(G_ASSET[f"SpawnableWildlifeCage{i}.prefab"]))


# ── 4. Scoring rule ──────────────────────────────────────────────────────────
# metric 7 = ScoringMetric.LifeformsKilled. Golf: the winning hunter carries a finish time,
# everyone else a sentinel, so lower is better.
emit("Assets/_SO_Assets/Scoring Rules/WildlifeLiberationScoringRule.asset",
     HEADER_FOR(G_SCRIPT["WildlifeLiberationScoringRuleSO"], "WildlifeLiberationScoringRule") +
     "  metric: 7\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/WildlifeLiberationScoringRule.asset.meta",
     asset_meta(G_ASSET["WildlifeLiberationScoringRule"]))


# ── 5. Arcade game config ────────────────────────────────────────────────────
# SPARROW ONLY: a single entry in Vessels is what drives all three enforcement layers
# (GameDataSO.SyncFromArcadeGame's launcher clamp, ServerPlayerVesselInitializer's server-side
# spawn clamp, and the AI clamp in ServerPlayerVesselInitializerWithAI). 1-4 players.
#
# MinDomainsAllowed 2, like Ribcage: this is a DOMAIN race, and a one-domain lobby has no race
# in it - the single colour would cross the target unopposed. Note 4 players over 3 domains
# always means teammates; that is the intended shape, not a defect (see
# WildlifeLiberationScoringRuleSO for why a free-for-all was tried here and reverted).
emit("Assets/_SO_Assets/Games/ArcadeGameWildlifeLiberation.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameWildlifeLiberation") + f"""  Mode: 40
  IsMultiplayer: 1
  DisplayName: Wildlife Liberation
  Description: Three cages, and wildlife everywhere - swarms, big ones and worse,
    scattered from the open water you spawn in all the way to the core. Shoot what
    swims past, break in after what does not. You never know what the next one is.
    First domain to the kill count takes it.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  PreviewClip: {{fileID: {PREVIEW_FILEID}, guid: {EXISTING['PreviewClip']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameWildlifeLiberation
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Sparrow']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 427
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameWildlifeLiberation.asset.meta",
     asset_meta(G_ASSET["ArcadeGameWildlifeLiberation"]))


# ── 6. Cell configs + spawn profiles + the wildlife roster ───────────────────
#
# One CellConfigDataSO PER INTENSITY, because PhaseThresholds must ride ITS OWN baseline (the
# cages run 9,206 - 12,870 prisms), and one SpawnProfileSO per intensity because the roster
# scales with intensity too ("later intensities will have more fauna").
#
# NO NUCLEUS by design: the jail's core is a STRUCTURE, not a nucleus, so there is no control
# zone and herbivores keep the legacy opposing-domain diet. That matters here - it is what makes
# a player's trail inside a room into food, and the creature swarm into a real hazard rather
# than scenery.
FOLDER = "Assets/_SO_Assets/Cell Configs/Wildlife Liberation Cell"
emit(FOLDER + ".meta", meta(guid("folder/WildlifeLiberationCell"), folder=True))


def fauna_asset_name(species, intensity):
    return f"Wildlife {species} {intensity}"


def fauna_asset_path(species, intensity):
    return f"{FOLDER}/{fauna_asset_name(species, intensity)}.asset"


# The tadpole swarm keeps the authored Blob expression (small body, long tail prisms, 90s
# starvation clock, forager). Everything else keeps its prefab as authored.
TADPOLE_VARIANT = f"""  Variant:
    Enabled: 1
    BaseBodyScale: 0.4
    BodyPrismScale: {{x: 0.8, y: 0.8, z: 7}}
    BodyMaterial: {{fileID: 2100000, guid: {EXISTING['TadpoleBodyMat']}, type: 2}}
    StarvationSeconds: 90
    Forager: 1
    CohesionRadius: 50
    BehaviorUpdateRate: 1.5
    TrailBlockInteractionRadius: 30
    GoalWeight: 3
    MinSpeed: 10
    MaxSpeed: 15
    OverrideAudio: 0
    AudioLoopEvent:
      Guid:
        Data1: 0
        Data2: 0
        Data3: 0
        Data4: 0
      Path:
    AudioMinDistance: -1
    AudioMaxDistance: -1
"""
PLAIN_VARIANT = """  Variant:
    Enabled: 0
"""

for i in INTENSITIES:
    for species, seed, cap, _prisms in budget.roster_for(i):
        # ONE band for every species: the whole arena. See wildlife_cage_budget.ROAM_INNER for
        # what replaced the per-room pens and what that costs the cage.
        inner, outer = budget.ROAM_INNER, budget.ROAM_OUTER
        palette = "".join(
            f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in PALETTE[species])

        # Reproduction: the population DRIVER above the seed floor. Bigger creatures convert
        # prey to offspring more slowly, so the swarm churns and the kaiju does not.
        feeds = {"Tadpole": 24, "QuadFish": 20, "Brittlestar": 16, "Shark": 8, "WormColony": 0}[species]
        cooldown = {"Tadpole": 10, "QuadFish": 12, "Brittlestar": 14, "Shark": 30, "WormColony": 60}[species]

        emit(fauna_asset_path(species, i),
             HEADER_FOR(EXISTING["FaunaConfigurationSO"], fauna_asset_name(species, i)) +
             f"""  FaunaPrefab: {{fileID: {FAUNA_FILEID[species]}, guid: {FAUNA_PREFAB[species]}, type: 3}}
  InitialSpawnCount: {seed}
  PopulationSize: {seed}
  SpawnProbability: 1
  FeedsPerOffspring: {feeds}
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: {cooldown}
  MaxLivePopulation: {cap}
  ReleaseTier: 0
  BandInnerRadius: {inner:.0f}
  BandOuterRadius: {outer:.0f}
  CenterFocusBias: 0
  Element: 0
""" + (TADPOLE_VARIANT if species == "Tadpole" else PLAIN_VARIANT) + f"""  SpreadElements: 1
  ElementPalette:
{palette}""")
        emit(fauna_asset_path(species, i) + ".meta",
             asset_meta(G_ASSET[f"Fauna/{species}/{i}"]))

    supported = "".join(
        f"  - {{fileID: 11400000, guid: {G_ASSET[f'Fauna/{species}/{i}']}, type: 2}}\n"
        for species, _s, _c, _p in budget.ROSTER)

    seed_total, cap_total, prism_total = budget.fauna_totals(i)
    emit(f"{FOLDER}/Wildlife Spawn Profile {i}.asset",
         HEADER_FOR(EXISTING["SpawnProfileSO"], f"Wildlife Spawn Profile {i}") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras: []
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 2
  InitialFaunaReleaseTier: 0
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 20
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 0
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 0
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas:
{supported}""")
    emit(f"{FOLDER}/Wildlife Spawn Profile {i}.asset.meta",
         asset_meta(G_ASSET[f"WildlifeSpawnProfile{i}"]))

    n, v, danger = budget.cumulative(i)
    th = budget.phase_thresholds(n, v)
    forms = " / ".join(budget.shell_rows(i, s)["form"] for s in range(budget.SHELL_COUNT))
    emit(f"{FOLDER}/Wildlife Liberation Cell Config {i}.asset",
         HEADER_FOR(EXISTING["CellConfigDataSO"], f"Wildlife Liberation Cell Config {i}") +
         f"""  CellName: Wildlife Liberation
  Description: The three-layer jail at intensity {i} - cages at 1050 / 600 / 200 ({forms}),
    {n} prisms of bar and {danger} danger traps in the core. Holds {seed_total} creatures at
    seed and up to {cap_total} ({prism_total} body prisms), every species roaming the whole
    arena on one shared band ({budget.ROAM_INNER:.0f}..{budget.ROAM_OUTER:.0f}). NO NUCLEUS
    by design. PhaseThresholds ride THIS intensity's own baseline; regenerate with
    Tools/Build/author_wildlife_liberation_assets.py after any change rather than hand-editing.
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['Membrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'WildlifeSpawnProfile{i}']}, type: 2}}
  EnvironmentPrefab: {{fileID: 5260000000000403, guid: {G_ASSET[f'SpawnableWildlifeCage{i}.prefab']}, type: 3}}
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
    emit(f"{FOLDER}/Wildlife Liberation Cell Config {i}.asset.meta",
         asset_meta(G_ASSET[f"WildlifeCellConfig{i}"]))


# ── 7-10. BRING-UP ONLY: the scene clone, the arcade card's registrations ────
# Skipped by --population. These rebuild the scene from the donor and register the card;
# both are one-time acts, and re-running the clone would overwrite this mode's scene with
# a fresh copy of whatever MinigameRampage.unity has since become.
if not POPULATION_ONLY:
    # ── 7. Scene: clone MinigameRampage, swap the mode-specific wiring ───────────
    DONOR_SCENE = os.path.join(ROOT, "Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity")
    with open(DONOR_SCENE, encoding="utf-8") as fh:
        scene = fh.read()

    # 7a. turn monitor script swap (field set is identical - base TurnMonitor fields only)
    scene, n = re.subn(EXISTING["RampagePrismTurnMonitor"], G_SCRIPT["WildlifeKillTurnMonitor"], scene)
    assert n == 1, f"turn monitor guid appeared {n} times"

    # 7b. controller script swap + its serialized field block
    scene, n = re.subn(EXISTING["RampageController"], G_SCRIPT["WildlifeLiberationController"], scene)
    assert n == 1, f"controller guid appeared {n} times"

    OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}
      arenaCell: {{fileID: 1700000065}}
      aiRetargetSeconds: 1.5
    """
    NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['WildlifeLiberationScoringRule']}, type: 2}}
      arenaCell: {{fileID: 1700000065}}
      firstMilestoneFraction: 0.25
      secondMilestoneFraction: 0.5
      progressSampleSeconds: 0.5
      aiRetargetSeconds: 2.5
    """
    assert OLD_FIELDS in scene, "controller field block not found in donor scene"
    scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

    # 7c. Cell: swap the donor's single config for the FOUR per-intensity configs and flip the
    # choice mode to IntensityWise - the platform's own way to vary a cell by intensity.
    OLD_CELL = f"""  CellConfigs:
      - {{fileID: 11400000, guid: {EXISTING['RampageCellConfig']}, type: 2}}
      cellTypeChoiceOptions: 0
    """
    NEW_CELL = "  CellConfigs:\n" + "".join(
        f"  - {{fileID: 11400000, guid: {G_ASSET[f'WildlifeCellConfig{i}']}, type: 2}}\n"
        for i in INTENSITIES) + "  cellTypeChoiceOptions: 1\n"
    assert OLD_CELL in scene, "donor Cell config block not found"
    scene = scene.replace(OLD_CELL, NEW_CELL)

    # 7d. Spawn OUTSIDE the jail, on the equator. The donor's four authored transforms sit at +/-50
    # - dead centre of the core cage, so everyone would start locked in the maximum-security room
    # with the kaiju. Switch to the computed cell spawn ring (CellSpawnFormation, all facing the
    # cell) with a radius FLOOR, because this cell has no nucleus for the ring to measure off.
    # spawnFormation 1 = EquatorialRing: everyone on ONE horizontal circle, so nobody is dropped on
    # a boxed cage's corner while someone else gets a flat face.
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

    # 7e. SPARROW-ONLY, third layer: the AI templates. ServerPlayerVesselInitializerWithAI clamps
    # these through GameDataSO.ClampVesselToGame anyway, but authoring the right class here means
    # the scene is honest on its own and the clamp never has to fire. 3 = Rhino (Rampage's donor
    # value), 11 = Sparrow.
    scene, n = re.subn(r"^  - vesselClass: 3$", "  - vesselClass: 11", scene, flags=re.M)
    assert n == 4, f"expected 4 AI vessel templates, patched {n}"

    emit("Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity", scene)
    emit("Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity.meta",
         scene_meta(G_ASSET["MinigameWildlifeLiberation.unity"]))


    # ── 8. Register the card in the party-games list ─────────────────────────────
    LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
    with open(os.path.join(ROOT, LIST_PATH), encoding="utf-8") as fh:
        games = fh.read()
    entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameWildlifeLiberation']}, type: 2}}\n"
    if entry not in games:
        assert games.endswith("\n")
        games = games + entry
    emit(LIST_PATH, games)


    # ── 9. Always-unlocked so the card is clickable on a fresh account ───────────
    PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
    with open(os.path.join(ROOT, PROG_PATH), encoding="utf-8") as fh:
        prog = fh.read()
    if re.search(r"^  alwaysUnlockedModes:\n(  - \d+\n)*  - 40\n", prog, re.M) is None:
        prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 40\n", prog, count=1)
        assert n == 1, "alwaysUnlockedModes block not found"
    emit(PROG_PATH, prog)


    # ── 10. Build settings ───────────────────────────────────────────────────────
    BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
    with open(os.path.join(ROOT, BUILD_PATH), encoding="utf-8") as fh:
        build = fh.read()
    if "MinigameWildlifeLiberation.unity" not in build:
        anchor = re.search(
            r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRampage\.unity\n"
            r"    guid: [0-9a-f]{32}\n)", build)
        assert anchor, "Rampage scene entry not found in EditorBuildSettings"
        build = build.replace(anchor.group(1), anchor.group(1) +
                              "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity\n"
                              f"    guid: {G_ASSET['MinigameWildlifeLiberation.unity']}\n")
    emit(BUILD_PATH, build)


# ── 11. End-game condition target ────────────────────────────────────────────
# The shared overrides asset is what FrogletTools > Game Modes > End Game Conditions edits. A
# missing key would silently fall back to the C# field initializer, so author both the live and
# the build-baseline value explicitly.
#
# SET the value, never merely insert it. The first version only inserted a missing key, so once
# the key existed this script could no longer move the target - and WILDLIFE_KILL_TARGET above
# would have quietly become a comment rather than the source of truth. Live and Build are held
# equal so a clean checkout is already in sync (the skill's rule; the build-time auto-restore
# copies Build onto Live).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
with open(os.path.join(ROOT, END_PATH), encoding="utf-8") as fh:
    endcond = fh.read()
for after_key, new_key in (("ribcagePrismTarget", "wildlifeKillTarget"),
                           ("ribcagePrismTargetBuild", "wildlifeKillTargetBuild")):
    line = f"  {new_key}: {WILDLIFE_KILL_TARGET}\n"
    m = re.search(rf"^  {new_key}: (\d+)\n", endcond, re.M)
    if m:
        endcond = endcond.replace(m.group(0), line, 1)
        continue
    m = re.search(rf"^  {after_key}: (\d+)\n", endcond, re.M)
    assert m, f"{after_key} not found in {END_PATH} - run author_ribcage_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + line, 1)
emit(END_PATH, endcond)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

all_new = list(G_SCRIPT.values()) + list(G_ASSET.values()) + [guid("folder/WildlifeLiberationCell")]
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

# .meta files THIS script owns are excluded from the collision sweep - otherwise a second run
# flags its own (byte-identical) output as a collision and the script stops being idempotent.
# Ownership is by PATH, not by "did this run emit it": under --population the bring-up sections
# are skipped, and their metas are still this generator's own output sitting on disk.
owned_metas = {os.path.normpath(os.path.join(ROOT, rel)) for rel in files if rel.endswith(".meta")}
owned_metas |= {os.path.normpath(os.path.join(ROOT, rel)) for rel in (
    "Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity.meta",
)}

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
for species, pal in PALETTE.items():
    for g in pal:
        if g not in existing_guids:
            errors.append(f"{species} element-palette GUID {g} does not resolve to any asset")

# the scene must no longer mention the donor's mode-specific guids. Read from disk under
# --population (this run did not re-clone it) so the assertions still hold against what SHIPS.
_scene_rel = "Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity"
if _scene_rel in files:
    sc = files[_scene_rel]
else:
    with open(os.path.join(ROOT, _scene_rel), encoding="utf-8") as _fh:
        sc = _fh.read()
for name in ("RampageController", "RampagePrismTurnMonitor", "RampageCellConfig", "RampageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("WildlifeLiberationController", "WildlifeKillTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["WildlifeLiberationScoringRule"] not in sc:
    errors.append("cloned scene missing the scoring rule reference")
for i in INTENSITIES:
    if G_ASSET[f"WildlifeCellConfig{i}"] not in sc:
        errors.append(f"cloned scene missing cell config {i}")
if "  cellTypeChoiceOptions: 1\n" not in sc:
    errors.append("scene Cell is not on CellTypeChoiceOptions.IntensityWise - "
                  "the per-intensity configs would never be selected")
if "vesselClass: 3" in sc:
    errors.append("scene still authors a non-Sparrow AI vessel class")
if sc.count("vesselClass: 11") != 4:
    errors.append("scene does not author 4 Sparrow AI templates")

# the runtime cell SO must carry the fauna-kill channel, or nothing scores
if f"OnFaunaKilled: {{fileID: 11400000, guid: {G_ASSET['Event_FaunaKilled']}" not in files[RUNTIME_CELL_PATH]:
    errors.append("Runtime Cell Data is missing the OnFaunaKilled channel - no kill would score")

# Sparrow-only must be a SINGLE entry, or the clamps let another hull through
arcade = files["Assets/_SO_Assets/Games/ArcadeGameWildlifeLiberation.asset"]
vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", arcade, re.M)
if not vessels or vessels.group(1).count("- {fileID") != 1:
    errors.append("ArcadeGameWildlifeLiberation must author EXACTLY ONE vessel (Sparrow)")
elif EXISTING["Vessel_Sparrow"] not in vessels.group(1):
    errors.append("ArcadeGameWildlifeLiberation's single vessel is not Sparrow")


# serialized MonoBehaviour keys must exist on the C# class (asset-surgery §3)
def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    # NOTE the modifier group must span static/const/readonly in any order, not just
    # `readonly`. A narrower version silently reported `public const float OpenWaterInner` as
    # MISSING, and the caller concluded the C# was wrong when it was the regex that was too
    # tight - the same false-negative class the [SerializeField] branch below was added for.
    MODS = r"(?:(?:public|protected|private|internal|static|const|readonly|new)\s+)+"
    for m in re.finditer(MODS + TYPE + r"\s+(\w+)\s*(?:=|;|\{|=>)", src):
        out.add(m.group(1))
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    return out


CHECKS = [
    (f"{FOLDER}/Wildlife Liberation Cell Config {i}.asset",
     "Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs") for i in INTENSITIES
] + [
    (f"{FOLDER}/Wildlife Spawn Profile {i}.asset",
     "Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs") for i in INTENSITIES
] + [
    (fauna_asset_path(species, i),
     "Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs")
    for i in INTENSITIES for species, _s, _c, _p in budget.ROSTER
] + [
    ("Assets/_SO_Assets/Games/ArcadeGameWildlifeLiberation.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs"),
]

if "intensityTier" not in cs_fields(
        "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableWildlifeCage.cs"):
    errors.append("SpawnableWildlifeCage.cs has no 'intensityTier' field - the per-intensity "
                  "prefab variants would all build the same cage")
CAGE_CS = "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableWildlifeCage.cs"
with open(os.path.join(ROOT, CAGE_CS), encoding="utf-8") as _fh:
    _cage_src = _fh.read()

for cage_const in ("OpenWaterInner", "OpenWaterOuter", "RoomCount"):
    if cage_const not in cs_fields(CAGE_CS):
        errors.append(f"SpawnableWildlifeCage.cs has no '{cage_const}' - the open water outside "
                      f"the cages would not exist and the AI would never patrol it")
# RoomInner/RoomOuter are METHODS, which cs_fields (a FIELD scanner) cannot see - match the
# source. They are the cage's room geometry, and the AI hunters' patrol is their only consumer.
for room_fn in ("RoomInner", "RoomOuter"):
    if not re.search(rf"public static float {room_fn}\(int shell\)", _cage_src):
        errors.append(f"SpawnableWildlifeCage.cs has no '{room_fn}(int shell)' - the rooms would "
                      f"not exist as geometry and the AI hunters would never patrol them")
# The C# side of the roam band, so the two cannot drift: the asset numbers below are only
# meaningful if the cage still declares the same band.
for const, want in (("RoamInner", budget.ROAM_INNER), ("RoamOuter", budget.ROAM_OUTER)):
    m = re.search(rf"public const float {const} = ([\d.]+)f;", _cage_src)
    if not m:
        errors.append(f"SpawnableWildlifeCage.cs has no 'RoamInner/RoamOuter' const - the one "
                      f"band every species roams would have no single source")
    elif float(m.group(1)) != want:
        errors.append(f"SpawnableWildlifeCage.{const} is {m.group(1)} but "
                      f"wildlife_cage_budget says {want} - the band and the arena have drifted")
for band_field in ("BandInnerRadius", "BandOuterRadius"):
    if band_field not in cs_fields("Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs"):
        errors.append(f"FaunaConfigurationSO.cs has no '{band_field}' - the wildlife would have "
                      f"no band at all and would spawn on the cell centre")
if "OnFaunaKilled" not in cs_fields("Assets/_Scripts/Utility/DataContainers/CellRuntimeDataSO.cs"):
    errors.append("CellRuntimeDataSO.cs has no 'OnFaunaKilled' channel - no kill would score")

# A comeback rate is a function of the TARGET (bonusLevels = deficit x rate), so re-targeting a
# mode silently disarms it. Dog Fight recorded the trap, The Bends hit it 20x harder and added
# this assert; this mode is its third outing. A quarter-of-target deficit must buy at least one
# WHOLE element level, or the trailing domain gets a rounding error instead of a comeback.
_quarter_deficit_levels = (WILDLIFE_KILL_TARGET / 4.0) * COMEBACK_RATE
if _quarter_deficit_levels < 1.0:
    errors.append(
        f"ComebackRatePerScoreDeficit {COMEBACK_RATE} against a kill target of "
        f"{WILDLIFE_KILL_TARGET} buys only {_quarter_deficit_levels:.2f} element levels at a "
        f"quarter-of-target deficit - under one whole level the comeback does nothing. Rescale "
        f"COMEBACK_RATE with the target (>= {4.0 / WILDLIFE_KILL_TARGET:.3f}).")

SO_BASE = {"CellName", "Description", "Icon", "Difficulty", "CellEndGameScore", "Mode",
           "IsMultiplayer", "DisplayName", "IconActive", "IconInactive", "CardBackground",
           "PreviewClip", "GolfScoring", "SceneName"}
for asset_path, cs_path in CHECKS:
    keys = set(re.findall(r"^  (\w+):", files[asset_path], re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = cs_fields(cs_path) | SO_BASE
    for extra in ("Assets/_Scripts/ScriptableObjects/SO_Game.cs",):
        if os.path.exists(os.path.join(ROOT, extra)):
            known |= cs_fields(extra)
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on "
                      f"{os.path.basename(cs_path)}: {sorted(unknown)}")

# Every fauna config must carry the SAME band, and it must be the whole arena. This replaced a
# check that each species sat strictly inside its own room's walls; the property worth asserting
# now is the opposite one - that no species has quietly been given a narrower band than its
# neighbours, which is how a "tier" would creep back in.
_bands = set()
for i in INTENSITIES:
    for species, _s, _c, _p in budget.ROSTER:
        body = files[fauna_asset_path(species, i)]
        inner = float(re.search(r"^  BandInnerRadius: ([\d.]+)", body, re.M).group(1))
        outer = float(re.search(r"^  BandOuterRadius: ([\d.]+)", body, re.M).group(1))
        _bands.add((inner, outer))
        # Outside the membrane is unreachable mass and unreachable prey; a band that does not
        # reach past the outer cage would put the players' own spawn ring out of bounds.
        if not (0.0 <= inner < outer <= 1200.0):
            errors.append(f"{fauna_asset_name(species, i)}: band {inner}..{outer} is not "
                          f"inside the membrane (1200)")
        if outer <= budget.SHELL_RADII[0]:
            errors.append(f"{fauna_asset_name(species, i)}: band outer {outer} does not "
                          f"reach past the outer cage ({budget.SHELL_RADII[0]}), so nothing "
                          f"would live in the open water the players spawn in")
if len(_bands) != 1:
    errors.append(f"the wildlife carries {len(_bands)} different bands {sorted(_bands)} - every "
                  f"species must share the one roam band, or the tiers re-form by radius")
elif _bands != {(budget.ROAM_INNER, budget.ROAM_OUTER)}:
    errors.append(f"the roam band on the assets {sorted(_bands)} is not "
                  f"{(budget.ROAM_INNER, budget.ROAM_OUTER)} from wildlife_cage_budget")

# ── PRUNE: assets this generator used to emit and no longer does ────────────
# The roster changes shape between passes (a species is dropped, a room is added), and a
# generator that only ever WRITES leaves the retired assets behind - stale FaunaConfigurationSOs
# that nothing references but that read as live content to the next person. Scoped hard: only
# this mode's own folder, only files matching this generator's own naming, and only ones this
# run did not emit.
stale = []
_folder_abs = os.path.join(ROOT, FOLDER)
if os.path.isdir(_folder_abs):
    _emitted = {os.path.normpath(os.path.join(ROOT, rel)) for rel in files}
    for fn in sorted(os.listdir(_folder_abs)):
        if not fn.startswith("Wildlife ") or not fn.endswith((".asset", ".asset.meta")):
            continue
        full = os.path.normpath(os.path.join(_folder_abs, fn))
        if full not in _emitted:
            stale.append(full)

if errors:
    print("VALIDATION FAILED — nothing written:")
    for e in errors:
        print("  ✗", e)
    sys.exit(1)

print(f"Validation passed ({len(files)} files).")
for rel in sorted(files):
    print("  ", rel)
if stale:
    print(f"\nStale (retired by this roster, will be DELETED): {len(stale)}")
    for f in stale:
        print("   -", os.path.relpath(f, ROOT))

if CHECK_ONLY:
    print("\n--check: no files written, nothing deleted.")
    sys.exit(0)

for f in stale:
    os.remove(f)

for rel, content in files.items():
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
print(f"\nWrote {len(files)} files.")
