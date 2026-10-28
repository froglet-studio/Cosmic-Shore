"""The set-and-forget worker: `compute.py run`.

One loop, forever (or `--once`):

    wait until the person is away (idle >= --when-idle minutes, on mains power)
    fetch the queue refs and every machine's results
    pick the highest-priority job this machine can run (compute_lib.priority, divided by how many machines are on it)
    export the job's code at its ref into a private folder (never the person's working tree or repository config)
    run it for one session (--session minutes), yielding the moment the person comes back
    publish this machine's results to refs/heads/compute/<user>/<host>

A SEARCH job runs its evaluator on a process pool, one generation at a time; a COMMAND job (a long training) runs one
exclusive UNIT as a subprocess that checkpoints, so any machine can pick a unit up where another left it.
"""
import concurrent.futures as cf
import fnmatch
import glob
import importlib
import json
import math
import multiprocessing as mp
import os
import shutil
import signal
import subprocess
import sys
import time

import compute_lib as L
import optimizers as O

TOOLS = os.path.dirname(os.path.abspath(__file__))
VERSION = "1"


# ── worktrees ────────────────────────────────────────────────────────────────

def _evaluator(job):
    return importlib.import_module("evaluators." + job["evaluator"]["module"]) if job.get("kind") == "search" else None


def sparse_paths(job, module=None):
    paths = list(job.get("paths") or [])
    if module is not None:
        paths += list(getattr(module, "PATHS", []))
    if job.get("kind") == "command":
        paths += list((job.get("command") or {}).get("paths") or [])
    return sorted(set(paths)) or ["Tools/"]


def prepare_target(job, module=None, cwd=None, quiet=False):
    """The job's paths at its ref's tip (default origin/bleeding-edge), exported into a private folder under
    ~/.froglet-compute/wt (no worktree, no sparse-checkout: the person's repository config is never written)."""
    ref = job.get("ref") or "origin/bleeding-edge"
    sha = L.rev(ref, cwd=cwd)
    if not sha:
        raise RuntimeError(f"ref {ref} not found (fetch it: git fetch origin {ref.replace('origin/', '')})")
    patterns = sparse_paths(job, module)
    parent = os.path.join(L.cache_dir(), "wt")
    os.makedirs(parent, exist_ok=True)
    path = L.export_tree(sha, patterns, parent, L.slug(ref), cwd=cwd)
    L.log(f"target {ref} @ {sha[:9]} ({len(patterns)} paths) at {path}", quiet)
    return path, sha


# ── the evaluator pool (spawned processes) ───────────────────────────────────

_EV = {}


def _init_pool(tools, root, module_name, args):
    sys.path[:0] = [tools, os.path.join(root, "Tools", "Build")]
    L.lower_priority()
    m = importlib.import_module("evaluators." + module_name)
    ctx = {"root": root, "args": args, "tools": tools}
    if hasattr(m, "setup"):
        m.setup(ctx)
    _EV.update(module=m, ctx=ctx)


def _eval_one(params):
    t = time.time()
    try:
        r = _EV["module"].evaluate(params, _EV["ctx"])
        r = dict(r or {})
    except Exception as e:  # an evaluator fault is a result, not a crash: the board shows it
        r = {"loss": float("inf"), "valid": False, "error": f"{type(e).__name__}: {e}"}
    r.setdefault("loss", float("inf"))
    if not L.finite(r["loss"]):
        r["loss"] = float("inf")
    r["seconds"] = round(time.time() - t, 3)
    return r


class Pool:
    def __init__(self, cores, root, job):
        ev_args = dict(job["evaluator"].get("args") or {}, _space=job.get("space_full") or job.get("space") or [],
                       _job=job["id"])
        self.args = (TOOLS, root, job["evaluator"]["module"], ev_args)
        self.cores = cores
        self.timeout = float(job["evaluator"].get("timeout", 900))
        self.ex = None

    def _start(self):
        self.ex = cf.ProcessPoolExecutor(max_workers=self.cores, mp_context=mp.get_context("spawn"),
                                         initializer=_init_pool, initargs=self.args)

    def map(self, params_list):
        if self.ex is None:
            self._start()
        futs = [self.ex.submit(_eval_one, p) for p in params_list]
        out = []
        for f in futs:
            try:
                out.append(f.result(timeout=self.timeout))
            except cf.TimeoutError:
                out.append({"loss": float("inf"), "valid": False, "error": "timeout"})
                self.close(kill=True)
                self._start()
            except Exception as e:
                out.append({"loss": float("inf"), "valid": False, "error": f"pool: {e}"})
                self.close(kill=True)
                self._start()
        return out

    def close(self, kill=False):
        if self.ex is not None:
            if kill:
                for p in list(getattr(self.ex, "_processes", {}).values()):
                    try:
                        p.kill()
                    except Exception:
                        pass
            self.ex.shutdown(wait=not kill, cancel_futures=True)
            self.ex = None


# ── presence ─────────────────────────────────────────────────────────────────

class Presence:
    """Is the person away? `when_idle` minutes of no input, on mains power (unless allowed on battery)."""

    def __init__(self, when_idle, always=False, on_battery=False, quiet=False, between=None):
        self.when_idle, self.always, self.allow_battery, self.quiet = when_idle, always, on_battery, quiet
        self.window = L.parse_window(between)        # --between 22:00-07:00: only inside these hours
        self._warned = False

    def away(self):
        if not L.in_window(self.window):
            return False
        if self.always:
            return True
        if not self.allow_battery and L.on_battery():
            return False
        idle = L.idle_seconds()
        if idle is None:
            if not self._warned:
                L.log("this OS does not report idle time: running at the lowest priority anyway "
                      "(install xprintidle, or pass --always on a dedicated box)", self.quiet)
                self._warned = True
            return True
        return idle >= self.when_idle * 60

    def waiting_for(self):
        """What the worker is waiting on, in words."""
        if not L.in_window(self.window):
            (h1, m1), (h2, m2) = self.window
            return f"the {h1:02d}:{m1:02d}-{h2:02d}:{m2:02d} window"
        return "you to be away"

    def back(self):
        """The person touched the machine, or the --between window closed: yield NOW (checkpoint and stop)."""
        if not L.in_window(self.window):
            return True
        if self.always:
            return False
        if not self.allow_battery and L.on_battery():
            return True
        idle = L.idle_seconds()
        return idle is not None and idle < 5


# ── one search session ───────────────────────────────────────────────────────

def _global_best_u(space, epoch):
    if epoch and epoch.get("best") and epoch["best"].get("params"):
        return space.encode(epoch["best"]["params"])
    return None


def search_session(job, root, sha, ident, machines, presence, minutes, cores, max_evals=None, quiet=False,
                   on_progress=None):
    module = _evaluator(job)
    ev_args = job["evaluator"].get("args") or {}
    inputs = module.inputs(ev_args) if hasattr(module, "inputs") else []
    epoch = L.epoch_key(job, getattr(module, "VERSION", "0"), L.hash_paths(root, inputs))
    user, host = ident
    folder = os.path.join(L.results_folder(user, host), "jobs", job["id"], epoch)
    state_path = os.path.join(L.cache_dir(), "state", job["id"], epoch + ".json")
    space = O.Space(job["space"])
    others = L.merged_job(machines, job["id"]).get(epoch)
    gbest = _global_best_u(space, others)

    saved = L.read_json(state_path)
    summary = L.read_json(os.path.join(folder, "summary.json"), {}) or {}
    best = L.read_json(os.path.join(folder, "best.json"))
    if saved:
        opt = O.restore(job, space, saved)
        L.log(f"{job['id']}: resuming epoch {epoch} ({summary.get('evals', 0)} evals here)", quiet)
    else:
        x0 = gbest or space.init_point()
        opt = O.make(job, space, seed=hash((user, host, time.time())) & 0xFFFFFFFF, x0=x0)
        L.log(f"{job['id']}: new epoch {epoch} here" + (" (starting from the team's best)" if gbest else ""), quiet)
    if isinstance(opt, O.MapElites) and others and others.get("archive"):
        opt.merge({c: e for c, e in others["archive"].items() if "u" in e})
        if not opt.archive and job.get("seeds"):
            opt.seeds = [space.encode(s) for s in job["seeds"]]
    elif isinstance(opt, O.MapElites) and not saved and job.get("seeds"):
        opt.seeds = [space.encode(s) for s in job["seeds"]]

    fixed = dict(job.get("fixed") or {})                  # a manual run's settings held at a value
    pool = Pool(cores, root, job)
    t_start = time.time()
    evals = int(summary.get("evals", 0))
    evals0, best0 = evals, (best or {}).get("loss")
    seconds = float(summary.get("seconds", 0.0))
    history = summary.get("history", [])
    errors = summary.get("errors", {})
    machine = f"{user}/{host}"
    worst = float((job.get("algorithm") or {}).get("worst", 1.0))

    # The shipped values, scored once per epoch per machine: the board's "how much better than today".
    if "baseline" not in summary:
        base = module.baseline(ev_args) if hasattr(module, "baseline") else None
        if not base and any(d.get("init") is not None for d in job["space"]):
            base = space.decode(space.init_point())        # a job's `init` values ARE the shipped numbers
            base = dict(job.get("fixed_init") or {}, **base)   # a manual run's fixed settings, at today's values
        if base:
            r = pool.map([base])[0]
            summary["baseline"] = {"loss": r["loss"], "metrics": r.get("metrics", {}), "params": base,
                                   "error": r.get("error")}
            L.log(f"{job['id']}: shipped values score {r['loss']:.5g}" + (f" ({r['error']})" if r.get("error") else ""), quiet)
    last_save = last_tick = time.time()
    stopped = "budget"
    try:
        while True:
            if presence.back():
                stopped = "the person came back"
                break
            if time.time() - t_start > minutes * 60:
                break
            if max_evals and evals - evals0 >= max_evals:      # this session's budget, not the machine's total
                stopped = "max evals"
                break
            us = opt.ask()
            params = [dict(fixed, **space.decode(u)) for u in us]
            results = pool.map(params)
            n_err = sum(1 for r in results if r.get("error"))
            for r in results:
                if r.get("error"):
                    errors[r["error"][:160]] = errors.get(r["error"][:160], 0) + 1
            if n_err == len(results) and evals < 3 * len(results):
                stopped = f"evaluator failing: {results[0]['error']}"
                summary["broken"] = {"error": results[0]["error"], "sha": sha, "time": L.now()}
                break
            evals += len(results)
            if isinstance(opt, O.MapElites):
                opt.tell(us, results)
                qd = opt.qd_score(worst)
                if not history or -qd < history[-1][1] - 1e-12:
                    history.append([evals, -qd, L.now(), machine])
            else:
                opt.tell(us, [r["loss"] for r in results], global_best_u=gbest)
            improved = False
            for u, p, r in zip(us, params, results):
                if L.finite(r["loss"]) and (best is None or r["loss"] < best["loss"]):
                    best = {"loss": r["loss"], "params": p, "metrics": r.get("metrics", {}),
                            "descriptors": r.get("descriptors"), "sha": sha, "evals_at": evals, "time": L.now()}
                    improved = True
            if improved:
                if not isinstance(opt, O.MapElites):
                    history.append([evals, best["loss"], L.now(), machine])
                L.log(f"{job['id']}: new best {best['loss']:.6g} after {evals} evals", quiet)
            if on_progress and time.time() - last_tick > 20:      # live: the hub hears every ~20 s
                last_tick = time.time()
                on_progress({"epoch": epoch, "evals": evals - evals0, "best": (best or {}).get("loss"),
                             "best_start": best0, "live_only": True})
            if time.time() - last_save > 120:
                _save(folder, state_path, opt, summary, best, evals, seconds + time.time() - t_start, history, errors,
                      space, sha)
                last_save = time.time()
                if on_progress:
                    on_progress({"epoch": epoch, "evals": evals - evals0, "best": (best or {}).get("loss"),
                                 "best_start": best0})
    finally:
        pool.close(kill=stopped == "the person came back")
        _save(folder, state_path, opt, summary, best, evals, seconds + time.time() - t_start, history, errors, space, sha)
    return {"job": job["id"], "epoch": epoch, "evals": evals, "session_evals": evals - evals0, "stopped": stopped,
            "best": best, "best_start": best0}


def _save(folder, state_path, opt, summary, best, evals, seconds, history, errors, space, sha):
    summary.update({"evals": evals, "seconds": round(seconds, 1), "history": history[-500:], "updated": L.now(),
                    "errors": dict(sorted(errors.items(), key=lambda kv: -kv[1])[:10]), "sha": sha})
    if hasattr(opt, "restarts"):
        summary["restarts"] = opt.restarts
    L.write_json(os.path.join(folder, "summary.json"), summary)
    if best:
        L.write_json(os.path.join(folder, "best.json"), best)
    if isinstance(opt, O.MapElites):
        cells = {}
        for c, e in opt.archive.items():
            cells[c] = {"loss": e["loss"], "u": e["u"], "params": space.decode(e["u"]),
                        "descriptors": e.get("descriptors"), "metrics": e.get("metrics", {})}
        L.write_json(os.path.join(folder, "archive.json"),
                     {"cells": cells, "filled": len(cells), "total": opt.cells_total})
    L.write_json(state_path, opt.state())


# ── one command session (a long training, one exclusive unit) ────────────────

def _fmt(s, env):
    for k, v in env.items():
        s = s.replace("{" + k + "}", str(v))
    return s


def _read_metric(spec, out_dir):
    if not spec:
        return None
    p = os.path.join(out_dir, spec["file"])
    d = L.read_json(p)
    if d is None:
        return None
    def get(key):
        v = d
        for k in key.split("."):
            if not isinstance(v, dict) or k not in v:
                return None
            v = v[k]
        return v
    v = get(spec["key"])
    if v is not None and spec.get("of"):               # a fraction: step / steps
        total = get(spec["of"])
        return float(v) / float(total) if total else None
    return v


def command_session(job, root, sha, ident, machines, presence, minutes, cores, quiet=False, on_claim=None,
                    on_progress=None, manual=False):
    c = job["command"]
    user, host = ident
    epoch = L.epoch_key(job, c.get("version", "1"))
    merged = L.merged_job(machines, job["id"]).get(epoch) or {"units": {}}
    me = f"{user}/{host}"
    lease = float(c.get("lease_hours", 3)) * 3600
    busy = set()
    for m in machines:
        hb = m.get("heartbeat") or {}
        cur = hb.get("current") or {}
        if cur.get("job") == job["id"] and f"{m['user']}/{m['host']}" != me and L.now() - int(hb.get("updated", 0)) < lease:
            busy.add(cur.get("unit"))
    units = [u for u in c["units"] if not (merged["units"].get(u["id"]) or {}).get("done") and u["id"] not in busy]
    if not units:
        return {"job": job["id"], "epoch": epoch, "stopped": "every unit is done or taken"}
    # Relay first: the unit with the most progress somewhere picks up where that machine stopped.
    units.sort(key=lambda u: -float((merged["units"].get(u["id"]) or {}).get("progress", 0)))
    unit = units[0]
    if on_claim:
        on_claim(unit["id"])        # the lease: published before the run starts, so no other machine takes it
    run_dir = os.path.join(L.cache_dir(), "runs", job["id"], unit["id"])
    os.makedirs(run_dir, exist_ok=True)
    folder = os.path.join(L.results_folder(user, host), "jobs", job["id"], epoch)
    mine = L.read_json(os.path.join(folder, f"unit-{unit['id']}.json"), {}) or {}
    theirs = merged["units"].get(unit["id"]) or {}
    if float(theirs.get("progress", 0)) > float(mine.get("progress", 0)) and theirs.get("machine") != me:
        _pull_unit_files(job, epoch, unit["id"], theirs, run_dir, machines)
        L.log(f"{job['id']}/{unit['id']}: relayed from {theirs['machine']} at {theirs.get('progress')}", quiet)
    env = {"python": sys.executable, "out": run_dir, "root": root, "tools": TOOLS, "threads": max(1, cores),
           "sha": sha, "repo": L.REPO, "minutes": minutes, "app_ref": L.app_ref() or "", **{f"unit.{k}": v for k, v in unit.get("args", {}).items()}}
    argv = [_fmt(a, env) for a in c["argv"]]
    if c.get("resume") and any(os.path.exists(os.path.join(run_dir, f)) for f in c.get("resume_if", [])):
        argv += [_fmt(a, env) for a in c["resume"]]
    log_path = os.path.join(run_dir, "worker.log")
    L.log(f"{job['id']}/{unit['id']}: {' '.join(argv)}", quiet)
    kw = {}
    if os.name == "nt":
        kw["creationflags"] = 0x00000200 | 0x00000040 | 0x08000000     # new process group, idle priority, no window
    else:
        kw["preexec_fn"] = lambda: os.nice(19)
    t0 = time.time()
    count0 = _read_metric(c.get("count"), run_dir) or 0         # live "evals" are this session's, as for a search
    with open(log_path, "a", encoding="utf-8") as logf:
        penv = dict(os.environ, FROGLET_COMPUTE_REPO=L.REPO, **({"FROGLET_COMPUTE_MANUAL": "1"} if manual else {}))
        proc = subprocess.Popen(argv, cwd=root, stdout=logf, stderr=subprocess.STDOUT, env=penv, **kw)
        stopped = "budget"
        last_pub = time.time()
        while proc.poll() is None:
            time.sleep(15)
            if on_progress:         # live: the hub hears how far the unit has got every ~15 s
                on_progress({"epoch": epoch, "unit": unit["id"], "progress": _read_metric(c.get("progress"), run_dir),
                             "evals": max(0, int((_read_metric(c.get("count"), run_dir) or 0) - count0)), "best": _read_metric(c.get("score"), run_dir),
                             "phase": _read_metric({"file": (c.get("progress") or {}).get("file", ""), "key": "phase"}, run_dir)
                             if c.get("progress") else None, "live_only": True})
            if presence.back():
                stopped = "the person came back"
                break
            if time.time() - t0 > minutes * 60:
                break
            if time.time() - last_pub > 600:
                _save_unit(job, folder, unit, run_dir, c, sha, done=False)
                last_pub = time.time()
                if on_progress:
                    on_progress({"epoch": epoch, "unit": unit["id"], "progress": _read_metric(c.get("progress"), run_dir)})
        if proc.poll() is None:
            try:
                proc.send_signal(signal.CTRL_BREAK_EVENT if os.name == "nt" else signal.SIGINT)
                proc.wait(timeout=60)
            except Exception:
                proc.kill()
        rc = proc.returncode
    progress = _read_metric(c.get("progress"), run_dir)
    done = rc == 0 and (progress is None or float(progress) >= 1.0)
    _save_unit(job, folder, unit, run_dir, c, sha, done=done)
    if rc not in (0, None) and stopped == "budget":
        why = (L.read_json(os.path.join(run_dir, "result.json"), {}) or {}).get("reason")
        stopped = f"failed (exit {rc}): {why}" if why else f"failed (exit {rc}; see {log_path})"
    count = _read_metric(c.get("count"), run_dir)
    score = _read_metric(c.get("score"), run_dir)
    return {"job": job["id"], "epoch": epoch, "unit": unit["id"], "stopped": "finished" if done else stopped, "rc": rc,
            "count": int(count) if L.finite(count) else None, "best": {"loss": score} if L.finite(score) else None}


def _save_unit(job, folder, unit, run_dir, c, sha, done):
    keep_dir = os.path.join(folder, "files", unit["id"])
    os.makedirs(keep_dir, exist_ok=True)
    cap = float(c.get("max_mb", 25)) * 1024 * 1024
    total = 0
    kept = []
    for pattern in c.get("keep", []):
        for p in sorted(glob.glob(os.path.join(run_dir, pattern))):
            size = os.path.getsize(p)
            if total + size > cap:
                continue
            shutil.copy2(p, os.path.join(keep_dir, os.path.basename(p)))
            total += size
            kept.append(os.path.basename(p))
    progress = _read_metric(c.get("progress"), run_dir)
    score = _read_metric(c.get("score"), run_dir)
    L.write_json(os.path.join(folder, f"unit-{unit['id']}.json"),
                 {"progress": float(progress) if progress is not None else 0.0, "score": score, "done": done,
                  "files": kept, "sha": sha, "updated": L.now()})
    summary = L.read_json(os.path.join(folder, "summary.json"), {}) or {}
    summary.update({"updated": L.now(), "evals": summary.get("evals", 0)})
    L.write_json(os.path.join(folder, "summary.json"), summary)


def _pull_unit_files(job, epoch, unit_id, theirs, run_dir, machines):
    who = theirs.get("machine", "")
    for m in machines:
        if f"{m['user']}/{m['host']}" == who:
            for name in theirs.get("files", []):
                data = subprocess.run(["git", "show", f"{m['ref']}:jobs/{job['id']}/{epoch}/files/{unit_id}/{name}"],
                                      cwd=L.REPO, **L.NO_WINDOW, capture_output=True)
                if data.returncode == 0:
                    with open(os.path.join(run_dir, name), "wb") as fh:
                        fh.write(data.stdout)


# ── choosing ─────────────────────────────────────────────────────────────────

def ranked(jobs, machines, caps):
    """Every job with its priority and, when it cannot run here, why not."""
    rows = []
    for j in jobs.values():
        problems = L.validate_job(j)
        epochs = L.merged_job(machines, j["id"])
        ek, e = L.current_epoch(epochs)
        score, why = L.priority(j, machines, jobs, e)
        reason = None
        distrust = L.untrusted(j)
        if problems:
            reason = "invalid: " + "; ".join(problems)
        elif distrust:
            reason = "not run: " + distrust
        elif j["status"] not in L.RUNNABLE:
            reason = f"status {j['status']}" + (f" ({j.get('blocked_reason')})" if j.get("blocked_reason") else "")
        else:
            blockers = [b for b in L.blocked_by(j, jobs)
                        if not L.converged(jobs.get(b, {}), L.current_epoch(L.merged_job(machines, b))[1])]
            missing = L.missing_requirements(j, caps)
            if blockers:
                reason = "waits on " + ", ".join(blockers)
            elif missing:
                reason = "this machine lacks " + ", ".join(missing)
            elif L.converged(j, e):
                reason = "converged (promote it: compute.py promote " + j["id"] + ")"
        rows.append({"job": j, "score": score, "why": why, "reason": reason, "epoch": ek, "merged": e})
    rows.sort(key=lambda r: (r["reason"] is not None, -r["score"]))
    return rows


def _resolve_request(text, queue, local):
    """--request: a token from the console, a request file, or the id of a request on the queue refs."""
    import manual_runs as R
    if os.path.isfile(text):
        return L.read_json(text)
    if R.ID.fullmatch(text):
        return (R.load(local=True) if local else R.load([f"origin/{q}" for q in queue])).get(text)
    try:
        return R.from_token(text)
    except Exception:
        return None


def run(args):
    import manual_runs as R
    quiet = args.quiet
    L.lower_priority()
    caps = L.capabilities()
    ident = L.identity()
    me = f"{ident[0]}/{ident[1]}"
    cores = args.cores or max(1, (os.cpu_count() or 2) - 1)
    manual = getattr(args, "request", None)
    if manual:                      # a manual run is now, on this computer: it does not wait for idle
        args.always, args.between, args.once = True, None, True
    try:
        presence = Presence(args.when_idle, always=args.always, on_battery=args.on_battery, quiet=quiet,
                            between=args.between)
    except ValueError as e:
        print(e)
        return 2
    folder = L.results_folder(*ident)
    L.log(f"froglet-compute worker {ident[0]}/{ident[1]}: {cores} cores, "
          + ", ".join(k for k, v in caps.items() if v is True), quiet)
    waiting_said = False
    while True:
        if not presence.away():
            if not waiting_said:
                L.log(f"waiting for {presence.waiting_for()} ({args.when_idle} min idle"
                      + ("" if args.on_battery else ", on mains power") + ")", quiet)
                waiting_said = True
            if args.once:
                return 0
            time.sleep(30)
            continue
        waiting_said = False
        queue = [r.replace("origin/", "") for r in (args.queue or L.QUEUE_REFS)]
        if not args.no_fetch:
            L.fetch(queue, results=True, quiet=quiet)
        jobs = L.load_jobs(local=args.local) if args.local else L.load_jobs([f"origin/{q}" for q in queue])
        machines = L.gather()
        # Manual runs first: the one asked for on the command line, else the oldest open request this computer may run.
        req, job, max_evals = None, None, args.max_evals
        if manual:
            req = _resolve_request(manual, queue, args.local)
            base = jobs.get((req or {}).get("job"))
            why = "not a request id, token or file" if req is None else (R.problems(req, base) or
                                                                          ([L.untrusted(base)] if L.untrusted(base) else []))
            if why:
                L.log("this request cannot run: " + "; ".join(why), False)
                if req is not None and base is None:
                    L.log(f"(read the jobs on {', '.join(queue)}; a run made by a newer console needs this computer's "
                          "Froglet Compute updated: run the Install line again)", False)
                return 2
            job = R.derive(base, req)
        elif not args.job and not getattr(args, "no_requests", False):
            reqs = R.load(local=True) if args.local else R.load([f"origin/{q}" for q in queue])
            got = R.pick(reqs, jobs, machines, me, caps)
            if got:
                req, job = got
        if req:
            st = R.progress(req, machines)
            left = st["budget"] - st["tries"]
            if left <= 0:
                L.log(f"request {req['id']} is already done ({st['tries']} of {st['budget']} tries)", quiet)
                return 0
            max_evals = min(left, args.max_evals) if args.max_evals else left
            L.log(f"manual run {req['id']} by {req.get('by')}: {job['id']}, {left} tries left"
                  + (f" - {req['note']}" if req.get("note") else ""), quiet)
        rows = [] if req else ranked(jobs, machines, caps)
        pick = {"job": job} if req else None
        for r in rows:
            if args.job and r["job"]["id"] != args.job:
                continue
            if r["reason"] is None:
                pick = r
                break
        if pick is None:
            L.log("nothing this machine can run" + (f" (job {args.job}: {next((r['reason'] for r in rows if r['job']['id'] == args.job), 'not found')})" if args.job else "")
                  + " - a session can find more: /compute-scout", quiet)
            if args.once:
                return 1
            time.sleep(120)           # look again soon: a manual run from someone's phone may arrive any minute
            continue
        job = pick["job"]
        if req is None:
            max_evals = args.max_evals
        module = _evaluator(job)
        root, sha = prepare_target(job, module, quiet=quiet)
        prev_hb = L.read_json(os.path.join(folder, "heartbeat.json"), {}) or {}
        since = L.now()
        hb = {"user": ident[0], "host": ident[1], "caps": caps, "cores": cores, "version": VERSION,
              "updated": since, "current": {"job": job["id"], "since": since, "minutes": args.session,
                                            "max_evals": max_evals or None, "request": (req or {}).get("id")},
              "runs": prev_hb.get("runs", []), "totals": prev_hb.get("totals", {"sessions": 0})}
        last_progress = [time.time()]

        def progress(p):
            """While a session runs, the console shows how far it has got: to the hub every ~20 s (live), to git
            every few minutes (everyone else)."""
            live_only = p.pop("live_only", False)
            hb["current"].update(p)
            hb["updated"] = L.now()
            L.post_heartbeat(hb)
            if live_only:
                return
            L.write_json(os.path.join(folder, "heartbeat.json"), hb)
            if not args.no_push and time.time() - last_progress[0] >= args.progress_minutes * 60:
                L.publish(folder, *ident, f"compute: {job['id']} progress", quiet=True)
                last_progress[0] = time.time()
        if job["kind"] == "command":
            def claim(unit_id):
                hb["current"]["unit"] = unit_id
                L.write_json(os.path.join(folder, "heartbeat.json"), hb)
                if not args.no_push:
                    L.publish(folder, *ident, f"compute: {job['id']}/{unit_id} claimed", quiet=quiet)
            out = command_session(job, root, sha, ident, machines, presence, args.session, cores, quiet=quiet,
                                  on_claim=claim, on_progress=progress, manual=bool(manual))
            if req:
                # A command request's tries are its unit's count (the Unity trainer: matches played), its best the
                # unit's score.
                path = os.path.join(folder, "requests", req["id"] + ".json")
                mine = L.read_json(path, {}) or {}
                best = (out.get("best") or {}).get("loss")
                if L.finite(mine.get("best")) and (best is None or mine["best"] < best):
                    best = mine["best"]
                L.write_json(path, {"id": req["id"], "job": req["job"], "derived": job["id"], "epoch": out.get("epoch"),
                                    "tries": max(int(mine.get("tries", 0)), int(out.get("count") or 0)),
                                    "best": best, "updated": L.now(), "stopped": out.get("stopped")})
        else:
            L.write_json(os.path.join(folder, "heartbeat.json"), hb)
            L.post_heartbeat(hb)
            if not args.no_push:
                L.publish(folder, *ident, f"compute: {job['id']} started", quiet=quiet)
            out = search_session(job, root, sha, ident, machines, presence, args.session, cores,
                                 max_evals=max_evals, quiet=quiet, on_progress=progress)
            if req:
                # This computer's part of the request: its tries so far and its best, read by the console and by
                # every worker deciding whether the request is done.
                path = os.path.join(folder, "requests", req["id"] + ".json")
                mine = L.read_json(path, {}) or {}
                best = (out.get("best") or {}).get("loss")
                if L.finite(mine.get("best")) and (best is None or mine["best"] < best):
                    best = mine["best"]
                L.write_json(path, {"id": req["id"], "job": req["job"], "derived": job["id"], "epoch": out.get("epoch"),
                                    "tries": int(mine.get("tries", 0)) + int(out.get("session_evals") or 0),
                                    "best": best, "updated": L.now(), "stopped": out.get("stopped")})
        hb["updated"] = L.now()
        totals = hb.get("totals") or {"sessions": 0}
        totals["sessions"] = int(totals.get("sessions", 0)) + 1
        hb["totals"] = totals
        hb["last"] = {k: v for k, v in out.items() if k != "best"}
        # The runs log the console shows: one row per session, newest last, the last 30 kept.
        hb["runs"] = (hb.get("runs") or [])[-29:] + [{
            "job": job["id"], "request": (req or {}).get("id"), "epoch": out.get("epoch"), "unit": out.get("unit"),
            "start": since, "end": L.now(),
            "evals": out.get("session_evals") if out.get("session_evals") is not None else out.get("count"), "stopped": out.get("stopped"), "best_start": out.get("best_start"),
            "best_end": (out.get("best") or {}).get("loss")}]
        if out.get("stopped") != "budget" or args.once:
            hb["current"] = {}
        L.write_json(os.path.join(folder, "heartbeat.json"), hb)
        L.post_heartbeat(hb)
        n = out.get("evals", out.get("count"))
        L.log(f"{job['id']}: session over ({out.get('stopped')}, {n if n is not None else '-'} "
              f"{'matches' if job['kind'] == 'command' and out.get('count') is not None else 'evals'})", quiet)
        if not args.no_push:
            sha_pub = L.publish(folder, *ident, f"compute: {job['id']} ({out.get('stopped')})", quiet=quiet)
            L.log(f"published {('compute/' + ident[0] + '/' + ident[1])} @ {(sha_pub or '?')[:9]}", quiet)
        if args.once:
            return 0
