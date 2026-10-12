#!/usr/bin/env python3
"""
Build a branch that carries only the chosen PARTS of cece/nice-brahmagupta-ojclij, on top of
origin/bleeding-edge, for a selective push after testing. Docs/BRANCH_TEST_PLAN_2026-10-12.md
describes every part, its risks, its benefits and its editor test steps.

    python3 Tools/Build/pick_branch_parts.py list
    python3 Tools/Build/pick_branch_parts.py build A C J K --branch push/qol-and-leaks
    python3 Tools/Build/pick_branch_parts.py build L --branch push/evolution --base origin/bleeding-edge

What `build` does, without touching your checkout:
  1. adds the parts each chosen part needs (DEPENDS, below) and says so;
  2. creates the branch at --base in a temporary worktree;
  3. cherry-picks the chosen parts' commits in the order they were made (-x records the source);
  4. when a pick stops on DOCUMENTATION only (the shared checklists, the Steam board, the AI docs:
     every part appended to the same running sections), keeps the base's version of that file and
     goes on, listing what it dropped; the dropped text is this branch's own record of a part, and
     Docs/BRANCH_TEST_PLAN_2026-10-12.md carries every part's test steps whatever is picked;
  5. stops and reports on any conflict in code or assets (none exist for single parts measured
     on 2026-10-12, so one means bleeding-edge moved; resolve it by hand in the worktree it names);
  6. removes the worktree and leaves the branch. It never pushes: review, then
     `git push -u origin <branch>` and open the pull request.

A one-off for this branch's selective push. Delete it with the test plan once the parts have landed.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile

PARTS = {
    "A": ("CI gates and offline audit tools",
          "8c6952942 a70e2cd49 d0405a4fd ea7b82e1b 1c5753e22 e81cf407e e8f7a5557"),
    "B": ("Repo hygiene: untracked FMOD cache, PR template, hygiene report", "001f75452"),
    "C": ("Steam dual-app uploader and Playtest runbook", "bc472fc70"),
    "D": ("Plans and docs only (Rhino packet, index, labs note, QA backlog, I1 negative result, checklist entries)",
          "e335ed1c0 2a8f1119b 80aabc866 889e950c4 48d84486f b762f66a2 94fc408b4 e0454b9c8"),
    "E": ("AI training game fixes and the deployed tournament genome", "0e67df5bb 369d4f9ad 67f25a9fb"),
    "F": ("Skim Race AI intensity-4 policy v2-i4", "acf012253 9339b9f21"),
    "G": ("Load-time target and unattended load sweep (R7)", "766b14800"),
    "H": ("Parity harness, Unity side (replay recorder/player, deterministic session, probe, capture)",
          "1a397c3e3 646d599e0"),
    "I": ("Prisma engine (Port/ only)", "93c5d74e7 8bd90dbe4 e03b31341 1fcb52135 1ea3d23b5 441fafc47 6bf9e5a0c"),
    "J": ("Multiplayer QoL: held toast, Add Friend, Recently played with, ready identity, dropping out, parked inbox patch",
          "75b764375 d70061b42 278055856 107c04c8a 6be10752d 71ed6a5ce"),
    "K": ("Leak and lifetime fixes", "c88d7083f 1c6fc87ad f375809ea e12a8165a"),
    "L": ("Heritable genome (evolution), from cece/friendly-goodall-woddjw",
          "3bdd94362 e91c90363 b6e3a44ec 323a18eda f3b60adcb 511501e84 52d96167f"),
}

# A part on the left does not build or does not work without the parts on the right. Filled from the
# measurement of each part built ALONE on origin/bleeding-edge (Docs/BRANCH_TEST_PLAN_2026-10-12.md,
# "Parts alone"); empty until that measurement lands, so nothing unmeasured is asserted here.
DEPENDS = {}

DOC_PREFIXES = ("Docs/", "Port/docs/", ".github/pull_request_template.md")


def git(*args, cwd=None, check=True):
    r = subprocess.run(["git", *args], cwd=cwd, text=True, capture_output=True)
    if check and r.returncode != 0:
        raise SystemExit(f"git {' '.join(args)} failed:\n{r.stdout}{r.stderr}")
    return r


def is_doc(path):
    return path.startswith(DOC_PREFIXES) or path.endswith(".md")


def resolve(keys):
    chosen, added = [], []

    def visit(k):
        if k in chosen:
            return
        for d in DEPENDS.get(k, []):
            if d not in chosen:
                visit(d)
                if d not in keys:
                    added.append((d, k))
        chosen.append(k)

    for k in keys:
        visit(k)
    return chosen, added


def ordered_commits(keys, source):
    wanted = {}
    for k in keys:
        for c in PARTS[k][1].split():
            wanted[git("rev-parse", c).stdout.strip()] = k
    history = git("rev-list", "--reverse", "--no-merges", source).stdout.split()
    return [(c, wanted[c]) for c in history if c in wanted]


def cmd_list(_):
    for k, (title, commits) in PARTS.items():
        deps = DEPENDS.get(k)
        print(f"{k}  {title}\n   commits: {commits}" + (f"\n   needs: {', '.join(deps)}" if deps else ""))


def cmd_build(a):
    keys = [k.upper() for k in a.parts]
    unknown = [k for k in keys if k not in PARTS]
    if unknown:
        raise SystemExit(f"unknown part(s): {', '.join(unknown)}; run `list`")
    chosen, added = (keys, []) if a.no_deps else resolve(keys)
    for dep, by in added:
        print(f"adding part {dep}: part {by} needs it")
    if git("rev-parse", "--verify", "--quiet", f"refs/heads/{a.branch}", check=False).returncode == 0:
        raise SystemExit(f"branch {a.branch} already exists; pick another name or delete it")
    commits = ordered_commits(chosen, a.source)
    wt = tempfile.mkdtemp(prefix="pick-parts-")
    shutil.rmtree(wt)
    git("worktree", "add", "-q", "-b", a.branch, wt, a.base)
    dropped, ok = [], True
    try:
        for sha, k in commits:
            r = git("cherry-pick", "-x", sha, cwd=wt, check=False)
            if r.returncode == 0:
                continue
            conflicted = git("diff", "--name-only", "--diff-filter=U", cwd=wt).stdout.split()
            if not conflicted:  # an empty pick: the change is already on the base
                git("cherry-pick", "--skip", cwd=wt, check=False)
                print(f"  {sha[:9]} ({k}) already on {a.base}; skipped")
                continue
            code = [p for p in conflicted if not is_doc(p)]
            if code:
                print(f"STOP: {sha[:9]} (part {k}) conflicts in code or assets: {', '.join(code)}")
                print(f"  resolve it in {wt}, `git cherry-pick --continue`, then pick the rest by hand")
                ok = False
                return
            for p in conflicted:
                git("checkout", "--ours", "--", p, cwd=wt)
                git("add", "--", p, cwd=wt)
                dropped.append((sha[:9], k, p))
            env = dict(os.environ, GIT_EDITOR="true")
            r = subprocess.run(["git", "cherry-pick", "--continue"], cwd=wt, text=True, capture_output=True, env=env)
            if r.returncode != 0:
                git("cherry-pick", "--skip", cwd=wt, check=False)  # the pick was docs only and nothing remains
        print(f"built {a.branch}: parts {' '.join(chosen)}, {len(commits)} commit(s) on {a.base}")
        for sha, k, p in dropped:
            print(f"  kept {a.base}'s {p} where {sha} (part {k}) conflicted on it")
        print(f"next: review `git log --oneline {a.base}..{a.branch}`, then git push -u origin {a.branch}")
    finally:
        if ok:
            git("worktree", "remove", "--force", wt, check=False)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list").set_defaults(fn=cmd_list)
    b = sub.add_parser("build")
    b.add_argument("parts", nargs="+")
    b.add_argument("--branch", required=True)
    b.add_argument("--base", default="origin/bleeding-edge")
    b.add_argument("--source", default="origin/cece/nice-brahmagupta-ojclij",
                   help="the branch the parts' commits live on")
    b.add_argument("--no-deps", action="store_true",
                   help="do not add the parts a chosen part needs (measurement only: the result may not build)")
    b.set_defaults(fn=cmd_build)
    a = ap.parse_args()
    a.fn(a)


if __name__ == "__main__":
    sys.exit(main())
