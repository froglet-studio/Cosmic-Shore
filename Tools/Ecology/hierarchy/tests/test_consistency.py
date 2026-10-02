"""GATE: a population simulated MACRO for T then expanded matches one simulated MICRO for T.

World: a 600-u cell (113 regions), the same starting state (flora field + macro bins) for every arm; the arms
then differ only in which level simulates the fauna:
    micro   every region HOT from t=0 (expanded on the first tick) for T
    macro   every region COLD for T, then expanded (the summary is read off the expanded agents)
    switch  COLD for T/2, HOT for T/2 (a pilot arriving half way)
Compared over N seeds (seed means): herbivore / predator COUNT, mean STOMACH, PHASE mix (sated/forage/hungry,
total variation distance), standing FLORA. Each must be within tolerance of the micro arm.
NEGATIVE CONTROLS (planted bugs) must FAIL:
    macro_graze_x1.5      macro grazing 50% too strong
    expand_mean_field     expansion hands every agent the REGION's mean stomach (cohort structure lost)
    macro_attack_x2       predation attack rate mis-calibrated by 2x
    (macro_no_sprint_cost was a control until the sprint cost was MEASURED to be ~1% of predator burn: a
     control that stops breaking anything is retired, not kept as a fake pass)
Run:  python -m hierarchy.tests.test_consistency [--seeds 6] [--T 300]
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from hierarchy.params import Params  # noqa: E402
from hierarchy.sim import HierSim  # noqa: E402

TOL = dict(count=0.10, mean_e=0.10, sd_e=0.25, phase=0.08, flora=0.05, kills=0.30)
BUGS = ["macro_graze_x1.5", "expand_mean_field", "macro_attack_x2"]


def make(P, init_seed, run_seed, R, dens, pred_frac):
    sim = HierSim(P, seed=init_seed, R=R)
    nreg = sim.W.nreg
    sim.populate(int(nreg * dens), int(nreg * dens * pred_frac))
    sim.rng.bit_generator.state = np.random.default_rng(10_000 + run_seed).bit_generator.state
    return sim


def arm(kind, P, init_seed, run_seed, T, R, dens, pred_frac, series=False):
    sim = make(P, init_seed, run_seed, R, dens, pred_frac)
    nreg = sim.W.nreg
    steps = int(T / P.dt_micro)
    per = int(round(P.dt_macro / P.dt_micro))
    traj = []
    for i in range(steps):
        hot = kind == "micro" or (kind == "switch" and i >= steps // 2)
        sim.force_hot = np.full(nreg, hot)
        sim.step()
        if series and i % (per * 10) == 0:
            s = sim.summary(); traj.append([sim.t, s["herb"]["count"], s["pred"]["count"], s["flora"], sim.A.kills + sim.M.kills,
                                         int(sim.A.births[1] + sim.M.births[1]), int(sim.A.deaths[1] + sim.M.deaths[1]), s["pred"]["mean_e"]])
    # read the summary off EXPANDED agents (macro arm: expand everything now)
    sim.force_hot = np.ones(nreg, bool)
    sim._lod()
    s = sim.summary()
    s["kills"] = int(sim.A.kills + sim.M.kills)      # the predation FLOW: stocks barely move in 300 s
    s["traj"] = traj
    return s


def compare(ref, test):
    """seed-mean errors of `test` against `ref` (lists of summaries)."""
    def m(rs, sp, key):
        return float(np.mean([r[sp][key] for r in rs]))
    err = {}
    for sp in ("herb", "pred"):
        for key in ("count", "mean_e", "sd_e"):
            a, b = m(ref, sp, key), m(test, sp, key)
            err[f"{sp}_{key}"] = abs(b - a) / max(abs(a), 1e-9)
        pa = np.mean([r[sp]["phase"] for r in ref], 0); pb = np.mean([r[sp]["phase"] for r in test], 0)
        err[f"{sp}_phase"] = float(0.5 * np.abs(pa - pb).sum())
    a, b = np.mean([r["flora"] for r in ref]), np.mean([r["flora"] for r in test])
    err["flora"] = abs(b - a) / a
    a, b = np.mean([r["kills"] for r in ref]), np.mean([r["kills"] for r in test])
    err["kills"] = abs(b - a) / max(a, 1.0)
    # seed-to-seed spread of the reference (the noise floor the tolerance has to sit above)
    sd = {}
    for sp in ("herb", "pred"):
        v = [r[sp]["count"] for r in ref]
        sd[f"{sp}_count"] = float(np.std(v) / max(np.mean(v), 1e-9) / np.sqrt(len(v)))
    return err, sd


def within(err):
    lim = dict(herb_count=TOL["count"], pred_count=TOL["count"], herb_mean_e=TOL["mean_e"],
               herb_sd_e=TOL["sd_e"], pred_sd_e=TOL["sd_e"],
               pred_mean_e=TOL["mean_e"], herb_phase=TOL["phase"], pred_phase=TOL["phase"], flora=TOL["flora"], kills=TOL["kills"])
    return all(err[k] <= lim[k] for k in lim), {k: err[k] <= lim[k] for k in lim}


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, default=6)
    ap.add_argument("--T", type=float, default=300.0)
    ap.add_argument("--R", type=float, default=600.0)
    ap.add_argument("--dens", type=float, default=40.0)
    ap.add_argument("--pred_frac", type=float, default=0.06)
    ap.add_argument("--out", default="gate_consistency.json")
    ap.add_argument("--set", nargs="*", default=[], help="Params overrides key=value (float)")
    a = ap.parse_args(argv)
    over = {k: float(v) for k, v in (s.split("=") for s in a.set)}

    def Pm(**kw):
        return Params(**over, **kw)
    t0 = time.time()
    cfg = (a.T, a.R, a.dens, a.pred_frac)
    seeds = list(range(a.seeds))
    micro = [arm("micro", Pm(), 1, s, *cfg, series=True) for s in seeds]
    res = {}
    for name, P in [("clean", Pm())] + [(b, Pm(bug=b)) for b in BUGS]:
        mac = [arm("macro", P, 1, s, *cfg, series=True) for s in seeds]
        e, sd = compare(micro, mac)
        ok, which = within(e)
        res[name] = dict(err=e, passed=ok, which=which, noise=sd)
        if name == "clean":
            sw = [arm("switch", P, 1, s, *cfg) for s in seeds]
            e2, _ = compare(micro, sw)
            ok2, which2 = within(e2)
            res["switch"] = dict(err=e2, passed=ok2, which=which2)
            res["clean"]["traj_macro"] = [r["traj"] for r in mac]
    res["micro_traj"] = [r["traj"] for r in micro]
    gate = res["clean"]["passed"] and res["switch"]["passed"] and not any(res[b]["passed"] for b in BUGS)
    out = dict(gate_passed=bool(gate), tol=TOL, overrides=over, cfg=dict(T=a.T, R=a.R, dens=a.dens, pred_frac=a.pred_frac,
               seeds=a.seeds), results=res, wall_s=time.time() - t0)
    os.makedirs(os.path.join(os.path.dirname(__file__), "..", "results"), exist_ok=True)
    with open(os.path.join(os.path.dirname(__file__), "..", "results", a.out), "w") as fh:
        json.dump(out, fh, indent=1, default=float)
    for k in ["clean", "switch"] + BUGS:
        e = res[k]["err"]
        print(f"{k:20s} passed={res[k]['passed']!s:5s} " + " ".join(f"{x}={v:.3f}" for x, v in e.items()))
    print("noise (SE of micro counts):", res["clean"]["noise"])
    print("GATE", "PASS" if gate else "FAIL", f"({time.time() - t0:.0f}s)")
    return 0 if gate else 1


if __name__ == "__main__":
    sys.exit(main())
