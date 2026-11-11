"""Mechanics fit: a genome's simulation against recorded sessions, on statistics of the per-frame game state.

    python3 -m agenome.mechfit OUT.json --genome G.json --sessions DIR [DIR ...] [--gens 30] [--workers 8]

The statistics are taken per AI vessel over the clip and averaged: speed and boost quantiles, the share of time with
no boost, boost resets, crystals collected, distance flown, turn-rate quantiles. A session clip starts at Go, so the
simulation is compared from its own start (no warmup). loss = mean |sim - real| / scale over the statistics.
Tunes the bytes of ag.pilot, ag.skim, ag.impact and the handling bytes of ag.vessel with sep-CMA-ES.
"""
import argparse
import json
import multiprocessing as mp
import os

import numpy as np

from .genome import Genome
from .ops import OPS

NAMES = ["speed_q10", "speed_q25", "speed_q50", "speed_q75", "speed_q90", "boost_q25", "boost_q50", "boost_q75",
         "boost_q90", "noboost", "resets", "crystals", "dist", "turn_q50", "turn_q90"]
SCALE = np.array([15, 15, 20, 30, 30, 0.5, 0.6, 0.6, 0.5, 0.12, 1.5, 2.5, 700, 6, 20], float)
TUNE = {"ag.pilot": None, "ag.skim": ("radius",), "ag.impact": None, "ag.vessel": ("turn", "follow", "accel")}


def _vec(speed, boost, course, crystals, secs):
    """speed, boost [T] at 15 fps, course [T,3] unit directions, crystals collected in the clip."""
    k = 45.0 / secs
    db = np.diff(boost)
    cosang = np.clip((course[1:] * course[:-1]).sum(1), -1, 1)
    turn = np.degrees(np.arccos(cosang)) * 15.0
    dist = float(speed.sum() / 15.0)
    return np.array([*np.percentile(speed, [10, 25, 50, 75, 90]), *np.percentile(boost, [25, 50, 75, 90]),
                     float((boost < 1.05).mean()), float((db < -0.5).sum()) * k, crystals * k, dist * k,
                     *np.percentile(turn, [50, 90])])


def real_stats(paths):
    rows = []
    for p in paths:
        L = [json.loads(l) for l in open(os.path.join(p, "state.jsonl"))]
        for j, v0 in enumerate(L[0]["vessels"]):
            if not v0["ai"]:
                continue
            v = [f["vessels"][j] for f in L]
            rows.append(_vec(np.array([x["speed"] for x in v]), np.array([x["boost_mult"] for x in v]),
                             np.array([x["course"] for x in v]),
                             v[-1]["crystals_collected"] - v[0]["crystals_collected"], len(L) / 15.0))
    rows = np.array(rows)
    return rows.mean(0), rows.std(0), rows


def sim_stats(g, secs=45.0, seeds=(0, 1)):
    from .sim import build_populations, World
    g = g.copy().ensure_root()
    a_idx = g.nodes[g.root()].inputs[1]
    rows = []
    for sd in seeds:
        w = World(build_populations(g, a_idx, gseed=g.seed + 1000 * sd))
        gs = w.games[0].sim
        dt = 1 / 60
        sp, bm, co = [], [], []
        for i in range(int(secs / dt)):
            w.step(dt)
            if i % 4 == 3:
                sp.append(gs.speed.copy()); bm.append(gs.boost.copy()); co.append(gs.Rh[:, :, 2].copy())
        sp, bm, co = np.array(sp), np.array(bm), np.array(co)
        for k in range(sp.shape[1]):
            rows.append(_vec(sp[:, k], bm[:, k], co[:, k], float(gs.score[k]), secs))
    return np.array(rows).mean(0)


def loss(vec, target):
    return float((np.abs(vec - target) / SCALE).mean())


def slots(g):
    out = []
    for i in sorted(g.live()):
        n = g.nodes[i]
        if n.op in TUNE:
            for j, p in enumerate(OPS[n.op].params):
                if (TUNE[n.op] is None or p.name in TUNE[n.op]) and p.kind in ("lin", "exp"):
                    out.append((i, j))
    return out


_G = {}


def _init(gj, target, secs):
    _G["g"], _G["t"], _G["secs"] = Genome.from_json(gj), target, secs


def _apply(g, sl, x):
    g = g.copy()
    for (i, j), b in zip(sl, x):
        g.nodes[i].params[j] = int(b)
    return g


def _eval(x):
    g = _G["g"]
    try:
        return loss(sim_stats(_apply(g, slots(g), x), _G["secs"]), _G["t"])
    except Exception:
        return 99.0


def main(argv=None):
    from .tune import SepCMA
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--genome", required=True)
    ap.add_argument("--sessions", nargs="+", required=True)
    ap.add_argument("--gens", type=int, default=30)
    ap.add_argument("--lam", type=int, default=16)
    ap.add_argument("--workers", type=int, default=8)
    ap.add_argument("--sigma", type=float, default=20.0)
    a = ap.parse_args(argv)
    g = Genome.load(a.genome)
    target, sd, rows = real_stats(a.sessions)
    secs = 45.0
    sl = slots(g)
    x0 = np.array([g.nodes[i].params[j] for i, j in sl], float)
    base = sim_stats(g, secs)
    print("real  ", dict(zip(NAMES, target.round(2))))
    print("before", dict(zip(NAMES, base.round(2))), "loss", round(loss(base, target), 4), flush=True)
    es = SepCMA(x0, a.sigma, lam=a.lam, seed=1)
    best, bx = loss(base, target), x0
    with mp.Pool(a.workers, initializer=_init, initargs=(g.to_json(), target, secs)) as pool:
        for gen in range(a.gens):
            X = es.ask()
            f = pool.map(_eval, [x for x in X])
            es.tell(np.array(f))
            k = int(np.argmin(f))
            if f[k] < best:
                best, bx = f[k], X[k].copy()
            print(gen, "gen best", round(min(f), 4), "best", round(best, 4), "sigma", round(es.sigma, 1), flush=True)
    gb = _apply(g, sl, bx)
    after = sim_stats(gb, secs, seeds=(5, 6, 7))
    b0 = sim_stats(g, secs, seeds=(5, 6, 7))
    gb.meta["mechfit"] = {"sessions": [os.path.basename(p.rstrip("/")) for p in a.sessions], "loss_before_unseen_seeds": loss(b0, target),
                          "loss_after_unseen_seeds": loss(after, target), "names": NAMES, "real": target.tolist(),
                          "before": b0.tolist(), "after": after.tolist()}
    gb.save(a.out)
    print("after (unseen seeds)", dict(zip(NAMES, after.round(2))), "loss", round(loss(after, target), 4), "was", round(loss(b0, target), 4))


if __name__ == "__main__":
    main()
