#!/usr/bin/env python3
"""Machine-facing CLI for a QA session — what the Unity window drives.

The split this file exists to enforce:

    Python owns the FILE FORMAT and the RULES.  C# owns PIXELS.

The editor window never parses or writes a results file and never re-implements a
validation rule; it calls the commands below and renders what comes back. That
keeps every rule in code that self-tests (`--selftest` here, in submit.py and in
apply_results.py) and reduces the un-compilable C# surface to layout, whose
failure mode is visible immediately rather than silent.

Commands:
    state   --json                       everything the window needs to draw itself
    new     --tester X --items A,B,C     create a session file
    set     --item ID --verdict V [--notes N]   add or update one row
    remove  --item ID                    drop a row
    attach  --item ID --file PATH        copy evidence in, reference it from Notes
    submit  [--accept-head]              validate + publish (delegates to submit.py)

Every command prints JSON when given --json, so the window never scrapes prose.
"""

import argparse
import datetime
import json
import os
import re
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import submit as submit_mod  # noqa: E402
from apply_results import (  # noqa: E402
    ARCHIVE, BACKLOG, RESULTS_DIR, ROOT, VALID,
    file_hash, load_ledger, parse_session, split_items,
)

TABLE_OPEN = "<!-- qa-results-table -->"
TABLE_CLOSE = "<!-- /qa-results-table -->"


# ---------------------------------------------------------------- backlog

def backlog_items():
    """[(id, priority, title)] in backlog order — the window's item picker."""
    text = open(BACKLOG).read()
    out, priority = [], "P?"
    for line in text.splitlines():
        m = re.match(r"^## Priority (\d)", line)
        if m:
            priority = "P" + m.group(1)
        m = re.match(r"^### (QA-[A-Z0-9-]+)\s*([⬜🟡🔴⛔])?\s*—?\s*(.*)$", line)
        if m:
            out.append({"id": m.group(1), "priority": priority,
                        "status": m.group(2) or "⬜", "title": m.group(3).strip()})
    return out


def git(*args):
    try:
        return subprocess.check_output(("git",) + args, cwd=ROOT,
                                       stderr=subprocess.DEVNULL).decode().strip()
    except Exception:
        return ""


# ---------------------------------------------------------------- the file

def session_path(name):
    return os.path.join(RESULTS_DIR, os.path.basename(name))


def find_session(name=None):
    if name:
        return session_path(name)
    files = sorted(f for f in os.listdir(RESULTS_DIR)
                   if f.endswith(".md") and f != "TEMPLATE.md")
    return session_path(files[-1]) if files else None


def read_rows(text):
    """Rows exactly as written, blanks and typos included (submit.py's view)."""
    return submit_mod.table_rows(text)[0]


def write_rows(text, rows):
    body = ("| ID | Result | Notes |\n|---|---|---|\n"
            + "".join("| %s | %s | %s |\n" % r for r in rows))
    return re.sub(re.escape(TABLE_OPEN) + r".*?" + re.escape(TABLE_CLOSE),
                  TABLE_OPEN + "\n\n" + body + "\n" + TABLE_CLOSE, text, flags=re.S)


def upsert(path, item, verdict=None, notes=None):
    text = open(path).read()
    rows, found = [], False
    for iid, v, n in read_rows(text):
        if iid == item:
            found = True
            rows.append((iid, verdict if verdict is not None else v,
                         notes if notes is not None else n))
        else:
            rows.append((iid, v, n))
    if not found:
        rows.append((item, verdict or "", notes or ""))
    open(path, "w").write(write_rows(text, rows))


def drop(path, item):
    text = open(path).read()
    open(path, "w").write(
        write_rows(text, [r for r in read_rows(text) if r[0] != item]))


NEW_TEMPLATE = """# QA session results

**Nothing here is published until you press Submit.** Add rows across as many days
as you like and submit whenever you want what you have so far to reach engineering;
only new verdicts are published each time. **A published verdict is frozen — a
retest is a NEW session file, never an edit.** Guide: `Docs/QA/README.md`.

## Session

| Field | Value |
|---|---|
| Tester | {tester} |
| Date | {date} |
| Branch | {branch} |
| Commit | `{commit}` |
| Unity version | {unity} |
| Platform(s) | {platform} |
| Submitted | *written by submit.py — leave alone* |

## Results

<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|
{rows}
<!-- /qa-results-table -->

---

## Working notes

*(Scratch space. Only the table above is read — write freely here as you go; it
becomes the dev task's detail if an item fails.)*

{scratch}
## Evidence

Files live in `Docs/QA/RESULTS/evidence/{stem}/` and are referenced from the Notes
cell of the row they belong to.

## Anything else

*(Feel, tuning opinions, and anything that looked wrong but was not on the list.
These change no item's status, but they are read when the backlog is regenerated
and can become new items.)*
"""


def create(tester, items, unity="", platform="Editor", date=None, name=None):
    date = date or datetime.date.today().isoformat()
    slug = re.sub(r"[^a-z0-9-]+", "-", tester.strip().lower()).strip("-") or "tester"
    fname = name or "%s-%s.md" % (date, slug)
    path = session_path(fname)
    known = {i["id"]: i for i in backlog_items()}
    items = [i for i in items if i in known]
    body = "".join("| %s |  |  |\n" % i for i in items) or "| |  |  |\n"
    scratch = "".join("### %s — %s\n\n_(what you saw)_\n\n" % (i, known[i]["title"])
                      for i in items)
    open(path, "w").write(NEW_TEMPLATE.format(
        tester=tester, date=date, branch=git("rev-parse", "--abbrev-ref", "HEAD"),
        commit=git("rev-parse", "--short", "HEAD"), unity=unity, platform=platform,
        rows=body, scratch=scratch, stem=fname[:-3]))
    os.makedirs(os.path.join(RESULTS_DIR, "evidence", fname[:-3]), exist_ok=True)
    return path


def attach(path, item, src):
    stem = os.path.basename(path)[:-3]
    dest_dir = os.path.join(RESULTS_DIR, "evidence", stem)
    os.makedirs(dest_dir, exist_ok=True)
    base = "%s-%s" % (item, os.path.basename(src))
    shutil.copyfile(src, os.path.join(dest_dir, base))
    ref = "evidence/%s/%s" % (stem, base)
    text = open(path).read()
    for iid, v, n in read_rows(text):
        if iid == item and ref not in n:
            upsert(path, item, notes=(n + " " if n.strip() else "") + ref)
            break
    return ref


# ---------------------------------------------------------------- state

def state(path):
    """One JSON blob describing everything the window draws.

    SHAPED FOR UnityEngine.JsonUtility, which cannot deserialise dictionaries and
    cannot map keys that are not valid C# identifiers ("Unity version"). So the
    metadata is flattened into named fields and each row carries its problems as
    one pre-joined string. Shaping the payload for the consumer keeps a hand-rolled
    JSON parser out of the C# half, which is the half that cannot be compile-tested.
    """
    items = backlog_items()
    head = git("rev-parse", "--short", "HEAD")
    out = {"root": ROOT, "head": head,
           "branch": git("rev-parse", "--abbrev-ref", "HEAD"),
           "backlog": items,
           "sessions": sorted(f for f in os.listdir(RESULTS_DIR)
                              if f.endswith(".md") and f != "TEMPLATE.md"),
           "hasSession": False, "sessionFile": "", "sessionPath": "",
           "tester": "", "date": "", "commit": "", "unity": "", "platform": "",
           "submitted": False, "canSubmit": False, "blocking": 0,
           "rows": [], "problems": [], "verdicts": sorted(VALID)}
    if not path or not os.path.exists(path):
        return out

    text = open(path).read()
    fname = os.path.basename(path)
    entry = load_ledger()["sessions"].get(fname, {})
    applied = entry.get("items", {})
    meta, _ = parse_session(text)
    known = {i["id"] for i in items}
    archived = set(re.findall(r"^\|\s*(QA-[A-Z0-9-]+)\s*\|",
                              open(ARCHIVE).read(), re.M))
    problems, rows = submit_mod.validate(text, fname, known, archived, applied, head)

    by_item = {}
    for p in problems:
        by_item.setdefault(p.where, []).append(p)

    def joined(item):
        return "\n".join(
            (x.what + ((" — " + x.fix) if x.fix else "")) for x in by_item.get(item, []))

    out.update({
        "hasSession": True, "sessionFile": fname, "sessionPath": path,
        "tester": meta.get("Tester", ""), "date": meta.get("Date", ""),
        "commit": meta.get("Commit", ""), "unity": meta.get("Unity version", ""),
        "platform": meta.get("Platform(s)", ""),
        "submitted": entry.get("submitted_hash") == file_hash(text),
        "blocking": sum(1 for x in problems if x.blocking),
        "canSubmit": not any(x.blocking for x in problems),
        "rows": [{"id": i, "verdict": v, "notes": n,
                  "frozen": i in applied,
                  "problem": joined(i),
                  "problemBlocking": any(x.blocking for x in by_item.get(i, []))}
                 for i, v, n in rows],
        "problems": [{"blocking": x.blocking, "where": x.where, "what": x.what,
                      "fix": x.fix} for x in problems
                     if x.where not in {r[0] for r in rows}],
    })
    return out


# ---------------------------------------------------------------- driver

def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("command", choices=("state", "new", "set", "remove", "attach",
                                        "submit", "selftest"))
    ap.add_argument("--file")
    ap.add_argument("--item")
    ap.add_argument("--verdict")
    ap.add_argument("--notes")
    ap.add_argument("--tester")
    ap.add_argument("--items", default="")
    ap.add_argument("--unity", default="")
    ap.add_argument("--platform", default="Editor")
    ap.add_argument("--src")
    ap.add_argument("--accept-head", action="store_true")
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args(argv)

    if a.command == "selftest":
        return selftest()

    path = find_session(a.file)
    if a.command == "new":
        path = create(a.tester or "tester",
                      [s for s in a.items.split(",") if s.strip()],
                      a.unity, a.platform)
    elif a.command == "set":
        upsert(path, a.item, a.verdict, a.notes)
    elif a.command == "remove":
        drop(path, a.item)
    elif a.command == "attach":
        attach(path, a.item, a.src)
    elif a.command == "submit":
        argv2 = [os.path.basename(path)] + (["--accept-head"] if a.accept_head else [])
        code = submit_mod.main(argv2)
        if a.json:
            print(json.dumps(dict(state(path), submitExit=code), indent=1))
        return code

    print(json.dumps(state(path), indent=1) if a.json else
          "ok: %s" % os.path.relpath(path, ROOT))
    return 0


WINDOW_CS = os.path.join(ROOT, "Assets", "_Scripts", "Editor", "QA", "QASessionWindow.cs")


def contract_matches_csharp(sample):
    """The JSON above must match the window's [Serializable] fields exactly.

    JsonUtility fails SILENTLY on a mismatch — an unknown key is ignored and a missing
    one leaves a default — so drift between these two halves would show up as blank
    fields in the UI rather than an error. This is the gate against that. Skipped if
    the C# is absent (nothing to drift from).
    """
    if not os.path.exists(WINDOW_CS):
        return True
    cs = open(WINDOW_CS).read()

    def fields(cls):
        m = re.search(r"public class %s\s*\{(.*?)\n        \}" % cls, cs, re.S)
        return set(re.findall(r"public [\w\[\]<>, ]+? (\w+)\s*[;=]", m.group(1))) if m else None

    top = fields("SessionState")
    if top is None or top != set(sample):
        return False
    for cls, key, blank in (("BacklogItem", "backlog", {"id", "priority", "status", "title"}),
                            ("Row", "rows", {"id", "verdict", "notes", "frozen",
                                             "problem", "problemBlocking"}),
                            ("Problem", "problems", {"blocking", "where", "what", "fix"})):
        f = fields(cls)
        if f is None or f != blank:
            return False
        for row in sample.get(key) or []:
            if set(row) != f:
                return False
    return True


# ---------------------------------------------------------------- selftest

def selftest():
    import tempfile
    import apply_results as ar
    saved = (ar.BACKLOG, ar.ARCHIVE, ar.RESULTS_DIR, ar.LEDGER)
    g = globals()
    saved_g = (g["BACKLOG"], g["ARCHIVE"], g["RESULTS_DIR"])
    tmp = tempfile.mkdtemp()
    try:
        res = os.path.join(tmp, "RESULTS")
        os.mkdir(res)
        bl = os.path.join(tmp, "QA_BACKLOG.md")
        arch = os.path.join(tmp, "ARCHIVE.md")
        open(bl, "w").write(
            "## Priority 0 — gates\n### QA-ONE ⬜ — first thing\nbody\n\n"
            "### QA-TWO ⬜ — second thing\nbody\n\n"
            "## Priority 1 — features\n### QA-THREE ⬜ — third thing\nbody\n")
        open(arch, "w").write("| QA-OLD | `x` | d | t |  |\n")
        for mod in (ar, submit_mod):
            mod.BACKLOG, mod.ARCHIVE, mod.RESULTS_DIR = bl, arch, res
            mod.LEDGER = os.path.join(tmp, ".applied.json")
        g["BACKLOG"], g["ARCHIVE"], g["RESULTS_DIR"] = bl, arch, res

        items = backlog_items()
        assert [i["id"] for i in items] == ["QA-ONE", "QA-TWO", "QA-THREE"], items
        assert items[0]["priority"] == "P0" and items[2]["priority"] == "P1", items
        assert items[0]["title"] == "first thing", items

        p = create("Ada Lovelace", ["QA-ONE", "QA-TWO", "QA-NOPE"], "6000.3.17f1")
        assert os.path.basename(p).endswith("-ada-lovelace.md"), p
        txt = open(p).read()
        assert "| Tester | Ada Lovelace |" in txt
        assert "QA-NOPE" not in txt, "unknown ids must not be written into the form"
        assert txt.count(TABLE_OPEN) == 1 and txt.count(TABLE_CLOSE) == 1

        st = state(p)
        assert [r["id"] for r in st["rows"]] == ["QA-ONE", "QA-TWO"], st["rows"]
        assert st["canSubmit"] is False, "blank verdicts must block"
        assert "no verdict" in st["rows"][0]["problem"]
        assert st["tester"] == "Ada Lovelace" and st["unity"] == "6000.3.17f1"

        upsert(p, "QA-ONE", "PASS", "")
        upsert(p, "QA-TWO", "FAIL", "Step 3: NRE in Prism.Explode with a long note")
        st = state(p)
        assert st["canSubmit"] is True, st["problems"]
        assert st["rows"][0]["verdict"] == "PASS"
        assert not st["submitted"] and st["hasSession"]

        # editing one row must not disturb the other
        upsert(p, "QA-TWO", notes="edited note that is quite long indeed yes")
        st = state(p)
        assert st["rows"][0] == {"id": "QA-ONE", "verdict": "PASS", "notes": "",
                                 "frozen": False, "problem": "",
                                 "problemBlocking": False}, st["rows"][0]
        assert "edited note" in st["rows"][1]["notes"]

        drop(p, "QA-ONE")
        assert [r["id"] for r in state(p)["rows"]] == ["QA-TWO"]

        # adding a row the tester did not start with
        upsert(p, "QA-THREE", "BLOCKED", "no second machine available today")
        assert len(state(p)["rows"]) == 2

        # attach copies the file in and references it from that row only
        src = os.path.join(tmp, "shot.png")
        open(src, "w").write("x")
        ref = attach(p, "QA-TWO", src)
        st = state(p)
        assert ref in st["rows"][0]["notes"] and ref not in st["rows"][1]["notes"]
        assert os.path.exists(os.path.join(res, "evidence",
                                           os.path.basename(p)[:-3],
                                           "QA-TWO-shot.png"))

        # state() on a missing/none session still describes the backlog
        empty = state(None)
        assert empty["hasSession"] is False and len(empty["backlog"]) == 3
        assert empty["rows"] == [] and empty["canSubmit"] is False

        assert contract_matches_csharp(state(p)), "JSON shape drifted from the C# window"

        print("session selftest: 19/19 checks passed")
        return 0
    finally:
        ar.BACKLOG, ar.ARCHIVE, ar.RESULTS_DIR, ar.LEDGER = saved
        submit_mod.BACKLOG, submit_mod.ARCHIVE, submit_mod.RESULTS_DIR = saved[:3]
        submit_mod.LEDGER = saved[3]
        g["BACKLOG"], g["ARCHIVE"], g["RESULTS_DIR"] = saved_g
        __import__("shutil").rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    try:
        import signal
        signal.signal(signal.SIGPIPE, signal.SIG_DFL)
    except (ImportError, AttributeError, ValueError):
        pass
    sys.exit(main())
