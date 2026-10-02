"""Trap dose-response: how lethal is a lane web as a function of colony size and strand rule, and does the
lane counterplay survive the tuning? python Tools/Ecology/builders/sweep_traps.py"""
import json, os, sys
from itertools import product
from multiprocessing import Pool
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from builders.harness import run_one
from builders.traps import TrapBuilders

GRID = list(product((15, 30, 60), (1, 2, 3), (1.2, 3.0)))   # workers, max_nb, lane_min


def one(args):
    n, nb, lm = args
    row = dict(n=n, max_nb=nb, lane_min=lm)
    for pol in ("circuit", "varied", "wander"):
        h, age, built = [], [], []
        for sd in (7, 23):
            r = run_one(lambda a, s: TrapBuilders(a, seed=s, n=n, max_nb=nb, lane_min=lm), pol, sd, minutes=3)
            h.append(r["hits_per_min"]); built.append(r["structure"]["built"])
            if r["structure"]["trap_age_med"] is not None:
                age.append(r["structure"]["trap_age_med"])
        row[pol] = round(float(np.mean(h)), 2); row[pol + "_built"] = int(np.mean(built))
        if pol == "circuit":
            row["age"] = round(float(np.median(age)), 1) if age else None
    row["counterplay"] = round(row["varied"] / row["circuit"], 3) if row["circuit"] else None
    return row


if __name__ == "__main__":
    rows = []
    with Pool(4) as p:
        for r in p.imap_unordered(one, GRID):
            rows.append(r); print(json.dumps(r), flush=True)
    os.makedirs(os.path.join(os.path.dirname(__file__), "results"), exist_ok=True)
    json.dump(sorted(rows, key=lambda r: (r["n"], r["max_nb"], r["lane_min"])),
              open(os.path.join(os.path.dirname(__file__), "results", "traps_sweep.json"), "w"), indent=1)
