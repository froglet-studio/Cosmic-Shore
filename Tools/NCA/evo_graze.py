"""Gradual predation instead of the yardstick's one-shot cull: after growing 240 steps, a predator eats
ONE tadpole of the old majority element every `every` steps until the yardstick's target element leads
(or `max_eat` are eaten), while the rule keeps running; then 240 more steps. A one-shot cull opens
~100-200 free slots at once; grazing opens one at a time, so the rule must keep winning each refill.

    python Tools/NCA/evo_graze.py g2:results/evo/genome.npy base:-
"""
import json
import os
import sys
from multiprocessing import Pool

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo_compare as ec  # noqa: E402
import evo_model as em  # noqa: E402
import swarm_nca as sn  # noqa: E402


@torch.no_grad()
def graze_rollout(model, seed, every=2, max_eat=400, L=None):
    L = L or sn.LossCfg()
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[k] for k in sn.KINDS], model.world, gen)
    for _ in range(240):
        sw = model(sw, gen)
    old = [int(torch.bincount(sw.elem[b][sw.active[b] & sw.hatched[b]], minlength=4).argmax()) for b in range(4)]
    eaten = [0] * 4; flipped = [None] * 4
    t = 0
    while t < max_eat * every:
        if t % every == 0:
            for b, k in enumerate(sn.KINDS):
                if flipped[b] is not None or eaten[b] >= max_eat:
                    continue
                a = sw.active[b] & sw.hatched[b]
                c = torch.bincount(sw.elem[b][a], minlength=4)
                if int(c.argmax()) == sn.SWITCH_TO[k]:
                    flipped[b] = t; continue
                idx = (a & (sw.elem[b] == old[b])).nonzero().squeeze(1)
                if len(idx) == 0:
                    continue
                i = idx[torch.randint(len(idx), (1,), generator=gen)]
                sw.active[b, i] = False; sw.hatched[b, i] = False; sw.s[b, i] = 0.0
                eaten[b] += 1
        if all(f is not None for f in flipped):
            break
        sw = model(sw, gen)
        t += 1
    for _ in range(240):
        sw = model(sw, gen)
    out = {}
    for b, k in enumerate(sn.KINDS):
        new = sn.PLAN_OF[sn.SWITCH_TO[k]]
        x = sn.decode(sw, b)
        row = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        n = int((sw.active[b] & sw.hatched[b]).sum())
        ok = n >= sn.MIN_TEST_BODY and row[new] < min(v for kk, v in row.items() if kk != new)
        out[k] = dict(to=new, ok=bool(ok), eaten=eaten[b], flipped_after=flipped[b], n=n,
                      elements=torch.bincount(sw.elem[b][sw.active[b] & sw.hatched[b]], minlength=4).tolist())
    return out


def job(args):
    kind, path, seed = args
    torch.set_num_threads(1)
    m = ec.make(kind, path)
    return graze_rollout(m, seed)


def main():
    seeds = [700 + i for i in range(8)]
    res = {}
    with Pool(int(os.environ.get("EVO_PROCS", "4"))) as pool:
        for spec in sys.argv[1:]:
            kind, path = spec.split(":", 1)
            r = pool.map(job, [(kind, path, s) for s in seeds])
            per = {k: sum(x[k]["ok"] for x in r) for k in sn.KINDS}
            res[spec] = dict(switch_tests_passed_of_8_seeds=per, mean_switches=round(sum(per.values()) / len(seeds), 3),
                             eaten_mean={k: round(float(np.mean([x[k]["eaten"] for x in r])), 1) for k in sn.KINDS},
                             never_flipped={k: sum(x[k]["flipped_after"] is None for x in r) for k in sn.KINDS})
            print(spec, json.dumps(res[spec]), flush=True)
    json.dump(res, open(os.path.join(HERE, "results", "evo", "graze.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
