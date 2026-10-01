"""evo16: harden the 16/16 evo genome - new behaviours on top of evo_model.EvoRule.

Evo16Rule = EvoRule (G2 rule + B1 lay homeostat, B2 egg choice, B3 majority lock, B5 output gains) plus
three new behaviours, each with an on/off gene (ON when > 0) so CMA-ES can delete it and so it can be
ablated:

  B6  regrowth homeostat  laying is boosted by the swarm's headcount DEFICIT against its own reference:
                          the high-water mark it has reached (capped by B8's plan headcount when B8 is on).
                          mult *= 1 + softplus(a_reg) * clamp((ref - n) / ref, 0, 1)
  B7  wound sensing       every tadpole remembers an EMA of how many neighbours it has (within r_lay).
                          A tadpole that suddenly has fewer than it remembers is on a WOUND; it lays more
                          (1 + softplus(a_wnd) * wound), and while the swarm is in deficit the un-wounded
                          lay less (1 - sigmoid(f_wnd) * deficit), so a carved body refills where it was cut
                          instead of all over its surface. Production gating only.
  B8  headcount target    per plan, a headcount the swarm stops growing at: target = T[plan].n * exp(h[plan]);
                          laying is gated by sigmoid(k_head * (target - n) / target / 0.1). Plans' own unit
                          counts are the init (whale 192, puffer 179, jelly 88, dragonfly 76), so a whale is
                          big and a dragonfly small instead of everything filling the 280 cap. Never culls.

State (high-water, neighbour EMA, the lock) lives on the model, [B] / [B,N], and is re-initialised whenever
the incoming swarm is not the one this model produced last step (a new seed, or swarm_eval branching a
grown swarm into a new batch). `adopt(idx)` lets a caller keep it across a re-index (evo16_fit does, for
its strike rows, so a vessel strike lands on a swarm that remembers how big it was).

`stateless = True`: swarm_eval batches the switch branches (state re-initialises at the branch, which is
what the branch's fresh swarm would see).
"""
import math
import os
import sys

import numpy as np
import torch
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import evo_model as em  # noqa: E402
import swarm_nca as sn  # noqa: E402

_T = sn.load_targets()
PLAN_N = torch.tensor([float(_T[sn.PLAN_OF[e]].n) for e in range(4)])    # by majority ELEMENT

NEW = [
    ("sw_reg", 1, -1.0), ("a_reg", 1, 1.0),
    ("sw_wnd", 1, -1.0), ("a_wnd", 1, 1.0), ("f_wnd", 1, 0.0), ("tau_wnd", 1, 0.0),
    ("sw_head", 1, -1.0), ("h_head", 4, 0.3), ("k_head", 1, 1.0),
    ("sw_hsel", 1, -1.0),      # B8b: the headcount gate throttles only parents whose element is NOT short
    ("t_hsel", 1, 0.0),        # B8b: "short" means the deficit exceeds t_hsel (a share, 0..1)
]
LAYOUT = em.LAYOUT + NEW
SLICES, DIM = {}, 0
for _n, _s, _ in LAYOUT:
    SLICES[_n] = slice(DIM, DIM + _s); DIM += _s
SWITCHES = ("sw_lay", "sw_egg", "sw_lock", "sw_out", "sw_reg", "sw_wnd", "sw_head", "sw_hsel")


def default_genome():
    g = np.zeros(DIM)
    for n, s, v in LAYOUT:
        g[SLICES[n]] = v
    return g


def from_evo(g_evo):
    """Extend an evo_model genome (95 floats), or an evo16 genome saved before a gene was appended, with
    the missing genes at their defaults (behaviours off)."""
    g = default_genome()
    g[:len(g_evo)] = g_evo
    return g


def gene(g, name):
    v = g[SLICES[name]]
    return float(v[0]) if len(v) == 1 else v


def sp(x):
    return math.log1p(math.exp(x))


def sig(x):
    return 1 / (1 + math.exp(-x))


def describe(g):
    lines = [em.describe(g[:em.DIM]),
             f"B6 regrowth on={gene(g,'sw_reg')>0} gain={sp(gene(g,'a_reg')):.2f}",
             f"B7 wound on={gene(g,'sw_wnd')>0} gain={sp(gene(g,'a_wnd')):.2f} focus={sig(gene(g,'f_wnd')):.2f} "
             f"ema rate={0.3*sig(gene(g,'tau_wnd')):.3f}",
             f"B8 headcount on={gene(g,'sw_head')>0} k={gene(g,'k_head'):.2f} targets " +
             " ".join(f"{sn.PLAN_OF[e]}={float(PLAN_N[e])*math.exp(gene(g,'h_head')[e]):.0f}" for e in range(4)) +
             f" | B8b selective={gene(g,'sw_hsel')>0} past deficit {gene(g,'t_hsel'):.2f}"]
    return "\n".join(lines)


class Evo16Rule(em.EvoRule):
    stateless = True

    def __init__(self, genome):
        genome = np.asarray(genome, float)
        if len(genome) < DIM:
            genome = from_evo(genome)
        super().__init__(genome[:em.DIM])
        self.genome = genome
        g = genome
        self.on = {k: gene(g, k) > 0 for k in SWITCHES}
        self.a_reg = sp(gene(g, "a_reg"))
        self.a_wnd, self.f_wnd, self.r_wnd = sp(gene(g, "a_wnd")), sig(gene(g, "f_wnd")), 0.3 * sig(gene(g, "tau_wnd"))
        self.head_n = PLAN_N * torch.tensor(np.exp(gene(g, "h_head")), dtype=torch.float32)
        self.k_head = gene(g, "k_head")
        self.t_hsel = gene(g, "t_hsel")
        self._tok = None
        self.hw = None
        self.ema = None

    # ------------------------------------------------------------ state ---
    def _reset(self, sw):
        n = (sw.active & sw.hatched).sum(1).float()
        self.hw = n.clone()
        self.ema = None                        # filled from the first neighbour count
        self.locked = torch.full((sw.B,), -1, dtype=torch.long)

    def adopt(self, idx, sw_new):
        """Keep the state of rows `idx` (a re-index of the last batch) for the swarm `sw_new`."""
        self.hw = self.hw[idx].clone()
        self.ema = None if self.ema is None else self.ema[idx].clone()
        self.locked = self.locked[idx].clone()
        self._tok = sw_new.pos

    def forward(self, sw, gen=None, bud=True, fire=None):
        with torch.no_grad():
            if self._tok is not sw.pos or self.hw is None or self.hw.shape[0] != sw.B:
                self._reset(sw)
            fresh = sw.clock == 0
            if fresh.any():
                self.hw[fresh] = (sw.active & sw.hatched)[fresh].sum(1).float()
                self.locked[fresh] = -1
                if self.ema is not None:
                    self.ema[fresh] = -1
            self._update_lock(sw)
            out = self._step(sw, gen, bud, fire)
            self._tok = out.pos
            return out

    # ------------------------------------------------------------- laying ---
    @torch.no_grad()
    def _lay(self, sw, gi, gj, gen, q, pe=None):
        W = self.world
        B, N, _ = sw.pos.shape
        pos = sw.pos.reshape(B * N, 3)
        dxl = pos[gj] - pos[gi]
        close = ((dxl * dxl).sum(-1) < W.r_lay ** 2).float()
        ncl = torch.zeros(B * N).index_add(0, gi, close).view(B, N)
        alive = sw.active & sw.hatched
        n = alive.sum(1).float()
        self.hw = torch.maximum(self.hw, n)
        # neighbour memory (every step, also when B7 is off, so its state is ready if adopted)
        if self.ema is None:
            self.ema = torch.where(alive, ncl, torch.full_like(ncl, -1.0))
        newb = alive & (self.ema < 0)
        self.ema = torch.where(newb, ncl, self.ema)
        self.ema = torch.where(alive, self.ema + self.r_wnd * (ncl - self.ema), torch.full_like(ncl, -1.0))
        mult = torch.ones(B, N)
        lead = self.locked.clamp(min=0)
        tgt = self.head_n[lead]                                             # [B]
        if self.on["sw_head"]:
            hg = (torch.sigmoid(self.k_head * (tgt - n) / tgt / 0.1) / torch.sigmoid(torch.tensor(self.k_head * 10.0)))[:, None].expand(B, N)
            if self.on["sw_hsel"]:
                # B8b: a parent whose element the locked plan is SHORT of keeps laying past the headcount,
                # so a full swarm can still change its mix (a plan-sized gate otherwise froze charge->time)
                cnt = (alive.float()[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1)
                share = cnt / cnt.sum(1, keepdim=True).clamp(min=1)
                short = (self.D[lead] - share).gather(1, sw.elem) > self.t_hsel
                hg = torch.where(short, torch.ones_like(hg), hg)
            mult = mult * hg
        ref = torch.minimum(self.hw, tgt) if self.on["sw_head"] else self.hw
        deficit = ((ref - n) / ref.clamp(min=1)).clamp(0, 1)                # [B]
        if self.on["sw_reg"]:
            mult = mult * (1 + self.a_reg * deficit)[:, None]
        if self.on["sw_wnd"]:
            wound = ((self.ema - ncl) / (self.ema + 1)).clamp(0, 1) * alive.float()
            wound = torch.where(wound > 0.15, wound, torch.zeros_like(wound))
            mult = mult * (1 + self.a_wnd * wound) * (1 - self.f_wnd * deficit[:, None] * (wound <= 0).float())
        q2 = mult.reshape(-1) if q is None else q * mult.reshape(-1)
        return super()._lay(sw, gi, gj, gen, q2, pe)
