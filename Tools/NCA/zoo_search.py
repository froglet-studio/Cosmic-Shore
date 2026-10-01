"""MAP-Elites over the zoo genome (zoo_model.GENES): a map of distinct creatures that all still pass.

Every candidate is a mutant (or crossover) of an elite. It is evaluated on the round-2 yardstick
(swarm_eval.evaluate, 1 sample per test, fail-fast gates (4, 8): a candidate failing an own plan or a
standard switch costs a third of a full evaluation) and kept only if it passes EVERY feasible test
(passed == feasible >= 13). Survivors are described (zoo_probe.describe) and binned on three
player-facing axes; within a cell the crisper body wins (lower mean wanted divergence).

Every evaluated candidate is appended to runs/zoo/log.jsonl, so the map can be re-binned later and the
search resumes from the log.

    python Tools/NCA/zoo_search.py --hours 6.5 --workers 4
"""
from __future__ import annotations

import argparse
import json
import math
import os
import random
import sys
import time
from concurrent.futures import ProcessPoolExecutor, FIRST_COMPLETED, wait

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
RUN = os.path.join(HERE, "runs", "zoo")
LOG = os.path.join(RUN, "log.jsonl")

# the map: stance (flee <-> mob), liveliness (idle speed), drama (switch spectacle)
AXES = {
    "stance": [-1.5, -0.5, 0.25, 1.0, 2.0],      # log2 crowd ratio vs an inert body
    "live": [0.3, 0.45, 0.6, 0.8],               # voxels / step at rest
    "drama": [1.1, 1.25, 1.45, 1.75],            # peak radius during a switch / grown radius
}


def cell(d):
    return tuple(int(np.searchsorted(AXES[a], d[a])) for a in AXES)


def _init_worker():
    import torch
    torch.set_num_threads(1)


def evaluate_genome(g):
    """Worker: yardstick (fail-fast), then descriptors for survivors."""
    import torch
    torch.set_num_threads(1)
    import swarm_eval as se
    import swarm_nca as sn
    import zoo_model as zm
    import zoo_probe as zp
    t0 = time.time()
    try:
        r = se.evaluate(zm.make(g), samples=1, gate=(4, 8), log=lambda *a: None)
    except Exception as ex:  # a degenerate genome must not kill the search
        return dict(g=g, ok=False, err=repr(ex), sec=round(time.time() - t0, 1))
    ok = bool(r["complete"] and r["passed"] == r["feasible"] and r["feasible"] >= 13)
    rec = dict(g=g, ok=ok, passed=r["passed"], feasible=r["feasible"], tiers=r["tiers_run"])
    if ok:
        div = [r["own"][k]["cross"][k] for k in sn.KINDS] + [v["cross"][k.split("->")[1]] for k, v in r["switch"].items()]
        rec["div"] = round(float(np.mean(div)), 3)
        d = zp.describe(g)
        rec["desc"] = {k: v for k, v in d.items() if k != "per"}
        rec["per"] = d["per"]
    rec["sec"] = round(time.time() - t0, 1)
    return rec


def mutate(u, rng, sigma):
    import zoo_model as zm
    u = u.copy()
    for i, k in enumerate(zm.NAMES):
        kind = zm.GENES[k][2]
        if kind == "b":
            if rng.random() < 0.12:
                u[i] = 1.0 - round(u[i])
        elif rng.random() < 0.3:
            u[i] = u[i] + rng.normal() * sigma
    return np.clip(u, 0, 1)


def load_log():
    recs = []
    if os.path.exists(LOG):
        for line in open(LOG):
            try:
                recs.append(json.loads(line))
            except json.JSONDecodeError:
                pass
    return recs


def build_map(recs):
    elites = {}
    for r in recs:
        if not r.get("ok"):
            continue
        c = cell(r["desc"])
        if c not in elites or r["div"] < elites[c]["div"]:
            elites[c] = r
    return elites


def main():
    import zoo_model as zm
    ap = argparse.ArgumentParser()
    ap.add_argument("--hours", type=float, default=6.0)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()
    os.makedirs(RUN, exist_ok=True)
    rng = np.random.default_rng(a.seed + len(load_log()))
    recs = load_log()
    elites = build_map(recs)
    deadline = time.time() + a.hours * 3600
    base = zm.to_unit(zm.field_genome())

    first = [not recs]

    def candidate():
        if first[0]:
            first[0] = False
            return zm.field_genome()
        if len(elites) < 4 or rng.random() < 0.08:
            # bootstrap / immigrants: big jumps from field itself, behaviour bits random
            u = mutate(base, rng, 0.35)
            for i, k in enumerate(zm.NAMES):
                if zm.GENES[k][2] == "b":
                    u[i] = float(rng.random() < 0.5)
            return zm.from_unit(u)
        pool = list(elites.values())
        p1 = pool[rng.integers(len(pool))]
        u = zm.to_unit(p1["g"])
        if rng.random() < 0.25 and len(pool) > 1:
            p2 = pool[rng.integers(len(pool))]
            u2 = zm.to_unit(p2["g"])
            mask = rng.random(len(u)) < 0.5
            u = np.where(mask, u, u2)
        sigma = float(rng.choice([0.06, 0.12, 0.25]))
        return zm.from_unit(mutate(u, rng, sigma))

    n_eval = len(recs)
    t_start = time.time()
    with ProcessPoolExecutor(a.workers, initializer=_init_worker) as ex:
        futs = {ex.submit(evaluate_genome, candidate()) for _ in range(a.workers * 2)}
        while futs:
            done, futs = wait(futs, return_when=FIRST_COMPLETED)
            for f in done:
                rec = f.result()
                rec["t"] = round(time.time(), 1)
                recs.append(rec); n_eval += 1
                with open(LOG, "a") as fh:
                    fh.write(json.dumps(rec) + "\n")
                if rec.get("ok"):
                    c = cell(rec["desc"])
                    new = c not in elites
                    if new or rec["div"] < elites[c]["div"]:
                        elites[c] = rec
                    print(f"[{n_eval}] ok cell={c} {'NEW' if new else ''} div={rec['div']} desc="
                          f"{ {k: rec['desc'][k] for k in ('stance', 'live', 'drama', 'heal', 'loose')} } "
                          f"map={len(elites)} {rec['sec']}s", flush=True)
                else:
                    print(f"[{n_eval}] reject {rec.get('passed')}/{rec.get('feasible')} tiers={rec.get('tiers')} "
                          f"{rec.get('err', '')} {rec['sec']}s", flush=True)
                if time.time() < deadline:
                    futs.add(ex.submit(evaluate_genome, candidate()))
    ok = sum(r.get("ok", False) for r in recs)
    print(f"done: {n_eval} evaluated, {ok} valid, map {len(elites)} cells, {(time.time() - t_start) / 60:.0f} min")


if __name__ == "__main__":
    main()
