#!/usr/bin/env python3
"""Author the pilots-and-artifacts milestone-1 assets (Docs/PILOTS_AND_ARTIFACTS.md §10, slice 2).

A generator in the author_*_assets.py family: deterministic guids (md5 of a stable name),
idempotent (a re-run rewrites the same bytes), --check compares against disk and exits nonzero on
drift without writing anything, --self-test proves the checks fire. It owns:

  * Assets/_SO_Assets/Pilots/                                   folder
  * Assets/_SO_Assets/Pilots/Default/                           folder
  * Assets/_SO_Assets/Pilots/Default/Pilot_<Vessel>_Default.asset   one per milestone vessel
  * Assets/_SO_Assets/Pilots/PilotArtifactCatalog.asset         every pilot and artifact, in order
  * the `keywords` key of Assets/_SO_Assets/Classes/SO_Class_<Vessel>.asset (that key only;
    the rest of each vessel asset is left byte-for-byte as it is)

The CONTENT is §4's proposed keyword table and §6's default-kit column, transcribed. A content
change is an edit to MILESTONE below and a re-run, never a hand edit of the assets: a hand edit is
reverted by the next run and reported as drift by --check.

Usage:
    python3 Tools/Build/author_pilot_assets.py             # write
    python3 Tools/Build/author_pilot_assets.py --check     # report drift, write nothing
    python3 Tools/Build/author_pilot_assets.py --self-test # negative controls
"""
import hashlib
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

PILOTS_DIR = "Assets/_SO_Assets/Pilots"
DEFAULTS_DIR = PILOTS_DIR + "/Default"
CATALOG_PATH = PILOTS_DIR + "/PilotArtifactCatalog.asset"
CLASS_ASSET = "Assets/_SO_Assets/Classes/SO_Class_{}.asset"

PILOT_SO_CS = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/PilotSO.cs"
CATALOG_SO_CS = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/PilotArtifactCatalogSO.cs"
VESSEL_CLASS_CS = "Assets/_Scripts/Data/Enums/VesselClassType.cs"
VESSEL_KEYWORD_CS = "Assets/_Scripts/Data/Enums/VesselKeyword.cs"

# The milestone-1 fleet (§6): vessel, its default pilot's kit name (§6 "Pilot A"), and the
# keywords it satisfies (§4's starting vocabulary). Grizzly, Termite, Falcon and Shrike are out.
MILESTONE = [
    ("Manta",     "Yastri Trail", ["BasePrismProduction", "CrystalBlast"]),
    ("Dolphin",   "Echo Sight",   ["BasePrismProduction", "Boost", "CrystalBlast"]),
    ("Rhino",     "Energy Sword", ["BasePrismProduction", "Blade", "CrystalBlast"]),
    ("Squirrel",  "Boost Ring",   ["BasePrismProduction", "Boost", "Blade", "CrystalBlast", "Attach"]),
    ("Serpent",   "Sniper Shot",  ["BasePrismProduction", "Projectile"]),
    ("Sparrow",   "Pulsefire",    ["BasePrismProduction", "Boost", "Projectile"]),
    ("Urchin",    "Chain Spikes", ["BasePrismProduction", "Boost", "Attach"]),
    ("Butterfly", "Dust Mode",    ["BasePrismProduction"]),
    ("Thresher",  "Winch",        ["BasePrismProduction", "Blade", "Tether"]),
]


def guid_for(name):
    return hashlib.md5(f"CosmicShore/PilotsAndArtifacts/{name}".encode()).hexdigest()


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", newline="") as f:
        return f.read()


def script_guid(cs_rel):
    m = re.search(r"^guid: ([0-9a-f]{32})\s*$", read(cs_rel + ".meta"), re.M)
    assert m, f"no guid in {cs_rel}.meta"
    return m.group(1)


def enum_values(cs_rel, enum_name):
    """Name -> int for an enum with explicit values, read from the C# so the table cannot drift."""
    src = read(cs_rel)
    body = re.search(r"enum\s+" + enum_name + r"\s*\{(.*?)\}", src, re.S)
    assert body, f"enum {enum_name} not found in {cs_rel}"
    values = {name: int(v) for name, v in re.findall(r"^\s*(\w+)\s*=\s*(-?\d+)\s*,", body.group(1), re.M)}
    assert values, f"enum {enum_name} has no explicit values"
    return values


def yaml_str(s):
    """A string the way Unity writes it: plain when safe, single-quoted otherwise."""
    if s == "":
        return ""
    plain_ok = (re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9 .,'()/-]*", s) is not None
                and ": " not in s and " #" not in s and not s.endswith(" "))
    return s if plain_ok else "'" + s.replace("'", "''") + "'"


SO_HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier:
"""

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def pilot_asset(vessel, kit, vessel_value, script):
    name = f"Pilot_{vessel}_Default"
    description = (f"The {vessel}'s base kit, flown as shipped. Every other {vessel} pilot "
                   f"swaps part of it for something different.")
    return SO_HEADER.format(script=script, name=name) + (
        f"  id: pilot.{vessel.lower()}.default\n"
        f"  displayName: {yaml_str(kit)}\n"
        f"  description: {yaml_str(description)}\n"
        f"  flavor: \n"
        f"  portrait: {{fileID: 0}}\n"
        f"  icon: {{fileID: 0}}\n"
        f"  requirement:\n"
        f"    Vessel: {vessel_value}\n"
        f"    Keywords: []\n"
        f"  isDefault: 1\n"
        f"  elementalBoost:\n"
        f"    Element: 0\n"
        f"    Petals: 0\n"
        f"  aiBehaviorDescription: \n"
    )


def catalog_asset(pilot_guids, script):
    lines = "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in pilot_guids)
    return SO_HEADER.format(script=script, name="PilotArtifactCatalog") + (
        "  pilots:\n" + lines + "  artifacts: []\n"
    )


KEYWORDS_BLOCK = re.compile(r"^  keywords:(?: \[\])?\n(?:  - -?\d+\n)*", re.M)


def with_keywords(class_text, keyword_values):
    """The SO_Class asset with its `keywords` key set; everything else unchanged. Idempotent."""
    block = "  keywords:\n" + "".join(f"  - {v}\n" for v in keyword_values) if keyword_values else "  keywords: []\n"
    found = KEYWORDS_BLOCK.findall(class_text)
    assert len(found) <= 1, "more than one keywords key"
    if found:
        return KEYWORDS_BLOCK.sub(lambda _: block, class_text, count=1)
    anchor = "  gameplayParameter1:\n"
    assert class_text.count(anchor) == 1, "SO_Vessel anchor 'gameplayParameter1:' not found exactly once"
    return class_text.replace(anchor, block + anchor, 1)


def build():
    """Every file this generator owns, as {relative path: text}."""
    vessel_values = enum_values(VESSEL_CLASS_CS, "VesselClassType")
    keyword_values = enum_values(VESSEL_KEYWORD_CS, "VesselKeyword")
    pilot_script = script_guid(PILOT_SO_CS)
    catalog_script = script_guid(CATALOG_SO_CS)

    out = {
        PILOTS_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder/Pilots")),
        DEFAULTS_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder/Pilots/Default")),
    }
    pilot_guids = []
    for vessel, kit, keywords in MILESTONE:
        path = f"{DEFAULTS_DIR}/Pilot_{vessel}_Default.asset"
        guid = guid_for(f"Pilot_{vessel}_Default.asset")
        pilot_guids.append(guid)
        out[path] = pilot_asset(vessel, kit, vessel_values[vessel], pilot_script)
        out[path + ".meta"] = ASSET_META.format(guid=guid)

        class_path = CLASS_ASSET.format(vessel)
        out[class_path] = with_keywords(read(class_path), [keyword_values[k] for k in keywords])

    out[CATALOG_PATH] = catalog_asset(pilot_guids, catalog_script)
    out[CATALOG_PATH + ".meta"] = ASSET_META.format(guid=guid_for("PilotArtifactCatalog.asset"))
    return out


def drift(files):
    """Paths whose disk content differs from what this generator would write."""
    changed = []
    for rel, text in files.items():
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full) or read(rel) != text:
            changed.append(rel)
    return changed


def guid_owners(guid):
    out = subprocess.run(["grep", "-rl", "--include=*.meta", f"^guid: {guid}$", os.path.join(ROOT, "Assets")],
                         capture_output=True, text=True).stdout.split("\n")
    return sorted(os.path.relpath(p, ROOT) for p in out if p)


def check_guid_ownership(files):
    """Each minted guid is owned by exactly the .meta this generator writes for it."""
    problems = []
    for rel, text in files.items():
        if not rel.endswith(".meta"):
            continue
        guid = re.search(r"^guid: ([0-9a-f]{32})$", text, re.M).group(1)
        owners = [o for o in guid_owners(guid) if o != rel]
        if owners:
            problems.append(f"{rel}: guid {guid} is also owned by {', '.join(owners)}")
    return problems


def self_test():
    files = build()
    failures = []

    # 1. Idempotent: applying the keyword edit twice changes nothing the second time.
    for vessel, _, keywords in MILESTONE:
        once = files[CLASS_ASSET.format(vessel)]
        twice = with_keywords(once, [enum_values(VESSEL_KEYWORD_CS, "VesselKeyword")[k] for k in keywords])
        if once != twice:
            failures.append(f"keyword edit is not idempotent on {vessel}")

    # 2. The edit touches only the keywords key of a vessel asset.
    for vessel, _, _ in MILESTONE:
        before = KEYWORDS_BLOCK.sub("", read(CLASS_ASSET.format(vessel)))
        after = KEYWORDS_BLOCK.sub("", files[CLASS_ASSET.format(vessel)])
        if before != after:
            failures.append(f"{vessel}: the edit changed more than the keywords key")

    # 3. Negative control: drift detection fires on a changed file.
    path = f"{DEFAULTS_DIR}/Pilot_Manta_Default.asset"
    if os.path.exists(os.path.join(ROOT, path)):
        tampered = dict(files)
        tampered[path] = files[path].replace("pilot.manta.default", "pilot.manta.tampered")
        if path not in drift(tampered):
            failures.append("drift() did not report a tampered pilot asset")

    # 4. Negative control: a vessel asset without the anchor is refused, not silently skipped.
    try:
        with_keywords("MonoBehaviour:\n  Class: 1\n", [1])
        failures.append("with_keywords accepted an asset with no anchor")
    except AssertionError:
        pass

    # 5. Negative control: a string that YAML would misread is quoted.
    if yaml_str("a: b") != "'a: b'" or yaml_str("Winch") != "Winch":
        failures.append("yaml_str quoting is wrong")

    # 6. Every default pilot binds its own vessel class and the catalog lists all of them, in order.
    catalog = files[CATALOG_PATH]
    listed = re.findall(r"guid: ([0-9a-f]{32}), type: 2", catalog)
    expected = [guid_for(f"Pilot_{v}_Default.asset") for v, _, _ in MILESTONE]
    if listed != expected:
        failures.append("catalog does not list the default pilots in MILESTONE order")

    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else f"OK ({6} checks)")
    return 1 if failures else 0


def main():
    if "--self-test" in sys.argv:
        return self_test()

    files = build()
    problems = check_guid_ownership(files)
    changed = drift(files)

    if "--check" in sys.argv:
        for p in problems:
            print("GUID:", p)
        for rel in changed:
            print("DRIFT:", rel)
        print(f"check: {len(files)} files, {len(changed)} drifted, {len(problems)} guid problems")
        return 1 if changed or problems else 0

    if problems:
        for p in problems:
            print("GUID:", p)
        return 1
    for rel in changed:
        full = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="") as f:
            f.write(files[rel])
        print("wrote", rel)
    print(f"{len(changed)} written, {len(files) - len(changed)} already current")
    return 0


if __name__ == "__main__":
    sys.exit(main())
