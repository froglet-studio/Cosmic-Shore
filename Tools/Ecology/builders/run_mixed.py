"""One cell, three builder/thief species, three kinds of pilot. Do they coexist, and who gets the mass?
All fauna share the cell's ONE controlling colour (CLAUDE.md), so they can never steal from each other - they
compete only for loose mass. python Tools/Ecology/builders/run_mixed.py"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from common.arena import Arena, Pilot, Recorder
from builders import harness
from builders.harness import ram, make_pilot, vary_lines, OUT
from builders.wasp import WaspComb
from builders.traps import TrapBuilders
from builders.wearers import Wearers


def run(seed, minutes=5.0, dt=0.1, record=False):
    ar = Arena(seed=seed); ar.scatter_mass(1500); ar.struct_owner = {}
    pil = [make_pilot(p, ar) for p in ("circuit", "wander", "hunter")]
    sps = [WaspComb(ar, seed=seed), TrapBuilders(ar, seed=seed, n=30, max_nb=2, lane_min=3.0),
           Wearers(ar, seed=seed, contact=0.5, body_cap=150)]
    rec = Recorder(every=harness.REC_EVERY) if record else None; hud = []
    for _ in range(int(minutes * 60 / dt)):
        tg, th = [], []
        for sp in sps:
            sp.step(ar, dt); tg += list(ar.targets); th += list(ar.threats)
        ar.targets, ar.threats = tg, th
        ram(ar, sps, dt); vary_lines(ar); ar.step(dt)
        if rec:
            rec.frame(ar, sps)
            if rec.k % rec.every == 0:
                hud.append(" | ".join(sp.hud(ar) for sp in sps))
    res = dict(seed=seed, audit=round(ar.audit(), 6), moves_per_s=round(ar.moves / (minutes * 60), 1),
               hits={p.name: dict(n=len(p.hits), kinds={k: sum(1 for h in p.hits if h[1] == k) for k in {h[1] for h in p.hits}})
                     for p in pil},
               species={sp.name: sp.metrics(ar, minutes) for sp in sps})
    taken = {sp.name: (sp.placed if hasattr(sp, "placed") else sp.worn_steals) for sp in sps}
    res["mass_share"] = {k: round(v / max(sum(taken.values()), 1), 3) for k, v in taken.items()}
    if rec:
        os.makedirs(OUT, exist_ok=True)
        path = os.path.join(OUT, f"mixed_{seed}.json")
        rec.save(path, dict(label=f"mixed cell (seed {seed})", note="Wasp comb + trap web + wearers in one cell; "
                            "pilots: a racer (circuit), a wanderer, a hunter.", hud=hud))
        res["recording"] = path
    return res


if __name__ == "__main__":
    rows = []; recs = []
    for sd in (7, 23):
        r = run(sd, record=(sd == 7))
        if "recording" in r: recs.append(r.pop("recording"))
        rows.append(r); print(json.dumps(r, default=str), flush=True)
    json.dump(rows, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "results", "mixed.json"), "w"), indent=1, default=str)
    from common.viewer import build
    build(os.path.join(OUT, "mixed.html"), recs, "Builders and thieves: one cell")
