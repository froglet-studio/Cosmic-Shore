"""Write results/posinfo2/ for a trained PosInfo2Rule: summary.json + rollout.json (exactly as
swarm_gpu.py's publisher writes them - sn.rollout + sn.pack - so the viewer can show it), probe.json,
eval16.json (seed 7), scorecard.json (seeds 7,23,41; accurate / lossless / performant / emergent),
feel.json (swarm_feel next to evo's calibration), rule.pt.

    python Tools/NCA/posinfo2_publish.py --rule runs/p2_a/p2_02400.pt --log runs/p2_a
"""
import argparse
import json
import os
import shutil
import sys

import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval  # noqa: E402
import swarm_probe  # noqa: E402
import swarm_feel as sf  # noqa: E402
import scorecard as scd  # noqa: E402
import posinfo2_rule as p2  # noqa: E402

OUT = os.path.join(HERE, "results", "posinfo2")
LOCALITY = "mixed"   # neighbour perception + a body frame (centroid/PCA) and census computed over the whole swarm


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rule", required=True)
    ap.add_argument("--log", default="")
    ap.add_argument("--out", default=OUT)
    ap.add_argument("--seeds", default="7,23,41")
    ap.add_argument("--tag", default="posinfo2")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    rule = p2.load(a.rule)
    rule.eval()
    torch.set_num_threads(4)
    with torch.no_grad():
        data, summary = sn.rollout(rule, 240, L=sn.LossCfg())
        sn.print_cross(summary); sn.print_switch(summary)
        ev = swarm_eval.evaluate(rule, samples=3, full=True)
        print(swarm_eval.matrix(ev))
        summary["eval16"] = {k: ev[k] for k in ("passed", "feasible", "own_passed", "std_passed", "rest_passed",
                                                "complete", "na", "samples", "tiers_run")}
        summary["eval16"]["own"] = {k: v["rate"] for k, v in ev["own"].items()}
        summary["eval16"]["switch"] = {k: v["rate"] for k, v in ev["switch"].items()}
        passed, close = sn.tests_passed(summary)
        summary["tests_passed"] = passed; summary["tests_close"] = round(close, 2)
        summary["meta"] = {"device": "cpu", "step": torch.load(a.rule, weights_only=False).get("step"), "tag": a.tag,
                           "note": "PosInfo2Rule: posinfo (G2 + body-frame positional info, BPTT) fine-tuned with switches; "
                                   "designed molting homeostat (homeo 4); learned death masked (lossless)."}
        summary["rule"] = "rule.pt"
        probe = swarm_probe.probe(rule)
        torch.set_num_threads(1)               # the scorecard's cost number is 1-thread
        sc = scd.scorecard(rule, [int(s) for s in a.seeds.split(",")], LOCALITY, 3)
        torch.set_num_threads(4)
        feel = sf.feel(rule, seed=7)
    json.dump(sn.pack(data, 240), open(os.path.join(a.out, "rollout.json"), "w"))
    json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
    json.dump(ev, open(os.path.join(a.out, "eval16.json"), "w"), indent=1)
    json.dump(probe, open(os.path.join(a.out, "probe.json"), "w"), indent=1)
    json.dump(sc, open(os.path.join(a.out, "scorecard.json"), "w"), indent=1)
    json.dump(feel, open(os.path.join(a.out, "feel.json"), "w"), indent=1)
    shutil.copy(a.rule, os.path.join(a.out, "rule.pt"))
    if a.log:
        for f in ("log.jsonl", "evals.jsonl", "config.json", "posinfo2.json"):
            p = os.path.join(a.log, f)
            if os.path.exists(p):
                shutil.copy(p, os.path.join(a.out, f))
    print("tests_passed", passed, "eval16", ev["passed"], "/", ev["feasible"])
    print("probe", json.dumps(probe))
    print("SCORECARD", sc["headline"])
    print(json.dumps(sc["accurate"], indent=1))


if __name__ == "__main__":
    main()
