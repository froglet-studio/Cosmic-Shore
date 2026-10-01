"""Train PosInfoRule by backprop through time, warm-started from G2 (step 0 == G2).

Reuses swarm_nca.train unchanged: the module-level SwarmRule name is pointed at PosInfoRule for the
duration (train() builds `SwarmRule(world, hidden)` and zero-pads new input columns on warm start,
which is exactly the step-0-equals-G2 initialisation). Every `--eval-every` snapshot steps the own
plans are grown from fresh seeds (swarm_eval tier 1, 3 samples) and the per-term breakdown is logged
to <run>/evals.jsonl.

    python Tools/NCA/posinfo_train.py --run runs/posinfo_a --steps 3000
"""
import argparse
import json
import os
import sys
import time
from dataclasses import replace

import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import posinfo_rule as pr  # noqa: E402

G2 = os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt")

# G2's own training overrides (results/swarm_coevo_g2/summary.json meta), switches off for stage 1
G2_CFG = dict(per_kind=2, pool=24, seed_every=6, roll_min=48, roll_max=96, bptt=28, sticky_plan=1,
              p_switch=0.0, switch_cooldown=240, p_ratio=0.5, margin=0.15, w_over=1.0, learned_lay=1,
              min_body=76.0, w_body=20.0)

TERMS = None


def term_variants():
    base = sn.LossCfg()
    return {"pos": replace(base, w_h=0, w_tier=0, w_face=0, w_sp=0, w_dom=0, w_elem=0),
            "+elem": replace(base, w_h=0, w_tier=0, w_face=0, w_sp=0, w_dom=0),
            "+dom": replace(base, w_h=0, w_tier=0, w_face=0, w_sp=0),
            "full": base}


@torch.no_grad()
def own_eval(rule, seed=7, samples=3, steps=240):
    """Grow every plan `samples` times; per plan: mean full loss, pass rate (<=8, closest, >=32), terms."""
    T = sn.load_targets()
    V = term_variants()
    kinds = [k for k in sn.KINDS for _ in range(samples)]
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[k] for k in kinds], rule.world, gen)
    for _ in range(steps):
        sw = rule(sw, gen)
    out = {}
    for b, k in enumerate(kinds):
        x = sn.decode(sw, b)
        row = {k2: sn.swarm_loss(x, T[k2], sn.LossCfg())[1]["sink"] for k2 in sn.KINDS}
        n = int((sw.active[b] & sw.hatched[b]).sum())
        terms = {v: sn.swarm_loss(x, T[k], L)[1]["sink"] for v, L in V.items()}
        others = min(v for k2, v in row.items() if k2 != k)
        ok = n >= sn.MIN_TEST_BODY and row[k] <= sn.MAX_TEST_LOSS and row[k] < others
        o = out.setdefault(k, dict(loss=[], ok=[], n=[], terms={v: [] for v in V}, gap=[]))
        o["loss"].append(round(row[k], 2)); o["ok"].append(ok); o["n"].append(n); o["gap"].append(round(others - row[k], 2))
        for v in V:
            o["terms"][v].append(round(terms[v], 2))
    return out


def summarise(ev):
    return " | ".join(f"{k[:2]} {sum(v['loss'])/len(v['loss']):5.2f} ok{sum(v['ok'])}" for k, v in ev.items())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", default=os.path.join(HERE, "runs", "posinfo_a"))
    ap.add_argument("--steps", type=int, default=3000)
    ap.add_argument("--init", default=G2)
    ap.add_argument("--morph", type=int, default=0)
    ap.add_argument("--lr", type=float, default=5e-4)
    ap.add_argument("--snap", type=int, default=100)
    ap.add_argument("--eval-every", type=int, default=200)
    ap.add_argument("--set", nargs="*", default=[], help="TrainCfg overrides key=value")
    a = ap.parse_args()
    torch.set_num_threads(4)
    cfgd = dict(G2_CFG)
    for kv in a.set:
        k, v = kv.split("=", 1)
        t = type(getattr(sn.TrainCfg, k)) if hasattr(sn.TrainCfg, k) else float
        cfgd[k] = t(float(v)) if t in (int, float) else t(v)
    cfg = sn.TrainCfg(run=a.run, steps=a.steps, init=a.init, lr=a.lr, snap_every=a.snap, log_every=10, **cfgd)
    class _Rule(pr.PosInfoRule):                                         # train() builds SwarmRule(world, hidden)
        def __init__(self, world, hidden=192):
            super().__init__(world, hidden=hidden, morph=a.morph)
    _Rule.NAN_DUMP = ""
    sn.SwarmRule = _Rule
    os.makedirs(a.run, exist_ok=True)
    json.dump(dict(morph=a.morph, init=a.init), open(os.path.join(a.run, "posinfo.json"), "w"))
    evlog = open(os.path.join(a.run, "evals.jsonl"), "a")

    def on_snapshot(step, rule):
        pr.save(rule, os.path.join(a.run, f"pos_{step:05d}.pt"), step)
        if step % a.eval_every:
            return
        t0 = time.time()
        rule.eval()
        ev = own_eval(rule)
        rule.train()
        evlog.write(json.dumps(dict(step=step, own=ev)) + "\n"); evlog.flush()
        print(f"EVAL {step}: {summarise(ev)}  ({time.time()-t0:.0f}s)", flush=True)

    sn.train(cfg, sn.World(), sn.LossCfg(), resume=True, on_snapshot=on_snapshot)


if __name__ == "__main__":
    main()
