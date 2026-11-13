#!/usr/bin/env python3
"""Split the Rhino in two: the SWORDFISH (all about the sword) and the RHINO (all about the plough).

    python3 Tools/Build/author_swordfish_assets.py             # author what is missing, then check
    python3 Tools/Build/author_swordfish_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_swordfish_assets.py --self-test # prove every check fires

Garrett, 2026-11-07: "split the rhino into two vessels: the rhino and a swordfish. the sword fish will be all about the
sword. the rhino will be all about the plow." His answers the same day: the Rhino's plough kit (Space Furrow / L5
Unstoppable, Mass Blade / L5 Berm, Charge Carry / L5 Avalanche); a trigger pull is "a little shove in the direction of
the trigger, holding it keeps the plough steered"; the Swordfish keeps the ramp boost; the sword modes move (Cleave
becomes the Swordfish's, Astro League seats it in the Rhino's place; Headlong stays the Rhino's). Design:
Assets/_Scripts/Controller/Vessel/R_VesselActions/SWORDFISH.md and RHINO_PLOUGH.md.

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/Swordfish.prefab (+ .meta)   a text clone of Rhino.prefab AS IT WAS (with the sword):
        - the root GameObject and VesselStatus named Swordfish, `vesselType: 30` (VesselClassType.Swordfish)
        - a FRESH Netcode GlobalObjectIdHash for this guid (a disk copy keeps the donor's; Netcode keys on it alone)
        - its own vessel impactor container (SwordfishImpactorDataContainer): the Rhino's, with the plough replaced by
          the fleet's ram (VesselDamagePrismEffect) - the hull breaks what it rams, the sword cuts ahead of it
      Everything else is the Rhino's sword kit, unchanged: the blade, energize, afterimages, the surge ring, the ramp
      boost, Momentum, the HUD, the camera, the model (the Rhino's rig until a swordfish model is made, /vessel-model).
    Then Rhino.prefab LOSES the sword (a document-level pass, `dangling_refs` gates it):
        - the swing executor's component becomes RhinoPloughSteerExecutor IN PLACE (same fileID, so the action
          registry's list still names it), its GameObject renamed PloughSteerActionRegistry; LT/RT (pad and touch
          overrides, InputEvents 2 / 1) bind RhinoPloughSteerLeft / Right instead of the shield swipes
        - removed: ScaleSkimmerObject (the blade driver + the afterimages), the inactive ZoomCameraObject that read the
          driver, the five blade tracers, the sword FX added to
          the blade, SwordfishSwordNetworkRelay and SwordfishSwordAutopilot; the HUD controller's sword references are nulled
        - the blade skimmer stays as the Rhino's only near-field skimmer (vessel rule 11: an elemental crystal is
          collected only by an initialised skimmer), re-posed as a static 15 u crystal catcher round the hull with a
          haptics-only container (RhinoSkimmerImpactorDataContainer) - it cuts nothing and takes no petals
        - a new PloughBlade hull collider across the nose (RhinoPloughBlade: its width is the Mass ability)
    RhinoImpactorDataContainer drops the omni-crystal surge (the sword ring is the Swordfish's).
    SO_Class_Swordfish + its entry in SO_Classlist_All / _Classes; one entry each in `Vessel Prefab Container.asset`
    (the root TRANSFORM) and `DefaultNetworkPrefabs.asset` (the root GAMEOBJECT); Resources/ElementalAbilityMaps/
    Swordfish.asset (the Rhino's sword cards) and the Rhino's map rewritten as the plough kit; four
    CrystalHullFusionConfig entries for class 30 reusing the Rhino's bakes (it draws the Rhino's mesh, vessel rule 42).
    The enum, the toybox roster, the camera feel row and the touch-profile row are CODE and are written by hand
    (vessel rule 40); NetworkManager.prefab's ProtocolVersion is raised by hand in the same change.

BOTH PREFAB PASSES ARE SPENT ONE-SHOTS ONCE COMMITTED (CLAUDE.md): the clone runs only while Swordfish.prefab is absent
and the Rhino still carries its sword; the strip only while the Rhino still carries the swing executor. `--check`
validates the SHIPPED prefabs' invariants, never a diff against a donor that has moved on.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import (global_object_id_hash, replace_once, split_docs, join_docs, get_doc,  # noqa: E402
                                 set_doc, remove_docs, remove_gameobject_tree, dangling_refs, add_child)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
RHINO = os.path.join(VESSELS, "Rhino.prefab")
PREFAB = os.path.join(VESSELS, "Swordfish.prefab")
SO = os.path.join(ROOT, "Assets", "_SO_Assets")
CONTAINER = os.path.join(SO, "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(SO, "Classes")
RHINO_CLASS = os.path.join(CLASSES, "SO_Class_Rhino.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Swordfish.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
RHINO_MAP = os.path.join(MAPS, "Rhino.asset")
MAP = os.path.join(MAPS, "Swordfish.asset")
FUSION = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
EFFECTS = os.path.join(SO, "Effects")
VESSEL_CONTAINERS = os.path.join(EFFECTS, "Effect Containers", "VesselContainers")
SKIMMER_CONTAINERS = os.path.join(EFFECTS, "Effect Containers", "SkimmerContainers")
RHINO_VESSEL_CONTAINER = os.path.join(VESSEL_CONTAINERS, "RhinoImpactorDataContainer.asset")
SWORDFISH_VESSEL_CONTAINER = os.path.join(VESSEL_CONTAINERS, "SwordfishImpactorDataContainer.asset")
RHINO_SKIMMER_CONTAINER = os.path.join(SKIMMER_CONTAINERS, "RhinoSkimmerImpactorDataContainer.asset")
ACTION_DIR = os.path.join(SO, "VesselActions", "Rhino")
STEER_RIGHT = os.path.join(ACTION_DIR, "RhinoPloughSteerRightAction.asset")
STEER_LEFT = os.path.join(ACTION_DIR, "RhinoPloughSteerLeftAction.asset")
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel")
STEER_SO_META = os.path.join(SCRIPTS, "R_VesselActions", "Data Containers", "RhinoPloughSteerActionSO.cs.meta")
STEER_EXEC_META = os.path.join(SCRIPTS, "R_VesselActions", "Executors", "RhinoPloughSteerExecutor.cs.meta")
BLADE_META = os.path.join(SCRIPTS, "RhinoPloughBlade.cs.meta")

# 24-27 are claimed on in-flight branches (Flea, Warden, Bulwark, Tempest), 28 is the Meerkat, 29 the Tiger Moth; 30 was
# free on every origin branch on 2026-11-07.
CLASS_ID = 30
RHINO_CLASS_ID = 3
PREFAB_GUID = "5d0f3c9e8a7b4e21b6c4f1a2e9d80b37"
CLASS_GUID = "a7e14c2b9d3f4a6c8b5e0d1f2c3a4b96"
MAP_GUID = "3b9e7d1c5a2f4e8d9c6b0a1e2f3d4c58"
SWORDFISH_VESSEL_CONTAINER_GUID = "c4a8e2f6b1d94c37a5e0f9b8d7c6a213"
RHINO_SKIMMER_CONTAINER_GUID = "e9b3c7a1d5f24b68c0a4e8f2b6d1c975"
STEER_RIGHT_GUID = "1f6a9c3e7b2d4f80a5c1e9b3d7f2a648"
STEER_LEFT_GUID = "8d2b6f0a4c9e4d13b7f5a1c8e3b9d0f2"

# --- the Rhino prefab's fileIDs (measured 2026-11-07 on bleeding-edge 89216249) ---------------------------------------
ROOT_GO = "6417075533431866457"
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "3711687605623876765"
VESSEL_IMPACTOR_ID = "3485795873575935315"
HUD_CONTROLLER_ID = "2171807218592972036"
SWING_EXECUTOR_ID = "5837219046513728403"   # ShieldSwipeActionExecutor -> RhinoPloughSteerExecutor (in place)
SWING_GO = "5837219046513728401"
SCALE_SKIMMER_GO = "2798795461378093357"    # ShieldSkimmerScaleDriver + SwordfishSwordAfterimage
ZOOM_CAMERA_GO = "6085091417435899968"      # inactive CameraZoomFollowScaleProvider reading the blade driver
TRACER_GOS = ["5300000000001101", "5300000000001111", "5300000000001121", "5300000000001131", "5300000000001141"]
SWORD_FX_ID = "5300000000001301"            # SwordfishSwordFXController, ADDED to the blade instance
RELAY_ID = "7740000000000001601"
SWORD_AUTOPILOT_ID = "7740000000000001603"
BLADE_INSTANCE = "1660592348679971898"      # the ForceFieldSkimmer Variant instance (the blade / crystal catcher)
SKIMMER_SOURCE = "1957019139854139517"      # its Skimmer component's id in the variant
NEAR_SKIMMER_ID = "874579554510349383"
PLOUGH_BLADE_GO = "7741000000000002001"
PLOUGH_BLADE_TRANSFORM = "7741000000000002002"
PLOUGH_BLADE_BOX = "7741000000000002003"
PLOUGH_BLADE_IMPACT = "7741000000000002004"
PLOUGH_BLADE_COMPONENT = "7741000000000002005"

# --- script and asset guids ------------------------------------------------------------------------------------------
SWING_EXECUTOR_SCRIPT = "fc766772d5459db71a1c792a127f2e48"   # ShieldSwipeActionExecutor
SWIPE_RIGHT_GUID = "b92a3e13c6d6cae4c13166674711fddb"        # SwordfishShieldSwipeRightAction
SWIPE_LEFT_GUID = "4ff8e060662050b3e6c52df601ba9ca2"         # SwordfishShieldSwipeLeftAction
IMPACT_COLLIDER_SCRIPT = "fafcc767febd41ccbad67c5457dc432d"
SWORD_SCRIPTS = {   # nothing of the sword may stay on the Rhino
    "ShieldSkimmerScaleDriver": "ShieldSkimmerScaleDriver.cs.meta",
    "SwordfishSwordAfterimage": "SwordfishSwordAfterimage.cs.meta",
    "SwordfishSwordFXController": "SwordfishSwordFXController.cs.meta",
    "SwordfishSwordNetworkRelay": "SwordfishSwordNetworkRelay.cs.meta",
    "SwordfishSwordAutopilot": "SwordfishSwordAutopilot.cs.meta",
}
PLOUGH_EFFECT_GUID = "dd3c981395e34324b68b9ce34145854c"      # RhinoPloughPrismEffect.asset
RAM_EFFECT_GUID = "4da62d4eefe03594ba35027dd537aff0"         # VesselDamagePrismEffect.asset (the fleet ram)
SURGE_EFFECT_GUID = "2c9905659013874571988cf2fda5904c"       # SwordfishVesselSurgeByCrystalEffect.asset
SWORD_SKIMMER_CONTAINER_GUID = "05483649aa8bbea458ee49ed300542bd"   # SwordfishForceFieldSkimmerImpactorDataContainer
SKIMMER_CONTAINER_SCRIPT = "68b3e74284f14e32a6e21b5fb8c3e8ad"
VESSEL_HAPTICS_BY_SKIMMER = "02cd4a20ea91bbc4591c9ccbf9db91af"
SKIMMER_HAPTICS_BY_PRISM = "dcce08b9351b97c4eb275508001a5b8e"


def guid_of(meta_path: str) -> str:
    with open(meta_path, encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
    if not m:
        raise RuntimeError(f"{os.path.relpath(meta_path, ROOT)}: no guid")
    return m.group(1)


def script_guid(file_name: str) -> str:
    for d, _, files in os.walk(os.path.join(ROOT, "Assets", "_Scripts")):
        if file_name in files:
            return guid_of(os.path.join(d, file_name))
    raise RuntimeError(f"no script {file_name}")


def read(path: str) -> str:
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def write(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print(f"  wrote {os.path.relpath(path, ROOT)}")


def asset_meta(guid: str, main_object: str = "11400000") -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: {main_object}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def prefab_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n")


def mono_asset(script: str, name: str, body: str) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n  m_Name: {name}\n  m_EditorClassIdentifier: \n"
            + body)


def has_sword(rhino: str) -> bool:
    return f"guid: {SWING_EXECUTOR_SCRIPT}, type: 3" in rhino


# ----------------------------------------------------------------------------- the Swordfish prefab (a clone)
def clone_prefab(donor: str) -> str:
    t = donor
    t = replace_once(t, "  m_Name: Rhino\n  m_TagString", "  m_Name: Swordfish\n  m_TagString", "root name")
    t = replace_once(t, f"  _name: Rhino\n  vesselType: {RHINO_CLASS_ID}\n",
                     f"  _name: Swordfish\n  vesselType: {CLASS_ID}\n", "VesselStatus")
    t = re.sub(r"(  GlobalObjectIdHash: )\d+\n", rf"\g<1>{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n",
               t, count=1)
    t = re.sub(r"(  InScenePlacedSourceGlobalObjectIdHash: )\d+\n", r"\g<1>0\n", t, count=1)
    rhino_container = guid_of(RHINO_VESSEL_CONTAINER + ".meta")
    t = replace_once(t, f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {rhino_container},",
                     f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {SWORDFISH_VESSEL_CONTAINER_GUID},",
                     "the vessel impactor container")
    return t


# ----------------------------------------------------------------------------- the Rhino loses its sword
def strip_rhino(rhino: str) -> str:
    head, docs = split_docs(rhino)
    exec_guid = guid_of(STEER_EXEC_META)

    # 1. the swing executor becomes the plough steer, in place
    old = get_doc(docs, SWING_EXECUTOR_ID)
    if old is None or f"guid: {SWING_EXECUTOR_SCRIPT}" not in old:
        raise RuntimeError("Rhino drifted: the swing executor is not where it was")
    set_doc(docs, SWING_EXECUTOR_ID, old.split("  m_Script:")[0] +
            f"  m_Script: {{fileID: 11500000, guid: {exec_guid}, type: 3}}\n  m_Name: \n"
            "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Gameplay.RhinoPloughSteerExecutor\n")
    go = get_doc(docs, SWING_GO)
    set_doc(docs, SWING_GO, replace_once(go, "  m_Name: ShieldSwipeActionRegistry\n",
                                         "  m_Name: PloughSteerActionRegistry\n", "the swing GameObject"))

    # 2. the blade driver, the afterimages and the tracers
    remove_gameobject_tree(docs, SCALE_SKIMMER_GO)
    remove_gameobject_tree(docs, ZOOM_CAMERA_GO)
    for tracer in TRACER_GOS:
        remove_gameobject_tree(docs, tracer)

    # 3. the sword FX added to the blade instance, the relay and the sword's autopilot on the root
    inst = get_doc(docs, BLADE_INSTANCE)
    inst = re.sub(r"    - targetCorrespondingSourceObject: \{fileID: -?\d+, guid: [0-9a-f]+,\n        type: 3\}\n"
                  rf"      insertIndex: -?\d+\n      addedObject: \{{fileID: {SWORD_FX_ID}\}}\n", "", inst)
    if f"addedObject: {{fileID: {SWORD_FX_ID}}}" in inst:
        raise RuntimeError("Rhino drifted: could not detach the sword FX from the blade instance")
    remove_docs(docs, [SWORD_FX_ID, RELAY_ID, SWORD_AUTOPILOT_ID])
    root = get_doc(docs, ROOT_GO)
    for c in (RELAY_ID, SWORD_AUTOPILOT_ID):
        root = replace_once(root, f"  - component: {{fileID: {c}}}\n", "", f"root component {c}")
    set_doc(docs, ROOT_GO, root)

    # 4. the blade skimmer becomes a static crystal catcher round the hull (vessel rule 11)
    inst = re.sub(r"(      propertyPath: m_LocalScale\.[xyz]\n      value: )[-\d.e]+", r"\g<1>30", inst)
    inst = re.sub(r"(      propertyPath: m_LocalPosition\.[xyz]\n      value: )[-\d.e]+", r"\g<1>0", inst)
    inst = re.sub(r"(      propertyPath: m_LocalRotation\.w\n      value: )[-\d.e]+", r"\g<1>1", inst)
    inst = re.sub(r"(      propertyPath: m_LocalRotation\.[xyz]\n      value: )[-\d.e]+", r"\g<1>0", inst)
    inst = re.sub(r"(      propertyPath: Scale\.(?:Max|Min|Value)\n      value: )[-\d.e]+", r"\g<1>30", inst)
    inst = re.sub(r"(      propertyPath: Scale\.element\n      value: )\d+", r"\g<1>0", inst)
    inst = re.sub(r"(      propertyPath: m_Name\n      value: )ForceFieldSkimmer\n", r"\g<1>CrystalSkimmer\n", inst)
    inst = inst.replace(f"objectReference: {{fileID: 11400000, guid: {SWORD_SKIMMER_CONTAINER_GUID},",
                        f"objectReference: {{fileID: 11400000, guid: {RHINO_SKIMMER_CONTAINER_GUID},", 1)
    extra = "".join(
        f"    - target: {{fileID: {SKIMMER_SOURCE}, guid: c8d5df1098821194eb69eec4c965548e, type: 3}}\n"
        f"      propertyPath: {path}\n      value: {value}\n      objectReference: {{fileID: 0}}\n"
        for path, value in (("elongateYOnly", 0), ("Scale.Enabled", 0)))
    inst = replace_once(inst, "    m_RemovedComponents:", extra + "    m_RemovedComponents:", "the blade's modifications")
    set_doc(docs, BLADE_INSTANCE, inst)

    # 5. LT / RT steer the plough (pad and touch overrides alike)
    handler_id = next(k for k, c in docs if "  _gamepadActionOverrides:" in c)
    h = get_doc(docs, handler_id)
    h = h.replace(f"guid: {SWIPE_RIGHT_GUID}, type: 2", f"guid: {STEER_RIGHT_GUID}, type: 2")
    h = h.replace(f"guid: {SWIPE_LEFT_GUID}, type: 2", f"guid: {STEER_LEFT_GUID}, type: 2")
    set_doc(docs, handler_id, h)

    # 6. the HUD controller keeps its Time card (Momentum); its sword references go
    hud = get_doc(docs, HUD_CONTROLLER_ID)
    hud = re.sub(r"  swordDriver: \{fileID: -?\d+\}\n", "  swordDriver: {fileID: 0}\n", hud)
    hud = re.sub(r"  afterimage: \{fileID: -?\d+\}\n", "  afterimage: {fileID: 0}\n", hud)
    set_doc(docs, HUD_CONTROLLER_ID, hud)

    # 7. the plough blade (Mass): one hull collider across the nose
    blade_script = guid_of(BLADE_META)
    docs.append([PLOUGH_BLADE_GO,
        f"--- !u!1 &{PLOUGH_BLADE_GO}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n"
        f"  - component: {{fileID: {PLOUGH_BLADE_TRANSFORM}}}\n  - component: {{fileID: {PLOUGH_BLADE_BOX}}}\n"
        f"  - component: {{fileID: {PLOUGH_BLADE_IMPACT}}}\n  - component: {{fileID: {PLOUGH_BLADE_COMPONENT}}}\n"
        "  m_Layer: 8\n  m_Name: PloughBlade\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"])
    docs.append([PLOUGH_BLADE_TRANSFORM,
        f"--- !u!4 &{PLOUGH_BLADE_TRANSFORM}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {PLOUGH_BLADE_GO}}}\n"
        "  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: -1.2, z: 7.5}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n"
        f"  m_Father: {{fileID: {ROOT_TRANSFORM_ID}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"])
    docs.append([PLOUGH_BLADE_BOX,
        f"--- !u!65 &{PLOUGH_BLADE_BOX}\nBoxCollider:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {PLOUGH_BLADE_GO}}}\n"
        "  m_Material: {fileID: 0}\n  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_ExcludeLayers:\n"
        "    serializedVersion: 2\n    m_Bits: 0\n  m_LayerOverridePriority: 0\n  m_IsTrigger: 1\n  m_ProvidesContacts: 0\n"
        "  m_Enabled: 1\n  serializedVersion: 3\n  m_Size: {x: 14, y: 10, z: 2}\n  m_Center: {x: 0, y: 0, z: 0}\n"])
    docs.append([PLOUGH_BLADE_IMPACT,
        f"--- !u!114 &{PLOUGH_BLADE_IMPACT}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {PLOUGH_BLADE_GO}}}\n"
        f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {IMPACT_COLLIDER_SCRIPT}, type: 3}}\n"
        f"  m_Name: \n  m_EditorClassIdentifier: \n  impactorObject: {{fileID: {VESSEL_IMPACTOR_ID}}}\n"])
    docs.append([PLOUGH_BLADE_COMPONENT,
        f"--- !u!114 &{PLOUGH_BLADE_COMPONENT}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {PLOUGH_BLADE_GO}}}\n"
        f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {blade_script}, type: 3}}\n"
        "  m_Name: \n  m_EditorClassIdentifier: \n  width:\n    Enabled: 1\n    Value: 14\n    Min: 14\n    Max: 40\n"
        "    element: 2\n    UseFloor: 1\n    Floor: 10\n"])
    add_child(docs, ROOT_TRANSFORM_ID, PLOUGH_BLADE_TRANSFORM)

    out = join_docs(head, docs)
    bad = dangling_refs(out)
    if bad:
        raise RuntimeError(f"the strip left dangling references: {bad[:6]}")
    return out


# ----------------------------------------------------------------------------- the small assets
def steer_asset(name: str, side: int) -> str:
    return mono_asset(guid_of(STEER_SO_META), name,
                      f"  side: {side}\n  shoveSpeed: 90\n  shoveSeconds: 0.25\n")


def rhino_skimmer_container() -> str:
    return mono_asset(SKIMMER_CONTAINER_SCRIPT, "RhinoSkimmerImpactorDataContainer",
                      "  vesselSkimmerEffectsSO:\n"
                      f"  - {{fileID: 11400000, guid: {VESSEL_HAPTICS_BY_SKIMMER}, type: 2}}\n"
                      "  skimmerPrismEffectsSO:\n"
                      f"  - {{fileID: 11400000, guid: {SKIMMER_HAPTICS_BY_PRISM}, type: 2}}\n"
                      "  skimmerCrystalEffectsSO: []\n")


def swordfish_vessel_container(rhino_container: str) -> str:
    t = replace_once(rhino_container, "  m_Name: RhinoImpactorDataContainer\n",
                     "  m_Name: SwordfishImpactorDataContainer\n", "container name")
    return replace_once(t, f"guid: {PLOUGH_EFFECT_GUID}, type: 2", f"guid: {RAM_EFFECT_GUID}, type: 2", "the plough")


SWORDFISH_TIME = """  - Element: 4
    AbilityLabel: 'Ramp Spool'
    AbilityDescription: 'Time raises how fast the ramp boost winds the Swordfish up to its top speed: 220 u/s per second at rest, 550 at level 10, 110 at the -5 floor (RampBoostActionSO.timeAccelerationMultiplier, x1 -> x2.5, floored x0.5, read live). It does not raise the ceiling - cruise 50 x maxBoostMultiplier 16.8 = 840 u/s - only how long the run-up takes. Its hull carries no prism slow, so ramming costs it no speed; unlike the Rhino it does not plough - the hull breaks what it rams (the fleet ram, VesselDamagePrismEffect) and the sword cuts ahead of it.'
    Input: 0
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: 'Momentum'
    UpgradeDescription: 'Above 70% of top speed (588 u/s) danger prisms cannot drain the Swordfish''s elements - the elemental-debuff ward scoped to DangerPrism alone, so an opposing pilot''s blast still lands (VesselElementalImmunity on the root, condition WhileAboveTopSpeedFraction, upgradeGate Time). The Time card lights while the ward is holding.'
"""

# The plough kit as it stands since the 2026-11-12 rework (RHINO_PLOUGH.md): only written when the map still
# carries the sword (the one-shot split), so the rework is what a re-split produces.
RHINO_PLOUGH_ENTRIES = """  - Element: 1
    AbilityLabel: 'Gore'
    AbilityDescription: 'Ram a rival vessel and its petals come loose: a gore is a scored Strike (8 points, Swordfish sword price) decided on the Rhino''s owner, landed only while the Rhino is moving faster than its victim, once per 1.4 s per victim (VesselCombatHitByGore). Charge lowers the least ram speed a gore needs, like the Squirrel joust: 150 u/s at rest (three times the 50 u/s cruise, a short ramp wind-up) down to 50 u/s at level 10 (a cruising Rhino gores), read at the replicated level (RhinoGoreSpeed); a slower contact is neither scored nor robbed. Charge also carries a thrown prism on to strike the next one in its line: 0 u at rest, 40 u at level 10 (RhinoPloughPrismEffect.carryDistance); a thrown prism that strikes another destroys both.'
    Input: 0
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: 'Gore Steal'
    UpgradeDescription: 'The petals a gore knocks loose land on the Rhino instead of floating free as crystals (RhinoGoreBySkimmerEffectSO.stealUpgradeElement, gated on IsUpgradeActive(Charge); 2026-11-12, replacing Avalanche).'
  - Element: 2
    AbilityLabel: 'Heft'
    AbilityDescription: 'A heavier plough throws further: Mass adds 0 u at rest to 60 u at level 10 to every throw, at any speed, on top of the speed-driven throw (speed x 0.12 s, at most 90 u) (RhinoPloughPrismEffect.furrowDistance, replicated level). Pull a trigger for a shove that way; HOLD it and everything the hull ploughs is thrown to that side, across the centreline (RhinoPloughSteerAction: LT left, RT right, the last one pulled wins). Let go and each prism goes to the side it already sat on.'
    Input: 1
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: 'Berm'
    UpgradeDescription: 'A ploughed prism LANDS SHIELDED, so the furrow banks into walls of armour; a prism that strikes another still breaks (RhinoPloughPrismEffectSO.bermUpgradeElement, gated on IsUpgradeActive(Mass); Garrett, 2026-11-07).'
  - Element: 3
    AbilityLabel: 'Blade'
    AbilityDescription: 'A plough blade across the nose: one hull collider whose width follows Space - 14 u at rest (a little wider than the hull), 40 u at level 10, floored at 10, read at the replicated level (RhinoPloughBlade.width). A wider blade reaches more mass, and the plough throws what it catches clear of the whole blade.'
    Input: 0
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: 'Unstoppable'
    UpgradeDescription: 'The plough moves SUPER-shielded mass too; without it super-shielded prisms are the immovable tier and only jiggle (RhinoPloughPrismEffectSO.unstoppableUpgradeElement, gated on IsUpgradeActive(Space); Garrett, 2026-11-07). Arena linings and race tracks are super-shielded, so an upgraded Rhino can plough them out of line.'
"""


def entry_span(text: str, element: int):
    m = re.search(rf"  - Element: {element}\n.*?(?=\n  - Element:|\n  omniAbilityLabel:|\Z)", text, re.S)
    if not m:
        raise RuntimeError(f"no element {element} entry")
    return m


def swordfish_map(rhino_map: str) -> str:
    t = replace_once(rhino_map, "  m_Name: Rhino\n", "  m_Name: Swordfish\n", "map name")
    t = replace_once(t, f"  vesselClass: {RHINO_CLASS_ID}\n", f"  vesselClass: {CLASS_ID}\n", "map class")
    m = entry_span(t, 4)
    t = t[:m.start()] + SWORDFISH_TIME.rstrip("\n") + t[m.end():]
    head, tail = t.split("  - Element: 4\n", 1)
    head = (head.replace("(the Rhino has no energy meter since 2026-10-26)", "(the sword has had no energy meter since 2026-10-26)")
                .replace("the Rhino''s own blade", "the Swordfish''s own blade")
                .replace("they land on the Rhino", "they land on the Swordfish")
                .replace("the Rhino''s ForceFieldSkimmer", "the Swordfish''s ForceFieldSkimmer")
                .replace("from the pilot's blade", "from the pilot''s blade"))   # an unescaped quote on the Rhino's map
    return head + "  - Element: 4\n" + tail


def rhino_plough_map(rhino_map: str) -> str:
    start = entry_span(rhino_map, 1).start()
    end = entry_span(rhino_map, 3).end()
    t = rhino_map[:start] + RHINO_PLOUGH_ENTRIES.rstrip("\n") + rhino_map[end:]
    return re.sub(r"(?m)^  omniAbilityLabel: .*$", "  omniAbilityLabel: Stampede", t)


def class_asset(rhino_class: str) -> str:
    t = replace_once(rhino_class, "  m_Name: SO_Class_Rhino\n", "  m_Name: SO_Class_Swordfish\n", "class name")
    t = replace_once(t, f"  Class: {RHINO_CLASS_ID}\n", f"  Class: {CLASS_ID}\n", "class id")
    return re.sub(r"  Name: Rhino\n  Description: .*?(?=\n  PrimaryElement:)",
                  "  Name: Swordfish\n  Description: \"All sword. Your triggers swing a blade that cuts every prism "
                  "it touches; hold both to light it, and a lit blade lays afterimages of itself. Run flat out and "
                  "the ramp winds you up to the fastest top speed in the fleet.\"", t, count=1, flags=re.S)


def add_list_entry(text: str, list_key: str, entry: str) -> str:
    if entry in text:
        return text
    m = re.search(rf"^  {list_key}:\n((?:  - .*\n(?:    .*\n)*)*)", text, re.M)
    if not m:
        raise RuntimeError(f"no list {list_key}")
    return text[:m.end()] + entry + text[m.end():]


def list_key_of(text: str) -> str:
    m = re.search(r"^  (\w+):\n  - \{fileID: -?\d+, guid: [0-9a-f]{32}, type: [23]\}", text, re.M)
    if not m:
        raise RuntimeError("no guid list")
    return m.group(1)


def network_entry() -> str:
    return (f"  - Override: 0\n    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n"
            "    SourcePrefabToOverride: {fileID: 0}\n    SourceHashToOverride: 0\n    OverridingTargetPrefab: {fileID: 0}\n")


def fusion_entries(config: str) -> str:
    rhino = re.findall(rf"  - vessel: {RHINO_CLASS_ID}\n(?:    .*\n)+", config)
    if len(rhino) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Rhino entries, found {len(rhino)}")
    return "".join(e.replace(f"  - vessel: {RHINO_CLASS_ID}\n", f"  - vessel: {CLASS_ID}\n", 1) for e in rhino)


# ----------------------------------------------------------------------------- author + check
def author() -> None:
    rhino = read(RHINO)
    if not os.path.exists(PREFAB) and has_sword(rhino):
        write(PREFAB, clone_prefab(rhino))
        write(PREFAB + ".meta", prefab_meta(PREFAB_GUID))
    if not os.path.exists(SWORDFISH_VESSEL_CONTAINER):
        write(SWORDFISH_VESSEL_CONTAINER, swordfish_vessel_container(read(RHINO_VESSEL_CONTAINER)))
        write(SWORDFISH_VESSEL_CONTAINER + ".meta", asset_meta(SWORDFISH_VESSEL_CONTAINER_GUID))
    if not os.path.exists(RHINO_SKIMMER_CONTAINER):
        write(RHINO_SKIMMER_CONTAINER, rhino_skimmer_container())
        write(RHINO_SKIMMER_CONTAINER + ".meta", asset_meta(RHINO_SKIMMER_CONTAINER_GUID))
    for path, guid, name, side in ((STEER_RIGHT, STEER_RIGHT_GUID, "RhinoPloughSteerRightAction", 0),
                                   (STEER_LEFT, STEER_LEFT_GUID, "RhinoPloughSteerLeftAction", 1)):
        if not os.path.exists(path):
            write(path, steer_asset(name, side))
            write(path + ".meta", asset_meta(guid))
    if not os.path.exists(MAP):
        write(MAP, swordfish_map(read(RHINO_MAP)))
        write(MAP + ".meta", asset_meta(MAP_GUID))
    rmap = read(RHINO_MAP)
    if "AbilityLabel: 'Energize'" in rmap:
        write(RHINO_MAP, rhino_plough_map(rmap))
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset(read(RHINO_CLASS)))
        write(CLASS_ASSET + ".meta", asset_meta(CLASS_GUID))
    # the Rhino's prefab: strip the sword only after the Swordfish carries it away
    if has_sword(rhino) and os.path.exists(PREFAB):
        write(RHINO, strip_rhino(rhino))
    rc = read(RHINO_VESSEL_CONTAINER)
    surge_line = f"  - {{fileID: 11400000, guid: {SURGE_EFFECT_GUID}, type: 2}}\n"
    if surge_line in rc:
        write(RHINO_VESSEL_CONTAINER, rc.replace(surge_line, "", 1))
    for path in CLASS_LISTS:
        t = read(path)
        n = add_list_entry(t, list_key_of(t), f"  - {{fileID: 11400000, guid: {CLASS_GUID}, type: 2}}\n")
        if n != t: write(path, n)
    t = read(CONTAINER)
    n = add_list_entry(t, list_key_of(t), f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n")
    if n != t: write(CONTAINER, n)
    t = read(NETWORK_PREFABS)
    if f"guid: {PREFAB_GUID}" not in t:
        m = list(re.finditer(r"  - Override: \d+\n(?:    .*\n)+", t))
        if not m:
            raise RuntimeError("DefaultNetworkPrefabs: no entries")
        write(NETWORK_PREFABS, t[:m[-1].end()] + network_entry() + t[m[-1].end():])
    t = read(FUSION)
    if f"  - vessel: {CLASS_ID}\n" not in t:
        write(FUSION, t.rstrip("\n") + "\n" + fusion_entries(t))


def errors() -> list:
    out = []

    def need(cond, what):
        if not cond:
            out.append(what)

    for p in (PREFAB, MAP, CLASS_ASSET, SWORDFISH_VESSEL_CONTAINER, RHINO_SKIMMER_CONTAINER, STEER_RIGHT, STEER_LEFT):
        need(os.path.exists(p) and os.path.exists(p + ".meta"), f"missing {os.path.relpath(p, ROOT)} (+ .meta)")
    if out:
        return out

    # ---- the Swordfish carries the sword
    sf = read(PREFAB)
    need(guid_of(PREFAB + ".meta") == PREFAB_GUID, "Swordfish.prefab.meta guid moved")
    need(f"  _name: Swordfish\n  vesselType: {CLASS_ID}\n" in sf, "VesselStatus is not the Swordfish (30)")
    need(f"  GlobalObjectIdHash: {global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n" in sf,
         "GlobalObjectIdHash is not this prefab's (a donor hash keys two prefabs on one Netcode entry)")
    need(has_sword(sf), "the Swordfish has no sword swing executor")
    for name, meta in SWORD_SCRIPTS.items():
        need(f"guid: {script_guid(meta)}, type: 3" in sf, f"the Swordfish lost its {name}")
    need(f"guid: {SWIPE_RIGHT_GUID}" in sf and f"guid: {SWIPE_LEFT_GUID}" in sf, "the Swordfish's triggers are not the swings")
    need(f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {SWORDFISH_VESSEL_CONTAINER_GUID}," in sf,
         "the Swordfish does not use its own vessel container")
    need(not dangling_refs(sf), "Swordfish.prefab has dangling local references")
    sc = read(SWORDFISH_VESSEL_CONTAINER)
    need(f"guid: {RAM_EFFECT_GUID}" in sc and f"guid: {PLOUGH_EFFECT_GUID}" not in sc,
         "the Swordfish's hull ploughs (it rams; the plough is the Rhino's)")
    need(f"guid: {SURGE_EFFECT_GUID}" in sc, "the Swordfish's omni-crystal surge (the sword ring) is not in its container")

    # ---- the Rhino carries the plough and nothing of the sword
    rh = read(RHINO)
    need(not has_sword(rh), "the Rhino still carries the sword swing executor")
    for name, meta in SWORD_SCRIPTS.items():
        need(f"guid: {script_guid(meta)}, type: 3" not in rh, f"the Rhino still carries {name}")
    need(f"guid: {SWIPE_RIGHT_GUID}" not in rh and f"guid: {SWIPE_LEFT_GUID}" not in rh, "a sword swing is still bound on the Rhino")
    need(rh.count(f"guid: {STEER_RIGHT_GUID}, type: 2") == 2 and rh.count(f"guid: {STEER_LEFT_GUID}, type: 2") == 2,
         "LT / RT do not steer the plough on both the pad and touch")
    need(f"guid: {guid_of(STEER_EXEC_META)}, type: 3" in rh, "the Rhino has no plough steer executor")
    need(f"guid: {guid_of(BLADE_META)}, type: 3" in rh, "the Rhino has no plough blade (Space)")
    need(f"  _nearFieldSkimmer: {{fileID: {NEAR_SKIMMER_ID}}}\n" in rh, "the Rhino lost its near-field skimmer (no crystals)")
    need(f"guid: {RHINO_SKIMMER_CONTAINER_GUID}," in rh and f"guid: {SWORD_SKIMMER_CONTAINER_GUID}," not in rh,
         "the Rhino's skimmer still cuts like the sword")
    need(not dangling_refs(rh), "Rhino.prefab has dangling local references")
    rk = read(RHINO_SKIMMER_CONTAINER)
    need(f"guid: {SKIMMER_CONTAINER_SCRIPT}" in rk and "Damage" not in rk, "the Rhino's skimmer container damages prisms like the sword")
    rc = read(RHINO_VESSEL_CONTAINER)
    need(f"guid: {PLOUGH_EFFECT_GUID}" in rc, "the Rhino's hull does not plough")
    need(f"guid: {SURGE_EFFECT_GUID}" not in rc, "the Rhino still has the sword's surge")

    # ---- maps, class, registrations
    mp = read(MAP)
    need(f"  vesselClass: {CLASS_ID}\n" in mp and "AbilityLabel: 'Energize'" in mp and "UpgradeLabel: 'Shield Breaker'" in mp,
         "the Swordfish map is not the sword kit on class 30")
    rm = read(RHINO_MAP)
    for label in ("'Gore'", "'Gore Steal'", "'Heft'", "'Berm'", "'Blade'", "'Unstoppable'", "'Ramp Spool'", "'Momentum'"):
        need(label in rm, f"the Rhino's map has no {label}")
    need("'Energize'" not in rm and "'Afterimage'" not in rm and "'Blade Reach'" not in rm, "a sword card is on the Rhino's map")
    need(f"  Class: {CLASS_ID}\n" in read(CLASS_ASSET), "SO_Class_Swordfish is not class 30")
    for path in CLASS_LISTS:
        need(f"guid: {CLASS_GUID}" in read(path), f"{os.path.basename(path)} does not list SO_Class_Swordfish")
    need(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in read(CONTAINER),
         "the Vessel Prefab Container does not list the Swordfish's root transform")
    need(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in read(NETWORK_PREFABS),
         "DefaultNetworkPrefabs does not list the Swordfish's root GameObject")
    need(read(FUSION).count(f"  - vessel: {CLASS_ID}\n") == 4, "the Swordfish does not fuse all four elements")
    for path, side in ((STEER_RIGHT, 0), (STEER_LEFT, 1)):
        a = read(path)
        need(f"guid: {guid_of(STEER_SO_META)}" in a and f"  side: {side}\n" in a,
             f"{os.path.basename(path)} is not a {'right' if side == 0 else 'left'} plough steer")
    return out


def write_quiet(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def self_test() -> int:
    """Each planted defect must be caught by the check that guards it."""
    sf, rh = read(PREFAB), read(RHINO)
    cases = [
        ("donor hash", PREFAB, sf, re.sub(r"GlobalObjectIdHash: \d+", "GlobalObjectIdHash: 234294682", sf, count=1)),
        ("swordfish class", PREFAB, sf, sf.replace(f"vesselType: {CLASS_ID}\n", "vesselType: 3\n", 1)),
        ("swordfish ploughs", SWORDFISH_VESSEL_CONTAINER, read(SWORDFISH_VESSEL_CONTAINER),
         read(SWORDFISH_VESSEL_CONTAINER).replace(RAM_EFFECT_GUID, PLOUGH_EFFECT_GUID)),
        ("rhino swing bound", RHINO, rh, rh.replace(f"guid: {STEER_RIGHT_GUID}, type: 2", f"guid: {SWIPE_RIGHT_GUID}, type: 2", 1)),
        ("rhino dangling", RHINO, rh, rh.replace(f"impactorObject: {{fileID: {VESSEL_IMPACTOR_ID}}}\n",
                                                 "impactorObject: {fileID: 123}\n", 1)),
        ("rhino skimmer cuts", RHINO, rh, rh.replace(RHINO_SKIMMER_CONTAINER_GUID, SWORD_SKIMMER_CONTAINER_GUID, 1)),
        ("rhino surge back", RHINO_VESSEL_CONTAINER, read(RHINO_VESSEL_CONTAINER),
         read(RHINO_VESSEL_CONTAINER).replace("  vesselCrystalEffects:\n",
                                              f"  vesselCrystalEffects:\n  - {{fileID: 11400000, guid: {SURGE_EFFECT_GUID}, type: 2}}\n", 1)),
        ("rhino map sword", RHINO_MAP, read(RHINO_MAP), read(RHINO_MAP).replace("'Furrow'", "'Blade Reach'", 1)),
    ]
    failed = 0
    for name, path, good, bad in cases:
        if bad == good:
            print(f"  SELF-TEST FAILED: '{name}' could not be planted"); failed += 1; continue
        try:
            write_quiet(path, bad)
            if not errors():
                print(f"  SELF-TEST FAILED: '{name}' was not caught"); failed += 1
            else:
                print(f"  caught: {name}")
        finally:
            write_quiet(path, good)
    return failed


def main() -> int:
    if "--self-test" in sys.argv:
        bad = self_test()
        print("self-test " + ("FAILED" if bad else "passed"))
        return 1 if bad else 0
    if "--check" not in sys.argv:
        author()
    errs = errors()
    for e in errs:
        print(f"  FAIL {e}")
    print("swordfish / rhino split: " + ("FAIL" if errs else "ok"))
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main())
