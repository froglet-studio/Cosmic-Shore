"""Compare swarm models on the same held-out seeds: strict tests passed, probe heal, liveliness.

    python Tools/NCA/evo_compare.py base:- g2:results/evo/genome.npy compact:results/evo/compact/genome.npy
"""
import json
import os
import sys
from multiprocessing import Pool

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo_model as em  # noqa: E402
import swarm_nca as sn  # noqa: E402


def make(kind, path):
    if kind == "base":
        return sn.load_rule(os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt"))
    if kind == "compact":
        import evo_compact as ec
        return ec.CompactRule(np.load(path))
    return em.EvoRule(np.load(path))


def job(args):
    kind, path, seed = args
    torch.set_num_threads(1)
    m = make(kind, path)
    if kind == "base":
        m.eval()
    s = em.fast_rollout(m, seed, probe=True)
    p, _ = sn.tests_passed(s)
    return dict(passed=p, heal=float(np.mean(s["probe"]["heal"])), live=float(np.mean(s["live"])),
                live_time=float(s["live"][3]))


def main():
    specs = [a.split(":", 1) for a in sys.argv[1:]]
    seeds = [900 + i for i in range(12)]
    out = {}
    with Pool(4) as pool:
        for kind, path in specs:
            r = pool.map(job, [(kind, path, s) for s in seeds])
            key = f"{kind}:{path}"
            out[key] = {k: round(float(np.mean([x[k] for x in r])), 3) for k in r[0]}
            out[key]["passed_list"] = [x["passed"] for x in r]
            print(key, json.dumps(out[key]), flush=True)
    json.dump(out, open(os.path.join(HERE, "results", "evo", "compare.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
