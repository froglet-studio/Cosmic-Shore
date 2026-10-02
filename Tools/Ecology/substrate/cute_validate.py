import sys, os, json
# Requires the emotion probe from branch cece/eco-emotion (common/affect.py + emotion/); see results/emotion.json.
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import numpy as np
from substrate.cute_search import mk
from substrate.emotion_check import run
best = dict(speed=31.39, burst=2.44, comfort=68.6, w_curious=2.43, gait_hz=2.15, gait_amp=27.57, aspect=1.06,
            fear_gain=1.45, fear_decay=1.27, curiosity_rate=0.33)
out = {}
for label, p in (("best", best), ("best_no_gait", dict(best, gait_amp=0.0)), ("best_round_only", dict(best, gait_amp=0.0, w_curious=1.0, comfort=90))):
    for cond, n, mass, kw in (("sparse_fed", 60, 5000, dict(spread=500, init_hunger=0.1)),
                              ("dense_hungry", 600, 600, dict(spread=70, init_hunger=0.85))):
        ps = [run("x", mk(p)(n, s), mass, s, **kw)[0] for s in (41, 1000, 5)]
        m = {e: round(float(np.mean([q[e] for q in ps])), 3) for e in ps[0]}
        out[f"{label}/{cond}"] = m; print(label, cond, max(m, key=m.get), m, flush=True)
json.dump(out, open("substrate/cute_validate.json", "w"), indent=1)
