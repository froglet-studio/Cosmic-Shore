#!/usr/bin/env python3
"""The Loop Atlas model: YAML files in the repo are the source of truth, the map is built from them.

The model lives in Docs/LoopAtlas/model/ as small YAML files a designer can edit by hand. This tool assembles them
into one model (Docs/LoopAtlas/model.json, committed so other tools can read it without YAML), validates it against
Docs/LoopAtlas/schema.json and the cross-reference checks below, and writes the Loop Atlas page (the claude.ai
artifact) from Docs/LoopAtlas/page/atlas.template.html with the model put where the template says.

  python3 Tools/Build/loop_atlas.py build              # validate, write model.json
  python3 Tools/Build/loop_atlas.py build --html OUT   # ...and write the page to OUT, ready to publish
  python3 Tools/Build/loop_atlas.py --check            # validate, fail if model.json is stale (CI runs this)
  python3 Tools/Build/loop_atlas.py report             # drift report: source docs that do not exist, and the rest
  python3 Tools/Build/loop_atlas.py import PAGE.html   # one-off: split a published page's model into YAML
  python3 Tools/Build/loop_atlas.py self-test          # the checks refuse what they must (negative controls)

Needs PyYAML and jsonschema (pip install pyyaml jsonschema). Docs/LoopAtlas/README.md explains the files and fields.
"""
from __future__ import annotations

import argparse
import copy
import json
import re
import sys
from pathlib import Path

try:
    import yaml
except ImportError:  # pragma: no cover
    sys.exit("loop_atlas.py needs PyYAML: pip install pyyaml jsonschema")
try:
    import jsonschema
except ImportError:  # pragma: no cover
    sys.exit("loop_atlas.py needs jsonschema: pip install pyyaml jsonschema")

REPO = Path(__file__).resolve().parents[2]
ROOT = REPO / "Docs" / "LoopAtlas"
MODEL_DIR = ROOT / "model"
SCHEMA = ROOT / "schema.json"
MODEL_JSON = ROOT / "model.json"
TEMPLATE = ROOT / "page" / "atlas.template.html"

# Where each top-level key of the model lives. Nodes come from loops/*.yaml (in file-name order), the fleet from
# fleet/*.yaml (in fleetOrder). Everything else is a key of one of these files.
FILES = {
    "meta.yaml": ["schema", "updated", "defaultVessel", "artifact", "fleetOrder"],
    "vocabulary.yaml": ["layers", "tiers", "drivers", "handoffs"],
    "kit.yaml": ["roles", "roleHoles", "kit", "pulse", "verbHole", "archetypes", "elementDrivers"],
    "proposals.yaml": ["proposals"],
    "edges.yaml": ["edges"],
}
# The order of the assembled model's keys (the order the page was first published with).
KEY_ORDER = ["schema", "updated", "layers", "tiers", "drivers", "handoffs", "nodes", "edges", "roles", "roleHoles",
             "kit", "pulse", "verbHole", "archetypes", "elementDrivers", "proposals", "fleet", "fleetOrder",
             "defaultVessel", "artifact"]
# The loops/ files an import writes: each top-level subtree of the era goes in one file.
LOOP_FILES = [
    ("00-long-game.yaml", ["era"]),
    ("01-sitting.yaml", ["sitting", "breath"]),
    ("02-toybox.yaml", ["toybox"]),
    ("03-arcade.yaml", ["arcade"]),
    ("04-arena.yaml", ["arena"]),
    ("05-voyages.yaml", ["voyages"]),
    ("06-arcs.yaml", ["arc."]),
    ("07-era.yaml", ["era."]),
]
MODEL_MARK = "/*@loop-atlas-model*/null"
PAGE_PATTERN = re.compile(r"var M = ")


class AtlasError(Exception):
    pass


# ----------------------------------------------------------------------------------------------- reading and writing

def _load_yaml(path: Path):
    with path.open(encoding="utf-8") as f:
        return yaml.safe_load(f)


class _Dumper(yaml.SafeDumper):
    """Short lists of plain values (timescales, drivers, touches) on one line; everything else in block style."""


def _represent_list(dumper, data):
    flow = len(data) <= 8 and all(isinstance(x, (int, float, str)) and len(str(x)) <= 24 for x in data)
    return dumper.represent_sequence("tag:yaml.org,2002:seq", data, flow_style=flow)


_Dumper.add_representer(list, _represent_list)


def _dump_yaml(data, header: str) -> str:
    body = yaml.dump(data, Dumper=_Dumper, sort_keys=False, allow_unicode=True, width=118, default_flow_style=False)
    return header + body


def assemble(model_dir: Path = MODEL_DIR) -> dict:
    """Read the YAML files and return the model, keys in KEY_ORDER. Raises AtlasError on a structural problem."""
    parts: dict = {}
    for name, keys in FILES.items():
        path = model_dir / name
        if not path.exists():
            raise AtlasError(f"missing {path.relative_to(REPO) if path.is_relative_to(REPO) else path}")
        data = _load_yaml(path) or {}
        extra = set(data) - set(keys)
        if extra:
            raise AtlasError(f"{name}: keys {sorted(extra)} belong in another file (this one holds {keys})")
        for k in keys:
            if k not in data:
                raise AtlasError(f"{name}: missing key '{k}'")
            parts[k] = data[k]

    nodes = []
    loop_files = sorted((model_dir / "loops").glob("*.yaml"))
    if not loop_files:
        raise AtlasError("no loops/*.yaml files")
    for path in loop_files:
        data = _load_yaml(path) or []
        if not isinstance(data, list):
            raise AtlasError(f"loops/{path.name}: must be a list of loops")
        nodes.extend(data)
    parts["nodes"] = nodes

    fleet = {}
    vessel_files = {p.stem: p for p in (model_dir / "fleet").glob("*.yaml")}
    order = parts["fleetOrder"]
    missing = [v for v in order if v not in vessel_files]
    unlisted = sorted(set(vessel_files) - set(order))
    if missing:
        raise AtlasError(f"fleetOrder names vessels with no fleet/<name>.yaml: {missing}")
    if unlisted:
        raise AtlasError(f"fleet/ has vessels missing from meta.yaml fleetOrder: {unlisted}")
    for v in order:
        fleet[v] = _load_yaml(vessel_files[v])
    parts["fleet"] = fleet

    return {k: parts[k] for k in KEY_ORDER}


def dump_model(model: dict) -> str:
    """The model as the page carries it: compact JSON, no ASCII escaping."""
    return json.dumps(model, ensure_ascii=False, separators=(",", ":"))


def model_json_text(model: dict) -> str:
    return json.dumps(model, ensure_ascii=False, indent=1) + "\n"


# ------------------------------------------------------------------------------------------------------------ checks

def validate(model: dict) -> list[str]:
    """Every problem with the model, as one line each. An empty list means the model is whole."""
    errors: list[str] = []
    schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
    validator = jsonschema.Draft202012Validator(schema)
    for e in sorted(validator.iter_errors(model), key=lambda e: list(e.absolute_path)):
        where = "/".join(str(p) for p in e.absolute_path) or "(root)"
        errors.append(f"schema: {where}: {e.message}")
    if errors:  # the cross-reference checks assume the shapes are right
        return errors

    def dupes(items, what):
        seen = set()
        for i in items:
            if i in seen:
                errors.append(f"{what} '{i}' is defined twice")
            seen.add(i)
        return seen

    tiers = {t["id"]: t for t in model["tiers"]}
    dupes([t["id"] for t in model["tiers"]], "tier")
    drivers = dupes([d["id"] for d in model["drivers"]], "driver")
    handoffs = dupes([h["id"] for h in model["handoffs"]], "handoff")
    letters = dupes([l["letter"] for l in model["layers"]], "layer letter")
    node_ids = dupes([n["id"] for n in model["nodes"]], "loop")
    dupes([p["id"] for p in model["proposals"]], "proposal")
    nodes = {n["id"]: n for n in model["nodes"]}

    def check_layers(value, where):
        bad = set(value) - letters
        if bad:
            errors.append(f"{where}: layer letters {sorted(bad)} are not in vocabulary.yaml layers")

    def check_drivers(ds, where):
        for d in ds:
            if d not in drivers:
                errors.append(f"{where}: driver '{d}' is not in vocabulary.yaml drivers")

    def check_holes(holes, where):
        for i, h in enumerate(holes or []):
            check_layers(h["on"], f"{where} hole {i}")

    roots = []
    for n in model["nodes"]:
        where = f"loop '{n['id']}'"
        if n["tier"] not in tiers:
            errors.append(f"{where}: tier {n['tier']} is not in vocabulary.yaml tiers")
        lo, hi = n["t"]
        if lo > hi:
            errors.append(f"{where}: timescale {n['t']} runs backwards")
        check_drivers(n["drivers"], where)
        check_holes(n.get("holes"), where)
        if not n["phases"] and n["kind"] != "fleet":
            errors.append(f"{where}: a {n['kind']} needs at least one phase")
        if "in" in n:
            check_layers(n["in"], where)
        parent = n.get("parent")
        if parent is None:
            roots.append(n["id"])
            continue
        if parent not in nodes:
            errors.append(f"{where}: parent '{parent}' is not a loop")
            continue
        p = nodes[parent]
        if "at" in n and n["at"] >= len(p["phases"]):
            errors.append(f"{where}: at={n['at']} but parent '{parent}' has {len(p['phases'])} phases")
        if n["tier"] > p["tier"]:
            errors.append(f"{where}: tier {n['tier']} sits above its parent '{parent}' (tier {p['tier']})")

    if len(roots) != 1:
        errors.append(f"the model needs exactly one root loop (no parent); found {roots}")
    else:
        # every loop reachable from the root, and no cycles in the parent chain
        for n in model["nodes"]:
            seen, cur = set(), n
            while cur.get("parent") is not None and cur["parent"] in nodes:
                if cur["id"] in seen:
                    errors.append(f"loop '{n['id']}': its parent chain loops back on itself")
                    break
                seen.add(cur["id"])
                cur = nodes[cur["parent"]]
            else:
                if cur["id"] != roots[0] and cur.get("parent") is None:
                    errors.append(f"loop '{n['id']}' is not reachable from the root '{roots[0]}'")

    for i, e in enumerate(model["edges"]):
        where = f"edge {i} ({e['from']} -> {e['to']})"
        for end in ("from", "to"):
            if e[end] not in node_ids:
                errors.append(f"{where}: '{e[end]}' is not a loop")
        if "handoff" in e and e["handoff"] not in handoffs:
            errors.append(f"{where}: handoff '{e['handoff']}' is not in vocabulary.yaml handoffs")
        if "in" in e:
            check_layers(e["in"], where)

    for p in model["proposals"]:
        for t in p["touches"]:
            if t not in node_ids:
                errors.append(f"proposal {p['id']}: touches '{t}', which is not a loop")

    for key, role in list(model["roles"].items()) + [("pulse", model["pulse"])]:
        check_drivers(role["drivers"], f"role '{key}'")
        check_holes(role.get("holes"), f"role '{key}'")
    check_holes(model["roleHoles"], "roleHoles")
    check_holes(model["kit"].get("holes"), "kit")
    check_layers(model["verbHole"]["on"], "verbHole")
    for el, ds in model["elementDrivers"].items():
        check_drivers(ds, f"elementDrivers '{el}'")

    if model["defaultVessel"] not in model["fleet"]:
        errors.append(f"defaultVessel '{model['defaultVessel']}' is not in the fleet")
    archetypes = set(model["archetypes"])

    def check_combo(c, where):
        check_drivers(c["drivers"], where)
        if "in" in c:
            check_layers(c["in"], where)
        for j, s in enumerate(c.get("sub", [])):
            if "at" in s and s["at"] >= len(c["chain"]):
                errors.append(f"{where} sub {j}: at={s['at']} but the combo has {len(c['chain'])} steps")
            check_combo(s, f"{where} sub '{s['name']}'")

    for name, v in model["fleet"].items():
        for el, verb in v["verbs"].items():
            if verb["arch"] not in archetypes:
                errors.append(f"vessel '{name}' {el}: archetype '{verb['arch']}' is not in kit.yaml archetypes")
            hand = verb.get("hand")
            if hand:
                if hand["at"] >= len(hand["phases"]):
                    errors.append(f"vessel '{name}' {el}: hand loop at={hand['at']} past its {len(hand['phases'])} phases")
                if "in" in hand:
                    check_layers(hand["in"], f"vessel '{name}' {el} hand loop")
        for c in v["combos"]:
            check_combo(c, f"vessel '{name}' combo '{c['name']}'")
    return errors


def report(model: dict) -> list[str]:
    """Drift that does not fail the build yet: things a person should look at."""
    lines = []
    doc_re = re.compile(r"^((?:Docs|Assets|Tools)/[^ ]+?\.(?:md|asset|json|py|cs))\b")
    cited = set()
    for n in model["nodes"]:
        for s in n["src"]:
            m = doc_re.match(s)
            if not m:
                continue
            cited.add(m.group(1))
            if not (REPO / m.group(1)).exists():
                lines.append(f"loop '{n['id']}' cites {m.group(1)}, which is not in the repo")
    for name, v in model["fleet"].items():
        for el, verb in v["verbs"].items():
            src = verb["source"]
            if doc_re.match(src) and not (REPO / doc_re.match(src).group(1)).exists():
                lines.append(f"vessel '{name}' {el} cites {src}, which is not in the repo")
    for n in model["nodes"]:
        tier = next(t for t in model["tiers"] if t["id"] == n["tier"])
        lo, hi = tier["span"]
        if n["t"][1] < lo or n["t"][0] > hi:
            lines.append(f"loop '{n['id']}': timescale {n['t']} s lies outside its tier {tier['term']} {tier['span']} s")
    open_holes = sum(len(n.get("holes", [])) for n in model["nodes"])
    lines.append(f"{len(model['nodes'])} loops, {len(model['edges'])} edges, {len(model['fleet'])} vessels, "
                 f"{open_holes} open holes on loops, {len(model['proposals'])} proposals, {len(cited)} docs cited")
    return lines


# --------------------------------------------------------------------------------------------------------- the page

def render_page(model: dict, template: Path = TEMPLATE) -> str:
    text = template.read_text(encoding="utf-8")
    if text.count(MODEL_MARK) != 1:
        raise AtlasError(f"{template.name} must hold the model mark {MODEL_MARK!r} exactly once")
    return text.replace(MODEL_MARK, dump_model(model))


def extract_page_model(page: str) -> tuple[dict, int, int]:
    m = PAGE_PATTERN.search(page)
    if not m or len(PAGE_PATTERN.findall(page)) != 1:
        raise AtlasError("the page must hold exactly one 'var M = {...}'")
    start = m.end()
    model, length = json.JSONDecoder().raw_decode(page[start:])
    return model, start, start + length


def do_import(page_path: Path, model_dir: Path = MODEL_DIR, template: Path = TEMPLATE) -> None:
    page = page_path.read_text(encoding="utf-8")
    model, start, end = extract_page_model(page)
    if list(model) != KEY_ORDER:
        raise AtlasError(f"the page's model keys {list(model)} differ from KEY_ORDER; update the tool first")
    header = ("# Loop Atlas model: {what}. Docs/LoopAtlas/README.md explains the fields.\n"
              "# Edit here; Tools/Build/loop_atlas.py build rebuilds model.json and the page.\n")
    for name, keys in FILES.items():
        (model_dir / name).parent.mkdir(parents=True, exist_ok=True)
        (model_dir / name).write_text(_dump_yaml({k: model[k] for k in keys}, header.format(what=name[:-5])),
                                      encoding="utf-8")
    loops_dir = model_dir / "loops"
    loops_dir.mkdir(parents=True, exist_ok=True)
    placed = []
    for fname, prefixes in LOOP_FILES:
        chunk = []
        for n in model["nodes"]:
            top = n
            while top.get("parent") not in (None, "era", "sitting"):
                top = next(x for x in model["nodes"] if x["id"] == top["parent"])
            if any(top["id"] == p or (p.endswith(".") and top["id"].startswith(p)) for p in prefixes):
                chunk.append(n)
        placed.extend(chunk)
        (loops_dir / fname).write_text(_dump_yaml(chunk, header.format(what="loops, " + fname[3:-5])),
                                       encoding="utf-8")
    if [n["id"] for n in placed] != [n["id"] for n in model["nodes"]]:
        raise AtlasError("the loops did not split into LOOP_FILES in the page's order; adjust LOOP_FILES")
    fleet_dir = model_dir / "fleet"
    fleet_dir.mkdir(parents=True, exist_ok=True)
    for name, vessel in model["fleet"].items():
        (fleet_dir / f"{name}.yaml").write_text(_dump_yaml(vessel, header.format(what=f"vessel {name}")),
                                                encoding="utf-8")
    template.parent.mkdir(parents=True, exist_ok=True)
    template.write_text(page[:start] + MODEL_MARK + page[end:], encoding="utf-8")
    back = assemble(model_dir)
    if dump_model(back) != dump_model(model):
        raise AtlasError("the YAML files do not read back to the page's model")
    if render_page(back, template) != page:
        raise AtlasError("the template does not rebuild the page byte for byte")


# --------------------------------------------------------------------------------------------------------- self-test

def self_test() -> int:
    model = assemble()
    base = validate(model)
    if base:
        print("self-test: the shipped model must be valid first:\n  " + "\n  ".join(base))
        return 1
    if render_page(model) == TEMPLATE.read_text(encoding="utf-8"):
        print("self-test: render_page did not put the model in the page")
        return 1

    def mutated(fn):
        m = copy.deepcopy(model)
        fn(m)
        return validate(m)

    cases = {
        "unknown parent": lambda m: m["nodes"][1].__setitem__("parent", "no.such.loop"),
        "duplicate loop id": lambda m: m["nodes"].append(copy.deepcopy(m["nodes"][1])),
        "unknown driver": lambda m: m["nodes"][0]["drivers"].append("boredom"),
        "edge to nowhere": lambda m: m["edges"][0].__setitem__("to", "nowhere"),
        "unknown handoff": lambda m: m["edges"][3].__setitem__("handoff", "teleport"),
        "proposal touches nothing": lambda m: m["proposals"][4]["touches"].append("gone"),
        "phase index past the parent": lambda m: m["nodes"][2].__setitem__("at", 99),
        "child above its parent": lambda m: m["nodes"][2].__setitem__("tier", 7),
        "timescale backwards": lambda m: m["nodes"][0].__setitem__("t", [10, 1]),
        "unknown field (schema)": lambda m: m["nodes"][0].__setitem__("colour", "red"),
        "missing phases (schema)": lambda m: m["nodes"][0].pop("phases"),
        "loop with no phases": lambda m: m["nodes"][1].__setitem__("phases", []),
        "bad layer letter": lambda m: m["nodes"][0]["holes"][0].__setitem__("on", "nq"),
        "second root": lambda m: m["nodes"][5].pop("parent"),
        "unknown default vessel": lambda m: m.__setitem__("defaultVessel", "Narwhal"),
        "unknown archetype": lambda m: next(iter(next(iter(m["fleet"].values()))["verbs"].values()))
                                       .__setitem__("arch", "telepathy"),
    }
    failed = 0
    for name, fn in cases.items():
        errs = mutated(fn)
        ok = bool(errs)
        failed += not ok
        print(f"  {'refused' if ok else 'MISSED '}  {name}" + (f": {errs[0]}" if ok else ""))
    if failed:
        print(f"self-test: {failed} negative control(s) were not refused")
        return 1
    print(f"self-test: all {len(cases)} negative controls refused")
    return 0


# -------------------------------------------------------------------------------------------------------------- main

def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="validate and fail if model.json is stale")
    sub = ap.add_subparsers(dest="cmd")
    b = sub.add_parser("build", help="validate and write model.json")
    b.add_argument("--html", type=Path, help="also write the page here")
    sub.add_parser("report", help="drift report")
    i = sub.add_parser("import", help="split a published page's model into YAML (one-off)")
    i.add_argument("page", type=Path)
    sub.add_parser("self-test", help="negative controls")
    args = ap.parse_args(argv)

    try:
        if args.cmd == "import":
            do_import(args.page)
            print(f"imported {args.page} into {MODEL_DIR.relative_to(REPO)} and {TEMPLATE.relative_to(REPO)}")
            args.cmd = "build"
            args.html = None
        if args.cmd == "self-test":
            return self_test()
        model = assemble()
        errors = validate(model)
        if errors:
            print(f"loop_atlas: {len(errors)} problem(s) in the model:")
            for e in errors:
                print(f"  {e}")
            return 1
        if args.cmd == "report":
            for line in report(model):
                print(line)
            return 0
        text = model_json_text(model)
        if args.check or args.cmd is None:
            current = MODEL_JSON.read_text(encoding="utf-8") if MODEL_JSON.exists() else ""
            if current != text:
                print(f"loop_atlas: {MODEL_JSON.relative_to(REPO)} is stale; run python3 Tools/Build/loop_atlas.py build")
                return 1
            render_page(model)  # the template still takes the model
            print(f"loop_atlas: model valid ({len(model['nodes'])} loops, {len(model['edges'])} edges, "
                  f"{len(model['fleet'])} vessels) and model.json up to date")
            return 0
        MODEL_JSON.write_text(text, encoding="utf-8")
        print(f"wrote {MODEL_JSON.relative_to(REPO)}")
        if args.html:
            args.html.write_text(render_page(model), encoding="utf-8")
            print(f"wrote {args.html}")
        return 0
    except AtlasError as e:
        print(f"loop_atlas: {e}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
