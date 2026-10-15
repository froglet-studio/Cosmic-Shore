#!/usr/bin/env python3
"""Author the Goby (the Goby & Pistol duo) from the Squirrel and register it, then prove the shipped set.

    python3 Tools/Build/author_goby_assets.py             # author what is missing, then check
    python3 Tools/Build/author_goby_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_goby_assets.py --self-test # prove every check fires

WHAT IT MAKES (the duo brief: .claude/skills/duo-vessel/references/designs/goby-and-pistol.md)
    Assets/_Prefabs/Spacevessels/Goby.prefab (+ .meta)   a text clone of Squirrel.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 16` (VesselClassType.Goby)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
          (a disk copy keeps the donor's hash and Netcode then keys two prefabs on one entry)
        - GobyPistolDuo on the root, wired to GobyPistolConfig.asset (the Pistol: the second half of the duo)
      Everything else is the Squirrel's: the Goby flies on its numbers and its abilities [stated, 2026-10-14];
      the duo's own element row (primer, flares) is an open design question, not filled here.
    SO_Class_Goby (owned from the start, the Squirrel's icons as placeholders) + its entry in
    SO_Classlist_All / _Classes; one entry each in `Vessel Prefab Container.asset` (the root TRANSFORM) and
    `DefaultNetworkPrefabs.asset` (the root GAMEOBJECT); Resources/ElementalAbilityMaps/Goby.asset (the
    Squirrel's map, unchanged but for its name and class); and four CrystalHullFusionConfig entries
    (the Squirrel's, the Goby draws the Squirrel's mesh so its bakes apply).
    The toybox roster (ToyVesselRoster.Default) is CODE and is not written here (vessel skill rule 40).

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while Goby.prefab is
absent, and `--check` validates the SHIPPED prefab's invariants rather than diffing it against a fresh clone
of a donor that will move on. To re-author from scratch, delete Goby.prefab and its .meta and run again.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)

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
DUO_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "Duo", "GobyPistol", "GobyPistolDuo.cs.meta")
CONFIG_META = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "GobyPistol", "GobyPistolConfig.asset.meta")

GOBY_CLASS_ID = 16
SQUIRREL_CLASS_ID = 6
PREFAB_GUID = "982ad3e4c35f4421ae0424bf7bb1ee47"
CLASS_GUID = "b569f215d5ca452d9e4ab28af3ee55ab"
MAP_GUID = "0d9a12bec1e54418b13d4c3d5ad0fb77"
ROOT_GO = "6417075533431866457"              # the hull's root GameObject (same in every Squirrel-derived prefab)
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "1733035045152327888"    # the NetworkObject component on it
DUO_ID = "5137264980016000001"               # GobyPistolDuo on the root
SO_VESSEL_SCRIPT = "0309a5343d820c14abd73b35f6fa50f1"
SQUIRREL_ICON_ACTIVE = "5d8f022f56d65ef4fae2cea4428a990f"
SQUIRREL_ICON_INACTIVE = "c7a15d77edb2723429254cf708d9d20c"
AI_LEVEL_HARD = 3                            # AIDifficulty.Hard


def guid_of(meta_path: str) -> str:
    with open(meta_path, encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
    if not m:
        raise RuntimeError(f"{os.path.relpath(meta_path, ROOT)}: no guid")
    return m.group(1)


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
    return text + duo_block(script_guid, config_guid)


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the class asset
def class_asset() -> str:
    return (
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SO_VESSEL_SCRIPT}, type: 3}}\n"
        "  m_Name: SO_Class_Goby\n  m_EditorClassIdentifier: \n"
        f"  Class: {GOBY_CLASS_ID}\n  Name: Goby\n"
        "  Description: Two craft in one slot. The Goby flies on two thumbs and lays the path;\n"
        "    the Pistol shrimp rides that path side on, as a sidescroller, diving down the\n"
        "    hills, launching into clusters and firing vortex rings. D-pad left or right swaps\n"
        "    which half you fly; the AI flies the other.\n"
        "  PrimaryElement: 3\n  Element: {fileID: 0}\n"
        "  InitialResourceLevels:\n    Mass: 0\n    Charge: 0\n    Space: 0\n    Time: 0\n"
        f"  IconActive: {{fileID: 21300000, guid: {SQUIRREL_ICON_ACTIVE}, type: 3}}\n"
        f"  IconInactive: {{fileID: 21300000, guid: {SQUIRREL_ICON_INACTIVE}, type: 3}}\n"
        "  PreviewImage: {fileID: 0}\n  SquadImage: {fileID: 0}\n  TrailPreviewImage: {fileID: 0}\n"
        "  CardSilohoutteActive: {fileID: 0}\n  CardSilohoutteInactive: {fileID: 0}\n"
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


def build_map(donor_map: str) -> str:
    text = replace_once(donor_map, "  m_Name: Squirrel\n", "  m_Name: Goby\n", "map name")
    return replace_once(text, f"  vesselClass: {SQUIRREL_CLASS_ID}\n", f"  vesselClass: {GOBY_CLASS_ID}\n", "map vesselClass")


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
def check_prefab(text: str, script_guid: str, config_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
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
    want(duo and f"  config: {{fileID: 11400000, guid: {config_guid}, type: 2}}" in duo.group(1),
         "Goby.prefab: GobyPistolDuo.config is not GobyPistolConfig.asset (a null config runs the SHIPPED defaults, not the clone)")
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
    want(f"  vesselClass: {GOBY_CLASS_ID}\n" in gmap and "  m_Name: Goby\n" in gmap, "ElementalAbilityMaps/Goby: wrong class or name")
    goby, squirrel = fusion_entries(fusion, GOBY_CLASS_ID), fusion_entries(fusion, SQUIRREL_CLASS_ID)
    want(len(goby) == 4, f"CrystalHullFusionConfig: {len(goby)} Goby entries, not 4 (CrystalHullFusionConfigTests needs one per element)")
    want([e.split("\n", 1)[1] for e in goby] == [e.split("\n", 1)[1] for e in squirrel],
         "CrystalHullFusionConfig: the Goby's entries are not the Squirrel's (it draws the Squirrel's mesh)")
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
    for path in (CLASS_ASSET, MAP):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    errs += check_registrations(read(CONTAINER), read(NETWORK_PREFABS),
                                [(os.path.basename(p), read(p)) for p in CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(FUSION))
    return errs


def author():
    script_guid, config_guid = guid_of(DUO_SCRIPT_META), guid_of(CONFIG_META)
    if not os.path.exists(PREFAB):   # the spent one-shot stands down once the prefab is committed
        write(PREFAB, build_clone(read(DONOR), script_guid, config_guid))
        write(PREFAB + ".meta", prefab_meta())
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset()); write(CLASS_ASSET + ".meta", native_meta(CLASS_GUID))
    if not os.path.exists(MAP):
        write(MAP, build_map(read(DONOR_MAP))); write(MAP + ".meta", native_meta(MAP_GUID))
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
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>2256742461", good, count=1), script_guid, config_guid), 1, "the donor's hash")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {DUO_ID}}}\n", ""), script_guid, config_guid), 1, "the duo off the root")
    expect(check_prefab(good.replace(f"guid: {config_guid}, type: 2", "guid: 00000000000000000000000000000000, type: 2"), script_guid, config_guid), 1, "the duo's config unwired")
    container, network, fusion = register_container(read(CONTAINER)), register_network(read(NETWORK_PREFABS)), register_fusion(read(FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in CLASS_LISTS]
    cls, gmap = class_asset(), build_map(read(DONOR_MAP))
    expect(check_registrations(container, network, lists, cls, gmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, gmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, gmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, gmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls, gmap, fusion.replace(f"  - vessel: {GOBY_CLASS_ID}\n", "  - vessel: 99\n", 1)), 1, "a missing fusion entry")
    expect(check_registrations(container, network, lists, cls, read(DONOR_MAP), fusion), 1, "the Squirrel's own map")
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
