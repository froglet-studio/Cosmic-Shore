"""Selection for the genome search: survival, parent choice, operator choice and a parameter emitter.

Each piece is the current best practice for its job and is generic (plain numbers in, indices out; numpy only), so
the same code serves any search that trades a loss against a size:

  roi_order      SURVIVAL. Non-dominated sorting on (dist, size) inside a region of interest near the best dist,
                 each front ordered by exclusive 2-D hypervolume contribution (SMS-EMOA's greedy removal of the least
                 contributor, Beume, Naujoks and Emmerich 2007); outside the region, by dist. It replaces the
                 COMPLEXIFY/COMPRESS regime switch: the best-dist genome and the smallest genome near it survive in
                 the same generation, so fidelity never waits out a compression phase and compression never waits out
                 a fidelity phase.
  lexicase       PARENTS. Semi-dynamic epsilon-lexicase over per-case errors (La Cava, Spector and McPhee 2016; La
                 Cava, Helmuth, Spector and Moore 2019), epsilon = the median absolute deviation of each case over
                 the pool. A parent can be chosen for being the best at a few cases (colour, motion, one judge net)
                 rather than at the weighted sum, which keeps specialists alive as material for crossover.
  Matching       OPERATORS. Adaptive operator selection by probability matching (Goldberg 1990; Thierens 2005) with
                 a floor per arm, credited by whether the child survived selection.
  CMAEmitter     PARAMETERS. Separable CMA-ES (agenome.tune.SepCMA) on a block of the best genome's parameter bytes,
                 asked for a share of every generation's children and told their scores (a CMA-ME-style emitter,
                 Fontaine et al. 2020, inside the generational loop), re-anchored when a different structure becomes
                 the best.
"""
import math

import numpy as np


# ---------------------------------------------------------------------- survival: region-of-interest hypervolume
def dominates(a, b):
    """a dominates b on (f1, f2), both minimised."""
    return a[0] <= b[0] and a[1] <= b[1] and (a[0] < b[0] or a[1] < b[1])


def fronts(points):
    """Non-dominated fronts of points [(f1, f2)], as lists of indices, each sorted by f1 then f2. O(n^3) in the worst
    case, which is nothing for the hundred-odd candidates a generation has."""
    left = list(range(len(points)))
    out = []
    while left:
        cur = [i for i in left if not any(dominates(points[j], points[i]) for j in left if j != i)]
        cur.sort(key=lambda i: (points[i][0], points[i][1]))
        out.append(cur)
        gone = set(cur)
        left = [i for i in left if i not in gone]
    return out


def hv_contributions(front, ref):
    """Exclusive hypervolume contribution of each point of one front (mutually non-dominated, sorted by f1 ascending,
    hence f2 descending) to the area dominated below the reference point ref = (r1, r2). Points at or beyond the
    reference contribute 0."""
    n = len(front)
    out = []
    for k in range(n):
        right = front[k + 1][0] if k + 1 < n else ref[0]
        up = front[k - 1][1] if k > 0 else ref[1]
        out.append(max(0.0, min(right, ref[0]) - front[k][0]) * max(0.0, min(up, ref[1]) - front[k][1]))
    return out


def hv_order(points, ref):
    """Order the points of one front from most to least worth keeping: drop the least exclusive contributor, recompute
    its neighbours' contributions, repeat (SMS-EMOA), and return the reverse of the drop order. Ties drop the worse
    f1 first."""
    idx = sorted(range(len(points)), key=lambda i: (points[i][0], points[i][1]))
    dropped = []
    while idx:
        P = [points[i] for i in idx]
        c = hv_contributions(P, ref)
        k = min(range(len(idx)), key=lambda k: (c[k], -P[k][0], -P[k][1]))
        dropped.append(idx.pop(k))
    return dropped[::-1]


def hypervolume(points, ref):
    """Area dominated by points [(f1, f2)] (minimised) below ref, for logging and benchmarks."""
    pts = sorted((p for p in points if p[0] < ref[0] and p[1] < ref[1]), key=lambda p: (p[0], p[1]))
    # sweep along f1: each staircase step adds (next f1 - this f1) * (r2 - lowest f2 so far)
    area, low = 0.0, ref[1]
    stair = []
    for p in pts:
        if p[1] < low:
            low = p[1]
            stair.append((p[0], low))
    for k, (x, y) in enumerate(stair):
        x2 = stair[k + 1][0] if k + 1 < len(stair) else ref[0]
        area += (x2 - x) * (ref[1] - y)
    return area


def roi_bound(best, span):
    """The worst dist inside the region of interest: best + span * |best| (an absolute span when best is 0), so the
    band is right for losses of either sign (the lab engine's floor = 0.98 * peak inverted for negative scores)."""
    return best + span * abs(best) if best != 0 else span


def roi_order(points, span=0.05, size_ref=None, keep_best=True):
    """Survival order of candidates with objectives points = [(dist, size)], best first.

    Inside the region of interest (dist <= roi_bound(best dist, span) and size < size_ref): non-dominated fronts in
    turn, each ordered by hv_order against the reference point (roi_bound, size_ref); then every candidate outside
    the region, by dist then size. keep_best puts the best-dist candidate (smallest size among ties) first whatever
    its size, so the primary objective is elitist. size_ref None = 10% above the largest size in the region."""
    n = len(points)
    if n == 0:
        return []
    d0 = min(p[0] for p in points)
    r1 = roi_bound(d0, span)
    inside = [i for i in range(n) if points[i][0] <= r1 and (size_ref is None or points[i][1] < size_ref)]
    if size_ref is None:
        size_ref = 1.1 * max(points[i][1] for i in inside) + 1.0 if inside else 1.0
    # the reference sits a hair past the band so a candidate exactly on the bound still has area
    ref = (r1 + 1e-9 + 1e-6 * abs(r1), float(size_ref))
    order = []
    if inside:
        sub = [points[i] for i in inside]
        for fr in fronts(sub):
            order += [inside[fr[k]] for k in hv_order([sub[j] for j in fr], ref)]
    seen = set(order)
    order += sorted((i for i in range(n) if i not in seen), key=lambda i: (points[i][0], points[i][1]))
    if keep_best:
        b = min(range(n), key=lambda i: (points[i][0], points[i][1]))
        order.remove(b)
        order.insert(0, b)
    return order


# ---------------------------------------------------------------------- parents: epsilon-lexicase
def mad_eps(E):
    """Median absolute deviation of each column of E over its finite entries (0 where none)."""
    E = np.asarray(E, float)
    out = np.zeros(E.shape[1])
    for j in range(E.shape[1]):
        col = E[:, j][np.isfinite(E[:, j])]
        if len(col):
            out[j] = float(np.median(np.abs(col - np.median(col))))
    return out


class Lexicase:
    """Semi-dynamic epsilon-lexicase over a fixed pool: E[n, m] = per-case errors (lower is better; nan or inf =
    missing, never best). select(rng) shuffles the cases, keeps the candidates within epsilon of the best on each in
    turn, and returns one survivor at random (an index into the pool)."""

    def __init__(self, E):
        E = np.asarray(E, float)
        if E.ndim != 2:
            raise ValueError("E must be [candidates, cases]")
        self.E = np.where(np.isfinite(E), E, np.inf)
        self.eps = mad_eps(E)
        self.n, self.m = self.E.shape

    def select(self, rng):
        cand = np.arange(self.n)
        cases = list(range(self.m))
        rng.shuffle(cases)
        for j in cases:
            col = self.E[cand, j]
            best = col.min()
            if not np.isfinite(best):
                continue
            cand = cand[col <= best + self.eps[j]]
            if len(cand) == 1:
                break
        return int(cand[rng.randrange(len(cand))])


# ---------------------------------------------------------------------- operators: probability matching
class Matching:
    """Adaptive operator selection by probability matching. Each arm's quality Q is a recency-weighted mean of its
    rewards (alpha per generation), and an arm is picked with probability p_min + (1 - K p_min) Q / sum(Q): every arm
    keeps at least p_min, so a slow-paying operator (structure, crossover) is never starved by a quick-paying one
    (parameter tweaks). The prior mix is rescaled to the first rewards seen, so rewards of any scale (a survival
    rate near 0.3, a relative gain near 0.01) take over at the same pace."""

    def __init__(self, arms, p0=None, p_min=0.1, alpha=0.2):
        self.arms = list(arms)
        K = len(self.arms)
        if not 0 <= p_min * K < 1:
            raise ValueError("p_min * arms must be below 1")
        self.p_min, self.alpha = p_min, alpha
        p0 = p0 or {a: 1.0 / K for a in self.arms}
        tot = sum(p0[a] for a in self.arms)
        # start the qualities where the probabilities equal the prior mix (an arm below p_min starts at p_min), so
        # generation 0 draws the operators the fixed search would; rewards then take over at rate alpha
        self.Q = {a: max(0.0, p0[a] / tot - p_min) / (1 - K * p_min) for a in self.arms}
        self.scaled = False
        self.p = self._probs()

    def _probs(self):
        K = len(self.arms)
        s = sum(self.Q.values())
        if s <= 0:
            return {a: 1.0 / K for a in self.arms}
        return {a: self.p_min + (1 - K * self.p_min) * self.Q[a] / s for a in self.arms}

    def pick(self, rng):
        r, acc = rng.random(), 0.0
        for a in self.arms:
            acc += self.p[a]
            if r < acc:
                return a
        return self.arms[-1]

    def update(self, rewards):
        """rewards: {arm: [reward per child this generation]}; arms with no children keep their quality."""
        if not self.scaled:
            seen = [float(r) for a, rs in rewards.items() if a in self.Q for r in rs]
            m = float(np.mean(seen)) if seen else 0.0
            if m > 0:
                s = sum(self.Q.values()) or 1.0
                self.Q = {a: q / s * len(self.arms) * m for a, q in self.Q.items()}
                self.scaled = True
        for a, rs in rewards.items():
            if a in self.Q and rs:
                self.Q[a] = (1 - self.alpha) * self.Q[a] + self.alpha * float(np.mean(rs))
        self.p = self._probs()

    def state(self):
        return {a: round(self.p[a], 3) for a in self.arms}


# ---------------------------------------------------------------------- parameters: an in-loop sep-CMA-ES emitter
def structure_key(g):
    """A genome's structure without its parameter bytes: ops and wiring."""
    return tuple((n.op, tuple(n.inputs)) for n in g.nodes)


class CMAEmitter:
    """Separable CMA-ES on a block of the anchor genome's parameter bytes, k children per generation.

    A block runs for gens_per_block generations from the best genome (anchor) on up to `block` of its live parameter
    slots, then the next block starts from the best genome then. A block also ends early when a genome with another
    structure has become the best, so the emitter always tunes the current leader. After a block that ran its course
    the start step follows the offline tuner's rule (agenome.tune): kept after a block that improved the anchor, halved
    (to sigma_min) after one that did not, because a tuned genome sits in a narrow basin."""

    def __init__(self, block=24, gens_per_block=8, sigma=12.0, sigma_min=2.0, seed=0):
        self.block, self.gens_per_block = block, gens_per_block
        self.sigma0, self.sigma_init, self.sigma_min = sigma, sigma, sigma_min
        self.rng = np.random.default_rng(seed)
        self.es = None
        self.anchor = None      # the genome the block builds candidates from
        self.anchor_dist = None
        self.slots = []
        self.gens = 0
        self.best = None        # best dist this block
        self.pending = None
        self.blocks = 0

    def _start(self, cand, k):
        from .tune import SepCMA, param_slots
        g = cand["genome"]
        slots = param_slots(g)
        if self.es is not None and self.gens >= self.gens_per_block:
            # only a block that ran its course judges the step: a block cut short because another structure took the
            # lead says nothing about it (halving on those collapsed the step from 12 to 1.7 bytes in 40 generations)
            fruitful = self.best is not None and self.best < self.anchor_dist - 1e-9
            self.sigma0 = max(self.sigma0, self.es.sigma) if fruitful else max(self.sigma_min, 0.5 * self.sigma0)
        self.anchor, self.anchor_dist, self.anchor_key = g, float(cand["dist"]), structure_key(g)
        self.slots = [slots[i] for i in self.rng.permutation(len(slots))[: self.block]] if slots else []
        self.gens, self.best = 0, None
        self.blocks += 1
        if not self.slots:
            self.es = None
            return
        x0 = [g.nodes[i].params[j] for i, j in self.slots]
        self.es = SepCMA(x0, self.sigma0, lam=max(2, k), seed=int(self.rng.integers(1 << 30)))

    def ask(self, leader, k):
        """k child genomes. leader = the current best candidate (a dict with 'genome' and 'dist')."""
        stale = self.es is None or self.gens >= self.gens_per_block or self.es.lam != max(2, k)
        if not stale and structure_key(leader["genome"]) != self.anchor_key and leader["dist"] < (
                self.best if self.best is not None else self.anchor_dist) - 1e-9:
            stale = True  # another structure leads: tune it instead
        if stale:
            self._start(leader, k)
        if self.es is None:
            self.pending = None
            return []
        X = self.es.ask()
        kids, seen = [], {tuple(self.anchor.nodes[i].params[j] for i, j in self.slots)}
        for x in X:
            v = [int(round(t)) for t in x]
            for _ in range(8):  # rounding collapses small steps onto one byte vector: nudge duplicates apart
                if tuple(v) not in seen:
                    break
                j = int(self.rng.integers(len(v)))
                v[j] = int(np.clip(v[j] + (1 if self.rng.random() < 0.5 else -1), 0, 255))
            seen.add(tuple(v))
            h = self.anchor.copy()
            for (i, j), b in zip(self.slots, v):
                h.nodes[i].params[j] = b
            kids.append(h)
        self.pending = X
        return kids[:k]

    def tell(self, dists):
        """The dists of the children ask() returned, in order (a failed render counts as a very bad dist)."""
        if self.es is None or self.pending is None:
            return
        f = np.asarray(list(dists) + [99.0] * (len(self.pending) - len(dists)), float)[: len(self.pending)]
        f = np.where(np.isfinite(f), f, 99.0)
        self.es.tell(f)
        b = float(f.min())
        self.best = b if self.best is None else min(self.best, b)
        self.gens += 1
        self.pending = None

    def state(self):
        if self.es is None:
            return {"cma_block": self.blocks, "cma_slots": 0}
        return {"cma_block": self.blocks, "cma_slots": len(self.slots), "cma_gen": self.gens,
                "cma_sigma": round(float(self.es.sigma), 2)}
