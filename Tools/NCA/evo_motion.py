"""Motion stats for any swarm model (what a player would SEE): per element mean per-step heart speed of
live tadpoles over a window, and the swarm's centroid drift and RMS-radius breathing.

    python Tools/NCA/evo_motion.py --model g2 --genome results/evo/genome.npy
"""
import argparse
import json
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402


@torch.no_grad()
def motion(model, seed=5, grow=200, window=80):
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[k] for k in sn.KINDS], model.world, gen)
    for _ in range(grow):
        sw = model(sw, gen)
    sp = np.zeros((4, 4)); cnt = np.zeros((4, 4)); rms = [[] for _ in range(4)]; cen = [[] for _ in range(4)]
    for _ in range(window):
        a0 = sw.active & sw.hatched; p0 = sw.pos.clone()
        sw = model(sw, gen)
        a1 = a0 & sw.active & sw.hatched
        d = (sw.pos - p0).norm(dim=-1)
        for b in range(4):
            m = sw.active[b] & sw.hatched[b]; p = sw.pos[b][m]; c = p.mean(0)
            rms[b].append(float(((p - c) ** 2).sum(-1).mean().sqrt())); cen[b].append(c.numpy())
            for e in range(4):
                sel = a1[b] & (sw.elem[b] == e)
                if sel.any():
                    sp[b, e] += float(d[b][sel].sum()); cnt[b, e] += int(sel.sum())
    out = {}
    for b, k in enumerate(sn.KINDS):
        out[k] = dict(speed={sn.ELEMENTS[e]: round(sp[b, e] / cnt[b, e], 3) for e in range(4) if cnt[b, e]},
                      target_speed_per_frame={sn.ELEMENTS[e]: round(v, 2) for e, v in enumerate(T[k].speed)},
                      rms_mean=round(float(np.mean(rms[b])), 2), rms_breath=round(float(np.std(rms[b])), 3),
                      drift_per_step=round(float(np.linalg.norm(cen[b][-1] - cen[b][0]) / window), 3))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="g2", choices=["g2", "compact", "base"])
    ap.add_argument("--genome", default="")
    a = ap.parse_args()
    torch.set_num_threads(4)
    if a.model == "base":
        m = sn.load_rule(os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt"))
    elif a.model == "compact":
        import evo_compact as ec
        m = ec.CompactRule(np.load(a.genome))
    else:
        import evo_model as em
        m = em.EvoRule(np.load(a.genome))
    print(json.dumps(motion(m), indent=1))


if __name__ == "__main__":
    main()
