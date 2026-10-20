#!/usr/bin/env python3
"""The PRISM CLIP AUDIT: does any shipped Spawnable shape lay a prism inside another?

THE RULE (Docs/ECOSYSTEM.md §49.10, Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md): NO PRISM MAY
INTERPENETRATE ANOTHER. Spindles are exempt (a limb may pass through a plate); a plate may not pass
through a plate. Until this audit the rule was held only by the generators that had been fitted
under it (Garland, Borromean, Schwarz P, the quasicrystal struts, the shields) - the ~60 Spawnable
shapes the cells, the menu and the arcade arenas lay had never been measured at all.

HOW IT MEASURES
---------------
Every prefab in Assets/ whose generator component is a compiled Spawnable shape is read the way
the card-art renderer reads it (`render_card_backgrounds.generator_node`: every serialized value
field, nested child generators followed), then RUN by the shipped C# through
`prism_clip_audit/` (Roslyn + the card-art shims) at intensities 1-4. Each laid prism is its
oriented box (centre, rotation, full extents = its lay scale) and every pair whose bounding
spheres meet goes through the 15-axis separating-axis test.

    CLIPPING   the pair interpenetrates (beyond a float epsilon of the smaller prism)
    DEEP       by more than a quarter of the smaller prism's thinnest half-extent
    ADJACENT   the clipping pair is consecutive in lay order - one trail touching its own
               neighbour, which is one structural fix (the trail's pitch), not N

    python3 Tools/Build/audit_prism_clipping.py                    # the report
    python3 Tools/Build/audit_prism_clipping.py --check            # fail on any clipping prefab
    python3 Tools/Build/audit_prism_clipping.py --json out.json    # the raw measurements
    python3 Tools/Build/audit_prism_clipping.py SpawnableStar      # only prefabs of these shapes

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
CLEARANCE = "Assets/_Scripts/Controller/Environment/Spawning/PrismClearance.cs"
BASELINES_REL = "Tools/Build/prism_clearance_baselines.json"
HARNESS_FILES = ["Tools/Build/prism_clip_audit/Audit.cs", "Tools/Build/prism_clip_audit/ClipShim.cs",
                 "Tools/Build/prism_clip_audit/run.sh", "Tools/Build/audit_prism_clipping.py"]
# SpawnableGyroid walks the shipped bond-mate table (its enum is lifted by run.sh).
GYROID_TABLES = ["Assets/_Scripts/Controller/Assemblers/CornerSiteType.cs",
                 "Assets/_Scripts/Controller/Assemblers/GyroidBondMateData.cs",
                 "Assets/_Scripts/Controller/Assemblers/GyroidBondMateDataContainer.cs"]

# Shapes that are not audited HERE, each with the reason and where its clearance is held instead.
# Adding a row is the only sanctioned way to leave a shape out.
ABSTRACT = {"SpawnableShapeBase"}
# Generators that lay nothing until their controller hands them a course (SetCourse), with the
# base seed the audit poses them on.
POSED = {"SpawnableBreakwater": 9000}
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
    for s in rcb.SOURCES + [CLEARANCE] + GYROID_TABLES + shapes():
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
    prism = rcb.ref(body, "prism") or rcb.ref(body, "leafPrefab")
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
                e["course"] = {"intensity": i, "seed": POSED[name] + i, "settings": posed_settings(name)}
            spec.append(e)
    return spec, skipped


def run(spec, rows_dir=None):
    with tempfile.TemporaryDirectory() as tmp:
        sp = Path(tmp) / "spec.json"
        out = []
        for e in spec:
            o = {"name": e["name"], "node": e["node"], "window": e["window"]}
            if "course" in e:
                o["course"] = e["course"]
            if rows_dir and e["intensity"] == RENDER_INTENSITY:
                o["rows"] = str(Path(rows_dir) / (stem(e) + ".json"))
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


RENDER_INTENSITY = 1
RENDER_W, RENDER_H = 480, 360


def stem(e):
    """`<prefab stem>_<component fileID>`: a prefab can carry more than one generator."""
    return f"{Path(e['prefab']).stem}_{e['name'].split('#')[1].split('@')[0]}"


def render(rows_dir, out_png, prefabs):
    """A before/after contact sheet of every clipping prefab at RENDER_INTENSITY, drawn by the
    card-art renderer from the lay lists the harness wrote: what the shape lays today on the left,
    what it lays once PrismClearance has resolved it on the right."""
    cards, order = [], []
    for e in prefabs:
        f = Path(rows_dir) / (stem(e) + ".json")
        if not f.exists():
            continue
        data = json.loads(f.read_text())
        for side in ("before", "after"):
            name = f"{stem(e)}_{side}"
            cards.append({"name": name, "width": RENDER_W, "height": RENDER_H, "supersample": 2,
                          "camera": {"azimuth": 35, "elevation": 24, "distance": 0.95, "fov": 42},
                          "look": {}, "layers": [{"kind": "prisms", "rows": data[side]}]})
            order.append(name)
    with tempfile.TemporaryDirectory() as tmp:
        spec = Path(tmp) / "spec.json"
        spec.write_text(json.dumps({"palette": rcb.palette(), "cards": cards}))
        env = dict(os.environ)
        env.setdefault("DOTNET_ROOT", str(Path.home() / ".dotnet"))
        res = subprocess.run(["bash", str(rcb.HARNESS), str(spec), tmp], env=env, capture_output=True, text=True)
        if res.returncode != 0:
            sys.stderr.write(res.stderr[-3000:])
            raise SystemExit("card-art renderer failed")
        images = {n: (Path(tmp) / f"{n}.rgb").read_bytes() for n in order}
    cols, pad = 2, 4
    rows = (len(order) + cols - 1) // cols
    W, H = cols * RENDER_W + (cols + 1) * pad, rows * RENDER_H + (rows + 1) * pad
    out = bytearray(b"\x10\x10\x14" * (W * H))
    for k, n in enumerate(order):
        src = images[n]
        ox, oy = pad + (k % cols) * (RENDER_W + pad), pad + (k // cols) * (RENDER_H + pad)
        for y in range(RENDER_H):
            o = ((oy + y) * W + ox) * 3
            out[o:o + RENDER_W * 3] = src[y * RENDER_W * 3:(y + 1) * RENDER_W * 3]
    rcb.png(out_png, W, H, bytes(out))
    return order


def baselines_of(rows, spec):
    """The committed record a cell's PhaseThresholds are rebased on (prism_clearance.py): per
    generator and intensity, the count and volume it AUTHORS and the count and volume the game
    LAYS once PrismClearance has run. Every source that can move a number is hashed into it."""
    import hashlib
    sources = sorted(set(list_sources() + HARNESS_FILES + [e["prefab"] for e in spec]))
    h = hashlib.sha256()
    for rel in sources:
        h.update(rel.encode())
        h.update((ROOT / rel).read_bytes())
    gens = {}
    for r in rows:
        key = r["name"].split("@")[0]
        g = gens.setdefault(key, {"prefab": r["prefab"], "shape": r["shape"], "intensities": {}})
        g["intensities"][str(r["intensity"])] = {k: r[k] for k in
                                                 ("authored_count", "authored_volume", "laid_count", "laid_volume")}
    return {"sources_sha256": h.hexdigest(), "generators": gens}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("shapes", nargs="*")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--json")
    ap.add_argument("--list-sources", action="store_true")
    ap.add_argument("--write-baselines", action="store_true",
                    help=f"rewrite {BASELINES_REL} (what every generator authors and what the game lays)")
    ap.add_argument("--render", metavar="SHEET.png",
                    help="also write a before/after contact sheet of every clipping prefab")
    a = ap.parse_args()
    if a.list_sources:
        print("\n".join(list_sources()))
        return 0

    spec, skipped = build_spec(set(a.shapes) or None)
    rows_tmp = tempfile.TemporaryDirectory() if a.render else None
    results, notes = run(spec, rows_tmp.name if rows_tmp else None)
    by_name = {e["name"]: e for e in spec}
    rows = []
    for r in results:
        e = by_name[r["name"]]
        rows.append({**r, "prefab": e["prefab"], "shape": e["shape"], "intensity": e["intensity"]})
    if a.json:
        Path(a.json).write_text(json.dumps(rows, indent=1))

    # What the GAME lays: the resolved lays for a shape the game runs PrismClearance on, the raw
    # generator output for one it does not.
    for r in rows:
        r["in_game"] = r["resolved_clipping"] if r["resolves_in_game"] else r["clipping"]

    # One line per prefab (per generator component): its worst intensity.
    worst = {}
    for r in rows:
        k = r["name"].split("@")[0]
        key = (r["in_game"], r["clipping"], r["worst_depth_rel"])
        if k not in worst or key > (worst[k]["in_game"], worst[k]["clipping"], worst[k]["worst_depth_rel"]):
            worst[k] = r
    emits = sorted((r for r in worst.values() if r["clipping"]), key=lambda r: (-r["deep"], -r["clipping"]))
    in_game = [r for r in worst.values() if r["in_game"]]
    clean = [r for r in worst.values() if not r["clipping"] and r["prisms"]]
    empty = [r for r in worst.values() if not r["prisms"]]
    clipping = emits

    print(f"PRISM CLIP AUDIT - {len(worst)} generators on {len({r['prefab'] for r in rows})} prefabs, "
          f"{len(rows)} runs (intensities {INTENSITIES[0]}-{INTENSITIES[-1]})")
    print(f"  the generator emits clipping prisms: {len(emits)}  |  emits none: {len(clean)}  |  "
          f"laid nothing: {len(empty)}")
    print(f"  the GAME lays clipping prisms (after PrismClearance): {len(in_game)}\n")
    if emits:
        print(f"  {'prefab':<52} {'shape':<26} {'prisms':>6} {'clip':>6} {'adj':>5} {'dup':>5} {'deep':>5} {'worst':>16}"
              f"  || game: {'shrunk':>6} {'cleared':>7} {'clips':>5} {'vol':>5} {'ms':>5}")
        for r in emits:
            game = (f"{r['resolved_shrunk']:>6} {r['resolved_cleared']:>7} {r['in_game']:>5} "
                    f"{r['resolved_volume_kept']:>5.0%} {r['generate_and_resolve_ms']:>5.0f}"
                    if r["resolves_in_game"] else f"{'(not resolved)':>21} {r['in_game']:>5}")
            print(f"  {r['prefab'].replace('Assets/_Prefabs/', '')[-52:]:<52} {r['shape']:<26} {r['prisms']:>6} "
                  f"{r['clipping']:>6} {r['clipping_adjacent']:>5} {r['duplicates']:>5} {r['deep']:>5} "
                  f"{r['worst_depth']:>7.2f}u {r['worst_depth_rel']:>6.0%}  || game: {game}")
        print("\n  clip = interpenetrating pairs, adj = of them consecutive in lay order, dup = the same prism "
              "laid twice, deep = deeper than a quarter of the thinner prism; worst = deepest pair (u, and as a "
              "share of the thinner prism's half-thickness). game = what is laid once PrismClearance has run: "
              "prisms shrunk, prisms cleared, pairs still clipping, volume kept, ms to generate + resolve.")
    if empty:
        print("\n  laid nothing (a lay path outside GenerateTrailData, or an empty authoring):")
        for r in empty:
            print(f"    {r['prefab']}  ({r['shape']}{', faulted' if r['faulted'] else ''})")
    for rel, why in skipped:
        print(f"  skipped {rel}: {why}")
    for n in sorted(set(notes)):
        print(f"  {n}")
    print("\n  not audited here: " + "; ".join(f"{k} ({v})" for k, v in NOT_AUDITED.items()))
    if a.render:
        drawn = render(rows_tmp.name, a.render, clipping)
        print(f"\n  before/after sheet: {a.render} ({len(drawn) // 2} prefabs, left = today, right = resolved)")
        rows_tmp.cleanup()
    baselines = baselines_of(rows, spec)
    stale = False
    if a.write_baselines:
        (ROOT / BASELINES_REL).write_text(json.dumps(baselines, indent=1, sort_keys=True) + "\n")
        print(f"\n  wrote {BASELINES_REL} ({len(baselines['generators'])} generators)")
    elif a.check and not a.shapes:
        committed = json.loads((ROOT / BASELINES_REL).read_text()) if (ROOT / BASELINES_REL).exists() else None
        if committed != baselines:
            stale = True
            print(f"\n  FAIL {BASELINES_REL} is stale - re-run with --write-baselines, then re-run every "
                  "generator that reads it (prism_clearance.py lists them)")
    if a.check and (in_game or stale):
        for r in in_game:
            print(f"  FAIL {r['prefab']} ({r['shape']}) intensity {r['intensity']}: the game lays "
                  f"{r['in_game']} interpenetrating pair(s)")
        print(f"\nFAIL: {len(in_game)} generator(s) lay a prism inside another" if in_game else "\nFAIL: stale baselines")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
