#!/usr/bin/env python3
"""The EXACTNESS fixture for the shipped evofate core (SwarmEvoFateCore.cs): random swarm states, and what
the research's own evofate_model.EvoFate perceives and computes for every member of them.

    python3 Tools/Build/swarm_core_harness/evofate_fixture.py <out.json> [--nca <Tools/NCA>] [--states 6]

For each state (a random knot of 40-260 members: random elements, three domains, a share of eggs, random
32-channel state) it writes every active member's 232 perception features (EvoRule.perceive + the glob row
exactly as EvoFate._g2 builds them) and the network's 35 outputs (EvoRule.mlp). The C# harness
(`run.sh evofate <plans> <rule> <fixture>`) loads the same states into the core, recomputes both through
SwarmEvoFateCore.FeaturesOf and SwarmEvoRule.Forward, and reports the worst absolute and relative error. This
is the part of the port that CAN be exact (the step itself draws random numbers, so the step is proven by
distribution, score_evofate.py).
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from score_grid import research_dir  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--nca", default="")
    ap.add_argument("--states", type=int, default=6)
    a = ap.parse_args()
    nca = research_dir(a.nca)
    sys.path.insert(0, nca)
    import numpy as np
    import torch
    import torch.nn.functional as F
    torch.set_num_threads(1)
    import swarm_nca as sn
    import evofate_model as efm

    model = efm.load(os.path.join(nca, "results", "evofate", "params.json"))
    W = model.world
    rng = np.random.default_rng(20261001)
    states = []
    for k in range(a.states):
        N = W.capacity
        n = int(rng.integers(40, 261))
        pos = np.zeros((N, 3), np.float32); s = np.zeros((N, sn.C), np.float32)
        elem = np.zeros(N, np.int64); dom = np.zeros(N, np.int64)
        act = np.zeros(N, bool); hat = np.zeros(N, bool)
        r = 4.0 + 0.06 * n
        pos[:n] = (rng.normal(size=(n, 3)) * r / 2).astype(np.float32)
        s[:n] = (rng.normal(size=(n, sn.C)) * 0.8).astype(np.float32)
        elem[:n] = rng.integers(0, 4, n); dom[:n] = rng.integers(0, 3, n)
        act[:n] = True; hat[:n] = rng.random(n) < 0.85
        s[:n, sn.A] = np.where(hat[:n], 1.0, 0.2)
        sw = sn.Swarm(torch.tensor(pos)[None], torch.tensor(s)[None], torch.tensor(elem)[None], torch.tensor(dom)[None],
                      torch.tensor(act)[None], torch.tensor(hat)[None], torch.zeros(1, dtype=torch.long))
        with torch.no_grad():
            p, x_s = sw.pos.reshape(N, 3), sw.s.reshape(N, sn.C)
            e_, d_, h_ = sw.elem.reshape(N), sw.dom.reshape(N), sw.hatched.reshape(N)
            gi, gj = sn.edges(sw, W.R)
            x = torch.cat([x_s, F.one_hot(e_, 4).to(x_s.dtype), h_[:, None].to(x_s.dtype)], 1)
            hb = (sw.hatched & sw.active).float()
            cnt = hb.sum(1).clamp(min=1)
            mixe = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1) / cnt[:, None]
            glob = torch.cat([(cnt / 100)[:, None], mixe], 1).expand(N, model.G)
            feats = torch.cat([model.perceive(p, x, d_, gi, gj, N), glob], 1)
            out = model.mlp(feats)
        idx = np.nonzero(act)[0]
        states.append(dict(
            n=n, pos=pos[:n].tolist(), s=s[:n].tolist(), elem=elem[:n].tolist(), dom=dom[:n].tolist(),
            hatched=hat[:n].astype(int).tolist(),
            feats=feats.numpy()[idx].tolist(), out=out.numpy()[idx].tolist()))
        print(f"  state {k}: n={n}, edges={len(gi)}")
    with open(a.out, "w") as fh:
        json.dump(dict(R=W.R, rho0=W.rho0, F=int(model.F), states=states), fh)
    print(f"wrote {a.out}")


if __name__ == "__main__":
    main()
