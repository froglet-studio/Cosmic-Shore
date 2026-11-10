"""Population search: regime-switching Pareto ("diamond") over (distance to Amoebius down, genome bytes down),
with a MAP-Elites side archive for diversity.

The regimes are Garrett's pareto_complexity.py (NCA Ecology Lab) lifted from a (1+lambda) climber to a population:
  COMPLEXIFY - select on distance to Amoebius; growth mutations (insert, duplicate, swap, crossover) dominate.
  COMPRESS   - hold distance with a leaky-ReLU floor (peak / retain), minimise compressed bytes; shrink mutations
               (delete, prune, param -> default) dominate.
Flips on plateau (patience) or when the population's median size exceeds the byte budget.

Run:  python3 -m agenome.search --run RUN_DIR --stats STATS.npz --seeds 'GLOB' [--workers 4] [--gens 200]
"""
import argparse
import glob
import json
import math
import multiprocessing as mp
import os
import random
import time
import traceback

import numpy as np

from .genome import Genome, mutate, crossover, _delete
from .ops import OPS

COMPLEXIFY, COMPRESS = "COMPLEXIFY", "COMPRESS"

# ---------------------------------------------------------------------- worker side
_R = None
_ST = None
_HOLD = None


def _init(stats_path, hold_path, lp_threads):
    global _R, _ST, _HOLD
    os.environ.setdefault("LP_NUM_THREADS", str(lp_threads))
    os.environ.pop("DISPLAY", None)
    from .render import Renderer
    from .features import TargetStats
    _R = Renderer()
    _ST = TargetStats.load(stats_path)
    _HOLD = TargetStats.load(hold_path) if hold_path else None


def _eval(payload):
    from .features import window_features
    gj, n_frames = payload
    t0 = time.time()
    try:
        g = Genome.from_json(gj)
        fr = _R.render(g, n_frames)
        f = window_features(fr, sub=2)
        d = _ST.distance(f)
        res = {"ok": True, "dist": d["total"], "groups": {k: round(v, 4) for k, v in d.items() if k != "total"},
               "zbytes": g.size(), "nodes": len(g.nodes), "luma": float(fr.mean() / 255.0),
               "motion": float(np.abs(np.diff(fr[::4].astype(np.int16), axis=0)).mean() / 255.0) if len(fr) > 4 else 0.0}
        if _HOLD is not None:
            res["hold"] = _HOLD.distance(f)["total"]
    except Exception as e:
        res = {"ok": False, "dist": 99.0, "zbytes": 10 ** 6, "err": f"{type(e).__name__}: {e}"[:300],
               "nodes": 0, "luma": 0.0, "motion": 0.0}
    res["sec"] = time.time() - t0
    return res


# ---------------------------------------------------------------------- variation with regime bias
def shrink(g, rng):
    g = g.copy()
    r = rng.random()
    n = len(g.nodes)
    if r < 0.45 and n > 2:
        cand = [i for i, nd in enumerate(g.nodes) if nd.op != "out"]
        if cand:
            _delete(g, rng.choice(cand))
    elif r < 0.85:
        i = rng.randrange(n)
        o = OPS[g.nodes[i].op]
        if o.params:
            j = rng.randrange(len(o.params))
            g.nodes[i].params[j] = o.params[j].default
    g.repair()
    g.ensure_root()
    return g.prune()


def grow(g, rng, pool):
    r = rng.random()
    if r < 0.15 and pool:
        return crossover(g, rng.choice(pool), rng).ensure_root().prune()
    h = g
    for _ in range(1 + int(rng.random() < 0.4)):
        h = mutate(h, rng, structural=0.3)
    return h.ensure_root().prune()


# ---------------------------------------------------------------------- pareto utilities
def dominates(a, b):
    return a["dist"] <= b["dist"] and a["zbytes"] <= b["zbytes"] and (a["dist"] < b["dist"] or a["zbytes"] < b["zbytes"])


class Archive:
    def __init__(self):
        self.members = []

    def add(self, c):
        if not c["ok"]:
            return False
        for m in self.members:
            if dominates(m, c) or (m["dist"] == c["dist"] and m["zbytes"] == c["zbytes"]):
                return False
        self.members = [m for m in self.members if not dominates(c, m)]
        self.members.append(c)
        return True


def leaky(x, leak=0.1):
    return x if x > 0 else leak * x


class MapElites:
    """Cells over (log2 zbytes, mean luma); keeps the closest-to-Amoebius genome per cell."""

    def __init__(self):
        self.cells = {}

    def key(self, c):
        zb = min(9, max(0, int(math.log2(max(32, c["zbytes"])) - 5)))
        lu = min(7, max(0, int(c["luma"] * 16)))
        return (zb, lu)

    def add(self, c):
        if not c["ok"]:
            return False
        k = self.key(c)
        if k not in self.cells or c["dist"] < self.cells[k]["dist"]:
            self.cells[k] = c
            return True
        return False


# ---------------------------------------------------------------------- driver
def load_seeds(patterns, rng, limit=None):
    out = []
    for pat in patterns:
        for p in sorted(glob.glob(pat, recursive=True)):
            try:
                g = Genome.load(p).ensure_root().prune()
                if not g.nodes:
                    continue
                g.meta.setdefault("src", os.path.relpath(p, "/mnt/project-files/amoebius-genome") if p.startswith("/mnt") else p)
                out.append(g)
            except Exception as e:
                print("seed load fail", p, e)
    rng.shuffle(out)
    return out[:limit] if limit else out


def run(args):
    rng = random.Random(args.seed)
    os.makedirs(args.run, exist_ok=True)
    os.makedirs(os.path.join(args.run, "best"), exist_ok=True)
    log = open(os.path.join(args.run, "log.jsonl"), "a")
    seeds = load_seeds(args.seeds, rng)
    from .handmade import amoebius_like
    seeds.append(amoebius_like())
    print(f"{len(seeds)} seeds", flush=True)
    ctx = mp.get_context("spawn")
    pool = ctx.Pool(args.workers, initializer=_init, initargs=(args.stats, args.hold, args.lp_threads))

    def evaluate(gs):
        res = pool.map(_eval, [(g.to_json(), args.frames) for g in gs], chunksize=1)
        out = []
        for g, r in zip(gs, res):
            r["genome"] = g
            out.append(r)
        return out

    t_start = time.time()
    pop = evaluate(seeds)
    bad = [c for c in pop if not c["ok"]]
    for c in bad[:5]:
        print("seed error:", c.get("err"), flush=True)
    pop = [c for c in pop if c["ok"]]
    archive, elites = Archive(), MapElites()
    for c in pop:
        archive.add(c)
        elites.add(c)
    pop.sort(key=lambda c: c["dist"])
    pop = pop[:args.mu]
    regime = COMPLEXIFY
    peak = min(c["dist"] for c in pop)
    since = 0
    evals = len(seeds)
    for gen in range(args.gens):
        parents = [c["genome"] for c in pop]
        extra = [m["genome"] for m in archive.members] + [m["genome"] for m in elites.cells.values()]
        kids = []
        for k in range(args.lam):
            src = rng.random()
            if src < 0.6:
                p = min(rng.sample(pop, min(3, len(pop))), key=lambda c: c["dist"] if regime == COMPLEXIFY else c["zbytes"])["genome"]
            else:
                p = rng.choice(extra) if extra else rng.choice(parents)
            if regime == COMPLEXIFY:
                kids.append(grow(p, rng, parents + extra))
            else:
                kids.append(shrink(p, rng) if rng.random() < 0.7 else grow(p, rng, parents))
        res = evaluate(kids)
        evals += len(kids)
        nerr = sum(1 for c in res if not c["ok"])
        for c in res:
            archive.add(c)
            elites.add(c)
        allc = pop + [c for c in res if c["ok"]]
        best_now = min(c["dist"] for c in allc)
        floor = peak / args.retain
        if regime == COMPLEXIFY:
            if best_now < peak - 1e-4:
                peak = best_now
                since = 0
            else:
                since += 1
            key = lambda c: c["dist"] + args.tiebreak * c["zbytes"]
        else:
            key = lambda c: c["zbytes"] + args.beta * leaky(c["dist"] - floor)
            if best_now < peak - 1e-4:
                peak = best_now
            prev_min = min(c["zbytes"] for c in pop if c["dist"] <= floor) if any(c["dist"] <= floor for c in pop) else 1e9
            new_min = min((c["zbytes"] for c in allc if c["dist"] <= floor), default=1e9)
            since = 0 if new_min < prev_min else since + 1
        # elitist selection with a little diversity: unique genomes by bytes
        allc.sort(key=key)
        seen, nxt = set(), []
        for c in allc:
            b = c["genome"].to_bytes()
            if b in seen:
                continue
            seen.add(b)
            nxt.append(c)
            if len(nxt) >= args.mu:
                break
        pop = nxt
        med_size = float(np.median([c["zbytes"] for c in pop]))
        flipped = False
        if regime == COMPLEXIFY and (since >= args.patience or med_size > args.budget):
            regime, since, flipped = COMPRESS, 0, True
        elif regime == COMPRESS and since >= args.patience:
            regime, since, flipped = COMPLEXIFY, 0, True
        best = min(pop, key=lambda c: c["dist"])
        simplest_ok = min((m for m in archive.members if m["dist"] <= floor), key=lambda m: m["zbytes"], default=best)
        rec = {"gen": gen, "t": round(time.time() - t_start, 1), "evals": evals, "regime": regime, "flip": flipped,
               "peak": round(peak, 4), "best_dist": round(best["dist"], 4), "best_hold": round(best.get("hold", -1), 4),
               "best_zbytes": best["zbytes"], "simplest_ok_zbytes": simplest_ok["zbytes"],
               "median_zbytes": med_size, "archive": len(archive.members), "elites": len(elites.cells),
               "errors": nerr, "sec_per_eval": round(float(np.mean([c["sec"] for c in res])), 3),
               "best_groups": best.get("groups")}
        log.write(json.dumps(rec) + "\n")
        log.flush()
        print(json.dumps({k: v for k, v in rec.items() if k != "best_groups"}), flush=True)
        if gen % args.save_every == 0 or gen == args.gens - 1:
            save_state(args.run, pop, archive, elites, gen)
    pool.close()
    save_state(args.run, pop, archive, elites, args.gens)


def save_state(run_dir, pop, archive, elites, gen):
    def pack(c):
        d = {k: v for k, v in c.items() if k != "genome"}
        d["genome"] = c["genome"].to_json()
        return d
    with open(os.path.join(run_dir, "pareto.json"), "w") as f:
        json.dump(sorted([pack(m) for m in archive.members], key=lambda d: d["zbytes"]), f)
    with open(os.path.join(run_dir, "elites.json"), "w") as f:
        json.dump([{"cell": list(k), **pack(c)} for k, c in elites.cells.items()], f)
    with open(os.path.join(run_dir, "population.json"), "w") as f:
        json.dump([pack(c) for c in pop], f)
    best = min(pop, key=lambda c: c["dist"])
    best["genome"].save(os.path.join(run_dir, "best", f"best_gen{gen:04d}.json"))
    best["genome"].save(os.path.join(run_dir, "best", "best.json"))


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True)
    ap.add_argument("--stats", required=True)
    ap.add_argument("--hold", default=None)
    ap.add_argument("--seeds", nargs="*", default=[])
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--lp-threads", type=int, default=1)
    ap.add_argument("--gens", type=int, default=200)
    ap.add_argument("--mu", type=int, default=32)
    ap.add_argument("--lam", type=int, default=32)
    ap.add_argument("--frames", type=int, default=60)
    ap.add_argument("--patience", type=int, default=8)
    ap.add_argument("--retain", type=float, default=0.97)
    ap.add_argument("--beta", type=float, default=300.0)
    ap.add_argument("--budget", type=float, default=1500.0)
    ap.add_argument("--tiebreak", type=float, default=2e-5)
    ap.add_argument("--save-every", type=int, default=5)
    ap.add_argument("--seed", type=int, default=0)
    run(ap.parse_args(argv))


if __name__ == "__main__":
    main()
