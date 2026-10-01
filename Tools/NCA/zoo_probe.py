"""Player-facing BEHAVIOUR DESCRIPTORS for a zoo genome (the axes of the elite map).

Per body plan, grow 240 steps from the standard seed, then branch the grown swarm into five
experiments (each on its own copy of the swarm and the model's memory):

  idle     40 steps, no ship:          liveliness = mean tadpole speed (voxels/step),
                                       looseness  = mean distance from a tadpole to its home slot
                                       cost       = ms per step (numpy, this machine)
  loiter   a ship parks 1.2 RMS radii off the centroid, barely moving, 80 steps:
                                       crowd = mean share of the swarm within 2 ship radii
                                       stance = log2((crowd + eps) / (inert crowd + eps)): <0 flees, >0 mobs
  pass     a ship crosses the centroid at 3 voxels/step: touched = share ever inside it (low = parts)
  strike   swarm_probe.strike (a third removed): heal_steps = steps until the own-plan score is back
                                       within 1.2x + 0.5 of before (checked every 5, capped at 150)
  switch   swarm_eval.cull_to the standard switch target, 150 steps:
                                       drama = peak RMS radius / grown RMS radius during the morph,
                                       latency = steps until the new plan is closest (every 10)

Descriptors are averaged over the four plans. Nothing here is scored by the yardstick.
"""
from __future__ import annotations

import copy
import json
import math
import os
import sys
import time

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_eval as se  # noqa: E402
import swarm_probe  # noqa: E402
import zoo_model as zm  # noqa: E402

_T = None
EPS = 0.01


def targets():
    global _T
    if _T is None:
        _T = sn.load_targets()
    return _T


def _alive(sw):
    return (sw.active[0] & sw.hatched[0]).numpy()


def _rms(sw):
    p = sw.pos[0].numpy()[_alive(sw)]
    if len(p) < 2:
        return 0.0, np.zeros(3)
    c = p.mean(0)
    return float(np.sqrt(((p - c) ** 2).sum(-1).mean())), c


def _branch(model, sw):
    m2 = copy.copy(model)
    m2.mem = copy.deepcopy(model.mem)
    m2.predators = []; m2.timing = []
    return m2, sw.clone()


def _score(sw, k, L):
    return sn.swarm_loss(sn.decode(sw, 0), targets()[k], L)[1]["sink"]


@torch.no_grad()
def describe_plan(g, k, seed=5, L=None, inert=False):
    L = L or sn.LossCfg()
    T = targets()
    model = zm.make(g)
    if inert:
        model.cfg.flee = (0.0, 0.0, 0.0, 0.0); model.cfg.mob = (0.0, 0.0, 0.0, 0.0); model.cfg.hunt = 0.0
        model.cfg.curious = 0.0; model.cfg.relay = 0.0; model.cfg.sense = 0.01
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[k]], model.world, gen)
    for _ in range(240):
        sw = model(sw, gen)
    out = {}
    rms0, c0 = _rms(sw)
    before = _score(sw, k, L)
    out["n"] = int(_alive(sw).sum()); out["before"] = round(before, 2)
    # idle
    m, s = _branch(model, sw)
    spd, loose = [], []
    t0 = time.perf_counter()
    for _ in range(40):
        s = m(s, gen)
        al = _alive(s)
        S = s.s[0].numpy()
        spd.append(float(np.linalg.norm(S[al, 12:15], axis=-1).mean()))
        hs = S[al, zm.HOME].astype(int) - 1
        pl = m.plans[m.mem[0]["plan"]]
        sp, _, _ = m._at(m.mem[0], pl)
        ok = hs >= 0
        if ok.any():
            loose.append(float(np.linalg.norm(s.pos[0].numpy()[al][ok] - (sp[hs[ok]] + m.mem[0]["anchor"]), axis=-1).mean()))
    out["ms"] = round(1000 * (time.perf_counter() - t0) / 40, 2)
    out["live"] = round(float(np.mean(spd)), 3); out["loose"] = round(float(np.mean(loose)) if loose else 0.0, 3)
    # loiter
    m, s = _branch(model, sw)
    ship = c0 + np.array([1.2 * rms0, 0, 0]); rad = 0.3 * rms0
    crowd = []
    for t in range(80):
        m.predators = [(ship + np.array([0, 0, 0.3 * math.sin(t / 10)]), rad, np.array([0.0, 0.0, 0.3]))]
        s = m(s, gen)
        al = _alive(s)
        crowd.append(float((np.linalg.norm(s.pos[0].numpy()[al] - ship, axis=-1) < 2 * rad).mean()))
    out["crowd"] = round(float(np.mean(crowd[20:])), 4)
    if inert:
        return out
    # pass
    m, s = _branch(model, sw)
    rad = 0.6 * rms0
    rng = np.random.default_rng(seed)
    d = rng.normal(size=3); d /= np.linalg.norm(d)
    start = c0 - d * (rms0 * 2.5 + rad)
    span = int(2 * (rms0 * 2.5 + rad) / 3.0)
    touched = np.zeros(s.pos.shape[1], bool)
    for t in range(span):
        pc = start + d * 3.0 * t
        m.predators = [(pc, rad, d * 3.0)]
        al = _alive(s)
        touched |= al & (np.linalg.norm(s.pos[0].numpy() - pc, axis=-1) < rad)
        s = m(s, gen)
    out["touched"] = round(float(touched.sum()) / max(1, int(_alive(s).sum())), 3)
    # strike
    m, s = _branch(model, sw)
    swarm_probe.strike(s, gen=sn.make_gen(seed + 1))
    heal = 150
    for t in range(1, 151):
        s = m(s, gen)
        if t % 5 == 0 and _score(s, k, L) <= 1.2 * before + 0.5:
            heal = t
            break
    out["heal"] = heal
    # switch
    m, s = _branch(model, sw)
    e = sn.SWITCH_TO[k]; to = sn.PLAN_OF[e]
    se.cull_to(s, 0, e, sn.make_gen(seed + 2))
    peak, lat = 0.0, 150
    for t in range(1, 151):
        s = m(s, gen)
        r, _ = _rms(s)
        peak = max(peak, r)
        if lat == 150 and t % 10 == 0:
            x = sn.decode(s, 0)
            row = {k2: sn.swarm_loss(x, T[k2], L)[1]["sink"] for k2 in sn.KINDS}
            if min(row, key=row.get) == to:
                lat = t
    out["drama"] = round(peak / max(rms0, 1e-3), 3)
    out["latency"] = lat
    return out


_BASE = None


def inert_crowd():
    """The crowd share around a loitering ship when nothing reacts (field's body, reactions off), per plan."""
    global _BASE
    if _BASE is None:
        path = os.path.join(HERE, "results", "zoo", "inert_crowd.json")
        if os.path.exists(path):
            _BASE = json.load(open(path))
        else:
            _BASE = {k: describe_plan(zm.field_genome(), k, inert=True)["crowd"] for k in sn.KINDS}
            os.makedirs(os.path.dirname(path), exist_ok=True)
            json.dump(_BASE, open(path, "w"), indent=1)
    return _BASE


def describe(g):
    base = inert_crowd()
    per = {k: describe_plan(g, k) for k in sn.KINDS}
    avg = lambda f: float(np.mean([per[k][f] for k in sn.KINDS]))
    stance = float(np.mean([math.log2((per[k]["crowd"] + EPS) / (base[k] + EPS)) for k in sn.KINDS]))
    return dict(stance=round(stance, 3), live=round(avg("live"), 3), drama=round(avg("drama"), 3),
                heal=round(avg("heal"), 1), loose=round(avg("loose"), 3), touched=round(avg("touched"), 3),
                latency=round(avg("latency"), 1), ms=round(max(per[k]["ms"] for k in sn.KINDS), 2), per=per)


if __name__ == "__main__":
    torch.set_num_threads(1)
    t0 = time.time()
    print(inert_crowd())
    d = describe(zm.field_genome())
    print(json.dumps({k: v for k, v in d.items() if k != "per"}), round(time.time() - t0, 1), "s")
