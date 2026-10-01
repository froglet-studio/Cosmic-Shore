"""ORGANIC-feel metrics for any swarm model (model(sw, gen) -> Swarm, model.world).

The lead judges four gallery approaches by eye (Tools/NCA/DISCOVERIES.md, feel review):
  evo          "beautifully organic... always feels like a swarm but interesting to watch it try to be more"
  evo compact  "very jerky... clusters make planar surfaces with its crystals"   (not desirable)
  field        "lost too much organic imperfection; its mistakes feel like bugs, not emergence"
  hgrid oracle "both accurate and organic"
This file turns those words into numbers that are cheap, documented and model-agnostic. Every
metric is measured on a GROWN body (240 steps from the plan's seed), over a window of `win` steps,
on tadpoles alive through the whole window (array slot = identity).

  jerk      mean |third difference| of position (voxels/step^3), per element and overall.
            Smooth swimming has small jerk; "very jerky" is large. Reported RELATIVE to the mean
            speed too (jerk_rel = jerk / mean |v|), so a fast swarm is not punished for being fast.
  planar    crystal-cluster planarity. For every tadpole, the PCA of its 8 nearest neighbours
            (itself included): flatness = smallest eigenvalue / largest. planar_frac = share of
            tadpoles whose neighbourhood is a sheet (flatness < 0.1). A blob or a body volume has
            flatness ~0.3-0.6; "clusters make planar surfaces" is a large planar_frac. Measured
            against the PLAN's own value (planar_plan) because a jellyfish bell IS a surface.
  coherence mean cosine between a tadpole's velocity and its 6 nearest neighbours' mean velocity.
            ~1 = marching in lock-step (machine), ~0 = brownian (gas). A school is in between.
  jitter    "slot-free jitter": RMS of each tadpole's velocity about its neighbourhood mean, over its
            speed (the imperfection that reads as life: everyone does the same thing slightly
            differently). 0 = rigid, >1 = noise dominates.
  phase     phase diversity: circular spread of each tadpole's dominant motion phase (the angle of
            its velocity in the body's principal plane), 1 - |mean unit phasor|; 0 = all in phase.
  stuck     share of tadpoles that moved < 0.02 voxel/step on average while the body around them
            moves (a bug-like freeze)
  osc       share of steps on which a tadpole reverses (cos(v_t, v_t-1) < -0.5): a twitch/oscillation,
            the bug-like failure of a controller fighting itself.

`in_band()` checks the ORGANIC band (BAND below), calibrated on the lead's review of the gallery.

    python Tools/NCA/swarm_feel.py --model evo:Tools/NCA/results/evo/genome.npy
"""
from __future__ import annotations

import argparse
import json
import math

import torch

import swarm_nca as sn


def _alive(sw):
    return (sw.active[0] & sw.hatched[0]).clone()


@torch.no_grad()
def record(model, kind, seed=7, warm=240, win=64):
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[kind]], model.world, gen)
    for _ in range(warm):
        sw = model(sw, gen)
    P, alive = [], _alive(sw)
    elem = sw.elem[0].clone()
    for _ in range(win + 1):
        P.append(sw.pos[0].clone())
        sw = model(sw, gen)
        alive &= _alive(sw) & (sw.elem[0] == elem)
    return torch.stack(P)[:, alive], elem[alive]                  # [win+1, n, 3], [n]


def _knn(x, k):
    d = torch.cdist(x, x)
    return d.topk(min(k, len(x)), largest=False).indices      # includes self


def flatness(x, k=8):
    idx = _knn(x, k)
    nb = x[idx]                                                 # [n,k,3]
    nb = nb - nb.mean(1, keepdim=True)
    cov = nb.transpose(1, 2) @ nb / k
    ev = torch.linalg.eigvalsh(cov).clamp(min=1e-9)             # ascending
    return ev[:, 0] / ev[:, 2]


def plan_flatness(kind):
    T = sn.load_targets()[kind]
    return float((flatness(T.frames[0]["p"]) < 0.1).float().mean())


def metrics(P, elem, kind=None):
    n = P.shape[1]
    if n < 8:
        return dict(n=n)
    V = P[1:] - P[:-1]                                          # [w,n,3]
    J = P[3:] - 3 * P[2:-1] + 3 * P[1:-2] - P[:-3]
    jn = J.norm(dim=-1)                                         # [w-2,n]
    sp = V.norm(dim=-1)
    out = dict(n=n, speed=round(float(sp.mean()), 3), jerk=round(float(jn.mean()), 3),
               jerk_rel=round(float(jn.mean() / sp.mean().clamp(min=1e-6)), 3))
    out["jerk_e"] = {sn.ELEMENTS[e]: round(float(jn[:, elem == e].mean()), 3) for e in range(4) if int((elem == e).sum()) >= 3}
    fl = torch.stack([flatness(P[t]) for t in range(0, P.shape[0], 8)])
    out["planar_frac"] = round(float((fl < 0.1).float().mean()), 3)
    out["flat_mean"] = round(float(fl.mean()), 3)
    if kind:
        out["planar_plan"] = round(plan_flatness(kind), 3)
    # neighbourhood velocity statistics
    coh, jit = [], []
    for t in range(0, V.shape[0], 4):
        idx = _knn(P[t], 7)[:, 1:]
        vm = V[t][idx].mean(1)
        v = V[t]
        cs = (v * vm).sum(-1) / (v.norm(dim=-1) * vm.norm(dim=-1)).clamp(min=1e-6)
        coh.append(cs.mean())
        jit.append(((v - vm).norm(dim=-1) / v.norm(dim=-1).clamp(min=1e-3)).clamp(max=5).mean())
    out["coherence"] = round(float(torch.stack(coh).mean()), 3)
    out["jitter"] = round(float(torch.stack(jit).mean()), 3)
    # phase: velocity angle in the body's principal plane
    c = P[0] - P[0].mean(0)
    _, _, Vt = torch.linalg.svd(c, full_matrices=False)
    a, b = Vt[0], Vt[1]
    ang = torch.atan2((V * b).sum(-1), (V * a).sum(-1))         # [w,n]
    ph = torch.stack([torch.cos(ang), torch.sin(ang)], -1).mean(0)
    ph = ph / ph.norm(dim=-1, keepdim=True).clamp(min=1e-6)
    out["phase"] = round(float(1 - ph.mean(0).norm()), 3)
    body = float(sp.mean())
    out["stuck"] = round(float(((sp.mean(0) < 0.02) & (body > 0.05)).float().mean()), 3)
    cosv = (V[1:] * V[:-1]).sum(-1) / (V[1:].norm(dim=-1) * V[:-1].norm(dim=-1)).clamp(min=1e-6)
    out["osc"] = round(float((cosv < -0.5).float().mean()), 3)
    return out


def feel(model, kinds=sn.KINDS, seed=7, warm=240, win=64):
    res = {}
    for k in kinds:
        P, e = record(model, k, seed, warm, win)
        res[k] = metrics(P, e, k)
    keys = ["speed", "jerk", "jerk_rel", "planar_frac", "flat_mean", "coherence", "jitter", "phase", "stuck", "osc"]
    res["mean"] = {q: round(sum(res[k][q] for k in kinds if q in res[k]) / len(kinds), 3) for q in keys}
    return res


# The organic band, calibrated on the lead's review (results/hgrid2/feel.json "calibration"):
#   jerk_rel in [0.2, 2.5]   below 0.2 is machine-smooth (field 0.15, "too clean"); evo's brownian 1.9 is organic
#   osc <= 0.08              twitching / reversing in place: evo compact 0.17 ("very jerky"); all others <= 0.06
#   stuck <= 0.02            frozen tadpoles in a moving body: field 0.10 ("mistakes feel like bugs")
#   planar_excess <= 0.15    worst plan's planar_frac above the PLAN's own: compact's jellyfish +0.23 ("planar
#                            surfaces with its crystals"); a body that matches its plan is as flat as the plan
# It sorts the four reviewed approaches exactly as the lead did (evo, hgrid oracle in; compact, field out).
# coherence / jitter / phase are descriptive (how school-like vs gas-like), not pass/fail.
BAND = dict(jerk_rel=(0.2, 2.5), osc_max=0.08, stuck_max=0.02, planar_excess_max=0.15)


def planar_excess(res):
    return max(max(0.0, res[k]["planar_frac"] - res[k].get("planar_plan", 0.0)) for k in sn.KINDS)


def in_band(res, band=None):
    band = band or BAND
    m = res["mean"]
    checks = dict(jerk_rel=band["jerk_rel"][0] <= m["jerk_rel"] <= band["jerk_rel"][1], osc=m["osc"] <= band["osc_max"],
                  stuck=m["stuck"] <= band["stuck_max"], planar=planar_excess(res) <= band["planar_excess_max"])
    return all(checks.values()), checks


def main():
    import swarm_eval
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    torch.set_num_threads(4)
    res = feel(swarm_eval.load_model(a.model))
    ok, checks = in_band(res)
    res["organic"] = dict(ok=ok, checks=checks, planar_excess=round(planar_excess(res), 3))
    print(json.dumps(res, indent=1))
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
