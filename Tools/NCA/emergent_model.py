"""Species 3, the PURE EMERGENT swimmer: a learned per-tadpole rule that reads only local signals.

A tadpole perceives exactly two things:
  (a) its neighbours within the world radius R (swarm_nca.SwarmRule.perceive, unchanged: own state,
      same-domain / other-domain neighbour means, neighbour gradients, local density), and
  (b) K chemicals that the tadpoles THEMSELVES secrete into the medium. Each chemical lives on a
      fixed world grid (the water, not a body frame), diffuses to the 6 neighbouring cells and decays
      every step. A tadpole reads the concentration and its gradient at its own position.
It does NOT see: the headcount, the element census, the body centroid / axes, a clock, a slot, or
any designed attractor. swarm_nca.SwarmRule's population input (headcount + element mix, `glob`)
is removed.

What the rule outputs (all learned, one MLP shared by every tadpole):
  * velocity (capped per element by the world's vmax), its own state update (hatching, facing,
    prism, tier, spindle, hidden channels), its laying gate (World.learned_lay) and its death
    channel (a death is the rule's decision; it is penalised in training, never masked);
  * K secretion rates (sigmoid x E_MAX, so a tadpole can only add chemical, never remove it).
The chemicals' diffusion and decay rates are learned too (bounded so the explicit scheme is stable).

Designed (the shared world of swarm_nca, untouched): collision, membrane, per-element top speed,
neighbour radius, the egg rules (budding when crowded less than k_bud, egg life, breed-true domain,
rare element mutation), the capacity of 280 slots. Nothing steers toward the target.

The chemical grids ride on the swarm (ESwarm.fld), so cloning / culling / striking a swarm in the
shared yardsticks (swarm_probe, swarm_feel, scorecard) carries its water with it.
"""
from __future__ import annotations

import math
import os

import torch
import torch.nn as nn
import torch.nn.functional as F

import swarm_nca as sn
from swarm_nca import A, C, DIE, Swarm

K_CHEM = 4            # chemicals
GRID = 32             # cells per side
CELL = 3.0            # voxels per cell: the grid spans +-48 voxels around the origin (the cell membrane is 80)
E_MAX = 1.0           # max secretion per tadpole per step
D_MAX = 0.16          # explicit 3D diffusion is stable below 1/6
CHEM_FEATS = K_CHEM * 4


class ESwarm(Swarm):
    """A Swarm that also carries its water: fld [B, K, G, G, G] chemical concentrations."""
    FIELDS = Swarm.FIELDS + ("fld",)

    def __init__(self, *args, fld=None):
        super().__init__(*args)
        B = self.pos.shape[0]
        self.fld = fld if fld is not None else torch.zeros(B, K_CHEM, GRID, GRID, GRID, dtype=self.pos.dtype)

    @staticmethod
    def of(sw: Swarm):
        if isinstance(sw, ESwarm):
            return sw
        return ESwarm(*[getattr(sw, a) for a in Swarm.FIELDS])

    def _mk(self, vals):
        return ESwarm(*vals[:-1], fld=vals[-1])

    def detach(self):
        o = Swarm.detach(self)
        return ESwarm(*[getattr(o, a) for a in Swarm.FIELDS], fld=self.fld.detach())

    def clone(self):
        return self._mk([getattr(self, a).clone() for a in ESwarm.FIELDS])

    def index(self, idx):
        return self._mk([getattr(self, a)[idx] for a in ESwarm.FIELDS])

    @staticmethod
    def cat(xs):
        xs = [ESwarm.of(x) for x in xs]
        return xs[0]._mk([torch.cat([getattr(x, a) for x in xs]) for a in ESwarm.FIELDS])


def _corners(pos):
    """Trilinear corners of world positions pos [n,3] on the chemical grid: flat cell index [n,8],
    weights [n,8] (zero outside the grid)."""
    u = (pos + GRID * CELL / 2) / CELL - 0.5
    i0 = torch.floor(u)
    fr = u - i0
    i0 = i0.long()
    idx, wts = [], []
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                ix, iy, iz = i0[:, 0] + dx, i0[:, 1] + dy, i0[:, 2] + dz
                w = (fr[:, 0] if dx else 1 - fr[:, 0]) * (fr[:, 1] if dy else 1 - fr[:, 1]) * (fr[:, 2] if dz else 1 - fr[:, 2])
                ok = (ix >= 0) & (ix < GRID) & (iy >= 0) & (iy < GRID) & (iz >= 0) & (iz < GRID)
                idx.append(((ix.clamp(0, GRID - 1) * GRID + iy.clamp(0, GRID - 1)) * GRID + iz.clamp(0, GRID - 1)))
                wts.append(w * ok.to(w.dtype))
    return torch.stack(idx, 1), torch.stack(wts, 1)


_LAP = None


def _lap_kernel(dtype):
    global _LAP
    if _LAP is None or _LAP.dtype != dtype:
        k = torch.zeros(1, 1, 3, 3, 3, dtype=dtype)
        k[0, 0, 1, 1, 1] = -6
        for (a, b, c) in ((0, 1, 1), (2, 1, 1), (1, 0, 1), (1, 2, 1), (1, 1, 0), (1, 1, 2)):
            k[0, 0, a, b, c] = 1
        _LAP = k
    return _LAP


def laplacian(f):
    B, Kc = f.shape[:2]
    return F.conv3d(f.reshape(B * Kc, 1, GRID, GRID, GRID), _lap_kernel(f.dtype), padding=1).view_as(f)


def gradient(f):
    """Central-difference gradient per chemical, [B, K, 3, G, G, G] in concentration per voxel."""
    p = F.pad(f, (1, 1, 1, 1, 1, 1))
    gx = (p[:, :, 2:, 1:-1, 1:-1] - p[:, :, :-2, 1:-1, 1:-1])
    gy = (p[:, :, 1:-1, 2:, 1:-1] - p[:, :, 1:-1, :-2, 1:-1])
    gz = (p[:, :, 1:-1, 1:-1, 2:] - p[:, :, 1:-1, 1:-1, :-2])
    return torch.stack([gx, gy, gz], 2) / (2 * CELL)


class EmergentRule(sn.SwarmRule):
    """SwarmRule minus every global input, plus a stigmergic chemical channel."""

    def __init__(self, world: sn.World, hidden=192, fire_rate=0.5, k_chem=K_CHEM):
        nn.Module.__init__(self)
        self.world, self.hidden, self.fire_rate = world, hidden, fire_rate
        self.G = 0
        self.k = k_chem
        X = self.X
        self.F_local = X * 3 + X * 3 + 3 + 2
        self.F = self.F_local + CHEM_FEATS
        self.w1 = nn.Parameter(torch.empty(hidden, self.F)); nn.init.xavier_uniform_(self.w1)
        self.b1 = nn.Parameter(torch.zeros(hidden))
        self.w2 = nn.Parameter(torch.empty(hidden, hidden)); nn.init.xavier_uniform_(self.w2, gain=0.5)
        self.b2 = nn.Parameter(torch.zeros(hidden))
        self.w3 = nn.Parameter(torch.zeros(C + 3 + k_chem, hidden))
        self.b3 = nn.Parameter(torch.zeros(C + 3 + k_chem))
        with torch.no_grad():
            self.b3[A] = 0.04
        # chemical physics, learned inside stable bounds: D in (0, D_MAX), decay in (0.002, 0.5)
        self.d_raw = nn.Parameter(torch.tensor([1.5, 0.5, 0.0, -1.0])[:k_chem].clone())
        self.l_raw = nn.Parameter(torch.tensor([-4.0, -3.0, -2.0, -1.0])[:k_chem].clone())

    def chem_rates(self):
        return D_MAX * torch.sigmoid(self.d_raw), 0.002 + 0.5 * torch.sigmoid(self.l_raw)

    @torch.no_grad()
    def warm_from(self, path, glob_const=None):
        """Initialise from a swarm_nca rule (e.g. the solo_space specialist). Its population inputs are
        folded into the first bias as a CONSTANT, so the warm-started rule reads nothing global."""
        st = torch.load(path, weights_only=False, map_location="cpu")["rule"]
        w1 = st["w1"]
        n_local = self.F_local
        self.w1.zero_(); self.w1[:, :n_local] = w1[:, :n_local]
        self.b1.copy_(st["b1"])
        if w1.shape[1] > n_local and glob_const is not None:
            self.b1 += w1[:, n_local:n_local + len(glob_const)] @ torch.tensor(glob_const, dtype=w1.dtype)
        self.w2.copy_(st["w2"]); self.b2.copy_(st["b2"])
        self.w3.zero_(); self.b3.zero_()
        self.w3[:C + 3] = st["w3"]; self.b3[:C + 3] = st["b3"]
        nn.init.normal_(self.w1[:, n_local:], std=0.02)

    def forward(self, sw, gen=None, bud=True, fire=None):
        return self._step(ESwarm.of(sw), gen, bud, fire)

    def _step(self, sw: ESwarm, gen=None, bud=True, fire=None):
        W = self.world
        B, N, _ = sw.pos.shape
        n = B * N
        G3 = GRID ** 3
        pos, s = sw.pos.reshape(n, 3), sw.s.reshape(n, C)
        elem, dom, act = sw.elem.reshape(n), sw.dom.reshape(n), sw.active.reshape(n)
        hatched = sw.hatched.reshape(n)
        gi, gj = sn.edges(sw, W.R)
        x = torch.cat([s, F.one_hot(elem, 4).to(s.dtype), hatched[:, None].to(s.dtype)], 1)
        fire = act & ((torch.rand(n, generator=gen) <= self.fire_rate) if fire is None else fire.reshape(n))
        idx = fire.nonzero().squeeze(1)
        e = fire[gi]
        # ---- read the water at my position: concentration (log) and its gradient (relative, Weber-like)
        fld = sw.fld
        grd = gradient(fld)                                                 # [B,K,3,G,G,G]
        tab = torch.cat([fld.reshape(B, self.k, G3), grd.reshape(B, self.k * 3, G3)], 1)   # [B,4K,G3]
        tab = tab.permute(0, 2, 1).reshape(B * G3, 4 * self.k)
        cidx, cw = _corners(pos)
        boff = (torch.arange(n) // N) * G3
        val = (tab[cidx + boff[:, None]] * cw[:, :, None]).sum(1)          # [n, 4K]
        conc = val[:, :self.k].clamp(min=0)
        g = val[:, self.k:].view(n, self.k, 3) / (1 + conc)[:, :, None]
        chem = torch.cat([torch.log1p(conc), 4 * g.reshape(n, 3 * self.k)], 1)
        feats = torch.cat([self.perceive(pos, x, dom, gi[e], gj[e], n), chem], 1).index_select(0, idx)
        out = self.mlp(feats)
        ds = torch.zeros(n, C).index_copy(0, idx, out[:, :C])
        vmax = torch.tensor(W.vmax)[elem].index_select(0, idx)[:, None]
        v = torch.zeros(n, 3).index_copy(0, idx, vmax * torch.tanh(out[:, C:C + 3]))
        sec = torch.zeros(n, self.k).index_copy(0, idx, E_MAX * torch.sigmoid(out[:, C + 3:]))
        live = (act & hatched).to(s.dtype)
        sec = sec * live[:, None]
        s = (s + ds).clamp(-sn.S_MAX, sn.S_MAX)
        # ---- the water: secrete, diffuse, decay (all local; the box edge absorbs)
        dep = torch.zeros(B * G3, self.k).index_add(0, (cidx + boff[:, None]).reshape(-1),
                                                    (cw[:, :, None] * sec[:, None, :]).reshape(-1, self.k))
        dep = dep.view(B, G3, self.k).permute(0, 2, 1).reshape_as(fld)
        D, lam = self.chem_rates()
        fld = (1 - lam)[None, :, None, None, None] * (fld + D[None, :, None, None, None] * laplacian(fld)) + dep
        # ---- motion + world physics (swarm_nca, unchanged)
        pos = pos + v
        dxc = pos[gj] - pos[gi]
        r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - r).clamp(min=0) / W.r0
        pos = pos + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = pos.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        pos = pos - 0.5 * (rad - W.membrane).clamp(min=0) * pos / rad
        with torch.no_grad():
            hat2 = hatched | (act & (s[:, A] > 0.1))
            died = act & hatched & (s[:, DIE] > sn.DIE_AT)
            alive = hat2 & ~died
            near = alive.float().clone().scatter_reduce(0, gi, alive[gj].float(), reduce="amax", include_self=True) > 0
            age = sw.age.reshape(n) + (act & ~hat2).long()
            gone_egg = act & ~hat2 & (~near | (age > W.egg_life))
            keep = act & ~died & ~gone_egg
            new_hatched = hat2 & keep
            deaths = sw.deaths + died.view(B, N).sum(1)
        s = s * keep[:, None].to(s.dtype)
        out_sw = ESwarm(pos.view(B, N, 3), s.view(B, N, C), sw.elem.clone(), sw.dom.clone(), keep.view(B, N),
                        new_hatched.view(B, N), deaths, sw.mutants.clone(), (age * keep.long()).view(B, N), sw.clock + 1,
                        sw.plan.clone(), sw.since + 1, sw.bw * keep.view(B, N).to(sw.bw.dtype), fld=fld)
        if bud:
            self.lay(out_sw, gi, gj, gen)
        return out_sw


def save_rule(rule, path, extra=None):
    from dataclasses import asdict
    torch.save(dict(rule={k: v.detach().cpu() for k, v in rule.state_dict().items()}, world=asdict(rule.world),
                    hidden=rule.hidden, k_chem=rule.k, **(extra or {})), path)


def load_rule(path):
    st = torch.load(path, weights_only=False, map_location="cpu")
    w = dict(st["world"]); w["vmax"] = tuple(w["vmax"])
    rule = EmergentRule(sn.World(**w), hidden=st["hidden"], k_chem=st.get("k_chem", K_CHEM))
    rule.load_state_dict(st["rule"])
    return rule
