"""Calibrate swarm_feel on the four gallery approaches the lead reviewed (+ any extra models).
    python Tools/NCA/hgrid2_feelcal.py [--out results/hgrid2/feel_calibration.json]"""
import argparse, json, os, sys, torch
import swarm_eval, swarm_feel, hgrid2_eval
GALLERY = {
    "evo (organic)": "evo:results/evo/genome.npy",
    "evo compact (jerky, planar)": "evo_compact:results/evo/compact/genome.npy",
    "field (too clean)": "field:results/field/params.json",
    "hgrid oracle (accurate+organic)": "hgrid_oracle:results/hgrid/oracle/summary.json",
}
ap = argparse.ArgumentParser(); ap.add_argument("--out", default=""); ap.add_argument("--extra", action="append", default=[])
a = ap.parse_args(); torch.set_num_threads(int(os.environ.get("NT", "4")))
res = {}
for name, spec in GALLERY.items():
    res[name] = swarm_feel.feel(swarm_eval.load_model(spec)); print(name, json.dumps(res[name]["mean"]), flush=True)
for p in a.extra:
    res[p] = swarm_feel.feel(hgrid2_eval.load(p)); print(p, json.dumps(res[p]["mean"]), flush=True)
if a.out:
    json.dump(res, open(a.out, "w"), indent=1)
