"""SMOOTHNESS OF CHANGE for any swarm model: how the body gets from one form to the next.

swarm_feel scores a body that has already grown (steady swimming). The lead also wants the CHANGES
held to a standard: a swarm that loses its majority and becomes another creature, or heals after a
vessel strike, should flow there, not snap, stall, thrash or pop. This file records every step of
those changes and turns them into numbers. Measured events (one rollout each, seed fixed):

  switch  the 4 standard switches (swarm_eval.transitions()[0]): grow 240 steps, swarm_eval.cull_to the
          runner-up element, run 240 steps toward the new plan
  heal    swarm_probe.strike on each of the 4 plans (a third of the body removed), then 240 steps

Per event, on tadpole identities (array slot = identity):

  mono        loss-path total variation over the net drop (the loss-to-goal series, sampled every 16 steps
              against frames 0 and 4 of the plan's animation - a cheap, consistent proxy; goal = the new plan
              for a switch, the same plan for a heal). Reported only: when the drop is small (a heal) the
              body's ordinary swimming wiggle makes it explode (first calibration: up to 137).
  backtrack   the uphill part of that path that is NOT the body's ordinary wiggle: the sum of its climbs,
              minus the climbs the SAME body makes once settled (8 more samples past the window, same
              sampling, scaled to this path's loss level), over the starting loss. 0 = it never went back. Penalised.
  settle      share of the window before the loss is within 10% (of the net drop) of where it ends.
  travel      the event's median per-step 95th-percentile tadpole speed over the SAME percentile on the
              body's own steady state (the grown body before the event). A switch IS a migration, so this is
              expected to differ from 1 (calibration: 0.2 - 5.5); reported, not penalised.
  lurch       a SPIKE: the worst step's 95th-percentile speed over the event's own median of it. 1 = the
              migration flows at an even pace; a big number is a sudden jolt inside the change.
  teleport    the largest single-step displacement of any tadpole over the world's top speed (2.0 voxels,
              Time's vmax). Up to ~1.5 is a fast swimmer plus a collision push; well above it means something was MOVED rather than swam (a transfer that relocates
              a tadpole, a respawn) - the continuity law's worst failure.
  jerk_rel    mean |third difference| / mean speed DURING the change (swarm_feel's definition).
  molt_burst  the largest share of the body that changed ELEMENT in any 8-step window. A molt recolours a
              crystal; spread out it reads as a wave, all at once it reads as a pop.
  birth_burst the largest share of the body that hatched in any 8-step window (a body that re-forms by a
              flash of births pops instead of growing). Both shares are of max(the body now, the body at
              the end), so a body regrowing after a cull is not charged for having been small.
  deaths      self-inflicted deaths after the event (lossless = 0; the cull/strike itself does not count).

`rough` per event = 4 * backtrack + max(0, lurch / 2.5 - 1) + 2 * max(0, teleport / 1.5 - 1) + max(0, jerk_rel - 2.5)
                    + 10 * max(0, molt_burst - 0.10) + 10 * max(0, birth_burst - 0.25),
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

LURCH_OK, JERK_OK, BURST_OK, BIRTH_OK, TELEPORT_OK = 2.5, 2.5, 0.10, 0.25, 1.5
LOSS_FRAMES = (0, 4)


def _alive(sw):
    return (sw.active[0] & sw.hatched[0]).clone()


@torch.no_grad()
def _track(model, sw, gen, goal, targets, L, steps, every, ref_speed, tail=8):
    pos, alive, elem, hat, losses = [sw.pos[0].clone()], [_alive(sw)], [sw.elem[0].clone()], [sw.hatched[0].clone()], []
    loss = lambda s: float(sn.swarm_loss(sn.decode(s, 0), targets[goal], L, frames=LOSS_FRAMES)[1]["sink"])
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
    # the settled body's own wiggle: keep the SAME swarm running `tail` more samples past the window and
    # measure its climbs per unit loss (same sampling) - what an ordinary swimming body does on its goal plan
    lt = [losses[-1]]
    for i in range(tail * every):
        sw = model(sw, gen)
        if (i + 1) % every == 0:
            lt.append(loss(sw))
    lt = torch.tensor(lt)
    noise_rel = float((lt[1:] - lt[:-1]).clamp(min=0).sum() / lt[1:].sum().clamp(min=1e-6))
    P, A, E, H = torch.stack(pos), torch.stack(alive), torch.stack(elem), torch.stack(hat)
    n_t = torch.maximum(A.sum(1).float(), A[-1].sum().float()).clamp(min=1)
    # speeds of tadpoles alive at both ends of a step
    both = A[1:] & A[:-1]
    V = (P[1:] - P[:-1]).norm(dim=-1)
    p95 = torch.stack([torch.quantile(V[t][both[t]], 0.95) if int(both[t].sum()) >= 4 else torch.tensor(0.0) for t in range(len(V))])
    p95v = p95[p95 > 0]
    med = float(p95v.median()) if len(p95v) else 1e-4
    lurch = float(p95.max()) / max(med, 1e-4)
    travel = med / max(ref_speed, 1e-4)
    teleport = float(V[both].max()) / 2.0 if int(both.sum()) else 0.0
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
    climb = float((Ls[1:] - Ls[:-1]).clamp(min=0).sum())
    backtrack = max(0.0, climb - noise_rel * float(Ls[1:].sum())) / max(float(Ls[0]), 1.0)
    thr = float(Ls[-1]) + 0.1 * max(drop, 0.0)
    settle = next((i for i, v in enumerate(losses) if v <= thr), len(losses) - 1) / max(1, len(losses) - 1)
    r = dict(loss_start=round(float(Ls[0]), 2), loss_end=round(float(Ls[-1]), 2), mono=round(mono, 3), backtrack=round(backtrack, 3), noise_rel=round(noise_rel, 4), settle=round(settle, 3),
             travel=round(travel, 2), lurch=round(lurch, 2), teleport=round(teleport, 3), jerk_rel=round(jerk_rel, 3), molt_burst=round(win(molt), 3), birth_burst=round(win(born), 3),
             deaths=deaths, n_end=int(A[-1].sum()))
    r["rough"] = round(4 * r["backtrack"] + max(0.0, r["lurch"] / LURCH_OK - 1) + 2 * max(0.0, r["teleport"] / TELEPORT_OK - 1)
                       + max(0.0, r["jerk_rel"] - JERK_OK)
                       + 10 * max(0.0, r["molt_burst"] - BURST_OK) + 10 * max(0.0, r["birth_burst"] - BIRTH_OK), 3)
    return r


@torch.no_grad()
def _grown(model, k, targets, steps, seed):
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([targets[k]], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    # the body's own steady-state p95 speed, median over 16 more steps
    a, p0, sp_ = _alive(sw), sw.pos[0].clone(), []
    for _ in range(16):
        sw = model(sw, gen)
        b = a & _alive(sw)
        if int(b.sum()) >= 4:
            sp_.append(float(torch.quantile((sw.pos[0][b] - p0[b]).norm(dim=-1), 0.95)))
        a, p0 = _alive(sw), sw.pos[0].clone()
    ref = float(torch.tensor(sp_).median()) if sp_ else 1e-3
    return sw, gen, ref


@torch.no_grad()
def smooth(model, seed=7, steps=240, window=240, every=16, log=print):
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
    keys = ["mono", "backtrack", "settle", "travel", "lurch", "teleport", "jerk_rel", "molt_burst", "birth_burst", "rough"]
    out["mean"] = {kk: round(sum(v[kk] for v in ev) / len(ev), 3) for kk in keys}
    out["worst"] = {kk: round(max(v[kk] for v in ev), 3) for kk in keys if kk not in ("settle", "travel")}
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
