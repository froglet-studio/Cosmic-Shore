"""Re-score one evofate cfg at one seed: full swarm_eval (16 transitions, 3 samples) + swarm_feel AT THAT SEED.
    python Tools/NCA/evofate_validate.py <name> <seed> '<FateCfg json>'   (appends to runs/evofate/val3.jsonl)"""
import json, os, sys, torch
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import swarm_eval as se, swarm_feel as sf, swarm_nca as sn, evofate_model as ef
torch.set_num_threads(1)
name, seed, cfg = sys.argv[1], int(sys.argv[2]), json.loads(sys.argv[3])
r = se.evaluate(ef.EvoFate(ef.FateCfg(**cfg)), seed=seed, samples=3, full=True, log=lambda *a: None)
f = sf.feel(ef.EvoFate(ef.FateCfg(**cfg)), seed=seed); ok, ch = sf.in_band(f)
rec = dict(name=name, seed=seed, cfg=cfg, passed=r["passed"], feasible=r["feasible"],
           own={k: round(r["own"][k]["cross"][k],2) for k in sn.KINDS}, fails=[k for k,v in r["switch"].items() if not v["ok"]],
           feel=dict(osc=f["mean"]["osc"], jr=f["mean"]["jerk_rel"], pe=round(sf.planar_excess(f),3), jitter=f["mean"].get("jitter"), coherence=f["mean"].get("coherence"), band=ok))
print(json.dumps(rec), flush=True)
open("runs/evofate/val3.jsonl","a").write(json.dumps(rec)+"\n")
