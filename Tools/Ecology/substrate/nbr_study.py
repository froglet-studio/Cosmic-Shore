"""Cell MOMENTS vs EXACT pairwise neighbours. The substrate reads its neighbours as per-cell moments (count,
centroid, mean velocity) summed over the 27 surrounding cells - O(1) per agent, no pair loop. Is that
approximation changing behaviour, and what would exactness cost?

  behaviour: grazer school, dense-hungry locusts, sparse-fed locusts (numba backend, 2 seeds): scorecard feel,
             encounter, hits, quorum phase
  cost:      the neighbour stage alone at N = 10k / 100k (uniform cloud, k = 1)

    python -m substrate.nbr_study   -> results/nbr.json
"""
import json, os, sys, time
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from dataclasses import replace
from common.arena import Arena, Pilot
from common.scorecard import Probe, run_score
from substrate.core import Substrate
from substrate.metrics import Encounter
from substrate import species as S
from substrate import kernels_nb as knb

HERE = os.path.dirname(__file__)


def behave(cond, exact, seed, minutes=1.0, dt=0.1):
    ar = Arena(seed=seed)
    if cond == "grazer":
        ar.scatter_mass(2500); P = S.grazer(seed=seed); kw = {}
    elif cond == "locust_dense_hungry":
        ar.scatter_mass(600); P = S.locust(n0=600, seed=seed); kw = dict(spread=70, init_hunger=0.85)
    else:
        ar.scatter_mass(5000, vol=(20, 60)); P = S.locust(n0=60, seed=seed); kw = dict(spread=500, init_hunger=0.1)
    P = replace(P, nbr_exact=exact)
    pl = ar.add_pilot(Pilot.wanderer())
    sp = Substrate(ar, P, backend="numba", **kw)
    pl.pos = sp.home + np.array([300.0, 0, 0]); pl.goal = sp.home.copy()
    pr = Probe(dt); enc = Encounter()
    for _ in range(int(minutes * 60 / dt)):
        sp.step(ar, dt); ar.step(dt); pr.observe(ar, sp); enc.observe(ar, sp)
    r = run_score(ar, sp, pr, minutes)
    return dict(hits=r["hits_per_min"], feel=r["feel"], enc=enc.summary(),
                phase=round(float(sp.phase[sp.alive].mean()), 3))


def cost(N, exact, reps=5):
    rng = np.random.default_rng(1)
    pos = rng.normal(0, 360, (N, 3)); vel = rng.normal(0, 30, (N, 3)); ph = rng.random(N)
    sl = np.arange(N, dtype=np.int64)
    f = (lambda: knb.neighbours_exact(pos, vel, ph, sl, 30.0, 1200.0, 5.0)) if exact else \
        (lambda: knb.neighbours(pos, vel, ph, sl, 30.0, 1200.0))
    f()
    t = time.perf_counter()
    for _ in range(reps):
        o = f()
    return round((time.perf_counter() - t) / reps * 1000, 2), float(np.mean(o["count"]))


if __name__ == "__main__":
    out = dict(behaviour=[], cost=[])
    for cond in ("grazer", "locust_dense_hungry", "locust_sparse_fed"):
        for exact in (False, True):
            rs = [behave(cond, exact, s) for s in (7, 23)]
            row = dict(cond=cond, exact=exact, hits=float(np.mean([r["hits"] for r in rs])),
                       phase=float(np.mean([r["phase"] for r in rs])),
                       **{k: float(np.mean([r["feel"][k] for r in rs])) for k in ("speed_rel", "coherence", "jerk_rel")},
                       **{k: float(np.mean([r["enc"][k] or 0 for r in rs])) for k in ("heading_to_pilot", "near_frac")})
            row = {k: (round(v, 3) if isinstance(v, float) else v) for k, v in row.items()}
            out["behaviour"].append(row); print(json.dumps(row))
    for N in (10000, 100000):
        for exact in (False, True):
            ms, mc = cost(N, exact)
            out["cost"].append(dict(N=N, exact=exact, ms=ms, mean_neighbours=round(mc, 1))); print(out["cost"][-1])
    json.dump(out, open(os.path.join(HERE, "results", "nbr.json"), "w"), indent=1)
