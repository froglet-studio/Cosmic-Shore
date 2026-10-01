"""Score a sort model on every shared yardstick and write results/sort/.

    python Tools/NCA/sort_publish.py runs/sort/cma_k12/best.npy --set K=12,per_well=4,molt=1 [--log runs/.../log.jsonl]
    python Tools/NCA/sort_publish.py "" --set ...          # the base config (no CMA vector)

Writes: params.json (the whole model: SortCfg), summary.json (swarm_nca.rollout, seed 7, unchanged),
rollout.json (swarm_nca.pack, exactly as swarm_gpu.py's publisher writes it), probe.json
(swarm_probe.probe), eval16.json (swarm_eval.evaluate --full, 3 samples, seed 7), heldout.json
(swarm_eval.evaluate on seeds 101..104, own tier + switches), terms.json (pos / +elem / +dom / full).
"""
import argparse, dataclasses as dc, json, os, shutil, sys, time
import numpy as np, torch

HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import swarm_probe  # noqa: E402
import sort_model as sm  # noqa: E402
import sort_eval as so  # noqa: E402
import sort_diag as sd  # noqa: E402

OUT = os.path.join(HERE, "results", "sort")


def build(vec, sets):
    m = so.make("", sets)
    if vec:
        m = sm.SortSwarm(sm.cfg_from_vec(np.load(vec), m.cfg))
    return m


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("vec"); ap.add_argument("--set", default="")
    ap.add_argument("--log", default=""); ap.add_argument("--out", default=OUT); ap.add_argument("--heldout", type=int, default=4)
    ap.add_argument("--skip", default="", help="comma list of parts to skip: rollout,probe,eval,heldout,terms")
    a = ap.parse_args(); torch.set_num_threads(4)
    os.makedirs(a.out, exist_ok=True); skip = set(a.skip.split(","))
    m = build(a.vec, a.set)
    cfg = dc.asdict(m.cfg)
    json.dump(dict(cfg=cfg, model="sort_model.SortSwarm", vec=a.vec, set=a.set), open(os.path.join(a.out, "params.json"), "w"), indent=1)
    if a.vec:
        np.save(os.path.join(a.out, "cma_vec.npy"), np.load(a.vec))
    if a.log:
        shutil.copy(a.log, os.path.join(a.out, "search_log.jsonl"))
    t0 = time.time()
    if "rollout" not in skip:
        data, summary = sn.rollout(m, 240)
        passed, close = sn.tests_passed(summary)
        sn.print_cross(summary); sn.print_switch(summary)
        print(f"tests_passed {passed}/8 (summed {close:.1f}) [{time.time() - t0:.0f}s]", flush=True)
        summary["meta"] = dict(tag="sort", params="params.json", tests_passed=passed,
                               note="Emergent cell sorting: per-type positional-information wells + fate commitment + local adhesion/swaps; class homeostat.")
        json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
        json.dump(sn.pack(data, 240), open(os.path.join(a.out, "rollout.json"), "w"))
    if "terms" not in skip:
        json.dump(sd.breakdown(build(a.vec, a.set)), open(os.path.join(a.out, "terms.json"), "w"), indent=1)
    if "probe" not in skip:
        pr = swarm_probe.probe(build(a.vec, a.set)); print("probe", json.dumps(pr), flush=True)
        json.dump(pr, open(os.path.join(a.out, "probe.json"), "w"), indent=1)
    if "eval" not in skip:
        res = se.evaluate(build(a.vec, a.set), seed=7, samples=3, full=True)
        print(se.matrix(res)); print("eval16", res["passed"], "/", res["feasible"], flush=True)
        json.dump(res, open(os.path.join(a.out, "eval16.json"), "w"), indent=1)
    if "heldout" not in skip:
        ho = {}
        for s in range(a.heldout):
            r = se.evaluate(build(a.vec, a.set), seed=101 + s, samples=3, full=True)
            ho[101 + s] = dict(passed=r["passed"], feasible=r["feasible"], own_passed=r["own_passed"],
                               own_losses={k: r["own"][k]["cross"][k] for k in sn.KINDS}, matrix=se.matrix(r), na=r["na"])
            print("held-out seed", 101 + s, ho[101 + s]["passed"], "/", ho[101 + s]["feasible"], ho[101 + s]["own_losses"], flush=True)
        json.dump(ho, open(os.path.join(a.out, "heldout.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
