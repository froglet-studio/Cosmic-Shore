"""Fit the MACRO rates to the MICRO level (Direction E's calibrate.py idea, done as a trajectory fit).

The micro level is the truth (it is the same rules a pilot sees). `run.consistency` records the all-micro
trajectories (every region expanded, no pilots, no structures). Here the all-macro cell is re-run with
candidate macro dials and scored by the mean |log(macro/micro)| of every species' count (t >= 60 s, 3 seeds);
a coordinate search in log space keeps the best. Fitted dials (macro only - the micro rules never change):
    <herbivore>.F_half    the region food at half the maximum grazing rate (encounter efficiency)
    <predator>.a_attack   Holling II attack rate of the region's cohort on its prey cohorts
    <species>.hop         migration rate (how fast a cohort spreads to the food / prey it needs)

    python3 -m living_cell.calibrate CONS
"""
from __future__ import annotations

import json
import math
import os
import sys
from multiprocessing import Pool

import numpy as np

from .cell import Cell
from .run import RES, save

# Round 2: thieves are NOT fitted. The calibration runs have no pilots, so the micro thieves lose their real food
# (stolen trail) and starve; fitting the macro thief to that taught it to starve (thief.F_half 1200 in round 1,
# 9600 in the first round-2 fit). Thieves keep their hand-set F_half (300).
SKIP = ("thief",)
DIALS = [("grazer.F_half", 250.0), ("locust.F_half", 250.0), ("pack.a_attack", 6.0e-4), ("lurker.a_attack", 1.5e-4),
         ("grazer.hop", 0.003), ("locust.hop", 0.006), ("pack.hop", 0.004)]


def macro_traj(a):
    seed, cfg, minutes = a
    c = Cell(seed=seed, cfg=dict(cfg, traps=False, fortress=False, physarum=False), pilots=())
    rows = []
    for i in range(int(minutes * 60)):
        if i % 30 == 0:                      # aligned with run._consist's rows (every 300 ticks from t = 0)
            rows.append({k: v for k, v in c.census().items() if k in c.guilds})
        # all macro: skip the 0.1 s micro loop entirely (no agents exist without pilots)
        c.w.t += 1.0
        c.w.rebuild()
        c._macro(1.0)
    return rows


def error(micro, cfg, minutes, seeds):
    with Pool(len(seeds)) as p:
        mac = p.map(macro_traj, [(s, cfg, minutes) for s in seeds])
    err = []
    for sp in micro[0][0]:
        if sp in SKIP:
            continue
        for k in range(1, len(micro[0])):
            mi = np.mean([m[k][sp] for m in micro]); ma = np.mean([m[k][sp] for m in mac])
            err.append(abs(math.log((ma + 5) / (mi + 5))))
    return float(np.mean(err)), mac


def main(cfg_name="CONS", cons_file="consistency.json", out_file="calibrate.json"):
    from . import rounds
    base = dict(getattr(rounds, cfg_name))
    cons = json.load(open(os.path.join(RES, cons_file)))
    micro_runs = [x for x in cons["runs"] if x["mode"] == "micro"]
    seeds = [x["seed"] for x in micro_runs]
    minutes = len(micro_runs[0]["rows"]) * 0.5
    micro = [[r["census"] for r in x["rows"]] for x in micro_runs]
    cur = {k: v for k, v in DIALS}
    for k in cur:
        if k in base:
            cur[k] = base[k]
    best, _ = error(micro, dict(base, **cur), minutes, seeds)
    log = [dict(step=0, err=round(best, 4), dials=dict(cur))]
    print("start", round(best, 4))
    for sweep in range(3):
        for k, _ in DIALS:
            for f in (0.25, 0.5, 2.0, 4.0):
                cand = dict(cur); cand[k] = cur[k] * f
                e, _ = error(micro, dict(base, **cand), minutes, seeds)
                log.append(dict(step=len(log), dial=k, factor=f, err=round(e, 4)))
                if e < best - 1e-3:
                    best, cur = e, cand
                    print("  ", k, "x", f, "->", round(e, 4))
        print("sweep", sweep, round(best, 4))
    e, mac = error(micro, dict(base, **cur), minutes, seeds)
    gap = {}
    for sp in micro[0][0]:
        mi = np.mean([m[-1][sp] for m in micro]); ma = np.mean([m[-1][sp] for m in mac])
        gap[sp] = dict(micro_end=round(float(mi), 1), macro_end=round(float(ma), 1), rel=round(float(abs(ma - mi) / max(mi, 1)), 3))
    out = dict(cfg=cfg_name, fitted={k: float(v) for k, v in cur.items()}, err_start=log[0]["err"], err_end=round(best, 4),
               gap_end=gap, log=log)
    save(out_file, out)
    print(json.dumps({k: v for k, v in out.items() if k != "log"}, indent=1))
    return out


if __name__ == "__main__":
    main(*(sys.argv[1:4] if len(sys.argv) > 1 else ["CONS"]))
