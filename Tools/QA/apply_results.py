#!/usr/bin/env python3
"""Apply submitted QA results to the backlog, archive and dev-task list.

This is the deterministic half of the QA loop (`Docs/QA/README.md`). It reads every
`Docs/QA/RESULTS/*.md` session file that has not been applied yet and rewrites:

  Docs/QA/QA_BACKLOG.md   PASS items removed; others re-marked with their status
  Docs/QA/ARCHIVE.md      one row per PASS
  Docs/QA/DEV_TASKS.md    one task per FAIL

Applied session files are recorded in Docs/QA/.applied.json so re-running is a no-op.

Usage:
    python3 Tools/QA/apply_results.py            # apply, write files
    python3 Tools/QA/apply_results.py --dry-run  # report only
    python3 Tools/QA/apply_results.py --selftest # run built-in tests, touch nothing
"""

import argparse
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
QA_DIR = os.path.join(ROOT, "Docs", "QA")
BACKLOG = os.path.join(QA_DIR, "QA_BACKLOG.md")
ARCHIVE = os.path.join(QA_DIR, "ARCHIVE.md")
DEV_TASKS = os.path.join(QA_DIR, "DEV_TASKS.md")
RESULTS_DIR = os.path.join(QA_DIR, "RESULTS")
LEDGER = os.path.join(QA_DIR, ".applied.json")

VALID = {"PASS", "FAIL", "PARTIAL", "BLOCKED", "SKIP"}
MARKER = {"FAIL": "🔴", "PARTIAL": "🟡", "BLOCKED": "⛔"}
STATUS_CHARS = "⬜🟡🔴⛔"


# ---------------------------------------------------------------- parsing

def parse_session(text):
    """Return (meta, [(item_id, result, notes), ...]) for one results file."""
    meta = {}
    for key in ("Tester", "Date", "Branch", "Commit", "Unity version", "Platform(s)"):
        m = re.search(r"^\|\s*%s\s*\|\s*(.+?)\s*\|\s*$" % re.escape(key), text,
                      re.M | re.I)
        if m:
            meta[key] = m.group(1).strip().strip("*` ")

    body = text
    fenced = re.search(r"<!--\s*qa-results-table\s*-->(.*?)<!--\s*/qa-results-table\s*-->",
                       text, re.S)
    if fenced:
        body = fenced.group(1)

    rows = []
    for line in body.splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")] if "|" in line else []
        if len(cells) < 2:
            continue
        item = cells[0].strip("`* ")
        result = cells[1].upper().strip("*` ")
        if not item.startswith("QA-") or result not in VALID:
            continue
        notes = cells[2] if len(cells) > 2 else ""
        rows.append((item, result, notes))
    return meta, rows


def split_items(backlog):
    """Return [(item_id, section_text)] for every '### QA-…' section in the backlog."""
    out = []
    for m in re.finditer(r"^### (QA-[A-Z0-9-]+)\b", backlog, re.M):
        start = m.start()
        nxt = re.search(r"^(### QA-|## |---\s*$)", backlog[m.end():], re.M)
        end = m.end() + nxt.start() if nxt else len(backlog)
        out.append((m.group(1), backlog[start:end]))
    return out


# ---------------------------------------------------------------- rewriting

def remove_section(backlog, item_id):
    for iid, section in split_items(backlog):
        if iid == item_id:
            return backlog.replace(section, "", 1)
    return backlog


def set_status(backlog, item_id, result, notes, meta):
    """Re-mark a section's heading and append/refresh its QA note line."""
    marker = MARKER.get(result)
    if marker is None:
        return backlog
    for iid, section in split_items(backlog):
        if iid != item_id:
            continue
        head_end = section.index("\n") if "\n" in section else len(section)
        head, rest = section[:head_end], section[head_end:]
        head = re.sub(r"### %s\s*[%s]?" % (re.escape(item_id), STATUS_CHARS),
                      "### %s %s" % (item_id, marker), head, count=1)
        rest = re.sub(r"\n\*\*Last QA:.*?(?=\n\*\*|\n\d\.|\n\n|\Z)", "", rest, flags=re.S)
        note = "\n**Last QA:** %s on `%s` (%s, %s)%s\n" % (
            result, meta.get("Commit", "?"), meta.get("Date", "?"),
            meta.get("Tester", "?"), (" — " + notes) if notes else "")
        return backlog.replace(section, head + note + rest, 1)
    return backlog


def append_archive(archive, rows):
    if not rows:
        return archive
    lines = "".join("| %s | `%s` | %s | %s | %s |\n" % r for r in rows)
    return archive.replace("\n<!-- /qa-archive -->", lines + "\n<!-- /qa-archive -->", 1)


def next_task_number(dev_tasks):
    nums = [int(n) for n in re.findall(r"^## DT-(\d+)", dev_tasks, re.M)]
    return max(nums) + 1 if nums else 1


def append_dev_tasks(dev_tasks, failures):
    if not failures:
        return dev_tasks
    n = next_task_number(dev_tasks)
    blocks = []
    for item_id, notes, meta in failures:
        symptom = (notes.split(".")[0][:90] or item_id) if notes else item_id
        blocks.append(
            "## DT-%03d — %s 🔵\n"
            "- **QA item:** %s\n"
            "- **Failed on:** %s @ %s, %s, %s, %s by %s\n"
            "- **Observed:** %s\n"
            "- **Done when:** %s passes.\n" % (
                n, symptom, item_id,
                meta.get("Branch", "?"), meta.get("Commit", "?"),
                meta.get("Unity version", "?"), meta.get("Platform(s)", "?"),
                meta.get("Date", "?"), meta.get("Tester", "?"),
                notes or "(no note supplied — chase the tester)", item_id))
        n += 1
    body = "\n".join(blocks)
    dev_tasks = dev_tasks.replace(
        "*No open tasks. This file fills in as QA submits results.*\n", "", 1)
    return dev_tasks.replace("\n<!-- /qa-dev-tasks -->", "\n" + body + "\n<!-- /qa-dev-tasks -->", 1)


# ---------------------------------------------------------------- driver

def apply_all(dry_run=False):
    ledger = json.load(open(LEDGER)) if os.path.exists(LEDGER) else {"applied": []}
    applied = set(ledger.get("applied", []))

    sessions = sorted(f for f in os.listdir(RESULTS_DIR)
                      if f.endswith(".md") and f != "TEMPLATE.md" and f not in applied)
    if not sessions:
        print("No new results files to apply.")
        return 0

    backlog = open(BACKLOG).read()
    archive = open(ARCHIVE).read()
    dev_tasks = open(DEV_TASKS).read()
    known = {iid for iid, _ in split_items(backlog)}

    passed, failed, other, unknown = [], [], [], []
    for fname in sessions:
        meta, rows = parse_session(open(os.path.join(RESULTS_DIR, fname)).read())
        for item_id, result, notes in rows:
            if item_id not in known:
                unknown.append((fname, item_id))
                continue
            if result == "PASS":
                backlog = remove_section(backlog, item_id)
                known.discard(item_id)
                passed.append((item_id, meta.get("Commit", "?"), meta.get("Date", "?"),
                               meta.get("Tester", "?"), notes))
            elif result == "FAIL":
                backlog = set_status(backlog, item_id, result, notes, meta)
                failed.append((item_id, notes, meta))
            elif result in ("PARTIAL", "BLOCKED"):
                backlog = set_status(backlog, item_id, result, notes, meta)
                other.append((item_id, result))

    archive = append_archive(archive, passed)
    dev_tasks = append_dev_tasks(dev_tasks, failed)

    print("Sessions applied : %s" % ", ".join(sessions))
    print("PASS  (archived) : %d  %s" % (len(passed), [p[0] for p in passed]))
    print("FAIL  (dev tasks): %d  %s" % (len(failed), [f[0] for f in failed]))
    print("PARTIAL/BLOCKED  : %d  %s" % (len(other), other))
    if unknown:
        print("UNKNOWN ids (ignored, check for typos): %s" % unknown)
    if dry_run:
        print("\n--dry-run: nothing written.")
        return 0

    open(BACKLOG, "w").write(backlog)
    open(ARCHIVE, "w").write(archive)
    open(DEV_TASKS, "w").write(dev_tasks)
    ledger["applied"] = sorted(applied | set(sessions))
    json.dump(ledger, open(LEDGER, "w"), indent=2)
    print("\nWrote QA_BACKLOG.md, ARCHIVE.md, DEV_TASKS.md and .applied.json.")
    return 0


# ---------------------------------------------------------------- selftest

SAMPLE_BACKLOG = """# Backlog
## Priority 0
### QA-ONE ⬜ — first
**Source:** PR #1.
1. Do a thing.

### QA-TWO ⬜ — second
**Source:** PR #2.
1. Do another thing.

---
## Priority 2
### QA-THREE ⬜ — third
Body.
"""

SAMPLE_RESULTS = """| Tester | Ada |
| Date | 2026-08-06 |
| Branch | bleeding-edge |
| Commit | abc1234 |
| Unity version | 6000.0.1 |
| Platform(s) | Editor |
<!-- qa-results-table -->
| ID | Result | Notes |
|---|---|---|
| QA-ONE | PASS |  |
| QA-TWO | FAIL | Step 2: console error XYZ. |
| QA-THREE | BLOCKED | build broken |
| QA-NOPE | PASS | typo id |
<!-- /qa-results-table -->
"""


def selftest():
    meta, rows = parse_session(SAMPLE_RESULTS)
    assert meta["Tester"] == "Ada" and meta["Commit"] == "abc1234", meta
    assert rows == [("QA-ONE", "PASS", ""),
                    ("QA-TWO", "FAIL", "Step 2: console error XYZ."),
                    ("QA-THREE", "BLOCKED", "build broken"),
                    ("QA-NOPE", "PASS", "typo id")], rows

    ids = [i for i, _ in split_items(SAMPLE_BACKLOG)]
    assert ids == ["QA-ONE", "QA-TWO", "QA-THREE"], ids

    b = remove_section(SAMPLE_BACKLOG, "QA-ONE")
    assert "QA-ONE" not in b and "QA-TWO" in b and "QA-THREE" in b

    b = set_status(b, "QA-TWO", "FAIL", "Step 2: console error XYZ.", meta)
    assert "### QA-TWO 🔴" in b, b
    assert "**Last QA:** FAIL on `abc1234`" in b, b
    assert "Do another thing" in b, "body must survive re-marking"

    # re-marking twice must not stack note lines
    b2 = set_status(b, "QA-TWO", "PARTIAL", "second pass", meta)
    assert b2.count("**Last QA:**") == 1, b2
    assert "### QA-TWO 🟡" in b2

    a = append_archive("x\n\n<!-- /qa-archive -->\n",
                       [("QA-ONE", "abc1234", "2026-08-06", "Ada", "")])
    assert "| QA-ONE | `abc1234` | 2026-08-06 | Ada |  |" in a, a

    d = append_dev_tasks("head\n<!-- qa-dev-tasks -->\n\n<!-- /qa-dev-tasks -->\n",
                         [("QA-TWO", "Step 2: console error XYZ.", meta)])
    assert "## DT-001 — Step 2: console error XYZ 🔵" in d, d
    assert next_task_number(d) == 2

    print("selftest: 8/8 checks passed")
    return 0


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    sys.exit(selftest() if args.selftest else apply_all(args.dry_run))
