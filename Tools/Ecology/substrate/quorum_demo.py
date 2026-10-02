"""The locust claim: ONE parameter set (species.locust) is cute when sparse and fed and frightening when dense
and hungry. Three runs, same params, measured with the shared scorecard's feel stats:

  sparse_fed     60 locusts spread over the cell, rich mass, hunger ~0.1
  dense_hungry   600 locusts in one cloud, little mass, hunger ~0.85
  emergent       200 locusts, modest mass, normal start: the population eats down its food, gets hungry and
                 crowds, and the quorum flips it on its own (time to flip is reported)

    python -m substrate.quorum_demo     (from Tools/Ecology)
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from common.arena import Arena, Pilot
from common.scorecard import Probe, run_score
from substrate.core import Substrate
from substrate import species as S
from substrate.metrics import Encounter

HERE = os.path.dirname(__file__)


def run(cond, seed, minutes=1.5, dt=0.1, backend="numpy", policy="wander"):
    ar = Arena(seed=seed)
    if cond == "sparse_fed":
        ar.scatter_mass(5000, vol=(20, 60)); P = S.locust(n0=60, seed=seed); kw = dict(spread=500, init_hunger=0.1)
    elif cond == "dense_hungry":
        ar.scatter_mass(600); P = S.locust(n0=600, seed=seed); kw = dict(spread=70, init_hunger=0.85)
    else:
        ar.scatter_mass(2500); P = S.locust(n0=200, seed=seed); kw = dict(spread=120)
    pl = ar.add_pilot(Pilot.wanderer() if policy == "wander" else Pilot.evader())
    sp = Substrate(ar, P, backend=backend, **kw)
    if cond != "emergent":   # place the pilot near the swarm so the feel stats see an encounter
        pl.pos = sp.home + np.array([250.0, 0, 0]); pl.goal = sp.home.copy()
    pr = Probe(dt); enc = Encounter(); trace = []; flip = None
    for i in range(int(minutes * 60 / dt)):
        sp.step(ar, dt); ar.step(dt); pr.observe(ar, sp); enc.observe(ar, sp)
        ph = float(sp.phase[sp.alive].mean()) if sp.alive.any() else 0
        gf = float(np.mean(sp.phase[sp.alive] > 0.5)) if sp.alive.any() else 0
        if flip is None and gf > 0.25: flip = round(ar.t, 1)      # a band (>= 1/4 of the population) went gregarious
        if i % 10 == 0:
            trace.append(dict(t=round(ar.t, 1), phase=round(ph, 3), greg_frac=round(gf, 3), hunger=round(float(sp.hunger[sp.alive].mean()), 3),
                              n=int(sp.alive.sum()), live_vol=round(ar.live_volume())))
    r = run_score(ar, sp, pr, minutes); r.pop("hit_times")
    r.update(encounter=enc.summary(), greg_frac_end=round(float(np.mean(sp.phase[sp.alive] > 0.5)), 3), phase_end=round(float(sp.phase[sp.alive].mean()), 3), flip_t=flip, trace=trace)
    return r


if __name__ == "__main__":
    out = {}
    for cond, mins in (("sparse_fed", 1.5), ("dense_hungry", 1.5), ("emergent", 4.0)):
        out[cond] = [run(cond, s, minutes=mins) for s in (7, 23)]
        f = [r["feel"] for r in out[cond]]
        print(cond, "phase", [r["phase_end"] for r in out[cond]], "greg", [r["greg_frac_end"] for r in out[cond]], "flip", [r["flip_t"] for r in out[cond]],
              "hits/min", [r["hits_per_min"] for r in out[cond]],
              {k: round(float(np.mean([x[k] for x in f])), 3) for k in f[0]}, out[cond][0]["encounter"])
    json.dump(out, open(os.path.join(HERE, "results", "quorum.json"), "w"), indent=1)
