"""Co-evolve the predator and prey policies of arms_sim.py: an asymmetric arms race by evolution strategies.

ALGORITHM (OpenAI-ES, antithetic, per species, Adam):
  FACTORIAL blocks against the CURRENT opponent. A block is 4 encounters sharing one seed (common random numbers):
      (pred + e, prey + d), (pred - e, prey + d), (pred + e, prey - d), (pred - e, prey - d)
  so each species' antithetic difference is averaged over the other's two signs: both species get a clean
  gradient estimate from the same 4 rollouts.
  POOL pairs against HISTORY (anti-cycling). Every `snap_every` generations both policies are frozen into a pool;
  each generation also runs antithetic pairs of the current predator against prey drawn uniformly from the whole
  pool (fictitious play: the best response to the average of the past) and the current prey against pool
  predators. A newer generation therefore cannot win by exploiting only the current opponent's latest quirk.
  The pool share is `pool_frac` of the pairs.

FITNESS (per encounter, a fixed-roster 40 s encounter; no births, no starvation inside one):
  predator  net mass per predator = catches * m_prey - metabolic burn (so bursting costs and an ambush can pay)
  prey      -(share of the prey caught) + w_graze * net mass per prey (grazed - burn)

Snapshots, the pool and the optimiser state are saved every generation-block, so re-running resumes.

    python Tools/NCA/arms_train.py --tag a1 --gens 2000          # log: runs/arms_a1/log.jsonl
"""
from __future__ import annotations

import argparse
import json
import os
import time

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")

import numpy as np
import multiprocessing as mp

import arms_sim as A

HERE = os.path.dirname(os.path.abspath(__file__))


def fitness(cfg, st):
    nq0, np0 = cfg.n_prey, cfg.n_pred
    caught = (st.caught_t[:, :nq0] >= 0).mean(1)
    f_pred = (st.catches.sum(1) * cfg.m_prey - st.burned_p.sum(1)) / np0
    net_q = (st.grazed[:, :nq0] - st.burned_q[:, :nq0]).mean(1) / cfg.m_prey
    return f_pred, caught, net_q, st.catches.sum(1)


def _work(args):
    cfg, thq, thp, seeds, steps = args
    st = A.run(cfg, thq, thp, len(seeds), seeds, steps, rng=np.random.default_rng(int(seeds[0]) * 7 + 3))
    return fitness(cfg, st)


def evaluate(pool, cfg, thq, thp, seeds, steps, workers):
    """Encounters in parallel chunks -> (f_pred, caught, net_q, catches) arrays of len B."""
    B = len(seeds)
    cuts = np.array_split(np.arange(B), workers)
    jobs = [(cfg, thq[c], thp[c], seeds[c], steps) for c in cuts if len(c)]
    res = pool.map(_work, jobs) if pool is not None else list(map(_work, jobs))
    return [np.concatenate([r[i] for r in res]) for i in range(4)]


class Adam:
    def __init__(self, n, lr, b1=0.9, b2=0.999):
        self.m = np.zeros(n); self.v = np.zeros(n); self.t = 0; self.lr = lr; self.b1 = b1; self.b2 = b2

    def step(self, g):
        self.t += 1
        self.m = self.b1 * self.m + (1 - self.b1) * g
        self.v = self.b2 * self.v + (1 - self.b2) * g * g
        mh = self.m / (1 - self.b1 ** self.t); vh = self.v / (1 - self.b2 ** self.t)
        return self.lr * mh / (np.sqrt(vh) + 1e-8)


def _norm_diffs(d):
    s = d.std()
    return d / s if s > 1e-9 else d * 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tag", default="a1")
    ap.add_argument("--gens", type=int, default=2000)
    ap.add_argument("--blocks", type=int, default=16, help="factorial blocks (4 encounters each) vs the current opponent")
    ap.add_argument("--pool_pairs", type=int, default=12, help="antithetic pairs per species vs the history pool")
    ap.add_argument("--secs", type=float, default=40.0)
    ap.add_argument("--sigma", type=float, default=0.04)
    ap.add_argument("--lr", type=float, default=0.01)
    ap.add_argument("--wd", type=float, default=0.002)
    ap.add_argument("--w_graze", type=float, default=0.5)
    ap.add_argument("--snap_every", type=int, default=10)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--confusion", type=float, default=0.3)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()

    out = os.path.join(HERE, "runs", f"arms_{a.tag}")
    os.makedirs(os.path.join(out, "snaps"), exist_ok=True)
    cfg = A.Cfg(confusion=a.confusion)
    steps = int(round(a.secs / cfg.dt))
    ck = os.path.join(out, "state.npz")
    rng = np.random.default_rng(a.seed)
    if os.path.exists(ck):
        z = np.load(ck, allow_pickle=True)
        thq, thp = z["thq"], z["thp"]
        oq, op = Adam(len(thq), a.lr), Adam(len(thp), a.lr)
        oq.m, oq.v, oq.t = z["oqm"], z["oqv"], int(z["oqt"])
        op.m, op.v, op.t = z["opm"], z["opv"], int(z["opt"])
        gen0 = int(z["gen"]) + 1
        rng = np.random.default_rng([a.seed, gen0])
        print("resumed at gen", gen0, flush=True)
    else:
        thq = A.init_params(rng, A.PREY_IN, A.PREY_OUT).astype(np.float64)
        thp = A.init_params(rng, A.PRED_IN, A.PRED_OUT).astype(np.float64)
        oq, op = Adam(len(thq), a.lr), Adam(len(thp), a.lr)
        gen0 = 0
        json.dump(dict(args=vars(a), cfg=A.cfg_dict(cfg), algo="OpenAI-ES antithetic, factorial blocks vs current + "
                       "antithetic pairs vs a uniform history pool (fictitious play), Adam"),
                  open(os.path.join(out, "config.json"), "w"), indent=1)
    snaps = sorted(f for f in os.listdir(os.path.join(out, "snaps")) if f.endswith(".npz"))
    pool_q = [np.load(os.path.join(out, "snaps", f))["thq"] for f in snaps]
    pool_p = [np.load(os.path.join(out, "snaps", f))["thp"] for f in snaps]
    if not snaps:
        np.savez(os.path.join(out, "snaps", "g00000.npz"), thq=thq, thp=thp)
        pool_q.append(thq.copy()); pool_p.append(thp.copy())

    workers = mp.Pool(a.workers) if a.workers > 1 else None
    logf = open(os.path.join(out, "log.jsonl"), "a")
    K, M, s = a.blocks, a.pool_pairs, a.sigma
    for gen in range(gen0, a.gens):
        t0 = time.time()
        eq = rng.normal(size=(K + M, len(thq))); ep = rng.normal(size=(K + M, len(thp)))
        seeds_blk = rng.integers(1, 2 ** 31 - 1, K)
        # factorial: rows (sp, sq) = (+,+), (-,+), (+,-), (-,-)
        SP = np.array([1, -1, 1, -1]); SQ = np.array([1, 1, -1, -1])
        Tq, Tp, seeds = [], [], []
        for k in range(K):
            for j in range(4):
                Tq.append(thq + s * SQ[j] * eq[k]); Tp.append(thp + s * SP[j] * ep[k]); seeds.append(seeds_blk[k])
        # pool pairs: predator +-e vs a pool prey; prey +-d vs a pool predator
        hq = rng.integers(0, len(pool_q), M); hp = rng.integers(0, len(pool_p), M)
        seeds_pool = rng.integers(1, 2 ** 31 - 1, 2 * M)
        for m in range(M):
            for sg in (1, -1):
                Tq.append(pool_q[hq[m]]); Tp.append(thp + s * sg * ep[K + m]); seeds.append(seeds_pool[m])
        for m in range(M):
            for sg in (1, -1):
                Tq.append(thq + s * sg * eq[K + m]); Tp.append(pool_p[hp[m]]); seeds.append(seeds_pool[M + m])
        Tq = np.stack(Tq).astype(np.float32); Tp = np.stack(Tp).astype(np.float32); seeds = np.array(seeds)
        f_pred, caught, net_q, catches = evaluate(workers, cfg, Tq, Tp, seeds, steps, a.workers)
        f_prey = -caught + a.w_graze * net_q
        # gradients
        n4 = 4 * K
        fp4 = f_pred[:n4].reshape(K, 4); fq4 = f_prey[:n4].reshape(K, 4)
        dp = np.concatenate([(fp4[:, 0] + fp4[:, 2]) / 2 - (fp4[:, 1] + fp4[:, 3]) / 2,
                             f_pred[n4:n4 + 2 * M:2] - f_pred[n4 + 1:n4 + 2 * M:2]])
        dq = np.concatenate([(fq4[:, 0] + fq4[:, 1]) / 2 - (fq4[:, 2] + fq4[:, 3]) / 2,
                             f_prey[n4 + 2 * M::2] - f_prey[n4 + 2 * M + 1::2]])
        gp = (_norm_diffs(dp)[:, None] * ep).mean(0) / s
        gq = (_norm_diffs(dq)[:, None] * eq).mean(0) / s
        thp = thp + op.step(gp) - a.lr * a.wd * thp
        thq = thq + oq.step(gq) - a.lr * a.wd * thq
        mins = a.secs / 60
        rec = dict(gen=gen, sec=round(time.time() - t0, 2),
                   cur_catch_pm=round(float(catches[:n4].mean() / mins), 2),
                   cur_caught=round(float(caught[:n4].mean()), 3),
                   cur_netq=round(float(net_q[:n4].mean()), 3),
                   cur_fpred=round(float(f_pred[:n4].mean()), 3),
                   predVpool_catch_pm=round(float(catches[n4:n4 + 2 * M].mean() / mins), 2),
                   preyVpool_catch_pm=round(float(catches[n4 + 2 * M:].mean() / mins), 2),
                   pool=len(pool_q), gnorm=[round(float(np.linalg.norm(gp)), 2), round(float(np.linalg.norm(gq)), 2)],
                   pnorm=[round(float(np.linalg.norm(thp)), 2), round(float(np.linalg.norm(thq)), 2)])
        logf.write(json.dumps(rec) + "\n"); logf.flush()
        if gen % 5 == 0:
            print(json.dumps(rec), flush=True)
        if (gen + 1) % a.snap_every == 0:
            np.savez(os.path.join(out, "snaps", f"g{gen + 1:05d}.npz"), thq=thq, thp=thp)
            pool_q.append(thq.copy()); pool_p.append(thp.copy())
            np.savez(ck, thq=thq, thp=thp, oqm=oq.m, oqv=oq.v, oqt=oq.t, opm=op.m, opv=op.v, opt=op.t, gen=gen)


if __name__ == "__main__":
    main()
