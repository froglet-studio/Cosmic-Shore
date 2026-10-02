"""Quick diagnostics: python diag.py <species> <policy> <seed> [minutes] -> per-10s min pilot distance, hits,
live count, mean intent of the nearest agent."""
import importlib, sys
import numpy as np
from run import make_arena, POLICIES, DT
key, pol, seed = sys.argv[1], sys.argv[2], int(sys.argv[3]); mins = float(sys.argv[4]) if len(sys.argv) > 4 else 1.5
ar = make_arena(seed); sp = importlib.import_module(f"species.{key}").make(ar); ar.add_pilot(POLICIES[pol]())
md, mi = 1e9, 0
for s in range(int(mins * 60 / DT)):
    sp.step(ar, DT); ar.step(DT)
    P = sp.pos[sp.alive]
    if len(P):
        dd = np.linalg.norm(P - ar.pilots[0].pos, axis=1); j = np.argmin(dd); md = min(md, dd[j]); mi = max(mi, sp.intent[sp.alive][j])
    if (s + 1) % 100 == 0:
        print(f"t {ar.t:5.1f} mind {md:7.1f} maxint {mi:.2f} hits {len(ar.log)} live {sp.alive.sum()} "
              + " ".join(f"{k}={v}" for k, v in getattr(sp, 'phase_stats', lambda: {})().items()))
        md, mi = 1e9, 0
