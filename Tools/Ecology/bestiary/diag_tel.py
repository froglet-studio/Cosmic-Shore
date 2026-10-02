"""Per-engagement telegraph breakdown: python diag_tel.py <species> [policy ...]"""
import importlib, sys
import numpy as np
from run import make_arena, POLICIES, DT, TelProbe
from core import ScoreView
key = sys.argv[1]; pols = sys.argv[2:] or ["wander", "skimmer", "hunter"]
for pol in pols:
    for seed in (7, 23, 41):
        ar = make_arena(seed); sp = importlib.import_module(f"species.{key}").make(ar); ar.add_pilot(POLICIES[pol]())
        pr = TelProbe(DT); v = ScoreView(sp)
        for _ in range(900):
            sp.step(ar, DT); ar.step(DT); pr.observe(ar, v)
        leads = pr.first_leads(ar)
        firsts = []; last = -1e9
        for (t, n, kind, amt) in ar.log:
            if t - last > 3.0: firsts.append((round(t, 1), kind, amt))
            last = t
        print(pol, seed, [(f, round(l, 2)) for f, l in zip(firsts, leads)])
