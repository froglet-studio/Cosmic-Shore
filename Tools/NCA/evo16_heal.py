"""evo16: a lower-noise heal yardstick - K vessel strikes (different directions) per plan per seed.

swarm_probe strikes each plan once; one strike direction makes heal swing +-0.5 from seed to seed. Here
each grown plan is cloned K times, each clone struck along its own random direction (swarm_probe's
geometry: the RMS sphere one radius off the centroid), all run --regrow steps (default 120, the probe's) as one batch with the struck
swarm's behaviour state kept (as evo16_fit does), and heal = (cut - recovered) / (cut - before) per
strike, clipped to [-1, 1] with evo_model.fast_probe's rule for a near-zero cut.

    python Tools/NCA/evo16_heal.py a.npy b.npy --seeds 8 --strikes 4 --out runs/evo16/heal.json
"""
import argparse
import json
import os
import sys

import numpy as np

from evo16_pool import pinned_pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def _one(args):
    path, seed, K, regrow, nostrike = args
    import torch
    import evo_model as em
    import evo16_model as e16
    import swarm_nca as sn
    torch.set_num_threads(1)
    model = e16.Evo16Rule(np.load(path))
    T = sn.load_targets(); L = sn.LossCfg()
    with torch.no_grad():
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k] for k in sn.KINDS], model.world, gen)
        for _ in range(240):
            sw = model(sw, gen)
        subs, kinds, hw, ema, lock, before = [], [], [], [], [], []
        for b, k in enumerate(sn.KINDS):
            b0 = sn.swarm_loss(sn.decode(sw, b), T[k], L)[1]["sink"]
            for _ in range(K):
                sub = sw.index(torch.tensor([b])).clone()
                if not nostrike:
                    em._strike_b(sub, 0, gen)
                subs.append(sub); kinds.append(k); before.append(b0)
                hw.append(float(model.hw[b])); ema.append(model.ema[b:b + 1].clone()); lock.append(int(model.locked[b]))
        bs = sn.Swarm.cat(subs)
        cut = [sn.swarm_loss(sn.decode(bs, i), T[k], L)[1]["sink"] for i, k in enumerate(kinds)]
        model.hw = torch.tensor(hw); model.ema = torch.cat(ema); model.locked = torch.tensor(lock, dtype=torch.long); model._tok = bs.pos
        g2 = sn.make_gen(seed + 1)
        for _ in range(regrow):
            bs = model(bs, g2)
        out = {k: [] for k in sn.KINDS}
        for i, k in enumerate(kinds):
            r = sn.swarm_loss(sn.decode(bs, i), T[k], L)[1]["sink"]
            span = cut[i] - before[i]
            h = max(-1.0, min(1.0, (cut[i] - r) / span)) if span > 0.3 else (1.0 if r <= cut[i] + 0.3 else -1.0)
            out[k].append(dict(heal=h, before=before[i], cut=cut[i], rec=r))
    return path, seed, out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("genomes", nargs="+")
    ap.add_argument("--seeds", type=int, default=8)
    ap.add_argument("--seed0", type=int, default=7700)
    ap.add_argument("--strikes", type=int, default=4)
    ap.add_argument("--regrow", type=int, default=120)
    ap.add_argument("--nostrike", action="store_true", help="control: same protocol without the strike (drift of an intact swarm)")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    seeds = [a.seed0 + i for i in range(a.seeds)]
    with pinned_pool(4) as pool:
        R = pool.map(_one, [(p, s, a.strikes, a.regrow, a.nostrike) for p in a.genomes for s in seeds])
    res = {}
    for p in a.genomes:
        per = {}
        for q, s, out in R:
            if q != p:
                continue
            for k, lst in out.items():
                per.setdefault(k, []).extend(lst)
        summ = {k: dict(heal_mean=round(float(np.mean([x["heal"] for x in v])), 3),
                        heal_ge_half=round(float(np.mean([x["heal"] >= 0.5 for x in v])), 2),
                        before=round(float(np.mean([x["before"] for x in v])), 2), cut=round(float(np.mean([x["cut"] for x in v])), 2),
                        rec=round(float(np.mean([x["rec"] for x in v])), 2), n=len(v)) for k, v in per.items()}
        res[p] = summ
        print(os.path.basename(os.path.dirname(p)) + "/" + os.path.basename(p), json.dumps(summ), flush=True)
    if a.out:
        json.dump(dict(seeds=seeds, strikes=a.strikes, regrow=a.regrow, nostrike=a.nostrike, results=res), open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
