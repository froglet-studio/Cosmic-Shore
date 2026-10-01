"""Tiered, fail-fast evaluation over ALL transitions: 4 own-plan tests + 12 switches = 16.

The old yardstick (swarm_nca.rollout + tests_passed) checks 4 own plans and only 4 of the 12
possible switches (one per plan, picked arbitrarily). Every 7/8 run of round 1 failed the same one
of those four, so the other eight are likely hiding failures too. Checking all 16 every time is
expensive, so evaluation is tiered and stops early:

  tier 1  own plans    grow each seeding 240 steps; 4 tests              (always)
  tier 2  standard     the 4 switches rollout() has always run            (if tier 1 >= gate[0], default ALL 4)
  tier 3  the rest     the other 8 switches                               (if tiers 1+2 >= gate[1])

Every test runs `samples` independent rollouts (default 3) and passes on a majority; its pass RATE
is reported too, because a single rollout per test turned out to be noisy (G2 passed 1 of the 4
standard switches in rollout() and 0 of 4 here on another seed).

A switch removes (as if eaten) just enough of every element that does not trail the target element
that the target becomes the strict majority (`cull_to`; NOT swarm_nca.lose_majority, which only cuts
the old majority, so the runner-up rather than the target takes over), runs 240 more steps, and passes only if the
swarm is alive with >= 32 tadpoles, strictly closest to the new majority's plan, AND within
MAX_TEST_LOSS (8, default LossCfg) of it. Every test, own plans included, has that absolute bar: the
user's rule is that a body more than 8 from its plan fails however much closer it is than the others. A target element
the grown swarm holds fewer than 2 of cannot take over; that transition is reported as n/a (it cannot
happen to that body) rather than as a pass or a fail.

Stateless models (swarm_nca.SwarmRule) branch every switch off one grown swarm and run the branches
as one batch, so all 16 tests cost about 2x the old 8. Models that keep per-swarm state on themselves
(field, hgrid) regrow the identical seeding (same generator seed) for each switch instead.

    python Tools/NCA/swarm_eval.py --model rule:Tools/NCA/results/swarm_coevo_g2/rule.pt
    python Tools/NCA/swarm_eval.py --model field:Tools/NCA/results/field/params.json --full
"""
import argparse
import json
import os
import time

import torch

import swarm_nca as sn

def cull_to(sw, b, e, gen, margin=0.1, keep_min=6):
    """In place: remove (as if eaten) just enough of EVERY element that does not trail element e that
    e becomes the strict majority, leading each by `margin` x its own count (at least 1). Returns
    False (and changes nothing) if e holds fewer than 2 tadpoles or the swarm would drop below
    keep_min. swarm_nca.lose_majority only cuts the old majority, so the second-largest element, not
    e, takes over unless e already was the runner-up; that is why the four standard switches all
    target each plan's runner-up."""
    m = sw.active[b] & sw.hatched[b]
    c = torch.bincount(sw.elem[b][m], minlength=4)
    ce = int(c[e])
    if ce < 2:
        return False
    lead = max(1, int(round(margin * ce)))
    cap = max(0, ce - lead)                      # every other element is cut to at most this
    kills = [max(0, int(c[f]) - cap) if f != e else 0 for f in range(4)]
    if int(c.sum()) - sum(kills) < keep_min:
        return False
    for f in range(4):
        k = kills[f]
        if k <= 0:
            continue
        idx = (m & (sw.elem[b] == f)).nonzero().squeeze(1)
        kill = idx[torch.randperm(len(idx), generator=gen, device=idx.device)[:k]]
        sw.active[b, kill] = False; sw.hatched[b, kill] = False; sw.s[b, kill] = 0.0
    return True


def _plan_of_elem(e):
    """Element index -> the plan that uses most of it (Charge -> pufferfish, Mass -> whale, ...)."""
    return sn.PLAN_OF[e]


def transitions():
    """All 12 (plan, target element) pairs, the four standard ones first."""
    std = [(k, sn.SWITCH_TO[k]) for k in sn.KINDS]
    rest = [(k, e) for k in sn.KINDS for e in range(4) if _plan_of_elem(e) != k and (k, e) not in std]
    return std, rest


def _score_row(sw, b, targets, L):
    x = sn.decode(sw, b)
    return {k2: round(sn.swarm_loss(x, targets[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}


def _passes(row, want, n, max_loss=None):
    """Alive with >= MIN_TEST_BODY tadpoles, strictly closest to the wanted plan, AND within max_loss
    (default swarm_nca.MAX_TEST_LOSS = 8) of it: being the closest of four plans is not a creature."""
    others = [v for k, v in row.items() if k != want]
    ml = sn.MAX_TEST_LOSS if max_loss is None else max_loss
    return n >= sn.MIN_TEST_BODY and row[want] <= ml and row[want] < min(others) - 1e-6


def _alive(sw, b):
    return int((sw.active[b] & sw.hatched[b]).sum())


def _grow(model, kinds, targets, steps, seed):
    """Grow one swarm per entry of `kinds` (a batch when the model is stateless)."""
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([targets[k] for k in kinds], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    return sw, gen


@torch.no_grad()
def evaluate(model, steps=240, switch_steps=240, seed=7, samples=3, L=None, gate=(4, 6), full=False, log=print, max_loss=None):
    """Every test is run `samples` times (independent seeds); a test PASSES when more than half of its
    samples pass, and its pass RATE is reported, so one lucky rollout cannot carry a test."""
    L = L or sn.LossCfg()
    targets = sn.load_targets()
    stateless = getattr(model, "stateless", type(model) is sn.SwarmRule)   # a subclass may keep state on itself
    t0 = time.time()
    S = samples
    out = dict(own={}, switch={}, tiers_run=[], na=[], samples=S, max_loss=sn.MAX_TEST_LOSS if max_loss is None else max_loss)
    # tier 1: own plans. grown[k][s] = (swarm batch, index in it, generator)
    grown = {k: [] for k in sn.KINDS}
    if stateless:
        kinds = [k for k in sn.KINDS for _ in range(S)]
        sw, gen = _grow(model, kinds, targets, steps, seed)
        for i, k in enumerate(kinds):
            grown[k].append((sw, i, gen))
    else:
        for k in sn.KINDS:
            for r in range(S):
                sw, gen = _grow(model, [k], targets, steps, seed + 101 * r)
                grown[k].append((sw, 0, gen))
    for k in sn.KINDS:
        oks, rows, ns = [], [], []
        for sw, b, _ in grown[k]:
            row = _score_row(sw, b, targets, L); n = _alive(sw, b)
            oks.append(_passes(row, k, n, max_loss)); rows.append(row); ns.append(n)
        out["own"][k] = dict(cross={k2: round(sum(r[k2] for r in rows) / S, 2) for k2 in sn.KINDS}, n=round(sum(ns) / S),
                             rate=round(sum(oks) / S, 2), ok=sum(oks) * 2 > S)
    own = sum(v["ok"] for v in out["own"].values())
    out["tiers_run"].append("own")
    log(f"tier 1 own plans: {own}/4  rates {[out['own'][k]['rate'] for k in sn.KINDS]}  ({time.time() - t0:.0f}s)")
    std, rest = transitions()

    def record(k, to, results):
        oks = [r[0] for r in results]
        out["switch"][f"{k}->{to}"] = dict(
            cross={k2: round(sum(r[1][k2] for r in results) / len(results), 2) for k2 in sn.KINDS},
            n=round(sum(r[2] for r in results) / len(results)), rate=round(sum(oks) / len(results), 2),
            ok=sum(oks) * 2 > len(results), majority=[r[3] for r in results])
        return out["switch"][f"{k}->{to}"]["ok"]

    def run(pairs):
        done = 0
        res = {}
        if stateless:
            subs, keys = [], []
            for k, e in pairs:
                for sw0, b0, gen in grown[k]:
                    sub = sn.Swarm.cat([sw0.index(torch.tensor([b0]))])
                    if not cull_to(sub, 0, e, gen):
                        continue
                    subs.append(sub); keys.append((k, e))
            if subs:
                sw = sn.Swarm.cat(subs)
                gen = sn.make_gen(seed + 1)
                for _ in range(switch_steps):
                    sw = model(sw, gen)
                for b, (k, e) in enumerate(keys):
                    to = _plan_of_elem(e); row = _score_row(sw, b, targets, L); n = _alive(sw, b)
                    res.setdefault((k, e), []).append((_passes(row, to, n, max_loss), row, n, sn.majority_plan(sw, b, to)))
        else:
            for k, e in pairs:
                to = _plan_of_elem(e)
                for r in range(S):
                    sw, gen = _grow(model, [k], targets, steps, seed + 101 * r)   # identical to tier 1's swarm
                    if not cull_to(sw, 0, e, gen):
                        continue
                    for _ in range(switch_steps):
                        sw = model(sw, gen)
                    row = _score_row(sw, 0, targets, L); n = _alive(sw, 0)
                    res.setdefault((k, e), []).append((_passes(row, to, n, max_loss), row, n, sn.majority_plan(sw, 0, to)))
        for k, e in pairs:
            to = _plan_of_elem(e)
            if (k, e) not in res:
                out["na"].append(f"{k}->{to}")
            else:
                done += record(k, to, res[(k, e)])
        return done

    s_std = s_rest = None
    if full or own >= gate[0]:
        s_std = run(std); out["tiers_run"].append("standard")
        log(f"tier 2 standard switches: {s_std}/4  ({time.time() - t0:.0f}s)")
        if full or own + s_std >= gate[1]:
            s_rest = run(rest); out["tiers_run"].append("rest")
            log(f"tier 3 other switches: {s_rest}/{len(rest)}  ({time.time() - t0:.0f}s)")
    passed = own + (s_std or 0) + (s_rest or 0)
    feasible = 16 - len(out["na"])
    out.update(passed=passed, feasible=feasible, own_passed=own, std_passed=s_std, rest_passed=s_rest,
               complete=len(out["tiers_run"]) == 3, seconds=round(time.time() - t0, 1))
    return out


def matrix(res):
    """The 4x4 table: rows = grown plan, columns = the plan it should end as (diagonal = own plan)."""
    lines = ["from \\ to   " + " ".join(f"{k[:5]:>7s}" for k in sn.KINDS)]
    for k in sn.KINDS:
        cells = []
        for to in sn.KINDS:
            if to == k:
                c = res["own"][k]["ok"]; r = res["own"][k]["rate"]
            else:
                v = res["switch"].get(f"{k}->{to}")
                c = None if v is None else v["ok"]; r = None if v is None else v["rate"]
                if v is None and f"{k}->{to}" in res["na"]:
                    cells.append(f"{'n/a':>7s}"); continue
            cells.append(f"{(f'{r:.2f}' + ('+' if c else '-')) if c is not None else '-':>7s}")
        lines.append(f"{k:10s}  " + " ".join(cells))
    return "\n".join(lines)


def load_model(spec):
    kind, path = spec.split(":", 1)
    if kind == "rule":
        return sn.load_rule(path)
    if kind == "evo":
        import numpy as np, evo_model as em
        return em.EvoRule(np.load(path))
    if kind == "hgrid2":
        import hgrid2_model as hm
        return hm.Boid2(sn.World(), hm.Cfg(**json.load(open(path))["cfg"]))
    if kind == "sort":
        import sort_model as sm
        return sm.load(path)
    if kind == "sortfeel":
        import sortfeel_model as fm
        return fm.load(path)
    if kind == "posinfo":
        import posinfo_rule as pr
        return pr.load(path)
    if kind == "posinfo2":
        import posinfo2_rule as p2
        return p2.load(path)
    if kind == "ensemble":
        import ensemble_swarm as es
        return es.load(path)
    if kind == "evo_compact":
        import numpy as np, evo_compact as ec
        return ec.CompactRule(np.load(path))
    if kind == "field":
        import field_swarm as fs
        cfg = json.load(open(path))["cfg"]
        cfg["vmax"] = tuple(cfg["vmax"])
        return fs.FieldSwarm(fs.FieldCfg(**cfg))
    if kind in ("hgrid_oracle", "hgrid_hybrid"):
        import hgrid_boid as hb
        meta = json.load(open(path))["meta"]
        cfg = hb.BoidCfg(**meta["cfg"])
        if kind == "hgrid_oracle":
            return hb.make_oracle(cfg)
        import hgrid_hybrid as hh
        return hh.HybridRule(sn.load_rule(os.path.join(sn.HERE, meta["rule"]) if not os.path.isabs(meta["rule"]) else meta["rule"]),
                             cfg, meta.get("shed", 1))
    if kind == "combo":
        import combo_model as cm
        return cm.load(path)
    raise ValueError(spec)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True, help="rule:<rule.pt> | evo:<genome.npy> | evo_compact:<genome.npy> | field:<params.json> | hgrid_oracle:<summary.json> | hgrid_hybrid:<summary.json>")
    ap.add_argument("--full", action="store_true", help="run every tier regardless of the gates")
    ap.add_argument("--samples", type=int, default=3, help="independent rollouts per test (majority decides)")
    ap.add_argument("--scale-inv", type=int, default=0)
    ap.add_argument("--seed", type=int, default=7, help="base rollout seed (held-out checks: anything but 7)")
    ap.add_argument("--max-loss", type=float, default=None, help="absolute pass bar (default swarm_nca.MAX_TEST_LOSS = 8)")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    model = load_model(a.model)
    res = evaluate(model, L=sn.LossCfg(scale_inv=a.scale_inv), full=a.full, samples=a.samples, max_loss=a.max_loss, seed=a.seed)
    print(matrix(res))
    print(f"PASSED {res['passed']}/{res['feasible']} feasible (16 total; n/a: {res['na']}); tiers run: {res['tiers_run']}; {res['seconds']}s")
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
