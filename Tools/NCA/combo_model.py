"""COMBO: hgrid2 (grid morphogen, accurate + organic) made LOSSLESS - its starvation replaced by MOLTING.

hgrid2 corrects composition by starving surplus on a hunger timer (0.04/step, wither at 1.0): an imposed
death the game forbids. Here the identical selection (staggered per-tadpole hunger rates, the same class
excess, the same element floor) leads to a MOLT instead: the tadpole re-forms its crystal into the
element of its OWN domain the plan is most short of. Domain never changes. Nothing dies.

  molt progress   reuses the DIE channel as a 0..1 molt clock (`molt_rate` x a per-tadpole factor in
                  [0.5, 1.5]); at 1 the element flips and the clock restarts. The game animates the
                  crystal across that window (shrink old shape -> grow new one), so continuity holds.
  quota           a class molts at most its excess over the plan; a receiving class takes at most its
                  deficit; no non-majority element may come to tie the plan's major through a molt
                  (sort's guard) - otherwise a 9-tadpole survivor would flip plan by itself.
  transfer        (optional) a surplus tadpole whose own domain has no deficit anywhere becomes a FILLER:
                  it keeps its class but follows the all-class deficit only (its own-class pull is off), so
                  it fills holes in the outline instead of clinging where its class is not wanted.
  lay_cap         (optional) laying stops once the body holds `lay_cap` x the plan's headcount (a body that
                  cannot shed surplus must not breed more of it).

    model = combo_model.make(**hgrid2_eval.BEST, molt=1)
"""
from __future__ import annotations

from dataclasses import dataclass

import torch

import swarm_nca as sn
import hgrid2_model as hm


@dataclass
class ComboCfg(hm.Cfg):
    molt: int = 1               # replace starvation with molting (0 = hgrid2's starvation, for the ablation)
    molt_rate: float = 0.04     # molt clock per step (x a per-tadpole factor in [0.5, 1.5]); 25 steps typical
    molt_guard: int = 1         # never let a molt bring a non-major element level with the plan's major
    transfer: int = 0           # surplus with no molt target in its domain becomes an outline filler
    lay_cap: float = 0.0        # >0: no laying once live headcount >= lay_cap x plan headcount
    grid_every: int = 1         # (cost) the coarse grid step every k steps; the fine layer runs every step


class ComboBoid(hm.Boid2):
    def __init__(self, world, cfg: ComboCfg, targets=None):
        super().__init__(world, cfg, targets)
        self.molts = 0

    # --- composition: molt instead of starve -------------------------------------------------------
    def starve_staggered(self, out, ch=sn.DIE):
        cfg = self.cfg
        if not cfg.molt:
            return super().starve_staggered(out, ch)
        B, N, _ = out.pos.shape
        if hm.Boid2._U is None or hm.Boid2._U.shape[0] != N:
            hm.Boid2._U = 0.5 + torch.rand(N, generator=torch.Generator().manual_seed(1234))
        U = hm.Boid2._U
        live = out.active & out.hatched
        cls = out.elem * 3 + out.dom
        have = torch.zeros(B, 12).scatter_add(1, cls, live.float())
        want = torch.zeros(B, 12)
        for b in range(B):
            if int(out.gplan[b]) < 0:
                continue
            fr = self.targets[sn.KINDS[int(out.gplan[b])]].frames[0]
            want[b] = torch.bincount(fr["elem"] * 3 + out.dmap[b][fr["slot"]], minlength=12).float()
        slack = torch.full_like(want, cfg.starve_slack)
        if cfg.small_slack >= 0:
            small = (want.view(-1, 4, 3).sum(-1) <= 2)
            slack = torch.where(small.repeat_interleave(3, 1), torch.full_like(want, cfg.small_slack), slack)
        excess = (have - torch.floor((1 + cfg.starve_tol) * want) - slack).clamp(min=0)
        deficit = (want - have).clamp(min=0)                                                   # [B,12]
        # a class is "in surplus" only if its domain has somewhere to put it (else: transfer / wait)
        ddef = deficit.view(B, 4, 3).sum(1)                                                     # [B,3] per domain
        can = torch.gather(ddef, 1, out.dom) > 0.5                                              # [B,N]
        sur_i = (torch.gather(excess, 1, cls) > 0) & live
        h = out.s[..., ch]
        moltable = sur_i & can
        h = torch.where(moltable, h + cfg.molt_rate * U[None], (h - cfg.molt_rate).clamp(min=0))
        filler = sur_i & ~can
        for b in range(B):
            if int(out.gplan[b]) < 0:
                continue
            cand = (live[b] & moltable[b] & (h[b] >= 1.0)).nonzero().squeeze(1)
            if len(cand) == 0:
                continue
            cand = cand[torch.argsort(-h[b, cand])]
            quota = excess[b].clone(); dfc = deficit[b].clone()
            ecnt = torch.bincount(out.elem[b][live[b]], minlength=4).float()
            maj = sn.MAJOR[sn.KINDS[int(out.gplan[b])]]
            for i in cand.tolist():
                c = int(cls[b, i]); d = int(out.dom[b, i]); e0 = int(out.elem[b, i])
                if quota[c] < 1:
                    h[b, i] = 1.0
                    continue
                opts = dfc.view(4, 3)[:, d].clone()
                opts[e0] = 0
                if cfg.molt_guard:
                    for e in range(4):
                        if e != maj and e != e0 and ecnt[e] + 1 >= ecnt[maj] - (1 if e0 == maj else 0):
                            opts[e] = 0
                if float(opts.max()) < 0.5:
                    h[b, i] = 1.0
                    continue
                ne = int(opts.argmax())
                out.elem[b, i] = ne
                quota[c] -= 1; dfc[ne * 3 + d] -= 1
                ecnt[e0] -= 1; ecnt[ne] += 1
                h[b, i] = 0.0
                self.molts += 1
        out.s[..., ch] = h
        self._filler = filler if cfg.transfer else None

    # --- transfer: a stranded surplus tadpole follows the outline only ------------------------------
    def fine_disp(self, out, pos0, live, k_scale=1.0):
        d = super().fine_disp(out, pos0, live, k_scale)
        f = getattr(self, "_filler", None)
        if f is not None and f.shape == live.shape and bool(f.any()):
            # outline-only for fillers: the all-class fine gradient instead of the own-class one
            cfg = self.cfg
            k0, kt0 = cfg.k_fine, cfg.k_fine_tot
            cfg.k_fine, cfg.k_fine_tot = 0.0, max(k0, 1.0)
            try:
                dt = super().fine_disp(out, pos0, live & f, k_scale)
            finally:
                cfg.k_fine, cfg.k_fine_tot = k0, kt0
            d = torch.where(f[..., None], dt, d)
        return d

    def _lay(self, sw, parents_ok, have, want, gd, gen):
        cfg = self.cfg
        if cfg.lay_cap:
            live = (sw.active & sw.hatched)
            n = (sw.active).sum(1).float()
            cap = cfg.lay_cap * want.sum(1)
            parents_ok = parents_ok & (n < cap)[:, None]
        return super()._lay(sw, parents_ok, have, want, gd, gen)


def make(**kw):
    cfg = ComboCfg(**{k: (type(getattr(ComboCfg, k))(v) if not isinstance(getattr(ComboCfg, k), str) else v)
                      for k, v in kw.items()})
    return ComboBoid(sn.World(), cfg)
