"""Probes for hybrid3d rules: put a multi-form rule in states it was never trained on and record
what it does. Every probe writes a contact sheet (png), a genome/size trace (json) and a sparse
voxel recording (npz) the lab viewer plays back.

    python3 Tools/NCA/hybrid_probe.py --model /mnt/project-files/hybrid3d/h1/model.pt --out runs/probe_h1
    python3 Tools/NCA/hybrid_probe.py --model ... --only graft_front,twins

Probes (lizard L, whale W, jelly J; codes are genome rows):
  pure_*       control: one seed of each animal
  seed_WJ_t    one seed whose genome is (1-t) whale + t jelly, t = .25 .5 .75 (also L/W, L/J at .5)
  infect_WJ    a grown whale whose head half has its GENOME rewritten to jelly (tissue untouched)
  splice_WJ    head half of a grown whale + tail half of a grown jelly (whole state spliced)
  twins_WJ     a whale seed and a jelly seed 14 voxels apart, growing into each other
  split_*      a grown animal cut through the middle by a 3-voxel slab, halves pulled 6 voxels apart
  wound_WJ     the splice, then a ball cut out across the seam every 400 steps
"""
from __future__ import annotations

import argparse
import json
import os
import sys

import numpy as np
import torch
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from nca3d import CA3D, render  # noqa: E402
from hybrid3d import GENOME, FORMS  # noqa: E402

BIG = (24, 56, 72)                 # probe arena (the rule is translation invariant)
C0 = (12, 28, 36)
L_, W_, J_ = np.eye(3)


def seed_at(x, pos, code):
    x[0, pos[0], pos[1], pos[2], 3:] = 1.0
    x[0, pos[0], pos[1], pos[2], list(GENOME)] = torch.as_tensor(code, dtype=torch.float32)
    return x


def empty():
    return torch.zeros((1,) + BIG + (16,))


@torch.no_grad()
def run(ca, x, n, rec=None, every=4, hook=None, t0=0):
    for i in range(1, n + 1):
        x = ca(x)
        if hook:
            x = hook(t0 + i, x)
        if rec is not None and (t0 + i) % every == 0:
            rec.append(snapshot(x, t0 + i))
    return x


def snapshot(x, t):
    s = x[0]
    a = s[..., 3]
    m = a > 0.1
    idx = m.nonzero()
    v = s[m]
    rgb = (v[:, :3] / v[:, 3:4].clamp(min=1e-3)).clamp(0, 1)
    g = v[:, list(GENOME)]
    return {"t": t, "idx": idx.numpy().astype(np.uint8), "rgba": torch.cat([rgb, v[:, 3:4].clamp(0, 1)], 1).mul(255).byte().numpy(),
            "g": g.clamp(-0.5, 1.5).add(0.5).mul(127).byte().numpy()}


def readout(x):
    m = (x[0, ..., 3] > 0.1)
    n = int(m.sum())
    g = x[0][m][:, list(GENOME)].mean(0).tolist() if n else [0, 0, 0]
    # fraction of cells whose strongest genome is each form
    lab = x[0][m][:, list(GENOME)].argmax(1) if n else torch.zeros(0, dtype=torch.long)
    frac = [float((lab == k).float().mean()) if n else 0 for k in range(3)]
    return {"n": n, "genome": [round(v, 3) for v in g], "frac": [round(v, 3) for v in frac]}


def half_mask(x, axis=3, keep_low=True, at=None):
    m = (x[0, ..., 3] > 0.1).nonzero().float()
    c = at if at is not None else float(m[:, axis - 1].mean())
    ar = torch.arange(x.shape[axis]).float()
    sel = (ar < c) if keep_low else (ar >= c)
    shape = [1, 1, 1, 1, 1]; shape[axis] = -1
    return sel.view(shape).float(), c


def shift(x, d, axis):
    return torch.roll(x, d, axis)


def grow(ca, code, n=400, pos=C0, seed=1):
    torch.manual_seed(seed)
    return run(ca, seed_at(empty(), pos, code), n)


def probes(ca):
    P = {}
    for k, c in zip(FORMS, (L_, W_, J_)):
        P[f"pure_{k}"] = lambda c=c: (seed_at(empty(), C0, c), None)
    for t in (0.25, 0.5, 0.75):
        P[f"seed_WJ_{t}"] = lambda t=t: (seed_at(empty(), C0, (1 - t) * W_ + t * J_), None)
    P["seed_LW_0.5"] = lambda: (seed_at(empty(), C0, 0.5 * L_ + 0.5 * W_), None)
    P["seed_LJ_0.5"] = lambda: (seed_at(empty(), C0, 0.5 * L_ + 0.5 * J_), None)

    def infect():
        x = grow(ca, W_)
        m, c = half_mask(x, 3, keep_low=False)               # head half = high W? either half: a coin flip, documented
        g = x[..., list(GENOME)]
        alive = (x[..., 3:4] > 0.1).float()
        newg = g * (1 - m) + torch.as_tensor(J_, dtype=torch.float32) * alive * m
        x[..., list(GENOME)] = newg
        return x, None
    P["infect_WJ"] = infect

    def splice(a=W_, b=J_):
        xa, xb = grow(ca, a), grow(ca, b, seed=2)
        ma, _ = half_mask(xa, 3, True, at=C0[2])
        return xa * ma + xb * (1 - ma), None
    P["splice_WJ"] = splice
    P["splice_LJ"] = lambda: splice(L_, J_)
    P["splice_LW"] = lambda: splice(L_, W_)

    def twins(a=W_, b=J_, gap=14):
        x = empty()
        seed_at(x, (C0[0], C0[1], C0[2] - gap // 2), a)
        seed_at(x, (C0[0], C0[1], C0[2] + gap // 2), b)
        return x, None
    P["twins_WJ"] = twins
    P["twins_LL"] = lambda: twins(L_, L_, 18)

    def split(code):
        x = grow(ca, code)
        lo, c = half_mask(x, 3, True)
        c = int(round(c))
        ar = torch.arange(BIG[2]).view(1, 1, 1, -1, 1)
        left = x * (ar < c - 1).float()
        right = x * (ar > c + 1).float()
        return shift(left, -3, 3) + shift(right, 3, 3), None
    for k, c in zip(FORMS, (L_, W_, J_)):
        P[f"split_{k}"] = lambda c=c: split(c)

    def wound():
        x, _ = splice()
        zz, yy, xx = torch.meshgrid(*[torch.arange(n).float() for n in BIG], indexing="ij")
        ball = ((zz - C0[0]) ** 2 + (yy - C0[1]) ** 2 + (xx - C0[2]) ** 2 < 36).float()[None, ..., None]

        def hook(t, x):
            return x * (1 - ball) if t % 400 == 0 else x
        return x, hook
    P["wound_WJ"] = wound
    return P


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--steps", type=int, default=2000)
    ap.add_argument("--only", default="")
    ap.add_argument("--every", type=int, default=4)
    ap.add_argument("--threads", type=int, default=1)
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    os.makedirs(a.out, exist_ok=True)
    ca = CA3D(16, 128)
    ca.load_state_dict(torch.load(a.model, map_location="cpu"))
    P = probes(ca)
    names = [n for n in P if not a.only or n in a.only.split(",")]
    sheet_t = [0, 100, 250, 500, 1000, 1500, 2000, 3000, 4000]
    sheet_t = [t for t in sheet_t if t <= a.steps]
    summary = {}
    for name in names:
        x, hook = P[name]()
        torch.manual_seed(7)
        rec, trace, ims = [snapshot(x, 0)], [], [render(x[0], px=128)]
        t = 0
        for t1 in sheet_t[1:]:
            while t < t1:
                n = min(50, t1 - t)
                x = run(ca, x, n, rec, a.every, hook, t0=t)
                t += n
                trace.append({"t": t, **readout(x)})
            ims.append(render(x[0], px=128))
        Image.fromarray((np.concatenate(ims, 1) * 255).astype(np.uint8)).save(os.path.join(a.out, f"{name}.png"))
        np.savez_compressed(os.path.join(a.out, f"{name}.npz"),
                            t=np.array([r["t"] for r in rec]), n=np.array([len(r["idx"]) for r in rec]),
                            idx=np.concatenate([r["idx"] for r in rec]), rgba=np.concatenate([r["rgba"] for r in rec]),
                            g=np.concatenate([r["g"] for r in rec]))
        summary[name] = trace
        last = trace[-1]
        print(f"{name:14s} n={last['n']:5d} genome={last['genome']} frac={last['frac']}", flush=True)
        json.dump(summary, open(os.path.join(a.out, "trace.json"), "w"))
    rows = [np.asarray(Image.open(os.path.join(a.out, f"{n}.png"))) for n in names]
    wmax = max(r.shape[1] for r in rows)
    rows = [np.pad(r, ((0, 0), (0, wmax - r.shape[1]), (0, 0)), constant_values=255) for r in rows]
    Image.fromarray(np.concatenate(rows, 0)).save(os.path.join(a.out, "sheet.png"))


if __name__ == "__main__":
    main()
