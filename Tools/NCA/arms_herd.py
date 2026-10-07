"""Run a9: the SELFISH HERD. Prey selection at the level of the individual instead of the species mean.

Every earlier run scored the prey policy on the species mean (share of the 120 caught), so being the safer one in
the middle of a group could not pay. Hamilton's selfish herd needs individual-level selection. Here every pond holds
T = 4 prey TRIBES of 30, interleaved at random, sharing the base policy but carrying different perturbations:
(+e_a, -e_a, +e_b, -e_b). A tribe's fitness is its own -(share caught) + w_graze * net mass grazed, and the prey
gradient is estimated from WITHIN-pond differences, f(+e_a) - f(-e_a), so a perturbation that survives at its
pond-mates' expense is rewarded. Predators keep one shared policy, trained by antithetic encounter pairs (same seed,
same prey perturbations, predator +-d). There is no history pool: run a4 showed no cycling without one.

    python Tools/NCA/arms_herd.py --tag a9 --init runs/arms_a3/snaps/g01460.npz --gens 500
"""
from __future__ import annotations

import argparse
import json
import os
import time

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")

import multiprocessing as mp

import numpy as np

import arms_sim as A
from arms_train import Adam, _norm_diffs

HERE = os.path.dirname(os.path.abspath(__file__))
T = 4


def herd_step(cfg, st, thq_t, thp, rng):
    """thq_t [B, T, P]: one prey parameter vector per tribe; prey i belongs to tribe i % T."""
    xq, xp, aux = A.observe(cfg, st)
    B, N, D = xq.shape
    oq = np.zeros((B, N, A.PREY_OUT), np.float32)
    for t in range(T):
        oq[:, t::T] = A.mlp(thq_t[:, t], xq[:, t::T], A.PREY_IN, A.PREY_OUT)
    op = A.mlp(thp, xp, A.PRED_IN, A.PRED_OUT)
    A.apply(cfg, st, oq, op, aux, rng=rng)


def _work(args):
    cfg, thq_t, thp, seeds, steps = args
    st = A.reset(cfg, len(seeds), seeds)
    rng = np.random.default_rng(int(seeds[0]) * 7 + 3)
    for _ in range(steps):
        herd_step(cfg, st, thq_t, thp, rng)
    N = cfg.n_prey
    caught = st.caught_t[:, :N] >= 0
    net = st.grazed[:, :N] - st.burned_q[:, :N]
    fq = np.stack([-caught[:, t::T].mean(1) + 0.5 * net[:, t::T].mean(1) for t in range(T)], 1)   # [B, T]
    fp = (st.catches.sum(1) * cfg.m_prey - 0.25 * st.burned_p.sum(1)) / cfg.n_pred
    return fq, fp, st.catches.sum(1), caught.mean(1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tag", default="a9")
    ap.add_argument("--init", required=True)
    ap.add_argument("--gens", type=int, default=500)
    ap.add_argument("--pairs", type=int, default=24, help="encounter pairs per generation (2 x pairs encounters)")
    ap.add_argument("--secs", type=float, default=30.0)
    ap.add_argument("--sigma", type=float, default=0.02)
    ap.add_argument("--lr", type=float, default=0.005)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--seed", type=int, default=9)
    a = ap.parse_args()
    out = os.path.join(HERE, "runs", f"arms_{a.tag}")
    os.makedirs(os.path.join(out, "snaps"), exist_ok=True)
    cfg = A.Cfg()
    steps = int(a.secs / cfg.dt)
    ck = os.path.join(out, "state.npz")
    if os.path.exists(ck):
        z = np.load(ck); thq, thp = z["thq"], z["thp"]; gen0 = int(z["gen"]) + 1
        oq, op = Adam(len(thq), a.lr), Adam(len(thp), a.lr)
        oq.m, oq.v, oq.t, op.m, op.v, op.t = z["oqm"], z["oqv"], int(z["oqt"]), z["opm"], z["opv"], int(z["opt"])
    else:
        z = np.load(a.init); thq = z["thq"].astype(np.float64); thp = z["thp"].astype(np.float64); gen0 = 0
        oq, op = Adam(len(thq), a.lr), Adam(len(thp), a.lr)
        np.savez(os.path.join(out, "snaps", "g00000.npz"), thq=thq, thp=thp)
        json.dump(dict(args=vars(a), cfg=A.cfg_dict(cfg), algo="selfish herd: 4 prey tribes per pond with within-pond "
                       "antithetic perturbations (individual-level prey selection); predators antithetic encounter pairs; no pool"),
                  open(os.path.join(out, "config.json"), "w"), indent=1)
    rng = np.random.default_rng([a.seed, gen0])
    pool = mp.Pool(a.workers)
    logf = open(os.path.join(out, "log.jsonl"), "a")
    K, s = a.pairs, a.sigma
    for gen in range(gen0, a.gens):
        t0 = time.time()
        eq = rng.normal(size=(K, 2, len(thq)))            # per pair: two prey directions (tribes +a,-a,+b,-b)
        ep = rng.normal(size=(K, len(thp)))
        seeds = rng.integers(1, 2 ** 31 - 1, K)
        TQ, TP, SD = [], [], []
        for k in range(K):
            tribes = np.stack([thq + s * eq[k, 0], thq - s * eq[k, 0], thq + s * eq[k, 1], thq - s * eq[k, 1]])
            for sg in (1, -1):
                TQ.append(tribes); TP.append(thp + sg * s * ep[k]); SD.append(seeds[k])
        TQ = np.stack(TQ).astype(np.float32); TP = np.stack(TP).astype(np.float32); SD = np.array(SD)
        cuts = np.array_split(np.arange(len(SD)), a.workers)
        res = pool.map(_work, [(cfg, TQ[c], TP[c], SD[c], steps) for c in cuts])
        fq = np.concatenate([r[0] for r in res]); fp = np.concatenate([r[1] for r in res])
        catches = np.concatenate([r[2] for r in res]); caught = np.concatenate([r[3] for r in res])
        # prey: within-pond differences, averaged over the predator sign pair
        fq2 = fq.reshape(K, 2, T).mean(1)                 # [K, T]
        dq = np.concatenate([fq2[:, 0] - fq2[:, 1], fq2[:, 2] - fq2[:, 3]])
        Eq = np.concatenate([eq[:, 0], eq[:, 1]])
        dp = fp.reshape(K, 2)[:, 0] - fp.reshape(K, 2)[:, 1]
        thq = thq + oq.step((_norm_diffs(dq)[:, None] * Eq).mean(0) / s)
        thp = thp + op.step((_norm_diffs(dp)[:, None] * ep).mean(0) / s)
        rec = dict(gen=gen, sec=round(time.time() - t0, 2), cur_catch_pm=round(float(catches.mean() / (a.secs / 60)), 2),
                   cur_caught=round(float(caught.mean()), 3), within_pond_spread=round(float(np.abs(dq).mean()), 4),
                   cur_netq=0.0, cur_fpred=round(float(fp.mean()), 3), predVpool_catch_pm=0.0, preyVpool_catch_pm=0.0,
                   pnorm=[round(float(np.linalg.norm(thp)), 2), round(float(np.linalg.norm(thq)), 2)])
        logf.write(json.dumps(rec) + "\n"); logf.flush()
        if gen % 5 == 0:
            print(json.dumps(rec), flush=True)
        if (gen + 1) % 10 == 0:
            np.savez(os.path.join(out, "snaps", f"g{gen + 1:05d}.npz"), thq=thq, thp=thp)
            np.savez(ck, thq=thq, thp=thp, oqm=oq.m, oqv=oq.v, oqt=oq.t, opm=op.m, opv=op.v, opt=op.t, gen=gen)


if __name__ == "__main__":
    main()
