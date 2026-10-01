"""sortfeel: sort_model.SortSwarm made ORGANIC without touching what makes it accurate.

Diagnosis (sortfeel_diag.py, results/sortfeel/diag.json): the sheets are NOT tissue boundaries and NOT
differential adhesion (switching adhesion OFF makes planar_frac WORSE everywhere: whale 0.69 -> 0.75,
jellyfish 0.41 -> 0.83). They come from COMPRESSION: every tadpole is pulled to the CENTRE of its fated
Gaussian well (gradient = inv(cov) @ d, a spring that is stiffest along the well's THIN axis) and stopped
only by collision. A crowd squeezed hardest along one axis packs into a pancake; with ~zero residual
motion (mean speed 0.019) it then sits as a frozen lattice. Halving the pull (weak_well) drops planar
below the plan's own value on every plan.

Two changes, both local, both off by default (base = the published sort):
  well_dead  FLAT-BOTTOMED wells: the fate pull is the gradient of 0.5 * max(0, m - m0)^2 where m is the
             Mahalanobis distance to the fated well; inside m0 (in units of the well's own sigma) there is
             no pull at all, so a well's tadpoles FILL its ellipsoid as a liquid instead of being crushed to
             its centre. Same wells, same fates, same code.
  wander     per-tadpole persistent wander: an Ornstein-Uhlenbeck velocity (amplitude `wander`, correlation
             time `wander_tau` steps) with a per-tadpole frequency spread; everyone does the same thing
             slightly differently. It never moves a tadpole out of its well (the well pull + collision
             bound it) - it melts the lattice and gives the body a living shimmer.
  breathe    optional: the dead zone m0 oscillates slowly (period `breathe_T`, per-type phase), so tissues
             gently swell and settle (look option; costs nothing).
Nothing here dies or is culled; composition, molting and transfer are sort's, unchanged.
"""
import math
import os
import sys
from dataclasses import dataclass, asdict

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sort_model as sm  # noqa: E402
import swarm_nca as sn  # noqa: E402

H_WAND = slice(22, 25)   # wander velocity (OU state)


@dataclass
class SortFeelCfg(sm.SortCfg):
    well_dead: float = 0.0      # m0: flat-bottom radius of a fated well, in well sigmas (0 = sort)
    wander: float = 0.0         # OU wander amplitude (voxels/step)
    wander_tau: float = 12.0    # OU correlation time (steps)
    breathe: float = 0.0        # amplitude of the m0 oscillation (well sigmas)
    breathe_T: float = 90.0     # its period (steps)


FEEL_GENES = [("well_dead", 0.0, 2.5, False), ("wander", 0.0, 0.25, False), ("wander_tau", 3.0, 40.0, True)]
GENES = sm.GENES + FEEL_GENES


def cfg_from_vec(z, base):
    d = asdict(base)
    for (n, lo, hi, lg), v in zip(GENES, z):
        b = d[n]
        if lg:
            val = math.exp(math.log(max(b, 1e-6)) + 0.5 * v)
        else:
            val = b + 0.15 * (hi - lo) * v
        d[n] = float(min(hi, max(lo, val)))
    d["vmax"] = tuple(d["vmax"])
    return SortFeelCfg(**d)


class _DeadCode:
    """Wraps a PlanCode: energy_grad_fate becomes the flat-bottomed well."""

    def __init__(self, code, m0_fn):
        self._c = code
        self._m0 = m0_fn

    def __getattr__(self, k):
        return getattr(self._c, k)

    def energy_grad_fate(self, x, e, s, k):
        E, Md = self._c.energy_grad_fate(x, e, s, k)
        m = np.sqrt(np.maximum(2 * E, 1e-12))
        m0 = self._m0(e, s)
        sc = np.clip(1 - m0 / m, 0, None)
        return 0.5 * np.clip(m - m0, 0, None) ** 2, Md * sc[:, None]


class SortFeel(sm.SortSwarm):
    def __init__(self, cfg=None, world=None):
        super().__init__(cfg or SortFeelCfg(), world)
        self._t = 0
        if self.cfg.well_dead > 0 or self.cfg.breathe > 0:
            self.codes = {k: _DeadCode(c, self._m0) for k, c in self.codes.items()}

    def _m0(self, e, s):
        c = self.cfg
        if c.breathe <= 0:
            return c.well_dead
        return max(0.0, c.well_dead + c.breathe * math.sin(2 * math.pi * self._t / c.breathe_T + 1.7 * (e * 3 + s)))

    def _step(self, sw, b):
        cfg = self.cfg
        self._t = int(sw.clock[b])
        if cfg.wander <= 0:
            return super()._step(sw, b)
        S = sw.s[b].numpy(); pos = sw.pos[b].numpy()
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        idx = np.nonzero(act & hat)[0]
        wsave = S[:, H_WAND].copy()          # the look pass rewrites hidden channels past H_FKEY
        super()._step(sw, b)
        S = sw.s[b].numpy(); pos = sw.pos[b].numpy()
        S[:, H_WAND] = wsave
        if len(idx) == 0:
            return
        rng = self.mem[b]["rng"]
        idx2 = np.nonzero(sw.active[b].numpy() & sw.hatched[b].numpy())[0]
        keep = np.intersect1d(idx, idx2)
        if len(keep) == 0:
            return
        # OU wander, per-tadpole time constant spread (+-40%, from the slot index: stable identity)
        tau = cfg.wander_tau * (0.6 + 0.8 * ((keep * 0.6180339) % 1.0))
        a = np.exp(-1.0 / tau)[:, None]
        w = S[keep, H_WAND]
        w = a * w + np.sqrt(1 - a ** 2) * cfg.wander * rng.normal(size=(len(keep), 3))
        S[keep, H_WAND] = w
        pos[keep] += w
        # newborns start with no wander
        fresh = np.setdiff1d(np.nonzero(act & ~hat)[0], idx2)
        S[fresh, H_WAND] = 0.0


def load(path):
    import json
    d = json.load(open(path))
    c = d["cfg"]; c["vmax"] = tuple(c["vmax"])
    return SortFeel(SortFeelCfg(**c))


def from_sort(path, **kw):
    import json
    c = json.load(open(path))["cfg"]; c["vmax"] = tuple(c["vmax"]); c.update(kw)
    return SortFeel(SortFeelCfg(**c))
