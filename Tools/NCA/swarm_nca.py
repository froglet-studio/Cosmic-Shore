"""Elemental swarm automaton - ONE learned rule, four body plans, chosen by the seed's element mix.

Every particle is a whole tadpole fauna (TadPoleFauna.prefab): a heart crystal, a spindle, a
prism. What a tadpole IS is fixed at birth and never learned:

  * ELEMENT (Charge 0 / Mass 1 / Space 2 / Time 3). A child is its parent's element, except for
    a rare designed mutation (`World.p_mut`). Whether a mutant lives is the rule's call, so a
    cross-element birth survives only where the body plan wants it (selection, not design).
  * DOMAIN (0 / 1 / 2). A child is ALWAYS its parent's domain: no domain ever breeds another.
  * IDENTITY. The prism the rule asks for is mapped into the element's range inside the model
    (`prism_h`), so a Space tadpole cannot grow a cube and only Charge can change state.

What the rule learns, per tadpole, from its neighbours alone: where to move, whether an egg
hatches, whether a hatched tadpole stays alive, which way it faces, its prism (half-extents
and, for Charge, plain / danger / shield), and its spindle (length and bend).

Perception is DOMAIN-NEUTRAL by construction. A tadpole never sees a domain's identity, only
"same domain as me" vs "other domain": neighbour means are taken separately over the two sets,
and the gradient of the same-domain indicator points toward the other domain's region. Element
identity IS seen (each element has its own job).

Designed physics (the world's rules, not learned):
  * COLLISION: hearts closer than r0 push apart.
  * BUDDING: a hatched tadpole with fewer than k_bud neighbours lays one egg (alpha 0) beside
    itself, away from its neighbours. The rule decides whether the egg hatches (alpha > 0.1).
  * DEATH: a hatched tadpole whose alpha falls below 0.1 dies and leaves a lime crystal. An egg
    that is no longer next to a living tadpole is simply gone (it never lived).
  * SPEED: each element has its own top speed per step; Time's is the highest.

The loss is the debiased Sinkhorn divergence between the hatched swarm (weighted by alpha) and
the target's units, on [position, element, domain, prism, tier, facing, spindle], minimised over
slot -> domain assignments (so a target asks for REGIONS of different domains, never a specific
domain), plus a count term and a survival barrier. One rule is scored on four seedings at once,
one per body plan, and the reported loss is the sum.

    python3 Tools/NCA/swarm_nca.py selftest
    python3 Tools/NCA/swarm_nca.py bench
    python3 Tools/NCA/swarm_nca.py train --run runs/swarm
    python3 Tools/NCA/swarm_nca.py rollout --run runs/swarm     # export results for the viewer
"""
from __future__ import annotations

import argparse
import itertools
import json
import math
import os
import sys
import time
from dataclasses import dataclass, asdict, field, replace

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
KINDS = ("mass", "space", "charge", "time")          # the four body plans
MAJOR = {"mass": 1, "space": 2, "charge": 0, "time": 3}
ELEMENTS = ("Charge", "Mass", "Space", "Time")
HMIN, HMAX = 0.25, 3.0                               # swarm_model.js clampH

torch.set_num_threads(int(os.environ.get("NCA_THREADS", 0)) or max(1, os.cpu_count() or 1))
DEVICE = torch.device("cpu")


def set_device(name="cpu"):
    """Run on `name` ("cpu", "cuda", "mps"): every tensor is created there (torch's default device)
    and every random generator lives there. numpy crossings copy back explicitly."""
    global DEVICE
    dev = torch.device(name)
    if dev.type == "cuda" and not torch.cuda.is_available():
        sys.exit(f"--device {name}: this torch has no CUDA (torch {torch.__version__})")
    DEVICE = dev
    torch.set_default_device(dev)
    return dev


def make_gen(seed):
    return torch.Generator(device=DEVICE).manual_seed(seed)


# ------------------------------------------------------------ element identity ---

def identity_np(elem, h, tier):
    """swarm_model.js identity(), after clampH. h = [long, wide, thin]."""
    h = np.clip(np.asarray(h, float), HMIN, HMAX)
    if elem == 0:
        o = np.clip(h, 0.3, 2.2)
    elif elem == 1:
        o = np.clip(h, 0.5, 1.8); o = np.minimum(o, o.min() * 1.6)
    elif elem == 2:
        L = np.clip(h[0], 0.9, 3.0); c = lambda x: np.clip(min(x, L / 4), 0.25, 0.42)
        o = np.array([L, c(h[1]), c(h[2])])
    else:
        L = np.clip(h[0], 0.45, 1.2); c = lambda x: np.clip(min(x, L / 1.5), 0.3, 0.6)
        o = np.array([L, c(h[1]), c(h[2])])
    return o, (tier if elem == 0 else 0)


def prism_h(raw, elem):
    """Differentiable identity: raw [n,3] -> half-extents inside each element's range."""
    s = torch.sigmoid(raw)
    out = torch.empty_like(s)
    # Charge: each axis 0.3 .. 2.2
    m = elem == 0
    out[m] = 0.3 + 1.9 * s[m]
    # Mass: each axis 0.5 .. 1.8, no axis above 1.6x the smallest (closer to 1,1,1)
    m = elem == 1
    o = 0.5 + 1.3 * s[m]
    out[m] = torch.minimum(o, 1.6 * o.min(1, keepdim=True).values)
    # Space: long 0.9 .. 3.0; cross 0.25 .. min(0.42, long/4) - thin at every size
    m = elem == 2
    L = 0.9 + 2.1 * s[m][:, :1]
    cmax = (L / 4).clamp(0.25, 0.42)
    out[m] = torch.cat([L, 0.25 + (cmax - 0.25) * s[m][:, 1:]], 1)
    # Time: long 0.45 .. 1.2; cross 0.3 .. min(0.6, long/1.5)
    m = elem == 3
    L = 0.45 + 0.75 * s[m][:, :1]
    cmax = (L / 1.5).clamp(0.3, 0.6)
    out[m] = torch.cat([L, 0.3 + (cmax - 0.3) * s[m][:, 1:]], 1)
    return out


# ------------------------------------------------------------------ targets ---

@dataclass
class Target:
    kind: str
    name: str
    elem_major: int
    frames: list            # per frame: dict of tensors p [M,3], elem [M], slot [M], tier [M], h [M,3], f [M,3], sp [M,2]
    n: int
    slots: int
    mix: list               # element counts
    slot_mix: list          # slot counts
    speed: list             # per element mean heart displacement per frame


def load_targets(path=None):
    path = path or os.path.join(HERE, "results", "swarm_targets")
    out = {}
    for k in KINDS:
        d = json.load(open(os.path.join(path, k + ".json")))
        g = np.array(d["grid"], float) / 2
        frames = []
        for fr in d["frames"]:
            U = fr["units"]
            hs, tiers = [], []
            for u in U:
                h, t = identity_np(u["elem"], u["prism"]["h"], u["prism"].get("tier", 0))
                hs.append(h); tiers.append(t)
            frames.append(dict(
                p=torch.tensor(np.array([u["p"] for u in U]) - g, dtype=torch.float32),
                elem=torch.tensor([u["elem"] for u in U]),
                slot=torch.tensor([u["prism"]["slot"] for u in U]),
                tier=torch.tensor(tiers),
                h=torch.tensor(np.array(hs), dtype=torch.float32),
                f=torch.tensor([u["f"] for u in U], dtype=torch.float32),
                sp=torch.tensor([[u["spindle"]["len"], u["spindle"]["bend"]] for u in U], dtype=torch.float32)))
        r = d["report"]
        out[k] = Target(k, d["name"], d["elem"], frames, r["units"], int(max(r["slots"]) > 0) + sum(1 for c in r["slots"] if c > 0) - 1,
                        r["elements"], r["slots"], r["speed"])
        out[k].slots = sum(1 for c in r["slots"] if c > 0)
    return out


# -------------------------------------------------------------------- world ---

@dataclass
class World:
    R: float = 8.0            # perception radius (voxels; a heart is ~2 across)
    r0: float = 2.4           # collision spacing (target nearest neighbours: 2.5 - 3.5)
    rep: float = 0.4          # collision strength
    vmax: tuple = (0.8, 0.8, 0.8, 2.0)   # per-element top speed per step (Time zips)
    r_bud: float = 2.6        # an egg is laid this far from its parent
    k_bud: int = 7            # lay while fewer neighbours than this within r_lay
    r_lay: float = 4.0        # crowding radius for laying (a sheet at target spacing has ~6)
    p_bud: float = 0.2        # per-step laying probability for an eligible parent
    p_mut: float = 0.005      # an egg is another element (domain never changes)
    egg_life: int = 6         # an egg that has not hatched after this many steps is gone (it never lived)
    rho0: float = 8.0         # crowding normaliser
    capacity: int = 280       # tadpole slots per sample (largest plan 192)
    seed_n: int = 16          # tadpoles in a seed
    membrane: float = 80.0    # containment sphere (the cell membrane that pens fauna in the game)
    learned_lay: int = 0      # 1: the rule gates its own laying (channel LAY); the gate gets a gradient
    lay_gain: float = 4.0     # gate q = sigmoid(lay_gain * s[LAY] + lay_bias); at s = 0 the gate is ~0.95
    lay_bias: float = 3.0
    learned_egg: int = 0      # 1: a parent may CHOOSE its egg's element (channels EGG, softmax); the choice gets a gradient
    p_cross: float = 0.1      # learned_egg: share of eggs whose element the parent chooses (the rest breed true);
                              # "a bit of reproduction of different elements here and there". Domain always breeds true.


C = 32                        # channels: 0 hatch | 1-3 facing | 4-6 prism | 7-9 tier | 10-11 spindle | 12-30 hidden | 31 death
A, FAC, PR, TI, SP, DIE = 0, slice(1, 4), slice(4, 7), slice(7, 10), slice(10, 12), 31
LAY = 30                      # (learned_lay only) the rule's laying gate; otherwise a hidden channel
EGG = slice(26, 30)           # (learned_egg only) a parent's preference over its egg's element; otherwise hidden
S_MAX = 1e3                   # state guard: never reached by a healthy rule; stops an overflow turning into NaN
DIE_AT = 1.0                  # a hatched tadpole whose death channel passes this dies (and leaves a crystal)


class Swarm:
    """pos [B,N,3], s [B,N,C], elem/dom [B,N] long, active/hatched [B,N] bool, deaths [B] long."""

    FIELDS = ("pos", "s", "elem", "dom", "active", "hatched", "deaths", "mutants", "age", "clock", "plan", "since", "bw")

    def __init__(self, pos, s, elem, dom, active, hatched, deaths, mutants=None, age=None, clock=None,
                 plan=None, since=None, bw=None):
        self.pos, self.s, self.elem, self.dom = pos, s, elem, dom
        self.active, self.hatched, self.deaths = active, hatched, deaths
        self.mutants = mutants if mutants is not None else torch.zeros_like(deaths)
        self.age = age if age is not None else torch.zeros_like(elem)   # steps an egg has waited
        self.clock = clock if clock is not None else torch.zeros_like(deaths)   # steps since the seed
        self.plan = plan if plan is not None else torch.zeros_like(deaths)     # the plan it is scored against (sticky)
        self.since = since if since is not None else torch.zeros_like(deaths)  # steps since that plan was set
        self.bw = bw if bw is not None else torch.zeros(pos.shape[:2], dtype=pos.dtype)  # laying-gate gradient carrier (value 0)

    def detach(self):
        return Swarm(self.pos.detach(), self.s.detach(), self.elem, self.dom, self.active, self.hatched,
                     self.deaths, self.mutants, self.age, self.clock, self.plan, self.since, self.bw.detach())

    def backfill(self, kind):
        """A pool pickled before plan/since/bw existed."""
        B, N = self.pos.shape[:2]
        if not hasattr(self, "plan"):
            self.plan = torch.full((B,), KINDS.index(kind), dtype=torch.long)
        if not hasattr(self, "since"):
            self.since = torch.full((B,), 10 ** 6, dtype=torch.long)
        if not hasattr(self, "bw"):
            self.bw = torch.zeros(B, N, dtype=self.pos.dtype)
        return self

    def clone(self):
        return Swarm(*[getattr(self, a).clone() for a in Swarm.FIELDS])

    def index(self, idx):
        return Swarm(*[getattr(self, a)[idx] for a in Swarm.FIELDS])

    @staticmethod
    def cat(xs):
        return Swarm(*[torch.cat([getattr(x, a) for x in xs]) for a in Swarm.FIELDS])

    @property
    def B(self):
        return self.pos.shape[0]


def largest_remainder(weights, n, need=None):
    """Integer split of n by weights; every index in `need` gets at least 1."""
    w = np.asarray(weights, float); w = w / w.sum()
    base = np.floor(w * n).astype(int); rem = w * n - base
    for i in (need or []):
        if base[i] == 0:
            base[i] = 1
    while base.sum() < n:
        i = int(np.argmax(rem)); base[i] += 1; rem[i] = -1
    while base.sum() > n:
        i = int(np.argmax(base)); base[i] -= 1
    return base


def seed_mix(t: Target, n):
    """Element and domain counts of a seed: the target's ratios, every element it uses present."""
    em = largest_remainder(t.mix, n, need=[e for e in range(4) if t.mix[e] > 0])
    dm = largest_remainder([c for c in t.slot_mix if c > 0], n, need=list(range(t.slots)))
    return em, dm


def seed_swarm(targets_for_batch, world: World, gen=None):
    """A small clump per sample: seed_n hatched tadpoles at the target's element and domain mix."""
    B, N = len(targets_for_batch), world.capacity
    pos = torch.zeros(B, N, 3); s = torch.zeros(B, N, C)
    elem = torch.zeros(B, N, dtype=torch.long); dom = torch.zeros(B, N, dtype=torch.long)
    active = torch.zeros(B, N, dtype=torch.bool)
    n = world.seed_n
    for b, t in enumerate(targets_for_batch):
        em, dm = seed_mix(t, n)
        e = torch.tensor(np.repeat(np.arange(4), em)); d = torch.tensor(np.repeat(np.arange(len(dm)), dm))
        e = e[torch.randperm(n, generator=gen)]; d = d[torch.randperm(n, generator=gen)]
        elem[b, :n], dom[b, :n] = e, d
        pos[b, :n] = torch.randn(n, 3, generator=gen) * 2.0
        s[b, :n, A] = 1.0
        active[b, :n] = True
    plan = torch.tensor([KINDS.index(t.kind) for t in targets_for_batch], dtype=torch.long)
    return Swarm(pos, s, elem, dom, active, active.clone(), torch.zeros(B, dtype=torch.long), plan=plan)


# --------------------------------------------------------------------- rule ---

def edges(sw: Swarm, R):
    with torch.no_grad():
        B, N, _ = sw.pos.shape
        d = torch.cdist(sw.pos, sw.pos)
        m = (d < R) & sw.active[:, :, None] & sw.active[:, None, :]
        m &= ~torch.eye(N, dtype=torch.bool)[None]
        b, i, j = m.nonzero(as_tuple=True)
        return b * N + i, b * N + j


class SwarmRule(nn.Module):
    X = C + 5                                         # what a neighbour shows: state + element + hatched

    def __init__(self, world: World, hidden=192, fire_rate=0.5):
        super().__init__()
        self.world, self.hidden, self.fire_rate = world, hidden, fire_rate
        X = self.X
        # own [X] | same-domain mean [X] | other-domain mean [X] | gradient [X*3] | same-dom gradient [3] | rho, rho_same
        # + population signals (the game's Cell tracks its fauna population): headcount/100, element mix [4]
        self.G = 5
        self.F = X * 3 + X * 3 + 3 + 2 + self.G
        self.w1 = nn.Parameter(torch.empty(hidden, self.F)); nn.init.xavier_uniform_(self.w1)
        self.b1 = nn.Parameter(torch.zeros(hidden))
        self.w2 = nn.Parameter(torch.empty(hidden, hidden)); nn.init.xavier_uniform_(self.w2, gain=0.5)
        self.b2 = nn.Parameter(torch.zeros(hidden))
        self.w3 = nn.Parameter(torch.zeros(C + 3, hidden))   # zero init: identity, no motion
        self.b3 = nn.Parameter(torch.zeros(C + 3))
        with torch.no_grad():
            self.b3[A] = 0.04                          # a small hatching prior: eggs hatch in a few steps

    def perceive(self, pos, x, dom, gi, gj, n):
        R = self.world.R
        dx = pos[gj] - pos[gi]
        q = (1 - (dx * dx).sum(-1) / (R * R)).clamp(min=0)
        w, g = q ** 3, q ** 2
        same = (dom[gi] == dom[gj]).to(pos.dtype)
        X = self.X
        ws, wo = w * same, w * (1 - same)
        rs = torch.zeros(n).index_add(0, gi, ws); ro = torch.zeros(n).index_add(0, gi, wo)
        ms = torch.zeros(n, X).index_add(0, gi, ws[:, None] * x[gj]) / (1 + rs)[:, None]
        mo = torch.zeros(n, X).index_add(0, gi, wo[:, None] * x[gj]) / (1 + ro)[:, None]
        u = dx / R
        gs = torch.zeros(n).index_add(0, gi, g)
        grad = torch.zeros(n, 3, X).index_add(0, gi, (g[:, None, None] * u[:, :, None]) * (x[gj] - x[gi])[:, None, :])
        grad = grad / (1 + gs)[:, None, None]
        gsame = torch.zeros(n, 3).index_add(0, gi, g[:, None] * u * (same - 1)[:, None]) / (1 + gs)[:, None]
        rho = (rs + ro) / self.world.rho0
        return torch.cat([x, ms, mo, grad.reshape(n, 3 * X), gsame, rho[:, None], (rs / self.world.rho0)[:, None]], 1)

    def mlp(self, f):
        h = torch.relu(F.linear(f, self.w1, self.b1))
        h = torch.relu(F.linear(h, self.w2, self.b2)) + h
        return F.linear(h, self.w3, self.b3)

    NAN_DUMP = os.environ.get("NCA_NAN_DUMP", "")   # a path: on the first finite -> non-finite step, save it there

    def forward(self, sw: Swarm, gen=None, bud=True, fire=None):
        if not SwarmRule.NAN_DUMP:
            return self._step(sw, gen, bud, fire)
        finite = lambda x: bool(torch.isfinite(x.s).all()) and bool(torch.isfinite(x.pos).all())
        ok_in = finite(sw)
        gstate = gen.get_state().clone() if gen is not None else None
        before = sw.clone() if ok_in else None
        out = self._step(sw, gen, bud, fire)
        if ok_in and not finite(out):
            bad = (~torch.isfinite(out.s)).any(-1) | (~torch.isfinite(out.pos)).any(-1)
            torch.save(dict(sw={a: getattr(before, a).detach().cpu() for a in Swarm.FIELDS}, gen=gstate,
                            rule={k: v.detach().cpu() for k, v in self.state_dict().items()}, world=asdict(self.world),
                            hidden=self.hidden, bud=bud, bad=bad.nonzero().tolist()), SwarmRule.NAN_DUMP)
            print(f"NAN DUMP: a finite swarm went non-finite in one step ({int(bad.sum())} particles); saved {SwarmRule.NAN_DUMP}", flush=True)
            SwarmRule.NAN_DUMP = ""
        return out

    def _step(self, sw: Swarm, gen=None, bud=True, fire=None):
        W = self.world
        B, N, _ = sw.pos.shape
        n = B * N
        pos, s = sw.pos.reshape(n, 3), sw.s.reshape(n, C)
        elem, dom, act = sw.elem.reshape(n), sw.dom.reshape(n), sw.active.reshape(n)
        hatched = sw.hatched.reshape(n)
        gi, gj = edges(sw, W.R)
        x = torch.cat([s, F.one_hot(elem, 4).to(s.dtype), hatched[:, None].to(s.dtype)], 1)
        fire = act & ((torch.rand(n, generator=gen) <= self.fire_rate) if fire is None else fire.reshape(n))
        idx = fire.nonzero().squeeze(1)
        e = fire[gi]
        with torch.no_grad():
            hb = (sw.hatched & sw.active).float()                                  # [B, N]
            cnt = hb.sum(1).clamp(min=1)
            mixe = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1) / cnt[:, None]
            glob = torch.cat([(cnt / 100)[:, None], mixe], 1)                     # [B, 5]
            glob = glob[:, None, :].expand(B, N, self.G).reshape(n, self.G)
        feats = torch.cat([self.perceive(pos, x, dom, gi[e], gj[e], n), glob], 1).index_select(0, idx)
        out = self.mlp(feats)
        ds = torch.zeros(n, C).index_copy(0, idx, out[:, :C])
        vmax = torch.tensor(W.vmax)[elem].index_select(0, idx)[:, None]
        v = torch.zeros(n, 3).index_copy(0, idx, vmax * torch.tanh(out[:, C:]))
        s = (s + ds).clamp(-S_MAX, S_MAX)
        pos = pos + v
        # collision
        dxc = pos[gj] - pos[gi]
        r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - r).clamp(min=0) / W.r0
        pos = pos + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = pos.norm(dim=-1, keepdim=True).clamp(min=1e-6)            # membrane: pushed back inside
        pos = pos - 0.5 * (rad - W.membrane).clamp(min=0) * pos / rad
        # hatch / death / egg loss (bookkeeping, no gradient)
        with torch.no_grad():
            # EGGS hatch when the rule raises channel A past 0.1. HATCHED tadpoles live until their own
            # death channel passes DIE_AT: dying is its own decision, never a side effect of dimming.
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
        if bud:
            self.lay(out_sw, gi, gj, gen)
        return out_sw

    def lay(self, sw: Swarm, gi, gj, gen=None):
        """Designed reproduction: a hatched tadpole short of neighbours lays one egg of its own
        domain (and, but for a rare mutation, its own element) into a free slot.

        learned_lay: the rule also gates its own laying, q = sigmoid(gain * s[LAY] + bias), and the
        coin is p_bud * q. The coin has no gradient, so each child carries bw = dlog(q_parent) with
        value 0 (straight-through score function): whatever the loss thinks of that child flows back
        into the parent's decision to lay it."""
        W = self.world
        B, N, _ = sw.pos.shape
        q = torch.sigmoid(W.lay_gain * sw.s[:, :, LAY].reshape(-1) + W.lay_bias) if W.learned_lay else None
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
            # score function of the CHOSEN element, for the eggs whose element the parent chose
            crossed = torch.cat([x for *_, x, _ in laid]); chosen = torch.cat([y for *_, y in laid])
            pc = pe[par, chosen]
            dl = dl + crossed.to(dl.dtype) * (pc - pc.detach()) / pc.detach().clamp(min=0.05)
        sw.bw = sw.bw + torch.zeros(B * N, dtype=dl.dtype).index_add(0, child, dl).view(B, N)

    @torch.no_grad()
    def _lay(self, sw: Swarm, gi, gj, gen, q, pe=None):
        W = self.world
        B, N, _ = sw.pos.shape
        laid = []
        pos = sw.pos.reshape(B * N, 3)
        dxl = pos[gj] - pos[gi]
        close = ((dxl * dxl).sum(-1) < W.r_lay ** 2).float()
        cnt = torch.zeros(B * N).index_add(0, gi, close)
        cen = torch.zeros(B * N, 3).index_add(0, gi, dxl * close[:, None])
        ok = (sw.hatched.reshape(-1) & sw.active.reshape(-1) & (cnt < W.k_bud)
              & (torch.rand(B * N, generator=gen) <= (W.p_bud if q is None else W.p_bud * q))).view(B, N)
        for b in range(B):
            parents = ok[b].nonzero().squeeze(1)
            free = (~sw.active[b]).nonzero().squeeze(1)
            k = min(len(parents), len(free))
            if k == 0:
                continue
            parents = parents[torch.randperm(len(parents), generator=gen)[:k]]
            slots = free[:k]
            away = -cen.view(B, N, 3)[b, parents]
            away = away / away.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            noise = torch.randn(k, 3, generator=gen)
            dirn = away + 0.6 * noise / noise.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            dirn = dirn / dirn.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            sw.pos[b, slots] = sw.pos[b, parents].detach() + W.r_bud * dirn
            sw.s[b, slots] = 0.0
            e = sw.elem[b, parents].clone()
            crossed = torch.zeros(k, dtype=torch.bool); chosen = e.clone()
            if pe is not None:
                crossed = torch.rand(k, generator=gen) < W.p_cross
                chosen = torch.multinomial(pe[b * N + parents], 1, generator=gen).squeeze(1)
                e = torch.where(crossed, chosen, e)
            mut = torch.rand(k, generator=gen) < W.p_mut
            if mut.any():
                shift = torch.randint(1, 4, (int(mut.sum()),), generator=gen)
                e[mut] = (e[mut] + shift) % 4
                sw.mutants[b] += int(mut.sum())
            sw.elem[b, slots] = e
            sw.dom[b, slots] = sw.dom[b, parents]          # domain ALWAYS breeds true
            sw.active[b, slots] = True
            sw.hatched[b, slots] = False
            sw.age[b, slots] = 0
            laid.append((b, slots, parents, crossed, chosen))
        return laid


# --------------------------------------------------------------- decoding ---

def decode(sw: Swarm, b):
    """Sample b's live tadpoles as the loss/renderer sees them (alpha-weighted)."""
    m = sw.active[b]
    s, e = sw.s[b][m], sw.elem[b][m]
    # A tadpole is alive or not: it counts as exactly 1 once alpha > 0.1 (forward), with the gradient
    # of a steep sigmoid at the threshold (straight-through). A soft alpha-weighted count let the rule
    # keep every tadpole barely visible and pass for a swarm a quarter its real size.
    hat = sw.hatched[b][m]
    egg = torch.sigmoid(20 * (s[:, A] - 0.1))            # an egg's pull toward hatching
    pdie = torch.sigmoid(4 * (s[:, DIE] - DIE_AT))       # a tadpole's pull toward dying
    w = torch.where(hat, 1 - pdie + pdie.detach(), egg - egg.detach()) + sw.bw[b][m]
    raw = s[:, FAC]
    f = raw / torch.sqrt((raw * raw).sum(-1, keepdim=True) + 0.04)
    h = prism_h(s[:, PR], e)
    tl = torch.softmax(s[:, TI], -1)
    tier = torch.where((e == 0)[:, None], tl, torch.tensor([1.0, 0.0, 0.0]).expand_as(tl))
    sp = torch.stack([0.6 * torch.sigmoid(s[:, SP.start]), 0.5 * torch.tanh(s[:, SP.start + 1])], 1)
    return dict(p=sw.pos[b][m], w=w, alpha=s[:, A], s=s, pdie=pdie, elem=e, dom=sw.dom[b][m], h=h, tier=tier, f=f, sp=sp,
                hatched=sw.hatched[b][m], idx=m.nonzero().squeeze(1))


# --------------------------------------------------------------------- loss ---

@dataclass
class LossCfg:
    pos_scale: float = 2.0     # voxels: a cost of 1 is a heart 2 voxels off
    w_elem: float = 60.0       # element mismatch (elements are immutable: this decides who hatches)
    w_dom: float = 25.0        # domain region mismatch (under the best slot->domain assignment)
    w_h: float = 3.0
    w_tier: float = 6.0
    w_face: float = 1.5
    w_sp: float = 1.0
    eps: float = 0.05
    w_count: float = 0.0       # headcount is NOT a goal: the plan is the element ratios + shape (opt back in > 0)
    w_survive: float = 60.0     # a death must cost more than the count gains from it: restraint belongs at hatching
    rel_elem: int = 0          # 1: the target's element marginal is reweighted to the swarm's own mix, so the
                               # divergence measures GEOMETRY given the composition (w_mix scores the mix itself)
    w_mix: float = 0.0         # squared error of the swarm's element shares against the plan's
    w_con: float = 0.0         # contrastive hinge: the own plan must beat every other plan by con_margin
    con_margin: float = 4.0
    min_body: float = 0.0      # one-sided body floor: w_body * relu(1 - n / min_body)^2, the SAME for every plan
    w_body: float = 0.0        # (rules out the shrink-to-nothing shortcut without letting headcount pick a plan)
    w_over: float = 0.0        # overflow penalty: mean over live particles of sum(relu(|s| - over_band)); the
    over_band: float = 5.0     # state is otherwise unbounded and runs away to overflow (the NaN hangs)
    scale_inv: int = 0         # 1: a body plan is a shape, not a size - the swarm is rescaled to the plan's RMS
                               # radius before matching (headcount sets the scale; collision fixes the spacing)


def _plan_rms(T):
    if not hasattr(T, "_rms"):
        p = T.frames[0]["p"]
        T._rms = float(((p - p.mean(0)) ** 2).sum(-1).mean().sqrt())
    return T._rms


def _rescaled(x, a, T):
    """scale_inv: centre the swarm (weights a) and scale its RMS radius to the plan's. Uniform scale
    only - a stretched or squashed swarm still differs from the plan."""
    c = (a[:, None] * x["p"]).sum(0)
    d = x["p"] - c
    r = (a * (d * d).sum(-1)).sum().clamp(min=1e-6).sqrt()
    x2 = dict(x); x2["p"] = d * (_plan_rms(T) / r)
    return x2


def _lkey(L):
    return (L.pos_scale, L.w_elem, L.w_dom, L.w_h, L.w_tier, L.w_face, L.w_sp, L.eps)


def _centre(x):
    """Translation-invariant: a body plan is a shape, not a place. Weighted centroid at the origin."""
    w = x["w"] if "w" in x else torch.ones(len(x["p"]))
    c = (w[:, None] * x["p"]).sum(0) / w.sum().clamp(min=1e-6)
    return x["p"] - c


def _poscost(d2):
    """Huber on squared distance in units of pos_scale: quadratic to 4 scales out, then linear, so a
    stray tadpole far away pulls, but does not dominate the transport plan."""
    return torch.where(d2 < 16, d2, 8 * torch.sqrt(d2.clamp(min=1e-9)) - 16)


def cost_matrix(x, t, perm, L: LossCfg):
    if "pc" not in t:
        t["pc"] = t["p"] - t["p"].mean(0)
    dp = _poscost(((_centre(x)[:, None] - t["pc"][None]) ** 2).sum(-1) / (2 * L.pos_scale ** 2))
    ce = L.w_elem * (x["elem"][:, None] != t["elem"][None]).float()
    tdom = perm[t["slot"]]
    cd = L.w_dom * (x["dom"][:, None] != tdom[None]).float()
    ch = L.w_h * ((x["h"][:, None] - t["h"][None]) ** 2).sum(-1)
    tt = F.one_hot(t["tier"], 3).float()
    ct = L.w_tier * ((x["tier"][:, None] - tt[None]) ** 2).sum(-1)
    cf = L.w_face * (1 - (x["f"][:, None] * t["f"][None]).sum(-1))
    cs = L.w_sp * ((x["sp"][:, None] - t["sp"][None]) ** 2).sum(-1)
    return dp + ce + cd + ch + ct + cf + cs


def self_cost(x, L: LossCfg):
    dp = _poscost(((x["p"][:, None] - x["p"][None]) ** 2).sum(-1) / (2 * L.pos_scale ** 2))
    ce = L.w_elem * (x["elem"][:, None] != x["elem"][None]).float()
    cd = L.w_dom * (x["dom"][:, None] != x["dom"][None]).float()
    ch = L.w_h * ((x["h"][:, None] - x["h"][None]) ** 2).sum(-1)
    ct = L.w_tier * ((x["tier"][:, None] - x["tier"][None]) ** 2).sum(-1)
    cf = L.w_face * (1 - (x["f"][:, None] * x["f"][None]).sum(-1))
    cs = L.w_sp * ((x["sp"][:, None] - x["sp"][None]) ** 2).sum(-1)
    return dp + ce + cd + ch + ct + cf + cs


def _lse(x, dim):
    return torch.logsumexp(x, dim)


def sinkhorn_ot(Cm, a, b, eps, iters_per=3):
    """Entropic OT value with eps-scaling. Potentials are found without gradient; the value is one
    differentiable soft-c-transform at the end (envelope theorem), as in geomloss."""
    loga, logb = torch.log(a.clamp(min=1e-12)), torch.log(b.clamp(min=1e-12))
    with torch.no_grad():
        # a non-finite cost would make the eps-halving loop below run forever (NaN never reaches eps)
        Cd = torch.nan_to_num(Cm.detach(), nan=1e6, posinf=1e6, neginf=-1e6); la, lb = loga.detach(), logb.detach()
        f = torch.zeros(Cd.shape[0]); g = torch.zeros(Cd.shape[1])
        e = max(float(Cd.max()), eps)
        for _guard in range(200):
            for _ in range(iters_per):
                f = -e * _lse(lb[None] + (g[None] - Cd) / e, 1)
                g = -e * _lse(la[:, None] + (f[:, None] - Cd) / e, 0)
            if e <= eps:
                break
            e = max(e * 0.5, eps)
    fd = -eps * _lse(logb[None] + (g[None] - Cm) / eps, 1)
    gd = -eps * _lse(loga[:, None] + (f[:, None] - Cm) / eps, 0)
    return (a * fd).sum() + (b * gd).sum()


PERMS = {1: [(0,)], 2: list(itertools.permutations(range(2))), 3: list(itertools.permutations(range(3)))}


def _target_self_cost(t, L):
    key = ("_selfC",) + _lkey(L)
    if key not in t:
        x = dict(p=t["p"], elem=t["elem"], dom=t["slot"], h=t["h"], tier=F.one_hot(t["tier"], 3).float(),
                 f=t["f"], sp=t["sp"])
        t[key] = self_cost(x, L)
    return t[key]


def target_self_ot(t, L):
    key = ("_self",) + _lkey(L)                     # keyed by the cost weights (a geometry-only score differs)
    if key not in t:
        b = torch.full((len(t["p"]),), 1.0 / len(t["p"]))
        t[key] = float(sinkhorn_ot(_target_self_cost(t, L), b, b, L.eps))
    return t[key]


def target_self_ot_b(t, b, L):
    """The target's self transport under a reweighted marginal b (rel_elem); not cached."""
    return float(sinkhorn_ot(_target_self_cost(t, L), b, b, L.eps))


def _plan_marginal(t, xelem, a):
    """rel_elem: target weights whose element shares match the swarm's (detached) shares, over the
    elements the target has; a 10% uniform floor keeps every target unit in the transport."""
    te = t["elem"]
    with torch.no_grad():
        sx = torch.zeros(4).index_add(0, xelem, a.detach())
        cnt = torch.bincount(te, minlength=4).float()
        sx = sx * (cnt > 0).float()
        if float(sx.sum()) < 1e-6:
            return torch.full((len(te),), 1.0 / len(te))
        sx = sx / sx.sum()
        b = sx[te] / cnt[te].clamp(min=1)
        b = 0.9 * b / b.sum() + 0.1 / len(te)
    return b


def _divergence(x, a, oaa, T, L, frames=None, ndom=None):
    """Debiased Sinkhorn divergence to the best (frame, slot->domain assignment) of target T."""
    if L.scale_inv:
        x = _rescaled(x, a, T)
        oaa = sinkhorn_ot(self_cost(x, L), a, a, L.eps)
    frames = range(len(T.frames)) if frames is None else frames
    perms = PERMS[ndom or T.slots]
    best = None
    with torch.no_grad():
        for k in frames:
            t = T.frames[k]
            b = _plan_marginal(t, x["elem"], a) if L.rel_elem else torch.full((len(t["p"]),), 1.0 / len(t["p"]))
            for perm in perms:
                v = float(sinkhorn_ot(cost_matrix(x, t, torch.tensor(perm), L), a, b, max(L.eps, 0.5), iters_per=2))
                if best is None or v < best[0]:
                    best = (v, k, perm, b)
    _, k, perm, b = best
    t = T.frames[k]
    oab = sinkhorn_ot(cost_matrix(x, t, torch.tensor(perm), L), a, b, L.eps)
    tself = target_self_ot_b(t, b, L) if L.rel_elem else target_self_ot(t, L)
    return oab - 0.5 * oaa - 0.5 * tself, k, perm


def swarm_loss(x, T: Target, L: LossCfg, frames=None, ndom=None, others=None):
    """Sinkhorn divergence to the best (frame, slot->domain assignment), + count + survival
    (+ element-mix error, + a contrastive margin over the `others` plans)."""
    w = x["w"]
    tot = w.sum()
    if tot < 1e-3:
        z = (w * 0).sum() + 100.0
        return z, dict(sink=100.0, count=1.0, frame=0, perm=(0,), n=0.0)
    a = w / tot
    oaa = None if L.scale_inv else sinkhorn_ot(self_cost(x, L), a, a, L.eps)
    sink, k, perm = _divergence(x, a, oaa, T, L, frames, ndom)
    count = ((tot - T.n) / T.n) ** 2
    hat = x["hatched"].float()
    survive = (x["pdie"] * hat).sum() / T.n
    loss = sink + L.w_count * count + L.w_survive * survive
    info = dict(sink=float(sink.detach()), count=float(count.detach()), survive=float(survive.detach()), frame=k, perm=perm, n=float(tot))
    if L.w_body and L.min_body:
        body = torch.relu(1 - tot / L.min_body) ** 2
        loss = loss + L.w_body * body; info["body"] = float(body.detach())
    if L.w_over and "s" in x:
        over = torch.relu(x["s"].abs() - L.over_band).sum(-1).mean()
        loss = loss + L.w_over * over; info["over"] = float(over.detach())
    if L.w_mix:
        share = torch.zeros(4).index_add(0, x["elem"], a)
        tm = torch.tensor(T.mix, dtype=share.dtype); tm = tm / tm.sum()
        mix = ((share - tm) ** 2).sum()
        loss = loss + L.w_mix * mix; info["mix"] = float(mix.detach())
    if L.w_con and others:
        so = torch.stack([_divergence(x, a, oaa, T2, L)[0] for T2 in others]).min()
        con = torch.relu(L.con_margin + sink - so)
        loss = loss + L.w_con * con; info["con"] = float(con.detach()); info["gap"] = float((so - sink).detach())
    return loss, info


def speed_of(xa, xb, period):
    """Per-element mean heart displacement per FRAME between two checkpoints `period` steps apart,
    over tadpoles alive at both (slot identity). Returns [4] tensor and a [4] presence mask."""
    ia, ib = xa["idx"], xb["idx"]
    common, ca, cb = np.intersect1d(ia.cpu().numpy(), ib.cpu().numpy(), return_indices=True)
    v = torch.zeros(4); m = torch.zeros(4, dtype=torch.bool)
    if len(common) == 0:
        return v, m
    ca, cb = torch.as_tensor(ca, device=DEVICE), torch.as_tensor(cb, device=DEVICE)
    ok = (xa["hatched"][ca] & xb["hatched"][cb])
    d = (xb["p"][cb] - xa["p"][ca]).norm(dim=-1)
    e = xa["elem"][ca]
    out = []
    for el in range(4):
        sel = ok & (e == el)
        if sel.any():
            out.append(d[sel].mean()); m[el] = True
        else:
            out.append(torch.zeros(()))
    return torch.stack(out), m


def anim_loss(xs, T: Target, L: LossCfg, period, w_speed, k0=None):
    """xs: decoded checkpoints, `period` steps apart. Checkpoint j is matched to frame (k0 + j) % K:
    k0 given = the birth clock; None = phase-free (the best start, no gradient). The slot->domain
    assignment is one per rollout."""
    K = len(T.frames)
    xl = xs[-1]
    if xl["w"].sum() < 1e-3:
        z = (xl["w"] * 0).sum() + 100.0
        return z, dict(sink=100.0, count=1.0, frame=0, perm=(0,), n=0.0, speed=[0, 0, 0, 0])
    _, info = swarm_loss(xl, T, L)
    perm = torch.tensor(info["perm"])
    if k0 is None:
        with torch.no_grad():
            Cjk = np.zeros((len(xs), K))
            for j, x in enumerate(xs):
                tot = x["w"].sum()
                if tot < 1e-3:
                    Cjk[j] = 100.0
                    continue
                a = x["w"] / tot
                for k in range(K):
                    t = T.frames[k]
                    b = torch.full((len(t["p"]),), 1.0 / len(t["p"]))
                    Cjk[j, k] = float(sinkhorn_ot(cost_matrix(x, t, perm, L), a, b, 0.5, iters_per=2))
            k0 = int(np.argmin([sum(Cjk[j, (s + j) % K] for j in range(len(xs))) for s in range(K)]))
    sinks, counts, surv = [], [], []
    for j, x in enumerate(xs):
        t = T.frames[(k0 + j) % K]
        tot = x["w"].sum()
        if tot < 1e-3:
            continue
        a = x["w"] / tot
        b = torch.full((len(t["p"]),), 1.0 / len(t["p"]))
        oab = sinkhorn_ot(cost_matrix(x, t, perm, L), a, b, L.eps)
        oaa = sinkhorn_ot(self_cost(x, L), a, a, L.eps)
        sinks.append(oab - 0.5 * oaa - 0.5 * target_self_ot(t, L))
        counts.append(((tot - T.n) / T.n) ** 2)
        surv.append((x["pdie"] * x["hatched"].float()).sum() / T.n)
    sink = torch.stack(sinks).mean()
    count = torch.stack(counts).mean()
    survive = torch.stack(surv).mean()
    tgt = torch.tensor(T.speed, dtype=torch.float32)
    sp, ms = [], []
    for j in range(len(xs) - 1):
        v, m = speed_of(xs[j], xs[j + 1], period)
        sp.append(v); ms.append(m)
    v = torch.stack(sp).mean(0); m = torch.stack(ms).all(0) & (torch.tensor(T.mix) > 0)
    speed = (((v - tgt) / (tgt + 1.0)) ** 2 * m.float()).sum()
    loss = sink + L.w_count * count + L.w_survive * survive + w_speed * speed
    return loss, dict(sink=float(sink.detach()), count=float(count.detach()), survive=float(survive.detach()),
                      speed=[round(float(x), 2) for x in v], frame=k0, perm=tuple(perm.tolist()), n=float(xl["w"].sum().detach()))


# ---------------------------------------------------------------- metrics ---

@torch.no_grad()
def census(sw: Swarm, b, T: Target):
    m = sw.active[b] & sw.hatched[b]
    e = sw.elem[b][m]; d = sw.dom[b][m]
    el = torch.bincount(e, minlength=4).tolist()
    dl = torch.bincount(d, minlength=3).tolist()
    n = int(m.sum())
    return dict(n=n, target_n=T.n, elements=el, target_elements=T.mix,
                majority=ELEMENTS[int(np.argmax(el))] if n else "-", domains=dl,
                deaths=int(sw.deaths[b]), mutants=int(sw.mutants[b]))


PLAN_OF = {MAJOR[k]: k for k in KINDS}          # majority element -> the plan that uses most of it


@torch.no_grad()
def majority_plan(sw: Swarm, b, fallback):
    """The plan a sample is scored against: the one belonging to its living majority element."""
    m = sw.active[b] & sw.hatched[b]
    if int(m.sum()) == 0:
        return fallback
    c = torch.bincount(sw.elem[b][m], minlength=4)
    return PLAN_OF[int(c.argmax())]


@torch.no_grad()
def lose_majority(sw: Swarm, b, gen, keep_min=6, to=None, mode="excess", margin=0.0, targets=None):
    """In place: remove (as if eaten) enough of sample b's majority element that another element
    present becomes the majority. The swarm is then scored against the new majority's plan, which is
    the only way a rule can learn to SWITCH plans when its ratios change. Returns the new majority's
    plan, or None when no switch was possible.

    mode "excess": the old majority loses just enough (+ a random extra) to fall one behind.
    mode "tie": the same, but the new majority then leads by margin x the headcount (a clear switch,
                not a coin the rule can flip back by laying one more of the old element).
    mode "ratio": every element is culled toward the NEW plan's element mix (needs `targets`)."""
    m = sw.active[b] & sw.hatched[b]
    c = torch.bincount(sw.elem[b][m], minlength=4)
    maj = int(c.argmax())
    others = [e for e in range(4) if e != maj and int(c[e]) >= 2]
    if not others:
        return None
    if to is not None and to not in others:
        return None
    e2 = to if to is not None else others[int(torch.randint(len(others), (1,), generator=gen))]
    n = int(m.sum())
    if mode == "ratio":
        mix = torch.tensor(targets[PLAN_OF[e2]].mix, dtype=torch.float)
        frac = mix / mix.sum()
        ok = frac > 0
        sc = min(float(c[e]) / float(frac[e]) for e in range(4) if ok[e])
        keep = torch.tensor([min(int(c[e]), int(math.floor(sc * float(frac[e])))) if ok[e] else 0 for e in range(4)])
        if int(keep.argmax()) != e2 or int(keep.sum()) < keep_min:
            return None
        kills = c.cpu() - keep
    else:
        extra = int(math.ceil(margin * n)) if mode == "tie" else \
            int(torch.randint(0, max(1, int(c[e2]) // 3 + 1), (1,), generator=gen))
        excess = int(c[maj]) - int(c[e2]) + 1 + extra
        if n - excess < keep_min or excess <= 0 or excess > int(c[maj]):
            return None
        kills = torch.zeros(4, dtype=torch.long); kills[maj] = excess
    for e in range(4):
        k = int(kills[e])
        if k <= 0:
            continue
        idx = (m & (sw.elem[b] == e)).nonzero().squeeze(1)
        kill = idx[torch.randperm(len(idx), generator=gen)[:k]]
        sw.active[b, kill] = False; sw.hatched[b, kill] = False; sw.s[b, kill] = 0.0
    return majority_plan(sw, b, None)


# ------------------------------------------------------------------ train ---

@dataclass
class TrainCfg:
    run: str = os.path.join(HERE, "runs", "swarm")
    steps: int = 3000
    lr: float = 5e-4
    pool: int = 16                 # pool samples per body plan
    per_kind: int = 1              # samples per body plan per step (batch = 4 x per_kind)
    roll_min: int = 40
    roll_max: int = 64
    bptt: int = 28
    seed_every: int = 2            # replace one pool sample per plan with a fresh seed every k steps
    grow_steps: int = 90           # a fresh seed is grown this far (no grad) before entering the pool
    log_every: int = 10
    snap_every: int = 250
    seed: int = 0
    hidden: int = 192
    init: str = ""                 # warm start: a rule_*.pt (e.g. the static stage)
    anim: int = 0                  # 1: match the 8-frame loop, not just the body
    period: int = 8                # steps per target frame
    window: int = 8                # checkpoints per rollout (period apart, all backpropagated)
    birth_clock: int = 480         # samples younger than this follow the clock from birth; older ones are phase-free
    w_speed: float = 2.0
    replace_above: float = 120.0   # pool hygiene: a sample whose loss passed this goes back to a seed
    replace_count: float = 1e9     # ... or whose headcount is this far off its plan's (off: count is not a goal)
    p_switch: float = 0.15         # a pool sample loses enough of its majority element (eaten) that another
                                   # element takes the majority; from then on it is scored against THAT plan
    sticky_plan: int = 0           # 1: a sample keeps the plan its seed / last switch gave it (reverting to
                                   # the old plan by out-laying the new majority no longer changes the label)
    switch_cooldown: int = 0       # a sample switches again only after this many steps on its current plan
    p_ratio: float = 0.0           # share of switches that cull toward the new plan's element MIX (else "tie")
    margin: float = 0.0            # "tie" switches leave the new majority ahead by this fraction of the headcount
    # world / loss knobs (mirrored so a run's flags set them; see World and LossCfg)
    learned_lay: int = 0
    lay_gain: float = 4.0
    lay_bias: float = 3.0
    learned_egg: int = 0
    p_cross: float = 0.1
    rel_elem: int = 0
    w_mix: float = 0.0
    w_con: float = 0.0
    con_margin: float = 4.0
    scale_inv: int = 0
    w_over: float = 0.0
    over_band: float = 5.0
    min_body: float = 0.0
    w_body: float = 0.0


def make_seed_pool(rule, targets, cfg: TrainCfg, gen):
    """Fresh seeds per plan, pre-grown without gradient so the pool starts with bodies to shape."""
    world = rule.world
    pool = {}
    for k in KINDS:
        sw = seed_swarm([targets[k]] * cfg.pool, world, gen)
        pool[k] = sw
    return pool


def train(cfg: TrainCfg, world: World, L: LossCfg, resume=True, on_snapshot=None):
    os.makedirs(cfg.run, exist_ok=True)
    world = replace(world, learned_lay=cfg.learned_lay, lay_gain=cfg.lay_gain, lay_bias=cfg.lay_bias,
                    learned_egg=cfg.learned_egg, p_cross=cfg.p_cross)
    L = replace(L, rel_elem=cfg.rel_elem, w_mix=cfg.w_mix, w_con=cfg.w_con, con_margin=cfg.con_margin, scale_inv=cfg.scale_inv,
                w_over=cfg.w_over, over_band=cfg.over_band, min_body=cfg.min_body, w_body=cfg.w_body)
    targets = load_targets()
    torch.manual_seed(cfg.seed)
    gen = make_gen(cfg.seed)
    rule = SwarmRule(world, hidden=cfg.hidden)
    opt = torch.optim.Adam(rule.parameters(), lr=cfg.lr)
    sched = torch.optim.lr_scheduler.MultiStepLR(opt, [int(cfg.steps * 0.6), int(cfg.steps * 0.85)], 0.3)
    start = 0
    ck = os.path.join(cfg.run, "latest.pt")
    pool = make_seed_pool(rule, targets, cfg, gen)
    if cfg.init and not os.path.exists(ck):
        sd = {k_: v.to(DEVICE) for k_, v in torch.load(cfg.init, weights_only=False, map_location=DEVICE)["rule"].items()}
        for k_, v in rule.state_dict().items():               # new input columns enter at zero weight
            if sd[k_].shape != v.shape:
                pad = torch.zeros_like(v); pad[tuple(slice(0, d) for d in sd[k_].shape)] = sd[k_]; sd[k_] = pad
        rule.load_state_dict(sd)
        print(f"warm start from {cfg.init}")
    if resume and os.path.exists(ck):
        st = torch.load(ck, weights_only=False, map_location=DEVICE)
        rule.load_state_dict(st["rule"]); opt.load_state_dict(st["opt"]); sched.load_state_dict(st["sched"])
        pool = {k_: v.backfill(k_) for k_, v in st["pool"].items()}; start = st["step"]
        print(f"resumed at step {start}")
    json.dump(dict(train=asdict(cfg), world=asdict(world), loss=asdict(L)), open(os.path.join(cfg.run, "config.json"), "w"), indent=1)
    log = open(os.path.join(cfg.run, "log.jsonl"), "a")
    for step in range(start, cfg.steps):
        t0 = time.time()
        picks, batch = {}, []
        for k in KINDS:
            idx = torch.randperm(cfg.pool, generator=gen)[:cfg.per_kind]
            picks[k] = idx
            sub = pool[k].index(idx)
            if step % cfg.seed_every == 0:                  # one fresh seed in this plan's slot 0
                sub = Swarm.cat([seed_swarm([targets[k]], world, gen), sub.index(torch.arange(1, cfg.per_kind))]) \
                    if cfg.per_kind > 1 else seed_swarm([targets[k]], world, gen)
            for j in range(1 if step % cfg.seed_every == 0 else 0, cfg.per_kind):
                if int(sub.since[j]) < cfg.switch_cooldown:
                    continue
                if float(torch.rand((), generator=gen)) < cfg.p_switch:
                    mode = "ratio" if cfg.p_ratio > 0 and float(torch.rand((), generator=gen)) < cfg.p_ratio else ("tie" if cfg.sticky_plan else "excess")
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
        groups = [k for k in KINDS for _ in range(cfg.per_kind)]       # which seeding a sample came from
        nonfinite = 0
        for b in range(sw.B):                                # a sample whose state went non-finite is reseeded
            if not (bool(torch.isfinite(sw.s[b]).all()) and bool(torch.isfinite(sw.pos[b]).all())):
                nonfinite += 1
                sw = Swarm.cat([sw.index(torch.arange(0, b)), seed_swarm([targets[groups[b]]], world, gen),
                                sw.index(torch.arange(b + 1, sw.B))])

        def plan_of(sw_, b_, g_):
            return KINDS[int(sw_.plan[b_])] if cfg.sticky_plan else majority_plan(sw_, b_, g_)
        losses, infos, plans = [], {}, []
        if cfg.anim:
            cks = []
            clock0 = sw.clock.clone()
            for i in range(cfg.window * cfg.period):
                sw = rule(sw, gen)
                if (i + 1) % cfg.period == 0:
                    cks.append(sw)
            for b, g in enumerate(groups):
                k = plan_of(sw, b, g)
                xs = [decode(c, b) for c in cks]
                age = int(clock0[b]) + cfg.period          # the first checkpoint's age
                k0 = (age // cfg.period) % len(targets[k].frames) if age < cfg.birth_clock else None
                l, info = anim_loss(xs, targets[k], L, cfg.period, cfg.w_speed, k0)
                losses.append(l); plans.append(k)
                infos.setdefault(g, []).append(info)
        else:
            for _ in range(cfg.bptt):
                sw = rule(sw, gen)
            for b, g in enumerate(groups):
                k = plan_of(sw, b, g)                       # scored against ONE plan only (current/sticky)
                x = decode(sw, b)
                others = [targets[k2] for k2 in KINDS if k2 != k] if L.w_con else None
                l, info = swarm_loss(x, targets[k], L, others=others)
                losses.append(l); plans.append(k)
                infos.setdefault(g, []).append(info)
        ok = [bool(torch.isfinite(l)) for l in losses]
        for b, good in enumerate(ok):                       # a non-finite loss: dropped, and its sample reseeded
            if not good:
                nonfinite += 1; g_ = groups[b]; infos[g_][b - KINDS.index(g_) * cfg.per_kind]["sink"] = 1e9
        opt.zero_grad()
        if any(ok):
            loss = torch.stack([l for l, good in zip(losses, ok) if good]).sum() / cfg.per_kind
            loss.backward()
            grads = [p.grad for p in rule.parameters() if p.grad is not None]
            if all(bool(torch.isfinite(g_).all()) for g_ in grads):
                for g_ in grads:
                    g_ /= (g_.norm() + 1e-8)                  # per-tensor normalised gradient (as the NCA paper)
                opt.step()
            else:
                nonfinite += 1
        else:
            loss = torch.zeros(())
        sched.step()
        sw = sw.detach()
        for i, k in enumerate(KINDS):
            sub = sw.index(torch.arange(i * cfg.per_kind, (i + 1) * cfg.per_kind))
            # an extinct or blown-up sample is replaced by a seed, so the pool keeps learnable states
            for j in range(cfg.per_kind):
                tk = targets[plans[i * cfg.per_kind + j]]
                off = abs(infos[k][j]["n"] - tk.n) / tk.n
                if int(sub.active[j].sum()) == 0 or infos[k][j]["sink"] > cfg.replace_above or off > cfg.replace_count \
                        or not bool(torch.isfinite(sub.s[j]).all()):
                    sub = Swarm.cat([sub.index(torch.arange(0, j)), seed_swarm([targets[k]], world, gen),
                                     sub.index(torch.arange(j + 1, cfg.per_kind))])
            for j, pi in enumerate(picks[k].tolist()):
                for a in Swarm.FIELDS:
                    getattr(pool[k], a)[pi] = getattr(sub, a)[j]
        dt = time.time() - t0
        if step % cfg.log_every == 0 or step == cfg.steps - 1:
            rev = sum(majority_plan(sw, b, plans[b]) != plans[b] for b in range(sw.B)) / sw.B
            rec = dict(step=step, loss=float(loss), T=T, sec=round(dt, 2), lr=sched.get_last_lr()[0], rev=round(rev, 3),
                       nonfinite=nonfinite, smax=round(float(sw.s.abs().nan_to_num(posinf=1e9).amax()), 1))
            for i, k in enumerate(KINDS):
                c = census(sw, i * cfg.per_kind, targets[k])
                inf = infos[k][0]
                rec[k] = dict(plan=plans[i * cfg.per_kind], sink=round(inf["sink"], 3), count=round(inf.get("count", 0), 3), n=c["n"],
                              el=c["elements"], dom=c["domains"], deaths=c["deaths"], frame=inf["frame"],
                              speed=inf.get("speed"), **{x: round(inf[x], 3) for x in ("mix", "con", "gap", "survive", "over") if x in inf})
            log.write(json.dumps(rec) + "\n"); log.flush()
            print(f"{step:5d} loss {float(loss):8.3f} T{T} {dt:4.1f}s rev{rev:.2f}{f' nf{nonfinite}' if nonfinite else ''} s{rec['smax']:.0f} | " + " | ".join(
                f"{k[:2]}>{rec[k]['plan'][:2]} {rec[k]['sink']:6.2f} n{rec[k]['n']:3d} d{rec[k]['deaths']}" for k in KINDS), flush=True)
        if (step + 1) % cfg.snap_every == 0 or step == cfg.steps - 1:
            torch.save(dict(rule=rule.state_dict(), opt=opt.state_dict(), sched=sched.state_dict(), pool=pool,
                            step=step + 1), ck)
            torch.save(dict(rule={k_: v.cpu() for k_, v in rule.state_dict().items()}, world=asdict(world),
                            hidden=cfg.hidden, step=step + 1), os.path.join(cfg.run, f"rule_{step + 1:05d}.pt"))
            if on_snapshot:
                on_snapshot(step + 1, rule)
    return rule


# --------------------------------------------------------------- evaluate ---

def load_rule(path):
    st = torch.load(path, weights_only=False, map_location=DEVICE)
    w = st["world"]; w["vmax"] = tuple(w["vmax"])
    world = World(**w)
    rule = SwarmRule(world, hidden=st["hidden"])
    rule.load_state_dict(st["rule"])
    return rule


@torch.no_grad()
def evaluate(rule, steps=200, seeds=4, every=4, L=None):
    """Grow every plan from `seeds` fresh seeds; score each against EVERY target (so the table says
    whether the seed mix, not chance, picked the body). Returns a summary dict."""
    L = L or LossCfg()
    targets = load_targets()
    gen = make_gen(1234)
    res = {}
    for k in KINDS:
        sw = seed_swarm([targets[k]] * seeds, rule.world, gen)
        traj = []
        for t in range(steps):
            sw = rule(sw, gen)
            if t % every == 0:
                traj.append(sw.clone())
        rows = []
        for b in range(seeds):
            x = decode(sw, b)
            row = {}
            for k2 in KINDS:
                _, info = swarm_loss(x, targets[k2], L)
                row[k2] = info["sink"]
            row["census"] = census(sw, b, targets[k])
            rows.append(row)
        res[k] = dict(rows=rows, final=sw, traj=traj)
    return res



SWITCH_TO = {"mass": 2, "space": 0, "charge": 3, "time": 1}   # whale->jelly, jelly->puffer, puffer->dragonfly, dragonfly->whale


@torch.no_grad()
def rollout(rule, steps=240, every=5, seed=7, L=None, switch_steps=240):
    """Grow one swarm per body plan from a fresh seed and record it. Returns (data, summary):
    data[kind] = {frames: int16 [F, n_max, 15] packed units, n: [F], crystals: [[x,y,z,elem,step]]},
    summary = the cross-score matrix (every grown swarm against EVERY target) + census."""
    L = L or LossCfg()
    Lg = replace(L, w_elem=0.0, w_dom=0.0, rel_elem=0, w_mix=0.0, w_con=0.0)   # geometry only: shape, not composition
    targets = load_targets()
    gen = make_gen(seed)
    data, summary = {}, {"cross": {}, "census": {}, "steps": steps}
    for k in KINDS:
        sw = seed_swarm([targets[k]], rule.world, gen)
        frames, ns, crystals = [], [], []
        switched_at = None
        total = steps + (switch_steps if switch_steps else 0)
        for t in range(total + 1):
            if switch_steps and t == steps:
                x = decode(sw, 0)
                summary["cross"][k] = {k2: round(swarm_loss(x, targets[k2], L)[1]["sink"], 2) for k2 in KINDS}
                summary.setdefault("cross_geo", {})[k] = {k2: round(swarm_loss(x, targets[k2], Lg)[1]["sink"], 2) for k2 in KINDS}
                summary["census"][k] = census(sw, 0, targets[k])
                if lose_majority(sw, 0, gen, to=SWITCH_TO[k]):
                    switched_at = t
            if t % every == 0:
                x = decode(sw, 0)
                vis = x["hatched"]
                tier = x["tier"].argmax(1)
                u = torch.cat([x["p"], x["elem"][:, None].float(), x["dom"][:, None].float(), x["h"],
                               tier[:, None].float(), x["f"], x["sp"], x["w"][:, None]], 1)[vis]
                frames.append(u.cpu().numpy()); ns.append(int(vis.sum()))
            if t == total:
                break
            before = sw.active[0] & sw.hatched[0]
            pos0, el0 = sw.pos[0].clone(), sw.elem[0].clone()
            sw = rule(sw, gen)
            died = before & ~(sw.active[0] & sw.hatched[0])
            for i in died.nonzero().squeeze(1).tolist():
                crystals.append([*[round(float(v), 2) for v in pos0[i]], int(el0[i]), t])
        nmax = max(ns + [1])
        arr = np.zeros((len(frames), nmax, 15), np.float32)
        for i, f in enumerate(frames):
            arr[i, :len(f)] = f
        data[k] = dict(frames=arr, n=ns, crystals=crystals, switched_at=switched_at)
        x = decode(sw, 0)
        if switch_steps:
            new = PLAN_OF[SWITCH_TO[k]]
            row = {k2: round(swarm_loss(x, targets[k2], L)[1]["sink"], 2) for k2 in KINDS}
            summary.setdefault("switch", {})[k] = dict(to=new, done=switched_at is not None, cross=row,
                                                        majority=majority_plan(sw, 0, k), census=census(sw, 0, targets[new]))
        else:
            summary["cross"][k] = {k2: round(swarm_loss(x, targets[k2], L)[1]["sink"], 2) for k2 in KINDS}
            summary["census"][k] = census(sw, 0, targets[k])
    return data, summary


MIN_TEST_BODY = 32     # a swarm must have grown (twice the 16-tadpole seed) for a plan test to count


def tests_passed(summary):
    """The 8 tests: each seeding grows closest to its own plan (4), and after losing its majority each
    swarm ends closest to the new majority's plan (4). A test passes only if the swarm is alive and its
    plan is STRICTLY closest - an extinct swarm scores the sentinel 100 against every plan, and a tie
    must not count as a pass. The swarm must also have grown to MIN_TEST_BODY: the learned laying gate
    found a shortcut of never laying, and a 16-tadpole clump 'closest to the jellyfish' is not a body.
    Returns (passed, summed divergence over the 8 wanted plans)."""
    def ok(row, want, n):
        others = [v for k, v in row.items() if k != want]
        return n >= MIN_TEST_BODY and row[want] < 99.9 and row[want] < min(others) - 1e-6
    cross, cen, sw = summary["cross"], summary.get("census", {}), summary.get("switch", {})
    passed = sum(ok(cross[k], k, cen.get(k, {}).get("n", 1)) for k in KINDS)
    close = sum(cross[k][k] for k in KINDS)
    for v in sw.values():
        passed += ok(v["cross"], v["to"], v.get("census", {}).get("n", 1))
        close += v["cross"][v["to"]]
    return passed, close


def pack(data, steps=240):
    """int16 quantisation for the viewer: positions x50, extents/vectors x1000."""
    import base64
    scale = np.array([50, 50, 50, 1, 1, 1000, 1000, 1000, 1, 1000, 1000, 1000, 1000, 1000, 1000], np.float32)
    out = {}
    for k, d in data.items():
        q = np.clip(np.round(d["frames"] * scale), -32767, 32767).astype("<i2")
        out[k] = dict(shape=list(q.shape), b64=base64.b64encode(q.tobytes()).decode(), n=d["n"], crystals=d["crystals"],
                      switched_at=d.get("switched_at"))
    out["scale"] = scale.tolist()
    out["steps"] = steps
    return out


def print_switch(summary):
    if "switch" not in summary:
        return
    print("after losing the majority (another 240 steps), scored against each target:")
    for k in KINDS:
        sw_ = summary["switch"][k]
        row = sw_["cross"]; best = min(row, key=row.get)
        print(f"  {k:8s}-> {sw_['to']:7s} " + " ".join(f"{k2}:{row[k2]:7.2f}{'*' if k2 == best else ' '}" for k2 in KINDS)
              + f"  majority now {sw_['majority']}, n={sw_['census']['n']} el={sw_['census']['elements']}"
              + ("" if sw_["done"] else "  (could not switch: too few of the new element)"))


def print_geo(summary):
    if "cross_geo" not in summary:
        return
    print("same, GEOMETRY only (no element / domain cost) - does the shape, not the mix, pick the plan?")
    for k in KINDS:
        row = summary["cross_geo"][k]; best = min(row, key=row.get)
        print(f"  {k:8s}  " + "".join(f"{row[k2]:9.2f}" + ("*" if k2 == best else " ") for k2 in KINDS))


def print_cross(summary):
    print("grown from (rows) scored against each target (columns), Sinkhorn divergence, lower is closer:")
    print("            " + "".join(f"{k:>9s}" for k in KINDS))
    for k in KINDS:
        row = summary["cross"][k]
        best = min(row, key=row.get)
        print(f"  {k:8s}  " + "".join(f"{row[k2]:9.2f}" + ("*" if k2 == best else " ") for k2 in KINDS)[:-1]
              + f"   n={summary['census'][k]['n']}/{summary['census'][k]['target_n']} el={summary['census'][k]['elements']}"
              f" dom={summary['census'][k]['domains']} crystals={summary['census'][k]['deaths']} mutants={summary['census'][k]['mutants']}")


# --------------------------------------------------------------------- cli ---

def selftest():
    targets = load_targets()
    world = World(capacity=200)
    rule = SwarmRule(world)
    gen = make_gen(0)
    sw = seed_swarm([targets[k] for k in KINDS], world, gen)
    for k in KINDS:
        em, dm = seed_mix(targets[k], world.seed_n)
        print(k, targets[k].name, "seed elements", em.tolist(), "domains", dm.tolist(), "slots", targets[k].slots)
    for _ in range(30):
        sw = rule(sw, gen)
    # zero-init rule: no state change, so nobody dies; eggs are laid with parent domain
    assert int(sw.deaths.sum()) == 0
    for b in range(sw.B):
        m = sw.active[b]
        assert set(sw.dom[b][m].tolist()) <= set(range(targets[KINDS[b]].slots))
    # identity mapping keeps every element in range
    raw = torch.randn(4000, 3) * 4
    e = torch.randint(0, 4, (4000,))
    h = prism_h(raw, e)
    for el in range(4):
        hh = h[e == el]
        print(ELEMENTS[el], "h range", hh.min(0).values.cpu().numpy().round(2), hh.max(0).values.cpu().numpy().round(2),
              "aspect", (hh.max(1).values / hh.min(1).values).max().item())
    assert ((h[e == 1].max(1).values / h[e == 1].min(1).values) <= 1.6 + 1e-5).all()
    assert ((h[e == 2][:, 0] / h[e == 2][:, 1:].max(1).values) >= 2.14 - 1e-4).all()
    # loss: target vs itself is ~0, a shuffled-domain target is also ~0 (domain-neutral)
    L = LossCfg()
    for k in KINDS:
        t = targets[k].frames[0]
        x = dict(p=t["p"], w=torch.ones(len(t["p"])), alpha=torch.ones(len(t["p"])), pdie=torch.zeros(len(t["p"])), elem=t["elem"], dom=t["slot"], h=t["h"],
                 tier=F.one_hot(t["tier"], 3).float(), f=t["f"], sp=t["sp"], hatched=torch.ones(len(t["p"]), dtype=torch.bool))
        l0, i0 = swarm_loss(x, targets[k], L)
        x2 = dict(x); x2["dom"] = (t["slot"] + 1) % targets[k].slots
        l1, i1 = swarm_loss(x2, targets[k], L)
        x3 = dict(x); x3["p"] = t["p"] + torch.tensor([6.0, 0, 0])
        _, i2 = swarm_loss(x3, targets[k], L)
        x4 = dict(x); x4["p"] = t["p"] * torch.tensor([1.3, 1.0, 1.0])
        _, i3 = swarm_loss(x4, targets[k], L)
        print(f"{k}: self {i0['sink']:.4f} relabelled-domains {i1['sink']:.4f} (perm {i1['perm']}) "
              f"shifted-6 {i2['sink']:.4f} stretched-1.3x {i3['sink']:.3f}")
        assert i0["sink"] < 0.05 and i1["sink"] < 0.05 and i2["sink"] < 0.05 and i3["sink"] > 0.3
    # cross-target separation: each target scored against the others
    print("target x target sink:")
    for k in KINDS:
        t = targets[k].frames[0]
        x = dict(p=t["p"], w=torch.ones(len(t["p"])), alpha=torch.ones(len(t["p"])), pdie=torch.zeros(len(t["p"])), elem=t["elem"], dom=t["slot"], h=t["h"],
                 tier=F.one_hot(t["tier"], 3).float(), f=t["f"], sp=t["sp"], hatched=torch.ones(len(t["p"]), dtype=torch.bool))
        print(" ", k, " ".join(f"{k2}:{swarm_loss(x, targets[k2], L)[1]['sink']:7.2f}" for k2 in KINDS))
    print("selftest ok")


def bench():
    targets = load_targets()
    world = World()
    rule = SwarmRule(world)
    gen = make_gen(0)
    sw = seed_swarm([targets[k] for k in KINDS], world, gen)
    with torch.no_grad():
        for _ in range(60):
            sw = rule(sw, gen)
    # fake a grown swarm: activate everything visibly to time a worst case
    sw.active[:] = True; sw.hatched[:] = True; sw.s[:, :, A] = 1.0
    sw.pos = torch.randn_like(sw.pos) * 15
    t0 = time.time()
    with torch.no_grad():
        for _ in range(10):
            sw2 = rule(sw, gen, bud=False)
    print(f"forward (4 x {world.capacity} full): {(time.time() - t0) / 10 * 1000:.1f} ms/step")
    sw = sw.detach()
    t0 = time.time()
    x = sw
    for _ in range(28):
        x = rule(x, gen, bud=False)
    L = LossCfg()
    loss = sum(swarm_loss(decode(x, b), targets[k], L)[0] for b, k in enumerate(KINDS))
    t1 = time.time()
    loss.backward()
    print(f"28 steps fwd {t1 - t0:.2f}s, loss+bwd {time.time() - t1:.2f}s")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("selftest"); sub.add_parser("bench")
    ro = sub.add_parser("rollout")
    ro.add_argument("--rule", required=True)
    ro.add_argument("--steps", type=int, default=240)
    ro.add_argument("--out", default=os.path.join(HERE, "results", "swarm_coevo"))
    ro.add_argument("--scale-inv", type=int, default=0)
    tr = sub.add_parser("train")
    for f_, v in asdict(TrainCfg()).items():
        tr.add_argument("--" + f_.replace("_", "-"), type=type(v), default=v)
    a = ap.parse_args()
    if a.cmd == "selftest":
        selftest()
    elif a.cmd == "bench":
        bench()
    elif a.cmd == "rollout":
        rule = load_rule(a.rule)
        data, summary = rollout(rule, a.steps, L=LossCfg(scale_inv=a.scale_inv))
        print_cross(summary); print_geo(summary); print_switch(summary)
        os.makedirs(a.out, exist_ok=True)
        json.dump(pack(data, a.steps), open(os.path.join(a.out, "rollout.json"), "w"))
        summary["rule"] = os.path.relpath(a.rule, HERE)
        json.dump(summary, open(os.path.join(a.out, "summary.json"), "w"), indent=1)
    elif a.cmd == "train":
        cfg = TrainCfg(**{k: getattr(a, k) for k in asdict(TrainCfg())})
        train(cfg, World(), LossCfg())


if __name__ == "__main__":
    main()
