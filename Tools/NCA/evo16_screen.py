"""evo16: screen / ablate genome variants on the fast fitness (evo16_fit) over N seeds, 4 processes.

    python Tools/NCA/evo16_screen.py Tools/NCA/results/evo/genome.npy --seeds 8 \
        --variant base: --variant reg:sw_reg=0.5 --variant all:sw_reg=0.5,sw_wnd=0.5,sw_head=0.5

A variant is name:gene=value,...; `--ablate` adds one variant per ON behaviour switch with it turned off.
"""
import argparse
import json
import os
import sys
from evo16_pool import pinned_pool

import numpy as np

os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo16_model as e16  # noqa: E402


def _eval(args):
    import evo16_fit as ef
    g, s = args
    return ef.evaluate(g, (s,))[0]


def apply(base, spec):
    g = base.copy()
    for kv in filter(None, spec.split(",")):
        k, v = kv.split("="); g[e16.SLICES[k]] = float(v)
    return g


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("genome")
    ap.add_argument("--seeds", type=int, default=8)
    ap.add_argument("--seed0", type=int, default=300)
    ap.add_argument("--variant", action="append", default=[])
    ap.add_argument("--ablate", action="store_true")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    g0 = np.load(a.genome)
    base = e16.from_evo(g0) if len(g0) < e16.DIM else g0
    vs = [v.split(":", 1) for v in a.variant] or [["base", ""]]
    if a.ablate:
        vs = [["full", ""]] + [[f"no_{k}", f"{k}=-1"] for k in e16.SWITCHES if base[e16.SLICES[k]][0] > 0]
        offs = ",".join(f"{k}=-1" for k in e16.SWITCHES if base[e16.SLICES[k]][0] > 0)
        vs.append(["all_off_G2", offs])
    seeds = [a.seed0 + i for i in range(a.seeds)]
    jobs = [(apply(base, spec), s) for _, spec in vs for s in seeds]
    with pinned_pool(4) as pool:
        R = pool.map(_eval, jobs)
    out = {}
    for j, (name, spec) in enumerate(vs):
        rr = R[j * len(seeds):(j + 1) * len(seeds)]
        fails = {}
        for r in rr:
            for f in r["fails"]:
                fails[f] = fails.get(f, 0) + 1
        out[name] = dict(spec=spec, f=round(float(np.mean([r["f"] for r in rr])), 3), pass_rate=round(float(np.mean([r["p"] for r in rr])), 3),
                         heal=np.mean([r["heal"] for r in rr], 0).round(2).tolist(), fails=fails)
        print(f"{name:14s} f={out[name]['f']:.3f} pass={out[name]['pass_rate']:.3f} heal(m,s,c,t)={out[name]['heal']} fails={fails}", flush=True)
    if a.out:
        json.dump(dict(seeds=seeds, variants=out), open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
