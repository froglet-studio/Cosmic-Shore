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
  * Assets/_SO_Assets/Pilots/Roster/Pilot_<Vessel>_<Name>.asset      two per Hangar vessel (2026-10-26)
  * Assets/_SO_Assets/Pilots/Artifacts/Artifact_<Fits>_<Name>.asset  two per vessel, five for the fleet
  * Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/ArtifactStat.cs (+ .meta)  the stat keys

THE ROSTER (Shombith, 2026-10-26, Docs/Pilots/PILOTS_AND_ARTIFACTS.md): the 40 pilots and 45 artifacts come from
Docs/Pilots/pilots_and_artifacts.json, the one spec. A pilot rewrites one ability of its home vessel and is not tied
to an element (its boost stays none); an artifact moves one number in three ranks, three slots per vessel. Eight
pilots wear a spare portrait until the artist paints them (portraitIsStandIn); eight have none yet. Ids are what
players save: never rename one. statId values serialize: never renumber or reuse one.

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
ARTIFACT_SO_CS = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/ArtifactSO.cs"
STAT_CS = "Assets/_Scripts/ScriptableObjects/PilotsAndArtifacts/ArtifactStat.cs"
SPEC = "Docs/Pilots/pilots_and_artifacts.json"
ROSTER_DIR = PILOTS_DIR + "/Roster"
ARTIFACTS_DIR = PILOTS_DIR + "/Artifacts"
CLASS_LIST = "Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset"   # the Hangar's vessel list
OMEGA = {"Warden", "Bulwark", "Tempest"}                              # crews of other hulls: no pilots of their own
KIND = {"Add": 1, "Percent": 2, "Multiply": 3, "Set": 4}           # ArtifactValueKind
RANKS = 3
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


def yaml_any(s):
    """yaml_str for ASCII; a double-quoted, escaped scalar for anything else (Unity's own form)."""
    if all(32 <= ord(c) <= 126 for c in s):
        return yaml_str(s)
    out = "".join("\\\\" if c == "\\" else '\\"' if c == '"' else c if 32 <= ord(c) <= 126 else f"\\u{ord(c):04X}" for c in s)
    return f'"{out}"'


def pilot_asset(vessel, kit, vessel_value, script):
    name = f"Pilot_{vessel}_Default"
    description = (f"The {vessel}'s base kit, flown as shipped. Every other {vessel} pilot "
                   f"swaps part of it for something different.")
    return SO_HEADER.format(script=script, name=name) + (
        f"  id: pilot.{vessel.lower()}.default\n"
        f"  displayName: {yaml_str(kit)}\n"
        f"  ability: \n"
        f"  description: {yaml_str(description)}\n"
        f"  flavor: \n"
        f"  portrait: {{fileID: 0}}\n"
        f"  portraitIsStandIn: 0\n"
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


def catalog_asset(pilot_guids, script, artifact_guids=(), slots=3):
    lines = "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in pilot_guids)
    arts = "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in artifact_guids)
    return SO_HEADER.format(script=script, name="PilotArtifactCatalog") + (
        "  pilots:\n" + lines + ("  artifacts:\n" + arts if arts else "  artifacts: []\n")
        + f"  slotsPerVessel: {slots}\n"
    )


# ---------------------------------------------------------------- the roster (2026-10-26)

def load_spec():
    import json
    return json.loads(read(SPEC))


def file_stem(text):
    return re.sub(r"[^A-Za-z0-9]", "", text)


def asset_meta_guid(rel):
    m = re.search(r"^guid: ([0-9a-f]{32})\s*$", read(rel + ".meta"), re.M)
    assert m, f"no guid in {rel}.meta"
    return m.group(1)


def hangar_vessels():
    """The Hangar's hulls without the omega crews: the vessels that each get two pilots and two artifacts."""
    guids = re.findall(r"\{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}", read(CLASS_LIST))
    names = {}
    for f in os.listdir(os.path.join(ROOT, "Assets/_SO_Assets/Classes")):
        if f.startswith("SO_Class_") and f.endswith(".asset"):
            names[asset_meta_guid("Assets/_SO_Assets/Classes/" + f)] = f[len("SO_Class_"):-len(".asset")]
    return [names[g] for g in guids if g in names and names[g] not in OMEGA]


def parse_rank(label, kind=None):
    """'+10%' -> (10, Percent); 'x1.25' -> (1.25, Multiply); '+2 u' -> (2, Add). A given kind (the spec's 'Set') takes
    the label's first number. A two-number label ('+10% / +20%') reads its first."""
    first = label.split(" / ")[0].strip()
    if kind:
        m = re.search(r"-?\d+(?:\.\d+)?", first)
        assert m, f"rank label {label!r} has no number"
        return float(m.group(0)), kind
    if first.startswith("x"):
        return float(first[1:]), "Multiply"
    m = re.match(r"^([+-]?\d+(?:\.\d+)?)\s*(%|u|s)?$", first)
    assert m, f"rank label {label!r} has no number this generator reads"
    return float(m.group(1)), ("Percent" if m.group(2) == "%" else "Add")


def fnum(v):
    return str(int(v)) if float(v).is_integer() else repr(float(v))


def roster_pilot_asset(p, vessel_value, script):
    name = f"Pilot_{p['vessel']}_{file_stem(p['name'])}"
    portrait = "{fileID: 0}"
    if p.get("portrait"):
        portrait = f"{{fileID: 21300000, guid: {asset_meta_guid(p['portrait'])}, type: 3}}"
    return name, SO_HEADER.format(script=script, name=name) + (
        f"  id: {p['id']}\n"
        f"  displayName: {yaml_any(p['name'])}\n"
        f"  ability: {yaml_any(p['ability'])}\n"
        f"  description: {yaml_any(p['rule'])}\n"
        f"  flavor: \n"
        f"  portrait: {portrait}\n"
        f"  portraitIsStandIn: {1 if p.get('portraitIsStandIn') else 0}\n"
        f"  icon: {{fileID: 0}}\n"
        f"  requirement:\n"
        f"    Vessel: {vessel_value}\n"
        f"    Keywords: []\n"
        f"  isDefault: 0\n"
        f"  elementalBoost:\n"
        f"    Element: 0\n"
        f"    Petals: 0\n"
        f"  aiBehaviorDescription: {yaml_any(p['ai'])}\n"
    )


def artifact_asset(a, vessel_value, script):
    fits = "Fleet" if a["fits"] == "Fleet" else a["fits"]
    name = f"Artifact_{fits}_{file_stem(a['name'])}"
    parsed = [parse_rank(r, a.get("kind")) for r in a["ranks"]]
    return name, SO_HEADER.format(script=script, name=name) + (
        f"  id: {a['id']}\n"
        f"  displayName: {yaml_any(a['name'])}\n"
        f"  description: {yaml_any(a['changes'])}\n"
        f"  lore: \n"
        f"  icon: {{fileID: 0}}\n"
        f"  requirement:\n"
        f"    Vessel: {vessel_value}\n"
        f"    Keywords: []\n"
        f"  stat: {a['statId']}\n"
        "  rankLabels:\n" + "".join(f"  - {yaml_any(r)}\n" for r in a["ranks"])
        + "  rankValues:\n" + "".join(f"  - {fnum(v)}\n" for v, _ in parsed)
        + f"  kind: {KIND[parsed[0][1]]}\n"
        f"  atRisk: 1\n"
    )


def stat_cs(artifacts):
    rows = "".join(f"        {a['stat']} = {a['statId']},   // {a['name']} ({a['fits']}): {a['changes']}\n"
                   for a in sorted(artifacts, key=lambda a: a["statId"]))
    return ("// GENERATED by Tools/Build/author_pilot_assets.py from Docs/Pilots/pilots_and_artifacts.json - do not edit.\n"
            "// Values are serialized on every ArtifactSO: never renumber, never reuse one.\n"
            "namespace CosmicShore.ScriptableObjects\n{\n"
            "    /// <summary>The number an artifact changes: the key vessel code reads an artifact's value by.</summary>\n"
            "    public enum ArtifactStat\n    {\n        None = 0,\n" + rows + "    }\n}\n")


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
    vessels = hangar_vessels()
    ids = enum_values(VESSEL_CLASS_CS, "VesselClassType")
    pilots, artifacts = spec["pilots"], spec["artifacts"]
    if len(vessels) != 20:
        out.append(f"the Hangar lists {len(vessels)} vessels without the omega crews, not 20")
    def ascii_ok(t):
        return all(32 <= ord(c) <= 126 for c in t)
    for field, items in (("pilot", pilots), ("artifact", artifacts)):
        seen = [x["id"] for x in items]
        if len(set(seen)) != len(seen):
            out.append(f"two {field}s share an id")
        for x in items:
            if not re.fullmatch(r"(pilot|artifact)\.[a-z0-9]+\.[a-z0-9-]+", x["id"]):
                out.append(f"{field} {x['name']}: id {x['id']!r} is not lowercase-with-dots")
    if len({file_stem(p["name"]) + p["vessel"] for p in pilots}) != len(pilots):
        out.append("two pilots would share an asset name")
    for v in vessels:
        n = sum(1 for p in pilots if p["vessel"] == v)
        if n != 2:
            out.append(f"{v} has {n} pilots, not 2")
        n = sum(1 for a in artifacts if a["fits"] == v)
        if n != 2:
            out.append(f"{v} has {n} artifacts of its own, not 2")
    for p in pilots:
        if p["vessel"] not in vessels:
            out.append(f"pilot {p['name']} flies {p['vessel']}, which the Hangar does not list")
        for f in ("name", "ability", "rule", "ai"):
            if not p.get(f, "").strip():
                out.append(f"pilot {p['name']} has no {f}")
            elif not ascii_ok(p[f]):
                out.append(f"pilot {p['name']}: {f} is not printable ASCII (the UI font has no other glyphs)")
        if p.get("portrait"):
            if not os.path.exists(os.path.join(ROOT, p["portrait"] + ".meta")):
                out.append(f"pilot {p['name']}: portrait not in the repo")
        elif p.get("portraitIsStandIn"):
            out.append(f"pilot {p['name']}: a stand-in with no portrait")
    portraits = [p["portrait"] for p in pilots if p.get("portrait")]
    if len(set(portraits)) != len(portraits):
        out.append("two pilots share one portrait")
    stat_ids = [a["statId"] for a in artifacts]
    if not all(isinstance(i, int) and i > 0 for i in stat_ids):
        out.append("a statId is not a positive integer")
    if len(set(stat_ids)) != len(stat_ids):
        out.append("two artifacts share a statId")
    if len({a["stat"] for a in artifacts}) != len(artifacts):
        out.append("two artifacts share a stat name")
    if sum(1 for a in artifacts if a["fits"] == "Fleet") != 5:
        out.append("the fleet does not have five artifacts")
    for a in artifacts:
        if a["fits"] != "Fleet" and a["fits"] not in ids:
            out.append(f"artifact {a['name']} fits an unknown vessel {a['fits']}")
        if not re.fullmatch(r"[A-Z][A-Za-z0-9]*", a["stat"]):
            out.append(f"artifact {a['name']}: stat {a['stat']!r} is not a C# name")
        if len(a["ranks"]) != RANKS:
            out.append(f"artifact {a['name']} has {len(a['ranks'])} ranks, not {RANKS}")
        if a.get("kind") and a["kind"] not in KIND:
            out.append(f"artifact {a['name']}: unknown kind {a['kind']!r}")
        try:
            kinds = {parse_rank(r, a.get("kind"))[1] for r in a["ranks"]}
            if len(kinds) != 1:
                out.append(f"artifact {a['name']}: its ranks read as different kinds {sorted(kinds)}")
        except AssertionError as e:
            out.append(f"artifact {a['name']}: {e}")
        for t in [a["name"], a["changes"]] + a["ranks"]:
            if not t.strip() or not ascii_ok(t):
                out.append(f"artifact {a['name']}: {t!r} is empty or not printable ASCII")
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

    spec = load_spec() if spec is None else spec
    artifact_script = script_guid(ARTIFACT_SO_CS)
    out[ROSTER_DIR + ".meta"] = FOLDER_META.format(guid=guid_for("folder/Pilots/Roster"))
    out[ARTIFACTS_DIR + ".meta"] = FOLDER_META.format(guid=guid_for("folder/Pilots/Artifacts"))
    for p in spec["pilots"]:
        name, text = roster_pilot_asset(p, vessel_values[p["vessel"]], pilot_script)
        path = f"{ROSTER_DIR}/{name}.asset"
        guid = guid_for(f"Roster/{name}.asset")
        pilot_guids.append(guid)
        out[path] = text
        out[path + ".meta"] = ASSET_META.format(guid=guid)
    # Each vessel's own artifacts, then the fleet's: GetArtifactsFor keeps authored order, so a hull lists its own first.
    artifact_guids = []
    ordered = [a for a in spec["artifacts"] if a["fits"] != "Fleet"] + [a for a in spec["artifacts"] if a["fits"] == "Fleet"]
    for a in ordered:
        value = vessel_values["Any"] if a["fits"] == "Fleet" else vessel_values[a["fits"]]
        name, text = artifact_asset(a, value, artifact_script)
        path = f"{ARTIFACTS_DIR}/{name}.asset"
        guid = guid_for(f"Artifacts/{name}.asset")
        artifact_guids.append(guid)
        out[path] = text
        out[path + ".meta"] = ASSET_META.format(guid=guid)
    out[STAT_CS] = stat_cs(spec["artifacts"])
    out[STAT_CS + ".meta"] = SCRIPT_META.format(guid=guid_for("ArtifactStat.cs"))

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
    # Every asset in the roster and artifact folders is one this generator owns: a stray sits in no catalog.
    for d in (ROSTER_DIR, ARTIFACTS_DIR):
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
    pilots_part = catalog.split("  pilots:\n")[1].split("  artifacts:")[0]
    listed = re.findall(r"guid: ([0-9a-f]{32}), type: 2", pilots_part)
    expected = [guid_for(f"Pilot_{v}_Default.asset") for v, _, _ in MILESTONE]
    if listed[:len(expected)] != expected:
        failures.append("catalog does not list the default pilots first, in MILESTONE order")
    if len(listed) != len(expected) + len(load_spec()["pilots"]):
        failures.append("catalog does not list every roster pilot after the defaults")

    # 7. The roster's rules, each planted defect caught (2026-10-26).
    import copy
    spec = load_spec()
    if validate_spec(spec):
        failures.append("the shipped spec does not validate: " + "; ".join(validate_spec(spec)))
    plants = {
        "a vessel with three pilots": lambda s: s["pilots"].append(dict(s["pilots"][0], name="Extra", id="pilot.x.extra")),
        "a vessel with one pilot": lambda s: s["pilots"].pop(0),
        "two pilots share an id": lambda s: s["pilots"][1].update(id=s["pilots"][0]["id"]),
        "an id with capitals": lambda s: s["pilots"][1].update(id="pilot.Rhino.x"),
        "a pilot with no rule": lambda s: s["pilots"][2].update(rule=" "),
        "a pilot text the font cannot draw": lambda s: s["pilots"][2].update(ability="Sword \u00b7 throw"),
        "a pilot on a vessel the Hangar does not list": lambda s: s["pilots"][3].update(vessel="Grizzly"),
        "a stand-in with no portrait": lambda s: s["pilots"][4].update(portrait=None, portraitIsStandIn=True),
        "two pilots share a portrait": lambda s: s["pilots"][5].update(portrait=s["pilots"][6]["portrait"]),
        "two artifacts share a statId": lambda s: s["artifacts"][1].update(statId=s["artifacts"][0]["statId"]),
        "a statId of zero": lambda s: s["artifacts"][2].update(statId=0),
        "an artifact with two ranks": lambda s: s["artifacts"][3]["ranks"].pop(),
        "ranks of mixed kinds": lambda s: s["artifacts"][6]["ranks"].__setitem__(0, "x2"),
        "a vessel with three artifacts": lambda s: s["artifacts"][5].update(fits="Manta"),
        "a stat that is not a C# name": lambda s: s["artifacts"][9].update(stat="long barrel"),
        "a rank label the font cannot draw": lambda s: s["artifacts"][9]["ranks"].__setitem__(0, "4 \u2192 5"),
        "six slots": lambda s: s.update(slotsPerVessel=6),
    }
    for what, fn in plants.items():
        s = copy.deepcopy(spec)
        fn(s)
        if not validate_spec(s):
            failures.append(f"validate_spec missed: {what}")
    if parse_rank("+10%") != (10.0, "Percent") or parse_rank("x1.25") != (1.25, "Multiply") \
            or parse_rank("+2 u") != (2.0, "Add") or parse_rank("5 pellets", "Set") != (5.0, "Set") \
            or parse_rank("+15% / +35%") != (15.0, "Percent"):
        failures.append("parse_rank reads a label wrong")
    if yaml_any("4 \u2192 5") != '"4 \\u2192 5"':
        failures.append("yaml_any does not escape non-ASCII")
    tampered = dict(files)
    tampered[f"{ROSTER_DIR}/Pilot_Rhino_Tomahawk.asset"] = "tampered"
    if os.path.exists(os.path.join(ROOT, ROSTER_DIR, "Pilot_Rhino_Tomahawk.asset")) and \
            f"{ROSTER_DIR}/Pilot_Rhino_Tomahawk.asset" not in drift(tampered):
        failures.append("drift() did not report a tampered roster pilot")

    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else f"OK (7 checks, {len(plants)} planted roster defects caught)")
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
