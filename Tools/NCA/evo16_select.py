"""evo16: compare candidate genomes on the fast fitness (evo16_fit) over the same N fresh seeds.

    python Tools/NCA/evo16_select.py a.npy b.npy ... --seeds 12 --seed0 5000 --out runs/evo16/select.json

Heal on one strike is noisy (a random strike direction, one sample per plan), so a 2- or 4-seed check
inside the search swings by +-0.5; candidates are chosen here on more seeds, with the median and the
share of strikes that heal >= 0.5 reported beside the mean.
"""
import argparse
import json
import os
import sys

import numpy as np

from evo16_pool import pinned_pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def _eval(args):
    import evo16_fit as ef
    path, s = args
    return path, ef.evaluate(np.load(path), (s,))[0]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("genomes", nargs="+")
    ap.add_argument("--seeds", type=int, default=12)
    ap.add_argument("--seed0", type=int, default=5000)
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    seeds = [a.seed0 + i for i in range(a.seeds)]
    with pinned_pool(4) as pool:
        R = pool.map(_eval, [(p, s) for p in a.genomes for s in seeds])
    out = {}
    for p in a.genomes:
        rr = [r for q, r in R if q == p]
        h = np.array([r["heal"] for r in rr])
        fails = {}
        for r in rr:
            for f in r["fails"]:
                fails[f] = fails.get(f, 0) + 1
        out[p] = dict(f=round(float(np.mean([r["f"] for r in rr])), 3), pass_rate=round(float(np.mean([r["p"] for r in rr])), 4),
                      margin=round(float(np.mean([r["m"] for r in rr])), 3), heal_mean=h.mean(0).round(2).tolist(),
                      heal_median=np.median(h, 0).round(2).tolist(), heal_ge_half=(h >= 0.5).mean(0).round(2).tolist(), fails=fails)
        print(os.path.basename(p), json.dumps(out[p]), flush=True)
    if a.out:
        json.dump(dict(seeds=seeds, candidates=out), open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
