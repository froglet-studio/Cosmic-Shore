"""Tune a genome's parameter bytes with separable CMA-ES against the island fitness (no structural change).

    python3 -m agenome.tune OUT_DIR --genome G.json [G2.json ...] --stats S.npz --hold H.npz \
        [--evals 400] [--block 24] [--windows 2] [--workers 4]

Parameter-only steps are the strongest lever measured so far (a hand-built seed went from 4.40 to 1.57 on 150
renders, where 2,300 renders of structural search reached 2.15), and the islands spend a quarter of their renders on
random byte tweaks around their best. This replaces the random tweaks with sep-CMA-ES (Ros and Hansen 2008: a
diagonal covariance, so it scales to the hundreds of bytes a genome has) on blocks of up to --block parameters at a
time, each block started from the best genome so far.

The objective is the island's: stat_w * stat + judge_w * judge, the judge v3 on --windows windows (--hop seconds
apart), each window the worse of the two nets, averaged. Judge more than one window: a climb on one window fits
exactly those 4 seconds (2026-11-10: genomes judged 0.87 there read 2.0 to 3.9 later). The online critic is left
out; the islands re-judge a tuned genome with their own critic when it arrives as a seed.

The tuned genome is then judged on --check-n windows --check-hop seconds apart (default 6 x 4 s: 0 to 20 s), and the
windows the climb never saw (4, 12, 16, 20 s by default) are reported before and after as meta.cma.unseen_before/after:
the honest read of whether the tune generalised or only fitted the judged windows.

Writes OUT_DIR/<file stem>__cma.json (meta.name <lineage>__cma) when the climb improved (the best genome, meta.cma = before/after/unseen/evals) and
OUT_DIR/log.jsonl (one line per CMA generation, plus one "check" line per genome).
"""
import argparse
import json
import math
import multiprocessing as mp
import os
import time

import numpy as np

from .genome import Genome
from .ops import OPS


class _Args:
    judge_w = 1.0
    judge_n = 12
    judge_net = "inception+mobilenet"
    judge_agg = "max"


def param_slots(g):
    """(node index, param index) of every tunable byte of g's live nodes."""
    live = g.live() if hasattr(g, "live") else range(len(g.nodes))
    return [(i, j) for i in sorted(live) for j in range(len(OPS[g.nodes[i].op].params))]


class SepCMA:
    """Separable CMA-ES (diagonal covariance) on a box, minimising."""

    def __init__(self, x0, sigma0, lo=0.0, hi=255.0, lam=None, seed=0):
        n = len(x0)
        self.n, self.lo, self.hi = n, lo, hi
        self.rng = np.random.default_rng(seed)
        self.m = np.asarray(x0, float).copy()
        self.sigma = float(sigma0)
        self.lam = lam or 4 + int(3 * math.log(max(n, 2)))
        self.mu = self.lam // 2
        w = np.log(self.mu + 0.5) - np.log(np.arange(1, self.mu + 1))
        self.w = w / w.sum()
        self.mueff = 1.0 / (self.w ** 2).sum()
        self.cs = (self.mueff + 2) / (n + self.mueff + 5)
        self.ds = 1 + 2 * max(0.0, math.sqrt((self.mueff - 1) / (n + 1)) - 1) + self.cs
        self.cc = (4 + self.mueff / n) / (n + 4 + 2 * self.mueff / n)
        c1 = 2 / ((n + 1.3) ** 2 + self.mueff)
        cmu = min(1 - c1, 2 * (self.mueff - 2 + 1 / self.mueff) / ((n + 2) ** 2 + self.mueff))
        self.c1, self.cmu = c1 * (n + 2) / 3, cmu * (n + 2) / 3  # the separable variant's faster learning rates
        self.C = np.ones(n)
        self.ps = np.zeros(n)
        self.pc = np.zeros(n)
        self.chi = math.sqrt(n) * (1 - 1 / (4 * n) + 1 / (21 * n * n))
        self.gen = 0

    def ask(self):
        self.z = self.rng.standard_normal((self.lam, self.n))
        self.y = self.z * np.sqrt(self.C)
        return np.clip(self.m + self.sigma * self.y, self.lo, self.hi)

    def tell(self, f):
        order = np.argsort(f)[: self.mu]
        yw = (self.w[:, None] * self.y[order]).sum(0)
        zw = (self.w[:, None] * self.z[order]).sum(0)
        self.m = np.clip(self.m + self.sigma * yw, self.lo, self.hi)
        self.ps = (1 - self.cs) * self.ps + math.sqrt(self.cs * (2 - self.cs) * self.mueff) * zw
        hs = np.linalg.norm(self.ps) / math.sqrt(1 - (1 - self.cs) ** (2 * (self.gen + 1))) < (1.4 + 2 / (self.n + 1)) * self.chi
        self.pc = (1 - self.cc) * self.pc + hs * math.sqrt(self.cc * (2 - self.cc) * self.mueff) * yw
        rank_mu = (self.w[:, None] * self.y[order] ** 2).sum(0)
        self.C = (1 - self.c1 - self.cmu) * self.C + self.c1 * self.pc ** 2 + self.cmu * rank_mu
        self.C = np.clip(self.C, 1e-6, 1e6)
        self.sigma *= math.exp((self.cs / self.ds) * (np.linalg.norm(self.ps) / self.chi - 1))
        self.sigma = float(np.clip(self.sigma, 0.5, 128.0))  # below half a byte nothing changes
        self.gen += 1


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("--genome", nargs="+", required=True)
    ap.add_argument("--stats", required=True)
    ap.add_argument("--hold", default=None)
    ap.add_argument("--evals", type=int, default=400, help="renders per genome (each judged window is one more)")
    ap.add_argument("--block", type=int, default=24, help="parameters tuned together; each block starts from the best")
    ap.add_argument("--gens-per-block", type=int, default=8)
    ap.add_argument("--sigma", type=float, default=12.0, help="initial step in byte units")
    ap.add_argument("--sigma-min", type=float, default=2.0, help="smallest block start step after fruitless blocks")
    ap.add_argument("--windows", type=int, default=2)
    ap.add_argument("--hop", type=float, default=8.0)
    ap.add_argument("--check-n", type=int, default=6, help="windows for the after-tune check (0: no check)")
    ap.add_argument("--check-hop", type=float, default=4.0)
    ap.add_argument("--frames", type=int, default=60)
    ap.add_argument("--stat-w", type=float, default=0.5)
    ap.add_argument("--judge-w", type=float, default=1.0)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--lp-threads", type=int, default=1)
    ap.add_argument("--mem-gb", type=float, default=3.0)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args(argv)
    os.makedirs(a.out, exist_ok=True)
    from .search import JudgeState, _eval, _init
    judge = JudgeState(_Args())
    pool = mp.get_context("spawn").Pool(a.workers, initializer=_init,
                                        initargs=(a.stats, a.hold, a.lp_threads, a.mem_gb))
    log = open(os.path.join(a.out, "log.jsonl"), "a")
    cache = {}

    def evaluate(gs):
        keys = [g.to_bytes().hex() for g in gs]
        todo = [(k, g) for k, g in zip(keys, gs) if k not in cache]
        uniq = {k: g for k, g in todo}
        res = pool.map(_eval, [(g.to_json(), a.frames, judge.n, a.windows, a.hop) for g in uniq.values()])
        for c in res:
            c.pop("frames", None)
        judge.score(res)
        for k, c in zip(uniq, res):
            c["dist"] = (a.stat_w * c["stat"] + a.judge_w * c.get("judge", 99.0)) if c.get("ok") else 99.0
            cache[k] = c
        return [cache[k] for k in keys], len(uniq)

    seen = {round(k * a.hop, 3) for k in range(a.windows)}
    times = [round(k * a.check_hop, 3) for k in range(a.check_n)]
    unseen = [i for i, t in enumerate(times) if t not in seen]

    def check(gs):
        """Judge each genome on the check windows; returns (all-window judge, unseen-window mean, per-window list)."""
        if a.check_n < 2 or not unseen:
            return [(None, None, None)] * len(gs)
        res = pool.map(_eval, [(g.to_json(), a.frames, judge.n, a.check_n, a.check_hop) for g in gs])
        for c in res:
            c.pop("frames", None)
        judge.score(res)
        out = []
        for c in res:
            w = c.get("judge_windows")
            out.append((c.get("judge"), round(float(np.mean([w[i] for i in unseen])), 4) if w else None, w))
        return out

    rng = np.random.default_rng(a.seed)
    for path in a.genome:
        g0 = Genome.from_json(json.load(open(path))).ensure_root()
        stem = os.path.splitext(os.path.basename(path))[0]
        name = (g0.meta or {}).get("name") or stem  # the lineage name (the islands' cap reads the part before "__")
        (c0,), _ = evaluate([g0])
        best_g, best = g0, c0
        print(f"{name}: start dist {c0['dist']:.3f} judge {c0.get('judge')} stat {c0['stat']:.3f}", flush=True)
        slots = param_slots(g0)
        spent, t0 = 1, time.time()
        sigma0 = a.sigma
        while spent < a.evals and slots:
            block = [slots[k] for k in rng.permutation(len(slots))[: a.block]]
            x0 = [best_g.nodes[i].params[j] for i, j in block]
            es = SepCMA(x0, sigma0, seed=int(rng.integers(1 << 30)))
            before_block = best["dist"]
            for _ in range(a.gens_per_block):
                if spent >= a.evals:
                    break
                X = es.ask()
                cands = []
                for x in X:
                    h = best_g.copy()
                    for (i, j), v in zip(block, x):
                        h.nodes[i].params[j] = int(round(v))
                    cands.append(h)
                res, n_new = evaluate(cands)
                spent += max(1, n_new)
                f = np.array([c["dist"] for c in res])
                es.tell(f)
                k = int(np.argmin(f))
                if f[k] < best["dist"]:
                    best_g, best = cands[k], res[k]
                log.write(json.dumps({"genome": name, "spent": spent, "gen": es.gen, "sigma": round(es.sigma, 2),
                                      "gen_best": round(float(f[k]), 4), "best": round(best["dist"], 4),
                                      "best_judge": best.get("judge"), "t": round(time.time() - t0, 1)}) + "\n")
                log.flush()
            # between blocks, a 1/5th-style rule on the step: a block that found nothing halves the next block's start
            # (an already-tuned genome sits in a narrow basin: on 2026-11-10 most steps of 18 bytes made it worse)
            sigma0 = max(sigma0, es.sigma) if best["dist"] < before_block else max(a.sigma_min, 0.5 * sigma0)
            print(f"{name}: {spent} evals, best dist {best['dist']:.3f} judge {best.get('judge')} "
                  f"windows {best.get('judge_windows')}, next sigma {sigma0:.1f}", flush=True)
        if best["dist"] >= c0["dist"]:
            print(f"{name}: no gain in {spent} evals (dist {c0['dist']:.3f}); nothing written", flush=True)
            continue
        (jb, ub, wb), (ja, ua, wa) = check([g0, best_g])
        log.write(json.dumps({"genome": name, "check": {"times": times, "unseen": [times[i] for i in unseen],
                                                         "before": wb, "after": wa, "unseen_before": ub,
                                                         "unseen_after": ua}}) + "\n")
        log.flush()
        g = best_g.copy()
        g.meta = dict(g0.meta or {})
        g.meta.update({"name": f"{name}__cma", "tuned_from": os.path.basename(path),
                       "cma": {"before": round(c0["dist"], 4), "after": round(best["dist"], 4),
                               "judge_before": c0.get("judge"), "judge_after": best.get("judge"),
                               "judge_windows": best.get("judge_windows"), "windows": a.windows, "hop": a.hop,
                               "check_times": times, "check_before": wb, "check_after": wa,
                               "unseen_before": ub, "unseen_after": ua,
                               "evals": spent, "stat_w": a.stat_w}})
        out = os.path.join(a.out, f"{stem}__cma.json")
        g.save(out)
        print(f"wrote {out}: dist {c0['dist']:.3f} -> {best['dist']:.3f}; judge {c0.get('judge')} -> "
              f"{best.get('judge')}; unseen windows {ub} -> {ua}", flush=True)
    pool.terminate()


if __name__ == "__main__":
    main()
