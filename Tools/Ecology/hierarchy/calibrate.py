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
from .sim import HierSim, phase_of

OUT = os.path.join(os.path.dirname(__file__), "results")


def macro_graze_prediction(sim, thetas):
    """The macro grazing formula evaluated on the CURRENT micro state, per theta. Returns (len(thetas), nreg)."""
    W, P, A = sim.W, sim.P, sim.A
    n = A.n
    reg = np.maximum(W.region_of(A.view("pos")), 0)
    isH = A.view("sp") == 0
    E = A.view("E")
    sat = np.clip(1 - E / HERB.e_max, 0, 1)
    satb = sat
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


def run(R=600.0, T=400.0, seed=1, P=None, dens=50.0, pred_frac=0.09, flora_fill=0.5):
    P = P or Params()
    sim = HierSim(P, seed=seed, R=R)
    nreg = sim.W.nreg
    sim.populate(int(nreg * dens), int(nreg * dens * pred_frac), flora_fill=flora_fill)
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
            ph0 = np.where(s0 == 0, phase_of(HERB, A.view("E")), phase_of(PRED, A.view("E")))
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
        for h in np.concatenate([[0.0], np.geomspace(0.1, 2000, 70)]):
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
    # three scenarios so every fitted rate is seen over the range it is used in: dense + fed, sparse + fed
    # (low-density predation, which sets the cycle's trough), dense + starved (hungry movement)
    scen = [dict(dens=50.0, flora_fill=0.5), dict(dens=8.0, flora_fill=0.5, pred_frac=0.3),
            dict(dens=40.0, flora_fill=0.04)]
    runs = [run(a.R, a.T, seed=s + 1, P=P, **sc) for s in range(a.seeds) for sc in scen]
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


def hop_probe(P=None, R=600.0, T=120.0, n=3000, seed=3):
    """Movement only: agents of one species held at a fixed stomach (one per phase), no other species, MSD over
    10 s windows -> D -> per-neighbour hop rate D / L^2. Isolates movement from the biology."""
    P = P or Params()
    out = {}
    for s, sp in ((0, HERB), (1, PRED)):
        rates = []
        for frac in (0.9, 0.55, 0.15):            # sated, forage, hungry (fractions of e_birth)
            sim = HierSim(P, seed=seed, R=R)
            sim.W.seed_flora(0.5, 3000)
            sim.force_hot = np.ones(sim.W.nreg, bool)
            pos = sim.W.centers[sim.rng.integers(0, sim.W.nreg, n)] + sim.rng.uniform(-90, 90, (n, 3))
            pos *= np.minimum(1.0, (R * 0.95) / np.linalg.norm(pos, axis=1))[:, None]
            E = frac * sp.e_birth
            sim.A.add(pos, np.full(n, s, np.int8), np.zeros(n, np.int8), np.full(n, E))
            snaps = []
            for i in range(int(T / P.dt_micro)):
                sim.step()
                sim.A.view("E")[:] = E
                if i % 100 == 0:
                    snaps.append(sim.A.view("pos").copy())
            d2 = [((snaps[j + 1] - snaps[j]) ** 2).sum(1).mean() for j in range(2, len(snaps) - 1)]
            D = float(np.mean(d2)) / 60.0
            rates.append(D / P.L ** 2)
        out[sp.name] = rates
    return out


def predation_probe(P=None, R=600.0, T=60.0, dens=(2, 5, 10, 25, 50, 100), n_pred_per_reg=1.0, seed=4):
    """Clean functional response: herbivores at a fixed density (a kill is replaced by a fresh herbivore at a
    random spot), hungry hunters at a fixed stomach, no births / starvation (stomachs pinned). Returns kills per
    hunter per second at each density, and the Holling fit."""
    P = P or Params()
    rows = []
    for d in dens:
        sim = HierSim(P, seed=seed, R=R)
        sim.W.seed_flora(0.5, 3000)
        nr = sim.W.nreg
        sim.force_hot = np.ones(nr, bool)
        nh, npd = int(d * nr), max(1, int(n_pred_per_reg * nr))

        def spots(k):
            p = sim.W.centers[sim.rng.integers(0, nr, k)] + sim.rng.uniform(-100, 100, (k, 3))
            return p * np.minimum(1.0, (R * 0.95) / np.linalg.norm(p, axis=1))[:, None]
        sim.A.add(spots(nh), np.zeros(nh, np.int8), np.zeros(nh, np.int8), np.full(nh, 12.0))
        sim.A.add(spots(npd), np.ones(npd, np.int8), np.zeros(npd, np.int8), np.full(npd, 50.0))
        k0 = 0
        for i in range(int(T / P.dt_micro)):
            sim.step()
            A = sim.A
            isH = A.view("sp") == 0
            A.view("E")[isH] = 12.0
            A.view("E")[~isH] = 50.0
            miss = nh - int(isH.sum())
            if miss > 0:
                sim.A.add(spots(miss), np.zeros(miss, np.int8), np.zeros(miss, np.int8), np.full(miss, 12.0))
        rows.append((d, sim.A.kills / npd / T))
    rows = np.array(rows)
    best = None
    for a in np.geomspace(1e-5, 1e-1, 161):
        for h in np.concatenate([[0.0], np.geomspace(0.1, 2000, 120)]):
            pred = a * rows[:, 0] / (1 + a * h * rows[:, 0])
            err = np.sum((np.log(pred + 1e-9) - np.log(rows[:, 1] + 1e-9)) ** 2)
            if best is None or err < best[0]:
                best = (err, a, h)
    return dict(rows=rows.tolist(), a=float(best[1]), h=float(best[2]), logerr=float(best[0]))


def fit_occupancy(P=None, R=600.0, T=400.0, seed=5, dens=40.0, pred_frac=0.04, scenarios=((0.5, 1.0), (0.15, 1.0), (0.5, 2.0))):
    """Fit the macro occupancy field (occ_theta, occ_tau): shadow fields for every grid point evolve on the REAL
    micro flora field with the macro rule; each predicts the 1-s grazing from the micro herbivores' own satiation.
    Scored by the mean relative error of total grazing per window, summed over scenarios (flora fill, patchiness)."""
    P = P or Params()
    thetas = np.array([0.0, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0])
    taus = np.array([1.0, 5.0, 15.0, 30.0, 60.0, 120.0, 1e9])
    score = np.zeros((len(thetas), len(taus)))
    per = int(round(P.dt_macro / P.dt_micro))
    for fill, patch in scenarios:
        sim = HierSim(P, seed=seed, R=R)
        nr = sim.W.nreg
        sim.W.seed_flora(fill, 3000, patchiness=patch)
        G0 = sim.M.H
        r = sim.rng.choice(nr, int(nr * dens), p=sim.W.F.sum(1) / sim.W.F.sum())
        G0.place(r, sim.rng.integers(0, 4, len(r)), np.clip(sim.rng.normal(12, 2, len(r)), 3, 19))
        rp = sim.rng.choice(nr, int(nr * dens * pred_frac))
        sim.M.Pr.place(rp, sim.rng.integers(0, 4, len(rp)), np.full(len(rp), 60.0))
        sim.force_hot = np.ones(nr, bool)
        W = sim.W
        uni = W.vox_ok / W.vox_ok.sum(1, keepdims=True)
        occ = np.broadcast_to(uni, (len(thetas), len(taus)) + uni.shape).copy()
        err = np.zeros((len(thetas), len(taus))); nwin = 0
        for i in range(int(T / P.dt_micro)):
            if i % per == 0:
                if i > 0:
                    real = sim.A.graze_acc.sum()
                    if real > 0:
                        err += np.abs(pred - real) / real; nwin += 1
                sim.A.graze_acc[:] = 0
                G = W.F + W.K
                A = sim.A
                reg = np.maximum(W.region_of(A.view("pos")), 0)
                isH = A.view("sp") == 0
                sat = np.clip(1 - A.view("E") / HERB.e_max, 0, 1)
                eff = np.bincount(reg[isH], weights=sat[isH], minlength=nr)
                g = G / (G + P.h_half)
                for a, th in enumerate(thetas):
                    tgt = np.power(np.maximum(G, 0), th) * W.vox_ok
                    tgt /= np.maximum(tgt.sum(1, keepdims=True), 1e-12)
                    for b, tau in enumerate(taus):
                        occ[a, b] += (tgt - occ[a, b]) * min(1.0, P.dt_macro / tau)
                want = eff[None, None, :, None] * occ * P.h_intake * g[None, None] * P.dt_macro
                pred = np.minimum(want, G[None, None]).sum(axis=(2, 3))
            sim.step()
        score += err / max(nwin, 1)
    a, b = np.unravel_index(np.argmin(score), score.shape)
    return dict(theta=float(thetas[a]), tau=float(taus[b]), score=score.round(4).tolist(),
                thetas=thetas.tolist(), taus=taus.tolist(), best_err=float(score[a, b] / len(scenarios)),
                static_theta0_err=float(score[0, -1] / len(scenarios)))
