"""Seed variance of the yardstick: score several models over several rollout seeds (one rollout is
noisy - firing masks, laying coins - at the +-10-20 summed-divergence level).

    python Tools/NCA/hgrid_seeds.py --seeds 4 --models g2,oracle,hybrid,e2e:runs/e2e_best.pt
"""
from __future__ import annotations

import argparse
import json
import os

import torch

import swarm_nca as sn
import hgrid_boid as hb
import hgrid_eval

HERE = os.path.dirname(os.path.abspath(__file__))


def build(spec):
    name, _, arg = spec.partition(":")
    if name == "g2":
        return sn.load_rule(os.path.join(HERE, "results/swarm_coevo_g2/rule.pt"))
    if name == "oracle":
        return hb.make_oracle()
    if name == "oracle_nostarve":
        return hb.make_oracle(hb.BoidCfg(starve=0))
    if name == "hybrid":
        import hgrid_hybrid
        return hgrid_hybrid.HybridRule(sn.load_rule(os.path.join(HERE, "results/swarm_coevo_g2/rule.pt")), hb.BoidCfg())
    if name == "e2e":
        import hgrid_e2e
        return hgrid_e2e.make(arg)
    raise ValueError(spec)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, default=4)
    ap.add_argument("--models", default="g2,oracle,hybrid")
    ap.add_argument("--threads", type=int, default=4)
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    res = {}
    for spec in a.models.split(","):
        m = build(spec)
        rows = []
        for s in range(a.seeds):
            _, summ = hgrid_eval.score(m, seed=7 + s)
            own = sum(sn.tests_passed(dict(cross=summ["cross"], census=summ["census"]))[:1])
            rows.append(dict(seed=7 + s, tests=summ["tests_passed"], own=own, close=summ["close"]))
            print(spec, rows[-1], flush=True)
        t = [r["tests"] for r in rows]; c = [r["close"] for r in rows]
        res[spec] = dict(rows=rows, tests_mean=sum(t) / len(t), tests_min=min(t), close_mean=round(sum(c) / len(c), 1))
        print(spec, "MEAN", res[spec]["tests_mean"], "min", res[spec]["tests_min"], "close", res[spec]["close_mean"], flush=True)
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
