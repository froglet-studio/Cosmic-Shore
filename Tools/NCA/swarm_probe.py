"""Player-facing probes for any swarm model: what happens when a vessel flies through it.

A "model" is anything that steps a swarm_nca.Swarm: `model(sw, gen) -> sw`, with `model.world` a
swarm_nca.World. SwarmRule qualifies; so does any other approach that keeps its state in a Swarm
(extra per-swarm state may live on the model itself, reset when `sw.clock` is 0).

probe() grows every plan from a fresh seed, then strikes it: every tadpole inside a sphere
(the swarm's RMS radius) centred one radius off the centroid is removed, as a vessel
ramming through would. It scores the swarm against its own plan before, right after, and after
`regrow` more steps. A swarm players can carve and watch heal scores recovered < cut, close to
before. Nothing here is a training target; it is a shared yardstick.

    python Tools/NCA/swarm_probe.py --rule Tools/NCA/results/swarm_coevo_g2/rule.pt
"""
import argparse
import json
import torch

import swarm_nca as sn


def _alive(sw):
    return sw.active[0] & sw.hatched[0]


def strike(sw, frac=1.0, gen=None):
    """In place: remove every live tadpole inside a sphere of radius frac x RMS radius, centred
    frac x RMS radius off the centroid along a seeded random direction. Returns how many died."""
    m = _alive(sw)
    p = sw.pos[0][m]
    if len(p) < 4:
        return 0
    c = p.mean(0)
    rms = ((p - c) ** 2).sum(-1).mean().sqrt()
    d = torch.randn(3, generator=gen)
    d = d / d.norm().clamp(min=1e-6)
    hit = m & (((sw.pos[0] - (c + frac * rms * d)) ** 2).sum(-1) < (frac * rms) ** 2)
    sw.active[0, hit] = False; sw.hatched[0, hit] = False; sw.s[0, hit] = 0.0
    return int(hit.sum())


@torch.no_grad()
def probe(model, steps=240, regrow=120, seed=11, L=None):
    L = L or sn.LossCfg()
    targets = sn.load_targets()
    gen = sn.make_gen(seed)
    out = {}
    for k in sn.KINDS:
        sw = sn.seed_swarm([targets[k]], model.world, gen)
        for _ in range(steps):
            sw = model(sw, gen)
        score = lambda: round(sn.swarm_loss(sn.decode(sw, 0), targets[k], L)[1]["sink"], 2)
        r = dict(before=score(), n_before=int(_alive(sw).sum()))
        r["killed"] = strike(sw, gen=gen)
        r["cut"] = score()
        for _ in range(regrow):
            sw = model(sw, gen)
        r["recovered"] = score(); r["n_after"] = int(_alive(sw).sum())
        # heal fraction: 1 = back to the pre-strike score, 0 = no better than right after the cut
        span = r["cut"] - r["before"]
        r["heal"] = round((r["cut"] - r["recovered"]) / span, 3) if span > 1e-6 else None
        out[k] = r
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rule", required=True)
    ap.add_argument("--scale-inv", type=int, default=0)
    a = ap.parse_args()
    rule = sn.load_rule(a.rule)
    print(json.dumps(probe(rule, L=sn.LossCfg(scale_inv=a.scale_inv)), indent=1))


if __name__ == "__main__":
    main()
