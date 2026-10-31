"""pareto.py - a small Pareto archive and the "diamond" regime switch, for coevo.py.

Modelled on the NCA ecology lab's pareto_complexity.py (Candidate/dominates/ParetoArchive/DiamondController, on the
lab's Google Drive): the same ideas, generalised to any number of maximised objectives, and the controller's
COMPLEXIFY/COMPRESS regimes re-labelled FIT/DIVERSIFY with the same leaky soft floor. Pure stdlib.
"""
from __future__ import annotations

import math


def dominates(a: tuple[float, ...], b: tuple[float, ...]) -> bool:
    """a dominates b: no worse on every objective (all maximised) and better on one."""
    return all(x >= y for x, y in zip(a, b)) and any(x > y for x, y in zip(a, b))


def non_dominated(items: dict[str, tuple[float, ...]]) -> list[str]:
    keys = list(items)
    return [k for k in keys if not any(dominates(items[o], items[k]) for o in keys if o != k)]


def fronts(items: dict[str, tuple[float, ...]]) -> list[list[str]]:
    """Non-dominated sorting: front 0 first."""
    left = dict(items)
    out = []
    while left:
        f = non_dominated(left)
        out.append(f)
        for k in f:
            left.pop(k)
    return out


def crowding(items: dict[str, tuple[float, ...]], keys: list[str]) -> dict[str, float]:
    dist = {k: 0.0 for k in keys}
    if len(keys) <= 2:
        return {k: math.inf for k in keys}
    m = len(next(iter(items.values())))
    for j in range(m):
        ks = sorted(keys, key=lambda k: items[k][j])
        lo, hi = items[ks[0]][j], items[ks[-1]][j]
        dist[ks[0]] = dist[ks[-1]] = math.inf
        if hi - lo <= 1e-12:
            continue
        for i in range(1, len(ks) - 1):
            dist[ks[i]] += (items[ks[i + 1]][j] - items[ks[i - 1]][j]) / (hi - lo)
    return dist


def truncate(items: dict[str, tuple[float, ...]], cap: int) -> list[str]:
    """The best `cap` keys by front, then crowding distance (NSGA-II)."""
    keep: list[str] = []
    for f in fronts(items):
        if len(keep) + len(f) <= cap:
            keep += f
            continue
        cd = crowding(items, f)
        keep += sorted(f, key=lambda k: -cd[k])[: cap - len(keep)]
        break
    return keep


def leaky_relu(x: float, alpha: float = 0.01) -> float:
    return x if x > 0 else alpha * x


def soft_floor_score(fit: float, div: float, peak_fit: float, margin: float, beta: float) -> float:
    """The DIVERSIFY regime's score: diversity, minus a leaky penalty once fit drops below (peak - margin)."""
    return div - beta * leaky_relu((peak_fit - margin) - fit)


class Regime:
    """FIT <-> DIVERSIFY, per side. mode 'alternate' flips every time the side evolves; 'patience' flips when the
    regime's own score has not improved by `eps` for `patience` evolving generations (the diamond controller)."""

    FIT, DIVERSIFY = "FIT", "DIVERSIFY"

    def __init__(self, state: dict | None = None, mode: str = "alternate", patience: int = 3, eps: float = 1e-3):
        s = state or {}
        self.mode = s.get("mode", mode)
        self.current = s.get("current", self.FIT)
        self.best = s.get("best", -math.inf)
        self.stale = s.get("stale", 0)
        self.patience = s.get("patience", patience)
        self.eps = s.get("eps", eps)
        self.history = s.get("history", [])

    def step(self, score: float) -> str:
        """Record this generation's best regime score; returns the regime for the side's NEXT evolving generation."""
        self.history.append([self.current, score])
        if self.mode == "alternate":
            self.current = self.DIVERSIFY if self.current == self.FIT else self.FIT
            return self.current
        if score > self.best + self.eps:
            self.best, self.stale = score, 0
        else:
            self.stale += 1
        if self.stale >= self.patience:
            self.current = self.DIVERSIFY if self.current == self.FIT else self.FIT
            self.best, self.stale = -math.inf, 0
        return self.current

    def to_json(self) -> dict:
        return {"mode": self.mode, "current": self.current, "best": self.best if math.isfinite(self.best) else None,
                "stale": self.stale, "patience": self.patience, "eps": self.eps, "history": self.history}

    @classmethod
    def from_json(cls, s: dict) -> "Regime":
        r = cls(s)
        if s.get("best") is None:
            r.best = -math.inf
        return r
