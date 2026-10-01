"""PLAY-HARDENED learned swarm: the G2 tadpole rule, fine-tuned to be fought and flown through.

Direction "play" of the swarm research portfolio (Tools/NCA/briefs/play.md). The G2 rule
(results/swarm_coevo_g2/rule.pt) grows each element's body plan and sometimes switches plans, but it
was only ever trained on undisturbed swarms. Here the training pool is HIT the way the regenerating
lizard NCA's pool was damaged (README, "regenerating"):

- strike   : swarm_probe.strike's sphere (a vessel ramming through) - removes ~a third
- cull     : a random partial cull (10-40%, element-blind) - fauna being eaten
- scatter  : a blast impulse that shoves (does not kill) everything near a point
- predator : a sphere that sweeps THROUGH the swarm at speed, pushes tadpoles out of its way and
             kills the few it catches in its core. Unlike the other three it is SENSED: each tadpole
             gets 7 extra inputs (where the predator is, how close, which way it moves) inside a
             sense radius of 3 predator radii. Zero when there is no predator, so on the shared
             yardstick (swarm_nca.rollout) the extra inputs are silent.

Objectives: the usual G2 loss at the end of the backpropagated window (so damage at a random time in
the rollout scores healing at several delays), plus a REACTION term while a predator is near: the
mean over the window of sum_alive relu(1 - d / (gap x radius))^2 - i.e. "open a gap around it". The
end-of-window shape loss is what asks the gap to close again behind it.

    python Tools/NCA/play_swarm.py train --tag p1 --steps 3000
    python Tools/NCA/play_swarm.py eval  --rule Tools/NCA/runs/play_p1/rule_01000.pt
    python Tools/NCA/play_swarm.py publish --rule ... --out Tools/NCA/results/play
"""
import argparse
import json
import math
import os
import shutil
import sys
import time
from dataclasses import asdict, dataclass, replace

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
from swarm_nca import A, C, DIE, DIE_AT, S_MAX, Swarm  # noqa: E402
import swarm_probe as sp  # noqa: E402

torch.set_num_threads(4)
G2 = os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt")
NP = 7                       # predator inputs per tadpole


# --------------------------------------------------------------------- rule ---

class PlayRule(sn.SwarmRule):
    """SwarmRule + predator sensing and predator physics. Extra state lives on the model:
    self.pred = dict(p [B,3], v [B,3], r [B], on [B] bool) or None; it is dropped whenever a swarm
    with clock 0 (a fresh seed) or a different batch size comes in, so the shared yardstick never
    sees a stale predator. self.kills counts predator kills per sample."""

    def __init__(self, world, hidden=192, fire_rate=0.5, p_kill=0.5, core=0.75, push=0.8):
        super().__init__(world, hidden, fire_rate)
        self.F += NP
        w1 = torch.empty(hidden, self.F); nn.init.xavier_uniform_(w1)
        self.w1 = nn.Parameter(w1)
        self.pred = None
        self.kills = None
        self.p_kill, self.core, self.push = p_kill, core, push
        self.avoid_acc = None      # (training) per-sample running reaction penalty, with gradient
        self.gap = 1.6
        self.pred_gain = 1.0       # predator inputs are multiplied by this (p2: 3, so the zero-init columns matter sooner)

    def set_predator(self, p, v, r, on):
        self.pred = dict(p=p.clone(), v=v.clone(), r=r.clone(), on=on.clone())

    def pred_feats(self, pos, B, N):
        n = B * N
        if self.pred is None or not bool(self.pred["on"].any()):
            return torch.zeros(n, NP)
        pp = self.pred["p"][:, None, :].expand(B, N, 3).reshape(n, 3)
        pv = self.pred["v"][:, None, :].expand(B, N, 3).reshape(n, 3)
        rs = (3.0 * self.pred["r"])[:, None].expand(B, N).reshape(n)
        on = self.pred["on"][:, None].expand(B, N).reshape(n).to(pos.dtype)
        rel = (pos - pp) / rs[:, None]
        f = (1 - (rel * rel).sum(-1)).clamp(min=0) * on
        return self.pred_gain * torch.cat([rel * f[:, None], f[:, None], pv / 3.0 * f[:, None]], 1)

    def forward(self, sw: Swarm, gen=None, bud=True, fire=None):
        B = sw.B
        if self.pred is not None and (self.pred["p"].shape[0] != B or bool((sw.clock == 0).all())):
            self.pred = None
        if self.kills is None or self.kills.shape[0] != B or bool((sw.clock == 0).all()):
            self.kills = torch.zeros(B, dtype=torch.long)
        return self._step(sw, gen, bud, fire)

    def _step(self, sw: Swarm, gen=None, bud=True, fire=None):
        W = self.world
        B, N, _ = sw.pos.shape
        n = B * N
        pos, s = sw.pos.reshape(n, 3), sw.s.reshape(n, C)
        elem, dom, act = sw.elem.reshape(n), sw.dom.reshape(n), sw.active.reshape(n)
        hatched = sw.hatched.reshape(n)
        gi, gj = sn.edges(sw, W.R)
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
        pf = self.pred_feats(pos, B, N)
        feats = torch.cat([self.perceive(pos, x, dom, gi[e], gj[e], n), glob, pf], 1).index_select(0, idx)
        out = self.mlp(feats)
        ds = torch.zeros(n, C).index_copy(0, idx, out[:, :C])
        vmax = torch.tensor(W.vmax)[elem].index_select(0, idx)[:, None]
        v = torch.zeros(n, 3).index_copy(0, idx, vmax * torch.tanh(out[:, C:]))
        s = (s + ds).clamp(-S_MAX, S_MAX)
        pos = pos + v
        dxc = pos[gj] - pos[gi]
        r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - r).clamp(min=0) / W.r0
        pos = pos + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = pos.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        pos = pos - 0.5 * (rad - W.membrane).clamp(min=0) * pos / rad
        # ---- the predator: kills in its core, shoves everything else out of its sphere, then moves on
        killed = torch.zeros(n, dtype=torch.bool)
        if self.pred is not None and bool(self.pred["on"].any()):
            pr = self.pred
            pp = pr["p"][:, None, :].expand(B, N, 3).reshape(n, 3)
            rr = pr["r"][:, None].expand(B, N).reshape(n)
            on = pr["on"][:, None].expand(B, N).reshape(n)
            dp = pos - pp
            dn = (dp * dp).sum(-1).clamp(min=1e-8).sqrt()
            with torch.no_grad():
                killed = on & act & hatched & (dn < self.core * rr) & (torch.rand(n, generator=gen) < self.p_kill)
            inside = ((rr - dn).clamp(min=0) * on.to(pos.dtype))
            pos = pos + self.push * inside[:, None] * dp / dn[:, None]
            if self.avoid_acc is not None:
                live = (act & hatched & ~killed).to(pos.dtype) * on.to(pos.dtype)
                pen = (F.relu(1 - dn / (self.gap * rr)) ** 2 * live).view(B, N).sum(1)
                self.avoid_acc = self.avoid_acc + pen
            with torch.no_grad():
                pr["p"] = pr["p"] + pr["v"] * pr["on"][:, None].to(pos.dtype)
                self.kills = self.kills + killed.view(B, N).sum(1)
        with torch.no_grad():
            hat2 = hatched | (act & (s[:, A] > 0.1))
            died = (act & hatched & (s[:, DIE] > DIE_AT)) | killed
            alive = hat2 & ~died
            near = alive.float().clone().scatter_reduce(0, gi, alive[gj].float(), reduce="amax", include_self=True) > 0
            age = sw.age.reshape(n) + (act & ~hat2).long()
            gone_egg = act & ~hat2 & (~near | (age > W.egg_life))
            keep = act & ~died & ~gone_egg
            new_hatched = hat2 & keep
            deaths = sw.deaths + died.view(B, N).sum(1)
        s = s * keep[:, None].to(s.dtype)
        out_sw = Swarm(pos.view(B, N, 3), s.view(B, N, C), sw.elem.clone(), sw.dom.clone(), keep.view(B, N),
                       new_hatched.view(B, N), deaths, sw.mutants.clone(), (age * keep.long()).view(B, N), sw.clock + 1,
                       sw.plan.clone(), sw.since + 1, sw.bw * keep.view(B, N).to(sw.bw.dtype))
        if bud:
            self.lay(out_sw, gi, gj, gen)
        return out_sw


def load_play(path, **kw):
    st = torch.load(path, weights_only=False, map_location="cpu")
    w = st["world"]; w["vmax"] = tuple(w["vmax"])
    rule = PlayRule(sn.World(**w), hidden=st["hidden"], **kw)
    sd = st["rule"]
    for k, v in rule.state_dict().items():                 # G2 has no predator columns: they enter at zero
        if sd[k].shape != v.shape:
            pad = torch.zeros_like(v); pad[tuple(slice(0, d) for d in sd[k].shape)] = sd[k]; sd[k] = pad
    rule.load_state_dict(sd)
    for k in ("p_kill", "core", "push", "gap", "pred_gain"):
        if k in st.get("play", {}):
            setattr(rule, k, st["play"][k])
    return rule


def save_play(rule, path, step, extra=None):
    torch.save(dict(rule={k: v.detach().cpu() for k, v in rule.state_dict().items()}, world=asdict(rule.world),
                    hidden=rule.hidden, step=step, play=dict(p_kill=rule.p_kill, core=rule.core, push=rule.push, gap=rule.gap, pred_gain=rule.pred_gain),
                    **(extra or {})), path)


# ------------------------------------------------------------ interactions ---

def _alive(sw, b):
    return sw.active[b] & sw.hatched[b]


def _centre_rms(sw, b):
    m = _alive(sw, b)
    p = sw.pos[b][m].detach()
    if len(p) < 2:
        return torch.zeros(3), 1.0
    c = p.mean(0)
    return c, float(((p - c) ** 2).sum(-1).mean().sqrt())


@torch.no_grad()
def strike(sw, b, gen, frac=1.0):
    one = sw.index(torch.tensor([b]))
    k = sp.strike(one, frac=frac, gen=gen)
    for a in ("active", "hatched", "s"):
        getattr(sw, a)[b] = getattr(one, a)[0]
    return k


@torch.no_grad()
def cull(sw, b, gen, frac):
    m = _alive(sw, b)
    idx = m.nonzero().squeeze(1)
    k = int(len(idx) * frac)
    if len(idx) - k < 8:
        return 0
    kill = idx[torch.randperm(len(idx), generator=gen)[:k]]
    sw.active[b, kill] = False; sw.hatched[b, kill] = False; sw.s[b, kill] = 0.0
    return k


@torch.no_grad()
def scatter(sw, b, gen, amp):
    c, rms = _centre_rms(sw, b)
    d = torch.randn(3, generator=gen); d = d / d.norm().clamp(min=1e-6)
    o = c + 0.6 * rms * d
    dp = sw.pos[b] - o
    dn = dp.norm(dim=-1, keepdim=True).clamp(min=1e-3)
    sw.pos[b] += sw.active[b][:, None].float() * amp * torch.exp(-(dn / rms) ** 2) * dp / dn


@torch.no_grad()
def predator_path(sw, b, gen, t_cross, speed, radius, offset=0.4):
    """A straight pass that crosses the swarm's centroid (+ a lateral offset in rms units) at step t_cross."""
    c, rms = _centre_rms(sw, b)
    d = torch.randn(3, generator=gen); d = d / d.norm().clamp(min=1e-6)
    lat = torch.randn(3, generator=gen); lat = lat - (lat @ d) * d; lat = lat / lat.norm().clamp(min=1e-6)
    off = float(torch.rand((), generator=gen)) * offset * rms
    v = d * speed
    p = c + off * lat - v * t_cross
    return p, v


def relabel(sw, b):
    """After a hit, the swarm is scored against its CURRENT majority's plan (as the design says)."""
    new = sn.majority_plan(sw, b, None)
    if new is not None and sn.KINDS.index(new) != int(sw.plan[b]):
        sw.plan[b] = sn.KINDS.index(new); sw.since[b] = 0


# ------------------------------------------------------------------ train ---

@dataclass
class PlayCfg:
    tag: str = "p1"
    init: str = G2
    steps: int = 3000
    lr: float = 2e-4
    pool: int = 12
    per_kind: int = 2
    roll_min: int = 48
    roll_max: int = 96
    bptt: int = 28
    seed_every: int = 8
    pregrow: int = 240
    seed: int = 1
    # G2's switching (sticky labels, ratio / tie culls)
    p_switch: float = 0.2
    switch_cooldown: int = 240
    p_ratio: float = 0.5
    margin: float = 0.15
    # interactions (per picked sample per step)
    p_strike: float = 0.25
    p_cull: float = 0.08
    p_scatter: float = 0.12
    p_pred: float = 0.35
    pred_speed: tuple = (1.0, 3.0)
    pred_radius: tuple = (4.0, 8.0)
    w_avoid: float = 2.0       # x mean over the window of sum relu(1 - d/(gap r))^2
    gap: float = 1.6
    pred_gain: float = 1.0
    # loss (G2's)
    w_over: float = 1.0
    min_body: float = 76.0
    w_body: float = 20.0
    replace_above: float = 120.0
    snap_every: int = 250
    log_every: int = 10


def train(cfg: PlayCfg):
    run = os.path.join(HERE, "runs", f"play_{cfg.tag}")
    os.makedirs(run, exist_ok=True)
    L = sn.LossCfg(w_over=cfg.w_over, min_body=cfg.min_body, w_body=cfg.w_body)
    targets = sn.load_targets()
    torch.manual_seed(cfg.seed)
    gen = sn.make_gen(cfg.seed)
    rule = load_play(cfg.init)
    rule.gap = cfg.gap
    rule.pred_gain = cfg.pred_gain
    opt = torch.optim.Adam(rule.parameters(), lr=cfg.lr)
    sched = torch.optim.lr_scheduler.MultiStepLR(opt, [int(cfg.steps * 0.6), int(cfg.steps * 0.85)], 0.3)
    ck = os.path.join(run, "latest.pt")
    start = 0
    if os.path.exists(ck):
        st = torch.load(ck, weights_only=False)
        rule.load_state_dict(st["rule"]); opt.load_state_dict(st["opt"]); sched.load_state_dict(st["sched"])
        pool = st["pool"]; start = st["step"]
        print(f"resumed at {start}", flush=True)
    else:
        pool = {}
        with torch.no_grad():
            for k in sn.KINDS:
                sw = sn.seed_swarm([targets[k]] * cfg.pool, rule.world, gen)
                for _ in range(cfg.pregrow):
                    sw = rule(sw, gen)
                pool[k] = sw.detach()
                print(f"pregrew {k}", flush=True)
    json.dump(asdict(cfg), open(os.path.join(run, "config.json"), "w"), indent=1)
    log = open(os.path.join(run, "log.jsonl"), "a")
    for step in range(start, cfg.steps):
        t0 = time.time()
        picks, batch, ev = {}, [], []
        for k in sn.KINDS:
            idx = torch.randperm(cfg.pool, generator=gen)[:cfg.per_kind]
            picks[k] = idx
            sub = pool[k].index(idx).clone()
            if step % cfg.seed_every == 0:
                sub = Swarm.cat([sn.seed_swarm([targets[k]], rule.world, gen), sub.index(torch.arange(1, cfg.per_kind))])
            for j in range(cfg.per_kind):
                if int(sub.clock[j]) > 0 and int(sub.since[j]) >= cfg.switch_cooldown \
                        and float(torch.rand((), generator=gen)) < cfg.p_switch:
                    mode = "ratio" if float(torch.rand((), generator=gen)) < cfg.p_ratio else "tie"
                    new = sn.lose_majority(sub, j, gen, mode=mode, margin=cfg.margin, targets=targets)
                    if new is not None:
                        sub.plan[j] = sn.KINDS.index(new); sub.since[j] = 0
            batch.append(sub)
        sw = Swarm.cat(batch)
        B = sw.B
        T = int(torch.randint(cfg.roll_min, cfg.roll_max + 1, (1,), generator=gen))
        # schedule this rollout's interactions: (kind, sample, step); predators are set up front
        events = []
        pp, pv = torch.zeros(B, 3), torch.zeros(B, 3)
        pr, pon = torch.ones(B), torch.zeros(B, dtype=torch.bool)
        pred_start = torch.zeros(B, dtype=torch.long)
        for b in range(B):
            if int(sw.clock[b]) < 60:          # a fresh seed has no body to hit yet
                continue
            u = lambda: float(torch.rand((), generator=gen))
            if u() < cfg.p_strike:
                events.append(("strike", b, int(torch.randint(0, T, (1,), generator=gen))))
            if u() < cfg.p_cull:
                events.append(("cull", b, int(torch.randint(0, T, (1,), generator=gen))))
            if u() < cfg.p_scatter:
                events.append(("scatter", b, int(torch.randint(0, T, (1,), generator=gen))))
            if u() < cfg.p_pred:
                spd = cfg.pred_speed[0] + u() * (cfg.pred_speed[1] - cfg.pred_speed[0])
                rad = cfg.pred_radius[0] + u() * (cfg.pred_radius[1] - cfg.pred_radius[0])
                # cross the centroid inside the backpropagated window (80%) or anywhere (20%)
                tc = (T - cfg.bptt + int(torch.randint(4, cfg.bptt, (1,), generator=gen))) if u() < 0.8 \
                    else int(torch.randint(0, T, (1,), generator=gen))
                _, rms = _centre_rms(sw, b)
                lead = int((rms * 1.5 + 3 * rad) / spd)            # appear outside its sense range
                t0b = max(0, tc - lead)
                pred_start[b] = t0b
                p, v = predator_path(sw, b, gen, tc - t0b, spd, rad)
                pp[b], pv[b], pr[b] = p, v, rad
                events.append(("pred", b, t0b))
        rule.pred = None
        rule.kills = torch.zeros(B, dtype=torch.long)
        rule.avoid_acc = None
        pending = {}
        for e in events:
            pending.setdefault(e[2], []).append(e)

        def fire_events(t, sw_):
            for kind, b, _ in pending.get(t, []):
                if kind == "strike":
                    strike(sw_, b, gen, frac=0.6 + 0.6 * float(torch.rand((), generator=gen)))
                elif kind == "cull":
                    cull(sw_, b, gen, 0.1 + 0.3 * float(torch.rand((), generator=gen)))
                elif kind == "scatter":
                    scatter(sw_, b, gen, 4.0 + 8.0 * float(torch.rand((), generator=gen)))
                elif kind == "pred":
                    if rule.pred is None:
                        rule.set_predator(pp, pv, pr, torch.zeros(B, dtype=torch.bool))
                    # aim at the swarm as it is NOW: appear outside its sense range, cross the centroid
                    spd, rad = float(pv[b].norm()), float(pr[b])
                    lead = max(1, int((_centre_rms(sw_, b)[1] * 1.5 + 3 * rad) / spd))
                    p, v = predator_path(sw_, b, gen, lead, spd, rad)
                    rule.pred["p"][b], rule.pred["v"][b], rule.pred["on"][b] = p, v, True
                if kind in ("strike", "cull"):
                    relabel(sw_, b)

        with torch.no_grad():
            for t in range(T - cfg.bptt):
                fire_events(t, sw)
                sw = rule(sw, gen)
        sw = sw.detach()
        if rule.pred is not None:
            rule.pred = {k: v.detach() for k, v in rule.pred.items()}
        rule.avoid_acc = torch.zeros(B)
        for t in range(T - cfg.bptt, T):
            fire_events(t, sw)
            sw = rule(sw, gen)
        avoid = rule.avoid_acc / cfg.bptt
        rule.avoid_acc = None
        groups = [k for k in sn.KINDS for _ in range(cfg.per_kind)]
        losses, infos, plans = [], [], []
        for b, g in enumerate(groups):
            k = sn.KINDS[int(sw.plan[b])]
            x = sn.decode(sw, b)
            l, info = sn.swarm_loss(x, targets[k], L)
            l = l + cfg.w_avoid * avoid[b]
            info["avoid"] = float(avoid[b])
            losses.append(l); infos.append(info); plans.append(k)
        ok = [bool(torch.isfinite(l)) for l in losses]
        opt.zero_grad()
        nonfinite = 0
        if any(ok):
            loss = torch.stack([l for l, g_ in zip(losses, ok) if g_]).sum() / cfg.per_kind
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
        sched.step()
        sw = sw.detach()
        kills = rule.kills.clone()
        rule.pred = None
        for i, k in enumerate(sn.KINDS):
            for j in range(cfg.per_kind):
                b = i * cfg.per_kind + j
                one = sw.index(torch.tensor([b]))
                if (not ok[b]) or int(one.active[0].sum()) == 0 or infos[b]["sink"] > cfg.replace_above \
                        or not bool(torch.isfinite(one.s).all()):
                    nonfinite += 0 if ok[b] else 1
                    one = sn.seed_swarm([targets[k]], rule.world, gen)
                pi = int(picks[k][j])
                for a in Swarm.FIELDS:
                    getattr(pool[k], a)[pi] = getattr(one, a)[0]
        dt = time.time() - t0
        if step % cfg.log_every == 0 or step == cfg.steps - 1:
            evs = {}
            for kind, b, _ in events:
                evs[kind] = evs.get(kind, 0) + 1
            rec = dict(step=step, loss=float(loss), T=T, sec=round(dt, 2), nf=nonfinite, ev=evs,
                       kills=int(kills.sum()), smax=round(float(sw.s.abs().amax()), 1),
                       avoid=[round(i_["avoid"], 2) for i_ in infos],
                       sink=[round(i_["sink"], 2) for i_ in infos], n=[int(_alive(sw, b).sum()) for b in range(B)],
                       plan=[p_[:2] for p_ in plans])
            log.write(json.dumps(rec) + "\n"); log.flush()
            print(f"{step:5d} loss {float(loss):7.2f} T{T} {dt:4.1f}s ev{evs} kills{int(kills.sum())} "
                  f"sink {' '.join(f'{x:.0f}' for x in rec['sink'])} avoid {' '.join(f'{x:.1f}' for x in rec['avoid'])}", flush=True)
        if (step + 1) % cfg.snap_every == 0 or step == cfg.steps - 1:
            torch.save(dict(rule=rule.state_dict(), opt=opt.state_dict(), sched=sched.state_dict(), pool=pool, step=step + 1), ck)
            path = os.path.join(run, f"rule_{step + 1:05d}.pt")
            save_play(rule, path, step + 1)
            res = evaluate(rule)
            res["step"] = step + 1
            with open(os.path.join(run, "evals.jsonl"), "a") as f:
                f.write(json.dumps(res) + "\n")
            print("EVAL", json.dumps(brief(res)), flush=True)
    return rule


# --------------------------------------------------------------- evaluate ---

@torch.no_grad()
def predator_probe(model, steps=240, regrow=120, seed=21, speed=2.0, radius=6.0, record=False, L=None):
    """Grow each plan, then fly a predator (radius `radius`, `speed` per step) straight through its centroid.
    Reported per plan: kills; `gap` = mean distance from the predator's centre to the nearest tadpole
    while it is inside the swarm (radius = no gap opened, it only pushes; bigger = they part ahead of it);
    `crowd` = mean live tadpoles within 1.6 radii then; shape before / worst / after `regrow` steps; heal."""
    L = L or sn.LossCfg()
    targets = sn.load_targets()
    gen = sn.make_gen(seed)
    out, rec = {}, {}
    for k in sn.KINDS:
        sw = sn.seed_swarm([targets[k]], model.world, gen)
        frames, ns, track = [], [], []
        def snap():
            x = sn.decode(sw, 0); vis = x["hatched"]; tier = x["tier"].argmax(1)
            u = torch.cat([x["p"], x["elem"][:, None].float(), x["dom"][:, None].float(), x["h"], tier[:, None].float(),
                           x["f"], x["sp"], x["w"][:, None]], 1)[vis]
            frames.append(u.numpy()); ns.append(int(vis.sum()))
            pr = getattr(model, "pred", None)
            track.append([round(float(v_), 2) for v_ in pr["p"][0]] + [float(pr["r"][0])] if pr is not None and bool(pr["on"][0]) else None)
        for t in range(steps):
            sw = model(sw, gen)
            if record and t % 5 == 0:
                snap()
        score = lambda: round(sn.swarm_loss(sn.decode(sw, 0), targets[k], L)[1]["sink"], 2)
        r = dict(before=score(), n_before=int(_alive(sw, 0).sum()))
        c, rms = _centre_rms(sw, 0)
        d = torch.randn(3, generator=gen); d = d / d.norm()
        dist = rms * 1.5 + 3 * radius + 10
        n_pass = int(2 * dist / speed)
        pp = (c - d * dist)[None]; pv = (d * speed)[None]
        has = isinstance(model, PlayRule)
        if has:
            model.set_predator(pp, pv, torch.tensor([radius]), torch.tensor([True]))
            model.kills = torch.zeros(1, dtype=torch.long)
        p_cur = pp[0].clone()
        gaps, crowd, worst, kills = [], [], r["before"], 0
        for t in range(n_pass):
            if not has:      # a model without predator support: apply the same physics from outside
                m = _alive(sw, 0)
                dp = sw.pos[0] - p_cur; dn = dp.norm(dim=-1).clamp(min=1e-6)
                kk = m & (dn < 0.75 * radius) & (torch.rand(len(dn), generator=gen) < 0.5)
                sw.active[0, kk] = False; sw.hatched[0, kk] = False; sw.s[0, kk] = 0; kills += int(kk.sum())
                sw.pos[0] += 0.8 * (radius - dn).clamp(min=0)[:, None] * dp / dn[:, None]
                p_cur = p_cur + pv[0]
            sw = model(sw, gen)
            if has:
                p_cur = model.pred["p"][0].clone()
            if record and t % 2 == 0:
                snap()
            m = _alive(sw, 0)
            cc, rr = _centre_rms(sw, 0)
            if float((p_cur - cc).norm()) < 0.7 * rr and int(m.sum()) > 0:
                dn = (sw.pos[0][m] - p_cur).norm(dim=-1)
                gaps.append(float(dn.min())); crowd.append(int((dn < 1.6 * radius).sum()))
            if t % 4 == 0:
                worst = max(worst, score())
        if has:
            kills = int(model.kills[0]); model.pred = None
        r.update(kills=kills, gap=round(float(np.mean(gaps)), 2) if gaps else None,
                 crowd=round(float(np.mean(crowd)), 1) if crowd else None, worst=round(worst, 2), pass_steps=n_pass)
        for t in range(regrow):
            sw = model(sw, gen)
            if record and t % 5 == 0:
                snap()
            if t == 59:
                r["after60"] = score()
        r["after"] = score(); r["n_after"] = int(_alive(sw, 0).sum())
        span = r["worst"] - r["before"]
        r["heal"] = round((r["worst"] - r["after"]) / span, 3) if span > 1e-6 else None
        out[k] = r
        if record:
            nmax = max(ns + [1]); arr = np.zeros((len(frames), nmax, 15), np.float32)
            for i, f in enumerate(frames):
                arr[i, :len(f)] = f
            rec[k] = dict(frames=arr, n=ns, crystals=[], switched_at=None, predator=track)
    return (out, rec) if record else out


@torch.no_grad()
def multi_probe(model, seeds=(11, 12, 13), L=None):
    """swarm_probe.probe over several seeds (the shared probe is one strike per plan - noisy)."""
    rows = [sp.probe(model, seed=s, L=L) for s in seeds]
    heal = [r[k]["heal"] for r in rows for k in sn.KINDS if r[k]["heal"] is not None]
    rec = [r[k]["recovered"] - r[k]["before"] for r in rows for k in sn.KINDS]
    return dict(rows=rows, heal_mean=round(float(np.mean(heal)), 3) if heal else None, n_heal=len(heal),
                excess_mean=round(float(np.mean(rec)), 2))


@torch.no_grad()
def drift_probe(model, steps=240, more=120, seeds=(11, 12, 13), L=None):
    """The control the heal numbers need: grow, score, run `more` steps UNDISTURBED, score again.
    A rule whose body wanders on its own 'heals' to wherever it was going anyway."""
    L = L or sn.LossCfg()
    targets = sn.load_targets()
    out = {}
    for k in sn.KINDS:
        d = []
        for s_ in seeds:
            gen = sn.make_gen(s_)
            sw = sn.seed_swarm([targets[k]], model.world, gen)
            for _ in range(steps):
                sw = model(sw, gen)
            a = sn.swarm_loss(sn.decode(sw, 0), targets[k], L)[1]["sink"]
            for _ in range(more):
                sw = model(sw, gen)
            b = sn.swarm_loss(sn.decode(sw, 0), targets[k], L)[1]["sink"]
            d.append((round(a, 2), round(b, 2)))
        out[k] = d
    out["drift_mean"] = round(float(np.mean([b - a for k in sn.KINDS for a, b in out[k]])), 2)
    return out


def evaluate(rule):
    rule.eval()
    data, summary = sn.rollout(rule, 240)
    passed, close = sn.tests_passed(summary)
    probe = sp.probe(rule)
    mp = multi_probe(rule)
    pred = predator_probe(rule)
    rule.train()
    return dict(tests=passed, close=round(close, 2), summary=summary, probe=probe, multi=mp, pred=pred)


def brief(res):
    p = res["pred"]
    return dict(step=res.get("step"), tests=res["tests"], close=res["close"],
                probe_heal={k: v["heal"] for k, v in res["probe"].items()},
                heal_mean=res["multi"]["heal_mean"], excess=res["multi"]["excess_mean"],
                pred={k: (v["kills"], v["gap"], v["crowd"], v["heal"]) for k, v in p.items()})


def publish(rule_path, out):
    """Write results/<out>/: summary.json, rollout.json (swarm_gpu's publisher format), probe.json,
    predator.json (a predator pass, same frame packing + the predator's track), rule.pt."""
    os.makedirs(out, exist_ok=True)
    rule = load_play(rule_path)
    rule.eval()
    data, summary = sn.rollout(rule, 240)
    passed, close = sn.tests_passed(summary)
    st = torch.load(rule_path, weights_only=False)
    summary["meta"] = dict(device="cpu", step=st.get("step"), tag="play",
                           note=f"play-hardened G2 fine-tune ({os.path.basename(rule_path)})")
    summary["rule"] = "rule.pt"
    summary["tests_passed"] = passed
    json.dump(summary, open(os.path.join(out, "summary.json"), "w"), indent=1)
    json.dump(sn.pack(data, 240), open(os.path.join(out, "rollout.json"), "w"))
    probe = sp.probe(rule)
    mp = multi_probe(rule)
    pred, rec = predator_probe(rule, record=True)
    preds = [pred] + [predator_probe(rule, seed=s_) for s_ in (22, 23)]
    drift = drift_probe(rule)
    json.dump(dict(probe=probe, multi=mp, predator=pred, predator_seeds=preds, drift=drift), open(os.path.join(out, "probe.json"), "w"), indent=1)
    packed = sn.pack(rec, 240)
    for k in sn.KINDS:
        packed[k]["predator"] = rec[k]["predator"]
    json.dump(packed, open(os.path.join(out, "predator.json"), "w"))
    shutil.copy(rule_path, os.path.join(out, "rule.pt"))
    print(json.dumps(dict(tests=passed, close=close, probe={k: v["heal"] for k, v in probe.items()},
                          heal_mean=mp["heal_mean"], pred={k: (v["kills"], v["gap"], v["heal"]) for k, v in pred.items()})))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["train", "eval", "publish"])
    ap.add_argument("--tag", default="p1")
    ap.add_argument("--rule", default=G2)
    ap.add_argument("--out", default=os.path.join(HERE, "results", "play"))
    ap.add_argument("--set", action="append", default=[])
    a = ap.parse_args()
    if a.cmd == "train":
        cfg = PlayCfg(tag=a.tag)
        for kv in a.set:
            k, v = kv.split("=", 1)
            cur = getattr(cfg, k)
            setattr(cfg, k, tuple(float(x) for x in v.split(",")) if isinstance(cur, tuple) else type(cur)(float(v) if isinstance(cur, (int, float)) else v))
        train(cfg)
    elif a.cmd == "eval":
        res = evaluate(load_play(a.rule))
        print(json.dumps(brief(res)))
        print(json.dumps(res["pred"], indent=1))
    else:
        publish(a.rule, a.out)


if __name__ == "__main__":
    main()
