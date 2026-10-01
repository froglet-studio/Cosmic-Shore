"""Extra probes for the `field` direction (designed fields + boids), on top of the shared yardstick.

  * switch_trace   - the rollout's own cull (swarm_nca.lose_majority, mode "excess"), but scored every
                     10 steps afterwards: how many steps until the new plan is closest (switch latency),
                     and the post-cull census. Also a STRICT variant whose cull really hands the majority
                     to the switch target (every other element culled below it), since the yardstick's
                     dragonfly cull leaves Space, not Mass, in the majority.
  * predator_compare - the predator pass with the school's reactions ON versus OFF (same seed), so the
                     parting is measured against a swarm that does not react at all.
  * ablations      - the strict tests for mode="field" (no slot assignment), molt=0, dwell=0.

    python Tools/NCA/field_probes.py [--out Tools/NCA/results/field/probes_extra.json]
"""
import argparse
import json
import os
import sys
import time
from dataclasses import replace

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import field_swarm as fs  # noqa: E402


@torch.no_grad()
def strict_cull(sw, b, to, gen, keep_extra=1):
    """Cull every element other than `to` down below `to`'s count (keep_extra fewer), as if a predator
    ate selectively. Returns the number removed."""
    m = sw.active[b] & sw.hatched[b]
    c = torch.bincount(sw.elem[b][m], minlength=4)
    cap = max(0, int(c[to]) - keep_extra)
    killed = 0
    for e in range(4):
        if e == to or int(c[e]) <= cap:
            continue
        idx = (m & (sw.elem[b] == e)).nonzero().squeeze(1)
        kill = idx[torch.randperm(len(idx), generator=gen)[:int(c[e]) - cap]]
        sw.active[b, kill] = False; sw.hatched[b, kill] = False; sw.s[b, kill] = 0.0
        killed += len(kill)
    return killed


def switch_trace(cfg, strict=False, steps=240, after=240, seed=7, L=None):
    L = L or sn.LossCfg()
    T = sn.load_targets()
    out = {}
    for k in sn.KINDS:
        model = fs.FieldSwarm(cfg)
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([T[k]], model.world, gen)
        for _ in range(steps):
            sw = model(sw, gen)
        to = sn.SWITCH_TO[k]
        if strict:
            killed = strict_cull(sw, 0, to, gen)
        else:
            n0 = int((sw.active[0] & sw.hatched[0]).sum())
            sn.lose_majority(sw, 0, gen, to=to)
            killed = n0 - int((sw.active[0] & sw.hatched[0]).sum())
        cen0 = sn.census(sw, 0, T[k])
        want = sn.PLAN_OF[to]
        trace, latency = [], None
        for t in range(after + 1):
            if t % 10 == 0:
                x = sn.decode(sw, 0)
                row = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
                best = min(row, key=row.get)
                trace.append(dict(t=t, best=best, n=int((sw.active[0] & sw.hatched[0]).sum()), want=row[want]))
                if latency is None and best == want:
                    latency = t
            if t < after:
                sw = model(sw, gen)
        x = sn.decode(sw, 0)
        row = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        n = int((sw.active[0] & sw.hatched[0]).sum())
        ok = n >= sn.MIN_TEST_BODY and row[want] < min(v for k2, v in row.items() if k2 != want) - 1e-6
        out[k] = dict(to=want, killed=killed, after_cull=cen0["elements"], majority_after_cull=cen0["majority"],
                      pass_=bool(ok), cross=row, n=n, elements=sn.census(sw, 0, T[want])["elements"],
                      latency_steps=latency, switches=model.mem[0]["switches"], trace=trace)
    return out


def predator_compare(cfg):
    off = replace(cfg, flee=(0.0, 0.0, 0.0, 0.0), relay=0.0, sense=0.01)
    res = {}
    for k in sn.KINDS:
        on = fs.predator_pass(fs.FieldSwarm(cfg), k)
        of = fs.predator_pass(fs.FieldSwarm(off), k)
        res[k] = dict(react_on={kk: v for kk, v in on.items() if kk != "trace"},
                      react_off={kk: v for kk, v in of.items() if kk != "trace"})
    return res


def ablation(cfg, **kw):
    c = replace(cfg, **kw)
    data, summary = sn.rollout(fs.FieldSwarm(c), 240)
    p, close = sn.tests_passed(summary)
    diag = {k: summary["cross"][k][k] for k in sn.KINDS}
    sw = {k: summary["switch"][k]["cross"][summary["switch"][k]["to"]] for k in sn.KINDS}
    return dict(set=kw, passed=p, close=round(close, 2), own=diag, switch_to=sw)


def main():
    torch.set_num_threads(4)
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(HERE, "results", "field", "probes_extra.json"))
    ap.add_argument("--set", action="append", default=[])
    a = ap.parse_args()
    cfg = fs.FieldCfg()
    for kv in a.set:
        k, v = kv.split("=", 1)
        cur = getattr(cfg, k)
        setattr(cfg, k, type(cur)(eval(v)) if isinstance(cur, tuple) else type(cur)(v))
    t0 = time.time()
    res = {}
    res["switch_yardstick_cull"] = switch_trace(cfg)
    res["switch_strict_cull"] = switch_trace(cfg, strict=True)
    for name in ("switch_yardstick_cull", "switch_strict_cull"):
        print(name)
        for k, v in res[name].items():
            print(f"  {k:7s}->{v['to']:7s} pass={v['pass_']} latency={v['latency_steps']} killed={v['killed']} "
                  f"after_cull={v['after_cull']} n={v['n']} el={v['elements']} want={v['cross'][v['to']]}")
    res["predator"] = predator_compare(cfg)
    for k, v in res["predator"].items():
        print(f"  predator {k:7s} touched on {v['react_on']['touched_share']} off {v['react_off']['touched_share']}  "
              f"worst on {v['react_on']['worst']} off {v['react_off']['worst']}")
    res["ablations"] = [ablation(cfg, mode="field"), ablation(cfg, molt=0), ablation(cfg, dwell=1),
                        ablation(cfg, mode="field", molt=0)]
    for r in res["ablations"]:
        print("  ablation", r["set"], r["passed"], "own", r["own"], "switch", r["switch_to"])
    res["ablations"] += [ablation(cfg, assign="greedy"), ablation(cfg, wander=0.25)]
    for r in res["ablations"][-2:]:
        print("  ablation", r["set"], r["passed"], "own", r["own"], "switch", r["switch_to"])
    res["tie_churn"] = [tie_churn(cfg, d, seed=s, every=1, bite=2, steps=120) for d in (1, 12) for s in (4, 5, 6, 7)]
    for r in res["tie_churn"]:
        print("  tie churn", r)
    res["loiter"] = {k: dict(mob=loiter(cfg, k), no_mob=loiter(replace(cfg, mob=(0.0, 0.0, 0.0, 0.0)), k)) for k in sn.KINDS}
    for k, v in res["loiter"].items():
        print("  loiter", k, v)
    res["cfg"] = {k: v for k, v in vars(cfg).items()}
    json.dump(res, open(a.out, "w"), indent=1)
    print(f"done in {time.time() - t0:.0f}s -> {a.out}")



def tie_churn(cfg, dwell, steps=200, every=3, seed=4, bite=1):
    """Grow a whale, cull Mass to one ahead of Space, then a predator eats one Mass OR one Space tadpole
    (coin flip) every `every` steps: the majority sits on a knife edge. Counts plan commits (flip-flops)."""
    T = sn.load_targets()
    model = fs.FieldSwarm(replace(cfg, dwell=dwell))
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T["mass"]], model.world, gen)
    for _ in range(240):
        sw = model(sw, gen)
    m = sw.active[0] & sw.hatched[0]
    c = torch.bincount(sw.elem[0][m], minlength=4)
    idx = (m & (sw.elem[0] == 1)).nonzero().squeeze(1)
    kill = idx[:int(c[1]) - int(c[2]) - 1]
    sw.active[0, kill] = False; sw.hatched[0, kill] = False; sw.s[0, kill] = 0
    rng = np.random.default_rng(seed)
    n0 = len(model.mem[0]["switches"])
    for t in range(steps):
        if t % every == 0:
            m = sw.active[0] & sw.hatched[0]
            e = int(rng.choice([1, 2]))
            cand = (m & (sw.elem[0] == e)).nonzero().squeeze(1)
            if len(cand) > bite + 2:
                j = cand[rng.permutation(len(cand))[:bite]]
                sw.active[0, j] = False; sw.hatched[0, j] = False; sw.s[0, j] = 0
        sw = model(sw, gen)
    sws = model.mem[0]["switches"][n0:]
    return dict(dwell=dwell, commits=len(sws), path=[f"{a}->{b}@{s}" for s, a, b in sws],
                final=model.mem[0]["plan"], n=int((sw.active[0] & sw.hatched[0]).sum()))


def loiter(cfg, kind="time", park=80, seed=5):
    """A ship parks 1.2 RMS radii off the grown swarm's centroid for `park` steps (barely moving), then
    leaves. Reports how many tadpoles crowd within 2 ship radii (the mob) and the re-formed score."""
    T = sn.load_targets()
    model = fs.FieldSwarm(cfg)
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[kind]], model.world, gen)
    for _ in range(240):
        sw = model(sw, gen)
    score = lambda: round(sn.swarm_loss(sn.decode(sw, 0), T[kind], sn.LossCfg())[1]["sink"], 2)
    before = score()
    al = (sw.active[0] & sw.hatched[0]).numpy(); p = sw.pos[0].numpy()[al]
    c = p.mean(0); rms = float(np.sqrt(((p - c) ** 2).sum(-1).mean()))
    ship = c + np.array([1.2 * rms, 0, 0]); rad = 0.3 * rms
    crowd = []
    for t in range(park):
        model.predators = [(ship + np.array([0, 0, 0.3 * np.sin(t / 10)]), rad, np.array([0.0, 0.0, 0.3]))]
        sw = model(sw, gen)
        al = (sw.active[0] & sw.hatched[0]).numpy()
        crowd.append(int((np.linalg.norm(sw.pos[0].numpy()[al] - ship, axis=-1) < 2 * rad).sum()))
    model.predators = []
    for _ in range(120):
        sw = model(sw, gen)
    return dict(kind=kind, before=before, mob_mean=round(float(np.mean(crowd[20:])), 1), mob_max=max(crowd), after=score())


if __name__ == "__main__":
    main()
