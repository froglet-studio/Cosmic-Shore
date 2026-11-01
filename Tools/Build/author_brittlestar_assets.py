#!/usr/bin/env python3
"""The Brittle Star's VESSEL CREATION TOOL: its prefab, assets and every registration, then prove the shipped set.

    python3 Tools/Build/author_brittlestar_assets.py             # author what is missing (idempotent), then check
    python3 Tools/Build/author_brittlestar_assets.py --check     # validate the SHIPPED files, write nothing
    python3 Tools/Build/author_brittlestar_assets.py --self-test # prove every check fires (negative controls)

Design: Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md (from the Notion "Class - Brittle Star" page).

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/BrittleStar.prefab (+ .meta) - a document-level clone of Stoat.prefab (the fleet's
    leanest self-owned hull: procedural, its own colliders, DefaultVesselTelemetry, an authored HUD with petal bars and a
    gauge per card), turned into the Brittle Star:
        - root GameObject / VesselStatus renamed, `vesselType: 16` (VesselClassType.BrittleStar), root scale 1;
        - a FRESH Netcode GlobalObjectIdHash for THIS prefab guid (author_stoat_assets.global_object_id_hash, verified
          there against a shipped hash) and InScenePlacedSourceGlobalObjectIdHash cleared (vessel rule 42);
        - the flight model: the Sparrow's ONE-THUMB transformer block (single-stick, no vector model) on the SAME fileID,
          its script swapped to BrittleStarVesselTransformer (Speed Mode's multiplier);
        - the trail: the Stoat's spawner swapped (same fileID) to BrittleStarPrismController laying a single small
          block from the cockpit (Speed Mode stretches its spacing; Creation Mode's pentagon is the executor's);
        - the hull: the StoatHull GameObject becomes BrittleStarHull (same fileIDs, so VesselCustomization's paint and
          the renderer stay wired), its builder swapped to BrittleStarHullBuilder; the two VesselJet instances the Stoat
          carried are REMOVED (the hull draws its own six thruster flames); the hit box re-sized to the star;
        - the animation: StoatAnimation -> BrittleStarAnimation on the same fileID (VesselStatus's reference holds);
        - the executors: every Stoat executor removed; BrittleStarExecutor added (config, hull, gun, turn-end channel);
          a BrittleStarTurrets object with a Gun, a ProjectileFactory and a pool of SparrowProjectile rounds (friendly
          fire on, swept hits, the fleet's scored bullet hit);
        - the bindings (shared, like the Sparrow's): RT Fire, LT Ring Detonation, A Speed, B Creation, X Destruction;
        - the AI: no blind ability cycle (the executor's autopilot presses the replicated controls); its boost policy is
          BrittleStarAIBoostPolicy (Speed Mode on the fleet's straight-and-runway law, pressed by the executor);
        - the HUD: BrittleStarHUDVariant (a copy of StoatHUDVariant: same internal fileIDs, the view swapped to
          BrittleStarHUDView, the four icons and gauges renamed) and BrittleStarHUDController;
        - the camera, the impactor containers and the skimmer's container: the Brittle Star's own;
        - the skimmer: affectSelf off (vessel contract §9).
    Assets: BrittleStarConfig + the five control assets (VesselActions/BrittleStar/), the crystal effect, the two
    containers, the camera, SO_Class_BrittleStar, the elemental map, every new script's .meta, the doc's .meta.
    Registrations: Vessel Prefab Container (root TRANSFORM), DefaultNetworkPrefabs (root GAMEOBJECT), SO_Classlist_All /
    _Classes, four CrystalHullFusionConfig entries (bake-free: the hull is generated at runtime).
    NOT here (code, held by tests): the enum member, ToyVesselRoster.Default, EnumIntegrityTests, the one-thumb roster.

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md: "a spent one-shot must stand down, not abort").
Stoat.prefab moves on; re-cloning would silently re-author the Brittle Star from a donor it has diverged from. So the
prefab and the HUD variant are built ONLY while absent, and --check validates the SHIPPED files' invariants. To
re-author from scratch, delete BrittleStar.prefab / BrittleStarHUDVariant.prefab and their .metas and run again.

TUNING IS SEEDED, NOT OWNED: BrittleStarConfig.asset and the camera asset are written once if absent and then only
checked for their script and name, so tuning them in the editor (or from a future Vessel Studio page) never fails this
gate. Everything else listed above is owned outright and must match byte for byte.
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
SPARROW = os.path.join(VESSELS, "Sparrow.prefab")
PREFAB = os.path.join(VESSELS, "BrittleStar.prefab")
HUD_DIR = A("_Prefabs", "UI Elements", "VesselHUD")
HUD_DONOR = os.path.join(HUD_DIR, "StoatHUDVariant.prefab")
HUD_VARIANT_PATH = os.path.join(HUD_DIR, "BrittleStarHUDVariant.prefab")
ACTIONS_DIR = A("_SO_Assets", "VesselActions", "BrittleStar")
CONTAINER = A("_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = A("DefaultNetworkPrefabs.asset")
CLASSES = A("_SO_Assets", "Classes")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_BrittleStar.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAP = A("Resources", "ElementalAbilityMaps", "BrittleStar.asset")
FUSION = A("Resources", "CrystalHullFusionConfig.asset")
CAMERA = A("_SO_Assets", "Camera", "BrittleStarCameraSettingsSO.asset")
VESSEL_CONTAINER = A("_SO_Assets", "Effects", "Effect Containers", "VesselContainers", "BrittleStarImpactorDataContainer.asset")
SKIMMER_CONTAINER = A("_SO_Assets", "Effects", "Effect Containers", "SkimmerContainers", "BrittleStarSkimmerImpactorDataContainer.asset")
CRYSTAL_EFFECT = A("_SO_Assets", "Effects", "Vessel Crystal Effects", "BrittleStarRingChargeByCrystalEffect.asset")
AI_POLICY = A("_SO_Assets", "AI Boost Policies", "BrittleStarAIBoostPolicy.asset")
SCRIPTS = A("_Scripts")
DOC = os.path.join(SCRIPTS, "Controller", "Vessel", "R_VesselActions", "BRITTLE_STAR.md")

CLASS_ID = 16
STOAT_CLASS_ID = 15


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/BrittleStar/{name}".encode()).hexdigest()


PREFAB_GUID = guid_for("BrittleStar.prefab")
HUD_VARIANT = guid_for("BrittleStarHUDVariant.prefab")
CONFIG = guid_for("BrittleStarConfig.asset")
CLASS_GUID = guid_for("SO_Class_BrittleStar.asset")
MAP_GUID = guid_for("ElementalAbilityMaps/BrittleStar.asset")
CAMERA_GUID = guid_for("BrittleStarCameraSettingsSO.asset")
VESSEL_CONTAINER_GUID = guid_for("BrittleStarImpactorDataContainer.asset")
SKIMMER_CONTAINER_GUID = guid_for("BrittleStarSkimmerImpactorDataContainer.asset")
CRYSTAL_EFFECT_GUID = guid_for("BrittleStarRingChargeByCrystalEffect.asset")
ACTIONS_FOLDER_GUID = guid_for("VesselActions/BrittleStar")
DOC_GUID = guid_for("BRITTLE_STAR.md")
AI_POLICY_GUID = guid_for("BrittleStarAIBoostPolicy.asset")

# (control name, enum value, InputEvents it is bound to). The InputEvents numbers: RightStickAction=1 (RT / LMB /
# Right Shift), LeftStickAction=2 (LT / RMB / Left Shift), Button1=6 (A / Space), Button2=7 (B / T), Button3=8 (X / Q),
# Button4=14 (Y / F). Charge Mode was FlipAction=3 (RB / E) until 2026-10-27, when Garrett moved every RB ability to Y:
# the shoulders belong to the pilot (LB sight, RB payload; Docs/Pilots/CALLSIGNS.md section 5).
CONTROLS = (("BrittleStarSpeedMode", 0, 6), ("BrittleStarCreationMode", 1, 7), ("BrittleStarDestructionMode", 2, 8),
            ("BrittleStarFire", 3, 1), ("BrittleStarRingDetonation", 4, 2), ("BrittleStarChargeMode", 5, 14))
CONTROL_GUID = {name: guid_for(name + ".asset") for name, _, _ in CONTROLS}

# Every script this vessel added: (path under Assets/_Scripts, guid).
NEW_SCRIPTS = tuple((rel, guid_for(os.path.basename(rel))) for rel in (
    "Controller/Vessel/BrittleStarHullForm.cs",
    "Controller/Vessel/BrittleStarHullBuilder.cs",
    "Controller/Vessel/BrittleStarHullSource.cs",
    "Controller/Vessel/BrittleStarHullModel.cs",
    "Controller/Vessel/BrittleStarRules.cs",
    "Controller/Vessel/BrittleStarCord.cs",
    "Controller/Vessel/BrittleStarVesselTransformer.cs",
    "Controller/Vessel/BrittleStarPrismController.cs",
    "Controller/Animation/BrittleStarAnimation.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/BrittleStarActionSO.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/BrittleStarConfigSO.cs",
    "Controller/Vessel/R_VesselActions/Executors/BrittleStarExecutor.cs",
    "Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/BrittleStarRingChargeByCrystalEffectSO.cs",
    "UI/Controller/BrittleStarHUDController.cs",
    "UI/View/BrittleStarHUDView.cs",
    "Controller/AI/Boost/BrittleStarSpeedModeAIPolicySO.cs",
    "Tests/Editor/BrittleStarTests.cs",
))
SCRIPT = {os.path.basename(rel)[:-3]: guid for rel, guid in NEW_SCRIPTS}

# ---- the donor's documents (Stoat.prefab), by fileID ---------------------------------------------------------------
ROOT_GO = "6417075533431866457"
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "1733035045152327888"
VESSEL_STATUS_ID = "6441696348568773771"
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
STOAT_EXECUTORS = tuple(f"513726498001234560{i}" for i in range(1, 7))
HULL_GO_ID = "5137264980012345610"
HULL_TRANSFORM_ID = "5137264980012345611"
HULL_BUILDER_ID = "5137264980012345614"
JET_INSTANCES = ("3003162917154485572", "3287304896823774770")
BODY_GO_ID, BODY_COLLIDER_ID = "5137264980012345630", "5137264980012345632"
TRIGGER_GO_ID, TRIGGER_COLLIDER_ID = "5137264980012345634", "5137264980012345636"
HUD_INSTANCE_ID, HUD_VIEW_ID = "5137264980012345620", "5137264980012345622"
SKIMMER_INSTANCE_ID = "7079694710114171170"
TAIL_INSTANCES = (("5313661885591395716", 1), ("7047167775550959062", -1))
STOAT_HUD_VARIANT = "1f9b41e6fa58494599674753191b495b"

# ---- new documents (one readable family, all < INT64_MAX) ----------------------------------------------------------
EXECUTOR_ID = "4419001122334455001"
TURRETS_GO = "4419001122334455010"
TURRETS_TRANSFORM = "4419001122334455011"
GUN_ID = "4419001122334455012"
FACTORY_ID = "4419001122334455013"
POOL_ID = "4419001122334455014"

# ---- fleet guids ---------------------------------------------------------------------------------------------------
SINGLE_STICK_SCRIPT = "b6b54993a316e0041b4bf5981190ac67"     # what the Sparrow's transformer block is
GUN_SCRIPT = "35a4e5dfc7064254cb27fc78eefb05a0"
FACTORY_SCRIPT = "a9b3080b23e64bba93ece7a3dece3463"
POOL_SCRIPT = "b5daed71d21246db9fdb15a9d495e1ec"
SPARROW_ROUND = "{fileID: 7632677803272750685, guid: 1bdcea9e9329dd141af7ab874a4d31a2, type: 3}"   # SparrowProjectile
COMBAT_HIT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"
TURN_END_CHANNEL = "498a06d44bde9184f985c938c803b2a1"
CORD_MATERIAL = "02033cc2904f4f25bf3b6888ce3c98f9"         # ThresherChainMaterial: the cords are the Thresher's chain
SO_VESSEL_SCRIPT = "0309a5343d820c14abd73b35f6fa50f1"
MAP_SCRIPT = "4b3f2a8c8f344355b16a4d4296ae3f98"
CAMERA_SCRIPT = "fd4e6f3597f7439ba7333b35a3a9e164"
VESSEL_CONTAINER_SCRIPT = "fde7d8c2dacf45f99e46799ffeea15e6"
SKIMMER_CONTAINER_SCRIPT = "68b3e74284f14e32a6e21b5fb8c3e8ad"
CARD_ACTIVE, CARD_INACTIVE = "b5a17e5c3d0f4a7e9c1b2d4f6a8c0e21", "b5a17e5c3d0f4a7e9c1b2d4f6a8c0e22"
# the rest of the card set, rendered from the MODEL by Tools/Build/vessel_model/render_cards.py (2026-10-18), which
# names a new PNG md5("cosmic-shore-card:" + its repo path); BrittleStar.png / BrittleStar_Inactive.png keep the guids above
def _model_card(rel):
    return hashlib.md5(("cosmic-shore-card:" + rel).encode()).hexdigest()
CARD_SILHOUETTE = _model_card("Assets/_Graphics/CardImages/BrittleStar_silhouette.png")
CARD_LARGE = _model_card("Assets/_Graphics/CardImages/BrittleStar_large.png")
CARD_PREVIEW = _model_card("Assets/_Graphics/Design Assests/Hangar/BrittleStar_Hangar_Preview.png")
# The Stoat's four icon sprites (by element) and the Brittle Star's that replace them in the HUD variant.
ICON_SWAP = {"ebdd346798d474b5a5ab2364683a8725": hashlib.md5(b"CosmicShore/BrittleStarIcons/BrittleStar_RingDetonation.png").hexdigest(),
             "783faf75777a15c5b2241cfc3e2691d3": hashlib.md5(b"CosmicShore/BrittleStarIcons/BrittleStar_CreationMode.png").hexdigest(),
             "586d56db81bb568c940094830cd59abb": hashlib.md5(b"CosmicShore/BrittleStarIcons/BrittleStar_DestructionMode.png").hexdigest(),
             "669089ec7ad29ed295eccc53d8615225": hashlib.md5(b"CosmicShore/BrittleStarIcons/BrittleStar_SpeedMode.png").hexdigest()}
# The cards the icon kit has recut into parts (the Stoat sprite they replaced -> a view field one of the parts wires).
KIT_RECUT = {"ebdd346798d474b5a5ab2364683a8725": "chargeStarIcon"}   # Charge: the five cords (2026-11-01)
# Script guids that must not survive the clone (every Stoat-specific component).
STOAT_SCRIPTS = ("f8734be12714484eb93134990801178a",   # StoatAnimation
                 "1c6aec6ea8ef44f1bef4884679d33d0a",   # StoatHullBuilder
                 "86da9d91d4364f02ad41dd723756e970",   # StoatHUDController
                 "c120940f60a14c2aa390446ea0791b5a",   # StoatHUDView
                 "cf82f131a8df416cb205ab8055108b82",   # StoatSlingExecutor
                 "95baa921cf22958449a3143749d5e0e3")   # the Stoat's base VesselTransformer (two-stick)
VESSEL_JET_GUID = "55ceca573be662744948afc745f8840b"

# Dimensions (world units): the hit box spans the cockpit and the inner arms. Since the ring grew to radius 6
# (2026-10-14) it no longer covers the ring: the ring is the crosshair's frame, and a 12-unit solid box would make
# every prism near the line a collision.
BODY_BOX = ((0, 0, 0), (4.6, 4.6, 2.4))
TRIGGER_BOX = ((0, 0, 0), (5.4, 5.4, 3.0))
TAIL_X, TAIL_Z = 1.2, -3.0


# ===================================================================================================== the prefab
def mb(fid, go, script, ident=""):
    return S.mb_header(fid, go, script, ident)


def executor_block() -> str:
    return (mb(EXECUTOR_ID, SHIP_ACTIONS_GO, SCRIPT["BrittleStarExecutor"]) +
            f"  config: {{fileID: 11400000, guid: {CONFIG}, type: 2}}\n"
            f"  hull: {{fileID: {HULL_BUILDER_ID}}}\n"
            f"  gun: {{fileID: {GUN_ID}}}\n"
            f"  OnMiniGameTurnEnd: {{fileID: 11400000, guid: {TURN_END_CHANNEL}, type: 2}}\n"
            f"  cordMaterial: {{fileID: 2100000, guid: {CORD_MATERIAL}, type: 2}}\n")


def turret_blocks() -> str:
    return (f"--- !u!1 &{TURRETS_GO}\n"
            "GameObject:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            "  serializedVersion: 6\n"
            "  m_Component:\n"
            f"  - component: {{fileID: {TURRETS_TRANSFORM}}}\n"
            f"  - component: {{fileID: {GUN_ID}}}\n"
            f"  - component: {{fileID: {FACTORY_ID}}}\n"
            f"  - component: {{fileID: {POOL_ID}}}\n"
            "  m_Layer: 8\n"
            "  m_Name: BrittleStarTurrets\n"
            "  m_TagString: Untagged\n"
            "  m_Icon: {fileID: 0}\n"
            "  m_NavMeshLayer: 0\n"
            "  m_StaticEditorFlags: 0\n"
            "  m_IsActive: 1\n"
            f"--- !u!4 &{TURRETS_TRANSFORM}\n"
            "Transform:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {TURRETS_GO}}}\n"
            "  serializedVersion: 2\n"
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
            "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
            "  m_ConstrainProportionsScale: 0\n"
            "  m_Children: []\n"
            f"  m_Father: {{fileID: {ROOT_TRANSFORM_ID}}}\n"
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n" +
            mb(GUN_ID, TURRETS_GO, GUN_SCRIPT) +
            "  firePeriod: 0.2\n"
            "  sideLength: 2\n"
            f"  projectileFactory: {{fileID: {FACTORY_ID}}}\n" +
            mb(FACTORY_ID, TURRETS_GO, FACTORY_SCRIPT) +
            "  poolEntries:\n"
            "  - type: 0\n"
            f"    poolManager: {{fileID: {POOL_ID}}}\n" +
            mb(POOL_ID, TURRETS_GO, POOL_SCRIPT) +
            f"  prefab: {SPARROW_ROUND}\n"
            # Space Mode's five guns (2026-10-17): up to 450 rounds/s at a full pull, overheating in ~6 s at Space 0, each
            # round flying 4.5 s - ~2,000 alive in a burst, plus the one turret's sustained 90/s. At Space 10 a burst
            # runs ~20 s (heatCapacity x3.33) and each round flies 9 s, so ~4,000 can be alive: past maxSize the pool
            # creates on demand (sized here for Space 0; BRITTLE_STAR.md §3.4 Cost).
            "  defaultCapacity: 1400\n"
            "  maxSize: 2400\n"
            "  enableBufferMaintenance: 1\n"
            "  bufferSizeTarget: 1400\n"
            "  maxInstantiateRate: 600\n"
            "  baseInstantiateRate: 200\n"
            "  maxAddsPerFrame: 64\n")


def bindings() -> str:
    """Shared (every device, the Sparrow's shape): touch reaches RT/LT through its own trigger gestures."""
    out = "  _inputEventShipActions:\n"
    for name, _, event in sorted(CONTROLS, key=lambda c: c[2]):
        out += f"  - InputEvent: {event}\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL_GUID[name]}, type: 2}}\n"
    return out + "  _resourceEventClassActions: []\n  _touchActionOverrides: []\n  _gamepadActionOverrides: []\n"


def sparrow_transformer_body() -> str:
    """The Sparrow's one-thumb transformer block's FIELDS (everything after m_EditorClassIdentifier)."""
    text = open(SPARROW, encoding="utf-8").read()
    head, docs = S.split_docs(text)
    for fid, chunk in docs:
        if f"guid: {SINGLE_STICK_SCRIPT}, type: 3}}" in chunk and "PitchScaler" in chunk:
            body = chunk.split("  m_EditorClassIdentifier: \n", 1)[1]
            # The Brittle Star has no boost ability: decay off, no boostChanged channel; its own cruise.
            body = S.set_field(body, "DefaultThrottleScaler", "35")
            body = S.set_field(body, "DefaultMinimumSpeed", "10")
            body = S.set_field(body, "ThrottleScaler", "35")
            body = S.set_field(body, "MinimumSpeed", "10")
            # No boost ability, so no Time dial on a boost it never does (Speed Mode is the one Time dial).
            body = body.replace("  BoostSpeedMultiplier:\n    Enabled: 1\n", "  BoostSpeedMultiplier:\n    Enabled: 0\n", 1)
            return body.rstrip("\n") + "\n"
    raise RuntimeError("Sparrow.prefab drifted: its SingleStickVesselTransformer block not found")


def animation_block() -> str:
    return (mb(ANIMATION_ID, ROOT_GO, SCRIPT["BrittleStarAnimation"]) +
            "  SkinnedMeshRenderer: {fileID: 0}\n"
            "  SaveNewPositions: 1\n"
            "  brakeThreshold: 0.65\n"
            "  lerpAmount: 2\n"
            "  smallLerpAmount: 0.7\n"
            f"  executor: {{fileID: {EXECUTOR_ID}}}\n")


def hud_controller_block() -> str:
    return (mb(HUD_CONTROLLER_ID, ROOT_GO, SCRIPT["BrittleStarHUDController"], "Assembly-CSharp::CosmicShore.UI.BrittleStarHUDController") +
            f"  baseView: {{fileID: {HUD_VIEW_ID}}}\n"
            "  _iconSetSwitcher: {fileID: 0}\n"
            f"  view: {{fileID: {HUD_VIEW_ID}}}\n"
            f"  star: {{fileID: {EXECUTOR_ID}}}\n")


def hull_builder_block() -> str:
    # The hull component on the BrittleStarHull GameObject. Since 2026-10-18 it is BrittleStarHullModel (it finds its parts
    # in BrittleStar.fbx, nested under the hull, by name; /vessel-model, specs/brittlestar.py); the procedural
    # BrittleStarHullBuilder it replaced sat on the same fileID, so the executor's `hull` reference never moved. Nothing
    # is serialized: the ring's numbers are BrittleStarHullForm.Defaults, the one source. (The clone that calls this is a
    # spent one-shot: the committed prefab already carries the model, which only the editor-free surgery recorded in
    # BRITTLE_STAR.md section 2 adds.)
    return mb(HULL_BUILDER_ID, HULL_GO_ID, SCRIPT["BrittleStarHullModel"])


def box(chunk: str, centre, size) -> str:
    chunk = re.sub(r"  m_Size: \{[^}]*\}\n", f"  m_Size: {S.v3(*size)}\n", chunk)
    return re.sub(r"  m_Center: \{[^}]*\}\n", f"  m_Center: {S.v3(*centre)}\n", chunk)


def set_mod(chunk: str, path: str, value: str) -> str:
    pat = re.compile(r"(      propertyPath: %s\n      value: )[^\n]*\n" % re.escape(path))
    if not pat.search(chunk):
        raise RuntimeError(f"Stoat.prefab drifted: no modification {path}")
    return pat.sub(lambda m: m.group(1) + value + "\n", chunk, count=1)


def build_prefab(donor: str, sparrow_body: str) -> str:
    text = donor
    text = S.replace_once(text, "  m_Name: Stoat\n", "  m_Name: BrittleStar\n", "root GameObject name")
    text = S.replace_once(text, "  _name: Stoat\n", "  _name: BrittleStar\n", "VesselStatus _name")
    text = S.replace_once(text, f"  vesselType: {STOAT_CLASS_ID}\n", f"  vesselType: {CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)

    head, docs = S.split_docs(text)

    # Root: scale 1, the turrets object as a child.
    root = S.get_doc(docs, ROOT_TRANSFORM_ID)
    root = re.sub(r"  m_LocalScale: \{[^}]*\}\n", "  m_LocalScale: {x: 1, y: 1, z: 1}\n", root, count=1)
    S.set_doc(docs, ROOT_TRANSFORM_ID, root)
    S.add_child(docs, ROOT_TRANSFORM_ID, TURRETS_TRANSFORM)

    # Executors: every Stoat one out, the Brittle Star's in.
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
    i = S.doc_index(docs, REGISTRY_ID)
    docs.insert(i + 1, [EXECUTOR_ID, executor_block()])
    docs.append([TURRETS_GO, turret_blocks()])

    # Bindings.
    handler = S.get_doc(docs, HANDLER_ID)
    handler = re.sub(r"  _inputEventShipActions:(?:.*\n)*?(?=  _onButtonPressed:)", bindings(), handler, count=1)
    S.set_doc(docs, HANDLER_ID, handler)

    # Flight model: the Sparrow's one-thumb block, on the same fileID, as BrittleStarVesselTransformer.
    tr = S.get_doc(docs, TRANSFORMER_ID)
    tr = tr.split("  m_Script:", 1)[0] + f"  m_Script: {{fileID: 11500000, guid: {SCRIPT['BrittleStarVesselTransformer']}, type: 3}}\n" \
        "  m_Name: \n  m_EditorClassIdentifier: \n" + sparrow_body
    S.set_doc(docs, TRANSFORMER_ID, tr)

    # Trail: one small block from the cockpit (the class document's "single small block"), skimmer clearance on.
    pc = S.get_doc(docs, PRISM_CONTROLLER_ID)
    pc = pc.replace("guid: 909fb5cbbca8c4549b5a9df56d837e55, type: 3}", f"guid: {SCRIPT['BrittleStarPrismController']}, type: 3}}", 1)
    for key, value in (("BaseScale", "{x: 1, y: 1, z: 6}"), ("prismType", "2"), ("initialWavelength", "6"),
                       ("minWavelength", "6"), ("maxBlockScale", "1"), ("waitTillOutsideSkimmer", "1"),
                       ("Gap", "0"), ("MinimumGap", "0"), ("offset", "2")):
        pc = S.set_field(pc, key, value)
    S.set_doc(docs, PRISM_CONTROLLER_ID, pc)

    # Animation, HUD controller, AI, camera, impactor.
    S.set_doc(docs, ANIMATION_ID, animation_block())
    S.set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    ai = S.get_doc(docs, AI_PILOT_ID)
    ai = S.set_field(ai, "boostPolicy", f"{{fileID: 11400000, guid: {AI_POLICY_GUID}, type: 2}}")
    S.set_doc(docs, AI_PILOT_ID, ai)
    cam = S.get_doc(docs, CAMERA_CUSTOMIZER_ID)
    cam = re.sub(r"  settings: \{[^}]*\}\n", f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}\n", cam)
    S.set_doc(docs, CAMERA_CUSTOMIZER_ID, cam)
    imp = S.get_doc(docs, VESSEL_IMPACTOR_ID)
    imp = re.sub(r"  vesselImpactorDataContainerSO: \{[^}]*\}\n",
                 f"  vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {VESSEL_CONTAINER_GUID}, type: 2}}\n", imp)
    S.set_doc(docs, VESSEL_IMPACTOR_ID, imp)

    # Hull: same fileIDs, the Brittle Star's builder, no jets (the hull draws its own flames).
    for inst in JET_INSTANCES:
        S.remove_instance(docs, inst)
    hull_go = S.get_doc(docs, HULL_GO_ID).replace("  m_Name: StoatHull\n", "  m_Name: BrittleStarHull\n")
    S.set_doc(docs, HULL_GO_ID, hull_go)
    S.set_doc(docs, HULL_BUILDER_ID, hull_builder_block())

    # Hit box: the star's own.
    S.set_doc(docs, BODY_GO_ID, S.get_doc(docs, BODY_GO_ID).replace("  m_Name: StoatBody\n", "  m_Name: BrittleStarBody\n"))
    S.set_doc(docs, TRIGGER_GO_ID, S.get_doc(docs, TRIGGER_GO_ID).replace("  m_Name: StoatHitbox\n", "  m_Name: BrittleStarHitbox\n"))
    S.set_doc(docs, BODY_COLLIDER_ID, box(S.get_doc(docs, BODY_COLLIDER_ID), *BODY_BOX))
    S.set_doc(docs, TRIGGER_COLLIDER_ID, box(S.get_doc(docs, TRIGGER_COLLIDER_ID), *TRIGGER_BOX))

    # The wingtip tails: off the cockpit's flanks, behind it (the ring spins and is thrown; the cockpit stays).
    for inst, side in TAIL_INSTANCES:
        chunk = S.get_doc(docs, inst)
        chunk = set_mod(chunk, "m_LocalPosition.x", S.num(side * TAIL_X))
        chunk = set_mod(chunk, "m_LocalPosition.z", S.num(TAIL_Z))
        S.set_doc(docs, inst, chunk)

    # Skimmer: the Brittle Star's container, no self-skim bookkeeping.
    sk = S.get_doc(docs, SKIMMER_INSTANCE_ID)
    sk = re.sub(r"(propertyPath: skimmerImpactorDataContainer\n      value: \n      objectReference: )\{fileID: 11400000, guid: [0-9a-f]{32},\n        type: 2\}",
                lambda m: m.group(1) + f"{{fileID: 11400000, guid: {SKIMMER_CONTAINER_GUID},\n        type: 2}}", sk)
    sk = set_mod(sk, "affectSelf", "0")
    S.set_doc(docs, SKIMMER_INSTANCE_ID, sk)

    # HUD: the Brittle Star's variant, same internal fileIDs.
    for fid, chunk in list(docs):
        if f"m_PrefabInstance: {{fileID: {HUD_INSTANCE_ID}}}" in chunk or fid == HUD_INSTANCE_ID:
            chunk = chunk.replace(STOAT_HUD_VARIANT, HUD_VARIANT)
            chunk = chunk.replace("      value: StoatHUDVariant\n", "      value: BrittleStarHUDVariant\n")
            chunk = chunk.replace(f"guid: {S.HUD_VIEW_SCRIPT}, type: 3}}", f"guid: {SCRIPT['BrittleStarHUDView']}, type: 3}}")
            chunk = chunk.replace("CosmicShore.UI.StoatHUDView", "CosmicShore.UI.BrittleStarHUDView")
            S.set_doc(docs, fid, chunk)

    return S.join_docs(head, docs)


# ===================================================================================================== the HUD variant
def build_hud_variant(donor: str) -> str:
    text = donor.replace("      value: StoatHUDVariant\n", "      value: BrittleStarHUDVariant\n")
    text = text.replace(f"guid: {S.HUD_VIEW_SCRIPT}, type: 3}}", f"guid: {SCRIPT['BrittleStarHUDView']}, type: 3}}")
    text = text.replace("CosmicShore.UI.StoatHUDView", "CosmicShore.UI.BrittleStarHUDView")
    for old, new in (("rippleGauge", "ringGauge"), ("modeGauge", "creationGauge"),
                     ("accretionGauge", "destructionGauge"), ("timeGauge", "speedGauge")):
        text = S.replace_once(text, f"  {old}: {{fileID:", f"  {new}: {{fileID:", f"view field {old}")
    for old, new in (("RippleGauge", "RingGauge"), ("ModeGauge", "CreationGauge"),
                     ("AccretionGauge", "DestructionGauge"), ("TimeGauge", "SpeedGauge")):
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
    """SEEDED once (see the module doc). Numbers = BrittleStarConfigSO's field initializers, the C# being the source."""
    g = lambda guid: f"{{fileID: 11400000, guid: {guid}, type: 2}}"
    return (header(SCRIPT["BrittleStarConfigSO"], "BrittleStarConfig") + CONFIG_BODY.replace("@COMBAT@", g(COMBAT_HIT_CHANNEL)) +
            "".join(S.empty_event(n) for n in CONFIG_EVENTS))


CONFIG_EVENTS = ("speedModeEvent", "creationModeEvent", "destructionModeEvent", "chargeModeEvent", "modeOffEvent",
                 "fireEvent", "cordSliceEvent", "ringLaunchEvent", "ringBlastEvent", "ringRebuildEvent",
                 "chargeStoredEvent", "overheatEvent")

CONFIG_BODY = (
    "  modeBlendSeconds: 0.35\n"
    "  speedModeMultiplier:\n" + ef(6, 6, 8, 4, 4) +
    "  speedModeSpacingFollow: 1\n"
    "  speedModeStretch: 1\n"
    "  prismWidthScale:\n" + ef(1, 1, 2, 2, 1) +
    "  creationSpacing: 6\n"
    "  creationRadiusScale: 1\n"
    "  creationOffset: 2\n"
    "  creationMaxPerFrame: 3\n"
    "  creationSpeedMultiplier: 0.5\n"
    "  creationWaitTime: 0.6\n"
    "  fireRate: 90\n"
    "  projectileSpeed: 135\n"
    "  projectileTime: 4.5\n"
    "  rangeMultiplier:\n" + ef(1, 1, 2, 3, 0.7) +
    "  projectileScale: 1\n"
    "  projectileScaleMultiplier:\n" + ef(1, 1, 1.8, 1, 0.6) +
    "  roundGrowthFactor: 3\n"
    "  armFocusDistance: 120\n"
    "  armSplayDegrees: 25\n"
    "  aimDeadzone: 0.25\n"
    "  ringTurnRate: 540\n"
    "  heatPerRound: 0.000637\n"
    "  heatCapacity:\n" + ef(1, 1, 3.3333, 3, 1) +
    "  coolRate: 0.12\n"
    "  chargeCoolRate: 0.6\n"
    "  overheatRecoverBelow: 0.35\n"
    "  cordLength: 80\n"
    "  cordLooseLengthScale: 1.5\n"
    "  cordLinks: 16\n"
    "  cordCutRadius: 0.6\n"
    "  cordLooseWave: 144\n"
    "  cordRigidWave: 2500\n"
    "  cordLooseDamping: 0.4\n"
    "  cordRigidDamping: 14\n"
    "  cordPose: 1600\n"
    "  cordMaxRigidity: 0.4\n"
    "  cordRigidityRate: 12\n"
    "  cordDebrisRestitution: 0.5\n"
    "  cordDebrisSpeedLimit: 200\n"
    "  cordRenderSubdivisions: 4\n"
    "  cordColor: {r: 0.07, g: 0.065, b: 0.065, a: 1}\n"
    "  cordRimBrightness: 0.75\n"
    "  cordFallbackRim: {r: 0.3, g: 0.9, b: 0.85, a: 1}\n"
    "  ringLaunchSpeed: 240\n"
    "  ringFuseSeconds: 1.1\n"
    "  ringContactRadiusScale: 1\n"
    "  ringFlightSpin: 900\n"
    "  ringBlastSeconds: 0.7\n"
    "  ringBlastFallbackRadius: 200\n"
    "  ringBlastThicknessScale: 4\n"
    "  blastScale:\n" + ef(1, 1, 4, 1, 1) +
    "  ringBlastFrontWidth: 0.05\n"
    "  ringBlastFrontHeight: 0.04\n"
    "  ringBlastRelief: 0.12\n"
    "  ringBlastSpinDegrees: 108\n"
    "  ringBlastCrestDepth: 2.5\n"
    "  ringBlastPentagonLag: 0.8\n"
    "  ringBlastArmWidth: 0.08\n"
    "  ringBlastArmWiggle: 0.06\n"
    "  ringBlastArmCurlDegrees: 30\n"
    "  maxSpareRings: 2\n"
    "  rebuildSeconds: 3\n"
    "  ringBlastHitClass: 3\n"
    "  ringBlastHitCooldown: 0.5\n"
    "  cordHitCooldown: 0.5\n"
    "  onCombatHitLanded: @COMBAT@\n"
    "  aiModeIntervalSeconds: 3\n"
    "  aiFireMinTargets: 2\n"
    "  aiDestructionMinTargets: 8\n"
    "  aiDetonateMinTargets: 18\n"
    "  aiSpeedModeDegrees: 50\n"
    "  aiTriggerMinSeconds: 0.4\n")


def control_asset(name: str, value: int) -> str:
    return header(SCRIPT["BrittleStarActionSO"], name) + f"  control: {value}\n"


def crystal_effect_asset() -> str:
    return header(SCRIPT["BrittleStarRingChargeByCrystalEffectSO"], "BrittleStarRingChargeByCrystalEffect")


def vessel_container_asset() -> str:
    """The fleet's prism effects (the Stoat's set: slow / damage / boost reset / danger debuff / haptics - no element
    gates), and for EVERY crystal kind the haptics plus the ring charge (the document: 'a crystal', any crystal)."""
    g = lambda guid: f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
    crystal = g(CRYSTAL_EFFECT_GUID)
    return (header(VESSEL_CONTAINER_SCRIPT, "BrittleStarImpactorDataContainer") +
            "  vesselPrismEffects:\n" + g("243d6d82ba7f20146a70bdc112b9a278") + g("4da62d4eefe03594ba35027dd537aff0") +
            g("04f85a1f6b06b9248827bf8740168b2b") + g("6c233c580949b13419f068454c74a110") +
            g("c7ccaca885824b24b716b12148d77ce1") + g("ca2d1be3880b82941a0465a1f7e45280") +
            "  vesselCrystalEffects:\n" + g("8de915fb00503344ca89a92a0a127b00") + crystal +
            "  vesselMassCrystalEffects:\n" + crystal +
            "  vesselChargeCrystalEffects:\n" + crystal +
            "  vesselSpaceCrystalEffects:\n" + crystal +
            "  vesselTimeCrystalEffects:\n" + crystal +
            "  vesselSkimmerEffects: []\n  vesselLifeformCrystalEffects: []\n"
            "  omniCrystalRetirement: {fileID: 0}\n")


def skimmer_container_asset() -> str:
    """The joust hit and the skim explosion, the skim haptics and the forcefield crackle (the Stoat's, no gates)."""
    g = lambda guid: f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
    return (header(SKIMMER_CONTAINER_SCRIPT, "BrittleStarSkimmerImpactorDataContainer") +
            "  vesselSkimmerEffectsSO:\n" + g("c2570a8d81db3ab4eb0ba30f184e081f") + g("7e54b5536d0d1fbcda92e10ddf159bf6") +
            "  skimmerPrismEffectsSO:\n" + g("dcce08b9351b97c4eb275508001a5b8e") + g("5bea3c7d44b956064c78606bba3077b8") +
            "  skimmerCrystalEffectsSO: []\n")


def camera_asset() -> str:
    """SEEDED once. A hard-attached chase (the fleet default: no look-ahead, no ease) straight BEHIND the hull with no
    height offset - the whole vessel is a crosshair, the ring framing what it flies at (Garrett, 2026-10-14) - far enough
    back that the 12-unit ring reads; the fleet's 12000 far plane STATED (vessel contract 1.8)."""
    return (header(CAMERA_SCRIPT, "BrittleStarCameraSettingsSO") +
            "  mode: 0\n"
            "  followOffset: {x: 0, y: 0, z: -38}\n"
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
    return (header(SO_VESSEL_SCRIPT, "SO_Class_BrittleStar").replace("  m_EditorClassIdentifier: \n",
                                                                      "  m_EditorClassIdentifier: Assembly-CSharp::SO_Vessel\n") +
            f"  Class: {CLASS_ID}\n"
            "  Name: Brittle Star\n"
            "  Description: A one-touch flyer built round a cockpit and a five-pointed support ring. Swap between Speed,\n"
            "    Creation, Space and Charge modes - five guns that overheat, or five cords that whip and stiffen into\n"
            "    sabers - turn the ring to bring a turret to bear, and throw spare rings as bombs.\n"
            "  PrimaryElement: 0\n"
            "  Element: {fileID: 0}\n"
            "  InitialResourceLevels: {Mass: 0, Charge: 0, Space: 0, Time: 0}\n"
            f"  IconActive: {{fileID: 21300000, guid: {CARD_ACTIVE}, type: 3}}\n"
            f"  IconInactive: {{fileID: 21300000, guid: {CARD_INACTIVE}, type: 3}}\n"
            f"  PreviewImage: {{fileID: 21300000, guid: {CARD_PREVIEW}, type: 3}}\n"
            f"  SquadImage: {{fileID: 21300000, guid: {CARD_LARGE}, type: 3}}\n"
            "  TrailPreviewImage: {fileID: 0}\n"
            f"  CardSilohoutteActive: {{fileID: 21300000, guid: {CARD_ACTIVE}, type: 3}}\n"
            f"  CardSilohoutteInactive: {{fileID: 21300000, guid: {CARD_SILHOUETTE}, type: 3}}\n"
            "  Abilities: []\n"
            "  Games: []\n"
            "  TrainingGames: []\n"
            "  gameplayParameter1:\n    LeftHandLabel: Casual\n    RightHandLabel: Challenging\n    Value: 0.5\n"
            "  gameplayParameter2:\n    LeftHandLabel: Relaxing\n    RightHandLabel: Thrilling\n    Value: 0.5\n"
            "  gameplayParameter3:\n    LeftHandLabel: Solo\n    RightHandLabel: Social\n    Value: 0.5\n"
            "  isLocked: 0\n"
            "  ownedFromStart: 0\n"
            "  UnlockCost: 100\n")


def quote(text: str) -> str:
    """A single-quoted YAML scalar, folded at ~100 columns the way Unity writes long strings."""
    words, lines, line = text.replace("'", "''").split(), [], ""
    for w in words:
        if len(line) + len(w) + 1 > 96 and line:
            lines.append(line)
            line = w
        else:
            line = f"{line} {w}" if line else w
    lines.append(line)
    return "'" + "\n      ".join(lines) + "'"


MAP_ENTRIES = (
    (1, "Charge Mode", 14,
     "Y. FIVE CORDS, one from each turret, slicing every prism they touch (any domain; your own fresh trail is spared) "
     "and striking rival hulls for petals. Released, RT leaves them WHIPS at 150% length: heavy, inextensible, slow - a "
     "turn, a ring flick or a sweep of the right stick sends a wave travelling down them that rings on for seconds. "
     "Pulled, RT shortens them to 100% and stiffens them into five SABERS that keep 60% of the whip's give (analog: "
     "every depth in between). "
     "The right stick turns them with the ring and opens them out. Using them cools the guns. "
     "LT throws the ring - only with a SPARE ring in stock (one per crystal, two at most); the mounted ring then regrows "
     "on its own. It blows on contact or its fuse in a disk as wide as the cell's nucleus, bursting out of the ring as "
     "faceted volumes - five coiling arms led by crystals, five crests and the ring's own pentagon - still spinning, destroying every prism in its "
     "plane and stripping petals from every rival hull it crosses (petals only - nothing is moved). The card shows the "
     "spare stock. Charge is threat - one dial: the cords' cut width and the rounds' size x1 -> x1.8 at level 10 "
     "(projectileScaleMultiplier), the disk's thickness x1 -> x4 on top of its x4 base, 4 to 16 times the ring's (blastScale)."),
    (2, "Creation Mode", 7,
     "B. A trail behind EACH of the ring's five corners instead of the single small block from the cockpit - the same "
     "small block, five lines that turn with the ring's firing rotation. The SLOWEST mode: cruise x0.5. Mass is girth - "
     "one dial: the width and height of EVERY block you lay, the base trail's and the corner trails', x1 -> x2 at level "
     "10, never their length (BrittleStarConfigSO.prismWidthScale)."),
    (3, "Space Mode", 8,
     "X. FIVE GUNS: RT fires every turret, its pull setting the rate (analog, up to 90 rounds/s each; the Sparrow's "
     "round at 135 u/s, swelling as it flies, 387 u reach). The right stick's direction turns the ring, and the guns with "
     "it; its deflection moves them in and out: centred they meet 120 u ahead (the vessel a crosshair), pushed to the rim "
     "they splay 25 degrees. In every other mode RT fires the one turret the ring has turned to the aim, the same gun "
     "with no spread. Every round HEATS the guns - heat per second is the pull x the guns firing - and overheated they "
     "cannot fire until they cool; overheated in Space Mode your trail lays DANGER prisms. The card shows the heat. "
     "Friendly fire is on. A round on a rival hull is a scored bullet hit (petals only). Space is reach and endurance: "
     "every gun's range x1 -> x2 at level 10 (BrittleStarConfigSO.rangeMultiplier), and the heat the guns hold x1 -> "
     "x3.33, so a full five-gun burst runs ~6 s at level 0 and ~20 s at level 10 (BrittleStarConfigSO.heatCapacity)."),
    (4, "Speed Mode", 6,
     "A. Ignite every thruster: the hull's cruise speed x6, the base trail's spacing stretched by the same ratio so "
     "production per second does not rise, and every prism stretched with it so the trail still joins up. Time is rate - "
     "one dial: Speed Mode's multiplier x6 -> x8 at level 10 (BrittleStarConfigSO.speedModeMultiplier)."),
)


def map_asset() -> str:
    out = header(MAP_SCRIPT, "BrittleStar").replace("  m_EditorClassIdentifier: \n", "  m_EditorClassIdentifier:\n")
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
                # gate forbids inventing one (BRITTLE_STAR.md §5 carries proposals for Garrett).
                "    UpgradeLabel: \n"
                "    UpgradeDescription: \n")
    return out


def ai_policy_asset() -> str:
    """SEEDED once. The base class's straight-and-runway law at the fleet's numbers; Speed Mode's own hold / rest."""
    return (header(SCRIPT["BrittleStarSpeedModeAIPolicySO"], "BrittleStarAIBoostPolicy") +
            "  alignDegrees: 12\n"
            "  releaseAlignDegrees: 25\n"
            "  stickBand: 0.35\n"
            "  releaseStickBand: 0.6\n"
            "  engageRunwaySeconds: 1.5\n"
            "  releaseRunwaySeconds: 0.5\n"
            "  disabledInModes: []\n"
            "  requireAIPlayer: 0\n"
            "  minHoldSeconds: 1.2\n"
            "  minRestSeconds: 0.8\n")


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
        AI_POLICY + ".meta": S.asset_meta(AI_POLICY_GUID),
        os.path.join(ACTIONS_DIR, "BrittleStarConfig.asset.meta"): S.asset_meta(CONFIG),
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


SEEDED = {os.path.join(ACTIONS_DIR, "BrittleStarConfig.asset"): (config_asset, SCRIPT["BrittleStarConfigSO"], "BrittleStarConfig"),
          CAMERA: (camera_asset, CAMERA_SCRIPT, "BrittleStarCameraSettingsSO"),
          AI_POLICY: (ai_policy_asset, SCRIPT["BrittleStarSpeedModeAIPolicySO"], "BrittleStarAIBoostPolicy")}


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
    """Four entries copied from the Stoat's (bake-free: like the Stoat, the hull is generated at runtime, so the fusion
    solves on a worker - CRYSTAL_HULL_FUSION.md), renumbered to the Brittle Star."""
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
        return ["BrittleStar.prefab is missing"]
    head, docs = S.split_docs(prefab)
    ids = {k for k, _ in docs}
    d = dict(docs)

    def need(cond, msg):
        if not cond:
            errors.append(msg)

    need(f"  vesselType: {CLASS_ID}\n" in prefab, "vesselType is not 16 (BrittleStar)")
    need("  _name: BrittleStar\n" in prefab and "  m_Name: BrittleStar\n" in prefab, "the prefab is not named BrittleStar")
    m = re.search(r"  GlobalObjectIdHash: (\d+)\n", d.get(NETWORK_OBJECT_ID, ""))
    need(m and int(m.group(1)) == S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "GlobalObjectIdHash is not this prefab's (a copied hash collides with the donor's, vessel rule 42)")
    for guid in STOAT_SCRIPTS + (VESSEL_JET_GUID, STOAT_HUD_VARIANT):
        need(guid not in prefab, f"a Stoat-only reference survived the clone: {guid}")
    for fid in STOAT_EXECUTORS:
        need(fid not in ids, f"Stoat executor {fid} still present")
    need(EXECUTOR_ID in ids and f"guid: {SCRIPT['BrittleStarExecutor']}" in d.get(EXECUTOR_ID, ""), "BrittleStarExecutor missing")
    need(f"  - {{fileID: {EXECUTOR_ID}}}\n" in d.get(REGISTRY_ID, ""), "BrittleStarExecutor is not in the ActionExecutorRegistry")
    ex = d.get(EXECUTOR_ID, "")
    need(f"guid: {CONFIG}" in ex and f"hull: {{fileID: {HULL_BUILDER_ID}}}" in ex and f"gun: {{fileID: {GUN_ID}}}" in ex
         and TURN_END_CHANNEL in ex, "BrittleStarExecutor is not wired (config / hull / gun / turn-end channel)")
    need(f"cordMaterial: {{fileID: 2100000, guid: {CORD_MATERIAL}, type: 2}}" in ex,
         "BrittleStarExecutor.cordMaterial is not ThresherChainMaterial (the cords would draw as flat lines)")
    need(f"projectileFactory: {{fileID: {FACTORY_ID}}}" in d.get(GUN_ID, "") and SPARROW_ROUND in d.get(POOL_ID, ""),
         "the turrets' Gun / ProjectileFactory / pool are not wired")
    need(f"guid: {SCRIPT['BrittleStarVesselTransformer']}" in d.get(TRANSFORMER_ID, "") and "vectorFlightModel: 0" in d.get(TRANSFORMER_ID, ""),
         "the transformer is not the one-thumb BrittleStarVesselTransformer")
    need("  BoostSpeedMultiplier:\n    Enabled: 0\n" in d.get(TRANSFORMER_ID, ""),
         "BoostSpeedMultiplier is enabled - a second Time dial on a boost the hull never does")
    need(f"guid: {SCRIPT['BrittleStarPrismController']}" in d.get(PRISM_CONTROLLER_ID, ""), "the spawner is not BrittleStarPrismController")
    need(f"guid: {SCRIPT['BrittleStarAnimation']}" in d.get(ANIMATION_ID, ""), "the animation is not BrittleStarAnimation")
    need(f"guid: {SCRIPT['BrittleStarHullModel']}" in d.get(HULL_BUILDER_ID, ""), "the hull component is not BrittleStarHullModel")
    # the model: BrittleStar.fbx nested under the hull (its root's constant fileIDs), and nothing left of the
    # placeholder's renderer on the hull (a MeshRenderer there would be painted instead of the model)
    model_meta = os.path.join(A("_Models"), "Vessel Models", "BrittleStar.fbx.meta")
    mm = re.search(r"^guid: ([0-9a-f]{32})", open(model_meta).read(), flags=re.M) if os.path.exists(model_meta) else None
    need(mm is not None, "Assets/_Models/Vessel Models/BrittleStar.fbx.meta is missing (build it: vessel_model/build.py brittlestar)")
    if mm:
        inst = [k for k, v in docs if f"m_SourcePrefab: {{fileID: 100100000, guid: {mm.group(1)}, type: 3}}" in v]
        need(len(inst) == 1 and f"m_TransformParent: {{fileID: {HULL_TRANSFORM_ID}}}" in d.get(inst[0], ""),
             "BrittleStar.fbx is not nested once under the BrittleStarHull transform")
    hull_go_doc = d.get(HULL_GO_ID, "")
    need(len(re.findall(r"  - component: ", hull_go_doc)) == 2,
         "the BrittleStarHull GameObject carries more than its Transform and BrittleStarHullModel (a leftover renderer?)")
    need(f"guid: {SCRIPT['BrittleStarHUDController']}" in d.get(HUD_CONTROLLER_ID, "")
         and f"star: {{fileID: {EXECUTOR_ID}}}" in d.get(HUD_CONTROLLER_ID, ""), "the HUD controller is not BrittleStarHUDController wired to the executor")
    need(f"guid: {HUD_VARIANT}" in d.get(HUD_INSTANCE_ID, ""), "the HUD instance is not BrittleStarHUDVariant")
    need(f"guid: {CAMERA_GUID}" in d.get(CAMERA_CUSTOMIZER_ID, ""), "the camera is not BrittleStarCameraSettingsSO")
    need(f"guid: {VESSEL_CONTAINER_GUID}" in d.get(VESSEL_IMPACTOR_ID, ""), "the VesselImpactor does not use BrittleStarImpactorDataContainer")
    need(SKIMMER_CONTAINER_GUID in d.get(SKIMMER_INSTANCE_ID, ""), "the skimmer does not use BrittleStarSkimmerImpactorDataContainer")
    need("  abilities: []\n" in d.get(AI_PILOT_ID, ""), "the AIPilot runs a blind ability cycle (the executor's autopilot presses)")
    need(f"boostPolicy: {{fileID: 11400000, guid: {AI_POLICY_GUID}, type: 2}}" in d.get(AI_PILOT_ID, ""),
         "the AIPilot's boost policy is not BrittleStarAIBoostPolicy")
    handler = d.get(HANDLER_ID, "")
    for name, _, event in CONTROLS:
        need(re.search(r"  - InputEvent: %d\n    ShipActions:\n    - \{fileID: 11400000, guid: %s, type: 2\}" % (event, CONTROL_GUID[name]), handler),
             f"{name} is not bound to InputEvents {event}")
    need("_gamepadActionOverrides: []" in handler and "_touchActionOverrides: []" in handler, "device overrides must be empty (shared bindings)")
    dangling = S.dangling_refs(prefab)
    need(not dangling, f"{len(dangling)} dangling local reference(s): {dangling[:5]}")

    hud = files.get(HUD_VARIANT_PATH)
    if hud is None:
        errors.append("BrittleStarHUDVariant.prefab is missing")
    else:
        need(f"guid: {SCRIPT['BrittleStarHUDView']}" in hud and S.HUD_VIEW_SCRIPT not in hud, "the HUD variant's view is not BrittleStarHUDView")
        for field in ("ringGauge", "creationGauge", "destructionGauge", "speedGauge"):
            need(re.search(r"  %s: \{fileID: [1-9]\d*\}" % field, hud), f"HUD view field {field} is not wired")
        for old, new in ICON_SWAP.items():
            # A card the icon kit has recut (Tools/Build/icon_kit/specs/brittlestar.py) wears kit PARTS on child Images and
            # its slot Image carries no sprite, so the round-1 sprite is gone on purpose: its view field stands in.
            recut = KIT_RECUT.get(old)
            done = new in hud or (recut and re.search(r"  %s: \{fileID: [1-9]\d*\}" % recut, hud))
            need(old not in hud and done, f"HUD icon {new} not swapped in")

    for path, want in owned_files().items():
        have = files.get(path)
        need(have == want, f"{os.path.relpath(path, ROOT)} is {'missing' if have is None else 'drifted'} - run the tool")
    for path, (_, script, name) in SEEDED.items():
        have = files.get(path) or ""
        need(f"guid: {script}, type: 3}}" in have and f"  m_Name: {name}\n" in have, f"{os.path.relpath(path, ROOT)} missing or not a {name}")

    need(f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(CONTAINER, ""),
         "not registered in the Vessel Prefab Container (by its root TRANSFORM)")
    need(f"    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(NETWORK_PREFABS, ""),
         "not registered in DefaultNetworkPrefabs (by its root GAMEOBJECT)")
    for path in CLASS_LISTS:
        need(CLASS_GUID in files.get(path, ""), f"SO_Class_BrittleStar is not in {os.path.basename(path)}")
    need(len(re.findall(r"^  - vessel: %d\n" % CLASS_ID, files.get(FUSION, ""), re.M)) == 4,
         "CrystalHullFusionConfig does not carry exactly four Brittle Star entries")
    for asset, guid in ((os.path.join(ROOT, "Assets/_Graphics/CardImages/BrittleStar.png.meta"), CARD_ACTIVE),
                        (os.path.join(ROOT, "Assets/_Graphics/CardImages/BrittleStar_Inactive.png.meta"), CARD_INACTIVE)):
        need(f"guid: {guid}" in files.get(asset, ""), f"{os.path.basename(asset)} missing (vessel_model/render_cards.py BrittleStar)")
    enum = files.get(os.path.join(SCRIPTS, "Data/Enums/VesselClassType.cs"), "")
    need(re.search(r"\bBrittleStar = %d," % CLASS_ID, enum), "VesselClassType has no BrittleStar = 16")
    roster = files.get(os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs"), "")
    need("VesselClassType.BrittleStar" in roster, "ToyVesselRoster.Default does not offer the Brittle Star (vessel rule 40)")
    return errors


def read_all() -> "dict[str, str]":
    paths = {PREFAB, HUD_VARIANT_PATH, CONTAINER, NETWORK_PREFABS, FUSION, *CLASS_LISTS, *owned_files(), *SEEDED,
             os.path.join(ROOT, "Assets/_Graphics/CardImages/BrittleStar.png.meta"),
             os.path.join(ROOT, "Assets/_Graphics/CardImages/BrittleStar_Inactive.png.meta"),
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
        donor = open(DONOR, encoding="utf-8", newline="").read()
        write(PREFAB, build_prefab(donor, sparrow_transformer_body()))
        print("cloned Stoat.prefab -> BrittleStar.prefab")
    else:
        print("BrittleStar.prefab exists - the clone stands down (spent one-shot)")
    if HUD_VARIANT_PATH not in files:
        write(HUD_VARIANT_PATH, build_hud_variant(open(HUD_DONOR, encoding="utf-8", newline="").read()))
        print("cloned StoatHUDVariant.prefab -> BrittleStarHUDVariant.prefab")
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
        "a dangling reference": (PREFAB, lambda t: t.replace(f"  gun: {{fileID: {GUN_ID}}}", "  gun: {fileID: 12345}")),
        "the donor's hash": (PREFAB, lambda t: re.sub(r"  GlobalObjectIdHash: \d+", "  GlobalObjectIdHash: 1", t, count=1)),
        "a Stoat script left behind": (PREFAB, lambda t: t.replace(SCRIPT["BrittleStarAnimation"], "f8734be12714484eb93134990801178a")),
        "unregistered from the container": (CONTAINER, lambda t: t.replace(PREFAB_GUID, "0" * 32)),
        "the map drifted": (MAP, lambda t: t.replace("Input: 6", "Input: 0")),
        "Fire unbound": (PREFAB, lambda t: t.replace(CONTROL_GUID["BrittleStarFire"], "f" * 32)),
        "cord material unwired": (PREFAB, lambda t: t.replace(f"guid: {CORD_MATERIAL}", "guid: " + "e" * 32)),
        "fusion entries missing": (FUSION, lambda t: t.replace(f"  - vessel: {CLASS_ID}\n", "  - vessel: 99\n")),
        "roster entry missing": (os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs"), lambda t: t.replace("VesselClassType.BrittleStar", "")),
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
    print(f"author_brittlestar_assets --self-test: {len(mutations) - failed}/{len(mutations)} negative controls fired")
    return 1 if failed else 0


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errors = check(read_all())
    for e in errors:
        print("FAIL:", e)
    print("author_brittlestar_assets: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
