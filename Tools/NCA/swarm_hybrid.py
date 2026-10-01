"""Hybrids of single-plan specialists (round 1b).

Every specialist is the same base rule (G2) fine-tuned on one body plan, so its genome is
base + tau_k with tau_k = theta_k - theta_base. Networks fine-tuned from one start stay in one basin,
so task vectors can be ADDED (task arithmetic / model soups):

    hybrid(a) = theta_base + sum_k a_k * tau_k

plus per-layer CROSSOVER (each weight tensor's task vector taken from one specialist). This script
builds a population of such hybrids, scores each with the shared yardstick (swarm_nca.rollout +
tests_passed, all four seedings and all four switches), and writes the ranked population. The best
genomes seed the next combined-loss search: `--set init=<hybrid>.pt` for gradient training, or the
whole population for evolution.

    python Tools/NCA/swarm_hybrid.py --base Tools/NCA/results/swarm_coevo_g2/rule.pt \
        --spec mass=Tools/NCA/results/solo_mass/rule_latest.pt --spec space=... \
        --out Tools/NCA/results/hybrid --n 24
"""
import argparse
import itertools
import json
import os
import random

import torch

import swarm_nca as sn


def load(path):
    st = torch.load(path, weights_only=False, map_location="cpu")
    return st, {k: v.float() for k, v in st["rule"].items()}


def task_vectors(base, specs):
    return {name: {k: sd[k] - base[k] for k in base} for name, sd in specs.items()}


def combine(base, taus, coef, pick=None):
    """theta = base + sum_k coef[k] * tau_k; with `pick` (tensor name -> specialist), that tensor's
    task vector comes from one specialist alone (scaled by the sum of all coefficients)."""
    out = {}
    tot = sum(coef.values())
    for k, v in base.items():
        if pick and k in pick:
            out[k] = v + tot * taus[pick[k]][k]
        else:
            out[k] = v + sum(c * taus[n][k] for n, c in coef.items())
    return out


def population(names, n, seed=0):
    """Named recipes: the plain sum, uniform averages at several strengths, each specialist alone,
    leave-one-out sums, random Dirichlet mixtures, and per-layer crossovers."""
    rng = random.Random(seed)
    pop = [("base", {k: 0.0 for k in names}, None)]
    for s in (0.5, 1.0):
        pop.append((f"sum x{s}", {k: s for k in names}, None))
    pop.append(("mean", {k: 1 / len(names) for k in names}, None))
    for k in names:
        pop.append((f"only {k}", {k2: float(k2 == k) for k2 in names}, None))
    for k in names:
        pop.append((f"all but {k}", {k2: float(k2 != k) for k2 in names}, None))
    while len(pop) < n:
        if rng.random() < 0.5:
            w = [rng.gammavariate(1.0, 1.0) for _ in names]
            s = rng.uniform(0.5, 1.5) / sum(w)
            pop.append((f"mix {len(pop)}", {k: round(x * s, 3) for k, x in zip(names, w)}, None))
        else:
            pop.append((f"cross {len(pop)}", {k: 1 / len(names) for k in names}, "random"))
    return pop[:max(n, len(pop))]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--spec", action="append", default=[], help="plan=path to a specialist rule")
    ap.add_argument("--out", default=os.path.join(sn.HERE, "results", "hybrid"))
    ap.add_argument("--n", type=int, default=24)
    ap.add_argument("--steps", type=int, default=240)
    ap.add_argument("--scale-inv", type=int, default=0)
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    st, base = load(a.base)
    specs = {}
    for s in a.spec:
        name, path = s.split("=", 1)
        specs[name] = load(path)[1]
    names = list(specs)
    taus = task_vectors(base, specs)
    for n_, t in taus.items():
        print(f"tau_{n_}: |tau| = {sum(float(v.norm() ** 2) for v in t.values()) ** 0.5:.3f}")
    rng = random.Random(a.seed)
    L = sn.LossCfg(scale_inv=a.scale_inv)
    rows = []
    for i, (label, coef, cross) in enumerate(population(names, a.n, a.seed)):
        pick = {k: rng.choice(names) for k in base} if cross == "random" and names else None
        sd = combine(base, taus, coef, pick)
        w = st["world"]; w["vmax"] = tuple(w["vmax"])
        rule = sn.SwarmRule(sn.World(**w), hidden=st["hidden"]); rule.load_state_dict(sd)
        with torch.no_grad():
            _, summ = sn.rollout(rule, steps=a.steps, L=L)
        passed, close = sn.tests_passed(summ)
        path = os.path.join(a.out, f"hybrid_{i:02d}.pt")
        torch.save(dict(rule=sd, world=st["world"], hidden=st["hidden"], recipe=dict(label=label, coef=coef, pick=pick)), path)
        rows.append(dict(i=i, label=label, coef=coef, crossover=pick is not None, passed=passed, close=round(close, 2),
                         own={k: summ["cross"][k][k] for k in sn.KINDS}, path=os.path.basename(path)))
        print(f"{i:2d} {label:14s} {passed}/8 close {close:7.2f} " + " ".join(f"{k[:2]} {summ['cross'][k][k]:5.1f}" for k in sn.KINDS), flush=True)
    rows.sort(key=lambda r: (-r["passed"], r["close"]))
    json.dump(rows, open(os.path.join(a.out, "population.json"), "w"), indent=1)
    print("best:", rows[0]["label"], rows[0]["passed"], rows[0]["close"], rows[0]["path"])


if __name__ == "__main__":
    main()
