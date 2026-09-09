#!/usr/bin/env python3
"""
element_ability_table.py — the fleet's element -> ability -> level-5-upgrade table,
read from the SHIPPED ASSETS AND CODE rather than from any document.

    python3 Tools/Build/element_ability_table.py                  # whole fleet
    python3 Tools/Build/element_ability_table.py Dolphin Sparrow  # named vessels
    python3 Tools/Build/element_ability_table.py --element Space  # one column, fleet-wide
    python3 Tools/Build/element_ability_table.py --gaps           # only the rows that disagree
    python3 Tools/Build/element_ability_table.py --json           # machine-readable

WHY IT READS THREE SOURCES AND NOT ONE
--------------------------------------
An element's ability is authored in one place and IMPLEMENTED in two others, and the
three drift:

  1. Assets/Resources/ElementalAbilityMaps/{Vessel}.asset
       the DECLARATION: ability name, input, the generic MultiplierAtFullLevel, the
       unlock level / latch policy, and the level-5 upgrade's name and prose.
       `/vessel` SKILL.md 2: this asset is a record of intent -- an `UpgradeLabel` is
       documentation until something gates on it.

  2. `IsUpgradeActive(Element.X)` call sites
       the LEVEL-5 UPGRADE, actually. This is the replicated NetElementUnlocks bit, and
       it is the only thing that makes an upgrade real. A map row with an UpgradeLabel and
       no reachable gate is prose.

  3. `handler.Multiplier(Element.X)` and `ElementalScaling.*` call sites
       the SCALING, actually. Two channels: the GENERIC one (the map's own
       MultiplierAtFullLevel, read back through the handler) and BESPOKE authored
       endpoints on an action/effect SO (`...AtFullSpace`, `...AtRestCharge`, the
       round-growth pair). CONTRACT 4.2 forbids double-dipping, so a vessel using the
       bespoke channel pins its map multiplier to 1 -- which makes a map multiplier of 1
       ambiguous on its face (deliberately inert, or never wired?) and is exactly what
       this tool disambiguates.

Sources 2 and 3 live in C# that a given vessel may or may not REACH, so the join is a
reference walk from the vessel prefab through its wired action SOs, executors, and
impact-effect containers -- never a naming convention. Where a site takes its element or
its endpoints from a serialized field, the value is read from the asset instance the walk
actually arrived at, so the numbers reported are that vessel's numbers.

This is a READER (`Docs/TOOLING.md`): it writes nothing, records no ledger entry, and
needs no ship contract. It never opens Unity.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from collections import defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
MAP_DIR = os.path.join(ASSETS, "Resources", "ElementalAbilityMaps")
VESSEL_PREFABS = os.path.join(ASSETS, "_Prefabs", "Spacevessels")
SCRIPTS = os.path.join(ASSETS, "_Scripts")

ELEMENTS = {1: "Charge", 2: "Mass", 3: "Space", 4: "Time"}
ELEMENT_ORDER = ["Charge", "Mass", "Space", "Time"]  # the HUD row's order
ELEMENT_BY_NAME = {v: k for k, v in ELEMENTS.items()}

# InputEvents -> (pad control, keyboard control). Mirrors InputHintBindingMap's table
# (Assets/_Scripts/UI/Elements/InputHintBindingMap.cs) -- the same derivation the ability
# lockup draws its control chip from. A blank is honest: a passive ability has no button,
# and several pad buttons have no keyboard twin.
INPUT_EVENTS = {
    0: ("FullSpeedStraightAction", "", ""),
    1: ("RightStickAction", "RT", "R-Shift"),
    2: ("LeftStickAction", "LT", "L-Shift"),
    3: ("FlipAction", "RB", ""),
    4: ("IdleAction", "", ""),
    5: ("MinimumSpeedStraightAction", "", ""),
    6: ("Button1Action", "A", "Space"),
    7: ("Button2Action", "B", "R"),
    8: ("Button3Action", "X", "Q"),
    9: ("NodeTapAction", "", ""),
    10: ("SelfTapAction", "", ""),
    11: ("OnlyRightStickAction", "RT", "R-Shift"),
    12: ("OnlyLeftStickAction", "LT", "L-Shift"),
    13: ("BothSticksAction", "LT+RT", "Both shifts"),
}

GUID_RE = re.compile(r"guid:\s*([0-9a-f]{32})")
DOC_SPLIT_RE = re.compile(r"^--- !u!\d+ &\d+.*$", re.M)


# ───────────────────────── source scanning ─────────────────────────

def blank_comments(src: str) -> str:
    """Replace comment bodies with spaces, preserving every offset and newline, so a
    regex match's offset still maps to the real line number."""
    out = list(src)
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == '"' or c == "'":
            q, i = c, i + 1
            while i < n and src[i] != q:
                i += 2 if src[i] == "\\" else 1
            i += 1
        elif c == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != "\n":
                out[i] = " "
                i += 1
        elif c == "/" and i + 1 < n and src[i + 1] == "*":
            while i < n and not (src[i] == "*" and i + 1 < n and src[i + 1] == "/"):
                if src[i] != "\n":
                    out[i] = " "
                i += 1
            for j in range(i, min(i + 2, n)):
                out[j] = " "
            i += 2
        else:
            i += 1
    return "".join(out)


def args_at(src: str, open_paren: int):
    """Split the top-level, comma-separated arguments of the call whose '(' is at
    open_paren. Returns (list_of_arg_strings, index_after_close)."""
    depth, i, n = 0, open_paren, len(src)
    start, args = open_paren + 1, []
    while i < n:
        c = src[i]
        if c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
            if depth == 0:
                args.append(src[start:i])
                return [a.strip() for a in args], i + 1
        elif c == "," and depth == 1:
            args.append(src[start:i])
            start = i + 1
        i += 1
    return [], n


CALL_RES = {
    # kind          regex over comment-blanked source (matches up to and incl. the '(')
    "upgrade":  re.compile(r"IsUpgradeActive\s*\("),
    "map":      re.compile(r"\.Multiplier\s*\(\s*Element\.(\w+)\s*\)"),
    "scale":    re.compile(r"ElementalScaling\.(Multiplier|MultiplierFromRest|Scale)\s*\("),
    "growth":   re.compile(r"ElementalScaling\.(RoundGrowthFactor|RoundGrowthFactorForLevel)\s*\("),
    "raw":      re.compile(r"ElementalScaling\.(Level01|MeetsQualitativeThreshold)\s*\("),
    # A FOURTH channel: a bespoke lerp fed by a direct level read, with its endpoints
    # declared beside it rather than passed at the call (UrchinSlipActionSO's ghost
    # seconds). Reported only when the file declares an AtRest/AtFull pair NAMED for the
    # element -- which is what keeps the HUD's and the comeback system's level reads,
    # which scale nothing, out of the table.
    "level":    re.compile(r"\.(GetLevel|GetNormalizedLevel)\s*\(\s*Element\.(\w+)\s*\)"),
}

PROP_RE = re.compile(r"public\s+(?:static\s+)?float\s+(\w+)\s*=>\s*([\w.]+)\s*;")

# Field scanning is LINE-BASED on purpose. The obvious single regex --
# `(?:\[[^\]]*\]\s*)*(?:public|private|...)?\s*float\s+(\w+)\s*=\s*(-?[0-9.]+)f?;`
# -- is all-optional at its head and backtracks catastrophically: 15.2s over 151 files,
# three quarters of the tool's entire runtime, for a job that is one pass over lines.
FLOAT_LINE_RE = re.compile(r"\bfloat\s+(\w+)\s*=\s*(-?[0-9.]+)f?\s*;")
BOOL_LINE_RE = re.compile(r"\bbool\s+(\w+)\s*[=;]")
ELEM_LINE_RE = re.compile(r"\bElement\s+(\w+)\s*=\s*Element\.(\w+)\s*;")
DECL_HEAD_RE = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal|static|readonly|const|\s)*")


def scan_fields(src: str):
    """{float field: default}, {Element field: name}, {serialized bool field} — one pass."""
    floats, elems, bools = {}, {}, set()
    lines = src.split("\n")
    for i, line in enumerate(lines):
        if "float " in line:
            m = FLOAT_LINE_RE.search(line)
            if m and DECL_HEAD_RE.match(line).end() >= line.index("float"):
                floats.setdefault(m.group(1), float(m.group(2)))
        if "Element " in line:
            m = ELEM_LINE_RE.search(line)
            if m:
                elems.setdefault(m.group(1), m.group(2))
        if "bool " in line:
            m = BOOL_LINE_RE.search(line)
            # [SerializeField] may sit on this line or up to two above it (a Tooltip in
            # between is the house style).
            window = "\n".join(lines[max(0, i - 3):i + 1])
            if m and "SerializeField" in window:
                bools.add(m.group(1))
    return floats, elems, bools


def scan_symbols(prescanned):
    """Project-wide {property -> backing field} and {field -> code default}.

    An endpoint is regularly authored on a DIFFERENT script from the one that reads it
    (`DeployTeamCrystalActionExecutor` reads `so.CooldownMultiplierAtFullMass`, declared on
    `DeployTeamCrystalActionSO`), and that SO carries no elemental site of its own -- so
    resolving the name inside the reading file alone finds nothing and the tool reports a
    scaling site with no numbers. The lookup has to span the project."""
    props, floats = {}, {}
    for src in prescanned:
        for m in PROP_RE.finditer(src):
            props.setdefault(m.group(1), m.group(2).split(".")[-1])
        for field, value in scan_fields(src)[0].items():
            floats.setdefault(field, value)
    return props, floats


def scan_scripts():
    """(site index, blanked sources that declare an elemental endpoint).

    One walk over _Scripts serves both: the per-file site index, and the project-wide
    symbol table an endpoint authored on another script needs."""
    index, endpoint_sources = {}, []
    for dirpath, _dirs, files in os.walk(SCRIPTS):
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(dirpath, name)
            try:
                raw = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            # The prefilter is part of the contract, not an optimisation: a file it drops
            # is invisible to every later stage. `UrchinSlipActionSO` names none of the
            # first three and scales the whole Slip ability through the fourth.

            has_endpoint = "AtFull" in raw or "AtRest" in raw or "Multiplier" in raw
            if not has_endpoint and not any(
                    k in raw for k in ("IsUpgradeActive", "ElementalScaling",
                                       ".Multiplier(Element.", "GetLevel(Element.",
                                       "GetNormalizedLevel(Element.")):
                continue
            src = blank_comments(raw)
            if has_endpoint:
                endpoint_sources.append(src)
            line_of = _line_indexer(src)
            sites = []
            for kind, rx in CALL_RES.items():
                for m in rx.finditer(src):
                    if kind == "map":
                        sites.append(dict(kind="map", element=m.group(1),
                                          element_field=None, args=[],
                                          line=line_of(m.start()), fn=None,
                                          near=_nearby(src, m.start())))
                        continue
                    if kind == "level":
                        sites.append(dict(kind="level", element=m.group(2),
                                          element_field=None, args=[],
                                          line=line_of(m.start()), fn=m.group(1),
                                          near=_nearby(src, m.start())))
                        continue
                    open_paren = src.index("(", m.start())
                    args, _ = args_at(src, open_paren)
                    fn = m.group(1) if m.lastindex else None
                    elem, elem_field = _element_from_args(kind, args, fn)
                    sites.append(dict(kind=kind, element=elem, element_field=elem_field,
                                      args=args, line=line_of(m.start()), fn=fn,
                                      near=_nearby(src, m.start())))
            if not sites:
                continue
            floats, elems, bools = scan_fields(src)
            index[path] = dict(
                sites=sites,
                props={m.group(1): m.group(2).split(".")[-1] for m in PROP_RE.finditer(src)},
                floats=floats, elems=elems, bools=bools, src=src,
            )
    return index, endpoint_sources


def _nearby(src: str, offset: int, back=400):
    """Identifiers in the innermost parenthesised group the site sits inside -- enough to
    spot a serialized bool that switches the branch off for a particular vessel.

    Scoped to the enclosing group rather than to a window of preceding characters, because
    a DISJUNCT is not a guard: `if (shielded || (massUpgradeShieldsTrail && ...Active(...)))`
    puts two serialized bools in view and only the conjoined one gates the call. A window
    picked up `shielded: 0` and declared the Squirrel's Heavy Trail dead while its real
    gate was authored ON."""
    depth, i, start = 0, offset, max(0, offset - back)
    while i > start:
        c = src[i]
        if c == ")":
            depth += 1
        elif c == "(":
            if depth == 0:
                break
            depth -= 1
        i -= 1
    return set(re.findall(r"[_A-Za-z]\w*", src[i:offset]))


def _line_indexer(src: str):
    starts = [0]
    for i, c in enumerate(src):
        if c == "\n":
            starts.append(i + 1)
    import bisect
    return lambda off: bisect.bisect_right(starts, off)


def _element_from_args(kind, args, fn):
    """(literal element name, serialized-field name) — exactly one is set, or neither."""
    if kind == "growth":
        return "Mass", None            # the round-growth curve is Mass by construction
    for a in args:
        m = re.fullmatch(r"Element\.(\w+)", a)
        if m and m.group(1) in ELEMENT_BY_NAME:
            return m.group(1), None
    if kind == "upgrade" and len(args) == 1:
        a = args[0].split(".")[-1]
        if re.fullmatch(r"[_A-Za-z]\w*", a):
            return None, a             # element comes from a serialized field
    return None, None


# ───────────────────────── asset graph ─────────────────────────

def build_guid_index():
    guid_to_path = {}
    for dirpath, _dirs, files in os.walk(ASSETS):
        for name in files:
            if not name.endswith(".meta") or not name.endswith(
                    (".cs.meta", ".asset.meta", ".prefab.meta")):
                continue
            meta = os.path.join(dirpath, name)
            try:
                with open(meta, encoding="utf-8", errors="replace") as fh:
                    for line in fh:
                        if line.startswith("guid:"):
                            guid_to_path[line.split()[1].strip()] = meta[:-5]
                            break
            except OSError:
                pass
    return guid_to_path


def documents(text: str):
    """Split a Unity YAML file into per-object documents (script guid, body)."""
    parts = DOC_SPLIT_RE.split(text)
    for body in parts:
        m = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})", body)
        yield (m.group(1) if m else None), body


def walk_from(prefab_path, guid_to_path, script_index, max_depth=3):
    """Every (indexed script .cs or None, owning asset path, document body) the vessel
    prefab reaches, through its wired action SOs, its executors and vessel-root components,
    and its impact-effect containers -- a reference walk, never a naming convention."""
    seen, found = set(), []
    queue = [(prefab_path, 0)]
    while queue:
        path, depth = queue.pop(0)
        if path in seen or not os.path.isfile(path):
            continue
        seen.add(path)
        try:
            text = open(path, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        for script_guid, body in documents(text):
            cs = guid_to_path.get(script_guid) if script_guid else None
            found.append((cs, path, body))
            if depth >= max_depth:
                continue
            for g in set(GUID_RE.findall(body)):
                if g == script_guid:
                    continue
                target = guid_to_path.get(g)
                if target and target.endswith((".asset", ".prefab")) and target not in seen:
                    queue.append((target, depth + 1))
    return found


ELEMENTAL_FLOAT_RE = re.compile(
    r"^([ \t]*)(\w+):[ \t]*\n"
    r"\1[ \t]+Enabled:[ \t]*(\d+)[ \t]*\n"
    r"\1[ \t]+Value:[ \t]*(-?[\d.]+)[ \t]*\n"
    r"\1[ \t]+Min:[ \t]*(-?[\d.]+)[ \t]*\n"
    r"\1[ \t]+Max:[ \t]*(-?[\d.]+)[ \t]*\n"
    r"\1[ \t]+element:[ \t]*(\d+)[ \t]*$", re.M)


def elemental_floats(body: str, owner: str):
    """The THIRD scaling channel: `ElementalFloat` (Assets/_Scripts/Controller/Vessel).

    It carries no call site at all -- element, Min and Max are serialized on the asset and
    `EvaluateLive` lerps Min->Max over level/10 -- so a tool that indexed only C# call sites
    reports `NO SCALING` for an element that demonstrably scales. The Squirrel's Mass slot is
    exactly that: `trailVolume` 1 -> 2.5 on element 2, authored and enabled on the prefab,
    with nothing in code naming Mass."""
    out = []
    for m in ELEMENTAL_FLOAT_RE.finditer(body):
        _indent, field, enabled, _value, lo, hi, element = m.groups()
        name = ELEMENTS.get(int(element))
        if not name:
            continue
        out.append({
            "kind": "elemental-float", "element": name, "field": field,
            "enabled": enabled == "1", "min": float(lo), "max": float(hi),
            "site": f"{field} (ElementalFloat)", "script": "",
            "asset": os.path.relpath(owner, ROOT), "call": "EvaluateLive",
            "endpoints": [], "disabled_by": None if enabled == "1" else "Enabled",
        })
    return out


def follow_static_calls(reached, script_index):
    """One more hop: a STATIC call from a reached script into another indexed one.

    Reachability through serialized references misses a static class outright, because
    nothing ever holds a reference to it. `ScarabBallForge` is referenced by no prefab and
    no asset in the project -- it is reached only as `ScarabBallForge.<member>` from an
    effect SO that IS wired -- so a walk that stopped at references reported the Scarab's
    Space multiplier as authored-but-never-read when it is read on every ball forged."""
    names = {os.path.splitext(os.path.basename(cs))[0]: cs for cs in script_index}
    out, seen = list(reached), {cs for cs, _o, _b in reached if cs}
    frontier = [(cs, owner, body) for cs, owner, body in reached if cs]
    while frontier:
        nxt = []
        for cs, owner, body in frontier:
            info = script_index.get(cs)
            if info is not None:
                src = info["src"]
            else:
                try:
                    src = open(cs, encoding="utf-8", errors="replace").read()
                except OSError:
                    continue
            for name in set(re.findall(r"\b([A-Z]\w+)\s*\.", src)):
                target = names.get(name)
                if target and target not in seen and target != cs:
                    seen.add(target)
                    entry = (target, owner, body)
                    out.append(entry)
                    nxt.append(entry)
        frontier = nxt
    return out


def yaml_value(body: str, field: str):
    m = re.search(rf"^\s*{re.escape(field)}:\s*(-?[\d.]+)\s*$", body, re.M)
    return float(m.group(1)) if m else None


# ───────────────────────── the map asset ─────────────────────────

def parse_map(path):
    text = open(path, encoding="utf-8", errors="replace").read()
    vessel = os.path.splitext(os.path.basename(path))[0]
    entries, cur = [], None
    for line in text.splitlines():
        m = re.match(r"\s*-\s+Element:\s*(\d+)", line)
        if m:
            cur = {"element": ELEMENTS.get(int(m.group(1)), f"?{m.group(1)}")}
            entries.append(cur)
            continue
        if cur is None:
            continue
        m = re.match(r"\s{4}(\w+):\s*(.*)$", line)
        if m:
            cur[m.group(1)] = m.group(2).strip()
            cur["_last"] = m.group(1)
        elif re.match(r"\s{6}\S", line) and cur.get("_last"):
            cur[cur["_last"]] = (cur[cur["_last"]] + " " + line.strip()).strip()
    for e in entries:
        e.pop("_last", None)
    return vessel, entries


def fnum(entry, key, default):
    try:
        return float(entry.get(key, default))
    except (TypeError, ValueError):
        return default


# ───────────────────────── the join ─────────────────────────

def rows_for_vessel(vessel, map_path, guid_to_path, script_index, symbols, max_depth):
    _v, entries = parse_map(map_path)
    prefab = os.path.join(VESSEL_PREFABS, f"{vessel}.prefab")
    reached = walk_from(prefab, guid_to_path, script_index, max_depth) \
        if os.path.isfile(prefab) else []
    # Every document the walk touched, for resolving an endpoint authored on an asset OTHER
    # than the one whose script reads it.
    reached = follow_static_calls(reached, script_index)
    bodies = [(owner, body) for _cs, owner, body in reached]

    per_element = defaultdict(lambda: {"upgrade": [], "map": [], "scale": []})
    for _cs, owner, body in reached:
        for ef in elemental_floats(body, owner):
            per_element[ef["element"]]["scale"].append(ef)
    for cs, owner, body in reached:
        info = script_index.get(cs) if cs else None
        if info is None:
            continue
        for site in info["sites"]:
            element = site["element"]
            if site["element_field"]:
                element = resolve_element_field(site["element_field"], info, body)
            if element not in ELEMENT_BY_NAME:
                continue
            bucket = {"upgrade": "upgrade", "map": "map",
                      "scale": "scale", "growth": "scale", "raw": "scale",
                      "level": "scale"}[site["kind"]]
            described = describe_site(site, cs, owner, info, body, bodies, symbols)
            if described is not None:
                per_element[element][bucket].append(described)

    rows = []
    for entry in entries:
        element = entry.get("element", "?")
        hits = per_element.get(element, {"upgrade": [], "map": [], "scale": []})
        rows.append(build_row(vessel, entry, hits))
    return rows


def resolve_element_field(field, info, body):
    v = None
    m = re.search(rf"^\s*{re.escape(field)}:\s*(\d+)\s*$", body, re.M)
    if m:
        v = ELEMENTS.get(int(m.group(1)))
    return v or info["elems"].get(field)


def find_authored(field, own_body, bodies):
    """(value, source-asset) for a serialized field: the site's own document first, then
    anything else the vessel reaches."""
    v = yaml_value(own_body, field)
    if v is not None:
        return v, None
    for owner, body in bodies:
        v = yaml_value(body, field)
        if v is not None:
            return v, owner
    return None, None


def guard_state(site, info, own_body):
    """A serialized bool in the same condition, authored FALSE on this vessel, means the
    branch is dead here even though the code is reachable -- the Dolphin reaches the
    Squirrel's Heavy Trail gate with `massUpgradeShieldsTrail: 0`."""
    for name in site.get("near", ()):
        if name not in info["bools"]:
            continue
        m = re.search(rf"^\s*{re.escape(name)}:\s*(\d+)\s*$", own_body, re.M)
        if m and m.group(1) == "0":
            return name
    return None


def describe_site(site, cs, owner, info, body, bodies, symbols):
    out = {
        "kind": site["kind"],
        "site": f"{os.path.basename(cs)}:{site['line']}",
        "script": os.path.relpath(cs, ROOT),
        "asset": os.path.relpath(owner, ROOT),
        "call": site["fn"] or "IsUpgradeActive",
        "endpoints": [],
        "disabled_by": guard_state(site, info, body),
    }
    if site["kind"] in ("level", "raw"):
        # Endpoints are declared beside the read, not passed to it: take the file's own
        # float fields whose names END in this element, which is the convention the
        # bespoke-endpoint channel already follows (`ghostSecondsAtFullTime`).
        el = site["element"]
        for field in info["floats"]:
            if not re.search(rf"At(?:Rest|Resting|Full){el}$", field):
                continue
            value, elsewhere = find_authored(field, body, bodies)
            src = "asset"
            if value is None:
                value, src = info["floats"][field], "code default"
            out["endpoints"].append({"field": field, "value": value, "from": src,
                                     "authored_on": os.path.relpath(elsewhere, ROOT)
                                     if elsewhere else out["asset"]})
        if not out["endpoints"] and site["kind"] == "level":
            return None          # a level read that scales nothing (HUD, comeback system)
    if site["kind"] in ("scale", "growth"):
        props, floats = symbols
        for arg in site["args"]:
            token = arg.split(".")[-1].strip()
            if not re.fullmatch(r"[_A-Za-z]\w*", token) or token in ("status", "_status"):
                continue
            if re.fullmatch(r"Element\.\w+", arg):
                continue
            field = info["props"].get(token) or props.get(token, token)
            value, elsewhere = find_authored(field, body, bodies)
            src = "asset"
            if value is None:
                value, src = info["floats"].get(field, floats.get(field)), "code default"
            if value is None:
                continue
            out["endpoints"].append({"field": field, "value": value, "from": src,
                                     "authored_on": os.path.relpath(elsewhere, ROOT)
                                     if elsewhere else out["asset"]})
    return out


def build_row(vessel, entry, hits):
    element = entry.get("element", "?")
    at_full = fnum(entry, "MultiplierAtFullLevel", 1.0)
    input_id = int(fnum(entry, "Input", 0))
    label = entry.get("AbilityLabel", "").strip()
    upgrade_label = entry.get("UpgradeLabel", "").strip()
    designed = bool(label) and "(open design slot)" not in label

    map_live = any(not h["disabled_by"] for h in hits["map"])
    scale_sites = dedupe(hits["scale"], by_endpoints=True)
    live_scale = [x for x in scale_sites if not x["disabled_by"] and not is_inert(x)]
    upgrade_sites = dedupe(hits["upgrade"])

    notes = []
    if not designed:
        notes.append("OPEN DESIGN SLOT — nothing authored")
    else:
        live_gates = [g for g in upgrade_sites if not g["disabled_by"]]
        if upgrade_label and not live_gates:
            notes.append("UPGRADE IS PROSE — no live IsUpgradeActive gate"
                         + (" (every gate is authored off on this hull)"
                            if upgrade_sites else ""))
        if not upgrade_label and live_gates:
            notes.append("gate exists but the map names no upgrade")
        if abs(at_full - 1.0) > 1e-6 and not map_live:
            notes.append(f"DEAD MAP MULTIPLIER — x{at_full:g} authored, never read")
        if abs(at_full - 1.0) <= 1e-6 and map_live and not live_scale:
            notes.append("map multiplier read but pinned to x1 — scaling is inert")
        if not live_scale and not (map_live and abs(at_full - 1.0) > 1e-6):
            notes.append("NO SCALING — this element changes no number")
        if abs(at_full - 1.0) > 1e-6 and live_scale and map_live:
            notes.append("TWO LIVE SCALING CHANNELS — a genuine double-dip if the map "
                         "multiplier and the bespoke endpoint drive the SAME parameter")

    return {
        "vessel": vessel,
        "element": element,
        "ability": label,
        "description": entry.get("AbilityDescription", "").strip(),
        "input": INPUT_EVENTS.get(input_id, (f"?{input_id}", "", ""))[0],
        "pad": INPUT_EVENTS.get(input_id, ("", "", ""))[1],
        "key": INPUT_EVENTS.get(input_id, ("", "", ""))[2],
        "map_multiplier_at_full": at_full,
        "map_min_multiplier": fnum(entry, "MinMultiplier", 0.25),
        "map_multiplier_is_read": map_live,
        "map_multiplier_read_at": [h["site"] for h in dedupe(hits["map"])],
        "unlock_level": int(fnum(entry, "UnlockLevel", 5)),
        "relock_below": int(fnum(entry, "RelockBelowLevel", 4)),
        "latch": "LatchForLife" if fnum(entry, "LatchPolicy", 0) == 1 else "Relock",
        "upgrade": upgrade_label,
        "upgrade_description": entry.get("UpgradeDescription", "").strip(),
        "upgrade_gates": upgrade_sites,
        "scaling": scale_sites,
        "notes": notes,
    }


def dedupe(sites, by_endpoints=False):
    """Collapse repeats. With by_endpoints, several call sites reading ONE authored pair
    collapse into one row that lists them all -- the Dolphin's blast reads
    `_heightMultiplierAtFullSpace` at three call sites and that is one number, not three."""
    seen, out = {}, []
    for s in sites:
        key = ((s["asset"], s["kind"], s.get("field", ""),
                tuple(sorted(e["field"] for e in s["endpoints"])))
               if by_endpoints else
               (s["site"], tuple(sorted(e["field"] for e in s["endpoints"]))))
        if key in seen:
            seen[key].setdefault("also", []).append(s["site"])
            continue
        seen[key] = s
        out.append(s)
    return out


# ───────────────────────── report ─────────────────────────

W = 96


def wrap(text, indent, width=W):
    words, line, out = text.split(), "", []
    for w in words:
        if len(line) + len(w) + 1 > width - len(indent):
            out.append(indent + line)
            line = w
        else:
            line = f"{line} {w}".strip()
    if line:
        out.append(indent + line)
    return out


def endpoint_pair(site):
    """(at-rest, at-full) of a scaling site, ignoring the min-multiplier FLOOR that rides
    along in the same argument list. The floor is a guard against inversion, never a
    statement about how the element scales, so folding it into the reading makes an
    authored x1 -> x1 (the author saying `this element does not scale me`) look live."""
    eps = site["endpoints"]
    at_rest = next((e for e in eps
                    if "AtRest" in e["field"] or "AtResting" in e["field"]), None)
    at_full = next((e for e in eps if "AtFull" in e["field"]), None)
    return at_rest, at_full


def is_inert(site):
    if site["kind"] == "elemental-float":
        return abs(site["max"] - site["min"]) < 1e-6
    at_rest, at_full = endpoint_pair(site)
    if at_full is None:
        return bool(site["endpoints"]) and all(
            abs(e["value"] - 1.0) < 1e-6 for e in site["endpoints"])
    return (abs(at_full["value"] - 1.0) < 1e-6
            and (at_rest is None or abs(at_rest["value"] - 1.0) < 1e-6))


def endpoint_phrase(site):
    eps = site["endpoints"]
    if not eps and site["kind"] != "elemental-float":
        return None
    if site["kind"] == "elemental-float":
        return f"{site['min']:g} -> {site['max']:g} across L0..L10"
    at_rest, at_full = endpoint_pair(site)
    if at_full and at_rest:
        unit = "" if site["kind"] != "level" else " (absolute, not a multiplier)"
        pre = "" if site["kind"] == "level" else "x"
        return (f"{pre}{at_rest['value']:g} at rest -> "
                f"{pre}{at_full['value']:g} at L10{unit}")
    if at_full:
        return f"x1 at rest -> x{at_full['value']:g} at L10"
    return ", ".join(f"{e['field']}={e['value']:g}" for e in eps)


def print_row(row, verbose):
    pad, key = row["pad"], row["key"]
    control = f"{pad} / {key}" if pad and key else (pad or "passive")
    head = f"  {row['element'].upper():<7} {row['ability'] or '(open design slot)':<34} {control}"
    print(head)

    if not row["ability"] or "(open design slot)" in row["ability"]:
        for n in row["notes"]:
            print(f"          ! {n}")
        return

    # scaling
    shown = [s for s in row["scaling"]
             if verbose or not (s["kind"] == "elemental-float" and s["disabled_by"])]
    if shown:
        for s in shown:
            phrase = (endpoint_phrase(s)
                      or f"drives a value off the raw {row['element']} level "
                         f"(no endpoint pair named for the element)")
            off = (f"   [OFF: {s['disabled_by']} = 0]" if s["disabled_by"]
                   else ("   (inert - authored x1)" if is_inert(s) else ""))
            print(f"          scale   {phrase}{off}")
            where = ", ".join([s["site"]] + s.get("also", []))
            src = s["endpoints"][0]["authored_on"] if s["endpoints"] else s["asset"]
            if s["kind"] == "elemental-float":
                print(f"                  {s['site']}  <- {os.path.basename(s['asset'])}")
                continue
            print(f"                  {where}  <- {os.path.basename(src)}")
    if row["map_multiplier_is_read"]:
        mm = row["map_multiplier_at_full"]
        tag = "" if abs(mm - 1) > 1e-6 else "   (inert - pinned to 1)"
        print(f"          scale   map x{mm:g} at L10, floor x{row['map_min_multiplier']:g}{tag}")
        for site in row["map_multiplier_read_at"][:3]:
            print(f"                  read at {site}")
    elif not shown:
        print(f"          scale   none reachable  (map authors x{row['map_multiplier_at_full']:g})")

    # upgrade
    lvl = row["unlock_level"]
    latch = "" if row["latch"] == "Relock" else f", {row['latch']}"
    if row["upgrade"]:
        print(f"          L{lvl}      {row['upgrade']}   "
              f"(relock < {row['relock_below']}{latch})")
    else:
        print(f"          L{lvl}      -")
    for g in row["upgrade_gates"]:
        off = f"   [OFF: {g['disabled_by']} = 0]" if g["disabled_by"] else ""
        print(f"                  gated at {g['site']}  <- "
              f"{os.path.basename(g['asset'])}{off}")
    if not row["upgrade_gates"] and row["upgrade"]:
        print("                  gated at (nothing)")
    if verbose and row["upgrade_description"]:
        for line in wrap(row["upgrade_description"], " " * 18):
            print(line)
    if verbose and row["description"]:
        print("          what    ", end="")
        lines = wrap(row["description"], " " * 18)
        print(lines[0].strip())
        for line in lines[1:]:
            print(line)

    for n in row["notes"]:
        lines = wrap(n, " " * 18)
        print("          !       " + lines[0].strip())
        for line in lines[1:]:
            print(line)


def main():
    ap = argparse.ArgumentParser(
        description="The fleet's element -> ability -> L5-upgrade table, read from assets + code.")
    ap.add_argument("vessels", nargs="*", help="vessel names (default: every map asset)")
    ap.add_argument("--element", "-e", help="only this element (Charge/Mass/Space/Time)")
    ap.add_argument("--gaps", action="store_true",
                    help="only rows whose declaration and wiring disagree")
    ap.add_argument("--verbose", "-v", action="store_true",
                    help="include the authored ability and upgrade prose")
    ap.add_argument("--json", action="store_true", help="machine-readable")
    ap.add_argument("--depth", type=int, default=3,
                    help="reference-walk depth from the vessel prefab (default 3)")
    args = ap.parse_args()

    if not os.path.isdir(MAP_DIR):
        sys.exit(f"no map folder at {MAP_DIR}")

    available = sorted(f[:-6] for f in os.listdir(MAP_DIR) if f.endswith(".asset"))
    if args.vessels:
        wanted, unknown = [], []
        for v in args.vessels:
            match = next((a for a in available if a.lower() == v.lower()), None)
            (wanted if match else unknown).append(match or v)
        if unknown:
            sys.exit(f"no ability map for: {', '.join(unknown)}\nhave: {', '.join(available)}")
    else:
        wanted = available

    script_index, endpoint_sources = scan_scripts()
    symbols = scan_symbols(endpoint_sources)
    guid_to_path = build_guid_index()

    all_rows, by_vessel_all = [], {}
    for vessel in wanted:
        rows = rows_for_vessel(vessel, os.path.join(MAP_DIR, f"{vessel}.asset"),
                               guid_to_path, script_index, symbols, args.depth)
        order = {e: i for i, e in enumerate(ELEMENT_ORDER)}
        rows.sort(key=lambda r: order.get(r["element"], 9))
        by_vessel_all[vessel] = rows          # the counts describe the VESSEL, never the filter
        all_rows.extend(rows)

    if args.element:
        el = args.element.capitalize()
        all_rows = [r for r in all_rows if r["element"] == el]
    if args.gaps:
        all_rows = [r for r in all_rows if r["notes"]]

    if args.json:
        json.dump(all_rows, sys.stdout, indent=2)
        print()
        return

    print("=" * W)
    print("ELEMENT ABILITY TABLE".center(W))
    print("declared in Resources/ElementalAbilityMaps -- wired in the code the prefab reaches"
          .center(W))
    print("=" * W)

    by_vessel = defaultdict(list)
    for r in all_rows:
        by_vessel[r["vessel"]].append(r)

    for vessel in wanted:
        rows = by_vessel.get(vessel)
        if not rows:
            continue
        whole = by_vessel_all[vessel]
        designed = sum(1 for r in whole
                       if r["ability"] and "(open design slot)" not in r["ability"])
        # An upgrade counts as wired only when the map NAMES one and something gates it.
        # A shared effect SO puts a gate within every vessel's reach -- every hull carries a
        # crystal-explosion effect and a VesselPrismController -- so counting bare gates
        # credits Manta with upgrades it has never declared.
        gated = sum(1 for r in whole if r["upgrade"]
                    and any(not g["disabled_by"] for g in r["upgrade_gates"]))
        scaled = sum(1 for r in whole
                     if any(not x["disabled_by"] and not is_inert(x) for x in r["scaling"])
                     or (r["map_multiplier_is_read"]
                         and abs(r["map_multiplier_at_full"] - 1) > 1e-6))
        print(f"\n{vessel.upper()}   abilities {designed}/4   scaling wired {scaled}/4   "
              f"L5 gates wired {gated}/4")
        print("-" * W)
        for r in rows:
            print_row(r, args.verbose)

    print("\n" + "-" * W)
    flagged = [r for r in all_rows if r["notes"]]
    print(f"{len(all_rows)} rows, {len(flagged)} with a declaration/wiring disagreement"
          f"{'  (--gaps to list only those)' if flagged and not args.gaps else ''}")


if __name__ == "__main__":
    main()
