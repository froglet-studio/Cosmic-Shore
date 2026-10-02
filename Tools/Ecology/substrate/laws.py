"""Audit the locked laws (Tools/Ecology/PROGRAM.md §1) on the substrate, every species, every step.

  mass        live prism volume + agents' stock is constant to float rounding, every step (eating MOVES
              volume into stock; a death re-lays stock as prisms; reproduction splits stock). Nothing decays.
  no_imposed  every death has an active cause: 'pilot' (rammed by a hunting pilot) or 'starvation' with
              hunger pinned at 1 for >= starve_s. There is no lifespan, no cap cull, no timer.
  continuity  (a) no pop-in: an agent first rendered has size <= grow-in step; (b) no pop-out: an agent that
              stops being rendered had size <= one wither step; (c) no teleport: |dpos| <= |v| dt and
              |d|v|| <= accel dt (+ tolerance); (d) rendered size never jumps more than growth + phase rate.

Run: python -m substrate.laws   (writes results/laws.json)
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from common.arena import Arena, Pilot
from substrate.core import Substrate
from substrate import species as S

HERE = os.path.dirname(__file__)


def audit(name, policy, seed=7, minutes=4.0, dt=0.1, backend="numpy", scarce=False):
    ar = Arena(seed=seed); ar.scatter_mass(300 if scarce else 3000)
    ar.add_pilot(Pilot.hunter() if policy == "hunter" else Pilot.wanderer())
    P = S.SPECIES[name](seed=seed)
    sp = Substrate(ar, P, backend=backend)
    total0 = ar.live_volume() + sp.mass_held()
    worst_mass = 0.0
    prev_alive = sp.alive.copy(); prev_size = sp._sizes().copy(); prev_pos = sp.pos.copy()
    prev_spd = np.linalg.norm(sp.vel, axis=1)
    acc_max = max(P.solitary.accel, P.gregarious.accel)
    grow_step = dt / P.grow_s * max(P.solitary.size, P.gregarious.size)
    phase_step = dt * P.q_rate * abs(P.gregarious.size - P.solitary.size)
    v = dict(pop_in=0, pop_out=0, teleport=0, accel=0, size_jump=0, steps=0)
    for _ in range(int(minutes * 60 / dt)):
        sp.step(ar, dt); ar.step(dt)
        tot = ar.live_volume() + sp.mass_held()
        worst_mass = max(worst_mass, abs(tot - total0) / total0)
        size = sp._sizes(); a = sp.alive
        born = a & ~prev_alive; gone = ~a & prev_alive; both = a & prev_alive
        v["pop_in"] += int(np.sum(size[born] > grow_step + 1e-9))
        v["pop_out"] += int(np.sum(prev_size[gone] > grow_step + 1e-9))
        spd = np.linalg.norm(sp.vel, axis=1)
        tot_spd = np.linalg.norm(sp.vel + sp.gaitv, axis=1)       # a gait moves the body too
        moved = np.linalg.norm(sp.pos - prev_pos, axis=1)
        # positions may be pulled in by the membrane clamp (shorter, never longer)
        v["teleport"] += int(np.sum(moved[both] > tot_spd[both] * dt * 1.0001 + 1e-6))
        v["accel"] += int(np.sum(np.abs(spd[both] - prev_spd[both]) > acc_max * dt * 1.0001 + 1e-6))
        v["size_jump"] += int(np.sum(np.abs(size[both] - prev_size[both]) > grow_step + phase_step + 1e-6))
        v["steps"] += 1
        prev_alive = a.copy(); prev_size = size.copy(); prev_pos = sp.pos.copy(); prev_spd = spd
    causes = {}
    bad = 0
    for (t, c, h, st) in sp.death_log:
        causes[c] = causes.get(c, 0) + 1
        if c == "starvation" and (h < 1.0 or st < P.starve_s):
            bad += 1
        if c not in ("pilot", "starvation"):
            bad += 1
    return dict(species=name, policy=policy, scarce=scarce, mass_rel_err=worst_mass, eaten=round(ar.eaten),
                laid=round(sp.laid), births=sp.births, deaths=causes, imposed_deaths=bad, **v,
                ok=bool(worst_mass < 1e-9 and bad == 0 and v["pop_in"] == v["pop_out"] == v["teleport"] ==
                        v["accel"] == v["size_jump"] == 0))


if __name__ == "__main__":
    rows = []
    for name in ("grazer", "locust", "pack", "leviathan"):
        for policy, scarce in (("wander", False), ("hunter", False), ("wander", True)):
            r = audit(name, policy, scarce=scarce); rows.append(r); print(json.dumps(r))
    r = audit("locust", "hunter", backend="fused"); rows.append(dict(r, backend="fused")); print(json.dumps(r))
    json.dump(rows, open(os.path.join(HERE, "results", "laws.json"), "w"), indent=1)
    print("ALL OK" if all(r["ok"] for r in rows) else "VIOLATIONS")
