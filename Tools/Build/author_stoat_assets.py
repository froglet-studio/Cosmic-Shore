#!/usr/bin/env python3
"""Author the Stoat's prefab from the Squirrel's and register it, then prove the shipped set.

    python3 Tools/Build/author_stoat_assets.py             # author what is missing, then check
    python3 Tools/Build/author_stoat_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_stoat_assets.py --self-test # prove every check fires

WHAT IT MAKES (`R_VesselActions/STOAT.md`)
    Assets/_Prefabs/Spacevessels/Stoat.prefab (+ .meta)   a text clone of Squirrel.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 14` (VesselClassType.Stoat)
        - a FRESH Netcode GlobalObjectIdHash, computed the way NetworkObject.OnValidate does
          (XXHash32 of the GlobalObjectId string) so the editor regenerates the same number, and
          InScenePlacedSourceGlobalObjectIdHash cleared - a disk copy keeps the donor's hash and
          Netcode then keys two prefabs on one entry (Tools/Build/check_network_prefab_hashes.py)
        - the gamepad bindings replaced: LT (InputEvents 2) -> StoatSlingLeftAction,
          RT (1) -> StoatSlingRightAction, X (6) -> StoatHoldAction; touch overrides cleared
          (no touch design yet - the Gibbon's call, recorded in STOAT.md)
        - StoatSlingExecutor + ToggleTranslationModeActionExecutor added on the ShipActions
          object and in the ActionExecutorRegistry, the stop executor wired to the hull's own
          VesselPrismController and the fleet's shared stationaryModeChanged / OnMiniGameTurnEnd
          channels (the same guids the Sparrow carries, which the shared-channel gate checks)
    STAGE 2 (idempotent, runs on the committed prefab): the PROCEDURAL HULL - a StoatHull
    GameObject under the root (Transform, MeshFilter, MeshRenderer with the body + domain
    materials, StoatHullBuilder hiding the Squirrel model's renderers), VesselCustomization's
    painted geometry moved onto it, and the root's VesselAnimation switched to StoatAnimation on
    the same fileID (STOAT.md section 2).
    STAGE 3: SO_Class_Stoat (the SO_Vessel a card's Vessels list names a hull by; owned from the
    start, the Squirrel's icons as placeholders) and its entry in SO_Classlist_All / _Classes.
    and one entry each in `Assets/_SO_Assets/Vessel Prefab Container.asset` and
    `Assets/DefaultNetworkPrefabs.asset`, the two lists that make a hull spawnable.
    STAGE 4 (idempotent, runs on the committed prefab): the round-15 FIELD DIPOLE and PATHFINDER
    (`R_VesselActions/STOAT_DIPOLE.md`) - LT/RT rebound to StoatDipoleLeft/RightAction (X keeps the
    hold), StoatDipoleExecutor + StoatPathfinderExecutor on the ShipActions object and in the
    registry, both wired to StoatDipoleConfig, the base turn rates x0.4 (the studio's ftTurnScale:
    the hull leans on its poles to turn), and the dipole's config / action assets and every new
    script's .meta (deterministic guids). StoatSlingExecutor stays on the prefab, unbound and inert,
    so rebinding the round-4 sling assets restores it.

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md, "a spent one-shot must
stand down, not abort"). Squirrel.prefab moves on; re-cloning it would silently re-author the
Stoat from a donor the Stoat has since diverged from. So the clone runs ONLY while Stoat.prefab
is absent, and `--check` validates the SHIPPED prefab's invariants (the list above) rather than
diffing it against a fresh clone. To re-author from scratch, delete Stoat.prefab and its .meta
and run again.
"""
import os
import re
import struct
import sys
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Squirrel.prefab")
PREFAB = os.path.join(VESSELS, "Stoat.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
ACTIONS = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "Stoat")
MAP = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps", "Stoat.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Stoat.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
CLASS_GUID = "cc163188820f4e7a985561bc609da2e0"
SO_VESSEL_SCRIPT = "0309a5343d820c14abd73b35f6fa50f1"
# Placeholder art: the Squirrel's class icons (the hull it was cloned from) until the Stoat's land.
SQUIRREL_ICON_ACTIVE = "5d8f022f56d65ef4fae2cea4428a990f"
SQUIRREL_ICON_INACTIVE = "c7a15d77edb2723429254cf708d9d20c"

STOAT_CLASS_ID = 14
PREFAB_GUID = "599b396ff054448db5bccc9307dfb4c9"
ROOT_GO = "6417075533431866457"              # the hull's root GameObject (same in every Squirrel-derived prefab)
NETWORK_OBJECT_ID = "1733035045152327888"    # the NetworkObject component on it
SHIP_ACTIONS_GO = "7850473780452578400"
REGISTRY_ID = "9105512684531825410"
SLING_EXECUTOR_ID = "5137264980012345601"
STOP_EXECUTOR_ID = "5137264980012345602"
ROOT_TRANSFORM_ID = "5928382658856804067"
# The procedural hull (stage 2): a StoatHull GameObject under the root, renderer-only like the
# Scarab's ScarabHull, painted by VesselCustomization in place of the Squirrel mesh it hides.
HULL_GO_ID = "5137264980012345610"
HULL_TRANSFORM_ID = "5137264980012345611"
HULL_FILTER_ID = "5137264980012345612"
HULL_RENDERER_ID = "5137264980012345613"
HULL_BUILDER_ID = "5137264980012345614"
SHIPS_LAYER = 8
SQUIRREL_MESH_GO = "3168954648161338247"         # the Squirrel FBX mesh VesselCustomization painted
SQUIRREL_MODEL_ROOT = "8301242790861813107"      # the FBX instance's root Transform (stripped)
ANIMATION_ID = "5500781737155866732"             # the root's VesselAnimation component
MANTA_ANIMATION_SCRIPT = "2116872444dccfd43a29513c3af008b7"  # what the Squirrel clone carried

# Script guids (from the .cs.meta files).
SLING_EXECUTOR_SCRIPT = "cf82f131a8df416cb205ab8055108b82"
STOP_EXECUTOR_SCRIPT = "8c3f54abda8145ecabf7c85253cab9ed"    # ToggleTranslationModeActionExecutor
PRISM_CONTROLLER_SCRIPT = "909fb5cbbca8c4549b5a9df56d837e55"  # VesselPrismController
HULL_BUILDER_SCRIPT = "1c6aec6ea8ef44f1bef4884679d33d0a"   # StoatHullBuilder
STOAT_ANIMATION_SCRIPT = "f8734be12714484eb93134990801178a"  # StoatAnimation
BODY_MATERIAL = "54e78ce17120fe641904833c11bc1210"   # BlueBaseVesselMaterial  (slot 0, the plates)
ACCENT_MATERIAL = "539a8c65974bf0b48aae77d83884c13b" # GreenAccentVesselMaterial (slot 1, repainted to the domain)
# Asset guids (from the .asset.meta files).
SLING_CONFIG = "e79a09360967428cb727002bd123383a"
SLING_LEFT = "5c382e0def5b45d7b1230d75e8be082b"
SLING_RIGHT = "012d65baf31c49c496d38975eba8c0be"
HOLD = "9b50ea11259b4b1aa750eea7f1d7f781"
STATIONARY_MODE_CHANGED = "0b48e834efdbe654ca3c7df60370ea3f"
ON_MINIGAME_TURN_END = "498a06d44bde9184f985c938c803b2a1"

# Stage 4: the round-15 field dipole + pathfinder (R_VesselActions/STOAT_DIPOLE.md).
DIPOLE_EXECUTOR_ID = "5137264980012345603"
PATHFINDER_EXECUTOR_ID = "5137264980012345604"
TRANSFORMER_ID = "7686683633640807837"       # the hull's VesselTransformer
# The studio's ftTurnScale 0.4 on the shipped rates: the hull turns less on its own and more on its poles.
TURN_RATES = (("PitchScaler", "120", "48"), ("YawScaler", "120", "48"), ("RollScaler", "130", "52"))
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")
VESSEL_ACTIONS_SRC = os.path.join(SCRIPTS, "Controller", "Vessel", "R_VesselActions")
DIPOLE_SCRIPTS = (   # (path, guid): every script the dipole added, its .meta authored here
    (os.path.join(VESSEL_ACTIONS_SRC, "Executors", "StoatDipoleExecutor.cs"), "6eee000b02734120a5138fb645997915"),
    (os.path.join(VESSEL_ACTIONS_SRC, "Executors", "StoatPathfinderExecutor.cs"), "ec03f8093b534dfe98ec25e13f18c576"),
    (os.path.join(VESSEL_ACTIONS_SRC, "Data Containers", "StoatDipoleConfigSO.cs"), "84eb4c6b05a348e193602462d620198f"),
    (os.path.join(VESSEL_ACTIONS_SRC, "Data Containers", "StoatDipoleActionSO.cs"), "46100aa2bb8e43e8ae09952119374c52"),
    (os.path.join(VESSEL_ACTIONS_SRC, "StoatPathfinderDots.cs"), "f8c8092f5de744138b2c2782cdb62136"),
    (os.path.join(VESSEL_ACTIONS_SRC, "StoatDipoleMath.cs"), "980f20f428aa4a30bf2bfd6ec5cec481"),
    (os.path.join(SCRIPTS, "Controller", "Environment", "BlackHole", "BlackHoleCrystalStrip.cs"), "f3deff9bd0464aab8296b8cc73d51678"),
    (os.path.join(SCRIPTS, "Tests", "Editor", "StoatDipoleTests.cs"), "4eef6292b78740038ccfc2d2507bc3ba"),
)
DIPOLE_DOC = os.path.join(VESSEL_ACTIONS_SRC, "STOAT_DIPOLE.md")
DIPOLE_DOC_GUID = "6623b604f5b54f83be76d9e2c44677cc"
DIPOLE_EXECUTOR_SCRIPT = DIPOLE_SCRIPTS[0][1]
PATHFINDER_EXECUTOR_SCRIPT = DIPOLE_SCRIPTS[1][1]
DIPOLE_CONFIG_SCRIPT = DIPOLE_SCRIPTS[2][1]
DIPOLE_ACTION_SCRIPT = DIPOLE_SCRIPTS[3][1]
DIPOLE_CONFIG = "c6ae04d75c934a21aae1b3607d52cb84"
# Stage 5: the studio's chase camera (StoatFlightStudio.html cameraStep: 6.5 up, 21 behind, looking 40 past the
# nose and 3 up). Before it the Stoat flew on the Squirrel's camera asset: flat, 17 straight behind.
CAMERA_DIR = os.path.join(ROOT, "Assets", "_SO_Assets", "Camera")
CAMERA_ASSET = os.path.join(CAMERA_DIR, "StoatCameraSettingsSO.asset")
CAMERA_GUID = "e17964483cec46e09bccb69a53eee1d0"
CAMERA_SCRIPT = "fd4e6f3597f7439ba7333b35a3a9e164"
SQUIRREL_CAMERA_GUID = "3c3fc507fffc4f5e86ab043ee7543cd2"
DIPOLE_LEFT = "9eacf723b796418caecfe237336b9985"
DIPOLE_RIGHT = "62ce60a32bef4c70b42fd1f7cd9896ff"


# ----------------------------------------------------------------------------- Netcode's hash
def xxhash32(data: bytes, seed: int = 0) -> int:
    """XXHash32 - what Netcode's `string.Hash32()` computes over the UTF-8 GlobalObjectId."""
    p1, p2, p3, p4, p5 = 2654435761, 2246822519, 3266489917, 668265263, 374761393
    m = 0xFFFFFFFF
    rotl = lambda x, r: ((x << r) | (x >> (32 - r))) & m
    n, i = len(data), 0
    if n >= 16:
        v = [(seed + p1 + p2) & m, (seed + p2) & m, seed & m, (seed - p1) & m]
        while i <= n - 16:
            for k in range(4):
                lane = struct.unpack_from("<I", data, i)[0]
                i += 4
                v[k] = (rotl((v[k] + lane * p2) & m, 13) * p1) & m
        h = (rotl(v[0], 1) + rotl(v[1], 7) + rotl(v[2], 12) + rotl(v[3], 18)) & m
    else:
        h = (seed + p5) & m
    h = (h + n) & m
    while i <= n - 4:
        h = (rotl((h + struct.unpack_from("<I", data, i)[0] * p3) & m, 17) * p4) & m
        i += 4
    while i < n:
        h = (rotl((h + data[i] * p5) & m, 11) * p1) & m
        i += 1
    h ^= h >> 15
    h = (h * p2) & m
    h ^= h >> 13
    h = (h * p3) & m
    h ^= h >> 16
    return h


def global_object_id_hash(prefab_guid: str, component_file_id: str) -> int:
    """NetworkObject.OnValidate: GlobalObjectId.GetGlobalObjectIdSlow(this).ToString().Hash32()
    - identifier type 1 (imported asset), the component's local fileID, prefab id 0."""
    return xxhash32(f"GlobalObjectId_V1-1-{prefab_guid}-{component_file_id}-0".encode("utf-8"))


# ----------------------------------------------------------------------------- the clone
def replace_once(text: str, old: str, new: str, what: str) -> str:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"donor drifted: expected exactly one '{what}', found {count}")
    return text.replace(old, new, 1)


def find_component_id(text: str, script_guid: str) -> str:
    """The fileID of the first MonoBehaviour document whose m_Script is `script_guid`."""
    for m in re.finditer(r"--- !u!114 &(-?\d+)\n(.*?)(?=\n--- !u!|\Z)", text, re.S):
        if f"m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}" in m.group(2):
            return m.group(1)
    raise RuntimeError(f"donor drifted: no component with script {script_guid}")


def executor_blocks(prism_controller_id: str) -> str:
    return (
        f"--- !u!114 &{SLING_EXECUTOR_ID}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {SHIP_ACTIONS_GO}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SLING_EXECUTOR_SCRIPT}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        f"  config: {{fileID: 11400000, guid: {SLING_CONFIG}, type: 2}}\n"
        f"--- !u!114 &{STOP_EXECUTOR_ID}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {SHIP_ACTIONS_GO}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {STOP_EXECUTOR_SCRIPT}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        f"  vesselPrismController: {{fileID: {prism_controller_id}}}\n"
        "  seedAssemblerExecutor: {fileID: 0}\n"
        "  stationarySeedConfig: {fileID: 0}\n"
        f"  stationaryModeChanged: {{fileID: 11400000, guid: {STATIONARY_MODE_CHANGED}, type: 2}}\n"
        f"  OnMiniGameTurnEnd: {{fileID: 11400000, guid: {ON_MINIGAME_TURN_END}, type: 2}}\n"
    )


def bindings() -> str:
    """The SHIPPED bindings (stage 4): LT/RT the dipole's two triggers, X the hold."""
    return (
        "  _touchActionOverrides: []\n"
        "  _gamepadActionOverrides:\n"
        "  - InputEvent: 2\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {DIPOLE_LEFT}, type: 2}}\n"
        "  - InputEvent: 1\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {DIPOLE_RIGHT}, type: 2}}\n"
        "  - InputEvent: 6\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {HOLD}, type: 2}}\n"
    )


def build_clone(donor: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Squirrel\n", "  m_Name: Stoat\n", "root GameObject name")
    text = replace_once(text, "  _name: Squirrel\n", "  _name: Stoat\n", "VesselStatus _name")
    text = replace_once(text, "  vesselType: 6\n", f"  vesselType: {STOAT_CLASS_ID}\n", "vesselType")

    # Netcode identity. The donor's hash is whatever it is; what matters is that ours is the
    # number the editor would write for THIS guid, so OnValidate changes nothing.
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)

    # The bindings: everything from _touchActionOverrides to the line before _onButtonPressed.
    m = re.search(r"  _touchActionOverrides:\n(?:.*\n)*?(?=  _onButtonPressed:)", text)
    if not m:
        raise RuntimeError("donor drifted: R_VesselActionHandler binding block not found")
    text = text[:m.start()] + bindings() + text[m.end():]

    # The executors: on the ShipActions object, in the registry, and as documents after it.
    text = replace_once(text, f"  - component: {{fileID: {REGISTRY_ID}}}\n",
                        f"  - component: {{fileID: {REGISTRY_ID}}}\n"
                        f"  - component: {{fileID: {SLING_EXECUTOR_ID}}}\n"
                        f"  - component: {{fileID: {STOP_EXECUTOR_ID}}}\n", "ShipActions m_Component entry")
    reg = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  _executors:\n((?:  - \{fileID: -?\d+\}\n)+)" % REGISTRY_ID, text)
    if not reg:
        raise RuntimeError("donor drifted: ActionExecutorRegistry _executors list not found")
    text = text[:reg.end(1)] + f"  - {{fileID: {SLING_EXECUTOR_ID}}}\n  - {{fileID: {STOP_EXECUTOR_ID}}}\n" + text[reg.end(1):]
    end = re.search(r"--- !u!114 &%s\n(?:.*\n)*?(?=--- !u!)" % REGISTRY_ID, text)
    if not end:
        raise RuntimeError("donor drifted: ActionExecutorRegistry document has no successor")
    text = text[:end.end()] + executor_blocks(find_component_id(donor, PRISM_CONTROLLER_SCRIPT)) + text[end.end():]
    return text


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- stage 2: the hull
def hull_blocks() -> str:
    """The StoatHull GameObject: Transform under the root, MeshFilter (the builder fills it at
    Awake), MeshRenderer with the body + domain materials, and the StoatHullBuilder pointing at
    the Squirrel model it hides. Proportions are NOT serialized here - the builder's field
    initializers are the shipped values (round 2, Option 2), so the C# is the one source."""
    return (
        f"--- !u!1 &{HULL_GO_ID}\n"
        "GameObject:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  serializedVersion: 6\n"
        "  m_Component:\n"
        f"  - component: {{fileID: {HULL_TRANSFORM_ID}}}\n"
        f"  - component: {{fileID: {HULL_FILTER_ID}}}\n"
        f"  - component: {{fileID: {HULL_RENDERER_ID}}}\n"
        f"  - component: {{fileID: {HULL_BUILDER_ID}}}\n"
        f"  m_Layer: {SHIPS_LAYER}\n"
        "  m_Name: StoatHull\n"
        "  m_TagString: Untagged\n"
        "  m_Icon: {fileID: 0}\n"
        "  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n"
        "  m_IsActive: 1\n"
        f"--- !u!4 &{HULL_TRANSFORM_ID}\n"
        "Transform:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {HULL_GO_ID}}}\n"
        "  serializedVersion: 2\n"
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_ConstrainProportionsScale: 0\n"
        "  m_Children: []\n"
        f"  m_Father: {{fileID: {ROOT_TRANSFORM_ID}}}\n"
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
        f"--- !u!33 &{HULL_FILTER_ID}\n"
        "MeshFilter:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {HULL_GO_ID}}}\n"
        "  m_Mesh: {fileID: 0}\n"
        f"--- !u!23 &{HULL_RENDERER_ID}\n"
        "MeshRenderer:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {HULL_GO_ID}}}\n"
        "  m_Enabled: 1\n"
        "  m_CastShadows: 1\n"
        "  m_ReceiveShadows: 1\n"
        "  m_DynamicOccludee: 1\n"
        "  m_StaticShadowCaster: 0\n"
        "  m_MotionVectors: 1\n"
        "  m_LightProbeUsage: 1\n"
        "  m_ReflectionProbeUsage: 1\n"
        "  m_RayTracingMode: 2\n"
        "  m_RayTraceProcedural: 0\n"
        "  m_RayTracingAccelStructBuildFlagsOverride: 0\n"
        "  m_RayTracingAccelStructBuildFlags: 1\n"
        "  m_SmallMeshCulling: 1\n"
        "  m_RenderingLayerMask: 1\n"
        "  m_RendererPriority: 0\n"
        "  m_Materials:\n"
        f"  - {{fileID: 2100000, guid: {BODY_MATERIAL}, type: 2}}\n"
        f"  - {{fileID: 2100000, guid: {ACCENT_MATERIAL}, type: 2}}\n"
        "  m_StaticBatchInfo:\n"
        "    firstSubMesh: 0\n"
        "    subMeshCount: 0\n"
        "  m_StaticBatchRoot: {fileID: 0}\n"
        "  m_ProbeAnchor: {fileID: 0}\n"
        "  m_LightProbeVolumeOverride: {fileID: 0}\n"
        "  m_ScaleInLightmap: 1\n"
        "  m_ReceiveGI: 1\n"
        "  m_PreserveUVs: 0\n"
        "  m_IgnoreNormalsForChartDetection: 0\n"
        "  m_ImportantGI: 0\n"
        "  m_StitchLightmapSeams: 1\n"
        "  m_SelectedEditorRenderState: 3\n"
        "  m_MinimumChartSize: 4\n"
        "  m_AutoUVMaxDistance: 0.5\n"
        "  m_AutoUVMaxAngle: 89\n"
        "  m_LightmapParameters: {fileID: 0}\n"
        "  m_SortingLayerID: 0\n"
        "  m_SortingLayer: 0\n"
        "  m_SortingOrder: 0\n"
        "  m_AdditionalVertexStreams: {fileID: 0}\n"
        f"--- !u!114 &{HULL_BUILDER_ID}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {HULL_GO_ID}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {HULL_BUILDER_SCRIPT}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        f"  legacyModelRoot: {{fileID: {SQUIRREL_MODEL_ROOT}}}\n"
    )


def animation_block() -> str:
    """The root's VesselAnimation, switched from the Squirrel clone's MantaAnimationContoller to
    StoatAnimation on the SAME fileID (so VesselStatus's reference to it holds). The base fields
    keep their authored values; the Stoat fields are the user's lope (the C# initializers)."""
    return (
        f"--- !u!114 &{ANIMATION_ID}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {ROOT_GO}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {STOAT_ANIMATION_SCRIPT}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        "  SkinnedMeshRenderer: {fileID: 0}\n"
        "  SaveNewPositions: 1\n"
        "  brakeThreshold: 0.65\n"
        "  lerpAmount: 2\n"
        "  smallLerpAmount: 0.7\n"
        "  lopeAmplitude: 1.6\n"
        "  lopeRate: 0.66\n"
        "  lopeArch: 0.45\n"
        "  lopeSquash: 0.2\n"
        "  lopeSpeedLink: 0\n"
        "  cruiseSpeed: 60\n"
        "  spineArchLift: 0.35\n"
        "  turnBendDegrees: 12\n"
        "  headTurnDegrees: 18\n"
        "  headNodDegrees: 14\n"
        "  tailSwingDegrees: 28\n"
        "  tailLift: 0.45\n"
        "  legReachDegrees: 52\n"
    )


def apply_hull(text: str) -> str:
    """Stage 2, idempotent: add the hull, point the paint at it, swap the animation. Runs on the
    committed prefab (the clone stands down once it exists), so it re-checks its own anchors."""
    if f"--- !u!1 &{HULL_GO_ID}\n" not in text:
        # the root's children list gains the hull's transform
        m = re.search(r"(--- !u!4 &%s\n(?:.*\n)*?  m_Children:\n)((?:  - \{fileID: -?\d+\}\n)*)" % ROOT_TRANSFORM_ID, text)
        if not m:
            raise RuntimeError("Stoat.prefab drifted: root Transform m_Children not found")
        text = text[:m.end(2)] + f"  - {{fileID: {HULL_TRANSFORM_ID}}}\n" + text[m.end(2):]
        # the documents go after the root Transform's document
        end = re.search(r"--- !u!4 &%s\n(?:.*\n)*?(?=--- !u!)" % ROOT_TRANSFORM_ID, text)
        text = text[:end.end()] + hull_blocks() + text[end.end():]
    # the domain paint moves from the hidden Squirrel mesh to the hull
    text = text.replace(f"  _shipGeometries:\n  - {{fileID: {SQUIRREL_MESH_GO}}}\n",
                        f"  _shipGeometries:\n  - {{fileID: {HULL_GO_ID}}}\n")
    # the animation component, swapped in place
    m = re.search(r"--- !u!114 &%s\n(?:.*\n)*?(?=--- !u!)" % ANIMATION_ID, text)
    if not m:
        raise RuntimeError("Stoat.prefab drifted: the root's VesselAnimation component not found")
    if STOAT_ANIMATION_SCRIPT not in m.group(0) and MANTA_ANIMATION_SCRIPT not in m.group(0):
        raise RuntimeError("Stoat.prefab drifted: the root's VesselAnimation is neither the clone's nor the Stoat's")
    text = text[:m.start()] + animation_block() + text[m.end():]
    return text


# ----------------------------------------------------------------------------- stage 3: the class asset
def class_asset() -> str:
    """SO_Class_Stoat - the SO_Vessel a card's Vessels list, the hangar and the class lists name a
    hull by (CONTRACT.md 1.9). Butterfly-shaped; owned from the start so the prototype can be
    picked; the Squirrel's icons stand in until the Stoat's art lands."""
    return (
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SO_VESSEL_SCRIPT}, type: 3}}\n"
        "  m_Name: SO_Class_Stoat\n  m_EditorClassIdentifier: Assembly-CSharp::SO_Vessel\n"
        f"  Class: {STOAT_CLASS_ID}\n  Name: Stoat\n"
        "  Description: Never flies straight for long. The Stoat throws a wormhole pair across\n"
        "    itself - attractor on one side, repulsor on the other - and goes where the pull\n"
        "    throws it.\n"
        "  PrimaryElement: 3\n  Element: {fileID: 0}\n"
        "  InitialResourceLevels:\n    Mass: 0\n    Charge: 0\n    Space: 0\n    Time: 0\n"
        f"  IconActive: {{fileID: 21300000, guid: {SQUIRREL_ICON_ACTIVE}, type: 3}}\n"
        f"  IconInactive: {{fileID: 21300000, guid: {SQUIRREL_ICON_INACTIVE}, type: 3}}\n"
        "  PreviewImage: {fileID: 0}\n  SquadImage: {fileID: 0}\n  TrailPreviewImage: {fileID: 0}\n"
        "  CardSilohoutteActive: {fileID: 0}\n  CardSilohoutteInactive: {fileID: 0}\n"
        "  Abilities: []\n  Games: []\n  TrainingGames: []\n"
        "  gameplayParameter1:\n    LeftHandLabel: Casual\n    RightHandLabel: Challenging\n    Value: 0.6\n"
        "  gameplayParameter2:\n    LeftHandLabel: Relaxing\n    RightHandLabel: Thrilling\n    Value: 0.7\n"
        "  gameplayParameter3:\n    LeftHandLabel: Solo\n    RightHandLabel: Social\n    Value: 0.5\n"
        "  isLocked: 0\n  ownedFromStart: 1\n  UnlockCost: 0\n"
    )


def class_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {CLASS_GUID}\n"
            "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def class_list_entry() -> str:
    return f"  - {{fileID: 11400000, guid: {CLASS_GUID}, type: 2}}\n"


def register_class_list(text: str) -> str:
    if CLASS_GUID in text:
        return text
    m = re.search(r"  VesselList:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    if not m:
        raise RuntimeError("class list: VesselList not found")
    return text[:m.end(1)] + class_list_entry() + text[m.end(1):]


# ----------------------------------------------------------------------------- registrations
def container_entry() -> str:
    # The container's slots are Transform[]: the entry names the prefab's ROOT TRANSFORM. The GameObject's
    # fileID (which DefaultNetworkPrefabs rightly takes) loads there as a reference that throws
    # MissingReferenceException, and hung boot on "Host ready..." (check_vessel_prefab_container.py check 7).
    return f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n"


def register_container(text: str) -> str:
    if PREFAB_GUID in text:
        return text
    m = re.search(r"  _shipPrefabs:\n((?:  - \{fileID: -?\d+, guid: [0-9a-f]{32}, type: 3\}\n)+)", text)
    if not m:
        raise RuntimeError("Vessel Prefab Container: _shipPrefabs list not found")
    return text[:m.end(1)] + container_entry() + text[m.end(1):]


def network_entry() -> str:
    return ("  - Override: 0\n"
            f"    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n"
            "    SourcePrefabToOverride: {fileID: 0}\n"
            "    SourceHashToOverride: 0\n"
            "    OverridingTargetPrefab: {fileID: 0}\n")


def register_network(text: str) -> str:
    if PREFAB_GUID in text:
        return text
    if not text.endswith("\n"):
        text += "\n"
    return text + network_entry()


# ----------------------------------------------------------------------------- the checks
# ----------------------------------------------------------------------------- stage 4: the dipole
def num(v: float) -> str:
    """A float the way Unity's YAML writes it (7 significant digits, integers bare)."""
    return str(int(v)) if float(v).is_integer() else ("%.7g" % v)


def color(r: float, g: float, b: float, a: float = 1.0) -> str:
    return f"{{r: {num(r)}, g: {num(g)}, b: {num(b)}, a: {num(a)}}}"


def elemental(value, lo, hi, element, floor) -> str:
    return ("    Enabled: 1\n"
            f"    Value: {num(value)}\n"
            f"    Min: {num(lo)}\n"
            f"    Max: {num(hi)}\n"
            f"    element: {element}\n"
            "    UseFloor: 1\n"
            f"    Floor: {num(floor)}\n")


def empty_event(name: str) -> str:
    return (f"  {name}:\n"
            "    Guid:\n"
            "      Data1: 0\n"
            "      Data2: 0\n"
            "      Data3: 0\n"
            "      Data4: 0\n"
            "    Path: \n")


def so_header(script_guid: str, name: str) -> str:
    return ("%YAML 1.1\n"
            "%TAG !u! tag:unity3d.com,2011:\n"
            "--- !u!114 &11400000\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            f"  m_Name: {name}\n"
            "  m_EditorClassIdentifier: \n")


def dipole_config_asset() -> str:
    """StoatDipoleConfig: the studio's shipped round-15 row (StoatDipoleConfigSO's initializers)."""
    return (so_header(DIPOLE_CONFIG_SCRIPT, "StoatDipoleConfig") +
            "  holdExponent: 1.5\n"
            "  holdRampSeconds: 1.5\n"
            "  keySqueeze: 0.5\n"   # the studio's dpKeySqueeze
            "  aheadDistance: 250\n"
            "  sidewaysMax: 200\n"
            "  lengthwaysMax: 120\n"
            "  followRate: 6\n"
            "  poleGM: 120000\n"
            "  poleHorizon: 3.5\n"
            "  poleSize:\n" + elemental(1, 1, 2, 3, 1) +
            "  sourcePush: 1\n"
            "  sourceSofteningHorizons: 2\n"
            "  accelerationCap: 3000\n"
            "  sinkGrowSeconds: 0.9\n"
            "  sourceGrowSeconds: 1.1\n"
            "  turnCap: 12\n"
            "  grip: 0.5\n"
            "  gravitySpeedCeilingCruises: 4\n"
            "  gravityFadeSeconds: 2\n"
            "  domainTintAmount: 0\n"   # the studio draws no tint: a black shadow, a white core
            "  crystalStripShare: 1\n"
            "  faunaSwallowHorizons: 1.5\n"
            "  pathLength: 600\n"
            "  pathStep: 3\n"
            "  pathNoseOffset: 8\n"
            "  loopMargin: 8\n"
            "  minLoop: 60\n"
            "  warpDegrees: 3\n"
            "  boost:\n" + elemental(2, 2, 4, 4, 1) +
            "  boostRise: 20\n"
            "  boostFadeSeconds: 0.6\n"
            "  dotPixels: 4\n"
            "  dotGap: 10\n"
            "  dotsInWorld: 1\n"          # the path in the scene, denser than the studio's 2D line (the user, 2026-10-10)
            "  worldDotSize: 0.7\n"
            "  worldDotSpacing: 3\n"
            "  worldDotMinPixels: 2.5\n"
            f"  openColor: {color(150 / 255, 166 / 255, 194 / 255)}\n"
            f"  warpedColor: {color(157 / 255, 1, 46 / 255)}\n"
            "  autopilotHold01: 1\n"
            "  autopilotMinDistance: 300\n"
            "  autopilotHoldSeconds: 4\n"
            "  autopilotIntervalSeconds: 2\n"
            # the studio's path-watching field AI (aiWarpQ 1 above, aiNear, aiLimeWait)
            "  autopilotWatchPath: 1\n"
            "  autopilotLetGoNear: 60\n"
            "  autopilotDrySeconds: 0.5\n"
            "  autopilotMinHoldSeconds: 0.8\n"
            "  autopilotMaxHoldSeconds: 15\n"
            "  autopilotRelaySeconds: 0.25\n" +
            empty_event("openEvent") + empty_event("annihilateEvent") + empty_event("warpEvent"))


def dipole_action_asset(name: str, side: int) -> str:
    return so_header(DIPOLE_ACTION_SCRIPT, name) + f"  side: {side}\n"


def asset_meta(guid: str) -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


def script_meta(guid: str) -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "MonoImporter:\n"
            "  externalObjects: {}\n"
            "  serializedVersion: 2\n"
            "  defaultReferences: []\n"
            "  executionOrder: 0\n"
            "  icon: {instanceID: 0}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


DIPOLE_ASSETS = (("StoatDipoleConfig", DIPOLE_CONFIG, dipole_config_asset),
                 ("StoatDipoleLeftAction", DIPOLE_LEFT, lambda: dipole_action_asset("StoatDipoleLeftAction", 0)),
                 ("StoatDipoleRightAction", DIPOLE_RIGHT, lambda: dipole_action_asset("StoatDipoleRightAction", 1)))


def dipole_executor_blocks() -> str:
    def block(fid: str, script: str) -> str:
        return (f"--- !u!114 &{fid}\n"
                "MonoBehaviour:\n"
                "  m_ObjectHideFlags: 0\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                f"  m_GameObject: {{fileID: {SHIP_ACTIONS_GO}}}\n"
                "  m_Enabled: 1\n"
                "  m_EditorHideFlags: 0\n"
                f"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n"
                "  m_Name: \n"
                "  m_EditorClassIdentifier: \n"
                f"  config: {{fileID: 11400000, guid: {DIPOLE_CONFIG}, type: 2}}\n")
    return block(DIPOLE_EXECUTOR_ID, DIPOLE_EXECUTOR_SCRIPT) + block(PATHFINDER_EXECUTOR_ID, PATHFINDER_EXECUTOR_SCRIPT)


def transformer_block(text: str):
    return re.search(r"--- !u!114 &%s\n(?:.*\n)*?(?=--- !u!)" % TRANSFORMER_ID, text)


def apply_dipole(text: str) -> str:
    """Stage 4 on the committed prefab: rebind the triggers, add the two executors, slow the base turn.
    Idempotent (re-applying it to its own output changes nothing); re-checks its own anchors."""
    m = re.search(r"  _touchActionOverrides:.*\n(?:.*\n)*?(?=  _onButtonPressed:)", text)
    if not m:
        raise RuntimeError("Stoat.prefab drifted: R_VesselActionHandler binding block not found")
    text = text[:m.start()] + bindings() + text[m.end():]
    if f"  - component: {{fileID: {DIPOLE_EXECUTOR_ID}}}\n" not in text:
        text = replace_once(text, f"  - component: {{fileID: {STOP_EXECUTOR_ID}}}\n",
                            f"  - component: {{fileID: {STOP_EXECUTOR_ID}}}\n"
                            f"  - component: {{fileID: {DIPOLE_EXECUTOR_ID}}}\n"
                            f"  - component: {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n", "ShipActions stop executor entry")
    reg = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  _executors:\n((?:  - \{fileID: -?\d+\}\n)+)" % REGISTRY_ID, text)
    if not reg:
        raise RuntimeError("Stoat.prefab drifted: ActionExecutorRegistry _executors list not found")
    if f"  - {{fileID: {DIPOLE_EXECUTOR_ID}}}\n" not in reg.group(1):
        text = text[:reg.end(1)] + f"  - {{fileID: {DIPOLE_EXECUTOR_ID}}}\n  - {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n" + text[reg.end(1):]
    if f"--- !u!114 &{DIPOLE_EXECUTOR_ID}\n" not in text:
        end = re.search(r"--- !u!114 &%s\n(?:.*\n)*?(?=--- !u!)" % STOP_EXECUTOR_ID, text)
        if not end:
            raise RuntimeError("Stoat.prefab drifted: the stop executor's document has no successor")
        text = text[:end.end()] + dipole_executor_blocks() + text[end.end():]
    t = transformer_block(text)
    if not t:
        raise RuntimeError("Stoat.prefab drifted: the VesselTransformer document not found")
    block = t.group(0)
    for key, old, new in TURN_RATES:
        block = block.replace(f"  {key}: {old}\n", f"  {key}: {new}\n", 1)
    return text[:t.start()] + block + text[t.end():]


def camera_asset() -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {CAMERA_SCRIPT}, type: 3}}\n"
            "  m_Name: StoatCameraSettingsSO\n  m_EditorClassIdentifier: \n"
            "  mode: 0\n"
            "  followOffset: {x: 0, y: 6.5, z: -21}\n"
            "  lookAheadDistance: 40\n"
            "  lookAheadLift: 3\n"
            "  chaseEaseRate: 7\n"
            "  framingFieldOfView: 68\n"
            "  dynamicMinDistance: 10\n  dynamicMaxDistance: 40\n  followSmoothTime: 0.2\n  rotationSmoothTime: 5\n"
            "  disableSmoothing: 0\n  nearClipPlane: 0.3\n  farClipPlane: 12000\n  enableAdaptiveZoom: 0\n"
            "  adaptiveMaxDistance: 0\n  orthographicSize: 5\n")


def camera_reference(guid: str) -> str:
    return f"  settings: {{fileID: 11400000, guid: {guid}, type: 2}}\n  OnInitializePlayerCamera:"


def apply_camera(text: str) -> str:
    """Stage 5 on the committed prefab: VesselCameraCustomizer points at the Stoat's own camera asset. Idempotent."""
    if camera_reference(CAMERA_GUID) in text:
        return text
    return replace_once(text, camera_reference(SQUIRREL_CAMERA_GUID), camera_reference(CAMERA_GUID),
                        "VesselCameraCustomizer settings (the Squirrel's camera asset)")


def seeded_assets() -> "dict[str, str]":
    """The TUNING assets: {absolute path: the script guid they must carry}. This generator SEEDS them (writes them only
    when absent) and then checks only that they exist with the right script. Their numbers belong to the Vessel Studio
    window in Unity (FrogletTools > Vessels > Vessel Studio > the Stoat card > TUNE IN UNITY, Assets/_Scripts/Editor/Studios/), where they are
    tuned live and committed by its ship panel; asserting them here made every tweak fail this gate."""
    return {os.path.join(ACTIONS, "StoatDipoleConfig.asset"): DIPOLE_CONFIG_SCRIPT, CAMERA_ASSET: CAMERA_SCRIPT}


def dipole_files() -> "dict[str, str]":
    """Every non-prefab file stage 4 owns, {absolute path: content}."""
    out = {}
    for name, guid, make in DIPOLE_ASSETS:
        out[os.path.join(ACTIONS, name + ".asset")] = make()
        out[os.path.join(ACTIONS, name + ".asset.meta")] = asset_meta(guid)
    for path, guid in DIPOLE_SCRIPTS:
        out[path + ".meta"] = script_meta(guid)
    out[DIPOLE_DOC + ".meta"] = ("fileFormatVersion: 2\n"
                                 f"guid: {DIPOLE_DOC_GUID}\n"
                                 "TextScriptImporter:\n"
                                 "  externalObjects: {}\n"
                                 "  userData: \n"
                                 "  assetBundleName: \n"
                                 "  assetBundleVariant: \n")
    out[CAMERA_ASSET] = camera_asset()
    out[CAMERA_ASSET + ".meta"] = asset_meta(CAMERA_GUID)
    return out


def check(files: "dict[str, str]") -> "list[str]":
    """Every invariant of the shipped set, over {relative path: content}. Pure, so the self-test
    can hand it mutated copies."""
    errors = []
    rel_prefab = os.path.relpath(PREFAB, ROOT)
    prefab = files.get(rel_prefab)
    if prefab is None:
        return [f"{rel_prefab} is missing - run without --check to author it"]
    meta = files.get(rel_prefab + ".meta", "")
    if f"guid: {PREFAB_GUID}" not in meta:
        errors.append(f"{rel_prefab}.meta does not carry guid {PREFAB_GUID}")

    def want(cond, msg):
        if not cond:
            errors.append(f"{rel_prefab}: {msg}")

    want(prefab.count("  m_Name: Stoat\n") == 1, "root GameObject is not named Stoat")
    want(prefab.count("  _name: Stoat\n") == 1, "VesselStatus._name is not Stoat")
    want("  m_Name: Squirrel\n" not in prefab and "  _name: Squirrel\n" not in prefab, "still named Squirrel somewhere")
    types = re.findall(r"  vesselType: (-?\d+)\n", prefab)
    want(types == [str(STOAT_CLASS_ID)], f"vesselType must be exactly one '{STOAT_CLASS_ID}', found {types}")

    net = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n"
                    % NETWORK_OBJECT_ID, prefab)
    if not net:
        want(False, "NetworkObject block not found")
    else:
        expected = global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)
        want(int(net.group(1)) == expected,
             f"GlobalObjectIdHash is {net.group(1)}, the editor would write {expected} for this guid")
        want(net.group(2) == "0", "InScenePlacedSourceGlobalObjectIdHash must be 0 on a disk-authored prefab")
        donor = files.get(os.path.relpath(DONOR, ROOT), "")
        dm = re.search(r"  GlobalObjectIdHash: (\d+)\n", donor)
        if dm:
            want(dm.group(1) != net.group(1), "GlobalObjectIdHash still equals the Squirrel's - Netcode would key both on one entry")

    want(bindings() in prefab, "gamepad bindings are not LT->dipole left, RT->dipole right, X->hold (touch cleared)")
    want(f"  - component: {{fileID: {DIPOLE_EXECUTOR_ID}}}\n" in prefab, "StoatDipoleExecutor is not on the ShipActions object")
    want(f"  - component: {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n" in prefab, "StoatPathfinderExecutor is not on the ShipActions object")
    want(dipole_executor_blocks() in prefab, "the dipole / pathfinder executor documents are not authored as expected (StoatDipoleConfig)")
    tb = transformer_block(prefab)
    want(tb is not None and all(f"  {k}: {new}\n" in tb.group(0) for k, _, new in TURN_RATES),
         "the VesselTransformer's turn rates are not the dipole's (pitch 48, yaw 48, roll 52)")
    want(f"  - component: {{fileID: {SLING_EXECUTOR_ID}}}\n" in prefab, "StoatSlingExecutor is not on the ShipActions object")
    want(f"  - component: {{fileID: {STOP_EXECUTOR_ID}}}\n" in prefab, "ToggleTranslationModeActionExecutor is not on the ShipActions object")
    reg = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  _executors:\n((?:  - \{fileID: -?\d+\}\n)+)" % REGISTRY_ID, prefab)
    want(reg is not None and f"  - {{fileID: {DIPOLE_EXECUTOR_ID}}}\n" in reg.group(1)
         and f"  - {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n" in reg.group(1),
         "the dipole / pathfinder executors are not in the ActionExecutorRegistry")
    want(reg is not None and f"  - {{fileID: {SLING_EXECUTOR_ID}}}\n" in reg.group(1)
         and f"  - {{fileID: {STOP_EXECUTOR_ID}}}\n" in reg.group(1), "registry _executors lacks the two Stoat executors")
    try:
        prism = find_component_id(prefab, PRISM_CONTROLLER_SCRIPT)
        want(executor_blocks(prism) in prefab, "executor documents are not authored as expected (config, prism controller, shared channels)")
    except RuntimeError as e:
        want(False, str(e))

    # stage 2: the hull
    want(hull_blocks() in prefab, "the StoatHull GameObject (filter, renderer, builder) is not authored as expected")
    want(re.search(r"--- !u!4 &%s\n(?:.*\n)*?  m_Children:\n(?:  - \{fileID: -?\d+\}\n)*  - \{fileID: %s\}\n"
                   % (ROOT_TRANSFORM_ID, HULL_TRANSFORM_ID), prefab) is not None, "the hull is not a child of the root")
    want(f"  _shipGeometries:\n  - {{fileID: {HULL_GO_ID}}}\n" in prefab,
         "VesselCustomization does not paint the hull (the domain colour would land on the hidden Squirrel mesh)")
    want(animation_block() in prefab, "the root's VesselAnimation is not StoatAnimation with the user's lope")
    want(camera_reference(CAMERA_GUID) in prefab,
         "VesselCameraCustomizer does not use StoatCameraSettingsSO (the studio's chase camera)")
    want(MANTA_ANIMATION_SCRIPT not in prefab, "the Squirrel clone's MantaAnimationContoller is still on the prefab")
    want(f"  - component: {{fileID: {ANIMATION_ID}}}\n" in prefab, "the animation component left the root")

    rel_container = os.path.relpath(CONTAINER, ROOT)
    want_c = files.get(rel_container, "")
    if container_entry() not in want_c:
        errors.append(f"{rel_container}: no entry for Stoat.prefab")
    rel_net = os.path.relpath(NETWORK_PREFABS, ROOT)
    if network_entry() not in files.get(rel_net, ""):
        errors.append(f"{rel_net}: no entry for Stoat.prefab")

    for name, guid in (("StoatSlingConfig", SLING_CONFIG), ("StoatSlingLeftAction", SLING_LEFT),
                       ("StoatSlingRightAction", SLING_RIGHT), ("StoatHoldAction", HOLD)):
        rel = os.path.relpath(os.path.join(ACTIONS, name + ".asset"), ROOT)
        if rel not in files:
            errors.append(f"{rel} is missing")
        elif f"guid: {guid}" not in files.get(rel + ".meta", ""):
            errors.append(f"{rel}.meta does not carry guid {guid}")
    rel_map = os.path.relpath(MAP, ROOT)
    mp = files.get(rel_map)
    if mp is None:
        errors.append(f"{rel_map} is missing")
    else:
        if f"  vesselClass: {STOAT_CLASS_ID}\n" not in mp:
            errors.append(f"{rel_map}: vesselClass is not {STOAT_CLASS_ID}")
        if "    AbilityLabel: Field Dipole\n    AbilityDescription" not in mp or "    Input: 2\n" not in mp:
            errors.append(f"{rel_map}: Space must be the Field Dipole on the triggers (Input 2)")
        if "    AbilityLabel: Pathfinder\n" not in mp:
            errors.append(f"{rel_map}: Time must be the Pathfinder")
    seeded = seeded_assets()
    for path, want_text in dipole_files().items():
        rel = os.path.relpath(path, ROOT)
        if not os.path.exists(path[:-5]) and path.endswith(".cs.meta"):
            errors.append(f"{rel[:-5]} is missing (its .meta is authored here)")
        if path in seeded:
            text = files.get(rel)
            if text is None:
                errors.append(f"{rel} is missing - run without --check to seed it")
            elif f"m_Script: {{fileID: 11500000, guid: {seeded[path]}, type: 3}}" not in text:
                errors.append(f"{rel} does not carry its script (guid {seeded[path]})")
            continue
        if files.get(rel) != want_text:
            errors.append(f"{rel} is missing or not as authored - run without --check")
    rel_class = os.path.relpath(CLASS_ASSET, ROOT)
    if files.get(rel_class) != class_asset():
        errors.append(f"{rel_class} is missing or not as authored (Class {STOAT_CLASS_ID}, owned from start)")
    if f"guid: {CLASS_GUID}" not in files.get(rel_class + ".meta", ""):
        errors.append(f"{rel_class}.meta does not carry guid {CLASS_GUID}")
    for cl in CLASS_LISTS:
        rel_cl = os.path.relpath(cl, ROOT)
        if files.get(rel_cl, "").count(class_list_entry()) != 1:
            errors.append(f"{rel_cl}: SO_Class_Stoat is not listed exactly once")
    sl = files.get(os.path.relpath(os.path.join(ACTIONS, "StoatSlingLeftAction.asset"), ROOT), "")
    sr = files.get(os.path.relpath(os.path.join(ACTIONS, "StoatSlingRightAction.asset"), ROOT), "")
    if sl and "  side: 0\n" not in sl:
        errors.append("StoatSlingLeftAction.asset is not side 0 (Left)")
    if sr and "  side: 1\n" not in sr:
        errors.append("StoatSlingRightAction.asset is not side 1 (Right)")
    return errors


def read_all() -> "dict[str, str]":
    paths = [PREFAB, PREFAB + ".meta", DONOR, CONTAINER, NETWORK_PREFABS, MAP, CLASS_ASSET, CLASS_ASSET + ".meta"] + CLASS_LISTS
    for name in ("StoatSlingConfig", "StoatSlingLeftAction", "StoatSlingRightAction", "StoatHoldAction"):
        paths += [os.path.join(ACTIONS, name + ".asset"), os.path.join(ACTIONS, name + ".asset.meta")]
    paths += list(dipole_files().keys())
    files = {}
    for p in paths:
        if os.path.exists(p):
            with open(p, encoding="utf-8", newline="") as f:
                files[os.path.relpath(p, ROOT)] = f.read().replace("\r\n", "\n")
    return files


def write(path: str, text: str) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


# ----------------------------------------------------------------------------- self-test
def self_test() -> int:
    ok = xxhash32(b"") == 0x02CC5D05 and xxhash32(b"a") == 0x550D7456
    # The Squirrel's own hash, as the editor wrote it, from its guid and NetworkObject id.
    ok &= global_object_id_hash("84755e5175a641e0bff6b47d35746cff", NETWORK_OBJECT_ID) == 2256742461
    print(f"xxhash32 reproduces Netcode's hashes: {'OK' if ok else 'FAIL'}")
    files = read_all()
    base = check(files)
    print(f"shipped set: {'OK' if not base else 'FAIL ' + '; '.join(base)}")
    ok &= not base
    rel = os.path.relpath(PREFAB, ROOT)

    def fires(label, mutate):
        nonlocal ok
        mutated = dict(files)
        mutate(mutated)
        errs = check(mutated)
        print(f"  negative control {label}: {'fires' if errs else 'MISSED'}")
        ok &= bool(errs)

    fires("wrong vesselType", lambda f: f.__setitem__(rel, f[rel].replace(f"  vesselType: {STOAT_CLASS_ID}\n", "  vesselType: 6\n")))
    fires("donor hash kept", lambda f: f.__setitem__(rel, re.sub(r"  GlobalObjectIdHash: \d+\n", "  GlobalObjectIdHash: 2256742461\n", f[rel], count=1)))
    fires("executor dropped from registry", lambda f: f.__setitem__(rel, f[rel].replace(f"  - {{fileID: {SLING_EXECUTOR_ID}}}\n", "", 1)))
    fires("left trigger unbound", lambda f: f.__setitem__(rel, f[rel].replace(f"    - {{fileID: 11400000, guid: {DIPOLE_LEFT}, type: 2}}\n", "", 1)))
    fires("pathfinder dropped from registry", lambda f: f.__setitem__(rel, f[rel].replace(f"  - {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n", "", 1)))
    fires("turn rates reverted", lambda f: f.__setitem__(rel, f[rel].replace("  PitchScaler: 48\n", "  PitchScaler: 120\n", 1)))
    cfg = os.path.relpath(os.path.join(ACTIONS, "StoatDipoleConfig.asset"), ROOT)
    fires("dipole config's script swapped", lambda f: f.__setitem__(cfg, f.get(cfg, "").replace(DIPOLE_CONFIG_SCRIPT, "0" * 32)))
    fires("dipole config deleted", lambda f: f.pop(cfg, None))
    fires("container entry missing", lambda f: f.__setitem__(os.path.relpath(CONTAINER, ROOT), f[os.path.relpath(CONTAINER, ROOT)].replace(container_entry(), "")))
    fires("network entry missing", lambda f: f.__setitem__(os.path.relpath(NETWORK_PREFABS, ROOT), f[os.path.relpath(NETWORK_PREFABS, ROOT)].replace(network_entry(), "")))
    fires("map lost the dipole's input", lambda f: f.__setitem__(os.path.relpath(MAP, ROOT), f[os.path.relpath(MAP, ROOT)].replace("    Input: 2\n", "    Input: 0\n")))
    fires("camera back on the Squirrel's", lambda f: f.__setitem__(rel, f[rel].replace(camera_reference(CAMERA_GUID), camera_reference(SQUIRREL_CAMERA_GUID))))
    cam = os.path.relpath(CAMERA_ASSET, ROOT)
    fires("chase camera's script swapped", lambda f: f.__setitem__(cam, f.get(cam, "").replace(CAMERA_SCRIPT, "0" * 32)))
    # a tuned number is NOT a finding: the Vessel Studio window owns it
    tuned = dict(files); tuned[cfg] = tuned[cfg].replace("  aheadDistance: 250\n", "  aheadDistance: 200\n")
    ok_tuned = not check(tuned)
    print(f"  a number tuned in the Vessel Studio window passes: {'yes' if ok_tuned else 'NO'}")
    ok &= ok_tuned
    fires("container entry is the GameObject", lambda f: f.__setitem__(os.path.relpath(CONTAINER, ROOT), f[os.path.relpath(CONTAINER, ROOT)].replace(container_entry(), f"  - {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n")))
    fires("hull dropped", lambda f: f.__setitem__(rel, f[rel].replace(hull_blocks(), "")))
    fires("paint left on the Squirrel mesh", lambda f: f.__setitem__(rel, f[rel].replace(
        f"  _shipGeometries:\n  - {{fileID: {HULL_GO_ID}}}\n", f"  _shipGeometries:\n  - {{fileID: {SQUIRREL_MESH_GO}}}\n")))
    fires("animation reverted to the Manta's", lambda f: f.__setitem__(rel, f[rel].replace(STOAT_ANIMATION_SCRIPT, MANTA_ANIMATION_SCRIPT)))
    fires("class asset missing from a class list", lambda f: f.__setitem__(os.path.relpath(CLASS_LISTS[0], ROOT),
          f[os.path.relpath(CLASS_LISTS[0], ROOT)].replace(class_list_entry(), "")))
    # stage 2 is idempotent: applying it to the shipped prefab changes nothing
    idem = apply_hull(files[rel]) == files[rel]
    print(f"  stage 2 idempotent on the shipped prefab: {'yes' if idem else 'NO'}")
    ok &= idem
    idem = apply_dipole(files[rel]) == files[rel]
    print(f"  stage 4 idempotent on the shipped prefab: {'yes' if idem else 'NO'}")
    ok &= idem
    idem = apply_camera(files[rel]) == files[rel]
    print(f"  stage 5 idempotent on the shipped prefab: {'yes' if idem else 'NO'}")
    ok &= idem
    print("self-test " + ("OK" if ok else "FAILED"))
    return 0 if ok else 1


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        if not os.path.exists(PREFAB):
            with open(DONOR, encoding="utf-8", newline="") as f:
                donor = f.read().replace("\r\n", "\n")
            write(PREFAB, build_clone(donor))
            write(PREFAB + ".meta", prefab_meta())
            print(f"wrote {os.path.relpath(PREFAB, ROOT)} (+ .meta) from Squirrel.prefab")
        else:
            print(f"{os.path.relpath(PREFAB, ROOT)} exists - the clone stands down (delete it to re-author)")
        with open(PREFAB, encoding="utf-8", newline="") as f:
            before = f.read().replace("\r\n", "\n")
        after = apply_hull(before)
        if after != before:
            write(PREFAB, after)
            print(f"stage 2: the procedural hull + StoatAnimation authored into {os.path.relpath(PREFAB, ROOT)}")
        before = after
        after = apply_dipole(before)
        if after != before:
            write(PREFAB, after)
            print(f"stage 4: the field dipole + pathfinder wired into {os.path.relpath(PREFAB, ROOT)}")
        before = after
        after = apply_camera(before)
        if after != before:
            write(PREFAB, after)
            print(f"stage 5: the studio's chase camera wired into {os.path.relpath(PREFAB, ROOT)}")
        for path, text in dipole_files().items():
            old = None
            if os.path.exists(path):
                with open(path, encoding="utf-8", newline="") as f:
                    old = f.read().replace("\r\n", "\n")
            if path in seeded_assets() and old is not None:
                continue   # a tuning asset: seeded once, its numbers are the Vessel Studio window's
            if old != text:
                write(path, text)
                print(f"stage 4: wrote {os.path.relpath(path, ROOT)}")
        if not os.path.exists(CLASS_ASSET):
            write(CLASS_ASSET, class_asset())
            write(CLASS_ASSET + ".meta", class_meta())
            print(f"stage 3: wrote {os.path.relpath(CLASS_ASSET, ROOT)}")
        for cl in CLASS_LISTS:
            with open(cl, encoding="utf-8", newline="") as f:
                before = f.read().replace("\r\n", "\n")
            after = register_class_list(before)
            if after != before:
                write(cl, after)
                print(f"stage 3: registered SO_Class_Stoat in {os.path.relpath(cl, ROOT)}")
        for path, fn in ((CONTAINER, register_container), (NETWORK_PREFABS, register_network)):
            with open(path, encoding="utf-8", newline="") as f:
                before = f.read().replace("\r\n", "\n")
            after = fn(before)
            if after != before:
                write(path, after)
                print(f"registered Stoat.prefab in {os.path.relpath(path, ROOT)}")
    errors = check(read_all())
    for e in errors:
        print(f"FAIL: {e}")
    print("author_stoat_assets: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
