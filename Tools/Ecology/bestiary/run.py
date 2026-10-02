"""Score the bestiary with the shared threat/feel scorecard (common/scorecard.py).

    python Tools/Ecology/bestiary/run.py                 # every species, 4 pilots x 3 seeds -> scorecards.json
    python Tools/Ecology/bestiary/run.py pack leech      # just these (merged into scorecards.json)
    python Tools/Ecology/bestiary/run.py --seeds 7 23 41 101 --minutes 2

One run = one species x one pilot policy x one seed in the shared Arena with pilot TRAILS on (the game always
has them, and two species eat or steal them). The shared `combine` gives counterplay / telegraph / payoff /
variety / feel; this runner adds what the bestiary needs on top:
    hits_per_min_skimmer   the farming player (the shared combine ignores it)
    hits_by_kind           bite / burn / drain / steal / eat, per policy
    conservation           max |ledger drift| over the run (arena live volume + species-held - start - trail laid)
    ms_per_step, agents    numpy cost (one thread) and the mean live population
    phase                  species-specific phase stats (e.g. the locust's gregarious fraction)
"""
from __future__ import annotations

import argparse
import importlib
import json
import os
import sys
import time
from multiprocessing import Pool

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
sys.path.insert(0, HERE)

from common.arena import Arena, Pilot  # noqa: E402
from common.scorecard import Probe, combine, run_score  # noqa: E402
from core import ScoreView  # noqa: E402

SPECIES = ["pack", "leech", "locust", "stampede", "lurker", "leviathan", "thief", "mobber"]
POLICIES = {"wander": Pilot.wanderer, "evader": Pilot.evader, "hunter": Pilot.hunter, "skimmer": Pilot.skimmer,
            # the shared evader flies 140 u/s against the wanderer's 120, so `counterplay` mixes "fleeing" with
            # "being faster". This one flees at the wanderer's own speed: counterplay_same_speed is fleeing alone.
            "evader120": lambda: Pilot("evader", speed=120.0, name="evader120")}
DT = 0.1


def make_arena(seed, mass=3000):
    ar = Arena(seed=seed)
    ar.scatter_mass(mass)
    ar.enable_trails(spacing=15.0, vol=10.0)
    return ar


def one(args):
    key, policy, seed, minutes, rec_every = args
    mod = importlib.import_module(f"species.{key}")
    ar = make_arena(seed)
    sp = mod.make(ar)
    ar.add_pilot(POLICIES[policy]())
    view = ScoreView(sp)
    pr = Probe(DT)
    led0 = ar.live_volume() + sp.ledger()
    drift = 0.0
    nsteps = int(minutes * 60 / DT)
    tcost = 0.0
    pop = []
    rec = None
    if rec_every:
        from common.arena import Recorder
        rec = Recorder(every=rec_every)
    for _ in range(nsteps):
        t0 = time.perf_counter()
        sp.step(ar, DT)
        tcost += time.perf_counter() - t0
        ar.step(DT)
        pr.observe(ar, view)
        pop.append(int(sp.alive.sum()))
        drift = max(drift, abs(ar.live_volume() + sp.ledger() - led0 - ar.trail_laid))
        if rec:
            rec.frame(ar, [sp])
    r = run_score(ar, view, pr, minutes)
    kinds = {}
    for (_t, _n, kind, amt) in ar.log:
        kinds[kind] = kinds.get(kind, 0) + 1
    r.update(hits_by_kind=kinds, conservation=round(drift, 3), ms_per_step=round(1000 * tcost / nsteps, 3),
             agents=round(float(np.mean(pop)), 1), phase=getattr(sp, "phase_stats", lambda: {})())
    return (key, policy, seed), r, (rec, ar, sp) if rec else None


def score(keys, seeds, minutes, procs=4):
    jobs = [(k, p, s, minutes, 0) for k in keys for p in POLICIES for s in seeds]
    with Pool(procs) as pool:
        res = pool.map(one, jobs)
    out = {}
    for k in keys:
        allruns = {(p, s): r for (kk, p, s), r, _ in res if kk == k}
        runs = {ps: r for ps, r in allruns.items() if ps[0] != "evader120"}
        card = combine(runs, minutes)
        e120 = float(np.mean([r["hits_per_min"] for (p, s), r in allruns.items() if p == "evader120"]))
        card["hits_per_min_evader120"] = round(e120, 2)
        w = card["hits_per_min_wander"]
        card["counterplay_same_speed"] = round(e120 / w, 2) if w else None
        sk = [r["hits_per_min"] for (p, s), r in runs.items() if p == "skimmer"]
        card["hits_per_min_skimmer"] = round(float(np.mean(sk)), 2)
        card["hits_by_kind"] = {p: {} for p in POLICIES}
        for (p, s), r in allruns.items():
            for kind, c in r["hits_by_kind"].items():
                card["hits_by_kind"][p][kind] = round(card["hits_by_kind"][p].get(kind, 0) + c / len(seeds) / minutes, 2)
        card["crystals_per_min_hunter"] = round(float(np.mean(
            [r["crystals"] for (p, s), r in runs.items() if p == "hunter"])) / minutes, 2)
        card["conservation_max_drift"] = max(r["conservation"] for r in runs.values())
        card["ms_per_step"] = round(float(np.mean([r["ms_per_step"] for r in runs.values()])), 3)
        card["agents"] = round(float(np.mean([r["agents"] for r in runs.values()])), 1)
        card["us_per_agent_step"] = round(1000 * card["ms_per_step"] / max(card["agents"], 1), 2)
        ph = [r["phase"] for r in runs.values() if r["phase"]]
        if ph:
            card["phase"] = {k2: round(float(np.mean([x[k2] for x in ph if k2 in x])), 3) for k2 in ph[0]}
        mod = importlib.import_module(f"species.{k}")
        card["emotion"] = mod.EMOTION
        card["counter"] = mod.COUNTER
        card["minutes"], card["seeds"] = minutes, list(seeds)
        out[k] = card
    return out


def verdict(c):
    tel = c["telegraph_s"]; cp = c["counterplay"]; pay = c["payoff_per_min"]
    return dict(telegraph=tel is not None and tel >= 0.7, counterplay=cp is not None and cp < 1.0,
                variety=c["variety"] > 0, payoff=(pay or 0) > 0)


def feel_spread(cards):
    """Pairwise distance between species' feel vectors (each axis z-scored across species). The min pair says
    whether two species FEEL the same."""
    keys = list(cards)
    axes = ["size", "speed_rel", "approach", "coherence", "burstiness", "jerk_rel"]
    X = np.array([[cards[k]["feel"][a] for a in axes] for k in keys], float)
    X[:, 0] = np.log(np.maximum(X[:, 0], 0.1))
    Z = (X - X.mean(0)) / np.maximum(X.std(0), 1e-9)
    D = np.linalg.norm(Z[:, None] - Z[None], axis=2)
    np.fill_diagonal(D, np.inf)
    i, j = np.unravel_index(np.argmin(D), D.shape)
    return dict(min_pair=[keys[i], keys[j]], min_dist=round(float(D[i, j]), 2),
                mean_dist=round(float(D[np.isfinite(D)].mean()), 2))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("keys", nargs="*")
    ap.add_argument("--seeds", nargs="+", type=int, default=[7, 23, 41])
    ap.add_argument("--minutes", type=float, default=1.5)
    ap.add_argument("--out", default=os.path.join(HERE, "scorecards.json"))
    a = ap.parse_args()
    keys = a.keys or SPECIES
    t0 = time.time()
    cards = score(keys, a.seeds, a.minutes)
    allc = json.load(open(a.out)) if os.path.exists(a.out) else {}
    allc.setdefault("species", {}).update(cards)
    sp = {k: v for k, v in allc["species"].items() if k in SPECIES}
    if len(sp) >= 2:
        allc["feel_spread"] = feel_spread(sp)
    for k, c in cards.items():
        c["verdict"] = verdict(c)
    json.dump(allc, open(a.out, "w"), indent=1)
    for k, c in cards.items():
        print(f"{k:10s} tel {c['telegraph_s']}  cp {c['counterplay']} cp120 {c['counterplay_same_speed']} (w {c['hits_per_min_wander']} e {c['hits_per_min_evader']} s {c['hits_per_min_skimmer']})"
              f"  pay {c['payoff_per_min']}  var {c['variety']}  cons {c['conservation_max_drift']}  "
              f"{c['ms_per_step']}ms/{c['agents']}ag  {c['verdict']}")
        print("           feel", c["feel"], "kinds", c["hits_by_kind"], "phase", c.get("phase"))
    if "feel_spread" in allc:
        print("feel spread", allc["feel_spread"])
    print(f"{time.time() - t0:.0f}s")
