#!/usr/bin/env python3
"""Author the Goby (the Goby & Pistol duo) from the Squirrel and register it, then prove the shipped set.

    python3 Tools/Build/author_goby_assets.py             # author what is missing, then check
    python3 Tools/Build/author_goby_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_goby_assets.py --self-test # prove every check fires

WHAT IT MAKES (the duo brief: .claude/skills/duo-vessel/references/designs/goby-and-pistol.md)
    Assets/_Prefabs/Spacevessels/Goby.prefab (+ .meta)   a text clone of Squirrel.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 18` (VesselClassType.Goby; it was 16 until the BrittleStar took 16)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
          (a disk copy keeps the donor's hash and Netcode then keys two prefabs on one entry)
        - GobyPistolDuo on the root, wired to GobyPistolConfig.asset (the Pistol: the second half of the duo)
        - the model: the Goby's own hull, Assets/_Models/Vessel Models/Goby.fbx (specs/goby.py, /vessel-model), built on
          the Squirrel's rig and names, so every reference the clone makes into the Squirrel's model binds it by a guid
          swap (Unity derives a model's sub-object ids from type + name); the Squirrel's Animator controller drives it
      DefaultThrottleScaler = 160 (the Goby's own number; it never cruises now: the seat pins it, see below);
        - THE GOBY'S OWN KIT, not the Squirrel's (Garrett, 2026-10-22: "it should not have the squirrels abilities or be
          anything like the squirrel. it should be a goby with its own kit that represents a goby and synergizes with
          the pistol and its kit"). Retrofitted onto the committed prefab, idempotently:
            * the Squirrel's active kit is stripped: the tube (boost ring) and drift executors' subtrees and the drift
              audio (strip_squirrel_kit; nothing may dangle);
            * the hull's input bindings are the Goby's two buttons (CrewButtonActionSO): Dart on Button3Action and Lock
              on Button4Action (a pad's X / Y, keys Q / F, the Goby's phone buttons), and on a pad also RT (Dart) and LT
              (Lock); the Squirrel's thumb-lift overrides are gone, so lifting a thumb while aiming fires nothing;
            * Resources/ElementalAbilityMaps/Goby.asset is the Goby & Pistol's own four cards (GOBY_MAP);
            * the HUD is the Goby & Pistol's own, each seat's four cards (author_goby_hud.py: the Squirrel's HUD
              instance and controller swapped for GobyHUDVariant and GobyPistolHUDController).
          NOT stripped (it needs the editor): the Squirrel's skimmer, which still collects crystals and carries the
          Squirrel's passive skimmer effects (team task in Docs/TEAM_TASKS.md).
    Assets/_SO_Assets/VesselActions/Common Vessel Action/CrewButton0.asset, CrewButton1.asset (+ .meta).
    SO_Class_Goby (owned from the start, the Squirrel's icons as placeholders) + its entry in
    SO_Classlist_All / _Classes; one entry each in `Vessel Prefab Container.asset` (the root TRANSFORM) and
    `DefaultNetworkPrefabs.asset` (the root GAMEOBJECT); Resources/ElementalAbilityMaps/Goby.asset (the Goby's
    own map, above); and four CrystalHullFusionConfig entries
    (copied from the Squirrel's: their bakes read STALE against the Goby's own hull, so the game solves them on a worker
    thread until FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions writes the Goby's own; one per element).
    The toybox roster (ToyVesselRoster.Default) is CODE and is not written here (vessel skill rule 40).

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while Goby.prefab is
absent (the model swap alone also applies to a committed prefab that still draws the Squirrel, idempotently), and `--check` validates the SHIPPED prefab's invariants rather than diffing it against a fresh clone
of a donor that will move on. To re-author from scratch, delete Goby.prefab and its .meta and run again.
"""
import hashlib
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)
from studio_to_unity import parse_shipped  # noqa: E402  (the lab's SHIPPED block: the Goby's own cruise)
import author_goby_hud  # noqa: E402  (each seat's own cards: the HUD swap the retrofit applies)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Squirrel.prefab")
PREFAB = os.path.join(VESSELS, "Goby.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Goby.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
DONOR_MAP = os.path.join(MAPS, "Squirrel.asset")
MAP = os.path.join(MAPS, "Goby.asset")
FUSION = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
DUO_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "Crew", "GobyPistol", "GobyPistolDuo.cs.meta")
CONFIG_META = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "GobyPistol", "GobyPistolConfig.asset.meta")
MODELS = os.path.join(ROOT, "Assets", "_Models", "Vessel Models")
SQUIRREL_MODEL_META = os.path.join(MODELS, "SquirrelVessel_CosmicShoresTest1.fbx.meta")
GOBY_MODEL_META = os.path.join(MODELS, "Goby.fbx.meta")

GOBY_CLASS_ID = 18   # 16 is the BrittleStar on bleeding-edge; 18 was left for the Goby (VesselClassType.cs)
SQUIRREL_CLASS_ID = 6
PREFAB_GUID = "982ad3e4c35f4421ae0424bf7bb1ee47"
CLASS_GUID = "b569f215d5ca452d9e4ab28af3ee55ab"
MAP_GUID = "0d9a12bec1e54418b13d4c3d5ad0fb77"
ROOT_GO = "6417075533431866457"              # the hull's root GameObject (same in every Squirrel-derived prefab)
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "1733035045152327888"    # the NetworkObject component on it
DUO_ID = "5137264980016000001"               # GobyPistolDuo on the root
SO_VESSEL_SCRIPT = "0309a5343d820c14abd73b35f6fa50f1"


def _model_card(rel):
    """The guid vessel_model/render_cards.py gives a NEW card PNG: md5("cosmic-shore-card:" + its repo path)."""
    return hashlib.md5(("cosmic-shore-card:" + rel).encode()).hexdigest()


# the card images, rendered from the Goby's own model by Tools/Build/vessel_model/render_cards.py Goby (2026-10-18)
CARD_ICON = _model_card("Assets/_Graphics/CardImages/Goby.png")
CARD_INACTIVE = _model_card("Assets/_Graphics/CardImages/Goby_Inactive.png")
CARD_SILHOUETTE = _model_card("Assets/_Graphics/CardImages/Goby_silhouette.png")
CARD_LARGE = _model_card("Assets/_Graphics/CardImages/Goby_large.png")
CARD_PREVIEW = _model_card("Assets/_Graphics/Design Assests/Hangar/Goby_Hangar_Preview.png")
AI_LEVEL_HARD = 3                            # AIDifficulty.Hard

# The Goby's own buttons (CrewButtonActionSO): one stateless asset per button, sent to the seat on the hull.
CREW_BUTTON_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "Crew", "CrewButtonActionSO.cs.meta")
COMMON_ACTIONS = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "Common Vessel Action")
CREW_BUTTONS = ((os.path.join(COMMON_ACTIONS, "CrewButton0.asset"), "ff32ad613c6545e6b07b792aad55ff3c", "CrewButton0", 0),
                (os.path.join(COMMON_ACTIONS, "CrewButton1.asset"), "a78d39c97374404a9a4e1bd8620d884f", "CrewButton1", 1))
BUTTON3_ACTION, BUTTON4_ACTION = 8, 14       # InputEvents: a pad's X / Y, keys Q / F
MAP_SCRIPT_GUID = "4b3f2a8c8f344355b16a4d4296ae3f98"   # ElementalAbilityMapSO
RIGHT_STICK_ACTION, LEFT_STICK_ACTION = 1, 2 # InputEvents: a pad's RT / LT (and a phone's thumb lifts, left unbound on touch)
SQUIRREL_SUBTREES = ["SquirrelTubeActionExecutor", "DriftTrailActionExecutor"]
SQUIRREL_COMPONENTS = ["DriftAudioController"]

# The Goby's own impact containers: the Squirrel's, minus the Squirrel's kit (Garrett, 2026-10-22). The hull keeps the
# fleet's prism damage, danger debuff and haptics; the skimmer keeps only its haptics and forcefield crackle (it still
# collects crystals: that is the crystal's impactor, not these lists).
CONTAINERS = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Effect Containers")
GOBY_CONTAINERS = [
    # (squirrel container, goby container, goby guid, effects dropped)
    (os.path.join(CONTAINERS, "VesselContainers", "SquirrelImpactorDataContainer.asset"),
     os.path.join(CONTAINERS, "VesselContainers", "GobyImpactorDataContainer.asset"), "93b182e9a1994f0b9afdfd8120ab00ad",
     ["SquirrelVesselHapticsByPrismEffect", "VesselResetBoostPrismEffect", "SquirrelVesselChangeSpeedByPrism",
      "SquirrelVesselExplosionByCrystalEffect", "SquirrelVesselWitherLifeformByCrystalEffect", "SquirrelCrystalMorphByCrystal"]),
    (os.path.join(CONTAINERS, "SkimmerContainers", "SquirrelSkimmerImpactorDataContainer.asset"),
     os.path.join(CONTAINERS, "SkimmerContainers", "GobySkimmerImpactorDataContainer.asset"), "69e574b928414a7f98965dbccb9473d1",
     ["VesselExplosionBySkimmerEffect", "VesselOvertakeBySkimmerEffect", "VesselCombatHitByJoust", "SkimmerBoostPrismEffect",
      "SkimmerStealPrismEffect"]),
]


def asset_guid(name: str) -> str:
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
        if name + ".asset.meta" in files:
            return guid_of(os.path.join(dirpath, name + ".asset.meta"))
    raise RuntimeError(f"no asset named {name}")


def goby_container(squirrel_text: str, drop) -> str:
    """The Squirrel's container without the effects in `drop`, named for the Goby, its omni retirement cleared."""
    guids = {asset_guid(n) for n in drop}
    out = []
    for line in squirrel_text.splitlines(keepends=True):
        m = re.search(r"guid: (\w+)", line)
        if m and m.group(1) in guids:
            if line.startswith("  omniCrystalRetirement:"):
                out.append("  omniCrystalRetirement: {fileID: 0}\n")
            continue
        out.append(line)
    text = "".join(out).replace("m_Name: Squirrel", "m_Name: Goby", 1)
    return re.sub(r"^(  \w+):\n(?=  [a-zA-Z_])", r"\1: []\n", text, flags=re.M)   # an emptied list prints as []


def swap_containers(text: str) -> str:
    """Goby.prefab's references to the Squirrel's containers point at the Goby's. Idempotent."""
    for squirrel, _, goby_guid, _ in GOBY_CONTAINERS:
        text = text.replace(f"guid: {guid_of(squirrel + '.meta')},", f"guid: {goby_guid},")   # YAML may wrap before "type"
    return text


def author_containers():
    for squirrel, goby, guid, drop in GOBY_CONTAINERS:
        want = goby_container(read(squirrel), drop)
        if not os.path.exists(goby) or read(goby) != want:
            write(goby, want)
        if not os.path.exists(goby + ".meta"):
            write(goby + ".meta", native_meta(guid))


def check_containers(prefab_text: str):
    errs = []
    for squirrel, goby, guid, drop in GOBY_CONTAINERS:
        rel = os.path.relpath(goby, ROOT)
        if not os.path.exists(goby) or not os.path.exists(goby + ".meta"):
            errs.append(f"{rel} (+ .meta) is missing: run Tools/Build/author_goby_assets.py"); continue
        if guid_of(goby + ".meta") != guid:
            errs.append(f"{rel}.meta: guid is not the one Goby.prefab names")
        if read(goby) != goby_container(read(squirrel), drop):
            errs.append(f"{rel}: not the Squirrel's container minus the Squirrel's kit (run the tool)")
        if f"guid: {guid_of(squirrel + '.meta')}," in prefab_text:
            errs.append(f"Goby.prefab: still names {os.path.basename(squirrel)} (the Squirrel's skimmer and crystal effects)")
        if f"guid: {guid}," not in prefab_text:
            errs.append(f"Goby.prefab: does not name {os.path.basename(goby)}")
    return errs

# The Goby & Pistol's four cards (Garrett, 2026-10-22). Passive cards (Input 0): what each element scales happens in the
# seat (GobyPistolDuo), read from GobyPistolConfig.asset's ElementalFloats and gated by IsUpgradeActive at level 5.
GOBY_KIT = [
    (1, "Vortex Sting", "A vortex ring from the Pistol's Snap that hits a rival hull takes petals from each of its elements; Charge "
        "raises how many (GobyPistolConfig ringPetals, 1 at rest -> 3 at level 10). While the Goby holds a lock on a rival that "
        "is on the Pistol's screen, the ring flies at it.", "", ""),
    (2, "Dens", "At each perch the Pistol rests and builds a den of prisms and shielded prisms, as many as the distance it travelled "
        "since its last den and Mass make (denMassScale, x1 at rest -> x2.5 at level 10). It can ride back to an old den to add "
        "to it.", "Fortress", "Dens also grow danger prisms and super-shielded prisms."),
    (3, "Undertow", "Mass either side of the Pistol's plane is pulled into it, from further with Space (pullReach, 40 u at rest -> "
        "200 u at level 10). Danger, super-shielded and the pair's own prisms never move.", "Deep Undertow",
        "Shielded prisms are pulled in too."),
    (4, "Quick Jump", "Time shortens the cooldown of the Pistol's jump, the burst of speed that flings it off its ground "
        "(jumpCooldown, 9 s at rest -> 3 s at level 10).", "Phase Dart",
        "The Goby's darts pass through prisms."),
]


def yaml_quote(text: str) -> str:
    return "'" + text.replace("'", "''") + "'"


def goby_map() -> str:
    """Resources/ElementalAbilityMaps/Goby.asset: the Goby & Pistol's own four cards."""
    head = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {MAP_SCRIPT_GUID}, type: 3}}\n"
            f"  m_Name: Goby\n  m_EditorClassIdentifier:\n  vesselClass: {GOBY_CLASS_ID}\n  entries:\n")
    return head + "".join(
        f"  - Element: {e}\n    AbilityLabel: {yaml_quote(label)}\n    AbilityDescription: {yaml_quote(desc)}\n    Input: 0\n"
        f"    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n    UpgradeLabel: {yaml_quote(ul)}\n"
        f"    UpgradeDescription: {yaml_quote(ud)}\n" for e, label, desc, ul, ud in GOBY_KIT)


def guid_of(meta_path: str) -> str:
    with open(meta_path, encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
    if not m:
        raise RuntimeError(f"{os.path.relpath(meta_path, ROOT)}: no guid")
    return m.group(1)


# ----------------------------------------------------------------------------- the seat's buttons
def crew_button_asset(script_guid: str, name: str, k: int) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier: \n  button: {k}\n")


def _binding_block(key: str, pairs) -> str:
    if not pairs:
        return f"  {key}: []\n"
    return f"  {key}:\n" + "".join(
        f"  - InputEvent: {ie}\n    ShipActions:\n    - {{fileID: 11400000, guid: {guid}, type: 2}}\n" for ie, guid in pairs)


DART, LOCK = CREW_BUTTONS[0][1], CREW_BUTTONS[1][1]
# Dart on Button3Action (X / Q), Lock on Button4Action (Y / F); a pad's RT / LT too; nothing on a phone's thumb lifts
GOBY_BINDINGS = {
    "_inputEventShipActions": _binding_block("_inputEventShipActions", [(BUTTON3_ACTION, DART), (BUTTON4_ACTION, LOCK)]),
    "_touchActionOverrides": _binding_block("_touchActionOverrides", []),
    "_gamepadActionOverrides": _binding_block("_gamepadActionOverrides", [(RIGHT_STICK_ACTION, DART), (LEFT_STICK_ACTION, LOCK)]),
}


def _block_re(key: str):
    return re.compile(r"  %s:(?: \[\])?\n(?:  [- ] .*\n)*" % key)


def bind_buttons(text: str) -> str:
    """The hull's three binding lists are the Goby's two buttons. Idempotent."""
    for key, block in GOBY_BINDINGS.items():
        text = _block_re(key).sub(lambda m: block, text, count=1)
    return text


def bindings_of(text: str) -> dict:
    out = {}
    for key in GOBY_BINDINGS:
        m = _block_re(key).search(text)
        out[key] = m.group(0) if m else ""
    return out


def strip_squirrel_kit(text: str) -> str:
    """The Squirrel's active kit off the Goby: the subtrees in SQUIRREL_SUBTREES and the components in SQUIRREL_COMPONENTS,
    every reference to them dropped from lists (the Wrasse generator's strip, generic). Idempotent."""
    from author_wrasse_assets import split_docs, guid_of_script
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
        if name in SQUIRREL_SUBTREES:
            take(go)
    kill = {guid_of_script(n) for n in SQUIRREL_COMPONENTS}
    removed = set(gos) | {transform_of[g] for g in gos if g in transform_of}
    for k, a, d in docs:
        go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", d, re.M)
        if go and go.group(1) in gos:
            removed.add(a)
        sc = re.search(r"^  m_Script: \{fileID: 11500000, guid: (\w+)", d, re.M)
        if k == "114" and sc and sc.group(1) in kill:
            removed.add(a)
    if not removed:
        return text
    out = []
    for k, a, d in docs:
        if a in removed:
            continue
        for r in removed:
            d = re.sub(r"^  - component: \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"^  - \{fileID: %s\}\n" % r, "", d, flags=re.M)
        out.append(d)
    text = head + "".join(out)
    return re.sub(r"^(  (?:m_Children|_executors|m_Component)):\n(?=  [a-zA-Z_<]|--- |\Z)", r"\1: []\n", text, flags=re.M)


def squirrel_kit_left(text: str):
    """What of the Squirrel's active kit is still on the prefab (names), and any local reference that names nothing."""
    from author_wrasse_assets import dangling_local_refs, guid_of_script
    left = [n for n in SQUIRREL_SUBTREES if re.search(r"^  m_Name: %s$" % n, text, re.M)]
    left += [n for n in SQUIRREL_COMPONENTS + SQUIRREL_SUBTREES if f"guid: {guid_of_script(n)}, type: 3" in text]
    return sorted(set(left)), dangling_local_refs(text)


def author_crew_buttons():
    script_guid = guid_of(CREW_BUTTON_SCRIPT_META)
    for path, guid, name, k in CREW_BUTTONS:
        if not os.path.exists(path) or read(path) != crew_button_asset(script_guid, name, k):
            write(path, crew_button_asset(script_guid, name, k))
        if not os.path.exists(path + ".meta"):
            write(path + ".meta", native_meta(guid))


def check_crew_buttons():
    errs = []
    script_guid = guid_of(CREW_BUTTON_SCRIPT_META)
    for path, guid, name, k in CREW_BUTTONS:
        rel = os.path.relpath(path, ROOT)
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            errs.append(f"{rel} (+ .meta) is missing: run Tools/Build/author_goby_assets.py")
            continue
        if guid_of(path + ".meta") != guid:
            errs.append(f"{rel}.meta: guid is not the one Goby.prefab binds")
        if read(path) != crew_button_asset(script_guid, name, k):
            errs.append(f"{rel}: not CrewButtonActionSO button {k}")
    return errs


# ----------------------------------------------------------------------------- the prefab
def duo_block(script_guid: str, config_guid: str) -> str:
    return (f"--- !u!114 &{DUO_ID}\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: \n"
            "  m_EditorClassIdentifier: \n"
            f"  config: {{fileID: 11400000, guid: {config_guid}, type: 2}}\n"
            f"  aiLevel: {AI_LEVEL_HARD}\n"
            "  showSideView: 1\n")


def build_clone(donor: str, script_guid: str, config_guid: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Squirrel\n", "  m_Name: Goby\n", "root GameObject name")
    text = replace_once(text, "  _name: Squirrel\n", "  _name: Goby\n", "VesselStatus _name")
    text = replace_once(text, f"  vesselType: {SQUIRREL_CLASS_ID}\n", f"  vesselType: {GOBY_CLASS_ID}\n", "vesselType")
    # the Goby zips (Garrett, 2026-10-16): its cruise is the lab's gCruise, not the donor's 60. studio_to_unity.py maps the
    # same key to this line, so a re-author and a clone agree and --check reports a hand edit as drift
    text = replace_once(text, "  DefaultThrottleScaler: 60\n", f"  DefaultThrottleScaler: {cruise_text()}\n", "DefaultThrottleScaler")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)
    # GobyPistolDuo: on the root's component list (after its last entry) and as a document at the end
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    if not root:
        raise RuntimeError("donor drifted: the root GameObject's m_Component list was not found")
    text = text[:root.end(1)] + f"  - component: {{fileID: {DUO_ID}}}\n" + text[root.end(1):]
    if not text.endswith("\n"):
        text += "\n"
    return retrofit(swap_model(text)) + duo_block(script_guid, config_guid)


def retrofit(text: str) -> str:
    """The Goby's own kit onto a Squirrel-derived prefab (the strip, then the bindings). Idempotent."""
    return author_goby_hud.swap_hud(swap_containers(bind_buttons(strip_squirrel_kit(text))))


def swap_model(text: str) -> str:
    """Every reference into the Squirrel's model becomes the same reference into the Goby's (same names, so same ids)."""
    return text.replace(f"guid: {guid_of(SQUIRREL_MODEL_META)},", f"guid: {guid_of(GOBY_MODEL_META)},")


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the class asset
# A crew vessel's card names both subvessels (the Wrasse's rule, SO_Vessel.CrewNames / CardName): "Goby and Pistol".
# Name stays "Goby", the vessel's key (the hangar's cloud unlocks are keyed by it).
CREW = "  CrewNames:\n  - Goby\n  - Pistol\n  CrewNameTemplate: '{0} and {1}'\n"


def class_asset() -> str:
    return (
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SO_VESSEL_SCRIPT}, type: 3}}\n"
        "  m_Name: SO_Class_Goby\n  m_EditorClassIdentifier: \n"
        f"  Class: {GOBY_CLASS_ID}\n  Name: Goby\n"
        "  Description: Two seats, one hull, like a goby and the pistol shrimp it shares a burrow\n"
        "    with. One player flies the Goby, the hull, on two thumbs and lays the path; the other\n"
        "    rides that path side on as the Pistol, a sidescroller - diving down the hills,\n"
        "    launching into clusters and firing vortex rings. D-pad left or right swaps seats;\n"
        "    the AI takes the empty one.\n"
        + CREW +
        "  PrimaryElement: 3\n  Element: {fileID: 0}\n"
        "  InitialResourceLevels:\n    Mass: 0\n    Charge: 0\n    Space: 0\n    Time: 0\n"
        f"  IconActive: {{fileID: 21300000, guid: {CARD_ICON}, type: 3}}\n"
        f"  IconInactive: {{fileID: 21300000, guid: {CARD_INACTIVE}, type: 3}}\n"
        f"  PreviewImage: {{fileID: 21300000, guid: {CARD_PREVIEW}, type: 3}}\n"
        f"  SquadImage: {{fileID: 21300000, guid: {CARD_LARGE}, type: 3}}\n  TrailPreviewImage: {{fileID: 0}}\n"
        f"  CardSilohoutteActive: {{fileID: 21300000, guid: {CARD_ICON}, type: 3}}\n"
        f"  CardSilohoutteInactive: {{fileID: 21300000, guid: {CARD_SILHOUETTE}, type: 3}}\n"
        "  Abilities: []\n  Games: []\n  TrainingGames: []\n"
        "  gameplayParameter1:\n    LeftHandLabel: Casual\n    RightHandLabel: Challenging\n    Value: 0.5\n"
        "  gameplayParameter2:\n    LeftHandLabel: Relaxing\n    RightHandLabel: Thrilling\n    Value: 0.6\n"
        "  gameplayParameter3:\n    LeftHandLabel: Solo\n    RightHandLabel: Social\n    Value: 0.8\n"
        "  isLocked: 0\n  ownedFromStart: 1\n  UnlockCost: 0\n")


def native_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\n"
            "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def register_class_list(text: str) -> str:
    if CLASS_GUID in text:
        return text
    m = re.search(r"  VesselList:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    if not m:
        raise RuntimeError("class list: VesselList not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {CLASS_GUID}, type: 2}}\n" + text[m.end(1):]


# ----------------------------------------------------------------------------- registrations
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


def fusion_entries(text: str, vessel: int):
    """Every `  - vessel: N` entry block of the fusion config's list, in order."""
    return re.findall(r"  - vessel: %d\n(?:    .*\n)+" % vessel, text)


def register_fusion(text: str) -> str:
    if fusion_entries(text, GOBY_CLASS_ID):
        return text
    donor = fusion_entries(text, SQUIRREL_CLASS_ID)
    if len(donor) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Squirrel entries, found {len(donor)}")
    last = None
    for m in re.finditer(r"  - vessel: \d+\n(?:    .*\n)+", text):
        last = m
    added = "".join(e.replace(f"  - vessel: {SQUIRREL_CLASS_ID}\n", f"  - vessel: {GOBY_CLASS_ID}\n", 1) for e in donor)
    return text[:last.end()] + added + text[last.end():]


# ----------------------------------------------------------------------------- checks
LAB = os.path.join(ROOT, "Docs", "Studios", "VesselStudio", "goby.html")


def cruise_text() -> str:
    """The lab's SHIPPED gCruise as the prefab writes it (an integer prints without a point, as Unity does)."""
    v = parse_shipped(read(LAB))["gCruise"]
    return str(int(v)) if v == int(v) else repr(v)


def check_prefab(text: str, script_guid: str, config_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"  DefaultThrottleScaler: {cruise_text()}\n" in text,
         f"Goby.prefab: DefaultThrottleScaler is not the lab's gCruise ({cruise_text()}): the Goby flies on its own cruise, not the donor's")
    want("  m_Name: Goby\n" in text, "Goby.prefab: the root is not named Goby")
    want("  _name: Goby\n" in text, "Goby.prefab: VesselStatus _name is not Goby")
    want(f"  vesselType: {GOBY_CLASS_ID}\n" in text, f"Goby.prefab: vesselType is not {GOBY_CLASS_ID}")
    want(f"  vesselType: {SQUIRREL_CLASS_ID}\n" not in text, "Goby.prefab: still carries the Squirrel's vesselType")
    h = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(h and int(h.group(1)) == global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "Goby.prefab: GlobalObjectIdHash is not this guid's (a copied hash collides with the donor in Netcode)")
    ip = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(ip and ip.group(1) == "0", "Goby.prefab: InScenePlacedSourceGlobalObjectIdHash is not cleared")
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    want(root and f"{{fileID: {DUO_ID}}}" in root.group(1), "Goby.prefab: GobyPistolDuo is not on the root's component list")
    duo = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % DUO_ID, text)
    want(duo and f"guid: {script_guid}" in duo.group(1), "Goby.prefab: the GobyPistolDuo document is missing or names another script")
    sq, gb = guid_of(SQUIRREL_MODEL_META), guid_of(GOBY_MODEL_META)
    want(f"guid: {sq}," not in text, "Goby.prefab: still draws the Squirrel's model (run the tool: the Goby has its own hull, Goby.fbx)")
    want(f"m_SourcePrefab: {{fileID: 100100000, guid: {gb}, type: 3}}" in text, "Goby.prefab: the nested model is not Goby.fbx")
    want(duo and f"  config: {{fileID: 11400000, guid: {config_guid}, type: 2}}" in duo.group(1),
         "Goby.prefab: GobyPistolDuo.config is not GobyPistolConfig.asset (a null config runs the SHIPPED defaults, not the clone)")
    want(bindings_of(text) == GOBY_BINDINGS,
         "Goby.prefab: the bindings are not the Goby's: Dart on Button3Action / RT, Lock on Button4Action / LT, nothing on "
         "a phone's thumb lifts (the Squirrel's drift and boost ring would still fire, or the Goby could not dart or lock)")
    left, dangling = squirrel_kit_left(text)
    want(not left, f"Goby.prefab: the Squirrel's kit is still on it: {left}")
    want(not dangling, f"Goby.prefab: local references that name no object: {dangling[:5]}")
    errs += author_goby_hud.check_hud(text)
    return errs


def check_registrations(container: str, network: str, lists, cls: str, gmap: str, fusion: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in container,
         "Vessel Prefab Container: no Goby entry on its root TRANSFORM (the Vessel Changer offers only what is here)")
    want(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in network,
         "DefaultNetworkPrefabs: no Goby entry on its root GAMEOBJECT (a swap to it cannot spawn)")
    for name, text in lists:
        want(text.count(CLASS_GUID) == 1, f"{name}: SO_Class_Goby listed {text.count(CLASS_GUID)} times, not once")
    want(f"  Class: {GOBY_CLASS_ID}\n" in cls and "  ownedFromStart: 1\n" in cls, "SO_Class_Goby: wrong class or not owned from the start")
    want(CREW in cls, "SO_Class_Goby: the card does not name both subvessels (CrewNames Goby, Pistol: SO_Vessel.CardName)")
    want(all(f"guid: {g}, type: 3" in cls for g in (CARD_ICON, CARD_INACTIVE, CARD_SILHOUETTE, CARD_LARGE, CARD_PREVIEW)),
         "SO_Class_Goby: the card images are not the renders of the Goby's own model (vessel_model/render_cards.py Goby)")
    want(gmap == goby_map(), "ElementalAbilityMaps/Goby: not the Goby & Pistol's own four cards (GOBY_KIT; run the tool)")
    goby, squirrel = fusion_entries(fusion, GOBY_CLASS_ID), fusion_entries(fusion, SQUIRREL_CLASS_ID)
    want(len(goby) == 4, f"CrystalHullFusionConfig: {len(goby)} Goby entries, not 4 (CrystalHullFusionConfigTests needs one per element)")
    want(sorted(re.search(r"    element: (\d+)", e).group(1) for e in goby) == sorted(re.search(r"    element: (\d+)", e).group(1) for e in squirrel),
         "CrystalHullFusionConfig: the Goby's entries do not cover the elements the Squirrel's do (one per element)")
    return errs


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print("wrote", os.path.relpath(path, ROOT))


def check_all():
    script_guid, config_guid = guid_of(DUO_SCRIPT_META), guid_of(CONFIG_META)
    errs = []
    if not os.path.exists(PREFAB) or not os.path.exists(PREFAB + ".meta"):
        return ["Goby.prefab (+ .meta) is missing: run Tools/Build/author_goby_assets.py"]
    errs += [] if guid_of(PREFAB + ".meta") == PREFAB_GUID else ["Goby.prefab.meta: guid is not PREFAB_GUID"]
    errs += check_prefab(read(PREFAB), script_guid, config_guid)
    errs += [f"author_goby_hud: {d} drifted (run Tools/Build/author_goby_hud.py)" for d in author_goby_hud.sync(check=True)]
    errs += check_crew_buttons()
    errs += check_containers(read(PREFAB))
    for path in (CLASS_ASSET, MAP):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    errs += check_registrations(read(CONTAINER), read(NETWORK_PREFABS),
                                [(os.path.basename(p), read(p)) for p in CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(FUSION))
    return errs


def author():
    script_guid, config_guid = guid_of(DUO_SCRIPT_META), guid_of(CONFIG_META)
    for rel in author_goby_hud.sync(check=False):   # each seat's icons and the HUD variant, before the prefab names them
        print("wrote", rel)
    if not os.path.exists(PREFAB):   # the spent one-shot stands down once the prefab is committed
        write(PREFAB, build_clone(read(DONOR), script_guid, config_guid))
        write(PREFAB + ".meta", prefab_meta())
    else:                            # but the model swap and the seat's buttons apply to the committed prefab, once
        old = read(PREFAB); new = retrofit(swap_model(old))
        if new != old:
            write(PREFAB, new)
    author_crew_buttons()
    author_containers()
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset()); write(CLASS_ASSET + ".meta", native_meta(CLASS_GUID))
    if not os.path.exists(MAP) or read(MAP) != goby_map():
        write(MAP, goby_map())
        if not os.path.exists(MAP + ".meta"):
            write(MAP + ".meta", native_meta(MAP_GUID))
    for path, fn in [(CONTAINER, register_container), (NETWORK_PREFABS, register_network), (FUSION, register_fusion)] + \
                    [(p, register_class_list) for p in CLASS_LISTS]:
        old = read(path); new = fn(old)
        if new != old:
            write(path, new)


def self_test():
    """Every check must fire on a broken copy of the shipped set (negative controls), and pass on it intact."""
    script_guid, config_guid = guid_of(DUO_SCRIPT_META), guid_of(CONFIG_META)
    good = build_clone(read(DONOR), script_guid, config_guid)
    fails = []
    def expect(errs, n_min, what):
        if not ((len(errs) == 0) if n_min == 0 else (len(errs) >= n_min)):
            fails.append(f"{what}: {errs}")
    expect(check_prefab(good, script_guid, config_guid), 0, "a fresh clone passes")
    expect(check_prefab(good.replace(f"  vesselType: {GOBY_CLASS_ID}\n", f"  vesselType: {SQUIRREL_CLASS_ID}\n"), script_guid, config_guid), 1, "the Squirrel's vesselType")
    expect(check_prefab(good.replace(f"  DefaultThrottleScaler: {cruise_text()}\n", "  DefaultThrottleScaler: 60\n"), script_guid, config_guid), 1, "the donor's cruise")
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>2256742461", good, count=1), script_guid, config_guid), 1, "the donor's hash")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {DUO_ID}}}\n", ""), script_guid, config_guid), 1, "the duo off the root")
    expect(check_prefab(good.replace(f"guid: {config_guid}, type: 2", "guid: 00000000000000000000000000000000, type: 2"), script_guid, config_guid), 1, "the duo's config unwired")
    expect(check_prefab(good.replace(f"guid: {guid_of(GOBY_MODEL_META)},", f"guid: {guid_of(SQUIRREL_MODEL_META)},"), script_guid, config_guid), 1, "the Squirrel's model")
    expect(check_prefab(good.replace(GOBY_BINDINGS["_inputEventShipActions"], "  _inputEventShipActions: []\n"), script_guid, config_guid), 1, "the seat's buttons unbound")
    expect(check_prefab(good.replace(GOBY_BINDINGS["_touchActionOverrides"], "  _touchActionOverrides:\n  - InputEvent: 12\n    ShipActions:\n    - {fileID: 11400000, guid: aa095c07c8cd0374698884d1a44dbdec, type: 2}\n"), script_guid, config_guid), 1, "the Squirrel's drift on a thumb lift")
    expect(check_prefab(bind_buttons(swap_model(read(DONOR))) + duo_block(script_guid, config_guid), script_guid, config_guid), 1, "the Squirrel's executors left on")
    expect(check_prefab(good.replace(author_goby_hud.VARIANT_GUID, author_goby_hud.SQUIRREL_HUD_VARIANT_GUID), script_guid, config_guid), 1, "the Squirrel's HUD")
    if retrofit(good) != good:
        fails.append("retrofit (the HUD swap included) is not idempotent")
    if not check_containers(read(DONOR)):
        fails.append("the containers check did not fire on the Squirrel's own prefab")
    if bind_buttons(good) != good:
        fails.append("bind_buttons is not idempotent")
    container, network, fusion = register_container(read(CONTAINER)), register_network(read(NETWORK_PREFABS)), register_fusion(read(FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in CLASS_LISTS]
    cls, gmap = class_asset(), goby_map()
    expect(check_registrations(container, network, lists, cls, gmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, gmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, gmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, gmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls, gmap, fusion.replace(f"  - vessel: {GOBY_CLASS_ID}\n", "  - vessel: 99\n", 1)), 1, "a missing fusion entry")
    expect(check_registrations(container, network, lists, cls, read(DONOR_MAP), fusion), 1, "the Squirrel's own map")
    expect(check_registrations(container, network, lists, cls.replace(CREW, ""), gmap, fusion), 1, "no crew names on the card")
    expect(check_registrations(container, network, lists, cls.replace(CARD_ICON, "5d8f022f56d65ef4fae2cea4428a990f"), gmap, fusion), 1, "the Squirrel's card icon")
    if register_container(container) != container or register_fusion(fusion) != fusion or register_network(network) != network:
        fails.append("registration is not idempotent")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_goby_assets self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errs = check_all()
    for e in errs:
        print("FAIL:", e)
    print("author_goby_assets:", "ok" if not errs else f"{len(errs)} problem(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
