"""Train posinfo2: the posinfo rule fine-tuned WITH SWITCHES and the molting homeostat ON, death masked.

Warm start: results/posinfo/rule.pt (tier-1 pass at the 8 bar). swarm_nca.train runs unchanged; its
module-level SwarmRule is pointed at PosInfo2Rule (homeo=4 molting, no_die=1) for the duration. With
p_switch > 0 a pool sample is culled (eaten) so another element takes the majority and is scored against
that plan from then on (sticky_plan): the designed homeostat molts/lays the composition, the learned rule
must learn to RESHAPE. Every --eval-every snapshots: the 16-transition yardstick (seed 7) + lossless count.

    python Tools/NCA/posinfo2_train.py --run runs/p2_a --steps 2400 --set p_switch=0.3
"""
import argparse
import json
import os
import sys
import time

import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import posinfo2_rule as p2  # noqa: E402
import posinfo_train as pt  # noqa: E402

INIT = os.path.join(HERE, "results", "posinfo", "rule.pt")
CFG = dict(pt.G2_CFG, p_switch=0.3, switch_cooldown=120, p_ratio=0.5, margin=0.15)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", default=os.path.join(HERE, "runs", "p2_a"))
    ap.add_argument("--steps", type=int, default=2400)
    ap.add_argument("--init", default=INIT)
    ap.add_argument("--lr", type=float, default=3e-4)
    ap.add_argument("--snap", type=int, default=100)
    ap.add_argument("--eval-every", type=int, default=200)
    ap.add_argument("--homeo", type=int, default=4)
    ap.add_argument("--p-molt", type=float, default=0.1)
    ap.add_argument("--set", nargs="*", default=[])
    a = ap.parse_args()
    torch.set_num_threads(4)
    cfgd = dict(CFG)
    for kv in a.set:
        k, v = kv.split("=", 1)
        t = type(getattr(sn.TrainCfg, k)) if hasattr(sn.TrainCfg, k) else float
        cfgd[k] = t(float(v)) if t in (int, float) else t(v)
    cfg = sn.TrainCfg(run=a.run, steps=a.steps, init=a.init, lr=a.lr, snap_every=a.snap, log_every=10, **cfgd)

    class _Rule(p2.PosInfo2Rule):
        def __init__(self, world, hidden=192):
            super().__init__(world, hidden=hidden, homeo=a.homeo, no_die=1, p_molt=a.p_molt)
    _Rule.NAN_DUMP = ""
    sn.SwarmRule = _Rule
    os.makedirs(a.run, exist_ok=True)
    json.dump(dict(homeo=a.homeo, p_molt=a.p_molt, init=a.init, cfg=cfgd), open(os.path.join(a.run, "posinfo2.json"), "w"), indent=1)
    evlog = open(os.path.join(a.run, "evals.jsonl"), "a")

    def on_snapshot(step, rule):
        p2.save(rule, os.path.join(a.run, f"p2_{step:05d}.pt"), step)
        if step % a.eval_every:
            return
        import swarm_eval, scorecard
        t0 = time.time()
        rule.eval()
        e16 = swarm_eval.evaluate(rule, samples=3, full=True, log=lambda *_: None)
        ll = scorecard.lossless_and_cost(rule, seed=7)
        rule.train()
        rec = dict(step=step, passed=e16["passed"], feasible=e16["feasible"], own_passed=e16["own_passed"],
                   own={k: e16["own"][k]["cross"][k] for k in sn.KINDS},
                   switch={k: (v["rate"], v["cross"][k.split("->")[1]]) for k, v in e16["switch"].items()},
                   deaths=ll["deaths"], ms=ll["ms_per_step"])
        evlog.write(json.dumps(rec) + "\n"); evlog.flush()
        print(swarm_eval.matrix(e16), flush=True)
        print(f"EVAL16 {step}: {e16['passed']}/{e16['feasible']} own {rec['own']} deaths {ll['deaths']} ({time.time()-t0:.0f}s)", flush=True)

    sn.train(cfg, sn.World(), sn.LossCfg(), resume=True, on_snapshot=on_snapshot)


if __name__ == "__main__":
    main()
