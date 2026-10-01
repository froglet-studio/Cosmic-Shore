"""Profile posinfo2 / lite configs: per-phase ms per step over 200 steps of a grown body, the B-scaling
(ms per swarm-step for B batched swarms), network rows per tadpole-step and multiply-adds per tadpole-step.

  python Tools/NCA/lite_posinfo2_profile.py --out Tools/NCA/results/lite_posinfo2/profile.json
"""
from __future__ import annotations

import argparse
import json
import sys
import time

import torch

import lite_posinfo2_model as lm
import posinfo2_rule as p2
import swarm_nca as sn

RULE = "Tools/NCA/results/posinfo2/rule.pt"
CONFIGS = {
    "base": {},
    "homeo4": dict(homeo_every=4),
    "homeo8": dict(homeo_every=8),
    "frame4": dict(frame_every=4),
    "k2": dict(fire_k=2),
    "k4": dict(fire_k=4),
    "k8": dict(fire_k=8),
    "k16": dict(fire_k=16),
    "k4_h4_f4": dict(fire_k=4, homeo_every=4, frame_every=4),
    "h4_f4": dict(homeo_every=4, frame_every=4),
    "k2_h4_f4": dict(fire_k=2, homeo_every=4, frame_every=4),
    "k4_h8_f4": dict(fire_k=4, homeo_every=8, frame_every=4),
    "k8_h8_f8": dict(fire_k=8, homeo_every=8, frame_every=8),
    "k4_h8_f4_half": dict(fire_k=4, homeo_every=8, frame_every=4, half=1),
}


@torch.no_grad()
def grow(rule, kind, B, seed=7, steps=240):
    targets = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([targets[kind]] * B, rule.world, gen)
    for _ in range(steps):
        sw = rule(sw, gen)
    return sw, gen


_GROWN = {}


@torch.no_grad()
def profile(flags, B=1, steps=200, kind="mass"):
    key = (kind, B)
    if key not in _GROWN:                               # grow with the BASE rule so every config times the same body
        _GROWN[key] = grow(p2.load(RULE), kind, B)
    sw, gen = _GROWN[key][0].clone(), sn.make_gen(99)
    rule = lm.load(RULE, **flags)
    rule.prof = {}
    rule.net_rows = 0
    live = 0
    t0 = time.perf_counter()
    for _ in range(steps):
        live += int((sw.active & sw.hatched).sum())
        sw = rule(sw, gen)
    sec = time.perf_counter() - t0
    ms = {k: round(1000 * v / steps, 3) for k, v in rule.prof.items()}
    rows_per_ts = rule.net_rows / max(1, live)
    mac = lm.macs_per_eval(rule)
    return dict(ms_per_step=round(1000 * sec / steps, 3), ms_per_swarm_step=round(1000 * sec / steps / B, 3),
                ms_per_tadpole_step_us=round(1e6 * sec / max(1, live), 2), phases=ms, B=B,
                grown_n=round(live / steps / B, 1), net_rows_per_tadpole_step=round(rows_per_ts, 3),
                net_macs_per_eval=mac, net_macs_per_tadpole_step=round(mac * rows_per_ts))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="Tools/NCA/results/lite_posinfo2/profile.json")
    ap.add_argument("--configs", default=",".join(CONFIGS))
    ap.add_argument("--batches", default="1,4,16,64")
    a = ap.parse_args()
    torch.set_num_threads(1)
    res = {}
    for B in map(int, a.batches.split(",")):
        for name in a.configs.split(","):
            res.setdefault(name, {})
            r = profile(CONFIGS[name], B=B, steps=200 if B <= 4 else 50)
            res[name][str(B)] = r
            print(name, B, json.dumps(r), flush=True)
    json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    sys.path.insert(0, "Tools/NCA")
    main()
