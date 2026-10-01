"""Fast screening for evofate: grow all four plans in ONE batch (per-sample memory on the model), score
own plans, then branch the four standard fair-cull switches (swarm_eval.cull_to) off the grown batch.
Cheaper than swarm_eval.evaluate (which regrows per test for stateful models); used for search and the
pull curve. The binding numbers always come from swarm_eval / scorecard."""
import json
import sys
import time

import numpy as np
import torch

import swarm_nca as sn
import swarm_eval as se
import evofate_model as ef


def clone_mem(model, src, dst_b):
    m = model.mem[src]
    return {k: (v.copy() if isinstance(v, np.ndarray) else (np.random.default_rng(int(m["rng"].integers(1 << 30))) if k == "rng" else v))
            for k, v in m.items()}


@torch.no_grad()
def screen(model, seed=7, steps=240, switch_steps=240, switches="std", L=None):
    L = L or sn.LossCfg()
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    model.mem = {}
    sw = sn.seed_swarm([T[k] for k in sn.KINDS], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    own = {}
    for b, k in enumerate(sn.KINDS):
        row = se._score_row(sw, b, T, L)
        own[k] = dict(loss=row[k], ok=se._passes(row, k, se._alive(sw, b)), n=se._alive(sw, b), row=row)
    out = dict(own=own, sw={})
    if switches:
        std, rest = se.transitions()
        pairs = std if switches == "std" else std + rest
        subs, keys, mems = [], [], {}
        for k, e in pairs:
            b0 = sn.KINDS.index(k)
            sub = sw.index(torch.tensor([b0])).clone()
            if not se.cull_to(sub, 0, e, gen):
                continue
            mems[len(subs)] = clone_mem(model, b0, len(subs))
            subs.append(sub); keys.append((k, e))
        if subs:
            s2 = sn.Swarm.cat(subs)
            saved_lock = model.locked
            model.mem = mems
            model.locked = torch.cat([saved_lock[[sn.KINDS.index(k) for k, _ in keys]]])
            for _ in range(switch_steps):
                s2 = model(s2, gen)
            for b, (k, e) in enumerate(keys):
                to = sn.PLAN_OF[e]
                row = se._score_row(s2, b, T, L)
                out["sw"][f"{k}->{to}"] = dict(loss=row[to], ok=se._passes(row, to, se._alive(s2, b)), n=se._alive(s2, b))
    return out


def fitness(res):
    """Own plans weigh double; each test: pass bonus + a smooth loss term (bar 8)."""
    f = 0.0
    for v in res["own"].values():
        f += 2 * (float(v["ok"]) + max(0.0, 1 - v["loss"] / 16))
    for v in res["sw"].values():
        f += float(v["ok"]) + max(0.0, 1 - v["loss"] / 16)
    return f


def fmt(res):
    o = " ".join(f"{k[:2]}{v['loss']:.1f}{'+' if v['ok'] else '-'}" for k, v in res["own"].items())
    s = " ".join(f"{k}:{v['loss']:.1f}{'+' if v['ok'] else '-'}" for k, v in res["sw"].items())
    return f"own [{o}]  sw [{s}]"


if __name__ == "__main__":
    torch.set_num_threads(1)
    kw = json.loads(sys.argv[1]) if len(sys.argv) > 1 else {}
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 7
    t = time.time()
    m = ef.EvoFate(ef.FateCfg(**kw))
    r = screen(m, seed=seed)
    print(json.dumps(kw), f"fit {fitness(r):.2f}", fmt(r), f"{time.time()-t:.0f}s", flush=True)
