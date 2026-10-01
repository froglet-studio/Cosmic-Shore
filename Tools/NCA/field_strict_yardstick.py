"""The yardstick with a FAIR switch cull, for any model: identical to swarm_nca.rollout's eight tests except
the cull removes every element other than the switch target down below it (field_probes.strict_cull), so the
switch target really holds the majority afterwards. (The stock dragonfly -> whale cull leaves Space at 73%.)

    python Tools/NCA/field_strict_yardstick.py   # field model + the learned rules -> results/field/strict_yardstick.json
"""
import json
import os
import sys

import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import field_swarm as fs  # noqa: E402
import field_probes as fp  # noqa: E402
import field_hybrid as fh  # noqa: E402


@torch.no_grad()
def strict_rollout(model, L, steps=240, seed=7):
    T = sn.load_targets(); gen = sn.make_gen(seed)
    summary = {"cross": {}, "census": {}, "switch": {}}
    for k in sn.KINDS:
        sw = sn.seed_swarm([T[k]], model.world, gen)
        for _ in range(steps):
            sw = model(sw, gen)
        x = sn.decode(sw, 0)
        summary["cross"][k] = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        summary["census"][k] = sn.census(sw, 0, T[k])
        to = sn.SWITCH_TO[k]
        fp.strict_cull(sw, 0, to, gen)
        for _ in range(steps):
            sw = model(sw, gen)
        x = sn.decode(sw, 0); new = sn.PLAN_OF[to]
        summary["switch"][k] = dict(to=new, done=True, cross={k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS},
                                    majority=sn.majority_plan(sw, 0, k), census=sn.census(sw, 0, T[new]))
    return summary


def main():
    torch.set_num_threads(4)
    res = {}
    models = [("field", lambda: fs.FieldSwarm(), 0)]
    for tag in ("g2", "f1", "e8"):
        path = os.path.join(HERE, "results", f"swarm_coevo_{tag}", "rule.pt")
        si = int(json.load(open(os.path.join(HERE, "results", f"swarm_coevo_{tag}", "summary.json"))).get("scale_inv", 0))
        models += [(tag, lambda p=path: sn.load_rule(p), si), (tag + "+composition", lambda p=path: fh.Hybrid(sn.load_rule(p)), si)]
    for name, make, si in models:
        m = make(); m.eval()
        s = strict_rollout(m, sn.LossCfg(scale_inv=si))
        p, close = sn.tests_passed(s)
        res[name] = dict(passed=p, close=round(close, 2), switch_best={k: min(v["cross"], key=v["cross"].get) + "/" + v["to"] for k, v in s["switch"].items()},
                         n={k: v["census"]["n"] for k, v in s["switch"].items()})
        print(name, res[name], flush=True)
    json.dump(res, open(os.path.join(HERE, "results", "field", "strict_yardstick.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
