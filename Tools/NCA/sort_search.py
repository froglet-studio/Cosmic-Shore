"""CMA-ES over the sort model's continuous genes (sort_model.GENES), fitness = all 16 transitions.

    python Tools/NCA/sort_search.py --run runs/sort/cma_k12 --set K=12,per_well=4,molt=1 --pop 10 --gens 40

A candidate is scored on ONE fresh seed per generation (shared by the whole population): each plan is
grown once (240 steps), its own test scored, then each of the three switch targets is run from a COPY of
that grown swarm and model (cull_to, 240 steps). Fitness per test: 1 if it passes the strict bar
(alive >= 32, closest, <= 8) plus 0.5 * clip((8 - loss) / 8, -1, 1); own-plan tests count double (tier 1
first). Every --check generations the CMA mean is re-scored on held-out seeds 500..503.
"""
import argparse, copy, json, os, pickle, sys, time
from multiprocessing import Pool
import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import sort_model as sm  # noqa: E402
import sort_eval as so  # noqa: E402

W_OWN = 2.0


@torch.no_grad()
def all16(model, seed, L=None):
    """{test: (loss_to_wanted, passed)} for 4 own + up to 12 switches (n/a skipped)."""
    L = L or sn.LossCfg(); T = sn.load_targets(); out = {}
    for k in sn.KINDS:
        gen = sn.make_gen(seed); sw = sn.seed_swarm([T[k]], model.world, gen)
        model.mem = {}
        for _ in range(240):
            sw = model(sw, gen)
        row = se._score_row(sw, 0, T, L); n = se._alive(sw, 0)
        out[k] = (row[k], se._passes(row, k, n))
        for e in range(4):
            to = sn.PLAN_OF[e]
            if to == k:
                continue
            sw2 = sw.clone(); m2 = copy.deepcopy(model); g2 = sn.make_gen(seed * 7 + e)
            if not se.cull_to(sw2, 0, e, g2):
                continue
            for _ in range(240):
                sw2 = m2(sw2, g2)
            row = se._score_row(sw2, 0, T, L); n = se._alive(sw2, 0)
            out[f"{k}->{to}"] = (row[to], se._passes(row, to, n))
    return out


def fitness(res):
    f = 0.0; p = 0
    for t, (loss, ok) in res.items():
        w = 1.0 if "->" in t else W_OWN
        f += w * (float(ok) + 0.5 * float(np.clip((8 - loss) / 8, -1, 1)))
        p += int(ok)
    return f, p


def _eval(args):
    z, sets, seed = args
    torch.set_num_threads(1)
    m = so.make("", sets)
    m = sm.SortSwarm(sm.cfg_from_vec(z, m.cfg))
    res = all16(m, seed)
    f, p = fitness(res)
    return f, p, {k: round(v[0], 2) for k, v in res.items()}


def main():
    import cma
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True); ap.add_argument("--set", default="")
    ap.add_argument("--pop", type=int, default=10); ap.add_argument("--sigma", type=float, default=0.6)
    ap.add_argument("--gens", type=int, default=40); ap.add_argument("--check", type=int, default=5)
    ap.add_argument("--hours", type=float, default=99)
    ap.add_argument("--init", default="", help="start the CMA mean at this vector (.npy)")
    a = ap.parse_args()
    os.makedirs(a.run, exist_ok=True)
    sp = os.path.join(a.run, "state.pkl")
    if os.path.isfile(sp):
        st = pickle.load(open(sp, "rb")); es, g0, best = st["es"], st["gen"], st["best"]
        print("resumed at", g0, flush=True)
    else:
        x0 = np.load(a.init) if a.init else np.zeros(len(sm.GENES))
        es = cma.CMAEvolutionStrategy(x0, a.sigma, {"popsize": a.pop, "seed": 3, "verbose": -9})
        g0, best = 0, dict(f=-1e9, held=-1e9)
    log = open(os.path.join(a.run, "log.jsonl"), "a"); t_end = time.time() + a.hours * 3600
    with Pool(4) as pool:
        for gen in range(g0, a.gens):
            if time.time() > t_end:
                break
            t0 = time.time(); X = es.ask(); seed = 2000 + gen
            R = pool.map(_eval, [(x, a.set, seed) for x in X])
            fit = [r[0] for r in R]
            es.tell(X, [-f for f in fit])
            i = int(np.argmax(fit))
            rec = dict(gen=gen, fmax=fit[i], fmean=float(np.mean(fit)), passed_best=R[i][1],
                       passed_mean=float(np.mean([r[1] for r in R])), losses_best=R[i][2], sigma=float(es.sigma),
                       sec=round(time.time() - t0, 1))
            np.save(os.path.join(a.run, "mean.npy"), es.mean)
            if fit[i] > best["f"]:
                best.update(f=fit[i], gen=gen); np.save(os.path.join(a.run, "best_gen.npy"), X[i])
            if (gen + 1) % a.check == 0:
                rr = pool.map(_eval, [(es.mean, a.set, 500 + s) for s in range(4)])
                h = float(np.mean([r[0] for r in rr])); rec["held_mean"] = h; rec["held_passed"] = [r[1] for r in rr]
                rec["held_losses"] = rr[0][2]
                if h > best["held"]:
                    best.update(held=h, held_gen=gen); np.save(os.path.join(a.run, "best.npy"), es.mean)
            log.write(json.dumps(rec) + "\n"); log.flush(); print(json.dumps(rec), flush=True)
            pickle.dump(dict(es=es, gen=gen + 1, best=best), open(sp + ".tmp", "wb")); os.replace(sp + ".tmp", sp)
    print("best", best, flush=True)


if __name__ == "__main__":
    main()
