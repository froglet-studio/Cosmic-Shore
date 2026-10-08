#!/usr/bin/env python3
"""Author the Stoat's prefab from the Squirrel's and register it, then prove the shipped set.

    python3 Tools/Build/author_stoat_assets.py             # author what is missing, then check
    python3 Tools/Build/author_stoat_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_stoat_assets.py --self-test # prove every check fires

WHAT IT MAKES (`R_VesselActions/STOAT.md`)
    Assets/_Prefabs/Spacevessels/Stoat.prefab (+ .meta)   a text clone of Squirrel.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 14` (VesselClassType.Stoat)
        - a FRESH Netcode GlobalObjectIdHash, computed the way NetworkObject.OnValidate does
          (XXHash32 of the GlobalObjectId string) so the editor regenerates the same number, and
          InScenePlacedSourceGlobalObjectIdHash cleared - a disk copy keeps the donor's hash and
          Netcode then keys two prefabs on one entry (Tools/Build/check_network_prefab_hashes.py)
        - the gamepad bindings replaced: LT (InputEvents 2) -> StoatSlingLeftAction,
          RT (1) -> StoatSlingRightAction, X (6) -> StoatHoldAction; touch overrides cleared
          (no touch design yet - the Gibbon's call, recorded in STOAT.md)
        - StoatSlingExecutor + ToggleTranslationModeActionExecutor added on the ShipActions
          object and in the ActionExecutorRegistry, the stop executor wired to the hull's own
          VesselPrismController and the fleet's shared stationaryModeChanged / OnMiniGameTurnEnd
          channels (the same guids the Sparrow carries, which the shared-channel gate checks)
    and one entry each in `Assets/_SO_Assets/Vessel Prefab Container.asset` and
    `Assets/DefaultNetworkPrefabs.asset`, the two lists that make a hull spawnable.

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md, "a spent one-shot must
stand down, not abort"). Squirrel.prefab moves on; re-cloning it would silently re-author the
Stoat from a donor the Stoat has since diverged from. So the clone runs ONLY while Stoat.prefab
is absent, and `--check` validates the SHIPPED prefab's invariants (the list above) rather than
diffing it against a fresh clone. To re-author from scratch, delete Stoat.prefab and its .meta
and run again.
"""
import os
import re
import struct
import sys
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Squirrel.prefab")
PREFAB = os.path.join(VESSELS, "Stoat.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
ACTIONS = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "Stoat")
MAP = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps", "Stoat.asset")

STOAT_CLASS_ID = 14
PREFAB_GUID = "599b396ff054448db5bccc9307dfb4c9"
ROOT_GO = "6417075533431866457"              # the hull's root GameObject (same in every Squirrel-derived prefab)
NETWORK_OBJECT_ID = "1733035045152327888"    # the NetworkObject component on it
SHIP_ACTIONS_GO = "7850473780452578400"
REGISTRY_ID = "9105512684531825410"
SLING_EXECUTOR_ID = "5137264980012345601"
STOP_EXECUTOR_ID = "5137264980012345602"

# Script guids (from the .cs.meta files).
SLING_EXECUTOR_SCRIPT = "cf82f131a8df416cb205ab8055108b82"
STOP_EXECUTOR_SCRIPT = "8c3f54abda8145ecabf7c85253cab9ed"    # ToggleTranslationModeActionExecutor
PRISM_CONTROLLER_SCRIPT = "909fb5cbbca8c4549b5a9df56d837e55"  # VesselPrismController
# Asset guids (from the .asset.meta files).
SLING_CONFIG = "e79a09360967428cb727002bd123383a"
SLING_LEFT = "5c382e0def5b45d7b1230d75e8be082b"
SLING_RIGHT = "012d65baf31c49c496d38975eba8c0be"
HOLD = "9b50ea11259b4b1aa750eea7f1d7f781"
STATIONARY_MODE_CHANGED = "0b48e834efdbe654ca3c7df60370ea3f"
ON_MINIGAME_TURN_END = "498a06d44bde9184f985c938c803b2a1"


# ----------------------------------------------------------------------------- Netcode's hash
def xxhash32(data: bytes, seed: int = 0) -> int:
    """XXHash32 - what Netcode's `string.Hash32()` computes over the UTF-8 GlobalObjectId."""
    p1, p2, p3, p4, p5 = 2654435761, 2246822519, 3266489917, 668265263, 374761393
    m = 0xFFFFFFFF
    rotl = lambda x, r: ((x << r) | (x >> (32 - r))) & m
    n, i = len(data), 0
    if n >= 16:
        v = [(seed + p1 + p2) & m, (seed + p2) & m, seed & m, (seed - p1) & m]
        while i <= n - 16:
            for k in range(4):
                lane = struct.unpack_from("<I", data, i)[0]
                i += 4
                v[k] = (rotl((v[k] + lane * p2) & m, 13) * p1) & m
        h = (rotl(v[0], 1) + rotl(v[1], 7) + rotl(v[2], 12) + rotl(v[3], 18)) & m
    else:
        h = (seed + p5) & m
    h = (h + n) & m
    while i <= n - 4:
        h = (rotl((h + struct.unpack_from("<I", data, i)[0] * p3) & m, 17) * p4) & m
        i += 4
    while i < n:
        h = (rotl((h + data[i] * p5) & m, 11) * p1) & m
        i += 1
    h ^= h >> 15
    h = (h * p2) & m
    h ^= h >> 13
    h = (h * p3) & m
    h ^= h >> 16
    return h


def global_object_id_hash(prefab_guid: str, component_file_id: str) -> int:
    """NetworkObject.OnValidate: GlobalObjectId.GetGlobalObjectIdSlow(this).ToString().Hash32()
    - identifier type 1 (imported asset), the component's local fileID, prefab id 0."""
    return xxhash32(f"GlobalObjectId_V1-1-{prefab_guid}-{component_file_id}-0".encode("utf-8"))


# ----------------------------------------------------------------------------- the clone
def replace_once(text: str, old: str, new: str, what: str) -> str:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"donor drifted: expected exactly one '{what}', found {count}")
    return text.replace(old, new, 1)


def find_component_id(text: str, script_guid: str) -> str:
    """The fileID of the first MonoBehaviour document whose m_Script is `script_guid`."""
    for m in re.finditer(r"--- !u!114 &(-?\d+)\n(.*?)(?=\n--- !u!|\Z)", text, re.S):
        if f"m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}" in m.group(2):
            return m.group(1)
    raise RuntimeError(f"donor drifted: no component with script {script_guid}")


def executor_blocks(prism_controller_id: str) -> str:
    return (
        f"--- !u!114 &{SLING_EXECUTOR_ID}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {SHIP_ACTIONS_GO}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SLING_EXECUTOR_SCRIPT}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        f"  config: {{fileID: 11400000, guid: {SLING_CONFIG}, type: 2}}\n"
        f"--- !u!114 &{STOP_EXECUTOR_ID}\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {SHIP_ACTIONS_GO}}}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {STOP_EXECUTOR_SCRIPT}, type: 3}}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        f"  vesselPrismController: {{fileID: {prism_controller_id}}}\n"
        "  seedAssemblerExecutor: {fileID: 0}\n"
        "  stationarySeedConfig: {fileID: 0}\n"
        f"  stationaryModeChanged: {{fileID: 11400000, guid: {STATIONARY_MODE_CHANGED}, type: 2}}\n"
        f"  OnMiniGameTurnEnd: {{fileID: 11400000, guid: {ON_MINIGAME_TURN_END}, type: 2}}\n"
    )


def bindings() -> str:
    return (
        "  _touchActionOverrides: []\n"
        "  _gamepadActionOverrides:\n"
        "  - InputEvent: 2\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {SLING_LEFT}, type: 2}}\n"
        "  - InputEvent: 1\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {SLING_RIGHT}, type: 2}}\n"
        "  - InputEvent: 6\n"
        "    ShipActions:\n"
        f"    - {{fileID: 11400000, guid: {HOLD}, type: 2}}\n"
    )


def build_clone(donor: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Squirrel\n", "  m_Name: Stoat\n", "root GameObject name")
    text = replace_once(text, "  _name: Squirrel\n", "  _name: Stoat\n", "VesselStatus _name")
    text = replace_once(text, "  vesselType: 6\n", f"  vesselType: {STOAT_CLASS_ID}\n", "vesselType")

    # Netcode identity. The donor's hash is whatever it is; what matters is that ours is the
    # number the editor would write for THIS guid, so OnValidate changes nothing.
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)

    # The bindings: everything from _touchActionOverrides to the line before _onButtonPressed.
    m = re.search(r"  _touchActionOverrides:\n(?:.*\n)*?(?=  _onButtonPressed:)", text)
    if not m:
        raise RuntimeError("donor drifted: R_VesselActionHandler binding block not found")
    text = text[:m.start()] + bindings() + text[m.end():]

    # The executors: on the ShipActions object, in the registry, and as documents after it.
    text = replace_once(text, f"  - component: {{fileID: {REGISTRY_ID}}}\n",
                        f"  - component: {{fileID: {REGISTRY_ID}}}\n"
                        f"  - component: {{fileID: {SLING_EXECUTOR_ID}}}\n"
                        f"  - component: {{fileID: {STOP_EXECUTOR_ID}}}\n", "ShipActions m_Component entry")
    reg = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  _executors:\n((?:  - \{fileID: -?\d+\}\n)+)" % REGISTRY_ID, text)
    if not reg:
        raise RuntimeError("donor drifted: ActionExecutorRegistry _executors list not found")
    text = text[:reg.end(1)] + f"  - {{fileID: {SLING_EXECUTOR_ID}}}\n  - {{fileID: {STOP_EXECUTOR_ID}}}\n" + text[reg.end(1):]
    end = re.search(r"--- !u!114 &%s\n(?:.*\n)*?(?=--- !u!)" % REGISTRY_ID, text)
    if not end:
        raise RuntimeError("donor drifted: ActionExecutorRegistry document has no successor")
    text = text[:end.end()] + executor_blocks(find_component_id(donor, PRISM_CONTROLLER_SCRIPT)) + text[end.end():]
    return text


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- registrations
def container_entry() -> str:
    return f"  - {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n"


def register_container(text: str) -> str:
    if PREFAB_GUID in text:
        return text
    m = re.search(r"  _shipPrefabs:\n((?:  - \{fileID: -?\d+, guid: [0-9a-f]{32}, type: 3\}\n)+)", text)
    if not m:
        raise RuntimeError("Vessel Prefab Container: _shipPrefabs list not found")
    return text[:m.end(1)] + container_entry() + text[m.end(1):]


def network_entry() -> str:
    return ("  - Override: 0\n"
            f"    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n"
            "    SourcePrefabToOverride: {fileID: 0}\n"
            "    SourceHashToOverride: 0\n"
            "    OverridingTargetPrefab: {fileID: 0}\n")


def register_network(text: str) -> str:
    if PREFAB_GUID in text:
        return text
    if not text.endswith("\n"):
        text += "\n"
    return text + network_entry()


# ----------------------------------------------------------------------------- the checks
def check(files: "dict[str, str]") -> "list[str]":
    """Every invariant of the shipped set, over {relative path: content}. Pure, so the self-test
    can hand it mutated copies."""
    errors = []
    rel_prefab = os.path.relpath(PREFAB, ROOT)
    prefab = files.get(rel_prefab)
    if prefab is None:
        return [f"{rel_prefab} is missing - run without --check to author it"]
    meta = files.get(rel_prefab + ".meta", "")
    if f"guid: {PREFAB_GUID}" not in meta:
        errors.append(f"{rel_prefab}.meta does not carry guid {PREFAB_GUID}")

    def want(cond, msg):
        if not cond:
            errors.append(f"{rel_prefab}: {msg}")

    want(prefab.count("  m_Name: Stoat\n") == 1, "root GameObject is not named Stoat")
    want(prefab.count("  _name: Stoat\n") == 1, "VesselStatus._name is not Stoat")
    want("  m_Name: Squirrel\n" not in prefab and "  _name: Squirrel\n" not in prefab, "still named Squirrel somewhere")
    types = re.findall(r"  vesselType: (-?\d+)\n", prefab)
    want(types == [str(STOAT_CLASS_ID)], f"vesselType must be exactly one '{STOAT_CLASS_ID}', found {types}")

    net = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n"
                    % NETWORK_OBJECT_ID, prefab)
    if not net:
        want(False, "NetworkObject block not found")
    else:
        expected = global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)
        want(int(net.group(1)) == expected,
             f"GlobalObjectIdHash is {net.group(1)}, the editor would write {expected} for this guid")
        want(net.group(2) == "0", "InScenePlacedSourceGlobalObjectIdHash must be 0 on a disk-authored prefab")
        donor = files.get(os.path.relpath(DONOR, ROOT), "")
        dm = re.search(r"  GlobalObjectIdHash: (\d+)\n", donor)
        if dm:
            want(dm.group(1) != net.group(1), "GlobalObjectIdHash still equals the Squirrel's - Netcode would key both on one entry")

    want(bindings() in prefab, "gamepad bindings are not LT->sling left, RT->sling right, X->hold (touch cleared)")
    want(f"  - component: {{fileID: {SLING_EXECUTOR_ID}}}\n" in prefab, "StoatSlingExecutor is not on the ShipActions object")
    want(f"  - component: {{fileID: {STOP_EXECUTOR_ID}}}\n" in prefab, "ToggleTranslationModeActionExecutor is not on the ShipActions object")
    reg = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  _executors:\n((?:  - \{fileID: -?\d+\}\n)+)" % REGISTRY_ID, prefab)
    want(reg is not None and f"  - {{fileID: {SLING_EXECUTOR_ID}}}\n" in reg.group(1)
         and f"  - {{fileID: {STOP_EXECUTOR_ID}}}\n" in reg.group(1), "registry _executors lacks the two Stoat executors")
    try:
        prism = find_component_id(prefab, PRISM_CONTROLLER_SCRIPT)
        want(executor_blocks(prism) in prefab, "executor documents are not authored as expected (config, prism controller, shared channels)")
    except RuntimeError as e:
        want(False, str(e))

    rel_container = os.path.relpath(CONTAINER, ROOT)
    want_c = files.get(rel_container, "")
    if container_entry() not in want_c:
        errors.append(f"{rel_container}: no entry for Stoat.prefab")
    rel_net = os.path.relpath(NETWORK_PREFABS, ROOT)
    if network_entry() not in files.get(rel_net, ""):
        errors.append(f"{rel_net}: no entry for Stoat.prefab")

    for name, guid in (("StoatSlingConfig", SLING_CONFIG), ("StoatSlingLeftAction", SLING_LEFT),
                       ("StoatSlingRightAction", SLING_RIGHT), ("StoatHoldAction", HOLD)):
        rel = os.path.relpath(os.path.join(ACTIONS, name + ".asset"), ROOT)
        if rel not in files:
            errors.append(f"{rel} is missing")
        elif f"guid: {guid}" not in files.get(rel + ".meta", ""):
            errors.append(f"{rel}.meta does not carry guid {guid}")
    rel_map = os.path.relpath(MAP, ROOT)
    mp = files.get(rel_map)
    if mp is None:
        errors.append(f"{rel_map} is missing")
    else:
        if f"  vesselClass: {STOAT_CLASS_ID}\n" not in mp:
            errors.append(f"{rel_map}: vesselClass is not {STOAT_CLASS_ID}")
        if "    Input: 2\n" not in mp or "    Input: 6\n" not in mp:
            errors.append(f"{rel_map}: the sling (Input 2) and the hold (Input 6) must both be declared")
    sl = files.get(os.path.relpath(os.path.join(ACTIONS, "StoatSlingLeftAction.asset"), ROOT), "")
    sr = files.get(os.path.relpath(os.path.join(ACTIONS, "StoatSlingRightAction.asset"), ROOT), "")
    if sl and "  side: 0\n" not in sl:
        errors.append("StoatSlingLeftAction.asset is not side 0 (Left)")
    if sr and "  side: 1\n" not in sr:
        errors.append("StoatSlingRightAction.asset is not side 1 (Right)")
    return errors


def read_all() -> "dict[str, str]":
    paths = [PREFAB, PREFAB + ".meta", DONOR, CONTAINER, NETWORK_PREFABS, MAP]
    for name in ("StoatSlingConfig", "StoatSlingLeftAction", "StoatSlingRightAction", "StoatHoldAction"):
        paths += [os.path.join(ACTIONS, name + ".asset"), os.path.join(ACTIONS, name + ".asset.meta")]
    files = {}
    for p in paths:
        if os.path.exists(p):
            with open(p, encoding="utf-8", newline="") as f:
                files[os.path.relpath(p, ROOT)] = f.read().replace("\r\n", "\n")
    return files


def write(path: str, text: str) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


# ----------------------------------------------------------------------------- self-test
def self_test() -> int:
    ok = xxhash32(b"") == 0x02CC5D05 and xxhash32(b"a") == 0x550D7456
    # The Squirrel's own hash, as the editor wrote it, from its guid and NetworkObject id.
    ok &= global_object_id_hash("84755e5175a641e0bff6b47d35746cff", NETWORK_OBJECT_ID) == 2256742461
    print(f"xxhash32 reproduces Netcode's hashes: {'OK' if ok else 'FAIL'}")
    files = read_all()
    base = check(files)
    print(f"shipped set: {'OK' if not base else 'FAIL ' + '; '.join(base)}")
    ok &= not base
    rel = os.path.relpath(PREFAB, ROOT)

    def fires(label, mutate):
        nonlocal ok
        mutated = dict(files)
        mutate(mutated)
        errs = check(mutated)
        print(f"  negative control {label}: {'fires' if errs else 'MISSED'}")
        ok &= bool(errs)

    fires("wrong vesselType", lambda f: f.__setitem__(rel, f[rel].replace(f"  vesselType: {STOAT_CLASS_ID}\n", "  vesselType: 6\n")))
    fires("donor hash kept", lambda f: f.__setitem__(rel, re.sub(r"  GlobalObjectIdHash: \d+\n", "  GlobalObjectIdHash: 2256742461\n", f[rel], count=1)))
    fires("executor dropped from registry", lambda f: f.__setitem__(rel, f[rel].replace(f"  - {{fileID: {SLING_EXECUTOR_ID}}}\n", "", 1)))
    fires("left trigger unbound", lambda f: f.__setitem__(rel, f[rel].replace(f"    - {{fileID: 11400000, guid: {SLING_LEFT}, type: 2}}\n", "", 1)))
    fires("container entry missing", lambda f: f.__setitem__(os.path.relpath(CONTAINER, ROOT), f[os.path.relpath(CONTAINER, ROOT)].replace(container_entry(), "")))
    fires("network entry missing", lambda f: f.__setitem__(os.path.relpath(NETWORK_PREFABS, ROOT), f[os.path.relpath(NETWORK_PREFABS, ROOT)].replace(network_entry(), "")))
    fires("map lost the hold", lambda f: f.__setitem__(os.path.relpath(MAP, ROOT), f[os.path.relpath(MAP, ROOT)].replace("    Input: 6\n", "    Input: 0\n")))
    print("self-test " + ("OK" if ok else "FAILED"))
    return 0 if ok else 1


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        if not os.path.exists(PREFAB):
            with open(DONOR, encoding="utf-8", newline="") as f:
                donor = f.read().replace("\r\n", "\n")
            write(PREFAB, build_clone(donor))
            write(PREFAB + ".meta", prefab_meta())
            print(f"wrote {os.path.relpath(PREFAB, ROOT)} (+ .meta) from Squirrel.prefab")
        else:
            print(f"{os.path.relpath(PREFAB, ROOT)} exists - the clone stands down (delete it to re-author)")
        for path, fn in ((CONTAINER, register_container), (NETWORK_PREFABS, register_network)):
            with open(path, encoding="utf-8", newline="") as f:
                before = f.read().replace("\r\n", "\n")
            after = fn(before)
            if after != before:
                write(path, after)
                print(f"registered Stoat.prefab in {os.path.relpath(path, ROOT)}")
    errors = check(read_all())
    for e in errors:
        print(f"FAIL: {e}")
    print("author_stoat_assets: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
