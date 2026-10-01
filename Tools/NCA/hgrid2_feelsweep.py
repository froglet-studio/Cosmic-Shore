"""Accuracy (diag, seed 7) + feel (organic band) for one config. python hgrid2_feelsweep.py [--evo] "k=v,..." """
import sys, os, json, torch
import swarm_nca as sn, swarm_feel as sf, hgrid2_diag as hd, hgrid2_eval as h2
torch.set_num_threads(int(os.environ.get("NT", "1")))
evo = "--evo" in sys.argv
arg = [a for a in sys.argv[1:] if not a.startswith("--")][0]
kw = dict(h2.BEST)
if evo:
    kw.update(h2.EVO)
kw.update({k: v.replace(";", ",") for k, v in (kv.split("=") for kv in arg.split(",") if kv)})
if evo:
    import hgrid2_evo as M
else:
    import hgrid2_model as M
r = hd.diag(M.make(**kw), log=lambda k, r: None)
f = sf.feel(M.make(**kw)); ok, ch = sf.in_band(f); m = f["mean"]
print(f"{arg:40s} full {[r[k]['full'] for k in sn.KINDS]} | organic {ok} jr {m['jerk_rel']} osc {m['osc']} stuck {m['stuck']} pex {sf.planar_excess(f):.2f} coh {m['coherence']} jit {m['jitter']}", flush=True)
