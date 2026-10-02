"""Experiments for the living cell (Direction G, Part 1). From Tools/Ecology:

    python3 -m living_cell.run baseline      # the default cell, several seeds
    python3 -m living_cell.run controls      # every metric's negative control (each must FIRE)
    python3 -m living_cell.run iterate       # composition / dial rounds (results/iterate.json, every run kept)
    python3 -m living_cell.run consistency   # macro vs micro on the same cell (what the one-cohort LOD costs)

A run: build the cell, two pilots (an EXPLORER touring the cell and a WANDERER) fly the whole time, laying
trail; after `burn` seconds each pilot's time is cut into consecutive 5-minute FLIGHTS that are scored.
"""
from __future__ import annotations

import json
import os
import sys
import time
from multiprocessing import Pool

import numpy as np

from .cell import Cell, DEFAULT
from .metrics import EcoRecorder, Flight, eco_metrics, continuity_metrics, replay_distance

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "results")


def run_cell(seed=1, cfg=None, minutes=30.0, pilots=("explore", "wander"), burn=600.0, flight_s=300.0, emo=True,
             bug="", dt=0.1, keep_series=True):
    t_wall = time.time()
    c = Cell(seed=seed, cfg=cfg, pilots=pilots, bug=bug)
    eco = EcoRecorder(every=5.0)
    flights, cur = [], []
    n = int(minutes * 60 / dt)
    for i in range(n):
        c.step(dt)
        eco.update(c)
        w = c.w
        if w.t >= burn and pilots:
            if not cur or cur[0].done():
                if cur:
                    flights += [(f.k, f.summary(emo=emo)) for f in cur]
                if w.t + flight_s <= minutes * 60 + 1e-6:
                    cur = [Flight(c, k, t0=w.t, seconds=flight_s) for k in range(len(pilots))]
                else:
                    cur = []
            for f in cur:
                f.update()
    if cur and cur[0].done():
        flights += [(f.k, f.summary(emo=emo)) for f in cur]
    em = eco_metrics(eco.rows, c, burn=min(burn, 300.0))
    cost = np.array(c.cost)
    out = dict(seed=seed, minutes=minutes, cfg={k: v for k, v in (cfg or {}).items()}, bug=bug,
               eco=em, continuity=continuity_metrics(c.w),
               flights=[dict(pilot=pilots[k], **{kk: vv for kk, vv in f.items() if kk != "events" or True}) for k, f in flights],
               cost_ms=dict(mean=round(float(cost.mean() * 1000), 2), p95=round(float(np.percentile(cost, 95) * 1000), 2)),
               stats=dict(rams=c.rams, pilot_kills=c.pilot_kills, crystals=c.w.crystals, shield_refusals=c.w.shield_refusals,
                          expands={k: g.expands for k, g in c.guilds.items()}, absorbs={k: g.absorbs for k, g in c.guilds.items()},
                          prey_kills={k: getattr(g, "prey_kills", 0) for k, g in c.guilds.items()},
                          births={k: g.births for k, g in c.guilds.items()}, starved={k: g.starved for k, g in c.guilds.items()},
                          eaten_by={k: round(v, 1) for k, v in c.w.eaten_by.items()},
                          thief=dict(steals=getattr(c.guilds.get("thief"), "steals", 0), hoarded=getattr(c.guilds.get("thief"), "hoarded", 0),
                                     larder=round(getattr(c.guilds.get("thief"), "larder", 0.0), 1)),
                          fortress=None if not c.fortress else dict(placed=c.fortress.placed, placed_trail=c.fortress.placed_trail,
                                                                     from_hoard=c.fortress.from_hoard, cuts=c.fortress.cuts,
                                                                     repairs=c.fortress.repairs, fill=round(c.fortress.shell_fill(), 3),
                                                                     workers=c.fortress.count(), births=c.fortress.births, starved=c.fortress.starved),
                          traps=None if not c.traps else dict(catches=c.traps.catches, snaps_pilot=c.traps.snaps_pilot, fired=c.traps.fired,
                                                               buds=c.traps.buds, n=len(c.traps.heart)),
                          physarum=None if not c.phys else dict(digested=round(c.phys.digested, 1), tubes=c.phys.n_tubes(),
                                                                 burns=c.phys.burns, reserve=round(c.phys.reserve, 1))),
               wall_s=round(time.time() - t_wall, 1))
    if keep_series:
        out["series"] = eco.rows
    return out


def _job(a):
    return run_cell(**a)


def pool_map(jobs, procs=4):
    with Pool(min(procs, len(jobs))) as p:
        return p.map(_job, jobs)


def flight_table(runs):
    F = [f for r in runs for f in r["flights"]]
    if not F:
        return {}
    m = lambda k: round(float(np.mean([f.get(k, 0) for f in F])), 3)
    out = {k: m(k) for k in ("enc_per_min", "variety", "variety_entropy_bits", "quiet_frac", "hits_per_min", "steals_per_min",
                             "emotion_distinct", "emotion_entropy_bits", "threat_range", "threat_peak", "active_encounters")}
    out["n_flights"] = len(F)
    # replayability: every pair of flights by the same pilot policy from DIFFERENT seeds
    d = []
    for i in range(len(runs)):
        for j in range(i + 1, len(runs)):
            for fa in runs[i]["flights"]:
                for fb in runs[j]["flights"]:
                    if fa["pilot"] == fb["pilot"]:
                        d.append(replay_distance(fa, fb))
    if d:
        out["replay_jaccard"] = round(float(np.mean([x["jaccard_dist"] for x in d])), 3)
        out["replay_species_tv"] = round(float(np.mean([x["species_tv"] for x in d])), 3)
        out["replay_emotion_tv"] = round(float(np.mean([x["emotion_tv"] for x in d])), 3)
    by = {}
    for f in F:
        for s, k in f["enc_by_species"].items():
            by[s] = by.get(s, 0) + k
    out["enc_by_species_total"] = by
    return out


def eco_table(runs):
    E = [r["eco"] for r in runs]
    return dict(shannon_mean=round(float(np.mean([e["shannon_mean"] for e in E])), 3),
                shannon_min=round(float(np.min([e["shannon_min"] for e in E])), 3),
                n_extinct=int(sum(e["persistence"]["n_extinct"] for e in E)),
                n_unrecovered=int(sum(e["persistence"]["n_unrecovered"] for e in E)),
                extinct=[list(e["persistence"]["extinct"]) for e in E],
                freeze_frac=round(float(np.mean([e["freeze_frac"] for e in E])), 3),
                flora_saturated=round(float(np.mean([e["flora_saturated_frac"] for e in E])), 3),
                reversals=[e["reversals"] for e in E],
                cv={k: round(float(np.mean([e["cv"].get(k, 0) for e in E])), 3) for k in E[0]["cv"]},
                audit_max=float(max(e["audit_max"] for e in E)), shield_eaten=int(sum(e["shield_eaten"] for e in E)),
                pop_ins=int(sum(r["continuity"]["pop_ins"] for r in runs)), pop_outs=int(sum(r["continuity"]["pop_outs"] for r in runs)),
                cost_ms=round(float(np.mean([r["cost_ms"]["mean"] for r in runs])), 2),
                cost_p95=round(float(np.max([r["cost_ms"]["p95"] for r in runs])), 2))


def save(name, obj):
    os.makedirs(RES, exist_ok=True)
    with open(os.path.join(RES, name), "w") as fh:
        json.dump(obj, fh, indent=1, default=lambda o: o.tolist() if hasattr(o, "tolist") else str(o))


def summarise(label, runs):
    s = dict(label=label, eco=eco_table(runs), player=flight_table(runs))
    print(json.dumps(s, indent=None)[:3000])
    return s


def baseline(seeds=(1, 2, 3, 4), minutes=30.0, cfg=None, label="baseline"):
    runs = pool_map([dict(seed=s, cfg=cfg, minutes=minutes) for s in seeds])
    s = summarise(label, runs)
    save(f"{label}.json", dict(summary=s, runs=runs))
    return s, runs


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "baseline"
    if what == "baseline":
        mins = float(sys.argv[2]) if len(sys.argv) > 2 else 30.0
        baseline(minutes=mins)
