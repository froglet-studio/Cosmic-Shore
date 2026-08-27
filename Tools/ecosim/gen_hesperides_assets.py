#!/usr/bin/env python3
"""
One-shot authoring script for the Hesperides garden cell + the PhyllotacticFlora species.

Unity asset surgery: writes the .meta files (deterministic guids, so a re-run is idempotent),
the three flora prefabs, the environment prefab, the twelve canonical per-element flora configs,
and the Hesperides cell folder. Kept in-repo so the exact authored values are reviewable and the
whole set can be regenerated after a tuning pass instead of being hand-edited in twelve places.

Run from the repo root:  python3 Tools/ecosim/gen_hesperides_assets.py

!! THIS SCRIPT IS NOT THE ONLY OWNER OF THE ASSETS IT WRITES. Step 4 rewrites the canonical
   Assets/_SO_Assets/Lifeforms/{species} Flora {element}.asset files WHOLESALE, and two other
   tools author fields inside them:
     * Tools/Build/author_lifeform_heart_sizes.py owns Variant.HeartWorldScale (fitted per
       ELEMENT to the lifeform's measured body). `carry_authored_heart` below quotes that
       value back into the regenerated YAML, so a re-run preserves it byte for byte - proven
       by regenerating all 88 canonical assets into a scratch tree and diffing: zero drift.
     * Tools/Build/author_flora_populations.py owns the population block (PopulationSize /
       MaxLivePopulation / GrowthPerOffspring / ...) on the Hesperides CELL configs, and
       Tools/Build/fit_schwarz_p_leaf_sizes.py owns the SchwarzP topiary's LeafSize. Those
       are NOT carried - re-run both after this script and check their diffs.
   (Lifeform LEVELS are retired - a lifeform is its species and its ELEMENT, and that element's
   variant block states the size of the heart it drops exactly once. Docs/ECOSYSTEM.md 40.2.)
"""
import hashlib
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def guid(name: str) -> str:
    """Deterministic 32-hex guid from a stable name (idempotent re-runs)."""
    return hashlib.md5(("cosmicshore/hesperides/" + name).encode()).hexdigest()


# HeartWorldScale is authored PER ELEMENT by Tools/Build/author_lifeform_heart_sizes.py, which
# fits it to the lifeform's measured body. This generator does not know that number, and it
# rewrites the canonical _SO_Assets/Lifeforms species assets wholesale - so without the carry
# below, one re-run silently dropped every Hesperides species back to
# ElementalCrystalSetSO.defaultHeartWorldScale. Two owners for one field is the trap
# (Docs/ECOSYSTEM.md 40.2): QUOTE the authored value, never fork it. Same rule
# Tools/Build/author_lattice_cell.py follows for the whole Variant block.
_VARIANT_HEAD = "  Variant:\n    Enabled: 1\n"
_HEART_LINE = re.compile(r"^    HeartWorldScale: [-\d.eE+]+$", re.M)


def carry_authored_heart(path, text):
    """Re-insert the existing asset's HeartWorldScale into freshly generated YAML."""
    if not path.endswith(".asset") or not os.path.exists(path):
        return text
    if _VARIANT_HEAD not in text or _HEART_LINE.search(text):
        return text
    with open(path, encoding="utf-8") as f:
        existing = f.read()
    found = _HEART_LINE.search(existing)
    if not found:
        return text
    return text.replace(_VARIANT_HEAD, _VARIANT_HEAD + found.group(0) + "\n", 1)


def write(rel, text):
    path = os.path.join(REPO, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    # Resolve the carry BEFORE opening for write: `open(path, "w")` truncates, so reading the
    # existing asset from inside the `with` block reads an empty file and silently carries
    # nothing.
    text = carry_authored_heart(path, text)
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
# Eight forms out of ONE growth model. What separates them is entirely parameters -
# how many tips, how hard they seek the growth axis, how much they wander and droop,
# how often and how wide they open a whorl, and the PROPORTIONS of their prisms
# (stem vs leaf, as multiples of the element's leaf identity).
SPECIES = {
    # The canopy tree: one trunk, strong tropism, forking, wide flaring cupped whorls.
    "Arbor": dict(
        initialTips=1, maxTips=10, maxDepth=24, maxTotalSpawnedObjects=260,
        growthsPerTick=3, maxSpawnsPerFrame=1,
        segmentLength=17, segmentTaper=0.96,
        stemScale=(0.50, 0.50, 0.90), leafScale=(1.00, 0.28, 0.95),
        depthTaper=0.965, prismJitter=0.20, whorlAlternateScale=0.62,
        tropism=0.60, wander=0.16, spreadDegrees=8, gravityDroop=0.05, spiralTwist=7,
        branchStartDepth=3, branchChance=0.32, branchAngle=36,
        whorlStartDepth=6, whorlEvery=3, whorlLeaves=5, whorlRadius=11, whorlFlare=1.1,
        leafPitchDegrees=28, terminalWhorlScale=1.8,
        leafSize=(4.5, 4.5, 1.2), growPeriod=1.2, plantPeriod=14, plantRadiusFraction=0.55,
        sites="Bed",
    ),
    # The climber: several tips, weak tropism, heavy wander, sparse paired leaves.
    "Tendril": dict(
        initialTips=3, maxTips=8, maxDepth=34, maxTotalSpawnedObjects=120,
        growthsPerTick=4, maxSpawnsPerFrame=1,
        segmentLength=11, segmentTaper=1.0,
        stemScale=(0.30, 0.12, 0.95), leafScale=(0.55, 0.22, 0.90),
        depthTaper=1.0, prismJitter=0.22, whorlAlternateScale=0.70,
        tropism=0.12, wander=0.50, spreadDegrees=55, gravityDroop=0.18, spiralTwist=18,
        branchStartDepth=6, branchChance=0.12, branchAngle=48,
        whorlStartDepth=4, whorlEvery=3, whorlLeaves=2, whorlRadius=5, whorlFlare=0.2,
        leafPitchDegrees=55, terminalWhorlScale=1.2,
        leafSize=(2.2, 5.4, 1.1), growPeriod=0.7, plantPeriod=9, plantRadiusFraction=0.5,
        sites="Climb",
    ),
    # The bed cover: no rise to speak of, a whorl at every node, steeply cupped.
    "Rosette": dict(
        initialTips=1, maxTips=4, maxDepth=8, maxTotalSpawnedObjects=90,
        growthsPerTick=2, maxSpawnsPerFrame=1,
        segmentLength=5, segmentTaper=1.05,
        stemScale=(0.35, 0.35, 0.90), leafScale=(0.90, 0.16, 1.15),
        depthTaper=0.99, prismJitter=0.16, whorlAlternateScale=0.50,
        tropism=0.90, wander=0.05, spreadDegrees=4, gravityDroop=0.0, spiralTwist=0,
        branchStartDepth=99, branchChance=0.0, branchAngle=20,
        whorlStartDepth=0, whorlEvery=1, whorlLeaves=8, whorlRadius=9, whorlFlare=2.4,
        leafPitchDegrees=62, terminalWhorlScale=0.0,
        leafSize=(5.6, 5.6, 1.0), growPeriod=1.6, plantPeriod=7, plantRadiusFraction=0.6,
        sites="Bed",
    ),
    # The fern: a clump of arching stems, leaflets in pairs the whole way along.
    "Frond": dict(
        initialTips=4, maxTips=6, maxDepth=20, maxTotalSpawnedObjects=150,
        growthsPerTick=3, maxSpawnsPerFrame=1,
        segmentLength=12, segmentTaper=0.94,
        stemScale=(0.22, 0.22, 0.95), leafScale=(0.50, 0.20, 1.00),
        depthTaper=0.95, prismJitter=0.18, whorlAlternateScale=0.85,
        tropism=0.45, wander=0.10, spreadDegrees=42, gravityDroop=0.35, spiralTwist=3,
        branchStartDepth=99, branchChance=0.0, branchAngle=20,
        whorlStartDepth=1, whorlEvery=1, whorlLeaves=2, whorlRadius=6, whorlFlare=0.9,
        leafPitchDegrees=42, terminalWhorlScale=1.4,
        leafSize=(3.0, 3.0, 1.4), growPeriod=0.9, plantPeriod=10, plantRadiusFraction=0.55,
        sites="Bed|Water",
    ),
    # The spire: a narrow mast whose small whorls corkscrew, opening a big head at the top.
    "Spire": dict(
        initialTips=1, maxTips=3, maxDepth=30, maxTotalSpawnedObjects=170,
        growthsPerTick=2, maxSpawnsPerFrame=1,
        segmentLength=13, segmentTaper=0.985,
        stemScale=(0.28, 0.28, 0.95), leafScale=(0.70, 0.25, 1.00),
        depthTaper=0.985, prismJitter=0.14, whorlAlternateScale=0.55,
        tropism=0.92, wander=0.05, spreadDegrees=3, gravityDroop=0.0, spiralTwist=26,
        branchStartDepth=12, branchChance=0.08, branchAngle=14,
        whorlStartDepth=2, whorlEvery=2, whorlLeaves=3, whorlRadius=4.5, whorlFlare=1.6,
        leafPitchDegrees=15, terminalWhorlScale=2.6,
        leafSize=(3.4, 3.4, 1.3), growPeriod=1.0, plantPeriod=18, plantRadiusFraction=0.5,
        sites="Ledge|Bed",
    ),
    # The bell: a short stalk and one big open head. In a basket the growth axis points
    # DOWN, so it hangs - which is the whole reason a site carries a normal.
    "Lantern": dict(
        initialTips=1, maxTips=2, maxDepth=6, maxTotalSpawnedObjects=70,
        growthsPerTick=2, maxSpawnsPerFrame=1,
        segmentLength=9, segmentTaper=1.0,
        stemScale=(0.22, 0.22, 0.92), leafScale=(1.00, 0.22, 1.05),
        depthTaper=1.0, prismJitter=0.15, whorlAlternateScale=0.75,
        tropism=0.85, wander=0.08, spreadDegrees=6, gravityDroop=0.0, spiralTwist=0,
        branchStartDepth=99, branchChance=0.0, branchAngle=20,
        whorlStartDepth=5, whorlEvery=1, whorlLeaves=9, whorlRadius=12, whorlFlare=0.4,
        leafPitchDegrees=-55, terminalWhorlScale=2.2,
        leafSize=(4.0, 4.0, 1.5), growPeriod=1.1, plantPeriod=11, plantRadiusFraction=0.45,
        sites="Basket",
    ),
    # The thicket: dense low forking, stubby prisms, no whorls at all - brain-coral cover.
    "Coral": dict(
        initialTips=3, maxTips=14, maxDepth=16, maxTotalSpawnedObjects=200,
        growthsPerTick=4, maxSpawnsPerFrame=1,
        segmentLength=7, segmentTaper=0.93,
        stemScale=(0.50, 0.50, 0.90), leafScale=(1.00, 1.00, 1.00),
        depthTaper=0.94, prismJitter=0.26, whorlAlternateScale=1.0,
        tropism=0.30, wander=0.34, spreadDegrees=48, gravityDroop=0.0, spiralTwist=0,
        branchStartDepth=1, branchChance=0.55, branchAngle=42,
        whorlStartDepth=99, whorlEvery=1, whorlLeaves=0, whorlRadius=3, whorlFlare=0.0,
        leafPitchDegrees=0, terminalWhorlScale=0.0,
        leafSize=(2.6, 2.6, 2.0), growPeriod=1.0, plantPeriod=15, plantRadiusFraction=0.55,
        sites="Bed|Water",
    ),
    # The reeds: a clump of tall bare stalks with the odd blade near the top.
    "Reed": dict(
        initialTips=5, maxTips=6, maxDepth=22, maxTotalSpawnedObjects=110,
        growthsPerTick=3, maxSpawnsPerFrame=1,
        segmentLength=14, segmentTaper=1.0,
        stemScale=(0.22, 0.22, 0.97), leafScale=(0.55, 0.22, 1.00),
        depthTaper=0.99, prismJitter=0.20, whorlAlternateScale=0.6,
        tropism=0.95, wander=0.07, spreadDegrees=12, gravityDroop=0.08, spiralTwist=11,
        branchStartDepth=99, branchChance=0.0, branchAngle=20,
        whorlStartDepth=8, whorlEvery=6, whorlLeaves=2, whorlRadius=7, whorlFlare=0.3,
        leafPitchDegrees=68, terminalWhorlScale=1.6,
        leafSize=(2.4, 2.4, 1.6), growPeriod=0.8, plantPeriod=12, plantRadiusFraction=0.5,
        sites="Water",
    ),
}

# FloraSiteKind bit values (must track the C# [Flags] enum).
SITE_KIND = {"Bed": 1, "Climb": 2, "Basket": 4, "Water": 8, "Ledge": 16}


def site_mask(spec):
    return sum(SITE_KIND[k] for k in spec.split("|"))


# Per-element expression (FloraVariantTuning), following the authored gyroid convention that an
# element's identity is its leaf PRISM and its growth TEMPO.
#
# For PhyllotacticFlora the identity lands in the prism CROSS-SECTION: prism LENGTHS are
# structural (a stem spans its segment, a leaf spans its reach - see StemPrismScale /
# LeafPrismScale), so LeafSize.z is not read and only x,y carry the element. That still reads
# clearly in-world - a Space garden is wiry and fine, a Mass garden is thick and heavy - and it
# stacks with the tempo and budget differences below. The assembled species (gyroid, Schwarz P)
# keep using LeafSize.z as their thin axis, unchanged.
ELEMENT_TUNING = {
    "Charge": dict(leaf_mul=(0.85, 0.85, 1.0), grow_mul=1.0, shield=1.0, budget_mul=0.85),
    "Mass":   dict(leaf_mul=(1.35, 1.35, 1.0), grow_mul=1.3, shield=0.0, budget_mul=1.2),
    "Space":  dict(leaf_mul=(0.50, 0.50, 1.0), grow_mul=1.8, shield=0.0, budget_mul=0.7),
    "Time":   dict(leaf_mul=(1.00, 1.00, 1.0), grow_mul=0.5, shield=0.0, budget_mul=1.0),
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
  healthBlocksForMaturity: 1
  minHealthBlocks: 0
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
  stemScale: {v3(s['stemScale'])}
  leafScale: {v3(s['leafScale'])}
  depthTaper: {s['depthTaper']}
  prismJitter: {s['prismJitter']}
  whorlAlternateScale: {s['whorlAlternateScale']}
  tropism: {s['tropism']}
  wander: {s['wander']}
  spreadDegrees: {s['spreadDegrees']}
  gravityDroop: {s['gravityDroop']}
  spiralTwist: {s['spiralTwist']}
  branchStartDepth: {s['branchStartDepth']}
  branchChance: {s['branchChance']}
  branchAngle: {s['branchAngle']}
  whorlStartDepth: {s['whorlStartDepth']}
  whorlEvery: {s['whorlEvery']}
  whorlLeaves: {s['whorlLeaves']}
  whorlRadius: {s['whorlRadius']}
  whorlFlare: {s['whorlFlare']}
  leafPitchDegrees: {s['leafPitchDegrees']}
  terminalWhorlScale: {s['terminalWhorlScale']}
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
                 spread, palette_guids, sites=None, leaf_override=None, budget_override=None):
    s = SPECIES[species]
    e = ELEMENT_TUNING[element]
    sites = site_mask(sites if sites else s["sites"])
    base_leaf = leaf_override if leaf_override else s["leafSize"]
    leaf = tuple(round(base_leaf[i] * e["leaf_mul"][i], 3) for i in range(3))
    grow = round(s["growPeriod"] * e["grow_mul"], 3)
    base_budget = budget_override if budget_override else s["maxTotalSpawnedObjects"]
    budget = int(round(base_budget * e["budget_mul"]))
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
  PreferredSites: {sites}
  Element: {ELEMENTS[element]}
  Variant:
    Enabled: 1
    LeafSize: {v3(leaf)}
    GrowPeriod: {grow}
    ShieldPeriod: {e['shield']}
    MaxTotalSpawnedObjects: {budget}
    PlantRadiusCellFraction: {s['plantRadiusFraction']}
  SpreadElements: {1 if spread else 0}
{palette_block}
"""


def topiary_config(asset_name, spec, _g):
    """
    A config for one of the SHIPPING assembled flora (gyroid / Schwarz P), tuned small and
    planted sparsely on bed ground. Element spreads across the species' own four canonical
    library configs in _SO_Assets/Lifeforms, exactly like every other cell does it.
    """
    fid, pguid = spec["prefab"]
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
  FloraPrefab: {{fileID: {fid}, guid: {pguid}, type: 3}}
  SpawnProbability: 1
  InitialSpawnCount: {spec['initial']}
  OverrideDefaultPlantPeriod: 1
  NewPlantPeriod: {spec['period']}
  PreferredSites: {SITE_KIND['Bed']}
  Element: 2
  Variant:
    Enabled: 1
    LeafSize: {v3(spec['leaf'])}
    GrowPeriod: {spec['grow']}
    ShieldPeriod: 0
    MaxTotalSpawnedObjects: {spec['budget']}
    PlantRadiusCellFraction: 0.5
  SpreadElements: 1
  ElementPalette: []
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
    # NOTE: this OVERWRITES the shipped library assets. The per-element HeartWorldScale
    # authored by Tools/Build/author_lifeform_heart_sizes.py survives, because write()
    # carries it across (see carry_authored_heart). Nothing else in those files is
    # second-owned. Run that script's --check after this one anyway.
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
    #
    # The initial batch is what the garden looks like the moment it finishes building; the
    # period is how fast it keeps filling toward the Frenzy ceiling. Counts are weighted to
    # the ground each species prefers (306 bed sites, 210 climb, 24 water, 14 basket, 9 ledge)
    # so no kind of ground sits conspicuously bare or conspicuously stacked.
    cell_flora = {}
    seeding = {
        "Arbor": (10, 16),     # bed - the canopy, sparse and large
        "Rosette": (16, 8),    # bed - carpet
        "Frond": (12, 10),     # bed + water
        "Coral": (8, 15),      # bed + water - low thicket
        "Spire": (6, 18),      # ledge + bed - the accents
        "Tendril": (20, 9),    # climb - the pergolas and trellises
        "Reed": (8, 12),       # water - the pool margin
        "Lantern": (8, 11),    # basket - the hanging bells
    }
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

    # Topiary: the two SHIPPING assembled species (gyroid + Schwarz P minimal surfaces) planted
    # sparsely on bed ground with a small prism budget, so they read as clipped specimen
    # topiary among the grown plants. Cross-species diversity for free - the garden borrows the
    # platform's flora rather than making everything a new species.
    TOPIARY = {
        "Gyroid": dict(prefab=("8186157953239024492", "a84d160ac0bcaf94da22c5368e4d3962"),
                       leaf=(3.6, 3.0, 2.2), grow=0.5, budget=190, initial=3, period=90),
        "SchwarzP": dict(prefab=("8186157953239024492", "3bbc2887bdb944b39945e4a926291007"),
                         leaf=(4.2, 4.2, 1.0), grow=0.8, budget=150, initial=2, period=110),
    }
    topiary_guids = {}
    for name, spec in TOPIARY.items():
        asset = f"Hesperides {name} Topiary Config Data"
        rel = f"{folder}/{asset}.asset"
        g = guid(f"topiary/{asset}")
        topiary_guids[name] = g
        made.append(write(rel, topiary_config(asset, spec, g)))
        meta_asset(rel, g)

    profile_guid = guid("profile/Hesperides")
    profile_rel = f"{folder}/Hesperides Cell Spawn Profile.asset"
    flora_refs = "\n".join(
        [f"  - {{fileID: 11400000, guid: {cell_flora[n]}, type: 2}}" for n in seeding] +
        [f"  - {{fileID: 11400000, guid: {topiary_guids[n]}, type: 2}}" for n in topiary_guids])
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
    # This script has NO read-only mode: main() writes assets unconditionally. Every other
    # script under Tools/ takes --check as a read-only probe, so a caller reasonably reaches
    # for it here to ask "would this revert anything?" - and silently rewrites 50 assets
    # instead. That has happened. Refuse any argument rather than ignore it.
    if len(sys.argv) > 1:
        sys.exit(
            f"{os.path.basename(__file__)} takes no arguments and has no --check mode: it "
            f"WRITES unconditionally.\n"
            f"Got: {' '.join(sys.argv[1:])}\n"
            f"It overwrites the canonical _SO_Assets/Lifeforms species assets. The authored "
            f"HeartWorldScale is carried across, but the flora POPULATION block and the "
            f"SchwarzP topiary leaf are not.\n"
            f"To ask whether anything has drifted, run the owning script's --check instead.\n"
            f"To author for real, run this with no arguments, then re-run\n"
            f"    python3 Tools/Build/author_lifeform_heart_sizes.py --check\n"
            f"    python3 Tools/Build/author_flora_populations.py --check\n"
            f"    python3 Tools/Build/fit_schwarz_p_leaf_sizes.py")
    main()
