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

--window-mode random (2026-11-10, after the fixed-window tunes above fitted their windows: c_mincell 0.947 -> 0.897
judged but 1.50 -> 1.47 on the windows it never saw): every CMA generation draws its --windows offsets from 0 to
--window-span s on a --window-step grid, the same draw for every candidate and for the current best (common random
windows: candidates are compared on equal terms, and no window stays long enough to be fitted). The best is replaced
only by a candidate that beats it on that generation's windows; its score is the mean over every window it has been
judged on. The check then uses --check-offsets (default: four windows from the end of the grid's last window, 24, 26,
28 and 30 s, so they share no frame with any window selection saw), and a genome is written only when it is better
there than the genome it started from.

Writes OUT_DIR/<file stem>__cma.json (meta.name <lineage>__cma) when the climb improved (the best genome, meta.cma =
before/after/unseen/evals), OUT_DIR/pareto.json (those genomes as island rows; in random mode the row's numbers are
its check windows, the comparable ones) and OUT_DIR/log.jsonl (one line per CMA generation, plus one "check" line per
genome). --publish DIR copies pareto.json and an args.json naming it an island to DIR (runs/tuned): every island then
resumes from it at its next start and draws immigrants out of it.
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


def graft_post(g, op):
    """g with a P op (one "p" input, such as post.bandgain) spliced in at the top of the root's post chain, at its
    default bytes (bandgain's are the identity), and the index of the new node."""
    o = OPS[op]
    if o.out != "P" or tuple(o.inputs) != ("p",):
        raise ValueError(f"--graft takes a P op with one p input, not {op}")
    g = g.copy()
    r = g.root()
    slot = list(OPS["out"].inputs).index("p")
    top = g.nodes[r].inputs[slot]
    g.nodes.insert(r, type(g.nodes[r])(op, [top], [q.default for q in o.params]))
    for n in g.nodes:
        n.inputs = [j + 1 if (j is not None and j >= r) else j for j in n.inputs]
    g.nodes[r + 1].inputs[slot] = r
    return g, r


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
        """Samples clipped to the box and rounded to bytes; the stored steps are the ones actually evaluated, so a
        sample pushed past a bound is not a free step that inflates sigma and C (code review 2026-11-10: about one
        byte per 24-byte block sits on 0 or 255, and post.bandgain's tailcut and chroma default to 0)."""
        z = self.rng.standard_normal((self.lam, self.n))
        X = np.round(np.clip(self.m + self.sigma * z * np.sqrt(self.C), self.lo, self.hi))
        self.y = (X - self.m) / self.sigma
        self.z = self.y / np.sqrt(self.C)
        return X

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
    ap.add_argument("--graft", default=None,
                    help="splice this P op (e.g. post.bandgain) onto the top of each genome's post chain first; its "
                         "parameters are the first block, and the output is named <stem>__<op>__cma")
    ap.add_argument("--windows", type=int, default=2)
    ap.add_argument("--hop", type=float, default=8.0)
    ap.add_argument("--window-mode", choices=("fixed", "random"), default="fixed",
                    help="random: each CMA generation draws its windows from 0..--window-span on a --window-step grid")
    ap.add_argument("--window-span", type=float, default=20.0)
    ap.add_argument("--window-step", type=float, default=2.0)
    ap.add_argument("--check-n", type=int, default=6, help="windows for the after-tune check (0: no check)")
    ap.add_argument("--check-hop", type=float, default=4.0)
    ap.add_argument("--check-offsets", default=None,
                    help="comma list of check windows in seconds (random mode default 24,26,28,30: no frame drawn)")
    ap.add_argument("--publish", default=None, help="copy pareto.json and an island args.json here (runs/tuned)")
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
    ctx = mp.get_context("spawn")
    pool = [ctx.Pool(a.workers, initializer=_init, initargs=(a.stats, a.hold, a.lp_threads, a.mem_gb))]

    def pmap(payloads, per=180.0):
        """A map over the pool with a deadline: a worker killed mid-render or a render that never returns would block
        it forever (code review 2026-11-10); on timeout the pool is rebuilt and the missing results fail."""
        jobs = [pool[0].apply_async(_eval, (p,)) for p in payloads]
        deadline = time.time() + per * (1 + len(payloads) // max(1, a.workers))
        out, dead = [], False
        for j in jobs:
            try:
                out.append(j.get(timeout=max(1.0, deadline - time.time())) if not dead else None)
            except mp.TimeoutError:
                dead = True
                out.append(None)
        if dead:
            print("evaluation timed out; restarting the worker pool", flush=True)
            pool[0].terminate()
            pool[0] = ctx.Pool(a.workers, initializer=_init, initargs=(a.stats, a.hold, a.lp_threads, a.mem_gb))
        return [r if r is not None else {"ok": False, "stat": 99.0, "zbytes": 10 ** 6, "err": "timeout",
                                         "nodes": 0, "luma": 0.0, "motion": 0.0} for r in out]
    log = open(os.path.join(a.out, "log.jsonl"), "a")
    cache = {}

    def evaluate(gs, offsets=None):
        """Score gs on the fixed windows (count --windows, --hop apart) or on the given offsets (seconds)."""
        tag = tuple(offsets) if offsets is not None else None
        keys = [(g.to_bytes().hex(), tag) for g in gs]
        todo = [(k, g) for k, g in zip(keys, gs) if k not in cache]
        uniq = {k: g for k, g in todo}
        wins = list(offsets) if offsets is not None else a.windows
        res = pmap([(g.to_json(), a.frames, judge.n, wins, a.hop) for g in uniq.values()])
        for c in res:
            c.pop("frames", None)
        judge.score(res)
        for k, c in zip(uniq, res):
            c["dist"] = (a.stat_w * c["stat"] + a.judge_w * c.get("judge", 99.0)) if c.get("ok") else 99.0
            cache[k] = c
        return [cache[k] for k in keys], len(uniq)

    rand = a.window_mode == "random"
    grid = [round(k * a.window_step, 3) for k in range(int(a.window_span / a.window_step + 1e-9) + 1)]
    if rand and a.windows > len(grid):
        ap.error(f"--windows {a.windows} > the {len(grid)} offsets of the window grid")
    L = a.frames / 15.0  # a window spans (t, t + L] seconds
    climbed = list(grid) if rand else [k * a.hop for k in range(max(1, a.windows))]
    if a.check_offsets:
        times = [round(float(t), 3) for t in a.check_offsets.split(",") if t.strip()]
    elif rand:
        end = max(climbed) + L  # after the last window the climb can draw (24 s on the default 0-20 s grid)
        times = [round(end + k * 2.0, 3) for k in range(4)]
    else:
        times = [round(k * a.check_hop, 3) for k in range(a.check_n)]
    # unseen = sharing no frame with any climbed window (code review 2026-11-10: comparing start times alone let a
    # check window overlap a climbed one by 2-4 s with a finer hop or longer clips)
    unseen = [i for i, t in enumerate(times) if all(t + L <= c + 1e-6 or t >= c + L - 1e-6 for c in climbed)]
    checks = {}  # genome hex -> its check result (the check of g0 is shared by every graft of it)

    def check(gs):
        """Judge each genome on the check windows; returns (all-window judge, unseen-window mean, per-window list,
        the row: stat and judge over the check windows)."""
        if len(times) < 1 or not unseen or (not a.check_offsets and not rand and a.check_n < 2):
            return [(None, None, None, None)] * len(gs)
        todo = [g for g in gs if g.to_bytes().hex() not in checks]
        res = pmap([(g.to_json(), a.frames, judge.n, list(times), a.hop) for g in todo])
        for c in res:
            c.pop("frames", None)
        judge.score(res)
        for g, c in zip(todo, res):
            if c.get("ok"):
                c["dist"] = a.stat_w * c["stat"] + a.judge_w * c.get("judge", 99.0)
            checks[g.to_bytes().hex()] = c
        out = []
        for g in gs:
            c = checks[g.to_bytes().hex()]
            w = c.get("judge_windows")
            out.append((c.get("judge"), round(float(np.mean([w[i] for i in unseen])), 4) if w else None, w, c))
        return out

    def merged(hist):
        """One record of a genome judged on several generations' windows: the window-weighted means."""
        from .search import merge_windows
        r = dict(hist[0])
        for h in hist[1:]:
            r = merge_windows(r, dict(h))
        if r.get("ok"):
            r["dist"] = a.stat_w * r["stat"] + a.judge_w * r.get("judge", 99.0)
        return r

    rng = np.random.default_rng(a.seed)
    rows = []
    try:  # merge with an earlier invocation's rows into the same OUT_DIR (deduped by genome bytes below)
        rows.extend(json.load(open(os.path.join(a.out, "pareto.json"))))
    except Exception:
        pass
    stems_used = {}

    def draw():
        return sorted(float(t) for t in rng.choice(grid, size=a.windows, replace=False)) if rand else None

    def publish():
        if not a.publish:
            return
        os.makedirs(a.publish, exist_ok=True)
        # ADD to the published front, never replace it: a tune into a new OUT must not drop the rows earlier tunes
        # put there (audit 2026-11-10). One row per genome, this tune's row winning.
        keep = {}
        try:
            prev = json.load(open(os.path.join(a.publish, "pareto.json")))
        except Exception:
            prev = []
        for r in list(prev) + list(rows):
            try:
                keep[Genome.from_json(r["genome"]).to_bytes()] = r
            except Exception:
                continue
        for name, data in (("pareto.json", sorted(keep.values(), key=lambda r: r["zbytes"])),
                           ("args.json", dict(vars(a), island=os.path.basename(os.path.normpath(a.publish)),
                                              kind="tuner"))):
            tmp = os.path.join(a.publish, name + ".tmp")
            with open(tmp, "w") as f:
                json.dump(data, f)
            os.replace(tmp, os.path.join(a.publish, name))

    for path in a.genome:
        g0 = Genome.from_json(json.load(open(path))).ensure_root()
        stem = os.path.splitext(os.path.basename(path))[0]
        if stem in stems_used:  # runs/islandA/best.json and runs/islandD/best.json: keep both outputs
            stem = f"{stem}_{os.path.basename(os.path.dirname(os.path.abspath(path)))}_{len(stems_used)}"
        stems_used[stem] = path
        name = (g0.meta or {}).get("name") or stem  # the lineage name (the islands' cap reads the part before "__")
        (c0,), _ = evaluate([g0], draw())
        best_g, hist = g0, [c0]
        best = merged(hist)
        print(f"{name}: start dist {c0['dist']:.3f} judge {c0.get('judge')} stat {c0['stat']:.3f}", flush=True)
        focus = []
        if a.graft:
            # the grafted genome starts as the identity (or close to it); its parameters go first, then every block
            # draws from all parameters. The before/after numbers stay those of the ungrafted genome.
            gg, k = graft_post(g0, a.graft)
            (cg,), _ = evaluate([gg], draw())
            best_g, hist = gg, [cg]  # climbed from the graft; written only if it ends below the ungrafted genome
            best = merged(hist)
            focus = [(k, j) for j in range(len(OPS[a.graft].params))]
            stem = f"{stem}__{a.graft.replace('.', '_')}"
            print(f"{name}: grafted {a.graft} at node {k}: dist {cg['dist']:.3f} judge {cg.get('judge')}", flush=True)
        slots = param_slots(best_g)
        spent, t0 = 1, time.time()
        sigma0 = a.sigma
        while spent < a.evals and slots:
            block = focus or [slots[k] for k in rng.permutation(len(slots))[: a.block]]
            focus = []
            x0 = [best_g.nodes[i].params[j] for i, j in block]
            es = SepCMA(x0, sigma0, seed=int(rng.integers(1 << 30)))
            improved = False
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
                offs = draw()
                if rand:
                    # common random windows: the candidates and the current best are judged on the same fresh draw
                    res, n_new = evaluate(cands + [best_g], offs)
                    res, fe = res[:-1], res[-1]
                else:
                    res, n_new = evaluate(cands)
                spent += max(1, n_new)
                f = np.array([c["dist"] for c in res])
                es.tell(f)
                k = int(np.argmin(f))
                if rand:
                    if f[k] < fe["dist"]:
                        best_g, hist, improved = cands[k], [res[k]], True
                    elif fe.get("win_t") not in [h.get("win_t") for h in hist]:
                        hist.append(fe)
                    best = merged(hist)
                elif f[k] < best["dist"]:
                    best_g, best, improved = cands[k], res[k], True
                log.write(json.dumps({"genome": name, "input": path, "spent": spent, "gen": es.gen, "sigma": round(es.sigma, 2),
                                      "gen_best": round(float(f[k]), 4), "best": round(best["dist"], 4),
                                      "best_judge": best.get("judge"), "best_nwin": best.get("judge_nwin"),
                                      "windows": offs, "t": round(time.time() - t0, 1)}) + "\n")
                log.flush()
            # between blocks, a 1/5th-style rule on the step: a block that found nothing halves the next block's start
            # (an already-tuned genome sits in a narrow basin: on 2026-11-10 most steps of 18 bytes made it worse)
            sigma0 = max(sigma0, es.sigma) if improved else max(a.sigma_min, 0.5 * sigma0)
            print(f"{name}: {spent} evals, best dist {best['dist']:.3f} judge {best.get('judge')} "
                  f"windows {best.get('judge_windows')}, next sigma {sigma0:.1f}", flush=True)
        if best_g.to_bytes() == g0.to_bytes() or (not rand and best["dist"] >= c0["dist"]):
            print(f"{name}: no gain in {spent} evals (dist {c0['dist']:.3f}); nothing written", flush=True)
            continue
        (jb, ub, wb, cb), (ja, ua, wa, ca) = check([g0, best_g])
        log.write(json.dumps({"genome": name, "input": path, "check": {"times": times, "unseen": [times[i] for i in unseen],
                                                         "before": wb, "after": wa, "unseen_before": ub,
                                                         "unseen_after": ua,
                                                         "dist_before": cb and cb.get("dist"),
                                                         "dist_after": ca and ca.get("dist")}}) + "\n")
        log.flush()
        if rand and (ca is None or cb is None or not ca.get("ok") or ca["dist"] >= cb["dist"]):
            print(f"{name}: tuned on random windows but no better on the check windows {times} "
                  f"(dist {cb and round(cb.get('dist', 99), 4)} -> {ca and round(ca.get('dist', 99), 4)}); "
                  f"nothing written", flush=True)
            continue
        g = best_g.copy()
        g.meta = dict(g0.meta or {})
        before, after = (cb["dist"], ca["dist"]) if rand else (c0["dist"], best["dist"])
        g.meta.update({"name": f"{name}__cma", "tuned_from": os.path.basename(path),
                       "cma": {"before": round(before, 4), "after": round(after, 4),
                               "judge_before": cb.get("judge") if rand else c0.get("judge"),
                               "judge_after": ca.get("judge") if rand else best.get("judge"),
                               "judge_windows": best.get("judge_windows"), "windows": a.windows, "hop": a.hop,
                               "window_mode": a.window_mode, "train_judge": best.get("judge"),
                               "train_nwin": best.get("judge_nwin"),
                               "check_times": times, "check_before": wb, "check_after": wa,
                               "unseen_before": ub, "unseen_after": ua,
                               "evals": spent, "stat_w": a.stat_w}})
        out = os.path.join(a.out, f"{stem}__cma.json")
        g.save(out)
        # a pareto.json of the tuned genomes, in the islands' row format: --publish (or a copy to runs/<name>/ with an
        # island args.json) reaches every island; dist here has no critic term. In random mode the row is the check
        # windows' record (windows no genome of the tune was selected on), so rows of different tunes compare.
        src = ca if rand else best
        rows.append({k: src[k] for k in ("ok", "stat", "hold", "judge", "judge_hold", "judges", "judges_hold",
                                         "judge_windows", "judge_nwin", "judge_v", "stat_windows", "stat_nwin",
                                         "win_t", "zbytes", "nodes", "luma", "motion", "dist") if k in src})
        rows[-1].update({"unseen_before": ub, "unseen_after": ua, "genome": g.to_json()})
        keep = {}
        for r in rows:  # one row per genome, the newest
            try:
                keep[Genome.from_json(r["genome"]).to_bytes()] = r
            except Exception:
                continue
        rows[:] = list(keep.values())
        with open(os.path.join(a.out, "pareto.json.tmp"), "w") as f:
            json.dump(sorted(rows, key=lambda r: r["zbytes"]), f)
        os.replace(os.path.join(a.out, "pareto.json.tmp"), os.path.join(a.out, "pareto.json"))
        publish()
        print(f"wrote {out}: dist {before:.3f} -> {after:.3f}; judge {g.meta['cma']['judge_before']} -> "
              f"{g.meta['cma']['judge_after']}; unseen windows {ub} -> {ua}", flush=True)
    pool[0].terminate()


if __name__ == "__main__":
    main()
