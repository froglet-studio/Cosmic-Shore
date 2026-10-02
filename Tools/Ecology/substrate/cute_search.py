import json, os, sys, time
# Requires the emotion probe from branch cece/eco-emotion (common/affect.py + emotion/); see results/emotion.json.
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from dataclasses import replace
from substrate.emotion_check import run
from substrate import species as S

def mk(params):
    def f(n0, seed, hunger=None):
        P = S.locust(n0=n0, seed=seed)
        sol = replace(P.solitary, **{k: v for k, v in params.items() if k in P.solitary.__dataclass_fields__})
        spk = {k: v for k, v in params.items() if k in ("fear_gain", "fear_decay", "curiosity_rate")}
        return replace(P, solitary=sol, **spk)
    return f

def score(params, seeds=(7, 23), minutes=1.0):
    f = mk(params); ps = []
    for s in seeds:
        p, _ = run("x", f(60, s), 5000, s, minutes=minutes, spread=500, init_hunger=0.1); ps.append(p)
    return {e: float(np.mean([p[e] for p in ps])) for e in ps[0]}

rng = np.random.default_rng(int(sys.argv[1]) if len(sys.argv) > 1 else 0)
best = []
base = dict(speed=26, burst=2.0, comfort=90, w_curious=1.0, gait_hz=0.0, gait_amp=0.0, aspect=1.5,
            fear_gain=1.0, fear_decay=0.6, curiosity_rate=0.4)
print("base", score(base), flush=True)
for it in range(int(sys.argv[2]) if len(sys.argv) > 2 else 30):
    p = dict(speed=float(rng.uniform(25, 110)), burst=float(rng.uniform(1.0, 2.5)), comfort=float(rng.uniform(20, 110)),
             w_curious=float(rng.uniform(0.5, 3.0)), gait_hz=float(rng.uniform(1.2, 3.0)),
             gait_amp=float(rng.choice([0.0, rng.uniform(8, 40)])), aspect=float(rng.uniform(1.0, 1.3)),
             fear_gain=float(rng.uniform(0.3, 2.0)), fear_decay=float(rng.uniform(0.3, 2.0)),
             curiosity_rate=float(rng.uniform(0.2, 1.0)))
    sc = score(p)
    best.append((sc["cute"], p, sc))
    print(it, round(sc["cute"], 3), max(sc, key=sc.get), {k: round(v, 2) for k, v in p.items()}, flush=True)
best.sort(key=lambda x: -x[0])
json.dump(best[:8], open(os.path.join(os.path.dirname(__file__), f"cute_search_{sys.argv[1] if len(sys.argv)>1 else 0}.json"), "w"), indent=1)
