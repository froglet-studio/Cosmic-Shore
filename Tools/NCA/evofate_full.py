"""Full yardstick (swarm_eval.evaluate, 16 transitions, 3 samples) + swarm_feel for a list of cfgs.
    python evofate_full.py <out.jsonl> '<json list of cfg dicts>' [seed]"""
import json, sys, time
import torch
import swarm_eval as se, swarm_feel as sf, swarm_nca as sn
import evofate_model as ef

if __name__ == "__main__":
    torch.set_num_threads(1)
    seed = int(sys.argv[3]) if len(sys.argv) > 3 else 7
    for cfg in json.loads(sys.argv[2]):
        t = time.time()
        r = se.evaluate(ef.EvoFate(ef.FateCfg(**cfg)), seed=seed, samples=3, full=True, log=lambda *a: None)
        f = sf.feel(ef.EvoFate(ef.FateCfg(**cfg)), seed=7); ok, ch = sf.in_band(f)
        rec = dict(cfg=cfg, seed=seed, passed=r["passed"], feasible=r["feasible"], own={k: r["own"][k]["cross"][k] for k in sn.KINDS},
                   sw={k: v["cross"][sn.KINDS[[*sn.KINDS].index(k.split('->')[1])]] for k, v in r["switch"].items()},
                   sw_ok={k: v["ok"] for k, v in r["switch"].items()}, own_ok={k: r["own"][k]["ok"] for k in sn.KINDS},
                   feel=dict(mean=f["mean"], in_band=ok, checks=ch, pe=round(sf.planar_excess(f), 3),
                             per={k: {q: f[k][q] for q in ("osc", "planar_frac", "planar_plan", "coherence", "jitter")} for k in sn.KINDS}),
                   matrix=se.matrix(r), sec=round(time.time() - t))
        print(json.dumps(cfg), rec["passed"], "/", rec["feasible"], rec["own"], "osc", f["mean"]["osc"], "pe", rec["feel"]["pe"], "band", ok, flush=True)
        open(sys.argv[1], "a").write(json.dumps(rec) + "\n")
