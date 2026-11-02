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
the person's checkout is touched. The Ys half replays, file by file, only the AI paths Ys changed since the last sync
(no rename guessing), so a non-AI change on Ys can never reach `ai`, and an AI file both sides edited differently
stops the sync instead of overwriting either side. Ys work that already reached `ai` through bleeding-edge (Yash
merges Ys there) is not replayed again, and a copy one side has already moved past gives way to the newer one. The last synced Ys commit is the
`AI-Sync-Ys:` trailer on `ai`. Needs git 2.40 or newer (`merge-tree --merge-base`).

Promotion `ai` -> bleeding-edge is an ordinary pull request.
"""
import argparse
import fnmatch
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


def _merge(ours, theirs, cwd=None):
    """(tree, conflicts) of a three-way merge, computed without a working tree (-z: paths as they are, unquoted)."""
    args = ["merge-tree", "--write-tree", "--name-only", "--no-messages", "-z"]
    p = subprocess.run(["git", *args, ours, theirs], cwd=cwd or L.REPO, capture_output=True, text=True)
    parts = p.stdout.split("\0")
    if p.returncode not in (0, 1) or not parts[0].strip():
        raise RuntimeError(f"git merge-tree: {p.stderr.strip() or p.stdout.strip()}")
    conflicts = []
    for part in parts[1:] if p.returncode == 1 else []:
        if not part:
            break
        conflicts.append(part)
    return parts[0].strip(), list(dict.fromkeys(conflicts))


def _entry(commit, path, cwd=None):
    """(mode, blob) of one file in `commit`, or None when it has no file there."""
    out = _git("ls-tree", "-z", commit, "--", path, cwd=cwd).rstrip("\0")
    if not out:
        return None
    meta, name = out.split("\t", 1)
    mode, kind, sha = meta.split(" ")
    return (mode, sha) if kind == "blob" and name == path else None


def _is_ai(path):
    for spec in AI_PATHS:
        if spec.startswith(":(glob)"):
            if fnmatch.fnmatchcase(path, spec[len(":(glob)"):]):
                return True
        elif path == spec or (spec.endswith("/") and path.startswith(spec)):
            return True
    return False


def _bases(a, b, cwd=None):
    """Every merge base of a and b (a criss-cross history has several)."""
    return _git("merge-base", "--all", a, b, cwd=cwd, check=False).split()


def _newest_base(a, b, last, cwd=None):
    """The merge base of a and b to replay Ys from instead of `last`: the newest one after `last` (one every other
    such base is an ancestor of), or None."""
    later = [m for m in _bases(a, b, cwd) if _after(last, m, cwd)]
    tops = [m for m in later if not any(_after(m, n, cwd) for n in later)]
    return tops[0] if len(tops) == 1 else None


def _after(older, newer, cwd=None):
    return older != newer and subprocess.run(["git", "merge-base", "--is-ancestor", older, newer],
                                             cwd=cwd or L.REPO).returncode == 0


def _settle_merge(path, ai, be, last, bases, cwd=None):
    """Which side wins a conflict merging bleeding-edge into ai, when the conflict is only Ys meeting itself (None
    otherwise: the sync stops). ai must hold the file exactly as the last sync took it from Ys (`last`: nobody
    edited it on ai), and bleeding-edge exactly as at `met`, the Ys commit bleeding-edge merged. A `met` after
    `last` is the newer Ys, so bleeding-edge's copy wins; a `met` before it (a pull request from an older Ys head)
    is the older one, so ai's copy stays. A file missing on any side, a mode change, a rename or an edit on either
    side matches neither and stops the sync."""
    if not _is_ai(path):
        return None
    mine, was, theirs = (_entry(c, path, cwd) for c in (ai, last, be))
    if None in (mine, was, theirs) or mine != was:
        return None
    wins = set()
    for met in bases:  # several in a criss-cross history: each must agree
        if _entry(met, path, cwd) != theirs:
            continue
        wins.add(be if _after(last, met, cwd) else ai if _after(met, last, cwd) else None)
    return wins.pop() if len(wins) == 1 else None


def _states(path, tip, since, cwd=None, limit=300):
    """The commits from `since` (exclusive) to `tip` that changed `path`, merges included, newest first."""
    return _git("rev-list", f"--max-count={limit}", "--full-history", tip, f"^{since}", "--", path, cwd=cwd).split()


def _settle_replay(path, head, ys, last, cwd=None):
    """Which side wins a conflict replaying Ys's AI work onto ai, when one side holds exactly a copy the other side
    has already moved past (None otherwise: the sync stops). ai holds a copy Ys had after `last` (it came to ai
    through bleeding-edge, which Ys merged too) and Ys went on: Ys's copy wins. Ys holds a copy ai's history went
    through and Ys shares (Ys merged that bleeding-edge commit and never touched the file after): ai's copy stays."""
    if not _is_ai(path):
        return None
    mine, theirs = _entry(head, path, cwd), _entry(ys, path, cwd)
    if None in (mine, theirs):
        return None
    if any(_entry(c, path, cwd) == mine for c in _states(path, ys, last, cwd) if c != ys):
        return ys
    shared = [c for c in _states(path, head, last, cwd) if c != head and _after(c, ys, cwd)]
    if any(_entry(c, path, cwd) == theirs for c in shared):
        return head
    return None


def _reached(last, met, head, cwd=None):
    """True when every AI file Ys changed from `last` to `met` is in `head` exactly as at `met`: Ys's work up to
    `met` really arrived (a partial or reverted promotion leaves Ys's history in bleeding-edge but not its files)."""
    for path in _changed_ai(last, met, cwd):
        if _entry(head, path, cwd) != _entry(met, path, cwd):
            return False
    return True


def _with_entries(tree, entries, cwd=None):
    """`tree` with each path in `entries` set to its (mode, blob), or removed where it is None."""
    lines = [f"{e[0]} {e[1]}\t{p}" if e else f"0 {'0' * 40}\t{p}" for p, e in entries.items()]
    index = os.path.join(tempfile.mkdtemp(prefix="ai-branch-"), "index")
    try:
        env = {"GIT_INDEX_FILE": index}
        _git("read-tree", tree, cwd=cwd, env=env)
        _git("update-index", "-z", "--index-info", cwd=cwd, env=env, input="\0".join(lines) + "\0")
        return _git("write-tree", cwd=cwd, env=env).strip()
    finally:
        shutil.rmtree(os.path.dirname(index), ignore_errors=True)


def _merge_file(base, mine, theirs, cwd=None):
    """The blob of a clean three-way line merge of three blobs, or None (a conflict, or not text)."""
    tmp = tempfile.mkdtemp(prefix="ai-branch-")
    try:
        names = []
        for name, sha in (("mine", mine), ("base", base), ("theirs", theirs)):
            p = subprocess.run(["git", "cat-file", "blob", sha], cwd=cwd or L.REPO, capture_output=True)
            if p.returncode or b"\0" in p.stdout:
                return None
            names.append(os.path.join(tmp, name))
            with open(names[-1], "wb") as fh:
                fh.write(p.stdout)
        p = subprocess.run(["git", "merge-file", "-p", *names], cwd=cwd or L.REPO, capture_output=True)
        if p.returncode != 0:
            return None
        return subprocess.run(["git", "hash-object", "-w", "--stdin"], cwd=cwd or L.REPO, input=p.stdout,
                              capture_output=True, check=True).stdout.decode().strip()
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def _replay(head, ys, last, files, cwd=None):
    """(tree, conflicts): `head` with what Ys changed in `files` from `last` to `ys`, file by file and with no
    rename guessing (git's rename detection let a Ys rename carry a file over a fix made on ai, 2026-10-29). Ys's
    copy where ai's is untouched; ai's where Ys's is; a copy one side already moved past (`_settle_replay`); a clean
    line merge where both edited the same text file; anything else (a delete against an edit, a mode change, a line
    conflict) is a conflict."""
    entries, conflicts = {}, []
    for path in files:
        was, mine, theirs = (_entry(c, path, cwd) for c in (last, head, ys))
        if mine == theirs or theirs == was:
            continue
        if mine == was:
            entries[path] = theirs
            continue
        win = _settle_replay(path, head, ys, last, cwd)
        if win:
            entries[path] = _entry(win, path, cwd)
            continue
        merged = (_merge_file(was[1], mine[1], theirs[1], cwd)
                  if None not in (was, mine, theirs) and was[0] == mine[0] == theirs[0] else None)
        if merged:
            entries[path] = (mine[0], merged)
        else:
            conflicts.append(path)
    tree = _git("rev-parse", head + "^{tree}", cwd=cwd).strip()
    return (_with_entries(tree, entries, cwd) if entries else tree), conflicts


def _settle(conflicts, judge):
    """{path: the commit whose copy wins} for every conflict, or None when `judge` cannot settle one of them."""
    picks = {p: judge(p) for p in conflicts}
    return None if None in picks.values() else picks


def _last_synced(ai, cwd=None):
    out = _git("log", "-1", "--first-parent", "--format=%(trailers:key=AI-Sync-Ys,valueonly)", f"--grep=^{TRAILER}",
               ai, cwd=cwd)
    return out.strip() or None


def _changed_ai(a, b, cwd=None):
    return [l for l in _git("diff", "--name-only", "--no-renames", a, b, "--", *AI_PATHS, cwd=cwd).splitlines() if l]


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
            bases = _bases(be, ys, cwd)
            picks = _settle(conflicts, lambda p: _settle_merge(p, ai, be, last, bases, cwd)) if conflicts else None
            if picks:
                tree, conflicts = _with_entries(tree, {p: _entry(c, p, cwd) for p, c in picks.items()}, cwd), []
            if conflicts:
                raise RuntimeError(f"merging {BASE} into {AI_BRANCH} conflicts in: {', '.join(conflicts)} - "
                                   f"resolve it by hand on a branch and open a pull request into {AI_BRANCH}")
            head = _commit(tree, [ai, be], f"merge {BASE} into {AI_BRANCH}", cwd=cwd)
            steps.append({"sha": head, "what": f"merge {BASE} @ {be[:9]}"})
        # Ys work that reached ai by another road (Yash merged Ys into bleeding-edge, which ai just took) is synced
        # already: replay only what Ys changed after it, or Ys's own earlier edits read as a conflict with themselves.
        # Only when all of it arrived, and the trailer records the new start so the next sync judges from there.
        met = _newest_base(head, ys, last, cwd)
        if met and _reached(last, met, head, cwd):
            last = met
        if steps:
            tree = _git("rev-parse", head + "^{tree}", cwd=cwd).strip()
            head = _commit(tree, [ai, be], f"merge {BASE} into {AI_BRANCH}\n\n{TRAILER} {last}", cwd=cwd)
            steps[-1]["sha"] = head
        elif last != (_last_synced(ai, cwd) or last) and not _changed_ai(last, ys, cwd):
            tree = _git("rev-parse", head + "^{tree}", cwd=cwd).strip()
            head = _commit(tree, [head], f"chore(ai): {SOURCE} @ {last[:9]} is in {AI_BRANCH} already, through "
                           f"{BASE}\n\n{TRAILER} {last}", cwd=cwd)
            steps.append({"sha": head, "what": f"{SOURCE} @ {last[:9]} already in {AI_BRANCH} (bookkeeping)"})
    files = _changed_ai(last, ys, cwd) if last != ys else []
    if files:
        tree, conflicts = _replay(head, ys, last, files, cwd)
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
        pilot = "Assets/_Scripts/Controller/AI/Pilot.cs"

        def commit_on(branch, path, text, msg):
            g("checkout", "-q", branch, cwd=work); write(work, path, text)
            g("add", "-A", cwd=work); g("commit", "-qm", msg, cwd=work); g("push", "-q", "origin", branch, cwd=work)
            g("fetch", "-q", "origin", cwd=work)
            return g("rev-parse", "HEAD", cwd=work).strip()

        def merge_into(branch, what):
            g("checkout", "-q", branch, cwd=work); g("merge", "-q", "--no-edit", what, cwd=work)
            g("push", "-q", "origin", branch, cwd=work); g("fetch", "-q", "origin", cwd=work)

        # a pull request from an OLDER Ys head lands on bleeding-edge after the sync took a newer one: ai keeps
        # the newer copy, and the sync does not stop
        older = commit_on(SOURCE, pilot, "pilot v2.3\n", "ys 2.3")
        commit_on(SOURCE, pilot, "pilot v2.4\n", "ys 2.4")
        run_sync(); g("fetch", "-q", "origin", cwd=work)
        merge_into(BASE, older)
        try:
            run_sync(); g("fetch", "-q", "origin", cwd=work)
            if show(pilot) != "pilot v2.4\n":
                bad.append("ai branch: an older Ys promoted into bleeding-edge rolled ai back")
        except RuntimeError as e:
            bad.append(f"ai branch: an older Ys promoted into bleeding-edge stopped the sync: {e}")
        # Ys merges bleeding-edge, which carries an AI file edited there, and the sweep edits on top: Ys's copy
        notes = "Docs/AI_SYSTEM/notes.md"
        commit_on(BASE, notes, "notes from bleeding-edge\n", "be notes")
        run_sync(); g("fetch", "-q", "origin", cwd=work)
        merge_into(SOURCE, f"origin/{BASE}")
        commit_on(SOURCE, notes, "notes from bleeding-edge\nand the sweep\n", "sweep notes")
        try:
            run_sync(); g("fetch", "-q", "origin", cwd=work)
            if show(notes) != "notes from bleeding-edge\nand the sweep\n":
                bad.append("ai branch: the sweep's edit on top of a bleeding-edge copy did not come over")
        except RuntimeError as e:
            bad.append(f"ai branch: Ys merging bleeding-edge then editing on top stopped the sync: {e}")
        # a fix made on ai, and Ys renames the file and is promoted: the conflict is named under the new name, which
        # ai does not have - it must stop the sync, not drop ai's fix (found 2026-10-29)
        brain, mind = "Assets/_Scripts/Controller/AI/Brain.cs", "Assets/_Scripts/Controller/AI/Mind.cs"
        lines = [f"line {i}\n" for i in range(20)]
        commit_on(SOURCE, brain, "".join(lines), "ys brain")
        run_sync(); g("fetch", "-q", "origin", cwd=work)
        g("checkout", "-q", "-B", "fix", f"origin/{AI_BRANCH}", cwd=work)
        write(work, brain, "".join(lines[:10] + ["fixed on ai\n"] + lines[11:]))
        g("commit", "-qam", "ai fix", cwd=work); g("push", "-q", "origin", f"fix:{AI_BRANCH}", cwd=work)
        g("checkout", "-q", SOURCE, cwd=work); g("mv", brain, mind, cwd=work)
        write(work, mind, "".join(lines[:10] + ["changed on Ys\n"] + lines[11:]))
        g("add", "-A", cwd=work); g("commit", "-qm", "ys renames", cwd=work); g("push", "-q", "origin", SOURCE, cwd=work)
        fixed = L.rev(f"origin/{BASE}", cwd=work)
        merge_into(BASE, f"origin/{SOURCE}")
        try:
            plan(cwd=work)
            bad.append("ai branch: a rename on Ys silently dropped a fix made on ai (negative control)")
        except RuntimeError:
            pass
        g("push", "-q", "-f", "origin", f"{fixed}:{BASE}", cwd=work)  # undo the promotion for the case below
        g("fetch", "-q", "origin", cwd=work)
        # an AI file edited differently on ai and on Ys must stop the sync
        g("checkout", "-q", "-B", "fix", f"origin/{AI_BRANCH}", cwd=work)
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
