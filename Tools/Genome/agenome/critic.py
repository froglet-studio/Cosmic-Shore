"""Learned critic: a small CNN (JAX, CPU) that tells Amoebius frames from phenotype frames.

It is retrained during the search on (Amoebius train frames = real) vs (frames the population just rendered = fake),
so it keeps finding whatever still gives the phenotypes away: the adversarial half of the fitness. The fixed
statistics in features.py are the stable half. Held-out Amoebius frames (reel05) must stay 'real' to the critic, or
the critic is overfitting the training reels; that number is logged.

Input frames: uint8 [N,144,256,3] -> 2x2 average -> [N,72,128,3].
"""
import math
import os
import time

import numpy as np

_JAX = None


def _jax():
    global _JAX
    if _JAX is None:
        os.environ.setdefault("XLA_FLAGS", "--xla_cpu_multi_thread_eigen=false intra_op_parallelism_threads=1")
        import jax
        import jax.numpy as jnp
        _JAX = (jax, jnp)
    return _JAX


CH = (3, 16, 32, 48, 64)


def init_params(seed=0):
    rng = np.random.default_rng(seed)
    p = {}
    for i in range(4):
        cin, cout = CH[i], CH[i + 1]
        k = 3
        p[f"w{i}"] = (rng.normal(size=(k, k, cin, cout)) * math.sqrt(2.0 / (k * k * cin))).astype(np.float32)
        p[f"b{i}"] = np.zeros(cout, np.float32)
    p["wo"] = (rng.normal(size=(CH[-1] * 2, 1)) * 0.05).astype(np.float32)
    p["bo"] = np.zeros(1, np.float32)
    return p


def prep(frames):
    x = frames.astype(np.float32) / 255.0
    n, h, w, c = x.shape
    x = x.reshape(n, h // 2, 2, w // 2, 2, c).mean(axis=(2, 4))
    return x


def _forward(p, x, jnp, jax):
    h = x * 2.0 - 1.0
    for i in range(4):
        h = jax.lax.conv_general_dilated(h, p[f"w{i}"], (2, 2), "SAME", dimension_numbers=("NHWC", "HWIO", "NHWC"))
        h = h + p[f"b{i}"]
        h = jax.nn.leaky_relu(h, 0.2)
    m = h.mean(axis=(1, 2))
    s = jnp.sqrt(((h - m[:, None, None, :]) ** 2).mean(axis=(1, 2)) + 1e-6)
    z = jnp.concatenate([m, s], axis=1)
    return (z @ p["wo"] + p["bo"])[:, 0]


class Critic:
    def __init__(self, params=None, seed=0):
        jax, jnp = _jax()
        self.p = {k: jnp.asarray(v) for k, v in (params or init_params(seed)).items()}
        self._fwd = jax.jit(lambda p, x: _forward(p, x, jnp, jax))

        def loss(p, x, y, wd):
            lg = _forward(p, x, jnp, jax)
            l = jnp.mean(jax.nn.softplus(-lg) * y + jax.nn.softplus(lg) * (1 - y))
            reg = sum(jnp.sum(v ** 2) for k, v in p.items() if k.startswith("w"))
            return l + wd * reg + 1e-3 * jnp.mean(lg ** 2)

        self._grad = jax.jit(jax.value_and_grad(loss))
        self.m = {k: jnp.zeros_like(v) for k, v in self.p.items()}
        self.v = {k: jnp.zeros_like(v) for k, v in self.p.items()}
        self.t = 0

    def logits(self, frames, batch=32):
        x = prep(frames)
        out = []
        for i in range(0, len(x), batch):
            out.append(np.asarray(self._fwd(self.p, x[i:i + batch])))
        return np.concatenate(out) if out else np.zeros(0)

    def train(self, real, fake, steps=200, batch=48, lr=1e-3, wd=1e-4, rng=None, shift=6):
        jax, jnp = _jax()
        rng = rng or np.random.default_rng(0)
        xr, xf = prep(real), prep(fake)
        losses = []
        for _ in range(steps):
            ir = rng.integers(0, len(xr), batch // 2)
            jf = rng.integers(0, len(xf), batch // 2)
            x = np.concatenate([xr[ir], xf[jf]])
            if shift:
                dx, dy = rng.integers(-shift, shift + 1, 2)
                x = np.roll(x, (int(dy), int(dx)), axis=(1, 2))
            x = x * rng.uniform(0.9, 1.1)
            y = np.concatenate([np.ones(batch // 2), np.zeros(batch // 2)]).astype(np.float32)
            l, g = self._grad(self.p, jnp.asarray(x, jnp.float32), jnp.asarray(y), wd)
            self.t += 1
            b1, b2 = 0.9, 0.999
            for k in self.p:
                self.m[k] = b1 * self.m[k] + (1 - b1) * g[k]
                self.v[k] = b2 * self.v[k] + (1 - b2) * g[k] ** 2
                mh = self.m[k] / (1 - b1 ** self.t)
                vh = self.v[k] / (1 - b2 ** self.t)
                self.p[k] = self.p[k] - lr * mh / (jnp.sqrt(vh) + 1e-8)
            losses.append(float(l))
        return float(np.mean(losses[-20:]))

    def save(self, path):
        tmp = path + ".tmp.npz"
        np.savez(tmp, **{k: np.asarray(v) for k, v in self.p.items()})
        os.replace(tmp, path)

    @classmethod
    def load(cls, path):
        z = np.load(path)
        return cls({k: z[k] for k in z.files})


def critic_distance(logit_mean):
    """Map the mean logit to a distance-like term: ~0 when the critic believes it, grows when it does not."""
    return float(np.log1p(np.exp(-logit_mean)))


class CriticWatcher:
    """Worker-side: reload the critic whenever its file changes."""

    def __init__(self, path):
        self.path = path
        self.mtime = None
        self.c = None

    def get(self):
        if not self.path or not os.path.exists(self.path):
            return None
        m = os.path.getmtime(self.path)
        if m != self.mtime:
            try:
                self.c = Critic.load(self.path)
                self.mtime = m
            except Exception:
                pass
        return self.c
