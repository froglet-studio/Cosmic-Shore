"""Behaviour and feel metrics for a recorded predator/prey encounter (arms_sim.py). numpy only.

A recording is a dict of per-step arrays for ONE encounter (B index already taken):
  qp [T, Nq, 3], qv [T, Nq, 3], qa [T, Nq] alive;  pp [T, Np, 3], pv [T, Np, 3], pa [T, Np], pb [T, Np] bursting
  events: list of (t, pred, prey, x, y, z) catches;  dt;  R_cell
Every behaviour NAME used in NOTE.md is one of the numbers below, each against a null where one exists.

PREY
  polarisation_global   |mean unit velocity| over all prey (1 = everyone the same way)
  polarisation_local    mean over prey with >= 2 neighbours within 15 u of |mean unit velocity of itself + them|
  social_share          share of prey with >= 2 neighbours within 15 u (null: 120 uniform prey in the pond ~0.003)
  milling               |mean over a group of (r x v)/(|r||v|) . axis| about the group centroid (1 = a mill/torus)
  groups                clusters (single linkage, 8 u) with >= 3 prey; largest_share = largest cluster / alive
  nnd                   mean nearest-neighbour distance (u)
  split_on_attack       groups 1.5 s after a predator first comes within 20 u of a group, minus groups before
  flash_expansion       relative NND change around the attacked prey over the same window
  confusion_fail_share  share of contacts that failed on the confusion roll (crowd protects)
PREDATORS
  encirclement          for each (prey-group centroid, >= 2 predators within 50 u): 1 - |mean unit vector from the
                        centroid to those predators|; reported with the null for the same predator counts drawn
                        uniformly on the sphere (a pack attacking from all sides scores above the null)
  opposed_share         share of those moments with two predators more than 120 deg apart around the group
  pred_spacing          mean nearest-predator distance (u); pack_share = share of predators with a packmate < 40 u
  burst_bouts           burst runs: mean length (s), share of bouts that end in (or within 1 s of) a catch
  relay_share           catches where a DIFFERENT predator was nearest the victim 2 s earlier (within 40 u)
  membrane_catch_ratio  share of catches beyond 0.8 R / share of prey-time beyond 0.8 R (>1 = pinned on the wall)
  ambush_share          catches whose catcher averaged < 40% cruise speed over [-3 s, -1 s] before the catch
  food_bias             predator time-share in the richest-food third of cells / that third's volume share
TELEGRAPH
  converge              predators within 40 u of the victim, mean over [-4 s, -3 s] vs at the catch
  lead_time             seconds between the catcher's last approach start (distance to victim starts falling
                        steadily) and the catch: how long a hunt is visible before it lands
FEEL (swarm_feel's definitions in numpy, per species; positions sampled every step)
  jerk_rel, osc, stuck (mean speed < 2.5% of top speed: swarm_feel's 0.02 of 0.8), coherence, jitter.
  swarm_feel's organic BAND: jerk_rel in [0.2, 2.5], osc <= 0.08, stuck <= 0.02 (planar_excess has no plan here).
"""
from __future__ import annotations

import math

import numpy as np

BAND = dict(jerk_rel=(0.2, 2.5), osc_max=0.08, stuck_max=0.02)


# ----------------------------------------------------------------------------------------------- helpers ---

def clusters(P, r=8.0):
    """single-linkage labels for points P [n,3] at distance r (union-find over the dense distance matrix)."""
    n = len(P)
    if n == 0:
        return np.zeros(0, int)
    d = np.sqrt(((P[:, None] - P[None]) ** 2).sum(-1))
    lab = np.arange(n)
    adj = d < r
    # label propagation (min label over neighbours) until stable
    for _ in range(n):
        new = np.where(adj, lab[None, :], n).min(1)
        new = np.minimum(new, lab)
        new = new[new]
        if (new == lab).all():
            break
        lab = new
    _, lab = np.unique(lab, return_inverse=True)
    return lab


def _unit(v):
    return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-6)


def _null_coverage(k, rng, n=4000):
    u = _unit(rng.normal(size=(n, k, 3)))
    return float((1 - np.linalg.norm(u.mean(1), axis=-1)).mean())


# ---------------------------------------------------------------------------------------------- prey -------

def prey_metrics(rec, every=5):
    qp, qv, qa = rec["qp"], rec["qv"], rec["qa"]
    T = len(qp)
    pol_g, pol_l, mill, groups, largest, nnd, social = [], [], [], [], [], [], []
    for t in range(0, T, every):
        a = qa[t]
        P, V = qp[t][a], qv[t][a]
        if len(P) < 5:
            continue
        U = _unit(V)
        pol_g.append(np.linalg.norm(U.mean(0)))
        d = np.sqrt(((P[:, None] - P[None]) ** 2).sum(-1))
        m = d < 15.0
        grp = m.sum(1) >= 3                                  # itself + at least 2 neighbours
        if grp.any():
            pl = np.linalg.norm((m[..., None] * U[None]).sum(1) / m.sum(1, keepdims=True), axis=-1)
            pol_l.append(pl[grp].mean())
        social.append(grp.mean())
        np.fill_diagonal(d, np.inf)
        nnd.append(d.min(1).mean())
        lab = clusters(P)
        cnt = np.bincount(lab)
        groups.append(int((cnt >= 3).sum()))
        largest.append(cnt.max() / len(P))
        # milling of each group of >= 8, weighted by size
        ms, ws = [], []
        for g in np.nonzero(cnt >= 8)[0]:
            sel = lab == g
            r = P[sel] - P[sel].mean(0)
            c = np.cross(_unit(r), U[sel])
            ax = c.mean(0)
            ms.append(np.linalg.norm(ax)); ws.append(sel.sum())
        if ms:
            mill.append(np.average(ms, weights=ws))
    out = dict(polarisation_global=_r(np.mean(pol_g)), polarisation_local=_r(np.mean(pol_l)) if pol_l else None,
               social_share=_r(np.mean(social)),
               milling=_r(np.mean(mill)) if mill else None, groups=_r(np.mean(groups)),
               largest_share=_r(np.mean(largest)), nnd=_r(np.mean(nnd)))
    out.update(_attack_response(rec))
    return out


def _attack_response(rec, near=20.0, win=1.5):
    """split_on_attack / flash_expansion: compare the group structure around a prey 0.5 s before and win s after
    a predator first closes within `near` u of it (one sample per predator approach episode)."""
    qp, qa, pp, pa, dt = rec["qp"], rec["qa"], rec["pp"], rec["pa"], rec["dt"]
    T = len(qp)
    k0, k1 = int(0.5 / dt), int(win / dt)
    splits, expand = [], []
    was = np.zeros(pp.shape[1], bool)
    for t in range(k0, T - k1, 2):
        a = qa[t]
        if a.sum() < 5:
            continue
        d = np.sqrt(((pp[t][:, None] - qp[t][None]) ** 2).sum(-1))
        d[:, ~a] = np.inf
        close = (d.min(1) < near) & pa[t]
        for p in np.nonzero(close & ~was)[0]:
            q = int(d[p].argmin())
            def local(tt):
                aa = qa[tt]
                P = qp[tt][aa]
                c = qp[tt][q] if qa[tt][q] else qp[t][q]
                sel = np.linalg.norm(P - c, axis=1) < 30.0
                if sel.sum() < 4:
                    return None
                L = clusters(P[sel])
                dd = np.sqrt(((P[sel][:, None] - P[sel][None]) ** 2).sum(-1)); np.fill_diagonal(dd, np.inf)
                return (np.bincount(L) >= 2).sum(), dd.min(1).mean()
            b, f = local(t - k0), local(t + k1)
            if b and f:
                splits.append(f[0] - b[0]); expand.append(f[1] / b[1] - 1)
        was = close
    return dict(split_on_attack=_r(np.mean(splits)) if splits else None,
                flash_expansion=_r(np.mean(expand)) if expand else None, attack_samples=len(splits))


# ------------------------------------------------------------------------------------------- predators -----

def pred_metrics(rec, cfg, every=5, seed=0):
    qp, qa, pp, pv, pa, pb, dt = rec["qp"], rec["qa"], rec["pp"], rec["pv"], rec["pa"], rec["pb"], rec["dt"]
    T = len(qp)
    rng = np.random.default_rng(seed)
    cov, opp, ks, spacing, packs = [], [], [], [], []
    for t in range(0, T, every):
        P = qp[t][qa[t]]
        A = pp[t][pa[t]]
        if len(A) >= 2:
            d = np.sqrt(((A[:, None] - A[None]) ** 2).sum(-1)); np.fill_diagonal(d, np.inf)
            spacing.append(d.min(1).mean()); packs.append((d.min(1) < 40).mean())
        if len(P) < 3 or len(A) < 2:
            continue
        lab = clusters(P)
        for g in np.nonzero(np.bincount(lab) >= 3)[0]:
            c = P[lab == g].mean(0)
            v = A - c
            dist = np.linalg.norm(v, axis=1)
            sel = dist < 50.0
            if sel.sum() >= 2:
                u = _unit(v[sel])
                cov.append(1 - np.linalg.norm(u.mean(0))); ks.append(int(sel.sum()))
                cs = u @ u.T
                opp.append(float((cs < math.cos(math.radians(120))).any()))
    null = {k: _null_coverage(k, rng) for k in set(ks)}
    enc_null = float(np.mean([null[k] for k in ks])) if ks else None
    # burst bouts
    bouts, succ = [], []
    ev = rec["events"]
    catch_t = {}
    for (t, p, q, *_x) in ev:
        catch_t.setdefault(p, []).append(t)
    for p in range(pb.shape[1]):
        b = pb[:, p].astype(int)
        edges = np.diff(np.concatenate([[0], b, [0]]))
        st, en = np.nonzero(edges == 1)[0], np.nonzero(edges == -1)[0]
        for s0, e0 in zip(st, en):
            bouts.append((e0 - s0) * dt)
            ct = catch_t.get(p, [])
            succ.append(any(s0 * dt - 1e-6 <= c <= e0 * dt + 1.0 for c in ct))
    # per-catch analyses
    relay, memb, amb, conv0, conv1, lead = [], [], [], [], [], []
    k2, k3, k1, k4 = int(2 / dt), int(3 / dt), int(1 / dt), int(4 / dt)
    for (t, p, q, x, y, z) in ev:
        ti = int(round(t / dt))
        memb.append(np.linalg.norm([x, y, z]) > 0.8 * rec["R_cell"])
        if ti - k2 >= 0 and qa[ti - k2][q]:
            d = np.linalg.norm(pp[ti - k2] - qp[ti - k2][q], axis=1); d[~pa[ti - k2]] = np.inf
            j = int(d.argmin())
            if d[j] < 40:
                relay.append(j != p)
        if ti - k3 >= 0:
            sp = np.linalg.norm(pv[ti - k3:ti - k1, p], axis=1).mean()
            amb.append(sp < 0.4 * cfg.pred_v)
        if ti - k4 >= 0:
            def nclose(tt):
                d = np.linalg.norm(pp[tt] - qp[tt][q], axis=1); return int(((d < 40) & pa[tt]).sum())
            conv0.append(np.mean([nclose(tt) for tt in range(ti - k4, ti - k3)])); conv1.append(nclose(max(ti - 1, 0)))
        # lead time: walk back while the catcher's distance to the victim keeps falling (allowing 0.3 s wobbles)
        dist = [np.linalg.norm(pp[tt, p] - qp[tt, q]) for tt in range(max(0, ti - int(15 / dt)), ti)]
        if len(dist) > 2:
            dist = np.array(dist[::-1])                       # from the catch backwards
            k, slack = 0, 0
            while k + 1 < len(dist):
                if dist[k + 1] >= dist[k] - 1e-3:
                    k += 1; slack = 0
                else:
                    slack += 1
                    if slack > int(0.3 / dt):
                        break
                    k += 1
            lead.append(k * dt)
    # membrane null: share of prey-time beyond 0.8 R
    rr = np.linalg.norm(qp, axis=-1)
    memb_null = float((rr[qa] > 0.8 * rec["R_cell"]).mean())
    out = dict(encirclement=_r(np.mean(cov)) if cov else None, encirclement_null=_r(enc_null),
               encirclement_samples=len(cov), opposed_share=_r(np.mean(opp)) if opp else None,
               pred_spacing=_r(np.mean(spacing)) if spacing else None, pack_share=_r(np.mean(packs)) if packs else None,
               burst_bout_s=_r(np.mean(bouts)) if bouts else None, burst_bouts=len(bouts),
               burst_success=_r(np.mean(succ)) if succ else None,
               relay_share=_r(np.mean(relay)) if relay else None, relay_samples=len(relay),
               membrane_catch_share=_r(np.mean(memb)) if memb else None, membrane_prey_share=_r(memb_null),
               membrane_catch_ratio=_r(np.mean(memb) / max(memb_null, 1e-6)) if memb else None,
               ambush_share=_r(np.mean(amb)) if amb else None,
               converge_before=_r(np.mean(conv0)) if conv0 else None, converge_at=_r(np.mean(conv1)) if conv1 else None,
               lead_time_s=_r(np.median(lead)) if lead else None, lead_time_mean_s=_r(np.mean(lead)) if lead else None,
               catches=len(ev))
    if "food" in rec:
        out["food_bias"] = _food_bias(rec, cfg)
    return out


def _food_bias(rec, cfg):
    f = rec["food"]                                            # [G,G,G] template
    G = cfg.G
    rich = f >= np.quantile(f[f > 0], 2 / 3) if (f > 0).any() else f > 0
    pp, pa = rec["pp"], rec["pa"]
    idx = np.clip(((pp + cfg.R_cell) / (2 * cfg.R_cell) * G).astype(int), 0, G - 1)
    inrich = rich[idx[..., 0], idx[..., 1], idx[..., 2]]
    share = float(inrich[pa].mean())
    c = (np.arange(G) + 0.5) / G * 2 * cfg.R_cell - cfg.R_cell
    X, Y, Z = np.meshgrid(c, c, c, indexing="ij")
    inside = np.sqrt(X ** 2 + Y ** 2 + Z ** 2) < cfg.R_cell
    vol = float(rich[inside].mean())
    return _r(share / max(vol, 1e-6))


# -------------------------------------------------------------------------------------------------- feel ---

def feel(P, A, vmax):
    """swarm_feel's metrics on trajectories P [T, n, 3] for agents alive through the whole window (mask A [T,n])."""
    keep = A.all(0)
    P = P[:, keep]
    n = P.shape[1]
    if n < 4 or len(P) < 8:
        return dict(n=int(n))
    V = P[1:] - P[:-1]
    J = P[3:] - 3 * P[2:-1] + 3 * P[1:-2] - P[:-3]
    jn = np.linalg.norm(J, axis=-1); sp = np.linalg.norm(V, axis=-1)
    out = dict(n=int(n), speed=_r(sp.mean()), jerk_rel=_r(jn.mean() / max(sp.mean(), 1e-6)))
    coh, jit = [], []
    k = min(7, n)
    for t in range(0, len(V), 4):
        d = np.sqrt(((P[t][:, None] - P[t][None]) ** 2).sum(-1))
        idx = np.argsort(d, 1)[:, 1:k]
        vm = V[t][idx].mean(1); v = V[t]
        cs = (v * vm).sum(-1) / np.maximum(np.linalg.norm(v, axis=-1) * np.linalg.norm(vm, axis=-1), 1e-6)
        coh.append(cs.mean())
        jit.append(np.clip(np.linalg.norm(v - vm, axis=-1) / np.maximum(np.linalg.norm(v, axis=-1), 1e-3), None, 5).mean())
    out["coherence"] = _r(np.mean(coh)); out["jitter"] = _r(np.mean(jit))
    step_vmax = vmax
    out["stuck"] = _r(float((sp.mean(0) < 0.025 * step_vmax).mean()))
    cosv = (V[1:] * V[:-1]).sum(-1) / np.maximum(np.linalg.norm(V[1:], axis=-1) * np.linalg.norm(V[:-1], axis=-1), 1e-6)
    out["osc"] = _r(float((cosv < -0.5).mean()))
    ok = dict(jerk_rel=BAND["jerk_rel"][0] <= out["jerk_rel"] <= BAND["jerk_rel"][1], osc=out["osc"] <= BAND["osc_max"],
              stuck=out["stuck"] <= BAND["stuck_max"])
    out["organic"] = dict(ok=all(ok.values()), checks=ok)
    return out


def _r(x, k=3):
    return None if x is None else round(float(x), k)
