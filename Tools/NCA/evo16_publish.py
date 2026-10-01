"""evo16: score a genome on the shared yardsticks and write results/evo16/.

    python Tools/NCA/evo16_publish.py runs/evo16/s1/best.npy --log runs/evo16/s1/log.jsonl

Writes summary.json (swarm_nca.rollout, seed 7, unchanged), rollout.json (swarm_nca.pack, exactly as
swarm_gpu.py's publisher writes it), probe.json (swarm_probe.probe), eval16.json (swarm_eval --full,
seed 7, 3 samples), genome.npy + genome.json, and copies the search log. The held-out table is
evo16_measure.py's job (heldout.json).
"""
import argparse
import json
import os
import shutil
import sys
import time

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo16_model as e16  # noqa: E402
import swarm_eval as se  # noqa: E402
import swarm_nca as sn  # noqa: E402
import swarm_probe  # noqa: E402

OUT = os.path.join(HERE, "results", "evo16")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("genome")
    ap.add_argument("--log", default="")
    ap.add_argument("--out", default=OUT)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    g = np.load(a.genome)
    if len(g) < e16.DIM:
        g = e16.from_evo(g)
    os.sched_setaffinity(0, {0})
    torch.set_num_threads(1)
    t0 = time.time()
    data, summary = sn.rollout(e16.Evo16Rule(g), 240)
    passed, close = sn.tests_passed(summary)
    sn.print_cross(summary); sn.print_switch(summary)
    print(f"tests_passed {passed}/8 (summed divergence {close:.1f})  [{time.time()-t0:.0f}s]", flush=True)
    summary["meta"] = dict(tag="evo16", genome="genome.npy", base="results/swarm_coevo_g2/rule.pt", tests_passed=passed,
                           note="G2 rule + evolved behaviour genome hardened for held-out seeds and vessel-strike healing "
                                "(evo16_model.Evo16Rule: + regrowth, wound sensing, per-plan headcount).")
    json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
    json.dump(sn.pack(data, 240), open(os.path.join(a.out, "rollout.json"), "w"))
    pr = swarm_probe.probe(e16.Evo16Rule(g))
    print("probe", json.dumps({k: (v["heal"], v["n_before"], v["n_after"]) for k, v in pr.items()}), flush=True)
    json.dump(pr, open(os.path.join(a.out, "probe.json"), "w"), indent=1)
    ev = se.evaluate(e16.Evo16Rule(g), seed=7, samples=3, full=True)
    print(se.matrix(ev)); print(f"eval16 seed 7: {ev['passed']}/{ev['feasible']}", flush=True)
    json.dump(ev, open(os.path.join(a.out, "eval16.json"), "w"), indent=1)
    np.save(os.path.join(a.out, "genome.npy"), g)
    gj = {n: g[e16.SLICES[n]].tolist() for n, _, _ in e16.LAYOUT}
    gj["_describe"] = e16.describe(g)
    print(gj["_describe"])
    json.dump(gj, open(os.path.join(a.out, "genome.json"), "w"), indent=1)
    if a.log:
        shutil.copy(a.log, os.path.join(a.out, "search_log.jsonl"))


if __name__ == "__main__":
    main()
