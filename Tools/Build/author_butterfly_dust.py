#!/usr/bin/env python3
"""Author every asset the Butterfly's Mass-mode / Dust-mode redesign needs.

WHAT CHANGED (design record: Assets/_Scripts/Controller/Vessel/R_VesselActions/BUTTERFLY.md)

The hull used to carry TWO sphere skimmers -- a near-field and a far-field "wing" -- each with the
fleet's forcefield crackle overlay, so a pilot saw two bubbles around the ship. It now carries ONE
skimmer: a trigger CAPSULE hanging below the hull, live only in Dust mode, drawn only as particle
dust. The right trigger switches Mass mode (a wide wake) and Dust mode (the capsule).

What this script writes, and why each piece is an asset rather than a hand edit:

  ButterflyDustSkimmer.prefab   The capsule. It keeps Skimmer.prefab's fileIDs for the five
                                objects the two share (GameObject, Transform, Rigidbody, Skimmer,
                                SkimmerImpactor, ImpactCollider), which is what lets the Butterfly's
                                existing nested instance be RE-POINTED at it with every override
                                and every stripped reference still resolving -- the
                                _nearFieldSkimmer wire and the prism controller's clearance
                                skimmer survive the swap untouched.
  AOEButterflyBloom.prefab      The omni-crystal bloom: a copy of AOEMissileWarhead.prefab (a
                                one-object, non-destructive AOE), pointed at its own container.
  effect assets + containers    the dust's prism roll, the ally refresh, the opposing wither
                                (now flora AND fauna, own domain spared), the Charge-scaled pilot
                                drain, the bloom's heart-kill and its container, the crystal
                                effect that fires it.
  Butterfly.prefab surgery      re-point the near instance, delete the far one, wire the dust
                                field onto the mode executor.
  retirements                   the far-wing container and both wing-dissolve effects, which
                                existed only for the retired wings.

Deterministic and idempotent: every minted GUID is md5 of a stable label, and --check diffs every
file this script owns against disk without writing.

Run:  python3 Tools/Build/author_butterfly_dust.py [--check]
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is wrong: {ROOT}"
CHECK = "--check" in sys.argv

A = "Assets"
SKIMMER_BASE = f"{A}/_Prefabs/Spacevessels/Components/Skimmer.prefab"
DUST_PREFAB = f"{A}/_Prefabs/Spacevessels/Components/ButterflyDustSkimmer.prefab"
WARHEAD = f"{A}/_Prefabs/Projectile/AOEMissileWarhead.prefab"
BLOOM_PREFAB = f"{A}/_Prefabs/Projectile/AOEButterflyBloom.prefab"
BUTTERFLY = f"{A}/_Prefabs/Spacevessels/Butterfly.prefab"
FX = f"{A}/_SO_Assets/Effects"
SKIM_CONT = f"{FX}/Effect Containers/SkimmerContainers"
OLD_NEAR = f"{SKIM_CONT}/ButterflyNearWingSkimmerImpactorDataContainer.asset"
OLD_FAR = f"{SKIM_CONT}/ButterflyFarWingSkimmerImpactorDataContainer.asset"
DUST_CONT = f"{SKIM_CONT}/ButterflyDustSkimmerImpactorDataContainer.asset"
OLD_DISSOLVES = [f"{FX}/Skimmer Prism Effects/ButterflyWingDissolvePrismEffect.asset",
                 f"{FX}/Skimmer Prism Effects/ButterflyBroadwingDissolvePrismEffect.asset"]
DUST_PRISM = f"{FX}/Skimmer Prism Effects/ButterflyScaleDustPrismEffect.asset"
DUST_NOURISH = f"{FX}/Skimmer Crystal Effects/ButterflyDustNourishLifeformEffect.asset"
DUST_WITHER = f"{FX}/Skimmer Crystal Effects/ButterflyScaleDustWitherLifeformEffect.asset"
DUST_DEBUFF = f"{FX}/Vessel Skimmer Effects/ButterflyScaleDustDebuffBySkimmerEffect.asset"
DUST_COMBAT = f"{FX}/Vessel Skimmer Effects/ButterflyCombatHitBySkimmerEffect.asset"
BLOOM_WITHER = f"{FX}/Explosion Crystal Effects/ButterflyBloomWitherLifeformEffect.asset"
BLOOM_CONT = f"{FX}/Effect Containers/Explosion Containers/ButterflyBloomExplosionImpactorDataContainer.asset"
BLOOM_EFFECT = f"{FX}/Vessel Crystal Effects/ButterflyVesselExplosionByCrystalEffect.asset"
VESSEL_CONT = f"{FX}/Effect Containers/VesselContainers/ButterflyImpactorDataContainer.asset"
SQUIRREL_BLAST = f"{FX}/Vessel Crystal Effects/SquirrelVesselExplosionByCrystalEffect.asset"

SCRIPTS = {
    "ButterflyDustField": f"{A}/_Scripts/Controller/Vessel/R_VesselActions/ButterflyDustField.cs",
    "SkimmerScaleDustPrismEffectSO":
        f"{A}/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerScaleDustPrismEffectSO.cs",
    "SkimmerNourishLifeformByCrystalEffectSO":
        f"{A}/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Crystal Effects/SkimmerNourishLifeformByCrystalEffectSO.cs",
}
EXISTING_SCRIPTS = {
    "SkimmerImpactorDataContainerSO": f"{A}/_Scripts/Controller/ImpactEffects/Containers/SkimmerImpactorDataContainerSO.cs",
    "ExplosionImpactorDataContainerSO": f"{A}/_Scripts/Controller/ImpactEffects/Containers/ExplosionImpactorDataContainerSO.cs",
    "ExplosionWitherLifeformByCrystalEffectSO":
        f"{A}/_Scripts/Controller/ImpactEffects/EffectsSO/Explosion Crystal Effects/ExplosionWitherLifeformByCrystalEffectSO.cs",
    "VesselExplosionByCrystalEffectSO":
        f"{A}/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/VesselExplosionByCrystalEffectSO.cs",
}

PARTICLE_MAT = f"{A}/_Graphics/Design Assests/FX/fx_spark_oval.mat"

# ── the numbers ─────────────────────────────────────────────────────────────────
CAPSULE_WIDTH = 24.0          # world diameter of the dust column (radius 12)
CAPSULE_LEN_REST = 60.0       # Space 0: 60 u below the hull ...
CAPSULE_LEN_FULL = 150.0      # ... Space 10: 150 u (Space 15: 195 u), Space's ONE parameter
CAPSULE_LEN_FLOOR = 20.0      # the deficit band cannot collapse it
BLOOM_SCALE = 900.0           # AOE diameter: a 450 u radius heart-kill -- "large"
BLOOM_SECONDS = 0.6           # a large bloom is allowed to be seen
CHARGE_BITE_REST = 0.5        # Charge 0: half the priced bite ...
CHARGE_BITE_FULL = 2.0        # ... Charge 10: double it (Charge 15: 2.75x)
CHARGE_BITE_FLOOR = 0.25

# Skimmer.prefab's objects, reused verbatim by the dust prefab (see the module doc).
F_GO, F_TR, F_RB = "4816621673548213744", "7231566422788689087", "7751815150560592194"
F_SKIM, F_IMP, F_ICOL = "7751815150560592197", "7603142481619829103", "546241298706549007"
F_CAPS, F_DUST = "9103907316180665781", "1958240267839501301"   # new: capsule + dust field

NEAR_INSTANCE = "7526780584530545883"
FAR_INSTANCE = "4602949842258188811"
FAR_STRIPPED = ("6085221473828117326", "6609974900747343540")
DUST_FIELD_STRIPPED = "7700000000000002001"


def g(label: str) -> str:
    return hashlib.md5(f"CosmicShore/{label}".encode()).hexdigest()


def rd(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def exists(rel):
    return os.path.exists(os.path.join(ROOT, rel))


def meta_guid(rel):
    m = re.search(r"^guid: ([0-9a-f]{32})", rd(rel + ".meta"), re.M)
    assert m, rel
    return m.group(1)


def script_meta(gd):
    return (f"fileFormatVersion: 2\nguid: {gd}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def asset_meta(gd):
    return (f"fileFormatVersion: 2\nguid: {gd}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def prefab_meta(gd):
    return (f"fileFormatVersion: 2\nguid: {gd}\nPrefabImporter:\n  externalObjects: {{}}\n"
            f"  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def so(script_guid, name, body):
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier: \n" + body)


def ref(gd):
    return f"{{fileID: 11400000, guid: {gd}, type: 2}}"


def elemental(indent, enabled, value, lo, hi, element, floor):
    p = " " * indent
    return (f"{p}Enabled: {enabled}\n{p}Value: {value:g}\n{p}Min: {lo:g}\n{p}Max: {hi:g}\n"
            f"{p}element: {element}\n{p}UseFloor: 1\n{p}Floor: {floor:g}\n")


out = {}          # rel path -> text
deletions = []    # rel paths this script removes
errors = []

# ── script metas ────────────────────────────────────────────────────────────────
sg = {}
for name, rel in SCRIPTS.items():
    assert exists(rel), f"missing {rel}"
    sg[name] = meta_guid(rel) if exists(rel + ".meta") else g(f"script/{name}")
    out[rel + ".meta"] = script_meta(sg[name])
for name, rel in EXISTING_SCRIPTS.items():
    sg[name] = meta_guid(rel)
SKIMMER_SCRIPT = "f35ad70dcbb4e3b47af654e91d9387f8"
IMPACTOR_SCRIPT = "c1731f6481c2493aa107099246bb1af8"
ICOL_SCRIPT = "fafcc767febd41ccbad67c5457dc432d"

# ── containers + effects ────────────────────────────────────────────────────────
near_g = meta_guid(OLD_NEAR) if exists(OLD_NEAR + ".meta") else meta_guid(DUST_CONT)
dust_prism_g = g("asset/ButterflyScaleDustPrismEffect")
nourish_g = g("asset/ButterflyDustNourishLifeformEffect")
bloom_wither_g = g("asset/ButterflyBloomWitherLifeformEffect")
bloom_cont_g = g("asset/ButterflyBloomExplosionImpactorDataContainer")
bloom_effect_g = g("asset/ButterflyVesselExplosionByCrystalEffect")
dust_prefab_g = g("prefab/ButterflyDustSkimmer")
bloom_prefab_g = g("prefab/AOEButterflyBloom")
debuff_g, wither_g, combat_g = meta_guid(DUST_DEBUFF), meta_guid(DUST_WITHER), meta_guid(DUST_COMBAT)

out[DUST_PRISM] = so(sg["SkimmerScaleDustPrismEffectSO"], "ButterflyScaleDustPrismEffect",
    "  growWeight: 0.4\n  dangerWeight: 0.3\n  shieldWeight: 0.3\n  growFraction: 0.35\n"
    "  superShieldChance: 0.06\n  superShieldUpgradeElement: 3\n"
    "  destroyWeight: 1\n  shrinkWeight: 1\n  stealWeight: 1\n  shrinkFraction: 0.35\n"
    "  restitution: 0.33333334\n  debrisSpeedLimit: 120\n")
out[DUST_PRISM + ".meta"] = asset_meta(dust_prism_g)

out[DUST_NOURISH] = so(sg["SkimmerNourishLifeformByCrystalEffectSO"], "ButterflyDustNourishLifeformEffect",
    "  refreshCooldownSeconds: 5\n  onLifeformRefreshed: {fileID: 0}\n")
out[DUST_NOURISH + ".meta"] = asset_meta(nourish_g)

# The wither now takes flora AND fauna, and spares the pilot's own domain -- the ally half is the
# refresh above, and the two partition every lifeform by colour.
wither = rd(DUST_WITHER)
wither = re.sub(r"(?m)^  faunaOnly: \d$", "  faunaOnly: 0", wither)
wither = re.sub(r"(?m)^  sparesOwnDomain: \d$", "  sparesOwnDomain: 1", wither)
out[DUST_WITHER] = wither

debuff = rd(DUST_DEBUFF)
debuff = re.sub(r"(?m)^  biteScale:\n(?:    [^\n]*\n)+", "", debuff)
debuff = debuff.replace("  upgradeElement:",
    "  biteScale:\n" + elemental(4, 1, 1, CHARGE_BITE_REST, CHARGE_BITE_FULL, 1, CHARGE_BITE_FLOOR)
    + "  upgradeElement:", 1)
out[DUST_DEBUFF] = debuff

out[DUST_CONT] = so(sg["SkimmerImpactorDataContainerSO"], "ButterflyDustSkimmerImpactorDataContainer",
    f"  vesselSkimmerEffectsSO:\n  - {ref(debuff_g)}\n  - {ref(combat_g)}\n"
    f"  skimmerPrismEffectsSO:\n  - {ref(dust_prism_g)}\n"
    f"  skimmerCrystalEffectsSO: []\n"
    f"  skimmerLifeformCrystalEffectsSO:\n  - {ref(wither_g)}\n  - {ref(nourish_g)}\n")
out[DUST_CONT + ".meta"] = asset_meta(near_g)
for rel in (OLD_NEAR, OLD_NEAR + ".meta"):
    if exists(rel): deletions.append(rel)
for rel in [OLD_FAR, *OLD_DISSOLVES]:
    for r in (rel, rel + ".meta"):
        if exists(r): deletions.append(r)

out[BLOOM_WITHER] = so(sg["ExplosionWitherLifeformByCrystalEffectSO"], "ButterflyBloomWitherLifeformEffect",
    "  faunaOnly: 0\n  sparesOwnDomain: 1\n  onLifeformJousted: {fileID: 0}\n")
out[BLOOM_WITHER + ".meta"] = asset_meta(bloom_wither_g)

out[BLOOM_CONT] = so(sg["ExplosionImpactorDataContainerSO"], "ButterflyBloomExplosionImpactorDataContainer",
    "  vesselExplosionEffects: []\n  explosionPrismEffects: []\n  explosionCrystalEffects: []\n"
    f"  explosionLifeformCrystalEffects:\n  - {ref(bloom_wither_g)}\n")
out[BLOOM_CONT + ".meta"] = asset_meta(bloom_cont_g)

# The crystal effect that fires the bloom: the Squirrel's asset, re-pointed at the bloom prefab and
# resized. Forked rather than edited -- the Squirrel's shielded ring is the Squirrel's.
blast = rd(SQUIRREL_BLAST)
blast = blast.replace("m_Name: SquirrelVesselExplosionByCrystalEffect",
                      "m_Name: ButterflyVesselExplosionByCrystalEffect")
blast = re.sub(r"(?m)^  squirrelCrystalExplosionEvent: \{[^}]*\}\n(    type: 2\}\n)?",
               "  squirrelCrystalExplosionEvent: {fileID: 0}\n", blast)
blast = re.sub(r"(?m)^  _aoePrefabs:\n(?:  - [^\n]*\n)+",
               f"  _aoePrefabs:\n  - {{fileID: 0, guid: {bloom_prefab_g}, type: 3}}\n", blast)
blast = re.sub(r"(?m)^  _minExplosionScale: .*$", f"  _minExplosionScale: {BLOOM_SCALE:g}", blast)
blast = re.sub(r"(?m)^  _maxExplosionScale: .*$", f"  _maxExplosionScale: {BLOOM_SCALE:g}", blast)
out[BLOOM_EFFECT] = blast
out[BLOOM_EFFECT + ".meta"] = asset_meta(bloom_effect_g)

vc = rd(VESSEL_CONT)
squirrel_blast_g = meta_guid(SQUIRREL_BLAST)
vc = vc.replace(ref(squirrel_blast_g), ref(bloom_effect_g))
out[VESSEL_CONT] = vc

# ── the bloom prefab: the warhead, re-identified ────────────────────────────────
warhead = rd(WARHEAD)
warhead_cont_g = re.search(r"explosionImpactorDataContainer: \{fileID: 11400000, guid: (\w+)", warhead).group(1)
bloom = warhead.replace("67128045173902331", "85076661060900822")
bloom = bloom.replace("m_Name: AOEMissileWarhead", "m_Name: AOEButterflyBloom")
bloom = bloom.replace(warhead_cont_g, bloom_cont_g)
bloom = re.sub(r"(?m)^  ExplosionDuration: .*$", f"  ExplosionDuration: {BLOOM_SECONDS:g}", bloom)
# the GameObject's fileID is what the crystal effect points at
BLOOM_ROOT = re.search(r"--- !u!114 &(\d+)\nMonoBehaviour:(?:(?!--- !u!).)*?ExplosionDuration", bloom, re.S).group(1)
out[BLOOM_PREFAB] = bloom
out[BLOOM_PREFAB + ".meta"] = prefab_meta(bloom_prefab_g)
out[BLOOM_EFFECT] = out[BLOOM_EFFECT].replace(f"fileID: 0, guid: {bloom_prefab_g}", f"fileID: {BLOOM_ROOT}, guid: {bloom_prefab_g}")

# ── the dust skimmer prefab ─────────────────────────────────────────────────────
particle_mat_g = meta_guid(PARTICLE_MAT)
dust = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{F_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {F_TR}}}
  - component: {{fileID: {F_CAPS}}}
  - component: {{fileID: {F_RB}}}
  - component: {{fileID: {F_SKIM}}}
  - component: {{fileID: {F_IMP}}}
  - component: {{fileID: {F_ICOL}}}
  - component: {{fileID: {F_DUST}}}
  m_Layer: 7
  m_Name: ButterflyDustSkimmer
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{F_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {F_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: {CAPSULE_WIDTH:g}, y: {CAPSULE_LEN_REST:g}, z: {CAPSULE_WIDTH:g}}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!136 &{F_CAPS}
CapsuleCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {F_GO}}}
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 2
  m_Radius: 0.5
  m_Height: 1
  m_Direction: 1
  m_Center: {{x: 0, y: -0.5, z: 0}}
--- !u!54 &{F_RB}
Rigidbody:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {F_GO}}}
  serializedVersion: 4
  m_Mass: 1
  m_Drag: 0
  m_AngularDrag: 0.08
  m_CenterOfMass: {{x: 0, y: 0, z: 0}}
  m_InertiaTensor: {{x: 1, y: 1, z: 1}}
  m_InertiaRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ImplicitCom: 1
  m_ImplicitTensor: 1
  m_UseGravity: 0
  m_IsKinematic: 1
  m_Interpolate: 0
  m_Constraints: 0
  m_CollisionDetection: 0
--- !u!114 &{F_SKIM}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {F_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SKIMMER_SCRIPT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  onSkimmerShipImpact: {{fileID: 0}}
  vaccumAmount: 80
  vacuumCrystal: 1
  affectSelf: 0
  visible: 0
  Scale:
{elemental(4, 1, CAPSULE_LEN_REST, CAPSULE_LEN_REST, CAPSULE_LEN_FULL, 3, CAPSULE_LEN_FLOOR)}  elongateYOnly: 1
  markerDistance: 70
  AOEPrefab: {{fileID: 0}}
  AOEPeriod: 0
  lineMaterial: {{fileID: 0}}
--- !u!114 &{F_IMP}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {F_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMPACTOR_SCRIPT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  skimmerImpactorDataContainer: {ref(near_g)}
  skimmer: {{fileID: {F_SKIM}}}
--- !u!114 &{F_ICOL}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {F_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ICOL_SCRIPT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  impactorObject: {{fileID: {F_IMP}}}
--- !u!114 &{F_DUST}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {F_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {sg["ButterflyDustField"]}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  particleMaterial: {{fileID: 2100000, guid: {particle_mat_g}, type: 2}}
  dustColor: {{r: 1, g: 0.86, b: 0.52, a: 0.9}}
  motesPer100Volume: 3.5
  maxMotesPerSecond: 260
  moteLifetime: 1.1
  moteSize: {{x: 2.5, y: 6}}
  fallSpeed: 6
"""
out[DUST_PREFAB] = dust
out[DUST_PREFAB + ".meta"] = prefab_meta(dust_prefab_g)

# ── Butterfly.prefab surgery ────────────────────────────────────────────────────
bp = rd(BUTTERFLY)
docs = re.split(r"\n(?=--- !u!)", bp)
SKIM_BASE_G = meta_guid(SKIMMER_BASE)


def instance_doc(instance, parent, source_g, mods):
    lines = ["--- !u!1001 &" + instance, "PrefabInstance:", "  m_ObjectHideFlags: 0",
             "  serializedVersion: 2", "  m_Modification:", "    serializedVersion: 3",
             f"    m_TransformParent: {{fileID: {parent}}}", "    m_Modifications:"]
    for target, path, value, obj in mods:
        lines += [f"    - target: {{fileID: {target}, guid: {source_g},",
                  "        type: 3}", f"      propertyPath: {path}", f"      value: {value}",
                  f"      objectReference: {obj}"]
    lines += ["    m_RemovedComponents: []", "    m_RemovedGameObjects: []",
              "    m_AddedGameObjects: []", "    m_AddedComponents: []",
              f"  m_SourcePrefab: {{fileID: 100100000, guid: {source_g}, type: 3}}"]
    return "\n".join(lines)


new_docs = []
near_parent = None
for d in docs:
    head = d.split("\n", 1)[0]
    if head == f"--- !u!1001 &{FAR_INSTANCE}":
        continue
    if any(head.startswith(f"--- !u!{c} &{fid} stripped") for fid in FAR_STRIPPED for c in ("4", "114")):
        continue
    if head == f"--- !u!1001 &{NEAR_INSTANCE}":
        near_parent = re.search(r"m_TransformParent: \{fileID: (\d+)\}", d).group(1)
        z = "{fileID: 0}"
        mods = [(F_GO, "m_Name", "ButterflyDustSkimmer", z)]
        for axis, v in (("x", CAPSULE_WIDTH), ("y", CAPSULE_LEN_REST), ("z", CAPSULE_WIDTH)):
            mods.append((F_TR, f"m_LocalScale.{axis}", f"{v:g}", z))
        for axis in "xyz":
            mods.append((F_TR, f"m_LocalPosition.{axis}", "0", z))
        mods += [(F_TR, "m_LocalRotation.w", "1", z)]
        for axis in "xyz":
            mods.append((F_TR, f"m_LocalRotation.{axis}", "0", z))
        for axis in "xyz":
            mods.append((F_TR, f"m_LocalEulerAnglesHint.{axis}", "0", z))
        mods.append((F_IMP, "skimmerImpactorDataContainer", "", ref(near_g)))
        new_docs.append(instance_doc(NEAR_INSTANCE, near_parent, dust_prefab_g, mods))
        continue
    if f"m_PrefabInstance: {{fileID: {NEAR_INSTANCE}}}" in d:
        d = d.replace(SKIM_BASE_G, dust_prefab_g)
    if head.startswith(f"--- !u!114 &{DUST_FIELD_STRIPPED}"):
        continue   # re-emitted below, so a re-run is idempotent
    new_docs.append(d)

bp = "\n".join(new_docs)
assert near_parent, "near-wing instance not found (and the dust instance was not either)" \
    if f"&{NEAR_INSTANCE}" not in bp else "near parent unresolved"
bp = bp.replace(f"  - {{fileID: {FAR_STRIPPED[1]}}}\n", "")
bp = re.sub(r"(?m)^  _farFieldSkimmer: \{fileID: \d+\}$", "  _farFieldSkimmer: {fileID: 0}", bp)

# The mode executor's dust field: a stripped reference into the dust instance.
stripped = (f"--- !u!114 &{DUST_FIELD_STRIPPED} stripped\nMonoBehaviour:\n"
            f"  m_CorrespondingSourceObject: {{fileID: {F_DUST}, guid: {dust_prefab_g},\n    type: 3}}\n"
            f"  m_PrefabInstance: {{fileID: {NEAR_INSTANCE}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
            f"  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {sg['ButterflyDustField']}, type: 3}}\n"
            f"  m_Name: \n  m_EditorClassIdentifier: ")
bp = bp.rstrip("\n") + "\n" + stripped + "\n"
exec_hdr = "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Gameplay.SpreadWingsActionExecutor\n"
assert bp.count(exec_hdr) == 1, "mode executor not found"
exec_start = bp.index(exec_hdr)
exec_end = bp.index("\n--- !u!", exec_start)
block = bp[exec_start:exec_end]
block = re.sub(r"\n  dustField: \{fileID: \d+\}", "", block)
block = re.sub(r"(  config: \{[^}]*\})", r"\1" + f"\n  dustField: {{fileID: {DUST_FIELD_STRIPPED}}}", block, 1)
bp = bp[:exec_start] + block + bp[exec_end:]
out[BUTTERFLY] = bp

# ── validation ──────────────────────────────────────────────────────────────────
if "6540c78597c2dcc468123b270169fc44" in bp:
    errors.append("Butterfly.prefab still references the sphere Skimmer.prefab")
if FAR_INSTANCE in bp:
    errors.append("the far-wing instance survived")
for fid in FAR_STRIPPED:
    if fid in bp:
        errors.append(f"a far-wing stripped reference survived: {fid}")
if f"_nearFieldSkimmer: {{fileID: 281340694002912670}}" not in bp:
    errors.append("the near-field skimmer wire moved -- it must keep pointing at the stripped Skimmer")
if CAPSULE_LEN_FULL <= CAPSULE_WIDTH:
    errors.append("a capsule shorter than its width is a sphere -- Space would grow nothing")
if CHARGE_BITE_REST * CHARGE_BITE_FLOOR <= 0:
    errors.append("bite floor must be positive")
for rel in [DUST_PREFAB, BLOOM_PREFAB]:
    txt = out[rel]
    ids = re.findall(r"^--- !u!\d+ &(\d+)", txt, re.M)
    if len(ids) != len(set(ids)):
        errors.append(f"{rel}: duplicate fileIDs")
    for m in re.finditer(r"\{fileID: (\d{6,})\}", txt):
        if m.group(1) not in ids:
            errors.append(f"{rel}: dangling local fileID {m.group(1)}")

if errors:
    print("FAILED:")
    for e in errors: print("  " + e)
    sys.exit(1)

# ── write / check ───────────────────────────────────────────────────────────────
drift = []
for rel, text in sorted(out.items()):
    path = os.path.join(ROOT, rel)
    cur = open(path, encoding="utf-8").read() if os.path.exists(path) else None
    if cur != text:
        drift.append(rel)
        if not CHECK:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(text)
for rel in deletions:
    drift.append("(delete) " + rel)
    if not CHECK:
        os.remove(os.path.join(ROOT, rel))

if CHECK:
    if drift:
        print(f"--check: {len(drift)} file(s) differ from what this script authors:")
        for d in drift: print("  " + d)
        sys.exit(1)
    print(f"--check: OK, {len(out)} file(s) match.")
else:
    print(f"wrote {len([d for d in drift if not d.startswith('(delete)')])} file(s), "
          f"deleted {len(deletions)}; {len(out) - len(drift) + len(deletions)} already current.")
