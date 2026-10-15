#!/usr/bin/env python3
"""The Shrike's VESSEL CREATION TOOL: its prefab, assets and every registration, then prove the shipped set.

    python3 Tools/Build/author_shrike_assets.py             # author what is missing (idempotent), then check
    python3 Tools/Build/author_shrike_assets.py --check     # validate the SHIPPED files, write nothing
    python3 Tools/Build/author_shrike_assets.py --self-test # prove every check fires (negative controls)

Design: Assets/_Scripts/Controller/Vessel/R_VesselActions/SHRIKE.md. Source: the Shrike Studio
(Docs/Studios/VesselStudio/shrike.html, published as its own artifact), cloned by /artifact-to-unity; its numbers reach
the assets through Tools/Build/studio_to_unity.py and Tools/Build/studio_to_unity/shrike.json, never by hand.

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/Shrike.prefab - REBUILT from BrittleStar.prefab (the fleet's newest procedural one-thumb
    hull: its own colliders, an authored HUD with a gauge per card, the executor + registry pattern). The Shrike that
    shipped before this port was a flat Manta copy with no executor, no HUD, no camera and no impactors, unflyable in
    freestyle; it is replaced IN PLACE: the prefab guid (ae83e6dd...) and the Stoat-lineage root fileIDs are the same,
    so DefaultNetworkPrefabs' existing entry stays valid.
        - root GameObject / VesselStatus renamed, `vesselType: 10` (VesselClassType.Shrike);
        - a FRESH Netcode GlobalObjectIdHash for this prefab guid (author_stoat_assets.global_object_id_hash);
        - the flight model: the donor's one-thumb block on the SAME fileID, its script back to the fleet's
          SingleStickVesselTransformer, at the studio's design cruise (MinimumSpeed 0 + ThrottleScaler = `cruise`) and
          turn rates;
        - the trail: the spawner swapped (same fileID) to ShrikePrismController laying thin long blocks (Mass shortens
          the wavelength; the sling's thick blocks are the executor's);
        - the hull: BrittleStarHull becomes ShrikeHull (same fileIDs, so the paint and the renderer stay wired), its
          builder ShrikeHullBuilder; the hit box re-sized to the long hull;
        - the animation: ShrikeAnimation on the same fileID;
        - the executors: the Brittle Star's executor and its turrets object (gun, factory, round pool) removed;
          ShrikeExecutor added (config, hull, turn-end channel);
        - the bindings (shared): RT Void Lance, LT Targeting Array, A Nest / Dock, B Void Sling;
        - the AI: no blind ability cycle, the no-op HoldBoostAIPolicy (no boost to drive); the executor's autopilot presses
          the replicated controls;
        - the HUD: ShrikeHUDVariant (a copy of BrittleStarHUDVariant: same internal fileIDs, the view swapped to
          ShrikeHUDView, the four icons and gauges renamed) and ShrikeHUDController;
        - the camera, the impactor containers and the skimmer's container: the Shrike's own.
    Assets: ShrikeConfig + the four control assets (VesselActions/Shrike/), the crystal effect, the two containers, the
    camera, SO_Class_Shrike, the elemental map, every new script's .meta, the doc's .meta.
    Registrations: Vessel Prefab Container (root TRANSFORM), DefaultNetworkPrefabs (root GAMEOBJECT; already there),
    SO_Classlist_All / _Classes, and the four CrystalHullFusionConfig entries for vessel 10 made bake-free (the bakes
    were of the Manta-copy mesh; the hull is now generated at runtime).
    NOT here (code, held by tests): the enum member, ToyVesselRoster.Default, the one-thumb roster.

THE REBUILD IS A SPENT ONE-SHOT (CLAUDE.md: "a spent one-shot must stand down, not abort"). It runs only while
Shrike.prefab does not yet carry ShrikeExecutor; once the rebuilt prefab is committed the clone stands down and --check
validates the SHIPPED file's invariants. The HUD variant is built only while absent.

TUNING IS SEEDED, NOT OWNED: ShrikeConfig.asset and the camera asset are written once if absent and then only checked
for their script and name (studio_to_unity.py writes the mapped numbers). Everything else is owned outright.
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
DONOR = os.path.join(VESSELS, "BrittleStar.prefab")
PREFAB = os.path.join(VESSELS, "Shrike.prefab")
HUD_DIR = A("_Prefabs", "UI Elements", "VesselHUD")
HUD_DONOR = os.path.join(HUD_DIR, "BrittleStarHUDVariant.prefab")
HUD_VARIANT_PATH = os.path.join(HUD_DIR, "ShrikeHUDVariant.prefab")
ACTIONS_DIR = A("_SO_Assets", "VesselActions", "Shrike")
CONFIG_PATH = os.path.join(ACTIONS_DIR, "ShrikeConfig.asset")
CONTAINER = A("_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = A("DefaultNetworkPrefabs.asset")
CLASSES = A("_SO_Assets", "Classes")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Shrike.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAP = A("Resources", "ElementalAbilityMaps", "Shrike.asset")
FUSION = A("Resources", "CrystalHullFusionConfig.asset")
CAMERA = A("_SO_Assets", "Camera", "ShrikeCameraSettingsSO.asset")
VESSEL_CONTAINER = A("_SO_Assets", "Effects", "Effect Containers", "VesselContainers", "ShrikeImpactorDataContainer.asset")
SKIMMER_CONTAINER = A("_SO_Assets", "Effects", "Effect Containers", "SkimmerContainers", "ShrikeSkimmerImpactorDataContainer.asset")
CRYSTAL_EFFECT = A("_SO_Assets", "Effects", "Vessel Crystal Effects", "ShrikeCrystalEffect.asset")
SCRIPTS = A("_Scripts")
DOC = os.path.join(SCRIPTS, "Controller", "Vessel", "R_VesselActions", "SHRIKE.md")
CARD_ACTIVE_META = A("_Graphics", "CardImages", "Shrike.png.meta")
CARD_INACTIVE_META = A("_Graphics", "CardImages", "Shrike_Inactive.png.meta")

CLASS_ID = 10
DONOR_CLASS_ID = 16


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/Shrike/{name}".encode()).hexdigest()


PREFAB_GUID = "ae83e6dd3b718654db0175585a46edb2"   # the Shrike's own, from before the port: kept
HUD_VARIANT = guid_for("ShrikeHUDVariant.prefab")
CONFIG = guid_for("ShrikeConfig.asset")
CLASS_GUID = guid_for("SO_Class_Shrike.asset")
MAP_GUID = guid_for("ElementalAbilityMaps/Shrike.asset")
CAMERA_GUID = guid_for("ShrikeCameraSettingsSO.asset")
VESSEL_CONTAINER_GUID = guid_for("ShrikeImpactorDataContainer.asset")
SKIMMER_CONTAINER_GUID = guid_for("ShrikeSkimmerImpactorDataContainer.asset")
CRYSTAL_EFFECT_GUID = guid_for("ShrikeCrystalEffect.asset")
ACTIONS_FOLDER_GUID = guid_for("VesselActions/Shrike")
DOC_GUID = guid_for("SHRIKE.md")
CARD_ACTIVE = hashlib.md5(b"CosmicShore/Shrike/CardImages/Shrike.png").hexdigest()            # render_shrike_card_icon.py
CARD_INACTIVE = hashlib.md5(b"CosmicShore/Shrike/CardImages/Shrike_Inactive.png").hexdigest()

# (control name, enum value, InputEvents it is bound to). The InputEvents numbers: RightStickAction=1 (RT / LMB /
# Right Shift), LeftStickAction=2 (LT / RMB / Left Shift), Button1=6 (A / Space), Button2=7 (B / R).
CONTROLS = (("ShrikeLance", 0, 1), ("ShrikeSling", 1, 7), ("ShrikePaint", 2, 2), ("ShrikeDock", 3, 6))
CONTROL_GUID = {name: guid_for(name + ".asset") for name, _, _ in CONTROLS}

# Every script this vessel added: (path under Assets/_Scripts, guid).
NEW_SCRIPTS = tuple((rel, guid_for(os.path.basename(rel))) for rel in (
    "Controller/Vessel/ShrikeRules.cs",
    "Controller/Vessel/ShrikeHullForm.cs",
    "Controller/Vessel/ShrikeHullBuilder.cs",
    "Controller/Vessel/ShrikePrismController.cs",
    "Controller/Animation/ShrikeAnimation.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/ShrikeActionSO.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/ShrikeConfigSO.cs",
    "Controller/Vessel/R_VesselActions/Executors/ShrikeExecutor.cs",
    "Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/ShrikeCrystalEffectSO.cs",
    "UI/Controller/ShrikeHUDController.cs",
    "UI/View/ShrikeHUDView.cs",
    "Tests/Editor/ShrikeTests.cs",
    "Tests/Editor/ShrikeConfigTests.cs",
))
SCRIPT = {os.path.basename(rel)[:-3]: guid for rel, guid in NEW_SCRIPTS}

# ---- the donor's documents (BrittleStar.prefab, the Stoat lineage), by fileID ---------------------------------------
ROOT_GO = "6417075533431866457"
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "1733035045152327888"
TRANSFORMER_ID = "7686683633640807837"
PRISM_CONTROLLER_ID = "874579555101021522"
AI_PILOT_ID = "3440187540988767646"
ANIMATION_ID = "5500781737155866732"
HANDLER_ID = "9172589084772723142"
CAMERA_CUSTOMIZER_ID = "6989089190155014109"
VESSEL_IMPACTOR_ID = "2643412773408452782"
HUD_CONTROLLER_ID = "5649393235309590662"
SHIP_ACTIONS_GO = "7850473780452578400"
REGISTRY_ID = "9105512684531825410"
HULL_GO_ID = "5137264980012345610"
HULL_BUILDER_ID = "5137264980012345614"
BODY_GO_ID, BODY_COLLIDER_ID = "5137264980012345630", "5137264980012345632"
TRIGGER_GO_ID, TRIGGER_COLLIDER_ID = "5137264980012345634", "5137264980012345636"
HUD_INSTANCE_ID, HUD_VIEW_ID = "5137264980012345620", "5137264980012345622"
SKIMMER_INSTANCE_ID = "7079694710114171170"
TAIL_INSTANCES = (("5313661885591395716", 1), ("7047167775550959062", -1))
DONOR_EXECUTOR_ID = "4419001122334455001"
DONOR_TURRETS = tuple(f"441900112233445501{i}" for i in range(5))   # GO, Transform, Gun, Factory, Pool

# ---- new documents -------------------------------------------------------------------------------------------------
EXECUTOR_ID = "5521001122334455001"

# ---- fleet guids ---------------------------------------------------------------------------------------------------
SINGLE_STICK_SCRIPT = "b6b54993a316e0041b4bf5981190ac67"
COMBAT_HIT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"
TURN_END_CHANNEL = "498a06d44bde9184f985c938c803b2a1"
SO_VESSEL_SCRIPT = "0309a5343d820c14abd73b35f6fa50f1"
MAP_SCRIPT = "4b3f2a8c8f344355b16a4d4296ae3f98"
CAMERA_SCRIPT = "fd4e6f3597f7439ba7333b35a3a9e164"
VESSEL_CONTAINER_SCRIPT = "fde7d8c2dacf45f99e46799ffeea15e6"
SKIMMER_CONTAINER_SCRIPT = "68b3e74284f14e32a6e21b5fb8c3e8ad"
# The Sparrow's HoldBoostAIPolicy: the fleet wires a boost policy on every AIPilot (check_vessel_shared_channels.py), and
# this one makes no driver on a hull with no BoostActionSO bound - the Shrike has no boost, so it is a deliberate no-op.
NO_BOOST_POLICY = "193cc98fcde34143a71e79c5ccdaa1f4"


def bs_guid(name: str) -> str:
    return hashlib.md5(f"CosmicShore/BrittleStar/{name}".encode()).hexdigest()


BRITTLESTAR = {  # what the Brittle Star's tool minted (author_brittlestar_assets.guid_for): none may survive the rebuild
    name: bs_guid(name) for name in (
        "BrittleStarExecutor.cs", "BrittleStarHullBuilder.cs", "BrittleStarAnimation.cs", "BrittleStarHUDController.cs",
        "BrittleStarHUDView.cs", "BrittleStarVesselTransformer.cs", "BrittleStarPrismController.cs",
        "BrittleStarHUDVariant.prefab", "BrittleStarConfig.asset", "BrittleStarCameraSettingsSO.asset",
        "BrittleStarImpactorDataContainer.asset", "BrittleStarSkimmerImpactorDataContainer.asset",
        "BrittleStarAIBoostPolicy.asset")}
ICON_SWAP = {}   # BrittleStar icon sprite guid -> the Shrike's (author_shrike_icons.py), by element
for old, new in (("BrittleStar_RingDetonation.png", "Shrike_VoidLance.png"), ("BrittleStar_CreationMode.png", "Shrike_Nest.png"),
                 ("BrittleStar_DestructionMode.png", "Shrike_TargetingArray.png"), ("BrittleStar_SpeedMode.png", "Shrike_VoidSling.png")):
    ICON_SWAP[hashlib.md5(f"CosmicShore/BrittleStarIcons/{old}".encode()).hexdigest()] = \
        hashlib.md5(f"CosmicShore/ShrikeIcons/{new}".encode()).hexdigest()

# Flight (the studio's design ship: cruise 18 = MinimumSpeed 0 + ThrottleScaler 18; studio_to_unity owns the numbers).
FLIGHT = {"MinimumSpeed": "0", "DefaultMinimumSpeed": "0", "ThrottleScaler": "18", "DefaultThrottleScaler": "18",
          "PitchScaler": "60", "YawScaler": "40", "RollScaler": "15", "RotationThrottleScaler": "0"}
# The trail: thin long blocks (the spawner halves BaseScale.x for a centred lane), the studio's trailThin x trailLength,
# a wavelength of trailWavelength at Mass 0, laid behind the hull's contact radius.
TRAIL = {"BaseScale": "{x: 1.2, y: 0.6, z: 7}", "initialWavelength": "8", "minWavelength": "2", "offset": "7.5"}
# Dimensions (world units): the long hull with its wings, the barrel under the nose.
BODY_BOX = ((0, -0.2, 0.5), (2.6, 1.6, 9.0))
TRIGGER_BOX = ((0, -0.2, 0.5), (3.4, 2.2, 10.0))
TAIL_X, TAIL_Z = 4.4, -1.0


# ===================================================================================================== the prefab
def mb(fid, go, script, ident=""):
    return S.mb_header(fid, go, script, ident)


def executor_block() -> str:
    return (mb(EXECUTOR_ID, SHIP_ACTIONS_GO, SCRIPT["ShrikeExecutor"]) +
            f"  config: {{fileID: 11400000, guid: {CONFIG}, type: 2}}\n"
            f"  hull: {{fileID: {HULL_BUILDER_ID}}}\n"
            f"  OnMiniGameTurnEnd: {{fileID: 11400000, guid: {TURN_END_CHANNEL}, type: 2}}\n")


def bindings() -> str:
    out = "  _inputEventShipActions:\n"
    for name, _, event in sorted(CONTROLS, key=lambda c: c[2]):
        out += f"  - InputEvent: {event}\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL_GUID[name]}, type: 2}}\n"
    return out + "  _resourceEventClassActions: []\n  _touchActionOverrides: []\n  _gamepadActionOverrides: []\n"


def animation_block() -> str:
    return (mb(ANIMATION_ID, ROOT_GO, SCRIPT["ShrikeAnimation"]) +
            "  SkinnedMeshRenderer: {fileID: 0}\n"
            "  SaveNewPositions: 1\n"
            "  brakeThreshold: 0.65\n"
            "  lerpAmount: 2\n"
            "  smallLerpAmount: 0.7\n"
            f"  executor: {{fileID: {EXECUTOR_ID}}}\n")


def hud_controller_block() -> str:
    return (mb(HUD_CONTROLLER_ID, ROOT_GO, SCRIPT["ShrikeHUDController"], "Assembly-CSharp::CosmicShore.UI.ShrikeHUDController") +
            f"  baseView: {{fileID: {HUD_VIEW_ID}}}\n"
            "  _iconSetSwitcher: {fileID: 0}\n"
            f"  view: {{fileID: {HUD_VIEW_ID}}}\n"
            f"  shrike: {{fileID: {EXECUTOR_ID}}}\n")


def box(chunk: str, centre, size) -> str:
    chunk = re.sub(r"  m_Size: \{[^}]*\}\n", f"  m_Size: {S.v3(*size)}\n", chunk)
    return re.sub(r"  m_Center: \{[^}]*\}\n", f"  m_Center: {S.v3(*centre)}\n", chunk)


def set_mod(chunk: str, path: str, value: str) -> str:
    pat = re.compile(r"(      propertyPath: %s\n      value: )[^\n]*\n" % re.escape(path))
    if not pat.search(chunk):
        raise RuntimeError(f"BrittleStar.prefab drifted: no modification {path}")
    return pat.sub(lambda m: m.group(1) + value + "\n", chunk, count=1)


def build_prefab(donor: str) -> str:
    text = donor
    text = S.replace_once(text, "  m_Name: BrittleStar\n", "  m_Name: Shrike\n", "root GameObject name")
    text = S.replace_once(text, "  _name: BrittleStar\n", "  _name: Shrike\n", "VesselStatus _name")
    text = S.replace_once(text, f"  vesselType: {DONOR_CLASS_ID}\n", f"  vesselType: {CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)

    head, docs = S.split_docs(text)

    # The turrets object goes (the Shrike's missiles are its executor's).
    root = S.get_doc(docs, ROOT_TRANSFORM_ID)
    root = S.replace_once(root, f"  - {{fileID: {DONOR_TURRETS[1]}}}\n", "", "turrets child of the root")
    S.set_doc(docs, ROOT_TRANSFORM_ID, root)
    S.remove_docs(docs, DONOR_TURRETS)

    # Executors: the Brittle Star's out, the Shrike's in.
    ship = S.get_doc(docs, SHIP_ACTIONS_GO)
    ship = S.replace_once(ship, f"  - component: {{fileID: {DONOR_EXECUTOR_ID}}}\n", f"  - component: {{fileID: {EXECUTOR_ID}}}\n",
                          "Brittle Star executor component")
    S.set_doc(docs, SHIP_ACTIONS_GO, ship)
    reg = S.get_doc(docs, REGISTRY_ID)
    reg = re.sub(r"  _executors:\n(?:  - \{fileID: -?\d+\}\n)+", f"  _executors:\n  - {{fileID: {EXECUTOR_ID}}}\n", reg)
    S.set_doc(docs, REGISTRY_ID, reg)
    i = S.doc_index(docs, DONOR_EXECUTOR_ID)
    docs[i] = [EXECUTOR_ID, executor_block()]

    # Bindings.
    handler = S.get_doc(docs, HANDLER_ID)
    handler = re.sub(r"  _inputEventShipActions:(?:.*\n)*?(?=  _onButtonPressed:)", bindings(), handler, count=1)
    S.set_doc(docs, HANDLER_ID, handler)

    # Flight model: the one-thumb block on the same fileID, back to the fleet's SingleStickVesselTransformer.
    tr = S.get_doc(docs, TRANSFORMER_ID)
    tr = tr.replace(f"guid: {BRITTLESTAR['BrittleStarVesselTransformer.cs']}, type: 3}}", f"guid: {SINGLE_STICK_SCRIPT}, type: 3}}", 1)
    for key, value in FLIGHT.items():
        tr = S.set_field(tr, key, value)
    S.set_doc(docs, TRANSFORMER_ID, tr)

    # Trail: thin long blocks.
    pc = S.get_doc(docs, PRISM_CONTROLLER_ID)
    pc = pc.replace(f"guid: {BRITTLESTAR['BrittleStarPrismController.cs']}, type: 3}}", f"guid: {SCRIPT['ShrikePrismController']}, type: 3}}", 1)
    for key, value in TRAIL.items():
        pc = S.set_field(pc, key, value)
    S.set_doc(docs, PRISM_CONTROLLER_ID, pc)

    # Animation, HUD controller, AI (the no-op boost policy), camera, impactor.
    S.set_doc(docs, ANIMATION_ID, animation_block())
    S.set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    ai = S.get_doc(docs, AI_PILOT_ID)
    ai = S.set_field(ai, "boostPolicy", f"{{fileID: 11400000, guid: {NO_BOOST_POLICY}, type: 2}}")
    S.set_doc(docs, AI_PILOT_ID, ai)
    cam = S.get_doc(docs, CAMERA_CUSTOMIZER_ID)
    cam = re.sub(r"  settings: \{[^}]*\}\n", f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}\n", cam)
    S.set_doc(docs, CAMERA_CUSTOMIZER_ID, cam)
    imp = S.get_doc(docs, VESSEL_IMPACTOR_ID)
    imp = re.sub(r"  vesselImpactorDataContainerSO: \{[^}]*\}\n",
                 f"  vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {VESSEL_CONTAINER_GUID}, type: 2}}\n", imp)
    S.set_doc(docs, VESSEL_IMPACTOR_ID, imp)

    # Hull: same fileIDs, the Shrike's builder (its proportions are the C# field initializers: not serialized).
    S.set_doc(docs, HULL_GO_ID, S.get_doc(docs, HULL_GO_ID).replace("  m_Name: BrittleStarHull\n", "  m_Name: ShrikeHull\n"))
    S.set_doc(docs, HULL_BUILDER_ID, mb(HULL_BUILDER_ID, HULL_GO_ID, SCRIPT["ShrikeHullBuilder"]))

    # Hit box: the long hull's own.
    S.set_doc(docs, BODY_GO_ID, S.get_doc(docs, BODY_GO_ID).replace("  m_Name: BrittleStarBody\n", "  m_Name: ShrikeBody\n"))
    S.set_doc(docs, TRIGGER_GO_ID, S.get_doc(docs, TRIGGER_GO_ID).replace("  m_Name: BrittleStarHitbox\n", "  m_Name: ShrikeHitbox\n"))
    S.set_doc(docs, BODY_COLLIDER_ID, box(S.get_doc(docs, BODY_COLLIDER_ID), *BODY_BOX))
    S.set_doc(docs, TRIGGER_COLLIDER_ID, box(S.get_doc(docs, TRIGGER_COLLIDER_ID), *TRIGGER_BOX))

    # The wingtip tails: off the wing tips.
    for inst, side in TAIL_INSTANCES:
        chunk = S.get_doc(docs, inst)
        chunk = set_mod(chunk, "m_LocalPosition.x", S.num(side * TAIL_X))
        chunk = set_mod(chunk, "m_LocalPosition.z", S.num(TAIL_Z))
        S.set_doc(docs, inst, chunk)

    # Skimmer: the Shrike's container.
    sk = S.get_doc(docs, SKIMMER_INSTANCE_ID)
    sk = sk.replace(BRITTLESTAR["BrittleStarSkimmerImpactorDataContainer.asset"], SKIMMER_CONTAINER_GUID)
    S.set_doc(docs, SKIMMER_INSTANCE_ID, sk)

    # HUD: the Shrike's variant, same internal fileIDs.
    for fid, chunk in list(docs):
        if f"m_PrefabInstance: {{fileID: {HUD_INSTANCE_ID}}}" in chunk or fid == HUD_INSTANCE_ID:
            chunk = chunk.replace(BRITTLESTAR["BrittleStarHUDVariant.prefab"], HUD_VARIANT)
            chunk = chunk.replace("      value: BrittleStarHUDVariant\n", "      value: ShrikeHUDVariant\n")
            chunk = chunk.replace(f"guid: {BRITTLESTAR['BrittleStarHUDView.cs']}, type: 3}}", f"guid: {SCRIPT['ShrikeHUDView']}, type: 3}}")
            chunk = chunk.replace("CosmicShore.UI.BrittleStarHUDView", "CosmicShore.UI.ShrikeHUDView")
            S.set_doc(docs, fid, chunk)

    return S.join_docs(head, docs)


# ===================================================================================================== the HUD variant
def build_hud_variant(donor: str) -> str:
    text = S.replace_once(donor, "      value: BrittleStarHUDVariant\n", "      value: ShrikeHUDVariant\n", "variant name")
    text = S.replace_once(text, f"guid: {BRITTLESTAR['BrittleStarHUDView.cs']}, type: 3}}", f"guid: {SCRIPT['ShrikeHUDView']}, type: 3}}", "view script")
    text = text.replace("CosmicShore.UI.BrittleStarHUDView", "CosmicShore.UI.ShrikeHUDView")
    for old, new in (("ringGauge", "lanceGauge"), ("creationGauge", "nestGauge"),
                     ("destructionGauge", "arrayGauge"), ("speedGauge", "slingGauge")):
        text = S.replace_once(text, f"  {old}: {{fileID:", f"  {new}: {{fileID:", f"view field {old}")
    for old, new in (("RingGauge", "LanceGauge"), ("CreationGauge", "NestGauge"),
                     ("DestructionGauge", "ArrayGauge"), ("SpeedGauge", "SlingGauge")):
        text = S.replace_once(text, f"  m_Name: {old}\n", f"  m_Name: {new}\n", f"gauge object {old}")
    for old, new in ICON_SWAP.items():
        text = S.replace_once(text, f"guid: {old}, type: 3}}", f"guid: {new}, type: 3}}", f"icon sprite {old}")
    return text


# ===================================================================================================== assets
def header(script: str, name: str) -> str:
    return S.so_header(script, name)


def ef(value, lo, hi, element, floor) -> str:
    return S.elemental(value, lo, hi, element, floor)


def config_asset() -> str:
    """SEEDED once. Numbers = ShrikeConfigSO's field initializers = the studio's SHIPPED block."""
    return (header(SCRIPT["ShrikeConfigSO"], "ShrikeConfig") +
            "  hullRadius: 3\n"
            "  dockBonus: 1.6\n"
            "  dockBonusShielded: 2.5\n"
            "  dockQueryReach: 12\n"
            "  dockGraceSeconds: 0.5\n"
            "  undockPush: 8\n"
            "  ownTrailGraceSeconds: 1\n"
            "  lanceRange: 2400\n"
            "  lanceCooldown: 1.2\n"
            "  lanceRadius:\n" + ef(1.5, 1.5, 5, 1, 0.5) +
            "  lanceDebrisSpeed: 60\n"
            "  lanceBeamSeconds: 0.3\n"
            "  slingRange: 1600\n"
            "  tetherSpeed: 2400\n"
            "  slingSeconds: 1\n"
            "  slingCooldown:\n" + ef(6, 6, 2.5, 4, 1) +
            "  slingTurnRate: 6\n"
            "  lockRange: 1500\n"
            "  lockCone:\n" + ef(5, 5, 14, 3, 2) +
            "  missileSpeed: 220\n"
            "  missileTurn: 140\n"
            "  missileBlast: 18\n"
            "  missileEvery: 0.8\n"
            "  missileLife: 8\n"
            "  lockKeepScale: 1.6\n"
            "  massLockRadius: 35\n"
            "  nestBlocks:\n" + ef(12, 12, 32, 2, 6) +
            "  nestCooldown: 8\n"
            "  nestHealth: 4\n"
            "  nestHealthMax: 12\n"
            "  turretRegen: 6\n"
            "  crystalHealth: 3\n"
            "  maxNests: 3\n"
            "  trailWavelength:\n" + ef(8, 8, 4, 2, 2) +
            "  trailThin: 0.6\n"
            "  trailThick: 2.6\n"
            "  trailLength: 7\n"
            "  slingMaxBlocksPerFrame: 6\n"
            "  lanceHitClass: 0\n"
            "  missileHitClass: 3\n"
            "  hitCooldown: 0.05\n"
            f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_CHANNEL}, type: 2}}\n"
            "  aiLanceMinTargets: 2\n"
            "  aiDockSeconds: 3\n"
            "  aiNestEvery: 25\n"
            "  aiSlingMinDistance: 300\n"
            "  aiSlingDegrees: 20\n" +
            "".join(S.empty_event(n) for n in ("lanceEvent", "tetherEvent", "slingEvent", "dockEvent", "undockEvent",
                                               "nestEvent", "lockEvent", "missileLaunchEvent", "missileBlastEvent",
                                               "crystalEvent")))


def control_asset(name: str, value: int) -> str:
    return header(SCRIPT["ShrikeActionSO"], name) + f"  control: {value}\n"


def crystal_effect_asset() -> str:
    return header(SCRIPT["ShrikeCrystalEffectSO"], "ShrikeCrystalEffect")


def vessel_container_asset() -> str:
    """Prism contact DOCKS the Shrike (its executor), so of the fleet's prism effects only the haptics and the danger
    debuff (terrain punishment, friendly fire by design) stay: no ram damage, no slow, no boost reset. Every crystal kind
    runs the haptics plus the Shrike's crystal (the document: 'a crystal', any crystal)."""
    g = lambda guid: f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
    crystal = g(CRYSTAL_EFFECT_GUID)
    return (header(VESSEL_CONTAINER_SCRIPT, "ShrikeImpactorDataContainer") +
            "  vesselPrismEffects:\n" + g("c7ccaca885824b24b716b12148d77ce1") + g("ca2d1be3880b82941a0465a1f7e45280") +
            "  vesselCrystalEffects:\n" + g("8de915fb00503344ca89a92a0a127b00") + crystal +
            "  vesselMassCrystalEffects:\n" + crystal +
            "  vesselChargeCrystalEffects:\n" + crystal +
            "  vesselSpaceCrystalEffects:\n" + crystal +
            "  vesselTimeCrystalEffects:\n" + crystal +
            "  vesselSkimmerEffects: []\n  vesselLifeformCrystalEffects: []\n"
            "  omniCrystalRetirement: {fileID: 0}\n")


def skimmer_container_asset() -> str:
    """The joust hit and the skim explosion, the skim haptics and the forcefield crackle (the fleet's, no gates)."""
    g = lambda guid: f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
    return (header(SKIMMER_CONTAINER_SCRIPT, "ShrikeSkimmerImpactorDataContainer") +
            "  vesselSkimmerEffectsSO:\n" + g("c2570a8d81db3ab4eb0ba30f184e081f") + g("7e54b5536d0d1fbcda92e10ddf159bf6") +
            "  skimmerPrismEffectsSO:\n" + g("dcce08b9351b97c4eb275508001a5b8e") + g("5bea3c7d44b956064c78606bba3077b8") +
            "  skimmerCrystalEffectsSO: []\n")


def camera_asset() -> str:
    """SEEDED once. The studio's chase: 30 u behind with a 6 u lift (shrike.html CHASE), hard-attached; the fleet's
    12000 far plane STATED (vessel contract 1.8) - the lance reaches 2400 u."""
    return (header(CAMERA_SCRIPT, "ShrikeCameraSettingsSO") +
            "  mode: 0\n"
            "  followOffset: {x: 0, y: 6, z: -30}\n"
            "  lookAheadDistance: 0\n"
            "  lookAheadLift: 0\n"
            "  chaseEaseRate: 0\n"
            "  framingFieldOfView: 0\n"
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
    return (header(SO_VESSEL_SCRIPT, "SO_Class_Shrike").replace("  m_EditorClassIdentifier: \n",
                                                                "  m_EditorClassIdentifier: Assembly-CSharp::SO_Vessel\n") +
            f"  Class: {CLASS_ID}\n"
            "  Name: Shrike\n"
            "  Description: A slow sniper that docks to whatever it touches. Build nests of super-shielded blocks,\n"
            "    pierce anything with the Void Lance, sling across the cell on a tether, and paint targets for the\n"
            "    missiles your nests launch.\n"
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
            "  gameplayParameter1:\n    LeftHandLabel: Casual\n    RightHandLabel: Challenging\n    Value: 0.5\n"
            "  gameplayParameter2:\n    LeftHandLabel: Relaxing\n    RightHandLabel: Thrilling\n    Value: 0.5\n"
            "  gameplayParameter3:\n    LeftHandLabel: Solo\n    RightHandLabel: Social\n    Value: 0.5\n"
            "  isLocked: 0\n"
            "  ownedFromStart: 0\n"
            "  UnlockCost: 100\n")


MAP_ENTRIES = (
    (1, "Void Lance", 1,
     "RT (held). A hitscan beam 2400 u long that breaks every block it passes - super-shielded included, any domain - "
     "except the nest you are docked in; a rival hull on the line is a scored petal hit (petals only - nothing is "
     "moved). 1.2 s cooldown, faster docked. Charge is reach - one dial: the beam's radius 1.5 -> 5 u at level 10 "
     "(ShrikeConfigSO.lanceRadius)."),
    (2, "Nest", 6,
     "A. Flying: build a nest round the hull - a cup of super-shielded blocks behind and around you, the nose's way out "
     "open, a turret flora on top - and dock in it. Docked: undock with a push off the surface. Any block you touch "
     "docks you; docked, every cooldown runs x1.6 (x2.5 in a nest or on super-shielded mass). At most three nests; a "
     "fourth retires the oldest. Mass is volume - one dial: 12 -> 32 blocks per nest at level 10 "
     "(ShrikeConfigSO.nestBlocks), and the cruising trail's wavelength 8 -> 4 u (trailWavelength)."),
    (3, "Targeting Array", 2,
     "LT (held). Paint a rival hull inside the lock cone, else the centre of the mass nearest your aim; each nest "
     "launches one homing missile per paint while its turret has a health block to spare. A missile blows on contact "
     "in an 18 u blast (a shield takes the hit first); a rival hull inside it is a petal hit. Space is reach - one dial: "
     "the lock cone 5 -> 14 degrees at level 10 (ShrikeConfigSO.lockCone)."),
    (4, "Void Sling", 7,
     "B. Shoot a tether (1600 u); it anchors on the first block it meets and the hull flies straight there in exactly "
     "1 s, laying thick trail, docking on the first block it touches. A crystal resets it and gives every nest 3 health "
     "blocks. Time is rate - one dial: the cooldown 6 -> 2.5 s at level 10 (ShrikeConfigSO.slingCooldown)."),
)


def quote(text: str) -> str:
    words, lines, line = text.replace("'", "''").split(), [], ""
    for w in words:
        if len(line) + len(w) + 1 > 96 and line:
            lines.append(line)
            line = w
        else:
            line = f"{line} {w}" if line else w
    lines.append(line)
    return "'" + "\n      ".join(lines) + "'"


def map_asset() -> str:
    out = header(MAP_SCRIPT, "Shrike").replace("  m_EditorClassIdentifier: \n", "  m_EditorClassIdentifier:\n")
    out += f"  vesselClass: {CLASS_ID}\n  entries:\n"
    for element, label, input_event, description in MAP_ENTRIES:
        out += (f"  - Element: {element}\n"
                f"    AbilityLabel: {label}\n"
                f"    AbilityDescription: {quote(description)}\n"
                f"    Input: {input_event}\n"
                "    UnlockLevel: 5\n"
                "    RelockBelowLevel: 4\n"
                "    LatchPolicy: 0\n"
                # Level-5 upgrades are OPEN design slots: the class document names none, and the vessel skill's design
                # gate forbids inventing one.
                "    UpgradeLabel: \n"
                "    UpgradeDescription: \n")
    return out


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
        CRYSTAL_EFFECT: crystal_effect_asset(), CRYSTAL_EFFECT + ".meta": S.asset_meta(CRYSTAL_EFFECT_GUID),
        VESSEL_CONTAINER: vessel_container_asset(), VESSEL_CONTAINER + ".meta": S.asset_meta(VESSEL_CONTAINER_GUID),
        SKIMMER_CONTAINER: skimmer_container_asset(), SKIMMER_CONTAINER + ".meta": S.asset_meta(SKIMMER_CONTAINER_GUID),
        CLASS_ASSET: class_asset(), CLASS_ASSET + ".meta": S.asset_meta(CLASS_GUID),
        MAP: map_asset(), MAP + ".meta": S.asset_meta(MAP_GUID),
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


SEEDED = {CONFIG_PATH: (config_asset, SCRIPT["ShrikeConfigSO"], "ShrikeConfig"),
          CAMERA: (camera_asset, CAMERA_SCRIPT, "ShrikeCameraSettingsSO")}


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
    """The four vessel-10 entries go bake-free: their bakes were of the Manta-copy mesh, and the hull is now generated
    at runtime, so the fusion solves on a worker (CRYSTAL_HULL_FUSION.md) like the Stoat's and the Brittle Star's."""
    return re.sub(r"(  - vessel: %d\n    element: \d+\n    bake: )\{[^}]*\}\n" % CLASS_ID, lambda m: m.group(1) + "{fileID: 0}\n", text)


# ===================================================================================================== checks
def check(files: "dict[str, str]") -> "list[str]":
    errors = []
    prefab = files.get(PREFAB)
    if prefab is None:
        return ["Shrike.prefab is missing"]
    head, docs = S.split_docs(prefab)
    ids = {k for k, _ in docs}
    d = dict(docs)

    def need(cond, msg):
        if not cond:
            errors.append(msg)

    need(f"  vesselType: {CLASS_ID}\n" in prefab, "vesselType is not 10 (Shrike)")
    need("  _name: Shrike\n" in prefab and "  m_Name: Shrike\n" in prefab, "the prefab is not named Shrike")
    m = re.search(r"  GlobalObjectIdHash: (\d+)\n", d.get(NETWORK_OBJECT_ID, ""))
    need(m and int(m.group(1)) == S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "GlobalObjectIdHash is not this prefab's (a copied hash collides with the donor's, vessel rule 42)")
    for name, guid in BRITTLESTAR.items():
        need(guid not in prefab, f"a Brittle Star reference survived the rebuild: {name}")
    for fid in (DONOR_EXECUTOR_ID,) + DONOR_TURRETS:
        need(fid not in ids, f"Brittle Star document {fid} still present")
    need(EXECUTOR_ID in ids and f"guid: {SCRIPT['ShrikeExecutor']}" in d.get(EXECUTOR_ID, ""), "ShrikeExecutor missing")
    need(f"  - {{fileID: {EXECUTOR_ID}}}\n" in d.get(REGISTRY_ID, ""), "ShrikeExecutor is not in the ActionExecutorRegistry")
    ex = d.get(EXECUTOR_ID, "")
    need(f"guid: {CONFIG}" in ex and f"hull: {{fileID: {HULL_BUILDER_ID}}}" in ex and TURN_END_CHANNEL in ex,
         "ShrikeExecutor is not wired (config / hull / turn-end channel)")
    tr = d.get(TRANSFORMER_ID, "")
    need(f"guid: {SINGLE_STICK_SCRIPT}" in tr and "vectorFlightModel: 0" in tr, "the transformer is not the one-thumb SingleStickVesselTransformer")
    need("  DefaultMinimumSpeed: 0\n" in tr, "DefaultMinimumSpeed is not 0 (cruise is the studio's `cruise`, all throttle)")
    need(f"guid: {SCRIPT['ShrikePrismController']}" in d.get(PRISM_CONTROLLER_ID, ""), "the spawner is not ShrikePrismController")
    need(f"guid: {SCRIPT['ShrikeAnimation']}" in d.get(ANIMATION_ID, ""), "the animation is not ShrikeAnimation")
    need(f"guid: {SCRIPT['ShrikeHullBuilder']}" in d.get(HULL_BUILDER_ID, ""), "the hull builder is not ShrikeHullBuilder")
    need(f"guid: {SCRIPT['ShrikeHUDController']}" in d.get(HUD_CONTROLLER_ID, "")
         and f"shrike: {{fileID: {EXECUTOR_ID}}}" in d.get(HUD_CONTROLLER_ID, ""), "the HUD controller is not ShrikeHUDController wired to the executor")
    need(f"guid: {HUD_VARIANT}" in d.get(HUD_INSTANCE_ID, ""), "the HUD instance is not ShrikeHUDVariant")
    need(f"guid: {CAMERA_GUID}" in d.get(CAMERA_CUSTOMIZER_ID, ""), "the camera is not ShrikeCameraSettingsSO")
    need(f"guid: {VESSEL_CONTAINER_GUID}" in d.get(VESSEL_IMPACTOR_ID, ""), "the VesselImpactor does not use ShrikeImpactorDataContainer")
    need(SKIMMER_CONTAINER_GUID in d.get(SKIMMER_INSTANCE_ID, ""), "the skimmer does not use ShrikeSkimmerImpactorDataContainer")
    ai = d.get(AI_PILOT_ID, "")
    need("  abilities: []\n" in ai, "the AIPilot runs a blind ability cycle (the executor's autopilot presses)")
    need(f"  boostPolicy: {{fileID: 11400000, guid: {NO_BOOST_POLICY}, type: 2}}\n" in ai,
         "the AIPilot's boost policy is not the no-op HoldBoostAIPolicy (the Shrike has no boost)")
    handler = d.get(HANDLER_ID, "")
    for name, _, event in CONTROLS:
        need(re.search(r"  - InputEvent: %d\n    ShipActions:\n    - \{fileID: 11400000, guid: %s, type: 2\}" % (event, CONTROL_GUID[name]), handler),
             f"{name} is not bound to InputEvents {event}")
    need("_gamepadActionOverrides: []" in handler and "_touchActionOverrides: []" in handler, "device overrides must be empty (shared bindings)")
    dangling = S.dangling_refs(prefab)
    need(not dangling, f"{len(dangling)} dangling local reference(s): {dangling[:5]}")

    hud = files.get(HUD_VARIANT_PATH)
    if hud is None:
        errors.append("ShrikeHUDVariant.prefab is missing")
    else:
        need(f"guid: {SCRIPT['ShrikeHUDView']}" in hud and BRITTLESTAR["BrittleStarHUDView.cs"] not in hud, "the HUD variant's view is not ShrikeHUDView")
        for field in ("lanceGauge", "nestGauge", "arrayGauge", "slingGauge"):
            need(re.search(r"  %s: \{fileID: [1-9]\d*\}" % field, hud), f"HUD view field {field} is not wired")
        for old, new in ICON_SWAP.items():
            need(old not in hud and new in hud, f"HUD icon {new} not swapped in")

    for path, want in owned_files().items():
        have = files.get(path)
        need(have == want, f"{os.path.relpath(path, ROOT)} is {'missing' if have is None else 'drifted'} - run the tool")
    for path, (_, script, name) in SEEDED.items():
        have = files.get(path) or ""
        need(f"guid: {script}, type: 3}}" in have and f"  m_Name: {name}\n" in have, f"{os.path.relpath(path, ROOT)} missing or not a {name}")

    need(f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(CONTAINER, ""),
         "not registered in the Vessel Prefab Container (by its root TRANSFORM)")
    # Unity folds the long line ("guid: ...,\n      type: 3}"), as it did for the Shrike's entry from before the port.
    need(re.search(r"    Prefab: \{fileID: %s, guid: %s,\s+type: 3\}\n" % (ROOT_GO, PREFAB_GUID), files.get(NETWORK_PREFABS, "")),
         "not registered in DefaultNetworkPrefabs (by its root GAMEOBJECT)")
    for path in CLASS_LISTS:
        need(CLASS_GUID in files.get(path, ""), f"SO_Class_Shrike is not in {os.path.basename(path)}")
    fusion = files.get(FUSION, "")
    need(len(re.findall(r"^  - vessel: %d\n" % CLASS_ID, fusion, re.M)) == 4, "CrystalHullFusionConfig does not carry exactly four Shrike entries")
    need(not re.search(r"  - vessel: %d\n    element: \d+\n    bake: \{fileID: [1-9]" % CLASS_ID, fusion),
         "a Shrike fusion entry still names a bake of the Manta-copy mesh")
    for meta, guid in ((CARD_ACTIVE_META, CARD_ACTIVE), (CARD_INACTIVE_META, CARD_INACTIVE)):
        need(f"guid: {guid}" in files.get(meta, ""), f"{os.path.basename(meta)} missing (render_shrike_card_icon.py)")
    enum = files.get(os.path.join(SCRIPTS, "Data/Enums/VesselClassType.cs"), "")
    need(re.search(r"\bShrike = %d," % CLASS_ID, enum), "VesselClassType has no Shrike = 10")
    roster = files.get(os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs"), "")
    need("VesselClassType.Shrike" in roster, "ToyVesselRoster.Default does not offer the Shrike (vessel rule 40)")
    return errors


def read_all() -> "dict[str, str]":
    paths = {PREFAB, HUD_VARIANT_PATH, CONTAINER, NETWORK_PREFABS, FUSION, *CLASS_LISTS, *owned_files(), *SEEDED,
             CARD_ACTIVE_META, CARD_INACTIVE_META,
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
    if f"guid: {SCRIPT['ShrikeExecutor']}" not in files.get(PREFAB, ""):
        donor = open(DONOR, encoding="utf-8", newline="").read()
        write(PREFAB, build_prefab(donor))
        print("rebuilt Shrike.prefab from BrittleStar.prefab (the Manta copy is replaced in place, guid kept)")
    else:
        print("Shrike.prefab carries ShrikeExecutor - the rebuild stands down (spent one-shot)")
    if HUD_VARIANT_PATH not in files:
        write(HUD_VARIANT_PATH, build_hud_variant(open(HUD_DONOR, encoding="utf-8", newline="").read()))
        print("cloned BrittleStarHUDVariant.prefab -> ShrikeHUDVariant.prefab")
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
        "vesselType back to the donor's": (PREFAB, lambda t: t.replace(f"  vesselType: {CLASS_ID}\n", "  vesselType: 16\n")),
        "executor dropped from the registry": (PREFAB, lambda t: t.replace(f"  _executors:\n  - {{fileID: {EXECUTOR_ID}}}\n", "  _executors: []\n")),
        "a dangling reference": (PREFAB, lambda t: t.replace(f"  hull: {{fileID: {HULL_BUILDER_ID}}}", "  hull: {fileID: 12345}")),
        "the donor's hash": (PREFAB, lambda t: re.sub(r"  GlobalObjectIdHash: \d+", "  GlobalObjectIdHash: 1", t, count=1)),
        "a Brittle Star script left behind": (PREFAB, lambda t: t.replace(SCRIPT["ShrikeAnimation"], BRITTLESTAR["BrittleStarAnimation.cs"])),
        "unregistered from the container": (CONTAINER, lambda t: t.replace(PREFAB_GUID, "0" * 32)),
        "the map drifted": (MAP, lambda t: t.replace("Input: 7", "Input: 0")),
        "Lance unbound": (PREFAB, lambda t: t.replace(CONTROL_GUID["ShrikeLance"], "f" * 32)),
        "a stale fusion bake": (FUSION, lambda t: re.sub(r"(  - vessel: 10\n    element: 1\n    bake: )\{fileID: 0\}", r"\1{fileID: 11400000, guid: a828e0f4240b5d54e8a4aa29c016ea12, type: 2}", t)),
        "roster entry missing": (os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs"), lambda t: t.replace("VesselClassType.Shrike", "")),
        "the Brittle Star's boost policy": (PREFAB, lambda t: t.replace(NO_BOOST_POLICY, "005f1b63856ace9974b0b3dbf2bb0853")),
    }
    failed = 0
    for name, (path, mutate) in mutations.items():
        copy = dict(files)
        copy[path] = mutate(copy[path])
        if copy[path] == files[path]:
            print(f"SELF-TEST: mutation '{name}' changed nothing")
            failed += 1
        elif not check(copy):
            print(f"SELF-TEST: mutation '{name}' was NOT caught")
            failed += 1
    print(f"author_shrike_assets --self-test: {len(mutations) - failed}/{len(mutations)} negative controls fired")
    return 1 if failed else 0


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errors = check(read_all())
    for e in errors:
        print("FAIL:", e)
    print("author_shrike_assets: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
