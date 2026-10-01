"""HGRID2 direction (b): the EVOLVED body (evo_model.EvoRule, results/evo/genome.npy - the lead's
favourite feel) steered by the grid morphogen for BOTH composition and sorting.

Per step:
  1. the evolved rule moves, looks, hatches and dies exactly as shipped (its own laying switched off);
  2. the grid layer decides the plan (majority element) and rasterises it (hgrid2_model.Oracle2);
  3. STEER: every live tadpole gets an extra displacement = s_coarse x (its class's coarse deficit
     gradient) + s_fine x (the fine per-class deficit + feed-forward of hgrid2_model), capped at the
     element's top speed. s_* < 1 leaves the evolved motion visible underneath - the organic body
     is nudged into place, not replaced;
  4. LOOK (optional, ease_look): prism / tier / facing / spindle eased toward the grid's attributes;
  5. COMPOSITION: grid laying (a class with room breeds, 25% cross-element births) and shedding of
     surplus classes as crystals (round 1's hybrid).

    python Tools/NCA/hgrid2_evo.py --set s_fine=0.5 ...  (scored like hgrid2_eval)
"""
from __future__ import annotations

from dataclasses import dataclass, asdict

import numpy as np
import torch
import torch.nn.functional as F

import swarm_nca as sn
import hgrid_core as hc
import hgrid_boid as hb
import hgrid2_model as hm
import evo_model as em


@dataclass
class EvoCfg(hm.Cfg):
    s_coarse: float = 0.5
    s_fine: float = 0.7
    ease_look: float = 0.2
    shed: int = 1
    s_mig_note: str = 'migration uses k_mig / mig_th / mig_L of hgrid2_model.Cfg'


class EvoGrid:
    def __init__(self, cfg: EvoCfg, genome="results/evo/genome.npy"):
        import os
        self.cfg = cfg
        self.evo = em.EvoRule(np.load(os.path.join(sn.HERE, genome) if not os.path.isabs(genome) else genome))
        self.world = self.evo.world
        self.targets = sn.load_targets()
        self.boid = hm.Boid2(self.world, cfg, self.targets)
        self.oracle = self.boid.field_fn

    def __call__(self, sw, gen=None):
        cfg = self.cfg
        hsw = hb.HSwarm.lift(sw)
        gplan, dmap = hsw.gplan.clone(), hsw.dmap.clone()
        if bool((hsw.clock == 0).all()):
            gplan[:] = -1
        pos0 = sw.pos.clone()
        out = hb.HSwarm.lift(self.evo(sw, gen, bud=False))
        out.gplan, out.dmap = gplan, dmap
        B, N, _ = out.pos.shape
        live = out.active & out.hatched
        lf = live.float()
        centres = (out.pos * lf[..., None]).sum(1) / lf.sum(1).clamp(min=1)[:, None]
        centres = torch.round(centres / cfg.cell) * cfg.cell
        self.boid.decide(out)
        frame = hc.GridFrame(centres, cfg.G, cfg.cell)
        D = self.oracle(out, centres, live)
        Dc = D[:, :hc.NCLS].reshape(B, 4, 3, *D.shape[2:])
        inv = torch.zeros(B, 3, dtype=torch.long)
        for b in range(B):
            for s in range(3):
                inv[b, out.dmap[b, s]] = s
        Dd = torch.stack([Dc[b][:, inv[b]] for b in range(B)]).reshape(B, 12, *D.shape[2:])
        cls = out.elem * 3 + out.dom
        A = hc.blur(hc.splat(frame, out.pos, F.one_hot(cls, 12).float(), live), 1)
        Def = Dd - A
        gd = hc.sample(frame, hc.grad(frame, Def).flatten(1, 2), out.pos).reshape(B, N, 12, 3)
        d_own = torch.gather(hc.sample(frame, Def, out.pos), 2, cls[..., None])[..., 0]
        want = Dd.flatten(2).sum(-1)
        # STEER
        g_own = torch.gather(gd, 2, cls[..., None, None].expand(B, N, 1, 3))[:, :, 0]
        steer = cfg.s_coarse * cfg.k_class * g_own
        if cfg.s_fine:
            steer = steer + self.boid.fine_disp(out, pos0, live, k_scale=1.0) * cfg.s_fine
        vmax = torch.tensor(self.world.vmax)[out.elem][..., None]
        sp = steer.norm(dim=-1, keepdim=True)
        steer = steer * (vmax / sp.clamp(min=1e-6)).clamp(max=1.0)
        out.pos = out.pos + steer * lf[..., None]
        if cfg.k_mig:
            self.boid.migrate(out, pos0, live)
        if cfg.k_swap:
            self.boid.swaps(out, pos0, live)
        # LOOK
        if cfg.ease_look:
            n = B * N
            rows = hc.sample(frame, D, out.pos)
            pr, tl, fa, spn = hc.attr_at(rows.reshape(n, -1), out.elem.reshape(n))
            tgt = torch.cat([pr, tl, 5 * fa, spn], 1).view(B, N, 11)
            s = out.s
            cur = torch.cat([s[..., sn.PR], s[..., sn.TI], s[..., sn.FAC], s[..., sn.SP]], -1)
            cur = cur + cfg.ease_look * (tgt - cur) * lf[..., None]
            s[..., sn.PR], s[..., sn.TI], s[..., sn.FAC], s[..., sn.SP] = cur[..., :3], cur[..., 3:6], cur[..., 6:9], cur[..., 9:11]
        # COMPOSITION
        have = torch.zeros(B, 12).scatter_add(1, cls, lf)
        if cfg.shed and cfg.stagger:
            self.boid.starve_staggered(out, ch=25)          # hunger rides on a hidden channel: the evolved rule owns DIE
            live = out.active & out.hatched
            have = torch.zeros(B, 12).scatter_add(1, cls, live.float())
        elif cfg.shed:
            surplus = have > (1 + cfg.starve_tol) * want + 1.0
            sur_i = torch.gather(surplus, 1, cls) & live & (d_own < cfg.starve_local)
            h = out.s[..., 25]
            h = torch.where(sur_i, h + cfg.starve_rate, (h - cfg.starve_rate).clamp(min=0))
            died = live & (h > 1.0)
            out.s[..., 25] = torch.where(died, torch.zeros_like(h), h)
            out.deaths = out.deaths + died.sum(1)
            out.active = out.active & ~died; out.hatched = out.hatched & ~died
            out.s = out.s * (~died)[..., None].float()
            live = live & ~died
            have = torch.zeros(B, 12).scatter_add(1, cls, live.float())
        self.boid._lay(out, live, have, want, gd, gen)
        return out


def make(**kw):
    cfg = EvoCfg(**{k: (type(getattr(EvoCfg, k))(v) if not isinstance(getattr(EvoCfg, k), str) else v) for k, v in kw.items()})
    return EvoGrid(cfg)
