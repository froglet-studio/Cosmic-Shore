"""`compute.py self-test`: every piece, then the whole pipe end to end against a throwaway git remote.

    1. the searchers (optimizers.self_test: they search, they resume, a negative control fails)
    2. the course port's parity with the C# harness (course_port.self_test)
    3. every evaluator's own self-test
    4. every job file validates and names an evaluator that imports
    5. end to end (skipped by --quick): a scratch repo + bare remote; two "machines" run the toy job; the remote must
       hold both results branches, the second machine must start from the first one's best, the board must merge
       them, and the scratch repo's own checkout must be untouched (no dirty file, not made sparse).
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path[:0] = [HERE, os.path.join(HERE, "evaluators")]
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(REPO, "Tools", "Build"))

import compute_lib as L  # noqa: E402

EVALUATORS = ("toy", "drive_fit", "regatta_course", "regatta_elements", "bioform_forms")


def _git(*a, cwd, env=None):
    p = subprocess.run(["git", *a], cwd=cwd, capture_output=True, text=True, env=env)
    if p.returncode:
        raise RuntimeError(f"git {' '.join(a)}: {p.stderr}")
    return p.stdout


def end_to_end():
    bad = []
    tmp = tempfile.mkdtemp(prefix="froglet-compute-selftest-")
    try:
        remote = os.path.join(tmp, "remote.git")
        work = os.path.join(tmp, "work")
        _git("init", "--bare", "-q", remote, cwd=tmp)
        _git("init", "-q", "-b", "bleeding-edge", work, cwd=tmp)
        shutil.copytree(HERE, os.path.join(work, "Tools", "Compute"),
                        ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
        jobs = os.path.join(work, "Docs", "Compute", "jobs")
        os.makedirs(jobs)
        job = {"schema": L.SCHEMA, "id": "toy-smoke", "title": "self-test", "goal": "prove the pipe", "kind": "search",
               "status": "ready", "value": 5, "updated": "2026-01-01", "ref": "origin/bleeding-edge",
               "paths": ["Tools/Compute/"], "requires": ["python3"],
               "evaluator": {"module": "toy", "args": {"target": 1.5}},
               "space": [{"name": f"x{i}", "lo": -5.0, "hi": 5.0, "init": -4.0} for i in range(4)],
               "algorithm": {"name": "sep-cmaes", "popsize": 8}}
        with open(os.path.join(jobs, "toy-smoke.json"), "w") as fh:
            json.dump(job, fh)
        env0 = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@t", GIT_COMMITTER_NAME="t", GIT_COMMITTER_EMAIL="t@t")
        _git("add", "-A", cwd=work, env=env0)
        _git("commit", "-qm", "init", cwd=work, env=env0)
        _git("remote", "add", "origin", remote, cwd=work)
        _git("push", "-q", "origin", "bleeding-edge", cwd=work)
        _git("fetch", "-q", "origin", cwd=work)
        script = os.path.join(work, "Tools", "Compute", "compute.py")
        outs = []
        for host in ("box1", "box2"):
            env = dict(env0, FROGLET_COMPUTE_HOME=os.path.join(tmp, "cache-" + host), FROGLET_COMPUTE_USER="selftest",
                       FROGLET_COMPUTE_HOST=host)
            p = subprocess.run([sys.executable, script, "run", "--no-update", "--always", "--once", "--job", "toy-smoke",
                                "--max-evals", "160", "--cores", "2", "--queue", "bleeding-edge"],
                               cwd=work, env=env, capture_output=True, text=True, timeout=600)
            outs.append(p.stdout + p.stderr)
            if p.returncode != 0:
                bad.append(f"end to end: worker {host} exited {p.returncode}:\n{p.stdout[-1500:]}{p.stderr[-1500:]}")
                return bad
        heads = _git("ls-remote", "--heads", remote, cwd=tmp)
        for host in ("box1", "box2"):
            if f"refs/heads/compute/selftest/{host}" not in heads:
                bad.append(f"end to end: no results branch for {host} on the remote")
        if "starting from the team's best" not in outs[1]:
            bad.append("end to end: the second machine did not start from the first one's best")
        if _git("status", "--porcelain", cwd=work).strip():
            bad.append("end to end: the worker dirtied the checkout it was started from:\n" + _git("status", "--porcelain", cwd=work))
        if subprocess.run(["git", "config", "--get", "core.sparseCheckout"], cwd=work, capture_output=True, text=True).stdout.strip() == "true":
            bad.append("end to end: the worker made the person's own checkout sparse")
        if not os.path.isfile(os.path.join(work, "Docs", "Compute", "jobs", "toy-smoke.json")):
            bad.append("end to end: the person's checkout lost files")
        env = dict(env0, FROGLET_COMPUTE_HOME=os.path.join(tmp, "cache-board"))
        p = subprocess.run([sys.executable, script, "board", "--queue", "bleeding-edge"], cwd=work, env=env,
                           capture_output=True, text=True, timeout=300)
        if "toy-smoke" not in p.stdout or "selftest/box1" not in p.stdout:
            bad.append("end to end: the board does not show both machines' results:\n" + p.stdout[-1500:] + p.stderr[-800:])
        else:
            row = next(l for l in p.stdout.splitlines() if "`toy-smoke`" in l)
            cells = [c.strip() for c in row.split("|")]
            best, base = cells[5].split(" (")
            if not float(best) < float(base.rstrip(")")):
                bad.append(f"end to end: the best ({best}) is not better than the shipped values ({base})")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    return bad


def run(quick=False):
    import importlib
    import optimizers
    import course_port
    failures = []
    failures += optimizers.self_test()
    print("searchers: " + ("ok" if not failures else "FAILED"))
    if course_port.self_test():
        failures.append("course_port parity")
    for name in EVALUATORS:
        m = importlib.import_module(name)
        f = m.self_test() if hasattr(m, "self_test") else []
        print(f"evaluator {name}: " + ("ok" if not f else "FAILED " + "; ".join(f)))
        failures += f
    jobs = L.load_jobs(local=True)
    for jid, j in sorted(jobs.items()):
        p = L.validate_job(j)
        if j.get("kind") == "search" and not p:
            try:
                importlib.import_module("evaluators." + j["evaluator"]["module"])
            except Exception as e:
                p.append(f"evaluator does not import: {e}")
        for d in j.get("after") or []:
            if d not in jobs:
                p.append(f"after: {d} is not a job")
        if p:
            failures.append(f"job {jid}: " + "; ".join(p))
    print(f"jobs: {len(jobs)} " + ("ok" if not any(f.startswith("job ") for f in failures) else "FAILED"))
    if not quick:
        e2e = end_to_end()
        print("end to end: " + ("ok (two machines, one remote, the board merged them, the checkout untouched)" if not e2e else "FAILED"))
        failures += e2e
    print("\ncompute self-test: " + ("ok" if not failures else "FAILED\n  " + "\n  ".join(failures)))
    return 1 if failures else 0
