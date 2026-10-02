"""python emotion_explain.py <species> <viewer> <target emotion>: features + the probe's top contributions."""
import json, os, sys
import numpy as np
from emotion_check import encounter, CACHE, probe_mod
import importlib
pr = probe_mod.EmotionProbe(json.load(open(os.path.join(CACHE, "probe.json"))))
key, v, tgt = sys.argv[1:4]
warm = getattr(importlib.import_module(f"species.{key}"), "EMOTION_WARMUP", 0.0)
fs = [encounter(key, v, s, warm=warm) for s in (7, 23, 41)]
f = {k: float(np.mean([x[k] for x in fs])) for k in fs[0]}
sc = pr.score(f)
print("p", sc["p"])
print("top", sc["top"], "explain top:", pr.explain(f, sc["top"], 6))
print("explain target:", pr.explain(f, tgt, 8))
print({k: round(x, 3) for k, x in f.items()})
