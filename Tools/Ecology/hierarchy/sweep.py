"""Sweep shared-biology parameters on the cheap MACRO level for living dynamics (no extinction, no freeze, no
runaway, asynchronous regions), then the winners are re-checked by the consistency gate (micro <-> macro).

Only parameters both levels share are swept (metabolism, intake, flora growth, half-saturations); the
behaviour-derived rates (predation a/h, hops, occupancy) stay at their micro-fitted values.

    python -m hierarchy.sweep --T 3600 --grid name   (see GRIDS)
"""
from __future__ import annotations

import argparse
import itertools
import json
import os
from concurrent.futures import ProcessPoolExecutor
from dataclasses import replace

import numpy as np

from .params import Params, HERB, PRED
from . import params as PM
from .longrun import run, metrics

OUT = os.path.join(os.path.dirname(__file__), "results")

GRIDS = {
    "a": dict(h_half=[30.0, 90.0], pred_metab=[0.06, 0.12], flora_r=[0.003, 0.006]),
    "c": dict(h_half=[60.0, 90.0], pred_metab=[0.015, 0.02, 0.025, 0.03], flora_r=[0.006]),
    "b": dict(h_half=[90.0], pred_metab=[0.02, 0.03, 0.04], pred_sprint=[0.03, 0.06], flora_r=[0.003, 0.006]),
}


def one(args):
    T, kw, seed = args
    pm = kw.pop("pred_metab", None)
    hm = kw.pop("herb_metab", None)
    ps = kw.pop("pred_sprint", None)
    if ps is not None:
        PM.PRED.sprint_metab = ps
    if pm is not None:
        PM.PRED.metab = pm
    if hm is not None:
        PM.HERB.metab = hm
    P = Params(**kw)
    sim, rows, snaps, wall = run(T, 0, seed, P=P, verbose=False)
    met = metrics(rows, snaps, burn=min(900.0, T / 3))
    met.update(kw); met["pred_metab"] = pm; met["herb_metab"] = hm; met["pred_sprint"] = ps; met["wall"] = wall
    met["final_flora"] = float(rows[-1, 3]); met["final_N"] = float(rows[-1, 5])
    return met


def score(m):
    """Liveness: both species persist, breathe, don't run away; flora stays standing (vibrancy)."""
    alive = m["min_herb"] > 500 and m["min_pred"] > 50
    breathe = 0.05 < m["cv_herb"] < 0.6 and 0.05 < m["cv_pred"] < 0.8
    return dict(alive=alive, breathe=breathe, ok=alive and breathe)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--T", type=float, default=3600.0)
    ap.add_argument("--grid", default="a")
    ap.add_argument("--workers", type=int, default=3)
    a = ap.parse_args()
    g = GRIDS[a.grid]
    keys = list(g)
    jobs = [(a.T, dict(zip(keys, v)), 7) for v in itertools.product(*[g[k] for k in keys])]
    with ProcessPoolExecutor(a.workers) as ex:
        res = list(ex.map(one, jobs))
    for r in res:
        r.update(score(r))
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, f"sweep_{a.grid}.json"), "w") as fh:
        json.dump(res, fh, indent=1, default=float)
    for r in res:
        print({k: (round(v, 3) if isinstance(v, float) else v) for k, v in r.items()
               if k in keys + ["pred_sprint", "min_herb", "min_pred", "mean_herb", "mean_pred", "cv_herb", "cv_pred", "period_s",
                               "regional_sync", "final_flora", "ok"]})


if __name__ == "__main__":
    main()
