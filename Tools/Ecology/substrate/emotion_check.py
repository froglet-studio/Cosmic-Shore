"""Score substrate species/conditions with Direction C's frozen emotion probe, when the shared scorecard
carries it (common/affect.py + emotion/results/probe.json, branch cece/eco-emotion). On a tree without the
probe this prints the legacy feel stats only.

    python -m substrate.emotion_check     -> results/emotion.json (if the probe is present)
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from common.arena import Arena, Pilot
from common.scorecard import Probe, run_score
from substrate.core import Substrate
from substrate import species as S

HERE = os.path.dirname(__file__)
EMO = ("cute", "playful", "eerie", "majestic", "menacing", "terrifying")


def run(label, P, mass, seed, minutes=1.0, dt=0.1, **kw):
    ar = Arena(seed=seed); ar.scatter_mass(*mass) if isinstance(mass, tuple) else ar.scatter_mass(mass)
    pl = ar.add_pilot(Pilot.wanderer())
    sp = Substrate(ar, P, **kw)
    if kw.get("anchor") is None:
        pl.pos = sp.home + np.array([250.0, 0, 0]); pl.goal = sp.home.copy()
    pr = Probe(dt)
    for _ in range(int(minutes * 60 / dt)):
        sp.step(ar, dt); ar.step(dt); pr.observe(ar, sp)
    f = pr.feel()
    return {e: f.get(f"emo_{e}") for e in EMO}, f


if __name__ == "__main__":
    conds = [
        ("locust sparse+fed", lambda s: S.locust(n0=60, seed=s), 5000, dict(spread=500, init_hunger=0.1)),
        ("locust dense+hungry", lambda s: S.locust(n0=600, seed=s), 600, dict(spread=70, init_hunger=0.85)),
        ("grazer", lambda s: S.grazer(seed=s), 2500, {}),
        ("pack", lambda s: S.pack(seed=s), 2500, {}),
        ("lurker", lambda s: S.lurker(seed=s), 2500, dict(anchor="mass")),
        ("stampede", lambda s: S.stampede(seed=s), 2500, {}),
        ("leviathan (assembled)", lambda s: S.leviathan(seed=s), 2500, dict(spread=120, init_hunger=0.1)),
    ]
    out = {}
    for label, mk, mass, kw in conds:
        ps = []
        for seed in (7, 23):
            p, f = run(label, mk(seed), mass, seed, **kw)
            ps.append(p)
        if ps[0]["cute"] is None:
            print(label, "no emotion probe in this tree"); continue
        mean = {e: round(float(np.mean([p[e] for p in ps])), 3) for e in EMO}
        out[label] = dict(p=mean, top=max(mean, key=mean.get))
        print(label, out[label])
    if out:
        json.dump(out, open(os.path.join(HERE, "results", "emotion.json"), "w"), indent=1)
