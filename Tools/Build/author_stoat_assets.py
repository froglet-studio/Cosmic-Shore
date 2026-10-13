#!/usr/bin/env python3
"""Author the Stoat's prefab from the Squirrel's and register it, then prove the shipped set.

    python3 Tools/Build/author_stoat_assets.py             # author what is missing, then check
    python3 Tools/Build/author_stoat_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_stoat_assets.py --self-test # prove every check fires

WHAT IT MAKES (`R_VesselActions/STOAT.md`)
    Assets/_Prefabs/Spacevessels/Stoat.prefab (+ .meta)   a text clone of Squirrel.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 15` (VesselClassType.Stoat)
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

    STAGE 5: the root's uniform scale (HULL_SCALE).
    STAGE 6 (idempotent, runs on the committed prefab; 2026-10-13, `STOAT_DIPOLE.md` 1a-1e): the Stoat
    stops being a Squirrel clone. Removed: the Squirrel FBX (its hit box re-authored as the Stoat's own
    StoatBody / StoatHitbox sized to the hull; two of its four jets re-homed onto the hull's hips without
    the Squirrel's sideways DriftJet, two dropped), SquirrelHUDVariant + its hint roots +
    SquirrelVesselHUDController, SquirrelVesselTelemetry (-> DefaultVesselTelemetry), the DriftTrail /
    SquirrelTube executors, the SquirrelShard pool, ToggleAlignAction, the Squirrel-gated drift / proximity
    audio, the Squirrel drift switches, the skimmer's stale overrides and its Space scaling. Added: the
    Stoat's own HUD (StoatHUDVariant, copied once from the Thresher's: StoatHUDView + four icons + four
    gauges; StoatHUDController), camera (StoatCameraSettingsSO), impactor containers (Stoat* - no Squirrel
    element gates), AI boost policy, the mode switch (StoatModeExecutor + StoatModeAction on B) and the
    Charge ripple (StoatRippleExecutor + StoatRippleConfig), and the omni crystal's Overclock. Bindings:
    each trigger carries its dipole AND its orbit asset (the mode decides), B the switch.

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

STOAT_CLASS_ID = 15
PREFAB_GUID = "599b396ff054448db5bccc9307dfb4c9"
ROOT_GO = "6417075533431866457"              # the hull's root GameObject (same in every Squirrel-derived prefab)
NETWORK_OBJECT_ID = "1733035045152327888"    # the NetworkObject component on it
SHIP_ACTIONS_GO = "7850473780452578400"
REGISTRY_ID = "9105512684531825410"
SLING_EXECUTOR_ID = "5137264980012345601"
STOP_EXECUTOR_ID = "5137264980012345602"
ROOT_TRANSFORM_ID = "5928382658856804067"
# The whole hull's size (2026-10-13 play feedback: "a bit smaller") — the root Transform's uniform scale, so
# the drawn hull, its colliders and skimmers shrink together. WarpFieldVesselScaler reads it as the rest scale.
HULL_SCALE = 0.8
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
CAMERA_GUID = "2fb2a0bb0eaf4bd1863fdf55490fa869"   # = CAMERA_SETTINGS (stage 6); the seeded studio camera
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
    """The SHIPPED bindings (stage 6): each trigger carries BOTH its dipole and its orbit asset - the mode
    switch decides which one acts (StoatModeExecutor; each executor stands aside outside its mode) - X the
    hold, B the Mass / Time switch."""
    return (
        "  _touchActionOverrides: []\n"
        "  _gamepadActionOverrides:\n"
        "  - InputEvent: 2\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {DIPOLE_LEFT}, type: 2}}\n"
        f"    - {{fileID: 11400000, guid: {SLING_LEFT}, type: 2}}\n"
        "  - InputEvent: 1\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {DIPOLE_RIGHT}, type: 2}}\n"
        f"    - {{fileID: 11400000, guid: {SLING_RIGHT}, type: 2}}\n"
        "  - InputEvent: 6\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {HOLD}, type: 2}}\n"
        "  - InputEvent: 7\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {MODE_ACTION}, type: 2}}\n"
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
    """The StoatHull GameObject: Transform under the root (the two jets ride it - stage 6), MeshFilter
    (the builder fills it at Awake), MeshRenderer with the body + domain materials, and the
    StoatHullBuilder (its legacy model gone since stage 6). Proportions are NOT serialized here - the builder's field
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
        "  m_Children:\n" + "".join(f"  - {{fileID: {tr}}}\n" for _, tr, _ in JETS_KEPT) +
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
        # Stage 6 removed the Squirrel model this hid: nothing is hidden any more (the builder takes null).
        "  legacyModelRoot: {fileID: 0}\n"
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
    picked; its card icons are renders of its own hull (render_stoat_card_icon.py)."""
    return (
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SO_VESSEL_SCRIPT}, type: 3}}\n"
        "  m_Name: SO_Class_Stoat\n  m_EditorClassIdentifier: Assembly-CSharp::SO_Vessel\n"
        f"  Class: {STOAT_CLASS_ID}\n  Name: Stoat\n"
        "  Description: Never flies straight for long. In Mass mode the triggers lay a wormhole\n"
        "    dipole ahead - a sink and a source you fly through - and a thick trail behind; in\n"
        "    Time mode they lay an attractor to orbit and sling off a quarter-turn round. Every\n"
        "    pair that dies throws a space-time ripple that strips rivals.\n"
        "  PrimaryElement: 3\n  Element: {fileID: 0}\n"
        "  InitialResourceLevels:\n    Mass: 0\n    Charge: 0\n    Space: 0\n    Time: 0\n"
        f"  IconActive: {{fileID: 21300000, guid: {CARD_ICON_ACTIVE}, type: 3}}\n"
        f"  IconInactive: {{fileID: 21300000, guid: {CARD_ICON_INACTIVE}, type: 3}}\n"
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
    # `VesselPrefabContainer._shipPrefabs` is a `Transform[]`, so the entry names the root's
    # TRANSFORM, as every other hull's does (Dolphin, Sparrow, Butterfly...). The network list
    # below takes the GameObject. Until 2026-10-13 this wrote ROOT_GO here too.
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
    """StoatDipoleConfig: the studio's round-15 row as retuned in play on 2026-10-13 (StoatDipoleConfigSO's initializers)."""
    return (so_header(DIPOLE_CONFIG_SCRIPT, "StoatDipoleConfig") +
            "  holdExponent: 1.5\n"
            "  holdRampSeconds: 1.5\n"
            "  keySqueeze: 0.5\n"   # the studio's dpKeySqueeze
            "  aheadDistance: 250\n"
            "  sidewaysMax: 200\n"
            "  lengthwaysMax: 120\n"
            "  followRate: 6\n"
            "  poleGM: 50000\n"
            "  poleHorizon: 2.1\n"
            "  sinkSofteningHorizons: 14\n"
            "  poleSize:\n" + elemental(1, 1, 2, 3, 1) +
            "  sourcePush: 1\n"
            "  sourceSofteningHorizons: 2\n"
            "  accelerationCap: 3000\n"
            "  annihilateSeconds: 0.3\n"
            "  sinkGrowSeconds: 0.9\n"
            "  sourceGrowSeconds: 1.1\n"
            "  dieSeconds: 0.7\n"
            "  sourceSizeX: 1\n"
            "  throughZoneRadius: 135\n"
            "  throughDegrees: 200\n"
            "  turnCap: 12\n"
            "  grip: 0.5\n"
            "  gravitySpeedCeilingCruises: 4\n"
            "  gravityFadeSeconds: 2\n"
            "  domainTintAmount: 0\n"   # the studio draws no tint: a black shadow, a white core
            "  crystalStripShare: 1\n"
            "  faunaSwallowHorizons: 1.5\n"
            "  accretionPerPrism: 0.04\n"
            "  accretionMax: 1\n"
            "  pathLength: 1200\n"
            "  pathStep: 3\n"
            "  pathNoseOffset: 8\n"
            "  loopMargin: 8\n"
            "  minLoop: 60\n"
            "  warpDegrees: 3\n"
            "  boost:\n" + elemental(2.5, 2.5, 5, 4, 1) +
            "  boostRise: 20\n"
            "  boostFadeSeconds: 0.6\n"
            "  dotPixels: 4\n"
            "  dotGap: 2.5\n"
            "  dotDepthMinScale: 0.75\n"
            "  dotDepthFade: 0.15\n"
            "  dotsInWorld: 1\n"          # the path in the scene, denser than the studio's 2D line (the user, 2026-10-10)
            "  worldDotSize: 0.7\n"
            "  worldDotSpacing: 3\n"
            "  worldDotMinPixels: 2.5\n"
            f"  openColor: {color(150 / 255, 166 / 255, 194 / 255)}\n"
            f"  warpedColor: {color(157 / 255, 1, 46 / 255)}\n"
            "  foresightLength: 500\n"
            "  foresightRange: 1500\n"
            "  foresightMaxRivals: 3\n"
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


def root_scale_line() -> str:
    return f"  m_LocalScale: {{x: {num(HULL_SCALE)}, y: {num(HULL_SCALE)}, z: {num(HULL_SCALE)}}}\n"


def apply_scale(text: str) -> str:
    """The root Transform's uniform scale = HULL_SCALE. Idempotent; re-checks its anchor."""
    m = re.search(r"(--- !u!4 &%s\n(?:.*\n)*?)  m_LocalScale: \{[^}]*\}\n" % ROOT_TRANSFORM_ID, text)
    if not m:
        raise RuntimeError("Stoat.prefab drifted: the root Transform's m_LocalScale not found")
    return text[:m.end(1)] + root_scale_line() + text[m.end():]


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
    ARTIFACT, the one source of truth: /artifact-to-unity writes them (Tools/Build/studio_to_unity.py, map
    Tools/Build/studio_to_unity/stoat.json); asserting them here too would make two owners."""
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


# ----------------------------------------------------------------------------- stage 6: its own vessel
# 2026-10-13 (R_VesselActions/STOAT_DIPOLE.md section 1a-1e, STOAT.md section 4): the Stoat stops being a
# Squirrel clone. Everything below runs on the COMMITTED prefab and is idempotent (re-applying it to its
# own output changes nothing); the one-shot copies it makes from donors (the HUD variant from the
# Thresher's, the AI boost policy from the Squirrel's) stand down once the copy exists.
MODE_EXECUTOR_ID = "5137264980012345605"
RIPPLE_EXECUTOR_ID = "5137264980012345606"
HUD_INSTANCE_ID = "5137264980012345620"
HUD_RECT_ID = "5137264980012345621"
HUD_VIEW_ID = "5137264980012345622"
HUD_BARS_ID = "5137264980012345623"
BODY_GO_ID = "5137264980012345630"           # the non-trigger hit box + ImpactCollider (layer Default, as the FBX mesh's was)
BODY_TRANSFORM_ID = "5137264980012345631"
BODY_COLLIDER_ID = "5137264980012345632"
BODY_IMPACT_ID = "5137264980012345633"
TRIGGER_GO_ID = "5137264980012345634"        # the trigger volume (layer Ships, as the FBX's trigger GO was)
TRIGGER_TRANSFORM_ID = "5137264980012345635"
TRIGGER_COLLIDER_ID = "5137264980012345636"

HUD_CONTROLLER_ID = "5649393235309590662"    # the root's VesselHUDController (the Squirrel's, swapped in place)
HUD_HOLDER_RECT = "3410670963481274219"      # the root child the HUD instance hangs under
BARS_CONTROLLER_ID = "6013884537839217096"   # ElementalBarsController
CAMERA_CUSTOMIZER_ID = "6989089190155014109"
TELEMETRY_ID = "6248787816825736170"
VESSEL_IMPACTOR_ID = "2643412773408452782"
AI_PILOT_ID = "3440187540988767646"
SQUIRREL_HUD_INSTANCE = "5152493367006660778"
SQUIRREL_FBX_INSTANCE = "8410834229753696920"
SQUIRREL_ROOT_AUDIO = ("5363059078414614678", "7219854176267508625")   # DriftAudio / ProximityBoostAudio (class-gated to the Squirrel)
SQUIRREL_EXECUTOR_GOS = ("801949885325403072", "7788112233445566778")  # DriftTrailActionExecutor, SquirrelTubeActionExecutor
SQUIRREL_POOL_COMPONENT = "1907784425893164354"                         # NudgeShardPoolManager (SquirrelShard pool; nothing reads it)
SQUIRREL_ALIGN_COMPONENT = "1244531863877960553"                        # ToggleAlignAction (inactive)
JETS_KEPT = (("3003162917154485572", "2617076957950753345", -0.3), ("3287304896823774770", "2333430861380747575", 0.3))
JETS_DROPPED = ("4407056506752009302", "6084043296942237368")
# The jets ride the hull (they lope with it): at the hips, pointing back. Plume dial, VESSEL_TAIL_AND_JETS.md 3:
# (0.6, 0.6, 0.13) x |followOffset.z| / 20 = x0.85 at the Stoat's 17, divided back through the root's 0.8.
JET_LOCAL_Y, JET_LOCAL_Z = -0.05, -2.15
JET_SCALE = (0.6375, 0.6375, 0.138)
# The hit box: the hull's own extent (StoatHullForm at fleet scale: x +-0.45, y -0.66..0.82, z -4.79..3.17),
# nose to tail root, a little generous across - not the Squirrel's 4 u slab it replaces.
BODY_BOX_CENTER = (0, 0.08, -0.35)
BODY_BOX_SIZE = (1.2, 1.4, 7.0)
TRIGGER_BOX_SIZE = (1.6, 1.8, 7.6)

STAGE6_SCRIPTS = (   # (path, guid): every script stage 6 added, its .meta authored here
    (os.path.join(VESSEL_ACTIONS_SRC, "Executors", "StoatModeExecutor.cs"), "064761a8ffa04919a1308c63fe4b0732"),
    (os.path.join(VESSEL_ACTIONS_SRC, "Data Containers", "StoatModeActionSO.cs"), "4a205d398502473897ba2c04e8c4955e"),
    (os.path.join(VESSEL_ACTIONS_SRC, "Data Containers", "StoatRippleConfigSO.cs"), "6978f1c58d924a5a958ebf74c45bb1ac"),
    (os.path.join(VESSEL_ACTIONS_SRC, "StoatRippleMath.cs"), "f439b9cdec9840a2a5e3daecc634c215"),
    (os.path.join(VESSEL_ACTIONS_SRC, "StoatRipple.cs"), "19deb7d9b7344fbda936d6bc9e12c397"),
    (os.path.join(VESSEL_ACTIONS_SRC, "Executors", "StoatRippleExecutor.cs"), "1712753ee2d34c6294526e3c58d4b197"),
    (os.path.join(SCRIPTS, "Controller", "ImpactEffects", "EffectsSO", "Vessel Crystal Effects", "StoatOverclockByCrystalEffectSO.cs"),
     "104bc656c49e4ee099c38a569d907f62"),
    (os.path.join(SCRIPTS, "Tests", "Editor", "StoatRippleTests.cs"), "4a85b09c6e86465e82bc5c2d0f61fd2a"),
)
MODE_EXECUTOR_SCRIPT = STAGE6_SCRIPTS[0][1]
MODE_ACTION_SCRIPT = STAGE6_SCRIPTS[1][1]
RIPPLE_CONFIG_SCRIPT = STAGE6_SCRIPTS[2][1]
RIPPLE_EXECUTOR_SCRIPT = STAGE6_SCRIPTS[5][1]
OVERCLOCK_SCRIPT = STAGE6_SCRIPTS[6][1]
HUD_VIEW_SCRIPT = "c120940f60a14c2aa390446ea0791b5a"         # StoatHUDView
HUD_CONTROLLER_SCRIPT = "86da9d91d4364f02ad41dd723756e970"   # StoatHUDController
DEFAULT_TELEMETRY_SCRIPT = "eb7202b46b41476b97f51f71544f83f5"
IMPACT_COLLIDER_SCRIPT = "fafcc767febd41ccbad67c5457dc432d"
BARS_VIEW_SCRIPT = "5f380cf9710e6ec478121956993a387e"

MODE_ACTION = "f05e7ded817a4670bb78832c9c879566"
RIPPLE_CONFIG = "da077e9619f845c388ad5b7baf8da948"
OVERCLOCK_EFFECT = "6b508a166e124e95a6490871697185de"
CAMERA_SETTINGS = "2fb2a0bb0eaf4bd1863fdf55490fa869"
VESSEL_CONTAINER = "63a1b912028e471b9b7a2df570e90be8"
SKIMMER_CONTAINER = "ffdd072d412946c6ad6adf899a6ef7a6"
AI_BOOST_POLICY = "c691910cb0114ea7aeb572bc51b71a6a"
HUD_VARIANT = "1f9b41e6fa58494599674753191b495b"
CARD_ICON_ACTIVE = "627c06caaa88435fa2c44e98ad1fb611"      # render_stoat_card_icon.py
CARD_ICON_INACTIVE = "d845d4e49ee148f9b787599a3755eaa9"
SQUIRREL_CAMERA = "3c3fc507fffc4f5e86ab043ee7543cd2"
SQUIRREL_VESSEL_CONTAINER = "769c65db9958c064485be49a2183f263"
SQUIRREL_SKIMMER_CONTAINER = "643af834f1bcd9946903ac2c71d6d8ff"
SQUIRREL_AI_POLICY = "fc35731722bc45a7927d7f5eebc10ae2"
THRESHER_HUD_VARIANT = "6b2f9e1c3a7d4e8f9a0b1c2d3e4f5a6b"
VESSEL_HUD_PREFAB = "bc09c07dcb0724842a327c887609f156"

SO_ROOT = os.path.join(ROOT, "Assets", "_SO_Assets")
MODE_ACTION_PATH = os.path.join(ACTIONS, "StoatModeAction.asset")
RIPPLE_CONFIG_PATH = os.path.join(ACTIONS, "StoatRippleConfig.asset")
OVERCLOCK_PATH = os.path.join(SO_ROOT, "Effects", "Vessel Crystal Effects", "StoatOverclockByCrystalEffect.asset")
CAMERA_PATH = os.path.join(SO_ROOT, "Camera", "StoatCameraSettingsSO.asset")
VESSEL_CONTAINER_PATH = os.path.join(SO_ROOT, "Effects", "Effect Containers", "VesselContainers", "StoatImpactorDataContainer.asset")
SKIMMER_CONTAINER_PATH = os.path.join(SO_ROOT, "Effects", "Effect Containers", "SkimmerContainers", "StoatSkimmerImpactorDataContainer.asset")
AI_POLICY_PATH = os.path.join(SO_ROOT, "AI Boost Policies", "StoatAIBoostPolicy.asset")
SQUIRREL_AI_POLICY_PATH = os.path.join(SO_ROOT, "AI Boost Policies", "SquirrelAIBoostPolicy.asset")
HUD_DIR = os.path.join(ROOT, "Assets", "_Prefabs", "UI Elements", "VesselHUD")
HUD_VARIANT_PATH = os.path.join(HUD_DIR, "StoatHUDVariant.prefab")
THRESHER_HUD_PATH = os.path.join(HUD_DIR, "ThresherHUDVariant.prefab")
ICON_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons", "Stoat")


def stoat_icon_guid(name: str) -> str:
    """The ability icon's guid - author_stoat_icon_placeholders.py derives it the same way."""
    import hashlib
    return hashlib.md5(f"CosmicShore/StoatIcons/{name}".encode()).hexdigest()


# Thresher card art -> the Stoat's (the variant is copied from the Thresher's; same card order).
ICON_SWAP = (("372925148d56fc4971f260a7f5cdd470", "Stoat_Ripple.png"),        # Charge
             ("290bd445e0cc6921b2c8e07e2e99edf2", "Stoat_Modes.png"),         # Mass
             ("91ad84c1b3d7d9508b8aa3323cfb54fe", "Stoat_FieldDipole.png"),   # Space
             ("0cd003be08a8ace88fb858daf3916010", "Stoat_Pathfinder.png"))    # Time


# --- the document model: a prefab is a header plus fileID-keyed documents, in order ------------
DOC_RE = re.compile(r"--- !u!(\d+) &(-?\d+)( stripped)?\n")


def split_docs(text: str):
    starts = [m for m in DOC_RE.finditer(text)]
    head = text[:starts[0].start()] if starts else text
    out = []
    for i, m in enumerate(starts):
        end = starts[i + 1].start() if i + 1 < len(starts) else len(text)
        out.append([m.group(2), text[m.start():end]])
    return head, out


def join_docs(head, docs) -> str:
    return head + "".join(chunk for _, chunk in docs)


def doc_index(docs, fid):
    for i, (k, _) in enumerate(docs):
        if k == fid:
            return i
    return -1


def get_doc(docs, fid):
    i = doc_index(docs, fid)
    return docs[i][1] if i >= 0 else None


def set_doc(docs, fid, chunk):
    i = doc_index(docs, fid)
    if i < 0:
        raise RuntimeError(f"Stoat.prefab drifted: document {fid} not found")
    docs[i][1] = chunk


def remove_docs(docs, fids):
    keep = set(fids)
    docs[:] = [d for d in docs if d[0] not in keep]


def refs(chunk):
    return re.findall(r"\{fileID: (-?\d+)\}", chunk)


def remove_child(docs, transform_fid, child_fid):
    """Drop `child_fid` from a Transform/RectTransform's m_Children."""
    chunk = get_doc(docs, transform_fid)
    if chunk is None:
        return
    set_doc(docs, transform_fid, chunk.replace(f"  - {{fileID: {child_fid}}}\n", "", 1)
            .replace("  m_Children:\n  m_Father", "  m_Children: []\n  m_Father"))


def add_child(docs, transform_fid, child_fid):
    chunk = get_doc(docs, transform_fid)
    if f"  - {{fileID: {child_fid}}}\n" in chunk.split("  m_Father")[0]:
        return
    if "  m_Children: []\n" in chunk:
        chunk = chunk.replace("  m_Children: []\n", f"  m_Children:\n  - {{fileID: {child_fid}}}\n", 1)
    else:
        m = re.search(r"  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", chunk)
        chunk = chunk[:m.end(1)] + f"  - {{fileID: {child_fid}}}\n" + chunk[m.end(1):]
    set_doc(docs, transform_fid, chunk)


def go_of(chunk):
    m = re.search(r"  m_GameObject: \{fileID: (-?\d+)\}", chunk)
    return m.group(1) if m else None


def remove_gameobject_tree(docs, go_fid):
    """A GameObject, every component on it, every child below it (nested instances included), and its
    entry in its father's m_Children."""
    go = get_doc(docs, go_fid)
    if go is None:
        return
    comps = re.findall(r"  - component: \{fileID: (-?\d+)\}", go)
    doomed = {go_fid, *comps}
    for c in comps:
        chunk = get_doc(docs, c)
        if chunk is None or not re.match(r"--- !u!(4|224) ", chunk):
            continue
        father = re.search(r"  m_Father: \{fileID: (-?\d+)\}", chunk)
        if father and father.group(1) != "0":
            remove_child(docs, father.group(1), c)
        children = re.search(r"  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", chunk)
        for child in re.findall(r"-?\d+", children.group(1) if children else ""):
            cchunk = get_doc(docs, child)
            if cchunk is None:
                continue
            inst = re.search(r"  m_PrefabInstance: \{fileID: (-?\d+)\}", cchunk)
            if " stripped\n" in cchunk.split("\n", 1)[0] + "\n" and inst:
                remove_instance(docs, inst.group(1))
            else:
                remove_gameobject_tree(docs, go_of(cchunk))
    remove_docs(docs, doomed)


def remove_instance(docs, inst_fid):
    """A nested prefab instance: its PrefabInstance document, every stripped document it owns, every
    object ADDED to it (m_AddedGameObjects / m_AddedComponents), and its root's entry in its parent."""
    chunk = get_doc(docs, inst_fid)
    if chunk is None:
        return
    added = re.findall(r"addedObject: \{fileID: (-?\d+)\}", chunk)
    owned = [k for k, c in docs if f"  m_PrefabInstance: {{fileID: {inst_fid}}}\n" in c and k != inst_fid]
    parent = re.search(r"m_TransformParent: \{fileID: (-?\d+)\}", chunk).group(1)
    for k in owned:
        if parent != "0":
            remove_child(docs, parent, k)
    for a in added:
        achunk = get_doc(docs, a)
        if achunk is None:
            continue
        if re.match(r"--- !u!(4|224) ", achunk):
            inst = re.search(r"  m_PrefabInstance: \{fileID: (-?\d+)\}", achunk)
            if inst and inst.group(1) != "0":
                remove_instance(docs, inst.group(1))
            else:
                remove_gameobject_tree(docs, go_of(achunk))
        else:
            remove_docs(docs, [a])
    remove_docs(docs, owned + [inst_fid])


def dangling_refs(text: str):
    """Every local {fileID: N} (N != 0) that names no document in the file - the integrity gate for
    stage 6's removals (a dangling local reference is a Missing in the inspector and a null at runtime)."""
    head, docs = split_docs(text)
    ids = {k for k, _ in docs}
    out = set()
    for k, chunk in docs:
        for line in chunk.split("\n"):
            if "guid:" in line:
                continue
            for r in re.findall(r"\{fileID: (-?\d+)\}", line):
                if r != "0" and r not in ids:
                    out.add((k, r))
    return sorted(out)


def mb_header(fid, go, script, ident="") -> str:
    return (f"--- !u!114 &{fid}\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {go}}}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n"
            "  m_Name: \n"
            f"  m_EditorClassIdentifier: {ident}\n")


def stage6_executor_blocks() -> str:
    return (mb_header(MODE_EXECUTOR_ID, SHIP_ACTIONS_GO, MODE_EXECUTOR_SCRIPT) +
            f"  config: {{fileID: 11400000, guid: {MODE_ACTION}, type: 2}}\n" +
            mb_header(RIPPLE_EXECUTOR_ID, SHIP_ACTIONS_GO, RIPPLE_EXECUTOR_SCRIPT) +
            f"  config: {{fileID: 11400000, guid: {RIPPLE_CONFIG}, type: 2}}\n")


def hud_controller_block() -> str:
    return (mb_header(HUD_CONTROLLER_ID, ROOT_GO, HUD_CONTROLLER_SCRIPT, "Assembly-CSharp::CosmicShore.UI.StoatHUDController") +
            f"  baseView: {{fileID: {HUD_VIEW_ID}}}\n"
            "  _iconSetSwitcher: {fileID: 0}\n"
            f"  view: {{fileID: {HUD_VIEW_ID}}}\n"
            f"  mode: {{fileID: {MODE_EXECUTOR_ID}}}\n"
            f"  dipole: {{fileID: {DIPOLE_EXECUTOR_ID}}}\n"
            f"  sling: {{fileID: {SLING_EXECUTOR_ID}}}\n"
            f"  pathfinder: {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n")


def telemetry_block() -> str:
    """DefaultVesselTelemetry with the generic stats (the Thresher's shape) - the Squirrel's telemetry counted
    jousts, steals and clean crystal streaks the Stoat has no way to make."""
    return (mb_header(TELEMETRY_ID, ROOT_GO, DEFAULT_TELEMETRY_SCRIPT) +
            "  longestDriftStat: {fileID: 11400000, guid: c9ca3d6eff04d70468aa24ee5b1f561d, type: 2}\n"
            "  maxBoostTimeStat: {fileID: 11400000, guid: be1a9b6401dca924585cd31ff1034c6f, type: 2}\n"
            "  prismsDamagedStat: {fileID: 11400000, guid: 3ea71b2eaf0cdbb46a99ddaf04b2d999, type: 2}\n"
            "  boostMultiplierThreshold: 1.5\n")


def hud_instance_blocks() -> str:
    mods = ["      propertyPath: m_Name\n      value: StoatHUDVariant\n      objectReference: {fileID: 0}\n"]
    target_go = f"    - target: {{fileID: 1395415625850085981, guid: {HUD_VARIANT},\n        type: 3}}\n"
    target_rt = f"    - target: {{fileID: 7475086108544464464, guid: {HUD_VARIANT},\n        type: 3}}\n"
    rect = [("m_Pivot.x", "0.5"), ("m_Pivot.y", "0.5"), ("m_AnchorMax.x", "0.5"), ("m_AnchorMax.y", "0.5"),
            ("m_AnchorMin.x", "0.5"), ("m_AnchorMin.y", "0.5"), ("m_SizeDelta.x", "100"), ("m_SizeDelta.y", "100"),
            ("m_LocalPosition.x", "0"), ("m_LocalPosition.y", "0"), ("m_LocalPosition.z", "0"),
            ("m_LocalRotation.w", "1"), ("m_LocalRotation.x", "0"), ("m_LocalRotation.y", "0"), ("m_LocalRotation.z", "0"),
            ("m_AnchoredPosition.x", "0"), ("m_AnchoredPosition.y", "0"),
            ("m_LocalEulerAnglesHint.x", "0"), ("m_LocalEulerAnglesHint.y", "0"), ("m_LocalEulerAnglesHint.z", "0")]
    body = target_go + mods[0]
    for path, value in rect:
        body += target_rt + f"      propertyPath: {path}\n      value: {value}\n      objectReference: {{fileID: 0}}\n"
    return (f"--- !u!1001 &{HUD_INSTANCE_ID}\n"
            "PrefabInstance:\n"
            "  m_ObjectHideFlags: 0\n"
            "  serializedVersion: 2\n"
            "  m_Modification:\n"
            "    serializedVersion: 3\n"
            f"    m_TransformParent: {{fileID: {HUD_HOLDER_RECT}}}\n"
            "    m_Modifications:\n" + body +
            "    m_RemovedComponents: []\n"
            "    m_RemovedGameObjects: []\n"
            "    m_AddedGameObjects: []\n"
            "    m_AddedComponents: []\n"
            f"  m_SourcePrefab: {{fileID: 100100000, guid: {HUD_VARIANT}, type: 3}}\n"
            f"--- !u!224 &{HUD_RECT_ID} stripped\n"
            "RectTransform:\n"
            f"  m_CorrespondingSourceObject: {{fileID: 7475086108544464464, guid: {HUD_VARIANT},\n    type: 3}}\n"
            f"  m_PrefabInstance: {{fileID: {HUD_INSTANCE_ID}}}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            f"--- !u!114 &{HUD_VIEW_ID} stripped\n"
            "MonoBehaviour:\n"
            f"  m_CorrespondingSourceObject: {{fileID: 5875684227236902712, guid: {HUD_VARIANT},\n    type: 3}}\n"
            f"  m_PrefabInstance: {{fileID: {HUD_INSTANCE_ID}}}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {HUD_VIEW_SCRIPT}, type: 3}}\n"
            "  m_Name: \n"
            "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.StoatHUDView\n"
            f"--- !u!114 &{HUD_BARS_ID} stripped\n"
            "MonoBehaviour:\n"
            f"  m_CorrespondingSourceObject: {{fileID: 3959346466051048752, guid: {HUD_VARIANT},\n    type: 3}}\n"
            f"  m_PrefabInstance: {{fileID: {HUD_INSTANCE_ID}}}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {BARS_VIEW_SCRIPT}, type: 3}}\n"
            "  m_Name: \n"
            "  m_EditorClassIdentifier: \n")


def v3(x, y, z) -> str:
    return f"{{x: {num(x)}, y: {num(y)}, z: {num(z)}}}"


def collider_blocks() -> str:
    """The Stoat's own hit box (the Squirrel FBX it hid carried the old one): a non-trigger box + ImpactCollider
    on Default, a slightly larger trigger box on Ships - the two shapes the FBX's two GameObjects carried."""
    def go(fid, tr, comps, layer, name):
        return (f"--- !u!1 &{fid}\n"
                "GameObject:\n"
                "  m_ObjectHideFlags: 0\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                "  serializedVersion: 6\n"
                "  m_Component:\n" + "".join(f"  - component: {{fileID: {c}}}\n" for c in [tr] + comps) +
                f"  m_Layer: {layer}\n"
                f"  m_Name: {name}\n"
                "  m_TagString: Untagged\n"
                "  m_Icon: {fileID: 0}\n"
                "  m_NavMeshLayer: 0\n"
                "  m_StaticEditorFlags: 0\n"
                "  m_IsActive: 1\n")

    def transform(fid, gofid):
        return (f"--- !u!4 &{fid}\n"
                "Transform:\n"
                "  m_ObjectHideFlags: 0\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                f"  m_GameObject: {{fileID: {gofid}}}\n"
                "  serializedVersion: 2\n"
                "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
                "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
                "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
                "  m_ConstrainProportionsScale: 0\n"
                "  m_Children: []\n"
                f"  m_Father: {{fileID: {ROOT_TRANSFORM_ID}}}\n"
                "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n")

    def box(fid, gofid, trigger, size):
        return (f"--- !u!65 &{fid}\n"
                "BoxCollider:\n"
                "  m_ObjectHideFlags: 0\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                f"  m_GameObject: {{fileID: {gofid}}}\n"
                "  m_Material: {fileID: 0}\n"
                "  m_IncludeLayers:\n"
                "    serializedVersion: 2\n"
                "    m_Bits: 0\n"
                "  m_ExcludeLayers:\n"
                "    serializedVersion: 2\n"
                "    m_Bits: 0\n"
                "  m_LayerOverridePriority: 0\n"
                f"  m_IsTrigger: {1 if trigger else 0}\n"
                "  m_ProvidesContacts: 0\n"
                "  m_Enabled: 1\n"
                "  serializedVersion: 3\n"
                f"  m_Size: {v3(*size)}\n"
                f"  m_Center: {v3(*BODY_BOX_CENTER)}\n")

    return (go(BODY_GO_ID, BODY_TRANSFORM_ID, [BODY_COLLIDER_ID, BODY_IMPACT_ID], 0, "StoatBody") +
            transform(BODY_TRANSFORM_ID, BODY_GO_ID) +
            box(BODY_COLLIDER_ID, BODY_GO_ID, False, BODY_BOX_SIZE) +
            mb_header(BODY_IMPACT_ID, BODY_GO_ID, IMPACT_COLLIDER_SCRIPT) +
            f"  impactorObject: {{fileID: {VESSEL_IMPACTOR_ID}}}\n" +
            go(TRIGGER_GO_ID, TRIGGER_TRANSFORM_ID, [TRIGGER_COLLIDER_ID], SHIPS_LAYER, "StoatHitbox") +
            transform(TRIGGER_TRANSFORM_ID, TRIGGER_GO_ID) +
            box(TRIGGER_COLLIDER_ID, TRIGGER_GO_ID, True, TRIGGER_BOX_SIZE))


def jet_mods(side: float) -> str:
    target = "    - target: {fileID: 1007919433217068805, guid: 55ceca573be662744948afc745f8840b,\n        type: 3}\n"
    rows = [("m_LocalScale.x", JET_SCALE[0]), ("m_LocalScale.y", JET_SCALE[1]), ("m_LocalScale.z", JET_SCALE[2]),
            ("m_LocalPosition.x", side), ("m_LocalPosition.y", JET_LOCAL_Y), ("m_LocalPosition.z", JET_LOCAL_Z),
            ("m_LocalRotation.w", 1), ("m_LocalRotation.x", 0), ("m_LocalRotation.y", 0), ("m_LocalRotation.z", 0),
            ("m_LocalEulerAnglesHint.x", 0), ("m_LocalEulerAnglesHint.y", 0), ("m_LocalEulerAnglesHint.z", 0)]
    out = "".join(target + f"      propertyPath: {k}\n      value: {num(v)}\n      objectReference: {{fileID: 0}}\n" for k, v in rows)
    return out + ("    - target: {fileID: 1007919433217068806, guid: 55ceca573be662744948afc745f8840b,\n        type: 3}\n"
                  "      propertyPath: m_Name\n      value: VesselJet\n      objectReference: {fileID: 0}\n")


def rehome_jet(docs, inst_fid, side):
    chunk = get_doc(docs, inst_fid)
    added = re.findall(r"addedObject: \{fileID: (-?\d+)\}", chunk)
    remove_docs(docs, added)   # DriftJet: steers the plume SIDEWAYS (the Squirrel's side jets) unless drifting
    chunk = re.sub(r"m_TransformParent: \{fileID: -?\d+\}", f"m_TransformParent: {{fileID: {HULL_TRANSFORM_ID}}}", chunk, count=1)
    chunk = re.sub(r"    m_Modifications:\n(?:.*\n)*?(?=    m_RemovedComponents:)", "    m_Modifications:\n" + jet_mods(side), chunk, count=1)
    chunk = re.sub(r"    m_AddedComponents:\n(?:    - .*\n(?:      .*\n|        .*\n)*)*", "    m_AddedComponents: []\n", chunk, count=1)
    set_doc(docs, inst_fid, chunk)


def set_field(chunk: str, key: str, value: str) -> str:
    """Replace a top-level `  key: ...` line (a following continuation line too) in one document."""
    m = re.search(r"^  %s: .*\n(?:    \S.*\n)?" % re.escape(key), chunk, re.M)
    if not m:
        raise RuntimeError(f"Stoat.prefab drifted: field {key} not found")
    return chunk[:m.start()] + f"  {key}: {value}\n" + chunk[m.end():]


def prune_skimmer_overrides(chunk: str) -> str:
    """Overrides whose field the Skimmer / SkimmerImpactor no longer serializes (Unity never prunes them), and the
    Squirrel's Space-scaled skimmer (the Stoat's one Space dial is its poles' size): a fixed 15."""
    stale = ("skimmerPrismStayEffectsSO", "vesselSkimmerEffectsSO", "NotifyNearbyBlockCount", "notifyNearbyBlockCount",
             "blockStayEffects", "shipImpactEffects", "blockImpactEffects", "trailBlockImpactEffects", "speedTubes",
             "markerPrefab", "fuelAmount", "skimVisualFX", "alignWithTrail")
    def keep(mod):
        path = re.search(r"propertyPath: '?([A-Za-z_]+)", mod).group(1)
        return path not in stale
    head, rest = chunk.split("    m_Modifications:\n", 1)
    # No re.S: with it, `.*\n` ran to the LAST newline, so `tail` came back empty and the instance lost
    # m_RemovedComponents..m_SourcePrefab (Stoat.prefab's skimmer, 2026-10-13). The tail is taken with [\s\S] instead.
    mods_text, tail = re.match(r"((?:    - target:.*\n(?:      .*\n|        .*\n)*)*)([\s\S]*)", rest).groups()
    mods = re.findall(r"    - target:.*\n(?:      .*\n|        .*\n)*", mods_text)
    mods = [m for m in mods if keep(m)]
    out = []
    for m in mods:
        if "propertyPath: Scale.Enabled\n" in m:
            m = m.replace("      value: 1\n", "      value: 0\n")
        if "propertyPath: AOEPrefab\n" in m:
            m = re.sub(r"      objectReference: \{fileID: -?\d+, guid: b189bdcdd34040c4e9631f49d995b800,\n        type: 3\}\n",
                       "      objectReference: {fileID: 0}\n", m)
        if SQUIRREL_SKIMMER_CONTAINER in m:
            m = m.replace(SQUIRREL_SKIMMER_CONTAINER, SKIMMER_CONTAINER)
        out.append(m)
    return head + "    m_Modifications:\n" + "".join(out) + tail


def apply_own_vessel(text: str) -> str:
    """Stage 6 on the committed prefab: the Stoat's own HUD, colliders, jets, camera, containers, telemetry and
    executors; every Squirrel leftover gone. Idempotent; raises on a drifted anchor."""
    head, docs = split_docs(text)

    # 1. Squirrel leftovers: executors, the shard pool, the align toggle, the class-gated audio, the HUD, the model.
    for go in SQUIRREL_EXECUTOR_GOS:
        if get_doc(docs, go) is not None:
            remove_gameobject_tree(docs, go)
    for comp in (SQUIRREL_POOL_COMPONENT, SQUIRREL_ALIGN_COMPONENT):
        chunk = get_doc(docs, comp)
        if chunk is not None:
            remove_gameobject_tree(docs, go_of(chunk))
    for comp in SQUIRREL_ROOT_AUDIO:
        if get_doc(docs, comp) is not None:
            remove_docs(docs, [comp])
            set_doc(docs, ROOT_GO, get_doc(docs, ROOT_GO).replace(f"  - component: {{fileID: {comp}}}\n", ""))
    if get_doc(docs, SQUIRREL_HUD_INSTANCE) is not None:
        remove_instance(docs, SQUIRREL_HUD_INSTANCE)
    if get_doc(docs, SQUIRREL_FBX_INSTANCE) is not None:
        for inst, _, side in JETS_KEPT:
            rehome_jet(docs, inst, side)
        for inst in JETS_DROPPED:
            remove_instance(docs, inst)
        # The kept jets' roots were ADDED to the model; detach them from it before the model goes.
        fbx = get_doc(docs, SQUIRREL_FBX_INSTANCE)
        for _, tr, _ in JETS_KEPT:
            fbx = re.sub(r"    - targetCorrespondingSourceObject: \{[^}]*\}\n      insertIndex: -1\n      addedObject: \{fileID: %s\}\n" % tr, "", fbx)
        set_doc(docs, SQUIRREL_FBX_INSTANCE, fbx)
        remove_instance(docs, SQUIRREL_FBX_INSTANCE)
        remove_child(docs, ROOT_TRANSFORM_ID, SQUIRREL_MODEL_ROOT)

    # 2. The hull block in its final form (the jets ride it; the builder hides nothing).
    start, end = doc_index(docs, HULL_GO_ID), doc_index(docs, HULL_BUILDER_ID)
    if start < 0 or end < 0:
        raise RuntimeError("Stoat.prefab drifted: the StoatHull documents not found")
    _, hull = split_docs(hull_blocks())
    docs[start:end + 1] = hull

    # 3. The Stoat's own colliders under the root.
    if get_doc(docs, BODY_GO_ID) is None:
        _, cols = split_docs(collider_blocks())
        at = doc_index(docs, HULL_BUILDER_ID) + 1
        docs[at:at] = cols
        add_child(docs, ROOT_TRANSFORM_ID, BODY_TRANSFORM_ID)
        add_child(docs, ROOT_TRANSFORM_ID, TRIGGER_TRANSFORM_ID)

    # 4. The HUD: the Stoat's variant under the same holder, its controller in place of the Squirrel's.
    if get_doc(docs, HUD_INSTANCE_ID) is None:
        _, hud = split_docs(hud_instance_blocks())
        at = doc_index(docs, HUD_HOLDER_RECT) + 1
        docs[at:at] = hud
        add_child(docs, HUD_HOLDER_RECT, HUD_RECT_ID)
    set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    set_doc(docs, BARS_CONTROLLER_ID, set_field(get_doc(docs, BARS_CONTROLLER_ID), "elementBars", f"{{fileID: {HUD_BARS_ID}}}"))
    set_doc(docs, TELEMETRY_ID, telemetry_block())

    # 5. Its own camera, containers and AI policy.
    set_doc(docs, CAMERA_CUSTOMIZER_ID, set_field(get_doc(docs, CAMERA_CUSTOMIZER_ID), "settings",
                                                  f"{{fileID: 11400000, guid: {CAMERA_SETTINGS}, type: 2}}"))
    set_doc(docs, VESSEL_IMPACTOR_ID, set_field(get_doc(docs, VESSEL_IMPACTOR_ID), "vesselImpactorDataContainerSO",
                                                f"{{fileID: 11400000, guid: {VESSEL_CONTAINER}, type: 2}}"))
    ai = get_doc(docs, AI_PILOT_ID)
    set_doc(docs, AI_PILOT_ID, set_field(ai, "boostPolicy", f"{{fileID: 11400000, guid: {AI_BOOST_POLICY}, type: 2}}"))
    for k, chunk in docs:
        if "m_SourcePrefab: {fileID: 100100000, guid: " in chunk and "m_Name\n      value: align skimmer" in chunk:
            set_doc(docs, k, prune_skimmer_overrides(chunk))

    # 6. The executors: the mode switch and the ripple.
    ship = get_doc(docs, SHIP_ACTIONS_GO)
    if f"  - component: {{fileID: {MODE_EXECUTOR_ID}}}\n" not in ship:
        ship = ship.replace(f"  - component: {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n",
                            f"  - component: {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n"
                            f"  - component: {{fileID: {MODE_EXECUTOR_ID}}}\n"
                            f"  - component: {{fileID: {RIPPLE_EXECUTOR_ID}}}\n", 1)
        set_doc(docs, SHIP_ACTIONS_GO, ship)
    reg = get_doc(docs, REGISTRY_ID)
    for gone in ("2317382454779808403", "7788112233445566780"):
        reg = reg.replace(f"  - {{fileID: {gone}}}\n", "")
    if f"  - {{fileID: {MODE_EXECUTOR_ID}}}\n" not in reg:
        reg = reg.replace(f"  - {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n",
                          f"  - {{fileID: {PATHFINDER_EXECUTOR_ID}}}\n  - {{fileID: {MODE_EXECUTOR_ID}}}\n"
                          f"  - {{fileID: {RIPPLE_EXECUTOR_ID}}}\n", 1)
    set_doc(docs, REGISTRY_ID, reg)
    if get_doc(docs, MODE_EXECUTOR_ID) is None:
        _, ex = split_docs(stage6_executor_blocks())
        at = doc_index(docs, PATHFINDER_EXECUTOR_ID) + 1
        docs[at:at] = ex

    # 7. Flight / trail switches the Squirrel's drift owned: inert on the Stoat, so off.
    tr = get_doc(docs, TRANSFORMER_ID)
    set_doc(docs, TRANSFORMER_ID, tr.replace("  singleTriggerDrift: 1\n", "  singleTriggerDrift: 0\n"))
    pc = find_component_id(join_docs(head, docs), PRISM_CONTROLLER_SCRIPT)
    set_doc(docs, pc, get_doc(docs, pc).replace("  driftShieldsTrail: 1\n", "  driftShieldsTrail: 0\n"))

    return join_docs(head, docs)


# --- the files stage 6 owns ---------------------------------------------------------------------
def mode_action_asset() -> str:
    return (so_header(MODE_ACTION_SCRIPT, "StoatModeAction") +
            "  massModeThickness:\n" + elemental(3, 3, 6, 2, 1) +
            "  blendSeconds: 0.8\n"
            "  massUpgradeShieldsInMassMode: 1\n"
            "  autopilotTimeModeDegrees: 70\n"
            "  autopilotModeIntervalSeconds: 3\n" +
            empty_event("massModeEvent") + empty_event("timeModeEvent"))


def ripple_config_asset() -> str:
    return (so_header(RIPPLE_CONFIG_SCRIPT, "StoatRippleConfig") +
            "  radiusPerSeparation: 0.6\n"
            "  minRadius: 30\n"
            "  maxRadius: 160\n"
            "  reach:\n" + elemental(1, 1, 2, 1, 1) +
            "  expandSeconds: 0.9\n"
            "  fadeSeconds: 0.5\n"
            "  bandWidth: 0.12\n"
            "  bend: 0.025\n"
            "  chromaticSplit: 0.12\n"
            "  rimGlow: 1.6\n"
            "  petalsPerElement: 1\n"
            "  shedSpeed: 8\n"
            "  siphonOnChargeUpgrade: 1\n"
            "  killsFauna: 1\n" +
            empty_event("rippleEvent") + empty_event("stripEvent"))


def overclock_asset() -> str:
    return so_header(OVERCLOCK_SCRIPT, "StoatOverclockByCrystalEffect") + "  seconds: 6\n"


def vessel_container_asset() -> str:
    """The Squirrel's prism effects (the fleet's slow / damage / danger debuff / haptics - no element gates),
    and for crystals the haptics plus the Stoat's OVERCLOCK. Dropped: the Squirrel's omni explosion (Space-5
    gated boost ring), its crystal morph (which waits for that ring) and its Crystal Joust (Space-5 gated)."""
    g = lambda guid: f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
    return (so_header("fde7d8c2dacf45f99e46799ffeea15e6", "StoatImpactorDataContainer") +
            "  vesselPrismEffects:\n" + g("243d6d82ba7f20146a70bdc112b9a278") + g("4da62d4eefe03594ba35027dd537aff0") +
            g("04f85a1f6b06b9248827bf8740168b2b") + g("6c233c580949b13419f068454c74a110") +
            g("c7ccaca885824b24b716b12148d77ce1") + g("ca2d1be3880b82941a0465a1f7e45280") +
            "  vesselCrystalEffects:\n" + g("8de915fb00503344ca89a92a0a127b00") + g(OVERCLOCK_EFFECT) +
            "  vesselMassCrystalEffects: []\n  vesselChargeCrystalEffects: []\n  vesselSpaceCrystalEffects: []\n"
            "  vesselTimeCrystalEffects: []\n  vesselSkimmerEffects: []\n  vesselLifeformCrystalEffects: []\n"
            "  omniCrystalRetirement: {fileID: 0}\n")


def skimmer_container_asset() -> str:
    """The joust hit and the skim explosion (no element gates), and the skim haptics + forcefield crackle.
    Dropped: the Squirrel's overtake, its boost-meter skim (Time-scaled, Charge-5 gated) and its steal
    (Space-5 gated) - each would have given the Stoat a second, undocumented element dial or level 5."""
    g = lambda guid: f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
    return (so_header("68b3e74284f14e32a6e21b5fb8c3e8ad", "StoatSkimmerImpactorDataContainer") +
            "  vesselSkimmerEffectsSO:\n" + g("c2570a8d81db3ab4eb0ba30f184e081f") + g("7e54b5536d0d1fbcda92e10ddf159bf6") +
            "  skimmerPrismEffectsSO:\n" + g("dcce08b9351b97c4eb275508001a5b8e") + g("5bea3c7d44b956064c78606bba3077b8") +
            "  skimmerCrystalEffectsSO: []\n")


def stage6_files() -> "dict[str, str]":
    """Every non-prefab file stage 6 owns outright, {absolute path: content}."""
    out = {
        MODE_ACTION_PATH: mode_action_asset(), MODE_ACTION_PATH + ".meta": asset_meta(MODE_ACTION),
        RIPPLE_CONFIG_PATH: ripple_config_asset(), RIPPLE_CONFIG_PATH + ".meta": asset_meta(RIPPLE_CONFIG),
        OVERCLOCK_PATH: overclock_asset(), OVERCLOCK_PATH + ".meta": asset_meta(OVERCLOCK_EFFECT),
        # CAMERA_PATH: not owned here - the studio's chase camera is a SEEDED tuning asset (seeded_assets, stage 4).
        VESSEL_CONTAINER_PATH: vessel_container_asset(), VESSEL_CONTAINER_PATH + ".meta": asset_meta(VESSEL_CONTAINER),
        SKIMMER_CONTAINER_PATH: skimmer_container_asset(), SKIMMER_CONTAINER_PATH + ".meta": asset_meta(SKIMMER_CONTAINER),
        AI_POLICY_PATH + ".meta": asset_meta(AI_BOOST_POLICY),
        HUD_VARIANT_PATH + ".meta": prefab_meta().replace(PREFAB_GUID, HUD_VARIANT),
    }
    for path, guid in STAGE6_SCRIPTS:
        out[path + ".meta"] = script_meta(guid)
    return out


def ai_policy_from_donor(donor: str) -> str:
    """One-shot copy of the Squirrel's ring-skim boost policy under the Stoat's name (stands down once it exists)."""
    return donor.replace("  m_Name: SquirrelAIBoostPolicy\n", "  m_Name: StoatAIBoostPolicy\n", 1)


def hud_variant_from_donor(donor: str) -> str:
    """One-shot copy of ThresherHUDVariant.prefab - a true variant of VesselHUDPrefab with authored petal bars and a
    four-card row on known fileIDs - as the Stoat's: its own view (StoatHUDView), its own four icons, a gauge on every
    card. Stands down once StoatHUDVariant.prefab exists; --check then validates the shipped file."""
    head, docs = split_docs(donor)
    text = join_docs(head, docs).replace("ThresherHUDVariant", "StoatHUDVariant")
    for old, name in ICON_SWAP:
        text = text.replace(old, stoat_icon_guid(name))
    head, docs = split_docs(text)
    view = get_doc(docs, "5875684227236902712")
    view = view.replace("guid: a322d66caa3346d2b8ad15b0b21f9efa", f"guid: {HUD_VIEW_SCRIPT}")
    view = view.replace("CosmicShore.UI.ThresherHUDView", "CosmicShore.UI.StoatHUDView")
    view = re.sub(r"  - element: 2\n    icon: \{fileID: 7700000000000001115\}\n    upgradedSprite: \{fileID: 0\}\n    gauge: \{fileID: 0\}\n",
                  "  - element: 2\n    icon: {fileID: 7700000000000001115}\n    upgradedSprite: {fileID: 0}\n    gauge: {fileID: 7700000000000001223}\n", view)
    view = re.sub(r"  - element: 4\n    icon: \{fileID: 7700000000000001135\}\n    upgradedSprite: \{fileID: 0\}\n    gauge: \{fileID: 0\}\n",
                  "  - element: 4\n    icon: {fileID: 7700000000000001135}\n    upgradedSprite: {fileID: 0}\n    gauge: {fileID: 7700000000000001233}\n", view)
    view = re.sub(r"  ballHeatGauge: .*\n(?:.*\n)*", "  rippleGauge: {fileID: 7700000000000001203}\n"
                  "  modeGauge: {fileID: 7700000000000001223}\n"
                  "  accretionGauge: {fileID: 7700000000000001213}\n"
                  "  timeGauge: {fileID: 7700000000000001233}\n"
                  f"  gaugeColor: {color(0.59, 0.65, 0.76)}\n"
                  f"  readyColor: {color(157 / 255, 1, 46 / 255)}\n"
                  "  colorBlendRate: 15\n", view)
    set_doc(docs, "5875684227236902712", view)
    # The gauges: Charge and Space reuse the Thresher's two (renamed); Mass and Time get their own.
    for fid, name in (("7700000000000001200", "RippleGauge"), ("7700000000000001210", "AccretionGauge")):
        set_doc(docs, fid, re.sub(r"  m_Name: \w+\n", f"  m_Name: {name}\n", get_doc(docs, fid), count=1))
    for base, button_rect, name in (("7700000000000001220", "7700000000000001111", "ModeGauge"),
                                    ("7700000000000001230", "7700000000000001131", "TimeGauge")):
        if get_doc(docs, base) is not None:
            continue
        ids = [str(int(base) + i) for i in range(4)]
        src = [get_doc(docs, str(7700000000000001200 + i)) for i in range(4)]
        new = []
        for i, chunk in enumerate(src):
            c = chunk
            for j in range(4):
                c = c.replace(f"{{fileID: {7700000000000001200 + j}}}", f"{{fileID: {ids[j]}}}")
                c = c.replace(f"&{7700000000000001200 + j}\n", f"&{ids[j]}\n")
            c = c.replace("  m_Father: {fileID: 7700000000000001101}\n", f"  m_Father: {{fileID: {button_rect}}}\n")
            c = re.sub(r"  m_Name: \w+\n", f"  m_Name: {name}\n", c, count=1)
            new.append([ids[i], c])
        at = doc_index(docs, "7700000000000001213") + 1
        docs[at:at] = new
        add_child(docs, button_rect, ids[1])
    return join_docs(head, docs)


def class_icons_ok(files) -> bool:
    return all(f"guid: {g}" in files.get(os.path.relpath(os.path.join(ROOT, "Assets", "_Graphics", "CardImages", n + ".meta"), ROOT), "")
               for n, g in (("Stoat.png", CARD_ICON_ACTIVE), ("Stoat_Inactive.png", CARD_ICON_INACTIVE)))


def check_stage6(files, prefab, errors):
    rel_prefab = os.path.relpath(PREFAB, ROOT)

    def want(cond, msg):
        if not cond:
            errors.append(f"{rel_prefab}: {msg}")

    for guid, what in ((SQUIRREL_FBX_INSTANCE, "the Squirrel FBX instance"), (SQUIRREL_HUD_INSTANCE, "the SquirrelHUDVariant instance")):
        want(f"--- !u!1001 &{guid}\n" not in prefab, f"{what} is still nested in the prefab")
    for sguid, what in (("5db69771d02b4c24ca00998adf318137", "the Squirrel model"), ("c1572db06ad4244469ad3f25d86940b8", "SquirrelHUDVariant"),
                        ("002a4e51f3b840e3b397dac7b84f1a9a", "SquirrelVesselHUDController"),
                        ("2b0d487de1554acfad93a158ddac3874", "SquirrelVesselTelemetry"),
                        ("32b33e1ed2484f50a02e78e895fd4809", "SquirrelTubeActionExecutor"), (SQUIRREL_CAMERA, "the Squirrel's camera settings"),
                        (SQUIRREL_SKIMMER_CONTAINER, "the Squirrel's skimmer container"), (SQUIRREL_VESSEL_CONTAINER, "the Squirrel's vessel container"),
                        (SQUIRREL_AI_POLICY, "the Squirrel's AI boost policy"), ("d2af5f5040e134141a94c2b496074a65", "the SquirrelShard pool"),
                        ("e9da6bf37f53d1f4db0421348950f8ca", "the Squirrel crystal-explosion channel"), ("f076647eb7655744997dda101929faf0", "the Squirrel animator")):
        want(sguid not in prefab, f"still references {what} ({sguid})")
    want(stage6_executor_blocks() in prefab, "the mode / ripple executor documents are not authored as expected")
    want(hud_controller_block() in prefab, "the HUD controller is not StoatHUDController wired to the view and its four executors")
    want(hud_instance_blocks() in prefab, "the StoatHUDVariant instance is not authored as expected")
    want(collider_blocks() in prefab, "the Stoat's own hit box (StoatBody / StoatHitbox) is not authored as expected")
    want(telemetry_block() in prefab, "the telemetry is not DefaultVesselTelemetry")
    want(f"  settings: {{fileID: 11400000, guid: {CAMERA_SETTINGS}, type: 2}}\n" in prefab, "the camera is not StoatCameraSettingsSO")
    want(VESSEL_CONTAINER in prefab, "the VesselImpactor does not use StoatImpactorDataContainer")
    want(SKIMMER_CONTAINER in prefab, "the skimmer does not use StoatSkimmerImpactorDataContainer")
    want(f"  boostPolicy: {{fileID: 11400000, guid: {AI_BOOST_POLICY}, type: 2}}\n" in prefab, "the AIPilot's boost policy is not the Stoat's")
    want("  singleTriggerDrift: 1\n" not in prefab and "  driftShieldsTrail: 1\n" not in prefab, "a Squirrel drift switch is still on")
    want("skimmerPrismStayEffectsSO" not in prefab and "blockStayEffects" not in prefab, "stale skimmer overrides are back")
    # Every nested instance must close: a PrefabInstance with no m_SourcePrefab is unloadable (prune_skimmer_overrides
    # under re.S once cut the skimmer's tail off, and PrefabInstanceEditingTests threw on it).
    for inst in re.split(r"(?m)^(?=--- !u!)", prefab):
        if inst.startswith("--- !u!1001 &"):
            want("\n  m_SourcePrefab: {fileID: 100100000, guid: " in inst and "\n    m_RemovedComponents:" in inst,
                 f"PrefabInstance {inst.split('&', 1)[1].split()[0]} has no m_RemovedComponents / m_SourcePrefab (truncated)")
    for inst, tr, side in JETS_KEPT:
        chunk = re.search(r"--- !u!1001 &%s\n(?:.*\n)*?  m_SourcePrefab:.*\n" % inst, prefab)
        want(chunk is not None and f"m_TransformParent: {{fileID: {HULL_TRANSFORM_ID}}}" in chunk.group(0)
             and jet_mods(side) in chunk.group(0), f"the jet {inst} does not ride the hull at its hip")
    for inst in JETS_DROPPED:
        want(f"--- !u!1001 &{inst}\n" not in prefab, f"the Squirrel's extra jet {inst} is still nested")
    dangling = dangling_refs(prefab)
    want(not dangling, f"dangling local references after the surgery: {dangling[:6]}")
    reg = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  _executors:\n((?:  - \{fileID: -?\d+\}\n)+)" % REGISTRY_ID, prefab)
    want(reg is not None and f"  - {{fileID: {MODE_EXECUTOR_ID}}}\n" in reg.group(1) and f"  - {{fileID: {RIPPLE_EXECUTOR_ID}}}\n" in reg.group(1),
         "the mode / ripple executors are not in the ActionExecutorRegistry")
    for path, want_text in stage6_files().items():
        rel = os.path.relpath(path, ROOT)
        if files.get(rel) != want_text:
            errors.append(f"{rel} is missing or not as authored - run without --check")
    hud = files.get(os.path.relpath(HUD_VARIANT_PATH, ROOT), "")
    if not hud:
        errors.append(f"{os.path.relpath(HUD_VARIANT_PATH, ROOT)} is missing - run without --check")
    else:
        for need, what in ((f"guid: {HUD_VIEW_SCRIPT}", "StoatHUDView"), (f"guid: {VESSEL_HUD_PREFAB}", "a variant of VesselHUDPrefab"),
                           ("  modeGauge: {fileID: 7700000000000001223}\n", "the Mass card's mode gauge"),
                           ("  timeGauge: {fileID: 7700000000000001233}\n", "the Time card's gauge")):
            if need not in hud:
                errors.append(f"StoatHUDVariant.prefab: {what} missing")
        for _, name in ICON_SWAP:
            if stoat_icon_guid(name) not in hud:
                errors.append(f"StoatHUDVariant.prefab: the {name} icon is not on the row")
        if "ThresherHUDView" in hud or THRESHER_HUD_VARIANT in hud:
            errors.append("StoatHUDVariant.prefab: still names the Thresher's view or variant")
        if dangling_refs(hud):
            errors.append(f"StoatHUDVariant.prefab: dangling local references {dangling_refs(hud)[:4]}")
    if not files.get(os.path.relpath(AI_POLICY_PATH, ROOT), "").count("  m_Name: StoatAIBoostPolicy\n"):
        errors.append(f"{os.path.relpath(AI_POLICY_PATH, ROOT)} is missing or misnamed")
    if not class_icons_ok(files):
        errors.append("the Stoat's card icons (CardImages/Stoat.png, Stoat_Inactive.png) are missing - run render_stoat_card_icon.py")


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
    root = re.search(r"--- !u!4 &%s\n(?:.*\n)*?(  m_LocalScale: \{[^}]*\}\n)" % ROOT_TRANSFORM_ID, prefab)
    want(root is not None and root.group(1) == root_scale_line(), f"the root Transform is not scaled to {HULL_SCALE}")
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

    want(bindings() in prefab, "gamepad bindings are not LT->dipole+orbit left, RT->dipole+orbit right, X->hold, B->mode (touch cleared)")
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
        if "    AbilityLabel: Pathfinder / Time Mode\n" not in mp:
            errors.append(f"{rel_map}: Time must be the Pathfinder / Time Mode")
        if "    AbilityLabel: Ripple\n" not in mp or "    UpgradeLabel: Siphon\n" not in mp:
            errors.append(f"{rel_map}: Charge must be the Ripple (level 5 Siphon)")
        if "    AbilityLabel: Mass / Time Mode\n" not in mp or "    Input: 7\n" not in mp:
            errors.append(f"{rel_map}: Mass must be the Mass / Time Mode switch on B (Input 7)")
        for label in ("Accretion", "Foresight", "Armoured Wake"):
            if f"    UpgradeLabel: {label}\n" not in mp:
                errors.append(f"{rel_map}: the level-5 '{label}' is not declared")
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
    check_stage6(files, prefab, errors)
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
    paths += list(stage6_files().keys()) + [HUD_VARIANT_PATH, AI_POLICY_PATH]
    paths += [os.path.join(ROOT, "Assets", "_Graphics", "CardImages", n + ".meta") for n in ("Stoat.png", "Stoat_Inactive.png")]
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
    fires("hull scale reverted", lambda f: f.__setitem__(rel, f[rel].replace(root_scale_line(), "  m_LocalScale: {x: 1, y: 1, z: 1}\n", 1)))
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
    fires("Squirrel model back", lambda f: f.__setitem__(rel, f[rel] + "--- !u!1001 &8410834229753696920\nPrefabInstance:\n"))
    fires("Squirrel camera back", lambda f: f.__setitem__(rel, f[rel].replace(CAMERA_SETTINGS, SQUIRREL_CAMERA, 1)))
    fires("mode switch unbound", lambda f: f.__setitem__(rel, f[rel].replace(f"    - {{fileID: 11400000, guid: {MODE_ACTION}, type: 2}}\n", "", 1)))
    fires("ripple executor dropped from registry", lambda f: f.__setitem__(rel, f[rel].replace(f"  - {{fileID: {RIPPLE_EXECUTOR_ID}}}\n", "", 1)))
    fires("a dangling reference", lambda f: f.__setitem__(rel, f[rel].replace(f"  view: {{fileID: {HUD_VIEW_ID}}}\n", "  view: {fileID: 1234567}\n", 1)))
    hud_rel = os.path.relpath(HUD_VARIANT_PATH, ROOT)
    fires("HUD row lost its Time gauge", lambda f: f.__setitem__(hud_rel, f.get(hud_rel, "").replace("  timeGauge: {fileID: 7700000000000001233}\n", "  timeGauge: {fileID: 0}\n")))
    vc = os.path.relpath(VESSEL_CONTAINER_PATH, ROOT)
    fires("a nested instance truncated", lambda f: f.__setitem__(rel, re.sub(
        r"(--- !u!1001 &7079694710114171170\n(?:.*\n)*?)    m_RemovedComponents: \[\]\n(?:.*\n)*?  m_SourcePrefab: .*\n", r"\1", f[rel], count=1)))
    fires("container regained the Squirrel's explosion", lambda f: f.__setitem__(vc, f.get(vc, "") + "  - {fileID: 11400000, guid: 384844202f49d274185594aa5a884572, type: 2}\n"))
    # stage 2 is idempotent: applying it to the shipped prefab changes nothing
    idem = apply_hull(files[rel]) == files[rel]
    print(f"  stage 2 idempotent on the shipped prefab: {'yes' if idem else 'NO'}")
    ok &= idem
    idem = apply_dipole(files[rel]) == files[rel]
    print(f"  stage 4 idempotent on the shipped prefab: {'yes' if idem else 'NO'}")
    ok &= idem
    idem = apply_scale(files[rel]) == files[rel]
    print(f"  stage 5 idempotent on the shipped prefab: {'yes' if idem else 'NO'}")
    ok &= idem
    idem = apply_camera(files[rel]) == files[rel]
    print(f"  stage 5b (studio camera) idempotent on the shipped prefab: {'yes' if idem else 'NO'}")
    ok &= idem
    idem = apply_own_vessel(files[rel]) == files[rel]
    print(f"  stage 6 idempotent on the shipped prefab: {'yes' if idem else 'NO'}")
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
        after = apply_scale(before)
        if after != before:
            write(PREFAB, after)
            print(f"stage 5: the hull scaled to {HULL_SCALE} in {os.path.relpath(PREFAB, ROOT)}")
        before = after
        after = apply_camera(before)
        if after != before:
            write(PREFAB, after)
            print(f"stage 5b: the studio's chase camera wired into {os.path.relpath(PREFAB, ROOT)}")
        before = after
        if not os.path.exists(HUD_VARIANT_PATH):
            with open(THRESHER_HUD_PATH, encoding="utf-8", newline="") as f:
                write(HUD_VARIANT_PATH, hud_variant_from_donor(f.read().replace("\r\n", "\n")))
            print(f"stage 6: wrote {os.path.relpath(HUD_VARIANT_PATH, ROOT)} from ThresherHUDVariant.prefab")
        if not os.path.exists(AI_POLICY_PATH):
            with open(SQUIRREL_AI_POLICY_PATH, encoding="utf-8", newline="") as f:
                write(AI_POLICY_PATH, ai_policy_from_donor(f.read().replace("\r\n", "\n")))
            print(f"stage 6: wrote {os.path.relpath(AI_POLICY_PATH, ROOT)} from SquirrelAIBoostPolicy.asset")
        after = apply_own_vessel(before)
        if after != before:
            write(PREFAB, after)
            print(f"stage 6: the Stoat's own HUD, colliders, jets, camera, containers and executors in {os.path.relpath(PREFAB, ROOT)}")
        for path, text in stage6_files().items():
            old = None
            if os.path.exists(path):
                with open(path, encoding="utf-8", newline="") as f:
                    old = f.read().replace("\r\n", "\n")
            if old != text:
                write(path, text)
                print(f"stage 6: wrote {os.path.relpath(path, ROOT)}")
        if os.path.exists(CLASS_ASSET):
            with open(CLASS_ASSET, encoding="utf-8", newline="") as f:
                if f.read().replace("\r\n", "\n") != class_asset():
                    write(CLASS_ASSET, class_asset())
                    print(f"stage 6: rewrote {os.path.relpath(CLASS_ASSET, ROOT)} (its own card icons)")
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
