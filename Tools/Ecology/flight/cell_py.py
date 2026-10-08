"""The PYTHON reference for the flight page's WHOLE CELL: every ported species in ONE arena, as the page composes
them (src/50_worlds.js FlightWorld 'cell'), with a scripted pilot in place of the player. The per-species gate
(fidelity_py.py) scores each species alone in its own harness; this scores what only exists when they share a cell:
who eats, destroys, steals and hauls whose mass, how populations hold up next to each other, and who hits the pilot.

    python Tools/Ecology/flight/cell_py.py                      # 'cell' composition, both pilots, 6 seeds x 3 min
    python Tools/Ecology/flight/cell_py.py --drop locust        # the same cell without the locusts (an ablation)
    python Tools/Ecology/flight/cell_py.py --pop scored         # scored populations (grazers 120 / cap 240)

The world mirrors FlightWorld: the bestiary arena (BArena), 4200 scattered prisms, pilot trails every 15 u at vol 10
booked as the PILOT's wake (domain 1, trail flag), the fortress anchored at (0.38, 0.1, -0.25) R, the snap-trap
clumps in the 0.3-0.75 R band. Every rule is the species' own Python; only placement and populations are the page's.
Pilots: `wander` and `hunter` (the hunter also rams fortress structure and workers, as the page's player does).

Ledger (identical in cell_js.js): every removal / steal / haul is booked to the species whose step made it (`src`)
and to the CREATOR of the prism it touched: env (scattered), wake (the pilot's trail), or the species that laid it.
results/cell_py*.json.
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
for p in (ECO, os.path.join(ECO, "bestiary"), os.path.join(ECO, "flora"), HERE):
    if p not in sys.path:
        sys.path.insert(0, p)

DT = 0.1
ORDER = ("grazer", "locust", "stampede", "mobber", "thief", "pack", "lurker", "leviathan", "fortress", "snaptrap")
SAMPLE_EVERY = 10          # population sampled every k steps (1 s at DT 0.1)


def _arena_cls():
    from run import BArena                     # bestiary/run.py

    class FlightArena(BArena):
        """BArena as the flight page uses it: pilot trails are the pilot's wake (domain 1, trail flag) and the
        snap-trap's grove() is the page's clump band. The ledger hooks book every mass event to `src`."""

        def __init__(self, seed):
            super().__init__(seed=seed)
            self.src = "arena"
            self.creator: list = []            # prism index -> creator key
            self.book = dict(eat={}, destroy={}, steal={}, move={})
            self.hits_by: dict = {}

        def _who(self, i):
            i = int(i)
            return self.creator[i] if i < len(self.creator) else "env"

        def _tally(self, kind, i, v):
            if v <= 0:
                return
            d = self.book[kind].setdefault(self.src, {}); w = self._who(i)
            d[w] = d.get(w, 0.0) + v

        def scatter_mass(self, n, *a, **k):
            super().scatter_mass(n, *a, **k)
            self.creator.extend(["env"] * (len(self.mass_vol) - len(self.creator)))

        def lay_mass(self, p, vol, elem=0, dom=0, danger=False, trail=False, shielded=False, owner=-1):
            if owner >= 0:                      # a pilot's trail: the player's wake on the page
                dom, trail = 1, True
            i = super().lay_mass(p, vol, elem=elem, dom=dom, danger=danger, trail=trail, shielded=shielded, owner=owner)
            self.creator.extend(["env"] * (i - len(self.creator)))
            self.creator.append("wake" if owner >= 0 else self.src)
            return i

        def consume(self, i, by=""):
            v = super().consume(i, by); self._tally("eat", i, v); return v

        def destroy(self, i, by=""):
            v = super().destroy(i, by); self._tally("destroy", i, v); return v

        def steal(self, i, dom, by=""):
            v = super().steal(i, dom, by); self._tally("steal", i, v); return v

        def move_mass(self, i, p):
            super().move_mass(i, p); self._tally("move", i, float(self.mass_vol[int(i)]))

        def hit(self, pilot, kind, amount=1.0):
            super().hit(pilot, kind, amount)
            self.hits_by[self.src] = self.hits_by.get(self.src, 0) + 1

        def grove(self, n, margin=0.0):
            return self._ball(n, 0.3 * self.R, 0.75 * self.R)

        def step(self, dt):
            # the page's arena (and FloraArena) record each pilot's previous position every step; the snap trap's
            # ram / burn contacts are swept along prev -> pos, so a stale prev would sweep the whole flight path
            for p in self.pilots:
                p.prev = p.pos.copy()
            super().step(dt)

        def speed_factor(self, p):             # the page slows only the player (its flight model); pilots keep speed
            return 1.0

    return FlightArena


def _species(key, ar, seed, pop):
    if key == "grazer":
        import grazer
        if pop == "scored":
            return grazer.Grazer(ar)
        return grazer.Grazer(ar, n=900, cap=1600, clusters=10)
    if key == "fortress":
        from builders.fortress import Fortress
        R = ar.R
        return Fortress(ar, seed=seed, anchor=np.array([0.38 * R, 0.1 * R, -0.25 * R]))
    if key == "snaptrap":
        import snaptrap
        p = json.load(open(os.path.join(HERE, "params.json")))["species"]["snaptrap"]["extra"]["params"]
        return snaptrap.SnapTrap(ar, p)
    import importlib
    return importlib.import_module(f"species.{key}").make(ar)


def alive_count(sp):
    a = getattr(sp, "alive", None)
    return int(np.count_nonzero(a)) if a is not None else 0


def one(args):
    policy, seed, minutes, drop, pop = args
    from common.arena import Pilot
    from builders.harness import ram
    ar = _arena_cls()(seed)
    ar.scatter_mass(4200); ar.enable_trails(spacing=15.0, vol=10.0); ar.struct_owner = {}
    keys = [k for k in ORDER if k not in drop]
    sps = []
    for k in keys:
        ar.src = k
        sps.append(_species(k, ar, seed, pop))
    ar.src = "arena"
    pilot = ar.add_pilot(dict(wander=Pilot.wanderer, hunter=Pilot.hunter)[policy]())
    pilot.prev = pilot.pos.copy()         # the first step's sweep starts where the pilot is
    steps = int(round(minutes * 60 / DT))
    pops = {k: [] for k in keys}
    crys0 = {k: getattr(s, "crystals", 0) for k, s in zip(keys, sps)}
    fort = [s for k, s in zip(keys, sps) if k == "fortress"]
    t0 = time.perf_counter()
    for s_ in range(steps):
        for k, sp in zip(keys, sps):
            ar.src = k
            sp.step(ar, DT)
            if k == "fortress":
                ram(ar, fort, DT)
        ar.src = "arena"
        ar.step(DT)
        if s_ % SAMPLE_EVERY == 0:
            for k, sp in zip(keys, sps):
                pops[k].append(alive_count(sp))
    ms = (time.perf_counter() - t0) / steps * 1000
    per = lambda d: {a: {b: round(v / minutes, 2) for b, v in w.items()} for a, w in d.items()}
    return dict(policy=policy, seed=seed, minutes=minutes, drop=sorted(drop), pop=pop, species=keys,
                hits_by={k: round(v / minutes, 3) for k, v in ar.hits_by.items()},
                hits_per_min=round(len(ar.log) / minutes, 3),
                eat=per(ar.book["eat"]), destroy=per(ar.book["destroy"]), steal=per(ar.book["steal"]),
                move=per(ar.book["move"]),
                pop_mean={k: round(float(np.mean(v)), 2) for k, v in pops.items()},
                pop_end={k: v[-1] for k, v in pops.items()},
                pop_start={k: v[0] for k, v in pops.items()},
                crystals_per_min={k: round((getattr(s, "crystals", 0) - crys0[k]) / minutes, 3) for k, s in zip(keys, sps)},
                ms_per_step=round(ms, 2))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", nargs="+", type=int, default=[7, 23, 41, 101, 202, 303])
    ap.add_argument("--minutes", type=float, default=3.0)
    ap.add_argument("--policies", nargs="+", default=["wander", "hunter"])
    ap.add_argument("--drop", nargs="*", default=[])
    ap.add_argument("--pop", default="cell", choices=["cell", "scored"])
    ap.add_argument("--procs", type=int, default=4)
    ap.add_argument("--out", default=None)
    a = ap.parse_args()
    tag = ("_minus_" + "_".join(sorted(a.drop)) if a.drop else "") + ("_scored" if a.pop == "scored" else "")
    out = a.out or os.path.join(HERE, "results", f"cell_py{tag}.json")
    jobs = [(p, s, a.minutes, tuple(a.drop), a.pop) for p in a.policies for s in a.seeds]
    t0 = time.time()
    with Pool(a.procs) as pool:
        runs = []
        for r in pool.imap_unordered(one, jobs):
            runs.append(r)
            print(f"{r['policy']:7s} seed {r['seed']:4d}  hits/min {r['hits_per_min']:6.2f}  "
                  f"{r['ms_per_step']:7.1f} ms/step  ({time.time() - t0:.0f} s)", flush=True)
    runs.sort(key=lambda r: (r["policy"], r["seed"]))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    json.dump(dict(dt=DT, minutes=a.minutes, drop=sorted(a.drop), pop=a.pop, runs=runs), open(out, "w"), indent=1)
    print("wrote", out)
