#!/usr/bin/env python3
"""The Angler's VESSEL CREATION TOOL: its prefab, assets and every registration, then prove the shipped set.

    python3 Tools/Build/author_angler_assets.py             # author what is missing (idempotent), then check
    python3 Tools/Build/author_angler_assets.py --check     # validate the SHIPPED files, write nothing
    python3 Tools/Build/author_angler_assets.py --self-test # prove every check fires (negative controls)

Design: Assets/_Scripts/Controller/Vessel/R_VesselActions/ANGLER.md. Decided in the Angler Command Lab
(Docs/Studios/AnglerCommandLab.html, Docs/Studios/ANGLER_COMMAND_LAB.md round 2: the Sling painter).

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/Angler.prefab (+ .meta) - a document-level clone of Stoat.prefab (the fleet's leanest
    self-owned hull, the donor author_brittlestar_assets.py also uses), turned into the Angler:
        - root GameObject / VesselStatus renamed, `vesselType: 17` (VesselClassType.Angler);
        - a FRESH Netcode GlobalObjectIdHash for THIS prefab guid and InScenePlacedSourceGlobalObjectIdHash cleared
          (vessel rule 42);
        - the flight model: the Stoat's two-stick transformer block on the SAME fileID, its script swapped to
          AnglerVesselTransformer (the lure brake), cruise 40 u/s and boost x2.75 (110 u/s) - the lab's flight numbers;
        - the executors: every Stoat executor removed; AnglerCommandExecutor added (config, turn-end channel);
        - the bindings: X (Button3) Lure, A (Button1) Squad, B (Button2) Recall. The Sling painter reads the two
          TRIGGERS as analog depth, so neither trigger is bound to an action;
        - the AI: no blind ability cycle; the donor's boost policy kept (SkimRingAIPolicy, inert on a hull with no
          Squirrel tube bound - exactly as on the Stoat); no Angler autopilot yet (ANGLER.md follow-ups);
        - the HUD: AnglerHUDVariant (a copy of StoatHUDVariant: same internal fileIDs, the view swapped to
          AnglerHUDView, the four gauges renamed, the four icons swapped) and AnglerHUDController;
        - the camera: AnglerCameraSettingsSO (the lab's chase framing).
    KEPT FROM THE DONOR ON PURPOSE (a stand-in body until the Angler has its own, ANGLER.md §5): the Stoat's procedural
    hull (StoatHullBuilder) and its lope (StoatAnimation), the two jets, the tails, the hit box, the impactor and skimmer
    containers (shared effect SOs; the Angler adds no effect of its own).
    Assets: AnglerConfig + the three control assets (VesselActions/Angler/), the camera, SO_Class_Angler, every new
    script's .meta, the doc's .meta. Registrations: Vessel Prefab Container (root TRANSFORM), DefaultNetworkPrefabs (root
    GAMEOBJECT), SO_Classlist_All / _Classes, four CrystalHullFusionConfig entries (copied from the Stoat's: same hull).
    NOT here (code, held by tests): the enum member, ToyVesselRoster.Default, EnumIntegrityTests.
    NOT here AT ALL: Assets/Resources/ElementalAbilityMaps/Angler.asset. The element -> ability mapping is a PROPOSAL
    awaiting the designer (ANGLER.md §3; the vessel skill's design gate forbids authoring one to fill a slot). Without it
    every elemental multiplier is 1x and no level-5 upgrade unlocks, which is the honest state of an unapproved design.

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md: "a spent one-shot must stand down, not abort").
The prefab and the HUD variant are built ONLY while absent; --check validates the SHIPPED files' invariants. To
re-author from scratch, delete Angler.prefab / AnglerHUDVariant.prefab and their .metas and run again.

TUNING IS SEEDED, NOT OWNED: AnglerConfig.asset and the camera asset are written once if absent and then only checked
for their script and name. Their numbers belong to the ARTIFACT: Tools/Build/studio_to_unity.py --vessel Angler writes
the lab's values (map: Tools/Build/studio_to_unity/angler.json). Everything else listed above is owned outright.
"""
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import author_stoat_assets as S  # noqa: E402  (document-model helpers + Netcode hash; importing it writes nothing)
import author_angler_icons as I  # noqa: E402  (the icon and card guids; importing it writes nothing)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
A = lambda *p: os.path.join(ROOT, "Assets", *p)
VESSELS = A("_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Stoat.prefab")
PREFAB = os.path.join(VESSELS, "Angler.prefab")
HUD_DIR = A("_Prefabs", "UI Elements", "VesselHUD")
HUD_DONOR = os.path.join(HUD_DIR, "StoatHUDVariant.prefab")
HUD_VARIANT_PATH = os.path.join(HUD_DIR, "AnglerHUDVariant.prefab")
ACTIONS_DIR = A("_SO_Assets", "VesselActions", "Angler")
CONFIG_PATH = os.path.join(ACTIONS_DIR, "AnglerConfig.asset")
CONTAINER = A("_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = A("DefaultNetworkPrefabs.asset")
CLASSES = A("_SO_Assets", "Classes")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Angler.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAP = A("Resources", "ElementalAbilityMaps", "Angler.asset")
FUSION = A("Resources", "CrystalHullFusionConfig.asset")
CAMERA = A("_SO_Assets", "Camera", "AnglerCameraSettingsSO.asset")
SCRIPTS = A("_Scripts")
DOC = os.path.join(SCRIPTS, "Controller", "Vessel", "R_VesselActions", "ANGLER.md")

CLASS_ID = 17
STOAT_CLASS_ID = 15


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/Angler/{name}".encode()).hexdigest()


PREFAB_GUID = guid_for("Angler.prefab")
HUD_VARIANT = guid_for("AnglerHUDVariant.prefab")
CONFIG = guid_for("AnglerConfig.asset")
CLASS_GUID = guid_for("SO_Class_Angler.asset")
CAMERA_GUID = guid_for("AnglerCameraSettingsSO.asset")
ACTIONS_FOLDER_GUID = guid_for("VesselActions/Angler")
DOC_GUID = guid_for("ANGLER.md")

# (control name, AnglerControl value, InputEvents it is bound to). The InputEvents numbers: Button1=6 (A / Space),
# Button2=7 (B / R), Button3=8 (X / Q). The triggers (RightStickAction=1, LeftStickAction=2) stay unbound: the Sling
# painter reads their analog depth from the replicated InputStatus. LB raises no event in the game, so it is unused.
CONTROLS = (("AnglerLure", 0, 8), ("AnglerSquad", 1, 6), ("AnglerRecall", 2, 7))
CONTROL_GUID = {name: guid_for(name + ".asset") for name, _, _ in CONTROLS}

# Every script this vessel added: (path under Assets/_Scripts, guid).
NEW_SCRIPTS = tuple((rel, guid_for(os.path.basename(rel))) for rel in (
    "Controller/Environment/FloraAndFauna/IFaunaLeash.cs",
    "Controller/Vessel/AnglerCommandMath.cs",
    "Controller/Vessel/AnglerVesselTransformer.cs",
    "Controller/Vessel/R_VesselActions/AnglerWorldMarks.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/AnglerActionSO.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/AnglerConfigSO.cs",
    "Controller/Vessel/R_VesselActions/Executors/AnglerCommandExecutor.cs",
    "UI/Controller/AnglerHUDController.cs",
    "UI/View/AnglerHUDView.cs",
    "Tests/Editor/AnglerCommandMathTests.cs",
))
SCRIPT = {os.path.basename(rel)[:-3]: guid for rel, guid in NEW_SCRIPTS}

# ---- the donor's documents (Stoat.prefab), by fileID (the same table author_brittlestar_assets.py reads) -------------
ROOT_GO = "6417075533431866457"
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "1733035045152327888"
TRANSFORMER_ID = "7686683633640807837"
AI_PILOT_ID = "3440187540988767646"
HANDLER_ID = "9172589084772723142"
CAMERA_CUSTOMIZER_ID = "6989089190155014109"
HUD_CONTROLLER_ID = "5649393235309590662"
SHIP_ACTIONS_GO = "7850473780452578400"
REGISTRY_ID = "9105512684531825410"
STOAT_EXECUTORS = tuple(f"513726498001234560{i}" for i in range(1, 7))
HULL_GO_ID = "5137264980012345610"
HUD_INSTANCE_ID, HUD_VIEW_ID = "5137264980012345620", "5137264980012345622"
STOAT_HUD_VARIANT = "1f9b41e6fa58494599674753191b495b"
TWO_STICK_SCRIPT = "95baa921cf22958449a3143749d5e0e3"          # VesselTransformer (the Stoat's)

# ---- new documents (one readable family, all < INT64_MAX) ----------------------------------------------------------
EXECUTOR_ID = "4417001122334455001"

# ---- fleet guids ---------------------------------------------------------------------------------------------------
TURN_END_CHANNEL = "498a06d44bde9184f985c938c803b2a1"
SKIM_RING_POLICY = "c691910cb0114ea7aeb572bc51b71a6a"         # StoatAIBoostPolicy.asset (SkimRingAIPolicySO)
SO_VESSEL_SCRIPT = "0309a5343d820c14abd73b35f6fa50f1"
CAMERA_SCRIPT = "fd4e6f3597f7439ba7333b35a3a9e164"
# The Stoat's four icon sprites (charge, mass, space, time) and the Angler's that replace them in the HUD variant.
ICON_SWAP = {"ebdd346798d474b5a5ab2364683a8725": I.guid_for("Angler_Command.png"),
             "783faf75777a15c5b2241cfc3e2691d3": I.guid_for("Angler_Leash.png"),
             "586d56db81bb568c940094830cd59abb": I.guid_for("Angler_Lure.png"),
             "669089ec7ad29ed295eccc53d8615225": I.guid_for("Angler_Tame.png")}
CARD_ACTIVE, CARD_INACTIVE = I.guid_for(I.CARD_ACTIVE), I.guid_for(I.CARD_INACTIVE)
# Stoat-only BEHAVIOUR that must not survive the clone (the hull and the lope are kept: see the module doc).
STOAT_SCRIPTS = ("86da9d91d4364f02ad41dd723756e970",   # StoatHUDController
                 "c120940f60a14c2aa390446ea0791b5a",   # StoatHUDView
                 "cf82f131a8df416cb205ab8055108b82",   # StoatSlingExecutor
                 "6eee000b02734120a5138fb645997915",   # StoatDipoleExecutor
                 "ec03f8093b534dfe98ec25e13f18c576",   # StoatPathfinderExecutor
                 "064761a8ffa04919a1308c63fe4b0732",   # StoatModeExecutor
                 "1712753ee2d34c6294526e3c58d4b197",   # StoatRippleExecutor
                 TWO_STICK_SCRIPT)                     # swapped for AnglerVesselTransformer

# The lab's flight (Docs/Studios/AnglerCommandLab.html SHIPPED: cruise 40, boost 110). Cruise = MinimumSpeed +
# ThrottleScaler at full throttle (VesselTransformer), boost = cruise x MaxBoostMultiplier.
CRUISE, BOOST = 40, 110


# ===================================================================================================== the prefab
def executor_block() -> str:
    return (S.mb_header(EXECUTOR_ID, SHIP_ACTIONS_GO, SCRIPT["AnglerCommandExecutor"]) +
            f"  config: {{fileID: 11400000, guid: {CONFIG}, type: 2}}\n"
            f"  OnMiniGameTurnEnd: {{fileID: 11400000, guid: {TURN_END_CHANNEL}, type: 2}}\n")


def bindings() -> str:
    """Shared (every device): the three buttons. The triggers are read as analog depth, never bound."""
    out = "  _inputEventShipActions:\n"
    for name, _, event in sorted(CONTROLS, key=lambda c: c[2]):
        out += f"  - InputEvent: {event}\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL_GUID[name]}, type: 2}}\n"
    return out + "  _resourceEventClassActions: []\n  _touchActionOverrides: []\n  _gamepadActionOverrides: []\n"


def hud_controller_block() -> str:
    return (S.mb_header(HUD_CONTROLLER_ID, ROOT_GO, SCRIPT["AnglerHUDController"], "Assembly-CSharp::CosmicShore.UI.AnglerHUDController") +
            f"  baseView: {{fileID: {HUD_VIEW_ID}}}\n"
            "  _iconSetSwitcher: {fileID: 0}\n"
            f"  view: {{fileID: {HUD_VIEW_ID}}}\n"
            f"  angler: {{fileID: {EXECUTOR_ID}}}\n"
            "  lureEaseRate: 6\n")


def build_prefab(donor: str) -> str:
    text = donor
    text = S.replace_once(text, "  m_Name: Stoat\n", "  m_Name: Angler\n", "root GameObject name")
    text = S.replace_once(text, "  _name: Stoat\n", "  _name: Angler\n", "VesselStatus _name")
    text = S.replace_once(text, f"  vesselType: {STOAT_CLASS_ID}\n", f"  vesselType: {CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)

    head, docs = S.split_docs(text)

    # Executors: every Stoat one out, the Angler's in.
    ship = S.get_doc(docs, SHIP_ACTIONS_GO)
    for fid in STOAT_EXECUTORS:
        ship = ship.replace(f"  - component: {{fileID: {fid}}}\n", "")
    ship = ship.replace(f"  - component: {{fileID: {REGISTRY_ID}}}\n",
                        f"  - component: {{fileID: {REGISTRY_ID}}}\n  - component: {{fileID: {EXECUTOR_ID}}}\n", 1)
    S.set_doc(docs, SHIP_ACTIONS_GO, ship)
    reg = S.get_doc(docs, REGISTRY_ID)
    reg = re.sub(r"  _executors:\n(?:  - \{fileID: -?\d+\}\n)+", f"  _executors:\n  - {{fileID: {EXECUTOR_ID}}}\n", reg)
    S.set_doc(docs, REGISTRY_ID, reg)
    S.remove_docs(docs, STOAT_EXECUTORS)
    docs.insert(S.doc_index(docs, REGISTRY_ID) + 1, [EXECUTOR_ID, executor_block()])

    # Bindings: the three buttons.
    handler = S.get_doc(docs, HANDLER_ID)
    handler = re.sub(r"  _inputEventShipActions:(?:.*\n)*?(?=  _onButtonPressed:)", bindings(), handler, count=1)
    S.set_doc(docs, HANDLER_ID, handler)

    # Flight model: the Stoat's two-stick block on the same fileID, as AnglerVesselTransformer, at the lab's speeds.
    tr = S.get_doc(docs, TRANSFORMER_ID)
    tr = tr.replace(f"guid: {TWO_STICK_SCRIPT}, type: 3}}", f"guid: {SCRIPT['AnglerVesselTransformer']}, type: 3}}", 1)
    tr = S.set_field(tr, "DefaultThrottleScaler", str(CRUISE))
    tr = S.set_field(tr, "DefaultMinimumSpeed", "0")
    tr = S.set_field(tr, "MaxBoostMultiplier", S.num(BOOST / CRUISE))
    S.set_doc(docs, TRANSFORMER_ID, tr)

    # HUD controller, AI, camera.
    S.set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    cam = S.get_doc(docs, CAMERA_CUSTOMIZER_ID)
    cam = re.sub(r"  settings: \{[^}]*\}\n", f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}\n", cam)
    S.set_doc(docs, CAMERA_CUSTOMIZER_ID, cam)

    # The stand-in body keeps its fileIDs; only its GameObject's name says whose it is now.
    S.set_doc(docs, HULL_GO_ID, S.get_doc(docs, HULL_GO_ID).replace("  m_Name: StoatHull\n", "  m_Name: AnglerHull\n"))

    # HUD: the Angler's variant, same internal fileIDs.
    for fid, chunk in list(docs):
        if f"m_PrefabInstance: {{fileID: {HUD_INSTANCE_ID}}}" in chunk or fid == HUD_INSTANCE_ID:
            chunk = chunk.replace(STOAT_HUD_VARIANT, HUD_VARIANT)
            chunk = chunk.replace("      value: StoatHUDVariant\n", "      value: AnglerHUDVariant\n")
            chunk = chunk.replace(f"guid: {S.HUD_VIEW_SCRIPT}, type: 3}}", f"guid: {SCRIPT['AnglerHUDView']}, type: 3}}")
            chunk = chunk.replace("CosmicShore.UI.StoatHUDView", "CosmicShore.UI.AnglerHUDView")
            S.set_doc(docs, fid, chunk)

    return S.join_docs(head, docs)


# ===================================================================================================== the HUD variant
GAUGES = (("rippleGauge", "commandGauge", "RippleGauge", "CommandGauge"),     # charge
          ("modeGauge", "leashGauge", "ModeGauge", "LeashGauge"),             # mass
          ("accretionGauge", "lureGauge", "AccretionGauge", "LureGauge"),     # space
          ("timeGauge", "tameGauge", "TimeGauge", "TameGauge"))               # time


def build_hud_variant(donor: str) -> str:
    text = donor.replace("      value: StoatHUDVariant\n", "      value: AnglerHUDVariant\n")
    text = text.replace(f"guid: {S.HUD_VIEW_SCRIPT}, type: 3}}", f"guid: {SCRIPT['AnglerHUDView']}, type: 3}}")
    text = text.replace("CosmicShore.UI.StoatHUDView", "CosmicShore.UI.AnglerHUDView")
    for old, new, old_go, new_go in GAUGES:
        text = S.replace_once(text, f"  {old}: {{fileID:", f"  {new}: {{fileID:", f"view field {old}")
        text = S.replace_once(text, f"  m_Name: {old_go}\n", f"  m_Name: {new_go}\n", f"gauge object {old_go}")
    for old, new in ICON_SWAP.items():
        text = S.replace_once(text, f"guid: {old}, type: 3}}", f"guid: {new}, type: 3}}", f"icon sprite {old}")
    return text


# ===================================================================================================== assets
def config_asset() -> str:
    """SEEDED once. Numbers = AnglerConfigSO's field initializers = the lab's SHIPPED (studio_to_unity owns them after)."""
    return (S.so_header(SCRIPT["AnglerConfigSO"], "AnglerConfig") +
            "  lureAhead: 14\n"
            "  lureCruise: 12\n"
            "  lureRadius: 70\n"
            "  tameRadius: 22\n"
            "  tameTime: 2.5\n"
            "  tameDecay: 0.6\n"
            "  squadCap: 24\n"
            "  leashTow: 1.6\n"
            "  formBack: 6\n"
            "  tadTurn: 4\n"
            "  spareOwn: 1\n"
            "  paintRange: 160\n"
            "  paintCone: 8\n"
            "  brushMin: 0.35\n"
            "  eraseCone: 4\n"
            "  slingAhead: 12\n"
            "  slingSpeed: 150\n"
            "  homeTurn: 12\n"
            "  slingTime: 3\n"
            "  slingCatch: 8\n"
            "  slingCluster: 24\n"
            "  maxPaintMarkers: 400\n"
            "  brushInterval: 0\n")


def control_asset(name: str, value: int) -> str:
    return S.so_header(SCRIPT["AnglerActionSO"], name) + f"  control: {value}\n"


def camera_asset() -> str:
    """SEEDED once. The lab's chase (AnglerCommandLab.html render(): 40 u behind, 14 up, looking 55 u ahead, closing at
    0.18 a frame = 12/s, a 62-degree frame) - pulled back far enough that the school round the hull reads. The player's
    FOV stays the player's (framingFieldOfView, vessel rule 4.ad)."""
    return (S.so_header(CAMERA_SCRIPT, "AnglerCameraSettingsSO") +
            "  mode: 0\n"
            "  followOffset: {x: 0, y: 14, z: -40}\n"
            "  lookAheadDistance: 55\n"
            "  lookAheadLift: 0\n"
            "  chaseEaseRate: 12\n"
            "  framingFieldOfView: 62\n"
            "  dynamicMinDistance: 10\n"
            "  dynamicMaxDistance: 40\n"
            "  followSmoothTime: 0.2\n"
            "  rotationSmoothTime: 5\n"
            "  disableSmoothing: 0\n"
            "  nearClipPlane: 0.3\n"
            "  farClipPlane: 12000\n"
            "  enableAdaptiveZoom: 0\n"
            "  adaptiveMaxDistance: 0\n"
            "  orthographicSize: 5\n")


def class_asset() -> str:
    return (S.so_header(SO_VESSEL_SCRIPT, "SO_Class_Angler").replace("  m_EditorClassIdentifier: \n",
                                                                    "  m_EditorClassIdentifier: Assembly-CSharp::SO_Vessel\n") +
            f"  Class: {CLASS_ID}\n"
            "  Name: Angler\n"
            "  Description: The fleet's strategist. Hold the lure light out to draw wild fauna, tame the ones you hold,\n"
            "    and lead them as a school. Paint the prisms you want gone with RT, erase mistakes with LT, and let go to\n"
            "    throw the squad at what you painted.\n"
            "  PrimaryElement: 0\n"
            "  Element: {fileID: 0}\n"
            "  InitialResourceLevels: {Mass: 0, Charge: 0, Space: 0, Time: 0}\n"
            f"  IconActive: {{fileID: 21300000, guid: {CARD_ACTIVE}, type: 3}}\n"
            f"  IconInactive: {{fileID: 21300000, guid: {CARD_INACTIVE}, type: 3}}\n"
            "  PreviewImage: {fileID: 0}\n"
            "  SquadImage: {fileID: 0}\n"
            "  TrailPreviewImage: {fileID: 0}\n"
            "  CardSilohoutteActive: {fileID: 0}\n"
            "  CardSilohoutteInactive: {fileID: 0}\n"
            "  Abilities: []\n"
            "  Games: []\n"
            "  TrainingGames: []\n"
            "  gameplayParameter1:\n    LeftHandLabel: Casual\n    RightHandLabel: Challenging\n    Value: 0.6\n"
            "  gameplayParameter2:\n    LeftHandLabel: Relaxing\n    RightHandLabel: Thrilling\n    Value: 0.4\n"
            "  gameplayParameter3:\n    LeftHandLabel: Solo\n    RightHandLabel: Social\n    Value: 0.5\n"
            "  isLocked: 0\n"
            "  ownedFromStart: 0\n"
            "  UnlockCost: 100\n")


def text_meta(guid: str) -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "TextScriptImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


def folder_meta(guid: str) -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


def prefab_meta(guid: str) -> str:
    return S.prefab_meta().replace(S.PREFAB_GUID, guid)


def owned_files() -> "dict[str, str]":
    out = {
        CLASS_ASSET: class_asset(), CLASS_ASSET + ".meta": S.asset_meta(CLASS_GUID),
        CAMERA + ".meta": S.asset_meta(CAMERA_GUID),
        CONFIG_PATH + ".meta": S.asset_meta(CONFIG),
        ACTIONS_DIR + ".meta": folder_meta(ACTIONS_FOLDER_GUID),
        PREFAB + ".meta": prefab_meta(PREFAB_GUID),
        HUD_VARIANT_PATH + ".meta": prefab_meta(HUD_VARIANT),
        DOC + ".meta": text_meta(DOC_GUID),
    }
    for name, value, _ in CONTROLS:
        path = os.path.join(ACTIONS_DIR, name + ".asset")
        out[path] = control_asset(name, value)
        out[path + ".meta"] = S.asset_meta(CONTROL_GUID[name])
    for rel, guid in NEW_SCRIPTS:
        out[os.path.join(SCRIPTS, rel) + ".meta"] = S.script_meta(guid)
    return out


SEEDED = {CONFIG_PATH: (config_asset, SCRIPT["AnglerConfigSO"], "AnglerConfig"),
          CAMERA: (camera_asset, CAMERA_SCRIPT, "AnglerCameraSettingsSO")}


# ===================================================================================================== registrations
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


def register_class_list(text: str) -> str:
    if CLASS_GUID in text:
        return text
    m = re.search(r"  VesselList:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    if not m:
        raise RuntimeError("class list: VesselList not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {CLASS_GUID}, type: 2}}\n" + text[m.end(1):]


def register_fusion(text: str) -> str:
    """Four entries copied from the Stoat's: the Angler wears the Stoat's procedural hull, so the fusion is the same."""
    if re.search(r"^  - vessel: %d\n" % CLASS_ID, text, re.M):
        return text
    blocks = re.findall(r"(  - vessel: %d\n(?:    .*\n)+)" % STOAT_CLASS_ID, text)
    if len(blocks) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig drifted: {len(blocks)} Stoat entries, expected 4")
    added = "".join(b.replace(f"  - vessel: {STOAT_CLASS_ID}\n", f"  - vessel: {CLASS_ID}\n", 1) for b in blocks)
    last = text.rindex(blocks[-1]) + len(blocks[-1])
    return text[:last] + added + text[last:]


# ===================================================================================================== checks
def check(files: "dict[str, str]") -> "list[str]":
    errors = []
    prefab = files.get(PREFAB)
    if prefab is None:
        return ["Angler.prefab is missing"]
    head, docs = S.split_docs(prefab)
    ids = {k for k, _ in docs}
    d = dict(docs)

    def need(cond, msg):
        if not cond:
            errors.append(msg)

    need(f"  vesselType: {CLASS_ID}\n" in prefab, "vesselType is not 17 (Angler)")
    need("  _name: Angler\n" in prefab and "  m_Name: Angler\n" in prefab, "the prefab is not named Angler")
    m = re.search(r"  GlobalObjectIdHash: (\d+)\n", d.get(NETWORK_OBJECT_ID, ""))
    need(m and int(m.group(1)) == S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "GlobalObjectIdHash is not this prefab's (a copied hash collides with the donor's, vessel rule 42)")
    for guid in STOAT_SCRIPTS + (STOAT_HUD_VARIANT,):
        need(guid not in prefab, f"a Stoat-only reference survived the clone: {guid}")
    for fid in STOAT_EXECUTORS:
        need(fid not in ids, f"Stoat executor {fid} still present")
    ex = d.get(EXECUTOR_ID, "")
    need(f"guid: {SCRIPT['AnglerCommandExecutor']}" in ex, "AnglerCommandExecutor missing")
    need(f"  - {{fileID: {EXECUTOR_ID}}}\n" in d.get(REGISTRY_ID, ""), "AnglerCommandExecutor is not in the ActionExecutorRegistry")
    need(f"guid: {CONFIG}" in ex and TURN_END_CHANNEL in ex, "AnglerCommandExecutor is not wired (config / turn-end channel)")
    tr = d.get(TRANSFORMER_ID, "")
    need(f"guid: {SCRIPT['AnglerVesselTransformer']}" in tr, "the transformer is not AnglerVesselTransformer")
    need(f"  DefaultThrottleScaler: {CRUISE}\n" in tr and f"  MaxBoostMultiplier: {S.num(BOOST / CRUISE)}\n" in tr,
         f"the transformer is not at the lab's flight (cruise {CRUISE}, boost {BOOST})")
    need(f"guid: {SCRIPT['AnglerHUDController']}" in d.get(HUD_CONTROLLER_ID, "")
         and f"angler: {{fileID: {EXECUTOR_ID}}}" in d.get(HUD_CONTROLLER_ID, ""), "the HUD controller is not AnglerHUDController wired to the executor")
    need(f"guid: {HUD_VARIANT}" in d.get(HUD_INSTANCE_ID, ""), "the HUD instance is not AnglerHUDVariant")
    need(f"guid: {CAMERA_GUID}" in d.get(CAMERA_CUSTOMIZER_ID, ""), "the camera is not AnglerCameraSettingsSO")
    need("  abilities: []\n" in d.get(AI_PILOT_ID, ""), "the AIPilot runs a blind ability cycle (the Angler has no autopilot yet)")
    need(f"boostPolicy: {{fileID: 11400000, guid: {SKIM_RING_POLICY}, type: 2}}" in d.get(AI_PILOT_ID, ""),
         "the AIPilot's boost policy is not the donor's SkimRingAIPolicy (inert without a Squirrel tube; the shared-channel gate wants it wired)")
    handler = d.get(HANDLER_ID, "")
    for name, _, event in CONTROLS:
        need(re.search(r"  - InputEvent: %d\n    ShipActions:\n    - \{fileID: 11400000, guid: %s, type: 2\}" % (event, CONTROL_GUID[name]), handler),
             f"{name} is not bound to InputEvents {event}")
    need(not re.search(r"  - InputEvent: [12]\n", handler), "a trigger is bound to an action (the Sling painter reads their depth)")
    need("_gamepadActionOverrides: []" in handler and "_touchActionOverrides: []" in handler, "device overrides must be empty (shared bindings)")
    dangling = S.dangling_refs(prefab)
    need(not dangling, f"{len(dangling)} dangling local reference(s): {dangling[:5]}")

    hud = files.get(HUD_VARIANT_PATH)
    if hud is None:
        errors.append("AnglerHUDVariant.prefab is missing")
    else:
        need(f"guid: {SCRIPT['AnglerHUDView']}" in hud and S.HUD_VIEW_SCRIPT not in hud, "the HUD variant's view is not AnglerHUDView")
        for _, field, _, _ in GAUGES:
            need(re.search(r"  %s: \{fileID: [1-9]\d*\}" % field, hud), f"HUD view field {field} is not wired")
        for old, new in ICON_SWAP.items():
            need(old not in hud and new in hud, f"HUD icon {new} not swapped in")

    for path, want in owned_files().items():
        have = files.get(path)
        need(have == want, f"{os.path.relpath(path, ROOT)} is {'missing' if have is None else 'drifted'} - run the tool")
    for path, (_, script, name) in SEEDED.items():
        have = files.get(path) or ""
        need(f"guid: {script}, type: 3}}" in have and f"  m_Name: {name}\n" in have, f"{os.path.relpath(path, ROOT)} missing or not a {name}")
    need(MAP not in files, "ElementalAbilityMaps/Angler.asset exists - the element mapping is an unapproved PROPOSAL (ANGLER.md §3)")

    need(f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(CONTAINER, ""),
         "not registered in the Vessel Prefab Container (by its root TRANSFORM)")
    need(f"    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(NETWORK_PREFABS, ""),
         "not registered in DefaultNetworkPrefabs (by its root GAMEOBJECT)")
    for path in CLASS_LISTS:
        need(CLASS_GUID in files.get(path, ""), f"SO_Class_Angler is not in {os.path.basename(path)}")
    need(len(re.findall(r"^  - vessel: %d\n" % CLASS_ID, files.get(FUSION, ""), re.M)) == 4,
         "CrystalHullFusionConfig does not carry exactly four Angler entries")
    for asset, guid in ((os.path.join(ROOT, "Assets/_Graphics/CardImages/Angler.png.meta"), CARD_ACTIVE),
                        (os.path.join(ROOT, "Assets/_Graphics/CardImages/Angler_Inactive.png.meta"), CARD_INACTIVE)):
        need(f"guid: {guid}" in files.get(asset, ""), f"{os.path.basename(asset)} missing (author_angler_icons.py)")
    enum = files.get(os.path.join(SCRIPTS, "Data/Enums/VesselClassType.cs"), "")
    need(re.search(r"\bAngler = %d," % CLASS_ID, enum), "VesselClassType has no Angler = 17")
    roster = files.get(os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs"), "")
    need("VesselClassType.Angler" in roster, "ToyVesselRoster.Default does not offer the Angler (vessel rule 40)")
    return errors


def read_all() -> "dict[str, str]":
    paths = {PREFAB, HUD_VARIANT_PATH, CONTAINER, NETWORK_PREFABS, FUSION, MAP, *CLASS_LISTS, *owned_files(), *SEEDED,
             os.path.join(ROOT, "Assets/_Graphics/CardImages/Angler.png.meta"),
             os.path.join(ROOT, "Assets/_Graphics/CardImages/Angler_Inactive.png.meta"),
             os.path.join(SCRIPTS, "Data/Enums/VesselClassType.cs"),
             os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs")}
    out = {}
    for p in paths:
        if os.path.exists(p):
            with open(p, encoding="utf-8", newline="") as fh:
                out[p] = fh.read()
    return out


def write(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def author() -> None:
    files = read_all()
    if PREFAB not in files:
        write(PREFAB, build_prefab(open(DONOR, encoding="utf-8", newline="").read()))
        print("cloned Stoat.prefab -> Angler.prefab")
    else:
        print("Angler.prefab exists - the clone stands down (spent one-shot)")
    if HUD_VARIANT_PATH not in files:
        write(HUD_VARIANT_PATH, build_hud_variant(open(HUD_DONOR, encoding="utf-8", newline="").read()))
        print("cloned StoatHUDVariant.prefab -> AnglerHUDVariant.prefab")
    for path, text in owned_files().items():
        if files.get(path) != text:
            write(path, text)
            print("wrote", os.path.relpath(path, ROOT))
    for path, (make, _, _) in SEEDED.items():
        if path not in files:
            write(path, make())
            print("seeded", os.path.relpath(path, ROOT))
    for path, fn in ((CONTAINER, register_container), (NETWORK_PREFABS, register_network), (FUSION, register_fusion),
                     *((p, register_class_list) for p in CLASS_LISTS)):
        before = open(path, encoding="utf-8", newline="").read()
        after = fn(before)
        if after != before:
            write(path, after)
            print("registered in", os.path.relpath(path, ROOT))


def self_test() -> int:
    files = read_all()
    base = check(files)
    if base:
        print("self-test needs a clean shipped set first:\n  " + "\n  ".join(base))
        return 1
    mutations = {
        "vesselType back to the Stoat's": (PREFAB, lambda t: t.replace(f"  vesselType: {CLASS_ID}\n", "  vesselType: 15\n")),
        "executor dropped from the registry": (PREFAB, lambda t: t.replace(f"  _executors:\n  - {{fileID: {EXECUTOR_ID}}}\n", "  _executors: []\n")),
        "a dangling reference": (PREFAB, lambda t: t.replace(f"  angler: {{fileID: {EXECUTOR_ID}}}", "  angler: {fileID: 12345}")),
        "the donor's hash": (PREFAB, lambda t: re.sub(r"  GlobalObjectIdHash: \d+", "  GlobalObjectIdHash: 1", t, count=1)),
        "a Stoat executor left behind": (PREFAB, lambda t: t.replace(SCRIPT["AnglerCommandExecutor"], "6eee000b02734120a5138fb645997915")),
        "the Stoat's flight back": (PREFAB, lambda t: t.replace(f"  DefaultThrottleScaler: {CRUISE}\n", "  DefaultThrottleScaler: 60\n")),
        "a trigger bound": (PREFAB, lambda t: t.replace("  - InputEvent: 6\n", "  - InputEvent: 1\n", 1)),
        "unregistered from the container": (CONTAINER, lambda t: t.replace(PREFAB_GUID, "0" * 32)),
        "Lure unbound": (PREFAB, lambda t: t.replace(CONTROL_GUID["AnglerLure"], "f" * 32)),
        "fusion entries missing": (FUSION, lambda t: t.replace(f"  - vessel: {CLASS_ID}\n", "  - vessel: 99\n")),
        "roster entry missing": (os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs"), lambda t: t.replace("VesselClassType.Angler", "")),
        "an unapproved map authored": (MAP, lambda t: "authored"),
    }
    failed = 0
    for name, (path, mutate) in mutations.items():
        copy = dict(files)
        copy[path] = mutate(copy.get(path, ""))
        if copy[path] == files.get(path):
            print(f"SELF-TEST: mutation '{name}' changed nothing")
            failed += 1
        elif not check(copy):
            print(f"SELF-TEST: mutation '{name}' was NOT caught")
            failed += 1
    print(f"author_angler_assets --self-test: {len(mutations) - failed}/{len(mutations)} negative controls fired")
    return 1 if failed else 0


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errors = check(read_all())
    for e in errors:
        print("FAIL:", e)
    print("author_angler_assets: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
