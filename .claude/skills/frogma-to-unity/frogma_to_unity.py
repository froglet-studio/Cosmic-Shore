#!/usr/bin/env python3
"""frogma_to_unity.py - the helper of /frogma-to-unity: a picked Frogma board, built in the real Unity game.

    frogma_to_unity.py extract <board.dc.html> --id <spec id> [--board-version V] [--out Docs/UI/specs/<id>.md]
        Draft a spec from a board: every colour the board paints mapped to its theme token (Unity UIColorToken and
        the web token), every font size to its type role, every button label and state the markup shows. A colour
        or size with no token becomes a QUESTION, never a guess. The draft is a start: a person finishes it.
    frogma_to_unity.py spec-check <spec.md> [...]
        The spec format gate (Docs/UI/specs/README.md): every section, a status line, every hex on the page mapped
        in Literals or asked in Questions, every question with the board's reading and what the build does meanwhile.
    frogma_to_unity.py overrides [--base origin/bleeding-edge] [--allow N] [paths ...]
        The shared-prefab gate on the scenes and prefabs this branch changed (default: every changed .unity/.prefab):
        FAILS on a new prefab-instance override that NULLS a reference (objectReference {fileID: 0} over a field the
        source prefab wires: the Menu_Main domain-picker failure, CLAUDE.md "Shared prefabs") and on more than
        --allow new override paths on instances that already existed. New instances are placements, reported only.
    frogma_to_unity.py gate --spec <spec.md> [--base REF]
        Step 4 in one: spec-check, overrides (allow = the spec's "Allowed overrides"), every generator the spec's
        "Generators" line names with --check, check_ui_colors.py and colour_trace.py --check.
    frogma_to_unity.py proof --spec <spec.md> --board <board.dc.html> --file <scene|prefab> --objects A,B --out DIR
        The proof sheet when no Unity can render (a cloud session): the board rendered locally (frogma's
        render_local.cjs) beside the BUILT objects drawn from their serialized YAML (fill, border, label colour, label,
        size) at PC 1920x1080, phone 844x390 and iPad 1180x820, plus the gamepad-focus state (the kit's focus ring in
        the Selection token). Every tile says SIMULATION: /unity-look is the real renderer.
    frogma_to_unity.py status-note --live <canvas.json> --root <dir> --board <file.dc.html> --text "<status>"
        Put (or update) the build status under a board on Frogma: one title note f2u-<board stem>, the index written
        the way frogma.py writes it, only that key changed. Publish ONLY the index (frogma §1.5).
    frogma_to_unity.py --self-test
        Negative controls: a planted nulled reference override must fail `overrides`; a spec missing a section, a
        hex left unmapped and a question without a reading must fail `spec-check`; extract must map the kit's colours.

A READER except `extract --out` and `status-note` (they write the files they are given) and `proof` (PNG/HTML into
--out). It never writes a Unity asset: the build goes through the screen's generator or a FrogletTools wirer.
"""
from __future__ import annotations

import argparse
import html
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
THEME_ASSET = ROOT / "Assets/_SO_Assets/UI/UITheme.asset"
TOKENS_JSON = ROOT / "Docs/UI/froglet-tokens.json"
RENDER_LOCAL = ROOT / ".claude/skills/frogma/render_local.cjs"
KIT_BUTTON_GUID = "36c1a95b6e2ce113095054da8aaa40ab"

# UITheme.asset field -> UIColorToken name (Assets/_Scripts/UI/UITheme.cs).
FIELD_TOKEN = {
    "textLight": "TextLight", "textInactive": "TextInactive", "inactiveLight": "InactiveLight",
    "surfaceBlack": "SurfaceBlack", "surfaceVeryDark": "SurfaceVeryDark", "surfaceDark": "SurfaceDark",
    "surfaceLight": "SurfaceLight", "neutralLightest": "NeutralLightest", "cta": "Cta", "danger": "Danger",
    "controlActive": "ControlActive", "purchase": "Purchase", "tooltip": "Tooltip", "selection": "Selection",
}
RETIRED_TOKENS = {"elementCharge", "elementMass", "elementSpace", "elementTime"}   # elements carry no colour (LOCKED)
# Web tokens with no UIColorToken: where their Unity value lives.
WEB_ONLY = {"on-commit": "UITheme.OnCommit", "on-fill": "SurfaceVeryDark"}

SECTIONS = ("Target", "Regions", "Components", "States", "Text", "Icons", "Motion", "Devices", "Literals",
            "Questions", "Build map", "Differs from the board")
DEVICES = ("PC", "Phone", "iPad", "Gamepad")


# ----------------------------------------------------------------------------------------------------------------
# Tokens
# ----------------------------------------------------------------------------------------------------------------

def _hex(r: float, g: float, b: float) -> str:
    return "#%02X%02X%02X" % tuple(round(v * 255) for v in (r, g, b))


def unity_tokens(theme_text: str | None = None) -> dict[str, str]:
    """hex -> UIColorToken, read from UITheme.asset (the authored theme, never recalled)."""
    text = theme_text if theme_text is not None else THEME_ASSET.read_text(encoding="utf-8")
    out: dict[str, str] = {}
    for m in re.finditer(r"^  (\w+): \{r: ([\d.e-]+), g: ([\d.e-]+), b: ([\d.e-]+), a: [\d.e-]+\}", text, re.M):
        field, r, g, b = m.group(1), *map(float, m.group(2, 3, 4))
        if field in FIELD_TOKEN:
            out.setdefault(_hex(r, g, b), FIELD_TOKEN[field])
        elif field in RETIRED_TOKENS:
            out.setdefault(_hex(r, g, b), f"RETIRED:{field}")
    return out


def web_tokens() -> dict[str, list[str]]:
    """hex -> the deep-sea web token names (Docs/UI/froglet-tokens.json)."""
    data = json.loads(TOKENS_JSON.read_text(encoding="utf-8"))
    out: dict[str, list[str]] = {}
    for name, hx in data["themes"]["deep-sea"].items():
        out.setdefault(hx.upper(), []).append(name)
    return out


def type_roles() -> dict[int, list[str]]:
    data = json.loads(TOKENS_JSON.read_text(encoding="utf-8"))
    out: dict[int, list[str]] = {}
    for name, px in data["type"].items():
        out.setdefault(int(px), []).append(name)
    return out


def map_hex(hx: str, unity: dict, web: dict) -> str | None:
    hx = hx.upper()
    if hx in unity:
        names = web.get(hx, [])
        return f"{unity[hx]}" + (f" (web {', '.join(names)})" if names else "")
    for name in web.get(hx, []):
        if name in WEB_ONLY:
            return f"{WEB_ONLY[name]} (web {name})"
    return None


# ----------------------------------------------------------------------------------------------------------------
# extract
# ----------------------------------------------------------------------------------------------------------------

def board_markup(src: str) -> str:
    m = re.search(r"<x-dc>(.*)</x-dc>", src, re.S)
    return m.group(1) if m else src


def board_facts(src: str) -> dict:
    body = board_markup(src)
    hexes: dict[str, int] = {}
    for h in re.findall(r"#([0-9A-Fa-f]{6})\b", body):
        hexes["#" + h.upper()] = hexes.get("#" + h.upper(), 0) + 1
    rgba = sorted(set(re.findall(r"rgba\([^)]*\)", body)))
    sizes = sorted({int(s) for s in re.findall(r"font-size:\s*(\d+)px", body)})
    buttons = []
    for m in re.finditer(r"<button([^>]*)>(.*?)</button>", body, re.S):
        attrs, label = m.group(1), re.sub(r"<[^>]+>", "", m.group(2)).strip()
        style = re.search(r'style="([^"]*)"', attrs)
        st = style.group(1) if style else ""
        bg = re.search(r"background(?:-color)?:\s*(#[0-9A-Fa-f]{6})", st)
        fg = re.search(r"(?<![-\w])color:\s*(#[0-9A-Fa-f]{6})", st)
        state = ("disabled" if "disabled" in attrs else "pressed" if "translateY" in st else
                 "hover" if "linear-gradient" in st else "focus" if "box-shadow" in st else "rest")
        buttons.append({"label": html.unescape(label), "fill": bg.group(1).upper() if bg else None,
                        "text": fg.group(1).upper() if fg else None, "state": state})
    slivers = sorted({int(s) for s in re.findall(r"polygon\((\d+)px 0", src)})
    return {"hexes": hexes, "rgba": rgba, "sizes": sizes, "buttons": buttons, "slivers": slivers}


def cmd_extract(args) -> int:
    src = Path(args.board).read_text(encoding="utf-8")
    facts = board_facts(src)
    unity, web, roles = unity_tokens(), web_tokens(), type_roles()
    title = re.search(r"<title>(.*?)</title>", src)
    lines = [f"# Spec {args.id}: {html.unescape(title.group(1)) if title else Path(args.board).stem}", "",
             f"Board: {Path(args.board).name} on Frogma (https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2), "
             f"version {args.board_version or '<the version the read named>'}",
             "Status: draft (extracted by frogma_to_unity.py; finish every section, then ask for approval)", ""]
    for s in SECTIONS:
        lines += [f"## {s}", ""]
        if s == "Components":
            lines += ["| Component | Role | Token(s) | Type role | Spacing | Sliver | Unity |", "|---|---|---|---|---|---|---|"]
            seen = set()
            for b in facts["buttons"]:
                key = (b["label"], b["fill"], b["text"])
                if key in seen or b["state"] != "rest":
                    continue
                seen.add(key)
                fill = map_hex(b["fill"], unity, web) if b["fill"] else "-"
                text = map_hex(b["text"], unity, web) if b["text"] else "-"
                lines.append(f"| button \"{b['label']}\" | ? | fill {fill or 'Q: ' + str(b['fill'])}; label "
                             f"{text or 'Q: ' + str(b['text'])} | ? | ? | {', '.join(map(str, facts['slivers'])) or '?'} | ? |")
        elif s == "States":
            states = sorted({b["state"] for b in facts["buttons"]})
            lines += ["| State | Looks like | Token / rule |", "|---|---|---|"]
            lines += [f"| {st} | ? | ? |" for st in states]
        elif s == "Text":
            lines += ["Font sizes on the board (px at 1920) and their type roles:", ""]
            for px in facts["sizes"]:
                lines.append(f"- {px}px: {', '.join(roles.get(px, [])) or 'Q: no type role at this size'}")
        elif s == "Devices":
            lines += [f"- {d}: ?" for d in DEVICES]
        elif s == "Literals":
            lines += ["| Literal | Count | Token |", "|---|---|---|"]
            for hx, n in sorted(facts["hexes"].items(), key=lambda kv: -kv[1]):
                lines.append(f"| {hx} | {n} | {map_hex(hx, unity, web) or 'Q: no token'} |")
            for c in facts["rgba"]:
                lines.append(f"| {c} | - | Q: a translucent colour; name its token or rule |")
        elif s == "Questions":
            lines += ["(Q1: the question. Board: what the board shows. Meanwhile: what the build does until it is answered.)"]
        else:
            lines.append("?")
        lines.append("")
    text = "\n".join(lines)
    if args.out:
        Path(args.out).parent.mkdir(parents=True, exist_ok=True)
        Path(args.out).write_text(text, encoding="utf-8")
        print(f"wrote {args.out}")
    else:
        print(text)
    unmapped = [hx for hx in facts["hexes"] if not map_hex(hx, unity, web)]
    if unmapped:
        print(f"NOTE: {len(unmapped)} colour(s) with no token: {', '.join(unmapped)} - each is a question", file=sys.stderr)
    return 0


# ----------------------------------------------------------------------------------------------------------------
# spec-check
# ----------------------------------------------------------------------------------------------------------------

def spec_problems(text: str) -> list[str]:
    out = []
    heads = {m.group(1).strip() for m in re.finditer(r"^##\s+(.+)$", text, re.M)}
    for s in SECTIONS:
        if s not in heads:
            out.append(f"missing section '## {s}'")
    if not re.search(r"^Status:\s*(draft|approved|built|in PR)", text, re.M | re.I):
        out.append("no 'Status:' line (draft | approved (who, date) | built (PR))")
    if not re.search(r"^Board:\s*\S", text, re.M):
        out.append("no 'Board:' line naming the Frogma board and its version")

    def section(name: str) -> str:
        m = re.search(rf"^##\s+{re.escape(name)}\s*$(.*?)(?=^##\s|\Z)", text, re.M | re.S)
        return m.group(1) if m else ""

    lits = section("Literals")
    mapped = {m.group(1).upper() for m in re.finditer(r"^\|\s*(#[0-9A-Fa-f]{6})\s*\|", lits, re.M)}
    for row in re.finditer(r"^\|\s*(#[0-9A-Fa-f]{6})\s*\|[^|]*\|\s*([^|]*)\|", lits, re.M):
        if not row.group(2).strip() or row.group(2).strip() in ("?", "-"):
            out.append(f"Literals: {row.group(1)} has no token and no question")
    body = text.replace(lits, "")
    for hx in sorted({h.upper() for h in re.findall(r"#[0-9A-Fa-f]{6}\b", body)}):
        if hx not in mapped:
            out.append(f"{hx} appears in the spec but not in Literals (map it to a token or ask)")
    qs = section("Questions")
    for m in re.finditer(r"^\s*[-*]?\s*\**(Q\d+)\**[.:](.*?)(?=^\s*[-*]?\s*\**Q\d+\**[.:]|\Z)", qs, re.M | re.S):
        q = m.group(2)
        if "Board:" not in q or "Meanwhile:" not in q:
            out.append(f"Questions: {m.group(1)} needs 'Board:' (what the board shows) and 'Meanwhile:' (what the build does)")
    for ref in set(re.findall(r"\bQ: ", text)):
        pass
    dev = section("Devices")
    for d in DEVICES:
        if not re.search(rf"^\s*[-*]\s*\**{d}", dev, re.M):
            out.append(f"Devices: no line for {d}")
    if "?" in [l.strip() for l in text.splitlines()]:
        out.append("a section is still the draft's '?'")
    if not re.search(r"^Allowed overrides:\s*\d+", text, re.M):
        out.append("no 'Allowed overrides: N' line (new scene overrides on shared prefabs the spec needs; normally 0)")
    return out


def cmd_spec_check(args) -> int:
    bad = 0
    for p in args.specs:
        probs = spec_problems(Path(p).read_text(encoding="utf-8"))
        for x in probs:
            print(f"FAIL {p}: {x}")
        bad += bool(probs)
        if not probs:
            print(f"OK: {p} is a complete spec")
    return 1 if bad else 0


# ----------------------------------------------------------------------------------------------------------------
# overrides
# ----------------------------------------------------------------------------------------------------------------

# Unity wraps a long target line ("guid: ...,\n        type: 3}") and quotes a path with brackets: both allowed.
MOD_RE = re.compile(r"- target: \{fileID: (-?\d+), guid: ([0-9a-f]{32}),\s+type: \d+\}\n\s+propertyPath: '?([^'\n]*)'?\n"
                    r"\s+value: ?(.*)\n\s+objectReference: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?", re.M)
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)", re.M)


def instances(text: str) -> dict[str, list[tuple]]:
    """PrefabInstance document id -> its modifications (target fid, guid, path, value, ref fid, ref guid)."""
    out: dict[str, list[tuple]] = {}
    docs = list(DOC_RE.finditer(text))
    for i, d in enumerate(docs):
        if d.group(1) != "1001":
            continue
        body = text[d.end(): docs[i + 1].start() if i + 1 < len(docs) else len(text)]
        out[d.group(2)] = [m.groups() for m in MOD_RE.finditer(body)]
    return out


_GUID_PATHS: dict[str, Path] | None = None


def guid_path(guid: str) -> Path | None:
    global _GUID_PATHS
    if _GUID_PATHS is None:
        _GUID_PATHS = {}
        for meta in (ROOT / "Assets").rglob("*.prefab.meta"):
            try:
                m = re.search(r"^guid: ([0-9a-f]{32})", meta.read_text(encoding="utf-8", errors="replace"), re.M)
            except OSError:
                continue
            if m:
                _GUID_PATHS[m.group(1)] = meta.with_suffix("")
    return _GUID_PATHS.get(guid)


def source_reference(prefab_text: str, fid: str, path: str) -> str | None:
    """What the source prefab wires at `path` on document `fid`: 'ref' (a non-null reference), 'null', or None."""
    docs = list(DOC_RE.finditer(prefab_text))
    for i, d in enumerate(docs):
        if d.group(2) != fid:
            continue
        body = prefab_text[d.end(): docs[i + 1].start() if i + 1 < len(docs) else len(prefab_text)]
        m = re.match(r"([\w]+)\.Array\.data\[(\d+)\]$", path)
        if m:
            lst = re.search(rf"^  {m.group(1)}:\n((?:  - .*\n)+)", body, re.M)
            if not lst:
                return None
            items = lst.group(1).splitlines()
            idx = int(m.group(2))
            if idx >= len(items):
                return None
            r = re.search(r"fileID: (-?\d+)", items[idx])
            return None if not r else ("null" if r.group(1) == "0" else "ref")
        f = re.search(rf"^  {re.escape(path)}: \{{fileID: (-?\d+)", body, re.M)
        return None if not f else ("null" if f.group(1) == "0" else "ref")
    return None


def nulled(mod: tuple, cache: dict) -> str | None:
    """A reason when this modification nulls a reference the source prefab wires (or is shaped like it)."""
    fid, guid, path, value, ref, _ = mod
    if ref != "0" or value.strip():
        return None
    src = guid_path(guid)
    if src is not None:
        if src not in cache:
            cache[src] = src.read_text(encoding="utf-8", errors="replace")
        kind = source_reference(cache[src], fid, path)
        if kind == "ref":
            return f"nulls {path} on {src.name} (the prefab wires it)"
        if kind == "null":
            return None
    if re.search(r"\.Array\.data\[\d+\]$", path) and not path.startswith("m_text"):
        return f"nulls list element {path} (source unresolved: a nested prefab?)"
    return None


def git_show(base: str, path: str) -> str:
    r = subprocess.run(["git", "show", f"{base}:{path}"], cwd=ROOT, capture_output=True, text=True)
    return r.stdout if r.returncode == 0 else ""


def changed_assets(base: str) -> list[str]:
    r = subprocess.run(["git", "diff", "--name-only", f"{base}...HEAD"], cwd=ROOT, capture_output=True, text=True)
    w = subprocess.run(["git", "diff", "--name-only", "HEAD"], cwd=ROOT, capture_output=True, text=True)
    files = set(r.stdout.split("\n")) | set(w.stdout.split("\n"))
    return sorted(f for f in files if f.endswith((".unity", ".prefab")) and (ROOT / f).exists())


REF_FIELD_RE = re.compile(r"^  (\w+): \{fileID: (-?\d+)(?:, guid: [0-9a-f]{32})?(?:, type: \d+)?\}", re.M)
REF_LIST_RE = re.compile(r"^  (\w+):\n((?:  - \{fileID: -?\d+.*\}\n)+)", re.M)


def local_refs(text: str) -> dict[tuple[str, str], str]:
    """(document id, field or field[i]) -> referenced fileID, for the file's own MonoBehaviours (class 114)."""
    out = {}
    docs = list(DOC_RE.finditer(text))
    for i, d in enumerate(docs):
        if d.group(1) != "114":
            continue
        body = text[d.end(): docs[i + 1].start() if i + 1 < len(docs) else len(text)]
        for m in REF_FIELD_RE.finditer(body):
            if not m.group(1).startswith("m_"):
                out[(d.group(2), m.group(1))] = m.group(2)
        for m in REF_LIST_RE.finditer(body):
            for j, item in enumerate(m.group(2).splitlines()):
                r = re.search(r"fileID: (-?\d+)", item)
                out[(d.group(2), f"{m.group(1)}[{j}]")] = r.group(1)
    return out


def local_nulls(before: str, after: str, name: str) -> list[str]:
    """A reference the file's own components wired in `before` and hold null in `after`."""
    b, a = local_refs(before), local_refs(after)
    return [f"{name}: component &{k[0]} {k[1]} was wired ({b[k]}) and is now null"
            for k, v in a.items() if v == "0" and b.get(k, "0") != "0"]


def override_report(before: str, after: str, name: str) -> tuple[list[str], list[str], dict]:
    """(failures, notes, counts) for one file: nulled refs and new paths on instances that already existed."""
    b, a = instances(before), instances(after)
    cache: dict = {}
    fails, notes = [], []
    counts = {"new_paths": 0, "changed": 0, "new_instances": 0}
    for inst, mods in a.items():
        old = b.get(inst)
        if old is None:
            counts["new_instances"] += 1
            for m in mods:
                why = nulled(m, cache)
                if why:
                    fails.append(f"{name}: new instance &{inst} {why}")
            continue
        old_keys = {(m[0], m[1], m[2]): m for m in old}
        for m in mods:
            k = (m[0], m[1], m[2])
            if k in old_keys and old_keys[k] == m:
                continue
            why = nulled(m, cache)
            if why:
                fails.append(f"{name}: instance &{inst} {why}")
            if k not in old_keys:
                counts["new_paths"] += 1
                notes.append(f"{name}: instance &{inst} new override {m[2]}")
            else:
                counts["changed"] += 1
    fails += local_nulls(before, after, name)
    return fails, notes, counts


def cmd_overrides(args) -> int:
    files = args.paths or changed_assets(args.base)
    total_new, fails = 0, []
    for f in files:
        after = (ROOT / f).read_text(encoding="utf-8", errors="replace")
        before = git_show(args.base, f)
        fl, notes, c = override_report(before, after, f)
        fails += fl
        total_new += c["new_paths"]
        print(f"{f}: {c['new_instances']} new instance(s), {c['new_paths']} new override path(s) on existing "
              f"instances, {c['changed']} changed value(s)")
        for n in notes[:20]:
            print("   " + n)
    for x in fails:
        print("FAIL " + x)
    if total_new > args.allow:
        print(f"FAIL {total_new} new override path(s) on existing prefab instances; the spec allows {args.allow}. "
              "Change the prefab or a variant (Docs/GAMECANVAS.md), or add the need to the spec.")
        return 1
    if fails:
        return 1
    print(f"OK: {len(files)} file(s), no nulled reference, {total_new} new override path(s) (allowed {args.allow})")
    return 0


# ----------------------------------------------------------------------------------------------------------------
# gate
# ----------------------------------------------------------------------------------------------------------------

def run(cmd: list[str]) -> int:
    print("$ " + " ".join(cmd), flush=True)
    r = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
    tail = (r.stdout + r.stderr).strip().splitlines()[-3:]
    for t in tail:
        print("   " + t)
    return r.returncode


def cmd_gate(args) -> int:
    text = Path(args.spec).read_text(encoding="utf-8")
    bad = []
    probs = spec_problems(text)
    for p in probs:
        print(f"FAIL spec: {p}")
    if probs:
        bad.append("spec")
    allow = int(re.search(r"^Allowed overrides:\s*(\d+)", text, re.M).group(1)) if not probs else 0
    ns = argparse.Namespace(base=args.base, allow=allow, paths=[])
    if cmd_overrides(ns):
        bad.append("overrides")
    gens = re.search(r"^Generators:\s*(.+)$", text, re.M)
    for g in (gens.group(1).replace("`", "").split(",") if gens else []):
        g = g.strip()
        if g and run([sys.executable, g, "--check"]):
            bad.append(g)
    for cmd in ([sys.executable, "Tools/Build/check_ui_colors.py"],
                [sys.executable, "Tools/Build/colour_trace.py", "--check"]):
        if run(cmd):
            bad.append(cmd[1])
    print(("FAIL: " + ", ".join(bad)) if bad else "OK: the build holds the spec's gates")
    return 1 if bad else 0


# ----------------------------------------------------------------------------------------------------------------
# proof
# ----------------------------------------------------------------------------------------------------------------

def yaml_docs(text: str) -> dict[str, tuple[str, str]]:
    docs = list(DOC_RE.finditer(text))
    return {d.group(2): (d.group(1), text[d.end(): docs[i + 1].start() if i + 1 < len(docs) else len(text)])
            for i, d in enumerate(docs)}


def _col(body: str, field: str) -> str | None:
    m = re.search(rf"^  {field}: \{{r: ([\d.e-]+), g: ([\d.e-]+), b: ([\d.e-]+), a: ([\d.e-]+)\}}", body, re.M)
    if not m:
        return None
    r, g, b, a = map(float, m.groups())
    return f"rgba({round(r * 255)},{round(g * 255)},{round(b * 255)},{a:.3f})"


def built_button(text: str, name: str) -> dict:
    """A KitButton drawn as a plain node (generators emit them so): its colours, label, size and focus token."""
    docs = yaml_docs(text)
    go = next((fid for fid, (cls, body) in docs.items()
               if cls == "1" and re.search(rf"^  m_Name: {re.escape(name)}$", body, re.M)), None)
    if not go:
        raise SystemExit(f"no GameObject named {name}")
    comps = [m.group(1) for m in re.finditer(r"component: \{fileID: (-?\d+)\}", docs[go][1])]
    kb = next((c for c in comps if KIT_BUTTON_GUID in docs.get(c, ("", ""))[1]), None)
    if not kb:
        raise SystemExit(f"{name} carries no KitButton")
    body = docs[kb][1]

    def ref(field):
        m = re.search(rf"^  {field}: \{{fileID: (-?\d+)\}}", body, re.M)
        return docs.get(m.group(1), ("", ""))[1] if m else ""

    label_body = ""
    lst = re.search(r"^  contentGraphics:\n  - \{fileID: (-?\d+)\}", body, re.M)
    if lst:
        label_body = docs.get(lst.group(1), ("", ""))[1]
    rt = next((c for c in comps if docs.get(c, ("", ""))[0] == "224"), None)
    size = re.search(r"m_SizeDelta: \{x: ([\d.-]+), y: ([\d.-]+)\}", docs[rt][1]) if rt else None
    variant = re.search(r"^  variant: (\d+)", body, re.M)
    txt = re.search(r"^  m_text: (.*)$", label_body, re.M)
    return {"name": name, "variant": int(variant.group(1)) if variant else None,
            "fill": _col(ref("fillGraphic"), "m_Color"), "border": _col(ref("borderGraphic"), "m_Color"),
            "label": (txt.group(1).strip().strip("'\"") if txt else name).upper(),
            "text": _col(label_body, "m_fontColor"),
            "w": float(size.group(1)) if size else 260.0, "h": float(size.group(2)) if size else 60.0}


def theme_hex(field: str) -> str:
    m = re.search(rf"^  {field}: \{{r: ([\d.e-]+), g: ([\d.e-]+), b: ([\d.e-]+)", THEME_ASSET.read_text(), re.M)
    return _hex(*map(float, m.groups()))


DEVICE_SIZES = (("PC 1920x1080", 1920, 1080), ("Phone 844x390", 844, 390), ("iPad 1180x820", 1180, 820))


def proof_html(buttons: list[dict], board_png: str | None, title: str) -> str:
    sel = theme_hex("selection")
    ground = theme_hex("surfaceVeryDark")
    css = ("body{margin:0;background:#00010A;color:#E6E9FF;font-family:Aldrich,sans-serif}"
           ".sl{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}"
           ".cap{font:600 13px 'Chakra Petch',sans-serif;letter-spacing:.14em;text-transform:uppercase;color:#747BAD}"
           ".sim{color:#FF4B3A}")
    head = ('<link href="https://fonts.googleapis.com/css2?family=Aldrich&family=Chakra+Petch:wght@600&display=swap"'
            ' rel="stylesheet">')

    def btn(b, scale, focus=False):
        w, h = b["w"] * scale, b["h"] * scale
        inner = (f'<div class="sl" style="width:{w}px;height:{h}px;background:{b["border"]};padding:1px;box-sizing:border-box">'
                 f'<div class="sl" style="width:100%;height:100%;background:{b["fill"]};display:flex;align-items:center;'
                 f'justify-content:center;font:600 {18 * scale:.1f}px \'Chakra Petch\';letter-spacing:.1em;color:{b["text"]}">'
                 f'{html.escape(b["label"])}</div></div>')
        if focus:
            inner = f'<div class="sl" style="display:inline-block;padding:{3 * scale:.1f}px;background:{sel}">{inner}</div>'
        return f'<div style="margin:{8 * scale:.1f}px">{inner}</div>'

    cols = []
    for label, w, h in DEVICE_SIZES:
        scale = h / 1080   # CanvasScaler: 1920x1080, match height (Docs/UI_STYLE_GUIDE.md section 8)
        frame = (f'<div style="width:{w * 0.5}px;height:{h * 0.5}px;background:{ground};position:relative;overflow:hidden;'
                 f'display:flex;flex-wrap:wrap;align-content:center;justify-content:center;transform-origin:0 0">'
                 + "".join(btn(b, scale * 0.5) for b in buttons)
                 + "".join(btn(b, scale * 0.5, focus=True) for b in buttons[:1]) + "</div>")
        cols.append(f'<div style="margin:12px"><div class="cap">{label} (canvas scale {scale:.2f}; last tile: gamepad focus)</div>{frame}</div>')
    board = (f'<div style="margin:12px"><div class="cap">The Frogma board (render_local)</div>'
             f'<img src="{board_png}" style="width:960px;display:block"></div>') if board_png else ""
    rows = "".join(f"<tr><td>{html.escape(b['name'])}</td><td>{b['variant']}</td><td>{b['fill']}</td><td>{b['border']}</td>"
                   f"<td>{b['text']}</td></tr>" for b in buttons)
    return (f"<!doctype html><html><head><meta charset=utf-8>{head}<style>{css}</style></head><body>"
            f'<div style="padding:16px"><div class="cap">{html.escape(title)} - <span class="sim">SIMULATION from the '
            f'serialized YAML, not the Unity renderer (/unity-look)</span></div>'
            f'<div style="display:flex;align-items:flex-start;flex-wrap:wrap">{board}'
            f'<div>{"".join(cols)}</div></div>'
            f'<table class="cap" style="margin:12px;border-spacing:12px 2px"><tr><td>object</td><td>variant</td><td>fill</td>'
            f'<td>border</td><td>label</td></tr>{rows}</table></div></body></html>')


def shoot(html_path: Path, png: Path, width: int = 2100) -> None:
    js = (f"const {{chromium}}=require('playwright');(async()=>{{const b=await chromium.launch();"
          f"const p=await b.newPage({{viewport:{{width:{width},height:900}}}});await p.goto('file://{html_path}');"
          f"await p.waitForTimeout(800);await p.screenshot({{path:'{png}',fullPage:true}});await b.close();}})();")
    subprocess.run(["node", "-e", js], check=True, cwd=ROOT)


def cmd_proof(args) -> int:
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    text = (ROOT / args.file).read_text(encoding="utf-8")
    buttons = [built_button(text, n.strip()) for n in args.objects.split(",")]
    board_png = None
    if args.board:
        bdir = Path(args.board).resolve().parent
        rdir = out / "board"
        subprocess.run(["node", str(RENDER_LOCAL), str(bdir), str(rdir), Path(args.board).name], check=True, cwd=ROOT)
        pngs = sorted(rdir.glob("*.png"))
        board_png = str(pngs[0]) if pngs else None
    page = out / "proof.html"
    page.write_text(proof_html(buttons, board_png, f"{args.file}: {args.objects}"), encoding="utf-8")
    shoot(page.resolve(), (out / "proof.png").resolve())
    print(json.dumps(buttons, indent=1))
    print(f"wrote {out / 'proof.png'}")
    return 0


# ----------------------------------------------------------------------------------------------------------------
# status-note
# ----------------------------------------------------------------------------------------------------------------

def add_status_note(live: dict, board: str, text: str) -> str:
    entry = live["boards"].get(board)
    if not entry:
        raise SystemExit(f"no board {board} on the canvas")
    key = "f2u-" + Path(board).name.split(".")[0].lower()
    note = {"kind": "title1", "maxW": entry["w"], "page": entry["page"], "text": text, "w": 240,
            "x": entry["x"], "y": entry["y"] + entry["h"] + 40}
    below = [b for b in live["boards"].values() if b.get("page") == entry["page"] and b is not entry
             and b["x"] < entry["x"] + entry["w"] and entry["x"] < b["x"] + b["w"]
             and note["y"] <= b["y"] < note["y"] + 140]
    if below:
        raise SystemExit(f"no room under {board}: a board starts {below[0]['y'] - note['y']} px below the note")
    live["notes"][key] = note
    order = live.setdefault("noteOrder", [])
    if key not in order:
        order.append(key)
    return key


def cmd_status_note(args) -> int:
    live = json.loads(Path(args.live).read_text(encoding="utf-8"))
    before = json.loads(json.dumps(live))
    key = add_status_note(live, args.board, args.text)
    probe = json.loads(json.dumps(live))
    del probe["notes"][key]
    if key not in before["notes"]:
        probe["noteOrder"].remove(key)
    else:
        probe["notes"][key] = before["notes"][key]
    assert probe == before, "status-note changed a key that is not its own"
    root = Path(args.root)
    (root / "project").mkdir(parents=True, exist_ok=True)
    with open(root / "project" / "canvas.json", "w", encoding="utf-8") as f:   # frogma.py write_index's form
        json.dump(live, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"wrote {root / 'project/canvas.json'} with note {key}: {args.text}")
    return 0


# ----------------------------------------------------------------------------------------------------------------
# self-test
# ----------------------------------------------------------------------------------------------------------------

GOOD_SPEC = """# Spec t: x
Board: SGButtons.dc.html, version 1
Status: approved (Shombith, 2026-11-12)
Allowed overrides: 0
Generators: Tools/Build/author_ui_kit_prefabs.py

## Target
the kit
## Regions
one row
## Components
| Component | Role | Token(s) |
|---|---|---|
| button | commit | Tooltip |
## States
rest
## Text
18px Button
## Icons
none
## Motion
none
## Devices
- PC: 1920
- Phone: scaled
- iPad: scaled
- Gamepad: focus ring
## Literals
| Literal | Count | Token |
|---|---|---|
| #A600FF | 1 | Tooltip (web commit) |
## Questions
- Q1. Is it so? Board: yes. Meanwhile: it is.
## Build map
KitButton.cs
## Differs from the board
nothing
"""

PREFAB = """%YAML 1.1
--- !u!114 &500
MonoBehaviour:
  domainInfoItems:
  - {fileID: 600}
  - {fileID: 601}
  target: {fileID: 700}
"""


def self_test() -> int:
    fails = []

    def expect(cond, what):
        if not cond:
            fails.append(what)
        print(("  ok: " if cond else "  MISSED: ") + what)

    print("spec-check:")
    expect(spec_problems(GOOD_SPEC) == [], "a complete spec passes " + str(spec_problems(GOOD_SPEC)))
    expect(any("Motion" in p for p in spec_problems(GOOD_SPEC.replace("## Motion\nnone\n", ""))), "a missing section fails")
    expect(any("#123456" in p for p in spec_problems(GOOD_SPEC.replace("## Icons\nnone", "## Icons\n#123456"))),
           "a hex left out of Literals fails")
    expect(any("Q1" in p for p in spec_problems(GOOD_SPEC.replace(" Board: yes. Meanwhile: it is.", ""))),
           "a question without the board's reading fails")
    expect(any("Gamepad" in p for p in spec_problems(GOOD_SPEC.replace("- Gamepad: focus ring\n", ""))),
           "a spec with no gamepad line fails")

    print("overrides (negative control: a deliberately nulled reference):")
    tmp = Path(tempfile.mkdtemp())
    src = tmp / "Modal.prefab"
    src.write_text(PREFAB)
    global _GUID_PATHS
    saved = _GUID_PATHS
    _GUID_PATHS = {"a" * 32: src}

    def scene(mods: str) -> str:
        return ("%YAML 1.1\n--- !u!1001 &9\nPrefabInstance:\n  m_Modification:\n    m_Modifications:\n" + mods
                + "--- !u!1 &10\nGameObject:\n  m_Name: x\n")

    def mod(path, value="", ref="0"):
        return (f"    - target: {{fileID: 500, guid: {'a' * 32}, type: 3}}\n      propertyPath: {path}\n"
                f"      value: {value}\n      objectReference: {{fileID: {ref}}}\n")

    base = scene(mod("m_Name", "Modal"))
    f, _, _ = override_report(base, base, "same")
    expect(f == [], "an unchanged scene passes")
    f, _, c = override_report(base, scene(mod("m_Name", "Modal") + mod("domainInfoItems.Array.data[1]")), "planted")
    expect(any("domainInfoItems" in x for x in f), "a nulled list reference the prefab wires FAILS (the domain picker)")
    f, _, _ = override_report(base, scene(mod("m_Name", "Modal") + mod("target")), "planted")
    expect(any("target" in x for x in f), "a nulled single reference the prefab wires FAILS")
    f, _, c = override_report(base, scene(mod("m_Name", "Modal") + mod("m_text", "Hi")), "value")
    expect(f == [] and c["new_paths"] == 1, "a new value override is counted, not called a nulled reference")
    f, _, c = override_report("", scene(mod("domainInfoItems.Array.data[0]")), "new instance")
    expect(any("new instance" in x for x in f), "a NEW instance that nulls a wired reference fails too")
    wrapped = scene(mod("m_Name", "Modal") + (f"    - target: {{fileID: 500, guid: {'a' * 32},\n        type: 3}}\n"
                    "      propertyPath: 'domainInfoItems.Array.data[0]'\n      value: \n      objectReference: {fileID: 0}\n"))
    f, _, _ = override_report(base, wrapped, "wrapped")
    expect(any("domainInfoItems" in x for x in f), "Unity's wrapped target line and quoted path are read (missed once)")
    _GUID_PATHS = saved
    shutil.rmtree(tmp)
    comp = "--- !u!114 &77\nMonoBehaviour:\n  domainInfoItems:\n  - {fileID: 5}\n  - {fileID: 6}\n  picker: {fileID: 8}\n"
    expect(local_nulls(comp, comp, "x") == [], "a scene component left as it was passes")
    expect(len(local_nulls(comp, comp.replace("{fileID: 6}", "{fileID: 0}"), "x")) == 1,
           "a scene component's list element nulled FAILS")
    expect(len(local_nulls(comp, comp.replace("picker: {fileID: 8}", "picker: {fileID: 0}"), "x")) == 1,
           "a scene component's reference nulled FAILS")

    print("extract:")
    theme = THEME_ASSET.read_text(encoding="utf-8")
    u, w = unity_tokens(theme), web_tokens()
    expect(map_hex("#A600FF", u, w).startswith("Tooltip") and "commit" in map_hex("#A600FF", u, w),
           "#A600FF maps to Tooltip (web commit)")
    expect(map_hex("#FFFFFF", u, w).startswith("UITheme.OnCommit"), "#FFFFFF maps to UITheme.OnCommit (web on-commit)")
    expect(map_hex("#123456", u, w) is None, "an off-palette colour is a question, not a guess")
    expect(unity_tokens(theme).get("#FFD34D", "").startswith("RETIRED"), "an element colour is flagged retired")
    facts = board_facts('<x-dc><button class="btn sls" style="background-color: #A600FF; color: #FFFFFF;">PLAY</button>'
                        '<button style="background-color: #5C5F70; color: #25262D;" disabled>PLAY</button></x-dc>')
    expect([b["state"] for b in facts["buttons"]] == ["rest", "disabled"], "button states are read from the markup")

    print("status-note:")
    live = {"boards": {"B.dc.html": {"x": 0, "y": 0, "w": 1920, "h": 1080, "page": "p"},
                       "C.dc.html": {"x": 0, "y": 1260, "w": 1920, "h": 1080, "page": "p"}}, "notes": {}, "noteOrder": []}
    key = add_status_note(live, "B.dc.html", "Built in Unity - PR #1")
    expect(key == "f2u-b" and live["noteOrder"] == ["f2u-b"], "the note is added under its own key")
    live["boards"]["C.dc.html"]["y"] = 1150
    try:
        add_status_note(live, "B.dc.html", "x")
        expect(False, "a note that would sit on the next board is refused")
    except SystemExit:
        expect(True, "a note that would sit on the next board is refused")

    print("FAIL: %d control(s) missed" % len(fails) if fails else "OK: every negative control fires")
    return 1 if fails else 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--self-test", action="store_true")
    sub = ap.add_subparsers(dest="cmd")
    e = sub.add_parser("extract")
    e.add_argument("board")
    e.add_argument("--id", required=True)
    e.add_argument("--board-version")
    e.add_argument("--out")
    s = sub.add_parser("spec-check")
    s.add_argument("specs", nargs="+")
    o = sub.add_parser("overrides")
    o.add_argument("--base", default="origin/bleeding-edge")
    o.add_argument("--allow", type=int, default=0)
    o.add_argument("paths", nargs="*")
    g = sub.add_parser("gate")
    g.add_argument("--spec", required=True)
    g.add_argument("--base", default="origin/bleeding-edge")
    p = sub.add_parser("proof")
    p.add_argument("--board")
    p.add_argument("--file", required=True)
    p.add_argument("--objects", required=True)
    p.add_argument("--out", required=True)
    n = sub.add_parser("status-note")
    n.add_argument("--live", required=True)
    n.add_argument("--root", required=True)
    n.add_argument("--board", required=True)
    n.add_argument("--text", required=True)
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    return {"extract": cmd_extract, "spec-check": cmd_spec_check, "overrides": cmd_overrides, "gate": cmd_gate,
            "proof": cmd_proof, "status-note": cmd_status_note}.get(args.cmd, lambda a: ap.print_help() or 2)(args)


if __name__ == "__main__":
    sys.exit(main())
