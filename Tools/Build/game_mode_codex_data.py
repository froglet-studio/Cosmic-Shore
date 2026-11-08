#!/usr/bin/env python3
"""Game Mode Codex data layer.

Reads the shipped Cosmic Shore assets (read-only) and writes codex.json: one entry per
arcade / arena card listed in GameLists/ArcadeGames.asset or ArenaGames.asset.

Sources, in order of authority:
  card          Assets/_SO_Assets/Games/ArcadeGame*.asset (SO_ArcadeGame)
  element       Assets/_Scripts/Data/ModeGenre.cs rows, else the scoring rule's metric
  scoring rule  <Mode>ScoringRule.asset by name, else the one the mode's scene references
  end target    Assets/Resources/EndConditionOverrides.asset (+ GameModes switch arms in the SO)
  intensities   the mode scene's Cell component (CellConfigs + cellTypeChoiceOptions,
                prefab-instance overrides applied over the source prefab), resolved through
                Cell.IntensityIndex: index = min(intensity, count) - 1
  facts         markdown tables in the mode doc keyed by intensity (I1..I4 columns or
                intensity rows), 4-element literal constants in the generator whose name or
                comment says intensity, per-intensity end targets, and each cell config's
                own Description. Every fact carries its source path:line. Nothing is invented.

Usage: python3 Tools/Build/game_mode_codex_data.py [--repo PATH] [--out PATH]

The Game Mode Codex artifact (https://claude.ai/artifact/3fwMCmPpkFBcTBggYnqMEH) embeds a slimmed copy of
this output; rerun this and republish the page when a card, scene or cell config changes. A READER: it writes
only --out (default: your temp dir).
"""
import argparse
import ast
import json
import os
import re
import sys
from pathlib import Path

ap = argparse.ArgumentParser()
ap.add_argument("--repo", default=str(Path(__file__).resolve().parents[2]))
ap.add_argument("--out", default=os.path.join(__import__("tempfile").gettempdir(), "game_mode_codex.json"))
args = ap.parse_args()
REPO = Path(args.repo)
ASSETS = REPO / "Assets"

def rel(p):
    return str(Path(p).relative_to(REPO))

def read(p):
    try:
        return Path(p).read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None

# ---------------------------------------------------------------- guid index
GUID = {}
for root, dirs, files in os.walk(ASSETS):
    for f in files:
        if f.endswith(".meta"):
            fp = os.path.join(root, f)
            try:
                with open(fp, encoding="utf-8", errors="replace") as fh:
                    for _ in range(4):
                        line = fh.readline()
                        m = re.match(r"guid:\s*([0-9a-f]{32})", line)
                        if m:
                            GUID[m.group(1)] = Path(fp[:-5])
                            break
            except OSError:
                pass

REF = re.compile(r"\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32}),\s*type:\s*\d+\}")

def guid_path(g):
    return GUID.get(g)

def scalar(text, key):
    """Top-level-ish scalar field (two-space indent in an SO asset). Handles folded strings."""
    m = re.search(rf"^  {re.escape(key)}:[ ]?(.*)$", text, re.M)
    if not m:
        return None
    val = m.group(1)
    # folded continuation lines are indented deeper than the key
    lines = text[m.end():].split("\n")[1:]
    for ln in lines:
        if re.match(r"^    \S", ln) and not re.match(r"^    - ", ln):
            val += " " + ln.strip()
        else:
            break
    val = val.strip()
    if len(val) >= 2 and val[0] == val[-1] and val[0] in "'\"":
        q = val[0]
        val = val[1:-1]
        if q == "'":
            val = val.replace("''", "'")
    return val

def ref_list(text, key):
    m = re.search(rf"^  {re.escape(key)}:\s*\n((?:  - .*\n)*)", text, re.M)
    if not m:
        return []
    return [g for _, g in REF.findall(m.group(1))]

def ref_field(text, key):
    m = re.search(rf"^  {re.escape(key)}:\s*(\{{[^}}]*\}})", text, re.M | re.S)
    if not m:
        return None
    r = REF.search(m.group(1).replace("\n", " "))
    return r.group(2) if r else None

def num(v):
    try:
        return int(v)
    except (TypeError, ValueError):
        try:
            return float(v)
        except (TypeError, ValueError):
            return v

# ---------------------------------------------------------------- enums
GM_SRC = read(ASSETS / "_Scripts/Data/Enums/GameModes.cs")
GAMEMODES = {int(v): k for k, v in re.findall(r"^\s+(\w+)\s*=\s*(\d+)", GM_SRC, re.M)}

GENRE_SRC = "Assets/_Scripts/Data/ModeGenre.cs"
GENRE_ROWS = {"Scurry": ["Mass"], "Tollway": ["Mass"], "ScarabScramble": ["Mass"],
              "BroodRush": ["Mass", "Space"]}
METRIC_ELEMENT = {0: "Time", 1: "Time", 2: "Time", 4: "Time", 9: "Time",
                  6: "Mass", 10: "Mass", 12: "Mass",
                  5: "Space", 7: "Space", 11: "Space",
                  3: "Charge", 8: "Charge"}
SM_FILE = next((p for p in ASSETS.rglob("ScoringMetric.cs")), None)
METRIC_NAMES = {}
if SM_FILE:
    METRIC_NAMES = {int(v): k for k, v in re.findall(r"^\s+(\w+)\s*=\s*(\d+)", read(SM_FILE), re.M)}

# ---------------------------------------------------------------- scoring rules
SR_DIR = ASSETS / "_SO_Assets/Scoring Rules"
SCORING = {}  # guid -> (path, metric)
for p in SR_DIR.glob("*ScoringRule.asset"):
    t = read(p)
    g = next((k for k, v in GUID.items() if v == p), None)
    SCORING[g] = (p, num(scalar(t, "metric")))
SCORING_BY_NAME = {v[0].stem: (g, v) for g, v in SCORING.items()}

# ---------------------------------------------------------------- end conditions
ECO_PATH = ASSETS / "Resources/EndConditionOverrides.asset"
ECO_SO = ASSETS / "_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"
eco_text = read(ECO_PATH) or ""
eco_so = read(ECO_SO) or ""
ECO_TOOLTIP = {}
for m in re.finditer(r'\[Tooltip\("((?:[^"\\]|\\.)*)"(?:\s*\+\s*"((?:[^"\\]|\\.)*)")*\)\]\s*(?:\[[^\]]*\]\s*)*public\s+[\w<>]+\s+(\w+)', eco_so):
    ECO_TOOLTIP[m.group(3)] = m.group(1)
# explicit GameModes => field arms in the SO
ECO_EXPLICIT = {f: mode for mode, f in re.findall(r"GameModes\.(\w+)\s*=>\s*(\w+)\s*,", eco_so)}
ECO_EXPLICIT.update({"joustCount": "Joust", "maelstromWinTarget": "Maelstrom",
                     "nucleusRushWaveTarget": "BroodRush",       # SO doc: "Nucleus Rush (Brood Rush)"
                     "wildlifeKillTarget": "WildlifeLiberation"})  # SO doc: "Wildlife Liberation kill target"
ECO_FIELDS = []  # (field, value, line)
lines = eco_text.split("\n")
i = 0
while i < len(lines):
    m = re.match(r"^  (\w+):\s*(.*)$", lines[i])
    if m and not m.group(1).endswith("Build") and m.group(1) not in (
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
            "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
            "m_EditorClassIdentifier", "autoRestoreBuildValuesBeforeBuild"):
        field, val = m.group(1), m.group(2).strip()
        if val == "":
            arr = []
            j = i + 1
            while j < len(lines) and re.match(r"^  - ", lines[j]):
                arr.append(num(lines[j][4:].strip()))
                j += 1
            ECO_FIELDS.append((field, arr, i + 1))
            i = j
            continue
        ECO_FIELDS.append((field, num(val), i + 1))
    i += 1

def eco_mode(field):
    if field in ECO_EXPLICIT:
        return ECO_EXPLICIT[field]
    best = None
    for name in GAMEMODES.values():
        if field.lower().startswith(name.lower()) and (best is None or len(name) > len(best)):
            best = name
    return best

ECO_BY_MODE = {}
for field, val, ln in ECO_FIELDS:
    md = eco_mode(field)
    if md:
        ECO_BY_MODE.setdefault(md, []).append((field, val, ln))

# ---------------------------------------------------------------- scenes & cells
CELL_SCRIPT = None
for g, p in GUID.items():
    if p.name == "Cell.cs" and "Controller/Environment" in str(p):
        CELL_SCRIPT = g
SCENES = {p.stem: p for p in ASSETS.rglob("*.unity")}
CHOICE = {0: "Random", 1: "IntensityWise", 2: "EnvironmentFree", 3: "CourseVariantWise"}
cs = read(ASSETS / "_Scripts/Controller/Environment/Cell.cs") or ""
em = re.search(r"enum CellTypeChoiceOptions\s*\{(.*?)\}", cs, re.S)
if em:
    body = re.sub(r"//.*", "", em.group(1))
    found = re.findall(r"(\w+)\s*=\s*(\d+)", body)
    if found:
        CHOICE = {int(v): k for k, v in found}

def split_docs(text):
    out = []
    for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)", text, re.M | re.S):
        out.append((int(m.group(1)), m.group(2), m.group(3)))
    return out

MOD_RE = re.compile(
    r"- target: \{fileID: (-?\d+), guid: [0-9a-f]{32},\s*type: \d+\}\s*\n\s*propertyPath: '?([^'\n]+)'?\s*\n\s*value:[ ]?(.*)\n\s*objectReference: (\{[^}]*\})")
CELL_RE = None

def apply_mods(cfgs, choice, mods, only_target=None):
    cfgs = list(cfgs)
    size = None
    touched = False
    for tgt, path, val, oref in mods:
        if only_target is not None and tgt != only_target:
            continue
        if path == "cellTypeChoiceOptions":
            choice = int(val.strip() or 0); touched = True
        elif path == "CellConfigs.Array.size":
            size = int(val.strip()); touched = True
        else:
            dm = re.match(r"CellConfigs\.Array\.data\[(\d+)\]", path)
            if dm:
                k = int(dm.group(1))
                while len(cfgs) <= k:
                    cfgs.append(None)
                r = REF.search(oref.replace("\n", " "))
                cfgs[k] = r.group(2) if r else None
                touched = True
    if size is not None:
        cfgs = (cfgs + [None] * size)[:size]
    return cfgs, choice, touched

def is_cell(body):
    return bool(CELL_SCRIPT) and re.search(rf"m_Script:\s*\{{fileID: 11500000, guid: {CELL_SCRIPT}", body)

def cells_in_asset(path, depth=0):
    """[(configs, choice, via)] for every Cell an asset (scene or prefab) carries, following
    prefab instances and variants. A nested instance's fileIDs are not remapped: when an
    instance carries exactly one Cell, its CellConfigs / cellTypeChoiceOptions modifications
    are applied to it whatever their target fileID."""
    t = read(path) or ""
    out = []
    if depth > 4:
        return out
    for cls, fid, body in split_docs(t):
        if cls == 114 and is_cell(body):
            ch = re.search(r"^  cellTypeChoiceOptions:\s*(\d+)", body, re.M)
            out.append((ref_list(body, "CellConfigs"), int(ch.group(1)) if ch else 0,
                        f"component in {rel(path)}"))
        elif cls == 1001:
            sp = re.search(r"m_SourcePrefab:\s*\{fileID: \d+, guid: ([0-9a-f]{32})", body)
            src = guid_path(sp.group(1)) if sp else None
            if not src or src.suffix != ".prefab":
                continue
            base = cells_in_asset(src, depth + 1)
            mods = MOD_RE.findall(body)
            if len(base) == 1:
                cfgs, choice, via = base[0]
                cfgs, choice, _ = apply_mods(cfgs, choice, mods)
                out.append((cfgs, choice, f"prefab instance of {rel(src)} in {rel(path)}"))
            else:
                out.extend(base)
    return out

def scene_cells(scene_path):
    t = read(scene_path) or ""
    return [{"configs": c, "choice": ch, "via": v} for c, ch, v in cells_in_asset(scene_path)], t

def cell_config_info(g):
    p = guid_path(g) if g else None
    if not p:
        return None
    t = read(p) or ""
    env = ref_field(t, "EnvironmentPrefab")
    envp = guid_path(env) if env else None
    sp = ref_field(t, "SpawnProfile")
    spp = guid_path(sp) if sp else None
    return {
        "asset": p.stem, "path": rel(p),
        "cellName": scalar(t, "CellName"),
        "description": scalar(t, "Description") or None,
        "difficulty": num(scalar(t, "Difficulty")),
        "environmentPrefab": envp.stem if envp else None,
        "environmentPrefabPath": rel(envp) if envp else None,
        "environmentIntensity": num(scalar(t, "EnvironmentIntensity")) if envp else None,
        "spawnProfile": spp.stem if spp else None,
    }

# ---------------------------------------------------------------- docs & generators
def norm(s):
    return re.sub(r"[^a-z0-9]", "", s.lower())

GENERATORS = {norm(p.name[len("author_"):-len("_assets.py")]): p
              for p in (REPO / "Tools/Build").glob("author_*_assets.py")}
DOCS = {}
for base in (ASSETS / "_Scripts/Controller/Arcade", ASSETS / "_Scripts/Controller", REPO / "Docs"):
    for p in base.rglob("*.md"):
        DOCS.setdefault(norm(p.stem), p)

def find_doc(mode, display):
    for k in (norm(mode), norm(display or "")):
        if k in DOCS:
            return DOCS[k]
    return None

def find_generator(mode, display):
    for k in (norm(mode), norm(display or "")):
        if k in GENERATORS:
            return GENERATORS[k]
    return None

ILVL = re.compile(r"^\**\s*I([1-4])\b\s*(.*?)\**$")

def cells_of(row):
    s = row.strip()
    if s.startswith("|"):
        s = s[1:]
    if s.endswith("|"):
        s = s[:-1]
    return [c.strip() for c in s.split("|")]

def clean(c):
    return re.sub(r"\*\*|`", "", c).strip()

def doc_facts(path):
    """Intensity-keyed facts from markdown tables."""
    text = read(path) or ""
    lines = text.split("\n")
    facts = {i: [] for i in range(1, 5)}
    heading = None
    i = 0
    while i < len(lines):
        ln = lines[i]
        hm = re.match(r"^#+\s+(.*)", ln)
        if hm:
            heading = hm.group(1).strip()
        if ln.startswith("|") and i + 1 < len(lines) and re.match(r"^\|[\s:|-]+\|?\s*$", lines[i + 1]):
            header = cells_of(ln)
            start = i
            body = []
            j = i + 2
            while j < len(lines) and lines[j].startswith("|"):
                body.append((j + 1, cells_of(lines[j])))
                j += 1
            src = f"{rel(path)}:{start + 1}"
            # A) columns I1..I4
            colmap = {}
            for ci, h in enumerate(header):
                m = ILVL.match(clean(h))
                if m and ci > 0:
                    colmap[ci] = (int(m.group(1)), m.group(2).strip())
            if len(colmap) >= 2:
                for ci, (lvl, nm) in colmap.items():
                    if nm:
                        facts[lvl].append({"label": "level name", "value": nm, "section": heading, "source": src})
                for lno, row in body:
                    label = clean(row[0]) if row else ""
                    for ci, (lvl, _) in colmap.items():
                        if ci < len(row) and clean(row[ci]):
                            facts[lvl].append({"label": label or "(row)", "value": clean(row[ci]),
                                               "section": heading, "source": f"{rel(path)}:{lno}"})
            else:
                # B) rows keyed by intensity
                h0 = clean(header[0]).lower() if header else ""
                for lno, row in body:
                    if not row:
                        continue
                    c0 = clean(row[0])
                    m = re.match(r"^I?([1-4])\b\s*(.*)$", c0)
                    if not m:
                        continue
                    if not (c0.startswith("I") or "intensit" in h0):
                        continue
                    lvl = int(m.group(1))
                    if m.group(2).strip():
                        facts[lvl].append({"label": "level name", "value": m.group(2).strip(),
                                           "section": heading, "source": f"{rel(path)}:{lno}"})
                    for ci in range(1, min(len(row), len(header))):
                        v = clean(row[ci])
                        if v:
                            facts[lvl].append({"label": clean(header[ci]) or f"col{ci}", "value": v,
                                               "section": heading, "source": f"{rel(path)}:{lno}"})
            i = j
            continue
        i += 1
    return facts

def short(v):
    try:
        s = json.dumps(v)
    except TypeError:
        s = repr(v)
    return v if len(s) <= 300 else s[:297] + "..."

def generator_info(path):
    text = read(path) or ""
    facts = {i: [] for i in range(1, 5)}
    doc = None
    try:
        tree = ast.parse(text)
    except SyntaxError:
        return None, facts
    doc = ast.get_docstring(tree)
    src_lines = text.split("\n")
    for node in tree.body:
        if not isinstance(node, (ast.Assign, ast.AnnAssign)):
            continue
        targets = node.targets if isinstance(node, ast.Assign) else [node.target]
        names = [t.id for t in targets if isinstance(t, ast.Name)]
        if not names or node.value is None:
            continue
        try:
            val = ast.literal_eval(node.value)
        except Exception:
            continue
        if not isinstance(val, (list, tuple)) or len(val) != 4:
            continue
        name = names[0]
        ctx = " ".join(src_lines[max(0, node.lineno - 5):node.lineno]).lower()
        if not ("intensit" in name.lower() or "intensit" in ctx or re.search(r"\bi1\b", ctx)):
            continue
        for k, v in enumerate(val, start=1):
            facts[k].append({"label": name, "value": short(v), "source": f"{rel(path)}:{node.lineno}"})
    return doc, facts

# ---------------------------------------------------------------- main
def list_cards(list_name):
    t = read(ASSETS / f"_SO_Assets/Games/GameLists/{list_name}.asset") or ""
    return ref_list(t, "Games")

entries = []
problems = {"element": [], "cells": [], "target": []}
seen = set()
for list_kind, list_name in (("arcade", "ArcadeGames"), ("arena", "ArenaGames")):
    for g in list_cards(list_name):
        p = guid_path(g)
        if not p or not p.name.startswith("ArcadeGame") or p.suffix != ".asset":
            print(f"skip unresolved/non-card guid {g} in {list_name}: {p}", file=sys.stderr)
            continue
        if p in seen:
            continue
        seen.add(p)
        t = read(p)
        mode_id = num(scalar(t, "Mode"))
        mode = GAMEMODES.get(mode_id, f"Unknown{mode_id}")
        display = scalar(t, "DisplayName")
        scene_name = scalar(t, "SceneName")
        vessels = []
        for vg in ref_list(t, "Vessels"):
            vp = guid_path(vg)
            vt = read(vp) if vp else ""
            vessels.append(scalar(vt, "Name") or (vp.stem.replace("SO_Class_", "") if vp else vg))
        scene_path = SCENES.get(scene_name) if scene_name else None
        cells, scene_text = scene_cells(scene_path) if scene_path else ([], "")

        # scoring rule
        rule = None
        if f"{mode}ScoringRule" in SCORING_BY_NAME:
            rg, (rp, metric) = SCORING_BY_NAME[f"{mode}ScoringRule"]
            rule = {"asset": rp.stem, "path": rel(rp), "metric": metric, "found_by": "name"}
        elif scene_text:
            refs = [rg for rg in SCORING if rg and rg in scene_text]
            if len(refs) >= 1:
                rp, metric = SCORING[refs[0]]
                rule = {"asset": rp.stem, "path": rel(rp), "metric": metric, "found_by": f"scene {scene_name}"}
                if len(refs) > 1:
                    rule["other_rules_in_scene"] = [SCORING[r][0].stem for r in refs[1:]]
        if rule:
            rule["metricName"] = METRIC_NAMES.get(rule["metric"])

        # element
        if mode in GENRE_ROWS:
            elements, esrc = GENRE_ROWS[mode], f"{GENRE_SRC} (mode row)"
        elif rule and rule["metric"] in METRIC_ELEMENT:
            elements, esrc = [METRIC_ELEMENT[rule["metric"]]], f"{GENRE_SRC} (metric fallback, {rule['path']})"
        else:
            elements, esrc = [], None
            problems["element"].append(display)

        # end target
        targets = []
        for field, val, ln in ECO_BY_MODE.get(mode, []):
            targets.append({"field": field, "value": val, "tooltip": ECO_TOOLTIP.get(field),
                            "source": f"{rel(ECO_PATH)}:{ln}"})
        if not targets:
            problems["target"].append(display)

        gen = find_generator(mode, display)
        docp = find_doc(mode, display)
        gdoc, gfacts = generator_info(gen) if gen else (None, {i: [] for i in range(1, 5)})
        dfacts = doc_facts(docp) if docp else {i: [] for i in range(1, 5)}

        # intensities
        primary = cells[0] if cells else None
        lo = num(scalar(t, "MinIntensity")) or 1
        hi = num(scalar(t, "MaxIntensity")) or 4
        intensities = []
        for lvl in range(1, 5):
            cfg = None
            note = None
            if primary and primary["configs"]:
                n = len(primary["configs"])
                ch = CHOICE.get(primary["choice"], str(primary["choice"]))
                if ch == "IntensityWise":
                    cfg = cell_config_info(primary["configs"][min(lvl, n) - 1])
                    note = "IntensityWise: CellConfigs[min(intensity, count) - 1]"
                elif n == 1:
                    cfg = cell_config_info(primary["configs"][0])
                    note = f"{ch}: the scene's only config, every intensity"
                else:
                    note = f"{ch}: configs are not chosen by intensity (see cell.configs)"
            facts = []
            for tf in targets:
                if isinstance(tf["value"], list) and len(tf["value"]) >= lvl:
                    facts.append({"label": f"end target ({tf['field']})", "value": tf["value"][lvl - 1],
                                  "source": tf["source"]})
            if cfg and cfg.get("description"):
                facts.append({"label": "cell config description", "value": cfg["description"],
                              "source": cfg["path"]})
            facts += gfacts.get(lvl, []) + dfacts.get(lvl, [])
            intensities.append({
                "intensity": lvl,
                "offered": lo <= lvl <= hi,
                "cellConfig": cfg,
                "cellChoiceNote": note,
                "facts": facts,
            })
        if primary and CHOICE.get(primary["choice"]) == "CourseVariantWise":
            problems.setdefault("courseVariantsNotIntensities", []).append(display)
        elif not primary or not any(x["cellConfig"] for x in intensities):
            problems["cells"].append(display)

        cell_out = []
        for c in cells:
            cell_out.append({
                "choice": CHOICE.get(c["choice"], c["choice"]), "via": c["via"],
                "configs": [cell_config_info(x) if x else None for x in c["configs"]],
            })

        entries.append({
            "name": display,
            "mode": mode,
            "modeId": mode_id,
            "list": list_kind,
            "card": rel(p),
            "scene": scene_name,
            "scenePath": rel(scene_path) if scene_path else None,
            "description": scalar(t, "Description"),
            "minPlayers": num(scalar(t, "MinPlayersAllowed")),
            "maxPlayers": num(scalar(t, "MaxPlayersAllowed")),
            "minIntensity": num(scalar(t, "MinIntensity")),
            "maxIntensity": num(scalar(t, "MaxIntensity")),
            "golfScoring": scalar(t, "GolfScoring") == "1",
            "vessels": vessels,
            "elements": elements,
            "elementSource": esrc,
            "scoringRule": rule,
            "endTargets": targets or None,
            "cells": cell_out,
            "intensities": intensities,
            "generator": rel(gen) if gen else None,
            "generatorDocstring": gdoc,
            "modeDoc": rel(docp) if docp else None,
        })

out = {"generatedBy": "codex_data.py", "repo": str(REPO),
       "notes": "Read-only extraction. Missing = null. Facts carry source path:line.",
       "modes": entries}
Path(args.out).write_text(json.dumps(out, indent=1, ensure_ascii=False))

# summary
print(f"{'name':22} | {'list':6} | {'element':11} | {'vessels':38} | int | facts")
for e in entries:
    nint = sum(1 for x in e["intensities"] if x["cellConfig"])
    nf = sum(len(x["facts"]) for x in e["intensities"])
    v = ",".join(e["vessels"])
    print(f"{e['name'][:22]:22} | {e['list']:6} | {'+'.join(e['elements']) or '-':11} | {v[:38]:38} | {nint}/4 | {nf}")
print(f"\n{len(entries)} modes -> {args.out}")
for k, v in problems.items():
    print(f"unresolved {k}: {', '.join(v) if v else 'none'}")
