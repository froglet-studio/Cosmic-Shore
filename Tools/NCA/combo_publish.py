"""Publish a combo candidate to results/combo/ - every shared yardstick, written the shared way.

    python Tools/NCA/combo_publish.py --name C --set lay_cap=1.0 ... --seeds 7,23,41 [--only ...]
  stage "eval"     swarm_eval.evaluate per seed (one process per seed: --seed N) -> runs/combo/pub_<name>_eval_<seed>.json
  stage "rest"     params.json, summary.json + rollout.json (hgrid_eval.publish = swarm_gpu's publisher format),
                   probe.json, feel.json, eval16.json (seed 7), scorecard.json (scorecard.scorecard's schema,
                   assembled from the per-seed evals + scorecard.lossless_and_cost + swarm_feel at the first seed)
"""
import argparse, json, os, time
from dataclasses import asdict
import torch
import swarm_nca as sn, swarm_eval as se, swarm_probe, swarm_feel as sf, scorecard as sc, hgrid_eval
import combo_eval

RES = os.path.join(sn.HERE, "results", "combo")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", required=True)
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--seeds", default="7,23,41")
    ap.add_argument("--seed", type=int, default=None, help="stage eval: this one seed")
    ap.add_argument("--stage", default="rest", choices=["eval", "rest"])
    ap.add_argument("--out", default=RES)
    ap.add_argument("--locality", default="mixed")
    a = ap.parse_args()
    torch.set_num_threads(1)
    model, kw = combo_eval.build(a.set)
    if a.stage == "eval":
        res = se.evaluate(model, seed=a.seed, full=True)
        print(se.matrix(res))
        json.dump(res, open(os.path.join(combo_eval.RUNS, f"pub_{a.name}_eval_{a.seed}.json"), "w"), indent=1)
        print("PUB_EVAL", a.seed, res["passed"], res["feasible"], flush=True)
        return
    out = a.out
    os.makedirs(out, exist_ok=True)
    json.dump(dict(cfg=asdict(model.cfg), model="combo_model.ComboBoid", name=a.name), open(os.path.join(out, "params.json"), "w"), indent=1)
    seeds = [int(s) for s in a.seeds.split(",")]
    card = dict(locality=a.locality, accurate={}, seeds=seeds)
    for s in seeds:
        res = json.load(open(os.path.join(combo_eval.RUNS, f"pub_{a.name}_eval_{s}.json")))
        if s == seeds[0]:
            json.dump(res, open(os.path.join(out, "eval16.json"), "w"), indent=1)
        card["accurate"][str(s)] = dict(passed=res["passed"], feasible=res["feasible"], na=res["na"],
                                        own={k: res["own"][k]["cross"][k] for k in sn.KINDS},
                                        own_passed=res["own_passed"], seconds=res["seconds"])
    card["lossless"] = sc.lossless_and_cost(combo_eval.build(a.set)[0], seed=seeds[0])
    fe = sf.feel(combo_eval.build(a.set)[0], seed=seeds[0])
    ok, checks = sf.in_band(fe)
    card["emergent"] = dict(in_band=ok, checks=checks, mean=fe["mean"], planar_excess=round(sf.planar_excess(fe), 3))
    av = [v["passed"] / max(1, v["feasible"]) for v in card["accurate"].values()]
    card["headline"] = (f"accurate {min(av):.2f} worst seed | lossless {card['lossless']['lossless']} "
                        f"({card['lossless']['deaths_per_1k_tadpole_steps']}/1k) | {card['lossless']['ms_per_step']} ms/step "
                        f"@{card['lossless']['grown_n']} | organic {ok} | {a.locality}")
    json.dump(card, open(os.path.join(out, "scorecard.json"), "w"), indent=1)
    json.dump(card, open(os.path.join(out, f"scorecard_{a.name}.json"), "w"), indent=1)
    fe["organic"] = dict(ok=ok, checks=checks, planar_excess=round(sf.planar_excess(fe), 3))
    json.dump(fe, open(os.path.join(out, "feel.json"), "w"), indent=1)
    print(card["headline"], flush=True)
    model = combo_eval.build(a.set)[0]
    data, summ = hgrid_eval.score(model)
    hgrid_eval.report(summ)
    hgrid_eval.publish(out, model, data, summ, extra=dict(kind="combo", cfg=asdict(model.cfg)))
    pr = swarm_probe.probe(combo_eval.build(a.set)[0])
    json.dump(pr, open(os.path.join(out, "probe.json"), "w"), indent=1)
    print(json.dumps(pr), flush=True)


if __name__ == "__main__":
    main()
