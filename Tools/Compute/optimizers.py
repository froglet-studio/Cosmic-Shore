"""The searchers a compute job can name (`algorithm.name`). Pure Python, no numpy: the expensive part is always the
evaluator, never the search, and a searcher with no dependency runs on every contributor's machine.

Every searcher works in the UNIT CUBE [0, 1]^n; `Space` maps it to the job's parameters (float, log-float, int,
choice) and back. Every searcher's whole state is JSON (`state()` / `from_state()`), so a machine that stops halfway
through a generation resumes exactly where it was, and the board can merge what several machines found.

    sep-cmaes   separable CMA-ES (Ros & Hansen 2008) with IPOP restarts that start from the best ANY machine has
                found half the time. The default: continuous and mixed spaces up to a few hundred parameters.
    map-elites  quality-diversity (Mouret & Clune 2015, Iso+LineDD variation, Vassiliades & Mouret 2018): keeps the
                best solution in every cell of a grid of DESCRIPTORS, so the result is a whole bestiary of different
                good answers rather than one. For "more diversity" jobs (bioforms, play styles, course families).
    random      uniform sampling: smoke tests and baselines.
"""
import math
import random


class Space:
    def __init__(self, dims):
        self.dims = dims
        self.n = len(dims)

    def decode(self, u):
        out = {}
        for d, x in zip(self.dims, u):
            x = min(1.0, max(0.0, x))
            t = d.get("type", "float")
            if t == "choice":
                c = d["choices"]
                out[d["name"]] = c[min(len(c) - 1, int(x * len(c)))]
            elif t == "int":
                lo, hi = int(d["lo"]), int(d["hi"])
                out[d["name"]] = min(hi, lo + int(x * (hi - lo + 1)))
            elif d.get("log"):
                lo, hi = math.log(d["lo"]), math.log(d["hi"])
                out[d["name"]] = math.exp(lo + x * (hi - lo))
            else:
                out[d["name"]] = d["lo"] + x * (d["hi"] - d["lo"])
        return out

    def encode(self, params):
        u = []
        for d in self.dims:
            v = params.get(d["name"], d.get("init"))
            t = d.get("type", "float")
            if v is None:
                u.append(0.5)
            elif t == "choice":
                c = d["choices"]
                u.append((c.index(v) + 0.5) / len(c) if v in c else 0.5)
            elif t == "int":
                lo, hi = int(d["lo"]), int(d["hi"])
                u.append(min(1.0, max(0.0, (int(v) - lo + 0.5) / (hi - lo + 1))))
            elif d.get("log"):
                lo, hi = math.log(d["lo"]), math.log(d["hi"])
                u.append(min(1.0, max(0.0, (math.log(max(v, 1e-300)) - lo) / (hi - lo))))
            else:
                u.append(min(1.0, max(0.0, (v - d["lo"]) / (d["hi"] - d["lo"]))))
        return u

    def init_point(self):
        return self.encode({d["name"]: d.get("init") for d in self.dims if d.get("init") is not None})

    def min_step(self):
        """Per dimension, the smallest unit-cube step that moves an int/choice parameter (so the search never
        stalls on a plateau it cannot see across)."""
        out = []
        for d in self.dims:
            t = d.get("type", "float")
            if t == "int":
                out.append(0.3 / (int(d["hi"]) - int(d["lo"]) + 1))
            elif t == "choice":
                out.append(0.3 / len(d["choices"]))
            else:
                out.append(0.0)
        return out


def _mirror(x):
    """Reflect into [0, 1] (a boundary that bounces keeps the distribution's shape near the wall)."""
    x = x % 2.0
    return 2.0 - x if x > 1.0 else x


class SepCMAES:
    name = "sep-cmaes"

    def __init__(self, space, seed=0, popsize=None, sigma0=0.25, x0=None, rng_state=None):
        self.space = space
        n = space.n
        self.rng = random.Random(seed)
        self.base_pop = popsize or (4 + int(3 * math.log(max(n, 1))))
        self.sigma0 = sigma0
        self.restarts = 0
        self.best = None                       # (loss, u)
        self._reset(x0 or [0.5] * n, self.base_pop)

    def _reset(self, x0, lam):
        n = self.space.n
        self.lam = lam
        self.mu = lam // 2
        w = [math.log(self.mu + 0.5) - math.log(i + 1) for i in range(self.mu)]
        s = sum(w)
        self.w = [x / s for x in w]
        self.mueff = 1.0 / sum(x * x for x in self.w)
        me = self.mueff
        self.cs = (me + 2) / (n + me + 5)
        self.ds = 1 + 2 * max(0.0, math.sqrt((me - 1) / (n + 1)) - 1) + self.cs
        self.cc = (4 + me / n) / (n + 4 + 2 * me / n)
        c1 = 2 / ((n + 1.3) ** 2 + me)
        cmu = min(1 - c1, 2 * (me - 2 + 1 / me) / ((n + 2) ** 2 + me))
        scale = (n + 2) / 3.0                  # the separable variant's faster learning rates
        self.c1 = min(1.0, c1 * scale)
        self.cmu = min(1.0 - self.c1, cmu * scale)
        self.chin = math.sqrt(n) * (1 - 1 / (4 * n) + 1 / (21 * n * n))
        self.m = [min(1.0, max(0.0, x)) for x in x0]
        self.sigma = self.sigma0
        self.C = [1.0] * n
        self.pc = [0.0] * n
        self.ps = [0.0] * n
        self.gen = 0
        self.gen_best = []                     # this run's best per generation (stagnation test)
        self.pending = []                      # [(u, z)] asked and not yet told

    def ask(self, count=None):
        n = self.space.n
        floor = self.space.min_step()
        out = []
        for _ in range(count or self.lam):
            z = [self.rng.gauss(0, 1) for _ in range(n)]
            u = []
            for i in range(n):
                sd = max(self.sigma * math.sqrt(self.C[i]), floor[i])
                u.append(_mirror(self.m[i] + sd * z[i]))
            out.append(u)
        return out

    def tell(self, us, losses, global_best_u=None):
        """One generation. `us` are the points asked (any count >= mu); losses lower = better. Returns True when this
        tell restarted the search."""
        n = self.space.n
        pairs = sorted(((l if math.isfinite(l) else 1e300), u) for u, l in zip(us, losses))
        if self.best is None or pairs[0][0] < self.best[0]:
            self.best = (pairs[0][0], list(pairs[0][1]))
        self.gen_best.append(pairs[0][0])
        sel = [u for _, u in pairs[:self.mu]]
        mu = len(sel)
        w = self.w[:mu]
        ws = sum(w)
        w = [x / ws for x in w]
        old = self.m
        self.m = [sum(w[k] * sel[k][i] for k in range(mu)) for i in range(n)]
        yw = [(self.m[i] - old[i]) / self.sigma for i in range(n)]
        cs, cc, me = self.cs, self.cc, self.mueff
        self.ps = [(1 - cs) * self.ps[i] + math.sqrt(cs * (2 - cs) * me) * yw[i] / math.sqrt(self.C[i]) for i in range(n)]
        psn = math.sqrt(sum(x * x for x in self.ps))
        hs = psn / math.sqrt(1 - (1 - cs) ** (2 * (self.gen + 1))) < (1.4 + 2 / (n + 1)) * self.chin
        self.pc = [(1 - cc) * self.pc[i] + (math.sqrt(cc * (2 - cc) * me) * yw[i] if hs else 0.0) for i in range(n)]
        for i in range(n):
            rank_mu = sum(w[k] * ((sel[k][i] - old[i]) / self.sigma) ** 2 for k in range(mu))
            self.C[i] = ((1 - self.c1 - self.cmu) * self.C[i]
                         + self.c1 * (self.pc[i] ** 2 + (0 if hs else cc * (2 - cc) * self.C[i]))
                         + self.cmu * rank_mu)
            self.C[i] = min(max(self.C[i], 1e-20), 1e20)
        self.sigma *= math.exp(min(1.0, (cs / self.ds) * (psn / self.chin - 1)))
        self.sigma = min(self.sigma, 1.0)
        self.gen += 1
        return self._maybe_restart(global_best_u)

    def _maybe_restart(self, global_best_u):
        n = self.space.n
        spread = max(self.sigma * math.sqrt(c) for c in self.C)
        stall = 10 + int(30 * n / self.lam)
        stagnant = len(self.gen_best) > stall and min(self.gen_best[-stall:]) >= min(self.gen_best[:-stall]) - 1e-12
        cond = max(self.C) / max(min(self.C), 1e-30) > 1e14
        if spread < 1e-7 or stagnant or cond:
            self.restarts += 1
            lam = min(self.base_pop * (2 ** self.restarts), 512)
            if global_best_u is not None and self.rng.random() < 0.5:
                x0 = list(global_best_u)          # exploit what the whole team found
            elif self.best is not None and self.rng.random() < 0.3:
                x0 = list(self.best[1])
            else:
                x0 = [self.rng.random() for _ in range(n)]
            self._reset(x0, lam)
            return True
        return False

    def state(self):
        return {"name": self.name, "rng": _rng_state(self.rng), "base_pop": self.base_pop, "sigma0": self.sigma0,
                "restarts": self.restarts, "best": self.best, "lam": self.lam, "m": self.m, "sigma": self.sigma,
                "C": self.C, "pc": self.pc, "ps": self.ps, "gen": self.gen, "gen_best": self.gen_best[-400:]}

    @classmethod
    def from_state(cls, space, s):
        o = cls(space, popsize=s["base_pop"], sigma0=s["sigma0"], x0=s["m"])
        o._reset(s["m"], s["lam"])
        o.rng = _rng_load(s["rng"])
        o.restarts, o.best = s["restarts"], (tuple(s["best"]) if s.get("best") else None)
        o.sigma, o.C, o.pc, o.ps, o.gen, o.gen_best = s["sigma"], s["C"], s["pc"], s["ps"], s["gen"], s["gen_best"]
        return o


class MapElites:
    name = "map-elites"

    def __init__(self, space, descriptors, seed=0, batch=16, sigma_iso=0.04, sigma_line=0.2, init_random=64):
        self.space = space
        self.desc = descriptors                  # [{name, lo, hi, bins}]
        self.rng = random.Random(seed)
        self.batch = batch
        self.sigma_iso, self.sigma_line = sigma_iso, sigma_line
        self.init_random = init_random
        self.archive = {}                        # "i,j,k" -> {loss, u, descriptors, metrics}
        self.seen = 0
        self.best = None
        self.seeds = []                          # points to try first (shipped forms, a hand guess)

    @property
    def cells_total(self):
        t = 1
        for d in self.desc:
            t *= int(d["bins"])
        return t

    def cell_of(self, values):
        idx = []
        for d in self.desc:
            v = values.get(d["name"]) if isinstance(values, dict) else values[len(idx)]
            if v is None or not math.isfinite(v):
                return None
            k = int((v - d["lo"]) / (d["hi"] - d["lo"]) * d["bins"])
            idx.append(min(int(d["bins"]) - 1, max(0, k)))
        return ",".join(map(str, idx))

    def ask(self, count=None):
        n = self.space.n
        out = []
        for _ in range(count or self.batch):
            if self.seeds:
                out.append(self.seeds.pop(0))
                continue
            if len(self.archive) < 2 or self.seen < self.init_random:
                out.append([self.rng.random() for _ in range(n)])
                continue
            keys = list(self.archive)
            a = self.archive[self.rng.choice(keys)]["u"]
            b = self.archive[self.rng.choice(keys)]["u"]
            line = self.rng.gauss(0, 1) * self.sigma_line
            out.append([_mirror(a[i] + self.rng.gauss(0, 1) * self.sigma_iso + line * (b[i] - a[i])) for i in range(n)])
        return out

    def tell(self, us, results, global_best_u=None):
        """results: evaluator dicts (loss, descriptors, valid, metrics). Returns how many cells improved."""
        improved = 0
        for u, r in zip(us, results):
            self.seen += 1
            if not r or not r.get("valid", True) or not math.isfinite(r.get("loss", float("inf"))):
                continue
            cell = self.cell_of(r.get("descriptors") or {})
            if cell is None:
                continue
            cur = self.archive.get(cell)
            if cur is None or r["loss"] < cur["loss"]:
                self.archive[cell] = {"loss": r["loss"], "u": list(u), "descriptors": r.get("descriptors"),
                                      "metrics": r.get("metrics", {})}
                improved += 1
            if self.best is None or r["loss"] < self.best[0]:
                self.best = (r["loss"], list(u))
        return improved

    def qd_score(self, worst):
        """Sum over filled cells of how far each elite is below `worst` (the loss an acceptable answer must beat)."""
        return sum(max(0.0, worst - e["loss"]) for e in self.archive.values())

    def merge(self, cells):
        """Adopt elites another machine published (their decoded params are re-encoded by the caller)."""
        for cell, e in cells.items():
            cur = self.archive.get(cell)
            if "u" in e and (cur is None or e["loss"] < cur["loss"]):
                self.archive[cell] = {k: e[k] for k in ("loss", "u", "descriptors", "metrics") if k in e}

    def state(self):
        return {"name": self.name, "rng": _rng_state(self.rng), "archive": self.archive, "seen": self.seen,
                "best": self.best, "batch": self.batch, "sigma_iso": self.sigma_iso, "sigma_line": self.sigma_line,
                "init_random": self.init_random}

    @classmethod
    def from_state(cls, space, descriptors, s):
        o = cls(space, descriptors, batch=s["batch"], sigma_iso=s["sigma_iso"], sigma_line=s["sigma_line"],
                init_random=s["init_random"])
        o.rng = _rng_load(s["rng"])
        o.archive, o.seen = s["archive"], s["seen"]
        o.best = tuple(s["best"]) if s.get("best") else None
        return o


class RandomSearch:
    name = "random"

    def __init__(self, space, seed=0, batch=16):
        self.space, self.rng, self.batch, self.best = space, random.Random(seed), batch, None

    def ask(self, count=None):
        return [[self.rng.random() for _ in range(self.space.n)] for _ in range(count or self.batch)]

    def tell(self, us, losses, global_best_u=None):
        for u, l in zip(us, losses):
            if math.isfinite(l) and (self.best is None or l < self.best[0]):
                self.best = (l, list(u))
        return False

    def state(self):
        return {"name": self.name, "rng": _rng_state(self.rng), "batch": self.batch, "best": self.best}

    @classmethod
    def from_state(cls, space, s):
        o = cls(space, batch=s["batch"])
        o.rng = _rng_load(s["rng"])
        o.best = tuple(s["best"]) if s.get("best") else None
        return o


def _rng_state(rng):
    v, internal, g = rng.getstate()
    return [v, list(internal), g]


def _rng_load(s):
    r = random.Random()
    r.setstate((s[0], tuple(s[1]), s[2]))
    return r


def make(job, space, seed, x0=None):
    a = dict(job.get("algorithm") or {})
    name = a.get("name", "sep-cmaes")
    if name == "sep-cmaes":
        return SepCMAES(space, seed=seed, popsize=a.get("popsize"), sigma0=a.get("sigma0", 0.25), x0=x0)
    if name == "map-elites":
        return MapElites(space, a["descriptors"], seed=seed, batch=a.get("batch", 16),
                         sigma_iso=a.get("sigma_iso", 0.04), sigma_line=a.get("sigma_line", 0.2),
                         init_random=a.get("init_random", 64))
    if name == "random":
        return RandomSearch(space, seed=seed, batch=a.get("batch", 16))
    raise ValueError(f"unknown algorithm {name}")


def restore(job, space, s):
    name = s.get("name")
    if name == "sep-cmaes":
        return SepCMAES.from_state(space, s)
    if name == "map-elites":
        return MapElites.from_state(space, job["algorithm"]["descriptors"], s)
    if name == "random":
        return RandomSearch.from_state(space, s)
    raise ValueError(f"unknown state {name}")


def self_test():
    """Each searcher must actually search: sep-CMA-ES finds a shifted sphere's and Rastrigin-lite's minimum, MAP-Elites
    fills most of a 2-D grid, and a state round-trip continues identically. A negative control: a searcher handed
    shuffled losses must NOT converge on the sphere."""
    bad = []
    dims = [{"name": f"x{i}", "lo": -5.0, "hi": 5.0} for i in range(8)]
    sp = Space(dims)
    target = [1.234, -2.0, 0.5, 3.3, -4.1, 0.0, 2.2, -1.1]

    def sphere(u):
        p = sp.decode(u)
        return sum((p[f"x{i}"] - target[i]) ** 2 for i in range(8))

    es = SepCMAES(sp, seed=1)
    for _ in range(300):
        us = es.ask()
        es.tell(us, [sphere(u) for u in us])
    if es.best[0] > 1e-6:
        bad.append(f"sep-cmaes: sphere best {es.best[0]:.2e} (want < 1e-6)")
    # state round trip continues identically
    a = SepCMAES(sp, seed=7)
    for _ in range(5):
        us = a.ask()
        a.tell(us, [sphere(u) for u in us])
    b = SepCMAES.from_state(sp, a.state())
    if a.ask() != b.ask():
        bad.append("sep-cmaes: a restored state asks different points")
    # negative control
    rnd = random.Random(3)
    nc = SepCMAES(sp, seed=2)
    for _ in range(100):
        us = nc.ask()
        ls = [sphere(u) for u in us]
        rnd.shuffle(ls)
        nc.tell(us, ls)
    if sphere(nc.m) < 1e-3:
        bad.append("negative control: shuffled losses still converged (the test cannot see a broken search)")
    # integers and choices decode and the floor keeps them moving
    sp2 = Space([{"name": "k", "type": "int", "lo": 0, "hi": 9}, {"name": "c", "type": "choice", "choices": ["a", "b", "c"]}])
    es2 = SepCMAES(sp2, seed=4)
    for _ in range(60):
        us = es2.ask()
        es2.tell(us, [abs(sp2.decode(u)["k"] - 7) + (0 if sp2.decode(u)["c"] == "b" else 1) for u in us])
    if sp2.decode(es2.best[1]) != {"k": 7, "c": "b"}:
        bad.append(f"sep-cmaes: int/choice best {sp2.decode(es2.best[1])}")
    # MAP-Elites fills a grid
    sp3 = Space([{"name": "a", "lo": 0.0, "hi": 1.0}, {"name": "b", "lo": 0.0, "hi": 1.0}, {"name": "c", "lo": 0.0, "hi": 1.0}])
    me = MapElites(sp3, [{"name": "a", "lo": 0, "hi": 1, "bins": 8}, {"name": "b", "lo": 0, "hi": 1, "bins": 8}], seed=5)
    for _ in range(80):
        us = me.ask()
        res = []
        for u in us:
            p = sp3.decode(u)
            res.append({"loss": (p["c"] - 0.3) ** 2, "descriptors": {"a": p["a"], "b": p["b"]}})
        me.tell(us, res)
    if len(me.archive) < 0.85 * me.cells_total:
        bad.append(f"map-elites: filled {len(me.archive)} of {me.cells_total} cells")
    if max(e["loss"] for e in me.archive.values()) > 0.05:
        bad.append("map-elites: elites did not improve in quality")
    if MapElites.from_state(sp3, me.desc, me.state()).archive != me.archive:
        bad.append("map-elites: state round trip lost the archive")
    return bad
