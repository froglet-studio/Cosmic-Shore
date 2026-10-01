"""Per-phase profile of a combo-family model: time each phase of the step (perf_counter wrappers, the
ProfilerMarker idea) over `steps` steps of a body grown 240 steps, at batch sizes B, 1 thread.

    python Tools/NCA/lite_combo_profile.py --model combo:results/combo/params.json --out x.json [--B 1,4,16]
"""
from __future__ import annotations

import argparse
import json
import time
from collections import defaultdict

import torch

import swarm_eval as se
import swarm_nca as sn
import hgrid_boid as hb
import hgrid2_model as hm
import combo_model as cm


PHASES = [  # (class, method name, label)
    (hb.FieldBoid, "step", "coarse.step(incl field,lay)"),
    (hm.Boid2, "decide", "decide"),
    (cm.CachedOracle, "__call__", "coarse.plan_field(cache)"),
    (hb.FieldBoid, "_lay", "lay"),
    (cm.ComboBoid, "starve_staggered", "molt(controller)"),
    (cm.ComboBoid, "fine_disp", "fine(total)"),
    (hm.Boid2, "fine_targets", "fine.targets"),
    (cm.ComboBoid, "_proxy_dom", "proxy/transfer2"),
    (cm.ComboBoid, "migrate", "migrate(total)"),
]


def install(acc):
    saved = []
    for cls, name, label in PHASES:
        f = cls.__dict__[name]
        saved.append((cls, name, f))

        def wrap(f=f, label=label):
            def g(*a, **k):
                t = time.perf_counter()
                try:
                    return f(*a, **k)
                finally:
                    acc[label] += time.perf_counter() - t
            return g
        setattr(cls, name, wrap())
    return saved


def uninstall(saved):
    for cls, name, f in saved:
        setattr(cls, name, f)


def run(spec, B=1, grow=240, steps=200, phases=True, kinds=None):
    torch.set_num_threads(1)
    T = sn.load_targets()
    kinds = kinds or sn.KINDS
    res = {}
    for kind in kinds:
        m = se.load_model(spec)
        gen = sn.make_gen(7)
        sw = sn.seed_swarm([T[kind]] * B, m.world, gen)
        for _ in range(grow):
            sw = m(sw, gen)
        acc = defaultdict(float)
        saved = install(acc) if phases else []
        n = int((sw.active & sw.hatched).sum())
        t = time.perf_counter()
        try:
            for _ in range(steps):
                sw = m(sw, gen)
        finally:
            uninstall(saved)
        wall = time.perf_counter() - t
        res[kind] = dict(ms_step=round(wall / steps * 1000, 3), ms_swarm_step=round(wall / steps * 1000 / B, 3),
                         us_tadpole_step=round(wall / steps * 1e6 / max(n, 1), 3), n=n,
                         phases_ms={k: round(v / steps * 1000, 3) for k, v in sorted(acc.items(), key=lambda kv: -kv[1])})
    keys = ["ms_step", "ms_swarm_step", "us_tadpole_step"]
    res["mean"] = {k: round(sum(res[x][k] for x in kinds) / len(kinds), 3) for k in keys}
    return res


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--B", default="1")
    ap.add_argument("--steps", type=int, default=200)
    ap.add_argument("--nophases", action="store_true")
    a = ap.parse_args()
    out = {}
    for B in [int(x) for x in a.B.split(",")]:
        r = run(a.model, B=B, steps=a.steps, phases=not a.nophases)
        out[f"B{B}"] = r
        print(f"B={B}", json.dumps(r["mean"]), flush=True)
        if B == 1:
            for k in sn.KINDS:
                print(" ", k, r[k]["ms_step"], r[k]["phases_ms"], flush=True)
    json.dump(out, open(a.out, "w"), indent=1)
