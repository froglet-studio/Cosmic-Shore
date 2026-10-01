"""Score a sort model on the shared yardstick (swarm_eval.evaluate, unchanged) without editing swarm_eval.

    python Tools/NCA/sort_eval.py results/sort/params.json [--full] [--seed 7] [--set K=8,molt=0] [--out f.json]
"""
import argparse, dataclasses as dc, json
import torch
import swarm_eval as se
import sort_model as sm


def make(params="", sets=""):
    m = sm.load(params) if params else sm.SortSwarm()
    if sets:
        d = dc.asdict(m.cfg)
        for kv in sets.split(","):
            k, v = kv.split("=")
            d[k] = type(d[k])(float(v))
        d["vmax"] = tuple(d["vmax"])
        m = sm.SortSwarm(sm.SortCfg(**d))
    return m


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("params", nargs="?", default="")
    ap.add_argument("--full", action="store_true"); ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--samples", type=int, default=3); ap.add_argument("--set", default=""); ap.add_argument("--out", default="")
    a = ap.parse_args(); torch.set_num_threads(4)
    m = make(a.params, a.set)
    res = se.evaluate(m, seed=a.seed, samples=a.samples, full=a.full)
    print(se.matrix(res))
    print(f"PASSED {res['passed']}/{res['feasible']} feasible (n/a {res['na']}); own losses " +
          " / ".join(f"{res['own'][k]['cross'][k]:.2f}" for k in res['own']) + f"; {res['seconds']}s")
    for k, v in res["switch"].items():
        print(f"  {k:16s} rate {v['rate']:.2f} wanted {v['cross'][k.split('->')[1]]:.2f} cross {v['cross']} n {v['n']}")
    res["cfg"] = dc.asdict(m.cfg)
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)
