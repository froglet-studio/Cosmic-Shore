"""Wall-clock cost per agent-step of the substrate, numpy vs numba, N = 1k / 10k / 100k, fractional k = 1/4/8,
with a per-stage breakdown. Single process; numba uses all cores (NUMBA_NUM_THREADS). Writes bench.json.

    python -m substrate.bench [--quick]
"""
import json, os, sys, time, platform
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from dataclasses import replace
from common.arena import Arena, Pilot
from substrate.core import Substrate
from substrate import species as S

HERE = os.path.dirname(__file__)


def bench(N, backend, k, steps, G=40):
    ar = Arena(seed=7); ar.scatter_mass(4000); ar.add_pilot(Pilot.wanderer())
    P = replace(S.locust(n0=N), capacity=N, frac_k=k, birth_stock=1e12)     # fixed population for timing
    sp = Substrate(ar, P, backend=backend, spread=0.3 * ar.R, G=G)
    sp.publish = False          # scorecard plumbing (python lists of positions) is not part of the sim
    for _ in range(3):
        sp.step(ar, 0.1); ar.step(0.1)
    sp.timers = {}
    t = time.perf_counter()
    for _ in range(steps):
        sp.step(ar, 0.1); ar.step(0.1)
    wall = (time.perf_counter() - t) / steps
    stages = {kk: round(v / steps * 1000, 3) for kk, v in sp.timers.items()}
    return dict(N=N, backend=backend, k=k, ms_step=round(wall * 1000, 3),
                us_per_agent_step=round(wall / N * 1e6, 4), stages_ms=stages)


if __name__ == "__main__":
    quick = "--quick" in sys.argv
    rows = []
    for backend in ("numpy", "numba", "fused"):
        for N in (1000, 10000, 100000):
            for k in (1, 4, 8):
                steps = 30 if N <= 1000 else (12 if N <= 10000 else 4)
                if quick: steps = max(2, steps // 4)
                r = bench(N, backend, k, steps); rows.append(r)
                print(json.dumps(r))
    import numba
    meta = dict(cpu=platform.processor() or platform.machine(), cores=os.cpu_count(),
                numba_threads=numba.get_num_threads(), numpy=np.__version__, numba=numba.__version__,
                note="steer INCLUDES context; fused = drives+neighbours+quorum+steer+integrate in one numba parallel-for; ms_step includes the shared field update (40^3 x 5 channels) and the arena's own step is excluded")
    json.dump(dict(meta=meta, rows=rows), open(os.path.join(HERE, "bench.json"), "w"), indent=1)
