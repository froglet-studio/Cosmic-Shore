#!/usr/bin/env python3
"""The Loop Atlas: build and check the artifact that maps every gameplay loop in Cosmic Shore.

Garrett, 2026-11-18: "I want to visualize a model for how all of our gameplay loops fit together into a single
structure ... a very important tool for humans and AI to keep our shared understanding of the whole product up to
date." The artifact: https://claude.ai/artifact/1g4Xc6uSEt1EQFeQmHqsgs. How to use and update it: Docs/LoopAtlas/README.md.

    python3 Tools/Build/loop_atlas.py            validate the model and write Docs/LoopAtlas/artifact/project/
    python3 Tools/Build/loop_atlas.py --check    exit 1 when the model is invalid or the written files are stale
    python3 Tools/Build/loop_atlas.py --self-test  prove the validator fires on broken models
    python3 Tools/Build/loop_atlas.py --stats    print loop, hole and proposal counts per layer

Three sources, none typed twice:
- Docs/LoopAtlas/loop_model.json: the loops above the kit, the three layers, holes, proposals (hand-written).
- Docs/FleetCodex/abilities.json: what each vessel's abilities do (the Fleet Codex's table; this is a reader of it).
- Docs/ElementalLoop/STATUS.md: each vessel's games per element, its steps 1-3 and its arena games (generated board).
The look is the Froglet tokens (Docs/UI/froglet-tokens.json). A READER tool: it writes only under Docs/LoopAtlas/artifact/.

Standard library only.
"""
import argparse
import copy
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / "Docs" / "LoopAtlas"
MODEL = DOC / "loop_model.json"
SRC = DOC / "src"
OUT = DOC / "artifact" / "project"
ABILITIES = ROOT / "Docs" / "FleetCodex" / "abilities.json"
STATUS = ROOT / "Docs" / "ElementalLoop" / "STATUS.md"
TOKENS = ROOT / "Docs" / "UI" / "froglet-tokens.json"
ARTIFACT = "https://claude.ai/artifact/1g4Xc6uSEt1EQFeQmHqsgs"
ELEMENTS = ["Time", "Charge", "Mass", "Space"]
FONTS = "https://fonts.googleapis.com/css2?family=Aldrich&family=Chakra+Petch:wght@500;600;700&display=swap"
# How long one pass of a verb takes, by the shape of its trigger. A model archetype may carry its own "t".
ARCH_T = {"hold_release": [2, 12], "mode": [5, 30], "seat": [4, 20], "hold": [2, 12], "passive": [2, 15],
          "omni": [10, 60], "card": [10, 40], "off": [1, 1], "tap": [2, 20]}


# ---------------------------------------------------------------- reading the sources
def label_of(key):
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", key)


def read_board(text):
    """STATUS.md section 1 (steps per element, missing kit, arenas) and section 3 (games per element)."""
    board, games = {}, {}
    sec = None
    for line in text.splitlines():
        if line.startswith("## "):
            sec = line[3:5].strip(". ")
            continue
        if not line.startswith("| ") or line.startswith("| Vessel") or line.startswith("|---"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if sec == "1" and len(cells) == 7:
            marks = {e: cells[1 + i].strip("`") for i, e in enumerate(ELEMENTS)}
            board[cells[0]] = {"marks": marks, "missing": "" if cells[5] == "-" else cells[5],
                               "arenas": [] if cells[6] == "-" else [re.sub(r"(?<=[a-z])(?=[A-Z])", " ", a.strip()) for a in cells[6].split(",")]}
        elif sec == "3" and len(cells) == 5:
            games[cells[0]] = {e: ([] if cells[1 + i] in ("-", "excused") else [g.strip() for g in cells[1 + i].split(",")])
                               for i, e in enumerate(ELEMENTS)}
            games[cells[0]]["_excused"] = [e for i, e in enumerate(ELEMENTS) if cells[1 + i] == "excused"]
    return board, games


def archetype_of(trigger, archetypes):
    t = (trigger or "").lower()
    for key, a in archetypes.items():
        if re.search(a["match"], t):
            return key
    return "tap"


def build_fleet(model, abilities, board, games):
    fleet = {}
    names = sorted(set(k for k in abilities if not k.startswith("_")) | set(board))
    for v in names:
        ab = abilities.get(v, {})
        mv = model["vessels"].get(v, {})
        verbs = {}
        for e in ELEMENTS + ["Omni"]:
            a = ab.get(e)
            if not isinstance(a, dict):
                continue
            arch = archetype_of(a.get("trigger"), model["archetypes"])
            name = a.get("ability", e)
            if e == "Omni" and name[:1].islower():  # the codex words an unnamed omni as what it does
                name = "Omni crystal"
            verb = {"ability": name, "does": a.get("does", ""),
                    "trigger": a.get("trigger", ""), "scales": a.get("scales", ""), "source": a.get("source", ""),
                    "arch": arch, "t": model["archetypes"][arch].get("t", ARCH_T[arch])}
            hand = mv.get("verbs", {}).get(e)
            if hand:
                verb["hand"] = hand
            verbs[e] = verb
        b = board.get(v, {"marks": {}, "missing": "not on the board", "arenas": []})
        g = games.get(v, {})
        doc = next((x["source"] for x in verbs.values() if x.get("source")), "")
        fleet[v] = {"label": label_of(v), "verbs": verbs, "board": b["marks"], "missing": b["missing"], "arenas": b["arenas"],
                    "games": {e: g.get(e, []) for e in ELEMENTS}, "excused": g.get("_excused", []),
                    "lean": mv.get("lean"), "leanWhy": mv.get("leanWhy"), "combos": mv.get("combos", []), "doc": doc}
    return fleet


def resolve(model=None):
    """The model the page runs on: the hand-written model plus the fleet read from the codex and the board."""
    model = copy.deepcopy(model) if model is not None else json.loads(MODEL.read_text(encoding="utf-8"))
    abilities = json.loads(ABILITIES.read_text(encoding="utf-8"))
    board, games = read_board(STATUS.read_text(encoding="utf-8"))
    fleet = build_fleet(model, abilities, board, games)
    out = {k: model[k] for k in ("schema", "updated", "layers", "tiers", "drivers", "handoffs", "nodes", "edges", "roles",
                                 "roleHoles", "kit", "pulse", "verbHole", "archetypes", "elementDrivers", "proposals")}
    out["fleet"] = fleet
    out["fleetOrder"] = sorted(fleet, key=lambda k: fleet[k]["label"])
    out["defaultVessel"] = "Dolphin"
    out["artifact"] = ARTIFACT
    return out


# ---------------------------------------------------------------- validation
def validate(model, fleet=None):
    errs = []
    ids = {}
    drivers = {d["id"] for d in model["drivers"]}
    handoffs = {h["id"] for h in model["handoffs"]}
    for n in model["nodes"]:
        if n["id"] in ids:
            errs.append(f"duplicate loop id {n['id']}")
        ids[n["id"]] = n
    roots = [n for n in model["nodes"] if not n.get("parent")]
    if len(roots) != 1:
        errs.append(f"exactly one loop may have no parent, found {[n['id'] for n in roots]}")

    def check_holes(where, holes):
        for h in holes or []:
            if not set(h.get("on", "")) <= set("np") or not h.get("on"):
                errs.append(f"{where}: a hole's 'on' is n, p or np, got {h.get('on')!r}")
            if not h.get("q"):
                errs.append(f"{where}: a hole needs its question 'q'")

    for n in model["nodes"]:
        w = n["id"]
        if not set(n.get("in", "npx")) <= set("npx"):
            errs.append(f"{w}: 'in' is made of n, p, x")
        if not 0 <= n.get("tier", -1) <= 7:
            errs.append(f"{w}: tier is 0 to 7")
        t = n.get("t")
        if not (isinstance(t, list) and len(t) == 2 and 0 < t[0] <= t[1]):
            errs.append(f"{w}: t is [shortest, longest] seconds of one pass")
        for d in n.get("drivers", []):
            if d not in drivers:
                errs.append(f"{w}: unknown driver {d}")
        if n.get("kit") and n["kit"] not in model["roles"]:
            errs.append(f"{w}: unknown kit role {n['kit']}")
        p = n.get("parent")
        if p:
            if p not in ids:
                errs.append(f"{w}: parent {p} does not exist")
            else:
                par = ids[p]
                if n.get("tier", 0) > par.get("tier", 7):
                    errs.append(f"{w}: a loop cannot be slower (tier {n['tier']}) than the loop it sits in ({p}, tier {par['tier']})")
                if n.get("at") is not None and not 0 <= n["at"] < max(1, len(par.get("phases", []))):
                    errs.append(f"{w}: 'at' {n['at']} is not a step of {p}")
                if not set(n.get("in", "npx")) <= set(par.get("in", "npx")):
                    errs.append(f"{w}: is on a layer its parent {p} is not on")
        check_holes(w, n.get("holes"))
        if "x" in n.get("in", "npx") and "n" not in n.get("in", "npx") and "p" not in n.get("in", "npx"):
            if (n.get("prop") or {}).get("s") != "add":
                errs.append(f"{w}: a loop only on the proposed layer is marked prop.s = add")
    for e in model["edges"]:
        for k in ("from", "to"):
            if e[k] not in ids:
                errs.append(f"edge {e['from']} -> {e['to']}: {e[k]} does not exist")
        if e.get("kind") not in ("train", "cross", "feed"):
            errs.append(f"edge {e['from']} -> {e['to']}: kind is train, cross or feed")
        if e.get("handoff") and e["handoff"] not in handoffs:
            errs.append(f"edge {e['from']} -> {e['to']}: unknown hand-off {e['handoff']}")
        if e["from"] in ids and e["to"] in ids:
            both = set(ids[e["from"]].get("in", "npx")) & set(ids[e["to"]].get("in", "npx"))
            if not set(e.get("in", "npx")) <= both:
                errs.append(f"edge {e['from']} -> {e['to']}: is on a layer one of its ends is not on")
    for key, r in model["roles"].items():
        for d in r.get("drivers", []):
            if d not in drivers:
                errs.append(f"role {key}: unknown driver {d}")
    check_holes("roleHoles", model.get("roleHoles"))
    check_holes("kit", model["kit"].get("holes"))
    check_holes("pulse", model["pulse"].get("holes"))
    for p in model["proposals"]:
        for t in p.get("touches", []):
            if t not in ids:
                errs.append(f"proposal {p['id']}: touches {t}, which does not exist")
    for v, mv in model["vessels"].items():
        if fleet is not None and v not in fleet:
            errs.append(f"vessels.{v}: not a vessel the codex or the board knows")
        for c in mv.get("combos", []):
            for d in c.get("drivers", []):
                if d not in drivers:
                    errs.append(f"vessels.{v} combo {c.get('name')}: unknown driver {d}")
            for step in c.get("chain", []):
                if step[0] not in ("", "Omni", *ELEMENTS):
                    errs.append(f"vessels.{v} combo {c.get('name')}: {step[0]!r} is not an element")
        for e in mv.get("verbs", {}):
            if e not in ELEMENTS + ["Omni"]:
                errs.append(f"vessels.{v}.verbs: {e} is not an element")
    return errs


# ---------------------------------------------------------------- the artifact's files
def js_json(obj):
    s = json.dumps(obj, ensure_ascii=False, separators=(",", ":"))
    return s.replace("<", "\\u003c").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")


def tokens_json():
    fr = json.loads(TOKENS.read_text(encoding="utf-8"))
    ds, sh = fr["themes"]["deep-sea"], fr["themes"]["shore"]
    ramp_ds = ["#566DFE", "#667EFD", "#758FFB", "#869EFC", "#96ACFC", "#A8BBFB", "#B9C9FA", "#D2DBFC"]
    ramp_sh = ["#566DFE", "#4B5DF5", "#404EEC", "#383CE0", "#302BD5", "#2A1BC2", "#230BB0", "#16017F"]
    terms = ["Pulse", "Verb", "Combo", "Role", "Round", "Session", "Arc", "Era"]

    def c(name, a, b, usage):
        return {"name": name, "value": {"deep-sea": a.lower(), "shore": b.lower()}, "usage": usage}

    colors = [
        c("ground", ds["ground"], sh["ground"], "The page and the map behind every loop."),
        c("ground-deep", ds["ground-deep"], sh["ground-deep"], "The inspector panel, list rows on hover, and every second ring's fill so nesting reads."),
        c("surface", ds["surface"], sh["surface"], "Buttons at rest. Carries `text` only."),
        c("surface-raised", ds["surface-raised"], sh["surface-raised"], "Buttons on hover. Carries `text` only."),
        c("line", ds["line"], sh["line"], "Hairlines: panel edges, spokes from a step to the loop it opens, list rules."),
        c("text", ds["text"], sh["text"], "Names and body copy on `ground`, `ground-deep`, `surface` and `surface-raised`."),
        c("text-soft", ds["text-soft"], sh["text-soft"], "Timescales, sources and captions on `ground` and `ground-deep` only (it fails on `surface`)."),
        c("text-off", ds["text-off"], sh["text-off"], "Froglet's disabled text. Not used for anything a reader needs."),
        c("selection", ds["selection"], sh["selection"], "The picked loop's ring, pressed buttons, links, the focus ring. A choice you can change again."),
        c("on-selection", ds["on-fill"], "#FFFFFF", "Text on a `selection` fill."),
        c("proposed", ds["cta"], sh["cta"], "The Proposed layer: new loops, proposed crossings, the fill of a hole, the step where a verb's feeling lands. Lime is a call you can still change."),
        c("proposed-fill", ds["cta-fill"], sh["cta-fill"], "The one call-to-action button. Carries `on-fill`."),
        c("on-fill", ds["on-fill"], sh["on-fill"], "Text on `proposed-fill`."),
        c("planned", "#FE6EF7", sh["commit"], "The Planned layer: loops the plan has and the game lacks. Ruby is a commitment. Deep Sea uses ruby-76 so labels pass 4.5:1."),
        c("hole", ds["danger"], sh["danger"], "A hole: the diamond count on a ring, an open question. Fire means deal with it. Always with a number or the word."),
    ]
    for i in range(8):
        colors.append(c(f"tier-{i}", ramp_ds[i], ramp_sh[i],
                        f"The {terms[i]} tier's ring, bar and chip. One ordered blue ramp, stepped between Froglet's blue-60 and blue-84 "
                        f"(Deep Sea: slower is lighter; Shore: slower is darker). A mark, never body text."))
    return {
        "name": "Cosmic Shore Loop Atlas", "version": 1,
        "meta": {"source": "Docs/UI/froglet-tokens.json (Tools/Build/froglet_style.py), through Tools/Build/loop_atlas.py"},
        "color": {"themes": [{"id": "deep-sea", "name": "Deep Sea"}, {"id": "shore", "name": "Shore"}], "tokens": colors},
        "type": {
            "fonts": [],
            "families": {"read": "\"Aldrich\", system-ui, sans-serif", "label": "\"Chakra Petch\", system-ui, sans-serif"},
            "groups": [
                {"name": "Read (Aldrich)", "family": "read", "styles": [
                    {"name": "loop-title", "fontSize": "22px", "lineHeight": 1.2, "fontWeight": 400, "usage": "The picked loop's name in the inspector.", "sample": "A cell"},
                    {"name": "body", "fontSize": "14px", "lineHeight": 1.45, "fontWeight": 400, "usage": "What a loop is, notes, holes.", "sample": "The squad takes the cell's nucleus for its domain."},
                    {"name": "small", "fontSize": "12px", "lineHeight": 1.4, "fontWeight": 400, "usage": "Timescales and sources. Numbers are tabular.", "sample": "45 s to 3 min a pass"}]},
                {"name": "Press and label (Chakra Petch)", "family": "label", "styles": [
                    {"name": "button", "fontSize": "13px", "lineHeight": 1.2, "fontWeight": 600, "letterSpacing": "0.04em", "usage": "Buttons, always capitals.", "sample": "NOW  PLANNED  PROPOSED"},
                    {"name": "map-name", "fontSize": "15px", "lineHeight": 1.2, "fontWeight": 600, "usage": "A loop's name on the map.", "sample": "Brood Rush"},
                    {"name": "map-step", "fontSize": "12px", "lineHeight": 1.2, "fontWeight": 600, "usage": "A step's label round the picked ring.", "sample": "3  The cell is yours"},
                    {"name": "kicker", "fontSize": "11px", "lineHeight": 1.2, "fontWeight": 600, "letterSpacing": "0.08em", "usage": "Tier terms, section heads and chips, always capitals.", "sample": "ROUND"}]},
            ]},
        "spacing": {"tokens": [{"name": f"space-{v}", "value": f"{v}px", "usage": u} for v, u in [
            (4, "Between a chip and its neighbour."), (8, "Inside buttons and between related lines."), (12, "Panel insets on a phone."),
            (16, "Panel insets; the gap between controls."), (24, "Between sections of a list."), (32, "Froglet's scale, kept whole."),
            (48, "Froglet's scale, kept whole."), (64, "Froglet's scale, kept whole."), (96, "Froglet's scale, kept whole.")]]},
        "radius": {"tokens": [
            {"name": "sliver-large", "value": "14px", "usage": "The corner cut on large surfaces. Froglet cuts two opposite corners; nothing is rounded. Only loops are circles."},
            {"name": "sliver-small", "value": "10px", "usage": "The corner cut on buttons."}]},
        "stroke": {"tokens": [
            {"name": "hairline", "value": "1px", "usage": "Lines, spokes, a Verb's ring."},
            {"name": "emphasis", "value": "2px", "usage": "A Round's ring and slower; the picked ring is 2.5."}]},
    }


def preview(group, height, subtitle, title, call, extra_css=""):
    return (f'<!-- @dsCard group="{group}" height={height} subtitle="{subtitle}" -->\n<!doctype html>\n<html lang="en">\n<head>\n'
            f'<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1">\n<title>{title}</title>\n'
            f'<link rel="preconnect" href="https://fonts.googleapis.com">\n<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>\n'
            f'<link href="{FONTS}" rel="stylesheet">\n'
            f'<style>\n  html, body {{ margin: 0; background: var(--ground); }}\n  {extra_css}\n</style>\n</head>\n<body>\n<div id="root"></div>\n'
            f'<script>\n  {call}\n</script>\n</body>\n</html>\n')


def cover():
    return f'''<!-- @dsCard height=288 -->
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Cosmic Shore Loop Atlas</title>
<link href="{FONTS}" rel="stylesheet">
<style>
  html, body {{ margin: 0; height: 100%; }}
  body {{ background: var(--ground); color: var(--text); font-family: var(--font-read); overflow: hidden; }}
  .cover {{ position: relative; width: 960px; height: 288px; overflow: hidden; }}
  .band {{ position: absolute; left: 0; top: 0; }}
  .b-deep {{ fill: var(--tier-2); }}
  .b-sel {{ fill: var(--selection); }}
  .b-pale {{ fill: var(--tier-6); }}
  .b-prop {{ fill: var(--proposed); }}
  .b-hole {{ fill: var(--hole); }}
  .ring {{ fill: none; stroke: var(--ground); stroke-width: 2px; }}
  .words {{ position: absolute; left: 32px; right: 32px; bottom: 24px; }}
  .name {{ margin: 0; font-size: 56px; line-height: .95; font-weight: 400; color: var(--text); white-space: nowrap; }}
  .tag {{ margin: 12px 0 0; font-size: 14px; line-height: 20px; color: var(--text-soft); }}
</style>
</head>
<body>
<div class="cover">
<svg class="band" viewBox="0 0 960 112" width="960" height="112" aria-hidden="true">
<!--
  blocks      tier-2 352x112 (the map's working blue, largest) . selection 240x112 (the picked loop) . tier-6 208x112 (the slow tiers) . proposed 96x64 (a state: kept small) . hole 32x32 (a state: smallest)
  arrangement the top-band skeleton, because the name is too wide for the left zone: a strip of unequal bands flush across the top, space-16 gutters cut by the sliver, the two state colours hanging off the right
  pattern     the literal motif, a loop inside a loop: rings cut out of the blocks in ground, radii on the spacing scale (16, 32, 48, 64, 96), each block's rings sharing a centre like a loop and the faster loops it is made of
  scales      band 112 tall; block sides in space-16 multiples; corners cut by sliver-large (14); ring stroke = emphasis (2)
-->
<polygon class="b-deep" points="14,0 352,0 352,98 338,112 0,112 0,14"/>
<polygon class="b-sel" points="382,0 608,0 608,98 594,112 368,112 368,14"/>
<polygon class="b-pale" points="638,0 832,0 832,98 818,112 624,112 624,14"/>
<polygon class="b-prop" points="862,0 944,0 944,50 930,64 848,64 848,14"/>
<polygon class="b-hole" points="858,80 880,80 880,102 870,112 848,112 848,90"/>
<g class="ring">
<circle cx="176" cy="56" r="96"/><circle cx="176" cy="56" r="64"/><circle cx="208" cy="56" r="32"/><circle cx="220" cy="56" r="16"/>
<circle cx="488" cy="56" r="48"/><circle cx="504" cy="56" r="32"/><circle cx="512" cy="56" r="16"/>
<circle cx="728" cy="56" r="64"/><circle cx="752" cy="56" r="32"/>
<circle cx="896" cy="32" r="16"/>
</g>
</svg>
<div class="words">
<p class="name">Cosmic Shore Loop Atlas</p>
<p class="tag">Every loop the game offers, from one input to the long game: what it is now, what is planned, what is proposed.</p>
</div>
</div>
</body>
</html>
'''


def md_table(rows):
    out = ["| " + " | ".join(rows[0]) + " |", "|" + "---|" * len(rows[0])]
    out += ["| " + " | ".join(str(c).replace("|", "/") for c in r) + " |" for r in rows[1:]]
    return "\n".join(out)


def span(t):
    def one(s):
        if s < 1: return f"{s:g} s"
        if s < 90: return f"{s:g} s"
        if s < 5400: return f"{round(s / 60):g} min"
        if s < 86400: return f"{round(s / 360) / 10:g} h"
        if s < 1209600: return f"{round(s / 86400):g} d"
        if s < 5184000: return f"{round(s / 604800):g} wk"
        return f"{round(s / 2592000):g} mo"
    a, b = one(t[0]), one(t[1])
    return a if a == b else f"{a} to {b}"


def holes_on(model, letter):
    """(loop name, hole) for a layer. On the proposed layer a hole of the plan is shown with its fill."""
    out = []
    for n in model["nodes"]:
        if letter not in n.get("in", "npx"):
            continue
        for h in n.get("holes", []):
            if (letter == "x" and "p" in h["on"]) or (letter != "x" and letter in h["on"]):
                out.append((n["name"], h))
    return out


def layer_section(model, letter, title, lede):
    tiers = model["tiers"]
    key = {"n": "now", "p": "plan", "x": "prop"}[letter]
    words = {"signed": "signed off by a person", "built": "built, not signed off", "part": "partly there", "dark": "built, switched off",
             "decided": "decided", "add": "NEW", "change": "changed"}
    lines = [f"# {title}", "", lede, ""]
    for t in range(7, 2, -1):
        mine = [n for n in model["nodes"] if n["tier"] == t and letter in n.get("in", "npx")]
        if not mine:
            continue
        lines += [f"## {tiers[t]['term']} ({span(tiers[t]['span'])})", "", tiers[t]["is"], ""]
        rows = [["Loop", "One pass", "Passes before stepping up", "State", "Note"]]
        for n in mine:
            o = n.get(key) or {}
            reps = f"x{n['reps']}" if n.get("reps") else ("not set" if n.get("parent") else "")
            rows.append([f"**{n['name']}**", span(n["t"]), reps, words.get(o.get("s"), ""), o.get("note", "")])
        lines += [md_table(rows), ""]
    lines += ["## Role, Combo, Verb and Pulse", "",
              "These tiers are per vessel. Open the Loop Atlas card under Components, pick a vessel, and zoom into any Round. "
              "The same data as text: `assets/Model/loop-atlas.json`, key `fleet`.", ""]
    hs = holes_on(model, letter)
    if hs:
        lines += ["## Holes", ""]
        rows = [["Loop", "What is missing"] + (["Proposed"] if letter == "x" else [])]
        for name, h in hs:
            rows.append([name, h["q"]] + ([h.get("fill", "Still open.")] if letter == "x" else []))
        lines += [md_table(rows), ""]
    return "\n".join(lines)


def files(model):
    """Every file of the artifact but its index, as {path under project/: text}."""
    m = model
    out = {}
    js = (SRC / "atlas.js").read_text(encoding="utf-8")
    assert js.count("MODEL_JSON") == 2, "atlas.js names MODEL_JSON once in a comment and once in code"
    head = '/* @ds-bundle: {"format":4,"namespace":"LoopAtlas","components":[{"name":"LoopAtlas"},{"name":"TimescaleLadder"},{"name":"LoopRing"},{"name":"DriverChips"}]} */\n'
    out["components/bundle.js"] = head + js.replace("var M = MODEL_JSON;", "var M = " + js_json(m) + ";")
    out["components/bundle.css"] = (SRC / "atlas.css").read_text(encoding="utf-8")
    out["components/index.d.ts"] = (
        "/** Options for LoopAtlas.mount(host, options). */\n"
        "export interface LoopAtlasProps { layer?: 'now' | 'plan' | 'prop'; view?: 'map' | 'ladder' | 'drivers' | 'holes'; vessel?: string; select?: string; chrome?: boolean }\n"
        "export interface TimescaleLadderProps {}\nexport interface LoopRingProps { layer?: 'now' | 'plan' | 'prop'; vessel?: string; select?: string }\nexport interface DriverChipsProps {}\n"
        "declare global { interface Window { LoopAtlas: { model: object; mount(host: HTMLElement, options?: LoopAtlasProps): object; buildTree(layer: string, vessel: string): object } } }\n")
    out["tokens.json"] = json.dumps(tokens_json(), indent=1, ensure_ascii=False) + "\n"

    out["components/LoopAtlas/preview.html"] = preview(
        "Atlas", 900, "The whole product, zoomable", "Loop Atlas",
        "LoopAtlas.mount(document.getElementById('root'), {});", "#root { height: 100vh; min-height: 640px; }")
    out["components/LoopAtlas/README.md"] = (
        "# LoopAtlas\n\nThe whole product as one zoomable map of loops, on three layers. Use it to place a problem before discussing it: "
        "find the loop, read its tier, and ask that tier's question.\n\n"
        "- **Layer** (Now, Planned, Proposed): what the game has, what the team decided, and the plan with every hole filled by an invention.\n"
        "- **Map**: a ring is a loop. Inside it are the faster loops it is made of. Click a ring to step into it; the up arrow or Backspace steps out. "
        "Diamonds on a ring are its steps; a red diamond with a number counts its holes. Dashed arrows are crossings between places, dotted arrows are what feeds what.\n"
        "- **Vessel**: every Round holds the picked vessel's role and kit. Change the vessel in the bar, or open *The fleet* > *The fleet's kits* and pick one there.\n"
        "- **Timescales**: every loop on one clock, with how many passes it takes before the loop above takes its next step.\n"
        "- **Drivers**: which driver works at which tier, and every hand-off from one loop to another.\n"
        "- **Holes**: what is missing on this layer. On Proposed, each hole has a fill and the twelve proposals are listed.\n\n"
        "The consumer provides nothing: `LoopAtlas.mount(element, {layer, view, vessel, select})`. The model is `LoopAtlas.model`. "
        "Keys: arrows move between sibling loops, Enter steps in, Backspace steps out, + and - zoom.\n")
    out["components/TimescaleLadder/preview.html"] = preview(
        "Vocabulary", 420, "The eight tiers", "Timescale ladder", "LoopAtlas.TimescaleLadder(document.getElementById('root'));")
    out["components/TimescaleLadder/README.md"] = (
        "# TimescaleLadder\n\nThe eight tiers of loop, slowest at the top, each with its span on one clock and the question to ask of a problem at that tier. "
        "Quote a tier by its term (Pulse, Verb, Combo, Role, Round, Session, Arc, Era) in any doc, ticket or review. Draw a tier in its `tier-N` token and nothing else.\n")
    out["components/LoopRing/preview.html"] = preview(
        "Vocabulary", 520, "How one loop is drawn", "Loop ring",
        "LoopAtlas.LoopRing(document.getElementById('root'), { layer: 'plan', select: 'voy.cell' });", "#root { height: 520px; }")
    out["components/LoopRing/README.md"] = (
        "# LoopRing\n\nOne loop, the way every diagram of ours should draw it: a ring, its steps as diamonds clockwise from the top, the faster loops it is made of inside it, "
        "a spoke from a step to the loop that step opens, and the count of passes under it. The card shows a voyage's cell on the Planned layer. "
        "Ring colour is the tier (`tier-N`); `planned` and `proposed` replace it for loops that are not in the game; `selection` marks the picked ring; `hole` is the diamond count. "
        "Do not colour a loop by element: elements have no colour.\n")
    out["components/DriverChips/preview.html"] = preview(
        "Vocabulary", 560, "Drivers and hand-offs", "Drivers", "LoopAtlas.DriverChips(document.getElementById('root'));")
    out["components/DriverChips/README.md"] = (
        "# DriverChips\n\nThe words for why. A **driver** keeps a player inside a loop; ten are inner (the loop is its own reason) and two are outer (the game gives the reason). "
        "A **hand-off** is what moves a player out of one loop and into another. Tag every loop with its drivers and every crossing with its hand-off, using these names exactly.\n")
    out["components/Cover/preview.html"] = cover()

    # the brand book
    tiers = m["tiers"]
    counts = {L: sum(1 for n in m["nodes"] if L in n.get("in", "npx")) for L in "npx"}
    hole_counts = {L: len(holes_on(m, L)) for L in "npx"}
    out["README.md"] = "\n".join([
        "The Loop Atlas is the shared picture of Cosmic Shore as loops inside loops: what a player does in a tenth of a second, in a round, in a sitting, and over months, "
        "and what moves them from one to the next. Use it to say where a problem sits before discussing it, and to check that a plan covers every tier.",
        "",
        "## Read it",
        "",
        "- Open **Components > LoopAtlas**. That card is the atlas. The other cards are its vocabulary.",
        "- Pick a layer. **Now** is the game today. **Planned** is the stabilised version the team has decided on. **Proposed** is the plan with changes and with every hole filled by an invention.",
        "- A ring is a loop. The rings inside it are the faster loops it is made of. Click to step in.",
        "- Pick a vessel. Every Round holds that vessel's role and kit: its four elemental verbs, its omni crystal, its combos and its feel.",
        "",
        "## Say it",
        "",
        "- Name the tier first: " + ", ".join(f"**{t['term']}** ({span(t['span'])})" for t in tiers) + ".",
        "- A loop **steps up** when enough passes are done for the loop above to take its next step, and **steps down** into the loops it is made of. A change between loops at the same tier is a **crossing**.",
        "- A **driver** is why a player stays in a loop. A **hand-off** is what moves them out of it.",
        "- A **hole** is something a layer needs and does not have. On the Proposed layer a hole has a **fill**, which is an invention until someone accepts it.",
        "",
        "## Draw it",
        "",
        "- Draw a loop as a ring in its tier's token (`tier-0` to `tier-7`), with its steps as diamonds clockwise from the top.",
        "- Use `planned` for what the plan has and the game lacks, `proposed` for what is only proposed, `hole` for a hole (always with a count or the word), and `selection` for the picked loop.",
        "- Set names and buttons in `label` (Chakra Petch 600, buttons in capitals) and sentences in `read` (Aldrich). Text is `text` on `ground`, `ground-deep` or `surface`; `text-soft` only on `ground` and `ground-deep`.",
        "- Cut corners with `sliver-small` and `sliver-large`. Nothing is rounded except a loop.",
        "- Never give an element a colour. Mark an element with its name.",
        "",
        "## Use it as data",
        "",
        "- Agents read `assets/Model/loop-atlas.json`: tiers, drivers, hand-offs, every loop with its layers, holes and sources, every edge, every vessel's kit, and the proposals.",
        f"- Today it holds {counts['n']} loops above the kit on Now, {counts['p']} on Planned and {counts['x']} on Proposed, {len(m['fleet'])} vessels, "
        f"{hole_counts['n']} holes on Now and {hole_counts['p']} in the plan.",
        "- It is built from the repository by `Tools/Build/loop_atlas.py`. Change `Docs/LoopAtlas/loop_model.json`, never the files here.",
        ""])

    g = ["# The ladder of words", "", "Eight tiers, from one input to the months-long game. Place a problem on a tier before arguing about it.", ""]
    g += [md_table([["Tier", "One pass", "What it is", "Ask"]] + [[f"**{t['term']}**", span(t["span"]), t["is"], t["ask"]] for t in reversed(tiers)]), ""]
    g += ["## Drivers", "", "Why a player stays inside a loop.", "",
          md_table([["Driver", "Kind", "What it is"]] + [[f"**{d['name']}**", "inner" if d["kind"] == "inner" else "outer", d["is"]] for d in m["drivers"]]), ""]
    g += ["## Hand-offs", "", "What moves a player out of one loop and into another.", "",
          md_table([["Hand-off", "What it is"]] + [[f"**{h['name']}**", h["is"]] for h in m["handoffs"]]), ""]
    g += ["## Other words", "",
          "- **Pass**: one time round a loop.",
          "- **Step**: one bead on a loop's ring.",
          "- **Step up, step down**: a move between tiers. Hierarchy.",
          "- **Crossing**: a move between loops at the same tier, such as Arcade to Arena. Heterarchy.",
          "- **Feed**: one loop's output is another loop's input, such as crystals from a round into the fleet.",
          "- **Place**: the Toybox, the Arcade, the Arena, Voyages.",
          "- **Kit**: a vessel's four elemental verbs, its omni crystal, its combos and its feel. A game mode never changes it.",
          "- **Lean**: where a vessel sits between cultivator and destroyer. Measured from play, never set.", ""]
    out["guide/1-terms.md"] = "\n".join(g)
    out["guide/2-now.md"] = layer_section(m, "n", "Now", "What is in the game today. 'Built' means it exists; only 'signed off' means a person flew it and said yes.")
    out["guide/3-planned.md"] = layer_section(m, "p", "Planned", "The stabilised version the team has decided on. Its holes are what no decision covers yet.")
    px = layer_section(m, "x", "Proposed", "The plan with proposed changes. Every hole of the plan has a fill, and every fill is an invention until the team accepts it.")
    px += "\n## The proposals\n\n" + md_table([["#", "Proposal", "What changes"]] + [[p["id"], f"**{p['title']}**", p["what"]] for p in m["proposals"]]) + "\n"
    out["guide/4-proposed.md"] = px
    out["guide/5-keeping-it-true.md"] = "\n".join([
        "# Keeping it true", "",
        "The atlas is only useful while it matches the game and the plan.", "",
        "- The loops above the kit, the three layers, the holes and the proposals are written by hand in `Docs/LoopAtlas/loop_model.json`.",
        "- The kits come from `Docs/FleetCodex/abilities.json`, and each vessel's games, steps and arenas from `Docs/ElementalLoop/STATUS.md`. Fix those at their source.",
        "- `python3 Tools/Build/loop_atlas.py` validates the model and rebuilds these files; `--check` fails when they are stale.",
        "- When a decision lands: move the loop's `in` from `x` to `px`, or from `px` to `npx`, and move its note to the layer it now belongs to. Delete a hole when it is closed, with the source that closed it.",
        "- A proposal that is accepted becomes part of the plan; one that is thrown out is deleted, not parked.",
        "- Garrett asked for the atlas on 2026-11-18. The Proposed layer is Claude's and nobody has accepted any of it yet.", ""])

    out["assets/Model/loop-atlas.json"] = json.dumps(m, indent=1, ensure_ascii=False) + "\n"
    out["assets/Model/README.md"] = (
        "The atlas as data, for agents and tools. `nodes` are the loops above the kit (`in` says which layers: n now, p planned, x proposed; "
        "`t` is one pass in seconds; `reps` is passes before stepping up; `at` is the step of the parent it opens). `edges` are trains, crossings and feeds. "
        "`fleet` is every vessel's kit, games, board marks and combos. `roles` are the Role loops by kind of Round. Read `tiers` first.\n")
    return out


# ---------------------------------------------------------------- commands
def self_test():
    base = json.loads(MODEL.read_text(encoding="utf-8"))
    assert validate(base) == [], validate(base)

    def broken(mut, needle):
        m = copy.deepcopy(base)
        mut(m)
        errs = validate(m)
        assert any(needle in e for e in errs), f"validator missed: {needle}\n{errs}"

    broken(lambda m: m["nodes"].append(copy.deepcopy(m["nodes"][1])), "duplicate loop id")
    broken(lambda m: m["nodes"][1].update(parent="nowhere"), "does not exist")
    broken(lambda m: m["nodes"][1].update(tier=7, parent="toybox"), "cannot be slower")
    broken(lambda m: m["nodes"][1].update(at=99), "is not a step")
    broken(lambda m: m["nodes"][1].update(drivers=["greed"]), "unknown driver")
    broken(lambda m: m["edges"][0].update(to="nope"), "does not exist")
    broken(lambda m: m["edges"][0].update(kind="likes"), "kind is")
    broken(lambda m: m["nodes"][1].update(t=[9, 1]), "seconds of one pass")
    broken(lambda m: m["nodes"][1]["holes"][0].update(on="x"), "'on' is")
    broken(lambda m: m["proposals"][0].update(touches=["nope"]), "touches")
    assert archetype_of("Hold, release to boost", base["archetypes"]) == "hold_release"
    assert archetype_of("Pick up an omni crystal", base["archetypes"]) == "omni"
    assert archetype_of("Tap to switch mode", base["archetypes"]) == "mode"
    assert archetype_of("Switched off", base["archetypes"]) == "off"
    b, g = read_board("## 1. Vessels\n| Vessel | a |\n|---|\n| Dolphin | `D b b` | `b b .` | `- - -` | `b b .` | - | Regatta, BroodRush |\n"
                      "## 3. games\n| Dolphin | Switchback | The Bends | excused | Rampage |\n")
    assert b["Dolphin"]["marks"]["Time"] == "D b b" and b["Dolphin"]["arenas"] == ["Regatta", "Brood Rush"], b
    assert g["Dolphin"]["Time"] == ["Switchback"] and g["Dolphin"]["_excused"] == ["Mass"], g
    print("loop_atlas self-test: ok (10 negative controls, 4 archetypes, the board reader)")


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--stats", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        self_test()
        return 0
    raw = json.loads(MODEL.read_text(encoding="utf-8"))
    model = resolve(raw)
    errs = validate(raw, model["fleet"])
    if errs:
        print("loop_atlas: the model is invalid:\n  " + "\n  ".join(errs))
        return 1
    if a.stats:
        for L, name in (("n", "Now"), ("p", "Planned"), ("x", "Proposed")):
            hs = holes_on(raw, L)
            print(f"{name}: {sum(1 for n in raw['nodes'] if L in n.get('in', 'npx'))} loops above the kit, "
                  f"{sum(1 for e in raw['edges'] if L in e.get('in', 'npx'))} edges, {len(hs)} holes"
                  + (f", {sum(1 for _, h in hs if not h.get('fill'))} without a fill" if L == "x" else ""))
        print(f"{len(model['fleet'])} vessels, {sum(len(v['verbs']) for v in model['fleet'].values())} verbs, "
              f"{sum(len(v['combos']) for v in model['fleet'].values())} combos, {len(raw['proposals'])} proposals")
        return 0
    want = files(model)
    if a.check:
        stale = [p for p, text in want.items() if not (OUT / p).exists() or (OUT / p).read_text(encoding="utf-8") != text]
        if stale:
            print("loop_atlas --check: stale, run python3 Tools/Build/loop_atlas.py and republish:\n  " + "\n  ".join(stale))
            return 1
        print(f"loop_atlas --check: ok ({len(want)} files)")
        return 0
    for p, text in want.items():
        f = OUT / p
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_text(text, encoding="utf-8")
    print(f"loop_atlas: wrote {len(want)} files under {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
