#!/usr/bin/env python3
"""Performance never changes the design (LOCKED rule, Yash, 2026-11-27; CLAUDE.md).

A performance change makes the same game cheaper. A change that buys frames by giving the player less (fewer or
paused prisms and trails, a cap, a hold, an earlier wither, a content switch on a device tier) is a DESIGN change
and lands only with a person's yes. This gate finds those changes in a diff and fails unless the commit message
names who approved it with a line:

    Design-approved-by: <name> (<where they said it>)

Why it exists: on 2026-11-21 a performance commit (2fadca9772) made the menu trail wait at 20,000 cell prisms on
PC through a field the Toy Box trail shares, so every vessel's trail stopped for good in freestyle (Caleb found it,
#2417 fixed it); on 2026-11-27 another lowered the lava lamp's wait to 13,000 (5c5affcecf, reverted). Both were
measured wins and both were design changes nobody had asked for.

Usage:
    py Tools/Build/check_perf_design.py                 # staged changes (before a commit)
    py Tools/Build/check_perf_design.py --range A..B    # every commit in a range, each with its own message
    py Tools/Build/check_perf_design.py --self-test     # proves it fires on 5c5affcecf and passes a neutral commit
Exit 0 clean, 1 a design change without an approval line.
"""
import argparse
import re
import subprocess
import sys

# Fields and code that decide HOW MUCH the player gets. Extend this list when a new content lever appears.
CONTENT_FIELDS = [
    "menuAutopilotLaysNoTrail", "freestyleCellPrismBudget", "freestyleCellPrismResume",
    "lavaLampCellPrismBudget", "lavaLampCellPrismResume", "skimRaceTrail", "joustTrail", "raceBudget",
    "floorPerVessel", "ceilingPerVessel", "deactivateMenuWhileFlying", "cytoplasmDensity", "wanderwayBudget",
    "prismBudgetPerScene", "maxCrystalsPerScene", "lifeformScenes", "maxConcurrentArrivals", "aheadTargetScenes",
    "maxTrailBlocks",
]
CODE_PATTERNS = [r"\bSetTierHold\s*\(", r"\bRaceTrailCap\b", r"\bRemoveOldest\s*\(", r"\btierHold\s*=\s*true"]
WATCHED_EXT = (".cs", ".asset", ".prefab", ".unity")
APPROVAL = re.compile(r"^Design-approved-by:\s*\S+", re.M)
LINE = re.compile(r"^[+-](?![+-])\s*(?:-\s*)?(" + "|".join(CONTENT_FIELDS) + r")\s*:", re.M)
CODE = re.compile("|".join(CODE_PATTERNS))


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace").stdout


def findings(diff):
    """Lines of a unified diff that change a content lever: (file, line)."""
    out, path = [], None
    for ln in diff.splitlines():
        if ln.startswith("+++ "):
            p = ln[4:].strip()
            path = p[2:] if p.startswith("b/") else (None if p == "/dev/null" else p)
            continue
        if ln.startswith("--- ") and path is None:
            continue
        if not path or not path.endswith(WATCHED_EXT) or "/Tests/" in path or path.startswith("Port/"):
            continue
        if not ln.startswith(("+", "-")) or ln.startswith(("+++", "---")):
            continue
        if LINE.match(ln) or (path.endswith(".cs") and ln.startswith("+") and CODE.search(ln)
                              and not ln[1:].lstrip().startswith(("//", "///", "*"))):
            out.append((path, ln[:160]))
    return out


def check(label, diff, message):
    hits = findings(diff)
    if not hits or APPROVAL.search(message or ""):
        return True
    print(f"DESIGN CHANGE without approval in {label}:")
    for p, ln in hits[:12]:
        print(f"  {p}: {ln}")
    print("  This changes how much the player gets (prisms, trails, caps, holds, content on a tier).\n"
          "  Performance work may not do that on its own: ask Yash and the area's owner with the numbers,\n"
          "  then add 'Design-approved-by: <name> (<where>)' to the commit message. CLAUDE.md, LOCKED rule.")
    return False


def self_test():
    bad = "5c5affcecf"
    if not git("cat-file", "-t", bad).strip():
        print("self-test: 5c5affcecf not in this clone; cannot prove the gate fires"); return 1
    fired = not check(bad, git("show", "--format=", bad), git("show", "-s", "--format=%B", bad))
    approved = check(bad + " (with approval line)", git("show", "--format=", bad), "x\n\nDesign-approved-by: Test (self-test)\n")
    neutral = check("synthetic neutral diff", "+++ b/Assets/_Scripts/Foo.cs\n+int x = 1;\n", "fix: x")
    ok = fired and approved and neutral
    print(f"self-test: fires on {bad} {fired}; approval line passes {approved}; neutral passes {neutral} -> {'OK' if ok else 'FAIL'}")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--range")
    ap.add_argument("--message", help="commit message to check staged changes against (default: none)")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    ok = True
    if a.range:
        for sha in git("rev-list", "--no-merges", a.range).split():
            ok &= check(sha[:10], git("show", "--format=", sha), git("show", "-s", "--format=%B", sha))
    else:
        ok = check("staged changes", git("diff", "--cached"), a.message or "")
    if ok:
        print("check_perf_design: no unapproved design change")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
