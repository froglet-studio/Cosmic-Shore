"""Fractional update in the substrate: does re-steering only 1/k of agents per step change behaviour, and
does it smooth motion? (The swarm program found frac 8 both cheaper AND smoother for sortfeel.)

For k in 1,2,4,8,16, fixed population (no births/starvation, so agent indexing is stable for jerk):
  jerk_rel, burstiness, coherence (scorecard feel), heading_to_pilot / near_frac (encounter), hits/min,
  and the kernel's ms/step at that k. Two conditions: grazer school (calm) and dense hungry locusts (frenzy).

    python -m substrate.frac_study    -> results/frac.json
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

HERE = os.path.dirname(__file__)


def turn_rate(sp_hist, dt):
    """mean |heading change| per second over the run (deg/s) - a direct smoothness read."""
    return None


def run(cond, k, seed, minutes=1.0, dt=0.1, attn=False):
    ar = Arena(seed=seed)
    if cond == "grazer":
        ar.scatter_mass(2500); P = S.grazer(seed=seed); kw = {}
    else:
        ar.scatter_mass(800); P = S.locust(n0=500, seed=seed); kw = dict(spread=80, init_hunger=0.85)
    P = replace(P, frac_k=k, birth_stock=1e12, starve_s=1e12, attn_r=150.0 if attn else 0.0,
                attn_urg=0.6 if attn else 1.0)
    pl = ar.add_pilot(Pilot.wanderer())
    sp = Substrate(ar, P, **kw)
    pl.pos = sp.home + np.array([300.0, 0, 0]); pl.goal = sp.home.copy()
    pr = Probe(dt); enc = Encounter(); prevh = None; turns = []
    t = time.perf_counter()
    for _ in range(int(minutes * 60 / dt)):
        sp.step(ar, dt); ar.step(dt); pr.observe(ar, sp); enc.observe(ar, sp)
        v = sp.vel[sp.alive]; h = v / np.maximum(np.linalg.norm(v, axis=1, keepdims=True), 1e-9)
        if prevh is not None and len(h) == len(prevh):
            turns.append(float(np.degrees(np.arccos(np.clip(np.sum(h * prevh, 1), -1, 1))).mean() / dt))
        prevh = h
    wall = (time.perf_counter() - t) / (minutes * 600)
    r = run_score(ar, sp, pr, minutes); r.pop("hit_times")
    return dict(k=k, cond=cond, seed=seed, hits_per_min=r["hits_per_min"], feel=r["feel"], enc=enc.summary(),
                turn_deg_s=round(float(np.mean(turns)), 2), turn_p95=round(float(np.percentile(turns, 95)), 2),
                ms_step=round(wall * 1000, 2))


if __name__ == "__main__":
    rows = []
    for cond, attn in (("grazer", False), ("locust_frenzy", False), ("grazer", True), ("locust_frenzy", True)):
        for k in (1, 2, 4, 8, 16):
            rs = [run(cond, k, s, attn=attn) for s in (7, 23)]
            agg = dict(cond=cond, attn=attn, k=k, hits_per_min=np.mean([r["hits_per_min"] for r in rs]),
                       jerk_rel=np.mean([r["feel"]["jerk_rel"] for r in rs]),
                       coherence=np.mean([r["feel"]["coherence"] for r in rs]),
                       burstiness=np.mean([r["feel"]["burstiness"] for r in rs]),
                       heading_to_pilot=np.mean([r["enc"]["heading_to_pilot"] or 0 for r in rs]),
                       near_frac=np.mean([r["enc"]["near_frac"] for r in rs]),
                       turn_deg_s=np.mean([r["turn_deg_s"] for r in rs]), turn_p95=np.mean([r["turn_p95"] for r in rs]),
                       ms_step=np.mean([r["ms_step"] for r in rs]))
            agg = {k2: (round(float(v), 3) if isinstance(v, (float, np.floating)) else v) for k2, v in agg.items()}
            rows.append(agg); print(json.dumps(agg))
    json.dump(rows, open(os.path.join(HERE, "results", "frac.json"), "w"), indent=1)
