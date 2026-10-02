"""Score every substrate species with the shared threat/feel scorecard (common/scorecard.py), plus the
substrate's encounter metrics (metrics.py). 3 pilot policies x 2 seeds x `minutes`.

    python -m substrate.run_species [--minutes 1.5]     -> results/scorecard.json
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from common.arena import Arena, Pilot
from common.scorecard import Probe, run_score, combine
from substrate.core import Substrate
from substrate.metrics import Encounter
from substrate import species as S

HERE = os.path.dirname(__file__)
MK = dict(wander=Pilot.wanderer, evader=Pilot.evader, hunter=Pilot.hunter)


def one(name, policy, seed, minutes, dt=0.1, backend="numpy"):
    ar = Arena(seed=seed); ar.scatter_mass(2500)
    pl = ar.add_pilot(MK[policy]())
    sp = Substrate(ar, S.SPECIES[name](seed=seed), backend=backend)
    pl.pos = sp.home + ar._ball(1, 350, 450)[0]       # an encounter, not a 1200 u search
    pr = Probe(dt); enc = Encounter()
    for _ in range(int(minutes * 60 / dt)):
        sp.step(ar, dt); ar.step(dt); pr.observe(ar, sp); enc.observe(ar, sp)
    r = run_score(ar, sp, pr, minutes); r["encounter"] = enc.summary()
    r["phase_end"] = round(float(sp.phase[sp.alive].mean()), 3) if sp.alive.any() else None
    return r


if __name__ == "__main__":
    minutes = float(sys.argv[sys.argv.index("--minutes") + 1]) if "--minutes" in sys.argv else 1.5
    out = {}
    for name in ("grazer", "locust", "pack", "leviathan"):
        runs = {}
        for policy in ("wander", "evader", "hunter"):
            for seed in (7, 23):
                runs[(policy, seed)] = one(name, policy, seed, minutes)
        card = combine(runs, minutes)
        card["encounter_wander"] = {k: round(float(np.mean([runs[("wander", s)]["encounter"][k] or 0 for s in (7, 23)])), 3)
                                    for k in runs[("wander", 7)]["encounter"]}
        card["phase_end_wander"] = [runs[("wander", s)]["phase_end"] for s in (7, 23)]
        out[name] = card
        print(name, json.dumps(card))
    json.dump(out, open(os.path.join(HERE, "results", "scorecard.json"), "w"), indent=1)
