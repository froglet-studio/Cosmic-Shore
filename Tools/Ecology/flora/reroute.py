"""Re-route experiment (plant-specific metric): at t_cut, CUT everything the species has inside a ball of radius r
centred on its densest latent-threat region, then measure how the threat comes back.

    python reroute.py [species ...]  -> results/reroute.json

Reported per species (3 seeds, a wanderer flying meanwhile, params = searched best when present):
    pre          latent threat elements inside the ball just before the cut
    t50, t80     seconds until the ball holds 50% / 80% of `pre` again (None = not within the run)
    end_frac     fraction of `pre` at the end of the run
"""
import json, os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
from harness import FloraArena, resolve
from common.arena import Pilot
from search import SPEC


def one(name, params, seed, t_cut=60.0, after=90.0, r=120.0):
    ar = FloraArena(seed=seed); ar.grove_mass(1600, clumps=20)
    sp = resolve(SPEC[name])(ar, params); ar.species = sp
    pl = ar.add_pilot(Pilot.wanderer()); pl.prev = pl.pos.copy()
    m0 = ar.live_volume() + sp.mass_total() + sp.cut_volume
    dt = 0.1; series = []; c = None; pre = None
    for k in range(int((t_cut + after) / dt)):
        sp.step(ar, dt); ar.step(dt)
        if c is None and ar.t >= t_cut:
            T, _ = sp.threat_elements()
            if not len(T): return dict(pre=0)
            dens = [(np.linalg.norm(T - q, axis=1) < r).sum() for q in T[:: max(1, len(T) // 300)]]
            c = T[:: max(1, len(T) // 300)][int(np.argmax(dens))].copy()
            pre = int(np.max(dens)); sp.remove_ball(ar, c, r)
        elif c is not None and k % 5 == 0:
            T, _ = sp.threat_elements()
            series.append((round(ar.t - t_cut, 1), int((np.linalg.norm(T - c, axis=1) < r).sum()) if len(T) else 0))
    m1 = ar.live_volume() + sp.mass_total() + sp.cut_volume - ar.created
    def first(f):
        for t, n in series:
            if n >= f * pre: return t
        return None
    return dict(pre=pre, t50=first(0.5), t80=first(0.8), end_frac=round(series[-1][1] / max(pre, 1), 3),
                drift=(m1 - m0) / m0, series=series[::4])


if __name__ == "__main__":
    names = sys.argv[1:] or list(SPEC)
    out_p = os.path.join(HERE, "results", "reroute.json")
    res = json.load(open(out_p)) if os.path.exists(out_p) else {}
    for name in names:
        bp = os.path.join(HERE, "results", f"search_{name}_best.json")
        params = json.load(open(bp))["params"] if os.path.exists(bp) else {}
        runs = [one(name, params, s) for s in (7, 23, 41)]
        res[name] = dict(params=params, runs=runs,
                         t50_med=float(np.median([x["t50"] if x.get("t50") is not None else 999 for x in runs])),
                         t80_med=float(np.median([x["t80"] if x.get("t80") is not None else 999 for x in runs])),
                         end_frac_med=float(np.median([x.get("end_frac", 0) for x in runs])))
        print(name, {k: res[name][k] for k in ("t50_med", "t80_med", "end_frac_med")}, [x.get("pre") for x in runs], flush=True)
        json.dump(res, open(out_p, "w"), indent=1)
