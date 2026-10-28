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
        ev_args = dict(job["evaluator"].get("args") or {}, _space=job.get("space") or [], _job=job["id"])
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
        if base:
            r = pool.map([base])[0]
            summary["baseline"] = {"loss": r["loss"], "metrics": r.get("metrics", {}), "params": base,
                                   "error": r.get("error")}
            L.log(f"{job['id']}: shipped values score {r['loss']:.5g}" + (f" ({r['error']})" if r.get("error") else ""), quiet)
    last_save = time.time()
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
            params = [space.decode(u) for u in us]
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
                    on_progress=None):
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
    env = {"python": sys.executable, "out": run_dir, "root": root, "tools": TOOLS, "threads": max(1, cores), **{f"unit.{k}": v for k, v in unit.get("args", {}).items()}}
    argv = [_fmt(a, env) for a in c["argv"]]
    if c.get("resume") and any(os.path.exists(os.path.join(run_dir, f)) for f in c.get("resume_if", [])):
        argv += [_fmt(a, env) for a in c["resume"]]
    log_path = os.path.join(run_dir, "worker.log")
    L.log(f"{job['id']}/{unit['id']}: {' '.join(argv)}", quiet)
    kw = {}
    if os.name == "nt":
        kw["creationflags"] = 0x00000200 | 0x00000040     # new process group, idle priority
    else:
        kw["preexec_fn"] = lambda: os.nice(19)
    t0 = time.time()
    with open(log_path, "a", encoding="utf-8") as logf:
        proc = subprocess.Popen(argv, cwd=root, stdout=logf, stderr=subprocess.STDOUT, **kw)
        stopped = "budget"
        last_pub = time.time()
        while proc.poll() is None:
            time.sleep(15)
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
    return {"job": job["id"], "epoch": epoch, "unit": unit["id"], "stopped": "finished" if done else stopped, "rc": rc}


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
                                      cwd=L.REPO, capture_output=True)
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


def run(args):
    quiet = args.quiet
    L.lower_priority()
    caps = L.capabilities()
    ident = L.identity()
    cores = args.cores or max(1, (os.cpu_count() or 2) - 1)
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
        rows = ranked(jobs, machines, caps)
        pick = None
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
            time.sleep(1800)
            continue
        job = pick["job"]
        module = _evaluator(job)
        root, sha = prepare_target(job, module, quiet=quiet)
        prev_hb = L.read_json(os.path.join(folder, "heartbeat.json"), {}) or {}
        since = L.now()
        hb = {"user": ident[0], "host": ident[1], "caps": caps, "cores": cores, "version": VERSION,
              "updated": since, "current": {"job": job["id"], "since": since, "minutes": args.session,
                                            "max_evals": args.max_evals or None},
              "runs": prev_hb.get("runs", []), "totals": prev_hb.get("totals", {"sessions": 0})}
        last_progress = [time.time()]

        def progress(p):
            """While a session runs, the console shows how far it has got: published every few minutes."""
            hb["current"].update(p)
            hb["updated"] = L.now()
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
                                  on_claim=claim, on_progress=progress)
        else:
            L.write_json(os.path.join(folder, "heartbeat.json"), hb)
            if not args.no_push:
                L.publish(folder, *ident, f"compute: {job['id']} started", quiet=quiet)
            out = search_session(job, root, sha, ident, machines, presence, args.session, cores,
                                 max_evals=args.max_evals, quiet=quiet, on_progress=progress)
        hb["updated"] = L.now()
        totals = hb.get("totals") or {"sessions": 0}
        totals["sessions"] = int(totals.get("sessions", 0)) + 1
        hb["totals"] = totals
        hb["last"] = {k: v for k, v in out.items() if k != "best"}
        # The runs log the console shows: one row per session, newest last, the last 30 kept.
        hb["runs"] = (hb.get("runs") or [])[-29:] + [{
            "job": job["id"], "epoch": out.get("epoch"), "unit": out.get("unit"), "start": since, "end": L.now(),
            "evals": out.get("session_evals"), "stopped": out.get("stopped"), "best_start": out.get("best_start"),
            "best_end": (out.get("best") or {}).get("loss")}]
        if out.get("stopped") != "budget":
            hb["current"] = {}
        L.write_json(os.path.join(folder, "heartbeat.json"), hb)
        L.log(f"{job['id']}: session over ({out.get('stopped')}, {out.get('evals', '-')} evals)", quiet)
        if not args.no_push:
            sha_pub = L.publish(folder, *ident, f"compute: {job['id']} ({out.get('stopped')})", quiet=quiet)
            L.log(f"published {('compute/' + ident[0] + '/' + ident[1])} @ {(sha_pub or '?')[:9]}", quiet)
        if args.once:
            return 0
