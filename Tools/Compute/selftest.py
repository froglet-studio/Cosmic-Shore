"""`compute.py self-test`: every piece, then the whole pipe end to end against a throwaway git remote.

    1. the searchers (optimizers.self_test: they search, they resume, a negative control fails)
    2. the course port's parity with the C# harness (course_port.self_test)
    3. every evaluator's own self-test
    4. every job file validates and names an evaluator that imports
    5. end to end (skipped by --quick): a scratch repo + bare remote; two "machines" run the toy job; the remote must
       hold both results branches, the second machine must start from the first one's best, the board must merge
       them, and the scratch repo's own checkout must be untouched (no dirty file, not made sparse, no worktree
       added, no extension written to its config). The second machine runs through the self-updating supervisor, so the
       export of Tools/Compute from bleeding-edge is exercised too.
    6. the trust rules (compute_lib.untrusted): what a queue ref may make a worker execute, with negative controls
    7. the ai branch sync (ai_branch.self_test): AI changes come over from Ys-bleeding-edge, non-AI changes do not
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
            update = ["--no-update"] if host == "box1" else ["--home", "origin/bleeding-edge"]
            p = subprocess.run([sys.executable, script, "run", *update, "--always", "--once", "--job", "toy-smoke",
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
        if subprocess.run(["git", "config", "--get", "extensions.worktreeConfig"], cwd=work, capture_output=True, text=True).stdout.strip():
            bad.append("end to end: the worker wrote extensions.worktreeConfig into the person's repository config")
        if _git("worktree", "list", "--porcelain", cwd=work).count("worktree ") != 1:
            bad.append("end to end: the worker added a worktree to the person's repository:\n" + _git("worktree", "list", cwd=work))
        home = os.path.join(tmp, "cache-box2", "home")
        if not (os.path.isdir(home) and any(os.path.isfile(os.path.join(home, d, "Tools", "Compute", "compute.py"))
                                            for d in os.listdir(home))):
            bad.append("end to end: the supervisor did not run box2 from an export of bleeding-edge's Tools/Compute")
        if not os.path.isfile(os.path.join(work, "Docs", "Compute", "jobs", "toy-smoke.json")):
            bad.append("end to end: the person's checkout lost files")
        # A second session on the same machine gets its own --max-evals budget (it used to count the machine's total
        # and stop at once), and the runs log the console shows records it.
        env = dict(env0, FROGLET_COMPUTE_HOME=os.path.join(tmp, "cache-box1"), FROGLET_COMPUTE_USER="selftest",
                   FROGLET_COMPUTE_HOST="box1")
        subprocess.run([sys.executable, script, "run", "--no-update", "--always", "--once", "--job", "toy-smoke",
                        "--max-evals", "40", "--cores", "2", "--queue", "bleeding-edge"], cwd=work, env=env,
                       capture_output=True, text=True, timeout=600)
        hb = L.read_json(os.path.join(tmp, "cache-box1", "results", "selftest", "box1", "heartbeat.json"), {}) or {}
        runs = hb.get("runs") or []
        if len(runs) < 2 or int(runs[-1].get("evals") or 0) < 40:
            bad.append(f"end to end: a second session did not get its own budget or was not logged: {runs[-1:] or 'no runs log'}")
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


def schedule_window():
    """--between: inside, outside, and a window that wraps past midnight; a bad spec is refused."""
    import time as _t
    bad = []
    at = lambda h, m: _t.struct_time((2026, 10, 28, h, m, 0, 2, 301, -1))
    night, day = L.parse_window("22:00-07:00"), L.parse_window("09:30-17:00")
    for window, h, m, want in ((night, 23, 0, True), (night, 3, 0, True), (night, 12, 0, False), (night, 7, 0, False),
                               (day, 9, 30, True), (day, 16, 59, True), (day, 17, 0, False), (day, 8, 0, False)):
        if L.in_window(window, at(h, m)) != want:
            bad.append(f"schedule: {h:02d}:{m:02d} in {window} should be {want}")
    if not L.in_window(None):
        bad.append("schedule: no window should mean always")
    try:
        L.parse_window("10pm-7am")
        bad.append("schedule: a malformed --between was accepted")
    except ValueError:
        pass
    return bad


def windows_task():
    """The Task Scheduler line fits in 261 characters with a Microsoft Store Python and a long user name, and the
    launcher is valid Python that runs the worker without --quiet (its log is the only output)."""
    import install
    bad = []
    py = "C:\\Users\\Alexandra.Konstantinopoulou\\AppData\\Local\\Microsoft\\WindowsApps\\PythonSoftwareFoundation.Python.3.11_qbz5n2kfra8p0\\pythonw.exe"
    home = "C:\\Users\\Alexandra.Konstantinopoulou\\.froglet-compute"
    cmd = install._cmd(10.0, "22:00-07:00", False, "origin/claude/eager-franklin-e6g77h", "C:\\Users\\Alexandra.Konstantinopoulou\\Documents\\GitHub\\Cosmic-Shore",
                       ["bleeding-edge", "ai", "compute-queue", "claude/eager-franklin-e6g77h"])
    launcher, text, tr = install.windows_task(cmd, home, py)
    old = " ".join(f'"{c}"' for c in cmd)
    if len(tr) > 261:
        bad.append(f"windows: the task line is {len(tr)} characters (Task Scheduler takes 261)")
    if len(old) <= 261:
        bad.append("windows: the negative control (the full command on the task line) should be over 261")
    try:
        compile(text, launcher, "exec")
    except SyntaxError as e:
        bad.append(f"windows: the launcher is not valid Python: {e}")
    if "--quiet" in text or "--between" not in text:
        bad.append("windows: the launcher's argv is wrong")
    return bad


def trust_rules():
    """What a queue ref may make a worker execute. Each case is (job, why it must or must not run)."""
    bad = []
    search = {"kind": "search", "ref": L.AI_REF}
    command = {"kind": "command"}
    cases = [
        (dict(search, _ref="origin/compute-queue"), True, "a search job from compute-queue on a code ref"),
        (dict(search, _ref="origin/bleeding-edge", ref="origin/cece/some-branch"), False, "a job whose code ref is a personal branch"),
        (dict(search, _ref="origin/compute-queue", ref="origin/compute/x/y"), False, "a job whose code ref is a results branch"),
        (dict(command, _ref="origin/bleeding-edge"), True, "a command job from bleeding-edge"),
        (dict(command, _ref="origin/compute-queue"), False, "a command job from compute-queue"),
        (dict(command, _ref=L.AI_REF), False, "a command job from the ai branch"),
        (dict(search, _ref="origin/bleeding-edge", ref="origin/Ys-bleeding-edge"), False, "a job whose code is a person's own branch"),
        (dict(command, ref="origin/cece/some-branch"), True, "a job from the person's own working tree (--local)"),
    ]
    for job, runs, what in cases:
        if (L.untrusted(job) is None) != runs:
            bad.append(f"trust: {what} should {'run' if runs else 'be refused'} ({L.untrusted(job)})")
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
    win = windows_task()
    print("windows task line: " + ("ok" if not win else "FAILED"))
    failures += win
    trust = trust_rules()
    print("trust rules: " + ("ok" if not trust else "FAILED"))
    failures += trust
    import console as _console
    con = _console.self_test()
    print("console: " + ("ok (approvals: core four only, same epoch, same best)" if not con else "FAILED"))
    failures += con
    sched = schedule_window()
    print("schedule window: " + ("ok" if not sched else "FAILED"))
    failures += sched
    if not quick:
        import contextlib
        import io
        import ai_branch
        with contextlib.redirect_stdout(io.StringIO()):
            ai = ai_branch.self_test()
        print("ai branch sync: " + ("ok (AI changes come over, others do not, conflicts stop it)" if not ai else "FAILED"))
        failures += ai
    if not quick:
        e2e = end_to_end()
        print("end to end: " + ("ok (two machines, one remote, the board merged them, the checkout untouched)" if not e2e else "FAILED"))
        failures += e2e
    print("\ncompute self-test: " + ("ok" if not failures else "FAILED\n  " + "\n  ".join(failures)))
    return 1 if failures else 0
