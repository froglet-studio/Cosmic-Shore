"""Build the committed, curated viewer: builders/viewer.html (python Tools/Ecology/builders/make_viewer.py).
Each scene is re-run with a coarse frame step so the file stays a few MB. Every recording is seeded and the same
code path as the scorecards."""
import os, sys
from multiprocessing import Pool
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
from builders import harness
harness.REC_EVERY = 6
from builders.harness import run_one
from builders.run_fortress import cut_run
from builders.run_mixed import run as mixed_run
from builders.wasp import WaspComb
from builders.traps import TrapBuilders
from builders.wearers import Wearers


def scene(k):
    harness.REC_EVERY = 6
    if k == "fortress":
        return cut_run(7, mend="both", same_line=True, scar=3.0, record=True, tag="_viewer")[0]["recording"]
    if k == "mixed":
        return mixed_run(7, record=True)["recording"]
    fac, pol, mins, lab = dict(
        wasp_raid=(lambda a, s: WaspComb(a, seed=s), "hunter", 4.0, "wasp_comb_raid"),
        wasp_grow=(lambda a, s: WaspComb(a, seed=s), "wander", 4.0, "wasp_comb_grow"),
        traps_circuit=(lambda a, s: TrapBuilders(a, seed=s, n=30, max_nb=2, lane_min=3.0), "circuit", 3.0, "traps_racer"),
        traps_varied=(lambda a, s: TrapBuilders(a, seed=s, n=30, max_nb=2, lane_min=3.0), "varied", 3.0, "traps_varied"),
        wearers_wander=(lambda a, s: Wearers(a, seed=s, contact=0.5, body_cap=150), "wander", 3.0, "wearers_stalk"),
        wearers_hunter=(lambda a, s: Wearers(a, seed=s, contact=0.5, body_cap=150), "hunter", 3.0, "wearers_fight"),
    )[k]
    return run_one(fac, pol, 7, minutes=mins, record=True, label=lab)["recording"]


ORDER = ["fortress", "wasp_grow", "wasp_raid", "traps_circuit", "traps_varied", "wearers_wander", "wearers_hunter", "mixed"]

if __name__ == "__main__":
    with Pool(4) as p:
        paths = p.map(scene, ORDER)
    from common.viewer import build
    out = os.path.join(HERE, "viewer.html")
    print(build(out, paths, "Builders and thieves"), "bytes ->", out)
