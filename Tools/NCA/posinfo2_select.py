"""Pick the posinfo2 checkpoint to publish: the 16-transition yardstick at several seeds per candidate
(passed / feasible summed over seeds; ties broken by the mean own-plan loss).

    python Tools/NCA/posinfo2_select.py --seeds 7,23 --homeo 6 runs/p2_c/p2_00800.pt runs/p2_b/p2_00400.pt ...
"""
import argparse
import json
import torch
import swarm_eval as se
import posinfo2_rule as p2

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="+")
    ap.add_argument("--seeds", default="7,23")
    ap.add_argument("--homeo", type=int, default=None)
    ap.add_argument("--out", default="runs/p2_select.jsonl")
    a = ap.parse_args()
    torch.set_num_threads(4)
    f = open(a.out, "a")
    for p in a.paths:
        rule = p2.load(p, homeo=a.homeo); rule.eval()
        rec = dict(path=p, homeo=rule.homeo, seeds={})
        for s in [int(x) for x in a.seeds.split(",")]:
            r = se.evaluate(rule, seed=s, samples=3, full=True, log=lambda *_: None)
            rec["seeds"][s] = dict(passed=r["passed"], feasible=r["feasible"], own={k: v["cross"][k] for k, v in r["own"].items()},
                                   switch={k: (v["rate"], v["cross"][k.split("->")[1]]) for k, v in r["switch"].items()})
        rec["total"] = sum(v["passed"] for v in rec["seeds"].values()); rec["of"] = sum(v["feasible"] for v in rec["seeds"].values())
        print(p, rule.homeo, rec["total"], "/", rec["of"], {s: (v["passed"], v["feasible"]) for s, v in rec["seeds"].items()}, flush=True)
        f.write(json.dumps(rec) + "\n"); f.flush()
