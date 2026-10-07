"""Does the published emergent rule USE its secreted chemicals? Grows the jellyfish with the rule as trained and
with the chemical inputs cut (the w1 columns that read the water zeroed), seeds 7/23/41/1000, 600 steps, and reports
the static best-frame divergence (default LossCfg) at steps 300 and 600, plus the learned chemical physics.

    python Tools/NCA/emergent_chem_ablate.py --rule Tools/NCA/results/emergent/rule.pt --out Tools/NCA/results/emergent/ablation_chem.json
"""
import argparse, json
import numpy as np
import torch
import emergent_model as em
import swarm_nca as sn


@torch.no_grad()
def grow(rule, seed, steps=600):
    T = sn.load_targets()["space"]; L = sn.LossCfg()
    gen = sn.make_gen(seed); sw = sn.seed_swarm([T], rule.world, gen); out = []
    for t in range(steps):
        sw = rule(sw, gen)
        if t + 1 in (300, 600):
            out.append(round(sn.swarm_loss(sn.decode(sw, 0), T, L)[1]["sink"], 2))
    return out


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--rule", required=True); ap.add_argument("--out", default="")
    a = ap.parse_args(); torch.set_num_threads(4)
    res = {}
    for lab in ("as trained", "chemical inputs cut"):
        r = em.load_rule(a.rule)
        if lab != "as trained":
            with torch.no_grad():
                r.w1[:, r.F_local:] = 0
        rows = {s: grow(r, s) for s in (7, 23, 41, 1000)}
        res[lab] = dict(per_seed=rows, mean_300=round(float(np.mean([v[0] for v in rows.values()])), 2),
                        mean_600=round(float(np.mean([v[1] for v in rows.values()])), 2))
        print(lab, res[lab], flush=True)
    r = em.load_rule(a.rule)
    D, lam = r.chem_rates()
    w = r.w1.detach()
    res["chem_physics"] = dict(D=[round(float(x), 4) for x in D], decay=[round(float(x), 4) for x in lam],
                               range_voxels=[round(float(em.CELL * (d / l).sqrt()), 1) for d, l in zip(D, lam)])
    res["input_weight_norm"] = dict(local_mean_col=round(float(w[:, :r.F_local].norm(dim=0).mean()), 4),
                                    chem_mean_col=round(float(w[:, r.F_local:].norm(dim=0).mean()), 4))
    print(res["chem_physics"], res["input_weight_norm"])
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
