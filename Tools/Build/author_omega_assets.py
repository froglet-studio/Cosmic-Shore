#!/usr/bin/env python3
"""The Omega crews' VESSEL CREATION TOOL: three hulls (Warden 25, Bulwark 26, Tempest 27), their assets and every
registration, then prove the shipped set.

    python3 Tools/Build/author_omega_assets.py             # author what is missing (idempotent), then check
    python3 Tools/Build/author_omega_assets.py --check     # validate the SHIPPED files, write nothing
    python3 Tools/Build/author_omega_assets.py --self-test # prove every check fires (negative controls)

Design: Docs/Studios/CONCEPT_FLEET.md section 10 and Assets/_Scripts/Controller/Vessel/Crew/OMEGA_CREW.md. Prototyped in
the Vessel Studio's Omega Crews lab (Docs/Studios/VesselStudio/omega.html; Yash, 2026-10-26: "make this prototype in
Ys-bleeding-edge, I wanna test there").

WHAT IT MAKES, per crew
    Assets/_Prefabs/Spacevessels/<Crew>.prefab (+ .meta) - a document-level clone of Stoat.prefab (the fleet's leanest
    self-owned hull, the donor the Flea, the Angler and the Brittle Star generators also use), turned into the crew:
        - root GameObject / VesselStatus renamed, `vesselType` the crew's id;
        - a FRESH Netcode GlobalObjectIdHash for THIS prefab guid and InScenePlacedSourceGlobalObjectIdHash cleared
          (vessel rule 42);
        - OmegaCrew on the root, wired to the crew's config and the hull's transformer (the forms, the seats, the meter);
        - the flight model: the Stoat's two-stick transformer block on the SAME fileID, its script swapped to
          OmegaVesselTransformer (OmegaCrew switches its cruise and turn rates per form), seeded with the first member's
          page flight (OmegaMath.Members);
        - the executors: every Stoat executor removed and none added (the crew's state is OmegaCrew's);
        - the bindings: RT (RightStickAction 1) -> CrewTriggerRT and LT (LeftStickAction 2) -> CrewTriggerLT in the SHARED
          map and nothing in the device overrides (the Orca's: every device raises those two events from its triggers, a
          phone from its thumb lifts), sent to OmegaCrew.SetTrigger;
        - the AI: no ability cycle (an AIPilot flies the hull; it never presses a crew control);
        - the HUD: the Stoat's HUD variant and view kept as a stand-in, driven by OmegaHUDController;
        - the vessel impactor: the shared OmegaImpactorDataContainer, a fork of the Stoat's WITHOUT its omni Overclock
          (it drives Stoat executors) and WITH OmegaCrystalEffect (every crystal fills the omega meter).
    KEPT FROM THE DONOR ON PURPOSE (the crews have no bodies yet): the Stoat model and its lope, the jets, the tails, the
    hit box, the skimmer, the camera, the card art. The other two members are placeholder bodies OmegaCrewView draws.
    Assets: Omega<Crew>Config (seeded; the lab numbers), SO_Class_<Crew> (SeatCount 3), the element map (four OPEN slots:
    the page's element proposals are not signed off, vessel skill section 3). Shared: OmegaCrystalEffect,
    OmegaImpactorDataContainer (seeded), every new script's .meta, the doc's .meta. Registrations: Vessel Prefab Container
    (root TRANSFORM), DefaultNetworkPrefabs (root GAMEOBJECT), SO_Classlist_All / _Classes, four CrystalHullFusionConfig
    entries per crew (copied from the Stoat's: same hull). NOT here (code, held by tests): the enum members,
    ToyVesselRoster.Default, EnumIntegrityTests.

THE CLONE IS A SPENT ONE-SHOT ONCE A PREFAB IS COMMITTED (CLAUDE.md: "a spent one-shot must stand down, not abort").
A prefab is built ONLY while absent; patch_prefab re-applies the generator-owned parts idempotently; --check validates
the SHIPPED files. To re-author one from scratch, delete <Crew>.prefab and its .meta and run again.

TUNING IS SEEDED, NOT OWNED: the config assets are written once if absent and then only checked for their script, name
and keys. Their numbers belong to the ARTIFACT (/artifact-to-unity).
"""
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import author_stoat_assets as S  # noqa: E402  (document-model helpers + Netcode hash; importing it writes nothing)
import author_wrasse_assets as W  # noqa: E402  (the crew trigger bindings; importing it writes nothing)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
A = lambda *p: os.path.join(ROOT, "Assets", *p)
VESSELS = A("_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Stoat.prefab")
ACTIONS_DIR = A("_SO_Assets", "VesselActions", "Omega")
CONTAINER = A("_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = A("DefaultNetworkPrefabs.asset")
CLASSES = A("_SO_Assets", "Classes")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = A("Resources", "ElementalAbilityMaps")
FUSION = A("Resources", "CrystalHullFusionConfig.asset")
SCRIPTS = A("_Scripts")
DOC = os.path.join(SCRIPTS, "Controller", "Vessel", "Crew", "OMEGA_CREW.md")
EFFECTS = A("_SO_Assets", "Effects")
CRYSTAL_EFFECT = os.path.join(EFFECTS, "Vessel Crystal Effects", "OmegaCrystalEffect.asset")
IMPACT_CONTAINER = os.path.join(EFFECTS, "Effect Containers", "VesselContainers", "OmegaImpactorDataContainer.asset")
STOAT_IMPACT_CONTAINER_PATH = os.path.join(EFFECTS, "Effect Containers", "VesselContainers", "StoatImpactorDataContainer.asset")
ENUM = os.path.join(SCRIPTS, "Data", "Enums", "VesselClassType.cs")
ROSTER = os.path.join(SCRIPTS, "Controller", "Toys", "ToyVesselRoster.cs")

STOAT_CLASS_ID = 15


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/Omega/{name}".encode()).hexdigest()


# (name, VesselClassType id, OmegaMath.Crew value, first member's page flight (cruise, turn, roll), card description)
CREWS = (
    ("Warden", 25, 0, (60, 110, 130),
     "The field crew: the Stoat, the Thresher and the Butterfly. Apart, each flies its own hull (swap seats with the\n"
     "    D-pad); hold LT with the crew together to combine into the Warden's omega form, a lance of plates with the\n"
     "    wrecking ball at its prow. Helm flies, Arms swings the Ripple whip, Core holds the Fold ward and fires\n"
     "    Annihilation."),
    ("Bulwark", 26, 1, (58, 90, 100),
     "The heavy crew: the Rhino, the Grizzly and the Scarab. The slowest omega and the one that does not stop: it\n"
     "    ploughs the mass it touches. Helm flies, Arms fires the Siege cannon, Core holds the Switch wall and fires the\n"
     "    Breaker."),
    ("Tempest", 27, 2, (66, 120, 140),
     "The fast crew: the Squirrel, the Sparrow and the Manta. The fastest omega. Helm flies, Arms fires the Pulsefire\n"
     "    battery, Core holds the Afterburner veil and fires Kabloom."),
)
CREW_BY_NAME = {c[0]: c for c in CREWS}

CRYSTAL_EFFECT_GUID = guid_for("OmegaCrystalEffect.asset")
IMPACT_CONTAINER_GUID = guid_for("OmegaImpactorDataContainer.asset")
ACTIONS_FOLDER_GUID = guid_for("VesselActions/Omega")
DOC_GUID = guid_for("OMEGA_CREW.md")
STOAT_IMPACT_CONTAINER = "63a1b912028e471b9b7a2df570e90be8"     # StoatImpactorDataContainer.asset
MAP_SCRIPT = "4b3f2a8c8f344355b16a4d4296ae3f98"                 # ElementalAbilityMapSO
IMPACT_CONTAINER_SCRIPT = "fde7d8c2dacf45f99e46799ffeea15e6"    # VesselImpactorDataContainerSO
STOAT_OVERCLOCK = "6b508a166e124e95a6490871697185de"            # StoatOverclockByCrystalEffect.asset (drives Stoat executors)
AOE_EXPLOSION_REF = "{fileID: 3479271500403630839, guid: 4a855af160021d241b8c1ac70cf2d792, type: 3}"   # AOEExplosion.prefab
AOE_GRIZZLY_REF = "{fileID: 3479271500403630839, guid: 8713aea50e6b47119e57afc2ce603771, type: 3}"     # AOEGrizzlyExplosion.prefab

# Every script this vessel family added: (path under Assets/_Scripts, guid).
NEW_SCRIPTS = tuple((rel, guid_for(os.path.basename(rel))) for rel in (
    "Controller/Vessel/Crew/OmegaMath.cs",
    "Controller/Vessel/Crew/OmegaCrew.cs",
    "Controller/Vessel/Crew/OmegaCrewView.cs",
    "Controller/Vessel/Crew/OmegaCrewConfigSO.cs",
    "Controller/Vessel/OmegaVesselTransformer.cs",
    "Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/OmegaCrystalEffectSO.cs",
    "UI/Controller/OmegaHUDController.cs",
    "Tests/Editor/OmegaMathTests.cs",
))
SCRIPT = {os.path.basename(rel)[:-3]: guid for rel, guid in NEW_SCRIPTS}

# ---- the donor's documents (Stoat.prefab), by fileID (the table author_flea_assets.py reads) ---------------------
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
CREW_ID = "7120517381209347021"                                 # OmegaCrew on the root (fresh: no donor document has it)
TWO_STICK_SCRIPT = "95baa921cf22958449a3143749d5e0e3"          # VesselTransformer (the Stoat's)
# Stoat-only BEHAVIOUR that must not survive the clone (its model, lope, HUD view and camera are kept as stand-ins).
STOAT_SCRIPTS = ("86da9d91d4364f02ad41dd723756e970",   # StoatHUDController
                 "cf82f131a8df416cb205ab8055108b82",   # StoatSlingExecutor
                 "8c3f54abda8145ecabf7c85253cab9ed",   # ToggleTranslationModeActionExecutor
                 "6eee000b02734120a5138fb645997915",   # StoatDipoleExecutor
                 "ec03f8093b534dfe98ec25e13f18c576",   # StoatPathfinderExecutor
                 "064761a8ffa04919a1308c63fe4b0732",   # StoatModeExecutor
                 "1712753ee2d34c6294526e3c58d4b197",   # StoatRippleExecutor
                 TWO_STICK_SCRIPT)                     # swapped for OmegaVesselTransformer

# The crew's two triggers (CrewTriggerActionSO -> OmegaCrew.SetTrigger): RT the move / weapon / finisher, LT the gather,
# the split and the shield.
BINDINGS = W.crew_bindings([(W.RIGHT_STICK_ACTION, W.CREW_RT_GUID), (W.LEFT_STICK_ACTION, W.CREW_LT_GUID)])


class CrewPaths:
    def __init__(self, name: str):
        self.name, self.class_id, self.crew_value, self.flight, self.description = CREW_BY_NAME[name]
        self.prefab = os.path.join(VESSELS, f"{name}.prefab")
        self.prefab_guid = guid_for(f"{name}.prefab")
        self.config = os.path.join(ACTIONS_DIR, f"Omega{name}Config.asset")
        self.config_guid = guid_for(f"Omega{name}Config.asset")
        self.class_asset = os.path.join(CLASSES, f"SO_Class_{name}.asset")
        self.class_guid = guid_for(f"SO_Class_{name}.asset")
        self.map = os.path.join(MAPS, f"{name}.asset")
        self.map_guid = guid_for(f"ElementalAbilityMaps/{name}.asset")


PATHS = {c[0]: CrewPaths(c[0]) for c in CREWS}


# ===================================================================================================== the prefab
def hud_controller_block() -> str:
    return (S.mb_header(HUD_CONTROLLER_ID, ROOT_GO, SCRIPT["OmegaHUDController"], "Assembly-CSharp::CosmicShore.UI.OmegaHUDController") +
            f"  baseView: {{fileID: {HUD_VIEW_ID}}}\n"
            "  _iconSetSwitcher: {fileID: 0}\n"
            f"  view: {{fileID: {HUD_VIEW_ID}}}\n"
            f"  crew: {{fileID: {CREW_ID}}}\n")


def crew_block(c: CrewPaths) -> str:
    return (S.mb_header(CREW_ID, ROOT_GO, SCRIPT["OmegaCrew"], "Assembly-CSharp::CosmicShore.Gameplay.OmegaCrew") +
            f"  config: {{fileID: 11400000, guid: {c.config_guid}, type: 2}}\n"
            f"  flight: {{fileID: {TRANSFORMER_ID}}}\n")


def transformer(tr: str, c: CrewPaths) -> str:
    """The Stoat's two-stick block as OmegaVesselTransformer: the script and the first member's page flight."""
    tr = tr.replace(f"guid: {TWO_STICK_SCRIPT}, type: 3}}", f"guid: {SCRIPT['OmegaVesselTransformer']}, type: 3}}", 1)
    cruise, turn, roll = c.flight
    tr = S.set_field(tr, "DefaultThrottleScaler", str(cruise))
    tr = S.set_field(tr, "DefaultMinimumSpeed", "0")
    for field, value in (("PitchScaler", turn), ("YawScaler", turn), ("RollScaler", roll)):
        tr = S.set_field(tr, field, str(value))
    return tr


def add_component(docs, go_fid: str, comp_fid: str) -> None:
    go = S.get_doc(docs, go_fid)
    if f"  - component: {{fileID: {comp_fid}}}\n" in go:
        return
    m = re.search(r"  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)", go)
    if not m:
        raise RuntimeError("Stoat.prefab drifted: the root's m_Component list was not found")
    S.set_doc(docs, go_fid, go[:m.end(1)] + f"  - component: {{fileID: {comp_fid}}}\n" + go[m.end(1):])


def build_prefab(donor: str, c: CrewPaths) -> str:
    text = donor
    text = S.replace_once(text, "  m_Name: Stoat\n", f"  m_Name: {c.name}\n", "root GameObject name")
    text = S.replace_once(text, "  _name: Stoat\n", f"  _name: {c.name}\n", "VesselStatus _name")
    text = S.replace_once(text, f"  vesselType: {STOAT_CLASS_ID}\n", f"  vesselType: {c.class_id}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{S.global_object_id_hash(c.prefab_guid, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)

    head, docs = S.split_docs(text)
    # Executors: every Stoat one out, none in (the crew's state is OmegaCrew's).
    ship = S.get_doc(docs, SHIP_ACTIONS_GO)
    for fid in STOAT_EXECUTORS:
        ship = ship.replace(f"  - component: {{fileID: {fid}}}\n", "")
    S.set_doc(docs, SHIP_ACTIONS_GO, ship)
    reg = S.get_doc(docs, REGISTRY_ID)
    reg = re.sub(r"  _executors:\n(?:  - \{fileID: -?\d+\}\n)+", "  _executors: []\n", reg)
    S.set_doc(docs, REGISTRY_ID, reg)
    S.remove_docs(docs, STOAT_EXECUTORS)

    S.set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    S.set_doc(docs, HULL_GO_ID, S.get_doc(docs, HULL_GO_ID).replace("  m_Name: StoatHull\n", f"  m_Name: {c.name}Hull\n"))
    # the crew, on the root
    docs.append([CREW_ID, crew_block(c)])
    add_component(docs, ROOT_GO, CREW_ID)
    return patch_prefab(S.join_docs(head, docs), c)


def patch_prefab(text: str, c: CrewPaths) -> str:
    """Idempotent edits the committed prefab takes too (the clone itself is a spent one-shot): the transformer, the
    bindings, the HUD controller, the crew and the vessel impactor's container are the generator's."""
    text = text.replace(f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {STOAT_IMPACT_CONTAINER}, type: 2}}",
                        f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {IMPACT_CONTAINER_GUID}, type: 2}}")
    text = re.sub(r"  _inputEventShipActions:(?:.*\n)*?(?=  _onButtonPressed:)",
                  lambda _: BINDINGS + "  _resourceEventClassActions: []\n  _touchActionOverrides: []\n  _gamepadActionOverrides: []\n",
                  text, count=1)
    head, docs = S.split_docs(text)
    S.set_doc(docs, TRANSFORMER_ID, transformer(S.get_doc(docs, TRANSFORMER_ID), c))
    S.set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    if S.doc_index(docs, CREW_ID) < 0:
        docs.append([CREW_ID, crew_block(c)])
    else:
        S.set_doc(docs, CREW_ID, crew_block(c))
    add_component(docs, ROOT_GO, CREW_ID)
    return S.join_docs(head, docs)


# ===================================================================================================== assets
CONFIG_KEYS = ("crew", "combineSeconds", "gatherRange", "meterPerCrystal", "meterPerSkim", "regen", "drain", "combinedCrystal",
               "shieldDrain", "combineAt", "finisherAt", "finisherCone", "finisherCost", "splitSeconds", "omegaContactPlough",
               "startMeter", "ploughRadius", "cannonRange", "foldBoost", "formationSpread", "memberCatchUp",
               "mouseAimRadiansPerPixel", "aiAimRange", "aiShieldMass", "aiFinisherMass", "debrisSpeed", "debrisSpeedLimit",
               "finisherExplosions", "cannonExplosions")
SOUNDS = ("combineSound", "splitSound", "finisherSound", "weaponSound", "moveSound")


def config_asset(c: CrewPaths) -> str:
    """SEEDED once. The lab numbers = the page's DEFAULTS = OmegaCrewConfigSO's field initializers."""
    return (S.so_header(SCRIPT["OmegaCrewConfigSO"], f"Omega{c.name}Config") +
            f"  crew: {c.crew_value}\n"
            "  combineSeconds: 1.6\n"
            "  gatherRange: 140\n"
            "  meterPerCrystal: 0.22\n"
            "  meterPerSkim: 0.004\n"
            "  regen: 0.02\n"
            "  drain: 0.05\n"
            "  combinedCrystal: 0.5\n"
            "  shieldDrain: 0.06\n"
            "  combineAt: 0.3\n"
            "  finisherAt: 0.9\n"
            "  finisherCone: 180\n"
            "  finisherCost: 0.7\n"
            "  splitSeconds: 0.5\n"
            "  omegaContactPlough: 1\n"
            "  startMeter: 0.5\n"
            "  ploughRadius: 7\n"
            "  cannonRange: 140\n"
            "  foldBoost: 1.5\n"
            "  formationSpread: 40\n"
            "  memberCatchUp: 1.4\n"
            "  mouseAimRadiansPerPixel: 0.0035\n"
            "  aiAimRange: 120\n"
            "  aiShieldMass: 3\n"
            "  aiFinisherMass: 6\n"
            "  debrisSpeed: 90\n"
            "  debrisSpeedLimit: 120\n"
            f"  finisherExplosions:\n  - {AOE_GRIZZLY_REF}\n"
            f"  cannonExplosions:\n  - {AOE_EXPLOSION_REF}\n" +
            "".join(S.empty_event(s) for s in SOUNDS))


def crystal_effect_asset() -> str:
    return S.so_header(SCRIPT["OmegaCrystalEffectSO"], "OmegaCrystalEffect")


def impact_container_asset() -> str:
    """SEEDED once from the Stoat's vessel container: the Stoat's omni Overclock out (it drives Stoat executors), the
    omega meter's crystal effect in (fork, never share, before changing behaviour: vessel rule 8)."""
    text = open(STOAT_IMPACT_CONTAINER_PATH, encoding="utf-8", newline="").read()
    text = S.replace_once(text, "  m_Name: StoatImpactorDataContainer\n", "  m_Name: OmegaImpactorDataContainer\n", "container name")
    text = S.replace_once(text, f"  - {{fileID: 11400000, guid: {STOAT_OVERCLOCK}, type: 2}}\n",
                          f"  - {{fileID: 11400000, guid: {CRYSTAL_EFFECT_GUID}, type: 2}}\n", "the Stoat container's Overclock")
    return text


def sq(text: str) -> str:
    """A YAML single-quoted scalar: a ': ' or an apostrophe in plain text breaks Unity's reader (2026-10-26: both did)."""
    return "'" + text.replace("'", "''") + "'"


def yaml_errors(path: str, text: str) -> "list[str]":
    """Every document of a Unity YAML file must parse (check_generated_assets.py's [yaml] rule, run here too so this
    generator's own --check catches what it writes)."""
    try:
        import yaml  # noqa: PLC0415
    except ImportError:
        return []
    out = []
    for body in re.split(r"^--- !u!\d+ &-?\d+.*$", text, flags=re.M)[1:]:
        try:
            yaml.safe_load(body)
        except yaml.YAMLError as e:
            out.append(f"{os.path.relpath(path, ROOT)} does not parse as YAML: {str(e).splitlines()[0]}")
    return out


def map_asset(c: CrewPaths) -> str:
    """Four OPEN slots: the page's element proposals (omega-studio.js ELEMENTS) are written down as proposals, not wired,
    because nobody signed them off (vessel skill section 3). Input 0 and an empty UpgradeLabel are the honest state."""
    proposals = ((1, "Weapon power", "weaponPower", "x1 to x1.6", "Overload", "Charge scales the Arms seat's weapon: reach, blast and rate."),
                 (2, "Ward", "shieldDrain", "x1 to x0.4", "Bulwark", "Mass makes the Core shield cheaper to hold."),
                 (3, "Finisher reach", "finisherCone", "x1 to x1.5", "Horizon", "Space lengthens the finisher cone."),
                 (4, "Quick combine", "combineSeconds", "x1 to x0.5", "Instant", "Time shortens the dock and the split."))
    out = (S.so_header(MAP_SCRIPT, c.name).replace("  m_EditorClassIdentifier: \n", "  m_EditorClassIdentifier:\n") +
           f"  vesselClass: {c.class_id}\n  entries:\n")
    for element, name, param, scale, l5, tip in proposals:
        out += (f"  - Element: {element}\n    AbilityLabel: (open design slot)\n"
                "    AbilityDescription: " + sq(f"(open design slot) The Vessel Studio proposes \"{name}\": {tip} This element scales "
                                               f"{param} {scale} at level 10 (OmegaMath.ElementMults). Not wired and not signed off - "
                                               "OMEGA_CREW.md section 7.") + "\n"
                "    Input: 0\n    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n    UpgradeLabel:\n"
                "    UpgradeDescription: " + sq(f"(open design slot) The studio proposes \"{l5}\". Do not invent one without sign-off.") + "\n")
    return out


def class_asset(c: CrewPaths) -> str:
    """The Stoat's card sprites stand in until the crews have models to render their own. SeatCount 3: the card reads
    \"<Crew> . 3 seat\" (SO_Vessel.CardName), as the Orca's does."""
    return (S.so_header(S.SO_VESSEL_SCRIPT, f"SO_Class_{c.name}").replace("  m_EditorClassIdentifier: \n",
                                                                           "  m_EditorClassIdentifier: Assembly-CSharp::SO_Vessel\n") +
            f"  Class: {c.class_id}\n"
            f"  Name: {c.name}\n"
            f"  Description: {sq(c.description)}\n"
            "  CrewNames: []\n"
            "  CrewNameTemplate: '{0} and {1}'\n"
            "  SeatCount: 3\n"
            "  SeatLabelTemplate: '{name} \\u00b7 {seats} seat'\n"
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
            "  gameplayParameter1:\n    LeftHandLabel: Casual\n    RightHandLabel: Challenging\n    Value: 0.8\n"
            "  gameplayParameter2:\n    LeftHandLabel: Relaxing\n    RightHandLabel: Thrilling\n    Value: 0.9\n"
            "  gameplayParameter3:\n    LeftHandLabel: Solo\n    RightHandLabel: Social\n    Value: 0.7\n"
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
        ACTIONS_DIR + ".meta": folder_meta(ACTIONS_FOLDER_GUID),
        DOC + ".meta": text_meta(DOC_GUID),
        CRYSTAL_EFFECT: crystal_effect_asset(), CRYSTAL_EFFECT + ".meta": S.asset_meta(CRYSTAL_EFFECT_GUID),
        IMPACT_CONTAINER + ".meta": S.asset_meta(IMPACT_CONTAINER_GUID),
    }
    for c in PATHS.values():
        out[c.class_asset] = class_asset(c)
        out[c.class_asset + ".meta"] = S.asset_meta(c.class_guid)
        out[c.config + ".meta"] = S.asset_meta(c.config_guid)
        out[c.prefab + ".meta"] = S.prefab_meta().replace(S.PREFAB_GUID, c.prefab_guid)
        out[c.map] = map_asset(c)
        out[c.map + ".meta"] = S.asset_meta(c.map_guid)
    for rel, guid in NEW_SCRIPTS:
        out[os.path.join(SCRIPTS, rel) + ".meta"] = S.script_meta(guid)
    return out


def seeded() -> "dict[str, tuple]":
    out = {IMPACT_CONTAINER: (impact_container_asset, IMPACT_CONTAINER_SCRIPT, "OmegaImpactorDataContainer")}
    for c in PATHS.values():
        out[c.config] = ((lambda cc: (lambda: config_asset(cc)))(c), SCRIPT["OmegaCrewConfigSO"], f"Omega{c.name}Config")
    return out


# ===================================================================================================== registrations
def register_container(text: str, c: CrewPaths) -> str:
    if c.prefab_guid in text:
        return text
    m = re.search(r"  _shipPrefabs:\n((?:  - \{fileID: -?\d+, guid: [0-9a-f]{32}, type: 3\}\n)+)", text)
    if not m:
        raise RuntimeError("Vessel Prefab Container: _shipPrefabs list not found")
    return text[:m.end(1)] + f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {c.prefab_guid}, type: 3}}\n" + text[m.end(1):]


def register_network(text: str, c: CrewPaths) -> str:
    if c.prefab_guid in text:
        return text
    if not text.endswith("\n"):
        text += "\n"
    return text + ("  - Override: 0\n"
                   f"    Prefab: {{fileID: {ROOT_GO}, guid: {c.prefab_guid}, type: 3}}\n"
                   "    SourcePrefabToOverride: {fileID: 0}\n"
                   "    SourceHashToOverride: 0\n"
                   "    OverridingTargetPrefab: {fileID: 0}\n")


def register_class_list(text: str, c: CrewPaths) -> str:
    if c.class_guid in text:
        return text
    m = re.search(r"  VesselList:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    if not m:
        raise RuntimeError("class list: VesselList not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {c.class_guid}, type: 2}}\n" + text[m.end(1):]


def register_fusion(text: str, c: CrewPaths) -> str:
    """Four entries copied from the Stoat's (the crews wear the Stoat's hull)."""
    if re.search(r"^  - vessel: %d\n" % c.class_id, text, re.M):
        return text
    blocks = re.findall(r"(  - vessel: %d\n(?:    .*\n)+)" % STOAT_CLASS_ID, text)
    if len(blocks) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig drifted: {len(blocks)} Stoat entries, expected 4")
    added = "".join(b.replace(f"  - vessel: {STOAT_CLASS_ID}\n", f"  - vessel: {c.class_id}\n", 1) for b in blocks)
    last = text.rindex(blocks[-1]) + len(blocks[-1])
    return text[:last] + added + text[last:]


# ===================================================================================================== checks
def check_crew(files: "dict[str, str]", c: CrewPaths, errors: "list[str]") -> None:
    prefab = files.get(c.prefab)
    if prefab is None:
        errors.append(f"{c.name}.prefab is missing")
        return
    head, docs = S.split_docs(prefab)
    ids = {k for k, _ in docs}
    d = dict(docs)

    def need(cond, msg):
        if not cond:
            errors.append(f"{c.name}: {msg}")

    need(f"  vesselType: {c.class_id}\n" in prefab, f"vesselType is not {c.class_id}")
    need(f"  _name: {c.name}\n" in prefab and f"  m_Name: {c.name}\n" in prefab, f"the prefab is not named {c.name}")
    m = re.search(r"  GlobalObjectIdHash: (\d+)\n", d.get(NETWORK_OBJECT_ID, ""))
    need(m and int(m.group(1)) == S.global_object_id_hash(c.prefab_guid, NETWORK_OBJECT_ID),
         "GlobalObjectIdHash is not this prefab's (a copied hash collides with the donor's, vessel rule 42)")
    for guid in STOAT_SCRIPTS:
        need(guid not in prefab, f"a Stoat-only script survived the clone: {guid}")
    for fid in STOAT_EXECUTORS:
        need(fid not in ids, f"Stoat executor {fid} still present")
    need("  _executors: []\n" in d.get(REGISTRY_ID, ""), "the ActionExecutorRegistry is not empty (the crew has no executor)")
    tr = d.get(TRANSFORMER_ID, "")
    need(f"guid: {SCRIPT['OmegaVesselTransformer']}" in tr, "the transformer is not OmegaVesselTransformer")
    cruise, turn, roll = c.flight
    need(f"  DefaultThrottleScaler: {cruise}\n" in tr, f"DefaultThrottleScaler is not the first member's cruise {cruise}")
    for field, value in (("PitchScaler", turn), ("YawScaler", turn), ("RollScaler", roll)):
        need(f"  {field}: {value}\n" in tr, f"{field} is not the first member's {value}")
    crew = d.get(CREW_ID, "")
    need(f"guid: {SCRIPT['OmegaCrew']}" in crew, "OmegaCrew is not on the prefab")
    need(f"  config: {{fileID: 11400000, guid: {c.config_guid}, type: 2}}\n" in crew, f"OmegaCrew.config is not Omega{c.name}Config (a null config never seats the crew)")
    need(f"  flight: {{fileID: {TRANSFORMER_ID}}}\n" in crew, "OmegaCrew.flight is not the hull's transformer")
    need(f"  - component: {{fileID: {CREW_ID}}}\n" in d.get(ROOT_GO, ""), "OmegaCrew is not on the root's component list")
    hud = d.get(HUD_CONTROLLER_ID, "")
    need(f"guid: {SCRIPT['OmegaHUDController']}" in hud and f"  crew: {{fileID: {CREW_ID}}}\n" in hud,
         "the HUD controller is not OmegaHUDController wired to the crew")
    need(f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {IMPACT_CONTAINER_GUID}, type: 2}}" in prefab
         and STOAT_IMPACT_CONTAINER not in prefab, "the vessel impactor does not read OmegaImpactorDataContainer")
    need("  abilities: []\n" in d.get(AI_PILOT_ID, ""), "the AIPilot runs a blind ability cycle")
    handler = d.get(HANDLER_ID, "")
    need(BINDINGS in handler, "the hull's bindings are not RT -> CrewTriggerRT and LT -> CrewTriggerLT alone")
    need("  _touchActionOverrides: []\n  _gamepadActionOverrides: []\n" in handler,
         "a device override map is not empty (it would shadow the shared LT / RT on that device)")
    dangling = S.dangling_refs(prefab)
    need(not dangling, f"{len(dangling)} dangling local reference(s): {dangling[:5]}")

    cfg = files.get(c.config, "")
    for key in CONFIG_KEYS:
        need(re.search(r"^  %s:" % key, cfg, re.M), f"Omega{c.name}Config has no {key}")
    need(f"  crew: {c.crew_value}\n" in cfg, f"Omega{c.name}Config.crew is not {c.crew_value} (the hull would be another crew)")
    need(AOE_GRIZZLY_REF in cfg, f"Omega{c.name}Config.finisherExplosions is not AOEGrizzlyExplosion.prefab (the finisher would burst into nothing)")
    need(f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {c.prefab_guid}, type: 3}}\n" in files.get(CONTAINER, ""),
         "not registered in the Vessel Prefab Container (by its root TRANSFORM)")
    need(f"    Prefab: {{fileID: {ROOT_GO}, guid: {c.prefab_guid}, type: 3}}\n" in files.get(NETWORK_PREFABS, ""),
         "not registered in DefaultNetworkPrefabs (by its root GAMEOBJECT)")
    for path in CLASS_LISTS:
        need(c.class_guid in files.get(path, ""), f"SO_Class_{c.name} is not in {os.path.basename(path)}")
    need(len(re.findall(r"^  - vessel: %d\n" % c.class_id, files.get(FUSION, ""), re.M)) == 4,
         "CrystalHullFusionConfig does not carry exactly four entries")
    need(re.search(r"\b%s = %d," % (c.name, c.class_id), files.get(ENUM, "")), f"VesselClassType has no {c.name} = {c.class_id}")
    need(f"VesselClassType.{c.name}" in files.get(ROSTER, ""), "ToyVesselRoster.Default does not offer it (vessel rule 40)")
    need("  SeatCount: 3\n" in files.get(c.class_asset, ""), "SO_Class has no SeatCount 3 (the seat ring would see one seat)")


def check(files: "dict[str, str]") -> "list[str]":
    errors: "list[str]" = []
    for c in PATHS.values():
        check_crew(files, c, errors)
    for path, want in owned_files().items():
        if path.endswith(".asset"):
            errors.extend(yaml_errors(path, want))
        have = files.get(path)
        if have != want:
            errors.append(f"{os.path.relpath(path, ROOT)} is {'missing' if have is None else 'drifted'} - run the tool")
    for path, (_, script, name) in seeded().items():
        have = files.get(path) or ""
        if not (f"guid: {script}, type: 3}}" in have and f"  m_Name: {name}\n" in have):
            errors.append(f"{os.path.relpath(path, ROOT)} missing or not a {name}")
    container = files.get(IMPACT_CONTAINER, "")
    crystals = re.search(r"  vesselCrystalEffects:\n((?:  - \{[^}]*\}\n)*)", container)
    if not (crystals and f"guid: {CRYSTAL_EFFECT_GUID}" in crystals.group(1)):
        errors.append("OmegaImpactorDataContainer does not run OmegaCrystalEffect (crystals would not fill the meter)")
    if STOAT_OVERCLOCK in container:
        errors.append("OmegaImpactorDataContainer runs the Stoat's omni Overclock (it drives Stoat executors)")
    for rel, _ in NEW_SCRIPTS:
        if os.path.join(SCRIPTS, rel) not in files:
            errors.append(f"{rel} is missing")
    if DOC not in files:
        errors.append("OMEGA_CREW.md is missing")
    return errors


def read_all() -> "dict[str, str]":
    paths = {CONTAINER, NETWORK_PREFABS, FUSION, *CLASS_LISTS, *owned_files(), *seeded(), ENUM, ROSTER, DOC,
             *(os.path.join(SCRIPTS, rel) for rel, _ in NEW_SCRIPTS), *(c.prefab for c in PATHS.values())}
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
    donor = open(DONOR, encoding="utf-8", newline="").read()
    for c in PATHS.values():
        if c.prefab not in files:
            write(c.prefab, build_prefab(donor, c))
            print(f"cloned Stoat.prefab -> {c.name}.prefab")
        else:
            patched = patch_prefab(files[c.prefab], c)
            if patched != files[c.prefab]:
                write(c.prefab, patched)
                print(f"patched {c.name}.prefab (transformer, bindings, HUD controller, crew, impactor container)")
            else:
                print(f"{c.name}.prefab exists - the clone stands down (spent one-shot)")
    for path, text in owned_files().items():
        if files.get(path) != text:
            write(path, text)
            print("wrote", os.path.relpath(path, ROOT))
    for path, (make, _, _) in seeded().items():
        if path not in files:
            write(path, make())
            print("seeded", os.path.relpath(path, ROOT))
    for path, fn in ((CONTAINER, register_container), (NETWORK_PREFABS, register_network), (FUSION, register_fusion),
                     *((p, register_class_list) for p in CLASS_LISTS)):
        before = open(path, encoding="utf-8", newline="").read()
        after = before
        for c in PATHS.values():
            after = fn(after, c)
        if after != before:
            write(path, after)
            print("registered in", os.path.relpath(path, ROOT))


def self_test() -> int:
    files = read_all()
    base = check(files)
    if base:
        print("self-test needs a clean shipped set first:\n  " + "\n  ".join(base))
        return 1
    c = PATHS["Warden"]
    mutations = {
        "vesselType back to the Stoat's": (c.prefab, lambda t: t.replace(f"  vesselType: {c.class_id}\n", "  vesselType: 15\n")),
        "the donor's hash": (c.prefab, lambda t: re.sub(r"  GlobalObjectIdHash: \d+", "  GlobalObjectIdHash: 1", t, count=1)),
        "a Stoat executor left behind": (c.prefab, lambda t: t.replace("  _executors: []\n", "  _executors:\n  - {fileID: 5137264980012345603}\n", 1)),
        "the Stoat's transformer back": (c.prefab, lambda t: t.replace(SCRIPT["OmegaVesselTransformer"], TWO_STICK_SCRIPT)),
        "the first member's turn rate lost": (c.prefab, lambda t: t.replace("  PitchScaler: 110\n", "  PitchScaler: 48\n")),
        "the crew off the root": (c.prefab, lambda t: t.replace(f"  - component: {{fileID: {CREW_ID}}}\n", "")),
        "the crew's config unwired": (c.prefab, lambda t: t.replace(f"  config: {{fileID: 11400000, guid: {c.config_guid}, type: 2}}", "  config: {fileID: 0}")),
        "a dangling reference": (c.prefab, lambda t: t.replace(f"  flight: {{fileID: {TRANSFORMER_ID}}}", "  flight: {fileID: 12345}")),
        "LT on the RT asset": (c.prefab, lambda t: t.replace(W.CREW_LT_GUID, W.CREW_RT_GUID)),
        "a pad override shadowing RT": (c.prefab, lambda t: t.replace("  _gamepadActionOverrides: []\n", "  _gamepadActionOverrides:\n  - InputEvent: 1\n    ShipActions: []\n", 1)),
        "the Stoat's impactor container back": (c.prefab, lambda t: t.replace(IMPACT_CONTAINER_GUID, STOAT_IMPACT_CONTAINER)),
        "the crystal effect off the container": (IMPACT_CONTAINER, lambda t: t.replace(CRYSTAL_EFFECT_GUID, "0" * 32)),
        "the Stoat Overclock back": (IMPACT_CONTAINER, lambda t: t.replace(CRYSTAL_EFFECT_GUID, STOAT_OVERCLOCK)),
        "unregistered from the container": (CONTAINER, lambda t: t.replace(c.prefab_guid, "0" * 32)),
        "unregistered from the network list": (NETWORK_PREFABS, lambda t: t.replace(c.prefab_guid, "0" * 32)),
        "fusion entries missing": (FUSION, lambda t: t.replace(f"  - vessel: {c.class_id}\n", "  - vessel: 99\n")),
        "roster entry missing": (ROSTER, lambda t: t.replace("VesselClassType.Warden", "")),
        "enum member renumbered": (ENUM, lambda t: t.replace(f"Warden = {c.class_id},", "Warden = 24,")),
        "a map slot invented": (c.map, lambda t: t.replace("Input: 0", "Input: 1", 1)),
        "a config key gone": (c.config, lambda t: re.sub(r"  combineAt: .*\n", "", t)),
        "the wrong crew in the config": (c.config, lambda t: t.replace("  crew: 0\n", "  crew: 2\n")),
        "the finisher's explosion unwired": (c.config, lambda t: t.replace(AOE_GRIZZLY_REF, "{fileID: 0}")),
        "one seat on the card": (c.class_asset, lambda t: t.replace("  SeatCount: 3\n", "  SeatCount: 1\n")),
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
    print(f"author_omega_assets --self-test: {len(mutations) - failed}/{len(mutations)} negative controls fired")
    return 1 if failed else 0


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errors = check(read_all())
    for e in errors:
        print("FAIL:", e)
    print("author_omega_assets: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
