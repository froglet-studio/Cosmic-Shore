"""Score + publish an hgrid2 model on every shared yardstick:
  swarm_eval.evaluate (16 transitions, fair cull, 3 samples, loss-8 bar) -> eval16.json
  swarm_nca.rollout + tests_passed (old 8)                                -> summary.json, rollout.json
  swarm_probe.probe (vessel strike + heal)                                -> probe.json
  swarm_feel.feel (organic metrics)                                       -> feel.json
  the config                                                             -> params.json

    python Tools/NCA/hgrid2_eval.py --set k_fine=2 --set k_ff=1.5 ... --out Tools/NCA/results/hgrid2 [--only eval]
"""
import argparse, json, os, time
from dataclasses import asdict
import torch
import swarm_nca as sn, swarm_eval, swarm_probe, swarm_feel, hgrid_eval, hgrid2_model as hm

BEST = dict(k_fine=2, k_ff=1.5, interp=1, k_flow=0, periods="time:16", dmap_low=1, lock=60, k_class=10, sigma_rel=1.2,
            p_cross=0.25, stagger=1, k_mig=1, mig_L=40, starve_slack=0, starve_tol=0, elem_floor=1, small_slack=2)

EVO = dict(s_coarse=0.7, s_fine=1.0, steer_sync=1, ease_look=0, sync_elems="0,1,2")


def load(path):
    d = json.load(open(path))
    if d.get("model", "").startswith("hgrid2_evo"):
        import hgrid2_evo
        return hgrid2_evo.EvoGrid(hgrid2_evo.EvoCfg(**d["cfg"]))
    return hm.Boid2(sn.World(), hm.Cfg(**d["cfg"]))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--best", action="store_true")
    ap.add_argument("--out", default="")
    ap.add_argument("--only", default="eval,rollout,probe,feel")
    ap.add_argument("--threads", type=int, default=4)
    ap.add_argument("--evo", action="store_true", help="the evolved body (evo genome) steered by the grid (hgrid2_evo)")
    a = ap.parse_args()
    torch.set_num_threads(a.threads)
    kw = dict(BEST) if a.best else {}
    kw.update(dict(s.split("=", 1) for s in a.set))
    if a.evo:
        import hgrid2_evo
        kw.update(EVO)
        kw.update(dict(s.split("=", 1) for s in a.set))
        model = hgrid2_evo.make(**kw)
    else:
        model = hm.make(**kw)
    out = a.out
    if out:
        os.makedirs(out, exist_ok=True)
        json.dump(dict(cfg=asdict(model.cfg), model="hgrid2_evo.EvoGrid" if a.evo else "hgrid2_model.Boid2",
                       genome="results/evo/genome.npy" if a.evo else None), open(os.path.join(out, "params.json"), "w"), indent=1)
    only = a.only.split(",")
    if "eval" in only:
        res = swarm_eval.evaluate(model, full=True)
        print(swarm_eval.matrix(res))
        print(f"PASSED {res['passed']}/{res['feasible']} (n/a {res['na']}) {res['seconds']}s", flush=True)
        if out:
            json.dump(res, open(os.path.join(out, "eval16.json"), "w"), indent=1)
    if "rollout" in only:
        data, summ = hgrid_eval.score(model)
        hgrid_eval.report(summ)
        if out:
            hgrid_eval.publish(out, model, data, summ, extra=dict(kind="hgrid2", cfg=asdict(model.cfg)))
    if "probe" in only:
        pr = swarm_probe.probe(model)
        print(json.dumps(pr), flush=True)
        if out:
            json.dump(pr, open(os.path.join(out, "probe.json"), "w"), indent=1)
    if "feel" in only:
        fe = swarm_feel.feel(model)
        ok, checks = swarm_feel.in_band(fe)
        fe["organic"] = dict(ok=ok, checks=checks, planar_excess=round(swarm_feel.planar_excess(fe), 3))
        cal = os.path.join(sn.HERE, "results", "hgrid2", "feel_calibration.json")
        if os.path.exists(cal):
            c = json.load(open(cal))
            fe["calibration"] = {n: dict(mean=r["mean"], planar_excess=round(swarm_feel.planar_excess(r), 3),
                                         organic=swarm_feel.in_band(r)[0]) for n, r in c.items()}
        print(json.dumps(fe["mean"]), fe["organic"], flush=True)
        if out:
            json.dump(fe, open(os.path.join(out, "feel.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
