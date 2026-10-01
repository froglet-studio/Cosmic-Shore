"""evo16: a cheap, batched fitness that mirrors swarm_eval (16 transitions) + swarm_probe (heal) at once.

One seed = grow the four plans as one batch (240 steps), score the 4 own-plan tests, then branch the
grown batch into ONE batch of 16 rows - the 12 switches (swarm_eval.cull_to, fresh behaviour state, as
swarm_eval's batched branches see it) and 4 vessel strikes (swarm_probe geometry; behaviour state kept,
so a struck swarm remembers how big it was) - and run it 240 steps. Strikes are scored at +120 (the
probe's regrow), switches at +240. One sample per test (swarm_eval runs 3; the elite gets the real thing).

fitness = mean pass over the feasible tests + 0.5 * mean margin + W_HEAL * mean(min(heal, 0.5) / 0.5)
  margin per test in [-1, 1] = (best other - wanted) / (best other + wanted), -1 below MIN_TEST_BODY.
"""
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo_model as em  # noqa: E402
import evo16_model as e16  # noqa: E402
import swarm_eval as se  # noqa: E402
import swarm_nca as sn  # noqa: E402

W_HEAL = float(os.environ.get("EVO16_W_HEAL", "1.0"))


def _margin(row, want, n):
    if n < sn.MIN_TEST_BODY or row[want] >= 99.9:
        return -1.0
    o = min(v for k, v in row.items() if k != want)
    return max(-1.0, min(1.0, (o - row[want]) / (o + row[want])))


@torch.no_grad()
def run_seed(model, seed, steps=240, switch_steps=240, regrow=120, L=None):
    L = L or sn.LossCfg()
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[k] for k in sn.KINDS], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    res = dict(own={}, switch={}, heal={}, na=[], n={})
    for b, k in enumerate(sn.KINDS):
        row = se._score_row(sw, b, T, L); n = se._alive(sw, b)
        res["own"][k] = (se._passes(row, k, n), _margin(row, k, n))
        res["n"][k] = n
    std, rest = se.transitions()
    subs, keys, hw, ema, lock = [], [], [], [], []
    st = getattr(model, "hw", None) is not None
    for k, e in std + rest:
        b = sn.KINDS.index(k)
        sub = sw.index(torch.tensor([b])).clone()
        if not se.cull_to(sub, 0, e, gen):
            res["na"].append(f"{k}->{sn.PLAN_OF[e]}"); continue
        subs.append(sub); keys.append(("sw", k, e))
        if st:
            hw.append(float((sub.active & sub.hatched).sum())); ema.append(torch.full((1, sw.pos.shape[1]), -1.0)); lock.append(-1)
    before = {}
    for b, k in enumerate(sn.KINDS):
        sub = sw.index(torch.tensor([b])).clone()
        before[k] = sn.swarm_loss(sn.decode(sub, 0), T[k], L)[1]["sink"]
        em._strike_b(sub, 0, gen)
        subs.append(sub); keys.append(("strike", k, None))
        if st:
            hw.append(float(model.hw[b])); ema.append(model.ema[b:b + 1].clone() if model.ema is not None else torch.full((1, sw.pos.shape[1]), -1.0))
            lock.append(int(model.locked[b]))
    bs = sn.Swarm.cat(subs)
    cut = {k: sn.swarm_loss(sn.decode(bs, i), T[k], L)[1]["sink"] for i, (t, k, _) in enumerate(keys) if t == "strike"}
    if st:
        model.hw = torch.tensor(hw); model.ema = torch.cat(ema); model.locked = torch.tensor(lock, dtype=torch.long)
        model._tok = bs.pos
    g2 = sn.make_gen(seed + 1)
    for t in range(switch_steps):
        bs = model(bs, g2)
        if t + 1 == regrow:
            for i, (typ, k, _) in enumerate(keys):
                if typ == "strike":
                    r = sn.swarm_loss(sn.decode(bs, i), T[k], L)[1]["sink"]
                    span = cut[k] - before[k]
                    res["heal"][k] = (max(-1.0, min(1.0, (cut[k] - r) / span)) if span > 0.3 else (1.0 if r <= cut[k] + 0.3 else -1.0))
    for i, (typ, k, e) in enumerate(keys):
        if typ == "sw":
            to = sn.PLAN_OF[e]
            row = se._score_row(bs, i, T, L); n = se._alive(bs, i)
            res["switch"][f"{k}->{to}"] = (se._passes(row, to, n), _margin(row, to, n))
    return res


def fitness_of(res):
    tests = list(res["own"].values()) + list(res["switch"].values())
    p = float(np.mean([t[0] for t in tests])); m = float(np.mean([t[1] for t in tests]))
    h = list(res["heal"].values())
    hs = float(np.mean([min(x, 0.5) / 0.5 for x in h])) if h else 0.0
    return p + 0.5 * m + W_HEAL * hs, p, m, h


def evaluate(genome, seeds=(1,)):
    torch.set_num_threads(1)
    model = e16.Evo16Rule(genome)
    out = []
    for s in seeds:
        res = run_seed(model, s)
        f, p, m, h = fitness_of(res)
        out.append(dict(f=f, p=p, m=m, heal=h, passed=int(round(p * (len(res["own"]) + len(res["switch"])))),
                        feasible=len(res["own"]) + len(res["switch"]), fails=[k for k, v in {**res["own"], **res["switch"]}.items() if not v[0]]))
    return out


if __name__ == "__main__":
    import json, time, argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("genome")
    ap.add_argument("--seeds", default="1")
    a = ap.parse_args()
    g = np.load(a.genome)
    t0 = time.time()
    for r in evaluate(g, [int(s) for s in a.seeds.split(",")]):
        print(json.dumps(r))
    print(f"{time.time() - t0:.0f}s")
