"""Score an evofate model on every shared yardstick and write results/evofate/.

    python Tools/NCA/evofate_publish.py '<FateCfg json>' [--skip rollout,probe,eval,scorecard,feel,terms]

Writes: params.json (FateCfg + the evo genome it wraps), genome.npy (copy of results/evo/genome.npy),
summary.json (swarm_nca.rollout, seed 7), rollout.json (swarm_nca.pack, exactly as swarm_gpu.py's
publisher writes it), probe.json (swarm_probe.probe), eval16.json (swarm_eval.evaluate --full, 3 samples,
seed 7), scorecard.json (scorecard.scorecard at seeds 7/23/41, locality "mixed"), feel.json.
"""
import argparse
import dataclasses as dc
import json
import os
import shutil
import sys
import time

import torch

HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import swarm_probe  # noqa: E402
import swarm_feel as sf  # noqa: E402
import scorecard as sc  # noqa: E402
import evofate_model as ef  # noqa: E402

OUT = os.path.join(HERE, "results", "evofate")


def assemble(out, seeds):
    """scorecard.json in scorecard.scorecard's format, from the split parts (eval16_seed*.json, lossless.json, feel.json)."""
    r = dict(locality="mixed", accurate={}, seeds=seeds)
    for s in seeds:
        res = json.load(open(os.path.join(out, f"eval16_seed{s}.json")))
        r["accurate"][str(s)] = dict(passed=res["passed"], feasible=res["feasible"], na=res["na"],
                                     own={k: res["own"][k]["cross"][k] for k in sn.KINDS}, own_passed=res["own_passed"],
                                     seconds=res["seconds"])
    r["lossless"] = json.load(open(os.path.join(out, "lossless.json")))
    f = json.load(open(os.path.join(out, "feel.json")))
    r["emergent"] = dict(in_band=f["organic"]["ok"], checks=f["organic"]["checks"], mean=f["mean"],
                         planar_excess=f["organic"]["planar_excess"])
    acc = [v["passed"] / max(1, v["feasible"]) for v in r["accurate"].values()]
    r["headline"] = (f"accurate {min(acc):.2f} worst seed | lossless {r['lossless']['lossless']} "
                     f"({r['lossless']['deaths_per_1k_tadpole_steps']}/1k) | {r['lossless']['ms_per_step']} ms/step "
                     f"@{r['lossless']['grown_n']} | organic {r['emergent']['in_band']} | mixed")
    json.dump(r, open(os.path.join(out, "scorecard.json"), "w"), indent=1)
    print(r["headline"])


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("cfg"); ap.add_argument("--out", default=OUT)
    ap.add_argument("--skip", default=""); ap.add_argument("--seeds", default="7,23,41")
    ap.add_argument("--threads", type=int, default=1)
    ap.add_argument("--parts", default="", help="acc,lossless,assemble: the scorecard split across processes")
    a = ap.parse_args(); torch.set_num_threads(a.threads)
    os.makedirs(a.out, exist_ok=True); skip = set(a.skip.split(","))
    cfg = ef.FateCfg(**json.loads(a.cfg))
    mk = lambda: ef.EvoFate(cfg)
    json.dump(dict(cfg=dc.asdict(cfg), model="evofate_model.EvoFate", genome="genome.npy (results/evo, unchanged)",
                   rule="results/swarm_coevo_g2/rule.pt"), open(os.path.join(a.out, "params.json"), "w"), indent=1)
    shutil.copy(ef.EVO_GENOME, os.path.join(a.out, "genome.npy"))
    t0 = time.time()
    if "rollout" not in skip:
        data, summary = sn.rollout(mk(), 240)
        passed, close = sn.tests_passed(summary)
        sn.print_cross(summary); sn.print_switch(summary)
        print(f"tests_passed {passed}/8 (summed {close:.1f}) [{time.time() - t0:.0f}s]", flush=True)
        summary["meta"] = dict(tag="evofate", params="params.json", tests_passed=passed,
                               note="The evolved rule (G2 + evo genome) with a sticky FATE: newborns commit to an under-occupied "
                                    "positional-information well of their (element, region) type and a designed term steers them to it; "
                                    "composition designed (sort's homeostat + molting), learned death suppressed.")
        json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
        json.dump(sn.pack(data, 240), open(os.path.join(a.out, "rollout.json"), "w"))
    if "probe" not in skip:
        pr = swarm_probe.probe(mk()); print("probe", json.dumps(pr), flush=True)
        json.dump(pr, open(os.path.join(a.out, "probe.json"), "w"), indent=1)
    if "eval" not in skip:
        res = se.evaluate(mk(), seed=7, samples=3, full=True)
        print(se.matrix(res)); print("eval16", res["passed"], "/", res["feasible"], flush=True)
        json.dump(res, open(os.path.join(a.out, "eval16.json"), "w"), indent=1)
    if "feel" not in skip:
        f = sf.feel(mk(), seed=7); ok, checks = sf.in_band(f)
        f["organic"] = dict(ok=ok, checks=checks, planar_excess=round(sf.planar_excess(f), 3))
        json.dump(f, open(os.path.join(a.out, "feel.json"), "w"), indent=1)
    if "acc" in a.parts:                       # one scorecard seed (run seeds in parallel processes)
        for s in [int(x) for x in a.seeds.split(",")]:
            res = se.evaluate(mk(), seed=s, samples=3, full=True)
            print(se.matrix(res)); print("seed", s, res["passed"], "/", res["feasible"], flush=True)
            json.dump(res, open(os.path.join(a.out, f"eval16_seed{s}.json"), "w"), indent=1)
    if "lossless" in a.parts:
        r = sc.lossless_and_cost(mk(), seed=7)
        json.dump(r, open(os.path.join(a.out, "lossless.json"), "w"), indent=1)
    if "assemble" in a.parts:
        assemble(a.out, [int(x) for x in a.seeds.split(",")])
    if "scorecard" not in skip:
        r = sc.scorecard(mk(), [int(s) for s in a.seeds.split(",")], "mixed")
        print(r["headline"], flush=True)
        json.dump(r, open(os.path.join(a.out, "scorecard.json"), "w"), indent=1)
    print(f"done {time.time() - t0:.0f}s")


if __name__ == "__main__":
    main()
