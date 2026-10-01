"""Write results/posinfo/ for a trained PosInfoRule: summary.json + rollout.json (exactly as
swarm_gpu.py's publisher writes them, so the viewer can show it), probe.json, eval16.json (swarm_eval,
16 transitions), terms.json (own-plan loss with terms switched off, vs G2), rule.pt.

    python Tools/NCA/posinfo_publish.py --rule runs/posinfo_a/pos_02000.pt [--log runs/posinfo_a]
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
import posinfo_rule as pr  # noqa: E402
import posinfo_train as pt  # noqa: E402

OUT = os.path.join(HERE, "results", "posinfo")


def terms_table(models, seeds=(7, 8, 9)):
    res = {}
    for name, m in models.items():
        acc = {}
        for sd in seeds:
            ev = pt.own_eval(m, seed=sd, samples=1)
            for k, v in ev.items():
                for t, xs in v["terms"].items():
                    acc.setdefault(k, {}).setdefault(t, []).extend(xs)
        res[name] = {k: {t: round(sum(x) / len(x), 2) for t, x in d.items()} for k, d in acc.items()}
    return res


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rule", required=True)
    ap.add_argument("--log", default="")
    ap.add_argument("--out", default=OUT)
    ap.add_argument("--full", action="store_true")
    a = ap.parse_args()
    torch.set_num_threads(4)
    os.makedirs(a.out, exist_ok=True)
    rule = pr.load(a.rule)
    rule.eval()
    with torch.no_grad():
        data, summary = sn.rollout(rule, 240, L=sn.LossCfg())
        sn.print_cross(summary); sn.print_switch(summary)
        ev = swarm_eval.evaluate(rule, samples=3, full=a.full)
        print(swarm_eval.matrix(ev))
        summary["eval16"] = {k: ev[k] for k in ("passed", "feasible", "own_passed", "std_passed", "rest_passed",
                                                "complete", "na", "samples", "tiers_run")}
        summary["eval16"]["own"] = {k: v["rate"] for k, v in ev["own"].items()}
        summary["eval16"]["switch"] = {k: v["rate"] for k, v in ev["switch"].items()}
        passed, close = sn.tests_passed(summary)
        summary["tests_passed"] = passed; summary["tests_close"] = round(close, 2)
        summary["meta"] = {"device": "cpu", "step": torch.load(a.rule, weights_only=False).get("step"), "tag": "posinfo",
                           "note": "PosInfoRule: G2 + body-frame positional information (principal-axis frame), BPTT."}
        summary["rule"] = "rule.pt"
        probe = swarm_probe.probe(rule)
        terms = terms_table({"posinfo": rule, "g2": pr.from_g2(pt.G2)})
    json.dump(sn.pack(data, 240), open(os.path.join(a.out, "rollout.json"), "w"))
    json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
    json.dump(ev, open(os.path.join(a.out, "eval16.json"), "w"), indent=1)
    json.dump(probe, open(os.path.join(a.out, "probe.json"), "w"), indent=1)
    json.dump(terms, open(os.path.join(a.out, "terms.json"), "w"), indent=1)
    shutil.copy(a.rule, os.path.join(a.out, "rule.pt"))
    if a.log:
        for f in ("log.jsonl", "evals.jsonl", "config.json", "posinfo.json"):
            p = os.path.join(a.log, f)
            if os.path.exists(p):
                shutil.copy(p, os.path.join(a.out, f))
    print("tests_passed", passed, "eval16", ev["passed"], "/", ev["feasible"])
    print("probe", json.dumps(probe))
    print("terms", json.dumps(terms, indent=1))


if __name__ == "__main__":
    main()
