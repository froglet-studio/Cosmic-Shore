#!/usr/bin/env python3
"""
The port must never change the Unity project. Unity reads Assets/, Packages/ and
ProjectSettings/ (and builds from them); the port lives entirely under Port/ and only READS
Assets/. This fails if the current branch differs from the base branch anywhere outside
Port/, except the .gitignore rules that keep Port's own .csproj/.slnx files tracked, the
amoebius-* CI workflows, the amoebius* Claude Code skills, and text whose only change is the
engine's name (Prisma -> Amoebius: the rename reached the docs that mention the engine).

    python3 Port/tools/check_unity_isolation.py                 # against origin/bleeding-edge
    python3 Port/tools/check_unity_isolation.py --base main
"""
import argparse, re, subprocess, sys

ALLOWED = {".gitignore",             # only Port/** un-ignore lines; checked below
           # The one Unity-side file the port owns: FrogletTools > Amoebius > Launch Amoebius, an
           # editor-only menu that builds and opens Amoebius from the checkout (writes only Library/).
           "Assets/_Scripts/Editor/LaunchAmoebius.cs", "Assets/_Scripts/Editor/LaunchAmoebius.cs.meta",
           # ... under its name before the rename (the rename deletes it).
           "Assets/_Scripts/Editor/LaunchPrisma.cs", "Assets/_Scripts/Editor/LaunchPrisma.cs.meta",
           "Docs/TOOLING.md"}                # that tool's rows in the FrogletTools index
# CI that builds ONLY Port/ (never imports or edits the Unity project) is part of the port.
# prisma- and froglet-: the engine's earlier names.
PORT_CI_PREFIX = (".github/workflows/amoebius-", ".github/workflows/prisma-", ".github/workflows/froglet-")
# Agent guidance for the engine (a Claude Code skill) is tooling, never read by Unity.
PORT_SKILL_PREFIX = (".claude/skills/amoebius", ".claude/skills/prisma", ".claude/skills/froglet-")
# The engine's earlier name, as the rename replaced it (the game's Prism* words are untouched).
OLD_NAME = re.compile(r"Prisma(?![a-z])|prisma(?![a-z])|PRISMA(?![A-Z])")
NEW_NAME = {"Prisma": "Amoebius", "prisma": "amoebius", "PRISMA": "AMOEBIUS"}
UNITY_ROOTS = ("Assets/", "Packages/", "ProjectSettings/", "UserSettings/")


def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()


def only_renamed(base, path):
    """True when the file's only change since BASE is the engine's name."""
    try:
        old, new = (subprocess.check_output(["git", "show", f"{rev}:{path}"], text=True, stderr=subprocess.DEVNULL)
                    for rev in (base, "HEAD"))
    except (subprocess.CalledProcessError, UnicodeDecodeError):
        return False
    return old != new and OLD_NAME.sub(lambda m: NEW_NAME[m.group(0)], old) == new


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="origin/bleeding-edge")
    a = ap.parse_args()
    base = git("merge-base", "HEAD", a.base)
    changed = [p for p in git("diff", "--name-only", base, "HEAD").splitlines() if p and not p.startswith("Port/")]
    bad = [p for p in changed if p not in ALLOWED and not p.startswith(PORT_CI_PREFIX + PORT_SKILL_PREFIX)
           and not only_renamed(base, p)]
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
    print(f"ok: {len(changed)} change(s) outside Port/ vs {a.base}, all port-only (.gitignore rules, amoebius CI and skills)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
