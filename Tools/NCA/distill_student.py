"""distill: a LOCAL student rule distilled from the `field` teacher (research direction `distill`, round 2).

Every tadpole sees only
  * itself (element, domain, velocity, looks, its own hidden channels),
  * neighbours within World.R (kernel-weighted counts per element / domain, mean offset, mean velocity,
    mean plan belief, the separation vector, startle), and
  * the population signals SwarmRule already perceives: headcount and element mix,
  * (vessel reaction only) a ship inside its sensing range.
No slot table, no swarm-wide assignment, no "nearest empty slot". The body plan lives only in the
MLP's weights (the genome); a tadpole's position in the body comes from a LOCAL morphogen:

  Z (designed, local): every tadpole keeps an estimate of the swarm's centre in world coordinates and
  refines it by averaging its neighbours' estimates (dynamic average consensus: it carries its own motion,
  diffuses over the neighbour graph, and leaks toward its own position so births/deaths cannot bias it
  forever). Nothing reads the true centroid.
  CLK (designed, local): an internal clock inherited from the parent (the animation phase).

Learned actuators (one shared MLP, heads):
  velocity (3) | look deltas (11: facing, prism, tier, spindle) | lay gate + egg direction (+ egg element)
  | MOLT gate + molt target element (a surplus tadpole may change element over molt_steps; field's relaxed
  constraint) | plan belief PB (4 logits, a hidden channel the neighbours see) | startle (1).

All per-tadpole state lives in the Swarm's channels, so the model is STATELESS (swarm_eval batches it).
"""
from __future__ import annotations

import math
import os
import sys
from dataclasses import dataclass

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402

C = sn.C
A, FAC, PR, TI, SP, DIE = sn.A, sn.FAC, sn.PR, sn.TI, sn.SP, sn.DIE
VEL = slice(12, 15)
Z = slice(15, 18)            # centroid estimate (world coords)
PB = slice(18, 22)           # plan belief logits (charge, mass, space, time element order = MAJOR index)
MOLT, MOLT_TO = 22, 23
CLK = 24
STL = 25
CAND, CNT = 26, 27     # (plan_mode=census) candidate majority + how long it has held
LOOK = list(range(1, 12))    # FAC, PR, TI, SP

NF = 78                      # feature width (asserted in features())
OUT = 3 + 11 + 1 + 3 + 4 + 1 + 4 + 4 + 1   # v, look, lay, egg dir, egg elem, molt, molt_to, pb, startle
LOOK_SCALE = torch.tensor([4.0] * 3 + [5.0] * 3 + [8.0] * 3 + [3.0] * 2)


@dataclass
class StudentCfg:
    hidden: int = 256
    cons_iters: int = 2       # consensus sweeps per step
    cons_a: float = 0.6       # neighbour averaging weight
    cons_leak: float = 0.004  # leak toward own position
    molt_steps: int = 10
    sep_r: float = 2.0
    lay_temp: float = 1.0     # sampling temperature on the lay / molt gates (1 = as trained)
    collide: int = 1
    lay_scale: float = 1.0    # multiplies the lay probability (calibration knob)
    molt_scale: float = 1.0
    plan_mode: str = "learned"   # learned: PB is the MLP's head | census: PB is the population census with
                                 # a per-tadpole dwell counter (field's hysteresis, computed by every tadpole)
    dwell: int = 12


def _edges(pos, active, R):
    B, N, _ = pos.shape
    d = torch.cdist(pos, pos)
    m = (d < R) & active[:, :, None] & active[:, None, :]
    m &= ~torch.eye(N, dtype=torch.bool)[None]
    b, i, j = m.nonzero(as_tuple=True)
    return b * N + i, b * N + j


class Student(nn.Module):
    stateless = True

    def __init__(self, world: sn.World | None = None, cfg: StudentCfg | None = None):
        super().__init__()
        self.world = world or sn.World()
        self.cfg = cfg or StudentCfg()
        H = self.cfg.hidden
        self.net = nn.Sequential(nn.Linear(NF, H), nn.SiLU(), nn.Linear(H, H), nn.SiLU(), nn.Linear(H, H), nn.SiLU(),
                                 nn.Linear(H, OUT))
        self.register_buffer("mu", torch.zeros(NF))
        self.register_buffer("sd", torch.ones(NF))
        self.predators = []       # [(centre np[3], radius, velocity np[3])], as field
        self.sense = 2.2

    def eval(self):
        super().eval()
        return self

    # ------------------------------------------------------------------ perception

    def features(self, sw: sn.Swarm, gi=None, gj=None):
        """[B*N, NF] for every slot (rows of inactive slots are garbage; callers mask)."""
        W = self.world
        B, N, _ = sw.pos.shape
        n = B * N
        pos = sw.pos.reshape(n, 3); s = sw.s.reshape(n, C)
        elem = sw.elem.reshape(n); dom = sw.dom.reshape(n)
        live = (sw.active & sw.hatched).reshape(n)
        if gi is None:
            gi, gj = _edges(sw.pos, sw.active & sw.hatched, W.R)
        R = W.R
        dx = pos[gj] - pos[gi]
        d2 = (dx * dx).sum(-1)
        q = (1 - d2 / (R * R)).clamp(min=0)
        w = q ** 3
        z = lambda k: torch.zeros(n, k)
        sumw = z(1).index_add(0, gi, w[:, None])
        eoh = F.one_hot(elem, 4).float(); doh = F.one_hot(dom, 3).float()
        we = z(4).index_add(0, gi, w[:, None] * eoh[gj])
        same = (dom[gi] == dom[gj]).float()
        wd = z(2).index_add(0, gi, torch.stack([w * same, w * (1 - same)], 1))
        den = (1 + sumw)
        mdx = z(3).index_add(0, gi, w[:, None] * dx) / den / R
        se = (elem[gi] == elem[gj]).float()
        mdx_e = z(3).index_add(0, gi, (w * se)[:, None] * dx) / (1 + z(1).index_add(0, gi, (w * se)[:, None])) / R
        mv = z(3).index_add(0, gi, w[:, None] * s[gj, VEL]) / den
        pb = torch.softmax(s[:, PB], -1)
        mpb = z(4).index_add(0, gi, w[:, None] * pb[gj]) / den
        dist = d2.clamp(min=1e-8).sqrt()
        sr = self.cfg.sep_r
        near = (dist < sr).float()
        sep = z(3).index_add(0, gi, (-dx / dist[:, None]) * (near * (sr - dist))[:, None])
        nnd = torch.full((n,), R).scatter_reduce(0, gi, dist, reduce="amin", include_self=True)[:, None] / R
        stl = s[:, STL]
        mstl = z(1).index_add(0, gi, w[:, None] * stl[gj, None]) / den
        xstl = torch.zeros(n).scatter_reduce(0, gi, stl[gj], reduce="amax", include_self=True)[:, None]
        # own
        off = (s[:, Z] - pos) / 10.0
        look = s[:, LOOK] / LOOK_SCALE
        clk = s[:, CLK]
        ph = torch.stack([torch.sin(2 * math.pi * clk / 48), torch.cos(2 * math.pi * clk / 48),
                          torch.sin(2 * math.pi * clk / 84), torch.cos(2 * math.pi * clk / 84)], 1)
        mp = s[:, MOLT:MOLT + 1]
        mt = F.one_hot(s[:, MOLT_TO].long().clamp(0, 3), 4).float() * (mp > 0).float()
        # population signals (the Cell's census of its fauna): headcount, element mix
        hb = (sw.hatched & sw.active).float()
        cnt = hb.sum(1).clamp(min=1)
        mix = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1) / cnt[:, None]
        glob = torch.cat([(cnt / 100)[:, None], mix], 1)[:, None, :].expand(B, N, 5).reshape(n, 5)
        # a ship inside sensing range (local: nothing beyond sense x radius is seen)
        pf = z(8)
        preds = self.predators if isinstance(self.predators, dict) else ({None: self.predators} if self.predators else {})
        rowb = torch.arange(n) // N
        for b, plist in preds.items():
            rows = torch.ones(n, dtype=torch.bool) if b is None else (rowb == b)
            for (c, rad, pv) in plist:
                c = torch.as_tensor(np.asarray(c), dtype=torch.float32); pv = torch.as_tensor(np.asarray(pv), dtype=torch.float32)
                rel = pos - c
                dd = rel.norm(dim=-1)
                inr = ((dd < self.sense * rad * 2.5) & rows).float()[:, None]
                cand = torch.cat([rel / 10, pv.expand(n, 3), torch.full((n, 1), rad / 10), torch.ones(n, 1)], 1) * inr
                better = (inr[:, 0] > 0) & ((pf[:, 7] == 0) | (dd < pf[:, :3].norm(dim=-1) * 10))
                pf = torch.where(better[:, None], cand, pf)
        f = torch.cat([eoh, doh, s[:, VEL], off, off.norm(dim=-1, keepdim=True), look, pb, ph, mp, mt,
                       sumw / 8, we / 8, wd / 8, mdx, mdx_e, mv, mpb, sep, nnd, mstl, xstl, stl[:, None],
                       glob, pf], 1)
        assert f.shape[1] == NF, f.shape
        return f, live, (gi, gj)

    def head(self, f):
        return self.net((f - self.mu) / self.sd)

    # ------------------------------------------------------------------ mechanics

    @torch.no_grad()
    def consensus(self, sw: sn.Swarm, gi, gj):
        """Dynamic average consensus on the centre estimate (designed, local)."""
        B, N, _ = sw.pos.shape
        n = B * N
        pos = sw.pos.reshape(n, 3); s = sw.s.reshape(n, C)
        zz = s[:, Z].clone()
        live = (sw.active & sw.hatched).reshape(n)
        for _ in range(self.cfg.cons_iters):
            ssum = torch.zeros(n, 3).index_add(0, gi, zz[gj]); cnt = torch.zeros(n).index_add(0, gi, torch.ones(len(gi)))
            avg = torch.where(cnt[:, None] > 0, ssum / cnt.clamp(min=1)[:, None], zz)
            zz = zz + self.cfg.cons_a * (avg - zz)
        zz = zz + self.cfg.cons_leak * (pos - zz)
        s[:, Z] = torch.where(live[:, None], zz, s[:, Z])

    def student_action(self, f, s, elem, live, gen):
        cfg, W = self.cfg, self.world
        n = len(f)
        out = self.head(f)
        vmax = torch.tensor(W.vmax)[elem][:, None]
        v = out[:, 0:3]
        sp = v.norm(dim=-1, keepdim=True)
        v = v * torch.clamp(vmax / sp.clamp(min=1e-6), max=1.0)
        look = s[:, LOOK] + out[:, 3:14] * LOOK_SCALE
        pl = (torch.sigmoid(out[:, 14] / cfg.lay_temp) * cfg.lay_scale).clamp(max=1)
        lay_par = live & (torch.rand(n, generator=gen) < pl)
        lay_dir = F.normalize(out[:, 15:18], dim=-1)
        pm = (torch.sigmoid(out[:, 22] / cfg.lay_temp) * cfg.molt_scale).clamp(max=1)
        molt_start = live & (s[:, MOLT] <= 0) & (torch.rand(n, generator=gen) < pm)
        mto = out[:, 23:27].clone()
        mto[torch.arange(n), elem] = -1e9                    # never "molt" into what you are
        return dict(v=v, look=look, lay_par=lay_par, lay_dir=lay_dir, lay_el=elem.clone(), molt_start=molt_start,
                    molt_to=mto.argmax(-1), pb=out[:, 27:31], stl=out[:, 31].clamp(0, 1))

    def census_plan(self, sw, s, live):
        """Every tadpole reads the population census (the element mix SwarmRule already perceives) and
        keeps its own dwell counter: a new majority must hold `dwell` steps before it believes it."""
        B, N, _ = sw.pos.shape
        hb = (sw.hatched & sw.active).float()
        cnt = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1)                 # [B, 4]
        cur = s[:, PB].argmax(-1).view(B, N)
        top = cnt.argmax(-1)[:, None].expand(B, N)
        tie = cnt.gather(1, cur) >= cnt.gather(1, top)
        maj = torch.where(tie, cur, top)
        cand = s[:, CAND].long().view(B, N); c = s[:, CNT].view(B, N)
        newc = torch.where(maj == cur, torch.zeros_like(c), torch.where(maj == cand, c + 1, torch.ones_like(c)))
        newcand = torch.where(maj == cur, cur, maj)
        commit = newc >= self.cfg.dwell
        cur2 = torch.where(commit, maj, cur)
        newc = torch.where(commit, torch.zeros_like(newc), newc)
        lv = live.view(B, N)
        s[:, CAND] = torch.where(live, newcand.reshape(-1).float(), s[:, CAND])
        s[:, CNT] = torch.where(live, newc.reshape(-1), s[:, CNT])
        return 6.0 * (F.one_hot(cur2.reshape(-1), 4).float() - 0.25)

    def forward(self, sw: sn.Swarm, gen=None, action=None, **_):
        """One step. action=None: the student acts. action=dict: an external policy (the teacher, for
        DAgger's mixture rollouts) supplies v / look / lay events / molt starts; same mechanics."""
        with torch.no_grad():
            return self.step(sw, gen, action)

    def step(self, sw: sn.Swarm, gen=None, action=None, mix=None):
        W, cfg = self.world, self.cfg
        sw = sw.clone()
        B, N, _ = sw.pos.shape
        n = B * N
        live2 = sw.active & sw.hatched
        # first step of a seed: hidden channels
        fresh = (sw.clock == 0)
        if bool(fresh.any()):
            for b in fresh.nonzero().squeeze(1).tolist():
                sw.s[b, :, Z] = sw.pos[b]
                sw.s[b, :, DIE] = -12.0
                if cfg.plan_mode == "census":
                    m = live2[b]
                    e0 = int(torch.bincount(sw.elem[b][m], minlength=4).argmax())
                    sw.s[b, :, PB] = 6.0 * (F.one_hot(torch.tensor(e0), 4).float() - 0.25)
        gi, gj = _edges(sw.pos, live2, W.R)
        self.consensus(sw, gi, gj)
        f, live, _ = self.features(sw, gi, gj)
        self._last_feats = f
        pos = sw.pos.reshape(n, 3); s = sw.s.reshape(n, C)
        elem = sw.elem.reshape(n)
        vmax = torch.tensor(W.vmax)[elem][:, None]
        if action is None or mix is not None:
            sa = self.student_action(f, s, elem, live, gen)
            if action is None:
                action = sa
            else:
                mb = mix.view(B, 1).expand(B, N).reshape(n)          # True: the external policy acts for that sample
                action = {k: torch.where(mb.view(-1, *([1] * (action[k].dim() - 1))), action[k], sa[k]) for k in sa}
        v = action["v"]; look = action["look"]
        lay_par = action["lay_par"] & live; lay_dir = action["lay_dir"]; lay_el = action["lay_el"]
        molt_start = action["molt_start"] & live; molt_to = action["molt_to"]
        pb_new = action["pb"]; stl_new = action["stl"]
        if cfg.plan_mode == "census":
            pb_new = self.census_plan(sw, s, live)
        lv = live[:, None]
        s[:, VEL] = torch.where(lv, v, s[:, VEL])
        s[:, LOOK] = torch.where(lv, look, s[:, LOOK])
        s[:, PB] = torch.where(lv, pb_new, s[:, PB])
        s[:, STL] = torch.where(live, stl_new, s[:, STL])
        pos2 = pos + torch.where(lv, v, torch.zeros_like(v))
        if cfg.collide:
            dxc = pos2[gj] - pos2[gi]
            r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
            ov = (W.r0 - r).clamp(min=0) / W.r0
            pos2 = pos2 + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = pos2.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        pos2 = torch.where(rad > W.membrane, pos2 * W.membrane / rad, pos2)
        # the centre estimate rides the tadpole's own motion
        s[:, Z] = torch.where(lv, s[:, Z] + (pos2 - pos), s[:, Z])
        sw.pos = pos2.view(B, N, 3)
        # molting (designed mechanics, learned trigger)
        run = live & (s[:, MOLT] > 0)
        s[:, MOLT] = torch.where(run, s[:, MOLT] + 1.0 / cfg.molt_steps, s[:, MOLT])
        done = run & (s[:, MOLT] >= 1.0)
        elem2 = torch.where(done, s[:, MOLT_TO].long(), elem)
        s[:, MOLT] = torch.where(done, torch.zeros_like(s[:, MOLT]), s[:, MOLT])
        st = molt_start & ~run
        s[:, MOLT] = torch.where(st, torch.full_like(s[:, MOLT], 1e-3), s[:, MOLT])
        s[:, MOLT_TO] = torch.where(st, molt_to.float(), s[:, MOLT_TO])
        sw.elem = elem2.view(B, N)
        s[:, CLK] = torch.where(live, s[:, CLK] + 1, s[:, CLK])
        s[:, A] = torch.where(live, torch.ones_like(s[:, A]), s[:, A]); s[:, DIE] = torch.where(live, torch.full_like(s[:, A], -12.0), s[:, DIE])
        sw.s = s.view(B, N, C)
        # laying: an egg beside the parent along the learned direction, true-bred (element + domain)
        lay_par = lay_par.view(B, N)
        if bool(lay_par.any()):
            for b in range(B):
                par = lay_par[b].nonzero().squeeze(1)
                if len(par) == 0:
                    continue
                free = (~sw.active[b]).nonzero().squeeze(1)
                k = min(len(par), len(free))
                if k == 0:
                    continue
                par = par[:k]; slots = free[:k]
                dirn = lay_dir.view(B, N, 3)[b, par]
                sw.pos[b, slots] = sw.pos[b, par] + W.r_bud * dirn
                sw.s[b, slots] = sw.s[b, par]
                sw.s[b, slots, MOLT] = 0.0
                sw.s[b, slots, STL] = 0.0
                sw.elem[b, slots] = lay_el.view(B, N)[b, par]
                sw.dom[b, slots] = sw.dom[b, par]
                sw.active[b, slots] = True; sw.hatched[b, slots] = True; sw.age[b, slots] = 0
        sw.clock = sw.clock + 1
        sw.since = sw.since + 1
        return sw
