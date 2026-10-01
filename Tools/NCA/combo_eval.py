"""Score combo candidates.

    python Tools/NCA/combo_eval.py --name molt --set molt=1 --seed 7 [--quick] [--lossless]
      --quick      tier 1 only (own plans, 3 samples), prints own losses
      --lossless   scorecard.lossless_and_cost + feel (no eval)
      default      full swarm_eval at --seed -> runs/combo/<name>_eval_<seed>.json

Every candidate is hgrid2_eval.BEST + its --set overrides on combo_model.ComboCfg.
"""
import argparse, json, os, time
import torch
import swarm_nca as sn, swarm_eval as se, swarm_feel as sf, scorecard as sc
import hgrid2_eval, combo_model as cm

RUNS = os.path.join(sn.HERE, "runs", "combo")


def build(sets):
    kw = dict(hgrid2_eval.BEST)
    kw.update(dict(s.split("=", 1) for s in sets))
    return cm.make(**kw), kw


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", required=True)
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--quick", action="store_true")
    ap.add_argument("--lossless", action="store_true")
    ap.add_argument("--threads", type=int, default=1)
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    os.makedirs(RUNS, exist_ok=True)
    model, kw = build(a.set)
    t0 = time.time()
    if a.lossless:
        ll = sc.lossless_and_cost(model, seed=a.seed)
        f = sf.feel(model, seed=a.seed); ok, checks = sf.in_band(f)
        out = dict(kw=kw, lossless=ll, emergent=dict(in_band=ok, checks=checks, mean=f["mean"],
                                                      planar_excess=round(sf.planar_excess(f), 3)), molts=model.molts)
        path = os.path.join(RUNS, f"{a.name}_lossless_{a.seed}.json")
    else:
        res = se.evaluate(model, seed=a.seed, full=not a.quick, gate=(4, 6) if not a.quick else (5, 99))
        print(se.matrix(res))
        out = dict(kw=kw, res=res, passed=res["passed"], feasible=res["feasible"],
                   own={k: res["own"][k]["cross"][k] for k in sn.KINDS}, molts=model.molts)
        path = os.path.join(RUNS, f"{a.name}_{'quick' if a.quick else 'eval'}_{a.seed}.json")
    out["seconds"] = round(time.time() - t0, 1)
    json.dump(out, open(path, "w"), indent=1)
    print("RESULT", a.name, a.seed, json.dumps({k: v for k, v in out.items() if k not in ("res", "kw", "emergent")}), flush=True)


if __name__ == "__main__":
    main()
