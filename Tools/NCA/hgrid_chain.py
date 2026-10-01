"""HGRID chain demo: one swarm, eaten four times, becomes four creatures in turn.

Seeded as a whale (Mass majority). Every `every` steps a predator eats enough of the current majority
element that the swarm's next element takes over (whale -> jellyfish -> pufferfish -> dragonfly ->
whale ...), exactly the cull swarm_nca.rollout uses. After each phase the swarm is scored against all
four plans. Also measures what a player would see: per-element speed (voxels / step) against the
plan's own animation speed, births, crystals (deaths) and headcount over time.

    python Tools/NCA/hgrid_chain.py [oracle|hybrid|learned] [--ckpt ...] [--out results/hgrid/chain.json]
"""
from __future__ import annotations

import argparse
import json

import numpy as np
import torch

import swarm_nca as sn
import hgrid_boid as hb

ORDER = ["mass", "space", "charge", "time", "mass"]


@torch.no_grad()
def chain(model, every=240, seed=3, L=None, record=5):
    L = L or sn.LossCfg()
    targets = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([targets["mass"]], model.world, gen)
    phases, frames, ns = [], [], []
    speed_acc = np.zeros(4); speed_n = np.zeros(4)
    t = 0
    for i, want in enumerate(ORDER):
        if i > 0:
            new = sn.lose_majority(sw, 0, gen, to=sn.MAJOR[want])
            if new is None:
                phases.append(dict(want=want, skipped=True)); continue
        for k in range(every):
            live0 = sw.active[0] & sw.hatched[0]
            p0 = sw.pos[0].clone()
            sw = model(sw, gen)
            live1 = sw.active[0] & sw.hatched[0]
            both = live0 & live1
            if k > every // 2:
                d = (sw.pos[0] - p0).norm(dim=-1)
                for e in range(4):
                    m = both & (sw.elem[0] == e)
                    if m.any():
                        speed_acc[e] += float(d[m].mean()); speed_n[e] += 1
            if t % record == 0:
                x = sn.decode(sw, 0)
                vis = x["hatched"]
                tier = x["tier"].argmax(1)
                u = torch.cat([x["p"], x["elem"][:, None].float(), x["dom"][:, None].float(), x["h"],
                               tier[:, None].float(), x["f"], x["sp"], x["w"][:, None]], 1)[vis]
                frames.append(u.numpy()); ns.append(int(vis.sum()))
            t += 1
        x = sn.decode(sw, 0)
        row = {k2: round(sn.swarm_loss(x, targets[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        c = sn.census(sw, 0, targets[want])
        phases.append(dict(want=want, row=row, best=min(row, key=row.get), ok=min(row, key=row.get) == want,
                           n=c["n"], el=c["elements"], crystals=c["deaths"]))
        print(f"phase {i} want {want:7s} best {phases[-1]['best']:7s} " + " ".join(f"{k2}:{v:6.2f}" for k2, v in row.items())
              + f"  n={c['n']} el={c['elements']} crystals={c['deaths']}", flush=True)
    sp = (speed_acc / np.maximum(speed_n, 1)).round(3).tolist()
    nmax = max(ns + [1])
    arr = np.zeros((len(frames), nmax, 15), np.float32)
    for i, f in enumerate(frames):
        arr[i, :len(f)] = f
    return dict(phases=phases, speed_per_step=sp, ok=sum(p.get("ok", False) for p in phases)), dict(frames=arr, n=ns, crystals=[], switched_at=None)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", nargs="?", default="oracle")
    ap.add_argument("--ckpt", default="")
    ap.add_argument("--out", default="")
    ap.add_argument("--threads", type=int, default=1)
    ap.add_argument("--set", action="append")
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    import hgrid_eval
    cfg = hgrid_eval.parse_sets(hb.BoidCfg(), a.set)
    if a.kind == "oracle":
        model = hb.make_oracle(cfg)
    elif a.kind == "hybrid":
        import hgrid_hybrid
        model = hgrid_hybrid.HybridRule(sn.load_rule("results/swarm_coevo_g2/rule.pt"), cfg)
    else:
        import hgrid_nca
        model = hgrid_nca.load_model(a.ckpt, cfg)
    res, data = chain(model)
    print(json.dumps(res))
    if a.out:
        json.dump(dict(result=res, rollout=sn.pack({"chain": data}, 240 * len(ORDER))), open(a.out, "w"))


if __name__ == "__main__":
    main()
