"""Stigmergy fields: coarse 3D grids a cell owns, that agents (and pilots, and mass) deposit into and read.

One `Fields` object per arena (`Fields.of(arena)`), shared by every species living there, so a pack's threat
deposit is a grazer's danger signal with nothing wired between them. Channels are named; a species may own
private ones ("trail:<species>").

Every channel follows f <- decay * blur(f) + deposit. Blur is a SEPARABLE 3-tap kernel [a, 1-2a, a] per
axis (explicit diffusion; a <= 1/3 for stability), so one update is 3 passes of O(G^3). Decay applies to
SIGNALS only (scent fades) - never to mass. The food channel is not deposited by agents: it is rebuilt
from the arena's live prism volume every `food_every` steps, so it is a pure read of conserved mass.

Sampling is nearest-cell for values and a precomputed central-difference gradient for directions.
"""
from __future__ import annotations

import numpy as np

try:
    import numba as nb
    HAVE_NUMBA = True
except Exception:  # pragma: no cover
    HAVE_NUMBA = False


def _blur_axis_np(f, a, axis):
    g = f * (1.0 - 2.0 * a)
    s = [slice(None)] * 3
    lo = s.copy(); lo[axis] = slice(1, None)
    hi = s.copy(); hi[axis] = slice(None, -1)
    g[tuple(lo)] += a * f[tuple(hi)]
    g[tuple(hi)] += a * f[tuple(lo)]
    return g


if HAVE_NUMBA:
    @nb.njit(parallel=True, fastmath=True, cache=True)
    def _blur3_nb(f, a, decay):
        G = f.shape[0]
        g = np.empty_like(f)
        h = np.empty_like(f)
        c = 1.0 - 2.0 * a
        for x in nb.prange(G):
            for y in range(G):
                for z in range(G):
                    v = c * f[x, y, z]
                    if x > 0: v += a * f[x - 1, y, z]
                    if x < G - 1: v += a * f[x + 1, y, z]
                    g[x, y, z] = v
        for x in nb.prange(G):
            for y in range(G):
                for z in range(G):
                    v = c * g[x, y, z]
                    if y > 0: v += a * g[x, y - 1, z]
                    if y < G - 1: v += a * g[x, y + 1, z]
                    h[x, y, z] = v
        for x in nb.prange(G):
            for y in range(G):
                for z in range(G):
                    v = c * h[x, y, z]
                    if z > 0: v += a * h[x, y, z - 1]
                    if z < G - 1: v += a * h[x, y, z + 1]
                    g[x, y, z] = v * decay
        return g


class Fields:
    SHARED = ("food", "alarm", "threat")

    def __init__(self, R: float, G: int = 40, backend: str = "numpy"):
        self.R, self.G, self.backend = R, G, backend
        self.h = 2.0 * R / G
        self.ch: dict[str, np.ndarray] = {}
        self.grad: dict[str, np.ndarray] = {}
        self.cfg: dict[str, tuple] = {}        # name -> (blur a, decay per step)
        self.pending: dict[str, np.ndarray] = {}
        self.k = 0
        self.food_every = 10
        for name, a, d in (("food", 0.25, 1.0), ("alarm", 0.2, 0.85), ("threat", 0.2, 0.9)):
            self.add(name, a, d)

    @staticmethod
    def of(arena, G: int = 40, backend: str = "numpy") -> "Fields":
        f = getattr(arena, "fields", None)
        if f is None:
            f = Fields(arena.R, G, backend)
            arena.fields = f
        return f

    def add(self, name, a=0.2, decay=0.95):
        if name not in self.ch:
            self.ch[name] = np.zeros((self.G,) * 3, np.float32)
            self.grad[name] = np.zeros((self.G,) * 3 + (3,), np.float32)
            self.pending[name] = np.zeros(self.G ** 3, np.float32)
            self.cfg[name] = (a, decay)

    def cell(self, p):
        c = np.clip(((p + self.R) / self.h).astype(np.int64), 0, self.G - 1)
        return (c[..., 0] * self.G + c[..., 1]) * self.G + c[..., 2]

    def deposit(self, name, p, amount):
        if len(p) == 0:
            return
        self.pending[name] += np.bincount(self.cell(p), weights=np.broadcast_to(amount, (len(p),)),
                                          minlength=self.G ** 3).astype(np.float32)

    def sample(self, name, cells):
        return self.ch[name].reshape(-1)[cells]

    def sample_grad(self, name, cells):
        return self.grad[name].reshape(-1, 3)[cells]

    def update(self, arena):
        """Once per arena step (call it from exactly one place - the first species that steps)."""
        if getattr(self, "_t", None) == arena.t:
            return
        self._t = arena.t
        self.k += 1
        # food = a read of conserved live prism volume (rebuilt, not accumulated), then diffused
        if self.k % self.food_every == 1 or self.food_every == 1:
            live = arena.mass_alive & ~arena.mass_shielded
            src = np.bincount(self.cell(arena.mass_pos[live]), weights=arena.mass_vol[live],
                              minlength=self.G ** 3).reshape((self.G,) * 3).astype(np.float32)
            f = src
            for _ in range(4):
                f = self._blur(f, 0.25, 1.0)
            self.ch["food"] = f / max(float(f.max()), 1e-6)
            self._grad("food")
        # pilots deposit a wake into threat
        for p in arena.pilots:
            self.pending["threat"][self.cell(p.pos[None])[0]] += 1.0
        for name, (a, d) in self.cfg.items():
            if name == "food":
                continue
            f = self.ch[name] + self.pending[name].reshape((self.G,) * 3)
            self.pending[name][:] = 0
            self.ch[name] = self._blur(f, a, d)
            if self.k % 2 == 0:
                self._grad(name)

    def _blur(self, f, a, d):
        if self.backend == "numba" and HAVE_NUMBA:
            return _blur3_nb(np.ascontiguousarray(f, np.float32), np.float32(a), np.float32(d))
        for ax in range(3):
            f = _blur_axis_np(f, a, ax)
        return f * d

    def _grad(self, name):
        gx, gy, gz = np.gradient(self.ch[name])
        self.grad[name] = np.stack([gx, gy, gz], axis=-1).astype(np.float32)
