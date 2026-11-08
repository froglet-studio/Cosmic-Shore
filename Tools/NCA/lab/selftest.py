#!/usr/bin/env python3
"""
selftest.py -- the NCA Ecology Lab's Compute bridge, checked offline.

    1. the diamond optimizer on its toy problem: it grows past the answer, then compresses to it (no torch)
    2. diamond_nca with a synthetic score: finishes, shows the diamond, and a run stopped half way and resumed from
       evals.json ends exactly where an unbroken run ends, re-running no cached evaluation (no torch)
    3. the Notion side, dry: the runner's metric line, Kill dropped and Prioritize first (negative control: a Kill row
       that survives fails), push writes Running once then Done with the GIF, steer keeps an existing unit's id
    4. the job files: they validate under Froglet Compute's own rules, are on a declared plan, are not runnable
       (Compute is paused), their argv names files that exist, and what they resume from is relayed
    5. with torch: the trainer is interrupted by SIGINT, exits 0 with a checkpoint, and resumes to the end
    6. with torch: Froglet Compute's own worker (worker.command_session) runs the A1 job's argv on a tiny config,
       is sent away by a fake "person came back", and a second session resumes and finishes the unit

    python3 Tools/NCA/lab/selftest.py            # 1-4 always; 5-6 when torch imports
"""
from __future__ import annotations

import io
import json
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time
from contextlib import redirect_stdout

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path[:0] = [HERE, os.path.join(REPO, "Tools", "Compute")]

import pareto_complexity as P  # noqa: E402
import diamond_nca as D  # noqa: E402
import notion_writeback as N  # noqa: E402

JOBS = ("nca-lab-nca3d", "nca-lab-diamond")


def _quiet(fn, *a, **k):
    with redirect_stdout(io.StringIO()):
        return fn(*a, **k)


def diamond_toy():
    prob = P.FeatureSelectionDemo(60, 12, seed=1)
    ctrl = P.DiamondController(prob, P.DiamondConfig(generations=200, children=8, patience=15, seed=0))
    ctrl.run()
    peak = max(r["cur_complexity"] for r in ctrl.history)
    final = ctrl.archive.simplest_at_or_above(ctrl.peak_score)
    if not (final and final.score == 12 and final.complexity == 12 and peak > 12):
        return [f"diamond toy: final {final and (final.score, final.complexity)}, peak {peak} (want 12/12, peak > 12)"]
    return []


def diamond_resume(tmp):
    f = []
    whole, half = os.path.join(tmp, "whole"), os.path.join(tmp, "half")
    cfg = dict(D.DEFAULTS, generations=16)
    r1 = _quiet(D.search, cfg, whole, stub=True)
    _quiet(D.search, dict(cfg, generations=7), half, stub=True)        # "stopped" after 7 generations
    n_before = len(json.load(open(os.path.join(half, "evals.json"))))
    prob = D.NcaCapacity(cfg, half, stub=True)
    ctrl = D.Controller(prob, P.DiamondConfig(generations=16, children=cfg["children"], patience=cfg["patience"],
                                              seed=cfg["seed"]), half, 16)
    _quiet(ctrl.run)
    r2 = _quiet(D.search, cfg, half, stub=True)
    if r1["metrics"]["params"] != r2["metrics"]["params"] or r1["metrics"]["evals"] != r2["metrics"]["evals"]:
        f.append(f"diamond resume: whole {r1['metrics']} != resumed {r2['metrics']}")
    if prob.fresh != r1["metrics"]["evals"] - n_before:
        f.append(f"diamond resume: re-ran cached evaluations ({prob.fresh} fresh, {n_before} were cached)")
    if not r1["metrics"]["diamond"]:
        f.append("diamond_nca stub: no diamond (grow then compress) observed")
    return f


ROWS = [
    {"page_id": "a" * 32, "name": "A1 — sphere", "priority": "P0", "status": "Queued", "your_call": "",
     "created": "2026-10-16T15:53:24Z", "config": "job nca3d | target sphere | iters 4000"},
    {"page_id": "b" * 32, "name": "A2 — regen", "priority": "P0", "status": "Queued", "your_call": "Kill",
     "created": "2026-10-16T15:53:24Z", "config": "job nca3d | target sphere | damage_prob 0.5"},
    {"page_id": "c" * 32, "name": "A6 — gyroid", "priority": "P1", "status": "Queued", "your_call": "Prioritize",
     "created": "2026-10-16T15:53:25Z", "config": "job nca3d | target gyroid"},
    {"page_id": "d" * 32, "name": "OPT1 — optimizer", "priority": "P0", "status": "Done", "your_call": "",
     "created": "2026-10-16T15:53:24Z", "config": "module pareto_complexity.py"},
    {"page_id": "e" * 32, "name": "A4 — capacity", "priority": "P1", "status": "Queued", "your_call": "",
     "created": "2026-10-16T15:53:24Z", "config": "job diamond_nca | complexity params"},
]


def notion_dry(tmp):
    f = []
    line = N.fmt_metrics({"params": 12, "best_iou": 0.9, "device": "cuda", "history": [1]})
    if line != "best_iou=0.9 | params=12 | device=cuda":
        f.append(f"metric line: {line!r}")
    order = [r["name"].split()[0] for r in N.row_order(ROWS)]
    if order != ["A6", "A1", "A4"]:
        f.append(f"row order {order} (want A6 Prioritize, A1 P0, A4 P1; A2 Killed; OPT1 Done)")
    if "A2" in order:
        f.append("negative control: a Killed row survived")
    jobs = {"j": {"id": "j", "lab_job": "nca3d", "command": {"units": [
        {"id": "lab-a1", "args": {"notion_page": "a" * 32, "row": "A1", "config": "old"}}]}}}
    units = N.steer(ROWS, jobs)["j"]
    if [u["id"] for u in units] != ["lab-a6", "lab-a1"] or units[1]["args"]["config"] != ROWS[0]["config"]:
        f.append(f"steer: {[(u['id'], u['args'].get('config')) for u in units]}")
    notion = N.Notion(apply=False)
    unit = {"id": "lab-a1", "args": {"notion_page": "a" * 32}}
    job = {"id": "nca-lab-nca3d"}
    gif = b"GIF89a fake"
    res = json.dumps({"metrics": {"best_iou": 0.81, "params": 12304}}).encode()
    os.environ["HOME"] = tmp       # the GIF lands under ~/.froglet-compute: keep it in the scratch folder
    state = {}
    _quiet(N.push, notion, [(job, unit, {"progress": 0.4, "done": False, "machine": "u/h"}, {})], state)
    _quiet(N.push, notion, [(job, unit, {"progress": 0.6, "done": False, "machine": "u/h"}, {})], state)
    _quiet(N.push, notion, [(job, unit, {"progress": 1.0, "done": True, "machine": "u/h"},
                             {"result.json": res, "growth.gif": gif})], state)
    _quiet(N.push, notion, [(job, unit, {"progress": 1.0, "done": True, "machine": "u/h"},
                             {"result.json": res, "growth.gif": gif})], state)
    kinds = [(m, p.split("/")[1]) for m, p, _ in notion.sent]
    want = [("PATCH", "pages"), ("PATCH", "pages"), ("POST", "file_uploads"), ("POST", ""), ("PATCH", "blocks")]
    if len(kinds) != 5 or kinds[0] != want[0] or kinds[-1] != want[-1]:
        f.append(f"push requests: {kinds}")
    statuses = [b["properties"]["Status"]["status"]["name"] for m, p, b in notion.sent if p.startswith("/pages/")]
    if statuses != ["Running", "Done"]:
        f.append(f"push wrote statuses {statuses} (want Running once, then Done once)")
    done_text = notion.sent[1][2]["properties"]["Result"]["rich_text"][0]["text"]["content"]
    if not done_text.startswith("best_iou=0.81 | params=12304 | via Froglet Compute (u/h"):
        f.append(f"Result text: {done_text!r}")
    return f


def job_files():
    import compute_lib as L
    f = []
    plan = L.load_ai_plan(local=True)
    for jid in JOBS:
        j = json.load(open(os.path.join(REPO, "Docs", "Compute", "jobs", jid + ".json")))
        p = L.validate_job(j) + ([L.plan_problem(j, plan)] if L.plan_problem(j, plan) else [])
        if j["status"] in L.RUNNABLE:
            p.append("is runnable, but Froglet Compute is paused (Shombith 2026-11-06): keep it proposed")
        c = j["command"]
        script = c["argv"][1]
        if not os.path.isfile(os.path.join(REPO, script)):
            p.append(f"argv names {script}, which does not exist")
        if not set(c["resume_if"]) <= set(c["keep"]):
            p.append(f"resumes from {c['resume_if']} but keeps only {c['keep']}: a relayed unit could not resume")
        for u in c["units"]:
            if "notion_page" not in u["args"] or "config" not in u["args"]:
                p.append(f"unit {u['id']} lacks notion_page/config")
        if "cuda" not in j.get("requires", []):
            p.append("does not require cuda: a CPU-only computer would take a GPU-days unit")
        f += [f"job {jid}: {x}" for x in p]
    return f


def trainer_interrupt(tmp, py):
    out = os.path.join(tmp, "a1")
    cmd = [py, os.path.join(HERE, "nca3d_trainer.py"), "--out", out, "--device", "cpu", "--ckpt-every", "1000",
           "--config", "job nca3d | target sphere | size 12 | channels 8 | hidden 16 | iters 60 | batch 2 | "
                       "pool_size 8 | steps_min 8 | steps_max 12"]
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    t0 = time.time()
    while time.time() - t0 < 120:
        st = _read(os.path.join(out, "status.json"))
        if st and st.get("step", 0) >= 20:
            break
        time.sleep(0.2)
    proc.send_signal(signal.SIGINT)
    proc.wait(timeout=60)
    f = []
    st = _read(os.path.join(out, "status.json")) or {}
    if proc.returncode != 0:
        f.append(f"trainer: an interrupt exited {proc.returncode} (the worker would call it failed)")
    if st.get("phase") != "interrupted" or not os.path.isfile(os.path.join(out, "ckpt.pt")):
        f.append(f"trainer: no checkpoint on interrupt (status {st})")
    at = st.get("step", 0)
    r = subprocess.run(cmd + ["--resume"], capture_output=True, text=True, timeout=600)
    st = _read(os.path.join(out, "status.json")) or {}
    if r.returncode != 0 or st.get("phase") != "done" or '"resumed_at": ' + str(at) not in r.stdout:
        f.append(f"trainer: resume from {at} did not finish ({r.returncode}, {st}, {r.stdout[-300:]})")
    if not os.path.isfile(os.path.join(out, "result.json")):
        f.append("trainer: no result.json")
    return f


class _Away:
    """A person who is away for `seconds`, then comes back (Presence's interface, worker.py)."""
    def __init__(self, seconds):
        self.until = time.time() + seconds

    def back(self):
        return self.until is not None and time.time() > self.until


def worker_dry_run(tmp, py):
    """Froglet Compute's own command_session, on this checkout, with nothing published and no agent installed."""
    os.environ["FROGLET_COMPUTE_HOME"] = os.path.join(tmp, "home")
    import importlib
    import compute_lib as L
    importlib.reload(L)
    import worker as W
    importlib.reload(W)
    job = json.load(open(os.path.join(REPO, "Docs", "Compute", "jobs", "nca-lab-nca3d.json")))
    job["command"]["units"][0]["args"]["config"] = (
        "job nca3d | target sphere | size 12 | channels 8 | hidden 16 | iters 120 | batch 2 | pool_size 8 | "
        "steps_min 8 | steps_max 12")
    job["command"]["argv"] = [a if a != "--require-gpu" else "--device=cpu" for a in job["command"]["argv"]]
    W.sys.executable = py          # {python}: the interpreter with torch
    real_sleep = time.sleep
    time.sleep = lambda s: real_sleep(min(s, 1))     # the worker polls every 15 s; 1 s is enough here
    try:
        return _worker_sessions(W, L, job)
    finally:
        time.sleep = real_sleep


def _worker_sessions(W, L, job):
    f = []
    out1 = W.command_session(job, REPO, "selftest", ("selftest", "box"), [], _Away(6), 30, 1, quiet=True)
    folder = os.path.join(L.results_folder("selftest", "box"), "jobs", job["id"], out1["epoch"])
    u1 = _read(os.path.join(folder, "unit-lab-a1.json")) or {}
    if out1.get("stopped") != "the person came back" or u1.get("done") or not 0 < u1.get("progress", 0) < 1:
        f.append(f"worker: first session should stop part-way for the person ({out1}, {u1})")
    if "ckpt.pt" not in u1.get("files", []):
        f.append(f"worker: the checkpoint was not kept for the relay ({u1.get('files')})")
    out2 = W.command_session(job, REPO, "selftest", ("selftest", "box"), [], _Away(600), 30, 1, quiet=True)
    u2 = _read(os.path.join(folder, "unit-lab-a1.json")) or {}
    log = open(os.path.join(L.cache_dir(), "runs", job["id"], "lab-a1", "worker.log")).read()
    if out2.get("stopped") != "finished" or not u2.get("done") or "resumed_at" not in log:
        f.append(f"worker: second session should resume and finish ({out2}, {u2})")
    if not {"result.json", "growth.gif", "model.pt"} <= set(u2.get("files", [])):
        f.append(f"worker: finished unit kept {u2.get('files')}")
    print(f"  worker dry run: session 1 stopped at {u1.get('progress', 0):.2f} ({out1.get('stopped')}), "
          f"session 2 {out2.get('stopped')}, score {out2.get('best')}")
    return f


def _read(p):
    try:
        return json.load(open(p))
    except (OSError, ValueError):
        return None


def main():
    py = os.environ.get("NCA_LAB_PYTHON", sys.executable)
    has_torch = subprocess.run([py, "-c", "import torch"], capture_output=True).returncode == 0
    tmp = tempfile.mkdtemp(prefix="nca-lab-selftest-")
    home0 = os.environ.get("HOME")
    failures = []
    try:
        steps = [("diamond optimizer (toy)", diamond_toy, ()),
                 ("diamond_nca resume (stub score)", diamond_resume, (os.path.join(tmp, "d"),)),
                 ("notion writeback + steer (dry)", notion_dry, (tmp,)),
                 ("job files", job_files, ())]
        if has_torch:
            steps += [("trainer interrupt + resume (torch, cpu)", trainer_interrupt, (tmp, py)),
                      ("froglet compute worker dry run (torch, cpu)", worker_dry_run, (tmp, py))]
        for name, fn, args in steps:
            t = time.time()
            f = fn(*args)
            if home0:
                os.environ["HOME"] = home0
            print(f"{name}: " + ("ok" if not f else "FAILED\n  " + "\n  ".join(f)) + f"  ({time.time() - t:.1f}s)")
            failures += f
        if not has_torch:
            print("trainer + worker dry run: SKIPPED (no torch for " + py + "; set NCA_LAB_PYTHON)")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print("\nnca lab self-test: " + ("ok" if not failures else f"FAILED ({len(failures)})"))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
