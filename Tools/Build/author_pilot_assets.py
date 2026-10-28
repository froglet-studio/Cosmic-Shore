#!/usr/bin/env python3
"""Author the squad's PILOT and ARTIFACT assets (Docs/Pilots/CALLSIGNS.md, Docs/Pilots/ARTIFACTS.md).

A generator in the author_*_assets.py family: deterministic guids (md5 of a stable name), idempotent (a re-run
rewrites the same bytes), --check compares against disk and exits nonzero on drift without writing anything,
--self-test proves the checks fire. It owns:

  * Assets/_SO_Assets/Pilots/                                   folder
  * Assets/_SO_Assets/Pilots/Roster/Pilot_<Callsign>.asset       one pilot per callsign (17)
  * Assets/_SO_Assets/Pilots/Artifacts/Artifact_<Name>.asset     the artifacts (Javelin, Aegis, Hydra, Aftershock)
  * Assets/_SO_Assets/Pilots/Transforms/<Name>Transform.asset    each artifact's payload transformation
  * Assets/Resources/Pilots/PilotArtifactCatalog.asset          every pilot and artifact, in order (Resources: the
                                                                match loads it, PilotArtifactCatalogSO.Load)
  * the `keywords` key of Assets/_SO_Assets/Classes/SO_Class_<Vessel>.asset (that key only; the rest of each vessel
    asset is left byte-for-byte as it is)

THE ROSTER (Garrett, 2026-10-27): one pilot per callsign, any vessel. A pilot is a pair of eyes: it carries one
callsign (PilotSO.vision) and changes nothing else. Its name is its callsign's word, read from
Tools/Build/author_callsign_assets.py (the one list of the callsigns), so the two cannot drift. This replaces the
2026-10-26 roster on Shombith's branch (40 pilots, each rewriting one ability, and 45 vessel-bound numeric
artifacts), which was thrown out because it conflicts with CALLSIGNS.md and ARTIFACTS.md.

THE ARTIFACTS: each is one payload transformation with four element tracks (0 to 10, a twist at 5), bound to no
vessel class, from Docs/Pilots/pilots_and_artifacts.json. Ids are what players save: never rename one.

Usage:
    python3 Tools/Build/author_pilot_assets.py             # write
    python3 Tools/Build/author_pilot_assets.py --check     # report drift, write nothing
    python3 Tools/Build/author_pilot_assets.py --self-test # negative controls
"""
import hashlib
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_callsign_assets import CALLSIGNS  # noqa: E402  (value, word, meaning): one row per callsign

PILOTS_DIR = "Assets/_SO_Assets/Pilots"
ROSTER_DIR = PILOTS_DIR + "/Roster"
ARTIFACTS_DIR = PILOTS_DIR + "/Artifacts"
TRANSFORMS_DIR = PILOTS_DIR + "/Transforms"
CATALOG_DIR = "Assets/Resources/Pilots"                 # PilotArtifactCatalogSO.ResourcePath: vessels load it at runtime
CATALOG_PATH = CATALOG_DIR + "/PilotArtifactCatalog.asset"
CLASS_ASSET = "Assets/_SO_Assets/Classes/SO_Class_{}.asset"
SPEC = "Docs/Pilots/pilots_and_artifacts.json"

PILOT_SO_CS = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/PilotSO.cs"
CATALOG_SO_CS = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/PilotArtifactCatalogSO.cs"
ARTIFACT_SO_CS = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/ArtifactSO.cs"
TRANSFORM_CS = {
    "Javelin": "Assets/_Scripts/Controller/Payload/Transforms/JavelinTransformSO.cs",
    "Aegis": "Assets/_Scripts/Controller/Payload/Transforms/AegisTransformSO.cs",
    "Hydra": "Assets/_Scripts/Controller/Payload/Transforms/HydraTransformSO.cs",
    "Aftershock": "Assets/_Scripts/Controller/Payload/Transforms/AftershockTransformSO.cs",
}
CRACK_EFFECT_META = "Assets/_SO_Assets/Effects/Payload Prism Effects/PayloadCrackPrismEffect.asset.meta"
VESSEL_CLASS_CS = "Assets/_Scripts/Data/Enums/VesselClassType.cs"
VESSEL_KEYWORD_CS = "Assets/_Scripts/Data/Enums/VesselKeyword.cs"
ELEMENT_CS = "Assets/_Scripts/Data/Enums/Element.cs"
TRACK_ELEMENTS = ("Mass", "Space", "Time", "Charge")
MAX_LEVEL = 10

# The keywords each hull satisfies (Docs/PILOTS_AND_ARTIFACTS.md §4's starting vocabulary). Artifacts may lean on a
# shared keyword (Wake on BasePrismProduction), never on a class.
KEYWORDS = [
    ("Manta",     ["BasePrismProduction", "CrystalBlast"]),
    ("Dolphin",   ["BasePrismProduction", "Boost", "CrystalBlast"]),
    ("Rhino",     ["BasePrismProduction", "Blade", "CrystalBlast"]),
    ("Squirrel",  ["BasePrismProduction", "Boost", "Blade", "CrystalBlast", "Attach"]),
    ("Serpent",   ["BasePrismProduction", "Projectile"]),
    ("Sparrow",   ["BasePrismProduction", "Boost", "Projectile"]),
    ("Urchin",    ["BasePrismProduction", "Boost", "Attach"]),
    ("Butterfly", ["BasePrismProduction"]),
    ("Thresher",  ["BasePrismProduction", "Blade", "Tether"]),
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


def yaml_any(s):
    """yaml_str for ASCII; a double-quoted, escaped scalar for anything else (Unity's own form)."""
    if all(32 <= ord(c) <= 126 for c in s):
        return yaml_str(s)
    out = "".join("\\\\" if c == "\\" else '\\"' if c == '"' else c if 32 <= ord(c) <= 126 else f"\\u{ord(c):04X}" for c in s)
    return f'"{out}"'


def ascii_ok(t):
    return all(32 <= ord(c) <= 126 for c in t)


def file_stem(text):
    return re.sub(r"[^A-Za-z0-9]", "", text)


def load_spec():
    return json.loads(read(SPEC))


def asset_meta_guid(rel):
    m = re.search(r"^guid: ([0-9a-f]{32})\s*$", read(rel + ".meta"), re.M)
    assert m, f"no guid in {rel}.meta"
    return m.group(1)


def empty_event(field):
    return (f"  {field}:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n"
            "    Path: \n")


def callsign_word(value):
    for v, word, _ in CALLSIGNS:
        if v == value:
            return word
    raise AssertionError(f"no callsign {value}")


def pilot_id(value):
    return f"pilot.callsign.{callsign_word(value).lower()}"


def fnum(v):
    return str(int(v)) if float(v).is_integer() else repr(float(v))


def pilot_asset(value, any_vessel, script):
    word = callsign_word(value)
    name = f"Pilot_{word.capitalize()}"
    return name, SO_HEADER.format(script=script, name=name) + (
        f"  id: {pilot_id(value)}\n"
        f"  displayName: {yaml_str(word)}\n"
        f"  description: \n"
        f"  flavor: \n"
        f"  portrait: {{fileID: 0}}\n"
        f"  portraitIsStandIn: 0\n"
        f"  icon: {{fileID: 0}}\n"
        f"  requirement:\n"
        f"    Vessel: {any_vessel}\n"
        f"    Keywords: []\n"
        f"  vision: {value}\n"
        f"  aiBehaviorDescription: \n"
    )


def transform_asset(a, script, crack_guid):
    name = f"{a['transform']}Transform"
    t = a["transformTuning"]
    out = SO_HEADER.format(script=script, name=name)
    if a["transform"] == "Javelin":
        out += (f"  speed: {fnum(t['speed'])}\n  lingerPulseSeconds: {fnum(t['lingerPulseSeconds'])}\n"
                f"  burstEffects:\n  - {{fileID: 11400000, guid: {crack_guid}, type: 2}}\n")
    elif a["transform"] == "Aegis":
        out += f"  reraiseDelay: {fnum(t['reraiseDelay'])}\n  notHomeLine: {yaml_any(t['notHomeLine'])}\n"
    elif a["transform"] == "Hydra":
        out += (f"  speed: {fnum(t['speed'])}\n  fanDegrees: {fnum(t['fanDegrees'])}\n"
                f"  seekDegrees: {fnum(t['seekDegrees'])}\n  lingerPulseSeconds: {fnum(t['lingerPulseSeconds'])}\n")
    elif a["transform"] == "Aftershock":
        out += f"  ringShare: {fnum(t['ringShare'])}\n"
    return name, out


def artifact_asset(a, any_vessel, element_values, script, transform_guid):
    name = f"Artifact_{file_stem(a['name'])}"
    tracks = ""
    for tr in a["tracks"]:
        tracks += (f"  - element: {element_values[tr['element']]}\n    label: {yaml_any(tr['label'])}\n"
                   f"    atZero: {fnum(tr['atZero'])}\n    atTen: {fnum(tr['atTen'])}\n"
                   f"    baseCost: {tr['baseCost']}\n    costStep: {tr['costStep']}\n"
                   f"    twistLine: {yaml_any(tr['twist'])}\n")
    return name, SO_HEADER.format(script=script, name=name) + (
        f"  id: {a['id']}\n"
        f"  displayName: {yaml_any(a['name'])}\n"
        f"  description: {yaml_any(a['description'])}\n"
        f"  lore: \n"
        f"  icon: {{fileID: 0}}\n"
        f"  requirement:\n"
        f"    Vessel: {any_vessel}\n"
        f"    Keywords: []\n"
        f"  payloadTransform: {{fileID: 11400000, guid: {transform_guid}, type: 2}}\n"
        f"  tracks:\n{tracks}"
        f"  foundOnVoyages: {1 if a.get('foundOnVoyages') else 0}\n"
    ) + empty_event("loadSound") + empty_event("releaseSound")


def catalog_asset(pilot_guids, script, artifact_guids, slots):
    lines = "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in pilot_guids)
    arts = "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in artifact_guids)
    return SO_HEADER.format(script=script, name="PilotArtifactCatalog") + (
        "  pilots:\n" + lines + ("  artifacts:\n" + arts if arts else "  artifacts: []\n")
        + f"  slotsPerVessel: {slots}\n"
    )


SCRIPT_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def validate_spec(spec):
    """The roster's rules. Returns a list of problems (empty = sound)."""
    out = []
    pilots, artifacts = spec["pilots"], spec["artifacts"]
    values = [p["vision"] for p in pilots]
    if sorted(values) != [v for v, _, _ in CALLSIGNS]:
        out.append("the pilots are not one per callsign, each callsign once")
    for p in pilots:
        if p["vision"] not in {v for v, _, _ in CALLSIGNS}:
            out.append(f"pilot {p!r} carries no known sight")
    ids = [a["id"] for a in artifacts]
    if len(set(ids)) != len(ids):
        out.append("two artifacts share an id")
    for a in artifacts:
        name = a.get("name", "?")
        if not re.fullmatch(r"artifact\.[a-z0-9-]+", a["id"]):
            out.append(f"artifact {name}: id {a['id']!r} is not artifact.<lowercase>")
        if a.get("vessel"):
            out.append(f"artifact {name} names a vessel; no vessel class appears in an artifact")
        if a.get("transform") not in TRANSFORM_CS:
            out.append(f"artifact {name}: unknown transform {a.get('transform')!r}")
        for t in (a["name"], a["description"]):
            if not t.strip() or not ascii_ok(t):
                out.append(f"artifact {name}: {t!r} is empty or not printable ASCII")
        elements = [tr.get("element") for tr in a["tracks"]]
        if sorted(elements) != sorted(TRACK_ELEMENTS):
            out.append(f"artifact {name}: tracks {elements} are not one each of Mass, Space, Time and Charge")
        for tr in a["tracks"]:
            for f in ("label", "twist"):
                if not str(tr.get(f, "")).strip() or not ascii_ok(str(tr.get(f, ""))):
                    out.append(f"artifact {name} {tr.get('element')}: {f} is empty or not printable ASCII")
            if not isinstance(tr.get("baseCost"), int) or tr["baseCost"] < 1:
                out.append(f"artifact {name} {tr.get('element')}: baseCost must be a whole number of at least 1")
            if not isinstance(tr.get("costStep"), int) or tr["costStep"] < 0:
                out.append(f"artifact {name} {tr.get('element')}: costStep must be a whole number of at least 0")
    if not 1 <= spec["slotsPerVessel"] <= 4:
        out.append("slotsPerVessel must be 1 to 4 (the Hangar draws four slot frames)")
    return out


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


def build(spec=None):
    """Every file this generator owns, as {relative path: text}."""
    vessel_values = enum_values(VESSEL_CLASS_CS, "VesselClassType")
    keyword_values = enum_values(VESSEL_KEYWORD_CS, "VesselKeyword")
    element_values = enum_values(ELEMENT_CS, "Element")
    pilot_script = script_guid(PILOT_SO_CS)
    catalog_script = script_guid(CATALOG_SO_CS)
    artifact_script = script_guid(ARTIFACT_SO_CS)
    crack_guid = re.search(r"^guid: ([0-9a-f]{32})", read(CRACK_EFFECT_META), re.M).group(1)
    spec = load_spec() if spec is None else spec
    any_vessel = vessel_values["Any"]

    out = {
        PILOTS_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder/Pilots")),
        ROSTER_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder/Pilots/Roster")),
        ARTIFACTS_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder/Pilots/Artifacts")),
        TRANSFORMS_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder/Pilots/Transforms")),
        CATALOG_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder/Resources/Pilots")),
    }
    for vessel, keywords in KEYWORDS:
        class_path = CLASS_ASSET.format(vessel)
        out[class_path] = with_keywords(read(class_path), [keyword_values[k] for k in keywords])

    pilot_guids = []
    for p in sorted(spec["pilots"], key=lambda p: p["vision"]):
        name, text = pilot_asset(p["vision"], any_vessel, pilot_script)
        guid = guid_for(f"Roster/{name}.asset")
        pilot_guids.append(guid)
        out[f"{ROSTER_DIR}/{name}.asset"] = text
        out[f"{ROSTER_DIR}/{name}.asset.meta"] = ASSET_META.format(guid=guid)

    artifact_guids = []
    for a in spec["artifacts"]:
        tname, ttext = transform_asset(a, script_guid(TRANSFORM_CS[a["transform"]]), crack_guid)
        tguid = guid_for(f"Transforms/{tname}.asset")
        out[f"{TRANSFORMS_DIR}/{tname}.asset"] = ttext
        out[f"{TRANSFORMS_DIR}/{tname}.asset.meta"] = ASSET_META.format(guid=tguid)
        name, text = artifact_asset(a, any_vessel, element_values, artifact_script, tguid)
        guid = guid_for(f"Artifacts/{name}.asset")
        artifact_guids.append(guid)
        out[f"{ARTIFACTS_DIR}/{name}.asset"] = text
        out[f"{ARTIFACTS_DIR}/{name}.asset.meta"] = ASSET_META.format(guid=guid)

    out[CATALOG_PATH] = catalog_asset(pilot_guids, catalog_script, artifact_guids, spec["slotsPerVessel"])
    out[CATALOG_PATH + ".meta"] = ASSET_META.format(guid=guid_for("PilotArtifactCatalog.asset"))
    return out


def drift(files):
    """Paths whose disk content differs from what this generator would write."""
    changed = []
    for rel, text in files.items():
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full) or read(rel) != text:
            changed.append(rel)
    # Every asset in the roster, artifact and transform folders is one this generator owns: a stray sits in no catalog.
    for d in (ROSTER_DIR, ARTIFACTS_DIR, TRANSFORMS_DIR):
        full = os.path.join(ROOT, d)
        if os.path.isdir(full):
            changed += [f"{d}/{f} (stray)" for f in sorted(os.listdir(full)) if f"{d}/{f}" not in files]
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
    kv = enum_values(VESSEL_KEYWORD_CS, "VesselKeyword")
    for vessel, keywords in KEYWORDS:
        once = files[CLASS_ASSET.format(vessel)]
        if once != with_keywords(once, [kv[k] for k in keywords]):
            failures.append(f"keyword edit is not idempotent on {vessel}")

    # 2. The edit touches only the keywords key of a vessel asset.
    for vessel, _ in KEYWORDS:
        if KEYWORDS_BLOCK.sub("", read(CLASS_ASSET.format(vessel))) != KEYWORDS_BLOCK.sub("", files[CLASS_ASSET.format(vessel)]):
            failures.append(f"{vessel}: the edit changed more than the keywords key")

    # 3. Negative controls: drift fires on a changed file and on a stray asset.
    path = f"{ROSTER_DIR}/Pilot_Ensign.asset"
    if os.path.exists(os.path.join(ROOT, path)):
        tampered = dict(files)
        tampered[path] = files[path].replace("vision: 1", "vision: 2")
        if path not in drift(tampered):
            failures.append("drift() did not report a tampered pilot asset")
        stray = dict(files)
        del stray[path]
        if f"{path} (stray)" not in drift(stray):
            failures.append("drift() did not report a stray roster asset")

    # 4. A vessel asset without the anchor is refused, not silently skipped.
    try:
        with_keywords("MonoBehaviour:\n  Class: 1\n", [1])
        failures.append("with_keywords accepted an asset with no anchor")
    except AssertionError:
        pass

    # 5. Every pilot is one callsign, on any vessel; no artifact names a class.
    any_vessel = enum_values(VESSEL_CLASS_CS, "VesselClassType")["Any"]
    pilots = [t for r, t in files.items() if r.startswith(ROSTER_DIR + "/") and r.endswith(".asset")]
    if len(pilots) != len(CALLSIGNS):
        failures.append(f"{len(pilots)} pilots, not one per callsign")
    for text in pilots + [t for r, t in files.items() if r.startswith(ARTIFACTS_DIR + "/") and r.endswith(".asset")]:
        if f"    Vessel: {any_vessel}\n" not in text:
            failures.append("a pilot or artifact is bound to a vessel class")

    # 6. The spec's rules, each planted defect caught.
    import copy
    spec = load_spec()
    if validate_spec(spec):
        failures.append("the shipped spec does not validate: " + "; ".join(validate_spec(spec)))
    plants = {
        "a callsign with two pilots": lambda s: s["pilots"].append(dict(s["pilots"][0])),
        "a callsign with no pilot": lambda s: s["pilots"].pop(0),
        "a pilot with an unknown sight": lambda s: s["pilots"][1].update(vision=99),
        "two artifacts share an id": lambda s: s["artifacts"][1].update(id=s["artifacts"][0]["id"]),
        "an artifact bound to a vessel": lambda s: s["artifacts"][0].update(vessel="Rhino"),
        "an unknown transform": lambda s: s["artifacts"][0].update(transform="Gorgon"),
        "a missing track": lambda s: s["artifacts"][0]["tracks"].pop(),
        "two Mass tracks": lambda s: s["artifacts"][0]["tracks"][1].update(element="Mass"),
        "an empty twist": lambda s: s["artifacts"][1]["tracks"][0].update(twist=" "),
        "a label the font cannot draw": lambda s: s["artifacts"][1]["tracks"][2].update(label="4 \u2192 5"),
        "a free level": lambda s: s["artifacts"][0]["tracks"][0].update(baseCost=0),
        "six slots": lambda s: s.update(slotsPerVessel=6),
    }
    for what, fn in plants.items():
        s = copy.deepcopy(spec)
        fn(s)
        if not validate_spec(s):
            failures.append(f"validate_spec missed: {what}")
    if yaml_any("4 \u2192 5") != '"4 \\u2192 5"':
        failures.append("yaml_any does not escape non-ASCII")

    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else f"OK (6 checks, {len(plants)} planted spec defects caught)")
    return 1 if failures else 0


def main():
    if "--self-test" in sys.argv:
        return self_test()

    spec = load_spec()
    spec_problems = validate_spec(spec)
    if spec_problems:
        for p in spec_problems:
            print("SPEC:", p)
        return 1
    files = build(spec)
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
