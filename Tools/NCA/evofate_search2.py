"""CMA-ES around C2 with the FULL yardstick as fitness: swarm_eval.evaluate (16 transitions, 3 samples,
loss-8 bar) on one fresh seed per generation + the official swarm_feel at that seed.

fitness = passed + 0.5 * mean over own plans of max(0, (8 - loss) / 8)
          - 3 * (organic band violations, graded: osc over 0.07, planar excess over 0.13, jerk_rel over 2.45)

    python Tools/NCA/evofate_search2.py --run runs/evofate/cma3 --pop 8 --hours 1.2
"""
import argparse
import json
import os
import pickle
import sys
import time
from dataclasses import asdict
from multiprocessing import Pool

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_eval as se  # noqa: E402
import swarm_feel as sf  # noqa: E402
import swarm_nca as sn  # noqa: E402
import evofate_model as ef  # noqa: E402

BASE = json.load(open(os.path.join(HERE, "runs", "evofate", "C2.json")))
GENES = [("adh", 0.0, 1.2, False), ("te0", 0.0, 1.0, False), ("tmix", 0.3, 1.2, False), ("pull", 0.5, 4.0, True),
         ("e0", 0.3, 4.0, True), ("cov_scale", 0.4, 1.6, True), ("k_well", 0.1, 1.5, True), ("well_clip", 0.2, 1.5, True)]


def cfg_from_vec(z):
    d = asdict(ef.FateCfg(**BASE))
    for (n, lo, hi, lg), v in zip(GENES, z):
        b = d[n]
        val = (np.exp(np.log(max(b, 1e-6)) + 0.5 * v) if lg else b + 0.15 * (hi - lo) * v)
        d[n] = float(min(hi, max(lo, val)))
    return ef.FateCfg(**d)


def _eval(args):
    z, seed = args
    torch.set_num_threads(1)
    cfg = cfg_from_vec(z)
    r = se.evaluate(ef.EvoFate(cfg), seed=seed, samples=3, full=True, log=lambda *a: None)
    f = sf.feel(ef.EvoFate(cfg), seed=seed)
    m = f["mean"]; pe = sf.planar_excess(f)
    own = [r["own"][k]["cross"][k] for k in sn.KINDS]
    viol = 10 * max(0.0, m["osc"] - 0.07) + 4 * max(0.0, pe - 0.13) + 2 * max(0.0, m["jerk_rel"] - 2.45)
    fit = r["passed"] + 0.5 * float(np.mean([max(0.0, (8 - x) / 8) for x in own])) - 3 * viol
    return fit, r["passed"], own, dict(osc=m["osc"], pe=round(pe, 3), jr=m["jerk_rel"], band=sf.in_band(f)[0])


def main():
    import cma
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True); ap.add_argument("--pop", type=int, default=8)
    ap.add_argument("--sigma", type=float, default=0.5); ap.add_argument("--hours", type=float, default=1.0)
    a = ap.parse_args()
    os.makedirs(a.run, exist_ok=True)
    sp = os.path.join(a.run, "state.pkl")
    if os.path.isfile(sp):
        st = pickle.load(open(sp, "rb")); es, g0 = st["es"], st["gen"]
    else:
        es = cma.CMAEvolutionStrategy(np.zeros(len(GENES)), a.sigma, {"popsize": a.pop, "seed": 11, "verbose": -9}); g0 = 0
    log = open(os.path.join(a.run, "log.jsonl"), "a"); t_end = time.time() + a.hours * 3600
    with Pool(4) as pool:
        gen = g0
        while time.time() < t_end:
            t0 = time.time(); X = es.ask(); seed = 4000 + gen
            R = pool.map(_eval, [(x, seed) for x in X])
            fit = [r[0] for r in R]
            es.tell(X, [-f for f in fit])
            i = int(np.argmax(fit))
            rec = dict(gen=gen, seed=seed, fmax=round(fit[i], 3), fmean=round(float(np.mean(fit)), 3), best_passed=R[i][1],
                       best_own=[round(x, 2) for x in R[i][2]], best_feel=R[i][3],
                       best_cfg={n: round(getattr(cfg_from_vec(X[i]), n), 4) for n, *_ in GENES},
                       mean_cfg={n: round(getattr(cfg_from_vec(es.mean), n), 4) for n, *_ in GENES},
                       all=[(round(r[0], 2), r[1], r[3]["band"]) for r in R], sec=round(time.time() - t0))
            np.save(os.path.join(a.run, "mean.npy"), es.mean); np.save(os.path.join(a.run, f"best_g{gen}.npy"), X[i])
            log.write(json.dumps(rec) + "\n"); log.flush(); print(json.dumps(rec), flush=True)
            gen += 1
            pickle.dump(dict(es=es, gen=gen), open(sp + ".tmp", "wb")); os.replace(sp + ".tmp", sp)


if __name__ == "__main__":
    main()
