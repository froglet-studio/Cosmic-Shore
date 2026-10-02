"""Ladley & Bullock's logistic claim, measured: does a structure grow TOWARD where its material comes from?
cos(angle) between (structure centroid - anchor) and (mean pickup position - anchor), 8 seeds, plus the same
statistic with the supply direction of a DIFFERENT seed as the null. python Tools/Ecology/builders/run_supply.py"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from builders.harness import run_one
from builders.nest import NestWeavers
from builders.wasp import WaspComb

SPECIES = dict(nest_v0_shell=lambda a, s: NestWeavers(a, seed=s),
               nest_v1_logistic=lambda a, s: NestWeavers(a, seed=s, Rc=44, w=30, k_cement=0.4, nucleate=0.02, homing="core"),
               wasp=lambda a, s: WaspComb(a, seed=s))


def unit(v):
    return v / max(np.linalg.norm(v), 1e-9)


if __name__ == "__main__":
    seeds = (7, 23, 41, 59, 61, 83, 97, 101)
    out = {}
    for name, fac in SPECIES.items():
        rows = []
        for sd in seeds:
            sp = run_one(fac, "wander", sd, minutes=3)["_sp"]
            P = sp.lat.occupancy_points(); A = sp.lat.anchor
            rows.append((unit(P.mean(0) - A), unit(np.mean(sp.pickup_pos, axis=0) - A), float(np.linalg.norm(P.mean(0) - A))))
        cos = [float(c @ s) for c, s, _ in rows]
        null = [float(rows[i][0] @ rows[(i + 1) % len(rows)][1]) for i in range(len(rows))]
        out[name] = dict(cos_mean=round(float(np.mean(cos)), 3), cos=[round(c, 2) for c in cos],
                         null_mean=round(float(np.mean(null)), 3), offset_u=[round(r[2], 1) for r in rows])
        print(name, out[name], flush=True)
    json.dump(out, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "results", "supply_direction.json"), "w"), indent=1)
