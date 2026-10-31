"""Froglet Compute: the shared pieces (jobs, machines, git transport, the board's data). Stdlib only.

Docs/Compute/README.md is the design; this module is what every subcommand of compute.py stands on.

    jobs      Docs/Compute/jobs/<id>.json, read straight out of git (the queue refs), validated here
    machine   what this computer can run (python, numpy, torch, cuda, node, dotnet, cores) and whether its person
              is away (idle time, battery)
    results   a plain folder per machine, published as ONE commit on refs/heads/compute/<user>/<host> with git
              plumbing (no checkout, no touch of anyone's working tree)
    board     every machine's published results, merged per job and epoch
"""
import hashlib
import json
import math
import os
import platform
import re
import shutil
import socket
import subprocess
import tarfile
import tempfile
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
def _repo():
    """The person's repository. A worker updated from bleeding-edge runs from an export of Tools/Compute that is not a
    git checkout: the supervisor hands the repository down in FROGLET_COMPUTE_REPO, and the one-line setup records it
    in ~/.froglet-compute/config.json for commands typed by hand."""
    if os.environ.get("FROGLET_COMPUTE_REPO"):
        return os.environ["FROGLET_COMPUTE_REPO"]
    here = os.path.dirname(os.path.dirname(HERE))
    if os.path.exists(os.path.join(here, ".git")):
        return here
    home = os.environ.get("FROGLET_COMPUTE_HOME") or os.path.join(os.path.expanduser("~"), ".froglet-compute")
    try:
        with open(os.path.join(home, "config.json"), encoding="utf-8") as fh:
            return json.load(fh).get("repo") or here
    except Exception:
        return here


REPO = _repo()
JOBS_DIR = "Docs/Compute/jobs"
RESULTS_PREFIX = "compute"              # refs/heads/compute/<user>/<host>
# The AI's own branch: bleeding-edge plus AI work, owned by no person (Tools/Compute/ai_branch.py keeps it current).
AI_REF = "origin/ai"
QUEUE_REFS = ("origin/bleeding-edge", AI_REF, "origin/compute-queue")


def _configured_queue():
    """The queue the one-line setup recorded (config.json "queue": until Froglet Compute is merged, its own branch is
    on it), so a command typed by hand - a manual run pasted from the console - reads the same jobs the worker does."""
    home = os.environ.get("FROGLET_COMPUTE_HOME") or os.path.join(os.path.expanduser("~"), ".froglet-compute")
    try:
        with open(os.path.join(home, "config.json"), encoding="utf-8") as fh:
            extra = json.load(fh).get("queue") or []
    except Exception:
        extra = []
    refs = list(QUEUE_REFS) + [q if q.startswith("origin/") else "origin/" + q for q in extra if isinstance(q, str) and q]
    return tuple(dict.fromkeys(refs))


QUEUE_REFS = _configured_queue()
# What a worker EXECUTES, as opposed to what it reads. A job file may sit on any queue ref, but the code it runs comes
# only from refs the team shares: bleeding-edge (every change a reviewed pull request) and ai (bleeding-edge plus AI
# work). No person's own branch is one of them. compute-queue is pushed by the scout sweep without a pull request, so
# a job from it can only point a shipped evaluator at one of those refs; a command job (an argv run as the person)
# only comes from bleeding-edge.
CODE_REFS = ("origin/bleeding-edge", AI_REF)
COMMAND_REFS = ("origin/bleeding-edge",)
STATUSES = ("ready", "proposed", "blocked", "converged", "promoted", "retired", "paused")
RUNNABLE = ("ready",)
KINDS = ("search", "command")
ALGORITHMS = ("sep-cmaes", "map-elites", "random")
SCHEMA = "froglet-compute/job/1"


def cache_dir():
    d = os.environ.get("FROGLET_COMPUTE_HOME") or os.path.join(os.path.expanduser("~"), ".froglet-compute")
    os.makedirs(d, exist_ok=True)
    return d


def now():
    return int(time.time())


def slug(s):
    s = re.sub(r"[^a-z0-9]+", "-", (s or "").lower()).strip("-")
    return s or "anon"


def log(msg, quiet=False):
    if not quiet:
        print(time.strftime("%H:%M:%S ") + msg, flush=True)


# ── git ──────────────────────────────────────────────────────────────────────

# Windows: a console program started by the windowless worker (pythonw) opens a window of its own unless told not to.
# Every subprocess the worker starts passes this.
NO_WINDOW = {"creationflags": 0x08000000} if os.name == "nt" else {}     # CREATE_NO_WINDOW


def console_python():
    """python.exe for a probe that must print (pythonw.exe has no stdout); sys.executable elsewhere."""
    exe = sys.executable
    if os.name == "nt" and os.path.basename(exe).lower().startswith("pythonw"):
        p = os.path.join(os.path.dirname(exe), "python.exe")
        if os.path.exists(p):
            return p
    return exe


def git(*args, cwd=None, env=None, check=True, input=None, timeout=600):
    p = subprocess.run(["git", *args], cwd=cwd or REPO, env=env, input=input, capture_output=True, text=True,
                       timeout=timeout, **NO_WINDOW)
    if check and p.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)}: {p.stderr.strip() or p.stdout.strip()}")
    return p.stdout


def git_ok(*args, cwd=None):
    try:
        return git(*args, cwd=cwd, check=True)
    except Exception:
        return None


def rev(ref, cwd=None):
    out = git_ok("rev-parse", "--verify", "--quiet", ref + "^{commit}", cwd=cwd)
    return out.strip() if out else None


def fetch(refs=(), results=True, cwd=None, quiet=False):
    """Fetch the queue refs (branch names without origin/) and, optionally, every machine's results."""
    if refs:
        # A queue ref that does not exist yet (compute-queue before anyone pushed it) would fail the whole fetch.
        heads = git_ok("ls-remote", "--heads", "origin", *[f"refs/heads/{r}" for r in refs], cwd=cwd) or ""
        present = {line.split("refs/heads/", 1)[1] for line in heads.splitlines() if "refs/heads/" in line}
        refs = [r for r in refs if r in present]
    specs = [f"+refs/heads/{r}:refs/remotes/origin/{r}" for r in refs]
    if results:
        specs.append(f"+refs/heads/{RESULTS_PREFIX}/*:refs/remotes/origin/{RESULTS_PREFIX}/*")
    for attempt in range(4):
        try:
            git("fetch", "--quiet", "--no-tags", "origin", *specs, cwd=cwd, timeout=900)
            return True
        except Exception as e:
            if attempt == 3:
                log(f"fetch failed: {e}", quiet)
                return False
            time.sleep(2 ** (attempt + 1))


def identity(cwd=None):
    user = os.environ.get("FROGLET_COMPUTE_USER") or (git_ok("config", "user.email", cwd=cwd) or "").split("@")[0] \
        or (git_ok("config", "user.name", cwd=cwd) or "").strip() or os.environ.get("USER") or os.environ.get("USERNAME")
    host = os.environ.get("FROGLET_COMPUTE_HOST") or socket.gethostname().split(".")[0]
    return slug(user), slug(host)


def results_ref(user, host):
    return f"refs/heads/{RESULTS_PREFIX}/{user}/{host}"


def publish(folder, user, host, message, cwd=None, push=True, quiet=False):
    """Commit `folder` (the whole tree of this machine's results) as the next commit of compute/<user>/<host> and push
    it. Plumbing only: a private index, write-tree, commit-tree. The person's working tree and index are never read
    or written."""
    cwd = cwd or REPO
    gitdir = git("rev-parse", "--absolute-git-dir", cwd=cwd).strip()
    index = os.path.join(cache_dir(), f"index-{user}-{host}")
    env = dict(os.environ, GIT_INDEX_FILE=index, GIT_DIR=gitdir, GIT_WORK_TREE=folder)
    if os.path.exists(index):
        os.remove(index)
    git("add", "-A", ".", cwd=folder, env=env)
    tree = git("write-tree", cwd=folder, env=env).strip()
    parent = rev(f"origin/{RESULTS_PREFIX}/{user}/{host}", cwd=cwd)
    if parent and git("rev-parse", parent + "^{tree}", cwd=cwd).strip() == tree:
        return parent                                            # nothing new
    ident = dict(env, GIT_AUTHOR_NAME=f"froglet-compute ({user})", GIT_AUTHOR_EMAIL="compute@froglet.invalid",
                 GIT_COMMITTER_NAME=f"froglet-compute ({user})", GIT_COMMITTER_EMAIL="compute@froglet.invalid")
    args = ["commit-tree", tree, "-m", message] + (["-p", parent] if parent else [])
    sha = git(*args, cwd=folder, env=ident).strip()
    if push:
        for attempt in range(4):
            try:
                git("push", "--quiet", "origin", f"{sha}:{results_ref(user, host)}", cwd=cwd, timeout=600)
                git("update-ref", f"refs/remotes/origin/{RESULTS_PREFIX}/{user}/{host}", sha, cwd=cwd)
                break
            except Exception as e:
                if "non-fast-forward" in str(e) or "rejected" in str(e):
                    # The same machine name published from somewhere else: stack on top of it, ours wins.
                    fetch((), results=True, cwd=cwd, quiet=True)
                    parent = rev(f"origin/{RESULTS_PREFIX}/{user}/{host}", cwd=cwd)
                    sha = git("commit-tree", tree, "-m", message, *(["-p", parent] if parent else []),
                              cwd=folder, env=ident).strip()
                elif attempt == 3:
                    log(f"push failed: {e}", quiet)
                    return None
                time.sleep(2 ** (attempt + 1))
    return sha


def export_tree(sha, paths, parent, name, cwd=None, keep=3):
    """`paths` of commit `sha`, written into a private folder under `parent` straight from the object store
    (`git archive`). No worktree and no sparse-checkout, so nothing is added to the repository's config or worktree
    list. Reused while the commit and paths are the same; older exports of `name` beyond `keep` are deleted."""
    present = sorted({p for p in paths if git_ok("cat-file", "-e", f"{sha}:{p.rstrip('/')}", cwd=cwd) is not None})
    key = hashlib.sha1((sha + "\n" + "\n".join(present)).encode()).hexdigest()[:10]
    path = os.path.join(parent, f"{name}-{sha[:12]}-{key}")
    done = os.path.join(path, ".froglet-export")
    if not os.path.exists(done):
        shutil.rmtree(path, ignore_errors=True)
        tmp = path + ".part"
        shutil.rmtree(tmp, ignore_errors=True)
        os.makedirs(tmp)
        if present:
            # To a file, not through a pipe: tarfile stops at the end marker while git still writes its padding, and a
            # Windows pipe is too small to hold it - both would wait for ever (Shombith's install, 2026-10-28).
            tar_path = tmp + ".tar"
            p = subprocess.run(["git", "archive", "--format=tar", "-o", tar_path, sha, "--",
                                *[x.rstrip("/") for x in present]], cwd=cwd or REPO, capture_output=True, **NO_WINDOW)
            if p.returncode != 0:
                shutil.rmtree(tmp, ignore_errors=True)
                raise RuntimeError(f"git archive {sha[:9]}: {p.stderr.decode(errors='replace').strip()}")
            with tarfile.open(tar_path) as tar:
                if hasattr(tarfile, "data_filter"):
                    tar.extractall(tmp, filter="data")
                else:
                    tar.extractall(tmp)
            os.remove(tar_path)
        with open(os.path.join(tmp, ".froglet-export"), "w") as fh:
            fh.write(sha + "\n" + "\n".join(present) + "\n")
        os.replace(tmp, path)
    old = sorted((d for d in os.listdir(parent) if d.startswith(name + "-") and not d.endswith(".part")
                  and os.path.join(parent, d) != path), key=lambda d: os.path.getmtime(os.path.join(parent, d)))
    for d in old[:max(0, len(old) - (keep - 1))]:
        shutil.rmtree(os.path.join(parent, d), ignore_errors=True)     # a folder still in use stays; next time
    return path


def commit_files(branch, files, message, push=True, cwd=None, quiet=False):
    """Add or replace `files` ({path: text}) on origin/<branch> as one new commit, with git plumbing (a private index;
    the checkout is never touched), and push it. The branch is created as a root commit holding only these files when
    it does not exist yet. Returns the commit's sha, or None when the push failed."""
    cwd = cwd or REPO
    gitdir = git("rev-parse", "--absolute-git-dir", cwd=cwd).strip()
    tmp = tempfile.mkdtemp(prefix="compute-commit-")
    ident = dict(os.environ, GIT_AUTHOR_NAME=f"froglet-compute ({identity(cwd)[0]})", GIT_AUTHOR_EMAIL="compute@froglet.invalid",
                 GIT_COMMITTER_NAME=f"froglet-compute ({identity(cwd)[0]})", GIT_COMMITTER_EMAIL="compute@froglet.invalid")
    try:
        for attempt in range(4):
            fetch([branch], results=False, cwd=cwd, quiet=True)
            parent = rev(f"origin/{branch}", cwd=cwd)
            env = dict(ident, GIT_INDEX_FILE=os.path.join(tmp, "index"), GIT_DIR=gitdir)
            if os.path.exists(env["GIT_INDEX_FILE"]):
                os.remove(env["GIT_INDEX_FILE"])
            if parent:
                git("read-tree", parent, cwd=cwd, env=env)
            for path, text in files.items():
                blob = git("hash-object", "-w", "--stdin", cwd=cwd, env=env, input=text).strip()
                git("update-index", "--add", "--cacheinfo", f"100644,{blob},{path}", cwd=cwd, env=env)
            tree = git("write-tree", cwd=cwd, env=env).strip()
            sha = git("commit-tree", tree, "-m", message, *(["-p", parent] if parent else []), cwd=cwd, env=env).strip()
            if not push:
                return sha
            try:
                git("push", "--quiet", "origin", f"{sha}:refs/heads/{branch}", cwd=cwd, timeout=600)
                git("update-ref", f"refs/remotes/origin/{branch}", sha, cwd=cwd)
                return sha
            except Exception as e:
                if attempt == 3:
                    log(f"push to {branch} failed: {e}", quiet)
                    return None
                time.sleep(2 ** (attempt + 1))         # someone else pushed: rebuild on top of theirs
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def hub_config():
    """(url, key) of the hub this computer reports to live, or (None, None): FROGLET_COMPUTE_HUB / _HUB_KEY, else the
    hub and hub_key in ~/.froglet-compute/config.json (the setup writes them with --hub/--hub-key)."""
    url, key = os.environ.get("FROGLET_COMPUTE_HUB"), os.environ.get("FROGLET_COMPUTE_HUB_KEY")
    if not url:
        cfg = read_json(os.path.join(cache_dir(), "config.json"), {}) or {}
        url, key = cfg.get("hub"), cfg.get("hub_key")
    return (url.rstrip("/"), key) if url and key else (None, None)


def post_heartbeat(hb):
    """Send a heartbeat to the hub in the background: a phone sees the run's progress in seconds. Never blocks the
    worker and never fails it: a hub that is down only means the console falls back to git's slower updates."""
    url, key = hub_config()
    if not url:
        return

    def send():
        try:
            import urllib.request
            r = urllib.request.Request(url + "/api/heartbeat", data=json.dumps(hb, default=float).encode(), method="POST",
                                       headers={"Content-Type": "application/json", "X-Compute-Key": key})
            urllib.request.urlopen(r, timeout=5).read()
        except Exception:
            pass
    import threading
    threading.Thread(target=send, daemon=True).start()


# ── jobs ─────────────────────────────────────────────────────────────────────

def _job_problems(j):
    p = []
    for k in ("id", "title", "goal", "kind", "status", "value"):
        if k not in j:
            p.append(f"missing '{k}'")
    if j.get("schema") not in (None, SCHEMA):
        p.append(f"schema {j.get('schema')} (want {SCHEMA})")
    if j.get("kind") not in KINDS:
        p.append(f"kind {j.get('kind')} not in {KINDS}")
    if j.get("status") not in STATUSES:
        p.append(f"status {j.get('status')} not in {STATUSES}")
    if not isinstance(j.get("value", 0), (int, float)) or not 0 <= j.get("value", 0) <= 10:
        p.append("value must be a number 0-10")
    if j.get("id") and not re.fullmatch(r"[a-z0-9][a-z0-9-]{2,63}", j["id"]):
        p.append("id must be lower-kebab, 3-64 chars")
    if j.get("kind") == "search":
        ev = j.get("evaluator") or {}
        if not ev.get("module"):
            p.append("a search job names evaluator.module")
        algo = (j.get("algorithm") or {}).get("name", "sep-cmaes")
        if algo not in ALGORITHMS:
            p.append(f"algorithm {algo} not in {ALGORITHMS}")
        space = j.get("space") or []
        if not space:
            p.append("a search job needs a space (its parameters)")
        names = set()
        for d in space:
            if d.get("name") in names:
                p.append(f"parameter {d.get('name')} twice")
            names.add(d.get("name"))
            t = d.get("type", "float")
            if t in ("float", "int"):
                if not (isinstance(d.get("lo"), (int, float)) and isinstance(d.get("hi"), (int, float)) and d["lo"] < d["hi"]):
                    p.append(f"parameter {d.get('name')}: lo < hi required")
            elif t == "choice":
                if not d.get("choices"):
                    p.append(f"parameter {d.get('name')}: choices required")
            else:
                p.append(f"parameter {d.get('name')}: type {t}")
        if algo == "map-elites" and not (j.get("algorithm") or {}).get("descriptors"):
            p.append("map-elites needs algorithm.descriptors")
    if j.get("kind") == "command":
        c = j.get("command") or {}
        if not c.get("argv"):
            p.append("a command job names command.argv")
        if not c.get("units") and j.get("status") == "ready":
            p.append("a command job lists command.units (one exclusive run each)")
    for k in ("requires", "after", "paths"):
        if k in j and not isinstance(j[k], list):
            p.append(f"'{k}' must be a list")
    return p


def validate_job(j):
    return _job_problems(j)


def app_ref():
    """The ref this computer's Froglet Compute itself was installed from (config.json "ref", written by the setup),
    as origin/<ref>, or None. The worker already runs Tools/Compute from it, so a command job filed on that same ref
    asks for no trust the person has not already given: until Froglet Compute is merged, that is how the compute
    branch's own command jobs (unity-train) run on the computers installed from it."""
    ref = os.environ.get("FROGLET_COMPUTE_APP_REF") or (read_json(os.path.join(cache_dir(), "config.json"), {}) or {}).get("ref")
    return ("origin/" + ref.replace("origin/", "")) if ref else None


def untrusted(j):
    """Why a worker will not EXECUTE `j` (CODE_REFS, COMMAND_REFS), or None. A job read from the working tree
    (`--local`, the self-test) is the person's own and is trusted."""
    source = j.get("_ref")
    if source is None:
        return None
    ref = j.get("ref") or "origin/bleeding-edge"
    if ref not in CODE_REFS:
        return f"its code ref {ref} is not one workers run ({', '.join(CODE_REFS)})"
    if j.get("kind") == "command" and source not in COMMAND_REFS and source != app_ref():
        return f"a command job runs only from {', '.join(COMMAND_REFS)} or this computer's own install ref (this one is on {source})"
    return None


def load_jobs_from_dir(d):
    jobs = {}
    if not os.path.isdir(d):
        return jobs
    for name in sorted(os.listdir(d)):
        if name.endswith(".json"):
            with open(os.path.join(d, name), encoding="utf-8") as fh:
                j = json.load(fh)
            j["_source"] = os.path.join(d, name)
            jobs[j.get("id", name[:-5])] = j
    return jobs


def load_jobs_from_ref(ref, cwd=None):
    jobs = {}
    listing = git_ok("ls-tree", "--name-only", f"{ref}:{JOBS_DIR}", cwd=cwd)
    if not listing:
        return jobs
    for name in listing.split():
        if name.endswith(".json"):
            try:
                j = json.loads(git("show", f"{ref}:{JOBS_DIR}/{name}", cwd=cwd))
            except Exception:
                continue
            j["_source"] = f"{ref}:{JOBS_DIR}/{name}"
            j["_ref"] = ref
            jobs[j.get("id", name[:-5])] = j
    return jobs


def load_jobs(refs=None, local=False, cwd=None):
    """The queue: every job on every queue ref (a job edited on two refs: the later `updated` wins), or the working
    tree's Docs/Compute/jobs when `local` (for authoring and the self-test)."""
    if local:
        return load_jobs_from_dir(os.path.join(cwd or REPO, JOBS_DIR))
    jobs = {}
    for ref in refs or QUEUE_REFS:
        if not rev(ref, cwd=cwd):
            continue
        for jid, j in load_jobs_from_ref(ref, cwd=cwd).items():
            if jid not in jobs or str(j.get("updated", "")) > str(jobs[jid].get("updated", "")):
                jobs[jid] = j
    return jobs


def epoch_key(job, evaluator_version="", inputs_hash=""):
    """Results are only comparable within one EPOCH: the same space, objective, evaluator version and inputs. A change
    to any of them starts a new epoch (the old one stays on the board as history)."""
    spec = {k: job.get(k) for k in ("kind", "space", "evaluator", "command")}
    spec["algo"] = (job.get("algorithm") or {}).get("name")
    spec["descriptors"] = (job.get("algorithm") or {}).get("descriptors")
    blob = json.dumps(spec, sort_keys=True) + "|" + str(evaluator_version) + "|" + inputs_hash
    return hashlib.sha1(blob.encode()).hexdigest()[:12]


def hash_paths(root, rels):
    h = hashlib.sha1()
    for rel in sorted(rels or []):
        p = os.path.join(root, rel)
        h.update(rel.encode())
        if os.path.isfile(p):
            with open(p, "rb") as fh:
                h.update(fh.read())
        else:
            h.update(b"<missing>")
    return h.hexdigest()[:12]


# ── the machine ──────────────────────────────────────────────────────────────

def _has_module(name):
    try:
        __import__(name)
        return True
    except Exception:
        return False


def skip_modes(root=None):
    """The game modes Compute leaves out everywhere (Docs/Compute/skip_modes.json): {mode: why}."""
    return (read_json(os.path.join(root or REPO, "Docs", "Compute", "skip_modes.json"), {}) or {}).get("modes") or {}


CAPS_VERSION = 2          # raise it when capabilities() learns a new one, so a day-old cache is asked again


def capabilities(refresh=False):
    """What this machine can run. Cached for a day; `doctor` refreshes it."""
    path = os.path.join(cache_dir(), "capabilities.json")
    if not refresh and os.path.exists(path) and time.time() - os.path.getmtime(path) < 86400:
        with open(path, encoding="utf-8") as fh:
            cached = json.load(fh)
        if cached.get("caps_version") == CAPS_VERSION:      # a newer Froglet Compute asks about more (unity)
            return cached
    caps = {"caps_version": CAPS_VERSION, "python3": True, "os": platform.system().lower(), "cores": os.cpu_count() or 1,
            "python": platform.python_version()}
    caps["numpy"] = _has_module("numpy")
    torch_ok = False
    cuda = False
    try:
        out = subprocess.run([console_python(), "-c", "import torch,sys;sys.stdout.write(str(torch.cuda.is_available()))"],
                             capture_output=True, text=True, timeout=120, **NO_WINDOW)
        torch_ok = out.returncode == 0
        cuda = torch_ok and out.stdout.strip() == "True"
    except Exception:
        pass
    caps["torch"], caps["cuda"] = torch_ok, cuda
    caps["node"] = shutil.which("node") is not None
    dotnet_root = os.environ.get("DOTNET_ROOT") or os.path.join(os.path.expanduser("~"), ".dotnet")
    caps["dotnet8"] = bool(shutil.which("dotnet")) or os.path.isdir(os.path.join(dotnet_root, "sdk"))
    caps["bash"] = shutil.which("bash") is not None
    try:                    # the Unity editor of the project's own version: the real game, for the unity-train job
        import unity_run
        caps["unity"] = unity_run.find_editor(unity_run.project_version(rev("origin/bleeding-edge")))[0] is not None
    except Exception:
        caps["unity"] = False
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(caps, fh, indent=1)
    return caps


def missing_requirements(job, caps):
    return [r for r in job.get("requires", []) if not caps.get(r)]


def idle_seconds():
    """Seconds since this person last touched the keyboard or mouse; None when the OS will not say."""
    system = platform.system()
    try:
        if system == "Windows":
            import ctypes

            class LASTINPUTINFO(ctypes.Structure):
                _fields_ = [("cbSize", ctypes.c_uint), ("dwTime", ctypes.c_uint)]
            li = LASTINPUTINFO()
            li.cbSize = ctypes.sizeof(LASTINPUTINFO)
            if ctypes.windll.user32.GetLastInputInfo(ctypes.byref(li)):
                # Both are 32-bit millisecond ticks that wrap every 49.7 days.
                return ((ctypes.windll.kernel32.GetTickCount() - li.dwTime) & 0xFFFFFFFF) / 1000.0
        elif system == "Darwin":
            out = subprocess.run(["ioreg", "-c", "IOHIDSystem"], capture_output=True, text=True, timeout=10).stdout
            m = re.search(r'"HIDIdleTime"\s*=\s*(\d+)', out)
            if m:
                return int(m.group(1)) / 1e9
        else:
            if shutil.which("xprintidle") and os.environ.get("DISPLAY"):
                out = subprocess.run(["xprintidle"], capture_output=True, text=True, timeout=10).stdout.strip()
                if out.isdigit():
                    return int(out) / 1000.0
    except Exception:
        pass
    return None


def parse_window(spec):
    """'22:00-07:00' -> ((22, 0), (7, 0)); a window may wrap past midnight. None for no window."""
    if not spec:
        return None
    m = re.fullmatch(r"\s*(\d{1,2}):(\d{2})\s*-\s*(\d{1,2}):(\d{2})\s*", spec)
    if not m or int(m.group(1)) > 23 or int(m.group(3)) > 23 or int(m.group(2)) > 59 or int(m.group(4)) > 59:
        raise ValueError(f"--between wants HH:MM-HH:MM in local time, e.g. 22:00-07:00 (got {spec!r})")
    return (int(m.group(1)), int(m.group(2))), (int(m.group(3)), int(m.group(4)))


def in_window(window, when=None):
    """Is local time `when` (default now) inside `window` (from parse_window)? No window means always."""
    if window is None:
        return True
    t = time.localtime(when) if when is None or isinstance(when, (int, float)) else when
    m = t.tm_hour * 60 + t.tm_min
    start, end = window[0][0] * 60 + window[0][1], window[1][0] * 60 + window[1][1]
    return start <= m < end if start <= end else (m >= start or m < end)


def on_battery():
    system = platform.system()
    try:
        if system == "Windows":
            import ctypes

            class SPS(ctypes.Structure):
                _fields_ = [("ACLineStatus", ctypes.c_byte), ("BatteryFlag", ctypes.c_byte),
                            ("BatteryLifePercent", ctypes.c_byte), ("SystemStatusFlag", ctypes.c_byte),
                            ("BatteryLifeTime", ctypes.c_ulong), ("BatteryFullLifeTime", ctypes.c_ulong)]
            s = SPS()
            if ctypes.windll.kernel32.GetSystemPowerStatus(ctypes.byref(s)):
                return s.ACLineStatus == 0
        elif system == "Darwin":
            out = subprocess.run(["pmset", "-g", "batt"], capture_output=True, text=True, timeout=10).stdout
            return "Battery Power" in out
        else:
            base = "/sys/class/power_supply"
            if os.path.isdir(base):
                for name in os.listdir(base):
                    p = os.path.join(base, name, "online")
                    if os.path.exists(p) and ("AC" in name.upper() or "ADP" in name.upper()):
                        return open(p).read().strip() == "0"
    except Exception:
        pass
    return False


def lower_priority():
    """Run below everything the person runs: nice 19 / IDLE_PRIORITY_CLASS."""
    try:
        if platform.system() == "Windows":
            import ctypes
            ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x40)
        else:
            os.nice(19)
    except Exception:
        pass


# ── results on disk ──────────────────────────────────────────────────────────

def results_folder(user, host):
    d = os.path.join(cache_dir(), "results", user, host)
    os.makedirs(d, exist_ok=True)
    return d


def read_json(path, default=None):
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    except Exception:
        return default


def write_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=1, sort_keys=True, default=float)
    os.replace(tmp, path)


def finite(x):
    try:
        return math.isfinite(float(x))
    except Exception:
        return False


# ── the board's data: every machine's published results ──────────────────────

def result_branches(cwd=None):
    out = git_ok("for-each-ref", "--format=%(refname:short) %(committerdate:unix)",
                 f"refs/remotes/origin/{RESULTS_PREFIX}/", cwd=cwd) or ""
    rows = []
    for line in out.splitlines():
        name, _, t = line.partition(" ")
        parts = name.split("/")
        if len(parts) >= 4:
            rows.append({"ref": name, "user": parts[2], "host": "/".join(parts[3:]), "time": int(t or 0)})
    return rows


def read_branch_results(ref, cwd=None):
    """{heartbeat, jobs: {job: {epoch: {best, state_summary, archive}}}, requests: {id: result}} from one machine's
    results commit."""
    out = {"heartbeat": None, "jobs": {}, "requests": {}}
    hb = git_ok("show", f"{ref}:heartbeat.json", cwd=cwd)
    if hb:
        try:
            out["heartbeat"] = json.loads(hb)
        except Exception:
            pass
    for name in (git_ok("ls-tree", "--name-only", ref, "requests/", cwd=cwd) or "").split():
        if name.endswith(".json"):
            try:
                r = json.loads(git("show", f"{ref}:{name}", cwd=cwd))
                out["requests"][r.get("id") or os.path.basename(name)[:-5]] = r
            except Exception:
                pass
    listing = git_ok("ls-tree", "-r", "--name-only", ref, "jobs", cwd=cwd) or ""
    for path in listing.split("\n"):
        m = re.fullmatch(r"jobs/([^/]+)/([^/]+)/(best|summary|archive|unit-[^/]+)\.json", path.strip())
        if not m:
            continue
        job, epoch, what = m.groups()
        try:
            data = json.loads(git("show", f"{ref}:{path.strip()}", cwd=cwd))
        except Exception:
            continue
        out["jobs"].setdefault(job, {}).setdefault(epoch, {})[what] = data
    return out


def gather(cwd=None):
    """Every machine's results, plus who is on what right now."""
    machines = []
    for b in result_branches(cwd=cwd):
        r = read_branch_results(b["ref"], cwd=cwd)
        r.update(b)
        machines.append(r)
    return machines


def active_on(machines, job_id, within=3 * 3600):
    t = now()
    n = 0
    for m in machines:
        hb = m.get("heartbeat") or {}
        cur = hb.get("current") or {}
        if cur.get("job") == job_id and t - int(hb.get("updated", 0)) < within:
            n += 1
    return n


def merged_job(machines, job_id):
    """Per epoch: the best result across machines, total evaluations and machine-hours, the improvement history."""
    epochs = {}
    for m in machines:
        for epoch, d in (m["jobs"].get(job_id) or {}).items():
            e = epochs.setdefault(epoch, {"best": None, "evals": 0, "seconds": 0.0, "machines": [], "history": [],
                                          "archive": {}, "units": {}, "updated": 0})
            s = d.get("summary") or {}
            e["evals"] += int(s.get("evals", 0))
            e["seconds"] += float(s.get("seconds", 0.0))
            e["updated"] = max(e["updated"], int(s.get("updated", 0)))
            e["machines"].append(f"{m['user']}/{m['host']}")
            for h in s.get("history", []):
                e["history"].append(h)
            b = d.get("best")
            if b and finite(b.get("loss")) and (e["best"] is None or b["loss"] < e["best"]["loss"]):
                e["best"] = dict(b, machine=f"{m['user']}/{m['host']}")
            a = (d.get("archive") or {}).get("cells", {})
            for cell, elite in a.items():
                cur = e["archive"].get(cell)
                if cur is None or elite["loss"] < cur["loss"]:
                    e["archive"][cell] = dict(elite, machine=f"{m['user']}/{m['host']}")
            for k, v in d.items():
                if k.startswith("unit-"):
                    u = k[5:]
                    cur = e["units"].get(u)
                    if cur is None or float(v.get("progress", 0)) > float(cur.get("progress", 0)):
                        e["units"][u] = dict(v, machine=f"{m['user']}/{m['host']}")
    for e in epochs.values():
        e["history"].sort(key=lambda h: h[2] if len(h) > 2 else 0)
    return epochs


def current_epoch(epochs):
    if not epochs:
        return None, None
    k = max(epochs, key=lambda k: epochs[k]["updated"])
    return k, epochs[k]


def best_curve(epoch):
    """The team's best loss as evaluations accumulate, [(evals, loss), ...]. Each history row is
    [evals_at, loss, unix_time, machine] on its machine; machines run in parallel, so time orders them and evaluations
    are cumulative across machines in that order."""
    hist = sorted((epoch or {}).get("history") or [], key=lambda h: h[2] if len(h) > 2 else 0)
    best, cum, curve, last_evals = float("inf"), 0, [], {}
    for h in hist:
        if len(h) < 4:
            continue
        machine = h[3]
        cum += max(0, int(h[0]) - last_evals.get(machine, 0))
        last_evals[machine] = int(h[0])
        if h[1] < best:
            best = h[1]
            curve.append((cum, best))
    return curve


def converged(job, epoch):
    """Converged when the best across every machine has not improved by `min_improvement` (relative) over the last
    `window_evals` evaluations, and at least `min_evals` have been spent."""
    if not epoch or not epoch.get("best"):
        return False
    c = dict({"min_evals": 2000, "window_evals": 1500, "min_improvement": 0.003}, **(job.get("convergence") or {}))
    total = epoch["evals"]
    if total < c["min_evals"]:
        return False
    curve = best_curve(epoch)
    if not curve:
        return False
    final = curve[-1][1]
    ref = next((l for (ev, l) in reversed(curve) if ev <= total - c["window_evals"]), curve[0][1])
    scale = max(abs(ref), 1e-9)
    return (ref - final) / scale < c["min_improvement"]


def priority(job, machines=None, jobs=None, epoch=None):
    """How much the next machine-hour on this job is worth. value x unblocks x asked x starvation / crowding.
    Every factor is printed by `compute.py top --why`, so the ranking can be argued with."""
    v = float(job.get("value", 5))
    dependents = sum(1 for j in (jobs or {}).values() if job["id"] in (j.get("after") or []))
    unblocks = 1.0 + 0.5 * dependents
    asked = 1.3 if job.get("asked_by") else 1.0
    last = (epoch or {}).get("updated", 0) or 0
    days = (now() - last) / 86400 if last else 7.0
    starvation = 1.0 + min(1.0, days / 7.0)
    crowd = 1.0 + (active_on(machines or [], job["id"]) if machines else 0)
    score = v * unblocks * asked * starvation / crowd
    return round(score, 3), {"value": v, "unblocks": unblocks, "asked": asked, "starvation": round(starvation, 2),
                             "crowding": crowd}


def blocked_by(job, jobs, machines_cache=None):
    """The `after` jobs that are not yet converged/promoted."""
    out = []
    for a in job.get("after") or []:
        dep = (jobs or {}).get(a)
        if dep is None:
            out.append(f"{a} (missing)")
        elif dep.get("status") not in ("converged", "promoted", "retired"):
            out.append(a)
    return out
