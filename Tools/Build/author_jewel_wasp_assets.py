#!/usr/bin/env python3
"""Author the Jewel Wasp (vessel class 31, the possession vessel) and register it.

    python3 Tools/Build/author_jewel_wasp_assets.py             # author what is missing, then check
    python3 Tools/Build/author_jewel_wasp_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_jewel_wasp_assets.py --self-test # prove every check fires

Garrett, 2026-11-14: "Make a new vessel ... It's signature ability should be to possess fauna and be able to fly around
and Consume prisms or attack vessels as that fauna", then the kit he signed off the same day. Design:
Assets/_Scripts/Controller/Vessel/R_VesselActions/JEWEL_WASP.md. "Bring it to Unity" means the Toybox's Vessel Changer
offers it (Garrett, 2026-10-17).

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/JewelWasp.prefab (+ .meta)   a text clone of TigerMoth.prefab (the STAND-IN airframe
    until the /vessel-model round draws the wasp) with:
        - the root GameObject and VesselStatus named JewelWasp, `vesselType: 31` (VesselClassType.JewelWasp)
        - a FRESH Netcode GlobalObjectIdHash for this guid (a disk copy keeps the donor's, and Netcode keys on it alone)
        - three bindings: RT (1) the Right control, LT (2) the Left control, A (6, Button1Action: the pad's south face
          button, GamepadInputStrategy) the Leave control - all JewelWaspActionSO assets
        - the Drop executor's component turned into JewelWaspExecutor IN PLACE (same fileID, so the action registry's
          list still names it), its GameObject renamed Possession, the hull model wired as hullModel
        - the Butterfly's Mass/Dust mode (SpreadWings: its GameObject, transform, executor and registry entry) removed;
          the dust it switched on stays OFF without it
        - the HUD controller turned into JewelWaspHUDController in place, reading the possession executor
        - the impactor container switched to JewelWaspImpactorDataContainer: the Tiger Moth's prism effects, and the
          OMNI crystal hatches a predator (JewelWaspHatchByCrystalEffect) with the birth crystal morph as retirement
      Everything else is the Tiger Moth's: the hull, the wings, the wake, the camera, the AI pilot.
    Assets/_SO_Assets/VesselActions/JewelWasp/  JewelWaspConfig.asset (every number) and the three control assets
    Assets/_SO_Assets/Effects/...               JewelWaspHatchByCrystalEffect.asset, JewelWaspImpactorDataContainer.asset
    SO_Class_JewelWasp + its entry in SO_Classlist_All / _Classes; one entry each in `Vessel Prefab Container.asset`
    (the root TRANSFORM) and `DefaultNetworkPrefabs.asset` (the root GAMEOBJECT); Resources/ElementalAbilityMaps/
    JewelWasp.asset (the four signed-off cards).
    The enum, the toybox roster (ToyVesselRoster.Default) and the touch profile row are CODE / their own generator and
    are not written here (vessel skill rule 40). NetworkManager.prefab's ProtocolVersion is raised by hand in the same
    change (a new network prefab is a new wire).

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while JewelWasp.prefab is absent,
and `--check` validates the SHIPPED prefab's invariants rather than diffing it against a donor that will move on.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)
from author_tigermoth_assets import (add_list_entry, asset_meta, block, folder_meta, guid_of, list_key_of,  # noqa: E402
                                     prefab_meta, read, write, write_quiet)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "TigerMoth.prefab")
PREFAB = os.path.join(VESSELS, "JewelWasp.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
DONOR_CLASS = os.path.join(CLASSES, "SO_Class_TigerMoth.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_JewelWasp.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAP = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps", "JewelWasp.asset")
DONOR_MAP = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps", "TigerMoth.asset")
ACTION_DIR = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "JewelWasp")
CONFIG = os.path.join(ACTION_DIR, "JewelWaspConfig.asset")
CONTROLS = {   # name -> (asset, guid, JewelWaspControl value)
    "Right": (os.path.join(ACTION_DIR, "JewelWaspRightAction.asset"), "cfbf439da9974f7e97e3abc13cff0239", 0),
    "Left": (os.path.join(ACTION_DIR, "JewelWaspLeftAction.asset"), "d3578950de194b9cb4522849856e0010", 1),
    "Leave": (os.path.join(ACTION_DIR, "JewelWaspLeaveAction.asset"), "f72fedc038e74a7ea1ae63f64af5b950", 2),
}
EFFECTS = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects")
HATCH = os.path.join(EFFECTS, "Vessel Crystal Effects", "JewelWaspHatchByCrystalEffect.asset")
IMPACTOR = os.path.join(EFFECTS, "Effect Containers", "VesselContainers", "JewelWaspImpactorDataContainer.asset")
DONOR_IMPACTOR = os.path.join(EFFECTS, "Effect Containers", "VesselContainers", "ButterflyImpactorDataContainer.asset")
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")
R_ACTIONS = os.path.join(SCRIPTS, "Controller", "Vessel", "R_VesselActions")
EXECUTOR_META = os.path.join(R_ACTIONS, "Executors", "JewelWaspExecutor.cs.meta")
ACTION_SO_META = os.path.join(R_ACTIONS, "Data Containers", "JewelWaspActionSO.cs.meta")
CONFIG_SO_META = os.path.join(R_ACTIONS, "Data Containers", "JewelWaspConfigSO.cs.meta")
HATCH_SO_META = os.path.join(SCRIPTS, "Controller", "ImpactEffects", "EffectsSO", "Vessel Crystal Effects",
                             "JewelWaspHatchByCrystalEffectSO.cs.meta")
HUD_META = os.path.join(SCRIPTS, "UI", "Controller", "JewelWaspHUDController.cs.meta")

# 24-27 are claimed on in-flight branches (Flea, Warden, Bulwark, Tempest), 28 is the Meerkat, 29 the Tiger Moth, 30 the
# Swordfish; 31 was free on bleeding-edge and every open pull request on 2026-11-14.
CLASS_ID = 31
DONOR_CLASS_ID = 29
PREFAB_GUID = "6cb50bd6489047e19ff32101c7d5146a"
ACTION_DIR_GUID = "9510d72b17744c8c84b0eac953f0b706"
CONFIG_GUID = "f8f6b3fa4c354d2fa90c335d31ce324e"
HATCH_GUID = "6809c2cb0fa0402096482105f912cb79"
IMPACTOR_GUID = "2ff1c40845ed408c90ba87f10f4674ce"
CLASS_GUID = "f4dcdd79b01f4c8d8402b5f47983d529"
MAP_GUID = "efead37c7eaa42379c7c8a148747f2f0"

COMBAT_HIT_EVENT_GUID = "45fb43a0cddb5cc3cc4dc448df994152"   # Event_CombatHitStats
HAPTICS_EFFECT_GUID = "8de915fb00503344ca89a92a0a127b00"     # the omni crystal's haptics (the Angler's and the Moth's)
BIRTH_RETIREMENT_GUID = "5aa5eaa4f05943558b1dedc40f4c607b"   # FaunaBirthCrystalMorphByCrystal: the crystal folds into the newborn
BUTTERFLY_EXPLOSION_GUID = "aa63fd2ae68e61603096278e815d73e7"  # the Moth's omni explosion: not the wasp's
DONOR_IMPACTOR_GUID = "bd343f41747bf364dba3696eb3e33fbf"
DONOR_EXECUTOR_GUID = "7c42e6152d424e1896772b16b6dc533e"     # TigerMothDropExecutor
DONOR_HUD_GUID = "f9a83dffac054b05b9957faea68ac803"          # ButterflyHUDController
DONOR_DROP_ACTION_GUID = "a541c39b6f7a46c5be04c2b39275a4c0"  # TigerMothDropAction
DONOR_SPREAD_ACTION_GUID = "bae72b89360ec98408e562747f9a9549"  # SpreadWingsAction
# The predators an omni crystal can hatch: the Shark and the Worm Colony, all four elements of each (FaunaDiet.Predator).
OMNI_PREDATORS = [
    "58835b82ea284255855af2649ef185a5", "a690f25bf21e486ba0e500563b90f1ea",   # Shark Fauna Charge, Mass
    "eaf56c14345740849f35fc84467059e9", "78ce842bb8554d748af1e96abf430137",   # Shark Fauna Space, Time
    "c1a7e2b45f0d4c1e8a6b9d3f2e7c5a10", "a3d59c8e71b24f6a9c0e4d8b5f172c33",   # Worm Colony Charge, Mass
    "e7f1b3a2c9d84e5fb6a08c7d4e392b55", "b9c4d7e2a1f34b8cd5e6f0a39b8d1c77",   # Worm Colony Space, Time
]

ROOT_GO = "116782616907870029"
ROOT_TRANSFORM_ID = "9012526572693406291"
NETWORK_OBJECT_ID = "2912090631757861063"
EXECUTOR_ID = "2588221200834226537"      # TigerMothDropExecutor on the child "Drop" -> JewelWaspExecutor
EXECUTOR_GO = "5551892933991280828"
HUD_ID = "8298610961815995264"           # ButterflyHUDController -> JewelWaspHUDController
HULL_MODEL_ID = "6953798627971058969"    # the hull model's root transform (the stand-in airframe)
SPREAD_GO = "726101974328758376"
SPREAD_TRANSFORM = "8118271280978713450"
SPREAD_EXECUTOR = "2591329563551618180"
INPUT_RT, INPUT_LT, INPUT_A = 1, 2, 6   # RightStickAction, LeftStickAction, Button1Action (the pad's A)

# The hull model: the wasp's own (Tools/Build/vessel_model/specs/jewelwasp.py), on the Butterfly's rig and names, so the
# prefab binds it by swapping the model's guid alone (the borrowed-rig path); the stand-in was the Tiger Moth's Butterfly.
MODEL_FBX = os.path.join(ROOT, "Assets", "_Models", "Vessel Models", "JewelWasp.fbx")
BUTTERFLY_MODEL_GUID = "1c6e4c75c81047ddb894f8966caea0af"
# The possession's network relay (JewelWaspNetworkRelay, on the root beside the NetworkObject): the server decides the
# possession in a networked match and every peer mirrors it (JEWEL_WASP.md section 6.1).
RELAY_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "JewelWaspNetworkRelay.cs.meta")
RELAY_ID = "7741000000000003101"
# The Tiger Moth's Dust-mode capsule (ButterflyDustSkimmer), dead weight on the wasp: switched off and driven by nothing
# once SpreadWings went, yet still the near-field skimmer and the trail's clearance skimmer. Retired 2026-11-15: the trail
# measures its clearance off the always-on crystal catcher (its far-field skimmer), and the near-field slot is empty
# (the Termite's and the Falcon's shape: VesselController skips an empty slot).
DUST_SKIMMER_INSTANCE = "7526780584530545883"
DUST_SKIMMER_DOCS = ("7526780584530545883", "281340694002912670", "878149464122343524", "7700000000000002001")
DUST_SKIMMER = "281340694002912670"          # its Skimmer (stripped)
DUST_SKIMMER_TRANSFORM = "878149464122343524"
DUST_SKIMMER_PREFAB_GUID = "744c7a43f1156e7bde213ac0f51abc71"
CRYSTAL_SKIMMER = "92358954707176188"        # ButterflyCrystalSkimmer's Skimmer (stripped), the far-field slot
# The HUD: the wasp's own JewelWaspHUDVariant (Tools/Build/author_jewel_wasp_hud.py), never the Tiger Moth's.
BUTTERFLY_HUD_GUID = "dd198b52721e3c346970524ad4972959"
OWN_HUD_META = os.path.join(ROOT, "Assets", "_Prefabs", "UI Elements", "VesselHUD", "JewelWaspHUDVariant.prefab.meta")
# The wingbeat, a wasp's: fast and shallow, fore and hind wings hooked together (a wasp's hamuli), no soaring V.
# ButterflyAnimation's own fields on this hull (the Tiger Moth's values were a butterfly's slow, deep, lagged beat).
WING_BEAT = (("beatAmplitude", "52", "30"), ("beatAmplitudeAtCruise", "16", "12"), ("beatHz", "1.15", "5"),
             ("beatHzAtCruise", "1.7", "8"), ("glideLift", "26", "6"), ("hindLagBeats", "0.18", "0.03"))


# ----------------------------------------------------------------------------- the prefab
# The wasp's own chase camera (Garrett, 2026-11-19: "the camera on the jewel wasp could be much closer. when it is not in
# a lifeform"): forked from the Tiger Moth's, which stood 204 u back of a 6.5 u hull. A ride and a plant keep their own
# views (JewelWaspConfig.hostCameraFloor, the plant orbit).
CAMERA = os.path.join(ROOT, "Assets", "_SO_Assets", "Camera", "JewelWaspCameraSettingsSO.asset")
CAMERA_GUID = "085bebe09734406e8fc75553a8daa8a9"
DONOR_CAMERA = os.path.join(ROOT, "Assets", "_SO_Assets", "Camera", "ButterflyCameraSettingsSO.asset")
DONOR_CAMERA_GUID = "2bd6a6d58dbefa346b5b51b78a9c82a8"
CAMERA_OFFSET = "{x: 0, y: 8, z: -45}"


def camera_asset(donor: str) -> str:
    t = re.sub(r"(?m)^  m_Name: .*$", "  m_Name: JewelWaspCameraSettingsSO", donor, count=1)
    return re.sub(r"(?m)^  followOffset: .*$", f"  followOffset: {CAMERA_OFFSET}", t, count=1)


def clone_prefab(donor: str) -> str:
    t = donor
    t = replace_once(t, "  m_Name: TigerMoth\n  m_TagString", "  m_Name: JewelWasp\n  m_TagString", "root name")
    t = replace_once(t, f"  _name: TigerMoth\n  vesselType: {DONOR_CLASS_ID}\n", f"  _name: JewelWasp\n  vesselType: {CLASS_ID}\n",
                     "VesselStatus")
    t = re.sub(r"(  GlobalObjectIdHash: )\d+\n", rf"\g<1>{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", t, count=1)
    old_bindings = (f"  _inputEventShipActions:\n  - InputEvent: {INPUT_RT}\n    ShipActions:\n"
                    f"    - {{fileID: 11400000, guid: {DONOR_SPREAD_ACTION_GUID}, type: 2}}\n"
                    f"  - InputEvent: {INPUT_LT}\n    ShipActions:\n"
                    f"    - {{fileID: 11400000, guid: {DONOR_DROP_ACTION_GUID}, type: 2}}\n")
    t = replace_once(t, old_bindings, bindings(), "the bindings")

    # the executor, in place
    old_exec = block(t, EXECUTOR_ID)
    if f"guid: {DONOR_EXECUTOR_GUID}" not in old_exec:
        raise RuntimeError("donor drifted: the Drop executor is not where it was")
    head = old_exec.split("  m_Script:")[0]
    t = t.replace(old_exec, head +
                  f"  m_Script: {{fileID: 11500000, guid: {guid_of(EXECUTOR_META)}, type: 3}}\n"
                  "  m_Name: \n"
                  "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Gameplay.JewelWaspExecutor\n"
                  f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}\n"
                  f"  hullModel: {{fileID: {HULL_MODEL_ID}}}", 1)
    exec_go = block(t, EXECUTOR_GO)
    t = t.replace(exec_go, replace_once(exec_go, "  m_Name: Drop\n", "  m_Name: Possession\n", "the Drop GameObject"), 1)

    # the HUD controller, in place
    old_hud = block(t, HUD_ID)
    if f"guid: {DONOR_HUD_GUID}" not in old_hud:
        raise RuntimeError("donor drifted: the Butterfly HUD controller is not where it was")
    base_view = re.search(r"  baseView: \{fileID: (-?\d+)\}", old_hud).group(1)
    head = old_hud.split("  m_Script:")[0]
    t = t.replace(old_hud, head +
                  f"  m_Script: {{fileID: 11500000, guid: {guid_of(HUD_META)}, type: 3}}\n"
                  "  m_Name: \n"
                  "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.JewelWaspHUDController\n"
                  f"  baseView: {{fileID: {base_view}}}\n"
                  "  _iconSetSwitcher: {fileID: 0}\n"
                  f"  wasp: {{fileID: {EXECUTOR_ID}}}", 1)

    # SpreadWings (the Butterfly's Mass / Dust mode) goes
    for fid in (SPREAD_GO, SPREAD_TRANSFORM, SPREAD_EXECUTOR):
        t = t.replace(block(t, fid) + "\n", "", 1)
    t = replace_once(t, f"  - {{fileID: {SPREAD_TRANSFORM}}}\n", "", "SpreadWings in VesselActions' children")
    t = replace_once(t, f"  - {{fileID: {SPREAD_EXECUTOR}}}\n", "", "SpreadWings in the executor registry")

    # the impactor container
    t = replace_once(t, f"guid: {DONOR_IMPACTOR_GUID},\n    type: 2}}", f"guid: {IMPACTOR_GUID}, type: 2}}",
                     "the impactor container")
    return t


def bindings() -> str:
    out = "  _inputEventShipActions:\n"
    for inp, name in ((INPUT_RT, "Right"), (INPUT_LT, "Left"), (INPUT_A, "Leave")):
        out += f"  - InputEvent: {inp}\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROLS[name][1]}, type: 2}}\n"
    return out


# ----------------------------------------------------------------------------- the small assets
def so_head(script_guid: str, name: str, cls: str) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier: Assembly-CSharp::{cls}\n")


def efloat(key: str, at_rest, at_full, element: int, floor) -> str:
    # ElementalFloat.Multiplier(atRest, atFull, element, floor)
    return (f"  {key}:\n    Enabled: 1\n    Value: {at_rest}\n    Min: {at_rest}\n    Max: {at_full}\n"
            f"    element: {element}\n    UseFloor: 1\n    Floor: {floor}\n")


def empty_event(key: str) -> str:
    return f"  {key}:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"


FMOD_SLOTS = ("stingEvent", "nestEvent", "leaveEvent", "attackEvent", "suctionEvent", "burstEvent", "selectEvent")
FLORA_VIEW = ("  floraMinDistance: 18\n  floraFitRadii: 3.2\n  floraZoomRange: {x: 0.3, y: 4}\n  floraZoomRate: 1.4\n"
              "  floraOrbitDegrees: 100\n  floraRollDegrees: 110\n  floraFollowEase: 3\n"
              "  selectPulseSeconds: 0.35\n  selectPulseGlow: 4\n  floraAimAssistDegrees: 35\n"
              "  nestedTailWidth: 0.3\n  nestedGlow: 0.35\n  abilityFlashGlow: 1\n  abilityFlashSeconds: 0.6\n")


def config_asset() -> str:
    t = so_head(guid_of(CONFIG_SO_META), "JewelWaspConfig", "CosmicShore.Gameplay.JewelWaspConfigSO")
    t += ("  aimConeDegrees: 14\n  stingRangeOutsideCell: 3000\n  stingSpeed: 1400\n  stingSeconds: 0.35\n"
          "  stingCooldown: 0.6\n  stingMaxSeconds: 4\n"
          "  hullScale: 0.45\n  nestedScale: 0.22\n  stingCurlDegrees: 72\n  nestedCurlDegrees: 34\n  nestOffset: 1.3\n  fallbackHeartRadius: 2\n"
          "  hostTurnDegrees: 120\n  hostRollDegrees: 110\n")
    t += efloat("hostSpeedScale", 1.5, 3, 4, 1)
    t += "  hostSlowShare: 0.45\n  burstMultiplier: 2.2\n  burstSeconds: 1.2\n  burstCooldown: 3\n"
    t += efloat("suctionReachScale", 1, 3, 3, 0.5)
    t += "  suctionBites: 8\n  suctionInterval: 0.45\n"
    t += efloat("attackCooldown", 3, 0.8, 1, 0.4)
    t += "  attackSeconds: 0.6\n  attackReach: 22\n  attackAimReaches: 3\n  attackHitClass: 5\n"
    t += "  leaveSpeed: 90\n  leaveGraceSeconds: 0.6\n  burstRadius: 60\n  burstHitClass: 3\n"
    t += efloat("floraGrowth", 3.5, 7, 2, 2.2)
    t += "  overgrowthRadius: 200\n  floraCameraDistance: 650\n"
    t += FLORA_VIEW
    t += "  hostCameraBodies: 6\n  hostCameraBase: 30\n  hostCameraFloor: 204\n  cameraEase: 4\n"
    t += "  omniPredators:\n" + "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in OMNI_PREDATORS)
    t += "  aiHostSeconds: 14\n  aiLookSeconds: 1.5\n"
    t += f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_EVENT_GUID}, type: 2}}\n"
    for k in FMOD_SLOTS:
        t += empty_event(k)   # LOCKED FMOD convention: every slot ships EMPTY, never a borrowed event
    return t


def control_asset(name: str) -> str:
    return so_head(guid_of(ACTION_SO_META), f"JewelWasp{name}Action", "CosmicShore.Gameplay.JewelWaspActionSO") + \
        f"  control: {CONTROLS[name][2]}\n"


def hatch_asset() -> str:
    return so_head(guid_of(HATCH_SO_META), "JewelWaspHatchByCrystalEffect",
                   "CosmicShore.Gameplay.JewelWaspHatchByCrystalEffectSO")


def impactor_asset(donor: str) -> str:
    t = replace_once(donor, "  m_Name: ButterflyImpactorDataContainer\n", "  m_Name: JewelWaspImpactorDataContainer\n",
                     "container name")
    t = replace_once(t, f"  vesselCrystalEffects:\n  - {{fileID: 11400000, guid: {BUTTERFLY_EXPLOSION_GUID}, type: 2}}\n"
                        f"  - {{fileID: 11400000, guid: {HAPTICS_EFFECT_GUID}, type: 2}}\n",
                     f"  vesselCrystalEffects:\n  - {{fileID: 11400000, guid: {HAPTICS_EFFECT_GUID}, type: 2}}\n"
                     f"  - {{fileID: 11400000, guid: {HATCH_GUID}, type: 2}}\n", "the omni crystal's effects")
    return re.sub(r"  omniCrystalRetirement: \{[^}]*\}\n",
                  f"  omniCrystalRetirement: {{fileID: 11400000, guid: {BIRTH_RETIREMENT_GUID}, type: 2}}\n", t, count=1)


MAP_ENTRIES = """  - Element: 1
    AbilityLabel: Leave / Attack
    AbilityDescription: 'A leaves a host: the wasp pops out of the creature or plant it rides and flies on along the host''s heading, untouchable for a moment so the host cannot joust it (on screen on a phone: the Charge card is the A button). In a creature, RT AT A RIVAL VESSEL ahead plays the creature''s attack (a Brittlestar''s legs whip out; a body with no limbs lunges) and lands a Strike, the petal-stealing scored hit. Charge''s dial is the ATTACK''S RECHARGE: 3 s at rest, 0.8 s at Charge 10.'
    Input: 6
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Burst
    UpgradeDescription: 'Leaving a creature bursts it: every rival vessel within 60 u takes a blast-class hit.'
  - Element: 2
    AbilityLabel: Root
    AbilityDescription: 'LT AT A PLANT: hold to aim (the plant ahead lights in your hue), let go to sting it from anywhere in the cell. The wasp roots in the plant, the camera stands far back and the sticks turn it round the plant, and the plant GROWS while you are in it. Either trigger from a plant aims and stings the next host, so a plant is the best seat for picking one. Mass''s dial is HOW FAST THE PLANT GROWS: x2 at rest, x6 at Mass 10.'
    Input: 2
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Overgrowth
    UpgradeDescription: 'Every plant within 200 u of the one you are rooted in grows as fast.'
  - Element: 3
    AbilityLabel: Sting / Suction
    AbilityDescription: 'RT AT A CREATURE: hold to aim (the creature ahead lights in your hue), let go to sting it from anywhere in the cell - any creature, any diet; a worm segment takes the whole worm by its head, a swarm member the whole murmuration. The wasp nests beside its heart crystal and the sticks fly it. In a creature, RT AT PRISMS is suction: the nearest rival prisms ahead, shields and super-shields included, are pulled into its mouth and fill its stomach. Space''s dial is THE SUCTION''S REACH: x1 the creature''s own at rest, x3 at Space 10.'
    Input: 1
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Maw
    UpgradeDescription: 'Suction pulls from every side, not only ahead.'
  - Element: 4
    AbilityLabel: Swim / Burst
    AbilityDescription: 'In a creature the sticks fly it at a multiple of its own top speed (sticks together slowest, apart full), and LT is a BURST of speed: 2.2x for 1.2 s, then 3 s to recharge (the card''s icon swaps to the burst while you ride). Time''s dial is HOW FAST A HOST SWIMS: x1.5 its own top speed at rest, x3 at Time 10.'
    Input: 2
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Restless
    UpgradeDescription: 'No wait between stings, and none between bursts.'
"""


def map_asset(donor_map: str) -> str:
    t = replace_once(donor_map, "  m_Name: TigerMoth\n", "  m_Name: JewelWasp\n", "map name")
    t = replace_once(t, f"  vesselClass: {DONOR_CLASS_ID}\n", f"  vesselClass: {CLASS_ID}\n", "map class")
    i = t.index("  entries:\n") + len("  entries:\n")
    return t[:i] + MAP_ENTRIES


def class_asset(donor_class: str) -> str:
    t = replace_once(donor_class, "  m_Name: SO_Class_TigerMoth\n", "  m_Name: SO_Class_JewelWasp\n", "class name")
    t = replace_once(t, f"  Class: {DONOR_CLASS_ID}\n", f"  Class: {CLASS_ID}\n", "class id")
    t = re.sub(r"  Name: Tiger Moth\n  Description: .*?(?=\n  PrimaryElement:)",
               "  Name: Jewel Wasp\n  Description: \"Small, quick and patient. Sting any creature in the cell and it is "
               "yours to fly: drink the rival's mass with its mouth, strike their vessels with its body. Sting a plant "
               "and it grows. Press A to leave.\"", t, count=1, flags=re.S)
    return t


def network_entry() -> str:
    return (f"  - Override: 0\n    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n"
            "    SourcePrefabToOverride: {fileID: 0}\n    SourceHashToOverride: 0\n    OverridingTargetPrefab: {fileID: 0}\n")


def retire_dust_skimmer(t: str) -> str:
    """Take the Moth's dust capsule off the hull (idempotent): its four documents, its place under the root, and the two
    slots that named it."""
    if f"guid: {DUST_SKIMMER_PREFAB_GUID}" not in t:
        return t
    for fid in DUST_SKIMMER_DOCS:
        t = re.sub(rf"--- !u!\d+ &{fid}( stripped)?\n.*?(?=--- !u!|\Z)", "", t, count=1, flags=re.S)
    t = t.replace(f"  - {{fileID: {DUST_SKIMMER_TRANSFORM}}}\n", "", 1)
    t = t.replace(f"  skimmer: {{fileID: {DUST_SKIMMER}}}\n", f"  skimmer: {{fileID: {CRYSTAL_SKIMMER}}}\n", 1)
    t = t.replace(f"  _nearFieldSkimmer: {{fileID: {DUST_SKIMMER}}}\n", "  _nearFieldSkimmer: {fileID: 0}\n", 1)
    return t


def wear_own_hull(t: str) -> str:
    """The prefab after the clone: the wasp's own model in place of the Butterfly (a guid swap: the names, bones and
    sub-asset ids are the Butterfly's) and a wasp's wingbeat. Idempotent: it runs on the committed prefab every time."""
    model_guid = guid_of(MODEL_FBX + ".meta")
    t = t.replace(f"guid: {BUTTERFLY_MODEL_GUID}", f"guid: {model_guid}")
    t = re.sub(r"(propertyPath: m_Name\n      value: )Butterfly\n", r"\g<1>JewelWasp\n", t)
    for key, old, new in WING_BEAT:
        t = re.sub(rf"(?m)^  {key}: {re.escape(old)}$", f"  {key}: {new}", t)
    if f"--- !u!114 &{RELAY_ID}\n" not in t and os.path.exists(RELAY_META):
        root = block(t, ROOT_GO)
        last = re.findall(r"  - component: \{fileID: -?\d+\}\n", root)[-1]
        t = t.replace(root, root.replace(last, last + f"  - component: {{fileID: {RELAY_ID}}}\n", 1), 1)
        t = t.rstrip("\n") + "\n" + (
            f"--- !u!114 &{RELAY_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid_of(RELAY_META)}, type: 3}}\n  m_Name: \n"
            f"  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Gameplay.JewelWaspNetworkRelay\n"
            f"  ShowTopMostFoldoutHeaderGroup: 1\n")
    if os.path.exists(CAMERA + ".meta"):
        t = t.replace(f"  settings: {{fileID: 11400000, guid: {DONOR_CAMERA_GUID}, type: 2}}\n",
                      f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}\n", 1)
    return retire_dust_skimmer(t)


# ----------------------------------------------------------------------------- author + check
def author() -> None:
    if not os.path.exists(PREFAB) and os.path.exists(DONOR):
        write(PREFAB, clone_prefab(read(DONOR)))
        write(PREFAB + ".meta", prefab_meta(PREFAB_GUID))
    if not os.path.exists(CAMERA) and os.path.exists(DONOR_CAMERA):
        write(CAMERA, camera_asset(read(DONOR_CAMERA)))
        write(CAMERA + ".meta", asset_meta(CAMERA_GUID))
    if os.path.exists(PREFAB) and os.path.exists(MODEL_FBX + ".meta"):
        t = read(PREFAB)
        n = wear_own_hull(t)
        if n != t: write(PREFAB, n)
    if not os.path.exists(ACTION_DIR + ".meta"):
        write(ACTION_DIR + ".meta", folder_meta(ACTION_DIR_GUID))
    if not os.path.exists(CONFIG):
        write(CONFIG, config_asset())
        write(CONFIG + ".meta", asset_meta(CONFIG_GUID))
    for name, (path, guid, _) in CONTROLS.items():
        if not os.path.exists(path):
            write(path, control_asset(name))
            write(path + ".meta", asset_meta(guid))
    if not os.path.exists(HATCH):
        write(HATCH, hatch_asset())
        write(HATCH + ".meta", asset_meta(HATCH_GUID))
    if not os.path.exists(IMPACTOR):
        write(IMPACTOR, impactor_asset(read(DONOR_IMPACTOR)))
        write(IMPACTOR + ".meta", asset_meta(IMPACTOR_GUID))
    if not os.path.exists(MAP) and os.path.exists(DONOR_MAP):
        write(MAP, map_asset(read(DONOR_MAP)))
        write(MAP + ".meta", asset_meta(MAP_GUID))
    if not os.path.exists(CLASS_ASSET) and os.path.exists(DONOR_CLASS):
        write(CLASS_ASSET, class_asset(read(DONOR_CLASS)))
        write(CLASS_ASSET + ".meta", asset_meta(CLASS_GUID))
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


def errors() -> list:
    out = []
    def need(cond, what):
        if not cond: out.append(what)
    for p in [PREFAB, CONFIG, HATCH, IMPACTOR, MAP, CLASS_ASSET, CAMERA] + [c[0] for c in CONTROLS.values()]:
        need(os.path.exists(p) and os.path.exists(p + ".meta"), f"missing {os.path.relpath(p, ROOT)} (+ .meta)")
    if out:
        return out
    pf = read(PREFAB)
    need(guid_of(PREFAB + ".meta") == PREFAB_GUID, "JewelWasp.prefab.meta guid moved")
    need(guid_of(CAMERA + ".meta") == CAMERA_GUID, "JewelWaspCameraSettingsSO.asset.meta guid moved")
    need(f"  followOffset: {CAMERA_OFFSET}\n" in read(CAMERA), f"the wasp's own camera is not at {CAMERA_OFFSET}")
    need(f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}\n" in pf
         and f"guid: {DONOR_CAMERA_GUID}" not in pf, "the wasp flies the Tiger Moth's far camera, not its own")
    need(f"  _name: JewelWasp\n  vesselType: {CLASS_ID}\n" in pf, "VesselStatus is not the Jewel Wasp (31)")
    need(f"  GlobalObjectIdHash: {global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n" in pf,
         "GlobalObjectIdHash is not this prefab's (a donor hash keys two prefabs on one Netcode entry)")
    need(bindings() in pf, "RT / LT / A are not bound to the Right / Left / Leave controls")
    for g, what in ((DONOR_DROP_ACTION_GUID, "the Tiger Moth's Drop action"), (DONOR_SPREAD_ACTION_GUID, "SpreadWings' action"),
                    (DONOR_EXECUTOR_GUID, "a TigerMothDropExecutor"), (DONOR_HUD_GUID, "the Butterfly HUD controller"),
                    (DONOR_IMPACTOR_GUID, "the Butterfly's impactor container")):
        need(f"guid: {g}" not in pf, f"{what} is still on the hull")
    need("SpreadWingsActionExecutor" not in pf, "SpreadWings is still on the hull")
    exec_block = block(pf, EXECUTOR_ID)
    need(f"guid: {guid_of(EXECUTOR_META)}" in exec_block and f"config: {{fileID: 11400000, guid: {CONFIG_GUID}" in exec_block,
         "the possession executor is not wired to its config")
    need(f"  hullModel: {{fileID: {HULL_MODEL_ID}}}" in exec_block, "the possession executor has no hull model")
    need(os.path.exists(MODEL_FBX) and os.path.exists(MODEL_FBX + ".meta"),
         "JewelWasp.fbx is missing (build it: vessel_model/build.py JewelWasp)")
    if os.path.exists(MODEL_FBX + ".meta"):
        need(f"m_SourcePrefab: {{fileID: 100100000, guid: {guid_of(MODEL_FBX + '.meta')}, type: 3}}" in pf
             and BUTTERFLY_MODEL_GUID not in pf, "the hull still wears the Butterfly's model, not JewelWasp.fbx")
    relay_on_root = (f"  - component: {{fileID: {RELAY_ID}}}\n" in block(pf, ROOT_GO)
                     and f"--- !u!114 &{RELAY_ID}\n" in pf
                     and f"m_GameObject: {{fileID: {ROOT_GO}}}" in block(pf, RELAY_ID)
                     and f"guid: {guid_of(RELAY_META)}" in block(pf, RELAY_ID)) if os.path.exists(RELAY_META) else False
    need(relay_on_root, "JewelWaspNetworkRelay is not on the root beside the NetworkObject (the server cannot hand "
                        "a client its possession)")
    need(f"guid: {DUST_SKIMMER_PREFAB_GUID}" not in pf and f"  _nearFieldSkimmer: {{fileID: 0}}\n" in pf
         and f"  skimmer: {{fileID: {CRYSTAL_SKIMMER}}}\n" in pf,
         "the Tiger Moth's dust capsule is still on the hull (or the trail's clearance skimmer is not the crystal catcher)")
    need(f"  _farFieldSkimmer: {{fileID: {CRYSTAL_SKIMMER}}}\n" in pf, "the crystal catcher is not the far-field skimmer")
    for key, _, new in WING_BEAT:
        need(f"\n  {key}: {new}\n" in pf, f"the wingbeat's {key} is not the wasp's ({new})")
    need(os.path.exists(OWN_HUD_META) and f"guid: {guid_of(OWN_HUD_META)}" in pf and BUTTERFLY_HUD_GUID not in pf,
         "the hull still nests the Tiger Moth's HUD (run author_jewel_wasp_hud.py)")
    hud = block(pf, HUD_ID)
    need(f"guid: {guid_of(HUD_META)}" in hud and f"  wasp: {{fileID: {EXECUTOR_ID}}}" in hud,
         "the HUD controller does not read the possession executor")
    registry = re.search(r"  _executors:\n((?:  - \{fileID: -?\d+\}\n)+)", pf)
    need(registry is not None and f"{{fileID: {EXECUTOR_ID}}}" in registry.group(1),
         "the executor registry does not list the possession executor")
    need(f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {IMPACTOR_GUID}" in pf,
         "the hull's impactor is not JewelWaspImpactorDataContainer")
    local = set(re.findall(r"^--- !u!\d+ &(-?\d+)", pf, re.M))
    dangling = {i for i in re.findall(r"\{fileID: (-?\d+)\}", pf) if i != "0" and i not in local}
    need(not dangling, f"dangling local references: {sorted(dangling)[:5]}")

    cfg = read(CONFIG)
    need(f"guid: {guid_of(CONFIG_SO_META)}" in cfg, "JewelWaspConfig is not a JewelWaspConfigSO")
    for g in OMNI_PREDATORS:
        need(f"guid: {g}" in cfg, "an omni predator is missing from the config")
    need(f"onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_EVENT_GUID}" in cfg,
         "the attack scores on no channel (PvP is petals only: every hit is a scored hit)")
    # Every FMOD slot ships empty unless the placeholder ledger lists it (Garrett 2026-11-16: a recycled placeholder
    # is allowed only when Docs/AudioSystem/sound_placeholders.json names it and the event that replaces it).
    import sound_placeholders
    listed = {it["field"]: it["path"] for it in sound_placeholders._lock(ROOT).get(os.path.relpath(CONFIG, ROOT).replace(os.sep, "/"), [])}
    filled = re.findall(r"^  (\w+):\n    Guid:\n(?:      Data\d: -?\d+\n){4}    Path: (\S.*)$", cfg, re.M)
    need(len(re.findall(r"^    Path:", cfg, re.M)) == len(FMOD_SLOTS) and all(listed.get(f) == ev for f, ev in filled),
         "an FMOD slot holds an event the placeholder ledger does not list (LOCKED: slots ship empty or listed)")
    # one parameter per element (the fleet's rule), each its own element
    for key, element in (("hostSpeedScale", 4), ("suctionReachScale", 3), ("attackCooldown", 1), ("floraGrowth", 2)):
        m = re.search(rf"^  {key}:\n(?:    .*\n)*?    element: (\d)\n", cfg, re.M)
        need(m is not None and int(m.group(1)) == element, f"{key} is not scaled by element {element}")
    for name, (path, guid, value) in CONTROLS.items():
        a = read(path)
        need(guid_of(path + ".meta") == guid and f"guid: {guid_of(ACTION_SO_META)}" in a and f"  control: {value}\n" in a,
             f"JewelWasp{name}Action is not the {name} control")
    need(f"guid: {guid_of(HATCH_SO_META)}" in read(HATCH), "JewelWaspHatchByCrystalEffect is not the hatch effect")
    imp = read(IMPACTOR)
    need(f"guid: {HATCH_GUID}" in imp and BUTTERFLY_EXPLOSION_GUID not in imp,
         "the omni crystal does not hatch a predator (or still explodes as the Moth's)")
    need(f"omniCrystalRetirement: {{fileID: 11400000, guid: {BIRTH_RETIREMENT_GUID}" in imp,
         "the omni crystal does not fold into the newborn")
    mp = read(MAP)
    need(f"  vesselClass: {CLASS_ID}\n" in mp, "the map is not class 31")
    for label in ("Burst", "Overgrowth", "Maw", "Restless"):
        need(f"UpgradeLabel: {label}\n" in mp, f"the map has no {label} level-5 card")
    need(re.search(r"  - Element: 1\n(?:    .*\n)*?    Input: 6\n", mp) is not None, "Charge is not on A")
    need(f"  Class: {CLASS_ID}\n" in read(CLASS_ASSET), "SO_Class_JewelWasp is not class 31")
    card = os.path.join(ROOT, "Assets", "_Graphics", "CardImages", "JewelWasp.png.meta")
    need(os.path.exists(card) and f"  IconActive: {{fileID: 21300000, guid: {guid_of(card)}, type: 3}}\n" in read(CLASS_ASSET),
         "SO_Class_JewelWasp does not show the wasp's own card (render it: vessel_model/render_cards.py JewelWasp)")
    for path in CLASS_LISTS:
        need(f"guid: {CLASS_GUID}" in read(path), f"{os.path.basename(path)} does not list SO_Class_JewelWasp")
    need(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in read(CONTAINER),
         "the Vessel Prefab Container does not list the Jewel Wasp's root transform")
    need(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in read(NETWORK_PREFABS),
         "DefaultNetworkPrefabs does not list the Jewel Wasp's root GameObject")
    roster = read(os.path.join(SCRIPTS, "Controller", "Toys", "ToyVesselRoster.cs"))
    need("VesselClassType.JewelWasp" in roster, "the Vessel Changer does not offer the Jewel Wasp (ToyVesselRoster.Default)")
    return out


def self_test() -> int:
    """Each planted defect must be caught by the check that guards it."""
    good = read(PREFAB)
    cases = {
        "donor hash": good.replace(f"GlobalObjectIdHash: {global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}",
                                   "GlobalObjectIdHash: 2928199215"),
        "class id": good.replace(f"vesselType: {CLASS_ID}\n", "vesselType: 29\n"),
        "A unbound": good.replace(f"  - InputEvent: {INPUT_A}\n", "  - InputEvent: 7\n", 1),
        "dangling": good.replace(f"  wasp: {{fileID: {EXECUTOR_ID}}}", "  wasp: {fileID: 123}"),
        "no hull model": good.replace(f"  hullModel: {{fileID: {HULL_MODEL_ID}}}", "  hullModel: {fileID: 0}"),
        "the Butterfly's model": good.replace(f"guid: {guid_of(MODEL_FBX + '.meta')}", f"guid: {BUTTERFLY_MODEL_GUID}"),
        "a butterfly's beat": good.replace("\n  beatHz: 5\n", "\n  beatHz: 1.15\n"),
        "no network relay": good.replace(f"  - component: {{fileID: {RELAY_ID}}}\n", "", 1),
        "the dust capsule back": good.replace("  _nearFieldSkimmer: {fileID: 0}\n", f"  _nearFieldSkimmer: {{fileID: {DUST_SKIMMER}}}\n"),
        "the Moth's camera": good.replace(f"guid: {CAMERA_GUID}", f"guid: {DONOR_CAMERA_GUID}"),
        "the Moth's HUD": good.replace(f"guid: {guid_of(OWN_HUD_META)}", f"guid: {BUTTERFLY_HUD_GUID}"),
    }
    failed = 0
    try:
        for name, bad in cases.items():
            write_quiet(PREFAB, bad)
            if not errors():
                print(f"  SELF-TEST FAILED: '{name}' was not caught"); failed += 1
            else:
                print(f"  caught: {name}")
    finally:
        write_quiet(PREFAB, good)
    others = [
        # the slot may hold a listed placeholder (sound_placeholders.json) or be empty: an event path no ledger lists
        ("borrowed FMOD event", CONFIG, "  stingEvent:\n",
         lambda t: re.sub(r"(  stingEvent:\n    Guid:\n(?:      Data\d: -?\d+\n){4}    Path: ).*", r"\g<1>event:/SFX/Borrowed", t, count=1)),
        ("suction on Time", CONFIG, "  suctionReachScale:\n    Enabled: 1\n    Value: 1\n    Min: 1\n    Max: 3\n    element: 3\n",
         "  suctionReachScale:\n    Enabled: 1\n    Value: 1\n    Min: 1\n    Max: 3\n    element: 4\n"),
        ("omni explodes", IMPACTOR, f"guid: {HATCH_GUID}", f"guid: {BUTTERFLY_EXPLOSION_GUID}"),
        ("no Maw", MAP, "UpgradeLabel: Maw\n", "UpgradeLabel: \n"),
    ]
    for name, path, old, new in others:
        text = read(path)
        if old not in text:
            print(f"  SELF-TEST FAILED: '{name}' could not be planted"); failed += 1; continue
        try:
            write_quiet(path, new(text) if callable(new) else text.replace(old, new, 1))
            if not errors():
                print(f"  SELF-TEST FAILED: '{name}' was not caught"); failed += 1
            else:
                print(f"  caught: {name}")
        finally:
            write_quiet(path, text)
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
    print("jewel wasp assets: " + ("FAIL" if errs else "ok"))
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main())
