#!/usr/bin/env python3
"""Bake the Darwin Lab (Docs/EVOLUTION.md §7).

    python3 Tools/Evolution/build_lab.py            # writes DarwinLab.html + DarwinLab.artifact.html
    python3 Tools/Evolution/build_lab.py --check    # bakes to a temp dir and fails if the committed page differs,
                                                    # or if a shipped number no longer matches its asset

A lab is a READER of the shipped numbers. This script re-reads every number the arena runs on straight from the
assets it was copied from (the Blob cell's fauna configs, the tadpole and shark prefabs, the spawn profile, the
EvolutionSettings defaults in C#) and compares them to the values the harness ran with (results/shipped.json).
A mismatch is not fatal to the bake - the page SAYS ON SCREEN which asset moved - but --check fails on it, so the
rule "when the asset moves, the page follows or says it differs" is held by CI, not by memory.

Inputs (all tracked):  lab_template.html, sim.js, results/shipped.json, results/results.json, results/golden.json
Outputs:               DarwinLab.html (standalone, file://; what verify_lab.cjs checks)
                       DarwinLab.artifact.html (the body-only form the claude.ai Artifact tool publishes)
"""
import argparse
import datetime
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

# key -> (asset path relative to the repo, YAML field name, scale) ; scale divides the asset's value
ASSET_SOURCES = {
    "HerbFeedsPerOffspring": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Tadpole Fauna Config Data.asset", "FeedsPerOffspring", 1),
    "HerbCooldown": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Tadpole Fauna Config Data.asset", "ReproductionCooldownSeconds", 1),
    "HerbCap": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Tadpole Fauna Config Data.asset", "MaxLivePopulation", 1),
    "HerbFloor": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Tadpole Fauna Config Data.asset", "PopulationSize", 1),
    "HerbStarvation": ("Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab", "starvationSeconds", 1),
    "PredFeedsPerOffspring": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset", "FeedsPerOffspring", 1),
    "PredCooldown": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset", "ReproductionCooldownSeconds", 1),
    "PredCap": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset", "MaxLivePopulation", 1),
    "PredFloor": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset", "PopulationSize", 1),
    "PredStarvation": ("Assets/_Models/Fauna/MassSharkFauna.prefab", "starvationSeconds", 1),
    "SpawnWave": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset", "BaseFaunaSpawnTime", 1),
    "FoodCap": ("Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset", "FloraSpawnVolumeCeiling", 16),
}
# key -> (C# file, field name) for the script defaults the arena carries
CODE_SOURCES = {
    "HuntInterval": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs", "huntIntervalSeconds"),
    "PredationImmunity": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs", "predationImmunitySeconds"),
    "MutationRate": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "MutationRate"),
    "MutationSigma": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "MutationSigma"),
    "FounderSpread": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "FounderSpread"),
    "TempoPaceRange": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "TempoPaceRange"),
    "TempoUpkeepRange": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "TempoUpkeepRange"),
    "ReachRadiusRange": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "ReachRadiusRange"),
    "ReachUpkeepRange": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "ReachUpkeepRange"),
    "FecundityRange": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "FecundityRange"),
    "FecundityProvisionRange": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "FecundityProvisionRange"),
    "CohesionRange": ("Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/EvolutionSettings.cs", "CohesionRange"),
    "StomachCapacity": ("Assets/_Scripts/Utility/DataContainers/CellPhaseThresholds.cs", "NominalPrismVolume"),
}


def read(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as f:
        return f.read()


def yaml_field(text, name):
    m = re.search(rf"^\s*{re.escape(name)}:\s*(-?[0-9.]+)\s*$", text, re.M)
    return float(m.group(1)) if m else None


def code_field(text, name):
    # `public float MutationSigma = 0.08f;`, `[SerializeField] protected float predationImmunitySeconds = 6f;`,
    # `public const float NominalPrismVolume = 16f;`
    m = re.search(rf"\b{re.escape(name)}\s*=\s*(-?[0-9.]+)f?\s*;", text)
    return float(m.group(1)) if m else None


def asset_value(key):
    if key in ASSET_SOURCES:
        path, field, scale = ASSET_SOURCES[key]
        v = yaml_field(read(path), field)
        return None if v is None else v / scale
    if key in CODE_SOURCES:
        path, field = CODE_SOURCES[key]
        return code_field(read(path), field)
    return None


def drift(shipped):
    out = []
    for key, entry in shipped.items():
        baked = entry["value"]
        if isinstance(baked, bool):
            continue
        asset = asset_value(key)
        if asset is None:
            continue
        if abs(float(baked) - asset) > 1e-9:
            out.append({"key": key, "baked": baked, "asset": asset})
    return out


def branch():
    try:
        return subprocess.check_output(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=ROOT, text=True).strip()
    except Exception:
        return "?"


FONTS = ('<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,500'
         '&family=IBM+Plex+Sans:wght@400;500;600&family=IBM+Plex+Mono:wght@400;500&display=swap">')


def bake(out_dir, build_date=None):
    tpl = read("Tools/Evolution/lab_template.html")
    sim = read("Tools/Evolution/sim.js")
    shipped = json.loads(read("Tools/Evolution/results/shipped.json"))
    results = json.loads(read("Tools/Evolution/results/results.json"))
    # The page draws one sparkline per regime from the rows' means and never reads the final genomes: bake every
    # fifth census row (one per five minutes of cell time) and drop `final`, which keeps the page under 1 MB.
    for reg in results.get("regimes", []):
        for run in reg.get("runs", []):
            run.pop("final", None)
            rows = run.get("rows", [])
            run["rows"] = rows[::5] if len(rows) > 60 else rows
    golden = json.loads(read("Tools/Evolution/results/golden.json"))
    moved = drift(shipped)
    if "</script>" in sim:
        raise SystemExit("sim.js must not contain '</script>'")

    def js(obj):
        return json.dumps(obj, separators=(",", ":"), ensure_ascii=False).replace("</", "<\\/")

    page = (tpl.replace("/*__SIM_JS__*/", sim)
               .replace("/*__SHIPPED_JSON__*/ {}", js(shipped))
               .replace("/*__SHIPPED_DRIFT__*/ []", js(moved))
               .replace("/*__RESULTS_JSON__*/ {}", js(results))
               .replace("/*__GOLDEN_JSON__*/ {}", js(golden))
               .replace("__BRANCH__", branch())
               .replace("__BUILD_DATE__", build_date or datetime.date.today().isoformat()))

    start, end = page.index("<!--ARTIFACT-START-->"), page.index("<!--ARTIFACT-END-->")
    body = page[start + len("<!--ARTIFACT-START-->"):end].strip("\n")
    # standalone: no web font link (a blocked network must not log a console error under verify_lab.cjs)
    standalone = page.replace("<!--FONTS-->", "")
    # the artifact form: the content only (the Artifact tool wraps it in its own skeleton), with the fonts
    artifact = body.replace("<!--FONTS-->", FONTS)
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, "DarwinLab.html"), "w", encoding="utf-8") as f:
        f.write(standalone)
    with open(os.path.join(out_dir, "DarwinLab.artifact.html"), "w", encoding="utf-8") as f:
        f.write(artifact)
    return moved, len(standalone)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="bake to a temp dir; fail on drift or on a stale committed page")
    ap.add_argument("--date", default=None, help="build date to stamp (default: today)")
    args = ap.parse_args()
    if args.check:
        import tempfile
        committed = read("Tools/Evolution/DarwinLab.html")
        m = re.search(r"built: '([0-9-]+)'", committed)
        stamp = m.group(1) if m else datetime.date.today().isoformat()
        with tempfile.TemporaryDirectory() as tmp:
            moved, size = bake(tmp, stamp)
            fresh = open(os.path.join(tmp, "DarwinLab.html"), encoding="utf-8").read()
        ok = True
        if moved:
            ok = False
            for d in moved:
                print(f"DRIFT: {d['key']} bakes {d['baked']} but the asset now says {d['asset']}")
        if fresh != committed:
            ok = False
            print("STALE: Tools/Evolution/DarwinLab.html differs from a fresh bake - run build_lab.py and commit")
        print("darwin lab check:", "OK" if ok else "FAIL")
        sys.exit(0 if ok else 1)
    moved, size = bake(HERE, args.date)
    for d in moved:
        print(f"WARNING drift: {d['key']} baked {d['baked']}, asset says {d['asset']} (the page flags it)")
    print(f"wrote Tools/Evolution/DarwinLab.html ({size / 1024:.0f} KB) and DarwinLab.artifact.html; drift: {len(moved)}")


if __name__ == "__main__":
    main()
