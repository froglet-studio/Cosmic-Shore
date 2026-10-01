"""CMA-ES for sortfeel: sort's 17 genes + 3 feel genes (well_dead, wander, wander_tau), started at the
hand-picked candidate (results/sortfeel/params.json), fitness = sort_search.fitness over all 16
transitions MINUS a penalty for leaving the organic band (swarm_feel, same seed, with margins:
planar_excess <= 0.10, osc <= 0.06, stuck <= 0.015, jerk_rel in [0.3, 2.2]).

    python Tools/NCA/sortfeel_search.py --run runs/sortfeel/cma --pop 8 --gens 30 --hours 1.2
"""
import argparse, json, os, pickle, sys, time
from dataclasses import asdict
from multiprocessing import Pool
import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_feel as sf  # noqa: E402
import sort_search as ss  # noqa: E402
import sortfeel_model as fm  # noqa: E402

BASE = os.path.join(HERE, "results", "sortfeel", "params.json")
W_FEEL = 6.0


def feel_penalty(f):
    m = f["mean"]; pex = sf.planar_excess(f)
    p = 10 * max(0, pex - 0.10) + 20 * max(0, m["osc"] - 0.06) + 40 * max(0, m["stuck"] - 0.015)
    p += 2 * max(0, 0.3 - m["jerk_rel"]) + 2 * max(0, m["jerk_rel"] - 2.2)
    return p, pex


def _eval(args):
    z, seed = args
    torch.set_num_threads(1)
    base = fm.load(BASE).cfg
    m = fm.SortFeel(fm.cfg_from_vec(z, base))
    res = ss.all16(m, seed)
    f, p = ss.fitness(res)
    m2 = fm.SortFeel(fm.cfg_from_vec(z, base))
    fe = sf.feel(m2, seed=seed)
    pen, pex = feel_penalty(fe)
    return f - W_FEEL * pen, p, {k: round(v[0], 2) for k, v in res.items()}, dict(pex=round(pex, 3), pen=round(pen, 3), **{q: fe["mean"][q] for q in ("osc", "stuck", "jerk_rel", "speed")})


def main():
    import cma
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True); ap.add_argument("--pop", type=int, default=8)
    ap.add_argument("--sigma", type=float, default=0.5); ap.add_argument("--gens", type=int, default=30)
    ap.add_argument("--check", type=int, default=5); ap.add_argument("--hours", type=float, default=99)
    a = ap.parse_args()
    os.makedirs(a.run, exist_ok=True)
    sp = os.path.join(a.run, "state.pkl")
    if os.path.isfile(sp):
        st = pickle.load(open(sp, "rb")); es, g0, best = st["es"], st["gen"], st["best"]
    else:
        es = cma.CMAEvolutionStrategy(np.zeros(len(fm.GENES)), a.sigma, {"popsize": a.pop, "seed": 5, "verbose": -9})
        g0, best = 0, dict(f=-1e9, held=-1e9)
    log = open(os.path.join(a.run, "log.jsonl"), "a"); t_end = time.time() + a.hours * 3600
    with Pool(4) as pool:
        for gen in range(g0, a.gens):
            if time.time() > t_end:
                break
            t0 = time.time(); X = es.ask(); seed = 3000 + gen
            R = pool.map(_eval, [(x, seed) for x in X])
            fit = [r[0] for r in R]
            es.tell(X, [-f for f in fit])
            i = int(np.argmax(fit))
            rec = dict(gen=gen, fmax=round(fit[i], 3), fmean=round(float(np.mean(fit)), 3), passed_best=R[i][1],
                       passed_mean=float(np.mean([r[1] for r in R])), losses_best=R[i][2], feel_best=R[i][3],
                       pen_mean=round(float(np.mean([r[3]["pen"] for r in R])), 3), sigma=round(float(es.sigma), 3), sec=round(time.time() - t0, 1))
            np.save(os.path.join(a.run, "mean.npy"), es.mean)
            if fit[i] > best["f"]:
                best.update(f=fit[i], gen=gen); np.save(os.path.join(a.run, "best_gen.npy"), X[i])
            if (gen + 1) % a.check == 0:
                rr = pool.map(_eval, [(es.mean, 600 + s) for s in range(4)])
                h = float(np.mean([r[0] for r in rr])); rec["held_mean"] = round(h, 3); rec["held_passed"] = [r[1] for r in rr]
                rec["held_feel"] = [r[3] for r in rr]; rec["held_losses"] = rr[0][2]
                if h > best["held"]:
                    best.update(held=h, held_gen=gen); np.save(os.path.join(a.run, "best.npy"), es.mean)
            log.write(json.dumps(rec) + "\n"); log.flush(); print(json.dumps(rec), flush=True)
            pickle.dump(dict(es=es, gen=gen + 1, best=best), open(sp + ".tmp", "wb")); os.replace(sp + ".tmp", sp)
    print("best", best, flush=True)


if __name__ == "__main__":
    main()
