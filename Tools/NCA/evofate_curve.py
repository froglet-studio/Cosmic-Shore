"""The trade-off curve: fate pull strength vs own-plan loss vs feel (swarm_feel organic band).

    python evofate_curve.py <out.jsonl> '<json list of cfg dicts>'   (one process; run several in parallel)
"""
import json
import sys
import time

import torch

import swarm_feel as sf
import evofate_model as ef
import evofate_fast as fast


def run(cfg, seeds=(7, 23)):
    t = time.time()
    rows = {}
    for s in seeds:
        r = fast.screen(ef.EvoFate(ef.FateCfg(**cfg)), seed=s)
        rows[s] = dict(fit=round(fast.fitness(r), 2), own={k: v["loss"] for k, v in r["own"].items()},
                       own_ok=sum(v["ok"] for v in r["own"].values()),
                       sw={k: v["loss"] for k, v in r["sw"].items()}, sw_ok=sum(v["ok"] for v in r["sw"].values()))
    f = sf.feel(ef.EvoFate(ef.FateCfg(**cfg)), seed=7)
    ok, checks = sf.in_band(f)
    keep = {k: f[k] for k in f if k != "mean"}
    return dict(cfg=cfg, screen=rows, feel=dict(mean=f["mean"], in_band=ok, checks=checks,
                                                 planar_excess=round(sf.planar_excess(f), 3), per_plan=keep),
                sec=round(time.time() - t))


if __name__ == "__main__":
    torch.set_num_threads(1)
    out = sys.argv[1]
    for cfg in json.loads(sys.argv[2]):
        r = run(cfg)
        print(json.dumps(cfg), r["screen"][7]["own"], r["feel"]["mean"]["jitter"], r["feel"]["in_band"], flush=True)
        with open(out, "a") as fh:
            fh.write(json.dumps(r) + "\n")
