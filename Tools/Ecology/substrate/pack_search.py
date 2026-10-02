# Requires the emotion probe from branch cece/eco-emotion (common/affect.py + emotion/); see results/emotion.json.
"""Can the pack PARAMETERS alone move its read from 'playful' (0.88 at the shipped params) to menacing /
terrifying? Random search over the stalk regime (+ strike speed, quorum threshold, body size/aspect)."""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from dataclasses import replace
from substrate.emotion_check import run
from substrate import species as S

def mk(p):
    def f(seed):
        P = S.pack(seed=seed)
        st = replace(P.solitary, speed=p["speed"], ring_r=p["ring_r"], w_ring=p["w_ring"], w_hunt=p["w_hunt"],
                     turn=p["turn"], size=p["size"], aspect=p["aspect"], w_wander=p["w_wander"])
        sk = replace(P.gregarious, speed=p["strike"], size=p["size"] * 1.1, aspect=p["aspect"])
        return replace(P, solitary=st, gregarious=sk, q_up=p["q_up"], n0=int(p["n"]), capacity=int(p["n"]) * 2,
                       ring_roles=int(p["n"]))
    return f

def score(p, seeds=(7, 23)):
    ps = [run("x", mk(p)(s), 2500, s)[0] for s in seeds]
    return {e: float(np.mean([q[e] for q in ps])) for e in ps[0]}

rng = np.random.default_rng(int(sys.argv[1]))
res = []
for it in range(int(sys.argv[2])):
    p = dict(speed=float(rng.uniform(30, 140)), ring_r=float(rng.uniform(60, 300)), w_ring=float(rng.uniform(0.3, 2.0)),
             w_hunt=float(rng.uniform(0.0, 1.0)), turn=float(rng.uniform(0.8, 3.5)), size=float(rng.uniform(4, 14)),
             aspect=float(rng.uniform(1.5, 4.0)), w_wander=float(rng.uniform(0.0, 0.3)), strike=float(rng.uniform(120, 220)),
             q_up=float(rng.uniform(0.5, 1.2)), n=float(rng.integers(4, 13)))
    sc = score(p); res.append((sc["menacing"] + sc["terrifying"], p, sc))
    print(it, round(sc["menacing"], 3), round(sc["terrifying"], 3), max(sc, key=sc.get), {k: round(v, 2) for k, v in p.items()}, flush=True)
res.sort(key=lambda x: -x[0])
json.dump(res[:8], open(os.path.join(os.path.dirname(__file__), f"pack_search_{sys.argv[1]}.json"), "w"), indent=1)
