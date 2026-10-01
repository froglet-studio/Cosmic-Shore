"""evo16: CMA-ES over an Evo16Rule genome, fitness = evo16_fit (16 transitions + margin + heal).

    python Tools/NCA/evo16_search.py --run runs/evo16/s1 --init Tools/NCA/results/evo/genome.npy \
        --genes behaviour+new --set sw_reg=0.5,sw_wnd=0.5,sw_head=0.5 --pop 12 --seeds 2 --hours 3

Fresh seeds every generation (shared by the population, so candidates face the same dice), 4 worker
processes, resumable (CMA state pickled per generation). Every --check generations the generation best
and the CMA mean are re-scored on 4 held-out seeds (900..903); the best held-out genome -> best.npy.
"""
import argparse
import json
import os
import pickle
import sys
import time
from evo16_pool import pinned_pool

import numpy as np

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo16_model as e16  # noqa: E402

GROUPS = {
    "behaviour": ["sw_lay", "k_lay", "b_lay", "sw_egg", "p_egg", "beta_egg", "sw_lock", "lock", "D"],
    "out": ["sw_out", "g_out", "b_out"],
    "new": ["sw_reg", "a_reg", "sw_wnd", "a_wnd", "f_wnd", "tau_wnd", "sw_head", "h_head", "k_head"],
}
HELD = (900, 901, 902, 903)


def mask_for(groups):
    idx = []
    for gname in groups.split("+"):
        for n in GROUPS[gname]:
            s = e16.SLICES[n]; idx += list(range(s.start, s.stop))
    return np.array(sorted(set(idx)))


def _eval(args):
    import evo16_fit as ef
    g, seeds = args
    return ef.evaluate(g, seeds)


def main():
    import cma
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True)
    ap.add_argument("--genes", default="behaviour+new")
    ap.add_argument("--init", default="")
    ap.add_argument("--set", default="", help="name=value,... applied to the init genome")
    ap.add_argument("--pop", type=int, default=12)
    ap.add_argument("--seeds", type=int, default=2)
    ap.add_argument("--sigma", type=float, default=0.4)
    ap.add_argument("--gens", type=int, default=999)
    ap.add_argument("--check", type=int, default=4)
    ap.add_argument("--hours", type=float, default=3)
    a = ap.parse_args()
    os.makedirs(a.run, exist_ok=True)
    base = e16.default_genome()
    if a.init:
        g0 = np.load(a.init)
        base[:len(g0)] = g0
    for kv in filter(None, a.set.split(",")):
        k, v = kv.split("="); base[e16.SLICES[k]] = float(v)
    idx = mask_for(a.genes)
    state_p = os.path.join(a.run, "state.pkl")
    if os.path.isfile(state_p):
        st = pickle.load(open(state_p, "rb"))
        es, gen0, best, base = st["es"], st["gen"], st["best"], st["base"]
        print(f"resumed at generation {gen0}", flush=True)
    else:
        es = cma.CMAEvolutionStrategy(base[idx], a.sigma, {"popsize": a.pop, "seed": 3, "verbose": -9})
        gen0, best = 0, dict(f=-1e9, held=-1e9)
        np.save(os.path.join(a.run, "init.npy"), base)
    log = open(os.path.join(a.run, "log.jsonl"), "a")
    t_end = time.time() + a.hours * 3600
    with pinned_pool(4) as pool:
        for gen in range(gen0, a.gens):
            if time.time() > t_end:
                break
            t0 = time.time()
            X = es.ask()
            seeds = [7000 + 37 * gen + i for i in range(a.seeds)]
            G = []
            for x in X:
                g = base.copy(); g[idx] = x; G.append(g)
            R = pool.map(_eval, [(g, (s,)) for g in G for s in seeds])
            fit, P, H = [], [], []
            for i in range(len(G)):
                rr = [r[0] for r in R[i * a.seeds:(i + 1) * a.seeds]]
                fit.append(float(np.mean([r["f"] for r in rr])))
                P.append(float(np.mean([r["p"] for r in rr])))
                H.append(np.mean([r["heal"] for r in rr], 0).round(2).tolist())
            es.tell(X, [-f for f in fit])
            i = int(np.argmax(fit))
            mean_g = base.copy(); mean_g[idx] = es.mean
            rec = dict(gen=gen, fmax=round(fit[i], 3), fmean=round(float(np.mean(fit)), 3), p_best=round(P[i], 3),
                       p_mean=round(float(np.mean(P)), 3), heal_best=H[i], sigma=round(float(es.sigma), 3),
                       on_best=[k for k in e16.SWITCHES if G[i][e16.SLICES[k]][0] > 0], sec=round(time.time() - t0, 1))
            np.save(os.path.join(a.run, "mean.npy"), mean_g)
            if fit[i] > best["f"]:
                best.update(f=fit[i], gen=gen); np.save(os.path.join(a.run, "best_gen.npy"), G[i])
            if (gen + 1) % a.check == 0:
                for name, g in {"mean": mean_g, "gbest": G[i]}.items():
                    rr = [r[0] for r in pool.map(_eval, [(g, (s,)) for s in HELD])]
                    h = float(np.mean([r["f"] for r in rr]))
                    rec[f"held_{name}"] = round(h, 3)
                    rec[f"held_{name}_p"] = round(float(np.mean([r["p"] for r in rr])), 3)
                    rec[f"held_{name}_heal"] = np.mean([r["heal"] for r in rr], 0).round(2).tolist()
                    if h > best["held"]:
                        best.update(held=h, held_gen=gen, held_from=name, held_p=rec[f"held_{name}_p"])
                        np.save(os.path.join(a.run, "best.npy"), g)
            log.write(json.dumps(rec) + "\n"); log.flush()
            print(json.dumps(rec), flush=True)
            pickle.dump(dict(es=es, gen=gen + 1, best=best, base=base), open(state_p + ".tmp", "wb"))
            os.replace(state_p + ".tmp", state_p)
    print("best", best, flush=True)


if __name__ == "__main__":
    main()
