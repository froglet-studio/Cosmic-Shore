#!/usr/bin/env python3
"""principles.py - the principles ledger: every design rule the owners have stated, in their own words, with its
source line, so a session can check a design BEFORE building it (Docs/claude/PRINCIPLES.md, DESIGN_BRIEF.md;
Docs/SKILL_TRAINING_PLAN.md 3.3).

A READER tool, except --md (rewrites one generated block in PRINCIPLES.md) and --import (writes principles.json
from a mined file, the seed step). Stdlib only.

    python3 Tools/Build/principles.py                        # the ledger as a table
    python3 Tools/Build/principles.py --area vessel          # one area (vessel, ui, world-look, modes, ecology, ai, ...)
    python3 Tools/Build/principles.py --owner Garrett        # one owner's entries
    python3 Tools/Build/principles.py --check                # the gate (exit 1 on a failure)
    python3 Tools/Build/principles.py --md                   # regenerate the table in Docs/claude/PRINCIPLES.md
    python3 Tools/Build/principles.py --json
    python3 Tools/Build/principles.py --self-test
    python3 Tools/Build/principles.py --import mined.json    # seed principles.json from mined entries (ids assigned)

--check fails when:
  words    an entry's words (the first 40 characters, letters and digits only) are not within 5 lines of its
           source line in its source file: the record moved or was rewritten, so the entry is unfounded;
  locked   a LOCKED rule heading in CLAUDE.md or Docs/claude/*.md (a heading line, or a bold line at the start
           of a line, that says LOCKED) has no entry in that file within 2 lines of it;
  ids      two entries share an id.
Every failure prints as file:line and the id. Exit 0 ok, 1 a failure, 2 a setup error.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
LEDGER = os.path.join("Docs", "claude", "principles.json")
DOC = os.path.join("Docs", "claude", "PRINCIPLES.md")
BEGIN, END = "<!-- principles:begin -->", "<!-- principles:end -->"
LOCKED_LINE = re.compile(r"^(#{1,6} .*LOCKED|\*\*[^*]*LOCKED[^*]*\*\*)")
AREAS = ["vessel", "ui", "world-look", "modes", "ecology", "ai", "multiplayer", "camera", "audio", "prisms",
         "economy", "ftue", "process", "other"]


def norm(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", " ", (s or "").lower()).strip()


def load(root: str = ROOT, path: str = LEDGER) -> dict:
    with open(os.path.join(root, path), encoding="utf-8") as fh:
        return json.load(fh)


def read_lines(root: str, rel: str) -> list[str] | None:
    p = os.path.join(root, rel)
    if not os.path.isfile(p):
        return None
    with open(p, encoding="utf-8", errors="replace") as fh:
        return fh.read().splitlines()


def check_words(entry: dict, root: str, cache: dict) -> str | None:
    """None when the entry's words sit near its source line; else the reason."""
    rel, line = entry["source"]["file"], int(entry["source"]["line"])
    if rel not in cache:
        cache[rel] = read_lines(root, rel)
    lines = cache[rel]
    if lines is None:
        return f"{rel}: missing file"
    lo, hi = max(0, line - 6), min(len(lines), line + 5)
    window = norm(" ".join(lines[lo:hi]))
    key = norm(entry["words"])[:40]
    if not key:
        return f"{rel}:{line}: empty words"
    if key not in window:
        return f"{rel}:{line}: words not within 5 lines ('{entry['words'][:40]}...')"
    return None


def locked_lines(root: str) -> list[tuple[str, int, str]]:
    out = []
    files = [os.path.join("CLAUDE.md")]
    claude_dir = os.path.join(root, "Docs", "claude")
    if os.path.isdir(claude_dir):
        files += [os.path.join("Docs", "claude", f) for f in sorted(os.listdir(claude_dir)) if f.endswith(".md")]
    for rel in files:
        lines = read_lines(root, rel)
        if lines is None:
            continue
        for i, text in enumerate(lines, 1):
            if LOCKED_LINE.match(text.strip()) and "LOCKED" in text:
                out.append((rel, i, text.strip()))
    return out


def check(ledger: dict, root: str = ROOT) -> list[str]:
    failures: list[str] = []
    cache: dict = {}
    seen: dict[str, int] = {}
    for e in ledger.get("entries", []):
        seen[e["id"]] = seen.get(e["id"], 0) + 1
        why = check_words(e, root, cache)
        if why:
            failures.append(f"words  {why}  [{e['id']}]")
    for i, n in seen.items():
        if n > 1:
            failures.append(f"ids    {i} appears {n} times")
    by_file: dict[str, list[tuple[int, str]]] = {}
    for e in ledger.get("entries", []):
        by_file.setdefault(e["source"]["file"], []).append((int(e["source"]["line"]), e["id"]))
    for rel, line, text in locked_lines(root):
        near = [i for ln, i in by_file.get(rel, []) if abs(ln - line) <= 2]
        if not near:
            failures.append(f"locked {rel}:{line}: no entry for '{text[:70]}'")
    return failures


def table(entries: list[dict]) -> str:
    rows = ["| id | owner | date | rule | words | source |", "|---|---|---|---|---|---|"]
    for e in entries:
        words = e["words"].replace("|", "/").replace("\n", " ")
        if len(words) > 110:
            words = words[:107] + "..."
        rule = e["rule"].replace("|", "/").replace("\n", " ")
        rows.append(f"| {e['id']} | {e.get('owner', '')} | {e.get('date', '')} | {rule} | {words} | `{e['source']['file']}:{e['source']['line']}` |")
    return "\n".join(rows)


def write_md(ledger: dict, root: str = ROOT, doc: str = DOC) -> int:
    path = os.path.join(root, doc)
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    if BEGIN not in text or END not in text:
        print(f"principles: {doc} has no {BEGIN} / {END} markers", file=sys.stderr)
        return 2
    entries = sorted(ledger["entries"], key=lambda e: (AREAS.index(e["area"]) if e["area"] in AREAS else 99, e["id"]))
    parts = []
    for area in AREAS:
        group = [e for e in entries if e["area"] == area]
        if not group:
            continue
        parts.append(f"\n### {area} ({len(group)})\n\n{table(group)}\n")
    block = f"{BEGIN}\n{''.join(parts)}\n{END}"
    head, rest = text.split(BEGIN, 1)
    _, tail = rest.split(END, 1)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(head + block + tail)
    print(f"principles: wrote {len(entries)} entries into {doc}")
    return 0


def import_mined(mined_path: str, root: str = ROOT, out: str = LEDGER, measured: str = "") -> int:
    with open(mined_path, encoding="utf-8") as fh:
        mined = json.load(fh)
    if isinstance(mined, dict):
        mined = mined.get("entries") or mined.get("ok") or []
    entries, counters, cache = [], {}, {}
    seen_quotes = set()
    for m in sorted(mined, key=lambda m: (m.get("area", "other"), m.get("date") or "9999", m.get("source_file", ""), m.get("source_line", 0))):
        area = m.get("area") or "other"
        if area not in AREAS:
            area = "other"
        key = norm(m.get("quote", ""))[:60]
        if not key or key in seen_quotes:
            continue
        seen_quotes.add(key)
        probe = {"words": m.get("quote", ""), "source": {"file": m.get("source_file", ""), "line": m.get("source_line", 0)}}
        if check_words(probe, root, cache):
            continue  # unfounded at its source line: never enters the ledger
        counters[area] = counters.get(area, 0) + 1
        entries.append({
            "id": f"P-{area}-{counters[area]:02d}",
            "owner": m.get("owner") or "unattributed",
            "date": m.get("date") or "",
            "words": m.get("quote", ""),
            "rule": m.get("rule", ""),
            "source": {"file": m.get("source_file", ""), "line": int(m.get("source_line", 0))},
            "kind": m.get("kind") or "quote",
            "area": area,
            "retired": m.get("retired") or "",
            "test": m.get("test") or "",
            "status": "drafted",
        })
    ledger = {"version": 1, "measured": measured or "", "entries": entries}
    with open(os.path.join(root, out), "w", encoding="utf-8") as fh:
        json.dump(ledger, fh, indent=1, ensure_ascii=False)
        fh.write("\n")
    print(f"principles: imported {len(entries)} entries ({len(mined) - len(entries)} dropped as duplicates or unfounded) into {out}")
    return 0


def seed_locked(ledger: dict, root: str = ROOT) -> int:
    """Add an entry for every LOCKED heading no entry covers (kind LOCKED, the heading's words), so --check is complete."""
    by_file: dict[str, list[int]] = {}
    for e in ledger["entries"]:
        by_file.setdefault(e["source"]["file"], []).append(int(e["source"]["line"]))
    added = 0
    for rel, line, text in locked_lines(root):
        if any(abs(ln - line) <= 2 for ln in by_file.get(rel, [])):
            continue
        heading = re.sub(r"^#{1,6}\s*", "", text).strip("* ")
        owner = ""
        m = re.search(r"\b(Garrett|Shombith|Yash|Caleb)\b", heading)
        if m:
            owner = m.group(1)
        area = "process" if re.search(r"verify|commit|branch|tool|ship|menu root|convention|text has a human", heading, re.I) else "other"
        n = sum(1 for e in ledger["entries"] if e["area"] == area) + 1
        ledger["entries"].append({
            "id": f"P-{area}-{n:02d}", "owner": owner or "unattributed",
            "date": (re.search(r"20\d\d-\d\d-\d\d", heading) or [""])[0] if re.search(r"20\d\d-\d\d-\d\d", heading) else "",
            "words": heading[:300], "rule": heading[:200], "source": {"file": rel, "line": line}, "kind": "LOCKED",
            "area": area, "retired": "", "test": "", "status": "drafted",
        })
        by_file.setdefault(rel, []).append(line)
        added += 1
    return added


def self_test() -> int:
    with tempfile.TemporaryDirectory() as tmp:
        os.makedirs(os.path.join(tmp, "Docs", "claude"))
        with open(os.path.join(tmp, "CLAUDE.md"), "w", encoding="utf-8") as fh:
            fh.write("# x\n\n## A game mode never changes the vessel (LOCKED rule, Shombith, 2026-10-31)\n\n\"The vessel kit as designed always stays the same\"\n")
        with open(os.path.join(tmp, "Docs", "claude", "X.md"), "w", encoding="utf-8") as fh:
            fh.write("intro\n### Nothing in the world looks flat (LOCKED, Garrett 2026-10-18)\n\n> \"Nothing in the environment should ever look flat like this\"\n")
        good = {"version": 1, "entries": [
            {"id": "P-modes-01", "owner": "Shombith", "date": "2026-10-31", "words": "The vessel kit as designed always stays the same", "rule": "A mode never changes the vessel", "source": {"file": "CLAUDE.md", "line": 3}, "kind": "LOCKED", "area": "modes", "retired": "", "test": "", "status": "drafted"},
            {"id": "P-world-look-01", "owner": "Garrett", "date": "2026-10-18", "words": "Nothing in the environment should ever look flat like this", "rule": "No flat world effect", "source": {"file": "Docs/claude/X.md", "line": 2}, "kind": "LOCKED", "area": "world-look", "retired": "", "test": "", "status": "drafted"},
        ]}
        assert check(good, tmp) == [], check(good, tmp)
        # negative controls
        bad = json.loads(json.dumps(good)); bad["entries"][0]["words"] = "words that are nowhere in the file"
        assert any(f.startswith("words") for f in check(bad, tmp)), check(bad, tmp)
        bad = json.loads(json.dumps(good)); bad["entries"].pop(1)
        assert any(f.startswith("locked") for f in check(bad, tmp)), check(bad, tmp)
        bad = json.loads(json.dumps(good)); bad["entries"][1]["id"] = "P-modes-01"
        assert any(f.startswith("ids") for f in check(bad, tmp)), check(bad, tmp)
        # --md round trip and seed_locked
        with open(os.path.join(tmp, "Docs", "claude", "PRINCIPLES.md"), "w", encoding="utf-8") as fh:
            fh.write(f"# P\n\n{BEGIN}\nold\n{END}\n\ntail\n")
        assert write_md(good, tmp) == 0
        text = open(os.path.join(tmp, "Docs", "claude", "PRINCIPLES.md"), encoding="utf-8").read()
        assert "P-modes-01" in text and "old" not in text and text.endswith("tail\n"), text
        partial = json.loads(json.dumps(good)); partial["entries"].pop(1)
        assert seed_locked(partial, tmp) == 1 and check(partial, tmp) == [], check(partial, tmp)
        # import: a mined entry whose quote is not at its line is dropped
        mined = os.path.join(tmp, "mined.json")
        with open(mined, "w", encoding="utf-8") as fh:
            json.dump([{"owner": "Garrett", "date": "2026-10-18", "quote": "Nothing in the environment should ever look flat like this", "rule": "r", "source_file": "Docs/claude/X.md", "source_line": 2, "kind": "LOCKED", "area": "world-look"},
                       {"owner": "Garrett", "date": "", "quote": "not in the file at all", "rule": "r", "source_file": "Docs/claude/X.md", "source_line": 2, "kind": "quote", "area": "ui"}], fh)
        assert import_mined(mined, tmp, os.path.join("Docs", "claude", "p.json")) == 0
        imported = json.load(open(os.path.join(tmp, "Docs", "claude", "p.json"), encoding="utf-8"))
        assert [e["id"] for e in imported["entries"]] == ["P-world-look-01"], imported
    print("principles: self-test OK")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--area")
    ap.add_argument("--owner")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--md", action="store_true")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--import", dest="import_path", help="seed principles.json from a mined JSON list")
    ap.add_argument("--measured", default="", help="the measured date to record with --import")
    ap.add_argument("--seed-locked", action="store_true", help="add an entry for every uncovered LOCKED heading, then save")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if args.import_path:
        return import_mined(args.import_path, measured=args.measured)
    try:
        ledger = load()
    except (OSError, json.JSONDecodeError) as exc:
        print(f"principles: {exc}", file=sys.stderr)
        return 2
    if args.seed_locked:
        added = seed_locked(ledger)
        with open(os.path.join(ROOT, LEDGER), "w", encoding="utf-8") as fh:
            json.dump(ledger, fh, indent=1, ensure_ascii=False)
            fh.write("\n")
        print(f"principles: added {added} entries for uncovered LOCKED headings")
        return 0
    if args.check:
        failures = check(ledger)
        for f in failures:
            print("FAIL  " + f)
        print(f"principles: {len(ledger['entries'])} entries, {len(failures)} failure(s)")
        return 1 if failures else 0
    if args.md:
        return write_md(ledger)
    entries = ledger["entries"]
    if args.area:
        entries = [e for e in entries if e["area"] == args.area]
    if args.owner:
        entries = [e for e in entries if args.owner.lower() in e.get("owner", "").lower()]
    if args.json:
        print(json.dumps(entries, indent=1, ensure_ascii=False))
    else:
        print(table(entries))
        print(f"\n{len(entries)} entries")
    return 0


if __name__ == "__main__":
    sys.exit(main())
