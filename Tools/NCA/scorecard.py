"""One scorecard for every swarm model: is it PERFORMANT, LOSSLESS and EMERGENT, and does it pass?

The lead's goal (Tools/NCA/DISCOVERIES.md): "we will find a combination of strategies that are performant,
lossless, and emergent". This file turns those three words into numbers any model can be scored on, beside the
16-transition yardstick (swarm_eval, loss-8 bar). It runs no training and edits nothing shared.

  ACCURATE    swarm_eval.evaluate (16 transitions, fair cull, 3 samples, absolute bar 8), at the seeds given.
  LOSSLESS    self-inflicted deaths. The game forbids an imposed death (CLAUDE.md: no decay, no lifespan, no
              timer cull of a fed animal); the only deaths the yardstick may cause are its CULL (the stand-in
              for players killing members). Counted per slot: a tadpole active+hatched at step t and inactive at
              t+1, plus any rise of the model's own `deaths` counter, over a grow (240 steps) and every
              standard switch (cull, then 240 steps). Reported per 1,000 tadpole-steps; LOSSLESS = 0.
              Molting (a member changing element) is NOT a death and is allowed (a relaxed constraint).
  PERFORMANT  ms per step for ONE grown swarm (batch 1, 1 thread, CPU), mean over the four plans, plus the
              headcount it was measured at. This is the research prototype's cost, not the game's: the C#
              ports run 5-15x faster (field 0.1-0.9 ms, grid 0.6-1.2 ms per step; Docs/SWARM_FAUNA.md on
              cece/swarm-fauna-game). Compare models against each other, not against a frame budget.
  EMERGENT    swarm_feel: the organic band calibrated on the lead's review (evo + hgrid oracle in, evo compact +
              field out), plus LOCALITY, a declared property: what a tadpole reads ("local" = only fields /
              neighbours at its own position + population census; "global" = an assignment or a frame computed
              from the whole body). Pass `--locality` to record it; the scorecard cannot infer it.

    python Tools/NCA/scorecard.py --model hgrid2:Tools/NCA/results/hgrid2/params.json --seeds 7,23 \
        --locality local --out results/hgrid2/scorecard.json
"""
from __future__ import annotations

import argparse
import json
import time

import torch

import swarm_eval as se
import swarm_feel as sf
import swarm_nca as sn


def _live(sw):
    return sw.active & sw.hatched


@torch.no_grad()
def _run_counting(model, sw, gen, steps, acc):
    """Step the model, counting self-inflicted deaths and wall time per step."""
    for _ in range(steps):
        before, d0 = _live(sw).clone(), sw.deaths.clone()
        t = time.perf_counter()
        sw = model(sw, gen)
        acc["sec"] += time.perf_counter() - t
        acc["steps"] += 1
        acc["tadpole_steps"] += int(before.sum())
        gone = int((before & ~sw.active).sum())          # a slot that was alive and is no longer active
        acc["deaths"] += max(gone, int((sw.deaths - d0).clamp(min=0).sum()))
    return sw


@torch.no_grad()
def lossless_and_cost(model, seed=7, steps=240):
    targets = sn.load_targets()
    std, _ = se.transitions()
    per = {}
    for k in sn.KINDS:
        acc = dict(sec=0.0, steps=0, tadpole_steps=0, deaths=0)
        gen = sn.make_gen(seed)
        sw = sn.seed_swarm([targets[k]], model.world, gen)
        sw = _run_counting(model, sw, gen, steps, acc)
        grown_n = int(_live(sw)[0].sum())
        grow_ms = 1000 * acc["sec"] / max(1, acc["steps"])
        # cost at the grown size: 32 more steps, timed alone
        t_acc = dict(sec=0.0, steps=0, tadpole_steps=0, deaths=0)
        sw2 = _run_counting(model, sw.clone(), gen, 32, t_acc)
        e = [e for kk, e in std if kk == k][0]
        sub = sw2.clone()
        if se.cull_to(sub, 0, e, gen):
            sub = _run_counting(model, sub, gen, steps, acc)
        per[k] = dict(grown_n=grown_n, ms_per_step_grown=round(1000 * t_acc["sec"] / max(1, t_acc["steps"]), 3),
                      ms_per_step_grow=round(grow_ms, 3), deaths=acc["deaths"] + t_acc["deaths"],
                      tadpole_steps=acc["tadpole_steps"] + t_acc["tadpole_steps"])
    deaths = sum(v["deaths"] for v in per.values())
    ts = sum(v["tadpole_steps"] for v in per.values())
    return dict(per_plan=per, deaths=deaths, deaths_per_1k_tadpole_steps=round(1000 * deaths / max(1, ts), 3),
                lossless=deaths == 0,
                ms_per_step=round(sum(v["ms_per_step_grown"] for v in per.values()) / 4, 3),
                grown_n=round(sum(v["grown_n"] for v in per.values()) / 4))


def scorecard(model, seeds=(7,), locality="undeclared", samples=3, log=print):
    out = dict(locality=locality, accurate={}, seeds=list(seeds))
    for s in seeds:
        res = se.evaluate(model, seed=s, samples=samples, full=True, log=log)
        out["accurate"][str(s)] = dict(passed=res["passed"], feasible=res["feasible"], na=res["na"],
                                       own={k: res["own"][k]["cross"][k] for k in sn.KINDS},
                                       own_passed=res["own_passed"], seconds=res["seconds"])
    out["lossless"] = lossless_and_cost(model, seed=seeds[0])
    feel = sf.feel(model, seed=seeds[0])
    ok, checks = sf.in_band(feel)
    out["emergent"] = dict(in_band=ok, checks=checks, mean=feel["mean"], planar_excess=round(sf.planar_excess(feel), 3))
    a = [v["passed"] / max(1, v["feasible"]) for v in out["accurate"].values()]
    out["headline"] = (f"accurate {min(a):.2f} worst seed | lossless {out['lossless']['lossless']} "
                       f"({out['lossless']['deaths_per_1k_tadpole_steps']}/1k) | {out['lossless']['ms_per_step']} ms/step "
                       f"@{out['lossless']['grown_n']} | organic {ok} | {locality}")
    return out


def main():
    torch.set_num_threads(1)
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True, help="any swarm_eval.load_model spec")
    ap.add_argument("--seeds", default="7")
    ap.add_argument("--samples", type=int, default=3)
    ap.add_argument("--locality", default="undeclared", choices=["local", "global", "mixed", "undeclared"])
    ap.add_argument("--skip-eval", action="store_true", help="lossless/cost/feel only (fast)")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    model = se.load_model(a.model)
    if a.skip_eval:
        out = dict(locality=a.locality, lossless=lossless_and_cost(model, seed=int(a.seeds.split(",")[0])))
        f = sf.feel(model, seed=int(a.seeds.split(",")[0])); ok, checks = sf.in_band(f)
        out["emergent"] = dict(in_band=ok, checks=checks, mean=f["mean"], planar_excess=round(sf.planar_excess(f), 3))
    else:
        out = scorecard(model, [int(s) for s in a.seeds.split(",")], a.locality, a.samples)
    print(json.dumps(out, indent=1))
    if a.out:
        json.dump(out, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
