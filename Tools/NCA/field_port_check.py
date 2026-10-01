"""Run the C# port of the field swarm (field_port/FieldSwarmCore.cs) through the yardstick protocol and
score its dumped states with the UNCHANGED Python scorer. Needs a .NET 8 SDK (`DOTNET` env var or PATH).

    python Tools/NCA/field_port_check.py      # -> Tools/NCA/results/field/port_check.json
"""
import json
import os
import subprocess
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import field_swarm as fs  # noqa: E402


def export(path):
    plans = [fs.Plan(sn.load_targets()[k], 6) for k in sn.KINDS]
    T = sn.load_targets()
    gen = sn.make_gen(7)
    seeds = []
    for i, k in enumerate(sn.KINDS):
        sw = sn.seed_swarm([T[k]], sn.World(), gen)
        m = sw.active[0]
        seeds.append(dict(plan=i, pos=sw.pos[0][m].tolist(), elem=sw.elem[0][m].tolist(), dom=sw.dom[0][m].tolist()))
    json.dump(dict(plans=[dict(kind=p.kind, n=p.n, nslots=int((p.slot_mix > 0).sum()), order=p.order, elem=p.elem.tolist(),
                               slot=p.slot.tolist(), mix=p.mix.tolist(), slot_mix=p.slot_mix.tolist(), P=p.P.tolist()) for p in plans],
                   seeds=seeds), open(path, "w"))


def to_swarm(d, plan: fs.Plan):
    """A Swarm the scorer can decode: positions/elements/domains from C#, looks from the assigned slot."""
    n = len(d["elem"])
    _, _, ss = plan.at(d["t"])
    s = np.zeros((n, sn.C), np.float32)
    h = np.array(d["home"]); ok = h >= 0
    s[ok] = ss[h[ok]]
    s[:, sn.A] = 1.0; s[:, sn.DIE] = -12.0
    t = lambda a, dt=torch.float32: torch.tensor(np.asarray(a), dtype=dt)[None]
    act = torch.ones(1, n, dtype=torch.bool)
    return sn.Swarm(t(d["pos"]), t(s), t(d["elem"], torch.long), t(d["dom"], torch.long), act, act.clone(),
                    torch.zeros(1, dtype=torch.long))


def main():
    dotnet = os.environ.get("DOTNET", "dotnet")
    run = os.path.join(HERE, "runs", "field_port"); os.makedirs(run, exist_ok=True)
    export(os.path.join(run, "in.json"))
    subprocess.run([dotnet, "run", "-c", "Release", "--project", os.path.join(HERE, "field_port"), "--",
                    os.path.join(run, "in.json"), os.path.join(run, "out.json")], check=True)
    out = json.load(open(os.path.join(run, "out.json")))
    T = sn.load_targets(); plans = {k: fs.Plan(T[k], 6) for k in sn.KINDS}; L = sn.LossCfg()
    summary = {"cross": {}, "census": {}, "switch": {}}
    for k in sn.KINDS:
        o = out[k]
        pre = to_swarm(o["pre"], plans[sn.KINDS[o["pre"]["plan"]]])
        x = sn.decode(pre, 0)
        summary["cross"][k] = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        summary["census"][k] = sn.census(pre, 0, T[k])
        new = sn.PLAN_OF[sn.SWITCH_TO[k]]
        post = to_swarm(o["post"], plans[sn.KINDS[o["post"]["plan"]]])
        x = sn.decode(post, 0)
        summary["switch"][k] = dict(to=new, done=True, cross={k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS},
                                    majority=sn.majority_plan(post, 0, k), census=sn.census(post, 0, T[new]))
    sn.print_cross(summary); sn.print_switch(summary)
    passed, close = sn.tests_passed(summary)
    cost = {k: dict(grow_ms=round(out[k]["grow_ms"], 3), idle_ms=round(out[k]["idle_ms"], 3), vessel_ms=round(out[k]["pred_ms"], 3),
                    n=out[k]["n_idle"], switches=out[k]["post"]["switches"]) for k in sn.KINDS}
    print(f"C# PORT: TESTS PASSED {passed}/8 (summed {close:.2f}); cost per swarm-step:", cost)
    json.dump(dict(passed=passed, close=round(close, 2), summary=summary, cost=cost),
              open(os.path.join(HERE, "results", "field", "port_check.json"), "w"), indent=1)


if __name__ == "__main__":
    torch.set_num_threads(4)
    main()
