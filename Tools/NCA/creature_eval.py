"""The shared 16-transition yardstick (swarm_eval.evaluate, unchanged) on the creature model.

    python Tools/NCA/creature_eval.py [--seed 7] [--set key=value ...] [--out results/creature/eval16.json]
"""
import argparse
import json
import os
import sys

import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_eval as se  # noqa: E402
import creature_model as cm  # noqa: E402


def parse_sets(sets):
    out = {}
    for s in sets:
        k, v = s.split("=", 1)
        out[k] = json.loads(v)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--samples", type=int, default=3)
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    torch.set_num_threads(4)
    model = cm.CreatureRule(**parse_sets(a.set))
    res = se.evaluate(model, seed=a.seed, samples=a.samples, full=True)
    res["cfg"] = model.cfg
    print(se.matrix(res))
    print(f"PASSED {res['passed']}/{res['feasible']} (n/a {res['na']}) {res['seconds']}s")
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
