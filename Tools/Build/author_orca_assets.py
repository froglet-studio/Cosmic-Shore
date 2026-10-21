#!/usr/bin/env python3
"""Author the Orca (the 4-seat vessel: the Sparrow hull with one helm and three gunner seats) and register it, then prove
the shipped set.

    python3 Tools/Build/author_orca_assets.py             # author what is missing, then check
    python3 Tools/Build/author_orca_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_orca_assets.py --self-test # prove every check fires

"Bring it to Unity" means the Toybox's Vessel Changer offers it (Garrett, 2026-10-17: "my workflow is artifact to
vessel selector in toybox"). The design is the Crew of Four Lab's round 4 (Docs/Studios/CREW_OF_FOUR_LAB.md), and the
crew is Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md. This tool is author_wrasse_assets.py's second clone of the
Sparrow: it IMPORTS that tool's document helpers and Sparrow-kit strip (vessel skill rule 47) and never edits the Wrasse.

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/Orca.prefab (+ .meta)     a text clone of Sparrow.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 20` (VesselClassType.Orca)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
        - OrcaCrew on the root, wired to OrcaCrewConfig.asset
        - NO SPARROW KIT and NO BUTTONS (author_wrasse_assets.strip_sparrow_kit): the helm's boost and shield are the
          crew's (OrcaCrew reads the triggers), the gunners' missiles are the crew's
        - TWO THUMBS: the Sparrow's SingleStickVesselTransformer becomes its base VesselTransformer (Garrett, 2026-10-20:
          "the helm should be a two thumb flyer")
        - the lab's hull: the Sparrow's speed x hullSpeedMul (0.85) and its turn rates x hullTurnMul (0.55), written ONCE
          over the Sparrow's untouched numbers so a value tuned in Unity afterwards is never overwritten
        - OrcaHUDController in place of SparrowHUDController, wired to the crew
        - its VesselImpactor on OrcaImpactorDataContainer: the Sparrow's, without the Sparrow's Omni ward and with
          OrcaOmniCrystalEffect in its omni list (an Omni crystal refills the magazine)
    OrcaCrewConfig.asset (seeded once from the lab's SHIPPED numbers; Tools/Build/orca_crew_lab/parity.cjs holds it there),
    OrcaOmniCrystalEffect.asset, OrcaImpactorDataContainer.asset, Resources/OrcaCrewLit.mat (the crew's lit material,
    so URP/Lit ships in a player build), SO_Class_Orca (SeatCount 4: its card reads "Orca · 4 seat") + its entry in
    SO_Classlist_All / _Classes, one entry each in `Vessel Prefab Container.asset` (root TRANSFORM) and
    `DefaultNetworkPrefabs.asset` (root GAMEOBJECT), Resources/ElementalAbilityMaps/Orca.asset (the crew's four cards,
    every input 0) and four CrystalHullFusionConfig entries (the Sparrow's: the Orca draws the Sparrow's mesh).
    ToyVesselRoster.Default and EnumIntegrityTests are CODE and are not written here (vessel skill rule 40).

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while Orca.prefab is absent, and
`--check` validates the SHIPPED prefab's invariants. To re-author from scratch, delete Orca.prefab and its .meta.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)
import author_wrasse_assets as W  # noqa: E402  (the Sparrow clone's document helpers and kit strip)

ROOT = W.ROOT
VESSELS = W.VESSELS
DONOR = W.DONOR
PREFAB = os.path.join(VESSELS, "Orca.prefab")
CLASS_ASSET = os.path.join(W.CLASSES, "SO_Class_Orca.asset")
MAP = os.path.join(W.MAPS, "Orca.asset")
CREW_DIR = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "Crew")
SEAT_SCRIPT_META = os.path.join(CREW_DIR, "OrcaCrew.cs.meta")
CONFIG_SCRIPT_META = os.path.join(CREW_DIR, "OrcaCrewConfigSO.cs.meta")
CONFIG_DIR = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "OrcaCrew")
CONFIG = os.path.join(CONFIG_DIR, "OrcaCrewConfig.asset")
ORCA_CONTAINER = os.path.join(W.CONTAINERS, "OrcaImpactorDataContainer.asset")
OMNI_EFFECT = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Vessel Crystal Effects", "OrcaOmniCrystalEffect.asset")
OMNI_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "ImpactEffects", "EffectsSO", "Vessel Crystal Effects",
                                "OrcaOmniCrystalEffectSO.cs.meta")
LIT_MAT = os.path.join(ROOT, "Assets", "Resources", "OrcaCrewLit.mat")

ORCA_CLASS_ID = 20
SPARROW_CLASS_ID = W.SPARROW_CLASS_ID
PREFAB_GUID = "db757691ec7343cc9decf08f3fd8c8cc"
CLASS_GUID = "6984d323ceb54c5ab6944d3de86793d3"
MAP_GUID = "210e70a432684f2d96a0194df8c9a9c3"
ORCA_CONTAINER_GUID = "4d7a0d84f5694429a9a2d4c27d6d975b"
OMNI_EFFECT_GUID = "0d3ac0c0348f461cb7bf3da3bb784b19"
CONFIG_GUID = "38d89b6853e84f91b05c4a334256e3bb"
CONFIG_DIR_GUID = "982d89e12a5545eb9051ee1f82f4ff7b"
LIT_MAT_GUID = "ca4c63633add406ba5732231ca75f4e5"
URP_LIT_SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"
AOE_EXPLOSION_REF = "{fileID: 3479271500403630839, guid: 4a855af160021d241b8c1ac70cf2d792, type: 3}"   # AOEExplosion.prefab
GAME_DATA_GUID = "b35f33752bb10a44cb5033b5670f50aa"
ROOT_GO, ROOT_TRANSFORM_ID, NETWORK_OBJECT_ID = W.ROOT_GO, W.ROOT_TRANSFORM_ID, W.NETWORK_OBJECT_ID
SEAT_ID = "6420517381209347020"              # OrcaCrew on the root
# Round 5: two gun objects under the root, each a Gun + ProjectileFactory + ProjectilePoolManager (the Brittle Star's
# BrittleStarTurrets block and the Grizzly's bomb pool, cloned with the Orca's own capacities)
FAST_GO, FAST_TF, FAST_GUN, FAST_FACTORY, FAST_POOL = (str(6420517381209347100 + k) for k in range(1, 6))
BOMB_GO, BOMB_TF, BOMB_GUN, BOMB_FACTORY, BOMB_POOL = (str(6420517381209347110 + k) for k in range(1, 6))
SPARROW_ROUND_REF = "{fileID: 7632677803272750685, guid: 1bdcea9e9329dd141af7ab874a4d31a2, type: 3}"   # SparrowProjectile.prefab
GRIZZLY_BOMB_REF = "{fileID: 7632677803272750685, guid: d6e1fe636eb02bb73852e896ccb0532e, type: 3}"   # GrizzlyBomb.prefab
AOE_GRIZZLY_REF = "{fileID: 3479271500403630839, guid: 8713aea50e6b47119e57afc2ce603771, type: 3}"   # AOEGrizzlyExplosion.prefab
# the flown missile's look (round 9): the Sparrow's skyburst rocket, as SkyBurstProjectile.prefab draws it
MISSILE_MESH_REF = "{fileID: -3416553540687559647, guid: 98d8cb0114a1ad04e9682869849be719, type: 3}"   # Sparrow Missile.fbx
MISSILE_MATERIAL_REFS = ["{fileID: 2236194519873700097, guid: 98d8cb0114a1ad04e9682869849be719, type: 3}",
                         "{fileID: 2100000, guid: f5a1f1d453134cc4193ba1adbdd49d7a, type: 2}",
                         "{fileID: 2100000, guid: f5a1f1d453134cc4193ba1adbdd49d7a, type: 2}"]
COMBAT_HIT_GUID = "45fb43a0cddb5cc3cc4dc448df994152"   # Event_CombatHitStats.asset (the scoreboard's channel)


# ----------------------------------------------------------------------------- the prefab
def seat_block(script_guid: str) -> str:
    return (f"--- !u!114 &{SEAT_ID}\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: \n"
            "  m_EditorClassIdentifier: \n"
            f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}\n"
            f"  fastGun: {{fileID: {FAST_GUN}}}\n"
            f"  bombGun: {{fileID: {BOMB_GUN}}}\n")


def build_clone(donor: str, script_guid: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Sparrow\n", "  m_Name: Orca\n", "root GameObject name")
    text = replace_once(text, "  _name: Sparrow\n", "  _name: Orca\n", "VesselStatus _name")
    text = replace_once(text, f"  vesselType: {SPARROW_CLASS_ID}\n", f"  vesselType: {ORCA_CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    if not root:
        raise RuntimeError("donor drifted: the root GameObject's m_Component list was not found")
    text = text[:root.end(1)] + f"  - component: {{fileID: {SEAT_ID}}}\n" + text[root.end(1):]
    if not text.endswith("\n"):
        text += "\n"
    return retrofit(text + seat_block(script_guid).replace(f"  fastGun: {{fileID: {FAST_GUN}}}\n  bombGun: {{fileID: {BOMB_GUN}}}\n", ""))


def hud_retrofit(text: str) -> str:
    """SparrowHUDController (guns, missiles, roll) becomes OrcaHUDController, wired to the crew. Idempotent."""
    sparrow_hud, orca_hud = W.guid_of_script("SparrowHUDController"), W.guid_of_script("OrcaHUDController")
    head, docs = W.split_docs(text)
    out = []
    for _, _, d in docs:
        if f"guid: {sparrow_hud}, type: 3" in d:
            d = d.replace(f"guid: {sparrow_hud}, type: 3", f"guid: {orca_hud}, type: 3")
            for key in W.SPARROW_HUD_KEYS:
                d = re.sub(r"^  %s: \{[^}]*\}\n" % key, "", d, flags=re.M)
            d = d.rstrip("\n") + f"\n  crew: {{fileID: {SEAT_ID}}}\n"
        out.append(d)
    return head + "".join(out)


# The lab's hull (Crew of Four Lab: hullSpeedMul 0.85, hullTurnMul 0.55 of the Sparrow's numbers, both terms of the turn
# formula). Written ONCE, over the Sparrow's untouched numbers, so a value play-tuned in Unity is never overwritten.
SPARROW_HULL = {"DefaultMinimumSpeed": "10", "DefaultThrottleScaler": "25", "PitchScaler": "80", "YawScaler": "80",
                "RollScaler": "30", "RotationThrottleScaler": "0.1"}
ORCA_HULL = {"DefaultMinimumSpeed": "8.5", "DefaultThrottleScaler": "21.25", "PitchScaler": "44", "YawScaler": "44",
             "RollScaler": "16.5", "RotationThrottleScaler": "0.055"}


def tuning_retrofit(text: str) -> str:
    head, docs = W.split_docs(text)
    out = []
    for _, _, d in docs:
        if f"guid: {W.TWO_STICK_TRANSFORMER_GUID}, type: 3" in d and \
                all(f"\n  {k}: {v}\n" in d for k, v in SPARROW_HULL.items()):
            for k, v in ORCA_HULL.items():
                d = d.replace(f"\n  {k}: {SPARROW_HULL[k]}\n", f"\n  {k}: {v}\n")
        out.append(d)
    return head + "".join(out)


def gun_docs(go, tf, gun, factory, pool, name, prefab_ref, capacity, max_size, rate):
    """One gun object under the root: GameObject, Transform, Gun, ProjectileFactory (Normal -> the pool), and a
    ProjectilePoolManager of `prefab_ref` (the pool is injected with the hull's Container at spawn)."""
    head = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
    mono = lambda anchor, script: (f"--- !u!114 &{anchor}\nMonoBehaviour:\n" + head + f"  m_GameObject: {{fileID: {go}}}\n"
                                   "  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
                                   f"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n")
    return (f"--- !u!1 &{go}\nGameObject:\n" + head + "  serializedVersion: 6\n  m_Component:\n"
            f"  - component: {{fileID: {tf}}}\n  - component: {{fileID: {gun}}}\n  - component: {{fileID: {factory}}}\n"
            f"  - component: {{fileID: {pool}}}\n  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n"
            "  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
            f"--- !u!4 &{tf}\nTransform:\n" + head + f"  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n"
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n"
            f"  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {{fileID: {ROOT_TRANSFORM_ID}}}\n"
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
            + mono(gun, GUN_SCRIPT) + f"  firePeriod: 0.2\n  sideLength: 2\n  projectileFactory: {{fileID: {factory}}}\n"
            + mono(factory, FACTORY_SCRIPT) + f"  poolEntries:\n  - type: 0\n    poolManager: {{fileID: {pool}}}\n"
            + mono(pool, POOL_SCRIPT) + f"  prefab: {prefab_ref}\n  defaultCapacity: {capacity}\n  maxSize: {max_size}\n"
            f"  enableBufferMaintenance: 1\n  bufferSizeTarget: {capacity}\n  maxInstantiateRate: {rate}\n"
            f"  baseInstantiateRate: {max(1, rate // 3)}\n  maxAddsPerFrame: {max(1, rate // 10)}\n")


GUN_SCRIPT, FACTORY_SCRIPT, POOL_SCRIPT = ("35a4e5dfc7064254cb27fc78eefb05a0", "a9b3080b23e64bba93ece7a3dece3463",
                                           "b5daed71d21246db9fdb15a9d495e1ec")


def gun_retrofit(text: str) -> str:
    """Round 5: the fast gun's and the bomb's objects under the root, and the crew wired to them. Idempotent.
    Pools: the fast gun keeps rate x flight = 30 x 4.5 = 135 rounds alive (vessel skill 4.ab3), so 160 / 320; one bomb
    at a time, so 2 / 4."""
    if f"--- !u!1 &{FAST_GO}\n" in text:
        return text
    m = re.search(r"(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:\n)((?:  - \{fileID: -?\d+\}\n)*)" % ROOT_TRANSFORM_ID, text)
    if not m:
        raise RuntimeError("Orca.prefab: the root Transform's m_Children list was not found")
    text = text[:m.end(2)] + f"  - {{fileID: {FAST_TF}}}\n  - {{fileID: {BOMB_TF}}}\n" + text[m.end(2):]
    seat = re.search(r"(--- !u!114 &%s\n(?:.*\n)*?  config: \{[^}]*\}\n)" % SEAT_ID, text)
    if not seat:
        raise RuntimeError("Orca.prefab: the OrcaCrew document's config line was not found")
    if "  fastGun:" not in text[seat.start():seat.end() + 200]:
        text = text[:seat.end()] + f"  fastGun: {{fileID: {FAST_GUN}}}\n  bombGun: {{fileID: {BOMB_GUN}}}\n" + text[seat.end():]
    if not text.endswith("\n"):
        text += "\n"
    return (text + gun_docs(FAST_GO, FAST_TF, FAST_GUN, FAST_FACTORY, FAST_POOL, "OrcaFastGun", SPARROW_ROUND_REF, 160, 320, 200)
            + gun_docs(BOMB_GO, BOMB_TF, BOMB_GUN, BOMB_FACTORY, BOMB_POOL, "OrcaBombGun", GRIZZLY_BOMB_REF, 2, 4, 5))


# ----------------------------------------------------------------------------- the Orca model (round 6)
# Orca.fbx (Tools/Build/vessel_model/specs/orca.py, /vessel-model) nested under the root as "Orca", the Grouper's pattern
# (author_wrasse_assets.py on cece/loving-fermat-ntdnbu, round 7): it becomes the ship geometry the domain paints, and the
# Sparrow's mesh is switched off; its bones, jets, colliders and Animator stay, so the hull still flies on them.
# OrcaHullRig finds the model by that name and aims its three turrets.
ORCA_FBX = os.path.join(ROOT, "Assets", "_Models", "Vessel Models", "Orca.fbx")
ORCA_FBX_GUID = "9b34bfdd14c8462c8bc138b3938820a2"
SPARROW_FBX_GUID = "151e3f0352963624abf4da579618e66d"
SPARROW_INSTANCE_ID = "3134011750752858212"      # the Sparrow model nested under the root
SPARROW_SMR_SOURCE = "2692722512241470911"       # its SkinnedMeshRenderer, in the FBX
SPARROW_MODEL_GO = "2864103291091619125"         # its root GameObject (stripped): the ship geometry until round 6
MODEL_ROOT_GO = "919132149155446097"             # a model's root GameObject / Transform: constants of the importer
MODEL_ROOT_TRANSFORM = "-8679921383154817045"
ORCA_MODEL_INSTANCE, ORCA_MODEL_TF, ORCA_MODEL_GO = "6420517381209347201", "6420517381209347202", "6420517381209347203"
ORCA_DOMAIN_SLOT = 0                             # Orca.fbx's slot order puts Domain first (FLEET_CONVENTION)
SPARROW_SMR_OFF = (f"    - target: {{fileID: {SPARROW_SMR_SOURCE}, guid: {SPARROW_FBX_GUID},\n        type: 3}}\n"
                   "      propertyPath: m_Enabled\n      value: 0\n      objectReference: {fileID: 0}\n")


def model_instance(instance_id: str, transform_id: str, fbx_guid: str, name: str, go_id: str) -> str:
    """A model nested under the root in its own frame (the fleet's: nose +Z, up +Y, where the Sparrow's sits)."""
    mods = [(MODEL_ROOT_GO, "m_Name", name)] + [(MODEL_ROOT_TRANSFORM, k, v) for k, v in (
        ("m_LocalPosition.x", "0"), ("m_LocalPosition.y", "0"), ("m_LocalPosition.z", "0"), ("m_LocalRotation.w", "1"),
        ("m_LocalRotation.x", "0"), ("m_LocalRotation.y", "0"), ("m_LocalRotation.z", "0"),
        ("m_LocalEulerAnglesHint.x", "0"), ("m_LocalEulerAnglesHint.y", "0"), ("m_LocalEulerAnglesHint.z", "0"))]
    return (f"--- !u!1001 &{instance_id}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
            f"  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {{fileID: {ROOT_TRANSFORM_ID}}}\n"
            "    m_Modifications:\n" +
            "".join(f"    - target: {{fileID: {fid}, guid: {fbx_guid},\n        type: 3}}\n      propertyPath: {path}\n"
                    f"      value: {v}\n      objectReference: {{fileID: 0}}\n" for fid, path, v in mods) +
            "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
            f"  m_SourcePrefab: {{fileID: 100100000, guid: {fbx_guid}, type: 3}}\n"
            f"--- !u!4 &{transform_id} stripped\nTransform:\n"
            f"  m_CorrespondingSourceObject: {{fileID: {MODEL_ROOT_TRANSFORM}, guid: {fbx_guid},\n    type: 3}}\n"
            f"  m_PrefabInstance: {{fileID: {instance_id}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
            f"--- !u!1 &{go_id} stripped\nGameObject:\n"
            f"  m_CorrespondingSourceObject: {{fileID: {MODEL_ROOT_GO}, guid: {fbx_guid},\n    type: 3}}\n"
            f"  m_PrefabInstance: {{fileID: {instance_id}}}\n  m_PrefabAsset: {{fileID: 0}}\n")


def model_retrofit(text: str) -> str:
    """Round 6: the Orca model under the root, painted as the ship, the Sparrow's mesh off. Idempotent; stands down while
    the model is not built (the retrofit then leaves the Sparrow drawing, as before)."""
    if not os.path.exists(ORCA_FBX):
        return text
    if f"--- !u!1001 &{ORCA_MODEL_INSTANCE}\n" not in text:
        if not text.endswith("\n"):
            text += "\n"
        text += model_instance(ORCA_MODEL_INSTANCE, ORCA_MODEL_TF, ORCA_FBX_GUID, OrcaModelName, ORCA_MODEL_GO)
    m = re.search(r"(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:\n)((?:  - \{fileID: -?\d+\}\n)*)" % ROOT_TRANSFORM_ID, text)
    if m and f"  - {{fileID: {ORCA_MODEL_TF}}}\n" not in m.group(2):
        text = text[:m.end(2)] + f"  - {{fileID: {ORCA_MODEL_TF}}}\n" + text[m.end(2):]
    text = re.sub(r"  _shipGeometries:\n  - \{fileID: %s\}\n  _domainMaterialSlot: \d+\n" % SPARROW_MODEL_GO,
                  f"  _shipGeometries:\n  - {{fileID: {ORCA_MODEL_GO}}}\n  _domainMaterialSlot: {ORCA_DOMAIN_SLOT}\n", text)
    m = re.search(r"--- !u!1001 &%s\nPrefabInstance:\n(?:.*\n)*?    m_Modifications:\n" % SPARROW_INSTANCE_ID, text)
    if m and SPARROW_SMR_OFF not in text:
        text = text[:m.end()] + SPARROW_SMR_OFF + text[m.end():]
    return text


OrcaModelName = "Orca"   # OrcaHullRig.ModelName


def check_model(text: str):
    if not os.path.exists(ORCA_FBX):
        return []
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    inst = re.search(r"--- !u!1001 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % ORCA_MODEL_INSTANCE, text)
    want(inst and f"m_SourcePrefab: {{fileID: 100100000, guid: {ORCA_FBX_GUID}, type: 3}}" in inst.group(1)
         and f"m_TransformParent: {{fileID: {ROOT_TRANSFORM_ID}}}" in inst.group(1) and f"      value: {OrcaModelName}\n" in inst.group(1),
         f"Orca.prefab: Orca.fbx is not nested under the root as '{OrcaModelName}' (OrcaHullRig finds it by that name)")
    root_t = re.search(r"--- !u!4 &%s\nTransform:\n((?:.*\n)*?)(?=--- !u!|\Z)" % ROOT_TRANSFORM_ID, text)
    want(root_t and f"  - {{fileID: {ORCA_MODEL_TF}}}\n" in root_t.group(1), "Orca.prefab: the root's children do not list the Orca model")
    want(f"  _shipGeometries:\n  - {{fileID: {ORCA_MODEL_GO}}}\n  _domainMaterialSlot: {ORCA_DOMAIN_SLOT}\n" in text,
         "Orca.prefab: the ship geometry is not the Orca model (the domain colour would paint the hidden Sparrow)")
    want(SPARROW_SMR_OFF in text, "Orca.prefab: the Sparrow's mesh still draws (a Sparrow inside the Orca)")
    return errs


def retrofit(text: str) -> str:
    """No buttons, no Sparrow kit, two thumbs, the lab's hull, the Orca HUD and container, the round-5 guns, the round-6
    model. Idempotent."""
    text = W.BINDINGS.sub(lambda m: m.group(1) + " []\n", text, count=1)
    text = W.strip_sparrow_kit(text)
    text = hud_retrofit(text)
    text = text.replace(f"guid: {W.SINGLE_STICK_TRANSFORMER_GUID},", f"guid: {W.TWO_STICK_TRANSFORMER_GUID},")
    text = tuning_retrofit(text)
    text = text.replace(f"guid: {W.DONOR_CONTAINER_GUID},", f"guid: {ORCA_CONTAINER_GUID},")
    return model_retrofit(gun_retrofit(text)) if f"--- !u!114 &{SEAT_ID}\n" in text else text


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the crew's assets
def so_header(script_guid: str, name: str) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier: \n")


def empty_event(name: str) -> str:
    return (f"  {name}:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n")


def elemental(rest: str, full: str, element: int, floor: str) -> str:
    """An ElementalFloat block: `rest` at the resting level, `full` at level 10 (the lab's 1 + k x level / 15 is the
    line through 1 and 1 + 2k/3 at level 10, so `full` = rest x (1 + 2k/3))."""
    return (f"\n    Enabled: 1\n    Value: {rest}\n    Min: {rest}\n    Max: {full}\n    element: {element}\n"
            f"    UseFloor: 1\n    Floor: {floor}")


CHARGE, MASS, SPACE, TIME = 1, 2, 3, 4
# The lab's SHIPPED numbers (Docs/Studios/CrewOfFourLab.html, round 8; the missile look is game-side), in OrcaCrewConfigSO's field order. Seeded from the
# lab: the lab owns them and parity.cjs holds the asset to the page. A round that changes them deletes the asset and
# re-runs this tool (the guid is kept, so no reference moves).
CONFIG_NUMBERS = [
    ("hullRadius", "9"), ("boostTank", "3"), ("boostRefill", elemental("0.25", "0.35", TIME, "0.05")), ("shieldBlocks", "144"),
    ("shieldMinRadius", "50"), ("shieldMaxRadius", "70"), ("shieldOpenDeg", "45"),
    ("shieldSeconds", elemental("4", "6.1333333", MASS, "0.5")), ("shieldCooldown", "9"),
    ("shieldBlockScale", "{x: 10, y: 1, z: 1}"), ("shieldSuper", "1"), ("shieldLinger", "20"), ("mountSmooth", "0.35"), ("horizonSmooth", "3"), ("turretTurn", "160"),
    ("mouseAimRadiansPerPixel", "0.0035"), ("missileSpeed", "180"), ("missileTurn", "150"),
    ("missileScale", elemental("1", "1.4", CHARGE, "0.5")), ("missileLife", "5"),
    ("launchCooldown", "0.6"), ("blastRadius", elemental("30", "46", SPACE, "5")), ("blastSeconds", "0.5"),
    ("missileCollectRadius", "9"), ("missileContactRadius", "3.46"),
    ("missileMesh", MISSILE_MESH_REF), ("missileMaterials", "\n  - " + "\n  - ".join(MISSILE_MATERIAL_REFS)),
    ("missileLength", "7"), ("missileTrailSeconds", "3"), ("missileTrailWidth", "1.4"), ("missileTrailSpacing", "6"),
    ("missileLightRange", "30"), ("missileLightIntensity", "3"), ("missileIgnitionSeconds", "0.15"),
    ("missileBankDeg", "45"), ("missileBankSmooth", "0.12"),
    ("startMissiles", "2"), ("maxMissiles", "4"),
    ("omniMissiles", "1"), ("gunsOn", "1"), ("shotInherit", "1"), ("seatElements", f"\n  - {TIME}\n  - {SPACE}\n  - {MASS}\n  - {CHARGE}"),
    ("sniperCooldown", "12"), ("sniperRange", "3000"), ("sniperConeDeg", "1.5"), ("sniperMinPathRadius", "6"),
    ("sniperStripPerElement", "0.1"), ("sniperEjectSpeed", "45"), ("sniperSameVictimCooldown", "0.5"),
    ("sniperDebrisSpeed", "90"), ("sniperDebrisSpeedLimit", "120"), ("sniperBeamStartWidth", "1.5"),
    ("sniperBeamSeconds", "0.35"), ("sniperFlareRadius", "18"), ("sniperFlareSeconds", "0.3"),
    ("fastGunRate", "30"), ("fastGunRoundSpeed", "135"), ("fastGunRoundSeconds", "4.5"), ("fastGunRoundScale", "1"),
    ("fastGunRoundGrowth", "3"), ("fastGunHeatSeconds", "6"), ("fastGunCoolSeconds", "5"),
    ("fastGunPenRate", "10"), ("fastGunPenReach", "120"), ("fastGunPenSpacing", "5"), ("fastGunPenFill", "2"),
    ("fastGunPenGap", "0.35"), ("fastGunPenCap", "150"), ("fastGunPenScale", "{x: 0.8, y: 0.5, z: 4.6}"),
    ("bombSpeed", "90"), ("bombFuse", "10"), ("bombBlastRadius", elemental("50", "76.666667", SPACE, "10")),
    ("bombScale", "3.75"), ("bombColor", "{r: 1, g: 0.072, b: 0.105, a: 1}"), ("bombLitStrength", "1"),
    ("aiSkill", "2"),
    ("aiRetargetSeconds", "0.4"), ("turretFov", "75"), ("sniperScopeFov", "22"), ("sniperCamBack", "12"),
    ("sniperCamUp", "6"), ("fastGunCamBack", "14"),
    ("fastGunCamUp", "8"), ("bombCamBack", "22"), ("bombCamUp", "10"), ("gunCamAim", "180"),
    ("missileCamBack", "14"), ("missileCamUp", "3"),
]
SOUNDS = ["missileLaunchSound", "missileBurstSound", "shieldRaiseSound", "omniRefillSound", "emptyMagazineSound",
          "sniperShotSound", "bombThrowSound", "bombBurstSound"]


def config_asset(script_guid: str) -> str:
    text = so_header(script_guid, "OrcaCrewConfig")
    text += "".join(f"  {k}: {v}\n" for k, v in CONFIG_NUMBERS)
    text += f"  explosions:\n  - {AOE_EXPLOSION_REF}\n"
    text += f"  bombExplosions:\n  - {AOE_GRIZZLY_REF}\n"
    text += f"  gameData: {{fileID: 11400000, guid: {GAME_DATA_GUID}, type: 2}}\n"
    text += f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_GUID}, type: 2}}\n"
    text += "".join(empty_event(s) for s in SOUNDS)
    text += '  seatLineTemplate: "{seat}  \\xB7  {gun}  \\xB7  {count} missiles  \\xB7  1-4 to change seat"\n'
    text += "  seatNames:\n  - Helm\n  - Sniper\n  - Fast gun\n  - Bomb\n"
    text += "  gunNames:\n  - \n  - Sniper\n  - Fast gun\n  - Bomb\n"
    text += "  gunLineTemplate: '{name} {state}'\n  gunReadyText: ready\n  gunCooldownTemplate: '{seconds}s'\n"
    text += "  gunHotText: overheated\n  bombOutText: bomb out\n"
    text += "  crosshairColor: {r: 0.95, g: 0.97, b: 0.98, a: 1}\n  crosshairReadyColor: {r: 0.6156863, g: 1, b: 0.18039216, a: 1}\n"
    text += "  crosshairHotColor: {r: 1, g: 0.302, b: 0.427, a: 1}\n  crosshairWarmColor: {r: 1, g: 0.655, b: 0, a: 1}\n"
    return text


def folder_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
            "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def lit_material() -> str:
    """An opaque URP/Lit the crew tints per domain (OrcaCrewView). Referenced from Resources so the build keeps the
    shader (vessel skill rule 55); instancing on."""
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_Name: OrcaCrewLit\n"
            f"  m_Shader: {{fileID: 4800000, guid: {URP_LIT_SHADER_GUID}, type: 3}}\n"
            "  m_Parent: {fileID: 0}\n  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n"
            "  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 1\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n"
            "  stringTagMap: {}\n  disabledShaderPasses: []\n  m_LockedProperties: \n  m_SavedProperties:\n"
            "    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats:\n    - _Metallic: 0.25\n"
            "    - _Smoothness: 0.65\n    - _Surface: 0\n    m_Colors:\n    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}\n"
            "    - _Color: {r: 1, g: 1, b: 1, a: 1}\n  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n")


def mat_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\n"
            "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def container_retrofit(text: str) -> str:
    return text.replace(f"  - {{fileID: 11400000, guid: {W.SPARROW_WARD_EFFECT_GUID}, type: 2}}\n", "")


def container_asset(donor: str) -> str:
    text = container_retrofit(donor)
    text = replace_once(text, "  m_Name: SparrowImpactorDataContainer\n", "  m_Name: OrcaImpactorDataContainer\n", "container name")
    m = re.search(r"  vesselCrystalEffects:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)*)", text)
    if not m:
        raise RuntimeError("donor drifted: the Sparrow container's vesselCrystalEffects list was not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {OMNI_EFFECT_GUID}, type: 2}}\n" + text[m.end(1):]


# ----------------------------------------------------------------------------- the class asset and the map
DESCRIPTION = ("  Description: Four seats, one hull - a helm and three gunners. The helm flies with two\n"
               "    thumbs, boosts on the right trigger and rings the hull in danger prisms on the left;\n"
               "    each gunner looks out of a stabilized turret and flies the missile it releases. Every\n"
               "    empty seat is flown by the AI. Keys 1-4 or the D-pad change seat.\n")
# A multiplayer vessel is named by its seats (Garrett, 2026-10-20): SO_Vessel.CardName adds "· 4 seat".
SEATS = "  SeatCount: 4\n"


def class_asset(donor_class: str) -> str:
    text = replace_once(donor_class, "  m_Name: SO_Class_Sparrow\n", "  m_Name: SO_Class_Orca\n", "class m_Name")
    text = replace_once(text, f"  Class: {SPARROW_CLASS_ID}\n", f"  Class: {ORCA_CLASS_ID}\n", "class Class")
    text = replace_once(text, "  Name: Sparrow\n", "  Name: Orca\n", "class Name")
    text, n = re.subn(r"  Description: .*\n(?:    .*\n)*", lambda m: DESCRIPTION + SEATS, text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class Description was not found")
    text, n = re.subn(r"  isLocked: \d\n", "  isLocked: 0\n  ownedFromStart: 1\n", text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class isLocked line was not found")
    return text


def register_class_list(text: str) -> str:
    if CLASS_GUID in text:
        return text
    m = re.search(r"  VesselList:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    if not m:
        raise RuntimeError("class list: VesselList not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {CLASS_GUID}, type: 2}}\n" + text[m.end(1):]


def register_container(text: str) -> str:
    if PREFAB_GUID in text:
        return text
    m = re.search(r"  _shipPrefabs:\n((?:  - \{fileID: -?\d+, guid: [0-9a-f]{32}, type: 3\}\n)+)", text)
    if not m:
        raise RuntimeError("Vessel Prefab Container: _shipPrefabs list not found")
    return text[:m.end(1)] + f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n" + text[m.end(1):]


def register_network(text: str) -> str:
    if PREFAB_GUID in text:
        return text
    if not text.endswith("\n"):
        text += "\n"
    return text + ("  - Override: 0\n"
                   f"    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n"
                   "    SourcePrefabToOverride: {fileID: 0}\n"
                   "    SourceHashToOverride: 0\n"
                   "    OverridingTargetPrefab: {fileID: 0}\n")


# The crew's own cards. Input 0 (no button chip): the helm's boost and shield are on the triggers and the gunners'
# Release is the crew's. No level-5 upgrades: none is designed yet (vessel skill §3, the design-approval gate), so every
# UpgradeLabel is empty. The words are authored data the team edits; written once.
ORCA_MAP_ENTRIES = [
    (1, "Flown Missiles", "Each gunner launches missiles from its own magazine and flies them by hand; Charge makes them "
        "faster and sharper. An omni crystal gives every gunner a missile. The card counts your missiles."),
    (3, "Burst", "A missile bursts on contact, after five seconds, or when its gunner presses Missile again, and the "
        "third gunner's bomb bursts the same way; Space widens both blasts. The card flashes on every burst."),
    (2, "Danger Shield", "The helm rings the hull in super-shielded prisms, open ahead, on the left trigger; they ride "
        "with the hull, then drop where they are as danger prisms. Mass carries the ring longer. The card shows its "
        "recharge."),
    (4, "Boost Tank", "The helm boosts on the right trigger while the tank lasts; it refills on its own, faster with "
        "Time. The card shows the tank."),
]


def map_entries() -> str:
    out = "  entries:\n"
    for element, label, desc in ORCA_MAP_ENTRIES:
        out += (f"  - Element: {element}\n    AbilityLabel: {W.yaml_str(label)}\n    AbilityDescription: {W.yaml_str(desc)}\n"
                f"    Input: 0\n    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n"
                f"    UpgradeLabel: \n    UpgradeDescription: \n")
    return out


def build_map(donor_map: str) -> str:
    text = replace_once(donor_map, "  m_Name: Sparrow\n", "  m_Name: Orca\n", "map name")
    text = replace_once(text, f"  vesselClass: {SPARROW_CLASS_ID}\n", f"  vesselClass: {ORCA_CLASS_ID}\n", "map vesselClass")
    return text[:text.index("  entries:\n")] + map_entries()


def register_fusion(text: str) -> str:
    if W.fusion_entries(text, ORCA_CLASS_ID):
        return text
    donor = W.fusion_entries(text, SPARROW_CLASS_ID)
    if len(donor) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Sparrow entries, found {len(donor)}")
    last = None
    for m in re.finditer(r"  - vessel: \d+\n(?:    .*\n)+", text):
        last = m
    added = "".join(e.replace(f"  - vessel: {SPARROW_CLASS_ID}\n", f"  - vessel: {ORCA_CLASS_ID}\n", 1) for e in donor)
    return text[:last.end()] + added + text[last.end():]


# ----------------------------------------------------------------------------- checks
def check_prefab(text: str, script_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want("  m_Name: Orca\n" in text, "Orca.prefab: the root is not named Orca")
    want("  _name: Orca\n" in text, "Orca.prefab: VesselStatus _name is not Orca")
    want(f"  vesselType: {ORCA_CLASS_ID}\n" in text, f"Orca.prefab: vesselType is not {ORCA_CLASS_ID}")
    want(f"  vesselType: {SPARROW_CLASS_ID}\n" not in text, "Orca.prefab: still carries the Sparrow's vesselType")
    h = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(h and int(h.group(1)) == global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "Orca.prefab: GlobalObjectIdHash is not this guid's (a copied hash collides with the donor in Netcode)")
    ip = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(ip and ip.group(1) == "0", "Orca.prefab: InScenePlacedSourceGlobalObjectIdHash is not cleared")
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    want(root and f"{{fileID: {SEAT_ID}}}" in root.group(1), "Orca.prefab: OrcaCrew is not on the root's component list")
    seat = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % SEAT_ID, text)
    want(seat and f"guid: {script_guid}" in seat.group(1), "Orca.prefab: the OrcaCrew document is missing or names another script")
    want(seat and f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}" in seat.group(1),
         "Orca.prefab: OrcaCrew.config is not OrcaCrewConfig.asset (a null config never seats the crew)")
    want("  _inputEventShipActions: []\n" in text, "Orca.prefab: the hull still binds buttons (the crew owns the triggers)")
    want(f"guid: {W.TWO_STICK_TRANSFORMER_GUID}, type: 3" in text and W.SINGLE_STICK_TRANSFORMER_GUID not in text,
         "Orca.prefab: the hull is not on the two-stick VesselTransformer (the helm flies with two thumbs)")
    want(f"guid: {ORCA_CONTAINER_GUID}," in text and f"guid: {W.DONOR_CONTAINER_GUID}," not in text,
         "Orca.prefab: the VesselImpactor is not on OrcaImpactorDataContainer (an Omni crystal would not refill the magazine)")
    names = set(re.findall(r"^  m_Name: (.*)$", text, re.M))
    for n in W.SPARROW_SUBTREES:
        want(n not in names, f"Orca.prefab: the Sparrow's {n} is still on the hull")
    for n in W.SPARROW_COMPONENTS + ["SparrowVesselTelemetry"]:
        want(f"guid: {W.guid_of_script(n)}, type: 3" not in text, f"Orca.prefab: the Sparrow's {n} is still on the hull")
    ai = re.search(r"guid: %s, type: 3\}\n((?:.*\n)*?)(?=--- !u!|\Z)" % W.guid_of_script("AIPilot"), text)
    want(ai and "  abilities: []\n" in ai.group(1), "Orca.prefab: the AIPilot still cycles the Sparrow's abilities")
    hud = re.search(r"guid: %s, type: 3\}\n((?:.*\n)*?)(?=--- !u!|\Z)" % W.guid_of_script("OrcaHUDController"), text)
    want(hud and f"  crew: {{fileID: {SEAT_ID}}}\n" in hud.group(1) and W.guid_of_script("SparrowHUDController") not in text,
         "Orca.prefab: the HUD is not OrcaHUDController wired to the crew (the cards would not read the magazine)")
    for go, gun, pool, name, ref in ((FAST_GO, FAST_GUN, FAST_POOL, "OrcaFastGun", SPARROW_ROUND_REF),
                                     (BOMB_GO, BOMB_GUN, BOMB_POOL, "OrcaBombGun", GRIZZLY_BOMB_REF)):
        want(f"--- !u!1 &{go}\n" in text and f"  m_Name: {name}\n" in text, f"Orca.prefab: no {name} object under the root (round 5)")
        want(seat and f"{{fileID: {gun}}}" in seat.group(1), f"Orca.prefab: OrcaCrew is not wired to {name}'s Gun")
        pd = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % pool, text)
        want(pd and f"  prefab: {ref}" in pd.group(1), f"Orca.prefab: {name}'s pool does not hold its projectile prefab")
    dangling = W.dangling_local_refs(text)
    want(not dangling, f"Orca.prefab: dangling local fileID reference(s) {dangling[:5]}")
    return errs


def check_assets(config: str, container: str, effect: str, material: str, config_script: str, omni_script: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"guid: {config_script}, type: 3" in config, "OrcaCrewConfig.asset: does not name OrcaCrewConfigSO")
    want(AOE_EXPLOSION_REF in config, "OrcaCrewConfig.asset: explosions is not AOEExplosion.prefab (a missile would burst into nothing)")
    want(f"guid: {GAME_DATA_GUID}" in config, "OrcaCrewConfig.asset: gameData is unwired (no rival hulls for the gunners or the missiles)")
    want(AOE_GRIZZLY_REF in config, "OrcaCrewConfig.asset: bombExplosions is not AOEGrizzlyExplosion.prefab (a bomb would burst into nothing)")
    want(f"guid: {COMBAT_HIT_GUID}" in config, "OrcaCrewConfig.asset: onCombatHitLanded is unwired (sniper hits would not score)")
    want(f"  - {{fileID: 11400000, guid: {OMNI_EFFECT_GUID}, type: 2}}\n" in container.split("  vesselElementalCrystalEffects:")[0],
         "OrcaImpactorDataContainer: OrcaOmniCrystalEffect is not in its omni (vesselCrystalEffects) list")
    want(W.SPARROW_WARD_EFFECT_GUID not in container, "OrcaImpactorDataContainer: still carries the Sparrow's Omni ward")
    want(f"guid: {omni_script}, type: 3" in effect, "OrcaOmniCrystalEffect.asset: does not name OrcaOmniCrystalEffectSO")
    want(f"guid: {URP_LIT_SHADER_GUID}, type: 3" in material and "m_EnableInstancingVariants: 1" in material,
         "Resources/OrcaCrewLit.mat: not URP/Lit with instancing (the build would strip the crew's shader)")
    return errs


def check_registrations(container: str, network: str, lists, cls: str, kmap: str, fusion: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in container,
         "Vessel Prefab Container: no Orca entry on its root TRANSFORM (the Vessel Changer offers only what is here)")
    want(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in network,
         "DefaultNetworkPrefabs: no Orca entry on its root GAMEOBJECT (a swap to it cannot spawn)")
    for name, text in lists:
        want(text.count(CLASS_GUID) == 1, f"{name}: SO_Class_Orca listed {text.count(CLASS_GUID)} times, not once")
    want(f"  Class: {ORCA_CLASS_ID}\n" in cls and "  ownedFromStart: 1\n" in cls and "  isLocked: 0\n" in cls,
         "SO_Class_Orca: wrong class, locked, or not owned from the start")
    want(SEATS in cls, "SO_Class_Orca: SeatCount is not 4 (its card must say it seats four)")
    want(f"  vesselClass: {ORCA_CLASS_ID}\n" in kmap and "  m_Name: Orca\n" in kmap, "ElementalAbilityMaps/Orca: wrong class or name")
    inputs = re.findall(r"^    Input: (-?\d+)\n", kmap, re.M)
    want(len(inputs) == 4 and set(inputs) == {"0"}, f"ElementalAbilityMaps/Orca: inputs {inputs} (every card is passive, Input 0)")
    want("Pulsefire" not in kmap and "Skyburst" not in kmap, "ElementalAbilityMaps/Orca: still the Sparrow's cards")
    orca, sparrow = W.fusion_entries(fusion, ORCA_CLASS_ID), W.fusion_entries(fusion, SPARROW_CLASS_ID)
    want(len(orca) == 4, f"CrystalHullFusionConfig: {len(orca)} Orca entries, not 4 (CrystalHullFusionConfigTests needs one per element)")
    want([e.split("\n", 1)[1] for e in orca] == [e.split("\n", 1)[1] for e in sparrow],
         "CrystalHullFusionConfig: the Orca's entries are not the Sparrow's (it draws the Sparrow's mesh)")
    return errs


read, write = W.read, W.write


def check_all():
    script_guid = W.guid_of(SEAT_SCRIPT_META)
    errs = []
    if not os.path.exists(PREFAB) or not os.path.exists(PREFAB + ".meta"):
        return ["Orca.prefab (+ .meta) is missing: run Tools/Build/author_orca_assets.py"]
    errs += [] if W.guid_of(PREFAB + ".meta") == PREFAB_GUID else ["Orca.prefab.meta: guid is not PREFAB_GUID"]
    errs += check_prefab(read(PREFAB), script_guid)
    errs += check_model(read(PREFAB))
    for path in (CLASS_ASSET, MAP, CONFIG, ORCA_CONTAINER, OMNI_EFFECT, LIT_MAT, CONFIG_DIR):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    for path, guid in ((CONFIG, CONFIG_GUID), (ORCA_CONTAINER, ORCA_CONTAINER_GUID), (OMNI_EFFECT, OMNI_EFFECT_GUID),
                       (LIT_MAT, LIT_MAT_GUID), (CLASS_ASSET, CLASS_GUID), (MAP, MAP_GUID)):
        if W.guid_of(path + ".meta") != guid:
            errs.append(f"{os.path.relpath(path, ROOT)}.meta: guid is not the one the prefab and registrations name")
    errs += check_assets(read(CONFIG), read(ORCA_CONTAINER), read(OMNI_EFFECT), read(LIT_MAT),
                         W.guid_of(CONFIG_SCRIPT_META), W.guid_of(OMNI_SCRIPT_META))
    errs += check_registrations(read(W.CONTAINER), read(W.NETWORK_PREFABS),
                                [(os.path.basename(p), read(p)) for p in W.CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(W.FUSION))
    return errs


def author():
    script_guid = W.guid_of(SEAT_SCRIPT_META)
    if not os.path.exists(PREFAB):   # the spent one-shot stands down once the prefab is committed
        write(PREFAB, build_clone(read(DONOR), script_guid))
        write(PREFAB + ".meta", prefab_meta())
    else:                            # the retrofits are not one-shots: they apply to the committed prefab
        old = read(PREFAB); new = retrofit(old)
        if new != old:
            write(PREFAB, new)
    if not os.path.isdir(CONFIG_DIR):
        os.makedirs(CONFIG_DIR)
    if not os.path.exists(CONFIG_DIR + ".meta"):
        write(CONFIG_DIR + ".meta", folder_meta(CONFIG_DIR_GUID))
    if not os.path.exists(CONFIG):   # seeded once; the lab owns the numbers afterwards (parity.cjs)
        write(CONFIG, config_asset(W.guid_of(CONFIG_SCRIPT_META))); write(CONFIG + ".meta", W.native_meta(CONFIG_GUID))
    if not os.path.exists(OMNI_EFFECT):
        write(OMNI_EFFECT, so_header(W.guid_of(OMNI_SCRIPT_META), "OrcaOmniCrystalEffect")); write(OMNI_EFFECT + ".meta", W.native_meta(OMNI_EFFECT_GUID))
    if not os.path.exists(ORCA_CONTAINER):
        write(ORCA_CONTAINER, container_asset(read(W.DONOR_CONTAINER))); write(ORCA_CONTAINER + ".meta", W.native_meta(ORCA_CONTAINER_GUID))
    else:
        old = read(ORCA_CONTAINER); new = container_retrofit(old)
        if new != old:
            write(ORCA_CONTAINER, new)
    if not os.path.exists(LIT_MAT):
        write(LIT_MAT, lit_material()); write(LIT_MAT + ".meta", mat_meta(LIT_MAT_GUID))
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset(read(W.DONOR_CLASS))); write(CLASS_ASSET + ".meta", W.native_meta(CLASS_GUID))
    if not os.path.exists(MAP):
        write(MAP, build_map(read(W.DONOR_MAP))); write(MAP + ".meta", W.native_meta(MAP_GUID))
    for path, fn in [(W.CONTAINER, register_container), (W.NETWORK_PREFABS, register_network), (W.FUSION, register_fusion)] + \
                    [(p, register_class_list) for p in W.CLASS_LISTS]:
        old = read(path); new = fn(old)
        if new != old:
            write(path, new)


def self_test():
    """Every check must fire on a broken copy of the shipped set (negative controls), and pass on it intact."""
    script_guid = W.guid_of(SEAT_SCRIPT_META)
    good = build_clone(read(DONOR), script_guid)
    fails = []
    def expect(errs, n_min, what):
        if not ((len(errs) == 0) if n_min == 0 else (len(errs) >= n_min)):
            fails.append(f"{what}: {errs}")
    expect(check_prefab(good, script_guid), 0, "a fresh clone passes")
    if os.path.exists(ORCA_FBX):   # round 6: the model's nesting (stands down with the retrofit while the model is unbuilt)
        modeled = retrofit(good)
        expect(check_model(modeled), 0, "the nested model passes")
        expect(check_model(modeled.replace(SPARROW_SMR_OFF, "")), 1, "the Sparrow's mesh left drawing")
        expect(check_model(modeled.replace(f"  - {{fileID: {ORCA_MODEL_GO}}}\n", f"  - {{fileID: {SPARROW_MODEL_GO}}}\n")), 1, "the domain painting the hidden Sparrow")
        expect(check_model(modeled.replace(f"      value: {OrcaModelName}\n", "      value: Orca.fbx\n")), 1, "the model renamed (the rig would not find it)")
    expect(check_prefab(good.replace(f"  vesselType: {ORCA_CLASS_ID}\n", f"  vesselType: {SPARROW_CLASS_ID}\n"), script_guid), 1, "the Sparrow's vesselType")
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>1124080984", good, count=1), script_guid), 1, "the donor's hash")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {SEAT_ID}}}\n", ""), script_guid), 1, "the crew off the root")
    expect(check_prefab(good.replace(f"guid: {CONFIG_GUID}, type: 2", "guid: 00000000000000000000000000000000, type: 2"), script_guid), 1, "the crew's config unwired")
    expect(check_prefab(good.replace("  _inputEventShipActions: []\n", "  _inputEventShipActions:\n  - InputEvent: 1\n    ShipActions: []\n"), script_guid), 1, "a button bound")
    expect(check_prefab(good.replace(ORCA_CONTAINER_GUID, W.DONOR_CONTAINER_GUID), script_guid), 1, "the Sparrow's container")
    expect(check_prefab(good.replace(W.guid_of_script("OrcaHUDController"), W.guid_of_script("SparrowHUDController")), script_guid), 1, "the Sparrow's HUD controller")
    expect(check_prefab(good.replace(W.TWO_STICK_TRANSFORMER_GUID, W.SINGLE_STICK_TRANSFORMER_GUID), script_guid), 1, "the one-thumb transformer")
    expect(check_prefab(read(DONOR).replace("  m_Name: Sparrow\n", "  m_Name: Orca\n"), script_guid), 3, "the Sparrow's kit, untouched")
    expect(check_prefab(good + "  stray: {fileID: 987654321}\n", script_guid), 1, "a dangling local reference")
    expect(check_prefab(good.replace(f"  fastGun: {{fileID: {FAST_GUN}}}\n", "  fastGun: {fileID: 0}\n"), script_guid), 1, "the fast gun unwired")
    expect(check_prefab(good.replace(GRIZZLY_BOMB_REF, SPARROW_ROUND_REF), script_guid), 1, "the bomb pool holding rounds")
    if gun_retrofit(good) != good:
        fails.append("the gun retrofit is not idempotent")
    if "\n  RollScaler: 16.5\n" not in good or "\n  DefaultThrottleScaler: 21.25\n" not in good:
        fails.append("a fresh clone does not get the lab's hull (speed x0.85, turn x0.55)")
    tuned = good.replace("\n  RollScaler: 16.5\n", "\n  RollScaler: 40\n")
    if tuning_retrofit(tuned) != tuned:
        fails.append("the hull retrofit overwrote a value tuned after it")
    if retrofit(good) != good:
        fails.append("the retrofit is not idempotent")
    cfg_script, omni_script = W.guid_of(CONFIG_SCRIPT_META), W.guid_of(OMNI_SCRIPT_META)
    cfg, cont, eff, mat = config_asset(cfg_script), container_asset(read(W.DONOR_CONTAINER)), so_header(omni_script, "OrcaOmniCrystalEffect"), lit_material()
    expect(check_assets(cfg, cont, eff, mat, cfg_script, omni_script), 0, "the crew's assets pass")
    expect(check_assets(cfg.replace(AOE_EXPLOSION_REF, "{fileID: 0}"), cont, eff, mat, cfg_script, omni_script), 1, "no explosion")
    expect(check_assets(cfg.replace(GAME_DATA_GUID, "0" * 32), cont, eff, mat, cfg_script, omni_script), 1, "no game data")
    expect(check_assets(cfg.replace(AOE_GRIZZLY_REF, "{fileID: 0}"), cont, eff, mat, cfg_script, omni_script), 1, "no bomb blast")
    expect(check_assets(cfg.replace(COMBAT_HIT_GUID, "0" * 32), cont, eff, mat, cfg_script, omni_script), 1, "no scoreboard channel")
    expect(check_assets(cfg, read(W.DONOR_CONTAINER), eff, mat, cfg_script, omni_script), 2, "the Sparrow's container")
    expect(check_assets(cfg, cont, eff.replace(omni_script, "0" * 32), mat, cfg_script, omni_script), 1, "the effect names another script")
    expect(check_assets(cfg, cont, eff, mat.replace("m_EnableInstancingVariants: 1", "m_EnableInstancingVariants: 0"), cfg_script, omni_script), 1, "no instancing")
    container, network, fusion = register_container(read(W.CONTAINER)), register_network(read(W.NETWORK_PREFABS)), register_fusion(read(W.FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in W.CLASS_LISTS]
    cls, kmap = class_asset(read(W.DONOR_CLASS)), build_map(read(W.DONOR_MAP))
    expect(check_registrations(container, network, lists, cls, kmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(W.CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, kmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(W.NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, kmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, kmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls.replace("  isLocked: 0\n", "  isLocked: 1\n"), kmap, fusion), 1, "a locked class")
    expect(check_registrations(container, network, lists, cls.replace(SEATS, ""), kmap, fusion), 1, "a card without its seats")
    expect(check_registrations(container, network, lists, cls, kmap, fusion.replace(f"  - vessel: {ORCA_CLASS_ID}\n", "  - vessel: 99\n", 1)), 1, "a missing fusion entry")
    expect(check_registrations(container, network, lists, cls, read(W.DONOR_MAP), fusion), 1, "the Sparrow's own map")
    expect(check_registrations(container, network, lists, cls, kmap.replace("    Input: 0\n", "    Input: 7\n", 1), fusion), 1, "a card with a button")
    if register_container(container) != container or register_fusion(fusion) != fusion or register_network(network) != network:
        fails.append("registration is not idempotent")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_orca_assets self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errs = check_all()
    for e in errs:
        print("FAIL:", e)
    print("author_orca_assets:", "ok" if not errs else f"{len(errs)} problem(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
