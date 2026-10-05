#!/usr/bin/env python3
"""
The port must never change the Unity project. Unity reads Assets/, Packages/ and
ProjectSettings/ (and builds from them); the port lives entirely under Port/ and only READS
Assets/. This fails if the current branch differs from the base branch anywhere outside
Port/, except the one .gitignore rule that keeps Port's own .csproj/.slnx files tracked.

    python3 Port/tools/check_unity_isolation.py                 # against origin/bleeding-edge
    python3 Port/tools/check_unity_isolation.py --base main
"""
import argparse, subprocess, sys

ALLOWED = {".gitignore"}            # only Port/** un-ignore lines; checked below
UNITY_ROOTS = ("Assets/", "Packages/", "ProjectSettings/", "UserSettings/")


def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="origin/bleeding-edge")
    a = ap.parse_args()
    base = git("merge-base", "HEAD", a.base)
    changed = [p for p in git("diff", "--name-only", base, "HEAD").splitlines() if p and not p.startswith("Port/")]
    bad = [p for p in changed if p not in ALLOWED]
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
    print(f"ok: {len(changed)} change(s) outside Port/ vs {a.base}, all port-only .gitignore rules")
    return 0


if __name__ == "__main__":
    sys.exit(main())
