#!/usr/bin/env python3
"""
One-shot authoring script for the Hesperides garden cell + the PhyllotacticFlora species.

Unity asset surgery: writes the .meta files (deterministic guids, so a re-run is idempotent),
the three flora prefabs, the environment prefab, the twelve canonical per-element flora configs,
and the Hesperides cell folder. Kept in-repo so the exact authored values are reviewable and the
whole set can be regenerated after a tuning pass instead of being hand-edited in twelve places.

Run from the repo root:  python3 Tools/ecosim/gen_hesperides_assets.py
"""
import hashlib
import os

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def guid(name: str) -> str:
    """Deterministic 32-hex guid from a stable name (idempotent re-runs)."""
    return hashlib.md5(("cosmicshore/hesperides/" + name).encode()).hexdigest()


def write(rel, text):
    path = os.path.join(REPO, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    return path


def meta_script(rel, g):
    write(rel + ".meta", f"""fileFormatVersion: 2
guid: {g}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
""")


def meta_prefab(rel, g):
    write(rel + ".meta", f"""fileFormatVersion: 2
guid: {g}
PrefabImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
""")


def meta_asset(rel, g):
    write(rel + ".meta", f"""fileFormatVersion: 2
guid: {g}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
""")


def meta_folder(rel, g):
    write(rel + ".meta", f"""fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
""")


# ── Existing guids this authoring depends on ─────────────────────────────────
SCRIPT_FLORA_CONFIG = "a32a297a7606432885f4d3e1f83bea9a"   # FloraConfigurationSO
SCRIPT_FAUNA_CONFIG = "c778cfbe4dfc4c5c8401e40c17802311"   # FaunaConfigurationSO
SCRIPT_SPAWN_PROFILE = "e8d8aa5d835249798a256e18f2f7d912"  # SpawnProfileSO
SCRIPT_CELL_CONFIG = "01f934d50526431a9392a6ceca1dc33d"    # CellConfigDataSO

RUNTIME_CELL_DATA = "8d4e8398eedc76c4dadb8604f89b9e1b"     # Runtime Cell Data.asset
RUNTIME_GAME_DATA = "b35f33752bb10a44cb5033b5670f50aa"
EVT_LIFEFORM_CREATED = "0ec64678e3c91034faed17b6e66ded9d"
EVT_LIFEFORM_DESTROYED = "af79f31492a261e49826374c21ee2234"

HEALTH_BLOCK = ("6313579230210663873", "1488a2ac58b2b4c43b14f84206bd9195")  # HealthBlock.prefab
BRANCH_SPINDLE = ("5157459880619768690", "f7ec1bbfe690a184b935434a6e0dcb7a")  # Spindles/Branch
ENV_PRISM = ("4563009547826722997", "ed9defc56162b4b4588e61c20984b6d9")     # environment prism

# Blob-family cell furniture (Hesperides is a Blob-family cell, same as Yggdra).
MEMBRANE = ("346633111830028674", "6e330f85972faf843b8a128e7166f7b5")
NUCLEUS = ("7555898194514117247", "b9cf1833fa2493d4b8724ccb6740fb3a")
CYTOPLASM = ("639495419069806261", "9cacd903fcf4643459f5f14ac811bb20")
MODIFIER = ("8058406376250941529", "daa37ae0e7af4b04383c1c4e6e76817d")

# Blob fauna configs - the same three species, reused so the garden's food web is the
# platform's, not a bespoke one.
BLOB_FAUNA = "b3e5efaa5053ab748943e4c3433f72d1"
BLOB_SHARK = "178e4d83e2fd4a4bae1ab253d7766ea7"
BLOB_TADPOLE = "fb217959401746e1b09cac81ffce665b"

SCRIPT_PHYLLOTACTIC = guid("script/PhyllotacticFlora")
SCRIPT_SPAWNABLE = guid("script/SpawnableHesperides")
SCRIPT_PLANTING_SITE = guid("script/FloraPlantingSite")

ELEMENTS = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}


# ── Flora species: form is prefab data, element is config data ───────────────
SPECIES = {
    # Arbor: one trunk, strong tropism, branching, wide flaring whorls. The canopy.
    "Arbor": dict(
        initialTips=1, maxTips=10, maxDepth=24, maxTotalSpawnedObjects=260,
        growthsPerTick=3, maxSpawnsPerFrame=1,
        segmentLength=17, segmentTaper=0.96,
        tropism=0.6, wander=0.16, spreadDegrees=8,
        branchStartDepth=3, branchChance=0.32, branchAngle=36,
        whorlStartDepth=6, whorlEvery=3, whorlLeaves=5, whorlRadius=11, whorlFlare=1.1,
        leafSize=(4.5, 4.5, 1.2), growPeriod=1.2, plantPeriod=14, plantRadiusFraction=0.55,
        healthBlocksForMaturity=1, minHealthBlocks=0,
    ),
    # Tendril: several tips, weak tropism, heavy wander, sparse paired leaves. The climber.
    "Tendril": dict(
        initialTips=3, maxTips=8, maxDepth=34, maxTotalSpawnedObjects=120,
        growthsPerTick=4, maxSpawnsPerFrame=1,
        segmentLength=11, segmentTaper=1.0,
        tropism=0.12, wander=0.5, spreadDegrees=55,
        branchStartDepth=6, branchChance=0.12, branchAngle=48,
        whorlStartDepth=4, whorlEvery=3, whorlLeaves=2, whorlRadius=5, whorlFlare=0.2,
        leafSize=(2.2, 5.4, 1.1), growPeriod=0.7, plantPeriod=9, plantRadiusFraction=0.5,
        healthBlocksForMaturity=1, minHealthBlocks=0,
    ),
    # Rosette: no rise to speak of, whorls from the first node, many leaves. The bed cover.
    "Rosette": dict(
        initialTips=1, maxTips=4, maxDepth=8, maxTotalSpawnedObjects=90,
        growthsPerTick=2, maxSpawnsPerFrame=1,
        segmentLength=5, segmentTaper=1.05,
        tropism=0.9, wander=0.05, spreadDegrees=4,
        branchStartDepth=99, branchChance=0.0, branchAngle=20,
        whorlStartDepth=0, whorlEvery=1, whorlLeaves=8, whorlRadius=9, whorlFlare=2.4,
        leafSize=(5.6, 5.6, 1.0), growPeriod=1.6, plantPeriod=7, plantRadiusFraction=0.6,
        healthBlocksForMaturity=1, minHealthBlocks=0,
    ),
}

# Per-element expression (FloraVariantTuning), following the authored gyroid convention:
# the element's identity is largely the leaf PRISM and the growth TEMPO.
ELEMENT_TUNING = {
    "Charge": dict(leaf_mul=(0.9, 0.9, 1.0), grow_mul=1.0, shield=1.0, budget_mul=0.85),
    "Mass":   dict(leaf_mul=(1.25, 1.25, 1.6), grow_mul=1.3, shield=0.0, budget_mul=1.2),
    "Space":  dict(leaf_mul=(0.55, 0.55, 3.2), grow_mul=1.8, shield=0.0, budget_mul=0.7),
    "Time":   dict(leaf_mul=(1.0, 1.0, 1.0), grow_mul=0.5, shield=0.0, budget_mul=1.0),
}


def v3(t):
    return "{x: %g, y: %g, z: %g}" % t


def flora_prefab(name, s):
    """A PhyllotacticFlora prefab: root + script + an elemental crystal child (the heart)."""
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &6774738432424273872
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5739209271613175231}}
  - component: {{fileID: 7514956980722975813}}
  m_Layer: 0
  m_Name: {name}Flora
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5739209271613175231
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 6774738432424273872}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: 1648936725651798773}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &7514956980722975813
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 6774738432424273872}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_PHYLLOTACTIC}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  gameData: {{fileID: 11400000, guid: {RUNTIME_GAME_DATA}, type: 2}}
  cellData: {{fileID: 11400000, guid: {RUNTIME_CELL_DATA}, type: 2}}
  healthPrism: {{fileID: {HEALTH_BLOCK[0]}, guid: {HEALTH_BLOCK[1]}, type: 3}}
  spindle: {{fileID: {BRANCH_SPINDLE[0]}, guid: {BRANCH_SPINDLE[1]}, type: 3}}
  healthBlocksForMaturity: {s['healthBlocksForMaturity']}
  minHealthBlocks: {s['minHealthBlocks']}
  shieldPeriod: 0
  autoInitialize: 1
  domain: 0
  onLifeFormCreated: {{fileID: 11400000, guid: {EVT_LIFEFORM_CREATED}, type: 2}}
  onLifeFormDestroyed: {{fileID: 11400000, guid: {EVT_LIFEFORM_DESTROYED}, type: 2}}
  leafSize: {v3(s['leafSize'])}
  growPeriod: {s['growPeriod']}
  PlantPeriod: {s['plantPeriod']}
  stunDuration: 1
  plantRadiusCellFraction: {s['plantRadiusFraction']}
  initialTips: {s['initialTips']}
  maxTips: {s['maxTips']}
  maxDepth: {s['maxDepth']}
  maxTotalSpawnedObjects: {s['maxTotalSpawnedObjects']}
  growthsPerTick: {s['growthsPerTick']}
  maxSpawnsPerFrame: {s['maxSpawnsPerFrame']}
  segmentLength: {s['segmentLength']}
  segmentTaper: {s['segmentTaper']}
  tropism: {s['tropism']}
  wander: {s['wander']}
  spreadDegrees: {s['spreadDegrees']}
  branchStartDepth: {s['branchStartDepth']}
  branchChance: {s['branchChance']}
  branchAngle: {s['branchAngle']}
  whorlStartDepth: {s['whorlStartDepth']}
  whorlEvery: {s['whorlEvery']}
  whorlLeaves: {s['whorlLeaves']}
  whorlRadius: {s['whorlRadius']}
  whorlFlare: {s['whorlFlare']}
{CRYSTAL_CHILD}"""


# The heart: the same CrystalSpace instance + ElementalCrystalImpactor + ImpactCollider
# override block every authored flora carries. A config's Element replaces it at spawn
# (LifeFormCrystal.EnsureElementalCrystal); this is the authored fallback that keeps the
# "every lifeform drops a crystal" invariant true even with no config.
CRYSTAL_CHILD = """--- !u!1001 &5588088495769702632
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: 5739209271613175231}
    m_Modifications:
    - target: {fileID: 2965872700150217210, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_Enabled
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790464, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_Radius
      value: 1
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790464, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_Enabled
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790470, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_Name
      value: SpaceCrystal
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_RootOrder
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalScale.x
      value: 3
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalScale.y
      value: 3
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalScale.z
      value: 3
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalPosition.x
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalPosition.y
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalPosition.z
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalRotation.w
      value: 1
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalRotation.x
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalRotation.y
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalRotation.z
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalEulerAnglesHint.x
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalEulerAnglesHint.y
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_LocalEulerAnglesHint.z
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790495, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: m_Enabled
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 6588436222817790495, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      propertyPath: inactiveCrystalMaterial
      value:
      objectReference: {fileID: 2100000, guid: 6eea31b551b13184cb148646f27388e8, type: 2}
    m_RemovedComponents:
    - {fileID: 6588436222817790466, guid: a4bde9d72595bfb43aa3b791d02f4db8, type: 3}
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents:
    - targetCorrespondingSourceObject: {fileID: 6588436222817790470, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      insertIndex: -1
      addedObject: {fileID: 2321339933426151692}
    - targetCorrespondingSourceObject: {fileID: 6588436222817790470, guid: a4bde9d72595bfb43aa3b791d02f4db8,
        type: 3}
      insertIndex: -1
      addedObject: {fileID: 7383174847063407884}
  m_SourcePrefab: {fileID: 100100000, guid: a4bde9d72595bfb43aa3b791d02f4db8, type: 3}
--- !u!1 &1648936725651798766 stripped
GameObject:
  m_CorrespondingSourceObject: {fileID: 6588436222817790470, guid: a4bde9d72595bfb43aa3b791d02f4db8,
    type: 3}
  m_PrefabInstance: {fileID: 5588088495769702632}
  m_PrefabAsset: {fileID: 0}
--- !u!114 &2321339933426151692
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1648936725651798766}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e3952a371fe646fcbe11bf76ed7434ac, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  Crystal: {fileID: 1648936725651798775}
  elementalCrystalShipEffects:
  - {fileID: 11400000, guid: 0ac04a748b4c4ad8825e811431df45f4, type: 2}
  moveToVesselDuration: 3
  easeMoveToVessel: 1
--- !u!114 &7383174847063407884
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1648936725651798766}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fafcc767febd41ccbad67c5457dc432d, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  impactorObject: {fileID: 2321339933426151692}
--- !u!4 &1648936725651798773 stripped
Transform:
  m_CorrespondingSourceObject: {fileID: 6588436222817790493, guid: a4bde9d72595bfb43aa3b791d02f4db8,
    type: 3}
  m_PrefabInstance: {fileID: 5588088495769702632}
  m_PrefabAsset: {fileID: 0}
--- !u!114 &1648936725651798775 stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {fileID: 6588436222817790495, guid: a4bde9d72595bfb43aa3b791d02f4db8,
    type: 3}
  m_PrefabInstance: {fileID: 5588088495769702632}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1648936725651798766}
  m_Enabled: 0
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 5a8f286a585edf943935c4316edeb2b6, type: 3}
  m_Name:
  m_EditorClassIdentifier:
"""


def flora_config(asset_name, species, element, prefab_fileid, prefab_guid,
                 initial_count, probability, plant_period_override,
                 spread, palette_guids):
    s = SPECIES[species]
    e = ELEMENT_TUNING[element]
    leaf = tuple(round(s["leafSize"][i] * e["leaf_mul"][i], 3) for i in range(3))
    grow = round(s["growPeriod"] * e["grow_mul"], 3)
    budget = int(round(s["maxTotalSpawnedObjects"] * e["budget_mul"]))
    if palette_guids:
        palette_block = "  ElementPalette:\n" + "\n".join(
            f"  - {{fileID: 11400000, guid: {g}, type: 2}}" for g in palette_guids)
    else:
        palette_block = "  ElementPalette: []"
    override = 1 if plant_period_override else 0
    period = plant_period_override if plant_period_override else 2147483647
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_FLORA_CONFIG}, type: 3}}
  m_Name: {asset_name}
  m_EditorClassIdentifier:
  FloraPrefab: {{fileID: {prefab_fileid}, guid: {prefab_guid}, type: 3}}
  SpawnProbability: {probability}
  InitialSpawnCount: {initial_count}
  OverrideDefaultPlantPeriod: {override}
  NewPlantPeriod: {period}
  Element: {ELEMENTS[element]}
  Variant:
    Enabled: 1
    LeafSize: {v3(leaf)}
    GrowPeriod: {grow}
    ShieldPeriod: {e['shield']}
    MaxTotalSpawnedObjects: {budget}
    PlantRadiusCellFraction: {s['plantRadiusFraction']}
  InitialLevel: 1
  LeafScalePerLevel: 1.15
  CrystalScalePerLevel: 1.2
  SpreadElements: {1 if spread else 0}
{palette_block}
  Levels:
    Enabled: 1
    MinLevel: 1
    MaxLevel: 5
    RarityFalloff: 2
"""


def main():
    made = []

    # ── 1. Script metas ──────────────────────────────────────────────────────
    for rel, g in [
        ("Assets/_Scripts/Controller/Environment/FloraAndFauna/PhyllotacticFlora.cs", SCRIPT_PHYLLOTACTIC),
        ("Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableHesperides.cs", SCRIPT_SPAWNABLE),
        ("Assets/_Scripts/Controller/Environment/Spawning/FloraPlantingSite.cs", SCRIPT_PLANTING_SITE),
    ]:
        meta_script(rel, g)
        made.append(rel + ".meta")

    # ── 2. Flora prefabs ─────────────────────────────────────────────────────
    prefab_guids = {}
    for name, s in SPECIES.items():
        rel = f"Assets/_Prefabs/FloraAndFauna/{name}Flora.prefab"
        g = guid(f"prefab/{name}Flora")
        prefab_guids[name] = g
        made.append(write(rel, flora_prefab(name, s)))
        meta_prefab(rel, g)

    # ── 3. Environment prefab ────────────────────────────────────────────────
    env_guid = guid("prefab/SpawnableHesperides")
    env_rel = "Assets/_Prefabs/Spawnables/SpawnableHesperides.prefab"
    made.append(write(env_rel, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5210000000000101
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5210000000000102}}
  - component: {{fileID: 5210000000000103}}
  m_Layer: 0
  m_Name: SpawnableHesperides
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5210000000000102
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5210000000000101}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5210000000000103
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5210000000000101}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_SPAWNABLE}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: 137
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: 1
  prism: {{fileID: {ENV_PRISM[0]}, guid: {ENV_PRISM[1]}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
"""))
    meta_prefab(env_rel, env_guid)

    # ── 4. Canonical per-element library configs (one per species x element) ─
    library = {}
    for name in SPECIES:
        for element in ELEMENTS:
            asset = f"{name} Flora {element}"
            rel = f"Assets/_SO_Assets/Lifeforms/{asset}.asset"
            g = guid(f"lifeform/{asset}")
            library[(name, element)] = g
            made.append(write(rel, flora_config(
                asset, name, element, "7514956980722975813", prefab_guids[name],
                initial_count=1, probability=1, plant_period_override=None,
                spread=False, palette_guids=None)))
            meta_asset(rel, g)

    # ── 5. The Hesperides cell folder ────────────────────────────────────────
    folder = "Assets/_SO_Assets/Cell Configs/Hesperides Cell"
    meta_folder(folder, guid("folder/HesperidesCell"))

    # The cell's own flora config data: element SPREAD across the canonical palette (so the
    # garden carries all four elemental crystals), planting counts owned here.
    cell_flora = {}
    seeding = {"Arbor": (14, 14), "Tendril": (22, 9), "Rosette": (18, 7)}
    for name, (initial, period) in seeding.items():
        asset = f"Hesperides {name} Flora Config Data"
        rel = f"{folder}/{asset}.asset"
        g = guid(f"cellflora/{asset}")
        cell_flora[name] = g
        made.append(write(rel, flora_config(
            asset, name, "Mass", "7514956980722975813", prefab_guids[name],
            initial_count=initial, probability=1, plant_period_override=period,
            spread=True, palette_guids=[library[(name, e)] for e in ELEMENTS])))
        meta_asset(rel, g)

    profile_guid = guid("profile/Hesperides")
    profile_rel = f"{folder}/Hesperides Cell Spawn Profile.asset"
    flora_refs = "\n".join(
        f"  - {{fileID: 11400000, guid: {cell_flora[n]}, type: 2}}" for n in seeding)
    made.append(write(profile_rel, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_SPAWN_PROFILE}, type: 3}}
  m_Name: Hesperides Cell Spawn Profile
  m_EditorClassIdentifier:
  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 12000
  FloraInitialDelaySeconds: 2
  FloraSpawnIntervalSeconds: 0.35
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 25
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 30
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 3000
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0
  HerbivoreSpawnPointCount: 4
  HerbivoreSpawnRadius: 300
  PredatorSpawnPointCount: 2
  PredatorSpawnRadius: 480
  SupportedFloras:
{flora_refs}
  SupportedFaunas:
  - {{fileID: 11400000, guid: {BLOB_TADPOLE}, type: 2}}
  - {{fileID: 11400000, guid: {BLOB_FAUNA}, type: 2}}
  - {{fileID: 11400000, guid: {BLOB_SHARK}, type: 2}}
"""))
    meta_asset(profile_rel, profile_guid)

    cell_rel = f"{folder}/Hesperides Cell Config.asset"
    cell_guid = guid("cellconfig/Hesperides")
    made.append(write(cell_rel, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_CELL_CONFIG}, type: 3}}
  m_Name: Hesperides Cell Config
  m_EditorClassIdentifier:
  CellName: Hesperides
  Description: The Garden - terraced beds, pergolas and hanging baskets, planted with living flora
  Icon: {{fileID: 0}}
  Difficulty: 1
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE[0]}, guid: {MEMBRANE[1]}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS[0]}, guid: {NUCLEUS[1]}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM[0]}, guid: {CYTOPLASM[1]}, type: 3}}
  CellModifiers:
  - {{fileID: {MODIFIER[0]}, guid: {MODIFIER[1]}, type: 3}}
  SpawnProfile: {{fileID: 11400000, guid: {profile_guid}, type: 2}}
  EnvironmentPrefab: {{fileID: 5210000000000103, guid: {env_guid}, type: 3}}
  EnvironmentIntensity: 1
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: 16300
    RestlessExit: 15900
    FrenzyEnter: 33000
    FrenzyExit: 32200
    RestlessEnterVolume: 602000
    RestlessExitVolume: 592000
    FrenzyEnterVolume: 985000
    FrenzyExitVolume: 960000
"""))
    meta_asset(cell_rel, cell_guid)

    print(f"wrote {len(made)} assets (+ metas)")
    print("environment prefab guid:", env_guid)
    print("cell config guid:       ", cell_guid)
    for n, g in prefab_guids.items():
        print(f"{n}Flora prefab guid:".ljust(24), g)


if __name__ == "__main__":
    main()
