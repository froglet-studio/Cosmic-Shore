"""The `ai` branch: one home for the vessel AI that belongs to no person - `python3 Tools/Compute/ai_branch.py`.

    status            how far `ai` is behind bleeding-edge, and which AI files changed on Ys-bleeding-edge since the
                      last sync
    sync [--push]     bring both in: merge bleeding-edge into `ai`, then replay ONLY the AI paths (AI_PATHS) that
                      changed on Ys-bleeding-edge since the last sync. Without --push it builds the commits and prints
                      them; with --push it moves origin/ai (fast-forward only)
    --self-test       a scratch repo: the AI change comes over, the non-AI change does not, a second sync is a no-op,
                      a conflicting AI edit is refused

Why: until 2026-10-28 every AI job and the hourly AI learning sweep read and wrote `Ys-bleeding-edge`, Yash's own
integration branch, which also carries his unpromoted features (the Meerkat, the Vessel Studio, shaders). `ai` holds
only bleeding-edge plus AI work, so Froglet Compute and anyone else can depend on it (Shombith, 2026-10-28). While
the sweep still pushes to Ys-bleeding-edge, this sync is the bridge; once the sweep pushes to `ai`, the Ys half of
the sync finds nothing to replay.

How: plumbing only (`merge-tree --write-tree`, `commit-tree`, `update-ref`), so no working tree, index or config of
the person's checkout is touched. The Ys half is a three-way merge of a synthetic commit (the last synced Ys tree
with only its AI paths replaced by Ys's current ones), so a non-AI change on Ys can never reach `ai`, and an AI file
both sides edited differently stops the sync instead of overwriting either side. The last synced Ys commit is the
`AI-Sync-Ys:` trailer on `ai`. Needs git 2.40 or newer (`merge-tree --merge-base`).

Promotion `ai` -> bleeding-edge is an ordinary pull request.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import compute_lib as L  # noqa: E402

AI_BRANCH = "ai"
BASE = "bleeding-edge"
SOURCE = "Ys-bleeding-edge"
TRAILER = "AI-Sync-Ys:"

# What counts as AI work. Taken from the ownership table in Docs/AI_SYSTEM/BRANCH_WORKFLOW.md and the AI tools and
# assets measured on bleeding-edge (2026-10-28). Only these paths are replayed from Ys-bleeding-edge; extend it here.
AI_PATHS = [
    "Assets/_Scripts/Controller/AI/",
    "Assets/_Scripts/Utility/AITraining/",
    "Assets/Resources/AIBoostPolicies/",
    "Assets/Resources/RaceLines/",
    ":(glob)Assets/Resources/SkimRaceAIConfig*",
    "Docs/AI_SYSTEM/",
    ":(glob)Docs/SKIM_RACE_AI*",
    "Tools/Build/ai_learn.py",
    "Tools/Build/ai_branch_sync.py",
    "Tools/Build/author_ai_difficulty_row.py",
    "Tools/Build/author_race_lines.py",
    "Tools/Build/author_skimrace_ai_config.py",
    "Tools/Build/check_ai_no_state_writes.py",
    ":(glob)Tools/Build/*_drive_sim.py",
    "Tools/Build/learn_drives_from_flights.py",
    "Tools/Build/learn_from_flights.py",
    "Tools/Build/race_lines_learned.json",
    "Tools/Build/racing_line.py",
    "Tools/Build/regatta_balance.py",
    ":(glob)Tools/Build/skimrace_*.py",
    "Tools/Build/skimrace_sim_harness/",
    "Tools/Build/situation_fixtures/",
]

IDENT = {"GIT_AUTHOR_NAME": "froglet-compute (ai sync)", "GIT_AUTHOR_EMAIL": "compute@froglet.invalid",
         "GIT_COMMITTER_NAME": "froglet-compute (ai sync)", "GIT_COMMITTER_EMAIL": "compute@froglet.invalid"}


def _git(*args, cwd=None, env=None, check=True, input=None):
    return L.git(*args, cwd=cwd, env=dict(os.environ, **(env or {})), check=check, input=input)


def _commit(tree, parents, message, cwd=None):
    args = ["commit-tree", tree, "-m", message]
    for p in parents:
        args += ["-p", p]
    return _git(*args, cwd=cwd, env=IDENT).strip()


def _merge(ours, theirs, base=None, cwd=None):
    """(tree, conflicts) of a three-way merge, computed without a working tree."""
    args = ["merge-tree", "--write-tree", "--name-only", "--no-messages"]
    if base:
        args.append(f"--merge-base={base}")
    p = subprocess.run(["git", *args, ours, theirs], cwd=cwd or L.REPO, capture_output=True, text=True)
    lines = p.stdout.strip().splitlines()
    if p.returncode not in (0, 1) or not lines:
        raise RuntimeError(f"git merge-tree: {p.stderr.strip() or p.stdout.strip()}")
    return lines[0], ([l for l in lines[1:] if l.strip()] if p.returncode == 1 else [])


def _ai_view(base, source, cwd=None):
    """A commit whose tree is `base`'s with only the AI_PATHS changes from `base` to `source` applied: what Ys
    changed in AI, and nothing else."""
    raw = _git("diff", "--raw", "-z", "--no-renames", "--no-abbrev", base, source, "--", *AI_PATHS, cwd=cwd)
    parts = raw.split("\0")
    lines = []
    for i in range(0, len(parts) - 1, 2):
        meta, path = parts[i], parts[i + 1]
        _, newmode, _, newsha, state = meta.lstrip(":").split(" ")
        if state == "D":
            lines.append(f"0 {'0' * 40}\t{path}")
        else:
            lines.append(f"{newmode} {newsha}\t{path}")
    index = os.path.join(tempfile.mkdtemp(prefix="ai-branch-"), "index")
    try:
        env = {"GIT_INDEX_FILE": index}
        _git("read-tree", base, cwd=cwd, env=env)
        if lines:
            _git("update-index", "-z", "--index-info", cwd=cwd, env=env, input="\0".join(lines) + "\0")
        tree = _git("write-tree", cwd=cwd, env=env).strip()
    finally:
        shutil.rmtree(os.path.dirname(index), ignore_errors=True)
    return _commit(tree, [base], "ai view of " + source[:12], cwd=cwd)


def _blob(commit, path, cwd=None):
    out = L.git_ok("rev-parse", "--verify", "--quiet", f"{commit}:{path}", cwd=cwd)
    return out.strip() if out else None


def _promoted(path, ai, be, ys, last, cwd=None):
    """True when a conflict merging bleeding-edge into ai is only Ys meeting itself: ai holds the copy the last sync
    took from Ys (nobody edited it on ai), and bleeding-edge holds a later Ys state of it, merged there by Ys's owner.
    Then bleeding-edge's copy is the newer Ys and the Ys step after the merge brings whatever Ys changed since."""
    met = _git("merge-base", be, ys, cwd=cwd, check=False).strip()
    if not met or met == last or subprocess.run(["git", "merge-base", "--is-ancestor", last, met],
                                                cwd=cwd or L.REPO).returncode != 0:
        return False
    mine, theirs = _blob(ai, path, cwd), _blob(be, path, cwd)
    return mine == _blob(last, path, cwd) and theirs == _blob(met, path, cwd)


def _with_paths(tree, source, paths, cwd=None):
    """`tree` with `paths` set to what `source` holds (removed where `source` has none)."""
    lines = []
    for path in dict.fromkeys(paths):
        entry = _git("ls-tree", "-z", source, "--", path, cwd=cwd).rstrip("\0")
        if entry:
            meta, name = entry.split("\t", 1)
            mode, _, sha = meta.split(" ")
            lines.append(f"{mode} {sha}\t{name}")
        else:
            lines.append(f"0 {'0' * 40}\t{path}")
    index = os.path.join(tempfile.mkdtemp(prefix="ai-branch-"), "index")
    try:
        env = {"GIT_INDEX_FILE": index}
        _git("read-tree", tree, cwd=cwd, env=env)
        _git("update-index", "-z", "--index-info", cwd=cwd, env=env, input="\0".join(lines) + "\0")
        return _git("write-tree", cwd=cwd, env=env).strip()
    finally:
        shutil.rmtree(os.path.dirname(index), ignore_errors=True)


def _last_synced(ai, cwd=None):
    out = _git("log", "-1", "--first-parent", "--format=%(trailers:key=AI-Sync-Ys,valueonly)", f"--grep=^{TRAILER}",
               ai, cwd=cwd)
    return out.strip() or None


def _changed_ai(a, b, cwd=None):
    return [l for l in _git("diff", "--name-only", a, b, "--", *AI_PATHS, cwd=cwd).splitlines() if l]


def plan(cwd=None, remote="origin"):
    """The commits a sync would add to `ai` (none when it is current), and what each brings."""
    be, ys = L.rev(f"{remote}/{BASE}", cwd), L.rev(f"{remote}/{SOURCE}", cwd)
    if not be or not ys:
        raise RuntimeError(f"fetch {BASE} and {SOURCE} first")
    ai = L.rev(f"{remote}/{AI_BRANCH}", cwd)
    steps = []
    if ai is None:
        # Start: bleeding-edge as it is, plus whatever AI Ys has that bleeding-edge does not.
        last = _git("merge-base", be, ys, cwd=cwd).strip()
        head = be
    else:
        last = _last_synced(ai, cwd)
        if not last:
            raise RuntimeError(f"{remote}/{AI_BRANCH} has no '{TRAILER}' trailer: it was not made by this tool")
        head = ai
        if subprocess.run(["git", "merge-base", "--is-ancestor", be, ai], cwd=cwd or L.REPO).returncode != 0:
            tree, conflicts = _merge(ai, be, cwd=cwd)
            if conflicts and all(_promoted(p, ai, be, ys, last, cwd) for p in conflicts):
                tree = _with_paths(tree, be, conflicts, cwd)
                conflicts = []
            if conflicts:
                raise RuntimeError(f"merging {BASE} into {AI_BRANCH} conflicts in: {', '.join(conflicts)} - "
                                   f"resolve it by hand on a branch and open a pull request into {AI_BRANCH}")
            head = _commit(tree, [ai, be], f"merge {BASE} into {AI_BRANCH}\n\n{TRAILER} {last}", cwd=cwd)
            steps.append({"sha": head, "what": f"merge {BASE} @ {be[:9]}"})
        # Ys history that reached ai by another road (Yash merged Ys into bleeding-edge, which ai just took) is synced
        # already: replay only what Ys changed after it, or its own earlier edits read as a conflict with themselves.
        met = _git("merge-base", head, ys, cwd=cwd, check=False).strip()
        if met and met != last and subprocess.run(["git", "merge-base", "--is-ancestor", last, met],
                                                  cwd=cwd or L.REPO).returncode == 0:
            last = met
    files = _changed_ai(last, ys, cwd) if last != ys else []
    if files:
        view = _ai_view(last, ys, cwd)
        tree, conflicts = _merge(head, view, base=last, cwd=cwd)
        if conflicts:
            raise RuntimeError(f"AI files changed on both {AI_BRANCH} and {SOURCE}: {', '.join(conflicts)} - "
                               "resolve it by hand and open a pull request into " + AI_BRANCH)
        msg = (f"chore(ai): AI work from {SOURCE} @ {ys[:9]} ({len(files)} files)\n\n"
               + "\n".join(f"- {f}" for f in files[:40]) + ("\n- ..." if len(files) > 40 else "")
               + f"\n\nOnly AI_PATHS (Tools/Compute/ai_branch.py) are replayed.\n\n{TRAILER} {ys}")
        head = _commit(tree, [head], msg, cwd=cwd)
        steps.append({"sha": head, "what": f"{len(files)} AI files from {SOURCE} @ {ys[:9]}", "files": files})
    elif ai is None:
        tree = _git("rev-parse", be + "^{tree}", cwd=cwd).strip()
        head = _commit(tree, [be], f"chore(ai): start the {AI_BRANCH} branch from {BASE} @ {be[:9]}\n\n"
                       f"{SOURCE} @ {ys[:9]} has no AI change that {BASE} lacks.\n\n{TRAILER} {ys}", cwd=cwd)
        steps.append({"sha": head, "what": f"start from {BASE} @ {be[:9]}"})
    return {"ai": ai, "head": head, "steps": steps, "base": be, "source": ys, "last": last}


def status(cwd=None, remote="origin"):
    p = plan(cwd, remote)
    ai = p["ai"]
    if ai is None:
        print(f"{remote}/{AI_BRANCH} does not exist yet: `sync --push` creates it from {BASE}")
    else:
        behind = _git("rev-list", "--count", f"{ai}..{p['base']}", cwd=cwd).strip()
        print(f"{remote}/{AI_BRANCH} @ {ai[:9]}: {behind} commits behind {BASE}; last synced {SOURCE} @ {p['last'][:9]}")
    for s in p["steps"]:
        print(f"  would add {s['sha'][:9]}: {s['what']}")
        for f in s.get("files", [])[:20]:
            print(f"      {f}")
    if not p["steps"]:
        print("  current: nothing to bring in")
    return 0


def sync(push=False, cwd=None, remote="origin", fetch=True):
    if fetch:
        L.fetch([BASE, SOURCE, AI_BRANCH], results=False, cwd=cwd)
    p = plan(cwd, remote)
    if not p["steps"]:
        print(f"{AI_BRANCH} is current")
        return 0
    for s in p["steps"]:
        print(f"{s['sha'][:9]}  {s['what']}")
    if not push:
        print(f"(not pushed; `sync --push` moves {remote}/{AI_BRANCH} to {p['head'][:9]})")
        return 0
    lease = [f"--force-with-lease=refs/heads/{AI_BRANCH}:{p['ai'] or ''}"]
    _git("push", "--quiet", *lease, remote, f"{p['head']}:refs/heads/{AI_BRANCH}", cwd=cwd)
    _git("update-ref", f"refs/remotes/{remote}/{AI_BRANCH}", p["head"], cwd=cwd)
    print(f"pushed {remote}/{AI_BRANCH} -> {p['head'][:9]}")
    return 0


# ── self-test ────────────────────────────────────────────────────────────────

def self_test():
    bad = []
    tmp = tempfile.mkdtemp(prefix="ai-branch-selftest-")
    env = dict(os.environ, **IDENT)

    def g(*a, cwd):
        return subprocess.run(["git", *a], cwd=cwd, env=env, capture_output=True, text=True, check=True).stdout

    def write(work, path, text):
        full = os.path.join(work, path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w") as fh:
            fh.write(text)

    try:
        remote, work = os.path.join(tmp, "remote.git"), os.path.join(tmp, "work")
        g("init", "--bare", "-q", remote, cwd=tmp)
        g("init", "-q", "-b", BASE, work, cwd=tmp)
        write(work, "Assets/_Scripts/Controller/AI/Pilot.cs", "pilot v1\n")
        write(work, "Assets/_Scripts/Controller/Vessel/Hull.cs", "hull v1\n")
        g("add", "-A", cwd=work); g("commit", "-qm", "init", cwd=work)
        g("remote", "add", "origin", remote, cwd=work); g("push", "-q", "origin", BASE, cwd=work)
        g("checkout", "-qb", SOURCE, cwd=work)
        write(work, "Assets/_Scripts/Controller/AI/Pilot.cs", "pilot v2 (learned on Ys)\n")
        write(work, "Assets/_Scripts/Controller/Vessel/Hull.cs", "hull v2 (Ys only, not AI)\n")
        g("commit", "-qam", "ys work", cwd=work); g("push", "-q", "origin", SOURCE, cwd=work)
        g("fetch", "-q", "origin", cwd=work)

        def run_sync():
            return sync(push=True, cwd=work, fetch=False)

        run_sync()
        g("fetch", "-q", "origin", cwd=work)
        show = lambda path: g("show", f"origin/{AI_BRANCH}:{path}", cwd=work)
        if show("Assets/_Scripts/Controller/AI/Pilot.cs") != "pilot v2 (learned on Ys)\n":
            bad.append("ai branch: the AI change on Ys did not come over")
        if show("Assets/_Scripts/Controller/Vessel/Hull.cs") != "hull v1\n":
            bad.append("ai branch: a non-AI change on Ys reached ai (negative control)")
        first = L.rev(f"origin/{AI_BRANCH}", cwd=work)
        if plan(cwd=work)["steps"]:
            bad.append("ai branch: a second sync with nothing new still wants to commit")
        # bleeding-edge moves on; ai takes it by merge
        g("checkout", "-q", BASE, cwd=work)
        write(work, "Docs/readme.md", "new on bleeding-edge\n")
        g("add", "-A", cwd=work); g("commit", "-qm", "be work", cwd=work); g("push", "-q", "origin", BASE, cwd=work)
        g("fetch", "-q", "origin", cwd=work)
        run_sync(); g("fetch", "-q", "origin", cwd=work)
        if show("Docs/readme.md") != "new on bleeding-edge\n":
            bad.append("ai branch: bleeding-edge was not merged in")
        if subprocess.run(["git", "merge-base", "--is-ancestor", first, f"origin/{AI_BRANCH}"], cwd=work).returncode:
            bad.append("ai branch: the sync rewrote ai's history")
        # Ys is merged into bleeding-edge (its owner promotes it) and then keeps learning on the same AI file: ai
        # takes both, Ys's newer version, without reading Ys's own earlier edit as a conflict (2026-10-29)
        g("checkout", "-q", SOURCE, cwd=work)
        write(work, "Assets/_Scripts/Controller/AI/Pilot.cs", "pilot v2.1 (promoted)\n")
        g("commit", "-qam", "ys learns", cwd=work); g("push", "-q", "origin", SOURCE, cwd=work)
        g("checkout", "-q", BASE, cwd=work)
        g("merge", "-q", "--no-edit", SOURCE, cwd=work); g("push", "-q", "origin", BASE, cwd=work)
        g("checkout", "-q", SOURCE, cwd=work)
        write(work, "Assets/_Scripts/Controller/AI/Pilot.cs", "pilot v2.2 (after the promotion)\n")
        g("commit", "-qam", "ys learns again", cwd=work); g("push", "-q", "origin", SOURCE, cwd=work)
        g("fetch", "-q", "origin", cwd=work)
        try:
            run_sync(); g("fetch", "-q", "origin", cwd=work)
            if show("Assets/_Scripts/Controller/AI/Pilot.cs") != "pilot v2.2 (after the promotion)\n":
                bad.append("ai branch: after Ys was promoted, its newer AI change did not come over")
            if show("Assets/_Scripts/Controller/Vessel/Hull.cs") != "hull v2 (Ys only, not AI)\n":
                bad.append("ai branch: bleeding-edge's copy of Ys's non-AI file did not arrive through the merge")
        except RuntimeError as e:
            bad.append(f"ai branch: Ys promoted into bleeding-edge then edited again was refused: {e}")
        # an AI file edited differently on ai and on Ys must stop the sync
        g("checkout", "-qb", "fix", f"origin/{AI_BRANCH}", cwd=work)
        write(work, "Assets/_Scripts/Controller/AI/Pilot.cs", "pilot v3 (fixed on ai)\n")
        g("commit", "-qam", "ai fix", cwd=work); g("push", "-q", "origin", f"fix:{AI_BRANCH}", cwd=work)
        g("checkout", "-q", SOURCE, cwd=work)
        write(work, "Assets/_Scripts/Controller/AI/Pilot.cs", "pilot v3 (other on Ys)\n")
        g("commit", "-qam", "ys again", cwd=work); g("push", "-q", "origin", SOURCE, cwd=work)
        g("fetch", "-q", "origin", cwd=work)
        try:
            plan(cwd=work)
            bad.append("ai branch: an AI file edited on both sides was overwritten instead of refused")
        except RuntimeError as e:
            if "changed on both" not in str(e):
                bad.append(f"ai branch: wrong refusal: {e}")
        if g("status", "--porcelain", cwd=work).strip():
            bad.append("ai branch: the sync dirtied the checkout")
    except Exception as e:
        bad.append(f"ai branch: {e}")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    return bad


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", nargs="?", choices=("status", "sync"), default="status")
    ap.add_argument("--push", action="store_true", help="move origin/ai (sync only)")
    ap.add_argument("--no-fetch", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        bad = self_test()
        print("ai branch self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
        return 1 if bad else 0
    try:
        if a.command == "sync":
            return sync(push=a.push, fetch=not a.no_fetch)
        if not a.no_fetch:
            L.fetch([BASE, SOURCE, AI_BRANCH], results=False)
        return status()
    except RuntimeError as e:
        print(f"stopped: {e}")
        return 2


if __name__ == "__main__":
    sys.exit(main())
