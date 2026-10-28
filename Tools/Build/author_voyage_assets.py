#!/usr/bin/env python3
"""Author the voyage slice's CONTENT asset (Docs/Voyages/SLICE.md section 3).

A generator in the author_*_assets.py family: deterministic guids, idempotent, --check reports drift and writes
nothing, --self-test proves the checks fire. It owns:

  * Assets/Resources/Voyages/                       folder (VoyageContentSO.ResourcePath: the voyage loads it at runtime)
  * Assets/Resources/Voyages/VoyageContent.asset    the map, crew, jobs, events, seed news and lines, from
                                                    Docs/Voyages/voyage_slice.json
  * Assets/_SO_Assets/Toys/Toy_Voyage.asset         the Voyage toy (the spec's "toy" block)
  * Assets/Resources/Toybox.asset                   one line: the Voyage toy on the toybox's list (nothing else touched)

The asset's `cellPool` (which worlds the locations and lanes are) is the one hand-authored field: the generator
keeps whatever the asset already holds there, so a designer can drag worlds in without the next run undoing it.

Usage:
    python3 Tools/Build/author_voyage_assets.py             # write
    python3 Tools/Build/author_voyage_assets.py --check     # report drift, write nothing
    python3 Tools/Build/author_voyage_assets.py --self-test # negative controls
"""
import copy
import hashlib
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_pilot_assets import (  # noqa: E402  (shared YAML and enum helpers)
    ASSET_META, FOLDER_META, SO_HEADER, ROOT, ascii_ok, enum_values, fnum, read, script_guid, yaml_any,
)
from author_callsign_assets import CALLSIGNS  # noqa: E402

SPEC = "Docs/Voyages/voyage_slice.json"
PILOT_SPEC = "Docs/Pilots/pilots_and_artifacts.json"
OUT_DIR = "Assets/Resources/Voyages"
OUT = OUT_DIR + "/VoyageContent.asset"
SO_CS = "Assets/_Scripts/ScriptableObjects/Voyages/VoyageContentSO.cs"
TOY_CS = "Assets/_Scripts/ScriptableObjects/Voyages/VoyageToyDefinitionSO.cs"
TOY = "Assets/_SO_Assets/Toys/Toy_Voyage.asset"
TOYBOX = "Assets/Resources/Toybox.asset"
ARKWAY_SETTINGS_META = "Assets/_SO_Assets/Toys/Wander_WithArk.asset.meta"
ENUMS_CS = "Assets/_Scripts/Data/Voyages/VoyageEnums.cs"
DOMAINS_CS = "Assets/_Scripts/Data/Enums/Domains.cs"
ELEMENT_CS = "Assets/_Scripts/Data/Enums/Element.cs"
LINES_FIELDS = ["VoyageName", "BoardOpen", "Depart", "Arrived", "Vaults", "Wreck", "Hold", "HoldProgress", "Done",
                "Failed", "Stowed", "Extracted", "ArkLost", "BroughtBy", "NoWork", "Following"]


def guid_for(name):
    return hashlib.md5(f"CosmicShore/Voyages/{name}".encode()).hexdigest()


def load(rel):
    return json.loads(read(rel))


def pilot_ids():
    return {f"pilot.callsign.{word.lower()}" for _, word, _ in CALLSIGNS}


def artifact_ids():
    return {a["id"] for a in load(PILOT_SPEC)["artifacts"]}


def validate(spec):
    """Problems an author must fix, one per line (the C# VoyageContent.Validate checks the same at runtime)."""
    errors = []
    exp = enum_values(ENUMS_CS, "VoyageExperience")
    fac = enum_values(ENUMS_CS, "FactionType")
    kinds = enum_values(ENUMS_CS, "JobKind")
    tags = enum_values(ENUMS_CS, "NewsTag")
    places = enum_values(ENUMS_CS, "NewsPlace")
    abouts = enum_values(ENUMS_CS, "NewsAbout")
    elements = enum_values(ELEMENT_CS, "Element")
    pilots, artifacts = pilot_ids(), artifact_ids()

    def text(where, t):
        if not isinstance(t, str) or not ascii_ok(t):
            errors.append(f"{where}: text must be printable ASCII")

    locs = {}
    for l in spec["locations"]:
        if l["id"] in locs:
            errors.append(f"location id {l['id']} is used twice")
        locs[l["id"]] = l
        if l["experience"] not in exp:
            errors.append(f"{l['id']}: unknown experience {l['experience']}")
        text(l["id"], l["name"])
        text(l["id"], l.get("blurb", ""))
        if len(l.get("chart", [])) != 3:
            errors.append(f"{l['id']}: chart needs three numbers")
        if l["experience"] == "VaultField":
            if l.get("vaults", 0) < 1 or not l.get("finds"):
                errors.append(f"{l['id']}: a vault field needs vaults and finds")
            for a in l.get("finds", []):
                if a not in artifacts:
                    errors.append(f"{l['id']}: find {a} is no artifact in {PILOT_SPEC}")
        if l["experience"] == "Harbour" and l.get("home") not in ("Jade", "Ruby", "Gold"):
            errors.append(f"{l['id']}: a harbour needs a home domain (Jade, Ruby or Gold)")
    for d in ("Jade", "Ruby", "Gold"):
        if not any(l["experience"] == "Harbour" and l.get("home") == d for l in spec["locations"]):
            errors.append(f"no harbour for {d}")

    adj = {k: set() for k in locs}
    for lane in spec["lanes"]:
        for end in ("from", "to"):
            if lane[end] not in locs:
                errors.append(f"a lane names unknown location {lane[end]}")
        if lane["from"] in adj and lane["to"] in adj:
            adj[lane["from"]].add(lane["to"])
            adj[lane["to"]].add(lane["from"])
        if lane.get("cells", 1) < 0:
            errors.append(f"lane {lane['from']}-{lane['to']} has a negative length")
    # Every location can be reached from every harbour: a job may point anywhere.
    if locs:
        start = next(iter(locs))
        seen, todo = {start}, [start]
        while todo:
            for n in adj.get(todo.pop(), ()):
                if n not in seen:
                    seen.add(n)
                    todo.append(n)
        for k in locs:
            if k not in seen:
                errors.append(f"{k} cannot be reached by any lane")

    seen_pilots = set()
    for p in spec["crew"]:
        if p["pilot"] not in pilots:
            errors.append(f"crew {p['name']}: {p['pilot']} is no callsign pilot")
        if p["pilot"] in seen_pilots:
            errors.append(f"two crew people on {p['pilot']} (one person per callsign per crew)")
        seen_pilots.add(p["pilot"])
        if p["faction"] not in fac or p["faction"] == "None":
            errors.append(f"crew {p['name']}: unknown faction type {p['faction']}")
        text(p["name"], p["name"])
        text(p["name"], p.get("story", ""))
    for missing in sorted(pilots - seen_pilots):
        errors.append(f"no crew person on {missing}: every callsign pilot brings work")

    events = {e["id"] for e in spec["events"]}
    job_ids = set()
    for j in spec["jobs"]:
        if j["id"] in job_ids:
            errors.append(f"job id {j['id']} is used twice")
        job_ids.add(j["id"])
        if j["kind"] not in kinds:
            errors.append(f"{j['id']}: unknown kind {j['kind']}")
        if j["faction"] not in fac or j["faction"] == "None":
            errors.append(f"{j['id']}: unknown faction type {j['faction']}")
        if j["needs"] not in tags:
            errors.append(f"{j['id']}: unknown news tag {j['needs']}")
        if j["goes"] not in exp:
            errors.append(f"{j['id']}: unknown experience {j['goes']}")
        if j["element"] not in elements:
            errors.append(f"{j['id']}: unknown element {j['element']}")
        for k in ("onSuccess", "onFail"):
            if j[k] and j[k] not in events:
                errors.append(f"{j['id']}: {k} names unknown event {j[k]}")
        for k in ("title", "summary", "success", "fail"):
            text(j["id"], j[k])
    if "job.return" not in job_ids:
        errors.append("no job.return (the way home every board offers)")
    for f in fac:
        if f != "None" and not any(j["faction"] == f and j["needs"] == "None" and j["id"] != "job.return"
                                   for j in spec["jobs"]) and f != "Pirate":
            errors.append(f"faction type {f} has no standing work: its pilots would bring nothing anywhere")

    for e in spec["events"]:
        for w in e["writes"]:
            if w["tag"] not in tags or w["at"] not in places or w["about"] not in abouts:
                errors.append(f"{e['id']}: a write names an unknown tag, place or subject")
            text(e["id"], w["line"])
    for s in spec["seeds"]:
        if s["port"] not in locs or s["about"] not in locs:
            errors.append(f"seed news at {s['port']} about {s['about']} names an unknown location")
        elif locs[s["port"]]["experience"] not in ("Port", "Harbour"):
            errors.append(f"seed news at {s['port']}: only ports and harbours keep news")
        if s["tag"] not in tags:
            errors.append(f"seed news: unknown tag {s['tag']}")
        text("seed", s["line"])
    for w in spec["seedWrecks"]:
        if w["at"] not in locs:
            errors.append(f"seed wreck at unknown location {w['at']}")
        for a in w["artifacts"]:
            if a not in artifacts:
                errors.append(f"seed wreck at {w['at']}: {a} is no artifact")
    for k, v in spec.get("lines", {}).items():
        text(f"lines.{k}", v)
    for k in LINES_FIELDS:
        if k not in spec.get("hud", {}):
            errors.append(f"hud.{k} is missing (an empty string deletes a line; a missing key is a typo)")
        else:
            text(f"hud.{k}", spec["hud"][k])
    return errors


def ystr(s):
    out = yaml_any(s)
    return out


def emit(spec, cell_pool_block):
    exp = enum_values(ENUMS_CS, "VoyageExperience")
    fac = enum_values(ENUMS_CS, "FactionType")
    kinds = enum_values(ENUMS_CS, "JobKind")
    tags = enum_values(ENUMS_CS, "NewsTag")
    places = enum_values(ENUMS_CS, "NewsPlace")
    abouts = enum_values(ENUMS_CS, "NewsAbout")
    domains = enum_values(DOMAINS_CS, "Domains")
    elements = enum_values(ELEMENT_CS, "Element")

    def strings(indent, values):
        if not values:
            return " []\n"
        return "\n" + "".join(f"{indent}- {ystr(v)}\n" for v in values)

    o = SO_HEADER.format(script=script_guid(SO_CS), name="VoyageContent")
    o += "  content:\n    Locations:\n"
    for l in spec["locations"]:
        x, y, z = l["chart"]
        o += (f"    - Id: {ystr(l['id'])}\n      Name: {ystr(l['name'])}\n      Blurb: {ystr(l.get('blurb', ''))}\n"
              f"      Experience: {exp[l['experience']]}\n      Home: {domains[l.get('home', 'Blue')]}\n"
              f"      ChartX: {fnum(x)}\n      ChartY: {fnum(y)}\n      ChartZ: {fnum(z)}\n"
              f"      Cell: {l['cell']}\n      Vaults: {l.get('vaults', 0)}\n"
              f"      Finds:{strings('      ', l.get('finds', []))}"
              f"      HoldSeconds: {fnum(l.get('hold', 60))}\n")
    o += "    Lanes:\n"
    for lane in spec["lanes"]:
        o += f"    - From: {ystr(lane['from'])}\n      To: {ystr(lane['to'])}\n      Cells: {lane.get('cells', 1)}\n"
    o += "    Crew:\n"
    for p in spec["crew"]:
        o += (f"    - PilotId: {ystr(p['pilot'])}\n      Name: {ystr(p['name'])}\n      Faction: {fac[p['faction']]}\n"
              f"      Story: {ystr(p.get('story', ''))}\n")
    o += "    Jobs:\n"
    for j in spec["jobs"]:
        o += (f"    - Id: {ystr(j['id'])}\n      Kind: {kinds[j['kind']]}\n      Faction: {fac[j['faction']]}\n"
              f"      Needs: {tags[j['needs']]}\n      Goes: {exp[j['goes']]}\n"
              f"      Title: {ystr(j['title'])}\n      Summary: {ystr(j['summary'])}\n"
              f"      SuccessLine: {ystr(j['success'])}\n      FailLine: {ystr(j['fail'])}\n"
              f"      Reward: {j['reward']}\n      RewardElement: {elements[j['element']]}\n"
              f"      OnSuccess: {ystr(j['onSuccess'])}\n      OnFail: {ystr(j['onFail'])}\n")
    o += "    Events:\n"
    for e in spec["events"]:
        o += f"    - Id: {ystr(e['id'])}\n      Writes:"
        if not e["writes"]:
            o += " []\n"
        else:
            o += "\n"
            for w in e["writes"]:
                o += (f"      - Tag: {tags[w['tag']]}\n        At: {places[w['at']]}\n        About: {abouts[w['about']]}\n"
                      f"        Line: {ystr(w['line'])}\n")
        o += f"      SpendsNews: {1 if e.get('spendsNews') else 0}\n"
    o += "    Seeds:\n"
    for s in spec["seeds"]:
        o += (f"    - Port: {ystr(s['port'])}\n      Tag: {tags[s['tag']]}\n      About: {ystr(s['about'])}\n"
              f"      Line: {ystr(s['line'])}\n")
    o += "    SeedWrecks:\n"
    for w in spec["seedWrecks"]:
        o += f"    - At: {ystr(w['at'])}\n      Artifacts:{strings('      ', w['artifacts'])}      Line: {ystr(w['line'])}\n"
    lines = spec.get("lines", {})
    o += f"    WreckLine: {ystr(lines.get('wreck', ''))}\n    WreckNewsLine: {ystr(lines.get('wreckNews', ''))}\n"
    o += cell_pool_block
    o += "  lines:\n"
    hud = spec.get("hud", {})
    for k in LINES_FIELDS:
        o += f"    {k}: {ystr(hud.get(k, ''))}\n"
    return o


CELL_POOL = re.compile(r"^  cellPool:(?: \[\]\n|\n(?:  - .*\n(?:    .*\n)*)*)", re.M)


def color(c):
    return "{" + f"r: {fnum(c[0])}, g: {fnum(c[1])}, b: {fnum(c[2])}, a: {fnum(c[3])}" + "}"


def vec3(v):
    return "{" + f"x: {fnum(v[0])}, y: {fnum(v[1])}, z: {fnum(v[2])}" + "}"


def meta_guid(rel):
    return re.search(r"^guid: ([0-9a-f]{32})", read(rel), re.M).group(1)


def emit_toy(spec):
    t = spec["toy"]
    out = SO_HEADER.format(script=script_guid(TOY_CS), name="Toy_Voyage")
    out += (f"  id: {t['id']}\n  displayName: {yaml_any(t['displayName'])}\n  description: {yaml_any(t['description'])}\n"
            f"  unlockedByDefault: 1\n  placementAngleDegrees: {fnum(t['placementAngleDegrees'])}\n"
            f"  accentColor: {color(t['accent'])}\n"
            f"  withArk: {{fileID: 11400000, guid: {meta_guid(ARKWAY_SETTINGS_META)}, type: 2}}\n"
            f"  content: {{fileID: 11400000, guid: {guid_for('VoyageContent.asset')}, type: 2}}\n")
    for k in ("boardDistance", "beaconSpacing", "beaconBodyRadius", "beaconTriggerRadius"):
        out += f"  {k}: {fnum(t[k])}\n"
    out += f"  jobColor: {color(t['jobColor'])}\n  homeColor: {color(t['homeColor'])}\n"
    out += f"  seedElementLevel: {fnum(t['seedElementLevel'])}\n  wingmateLeash: {fnum(t['wingmateLeash'])}\n"
    out += f"  findDistance: {fnum(t['findDistance'])}\n  vaultRadius: {fnum(t['vaultRadius'])}\n"
    out += f"  vaultPrisms: {int(t['vaultPrisms'])}\n  vaultPrismScale: {vec3(t['vaultPrismScale'])}\n"
    out += f"  vaultOpenShare: {fnum(t['vaultOpenShare'])}\n  holdRadius: {fnum(t['holdRadius'])}\n"
    out += f"  chartSize: {fnum(t['chartSize'])}\n  chartOffset: {vec3(t['chartOffset'])}\n"
    for k in ("portColor", "vaultColor", "coreColor", "laneColor"):
        out += f"  {k}: {color(t[k])}\n"
    out += "  cardArt: {fileID: 0}\n"
    for k in ("shellLabel", "shellDetail", "shellSetSailVerb", "shellUnderwayLabel", "shellUnderwayDetail",
              "shellGiveUpVerb"):
        out += f"  {k}: {yaml_any(t['menu'][k])}\n"
    return out


def with_toy(toybox_text):
    """The toybox asset with the Voyage toy on its list (appended once); everything else unchanged."""
    entry = f"  - {{fileID: 11400000, guid: {guid_for('Toy_Voyage.asset')}, type: 2}}\n"
    if entry in toybox_text:
        return toybox_text
    m = re.search(r"^  toys:\n(?:  - .*\n)*", toybox_text, re.M)
    assert m, "Toybox.asset has no toys list"
    return toybox_text[:m.end()] + entry + toybox_text[m.end():]


def current_cell_pool():
    full = os.path.join(ROOT, OUT)
    if os.path.exists(full):
        m = CELL_POOL.search(read(OUT))
        if m:
            return m.group(0)
    return "  cellPool: []\n"


def build(spec=None):
    spec = spec or load(SPEC)
    return {
        OUT_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder")),
        OUT: emit(spec, current_cell_pool()),
        OUT + ".meta": ASSET_META.format(guid=guid_for("VoyageContent.asset")),
        TOY: emit_toy(spec),
        TOY + ".meta": ASSET_META.format(guid=guid_for("Toy_Voyage.asset")),
        TOYBOX: with_toy(read(TOYBOX)),
    }


def drift(files):
    return [rel for rel, text in files.items()
            if not os.path.exists(os.path.join(ROOT, rel)) or read(rel) != text]


def self_test():
    failures = []
    spec = load(SPEC)
    if validate(spec):
        failures.append("the shipped spec does not validate: " + "; ".join(validate(spec)))
    plants = {
        "an unknown lane end": lambda s: s["lanes"].append({"from": "loc.nowhere", "to": "loc.tollmoor", "cells": 1}),
        "an unreachable location": lambda s: s["locations"].append(dict(s["locations"][3], id="loc.island")),
        "a missing harbour": lambda s: s["locations"].pop(0),
        "a vault field with nothing to find": lambda s: next(l for l in s["locations"] if l["experience"] == "VaultField").update(finds=[]),
        "a find that is no artifact": lambda s: next(l for l in s["locations"] if l["experience"] == "VaultField").update(finds=["artifact.nope"]),
        "two people on one callsign": lambda s: s["crew"].append(dict(s["crew"][0], name="Twin")),
        "a callsign with no person": lambda s: s["crew"].pop(),
        "an unknown event": lambda s: s["jobs"][1].update(onSuccess="ev.nope"),
        "a job with no faction": lambda s: s["jobs"][1].update(faction="None"),
        "no way home": lambda s: s["jobs"].pop(0),
        "text the font cannot draw": lambda s: s["jobs"][1].update(title="carry → home"),
        "seed news at a vault field": lambda s: s["seeds"].append({"port": "loc.lantern", "tag": "Aid", "about": "loc.verdigris", "line": "x"}),
    }
    for what, fn in plants.items():
        s = copy.deepcopy(spec)
        fn(s)
        if not validate(s):
            failures.append(f"validate missed: {what}")
    files = build(spec)
    tampered = dict(files)
    tampered[OUT] = files[OUT].replace("Cell: 0", "Cell: 9")
    if os.path.exists(os.path.join(ROOT, OUT)) and OUT not in drift(tampered):
        failures.append("drift() did not report a tampered asset")
    # The hand-authored cell pool survives a regeneration.
    pool = "  cellPool:\n  - {fileID: 11400000, guid: 0123456789abcdef0123456789abcdef, type: 2}\n"
    text = emit(spec, pool)
    m = CELL_POOL.search(text)
    if not m or m.group(0) != pool:
        failures.append("the cell pool block is not kept as authored")
    if with_toy(with_toy(read(TOYBOX))) != with_toy(read(TOYBOX)):
        failures.append("the toybox line is not idempotent")
    if with_toy(read(TOYBOX)).count(guid_for("Toy_Voyage.asset")) != 1:
        failures.append("the Voyage toy is not on the toybox list exactly once")
    if emit(spec, current_cell_pool()) != emit(spec, current_cell_pool()):
        failures.append("emit is not deterministic")
    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else f"OK ({len(plants)} planted spec defects caught)")
    return 1 if failures else 0


def main():
    if "--self-test" in sys.argv:
        return self_test()
    spec = load(SPEC)
    problems = validate(spec)
    if problems:
        for p in problems:
            print("SPEC:", p)
        return 1
    files = build(spec)
    changed = drift(files)
    if "--check" in sys.argv:
        for rel in changed:
            print("DRIFT:", rel)
        print(f"check: {len(files)} files, {len(changed)} drifted")
        return 1 if changed else 0
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
