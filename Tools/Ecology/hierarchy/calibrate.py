"""Fit the macro rates FROM the micro level, never the other way round.

Runs a cell with every region forced HOT (pure micro), and every macro step (1 s) records:
  grazing   realised flora volume eaten per region  vs  the macro formula on the same instantaneous state
            (counts, stomach bins, flora field) for a grid of occupancy exponents theta -> best theta
  predation kills per region vs (hunters, herbivores in region) -> Holling II (a, h) by least squares
  movement  region changes per agent per second, by species and phase -> per-neighbour hop rate
Writes results/calibration.json and prints the fitted values. Usage:
    python -m hierarchy.calibrate [--R 600] [--T 400] [--seeds 2]
"""
from __future__ import annotations

import argparse
import json
import os

import numpy as np

from .params import Params, HERB, PRED
from .sim import HierSim

OUT = os.path.join(os.path.dirname(__file__), "results")


def macro_graze_prediction(sim, thetas):
    """The macro grazing formula evaluated on the CURRENT micro state, per theta. Returns (len(thetas), nreg)."""
    W, P, A = sim.W, sim.P, sim.A
    n = A.n
    reg = np.maximum(W.region_of(A.view("pos")), 0)
    isH = A.view("sp") == 0
    E = A.view("E")
    sat = np.clip(1 - E / HERB.e_max, 0, 1)
    # bin-centre satiation (exactly what macro would use)
    pop = sim.M.H
    satb = pop.sat[pop.bin_of(E)]
    eff = np.bincount(reg[isH], weights=satb[isH], minlength=W.nreg)
    G = W.F + W.K
    g = G / (G + P.h_half)
    out = []
    for th in thetas:
        w = np.power(np.maximum(G, 0), th) * W.vox_ok
        s = w.sum(1, keepdims=True)
        occ = np.where(s > 1e-9, w / np.maximum(s, 1e-12), W.vox_ok / W.vox_ok.sum(1, keepdims=True))
        want = eff[:, None] * occ * P.h_intake * g * P.dt_macro
        out.append(np.minimum(want, G).sum(1))
    return np.array(out)


def run(R=600.0, T=400.0, seed=1, P=None, dens=50.0, pred_frac=0.09):
    P = P or Params()
    sim = HierSim(P, seed=seed, R=R)
    nreg = sim.W.nreg
    sim.populate(int(nreg * dens), int(nreg * dens * pred_frac))
    sim.force_hot = np.ones(nreg, bool)
    thetas = np.array([0.0, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0])
    per = int(round(P.dt_macro / P.dt_micro))
    graze_pred, graze_real, pred_rows = [], [], []
    move = {0: np.zeros((3, 2)), 1: np.zeros((3, 2))}   # [phase] -> (region changes, agent-seconds)
    A = sim.A
    win = None
    for step in range(int(T / P.dt_micro)):
        if step % per == 0:
            if step > 0:
                graze_real.append(A.graze_acc.copy())
                graze_pred.append(pred0)
                pred_rows.append(np.stack([hunters0, herbs0, A.kill_acc.copy()], 1))
            sim.step()          # flora growth + (no-op) LOD/macro + one micro tick
            A.graze_acc[:] = 0; A.kill_acc[:] = 0
            pred0 = macro_graze_prediction(sim, thetas)
            reg = np.maximum(sim.W.region_of(A.view("pos")), 0)
            isH = A.view("sp") == 0
            hunt = (~isH) & (A.view("E") < P.p_hunt_below * PRED.e_max)
            hunters0 = np.bincount(reg[hunt], minlength=nreg)
            herbs0 = np.bincount(reg[isH], minlength=nreg)
            continue
        sim.step()
        if step % (per * 10) == 1:          # every 10 s: MSD window by species/phase at the window start
            ids = A.view("id").copy(); pos = A.view("pos").copy(); s0 = A.view("sp").copy()
            ph0 = np.where(s0 == 0, sim.M.H.phase_of_bin[sim.M.H.bin_of(A.view("E"))],
                           sim.M.Pr.phase_of_bin[sim.M.Pr.bin_of(A.view("E"))])
            if win is not None:
                common, i0, i1 = np.intersect1d(win[0], ids, return_indices=True)
                d2 = ((pos[i1] - win[1][i0]) ** 2).sum(1)
                for s in (0, 1):
                    m = win[2][i0] == s
                    np.add.at(move[s][:, 0], win[3][i0][m], d2[m])
                    np.add.at(move[s][:, 1], win[3][i0][m], 1.0)
            win = (ids, pos, s0, ph0)
    gp = np.array(graze_pred)          # (t, theta, reg)
    gr = np.array(graze_real)          # (t, reg)
    err = [float(np.mean(np.abs(gp[:, i].sum(1) - gr.sum(1))) / max(np.mean(gr.sum(1)), 1e-9)) for i in range(len(thetas))]
    err_reg = [float(np.mean(np.abs(gp[:, i] - gr)) / max(np.mean(gr), 1e-9)) for i in range(len(thetas))]
    rows = np.concatenate(pred_rows) if pred_rows else np.zeros((0, 3))
    return dict(thetas=thetas.tolist(), graze_err_total=err, graze_err_region=err_reg,
                graze_bias=[float(gp[:, i].sum() / max(gr.sum(), 1e-9)) for i in range(len(thetas))],
                pred_rows=rows.tolist(), move={s: move[s].tolist() for s in move}, kills=sim.A.kills,
                final=sim.summary())


def fit_holling(rows, dt):
    """Least squares for kills ~ hunters * a n / (1 + a h n) * dt over (a, h) on a log grid."""
    rows = np.asarray(rows)
    m = rows[:, 0] > 0
    Hn, Nn, K = rows[m, 0], rows[m, 1], rows[m, 2]
    best = None
    for a in np.geomspace(1e-5, 1e-1, 81):
        for h in np.concatenate([[0.0], np.geomspace(0.1, 200, 60)]):
            pred = Hn * a * Nn / (1 + a * h * Nn) * dt
            # Poisson deviance
            dev = 2 * np.sum(np.where(K > 0, K * np.log(np.maximum(K, 1e-12) / np.maximum(pred, 1e-12)), 0) - (K - pred))
            if best is None or dev < best[0]:
                best = (dev, a, h)
    return dict(a=float(best[1]), h=float(best[2]), deviance=float(best[0]), n=int(m.sum()),
                kills=float(K.sum()), mean_pred=float((Hn * best[1] * Nn / (1 + best[1] * best[2] * Nn) * dt).sum()))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--R", type=float, default=600.0)
    ap.add_argument("--T", type=float, default=400.0)
    ap.add_argument("--seeds", type=int, default=2)
    a = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    P = Params()
    runs = [run(a.R, a.T, seed=s + 1, P=P) for s in range(a.seeds)]
    rows = sum((r["pred_rows"] for r in runs), [])
    hol = fit_holling(rows, P.dt_macro)
    mv = {s: np.sum([r["move"][s] for r in runs], 0) for s in (0, 1)}
    # D = <|dx|^2> / (6 * 10 s); per-neighbour hop rate on a cubic lattice of spacing L is D / L^2
    hop = {SPN: [float(mv[s][ph, 0] / max(mv[s][ph, 1], 1e-9) / 60.0 / P.L ** 2) for ph in range(3)]
           for s, SPN in ((0, "herb"), (1, "pred"))}
    hop_n = {SPN: [int(mv[s][ph, 1]) for ph in range(3)] for s, SPN in ((0, "herb"), (1, "pred"))}
    gerr = np.mean([r["graze_err_total"] for r in runs], 0)
    gbias = np.mean([r["graze_bias"] for r in runs], 0)
    th = runs[0]["thetas"]
    res = dict(theta=th[int(np.argmin(gerr))], graze_err_total=dict(zip(map(str, th), gerr.tolist())),
               graze_bias=dict(zip(map(str, th), gbias.tolist())),
               holling=hol, hop_rate_per_neighbour=hop, hop_samples=hop_n, final=[r['final'] for r in runs], kills=[r["kills"] for r in runs])
    with open(os.path.join(OUT, "calibration.json"), "w") as fh:
        json.dump(res, fh, indent=1)
    print(json.dumps(res, indent=1))


if __name__ == "__main__":
    main()
