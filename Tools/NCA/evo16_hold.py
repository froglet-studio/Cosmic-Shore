"""evo16: the "hold" test - grow each plan and keep running it; is its own plan still strictly closest
(and how far has it drifted) at long horizons? The shared yardsticks stop at 240 steps.

    python Tools/NCA/evo16_hold.py results/evo16/genome.npy --seeds 8 --at 240,600,1200 --out results/evo16/hold.json
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
    path, seed, at = args
    import torch
    import evo16_model as e16
    import swarm_eval as se
    import swarm_nca as sn
    torch.set_num_threads(1)
    m = e16.Evo16Rule(np.load(path)); T = sn.load_targets(); L = sn.LossCfg()
    out = {}
    with torch.no_grad():
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k] for k in sn.KINDS], m.world, gen)
        for t in range(1, max(at) + 1):
            sw = m(sw, gen)
            if t in at:
                for b, k in enumerate(sn.KINDS):
                    row = se._score_row(sw, b, T, L); n = se._alive(sw, b)
                    out.setdefault(k, {})[t] = dict(own=row[k], closest=min(row, key=row.get), ok=se._passes(row, k, n), n=n)
    return path, seed, out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("genomes", nargs="+")
    ap.add_argument("--seeds", type=int, default=8)
    ap.add_argument("--seed0", type=int, default=8800)
    ap.add_argument("--at", default="240,600,1200")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    at = [int(x) for x in a.at.split(",")]
    with pinned_pool(4) as pool:
        R = pool.map(_one, [(p, s, at) for p in a.genomes for s in range(a.seed0, a.seed0 + a.seeds)])
    res = {}
    for p in a.genomes:
        rr = [o for q, _, o in R if q == p]
        res[p] = {k: {t: dict(hold=round(float(np.mean([o[k][t]["ok"] for o in rr])), 2),
                              own=round(float(np.mean([o[k][t]["own"] for o in rr])), 2),
                              wrong=sorted({o[k][t]["closest"] for o in rr if not o[k][t]["ok"]}))
                      for t in at} for k in rr[0]}
        print(p)
        for k, v in res[p].items():
            print("  ", k, "  ".join(f"t{t}: hold {x['hold']:.2f} own {x['own']:.1f} {x['wrong'] or ''}" for t, x in v.items()), flush=True)
    if a.out:
        json.dump(dict(at=at, results=res), open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
