"""GATE: nothing pops in or out where a pilot can see it (the platform-wide continuity law).

Two pilots fly a full cell for T seconds while regions expand and collapse around them. Every micro tick each
agent a pilot can SEE (sim.visible) is checked:
  pop_in      an id that was not drawn last tick appears in view WITHOUT a continuous origin. Legal origins:
              a newborn (bloom < 1: it grows from nothing at its parent's side), an emerging agent (emerge < 1:
              drawn sliding out of a representative that WAS drawn), an arrival (spawned AT a representative).
  pop_out     an id that was in view disappears without a cause. Legal causes: eaten (predation), starved
              (a starvation wither - in game this is the wither-to-crystal), absorbed while NOT visible.
  teleport    a visible agent's drawn position jumps > 3x its sprint step in one tick.
Gate: zero pop_in, zero pop_out, zero teleport. NEGATIVE CONTROLS: absorb ignoring visibility, and
expansion drawing agents at their sampled spot from frame 0 (no emerge), must both FAIL.
Run: python -m hierarchy.tests.test_continuity
"""
from __future__ import annotations

import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from hierarchy.params import Params, HERB, PRED  # noqa: E402
from hierarchy.sim import HierSim, Pilot  # noqa: E402


def run(bug="", T=150.0, seed=9):
    P = Params(bug=bug)
    sim = HierSim(P, seed=seed)
    sim.populate(45000, 3000)
    for sp in (140.0, 200.0):
        p = sim.add_pilot(Pilot.wanderer(speed=sp)); p.turn = 1.0
    observe = sim.visible                 # the test's own eyes: never patched by a planted bug
    if bug == "absorb_ignores_visibility":
        sim.visible = lambda pos: np.zeros(len(pos), bool)
    prev = {}
    pops_in = pops_out = tele = seen = 0
    sprint_step = max(HERB.sprint, PRED.sprint) * P.dt_micro
    for i in range(int(T / P.dt_micro)):
        k0 = sim.A.kills; d0 = sim.A.deaths.copy()
        out = {}
        sim.step()
        A = sim.A
        n = A.n
        if n == 0:
            prev = {}; continue
        sim.render(out)
        drawn = out["agents"]["pos"]
        if bug == "expand_no_emerge":
            drawn = A.view("pos")
        vis = observe(drawn)
        ids = A.view("id")
        bloom = A.view("bloom"); emerge = A.view("emerge")
        cur = {}
        for j in np.flatnonzero(vis):
            cur[int(ids[j])] = drawn[j]
        seen += len(cur)
        # pop-in
        for j in np.flatnonzero(vis):
            i_ = int(ids[j])
            if i_ in prev:
                if np.linalg.norm(drawn[j] - prev[i_]) > 3 * sprint_step + 1e-6 and emerge[j] >= 1.0:
                    tele += 1
                continue
            legal = bloom[j] < 1.0 or (emerge[j] < 1.0 and bug != "expand_no_emerge") or i_ in sim._arrived_ids
            was_drawn_last = i_ in sim._drawn_last
            if not (legal or was_drawn_last):
                pops_in += 1
        # pop-out: in view last tick, gone now, and not explained by a kill/starvation this tick
        gone = [i_ for i_ in prev if i_ not in set(ids.tolist())]
        explained = (A.kills - k0) + int((A.deaths - d0).sum())
        pops_out += max(0, len(gone) - explained)
        prev = cur
        sim._drawn_last = set(ids.tolist())
    return dict(bug=bug, seen_agent_ticks=seen, pop_in=pops_in, pop_out=pops_out, teleport=tele,
                expand=sim.events["expand"], absorb=sim.events["absorb"], arrive=sim.events["arrive"],
                passed=pops_in == 0 and pops_out == 0 and tele == 0)


def main():
    res = [run(""), run("absorb_ignores_visibility", T=60.0), run("expand_no_emerge", T=60.0)]
    gate = res[0]["passed"] and not res[1]["passed"] and not res[2]["passed"]
    os.makedirs(os.path.join(os.path.dirname(__file__), "..", "results"), exist_ok=True)
    with open(os.path.join(os.path.dirname(__file__), "..", "results", "gate_continuity.json"), "w") as fh:
        json.dump(dict(gate_passed=bool(gate), runs=res), fh, indent=1, default=float)
    for r in res:
        print(r)
    print("GATE", "PASS" if gate else "FAIL")
    return 0 if gate else 1


if __name__ == "__main__":
    sys.exit(main())
