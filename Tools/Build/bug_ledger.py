#!/usr/bin/env python3
"""
The agent's side of the Bug Ledger (FrogletTools > Diagnostics > Bug Ledger).

The editor captures every console error into BugLedger/local/ (gitignored) and, with
"Auto-publish after each play test" on, pushes the material changes to
BugLedger/shared/issues/ on the current branch. This script is how a Claude session
(the /bug-ledger skill) reads that set and hands a fix back:

    python3 Tools/Build/bug_ledger.py list                 # open issues, worst first
    python3 Tools/Build/bug_ledger.py list --state all --json
    python3 Tools/Build/bug_ledger.py show <id>            # the full captured record
    python3 Tools/Build/bug_ledger.py mark-fixed <id> --note "what was wrong, what changed"
    python3 Tools/Build/bug_ledger.py --self-test

mark-fixed writes the SHARED file as `validating`, stamped with a fresh fixedUtc. The
editor adopts it on its next sync (domain reload, play-mode entry, or Refresh) exactly
as if Mark Fixed had been pressed on that machine: the issue validates over its own
cleanSessionsRequired play runs and closes itself, or reopens with a regression count
if the error fires again — which the next auto-publish sends straight back here.

The files are written byte-for-byte the way BugLedgerIssue.ToJson writes them (stable
field order, one field per line, C#'s escaping), so a round trip through this script
and the editor is not a diff. --self-test holds that.

Reads local/issues too when it exists (a session running on the developer's own
machine): the local copy wins, the same rule the editor applies.
"""

import argparse
import datetime
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LEDGER = os.path.join(REPO, "BugLedger")
SHARED = os.path.join(LEDGER, "shared", "issues")
LOCAL = os.path.join(LEDGER, "local", "issues")

# BugLedgerIssue.ToJson field order. (key, kind) — kind: s string, i int, b bool.
FIELDS = [
    ("id", "s"), ("kind", "s"), ("state", "s"), ("severity", "s"), ("title", "s"),
    ("notes", "s"), ("signature", "s"), ("sample", "s"), ("stack", "s"), ("scope", "s"),
    ("logType", "s"), ("reporter", "s"), ("machine", "s"), ("createdUtc", "s"),
    ("lastSeenUtc", "s"), ("fixedBy", "s"), ("fixedUtc", "s"), ("resolvedUtc", "s"),
    ("resolution", "s"), ("timesSeen", "i"), ("regressions", "i"), ("cleanSessions", "i"),
    ("cleanSessionsRequired", "i"), ("validationPaused", "b"),
]
DEFAULTS = {k: ("" if t == "s" else 0 if t == "i" else False) for k, t in FIELDS}
DEFAULTS.update(kind="auto", state="open", severity="major", cleanSessionsRequired=2)

SEVERITY_RANK = {"blocker": 0, "major": 1, "minor": 2}


# ------------------------------------------------------------------ format

def _escape(s):
    """BugLedgerIssue.Escape: \" \\ \\n \\r \\t named, other control chars as \\u00xx, the rest raw."""
    out = []
    for c in s:
        if c == '"':
            out.append('\\"')
        elif c == "\\":
            out.append("\\\\")
        elif c == "\n":
            out.append("\\n")
        elif c == "\r":
            out.append("\\r")
        elif c == "\t":
            out.append("\\t")
        elif ord(c) < 0x20:
            out.append("\\u%04x" % ord(c))
        else:
            out.append(c)
    return "".join(out)


def to_json(issue):
    lines = ["{", '  "schema": 1,']
    for key, kind in FIELDS:
        v = issue.get(key, DEFAULTS[key])
        if kind == "s":
            lines.append('  "%s": "%s",' % (key, _escape(str(v))))
        elif kind == "i":
            lines.append('  "%s": %d,' % (key, int(v)))
        else:
            lines.append('  "%s": %s' % (key, "true" if v else "false"))
    lines.append("}")
    return "\n".join(lines) + "\n"


def from_json(text):
    raw = json.loads(text)
    issue = dict(DEFAULTS)
    for key, kind in FIELDS:
        if key in raw:
            issue[key] = raw[key]
    return issue if issue.get("id") else None


def now_iso():
    t = datetime.datetime.now(datetime.timezone.utc)
    return t.strftime("%Y-%m-%dT%H:%M:%S.") + "%03dZ" % (t.microsecond // 1000)


# ------------------------------------------------------------------ store

def _read_dir(path):
    issues = {}
    if not os.path.isdir(path):
        return issues
    for name in sorted(os.listdir(path)):
        if not name.endswith(".bug.json"):
            continue
        try:
            with open(os.path.join(path, name), encoding="utf-8") as f:
                issue = from_json(f.read())
        except (OSError, ValueError):
            print("warning: unreadable issue file skipped: %s" % name, file=sys.stderr)
            continue
        if issue:
            issues[issue["id"].lower()] = issue
    return issues


def load_all():
    issues = _read_dir(SHARED)
    issues.update(_read_dir(LOCAL))   # local wins, as in the editor
    return issues


def sort_key(issue):
    return (
        {"open": 0, "validating": 1, "ignored": 2}.get(issue["state"], 3),
        SEVERITY_RANK.get(issue["severity"], 1),
        -int(issue["regressions"]),
        -int(issue["timesSeen"]),
        issue["id"],
    )


# ------------------------------------------------------------------ commands

def cmd_list(args):
    issues = sorted(load_all().values(), key=sort_key)
    if args.state != "all":
        issues = [i for i in issues if i["state"] == args.state]
    if args.json:
        print(json.dumps(issues, indent=2, ensure_ascii=False))
        return 0
    if not issues:
        where = SHARED if os.path.isdir(SHARED) else "(no BugLedger/shared/issues yet)"
        print("No %s issues. Store: %s" % (args.state, where))
        return 0
    for i in issues:
        reg = "  REGRESSED x%d" % i["regressions"] if i["regressions"] else ""
        print("%-14s %-10s %-7s %-9s seen %-5d%s" % (
            i["id"], i["state"], i["severity"], i["scope"] or "-", i["timesSeen"], reg))
        print("    %s" % i["title"])
    print("\n%d issue(s)." % len(issues))
    return 0


def _find(issue_id):
    issues = load_all()
    issue = issues.get(issue_id.lower())
    if issue is None:
        sys.exit("no issue %r in %s" % (issue_id, SHARED))
    return issue


def cmd_show(args):
    issue = _find(args.id)
    for key, _ in FIELDS:
        v = issue[key]
        if isinstance(v, str) and "\n" in v:
            print("%s:\n    %s" % (key, v.replace("\n", "\n    ")))
        else:
            print("%s: %s" % (key, v))
    return 0


def cmd_mark_fixed(args):
    issue = _find(args.id)
    if issue["state"] == "ignored" and not args.force:
        sys.exit("%s is IGNORED (parked by a human) — leave it, or pass --force." % issue["id"])
    if not issue["signature"]:
        print("note: %s has no signature, so no play run can validate it — the editor will show it "
              "validating but only a human Resolve closes it." % issue["id"], file=sys.stderr)

    stamp = now_iso()
    entry = "Fix %s by %s: %s" % (stamp[:10], args.by, args.note.strip())
    issue["notes"] = (issue["notes"].rstrip() + "\n\n" + entry) if issue["notes"].strip() else entry
    issue["state"] = "validating"
    issue["fixedBy"] = args.by
    issue["fixedUtc"] = stamp
    issue["cleanSessions"] = 0
    issue["validationPaused"] = False

    os.makedirs(SHARED, exist_ok=True)
    path = os.path.join(SHARED, issue["id"] + ".bug.json")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(to_json(issue))
    print("%s -> validating (%s)" % (issue["id"], os.path.relpath(path, REPO)))
    return 0


# ------------------------------------------------------------------ self-test

# Exactly what BugLedgerIssue.ToJson emits for an issue carrying every escape class.
_FIXTURE = (
    '{\n'
    '  "schema": 1,\n'
    '  "id": "E-1a2b3c4d5e",\n'
    '  "kind": "auto",\n'
    '  "state": "open",\n'
    '  "severity": "major",\n'
    '  "title": "[PrismOcclusion] vessel \'Urchin(Clone)\' measured 0.53",\n'
    '  "notes": "",\n'
    '  "signature": "[prismocclusion] vessel \'urchin(clone)\' measured #.#",\n'
    '  "sample": "quote \\" backslash \\\\ tab \\t cr \\r bell \\u0007 bs \\u0008 dash \\u2014 raw \u2014",\n'
    '  "stack": "UnityEngine.Debug:LogError (object)\\nCosmicShore.Utility.X:Y ()",\n'
    '  "scope": "PlayMode",\n'
    '  "logType": "Error",\n'
    '  "reporter": "ys",\n'
    '  "machine": "PC",\n'
    '  "createdUtc": "2026-10-15T18:12:24.000Z",\n'
    '  "lastSeenUtc": "2026-10-15T18:19:55.000Z",\n'
    '  "fixedBy": "",\n'
    '  "fixedUtc": "",\n'
    '  "resolvedUtc": "",\n'
    '  "resolution": "",\n'
    '  "timesSeen": 2,\n'
    '  "regressions": 0,\n'
    '  "cleanSessions": 0,\n'
    '  "cleanSessionsRequired": 2,\n'
    '  "validationPaused": false\n'
    '}\n'
)


def self_test():
    global SHARED, LOCAL
    import tempfile

    issue = from_json(_FIXTURE)
    assert issue["sample"].endswith("raw \u2014") and "\u0007" in issue["sample"], "parse lost an escape"
    # \u2014 written escaped by a teammate's tool reads back as the raw char; ToJson writes it
    # raw, so re-serialising is the canonical form, not the fixture — compare canonical forms.
    canonical = to_json(issue)
    assert to_json(from_json(canonical)) == canonical, "round trip is not a fixed point"
    assert '"sample": "quote \\" backslash \\\\ tab \\t cr \\r bell \\u0007 bs \\u0008' in canonical, \
        "escaping drifted from BugLedgerIssue.Escape"
    for line, (key, _) in zip(canonical.splitlines()[2:], FIELDS):
        assert line.startswith('  "%s": ' % key), "field order drifted at %s" % key

    with tempfile.TemporaryDirectory() as tmp:
        SHARED = os.path.join(tmp, "shared", "issues")
        LOCAL = os.path.join(tmp, "local", "issues")
        os.makedirs(SHARED)
        with open(os.path.join(SHARED, "E-1a2b3c4d5e.bug.json"), "w", encoding="utf-8") as f:
            f.write(_FIXTURE)
        cmd_mark_fixed(argparse.Namespace(id="e-1A2B3C4D5E", note="widened the band", by="claude", force=False))
        with open(os.path.join(SHARED, "E-1a2b3c4d5e.bug.json"), encoding="utf-8") as f:
            fixed = from_json(f.read())
        assert fixed["state"] == "validating" and fixed["fixedBy"] == "claude"
        assert fixed["fixedUtc"].endswith("Z") and len(fixed["fixedUtc"]) == 24, fixed["fixedUtc"]
        assert fixed["notes"].endswith("by claude: widened the band")
        assert fixed["timesSeen"] == 2 and fixed["signature"] == issue["signature"], "mark-fixed touched a durable field"

        # Negative control: an ignored issue is a human's call and must not be overwritten.
        fixed["state"] = "ignored"
        with open(os.path.join(SHARED, "E-1a2b3c4d5e.bug.json"), "w", encoding="utf-8") as f:
            f.write(to_json(fixed))
        try:
            cmd_mark_fixed(argparse.Namespace(id="E-1a2b3c4d5e", note="x", by="claude", force=False))
        except SystemExit:
            pass
        else:
            raise AssertionError("mark-fixed overwrote an IGNORED issue")

        # Local wins over shared, as in the editor.
        os.makedirs(LOCAL)
        local = dict(issue, title="local copy")
        with open(os.path.join(LOCAL, "E-1a2b3c4d5e.bug.json"), "w", encoding="utf-8") as f:
            f.write(to_json(local))
        assert load_all()["e-1a2b3c4d5e"]["title"] == "local copy", "local did not win"

    print("bug_ledger.py self-test: OK")
    return 0


def main():
    if "--self-test" in sys.argv[1:]:
        return self_test()
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("list", help="list issues, worst first")
    p.add_argument("--state", default="open", choices=["open", "validating", "ignored", "all"])
    p.add_argument("--json", action="store_true")
    p.set_defaults(func=cmd_list)
    p = sub.add_parser("show", help="print one issue in full")
    p.add_argument("id")
    p.set_defaults(func=cmd_show)
    p = sub.add_parser("mark-fixed", help="publish a fix: the editor validates it over clean play runs")
    p.add_argument("id")
    p.add_argument("--note", required=True, help="root cause and what changed (lands in the issue's notes)")
    p.add_argument("--by", default="claude")
    p.add_argument("--force", action="store_true", help="also mark an IGNORED issue")
    p.set_defaults(func=cmd_mark_fixed)
    args = ap.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
