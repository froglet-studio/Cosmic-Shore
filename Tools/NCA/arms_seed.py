"""Generation 0 of the arms race: the two policies behaviour-cloned from the SIMPLEST hand-written heuristics.

Why a designed seed (runs a1 and a2, results/arms/a*_log.jsonl): from random weights, evolution strategies taught the
prey to evade within ~40 generations but never taught the predators to PURSUE. A one-line scripted pursuer catches
54 prey/min from a2's generation-170 prey, while the learned predators caught 3.8/min. The co-evolution
disengaged twice, with and without PFSP. So generation 0 is designed, and everything after it is learned. The
cross-generation matrix then asks the useful question directly: does the arms race improve on the hand design, and
what does it add?

  scripted predator  accelerate at the nearest perceived prey, led by 0.3 s of its relative velocity; burst when it
                     is within 40 u. No packmate input is used: any coordination later is LEARNED.
  scripted prey      accelerate straight away from the nearest perceived predator; otherwise climb the food gradient.
                     No neighbour input is used: any schooling later is LEARNED.
The signal output is cloned to 0 (unused) and the network sees all its usual inputs, so evolution can start using
them.

    python Tools/NCA/arms_seed.py --out runs/arms_seed.npz
"""
from __future__ import annotations

import argparse
import os

import numpy as np

import arms_sim as A


def scripted(cfg, xq, xp, noise=0.0, rng=None):
    """Heuristic actions from the policies' own observation vectors (see arms_sim.observe for the layout)."""
    nearp = xq[..., 21:24]
    has = np.abs(nearp).sum(-1) > 0
    flee = -nearp / (np.linalg.norm(nearp, axis=-1, keepdims=True) + 1e-6)
    food = np.clip(xq[..., 6:9] * 5, -1, 1)
    oq = np.zeros(xq.shape[:-1] + (A.PREY_OUT,), np.float32)
    oq[..., :3] = np.where(has[..., None], flee, food)
    near = xp[..., 29:32] * cfg.pred_R
    rv = xp[..., 32:35] * cfg.prey_v
    tgt = near + rv * 0.3
    op = np.zeros(xp.shape[:-1] + (A.PRED_OUT,), np.float32)
    seen = np.abs(near).sum(-1) > 0
    op[..., :3] = np.where(seen[..., None], tgt / (np.linalg.norm(tgt, axis=-1, keepdims=True) + 1e-6), [0.3, 0, 0])
    op[..., 4] = np.where(seen & (np.linalg.norm(near, axis=-1) < 40), 1, -1)
    if noise and rng is not None:                     # exploration while collecting (DAgger-lite coverage)
        oq[..., :3] += rng.normal(0, noise, oq[..., :3].shape)
        op[..., :3] += rng.normal(0, noise, op[..., :3].shape)
    return np.clip(oq, -1, 1), np.clip(op, -1, 1)


def collect(cfg, B=16, steps=300, seeds=(1, 2, 3, 4), noise=0.4):
    X = [[], []]; Y = [[], []]
    rng = np.random.default_rng(0)
    for s in seeds:
        st = A.reset(cfg, B, np.arange(B) + 1000 * s)
        for _ in range(steps):
            xq, xp, aux = A.observe(cfg, st)
            oq, op = scripted(cfg, xq, xp)
            aq, apr = scripted(cfg, xq, xp, noise, rng)
            X[0].append(xq[st.alive[0]]); Y[0].append(oq[st.alive[0]])
            X[1].append(xp[st.alive[1]]); Y[1].append(op[st.alive[1]])
            A.apply(cfg, st, aq, apr, aux, rng=rng)
    return [np.concatenate(x) for x in X], [np.concatenate(y) for y in Y]


def fit(X, Y, d_in, d_out, iters=4000, bs=4096, lr=3e-3, seed=0):
    """MSE regression of the tanh MLP (arms_sim.mlp layout) with hand-written backprop and Adam."""
    rng = np.random.default_rng(seed)
    th = A.init_params(rng, d_in, d_out).astype(np.float64)
    h = A.HID
    sizes = [d_in * h, h, h * h, h, h * d_out, d_out]
    m = np.zeros_like(th); v = np.zeros_like(th)
    Yc = np.clip(Y, -0.95, 0.95)
    for it in range(iters):
        idx = rng.integers(0, len(X), bs)
        x, y = X[idx].astype(np.float64), Yc[idx]
        o = np.cumsum([0] + sizes)
        W1 = th[o[0]:o[1]].reshape(d_in, h); b1 = th[o[1]:o[2]]
        W2 = th[o[2]:o[3]].reshape(h, h); b2 = th[o[3]:o[4]]
        W3 = th[o[4]:o[5]].reshape(h, d_out); b3 = th[o[5]:o[6]]
        z1 = np.tanh(x @ W1 + b1); z2 = np.tanh(z1 @ W2 + b2); out = np.tanh(z2 @ W3 + b3)
        g = 2 * (out - y) / bs * (1 - out ** 2)
        gW3 = z2.T @ g; gb3 = g.sum(0)
        g2 = g @ W3.T * (1 - z2 ** 2)
        gW2 = z1.T @ g2; gb2 = g2.sum(0)
        g1 = g2 @ W2.T * (1 - z1 ** 2)
        gW1 = x.T @ g1; gb1 = g1.sum(0)
        grad = np.concatenate([gW1.ravel(), gb1, gW2.ravel(), gb2, gW3.ravel(), gb3])
        m = 0.9 * m + 0.1 * grad; v = 0.999 * v + 0.001 * grad ** 2
        th -= lr * (m / (1 - 0.9 ** (it + 1))) / (np.sqrt(v / (1 - 0.999 ** (it + 1))) + 1e-8)
        if it % 1000 == 0 or it == iters - 1:
            print(f"  it {it} mse {float(((out - y) ** 2).mean()):.4f}", flush=True)
    return th.astype(np.float32)


def play(cfg, thq=None, thp=None, B=8, steps=300):
    """catches per minute per encounter; None = the scripted heuristic for that side."""
    st = A.reset(cfg, B, np.arange(B) + 50)
    rng = np.random.default_rng(0)
    for _ in range(steps):
        xq, xp, aux = A.observe(cfg, st)
        oq, op = scripted(cfg, xq, xp)
        if thq is not None:
            oq = A.mlp(np.repeat(thq[None], B, 0), xq, A.PREY_IN, A.PREY_OUT)
        if thp is not None:
            op = A.mlp(np.repeat(thp[None], B, 0), xp, A.PRED_IN, A.PRED_OUT)
        A.apply(cfg, st, oq, op, aux, rng=rng)
    return round(float(st.catches.sum(1).mean() / (steps * cfg.dt / 60)), 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "runs", "arms_seed.npz"))
    a = ap.parse_args()
    cfg = A.Cfg()
    X, Y = collect(cfg)
    print("samples prey", len(X[0]), "pred", len(X[1]))
    thq = fit(X[0], Y[0], A.PREY_IN, A.PREY_OUT)
    thp = fit(X[1], Y[1], A.PRED_IN, A.PRED_OUT)
    res = dict(scripted_v_scripted=play(cfg), cloned_v_cloned=play(cfg, thq, thp),
               cloned_pred_v_scripted_prey=play(cfg, None, thp), scripted_pred_v_cloned_prey=play(cfg, thq, None))
    print(res)
    np.savez(a.out, thq=thq, thp=thp, **{k: v for k, v in res.items()})


if __name__ == "__main__":
    main()
