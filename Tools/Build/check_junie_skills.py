#!/usr/bin/env python3
"""Fail unless `.junie/skills` is committed as ONE symlink to `../.claude/skills`.

WHY THIS EXISTS.

JetBrains Junie loads project skills only from `.junie/skills/<name>/SKILL.md`.
It never reads `.claude/skills`. Instead it offers to IMPORT that directory, and
the import copies. One such copy was committed in the 2026-08-25 merge
`def29f5e1`: the nine skills that existed then. Every later branch edited
`.claude/skills` alone. By 2026-10-08 the copies had drifted 3,920 `diff -r`
lines (asset-surgery 2,138, vessel 744, ship 660), and a Junie session was
following rules `.claude/skills` had already retired. The fix was to make
`.junie/skills` a symlink, which leaves drift no way to be represented. This
gate stops a copy from coming back. The most likely ways back are an accepted
Junie import, or a Windows checkout with `core.symlinks=false` (which writes the
link out as a one-line text file) that somebody "repairs" into a real directory.

WHAT IT CHECKS. It reads the git INDEX, i.e. what is staged or committed, and
not the working tree. That way a Windows checkout without symlinks still reads
the commit correctly. Every finding is a hard failure:

  copy          a file is tracked UNDER .junie/skills/. That is a second,
                hand-maintained copy of a skill again.
  not-a-link    .junie/skills is tracked, but not as a symlink (mode 120000).
  wrong-target  the link points somewhere other than ../.claude/skills.
  missing       nothing is tracked at .junie/skills, so Junie has no skills.
  dangling      no .claude/skills/*/SKILL.md is tracked, so the link resolves
                to nothing.

If Junie is ever retired, delete `.junie/` and this gate in the same change.

    python3 Tools/Build/check_junie_skills.py
    python3 Tools/Build/check_junie_skills.py --self-test

Exit code 0 when clean, 1 on any finding, 2 on a usage/setup error.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
import tempfile

# Tools/Build/<this file> -> Tools/Build -> Tools -> the repository root.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

LINK = ".junie/skills"
TARGET = "../.claude/skills"
SYMLINK_MODE = "120000"

RESTORE = (f"restore it with: git rm -r -q --cached {LINK}; rm -rf {LINK}; "
           f"ln -s {TARGET} {LINK}; git add {LINK}")


class SetupError(Exception):
    pass


def git(root: str, *args: str, stdin: bytes | None = None) -> bytes:
    try:
        proc = subprocess.run(["git", "-C", root, *args], input=stdin,
                              capture_output=True, check=False)
    except FileNotFoundError as exc:
        raise SetupError("git is not on PATH") from exc
    if proc.returncode != 0:
        raise SetupError(f"git {' '.join(args)} failed: {proc.stderr.decode(errors='replace').strip()}")
    return proc.stdout


def read_index(root: str) -> tuple[list[tuple[str, str, str | None]], int]:
    """(mode, path, link target or None) for every tracked entry at or under LINK,
    and how many skills .claude/skills holds."""
    entries = []
    for rec in git(root, "ls-files", "--stage", "-z", "--", LINK).split(b"\0"):
        if not rec:
            continue
        meta, path = rec.split(b"\t", 1)
        mode, sha, _stage = meta.decode().split()
        target = git(root, "cat-file", "blob", sha).decode(errors="replace") if mode == SYMLINK_MODE else None
        entries.append((mode, path.decode(errors="replace"), target))
    skills = [p for p in git(root, "ls-files", "-z", "--", ".claude/skills/*/SKILL.md").split(b"\0") if p]
    return entries, len(skills)


def findings(entries: list[tuple[str, str, str | None]], skill_count: int) -> list[str]:
    out = []
    link = [e for e in entries if e[1] == LINK]
    for _mode, path, _target in entries:
        if path.startswith(LINK + "/"):
            out.append(f"copy: {path} is tracked under {LINK}/. Edit the skill in .claude/skills/ "
                       f"instead, and {RESTORE}")
    if not link:
        if not out:
            out.append(f"missing: nothing is tracked at {LINK}, so Junie loads no skills. {RESTORE}")
        else:
            out.append(f"not-a-link: {LINK} is a real directory, not a symlink to {TARGET}")
    else:
        mode, _path, target = link[0]
        if mode != SYMLINK_MODE:
            out.append(f"not-a-link: {LINK} is tracked with mode {mode}, not as a symlink "
                       f"({SYMLINK_MODE}). A Windows checkout with core.symlinks=false does this. {RESTORE}")
        elif target != TARGET:
            out.append(f"wrong-target: {LINK} points at {target!r}, not {TARGET!r}. {RESTORE}")
    if skill_count == 0:
        out.append(f"dangling: no .claude/skills/*/SKILL.md is tracked, so {LINK} resolves to no skills")
    return out


def scan(root: str) -> list[str]:
    return findings(*read_index(root))


# ── self-test ────────────────────────────────────────────────────────────────
# Fixtures are built straight into a scratch repository's index with
# `git update-index --cacheinfo`. That exercises the real ls-files/cat-file
# parsing, not just findings(), and needs no OS symlink support.

def _stage(repo: str, mode: str, path: str, content: str) -> None:
    sha = git(repo, "hash-object", "-w", "--stdin", stdin=content.encode()).decode().strip()
    git(repo, "update-index", "--add", "--cacheinfo", f"{mode},{sha},{path}")


SKILL = ("100644", ".claude/skills/vessel/SKILL.md", "---\nname: vessel\n---\nbody\n")

CASES = [
    # (name, staged entries, finding kinds expected)
    ("the shipped shape", [SKILL, (SYMLINK_MODE, LINK, TARGET)], []),
    ("a committed copy", [SKILL, ("100644", LINK + "/vessel/SKILL.md", SKILL[2])], ["copy", "not-a-link"]),
    ("link committed as a text file", [SKILL, ("100644", LINK, TARGET)], ["not-a-link"]),
    ("link with a trailing newline", [SKILL, (SYMLINK_MODE, LINK, TARGET + "\n")], ["wrong-target"]),
    ("link to one skill", [SKILL, (SYMLINK_MODE, LINK, TARGET + "/vessel")], ["wrong-target"]),
    ("link removed", [SKILL], ["missing"]),
    ("no skills to point at", [(SYMLINK_MODE, LINK, TARGET)], ["dangling"]),
]


def self_test() -> int:
    failed = 0
    for name, staged, expected in CASES:
        with tempfile.TemporaryDirectory() as repo:
            git(repo, "init", "-q")
            for mode, path, content in staged:
                _stage(repo, mode, path, content)
            got = sorted({f.split(":", 1)[0] for f in scan(repo)})
        ok = got == sorted(expected)
        failed += not ok
        print(f"  {'ok  ' if ok else 'FAIL'} {name}: expected {sorted(expected)}, got {got}")
    print(f"self-test: {len(CASES) - failed}/{len(CASES)} cases")
    return 1 if failed else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--self-test", action="store_true", help="prove the gate detects its fixtures")
    parser.add_argument("--root", default=ROOT, help="repository root (default: this checkout)")
    args = parser.parse_args()
    try:
        if args.self_test:
            return self_test()
        problems = scan(args.root)
    except SetupError as exc:
        print(f"check_junie_skills: {exc}", file=sys.stderr)
        return 2
    for p in problems:
        print(f"FAIL {p}")
    if problems:
        return 1
    print(f"OK: {LINK} is a symlink to {TARGET}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
