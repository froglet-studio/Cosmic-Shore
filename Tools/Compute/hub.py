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

Run it behind a TLS proxy (Caddy on the VM: Tools/Compute/vm/setup-vm.sh); it listens on 127.0.0.1 by default. The
team key lives in ~/.froglet-compute/hub.json ({"key": ...}); the page asks for it once and keeps it in the browser.
Standard library only.
"""
import hmac
import json
import os
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import compute_lib as L
import console as C
import manual_runs as R

MAX_BODY = 256 * 1024


class Hub:
    def __init__(self, key, queue=None, rebuild=60, url=None, github_token=None, quiet=False):
        self.key, self.queue, self.rebuild, self.url, self.quiet = key, queue, rebuild, url, quiet
        self.github_token = github_token
        self.lock = threading.Lock()
        self.data, self.jobs, self.live, self.filed = None, {}, {}, []
        self.requests = {}                  # every request, whole, by id (git plus what this hub filed since)
        self.prs, self.prs_at = [], 0
        self.posts = {}

    # ── the snapshot ──
    def build_once(self):
        prs_file = None
        if self.github_token and time.time() - self.prs_at > 300:
            self.prs = self._pull_requests()
            self.prs_at = time.time()
        if self.prs:
            prs_file = os.path.join(L.cache_dir(), "hub-prs.json")
            L.write_json(prs_file, self.prs)
        data = C.build(fetch=True, prs=prs_file, url=self.url, queue=self.queue)
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
        while True:
            try:
                self.build_once()
            except Exception as e:
                L.log(f"hub: rebuild failed: {e}", self.quiet)
            time.sleep(self.rebuild)

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
            if self.filed:
                d = dict(d, manual=list(self.filed) + list(d.get("manual") or []))
            return d

    # ── posts ──
    def allowed(self, given):
        return bool(given) and hmac.compare_digest(str(given).encode(), self.key.encode())

    def heartbeat(self, body):
        user, host = L.slug(body.get("user")), L.slug(body.get("host"))
        with self.lock:
            self.live[f"{user}/{host}"] = {"hb": body, "at": L.now()}
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

        def _send(self, code, body, ctype="application/json"):
            raw = body if isinstance(body, bytes) else json.dumps(body, default=float).encode()
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Content-Length", str(len(raw)))
            self.end_headers()
            self.wfile.write(raw)

        def do_GET(self):
            path = self.path.split("?", 1)[0]
            if path in ("/", "/index.html"):
                return self._send(200, page(), "text/html; charset=utf-8")
            if path == "/console.json":
                d = hub.snapshot()
                return self._send(200, d) if d else self._send(503, {"error": "the first build is still running"})
            if path in ("/api/requests",) or path.startswith("/api/requests/"):
                if not hub.allowed(self.headers.get("X-Compute-Key")):
                    return self._send(401, {"error": "the team key is missing or wrong"})
                if path == "/api/requests":
                    return self._send(200, {"requests": hub.open_requests()})
                r = hub.requests.get(path.rsplit("/", 1)[1])
                return self._send(200, r) if r else self._send(404, {"error": "no such run"})
            if path == "/api/status":
                d = hub.snapshot() or {}
                return self._send(200, {"hub": True, "built": d.get("generated"), "live": (d.get("live") or {}).get("machines", [])})
            return self._send(404, {"error": "not found"})

        def do_POST(self):
            path = self.path.split("?", 1)[0]
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


def run(args):
    key = load_key(args.key_file)
    token = None
    if args.github_token_file and os.path.exists(args.github_token_file):
        with open(args.github_token_file, encoding="utf-8") as fh:
            token = fh.read().strip() or None
    hub = Hub(key, queue=args.queue, rebuild=args.rebuild, url=args.url, github_token=token, quiet=args.quiet)

    def page():
        with open(C.PAGE, encoding="utf-8") as fh:
            return (C.SKELETON + fh.read() + "</body></html>\n").encode()
    threading.Thread(target=hub.loop, daemon=True).start()
    srv = ThreadingHTTPServer((args.host, args.port), handler(hub, page))
    L.log(f"hub on http://{args.host}:{args.port} (team key in {args.key_file or os.path.join(L.cache_dir(), 'hub.json')})", args.quiet)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0
