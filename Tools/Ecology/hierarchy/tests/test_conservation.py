"""GATE: mass and headcount are conserved across expand / absorb / arrive, and in each level.

A full 1200-u cell, two fast pilots crossing it (hundreds of LOD transitions), 300 s. Checks:
  1. ledger total (flora + skeleton + nutrient + macro bodies/stomachs + agent bodies/stomachs) is constant:
     |drift| / total < 1e-11 at every macro step
  2. across every LOD pass (expand + absorb + inbox): individuals before == after, and fauna mass before ==
     after to 1e-9 volume (any settle to the soil must be ~0 - it is a rounding valve, not a sink)
  3. no negative counts, no negative stomachs, every flow actually exercised (expand, absorb, arrive,
     births, starvations and kills in BOTH levels)
NEGATIVE CONTROLS: each planted bug below must FAIL the gate. Run:  python -m hierarchy.tests.test_conservation
"""
from __future__ import annotations

import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from hierarchy.params import Params  # noqa: E402
from hierarchy.sim import HierSim, Pilot  # noqa: E402

BUGS = ["absorb_drop_stomach", "birth_free_body", "expand_lose_pool"]
TOL_REL = 1e-11
TOL_LOD = 1e-6


def run(bug="", T=300.0, seed=5, n_herb=30000, n_pred=2500):
    P = Params(bug=bug)
    sim = HierSim(P, seed=seed)
    sim.populate(n_herb, n_pred)
    for sp in (220.0, 260.0):
        p = sim.add_pilot(Pilot.wanderer(speed=sp)); p.turn = 1.0
    L0 = sim.ledger()["total"]
    worst_rel, worst_lod, worst_cnt = 0.0, 0.0, 0
    neg = False
    lod_orig = sim._lod

    def fauna():
        A = sim.A
        return sim.M.mass() + A.mass(), int(sim.M.H.count().sum() + sim.M.Pr.count().sum() + A.n + sim.M.inbox_count())

    def lod_checked():
        nonlocal worst_lod, worst_cnt
        m0, c0 = fauna(); N0 = float(sim.W.N.sum())
        lod_orig()
        m1, c1 = fauna(); N1 = float(sim.W.N.sum())
        worst_lod = max(worst_lod, abs((m0 - m1) - (N1 - N0)), abs(N1 - N0))
        worst_cnt = max(worst_cnt, abs(c0 - c1))
    sim._lod = lod_checked
    drain_orig = sim._drain_inbox

    def drain_checked():
        nonlocal worst_lod, worst_cnt
        m0, c0 = fauna()
        drain_orig()
        m1, c1 = fauna()
        worst_lod = max(worst_lod, abs(m0 - m1)); worst_cnt = max(worst_cnt, abs(c0 - c1))
    sim._drain_inbox = drain_checked
    for _ in range(int(T / P.dt_micro)):
        sim.step()
        if sim.k % 10 == 0:
            worst_rel = max(worst_rel, abs(sim.ledger()["total"] - L0) / L0)
            neg |= bool((sim.M.H.N < 0).any() or (sim.M.Pr.N < 0).any() or (sim.A.view("E") < -1e-9).any())
    ev = dict(sim.events)
    flows = dict(expand=ev["expand"], absorb=ev["absorb"], arrive=ev["arrive"],
                 macro_births=sim.M.births.tolist(), macro_deaths=sim.M.deaths.tolist(), macro_kills=sim.M.kills,
                 micro_births=sim.A.births.tolist(), micro_deaths=sim.A.deaths.tolist(), micro_kills=sim.A.kills)
    exercised = ev["expand"] > 0 and ev["absorb"] > 0 and ev["arrive"] > 0 and sim.M.kills > 0 and sim.A.kills > 0 \
        and sum(sim.M.births) > 0 and sum(sim.A.births) > 0
    ok = worst_rel < TOL_REL and worst_lod < TOL_LOD and worst_cnt == 0 and not neg
    return dict(bug=bug, passed=bool(ok), exercised=bool(exercised), ledger_rel_drift=worst_rel,
                lod_mass_mismatch=worst_lod, lod_count_mismatch=worst_cnt, negative_state=neg, flows=flows,
                total=L0, settle=float(ev["settle"]))


def main():
    # bug runs use the clean horizon: at T=120 no agent had bred yet, so birth_free_body had nothing to corrupt and
    # PASSED - a vacuous control (bounded cohorts clip expanded stomachs below e_birth, delaying the first agent birth)
    res = [run("")] + [run(b) for b in BUGS]
    gate = res[0]["passed"] and res[0]["exercised"] and not any(r["passed"] for r in res[1:])
    out = dict(gate_passed=bool(gate), runs=res)
    os.makedirs(os.path.join(os.path.dirname(__file__), "..", "results"), exist_ok=True)
    with open(os.path.join(os.path.dirname(__file__), "..", "results", "gate_conservation.json"), "w") as fh:
        json.dump(out, fh, indent=1, default=float)
    for r in res:
        print(f"{r['bug'] or 'CLEAN':22s} passed={r['passed']!s:5s} drift={r['ledger_rel_drift']:.2e} "
              f"lod_mass={r['lod_mass_mismatch']:.2e} lod_count={r['lod_count_mismatch']} exercised={r['exercised']}")
    print("GATE", "PASS" if gate else "FAIL")
    return 0 if gate else 1


if __name__ == "__main__":
    sys.exit(main())
