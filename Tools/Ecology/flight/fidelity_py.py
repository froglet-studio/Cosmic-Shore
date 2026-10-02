"""The PYTHON half of the flight fidelity gate: run every species the flight page ports, in the exact world its own
direction scored it in, against the scripted pilots, and record per-run statistics the JS port must reproduce.

    python Tools/Ecology/flight/fidelity_py.py                       # all species, 6 seeds x 1.5 min
    python Tools/Ecology/flight/fidelity_py.py pack thief --seeds 7 23

Worlds (each the species' own direction's harness, untouched):
  bestiary   bestiary/run.py's BArena: 3000 scattered prisms, pilot trails every 15 u (vol 10); pilots wander /
             evader / hunter. (pack, thief, locust, lurker, stampede, leviathan, mobber, and flight's grazer)
  builders   builders/harness.run_one: 1500 prisms, a trail prism every 0.25 s; the hunter RAMS structure and
             workers. (fortress)
  flora      flora/harness.FloraArena with no omni crystals (the JS port has no crystal-diversion model): the grove
             ball, 1600 grove prisms, trail every 24 u; pilots wander / reader / cutter. (snaptrap, searched params)

Per run: hits/min, hits by kind, kills (crystals)/min, the shared Probe telegraph, the bestiary's first-strike
telegraph (bestiary/run.py TelProbe), and the six shared feel axes. results/fidelity_py.json.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from multiprocessing import Pool

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ECO = os.path.dirname(HERE)
sys.path.insert(0, ECO)
sys.path.insert(0, os.path.join(ECO, "bestiary"))

WORLD = dict(pack="bestiary", thief="bestiary", locust="bestiary", lurker="bestiary", stampede="bestiary",
             leviathan="bestiary", mobber="bestiary", grazer="bestiary", fortress="builders", snaptrap="flora")
POLICIES = dict(bestiary=("wander", "evader", "hunter"), builders=("wander", "evader", "hunter"),
                flora=("wander", "reader", "cutter"))
DT = 0.1
FEEL = ("size", "speed_rel", "approach", "coherence", "burstiness", "jerk_rel")


def _make(key, ar, seed):
    if key == "grazer":
        sys.path.insert(0, HERE)
        import grazer
        return grazer.make(ar)
    if key == "fortress":
        from builders.fortress import Fortress
        return Fortress(ar, seed=seed)
    if key == "snaptrap":
        sys.path.insert(0, os.path.join(ECO, "flora"))
        import snaptrap
        p = json.load(open(os.path.join(HERE, "params.json")))["species"]["snaptrap"]["extra"]["params"]
        return snaptrap.SnapTrap(ar, p)
    import importlib
    return importlib.import_module(f"species.{key}").make(ar)


def one(args):
    key, policy, seed, minutes = args
    from run import TelProbe, BArena          # bestiary/run.py
    from common.arena import Pilot
    from common.scorecard import run_score
    from core import ScoreView                # bestiary/core.py
    world = WORLD[key]
    if world == "bestiary":
        ar = BArena(seed=seed); ar.scatter_mass(3000); ar.enable_trails(spacing=15.0, vol=10.0)
        sp = _make(key, ar, seed)
        ar.add_pilot(dict(wander=Pilot.wanderer, evader=Pilot.evader, hunter=Pilot.hunter)[policy]())
        view = ScoreView(sp)
    elif world == "builders":
        from builders.harness import make_pilot, ram
        from common.arena import Arena
        ar = Arena(seed=seed); ar.scatter_mass(1500); ar.struct_owner = {}
        make_pilot(policy, ar)
        sp = _make(key, ar, seed); view = sp
    else:
        sys.path.insert(0, os.path.join(ECO, "flora"))
        from harness import FloraArena
        ar = FloraArena(seed=seed, trails=True, n_crystals=0); ar.grove_mass(1600, clumps=20)
        sp = _make(key, ar, seed); ar.species = sp
        mk = dict(wander=Pilot.wanderer, reader=lambda: Pilot("reader", speed=120.0, name="reader"),
                  cutter=lambda: Pilot("cutter", speed=140.0, name="cutter"))[policy]
        pilot = ar.add_pilot(mk()); pilot.prev = pilot.pos.copy()
        view = sp
    pr = TelProbe(DT)
    steps = int(minutes * 60 / DT)
    t0 = time.perf_counter()
    for _ in range(steps):
        sp.step(ar, DT)
        if world == "builders":
            ram(ar, [sp], DT)
        if world == "flora" and policy == "cutter":
            p = ar.pilots[0]; sp.cut(ar, p, p.prev, p.pos)
        ar.step(DT)
        pr.observe(ar, view)
    ms = (time.perf_counter() - t0) / steps * 1000
    r = run_score(ar, view, pr, minutes)
    kinds = {}
    for (_t, _n, kd, _a) in ar.log:
        kinds[kd] = kinds.get(kd, 0) + 1
    feel = {k: r["feel"].get(k) for k in FEEL}
    return dict(species=key, policy=policy, seed=seed, minutes=minutes, hits_per_min=r["hits_per_min"],
                kinds=kinds, crystals_per_min=round(getattr(sp, "crystals", 0) / minutes, 3),
                kills_per_min=r["kills_per_min"], telegraph_s=r["telegraph_s"],
                leads=[round(x, 2) for x in pr.leads], first_leads=[round(x, 2) for x in pr.first_leads(ar)],
                feel=feel, ms_per_step=round(ms, 3))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("keys", nargs="*")
    ap.add_argument("--seeds", nargs="+", type=int, default=[7, 23, 41, 101, 202, 303])
    ap.add_argument("--minutes", type=float, default=1.5)
    ap.add_argument("--procs", type=int, default=4)
    ap.add_argument("--out", default=os.path.join(HERE, "results", "fidelity_py.json"))
    a = ap.parse_args()
    keys = a.keys or list(WORLD)
    jobs = [(k, p, s, a.minutes) for k in keys for p in POLICIES[WORLD[k]] for s in a.seeds]
    t0 = time.time()
    with Pool(a.procs) as pool:
        runs = pool.map(one, jobs, chunksize=1)
    old = json.load(open(a.out)) if os.path.exists(a.out) else {"runs": []}
    keep = [r for r in old["runs"] if r["species"] not in keys]
    json.dump(dict(dt=DT, minutes=a.minutes, seeds=a.seeds, worlds=WORLD, runs=keep + runs), open(a.out, "w"))
    for k in keys:
        for p in POLICIES[WORLD[k]]:
            rr = [r for r in runs if r["species"] == k and r["policy"] == p]
            print(f"{k:10s} {p:7s} hits/min {np.mean([r['hits_per_min'] for r in rr]):6.2f}  "
                  f"kills/min {np.mean([r['crystals_per_min'] for r in rr]):6.2f}  "
                  f"ms/step {np.mean([r['ms_per_step'] for r in rr]):.2f}")
    print(f"{time.time() - t0:.0f}s")
