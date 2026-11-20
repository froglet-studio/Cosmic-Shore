#!/usr/bin/env python3
"""Fail when a change ADDS a large file to git outside Git LFS (Docs/BRANCH_POLICY.md, Shombith 2026-11-20).

The repository is public and 4.2 GB, most of it history: build zips, WAV masters, editor caches and tool output that
were committed once and now ride along in every clone forever, even after they were deleted. This gate stops the next
one at the pull request, while it is still cheap to take out.

What it checks: every file the change adds or copies (git diff --diff-filter=AC base...head). A file whose git blob is
over the limit (default 10 MB) fails. A file stored in LFS passes on its own, because git only holds its ~130-byte
pointer. Renames and edits of files already in the repo are not checked: they add no new large file to history.

Ways through, in order of preference:
  1. keep it out of git (build output, caches, captures: add the folder to .gitignore);
  2. store it in LFS: `git lfs track "<path>"`, commit .gitattributes, re-add the file;
  3. a one-off exception agreed with the repo owner: add the exact path to .github/large-files-allowlist.txt.

Usage:
  python3 Tools/Build/check_large_files.py --base origin/bleeding-edge [--head HEAD] [--limit-mb 10]
  python3 Tools/Build/check_large_files.py --self-test
Exit codes: 0 pass, 1 a large file was added, 2 setup error (bad refs).
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
import tempfile

ALLOWLIST = ".github/large-files-allowlist.txt"


def git(*args: str, cwd: str | None = None) -> str:
    return subprocess.run(["git", *args], cwd=cwd, check=True, capture_output=True, text=True).stdout


def read_allowlist(repo: str) -> set[str]:
    path = os.path.join(repo, ALLOWLIST)
    if not os.path.exists(path):
        return set()
    with open(path, encoding="utf-8") as fh:
        return {line.strip() for line in fh if line.strip() and not line.lstrip().startswith("#")}


def added_files(base: str, head: str, repo: str) -> list[str]:
    out = git("diff", "--name-only", "-z", "--diff-filter=AC", f"{base}...{head}", cwd=repo)
    return [p for p in out.split("\0") if p]


def blob_size(head: str, path: str, repo: str) -> int:
    return int(git("cat-file", "-s", f"{head}:{path}", cwd=repo).strip())


def find_violations(base: str, head: str, limit_bytes: int, repo: str) -> list[tuple[str, int]]:
    allowed = read_allowlist(repo)
    bad = []
    for path in added_files(base, head, repo):
        if path in allowed:
            continue
        size = blob_size(head, path, repo)
        if size > limit_bytes:
            bad.append((path, size))
    return bad


def report(bad: list[tuple[str, int]], limit_mb: float) -> int:
    if not bad:
        print(f"check_large_files: OK, no added file over {limit_mb:g} MB outside LFS.")
        return 0
    print(f"check_large_files: FAIL, {len(bad)} added file(s) over {limit_mb:g} MB are stored in git, not LFS:")
    for path, size in sorted(bad, key=lambda t: -t[1]):
        print(f"  {size / 1048576:8.1f} MB  {path}")
    print("Keep it out of git (.gitignore), or store it in LFS:  git lfs track \"<path>\"  then re-add it.")
    print(f"A one-off exception agreed with the repo owner goes in {ALLOWLIST}. See Docs/BRANCH_POLICY.md.")
    return 1


def self_test() -> int:
    """Negative and positive controls in a throwaway repo: a big blob fails, a small one, an allowlisted one and a
    rename of an existing big file pass."""
    with tempfile.TemporaryDirectory() as repo:
        def run(*a: str) -> None:
            git(*a, cwd=repo)

        def write(rel: str, size: int) -> None:
            full = os.path.join(repo, rel)
            os.makedirs(os.path.dirname(full) or repo, exist_ok=True)
            with open(full, "wb") as fh:
                fh.write(os.urandom(size))

        run("init", "-q", "-b", "main")
        run("config", "user.email", "t@t")
        run("config", "user.name", "t")
        write("old_big.bin", 3000)
        run("add", ".")
        run("commit", "-q", "-m", "base")
        run("checkout", "-q", "-b", "change")
        write("new_big.bin", 3000)
        write("small.txt", 10)
        write("allowed_big.bin", 3000)
        os.makedirs(os.path.join(repo, ".github"))
        with open(os.path.join(repo, ALLOWLIST), "w") as fh:
            fh.write("# exceptions\nallowed_big.bin\n")
        run("mv", "old_big.bin", "moved_big.bin")
        run("add", ".")
        run("commit", "-q", "-m", "change")
        bad = find_violations("main", "HEAD", 2000, repo)
        names = [p for p, _ in bad]
        ok = names == ["new_big.bin"]
        print("self-test:", "PASS" if ok else f"FAIL (flagged {names}, expected ['new_big.bin'])")
        return 0 if ok else 1


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--base", help="the branch the change merges into, e.g. origin/bleeding-edge")
    ap.add_argument("--head", default="HEAD")
    ap.add_argument("--limit-mb", type=float, default=10.0)
    ap.add_argument("--repo", default=".")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if not args.base:
        ap.error("--base is required")
    try:
        bad = find_violations(args.base, args.head, int(args.limit_mb * 1048576), args.repo)
    except subprocess.CalledProcessError as exc:
        print(f"check_large_files: setup error: {exc.stderr.strip() or exc}", file=sys.stderr)
        return 2
    return report(bad, args.limit_mb)


if __name__ == "__main__":
    sys.exit(main())
