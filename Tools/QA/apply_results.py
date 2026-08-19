#!/usr/bin/env python3
"""Apply submitted QA results to the backlog, archive and dev-task list.

This is the deterministic half of the QA loop (`Docs/QA/README.md`). It reads every
`Docs/QA/RESULTS/*.md` session file that has not been applied yet and rewrites:

  Docs/QA/QA_BACKLOG.md   PASS items removed; others re-marked with their status
  Docs/QA/ARCHIVE.md      one row per PASS
  Docs/QA/DEV_TASKS.md    one task per FAIL

A session file is applied only when `Tools/QA/submit.py` has stamped it, and only
the rows that have not been applied before — so a tester can keep adding items to
one file across a multi-day session and publish as they go.

Two rules the ledger (Docs/QA/.applied.json) enforces:

  * Nothing is applied unless the file's current content is exactly what was
    submitted. Editing a submitted file parks it until submit.py runs again, so a
    half-written row can never reach DEV_TASKS.md.
  * An applied verdict is FROZEN. **A retest is a new session file, never an
    edit** — a changed row is reported loudly instead of being silently ignored,
    because a silent ignore is the failure mode this whole loop exists to remove.

Usage:
    python3 Tools/QA/apply_results.py            # apply, write files
    python3 Tools/QA/apply_results.py --dry-run  # report only
    python3 Tools/QA/apply_results.py --selftest # run built-in tests, touch nothing
"""

import argparse
import hashlib
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


def utf8_open(path, mode="r"):
    """Every file these tools touch is UTF-8, whatever the OS locale says.

    A bare open() uses the locale encoding — cp1252 on Windows — which mojibakes
    every em-dash and status glyph on READ (the QA window showed 'â€"' for '—')
    and raises UnicodeEncodeError on WRITE the moment a ⬜ reaches the backlog.
    All three QA tools route file I/O through here.
    """
    return open(path, mode, encoding="utf-8")


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


# ---------------------------------------------------------------- ledger

LEDGER_VERSION = 2


def file_hash(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def row_hash(result, notes):
    """Identity of a single submitted verdict. Changing either half changes it."""
    return hashlib.sha256(("%s\x00%s" % (result, notes)).encode("utf-8")).hexdigest()[:16]


def load_ledger():
    """Read the ledger, upgrading a v1 (flat filename list) in memory.

    v1 recorded only 'this filename was read', which is why a half-filled session
    was consumed and then never applied again. v2 records, per session, the hash
    submit.py stamped, the hash last applied, and the identity of every applied
    row — so a file can grow across a multi-day session while an already-applied
    verdict stays frozen.
    """
    if not os.path.exists(LEDGER):
        return {"version": LEDGER_VERSION, "sessions": {}}
    data = json.load(utf8_open(LEDGER))
    if "sessions" in data:
        data.setdefault("version", LEDGER_VERSION)
        return data
    # v1 -> v2: keep the filenames, but with no applied rows they are re-read once.
    return {"version": LEDGER_VERSION,
            "sessions": {f: {"submitted_hash": None, "applied_hash": None, "items": {}}
                         for f in data.get("applied", [])}}


def save_ledger(ledger):
    ledger["sessions"] = {k: ledger["sessions"][k] for k in sorted(ledger["sessions"])}
    json.dump(ledger, utf8_open(LEDGER, "w"), indent=2, sort_keys=False)
    utf8_open(LEDGER, "a").write("\n")


def stamp_submitted(path):
    """Record 'this exact content is submitted'. Called by submit.py once it validates.

    Submission state lives in the ledger, not in the filename, so a session file
    keeps one name from creation to merge — no rename step for a tester to forget.
    """
    fname = os.path.basename(path)
    ledger = load_ledger()
    entry = ledger["sessions"].setdefault(
        fname, {"submitted_hash": None, "applied_hash": None, "items": {}})
    entry["submitted_hash"] = file_hash(utf8_open(path).read())
    save_ledger(ledger)
    return entry


def submitted_state(entry, text):
    """Why (or whether) this session file is applicable right now."""
    h = file_hash(text)
    if entry.get("submitted_hash") is None:
        return "unsubmitted", h
    if entry["submitted_hash"] != h:
        return "edited-since-submit", h
    if entry.get("applied_hash") == h:
        return "already-applied", h
    return "ready", h


# ---------------------------------------------------------------- driver

def apply_all(dry_run=False):
    ledger = load_ledger()
    files = sorted(f for f in os.listdir(RESULTS_DIR)
                   if f.endswith(".md") and f != "TEMPLATE.md")

    backlog = utf8_open(BACKLOG).read()
    archive = utf8_open(ARCHIVE).read()
    dev_tasks = utf8_open(DEV_TASKS).read()
    known = {iid for iid, _ in split_items(backlog)}

    passed, failed, other, unknown, frozen, skipped = [], [], [], [], [], []
    touched = []

    for fname in files:
        entry = ledger["sessions"].setdefault(
            fname, {"submitted_hash": None, "applied_hash": None, "items": {}})
        text = utf8_open(os.path.join(RESULTS_DIR, fname)).read()
        state, h = submitted_state(entry, text)
        if state != "ready":
            skipped.append((fname, state))
            continue

        meta, rows = parse_session(text)
        applied_here = 0
        for item_id, result, notes in rows:
            rh = row_hash(result, notes)
            prior = entry["items"].get(item_id)
            if prior is not None:
                # A retest is a NEW session file, never an edit — an applied
                # verdict is frozen. Say so rather than ignoring it silently.
                if prior.get("row") != rh:
                    frozen.append((fname, item_id, prior.get("verdict"), result))
                continue
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
            else:
                continue  # SKIP records nothing
            entry["items"][item_id] = {"verdict": result, "row": rh,
                                       "date": meta.get("Date", "?")}
            applied_here += 1
        if applied_here:
            touched.append("%s (+%d)" % (fname, applied_here))
        entry["applied_hash"] = h

    archive = append_archive(archive, passed)
    dev_tasks = append_dev_tasks(dev_tasks, failed)

    if not touched and not frozen:
        print("No new verdicts to apply.")
        for fname, state in skipped:
            if state != "already-applied":
                print("  %-34s %s" % (fname, EXPLAIN[state]))
        return 0

    print("Sessions applied : %s" % (", ".join(touched) or "none"))
    print("PASS  (archived) : %d  %s" % (len(passed), [p[0] for p in passed]))
    print("FAIL  (dev tasks): %d  %s" % (len(failed), [f[0] for f in failed]))
    print("PARTIAL/BLOCKED  : %d  %s" % (len(other), other))
    for fname, state in skipped:
        if state != "already-applied":
            print("SKIPPED %s — %s" % (fname, EXPLAIN[state]))
    for fname, item_id, was, now in frozen:
        print("FROZEN  %s: %s was applied as %s and cannot be changed to %s in place.\n"
              "        A retest is a NEW session file, never an edit." %
              (fname, item_id, was, now))
    if unknown:
        print("UNKNOWN ids (ignored, check for typos): %s" % unknown)
    if dry_run:
        print("\n--dry-run: nothing written.")
        return 0

    utf8_open(BACKLOG, "w").write(backlog)
    utf8_open(ARCHIVE, "w").write(archive)
    utf8_open(DEV_TASKS, "w").write(dev_tasks)
    save_ledger(ledger)
    print("\nWrote QA_BACKLOG.md, ARCHIVE.md, DEV_TASKS.md and .applied.json.")
    return 0


EXPLAIN = {
    "unsubmitted": "not submitted yet (run: python3 Tools/QA/submit.py)",
    "edited-since-submit": "edited since it was submitted — re-run submit.py to publish the new rows",
    "already-applied": "no change since last apply",
}


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

    n = selftest_lifecycle()
    print("selftest: %d/%d checks passed" % (8 + n, 8 + n))
    return 0


def _session(rows, submitted=True):
    body = "".join("| %s | %s | %s |\n" % r for r in rows)
    return ("| Tester | Ada |\n| Date | 2026-08-14 |\n| Commit | abc1234 |\n"
            "| Submitted | %s |\n<!-- qa-results-table -->\n| ID | Result | Notes |\n"
            "|---|---|---|\n%s<!-- /qa-results-table -->\n"
            % ("yes" if submitted else "no", body))


def selftest_lifecycle():
    """End-to-end: the multi-day session, the burn that used to happen, the freeze."""
    global QA_DIR, BACKLOG, ARCHIVE, DEV_TASKS, RESULTS_DIR, LEDGER
    import tempfile, contextlib, io
    saved = (QA_DIR, BACKLOG, ARCHIVE, DEV_TASKS, RESULTS_DIR, LEDGER)
    tmp = tempfile.mkdtemp()
    try:
        QA_DIR = tmp
        BACKLOG = os.path.join(tmp, "QA_BACKLOG.md")
        ARCHIVE = os.path.join(tmp, "ARCHIVE.md")
        DEV_TASKS = os.path.join(tmp, "DEV_TASKS.md")
        RESULTS_DIR = os.path.join(tmp, "RESULTS")
        LEDGER = os.path.join(tmp, ".applied.json")
        os.mkdir(RESULTS_DIR)
        utf8_open(BACKLOG, "w").write(SAMPLE_BACKLOG)
        utf8_open(ARCHIVE, "w").write("head\n\n<!-- /qa-archive -->\n")
        utf8_open(DEV_TASKS, "w").write("head\n<!-- qa-dev-tasks -->\n\n<!-- /qa-dev-tasks -->\n")
        sess = os.path.join(RESULTS_DIR, "2026-08-14-ada.md")

        def run():
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                apply_all()
            return buf.getvalue()

        # Day 1, still working: not submitted -> nothing happens, and it says why.
        utf8_open(sess, "w").write(_session([("QA-ONE", "PASS", "")], submitted=False))
        out = run()
        assert "not submitted yet" in out, out
        assert "### QA-ONE ⬜" in utf8_open(BACKLOG).read(), "unsubmitted file must not apply"

        # Day 1, submitted: the one finished row lands.
        utf8_open(sess, "w").write(_session([("QA-ONE", "PASS", "")]))
        stamp_submitted(sess)
        out = run()
        assert "QA-ONE" in out and "QA-ONE" not in utf8_open(BACKLOG).read(), out

        # Re-running changes nothing (idempotent).
        assert "No new verdicts" in run()

        # Day 2, appended but NOT re-submitted: parked, with the reason.
        utf8_open(sess, "w").write(_session([("QA-ONE", "PASS", ""),
                                        ("QA-TWO", "FAIL", "Step 3: NRE in Prism.Explode")]))
        out = run()
        assert "edited since it was submitted" in out, out
        assert "### QA-TWO ⬜" in utf8_open(BACKLOG).read(), "unsubmitted edit must not apply"

        # Day 2, re-submitted: ONLY the new row applies. This is the burn that
        # used to swallow a whole finished session under the v1 ledger.
        stamp_submitted(sess)
        out = run()
        assert "QA-TWO" in out, out
        b = utf8_open(BACKLOG).read()
        assert "### QA-TWO 🔴" in b, b
        assert "DT-001" in utf8_open(DEV_TASKS).read()
        assert utf8_open(ARCHIVE).read().count("QA-ONE") == 1, "PASS must not be re-archived"

        # A retest is a NEW file, never an edit: changing an applied row is frozen,
        # reported, and does not touch the backlog.
        utf8_open(sess, "w").write(_session([("QA-ONE", "PASS", ""),
                                        ("QA-TWO", "PASS", "retested, works now")]))
        stamp_submitted(sess)
        out = run()
        assert "FROZEN" in out and "retest is a NEW session file" in out, out
        assert "### QA-TWO 🔴" in utf8_open(BACKLOG).read(), "frozen row must not be re-applied"
        assert utf8_open(DEV_TASKS).read().count("## DT-") == 1, "no duplicate dev task"

        # The v1 ledger upgrades in place rather than exploding.
        json.dump({"applied": ["old-session.md"]}, utf8_open(LEDGER, "w"))
        assert load_ledger()["sessions"]["old-session.md"]["items"] == {}
        return 9
    finally:
        QA_DIR, BACKLOG, ARCHIVE, DEV_TASKS, RESULTS_DIR, LEDGER = saved
        __import__("shutil").rmtree(tmp, ignore_errors=True)


def _quiet_sigpipe():
    """Piping into `head` must not print a traceback."""
    try:
        import signal
        signal.signal(signal.SIGPIPE, signal.SIG_DFL)
    except (ImportError, AttributeError, ValueError):
        pass


if __name__ == "__main__":
    _quiet_sigpipe()
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    sys.exit(selftest() if args.selftest else apply_all(args.dry_run))
