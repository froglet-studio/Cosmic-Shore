#!/usr/bin/env python3
"""Author the Kite (the Sparrow with the Kite & Tail navigator seat) and register it, then prove the shipped set.

    python3 Tools/Build/author_kite_assets.py             # author what is missing, then check
    python3 Tools/Build/author_kite_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_kite_assets.py --self-test # prove every check fires

"Bring it to Unity" means the Toybox's Vessel Changer offers it (Garrett, 2026-10-17: "my workflow is artifact to
vessel selector in toybox"). The changer offers what ToyVesselRoster.Default names AND the Vessel Prefab Container
can spawn, so the navigator - a SEAT on a hull, not a hull - ships as its own hull class, the way the Goby & Pistol
duo does on cece/sharp-tesla-j0kd7z (this tool is that branch's author_goby_assets.py, cloned for the Sparrow).

WHAT IT MAKES (the design: Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md)
    Assets/_Prefabs/Spacevessels/Kite.prefab (+ .meta)   a text clone of Sparrow.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 19` (VesselClassType.Kite)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
          (a disk copy keeps the donor's hash and Netcode then keys two prefabs on one entry)
        - KiteNavigator on the root, wired to KiteNavigatorConfig.asset
        - NO BUTTONS: the Sparrow's ability bindings are emptied (Garrett, 2026-10-18: "the pilot should be a one thumb flyer
          that has no buttons"); the Sparrow's SingleStickVesselTransformer already makes it a one-thumb hull, and lifting
          the thumb is the navigator's Time boost (KiteNavigator)
        - its VesselImpactor on KiteImpactorDataContainer: the Sparrow's container plus KiteOmniCrystalEffect in the omni
          list (an Omni crystal widens the navigator's board), so the Sparrow's own container is untouched
      Everything else is the Sparrow's: the lab flew the Sparrow's numbers, and the hull is the pilot's half.
      Unlike the clone, the no-buttons and container edits are RETROFITS: applied to the committed prefab too, idempotently.
    SO_Class_Kite (the Sparrow's icons and abilities, owned from the start) + its entry in SO_Classlist_All / _Classes;
    one entry each in `Vessel Prefab Container.asset` (the root TRANSFORM) and `DefaultNetworkPrefabs.asset` (the root
    GAMEOBJECT); Resources/ElementalAbilityMaps/Kite.asset (the Sparrow's map, unchanged but for its name and class);
    and four CrystalHullFusionConfig entries (the Sparrow's: the Kite draws the Sparrow's mesh so its bakes apply).
    The toybox roster (ToyVesselRoster.Default) is CODE and is not written here (vessel skill rule 40).

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while Kite.prefab is absent,
and `--check` validates the SHIPPED prefab's invariants rather than diffing it against a fresh clone of a donor that
will move on. To re-author from scratch, delete Kite.prefab and its .meta and run again.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Sparrow.prefab")
PREFAB = os.path.join(VESSELS, "Kite.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
DONOR_CLASS = os.path.join(CLASSES, "SO_Class_Sparrow.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Kite.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
DONOR_MAP = os.path.join(MAPS, "Sparrow.asset")
MAP = os.path.join(MAPS, "Kite.asset")
FUSION = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
SEAT_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "Crew", "KiteNavigator.cs.meta")
CONFIG_META = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "KiteNavigator", "KiteNavigatorConfig.asset.meta")
CONTAINERS = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Effect Containers", "VesselContainers")
DONOR_CONTAINER = os.path.join(CONTAINERS, "SparrowImpactorDataContainer.asset")
KITE_CONTAINER = os.path.join(CONTAINERS, "KiteImpactorDataContainer.asset")
OMNI_EFFECT = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Vessel Crystal Effects", "KiteOmniCrystalEffect.asset")
OMNI_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "ImpactEffects", "EffectsSO", "Vessel Crystal Effects",
                                "KiteOmniCrystalEffectSO.cs.meta")

# 16 is BrittleStar (bleeding-edge) and the Goby (cece/sharp-tesla-j0kd7z), 17 the Angler (cece/zealous-feynman-po8k8d);
# 18 is left for the Goby, which has to move off 16 when it merges. Checked across every origin branch 2026-10-17.
KITE_CLASS_ID = 19
SPARROW_CLASS_ID = 11
PREFAB_GUID = "c81d6c43058343b796fee1128ce6ccda"
CLASS_GUID = "db96e34a6c3b4cb1b498f0529f765db1"
MAP_GUID = "007a16964bc0490a937e8384d4e27eec"
DONOR_CONTAINER_GUID = "bd2b4a654fb302e47a23cf303280df2d"
KITE_CONTAINER_GUID = "dac5775f7c7b4048bab9186e40e50977"
OMNI_EFFECT_GUID = "a5c175fbce18440abb3570fbb8672a2a"
ROOT_GO = "8279817603024118164"              # the Sparrow's root GameObject
ROOT_TRANSFORM_ID = "4853044355858455827"
NETWORK_OBJECT_ID = "2386735669300085014"    # the NetworkObject component on it
SEAT_ID = "5137264980016000019"              # KiteNavigator on the root


def guid_of(meta_path: str) -> str:
    with open(meta_path, encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
    if not m:
        raise RuntimeError(f"{os.path.relpath(meta_path, ROOT)}: no guid")
    return m.group(1)


# ----------------------------------------------------------------------------- the prefab
def seat_block(script_guid: str, config_guid: str) -> str:
    return (f"--- !u!114 &{SEAT_ID}\n"
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
            f"  config: {{fileID: 11400000, guid: {config_guid}, type: 2}}\n")


def build_clone(donor: str, script_guid: str, config_guid: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Sparrow\n", "  m_Name: Kite\n", "root GameObject name")
    text = replace_once(text, "  _name: Sparrow\n", "  _name: Kite\n", "VesselStatus _name")
    text = replace_once(text, f"  vesselType: {SPARROW_CLASS_ID}\n", f"  vesselType: {KITE_CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)
    # KiteNavigator: on the root's component list (after its last entry) and as a document at the end
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    if not root:
        raise RuntimeError("donor drifted: the root GameObject's m_Component list was not found")
    text = text[:root.end(1)] + f"  - component: {{fileID: {SEAT_ID}}}\n" + text[root.end(1):]
    if not text.endswith("\n"):
        text += "\n"
    return retrofit(text + seat_block(script_guid, config_guid))


BINDINGS = re.compile(r"(  _inputEventShipActions:)\n(?:  [- ] .*\n)+")


def retrofit(text: str) -> str:
    """No buttons, and the Kite's own impactor container. Idempotent: run on the clone and on the committed prefab."""
    text = BINDINGS.sub(lambda m: m.group(1) + " []\n", text, count=1)
    return text.replace(f"guid: {DONOR_CONTAINER_GUID},", f"guid: {KITE_CONTAINER_GUID},")


def container_asset(donor: str) -> str:
    text = replace_once(donor, "  m_Name: SparrowImpactorDataContainer\n", "  m_Name: KiteImpactorDataContainer\n", "container name")
    m = re.search(r"  vesselCrystalEffects:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)*)", text)
    if not m:
        raise RuntimeError("donor drifted: the Sparrow container's vesselCrystalEffects list was not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {OMNI_EFFECT_GUID}, type: 2}}\n" + text[m.end(1):]


def omni_effect_asset(script_guid: str) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: KiteOmniCrystalEffect\n  m_EditorClassIdentifier: \n")


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the class asset
DESCRIPTION = ("  Description: Two seats in one Sparrow. One player flies; the navigator flies a kite on\n"
               "    the ship's own plane with one thumb - its head pops enemy prisms, its tail lassos\n"
               "    motes into blasts, prism clusters, boost and petal steals. D-pad left or right\n"
               "    swaps seats; the AI takes the empty one.\n")


def class_asset(donor_class: str) -> str:
    text = replace_once(donor_class, "  m_Name: SO_Class_Sparrow\n", "  m_Name: SO_Class_Kite\n", "class m_Name")
    text = replace_once(text, f"  Class: {SPARROW_CLASS_ID}\n", f"  Class: {KITE_CLASS_ID}\n", "class Class")
    text = replace_once(text, "  Name: Sparrow\n", "  Name: Kite\n", "class Name")
    text, n = re.subn(r"  Description: .*\n(?:    .*\n)*", lambda m: DESCRIPTION, text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class Description was not found")
    text, n = re.subn(r"  isLocked: \d\n", "  isLocked: 0\n  ownedFromStart: 1\n", text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class isLocked line was not found")
    return text


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


def build_map(donor_map: str) -> str:
    text = replace_once(donor_map, "  m_Name: Sparrow\n", "  m_Name: Kite\n", "map name")
    return replace_once(text, f"  vesselClass: {SPARROW_CLASS_ID}\n", f"  vesselClass: {KITE_CLASS_ID}\n", "map vesselClass")


def fusion_entries(text: str, vessel: int):
    """Every `  - vessel: N` entry block of the fusion config's list, in order."""
    return re.findall(r"  - vessel: %d\n(?:    .*\n)+" % vessel, text)


def register_fusion(text: str) -> str:
    if fusion_entries(text, KITE_CLASS_ID):
        return text
    donor = fusion_entries(text, SPARROW_CLASS_ID)
    if len(donor) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Sparrow entries, found {len(donor)}")
    last = None
    for m in re.finditer(r"  - vessel: \d+\n(?:    .*\n)+", text):
        last = m
    added = "".join(e.replace(f"  - vessel: {SPARROW_CLASS_ID}\n", f"  - vessel: {KITE_CLASS_ID}\n", 1) for e in donor)
    return text[:last.end()] + added + text[last.end():]


# ----------------------------------------------------------------------------- checks
def check_prefab(text: str, script_guid: str, config_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want("  m_Name: Kite\n" in text, "Kite.prefab: the root is not named Kite")
    want("  _name: Kite\n" in text, "Kite.prefab: VesselStatus _name is not Kite")
    want(f"  vesselType: {KITE_CLASS_ID}\n" in text, f"Kite.prefab: vesselType is not {KITE_CLASS_ID}")
    want(f"  vesselType: {SPARROW_CLASS_ID}\n" not in text, "Kite.prefab: still carries the Sparrow's vesselType")
    h = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(h and int(h.group(1)) == global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "Kite.prefab: GlobalObjectIdHash is not this guid's (a copied hash collides with the donor in Netcode)")
    ip = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(ip and ip.group(1) == "0", "Kite.prefab: InScenePlacedSourceGlobalObjectIdHash is not cleared")
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    want(root and f"{{fileID: {SEAT_ID}}}" in root.group(1), "Kite.prefab: KiteNavigator is not on the root's component list")
    seat = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % SEAT_ID, text)
    want(seat and f"guid: {script_guid}" in seat.group(1), "Kite.prefab: the KiteNavigator document is missing or names another script")
    want(seat and f"  config: {{fileID: 11400000, guid: {config_guid}, type: 2}}" in seat.group(1),
         "Kite.prefab: KiteNavigator.config is not KiteNavigatorConfig.asset (a null config never seats the navigator)")
    want("  _inputEventShipActions: []\n" in text, "Kite.prefab: the hull still binds buttons (the pilot is a one-thumb flyer with none)")
    want(f"guid: {KITE_CONTAINER_GUID}," in text and f"guid: {DONOR_CONTAINER_GUID}," not in text,
         "Kite.prefab: the VesselImpactor is not on KiteImpactorDataContainer (an Omni crystal would not widen the board)")
    return errs


def check_container(container: str, effect: str, omni_script_guid: str):
    errs = []
    if f"  - {{fileID: 11400000, guid: {OMNI_EFFECT_GUID}, type: 2}}\n" not in container.split("  vesselElementalCrystalEffects:")[0]:
        errs.append("KiteImpactorDataContainer: KiteOmniCrystalEffect is not in its omni (vesselCrystalEffects) list")
    if f"guid: {omni_script_guid}, type: 3" not in effect:
        errs.append("KiteOmniCrystalEffect.asset: does not name KiteOmniCrystalEffectSO")
    return errs


def check_registrations(container: str, network: str, lists, cls: str, kmap: str, fusion: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in container,
         "Vessel Prefab Container: no Kite entry on its root TRANSFORM (the Vessel Changer offers only what is here)")
    want(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in network,
         "DefaultNetworkPrefabs: no Kite entry on its root GAMEOBJECT (a swap to it cannot spawn)")
    for name, text in lists:
        want(text.count(CLASS_GUID) == 1, f"{name}: SO_Class_Kite listed {text.count(CLASS_GUID)} times, not once")
    want(f"  Class: {KITE_CLASS_ID}\n" in cls and "  ownedFromStart: 1\n" in cls and "  isLocked: 0\n" in cls,
         "SO_Class_Kite: wrong class, locked, or not owned from the start")
    want(f"  vesselClass: {KITE_CLASS_ID}\n" in kmap and "  m_Name: Kite\n" in kmap, "ElementalAbilityMaps/Kite: wrong class or name")
    kite, sparrow = fusion_entries(fusion, KITE_CLASS_ID), fusion_entries(fusion, SPARROW_CLASS_ID)
    want(len(kite) == 4, f"CrystalHullFusionConfig: {len(kite)} Kite entries, not 4 (CrystalHullFusionConfigTests needs one per element)")
    want([e.split("\n", 1)[1] for e in kite] == [e.split("\n", 1)[1] for e in sparrow],
         "CrystalHullFusionConfig: the Kite's entries are not the Sparrow's (it draws the Sparrow's mesh)")
    return errs


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print("wrote", os.path.relpath(path, ROOT))


def check_all():
    script_guid, config_guid = guid_of(SEAT_SCRIPT_META), guid_of(CONFIG_META)
    errs = []
    if not os.path.exists(PREFAB) or not os.path.exists(PREFAB + ".meta"):
        return ["Kite.prefab (+ .meta) is missing: run Tools/Build/author_kite_assets.py"]
    errs += [] if guid_of(PREFAB + ".meta") == PREFAB_GUID else ["Kite.prefab.meta: guid is not PREFAB_GUID"]
    errs += check_prefab(read(PREFAB), script_guid, config_guid)
    for path in (CLASS_ASSET, MAP):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    for path in (KITE_CONTAINER, OMNI_EFFECT):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    errs += check_container(read(KITE_CONTAINER), read(OMNI_EFFECT), guid_of(OMNI_SCRIPT_META))
    errs += check_registrations(read(CONTAINER), read(NETWORK_PREFABS),
                                [(os.path.basename(p), read(p)) for p in CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(FUSION))
    return errs


def author():
    script_guid, config_guid = guid_of(SEAT_SCRIPT_META), guid_of(CONFIG_META)
    if not os.path.exists(PREFAB):   # the spent one-shot stands down once the prefab is committed
        write(PREFAB, build_clone(read(DONOR), script_guid, config_guid))
        write(PREFAB + ".meta", prefab_meta())
    else:                            # the retrofits are not one-shots: they apply to the committed prefab
        old = read(PREFAB); new = retrofit(old)
        if new != old:
            write(PREFAB, new)
    if not os.path.exists(OMNI_EFFECT):
        write(OMNI_EFFECT, omni_effect_asset(guid_of(OMNI_SCRIPT_META))); write(OMNI_EFFECT + ".meta", native_meta(OMNI_EFFECT_GUID))
    if not os.path.exists(KITE_CONTAINER):
        write(KITE_CONTAINER, container_asset(read(DONOR_CONTAINER))); write(KITE_CONTAINER + ".meta", native_meta(KITE_CONTAINER_GUID))
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset(read(DONOR_CLASS))); write(CLASS_ASSET + ".meta", native_meta(CLASS_GUID))
    if not os.path.exists(MAP):
        write(MAP, build_map(read(DONOR_MAP))); write(MAP + ".meta", native_meta(MAP_GUID))
    for path, fn in [(CONTAINER, register_container), (NETWORK_PREFABS, register_network), (FUSION, register_fusion)] + \
                    [(p, register_class_list) for p in CLASS_LISTS]:
        old = read(path); new = fn(old)
        if new != old:
            write(path, new)


def self_test():
    """Every check must fire on a broken copy of the shipped set (negative controls), and pass on it intact."""
    script_guid, config_guid = guid_of(SEAT_SCRIPT_META), guid_of(CONFIG_META)
    good = build_clone(read(DONOR), script_guid, config_guid)
    fails = []
    def expect(errs, n_min, what):
        if not ((len(errs) == 0) if n_min == 0 else (len(errs) >= n_min)):
            fails.append(f"{what}: {errs}")
    expect(check_prefab(good, script_guid, config_guid), 0, "a fresh clone passes")
    expect(check_prefab(good.replace(f"  vesselType: {KITE_CLASS_ID}\n", f"  vesselType: {SPARROW_CLASS_ID}\n"), script_guid, config_guid), 1, "the Sparrow's vesselType")
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>1124080984", good, count=1), script_guid, config_guid), 1, "the donor's hash")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {SEAT_ID}}}\n", ""), script_guid, config_guid), 1, "the seat off the root")
    expect(check_prefab(good.replace(f"guid: {config_guid}, type: 2", "guid: 00000000000000000000000000000000, type: 2"), script_guid, config_guid), 1, "the seat's config unwired")
    expect(check_prefab(good.replace("  _inputEventShipActions: []\n", "  _inputEventShipActions:\n  - InputEvent: 1\n    ShipActions: []\n"), script_guid, config_guid), 1, "a button bound")
    expect(check_prefab(good.replace(KITE_CONTAINER_GUID, DONOR_CONTAINER_GUID), script_guid, config_guid), 1, "the Sparrow's container")
    if retrofit(good) != good:
        fails.append("the retrofit is not idempotent")
    omni_script = guid_of(OMNI_SCRIPT_META)
    kc, oe = container_asset(read(DONOR_CONTAINER)), omni_effect_asset(omni_script)
    expect(check_container(kc, oe, omni_script), 0, "a Kite container passes")
    expect(check_container(read(DONOR_CONTAINER), oe, omni_script), 1, "the Sparrow's container")
    expect(check_container(kc, oe.replace(omni_script, "0" * 32), omni_script), 1, "the effect names another script")
    container, network, fusion = register_container(read(CONTAINER)), register_network(read(NETWORK_PREFABS)), register_fusion(read(FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in CLASS_LISTS]
    cls, kmap = class_asset(read(DONOR_CLASS)), build_map(read(DONOR_MAP))
    expect(check_registrations(container, network, lists, cls, kmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, kmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, kmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, kmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls.replace("  isLocked: 0\n", "  isLocked: 1\n"), kmap, fusion), 1, "a locked class")
    expect(check_registrations(container, network, lists, cls, kmap, fusion.replace(f"  - vessel: {KITE_CLASS_ID}\n", "  - vessel: 99\n", 1)), 1, "a missing fusion entry")
    expect(check_registrations(container, network, lists, cls, read(DONOR_MAP), fusion), 1, "the Sparrow's own map")
    if register_container(container) != container or register_fusion(fusion) != fusion or register_network(network) != network:
        fails.append("registration is not idempotent")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_kite_assets self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errs = check_all()
    for e in errs:
        print("FAIL:", e)
    print("author_kite_assets:", "ok" if not errs else f"{len(errs)} problem(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
