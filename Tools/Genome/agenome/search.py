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
import sys
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


def _lab_run(fn, frames_list, mark):
    """One lab job in a scoring process: leave the job's mark file first (so the search can tell a job a dead pool
    failed after it had started from one it never started), then score with fn. fn is _lab_eval, passed in rather than
    named here so that a stand-in scorer a test installs reaches the scoring process too."""
    if mark:
        try:
            open(mark, "w").close()
        except OSError:
            pass
    return fn(frames_list)


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
    whose idle worker is killed (the OOM killer's likeliest target, at about 1.2 GB) keeps that worker's task-queue lock
    forever: no job runs again and Pool.terminate() itself blocks on the lock, so the island would hang for good (found
    by review on 2026-11-11 and reproduced). The executor instead fails every unfinished job at once
    (BrokenProcessPool): the pool restarts and those jobs are sent again. Each job marks a file when a process starts it
    (_lab_run). A job that had not started goes again at once, for free. A job that had started is charged a send, gets
    RETRIES more, and is a suspect: suspects go again one at a time (the suspect slot), so when one is a candidate whose
    clips kill or hang the scorer every time, its next failure is charged to it and to nobody else it was queued with,
    and it costs only itself. (Its restarts still count toward a stop when nothing scores in between: three that hang
    the scorer, left at the end of a payload with nothing else to score, stop the lab.) Suspects waiting their turn hold
    their clips and count toward MAX_PENDING, so when they fill it while a scoring process has nothing to do, submit()
    sends the oldest at once, outside the slot (it can then cost a job beside it, as before the slot) rather than leave
    the scorers idle. A pool that finishes nothing for STALL seconds while submit() waits, or for QUIET seconds while
    collect() waits, is stopped and restarted the same way (collect() does that up to GIVE_UPS times, then what is left
    goes unscored). A pool that cannot start, refuses a job, or loses its manager thread without breaking (before Python
    3.12.1 that thread dies so when its queue feeder thread cannot start, and the pool would never finish a job again;
    _reap() restarts it as soon as it sees that, and its jobs go again, see _bury) counts as a failed restart. The
    second and third restarts in a row with no job scored wait first (BACKOFF, then twice that), so a scorer that cannot
    load for a few seconds is ridden out. When the pool after MAX_RESTARTS such restarts fails too, the lab stops,
    loudly: the candidates it holds, and those that come until REVIVE seconds later (doubling while it keeps failing),
    read LAB_MISSING; then the lab starts over as at the start of the run, on trial until its first scored job (a pool
    that hangs on trial without loading the scorer stops it again at once, see _hung). Nothing here raises into the
    search."""
    LAB_MISSING = 10.0
    MAX_PENDING = 24   # unfinished lab jobs before submit() waits for one to finish
    STALL = 300.0      # submit(): nothing finished for this long while it waits means the pool is wedged
    QUIET = 120.0      # collect(): no job finishing for this long (plus 8 s a window) means a scoring process hangs
    MAX_RESTARTS = 3   # restarts in a row with no job scored in between before the lab stops scoring
    BACKOFF = 5.0      # wait before the 2nd restart in a row with no job scored, doubled before the 3rd (5 s, 10 s)
    REVIVE = 120.0     # a stopped lab starts over this long after it stopped; doubles while it keeps failing
    REVIVE_MAX = 1800.0
    UNSTICK = 5.0      # Python before 3.11.5: how long a dead pool's own shutdown gets before its job pipe is closed
    RETRIES = 1        # times a job that had STARTED when a dead or stopped pool failed it is sent again
    GIVE_UPS = 3       # collect(): hung pools restarted (their jobs sent again) before the unfinished jobs time out

    def __init__(self, args, ctx):
        self.on = getattr(args, "lab_w", 0.0) > 0
        self.w = getattr(args, "lab_w", 0.0)
        self.n = max(1, int(getattr(args, "lab_workers", 2)))
        # jobs: id(candidate) -> [candidate, future, clips, charged, mark, sends]. charged counts the sends that had
        # started when a pool failed them; mark is the file this send's process makes when it starts the job; the
        # clips are kept only until the job is final, so a job a dead pool failed can be sent again
        self.ctx, self.pool, self.jobs, self.dead = ctx, None, {}, False
        self.errors, self.wait, self.scored, self.restarts, self.failed_restarts, self.resent = 0, 0.0, 0, 0, 0, 0
        self.t_pool, self.mark_dir = 0.0, None
        self.suspects = []  # keys of the suspects waiting for the slot, oldest first
        self.graves = []  # per killed pool: (a weak ref to its manager thread, its unfinished futures) until done
        self.deaths, self.t_revive, self.revive_in, self.revived = 0, 0.0, self.REVIVE, False
        if self.on:
            import tempfile
            from .labscore import LAB
            self.mark_dir = tempfile.mkdtemp(prefix="agenome-lab-")
            self.new_pool()
            print(f"lab: weight {self.w}, {self.n} scoring processes, scorer {LAB}", flush=True)

    # ---- marks: a job's process makes its mark file when it starts the job
    def _mark(self, r, k):
        return os.path.join(self.mark_dir, f"{id(r)}-{k}") if self.mark_dir else None

    @staticmethod
    def _rm(path):
        if path:
            try:
                os.remove(path)
            except OSError:
                pass

    def _final(self, job):
        """The job will not be sent again: its clips go now."""
        job[2] = None
        self._rm(job[4])

    def _kill(self):
        """Stop the current pool without ever blocking: kill its processes (the executor then fails whatever is still
        unfinished) and let it clean up in the background. (Before Python 3.11.5, where no thread can start, a pids
        limit say, it waits here instead, up to UNSTICK seconds.) Its unfinished jobs go to the graves: a pool whose
        manager thread is gone cannot fail them itself, so _bury() does."""
        old, self.pool = self.pool, None
        if old is None:
            return
        # shutdown() drops the manager thread; the job table stays
        mgr, pending = getattr(old, "_executor_manager_thread", None), getattr(old, "_pending_work_items", None)
        for p in list((getattr(old, "_processes", None) or {}).values()):
            try:
                p.kill()
            except Exception:
                pass
        # Python 3.10, and 3.11 before 3.11.5, never close this end of the job pipe when a pool breaks (gh-94777): the
        # thread feeding it can stay blocked part-way through writing a clip, the old pool then never shuts down, and
        # the interpreter's exit waits on it for good. With the reader closed the write fails (EPIPE, which the
        # executor ignores) and the thread ends. Closing it at once would race that same thread, which closes the
        # reader itself when it is not blocked (later Pythons close it from the executor's own thread), and two closes
        # of one descriptor can shut a pipe the next pool has just been given (review, 2026-11-11, reproduced on 3.10,
        # 3.11.4 and 3.11.18). So, on those Pythons only, the old pool gets UNSTICK seconds to shut down, and its
        # reader is closed only if it is still stuck then, which it can only be behind a feeder blocked mid-write.
        unstick = None
        if sys.version_info < (3, 11, 5):
            import threading
            cq = getattr(old, "_call_queue", None)

            def unstick(mgr=mgr, cq=cq, wait=self.UNSTICK):
                if mgr is not None and mgr.ident is not None:  # one that never started (no thread left) feeds nothing
                    mgr.join(wait)
                if cq is not None and (mgr is None or mgr.is_alive()):
                    try:
                        cq._reader.close()
                    except Exception:
                        pass
            try:
                threading.Thread(target=unstick, name="lab-unstick", daemon=True).start()
                unstick = None
            except Exception:  # no thread can start: unstick() runs below, once the shutdown has woken the old pool
                pass
        try:
            old.shutdown(wait=False, cancel_futures=True)
        except Exception:
            pass
        if unstick is not None:
            try:
                unstick()
            except Exception:
                pass
        if pending:
            import weakref
            try:  # list() of a dict's values is atomic under the GIL, even while the old pool's own thread changes it
                futs = [w.future for w in list(pending.values())]
            except Exception:
                futs = []
            self.graves.append((None if mgr is None else weakref.ref(mgr), futs))
            self._bury()

    def _bury(self):
        """The unfinished jobs of a killed pool whose manager thread is gone fail here (BrokenProcessPool), so _reap()
        sends them again: that thread never started, or it died without failing them (before Python 3.12.1 it dies so
        when its queue feeder thread cannot start, and the pool never breaks; gh-109047). A live one fails them itself
        once it sees the kill, but one that is dying may not, so a pool whose manager thread is still alive is looked
        at again on the next call (_reap() calls this first) until none of its jobs is unfinished. A grave holds that
        thread weakly: once it has ended, nothing here keeps it or what it holds (the jobs' clips, when the pool's
        shutdown left them in a table only that thread still refers to) until the next call."""
        from concurrent.futures.process import BrokenProcessPool
        keep = []
        for ref, futs in self.graves:
            mgr = None if ref is None else ref()
            gone = mgr is None or not mgr.is_alive()  # looked at first: once it is gone, nothing else finishes them
            futs = [f for f in futs if not f.done()]
            if futs and not gone:
                keep.append((ref, futs))
                continue
            for f in futs:
                try:
                    f.set_exception(BrokenProcessPool("the lab pool's manager thread was gone"))
                except Exception:
                    pass
        self.graves = keep

    def new_pool(self, failed=False):
        """(Re)start the scoring processes. The jobs already submitted stay in self.jobs: a dead pool has failed them,
        and _reap() sends them again. The second and third failed restarts in a row (no job scored in between) wait
        BACKOFF seconds first, then twice that; the next failure (the scorer cannot load, say) stops the lab, loudly,
        until _revive(). Never raises: a pool that cannot start (no file descriptors or /dev/shm left, say) leaves no
        pool, and the next send tries again as a failed restart."""
        import gc
        from concurrent.futures import ProcessPoolExecutor
        if self.pool is not None:
            self._kill()
            gc.collect()  # what the dead pool held goes now, not at some later full collection
        if failed:
            self.restarts += 1
            self.failed_restarts += 1
            if self.failed_restarts > self.MAX_RESTARTS:
                self._stop(f"{self.failed_restarts} pool restarts in a row without a scored job")
                return
            if self.failed_restarts > 1:  # the pool before scored nothing either: let what stopped it pass first
                wait = self.BACKOFF * 2 ** (self.failed_restarts - 2)
                print(f"lab: {self.failed_restarts} pool restarts in a row without a scored job; waiting {wait:g} s "
                      "before the next", flush=True)
                time.sleep(wait)
        try:
            self.pool = ProcessPoolExecutor(self.n, mp_context=self.ctx, initializer=_lab_init)
            self.t_pool = time.time()
        except Exception as e:
            self.pool = None
            print("lab: the scoring processes did not start:", f"{type(e).__name__}: {e}"[:200], flush=True)

    def _stop(self, why):
        """The lab stops scoring, loudly, until _revive(): REVIVE seconds, doubling while it keeps failing. Its pool is
        gone (every caller killed it), so the jobs it holds are final now and read LAB_MISSING: their clips go, and a
        start-over in the same payload does not send them again behind the stop's back."""
        self.dead, self.deaths, self.revived = True, self.deaths + 1, False
        for job in self.jobs.values():
            if job[2] is not None:
                self._final(job)
        self.suspects = []
        self.t_revive = time.time() + self.revive_in
        print(f"lab: {why}; the lab stops (its candidates read LAB_MISSING) and starts over in {self.revive_in:.0f} s",
              flush=True)
        self.revive_in = min(2.0 * self.revive_in, self.REVIVE_MAX)

    def _started(self):
        """Whether a process of the current pool has started one of the jobs (its mark exists), so the scorer loaded
        there. A suspect waiting for the slot still has the mark of its send to an earlier pool: it does not count."""
        waiting = set(self.suspects)
        return any(j[4] and os.path.exists(j[4]) for k, j in self.jobs.items() if j[2] is not None and k not in waiting)

    def _hung(self, msg, then, started=None):
        """A pool finished nothing for STALL or QUIET seconds: it is killed and another starts (a failed restart; its
        jobs go again, the hung ones charged a send). On trial (the lab started over and nothing has scored since), a
        pool none of whose processes ever started a job stops the lab again at once instead: its scorer still does not
        load, and each further pool would hold the search that long again, at every try. A pool that did start a job
        loaded the scorer, so what hangs there may be a candidate's clips, and it counts as at the start of the run.
        started: whether the pool started a job, when the caller had to find that out before its jobs went (None: look
        now)."""
        import gc
        if not self.revived or (self._started() if started is None else started):
            print(msg + ("" if self.failed_restarts >= self.MAX_RESTARTS else then), flush=True)
            self.new_pool(failed=True)
            return
        print(msg, flush=True)
        self._kill()
        gc.collect()
        self.restarts += 1
        self.failed_restarts += 1
        self._stop("nothing has scored since it started over, and its newest pool never loaded the scorer")

    def _send(self, fr, mark):
        """Submit one job. A missing pool is started, and a pool that is broken (a scoring process died) or refuses the
        job is replaced, each as a failed restart. Never raises; None when the lab is dead or the job could not go. A
        fresh pool that refuses its first job too is dropped there: its manager thread may not have started (no thread
        left to start), and CPython never starts it later, so every job sent to it would wait for good."""
        import concurrent.futures as cf
        for attempt in (0, 1):
            if self.dead:
                return None
            if self.pool is None:
                self.new_pool(failed=True)
                if self.pool is None:
                    return None
            try:
                return self.pool.submit(_lab_run, _lab_eval, fr, mark)
            except Exception as e:
                if attempt:
                    print("lab submit failed:", f"{type(e).__name__}: {e}"[:200], flush=True)
                    self._kill()  # the next send starts another pool, as a failed restart
                    break
                last = self.failed_restarts >= self.MAX_RESTARTS  # then the restart below stops the lab instead
                if isinstance(e, cf.BrokenExecutor):
                    print("lab: a scoring process died" + ("" if last else "; restarting the lab pool"), flush=True)
                else:
                    print("lab: the scoring pool refused a job", f"({type(e).__name__}: {e})"[:200]
                          + ("" if last else "; restarting it"), flush=True)
                self.new_pool(failed=True)
        return None

    def _resend(self, job):
        """Send job again under a fresh mark. False, and the job final (its failed future kept), if it could not go."""
        self._rm(job[4])
        job[5] += 1
        job[4] = self._mark(job[0], job[5])
        self._rm(job[4])
        nf = self._send(job[2], job[4])
        if nf is None:  # the lab is dead or the job could not go: collect() counts the failed future
            self._final(job)
            return False
        job[1] = nf
        self.resent += 1
        return True

    def _inflight(self):
        """The futures of the jobs being scored: not final and not waiting for the suspect slot."""
        waiting = set(self.suspects)
        return [j[1] for k, j in self.jobs.items() if j[2] is not None and k not in waiting and not j[1].done()]

    def _lost_manager(self):
        """Whether the current pool's manager thread died without breaking it. Before Python 3.12.1 it dies so when its
        queue feeder thread cannot start (no thread left), and nothing breaks the pool (gh-109047): it would never
        finish a job again."""
        mgr = getattr(self.pool, "_executor_manager_thread", None)
        return mgr is not None and mgr.ident is not None and not mgr.is_alive() \
            and getattr(self.pool, "_broken", None) is False

    def _reap(self):
        """Go over the jobs that finished since the last look; returns how many became suspects and wait for the slot
        or are in it. A job is final once it scored, failed on its own, or was charged more than RETRIES sends; its
        clips go then. A scored job proves the scorer works (the restart count starts over). A job that a dead or
        stopped pool failed (BrokenProcessPool, or cancelled) goes again: at once if no process had started it, through
        the suspect slot if one had (or had at an earlier failure). A pool that lost its manager thread without
        breaking is restarted first (a failed restart), so its jobs go again here too."""
        from concurrent.futures.process import BrokenProcessPool
        if self._lost_manager():  # it goes now, not after a quiet spell
            print("lab: the scoring pool lost its manager thread"
                  + ("" if self.failed_restarts >= self.MAX_RESTARTS else "; restarting the lab pool"), flush=True)
            self.new_pool(failed=True)  # its jobs fail in the graves (_bury) and go again below
        self._bury()  # a killed pool's manager thread may have died since without failing its jobs
        waiting = set(self.suspects)
        fresh, sus, spent = [], [], 0
        for k, job in list(self.jobs.items()):
            f = job[1]
            if job[2] is None or k in waiting or not f.done():
                continue
            e = None if f.cancelled() else f.exception()
            if f.cancelled() or isinstance(e, BrokenProcessPool):
                if job[4] and os.path.exists(job[4]):  # a process had started it
                    job[3] += 1
                if not self.dead and job[3] <= self.RETRIES:
                    if job[3]:
                        self.suspects.append(k)
                        sus.append(job)
                    else:
                        fresh.append(job)
                    continue
                spent += 1  # it had already gone again: it stays unscored
            elif e is None and (f.result() or {}).get("ok"):
                self._scored_one()
            self._final(job)
        went = sum(self._resend(job) for job in fresh)  # a resend can restart the pool, and that restart stop the lab
        self._next_suspect()  # so can the first suspect's send
        nsus, lost = len(sus), sum(job[2] is None for job in sus)  # lost: its send failed, so it is final
        if fresh or nsus or spent:
            n = len(fresh) + nsus + spent
            if self.dead:
                print(f"lab: a stopped scoring pool failed {n} jobs, and the lab stopped: they stay unscored",
                      flush=True)
            else:
                print(f"lab: a stopped scoring pool failed {n} jobs: {went} of the {len(fresh)} that had not started "
                      f"went again, {nsus - lost} that had started (this time or before) go again one at a time"
                      + (f", {lost} more that had started could not be sent again" if lost else "")
                      + (f", {spent} that had already gone again stay unscored" if spent else ""), flush=True)
        return nsus - lost  # the suspects now waiting their turn, or being scored

    def _scored_one(self):
        """A job scored: the scorer works, so the count of restarts in a row starts over (and a revived lab is back)."""
        self.failed_restarts, self.revive_in = 0, self.REVIVE
        if self.revived:
            self.revived = False
            print("lab: the fresh pool scores; the lab is back", flush=True)

    def _revive(self):
        """A stopped lab starts over (submit() calls this once its wait is over): a fresh pool, and the restarts in a
        row count from 0 again, so a candidate whose clips kill the scorer costs only itself here too, as at the start
        of the run. It is on trial (revived) until a job scores; a pool that hangs on trial without loading the scorer
        stops it again (_hung).
        Stopping again doubles the wait; a scored job re-arms it."""
        self.dead, self.revived, self.failed_restarts = False, True, 0
        print("lab: starting over with a fresh scoring pool", flush=True)
        self.new_pool()

    def _next_suspect(self, now=False):
        """The suspect slot: the oldest waiting suspect goes when no other suspect is being scored (or at once, when
        now; see submit). If it is what kills or hangs the scorer, the failure it causes then charges it and the jobs
        scored beside it; those were never charged, so they only become suspects, while it has used its RETRIES and is
        final."""
        waiting = set(self.suspects)
        if not now and any(j[3] and j[2] is not None and k not in waiting and not j[1].done()
                           for k, j in self.jobs.items()):
            return
        while self.suspects:
            job = self.jobs.get(self.suspects.pop(0))
            if job is None or job[2] is None:
                continue
            if self.dead:
                self._final(job)
                continue
            if self._resend(job):
                return

    def submit(self, r):
        """Queue r's clips for lab scoring (never raises). Waits while MAX_PENDING jobs are unfinished, so at most that
        many candidates' clips wait in this process; that wait counts toward lab_wait. After STALL seconds with no job
        finished (counted from the newest pool's start) the pool is taken as wedged and restarts (its jobs go again)."""
        import concurrent.futures as cf
        fr = r.pop("lframes", None)
        if not (self.on and fr is not None and r.get("ok")):
            return
        t0 = t_stall = time.time()
        try:
            if self.dead and t0 >= self.t_revive:
                self._revive()
            while not self.dead:
                self._reap()
                if sum(j[2] is not None for j in self.jobs.values()) < self.MAX_PENDING:
                    break
                busy = self._inflight()
                if self.suspects and len(busy) < self.n:  # suspects fill the bound and a scoring process idles
                    self._next_suspect(now=True)
                    continue
                if time.time() - max(t_stall, self.t_pool) > self.STALL:
                    self._hung(f"lab: no scoring finished in {self.STALL:.0f} s", "; restarting the lab pool")
                    t_stall = time.time()
                    continue
                if busy:
                    cf.wait(busy, timeout=0.5, return_when=cf.FIRST_COMPLETED)
                else:  # cannot happen while the lab lives (a waiting suspect is sent once the slot is free)
                    time.sleep(0.2)
            mark = self._mark(r, 1)
            self._rm(mark)
            f = self._send(fr, mark)
            if f is None:  # unscored (LAB_MISSING); the run goes on
                self.errors += 1
                return
            self.jobs[id(r)] = [r, f, fr, 0, mark, 1]
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
        # free core, and suspects one at a time. A hung scoring process shows as a quiet spell with no job finishing;
        # then the pool restarts and its jobs go again (the hung ones charged a send), at most GIVE_UPS times.
        def until(ks):
            sus = sum(1 for k in ks if self.jobs[k][3])
            return time.time() + 120 + 8.0 * nwin * ((len(ks) - sus) / self.n + sus)

        deadline, quiet, last, gave_up, broken = until(keys), self.QUIET + 8.0 * nwin, time.time(), 0, False
        t_pool = self.t_pool
        while True:
            nsus = self._reap()  # a job a dead pool failed goes again here too
            deadline += (20 + 8.0 * nwin) * nsus  # a new suspect waits its turn in the slot
            live = [k for k in keys if self.jobs[k][2] is not None]
            if not live:
                break
            if self.t_pool != t_pool:  # a pool started meanwhile: its backoff wait and its load do not count
                t_pool = self.t_pool
                deadline = max(deadline, until(live))
            busy = self._inflight()  # any job finishing, ours or not, shows the pool is alive
            if busy:
                done, _ = cf.wait(busy, timeout=1.0, return_when=cf.FIRST_COMPLETED)
            else:  # cannot happen while the lab lives (a waiting suspect is sent once the slot is free)
                done = ()
                time.sleep(0.2)
            now = time.time()
            if done:
                last = now
            if now > deadline or now - max(last, self.t_pool) > quiet:  # from the newest pool's start too
                if self.dead or gave_up >= self.GIVE_UPS:
                    broken = True
                    break
                self._hung("lab scoring timed out", "; restarting the lab pool")  # _reap() says what goes again
                gave_up += 1
                deadline, last = until(live), time.time()
        loaded = broken and not self.dead and self._started()  # before this payload's jobs and their marks go
        for k in keys:
            job = self.jobs.pop(k)
            r, f = job[0], job[1]
            self._rm(job[4])
            if not f.done() or (job[2] is not None and k in self.suspects):  # still scoring, or still waiting its turn
                out = {"ok": False, "err": "lab timeout"}
            elif f.cancelled():
                out = {"ok": False, "err": "lab worker: cancelled"}
            else:
                # BrokenProcessPool (a scoring process died), or whatever the scorer raised, SystemExit included
                e = f.exception()
                out = {"ok": False, "err": f"lab worker: {type(e).__name__}: {e}"[:200]} if e is not None \
                    else (f.result() or {})
            if out.get("ok") and out.get("per"):
                r["lab"] = round(float(np.mean(out["per"])), 4)
                r["lab_windows"] = [round(x, 3) for x in out["per"]]
                r["lab_nwin"] = len(out["per"])
                self.scored += 1
                if job[2] is not None:  # _reap() never saw this one finish (collect gave up just after)
                    self._scored_one()
            else:
                self.errors += 1
                if self.errors <= 5:
                    print("lab error:", out.get("err"), flush=True)
        self.suspects = [k for k in self.suspects if k in self.jobs]
        if broken and not self.dead:  # kill the hung process; a dead one's pool restarted when it was found
            self._hung("lab scoring timed out", "; restarting the lab pool", loaded)
        self.wait += time.time() - t0

    def close(self):
        """End of run: every job was collected, so the processes are killed rather than joined (a hung one would hold
        the interpreter's exit)."""
        self._kill()
        if self.mark_dir:
            import shutil
            shutil.rmtree(self.mark_dir, ignore_errors=True)


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


def _close_pool(pool, wait=5.0):
    """End of run: close() lets the idle render workers take their sentinel and exit normally, so each one's atexit
    hook stops its Xvfb (render.ensure_display; terminate() SIGTERMs them past it, and where setpriv is missing those
    servers outlived the island: review, 2026-11-11). Whatever has not exited after `wait` seconds (a worker killed
    while idle keeps the others from their sentinels) goes through _drop_pool()."""
    if pool is None:
        return
    try:
        pool.close()
    except Exception:
        pass
    t_end = time.time() + wait
    for p in list(getattr(pool, "_pool", None) or []):
        try:
            p.join(max(0.0, t_end - time.time()))
        except Exception:
            pass
    _drop_pool(pool)


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

    audits, audit_redo = {}, set()

    def audit(c):
        """Judge c on the --audit-offsets windows, which selection never uses (cached by bytes): the honest number for
        a published best. Stored on the row as c['audit']. One made while the lab was stopped, so without its lab
        number, is made again on a later call once the lab is no longer stopped; if that render fails, the audit keeps
        what it had and is tried again on the next call."""
        if not args.audit_offsets or not c.get("ok"):
            return None
        key = c["genome"].to_bytes()
        if key not in audits or (key in audit_redo and not lab.dead):
            redo = key in audits
            audit_redo.discard(key)
            (r,) = run_payload([job(c["genome"].to_json(), list(args.audit_offsets))])
            judge.score([r])
            lab.collect([r])
            if redo and not r.get("ok"):
                audit_redo.add(key)
            else:
                audits[key] = ({"judge": r.get("judge"), "judge_windows": r.get("judge_windows"),
                                "t": r.get("win_t"), "stat": round(r["stat"], 4),
                                **({"lab": r.get("lab"), "lab_windows": r.get("lab_windows")} if lab.on else {})}
                               if r.get("ok") else {"err": r.get("err")})
                if lab.on and lab.dead and r.get("ok") and r.get("lab") is None:
                    audit_redo.add(key)
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
                   "lab_wait": round(lab.wait, 1), "lab_errors": lab.errors, "lab_stops": lab.deaths}
                  if lab.on else {}),
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
    _close_pool(state["pool"])
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
                         "logged as best_audit (comma list; 'none' = off). Any start renders at its real time "
                         "(render.py caps the warmup alone at 60 s)")
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
