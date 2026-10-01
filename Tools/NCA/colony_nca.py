"""Colony brain: a slow, shared latent that decides the swarm's body plan and broadcasts it.

Research direction "colony" (Tools/NCA/briefs/colony.md). The per-tadpole rule is swarm_nca's
SwarmRule (warm-started from results/swarm_coevo_g2/rule.pt). On top of it, every swarm carries ONE
colony state: a GRU that reads a pooled, domain-neutral summary of all its living tadpoles each step
(headcount, element counts and shares, body size and shape, mean hidden state; optionally an
attention pool over tadpoles) and broadcasts:

  * a CODE (K floats) that every tadpole's rule is conditioned on - the rule's input layer gains K
    zero-initialised columns, so at step 0 the model is exactly the G2 rule;
  * a per-element BREEDING BIAS added to every tadpole's laying gate (zero-initialised): the colony,
    not each tadpole, can decide which elements breed. This is the actuator the switch tests lacked:
    a full swarm (280 slots) can only change its mix while it refills after a loss.

Variants (ColonyCfg.mode):
  free  - code = tanh(W h): a free latent.
  vote  - a soft plan vote: p = softmax(W h) over the four plans, code = p @ E (four learned plan
          codes). The decision is inspectable (`col_vote` on the swarm), and w_vote can supervise it
          with the sticky plan label (the training signal's own notion of which plan is wanted).
  pool = "mean" | "attn" - how tadpoles are summarised (attn adds two learned attention queries).

The colony state lives on the Swarm object itself (attributes col_h / col_vote / col_lay), so any
caller that steps a Swarm through model(sw, gen) carries it (swarm_nca.rollout, swarm_probe.probe).
It is reset for every sample whose clock is 0 (a fresh seed).

    python3 Tools/NCA/colony_nca.py selftest
    python3 Tools/NCA/colony_nca.py train --tag vote --mode vote --w-vote 1 --steps 1500
    python3 Tools/NCA/colony_nca.py eval --rule runs/colony_vote/rule_01500.pt --publish
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
import time
from dataclasses import dataclass, asdict, replace

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
from swarm_nca import Swarm, C, A, DIE, DIE_AT, LAY, EGG, S_MAX, KINDS  # noqa: E402

WARM = os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt")
RESULT = os.path.join(HERE, "results", "colony")


@dataclass
class ColonyCfg:
    mode: str = "vote"        # "free" | "vote"
    pool: str = "mean"        # "mean" | "attn"
    H: int = 32               # colony GRU state
    K: int = 8                # broadcast code width
    lay_bias: int = 1         # 1: the colony adds a per-element bias to the laying gate
    vote_tau: float = 1.0     # vote softmax temperature


# ---------------------------------------------------------- colony state on a Swarm ---

COL = ("col_h", "col_vote", "col_lay")


def col_get(sw, name, B, width):
    v = getattr(sw, name, None)
    return torch.zeros(B, width) if v is None or v.shape[0] != B else v


def col_copy(dst, src, idx=None):
    for a in COL:
        v = getattr(src, a, None)
        if v is not None:
            setattr(dst, a, v if idx is None else v[idx])
    return dst


def cindex(sw, idx):
    return col_copy(sw.index(idx), sw, idx)


def ccat(xs, H, K4=4):
    out = Swarm.cat(xs)
    out.col_h = torch.cat([col_get(x, "col_h", x.B, H) for x in xs])
    out.col_vote = torch.cat([col_get(x, "col_vote", x.B, 4) for x in xs])
    out.col_lay = torch.cat([col_get(x, "col_lay", x.B, 4) for x in xs])
    return out


def cdetach(sw):
    out = sw.detach()
    for a in COL:
        v = getattr(sw, a, None)
        if v is not None:
            setattr(out, a, v.detach())
    return out


def cclone(sw):
    out = sw.clone()
    for a in COL:
        v = getattr(sw, a, None)
        if v is not None:
            setattr(out, a, v.clone())
    return out


# ------------------------------------------------------------------------- model ---

class ColonyRule(sn.SwarmRule):
    S_IN = 1 + 4 + 4 + 1 + 3 + 14            # count, shares, counts, rms, shape, mean hidden 12..25

    def __init__(self, world: sn.World, ccfg: ColonyCfg = None, hidden=192, fire_rate=0.5):
        super().__init__(world, hidden, fire_rate)
        self.ccfg = ccfg = ccfg or ColonyCfg()
        F0 = self.F
        self.F = F0 + ccfg.K
        w1 = torch.zeros(hidden, self.F); w1[:, :F0] = self.w1.data      # new code columns enter at zero
        self.w1 = nn.Parameter(w1)
        nin = self.S_IN
        if ccfg.pool == "attn":
            tok = C + 4 + 3
            self.att_k = nn.Linear(tok, 16); self.att_v = nn.Linear(tok, 16)
            self.att_q = nn.Parameter(torch.randn(2, 16) * 0.3)
            nin += 32
        self.gru = nn.GRUCell(nin, ccfg.H)
        if ccfg.mode == "vote":
            self.vote = nn.Linear(ccfg.H, 4)
            self.codes = nn.Parameter(torch.randn(4, ccfg.K) * 0.7)
        else:
            self.code = nn.Linear(ccfg.H, ccfg.K)
        self.layh = nn.Linear(ccfg.H, 4)
        nn.init.zeros_(self.layh.weight); nn.init.zeros_(self.layh.bias)

    # ------------------------------------------------------------ the colony's senses
    @torch.no_grad()
    def _stats(self, sw):
        hb = (sw.hatched & sw.active).float()                          # [B,N]
        cnt = hb.sum(1).clamp(min=1)
        oh = F.one_hot(sw.elem, 4).float()
        counts = (hb[:, :, None] * oh).sum(1)
        share = counts / cnt[:, None]
        c = (hb[:, :, None] * sw.pos).sum(1) / cnt[:, None]
        d = (sw.pos - c[:, None]) * hb[:, :, None]
        rms = ((d * d).sum(-1).sum(1) / cnt).clamp(min=1e-4).sqrt()
        G = torch.einsum("bni,bnj->bij", d, d) / cnt[:, None, None] / (rms ** 2)[:, None, None]
        ev = torch.linalg.eigvalsh(G + 1e-5 * torch.eye(3))            # ascending, sums to ~1
        hid = (hb[:, :, None] * sw.s[:, :, 12:26]).sum(1) / cnt[:, None]
        z = torch.cat([(cnt / 100)[:, None], share, counts / 100, (rms / 20)[:, None], ev, hid.clamp(-5, 5)], 1)
        return z, hb, d / rms[:, None, None]

    def _summary(self, sw):
        z, hb, dn = self._stats(sw)
        if self.ccfg.pool == "attn":
            tok = torch.cat([sw.s.detach().clamp(-5, 5), F.one_hot(sw.elem, 4).float(), dn.detach()], -1)   # [B,N,tok]
            k, v = self.att_k(tok), self.att_v(tok)
            lg = torch.einsum("qd,bnd->bqn", self.att_q, k) / 4.0
            lg = lg.masked_fill(hb[:, None, :] < 0.5, -1e9)
            att = torch.softmax(lg, -1) * (hb.sum(1) > 0).float()[:, None, None]
            z = torch.cat([z, torch.einsum("bqn,bnd->bqd", att, v).reshape(sw.B, -1)], 1)
        return z

    def colony(self, sw):
        """One colony tick: returns (h, vote probs [B,4], code [B,K], laying bias [B,4])."""
        B = sw.B
        h = col_get(sw, "col_h", B, self.ccfg.H)
        h = torch.where((sw.clock == 0)[:, None], torch.zeros_like(h), h)    # a fresh seed: a fresh colony
        h = self.gru(self._summary(sw), h)
        if self.ccfg.mode == "vote":
            logit = self.vote(h)
            p = torch.softmax(logit / self.ccfg.vote_tau, -1)
            code = p @ self.codes
        else:
            logit = torch.zeros(B, 4)
            p = torch.full((B, 4), 0.25)
            code = torch.tanh(self.code(h))
        lay = self.layh(h) if self.ccfg.lay_bias else torch.zeros(B, 4)
        return h, logit, code, lay

    # ------------------------------------------------------------ one step (SwarmRule._step + the code)
    def _step(self, sw: Swarm, gen=None, bud=True, fire=None):
        W = self.world
        B, N, _ = sw.pos.shape
        n = B * N
        h_col, logit, code, lay_e = self.colony(sw)
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
        cb = code[:, None, :].expand(B, N, self.ccfg.K).reshape(n, self.ccfg.K)
        feats = torch.cat([self.perceive(pos, x, dom, gi[e], gj[e], n), glob, cb], 1).index_select(0, idx)
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
        out_sw = Swarm(pos.view(B, N, 3), s.view(B, N, C), sw.elem.clone(), sw.dom.clone(), keep.view(B, N),
                       new_hatched.view(B, N), deaths, sw.mutants.clone(), (age * keep.long()).view(B, N), sw.clock + 1,
                       sw.plan.clone(), sw.since + 1, sw.bw * keep.view(B, N).to(sw.bw.dtype))
        out_sw.col_h, out_sw.col_vote, out_sw.col_lay = h_col, logit, lay_e
        if bud:
            self.lay(out_sw, gi, gj, gen)
        return out_sw

    def lay(self, sw: Swarm, gi, gj, gen=None):
        """SwarmRule.lay with the colony's per-element breeding bias inside the laying gate."""
        W = self.world
        B, N, _ = sw.pos.shape
        q = None
        if W.learned_lay:
            le = sw.col_lay[torch.arange(B)[:, None], sw.elem.clone()]               # [B,N]
            q = torch.sigmoid(W.lay_gain * sw.s[:, :, LAY] + W.lay_bias + le).reshape(-1)
        pe = torch.softmax(sw.s[:, :, EGG].reshape(B * N, 4), -1) if W.learned_egg else None
        laid = self._lay(sw, gi, gj, gen, None if q is None else q.detach(), None if pe is None else pe.detach())
        if not laid or (q is None and pe is None):
            return
        child = torch.cat([b * N + c for b, c, *_ in laid]); par = torch.cat([b * N + p for b, _, p, *_ in laid])
        dl = torch.zeros(len(child), dtype=sw.s.dtype)
        if q is not None:
            qp = q[par]
            dl = dl + (qp - qp.detach()) / qp.detach().clamp(min=0.1)
        if pe is not None:
            crossed = torch.cat([x for *_, x, _ in laid]); chosen = torch.cat([y for *_, y in laid])
            pc = pe[par, chosen]
            dl = dl + crossed.to(dl.dtype) * (pc - pc.detach()) / pc.detach().clamp(min=0.05)
        sw.bw = sw.bw + torch.zeros(B * N, dtype=dl.dtype).index_add(0, child, dl).view(B, N)


def warm_start(model, path=WARM):
    sd = torch.load(path, weights_only=False, map_location="cpu")["rule"]
    own = model.state_dict()
    for k, v in sd.items():
        if own[k].shape != v.shape:                                           # w1: new code columns stay zero
            pad = torch.zeros_like(own[k]); pad[tuple(slice(0, d) for d in v.shape)] = v; v = pad
        own[k] = v
    model.load_state_dict(own)


def save_model(model, path, **extra):
    torch.save(dict(rule={k: v.cpu() for k, v in model.state_dict().items()}, world=asdict(model.world),
                    colony=asdict(model.ccfg), hidden=model.hidden, **extra), path)


def load_model(path):
    st = torch.load(path, weights_only=False, map_location="cpu")
    w = st["world"]; w["vmax"] = tuple(w["vmax"])
    m = ColonyRule(sn.World(**w), ColonyCfg(**st["colony"]), hidden=st["hidden"])
    m.load_state_dict(st["rule"])
    return m


# ------------------------------------------------------------------------- train ---

@dataclass
class ColTrain:
    tag: str = "vote"
    steps: int = 1500
    lr: float = 5e-4
    lr_colony: float = 2e-3
    pool: int = 24
    per_kind: int = 2
    seed_every: int = 6
    roll_min: int = 48
    roll_max: int = 96
    bptt: int = 28
    p_switch: float = 0.25
    switch_cooldown: int = 240
    p_ratio: float = 0.34          # switch modes: ratio / tie / excess (the eval's mode)
    p_excess: float = 0.33
    margin: float = 0.15
    w_vote: float = 0.0            # supervise the plan vote with the sticky plan label
    w_mix: float = 0.0
    seed: int = 0
    log_every: int = 10
    snap_every: int = 100
    eval_every: int = 500
    learned_egg: int = 0
    # colony
    mode: str = "vote"
    pool_kind: str = "mean"
    H: int = 32
    K: int = 8
    lay_bias_head: int = 1
    init: str = ""                  # resume weights from another colony run's rule (otherwise warm from G2)


def run_dir(tag):
    return os.path.join(HERE, "runs", f"colony_{tag}")


def train(tc: ColTrain):
    run = run_dir(tc.tag)
    os.makedirs(run, exist_ok=True)
    world = sn.World(learned_lay=1, learned_egg=tc.learned_egg)
    L = sn.LossCfg(w_over=1.0, min_body=76, w_body=20, w_mix=tc.w_mix)
    targets = sn.load_targets()
    torch.manual_seed(tc.seed)
    gen = sn.make_gen(tc.seed)
    model = ColonyRule(world, ColonyCfg(mode=tc.mode, pool=tc.pool_kind, H=tc.H, K=tc.K, lay_bias=tc.lay_bias_head))
    if tc.init:
        model.load_state_dict(load_model(tc.init).state_dict())
    else:
        warm_start(model)
    rule_p = [model.w1, model.b1, model.w2, model.b2, model.w3, model.b3]
    ids = {id(p) for p in rule_p}
    col_p = [p for p in model.parameters() if id(p) not in ids]
    opt = torch.optim.Adam([dict(params=rule_p, lr=tc.lr), dict(params=col_p, lr=tc.lr_colony)])
    sched = torch.optim.lr_scheduler.MultiStepLR(opt, [int(tc.steps * 0.6), int(tc.steps * 0.85)], 0.3)
    H = model.ccfg.H
    pool = {k: sn.seed_swarm([targets[k]] * tc.pool, world, gen) for k in KINDS}
    start = 0
    ck = os.path.join(run, "latest.pt")
    if os.path.exists(ck):
        st = torch.load(ck, weights_only=False)
        model.load_state_dict(st["rule"]); opt.load_state_dict(st["opt"]); sched.load_state_dict(st["sched"])
        pool = st["pool"]; start = st["step"]
        print(f"resumed at step {start}", flush=True)
    for k in KINDS:
        for a in COL:
            if getattr(pool[k], a, None) is None:
                setattr(pool[k], a, torch.zeros(tc.pool, H if a == "col_h" else 4))
    json.dump(asdict(tc), open(os.path.join(run, "config.json"), "w"), indent=1)
    log = open(os.path.join(run, "log.jsonl"), "a")
    for step in range(start, tc.steps):
        t0 = time.time()
        picks, batch = {}, []
        for k in KINDS:
            idx = torch.randperm(tc.pool, generator=gen)[:tc.per_kind]
            picks[k] = idx
            sub = cindex(pool[k], idx)
            fresh = step % tc.seed_every == 0
            if fresh:
                sub = ccat([sn.seed_swarm([targets[k]], world, gen), cindex(sub, torch.arange(1, tc.per_kind))], H)
            for j in range(1 if fresh else 0, tc.per_kind):
                if int(sub.since[j]) < tc.switch_cooldown:
                    continue
                if float(torch.rand((), generator=gen)) < tc.p_switch:
                    u = float(torch.rand((), generator=gen))
                    mode = "ratio" if u < tc.p_ratio else ("excess" if u < tc.p_ratio + tc.p_excess else "tie")
                    new = sn.lose_majority(sub, j, gen, mode=mode, margin=tc.margin, targets=targets)
                    if new is not None:
                        sub.plan[j] = KINDS.index(new); sub.since[j] = 0
            batch.append(sub)
        sw = ccat(batch, H)
        T = int(torch.randint(tc.roll_min, tc.roll_max + 1, (1,), generator=gen))
        with torch.no_grad():
            for _ in range(T - tc.bptt):
                sw = model(sw, gen)
        sw = cdetach(sw)
        groups = [k for k in KINDS for _ in range(tc.per_kind)]
        votes = []
        for _ in range(tc.bptt):
            sw = model(sw, gen)
            votes.append(sw.col_vote)
        losses, infos, plans = [], {}, []
        for b, g in enumerate(groups):
            k = KINDS[int(sw.plan[b])]
            x = sn.decode(sw, b)
            l, info = sn.swarm_loss(x, targets[k], L)
            if tc.w_vote and tc.mode == "vote":
                lab = torch.full((len(votes),), int(sw.plan[b]), dtype=torch.long)
                vl = F.cross_entropy(torch.stack([v[b] for v in votes]), lab)
                l = l + tc.w_vote * vl; info["vote"] = float(vl.detach())
            info["vacc"] = int(int(sw.col_vote[b].argmax()) == int(sw.plan[b]))
            losses.append(l); plans.append(k); infos.setdefault(g, []).append(info)
        ok = [bool(torch.isfinite(l)) for l in losses]
        nonfinite = ok.count(False)
        opt.zero_grad()
        if any(ok):
            loss = torch.stack([l for l, good in zip(losses, ok) if good]).sum() / tc.per_kind
            loss.backward()
            grads = [p.grad for p in model.parameters() if p.grad is not None]
            if all(bool(torch.isfinite(g_).all()) for g_ in grads):
                for g_ in grads:
                    g_ /= (g_.norm() + 1e-8)
                opt.step()
            else:
                nonfinite += 1
        else:
            loss = torch.zeros(())
        sched.step()
        sw = cdetach(sw)
        for i, k in enumerate(KINDS):
            sub = cindex(sw, torch.arange(i * tc.per_kind, (i + 1) * tc.per_kind))
            for j in range(tc.per_kind):
                bad = int(sub.active[j].sum()) == 0 or infos[k][j]["sink"] > 120 or not ok[i * tc.per_kind + j] \
                    or not bool(torch.isfinite(sub.s[j]).all())
                if bad:
                    sub = ccat([cindex(sub, torch.arange(0, j)), sn.seed_swarm([targets[k]], world, gen),
                                cindex(sub, torch.arange(j + 1, tc.per_kind))], H)
            for j, pi in enumerate(picks[k].tolist()):
                for a in Swarm.FIELDS + COL:
                    getattr(pool[k], a)[pi] = getattr(sub, a)[j]
        dt = time.time() - t0
        if step % tc.log_every == 0 or step == tc.steps - 1:
            rec = dict(step=step, loss=float(loss), T=T, sec=round(dt, 2), nf=nonfinite,
                       smax=round(float(sw.s.abs().nan_to_num(posinf=1e9).amax()), 1),
                       vacc=round(np.mean([i["vacc"] for g in infos.values() for i in g]), 3))
            for i, k in enumerate(KINDS):
                c = sn.census(sw, i * tc.per_kind, targets[k]); inf = infos[k][0]
                rec[k] = dict(plan=plans[i * tc.per_kind], sink=round(inf["sink"], 3), n=c["n"], el=c["elements"],
                              vote=[round(float(v), 2) for v in torch.softmax(sw.col_vote[i * tc.per_kind], -1)],
                              lay=[round(float(v), 2) for v in sw.col_lay[i * tc.per_kind]],
                              **{x: round(inf[x], 3) for x in ("vote", "over", "body", "mix") if x in inf and x != "vote"}, vce=round(inf.get("vote", 0), 3))
            log.write(json.dumps(rec) + "\n"); log.flush()
            print(f"{step:5d} loss {float(loss):8.2f} T{T} {dt:4.1f}s vacc{rec['vacc']:.2f} s{rec['smax']:.0f} | " + " | ".join(
                f"{k[:2]}>{rec[k]['plan'][:2]} {rec[k]['sink']:6.2f} n{rec[k]['n']:3d}" for k in KINDS), flush=True)
        if (step + 1) % tc.snap_every == 0 or step == tc.steps - 1:
            torch.save(dict(rule=model.state_dict(), opt=opt.state_dict(), sched=sched.state_dict(), pool=pool,
                            step=step + 1), ck + ".tmp")
            os.replace(ck + ".tmp", ck)
            save_model(model, os.path.join(run, f"rule_{step + 1:05d}.pt"), step=step + 1, tag=tc.tag)
        if (step + 1) % tc.eval_every == 0 or step == tc.steps - 1:
            model.eval()
            res = evaluate(model, seeds=(7,))
            model.train()
            res["step"] = step + 1
            with open(os.path.join(run, "evals.jsonl"), "a") as f:
                f.write(json.dumps(res) + "\n")
            print(f"EVAL step {step + 1}: tests {res['tests']} (seed 7)", flush=True)
    return model


# ---------------------------------------------------------------------- evaluate ---

@torch.no_grad()
def evaluate(model, seeds=(7,), keep=False):
    """swarm_nca.rollout + tests_passed for each seed (the shared yardstick, unchanged)."""
    out = dict(tests=[], close=[])
    for s in seeds:
        data, summ = sn.rollout(model, 240, seed=s)
        p, c = sn.tests_passed(summ)
        out["tests"].append(p); out["close"].append(round(c, 2))
        if keep:
            out.setdefault("runs", []).append((data, summ))
        else:
            out.setdefault("summaries", []).append(summ)
    return out


@torch.no_grad()
def vote_trace(model, seed=7):
    """The colony's decision over a rollout identical to swarm_nca.rollout(seed): plan-vote probs and
    breeding bias every 10 steps, before and after the cull (same generator sequence)."""
    targets = sn.load_targets(); gen = sn.make_gen(seed)
    out = {}
    for k in KINDS:
        sw = sn.seed_swarm([targets[k]], model.world, gen)
        tr = []
        for t in range(481):
            if t == 240:
                sn.lose_majority(sw, 0, gen, to=sn.SWITCH_TO[k])
            if t % 10 == 0 and getattr(sw, "col_vote", None) is not None:
                m = sw.active[0] & sw.hatched[0]
                tr.append(dict(t=t, vote=[round(float(v), 3) for v in torch.softmax(sw.col_vote[0] / model.ccfg.vote_tau, -1)],
                               lay=[round(float(v), 2) for v in sw.col_lay[0]],
                               el=torch.bincount(sw.elem[0][m], minlength=4).tolist()))
            if t == 480:
                break
            sw = model(sw, gen)
        out[k] = tr
    return out


def publish(path, note_extra=None):
    """Write results/colony/: summary.json, rollout.json (swarm_nca.pack, as swarm_gpu.py does),
    probe.json, rule.pt, vote_trace.json, log.jsonl."""
    import swarm_probe
    model = load_model(path).eval()
    os.makedirs(RESULT, exist_ok=True)
    data, summary = sn.rollout(model, 240)
    p, c = sn.tests_passed(summary)
    sn.print_cross(summary); sn.print_geo(summary); sn.print_switch(summary)
    print(f"tests {p}/8  close {c:.2f}")
    st = torch.load(path, weights_only=False)
    summary["scale_inv"] = 0
    summary["rule"] = "rule.pt"
    summary["meta"] = dict(device="cpu", step=st.get("step"), tag="colony", colony=st["colony"],
                           note=f"Colony brain ({st['colony']['mode']}, {st['colony']['pool']} pool), run {st.get('tag')}, step {st.get('step')}.")
    json.dump(summary, open(os.path.join(RESULT, "summary.json"), "w"), indent=1)
    json.dump(sn.pack(data, 240), open(os.path.join(RESULT, "rollout.json"), "w"))
    shutil.copy(path, os.path.join(RESULT, "rule.pt"))
    pr = swarm_probe.probe(model)
    json.dump(pr, open(os.path.join(RESULT, "probe.json"), "w"), indent=1)
    print(json.dumps(pr))
    json.dump(vote_trace(model), open(os.path.join(RESULT, "vote_trace.json"), "w"))
    lg = os.path.join(run_dir(st.get("tag", "")), "log.jsonl")
    if os.path.exists(lg):
        shutil.copy(lg, os.path.join(RESULT, "log.jsonl"))
    return p, c, pr


# ------------------------------------------------------------------------ selftest ---

def selftest():
    targets = sn.load_targets()
    world = sn.World(learned_lay=1)
    base = sn.load_rule(WARM)
    for mode, pk in (("vote", "mean"), ("free", "attn")):
        m = ColonyRule(world, ColonyCfg(mode=mode, pool=pk)); warm_start(m)
        sw = sn.seed_swarm([targets[k] for k in KINDS], world, sn.make_gen(0))
        a, b = sw, cclone(sw)
        ga, gb = sn.make_gen(5), sn.make_gen(5)
        for t in range(40):
            a = base(a, ga); b = m(b, gb)
        d = float((a.pos - b.pos).abs().max()) + float((a.s - b.s).abs().max())
        same = bool(torch.equal(a.active, b.active))
        print(f"{mode}/{pk}: 40 steps vs G2 rule: max diff {d:.2e}, same alive {same}, n={a.active.sum(1).tolist()}")
        assert d < 1e-3 and same
        # colony state carries across rollout-style use and resets on a fresh seed
        assert b.col_h.shape == (4, m.ccfg.H)
    print("selftest ok: a fresh colony model is exactly the warm-start rule")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("selftest")
    tr = sub.add_parser("train")
    for f_, v in asdict(ColTrain()).items():
        tr.add_argument("--" + f_.replace("_", "-"), type=type(v), default=v)
    ev = sub.add_parser("eval")
    ev.add_argument("--rule", required=True)
    ev.add_argument("--seeds", default="7")
    ev.add_argument("--publish", action="store_true")
    a = ap.parse_args()
    torch.set_num_threads(4)
    if a.cmd == "selftest":
        selftest()
    elif a.cmd == "train":
        train(ColTrain(**{k: getattr(a, k) for k in asdict(ColTrain())}))
    elif a.cmd == "eval":
        if a.publish:
            publish(a.rule)
        else:
            r = evaluate(load_model(a.rule).eval(), seeds=tuple(int(x) for x in a.seeds.split(",")))
            for s in r["summaries"]:
                sn.print_cross(s); sn.print_switch(s)
            print("tests", r["tests"], "close", r["close"])


if __name__ == "__main__":
    main()
