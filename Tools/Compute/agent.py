"""The agent: what keeps a registered computer on the Compute Console between sessions (README section 17).

The supervisor loop (`compute.py run`, started at login on every OS) uses this to:
- say how the computer is, live: working, free (its person is away), in use, outside its hours, offline (no word);
- describe it: cores, memory, free disk, OS, Unity found, Unity open, whether its owner allows a run to start now;
- find the runs the console sent to it: start now (only if its owner allowed remote starts), at a set time, or when
  it is free; and notice when a run it is working on is stopped from the console.

Status goes to the hub every minute when there is one (live), and to git when it changes, at most every ten minutes,
and every half hour regardless, so the console shows the fleet with or without the hub. Standard library only.
"""
import json
import os
import platform
import shutil
import sys
import time
import urllib.request

import compute_lib as L

STATUSES = ("working", "free", "in use", "outside hours", "offline")
LIVE_EVERY, GIT_MIN, GIT_MAX = 60, 600, 1800


# ── what this computer is ────────────────────────────────────────────────────

def memory_gb():
    """(total, free) in GB, or (None, None)."""
    try:
        system = platform.system()
        if system == "Windows":
            import ctypes

            class MS(ctypes.Structure):
                _fields_ = [("len", ctypes.c_ulong), ("load", ctypes.c_ulong), ("total", ctypes.c_ulonglong),
                            ("avail", ctypes.c_ulonglong), ("tp", ctypes.c_ulonglong), ("ap", ctypes.c_ulonglong),
                            ("tv", ctypes.c_ulonglong), ("av", ctypes.c_ulonglong), ("ae", ctypes.c_ulonglong)]
            m = MS()
            m.len = ctypes.sizeof(MS)
            ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
            return round(m.total / 2 ** 30, 1), round(m.avail / 2 ** 30, 1)
        if system == "Linux":
            info = {}
            with open("/proc/meminfo") as fh:
                for line in fh:
                    k, _, v = line.partition(":")
                    info[k] = int(v.split()[0]) * 1024
            return round(info["MemTotal"] / 2 ** 30, 1), round(info.get("MemAvailable", 0) / 2 ** 30, 1)
        if system == "Darwin":
            import subprocess
            total = int(subprocess.run(["sysctl", "-n", "hw.memsize"], capture_output=True, text=True).stdout)
            return round(total / 2 ** 30, 1), None
    except Exception:
        pass
    return None, None


def specs():
    total, free = memory_gb()
    try:
        disk = round(shutil.disk_usage(L.cache_dir()).free / 2 ** 30, 1)
    except Exception:
        disk = None
    caps = L.capabilities()
    try:
        import unity_run
        unity_open = unity_run.person_unity_open()
    except Exception:
        unity_open = None
    return {"os": platform.system().lower(), "os_name": f"{platform.system()} {platform.release()}",
            "cores": os.cpu_count(), "ram_gb": total, "ram_free_gb": free, "disk_free_gb": disk,
            "unity": bool(caps.get("unity")), "unity_open": unity_open, "python": platform.python_version()}


def config():
    return L.read_json(os.path.join(L.cache_dir(), "config.json"), {}) or {}


def remote_start_allowed():
    """Whether this computer's owner lets a run started from the console begin at once, while they use it."""
    return bool(config().get("remote_start"))


def set_remote_start(on):
    path = os.path.join(L.cache_dir(), "config.json")
    cfg = config()
    cfg["remote_start"] = bool(on)
    L.write_json(path, cfg)
    return cfg["remote_start"]


def status_of(presence):
    """The computer's state when no session runs (a running session reports `working` itself)."""
    if not L.in_window(presence.window):
        return "outside hours"
    return "free" if presence.away() else "in use"


# ── telling the console ──────────────────────────────────────────────────────

class Beacon:
    """Sends the computer's state: to the hub every minute, to git on a change (at most every ten minutes) and every
    half hour. A running session's own heartbeats take over while it works."""

    def __init__(self, ident, quiet=True, push=True):
        self.ident, self.quiet, self.push = ident, quiet, push
        self.last_live, self.last_git, self.last_status = 0.0, 0.0, None

    def heartbeat(self, status, idle=None):
        user, host = self.ident
        folder = L.results_folder(user, host)
        path = os.path.join(folder, "heartbeat.json")
        hb = L.read_json(path, {}) or {}
        hb.update({"user": user, "host": host, "status": status, "idle_seconds": idle, "updated": L.now(),
                   "specs": specs(), "caps": L.capabilities(), "remote_start": remote_start_allowed(),
                   "agent": True})
        if status != "working":
            hb["current"] = {}
        return folder, path, hb

    def starting(self, req, idle=None):
        """The agent took a run the console sent: say so at once, naming it, so the console shows it as starting
        before the session's first report (a Unity run opens Unity for a minute or more first)."""
        folder, path, hb = self.heartbeat("working", idle)
        hb["current"] = {"job": req.get("job"), "request": req.get("id"), "since": L.now(), "phase": "starting",
                         "unit": _unit_of(req)}
        L.post_heartbeat(hb)
        L.write_json(path, hb)
        self.last_live = time.time()
        self.last_status = "working"
        return hb

    def tick(self, status, idle=None, force=False):
        now = time.time()
        changed = status != self.last_status
        to_live = force or changed or now - self.last_live >= LIVE_EVERY
        to_git = force or (changed and now - self.last_git >= GIT_MIN) or now - self.last_git >= GIT_MAX
        if not (to_live or to_git):
            return None
        folder, path, hb = self.heartbeat(status, idle)
        if to_live:
            L.post_heartbeat(hb)
            self.last_live = now
        if to_git:
            L.write_json(path, hb)
            if self.push:
                L.publish(folder, *self.ident, f"compute: {self.ident[1]} is {status}", quiet=True)
            self.last_git = now
        self.last_status = status
        return hb


def _unit_of(req):
    """The unit name a Unity run will work on (mode-vessel-iN), from its request; None for a search."""
    args = (req or {}).get("args") or {}
    if not (args.get("mode") and args.get("vessel")):
        return None
    lv = args.get("intensity") or 4
    lv = lv[0] if isinstance(lv, list) and lv else lv          # several levels: the first unit starts first
    return f"{str(args['mode']).lower()}-{str(args['vessel']).lower()}-i{int(lv)}"


# ── runs the console sent ────────────────────────────────────────────────────

def _hub_get(path):
    url, key = L.hub_config()
    if not url:
        return None
    try:
        req = urllib.request.Request(url + path, headers={"X-Compute-Key": key})
        with urllib.request.urlopen(req, timeout=10) as r:
            return json.loads(r.read().decode())
    except Exception:
        return None


def open_requests(queue, fetch=True):
    """Every request on the queue that is not done or cancelled, as {id: request}: from the hub when there is one
    (instant), else from git (compute-queue fetched)."""
    import manual_runs as R
    got = _hub_get("/api/requests")
    if isinstance(got, dict) and isinstance(got.get("requests"), list):
        return {r["id"]: r for r in got["requests"] if isinstance(r, dict) and r.get("id")}
    if fetch:
        L.fetch([q.replace("origin/", "") for q in queue], results=False, quiet=True)
    return R.load([q if q.startswith("origin/") else "origin/" + q for q in queue])


def due_now(requests, me, machines, now=None):
    """The requests this computer should start at once: aimed at it (or any computer), due (`not_before` passed), still
    open, and either `start: now` with its owner's consent or nothing else to wait for. The oldest first."""
    import manual_runs as R
    now = now or L.now()
    allowed = remote_start_allowed()
    out = []
    for r in sorted(requests.values(), key=lambda r: r.get("created", 0)):
        if r.get("cancelled") or r.get("here") or r.get("machine", "any") not in ("any", me):
            continue
        if int(r.get("not_before") or 0) > now or r.get("start") != "now" or not allowed:
            continue
        if R.progress(r, machines, now)["state"] != "open":
            continue
        out.append(r)
    return out


def cancelled(req_id, queue):
    """Whether a request was stopped from the console since it started (hub, else git)."""
    import manual_runs as R
    got = _hub_get("/api/requests/" + req_id)
    if isinstance(got, dict) and "cancelled" in got:
        return bool(got["cancelled"])
    L.fetch([q.replace("origin/", "") for q in queue if "compute-queue" in q] or ["compute-queue"], results=False, quiet=True)
    r = R.load([q if q.startswith("origin/") else "origin/" + q for q in queue]).get(req_id)
    return bool(r and r.get("cancelled"))


class StopWhenCancelled:
    """Wraps a session's presence: it also says "back" when the request it works on was stopped from the console
    (checked every `every` seconds), so search and command sessions stop as they do for a returning person."""

    def __init__(self, presence, req_id, queue, every=None):
        every = every or int(os.environ.get("FROGLET_COMPUTE_STOP_CHECK") or 30)
        self.presence, self.req_id, self.queue, self.every = presence, req_id, queue, every
        self.window = getattr(presence, "window", None)
        self._at, self._stopped = 0.0, False
        self.reason = None

    def away(self):
        return self.presence.away()

    def waiting_for(self):
        return self.presence.waiting_for()

    def back(self):
        if self.presence.back():
            self.reason = "the person came back"
            return True
        if not self._stopped and time.time() - self._at >= self.every:
            self._at = time.time()
            self._stopped = cancelled(self.req_id, self.queue)
        if self._stopped:
            self.reason = "stopped from the console"
        return self._stopped


# ── one agent per computer ───────────────────────────────────────────────────

_LOCK = None


def single_instance():
    """True when this is the computer's only agent (a lock file held while it runs); False when another holds it."""
    global _LOCK
    path = os.path.join(L.cache_dir(), "agent.lock")
    fh = open(path, "a+")
    try:
        if os.name == "nt":
            import msvcrt
            fh.seek(0)
            msvcrt.locking(fh.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(fh.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        fh.close()
        return False
    fh.seek(0)
    fh.truncate()
    fh.write(str(os.getpid()))
    fh.flush()
    _LOCK = fh
    return True


def machine_status(hb, now=None, live=False):
    """What the console shows for a computer from its last heartbeat: working, free, in use, outside hours, offline.
    A heartbeat older than 3 minutes (live) or 45 minutes (git) means offline; one from before the agent existed
    says working or idle from its `current`."""
    now = now or L.now()
    age = now - int((hb or {}).get("updated") or 0)
    if not hb or age > (180 if live else 2700):
        return "offline"
    if (hb.get("current") or {}).get("job"):
        return "working"
    s = hb.get("status")
    # "working" with no run to show: the agent took a run and the session has not reported yet (a Unity run opens
    # Unity first), or a session ended and the next has not started. Never "working" with nothing running.
    if s == "working":
        return "starting"
    return s if s in STATUSES else "free"


def self_test():
    bad = []
    hb = {"updated": 1000, "status": "in use"}
    for now, live, want in ((1050, True, "in use"), (1300, True, "offline"), (1300, False, "in use"),
                            (5000, False, "offline")):
        if machine_status(hb, now, live) != want:
            bad.append(f"agent: status at +{now - 1000}s ({'live' if live else 'git'}) should be {want}")
    if machine_status(dict(hb, current={"job": "x"}), 1010, True) != "working":
        bad.append("agent: a heartbeat with a job is working")
    if machine_status(dict(hb, status="working", current={}), 1010, True) != "starting":
        bad.append("agent: 'working' with no run to show is starting, never working")
    reqs = {"a": {"id": "a", "created": 1, "machine": "u/h", "start": "now", "budget": {"tries": 5}},
            "b": {"id": "b", "created": 2, "machine": "u/h", "start": "now", "not_before": 9e9, "budget": {"tries": 5}},
            "c": {"id": "c", "created": 3, "machine": "u/other", "start": "now", "budget": {"tries": 5}},
            "d": {"id": "d", "created": 4, "machine": "any", "budget": {"tries": 5}},
            "e": {"id": "e", "created": 5, "machine": "any", "start": "now", "cancelled": True, "budget": {"tries": 5}}}
    old = os.environ.get("FROGLET_COMPUTE_HOME")
    import tempfile
    tmp = tempfile.mkdtemp(prefix="agent-selftest-")
    os.environ["FROGLET_COMPUTE_HOME"] = tmp
    try:
        if due_now(reqs, "u/h", []):
            bad.append("agent: a run started now without its owner's consent")
        set_remote_start(True)
        got = [r["id"] for r in due_now(reqs, "u/h", [], now=100)]
        if got != ["a"]:
            bad.append(f"agent: with consent only the due, open run aimed here starts now ({got})")
        if not single_instance():
            bad.append("agent: the first agent could not take the lock")
    finally:
        if old is None:
            os.environ.pop("FROGLET_COMPUTE_HOME", None)
        else:
            os.environ["FROGLET_COMPUTE_HOME"] = old
    return bad


if __name__ == "__main__":
    b = self_test()
    print("agent self-test: " + ("ok" if not b else "FAILED\n  " + "\n  ".join(b)))
    sys.exit(1 if b else 0)
