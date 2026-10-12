#!/usr/bin/env python3
"""
Build a branch that carries only the chosen PARTS of cece/nice-brahmagupta-ojclij, on top of
origin/bleeding-edge, for a selective push after testing. Docs/BRANCH_TEST_PLAN_2026-10-12.md
describes every part, its risks, its benefits and its editor test steps.

    python3 Tools/Build/pick_branch_parts.py list
    python3 Tools/Build/pick_branch_parts.py build A C J K --branch push/qol-and-leaks
    python3 Tools/Build/pick_branch_parts.py build L --branch push/evolution --base origin/bleeding-edge

    python3 Tools/Build/pick_branch_parts.py check     # every branch commit is in a part; every part has steps

What `build` does, without touching your checkout:
  1. adds the parts each chosen part needs (DEPENDS, below) and says so, and warns when part A is
     left out (bleeding-edge's guard is red at "Validate project" until A lands) or when part D's
     QA items describe parts that are not picked;
  2. creates the branch at --base in a temporary worktree;
  3. cherry-picks the chosen parts' commits in the order they were made (-x records the source);
  4. when a pick stops on DOCUMENTATION only (the shared checklists, the Steam board, the AI docs:
     every part appended to the same running sections), keeps the base's version of that file and
     goes on, listing what it dropped;
  5. stops and reports on any conflict in code or assets (none exist for single parts measured
     on 2026-10-12, so one means bleeding-edge moved; resolve it by hand in the worktree it names);
  6. files the picked parts' editor test steps: it puts Docs/UNITY_VERIFICATION_CHECKLIST.md back to
     the base's text (this branch's own checklist hunks are written against sections the base does
     not have, and the reviewed steps supersede them) and inserts ONE section holding exactly the
     chosen parts' steps, read from Docs/BRANCH_TEST_PLAN_2026-10-12.md on --source. CLAUDE.md wants
     editor-unverified work filed there, and this way a selective push files only what it carries;
  7. removes any folder .meta the picks added whose folder the branch does not carry (Unity would
     delete it on import and leave a change in every tester's tree);
  8. removes the worktree and leaves the branch. It never pushes: review, then
     `git push -u origin <branch>` and open the pull request with the printed body.

A one-off for this branch's selective push. Delete it with the test plan once the parts have landed.
"""
import argparse
import datetime
import os
import re
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
    # 9339b9f21 also sits in F: it added H's two folder metas next to F's table rows. A commit in two
    # parts is picked once; without F its table hunk is a docs-only conflict and only the metas land.
    "H": ("Parity harness, Unity side (replay recorder/player, deterministic session, probe, capture)",
          "1a397c3e3 646d599e0 9339b9f21"),
    "I": ("Prisma engine (Port/ only)", "93c5d74e7 8bd90dbe4 e03b31341 1fcb52135 1ea3d23b5 441fafc47 6bf9e5a0c"),
    "J": ("Multiplayer QoL: held toast, Add Friend, Recently played with, ready identity, dropping out, invite inbox and badge",
          "75b764375 d70061b42 278055856 107c04c8a 6be10752d 71ed6a5ce caf4e2dd8"),
    "K": ("Leak and lifetime fixes", "c88d7083f 1c6fc87ad f375809ea e12a8165a"),
    "L": ("Heritable genome (evolution), from cece/friendly-goodall-woddjw",
          "3bdd94362 e91c90363 b6e3a44ec 323a18eda f3b60adcb 511501e84 52d96167f"),
}

# A part on the left does not build without the parts on the right. Measured 2026-10-12 by building
# every part ALONE on origin/bleeding-edge (reference compile, player + editor, and Prisma's live
# compile of the same tree; Docs/BRANCH_TEST_PLAN_2026-10-12.md, "Parts alone"):
#   G alone: LoadSweepRunner reads GameDataSO.IsGeneticTrainingSession, which E adds (editor + Prisma fail).
#   H alone: ParityProbe uses the engine's FMOD callback types that I adds (Prisma's live compile fails,
#            and Prisma parity CI runs on every bleeding-edge push).
#   I alone: ParityRun uses the game's ReplayFile / ReplayPlayer / DeterministicSession, which H adds.
# H and I therefore travel together.
DEPENDS = {
    "G": ["E"],
    "H": ["I"],
    "I": ["H"],
}

DOC_PREFIXES = ("Docs/", "Port/docs/", ".github/pull_request_template.md")
PLAN = "Docs/BRANCH_TEST_PLAN_2026-10-12.md"
CHECKLIST = "Docs/UNITY_VERIFICATION_CHECKLIST.md"
SOURCE_BRANCH = "cece/nice-brahmagupta-ojclij"
# Commits that touch only these paths are this branch's own scaffolding and never travel.
SOURCE_ONLY_PATHS = {"Tools/Build/pick_branch_parts.py", PLAN}
# Part D's QA backlog refresh and checklist entry describe these parts' features.
D_DESCRIBES = ["A", "E", "F", "G", "H", "J"]


def git(*args, cwd=None, check=True):
    r = subprocess.run(["git", *args], cwd=cwd, text=True, capture_output=True)
    if check and r.returncode != 0:
        raise SystemExit(f"git {' '.join(args)} failed:\n{r.stdout}{r.stderr}")
    return r


def is_doc(path):
    return path.startswith(DOC_PREFIXES) or path.endswith(".md")


def resolve(keys):
    """The chosen parts plus everything they need, each once (H and I need each other)."""
    chosen, added, seen = [], [], set()

    def visit(k):
        if k in seen:
            return
        seen.add(k)
        for d in DEPENDS.get(k, []):
            if d not in seen and d not in keys:
                added.append((d, k))
            visit(d)
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


def plan_steps(source, key):
    text = git("show", f"{source}:{PLAN}").stdout
    m = re.search(rf"<!-- steps:{key} -->\n(.*?)<!-- /steps:{key} -->", text, re.S)
    if not m:
        raise SystemExit(f"{PLAN} on {source} has no test steps for part {key}")
    return m.group(1).strip()


def file_checklist(wt, a, chosen):
    """Base's checklist plus one section holding exactly the chosen parts' reviewed test steps."""
    chosen = [k for k in PARTS if k in chosen]
    base_text = git("show", f"{a.base}:{CHECKLIST}").stdout
    lines = [f"### 🔴 From `{SOURCE_BRANCH}`: " + "; ".join(PARTS[k][0] for k in chosen)
             + f" (`{a.branch}`, {datetime.date.today().isoformat()}) — NOT EDITOR-VERIFIED", "",
             f"Picked by `Tools/Build/pick_branch_parts.py` from `{SOURCE_BRANCH}`. The steps are that "
             f"branch's reviewed test plan (`{PLAN}` there), one block per part this branch carries. "
             "Each step has a PASS and a FAIL line; record the result in the PR or in Docs/QA/.", ""]
    for k in chosen:
        lines += [f"#### {PARTS[k][0]}", "", plan_steps(a.source, k), ""]
    section = "\n".join(lines) + "\n"
    i = base_text.find("\n### ")
    text = base_text + "\n" + section if i < 0 else base_text[:i + 1] + section + "\n" + base_text[i + 1:]
    with open(os.path.join(wt, CHECKLIST), "w", encoding="utf-8") as f:
        f.write(text)
    git("add", "--", CHECKLIST, cwd=wt)
    git("commit", "-q", "-m", "docs(checklist): file the editor test steps of the parts this branch carries",
        "-m", f"One section, read from {PLAN} on {SOURCE_BRANCH}, for: " + ", ".join(chosen) + ".", cwd=wt)


def drop_orphan_metas(wt, base):
    added = git("diff", "--name-only", "--diff-filter=A", f"{base}..HEAD", cwd=wt).stdout.splitlines()
    orphans = [p for p in added if p.endswith(".meta") and not os.path.exists(os.path.join(wt, p[:-5]))]
    if not orphans:
        return []
    git("rm", "-q", "--", *orphans, cwd=wt)
    git("commit", "-q", "-m", "chore(meta): drop folder metas for folders this branch does not carry",
        "-m", "The picked commits added them alongside parts this branch leaves out.", cwd=wt)
    return orphans


def advise(chosen):
    if "A" not in chosen:
        print("WARNING: part A is not picked. bleeding-edge's guard has been red at 'Validate project' since "
              "2026-10-08 and part A is the fix, so this branch's guard run will be red for that reason alone.")
    if "D" in chosen:
        missing = [k for k in D_DESCRIBES if k not in chosen]
        if missing:
            print("WARNING: part D's QA backlog items and notes describe parts " + ", ".join(missing)
                  + " too, which this branch does not carry; QA would test code that is not there.")


def pr_body(chosen, branch):
    chosen = [k for k in PARTS if k in chosen]
    out = ["## What and why", "", f"Selected parts of `{SOURCE_BRANCH}`:", ""]
    out += [f"- {PARTS[k][0]}" for k in chosen]
    out += ["", "## Verification status", "",
            "- Compiled offline (Unity reference compile, player and editor configs) on the source branch.",
            f"- NOT run, untested; verify in editor: the steps filed in {CHECKLIST} under `{branch}`.", ""]
    return "\n".join(out)


def cmd_check(a):
    """Every non-merge commit on the source is in exactly one part or is scaffolding; every part has steps."""
    seen, bad = {}, []
    for k, (_, commits) in PARTS.items():
        for c in commits.split():
            r = git("rev-parse", "--verify", "--quiet", c + "^{commit}", check=False)
            if r.returncode != 0:
                bad.append(f"part {k}: {c} is not a commit")
                continue
            seen.setdefault(r.stdout.strip(), []).append(k)
    for sha, keys in seen.items():
        if len(keys) > 1 and set(keys) != {"F", "H"}:
            bad.append(f"{sha[:9]} is in parts {', '.join(keys)}")
    for line in git("log", "--no-merges", "--format=%H %s", f"{a.base}..{a.source}").stdout.splitlines():
        sha, subj = line.split(" ", 1)
        if sha in seen:
            continue
        files = set(git("show", "--format=", "--name-only", sha).stdout.split())
        if files and files <= SOURCE_ONLY_PATHS:
            continue
        bad.append(f"{sha[:9]} is in no part: {subj}")
    for k in PARTS:
        try:
            plan_steps(a.source, k)
        except SystemExit as e:
            bad.append(str(e))
    for b in bad:
        print("FAIL: " + b)
    print("check OK" if not bad else f"check FAILED ({len(bad)})")
    return 1 if bad else 0


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
    advise(chosen)
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
        orphans = drop_orphan_metas(wt, a.base)
        file_checklist(wt, a, chosen)
        print(f"built {a.branch}: parts {' '.join(chosen)}, {len(commits)} commit(s) on {a.base}")
        for sha, k, p in dropped:
            print(f"  kept {a.base}'s {p} where {sha} (part {k}) conflicted on it")
        for p in orphans:
            print(f"  dropped {p}: the branch does not carry its folder")
        print(f"  filed the test steps of parts {' '.join(chosen)} in {CHECKLIST}")
        print(f"next: review `git log --oneline {a.base}..{a.branch}`, then git push -u origin {a.branch}")
        print("pull request body:\n\n" + pr_body(chosen, a.branch))
    finally:
        if ok:
            git("worktree", "remove", "--force", wt, check=False)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list").set_defaults(fn=cmd_list)
    c = sub.add_parser("check")
    c.add_argument("--base", default="origin/bleeding-edge")
    c.add_argument("--source", default="origin/" + SOURCE_BRANCH)
    c.set_defaults(fn=cmd_check)
    b = sub.add_parser("build")
    b.add_argument("parts", nargs="+")
    b.add_argument("--branch", required=True)
    b.add_argument("--base", default="origin/bleeding-edge")
    b.add_argument("--source", default="origin/" + SOURCE_BRANCH,
                   help="the branch the parts' commits live on")
    b.add_argument("--no-deps", action="store_true",
                   help="do not add the parts a chosen part needs (measurement only: the result may not build)")
    b.set_defaults(fn=cmd_build)
    a = ap.parse_args()
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())
