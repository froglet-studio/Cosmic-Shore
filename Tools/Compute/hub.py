"""The hub: the Compute Console served live from one always-on computer (the Oracle VM) - `compute.py hub`.

A claude.ai page can only read files published with it, so the claude.ai console is a snapshot. The hub is the live
one: the same page, its data rebuilt from git every --rebuild seconds, and every computer's heartbeat laid on top the
moment it arrives (workers post one every ~20 s while they work), so a phone sees progress in seconds. It also takes
manual runs from the page and files them on compute-queue, so a run can be started from a phone.

    GET  /                 the console page
    GET  /console.json     the console's data (git snapshot + live heartbeats)
    GET  /api/status       {"hub": true, "built", "live": [machines heard from in the last 10 minutes]}
    POST /api/heartbeat    a worker's heartbeat (header X-Compute-Key)
    POST /api/requests     a manual run: the request JSON or {"token": ...} (header X-Compute-Key); checked with
                           manual_runs.problems() against the job, then filed on compute-queue
    GET  /api/requests     the runs not finished or stopped, whole (header X-Compute-Key): the agents read it every
                           minute to start a run sent to them at once
    GET  /api/requests/ID  one run, whole (X-Compute-Key): a computer working on it checks whether it was stopped
    POST /api/cancel       {"id": ...} (X-Compute-Key): stop a run; filed on compute-queue, the computer stops in ~30 s
    POST /api/run-here     the app only (`compute.py app`): {"token", "watch"} (X-Compute-Key) starts the run on THIS
                           computer at once, as the pasted `compute.py run --request` line did

`compute.py app` is the same hub on a person's own computer, as their Froglet Compute application (2026-10-29: "it should
be a PC application"): it listens on 127.0.0.1 only, answers only requests addressed to that name, opens the console in
the browser with the key already in the page, and starts a run on this computer at once instead of a line to paste.

Run it behind a TLS proxy (Caddy on the VM: Tools/Compute/vm/setup-vm.sh); it listens on 127.0.0.1 by default. The
team key lives in ~/.froglet-compute/hub.json ({"key": ...}); the page asks for it once and keeps it in the browser.
Standard library only.
"""
import contextlib
import hmac
import io
import json
import os
import re
import subprocess
import sys
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import compute_lib as L
import console as C
import manual_runs as R

MAX_BODY = 256 * 1024
HERE = os.path.dirname(os.path.abspath(__file__))


def installer(name, url, key, flags):
    """(text, content type, file name) of the double-click installer for `name` (windows | mac | linux), or (None, ..)."""
    import setup
    ref = setup.APP_REF if L.git_ok("cat-file", "-e", "origin/bleeding-edge:Tools/Compute/setup.py") is None else "bleeding-edge"
    opts = " ".join([f"--hub {url}", f"--hub-key {key}", *flags])
    if name == "setup.py":
        with open(os.path.join(HERE, "setup.py"), encoding="utf-8") as fh:
            return fh.read(), "text/x-python", "froglet-compute-setup.py"
    if name == "windows.bat":
        # The window does the rest: it finds the Cosmic-Shore folder (or Browse), asks the schedule, installs, starts the tray.
        text = "\r\n".join([
            "@echo off", "setlocal",
            "where python >nul 2>&1 || (echo Python is not installed. Install Python 3 from python.org (tick \"Add to PATH\"), then run this again. & pause & exit /b 1)",
            "where git >nul 2>&1 || (echo Git is not installed. Install Git for Windows, then run this again. & pause & exit /b 1)",
            'set "SETUP=%TEMP%\\froglet-compute-setup.py"',
            f'curl -fsSL "{url}/install/setup.py?key={key}" -o "%SETUP%" || (echo Could not reach the console at {url}. & pause & exit /b 1)',
            'for /f "delims=" %%p in (\'where python\') do (set "PY=%%p" & goto :found)',
            ":found",
            'set "PYW=%PY:python.exe=pythonw.exe%"',
            f'if exist "%PYW%" (start "" "%PYW%" "%SETUP%" --gui {opts}) else (start "" "%PY%" "%SETUP%" --gui {opts})',
            "exit /b 0", ""])
        return text, "application/x-bat", "froglet-compute-setup.bat"
    if name == "windows-plain.bat":
        text = "\r\n".join([
            "@echo off", "setlocal",
            "echo Froglet Compute: putting this PC on the team console.",
            "where git >nul 2>&1 || (echo Git is not installed. Install Git for Windows, then run this again. & pause & exit /b 1)",
            "where python >nul 2>&1 || (echo Python is not installed. Install Python 3 from python.org, then run this again. & pause & exit /b 1)",
            'set "REPO=%~dp0"',
            'if not exist "%REPO%.git" set /p "REPO=Path to your Cosmic-Shore folder (the one you open in Unity): "',
            'cd /d "%REPO%" || (echo That folder does not exist. & pause & exit /b 1)',
            'if not exist ".git" (echo That is not the Cosmic-Shore folder. & pause & exit /b 1)',
            f"git fetch origin {ref} || (echo Could not fetch from GitHub. & pause & exit /b 1)",
            f"git show origin/{ref}:Tools/Compute/setup.py | python - {opts}",
            "echo.", f"echo Done. This PC shows on {url} within a minute.", "pause", ""])
        return text, "application/x-bat", "froglet-compute-setup.bat"
    if name in ("mac.sh", "linux.sh"):
        text = "\n".join([
            "#!/bin/bash", "set -e",
            "command -v git >/dev/null || { echo 'git is not installed.'; exit 1; }",
            "command -v python3 >/dev/null || { echo 'python3 is not installed.'; exit 1; }",
            'SETUP="${TMPDIR:-/tmp}/froglet-compute-setup.py"',
            f'curl -fsSL "{url}/install/setup.py?key={key}" -o "$SETUP"',
            f'python3 "$SETUP" --gui {opts}', ""])
        return text, "text/x-shellscript", "froglet-compute-setup.sh"
    return None, None, None
AI_SYNC_EVERY = 3600                 # seconds between the VM's ai-branch syncs
CODE_CHECK_EVERY = 600               # seconds between the VM's looks for newer code on its branch


class Hub:
    def __init__(self, key, queue=None, rebuild=60, url=None, github_token=None, quiet=False, app=None):
        self.key, self.queue, self.rebuild, self.url, self.quiet = key, queue, rebuild, url, quiet
        self.app = app                      # {"port"}: the person's own app (compute.py app); None on the VM
        self.here = {}                      # runs this app started on this computer: id -> {"proc", "log", "started"}
        self.github_token = github_token
        self.lock = threading.Lock()
        self.data, self.jobs, self.live, self.filed = None, {}, {}, []
        self.requests = {}                  # every request, whole, by id (git plus what this hub filed since)
        self.prs, self.prs_at = [], 0
        self.error, self.started = None, time.time()      # the last build's failure, shown by the page
        self.posts = {}
        self.ai_sync, self.ai_sync_at = None, 0.0        # the hourly ai-branch sync (the VM only): its last result
        self.updates = {}                                # Update AI: vessel -> {state, by, note, at} (archive_update.py)
        self.code_check_at = time.time()                 # the VM's self-restart on newer code (restart_if_code_moved)
        self.leaving = set()                             # computers the console removed: their next heartbeat is told to leave
        self.forgets = {}                                # Remove / Delete: machine or request id -> {state, note, at}

    # ── the snapshot ──
    def build_once(self, fetch=True):
        prs_file = None
        if self.github_token and time.time() - self.prs_at > 300:
            self.prs = self._pull_requests()
            self.prs_at = time.time()
        if self.prs:
            prs_file = os.path.join(L.cache_dir(), "hub-prs.json")
            L.write_json(prs_file, self.prs)
        data = C.build(fetch=fetch, prs=prs_file, url=self.url, queue=self.queue)
        data["hub"] = True
        refs = [q if q.startswith("origin/") else "origin/" + q for q in (self.queue or L.QUEUE_REFS)]
        try:
            reqs = R.load(refs)
        except Exception:
            reqs = {}
        with self.lock:
            self.data, self.jobs = data, dict(C.LAST_JOBS)
            for rid, r in reqs.items():
                mine = self.requests.get(rid)
                if not (mine and mine.get("cancelled") and not r.get("cancelled")):     # a stop not yet in git stays
                    self.requests[rid] = {k: v for k, v in r.items() if not k.startswith("_")}
            self.filed = [f for f in self.filed if f["id"] not in {m["id"] for m in data.get("manual") or []}]

    def loop(self):
        # The app's first build reads the branches already on this computer (seconds, no network) so the page opens at
        # once; the full fetch of every team branch follows straight after (a minute or more the first time).
        quick = bool(self.app)
        while True:
            try:
                self.build_once(fetch=not quick)
                self.error = None
            except Exception as e:
                self.error = f"{type(e).__name__}: {e}"
                L.log(f"hub: rebuild failed: {e}", self.quiet)
            if quick:
                quick = False
                continue
            if self.syncs_ai() and time.time() - self.ai_sync_at > AI_SYNC_EVERY:
                self.sync_ai()
            if not self.app and time.time() - self.code_check_at > CODE_CHECK_EVERY:
                self.code_check_at = time.time()
                self.restart_if_code_moved()
            time.sleep(self.rebuild)

    def restart_if_code_moved(self):
        """The VM: when the checkout's upstream branch has newer Tools/Compute, pull it and exit; systemd restarts the
        hub on the new code within seconds (Restart=always). Nobody types a restart (Shombith, 2026-11-01)."""
        try:
            up = (L.git_ok("rev-parse", "--abbrev-ref", "@{u}") or "").strip()
            if not up.startswith("origin/"):
                return
            ref = up[len("origin/"):]
            L.git_ok("fetch", "--quiet", "--no-tags", "origin", f"+refs/heads/{ref}:refs/remotes/origin/{ref}")
            mine = (L.git_ok("rev-parse", "HEAD:Tools/Compute") or "").strip()
            theirs = (L.git_ok("rev-parse", f"origin/{ref}:Tools/Compute") or "").strip()
            if not mine or not theirs or mine == theirs:
                return
            L.git_ok("pull", "--ff-only", "--quiet")
            if (L.git_ok("rev-parse", "HEAD:Tools/Compute") or "").strip() != theirs:
                return
            L.log(f"hub: origin/{ref} has newer Froglet Compute: pulled it, restarting", self.quiet)
            os._exit(0)
        except Exception as e:
            L.log(f"hub: could not look for newer code ({e})", self.quiet)

    # ── the ai branch: the VM keeps it current, so nobody runs a sync by hand (README section 15) ──
    def syncs_ai(self):
        return bool(self.github_token) and not self.app

    def sync_ai(self):
        """bleeding-edge into ai and Ys-bleeding-edge's AI paths replayed, pushed with the VM's token (the clone's
        remote carries it). A failure is shown on the page and tried again next hour; it never stops the hub."""
        self.ai_sync_at = time.time()
        buf = io.StringIO()
        try:
            import ai_branch
            with contextlib.redirect_stdout(buf):
                rc = ai_branch.sync(push=True)
            self.ai_sync = {"at": L.now(), "ok": rc == 0, "note": buf.getvalue().strip()[-600:]}
        except Exception as e:
            self.ai_sync = {"at": L.now(), "ok": False, "error": f"{type(e).__name__}: {e}", "note": buf.getvalue().strip()[-600:]}
        L.log(f"hub: ai sync {'ok' if self.ai_sync['ok'] else 'failed'}: {self.ai_sync.get('error') or self.ai_sync['note'].splitlines()[-1:] or ''}", self.quiet)

    def _pull_requests(self):
        try:
            req = urllib.request.Request("https://api.github.com/repos/froglet-studio/Cosmic-Shore/pulls?state=open&per_page=30",
                                         headers={"Authorization": f"Bearer {self.github_token}",
                                                  "Accept": "application/vnd.github+json"})
            with urllib.request.urlopen(req, timeout=20) as r:
                return json.loads(r.read().decode())
        except Exception as e:
            L.log(f"hub: pull requests: {e}", self.quiet)
            return self.prs

    def snapshot(self):
        with self.lock:
            if self.data is None:
                return None
            d = C.overlay(self.data, self.live, self.jobs)
            if self.syncs_ai():
                d = dict(d, ai=dict(d.get("ai") or {}, hub_sync=self.ai_sync or {"due": True}))
            if self.updates:
                d = dict(d, ai_updates=dict(self.updates))
            if self.forgets:
                d = dict(d, forgets=dict(self.forgets))
            if self.filed:
                d = dict(d, manual=list(self.filed) + list(d.get("manual") or []))
            return d

    # ── posts ──
    def allowed(self, given):
        return bool(given) and hmac.compare_digest(str(given).encode(), self.key.encode())

    def heartbeat(self, body):
        user, host = L.slug(body.get("user")), L.slug(body.get("host"))
        me = f"{user}/{host}"
        with self.lock:
            if me in self.leaving:
                self.leaving.discard(me)
                self.live.pop(me, None)
                return 200, {"ok": True, "leave": True}
            self.live[me] = {"hb": body, "at": L.now()}
        return 200, {"ok": True}

    def request(self, body):
        req = R.from_token(body["token"]) if body.get("token") else body
        with self.lock:
            job = self.jobs.get((req or {}).get("job"))
        bad = R.problems(req, job) or ([L.untrusted(job)] if job and L.untrusted(job) else [])
        if bad:
            return 400, {"error": "this run cannot be made", "problems": bad}
        sha = R.file(req, quiet=True)
        if not sha:
            return 502, {"error": "could not file it on compute-queue (git push failed)"}
        with self.lock:
            self.requests[req["id"]] = {k: v for k, v in req.items() if not k.startswith("_")}
        st = {"state": "open", "tries": 0, "done_tries": 0, "budget": req["budget"]["tries"], "machines": [],
              "running_on": None, "updated": L.now()}
        entry = {"id": req["id"], "job": req["job"], "title": (job or {}).get("title"), "by": req.get("by"),
                 "created": req.get("created"), "note": req.get("note"), "machine": req.get("machine", "any"),
                 "fix": req.get("fix") or {}, "ranges": req.get("ranges") or {}, "args": req.get("args") or {},
                 "problems": [], "today": None, "best": None, **st}
        with self.lock:
            self.filed.insert(0, entry)
        L.log(f"hub: filed {req['id']} ({req['job']}) by {req.get('by')}", self.quiet)
        return 201, {"id": req["id"], "sha": sha}

    def open_requests(self):
        """The runs not stopped and not finished, whole (what an agent needs to start one)."""
        manual = {m["id"]: m for m in (self.snapshot() or {}).get("manual") or []}      # snapshot() takes the lock
        with self.lock:
            items = list(self.requests.items())
        return [r for rid, r in items
                if not r.get("cancelled") and (manual.get(rid) or {}).get("state", "open") in ("open", "running")]

    def cancel(self, body):
        rid = str(body.get("id", ""))
        with self.lock:
            known = self.requests.get(rid)
        if not R.ID.fullmatch(rid) or not known:
            return 404, {"error": f"no run {rid}"}
        req = dict(known, cancelled=True, cancelled_at=L.now())
        if not R.file(req, quiet=True):
            return 502, {"error": "could not file the stop on compute-queue (git push failed)"}
        with self.lock:
            self.requests[rid] = req
            for f in self.filed:
                if f["id"] == rid:
                    f["state"] = "cancelled"
        L.log(f"hub: stopped {rid}", self.quiet)
        return 200, {"id": rid, "cancelled": True}

    def forget(self, body):
        """Remove a computer: its agent is told to uninstall at its next heartbeat, its results branch is deleted, the
        console forgets it. Or delete a run's data (`id`): the run is cancelled and its folders leave every results
        branch. Both run in the background; /api/status `forgets` says how it went."""
        import forget as F
        machine, rid = str(body.get("machine") or ""), str(body.get("id") or "")
        key = machine or rid
        if not key:
            return 400, {"error": "no machine and no run"}
        with self.lock:
            if (self.forgets.get(key) or {}).get("state") == "running":
                return 409, {"error": f"{key} is being removed already"}
            self.forgets[key] = {"state": "running", "at": L.now(), "note": ""}
            if machine:
                self.leaving.add(machine)
                self.live.pop(machine, None)
        def work():
            try:
                if machine:
                    r = F.forget_machine(machine, push=True)
                else:
                    known = self.requests.get(rid)
                    if known and not known.get("cancelled"):
                        R.file(dict(known, cancelled=True, cancelled_at=L.now()), quiet=True)
                    r = F.forget_run(rid, push=True)
            except Exception as e:
                r = {"ok": False, "note": f"{type(e).__name__}: {e}"}
            with self.lock:
                self.forgets[key] = {"state": "done" if r.get("ok") else "failed", "at": L.now(), "note": r.get("note", "")}
            L.log(f"hub: forget {key}: {r.get('note')}", self.quiet)
            try:
                self.build_once(fetch=True)
            except Exception:
                pass
        threading.Thread(target=work, name=f"forget-{key}", daemon=True).start()
        return 202, {"key": key, "state": "running"}

    def update_ai(self, body):
        """Update AI: the vessel's trained genomes onto the ai branch, in the background; one at a time per vessel."""
        import archive_update as AU
        vessel, by = str(body.get("vessel") or ""), str(body.get("by") or "").strip()
        if by not in AU.approvers():
            return 403, {"error": f"the approver must be one of the core four: {', '.join(AU.approvers())}"}
        if not vessel:
            return 400, {"error": "no vessel"}
        with self.lock:
            cur = self.updates.get(vessel)
            if cur and cur.get("state") == "running":
                return 409, {"error": f"{vessel} is being updated already"}
            self.updates[vessel] = {"state": "running", "by": by, "at": L.now(), "note": "writing the archive on the ai branch"}
        def work():
            try:
                r = AU.update(vessel, by, push=True)
            except Exception as e:
                r = {"ok": False, "note": f"{type(e).__name__}: {e}"}
            with self.lock:
                self.updates[vessel] = {"state": "done" if r.get("ok") else "failed", "by": by, "at": L.now(),
                                        "note": r.get("note", ""), "sha": r.get("sha"), "entries": r.get("entries")}
            L.log(f"hub: update AI {vessel} by {by}: {r.get('note')}", self.quiet)
            if r.get("ok"):
                try:
                    self.build_once(fetch=True)      # the page shows the archive as it now is
                except Exception:
                    pass
        threading.Thread(target=work, name=f"update-ai-{vessel}", daemon=True).start()
        return 202, {"vessel": vessel, "state": "running"}

    def run_here(self, body):
        """The app only: start a run on this computer now, as `compute.py run --request <token>` (the line the page
        used to give to paste). It reports to this app live (FROGLET_COMPUTE_HUB), so Runs shows it by the second."""
        if not self.app:
            return 404, {"error": "not found"}
        req = R.from_token(body["token"]) if body.get("token") else body.get("request")
        with self.lock:
            job = self.jobs.get((req or {}).get("job"))
        bad = R.problems(req, job) or ([L.untrusted(job)] if job and L.untrusted(job) else [])
        if bad:
            return 400, {"error": "this run cannot be made", "problems": bad}
        with self.lock:
            busy = [rid for rid, r in self.here.items() if r["proc"].poll() is None]
        if busy:
            return 409, {"error": f"a run started here is still going ({busy[0]}): stop it first"}
        folder = os.path.join(L.cache_dir(), "app-runs")
        os.makedirs(folder, exist_ok=True)
        log = os.path.join(folder, f"{req['id']}.log")
        argv = [sys.executable, os.path.join(HERE, "compute.py"), "run", "--request", R.token(req)]
        if body.get("watch"):
            argv.append("--watch")
        env = dict(os.environ, FROGLET_COMPUTE_HUB=f"http://127.0.0.1:{self.app['port']}", FROGLET_COMPUTE_HUB_KEY=self.key)
        extra = {"creationflags": L.NO_WINDOW.get("creationflags", 0) | 0x00000200} if os.name == "nt" \
            else {"start_new_session": True}                                  # CREATE_NEW_PROCESS_GROUP
        with open(log, "ab") as out:
            proc = subprocess.Popen(argv, cwd=L.REPO, env=env, stdout=out, stderr=subprocess.STDOUT,
                                    stdin=subprocess.DEVNULL, **extra)
        with self.lock:
            self.here[req["id"]] = {"proc": proc, "log": log, "started": L.now()}
            self.requests[req["id"]] = {k: v for k, v in req.items() if not k.startswith("_")}
        L.log(f"app: started {req['id']} ({req['job']}) on this computer, log {log}", self.quiet)
        return 201, {"id": req["id"], "pid": proc.pid, "log": log}

    def here_runs(self):
        with self.lock:
            return [{"id": rid, "running": r["proc"].poll() is None, "exit": r["proc"].poll(), "log": r["log"],
                     "started": r["started"]} for rid, r in self.here.items()]

    def rate_ok(self, ip, limit=60):
        t = time.time()
        with self.lock:
            hits = [x for x in self.posts.get(ip, []) if t - x < 3600]
            hits.append(t)
            self.posts[ip] = hits
            return len(hits) <= limit


def handler(hub, page):
    class Handler(BaseHTTPRequestHandler):
        server_version = "froglet-compute-hub"

        def log_message(self, fmt, *a):
            pass

        def _send_installer(self, name):
            """The one-file installer a person double-clicks (README section 17): the hub's address and the team key
            are written into it, so it is served only to a request that carries the key (the page keeps it)."""
            from urllib.parse import parse_qs, urlparse
            q = parse_qs(urlparse(self.path).query)
            if not hub.allowed((q.get("key") or [""])[0]):
                return self._send(401, {"error": "the team key is missing or wrong"})
            flags = [f for f in ("--nights", "--always", "--only-sent") if (q.get(f.strip("-").replace("-", "_")) or [""])[0] == "1"
                     or (q.get(f.strip("-")) or [""])[0] == "1"]
            between = (q.get("between") or [""])[0]
            if re.fullmatch(r"\d{1,2}:\d{2}-\d{1,2}:\d{2}", between):
                flags += ["--between", between]
            if (q.get("remote") or ["1"])[0] == "1":
                flags.append("--allow-remote-start")
            text, ctype, fname = installer(name, hub.url or f"http://{self.headers.get('Host', '')}", hub.key, flags)
            if text is None:
                return self._send(404, {"error": "no such installer"})
            raw = text.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Disposition", f'attachment; filename="{fname}"')
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(raw)))
            self.end_headers()
            self.wfile.write(raw)

        def _send(self, code, body, ctype="application/json"):
            raw = body if isinstance(body, bytes) else json.dumps(body, default=float).encode()
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Content-Length", str(len(raw)))
            self.end_headers()
            self.wfile.write(raw)

        def _local(self):
            """The app answers only requests addressed to this computer by name: a web page elsewhere cannot reach it
            through a name that resolves here (DNS rebinding), and it never listens beyond 127.0.0.1."""
            if not hub.app:
                return True
            host = (self.headers.get("Host") or "").rsplit(":", 1)[0]
            return host in ("127.0.0.1", "localhost")

        def do_GET(self):
            path = self.path.split("?", 1)[0]
            if not self._local():
                return self._send(403, {"error": "this app answers its own computer only"})
            if path in ("/", "/index.html"):
                return self._send(200, page(), "text/html; charset=utf-8")
            if path.startswith("/install/"):
                return self._send_installer(path[9:])
            if path.startswith("/stills/") and re.fullmatch(r"[a-z0-9-]+\.jpg", path[8:]):
                still = os.path.join(os.path.dirname(C.PAGE), "stills", path[8:])
                if os.path.isfile(still):
                    with open(still, "rb") as fh:
                        return self._send(200, fh.read(), "image/jpeg")
                return self._send(404, {"error": "no such still"})
            if path == "/console.json":
                d = hub.snapshot()
                return self._send(200, d) if d else self._send(503, {
                    "error": hub.error or "the first build is still running", "failed": bool(hub.error),
                    "seconds": int(time.time() - hub.started)})
            if path in ("/api/requests",) or path.startswith("/api/requests/"):
                if not hub.allowed(self.headers.get("X-Compute-Key")):
                    return self._send(401, {"error": "the team key is missing or wrong"})
                if path == "/api/requests":
                    return self._send(200, {"requests": hub.open_requests()})
                r = hub.requests.get(path.rsplit("/", 1)[1])
                return self._send(200, r) if r else self._send(404, {"error": "no such run"})
            if path == "/api/status":
                d = hub.snapshot() or {}
                return self._send(200, {"hub": True, "app": bool(hub.app), "built": d.get("generated"), "error": hub.error,
                                        "ai_synced": hub.ai_sync if hub.syncs_ai() else None,
                                        "updates": hub.updates, "forgets": hub.forgets,
                                        "live": (d.get("live") or {}).get("machines", []),
                                        "here": hub.here_runs() if hub.app else []})
            return self._send(404, {"error": "not found"})

        def do_POST(self):
            path = self.path.split("?", 1)[0]
            if not self._local():
                return self._send(403, {"error": "this app answers its own computer only"})
            ip = self.headers.get("X-Forwarded-For", self.client_address[0]).split(",")[0].strip()
            if not hub.allowed(self.headers.get("X-Compute-Key")):
                return self._send(401, {"error": "the team key is missing or wrong"})
            n = int(self.headers.get("Content-Length") or 0)
            if n <= 0 or n > MAX_BODY:
                return self._send(413, {"error": "body too large or empty"})
            try:
                body = json.loads(self.rfile.read(n).decode())
            except Exception:
                return self._send(400, {"error": "not JSON"})
            if path == "/api/heartbeat":
                return self._send(*hub.heartbeat(body))
            if path == "/api/cancel":
                try:
                    return self._send(*hub.cancel(body))
                except Exception as e:
                    return self._send(400, {"error": f"not a stop: {e}"})
            if path in ("/api/forget", "/api/forget-run"):
                try:
                    return self._send(*hub.forget(body))
                except Exception as e:
                    return self._send(400, {"error": f"not removed: {e}"})
            if path == "/api/update-ai":
                try:
                    return self._send(*hub.update_ai(body))
                except Exception as e:
                    return self._send(400, {"error": f"not an update: {e}"})
            if path == "/api/run-here":
                try:
                    return self._send(*hub.run_here(body))
                except Exception as e:
                    return self._send(400, {"error": f"not a run: {e}"})
            if path == "/api/requests":
                if not hub.rate_ok(ip):
                    return self._send(429, {"error": "too many runs from here this hour"})
                try:
                    return self._send(*hub.request(body))
                except Exception as e:
                    return self._send(400, {"error": f"not a request: {e}"})
            return self._send(404, {"error": "not found"})
    return Handler


def load_key(path=None):
    path = path or os.path.join(L.cache_dir(), "hub.json")
    cfg = L.read_json(path, {}) or {}
    if not cfg.get("key"):
        import secrets
        cfg["key"] = secrets.token_urlsafe(18)
        L.write_json(path, cfg)
        os.chmod(path, 0o600)
    return cfg["key"]


def run(args, app=False):
    key = load_key(args.key_file)
    token = None
    if getattr(args, "github_token_file", None) and os.path.exists(args.github_token_file):
        with open(args.github_token_file, encoding="utf-8") as fh:
            token = fh.read().strip() or None
    hub = Hub(key, queue=args.queue, rebuild=args.rebuild, url=getattr(args, "url", None), github_token=token,
              quiet=args.quiet, app={"port": args.port} if app else None)
    me = "/".join(L.identity())
    # The app's page carries the key, so its buttons work with no prompt; the page is served to this computer only.
    boot = (f'<script>window.FC_APP={json.dumps({"key": key, "machine": me})};</script>' if app else "")

    def page():
        with open(C.PAGE, encoding="utf-8") as fh:
            return (C.SKELETON + boot + fh.read() + "</body></html>\n").encode()
    threading.Thread(target=hub.loop, daemon=True).start()
    host = "127.0.0.1" if app else args.host
    try:
        srv = ThreadingHTTPServer((host, args.port), handler(hub, page))
    except OSError as e:
        if app:
            print(f"Froglet Compute is already open at http://127.0.0.1:{args.port} ({e.strerror})")
            if not args.no_browser:
                import webbrowser
                webbrowser.open(f"http://127.0.0.1:{args.port}/")
            return 0
        raise
    where = f"http://{host}:{args.port}"
    if app:
        print(f"Froglet Compute is open at {where} (close this window to quit it)")
        if not args.no_browser:
            import webbrowser
            threading.Timer(1.0, lambda: webbrowser.open(where + "/")).start()
    else:
        L.log(f"hub on {where} (team key in {args.key_file or os.path.join(L.cache_dir(), 'hub.json')})", args.quiet)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0


def shortcut(port=8721):
    """A desktop launcher for the app: double-click to open Froglet Compute. Returns its path."""
    desk = os.path.join(os.path.expanduser("~"), "Desktop")
    os.makedirs(desk, exist_ok=True)
    py, script = sys.executable, os.path.join(HERE, "compute.py")
    if os.name == "nt":
        path = os.path.join(desk, "Froglet Compute.cmd")
        body = f'@echo off\r\ntitle Froglet Compute\r\ncd /d "{L.REPO}"\r\n"{py}" "{script}" app --port {port}\r\n'
    elif sys.platform == "darwin":
        path = os.path.join(desk, "Froglet Compute.command")
        body = f'#!/bin/sh\ncd "{L.REPO}" && exec "{py}" "{script}" app --port {port}\n'
    else:
        path = os.path.join(desk, "froglet-compute.desktop")
        body = (f"[Desktop Entry]\nType=Application\nName=Froglet Compute\nTerminal=true\nPath={L.REPO}\n"
                f'Exec="{py}" "{script}" app --port {port}\n')
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(body)
    if os.name != "nt":
        os.chmod(path, 0o755)
    return path
