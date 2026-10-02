"""Search the parametric critter for a TARGET emotion (CMA-ES, with a same-budget random-search baseline).

    python search.py --targets cute,playful,eerie,majestic,menacing,terrifying --gens 20

Fitness = mean over (2 seeds x {hover, cruise} viewers) of sqrt(P_logistic * P_subproto)[target] - the
GEOMETRIC mean of the two judges, so a creature that fools one judge and not the other scores low.
Validation = 6 FRESH seeds x {hover, cruise, evade} (evade was never in the fitness): hit rate (the probe's
top emotion is the target) and how often both judges agree. Laws are enforced inside the critter.
"""
from __future__ import annotations

import argparse
import json
import math
import os
from multiprocessing import Pool

import numpy as np

from critter import D, decode, factory
from probe import EmotionProbe, EMOTIONS
from sim import run, HERE

PROBE = None
FIT_SEEDS, FIT_VIEWERS = (11, 12), ("hover", "cruise")
VAL_SEEDS, VAL_VIEWERS = (101, 102, 103, 104, 105, 106), ("hover", "cruise", "evade")
SECONDS = 20.0


PROBE_PATH = os.environ.get("EMOTION_PROBE")        # e.g. results/probe_v3.json; default results/probe.json


def _probe():
    global PROBE
    if PROBE is None:
        PROBE = EmotionProbe.load(PROBE_PATH)
    return PROBE


def _eval(args):
    theta, target, seeds, viewers = args
    pr = _probe(); k = pr.emotions.index(target); vals, tops, agree = [], [], []
    for s in seeds:
        for v in viewers:
            f, _ = run(factory(theta), s, v, seconds=SECONDS)
            sc = pr.score(f)
            vals.append(math.sqrt(sc["_pl"][k] * sc["_ps"][k]))
            tops.append(sc["top"] == target); agree.append(sc["agreement"])
    return float(np.mean(vals)), float(np.mean(tops)), float(np.mean(agree))


def cma(target, gens, lam, pool, seed=0):
    rng = np.random.default_rng(seed)
    mu = lam // 2; w = np.log(mu + 0.5) - np.log(np.arange(1, mu + 1)); w /= w.sum(); mueff = 1 / np.sum(w ** 2)
    cc, cs = 4 / (D + 4), (mueff + 2) / (D + mueff + 5)
    c1 = 2 / ((D + 1.3) ** 2 + mueff); cmu = min(1 - c1, 2 * (mueff - 2 + 1 / mueff) / ((D + 2) ** 2 + mueff))
    damps = 1 + 2 * max(0, math.sqrt((mueff - 1) / (D + 1)) - 1) + cs; chiN = math.sqrt(D) * (1 - 1 / (4 * D) + 1 / (21 * D * D))
    m = rng.random(D); sigma = 0.3; C = np.eye(D); pc = np.zeros(D); ps = np.zeros(D)
    best = (-1, None); hist = []
    for g in range(gens):
        Bv, Dv = np.linalg.eigh(C)[1], np.sqrt(np.maximum(np.linalg.eigh(C)[0], 1e-12))
        Z = rng.normal(size=(lam, D)); Y = Z @ np.diag(Dv) @ Bv.T; X = m + sigma * Y
        Xc = np.clip(X, 0, 1)
        fit = np.array([r[0] for r in pool.map(_eval, [(x, target, FIT_SEEDS, FIT_VIEWERS) for x in Xc])])
        order = np.argsort(-fit)
        if fit[order[0]] > best[0]:
            best = (float(fit[order[0]]), Xc[order[0]].copy())
        hist.append(round(float(fit[order[0]]), 3))
        Ysel = (Xc[order[:mu]] - m) / sigma
        m_old = m; m = m + sigma * (w @ Ysel)
        Cinvsqrt = Bv @ np.diag(1 / Dv) @ Bv.T
        ps = (1 - cs) * ps + math.sqrt(cs * (2 - cs) * mueff) * Cinvsqrt @ (m - m_old) / sigma
        hs = np.linalg.norm(ps) / math.sqrt(1 - (1 - cs) ** (2 * (g + 1))) < (1.4 + 2 / (D + 1)) * chiN
        pc = (1 - cc) * pc + hs * math.sqrt(cc * (2 - cc) * mueff) * (m - m_old) / sigma
        C = (1 - c1 - cmu) * C + c1 * np.outer(pc, pc) + cmu * (Ysel.T * w) @ Ysel
        sigma *= math.exp((cs / damps) * (np.linalg.norm(ps) / chiN - 1)); sigma = min(sigma, 0.6)
        print(f"  {target} gen {g:2d} best {fit[order[0]]:.3f} sigma {sigma:.3f}", flush=True)
    return best, hist


def random_search(target, n, pool, seed=1):
    rng = np.random.default_rng(seed); X = rng.random((n, D))
    fit = np.array([r[0] for r in pool.map(_eval, [(x, target, FIT_SEEDS, FIT_VIEWERS) for x in X])])
    i = int(np.argmax(fit)); return (float(fit[i]), X[i]), np.maximum.accumulate(fit).tolist()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--targets", default=",".join(EMOTIONS)); ap.add_argument("--gens", type=int, default=20)
    ap.add_argument("--lam", type=int, default=14); ap.add_argument("--out", default=os.path.join(HERE, "results", "search.json"))
    a = ap.parse_args()
    res = json.load(open(a.out)) if os.path.exists(a.out) else {}
    with Pool(4) as pool:
        for t in a.targets.split(","):
            (bf, bx), hist = cma(t, a.gens, a.lam, pool)
            (rf, rx), rhist = random_search(t, a.gens * a.lam, pool)
            val = _eval((bx, t, VAL_SEEDS, VAL_VIEWERS)); rval = _eval((rx, t, VAL_SEEDS, VAL_VIEWERS))
            q = decode(bx)
            res[t] = dict(cma=dict(fitness=round(bf, 3), val_fitness=round(val[0], 3), val_hit=round(val[1], 3),
                                   val_agree=round(val[2], 3), history=hist, theta=bx.tolist(),
                                   params={k: (round(v, 3) if isinstance(v, float) else v) for k, v in q.items()}),
                          random=dict(fitness=round(rf, 3), val_fitness=round(rval[0], 3), val_hit=round(rval[1], 3),
                                      val_agree=round(rval[2], 3), theta=rx.tolist(), history=[round(x, 3) for x in rhist[::a.lam]]),
                          evals=a.gens * a.lam)
            print(t, json.dumps({k: v for k, v in res[t]["cma"].items() if k not in ("theta", "history")}), "| random",
                  res[t]["random"]["val_hit"], flush=True)
            json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
