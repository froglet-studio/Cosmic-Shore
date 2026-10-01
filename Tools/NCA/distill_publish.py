"""distill: publish a student to results/distill/ (summary.json, rollout.json as swarm_gpu writes it,
probe.json, eval16.json, student.pt).

    python Tools/NCA/distill_publish.py --ckpt runs/distill/cen/student_07.pt --tag census
"""
import argparse
import json
import os
import shutil
import sys
import time

import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import swarm_probe  # noqa: E402
import distill_train as dtr  # noqa: E402


def main():
    torch.set_num_threads(4)
    ap = argparse.ArgumentParser()
    ap.add_argument("--ckpt", required=True)
    ap.add_argument("--out", default=os.path.join(HERE, "results", "distill"))
    ap.add_argument("--tag", default="distill")
    ap.add_argument("--note", default="")
    ap.add_argument("--skip_eval", action="store_true")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    st = dtr.load(a.ckpt)
    t0 = time.time()
    data, summary = sn.rollout(st, 240)
    passed, close = sn.tests_passed(summary)
    sn.print_cross(summary); sn.print_switch(summary)
    print(f"TESTS PASSED {passed}/8 (summed wanted divergence {close:.2f}) {time.time() - t0:.0f}s")
    json.dump(sn.pack(data, 240), open(os.path.join(a.out, "rollout.json"), "w"))
    summary["meta"] = {"device": "cpu", "tag": a.tag, "note": a.note or "distill: local student distilled from field",
                       "cfg": st.cfg.__dict__, "tests_passed": passed, "close": round(close, 2)}
    summary["rule"] = "student.pt"
    json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
    probe = swarm_probe.probe(dtr.load(a.ckpt))
    print("probe:", json.dumps({k: {kk: v[kk] for kk in ("before", "cut", "recovered", "heal", "n_before", "n_after")} for k, v in probe.items()}))
    json.dump(probe, open(os.path.join(a.out, "probe.json"), "w"), indent=1)
    shutil.copy(a.ckpt, os.path.join(a.out, "student.pt"))
    if not a.skip_eval:
        res = se.evaluate(dtr.load(a.ckpt), full=True, samples=3)
        print(se.matrix(res)); print(f"PASSED {res['passed']}/{res['feasible']}")
        json.dump(res, open(os.path.join(a.out, "eval16.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
