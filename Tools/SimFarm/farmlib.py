"""farmlib.py - the sim farm's job folder: layout, atomic file moves, job files and result reading.

Shared by farm.py (the runner that owns the player processes) and coevo.py (the co-evolution driver that only writes
jobs and reads results). Pure stdlib.

Layout under the farm root (default D:/VoyageFarm):

    build/CosmicShoreSimFarm.exe     the player (SimFarmBuild)
    jobs/pending/<jobId>.json        queued; the runner takes the oldest
    jobs/running/<jobId>.json        owned by a slot (left behind if the runner dies; `farm.py run` requeues them)
    jobs/done/<jobId>.json           finished: the job plus "status" ("ok" | "partial" | "failed") and "completed"
    results/<jobId>.jsonl            the player's lines (ArkEscortSim), appended across attempts
    params/<jobId>.json              the job's params, passed as -arkEscortParams (a flat dict of named keys)
    logs/<jobId>.a<attempt>.log      the player's -logFile
    stats.jsonl                      one line per attempt: wall time, runs, peak memory, exit
    farm_status.json                 the runner's slots, rewritten every few seconds
    STOP                             create it to make the runner finish its running jobs and exit

A job file:
    {"jobId": "smoke-0001", "seed": 1000,
     "runs": [{"index": 0, "difficulty": 1, "squad": ["Sparrow", "Sparrow", "Sparrow"]}],
     "params": {...} | null,          # opaque flat dict, written to params/<jobId>.json for -arkEscortParams
     "tag": "smoke", "meta": {...},   # the submitter's own; the runner never reads them
     "attempt": 0, "submittedAt": 1730000000.0}
"""
from __future__ import annotations

import json
import os
import time
from pathlib import Path

DEFAULT_ROOT = Path(os.environ.get("VOYAGE_FARM_ROOT", r"D:\VoyageFarm"))
DEFAULT_EXE_NAME = "CosmicShoreSimFarm.exe"
OUTCOMES = {0: "none", 1: "survived", 2: "fallen", 3: "abandoned"}
FINISHED = (1, 2, 3)  # a run with one of these Results needs no retry


class Farm:
    def __init__(self, root: str | os.PathLike = DEFAULT_ROOT):
        self.root = Path(root)
        self.pending = self.root / "jobs" / "pending"
        self.running = self.root / "jobs" / "running"
        self.done = self.root / "jobs" / "done"
        self.results = self.root / "results"
        self.params = self.root / "params"
        self.logs = self.root / "logs"
        self.stats = self.root / "stats.jsonl"
        self.status = self.root / "farm_status.json"
        self.stop_file = self.root / "STOP"

    def ensure(self) -> "Farm":
        for d in (self.pending, self.running, self.done, self.results, self.params, self.logs):
            d.mkdir(parents=True, exist_ok=True)
        return self

    # ---- jobs -------------------------------------------------------------------------------------------------
    def submit(self, job: dict) -> Path:
        """Queue a job (atomically: written beside, then renamed into pending/)."""
        self.ensure()
        job = dict(job)
        job.setdefault("attempt", 0)
        job.setdefault("submittedAt", time.time())
        path = self.pending / f"{job['jobId']}.json"
        write_json_atomic(path, job)
        return path

    def job_state(self, job_id: str) -> str | None:
        for name, d in (("done", self.done), ("running", self.running), ("pending", self.pending)):
            if (d / f"{job_id}.json").exists():
                return name
        return None

    def done_job(self, job_id: str) -> dict | None:
        p = self.done / f"{job_id}.json"
        return read_json(p) if p.exists() else None

    def results_path(self, job_id: str) -> Path:
        return self.results / f"{job_id}.jsonl"

    def read_results(self, job_id: str) -> dict[int, dict]:
        """RunIndex -> the run's line. A finished result beats a None one; the later line wins between equals."""
        return read_results(self.results_path(job_id))


def read_json(path: Path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def write_json_atomic(path: Path, obj) -> None:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + f".tmp{os.getpid()}")
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(obj, f, indent=1)
    replace_retry(tmp, path)


def replace_retry(src: Path, dst: Path, tries: int = 20) -> None:
    """os.replace, retried: on Windows a reader holding the file open makes the rename fail for a moment."""
    for i in range(tries):
        try:
            os.replace(src, dst)
            return
        except PermissionError:
            if i == tries - 1:
                raise
            time.sleep(0.1)


def read_lines(path: Path) -> list[dict]:
    out = []
    if not Path(path).exists():
        return out
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                out.append(json.loads(line))
            except json.JSONDecodeError:
                pass  # a line cut by a kill mid-write
    return out


def read_results(path: Path) -> dict[int, dict]:
    best: dict[int, dict] = {}
    for rec in read_lines(path):
        if rec.get("done") or "RunIndex" not in rec:
            continue
        i = int(rec["RunIndex"])
        old = best.get(i)
        if old is None or rec.get("Result", 0) in FINISHED or old.get("Result", 0) not in FINISHED:
            best[i] = rec
    return best


def done_lines(path: Path) -> list[dict]:
    return [r for r in read_lines(path) if r.get("done")]
