#!/usr/bin/env python3
"""The PRISM CLIP AUDIT: does any shipped Spawnable shape lay a prism inside another?

THE RULE (Docs/ECOSYSTEM.md §49.10 and §62): NO PRISM MAY INTERPENETRATE ANOTHER. Spindles are
exempt (a limb may pass through a plate); a plate may not pass through a plate. It is held at
AUTHORING time: a generator emits flush prisms, and nothing resolves clipping while the game loads
(Garrett, 2026-10-20: "we don't want to bloat our run time load when this is a strictly editor
time solvable problem"). Fixes keep every prism - "don't reduce prism count, minimally reduce
scale so they are flush".

HOW IT MEASURES
---------------
Every prefab in Assets/ whose generator component is a compiled Spawnable shape (or a *Generator)
is read the way the card-art renderer reads it (`render_card_backgrounds.generator_node`), then
RUN by the shipped C# through `prism_clip_audit/` (Roslyn + the card-art shims) at intensities
1-4. Each laid prism is its oriented box at its lay scale, clamped into its prism prefab's
PrismScaleAnimator window as the lay clamps it; every pair whose AABBs overlap goes through the
15-axis separating-axis test. Faces that TOUCH are flush, which is the goal; deeper is a clip.

Every lay is tagged with the FAMILY that emitted it (a cell environment's generator methods above
Emit, any other shape's trail), so a report says where the fix belongs:

    inner pairs / flush   a family clipping ITSELF, and the largest uniform scale at which it is
                          flush (a rail or tiling whose prisms are too big for their spacing - fix
                          the size in the generator; 0 means coincident centres: a placement bug)
    crossings             two families passing through each other - a placement or a size fix in
                          the generator, by hand

    python3 Tools/Build/audit_prism_clipping.py                    # the summary
    python3 Tools/Build/audit_prism_clipping.py SpawnableOrrery -v # one shape, every family
    python3 Tools/Build/audit_prism_clipping.py --check            # fail on any clipping generator
    python3 Tools/Build/audit_prism_clipping.py --json out.json    # the raw measurements

Needs a dotnet 8 SDK reachable through DOTNET_ROOT (default ~/.dotnet). A READER tool: it writes
no asset, so it carries no FrogletToolChangeLedger / ship panel.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Build"))
import render_card_backgrounds as rcb  # noqa: E402  the one reader of a generator prefab

HARNESS = ROOT / "Tools/Build/prism_clip_audit/run.sh"
MG = "Assets/_Scripts/Controller/Environment/MiniGameObjects"
INTENSITIES = (1, 2, 3, 4)
# SpawnableGyroid walks the shipped bond-mate table (its enum is lifted by run.sh).
# The shipped bake format, so bake_prism_layouts.py writes exactly what the game reads.
BAKE_FORMAT = "Assets/_Scripts/Controller/Environment/Spawning/PrismLayoutBakeFormat.cs"
# Pure-C# helpers a shape calls. SpawnableGeode took MathfNoAlloc.Min (50a2e4e3c, 2026-10-24, the
# no-params-array three-value Min), and until it was listed here the harness failed to compile, so this
# audit and bake_prism_layouts.py --check both stopped running (CS0103 'MathfNoAlloc').
# SpawnableSwitchbackFeatures (and SwitchbackCourse, which rcb.SOURCES carries for SpawnableBreakwater)
# name the fun-type enum and the closed-form feature layout; without them the harness stopped
# compiling (CS0246 'SwitchbackFunType', 2026-10-28) and this audit stopped running.
SB = "Assets/_Scripts/Controller/Arcade/Switchback"
HELPERS = ["Assets/_Scripts/Utility/Foundation/MathfNoAlloc.cs",
           "Assets/_Scripts/Data/Enums/SwitchbackFunType.cs",
           f"{SB}/SwitchbackFeatures.cs", f"{SB}/SwitchbackLiveFeatures.cs", f"{SB}/SwitchbackBlastRun.cs",
           "Assets/_Scripts/Controller/Arcade/Breakwater/BreakwaterStationBuilder.cs"]
GYROID_TABLES = ["Assets/_Scripts/Controller/Assemblers/CornerSiteType.cs",
                 "Assets/_Scripts/Controller/Assemblers/GyroidBondMateData.cs",
                 "Assets/_Scripts/Controller/Assemblers/GyroidBondMateDataContainer.cs"]

# Shapes that are not audited HERE, each with the reason and where its clearance is held instead.
# Adding a row is the only sanctioned way to leave a shape out.
ABSTRACT = {"SpawnableShapeBase"}
# Generators that lay nothing until their controller hands them a course (SetCourse), with the
# base seed the audit poses them on.
POSED = {"SpawnableBreakwater": 9000,
         # Posed as BLAST RUN (the only fun type whose layout is all prisms the player must blast through) at the
         # picker's fixed world seeds 45_501-45_504 (SwitchbackFunTypes.WorldSeed), on the EditMode tests' shell.
         "SpawnableSwitchbackFeatures": 45_500}
SWITCHBACK_POSE = {"funType": 5, "gates": 20, "inner": 480.0, "outer": 1080.0, "firstGateDistance": 620.0,
                   "clearRadius": 480.0 / 1.22 + 30.0}
NOT_AUDITED = {
    "SpawnableCrystal": "instantiates a crystal, lays no prisms",
    "SpawnableFlora": "plants a flora; plants are held by their own species gates",
    "SpawnableShapeSign": "instantiates a sign prefab, lays no prisms",
}


def shapes():
    """Every Spawnable shape source the harness compiles (the base class included)."""
    out = []
    for p in sorted((ROOT / MG).glob("Spawnable*.cs")):
        if p.stem not in NOT_AUDITED:
            out.append(f"{MG}/{p.name}")
    return out


def list_sources():
    seen, out = set(), []
    for s in rcb.SOURCES + HELPERS + GYROID_TABLES + [BAKE_FORMAT] + shapes():
        if s not in seen:
            seen.add(s)
            out.append(s)
    return out


def generator_prefabs(only=None):
    """(prefab path, component fileID, script name) for every prefab component whose script is an
    audited Spawnable shape."""
    guid_to_name = {}
    for src in list_sources():
        n = Path(src).stem
        if n in ABSTRACT or not (n.startswith("Spawnable") or n.endswith("Generator")):
            continue
        m = re.search(r"^guid: ([0-9a-f]{32})", (ROOT / f"{src}.meta").read_text(), re.M)
        guid_to_name[m.group(1)] = n
    pat = re.compile(r"m_Script: \{fileID: 11500000, guid: (" + "|".join(guid_to_name) + r")\b")
    for prefab in sorted((ROOT / "Assets").rglob("*.prefab")):
        text = rcb.read(prefab)
        if not pat.search(text):
            continue
        for fid, (cls, body) in rcb.docs(text).items():
            if cls != 114:
                continue
            m = pat.search(body)
            if not m:
                continue
            name = guid_to_name[m.group(1)]
            if only and name not in only:
                continue
            yield prefab, fid, name


ADMITS_AUTHORED_SCALE = None
SCALE_ANIMATOR = "PrismScaleAnimator"


def admits_authored_scale(shape):
    """CellEnvironmentSpawnableBase.AdmitsAuthoredPrismScale overridden true: the lay skips the
    prism prefab's scale window."""
    global ADMITS_AUTHORED_SCALE
    if ADMITS_AUTHORED_SCALE is None:
        ADMITS_AUTHORED_SCALE = {p.stem for p in (ROOT / MG).glob("Spawnable*.cs")
                                 if re.search(r"override bool AdmitsAuthoredPrismScale\s*=>\s*true", rcb.read(p))}
    return shape in ADMITS_AUTHORED_SCALE


def scale_window(prefab, fid, shape):
    """The per-axis [minScale, maxScale] the game clamps a laid prism into
    (PrismScaleAnimator.SetTargetScale), read off the prism prefab the generator lays, or None
    when the shape admits its authored scale or names no prism."""
    if admits_authored_scale(shape):
        return None
    body = rcb.docs(rcb.read(prefab))[str(fid)][1]
    # The field a shape lays its prism through: most call it `prism`, the older shapes still
    # serialize it under its pre-rename name `trailBlock` (FormerlySerializedAs), the base `leafPrefab`.
    prism = rcb.ref(body, "prism") or rcb.ref(body, "trailBlock") or rcb.ref(body, "leafPrefab")
    if prism is None:
        return None
    for _, (cls, b) in rcb.docs(rcb.read(prism)).items():
        if cls == 114 and rcb.script_name(b) == SCALE_ANIMATOR:
            lo = re.search(r"^  minScale: (\{.*\})", b, re.M)
            hi = re.search(r"^  maxScale: (\{.*\})", b, re.M)
            if lo and hi:
                return {"min": rcb._vec(lo.group(1)), "max": rcb._vec(hi.group(1)),
                        "prism": prism.relative_to(ROOT).as_posix()}
    return None


_POSED_SETTINGS = {}


def posed_settings(shape):
    """The course settings a posed generator's controller supplies, read off the card-art recipe
    for the mode that lays it (it mirrors GateRaceController.BuildCourse: the shell from the cell's
    nucleus, the station count from EndConditionOverrides)."""
    if shape not in _POSED_SETTINGS:
        stem = {"SpawnableBreakwater": "Breakwater"}[shape]
        path = dict(rcb.roster())[stem]
        r = rcb.recipe(stem, path, rcb.palette(), rcb.end_conditions())
        _POSED_SETTINGS[shape] = next(l["settings"] for l in r["layers"] if l["kind"] == "breakwater")
    return _POSED_SETTINGS[shape]


def build_spec(only=None):
    spec, skipped = [], []
    for prefab, fid, name in generator_prefabs(only):
        rel = prefab.relative_to(ROOT).as_posix()
        try:
            node = rcb.generator_node(prefab, fid)
        except AssertionError as e:
            skipped.append((rel, str(e)))
            continue
        window = scale_window(prefab, fid, name)
        for i in INTENSITIES:
            n = json.loads(json.dumps(node))
            n["fields"]["intensityLevel"] = i
            e = {"name": f"{rel}#{fid}@{i}", "node": n, "window": window,
                 "prefab": rel, "shape": name, "intensity": i}
            if name in POSED:
                # Posed by its controller from the match seed: one representative course per
                # intensity (a live match rolls its own).
                if name == "SpawnableSwitchbackFeatures":
                    e["course"] = {"intensity": i, "seed": POSED[name] + i, "switchback": SWITCHBACK_POSE}
                else:
                    e["course"] = {"intensity": i, "seed": POSED[name] + i, "settings": posed_settings(name)}
            spec.append(e)
    return spec, skipped


def run(spec):
    with tempfile.TemporaryDirectory() as tmp:
        sp = Path(tmp) / "spec.json"
        out = []
        for e in spec:
            o = {"name": e["name"], "node": e["node"], "window": e["window"]}
            if "course" in e:
                o["course"] = e["course"]
            out.append(o)
        sp.write_text(json.dumps(out))
        env = dict(os.environ)
        env.setdefault("DOTNET_ROOT", str(Path.home() / ".dotnet"))
        res = subprocess.run(["bash", str(HARNESS), str(sp)], capture_output=True, text=True, env=env)
        if res.returncode != 0:
            sys.stderr.write(res.stderr[-4000:])
            raise SystemExit(f"prism clip harness failed (exit {res.returncode})")
        notes = [l for l in res.stderr.splitlines() if l.startswith(("[error]", "[clip]"))]
        return json.loads(res.stdout), notes


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("shapes", nargs="*")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--json")
    ap.add_argument("-v", "--verbose", action="store_true", help="every family and crossing")
    ap.add_argument("--list-sources", action="store_true")
    a = ap.parse_args()
    if a.list_sources:
        print("\n".join(list_sources()))
        return 0

    spec, skipped = build_spec(set(a.shapes) or None)
    results, notes = run(spec)
    by_name = {e["name"]: e for e in spec}
    rows = [{**r, "prefab": by_name[r["name"]]["prefab"], "shape": by_name[r["name"]]["shape"],
             "intensity": by_name[r["name"]]["intensity"]} for r in results]
    if a.json:
        Path(a.json).write_text(json.dumps(rows, indent=1))

    # One line per generator component: its worst intensity.
    worst = {}
    for r in rows:
        k = r["name"].split("@")[0]
        if k not in worst or r["clipping"] > worst[k]["clipping"]:
            worst[k] = r
    clipping = sorted((r for r in worst.values() if r["clipping"]), key=lambda r: -r["clipping"])
    empty = [r for r in worst.values() if not r["prisms"]]

    print(f"PRISM CLIP AUDIT - {len(worst)} generators on {len({r['prefab'] for r in rows})} prefabs, "
          f"{len(rows)} runs (intensities {INTENSITIES[0]}-{INTENSITIES[-1]})")
    print(f"  clipping: {len(clipping)}  |  flush: {len(worst) - len(clipping) - len(empty)}  |  "
          f"laid nothing: {len(empty)}\n")
    for r in clipping:
        print(f"  {r['prefab'].replace('Assets/_Prefabs/', ''):<52} I{r['intensity']} {r['prisms']:>6} prisms  "
              f"{r['clipping']:>6} clipping  {r['duplicates']:>5} coincident  worst {r['worst_depth']:.2f}u")
        fams = sorted((f for f in r["families"] if f["inner_pairs"]), key=lambda f: -f["inner_pairs"])
        for f in (fams if a.verbose else fams[:3]):
            print(f"      self   {f['family'][:60]:<60} {f['inner_pairs']:>6} pairs of {f['prisms']:>6}  "
                  f"flush at x{f['inner_flush_scale']:.3f}" + (f"  ({f['inner_coincident']} coincident)"
                                                               if f["inner_coincident"] else ""))
        for c in (r["crossings"] if a.verbose else r["crossings"][:3]):
            print(f"      cross  {(c['a'] + '  x  ' + c['b'])[:60]:<60} {c['pairs']:>6} pairs           "
                  f"flush at x{c['flush_scale']:.3f}")
    for r in empty:
        print(f"  laid nothing: {r['prefab']} ({r['shape']}{', faulted' if r['faulted'] else ''})")
    for rel, why in skipped:
        print(f"  skipped {rel}: {why}")
    for n in sorted(set(notes)):
        print(f"  {n}")
    print("\n  not audited here: " + "; ".join(f"{k} ({v})" for k, v in NOT_AUDITED.items()))
    if a.check and clipping:
        print(f"\nFAIL: {len(clipping)} generator(s) emit a prism inside another")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
