#!/usr/bin/env python3
"""Author the Tiger Moth (the Butterfly's airframe with the Drop as its Time ability) and register it.

    python3 Tools/Build/author_tigermoth_assets.py             # author what is missing, then check
    python3 Tools/Build/author_tigermoth_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_tigermoth_assets.py --self-test # prove every check fires

The drop won the blind A/B against the Fold (Garrett, 2026-10-28, Docs/Studios/ABTests/README.md "Butterfly fold or
drop v1"): "bring it to unity as the tigermoth". "Bring it to Unity" means the Toybox's Vessel Changer offers it
(Garrett, 2026-10-17), so the Tiger Moth ships as its own hull class beside the Butterfly, which keeps its Fold and
the Waystation race cut for it until its owner retires it. Design: Assets/_Scripts/Controller/Vessel/R_VesselActions/
TIGER_MOTH.md.

WHAT IT MAKES
    Assets/_Prefabs/Spacevessels/TigerMoth.prefab (+ .meta)   a text clone of Butterfly.prefab with:
        - the root GameObject and VesselStatus named TigerMoth, `vesselType: 29` (VesselClassType.TigerMoth)
        - a FRESH Netcode GlobalObjectIdHash for this guid (a disk copy keeps the donor's, and Netcode keys on it alone)
        - the Time binding (InputEvent 2, LT) on TigerMothDropAction instead of ButterflyFoldAction
        - the Fold executor's component turned into TigerMothDropExecutor IN PLACE (same fileID, so the action
          registry's list still names it), its GameObject renamed Drop
        - the HUD controller's Time veil wired to the drop executor (dropExecutor) instead of the fold (foldExecutor)
      Everything else is the Butterfly's: the hull, the wings, the dust, the wake, the camera, the HUD.
    Assets/_SO_Assets/VesselActions/TigerMoth/TigerMothDropAction.asset   the Drop's numbers (TigerMothDropActionSO)
    SO_Class_TigerMoth + its entry in SO_Classlist_All / _Classes; one entry each in `Vessel Prefab Container.asset`
    (the root TRANSFORM) and `DefaultNetworkPrefabs.asset` (the root GAMEOBJECT); Resources/ElementalAbilityMaps/
    TigerMoth.asset (the Butterfly's four cards with the Time card made the Drop).
    No CrystalHullFusionConfig entries: the Butterfly has none either (CrystalHullFusionConfigTests).
    The toybox roster (ToyVesselRoster.Default) and the enum are CODE and are not written here (vessel skill rule 40).
    NetworkManager.prefab's ProtocolVersion is raised by hand in the same change (a new network prefab is a new wire).

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while TigerMoth.prefab is absent,
and `--check` validates the SHIPPED prefab's invariants rather than diffing it against a donor that will move on.

THE BUTTERFLY IS RETIRED (Garrett, 2026-10-29: "retire the butterfly, the tiger moth replaces it"): its prefab, class
asset, element map, the Fold (FoldActionSO, FoldActionExecutor, ButterflyFoldAction.asset) and its registrations are
deleted, and its four modes fly the Tiger Moth. The donor files are gone, so the clone stands down for good (it could
only ever run with the donor present); `--check` also holds the retirement: no Butterfly in any registration, and no
Fold anywhere on the hull.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Butterfly.prefab")
PREFAB = os.path.join(VESSELS, "TigerMoth.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
DONOR_CLASS = os.path.join(CLASSES, "SO_Class_Butterfly.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_TigerMoth.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
DONOR_MAP = os.path.join(MAPS, "Butterfly.asset")
MAP = os.path.join(MAPS, "TigerMoth.asset")
ACTIONS = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions")
ACTION_DIR = os.path.join(ACTIONS, "TigerMoth")
ACTION = os.path.join(ACTION_DIR, "TigerMothDropAction.asset")
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "R_VesselActions")
EXECUTOR_META = os.path.join(SCRIPTS, "Executors", "TigerMothDropExecutor.cs.meta")
ACTION_SO_META = os.path.join(SCRIPTS, "Data Containers", "TigerMothDropActionSO.cs.meta")
# The retired Fold's guids (FoldActionExecutor.cs, ButterflyFoldAction.asset) and the retired Butterfly's (Butterfly.prefab,
# SO_Class_Butterfly.asset): kept so --check can prove none of them is referenced any more.
FOLD_EXECUTOR_GUID = "235fa4dc588e4721a2a26a1ed8d3c413"
FOLD_ACTION_GUID = "f367e6037b96cf54e82543964072bfd3"
BUTTERFLY_PREFAB_GUID = "aa59576a85f7b4746a63849ba535c74d"
BUTTERFLY_CLASS_GUID = "fe4abf38579d7f84baf16a532ed4a015"

# 24-27 are claimed on in-flight branches (Flea, Warden, Bulwark, Tempest) and 28 is the Meerkat; 29 was free on every
# origin branch on 2026-10-28.
CLASS_ID = 29
WORMHOLE_MATERIAL_GUID = "97a0b9d33f4289abcfc3b058a3ffb2d3"   # the Fold's mouths' CosmicShore/Wormhole material
TOLL_EVENT_GUID = "45fb43a0cddb5cc3cc4dc448df994152"         # Event_CombatHitStats
DONOR_CLASS_ID = 13
PREFAB_GUID = "bc7a968aa0d04c48b77ada29fb301291"
ACTION_GUID = "a541c39b6f7a46c5be04c2b39275a4c0"
ACTION_DIR_GUID = "4e6fc1ed76ee44ef972d0ae7686e1861"
CLASS_GUID = "d46d96ae184a444faf9a09a2b793a07d"
MAP_GUID = "329c7f0245114c0b8299547ba2e30a3d"

ROOT_GO = "116782616907870029"           # the Butterfly's root GameObject
ROOT_TRANSFORM_ID = "9012526572693406291"
NETWORK_OBJECT_ID = "2912090631757861063"  # the NetworkObject component on it
EXECUTOR_ID = "2588221200834226537"       # FoldActionExecutor on the child "Fold" -> TigerMothDropExecutor
EXECUTOR_GO = "5551892933991280828"
TIME_INPUT_EVENT = 2                      # LeftStickAction: LT on a pad, the Fold's binding


def guid_of(meta_path: str) -> str:
    with open(meta_path, encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
    if not m:
        raise RuntimeError(f"{os.path.relpath(meta_path, ROOT)}: no guid")
    return m.group(1)


def read(path: str) -> str:
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def write(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print(f"  wrote {os.path.relpath(path, ROOT)}")


def asset_meta(guid: str, main_object: str = "11400000") -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: {main_object}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def prefab_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n")


def folder_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def block(text: str, file_id: str) -> str:
    m = re.search(rf"--- !u!\d+ &{file_id}\n.*?(?=\n--- !u!|\Z)", text, re.S)
    if not m:
        raise RuntimeError(f"no document &{file_id}")
    return m.group(0)


# ----------------------------------------------------------------------------- the prefab
def clone_prefab(donor: str) -> str:
    exec_guid, fold_exec_guid = guid_of(EXECUTOR_META), FOLD_EXECUTOR_GUID
    fold_action = FOLD_ACTION_GUID
    t = donor
    t = replace_once(t, "  m_Name: Butterfly\n  m_TagString", "  m_Name: TigerMoth\n  m_TagString", "root name")
    t = replace_once(t, "  _name: Butterfly\n  vesselType: 13\n", f"  _name: TigerMoth\n  vesselType: {CLASS_ID}\n", "VesselStatus")
    t = re.sub(r"(  GlobalObjectIdHash: )\d+\n", rf"\g<1>{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", t, count=1)
    t = replace_once(t, f"  - InputEvent: {TIME_INPUT_EVENT}\n    ShipActions:\n    - {{fileID: 11400000, guid: {fold_action}, type: 2}}\n",
                     f"  - InputEvent: {TIME_INPUT_EVENT}\n    ShipActions:\n    - {{fileID: 11400000, guid: {ACTION_GUID}, type: 2}}\n",
                     "the Time binding")
    old_exec = block(t, EXECUTOR_ID)
    if f"guid: {fold_exec_guid}" not in old_exec:
        raise RuntimeError("donor drifted: the Fold executor is not where it was")
    head = old_exec.split("  m_Script:")[0]
    new_exec = (head +
                f"  m_Script: {{fileID: 11500000, guid: {exec_guid}, type: 3}}\n"
                "  m_Name: \n"
                "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Gameplay.TigerMothDropExecutor\n"
                f"  config: {{fileID: 11400000, guid: {ACTION_GUID}, type: 2}}")
    t = t.replace(old_exec, new_exec, 1)
    exec_go = block(t, EXECUTOR_GO)
    t = t.replace(exec_go, replace_once(exec_go, "  m_Name: Fold\n", "  m_Name: Drop\n", "the Fold GameObject"), 1)
    t = replace_once(t, f"  foldExecutor: {{fileID: {EXECUTOR_ID}}}\n",
                     f"  dropExecutor: {{fileID: {EXECUTOR_ID}}}\n", "the HUD's Time veil")
    return t


# ----------------------------------------------------------------------------- the small assets
def action_asset() -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid_of(ACTION_SO_META)}, type: 3}}\n"
            "  m_Name: TigerMothDropAction\n"
            "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Gameplay.TigerMothDropActionSO\n"
            "  fullCooldownSeconds: 20\n  fullCooldownDistance: 900\n  upgradeDistanceMultiplier: 2\n"
            "  cooldownMultiplierAtFullTime: 0.5\n  maxCooldownMultiplier: 2\n  dropSpeed: 900\n  dropEaseSeconds: 0.12\n"
            "  landSeconds: 0.25\n  cameraEnterSeconds: 0.35\n  gateRadius: 33\n  portalWindowRange: 2500\n"
            "  portalWindowFadeBand: 600\n  portalWindowRenderScale: 0.75\n  panoramaFaceSize: 256\n"
            f"  wormholeMaterial: {{fileID: 2100000, guid: {WORMHOLE_MATERIAL_GUID}, type: 2}}\n"
            "  gateBloomSeconds: 0.45\n  gateSettleSeconds: 1\n  rivalTollPetalsPerElement: 15\n  rivalTollShedSpeed: 25\n"
            f"  tollHitClass: 2\n  onTollHitLanded: {{fileID: 11400000, guid: {TOLL_EVENT_GUID}, type: 2}}\n")


DROP_ENTRY = """  - Element: 4
    AbilityLabel: Drop
    AbilityDescription: 'HOLD. The wings shut and the hull falls belly-first along its own down - the way its dust rains - without turning, at 900 u/s, while the camera swings up over it so the fall reads as flying forward. LET GO and it lands: the hull pitches 90 degrees nose-down into the line it fell on and flies straight on, so a drop is a turn as well as a jump. Longest drop 1800 u. Time''s dial is the RECHARGE (30 s at rest, half at Time 10).'
    Input: 2
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Far Drop
    UpgradeDescription: 'The drop falls twice as far.'
"""


def map_asset(donor_map: str) -> str:
    t = replace_once(donor_map, "  m_Name: Butterfly\n", "  m_Name: TigerMoth\n", "map name")
    t = replace_once(t, f"  vesselClass: {DONOR_CLASS_ID}\n", f"  vesselClass: {CLASS_ID}\n", "map class")
    m = re.search(r"  - Element: 4\n.*?(?=\n  - Element:|\Z)", t, re.S)
    if not m or "AbilityLabel: Fold" not in m.group(0):
        raise RuntimeError("donor map drifted: no Fold on Time")
    return t[:m.start()] + DROP_ENTRY.rstrip("\n") + t[m.end():]


def class_asset(donor_class: str) -> str:
    t = replace_once(donor_class, "  m_Name: SO_Class_Butterfly\n", "  m_Name: SO_Class_TigerMoth\n", "class name")
    t = replace_once(t, f"  Class: {DONOR_CLASS_ID}\n", f"  Class: {CLASS_ID}\n", "class id")
    t = re.sub(r"  Name: Butterfly\n  Description: .*?(?=\n  PrimaryElement:)",
               "  Name: Tiger Moth\n  Description: \"Slow, wide and quiet, like the Butterfly - until a bat comes. "
               "Hold to shut your wings and drop the way your dust falls; let go and you fly on down the line you "
               "fell.\"", t, count=1, flags=re.S)
    t = replace_once(t, "  ownedFromStart: 0\n", "  ownedFromStart: 1\n", "owned from start")
    return t


def add_list_entry(text: str, list_key: str, entry: str) -> str:
    """Append `entry` (a `  - {...}` line) to the YAML list under `list_key`, once."""
    if entry in text:
        return text
    m = re.search(rf"^  {list_key}:\n((?:  - .*\n(?:    .*\n)*)*)", text, re.M)
    if not m:
        raise RuntimeError(f"no list {list_key}")
    return text[:m.end()] + entry + text[m.end():]


def network_entry() -> str:
    return (f"  - Override: 0\n    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n"
            "    SourcePrefabToOverride: {fileID: 0}\n    SourceHashToOverride: 0\n    OverridingTargetPrefab: {fileID: 0}\n")


def list_key_of(text: str, sample_guid_fragment: str = "type: 3}") -> str:
    m = re.search(r"^  (\w+):\n  - \{fileID: -?\d+, guid: [0-9a-f]{32}, type: [23]\}", text, re.M)
    if not m:
        raise RuntimeError("no guid list")
    return m.group(1)


# ----------------------------------------------------------------------------- author + check
def author() -> None:
    if not os.path.exists(PREFAB) and os.path.exists(DONOR):
        write(PREFAB, clone_prefab(read(DONOR)))
        write(PREFAB + ".meta", prefab_meta(PREFAB_GUID))
    if not os.path.exists(ACTION_DIR + ".meta"):
        write(ACTION_DIR + ".meta", folder_meta(ACTION_DIR_GUID))
    if not os.path.exists(ACTION):
        write(ACTION, action_asset())
        write(ACTION + ".meta", asset_meta(ACTION_GUID))
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
    for p in (PREFAB, ACTION, MAP, CLASS_ASSET):
        need(os.path.exists(p) and os.path.exists(p + ".meta"), f"missing {os.path.relpath(p, ROOT)} (+ .meta)")
    if out:
        return out
    pf = read(PREFAB)
    need(guid_of(PREFAB + ".meta") == PREFAB_GUID, "TigerMoth.prefab.meta guid moved")
    need(f"  _name: TigerMoth\n  vesselType: {CLASS_ID}\n" in pf, "VesselStatus is not the Tiger Moth (29)")
    need(f"  GlobalObjectIdHash: {global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n" in pf,
         "GlobalObjectIdHash is not this prefab's (a donor hash keys two prefabs on one Netcode entry)")
    need(f"guid: {FOLD_ACTION_GUID}" not in pf, "the retired Fold's action is still bound")
    need(f"guid: {FOLD_EXECUTOR_GUID}" not in pf, "a retired FoldActionExecutor is still on the hull")
    need("  foldExecutor:" not in pf, "the HUD still carries the retired foldExecutor key")
    need(f"  - InputEvent: {TIME_INPUT_EVENT}\n    ShipActions:\n    - {{fileID: 11400000, guid: {ACTION_GUID}, type: 2}}\n" in pf,
         "LT is not bound to TigerMothDropAction")
    exec_block = block(pf, EXECUTOR_ID)
    need(f"guid: {guid_of(EXECUTOR_META)}" in exec_block and f"config: {{fileID: 11400000, guid: {ACTION_GUID}" in exec_block,
         "the drop executor is not wired to its action")
    need(f"  dropExecutor: {{fileID: {EXECUTOR_ID}}}\n" in pf, "the HUD's Time veil does not read the drop executor")
    local = set(re.findall(r"^--- !u!\d+ &(-?\d+)", pf, re.M))
    dangling = {i for i in re.findall(r"\{fileID: (-?\d+)\}", pf) if i != "0" and i not in local}
    need(not dangling, f"dangling local references: {sorted(dangling)[:5]}")
    act = read(ACTION)
    need(f"guid: {guid_of(ACTION_SO_META)}" in act, "TigerMothDropAction is not a TigerMothDropActionSO")
    need(guid_of(ACTION + ".meta") == ACTION_GUID, "TigerMothDropAction.asset.meta guid moved")
    mp = read(MAP)
    need(f"  vesselClass: {CLASS_ID}\n" in mp and "AbilityLabel: Drop" in mp and "AbilityLabel: Fold" not in mp,
         "the map's Time card is not the Drop on class 29")
    need(f"  Class: {CLASS_ID}\n" in read(CLASS_ASSET), "SO_Class_TigerMoth is not class 29")
    for path in CLASS_LISTS:
        need(f"guid: {CLASS_GUID}" in read(path), f"{os.path.basename(path)} does not list SO_Class_TigerMoth")
    need(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in read(CONTAINER),
         "the Vessel Prefab Container does not list the Tiger Moth's root transform")
    need(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in read(NETWORK_PREFABS),
         "DefaultNetworkPrefabs does not list the Tiger Moth's root GameObject")
    # the retirement: nothing registers the Butterfly any more
    for path in [CONTAINER, NETWORK_PREFABS] + CLASS_LISTS:
        t = read(path)
        need(BUTTERFLY_PREFAB_GUID not in t and BUTTERFLY_CLASS_GUID not in t,
             f"{os.path.basename(path)} still registers the retired Butterfly")
    need(not os.path.exists(DONOR), "Butterfly.prefab is back: the Butterfly is retired (TIGER_MOTH.md)")
    need(f"wormholeMaterial: {{fileID: 2100000, guid: {WORMHOLE_MATERIAL_GUID}" in act, "the drop's wormhole has no material")
    return out


def self_test() -> int:
    """Each planted defect must be caught by the check that guards it."""
    good = read(PREFAB)
    bound = f"guid: {ACTION_GUID}, type: 2}}\n"
    cases = {
        "donor hash": good.replace(f"GlobalObjectIdHash: {global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}",
                                   "GlobalObjectIdHash: 2928199215"),
        "fold bound": good.replace(f"    - {{fileID: 11400000, {bound}", f"    - {{fileID: 11400000, guid: {FOLD_ACTION_GUID}, type: 2}}\n", 1),
        "class id": good.replace(f"vesselType: {CLASS_ID}\n", "vesselType: 13\n"),
        "dangling": good.replace(f"  dropExecutor: {{fileID: {EXECUTOR_ID}}}", "  dropExecutor: {fileID: 123}"),
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
    return failed


def write_quiet(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


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
    print("tiger moth assets: " + ("FAIL" if errs else "ok"))
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main())
