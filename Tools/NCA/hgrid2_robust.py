"""Held-out seeds for the full 16-transition yardstick: python Tools/NCA/hgrid2_robust.py <seed> <out.json> [--evo] [--set k=v]"""
import sys, json, torch
import swarm_eval, hgrid2_eval
torch.set_num_threads(2)
seed, out = int(sys.argv[1]), sys.argv[2]
kw = dict(hgrid2_eval.BEST)
for a in sys.argv[3:]:
    if a.startswith("k=") or "=" in a and not a.startswith("--"):
        k, v = a.split("=", 1); kw[k] = v
if "--evo" in sys.argv:
    import hgrid2_evo as M
else:
    import hgrid2_model as M
model = M.make(**kw)
res = swarm_eval.evaluate(model, seed=seed, full=True)
print(swarm_eval.matrix(res)); print("PASSED", res["passed"], "/", res["feasible"], res["na"], flush=True)
json.dump(res, open(out, "w"), indent=1)
