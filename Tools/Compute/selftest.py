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
    8. Unity runs (skipped by --quick): the unity-train job against a fake Unity (unity_end_to_end)
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


def hub_end_to_end(tmp, work, remote, script, env0):
    """The hub: a wrong key is refused, a run outside its search is refused, a good one is filed on compute-queue, and
    a worker that reports to the hub shows up live in /console.json."""
    import socket
    import time as _t
    import urllib.error
    import urllib.request
    import manual_runs as R
    bad = []
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    home = os.path.join(tmp, "cache-hub")
    envh = dict(env0, FROGLET_COMPUTE_HOME=home, FROGLET_COMPUTE_USER="selftest", FROGLET_COMPUTE_HOST="hub")
    hub = subprocess.Popen([sys.executable, script, "hub", "--port", str(port), "--rebuild", "2", "--quiet",
                            "--queue", "bleeding-edge", "compute-queue"], cwd=work, env=envh,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    base = f"http://127.0.0.1:{port}"

    def call(path, body=None, key=None):
        r = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None,
                                   method="POST" if body is not None else "GET",
                                   headers={"Content-Type": "application/json", **({"X-Compute-Key": key} if key else {})})
        try:
            with urllib.request.urlopen(r, timeout=30) as resp:
                return resp.status, json.loads(resp.read().decode() or "{}")
        except urllib.error.HTTPError as e:
            return e.code, json.loads(e.read().decode() or "{}")
    try:
        for _ in range(120):
            try:
                if call("/api/status")[1].get("built"):
                    break
            except Exception:
                pass
            _t.sleep(0.5)
        key = (L.read_json(os.path.join(home, "hub.json"), {}) or {}).get("key")
        good = R.build("toy-smoke", 20, fix=["x3=0.0"], note="from a phone")
        if call("/api/requests", good, "wrong")[0] != 401:
            bad.append("hub: a wrong team key was accepted")
        if call("/api/requests", R.build("toy-smoke", 20, fix=["x0=99"]), key)[0] != 400:
            bad.append("hub: a run outside its search's bounds was accepted")
        code, ans = call("/api/requests", {"token": R.token(good)}, key)
        if code != 201 or subprocess.run(["git", "cat-file", "-e", f"refs/heads/compute-queue:Docs/Compute/requests/{good['id']}.json"],
                                         cwd=remote).returncode:
            bad.append(f"hub: a good run was not filed on compute-queue ({code} {ans})")
        envw = dict(env0, FROGLET_COMPUTE_HOME=os.path.join(tmp, "cache-live"), FROGLET_COMPUTE_USER="selftest",
                    FROGLET_COMPUTE_HOST="live", FROGLET_COMPUTE_HUB=base, FROGLET_COMPUTE_HUB_KEY=key or "")
        subprocess.run([sys.executable, script, "run", "--no-update", "--always", "--once", "--no-push", "--cores", "2",
                        "--max-evals", "16", "--queue", "bleeding-edge", "compute-queue"], cwd=work, env=envw,
                       capture_output=True, text=True, timeout=600)
        _t.sleep(1.5)
        d = call("/console.json")[1]
        if "selftest/live" not in (d.get("live") or {}).get("machines", []):
            bad.append(f"hub: a worker reporting to the hub did not show up live: {d.get('live')}")
    finally:
        hub.terminate()
        hub.wait(timeout=10)
    return bad


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
        # Manual runs: a request filed on compute-queue is taken before the automatic queue, its result is recorded,
        # and `request list` shows it done; a token from the console runs at once on this computer.
        envr = dict(env0, FROGLET_COMPUTE_HOME=os.path.join(tmp, "cache-req"), FROGLET_COMPUTE_USER="selftest",
                    FROGLET_COMPUTE_HOST="box3")
        p = subprocess.run([sys.executable, script, "request", "new", "toy-smoke", "--tries", "24", "--fix", "x0=0.5",
                            "--range", "x1=0:3", "--note", "self-test", "--push"], cwd=work, env=envr,
                           capture_output=True, text=True, timeout=300)
        rid = (p.stdout.split("request ", 1)[1].split(":", 1)[0] if "request r" in p.stdout else None)
        if p.returncode or not rid:
            bad.append(f"manual runs: request new --push failed:\n{p.stdout[-800:]}{p.stderr[-800:]}")
        else:
            subprocess.run([sys.executable, script, "run", "--no-update", "--always", "--once", "--cores", "2",
                            "--max-evals", "40", "--queue", "bleeding-edge", "compute-queue"], cwd=work, env=envr,
                           capture_output=True, text=True, timeout=600)
            got = _git("show", f"refs/heads/compute/selftest/box3:requests/{rid}.json", cwd=remote) if \
                subprocess.run(["git", "cat-file", "-e", f"refs/heads/compute/selftest/box3:requests/{rid}.json"],
                               cwd=remote).returncode == 0 else ""
            res = json.loads(got) if got else {}
            if int(res.get("tries", 0)) < 24:
                bad.append(f"manual runs: the worker did not take the request before the automatic queue: {res or 'no result'}")
            p = subprocess.run([sys.executable, script, "request", "list"], cwd=work, env=envr, capture_output=True,
                               text=True, timeout=300)
            if rid not in p.stdout or " done " not in p.stdout:
                bad.append(f"manual runs: request list does not show it done:\n{p.stdout[-600:]}")
        import manual_runs as R
        tok = R.token(R.build("toy-smoke", 16, fix=["x2=1.0"], note="now"))
        p = subprocess.run([sys.executable, script, "run", "--no-push", "--request", tok, "--cores", "2",
                            "--queue", "bleeding-edge"], cwd=work, env=envr, capture_output=True, text=True, timeout=600)
        if p.returncode or "manual run" not in p.stdout:
            bad.append(f"manual runs: a token did not run at once:\n{p.stdout[-800:]}{p.stderr[-400:]}")
        bad += hub_end_to_end(tmp, work, remote, script, env0)
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
    # The installed app's own ref (config.json "ref"): its command jobs run on that computer, no other branch's do.
    old_env = os.environ.get("FROGLET_COMPUTE_APP_REF")
    os.environ["FROGLET_COMPUTE_APP_REF"] = "claude/compute-branch"
    try:
        for job, runs, what in [
            (dict(command, ref=L.AI_REF, _ref="origin/claude/compute-branch"), True, "a command job from this computer's install ref"),
            (dict(command, ref=L.AI_REF, _ref="origin/claude/other-branch"), False, "a command job from another branch"),
            (dict(command, ref="origin/claude/compute-branch", _ref="origin/claude/compute-branch"), False,
             "a command job from the install ref whose CODE ref is a personal branch"),
        ]:
            if (L.untrusted(job) is None) != runs:
                bad.append(f"trust: {what} should {'run' if runs else 'be refused'} ({L.untrusted(job)})")
    finally:
        if old_env is None:
            os.environ.pop("FROGLET_COMPUTE_APP_REF", None)
        else:
            os.environ["FROGLET_COMPUTE_APP_REF"] = old_env
    return bad


FAKE_UNITY = r"""
import json, os, sys, time
a = sys.argv[1:]
def arg(n, d=None):
    return a[a.index(n) + 1] if n in a else d
out, proj, log = arg("-frogletTrain"), arg("-projectPath"), arg("-logFile")
mode = os.environ.get("FAKE_UNITY_MODE", "ok")
with open(log, "a") as fh:
    fh.write("fake unity " + " ".join(a) + "\n")
    if mode == "compile":
        fh.write("Assets/X.cs(1,1): error CS0103: The name 'x' does not exist\n")
        fh.flush()
        time.sleep(120)
        sys.exit(1)
inj = os.path.join(proj, "Assets", "Editor", "FrogletCompute", "FrogletComputeTraining.cs")
if not os.path.exists(inj) or "-executeMethod" not in a:
    open(os.path.join(out, "result.json"), "w").write(json.dumps({"schema": "froglet-compute/unity-train/1", "ok": False, "reason": "not injected"}))
    sys.exit(1)
state = os.path.join(proj, "Assets", "_SO_Assets", "AI Training", "SessionState.asset")
k = int(open(state).read().split(":")[1]) if os.path.exists(state) else 0
k0, target, reason = k, int(arg("-trainEpisodes")), "finished: the target episodes are done"
def status(phase):
    json.dump({"phase": phase, "episodes_total": k, "unit_episodes": target, "episodes": k - k0, "generation": k // 3,
               "best_fitness": 10.0 + k, "loss": -(10.0 + k)}, open(os.path.join(out, "status.json"), "w"))
while k < target:
    if os.path.exists(os.path.join(out, "stop")):
        reason = "stopped by Froglet Compute"
        break
    time.sleep(1.0 if mode == "slow" else 0.1)
    k += 1
    open(state, "w").write("EpisodesCompleted: %d" % k)
    status("training")
json.dump({"schema": "froglet-compute/unity-train/1", "ok": True, "reason": reason, "episodes": k - k0,
           "episodes_total": k, "best_fitness": 10.0 + k}, open(os.path.join(out, "result.json"), "w"))
json.dump({"genes": [k]}, open(os.path.join(out, "genome.json"), "w"))
"""


def unity_end_to_end():
    """The unity-train job against a FAKE Unity (a script that follows FrogletComputeTraining.cs's contract): the worker
    runs a unit from the queue into Froglet Compute's own project copy and publishes it; the wrapper resumes a saved
    session, stops cleanly on Ctrl-C, stops on a compile error, waits while the person's Unity is open, and says when
    Unity is missing. Skipped on Windows (the fake is a shell script)."""
    if os.name == "nt":
        return []
    import signal
    import time
    bad = []
    tmp = tempfile.mkdtemp(prefix="froglet-compute-unity-")
    try:
        remote, work = os.path.join(tmp, "remote.git"), os.path.join(tmp, "work")
        _git("init", "--bare", "-q", remote, cwd=tmp)
        _git("init", "-q", "-b", "bleeding-edge", work, cwd=tmp)
        shutil.copytree(HERE, os.path.join(work, "Tools", "Compute"), ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
        os.makedirs(os.path.join(work, "ProjectSettings"))
        open(os.path.join(work, "ProjectSettings", "ProjectVersion.txt"), "w").write("m_EditorVersion: 6000.3.17f1\n")
        st = os.path.join(work, "Assets", "_SO_Assets", "AI Training")
        os.makedirs(st)
        open(os.path.join(st, "SessionState.asset"), "w").write("EpisodesCompleted: 0")
        os.makedirs(os.path.join(work, "Docs", "Compute", "jobs"))
        job = json.load(open(os.path.join(REPO, "Docs", "Compute", "jobs", "unity-train.json")))
        job.update(status="ready", ref="origin/bleeding-edge")
        job["command"]["units"] = [{"id": "skimrace-squirrel-i4", "args": {"mode": "HexRace", "vessel": "Squirrel", "intensity": 4, "episodes": 5}}]
        json.dump(job, open(os.path.join(work, "Docs", "Compute", "jobs", "unity-train.json"), "w"))
        open(os.path.join(work, ".gitignore"), "w").write("Temp/\n")
        env0 = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@t", GIT_COMMITTER_NAME="t", GIT_COMMITTER_EMAIL="t@t")
        _git("add", "-A", cwd=work, env=env0)
        _git("commit", "-qm", "init", cwd=work, env=env0)
        _git("remote", "add", "origin", remote, cwd=work)
        _git("push", "-q", "origin", "bleeding-edge", cwd=work)
        _git("fetch", "-q", "origin", cwd=work)
        fake_py = os.path.join(tmp, "fake_unity.py")
        open(fake_py, "w").write(FAKE_UNITY)
        fake = os.path.join(tmp, "Unity")
        open(fake, "w").write(f"#!/bin/sh\nexec {sys.executable} {fake_py} \"$@\"\n")
        os.chmod(fake, 0o755)
        home = os.path.join(tmp, "home")
        env = dict(env0, FROGLET_COMPUTE_HOME=home, FROGLET_COMPUTE_USER="selftest", FROGLET_COMPUTE_HOST="unitybox",
                   FROGLET_COMPUTE_UNITY=fake, FROGLET_COMPUTE_UNITY_POLL="0.5")
        script = os.path.join(work, "Tools", "Compute", "compute.py")
        # 1. the worker takes the unit from the queue and finishes it
        p = subprocess.run([sys.executable, script, "run", "--no-update", "--always", "--once", "--job", "unity-train",
                            "--queue", "bleeding-edge"], cwd=work, env=env, capture_output=True, text=True, timeout=300)
        if p.returncode != 0:
            return [f"unity: worker exited {p.returncode}:\n{p.stdout[-1500:]}{p.stderr[-1500:]}"]
        ref = "refs/heads/compute/selftest/unitybox"
        listing = _git("ls-tree", "-r", "--name-only", ref, cwd=remote)
        unit = [x for x in listing.split() if x.endswith("unit-skimrace-squirrel-i4.json")]
        if not unit:
            bad.append("unity: no unit result was published")
        else:
            u = json.loads(_git("show", f"{ref}:{unit[0]}", cwd=remote))
            if not (u.get("done") and u.get("progress") == 1.0 and u.get("score") == -15.0):
                bad.append(f"unity: the unit should be done at 5/5 with loss -15 ({u})")
            for f in ("genome.json", "SessionState.asset", "status.json", "result.json"):
                if f not in (u.get("files") or []):
                    bad.append(f"unity: {f} was not kept")
        proj = os.path.join(home, "unity", "Cosmic-Shore")
        if not os.path.exists(os.path.join(proj, "Assets", "Editor", "FrogletCompute", "FrogletComputeTraining.cs")):
            bad.append("unity: the batch entry was not copied into the project copy")
        if _git("status", "--porcelain", cwd=work).strip():
            bad.append("unity: the person's checkout was changed")
        if os.path.exists(os.path.join(work, "Assets", "Editor")):
            bad.append("unity: the batch entry leaked into the person's checkout")
        sha = _git("rev-parse", "HEAD", cwd=work).strip()
        runner = os.path.join(work, "Tools", "Compute", "unity_run.py")

        def wrap(out, extra_env=None, episodes=5, wait=True):
            argv = [sys.executable, runner, "--sha", sha, "--out", out, "--mode", "HexRace", "--vessel", "Squirrel",
                    "--episodes", str(episodes), "--minutes", "5", "--quiet"]
            e = dict(env, **(extra_env or {}))
            if wait:
                return subprocess.run(argv, cwd=work, env=e, capture_output=True, text=True, timeout=120)
            return subprocess.Popen(argv, cwd=work, env=e, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        # 2. resume: a saved session at 3 of 5 plays the last 2
        out = os.path.join(tmp, "resume")
        os.makedirs(out)
        open(os.path.join(out, "SessionState.asset"), "w").write("EpisodesCompleted: 3")
        p = wrap(out)
        r = L.read_json(os.path.join(out, "result.json"), {}) or {}
        if p.returncode != 0 or r.get("episodes") != 2 or r.get("episodes_total") != 5:
            bad.append(f"unity: resume should play 2 more matches to 5 ({p.returncode}, {r})")
        # 3. Ctrl-C stops Unity cleanly and keeps the session
        out = os.path.join(tmp, "stop")
        proc = wrap(out, {"FAKE_UNITY_MODE": "slow"}, episodes=100, wait=False)
        t0 = time.time()
        while time.time() - t0 < 30 and ((L.read_json(os.path.join(out, "status.json"), {}) or {}).get("episodes_total") or 0) < 2:
            time.sleep(0.3)
        proc.send_signal(signal.SIGINT)
        try:
            rc = proc.wait(timeout=30)
        except subprocess.TimeoutExpired:
            proc.kill()
            rc = "hung"
        r = L.read_json(os.path.join(out, "result.json"), {}) or {}
        if rc != 0 or "stopped" not in str(r.get("reason")) or not os.path.exists(os.path.join(out, "SessionState.asset")):
            bad.append(f"unity: Ctrl-C should stop cleanly and keep the session ({rc}, {r})")
        # 4. a project that does not compile stops at once
        out = os.path.join(tmp, "compile")
        t0 = time.time()
        p = wrap(out, {"FAKE_UNITY_MODE": "compile"})
        if p.returncode != 6 or time.time() - t0 > 60:
            bad.append(f"unity: a compile error should stop the run with exit 6 ({p.returncode}, {time.time() - t0:.0f} s)")
        # 5. the person's own Unity open: an automatic run waits, a manual one goes ahead
        os.makedirs(os.path.join(work, "Temp"), exist_ok=True)
        open(os.path.join(work, "Temp", "UnityLockfile"), "w").close()
        p = wrap(os.path.join(tmp, "locked"))
        if p.returncode != 75:
            bad.append(f"unity: with the person's Unity open an automatic run should wait (exit 75, got {p.returncode})")
        p = wrap(os.path.join(tmp, "locked-manual"), {"FROGLET_COMPUTE_MANUAL": "1"})
        if p.returncode != 0:
            bad.append(f"unity: a manual run should go ahead while the person's Unity is open ({p.returncode})")
        shutil.rmtree(os.path.join(work, "Temp"))
        # 6. no Unity: says so (negative control for the capability)
        p = wrap(os.path.join(tmp, "nounity"), {"FROGLET_COMPUTE_UNITY": os.path.join(tmp, "nope")})
        if p.returncode != 4:
            bad.append(f"unity: a missing Unity should exit 4 ({p.returncode})")
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
    import manual_runs
    mr = manual_runs.self_test()
    print("manual runs: " + ("ok (refuses everything outside its search, narrows and fixes, tokens round-trip)" if not mr else "FAILED"))
    failures += mr
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
        ue = unity_end_to_end()
        print("unity runs: " + ("ok (a fake Unity: queued unit done and published, resume, Ctrl-C, compile error, your Unity open, no Unity)"
                                if not ue else "FAILED"))
        failures += ue
    print("\ncompute self-test: " + ("ok" if not failures else "FAILED\n  " + "\n  ".join(failures)))
    return 1 if failures else 0
