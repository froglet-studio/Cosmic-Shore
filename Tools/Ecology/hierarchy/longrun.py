"""Long-run living dynamics of the full cell (macro level; optionally with pilots driving the LOD).

    python -m hierarchy.longrun [--T 7200] [--pilots 0] [--tag name]

Records once per macro step: global counts per species, flora / skeleton / nutrient, hops (migration
flux), kills, births, starvations, occupied-region fractions; every 10 s a regional snapshot of herbivore,
predator and flora density (for the kymographs and synchrony). Writes results/longrun_<tag>.json and the
PNG plots, and prints the liveness metrics:
    min_herb / min_pred        never 0 (no extinction lock)
    cv_herb / cv_pred          > ~0.05 (no freeze: the system breathes)
    max/median                 < ~20 (no runaway)
    period_s                   dominant global cycle period (autocorrelation)
    regional_sync              mean pairwise correlation of regional herbivore series (low = asynchronous
                               patches = metapopulation rescue = the reason the whole stays alive)
    local_extinctions/recols   regions whose herbivores hit 0 and later came back (migration did it)
"""
from __future__ import annotations

import argparse
import json
import os
import time

import numpy as np

from .params import Params
from .sim import HierSim, Pilot

OUT = os.path.join(os.path.dirname(__file__), "results")


def run(T=7200.0, pilots=0, seed=7, P=None, n_herb=45000, n_pred=3000, snap_every=10.0, verbose=True):
    P = P or Params()
    sim = HierSim(P, seed=seed)
    sim.populate(n_herb, n_pred)
    for i in range(pilots):
        sim.add_pilot(Pilot.wanderer(speed=120.0 + 30 * i))
    if pilots == 0:
        sim.lod = False
    W = sim.W
    rows, snaps = [], []
    t0 = time.time()
    per = int(round(P.dt_macro / P.dt_micro))
    hops0 = kills0 = 0
    nmac = int(T / P.dt_macro)
    for k in range(nmac):
        if pilots == 0:
            # macro-only fast path: no micro ticks at all
            W.step_flora(P.dt_macro)
            sim.M.step(P.dt_macro, np.ones(W.nreg, bool), np.zeros(W.nreg, bool))
            sim.t += P.dt_macro
        else:
            for _ in range(per):
                sim.step()
        s = sim.summary()
        M = sim.M
        nH = M.H.count(); nP = M.Pr.count()
        rows.append([sim.t, s["herb"]["count"], s["pred"]["count"], float(W.F.sum()), float(W.K.sum()),
                     float(W.N.sum()), M.hops - hops0, M.kills + sim.A.kills - kills0,
                     float((nH > 0).mean()), float((nP > 0).mean()), sim.A.n, s["herb"]["mean_e"],
                     s["pred"]["mean_e"]] + s["herb"]["phase"] + s["pred"]["phase"])
        hops0, kills0 = M.hops, M.kills + sim.A.kills
        if k % int(snap_every / P.dt_macro) == 0:
            snaps.append(dict(t=sim.t, H=nH.tolist(), P=nP.tolist(), F=W.F.sum(1).round(1).tolist()))
        if verbose and k % 600 == 0:
            print(f"t={sim.t:6.0f} H={s['herb']['count']:6d} P={s['pred']['count']:5d} F={W.F.sum():9.0f} "
                  f"K={W.K.sum():8.0f} N={W.N.sum():9.0f} agents={sim.A.n} ({time.time() - t0:.0f}s)", flush=True)
    rows = np.array(rows)
    return sim, rows, snaps, time.time() - t0


def metrics(rows, snaps, burn=600.0):
    t = rows[:, 0]
    m = t >= burn
    H, Pd = rows[m, 1], rows[m, 2]
    out = dict(min_herb=int(H.min()), min_pred=int(Pd.min()), max_herb=int(H.max()), max_pred=int(Pd.max()),
               mean_herb=float(H.mean()), mean_pred=float(Pd.mean()),
               cv_herb=float(H.std() / H.mean()), cv_pred=float(Pd.std() / max(Pd.mean(), 1e-9)),
               runaway_herb=float(H.max() / max(np.median(H), 1)), runaway_pred=float(Pd.max() / max(np.median(Pd), 1)))
    x = H - H.mean()
    ac = np.correlate(x, x, "full")[len(x) - 1:]
    ac /= max(ac[0], 1e-12)
    # first local max after the first zero crossing
    z = np.flatnonzero(ac < 0)
    period = None
    if len(z):
        j = z[0] + int(np.argmax(ac[z[0]:]))
        if ac[j] > 0.1:
            period = float(j * (t[1] - t[0]))
    out["period_s"] = period
    out["autocorr_at_period"] = float(ac[int(period / (t[1] - t[0]))]) if period else None
    S = np.array([s["H"] for s in snaps if s["t"] >= burn], float)
    if len(S) > 4:
        occ = S.mean(0) > 1.0
        Z = S[:, occ]
        Z = (Z - Z.mean(0)) / np.maximum(Z.std(0), 1e-9)
        C = (Z.T @ Z) / len(Z)
        iu = np.triu_indices(C.shape[0], 1)
        out["regional_sync"] = float(np.nanmean(C[iu]))
        ext = (S[:-1] > 0) & (S[1:] == 0)
        rec = (S[:-1] == 0) & (S[1:] > 0)
        out["local_extinctions"] = int(ext.sum())
        out["recolonisations"] = int(rec.sum())
    out["mean_hops_per_s"] = float(rows[m, 6].mean())
    out["mean_kills_per_s"] = float(rows[m, 7].mean())
    return out


def plot(rows, snaps, sim, path_prefix, title=""):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    t = rows[:, 0] / 60.0
    fig, ax = plt.subplots(4, 1, figsize=(11, 12), constrained_layout=True)
    a = ax[0]
    a.plot(t, rows[:, 1], color="#3a9a5a", lw=1.2, label="herbivores")
    a.set_ylabel("herbivores", color="#3a9a5a")
    b = a.twinx(); b.plot(t, rows[:, 2], color="#c0453a", lw=1.2, label="predators"); b.set_ylabel("predators", color="#c0453a")
    a.set_title(f"{title} - populations (whole 1200-u cell)")
    a = ax[1]
    a.plot(t, rows[:, 3], label="flora F", color="#5a8a2a"); a.plot(t, rows[:, 4], label="skeleton K", color="#8a7a5a")
    a.plot(t, rows[:, 5], label="nutrient N", color="#5a6aaa"); a.legend(loc="upper right", fontsize=8)
    a.set_ylabel("volume"); a.set_title("the closed mass budget: flora / skeleton / soil")
    a = ax[2]
    a.plot(t, rows[:, 8], label="regions with herbivores", color="#3a9a5a")
    a.plot(t, rows[:, 9], label="regions with predators", color="#c0453a")
    b = a.twinx(); b.plot(t, np.convolve(rows[:, 6], np.ones(30) / 30, "same"), color="#888", lw=0.8)
    b.set_ylabel("hops/s (migration, 30 s mean)", color="#888")
    a.set_ylabel("occupied fraction"); a.legend(loc="lower left", fontsize=8); a.set_xlabel("minutes")
    # kymograph: herbivore density along x through the equatorial slab
    W = sim.W
    eq = np.abs(W.centers[:, 2]) < W.P.L * 0.6
    order = np.argsort(W.centers[eq, 0] + 1e-3 * W.centers[eq, 1])
    xs = W.centers[eq, 0][order]
    ux = np.unique(xs)
    K = np.array([[np.mean(np.array(s["H"])[eq][order][xs == u]) for u in ux] for s in snaps])
    a = ax[3]
    a.imshow(K.T, aspect="auto", origin="lower", cmap="viridis",
             extent=[snaps[0]["t"] / 60, snaps[-1]["t"] / 60, ux[0] - W.P.L / 2, ux[-1] + W.P.L / 2])
    a.set_ylabel("x (u), equatorial slab"); a.set_xlabel("minutes")
    a.set_title("kymograph: herbivores per region along x (travelling waves = migration fronts)")
    fig.savefig(path_prefix + "_series.png", dpi=110)
    plt.close(fig)
    fig, ax = plt.subplots(1, 2, figsize=(11, 4.5), constrained_layout=True)
    ax[0].plot(rows[:, 1], rows[:, 2], lw=0.6, color="#555")
    ax[0].set_xlabel("herbivores"); ax[0].set_ylabel("predators"); ax[0].set_title("phase plane")
    Kf = np.array([[np.mean(np.array(s["F"])[eq][order][xs == u]) for u in ux] for s in snaps])
    ax[1].imshow(Kf.T, aspect="auto", origin="lower", cmap="YlGn",
                 extent=[snaps[0]["t"] / 60, snaps[-1]["t"] / 60, ux[0] - W.P.L / 2, ux[-1] + W.P.L / 2])
    ax[1].set_title("flora along x: grazing fronts and regrowth (succession)"); ax[1].set_xlabel("minutes")
    fig.savefig(path_prefix + "_phase.png", dpi=110)
    plt.close(fig)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--T", type=float, default=7200.0)
    ap.add_argument("--pilots", type=int, default=0)
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--tag", default="macro")
    ap.add_argument("--herb", type=int, default=45000)
    ap.add_argument("--pred", type=int, default=3000)
    ap.add_argument("--snap", type=float, default=10.0)
    a = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    sim, rows, snaps, wall = run(a.T, a.pilots, a.seed, n_herb=a.herb, n_pred=a.pred, snap_every=a.snap)
    met = metrics(rows, snaps)
    met["wall_s"] = wall
    met["ledger_drift_rel"] = abs(sim.ledger()["total"] - sim.ledger0["total"]) / sim.ledger0["total"]
    plot(rows, snaps, sim, os.path.join(OUT, f"longrun_{a.tag}"), title=a.tag)
    with open(os.path.join(OUT, f"longrun_{a.tag}.json"), "w") as fh:
        json.dump(dict(metrics=met, cols="t herb pred F K N hops kills occH occP agents eH eP hs hf hh ps pf ph".split(),
                       rows=rows[::5].round(3).tolist()), fh)
    print(json.dumps(met, indent=1))


if __name__ == "__main__":
    main()
