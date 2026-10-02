"""The locked rules, asserted for every builder species (python Tools/Ecology/builders/test_rules.py).

  1. STEALING IS CHANGING HANDS: with a non-ramming pilot nothing is eaten or destroyed, live volume is unchanged
     (except what pilots LAID), and Arena.audit() == 0 in every run.
  2. SHIELDED MASS IS NEVER TAKEN: with 30% of the scattered mass shielded, no shielded prism ever changes domain,
     moves, or appears in a structure / body.
  3. NEGATIVE CONTROL: a deliberately broken thief that ignores the shield is CAUGHT by check 2.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from common.arena import Arena, Pilot                       # noqa: E402
from builders.harness import ram, TRAIL_EVERY               # noqa: E402
from builders.nest import NestWeavers                       # noqa: E402
from builders.wasp import WaspComb                          # noqa: E402
from builders.fortress import Fortress                      # noqa: E402
from builders.traps import TrapBuilders                     # noqa: E402
from builders.wearers import Wearers                        # noqa: E402

SPECIES = dict(nest=lambda a, s: NestWeavers(a, seed=s, homing="core", Rc=44, w=30, k_cement=0.4),
               wasp=lambda a, s: WaspComb(a, seed=s), fortress=lambda a, s: Fortress(a, seed=s),
               traps=lambda a, s: TrapBuilders(a, seed=s, danger=False), wearers=lambda a, s: Wearers(a, seed=s))


def held(sp):
    """Every mass index the species is holding (structure, carried, worn)."""
    out = set()
    if hasattr(sp, "body"):
        for b in sp.body:
            out |= set(b.values())
    else:
        out |= set(sp.lat.sites.keys()); out |= {int(c) for c in sp.carry if c >= 0}
    return out


def check(name, fac, seed=7, seconds=90, dt=0.1, policy="circuit", cheat=False):
    ar = Arena(seed=seed); ar.scatter_mass(1500, shielded_frac=0.3); ar.struct_owner = {}
    if policy == "circuit":
        th = np.linspace(0, 2 * np.pi, 7)[:-1]
        p = Pilot.circuit([np.array([600 * np.cos(a), 120 * np.sin(2 * a), 600 * np.sin(a)]) for a in th])
    else:
        p = Pilot.wanderer()
    p.trail_every = TRAIL_EVERY; ar.add_pilot(p)
    sp = fac(ar, seed)
    sh = np.flatnonzero(ar.mass_shielded)
    pos0, dom0 = ar.mass_pos[sh].copy(), ar.mass_dom[sh].copy()
    v0 = ar.live_volume()
    for _ in range(int(seconds / dt)):
        if cheat:     # the negative control: the species (and steal) are shown the world with every shield off
            saved = ar.mass_shielded.copy(); ar.mass_shielded[:] = False
            sp.step(ar, dt); ar.mass_shielded = saved
        else:
            sp.step(ar, dt)
        ar.step(dt)
    errs = []
    if abs(ar.audit()) > 1e-6:
        errs.append(f"audit {ar.audit()}")
    if ar.eaten or ar.destroyed:
        errs.append(f"eaten {ar.eaten} destroyed {ar.destroyed} (no ramming pilot, nothing may leave)")
    if abs(ar.live_volume() - (v0 + ar.laid)) > 1e-6:
        errs.append("live volume changed beyond what pilots laid")
    if (ar.mass_dom[sh] != dom0).any():
        errs.append(f"{int((ar.mass_dom[sh] != dom0).sum())} shielded prisms changed domain")
    if (np.abs(ar.mass_pos[sh] - pos0).sum(1) > 1e-9).any():
        errs.append(f"{int((np.abs(ar.mass_pos[sh] - pos0).sum(1) > 1e-9).sum())} shielded prisms moved")
    h = held(sp)
    if h & set(sh.tolist()):
        errs.append(f"{len(h & set(sh.tolist()))} shielded prisms held")
    return errs, ar.steals, len(h)


if __name__ == "__main__":
    bad = 0
    for name, fac in SPECIES.items():
        errs, steals, nh = check(name, fac)
        print(f"{name:9s} steals {steals:4d} held {nh:4d}  {'OK' if not errs else 'FAIL ' + '; '.join(errs)}")
        bad += bool(errs)
    errs, steals, nh = check("wearers-cheat", SPECIES["wearers"], cheat=True)
    print(f"negative control (shield-ignoring thief): {'CAUGHT: ' + '; '.join(errs) if errs else 'NOT CAUGHT - the test is blind'}")
    bad += not errs
    sys.exit(1 if bad else 0)
