"""Forget: remove a computer, or a run's data, from Froglet Compute (the console's Remove and Delete).

    python3 Tools/Compute/forget.py machine <user>/<host> [--push]      drop its results branch (compute/<user>/<host>)
    python3 Tools/Compute/forget.py run <request id> [--push]           drop that run's folders from every results branch
    python3 Tools/Compute/forget.py --self-test

A computer's results live on its own branch, `compute/<user>/<host>` (README section 5): forgetting the computer deletes
that branch on origin, so the board, the console and every merged number stop counting it. The hub also tells the
computer's agent to uninstall itself at its next heartbeat (`leave`), so the person's PC stops without a command.
A run sent from the console (`<job>~<request id>`) publishes into `jobs/<job>~<id>/` and `requests/<id>*` on every
machine that worked on it: forgetting the run removes those paths with one plumbing commit per branch (a private
index, `write-tree`, `commit-tree`, a force-with-lease push); nothing else on the branch changes, and the request is
cancelled first so no machine keeps working on it. Both are a person's choice from the console (Shombith, 2026-11-01:
"options to remove PC, data, run data etc from the console").
"""
import argparse
import os
import re
import subprocess
import sys
import tempfile

import compute_lib as L

ID = re.compile(r"r\d{8}-\d{6}-[0-9a-f]{4}")


def _git(*args, cwd=None, env=None, check=True, input=None):
    p = subprocess.run(["git", *args], cwd=cwd or L.REPO, env=env, input=input, capture_output=True, text=True, **L.NO_WINDOW)
    if check and p.returncode:
        raise RuntimeError(f"git {' '.join(args)}: {p.stderr.strip() or p.stdout.strip()}")
    return p.stdout


def without(tree, paths, cwd=None):
    """`tree` with every path in `paths` removed (a private index; the checkout is never touched)."""
    index = os.path.join(tempfile.mkdtemp(prefix="forget-"), "index")
    try:
        env = dict(os.environ, GIT_INDEX_FILE=index)
        _git("read-tree", tree, cwd=cwd, env=env)
        lines = [f"0 {'0' * 40}\t{p}" for p in paths]
        _git("update-index", "-z", "--index-info", cwd=cwd, env=env, input="\0".join(lines) + "\0")
        return _git("write-tree", cwd=cwd, env=env).strip()
    finally:
        try:
            os.remove(index)
            os.rmdir(os.path.dirname(index))
        except OSError:
            pass


def run_paths(ref, request_id, cwd=None):
    """The paths a run left on a results branch: jobs/<job>~<id>/** and requests/<id>*."""
    listing = _git("ls-tree", "-r", "--name-only", ref, cwd=cwd, check=False) or ""
    out = []
    for path in listing.splitlines():
        parts = path.split("/")
        if len(parts) >= 3 and parts[0] == "jobs" and parts[1].endswith("~" + request_id):
            out.append(path)
        elif len(parts) == 2 and parts[0] == "requests" and parts[1].startswith(request_id):
            out.append(path)
    return out


def forget_run(request_id, push=True, remote="origin", cwd=None):
    """Drop the run's data from every results branch: {ok, branches, files, note}."""
    if not ID.fullmatch(str(request_id)):
        return {"ok": False, "note": f"{request_id!r} is not a request id"}
    branches, files = 0, 0
    for b in L.result_branches(cwd=cwd):
        ref = b["ref"]
        paths = run_paths(ref, request_id, cwd)
        if not paths:
            continue
        head = _git("rev-parse", ref, cwd=cwd).strip()
        tree = without(f"{head}^{{tree}}", paths, cwd)
        sha = _git("commit-tree", tree, "-p", head, "-m", f"compute: forget run {request_id} ({len(paths)} files)", cwd=cwd,
                   env=dict(os.environ, GIT_AUTHOR_NAME="froglet-compute", GIT_AUTHOR_EMAIL="compute@froglet.studio",
                            GIT_COMMITTER_NAME="froglet-compute", GIT_COMMITTER_EMAIL="compute@froglet.studio")).strip()
        branch = ref.replace(f"{remote}/", "", 1)
        if push:
            _git("push", "--quiet", f"--force-with-lease=refs/heads/{branch}:{head}", remote, f"{sha}:refs/heads/{branch}", cwd=cwd)
        _git("update-ref", f"refs/remotes/{remote}/{branch}", sha, cwd=cwd)
        branches += 1
        files += len(paths)
    return {"ok": True, "branches": branches, "files": files,
            "note": f"run {request_id}: {files} file(s) removed from {branches} computer(s)" if branches else f"run {request_id}: nothing published for it"}


def forget_machine(machine, push=True, remote="origin", cwd=None):
    """Delete the computer's results branch on origin: {ok, note}. The hub tells its agent to leave separately."""
    user, _, host = str(machine).partition("/")
    if not (user and host) or not re.fullmatch(r"[a-z0-9._-]+/[a-z0-9._-]+", str(machine)):
        return {"ok": False, "note": f"{machine!r} is not <user>/<host>"}
    branch = f"{L.RESULTS_PREFIX}/{user}/{host}"
    if push:
        _git("push", "--quiet", remote, "--delete", f"refs/heads/{branch}", cwd=cwd, check=False)
    _git("update-ref", "-d", f"refs/remotes/{remote}/{branch}", cwd=cwd, check=False)
    return {"ok": True, "note": f"{machine} removed: its results branch is gone" + ("" if push else " here (not pushed)")}


def self_test():
    bad = []
    tmp = tempfile.mkdtemp(prefix="forget-test-")
    env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@t", GIT_COMMITTER_NAME="t", GIT_COMMITTER_EMAIL="t@t")
    def g(*a, cwd=tmp):
        return _git(*a, cwd=cwd, env=env)
    g("init", "-q", "-b", "main")
    files = {"heartbeat.json": "{}", "jobs/unity-train~r20261101-000000-abcd/e1/files/u/genome.json": "{}",
             "jobs/unity-train~r20261101-000000-abcd/e1/unit-u.json": "{}", "jobs/unity-train/e2/files/u/status.json": "{}",
             "requests/r20261101-000000-abcd.json": "{}", "requests/r20261101-000000-ffff.json": "{}"}
    for p, t in files.items():
        os.makedirs(os.path.dirname(os.path.join(tmp, p)) or tmp, exist_ok=True)
        with open(os.path.join(tmp, p), "w") as fh:
            fh.write(t)
    g("add", "-A"); g("commit", "-q", "-m", "results")
    head = g("rev-parse", "HEAD").strip()
    paths = run_paths("HEAD", "r20261101-000000-abcd", cwd=tmp)
    want = sorted(p for p in files if "~r20261101-000000-abcd" in p or p == "requests/r20261101-000000-abcd.json")
    if sorted(paths) != want:
        bad.append(f"the run's paths are wrong: {sorted(paths)} != {want}")
    tree = without(f"{head}^{{tree}}", paths, cwd=tmp)
    left = sorted(g("ls-tree", "-r", "--name-only", tree).split())
    keep = sorted(p for p in files if p not in want)
    if left != keep:
        bad.append(f"the tree after forgetting is wrong: {left} != {keep}")
    if g("status", "--porcelain").strip():
        bad.append("the checkout was touched")
    if forget_run("not-an-id", push=False)["ok"] or forget_machine("nope", push=False)["ok"]:
        bad.append("a bad id or machine name was accepted")
    return bad


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--self-test", action="store_true")
    sub = ap.add_subparsers(dest="cmd")
    m = sub.add_parser("machine"); m.add_argument("machine"); m.add_argument("--push", action="store_true")
    r = sub.add_parser("run"); r.add_argument("id"); r.add_argument("--push", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        bad = self_test()
        print("forget self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
        return 1 if bad else 0
    if a.cmd == "machine":
        res = forget_machine(a.machine, push=a.push)
    elif a.cmd == "run":
        res = forget_run(a.id, push=a.push)
    else:
        ap.print_help(); return 0
    print(res["note"])
    return 0 if res["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
