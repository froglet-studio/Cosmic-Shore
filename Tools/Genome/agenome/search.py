"""Population search: regime-switching Pareto ("diamond") over (distance to Amoebius down, genome bytes down),
with a MAP-Elites side archive for diversity and a learned critic retrained as the population moves.

The regimes are Garrett's pareto_complexity.py (NCA Ecology Lab) lifted from a (1+lambda) climber to a population:
  COMPLEXIFY - select on distance to Amoebius; growth mutations (insert, duplicate, swap, crossover) dominate.
  COMPRESS   - hold distance with a leaky-ReLU floor (peak / retain), minimise compressed bytes; shrink mutations
               (delete, prune, param -> default) dominate.
Flips on plateau (patience) or when the population's median size exceeds the byte budget.

Distance to Amoebius = stat (features.py, fixed) + critic_w * softplus(-critic logit) (critic.py, retrained every
--critic-every generations on Amoebius train frames vs the frames the search just rendered) + judge_w * judge (the fixed
perceptual judge, agenome.judge) + lab_w * lab (the fitness lab's element-aware scorer, agenome.labscore; off unless
--lab-w > 0, scored on the same windows as the judge by --lab-workers processes while the pool renders the rest).

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
# with --lab-w > 0, renders go to the pool this many at a time (each finished one holds its whole clips here until the
# lab takes it; LabState.MAX_PENDING bounds the lab's own queue)
LAB_CHUNK = 32

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
    """Render and score one genome. payload = (genome json, frames, judge frames, windows[, hop[, full]]): windows is
    either a list of start offsets in seconds (each window renders with t_shift = offset) or a count n, meaning offsets
    0, hop, 2 hop, ... (the older form, still used by agenome.tune). full (default False) also returns every window's
    whole clip as res['lframes'], for the lab scorer (its optical flow needs all 60 frames, not the judge's 12).
    The stat, held-out stat, groups, luma and motion
    are means over the windows, each window's judge frames go to the judge, and the critic's 4 frames are spread over
    the windows (one window: frames 0, 1/3, 2/3 and the end, as before; two: the first and last frame of each), so no
    term of the fitness is measured on one stretch of time (code review 2026-11-10: groups and the critic were still
    window 0 only, so a row's groups described window 0 while its stat was the mean)."""
    from .features import window_features
    gj, n_frames, n_judge = payload[:3]
    wins = payload[3] if len(payload) > 3 else 1
    if isinstance(wins, (list, tuple)):
        offsets = [float(t) for t in wins] or [0.0]
    else:
        hop = float(payload[4]) if len(payload) > 4 else 8.0
        offsets = [k * hop for k in range(max(1, int(wins)))]
    full = bool(payload[5]) if len(payload) > 5 else False
    t0 = time.time()
    try:
        g = Genome.from_json(gj)
        stats, holds, jfr, dks, lumas, motions, cfr, lfr = [], [], [], [], [], [], [], []
        nw = len(offsets)
        slots = [4 // nw + (1 if w < 4 % nw else 0) for w in range(nw)]  # the critic's 4 frames over the windows
        for w, ts in enumerate(offsets):
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
            dks.append(dk)
            lumas.append(float(frk.mean() / 255.0))
            motions.append(float(np.abs(np.diff(frk[::4].astype(np.int16), axis=0)).mean() / 255.0) if len(frk) > 4 else 0.0)
            if slots[w] > 0:
                ci = np.linspace(0, len(frk) - 1, slots[w]).astype(int) if slots[w] > 1 else [len(frk) // 2]
                cfr.append(frk[ci].copy())
            if full:
                lfr.append(frk)
        groups = {k: round(float(np.mean([dk[k] for dk in dks])), 4) for k in dks[0] if k != "total"}
        res = {"ok": True, "stat": float(np.mean(stats)), "groups": groups,
               "zbytes": g.size(), "nodes": len(g.nodes), "luma": float(np.mean(lumas)),
               "motion": float(np.mean(motions)),
               "frames": np.concatenate(cfr, axis=0),
               "stat_windows": [round(x, 4) for x in stats], "stat_nwin": len(offsets),
               "win_t": [round(t, 3) for t in offsets]}
        if holds:
            res["hold"] = float(np.mean(holds))
            res["hold_windows"] = [round(x, 4) for x in holds]
        if n_judge:
            res["jframes"] = jfr
        if full:
            res["lframes"] = lfr
    except Exception as e:
        res = {"ok": False, "stat": 99.0, "zbytes": 10 ** 6, "err": f"{type(e).__name__}: {e}"[:300],
               "nodes": 0, "luma": 0.0, "motion": 0.0}
    res["sec"] = time.time() - t0
    return res


# ---------------------------------------------------------------------- lab scorer side (only when --lab-w > 0)
def _lab_init():
    """A lab-scoring process: one numpy/OpenCV thread, no X display; loads the scorer once (about 20 s: the reference
    pickle plus Keras' InceptionV3 for its crit and dist axes, with the judge's weights from ~/.keras/models)."""
    for k in ("OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS"):
        os.environ[k] = "1"
    os.environ.setdefault("KERAS_BACKEND", "jax")
    os.environ.pop("DISPLAY", None)
    try:
        import cv2
        cv2.setNumThreads(1)
    except Exception:
        pass
    from . import labscore
    labscore.scorer()


def _lab_eval(frames_list):
    """Lab score of one candidate's windows (each a whole uint8 [60,144,256,3] clip): per-window scores, 1.0 = Amoebius'
    held-out source."""
    t0 = time.time()
    try:
        from . import labscore
        _, per, _ = labscore.windows(frames_list)
        return {"ok": True, "per": [float(x) for x in per], "sec": time.time() - t0}
    except Exception as e:
        return {"ok": False, "err": f"{type(e).__name__}: {e}"[:300], "sec": time.time() - t0}


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
    the luck of its first draw. Groups, luma and motion are merged the same way; the critic frames stay those of c's
    first evaluation (a fixed 4 per genome)."""
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
    for k in ("stat", "hold", "luma", "motion"):
        if k in c and k in r:
            c[k] = wavg(c[k], r[k], m0, m1)
    if isinstance(c.get("groups"), dict) and isinstance(r.get("groups"), dict):
        c["groups"] = {k: round(wavg(v, r["groups"][k], m0, m1), 4) if k in r["groups"] else v
                       for k, v in c["groups"].items()}
    for k in ("judge_windows", "judge_hold_windows", "stat_windows", "hold_windows", "win_t"):
        if k in c and k in r:
            c[k] = list(c[k]) + list(r[k])
    if "judge_nwin" in c and "judge_nwin" in r:
        c["judge_nwin"] = n0 + n1
    if "lab" in r:  # the lab scorer (--lab-w > 0) is merged over its own window count
        l0, l1 = int(c.get("lab_nwin", 0)) if "lab" in c else 0, int(r.get("lab_nwin", 1))
        c["lab"] = round(wavg(c["lab"], r["lab"], l0, l1), 4) if l0 else r["lab"]
        c["lab_windows"] = (list(c.get("lab_windows", [])) if l0 else []) + list(r.get("lab_windows", []))
        c["lab_nwin"] = l0 + l1
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


class LabState:
    """The fitness lab's element-aware scorer (agenome.labscore; 1.0 = Amoebius' held-out source, about 9 a typical
    phenotype) as a search term, on the same windows as the judge. Off unless --lab-w > 0, so the default fitness and
    every running island stay as they were. Each render result's whole clips go to --lab-workers scoring processes as
    soon as it arrives (submit), so the lab scores finished candidates while the render pool works on the rest;
    collect() waits for them. A candidate whose lab scoring failed has no 'lab' and dist reads it as LAB_MISSING.

    Memory: an unfinished job holds its candidate's clips in this process (about 6.6 MB a window), and the lab scores
    slower than the render pool renders, so submit() waits while MAX_PENDING jobs are unfinished (and the renders go in
    LAB_CHUNK chunks, see run()). A job's clips go as soon as it finishes. Without the bound, an island at generation 0
    queued every seed's clips and was OOM-killed at about 9.8 GB (labA, 2026-11-10).
    Failures: the scoring processes are a concurrent.futures ProcessPoolExecutor, not a multiprocessing.Pool. A Pool
    whose idle worker is killed (the OOM killer's likeliest target, at about 1.2 GB) keeps that worker's task-queue
    lock forever: no job runs again and Pool.terminate() itself blocks on the lock, so the island would hang for good
    (found by review on 2026-11-11 and reproduced). The executor instead fails every unfinished job at once
    (BrokenProcessPool): the pool restarts and each of those jobs is sent once more, so a dead process costs about the
    jobs it was running, not the whole queue. A pool that finishes nothing for STALL seconds while submit() waits is
    stopped and restarted the same way. A job still running when collect() gives up (QUIET with nothing finishing, or
    its deadline) goes unscored, and the pool restarts so the hung process dies."""
    LAB_MISSING = 10.0
    MAX_PENDING = 24   # unfinished lab jobs before submit() waits for one to finish
    STALL = 300.0      # submit(): nothing finished for this long while it waits means the pool is wedged
    QUIET = 120.0      # collect(): no job finishing for this long (plus 8 s a window) means a scoring process hangs
    MAX_RESTARTS = 3   # restarts in a row with no job scored in between before the lab stops scoring
    RETRIES = 1        # times a job that a dead or stopped pool failed is sent again

    def __init__(self, args, ctx):
        self.on = getattr(args, "lab_w", 0.0) > 0
        self.w = getattr(args, "lab_w", 0.0)
        self.n = max(1, int(getattr(args, "lab_workers", 2)))
        # jobs: id(candidate) -> [candidate, future, clips, sends]; the clips are kept only until the job is final, so
        # a job a dead pool failed can be sent again
        self.ctx, self.pool, self.jobs, self.dead = ctx, None, {}, False
        self.errors, self.wait, self.scored, self.restarts, self.failed_restarts, self.resent = 0, 0.0, 0, 0, 0, 0
        if self.on:
            from .labscore import LAB
            self.new_pool()
            print(f"lab: weight {self.w}, {self.n} scoring processes, scorer {LAB}", flush=True)

    def _kill(self):
        """Stop the current pool without ever blocking: kill its processes (the executor then fails whatever is still
        unfinished) and let it clean up in the background."""
        old, self.pool = self.pool, None
        if old is None:
            return
        for p in list((getattr(old, "_processes", None) or {}).values()):
            try:
                p.kill()
            except Exception:
                pass
        # Python 3.10, and 3.11 before 3.11.5, never close this end of the job pipe when a pool breaks (gh-94777): the
        # thread feeding it stays blocked part-way through writing a clip, the old pool never shuts down, and the
        # interpreter's exit waits on it for good. With every reader closed the write fails (EPIPE, which the executor
        # ignores) and the thread ends. Later Pythons close it themselves; closing it twice is harmless.
        try:
            old._call_queue._reader.close()
        except Exception:
            pass
        try:
            old.shutdown(wait=False, cancel_futures=True)
        except Exception:
            pass

    def new_pool(self, failed=False):
        """(Re)start the scoring processes. The jobs already submitted stay in self.jobs: a dead pool has failed them,
        and _reap() sends them again. After MAX_RESTARTS failed restarts in a row (no job scored in between: the scorer
        cannot load, say) the lab stops scoring for the rest of the run, loudly."""
        import gc
        from concurrent.futures import ProcessPoolExecutor
        if self.pool is not None:
            self._kill()
            gc.collect()  # what the dead pool held goes now, not at some later full collection
        if failed:
            self.restarts += 1
            self.failed_restarts += 1
            if self.failed_restarts > self.MAX_RESTARTS:
                self.dead = True
                print(f"lab: {self.failed_restarts} pool restarts without a scored job; the lab scores nothing more "
                      "this run (every candidate reads LAB_MISSING)", flush=True)
                return
        self.pool = ProcessPoolExecutor(self.n, mp_context=self.ctx, initializer=_lab_init)

    def _send(self, fr):
        """Submit one job, restarting a pool found broken (a scoring process died). Never raises; None when the lab is
        dead or the submit failed."""
        import concurrent.futures as cf
        for attempt in (0, 1):
            if self.dead or self.pool is None:
                return None
            try:
                return self.pool.submit(_lab_eval, fr)
            except cf.BrokenExecutor:
                if attempt:
                    break
                print("lab: a scoring process died; restarting the lab pool", flush=True)
                self.new_pool(failed=True)
            except Exception as e:
                print("lab submit failed:", f"{type(e).__name__}: {e}"[:200], flush=True)
                break
        return None

    def _reap(self):
        """Go over the jobs that finished since the last look: a job is final once it scored, failed on its own, or
        has been sent RETRIES + 1 times, and its clips go then; a job scored proves the scorer works (the restart count
        starts over); a job that a dead or stopped pool failed (BrokenProcessPool, or cancelled) is sent again."""
        import concurrent.futures as cf
        again = []
        for job in list(self.jobs.values()):
            f = job[1]
            if job[2] is None or not f.done():
                continue
            e = None if f.cancelled() else f.exception()
            if f.cancelled() or isinstance(e, cf.process.BrokenProcessPool):
                if job[3] <= self.RETRIES and not self.dead:
                    again.append(job)
                    continue
            elif e is None and (f.result() or {}).get("ok"):
                self.failed_restarts = 0
            job[2] = None
        if again:
            print(f"lab: sending {len(again)} jobs again that a stopped scoring pool failed", flush=True)
        for job in again:
            nf = self._send(job[2])
            if nf is None:  # the lab is dead: the failed future stays, and collect() counts it
                job[2] = None
                continue
            job[1], job[3] = nf, job[3] + 1
            self.resent += 1

    def submit(self, r):
        """Queue r's clips for lab scoring (never raises). Waits while MAX_PENDING jobs are unfinished, so at most that
        many candidates' clips wait in this process; that wait counts toward lab_wait. After STALL seconds with no job
        finished the pool is taken as wedged and restarts (its jobs are sent again)."""
        import concurrent.futures as cf
        fr = r.pop("lframes", None)
        if not (self.on and fr is not None and r.get("ok")):
            return
        t0 = t_stall = time.time()
        try:
            while not self.dead:
                self._reap()
                busy = [j[1] for j in self.jobs.values() if not j[1].done()]
                if len(busy) < self.MAX_PENDING:
                    break
                if time.time() - t_stall > self.STALL:
                    print(f"lab: no scoring finished in {self.STALL:.0f} s; restarting the lab pool", flush=True)
                    self.new_pool(failed=True)
                    t_stall = time.time()
                    continue
                cf.wait(busy, timeout=0.5, return_when=cf.FIRST_COMPLETED)
            f = self._send(fr)
            if f is None:  # unscored (LAB_MISSING); the run goes on
                self.errors += 1
                return
            self.jobs[id(r)] = [r, f, fr, 1]
        except Exception as e:
            self.errors += 1
            print("lab submit failed:", f"{type(e).__name__}: {e}"[:200], flush=True)
        finally:
            self.wait += time.time() - t0

    def collect(self, res):
        import concurrent.futures as cf
        if not self.on:
            return
        t0 = time.time()
        keys = [id(r) for r in res if id(r) in self.jobs]
        nwin = max([len(self.jobs[k][0].get("win_t") or [0]) for k in keys] or [1])
        # the first job in each process waits for the scorer to load (about 20 s); after that about 1.5 s a window on a
        # free core. A hung scoring process shows as a quiet spell with no job finishing; then the pool restarts.
        deadline = time.time() + 120 + 8.0 * nwin * len(keys) / self.n
        quiet = self.QUIET + 8.0 * nwin
        last, broken = time.time(), False
        while True:
            self._reap()  # a job a dead pool failed is sent again here too
            pending = [self.jobs[k][1] for k in keys if not self.jobs[k][1].done()]
            if not pending:
                break
            done, _ = cf.wait(pending, timeout=1.0, return_when=cf.FIRST_COMPLETED)
            now = time.time()
            if done:
                last = now
            if now > deadline or now - last > quiet:
                broken = True
                break
        for k in keys:
            r, f = self.jobs.pop(k)[:2]
            if not f.done():
                out = {"ok": False, "err": "lab timeout"}
            else:
                try:
                    out = f.result()
                except Exception as e:  # BrokenProcessPool (a scoring process died), a cancelled job, a worker error
                    out = {"ok": False, "err": f"lab worker: {type(e).__name__}: {e}"[:200]}
            if out.get("ok") and out.get("per"):
                r["lab"] = round(float(np.mean(out["per"])), 4)
                r["lab_windows"] = [round(x, 3) for x in out["per"]]
                r["lab_nwin"] = len(out["per"])
                self.scored += 1
                self.failed_restarts = 0
            else:
                self.errors += 1
                if self.errors <= 5:
                    print("lab error:", out.get("err"), flush=True)
        if broken and not self.dead:  # kill the hung process; a dead one's pool restarted when it was found
            print("lab scoring timed out; restarting the lab pool", flush=True)
            self.new_pool(failed=True)
        self.wait += time.time() - t0

    def close(self):
        """End of run: every job was collected, so the processes are killed rather than joined (a hung one would hold
        the interpreter's exit)."""
        self._kill()


class CriticState:
    def __init__(self, args):
        self.on = bool(args.real) and args.critic_w > 0
        self.w = args.critic_w
        self.stat_w, self.judge_w = args.stat_w, args.judge_w
        self.lab_w = getattr(args, "lab_w", 0.0)
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
            if self.lab_w > 0:  # never used alone: the red team climbed the lab while the judge got worse (MERGE.md)
                c["dist"] += self.lab_w * c.get("lab", LabState.LAB_MISSING)


def _drop_pool(pool, wait=2.0):
    """Stop a multiprocessing.Pool without ever blocking for good. A worker killed while it waits for a task (the OOM
    killer's likeliest pick: the render workers are the largest processes) keeps the task queue's read lock, and
    Pool.terminate() then blocks forever on that lock, as does the pool's own exit hook (review, 2026-11-11;
    reproduced). So terminate() runs in a daemon thread, which also uses up the exit hook, and the workers still alive
    after `wait` seconds are killed here."""
    import threading
    if pool is None:
        return
    t = threading.Thread(target=pool.terminate, name="drop-pool", daemon=True)
    t.start()
    t.join(wait)
    for p in list(getattr(pool, "_pool", None) or []):
        try:
            if p.exitcode is None:
                p.kill()
        except Exception:
            pass


def run(args):
    rng = random.Random(args.seed)
    os.makedirs(args.run, exist_ok=True)
    os.makedirs(os.path.join(args.run, "best"), exist_ok=True)
    log = open(os.path.join(args.run, "log.jsonl"), "a")
    with open(os.path.join(args.run, "args.json"), "w") as f:
        json.dump(vars(args), f, indent=1)
    seeds = load_seeds(args.seeds, rng)
    if not (args.no_handmade or os.environ.get("AGENOME_NO_HANDMADE") == "1"):
        # the hand-built Amoebius scaffolds join every run's seeds unless asked not to: an "isolated" island (a
        # werkkzeug-only or novelty run, island C's reservoir) otherwise carries these lineages whatever --seeds says
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
    lab = LabState(args, ctx)

    def job(gj, offsets):
        """One evaluation's payload; with the lab on, the workers also return every window's whole clip."""
        return (gj, args.frames, judge.n, offsets, 8.0, True) if lab.on else (gj, args.frames, judge.n, offsets)

    def new_pool():
        _drop_pool(state["pool"])  # never terminate() in this thread: it can block for good (see _drop_pool)
        state["pool"] = ctx.Pool(args.workers, initializer=_init,
                                 initargs=(args.stats, args.hold, args.lp_threads, args.mem_gb))
    new_pool()

    def run_payload(payload):
        """Evaluate payloads on the worker pool. With the lab on they go in LAB_CHUNK chunks: a finished render waits in
        this process with its whole clips until the lab takes it, so the pool may only run that far ahead. Each chunk is
        judged as it lands too (its judge frames, about 1.3 MB a window, go then), so a generation 0 of a thousand seeds
        does not hold them all; judge.score() skips what it has already scored."""
        if lab.on and len(payload) > LAB_CHUNK:
            res = []
            for i in range(0, len(payload), LAB_CHUNK):
                part = run_chunk(payload[i:i + LAB_CHUNK])
                judge.score(part)
                res += part
            return res
        return run_chunk(payload)

    def run_chunk(payload):
        """A hung evaluation restarts the pool instead of stalling the island."""
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
            t_sub = time.time()
            lab.submit(res[-1])  # the lab scores this one while the pool renders the rest (never raises)
            deadline += time.time() - t_sub  # time spent waiting on the lab is not the renders' time
        if broken:
            print("evaluation timed out; restarting the worker pool", flush=True)
            new_pool()
        return res

    def evaluate(gs):
        payload = [job(g.to_json(), draw_offsets(args, rng)) for g in gs]
        res = run_payload(payload)
        for g, r in zip(gs, res):
            r["genome"] = g
        crit.add_fakes(res)
        judge.score(res)
        lab.collect(res)
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
        res = run_payload([job(c["genome"].to_json(), o) for c, o in todo])
        judge.score(res)
        lab.collect(res)
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
            (r,) = run_payload([job(c["genome"].to_json(), list(args.audit_offsets))])
            judge.score([r])
            lab.collect([r])
            audits[key] = ({"judge": r.get("judge"), "judge_windows": r.get("judge_windows"), "t": r.get("win_t"),
                            "stat": round(r["stat"], 4),
                            **({"lab": r.get("lab"), "lab_windows": r.get("lab_windows")} if lab.on else {})}
                           if r.get("ok") else {"err": r.get("err")})
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
    regime = COMPLEXIFY
    peak = min(c["dist"] for c in pop)
    since = 0
    evals = len(seeds)
    for gen in range(args.gens):
        cinfo = {}
        moved = False
        lab.wait = 0.0
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
        kids = []
        if args.share and args.migrate_every > 0 and gen > 0 and gen % args.migrate_every == 0:
            held = {c["genome"].to_bytes() for c in pop + archive.members + list(elites.cells.values())}
            kids += immigrants(args, rng, k=args.migrants, have={lineage_of(c) for c in pop}, held=held)
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
        pop = survivors(allc, key, args.mu, args.lineage_cap)
        med_size = float(np.median([c["zbytes"] for c in pop]))
        flipped = False
        if regime == COMPLEXIFY and (since >= args.patience or med_size > args.budget):
            regime, since, flipped = COMPRESS, 0, True
        elif regime == COMPRESS and since >= args.patience:
            regime, since, flipped = COMPLEXIFY, 0, True
        best = min(pop, key=lambda c: c["dist"])
        aud = audit(best) if judge.on else None
        simplest_ok = min((m for m in archive.members if m["dist"] <= floor), key=lambda m: m["zbytes"], default=best)
        rec = {"gen": gen, "t": round(time.time() - t_start, 1), "evals": evals, "regime": regime, "flip": flipped,
               "peak": round(peak, 4), "best_dist": round(best["dist"], 4), "best_stat": round(best["stat"], 4),
               "best_crit": round(best.get("crit", 0.0), 3), "best_hold": round(best.get("hold", -1), 4),
               **({"best_judge": best.get("judge"), "best_judge_hold": best.get("judge_hold"),
                   "best_judge_nwin": best.get("judge_nwin"),
                   "top_judge": min((c.get("judge", 99) for c in allc), default=None)} if judge.on else {}),
               **({"best_audit": aud.get("judge"), "best_audit_windows": aud.get("judge_windows")} if aud else {}),
               **({"best_lab": best.get("lab"), "best_lab_nwin": best.get("lab_nwin"),
                   "top_lab": min((c.get("lab", 99) for c in allc), default=None),
                   "lab_wait": round(lab.wait, 1), "lab_errors": lab.errors} if lab.on else {}),
               **({"best_audit_lab": aud.get("lab"), "best_audit_lab_windows": aud.get("lab_windows")}
                  if aud and lab.on else {}),
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
    _drop_pool(state["pool"])
    lab.close()
    save_state(args.run, pop, archive, elites, args.gens)


def immigrants(args, rng, k=4, have=(), held=()):
    """Genomes other islands published under --share (their pareto.json and elites.json), re-evaluated here.

    Each front is ranked only against itself: islands score on different windows, weights and judge versions, so a
    dist from one folder says nothing about a dist from another. Code review, 2026-11-10: ranking the pooled rows by
    raw dist let the static seedbank (one window, judge v2, no critic term) supply nearly every migrant to every island,
    the same four at every migration, and no island's front reached another. Only folders whose args.json names an
    island count (islands publish one; the seedbank and tuner copies are --resume / --seeds inputs). The first
    immigrant is the best row of one other island picked at random, any lineage, so a leader's gains still spread; the
    rest go round the other islands taking each one's best row of a lineage this island lacks (have), then of any
    lineage not taken yet. Rows whose bytes are in held (what this island already holds) are skipped. k <= 0 imports
    nothing (it used to return the lead anyway)."""
    if k <= 0:
        return []
    me = os.path.abspath(os.path.join(args.share, args.island)) if args.island else None
    held, fronts = set(held), []
    # --import-from reads another folder's islands while this one publishes under --share: a trial island kept out of
    # runs/ (its dist has other terms) still gets the same immigrants as the islands it is compared with
    for folder in sorted(glob.glob(os.path.join(getattr(args, "import_from", None) or args.share, "*", ""))):
        folder = os.path.abspath(folder)
        if me and folder == me:
            continue
        try:
            if not json.load(open(os.path.join(folder, "args.json"))).get("island"):
                continue
        except Exception:
            continue
        rows, seen = [], set()
        for name in ("pareto.json", "elites.json"):
            try:
                data = json.load(open(os.path.join(folder, name)))
            except Exception:
                continue
            for d in data:
                try:
                    g = Genome.from_json(d["genome"])
                    b = g.to_bytes()
                except Exception:  # ops this code does not know yet (an island on newer code): skip, never crash
                    continue
                if b in held or b in seen:
                    continue
                seen.add(b)
                rows.append((float(d.get("dist", 99.0)), b, g))
        if rows:
            rows.sort(key=lambda r: r[0])
            fronts.append((os.path.basename(folder), rows))
    if not fronts:
        return []
    out, taken, used = [], set(), set()

    def take(name, b, g):
        g.meta["immigrant_from"] = name
        out.append(g)
        taken.add(lineage_name(g))
        used.add(b)

    name, rows = fronts[rng.randrange(len(fronts))]
    take(name, rows[0][1], rows[0][2])
    order = list(fronts)
    rng.shuffle(order)
    for lacking in (True, False):
        moved = True
        while len(out) < k and moved:
            moved = False
            for name, rows in order:
                if len(out) >= k:
                    break
                for _, b, g in rows:
                    L = lineage_name(g)
                    if b in used or L in taken or (lacking and L in have):
                        continue
                    take(name, b, g)
                    moved = True
                    break
    return out


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
        d = {k: v for k, v in c.items() if k not in ("genome", "frames", "jframes", "lframes")}
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
    ap.add_argument("--lab-w", type=float, default=0.0,
                    help="weight of the fitness lab's element-aware scorer (agenome.labscore; 1.0 = Amoebius' held-out "
                         "source, about 9 a typical phenotype), scored on the judged windows and added to dist beside "
                         "the judge, never alone; 0 = off (the default, so the merged code changes no running island). "
                         "About 1.5 s a window on a free core")
    ap.add_argument("--lab-workers", type=int, default=2,
                    help="processes that score the lab while the render pool works (each loads it once, about 20 s)")
    ap.add_argument("--critic-every", type=int, default=6)
    ap.add_argument("--critic-steps", type=int, default=120)
    ap.add_argument("--seeds", nargs="*", default=[])
    ap.add_argument("--no-handmade", action="store_true",
                    help="do not add the hand-built amoebius_like / amoebius_v2 scaffolds to the seeds (also "
                         "AGENOME_NO_HANDMADE=1)")
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
    ap.add_argument("--migrate-every", type=int, default=10, help="generations between immigrant draws (0 = none)")
    ap.add_argument("--lineage-cap", type=float, default=0.25,
                    help="largest share of the mu survivors one lineage (root seed) may hold; 0 = no cap")
    ap.add_argument("--migrants", type=int, default=4)
    ap.add_argument("--import-from", default=None,
                    help="folder of islands to draw immigrants from (default: --share); lets a trial island publish to a "
                         "folder of its own and still import what the compared islands import")
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
