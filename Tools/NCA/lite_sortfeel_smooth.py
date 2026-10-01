"""lite_sortfeel smooth screen: swarm_smooth.smooth (seed 7, the hold's SMOOTH axis) for lite configs,
one JSON per config under results/lite_sortfeel/smooth/. Cheap (~30 s each) so it survives restarts."""
import json, os, sys, torch
import swarm_eval as se, swarm_smooth as ss
torch.set_num_threads(4)
os.makedirs("results/lite_sortfeel/smooth", exist_ok=True)
for q in sys.argv[1:]:
    out = f"results/lite_sortfeel/smooth/{q.replace(',', '_').replace('=', '').replace('.', 'p')}.json"
    if os.path.exists(out):
        continue
    m = se.load_model("lite_sortfeel:results/sortfeel/params.json?" + q)
    r = ss.smooth(m, seed=7, log=lambda *_: None)
    d = {k: r[k] for k in ("smoothness", "mean", "worst", "deaths")}
    d["query"] = q
    json.dump(d, open(out, "w"), indent=1)
    print(q, d["smoothness"], d["worst"], flush=True)
