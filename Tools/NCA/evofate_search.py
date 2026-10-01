"""CMA-ES over evofate's fate genes, fitness = accuracy (own plans x2 + the 4 standard fair-cull switches,
evofate_fast.screen on one fresh seed per generation) + a FEEL term that keeps the swarm in evo's
organic region (osc <= 0.08, planar excess <= 0.15, low coherence = gas-like evo texture).

The feel is measured on the last 64 steps of the grow (batched, cheap); the binding numbers come later
from swarm_feel.feel + scorecard.

    python Tools/NCA/evofate_search.py --run runs/evofate/cma --pop 8 --gens 30
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
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import swarm_feel as sf  # noqa: E402
import evofate_model as ef  # noqa: E402
import evofate_fast as fast  # noqa: E402

GENES = ef.GENES + [("e0", 0.0, 8.0, False), ("f_inertia", 0.0, 0.95, False), ("tp", 0.3, 4.0, True), ("te0", 0.0, 2.0, False)]
BASE = json.loads(os.environ.get("EVOFATE_BASE", '{"pull":1.0,"e0":2.0,"f_inertia":0.5}'))
W_FEEL = float(os.environ.get("EVOFATE_W_FEEL", "4"))


def cfg_from_vec(z):
    d = asdict(ef.FateCfg(**BASE))
    for (n, lo, hi, lg), v in zip(GENES, z):
        b = d[n]
        val = (np.exp(np.log(max(b, 1e-6)) + 0.5 * v) if lg else b + 0.15 * (hi - lo) * v)
        d[n] = float(min(hi, max(lo, val)))
    return ef.FateCfg(**d)


@torch.no_grad()
def screen_feel(model, seed):
    """fast.screen, plus feel metrics on steps 176..240 of the grow (batch of 4)."""
    T = sn.load_targets(); L = sn.LossCfg()
    gen = sn.make_gen(seed); model.mem = {}
    sw = sn.seed_swarm([T[k] for k in sn.KINDS], model.world, gen)
    P = []; alive = None
    for t in range(240):
        if t >= 175:
            a = sw.active & sw.hatched
            alive = a.clone() if alive is None else alive & a
            P.append(sw.pos.clone())
        sw = model(sw, gen)
    P.append(sw.pos.clone()); alive &= sw.active & sw.hatched
    P = torch.stack(P)
    feel = {}
    for b, k in enumerate(sn.KINDS):
        m = alive[b]
        if int(m.sum()) > 10:
            feel[k] = sf.metrics(P[:, b][:, m], sw.elem[b][m], k)
    # the rest = fast.screen's protocol on this grown batch
    res = dict(own={}, sw={})
    for b, k in enumerate(sn.KINDS):
        row = se._score_row(sw, b, T, L)
        res["own"][k] = dict(loss=row[k], ok=se._passes(row, k, se._alive(sw, b)))
    std, _ = se.transitions()
    subs, keys, mems = [], [], {}
    for k, e in std:
        b0 = sn.KINDS.index(k); sub = sw.index(torch.tensor([b0])).clone()
        if not se.cull_to(sub, 0, e, gen):
            continue
        mems[len(subs)] = fast.clone_mem(model, b0, len(subs)); subs.append(sub); keys.append((k, e))
    if subs:
        lock = model.locked
        model.mem = mems; model.locked = lock[[sn.KINDS.index(k) for k, _ in keys]].clone()
        s2 = sn.Swarm.cat(subs)
        for _ in range(240):
            s2 = model(s2, gen)
        for b, (k, e) in enumerate(keys):
            to = sn.PLAN_OF[e]; row = se._score_row(s2, b, T, L)
            res["sw"][f"{k}->{to}"] = dict(loss=row[to], ok=se._passes(row, to, se._alive(s2, b)))
    return res, feel


def feel_term(feel):
    if len(feel) < 4:
        return -2.0, {}
    osc = np.mean([v["osc"] for v in feel.values()])
    pe = max(max(0.0, v["planar_frac"] - v.get("planar_plan", 0)) for v in feel.values())
    coh = np.mean([v["coherence"] for v in feel.values()])
    jr = np.mean([v["jerk_rel"] for v in feel.values()])
    t = -10 * max(0.0, osc - 0.07) - 4 * max(0.0, pe - 0.12) + 0.5 * (1 - coh) - 2 * max(0.0, 0.25 - jr)
    return float(t), dict(osc=round(float(osc), 3), pe=round(float(pe), 3), coh=round(float(coh), 3), jr=round(float(jr), 3))


def _eval(args):
    z, seed = args
    torch.set_num_threads(1)
    m = ef.EvoFate(cfg_from_vec(z))
    res, feel = screen_feel(m, seed)
    acc = fast.fitness(res)
    ft, fd = feel_term(feel)
    passed = sum(v["ok"] for v in res["own"].values()) + sum(v["ok"] for v in res["sw"].values())
    losses = {k: round(v["loss"], 2) for k, v in res["own"].items()} | {k: round(v["loss"], 2) for k, v in res["sw"].items()}
    return acc + W_FEEL * ft, passed, losses, fd


def main():
    import cma
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True); ap.add_argument("--pop", type=int, default=8)
    ap.add_argument("--sigma", type=float, default=0.6); ap.add_argument("--gens", type=int, default=30)
    ap.add_argument("--hours", type=float, default=99)
    a = ap.parse_args()
    os.makedirs(a.run, exist_ok=True)
    sp = os.path.join(a.run, "state.pkl")
    if os.path.isfile(sp):
        st = pickle.load(open(sp, "rb")); es, g0, best = st["es"], st["gen"], st["best"]
    else:
        es = cma.CMAEvolutionStrategy(np.zeros(len(GENES)), a.sigma, {"popsize": a.pop, "seed": 5, "verbose": -9})
        g0, best = 0, dict(f=-1e9)
    log = open(os.path.join(a.run, "log.jsonl"), "a"); t_end = time.time() + a.hours * 3600
    json.dump(dict(base=BASE, genes=GENES, w_feel=W_FEEL), open(os.path.join(a.run, "meta.json"), "w"), indent=1)
    with Pool(4) as pool:
        for gen in range(g0, a.gens):
            if time.time() > t_end:
                break
            t0 = time.time(); X = es.ask(); seed = 3000 + gen
            R = pool.map(_eval, [(x, seed) for x in X] + [(es.mean, seed)])
            Rm = R[-1]; R = R[:-1]
            fit = [r[0] for r in R]
            es.tell(X, [-f for f in fit])
            i = int(np.argmax(fit))
            rec = dict(gen=gen, fmax=round(fit[i], 3), fmean=round(float(np.mean(fit)), 3), passed_best=R[i][1],
                       feel_best=R[i][3], losses_best=R[i][2], mean_fit=round(Rm[0], 3), mean_passed=Rm[1], mean_feel=Rm[3],
                       mean_losses=Rm[2], mean_cfg={n: round(getattr(cfg_from_vec(es.mean), n), 4) for n, *_ in GENES},
                       sigma=round(float(es.sigma), 3), sec=round(time.time() - t0, 1))
            np.save(os.path.join(a.run, "mean.npy"), es.mean)
            if fit[i] > best["f"]:
                best.update(f=fit[i], gen=gen); np.save(os.path.join(a.run, "best_gen.npy"), X[i])
            log.write(json.dumps(rec) + "\n"); log.flush(); print(json.dumps(rec), flush=True)
            pickle.dump(dict(es=es, gen=gen + 1, best=best), open(sp + ".tmp", "wb")); os.replace(sp + ".tmp", sp)


if __name__ == "__main__":
    main()
