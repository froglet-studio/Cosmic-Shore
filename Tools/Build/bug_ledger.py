#!/usr/bin/env python3
"""
The agent's side of the Bug Ledger.

The editor captures every console error into BugLedger/local/ (gitignored). After a play
test that logged a new error it sends, with no question (since 2026-10-16), this
machine's issues and report files to its own branch, origin/bugledger/<user> (under
BugLedger/shared/), without touching the person's working tree or branch. This script is
how a Claude session (the /bug-ledger skill) reads those branches and hands a fix back:

    python3 Tools/Build/bug_ledger.py list                 # open issues, worst first
    python3 Tools/Build/bug_ledger.py list --state all --json
    python3 Tools/Build/bug_ledger.py show <id>            # the full captured record
    python3 Tools/Build/bug_ledger.py mark-fixed <id> --note "what was wrong, what changed"
    python3 Tools/Build/bug_ledger.py collect [--out board.json]   # EVERY origin branch, merged
    python3 Tools/Build/bug_ledger.py file --title "..." --notes "..." --reporter "..."
    python3 Tools/Build/bug_ledger.py --self-test

collect is the daily sweep's input: collaborators publish to whatever branch they are on,
so it reads BugLedger/shared/issues and BugLedger/shared/reports from every
refs/remotes/origin/* branch (git, no checkout), merges each issue across branches (the
worst state and the newest copy win, every branch it lives on is listed) and lists the
report files per collaborator. Run `git fetch origin` first. Its JSON is what the sweep
mirrors into the Team Bug Board artifact.

file creates a hand-filed `C-` issue in shared/ — how a bug filed on the Team Bug Board
(no Unity needed) reaches every editor's ledger once the branch is pulled.

mark-fixed <id> --note ... [--commit <sha>, default HEAD] rewrites the issue as
`validating` ON EVERY origin/bugledger/* BRANCH that carries it (a throwaway index and a
push; HEAD, the index and the working tree are untouched), recording the fix commit. The
sender's editor adopts it at its next sync (editor load, or before a send) only once that
commit is in its HEAD; its next clean play runs then close the issue, and if the error
fires again it reopens and the next "Send to Claude" brings it straight back here. Push
the fix commit first. An issue that only exists in the working tree's shared/ folder (the
older publish path) is written there instead.

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
import secrets
import subprocess
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
    ("lastSeenUtc", "s"), ("fixedBy", "s"), ("fixedUtc", "s"), ("fixCommit", "s"), ("resolvedUtc", "s"),
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
        # Sent from an editor: it lives on a reporter's bugledger/<user> branch, not in this tree.
        for _, branch_issue in _bugledger_copies(issue_id):
            return branch_issue
        sys.exit("no issue %r in %s or on any origin/bugledger/* branch (git fetch origin first)" % (issue_id, SHARED))
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

    commit = args.commit or (_git("rev-parse", "HEAD") or "").strip()
    stamp = now_iso()

    def apply_fix(target):
        entry = "Fix %s by %s (%s): %s" % (stamp[:10], args.by, commit[:10] or "no commit", args.note.strip())
        target["notes"] = (target["notes"].rstrip() + "\n\n" + entry) if target["notes"].strip() else entry
        target.update(state="validating", fixedBy=args.by, fixedUtc=stamp, fixCommit=commit,
                      cleanSessions=0, validationPaused=False)
        return target

    # Where the editor will look: every reporter's bugledger/<user> branch that carries the issue.
    # The machine adopts the fix once `commit` is in its HEAD, then validates it by play runs.
    written = 0
    for branch, ledger_issue in _bugledger_copies(issue["id"]):
        _write_to_branch(branch, "%s/%s.bug.json" % (SHARED_REL, ledger_issue["id"]),
                         to_json(apply_fix(ledger_issue)),
                         "bugledger: %s fixed in %s" % (ledger_issue["id"], commit[:10]))
        print("%s -> validating on %s (fix %s)" % (ledger_issue["id"], branch, commit[:10]))
        written += 1
    if written:
        return 0

    # Legacy: an issue published into the working tree's shared/ folder.
    os.makedirs(SHARED, exist_ok=True)
    path = os.path.join(SHARED, issue["id"] + ".bug.json")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(to_json(apply_fix(issue)))
    print("%s -> validating (%s)" % (issue["id"], os.path.relpath(path, REPO)))
    return 0


# ------------------------------------------------------------------ bugledger/<user> branches

SHARED_REL = "BugLedger/shared/issues"


def _run(args, env=None, stdin=None):
    """git in REPO; returns stdout, raises on failure (unlike _git, which returns None)."""
    full_env = dict(os.environ, **env) if env else None
    r = subprocess.run(["git", "-C", REPO] + list(args), capture_output=True, text=True,
                       encoding="utf-8", errors="replace", env=full_env, input=stdin)
    if r.returncode != 0:
        raise RuntimeError("git %s: %s" % (" ".join(args[:3]), r.stderr.strip()))
    return r.stdout


def _bugledger_copies(issue_id):
    """(branch, issue) for every origin/bugledger/* branch whose ledger holds this issue."""
    out = _git("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin/bugledger")
    for ref in (out or "").split():
        text = _git("show", "%s:%s/%s.bug.json" % (ref, SHARED_REL, issue_id))
        if not text:
            continue
        try:
            issue = from_json(text)
        except ValueError:
            continue
        if issue:
            yield ref[len("origin/"):], issue


def _write_to_branch(branch, repo_path, text, message):
    """Commit one file onto origin/<branch> without touching HEAD, the index or the working
    tree (throwaway GIT_INDEX_FILE), and push it. One retry from a fresh fetch if the editor
    pushed in between."""
    import tempfile
    for attempt in range(2):
        _git("fetch", "-q", "origin", "+refs/heads/%s:refs/remotes/origin/%s" % (branch, branch))
        parent = _run(["rev-parse", "--verify", "origin/%s^{commit}" % branch]).strip()
        with tempfile.TemporaryDirectory() as tmp:
            env = {"GIT_INDEX_FILE": os.path.join(tmp, "index")}
            _run(["read-tree", parent], env=env)
            blob = _run(["hash-object", "-w", "--stdin"], stdin=text).strip()
            _run(["update-index", "--add", "--cacheinfo", "100644,%s,%s" % (blob, repo_path)], env=env)
            tree = _run(["write-tree"], env=env).strip()
        commit = _run(["commit-tree", tree, "-p", parent, "-m", message]).strip()
        try:
            _run(["push", "-q", "origin", "%s:refs/heads/%s" % (commit, branch)])
            return
        except RuntimeError:
            if attempt == 1:
                raise


def cmd_file(args):
    """A hand-filed issue, shaped exactly like BugLedger.ReportCustom's (C-yyyyMMdd-HHmmss-xxxx)."""
    now = datetime.datetime.now(datetime.timezone.utc)
    stamp = now_iso()
    title = (args.title.strip().splitlines() or ["(no title)"])[0][:120]
    issue = dict(DEFAULTS)
    issue.update(
        id="C-%s-%s" % (now.strftime("%Y%m%d-%H%M%S"), secrets.token_hex(2)),
        kind="custom", state="open", severity=args.severity, title=title,
        notes=args.notes.strip(), reporter=args.reporter, machine=args.machine,
        createdUtc=stamp, lastSeenUtc=stamp, timesSeen=1,
    )
    os.makedirs(SHARED, exist_ok=True)
    path = os.path.join(SHARED, issue["id"] + ".bug.json")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(to_json(issue))
    print(issue["id"])
    return 0


# ------------------------------------------------------------------ every branch

STATE_RANK = {"open": 0, "validating": 1, "ignored": 2}


def _git(*args):
    r = subprocess.run(["git", "-C", REPO] + list(args), capture_output=True,
                       text=True, encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else None


def _remote_branches():
    out = _git("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin")
    return [b for b in (out or "").split() if b not in ("origin", "origin/HEAD")]


def collect_board():
    """Every origin branch's published ledger, merged. An issue lives on several branches when
    several collaborators hit it (the id is the signature hash): the WORST state wins (open over
    validating — a fix on one branch has not reached the others), ties go to the newest
    lastSeenUtc, counters take the max, and every branch it is on is listed."""
    issues, reports, blobs = {}, {}, {}
    branches = _remote_branches()
    for ref in branches:
        branch = ref[len("origin/"):]
        listing = _git("ls-tree", "-r", "-l", ref, "--", "BugLedger/shared")
        if not listing:
            continue
        for line in listing.splitlines():
            meta, _, path = line.partition("\t")
            parts = meta.split()
            if len(parts) < 4 or parts[1] != "blob":
                continue
            sha, size = parts[2], parts[3]
            if path.startswith("BugLedger/shared/issues/") and path.endswith(".bug.json"):
                if sha not in blobs:
                    text = _git("cat-file", "blob", sha)
                    try:
                        blobs[sha] = from_json(text) if text else None
                    except ValueError:
                        blobs[sha] = None
                issue = blobs[sha]
                if not issue:
                    continue
                key = issue["id"].lower()
                have = issues.get(key)
                if have is None:
                    merged = dict(issue, branches=[branch])
                else:
                    worse = (STATE_RANK.get(issue["state"], 3), _neg_time(issue["lastSeenUtc"])) < \
                            (STATE_RANK.get(have["state"], 3), _neg_time(have["lastSeenUtc"]))
                    merged = dict(issue if worse else have)
                    merged["branches"] = have["branches"] + [branch]
                    for counter in ("timesSeen", "regressions"):
                        merged[counter] = max(int(issue[counter]), int(have[counter]))
                issues[key] = merged
            elif path.startswith("BugLedger/shared/reports/"):
                rel = path[len("BugLedger/shared/reports/"):].split("/", 2)
                if len(rel) != 3:
                    continue
                user, kind, name = rel
                rkey = (user, kind, name)
                entry = reports.setdefault(rkey, {
                    "user": user, "kind": kind, "name": name, "bytes": int(size) if size.isdigit() else 0,
                    "path": path, "branches": [],
                })
                entry["branches"].append(branch)
    return {
        "generatedUtc": now_iso(),
        "branchesScanned": len(branches),
        "issues": sorted(issues.values(), key=sort_key),
        "reports": sorted(reports.values(), key=lambda r: (r["user"], r["kind"], r["name"]), reverse=False),
    }


def _neg_time(iso):
    """Sort key making NEWER timestamps compare smaller (ISO-8601 Z strings sort lexically)."""
    if not iso:
        return (1,)   # unknown time sorts as the OLDEST
    return tuple(-ord(c) for c in iso)


def cmd_collect(args):
    board = collect_board()
    text = json.dumps(board, indent=2, ensure_ascii=False)
    if args.out:
        with open(args.out, "w", encoding="utf-8", newline="\n") as f:
            f.write(text + "\n")
        print("%d issue(s), %d report file(s) from %d branch(es) -> %s" % (
            len(board["issues"]), len(board["reports"]), board["branchesScanned"], args.out))
    else:
        print(text)
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
    '  "fixCommit": "",\n'
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
        cmd_mark_fixed(argparse.Namespace(id="e-1A2B3C4D5E", note="widened the band", by="claude", force=False,
                                          commit="abc1234def"))
        with open(os.path.join(SHARED, "E-1a2b3c4d5e.bug.json"), encoding="utf-8") as f:
            fixed = from_json(f.read())
        assert fixed["state"] == "validating" and fixed["fixedBy"] == "claude"
        assert fixed["fixedUtc"].endswith("Z") and len(fixed["fixedUtc"]) == 24, fixed["fixedUtc"]
        assert fixed["notes"].endswith("by claude (abc1234def): widened the band"), fixed["notes"]
        assert fixed["fixCommit"] == "abc1234def", "the fix must name its commit, or no editor can gate on it"
        assert fixed["timesSeen"] == 2 and fixed["signature"] == issue["signature"], "mark-fixed touched a durable field"

        # Negative control: an ignored issue is a human's call and must not be overwritten.
        fixed["state"] = "ignored"
        with open(os.path.join(SHARED, "E-1a2b3c4d5e.bug.json"), "w", encoding="utf-8") as f:
            f.write(to_json(fixed))
        try:
            cmd_mark_fixed(argparse.Namespace(id="E-1a2b3c4d5e", note="x", by="claude", force=False, commit="abc"))
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

        # file: a board-filed bug lands as a C- issue the editor's parser accepts.
        cmd_file(argparse.Namespace(title="HUD overlaps\nsecond line", notes="seen on phone",
                                    severity="minor", reporter="caleb", machine="team-bug-board"))
        filed = [n for n in os.listdir(SHARED) if n.startswith("C-")]
        assert len(filed) == 1, filed
        with open(os.path.join(SHARED, filed[0]), encoding="utf-8") as f:
            c = from_json(f.read())
        assert c["kind"] == "custom" and c["state"] == "open" and c["title"] == "HUD overlaps", c
        assert len(c["id"]) == len("C-20261015-181224-abcd") and filed[0] == c["id"] + ".bug.json", c["id"]

    _self_test_collect()
    _self_test_branch_fix()
    print("bug_ledger.py self-test: OK")
    return 0


def _self_test_branch_fix():
    """mark-fixed against a sender's bugledger/<user> branch: the fix lands ON THE BRANCH (with
    its commit) and nothing else moves - not HEAD, not the index, not the working tree."""
    global REPO
    import tempfile

    saved = REPO
    with tempfile.TemporaryDirectory() as tmp:
        origin, work = os.path.join(tmp, "origin.git"), os.path.join(tmp, "work")
        subprocess.run(["git", "init", "-q", "--bare", origin], check=True, capture_output=True)
        subprocess.run(["git", "init", "-q", work], check=True, capture_output=True)

        def g(*a):
            return subprocess.run(["git", "-C", work] + list(a), check=True, capture_output=True,
                                  text=True).stdout

        g("config", "user.name", "t")
        g("config", "user.email", "t@t")
        base = from_json(_FIXTURE)
        rel = "%s/%s.bug.json" % (SHARED_REL, base["id"])
        os.makedirs(os.path.join(work, SHARED_REL))
        with open(os.path.join(work, rel), "w", encoding="utf-8", newline="\n") as f:
            f.write(to_json(base))
        g("add", "-A")
        g("commit", "-q", "-m", "sent from an editor")
        g("remote", "add", "origin", origin)
        g("push", "-q", "origin", "HEAD:refs/heads/bugledger/ys")
        g("fetch", "-q", "origin")
        head_before = g("rev-parse", "HEAD")

        REPO = work
        try:
            cmd_mark_fixed(argparse.Namespace(id=base["id"], note="fixed it", by="claude", force=False,
                                              commit="deadbeef01"))
        finally:
            REPO = saved

        g("fetch", "-q", "origin")
        on_branch = from_json(g("show", "origin/bugledger/ys:" + rel))
        assert on_branch["state"] == "validating" and on_branch["fixCommit"] == "deadbeef01", on_branch
        assert on_branch["timesSeen"] == base["timesSeen"], "the fix must not touch the sighting counters"
        assert g("rev-parse", "HEAD") == head_before, "mark-fixed moved HEAD"
        assert g("status", "--porcelain").strip() == "", "mark-fixed touched the working tree or index"
        with open(os.path.join(work, rel), encoding="utf-8") as f:
            assert from_json(f.read())["state"] == "open", "mark-fixed wrote the working-tree copy"


def _self_test_collect():
    """collect over a throwaway repo with two fake origin branches carrying the SAME issue in two
    states, plus a report file: one merged issue, worst state wins, both branches listed."""
    global REPO
    import tempfile

    saved = REPO
    with tempfile.TemporaryDirectory() as tmp:
        REPO = tmp
        try:
            def git(*a):
                subprocess.run(["git", "-C", tmp, "-c", "user.name=t", "-c", "user.email=t@t"] + list(a),
                               check=True, capture_output=True)

            issues_dir = os.path.join(tmp, "BugLedger", "shared", "issues")
            reports_dir = os.path.join(tmp, "BugLedger", "shared", "reports", "ys", "redlog")
            os.makedirs(issues_dir)
            os.makedirs(reports_dir)
            base = from_json(_FIXTURE)
            path = os.path.join(issues_dir, base["id"] + ".bug.json")

            git("init", "-q")
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(to_json(dict(base, state="validating", timesSeen=9, lastSeenUtc="2026-10-16T00:00:00.000Z")))
            git("add", "-A")
            git("commit", "-q", "-m", "a")
            git("update-ref", "refs/remotes/origin/fixed-branch", "HEAD")

            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(to_json(dict(base, state="open", timesSeen=3)))
            with open(os.path.join(reports_dir, "redconsole_x.log"), "w") as f:
                f.write("red\n")
            git("add", "-A")
            git("commit", "-q", "-m", "b")
            git("update-ref", "refs/remotes/origin/tester-branch", "HEAD")

            board = collect_board()
            assert board["branchesScanned"] == 2, board["branchesScanned"]
            assert len(board["issues"]) == 1, board["issues"]
            merged = board["issues"][0]
            assert merged["state"] == "open", "a fix on one branch hid the open copy on another"
            assert sorted(merged["branches"]) == ["fixed-branch", "tester-branch"], merged["branches"]
            assert merged["timesSeen"] == 9, "counters must take the max across branches"
            assert [(r["user"], r["kind"], r["name"]) for r in board["reports"]] == [("ys", "redlog", "redconsole_x.log")]
        finally:
            REPO = saved


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
    p.add_argument("--commit", help="the commit that carries the fix (default: HEAD). The reporter's "
                                    "editor starts validating only once this commit is in their HEAD")
    p.add_argument("--force", action="store_true", help="also mark an IGNORED issue")
    p.set_defaults(func=cmd_mark_fixed)
    p = sub.add_parser("collect", help="merge BugLedger/shared from every origin branch (fetch first)")
    p.add_argument("--out", help="write the board JSON here instead of stdout")
    p.set_defaults(func=cmd_collect)
    p = sub.add_parser("file", help="file a hand-written C- issue into shared/ (e.g. from the Team Bug Board)")
    p.add_argument("--title", required=True)
    p.add_argument("--notes", default="")
    p.add_argument("--severity", default="major", choices=["blocker", "major", "minor"])
    p.add_argument("--reporter", required=True)
    p.add_argument("--machine", default="team-bug-board")
    p.set_defaults(func=cmd_file)
    args = ap.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
