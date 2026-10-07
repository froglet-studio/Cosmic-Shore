"""Which single NON-LOCAL input buys the most? Measured on the closest learned relative that has them.

The solo_space specialist (results/solo_space/rule_latest.pt: G2 fine-tuned on the jellyfish alone) reads,
besides its neighbours, two swarm-wide signals every step: the HEADCOUNT (cnt/100) and the ELEMENT MIX
(4 shares). This replaces each with a constant (the plan's own value) and grows the jellyfish from the
16-tadpole seed, so the loss gap says what each global input is worth to a rule of this family.
Static best-frame divergence (default LossCfg) after `steps`, seeds 7/23/41.

    python Tools/NCA/emergent_ablate.py --out Tools/NCA/results/emergent/ablation_census.json
"""
from __future__ import annotations

import argparse
import inspect
import json
import os
import textwrap

import numpy as np
import torch

import swarm_nca as sn

HERE = os.path.dirname(os.path.abspath(__file__))
SOLO = os.path.join(HERE, "results", "solo_space", "rule_latest.pt")
MIX = torch.tensor([25 / 88, 2 / 88, 54 / 88, 7 / 88])


def _hook(self, sw, cnt, mixe):
    if "cnt" in self.mode:
        cnt = torch.full_like(cnt, float(self.c))
    if "mix" in self.mode:
        mixe = MIX[None].expand_as(mixe)
    return cnt, mixe


def ablatable_rule():
    """SwarmRule whose census can be replaced by constants (source-patched copy of SwarmRule._step)."""
    src = textwrap.dedent(inspect.getsource(sn.SwarmRule._step))
    key = "glob = torch.cat([(cnt / 100)[:, None], mixe], 1)"
    assert key in src
    src = src.replace(key, "cnt, mixe = HOOK(self, sw, cnt, mixe); " + key)
    ns = dict(sn.__dict__); ns["HOOK"] = _hook
    exec(src, ns)
    st = torch.load(SOLO, weights_only=False)
    w = dict(st["world"]); w["vmax"] = tuple(w["vmax"])
    R = type("AblatableRule", (sn.SwarmRule,), {"_step": ns["_step"], "mode": "true", "c": 0})
    r = R(sn.World(**w), st["hidden"]); r.load_state_dict(st["rule"])
    return r


@torch.no_grad()
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--steps", type=int, default=300)
    ap.add_argument("--seeds", default="7,23,41")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    torch.set_num_threads(1)
    T = sn.load_targets()["space"]; L = sn.LossCfg()
    r = ablatable_rule()
    out = {}
    for mode, c, lab in [("true", 0, "both global inputs (as trained)"), ("mix", 0, "headcount only (mix constant)"),
                         ("cnt", 88, "mix only (headcount constant 88)"), ("cnt", 150, "mix only (headcount constant 150)"),
                         ("cntmix", 88, "neither (both constant; = a purely local rule)")]:
        r.mode, r.c = mode, c
        res = []
        for s in [int(x) for x in a.seeds.split(",")]:
            gen = sn.make_gen(s); sw = sn.seed_swarm([T], r.world, gen)
            for _ in range(a.steps):
                sw = r(sw, gen)
            res.append(dict(seed=s, loss=round(sn.swarm_loss(sn.decode(sw, 0), T, L)[1]["sink"], 2),
                            n=int((sw.active & sw.hatched).sum())))
        out[lab] = dict(mean=round(float(np.mean([x["loss"] for x in res])), 2), per_seed=res)
        print(lab, out[lab], flush=True)
    if a.out:
        json.dump(dict(steps=a.steps, rule=os.path.relpath(SOLO, HERE), results=out), open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
