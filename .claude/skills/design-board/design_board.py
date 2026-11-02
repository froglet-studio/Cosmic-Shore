#!/usr/bin/env python3
"""design_board.py - the /design-board skill's helper for the Froglet Design Board canvas (the design board).

The design board is ONE claude.ai Design canvas, https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2 ("Froglet Design
Board"; it was "Menu Redesign B" until 2026-11-02). Its index is `project/canvas.json` and every board is a
`project/<Name>.dc.html` file. The canvas's pages are TABS (at most 40): "Menu Redesign" (the master tab every menu,
HUD and start-flow round lives in), "Compute Console", and one tab per new area (`new-tab`, the /design-board-new skill).
A ROUND is a titled section on a tab: its boards `R<N><Key>.dc.html` under a title note `rev<N>` ("Rev N - ..."), so a
round no longer costs a page. A copy of every board lives in `Docs/MenuRedesignB/board/` so a session can read designs
without the canvas. This script never talks to claude.ai: the Artifact tool reads and publishes; this script works on
the files it saved.

    design_board.py status   [--live <canvas.json>] [--version V]   what the board holds, what is new, what waits on a pick
    design_board.py find     <words...> [--live <canvas.json>]      boards and doc sections about a screen, newest first
    design_board.py tabs     [--live <canvas.json>]                 the tabs, their rounds and board counts
    design_board.py next-rev [--live <canvas.json>]                 the first free round number (note/page id AND board prefix)
    design_board.py plan     --live <canvas.json> --root <dir> --name "<round name>" (--tab <tab id> | --new-tab "<name>")
                             --board KEY="<title>" [...] [--interactive A,B] [--compare "<title>"]
                             [--notes "<title>" | --no-notes] [--notes-h 900] [--size 1920x1080] [--skeletons]
                                                                    writes <root>/project/canvas.json: the live index plus
                                                                    ONE new round (a titled section under the tab's last
                                                                    row) and its boards; prints the files to write
    design_board.py new-tab  --live <canvas.json> --root <dir> --name "<tab name>" [--id <id>]
                                                                    adds an empty tab and opens the canvas on it
    design_board.py fold     --live <canvas.json> --root <dir> --into <tab id> --name "<tab name>" --pages a,b,c
                             [--title "<canvas title>"]             moves whole pages into one tab as titled sections
                                                                    (frees pages when the canvas nears 40)
    design_board.py check    <dir> [--only R34] [--strict]          the offline gate on the boards in <dir> (holds canvas.json)
    design_board.py pull     --src <dir> [--version V] [--date D]   copy the boards and index saved from the canvas into the
                                                                    repo copy and regenerate its README
    design_board.py readme                                          regenerate the repo copy's README from its canvas.json
    design_board.py --self-test

A READER except for `plan`, `new-tab` and `fold` (write under --root), `pull` and `readme` (write Docs/MenuRedesignB/board/). Exit 0 = ok,
1 = a check failed (each one named), 2 = it could not run.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import html
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
BOARD_URL = "https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2"
BOARD_TITLE = "Froglet Design Board"
MAX_TABS = 40
COPY_DIR = REPO / "Docs" / "MenuRedesignB" / "board"
DOC = REPO / "Docs" / "MENU_REDESIGN_B.md"
README_START = "<!-- design_board.py readme: generated below, edit the text above -->"
README_END = "<!-- design_board.py readme: end -->"

# The round layout every page since Rev 3 uses: 1920x1080 screens three to a row, 180 px apart.
GAP_X = 180
GAP_Y = 180
PER_ROW = 3
TITLE_ABOVE = 400      # a round's or section's title note sits this far above its first row (the canvas wants >= 223)
ROUND_GAP = 900        # space between the tab's last row and the next round's title
SHELF_W = 36000        # fold packs sections left to right up to this width, then starts a shelf below
SHELF_GAP_X = 1200
SHELF_GAP_Y = 1400
DRAFT_MARK = "[DRAFT BOARD - not written yet]"

PAGE_ID_RE = re.compile(r"^[A-Za-z0-9_-]{1,40}$")
SEGMENT_RE = re.compile(r"^[A-Za-z0-9_][A-Za-z0-9_.-]*$")
HOLE_RE = re.compile(r"\{\{\s*(.*?)\s*\}\}", re.S)
PATH_RE = re.compile(r"^[$A-Za-z_][\w$]*(?:\.[\w$]+)*$")
LITERAL_RE = re.compile(r"^(true|false|null|-?\d+(\.\d+)?|'[^']*'|\"[^\"]*\")$")
EMOJI_RE = re.compile("[\U0001F000-\U0001FAFF]")
DINGBAT_RE = re.compile("[☀-➿]")
SUPPORT_LINE = '<script src="./support.js"></script>'


# ----------------------------------------------------------------------------------------------- helpers

def rel(p: Path) -> str:
    try:
        return str(p.relative_to(REPO))
    except ValueError:
        return str(p)


def load_index(path: Path) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def rev_of_page(page_id: str) -> int | None:
    m = re.fullmatch(r"rev(\d+)", page_id)
    return int(m.group(1)) if m else None


def rev_of_board(name: str) -> int | None:
    m = re.match(r"^R(\d+)(?!\d)", name)
    return int(m.group(1)) if m else None


def next_rev(index: dict) -> int:
    """The first round number no page id, no title note id and no board prefix has claimed. R3 and R30 differ."""
    taken = {rev_of_page(p.get("id", "")) for p in index.get("pages", [])}
    taken |= {rev_of_page(k) for k in index.get("notes", {})}
    taken |= {rev_of_board(b) for b in index.get("boards", {})}
    taken.discard(None)
    return (max(taken) + 1) if taken else 3


def first_page(index: dict) -> str | None:
    pages = index.get("pages", [])
    return pages[0].get("id") if pages else None


def tab_of(index: dict, entry: dict) -> str | None:
    """The tab a board or note shows on: its `page`, or the first page when it names none (the canvas's rule)."""
    return entry.get("page") or first_page(index)


def page_name(index: dict, page_id: str | None) -> str:
    for p in index.get("pages", []):
        if p.get("id") == page_id:
            return p.get("name", page_id)
    return "(no tab)" if page_id is None else page_id


def title_notes(index: dict) -> list[tuple[str, dict]]:
    return [(k, n) for k, n in index.get("notes", {}).items() if n.get("kind") == "title1"]


def section_of(index: dict, name: str) -> tuple[str, str]:
    """(note id, label) of the section a board belongs to: its round's `rev<N>` title note when there is one, else the
    nearest title note above the board on its tab whose span covers the board's left edge."""
    b = index.get("boards", {}).get(name, {})
    rev = rev_of_board(name)
    notes = index.get("notes", {})
    if rev is not None and f"rev{rev}" in notes:
        return f"rev{rev}", notes[f"rev{rev}"].get("text", f"Rev {rev}")
    tab = tab_of(index, b)
    best = None
    for k, n in title_notes(index):
        if tab_of(index, n) != tab:
            continue
        span = n.get("maxW", n.get("w", 1920))
        if n.get("y", 0) <= b.get("y", 0) and n.get("x", 0) - 1 <= b.get("x", 0) < n.get("x", 0) + max(span, 1920):
            key = (n.get("y", 0), n.get("x", 0))
            if best is None or key > best[0]:
                best = (key, k, n.get("text", k))
    if best:
        return best[1], best[2]
    return "", page_name(index, tab)


def round_label(index: dict, rev: int) -> str:
    """'Rev N - name' from the round's title note, else from a page of that id (rounds before tabs), else 'Rev N'."""
    n = index.get("notes", {}).get(f"rev{rev}")
    if n:
        return n.get("text", f"Rev {rev}")
    for p in index.get("pages", []):
        if p.get("id") == f"rev{rev}":
            return p.get("name", f"Rev {rev}")
    names = [k for k in index.get("boards", {}) if rev_of_board(k) == rev]
    return section_of(index, names[0])[1] if names else f"Rev {rev}"


def rounds(index: dict) -> list[tuple[int, str | None, list[str]]]:
    """Every round on the canvas, newest first: (number, tab id, board names)."""
    out: dict = {}
    for name, b in index.get("boards", {}).items():
        r = rev_of_board(name)
        if r is not None:
            out.setdefault(r, [None, []])[1].append(name)
            out[r][0] = tab_of(index, b)
    return [(r, v[0], sorted(v[1])) for r, v in sorted(out.items(), reverse=True)]


def tab_bottom(index: dict, tab: str | None) -> int | None:
    """The lowest edge of anything on a tab (boards and notes), or None for an empty tab."""
    ys = [b["y"] + b["h"] for b in index.get("boards", {}).values() if tab_of(index, b) == tab]
    ys += [n["y"] + (120 if n.get("kind") == "title1" else int(n.get("w", 300) * 4 / 3))
           for n in index.get("notes", {}).values() if tab_of(index, n) == tab and "y" in n]
    return max(ys) if ys else None


def slug(name: str) -> str:
    out = re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")
    return (out or "tab")[:40]


def boards_by_page(index: dict) -> dict:
    out: dict = {}
    for name, b in index.get("boards", {}).items():
        out.setdefault(tab_of(index, b), []).append((b.get("y", 0), b.get("x", 0), name, b))
    for v in out.values():
        v.sort()
    return out


def doc_headings() -> list[tuple[int, str]]:
    if not DOC.exists():
        return []
    out = []
    for i, line in enumerate(DOC.read_text(encoding="utf-8").splitlines(), 1):
        if line.startswith("## ") or line.startswith("### "):
            out.append((i, line))
    return out


def open_rounds() -> list[str]:
    """Doc headings of rounds still waiting on the person: a pick or a board approval."""
    return [h for _, h in doc_headings() if re.search(r"awaiting (a pick|board approval|a decision)", h)]


# ----------------------------------------------------------------------------------------------- status / find

def print_tabs(idx: dict) -> None:
    per_tab: dict = {}
    for name, b in idx.get("boards", {}).items():
        per_tab[tab_of(idx, b)] = per_tab.get(tab_of(idx, b), 0) + 1
    rs = rounds(idx)
    for p in idx.get("pages", []):
        mine = [r for r, t, _ in rs if t == p["id"]]
        latest = f"  latest: {round_label(idx, mine[0])}" if mine else ""
        print(f"    {p['id']:>16}  {p['name']}  ({per_tab.get(p['id'], 0)} boards, {len(mine)} rounds){latest}")


def cmd_status(args) -> int:
    repo_idx = load_index(COPY_DIR / "canvas.json") if (COPY_DIR / "canvas.json").exists() else {"boards": {}, "pages": []}
    live_idx = load_index(Path(args.live)) if args.live else None
    idx = live_idx or repo_idx
    print(f"Design board: {BOARD_URL}")
    print(f"  title: {idx.get('title')}   launch tab: {idx.get('launch', {}).get('page', '(canvas)')}")
    print(f"  tabs: {len(idx.get('pages', []))} of {MAX_TABS}   boards: {len(idx.get('boards', {}))}   next free round: Rev {next_rev(idx)}")
    if args.version:
        print(f"  live version read: {args.version}")
    print("  tabs:")
    print_tabs(idx)
    print("  latest rounds:")
    for r, t, names in rounds(idx)[:4]:
        print(f"    Rev {r:<3} on {page_name(idx, t)!r}: {round_label(idx, r)}  ({len(names)} boards)")
    if live_idx is not None:
        new_pages = [p for p in live_idx.get("pages", []) if p["id"] not in {q["id"] for q in repo_idx.get("pages", [])}]
        new_boards = sorted(set(live_idx.get("boards", {})) - set(repo_idx.get("boards", {})))
        gone = sorted(set(repo_idx.get("boards", {})) - set(live_idx.get("boards", {})))
        moved = sorted(k for k in live_idx.get("boards", {}) if k in repo_idx.get("boards", {}) and live_idx["boards"][k] != repo_idx["boards"][k])
        print(f"  live vs repo copy ({rel(COPY_DIR)}):")
        print(f"    new tabs: {[p['id'] for p in new_pages] or 'none'}")
        print(f"    new boards: {len(new_boards)} {new_boards[:12]}{' ...' if len(new_boards) > 12 else ''}")
        print(f"    boards removed on the canvas: {gone or 'none'}   entries moved or retitled: {len(moved)}")
        missing_files = [b for b in live_idx.get("boards", {}) if not (COPY_DIR / b).exists()]
        if missing_files:
            print(f"    board files the repo copy lacks ({len(missing_files)}), read them with the Artifact tool (paths project/<name>) and run pull:")
            for b in missing_files:
                print(f"      project/{b}")
        else:
            print("    every board file on the canvas is in the repo copy")
    rounds_open = open_rounds()
    print(f"  rounds waiting on a pick or approval, from {rel(DOC)} ({len(rounds_open)}):")
    for h in rounds_open[-8:]:
        print(f"    {h}")
    return 0


def cmd_tabs(args) -> int:
    idx = load_index(Path(args.live)) if args.live else load_index(COPY_DIR / "canvas.json")
    print(f"{idx.get('title')}: {len(idx.get('pages', []))} of {MAX_TABS} tabs, launch tab {idx.get('launch', {}).get('page', '(canvas)')}")
    print_tabs(idx)
    return 0


def cmd_find(args) -> int:
    idx = load_index(Path(args.live)) if args.live else load_index(COPY_DIR / "canvas.json")
    words = [w.lower() for w in args.words]
    hits = []
    for name, b in idx.get("boards", {}).items():
        hay = f"{name} {b.get('title', '')}".lower()
        if all(w in hay for w in words):
            r = rev_of_board(name)
            hits.append((r if r is not None else -1, b.get("y", 0), b.get("x", 0), name, b))
    hits.sort(key=lambda h: (-h[0], h[1], h[2]))
    print(f"boards about {' '.join(args.words)!r} ({len(hits)}), newest round first:")
    for _, _, _, name, b in hits:
        tag = " [interactive]" if b.get("is_interactive") else ""
        print(f"  {page_name(idx, tab_of(idx, b))} > {section_of(idx, name)[1]} | {name}{tag}\n      {b.get('title', '')}")
    doc_hits = [(i, h) for i, h in doc_headings() if all(w in h.lower() for w in words)]
    print(f"sections of {rel(DOC)} ({len(doc_hits)}), newest first:")
    for i, h in reversed(doc_hits):
        print(f"  L{i}: {h}")
    return 0


def cmd_next_rev(args) -> int:
    idx = load_index(Path(args.live)) if args.live else load_index(COPY_DIR / "canvas.json")
    n = next_rev(idx)
    src = "the live index" if args.live else "the REPO COPY (re-read the live index right before publishing)"
    print(f"Rev {n}  (title note rev{n}, board files R{n}<Key>.dc.html) - from {src}")
    return 0


# ----------------------------------------------------------------------------------------------- plan

def skeleton(title: str, w: int, h: int, fill: bool) -> str:
    root = ("width: 100%; min-height: 100vh" if fill else f"width: {w}px; height: {h}px") + \
        "; box-sizing: border-box; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; " \
        "font-family: 'Aldrich', 'Chakra Petch', sans-serif"
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{html.escape(title)}</title>
{SUPPORT_LINE}
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{{margin:0;background:#00010A}}
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.14em;text-transform:uppercase;color:#747BAD}}
.num{{font-variant-numeric:tabular-nums}}
</style>
</helmet>
<div style="{root}">
<span class="lab" style="position:absolute;left:64px;top:56px">{DRAFT_MARK}</span>
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
class Component extends DCLogic {{
renderVals() {{
return {{}};
}}
}}
</script>
</body>
</html>
"""


def add_tab(live: dict, name: str, tab_id: str | None = None, at: int | None = None) -> tuple[str | None, str]:
    """Adds a tab to the index in place. Returns (tab id, '') or (None, why it was refused)."""
    pages = live.setdefault("pages", [])
    name = name.strip()
    if not name:
        return None, "a tab needs a name"
    if any(p.get("name", "").strip().lower() == name.lower() for p in pages):
        return None, f"a tab named {name!r} exists already"
    if len(pages) >= MAX_TABS:
        return None, f"the canvas holds {len(pages)} tabs, its limit; fold old ones together first (`fold`)"
    tid = tab_id or slug(name)
    if not PAGE_ID_RE.match(tid):
        return None, f"tab id {tid!r} is not [A-Za-z0-9_-]{{1,40}}"
    taken = {p.get("id") for p in pages} | set(live.get("notes", {}))
    base, n = tid, 2
    while tid in taken:
        tid = f"{base[:36]}-{n}"
        n += 1
    entry = {"id": tid, "name": name}
    if at is None:
        pages.append(entry)
    else:
        pages.insert(at, entry)
    return tid, ""


def write_index(root: Path, live: dict) -> Path:
    (root / "project").mkdir(parents=True, exist_ok=True)
    out = root / "project" / "canvas.json"
    with open(out, "w", encoding="utf-8") as f:
        json.dump(live, f, indent=2, ensure_ascii=False)
        f.write("\n")
    return out


def only_ours_changed(original: dict, live: dict, boards: list[str], notes: list[str], tab: str | None, new_tab: bool) -> bool:
    check = json.loads(json.dumps(live))
    for f in boards:
        del check["boards"][f]
        check["order"].remove(f)
    for n in notes:
        del check["notes"][n]
    if new_tab:
        check["pages"] = [p for p in check["pages"] if p["id"] != tab]
    check["launch"] = original.get("launch", {})
    return check == original


def cmd_plan(args) -> int:
    live = load_index(Path(args.live))
    original = json.loads(json.dumps(live))
    rev = args.rev or next_rev(live)
    note_id = f"rev{rev}"
    if (any(p.get("id") == note_id for p in live.get("pages", [])) or note_id in live.get("notes", {})
            or any(rev_of_board(b) == rev for b in live.get("boards", {}))):
        print(f"ERROR: Rev {rev} is already taken on the canvas (page, title note or board prefix). Next free: Rev {next_rev(live)}", file=sys.stderr)
        return 1
    tab_ids = [p.get("id") for p in live.get("pages", [])]
    new_tab = False
    if args.new_tab:
        tab, why = add_tab(live, args.new_tab)
        if tab is None:
            print(f"ERROR: {why}", file=sys.stderr)
            return 1
        new_tab = True
    elif args.tab:
        tab = args.tab
        if tab not in tab_ids:
            listing = ", ".join(f"{q['id']} ({q.get('name', '')})" for q in live.get("pages", []))
            print(f"ERROR: no tab {tab!r}; tabs: {listing}", file=sys.stderr)
            return 1
    else:
        print("ERROR: say which tab the round goes on: --tab <id> (see `tabs`), or --new-tab \"<name>\"", file=sys.stderr)
        return 1
    w, h = (int(v) for v in args.size.lower().split("x"))
    interactive = {k.strip() for k in (args.interactive or "").split(",") if k.strip()}

    specs: list[tuple[str, str, bool, int]] = []  # (file, title, interactive, height)
    if args.compare:
        specs.append((f"R{rev}Compare.dc.html", f"Rev {rev} - {args.compare}", False, h))
    for spec in args.board or []:
        if "=" not in spec:
            print(f"ERROR: --board wants KEY=\"title\", got {spec!r}", file=sys.stderr)
            return 1
        key, title = spec.split("=", 1)
        key = key.strip()
        if not re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_-]*", key):
            print(f"ERROR: board key {key!r} must start with a letter, digit or _ and hold only those, - and _", file=sys.stderr)
            return 1
        is_i = key in interactive
        full = f"Rev {rev} {key} - {title.strip()}" + (" - interactive" if is_i else "")
        specs.append((f"R{rev}{key}.dc.html", full, is_i, h))
    unknown = interactive - {s.split("=", 1)[0].strip() for s in (args.board or [])}
    if unknown:
        print(f"ERROR: --interactive names boards not given: {sorted(unknown)}", file=sys.stderr)
        return 1
    if not specs:
        print("ERROR: a round needs at least one --board or --compare", file=sys.stderr)
        return 1
    notes_spec = None
    if not args.no_notes:
        notes_title = args.notes or "notes: what each option changes, what is shared, what differs from the build"
        notes_spec = (f"R{rev}Notes.dc.html", f"Rev {rev} - {notes_title}", False, int(args.notes_h))

    for file, *_ in specs + ([notes_spec] if notes_spec else []):
        if file in live.get("boards", {}):
            print(f"ERROR: {file} already exists on the canvas", file=sys.stderr)
            return 1

    # The round is a titled section under everything already on the tab.
    bottom = tab_bottom(live, tab)
    top = 0 if bottom is None else bottom + ROUND_GAP + TITLE_ABOVE
    boards = live.setdefault("boards", {})
    order = live.setdefault("order", [])
    label = args.name if args.name.lower().startswith("rev") else f"Rev {rev} - {args.name}"
    row_w = PER_ROW * w + (PER_ROW - 1) * GAP_X
    live.setdefault("notes", {})[note_id] = {"kind": "title1", "text": label, "x": 0, "y": top - TITLE_ABOVE,
                                             "w": 240, "maxW": row_w, "page": tab}
    placed = []
    for i, (file, title, is_i, bh) in enumerate(specs):
        col, row = i % PER_ROW, i // PER_ROW
        entry = {"x": col * (w + GAP_X), "y": top + row * (h + GAP_Y), "w": w, "h": bh, "page": tab, "title": title}
        if is_i:
            entry["is_interactive"] = True
        boards[file] = entry
        order.append(file)
        placed.append((file, entry))
    if notes_spec:
        file, title, _, nh = notes_spec
        rows = (len(specs) + PER_ROW - 1) // PER_ROW
        entry = {"x": 0, "y": top + rows * (h + GAP_Y), "w": w, "h": nh, "page": tab, "title": title}
        boards[file] = entry
        order.append(file)
        placed.append((file, entry))
    live.setdefault("launch", {})["view"] = "canvas"
    live["launch"]["page"] = tab

    if not only_ours_changed(original, live, [f for f, _ in placed], [note_id], tab, new_tab):
        print("ERROR: the plan changed a key that is not ours; refusing to write", file=sys.stderr)
        return 1

    root = Path(args.root)
    out = write_index(root, live)
    print(f"Rev {rev}: {label!r} on tab {tab!r} ({page_name(live, tab)}){' (new tab)' if new_tab else ''}, "
          f"title note {note_id!r} at y={top - TITLE_ABOVE}; launch opens on the tab. Index written to {out}")
    print("Boards to write (each its WHOLE .dc.html; $preview and the root's size must equal w x h):")
    for file, e in placed:
        print(f"  project/{file:<28} x={e['x']:<5} y={e['y']:<6} {e['w']}x{e['h']}{'  interactive' if e.get('is_interactive') else ''}\n      {e['title']}")
        if args.skeletons:
            target = root / "project" / file
            if not target.exists():
                target.write_text(skeleton(e["title"], e["w"], e["h"], False), encoding="utf-8")
    if args.skeletons:
        print(f"Skeletons written under {root / 'project'} (each carries {DRAFT_MARK!r}; `check` fails until it is replaced).")
    print("Publish: Artifact publish  url=<board>  root=<root>  file_path=<root>/project/canvas.json  files={\"project/<file>\": \"project/<file>\", ...}")
    return 0


def cmd_new_tab(args) -> int:
    live = load_index(Path(args.live))
    original = json.loads(json.dumps(live))
    tab, why = add_tab(live, args.name, args.id)
    if tab is None:
        print(f"ERROR: {why}", file=sys.stderr)
        return 1
    live.setdefault("launch", {})["view"] = "canvas"
    live["launch"]["page"] = tab
    if not only_ours_changed(original, live, [], [], tab, True):
        print("ERROR: adding the tab changed a key that is not ours; refusing to write", file=sys.stderr)
        return 1
    out = write_index(Path(args.root), live)
    print(f"Tab {tab!r} ({args.name.strip()!r}) added as tab {len(live['pages'])} of {MAX_TABS}; the canvas opens on it. Index written to {out}")
    print("Publish ONLY the index: Artifact publish  url=<board>  root=<root>  file_path=<root>/project/canvas.json")
    print(f"Its first round: plan --tab {tab} ...")
    return 0


def cmd_fold(args) -> int:
    """Moves whole pages into one tab, each as a titled section, packed newest-first on shelves, and drops the pages.
    Boards and notes keep their files, titles and relative layout; only x, y and page change."""
    live = load_index(Path(args.live))
    pages = live.get("pages", [])
    ids = [p.get("id") for p in pages]
    want = [x.strip() for x in args.pages.split(",") if x.strip()]
    missing = [x for x in want if x not in ids]
    if missing or not want:
        print(f"ERROR: no such pages {missing}; pages: {ids}", file=sys.stderr)
        return 1
    if len(set(want)) != len(want):
        print("ERROR: a page is named twice", file=sys.stderr)
        return 1
    first = first_page(live)
    names = {p["id"]: p.get("name", p["id"]) for p in pages}
    target = args.into
    existing_target = target in ids and target not in want
    if not PAGE_ID_RE.match(target):
        print(f"ERROR: tab id {target!r} is not [A-Za-z0-9_-]{{1,40}}", file=sys.stderr)
        return 1
    # Measure each page's content (page-less boards and notes belong to the first page).
    sections = []
    for pid in want:
        bs = [k for k, b in live.get("boards", {}).items() if (b.get("page") or first) == pid]
        ns = [k for k, n in live.get("notes", {}).items() if (n.get("page") or first) == pid and "x" in n and "y" in n]
        xs = [live["boards"][k]["x"] for k in bs] + [live["notes"][k]["x"] for k in ns]
        ys = [live["boards"][k]["y"] for k in bs] + [live["notes"][k]["y"] for k in ns]
        x2 = [live["boards"][k]["x"] + live["boards"][k]["w"] for k in bs] + [live["notes"][k]["x"] + live["notes"][k].get("maxW", live["notes"][k].get("w", 300)) for k in ns]
        y2 = [live["boards"][k]["y"] + live["boards"][k]["h"] for k in bs] + [live["notes"][k]["y"] + 120 for k in ns]
        if not xs:
            sections.append((pid, bs, ns, 0, 0, 0, 0))
            continue
        sections.append((pid, bs, ns, min(xs), min(ys), max(x2) - min(xs), max(y2) - min(ys)))
    # Pack the sections on shelves under whatever the target tab already holds.
    bottom = tab_bottom(live, target) if existing_target else None
    cur_y = 0 if bottom is None else bottom + SHELF_GAP_Y
    cur_x, shelf_h = 0, 0
    moves = {}
    for pid, bs, ns, x0, y0, w, h in sections:
        if cur_x and cur_x + w > SHELF_W:
            cur_x, cur_y, shelf_h = 0, cur_y + shelf_h + SHELF_GAP_Y, 0
        moves[pid] = (cur_x - x0, cur_y + TITLE_ABOVE - y0, cur_x, cur_y, w)
        cur_x += w + SHELF_GAP_X
        shelf_h = max(shelf_h, h + TITLE_ABOVE)
    for pid, bs, ns, *_ in sections:
        dx, dy, sx, sy, w = moves[pid]
        for k in bs:
            e = live["boards"][k]
            e["x"], e["y"], e["page"] = e["x"] + dx, e["y"] + dy, target
        for k in ns:
            e = live["notes"][k]
            e["x"], e["y"], e["page"] = e["x"] + dx, e["y"] + dy, target
        nid = pid if pid not in live.get("notes", {}) else f"sec-{pid}"[:40]
        live.setdefault("notes", {})[nid] = {"kind": "title1", "text": names[pid], "x": sx, "y": sy,
                                            "w": 240, "maxW": max(1920, w), "size": "xxl", "page": target}
    # The pages go; the target tab takes the place of the earliest one folded (or keeps its own).
    at = min(ids.index(x) for x in want)
    live["pages"] = [p for p in pages if p.get("id") not in want]
    if not existing_target:
        if target in [p.get("id") for p in live["pages"]]:
            print(f"ERROR: tab id {target!r} is taken", file=sys.stderr)
            return 1
        at = min(at, len(live["pages"]))
        live["pages"].insert(at, {"id": target, "name": args.name})
    elif args.name:
        for p in live["pages"]:
            if p["id"] == target:
                p["name"] = args.name
    lp = live.get("launch", {}).get("page")
    if lp in want:
        live["launch"]["page"] = target
    # Nothing on another page may still name a folded page, and nothing may be lost.
    left = [k for k, b in {**live.get("boards", {}), **live.get("notes", {})}.items() if b.get("page") in want]
    if left:
        print(f"ERROR: still on a folded page: {left[:5]}", file=sys.stderr)
        return 1
    if args.title:
        live["title"] = args.title
    out = write_index(Path(args.root), live)
    print(f"Folded {len(want)} page(s) into tab {target!r} ({page_name(live, target)}): "
          f"{sum(len(s[1]) for s in sections)} boards, {sum(len(s[2]) for s in sections)} notes moved; "
          f"{len(live['pages'])} of {MAX_TABS} tabs now. Index written to {out}")
    for pid, *_ in sections:
        dx, dy, sx, sy, w = moves[pid]
        print(f"  {pid:>10}  {names[pid]!r}  section at x={sx} y={sy} (width {w})")
    return 0


# ----------------------------------------------------------------------------------------------- check

NODE_HARNESS = r"""
'use strict';
const fs = require('fs');
const job = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const out = { errors: [], warnings: [], info: [] };
const finish = () => { process.stdout.write(JSON.stringify(out)); process.exit(0); };
// A browser-less stand-in for the canvas runtime: timers never fire, the DOM is absent.
globalThis.window = globalThis;
globalThis.document = { querySelector: () => null, querySelectorAll: () => [], getElementById: () => null, body: {}, documentElement: {}, addEventListener() {}, removeEventListener() {}, createElement: () => ({ style: {}, setAttribute() {}, appendChild() {} }) };
try { Object.defineProperty(globalThis, 'navigator', { value: { userAgent: 'design_board check' }, configurable: true }); } catch (e) {}
globalThis.requestAnimationFrame = () => 1; globalThis.cancelAnimationFrame = () => {};
globalThis.setTimeout = () => 1; globalThis.setInterval = () => 1; globalThis.clearTimeout = () => {}; globalThis.clearInterval = () => {};
globalThis.localStorage = { getItem: () => null, setItem() {}, removeItem() {} };
class DCLogic {
  constructor(props) { this.props = props || {}; this.state = {}; }
  setState(o) { if (typeof o === 'function') o = o(this.state, this.props); Object.assign(this.state, o || {}); }
  forceUpdate() {}
}
let Component;
try { Component = new Function('DCLogic', job.script + '\nreturn Component;')(DCLogic); }
catch (e) { out.errors.push('logic block does not compile: ' + e.message); finish(); }
if (typeof Component !== 'function') { out.errors.push('logic block defines no `class Component extends DCLogic`'); finish(); }
let c;
try { c = new Component(job.defaults); } catch (e) { out.errors.push('constructor throws: ' + e.message); finish(); }
if (typeof c.renderVals !== 'function') { out.errors.push('Component has no renderVals()'); finish(); }
if (typeof c.componentDidMount === 'function') { try { c.componentDidMount(); } catch (e) { out.warnings.push('componentDidMount throws without a browser: ' + e.message); } }
const render = (why) => { try { return c.renderVals(); } catch (e) { out.errors.push('renderVals() throws ' + why + ': ' + e.message); return null; } };
let vals = render('at rest');
if (vals === null) finish();
if (typeof vals !== 'object') { out.errors.push('renderVals() returned ' + typeof vals + ', not an object'); finish(); }
const lookup = (obj, path) => { let v = obj; for (const seg of path.split('.')) { if (v === null || v === undefined) return undefined; v = v[seg]; } return v; };
// Loop scopes: every item of every enclosing <sc-for> list is a candidate scope (items differ: a locked tile has no
// bars, an open seat has no pilot), capped at 64 per level. A hole passes when ANY candidate resolves its head key.
const expand = (scopes) => {
  let cands = [vals];
  for (const [as, listPath] of scopes) {
    const next = []; let sawValue = false, sawArray = false;
    for (const sc of cands) {
      const list = lookup(sc, listPath) ?? lookup(vals, listPath);
      if (list !== undefined && list !== null) sawValue = true;
      if (!Array.isArray(list)) continue;
      sawArray = true;
      list.slice(0, 64).forEach((it, i) => next.push(Object.assign({}, sc, { [as]: it, $index: i })));
    }
    if (!next.length) return { cands: [], empty: !sawValue || sawArray, listPath };
    cands = next;
  }
  return { cands, empty: false, listPath: null };
};
// Holes: each {{a.b}} must resolve from renderVals(), or from an item of the enclosing <sc-for> lists.
const emptyNoted = new Set();
for (const hole of job.holes) {
  const head = hole.path.split('.')[0];
  if (head === '$index') continue;
  const ex = expand(hole.scopes);
  if (!ex.cands.length) {
    if (ex.empty) { if (!emptyNoted.has(ex.listPath)) { emptyNoted.add(ex.listPath); out.info.push('list ' + ex.listPath + ' is empty at rest; its holes are unchecked'); } }
    else out.errors.push(hole.path + ': <sc-for list="{{' + ex.listPath + '}}"> is not an array at rest');
    continue;
  }
  if (!ex.cands.some((sc) => head in sc)) { out.errors.push('{{' + hole.path + '}} has no key ' + head + ' in renderVals()' + (hole.scopes.length ? ' or any item of its loop' : '')); continue; }
  if (hole.path.includes('.') && ex.cands.every((sc) => lookup(sc, hole.path) === undefined)) out.warnings.push('{{' + hole.path + '}} is undefined on every item at rest');
}
// Press every handler the markup shows: a handler inside <sc-if value="{{x}}"> waits until x is truthy, so the
// presses follow the UI's own order (make, then pick, then create), and a `create` that read a draft it had just
// cleared (Rev 31) is caught without pressing buttons the viewer could not see.
const ev = { preventDefault() {}, stopPropagation() {}, target: { value: '', checked: false }, currentTarget: { value: '' }, key: 'Enter', clientX: 0, clientY: 0 };
const truthy = (cond) => { if (/^(true|false|null|-?\d+(\.\d+)?|'[^']*'|"[^"]*")$/.test(cond.path)) return cond.path !== 'false' && cond.path !== 'null' && cond.path !== '0'; return expand(cond.scopes).cands.some((sc) => !!lookup(sc, cond.path)); };
const pressed = new Set();
let pressedCount = 0, hidden = 0;
for (let round = 0; round < 12; round++) {
  let progress = false;
  for (const h of job.handlers) {
    const id = h.path + '|' + JSON.stringify(h.scopes);
    if (pressed.has(id)) continue;
    if (!h.conds.every(truthy)) continue;
    const cands = expand(h.scopes).cands;
    if (!cands.length) continue;
    const sc = cands.find((x) => typeof lookup(x, h.path) === 'function') || cands[0];
    const fn = lookup(sc, h.path);
    if (typeof fn !== 'function') { if (cands.some((x) => lookup(x, h.path) !== undefined) || !h.scopes.length) out.errors.push('on*="{{' + h.path + '}}" is not a function from renderVals()' + (h.scopes.length ? ' (loop item)' : '')); pressed.add(id); continue; }
    pressed.add(id); pressedCount++; progress = true;
    try { fn.call(c, ev); } catch (e) { out.errors.push('handler ' + h.path + ' throws when pressed: ' + e.message); continue; }
    const v = render('after ' + h.path); if (v) vals = v;
  }
  if (!progress) break;
}
for (const h of job.handlers) { const id = h.path + '|' + JSON.stringify(h.scopes); if (!pressed.has(id)) hidden++; }
if (hidden) out.info.push(hidden + ' handler(s) never became visible from the pressed states');
out.info.push('handlers pressed: ' + pressedCount);
finish();
"""


class Report:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def err(self, where: str, msg: str) -> None:
        self.errors.append(f"{where}: {msg}")

    def warn(self, where: str, msg: str) -> None:
        self.warnings.append(f"{where}: {msg}")


def parse_props(raw: str, where: str, rep: Report) -> dict:
    try:
        props = json.loads(html.unescape(raw))
    except json.JSONDecodeError as e:
        rep.err(where, f"data-props is not JSON after HTML-unescaping ({e.msg}); write &amp; &#39; and \\\" inside it")
        return {}
    if not isinstance(props, dict):
        rep.err(where, "data-props must be a JSON object")
        return {}
    return props


def markup_scopes(src: str):
    """For every {{hole}} and every on*="{{handler}}" binding: the <sc-for> loops it sits in ([as, listPath], outermost
    first) and the <sc-if> conditions above it (each a hole path, with its own loop scopes). Approximate but nesting-aware."""
    events = []
    for m in re.finditer(r"<sc-for\b([^>]*)>", src):
        attrs = m.group(1)
        lm = re.search(r'list="\{\{\s*([^}]+?)\s*\}\}"', attrs)
        am = re.search(r'as="([^"]+)"', attrs)
        events.append((m.start(), "open", ("for", am.group(1) if am else "item", lm.group(1).strip() if lm else "")))
    for m in re.finditer(r"<sc-if\b([^>]*)>", src):
        vm = re.search(r'value="\{\{\s*([^}]+?)\s*\}\}"', m.group(1))
        events.append((m.start(), "open", ("if", vm.group(1).strip() if vm else "true", None)))
    for m in re.finditer(r"</sc-(for|if)\s*>", src):
        events.append((m.start(), "close", (m.group(1), None, None)))
    events.sort()

    def scopes_at(pos: int, stack: list, ei: int):
        while ei < len(events) and events[ei][0] < pos:
            kind, payload = events[ei][1], events[ei][2]
            if kind == "open":
                if payload[0] == "for":
                    stack.append(("for", payload[1], payload[2]))
                else:
                    stack.append(("if", payload[1], [list(f) for f in loops(stack)]))
            else:
                for i in range(len(stack) - 1, -1, -1):
                    if stack[i][0] == payload[0]:
                        del stack[i]
                        break
            ei += 1
        return ei

    def loops(stack):
        return [(f[1], f[2]) for f in stack if f[0] == "for"]

    def conds(stack):
        return [{"path": f[1], "scopes": f[2]} for f in stack if f[0] == "if"]

    holes, handlers = [], []
    stack: list = []
    ei = 0
    items = [(m.start(), "hole", m.group(1).strip()) for m in HOLE_RE.finditer(src)]
    items += [(m.start(), "handler", m.group(1).strip()) for m in re.finditer(r'\bon[A-Z]\w*="\{\{\s*([^}]+?)\s*\}\}"', src)]
    items.sort()
    for pos, kind, path in items:
        ei = scopes_at(pos, stack, ei)
        rec = {"path": path, "scopes": [list(f) for f in loops(stack)], "conds": conds(stack)}
        (holes if kind == "hole" else handlers).append(rec)
    return holes, handlers


def hole_scopes(src: str) -> list[dict]:
    return markup_scopes(src)[0]


def check_board(dir_: Path, name: str, entry: dict | None, rep: Report, run_node: bool = True) -> None:
    where = name
    for seg in name.split("/"):
        if not SEGMENT_RE.match(seg) or seg in ("..", "."):
            rep.err(where, f"path segment {seg!r} must start with a letter, digit or _ and hold only those, . and -")
    if not name.endswith(".dc.html"):
        rep.err(where, "a board file ends in .dc.html")
    path = dir_ / name
    if not path.exists():
        rep.err(where, "listed in canvas.json but the file is missing: write it or take it out of boards and order")
        return
    src = path.read_text(encoding="utf-8", errors="replace")
    if not re.match(r"\s*<!doctype html>", src, re.I):
        rep.err(where, "must start with <!doctype html>")
    if src.count(SUPPORT_LINE) != 1:
        rep.err(where, f"the head must hold exactly one {SUPPORT_LINE}")
    if "<x-dc>" not in src or "</x-dc>" not in src:
        rep.err(where, "all UI goes inside one <x-dc>...</x-dc>")
    if "<helmet>" not in src:
        rep.warn(where, "no <helmet> (page basics: body{margin:0}, fonts, a/a:hover colours)")
    if DRAFT_MARK in src:
        rep.err(where, "still a skeleton: the draft marker is in it")
    for tag in ("<iframe", "<object", "<embed"):
        if tag in src.lower():
            rep.err(where, f"{tag}> is not allowed on a board")
    if re.search(r"""(src|href)\s*=\s*["']data:|url\(\s*["']?data:""", src, re.I):
        rep.err(where, "a data: URI; upload the file to the canvas (asset: true) and use its /_blob/<id> url")
    for bad in ("innerHTML", "appendChild(", "document.createElement"):
        if bad in src:
            rep.err(where, f"{bad}: UI is <x-dc> markup, never script-built")
    if re.search(r"(window|document)\.addEventListener\(\s*['\"]key", src):
        rep.err(where, "a global key handler; bind onKeyDown on the element instead")
    em = EMOJI_RE.search(src)
    if em:
        rep.err(where, f"emoji {em.group(0)!r}; icons are inline stroke SVG")
    dm = DINGBAT_RE.search(src)
    if dm:
        rep.warn(where, f"dingbat {dm.group(0)!r}; make sure it is a glyph the fonts draw, not an emoji")
    m = re.search(r"""<script type="text/x-dc" data-dc-script data-props='(.*?)'>(.*?)</script>""", src, re.S)
    if not m:
        rep.err(where, "no <script type=\"text/x-dc\" data-dc-script data-props='...'> block (single-quoted data-props)")
        return
    props = parse_props(m.group(1), where, rep)
    script = m.group(2)
    if "class Component extends DCLogic" not in script:
        rep.err(where, "the logic block must define `class Component extends DCLogic`")
    if re.search(r"^\s*(import|export)\b", script, re.M):
        rep.err(where, "no import/export in the logic block (classic JS)")
    preview = props.get("$preview") if isinstance(props, dict) else None
    if not isinstance(preview, dict) or "width" not in preview or "height" not in preview:
        rep.err(where, 'data-props needs "$preview":{"width":W,"height":H}')
    elif entry is not None and (preview.get("width"), preview.get("height")) != (entry.get("w"), entry.get("h")):
        rep.err(where, f"$preview {preview.get('width')}x{preview.get('height')} != canvas.json frame {entry.get('w')}x{entry.get('h')}")
    # The root element right after </helmet>.
    body = src.split("</helmet>", 1)[1] if "</helmet>" in src else src.split("<x-dc>", 1)[-1]
    rm = re.search(r"<(\w+)\b([^>]*)>", body)
    if rm and entry is not None:
        style = re.search(r'style="([^"]*)"', rm.group(2))
        st = style.group(1) if style else ""
        wm = re.search(r"(?<![\w-])width:\s*(\d+)px", st)
        hm = re.search(r"(?<![\w-])height:\s*(\d+)px", st)
        mh = re.search(r"min-height:\s*(\d+)px", st)
        if entry.get("expand") == "fill":
            if wm:
                rep.warn(where, "a PAGE (expand: fill) has a px root width; a page is fluid")
        else:
            if not wm or not (hm or mh):
                rep.err(where, "the root element needs inline `width: Wpx; height: Hpx` equal to its frame")
            else:
                if int(wm.group(1)) != entry.get("w"):
                    rep.err(where, f"root width {wm.group(1)} != canvas.json frame width {entry.get('w')}")
                if hm and int(hm.group(1)) != entry.get("h"):
                    rep.err(where, f"root {wm.group(1)}x{hm.group(1)} != canvas.json frame {entry.get('w')}x{entry.get('h')}")
                elif not hm and int(mh.group(1)) != entry.get("h"):
                    rep.warn(where, f"root min-height {mh.group(1)} != frame height {entry.get('h')}; the frame should be the board's height at rest")
    # Holes are dotted lookups or literals, never expressions.
    holes = hole_scopes(src)
    for hd in holes:
        p = hd["path"]
        if not (PATH_RE.match(p) or LITERAL_RE.match(p)):
            rep.err(where, f"{{{{{p}}}}} is an expression; compute it in renderVals() and bind the name")
    for sm in re.finditer(r"<sc-(for|if)\b([^>]*)>", src):
        if "hint-" not in sm.group(2):
            rep.warn(where, f"<sc-{sm.group(1)}> without a hint-* attribute (renders while values stream in)")
    if re.search(r"<[A-Z][A-Za-z]*\s*/?>", body):
        rep.err(where, "a capitalised tag; child components are <dc-import name=...></dc-import>")
    if re.search(r"<dc-import\b[^>]*/>", src):
        rep.err(where, "<dc-import> is never self-closed")
    if not run_node:
        return
    node = shutil.which("node")
    if not node:
        rep.warn(where, "node not found: the logic block was not run")
        return
    defaults = {k: v.get("default") for k, v in props.items() if isinstance(v, dict) and k != "$preview" and "default" in v}
    holes, handlers = markup_scopes(src)
    job = {"script": script, "defaults": defaults,
           "holes": [h for h in holes if PATH_RE.match(h["path"]) and not LITERAL_RE.match(h["path"])],
           "handlers": [h for h in handlers if PATH_RE.match(h["path"])]}
    with tempfile.TemporaryDirectory() as td:
        jp = Path(td) / "job.json"
        hp = Path(td) / "harness.js"
        jp.write_text(json.dumps(job), encoding="utf-8")
        hp.write_text(NODE_HARNESS, encoding="utf-8")
        try:
            r = subprocess.run([node, str(hp), str(jp)], capture_output=True, text=True, timeout=60)
        except subprocess.TimeoutExpired:
            rep.err(where, "the logic block ran for 60 s without finishing (a loop that never ends?)")
            return
    if r.returncode != 0 or not r.stdout.strip():
        rep.err(where, f"the logic harness died: {r.stderr.strip()[:300]}")
        return
    res = json.loads(r.stdout)
    for e in res["errors"]:
        rep.err(where, e)
    for w in res["warnings"]:
        rep.warn(where, w)


def check_index(dir_: Path, idx: dict, only: str | None, rep: Report) -> None:
    where = "canvas.json"
    if idx.get("v") != 3:
        rep.err(where, f"v must be 3, is {idx.get('v')!r}")
    boards = idx.get("boards", {})
    order = idx.get("order", [])
    if sorted(order) != sorted(boards):
        extra = sorted(set(order) - set(boards))
        missing = sorted(set(boards) - set(order))
        rep.err(where, f"order must list every board once: not in boards {extra}, not in order {missing}")
    if len(set(order)) != len(order):
        rep.err(where, "order repeats a path")
    page_ids = []
    for p in idx.get("pages", []):
        pid = p.get("id", "")
        page_ids.append(pid)
        if not PAGE_ID_RE.match(pid):
            rep.err(where, f"page id {pid!r} is not [A-Za-z0-9_-]{{1,40}}")
        if not p.get("name"):
            rep.err(where, f"page {pid!r} has no name")
    if len(set(page_ids)) != len(page_ids):
        rep.err(where, "a page id repeats")
    if len(page_ids) > MAX_TABS:
        rep.err(where, f"{len(page_ids)} tabs (pages); the canvas allows {MAX_TABS}: fold old ones together (`fold`)")
    lp = idx.get("launch", {}).get("page")
    if lp and lp not in page_ids:
        rep.err(where, f"launch.page {lp!r} is not a page")
    stems = {}
    for name, b in boards.items():
        for k in ("x", "y", "w", "h"):
            if not isinstance(b.get(k), (int, float)):
                rep.err(where, f"{name}: {k} missing or not a number")
        if isinstance(b.get("w"), (int, float)) and not 40 <= b["w"] <= 8000:
            rep.err(where, f"{name}: w {b['w']} outside 40-8000")
        if isinstance(b.get("h"), (int, float)) and not 40 <= b["h"] <= 8000:
            rep.err(where, f"{name}: h {b['h']} outside 40-8000")
        if b.get("page") is not None and b["page"] not in page_ids:
            rep.err(where, f"{name}: page {b['page']!r} is not a page")
        stem = name.rsplit("/", 1)[-1][: -len(".dc.html")] if name.endswith(".dc.html") else name
        if stem in stems:
            rep.err(where, f"{name} and {stems[stem]} share the stem {stem!r}; stems are unique")
        stems[stem] = name
    for n, note in idx.get("notes", {}).items():
        if not PAGE_ID_RE.match(n):
            rep.err(where, f"note id {n!r} is not [A-Za-z0-9_-]{{1,40}}")
        if note.get("kind") == "link":
            if "href" not in note or "text" in note:
                rep.err(where, f"note {n!r}: a link card has href and NO text")
        elif not all(k in note for k in ("x", "y", "text")):
            rep.err(where, f"note {n!r} needs x, y and text")
        if note.get("page") is not None and note["page"] not in page_ids:
            rep.err(where, f"note {n!r}: page {note['page']!r} is not a page")
    # Files on disk that the index does not list still show on the canvas.
    for f in sorted(dir_.glob("*.dc.html")):
        if f.name not in boards and not (dir_ / "ds").exists():
            rep.warn("files", f"{f.name} is on disk but not in canvas.json; every .dc.html shows, so list it (or do not publish it)")
    # New boards must not sit on another frame of their page.
    mine = [n for n in boards if (only is None or n.startswith(only))]
    for n in mine:
        a = boards[n]
        for o, b in boards.items():
            if o == n or b.get("page") != a.get("page"):
                continue
            if all(isinstance(v.get(k), (int, float)) for v in (a, b) for k in ("x", "y", "w", "h")):
                overlap = a["x"] < b["x"] + b["w"] and b["x"] < a["x"] + a["w"] and a["y"] < b["y"] + b["h"] and b["y"] < a["y"] + a["h"]
                if overlap:
                    msg = f"{n} overlaps {o} on page {a.get('page')!r}"
                    if only is None:
                        rep.warn(where, msg)
                    else:
                        rep.err(where, msg)


def cmd_check(args) -> int:
    dir_ = Path(args.dir)
    if (dir_ / "project" / "canvas.json").exists():
        dir_ = dir_ / "project"
    idx_path = dir_ / "canvas.json"
    if not idx_path.exists():
        print(f"ERROR: no canvas.json in {dir_}", file=sys.stderr)
        return 2
    rep = Report()
    try:
        idx = load_index(idx_path)
    except json.JSONDecodeError as e:
        print(f"canvas.json: not JSON ({e.msg} line {e.lineno})")
        return 1
    check_index(dir_, idx, args.only, rep)
    names = [n for n in idx.get("boards", {}) if args.only is None or n.startswith(args.only)]
    if args.only is not None and not names:
        rep.err("check", f"no board starts with {args.only!r}")
    for n in names:
        if args.only is None and not (dir_ / n).exists():
            rep.warn(n, "listed in canvas.json but not on disk here (read it from the canvas, or run pull)")
            continue
        check_board(dir_, n, idx["boards"][n], rep, run_node=not args.no_node)
    for w in rep.warnings:
        print(f"warn  {w}")
    for e in rep.errors:
        print(f"FAIL  {e}")
    scope = f"{len(names)} board(s)" + (f" starting with {args.only}" if args.only else "")
    if rep.errors or (args.strict and rep.warnings):
        print(f"design board check: {len(rep.errors)} failure(s), {len(rep.warnings)} warning(s) on {scope}")
        return 1
    print(f"design board check: OK on {scope} ({len(rep.warnings)} warning(s))")
    return 0


# ----------------------------------------------------------------------------------------------- pull / readme

def render_readme(idx: dict, copy_dir: Path, version: str | None, date: str) -> str:
    lines = [README_START, ""]
    ver = f"canvas version `{version}`" if version else "the canvas"
    rs = rounds(idx)
    span = f"Rev {rs[-1][0]} to Rev {rs[0][0]}, " if rs else ""
    lines.append(f"Copied on {date} from {ver} of **{idx.get('title', '')}**: {span}{len(idx.get('boards', {}))} boards on "
                 f"{len(idx.get('pages', []))} tabs. The canvas opens on the tab `{idx.get('launch', {}).get('page', 'canvas')}`. "
                 "Refresh with `python3 .claude/skills/design-board/design_board.py pull --src <dir the Artifact read saved> --version <v>`.")
    lines.append("")
    lines.append("## Tabs, rounds and boards (newest first)")
    lines.append("")
    for p in idx.get("pages", []):
        pid = p["id"]
        groups: dict = {}
        for name, b in idx.get("boards", {}).items():
            if tab_of(idx, b) != pid:
                continue
            sid, label = section_of(idx, name)
            g = groups.setdefault(sid or label, {"label": label, "rev": -1, "entries": []})
            r = rev_of_board(name)
            g["rev"] = max(g["rev"], r if r is not None else -1)
            g["entries"].append((b.get("y", 0), b.get("x", 0), name, b))
        lines.append(f"### Tab: {p['name']}  (`{pid}`)")
        lines.append("")
        if not groups:
            lines.append("No boards yet.")
            lines.append("")
            continue
        for g in sorted(groups.values(), key=lambda g: (-g["rev"], min(e[0] for e in g["entries"]), min(e[1] for e in g["entries"]))):
            lines.append(f"#### {g['label']}")
            lines.append("")
            for _, _, name, b in sorted(g["entries"]):
                exists = (copy_dir / name).exists()
                link = f"[`{name}`]({name})" if exists else f"`{name}` (not copied yet)"
                tag = " - interactive" if b.get("is_interactive") and "interactive" not in b.get("title", "").lower() else ""
                lines.append(f"- {link} - {b.get('title', '').strip() or name}{tag}")
            lines.append("")
    lines.append(README_END)
    return "\n".join(lines) + "\n"


def write_readme(idx: dict, version: str | None, date: str) -> Path:
    readme = COPY_DIR / "README.md"
    text = readme.read_text(encoding="utf-8") if readme.exists() else ""
    if README_START in text and README_END in text:
        head = text.split(README_START, 1)[0]
        tail = text.split(README_END, 1)[1]
    else:
        # First run on the hand-kept README: keep its introduction (everything above "## Rounds and boards").
        head = text.split("## Rounds and boards", 1)[0] if "## Rounds and boards" in text else text
        tail = "\n"
        if not version:
            vm = re.search(r"canvas version `([^`]+)`", text)
            version = vm.group(1) if vm else None
    head = re.sub(r"Rev \d+ to Rev \d+,\ncopied here on [\d-]+ from canvas version `[^`]+` so", "every round,\ncopied here so", head)
    body = render_readme(idx, COPY_DIR, version, date)
    readme.write_text(head.rstrip("\n") + "\n\n" + body + tail.lstrip("\n"), encoding="utf-8")
    return readme


def cmd_pull(args) -> int:
    src = Path(args.src)
    if (src / "project").is_dir():
        src = src / "project"
    src_idx = src / "canvas.json"
    if not src_idx.exists():
        print(f"ERROR: {src} holds no canvas.json; read `project/canvas.json` from the canvas into it first", file=sys.stderr)
        return 2
    COPY_DIR.mkdir(parents=True, exist_ok=True)
    idx = load_index(src_idx)
    new = changed = same = 0
    for f in sorted(src.glob("*.dc.html")):
        target = COPY_DIR / f.name
        data = f.read_bytes()
        if not target.exists():
            new += 1
            print(f"  new      {f.name}")
        elif target.read_bytes() != data:
            changed += 1
            print(f"  changed  {f.name}")
        else:
            same += 1
            continue
        target.write_bytes(data)
    old_idx = load_index(COPY_DIR / "canvas.json") if (COPY_DIR / "canvas.json").exists() else {"boards": {}, "pages": []}
    shutil.copyfile(src_idx, COPY_DIR / "canvas.json")
    new_pages = [p["id"] for p in idx.get("pages", []) if p["id"] not in {q["id"] for q in old_idx.get("pages", [])}]
    print(f"boards: {new} new, {changed} changed, {same} unchanged; index: {len(idx.get('boards', {}))} boards, new tabs {new_pages or 'none'}")
    missing = [b for b in idx.get("boards", {}) if not (COPY_DIR / b).exists()]
    date = args.date or _dt.date.today().isoformat()
    readme = write_readme(idx, args.version, date)
    print(f"README regenerated: {rel(readme)}")
    if missing:
        print(f"{len(missing)} board(s) in the index have no file in the copy; read them from the canvas (paths below) and pull again:")
        for b in missing:
            print(f"  project/{b}")
        return 1
    return 0


def cmd_readme(args) -> int:
    idx = load_index(COPY_DIR / "canvas.json")
    date = args.date or _dt.date.today().isoformat()
    readme = write_readme(idx, args.version, date)
    print(f"README regenerated: {rel(readme)}")
    return 0


# ----------------------------------------------------------------------------------------------- self-test

GOOD_LOGIC = """class Component extends DCLogic {
constructor(p){ super(p); this.state = { picked: 'A', made: [] }; }
renderVals() {
  const opts = ['A','B'].map(k => ({ key: k, on: k === this.state.picked, pick: () => this.setState({ picked: k }) }));
  return { title: this.props.title ?? 'Round', opts, make: () => this.setState({ made: this.state.made.concat(this.state.picked) }) };
}
}"""
BAD_LOGIC = """class Component extends DCLogic {
constructor(p){ super(p); this.state = { draft: 'x' }; }
renderVals() {
  return { title: 'T', create: () => { this.setState({ draft: null }); return this.state.draft.trim(); } };
}
}"""


def good_board(w: int, h: int, logic: str = GOOD_LOGIC, markup: str = "") -> str:
    markup = markup or ('<h1 style="margin:0">{{title}}</h1><sc-for list="{{opts}}" as="o" hint-placeholder-count="2">'
                        '<button onClick="{{o.pick}}" style="height:48px">{{o.key}}</button></sc-for>'
                        '<button onClick="{{make}}" style="height:48px">MAKE</button>')
    s = skeleton("t", w, h, False)
    s = s.replace(f'<span class="lab" style="position:absolute;left:64px;top:56px">{DRAFT_MARK}</span>', markup)
    return s.replace("class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", logic).replace(
        """data-props='{"$preview":{"width":%d,"height":%d}}'""" % (w, h),
        """data-props='{"title":{"editor":"text","default":"Round &amp; co"},"$preview":{"width":%d,"height":%d}}'""" % (w, h))


def self_test() -> int:
    fails = []

    def expect(cond: bool, what: str) -> None:
        print(("  ok   " if cond else "  FAIL ") + what)
        if not cond:
            fails.append(what)

    with tempfile.TemporaryDirectory() as td:
        td = Path(td)
        live = {"v": 3, "createdOnFiles": {"v": 1, "at": "2026-10-14T09:00:00Z"}, "title": "T", "launch": {"view": "canvas", "page": "rev30"},
                "pages": [{"id": "menu", "name": "Menu"}, {"id": "rev3", "name": "Rev 3"}, {"id": "rev30", "name": "Rev 30 - X"}],
                "boards": {"Main.dc.html": {"x": 0, "y": 0, "w": 1920, "h": 1080, "title": "Item 1"},
                           "R3Home.dc.html": {"x": 0, "y": 0, "w": 1920, "h": 1080, "page": "rev3", "title": "Rev 3 Home"},
                           "R30CodexVault.dc.html": {"x": 0, "y": 0, "w": 1920, "h": 1080, "page": "rev30", "title": "Rev 30 - A Vault", "is_interactive": True}},
                "order": ["R30CodexVault.dc.html", "R3Home.dc.html", "Main.dc.html"], "notes": {}, "designSystems": [], "attachments": {}}
        lp = td / "live.json"
        lp.write_text(json.dumps(live), encoding="utf-8")
        expect(next_rev(live) == 31, "next-rev: R3 and R30 taken -> Rev 31 (R3 is not a prefix of R30)")
        live2 = json.loads(json.dumps(live))
        live2["boards"]["R31DeckMake.dc.html"] = {"x": 0, "y": 0, "w": 1, "h": 1, "page": "rev31"}
        expect(next_rev(live2) == 32, "next-rev: a board prefix claims a round even without its page")
        root = td / "root"
        ns = argparse.Namespace(live=str(lp), root=str(root), name="Squads, three ways", board=['A=Hangar bay', 'B=Formation', 'C=The deck', 'D=Dossier'],
                                interactive="C", compare="the four side by side", notes=None, no_notes=False, notes_h="900", size="1920x1080", rev=None,
                                skeletons=True, tab=None, new_tab=None)
        expect(cmd_plan(ns) == 1, "plan: refuses a round that names no tab")
        expect(cmd_plan(argparse.Namespace(**{**vars(ns), "tab": "nope"})) == 1, "plan: refuses a tab that does not exist")
        rc = cmd_plan(argparse.Namespace(**{**vars(ns), "tab": "rev30"}))
        planned = load_index(root / "project" / "canvas.json")
        top = 1080 + ROUND_GAP + TITLE_ABOVE
        expect(rc == 0 and len(planned["pages"]) == 3 and planned["notes"]["rev31"]["text"] == "Rev 31 - Squads, three ways"
               and planned["notes"]["rev31"]["page"] == "rev30" and planned["notes"]["rev31"]["y"] == top - TITLE_ABOVE,
               "plan: a round is a titled section on its tab (no new page), titled under the tab's last row")
        expect(planned["launch"]["page"] == "rev30", "plan: launch opens on the round's tab")
        expect(planned["boards"]["R31Compare.dc.html"]["x"] == 0 and planned["boards"]["R31A.dc.html"]["x"] == 2100
               and planned["boards"]["R31B.dc.html"]["x"] == 4200 and planned["boards"]["R31C.dc.html"] == {"x": 0, "y": top + 1260, "w": 1920, "h": 1080, "page": "rev30", "title": "Rev 31 C - The deck - interactive", "is_interactive": True}
               and planned["boards"]["R31Notes.dc.html"]["y"] == top + 2520, "plan: compare first, three to a row, 180 apart, notes under the last row")
        expect(all(k in planned["boards"] for k in live["boards"]) and planned["order"][:3] == live["order"] and planned["attachments"] == {},
               "plan: every key that was there is still there")
        expect(next_rev(planned) == 32 and round_label(planned, 31) == "Rev 31 - Squads, three ways" and round_label(planned, 30) == "Rev 30 - X",
               "next-rev and round labels read title notes as well as old pages")
        rc2 = cmd_plan(argparse.Namespace(**{**vars(ns), "rev": 30, "tab": "rev30", "root": str(td / "r2")}))
        expect(rc2 == 1, "plan: refuses a taken round")
        rc3 = cmd_plan(argparse.Namespace(**{**vars(ns), "new_tab": "Voyage Redesign", "root": str(td / "r3"), "skeletons": False}))
        p3 = load_index(td / "r3" / "project" / "canvas.json")
        expect(rc3 == 0 and p3["pages"][-1] == {"id": "voyage-redesign", "name": "Voyage Redesign"} and p3["boards"]["R31A.dc.html"]["y"] == 0
               and p3["notes"]["rev31"]["y"] == -TITLE_ABOVE and p3["launch"]["page"] == "voyage-redesign", "plan --new-tab: the round opens a new tab at its top")
        # new-tab and fold.
        rc = cmd_new_tab(argparse.Namespace(live=str(lp), root=str(td / "nt"), name="Menu Redesign C", id=None))
        nt = load_index(td / "nt" / "project" / "canvas.json")
        expect(rc == 0 and nt["pages"][-1] == {"id": "menu-redesign-c", "name": "Menu Redesign C"} and nt["launch"]["page"] == "menu-redesign-c"
               and nt["boards"] == live["boards"], "new-tab: adds the tab, opens on it, touches no board")
        expect(cmd_new_tab(argparse.Namespace(live=str(td / "nt" / "project" / "canvas.json"), root=str(td / "nt2"), name="menu redesign c", id=None)) == 1,
               "new-tab: refuses a name that exists")
        full = json.loads(json.dumps(live))
        full["pages"] = [{"id": f"p{i}", "name": f"P{i}"} for i in range(MAX_TABS)]
        fp = td / "full.json"
        fp.write_text(json.dumps(full), encoding="utf-8")
        expect(cmd_new_tab(argparse.Namespace(live=str(fp), root=str(td / "nt3"), name="One more", id=None)) == 1, "new-tab: refuses a 41st tab")
        live_n = json.loads(json.dumps(live))
        live_n["notes"]["item1"] = {"kind": "title1", "text": "Item 1", "x": 0, "y": -300, "w": 240, "maxW": 1920}
        lnp = td / "live_n.json"
        lnp.write_text(json.dumps(live_n), encoding="utf-8")
        rc = cmd_fold(argparse.Namespace(live=str(lnp), root=str(td / "fo"), into="menu-redesign", name="Menu Redesign", pages="rev30,rev3,menu", title="Froglet Design Board"))
        fo = load_index(td / "fo" / "project" / "canvas.json")
        expect(rc == 0 and fo["pages"] == [{"id": "menu-redesign", "name": "Menu Redesign"}] and fo["title"] == "Froglet Design Board"
               and all(b["page"] == "menu-redesign" for b in fo["boards"].values()) and fo["launch"]["page"] == "menu-redesign",
               "fold: three pages (and the page-less boards of the first) become one tab")
        expect(fo["notes"]["rev30"]["text"] == "Rev 30 - X" and fo["notes"]["menu"]["text"] == "Menu"
               and fo["boards"]["R30CodexVault.dc.html"]["y"] - fo["notes"]["rev30"]["y"] == TITLE_ABOVE
               and fo["notes"]["item1"]["y"] < fo["boards"]["Main.dc.html"]["y"] and fo["notes"]["menu"]["y"] < fo["notes"]["item1"]["y"],
               "fold: each page is a titled section, its own notes kept above its boards")
        rep = Report()
        check_index(td / "fo" / "project", fo, None, rep)
        expect(not rep.errors and not any("overlaps" in w for w in rep.warnings), "fold: the folded index passes the index gate, no frame overlaps")
        expect(section_of(fo, "R3Home.dc.html")[1] == "Rev 3" and section_of(fo, "Main.dc.html")[1] == "Item 1",
               "section_of: a board reads its round's or nearest title")
        expect(next_rev(fo) == 31, "fold: the round numbers stay claimed after their pages go")
        # The gate: skeletons fail, a good board passes, a bad board names its faults.
        rep = Report()
        check_board(root / "project", "R31A.dc.html", planned["boards"]["R31A.dc.html"], rep, run_node=False)
        expect(any("skeleton" in e for e in rep.errors), "check: a skeleton still carrying the draft marker fails")
        (root / "project" / "R31A.dc.html").write_text(good_board(1920, 1080), encoding="utf-8")
        for k in ("B", "C", "D", "Compare"):
            (root / "project" / f"R31{k}.dc.html").write_text(good_board(1920, 1080, GOOD_LOGIC, '<h1 style="margin:0">{{title}}</h1>'), encoding="utf-8")
        (root / "project" / "R31Notes.dc.html").write_text(good_board(1920, 900, GOOD_LOGIC, '<h1 style="margin:0">{{title}}</h1>'), encoding="utf-8")
        rc = cmd_check(argparse.Namespace(dir=str(root), only="R31", strict=False, no_node=False))
        expect(rc == 0, "check: a complete round passes (holes resolve, loop items resolve, handlers press clean)")
        bad = good_board(1920, 1080, BAD_LOGIC, '<h1>{{title}}</h1><p>{{a + b}}</p><p>{{nothere}}</p><button onClick="{{create}}">GO</button>')
        bad = bad.replace("width: 1920px; height: 1080px", "width: 1900px; height: 1080px").replace('"$preview":{"width":1920', '"$preview":{"width":1900')
        (root / "project" / "R31D.dc.html").write_text(bad, encoding="utf-8")
        rep = Report()
        check_board(root / "project", "R31D.dc.html", planned["boards"]["R31D.dc.html"], rep)
        joined = "\n".join(rep.errors)
        expect("is an expression" in joined, "check: an expression hole is named")
        expect("no key nothere" in joined, "check: a hole with no renderVals() key is named")
        expect("handler create throws" in joined, "check: a handler that reads state it just cleared is caught (the Rev 31 `create` bug)")
        expect("$preview 1900x1080 != canvas.json frame 1920x1080" in joined and "root width 1900 != canvas.json frame width 1920" in joined, "check: frame, $preview and root size must agree")
        bad2 = good_board(1920, 1080, GOOD_LOGIC, '<iframe src="x"></iframe><Card/><img src="data:image/png;base64,AAAA"> \U0001F680')
        (root / "project" / "R31B.dc.html").write_text(bad2, encoding="utf-8")
        rep = Report()
        check_board(root / "project", "R31B.dc.html", planned["boards"]["R31B.dc.html"], rep, run_node=False)
        joined = "\n".join(rep.errors)
        expect("<iframe>" in joined and "capitalised tag" in joined and "data: URI" in joined and "emoji" in joined, "check: iframe, capitalised tag, data URI and emoji are each named")
        idx_bad = json.loads(json.dumps(planned))
        idx_bad["boards"]["R31B.dc.html"]["x"] = 2000
        idx_bad["order"].remove("R31D.dc.html")
        rep = Report()
        check_index(root / "project", idx_bad, "R31", rep)
        joined = "\n".join(rep.errors)
        expect("overlaps" in joined and "order must list every board once" in joined, "check: an overlapping new frame and a board missing from order are named")
        # pull + readme into a fake repo copy.
        global COPY_DIR, DOC
        saved = (COPY_DIR, DOC)
        try:
            COPY_DIR = td / "copy"
            DOC = td / "nodoc.md"
            COPY_DIR.mkdir()
            (COPY_DIR / "README.md").write_text("# Copy\n\nIntro kept, Rev 3 to Rev 24,\ncopied here on 2026-10-26 from canvas version `old` so a session can read.\n\n## Rounds and boards\n\n- old hand list\n", encoding="utf-8")
            (root / "project" / "R31B.dc.html").write_text(good_board(1920, 1080), encoding="utf-8")
            (root / "project" / "R31D.dc.html").write_text(good_board(1920, 1080), encoding="utf-8")
            rc = cmd_pull(argparse.Namespace(src=str(root), version="v-test", date="2026-10-28"))
            readme = (COPY_DIR / "README.md").read_text(encoding="utf-8")
            expect(rc == 1 and (COPY_DIR / "R31A.dc.html").exists() and (COPY_DIR / "canvas.json").exists(), "pull: copies boards and the index, exit 1 while index boards lack files (Main, R3Home, R30)")
            expect("Intro kept, every round,\ncopied here so" in readme and "- old hand list" not in readme and "#### Rev 31 - Squads, three ways" in readme
                   and "### Tab: Rev 30 - X  (`rev30`)" in readme and readme.index("#### Rev 31 - Squads") < readme.index("#### Rev 30 - X")
                   and "`Main.dc.html` (not copied yet)" in readme and "canvas version `v-test`" in readme,
                   "readme: intro kept, the hand list replaced, by tab, newest round first, uncopied boards marked")
            for n in ("Main.dc.html", "R3Home.dc.html", "R30CodexVault.dc.html"):
                (root / "project" / n).write_text(good_board(1920, 1080), encoding="utf-8")
            rc = cmd_pull(argparse.Namespace(src=str(root), version="v-test2", date="2026-10-28"))
            readme2 = (COPY_DIR / "README.md").read_text(encoding="utf-8")
            expect(rc == 0 and readme2.count(README_START) == 1 and "v-test2" in readme2 and "(not copied yet)" not in readme2, "pull: a second pull is exit 0 and regenerates in place")
        finally:
            COPY_DIR, DOC = saved
    print("design_board.py self-test:", "OK" if not fails else f"{len(fails)} FAILED")
    return 0 if not fails else 1


# ----------------------------------------------------------------------------------------------- main

def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--self-test", action="store_true")
    sub = ap.add_subparsers(dest="cmd")
    s = sub.add_parser("status"); s.add_argument("--live"); s.add_argument("--version")
    s = sub.add_parser("find"); s.add_argument("words", nargs="+"); s.add_argument("--live")
    s = sub.add_parser("tabs"); s.add_argument("--live")
    s = sub.add_parser("next-rev"); s.add_argument("--live")
    s = sub.add_parser("plan")
    s.add_argument("--live", required=True); s.add_argument("--root", required=True); s.add_argument("--name", required=True)
    s.add_argument("--tab"); s.add_argument("--new-tab")
    s.add_argument("--board", action="append"); s.add_argument("--interactive"); s.add_argument("--compare")
    s.add_argument("--notes"); s.add_argument("--no-notes", action="store_true"); s.add_argument("--notes-h", default="900")
    s.add_argument("--size", default="1920x1080"); s.add_argument("--rev", type=int); s.add_argument("--skeletons", action="store_true")
    s = sub.add_parser("new-tab"); s.add_argument("--live", required=True); s.add_argument("--root", required=True)
    s.add_argument("--name", required=True); s.add_argument("--id")
    s = sub.add_parser("fold"); s.add_argument("--live", required=True); s.add_argument("--root", required=True)
    s.add_argument("--into", required=True); s.add_argument("--name", required=True); s.add_argument("--pages", required=True)
    s.add_argument("--title")
    s = sub.add_parser("check"); s.add_argument("dir"); s.add_argument("--only"); s.add_argument("--strict", action="store_true"); s.add_argument("--no-node", action="store_true")
    s = sub.add_parser("pull"); s.add_argument("--src", required=True); s.add_argument("--version"); s.add_argument("--date")
    s = sub.add_parser("readme"); s.add_argument("--version"); s.add_argument("--date")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if not args.cmd:
        ap.print_help()
        return 2
    return {"status": cmd_status, "tabs": cmd_tabs, "find": cmd_find, "next-rev": cmd_next_rev, "plan": cmd_plan,
            "new-tab": cmd_new_tab, "fold": cmd_fold, "check": cmd_check, "pull": cmd_pull, "readme": cmd_readme}[args.cmd](args)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except BrokenPipeError:
        sys.exit(0)
