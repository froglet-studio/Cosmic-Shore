"""Full scorecard for a sortfeel params file, seeds evaluated in parallel (one process each), merged into
exactly scorecard.scorecard()'s output shape.
    python sortfeel_score.py results/sortfeel/params.json results/sortfeel/scorecard.json [7,23,41] [locality]
"""
import json, os, sys
from multiprocessing import Pool
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)

def _acc(args):
    spec, s = args
    import torch; torch.set_num_threads(1)
    import swarm_eval as se, swarm_nca as sn
    res = se.evaluate(se.load_model(spec), seed=s, samples=3, full=True, log=lambda *a: None)
    return s, res

def _rest(spec, s):
    import torch; torch.set_num_threads(1)
    import swarm_eval as se, swarm_feel as sf, scorecard as scm
    m = se.load_model(spec)
    ll = scm.lossless_and_cost(m, seed=s)
    m = se.load_model(spec)
    f = sf.feel(m, seed=s); ok, ch = sf.in_band(f)
    return ll, f, ok, ch

def _job(a):
    if a[0] == "acc": return ("acc",) + _acc(a[1:])
    return ("rest",) + _rest(*a[1:])

if __name__ == "__main__":
    params, out = sys.argv[1], sys.argv[2]
    seeds = [int(x) for x in (sys.argv[3] if len(sys.argv) > 3 else "7,23,41").split(",")]
    loc = sys.argv[4] if len(sys.argv) > 4 else "mixed"
    spec = "sortfeel:" + params
    import swarm_nca as sn
    jobs = [("acc", spec, s) for s in seeds] + [("rest", spec, seeds[0])]
    with Pool(min(4, len(jobs))) as p:
        R = p.map(_job, jobs)
    sc = dict(locality=loc, accurate={}, seeds=seeds)
    full = {}
    for r in R:
        if r[0] == "acc":
            s, res = r[1], r[2]; full[str(s)] = res
            sc["accurate"][str(s)] = dict(passed=res["passed"], feasible=res["feasible"], na=res["na"],
                                          own={k: res["own"][k]["cross"][k] for k in sn.KINDS},
                                          own_passed=res["own_passed"], seconds=res["seconds"])
        else:
            ll, f, ok, ch = r[1:]
            import swarm_feel as sf
            sc["lossless"] = ll
            sc["emergent"] = dict(in_band=ok, checks=ch, mean=f["mean"], planar_excess=round(sf.planar_excess(f), 3),
                                  per_plan={k: f[k] for k in sn.KINDS})
    a = [v["passed"] / max(1, v["feasible"]) for v in sc["accurate"].values()]
    sc["headline"] = (f"accurate {min(a):.2f} worst seed | lossless {sc['lossless']['lossless']} "
                      f"({sc['lossless']['deaths_per_1k_tadpole_steps']}/1k) | {sc['lossless']['ms_per_step']} ms/step "
                      f"@{sc['lossless']['grown_n']} | organic {sc['emergent']['in_band']} | {loc}")
    json.dump(sc, open(out, "w"), indent=1)
    json.dump(full, open(out.replace(".json", "_eval.json"), "w"), indent=1)
    print(sc["headline"]); print(json.dumps(sc["accurate"], indent=1))
