#!/usr/bin/env python3
"""
apply_results.py — the deterministic half of the QA loop.

Reads every Docs/QA/RESULTS/*.md session file and applies the latest verdict for
each QA item to the three skill-owned files:

  Docs/QA/QA_BACKLOG.md   the live list           (items are ### QA-... sections)
  Docs/QA/ARCHIVE.md      items that PASSED        (removed from the backlog)
  Docs/QA/DEV_TASKS.md    handoff tasks for FAILs

Effect of each result value:
  PASS     -> section moved out of the backlog into ARCHIVE.md
  FAIL     -> heading marked 🔴, a Last-result line added, a DEV_TASKS entry upserted
  PARTIAL  -> heading marked 🟡, a Last-result line added
  BLOCKED  -> heading marked ⛔, a Last-result line added
  SKIP     -> no change

The script is idempotent: running it twice with the same RESULTS files produces
the same output. It has no third-party dependencies (stdlib only).

Usage:  python3 Tools/QA/apply_results.py        # apply
        python3 Tools/QA/apply_results.py --check # report what would change, write nothing
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
QA = ROOT / "Docs" / "QA"
BACKLOG = QA / "QA_BACKLOG.md"
ARCHIVE = QA / "ARCHIVE.md"
DEV_TASKS = QA / "DEV_TASKS.md"
RESULTS_DIR = QA / "RESULTS"

EMOJI = {"FAIL": "🔴", "PARTIAL": "🟡", "BLOCKED": "⛔", "PASS": "✅"}
STATUS_EMOJI = "⬜🟡🔴⛔✅"
VALID = {"PASS", "FAIL", "PARTIAL", "BLOCKED", "SKIP"}

HEADING_RE = re.compile(
    r"^###\s+(?P<id>QA-[A-Z0-9\-]+)\s*(?P<emoji>[" + STATUS_EMOJI + r"])?\s*(?P<rest>.*)$"
)
LAST_RESULT_PREFIX = "> **Last result:**"
DATE_RE = re.compile(r"(\d{4}-\d{2}-\d{2})")


class Result:
    def __init__(self, item_id, value, notes, build, date, tester, source):
        self.item_id = item_id
        self.value = value
        self.notes = notes
        self.build = build
        self.date = date
        self.tester = tester
        self.source = source

    def sort_key(self):
        # latest wins: by date, then by source filename
        return (self.date or "0000-00-00", self.source)


def parse_results():
    """Return {item_id: latest Result} across all RESULTS/*.md (excluding TEMPLATE)."""
    latest: dict[str, Result] = {}
    if not RESULTS_DIR.exists():
        return latest
    for path in sorted(RESULTS_DIR.glob("*.md")):
        if path.name.upper() == "TEMPLATE.MD":
            continue
        text = path.read_text(encoding="utf-8")
        # date + tester from the header line or the filename
        date = None
        tester = path.stem
        m = re.search(r"^#\s*QA Results\s*—\s*(?P<date>[\d-]+)\s*—\s*(?P<tester>.+)$",
                      text, re.MULTILINE)
        if m:
            date = m.group("date").strip()
            tester = m.group("tester").strip()
        if not date:
            dm = DATE_RE.search(path.stem)
            date = dm.group(1) if dm else "0000-00-00"
        # build line
        build = ""
        bm = re.search(r"^Build:\s*(?P<b>.+)$", text, re.MULTILINE)
        if bm:
            build = bm.group("b").strip()
        # table rows
        for line in text.splitlines():
            if not line.strip().startswith("|"):
                continue
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if len(cells) < 2:
                continue
            item_id, value = cells[0], cells[1].upper()
            notes = cells[2] if len(cells) > 2 else ""
            if not item_id.startswith("QA-") or value not in VALID:
                continue
            r = Result(item_id, value, notes, build, date, tester, path.name)
            prev = latest.get(item_id)
            if prev is None or r.sort_key() >= prev.sort_key():
                latest[item_id] = r
    return latest


def split_sections(lines):
    """Split backlog lines into an ordered list of blocks.

    Each block is ('raw', [lines])  or  ('item', id, [section_lines]).
    An item section runs from its ### heading to the next ### / ## / EOF.
    """
    blocks = []
    i = 0
    n = len(lines)
    raw = []
    while i < n:
        m = HEADING_RE.match(lines[i])
        if m:
            if raw:
                blocks.append(("raw", raw))
                raw = []
            item_id = m.group("id")
            sec = [lines[i]]
            i += 1
            while i < n and not lines[i].startswith("### ") and not lines[i].startswith("## "):
                sec.append(lines[i])
                i += 1
            blocks.append(("item", item_id, sec))
        else:
            raw.append(lines[i])
            i += 1
    if raw:
        blocks.append(("raw", raw))
    return blocks


def set_heading_emoji(heading, emoji):
    m = HEADING_RE.match(heading)
    rest = m.group("rest")
    return f"### {m.group('id')} {emoji} {rest}".rstrip()


def apply_last_result_line(sec_lines, r):
    """Insert or replace the Last-result blockquote right after the heading."""
    emoji = EMOJI.get(r.value, "")
    note = r.notes if r.notes else "(no note)"
    line = (f"{LAST_RESULT_PREFIX} {emoji} {r.value} — {note}  "
            f"_(build {r.build or 'unknown'}, {r.date}, {r.tester})_")
    out = [sec_lines[0]]  # heading
    body = sec_lines[1:]
    # drop an existing Last-result line (and a blank that followed it)
    cleaned = []
    skip_blank = False
    for ln in body:
        if ln.startswith(LAST_RESULT_PREFIX):
            skip_blank = True
            continue
        if skip_blank and ln.strip() == "":
            skip_blank = False
            continue
        skip_blank = False
        cleaned.append(ln)
    out.append(line)
    out.append("")
    out.extend(cleaned)
    return out


def title_of(sec_lines):
    m = HEADING_RE.match(sec_lines[0])
    return m.group("rest").lstrip("—- ").strip() if m else ""


def remove_marked_block(text, item_id, kind):
    """Remove a <!-- kind:ID --> ... <!-- /kind:ID --> block if present. Returns (text, removed)."""
    start = f"<!-- {kind}:{item_id} -->"
    end = f"<!-- /{kind}:{item_id} -->"
    pat = re.compile(r"\n*" + re.escape(start) + r".*?" + re.escape(end) + r"\n*", re.DOTALL)
    if not pat.search(text):
        return text, False
    text = pat.sub("\n", text)
    # restore the "_(none yet)_" placeholder if no marked blocks of this kind remain
    if f"<!-- {kind}:" not in text and "_(none yet)_" not in text:
        text = text.rstrip("\n") + "\n\n_(none yet)_\n"
    return text, True


def upsert_marked_block(text, item_id, block, kind):
    """Insert or replace a block delimited by <!-- kind:ID --> ... <!-- /kind:ID -->."""
    start = f"<!-- {kind}:{item_id} -->"
    end = f"<!-- /{kind}:{item_id} -->"
    wrapped = f"{start}\n{block}\n{end}"
    pat = re.compile(re.escape(start) + r".*?" + re.escape(end), re.DOTALL)
    if pat.search(text):
        return pat.sub(wrapped, text)
    # first real entry: drop the "_(none yet)_" placeholder line if present
    text = re.sub(r"(?m)^_\(none yet\)_\s*\n?", "", text)
    text = text.rstrip("\n") + "\n"
    sep = "\n"
    return text + sep + wrapped + "\n"


def main():
    check = "--check" in sys.argv
    if not BACKLOG.exists():
        print(f"error: {BACKLOG} not found", file=sys.stderr)
        return 1

    results = parse_results()
    backlog_lines = BACKLOG.read_text(encoding="utf-8").splitlines()
    blocks = split_sections(backlog_lines)

    archive_text = ARCHIVE.read_text(encoding="utf-8") if ARCHIVE.exists() else \
        "# QA Archive — items that PASSED\n\nKept so a re-scan never resurrects a passed item.\n"
    devtasks_text = DEV_TASKS.read_text(encoding="utf-8") if DEV_TASKS.exists() else \
        "# QA Dev Tasks — failures converted to handoff work\n\nEach entry's definition of done is \"the QA item passes\".\n"

    changes = []
    new_blocks = []
    for b in blocks:
        if b[0] != "item":
            new_blocks.append(b)
            continue
        _, item_id, sec = b
        r = results.get(item_id)
        if r is None or r.value == "SKIP":
            new_blocks.append(b)
            continue
        if r.value == "PASS":
            already = f"<!-- archived:{item_id} -->" in archive_text
            if not already:
                stamp = (f"_Passed on build {r.build or 'unknown'} "
                         f"({r.date}, {r.tester})._")
                block = stamp + "\n\n" + "\n".join(sec).rstrip()
                archive_text = upsert_marked_block(archive_text, item_id, block, "archived")
            changes.append(f"PASS   {item_id}  -> archived, removed from backlog")
            devtasks_text, cleared = remove_marked_block(devtasks_text, item_id, "devtask")
            if cleared:
                changes.append(f"       {item_id}  -> cleared its stale dev task (now PASS)")
            # dropped from backlog (do not append to new_blocks)
            continue
        # FAIL / PARTIAL / BLOCKED
        sec2 = list(sec)
        sec2[0] = set_heading_emoji(sec2[0], EMOJI[r.value])
        sec2 = apply_last_result_line(sec2, r)
        new_blocks.append(("item", item_id, sec2))
        changes.append(f"{r.value:<7}{item_id}  -> marked {EMOJI[r.value]}")
        if r.value == "FAIL":
            task = (f"### {item_id} — {title_of(sec)}\n"
                    f"- **Failed on:** {r.build or 'unknown'} ({r.date}, {r.tester})\n"
                    f"- **Symptom:** {r.notes or '(see results file)'}\n"
                    f"- **Definition of done:** QA item `{item_id}` passes.")
            devtasks_text = upsert_marked_block(devtasks_text, item_id, task, "devtask")
            changes.append(f"       {item_id}  -> DEV_TASKS entry upserted")
        else:  # PARTIAL / BLOCKED supersedes any earlier FAIL — clear its stale dev task
            devtasks_text, cleared = remove_marked_block(devtasks_text, item_id, "devtask")
            if cleared:
                changes.append(f"       {item_id}  -> cleared its stale dev task (now {r.value})")

    # Post-loop: clear stale dev tasks for any non-FAIL result, including items already
    # archived in a prior run (which the backlog loop above no longer iterates).
    for iid, rr in results.items():
        if rr.value in ("PASS", "PARTIAL", "BLOCKED"):
            devtasks_text, cleared = remove_marked_block(devtasks_text, iid, "devtask")
            if cleared:
                changes.append(f"       {iid}  -> cleared its stale dev task (now {rr.value})")

    out_lines = []
    for b in new_blocks:
        if b[0] == "raw":
            out_lines.extend(b[1])
        else:
            out_lines.extend(b[2])
    new_backlog = "\n".join(out_lines).rstrip() + "\n"

    if not changes:
        print("No results to apply — backlog unchanged.")
        return 0

    print("Applying results:")
    for c in changes:
        print("  " + c)

    if check:
        print("\n--check: no files written.")
        return 0

    BACKLOG.write_text(new_backlog, encoding="utf-8")
    ARCHIVE.write_text(archive_text if archive_text.endswith("\n") else archive_text + "\n",
                       encoding="utf-8")
    DEV_TASKS.write_text(devtasks_text if devtasks_text.endswith("\n") else devtasks_text + "\n",
                         encoding="utf-8")
    print("\nWrote QA_BACKLOG.md, ARCHIVE.md, DEV_TASKS.md.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
