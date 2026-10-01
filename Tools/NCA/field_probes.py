"""Extra probes for the `field` direction (designed fields + boids), on top of the shared yardstick.

  * switch_trace   - the rollout's own cull (swarm_nca.lose_majority, mode "excess"), but scored every
                     10 steps afterwards: how many steps until the new plan is closest (switch latency),
                     and the post-cull census. Also a STRICT variant whose cull really hands the majority
                     to the switch target (every other element culled below it), since the yardstick's
                     dragonfly cull leaves Space, not Mass, in the majority.
  * predator_compare - the predator pass with the school's reactions ON versus OFF (same seed), so the
                     parting is measured against a swarm that does not react at all.
  * ablations      - the strict tests for mode="field" (no slot assignment), molt=0, dwell=0.

    python Tools/NCA/field_probes.py [--out Tools/NCA/results/field/probes_extra.json]
"""
import argparse
import json
import os
import sys
import time
from dataclasses import replace

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import field_swarm as fs  # noqa: E402


@torch.no_grad()
def strict_cull(sw, b, to, gen, keep_extra=1):
    """Cull every element other than `to` down below `to`'s count (keep_extra fewer), as if a predator
    ate selectively. Returns the number removed."""
    m = sw.active[b] & sw.hatched[b]
    c = torch.bincount(sw.elem[b][m], minlength=4)
    cap = max(0, int(c[to]) - keep_extra)
    killed = 0
    for e in range(4):
        if e == to or int(c[e]) <= cap:
            continue
        idx = (m & (sw.elem[b] == e)).nonzero().squeeze(1)
        kill = idx[torch.randperm(len(idx), generator=gen)[:int(c[e]) - cap]]
        sw.active[b, kill] = False; sw.hatched[b, kill] = False; sw.s[b, kill] = 0.0
        killed += len(kill)
    return killed


def switch_trace(cfg, strict=False, steps=240, after=240, seed=7, L=None):
    L = L or sn.LossCfg()
    T = sn.load_targets()
    out = {}
    for k in sn.KINDS:
        model = fs.FieldSwarm(cfg)
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k]], model.world, gen)
        for _ in range(steps):
            sw = model(sw, gen)
        to = sn.SWITCH_TO[k]
        if strict:
            killed = strict_cull(sw, 0, to, gen)
        else:
            n0 = int((sw.active[0] & sw.hatched[0]).sum())
            sn.lose_majority(sw, 0, gen, to=to)
            killed = n0 - int((sw.active[0] & sw.hatched[0]).sum())
        cen0 = sn.census(sw, 0, T[k])
        want = sn.PLAN_OF[to]
        trace, latency = [], None
        for t in range(after + 1):
            if t % 10 == 0:
                x = sn.decode(sw, 0)
                row = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
                best = min(row, key=row.get)
                trace.append(dict(t=t, best=best, n=int((sw.active[0] & sw.hatched[0]).sum()), want=row[want]))
                if latency is None and best == want:
                    latency = t
            if t < after:
                sw = model(sw, gen)
        x = sn.decode(sw, 0)
        row = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        n = int((sw.active[0] & sw.hatched[0]).sum())
        ok = n >= sn.MIN_TEST_BODY and row[want] < min(v for k2, v in row.items() if k2 != want) - 1e-6
        out[k] = dict(to=want, killed=killed, after_cull=cen0["elements"], majority_after_cull=cen0["majority"],
                      pass_=bool(ok), cross=row, n=n, elements=sn.census(sw, 0, T[want])["elements"],
                      latency_steps=latency, switches=model.mem[0]["switches"], trace=trace)
    return out


def predator_compare(cfg):
    off = replace(cfg, flee=(0.0, 0.0, 0.0, 0.0), relay=0.0, sense=0.01)
    res = {}
    for k in sn.KINDS:
        on = fs.predator_pass(fs.FieldSwarm(cfg), k)
        of = fs.predator_pass(fs.FieldSwarm(off), k)
        res[k] = dict(react_on={kk: v for kk, v in on.items() if kk != "trace"},
                      react_off={kk: v for kk, v in of.items() if kk != "trace"})
    return res


def ablation(cfg, **kw):
    c = replace(cfg, **kw)
    data, summary = sn.rollout(fs.FieldSwarm(c), 240)
    p, close = sn.tests_passed(summary)
    diag = {k: summary["cross"][k][k] for k in sn.KINDS}
    sw = {k: summary["switch"][k]["cross"][summary["switch"][k]["to"]] for k in sn.KINDS}
    return dict(set=kw, passed=p, close=round(close, 2), own=diag, switch_to=sw)


def main():
    torch.set_num_threads(4)
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(HERE, "results", "field", "probes_extra.json"))
    ap.add_argument("--set", action="append", default=[])
    a = ap.parse_args()
    cfg = fs.FieldCfg()
    for kv in a.set:
        k, v = kv.split("=", 1)
        cur = getattr(cfg, k)
        setattr(cfg, k, type(cur)(eval(v)) if isinstance(cur, tuple) else type(cur)(v))
    t0 = time.time()
    res = {}
    res["switch_yardstick_cull"] = switch_trace(cfg)
    res["switch_strict_cull"] = switch_trace(cfg, strict=True)
    for name in ("switch_yardstick_cull", "switch_strict_cull"):
        print(name)
        for k, v in res[name].items():
            print(f"  {k:7s}->{v['to']:7s} pass={v['pass_']} latency={v['latency_steps']} killed={v['killed']} "
                  f"after_cull={v['after_cull']} n={v['n']} el={v['elements']} want={v['cross'][v['to']]}")
    res["predator"] = predator_compare(cfg)
    for k, v in res["predator"].items():
        print(f"  predator {k:7s} touched on {v['react_on']['touched_share']} off {v['react_off']['touched_share']}  "
              f"worst on {v['react_on']['worst']} off {v['react_off']['worst']}")
    res["ablations"] = [ablation(cfg, mode="field"), ablation(cfg, molt=0), ablation(cfg, dwell=1),
                        ablation(cfg, mode="field", molt=0)]
    for r in res["ablations"]:
        print("  ablation", r["set"], r["passed"], "own", r["own"], "switch", r["switch_to"])
    res["cfg"] = {k: v for k, v in vars(cfg).items()}
    json.dump(res, open(a.out, "w"), indent=1)
    print(f"done in {time.time() - t0:.0f}s -> {a.out}")


if __name__ == "__main__":
    main()
