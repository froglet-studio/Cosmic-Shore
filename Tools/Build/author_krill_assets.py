#!/usr/bin/env python3
"""The Krill's VESSEL CREATION TOOL: its prefab, assets, card deck and every registration, then prove the shipped set.

    python3 Tools/Build/author_krill_assets.py             # author what is missing (idempotent), then check
    python3 Tools/Build/author_krill_assets.py --check     # validate the SHIPPED files, write nothing
    python3 Tools/Build/author_krill_assets.py --self-test # prove every check fires (negative controls)

Design: Assets/_Scripts/Controller/Vessel/R_VesselActions/KRILL.md, from Docs/Studios/SWARM_CARD_VESSEL.md and the Krill
Lab (Docs/Studios/KrillLab.html, Docs/Studios/KRILL_LAB.md). The Krill is the passive swarm card vessel: the pilot turns
a viewing plane and picks one card from each hand; the swarm roams the cell on its own.

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/Krill.prefab (+ .meta) - a document-level clone of Angler.prefab (the newest self-owned
    hull with its own FBX model, itself a clone of the Stoat), turned into the Krill:
        - root GameObject / VesselStatus renamed, `vesselType: 21` (VesselClassType.Krill; 20 is the Orca's on
          cece/happy-noether-m54hz6, 18 the Goby's);
        - a FRESH Netcode GlobalObjectIdHash for THIS prefab guid, InScenePlacedSourceGlobalObjectIdHash cleared (rule 42);
        - the "flight": the transformer block on the SAME fileID, its script swapped to KrillVesselTransformer (the stick
          turns the viewing plane; the carrier follows the swarm's centre at speed 0, so it lays no trail);
        - the executor: the Angler's swapped for KrillSwarmExecutor on the SAME fileID (config, turn-end channel,
          combat-hit channel, the hull to hide, the model each member copies, the carrier's two colliders);
        - the bindings: A (Button1) picks the Mass card, B (Button2) Time, X (Button3) Charge, Y (Button4Action) Space;
          the triggers stay unbound;
        - the AI: no blind ability cycle (the executor picks for an AI Krill); the donor's boost policy kept (inert);
        - the HUD: KrillHUDVariant (a copy of AnglerHUDVariant: same internal fileIDs, the view swapped to KrillHUDView,
          the four gauges named for their element, the four icons swapped, the hand's words authored) and
          KrillHUDController;
        - the camera: KrillCameraSettingsSO (the lab's viewSpan 1400: 1200 u back at a 60 degree frame);
        - the hull: KrillHull holds Krill.fbx (Tools/Build/vessel_model/specs/krill.py) in place of the Angler's model,
          and the root's VesselAnimation is KrillAnimation (same fileID);
        - the vessel impactor: KrillImpactorDataContainer, a fork of the Angler's without the Angler's omni tame (an
          omnicrystal deals the Krill a hand instead, in the executor).
    Assets: KrillConfig (seeded), KrillCardDeck (OWNED: generated from Docs/Studios/SwarmCardVessel/cards.json, the wired
    set parsed from the lab's IMPL), the four pick assets, the camera, SO_Class_Krill, the element map, every script's
    .meta, the doc's .meta. Registrations: Vessel Prefab Container (root TRANSFORM), DefaultNetworkPrefabs (root
    GAMEOBJECT), SO_Classlist_All / _Classes, four CrystalHullFusionConfig entries (copied from the Angler's).
    NOT here (code, held by tests): the enum member, ToyVesselRoster.Default, EnumIntegrityTests.
    THE ELEMENT MAP (Assets/Resources/ElementalAbilityMaps/Krill.asset): the four buttons, one per element, each picking
    that element's card (the design's controls). NO level-5 upgrade is authored for any element (vessel skill §3: never
    invent one to fill a slot); the cards' own levels are the Krill's growth.

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md: "a spent one-shot must stand down, not abort").
The prefab and the HUD variant are built ONLY while absent; --check validates the SHIPPED files' invariants. To
re-author from scratch, delete Krill.prefab / KrillHUDVariant.prefab and their .metas and run again.

TUNING IS SEEDED, NOT OWNED: KrillConfig.asset and the camera asset are written once if absent and then only checked for
their script and name. Their numbers belong to the lab (SHIPPED). The card deck is OWNED: cards.json is its source.
"""
import hashlib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import author_stoat_assets as S  # noqa: E402  (document-model helpers + Netcode hash; importing it writes nothing)
import author_angler_assets as AA  # noqa: E402  (the donor's fileID table; importing it writes nothing)
import krill_icon_ids as I  # noqa: E402  (the icon guids; no numpy / Pillow)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
A = lambda *p: os.path.join(ROOT, "Assets", *p)
VESSELS = A("_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Angler.prefab")
PREFAB = os.path.join(VESSELS, "Krill.prefab")
HUD_DIR = A("_Prefabs", "UI Elements", "VesselHUD")
HUD_DONOR = os.path.join(HUD_DIR, "AnglerHUDVariant.prefab")
HUD_VARIANT_PATH = os.path.join(HUD_DIR, "KrillHUDVariant.prefab")
ACTIONS_DIR = A("_SO_Assets", "VesselActions", "Krill")
CONFIG_PATH = os.path.join(ACTIONS_DIR, "KrillConfig.asset")
DECK_PATH = os.path.join(ACTIONS_DIR, "KrillCardDeck.asset")
CONTAINER = A("_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = A("DefaultNetworkPrefabs.asset")
CLASSES = A("_SO_Assets", "Classes")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Krill.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAP = A("Resources", "ElementalAbilityMaps", "Krill.asset")
FUSION = A("Resources", "CrystalHullFusionConfig.asset")
CAMERA = A("_SO_Assets", "Camera", "KrillCameraSettingsSO.asset")
SCRIPTS = A("_Scripts")
DOC = os.path.join(SCRIPTS, "Controller", "Vessel", "R_VesselActions", "KRILL.md")
EFFECTS = A("_SO_Assets", "Effects")
IMPACT_CONTAINER = os.path.join(EFFECTS, "Effect Containers", "VesselContainers", "KrillImpactorDataContainer.asset")
CARDS_JSON = os.path.join(ROOT, "Docs", "Studios", "SwarmCardVessel", "cards.json")
LAB = os.path.join(ROOT, "Docs", "Studios", "KrillLab.html")

CLASS_ID = 21
ANGLER_CLASS_ID = AA.CLASS_ID   # 17


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/Krill/{name}".encode()).hexdigest()


PREFAB_GUID = guid_for("Krill.prefab")
HUD_VARIANT = guid_for("KrillHUDVariant.prefab")
CONFIG = guid_for("KrillConfig.asset")
DECK = guid_for("KrillCardDeck.asset")
CLASS_GUID = guid_for("SO_Class_Krill.asset")
CAMERA_GUID = guid_for("KrillCameraSettingsSO.asset")
ACTIONS_FOLDER_GUID = guid_for("VesselActions/Krill")
DOC_GUID = guid_for("KRILL.md")
IMPACT_CONTAINER_GUID = guid_for("KrillImpactorDataContainer.asset")
MAP_GUID = guid_for("ElementalAbilityMaps/Krill.asset")
MODEL_GUID = guid_for("Krill.fbx")                              # Assets/_Models/Vessel Models/Krill.fbx (086d40c6...)
ANGLER_TAME = "da2993f6ca4f63a7d57038c331346cc2"               # AnglerTameByCrystalEffect.asset (the Angler's omni effect)
COMBAT_HIT_CHANNEL = AA.COMBAT_HIT_CHANNEL
TURN_END_CHANNEL = AA.TURN_END_CHANNEL
MAP_SCRIPT = AA.MAP_SCRIPT

# (asset name, Element value, InputEvents it is bound to). Element: Charge=1, Mass=2, Space=3, Time=4. InputEvents:
# Button1=6 (A), Button2=7 (B), Button3=8 (X), Button4Action=14 (Y). Mass on A and Time on B, the Sparrow's and the
# Angler's layout; Charge on X and Space on Y. The triggers stay unbound.
CONTROLS = (("KrillPickCharge", 1, 8), ("KrillPickMass", 2, 6), ("KrillPickSpace", 3, 14), ("KrillPickTime", 4, 7))
CONTROL_GUID = {name: guid_for(name + ".asset") for name, _, _ in CONTROLS}

# Every script this vessel added: (path under Assets/_Scripts, guid).
NEW_SCRIPTS = tuple((rel, guid_for(os.path.basename(rel))) for rel in (
    "Controller/Vessel/KrillRules.cs",
    "Controller/Vessel/KrillVesselTransformer.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/KrillCardDeckSO.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/KrillConfigSO.cs",
    "Controller/Vessel/R_VesselActions/Data Containers/KrillActionSO.cs",
    "Controller/Vessel/R_VesselActions/Executors/KrillSwarmExecutor.cs",
    "Controller/Animation/KrillAnimation.cs",
    "UI/Controller/KrillHUDController.cs",
    "UI/View/KrillHUDView.cs",
    "Tests/Editor/KrillRulesTests.cs",
))
SCRIPT = {os.path.basename(rel)[:-3]: guid for rel, guid in NEW_SCRIPTS}

# ---- the donor's documents (Angler.prefab), by fileID ----------------------------------------------------------------
ROOT_GO, ROOT_TRANSFORM_ID, NETWORK_OBJECT_ID = AA.ROOT_GO, AA.ROOT_TRANSFORM_ID, AA.NETWORK_OBJECT_ID
TRANSFORMER_ID, AI_PILOT_ID, HANDLER_ID = AA.TRANSFORMER_ID, AA.AI_PILOT_ID, AA.HANDLER_ID
CAMERA_CUSTOMIZER_ID, HUD_CONTROLLER_ID = AA.CAMERA_CUSTOMIZER_ID, AA.HUD_CONTROLLER_ID
REGISTRY_ID, EXECUTOR_ID = AA.REGISTRY_ID, AA.EXECUTOR_ID     # the Krill's executor takes the Angler's fileID
HULL_GO_ID, HULL_TRANSFORM_ID, ANIMATION_ID = AA.HULL_GO_ID, AA.HULL_TRANSFORM_ID, AA.ANIMATION_ID
MODEL_TRANSFORM_ID = AA.MODEL_TRANSFORM_ID                    # Krill.fbx's stripped root Transform under KrillHull
HUD_INSTANCE_ID, HUD_VIEW_ID = AA.HUD_INSTANCE_ID, AA.HUD_VIEW_ID
BODY_COLLIDER, HITBOX_COLLIDER = "5137264980012345632", "5137264980012345636"   # StoatBody / StoatHitbox BoxColliders
SKIM_RING_POLICY = AA.SKIM_RING_POLICY
# Angler-only references that must not survive the clone.
ANGLER_REFS = tuple(AA.SCRIPT.values()) + (AA.MODEL_GUID, AA.HUD_VARIANT, AA.CAMERA_GUID, AA.IMPACT_CONTAINER_GUID,
                                           AA.CONFIG) + tuple(AA.CONTROL_GUID.values())

# The lab's view (KrillLab.html SHIPPED viewSpan 1400): a 60 degree frame 1200 u back spans 2 x 1200 x tan 30 = 1386 u.
VIEW_BACK, VIEW_FOV = 1200, 60
CAMERA_SCRIPT = AA.CAMERA_SCRIPT
SO_VESSEL_SCRIPT = AA.SO_VESSEL_SCRIPT


def _model_card(rel):
    return hashlib.md5(("cosmic-shore-card:" + rel).encode()).hexdigest()


# The card set, rendered from the MODEL by Tools/Build/vessel_model/render_cards.py Krill.
CARD_ACTIVE = _model_card("Assets/_Graphics/CardImages/Krill.png")
CARD_INACTIVE = _model_card("Assets/_Graphics/CardImages/Krill_Inactive.png")
CARD_SILHOUETTE = _model_card("Assets/_Graphics/CardImages/Krill_silhouette.png")
CARD_LARGE = _model_card("Assets/_Graphics/CardImages/Krill_large.png")
CARD_PREVIEW = _model_card("Assets/_Graphics/Design Assests/Hangar/Krill_Hangar_Preview.png")

# The Angler's four icon sprites (charge, mass, space, time) and the Krill's that replace them in the HUD variant.
ICON_SWAP = {AA.I.guid_for("Angler_Command.png"): I.guid_for("Krill_Charge.png"),
             AA.I.guid_for("Angler_Leash.png"): I.guid_for("Krill_Mass.png"),
             AA.I.guid_for("Angler_Lure.png"): I.guid_for("Krill_Space.png"),
             AA.I.guid_for("Angler_Tame.png"): I.guid_for("Krill_Time.png")}


# ===================================================================================================== the cards
def read_cards():
    with open(CARDS_JSON, encoding="utf-8") as fh:
        return json.load(fh)


def wired_ids():
    """The Krill Lab's IMPL: the cards the lab (and so the game) runs."""
    text = open(LAB, encoding="utf-8").read()
    m = re.search(r"const IMPL = \{(.*?)\n\s*\};", text, re.S)
    if not m:
        raise RuntimeError("KrillLab.html: const IMPL not found")
    return re.findall(r"'([a-z]+-[a-z-]+)'", m.group(1))


ELEMENT = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}
STAT_KIND = {"lin": 0, "geo": 1, "pts": 2}


def q(text) -> str:
    """A YAML single-quoted scalar (Unity reads it; '' escapes a quote)."""
    return "'" + str(text or "").replace("'", "''") + "'"


def deck_asset() -> str:
    data = read_cards()
    wired = set(wired_ids())
    out = S.so_header(SCRIPT["KrillCardDeckSO"], "KrillCardDeck") + f"  revision: {data['revision']}\n  cards:\n"
    for c in data["cards"]:
        out += (f"  - id: {c['id']}\n"
                f"    element: {ELEMENT[c['element']]}\n"
                f"    displayName: {q(c['name'])}\n"
                f"    kind: {q(c['kind'])}\n"
                f"    shinesIn: {q(c['objective'])}\n"
                f"    text: {q(c['text'])}\n")
        out += "    stats:\n" if c["stats"] else "    stats: []\n"
        for s in c["stats"]:
            kind = STAT_KIND[s["kind"]]
            out += (f"    - label: {q(s['label'])}\n"
                    f"      kind: {kind}\n"
                    f"      a: {S.num(s.get('a') or 0)}\n"
                    f"      b: {S.num(s.get('b') or 0)}\n"
                    f"      unit: {q(s.get('unit', ''))}\n"
                    f"      decimals: {int(s.get('dec') or 0)}\n")
            steps = s.get("pts") or []
            out += "      steps:\n" + "".join(f"      - {q(p)}\n" for p in steps) if steps else "      steps: []\n"
        ms = c.get("milestones") or []
        out += "    milestones:\n" if ms else "    milestones: []\n"
        for m in ms:
            out += f"    - level: {m['level']}\n      text: {q(m['text'])}\n"
        out += f"    wired: {1 if c['id'] in wired else 0}\n"
    return out


# ===================================================================================================== the prefab
def executor_block() -> str:
    return (S.mb_header(EXECUTOR_ID, AA.SHIP_ACTIONS_GO, SCRIPT["KrillSwarmExecutor"]) +
            f"  config: {{fileID: 11400000, guid: {CONFIG}, type: 2}}\n"
            f"  OnMiniGameTurnEnd: {{fileID: 11400000, guid: {TURN_END_CHANNEL}, type: 2}}\n"
            f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_CHANNEL}, type: 2}}\n"
            f"  hullVisual: {{fileID: {HULL_TRANSFORM_ID}}}\n"
            f"  memberModel: {{fileID: {MODEL_TRANSFORM_ID}}}\n"
            "  carrierColliders:\n"
            f"  - {{fileID: {BODY_COLLIDER}}}\n"
            f"  - {{fileID: {HITBOX_COLLIDER}}}\n")


def bindings() -> str:
    """Shared (every device): the four buttons, each picking one element's card. The triggers are never bound."""
    out = "  _inputEventShipActions:\n"
    for name, _, event in sorted(CONTROLS, key=lambda c: c[2]):
        out += f"  - InputEvent: {event}\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL_GUID[name]}, type: 2}}\n"
    return out + "  _resourceEventClassActions: []\n  _touchActionOverrides: []\n  _gamepadActionOverrides: []\n"


def hud_controller_block() -> str:
    return (S.mb_header(HUD_CONTROLLER_ID, ROOT_GO, SCRIPT["KrillHUDController"], "Assembly-CSharp::CosmicShore.UI.KrillHUDController") +
            f"  baseView: {{fileID: {HUD_VIEW_ID}}}\n"
            "  _iconSetSwitcher: {fileID: 0}\n"
            f"  view: {{fileID: {HUD_VIEW_ID}}}\n"
            f"  krill: {{fileID: {EXECUTOR_ID}}}\n")


def animation_block() -> str:
    """The root's VesselAnimation as KrillAnimation (the SAME fileID, so VesselStatus's reference holds)."""
    return (S.mb_header(ANIMATION_ID, ROOT_GO, SCRIPT["KrillAnimation"]) +
            "  SkinnedMeshRenderer: {fileID: 0}\n"
            "  SaveNewPositions: 1\n"
            "  brakeThreshold: 0.65\n"
            "  lerpAmount: 2\n"
            "  smallLerpAmount: 0.7\n"
            f"  hullVisual: {{fileID: {HULL_TRANSFORM_ID}}}\n")


def hull_blocks() -> str:
    """KrillHull: the Angler generator's hull block naming the Krill's own model (the Stoat's shape, held by S)."""
    text = AA.hull_blocks()
    for old, new in (("  m_Name: AnglerHull\n", "  m_Name: KrillHull\n"), ("      value: Angler\n", "      value: Krill\n"),
                     (AA.MODEL_GUID, MODEL_GUID)):
        assert old in text, old
        text = text.replace(old, new)
    return text


def build_prefab(donor: str) -> str:
    text = donor
    text = S.replace_once(text, "  m_Name: Angler\n", "  m_Name: Krill\n", "root GameObject name")
    text = S.replace_once(text, "  _name: Angler\n", "  _name: Krill\n", "VesselStatus _name")
    text = S.replace_once(text, f"  vesselType: {ANGLER_CLASS_ID}\n", f"  vesselType: {CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)

    head, docs = S.split_docs(text)
    if S.get_doc(docs, EXECUTOR_ID) is None:
        raise RuntimeError("Angler.prefab drifted: the executor document not found")
    S.set_doc(docs, EXECUTOR_ID, executor_block())

    tr = S.get_doc(docs, TRANSFORMER_ID)
    tr = S.replace_once(tr, f"guid: {AA.SCRIPT['AnglerVesselTransformer']}, type: 3}}",
                        f"guid: {SCRIPT['KrillVesselTransformer']}, type: 3}}", "transformer script")
    tr = S.set_field(tr, "DefaultThrottleScaler", "0")
    tr = S.set_field(tr, "DefaultMinimumSpeed", "0")
    tr = S.set_field(tr, "MaxBoostMultiplier", "1")
    S.set_doc(docs, TRANSFORMER_ID, tr)

    S.set_doc(docs, HUD_CONTROLLER_ID, hud_controller_block())
    cam = S.get_doc(docs, CAMERA_CUSTOMIZER_ID)
    cam = re.sub(r"  settings: \{[^}]*\}\n", f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}\n", cam)
    S.set_doc(docs, CAMERA_CUSTOMIZER_ID, cam)
    S.set_doc(docs, ANIMATION_ID, animation_block())

    for fid, chunk in list(docs):
        if f"m_PrefabInstance: {{fileID: {HUD_INSTANCE_ID}}}" in chunk or fid == HUD_INSTANCE_ID:
            chunk = chunk.replace(AA.HUD_VARIANT, HUD_VARIANT)
            chunk = chunk.replace("      value: AnglerHUDVariant\n", "      value: KrillHUDVariant\n")
            chunk = chunk.replace(f"guid: {AA.SCRIPT['AnglerHUDView']}, type: 3}}", f"guid: {SCRIPT['KrillHUDView']}, type: 3}}")
            chunk = chunk.replace("CosmicShore.UI.AnglerHUDView", "CosmicShore.UI.KrillHUDView")
            S.set_doc(docs, fid, chunk)
    text = S.join_docs(head, docs)

    # the hull: the Angler's model documents become the Krill's
    head, docs = S.split_docs(text)
    start, end = S.doc_index(docs, HULL_GO_ID), S.doc_index(docs, MODEL_TRANSFORM_ID)
    if start < 0 or end < start:
        raise RuntimeError("Angler.prefab drifted: the AnglerHull documents not found")
    _, hull = S.split_docs(hull_blocks())
    docs[start:end + 1] = hull
    text = S.join_docs(head, docs)
    return patch_prefab(text)


def patch_prefab(text: str) -> str:
    """Idempotent edits the committed prefab takes too: the impactor container, the bindings and the executor block."""
    text = text.replace(f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {AA.IMPACT_CONTAINER_GUID}, type: 2}}",
                        f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {IMPACT_CONTAINER_GUID}, type: 2}}")
    text = re.sub(r"  _inputEventShipActions:(?:.*\n)*?(?=  _onButtonPressed:)", lambda _: bindings(), text, count=1)
    head, docs = S.split_docs(text)
    if S.get_doc(docs, EXECUTOR_ID) is not None:
        S.set_doc(docs, EXECUTOR_ID, executor_block())
        text = S.join_docs(head, docs)
    return text


# ===================================================================================================== the HUD variant
GAUGES = (("commandGauge", "chargeGauge", "CommandGauge", "ChargeGauge"),
          ("leashGauge", "massGauge", "LeashGauge", "MassGauge"),
          ("lureGauge", "spaceGauge", "LureGauge", "SpaceGauge"),
          ("tameGauge", "timeGauge", "TameGauge", "TimeGauge"))

# The hand's words (KrillHUDView). Authored here as the prefab's starting text; a person edits them on the variant.
VIEW_WORDS = ("  phaseNames:\n  - Omnicrystal\n  - Elemental crystals\n  - Opposing vessels\n  - Opposing mass\n"
              "  phaseTemplate: '{phase}  {seconds}s'\n"
              "  swarmTemplate: '{count} krill'\n"
              "  cardNameTemplate: '{name}'\n"
              "  cardLevelTemplate: 'Lv {level}'\n"
              "  heldTemplate: '{level}'\n"
              "  cardFontSize: 18\n"
              "  headerFontSize: 24\n"
              "  idleIconColor: {r: 1, g: 1, b: 1, a: 0.35}\n")


def build_hud_variant(donor: str) -> str:
    text = donor.replace("      value: AnglerHUDVariant\n", "      value: KrillHUDVariant\n")
    text = S.replace_once(text, f"guid: {AA.SCRIPT['AnglerHUDView']}, type: 3}}", f"guid: {SCRIPT['KrillHUDView']}, type: 3}}", "view script")
    text = text.replace("CosmicShore.UI.AnglerHUDView", "CosmicShore.UI.KrillHUDView")
    for old, new, old_go, new_go in GAUGES:
        text = re.sub(r"  (%s|%s): \{fileID:" % (old, new), f"  {new}: {{fileID:", text, count=1)
        text = S.replace_once(text, f"  m_Name: {old_go}\n", f"  m_Name: {new_go}\n", f"gauge object {old_go}")
    text = re.sub(r"  seedSprite: .*\n", "", text)
    text = re.sub(r"  countFontSize: .*\n", "", text)
    text = S.replace_once(text, "  timeGauge: {fileID:", "  timeGauge: {fileID:", "time gauge")
    text = re.sub(r"(  timeGauge: \{fileID: \d+\}\n)", lambda m: m.group(1) + VIEW_WORDS, text, count=1)
    for old, new in ICON_SWAP.items():
        text = S.replace_once(text, f"guid: {old}, type: 3}}", f"guid: {new}, type: 3}}", f"icon sprite {old}")
    return text


# ===================================================================================================== assets
def config_asset() -> str:
    """SEEDED once. Numbers = KrillConfigSO's field initializers = the lab's SHIPPED."""
    return (S.so_header(SCRIPT["KrillConfigSO"], "KrillConfig") +
            f"  deck: {{fileID: 11400000, guid: {DECK}, type: 2}}\n"
            "  tOmni: 30\n  tElem: 25\n  tVessel: 25\n  tMass: 25\n  goalY: 5\n  goalX: 30\n"
            "  startSize: 24\n  cap: 40\n  minSize: 6\n  cruise: 120\n  turn: 70\n"
            "  lvMass: 1\n  lvTime: 1\n  lvSpace: 1\n  lvCharge: 1\n"
            "  slab: 260\n  viewSpan: 1400\n"
            "  contactHz: 10\n  crystalReach: 28\n  omniReach: 20\n  prismReach: 25\n  baseBite: 0.5\n"
            "  rivalReach: 16\n  nipRate: 0.05\n  petalsPerHit: 0.33333334\n  hitCooldown: 0.5\n  fallbackCellRadius: 900\n"
            "  memberScale: 3\n  viewTurnRate: 90\n  followRate: 3\n")


def impact_container_asset() -> str:
    """SEEDED once from the Angler's vessel container (the shared prism / haptics effects the clone already ran), without
    the Angler's omni tame: an omnicrystal deals the Krill a hand (KrillSwarmExecutor). Fork, never share (vessel rule 8)."""
    text = open(AA.IMPACT_CONTAINER, encoding="utf-8", newline="").read()
    text = S.replace_once(text, "  m_Name: AnglerImpactorDataContainer\n", "  m_Name: KrillImpactorDataContainer\n", "container name")
    return S.replace_once(text, f"  - {{fileID: 11400000, guid: {ANGLER_TAME}, type: 2}}\n", "", "the Angler's omni tame")


def map_asset() -> str:
    """The four buttons (the design's controls, SWARM_CARD_VESSEL.md). No element has a level-5 upgrade: every
    UpgradeLabel stays empty, the honest state of an open design slot (vessel skill §3)."""
    def entry(element, label, desc, inp):
        return (f"  - Element: {element}\n    AbilityLabel: {label}\n    AbilityDescription: {q(desc)}\n    Input: {inp}\n"
                "    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n"
                "    UpgradeLabel: \n    UpgradeDescription: \n")
    common = (" card from the hand. A hand is dealt when the Omnicrystal phase ends, or when the swarm collects an "
              "omnicrystal in another phase; a pick adds the card's level to what you hold of it (up to 15). "
              "R_VesselActions/KRILL.md.")
    return (S.so_header(MAP_SCRIPT, "Krill").replace("  m_EditorClassIdentifier: \n", "  m_EditorClassIdentifier:\n") +
            f"  vesselClass: {CLASS_ID}\n  entries:\n" +
            entry(1, "Pick the Charge card", "X: pick the Charge" + common, 8) +
            entry(2, "Pick the Mass card", "A: pick the Mass" + common, 6) +
            entry(3, "Pick the Space card", "Y: pick the Space" + common, 14) +
            entry(4, "Pick the Time card", "B: pick the Time" + common, 7))


def control_asset(name: str, element: int) -> str:
    return S.so_header(SCRIPT["KrillActionSO"], name) + f"  element: {element}\n"


def camera_asset() -> str:
    """SEEDED once. The lab's view: viewSpan 1400 u across the short side; the carrier turns, the camera rides it."""
    return (S.so_header(CAMERA_SCRIPT, "KrillCameraSettingsSO") +
            "  mode: 0\n"
            f"  followOffset: {{x: 0, y: 0, z: -{VIEW_BACK}}}\n"
            "  lookAheadDistance: 0\n"
            "  lookAheadLift: 0\n"
            "  chaseEaseRate: 12\n"
            f"  framingFieldOfView: {VIEW_FOV}\n"
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
    return (S.so_header(SO_VESSEL_SCRIPT, "SO_Class_Krill").replace("  m_EditorClassIdentifier: \n",
                                                                   "  m_EditorClassIdentifier: Assembly-CSharp::SO_Vessel\n") +
            f"  Class: {CLASS_ID}\n"
            "  Name: Krill\n"
            "  Description: A swarm, not a ship. The krill roam the cell on their own after each objective in turn -\n"
            "    the omnicrystal, elemental crystals, rival vessels, rival mass. You turn the view and pick one card\n"
            "    from every hand; the cards you pick are what your swarm becomes.\n"
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
            "  gameplayParameter1:\n    LeftHandLabel: Casual\n    RightHandLabel: Challenging\n    Value: 0.3\n"
            "  gameplayParameter2:\n    LeftHandLabel: Relaxing\n    RightHandLabel: Thrilling\n    Value: 0.3\n"
            "  gameplayParameter3:\n    LeftHandLabel: Solo\n    RightHandLabel: Social\n    Value: 0.4\n"
            "  isLocked: 0\n"
            "  ownedFromStart: 0\n"
            "  UnlockCost: 100\n")


def owned_files() -> "dict[str, str]":
    out = {
        CLASS_ASSET: class_asset(), CLASS_ASSET + ".meta": S.asset_meta(CLASS_GUID),
        CAMERA + ".meta": S.asset_meta(CAMERA_GUID),
        CONFIG_PATH + ".meta": S.asset_meta(CONFIG),
        DECK_PATH: deck_asset(), DECK_PATH + ".meta": S.asset_meta(DECK),
        ACTIONS_DIR + ".meta": AA.folder_meta(ACTIONS_FOLDER_GUID),
        PREFAB + ".meta": AA.prefab_meta(PREFAB_GUID),
        HUD_VARIANT_PATH + ".meta": AA.prefab_meta(HUD_VARIANT),
        DOC + ".meta": AA.text_meta(DOC_GUID),
        IMPACT_CONTAINER + ".meta": S.asset_meta(IMPACT_CONTAINER_GUID),
        MAP: map_asset(), MAP + ".meta": S.asset_meta(MAP_GUID),
    }
    for name, element, _ in CONTROLS:
        path = os.path.join(ACTIONS_DIR, name + ".asset")
        out[path] = control_asset(name, element)
        out[path + ".meta"] = S.asset_meta(CONTROL_GUID[name])
    for rel, guid in NEW_SCRIPTS:
        out[os.path.join(SCRIPTS, rel) + ".meta"] = S.script_meta(guid)
    return out


SEEDED = {CONFIG_PATH: (config_asset, SCRIPT["KrillConfigSO"], "KrillConfig"),
          CAMERA: (camera_asset, CAMERA_SCRIPT, "KrillCameraSettingsSO"),
          IMPACT_CONTAINER: (impact_container_asset, "fde7d8c2dacf45f99e46799ffeea15e6", "KrillImpactorDataContainer")}


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
    """Four entries copied from the Angler's (no bake of the Krill's own exists yet)."""
    if re.search(r"^  - vessel: %d\n" % CLASS_ID, text, re.M):
        return text
    blocks = re.findall(r"(  - vessel: %d\n(?:    .*\n)+)" % ANGLER_CLASS_ID, text)
    if len(blocks) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig drifted: {len(blocks)} Angler entries, expected 4")
    added = "".join(b.replace(f"  - vessel: {ANGLER_CLASS_ID}\n", f"  - vessel: {CLASS_ID}\n", 1) for b in blocks)
    last = text.rindex(blocks[-1]) + len(blocks[-1])
    return text[:last] + added + text[last:]


# ===================================================================================================== checks
def check(files: "dict[str, str]") -> "list[str]":
    errors = []
    prefab = files.get(PREFAB)
    if prefab is None:
        return ["Krill.prefab is missing"]
    head, docs = S.split_docs(prefab)
    d = dict(docs)

    def need(cond, msg):
        if not cond:
            errors.append(msg)

    need(f"  vesselType: {CLASS_ID}\n" in prefab, "vesselType is not 21 (Krill)")
    need("  _name: Krill\n" in prefab and "  m_Name: Krill\n" in prefab, "the prefab is not named Krill")
    m = re.search(r"  GlobalObjectIdHash: (\d+)\n", d.get(NETWORK_OBJECT_ID, ""))
    need(m and int(m.group(1)) == S.global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "GlobalObjectIdHash is not this prefab's (a copied hash collides with the donor's, vessel rule 42)")
    for guid in ANGLER_REFS:
        need(guid not in prefab, f"an Angler-only reference survived the clone: {guid}")
    need(hull_blocks() in prefab, "KrillHull is not authored as expected (holding the Krill.fbx model)")
    need(animation_block() in prefab, "the root's VesselAnimation is not KrillAnimation")
    need(S.paint_block() in prefab, "VesselCustomization does not repaint the model's Domain slot (GreenAccentVesselMaterial)")
    need(d.get(EXECUTOR_ID) == executor_block(), "KrillSwarmExecutor is missing or not wired (config / channels / hull / model / colliders)")
    need(f"  - {{fileID: {EXECUTOR_ID}}}\n" in d.get(REGISTRY_ID, ""), "KrillSwarmExecutor is not in the ActionExecutorRegistry")
    for fid in (BODY_COLLIDER, HITBOX_COLLIDER, HULL_TRANSFORM_ID, MODEL_TRANSFORM_ID):
        need(fid in d, f"the executor names document {fid}, which the prefab does not have")
    tr = d.get(TRANSFORMER_ID, "")
    need(f"guid: {SCRIPT['KrillVesselTransformer']}" in tr, "the transformer is not KrillVesselTransformer")
    need("  DefaultThrottleScaler: 0\n" in tr and "  DefaultMinimumSpeed: 0\n" in tr,
         "the carrier is not at speed 0 (it would lay a trail and fly off the swarm)")
    need(d.get(HUD_CONTROLLER_ID) == hud_controller_block(), "the HUD controller is not KrillHUDController wired to the executor")
    need(f"guid: {HUD_VARIANT}" in d.get(HUD_INSTANCE_ID, ""), "the HUD instance is not KrillHUDVariant")
    need(f"guid: {CAMERA_GUID}" in d.get(CAMERA_CUSTOMIZER_ID, ""), "the camera is not KrillCameraSettingsSO")
    need(f"vesselImpactorDataContainerSO: {{fileID: 11400000, guid: {IMPACT_CONTAINER_GUID}, type: 2}}" in prefab,
         "the vessel impactor does not read KrillImpactorDataContainer")
    need(ANGLER_TAME not in files.get(IMPACT_CONTAINER, ANGLER_TAME), "KrillImpactorDataContainer still runs the Angler's omni tame")
    need("  abilities: []\n" in d.get(AI_PILOT_ID, ""), "the AIPilot runs a blind ability cycle (the executor picks for an AI Krill)")
    need(f"boostPolicy: {{fileID: 11400000, guid: {SKIM_RING_POLICY}, type: 2}}" in d.get(AI_PILOT_ID, ""),
         "the AIPilot's boost policy is not the donor's (the shared-channel gate wants it wired)")
    handler = d.get(HANDLER_ID, "")
    for name, _, event in CONTROLS:
        need(re.search(r"  - InputEvent: %d\n    ShipActions:\n    - \{fileID: 11400000, guid: %s, type: 2\}" % (event, CONTROL_GUID[name]), handler),
             f"{name} is not bound to InputEvents {event}")
    need(not re.search(r"  - InputEvent: [12]\n", handler), "a trigger is bound to an action")
    dangling = S.dangling_refs(prefab)
    need(not dangling, f"{len(dangling)} dangling local reference(s): {dangling[:5]}")

    hud = files.get(HUD_VARIANT_PATH)
    if hud is None:
        errors.append("KrillHUDVariant.prefab is missing")
    else:
        need(f"guid: {SCRIPT['KrillHUDView']}" in hud and AA.SCRIPT["AnglerHUDView"] not in hud, "the HUD variant's view is not KrillHUDView")
        for _, field, _, _ in GAUGES:
            need(re.search(r"  %s: \{fileID: [1-9]\d*\}" % field, hud), f"HUD view field {field} is not wired")
        for old, new in ICON_SWAP.items():
            need(old not in hud and new in hud, f"HUD icon {new} not swapped in")
        need(VIEW_WORDS in hud, "KrillHUDView's words (phase names, templates) are not authored on the variant")

    for path, want in owned_files().items():
        have = files.get(path)
        need(have == want, f"{os.path.relpath(path, ROOT)} is {'missing' if have is None else 'drifted'} - run the tool")
    for path, (_, script, name) in SEEDED.items():
        have = files.get(path) or ""
        need(f"guid: {script}, type: 3}}" in have and f"  m_Name: {name}\n" in have, f"{os.path.relpath(path, ROOT)} missing or not a {name}")
    need(f"  deck: {{fileID: 11400000, guid: {DECK}, type: 2}}\n" in files.get(CONFIG_PATH, ""), "KrillConfig does not name the card deck")
    need(f"  followOffset: {{x: 0, y: 0, z: -{VIEW_BACK}}}\n" in files.get(CAMERA, ""), f"the camera is not the lab's view (0, 0, -{VIEW_BACK})")
    need("  farClipPlane: 12000\n" in files.get(CAMERA, ""), "the camera does not state the fleet's farClipPlane 12000")
    deck = files.get(DECK_PATH, "")
    need(deck.count("\n  - id: ") == 80, "the card deck does not hold the 80 cards")
    need(deck.count("    wired: 1\n") == len(wired_ids()) == 24, "the deck's wired set is not the lab's 24 (IMPL)")

    need(f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(CONTAINER, ""),
         "not registered in the Vessel Prefab Container (by its root TRANSFORM)")
    need(f"    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n" in files.get(NETWORK_PREFABS, ""),
         "not registered in DefaultNetworkPrefabs (by its root GAMEOBJECT)")
    for path in CLASS_LISTS:
        need(CLASS_GUID in files.get(path, ""), f"SO_Class_Krill is not in {os.path.basename(path)}")
    need(len(re.findall(r"^  - vessel: %d\n" % CLASS_ID, files.get(FUSION, ""), re.M)) == 4,
         "CrystalHullFusionConfig does not carry exactly four Krill entries")
    need(f"guid: {MODEL_GUID}" in files.get(A("_Models", "Vessel Models", "Krill.fbx.meta"), ""),
         "Krill.fbx.meta is missing or does not carry the model guid (vessel_model/build.py krill)")
    for icon in I.ICONS:
        need(f"guid: {I.guid_for(icon)}" in files.get(A("_Graphics", "Icons", "AbilityIcons", "Krill", icon + ".meta"), ""),
             f"{icon} missing (author_krill_icons.py)")
    for rel, guid in (("CardImages/Krill.png", CARD_ACTIVE), ("CardImages/Krill_Inactive.png", CARD_INACTIVE)):
        need(f"guid: {guid}" in files.get(A("_Graphics", rel + ".meta"), ""), f"{rel} missing (vessel_model/render_cards.py Krill)")
    enum = files.get(os.path.join(SCRIPTS, "Data/Enums/VesselClassType.cs"), "")
    need(re.search(r"\bKrill = %d," % CLASS_ID, enum), "VesselClassType has no Krill = 21")
    roster = files.get(os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs"), "")
    need("VesselClassType.Krill" in roster, "ToyVesselRoster.Default does not offer the Krill (vessel rule 40)")
    return errors


def read_all() -> "dict[str, str]":
    paths = {PREFAB, HUD_VARIANT_PATH, CONTAINER, IMPACT_CONTAINER, NETWORK_PREFABS, FUSION, MAP, *CLASS_LISTS, *owned_files(), *SEEDED,
             A("_Models", "Vessel Models", "Krill.fbx.meta"),
             A("_Graphics", "CardImages", "Krill.png.meta"), A("_Graphics", "CardImages", "Krill_Inactive.png.meta"),
             *(A("_Graphics", "Icons", "AbilityIcons", "Krill", icon + ".meta") for icon in I.ICONS),
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
        print("cloned Angler.prefab -> Krill.prefab")
    else:
        print("Krill.prefab exists - the clone stands down (spent one-shot)")
        patched = patch_prefab(files[PREFAB])
        if patched != files[PREFAB]:
            write(PREFAB, patched)
            print("patched Krill.prefab (impactor container, executor wiring, bindings)")
    if HUD_VARIANT_PATH not in files:
        write(HUD_VARIANT_PATH, build_hud_variant(open(HUD_DONOR, encoding="utf-8", newline="").read()))
        print("cloned AnglerHUDVariant.prefab -> KrillHUDVariant.prefab")
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
        "vesselType back to the Angler's": (PREFAB, lambda t: t.replace(f"  vesselType: {CLASS_ID}\n", "  vesselType: 17\n")),
        "executor dropped from the registry": (PREFAB, lambda t: t.replace(f"  _executors:\n  - {{fileID: {EXECUTOR_ID}}}\n", "  _executors: []\n")),
        "a dangling reference": (PREFAB, lambda t: t.replace(f"  krill: {{fileID: {EXECUTOR_ID}}}", "  krill: {fileID: 12345}")),
        "the donor's hash": (PREFAB, lambda t: re.sub(r"  GlobalObjectIdHash: \d+", "  GlobalObjectIdHash: 1", t, count=1)),
        "the Angler's executor back": (PREFAB, lambda t: t.replace(SCRIPT["KrillSwarmExecutor"], AA.SCRIPT["AnglerCommandExecutor"])),
        "the carrier flies": (PREFAB, lambda t: t.replace("  DefaultThrottleScaler: 0\n", "  DefaultThrottleScaler: 40\n", 1)),
        "a trigger bound": (PREFAB, lambda t: t.replace("  - InputEvent: 6\n", "  - InputEvent: 1\n", 1)),
        "unregistered from the container": (CONTAINER, lambda t: t.replace(PREFAB_GUID, "0" * 32)),
        "the Mass pick unbound": (PREFAB, lambda t: t.replace(CONTROL_GUID["KrillPickMass"], "f" * 32)),
        "fusion entries missing": (FUSION, lambda t: t.replace(f"  - vessel: {CLASS_ID}\n", "  - vessel: 99\n")),
        "roster entry missing": (os.path.join(SCRIPTS, "Controller/Toys/ToyVesselRoster.cs"), lambda t: t.replace("VesselClassType.Krill", "")),
        "the element map drifted": (MAP, lambda t: t.replace("Input: 14", "Input: 13")),
        "the combat-hit channel unwired": (PREFAB, lambda t: t.replace(f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_CHANNEL}, type: 2}}", "  onCombatHitLanded: {fileID: 0}")),
        "the member model unwired": (PREFAB, lambda t: t.replace(f"  memberModel: {{fileID: {MODEL_TRANSFORM_ID}}}", "  memberModel: {fileID: 0}")),
        "the camera pulled in": (CAMERA, lambda t: t.replace(f"z: -{VIEW_BACK}", "z: -40")),
        "the Angler's model in the hull": (PREFAB, lambda t: t.replace(MODEL_GUID, AA.MODEL_GUID)),
        "the Angler's tame back on the omni list": (IMPACT_CONTAINER, lambda t: t.replace("  vesselCrystalEffects:\n", f"  vesselCrystalEffects:\n  - {{fileID: 11400000, guid: {ANGLER_TAME}, type: 2}}\n")),
        "a card un-wired in the deck": (DECK_PATH, lambda t: t.replace("    wired: 1\n", "    wired: 0\n", 1)),
        "the HUD words dropped": (HUD_VARIANT_PATH, lambda t: t.replace("  swarmTemplate: '{count} krill'\n", "")),
        "the Angler's HUD view back": (HUD_VARIANT_PATH, lambda t: t.replace(SCRIPT["KrillHUDView"], AA.SCRIPT["AnglerHUDView"])),
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
    print(f"author_krill_assets --self-test: {len(mutations) - failed}/{len(mutations)} negative controls fired")
    return 1 if failed else 0


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errors = check(read_all())
    for e in errors:
        print("FAIL:", e)
    print("author_krill_assets: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
