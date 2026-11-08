#!/usr/bin/env python3
"""
pareto_complexity.py -- regime-switching Pareto optimizer (pure stdlib).

Copied unchanged in behaviour from Garrett's nca_ecology_lab Drive folder (2026-10-18) so Froglet Compute can run it.

The search backbone for the NCA Ecology Lab. Unlike a brute-force sweep of a
fixed landscape, this treats the *complexity* (structure) of a solution as a
first-class objective under selection, and alternates two regimes to trace a
"diamond" trajectory of grow -> shrink -> grow:

  COMPLEXIFY : push performance, let the genome grow. Adding structure is how
               the search escapes an asymptote (a plateau it can't beat at the
               current complexity).
  COMPRESS   : hold performance with a *leaky-ReLU floor* while aggressively
               minimizing complexity -- strip out any structure that isn't
               paying for itself. Slides the population down the Pareto front
               toward parsimony.

Regime flips are triggered by plateau/asymptote detection and an optional hard
complexity budget, so the genome grows only as much as it must, then distills.

Lineage: Pareto selection over (performance up, complexity down) as in Eureqa /
symbolic regression (Schmidt & Lipson, Cornell) and Age-Fitness Pareto
Optimization; open-ended complexification as in NEAT/POET. The leaky-ReLU floor
is the concrete "hold the score, drive complexity down" operator.

The engine is generic: implement the Problem protocol (seed/evaluate/grow/shrink)
and the same controller drives a 3D-NCA genome, a GA racing genome, an equation,
etc. identically. Only numeric scalars cross the boundary, so "complexity" can be
param count, active channels, genome length, tree size -- whatever you define.
"""
from __future__ import annotations

import random
from dataclasses import dataclass, field
from typing import Any, Optional, Protocol


def leaky_relu(x: float, leak: float = 0.1) -> float:
    """Standard leaky ReLU. Used on (floor - score): positive => performance is
    below the floor and is penalized at full weight; negative => we're above the
    floor and only a small 'leak' of reward flows, so complexity dominates."""
    return x if x > 0.0 else leak * x


@dataclass
class Candidate:
    genome: Any
    score: float = float("-inf")       # performance, higher is better
    complexity: float = float("inf")   # structural cost, lower is better
    meta: dict = field(default_factory=dict)


def dominates(a: Candidate, b: Candidate) -> bool:
    """Pareto domination for (maximize score, minimize complexity)."""
    return (
        a.score >= b.score
        and a.complexity <= b.complexity
        and (a.score > b.score or a.complexity < b.complexity)
    )


class ParetoArchive:
    """Non-dominated set over (score up, complexity down) -- the Pareto front."""

    def __init__(self) -> None:
        self.members: list[Candidate] = []

    def add(self, c: Candidate) -> bool:
        for m in self.members:
            if dominates(m, c) or (m.score == c.score and m.complexity == c.complexity):
                return False
        self.members = [m for m in self.members if not dominates(c, m)]
        self.members.append(c)
        return True

    def best_score(self) -> Candidate:
        return max(self.members, key=lambda m: (m.score, -m.complexity))

    def simplest_at_or_above(self, floor: float) -> Optional[Candidate]:
        above = [m for m in self.members if m.score >= floor]
        return min(above, key=lambda m: m.complexity) if above else None

    def min_complexity(self) -> float:
        return min(m.complexity for m in self.members)

    def __len__(self) -> int:
        return len(self.members)


class Problem(Protocol):
    """A search problem. Keep genomes hashable/cloneable; return plain floats."""

    def seed(self, rng: random.Random) -> Any: ...
    def evaluate(self, genome: Any) -> tuple[float, float]: ...   # (score, complexity)
    def grow(self, genome: Any, rng: random.Random) -> Any: ...   # complexity-increasing edit
    def shrink(self, genome: Any, rng: random.Random) -> Any: ... # complexity-decreasing edit


COMPLEXIFY = "COMPLEXIFY"
COMPRESS = "COMPRESS"


@dataclass
class DiamondConfig:
    generations: int = 400
    children: int = 8                 # offspring evaluated per generation
    patience: int = 15                # gens w/o progress => plateau/asymptote
    improve_eps: float = 1e-9
    complexity_budget: float = float("inf")  # hard cap; exceeding it forces COMPRESS
    retain_fraction: float = 0.98     # COMPRESS must keep >= frac * peak score
    leak: float = 0.1                 # leaky-ReLU slope below the floor
    beta: float = 4.0                 # weight on the performance-floor term
    seed: int = 0


class DiamondController:
    """Alternates COMPLEXIFY<->COMPRESS to trace a grow/shrink diamond trajectory.

    Call run() to get the final Pareto archive; `.history` holds a per-generation
    record suitable for a Dispatch digest (regime, best score, min complexity...)."""

    def __init__(self, problem: Problem, cfg: DiamondConfig = DiamondConfig()) -> None:
        self.p = problem
        self.cfg = cfg
        self.rng = random.Random(cfg.seed)
        self.archive = ParetoArchive()
        self.regime = COMPLEXIFY
        self.peak_score = float("-inf")
        self.gens_since_progress = 0
        self.history: list[dict] = []

    def _eval(self, genome: Any) -> Candidate:
        s, c = self.p.evaluate(genome)
        return Candidate(genome, float(s), float(c))

    def _compress_objective(self, c: Candidate, floor: float) -> float:
        """Minimize: complexity, with a leaky floor protecting performance.
        Above the floor the floor-term is a small reward; below it, a full penalty."""
        return c.complexity + self.cfg.beta * leaky_relu(floor - c.score, self.cfg.leak)

    def run(self) -> ParetoArchive:
        start = self._eval(self.p.seed(self.rng))
        self.archive.add(start)
        self.peak_score = start.score
        cur = start
        for gen in range(self.cfg.generations):
            cur = self._step_complexify(cur) if self.regime == COMPLEXIFY else self._step_compress(cur)
            self._maybe_switch(cur)
            self._log(gen, cur)
        return self.archive

    def _step_complexify(self, cur: Candidate) -> Candidate:
        children = [self._eval(self.p.grow(cur.genome, self.rng)) for _ in range(self.cfg.children)]
        for ch in children:
            self.archive.add(ch)
        best_child = max(children, key=lambda c: c.score)   # climb score; structure rides along
        if best_child.score > self.peak_score + self.cfg.improve_eps:
            self.peak_score = best_child.score
            self.gens_since_progress = 0
        else:
            self.gens_since_progress += 1
        return best_child

    def _step_compress(self, cur: Candidate) -> Candidate:
        floor = self.cfg.retain_fraction * self.peak_score
        base = self.archive.simplest_at_or_above(floor) or self.archive.best_score()
        best, best_obj = base, self._compress_objective(base, floor)
        for _ in range(self.cfg.children):
            child = self._eval(self.p.shrink(base.genome, self.rng))
            self.archive.add(child)
            obj = self._compress_objective(child, floor)
            if obj < best_obj:
                best, best_obj = child, obj
        if best.complexity < base.complexity - self.cfg.improve_eps:
            self.gens_since_progress = 0
        else:
            self.gens_since_progress += 1
        return best

    def _maybe_switch(self, cur: Candidate) -> None:
        if self.regime == COMPLEXIFY:
            if cur.complexity > self.cfg.complexity_budget or self.gens_since_progress >= self.cfg.patience:
                self.regime = COMPRESS
                self.gens_since_progress = 0
        else:
            if self.gens_since_progress >= self.cfg.patience:
                self.regime = COMPLEXIFY
                self.gens_since_progress = 0

    def _log(self, gen: int, cur: Candidate) -> None:
        bs = self.archive.best_score()
        self.history.append(
            dict(
                gen=gen,
                regime=self.regime,
                best_score=round(bs.score, 4),
                cur_complexity=round(cur.complexity, 4),
                min_complexity=round(self.archive.min_complexity(), 4),
                peak_score=round(self.peak_score, 4),
                archive=len(self.archive),
            )
        )


# --------------------------------------------------------------------------- #
# Self-test: a toy problem that demonstrates the diamond trajectory on CPU.   #
# A hidden target subset of features must be "covered" (score). Any extra     #
# active feature is pure complexity. COMPLEXIFY overshoots (accretes spurious #
# structure while climbing coverage); COMPRESS then strips it back out while  #
# the leaky floor holds coverage at its peak.                                 #
# --------------------------------------------------------------------------- #
class FeatureSelectionDemo:
    def __init__(self, n_features: int = 60, target_size: int = 12, seed: int = 1) -> None:
        r = random.Random(seed)
        self.n = n_features
        self.target = frozenset(r.sample(range(n_features), target_size))

    def seed(self, rng: random.Random) -> frozenset:
        return frozenset()

    def evaluate(self, g: frozenset) -> tuple[float, float]:
        return float(len(g & self.target)), float(len(g))

    def grow(self, g: frozenset, rng: random.Random) -> frozenset:
        inactive = [i for i in range(self.n) if i not in g]
        return frozenset(g | {rng.choice(inactive)}) if inactive else g

    def shrink(self, g: frozenset, rng: random.Random) -> frozenset:
        active = list(g)
        if not active:
            return g
        active.remove(rng.choice(active))
        return frozenset(active)


def _print_diamond(ctrl: DiamondController, every: int = 10) -> None:
    h = ctrl.history
    peak_c = max(r["cur_complexity"] for r in h)
    switch_gens = [h[i]["gen"] for i in range(1, len(h)) if h[i]["regime"] != h[i - 1]["regime"]]
    print(f"{'gen':>4} {'regime':<11} {'score':>6} {'cur_cplx':>9} {'min_cplx':>9}")
    for r in h:
        if r["gen"] % every == 0 or r["gen"] in switch_gens or r["gen"] == h[-1]["gen"]:
            mark = " <- regime flip" if r["gen"] in switch_gens else ""
            print(f"{r['gen']:>4} {r['regime']:<11} {r['best_score']:>6} "
                  f"{r['cur_complexity']:>9} {r['min_complexity']:>9}{mark}")
    final = ctrl.archive.simplest_at_or_above(ctrl.peak_score) or ctrl.archive.best_score()
    print(f"\nregime flips at gens: {switch_gens}")
    print(f"peak complexity reached: {peak_c}")
    print(f"final: score={final.score} complexity={final.complexity} "
          f"(target size was {len(ctrl.p.target)})")  # type: ignore[attr-defined]
    print("diamond =",
          "YES (grew then shrank while holding score)"
          if peak_c > final.complexity and final.score >= 0.98 * ctrl.peak_score
          else "not observed")


if __name__ == "__main__":
    problem = FeatureSelectionDemo(n_features=60, target_size=12, seed=1)
    cfg = DiamondConfig(generations=200, children=8, patience=15, seed=0)
    ctrl = DiamondController(problem, cfg)
    ctrl.run()
    _print_diamond(ctrl, every=10)
