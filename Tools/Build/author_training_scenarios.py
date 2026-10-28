#!/usr/bin/env python3
"""The AI trainer's per-mode assets: Scenario_<Token>.asset and FitnessProfile_<Token>.asset under
Assets/_SO_Assets/AI Training/, one pair per TrainingModeCatalog row.

    python3 Tools/Build/author_training_scenarios.py            write the pairs that are missing
    python3 Tools/Build/author_training_scenarios.py --check    every row's pair exists and matches the C# (exit 1 if not)
    python3 Tools/Build/author_training_scenarios.py --self-test

Reads the C# as the one source: the rows of TrainingModeCatalog.Live (Runner/TrainingModeCatalog.cs) and, for the modes
added 2026-10-28, FitnessProfileSO.MetricRecipes (Fitness/FitnessProfileSO.cs: one row per line). It never rewrites a
pair that exists (the first twelve were authored by hand and are checked, not regenerated). A new pair's guids are
derived from its token, so writing it twice gives the same files. The edit-mode test
AITrainingCoreTests.Catalog_EveryLiveMode_HasScenarioAndProfileMatchingApplyFor checks the same in Unity.
"""
import argparse
import json
import os
import re
import sys
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TRAIN = "Assets/_Scripts/Utility/AITraining"
CATALOG = f"{TRAIN}/Runner/TrainingModeCatalog.cs"
PROFILE_CS = f"{TRAIN}/Fitness/FitnessProfileSO.cs"
SENSOR_CS = f"{TRAIN}/Sensors/TargetSensor.cs"
GAMEMODES_CS = "Assets/_Scripts/Data/Enums/GameModes.cs"
VESSELS_CS = "Assets/_Scripts/Data/Enums/VesselClassType.cs"
ASSETS = "Assets/_SO_Assets/AI Training"
SCENARIO_SCRIPT = f"{TRAIN}/Runner/TrainingScenarioSO.cs.meta"
PROFILE_SCRIPT = f"{TRAIN}/Fitness/FitnessProfileSO.cs.meta"

ROW = re.compile(r'new\(GameModes\.(\w+),\s*"(\w+)",\s*"([^"]*)",\s*VesselClassType\.(\w+),\s*(true|false),\s*(\d+),\s*'
                 r'TargetSensor\.TargetMode\.(\w+),\s*"(\w+)",\s*"((?:[^"\\]|\\.)*)"\)', re.S)
RECIPE = re.compile(r'new\(GameModes\.(\w+), ComponentKind\.(\w+), ([\d.]+)f, "([^"]*)", "((?:[^"\\]|\\.)*)"\),')


def read(rel, root=ROOT):
    with open(os.path.join(root, rel), encoding="utf-8") as fh:
        return fh.read()


def enum_values(text, name=None):
    if name:
        m = re.search(rf"enum {name}\s*\{{(.*?)\}}", text, re.S)
        text = m.group(1) if m else ""
    return {k: int(v) for k, v in re.findall(r"^\s*(\w+)\s*=\s*(-?\d+)\s*,", text, re.M)}


def script_guid(meta_rel, root=ROOT):
    return re.search(r"guid: ([0-9a-f]{32})", read(meta_rel, root)).group(1)


def model(root=ROOT):
    rows = [dict(mode=m, token=t, display=d, vessel=v, locked=l == "true", players=int(n), target=tm, scene=sc)
            for m, t, d, v, l, n, tm, sc, _ in ROW.findall(read(CATALOG, root))]
    recipes = {m: dict(kind=k, weight=float(w), label=lb, description=json.loads('"' + ds + '"'))
               for m, k, w, lb, ds in RECIPE.findall(read(PROFILE_CS, root))}
    return {"rows": rows, "recipes": recipes,
            "modes": enum_values(read(GAMEMODES_CS, root), "GameModes"),
            "vessels": enum_values(read(VESSELS_CS, root), "VesselClassType"),
            "targets": enum_values(read(SENSOR_CS, root), "TargetMode"),
            "kinds": enum_values(read(PROFILE_CS, root), "ComponentKind"),
            "scenario_script": script_guid(SCENARIO_SCRIPT, root), "profile_script": script_guid(PROFILE_SCRIPT, root)}


def guid_for(token, what):
    return uuid.uuid5(uuid.NAMESPACE_URL, f"froglet-studio/cosmic-shore/ai-training/{what}/{token}").hex


def yq(s):
    return json.dumps(s, ensure_ascii=False)


def num(x):
    return str(int(x)) if float(x).is_integer() else repr(float(x))


def meta(guid):
    return (f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n"
            "  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


HEADER = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
          "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
          "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n")


def profile_yaml(m, row, rec):
    entries = "".join(f"  - Kind: {m['kinds'][k]}\n    Weight: {num(w)}\n    Label: {lb}\n"
                      for k, w, lb in ((rec["kind"], rec["weight"], rec["label"]), ("TimePenalty", 1, "TimePenalty")))
    return (HEADER + f"  m_Script: {{fileID: 11500000, guid: {m['profile_script']}, type: 3}}\n"
            f"  m_Name: FitnessProfile_{row['token']}\n  m_EditorClassIdentifier: \n"
            f"  Description: {yq(rec['description'])}\n  Entries:\n" + entries)


def scenario_yaml(m, row, profile_guid):
    return (HEADER + f"  m_Script: {{fileID: 11500000, guid: {m['scenario_script']}, type: 3}}\n"
            f"  m_Name: Scenario_{row['token']}\n  m_EditorClassIdentifier: \n"
            f"  DisplayName: {yq(row['display'])}\n  GameMode: {m['modes'][row['mode']]}\n  Vessel: {m['vessels'][row['vessel']]}\n"
            "  Intensity: 4\n  PopulationSize: 24\n  EliteCount: 4\n  NumericMutationRate: 0.3\n  NumericMutationStrength: 0.18\n"
            "  StructuralMutationRate: 0.04\n  NoveltyWeight: 0.15\n  MaxEpisodeSeconds: 120\n  MinEpisodeSeconds: 5\n"
            f"  OpponentCount: {row['players']}\n  OpponentsUseTrainedGenome: 0\n  TargetMode: {m['targets'][row['target']]}\n"
            f"  FitnessProfile: {{fileID: 11400000, guid: {profile_guid}, type: 2}}\n  UseResetForReplay: 1\n"
            "  DelayBetweenEpisodes: 1\n  EarlyExitConditions: []\n")


def paths(row, root=ROOT):
    base = os.path.join(root, ASSETS)
    return (os.path.join(base, "Scenarios", f"Scenario_{row['token']}.asset"),
            os.path.join(base, "Profiles", f"FitnessProfile_{row['token']}.asset"))


def field(text, key):
    mm = re.search(rf"^  {key}: (.*)$", text, re.M)
    return mm.group(1).strip() if mm else None


def problems(m, root=ROOT):
    """Why the assets do not match the C#, per row: [] when they all do."""
    bad = []
    if len(m["rows"]) < 12:
        bad.append(f"only {len(m['rows'])} catalog rows parsed from {CATALOG} (the row pattern no longer matches)")
    tokens = [r["token"] for r in m["rows"]]
    if len(set(tokens)) != len(tokens):
        bad.append("a token appears twice in the catalog")
    for mode in m["recipes"]:
        if mode not in {r["mode"] for r in m["rows"]}:
            bad.append(f"{mode} has a MetricRecipe but no catalog row")
    for row in m["rows"]:
        t = row["token"]
        for k, table in (("mode", "modes"), ("vessel", "vessels"), ("target", "targets")):
            if row[k] not in m[table]:
                bad.append(f"{t}: {row[k]} is not in the {table} enum")
        sp, pp = paths(row, root)
        if not (os.path.exists(sp) and os.path.exists(pp) and os.path.exists(sp + ".meta") and os.path.exists(pp + ".meta")):
            bad.append(f"{t}: the scenario or profile asset (or its .meta) is missing - run this script")
            continue
        s, p = open(sp, encoding="utf-8").read(), open(pp, encoding="utf-8").read()
        pguid = re.search(r"guid: ([0-9a-f]{32})", open(pp + ".meta").read()).group(1)
        want = {"GameMode": str(m["modes"].get(row["mode"])), "Vessel": str(m["vessels"].get(row["vessel"])),
                "Intensity": "4", "OpponentCount": str(row["players"]), "TargetMode": str(m["targets"].get(row["target"])),
                "PopulationSize": "24", "MaxEpisodeSeconds": "120", "EarlyExitConditions": "[]"}
        for k, v in want.items():
            if field(s, k) != v:
                bad.append(f"{t}: scenario {k} is {field(s, k)}, the catalog says {v}")
        if f"guid: {pguid}" not in (field(s, "FitnessProfile") or ""):
            bad.append(f"{t}: the scenario does not point at FitnessProfile_{t}")
        rec = m["recipes"].get(row["mode"])
        if rec:
            if field(p, "Description") != yq(rec["description"]):
                bad.append(f"{t}: the profile's Description differs from FitnessProfileSO.MetricRecipes")
            kinds = [int(x) for x in re.findall(r"- Kind: (\d+)", p)]
            if kinds != [m["kinds"][rec["kind"]], m["kinds"]["TimePenalty"]]:
                bad.append(f"{t}: the profile's rows are {kinds}, the recipe says {rec['kind']} + TimePenalty")
    return bad


def write(m, root=ROOT, quiet=False):
    made = []
    for row in m["rows"]:
        sp, pp = paths(row, root)
        if os.path.exists(sp) and os.path.exists(pp):
            continue
        rec = m["recipes"].get(row["mode"])
        if rec is None:
            print(f"{row['token']}: no MetricRecipe and no assets - author its recipe in FitnessProfileSO first")
            continue
        os.makedirs(os.path.dirname(sp), exist_ok=True)
        os.makedirs(os.path.dirname(pp), exist_ok=True)
        pg, sg = guid_for(row["token"], "profile"), guid_for(row["token"], "scenario")
        for path, text in ((pp, profile_yaml(m, row, rec)), (pp + ".meta", meta(pg)),
                           (sp, scenario_yaml(m, row, pg)), (sp + ".meta", meta(sg))):
            with open(path, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(text)
        made.append(row["token"])
    if not quiet:
        print(f"wrote {len(made)} scenario/profile pair(s)" + (": " + ", ".join(made) if made else ""))
    return made


def self_test():
    """Against a copy of the real files: a fresh pair is written and passes; a drifted field, a missing asset and a
    recipe without a row each fail (negative controls)."""
    import shutil
    import tempfile
    bad = []
    tmp = tempfile.mkdtemp(prefix="training-scenarios-")
    try:
        for rel in (CATALOG, PROFILE_CS, SENSOR_CS, GAMEMODES_CS, VESSELS_CS, SCENARIO_SCRIPT, PROFILE_SCRIPT):
            os.makedirs(os.path.dirname(os.path.join(tmp, rel)), exist_ok=True)
            shutil.copy2(os.path.join(ROOT, rel), os.path.join(tmp, rel))
        shutil.copytree(os.path.join(ROOT, ASSETS, "Scenarios"), os.path.join(tmp, ASSETS, "Scenarios"))
        shutil.copytree(os.path.join(ROOT, ASSETS, "Profiles"), os.path.join(tmp, ASSETS, "Profiles"))
        m = model(tmp)
        if not m["recipes"]:
            bad.append("no MetricRecipes parsed")
        tok = next(r["token"] for r in m["rows"] if r["mode"] in m["recipes"])
        for p in paths(next(r for r in m["rows"] if r["token"] == tok), tmp):
            for f in (p, p + ".meta"):
                if os.path.exists(f):
                    os.remove(f)
        write(m, tmp, quiet=True)
        if problems(m, tmp):
            bad.append("a freshly written pair fails the check: " + "; ".join(problems(m, tmp))[:400])
        sp, pp = paths(next(r for r in m["rows"] if r["token"] == tok), tmp)
        text = open(sp).read()
        open(sp, "w").write(text.replace("  TargetMode: ", "  TargetMode: 9", 1))
        if not any("TargetMode" in x for x in problems(m, tmp)):
            bad.append("a drifted TargetMode was not caught")
        open(sp, "w").write(text)
        os.remove(pp)
        if not any("missing" in x for x in problems(m, tmp)):
            bad.append("a missing profile was not caught")
        m2 = dict(m, recipes=dict(m["recipes"], Nonexistent=m["recipes"][next(iter(m["recipes"]))]))
        if not any("Nonexistent" in x for x in problems(m2, tmp)):
            bad.append("a recipe without a catalog row was not caught")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    return bad


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        bad = self_test()
        print("author_training_scenarios self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
        return 1 if bad else 0
    m = model()
    if not a.check:
        write(m)
    bad = problems(m)
    print(f"{len(m['rows'])} catalog rows, {len(m['recipes'])} metric recipes: " + ("ok" if not bad else "PROBLEMS"))
    for b in bad:
        print("  " + b)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
