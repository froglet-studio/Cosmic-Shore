"""Metamorphosis swarm: the G2 tadpole rule plus actuators that let a swarm STEER ITS OWN COMPOSITION.

Research direction "meta" (Tools/NCA/briefs/meta.md). The G2 rule picks the right body plan for its
seed but fails 3 of 4 switch tests because, after a cull, the element mix drifts: laying is
element-blind and an element is fixed at birth, so a swarm cannot hold a new majority. This file
relaxes "element fixed at birth" and adds new actuators, all as a subclass of swarm_nca.SwarmRule:

  * METAMORPHOSIS. A hatched tadpole may start turning into another element. It is gradual and
    costly: a designed progress channel climbs 1/K per step for K steps, during which the tadpole
    slows (speed x (1 - slow * progress)) and cannot lay; at the end its element flips to the one it
    chose when it started. At most `meta_cap` of a swarm's live tadpoles may be metamorphosing at
    once. Start probability q = p_meta * sigmoid(meta_gain * s[DRIVE] + meta_bias); the target is
    drawn from softmax(s[PREF]) over the OTHER three elements. Both discrete choices get a
    score-function gradient that is parked in a value-zero carrier channel (CARRY) and moved onto
    the particle's straight-through loss weight (Swarm.bw) only when the metamorphosis COMPLETES - so
    the loss credits the tadpole in its NEW identity, never its old one.
  * SELECTIVE LAYING. The same element preference picks the element of a share `p_cross` of eggs
    (swarm_nca's learned_egg, here on the new PREF channels so G2's hidden channels stay G2's).
  * Domain ALWAYS breeds true and never metamorphoses.

The state is widened from 32 to CE channels; a 32-channel swarm (swarm_nca.seed_swarm, used by the
shared rollout / probe yardsticks) is padded on its first step. The G2 weights load exactly: every
old input column is remapped to its new position and every new column/row starts at zero, so a
fresh MetaRule with `p_meta = 0` reproduces G2 step for step.

    python Tools/NCA/meta_swarm.py selftest
    python Tools/NCA/meta_swarm.py train --run runs/meta/m1 --steps 4000
    python Tools/NCA/meta_swarm.py publish --rule runs/meta/m1/rule_01000.pt --out results/meta
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time
from dataclasses import dataclass, asdict, replace, field

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
from swarm_nca import (Swarm, World, LossCfg, KINDS, A, DIE, LAY, S_MAX, DIE_AT, edges, decode,  # noqa: E402
                       swarm_loss, census, seed_swarm, lose_majority, majority_plan, load_targets, make_gen)

C0 = sn.C                      # 32: the G2 state
PREF = slice(32, 36)           # element preference (metamorph target + chosen egg element), learned
DRIVE = 36                     # metamorphosis drive, learned
PROG = 37                      # metamorphosis progress 0..1, designed (the rule cannot write it)
TGT = 38                       # chosen target element + 1 while metamorphosing (0 otherwise), designed
CARRY = 39                     # value 0, carries the start decision's score-function gradient, designed
CE = 40
DESIGNED = (PROG, TGT, CARRY)


@dataclass
class MetaWorld(World):
    p_meta: float = 0.05       # top start probability per step for a hatched tadpole
    meta_gain: float = 4.0
    meta_bias: float = -4.0    # drive 0 -> q = p_meta * 0.018: rare but present (exploration)
    meta_steps: int = 12       # K: steps a metamorphosis takes
    meta_slow: float = 0.75    # speed multiplier while metamorphosing: 1 - slow * progress
    meta_cap: float = 0.08     # at most this share of a swarm's live tadpoles metamorphose at once


def widen(sw: Swarm) -> Swarm:
    if sw.s.shape[-1] >= CE:
        return sw
    B, N, c = sw.s.shape
    s = torch.cat([sw.s, torch.zeros(B, N, CE - c, dtype=sw.s.dtype)], -1)
    out = Swarm(*[getattr(sw, a) for a in Swarm.FIELDS])
    out.s = s
    return out


class MetaRule(sn.SwarmRule):
    X = CE + 5

    def __init__(self, world: MetaWorld, hidden=192, fire_rate=0.5):
        nn.Module.__init__(self)
        self.world, self.hidden, self.fire_rate = world, hidden, fire_rate
        X = self.X
        self.G = 5
        self.F = X * 3 + X * 3 + 3 + 2 + self.G
        self.w1 = nn.Parameter(torch.empty(hidden, self.F)); nn.init.xavier_uniform_(self.w1)
        self.b1 = nn.Parameter(torch.zeros(hidden))
        self.w2 = nn.Parameter(torch.empty(hidden, hidden)); nn.init.xavier_uniform_(self.w2, gain=0.5)
        self.b2 = nn.Parameter(torch.zeros(hidden))
        self.w3 = nn.Parameter(torch.zeros(CE + 3, hidden))
        self.b3 = nn.Parameter(torch.zeros(CE + 3))
        with torch.no_grad():
            self.b3[A] = 0.04
        self.events = []            # (rollout only) metamorphosis starts/ends, for the NOTE

    # ------------------------------------------------------------ warm start ---
    def load_g2(self, sd):
        """Load a 32-channel SwarmRule state dict into this wider rule, column for column."""
        X0 = C0 + 5
        def col(j):                  # old per-neighbour-feature index -> new
            return j if j < C0 else j + (CE - C0)
        old_cols, new_cols = [], []
        for blk in range(6):         # x, ms, mo, grad(dim 0..2)
            for j in range(X0):
                old_cols.append(blk * X0 + j); new_cols.append(blk * self.X + col(j))
        tail = 3 + 2 + self.G
        for t in range(tail):
            old_cols.append(6 * X0 + t); new_cols.append(6 * self.X + t)
        with torch.no_grad():
            self.w1.zero_(); self.w1[:, new_cols] = sd["w1"][:, old_cols]
            self.b1.copy_(sd["b1"]); self.w2.copy_(sd["w2"]); self.b2.copy_(sd["b2"])
            self.w3.zero_(); self.b3.zero_()
            self.w3[:C0] = sd["w3"][:C0]; self.b3[:C0] = sd["b3"][:C0]
            self.w3[CE:] = sd["w3"][C0:]; self.b3[CE:] = sd["b3"][C0:]

    # ------------------------------------------------------------------ step ---
    def forward(self, sw: Swarm, gen=None, bud=True, fire=None):
        return self._step(widen(sw), gen, bud, fire)

    def _step(self, sw: Swarm, gen=None, bud=True, fire=None):
        W = self.world
        B, N, _ = sw.pos.shape
        n = B * N
        pos, s = sw.pos.reshape(n, 3), sw.s.reshape(n, CE)
        elem, dom, act = sw.elem.reshape(n), sw.dom.reshape(n), sw.active.reshape(n)
        hatched = sw.hatched.reshape(n)
        gi, gj = edges(sw, W.R)
        x = torch.cat([s, F.one_hot(elem, 4).to(s.dtype), hatched[:, None].to(s.dtype)], 1)
        fire = act & ((torch.rand(n, generator=gen) <= self.fire_rate) if fire is None else fire.reshape(n))
        idx = fire.nonzero().squeeze(1)
        e = fire[gi]
        with torch.no_grad():
            hb = (sw.hatched & sw.active).float()
            cnt = hb.sum(1).clamp(min=1)
            mixe = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1) / cnt[:, None]
            glob = torch.cat([(cnt / 100)[:, None], mixe], 1)
            glob = glob[:, None, :].expand(B, N, self.G).reshape(n, self.G)
        feats = torch.cat([self.perceive(pos, x, dom, gi[e], gj[e], n), glob], 1).index_select(0, idx)
        out = self.mlp(feats)
        ds = torch.zeros(n, CE).index_copy(0, idx, out[:, :CE])
        ds[:, list(DESIGNED)] = 0.0                                       # designed channels are the world's
        meta_now = s[:, PROG].detach() > 0
        slow = (1 - W.meta_slow * s[:, PROG].detach()).clamp(0.1, 1.0)
        vmax = torch.tensor(W.vmax)[elem].index_select(0, idx)[:, None] * slow.index_select(0, idx)[:, None]
        v = torch.zeros(n, 3).index_copy(0, idx, vmax * torch.tanh(out[:, CE:]))
        s = (s + ds).clamp(-S_MAX, S_MAX)
        pos = pos + v
        dxc = pos[gj] - pos[gi]
        r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - r).clamp(min=0) / W.r0
        pos = pos + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = pos.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        pos = pos - 0.5 * (rad - W.membrane).clamp(min=0) * pos / rad
        with torch.no_grad():
            hat2 = hatched | (act & (s[:, A] > 0.1))
            died = act & hatched & (s[:, DIE] > DIE_AT)
            alive = hat2 & ~died
            near = alive.float().clone().scatter_reduce(0, gi, alive[gj].float(), reduce="amax", include_self=True) > 0
            age = sw.age.reshape(n) + (act & ~hat2).long()
            gone_egg = act & ~hat2 & (~near | (age > W.egg_life))
            keep = act & ~died & ~gone_egg
            new_hatched = hat2 & keep
            deaths = sw.deaths + died.view(B, N).sum(1)
        s = s * keep[:, None].to(s.dtype)
        new_elem = sw.elem.clone()
        bw = sw.bw * keep.view(B, N).to(sw.bw.dtype)
        s, new_elem, bw = self.metamorph(s, new_elem, bw, new_hatched.view(B, N), gen)
        out_sw = Swarm(pos.view(B, N, 3), s.view(B, N, CE), new_elem, sw.dom.clone(), keep.view(B, N),
                       new_hatched.view(B, N), deaths, sw.mutants.clone(), (age * keep.long()).view(B, N), sw.clock + 1,
                       sw.plan.clone(), sw.since + 1, bw)
        if bud:
            self.lay(out_sw, gi, gj, gen)
        return out_sw

    def metamorph(self, s, elem, bw, live, gen):
        """Advance running metamorphoses, finish the complete ones, start new ones (capped)."""
        W = self.world
        if W.p_meta <= 0:
            return s, elem, bw
        B, N = elem.shape
        n = B * N
        live = live.reshape(n)
        prog = s[:, PROG].detach()
        running = live & (prog > 0)
        prog2 = torch.where(running, prog + 1.0 / W.meta_steps, prog)
        done = running & (prog2 >= 1.0 - 1e-6)
        e_flat = elem.reshape(n).clone()
        carry = s[:, CARRY]
        if bool(done.any()):
            tg = (s[:, TGT].detach().round().long() - 1).clamp(0, 3)
            e_flat = torch.where(done, tg, e_flat)
            # the start decision is credited now, on the tadpole in its NEW identity
            bw = bw + (carry * done.to(carry.dtype)).view(B, N)
        # start new ones
        drive = s[:, DRIVE]
        q = W.p_meta * torch.sigmoid(W.meta_gain * drive + W.meta_bias)
        logits = s[:, PREF] - 1e4 * F.one_hot(e_flat, 4).to(s.dtype)       # never "into" its own element
        pe = torch.softmax(logits, -1)
        with torch.no_grad():
            cap = (W.meta_cap * live.view(B, N).sum(1).float()).floor()
            busy = (live & (prog2 > 0) & ~done).view(B, N).sum(1).float()
            room = (cap - busy).clamp(min=0)
            cand = live & (prog2 <= 0) & (torch.rand(n, generator=gen) < q.detach())
            start = torch.zeros(n, dtype=torch.bool)
            cb = cand.view(B, N)
            for b in range(B):
                k = int(room[b])
                if k <= 0:
                    continue
                ids = cb[b].nonzero().squeeze(1)
                if len(ids) == 0:
                    continue
                ids = ids[torch.randperm(len(ids), generator=gen)[:k]]
                start[b * N + ids] = True
            chosen = torch.multinomial(pe.detach().clamp(min=1e-8), 1, generator=gen).squeeze(1)
        sf = start.to(s.dtype)
        score = (q - q.detach()) / q.detach().clamp(min=1e-3) \
            + (pe.gather(1, chosen[:, None]).squeeze(1) - pe.gather(1, chosen[:, None]).squeeze(1).detach()) \
            / pe.gather(1, chosen[:, None]).squeeze(1).detach().clamp(min=0.05)
        new_prog = torch.where(done, torch.zeros_like(prog2), torch.where(start, torch.full_like(prog2, 1.0 / W.meta_steps), prog2))
        new_tgt = torch.where(start, (chosen + 1).to(s.dtype), torch.where(done, torch.zeros_like(prog), s[:, TGT].detach()))
        new_carry = torch.where(start, sf * score, torch.where(done, torch.zeros_like(carry), carry))
        live_f = live.to(s.dtype)
        s = torch.cat([s[:, :PROG], (new_prog * live_f)[:, None], (new_tgt * live_f)[:, None],
                       (new_carry * live_f)[:, None], s[:, CARRY + 1:]], 1)
        if self.events is not None and B == 1 and not self.training:
            self.events.append((int(start.sum()), int(done.sum()), int((new_prog > 0).sum())))
        return s, e_flat.view(B, N), bw

    def lay(self, sw: Swarm, gi, gj, gen=None):
        """swarm_nca's laying, with the egg-element choice read from PREF; a metamorphosing tadpole
        does not lay."""
        W = self.world
        B, N, _ = sw.pos.shape
        busy = sw.s[:, :, PROG].reshape(-1) > 0
        q = torch.sigmoid(W.lay_gain * sw.s[:, :, LAY].reshape(-1) + W.lay_bias) if W.learned_lay else torch.ones(B * N)
        q = q * (~busy).to(q.dtype)
        pe = torch.softmax(sw.s[:, :, PREF].reshape(B * N, 4), -1) if W.learned_egg else None
        laid = self._lay(sw, gi, gj, gen, q.detach(), None if pe is None else pe.detach())
        if not laid:
            return
        child = torch.cat([b * N + c for b, c, *_ in laid]); par = torch.cat([b * N + p for b, _, p, *_ in laid])
        dl = torch.zeros(len(child), dtype=sw.s.dtype)
        if W.learned_lay:
            qp = q[par]
            dl = dl + (qp - qp.detach()) / qp.detach().clamp(min=0.1)
        if pe is not None:
            crossed = torch.cat([x for *_, x, _ in laid]); chosen = torch.cat([y for *_, y in laid])
            pc = pe[par, chosen]
            dl = dl + crossed.to(dl.dtype) * (pc - pc.detach()) / pc.detach().clamp(min=0.05)
        sw.bw = sw.bw + torch.zeros(B * N, dtype=dl.dtype).index_add(0, child, dl).view(B, N)


def load_meta(path):
    st = torch.load(path, weights_only=False, map_location=sn.DEVICE)
    w = st["world"]; w["vmax"] = tuple(w["vmax"])
    rule = MetaRule(MetaWorld(**w), hidden=st["hidden"])
    rule.load_state_dict(st["rule"])
    return rule


def from_g2(path, world: MetaWorld):
    st = torch.load(path, weights_only=False, map_location=sn.DEVICE)
    rule = MetaRule(world, hidden=st["hidden"])
    rule.load_g2(st["rule"])
    return rule


# ------------------------------------------------------------------ train ---

@dataclass
class MetaCfg:
    run: str = os.path.join(HERE, "runs", "meta", "m1")
    init: str = os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt")
    steps: int = 4000
    lr: float = 3e-4
    pool: int = 24
    per_kind: int = 2
    roll_min: int = 48
    roll_max: int = 96
    bptt: int = 28
    seed_every: int = 6
    log_every: int = 10
    snap_every: int = 250
    seed: int = 0
    p_switch: float = 0.3
    switch_cooldown: int = 120
    p_ratio: float = 0.35          # share of switches culled to the new plan's mix
    p_excess: float = 0.35         # share culled to "one behind" (the evaluation's cull); the rest "tie"
    margin: float = 0.15
    replace_above: float = 120.0
    w_meta: float = 0.0            # cost per metamorphosis START (in loss units per started tadpole / plan size)
    # world knobs
    p_meta: float = 0.05
    meta_bias: float = -4.0
    meta_steps: int = 12
    meta_cap: float = 0.08
    learned_egg: int = 1
    p_cross: float = 0.1


def train(cfg: MetaCfg, resume=True, on_snapshot=None):
    os.makedirs(cfg.run, exist_ok=True)
    world = MetaWorld(learned_lay=1, learned_egg=cfg.learned_egg, p_cross=cfg.p_cross, p_meta=cfg.p_meta,
                      meta_bias=cfg.meta_bias, meta_steps=cfg.meta_steps, meta_cap=cfg.meta_cap)
    L = LossCfg(w_over=1.0, min_body=76, w_body=20)                    # G2's loss
    targets = load_targets()
    torch.manual_seed(cfg.seed)
    gen = make_gen(cfg.seed)
    ck = os.path.join(cfg.run, "latest.pt")
    rule = from_g2(cfg.init, world) if cfg.init.endswith("rule.pt") and "coevo" in cfg.init else load_meta(cfg.init)
    rule.world = world
    rule.events = None
    opt = torch.optim.Adam(rule.parameters(), lr=cfg.lr)
    start = 0
    pool = {k: widen(seed_swarm([targets[k]] * cfg.pool, world, gen)) for k in KINDS}
    if resume and os.path.exists(ck):
        st = torch.load(ck, weights_only=False)
        rule.load_state_dict(st["rule"]); opt.load_state_dict(st["opt"]); pool = st["pool"]; start = st["step"]
        print(f"resumed at step {start}", flush=True)
    else:
        print(f"warm start from {cfg.init}", flush=True)
    json.dump(dict(train=asdict(cfg), world=asdict(world), loss=asdict(L)), open(os.path.join(cfg.run, "config.json"), "w"), indent=1)
    log = open(os.path.join(cfg.run, "log.jsonl"), "a")
    for step in range(start, cfg.steps):
        t0 = time.time()
        picks, batch = {}, []
        for k in KINDS:
            idx = torch.randperm(cfg.pool, generator=gen)[:cfg.per_kind]
            picks[k] = idx
            sub = pool[k].index(idx)
            if step % cfg.seed_every == 0:
                sub = Swarm.cat([widen(seed_swarm([targets[k]], world, gen)), sub.index(torch.arange(1, cfg.per_kind))])
            for j in range(1 if step % cfg.seed_every == 0 else 0, cfg.per_kind):
                if int(sub.since[j]) < cfg.switch_cooldown:
                    continue
                if float(torch.rand((), generator=gen)) < cfg.p_switch:
                    u = float(torch.rand((), generator=gen))
                    mode = "ratio" if u < cfg.p_ratio else ("excess" if u < cfg.p_ratio + cfg.p_excess else "tie")
                    new = lose_majority(sub, j, gen, mode=mode, margin=cfg.margin, targets=targets)
                    if new is not None:
                        sub.plan[j] = KINDS.index(new); sub.since[j] = 0
            batch.append(sub)
        sw = Swarm.cat(batch)
        T = int(torch.randint(cfg.roll_min, cfg.roll_max + 1, (1,), generator=gen))
        with torch.no_grad():
            for _ in range(T - cfg.bptt):
                sw = rule(sw, gen)
        sw = sw.detach()
        # a value-zero carrier must not drag a stale graph across windows
        groups = [k for k in KINDS for _ in range(cfg.per_kind)]
        nonfinite = 0
        for b in range(sw.B):
            if not (bool(torch.isfinite(sw.s[b]).all()) and bool(torch.isfinite(sw.pos[b]).all())):
                nonfinite += 1
                sw = Swarm.cat([sw.index(torch.arange(0, b)), widen(seed_swarm([targets[groups[b]]], world, gen)),
                                sw.index(torch.arange(b + 1, sw.B))])
        starts = torch.zeros(())
        for _ in range(cfg.bptt):
            sw = rule(sw, gen)
            if cfg.w_meta:
                q = world.p_meta * torch.sigmoid(world.meta_gain * sw.s[:, :, DRIVE] + world.meta_bias)
                starts = starts + (q * (sw.active & sw.hatched).float()).sum() / sw.B
        losses, infos, plans = [], {}, []
        for b, g in enumerate(groups):
            k = KINDS[int(sw.plan[b])]
            x = decode(sw, b)
            l, info = swarm_loss(x, targets[k], L)
            losses.append(l); plans.append(k)
            infos.setdefault(g, []).append(info)
        ok = [bool(torch.isfinite(l)) for l in losses]
        for b, good in enumerate(ok):
            if not good:
                nonfinite += 1; g_ = groups[b]; infos[g_][b - KINDS.index(g_) * cfg.per_kind]["sink"] = 1e9
        opt.zero_grad()
        if any(ok):
            loss = torch.stack([l for l, good in zip(losses, ok) if good]).sum() / cfg.per_kind
            if cfg.w_meta:
                loss = loss + cfg.w_meta * starts / 100.0
            loss.backward()
            grads = [p.grad for p in rule.parameters() if p.grad is not None]
            if all(bool(torch.isfinite(g_).all()) for g_ in grads):
                for g_ in grads:
                    g_ /= (g_.norm() + 1e-8)
                opt.step()
            else:
                nonfinite += 1
        else:
            loss = torch.zeros(())
        sw = sw.detach()
        sw.s[:, :, CARRY] = 0.0
        for i, k in enumerate(KINDS):
            sub = sw.index(torch.arange(i * cfg.per_kind, (i + 1) * cfg.per_kind))
            for j in range(cfg.per_kind):
                if int(sub.active[j].sum()) == 0 or infos[k][j]["sink"] > cfg.replace_above or not bool(torch.isfinite(sub.s[j]).all()):
                    sub = Swarm.cat([sub.index(torch.arange(0, j)), widen(seed_swarm([targets[k]], world, gen)),
                                     sub.index(torch.arange(j + 1, cfg.per_kind))])
            for j, pi in enumerate(picks[k].tolist()):
                for a in Swarm.FIELDS:
                    getattr(pool[k], a)[pi] = getattr(sub, a)[j]
        dt = time.time() - t0
        if step % cfg.log_every == 0 or step == cfg.steps - 1:
            rev = sum(majority_plan(sw, b, plans[b]) != plans[b] for b in range(sw.B)) / sw.B
            mm = int(((sw.s[:, :, PROG] > 0) & sw.active).sum())
            rec = dict(step=step, loss=float(loss), T=T, sec=round(dt, 2), rev=round(rev, 3), nonfinite=nonfinite, meta=mm,
                       smax=round(float(sw.s[:, :, :C0].abs().nan_to_num(posinf=1e9).amax()), 1))
            for i, k in enumerate(KINDS):
                c = census(sw, i * cfg.per_kind + 1, targets[k])
                inf = infos[k][1]
                rec[k] = dict(plan=plans[i * cfg.per_kind + 1], sink=round(inf["sink"], 3), n=c["n"], el=c["elements"])
            log.write(json.dumps(rec) + "\n"); log.flush()
            print(f"{step:5d} loss {float(loss):8.3f} T{T} {dt:4.1f}s rev{rev:.2f} meta{mm:3d}{f' nf{nonfinite}' if nonfinite else ''} s{rec['smax']:.0f} | " + " | ".join(
                f"{k[:2]}>{rec[k]['plan'][:2]} {rec[k]['sink']:6.2f} n{rec[k]['n']:3d} {rec[k]['el']}" for k in KINDS), flush=True)
        if (step + 1) % cfg.snap_every == 0 or step == cfg.steps - 1:
            torch.save(dict(rule=rule.state_dict(), opt=opt.state_dict(), pool=pool, step=step + 1), ck + ".tmp")
            os.replace(ck + ".tmp", ck)
            path = os.path.join(cfg.run, f"rule_{step + 1:05d}.pt")
            torch.save(dict(rule={k_: v.cpu() for k_, v in rule.state_dict().items()}, world=asdict(world),
                            hidden=rule.hidden, step=step + 1), path)
            if on_snapshot:
                on_snapshot(step + 1, path)
    return rule


# --------------------------------------------------------------- evaluate ---

def evaluate(rule, out=None, meta_tag="meta", note=""):
    """The shared yardstick: swarm_nca.rollout + tests_passed + swarm_probe.probe. Writes the
    results folder exactly as swarm_gpu.py's publisher does (rollout.json = sn.pack(data, 240))."""
    import swarm_probe
    rule.eval()
    rule.events = []
    L = LossCfg()
    t0 = time.time()
    data, summary = sn.rollout(rule, 240, L=L)
    ev = rule.events
    rule.events = []
    pr = swarm_probe.probe(rule, L=L)
    rule.events = None
    rule.train()
    passed, close = sn.tests_passed(summary)
    summary["scale_inv"] = 0
    # per plan: metamorphoses started / completed over the 480-step rollout (events are per step, 4 plans in turn)
    per = len(ev) // 4 if ev else 0
    mstats = {}
    for i, k in enumerate(KINDS):
        seg = ev[i * per:(i + 1) * per]
        mstats[k] = dict(started_grow=sum(a for a, _, _ in seg[:240]), started_after_cull=sum(a for a, _, _ in seg[240:]),
                         completed=sum(b for _, b, _ in seg), peak_running=max([c for *_, c in seg] or [0]))
    summary["metamorphosis"] = mstats
    res = dict(passed=passed, close=round(close, 2), probe=pr, sec=round(time.time() - t0, 1))
    if out:
        os.makedirs(out, exist_ok=True)
        json.dump(sn.pack(data, 240), open(os.path.join(out, "rollout.json"), "w"))
        summary["meta"] = dict(device="cpu", tag=meta_tag, note=note)
        json.dump(summary, open(os.path.join(out, "summary.json"), "w"), indent=1)
        json.dump(pr, open(os.path.join(out, "probe.json"), "w"), indent=1)
    return summary, res


def print_eval(summary, res):
    sn.print_cross(summary); sn.print_geo(summary); sn.print_switch(summary)
    print("metamorphosis:", json.dumps(summary.get("metamorphosis")))
    print(f"TESTS PASSED {res['passed']}/8  close {res['close']}  ({res['sec']}s)")
    print("probe:", json.dumps({k: (v['before'], v['cut'], v['recovered'], v['heal'], v['killed'], v['n_after']) for k, v in res["probe"].items()}))


def selftest():
    torch.set_num_threads(4)
    g2 = sn.load_rule(os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt")); g2.eval()
    w = MetaWorld(**{**asdict(g2.world), "p_meta": 0.0})
    w.vmax = tuple(w.vmax)
    m = MetaRule(w, hidden=g2.hidden); m.load_g2(g2.state_dict()); m.eval(); m.events = None
    targets = load_targets()
    sw = seed_swarm([targets[k] for k in KINDS], g2.world, make_gen(3))
    a, b = sw.clone(), sw.clone()
    ga, gb = make_gen(5), make_gen(5)
    with torch.no_grad():
        for t in range(60):
            a = g2(a, ga); b = m(b, gb)
    err = float((a.pos - b.pos).abs().max()); serr = float((a.s - b.s[:, :, :C0]).abs().max())
    print("G2 vs MetaRule(p_meta=0) after 60 steps: max |dpos|", err, "max |ds|", serr,
          "n", a.active.sum(1).tolist(), b.active.sum(1).tolist())
    assert err < 1e-3 and serr < 1e-3
    # metamorphosis on: elements change, domains never do, progress bounded
    m.world = replace(w, p_meta=1.0, meta_bias=4.0, meta_cap=0.2)
    d0 = None
    with torch.no_grad():
        for t in range(40):
            dom_before, act_before = b.dom.clone(), b.active.clone()
            b = m(b, gb)
            same = act_before & b.active & b.hatched
            assert bool((b.dom[same] == dom_before[same]).all())
    print("after 40 forced-metamorph steps: elements", [torch.bincount(b.elem[i][b.active[i] & b.hatched[i]], minlength=4).tolist() for i in range(4)],
          "running", int((b.s[:, :, PROG] > 0).sum()), "max prog", float(b.s[:, :, PROG].max()))
    assert float(b.s[:, :, PROG].max()) <= 1.0
    # gradient reaches DRIVE/PREF through the carrier
    m.train(); m.events = None
    m.world = replace(w, p_meta=1.0, meta_bias=0.0, meta_cap=0.3, meta_steps=4, learned_lay=1)
    sw = widen(seed_swarm([targets["space"]], g2.world, make_gen(1)))
    with torch.no_grad():
        for _ in range(40):
            sw = m(sw, gb)
    sw = sw.detach()
    for _ in range(10):
        sw = m(sw, gb)
    loss, _ = swarm_loss(decode(sw, 0), targets["space"], LossCfg())
    loss.backward()
    gn = float(m.w3.grad[DRIVE].norm()), float(m.w3.grad[PREF].norm())
    print("grad on DRIVE / PREF rows:", gn)
    assert gn[0] > 0 and gn[1] > 0
    print("selftest ok")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["selftest", "train", "eval", "publish"])
    ap.add_argument("--rule", default="")
    ap.add_argument("--out", default="")
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--eval-every", type=int, default=500)
    a = ap.parse_args()
    torch.set_num_threads(4)
    if a.cmd == "selftest":
        selftest()
    elif a.cmd in ("eval", "publish"):
        rule = load_meta(a.rule) if "coevo" not in a.rule else from_g2(a.rule, MetaWorld(learned_lay=1, p_meta=0.0))
        summary, res = evaluate(rule, a.out or None, note=os.path.relpath(a.rule, HERE))
        print_eval(summary, res)
    elif a.cmd == "train":
        cfg = MetaCfg()
        for kv in a.set:
            k, v = kv.split("=", 1)
            t = type(getattr(cfg, k)); setattr(cfg, k, t(float(v)) if t in (int, float) else v)
        evlog = os.path.join(cfg.run, "evals.jsonl")

        def on_snap(step, path):
            if step % a.eval_every:
                return
            r = load_meta(path)
            summary, res = evaluate(r)
            print(f"=== eval step {step}"); print_eval(summary, res)
            with open(evlog, "a") as f:
                f.write(json.dumps(dict(step=step, rule=path, passed=res["passed"], close=res["close"],
                                        meta=summary["metamorphosis"], probe=res["probe"],
                                        cross=summary["cross"], switch={k: dict(cross=v["cross"], el=v["census"]["elements"], n=v["census"]["n"])
                                                                         for k, v in summary["switch"].items()})) + "\n")
        train(cfg, on_snapshot=on_snap)


if __name__ == "__main__":
    main()
