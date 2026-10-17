#!/usr/bin/env python3
"""Author every hull's Regatta RACE LINE style (RaceLineStyleSO) into Assets/Resources/RaceLines/<Hull>_Regatta.asset.

Every hull races its own line (Yash, 2026-10-17: "no vessels move in the same way ... it's always better to play from the
sides"): RegattaController flies each AI hull along the GAP between two rails (RegattaRaceLine) at the hull's distance from
the braid, with the hull's room and risk. The numbers below are the starting values, reasoned from each hull's size and
speed; a person's flights of a hull REPLACE them: learn_drives_from_flights.py --write measures where they cross the rings
and writes race_lines_learned.json beside this script, which this generator reads on top of the defaults.

The Squirrel (its own Skim Race pilot) and the Urchin (rides its own lane) have no style here on purpose.

    python3 Tools/Build/author_race_lines.py            # write the assets
    python3 Tools/Build/author_race_lines.py --check    # fail on drift (what a re-run would change), and on bad values
"""
import hashlib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "RaceLines")      # RaceLineStyleSO.ResourceFolder
SCRIPT = os.path.join(REPO, "Assets", "_Scripts", "Controller", "AI", "Racing", "RaceLineStyleSO.cs")
LEARNED = os.path.join(HERE, "race_lines_learned.json")
MODE = "Regatta"

# Field order = the SO's. LaneShare: 0 = the rails' radius in the gap, 1 = as far out as the tightest mouth allows.
FIELDS = ["LaneShare", "HullClearance", "Risk", "RiskSpread", "CarefulLookaheadSeconds", "BoldLookaheadSeconds",
          "MinLookahead", "MaxLookahead"]
COMMON = {"RiskSpread": 0.15, "CarefulLookaheadSeconds": 0.35, "BoldLookaheadSeconds": 0.8, "MinLookahead": 60,
          "MaxLookahead": 520}
RANGES = {"LaneShare": (0, 1), "HullClearance": (0, 60), "Risk": (0, 1), "RiskSpread": (0, 0.5),
          "CarefulLookaheadSeconds": (0, 3), "BoldLookaheadSeconds": (0, 3), "MinLookahead": (1, 400),
          "MaxLookahead": (1, 2000)}
# The starting values, per hull (why, in a word): big and fast flies far out and plays safer; agile flies closer and bolder.
HULLS = {
    "Rhino":   {"LaneShare": 0.80, "HullClearance": 18, "Risk": 0.40},   # the biggest hull, 840-1200 u/s on the ramp
    "Manta":   {"LaneShare": 0.70, "HullClearance": 16, "Risk": 0.50},   # wide wings, 720 u/s Soar
    "Dolphin": {"LaneShare": 0.60, "HullClearance": 12, "Risk": 0.50},   # a drift locks its course: room to slide
    "Stoat":   {"LaneShare": 0.55, "HullClearance": 10, "Risk": 0.60},   # a 72 u turning circle at any warp
    "Serpent": {"LaneShare": 0.60, "HullClearance": 12, "Risk": 0.50},
    "Sparrow": {"LaneShare": 0.55, "HullClearance": 10, "Risk": 0.50},
    "Scarab":  {"LaneShare": 0.60, "HullClearance": 12, "Risk": 0.45},   # cannot slow quickly: a little more care
}


def guid(key):
    return hashlib.md5(("cosmic-shore/" + key).encode()).hexdigest()


def script_guid():
    return re.search(r"guid: ([0-9a-f]{32})", open(SCRIPT + ".meta", encoding="utf-8").read()).group(1)


def learned():
    if not os.path.exists(LEARNED):
        return {}
    return json.load(open(LEARNED, encoding="utf-8")).get("hulls", {})


def values(hull):
    v = dict(COMMON)
    v.update(HULLS[hull])
    v.update({k: x for k, x in learned().get(hull, {}).items() if k in FIELDS})
    return v


def fmt(x):
    return str(int(x)) if float(x).is_integer() else repr(round(float(x), 4))


def asset_text(hull):
    name = f"{hull}_{MODE}"
    v = values(hull)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid()}, type: 3}}\n  m_Name: {name}\n  m_EditorClassIdentifier: \n"
            + "".join(f"  {k}: {fmt(v[k])}\n" for k in FIELDS))


def native_meta(g):
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def folder_meta(g):
    return f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def outputs():
    files = {OUT_DIR + ".meta": folder_meta(guid("folder/Resources/RaceLines"))}
    for hull in HULLS:
        path = os.path.join(OUT_DIR, f"{hull}_{MODE}.asset")
        files[path] = asset_text(hull)
        files[path + ".meta"] = native_meta(guid(f"asset/RaceLines/{hull}_{MODE}"))
    return files


def validate():
    problems = []
    for hull in HULLS:
        v = values(hull)
        for k in FIELDS:
            lo, hi = RANGES[k]
            if not lo <= float(v[k]) <= hi:
                problems.append(f"{hull}: {k} {v[k]} outside {RANGES[k]}")
        if v["MaxLookahead"] < v["MinLookahead"]:
            problems.append(f"{hull}: MaxLookahead under MinLookahead")
    for hull in learned():
        if hull not in HULLS:
            problems.append(f"race_lines_learned.json names {hull}, which has no style (Squirrel / Urchin fly their own)")
    return problems


def main():
    check = "--check" in sys.argv
    problems = validate()
    files = outputs()
    drift = [p for p, t in files.items() if not os.path.exists(p) or open(p, encoding="utf-8").read() != t]
    if check:
        for p in problems:
            print("check:", p)
        for p in drift:
            print("check: would change", os.path.relpath(p, REPO))
        ok = not problems and not drift
        print(f"author_race_lines --check: {'OK' if ok else 'FAILED'} ({len(HULLS)} hulls, {len(learned())} learned)")
        return 0 if ok else 1
    if problems:
        for p in problems:
            print("error:", p)
        return 1
    os.makedirs(OUT_DIR, exist_ok=True)
    for p, t in files.items():
        if not os.path.exists(p) or open(p, encoding="utf-8").read() != t:
            open(p, "w", encoding="utf-8").write(t)
            print("wrote", os.path.relpath(p, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main())
