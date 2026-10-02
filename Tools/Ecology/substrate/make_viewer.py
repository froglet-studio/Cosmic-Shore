"""Generate the substrate viewer (common/viewer.py): species from ONE substrate, each run a dropdown entry.

    python -m substrate.make_viewer [--full]     -> substrate/out/substrate_viewer.html (gitignored-size)
                                                 -> substrate/viewer_sample.html (small, committed)
The sample keeps frame rates and agent counts low enough to stay under ~3 MB.
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from dataclasses import replace
from common.arena import Arena, Pilot, Recorder
from common.viewer import build
from substrate.core import Substrate
from substrate import species as S

HERE = os.path.dirname(__file__)


def record(label, note, species, pilot, seconds, every, seed=7, mass=2500, spread=60.0, hunger=None, place=True,
           charge_at=None):
    ar = Arena(seed=seed); ar.scatter_mass(mass)
    pl = ar.add_pilot(pilot)
    sps = [Substrate(ar, P, spread=spread, init_hunger=hunger) for P in species]
    for sp in sps[1:]:
        sp.publish = False
    if place:
        pl.pos = sps[0].home + np.array([400.0, 0, 0])
    rec = Recorder(every=every)
    for i in range(int(seconds / 0.1)):
        if charge_at is not None:
            pl.policy = "hunter" if charge_at <= ar.t < charge_at + 12 else "wander"
        for sp in sps:
            sp.step(ar, 0.1)
        ar.step(0.1); rec.frame(ar, sps)
    path = os.path.join(HERE, "out", f"{label.split()[0].lower()}_{seed}.json")
    rec.save(path, dict(label=label, note=note))
    return path


if __name__ == "__main__":
    full = "--full" in sys.argv
    os.makedirs(os.path.join(HERE, "out"), exist_ok=True)
    k = 1 if full else 4            # frame-every multiplier for the sample
    runs = [
        record("Grazer school (cute)", "one substrate, grazer params: curious spring to a comfort ring, aligns, flees "
               "when the pilot charges; never aggressive", [S.grazer(n0=140)], Pilot.wanderer(), 60, 3 * k),
        record("Locust phase change", "ONE param set: green = solitary (shy, curious), yellow = gregarious (fast, "
               "convergent). The population eats its food, crowds and gets hungry; the quorum flips it", 
               [S.locust(n0=200)], Pilot.wanderer(), 120, 4 * k, mass=1500, spread=120),
        record("Pack (stalk -> strike)", "magenta = stalking on a ring AHEAD of the pilot; red = the same quorum "
               "(together + hungry) flipped it to strike", [S.pack(n0=8)], Pilot.wanderer(), 60, 2 * k),
        record("Leviathan condense/dissolve", "a sated grazer school condenses into a manta made of its own members, "
               "dissolves when hungry, condenses again", [S.leviathan(n0=140)], Pilot.wanderer(), 240, 6 * k,
               seed=23, place=False, spread=120, hunger=0.5),
        record("All three, one cell", "grazers + locusts + pack sharing one cell's fields: the pack's threat "
               "deposit is the grazers' danger", [S.grazer(n0=100), S.locust(n0=150), S.pack(n0=6)],
               Pilot.wanderer(), 90, 4 * k, mass=2000),
    ]
    out = os.path.join(HERE, "out", "substrate_viewer.html") if full else os.path.join(HERE, "viewer_sample.html")
    print(out, build(out, runs, "Substrate: one sim, many species"), "bytes")
