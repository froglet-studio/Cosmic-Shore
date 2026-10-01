"""Ablations of a sort model: which interaction matters.

    python Tools/NCA/sort_ablate.py results/sort/params.json [--seeds 3] [--out results/sort/ablations.json]

Each variant = the published cfg with a few fields changed. Scored with sort_search.all16 (every plan
grown once, the three switches branched from copies) on --seeds seeds; reported: own-plan losses
(mean over seeds), tests passed (mean), and the mean loss to the wanted plan over the switches.
"""
import argparse, dataclasses as dc, json, os, sys
from multiprocessing import Pool
import numpy as np, torch

HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import sort_model as sm  # noqa: E402
import sort_search as ss  # noqa: E402

NEIGH_OFF = dict(a_same=0.0, a_elem=0.0, a_role=0.0, a_other=0.0, swap=0.0)
VARIANTS = {
    "published": {},
    "no adhesion": dict(a_same=0.0, a_elem=0.0, a_role=0.0, a_other=0.0),
    "no swaps": dict(swap=0.0),
    "no neighbour sorting (adhesion+swaps off)": NEIGH_OFF,
    "no fate (climb the type's whole mixture)": dict(fate=0),
    "K=1 (one French flag per type)": dict(K=1),
    "K=2": dict(K=2), "K=4": dict(K=4), "K=8": dict(K=8),
    "local centre (consensus, no swarm average)": dict(local_centre=1),
    "laps (Time runners advance their fate)": dict(lap_every=6),
    "no molting (switches)": dict(molt=0),
    "molt without region transfer": dict(transfer=0),
    "no cross-laying": dict(p_cross=0.0),
}


def _job(args):
    name, cfgd, seed = args
    torch.set_num_threads(1)
    d = dict(cfgd); d["vmax"] = tuple(d["vmax"])
    m = sm.SortSwarm(sm.SortCfg(**d))
    res = ss.all16(m, seed)
    return name, seed, {k: [round(v[0], 2), bool(v[1])] for k, v in res.items()}


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("params"); ap.add_argument("--seeds", type=int, default=3)
    ap.add_argument("--out", default=""); ap.add_argument("--only", default="")
    a = ap.parse_args()
    base = json.load(open(a.params))["cfg"]
    names = [n for n in VARIANTS if not a.only or n in a.only.split(";")]
    jobs = []
    for n in names:
        d = dict(base); d.update(VARIANTS[n])
        for s in range(a.seeds):
            jobs.append((n, d, 3000 + s))
    with Pool(4) as pool:
        R = pool.map(_job, jobs)
    out = {}
    for n in names:
        rs = [r for nn, _, r in R if nn == n]
        own = {k: round(float(np.mean([r[k][0] for r in rs])), 2) for k in sn.KINDS}
        sw = [v[0] for r in rs for t, v in r.items() if "->" in t]
        passed = float(np.mean([sum(v[1] for v in r.values()) for r in rs]))
        feas = float(np.mean([len(r) for r in rs]))
        own_ok = float(np.mean([sum(r[k][1] for k in sn.KINDS) for r in rs]))
        out[n] = dict(own=own, own_passed=round(own_ok, 2), passed=round(passed, 2), feasible=round(feas, 2),
                      switch_mean_loss=round(float(np.mean(sw)), 2), runs=rs)
        print(f"{n:45s} own {own}  own_ok {own_ok:.2f}  passed {passed:.2f}/{feas:.1f}  switch-loss {np.mean(sw):.2f}", flush=True)
    if a.out:
        json.dump(out, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
