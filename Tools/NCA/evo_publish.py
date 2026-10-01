"""Score an evolved genome on the shared yardstick and write results/evo/.

    python Tools/NCA/evo_publish.py runs/evo_s1/best.npy [--log runs/evo_s1/log.jsonl] [--ablate] [--seeds 16]

Writes: summary.json (swarm_nca.rollout, seed 7, unchanged), rollout.json (swarm_nca.pack, exactly as
swarm_gpu.py's publisher writes it), probe.json (swarm_probe.probe), genome.npy + genome.json, and
robust.json (fast_rollout over held-out seeds, plus single-behaviour ablations with --ablate).
"""
import argparse
import json
import os
import shutil
import sys
import time
from multiprocessing import Pool

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo_model as em  # noqa: E402
import swarm_nca as sn  # noqa: E402
import swarm_probe  # noqa: E402

OUT = os.path.join(HERE, "results", "evo")
MODEL = "g2"


def make(g):
    if MODEL == "compact":
        import evo_compact as ec
        return ec.CompactRule(g)
    return em.EvoRule(g)


def _eval(args):
    g, s = args
    torch.set_num_threads(1)
    summ = em.fast_rollout(make(g), s)
    f, p, m = em.fitness(summ)
    return f, p


def robust(pool, g, seeds):
    r = pool.map(_eval, [(g, s) for s in seeds])
    ps = [x[1] for x in r]
    return dict(mean_passed=round(float(np.mean(ps)), 3), passed=ps, mean_fitness=round(float(np.mean([x[0] for x in r])), 3))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("genome")
    ap.add_argument("--log", default="")
    ap.add_argument("--ablate", action="store_true")
    ap.add_argument("--seeds", type=int, default=16)
    ap.add_argument("--out", default=OUT)
    ap.add_argument("--model", default="g2", choices=["g2", "compact"])
    a = ap.parse_args()
    global MODEL
    MODEL = a.model
    os.makedirs(a.out, exist_ok=True)
    g = np.load(a.genome)
    if MODEL == "g2":
        g = em.pad(g)
    torch.set_num_threads(4)
    model = make(g)
    t0 = time.time()
    data, summary = sn.rollout(model, 240)
    passed, close = sn.tests_passed(summary)
    sn.print_cross(summary); sn.print_geo(summary); sn.print_switch(summary)
    print(f"tests_passed {passed}/8 (summed divergence {close:.1f})  [{time.time()-t0:.0f}s]")
    summary["meta"] = dict(tag="evo", genome="genome.npy", base="results/swarm_coevo_g2/rule.pt", tests_passed=passed,
                           note="G2 rule + an evolved behaviour genome (CMA-ES over the strict 8-test yardstick).")
    json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
    json.dump(sn.pack(data, 240), open(os.path.join(a.out, "rollout.json"), "w"))
    pr = swarm_probe.probe(make(g))
    print("probe", json.dumps(pr))
    json.dump(pr, open(os.path.join(a.out, "probe.json"), "w"), indent=1)
    np.save(os.path.join(a.out, "genome.npy"), g)
    if MODEL == "compact":
        import evo_compact as ec
        gj = {n: float(g[i]) for i, n in enumerate(ec.NAMES)}
        gj["_desired_table"] = ec.desired_table(g).round(4).tolist()
        summary["meta"]["note"] = "Compact hand-designed rule (evo_compact.py), ~31 evolved parameters, no neural net."
        summary["meta"]["base"] = None
        json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
    else:
        gj = {n: (g[em.SLICES[n]].tolist()) for n, _, _ in em.LAYOUT}
        gj["_desired_table"] = em.desired_table(g).round(4).tolist()
        gj["_describe"] = em.describe(g)
        print(em.describe(g))
    gj["_model"] = MODEL
    json.dump(gj, open(os.path.join(a.out, "genome.json"), "w"), indent=1)
    if a.log:
        shutil.copy(a.log, os.path.join(a.out, "evo_log.jsonl"))
    seeds = [500 + i for i in range(a.seeds)]
    rob = {}
    with Pool(4) as pool:
        rob["best"] = robust(pool, g, seeds)
        print("held-out", rob["best"])
        if a.ablate and MODEL == "g2":
            for sw in ("sw_lay", "sw_egg", "sw_lock", "sw_out", "sw_swirl"):
                if em.gene(g, sw) <= 0:
                    continue
                g2 = g.copy(); g2[em.SLICES[sw]] = -1.0
                rob[f"without_{sw}"] = robust(pool, g2, seeds)
                print(sw, "off", rob[f"without_{sw}"])
            g0 = g.copy()
            for sw in ("sw_lay", "sw_egg", "sw_lock", "sw_out", "sw_swirl"):
                g0[em.SLICES[sw]] = -1.0
            rob["base_g2_all_off"] = robust(pool, g0, seeds)
            print("all off (G2)", rob["base_g2_all_off"])
    json.dump(rob, open(os.path.join(a.out, "robust.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
