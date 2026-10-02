"""Run a builder species against the shared pilots and score it: the threat/feel scorecard (common/scorecard.py)
plus the structure metrics Direction D owes (build rate, player-derived fraction, repair-after-cut, prism moves
per second, conservation audit, replayability across seeds).

Pilot abilities the arena does not model live here, as ACTIVE forces:
  ram      a hunter / cutter pilot destroys unshielded structure prisms it flies through (a vessel's prism
           collision) and kills workers it touches (crystal drop). Mass leaves only through this and eating.
"""
from __future__ import annotations

import json
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from common.arena import Arena, Pilot, Recorder          # noqa: E402
from common.scorecard import Probe, run_score, combine     # noqa: E402

from builders.core import structure_signature, shape_stats, components   # noqa: E402

OUT = os.path.join(HERE, "out")
TRAIL_EVERY = 0.25            # every pilot lays a conserved trail prism every 0.25 s (4/s, ~Squirrel cadence)


def make_pilot(policy, arena):
    mk = dict(wander=Pilot.wanderer, evader=Pilot.evader, hunter=Pilot.hunter, skimmer=Pilot.skimmer)
    if policy == "circuit":
        # a fixed race loop (the player who flies the same lanes)
        th = np.linspace(0, 2 * np.pi, 7)[:-1]
        wp = [np.array([600 * np.cos(a), 120 * np.sin(2 * a), 600 * np.sin(a)]) for a in th]
        p = Pilot.circuit(wp)
    elif policy == "varied":
        # the same loop, but the pilot re-draws its line every lap (counterplay probe for lane traps)
        th = np.linspace(0, 2 * np.pi, 7)[:-1]
        wp = [np.array([600 * np.cos(a), 120 * np.sin(2 * a), 600 * np.sin(a)]) for a in th]
        p = Pilot.circuit(wp, name="varied"); p.policy = "circuit"; p._varied = True; p._base = [w.copy() for w in wp]
    else:
        p = mk[policy]()
    p.trail_every = TRAIL_EVERY
    arena.add_pilot(p)
    if policy in ("circuit", "varied"):
        p.pos = p.waypoints[0] + np.array([0, 0, -80.0])
    return p


def vary_lines(arena):
    """Varied pilots jitter every waypoint by up to 150 u each time they complete a lap."""
    for p in arena.pilots:
        if getattr(p, "_varied", False) and p._wp == 0 and getattr(p, "_lap_wp", -1) != 0:
            p.waypoints = [b + arena.rng.uniform(-150, 150, 3) for b in p._base]
        if getattr(p, "_varied", False):
            p._lap_wp = p._wp


def ram(arena, colonies, dt, reach=4.0, policies=("hunter", "cutter")):
    """Ram-capable pilots destroy unshielded structure prisms and kill workers they touch."""
    for p in arena.pilots:
        if p.policy not in policies:
            continue
        for c in arena.mass_near(p.pos, p.radius + reach):
            if arena.mass_alive[c] and getattr(arena, "struct_owner", {}).get(int(c)):
                arena.destroy(int(c), by=p.name)
        for col in colonies:
            if not hasattr(col, "agent_pos"):
                continue
            d = np.linalg.norm(col.agent_pos - p.pos, axis=1)
            hit = col.alive & (d < p.radius + col.agent_size + 1.0)
            for k in np.flatnonzero(hit):
                col.on_rammed(arena, int(k), p)


def run_one(factory, policy, seed, minutes=2.0, dt=0.1, record=False, mass=1500, setup=None, label=""):
    ar = Arena(seed=seed)
    ar.scatter_mass(mass)
    ar.struct_owner = {}
    p = make_pilot(policy, ar)
    if setup:
        setup(ar, p)
    sp = factory(ar, seed)
    pr = Probe(dt); rec = Recorder(every=3) if record else None
    t0 = time.time(); steps = int(minutes * 60 / dt)
    hud = []
    for s in range(steps):
        sp.step(ar, dt)
        ram(ar, [sp], dt)
        vary_lines(ar)
        ar.step(dt)
        pr.observe(ar, sp)
        if rec:
            rec.frame(ar, [sp])
            if rec.k % rec.every == 0:
                hud.append(sp.hud(ar) if hasattr(sp, "hud") else "")
    wall = time.time() - t0
    r = run_score(ar, sp, pr, minutes)
    r["structure"] = sp.metrics(ar, minutes)
    r["audit"] = round(ar.audit(), 6)
    r["moves_per_s"] = round(ar.moves / (minutes * 60), 1)
    r["steals"] = ar.steals
    r["stolen_vol"] = round(ar.stolen, 1)
    r["wall_s"] = round(wall, 1)
    r["_sp"] = sp
    if rec:
        os.makedirs(OUT, exist_ok=True)
        path = os.path.join(OUT, f"{label or sp.name}_{policy}_{seed}.json")
        rec.save(path, dict(label=f"{label or sp.name} vs {policy} (seed {seed})", note=sp.note, hud=hud))
        r["recording"] = path
    return r


def replayability(sps):
    """Two seeds must build visibly different structures. Measured on rotation-invariant shape stats and on the
    raw occupancy of the anchor-centred grid (Jaccard distance; 0 = identical, 1 = disjoint)."""
    sigs = []
    for sp in sps:
        pts = sp.lat.occupancy_points()
        sigs.append((structure_signature(pts, sp.lat.anchor, bins=10, extent=sp.extent), shape_stats(pts, sp.lat.anchor),
                     components(sp.lat)))
    jac = []
    for i in range(len(sigs)):
        for j in range(i + 1, len(sigs)):
            a, b = sigs[i][0], sigs[j][0]
            u = (a | b).sum()
            jac.append(1 - (a & b).sum() / u if u else 0.0)
    return dict(jaccard_mean=round(float(np.mean(jac)), 3) if jac else None,
                shapes=[s[1] for s in sigs],
                components=[s[2][:6] for s in sigs])


def evaluate(factory, name, policies=("wander", "evader", "hunter"), seeds=(7, 23, 41), minutes=2.0, extra=None,
             record_seed=7):
    runs, structs, sps, recs = {}, {}, [], []
    for pol in policies:
        for sd in seeds:
            r = run_one(factory, pol, sd, minutes=minutes, record=(sd == record_seed), label=name)
            sp = r.pop("_sp")
            runs[(pol, sd)] = r
            structs[f"{pol}/{sd}"] = r["structure"] | dict(audit=r["audit"], moves_per_s=r["moves_per_s"],
                                                           steals=r["steals"], hits_per_min=r["hits_per_min"],
                                                           wall_s=r["wall_s"])
            if pol == "wander":
                sps.append(sp)
            if "recording" in r:
                recs.append(r["recording"])
            print(f"  {name} {pol:7s} seed {sd:3d}: hits/min {r['hits_per_min']:5.2f} "
                  f"built {r['structure'].get('built', 0):4d} audit {r['audit']:.3g} moves/s {r['moves_per_s']:6.1f} "
                  f"({r['wall_s']}s)", flush=True)
    card = combine({k: v for k, v in runs.items() if k[0] in ("wander", "evader", "hunter")}, minutes)
    card["replay"] = replayability(sps)
    card["structure"] = structs
    if extra:
        card.update(extra(runs))
    return card, recs


def save_card(name, card, recs, title):
    os.makedirs(OUT, exist_ok=True)
    def clean(o):
        if isinstance(o, dict):
            return {str(k): clean(v) for k, v in o.items()}
        if isinstance(o, (list, tuple)):
            return [clean(v) for v in o]
        if isinstance(o, (np.floating,)):
            return float(o)
        if isinstance(o, (np.integer,)):
            return int(o)
        return o
    res = os.path.join(HERE, "results"); os.makedirs(res, exist_ok=True)
    with open(os.path.join(res, f"{name}.json"), "w") as fh:
        json.dump(clean(card), fh, indent=1)
    if recs:
        from common.viewer import build
        build(os.path.join(OUT, f"{name}.html"), recs, title)
