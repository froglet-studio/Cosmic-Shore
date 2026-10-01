"""Chain demo for hgrid2 models (round 1's hgrid_chain.chain, unchanged): one swarm eaten four times,
whale -> jellyfish -> pufferfish -> dragonfly -> whale (the last phase uses lose_majority, so Space,
not Mass, takes over - see round 1's NOTE).
    python Tools/NCA/hgrid2_chain.py [--evo] --out results/hgrid2/chain.json"""
import argparse, json, torch
import swarm_nca as sn, hgrid_chain, hgrid2_eval as h2
ap = argparse.ArgumentParser(); ap.add_argument("--evo", action="store_true"); ap.add_argument("--out", default="")
a = ap.parse_args(); torch.set_num_threads(2)
kw = dict(h2.BEST)
if a.evo:
    import hgrid2_evo as M; kw.update(h2.EVO)
else:
    import hgrid2_model as M
res, data = hgrid_chain.chain(M.make(**kw))
print(json.dumps(res))
if a.out:
    json.dump(dict(result=res, rollout=sn.pack({"chain": data}, 240 * len(hgrid_chain.ORDER))), open(a.out, "w"))
