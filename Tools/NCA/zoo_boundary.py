"""How much of the zoo genome box still passes? Uniform-random genomes (not mutants of a passing
creature), evaluated on swarm_eval (1 sample, fail-fast gates), to measure how often a random
personality still grows and switches correctly - i.e. whether the yardstick constrains motion at all.

    python Tools/NCA/zoo_boundary.py --n 48  -> results/zoo/boundary.json
"""
import argparse
import json
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def _one(g):
    import torch
    torch.set_num_threads(1)
    import swarm_eval as se
    import zoo_model as zm
    r = se.evaluate(zm.make(g), samples=1, gate=(4, 8), log=lambda *a: None)
    fails = [k for k, v in r["own"].items() if not v["ok"]] + [k for k, v in r["switch"].items() if not v["ok"]]
    return dict(g=g, passed=r["passed"], feasible=r["feasible"], tiers=r["tiers_run"],
                ok=bool(r["complete"] and r["passed"] == r["feasible"] and r["feasible"] >= 13), fails=fails)


def main():
    import zoo_model as zm
    ap = argparse.ArgumentParser()
    ap.add_argument("--n", type=int, default=48)
    a = ap.parse_args()
    rng = np.random.default_rng(123)
    gs = [zm.from_unit(rng.random(len(zm.NAMES))) for _ in range(a.n)]
    t0 = time.time()
    with ProcessPoolExecutor(4) as ex:
        res = list(ex.map(_one, gs))
    ok = sum(r["ok"] for r in res)
    fails = {}
    for r in res:
        for f in r["fails"]:
            fails[f] = fails.get(f, 0) + 1
    out = dict(n=a.n, ok=ok, rate=round(ok / a.n, 3), fails=fails, seconds=round(time.time() - t0), runs=res)
    json.dump(out, open(os.path.join(HERE, "results", "zoo", "boundary.json"), "w"), indent=1)
    print(f"{ok}/{a.n} uniform-random genomes pass every feasible test; failing tests: {fails}")
    # which genes separate pass from fail?
    U = np.array([zm.to_unit(r["g"]) for r in res]); y = np.array([r["ok"] for r in res])
    if 0 < y.sum() < len(y):
        d = U[y].mean(0) - U[~y].mean(0)
        for i in np.argsort(-np.abs(d))[:8]:
            print(f"  {zm.NAMES[i]:15s} pass-mean minus fail-mean (unit) {d[i]:+.2f}")


if __name__ == "__main__":
    main()
