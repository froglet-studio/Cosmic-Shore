"""CMA-ES over an EvoRule genome (see evo_model.py), fitness = the strict 8-test yardstick.

    python Tools/NCA/evo_search.py --run runs/evo_s1 --genes behaviour --pop 10 --seeds 2 --gens 60
    python Tools/NCA/evo_search.py --run runs/evo_s2 --genes all --init runs/evo_s1/best.npy

Each generation draws fresh rollout seeds (common to every candidate in that generation, so they are
compared on the same dice), evaluates the population on 4 processes (1 torch thread each), and logs
every candidate. Every --check generations the incumbent best is re-scored on held-out seeds 100..107.
Resumable: the CMA state is pickled every generation.
"""
import argparse
import json
import os
import pickle
import sys
import time
from multiprocessing import Pool

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo_model as em  # noqa: E402

GROUPS = {
    "behaviour": ["sw_lay", "k_lay", "b_lay", "sw_egg", "p_egg", "beta_egg", "sw_lock", "lock", "D"],
    "out": ["sw_out", "g_out", "b_out"],
    "swirl": ["sw_swirl", "swirl", "swirl_gain"],
    "fear": ["sw_fear", "fear_k", "fear_tau"],
}
GROUPS["all"] = GROUPS["behaviour"] + GROUPS["out"] + GROUPS["swirl"] + GROUPS["fear"]


def mask_for(groups):
    idx = []
    for gname in groups.split("+"):
        for n in GROUPS[gname]:
            s = em.SLICES[n]; idx += list(range(s.start, s.stop))
    return np.array(sorted(set(idx)))


MODEL = "g2"


def _eval(args):
    g, seeds = args
    if MODEL == "compact":
        import evo_compact as ec
        return ec.evaluate(g, seeds)
    return em.evaluate(g, seeds)


def main():
    import cma
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True)
    ap.add_argument("--genes", default="behaviour")
    ap.add_argument("--init", default="")
    ap.add_argument("--fix", default="", help="name=value,... genes held at a value (e.g. sw_out=1)")
    ap.add_argument("--pop", type=int, default=10)
    ap.add_argument("--seeds", type=int, default=2)
    ap.add_argument("--sigma", type=float, default=0.5)
    ap.add_argument("--gens", type=int, default=60)
    ap.add_argument("--check", type=int, default=5)
    ap.add_argument("--hours", type=float, default=99)
    ap.add_argument("--model", default="g2", choices=["g2", "compact"])
    a = ap.parse_args()
    global MODEL
    MODEL = a.model
    os.makedirs(a.run, exist_ok=True)
    if MODEL == "compact":
        import evo_compact as ec
        ec.descriptors()
        base = np.load(a.init) if a.init else ec.default_genome()
        idx = np.arange(ec.DIM)
    else:
        base = em.pad(np.load(a.init)) if a.init else em.default_genome()
        idx = mask_for(a.genes)
    for kv in filter(None, a.fix.split(",")):
        k, v = kv.split("="); base[em.SLICES[k]] = float(v)
    state_p = os.path.join(a.run, "state.pkl")
    if os.path.isfile(state_p):
        st = pickle.load(open(state_p, "rb"))
        es, gen0, best, base = st["es"], st["gen"], st["best"], st["base"]
        print(f"resumed at generation {gen0}", flush=True)
    else:
        es = cma.CMAEvolutionStrategy(base[idx], a.sigma, {"popsize": a.pop, "seed": 1, "verbose": -9})
        gen0, best = 0, dict(f=-1e9, held=-1e9)
    log = open(os.path.join(a.run, "log.jsonl"), "a")
    t_end = time.time() + a.hours * 3600
    with Pool(4) as pool:
        for gen in range(gen0, a.gens):
            if time.time() > t_end:
                break
            t0 = time.time()
            X = es.ask()
            seeds = [1000 * (gen + 1) + i for i in range(a.seeds)]
            G = []
            for x in X:
                g = base.copy(); g[idx] = x; G.append(g)
            jobs = [(g, (s,)) for g in G for s in seeds]
            R = pool.map(_eval, jobs)
            fit, passed = [], []
            for i in range(len(G)):
                rr = R[i * a.seeds:(i + 1) * a.seeds]
                fit.append(float(np.mean([r[0] for r in rr]))); passed.append([r[1][0] for r in rr])
            es.tell(X, [-f for f in fit])
            i = int(np.argmax(fit))
            mean_g = base.copy(); mean_g[idx] = es.mean
            rec = dict(gen=gen, fmax=fit[i], fmean=float(np.mean(fit)), passed_best=passed[i],
                       passed_mean=float(np.mean([np.mean(p) for p in passed])), sigma=float(es.sigma), sec=round(time.time() - t0, 1))
            if fit[i] > best["f"]:
                best.update(f=fit[i], gen=gen); np.save(os.path.join(a.run, "best_gen.npy"), G[i])
            np.save(os.path.join(a.run, "mean.npy"), mean_g)
            if (gen + 1) % a.check == 0:
                # held-out: the distribution mean and the generation's best, on 8 unseen seeds
                cands = {"mean": mean_g, "gbest": G[i]}
                for name, g in cands.items():
                    rr = pool.map(_eval, [(g, (100 + s,)) for s in range(8)])
                    h = float(np.mean([r[0] for r in rr])); hp = [r[1][0] for r in rr]
                    rec[f"held_{name}"] = h; rec[f"held_{name}_passed"] = hp
                    if h > best["held"]:
                        best.update(held=h, held_gen=gen, held_passed=hp, held_from=name)
                        np.save(os.path.join(a.run, "best.npy"), g)
            log.write(json.dumps(rec) + "\n"); log.flush()
            print(json.dumps(rec), flush=True)
            pickle.dump(dict(es=es, gen=gen + 1, best=best, base=base), open(state_p + ".tmp", "wb"))
            os.replace(state_p + ".tmp", state_p)
    print("best", best, flush=True)


if __name__ == "__main__":
    main()
