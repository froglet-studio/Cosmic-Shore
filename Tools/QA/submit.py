#!/usr/bin/env python3
"""Submit a QA session — the "Submit" button on the results form.

Treats a results file the way a web form treats its fields: it checks the required
ones, refuses while anything is missing, says exactly what to fix, and only then
publishes. Publishing means stamping the file's current content into the ledger
(`Docs/QA/.applied.json`); `apply_results.py` will not touch a session that has
not been stamped, and will not touch rows added after the stamp.

There is no rename step. A session file keeps one name from creation to merge.

Multi-day sessions are the expected case: add rows, run this, repeat. Each run
publishes whatever is new. An already-published verdict is frozen — **a retest is
a new session file, never an edit.**

Usage:
    python3 Tools/QA/submit.py                 # newest unsubmitted session
    python3 Tools/QA/submit.py <file.md>       # a specific one
    python3 Tools/QA/submit.py --check         # validate only, never publish
    python3 Tools/QA/submit.py --selftest      # built-in tests, touch nothing
"""

import argparse
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from apply_results import (  # noqa: E402
    ARCHIVE, BACKLOG, RESULTS_DIR, ROOT, VALID,
    load_ledger, parse_session, row_hash, split_items, stamp_submitted, utf8_open,
)

REQUIRED_META = ("Tester", "Date", "Commit", "Unity version", "Platform(s)")
NEEDS_NOTE = ("FAIL", "PARTIAL", "BLOCKED")
SHORT_NOTE = 40


class Problem(object):
    """One validation finding. Blocking ones stop the submit; warnings do not."""

    def __init__(self, blocking, where, what, fix=""):
        self.blocking, self.where, self.what, self.fix = blocking, where, what, fix

    def render(self):
        mark = "✗" if self.blocking else "⚠"
        line = "  %s %-22s %s" % (mark, self.where, self.what)
        return line + ("\n      → " + self.fix if self.fix else "")


def git_head():
    try:
        return subprocess.check_output(["git", "rev-parse", "--short", "HEAD"],
                                       cwd=ROOT, stderr=subprocess.DEVNULL
                                       ).decode().strip()
    except Exception:
        return None


def table_rows(text):
    """Every row of the results table, INCLUDING ones with a blank/invalid verdict.

    parse_session() drops those by design; the whole point of this tool is to see
    them, because a misspelled verdict is otherwise lost without a word.
    """
    fenced = re.search(r"<!--\s*qa-results-table\s*-->(.*?)<!--\s*/qa-results-table\s*-->",
                       text, re.S)
    rows = []
    for line in (fenced.group(1) if fenced else "").splitlines():
        if "|" not in line:
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2 or not cells[0].strip("`* ").startswith("QA-"):
            continue
        rows.append((cells[0].strip("`* "), cells[1].strip("*` "),
                     cells[2] if len(cells) > 2 else ""))
    return rows, fenced is not None


def validate(text, fname, known, archived, applied_items, head=None):
    problems = []
    meta, _ = parse_session(text)

    for key in REQUIRED_META:
        if not meta.get(key) or meta[key].lower().startswith("your name"):
            problems.append(Problem(True, key, "is empty",
                                    "fill it in at the top of the file"))

    if head and meta.get("Commit") and meta["Commit"] != head:
        problems.append(Problem(
            True, "Commit", "says %s but you have %s checked out" % (meta["Commit"], head),
            "a result recorded against the wrong build sends engineering into the "
            "wrong diff. If you tested what is checked out now, re-run with "
            "--accept-head; otherwise check out the build you actually tested"))

    rows, fenced = table_rows(text)
    if not fenced:
        problems.append(Problem(
            True, "results table", "the qa-results-table markers are missing",
            "restore <!-- qa-results-table --> and <!-- /qa-results-table --> around "
            "the table; without them the whole file is parsed and scratch notes "
            "become verdicts"))
    if not rows:
        problems.append(Problem(True, "results table", "has no item rows",
                                "add one row per item you ran"))

    seen = set()
    for item_id, verdict, notes in rows:
        where = item_id
        if item_id in seen:
            problems.append(Problem(True, where, "appears twice",
                                    "one row per item"))
        seen.add(item_id)

        if not verdict:
            problems.append(Problem(
                True, where, "has no verdict",
                "fill it in, or delete the row if you did not run this item"))
        elif verdict.upper() not in VALID:
            problems.append(Problem(
                True, where, "verdict %r is not valid" % verdict,
                "use exactly one of PASS / FAIL / PARTIAL / BLOCKED / SKIP "
                "(anything else is silently dropped when the results are applied)"))
        elif verdict.upper() in NEEDS_NOTE and not notes.strip():
            problems.append(Problem(
                True, where, "is %s but Notes is empty" % verdict.upper(),
                "say which step number failed and what you saw — this text becomes "
                "the dev task an engineer picks up cold"))

        v = verdict.upper()
        prior = applied_items.get(item_id)
        if prior is not None:
            # Leaving a finished row in place across a multi-day session is the
            # NORMAL case and must stay silent. Only a row whose verdict or notes
            # CHANGED is the forbidden edit.
            if prior.get("row") != row_hash(v, notes):
                problems.append(Problem(
                    True, where, "was already submitted as %s and has been edited"
                    % prior.get("verdict"),
                    "an applied verdict is frozen. A retest is a NEW session file, "
                    "never an edit — restore the row, or start a new session"))
            continue
        if item_id not in known:
            hint = ("it already passed and left the backlog (see ARCHIVE.md) — a "
                    "retest belongs in a new session file"
                    if item_id in archived else
                    "copy the ID from QA_BACKLOG.md")
            problems.append(Problem(True, where, "is not an open backlog item", hint))

        if v == "FAIL":
            if "evidence/" not in notes and "attach" not in notes.lower():
                problems.append(Problem(
                    False, where, "is a FAIL with no evidence file referenced",
                    "a Console screenshot in Docs/QA/RESULTS/evidence/%s/ is usually "
                    "enough — not required, but it is what makes a failure "
                    "reproducible" % fname.replace(".md", "")))
            elif len(notes.strip()) < SHORT_NOTE:
                problems.append(Problem(False, where, "has a very short FAIL note"))
    return problems, rows


def write_receipt(path, total, new):
    """Stamp a human-readable 'Submitted' row into the session's metadata table.

    The machine's source of truth is the ledger hash; this row exists so the file
    is self-describing when someone reads it in a diff months later. Written
    BEFORE the content is hashed, so the stamp covers the receipt too.
    """
    import datetime
    value = "yes — %s · %d verdict(s), %d new this run" % (
        datetime.datetime.now().strftime("%Y-%m-%d %H:%M"), total, new)
    row = "| Submitted | %s |" % value
    text = utf8_open(path).read()
    if re.search(r"^\|\s*Submitted\s*\|.*\|\s*$", text, re.M):
        text = re.sub(r"^\|\s*Submitted\s*\|.*\|\s*$", row, text, count=1, flags=re.M)
    elif re.search(r"^\|\s*Platform\(s\)\s*\|.*\|\s*$", text, re.M):
        text = re.sub(r"^(\|\s*Platform\(s\)\s*\|.*\|\s*)$", r"\1\n" + row,
                      text, count=1, flags=re.M)
    else:
        return False
    utf8_open(path, "w").write(text)
    return True


def newest_unsubmitted():
    ledger = load_ledger()
    cands = []
    for f in sorted(os.listdir(RESULTS_DIR)):
        if not f.endswith(".md") or f == "TEMPLATE.md":
            continue
        entry = ledger["sessions"].get(f, {})
        text = utf8_open(os.path.join(RESULTS_DIR, f)).read()
        from apply_results import file_hash
        if entry.get("submitted_hash") != file_hash(text):
            cands.append(f)
    return cands[-1] if cands else None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("file", nargs="?")
    ap.add_argument("--check", action="store_true",
                    help="validate only; never publish")
    ap.add_argument("--accept-head", action="store_true",
                    help="record the currently checked-out commit as the build you "
                         "tested (use only if that is true)")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args(argv)
    if args.selftest:
        return selftest()

    fname = args.file and os.path.basename(args.file) or newest_unsubmitted()
    if not fname:
        print("Nothing to submit — every session file is already up to date.")
        return 0
    path = os.path.join(RESULTS_DIR, fname)
    if not os.path.exists(path):
        print("No such session file: %s" % path)
        return 2

    head = git_head()
    if args.accept_head and head:
        t = utf8_open(path).read()
        if re.search(r"^\|\s*Commit\s*\|.*\|\s*$", t, re.M):
            utf8_open(path, "w").write(re.sub(r"^\|\s*Commit\s*\|.*\|\s*$",
                                         "| Commit | `%s` |" % head, t, count=1, flags=re.M))
            print("Commit row set to %s (the build currently checked out)." % head)

    text = utf8_open(path).read()
    known = {i for i, _ in split_items(utf8_open(BACKLOG).read())}
    archived = set(re.findall(r"^\|\s*(QA-[A-Z0-9-]+)\s*\|", utf8_open(ARCHIVE).read(), re.M))
    applied = load_ledger()["sessions"].get(fname, {}).get("items", {})

    print("\nChecking %s" % os.path.relpath(path, ROOT))
    problems, rows = validate(text, fname, known, archived, applied, head)
    blocking = [p for p in problems if p.blocking]

    new_rows = [r for r in rows if r[0] not in applied]
    if not problems:
        print("  ✓ %d row(s), all required fields present" % len(rows))
    for p in problems:
        print(p.render())

    if blocking:
        print("\nNOT SUBMITTED — %d problem(s) to fix." % len(blocking))
        return 1
    if args.check:
        print("\n--check: valid, nothing published.")
        return 0

    write_receipt(path, len(rows), len(new_rows))
    stamp_submitted(path)
    print("\nSUBMITTED — %d new verdict(s) ready to apply." % len(new_rows))
    print("Next: python3 Tools/QA/apply_results.py --dry-run   (then ask Claude to "
          "apply and push, or commit the file yourself)")
    return 0


# ---------------------------------------------------------------- selftest

def selftest():
    known, archived = {"QA-ONE", "QA-TWO"}, {"QA-OLD"}

    def check(rows, applied=None, head=None, meta_ok=True, markers=True):
        meta = ("| Tester | Ada |\n| Date | 2026-08-14 |\n| Commit | abc1234 |\n"
                "| Unity version | 6000.3.17f1 |\n| Platform(s) | Editor |\n"
                if meta_ok else "| Tester |  |\n")
        body = "".join("| %s | %s | %s |\n" % r for r in rows)
        text = meta + (("<!-- qa-results-table -->\n%s<!-- /qa-results-table -->\n" % body)
                       if markers else body)
        return validate(text, "s.md", known, archived, applied or {}, head)[0]

    def blockers(ps):
        return [p.what for p in ps if p.blocking]

    # A clean session passes with nothing to say.
    assert check([("QA-ONE", "PASS", "")]) == []

    # The silent-drop trap is caught HERE instead of vanishing later.
    assert "not valid" in blockers(check([("QA-ONE", "PASSED", "")]))[0]

    # A blank verdict is a prompt, not a dropped row.
    assert "no verdict" in blockers(check([("QA-ONE", "", "")]))[0]

    # Anything that is not PASS must carry a note.
    assert "Notes is empty" in blockers(check([("QA-ONE", "FAIL", "")]))[0]
    assert blockers(check([("QA-ONE", "PASS", "")])) == []

    # Wrong build is blocking; matching build is silent.
    assert "checked out" in blockers(check([("QA-ONE", "PASS", "")], head="zzz9999"))[0]
    assert blockers(check([("QA-ONE", "PASS", "")], head="abc1234")) == []

    # Missing markers and missing metadata both block.
    assert any("markers" in b for b in blockers(check([("QA-ONE", "PASS", "")],
                                                      markers=False)))
    assert any("is empty" in b for b in blockers(check([("QA-ONE", "PASS", "")],
                                                       meta_ok=False)))

    # The freeze rule, stated at the form rather than after the fact.
    ps = check([("QA-TWO", "PASS", "retested")],
               applied={"QA-TWO": {"verdict": "FAIL", "row": row_hash("FAIL", "old")}})
    assert "already submitted as FAIL" in blockers(ps)[0]
    assert "NEW session file" in [p.fix for p in ps if p.blocking][0]

    # An UNCHANGED finished row is silent — that is the multi-day workflow.
    assert check([("QA-ONE", "PASS", "done")],
                 applied={"QA-ONE": {"verdict": "PASS",
                                     "row": row_hash("PASS", "done")}}) == []

    # An archived id gets the retest hint, not a bare "unknown".
    assert "ARCHIVE.md" in [p.fix for p in check([("QA-OLD", "PASS", "")])
                            if p.blocking][0]
    # A typo'd id gets the copy-paste hint.
    assert "QA_BACKLOG.md" in [p.fix for p in check([("QA-NOPE", "PASS", "")])
                               if p.blocking][0]

    # Duplicate rows block.
    assert any("twice" in b for b in
               blockers(check([("QA-ONE", "PASS", ""), ("QA-ONE", "FAIL", "x")])))

    # Evidence is a WARNING, never a blocker — a hard gate teaches people to type
    # junk to get past it, and some failures need no screenshot.
    ps = check([("QA-ONE", "FAIL", "Step 3: NullReferenceException in Prism.Explode "
                                   "at line 214, full trace in the console")])
    assert blockers(ps) == [] and len(ps) == 1 and not ps[0].blocking

    print("submit selftest: 17/17 checks passed")
    return 0


def _quiet_sigpipe():
    """Piping into `head` must not print a traceback."""
    try:
        import signal
        signal.signal(signal.SIGPIPE, signal.SIG_DFL)
    except (ImportError, AttributeError, ValueError):
        pass


if __name__ == "__main__":
    _quiet_sigpipe()
    sys.exit(main())
