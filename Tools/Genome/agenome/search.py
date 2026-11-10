"""Population search over (distance to Amoebius down, genome bytes down), with a MAP-Elites side archive for
diversity and a learned critic retrained as the population moves.

Four pieces, each switchable so any run can be compared with the search it replaced (agenome/selection.py):
  --survival pareto   (default) non-dominated sorting inside a region of interest (dist within --span of the best,
                      bytes under --budget), each front ordered by hypervolume contribution (SMS-EMOA): the best
                      genome and the smallest genome near it survive together, every generation.
             diamond  the regime switch of Garrett's pareto_complexity.py (NCA Ecology Lab) lifted to a population:
                      COMPLEXIFY selects on distance with growth mutations; COMPRESS holds distance above a leaky-ReLU
                      floor (peak / retain) and minimises bytes with shrink mutations; flips on plateau (--patience)
                      or when the median size passes --budget.
  --parents tournament  (default) a tournament of 3 on distance (bytes in COMPRESS);
            lexicase    epsilon-lexicase over the feature groups, judge nets, critic and size.
  --operators fixed     (default) the regime's mix (COMPLEXIFY grows, COMPRESS shrinks 70%, PARETO shrinks --shrink);
              adaptive  probability matching over param / struct / xover / shrink, credited by each child's gain
                        over its parent.
  --tuner cma         (default) the --tune share is a sep-CMA-ES emitter on the best genome's parameter bytes;
          random      the --tune share is random byte tweaks around the best few.
The A/B that chose the defaults: SEARCH.md beside this package.

Distance to Amoebius = stat (features.py, fixed) + critic_w * softplus(-critic logit) (critic.py, retrained every
--critic-every generations on Amoebius train frames vs the frames the search just rendered) + judge_w * judge.

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
from .selection import CMAEmitter, Lexicase, Matching, hypervolume, roi_bound, roi_order
from .steward import genome_diff

COMPLEXIFY, COMPRESS, PARETO = "COMPLEXIFY", "COMPRESS", "PARETO"
ARMS = ("param", "struct", "xover", "shrink")
# the fixed search's mix, roughly: grow() is 15% crossover and otherwise one or two mutations at 30% structural, and
# the PARETO fixed mix shrinks a quarter of the time
PRIOR_MIX = {"param": 0.40, "struct": 0.25, "xover": 0.12, "shrink": 0.23}

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
    """Render and score one genome. payload = (genome json, frames, judge frames, windows[, hop]): windows is either a
    list of start offsets in seconds (each window renders with t_shift = offset) or a count n, meaning offsets
    0, hop, 2 hop, ... (the older form, still used by agenome.tune). The stat and held-out stat are the means over the
    windows, each window's judge frames go to the judge, and groups, luma, motion and the critic's 4 frames come from
    the first window."""
    from .features import window_features
    gj, n_frames, n_judge = payload[:3]
    wins = payload[3] if len(payload) > 3 else 1
    if isinstance(wins, (list, tuple)):
        offsets = [float(t) for t in wins] or [0.0]
    else:
        hop = float(payload[4]) if len(payload) > 4 else 8.0
        offsets = [k * hop for k in range(max(1, int(wins)))]
    t0 = time.time()
    try:
        g = Genome.from_json(gj)
        stats, holds, jfr, first = [], [], [], None
        for ts in offsets:
            # later windows of the same phenotype: judged, and the stat averages over them too, so neither term can be
            # met by fitting one stretch of time (2026-11-10: genomes judged 0.87 on the first window read 2.0 to 3.9
            # later, and genomes climbed on two fixed windows read 0.3 to 0.6 worse on windows they never saw)
            frk = _R.render(g, n_frames, t_shift=ts)
            fk = window_features(frk, sub=2)
            dk = _ST.distance(fk)
            stats.append(dk["total"])
            if _HOLD is not None:
                holds.append(_HOLD.distance(fk)["total"])
            if n_judge:
                pick = np.linspace(0, len(frk) - 1, n_judge).round().astype(int)
                jfr.append(frk[pick].copy())
            if first is None:
                first = (frk, dk)
        fr, d = first
        res = {"ok": True, "stat": float(np.mean(stats)),
               "groups": {k: round(v, 4) for k, v in d.items() if k != "total"},
               "zbytes": g.size(), "nodes": len(g.nodes), "luma": float(fr.mean() / 255.0),
               "motion": float(np.abs(np.diff(fr[::4].astype(np.int16), axis=0)).mean() / 255.0) if len(fr) > 4 else 0.0,
               "frames": fr[np.linspace(0, len(fr) - 1, 4).astype(int)].copy(),
               "stat_windows": [round(x, 4) for x in stats], "stat_nwin": len(offsets),
               "win_t": [round(t, 3) for t in offsets]}
        if holds:
            res["hold"] = float(np.mean(holds))
            res["hold_windows"] = [round(x, 4) for x in holds]
        if n_judge:
            res["jframes"] = jfr
    except Exception as e:
        res = {"ok": False, "stat": 99.0, "zbytes": 10 ** 6, "err": f"{type(e).__name__}: {e}"[:300],
               "nodes": 0, "luma": 0.0, "motion": 0.0}
    res["sec"] = time.time() - t0
    return res


def window_grid(args):
    """The offsets a genome's judged windows are drawn from: random mode, 0 to --window-span on a --window-step grid;
    fixed mode, the --judge-windows offsets 0, hop, 2 hop, ..."""
    if getattr(args, "window_mode", "fixed") == "random":
        n = int(round(args.window_span / args.window_step)) + 1
        return [round(k * args.window_step, 3) for k in range(n)]
    return [round(k * args.judge_hop, 3) for k in range(max(1, args.judge_windows))]


def draw_offsets(args, rng, exclude=()):
    """The window offsets for one evaluation. Random mode: --judge-windows offsets drawn from the grid without
    replacement, skipping offsets this genome was already judged on (empty when none are left); fixed mode: the
    fixed offsets (empty when they are all in exclude, so a fixed-mode genome is never re-judged on the same windows)."""
    grid = window_grid(args)
    seen = {round(float(t), 3) for t in exclude}
    if getattr(args, "window_mode", "fixed") != "random":
        return [] if seen.issuperset(grid) and exclude else grid
    free = [t for t in grid if t not in seen]
    k = max(1, args.judge_windows)
    return sorted(rng.sample(free, k)) if len(free) >= k else []


def merge_windows(c, r):
    """Fold r, a re-evaluation of c's genome on other windows, into c: every score becomes the window-weighted mean
    over all the windows judged so far, so a long-lived genome converges on its look over the whole span instead of
    the luck of its first draw. The critic frames, groups, luma and motion stay those of c's first evaluation."""
    n0, n1 = int(c.get("judge_nwin", 1)), int(r.get("judge_nwin", 1))
    m0, m1 = int(c.get("stat_nwin", 1)), int(r.get("stat_nwin", 1))

    def wavg(a, b, wa, wb):
        return (a * wa + b * wb) / float(wa + wb)

    for k in ("judge", "judge_hold"):
        if k in c and k in r:
            c[k] = round(wavg(c[k], r[k], n0, n1), 4)
    for k in ("judges", "judges_hold"):
        if k in c and k in r and len(c[k]) == len(r[k]):
            c[k] = [round(wavg(x, y, n0, n1), 3) for x, y in zip(c[k], r[k])]
    for k in ("stat", "hold"):
        if k in c and k in r:
            c[k] = wavg(c[k], r[k], m0, m1)
    for k in ("judge_windows", "judge_hold_windows", "stat_windows", "hold_windows", "win_t"):
        if k in c and k in r:
            c[k] = list(c[k]) + list(r[k])
    if "judge_nwin" in c and "judge_nwin" in r:
        c["judge_nwin"] = n0 + n1
    c["stat_nwin"] = m0 + m1
    c["rejudged"] = int(c.get("rejudged", 0)) + 1
    return c


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


def vary(p, rng, regime, donors, parents, aos=None, shrink_p=0.25):
    """One child of p and the name of the operator that made it. Adaptive (aos given): the arm aos picks, i.e. one or
    two parameter tweaks, a structural mutation (swap, insert, delete, rewire, dup), a typed subgraph crossover with a
    donor, or a shrink. Fixed: COMPLEXIFY grows; COMPRESS shrinks 70% of the time; PARETO shrinks shrink_p of the
    time and grows otherwise."""
    if aos is not None:
        arm = aos.pick(rng)
        if arm == "param":
            h = p
            for _ in range(1 + int(rng.random() < 0.4)):
                h = mutate(h, rng, structural=0.0)
            return h.ensure_root().prune(), arm
        if arm == "struct":
            h = mutate(p, rng, structural=1.0)
            if rng.random() < 0.4:
                h = mutate(h, rng, structural=0.3)
            return h.ensure_root().prune(), arm
        if arm == "xover" and donors:
            return crossover(p, rng.choice(donors), rng).ensure_root().prune(), arm
        return shrink(p, rng), "shrink"
    if regime == COMPLEXIFY:
        return grow(p, rng, donors), "grow"
    if regime == COMPRESS:
        return (shrink(p, rng), "shrink") if rng.random() < 0.7 else (grow(p, rng, parents), "grow")
    return (shrink(p, rng), "shrink") if rng.random() < shrink_p else (grow(p, rng, donors), "grow")


def lexicase_matrix(cands, critic_on=False):
    """The cases epsilon-lexicase selects parents on: each feature group's distance (features.GROUPS, first window),
    each judge net's score, the critic's term and log bytes. A case a candidate lacks is nan (never best)."""
    from .features import GROUPS
    from .critic import critic_distance
    rows = []
    nj = max((len(c.get("judges") or []) for c in cands), default=0)
    for c in cands:
        g = c.get("groups") or {}
        row = [float(g.get(k, np.nan)) for k in GROUPS]
        js = list(c.get("judges") or [])
        row += [float(x) for x in js] + [np.nan] * (nj - len(js))
        if critic_on:
            row.append(critic_distance(c["crit"]) if "crit" in c else np.nan)
        row.append(math.log(max(1, c["zbytes"])))
        rows.append(row)
    return np.asarray(rows, float)


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


def lineage_of(c):
    """The root seed a genome descends from: children copy their parent's meta, so the seed's name survives (tuning
    suffixes such as __tuned or __jt are dropped, so a tuned seed and its descendants count as one lineage)."""
    return lineage_name(c["genome"])


def lineage_name(g):
    """lineage_of for a bare Genome."""
    m = g.meta or {}
    name = m.get("name") or os.path.splitext(os.path.basename(m.get("src", "") or ""))[0] or "?"
    return name.split("__")[0]


def survivors(cands, key, mu, cap_share):
    """The mu best by key, unique by bytes, with at most ceil(cap_share * mu) from any one lineage (0 = no cap); the
    cap is filled from the overflow only when too few lineages remain. Process mining (2026-11-10) found the 12
    genomes closest to Amoebius on six islands all descended from one tuned seed: cross-island resume and immigrants
    had collapsed the population to a monoculture."""
    cap = max(1, int(np.ceil(cap_share * mu))) if cap_share > 0 else None
    seen, out, spill, count = set(), [], [], {}
    for c in sorted(cands, key=key):
        b = c["genome"].to_bytes()
        if b in seen:
            continue
        seen.add(b)
        L = lineage_of(c)
        if cap and count.get(L, 0) >= cap:
            spill.append(c)
            continue
        count[L] = count.get(L, 0) + 1
        out.append(c)
        if len(out) >= mu:
            return out
    return out + spill[:mu - len(out)]


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
            # several nets: InceptionV3 and MobileNetV2 rank 30 genomes alike only at Spearman 0.79 (each has its own
            # quirks; frame sampling is not the cause: 12 of 60 frames rank at 0.992). Their MEAN was ridden anyway: by
            # 18:20 on 2026-11-10 the 13 best of 325 judged genomes read 0.86 on Inception and 1.21 on MobileNet (the
            # search learned to please one net), while genomes judged 2.2-4 read the same on both (ratio 1.02). So the
            # score is the LARGER of the two (v3): a genome has to look like Amoebius to both nets
            self.Js = [Judge(net, "native", args.judge_n) for net in args.judge_net.split("+")]
            self.agg = np.max if args.judge_agg == "max" else np.mean
            self.version = 3 if args.judge_agg == "max" else self.Js[0].version
            print(f"judge v{self.version}: {args.judge_net} ({args.judge_agg}), {args.judge_n} frames, levels "
                  f"{[round(J.level, 1) for J in self.Js]}", flush=True)

    def score(self, cands):
        if not self.on:
            return
        live = [c for c in cands if c.get("ok") and "jframes" in c]
        if live:
            clips, owner = [], []
            for k, c in enumerate(live):
                ws = c["jframes"] if isinstance(c["jframes"], list) else [c["jframes"]]
                clips += ws
                owner += [k] * len(ws)
            per = [J.score_many(clips) for J in self.Js]  # per[net][clip]
            for k, c in enumerate(live):
                idx = [i for i, o in enumerate(owner) if o == k]
                # in each judged window the nets combine (v3: the worse of the two), then the windows average: the
                # same order the footage calibration uses (calibration_max_native_v3.json), so the numbers compare
                win = [float(self.agg([p[i]["judge"] for p in per])) for i in idx]
                win_h = [float(self.agg([p[i]["judge_hold"] for p in per])) for i in idx]
                c["judge"] = round(float(np.mean(win)), 4)
                c["judge_hold"] = round(float(np.mean(win_h)), 4)
                c["judges"] = [round(float(np.mean([p[i]["judge"] for i in idx])), 3) for p in per]
                c["judges_hold"] = [round(float(np.mean([p[i]["judge_hold"] for i in idx])), 3) for p in per]
                c["judge_windows"] = [round(x, 3) for x in win]
                c["judge_hold_windows"] = [round(x, 3) for x in win_h]
                c["judge_nwin"] = len(idx)
                c["judge_v"] = self.version
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

    def run_payload(payload):
        """Evaluate payloads on the worker pool; a hung evaluation restarts the pool instead of stalling the island."""
        jobs = [state["pool"].apply_async(_eval, (p,)) for p in payload]
        res = []
        nwin = max([len(p[3]) if isinstance(p[3], (list, tuple)) else int(p[3]) for p in payload] or [1])
        deadline = time.time() + 60 + 6.0 * max(1, nwin) * len(jobs) / max(1, args.workers)
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
        return res

    def evaluate(gs):
        payload = [(g.to_json(), args.frames, judge.n, draw_offsets(args, rng)) for g in gs]
        res = run_payload(payload)
        for g, r in zip(gs, res):
            r["genome"] = g
        crit.add_fakes(res)
        judge.score(res)
        crit.score(res)
        return res

    def rejudge(cands):
        """Random windows: judge each candidate again on windows it has not been judged on and fold them in
        (merge_windows), so survivors' scores converge on the whole span. Returns how many were re-judged."""
        todo = []
        for c in cands:
            if c.get("ok"):
                o = draw_offsets(args, rng, exclude=c.get("win_t", []))
                if o:
                    todo.append((c, o))
        if not todo:
            return 0
        res = run_payload([(c["genome"].to_json(), args.frames, judge.n, o) for c, o in todo])
        judge.score(res)
        n = 0
        for (c, _), r in zip(todo, res):
            if r.get("ok"):
                merge_windows(c, r)
                n += 1
        return n

    audits = {}

    def audit(c):
        """Judge c on the --audit-offsets windows, which selection never uses (cached by bytes): the honest number for
        a published best. Stored on the row as c['audit']."""
        if not args.audit_offsets or not c.get("ok"):
            return None
        key = c["genome"].to_bytes()
        if key not in audits:
            (r,) = run_payload([(c["genome"].to_json(), args.frames, judge.n, list(args.audit_offsets))])
            judge.score([r])
            audits[key] = ({"judge": r.get("judge"), "judge_windows": r.get("judge_windows"), "t": r.get("win_t"),
                            "stat": round(r["stat"], 4)} if r.get("ok") else {"err": r.get("err")})
        c["audit"] = audits[key]
        return c["audit"]

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
    pop = survivors(pop, lambda c: c["dist"], args.mu, args.lineage_cap)
    pareto = args.survival == "pareto"
    regime = PARETO if pareto else COMPLEXIFY
    peak = min(c["dist"] for c in pop)
    since = 0
    evals = len(seeds)
    aos = Matching(ARMS, p0=PRIOR_MIX, p_min=args.aos_min, alpha=args.aos_rate) \
        if args.operators == "adaptive" else None
    emitter = CMAEmitter(block=args.cma_block, gens_per_block=args.cma_gens, sigma=args.cma_sigma,
                         sigma_min=args.cma_sigma_min, seed=args.seed, blocks=args.cma_blocks) \
        if args.tuner == "cma" else None
    for gen in range(args.gens):
        cinfo = {}
        moved = False
        if args.rejudge_every > 0 and gen > 0 and gen % args.rejudge_every == 0:
            # random windows: survivors and the published front get fresh windows folded into their scores, so a lucky
            # first draw does not keep a genome on top (each is judged at most once per window offset)
            cinfo["rejudged"] = rejudge(list({id(c): c for c in pop + archive.members}.values()))
            evals += cinfo["rejudged"]
            moved = cinfo["rejudged"] > 0
        if crit.on and gen > 0 and gen % args.critic_every == 0:
            cinfo.update(crit.train(args.critic_steps))
            moved = True
        if moved:
            rebuild()
            pop.sort(key=lambda c: c["dist"])
            peak = min(c["dist"] for c in pop)  # the yardstick moved: re-anchor the floor
        parents = [c["genome"] for c in pop]
        extra = [m["genome"] for m in archive.members] + [m["genome"] for m in elites.cells.values()]
        extra_c = archive.members + list(elites.cells.values())
        kids, tags, pdist, pcand = [], [], [], []
        if args.share and args.migrate_every > 0 and gen > 0 and gen % args.migrate_every == 0:
            imm = immigrants(args, rng, k=args.migrants, have={lineage_of(c) for c in pop})
            kids += imm
            tags += ["imm"] * len(imm)
            pdist += [None] * len(imm)
            pcand += [None] * len(imm)
        n_tune = min(int(round(args.lam * args.tune)), max(0, args.lam - len(kids)))
        best_first = sorted(pop, key=lambda c: c["dist"])
        n_cma, i_cma = 0, len(kids)
        if emitter is not None and n_tune:
            # TUNE as sep-CMA-ES on the leader's parameter bytes: the offline tuner's optimiser (agenome.tune) run as
            # an emitter inside the loop, so every structure that takes the lead is tuned while the structural search
            # goes on
            ck = emitter.ask(best_first[0], n_tune)
            n_cma = len(ck)
            kids += ck
            tags += ["cma"] * n_cma
            pdist += [emitter.anchor_dist] * n_cma
            pcand += [emitter.anchor_cand] * n_cma
        for _ in range(n_tune - n_cma):
            # TUNE: parameter-only steps around the current best few (a hill climb inside the population);
            # the seed thread's 150-eval param climb took a seed from 4.40 to 1.57 where structural search stalled
            pc = best_first[min(int(rng.expovariate(0.7)), len(best_first) - 1)]
            kids.append(mutate(pc["genome"], rng, structural=0.0).ensure_root().prune())
            tags.append("tune")
            pdist.append(pc["dist"])
            pcand.append(pc)
        lex = Lexicase(lexicase_matrix(pop, crit.on and crit.trained)) if args.parents == "lexicase" else None
        donors = parents + extra
        while len(kids) < args.lam:
            if rng.random() < 0.6:
                if lex is not None:
                    pc = pop[lex.select(rng)]
                else:
                    pc = min(rng.sample(pop, min(3, len(pop))),
                             key=lambda c: c["zbytes"] if regime == COMPRESS else c["dist"])
            else:
                pc = rng.choice(extra_c) if extra_c else rng.choice(pop)
            child, tag = vary(pc["genome"], rng, regime, donors, parents, aos, args.shrink)
            kids.append(child)
            tags.append(tag)
            pdist.append(pc["dist"])
            pcand.append(pc)
        res = evaluate(kids)
        evals += len(kids)
        for c, t in zip(res, tags):
            c["op"] = t
        if n_cma:
            emitter.tell([c["dist"] for c in res[i_cma:i_cma + n_cma]])
        nerr = sum(1 for c in res if not c["ok"])
        for c in res:
            archive.add(c)
            elites.add(c)
        allc = pop + [c for c in res if c["ok"]]
        best_now = min(c["dist"] for c in allc)
        if pareto:
            peak = best_now
            floor = roi_bound(peak, args.span)
            order = roi_order([(c["dist"], c["zbytes"]) for c in allc], args.span, args.budget)
            rank = {id(allc[i]): r for r, i in enumerate(order)}
            key = lambda c: rank[id(c)]
        else:
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
        pop = survivors(allc, key, args.mu, args.lineage_cap)
        med_size = float(np.median([c["zbytes"] for c in pop]))
        flipped = False
        if regime == COMPLEXIFY and (since >= args.patience or med_size > args.budget):
            regime, since, flipped = COMPRESS, 0, True
        elif regime == COMPRESS and since >= args.patience:
            regime, since, flipped = COMPLEXIFY, 0, True
        # per operator: children, survivors, and the mean relative gain over the parent's dist (clipped at 25%, 0 for
        # a child that is worse or failed). The gain is the adaptive operator choice's credit: survival credit was
        # measured paying shrink for filling the compact end of the band while the best dist stalled (2026-11-10)
        alive = {id(c) for c in pop}
        opstat, gains = {}, {}
        for c, pd in zip(res, pdist):
            t = c.get("op", "?")
            s = opstat.setdefault(t, [0, 0, 0.0])
            s[0] += 1
            s[1] += int(id(c) in alive)
            gain = 0.0
            if pd is not None and c.get("ok"):
                gain = min(0.25, max(0.0, (pd - c["dist"]) / max(abs(pd), 1e-9)))
            s[2] += gain
            gains.setdefault(t, []).append(gain)
        for s in opstat.values():
            s[2] = round(s[2] / max(1, s[0]), 4)
        if aos is not None:
            aos.update({t: g for t, g in gains.items() if t in aos.arms})
        best = min(pop, key=lambda c: c["dist"])
        # the steward's evidence (agenome/steward.py): every child that beat its parent and survived, with the edit
        # that did it and how each feature group moved
        events = []
        for c, pc in zip(res, pcand):
            if pc is None or not c.get("ok") or id(c) not in alive or c["dist"] >= pc["dist"]:
                continue
            ev = {"gen": gen, "evals": evals, "op": c.get("op"), "parent_dist": round(pc["dist"], 4),
                  "dist": round(c["dist"], 4), "gain": round((pc["dist"] - c["dist"]) / max(abs(pc["dist"]), 1e-9), 5),
                  "zbytes": c["zbytes"], "parent_zbytes": pc.get("zbytes"), "leader": c is best,
                  "src": lineage_of(c), "edit": genome_diff(pc["genome"], c["genome"])}
            if c.get("groups") and pc.get("groups"):
                ev["groups_delta"] = {k: round(c["groups"][k] - pc["groups"][k], 4) for k in c["groups"]
                                      if k in pc["groups"]}
            events.append(ev)
        if events:
            with open(os.path.join(args.run, "improvements.jsonl"), "a") as f:
                f.write("".join(json.dumps(e) + "\n" for e in events))
        aud = audit(best) if judge.on else None
        simplest_ok = min((m for m in archive.members if m["dist"] <= floor), key=lambda m: m["zbytes"], default=best)
        front = [(m["dist"], m["zbytes"]) for m in archive.members]
        rec = {"gen": gen, "t": round(time.time() - t_start, 1), "evals": evals, "regime": regime, "flip": flipped,
               "peak": round(peak, 4), "best_dist": round(best["dist"], 4), "best_stat": round(best["stat"], 4),
               "best_crit": round(best.get("crit", 0.0), 3), "best_hold": round(best.get("hold", -1), 4),
               **({"best_judge": best.get("judge"), "best_judge_hold": best.get("judge_hold"),
                   "best_judge_nwin": best.get("judge_nwin"),
                   "top_judge": min((c.get("judge", 99) for c in allc), default=None)} if judge.on else {}),
               **({"best_audit": aud.get("judge"), "best_audit_windows": aud.get("judge_windows")} if aud else {}),
               "best_zbytes": best["zbytes"], "simplest_ok_zbytes": simplest_ok["zbytes"],
               "median_zbytes": med_size, "archive": len(archive.members), "elites": len(elites.cells),
               "errors": nerr, "sec_per_eval": round(float(np.mean([c["sec"] for c in res])), 3),
               "best_src": best["genome"].meta.get("src", best["genome"].meta.get("name", "")),
               # the front inside the band (dist <= floor, bytes < budget): how many survivors live there and the
               # area the archive covers there
               "floor": round(floor, 4), "in_band": sum(1 for c in pop if c["dist"] <= floor),
               "hv_band": round(hypervolume(front, (floor, args.budget)), 3),
               "ops": opstat, "best_op": best.get("op"), **({"aos": aos.state()} if aos is not None else {}),
               **(emitter.state() if emitter is not None else {}),
               **({"front": [[round(d, 4), z] for d, z in sorted(front)]} if args.log_front else {}),
               **cinfo, "best_groups": best.get("groups")}
        log.write(json.dumps(rec) + "\n")
        log.flush()
        print(json.dumps({k: v for k, v in rec.items() if k not in ("best_groups", "front", "ops")}), flush=True)
        if gen % args.save_every == 0 or gen == args.gens - 1:
            save_state(args.run, pop, archive, elites, gen)
            publish(args, args.run)
    state["pool"].close()
    save_state(args.run, pop, archive, elites, args.gens)


def immigrants(args, rng, k=4, have=()):
    """Genomes other islands published under --share (their pareto.json and elites.json), re-evaluated here: the best
    one overall, then the best of each other lineage (root seed), lineages this island lacks first. Process mining
    (2026-11-10): the top k of the fronts by distance were all one seed (min+cellnet), so migration only spread the
    leader; the survivor cap limits a lineage inside an island, this keeps the exchange between islands diverse."""
    me = os.path.abspath(os.path.join(args.share, args.island)) if args.island else None
    best = {}  # lineage -> (dist, genome, island)
    for p in glob.glob(os.path.join(args.share, "*", "pareto.json")) + glob.glob(os.path.join(args.share, "*", "elites.json")):
        if me and os.path.abspath(os.path.dirname(p)) == me:
            continue
        try:
            data = json.load(open(p))
        except Exception:
            continue
        for d in data:
            try:
                g = Genome.from_json(d["genome"])
            except Exception:  # ops this code does not know yet (an island on newer code): skip, never crash
                continue
            L, dist = lineage_name(g), float(d.get("dist", 99.0))
            if L not in best or dist < best[L][0]:
                best[L] = (dist, g, os.path.basename(os.path.dirname(p)))
    if not best:
        return []
    have = set(have)
    lead = min(best, key=lambda L: best[L][0])
    rest = sorted((L for L in best if L != lead), key=lambda L: (L in have, best[L][0] + 0.01 * rng.random()))
    out = []
    for L in [lead] + rest[:max(0, k - 1)]:
        g = best[L][1]
        g.meta["immigrant_from"] = best[L][2]
        out.append(g)
    return out


def publish(args, run_dir):
    if not args.share or not args.island:
        return
    dst = os.path.join(args.share, args.island)
    os.makedirs(dst, exist_ok=True)
    import shutil
    # population.json and improvements.jsonl are the steward's evidence (agenome/steward.py)
    for name in ("pareto.json", "elites.json", "log.jsonl", "args.json", "population.json", "improvements.jsonl"):
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
    ap.add_argument("--judge-net", default="inception+mobilenet", help="the judge's nets, joined by +")
    ap.add_argument("--judge-agg", default="max", choices=["max", "mean"],
                    help="how the nets' scores combine: max (v3, default; the mean was ridden by pleasing one net) or "
                         "mean (v2)")
    ap.add_argument("--judge-n", type=int, default=12, help="frames per clip the judge sees")
    ap.add_argument("--judge-windows", type=int, default=1,
                    help="4-second windows of each phenotype the judge scores (window k starts k * --judge-hop seconds "
                         "after the first); the score is their mean. 2 or more stop a genome from fitting only the frames "
                         "it is judged on: best genomes judged 1.13 to 1.70 on their first window read 1.21 to 2.48 on "
                         "the next three (2026-11-10), and each extra window costs one more render")
    ap.add_argument("--judge-hop", type=float, default=8.0, help="seconds between judged windows (fixed mode)")
    ap.add_argument("--window-mode", default="fixed", choices=["fixed", "random"],
                    help="fixed: every genome is judged on offsets 0, hop, 2 hop, ...; random: each evaluation draws "
                         "--judge-windows offsets from 0..--window-span on a --window-step grid (genomes climbed on two "
                         "fixed windows read 0.3 to 0.6 worse on windows they never saw, 2026-11-10)")
    ap.add_argument("--window-span", type=float, default=20.0, help="random mode: latest window start, seconds")
    ap.add_argument("--window-step", type=float, default=2.0, help="random mode: grid of window starts, seconds")
    ap.add_argument("--rejudge-every", type=int, default=0,
                    help="random mode: every N generations judge the survivors and the front again on windows they "
                         "have not seen and fold them in (0 = never)")
    ap.add_argument("--audit-offsets", default="24,28",
                    help="window starts (s) never used for selection on which each generation's best is judged and "
                         "logged as best_audit (comma list; 'none' = off). Keep warmup + offset under 60 s")
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
    ap.add_argument("--survival", default="pareto", choices=["pareto", "diamond"],
                    help="pareto: non-dominated fronts inside the band (dist within --span of the best, bytes under "
                         "--budget) ordered by hypervolume contribution, every generation; diamond: the COMPLEXIFY / "
                         "COMPRESS regime switch (--patience, --retain, --beta, --tiebreak)")
    ap.add_argument("--span", type=float, default=0.05,
                    help="pareto: the band is dist <= best + span * |best| (0.05 = give up at most 5%% of fidelity "
                         "for bytes)")
    ap.add_argument("--parents", default="tournament", choices=["lexicase", "tournament"],
                    help="how the 60%% of parents drawn from the population are chosen: a tournament of 3 on dist "
                         "(default), or epsilon-lexicase over the feature groups, judge nets, critic and bytes (a wash "
                         "in the 2026-11-10 A/B: SEARCH.md)")
    ap.add_argument("--operators", default="fixed", choices=["adaptive", "fixed"],
                    help="fixed (default): the regime's mix; adaptive: probability matching over param / struct / "
                         "xover / shrink, credited by each child's gain over its parent (a wash in the A/B: SEARCH.md)")
    ap.add_argument("--shrink", type=float, default=0.25, help="pareto + fixed operators: share of shrink children")
    ap.add_argument("--aos-min", type=float, default=0.1, help="adaptive operators: smallest probability of an arm")
    ap.add_argument("--aos-rate", type=float, default=0.2, help="adaptive operators: learning rate per generation")
    ap.add_argument("--tuner", default="cma", choices=["cma", "random"],
                    help="the --tune share: a sep-CMA-ES emitter on the leader's parameter bytes, or random byte "
                         "tweaks around the best few")
    ap.add_argument("--cma-block", type=int, default=24, help="cma tuner: parameter bytes tuned together")
    ap.add_argument("--cma-gens", type=int, default=8, help="cma tuner: generations per block")
    ap.add_argument("--cma-sigma", type=float, default=12.0, help="cma tuner: first block's step, byte units")
    ap.add_argument("--cma-sigma-min", type=float, default=2.0, help="cma tuner: smallest block start step")
    ap.add_argument("--cma-blocks", default="random", choices=["random", "op"],
                    help="cma tuner: a block is --cma-block random bytes, or whole operators' parameters until it "
                         "holds --cma-block (LEDGER.md L7)")
    ap.add_argument("--log-front", action="store_true", help="write the archive's (dist, bytes) front every generation")
    ap.add_argument("--patience", type=int, default=8)
    ap.add_argument("--retain", type=float, default=0.97)
    ap.add_argument("--beta", type=float, default=300.0)
    ap.add_argument("--budget", type=float, default=1500.0,
                    help="bytes: diamond, the median size that forces COMPRESS; pareto, the band's size limit")
    ap.add_argument("--tiebreak", type=float, default=2e-5)
    ap.add_argument("--save-every", type=int, default=5)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--share", default=None, help="shared folder of islands, e.g. /mnt/project-files/amoebius-genome/runs")
    ap.add_argument("--island", default=None, help="this island's name under --share")
    ap.add_argument("--migrate-every", type=int, default=10, help="generations between immigrant draws (0 = none)")
    ap.add_argument("--lineage-cap", type=float, default=0.25,
                    help="largest share of the mu survivors one lineage (root seed) may hold; 0 = no cap")
    ap.add_argument("--migrants", type=int, default=4)
    args = ap.parse_args(argv)
    args.audit_offsets = [] if str(args.audit_offsets).strip().lower() in ("", "none", "0", "off") else \
        [float(x) for x in str(args.audit_offsets).split(",") if x.strip()]
    if args.window_mode == "random" and args.judge_windows > len(window_grid(args)):
        ap.error("--judge-windows is larger than the random window grid")
    if args.ss:
        os.environ["AGENOME_SS"] = str(args.ss)  # workers inherit it; every Renderer reads it
    run(args)


if __name__ == "__main__":
    main()
