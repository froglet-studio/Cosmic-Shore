"""Score any HGRID model on the shared yardstick (swarm_nca.rollout + tests_passed, swarm_probe.probe).

    python Tools/NCA/hgrid_eval.py oracle [--set k=v ...] [--out results/hgrid] [--probe]
    python Tools/NCA/hgrid_eval.py learned --ckpt runs/hgrid_nca/latest.pt [--out ...]
"""
from __future__ import annotations

import argparse
import json
import os
import time

import torch

import swarm_nca as sn
import swarm_probe
import hgrid_boid as hb

HERE = os.path.dirname(os.path.abspath(__file__))


def parse_sets(cfg, sets):
    for kv in sets or []:
        k, v = kv.split("=", 1)
        t = type(getattr(cfg, k))
        setattr(cfg, k, t(float(v)) if t in (int, float) else v)
    return cfg


def score(model, L=None, seed=7):
    L = L or sn.LossCfg()
    t0 = time.time()
    data, summary = sn.rollout(model, 240, L=L, seed=seed)
    passed, close = sn.tests_passed(summary)
    summary["tests_passed"] = passed
    summary["close"] = round(close, 2)
    summary["sec"] = round(time.time() - t0, 1)
    return data, summary


def report(summary):
    sn.print_cross(summary); sn.print_geo(summary); sn.print_switch(summary)
    print(f"TESTS PASSED {summary['tests_passed']}/8  (summed wanted-plan divergence {summary['close']})  {summary['sec']}s")


def publish(out, model, data, summary, probe=None, extra=None):
    os.makedirs(out, exist_ok=True)
    json.dump(sn.pack(data, 240), open(os.path.join(out, "rollout.json"), "w"))
    if extra:
        summary["meta"] = extra
    json.dump(summary, open(os.path.join(out, "summary.json"), "w"), indent=1)
    if probe is not None:
        json.dump(probe, open(os.path.join(out, "probe.json"), "w"), indent=1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", choices=["oracle", "learned"])
    ap.add_argument("--set", action="append")
    ap.add_argument("--ckpt", default="")
    ap.add_argument("--out", default="")
    ap.add_argument("--probe", action="store_true")
    ap.add_argument("--seeds", type=int, default=1)
    a = ap.parse_args()
    torch.set_num_threads(4)
    cfg = parse_sets(hb.BoidCfg(), a.set)
    if a.kind == "oracle":
        model = hb.make_oracle(cfg)
    else:
        import hgrid_nca
        model = hgrid_nca.load_model(a.ckpt, cfg)
    res = []
    for sd in range(a.seeds):
        data, summary = score(model, seed=7 + sd)
        report(summary)
        res.append(summary["tests_passed"])
    if a.seeds > 1:
        print("tests per seed", res, "mean", sum(res) / len(res))
    pr = None
    if a.probe:
        pr = swarm_probe.probe(model)
        print(json.dumps(pr))
    if a.out:
        publish(a.out, model, data, summary, pr, dict(kind=a.kind, cfg=hb.asdict(cfg), ckpt=a.ckpt))


if __name__ == "__main__":
    main()
