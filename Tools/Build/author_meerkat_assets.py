#!/usr/bin/env python3
"""Author the Meerkat (the wormhole vessel) from the Stoat's prefab and register it, then prove the shipped set.

    python3 Tools/Build/author_meerkat_assets.py             # author what is missing, then check
    python3 Tools/Build/author_meerkat_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_meerkat_assets.py --self-test # prove every check fires

The Meerkat is the Vessel Studio's Meerkat page (Docs/Studios/VesselStudio/meerkat.html, Docs/Studios/MEERKAT.md), cloned
into the game (/artifact-to-unity): the Stoat's Gate mode made its own vessel (Yash, 2026-10-27: "all its things similar to
the Stoat. Just rename to Meerkat and keep only one ability of the wormhole"). "Bring it to Unity" ends in the Toybox's
Vessel Changer (Garrett, 2026-10-17), which offers what ToyVesselRoster.Default names (CODE, not written here) AND the
Vessel Prefab Container can spawn.

WHAT IT MAKES (R_VesselActions/MEERKAT.md)
    Assets/_Prefabs/Spacevessels/Meerkat.prefab (+ .meta)   a text clone of Stoat.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 28` (VesselClassType.Meerkat)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
        - ONE THUMB: the transformer is MeerkatVesselTransformer (a SingleStickVesselTransformer that lets the tunnel fly
          the hull), at the studio's flight: PitchScaler = YawScaler = 120 (pitchYaw), RollScaler 15.6 (bankShare 0.12 x
          roll 130), cruise 60 (the Stoat's DefaultThrottleScaler, kept)
        - ONE ABILITY: StoatSlingExecutor's slot becomes MeerkatWormholeExecutor (MeerkatWormholeConfig, Runtime GameData);
          the Stoat's dipole, pathfinder, mode switch and ripple executors are removed (the Stoat keeps them)
        - the bindings, every device: RT (InputEvents 1) -> MeerkatWormholeFormAction, LT (2) -> MeerkatWormholeShortenAction,
          X (6) -> StoatHoldAction (the stance, which the studio's Meerkat keeps); no per-device overrides
        - MeerkatWormholeNetworkRelay on the root (every peer sees every tunnel)
        - StoatHUDController -> MeerkatHUDController (the Stoat's HUD view as a stand-in; the Time card shows the reticle)
      Everything else is the Stoat's: the studio flies the Stoat's model and lope as the Meerkat's stand-in hull.
    Assets/_SO_Assets/VesselActions/Meerkat/: MeerkatWormholeConfig.asset (every studio key at its SHIPPED value - a
      one-shot: after it exists only studio_to_unity.py writes its numbers), MeerkatWormholeFormAction / ShortenAction.
    SO_Class_Meerkat (the Stoat's icons as placeholders, owned from the start) + its entry in SO_Classlist_All / _Classes;
    one entry each in `Vessel Prefab Container.asset` (the root TRANSFORM) and `DefaultNetworkPrefabs.asset` (the root
    GAMEOBJECT); Resources/ElementalAbilityMaps/Meerkat.asset (four OPEN design slots: which element the wormhole is, and
    the other three abilities, are not designed - /vessel section 3); four CrystalHullFusionConfig entries (the Stoat's:
    the Meerkat draws the Stoat's mesh).

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while Meerkat.prefab is absent, and
`--check` validates the SHIPPED prefab's invariants rather than diffing it against a fresh clone of a donor that will move
on. To re-author from scratch, delete Meerkat.prefab and its .meta and run again.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)
from author_wrasse_assets import (split_docs, dangling_local_refs, native_meta, read, write,  # noqa: E402
                                  guid_of, guid_of_script, DOC)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Stoat.prefab")
PREFAB = os.path.join(VESSELS, "Meerkat.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
DONOR_CLASS = os.path.join(CLASSES, "SO_Class_Stoat.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Meerkat.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
DONOR_MAP = os.path.join(MAPS, "Stoat.asset")
MAP = os.path.join(MAPS, "Meerkat.asset")
FUSION = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
ACTIONS = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "Meerkat")
CONFIG = os.path.join(ACTIONS, "MeerkatWormholeConfig.asset")
FORM = os.path.join(ACTIONS, "MeerkatWormholeFormAction.asset")
SHORTEN = os.path.join(ACTIONS, "MeerkatWormholeShortenAction.asset")
STUDIO = os.path.join(ROOT, "Docs", "Studios", "VesselStudio", "meerkat.html")
CAMERAS = os.path.join(ROOT, "Assets", "_SO_Assets", "Camera")
DONOR_CAMERA = os.path.join(CAMERAS, "StoatCameraSettingsSO.asset")
CAMERA = os.path.join(CAMERAS, "MeerkatCameraSettingsSO.asset")

# 24 Flea (claude/flea-vessel), 25-27 Warden / Bulwark / Tempest (cece/keen-feynman-unrdda): checked across every origin
# branch 2026-10-27, so the Meerkat is 28.
MEERKAT_CLASS_ID = 28
STOAT_CLASS_ID = 15
PREFAB_GUID = "4e2a8c1f7b9d4f3e8a6c5b2d1e0f9a87"
CLASS_GUID = "8d3f1a2b4c5e4d6f9a7b8c9d0e1f2a3b"
MAP_GUID = "2b7e4c9a1d3f4a5b8c6d7e8f9a0b1c2d"
ACTIONS_DIR_GUID = "6c1d9e2f3a4b4c5d8e7f6a5b4c3d2e1f"
CONFIG_GUID = "9a8b7c6d5e4f4a3b2c1d0e9f8a7b6c5d"
FORM_GUID = "1f2e3d4c5b6a4978a8b7c6d5e4f3a2b1"
SHORTEN_GUID = "3c4d5e6f7a8b4c9d8e0f1a2b3c4d5e6f"
STOAT_HOLD_GUID = "9b50ea11259b4b1aa750eea7f1d7f781"   # StoatHoldAction (X: the stance)
GAME_DATA_GUID = "b35f33752bb10a44cb5033b5670f50aa"    # Runtime GameData (the domain palette)
STOAT_CAMERA_GUID = "2fb2a0bb0eaf4bd1863fdf55490fa869"  # StoatCameraSettingsSO (the clone's, until round 10)
CAMERA_GUID = "5e1e0cd061ab49dbbb24bf9c1080387b"        # MeerkatCameraSettingsSO

ROOT_GO = "6417075533431866457"              # the Stoat's root GameObject
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "1733035045152327888"    # the NetworkObject component on it
REGISTRY_ID = "9105512684531825410"          # ActionExecutorRegistry
WORMHOLE_ID = "5137264980012345601"          # StoatSlingExecutor's slot -> MeerkatWormholeExecutor
STANCE_ID = "5137264980012345602"            # ToggleTranslationModeActionExecutor (kept)
STOAT_ONLY = ["5137264980012345603", "5137264980012345604", "5137264980012345605", "5137264980012345606"]
HUD_ID = "5649393235309590662"               # StoatHUDController -> MeerkatHUDController
HUD_VIEW_ID = "5137264980012345622"          # StoatHUDView in the nested StoatHUDVariant
TRANSFORMER_ID = "7686683633640807837"
RELAY_ID = "2828000000000000001"             # MeerkatWormholeNetworkRelay on the root

RIGHT_STICK_ACTION, LEFT_STICK_ACTION, BUTTON1_ACTION = 1, 2, 6
TURN = {"PitchScaler": "120", "YawScaler": "120", "RollScaler": "15.6"}   # the studio's pitchYaw, bankShare x roll
STOAT_TURN = {"PitchScaler": "48", "YawScaler": "48", "RollScaler": "52"}

SCRIPTS = {   # script name -> its .cs.meta (the guids every prefab and asset here names)
    "MeerkatWormholeExecutor": "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/MeerkatWormholeExecutor.cs.meta",
    "MeerkatWormholeConfigSO": "Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/MeerkatWormholeConfigSO.cs.meta",
    "MeerkatWormholeActionSO": "Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/MeerkatWormholeActionSO.cs.meta",
    "MeerkatVesselTransformer": "Assets/_Scripts/Controller/Vessel/MeerkatVesselTransformer.cs.meta",
    "MeerkatWormholeNetworkRelay": "Assets/_Scripts/Controller/Vessel/MeerkatWormholeNetworkRelay.cs.meta",
    "MeerkatHUDController": "Assets/_Scripts/UI/Controller/MeerkatHUDController.cs.meta",
}


def script_guid(name: str) -> str:
    return guid_of(os.path.join(ROOT, SCRIPTS[name]))


# ----------------------------------------------------------------------------- the studio's shipped numbers
CONFIG_KEYS = ["reticleAhead", "bendMaxDegrees", "shaftMin", "shaftMax", "bendRadius", "throatDiameter", "mouthDiameter",
               "keyRampSeconds", "rideEntryMultiplier", "rideMidMultiplier", "rideExitMultiplier", "rideSpeedTunnelScale", "reticleDelaySeconds", "reticleGrowSeconds", "formSeconds", "staySeconds",
               "collapseSeconds", "warpStrength", "dentStrength", "rippleStrength", "rippleEdge", "rippleSeconds",
               "reticleSmoothSeconds", "reticleIdleAlpha", "reticleWhite", "bridgeVisibility", "bridgeGlass", "bridgeRings", "bridgeSpindlesPerRing",
               "bridgeRibs", "bridgeSpindleThickness", "bridgeShimmer", "bridgeWhite", "aiWormhole", "aiStraightMinDistance",
               "aiBentMinDistance", "aiShapeSeconds", "aiNoise"]


def shipped(page: str) -> dict:
    """The page's SHIPPED block, key -> number (the first value of a key, the page's base)."""
    m = re.search(r"const SHIPPED = \{(.*?)\n  \};", page, re.S)
    if not m:
        raise RuntimeError("meerkat.html: SHIPPED block not found")
    out = {}
    for k, v in re.findall(r"\b([A-Za-z]\w*): (-?[\d.]+)", re.sub(r"//.*", "", m.group(1))):
        out.setdefault(k, v)
    return out


def num(v: str) -> str:
    f = float(v)
    return str(int(f)) if f == int(f) else repr(f)


def empty_event(name: str) -> str:
    return (f"  {name}:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n")


def config_asset(values: dict) -> str:
    body = "".join(f"  {k}: {num(values[k])}\n" for k in CONFIG_KEYS)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid('MeerkatWormholeConfigSO')}, type: 3}}\n"
            "  m_Name: MeerkatWormholeConfig\n  m_EditorClassIdentifier: \n" + body +
            empty_event("formEvent") + empty_event("collapseEvent"))


# Studio keys renamed after the config was first written: old -> new (round 6: the ride's three multipliers).
RENAMED = {"throatSpeed": "rideMidMultiplier"}
# Studio keys the page retired (round 7, the umbrella handle: a fixed narrow throat and mouth, LT sets the shaft before a
# bend of fixed radius). Their lines leave the asset; the field is gone from MeerkatWormholeConfigSO.
RETIRED = ["lengthStraight", "lengthBent", "throatStraight", "throatBent", "mouthFlare",
           # round 9 (Yash, 2026-10-28: "the reticle is the silhouette of the geometry"): the reticle is the bridge's wireframe,
           # so its own geometry, glass and path go, and Garrett's reticleLook switch with them (one reticle now)
           "reticleGlass", "reticleRings", "reticleSpindlesPerRing", "reticleRibs", "reticleSpindleThickness", "reticlePath",
           "reticleLook",
           # round 10 (Yash, 2026-10-28): diameters ("the diameter of the entry exit pair ... and the inner diameter"), and the
           # ride camera is the follow camera's own distance with no blend (the hull always at the centre of the screen)
           "throatRadius", "mouthRadius", "rideCameraBack", "rideCameraLookAhead", "rideCameraBlendSeconds"]


def config_retrofit(cfg: str, values: dict) -> str:
    """A key the studio added after the config was first written joins it at the page's SHIPPED value (round 3: the reticle's
    feel and spindles); a key already there is never touched - its number is studio_to_unity.py's. Idempotent."""
    for old, new in RENAMED.items():   # a key the studio renamed keeps its number under the new name (FormerlySerializedAs)
        if not re.search(r"^  %s: " % new, cfg, re.M):
            cfg = re.sub(r"^  %s: " % old, f"  {new}: ", cfg, count=1, flags=re.M)
    for k in RETIRED:
        cfg = re.sub(r"^  %s: .*\n" % k, "", cfg, flags=re.M)
    for i, k in enumerate(CONFIG_KEYS):   # a new key goes in after the key before it, so the asset reads in the page's order
        if re.search(r"^  %s: " % k, cfg, re.M):
            continue
        prev = next((m for m in (re.search(r"^  %s: .*\n" % q, cfg, re.M) for q in reversed(CONFIG_KEYS[:i])) if m), None)
        at = prev.end() if prev else cfg.index("  formEvent:\n")
        cfg = cfg[:at] + f"  {k}: {num(values[k])}\n" + cfg[at:]
    return cfg


def action_asset(name: str, trigger: int) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid('MeerkatWormholeActionSO')}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier: \n  trigger: {trigger}\n")


# Round 10 (Yash, 2026-10-28: "the cameras, why offset should always be zero, the vessel should be in the center of the
# screen and this way we don't need to lerp the camera during entering into the tunnel. It is like a sparrow, it's always
# center"): the Meerkat's own camera settings, forked from the Stoat's (whose camera stays as it is): no height, no look
# lift, and hard-attached (chaseEaseRate 0 with mode 0: CustomCameraController puts the camera exactly at its offset), so
# the hull sits at the centre of the screen. A one-shot: after it exists, its numbers are the asset's.
CAMERA_CENTRED = {"followOffset": "{x: 0, y: 0, z: -21}", "lookAheadLift": "0", "chaseEaseRate": "0"}


def camera_asset(donor: str) -> str:
    text = replace_once(donor, "  m_Name: StoatCameraSettingsSO\n", "  m_Name: MeerkatCameraSettingsSO\n", "camera m_Name")
    for k, v in CAMERA_CENTRED.items():
        text = re.sub(r"^  %s: .*$" % k, f"  {k}: {v}", text, count=1, flags=re.M)
    return text


def check_camera(text: str):
    errs = []
    if "  mode: 0\n" not in text:
        errs.append("MeerkatCameraSettingsSO: mode is not 0 (a dynamic camera smooths, so the hull drifts off centre)")
    m = re.search(r"^  followOffset: \{x: (-?[\d.]+), y: (-?[\d.]+), z: (-?[\d.]+)\}$", text, re.M)
    if not m or float(m.group(1)) != 0 or float(m.group(2)) != 0:
        errs.append("MeerkatCameraSettingsSO: followOffset has an x or y offset (the hull is not at the centre of the screen)")
    for k in ("lookAheadLift", "chaseEaseRate"):
        m = re.search(r"^  %s: (-?[\d.]+)$" % k, text, re.M)
        if not m or float(m.group(1)) != 0:
            errs.append(f"MeerkatCameraSettingsSO: {k} is not 0 (the hull is not held at the centre)")
    return errs


def folder_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the prefab
def binding(ie: int, guid: str) -> str:
    return f"  - InputEvent: {ie}\n    ShipActions:\n    - {{fileID: 11400000, guid: {guid}, type: 2}}\n"


MEERKAT_BINDINGS = ("  _inputEventShipActions:\n" + binding(RIGHT_STICK_ACTION, FORM_GUID) +
                    binding(LEFT_STICK_ACTION, SHORTEN_GUID) + binding(BUTTON1_ACTION, STOAT_HOLD_GUID))
ANY_LIST = r"  {key}:(?: \[\])?\n(?:  [- ] .*\n)*"


def set_list(text: str, key: str, block: str) -> str:
    return re.sub(ANY_LIST.format(key=key), lambda m: block, text, count=1)


def relay_block() -> str:
    return (f"--- !u!114 &{RELAY_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid('MeerkatWormholeNetworkRelay')}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n")


def retrofit(text: str) -> str:
    """Everything that makes the clone the Meerkat. Idempotent: run on the clone and on the committed prefab."""
    head, docs = split_docs(text)
    out = []
    removed = set(STOAT_ONLY)
    for _, anchor, d in docs:
        if anchor in removed:
            continue
        for r in removed:
            d = re.sub(r"^  - component: \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"^  - \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"\{fileID: %s\}" % r, "{fileID: 0}", d)
        if anchor == WORMHOLE_ID and f"guid: {script_guid('MeerkatWormholeExecutor')}" not in d:
            d = re.sub(r"  m_Script: \{[^}]*\}\n", f"  m_Script: {{fileID: 11500000, guid: {script_guid('MeerkatWormholeExecutor')}, type: 3}}\n", d)
            d = re.sub(r"  config: \{[^}]*\}\n",
                       f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}\n"
                       f"  gameData: {{fileID: 11400000, guid: {GAME_DATA_GUID}, type: 2}}\n", d)
        if anchor == HUD_ID and f"guid: {script_guid('MeerkatHUDController')}" not in d:
            d = re.sub(r"  m_Script: \{[^}]*\}\n", f"  m_Script: {{fileID: 11500000, guid: {script_guid('MeerkatHUDController')}, type: 3}}\n", d)
            d = d.replace("Assembly-CSharp::CosmicShore.UI.StoatHUDController", "Assembly-CSharp::CosmicShore.UI.MeerkatHUDController")
            d = re.sub(r"^  (mode|dipole|sling|pathfinder): \{[^}]*\}\n", "", d, flags=re.M)
            d = d.rstrip("\n") + f"\n  wormhole: {{fileID: {WORMHOLE_ID}}}\n"
        if anchor == TRANSFORMER_ID:
            d = re.sub(r"  m_Script: \{[^}]*\}\n", f"  m_Script: {{fileID: 11500000, guid: {script_guid('MeerkatVesselTransformer')}, type: 3}}\n", d)
            if all(f"\n  {k}: {v}\n" in d for k, v in STOAT_TURN.items()):   # written once: never over a value tuned after
                for k, v in TURN.items():
                    d = d.replace(f"\n  {k}: {STOAT_TURN[k]}\n", f"\n  {k}: {v}\n")
        if "_inputEventShipActions:" in d:
            d = set_list(d, "_inputEventShipActions", MEERKAT_BINDINGS)
            d = set_list(d, "_touchActionOverrides", "  _touchActionOverrides: []\n")
            d = set_list(d, "_gamepadActionOverrides", "  _gamepadActionOverrides: []\n")
        out.append(d)
    text = head + "".join(out)
    text = text.replace(f"  settings: {{fileID: 11400000, guid: {STOAT_CAMERA_GUID}, type: 2}}",
                        f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}")
    if f"--- !u!114 &{RELAY_ID}\n" not in text:
        text = text.rstrip("\n") + "\n" + relay_block()
    m = re.search(r"(--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n(?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    if m and f"  - component: {{fileID: {RELAY_ID}}}\n" not in m.group(1):
        text = text[:m.end(1)] + f"  - component: {{fileID: {RELAY_ID}}}\n" + text[m.end(1):]
    return text


def build_clone(donor: str) -> str:
    text = replace_once(donor, "  m_Name: Stoat\n", "  m_Name: Meerkat\n", "root GameObject name")
    text = replace_once(text, "  _name: Stoat\n", "  _name: Meerkat\n", "VesselStatus _name")
    text = replace_once(text, f"  vesselType: {STOAT_CLASS_ID}\n", f"  vesselType: {MEERKAT_CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)
    return retrofit(text)


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the class, the map, the registrations
DESCRIPTION = ("  Description: Digs its own way. The reticle on its nose is the tunnel it would form; bend it with\n"
               "    the right stick, shorten it with LT, and RT ripples the wormhole open and dives you through it,\n"
               "    out the far end with the speed you went in with. Everyone sees the bridge it leaves.\n")


def class_asset(donor_class: str) -> str:
    text = replace_once(donor_class, "  m_Name: SO_Class_Stoat\n", "  m_Name: SO_Class_Meerkat\n", "class m_Name")
    text = replace_once(text, f"  Class: {STOAT_CLASS_ID}\n", f"  Class: {MEERKAT_CLASS_ID}\n", "class Class")
    text = replace_once(text, "  Name: Stoat\n", "  Name: Meerkat\n", "class Name")
    text, n = re.subn(r"  Description: .*\n(?:    .*\n)*", lambda m: DESCRIPTION, text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class Description was not found")
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


OPEN_SLOT = ("(open design slot) The wormhole is the Meerkat's one ability so far (Docs/Studios/MEERKAT.md): which "
             "element it is, the other three abilities and the level-5 upgrades are not designed yet.")
OPEN_SLOT_YAML = OPEN_SLOT.replace("'", "''")   # inside a single-quoted YAML scalar an apostrophe is doubled


def map_asset(donor_map: str) -> str:
    text = replace_once(donor_map, "  m_Name: Stoat\n", "  m_Name: Meerkat\n", "map name")
    text = replace_once(text, f"  vesselClass: {STOAT_CLASS_ID}\n", f"  vesselClass: {MEERKAT_CLASS_ID}\n", "map vesselClass")
    entries = "  entries:\n" + "".join(
        f"  - Element: {e}\n    AbilityLabel: '(open design slot)'\n    AbilityDescription: '{OPEN_SLOT_YAML}'\n"
        "    Input: 0\n    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n"
        "    UpgradeLabel: \n    UpgradeDescription: \n" for e in (3, 4, 2, 1))
    return text[:text.index("  entries:\n")] + entries


def fusion_entries(text: str, vessel: int):
    return re.findall(r"  - vessel: %d\n(?:    .*\n)+" % vessel, text)


def register_fusion(text: str) -> str:
    if fusion_entries(text, MEERKAT_CLASS_ID):
        return text
    donor = fusion_entries(text, STOAT_CLASS_ID)
    if len(donor) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Stoat entries, found {len(donor)}")
    last = None
    for m in re.finditer(r"  - vessel: \d+\n(?:    .*\n)+", text):
        last = m
    added = "".join(e.replace(f"  - vessel: {STOAT_CLASS_ID}\n", f"  - vessel: {MEERKAT_CLASS_ID}\n", 1) for e in donor)
    return text[:last.end()] + added + text[last.end():]


# ----------------------------------------------------------------------------- checks
def doc_of(text: str, anchor: str) -> str:
    m = re.search(r"--- !u!\d+ &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % anchor, text)
    return m.group(1) if m else ""


def check_prefab(text: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want("  m_Name: Meerkat\n" in text and "  _name: Meerkat\n" in text, "Meerkat.prefab: the root or VesselStatus is not named Meerkat")
    want(f"  vesselType: {MEERKAT_CLASS_ID}\n" in text and f"  vesselType: {STOAT_CLASS_ID}\n" not in text,
         f"Meerkat.prefab: vesselType is not {MEERKAT_CLASS_ID}")
    h = re.search(r"  GlobalObjectIdHash: (\d+)\n", doc_of(text, NETWORK_OBJECT_ID))
    want(h and int(h.group(1)) == global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "Meerkat.prefab: GlobalObjectIdHash is not this guid's (a copied hash collides with the Stoat in Netcode)")
    want("  InScenePlacedSourceGlobalObjectIdHash: 0\n" in doc_of(text, NETWORK_OBJECT_ID),
         "Meerkat.prefab: InScenePlacedSourceGlobalObjectIdHash is not cleared")
    w = doc_of(text, WORMHOLE_ID)
    want(f"guid: {script_guid('MeerkatWormholeExecutor')}, type: 3" in w and f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}" in w,
         "Meerkat.prefab: the wormhole executor is missing or not wired to MeerkatWormholeConfig (RT would form nothing)")
    reg = doc_of(text, REGISTRY_ID)
    want(f"  - {{fileID: {WORMHOLE_ID}}}\n" in reg and f"  - {{fileID: {STANCE_ID}}}\n" in reg,
         "Meerkat.prefab: the ActionExecutorRegistry does not list the wormhole and the stance executors")
    for sid in STOAT_ONLY:
        want(f"&{sid}\n" not in text and f"fileID: {sid}}}" not in text, f"Meerkat.prefab: the Stoat's executor {sid} is still on the hull")
    for name in ("StoatDipoleExecutor", "StoatPathfinderExecutor", "StoatModeExecutor", "StoatRippleExecutor",
                 "StoatSlingExecutor", "StoatHUDController"):
        want(f"guid: {guid_of_script(name)}, type: 3" not in text, f"Meerkat.prefab: the Stoat's {name} is still on the hull (one ability only)")
    t = doc_of(text, TRANSFORMER_ID)
    want(f"guid: {script_guid('MeerkatVesselTransformer')}, type: 3" in t, "Meerkat.prefab: the transformer is not MeerkatVesselTransformer (no ride, no one-thumb flight)")
    hud = doc_of(text, HUD_ID)
    want(f"guid: {script_guid('MeerkatHUDController')}, type: 3" in hud and f"  wormhole: {{fileID: {WORMHOLE_ID}}}\n" in hud,
         "Meerkat.prefab: the HUD is not MeerkatHUDController wired to the wormhole")
    m = re.search(r"  _inputEventShipActions:\n(?:  [- ] .*\n)*", text)
    want(m and m.group(0) == MEERKAT_BINDINGS,
         "Meerkat.prefab: the bindings are not RT -> MeerkatWormholeFormAction, LT -> MeerkatWormholeShortenAction, X -> StoatHoldAction")
    want("  _touchActionOverrides: []\n" in text and "  _gamepadActionOverrides: []\n" in text,
         "Meerkat.prefab: a per-device override still binds the Stoat's actions")
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    want(root and f"{{fileID: {RELAY_ID}}}" in root.group(1) and
         f"guid: {script_guid('MeerkatWormholeNetworkRelay')}, type: 3" in doc_of(text, RELAY_ID),
         "Meerkat.prefab: MeerkatWormholeNetworkRelay is not on the root (other players would not see the tunnels)")
    want(f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}" in text and STOAT_CAMERA_GUID not in text,
         "Meerkat.prefab: the camera is not MeerkatCameraSettingsSO (the Stoat's camera sits above and behind the hull)")
    want("  - component: {fileID: 0}\n" not in text, "Meerkat.prefab: a GameObject lists a null component")
    dangling = dangling_local_refs(text)
    want(not dangling, f"Meerkat.prefab: dangling local fileID reference(s) {dangling[:5]}")
    return errs


def check_config(cfg: str, values: dict):
    errs = []
    for k in CONFIG_KEYS:
        if not re.search(r"^  %s: -?[\d.]+\n" % k, cfg, re.M):
            errs.append(f"MeerkatWormholeConfig.asset: no `{k}` (every studio key is a field, by the same name)")
    for k in RETIRED:
        if re.search(r"^  %s: " % k, cfg, re.M):
            errs.append(f"MeerkatWormholeConfig.asset: `{k}` is retired (round 7) and still in the asset")
    if f"guid: {script_guid('MeerkatWormholeConfigSO')}, type: 3" not in cfg:
        errs.append("MeerkatWormholeConfig.asset: does not name MeerkatWormholeConfigSO")
    return errs


def check_registrations(container, network, lists, cls, kmap, fusion):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in container,
         "Vessel Prefab Container: no Meerkat entry on its root TRANSFORM (the Vessel Changer offers only what is here)")
    want(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in network,
         "DefaultNetworkPrefabs: no Meerkat entry on its root GAMEOBJECT (a swap to it cannot spawn)")
    for name, text in lists:
        want(text.count(CLASS_GUID) == 1, f"{name}: SO_Class_Meerkat listed {text.count(CLASS_GUID)} times, not once")
    want(f"  Class: {MEERKAT_CLASS_ID}\n" in cls and "  ownedFromStart: 1\n" in cls and "  isLocked: 0\n" in cls,
         "SO_Class_Meerkat: wrong class, locked, or not owned from the start")
    want(f"  vesselClass: {MEERKAT_CLASS_ID}\n" in kmap and kmap.count("(open design slot)") >= 4 and "Field Dipole" not in kmap,
         "ElementalAbilityMaps/Meerkat: not the Meerkat's four open design slots")
    want(OPEN_SLOT_YAML in kmap and f"'{OPEN_SLOT}'" not in kmap,
         "ElementalAbilityMaps/Meerkat: an apostrophe in a quoted description is not doubled, so the asset does not parse")
    want(len(fusion_entries(fusion, MEERKAT_CLASS_ID)) == 4, "CrystalHullFusionConfig: not 4 Meerkat entries")
    return errs


def check_all():
    errs = []
    if not os.path.exists(PREFAB) or not os.path.exists(PREFAB + ".meta"):
        return ["Meerkat.prefab (+ .meta) is missing: run Tools/Build/author_meerkat_assets.py"]
    errs += [] if guid_of(PREFAB + ".meta") == PREFAB_GUID else ["Meerkat.prefab.meta: guid is not PREFAB_GUID"]
    errs += check_prefab(read(PREFAB))
    for path, guid in ((CLASS_ASSET, CLASS_GUID), (MAP, MAP_GUID), (CONFIG, CONFIG_GUID), (FORM, FORM_GUID), (SHORTEN, SHORTEN_GUID),
                       (CAMERA, CAMERA_GUID)):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
        if guid_of(path + ".meta") != guid:
            errs.append(f"{os.path.relpath(path, ROOT)}.meta: not the guid the prefab binds")
    errs += check_config(read(CONFIG), shipped(read(STUDIO)))
    errs += check_camera(read(CAMERA))
    if read(FORM) != action_asset("MeerkatWormholeFormAction", 0) or read(SHORTEN) != action_asset("MeerkatWormholeShortenAction", 1):
        errs.append("MeerkatWormholeFormAction / ShortenAction: not the RT Form (0) and LT Shorten (1) triggers")
    errs += check_registrations(read(CONTAINER), read(NETWORK_PREFABS), [(os.path.basename(p), read(p)) for p in CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(FUSION))
    return errs


def author():
    values = shipped(read(STUDIO))
    if not os.path.isdir(ACTIONS):
        os.makedirs(ACTIONS)
        write(ACTIONS + ".meta", folder_meta(ACTIONS_DIR_GUID))
    if not os.path.exists(CONFIG):   # a one-shot: after it exists only studio_to_unity.py writes its numbers
        write(CONFIG, config_asset(values)); write(CONFIG + ".meta", native_meta(CONFIG_GUID))
    else:
        old = read(CONFIG); new = config_retrofit(old, values)
        if new != old:
            write(CONFIG, new)
    for path, guid, name, k in ((FORM, FORM_GUID, "MeerkatWormholeFormAction", 0), (SHORTEN, SHORTEN_GUID, "MeerkatWormholeShortenAction", 1)):
        if not os.path.exists(path) or read(path) != action_asset(name, k):
            write(path, action_asset(name, k)); write(path + ".meta", native_meta(guid))
    if not os.path.exists(CAMERA):   # a one-shot, like the config
        write(CAMERA, camera_asset(read(DONOR_CAMERA))); write(CAMERA + ".meta", native_meta(CAMERA_GUID))
    if not os.path.exists(PREFAB):   # the spent one-shot stands down once the prefab is committed
        write(PREFAB, build_clone(read(DONOR)))
        write(PREFAB + ".meta", prefab_meta())
    else:
        old = read(PREFAB); new = retrofit(old)
        if new != old:
            write(PREFAB, new)
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset(read(DONOR_CLASS))); write(CLASS_ASSET + ".meta", native_meta(CLASS_GUID))
    if not os.path.exists(MAP):
        write(MAP, map_asset(read(DONOR_MAP))); write(MAP + ".meta", native_meta(MAP_GUID))
    for path, fn in [(CONTAINER, register_container), (NETWORK_PREFABS, register_network), (FUSION, register_fusion)] + \
                    [(p, register_class_list) for p in CLASS_LISTS]:
        old = read(path); new = fn(old)
        if new != old:
            write(path, new)


def self_test():
    """Every check must fire on a broken copy (negative controls), and pass on the fresh clone."""
    good = build_clone(read(DONOR))
    fails = []
    def expect(errs, n_min, what):
        if not ((len(errs) == 0) if n_min == 0 else (len(errs) >= n_min)):
            fails.append(f"{what}: {errs}")
    expect(check_prefab(good), 0, "a fresh clone passes")
    expect(check_prefab(good.replace(f"  vesselType: {MEERKAT_CLASS_ID}\n", f"  vesselType: {STOAT_CLASS_ID}\n")), 1, "the Stoat's vesselType")
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>1012147755", good, count=1)), 1, "the Stoat's hash")
    expect(check_prefab(good.replace(f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}", "  config: {fileID: 0}")), 1, "config unwired")
    expect(check_prefab(good.replace(MEERKAT_BINDINGS, "  _inputEventShipActions: []\n")), 1, "RT unbound")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {RELAY_ID}}}\n", "")), 1, "the relay off the root")
    expect(check_prefab(good.replace(script_guid("MeerkatVesselTransformer"), "95baa921cf22958449a3143749d5e0e3")), 1, "the Stoat's transformer")
    expect(check_prefab(good + "  stray: {fileID: 987654321}\n"), 1, "a dangling local reference")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {STANCE_ID}}}\n", "  - component: {fileID: 0}\n")), 1, "a null component")
    expect(check_prefab(read(DONOR).replace("  m_Name: Stoat\n", "  m_Name: Meerkat\n")), 4, "the Stoat untouched")
    expect(check_prefab(good.replace(CAMERA_GUID, STOAT_CAMERA_GUID)), 1, "the Stoat's camera")
    cam = camera_asset(read(DONOR_CAMERA))
    expect(check_camera(cam), 0, "a fresh camera is centred")
    expect(check_camera(cam.replace("  followOffset: {x: 0, y: 0, z: -21}", "  followOffset: {x: 0, y: 6.5, z: -21}")), 1, "a height offset")
    expect(check_camera(cam.replace("  chaseEaseRate: 0\n", "  chaseEaseRate: 7\n")), 1, "an eased chase")
    expect(check_camera(read(DONOR_CAMERA)), 2, "the Stoat's camera")
    if retrofit(good) != good:
        fails.append("the retrofit is not idempotent")
    tuned = good.replace("\n  RollScaler: 15.6\n", "\n  RollScaler: 20\n")
    if retrofit(tuned) != tuned:
        fails.append("the turn retrofit overwrote a value tuned after it")
    if "\n  PitchScaler: 120\n" not in good or "\n  RollScaler: 15.6\n" not in good:
        fails.append("a fresh clone does not fly the studio's turn rates")
    values = shipped(read(STUDIO))
    cfg = config_asset(values)
    expect(check_config(cfg, values), 0, "a fresh config passes")
    expect(check_config(cfg.replace("  reticleAhead: 30\n", ""), values), 1, "a key missing")
    old_cfg = cfg.replace(f"  reticleSmoothSeconds: {num(values['reticleSmoothSeconds'])}\n", "").replace(f"  bridgeRings: {num(values['bridgeRings'])}\n", "")
    if config_retrofit(old_cfg, values) == old_cfg or check_config(config_retrofit(old_cfg, values), values):
        fails.append("the config retrofit does not add a key the studio added later")
    if config_retrofit(cfg, values) != cfg:
        fails.append("the config retrofit is not idempotent")
    renamed = cfg.replace("  rideMidMultiplier: ", "  throatSpeed: ")
    if "  rideMidMultiplier: 2.5\n" not in config_retrofit(renamed, values) or "  throatSpeed:" in config_retrofit(renamed, values):
        fails.append("the config retrofit does not carry a renamed key's number to its new name")
    retired = cfg.replace("  shaftMin: 60\n", "  lengthStraight: 600\n  shaftMin: 60\n")
    if check_config(retired, values) == [] or "lengthStraight" in config_retrofit(retired, values):
        fails.append("a retired key is neither flagged nor removed by the retrofit")
    pre7 = re.sub(r"  (shaftMin|shaftMax|bendRadius|throatDiameter|mouthDiameter): .*\n", "", cfg)
    if config_retrofit(pre7, values) != cfg:
        fails.append("the config retrofit does not bring a round-6 asset to the round-7 one, in the page's order")
    if "  shaftMax: 600\n" not in cfg or "  bridgeSpindleThickness: 0.25\n" not in cfg or "  aiWormhole: 1\n" not in cfg:
        fails.append(f"the config is not the studio's SHIPPED values")
    container, network, fusion = register_container(read(CONTAINER)), register_network(read(NETWORK_PREFABS)), register_fusion(read(FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in CLASS_LISTS]
    cls, kmap = class_asset(read(DONOR_CLASS)), map_asset(read(DONOR_MAP))
    expect(check_registrations(container, network, lists, cls, kmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, kmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, kmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, kmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls, read(DONOR_MAP), fusion), 1, "the Stoat's own map")
    if register_container(container) != container or register_fusion(fusion) != fusion or register_network(network) != network:
        fails.append("registration is not idempotent")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_meerkat_assets self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errs = check_all()
    for e in errs:
        print("FAIL:", e)
    print("author_meerkat_assets:", "ok" if not errs else f"{len(errs)} problem(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
