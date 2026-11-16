#!/usr/bin/env python3
"""THE REGATTA GENE CATALOGUE: every fundamental a Regatta course genome may place, with every parameter's legal range,
written from the game's own code (Plan step: time-arena-course; Regatta flywheel W2, Garrett 2026-11-16: "our genes
could bring in every fundamental with all their types and parameters fully expressable. all the segment spawners, all
the flora, anything").

Two sources, both the game's:
  1. THE C#. Every segment spawner under Assets/_Scripts/Controller/Environment/MiniGameObjects/Spawnable*.cs: its
     class, its base (a CellEnvironmentSpawnableBase "world" has a density dial and per-prism kinds), and every
     serialized field a gene can hold (float, int, bool, an enum, a Domains), with its default from the initializer
     and its range from [Range(a, b)] / [Min(a)]. A field without a range is given one round its default (x0.5 to x2,
     or 0..8 for an int that defaults to 0, which is how the spawners say "off"); a field with no default and no range,
     and every reference, vector, array, list or string field, stays at the spawner's own value and is listed under
     `fixed` with why.
  2. THE MEASURED RUN (`--measure`): Tools/Build/regatta_genome_harness/run.sh generates every kind at its defaults
     through the game's own code (Amoebius: the real SpawnableBase.GetTrailData, never a bake) and records prisms,
     bounding radius and finest prism axis (measured.json). A kind that throws, lays nothing, or whose finest prism
     would be under MIN_PRISM_AXIS at the largest size a course can hold is marked unusable, with why.

The output, Assets/Resources/Regatta/RegattaGeneCatalogue.json, is read by the game (RegattaGeneCatalogue.Shipped:
RegattaCourseGenome.Repair and RegattaPlacementBuilder) and by the search (Tools/Compute/evaluators/regatta_genome.py).

    python3 Tools/Build/regatta_gene_catalogue.py            # rewrite the catalogue from the C# + measured.json
    python3 Tools/Build/regatta_gene_catalogue.py --check    # exit 1 if the catalogue is stale
    python3 Tools/Build/regatta_gene_catalogue.py --describe # one line per kind
    python3 Tools/Build/regatta_gene_catalogue.py --self-test
"""
import glob
import json
import math
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SPAWNER_DIR = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Environment", "MiniGameObjects")
OUT = os.path.join(ROOT, "Assets", "Resources", "Regatta", "RegattaGeneCatalogue.json")
MEASURED = os.path.join(ROOT, "Tools", "Build", "regatta_genome_harness", "measured.json")
VERSION = 1

# A placement is scaled uniformly to a bounding radius in [SCALE_MIN, SCALE_MAX] (u; SCALE_MAX is the cell: a world that
# large is carved round the course and becomes the place the race runs through), and its prisms should not be finer than
# MIN_PRISM_AXIS (u): the prism scale animator floors an axis at 0.5 u, so a finer one would be drawn larger than laid
# and clip its neighbours (RegattaPlacementBuilder.MinPrismAxis leaves those out). A kind's scale range starts where its
# 5% finest prisms reach that; a kind that only reaches it beyond SCALE_MAX is unusable.
SCALE_MIN, SCALE_MAX = 20.0, 1100.0
MIN_PRISM_AXIS = 0.5
# A kind's 5% finest prisms may still be thinner (they are left out where laid: RegattaPlacementBuilder.MinPrismAxis).
# A kind that lays more than HEAVY prisms (or takes over HEAVY_MS) with every count at its high end has its count genes'
# high ends pulled in to HEAVY_SPAN x their defaults: a placement is thinned to 1,000 prisms anyway, and a course of 24
# must generate in about a second for the race judge.
HEAVY, HEAVY_MS, HEAVY_SPAN = 60000, 400.0, 1.25

# Kinds that are not placeable, from the code (not taste):
NOT_PLACEABLE = {
    "RegattaRails": "the Regatta course itself",
    "ShapeBase": "abstract base of the shape-drawing spawnables",
    "ShapeSign": "a MonoBehaviour that shows a sign, not a SpawnableBase",
    "Crystal": "lays crystals, not prisms: crystals are the genome's `features.Crystals` (racing AIs never take one)",
    "Flora": "lays one living plant prefab: living flora is the genome's `flora` (the cell plants on prepared sites)",
}

# Fields that are the placement's own genes, never a kind parameter.
BASE_FIELDS = {"seed", "domain", "density", "intensityLevel", "spawnClearRadius", "layAcrossFrames",
               "layBudgetMsPerFrame"}

SIMPLE = {"float": "float", "int": "int", "bool": "bool", "Domains": "domain"}

FIELD_RE = re.compile(
    r"((?:\[[^\]]*\]\s*)+)"                       # attribute blocks
    r"(?:public\s+|protected\s+|private\s+|internal\s+)*"
    r"([\w<>\[\]\.]+)\s+(\w+)\s*(?:=\s*([^;]+))?;")


def _enums():
    """Every enum in Assets/_Scripts: name -> members in declaration order."""
    out = {}
    for path in glob.glob(os.path.join(ROOT, "Assets", "_Scripts", "**", "*.cs"), recursive=True):
        try:
            src = open(path, encoding="utf-8").read()
        except OSError:
            continue
        for m in re.finditer(r"\benum\s+(\w+)\s*(?::\s*\w+\s*)?\{([^}]*)\}", src):
            body = re.sub(r"//[^\n]*|/\*.*?\*/", "", m.group(2), flags=re.S)
            body = re.sub(r"\[[^\]]*\]", "", body)
            names = [re.split(r"\s*=", x.strip())[0].strip() for x in body.split(",")]
            out.setdefault(m.group(1), [n for n in names if re.match(r"^\w+$", n)])
    return out


def _num(text):
    text = (text or "").strip().rstrip("fFdDmM")
    try:
        return float(text)
    except ValueError:
        return None


def _classes():
    """{class name: (base name, source)} for every Spawnable*.cs."""
    out = {}
    for path in sorted(glob.glob(os.path.join(SPAWNER_DIR, "Spawnable*.cs"))):
        src = open(path, encoding="utf-8").read()
        m = re.search(r"\bclass\s+(Spawnable\w+)\s*:\s*([\w\.]+)", src)
        if m:
            out[m.group(1)] = (m.group(2), src, os.path.relpath(path, ROOT))
    return out


def _fields(src):
    """[(attrs, type, name, default text)] of every serialized or public field, in order."""
    out = []
    code = re.sub(r"//[^\n]*", "", src)
    for m in FIELD_RE.finditer(code):
        attrs, typ, name, default = m.group(1), m.group(2), m.group(3), m.group(4)
        if "SerializeField" not in attrs and "public" not in code[max(0, m.end(1) - 1):m.start(2)]:
            continue
        if typ in ("return", "new", "class", "struct", "enum", "const", "static", "readonly", "override"):
            continue
        out.append((attrs, typ, name, (default or "").strip()))
    return out


def _gene(attrs, typ, name, default, enums):
    """(param dict, None) or (None, why fixed)."""
    rng = re.search(r"Range\(\s*([-\d\.fF]+)\s*,\s*([-\d\.fF]+)\s*\)", attrs)
    mn = re.search(r"Min\(\s*([-\d\.fF]+)\s*\)", attrs)
    lo_hi = (_num(rng.group(1)), _num(rng.group(2))) if rng else None
    floor = _num(mn.group(1)) if mn else None
    if typ == "bool":
        d = 1.0 if default.strip() == "true" else 0.0
        return {"name": name, "type": "bool", "cs_type": typ, "lo": 0, "hi": 1, "default": d}, None
    if typ == "Domains" or typ == "Data.Domains":
        dm = re.search(r"Domains\.(\w+)", default)
        doms = ["Jade", "Ruby", "Gold", "Blue"]
        d = float(doms.index(dm.group(1))) if dm and dm.group(1) in doms else 3.0
        return {"name": name, "type": "domain", "cs_type": "Domains", "lo": 0, "hi": 3, "default": d}, None
    if typ in enums and typ not in ("Domains",):
        members = enums[typ]
        dm = re.search(r"\.(\w+)\s*$", default)
        d = float(members.index(dm.group(1))) if dm and dm.group(1) in members else 0.0
        return {"name": name, "type": "enum", "cs_type": typ, "lo": 0, "hi": len(members) - 1, "default": d,
                "values": members}, None
    if typ not in ("float", "int"):
        return None, f"{typ} (not a scalar gene)"
    d = _num(default)
    if lo_hi:
        lo, hi = lo_hi
        if d is None:
            d = lo
    elif d is None:
        return None, "no default and no range to scale from"
    elif typ == "int" and d == 0:
        lo, hi = 0.0, 8.0
    elif d == 0:
        return None, "defaults to 0 with no range (no scale to search round)"
    else:
        lo, hi = (d * 0.5, d * 2.0) if d > 0 else (d * 2.0, d * 0.5)
        if typ == "int":
            lo, hi = float(max(1, math.floor(lo))) if d >= 1 else math.floor(lo), float(math.ceil(hi))
    if floor is not None:
        lo = max(lo, floor)
        hi = max(hi, lo)
    if typ == "int":
        lo, hi, d = float(math.floor(lo)), float(math.ceil(hi)), float(round(d))
    return {"name": name, "type": typ, "cs_type": typ, "lo": round(lo, 4), "hi": round(hi, 4),
            "default": round(min(max(d, lo), hi), 4)}, None


def scan():
    """The catalogue from the C# alone (no measured numbers)."""
    enums = _enums()
    classes = _classes()
    kinds = []
    for cls in sorted(classes):
        base, src, rel = classes[cls]
        kid = cls[len("Spawnable"):]
        chain, b = [cls], base
        while b in classes:
            chain.append(b)
            b = classes[b][0]
        world = b == "CellEnvironmentSpawnableBase" or base == "CellEnvironmentSpawnableBase"
        is_spawnable = b in ("SpawnableBase", "CellEnvironmentSpawnableBase")
        params, fixed, seen = [], [], set()
        for c in reversed(chain):
            for attrs, typ, name, default in _fields(classes[c][1]):
                if name in seen or name in BASE_FIELDS:
                    continue
                seen.add(name)
                gene, why = _gene(attrs, typ, name, default, enums)
                if gene:
                    params.append(gene)
                else:
                    fixed.append({"name": name, "why": why})
        why = NOT_PLACEABLE.get(kid) or ("" if is_spawnable else f"derives from {b}, not SpawnableBase")
        kinds.append({"id": kid, "type": f"CosmicShore.Gameplay.{cls}", "source": rel, "world": world,
                      "usable": not why, "why": why, "params": params, "fixed": fixed})
    return kinds


def merge(kinds, measured):
    """Fold the measured run in: native size and count, the scale range, and every kind the run could not place."""
    by = {m["id"]: m for m in (measured or {}).get("kinds", [])}
    for k in kinds:
        m = by.get(k["id"])
        k.update({"native_prisms": 0, "native_radius": 0.0, "native_min_axis": 0.0,
                  "scale_lo": SCALE_MIN, "scale_hi": SCALE_MAX})
        if not k["usable"]:
            continue
        if m is None:
            k["usable"], k["why"] = False, "not measured yet (Tools/Build/regatta_genome_harness/run.sh)"
            continue
        if m.get("failure"):
            k["usable"], k["why"] = False, f"generation failed in the game code: {m['failure']}"
            continue
        n, r = int(m.get("prisms", 0)), float(m.get("radius", 0.0))
        ax = float(m.get("min_axis_p5", m.get("min_axis", 0.0)))
        k.update({"native_prisms": n, "native_radius": round(r, 3), "native_min_axis": round(ax, 4)})
        if n <= 0 or r <= 1e-3:
            k["usable"], k["why"] = False, ("lays no prisms from its code defaults: it needs authored inputs (a "
                                            "prefab's waypoints, stations or lists) a gene cannot hold")
            continue
        if ax <= 0:
            k["usable"], k["why"] = False, ("its prisms take their size from a field the code leaves at zero (the "
                                            "prefab authors it), so at its code defaults they have no size")
            continue
        if int(m.get("prisms_hi", 0)) > HEAVY or float(m.get("ms_hi", 0.0)) > HEAVY_MS:
            for p in k["params"]:
                if p["type"] == "int" and p["hi"] > p["default"] and p["default"] > 0:
                    p["hi"] = float(max(p["default"], math.ceil(p["default"] * HEAVY_SPAN)))
            k["heavy"] = (f"{int(m.get('prisms_hi', 0))} prisms in {float(m.get('ms_hi', 0.0)):.0f} ms with every count "
                          f"at its high end: count genes held to x{HEAVY_SPAN} their defaults")
        # The smallest radius at which the finest prism is still MIN_PRISM_AXIS: s * ax / r >= MIN_PRISM_AXIS.
        lo = max(SCALE_MIN, MIN_PRISM_AXIS * r / ax) if ax > 0 else SCALE_MIN
        if lo > SCALE_MAX:
            k["usable"], k["why"] = False, (f"its finest prism ({ax:.2f} u at {r:.0f} u radius) would be under "
                                            f"{MIN_PRISM_AXIS} u at every size a course holds (<= {SCALE_MAX:.0f} u)")
            continue
        k["scale_lo"] = round(lo, 2)
    return kinds


def build():
    measured = json.load(open(MEASURED, encoding="utf-8")) if os.path.exists(MEASURED) else None
    kinds = merge(scan(), measured)
    return {"version": VERSION, "generated_by": "Tools/Build/regatta_gene_catalogue.py",
            "scale_min": SCALE_MIN, "scale_max": SCALE_MAX, "min_prism_axis": MIN_PRISM_AXIS,
            "measured_engine_commit": (measured or {}).get("engine_commit", ""),
            "kinds": kinds}


def text(cat):
    return json.dumps(cat, indent=1, sort_keys=False) + "\n"


def describe(cat):
    usable = [k for k in cat["kinds"] if k["usable"]]
    print(f"{len(cat['kinds'])} spawners scanned, {len(usable)} placeable, "
          f"{sum(len(k['params']) for k in usable)} parameter genes on them")
    for k in cat["kinds"]:
        tag = "OK " if k["usable"] else "-- "
        extra = (f"{k['native_prisms']:>6} prisms r={k['native_radius']:.0f} scale {k['scale_lo']:.0f}-{k['scale_hi']:.0f}"
                 if k["usable"] else k["why"])
        print(f"  {tag}{k['id']:<18} {len(k['params']):>2} genes  {extra}")


def self_test():
    enums = _enums()
    assert "GyroidBlockType" in enums and len(enums["GyroidBlockType"]) >= 2, "enum scan"
    g, _ = _gene("[SerializeField, Min(80f)]", "float", "kelpRadius", "320f", enums)
    assert g["lo"] == 160.0 and g["hi"] == 640.0, g
    g, _ = _gene("[SerializeField, Range(1, 4)]", "int", "tier", "1", enums)
    assert (g["lo"], g["hi"], g["default"]) == (1.0, 4.0, 1.0), g
    g, _ = _gene("[SerializeField, Min(0)]", "int", "dangerTipEveryNth", "0", enums)
    assert (g["lo"], g["hi"]) == (0.0, 8.0), g
    _, why = _gene("[SerializeField]", "Prism", "prism", "", enums)
    assert why, "a reference field is never a gene"
    _, why = _gene("[SerializeField]", "float", "blockCount", "", enums)
    assert why, "no default, no range: fixed"
    kinds = scan()
    ids = {k["id"] for k in kinds}
    assert {"Gyroid", "Kelpwood", "Helix", "Caldera"} <= ids, ids
    assert not next(k for k in kinds if k["id"] == "RegattaRails")["usable"]
    kelp = next(k for k in kinds if k["id"] == "Kelpwood")
    assert kelp["world"] and any(p["name"] == "strandCount" for p in kelp["params"]), kelp
    # Negative controls for the measured merge.
    fake = {"kinds": [{"id": "Kelpwood", "prisms": 100, "radius": 400.0, "min_axis": 0.1, "min_axis_p5": 0.1},
                      {"id": "Helix", "failure": "NullReferenceException"},
                      {"id": "Gyroid", "prisms": 50, "radius": 30.0, "min_axis": 3.0}]}
    m = {k["id"]: k for k in merge(scan(), fake)}
    assert not m["Kelpwood"]["usable"] and "finest prism" in m["Kelpwood"]["why"], m["Kelpwood"]
    assert not m["Helix"]["usable"] and "failed" in m["Helix"]["why"]
    assert m["Gyroid"]["usable"] and m["Gyroid"]["scale_lo"] == SCALE_MIN, m["Gyroid"]
    assert not m["Caldera"]["usable"] and "not measured" in m["Caldera"]["why"]
    print(f"regatta_gene_catalogue self-test OK: {len(kinds)} spawners scanned, 4 negative controls.")


def main(argv):
    if "--self-test" in argv:
        self_test()
        return 0
    cat = build()
    if "--describe" in argv:
        describe(cat)
        return 0
    if "--draft" in argv:   # the C#-only catalogue the measuring harness starts from (no measured numbers)
        print(text({"version": VERSION, "kinds": scan()}), end="")
        return 0
    new = text(cat)
    if "--check" in argv:
        old = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else ""
        if old != new:
            print(f"{os.path.relpath(OUT, ROOT)}: STALE (run python3 Tools/Build/regatta_gene_catalogue.py)")
            return 1
        print(f"{os.path.relpath(OUT, ROOT)}: up to date")
        return 0
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "w", encoding="utf-8").write(new)
    describe(cat)
    print(f"wrote {os.path.relpath(OUT, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
