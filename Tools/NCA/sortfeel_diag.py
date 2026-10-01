"""sortfeel step 1: WHERE are sort's flat neighbourhoods and WHICH mechanism makes them.

For each plan, grow the published sort model (results/sort/params.json) and measure swarm_feel's
planar_frac per ablation, plus a breakdown of the flat tadpoles by location:
  boundary   at least 2 of its 7 nearest neighbours are of a different (element, region) TYPE
  shell      in the outer 25% of radii from the body centre
  interior   neither
and a "crystal" statistic: the coefficient of variation of nearest-neighbour distance (a packed lattice
has CV ~0; a liquid ~0.15+). Also the own-plan loss of the grown body (sn.score loss vs its own plan).

    python Tools/NCA/sortfeel_diag.py --out Tools/NCA/results/sortfeel/diag.json
"""
import argparse
import copy
import json
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_feel as sf  # noqa: E402
import sort_model as sm  # noqa: E402

BASE = os.path.join(HERE, "results", "sort", "params.json")


def variants(cfg):
    v = {"published": {}}
    v["no_adhesion"] = dict(a_same=0.0, a_elem=0.0, a_role=0.0, a_other=0.0)
    v["symmetric_adhesion"] = dict(a_elem=-0.05, a_role=-0.05, a_other=-0.05)
    v["no_swaps"] = dict(swap=0.0)
    v["no_fate"] = dict(fate=0)
    v["isotropic_wells"] = dict(cov_scale=0.0)          # cov = I only (handled in PlanCode: cov*0 + I)
    v["wide_wells"] = dict(cov_scale=cfg.cov_scale * 2)
    v["weak_well"] = dict(k_well=cfg.k_well * 0.4)
    v["soft_collision"] = dict(k_rep=cfg.k_rep * 0.4)
    v["small_r0"] = dict(r0=cfg.r0 * 0.8)
    v["no_inertia"] = dict(inertia=0.0)
    return v


def make(cfg, **kw):
    d = copy.deepcopy(cfg.__dict__); d.update(kw)
    return sm.SortSwarm(sm.SortCfg(**d))


@torch.no_grad()
def grow(model, kind, seed=7, steps=240):
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[kind]], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    return sw


def breakdown(model, sw, kind):
    al = (sw.active[0] & sw.hatched[0]).numpy()
    P = sw.pos[0].numpy()[al]
    el = sw.elem[0].numpy()[al]; dm = sw.dom[0].numpy()[al]
    if len(P) < 9:
        return dict(n=len(P))
    Pt = torch.tensor(P, dtype=torch.float32)
    fl = sf.flatness(Pt).numpy()
    flat = fl < 0.1
    d = np.linalg.norm(P[:, None] - P[None], axis=-1) + np.eye(len(P)) * 1e9
    nn = np.argsort(d, 1)[:, :7]
    tid = el * 4 + dm
    other = (tid[nn] != tid[:, None]).sum(1)
    boundary = other >= 2
    r = np.linalg.norm(P - P.mean(0), axis=1)
    shell = r >= np.quantile(r, 0.75)
    nnd = d.min(1)
    out = dict(n=int(len(P)), planar=round(float(flat.mean()), 3),
               flat_at_boundary=round(float((flat & boundary).sum() / max(1, flat.sum())), 3),
               share_boundary=round(float(boundary.mean()), 3),
               flat_in_shell=round(float((flat & shell).sum() / max(1, flat.sum())), 3),
               planar_boundary=round(float(flat[boundary].mean()) if boundary.any() else 0, 3),
               planar_interior=round(float(flat[~boundary & ~shell].mean()) if (~boundary & ~shell).any() else 0, 3),
               planar_shell=round(float(flat[shell].mean()), 3),
               nn_mean=round(float(nnd.mean()), 3), nn_cv=round(float(nnd.std() / nnd.mean()), 3))
    return out


def plan_stats(kind):
    T = sn.load_targets()[kind]
    P = T.frames[0]["p"].numpy()
    d = np.linalg.norm(P[:, None] - P[None], axis=-1) + np.eye(len(P)) * 1e9
    nnd = d.min(1)
    return dict(n=len(P), planar=sf.plan_flatness(kind), nn_mean=round(float(nnd.mean()), 3),
                nn_cv=round(float(nnd.std() / nnd.mean()), 3))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="")
    ap.add_argument("--only", default="")
    a = ap.parse_args()
    torch.set_num_threads(4)
    base = sm.load(BASE).cfg
    res = {"plan": {k: plan_stats(k) for k in sn.KINDS}}
    print(json.dumps(res["plan"]))
    for name, kw in variants(base).items():
        if a.only and name not in a.only.split(","):
            continue
        model = make(base, **kw)
        row = {}
        for k in sn.KINDS:
            sw = grow(model, k)
            row[k] = breakdown(model, sw, k)
        res[name] = row
        print(name, json.dumps({k: (row[k].get("planar"), row[k].get("flat_at_boundary"), row[k].get("nn_cv")) for k in sn.KINDS}), flush=True)
    if a.out:
        os.makedirs(os.path.dirname(a.out), exist_ok=True)
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
