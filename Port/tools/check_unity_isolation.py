#!/usr/bin/env python3
"""
The port must never change the Unity project. Unity reads Assets/, Packages/ and
ProjectSettings/ (and builds from them); the port lives entirely under Port/ and only READS
Assets/. This fails if the current branch differs from the base branch anywhere outside
Port/, except the .gitignore rules that keep Port's own .csproj/.slnx files tracked, the
prisma-* CI workflows and the prisma* Claude Code skills.

    python3 Port/tools/check_unity_isolation.py                 # against origin/bleeding-edge
    python3 Port/tools/check_unity_isolation.py --base main
"""
import argparse, subprocess, sys

ALLOWED = {".gitignore",             # only Port/** un-ignore lines; checked below
           # The one Unity-side file the port owns: FrogletTools > Amoebius > Launch Amoebius, an
           # editor-only menu that builds and opens Prisma from the checkout (writes only Library/).
           "Assets/_Scripts/Editor/LaunchPrisma.cs", "Assets/_Scripts/Editor/LaunchPrisma.cs.meta",
           "Docs/TOOLING.md"}                # that tool's rows in the FrogletTools index
# CI that builds ONLY Port/ (never imports or edits the Unity project) is part of the port.
PORT_CI_PREFIX = (".github/workflows/prisma-", ".github/workflows/froglet-")  # froglet-: before the rename
# Agent guidance for the engine (a Claude Code skill) is tooling, never read by Unity.
PORT_SKILL_PREFIX = (".claude/skills/prisma", ".claude/skills/froglet-")
UNITY_ROOTS = ("Assets/", "Packages/", "ProjectSettings/", "UserSettings/")


def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="origin/bleeding-edge")
    a = ap.parse_args()
    base = git("merge-base", "HEAD", a.base)
    changed = [p for p in git("diff", "--name-only", base, "HEAD").splitlines() if p and not p.startswith("Port/")]
    bad = [p for p in changed if p not in ALLOWED and not p.startswith(PORT_CI_PREFIX + PORT_SKILL_PREFIX)]
    if ".gitignore" in changed:
        added = [l[1:] for l in git("diff", base, "HEAD", "--", ".gitignore").splitlines()
                 if l.startswith("+") and not l.startswith("+++")]
        for l in added:
            if l.strip() and not l.lstrip().startswith("#") and "Port/" not in l:
                bad.append(f".gitignore: {l.strip()}")
    unity = [p for p in bad if p.startswith(UNITY_ROOTS)]
    if bad:
        print("The port branch changes files outside Port/:")
        for p in bad:
            print(("  [UNITY] " if p in unity else "  ") + p)
        print("Move them to their own PR - the port must not change the Unity project.")
        return 1
    print(f"ok: {len(changed)} change(s) outside Port/ vs {a.base}, all port-only (.gitignore rules, prisma CI and skills)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
