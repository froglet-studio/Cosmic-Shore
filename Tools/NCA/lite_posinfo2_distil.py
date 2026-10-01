"""Distil posinfo2's 192-wide network into a smaller one (same inputs, same residual 2-layer shape).

Data: every network evaluation (input row, output row) of the TEACHER over grows of all four plans and a
fair-cull switch from each, seeds 1..S (not the hold seeds 7/23/41/101). Loss: MSE on the state outputs
plus a heavier MSE on tanh(velocity) (what actually moves a tadpole). The student is saved in posinfo2's
rule.pt format, so `lite_posinfo2:<path>?...` loads it; the hold is the acceptance test.

  python Tools/NCA/lite_posinfo2_distil.py --hidden 64 --out Tools/NCA/results/lite_posinfo2/student64.pt
"""
from __future__ import annotations

import argparse
import sys
import time

import torch
import torch.nn.functional as F

sys.path.insert(0, "Tools/NCA")
import lite_posinfo2_model as lm
import posinfo2_rule as p2
import swarm_eval as se
import swarm_nca as sn

RULE = "Tools/NCA/results/posinfo2/rule.pt"


@torch.no_grad()
def collect(seeds, steps=240):
    t = lm.load(RULE)
    X, Y = [], []
    base_mlp = t.mlp

    def rec(f):
        o = base_mlp(f)
        X.append(f.clone()); Y.append(o.clone())
        return o
    t.mlp = rec
    targets = sn.load_targets()
    std, _ = se.transitions()
    for s in seeds:
        for k in sn.KINDS:
            gen = sn.make_gen(1000 + s)
            sw = sn.seed_swarm([targets[k]], t.world, gen)
            for _ in range(steps):
                sw = t(sw, gen)
            for kk, e in std:
                if kk != k:
                    continue
                sub = sw.clone()
                if se.cull_to(sub, 0, e, gen):
                    for _ in range(steps):
                        sub = t(sub, gen)
        print(f"seed {s}: {sum(len(x) for x in X)} rows", flush=True)
    return torch.cat(X), torch.cat(Y), t


def train(X, Y, teacher, hidden, epochs=40, lr=2e-3):
    Fi = X.shape[1]
    w1 = torch.nn.Parameter(torch.empty(hidden, Fi)); torch.nn.init.xavier_uniform_(w1)
    b1 = torch.nn.Parameter(torch.zeros(hidden))
    w2 = torch.nn.Parameter(torch.empty(hidden, hidden)); torch.nn.init.xavier_uniform_(w2, gain=0.5)
    b2 = torch.nn.Parameter(torch.zeros(hidden))
    w3 = torch.nn.Parameter(torch.zeros(sn.C + 3, hidden)); b3 = torch.nn.Parameter(teacher.b3.data.clone())
    ps = [w1, b1, w2, b2, w3, b3]
    opt = torch.optim.Adam(ps, lr=lr)
    mu, sd = X.mean(0), X.std(0).clamp(min=1e-3)
    keep = torch.ones(sn.C + 3); keep[sn.DIE] = 0
    n = len(X)
    perm = torch.randperm(n, generator=torch.Generator().manual_seed(0))
    nv = n // 10
    va, tr = perm[:nv], perm[nv:]
    sched = torch.optim.lr_scheduler.CosineAnnealingLR(opt, epochs)

    def fwd(x):
        h = torch.relu(F.linear(x, w1, b1))
        h = torch.relu(F.linear(h, w2, b2)) + h
        return F.linear(h, w3, b3)

    def loss(i):
        o = fwd(X[i]); y = Y[i]
        ls = (((o[:, :sn.C] - y[:, :sn.C]) ** 2) * keep[:sn.C]).mean()
        lv = ((torch.tanh(o[:, sn.C:]) - torch.tanh(y[:, sn.C:])) ** 2).mean()
        return ls + 10 * lv, lv
    t0 = time.time()
    for ep in range(epochs):
        for i in tr[torch.randperm(len(tr))].split(512):
            l, _ = loss(i)
            opt.zero_grad(); l.backward(); opt.step()
        sched.step()
        if ep % 5 == 4 or ep == epochs - 1:
            with torch.no_grad():
                lval, lv = loss(va)
                yv = torch.tanh(Y[va][:, sn.C:]); vr = float(lv / yv.var())
            print(f"ep {ep + 1} val {float(lval):.5f} vel-mse {float(lv):.5f} (vel R^2 {1 - vr:.3f})  {time.time() - t0:.0f}s", flush=True)
    return {"w1": w1.data, "b1": b1.data, "w2": w2.data, "b2": b2.data, "w3": w3.data, "b3": b3.data}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hidden", type=int, default=64)
    ap.add_argument("--seeds", type=int, default=3)
    ap.add_argument("--epochs", type=int, default=40)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    torch.set_num_threads(int(__import__("os").environ.get("OMP_NUM_THREADS", 1)))
    torch.manual_seed(0)
    X, Y, t = collect(range(1, a.seeds + 1))
    W = train(X, Y, t, a.hidden, a.epochs)
    st = torch.load(RULE, weights_only=False)
    st["rule"] = W
    st["hidden"] = a.hidden
    st["distilled_from"] = RULE
    torch.save(st, a.out)
    print("saved", a.out)


if __name__ == "__main__":
    main()
