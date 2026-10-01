"""SMOOTHNESS OF CHANGE for any swarm model: how the body gets from one form to the next.

swarm_feel scores a body that has already grown (steady swimming). The lead also wants the CHANGES
held to a standard: a swarm that loses its majority and becomes another creature, or heals after a
vessel strike, should flow there, not snap, stall, thrash or pop. This file records every step of
those changes and turns them into numbers. Measured events (one rollout each, seed fixed):

  switch  the 4 standard switches (swarm_eval.transitions()[0]): grow 240 steps, swarm_eval.cull_to the
          runner-up element, run 240 steps toward the new plan
  heal    swarm_probe.strike on each of the 4 plans (a third of the body removed), then 240 steps

Per event, on tadpole identities (array slot = identity):

  mono        loss-path monotonicity: total variation of the loss-to-goal series (sampled every 8 steps)
              over the net drop. 1 = it only ever went downhill; 2 = it climbed back as far as it fell.
              (Goal = the new plan for a switch, the same plan for a heal.)
  settle      share of the window before the loss is within 10% (of the net drop) of where it ends.
  lurch       the worst step's 95th-percentile tadpole speed over the body's own steady-state mean speed
              (measured on the grown body before the event). A big number is a lurch or a teleport.
  jerk_rel    mean |third difference| / mean speed DURING the change (swarm_feel's definition).
  molt_burst  the largest share of the body that changed ELEMENT in any 8-step window. A molt recolours a
              crystal; spread out it reads as a wave, all at once it reads as a pop.
  birth_burst the largest share of the body that hatched in any 8-step window (a body that re-forms by a
              flash of births pops instead of growing).
  deaths      self-inflicted deaths after the event (lossless = 0; the cull/strike itself does not count).

`rough` per event = (mono - 1) + max(0, lurch / 4 - 1) + max(0, jerk_rel - 2.5)
                    + 10 * max(0, molt_burst - 0.10) + 10 * max(0, birth_burst - 0.10),
each term 0 inside its comfort range, and `smoothness` = 1 / (1 + mean rough) in (0, 1]. The terms are
reported separately so a regression can be traced; tools/NCA/hold.py gates on them against a held baseline.

    python Tools/NCA/swarm_smooth.py --model combo:Tools/NCA/results/combo/params.json --out results/combo/smooth.json
"""
from __future__ import annotations

import argparse
import json
import time

import torch

import swarm_eval as se
import swarm_nca as sn
import swarm_probe as sp

LURCH_OK, JERK_OK, BURST_OK = 4.0, 2.5, 0.10


def _alive(sw):
    return (sw.active[0] & sw.hatched[0]).clone()


@torch.no_grad()
def _track(model, sw, gen, goal, targets, L, steps, every, ref_speed):
    pos, alive, elem, hat, losses = [sw.pos[0].clone()], [_alive(sw)], [sw.elem[0].clone()], [sw.hatched[0].clone()], []
    loss = lambda s: float(sn.swarm_loss(sn.decode(s, 0), targets[goal], L)[1]["sink"])
    losses.append(loss(sw))
    act0 = sw.active[0].clone()
    deaths = 0
    for t in range(steps):
        before = sw.active[0] & sw.hatched[0]
        d0 = sw.deaths.clone()
        sw = model(sw, gen)
        deaths += max(int((before & ~sw.active[0]).sum()), int((sw.deaths - d0).clamp(min=0).sum()))
        pos.append(sw.pos[0].clone()); alive.append(_alive(sw)); elem.append(sw.elem[0].clone()); hat.append(sw.hatched[0].clone())
        if (t + 1) % every == 0:
            losses.append(loss(sw))
    P, A, E, H = torch.stack(pos), torch.stack(alive), torch.stack(elem), torch.stack(hat)
    n_t = A.sum(1).float().clamp(min=1)
    # speeds of tadpoles alive at both ends of a step
    both = A[1:] & A[:-1]
    V = (P[1:] - P[:-1]).norm(dim=-1)
    p95 = torch.stack([torch.quantile(V[t][both[t]], 0.95) if int(both[t].sum()) >= 4 else torch.tensor(0.0) for t in range(len(V))])
    lurch = float(p95.max() / max(ref_speed, 1e-4))
    # jerk over tadpoles alive for 4 consecutive steps
    a4 = A[3:] & A[2:-1] & A[1:-2] & A[:-3]
    J = (P[3:] - 3 * P[2:-1] + 3 * P[1:-2] - P[:-3]).norm(dim=-1)
    jm = float(J[a4].mean()) if int(a4.sum()) else 0.0
    vm = float(V[both].mean()) if int(both.sum()) else 1e-6
    jerk_rel = jm / max(vm, 1e-6)
    # molts (element change on a living identity) and hatches, in 8-step windows, as a share of the body
    molt = ((E[1:] != E[:-1]) & both).sum(1).float() / n_t[1:]
    born = (H[1:] & ~H[:-1] & A[1:]).sum(1).float() / n_t[1:]
    win = lambda x: float(torch.stack([x[i:i + 8].sum() for i in range(0, max(1, len(x) - 7))]).max())
    Ls = torch.tensor(losses)
    drop = float(Ls[0] - Ls[-1])
    tv = float((Ls[1:] - Ls[:-1]).abs().sum())
    mono = tv / max(drop, 0.5) if drop > 0 else tv / 0.5 + 1.0
    thr = float(Ls[-1]) + 0.1 * max(drop, 0.0)
    settle = next((i for i, v in enumerate(losses) if v <= thr), len(losses) - 1) / max(1, len(losses) - 1)
    r = dict(loss_start=round(float(Ls[0]), 2), loss_end=round(float(Ls[-1]), 2), mono=round(mono, 3), settle=round(settle, 3),
             lurch=round(lurch, 2), jerk_rel=round(jerk_rel, 3), molt_burst=round(win(molt), 3), birth_burst=round(win(born), 3),
             deaths=deaths, n_end=int(A[-1].sum()))
    r["rough"] = round((r["mono"] - 1) + max(0.0, r["lurch"] / LURCH_OK - 1) + max(0.0, r["jerk_rel"] - JERK_OK)
                       + 10 * max(0.0, r["molt_burst"] - BURST_OK) + 10 * max(0.0, r["birth_burst"] - BURST_OK), 3)
    return r


@torch.no_grad()
def _grown(model, k, targets, steps, seed):
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([targets[k]], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    # the body's own steady-state speed, over 16 more steps
    a, p0, sp_ = _alive(sw), sw.pos[0].clone(), []
    for _ in range(16):
        sw = model(sw, gen)
        b = a & _alive(sw)
        sp_.append(float((sw.pos[0][b] - p0[b]).norm(dim=-1).mean()) if int(b.sum()) else 0.0)
        a, p0 = _alive(sw), sw.pos[0].clone()
    return sw, gen, sum(sp_) / len(sp_)


@torch.no_grad()
def smooth(model, seed=7, steps=240, window=240, every=8, log=print):
    targets = sn.load_targets()
    L = sn.LossCfg()
    std, _ = se.transitions()
    out, t0 = dict(events={}), time.time()
    for k, e in std:
        sw, gen, ref = _grown(model, k, targets, steps, seed)
        to = sn.PLAN_OF[e]
        if not se.cull_to(sw, 0, e, gen):
            out["events"][f"switch {k}->{to}"] = None
            continue
        out["events"][f"switch {k}->{to}"] = _track(model, sw, gen, to, targets, L, window, every, ref)
        log(f"switch {k}->{to}: {out['events'][f'switch {k}->{to}']}  ({time.time() - t0:.0f}s)")
    for k in sn.KINDS:
        sw, gen, ref = _grown(model, k, targets, steps, seed + 1)
        sp.strike(sw, gen=gen)
        out["events"][f"heal {k}"] = _track(model, sw, gen, k, targets, L, window, every, ref)
        log(f"heal {k}: {out['events'][f'heal {k}']}  ({time.time() - t0:.0f}s)")
    ev = [v for v in out["events"].values() if v]
    keys = ["mono", "settle", "lurch", "jerk_rel", "molt_burst", "birth_burst", "rough"]
    out["mean"] = {kk: round(sum(v[kk] for v in ev) / len(ev), 3) for kk in keys}
    out["worst"] = {kk: round(max(v[kk] for v in ev), 3) for kk in keys if kk != "settle"}
    out["deaths"] = sum(v["deaths"] for v in ev)
    out["smoothness"] = round(1.0 / (1.0 + out["mean"]["rough"]), 3)
    out["seconds"] = round(time.time() - t0, 1)
    return out


def main():
    torch.set_num_threads(4)
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    res = smooth(se.load_model(a.model), seed=a.seed)
    print(json.dumps({k: v for k, v in res.items() if k != "events"}, indent=1))
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
