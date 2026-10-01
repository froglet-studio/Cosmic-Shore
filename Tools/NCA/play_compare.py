"""Multi-seed comparison of play-direction candidates on identical seeds.

Every single-trajectory number in this project is chaotic (two runs of the same rule differ in the
first decimal after a few hundred steps, from float threading order alone), so one rollout or one
strike is not evidence. This scores each rule on:
- the shared yardstick, swarm_nca.rollout + tests_passed, at three rollout seeds (7 is the canonical one)
- swarm_probe.probe (vessel strike) at four seeds: mean of recovered - before ("excess"; 0 = healed)
  and cut - before (how much the strike hurt)
- the drift control (no strike): mean score change over the same 120 steps
- predator_probe at three seeds: kills, gap, crowd, worst - before, after - before

    python Tools/NCA/play_compare.py G2=Tools/NCA/results/swarm_coevo_g2/rule.pt P2=Tools/NCA/runs/play_p2/rule_01500.pt
"""
import json
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_probe as sp  # noqa: E402
import play_swarm as ps  # noqa: E402


def score(path):
    rule = ps.load_play(path); rule.eval()
    out = dict(path=path)
    tests = []
    for seed in (7, 8, 9):
        _, s = sn.rollout(rule, 240, seed=seed)
        p, close = sn.tests_passed(s)
        own = sum(1 for k in sn.KINDS if min(s["cross"][k], key=s["cross"][k].get) == k)
        sw = sum(1 for v in s["switch"].values() if min(v["cross"], key=v["cross"].get) == v["to"])
        tests.append(dict(seed=seed, passed=p, own=own, switch=sw, close=round(close, 1)))
    out["tests"] = tests
    pr = [sp.probe(rule, seed=s) for s in (11, 12, 13, 14)]
    out["strike_excess"] = round(float(np.mean([r[k]["recovered"] - r[k]["before"] for r in pr for k in sn.KINDS])), 2)
    out["strike_cut"] = round(float(np.mean([r[k]["cut"] - r[k]["before"] for r in pr for k in sn.KINDS])), 2)
    out["strike_killed"] = round(float(np.mean([r[k]["killed"] for r in pr for k in sn.KINDS])), 1)
    out["strike_rows"] = pr
    d = ps.drift_probe(rule, seeds=(11, 12, 13, 14))
    out["drift"] = d["drift_mean"]
    pp = [ps.predator_probe(rule, seed=s) for s in (21, 22, 23)]
    f = lambda key: round(float(np.mean([r[k][key] for r in pp for k in sn.KINDS if r[k][key] is not None])), 2)
    out["pred"] = dict(kills=f("kills"), gap=f("gap"), crowd=f("crowd"),
                       hurt=round(float(np.mean([r[k]["worst"] - r[k]["before"] for r in pp for k in sn.KINDS])), 2),
                       after=round(float(np.mean([r[k]["after"] - r[k]["before"] for r in pp for k in sn.KINDS])), 2))
    out["pred_rows"] = pp
    return out


def main():
    torch.set_num_threads(4)
    res = {}
    for a in sys.argv[1:]:
        name, path = a.split("=", 1)
        res[name] = score(path)
        r = res[name]
        print(name, json.dumps(dict(tests=[t["passed"] for t in r["tests"]], own=[t["own"] for t in r["tests"]],
                                    switch=[t["switch"] for t in r["tests"]], strike_excess=r["strike_excess"],
                                    strike_cut=r["strike_cut"], drift=r["drift"], pred=r["pred"])), flush=True)
    out = os.path.join(HERE, "runs", "play", "compare.json")
    old = json.load(open(out)) if os.path.exists(out) else {}
    old.update(res)
    json.dump(old, open(out, "w"), indent=1)


if __name__ == "__main__":
    main()
