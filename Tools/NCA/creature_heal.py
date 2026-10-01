"""swarm_probe.probe (the shared vessel-strike probe, unchanged) over several seeds, in parallel: the
single-seed heal number is noisy (a strike removes a random third), so configs are compared on means.

    python Tools/NCA/creature_heal.py --seeds 11 12 13 14 --set wounds=0 [--set ...]
"""
import argparse
import json
import os
import sys
import multiprocessing as mp

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def _one(args):
    seed, cfg, evo = args
    import swarm_probe, creature_model as cm, evo_model as em
    if evo == "field":
        import creature_field as cf
        m = cf.CreatureField(**cfg)
    else:
        m = em.EvoRule(np.load(cm.GENOME)) if evo else cm.CreatureRule(**cfg)
    return seed, swarm_probe.probe(m, seed=seed)


def heal(seeds, cfg, evo=False):
    # sequential, one thread: a multiprocessing.Pool of torch workers ran ~60x slower here (thread
    # oversubscription / spin); run several configs as separate processes instead
    res = dict(_one((s, cfg, evo)) for s in seeds)
    kinds = list(next(iter(res.values())).keys())
    mean = {}
    for k in kinds:
        hs = [res[s][k]["heal"] for s in seeds if res[s][k]["heal"] is not None]
        mean[k] = round(float(np.mean(hs)), 3) if hs else None
    resid = {k: round(float(np.mean([res[s][k]["recovered"] - res[s][k]["before"] for s in seeds])), 3) for k in kinds}
    return dict(mean=mean, residual=resid, residual_overall=round(float(np.mean(list(resid.values()))), 3), overall=round(float(np.mean([v for v in mean.values() if v is not None])), 3), per_seed=res)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, nargs="+", default=[11, 12, 13, 14])
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--evo", action="store_true")
    ap.add_argument("--field", action="store_true")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    cfg = {kv.split("=", 1)[0]: json.loads(kv.split("=", 1)[1]) for kv in a.set}
    r = heal(a.seeds, cfg, "field" if a.field else a.evo)
    print(json.dumps({"cfg": cfg, "evo": a.evo, "residual": r["residual"], "residual_overall": r["residual_overall"], "heal_mean": r["mean"]}))
    if a.out:
        json.dump(r, open(a.out, "w"), indent=1)
