#!/usr/bin/env python3
"""farm.py - the Ark escort sim farm: N player processes (SimFarmBuild) playing jobs from a folder queue.

    # the runner (leave it running; Ctrl+C or create <root>/STOP to stop)
    python Tools/SimFarm/farm.py run   --root D:/VoyageFarm -n 4 --mode nographics
    # queue work by hand (coevo.py queues its own)
    python Tools/SimFarm/farm.py submit --root D:/VoyageFarm --plan "1:Sparrow,Sparrow,Sparrow" --jobs 8 --runs-per-job 2
    python Tools/SimFarm/farm.py status --root D:/VoyageFarm
    python Tools/SimFarm/farm.py stats  --root D:/VoyageFarm [--since-minutes 60]
    python Tools/SimFarm/farm.py report --root D:/VoyageFarm [--tag smoke]   # ark_escort_report-style table

Each slot launches
    <exe> [-batchmode -nographics | -screen-width W -screen-height H -screen-fullscreen 0] -force-d3d11
          -logFile logs/<job>.a<k>.log -job-worker-count 1
          -arkEscortSim @jobs/running/<job>.player.json -arkEscortOut results/<job>.jsonl -arkEscortQuit
          -arkEscortFixedDt 0.0333 [-arkEscortParams params/<job>.json]
and watches it: the exit, the {"done":true} line, a log heartbeat (--stall s without the log growing), a per-run
clock (--run-timeout s without a new result line; --boot-timeout for the first), and crash markers in the log.
A failed attempt kills the process tree and requeues only the runs without a finished result; after --max-attempts
the missing runs are written as Result 0 with "FarmError" and the job goes to done/ as "partial"/"failed".
Pure stdlib; psutil, when installed, adds memory/CPU numbers and CPU affinity.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import signal
import statistics
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from farmlib import (DEFAULT_EXE_NAME, DEFAULT_ROOT, FINISHED, OUTCOMES, Farm, done_lines, read_json,  # noqa: E402
                     read_lines, read_results, replace_retry, write_json_atomic)

try:
    import psutil  # optional
except ImportError:  # pragma: no cover
    psutil = None

BELOW_NORMAL = 0x00004000
CREATE_NO_WINDOW = 0x08000000
CRASH_MARKERS = re.compile(r"Crash!!!|device (was )?lost|DXGI_ERROR_DEVICE|\[ArkEscortSim\] (never ready|threw)")


def now() -> float:
    return time.time()


class Slot:
    def __init__(self, index: int, cpus: list[int] | None):
        self.index = index
        self.cpus = cpus
        self.proc: subprocess.Popen | None = None
        self.job: dict | None = None
        self.job_path: Path | None = None
        self.log: Path | None = None
        self.started = 0.0
        self.last_progress = 0.0
        self.last_log_size = -1
        self.last_log_change = 0.0
        self.log_offset = 0
        self.lines_seen = 0
        self.done_seen_at = 0.0
        self.peak_rss = 0
        self.peak_private = 0
        self.cpu_samples: list[float] = []
        self.failure = ""
        self.ps = None

    @property
    def busy(self) -> bool:
        return self.job is not None


class Runner:
    def __init__(self, a):
        self.a = a
        self.farm = Farm(a.root).ensure()
        exe = Path(a.exe) if a.exe else self.farm.root / "build" / DEFAULT_EXE_NAME
        if not exe.exists():
            sys.exit(f"no player at {exe} (build it with SimFarmBuild, or pass --exe)")
        self.exe = exe
        ncpu = os.cpu_count() or 8
        self.slots = []
        for i in range(a.n):
            cpus = None
            if a.affinity > 0 and psutil:
                # Leave the first two logical CPUs to the OS and the editor; then a block of --affinity per slot.
                cpus = [(2 + i * a.affinity + k) % ncpu for k in range(a.affinity)]
            self.slots.append(Slot(i, cpus))
        self.stopping = False

    # ---- queue ----------------------------------------------------------------------------------------------
    def recover_orphans(self):
        """Jobs left in running/ by a runner that died go back to pending/ (their finished runs stay counted), and
        players it left behind are killed."""
        if psutil:
            for p in psutil.process_iter(["name", "exe"]):
                try:
                    if p.info["exe"] and Path(p.info["exe"]).resolve() == self.exe.resolve():
                        print(f"[farm] killing a stray player, pid {p.pid}", flush=True)
                        subprocess.run(["taskkill", "/T", "/F", "/PID", str(p.pid)],
                                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                except Exception:
                    pass
        for p in self.farm.running.glob("*.json"):
            if p.name.endswith(".player.json"):
                p.unlink(missing_ok=True)
                continue
            job = read_json(p)
            self.requeue(job, p, "runner restarted", count_attempt=False)

    def next_job(self) -> Path | None:
        best, best_key = None, None
        for p in self.farm.pending.glob("*.json"):
            try:
                key = (p.stat().st_mtime, p.name)
            except FileNotFoundError:
                continue
            if best_key is None or key < best_key:
                best, best_key = p, key
        return best

    # ---- launch ---------------------------------------------------------------------------------------------
    def launch(self, slot: Slot, pending_path: Path):
        job_id = pending_path.stem
        running_path = self.farm.running / pending_path.name
        try:
            replace_retry(pending_path, running_path)
        except FileNotFoundError:
            return
        job = read_json(running_path)
        attempt = int(job.get("attempt", 0))
        results = self.farm.results_path(job_id)
        have = read_results(results)
        runs = [r for r in job.get("runs", []) if have.get(int(r.get("index", -1)), {}).get("Result", 0) not in FINISHED]
        if not runs and job.get("runs"):
            self.finish(slot, job, running_path, "ok", "already complete")
            return
        player_job = {"jobId": job_id, "seed": int(job.get("seed", 0)), "runs": runs}
        if not runs and job.get("plan"):
            player_job["plan"] = job["plan"]
        player_path = self.farm.running / f"{job_id}.player.json"
        write_json_atomic(player_path, player_job)

        args = [sys.executable, str(self.exe)] if self.exe.suffix == ".py" else [str(self.exe)]  # .py: a test double
        mode = job.get("mode") or self.a.mode
        if mode == "nographics":
            args += ["-batchmode", "-nographics"]
        else:
            args += ["-screen-width", str(self.a.width), "-screen-height", str(self.a.height), "-screen-fullscreen", "0"]
        args += ["-force-d3d11"]
        log = self.farm.logs / f"{job_id}.a{attempt}.log"
        args += ["-logFile", str(log)]
        if self.a.job_workers > 0:
            args += ["-job-worker-count", str(self.a.job_workers)]
        args += ["-arkEscortSim", "@" + str(player_path), "-arkEscortOut", str(results), "-arkEscortQuit"]
        fixed_dt = float(job.get("fixedDt", self.a.fixed_dt))
        if fixed_dt > 0:
            args += ["-arkEscortFixedDt", f"{fixed_dt:g}"]
        if self.a.time_scale and self.a.time_scale != 1:
            args += ["-arkEscortTimeScale", f"{self.a.time_scale:g}"]
        params = job.get("params")
        params_path = job.get("paramsPath")
        if params:
            params_path = str(self.farm.params / f"{job_id}.json")
            write_json_atomic(Path(params_path), params)
        if params_path:
            args += ["-arkEscortParams", str(params_path)]
        args += list(self.a.extra or [])

        flags = BELOW_NORMAL if self.a.below_normal else 0
        if mode == "nographics":
            flags |= CREATE_NO_WINDOW
        slot.proc = subprocess.Popen(args, cwd=str(self.exe.parent), creationflags=flags,
                                     stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL)
        slot.job, slot.job_path, slot.log = job, running_path, log
        slot.started = slot.last_progress = slot.last_log_change = now()
        slot.last_log_size, slot.log_offset, slot.done_seen_at, slot.failure = -1, 0, 0.0, ""
        slot.lines_seen = len(read_lines(results))
        slot.peak_rss = slot.peak_private = 0
        slot.cpu_samples = []
        slot.ps = None
        if psutil:
            try:
                slot.ps = psutil.Process(slot.proc.pid)
                if slot.cpus:
                    slot.ps.cpu_affinity(slot.cpus)
                slot.ps.cpu_percent(None)
            except Exception:
                slot.ps = None
        slot.job["_runsThisAttempt"] = len(runs)
        print(f"[farm] slot {slot.index}: {job_id} a{attempt} ({len(runs)} runs, {mode}) pid {slot.proc.pid}", flush=True)

    # ---- watch ----------------------------------------------------------------------------------------------
    def watch(self, slot: Slot):
        t = now()
        job_id = slot.job["jobId"]
        results = self.farm.results_path(job_id)
        lines = read_lines(results)
        if len(lines) > slot.lines_seen:
            slot.lines_seen = len(lines)
            slot.last_progress = t
        done = [r for r in lines if r.get("done")]
        if done and not slot.done_seen_at:
            slot.done_seen_at = t

        if slot.log and slot.log.exists():
            size = slot.log.stat().st_size
            if size != slot.last_log_size:
                slot.last_log_size, slot.last_log_change = size, t
                try:
                    with open(slot.log, "rb") as f:
                        f.seek(slot.log_offset)
                        chunk = f.read().decode("utf-8", "replace")
                        slot.log_offset = f.tell()
                    m = CRASH_MARKERS.search(chunk)
                    if m and not slot.failure:
                        slot.failure = f"log: {m.group(0)}"
                except OSError:
                    pass

        if slot.ps:
            try:
                mi = slot.ps.memory_info()
                slot.peak_rss = max(slot.peak_rss, getattr(mi, "peak_wset", 0) or mi.rss)
                slot.peak_private = max(slot.peak_private, getattr(mi, "private", 0) or 0)
                slot.cpu_samples.append(slot.ps.cpu_percent(None))
            except Exception:
                pass

        code = slot.proc.poll()
        if code is not None:
            self.end_attempt(slot, f"exit {code}" if not done else "", code)
            return
        reason = ""
        if done and t - slot.done_seen_at > self.a.quit_grace:
            reason = ""  # finished, but the process hangs on exit: just kill it
            self.kill(slot)
            self.end_attempt(slot, reason, None)
            return
        first = slot.lines_seen == 0 or slot.last_progress == slot.started
        limit = (self.a.boot_timeout + self.a.run_timeout) if first else self.a.run_timeout
        if slot.failure:
            reason = slot.failure
        elif t - slot.last_progress > limit:
            reason = f"no result for {t - slot.last_progress:.0f}s"
        elif t - slot.last_log_change > self.a.stall:
            reason = f"log stalled for {t - slot.last_log_change:.0f}s"
        if reason:
            print(f"[farm] slot {slot.index}: {job_id} failing: {reason}", flush=True)
            self.kill(slot)
            self.end_attempt(slot, reason, None)

    def kill(self, slot: Slot):
        if slot.proc and slot.proc.poll() is None:
            subprocess.run(["taskkill", "/T", "/F", "/PID", str(slot.proc.pid)],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            try:
                slot.proc.wait(15)
            except subprocess.TimeoutExpired:
                pass

    def end_attempt(self, slot: Slot, reason: str, code):
        job, path = slot.job, slot.job_path
        job_id = job["jobId"]
        results = self.farm.results_path(job_id)
        have = read_results(results)
        planned = [int(r.get("index", i)) for i, r in enumerate(job.get("runs", []))]
        missing = [i for i in planned if have.get(i, {}).get("Result", 0) not in FINISHED]
        wall = now() - slot.started
        ok_done = any(d.get("ok") for d in done_lines(results))
        runs_this = job.get("_runsThisAttempt", 0)
        stat = {"t": now(), "jobId": job_id, "tag": job.get("tag"), "slot": slot.index, "attempt": job.get("attempt", 0),
                "mode": job.get("mode") or self.a.mode, "planned": runs_this,
                "finished": runs_this - len(missing), "wall": round(wall, 1), "exit": code,
                "reason": reason or ("ok" if not missing else "incomplete"),
                "peakRssMB": round(slot.peak_rss / 2**20), "peakPrivateMB": round(slot.peak_private / 2**20),
                "cpuMean": round(statistics.mean(slot.cpu_samples), 1) if slot.cpu_samples else None,
                "n": self.a.n}
        with open(self.farm.stats, "a", encoding="utf-8") as f:
            f.write(json.dumps(stat) + "\n")
        slot.proc = None
        slot.job = None
        (self.farm.running / f"{job_id}.player.json").unlink(missing_ok=True)
        job.pop("_runsThisAttempt", None)
        if not missing:
            self.finish(slot, job, path, "ok", reason)
            print(f"[farm] slot {slot.index}: {job_id} ok in {wall:.0f}s", flush=True)
        else:
            why = reason or (f"exit {code}" if code is not None else "incomplete") + ("" if ok_done else ", no ok done line")
            self.requeue(job, path, why)

    def requeue(self, job: dict, path: Path, reason: str, count_attempt: bool = True):
        job_id = job["jobId"]
        attempt = int(job.get("attempt", 0)) + (1 if count_attempt else 0)
        job.setdefault("errors", []).append(reason)
        if attempt >= self.a.max_attempts:
            have = read_results(self.farm.results_path(job_id))
            planned = [int(r.get("index", i)) for i, r in enumerate(job.get("runs", []))]
            missing = [r for r in job.get("runs", []) if have.get(int(r["index"]), {}).get("Result", 0) not in FINISHED]
            with open(self.farm.results_path(job_id), "a", encoding="utf-8") as f:
                for r in missing:
                    f.write(json.dumps({"JobId": job_id, "RunIndex": r["index"], "Difficulty": r.get("difficulty"),
                                        "Squad": ",".join(r.get("squad", [])), "Result": 0,
                                        "FarmError": reason}) + "\n")
            status = "failed" if len(missing) == len(planned) else "partial"
            job["attempt"] = attempt
            self.finish(None, job, path, status, reason)
            print(f"[farm] {job_id} gave up after {attempt} attempts ({status}): {reason}", flush=True)
            return
        job["attempt"] = attempt
        dst = self.farm.pending / f"{job_id}.json"
        write_json_atomic(dst, job)
        t0 = float(job.get("submittedAt", 0)) or 1.0
        os.utime(dst, (t0, t0))  # back to the front of the queue
        path.unlink(missing_ok=True)
        print(f"[farm] {job_id} requeued (attempt {attempt}): {reason}", flush=True)

    def finish(self, slot, job: dict, path: Path, status: str, reason: str):
        job_id = job["jobId"]
        have = read_results(self.farm.results_path(job_id))
        job["status"] = status
        job["completed"] = sum(1 for r in have.values() if r.get("Result", 0) in FINISHED)
        job["finishedAt"] = now()
        if reason:
            job["note"] = reason
        write_json_atomic(self.farm.done / f"{job_id}.json", job)
        path.unlink(missing_ok=True)

    # ---- loop -----------------------------------------------------------------------------------------------
    def write_status(self):
        st = {"t": now(), "pid": os.getpid(), "n": self.a.n, "mode": self.a.mode,
              "pending": sum(1 for _ in self.farm.pending.glob("*.json")),
              "slots": [{"slot": s.index, "job": s.job["jobId"] if s.job else None,
                         "pid": s.proc.pid if s.proc else None,
                         "age": round(now() - s.started) if s.job else None,
                         "lines": s.lines_seen if s.job else None,
                         "peakRssMB": round(s.peak_rss / 2**20) if s.job else None} for s in self.slots]}
        try:
            write_json_atomic(self.farm.status, st)
        except OSError:
            pass

    def run(self):
        self.recover_orphans()
        print(f"[farm] {self.a.n} slots, mode {self.a.mode}, exe {self.exe}, root {self.farm.root}", flush=True)
        signal.signal(signal.SIGINT, self._sigint)
        last_launch = 0.0
        idle_since = now()
        try:
            while True:
                if self.farm.stop_file.exists() and not self.stopping:
                    print("[farm] STOP file: finishing running jobs", flush=True)
                    self.stopping = True
                for s in self.slots:
                    if s.busy:
                        self.watch(s)
                for s in self.slots:
                    if not s.busy and not self.stopping and now() - last_launch >= self.a.stagger:
                        p = self.next_job()
                        if p is None:
                            break
                        self.launch(s, p)
                        last_launch = now()
                busy = any(s.busy for s in self.slots)
                if busy:
                    idle_since = now()
                if self.stopping and not busy:
                    break
                if self.a.once and not busy and self.next_job() is None:
                    break
                if self.a.idle_exit and not busy and now() - idle_since > self.a.idle_exit:
                    break
                self.write_status()
                time.sleep(self.a.poll)
        finally:
            for s in self.slots:
                if s.busy:  # killed with the runner: back to pending, not counted as an attempt
                    self.kill(s)
                    job, path = s.job, s.job_path
                    s.job = s.proc = None
                    (self.farm.running / f"{job['jobId']}.player.json").unlink(missing_ok=True)
                    job.pop("_runsThisAttempt", None)
                    self.requeue(job, path, "runner stopped", count_attempt=False)
            self.write_status()
            if self.farm.stop_file.exists():
                self.farm.stop_file.unlink(missing_ok=True)
        print("[farm] stopped", flush=True)

    def _sigint(self, *_):
        if self.stopping:
            raise KeyboardInterrupt
        print("[farm] Ctrl+C: finishing running jobs (Ctrl+C again to kill them)", flush=True)
        self.stopping = True


# ---- submit / status / stats / report -----------------------------------------------------------------------
def parse_plan(plan: str) -> list[tuple[int, list[str]]]:
    out = []
    for run in plan.split(";"):
        run = run.strip()
        if not run:
            continue
        d, squad = run.split(":")
        out.append((int(d), [v.strip() for v in squad.split(",")]))
    return out


def cmd_submit(a):
    farm = Farm(a.root).ensure()
    plan = parse_plan(a.plan)
    params = read_json(Path(a.params)) if a.params else None
    tag = a.tag or "manual"
    stamp = time.strftime("%m%d%H%M%S")
    index = 0
    for j in range(a.jobs):
        runs = []
        for k in range(a.runs_per_job):
            d, squad = plan[index % len(plan)]
            runs.append({"index": k, "difficulty": d, "squad": squad})
            index += 1
        job = {"jobId": f"{tag}-{stamp}-{j:04d}", "seed": a.seed + j * 100, "runs": runs, "params": params,
               "tag": tag}
        if a.mode:
            job["mode"] = a.mode
        if a.fixed_dt is not None:
            job["fixedDt"] = a.fixed_dt
        p = farm.submit(job)
        print(p)


def cmd_status(a):
    farm = Farm(a.root).ensure()
    counts = {k: sum(1 for p in getattr(farm, k).glob("*.json") if not p.name.endswith(".player.json"))
              for k in ("pending", "running", "done")}
    print(f"pending {counts['pending']}  running {counts['running']}  done {counts['done']}")
    if farm.status.exists():
        st = read_json(farm.status)
        print(f"runner pid {st.get('pid')} ({now() - st['t']:.0f}s ago), {st['n']} slots, mode {st['mode']}")
        for s in st["slots"]:
            print(f"  slot {s['slot']}: {s['job'] or '-'}  age {s['age']}s  lines {s['lines']}  peak {s['peakRssMB']} MB")


def cmd_stats(a):
    farm = Farm(a.root)
    rows = read_lines(farm.stats)
    if a.since_minutes:
        rows = [r for r in rows if r["t"] > now() - a.since_minutes * 60]
    if a.tag:
        rows = [r for r in rows if r.get("tag") == a.tag]
    if not rows:
        print("no attempts")
        return
    by = collections.defaultdict(list)
    for r in rows:
        by[(r.get("mode"), r.get("n"))].append(r)
    for (mode, n), rs in sorted(by.items(), key=lambda kv: str(kv[0])):
        fin = sum(r["finished"] for r in rs)
        t0 = min(r["t"] - r["wall"] for r in rs)
        t1 = max(r["t"] for r in rs)
        span = max(t1 - t0, 1)
        per_run = [r["wall"] / r["finished"] for r in rs if r["finished"]]
        fails = [r for r in rs if r["reason"] not in ("ok", "")]
        rss = [r["peakRssMB"] for r in rs if r.get("peakRssMB")]
        cpu = [r["cpuMean"] for r in rs if r.get("cpuMean") is not None]
        print(f"mode {mode}, {n} slots: {len(rs)} attempts, {fin} runs in {span / 60:.1f} min = "
              f"{fin / span * 3600:.0f} runs/h; per instance {statistics.mean(per_run) if per_run else 0:.0f}s/run "
              f"(incl. boot); peak RSS mean {statistics.mean(rss) if rss else 0:.0f} MB max {max(rss) if rss else 0} MB; "
              f"CPU mean {statistics.mean(cpu) if cpu else 0:.0f}% of a core; {len(fails)} failed attempts")
        for r in fails[-5:]:
            print(f"   fail {r['jobId']} a{r['attempt']}: {r['reason']}")


def cmd_report(a):
    farm = Farm(a.root)
    runs = []
    for p in sorted(farm.done.glob("*.json")):
        job = read_json(p)
        if a.tag and job.get("tag") != a.tag:
            continue
        runs += list(read_results(farm.results_path(job["jobId"])).values())
    by = collections.defaultdict(list)
    for r in runs:
        by[(r.get("Difficulty"), r.get("Squad"))].append(r)
    print(f"{'d':>2} {'squad':28} {'runs':>4} {'won':>4} {'fell':>4} {'none':>4} {'hull min':>8} {'kills':>6} "
          f"{'game s':>7} {'real s':>7}")
    for (d, sq), rs in sorted(by.items(), key=lambda kv: (kv[0][0] or 0, kv[0][1] or "")):
        fin = [r for r in rs if r.get("Result") in FINISHED]
        won = sum(1 for r in fin if r["Result"] == 1)
        fell = sum(1 for r in fin if r["Result"] == 2)
        low = statistics.mean(r.get("HullMinFraction", 0) for r in fin) if fin else 0
        kills = statistics.mean(r.get("Kills", 0) for r in fin) if fin else 0
        gs = statistics.mean(r.get("EndedAt", 0) - r.get("UnderwayAt", 0) for r in fin) if fin else 0
        rs_ = statistics.mean(r.get("RealSeconds", 0) for r in fin) if fin else 0
        print(f"{d:>2} {str(sq)[:28]:28} {len(rs):>4} {won:>4} {fell:>4} {len(rs) - len(fin):>4} {low:8.0%} "
              f"{kills:6.1f} {gs:7.0f} {rs_:7.0f}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run", help="run the farm")
    r.add_argument("--root", default=str(DEFAULT_ROOT))
    r.add_argument("--exe", default=None, help=f"the player (default <root>/build/{DEFAULT_EXE_NAME})")
    r.add_argument("-n", type=int, default=4, help="parallel instances")
    r.add_argument("--mode", choices=("nographics", "windowed"), default="nographics")
    r.add_argument("--width", type=int, default=640)
    r.add_argument("--height", type=int, default=360)
    r.add_argument("--fixed-dt", type=float, default=1 / 30, help="game seconds per frame (0 = real time)")
    r.add_argument("--time-scale", type=float, default=1.0)
    r.add_argument("--job-workers", type=int, default=1, help="-job-worker-count per player (0 = Unity's default)")
    r.add_argument("--affinity", type=int, default=0, help="logical CPUs pinned per slot (needs psutil; 0 = none)")
    r.add_argument("--below-normal", type=int, default=1, help="run players at below-normal priority (1/0)")
    r.add_argument("--boot-timeout", type=float, default=300)
    r.add_argument("--run-timeout", type=float, default=600, help="seconds without a new result line")
    r.add_argument("--stall", type=float, default=240, help="seconds without the log growing")
    r.add_argument("--quit-grace", type=float, default=30)
    r.add_argument("--max-attempts", type=int, default=3)
    r.add_argument("--stagger", type=float, default=5, help="seconds between launches")
    r.add_argument("--poll", type=float, default=2)
    r.add_argument("--once", action="store_true", help="exit when the queue is empty and every slot idle")
    r.add_argument("--idle-exit", type=float, default=0, help="exit after this many idle seconds (0 = never)")
    r.add_argument("--extra", nargs=argparse.REMAINDER, help="more player arguments (last)")

    s = sub.add_parser("submit", help="queue plan jobs")
    s.add_argument("--root", default=str(DEFAULT_ROOT))
    s.add_argument("--plan", required=True, help='"d:V1,V2,V3;d:..." cycled over the runs')
    s.add_argument("--jobs", type=int, default=1)
    s.add_argument("--runs-per-job", type=int, default=1)
    s.add_argument("--seed", type=int, default=1000)
    s.add_argument("--params", default=None, help="a flat params JSON for -arkEscortParams")
    s.add_argument("--tag", default=None)
    s.add_argument("--mode", choices=("nographics", "windowed"), default=None, help="override the runner's mode")
    s.add_argument("--fixed-dt", type=float, default=None)

    for name, fn in (("status", cmd_status), ("stats", cmd_stats), ("report", cmd_report)):
        p = sub.add_parser(name)
        p.add_argument("--root", default=str(DEFAULT_ROOT))
        p.add_argument("--tag", default=None)
        p.add_argument("--since-minutes", type=float, default=0)
        p.set_defaults(fn=fn)

    a = ap.parse_args()
    if a.cmd == "run":
        Runner(a).run()
    elif a.cmd == "submit":
        cmd_submit(a)
    else:
        a.fn(a)


if __name__ == "__main__":
    main()
