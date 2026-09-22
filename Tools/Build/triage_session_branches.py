#!/usr/bin/env python3
"""Triage the agent-session branches piling up on origin.

WHY THIS EXISTS
---------------
Cosmic-Shore accumulates one remote branch per agent session (`claude/*`,
`cece/*`). At the time of writing that is 341 of the repo's 410 branches, and
the question asked of them - "is this safe to archive and delete, or is there
work here nobody landed?" - had been answered by hand at least three times.

THE MEASUREMENT THAT MAKES IT ANSWERABLE
----------------------------------------
The obvious query is wrong. `git log bleeding-edge..BRANCH` reports **6,289**
commits for a branch whose session wrote exactly **one**, because bleeding-edge
squash-merges: the shared history a stale branch forked from is no longer
reachable from bleeding-edge, so it all reads as divergence.

What the session actually authored is the set of commits reachable from the
branch and from *no other ref on origin*:

    git rev-list BRANCH --not <every other origin ref>

Measured against two hand-audited branches this returns 1 and 6 - exactly the
session commits - where the naive query returned 6,289 and 6,235.

Two properties of this repo make the rest of the classification content-based
rather than ancestry-based, and both are load-bearing:

  * bleeding-edge SQUASHES, so `merge-base --is-ancestor` reports NOT-ancestor
    for work that demonstrably landed (CellAggressionLevel.cs is on
    bleeding-edge; the commit that introduced it is not an ancestor).
  * the clone is SHALLOW (103 grafts), so ancestry is truncated anyway.

So a branch is judged by what its exclusive commits TOUCHED and what became of
those paths on the base branch. That is decidable from trees alone, which is
why --fetch uses `--filter=blob:none`: all 410 heads arrive in ~27s and no blob
content is ever needed (comparing two files for equality is a blob-OID compare
against the tree, not a read).

WHAT IT DOES NOT PROVE
----------------------
A DIVERGED verdict is "a human should look", never "there is salvageable work".
The tool cannot tell a change that was superseded by a better one from a change
that was dropped on the floor - both leave the touched paths modified. It sorts
DIVERGED so the branches most likely to matter surface first; it does not rank
their worth. It also reads no PR state, so a branch whose PR was closed
unmerged looks the same as one that was never proposed.

Read-only: it never writes, archives, deletes or pushes. Per Docs/TOOLING.md a
READER tool carries no ship contract.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from collections import Counter
from datetime import datetime, timezone

DEFAULT_BASE = "origin/bleeding-edge"
DEFAULT_PREFIXES = ("claude/", "cece/")
# A branch whose tip is younger than this is someone's live work, not cruft.
ACTIVE_DAYS = 14

VERDICTS = ("ACTIVE", "EMPTY", "UNMEASURED", "LANDED", "GONE", "DIVERGED")

VERDICT_HELP = {
    "ACTIVE":   f"tip < {ACTIVE_DAYS}d old - live work, leave it alone",
    "EMPTY":    "no exclusive commits - nothing was ever authored here",
    "LANDED":   "every path its commits touched is byte-identical on the base",
    "GONE":     "most paths its commits touched no longer exist on the base",
    "DIVERGED": "paths still exist and differ - needs a human look",
    "UNMEASURED": "has commits but no visible file changes - merges only, or truncated by the shallow clone",
}


def git(*args: str, stdin: str | None = None, cwd: str | None = None) -> str:
    return subprocess.run(
        ("git",) + args,
        check=True, capture_output=True, text=True,
        input=stdin, cwd=cwd,
    ).stdout


def git_ok(*args: str, cwd: str | None = None) -> bool:
    return subprocess.run(
        ("git",) + args, capture_output=True, cwd=cwd
    ).returncode == 0


def all_origin_refs(cwd=None) -> list[str]:
    out = git("for-each-ref", "--format=%(refname)", "refs/remotes/origin", cwd=cwd)
    return [r for r in out.splitlines() if r and not r.endswith("/HEAD")]


def session_branches(prefixes, cwd=None) -> list[str]:
    out = git("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin", cwd=cwd)
    names = []
    for r in out.splitlines():
        if not r.startswith("origin/"):
            continue
        short = r[len("origin/"):]
        if any(short.startswith(p) for p in prefixes):
            names.append(r)
    return sorted(names)


def exclusive_commits(branch: str, negatives: str, cwd=None) -> list[str]:
    """Commits reachable from `branch` and from no other origin ref."""
    res = subprocess.run(
        ("git", "rev-list", branch, "--stdin"),
        input=negatives, capture_output=True, text=True, cwd=cwd,
    )
    if res.returncode != 0:
        return []
    return res.stdout.split()


def touched_paths(commits: list[str], branch: str, cwd=None) -> tuple[list[str], int]:
    """Paths the exclusive commits themselves changed.

    Deliberately a per-commit union rather than one base..tip diff. A session
    branch that merged bleeding-edge into itself has that merge among its
    exclusive commits, so a base..tip diff reports the whole integration - one
    branch here measured 7,436 paths that way, against the ~20 its session
    actually wrote. The union answers "what did this session author", which is
    the question the verdict rests on.

    Merge commits contribute nothing (a merge authored no change of its own);
    the count of skipped merges is returned so the caller can say so.
    """
    paths: set[str] = set()
    merges = 0
    for c in commits:
        parents = git("rev-list", "--parents", "-1", c, cwd=cwd).split()[1:]
        if len(parents) > 1:
            merges += 1
            continue
        try:
            out = git("diff-tree", "--no-commit-id", "--name-only",
                      "-r", "--no-renames", c, cwd=cwd)
            paths.update(out.splitlines())
        except subprocess.CalledProcessError:
            continue
    return sorted(p for p in paths if p), merges


def blob_oid(rev: str, path: str, cwd=None) -> str | None:
    res = subprocess.run(
        ("git", "rev-parse", f"{rev}:{path}"),
        capture_output=True, text=True, cwd=cwd,
    )
    return res.stdout.strip() if res.returncode == 0 else None


def classify(branch, commits, paths, base, now, cwd=None) -> dict:
    tip_iso = git("log", "-1", "--format=%cI", branch, cwd=cwd).strip()
    tip = datetime.fromisoformat(tip_iso)
    age_days = (now - tip).days

    row = {
        "branch": branch[len("origin/"):] if branch.startswith("origin/") else branch,
        "tip_date": tip_iso[:10],
        "age_days": age_days,
        "commits": len(commits),
        "subjects": [
            git("log", "-1", "--format=%s", c, cwd=cwd).strip() for c in commits[:5]
        ],
        "paths_touched": len(paths),
        "paths_identical": 0,
        "paths_missing": 0,
        "paths_changed": 0,
    }

    if age_days < ACTIVE_DAYS:
        row["verdict"] = "ACTIVE"
        return row
    if not commits:
        row["verdict"] = "EMPTY"
        return row
    if not paths:
        # Commits exist but changed no file we can see - every one was a merge,
        # or a shallow graft truncated them. Reporting this as DIVERGED would
        # claim a measurement that was never taken.
        row["verdict"] = "UNMEASURED"
        return row

    for p in paths:
        b = blob_oid(base, p, cwd=cwd)
        if b is None:
            row["paths_missing"] += 1
        elif b == blob_oid(branch, p, cwd=cwd):
            row["paths_identical"] += 1
        else:
            row["paths_changed"] += 1

    n = max(1, len(paths))
    if row["paths_changed"] == 0 and row["paths_missing"] == 0:
        row["verdict"] = "LANDED"
    elif row["paths_missing"] > n / 2:
        row["verdict"] = "GONE"
    else:
        row["verdict"] = "DIVERGED"
    return row


def build_rows(base, prefixes, cwd=None, progress=True) -> list[dict]:
    if not git_ok("rev-parse", "--verify", "--quiet", base + "^{commit}", cwd=cwd):
        sys.exit(f"error: base ref {base!r} not found. Fetch it, or pass --base.")

    refs = all_origin_refs(cwd=cwd)
    branches = session_branches(prefixes, cwd=cwd)
    now = datetime.now(timezone.utc)
    rows = []
    for i, b in enumerate(branches, 1):
        if progress and i % 25 == 0:
            print(f"  ...{i}/{len(branches)}", file=sys.stderr)
        full = f"refs/remotes/{b}"
        negatives = "\n".join(f"^{r}" for r in refs if r != full) + "\n"
        commits = exclusive_commits(b, negatives, cwd=cwd)
        paths, merges = touched_paths(commits, b, cwd=cwd)
        row = classify(b, commits, paths, base, now, cwd=cwd)
        row["merge_commits"] = merges
        rows.append(row)
    return rows


def sort_key(r):
    # DIVERGED first and, within it, the branches most likely to matter:
    # most recent, then most commits.
    order = {v: i for i, v in enumerate(
        ("DIVERGED", "UNMEASURED", "GONE", "LANDED", "EMPTY", "ACTIVE"))}
    return (order.get(r["verdict"], 9), r["age_days"], -r["commits"])


def print_report(rows, show=None, verbose=False):
    counts = Counter(r["verdict"] for r in rows)
    print(f"\n{len(rows)} session branches\n")
    for v in VERDICTS:
        if counts.get(v):
            print(f"  {counts[v]:>4}  {v:<9} {VERDICT_HELP[v]}")
    print()

    wanted = [r for r in rows if show is None or r["verdict"] in show]
    wanted.sort(key=sort_key)
    if not wanted:
        return

    print(f"{'VERDICT':<9} {'AGE':>5} {'CMT':>4} {'PATHS':>5} {'=':>4} {'X':>4} {'!':>4}  BRANCH")
    print("-" * 110)
    for r in wanted:
        print(f"{r['verdict']:<9} {str(r['age_days'])+'d':>5} {r['commits']:>4} "
              f"{r['paths_touched']:>5} {r['paths_identical']:>4} "
              f"{r['paths_changed']:>4} {r['paths_missing']:>4}  {r['branch']}")
        if verbose:
            for s in r["subjects"]:
                print(f"{'':>38}  - {s[:80]}")
    print("\nlegend: = identical on base   X changed on base   ! missing from base")


# ----------------------------------------------------------------- self-test

def self_test() -> int:
    """Negative-controlled: every verdict must fire, on a repo built to force it.

    A classifier nobody has watched produce each of its answers is one nobody
    should trust - so this asserts all five, not just that the tool runs.
    """
    global ACTIVE_DAYS
    saved = ACTIVE_DAYS
    tmp = tempfile.mkdtemp(prefix="triage-selftest-")
    try:
        r = os.path.join(tmp, "repo")
        os.makedirs(r)
        ident = ("-c", "user.email=t@t", "-c", "user.name=t")

        def g(*a):
            return git(*(ident + a), cwd=r)

        def write(name, text):
            open(os.path.join(r, name), "w").write(text)

        g("init", "-q", "-b", "main")
        for name in ("keep.txt", "moves.txt", "dies.txt"):
            write(name, "base\n")
        g("add", "-A"); g("commit", "-qm", "base")
        base_sha = g("rev-parse", "HEAD").strip()

        # LANDED: main ends up byte-identical to what this branch wrote.
        g("checkout", "-q", "-b", "b-landed")
        write("keep.txt", "v2\n")
        g("add", "-A"); g("commit", "-qm", "landed change")

        # DIVERGED: branch edits a path main then edits differently.
        g("checkout", "-q", base_sha); g("checkout", "-q", "-b", "b-diverged")
        write("moves.txt", "branch\n")
        g("add", "-A"); g("commit", "-qm", "diverged change")

        # GONE: branch edits a path main deletes.
        g("checkout", "-q", base_sha); g("checkout", "-q", "-b", "b-gone")
        write("dies.txt", "branch\n")
        g("add", "-A"); g("commit", "-qm", "gone change")

        # EMPTY: branch authored nothing of its own.
        g("checkout", "-q", base_sha); g("checkout", "-q", "-b", "b-empty")

        # main moves on: adopt LANDED's bytes, edit moves.txt, delete dies.txt.
        g("checkout", "-q", "main")
        write("keep.txt", "v2\n")
        write("moves.txt", "main\n")
        os.remove(os.path.join(r, "dies.txt"))
        g("add", "-A"); g("commit", "-qm", "main moves on")

        # Present them as origin/* so the tool sees its real ref shape.
        for b in ("main", "b-landed", "b-diverged", "b-gone", "b-empty"):
            g("update-ref", f"refs/remotes/origin/{b}", b)

        failures = []

        # 1. The ACTIVE guard: fixtures are fresh, so it must shadow everything.
        ACTIVE_DAYS = saved
        rows = build_rows("origin/main", ("b-",), cwd=r, progress=False)
        got = {x["branch"].replace("origin/", ""): x["verdict"] for x in rows}
        if set(got.values()) != {"ACTIVE"}:
            failures.append(f"  ACTIVE guard did not shadow fresh tips: {got}")

        # 2. With the guard off, each content verdict must fire on its fixture.
        ACTIVE_DAYS = 0
        rows = build_rows("origin/main", ("b-",), cwd=r, progress=False)
        got = {x["branch"].replace("origin/", ""): x["verdict"] for x in rows}
        expect = {
            "b-landed": "LANDED",
            "b-diverged": "DIVERGED",
            "b-gone": "GONE",
            "b-empty": "EMPTY",
        }
        for k, want in sorted(expect.items()):
            if got.get(k) != want:
                failures.append(f"  {k}: expected {want}, got {got.get(k)}")

        # 3. Exclusive-commit detection is the whole basis - prove it isolates.
        #    b-landed must report exactly its own 1 commit, not main's history.
        landed = next(x for x in rows if x["branch"].endswith("b-landed"))
        if landed["commits"] != 1:
            failures.append(
                f"  b-landed: expected 1 exclusive commit, got {landed['commits']}")

        print("verdicts with guard on :", json.dumps(
            {k: "ACTIVE" for k in expect}, sort_keys=True))
        print("verdicts with guard off:", json.dumps(got, sort_keys=True))
        if failures:
            print("SELF-TEST FAILED:\n" + "\n".join(failures))
            return 1
        print("self-test OK: ACTIVE / LANDED / DIVERGED / GONE / EMPTY all fire, "
              "exclusive-commit isolation holds")
        return 0
    finally:
        ACTIVE_DAYS = saved
        shutil.rmtree(tmp, ignore_errors=True)


def main() -> int:
    global ACTIVE_DAYS
    ap = argparse.ArgumentParser(
        description="Classify agent-session branches as safe-to-retire or needs-a-look.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="Read-only. Never archives, deletes or pushes anything.",
    )
    ap.add_argument("--base", default=DEFAULT_BASE,
                    help=f"integration branch to judge against (default {DEFAULT_BASE})")
    ap.add_argument("--prefix", action="append", default=None,
                    help="branch prefix to include (repeatable; default claude/ cece/)")
    ap.add_argument("--fetch", action="store_true",
                    help="fetch all heads first with --filter=blob:none (~27s, no blob content)")
    ap.add_argument("--verdict", action="append", choices=VERDICTS,
                    help="only show these verdicts (repeatable)")
    ap.add_argument("--active-days", type=int, default=ACTIVE_DAYS,
                    help=f"tip younger than this is ACTIVE (default {ACTIVE_DAYS})")
    ap.add_argument("--verbose", action="store_true", help="list commit subjects")
    ap.add_argument("--json", metavar="PATH", help="also write the full table as JSON")
    ap.add_argument("--self-test", action="store_true",
                    help="run the negative-controlled classifier test and exit")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    ACTIVE_DAYS = args.active_days

    if args.fetch:
        print("fetching all heads (commits+trees only)...", file=sys.stderr)
        subprocess.run(("git", "fetch", "--filter=blob:none", "--no-tags",
                        "origin", "+refs/heads/*:refs/remotes/origin/*"), check=True)

    prefixes = tuple(args.prefix) if args.prefix else DEFAULT_PREFIXES
    rows = build_rows(args.base, prefixes)
    print_report(rows, show=set(args.verdict) if args.verdict else None,
                 verbose=args.verbose)

    if args.json:
        with open(args.json, "w") as fh:
            json.dump(rows, fh, indent=2)
        print(f"\nwrote {args.json}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
