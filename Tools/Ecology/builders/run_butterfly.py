"""Is a structure a function of the SEED or of the PLAY? Same seed, the pilot's start nudged by 1 u: if the built
structure still differs about as much as between two seeds, then two players on the same seed get different nests.
python Tools/Ecology/builders/run_butterfly.py"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from builders.harness import run_one, replayability
from builders.wasp import WaspComb
from builders.nest import NestWeavers
from builders.traps import TrapBuilders
from builders.wearers import Wearers

SPECIES = dict(nest_v1=(lambda a, s: NestWeavers(a, seed=s, Rc=44, w=30, k_cement=0.4, nucleate=0.02, homing="core"), "wander"),
               wasp=(lambda a, s: WaspComb(a, seed=s), "wander"),
               traps_v2=(lambda a, s: TrapBuilders(a, seed=s, n=30, max_nb=2, lane_min=3.0), "circuit"),
               wearers_v2=(lambda a, s: Wearers(a, seed=s, contact=0.5), "wander"))


def nudge(eps):
    def f(ar, p):
        p.pos = p.pos + np.array([eps, 0, 0])
    return f


if __name__ == "__main__":
    out = {}
    for name, (fac, pol) in SPECIES.items():
        base = run_one(fac, pol, 7, minutes=3)["_sp"]
        same = run_one(fac, pol, 7, minutes=3)["_sp"]
        nud = run_one(fac, pol, 7, minutes=3, setup=nudge(1.0))["_sp"]
        other = run_one(fac, pol, 23, minutes=3)["_sp"]
        out[name] = dict(same_seed=replayability([base, same])["jaccard_mean"],
                         nudged_1u=replayability([base, nud])["jaccard_mean"],
                         other_seed=replayability([base, other])["jaccard_mean"])
        print(name, out[name], flush=True)
    json.dump(out, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "results", "butterfly.json"), "w"), indent=1)
