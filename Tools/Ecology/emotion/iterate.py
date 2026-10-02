"""Grow the feature set one literature group at a time and log held-out agreement after each step.
The stopping rule: stop adding when the LOFO number stops improving.

    python iterate.py        -> results/iterations.json + a printed table
"""
from __future__ import annotations

import json
import os

import numpy as np

from model import load, lofo, within, transfer
from sim import HERE

GROUPS = [
    ("G0 old scorecard feel", ["size_log", "speed_rel", "approach", "coherence", "jerk_rel"]),
    ("G1 + looming (Schiff, Lee tau)", ["loom", "tau_inv", "view_fill", "proximity"]),
    ("G2 + scale & baby schema", ["extent_log", "roundness", "count_log"]),
    ("G3 + gaze / chase / encircle (Gao, Heider-Simmel)", ["gaze", "pursuit", "orbit", "encircle", "converge"]),
    ("G4 + coordination / uncanny", ["synchrony", "regularity", "mimicry"]),
    ("G5 + effort & rhythm (Laban, Pollick, Tremoulet)", ["accel_rel", "curvature", "bounce", "wobble_hz",
                                                          "speed_cv", "unpredict", "stillness", "burst",
                                                          "approach_retreat"]),
]


def main():
    ref = os.path.join(HERE, "results", "reference_set.json")
    ev = os.path.join(HERE, "results", "reference_evade.json")
    keys, log = [], []
    for name, add in GROUPS:
        keys = keys + add
        X, y, fam, _ = load(ref, keys)
        Xe, ye, _, _ = load(ev, keys)
        row = dict(step=name, n_features=len(keys))
        for kind in ("logistic", "prototype"):
            a, conf = lofo(X, y, fam, kind)
            row[kind] = dict(lofo=round(a, 3), within=round(within(X, y, kind), 3),
                             viewer=round(transfer(X, y, Xe, ye, kind), 3))
        log.append(row)
        print(f"{name:52s} d={len(keys):2d}  logistic lofo {row['logistic']['lofo']:.3f} within "
              f"{row['logistic']['within']:.3f} viewer {row['logistic']['viewer']:.3f} | prototype lofo "
              f"{row['prototype']['lofo']:.3f} within {row['prototype']['within']:.3f} viewer {row['prototype']['viewer']:.3f}")
    json.dump(log, open(os.path.join(HERE, "results", "iterations.json"), "w"), indent=1)
    return keys


if __name__ == "__main__":
    main()
