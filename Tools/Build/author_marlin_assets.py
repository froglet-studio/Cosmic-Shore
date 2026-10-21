#!/usr/bin/env python3
"""Author Marlin and Jacks (a marlin on the Rhino's flight rig, with two thumb-run jacks that herd real fauna) and register it, then prove it.

    python3 Tools/Build/author_marlin_assets.py             # author what is missing, then check
    python3 Tools/Build/author_marlin_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_marlin_assets.py --self-test # prove every check fires

"Bring it to Unity" means the Toybox's Vessel Changer offers it (Garrett, 2026-10-17). The changer offers what
ToyVesselRoster.Default names AND the Vessel Prefab Container can spawn, so the crew ships as its own hull class
(author_wrasse_assets.py is the pattern; this tool reuses its YAML helpers).

WHAT IT MAKES (the design: Assets/_Scripts/Controller/Vessel/R_VesselActions/MARLIN_JACKS.md)
    Assets/_Prefabs/Spacevessels/Marlin.prefab (+ .meta)   a text clone of Rhino.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 22` (VesselClassType.Marlin)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
        - MarlinJacksExecutor on the ShipActions object and in its ActionExecutorRegistry, wired to MarlinJacksConfig.asset,
          the fleet's turn-end channel and Event_CombatHitStats (the Angler's assets)
        - the marlin's buttons: the Rhino's ramp boost stays on Input 0 (Garrett: "the pilot has the rhino's full speed
          and straight ability to zoom forward"); TELEPORT on Input 1 (the trigger / a lifted thumb) and Input 4 (both
          thumbs lifted); CALL on Input 7 (A); the marlin's own DECOY on Input 6 (X; the Time level-5 upgrade). The
          Rhino's gamepad overrides (the shield swipes) are emptied
        - NO SWORD: the sword driver and its afterimages (ScaleSkimmerObject), the shield swipes (ShieldSwipeActionRegistry),
          the sword relay, the sword autopilot and the Momentum ward are stripped (RHINO_SUBTREES, RHINO_COMPONENTS). The
          ForceFieldSkimmer stays: it is the hull's near-field skimmer and its trail (its blade FX rests unlit)
        - RhinoVesselHUDController becomes MarlinJacksHUDController, wired to the executor
        - its VesselImpactor on MarlinImpactorDataContainer: haptics, the danger debuff and the prism damage; the Rhino's
          plough and its surge / explosion crystals are the Rhino's alone
        - THE BODIES (Garrett, 2026-10-21: "lets give it some models and get it ready to ship"): Marlin.fbx nested under the
          Rhino's fuselage part (so RhinoAnimation's turns carry it) as the ship geometry, the Rhino's rig switched off (its
          bones stay: the puppetry still turns them); two Jack.fbx under the root ('Jack A', 'Jack B'); MarlinCrewBodies on
          the root draws, swims and replicates them and puppets the marlin (bodies_retrofit; /vessel-model specs
          marlin.py and jack.py)
        - the ability row's four icons (author_marlin_icons.py) over the Rhino's sword placeholders
        - its own MarlinCameraSettingsSO (the Rhino's numbers at first) and DefaultVesselTelemetry with its stat channels
      Everything else is the Rhino's: its colliders, its two sticks, its ramp.
      Unlike the clone, the bindings, the strip and the HUD swap are RETROFITS: applied to the committed prefab too.
    MarlinJacksConfig.asset and the three controls (Assets/_SO_Assets/VesselActions/MarlinJacks/);
    SO_Class_Marlin ("Marlin and Jacks", its card rendered from Marlin.fbx, owned from the start) + its entry in SO_Classlist_All /
    _Classes; one entry each in `Vessel Prefab Container.asset` (the root TRANSFORM) and `DefaultNetworkPrefabs.asset`
    (the root GAMEOBJECT); Resources/ElementalAbilityMaps/Marlin.asset; and four CrystalHullFusionConfig entries (the
    Rhino's: they still bake the Rhino's hull shape until a marlin bake is made in the editor). ToyVesselRoster.Default is CODE and is not written here.

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while Marlin.prefab is absent, and
`--check` validates the SHIPPED prefab's invariants. To re-author from scratch, delete Marlin.prefab and its .meta.
"""
import hashlib
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402
from author_wrasse_assets import (DOC, dangling_local_refs, guid_of, guid_of_script, native_meta,  # noqa: E402
                                  read, split_docs, write, yaml_str, fusion_entries)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Rhino.prefab")
PREFAB = os.path.join(VESSELS, "Marlin.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
DONOR_CLASS = os.path.join(CLASSES, "SO_Class_Rhino.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Marlin.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
DONOR_MAP = os.path.join(MAPS, "Rhino.asset")
MAP = os.path.join(MAPS, "Marlin.asset")
FUSION = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
ACTIONS = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "MarlinJacks")
CONFIG = os.path.join(ACTIONS, "MarlinJacksConfig.asset")
CONTAINERS = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Effect Containers", "VesselContainers")
MARLIN_CONTAINER = os.path.join(CONTAINERS, "MarlinImpactorDataContainer.asset")

# 20 is the Orca's and 21 is claimed twice in flight (the Honeyguide and the Starlings, PR #1305). Checked across every
# origin branch 2026-10-21.
MARLIN_CLASS_ID = 22
RHINO_CLASS_ID = 3
PREFAB_GUID = "5d7f0a3c9e2b4f61a8c47e1b2d9f6a03"
CLASS_GUID = "8a41c6e2f0d94b7c9e35a1d7c4b8e2f6"
MAP_GUID = "c3e9a7f15b2d4e08a6f1d9c2b7e4a530"
CONFIG_GUID = "e17b4c9a2d6f4835b0a9c3e1f7d2b648"
# the marlin's own camera settings: the Rhino's numbers at first (the marlin is the Rhino's hull in flight), its own asset
# so the team tunes the marlin's chase camera without moving the Rhino's
CAMERA_DIR = os.path.join(ROOT, "Assets", "_SO_Assets", "Camera")
RHINO_CAMERA = os.path.join(CAMERA_DIR, "RhinoCameraSettingsSO.asset")
CAMERA = os.path.join(CAMERA_DIR, "MarlinCameraSettingsSO.asset")
RHINO_CAMERA_GUID = "6f9693afb46548dd83870cfb0b1ce939"
CAMERA_GUID = "b84e1d7a3c2f4a9e8d5b6c0f1a3e7d29"
CONTAINER_GUID = "9b2e6d4f1a7c4e3890d5b8a2c6f1e473"
CONTROLS = [  # (asset name, MarlinJacksControl value, guid)
    ("MarlinCall", 0, "4f8a2c6e9b1d4a7380e5c2f9d6b1a734"),
    ("MarlinTeleport", 1, "a6d1e9b3c7f24058b9e2a4d6c1f8b357"),
    ("MarlinDecoy", 2, "2c7e5a9f3b1d4c68a0f7e2b9d4c6a185"),
]
CONTROL = {name: guid for name, _, guid in CONTROLS}
DONOR_CONTAINER_GUID = "212ff1ef2b8c43c4ab1acfc073c79e0b"   # RhinoImpactorDataContainer
RAMP_BOOST_GUID = "cb9bc8a3724a4a67a3656d7df507b2cb"        # RhinoRampBoostAction
TURN_END_GUID = "498a06d44bde9184f985c938c803b2a1"          # the fleet's OnMiniGameTurnEnd (as the Angler wires it)
COMBAT_HIT_GUID = "45fb43a0cddb5cc3cc4dc448df994152"        # Event_CombatHitStats
COMPANION_FAUNA_GUID = "0e87f3117e7141a7b5bce66b0385a2d0"   # the Angler's companion species
ROOT_GO = "6417075533431866457"              # the Rhino's root GameObject
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "3711687605623876765"
SHIP_ACTIONS_GO = "1916516311732452555"      # the ShipActions child (its ActionExecutorRegistry)
REGISTRY_ID = "5397651131628914846"
EXECUTOR_ID = "2200000000000000001"          # MarlinJacksExecutor on ShipActions

# The marlin's buttons (InputEvents: 0 FullSpeedStraight, 1 RightStickAction, 4 IdleAction, 6 Button1 (X), 7 Button2 (A))
BINDINGS = re.compile(r"(  _inputEventShipActions:)\n(?:  [- ] .*\n)+")
OVERRIDES = re.compile(r"(  _gamepadActionOverrides:)\n(?:  [- ] .*\n)+")
WANT_BINDINGS = (
    "  _inputEventShipActions:\n"
    f"  - InputEvent: 0\n    ShipActions:\n    - {{fileID: 11400000, guid: {RAMP_BOOST_GUID}, type: 2}}\n"
    f"  - InputEvent: 1\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL['MarlinTeleport']}, type: 2}}\n"
    f"  - InputEvent: 4\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL['MarlinTeleport']}, type: 2}}\n"
    f"  - InputEvent: 6\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL['MarlinDecoy']}, type: 2}}\n"
    f"  - InputEvent: 7\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL['MarlinCall']}, type: 2}}\n")

RHINO_SUBTREES = ["ScaleSkimmerObject", "ShieldSwipeActionRegistry"]
RHINO_COMPONENTS = ["RhinoSwordNetworkRelay", "VesselElementalImmunity", "RhinoSwordAutopilot"]
RHINO_HUD_KEYS = ["view", "swordDriver", "afterimage", "speedWard"]


# ----------------------------------------------------------------------------- the prefab
def executor_block(script_guid: str) -> str:
    return (f"--- !u!114 &{EXECUTOR_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {SHIP_ACTIONS_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n"
            f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}\n"
            f"  OnMiniGameTurnEnd: {{fileID: 11400000, guid: {TURN_END_GUID}, type: 2}}\n"
            f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_GUID}, type: 2}}\n"
            "  jackColor: {r: 0.949, g: 0.784, b: 0.294, a: 1}\n  decoyColor: {r: 0.941, g: 0.627, b: 0.263, a: 1}\n"
            "  marlinColor: {r: 0.122, g: 0.722, b: 0.69, a: 1}\n  dangerColor: {r: 1, g: 0.353, b: 0.235, a: 1}\n"
            "  ringColor: {r: 0.949, g: 0.784, b: 0.294, a: 0.55}\n")


def component_list(text: str, go: str):
    return re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % go, text)


def build_clone(donor: str, script_guid: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Rhino\n", "  m_Name: Marlin\n", "root GameObject name")
    text = replace_once(text, "  _name: Rhino\n", "  _name: Marlin\n", "VesselStatus _name")
    text = replace_once(text, f"  vesselType: {RHINO_CLASS_ID}\n", f"  vesselType: {MARLIN_CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)
    if not text.endswith("\n"):
        text += "\n"
    return retrofit(text + executor_block(script_guid))


def strip_rhino_kit(text: str) -> str:
    """The sword and its parts off the marlin (see the module doc). Idempotent."""
    head, docs = split_docs(text)
    go_name = {a: (re.search(r"^  m_Name: (.*)$", d, re.M) or [None, None])[1] for k, a, d in docs if k == "1"}
    transform_of, children = {}, {}
    for k, a, d in docs:
        if k in ("4", "224") and " stripped" not in d.split("\n", 1)[0]:
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", d)
            if go:
                transform_of[go.group(1)] = a
            ch = re.search(r"  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", d)
            children[a] = re.findall(r"fileID: (-?\d+)", ch.group(1)) if ch else []
    go_of_transform = {t: g for g, t in transform_of.items()}
    gos = set()

    def take(go):
        if go in gos:
            return
        gos.add(go)
        for child in children.get(transform_of.get(go), []):
            if child in go_of_transform:
                take(go_of_transform[child])
    for go, name in go_name.items():
        if name in RHINO_SUBTREES:
            take(go)
    kill_scripts = {guid_of_script(n) for n in RHINO_COMPONENTS}
    removed = set(gos) | {transform_of[g] for g in gos if g in transform_of}
    for k, a, d in docs:
        go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", d, re.M)
        if go and go.group(1) in gos:
            removed.add(a)
        sc = re.search(r"^  m_Script: \{fileID: 11500000, guid: (\w+)", d, re.M)
        if k == "114" and sc and sc.group(1) in kill_scripts:
            removed.add(a)
    out = []
    for k, a, d in docs:
        if a in removed:
            continue
        for r in removed:
            d = re.sub(r"^  - component: \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"^  - \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"\{fileID: %s\}" % r, "{fileID: 0}", d)
        out.append(d)
    text = head + "".join(out)
    return re.sub(r"^(  (?:m_Children|_executors|m_Component)):\n(?=  [a-zA-Z_<]|--- |\Z)", r"\1: []\n", text, flags=re.M)


def executor_retrofit(text: str) -> str:
    """The executor on ShipActions' component list and in its registry. Idempotent."""
    m = component_list(text, SHIP_ACTIONS_GO)
    if m and f"{{fileID: {EXECUTOR_ID}}}" not in m.group(1):
        text = text[:m.end(1)] + f"  - component: {{fileID: {EXECUTOR_ID}}}\n" + text[m.end(1):]
    reg = re.search(r"(--- !u!114 &%s\n(?:.*\n)*?)  _executors:(?: \[\])?\n((?:  - \{fileID: -?\d+\}\n)*)" % REGISTRY_ID, text)
    if reg and f"  - {{fileID: {EXECUTOR_ID}}}\n" not in reg.group(2):
        text = text[:reg.start()] + reg.group(1) + "  _executors:\n" + reg.group(2) + f"  - {{fileID: {EXECUTOR_ID}}}\n" + text[reg.end():]
    return text


def hud_retrofit(text: str) -> str:
    """RhinoVesselHUDController (the sword) becomes MarlinJacksHUDController, wired to the executor. Idempotent."""
    rhino_hud, marlin_hud = guid_of_script("RhinoVesselHUDController"), guid_of_script("MarlinJacksHUDController")
    head, docs = split_docs(text)
    out = []
    for _, _, d in docs:
        if f"guid: {rhino_hud}, type: 3" in d:
            d = d.replace(f"guid: {rhino_hud}, type: 3", f"guid: {marlin_hud}, type: 3")
            for key in RHINO_HUD_KEYS:
                d = re.sub(r"^  %s: \{[^}]*\}\n" % key, "", d, flags=re.M)
            d = d.rstrip("\n") + f"\n  crew: {{fileID: {EXECUTOR_ID}}}\n"
        out.append(d)
    return head + "".join(out)


# ----------------------------------------------------------------------------- the bodies (Garrett, 2026-10-21: "lets give it
# some models and get it ready to ship"): Marlin.fbx and two Jack.fbx (/vessel-model, specs/marlin.py and specs/jack.py)
MARLIN_FBX_GUID = "6d2b8e4f0a9c4e17b3f5a8c1d2e7b904"
JACK_FBX_GUID = "3a8f1c6e2b5d4e79a0c4f7b1e9d2a658"
RHINO_RIG_GUID = "350f1f00a1b1c4041b6339bbc7504694"
RHINO_RIG_INSTANCE = "4710562938102847551"       # the Rhino's rig, nested under the fuselage part
FUSE_TRANSFORM = "5169072473205323491"           # 'Rhino_Test (1)': the part RhinoAnimation turns as the body
FUSE_GO = "5546161687949175897"                  # its GameObject: the ship geometry until the marlin
MODEL_ROOT_GO = "919132149155446097"             # a model's root GameObject / Transform: constants of the importer
MODEL_ROOT_TRANSFORM = "-8679921383154817045"
MARLIN_INSTANCE_ID, MARLIN_TRANSFORM_ID, MARLIN_GO_ID = "2200000000000000011", "2200000000000000012", "2200000000000000013"
JACK_IDS = (("Jack A", "2200000000000000021", "2200000000000000022"), ("Jack B", "2200000000000000031", "2200000000000000032"))
BODIES_ID = "2200000000000000041"                # MarlinCrewBodies on the root
# the ability row's four icons (author_marlin_icons.py), over the Rhino's sword placeholders the HUD variant draws
HUD_INSTANCE = "2754590152818896375"            # RhinoHUDVariant nested in the hull
HUD_VARIANT_GUID = "2656b477a4af7304180e65f2848a84b3"
ICON_SPRITES = (("7720000000000003015", "c3a91e5d7b2f4c68a0e4d1b9f6c2a873"),   # ChargeIcon: Danger Share
                ("7720000000000003025", "5e8b2d6a1f9c4b37a2d0e7c4b1f9a645"),   # MassIcon: Breeding
                ("7720000000000003035", "9f1c4a7e3b6d4e82b5a0c8d2e7f1b396"),   # SpaceIcon: Herding Ring
                ("7720000000000003045", "2b7d5f9a4c1e4a63b8f2d0e6a9c5b718"))   # TimeIcon: Herd Speed


def icon_override(image_id: str, sprite_guid: str) -> str:
    return (f"    - target: {{fileID: {image_id}, guid: {HUD_VARIANT_GUID},\n        type: 3}}\n      propertyPath: m_Sprite\n"
            f"      value: \n      objectReference: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}\n")


def icons_retrofit(text: str) -> str:
    """The HUD's four icons are the marlin's. Idempotent."""
    m = re.search(r"--- !u!1001 &%s\nPrefabInstance:\n(?:.*\n)*?    m_Modifications:\n" % HUD_INSTANCE, text)
    if not m:
        return text
    add = "".join(icon_override(i, g) for i, g in ICON_SPRITES if icon_override(i, g) not in text)
    return text[:m.end()] + add + text[m.end():]


TELEMETRY_ID = "2200000000000000051"             # DefaultVesselTelemetry on the root, its stat channels wired
# the stat channels every vessel's telemetry fires (the Wrasse's: longest drift, max boost time, prisms damaged). Without the
# component on the prefab VesselTelemetryBootstrapper adds one at runtime with these unwired, and warns.
TELEMETRY_FIELDS = ("  longestDriftStat: {fileID: 11400000, guid: c9ca3d6eff04d70468aa24ee5b1f561d, type: 2}\n"
                    "  maxBoostTimeStat: {fileID: 11400000, guid: be1a9b6401dca924585cd31ff1034c6f, type: 2}\n"
                    "  prismsDamagedStat: {fileID: 11400000, guid: 3ea71b2eaf0cdbb46a99ddaf04b2d999, type: 2}\n"
                    "  boostMultiplierThreshold: 1.5\n")
MARLIN_MODEL_Z = "-2"                            # under the fuselage: the hull's five colliders still enclose the body
RIG_OFF = (f"    - target: {{fileID: {MODEL_ROOT_GO}, guid: {RHINO_RIG_GUID},\n        type: 3}}\n"
           "      propertyPath: m_IsActive\n      value: 0\n      objectReference: {fileID: 0}\n")
# MarlinCrewBodies' serialized fields at the C# initializers, written once (the team tunes them in the inspector after)
BODIES_FIELDS = (
    "  marlinModelName: Marlin\n  replacedModelName: rhino_shapekey_with_animations\n  jackModelNames:\n  - Jack A\n"
    "  - Jack B\n  jackModelScale: 2.5\n  jackModelEuler: {x: 0, y: 0, z: 0}\n  jackSwimHz: 1\n  jackSwimHzPer100: 0.8\n"
    "  jackSwimDegrees: {x: 6, y: 12, z: 22}\n  jackSwimLagBeats: 0.18\n  jackFinFlapDegrees: 22\n  jackTurnRate: 8\n"
    "  jackHeadingMinSpeed: 6\n  cruiseSpeed: 60\n  boostSpeed: 260\n  tailHz: {x: 0.6, y: 1.6}\n  tailDegrees: {x: 6, y: 14}\n"
    "  turnBendDegrees: 20\n  fullYawRate: 110\n  finFollow: 0.6\n  sailCallDegrees: 14\n  sailBoostFoldDegrees: 40\n"
    "  sailRate: 5\n  remoteSmoothing: 10\n")


def model_instance(instance_id: str, transform_id: str, parent: str, fbx_guid: str, name: str, z: str = "0",
                   go_id: str = None) -> str:
    """A model nested in its own frame (the fleet's: nose +Z, up +Y) under `parent`, `z` along the parent's nose."""
    mods = [(MODEL_ROOT_GO, "m_Name", name)] + [(MODEL_ROOT_TRANSFORM, k, v) for k, v in (
        ("m_LocalPosition.x", "0"), ("m_LocalPosition.y", "0"), ("m_LocalPosition.z", z), ("m_LocalRotation.w", "1"),
        ("m_LocalRotation.x", "0"), ("m_LocalRotation.y", "0"), ("m_LocalRotation.z", "0"),
        ("m_LocalEulerAnglesHint.x", "0"), ("m_LocalEulerAnglesHint.y", "0"), ("m_LocalEulerAnglesHint.z", "0"))]
    out = (f"--- !u!1001 &{instance_id}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
           f"  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {{fileID: {parent}}}\n"
           "    m_Modifications:\n" +
           "".join(f"    - target: {{fileID: {fid}, guid: {fbx_guid},\n        type: 3}}\n      propertyPath: {path}\n"
                   f"      value: {v}\n      objectReference: {{fileID: 0}}\n" for fid, path, v in mods) +
           "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
           f"  m_SourcePrefab: {{fileID: 100100000, guid: {fbx_guid}, type: 3}}\n"
           f"--- !u!4 &{transform_id} stripped\nTransform:\n"
           f"  m_CorrespondingSourceObject: {{fileID: {MODEL_ROOT_TRANSFORM}, guid: {fbx_guid},\n    type: 3}}\n"
           f"  m_PrefabInstance: {{fileID: {instance_id}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    if go_id:
        out += (f"--- !u!1 &{go_id} stripped\nGameObject:\n"
                f"  m_CorrespondingSourceObject: {{fileID: {MODEL_ROOT_GO}, guid: {fbx_guid},\n    type: 3}}\n"
                f"  m_PrefabInstance: {{fileID: {instance_id}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return out


def telemetry_block() -> str:
    return (f"--- !u!114 &{TELEMETRY_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid_of_script('DefaultVesselTelemetry')}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n" + TELEMETRY_FIELDS)


def bodies_block(script_guid: str) -> str:
    return (f"--- !u!114 &{BODIES_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n" + BODIES_FIELDS)


def add_children(text: str, transform_id: str, children) -> str:
    m = re.search(r"(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:)( \[\])?\n((?:  - \{fileID: -?\d+\}\n)*)" % transform_id, text)
    if not m:
        return text
    add = "".join(f"  - {{fileID: {t}}}\n" for t in children if f"  - {{fileID: {t}}}\n" not in m.group(3))
    return text[:m.start()] + m.group(1) + "\n" + m.group(3) + add + text[m.end():]


def bodies_retrofit(text: str) -> str:
    """The marlin and the two jacks as models. Idempotent: every step looks before it writes."""
    script_guid = guid_of_script("MarlinCrewBodies")
    if f"--- !u!1001 &{MARLIN_INSTANCE_ID}\n" not in text:
        text += model_instance(MARLIN_INSTANCE_ID, MARLIN_TRANSFORM_ID, FUSE_TRANSFORM, MARLIN_FBX_GUID, "Marlin",
                               MARLIN_MODEL_Z, MARLIN_GO_ID)
    for name, iid, tid in JACK_IDS:
        if f"--- !u!1001 &{iid}\n" not in text:
            text += model_instance(iid, tid, ROOT_TRANSFORM_ID, JACK_FBX_GUID, name)
    if f"--- !u!114 &{BODIES_ID}\n" not in text:
        text += bodies_block(script_guid)
    if f"--- !u!114 &{TELEMETRY_ID}\n" not in text:
        text += telemetry_block()
    for cid in (BODIES_ID, TELEMETRY_ID):
        m = component_list(text, ROOT_GO)
        if m and f"{{fileID: {cid}}}" not in m.group(1):
            text = text[:m.end(1)] + f"  - component: {{fileID: {cid}}}\n" + text[m.end(1):]
    text = add_children(text, FUSE_TRANSFORM, [MARLIN_TRANSFORM_ID])
    text = add_children(text, ROOT_TRANSFORM_ID, [tid for _, _, tid in JACK_IDS])
    # the marlin is the drawn hull: the domain paint lands on it, and the Rhino's rig is switched off (its bones stay and
    # the puppetry still turns them; the hull's colliders are the fuselage's and the plates' own)
    text = text.replace(f"  _shipGeometries:\n  - {{fileID: {FUSE_GO}}}\n", f"  _shipGeometries:\n  - {{fileID: {MARLIN_GO_ID}}}\n")
    m = re.search(r"--- !u!1001 &%s\nPrefabInstance:\n(?:.*\n)*?    m_Modifications:\n" % RHINO_RIG_INSTANCE, text)
    if m and RIG_OFF not in text:
        text = text[:m.end()] + RIG_OFF + text[m.end():]
    return text


def check_bodies(text: str):
    errs = []

    def want(cond, msg):
        if not cond:
            errs.append(msg)
    for iid, parent, fbx, name in [(MARLIN_INSTANCE_ID, FUSE_TRANSFORM, MARLIN_FBX_GUID, "Marlin")] + \
                                  [(iid, ROOT_TRANSFORM_ID, JACK_FBX_GUID, n) for n, iid, _ in JACK_IDS]:
        inst = re.search(r"--- !u!1001 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % iid, text)
        want(inst and f"m_SourcePrefab: {{fileID: 100100000, guid: {fbx}, type: 3}}" in inst.group(1)
             and f"m_TransformParent: {{fileID: {parent}}}" in inst.group(1) and f"      value: {name}\n" in inst.group(1),
             f"Marlin.prefab: '{name}' is not nested where MarlinCrewBodies finds it")
    for tid, kids in ((FUSE_TRANSFORM, [MARLIN_TRANSFORM_ID]), (ROOT_TRANSFORM_ID, [t for _, _, t in JACK_IDS])):
        t = re.search(r"--- !u!4 &%s\nTransform:\n((?:.*\n)*?)(?=--- !u!|\Z)" % tid, text)
        want(t and all(f"  - {{fileID: {k}}}\n" in t.group(1) for k in kids), f"Marlin.prefab: transform {tid} does not list {kids}")
    root = component_list(text, ROOT_GO)
    want(root and f"{{fileID: {BODIES_ID}}}" in root.group(1), "Marlin.prefab: MarlinCrewBodies is not on the root")
    tel = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % TELEMETRY_ID, text)
    want(root and f"{{fileID: {TELEMETRY_ID}}}" in root.group(1) and tel and TELEMETRY_FIELDS in tel.group(1),
         "Marlin.prefab: no DefaultVesselTelemetry on the root with its stat channels (the bootstrapper's unwired fallback)")
    body = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % BODIES_ID, text)
    want(body and f"guid: {guid_of_script('MarlinCrewBodies')}, type: 3" in body.group(1),
         "Marlin.prefab: the MarlinCrewBodies document is missing or names another script (no one would see the jacks)")
    want(f"  _shipGeometries:\n  - {{fileID: {MARLIN_GO_ID}}}\n" in text,
         "Marlin.prefab: the ship geometry is not the marlin (the domain colour would paint the hidden Rhino)")
    want(RIG_OFF in text, "Marlin.prefab: the Rhino's rig still draws (a Rhino inside the marlin)")
    hud = re.search(r"--- !u!1001 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % HUD_INSTANCE, text)
    for image_id, sprite in ICON_SPRITES:
        want(hud and icon_override(image_id, sprite) in hud.group(1),
             f"Marlin.prefab: the HUD icon {image_id} is not the marlin's {sprite} (the Rhino's sword placeholder shows)")
    return errs


def retrofit(text: str) -> str:
    """The marlin's buttons, no sword, its HUD and container, the executor. Idempotent."""
    text = BINDINGS.sub(lambda m: WANT_BINDINGS, text, count=1)
    text = OVERRIDES.sub(lambda m: m.group(1) + " []\n", text, count=1)
    text = strip_rhino_kit(text)
    text = executor_retrofit(text)
    text = hud_retrofit(text)
    text = text.replace(f"guid: {DONOR_CONTAINER_GUID},", f"guid: {CONTAINER_GUID},")
    text = text.replace(f"  settings: {{fileID: 11400000, guid: {RHINO_CAMERA_GUID}, type: 2}}",
                        f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}")
    return icons_retrofit(bodies_retrofit(text))


def prefab_meta() -> str:
    return (f"fileFormatVersion: 2\nguid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the assets
def so_header(script_guid: str, name: str) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: {name}\n  m_EditorClassIdentifier: \n")


def config_asset(script_guid: str) -> str:
    """MarlinJacksConfigSO at its C# initializers (the lab's shipped values); the team tunes it in the inspector after."""
    return so_header(script_guid, "MarlinJacksConfig") + (
        "  labScale: 0.4\n  thumbMm: 22\n  thumbMargin: 1.25\n  viewSpan: 1800\n" + THUMB_VIEW_FIELDS + "  camSlack: 0.45\n  planePull: 1.6\n"
        "  separation: 58\n  neighbourRadius: 160\n  grazeSense: 130\n  minHerd: 30\n  herdCap: 150\n  floorBirthSeconds: 0.12\n"
        f"  companionFauna: {{fileID: 11400000, guid: {COMPANION_FAUNA_GUID}, type: 2}}\n" + HERD_MARK_FIELDS +
        "  dogSpeed: 520\n  padDogSpeed: 1\n  aiNoise: 0.15\n  callTime: 2.5\n  callCooldown: 20\n  decoyCooldown: 4\n"
        "  teleportGuard: 0.6\n  pooEvery: 6\n  pooCount: 7\n  pooSpread: 22\n  pooPrismScale: {x: 2, y: 2, z: 5}\n"
        "  attackRange: 200\n  contactReach: 8\n  strikeCooldown: 1.2\n  pipSize: 0.32\n  viewMargin: 0.02\n  viewDepth: 600\n"
        + STABILITY_FIELDS +
        f"  callSfx:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"
        f"  decoySfx:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"
        f"  teleportSfx:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n")


# The jacks' player's stability (Garrett, 2026-10-21: "the marlin makes the jacks player sick with its movement"). Added to a
# committed config once, at the C# initializers, so a value tuned after is never overwritten.
STABILITY_FIELDS = ("  jackSeatPlaneEase: 0.25\n  pilotSeatPlaneEase: 3\n  jackSeatMarlinWeight: 0\n  pilotSeatMarlinWeight: 0.45\n"
                    "  viewPanEase: 1.2\n")


# The jacks' view framed by the thumb (Garrett, 2026-10-21: "zoomed out enough that on a full size mobile device an above
# average sized thumb barely covers the smallest jack repulsion radius") and the herd's mark ("a slight hue shift in your
# domain"). Added once, like the stability fields.
THUMB_VIEW_FIELDS = "  phoneShortMm: 71\n  thumbCover: 0.9\n"
HERD_MARK_FIELDS = "  herdHueLean: 0.35\n"


def config_retrofit(text: str) -> str:
    """The stability fields after viewDepth, the thumb view's after viewSpan, the herd's mark after companionFauna; each
    once. Idempotent."""
    if "  jackSeatPlaneEase:" not in text:
        text = re.sub(r"^(  viewDepth: .*\n)", lambda m: m.group(1) + STABILITY_FIELDS, text, count=1, flags=re.M)
    if "  phoneShortMm:" not in text:
        text = re.sub(r"^(  viewSpan: .*\n)", lambda m: m.group(1) + THUMB_VIEW_FIELDS, text, count=1, flags=re.M)
    if "  herdHueLean:" not in text:
        text = re.sub(r"^(  companionFauna: .*\n)", lambda m: m.group(1) + HERD_MARK_FIELDS, text, count=1, flags=re.M)
    return text


def control_asset(script_guid: str, name: str, value: int) -> str:
    return so_header(script_guid, name) + f"  control: {value}\n"


# Haptics, the prism damage and the danger debuff (the Angler's): the marlin is a hull that flies through mass like any.
# The Rhino's plough and its surge / explosion crystals are the Rhino's.
CONTAINER_PRISM = ["ca2d1be3880b82941a0465a1f7e45280",   # VesselHapticsByPrismEffect
                   "4da62d4eefe03594ba35027dd537aff0",   # VesselDamagePrismEffect
                   "c7ccaca885824b24b716b12148d77ce1"]   # VesselElementalDebuffByDangerPrismEffect
CONTAINER_CRYSTAL = ["8de915fb00503344ca89a92a0a127b00"]  # VesselHapticsByCrystalEffect


def container_asset() -> str:
    lst = lambda gs: "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in gs)
    return so_header(guid_of_script("VesselImpactorDataContainerSO"), "MarlinImpactorDataContainer") + (
        "  vesselPrismEffects:\n" + lst(CONTAINER_PRISM) + "  vesselCrystalEffects:\n" + lst(CONTAINER_CRYSTAL) +
        "  vesselMassCrystalEffects: []\n  vesselChargeCrystalEffects: []\n  vesselSpaceCrystalEffects: []\n"
        "  vesselTimeCrystalEffects: []\n  vesselSkimmerEffects: []\n  vesselLifeformCrystalEffects: []\n"
        "  omniCrystalRetirement: {fileID: 0}\n")


DESCRIPTION = ("  Description: A marlin and two jacks herd the cell's wildlife. Run each jack with a thumb;\n"
               "    lift it to leave the decoy. The marlin flies the Rhino's ramp, calls the herd to\n"
               "    itself and teleports to the decoy. D-pad left or right swaps seats.\n")
CREW = "  CrewNames:\n  - Marlin\n  - Jacks\n  CrewNameTemplate: '{0} and {1}'\n"


# The class card, rendered from Marlin.fbx by Tools/Build/vessel_model/render_cards.py (2026-10-21), which names each new
# PNG md5("cosmic-shore-card:" + its repo path); this generator writes the class asset too, so it names the same guids.
def _model_card(rel):
    return hashlib.md5(("cosmic-shore-card:" + rel).encode()).hexdigest()


CARD_ACTIVE = _model_card("Assets/_Graphics/CardImages/Marlin.png")
CARD_FIELDS = {
    "IconActive": CARD_ACTIVE,
    "IconInactive": _model_card("Assets/_Graphics/CardImages/Marlin_Inactive.png"),
    "CardSilohoutteActive": CARD_ACTIVE,
    "CardSilohoutteInactive": _model_card("Assets/_Graphics/CardImages/Marlin_silhouette.png"),
    "SquadImage": _model_card("Assets/_Graphics/CardImages/Marlin_large.png"),
    "PreviewImage": _model_card("Assets/_Graphics/Design Assests/Hangar/Marlin_Hangar_Preview.png"),
}


def cards_retrofit(text: str) -> str:
    """Point the class asset's six card fields at the Marlin's own renders (they were the Rhino's). Idempotent."""
    for field, guid in CARD_FIELDS.items():
        text = re.sub(r"(  %s: \{fileID: 21300000, guid: )[0-9a-f]{32}(,[^}]*\})" % field, r"\g<1>%s\2" % guid, text, count=1)
    return text


def class_asset(donor_class: str) -> str:
    text = replace_once(donor_class, "  m_Name: SO_Class_Rhino\n", "  m_Name: SO_Class_Marlin\n", "class m_Name")
    text = replace_once(text, f"  Class: {RHINO_CLASS_ID}\n", f"  Class: {MARLIN_CLASS_ID}\n", "class Class")
    text = replace_once(text, "  Name: Rhino\n", "  Name: Marlin and Jacks\n", "class Name")
    text, n = re.subn(r"  Description: .*\n(?:    .*\n)*", lambda m: DESCRIPTION + CREW, text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class Description was not found")
    text, n = re.subn(r"  isLocked: \d\n", "  isLocked: 0\n  ownedFromStart: 1\n", text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class isLocked line was not found")
    return cards_retrofit(text)


# The four cards (Garrett, 2026-10-20). The words are authored data the team edits in the inspector; written once.
MAP_ENTRIES = [
    (1, 0, "Danger Share", "Charge sets the share of the herd that is DANGER: none at -5, all of it at 15. A danger "
        "creature that touches a rival hull stings it (a Strike) and is spent.",
     "Attack", "The herd goes for rivals near it and bites any it touches."),
    (2, 0, "Breeding", "Mass sets how fast the herd breeds: a birth takes the species' own feeds at -5 and a third of "
        "them at 15. The herd never starves below its minimum size.",
     "Mass Clusters", "Every few seconds a creature of the herd lays a cluster of your mass."),
    (3, 0, "Herding Ring", "Space widens the ring the marlin, both jacks and the decoy push the herd out of; it is "
        "never smaller than a big thumb.",
     "Eat Shields", "The herd eats shielded prisms."),
    (4, 7, "Herd Speed", "Time sets how fast the herd swims. The marlin's call (A) pulls the herd to it for a moment, on "
        "a long cooldown.",
     "Marlin Decoy", "The marlin leaves the decoy where it is (X), so it can teleport back."),
]


def map_entries() -> str:
    out = "  entries:\n"
    for element, inp, label, desc, up_label, up_desc in MAP_ENTRIES:
        out += (f"  - Element: {element}\n    AbilityLabel: {yaml_str(label)}\n    AbilityDescription: {yaml_str(desc)}\n"
                f"    Input: {inp}\n    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n"
                f"    UpgradeLabel: {yaml_str(up_label)}\n    UpgradeDescription: {yaml_str(up_desc)}\n")
    return out


def build_map(donor_map: str) -> str:
    text = replace_once(donor_map, "  m_Name: Rhino\n", "  m_Name: Marlin\n", "map name")
    text = replace_once(text, f"  vesselClass: {RHINO_CLASS_ID}\n", f"  vesselClass: {MARLIN_CLASS_ID}\n", "map vesselClass")
    return text[:text.index("  entries:\n")] + map_entries()


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
                   "    SourcePrefabToOverride: {fileID: 0}\n    SourceHashToOverride: 0\n    OverridingTargetPrefab: {fileID: 0}\n")


def register_fusion(text: str) -> str:
    if fusion_entries(text, MARLIN_CLASS_ID):
        return text
    donor = fusion_entries(text, RHINO_CLASS_ID)
    if len(donor) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Rhino entries, found {len(donor)}")
    last = None
    for m in re.finditer(r"  - vessel: \d+\n(?:    .*\n)+", text):
        last = m
    added = "".join(e.replace(f"  - vessel: {RHINO_CLASS_ID}\n", f"  - vessel: {MARLIN_CLASS_ID}\n", 1) for e in donor)
    return text[:last.end()] + added + text[last.end():]


# ----------------------------------------------------------------------------- checks
def check_prefab(text: str, script_guid: str):
    errs = []

    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want("  m_Name: Marlin\n" in text, "Marlin.prefab: the root is not named Marlin")
    want("  _name: Marlin\n" in text, "Marlin.prefab: VesselStatus _name is not Marlin")
    want(f"  vesselType: {MARLIN_CLASS_ID}\n" in text, f"Marlin.prefab: vesselType is not {MARLIN_CLASS_ID}")
    want(f"  vesselType: {RHINO_CLASS_ID}\n" not in text, "Marlin.prefab: still carries the Rhino's vesselType")
    h = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(h and int(h.group(1)) == global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "Marlin.prefab: GlobalObjectIdHash is not this guid's (a copied hash collides with the Rhino in Netcode)")
    ip = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(ip and ip.group(1) == "0", "Marlin.prefab: InScenePlacedSourceGlobalObjectIdHash is not cleared")
    sa = component_list(text, SHIP_ACTIONS_GO)
    want(sa and f"{{fileID: {EXECUTOR_ID}}}" in sa.group(1), "Marlin.prefab: MarlinJacksExecutor is not on ShipActions")
    reg = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % REGISTRY_ID, text)
    want(reg and f"  - {{fileID: {EXECUTOR_ID}}}\n" in reg.group(1),
         "Marlin.prefab: the executor is not in the ActionExecutorRegistry (the buttons would reach nothing)")
    ex = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % EXECUTOR_ID, text)
    want(ex and f"guid: {script_guid}, type: 3" in ex.group(1), "Marlin.prefab: the executor document names another script")
    for key, guid in (("config", CONFIG_GUID), ("OnMiniGameTurnEnd", TURN_END_GUID), ("onCombatHitLanded", COMBAT_HIT_GUID)):
        want(ex and f"  {key}: {{fileID: 11400000, guid: {guid}, type: 2}}" in ex.group(1),
             f"Marlin.prefab: MarlinJacksExecutor.{key} is not wired")
    want(WANT_BINDINGS in text, "Marlin.prefab: the marlin's buttons are not ramp 0, teleport 1 + 4, decoy 6, call 7")
    want("  _gamepadActionOverrides: []\n" in text, "Marlin.prefab: the Rhino's gamepad overrides (the swipes) are still bound")
    want(f"guid: {CONTAINER_GUID}," in text and f"guid: {DONOR_CONTAINER_GUID}," not in text,
         "Marlin.prefab: the VesselImpactor is not on MarlinImpactorDataContainer (the Rhino's plough would ride along)")
    names = set(re.findall(r"^  m_Name: (.*)$", text, re.M))
    for n in RHINO_SUBTREES:
        want(n not in names, f"Marlin.prefab: the Rhino's {n} is still on the hull")
    for n in RHINO_COMPONENTS + ["ShieldSkimmerScaleDriver", "RhinoSwordAfterimage", "ShieldSwipeActionExecutor",
                                 "RhinoVesselHUDController"]:
        want(f"guid: {guid_of_script(n)}, type: 3" not in text, f"Marlin.prefab: the Rhino's {n} is still on the hull")
    hud = re.search(r"guid: %s, type: 3\}\n((?:.*\n)*?)(?=--- !u!|\Z)" % guid_of_script("MarlinJacksHUDController"), text)
    want(hud and f"  crew: {{fileID: {EXECUTOR_ID}}}\n" in hud.group(1), "Marlin.prefab: the HUD is not MarlinJacksHUDController on the executor")
    dangling = dangling_local_refs(text)
    want(not dangling, f"Marlin.prefab: dangling local fileID reference(s) {dangling[:5]}")
    want(f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}" in text and RHINO_CAMERA_GUID not in text,
         "Marlin.prefab: the camera settings are not MarlinCameraSettingsSO (a tune would move the Rhino's camera)")
    return errs + check_bodies(text)


def check_assets(config: str, controls, container: str):
    errs = []
    if f"guid: {guid_of_script('MarlinJacksConfigSO')}, type: 3" not in config:
        errs.append("MarlinJacksConfig.asset: does not name MarlinJacksConfigSO")
    if "  jackSeatPlaneEase:" not in config or "  viewPanEase:" not in config:
        errs.append("MarlinJacksConfig.asset: no jacks'-seat stability fields (run the tool)")
    if "  phoneShortMm:" not in config or "  thumbCover:" not in config:
        errs.append("MarlinJacksConfig.asset: no thumb-framed view fields (run the tool)")
    if "  herdHueLean:" not in config:
        errs.append("MarlinJacksConfig.asset: no herd mark field (run the tool)")
    action = guid_of_script("MarlinJacksActionSO")
    for name, value, text in controls:
        if f"guid: {action}, type: 3" not in text or f"  control: {value}\n" not in text:
            errs.append(f"{name}.asset: not a MarlinJacksActionSO with control {value}")
    for g in CONTAINER_PRISM + CONTAINER_CRYSTAL:
        if g not in container:
            errs.append(f"MarlinImpactorDataContainer: missing effect {g}")
    return errs


def check_registrations(container: str, network: str, lists, cls: str, kmap: str, fusion: str):
    errs = []

    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in container,
         "Vessel Prefab Container: no Marlin entry on its root TRANSFORM (the Vessel Changer offers only what is here)")
    want(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in network,
         "DefaultNetworkPrefabs: no Marlin entry on its root GAMEOBJECT (a swap to it cannot spawn)")
    for name, text in lists:
        want(text.count(CLASS_GUID) == 1, f"{name}: SO_Class_Marlin listed {text.count(CLASS_GUID)} times, not once")
    want(f"  Class: {MARLIN_CLASS_ID}\n" in cls and "  ownedFromStart: 1\n" in cls and "  isLocked: 0\n" in cls,
         "SO_Class_Marlin: wrong class, locked, or not owned from the start")
    want(CREW in cls, "SO_Class_Marlin: the card does not name both subvessels (CrewNames Marlin, Jacks)")
    for field, guid in CARD_FIELDS.items():
        want(re.search(r"  %s: \{fileID: 21300000, guid: %s," % (field, guid), cls) is not None,
             f"SO_Class_Marlin: {field} is not the Marlin's rendered card (render_cards.py Marlin)")
    want(f"  vesselClass: {MARLIN_CLASS_ID}\n" in kmap and "  m_Name: Marlin\n" in kmap, "ElementalAbilityMaps/Marlin: wrong class or name")
    want("Sword" not in kmap, "ElementalAbilityMaps/Marlin: still the Rhino's cards")
    marlin, rhino = fusion_entries(fusion, MARLIN_CLASS_ID), fusion_entries(fusion, RHINO_CLASS_ID)
    want(len(marlin) == 4, f"CrystalHullFusionConfig: {len(marlin)} Marlin entries, not 4")
    want([e.split("\n", 1)[1] for e in marlin] == [e.split("\n", 1)[1] for e in rhino],
         "CrystalHullFusionConfig: the Marlin's entries are not the Rhino's (it draws the Rhino's mesh)")
    return errs


def control_path(name):
    return os.path.join(ACTIONS, f"{name}.asset")


def check_all():
    script_guid = guid_of_script("MarlinJacksExecutor")
    if not os.path.exists(PREFAB) or not os.path.exists(PREFAB + ".meta"):
        return ["Marlin.prefab (+ .meta) is missing: run Tools/Build/author_marlin_assets.py"]
    errs = [] if guid_of(PREFAB + ".meta") == PREFAB_GUID else ["Marlin.prefab.meta: guid is not PREFAB_GUID"]
    errs += check_prefab(read(PREFAB), script_guid)
    paths = [CLASS_ASSET, MAP, CONFIG, MARLIN_CONTAINER, CAMERA] + [control_path(n) for n, _, _ in CONTROLS]
    for path in paths:
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    for path, guid in [(CLASS_ASSET, CLASS_GUID), (MAP, MAP_GUID), (CONFIG, CONFIG_GUID), (MARLIN_CONTAINER, CONTAINER_GUID),
                       (CAMERA, CAMERA_GUID)] + \
                      [(control_path(n), g) for n, _, g in CONTROLS]:
        if guid_of(path + ".meta") != guid:
            errs.append(f"{os.path.relpath(path, ROOT)}.meta: guid is not the one the prefab names")
    errs += check_assets(read(CONFIG), [(n, v, read(control_path(n))) for n, v, _ in CONTROLS], read(MARLIN_CONTAINER))
    errs += check_registrations(read(CONTAINER), read(NETWORK_PREFABS), [(os.path.basename(p), read(p)) for p in CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(FUSION))
    return errs


def author():
    script_guid = guid_of_script("MarlinJacksExecutor")
    if not os.path.exists(PREFAB):   # the spent one-shot stands down once the prefab is committed
        write(PREFAB, build_clone(read(DONOR), script_guid))
        write(PREFAB + ".meta", prefab_meta())
    else:
        old = read(PREFAB); new = retrofit(old)
        if new != old:
            write(PREFAB, new)
    os.makedirs(ACTIONS, exist_ok=True)
    if not os.path.exists(ACTIONS + ".meta"):
        write(ACTIONS + ".meta", "fileFormatVersion: 2\nguid: 6b3d9e1f4a2c4875b0e7d3a9c1f6b284\nfolderAsset: yes\n"
                                 "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if not os.path.exists(CONFIG):
        write(CONFIG, config_asset(guid_of_script("MarlinJacksConfigSO"))); write(CONFIG + ".meta", native_meta(CONFIG_GUID))
    else:
        old = read(CONFIG); new = config_retrofit(old)
        if new != old:
            write(CONFIG, new)
    action = guid_of_script("MarlinJacksActionSO")
    for name, value, guid in CONTROLS:
        if not os.path.exists(control_path(name)):
            write(control_path(name), control_asset(action, name, value)); write(control_path(name) + ".meta", native_meta(guid))
    if not os.path.exists(CAMERA):
        write(CAMERA, replace_once(read(RHINO_CAMERA), "  m_Name: RhinoCameraSettingsSO\n", "  m_Name: MarlinCameraSettingsSO\n",
                                   "camera m_Name"))
        write(CAMERA + ".meta", native_meta(CAMERA_GUID))
    if not os.path.exists(MARLIN_CONTAINER):
        write(MARLIN_CONTAINER, container_asset()); write(MARLIN_CONTAINER + ".meta", native_meta(CONTAINER_GUID))
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset(read(DONOR_CLASS))); write(CLASS_ASSET + ".meta", native_meta(CLASS_GUID))
    else:
        old = read(CLASS_ASSET); new = cards_retrofit(old)
        if new != old:
            write(CLASS_ASSET, new)
    if not os.path.exists(MAP):
        write(MAP, build_map(read(DONOR_MAP))); write(MAP + ".meta", native_meta(MAP_GUID))
    for path, fn in [(CONTAINER, register_container), (NETWORK_PREFABS, register_network), (FUSION, register_fusion)] + \
                    [(p, register_class_list) for p in CLASS_LISTS]:
        old = read(path); new = fn(old)
        if new != old:
            write(path, new)


def self_test():
    """Every check must fire on a broken copy (negative controls), and pass on a fresh clone."""
    script_guid = guid_of_script("MarlinJacksExecutor")
    good = build_clone(read(DONOR), script_guid)
    fails = []

    def expect(errs, n_min, what):
        if not ((len(errs) == 0) if n_min == 0 else (len(errs) >= n_min)):
            fails.append(f"{what}: {errs}")
    expect(check_prefab(good, script_guid), 0, "a fresh clone passes")
    expect(check_prefab(good.replace(f"  vesselType: {MARLIN_CLASS_ID}\n", f"  vesselType: {RHINO_CLASS_ID}\n"), script_guid), 1, "the Rhino's vesselType")
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>1124080984", good, count=1), script_guid), 1, "the donor's hash")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {EXECUTOR_ID}}}\n", ""), script_guid), 1, "the executor off ShipActions")
    expect(check_prefab(good.replace(f"  - {{fileID: {EXECUTOR_ID}}}\n", ""), script_guid), 1, "the executor out of the registry")
    expect(check_prefab(good.replace(f"guid: {CONFIG_GUID}, type: 2", "guid: " + "0" * 32 + ", type: 2"), script_guid), 1, "the config unwired")
    expect(check_prefab(good.replace(WANT_BINDINGS, "  _inputEventShipActions: []\n"), script_guid), 1, "no buttons")
    expect(check_prefab(good.replace(CONTAINER_GUID, DONOR_CONTAINER_GUID), script_guid), 1, "the Rhino's container")
    expect(check_prefab(read(DONOR).replace("  m_Name: Rhino\n", "  m_Name: Marlin\n"), script_guid), 3, "the Rhino untouched")
    expect(check_prefab(good + "  stray: {fileID: 987654321}\n", script_guid), 1, "a dangling local reference")
    expect(check_prefab(good.replace(RIG_OFF, ""), script_guid), 1, "the Rhino's rig still drawn")
    expect(check_prefab(good.replace(CAMERA_GUID, RHINO_CAMERA_GUID), script_guid), 1, "the Rhino's camera settings")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {TELEMETRY_ID}}}\n", ""), script_guid), 1, "no telemetry")
    expect(check_prefab(good.replace(icon_override(*ICON_SPRITES[2]), ""), script_guid), 1, "the Rhino's Space icon")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {BODIES_ID}}}\n", ""), script_guid), 1, "no bodies component")
    expect(check_prefab(good.replace(f"  _shipGeometries:\n  - {{fileID: {MARLIN_GO_ID}}}\n",
                                     f"  _shipGeometries:\n  - {{fileID: {FUSE_GO}}}\n"), script_guid), 1, "the Rhino's ship geometry")
    expect(check_prefab(good.replace("      value: Jack B\n", "      value: Jack\n"), script_guid), 1, "a misnamed jack")
    if retrofit(good) != good:
        fails.append("the retrofit is not idempotent")
    if DOC.search(good) is None:
        fails.append("the clone has no documents")
    controls = [(n, v, control_asset(guid_of_script("MarlinJacksActionSO"), n, v)) for n, v, _ in CONTROLS]
    cfg, cont = config_asset(guid_of_script("MarlinJacksConfigSO")), container_asset()
    expect(check_assets(cfg, controls, cont), 0, "fresh assets pass")
    expect(check_assets(cfg.replace("  jackSeatPlaneEase: 0.25\n", ""), controls, cont), 1, "no stability fields")
    expect(check_assets(cfg.replace(THUMB_VIEW_FIELDS, ""), controls, cont), 1, "no thumb-framed view fields")
    expect(check_assets(cfg.replace(HERD_MARK_FIELDS, ""), controls, cont), 1, "no herd mark field")
    if config_retrofit(cfg) != cfg or "jackSeatPlaneEase" not in config_retrofit(cfg.replace(STABILITY_FIELDS, "")):
        fails.append("the config retrofit is not idempotent or does not add the stability fields")
    if config_retrofit(cfg.replace(THUMB_VIEW_FIELDS, "").replace(HERD_MARK_FIELDS, "")) != cfg:
        fails.append("the config retrofit does not put the thumb view and herd mark fields back where the C# declares them")
    expect(check_assets(cfg, [(n, v, t.replace("  control: ", "  control: 9")) for n, v, t in controls], cont), 1, "a wrong control")
    expect(check_assets(cfg, controls, cont.replace(CONTAINER_PRISM[1], "0" * 32)), 1, "a missing effect")
    container, network, fusion = register_container(read(CONTAINER)), register_network(read(NETWORK_PREFABS)), register_fusion(read(FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in CLASS_LISTS]
    cls, kmap = class_asset(read(DONOR_CLASS)), build_map(read(DONOR_MAP))
    expect(check_registrations(container, network, lists, cls, kmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, kmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, kmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, kmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls.replace(CREW, ""), kmap, fusion), 1, "a card without its crew")
    expect(check_registrations(container, network, lists, cls.replace(CARD_FIELDS["SquadImage"], "2286f04c9031ee84d8d935de1926f92a"), kmap,
                              fusion), 1, "the Rhino's squad image")
    if cards_retrofit(cls) != cls:
        fails.append("the card retrofit is not idempotent")
    expect(check_registrations(container, network, lists, cls, read(DONOR_MAP), fusion), 1, "the Rhino's own map")
    expect(check_registrations(container, network, lists, cls, kmap, fusion.replace(f"  - vessel: {MARLIN_CLASS_ID}\n", "  - vessel: 99\n", 1)), 1, "a missing fusion entry")
    if register_container(container) != container or register_fusion(fusion) != fusion or register_network(network) != network:
        fails.append("registration is not idempotent")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_marlin_assets self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errs = check_all()
    for e in errs:
        print("FAIL:", e)
    print("author_marlin_assets:", "ok" if not errs else f"{len(errs)} problem(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
