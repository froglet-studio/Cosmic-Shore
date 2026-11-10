"""Population search: regime-switching Pareto ("diamond") over (distance to Amoebius down, genome bytes down),
with a MAP-Elites side archive for diversity and a learned critic retrained as the population moves.

The regimes are Garrett's pareto_complexity.py (NCA Ecology Lab) lifted from a (1+lambda) climber to a population:
  COMPLEXIFY - select on distance to Amoebius; growth mutations (insert, duplicate, swap, crossover) dominate.
  COMPRESS   - hold distance with a leaky-ReLU floor (peak / retain), minimise compressed bytes; shrink mutations
               (delete, prune, param -> default) dominate.
Flips on plateau (patience) or when the population's median size exceeds the byte budget.

Distance to Amoebius = stat (features.py, fixed) + critic_w * softplus(-critic logit) (critic.py, retrained every
--critic-every generations on Amoebius train frames vs the frames the search just rendered).

Run:  python3 -m agenome.search --run RUN_DIR --stats STATS.npz --hold HOLD.npz --real 'frames.npy globs'
                                --seeds 'GLOB' [--workers 4] [--gens 200]
"""
import argparse
import glob
import json
import math
import multiprocessing as mp
import os
import random
import time

import numpy as np

from .genome import Genome, mutate, crossover, _delete
from .ops import OPS

COMPLEXIFY, COMPRESS = "COMPLEXIFY", "COMPRESS"

# ---------------------------------------------------------------------- worker side
_R = None
_ST = None
_HOLD = None


def _init(stats_path, hold_path, lp_threads, mem_gb=3.0):
    global _R, _ST, _HOLD
    os.environ["LP_NUM_THREADS"] = str(lp_threads)
    try:
        import resource
        lim = int(mem_gb * 2 ** 30)
        resource.setrlimit(resource.RLIMIT_AS, (lim, lim))
    except Exception:
        pass
    os.environ.pop("DISPLAY", None)
    from .render import Renderer
    from .features import TargetStats
    _R = Renderer()
    _ST = TargetStats.load(stats_path)
    _HOLD = TargetStats.load(hold_path) if hold_path else None


def _eval(payload):
    from .features import window_features
    gj, n_frames, n_judge = payload
    t0 = time.time()
    try:
        g = Genome.from_json(gj)
        fr = _R.render(g, n_frames)
        f = window_features(fr, sub=2)
        d = _ST.distance(f)
        res = {"ok": True, "stat": d["total"], "groups": {k: round(v, 4) for k, v in d.items() if k != "total"},
               "zbytes": g.size(), "nodes": len(g.nodes), "luma": float(fr.mean() / 255.0),
               "motion": float(np.abs(np.diff(fr[::4].astype(np.int16), axis=0)).mean() / 255.0) if len(fr) > 4 else 0.0,
               "frames": fr[np.linspace(0, len(fr) - 1, 4).astype(int)].copy()}
        if _HOLD is not None:
            res["hold"] = _HOLD.distance(f)["total"]
        if n_judge:
            res["jframes"] = fr[np.linspace(0, len(fr) - 1, n_judge).round().astype(int)].copy()
    except Exception as e:
        res = {"ok": False, "stat": 99.0, "zbytes": 10 ** 6, "err": f"{type(e).__name__}: {e}"[:300],
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
                g.meta.setdefault("src", p.split("amoebius-genome/")[-1])
                out.append(g)
            except Exception as e:
                print("seed load fail", p, e)
    rng.shuffle(out)
    return out[:limit] if limit else out


class JudgeState:
    """The fixed perceptual judge (agenome.judge): KID of the clip's CNN features against the training reels, over
    the level Amoebius' own held-out reel reaches (1.0). It never retrains, so a judged candidate keeps its score."""

    def __init__(self, args):
        self.on = args.judge_w > 0
        self.n = args.judge_n if self.on else 0
        self.Js = []
        if self.on:
            from .judge import Judge
            # several nets, averaged: InceptionV3 and MobileNetV2 rank 30 genomes alike only at Spearman 0.79 (each has
            # its own quirks, frame sampling is not the cause: 12 of 60 frames rank at 0.992), so the search climbs
            # their mean and cannot ride one network's blind spot
            self.Js = [Judge(net, "native", args.judge_n) for net in args.judge_net.split("+")]
            print(f"judge: {args.judge_net}, {args.judge_n} frames, levels "
                  f"{[round(J.level, 1) for J in self.Js]}", flush=True)

    def score(self, cands):
        if not self.on:
            return
        live = [c for c in cands if c.get("ok") and "jframes" in c]
        if live:
            per = [J.score_many([c["jframes"] for c in live]) for J in self.Js]
            for k, c in enumerate(live):
                c["judge"] = round(float(np.mean([p[k]["judge"] for p in per])), 4)
                c["judge_hold"] = round(float(np.mean([p[k]["judge_hold"] for p in per])), 4)
                c["judges"] = [round(p[k]["judge"], 3) for p in per]
        for c in cands:
            c.pop("jframes", None)


class CriticState:
    def __init__(self, args):
        self.on = bool(args.real) and args.critic_w > 0
        self.w = args.critic_w
        self.stat_w, self.judge_w = args.stat_w, args.judge_w
        self.c = None
        self.buf = []
        self.real = None
        self.hold = None
        self.rng = np.random.default_rng(args.seed)
        if self.on:
            from .critic import Critic
            reals = [np.load(p) for pat in args.real for p in sorted(glob.glob(pat))]
            self.real = np.concatenate(reals) if reals else None
            if args.real_hold:
                hs = [np.load(p) for p in sorted(glob.glob(args.real_hold))]
                self.hold = np.concatenate(hs)[::4] if hs else None
            self.c = Critic(seed=args.seed)
            self.trained = False
            print(f"critic: {len(self.real)} real frames", flush=True)

    def add_fakes(self, cands):
        for c in cands:
            if c.get("ok") and "frames" in c:
                self.buf.append(c["frames"])
        self.buf = self.buf[-400:]

    def train(self, steps):
        if not self.on or not self.buf:
            return {}
        fake = np.concatenate(self.buf[-200:])
        t0 = time.time()
        loss = self.c.train(self.real, fake, steps=steps, rng=self.rng)
        self.trained = True
        info = {"critic_loss": round(loss, 4), "critic_sec": round(time.time() - t0, 1)}
        if self.hold is not None:
            info["critic_hold_logit"] = round(float(self.c.logits(self.hold).mean()), 3)
        info["critic_fake_logit"] = round(float(self.c.logits(fake[-64:]).mean()), 3)
        return info

    def score(self, cands):
        """Set c['crit'] (mean logit) and c['dist'] (stat + w * softplus(-logit)) for candidates with frames."""
        from .critic import critic_distance
        live = [c for c in cands if c.get("ok") and "frames" in c]
        if self.on and self.trained and live:
            lg = self.c.logits(np.concatenate([c["frames"] for c in live]))
            for k, c in enumerate(live):
                c["crit"] = float(lg[4 * k:4 * k + 4].mean())
        for c in cands:
            if not c.get("ok"):
                c["dist"] = 99.0
                continue
            cd = critic_distance(c["crit"]) if (self.on and self.trained and "crit" in c) else 0.0
            jd = c.get("judge", 10.0) if self.judge_w > 0 else 0.0
            c["dist"] = self.stat_w * c["stat"] + self.w * cd + self.judge_w * jd


def run(args):
    rng = random.Random(args.seed)
    os.makedirs(args.run, exist_ok=True)
    os.makedirs(os.path.join(args.run, "best"), exist_ok=True)
    log = open(os.path.join(args.run, "log.jsonl"), "a")
    with open(os.path.join(args.run, "args.json"), "w") as f:
        json.dump(vars(args), f, indent=1)
    seeds = load_seeds(args.seeds, rng)
    from .handmade import amoebius_like, amoebius_v2
    seeds += [amoebius_like(), amoebius_v2(), amoebius_v2(seed=9)]
    if args.resume:
        skipped = 0
        for p in args.resume:
            try:
                rows = json.load(open(p))
            except Exception as e:
                print("resume skip", p, type(e).__name__, e, flush=True)
                continue
            for d in rows:
                try:
                    seeds.append(Genome.from_json(d["genome"]))
                except Exception:  # ops this code does not know yet
                    skipped += 1
        if skipped:
            print(f"resume: skipped {skipped} genomes this code cannot decode", flush=True)
    print(f"{len(seeds)} seeds", flush=True)
    ctx = mp.get_context("spawn")
    state = {"pool": None}
    crit = CriticState(args)
    judge = JudgeState(args)

    def new_pool():
        if state["pool"] is not None:
            state["pool"].terminate()
        state["pool"] = ctx.Pool(args.workers, initializer=_init,
                                 initargs=(args.stats, args.hold, args.lp_threads, args.mem_gb))
    new_pool()

    def evaluate(gs):
        payload = [(g.to_json(), args.frames, judge.n) for g in gs]
        jobs = [state["pool"].apply_async(_eval, (p,)) for p in payload]
        res = []
        deadline = time.time() + 60 + 6.0 * len(jobs) / max(1, args.workers)
        broken = False
        for j in jobs:
            try:
                if broken:
                    raise mp.TimeoutError()
                res.append(j.get(timeout=max(1.0, deadline - time.time())))
            except mp.TimeoutError:
                broken = True
                res.append({"ok": False, "stat": 99.0, "zbytes": 10 ** 6, "err": "timeout", "nodes": 0,
                            "luma": 0.0, "motion": 0.0, "sec": 0.0})
            except Exception as e:
                res.append({"ok": False, "stat": 99.0, "zbytes": 10 ** 6, "err": f"worker: {e}"[:200], "nodes": 0,
                            "luma": 0.0, "motion": 0.0, "sec": 0.0})
        if broken:
            print("evaluation timed out; restarting the worker pool", flush=True)
            new_pool()
        for g, r in zip(gs, res):
            r["genome"] = g
        crit.add_fakes(res)
        judge.score(res)
        crit.score(res)
        return res

    t_start = time.time()
    pop = evaluate(seeds)
    for c in [c for c in pop if not c["ok"]][:5]:
        print("seed error:", c.get("err"), flush=True)
    pop = [c for c in pop if c["ok"]]
    archive, elites = Archive(), MapElites()

    def rebuild():
        nonlocal archive, elites
        everyone = {id(c): c for c in pop + archive.members + list(elites.cells.values())}.values()
        crit.score(list(everyone))
        archive, elites = Archive(), MapElites()
        for c in everyone:
            archive.add(c)
            elites.add(c)

    if crit.on:
        info = crit.train(args.critic_steps * 2)
        print("critic", info, flush=True)
        rebuild()
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
        cinfo = {}
        if crit.on and gen > 0 and gen % args.critic_every == 0:
            cinfo = crit.train(args.critic_steps)
            rebuild()
            pop.sort(key=lambda c: c["dist"])
            peak = min(c["dist"] for c in pop)  # the yardstick moved: re-anchor the floor
        parents = [c["genome"] for c in pop]
        extra = [m["genome"] for m in archive.members] + [m["genome"] for m in elites.cells.values()]
        kids = []
        if args.share and gen > 0 and gen % args.migrate_every == 0:
            kids += immigrants(args, rng, k=args.migrants)
        n_tune = int(round(args.lam * args.tune))
        best_first = sorted(pop, key=lambda c: c["dist"])
        for k in range(max(0, args.lam - len(kids))):
            if k < n_tune:
                # TUNE: parameter-only steps around the current best few (a hill climb inside the population);
                # the seed thread's 150-eval param climb took a seed from 4.40 to 1.57 where structural search stalled
                p = best_first[min(int(rng.expovariate(0.7)), len(best_first) - 1)]["genome"]
                kids.append(mutate(p, rng, structural=0.0).ensure_root().prune())
                continue
            if rng.random() < 0.6:
                p = min(rng.sample(pop, min(3, len(pop))),
                        key=lambda c: c["dist"] if regime == COMPLEXIFY else c["zbytes"])["genome"]
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
            ok_pop = [c["zbytes"] for c in pop if c["dist"] <= floor]
            prev_min = min(ok_pop) if ok_pop else 1e9
            new_min = min((c["zbytes"] for c in allc if c["dist"] <= floor), default=1e9)
            since = 0 if new_min < prev_min else since + 1
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
               "peak": round(peak, 4), "best_dist": round(best["dist"], 4), "best_stat": round(best["stat"], 4),
               "best_crit": round(best.get("crit", 0.0), 3), "best_hold": round(best.get("hold", -1), 4),
               **({"best_judge": best.get("judge"), "best_judge_hold": best.get("judge_hold"),
                   "top_judge": min((c.get("judge", 99) for c in allc), default=None)} if judge.on else {}),
               "best_zbytes": best["zbytes"], "simplest_ok_zbytes": simplest_ok["zbytes"],
               "median_zbytes": med_size, "archive": len(archive.members), "elites": len(elites.cells),
               "errors": nerr, "sec_per_eval": round(float(np.mean([c["sec"] for c in res])), 3),
               "best_src": best["genome"].meta.get("src", best["genome"].meta.get("name", "")),
               **cinfo, "best_groups": best.get("groups")}
        log.write(json.dumps(rec) + "\n")
        log.flush()
        print(json.dumps({k: v for k, v in rec.items() if k != "best_groups"}), flush=True)
        if gen % args.save_every == 0 or gen == args.gens - 1:
            save_state(args.run, pop, archive, elites, gen)
            publish(args, args.run)
    state["pool"].close()
    save_state(args.run, pop, archive, elites, args.gens)


def immigrants(args, rng, k=4):
    """Best genomes other islands published under --share (their pareto.json), re-evaluated here."""
    out = []
    me = os.path.abspath(os.path.join(args.share, args.island)) if args.island else None
    for p in glob.glob(os.path.join(args.share, "*", "pareto.json")):
        if me and os.path.abspath(os.path.dirname(p)) == me:
            continue
        try:
            data = json.load(open(p))
        except Exception:
            continue
        data = sorted(data, key=lambda d: d.get("dist", 99))[:max(1, k)]
        for d in data:
            try:
                g = Genome.from_json(d["genome"])
            except Exception:  # ops this code does not know yet (an island on newer code): skip, never crash
                continue
            g.meta["immigrant_from"] = os.path.basename(os.path.dirname(p))
            out.append(g)
    rng.shuffle(out)
    return out[:k]


def publish(args, run_dir):
    if not args.share or not args.island:
        return
    dst = os.path.join(args.share, args.island)
    os.makedirs(dst, exist_ok=True)
    import shutil
    for name in ("pareto.json", "elites.json", "log.jsonl", "args.json"):
        src = os.path.join(run_dir, name)
        if os.path.exists(src):
            shutil.copyfile(src, os.path.join(dst, name + ".tmp"))
            os.replace(os.path.join(dst, name + ".tmp"), os.path.join(dst, name))
    b = os.path.join(run_dir, "best", "best.json")
    if os.path.exists(b):
        shutil.copyfile(b, os.path.join(dst, "best.json"))


def save_state(run_dir, pop, archive, elites, gen):
    def pack(c):
        d = {k: v for k, v in c.items() if k not in ("genome", "frames")}
        d["genome"] = c["genome"].to_json()
        return d
    tmp = lambda n: os.path.join(run_dir, n + ".tmp")
    for name, data in (("pareto.json", sorted([pack(m) for m in archive.members], key=lambda d: d["zbytes"])),
                       ("elites.json", [{"cell": list(k), **pack(c)} for k, c in elites.cells.items()]),
                       ("population.json", [pack(c) for c in pop])):
        with open(tmp(name), "w") as f:
            json.dump(data, f)
        os.replace(tmp(name), os.path.join(run_dir, name))
    best = min(pop, key=lambda c: c["dist"])
    best["genome"].save(os.path.join(run_dir, "best", f"best_gen{gen:04d}.json"))
    best["genome"].save(os.path.join(run_dir, "best", "best.json"))


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True)
    ap.add_argument("--stats", required=True)
    ap.add_argument("--hold", default=None)
    ap.add_argument("--real", nargs="*", default=[], help="frames.npy globs: Amoebius frames the critic calls real")
    ap.add_argument("--real-hold", default=None, help="frames.npy glob: held-out Amoebius frames (critic check)")
    ap.add_argument("--tune", type=float, default=0.25, help="share of each generation spent on parameter-only steps around the best")
    ap.add_argument("--ss", type=int, default=None, help="supersampling factor for every render (sets AGENOME_SS; default 1)")
    ap.add_argument("--critic-w", type=float, default=0.5)
    ap.add_argument("--judge-w", type=float, default=0.0,
                    help="weight of the fixed perceptual judge (agenome.judge; 1.0 = Amoebius' own level); 0 = off")
    ap.add_argument("--judge-net", default="inception+mobilenet", help="nets averaged by the judge, joined by +")
    ap.add_argument("--judge-n", type=int, default=12, help="frames per clip the judge sees")
    ap.add_argument("--stat-w", type=float, default=1.0, help="weight of the fixed statistics in dist")
    ap.add_argument("--critic-every", type=int, default=6)
    ap.add_argument("--critic-steps", type=int, default=120)
    ap.add_argument("--seeds", nargs="*", default=[])
    ap.add_argument("--resume", nargs="*", default=[], help="population/pareto/elites json files to add as seeds")
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--lp-threads", type=int, default=1)
    ap.add_argument("--mem-gb", type=float, default=3.0)
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
    ap.add_argument("--share", default=None, help="shared folder of islands, e.g. /mnt/project-files/amoebius-genome/runs")
    ap.add_argument("--island", default=None, help="this island's name under --share")
    ap.add_argument("--migrate-every", type=int, default=10)
    ap.add_argument("--migrants", type=int, default=4)
    args = ap.parse_args(argv)
    if args.ss:
        os.environ["AGENOME_SS"] = str(args.ss)  # workers inherit it; every Renderer reads it
    run(args)


if __name__ == "__main__":
    main()
