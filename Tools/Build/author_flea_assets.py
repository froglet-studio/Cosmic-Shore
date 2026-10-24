#!/usr/bin/env python3
"""The Flea's VESSEL CREATION TOOL: its prefab, assets and every registration, then prove the shipped set.

    python3 Tools/Build/author_flea_assets.py             # author what is missing (idempotent), then check
    python3 Tools/Build/author_flea_assets.py --check     # validate the SHIPPED files, write nothing
    python3 Tools/Build/author_flea_assets.py --self-test # prove every check fires (negative controls)

Design: Assets/_Scripts/Controller/Vessel/R_VesselActions/FLEA.md. Decided in the Vessel Studio's concept fleet (kind
`ricochet`, Docs/Studios/CONCEPT_FLEET.md; kept by Garrett on 2026-10-17: "bring it into the game, give me a branch").

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/Flea.prefab (+ .meta) - a document-level clone of Stoat.prefab (the fleet's leanest
    self-owned hull, the donor the Angler and the Brittle Star generators also use), turned into the Flea:
        - root GameObject / VesselStatus renamed, `vesselType: 24` (VesselClassType.Flea);
        - a FRESH Netcode GlobalObjectIdHash for THIS prefab guid and InScenePlacedSourceGlobalObjectIdHash cleared
          (vessel rule 42);
        - the flight model: the Stoat's two-stick transformer block on the SAME fileID, its script swapped to
          FleaVesselTransformer (FleaMath: crawl, bounce, hop) with its FleaConfig wired, the page's turn rates
          (pitch / yaw 110, roll 130: vessels/flea.json `flight`);
        - the executors: every Stoat executor removed and none added (the Flea's state is its transformer's);
        - the bindings: LT (LeftStickAction 2) = FleaCrouch, RT (RightStickAction 1) = FleaBounce, in the SHARED map and
          nothing in the device overrides, because every device raises those two events from its triggers (pad
          triggers, keyboard Left / Right Shift, mouse RMB / LMB, the touch trigger cards);
        - the AI: no ability cycle (an AIPilot flies the hull; it never presses a Flea control);
        - the HUD: the Stoat's HUD variant and view kept as a stand-in, driven by FleaHUDController;
        - the vessel impactor: FleaImpactorDataContainer, a fork of the Stoat's WITHOUT the hull-contact cost (prism
          damage, boost reset, speed change) and the Stoat's omni Overclock, WITH FleaBounceByPrismEffect: a prism is a
          surface to bounce off, never a crash.
    KEPT FROM THE DONOR ON PURPOSE (the Flea has no body yet): the Stoat model and its lope, the jets, the tails, the
    hit box, the skimmer, the camera, the card art. Each is a stand-in, named in FLEA.md §6.
    Assets: FleaConfig (seeded), FleaCrouch / FleaBounce, FleaBounceByPrismEffect, FleaImpactorDataContainer (seeded),
    SO_Class_Flea, the element map (four OPEN slots: the studio's element proposals are not signed off, vessel skill §3),
    every new script's .meta, the doc's .meta. Registrations: Vessel Prefab Container (root TRANSFORM),
    DefaultNetworkPrefabs (root GAMEOBJECT), SO_Classlist_All / _Classes, four CrystalHullFusionConfig entries (copied
    from the Stoat's: same hull). NOT here (code, held by tests): the enum member, ToyVesselRoster.Default,
    EnumIntegrityTests, OneThumbVesselCoverageTests' transformer table.

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md: "a spent one-shot must stand down, not abort").
The prefab is built ONLY while absent; patch_prefab re-applies the generator-owned parts idempotently; --check validates
the SHIPPED files. To re-author from scratch, delete Flea.prefab and its .meta and run again.

TUNING IS SEEDED, NOT OWNED: FleaConfig.asset is written once if absent and then only checked for its script, name and
keys. Its numbers belong to the ARTIFACT (/artifact-to-unity).
"""
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import author_stoat_assets as S  # noqa: E402  (document-model helpers + Netcode hash; importing it writes nothing)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
A = lambda *p: os.path.join(ROOT, "Assets", *p)
VESSELS = A("_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Stoat.prefab")
PREFAB = os.path.join(VESSELS, "Flea.prefab")
ACTIONS_DIR = A("_SO_Assets", "VesselActions", "Flea")
CONFIG_PATH = os.path.join(ACTIONS_DIR, "FleaConfig.asset")
CONTAINER = A("_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = A("DefaultNetworkPrefabs.asset")
CLASSES = A("_SO_Assets", "Classes")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Flea.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAP = A("Resources", "ElementalAbilityMaps", "Flea.asset")
FUSION = A("Resources", "CrystalHullFusionConfig.asset")
SCRIPTS = A("_Scripts")
DOC = os.path.join(SCRIPTS, "Controller", "Vessel", "R_VesselActions", "FLEA.md")
EFFECTS = A("_SO_Assets", "Effects")
BOUNCE_EFFECT = os.path.join(EFFECTS, "Vessel Prism Effects", "FleaBounceByPrismEffect.asset")
IMPACT_CONTAINER = os.path.join(EFFECTS, "Effect Containers", "VesselContainers", "FleaImpactorDataContainer.asset")
STOAT_IMPACT_CONTAINER_PATH = os.path.join(EFFECTS, "Effect Containers", "VesselContainers", "StoatImpactorDataContainer.asset")
ENUM = os.path.join(SCRIPTS, "Data", "Enums", "VesselClassType.cs")
ROSTER = os.path.join(SCRIPTS, "Controller", "Toys", "ToyVesselRoster.cs")

CLASS_ID = 24
STOAT_CLASS_ID = 15


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/Flea/{name}".encode()).hexdigest()


PREFAB_GUID = guid_for("Flea.prefab")
CONFIG = guid_for("FleaConfig.asset")
CLASS_GUID = guid_for("SO_Class_Flea.asset")
ACTIONS_FOLDER_GUID = guid_for("VesselActions/Flea")
DOC_GUID = guid_for("FLEA.md")
BOUNCE_EFFECT_GUID = guid_for("FleaBounceByPrismEffect.asset")
IMPACT_CONTAINER_GUID = guid_for("FleaImpactorDataContainer.asset")
MAP_GUID = guid_for("ElementalAbilityMaps/Flea.asset")
STOAT_IMPACT_CONTAINER = "63a1b912028e471b9b7a2df570e90be8"     # StoatImpactorDataContainer.asset
MAP_SCRIPT = "4b3f2a8c8f344355b16a4d4296ae3f98"                 # ElementalAbilityMapSO
IMPACT_CONTAINER_SCRIPT = "fde7d8c2dacf45f99e46799ffeea15e6"    # VesselImpactorDataContainerSO
# The Stoat container's hull-contact COST, which a Flea bounce replaces (the page: "a hull contact reflects it instead of
# resetting it"), and its omni crystal effect, which drives the Stoat's executors.
CONTACT_COSTS = {"4da62d4eefe03594ba35027dd537aff0": "VesselDamagePrismEffect",
                 "04f85a1f6b06b9248827bf8740168b2b": "VesselResetBoostPrismEffect",
                 "6c233c580949b13419f068454c74a110": "SquirrelVesselChangeSpeedByPrism"}
STOAT_OVERCLOCK = "6b508a166e124e95a6490871697185de"            # StoatOverclockByCrystalEffect.asset

# (control name, FleaControl value, InputEvents it is bound to): LeftStickAction = 2 (LT), RightStickAction = 1 (RT).
CONTROLS = (("FleaCrouch", 0, 2), ("FleaBounce", 1, 1))
CONTROL_GUID = {name: guid_for(name + ".asset") for name, _, _ in CONTROLS}

# Every script this vessel added: (path under Assets/_Scripts, guid).
NEW_SCRIPTS = tuple((rel, guid_for(os.path.basename(rel))) for rel in (
    "Controller/Vessel/FleaMath.cs",
    "Controller/Vessel/FleaVesselTransformer.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/FleaConfigSO.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/FleaActionSO.cs",
    "Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/FleaBounceByPrismEffectSO.cs",
    "UI/Controller/FleaHUDController.cs",
    "Tests/Editor/FleaMathTests.cs",
))
SCRIPT = {os.path.basename(rel)[:-3]: guid for rel, guid in NEW_SCRIPTS}

# ---- the donor's documents (Stoat.prefab), by fileID (the table author_angler_assets.py reads) ---------------------
ROOT_GO = "6417075533431866457"
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "1733035045152327888"
TRANSFORMER_ID = "7686683633640807837"
AI_PILOT_ID = "3440187540988767646"
HANDLER_ID = "9172589084772723142"
HUD_CONTROLLER_ID = "5649393235309590662"
SHIP_ACTIONS_GO = "7850473780452578400"
REGISTRY_ID = "9105512684531825410"
STOAT_EXECUTORS = tuple(f"513726498001234560{i}" for i in range(1, 7))
HULL_GO_ID = "5137264980012345610"
HUD_VIEW_ID = "5137264980012345622"
TWO_STICK_SCRIPT = "95baa921cf22958449a3143749d5e0e3"          # VesselTransformer (the Stoat's)
# Stoat-only BEHAVIOUR that must not survive the clone (its model, lope, HUD view and camera are kept as stand-ins).
STOAT_SCRIPTS = ("86da9d91d4364f02ad41dd723756e970",   # StoatHUDController
                 "cf82f131a8df416cb205ab8055108b82",   # StoatSlingExecutor
                 "8c3f54abda8145ecabf7c85253cab9ed",   # ToggleTranslationModeActionExecutor
                 "6eee000b02734120a5138fb645997915",   # StoatDipoleExecutor
                 "ec03f8093b534dfe98ec25e13f18c576",   # StoatPathfinderExecutor
                 "064761a8ffa04919a1308c63fe4b0732",   # StoatModeExecutor
                 "1712753ee2d34c6294526e3c58d4b197",   # StoatRippleExecutor
                 TWO_STICK_SCRIPT)                     # swapped for FleaVesselTransformer

# The page's flight (Docs/Studios/VesselStudio/vessels/flea.json `flight`: pitchYaw 110, roll 130 deg/s) and the kind's
# crawl (50 u/s, which FleaMath owns: the throttle never pushes this hull, so DefaultThrottleScaler only seeds `speed`).
CRAWL = 50
TURN_RATES = (("PitchScaler", 110), ("YawScaler", 110), ("RollScaler", 130))


# ===================================================================================================== the prefab
def _entries(controls) -> str:
    return "".join(f"  - InputEvent: {event}\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL_GUID[name]}, type: 2}}\n"
                   for name, _, event in sorted(controls, key=lambda c: c[2]))


def bindings() -> str:
    """LT crouch and RT bounce in the SHARED map; no device overrides (every device raises the two trigger events)."""
    return ("  _inputEventShipActions:\n" + _entries(CONTROLS) + "  _resourceEventClassActions: []\n"
            "  _touchActionOverrides: []\n  _gamepadActionOverrides: []\n")


def hud_controller_block() -> str:
    return (S.mb_header(HUD_CONTROLLER_ID, ROOT_GO, SCRIPT["FleaHUDController"], "Assembly-CSharp::CosmicShore.UI.FleaHUDController") +
            f"  baseView: {{fileID: {HUD_VIEW_ID}}}\n"
            "  _iconSetSwitcher: {fileID: 0}\n"
            f"  view: {{fileID: {HUD_VIEW_ID}}}\n"
            f"  flea: {{fileID: {TRANSFORMER_ID}}}\n"
            "  fullChain: 6\n")


def transformer(tr: str) -> str:
    """The Stoat's two-stick block as FleaVesselTransformer: the script, the config, the crawl and the page's turn rates."""
    tr = tr.replace(f"guid: {TWO_STICK_SCRIPT}, type: 3}}", f"guid: {SCRIPT['FleaVesselTransformer']}, type: 3}}", 1)
    tr = S.set_field(tr, "DefaultThrottleScaler", str(CRAWL))
    tr = S.set_field(tr, "DefaultMinimumSpeed", "0")
    for field, value in TURN_RATES:
        tr = S.set_field(tr, field, str(value))
    tr = re.sub(r"  config: .*\n", "", tr)
    return tr.rstrip("\n") + f"\n  config: {{fileID: 11400000, guid: {CONFIG}, type: 2}}\n"


def build_prefab(donor: str) -> str:
    text = donor
    text = S.replace_once(text, "  m_Name: Stoat\n", "  m_Name: Flea\n", "root GameObject name")
    text = S.replace_once(text, "  _name: Stoat\n", "  _name: Flea\n", "VesselStatus _name")
    text = S.replace_once(text, f"  vesselType: {STOAT_CLASS_ID}\n", f"  vesselType: {CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)

    head, docs = S.split_docs(text)
    # Executors: every Stoat one out, none in (the Flea's state is its transformer's).
    ship = S.get_doc(docs, SHIP_ACTIONS_GO)
    for fid in STOAT_EXECUTORS:
        ship = ship.replace(f"  - component: {{fileID: {fid}}}\n", "")
    S.set_doc(docs, SHIP_ACTIONS_GO, ship)
    reg = S.get_doc(docs, REGISTRY_ID)
    reg = re.sub(r"  _executors:\n(?:  - \{fileID: -?\d+\}\n)+", "  _executors: []\n", reg)
    S.set_doc(docs, REGISTRY_ID, reg)
    S.remove_docs(docs, STOAT_EXECUTORS)

    S.set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    S.set_doc(docs, HULL_GO_ID, S.get_doc(docs, HULL_GO_ID).replace("  m_Name: StoatHull\n", "  m_Name: FleaHull\n"))
    return patch_prefab(S.join_docs(head, docs))


def patch_prefab(text: str) -> str:
    """Idempotent edits the committed prefab takes too (the clone itself is a spent one-shot): the transformer, the
    bindings, the HUD controller and the vessel impactor's container are the generator's."""
    text = text.replace(f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {STOAT_IMPACT_CONTAINER}, type: 2}}",
                        f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {IMPACT_CONTAINER_GUID}, type: 2}}")
    text = re.sub(r"  _inputEventShipActions:(?:.*\n)*?(?=  _onButtonPressed:)", lambda _: bindings(), text, count=1)
    head, docs = S.split_docs(text)
    S.set_doc(docs, TRANSFORMER_ID, transformer(S.get_doc(docs, TRANSFORMER_ID)))
    S.set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    return S.join_docs(head, docs)


# ===================================================================================================== assets
def config_asset() -> str:
    """SEEDED once. Numbers = FleaConfigSO's field initializers = the ricochet kind's defaults."""
    return (S.so_header(SCRIPT["FleaConfigSO"], "FleaConfig") +
            "  crawl: 50\n"
            "  window: 0.18\n"
            "  perfectGain: 1.35\n"
            "  dullLoss: 0.85\n"
            "  topSpeed: 260\n"
            "  grip: 2.2\n"
            "  drag: 0.25\n"
            "  hopCharge: 0.6\n"
            "  hopMin: 60\n"
            "  hopMax: 160\n"
            "  samePrismGuard: 0.6\n")


CONFIG_KEYS = ("crawl", "window", "perfectGain", "dullLoss", "topSpeed", "grip", "drag", "hopCharge", "hopMin", "hopMax",
               "samePrismGuard")


def bounce_effect_asset() -> str:
    return S.so_header(SCRIPT["FleaBounceByPrismEffectSO"], "FleaBounceByPrismEffect")


def impact_container_asset() -> str:
    """SEEDED once from the Stoat's vessel container: the contact costs and the Stoat's omni effect out, the Flea's
    bounce in (fork, never share, before changing behaviour: vessel rule 8)."""
    text = open(STOAT_IMPACT_CONTAINER_PATH, encoding="utf-8", newline="").read()
    text = S.replace_once(text, "  m_Name: StoatImpactorDataContainer\n", "  m_Name: FleaImpactorDataContainer\n", "container name")
    for guid in (*CONTACT_COSTS, STOAT_OVERCLOCK):
        text = S.replace_once(text, f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n", "", f"the Stoat container's {guid}")
    return S.replace_once(text, "  vesselCrystalEffects:\n",
                          f"  - {{fileID: 11400000, guid: {BOUNCE_EFFECT_GUID}, type: 2}}\n  vesselCrystalEffects:\n", "prism list end")


def map_asset() -> str:
    """Four OPEN slots: the studio's element proposals (vessels/flea.json `elements`) are written down as proposals, not
    wired, because nobody signed them off (vessel skill §3). Input 0 and an empty UpgradeLabel are the honest state."""
    proposals = ((1, "Spring", "perfectGain", "x1 to x1.15", "Supercharge"),
                 (2, "Momentum", "topSpeed", "x1 to x1.2", "Shatter"),
                 (3, "Big Hop", "hopMax", "x1 to x1.5", "Leap"),
                 (4, "Wide Window", "window", "x1 to x1.7", "Slow Motion"))
    out = (S.so_header(MAP_SCRIPT, "Flea").replace("  m_EditorClassIdentifier: \n", "  m_EditorClassIdentifier:\n") +
           f"  vesselClass: {CLASS_ID}\n  entries:\n")
    for element, name, param, scale, l5 in proposals:
        out += (f"  - Element: {element}\n    AbilityLabel: (open design slot)\n"
                f"    AbilityDescription: '(open design slot) The Vessel Studio proposes \"{name}\": this element scales "
                f"FleaConfig.{param} {scale} at level 10 (FleaMath.AtLevels). Not wired and not signed off - FLEA.md section 5.'\n"
                "    Input: 0\n    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n    UpgradeLabel:\n"
                f"    UpgradeDescription: '(open design slot) The studio proposes \"{l5}\". Do not invent one without sign-off.'\n")
    return out


def control_asset(name: str, value: int) -> str:
    return S.so_header(SCRIPT["FleaActionSO"], name) + f"  control: {value}\n"


def class_asset() -> str:
    """The Stoat's card sprites stand in until the Flea has a model to render its own (FLEA.md §6)."""
    return (S.so_header(S.SO_VESSEL_SCRIPT, "SO_Class_Flea").replace("  m_EditorClassIdentifier: \n",
                                                                     "  m_EditorClassIdentifier: Assembly-CSharp::SO_Vessel\n") +
            f"  Class: {CLASS_ID}\n"
            "  Name: Flea\n"
            "  Description: The ricochet. It crawls on its own and gets its speed from bouncing off mass. Press RT just\n"
            "    as you hit a prism for a perfect bounce that banks speed and grows your chain; miss it and you dribble.\n"
            "    Hold LT to crouch, let go to hop.\n"
            "  PrimaryElement: 0\n"
            "  Element: {fileID: 0}\n"
            "  InitialResourceLevels: {Mass: 0, Charge: 0, Space: 0, Time: 0}\n"
            "  IconActive: {fileID: 21300000, guid: 627c06caaa88435fa2c44e98ad1fb611, type: 3}\n"
            "  IconInactive: {fileID: 21300000, guid: d845d4e49ee148f9b787599a3755eaa9, type: 3}\n"
            "  PreviewImage: {fileID: 21300000, guid: 7a2d25b9bec00f838022ba973ef1287d, type: 3}\n"
            "  SquadImage: {fileID: 21300000, guid: fab857e5d0454e7d44e392b62578d2ca, type: 3}\n"
            "  TrailPreviewImage: {fileID: 0}\n"
            "  CardSilohoutteActive: {fileID: 21300000, guid: 627c06caaa88435fa2c44e98ad1fb611, type: 3}\n"
            "  CardSilohoutteInactive: {fileID: 21300000, guid: 290641a0f0c8ae1a849fbc6943c93dae, type: 3}\n"
            "  Abilities: []\n"
            "  Games: []\n"
            "  TrainingGames: []\n"
            "  gameplayParameter1:\n    LeftHandLabel: Casual\n    RightHandLabel: Challenging\n    Value: 0.7\n"
            "  gameplayParameter2:\n    LeftHandLabel: Relaxing\n    RightHandLabel: Thrilling\n    Value: 0.8\n"
            "  gameplayParameter3:\n    LeftHandLabel: Solo\n    RightHandLabel: Social\n    Value: 0.4\n"
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


def owned_files() -> "dict[str, str]":
    out = {
        CLASS_ASSET: class_asset(), CLASS_ASSET + ".meta": S.asset_meta(CLASS_GUID),
        CONFIG_PATH + ".meta": S.asset_meta(CONFIG),
        ACTIONS_DIR + ".meta": folder_meta(ACTIONS_FOLDER_GUID),
        PREFAB + ".meta": S.prefab_meta().replace(S.PREFAB_GUID, PREFAB_GUID),
        DOC + ".meta": text_meta(DOC_GUID),
        BOUNCE_EFFECT: bounce_effect_asset(), BOUNCE_EFFECT + ".meta": S.asset_meta(BOUNCE_EFFECT_GUID),
        IMPACT_CONTAINER + ".meta": S.asset_meta(IMPACT_CONTAINER_GUID),
        MAP: map_asset(), MAP + ".meta": S.asset_meta(MAP_GUID),
    }
    for name, value, _ in CONTROLS:
        path = os.path.join(ACTIONS_DIR, name + ".asset")
        out[path] = control_asset(name, value)
        out[path + ".meta"] = S.asset_meta(CONTROL_GUID[name])
    for rel, guid in NEW_SCRIPTS:
        out[os.path.join(SCRIPTS, rel) + ".meta"] = S.script_meta(guid)
    return out


SEEDED = {CONFIG_PATH: (config_asset, SCRIPT["FleaConfigSO"], "FleaConfig"),
          IMPACT_CONTAINER: (impact_container_asset, IMPACT_CONTAINER_SCRIPT, "FleaImpactorDataContainer")}


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
    """Four entries copied from the Stoat's (the Flea wears the Stoat's hull)."""
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
        return ["Flea.prefab is missing"]
    head, docs = S.split_docs(prefab)
    ids = {k for k, _ in docs}
    d = dict(docs)

    def need(cond, msg):
        if not cond:
            errors.append(msg)

    need(f"  vesselType: {CLASS_ID}\n" in prefab, f"vesselType is not {CLASS_ID} (Flea)")
    need("  _name: Flea\n" in prefab and "  m_Name: Flea\n" in prefab, "the prefab is not named Flea")
    m = re.search(r"  GlobalObjectIdHash: (\d+)\n", d.get(NETWORK_OBJECT_ID, ""))
    need(m and int(m.group(1)) == S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "GlobalObjectIdHash is not this prefab's (a copied hash collides with the donor's, vessel rule 42)")
    for guid in STOAT_SCRIPTS:
        need(guid not in prefab, f"a Stoat-only script survived the clone: {guid}")
    for fid in STOAT_EXECUTORS:
        need(fid not in ids, f"Stoat executor {fid} still present")
    need("  _executors: []\n" in d.get(REGISTRY_ID, ""), "the ActionExecutorRegistry is not empty (the Flea has no executor)")
    tr = d.get(TRANSFORMER_ID, "")
    need(f"guid: {SCRIPT['FleaVesselTransformer']}" in tr, "the transformer is not FleaVesselTransformer")
    need(f"  config: {{fileID: 11400000, guid: {CONFIG}, type: 2}}\n" in tr, "FleaVesselTransformer.config is not FleaConfig")
    need("  vectorFlightModel: 1\n" in tr, "the transformer is off the vector model (the Flea's bounce writes momentum)")
    for field, value in TURN_RATES:
        need(f"  {field}: {value}\n" in tr, f"{field} is not the page's {value}")
    hud = d.get(HUD_CONTROLLER_ID, "")
    need(f"guid: {SCRIPT['FleaHUDController']}" in hud and f"  flea: {{fileID: {TRANSFORMER_ID}}}\n" in hud,
         "the HUD controller is not FleaHUDController wired to the transformer")
    need(f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {IMPACT_CONTAINER_GUID}, type: 2}}" in prefab
         and STOAT_IMPACT_CONTAINER not in prefab, "the vessel impactor does not read FleaImpactorDataContainer")
    need("  abilities: []\n" in d.get(AI_PILOT_ID, ""), "the AIPilot runs a blind ability cycle")
    handler = d.get(HANDLER_ID, "")
    for name, _, event in CONTROLS:
        need(re.search(r"  - InputEvent: %d\n    ShipActions:\n    - \{fileID: 11400000, guid: %s, type: 2\}" % (event, CONTROL_GUID[name]), handler),
             f"{name} is not bound to InputEvents {event}")
    need("  _touchActionOverrides: []\n  _gamepadActionOverrides: []\n" in handler,
         "a device override map is not empty (it would shadow the shared LT / RT on that device)")
    dangling = S.dangling_refs(prefab)
    need(not dangling, f"{len(dangling)} dangling local reference(s): {dangling[:5]}")

    container = files.get(IMPACT_CONTAINER, "")
    prisms = re.search(r"  vesselPrismEffects:\n((?:  - \{[^}]*\}\n)*)", container)
    need(prisms and f"guid: {BOUNCE_EFFECT_GUID}" in prisms.group(1), "FleaImpactorDataContainer does not run FleaBounceByPrismEffect")
    for guid, name in CONTACT_COSTS.items():
        need(guid not in container, f"FleaImpactorDataContainer still runs {name} (a bounce replaces the contact cost)")
    need(STOAT_OVERCLOCK not in container, "FleaImpactorDataContainer runs the Stoat's omni Overclock (it drives Stoat executors)")

    for path, want in owned_files().items():
        have = files.get(path)
        need(have == want, f"{os.path.relpath(path, ROOT)} is {'missing' if have is None else 'drifted'} - run the tool")
    cfg = files.get(CONFIG_PATH, "")
    for key in CONFIG_KEYS:
        need(re.search(r"^  %s:" % key, cfg, re.M), f"FleaConfig has no {key}")
    for path, (_, script, name) in SEEDED.items():
        have = files.get(path) or ""
        need(f"guid: {script}, type: 3}}" in have and f"  m_Name: {name}\n" in have, f"{os.path.relpath(path, ROOT)} missing or not a {name}")

    need(f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(CONTAINER, ""),
         "not registered in the Vessel Prefab Container (by its root TRANSFORM)")
    need(f"    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(NETWORK_PREFABS, ""),
         "not registered in DefaultNetworkPrefabs (by its root GAMEOBJECT)")
    for path in CLASS_LISTS:
        need(CLASS_GUID in files.get(path, ""), f"SO_Class_Flea is not in {os.path.basename(path)}")
    need(len(re.findall(r"^  - vessel: %d\n" % CLASS_ID, files.get(FUSION, ""), re.M)) == 4,
         "CrystalHullFusionConfig does not carry exactly four Flea entries")
    need(re.search(r"\bFlea = %d," % CLASS_ID, files.get(ENUM, "")), f"VesselClassType has no Flea = {CLASS_ID}")
    need("VesselClassType.Flea" in files.get(ROSTER, ""), "ToyVesselRoster.Default does not offer the Flea (vessel rule 40)")
    for rel, _ in NEW_SCRIPTS:
        need(os.path.join(SCRIPTS, rel) in files, f"{rel} is missing")
    need(DOC in files, "FLEA.md is missing")
    return errors


def read_all() -> "dict[str, str]":
    paths = {PREFAB, CONTAINER, NETWORK_PREFABS, FUSION, *CLASS_LISTS, *owned_files(), *SEEDED, ENUM, ROSTER, DOC,
             *(os.path.join(SCRIPTS, rel) for rel, _ in NEW_SCRIPTS)}
    out = {}
    for p in paths:
        if os.path.isfile(p):
            with open(p, encoding="utf-8", newline="") as fh:
                out[p] = fh.read()
        elif os.path.isdir(p):
            out[p] = ""
    return out


def write(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def author() -> None:
    files = read_all()
    if PREFAB not in files:
        write(PREFAB, build_prefab(open(DONOR, encoding="utf-8", newline="").read()))
        print("cloned Stoat.prefab -> Flea.prefab")
    else:
        print("Flea.prefab exists - the clone stands down (spent one-shot)")
        patched = patch_prefab(files[PREFAB])
        if patched != files[PREFAB]:
            write(PREFAB, patched)
            print("patched Flea.prefab (transformer, bindings, HUD controller, impactor container)")
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
        "the donor's hash": (PREFAB, lambda t: re.sub(r"  GlobalObjectIdHash: \d+", "  GlobalObjectIdHash: 1", t, count=1)),
        "a Stoat executor left behind": (PREFAB, lambda t: t.replace("  _executors: []\n", "  _executors:\n  - {fileID: 5137264980012345603}\n", 1)),
        "the Stoat's transformer back": (PREFAB, lambda t: t.replace(SCRIPT["FleaVesselTransformer"], TWO_STICK_SCRIPT)),
        "the config unwired": (PREFAB, lambda t: t.replace(f"  config: {{fileID: 11400000, guid: {CONFIG}, type: 2}}", "  config: {fileID: 0}")),
        "the Stoat's turn rate": (PREFAB, lambda t: t.replace("  PitchScaler: 110\n", "  PitchScaler: 48\n")),
        "a dangling reference": (PREFAB, lambda t: t.replace(f"  flea: {{fileID: {TRANSFORMER_ID}}}", "  flea: {fileID: 12345}")),
        "crouch unbound": (PREFAB, lambda t: t.replace(CONTROL_GUID["FleaCrouch"], "f" * 32)),
        "bounce on the wrong event": (PREFAB, lambda t: t.replace("  - InputEvent: 1\n", "  - InputEvent: 6\n", 1)),
        "a pad override shadowing RT": (PREFAB, lambda t: t.replace("  _gamepadActionOverrides: []\n", "  _gamepadActionOverrides:\n  - InputEvent: 1\n    ShipActions: []\n", 1)),
        "the Stoat's impactor container back": (PREFAB, lambda t: t.replace(IMPACT_CONTAINER_GUID, STOAT_IMPACT_CONTAINER)),
        "the bounce off the container": (IMPACT_CONTAINER, lambda t: t.replace(BOUNCE_EFFECT_GUID, "0" * 32)),
        "the contact damage back": (IMPACT_CONTAINER, lambda t: t.replace("  vesselCrystalEffects:\n", "  - {fileID: 11400000, guid: 4da62d4eefe03594ba35027dd537aff0, type: 2}\n  vesselCrystalEffects:\n", 1)),
        "unregistered from the container": (CONTAINER, lambda t: t.replace(PREFAB_GUID, "0" * 32)),
        "unregistered from the network list": (NETWORK_PREFABS, lambda t: t.replace(PREFAB_GUID, "0" * 32)),
        "fusion entries missing": (FUSION, lambda t: t.replace(f"  - vessel: {CLASS_ID}\n", "  - vessel: 99\n")),
        "roster entry missing": (ROSTER, lambda t: t.replace("VesselClassType.Flea", "")),
        "enum member renumbered": (ENUM, lambda t: t.replace(f"Flea = {CLASS_ID},", "Flea = 25,")),
        "a map slot invented": (MAP, lambda t: t.replace("Input: 0", "Input: 1", 1)),
        "a config key gone": (CONFIG_PATH, lambda t: re.sub(r"  window: .*\n", "", t)),
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
    print(f"author_flea_assets --self-test: {len(mutations) - failed}/{len(mutations)} negative controls fired")
    return 1 if failed else 0


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errors = check(read_all())
    for e in errors:
        print("FAIL:", e)
    print("author_flea_assets: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
