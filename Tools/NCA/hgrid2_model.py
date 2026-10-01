"""HGRID2: the grid morphogen, round 2 - accurate AND organic.

Round 1 (hgrid_boid.FieldBoid + OracleField) got the OUTLINE right from a coarse 16^3 grid of 6-voxel
cells: tadpoles climb their class's DEFICIT (wanted - actual density), lay where their class is
short, and starve where their class is in surplus. Its own-plan divergence sat at 7-18 because the
grid cannot SORT: within one 6-voxel cell (blurred over ~3 cells) every class looks alike, so which
element and which domain sits where inside the body is left to chance (runs/hgrid2/diag: pos 2.5-3.1
for three plans, then +3..5 from elements and +0.3..3.5 from domains; dragonfly pos alone 8.8).

Round 2 keeps the coarse grid for everything it does well (plan decision, composition, laying, flow,
looks) and adds a FINE morphogen layer at the scale of one tadpole:

  fine field   per class c = (element, domain): phi_c(x) = sum over the plan's units of class c of a
               Gaussian bump (sigma ~ the plan's unit spacing) MINUS the same bump over the live
               tadpoles of class c. It is the grid's class deficit, resolved finely enough to tell
               neighbouring sites apart; a tadpole climbs grad phi_c of its own class. No unit is ever
               ASSIGNED a site (field's Hungarian slots): sites are claimed by being occupied, so two
               tadpoles that want one site push it between them and one drifts to the next hole -
               imperfection that comes from competition, not from noise added on top.
  sharpness    the fine gain is scaled by how settled the body is (the coarse deficit has emptied), so
               the swarm first forms as a loose school and then crystallises its pattern.
  per-plan     animation period per plan (the dragonfly's wings move 10 voxels a frame) - a tadpole
  tempo        that cannot keep up with its target lags into the wrong pose.

Everything is designed (nothing learned) and model-agnostic in the yardstick's sense:
model(sw, gen) -> HSwarm, model.world.
"""
from __future__ import annotations

from dataclasses import dataclass, field, asdict

import torch
import torch.nn.functional as F

import swarm_nca as sn
import hgrid_core as hc
import hgrid_boid as hb


@dataclass
class Cfg(hb.BoidCfg):
    k_class: float = 6.0
    k_fine: float = 0.0         # gain on the fine class-deficit gradient
    k_fine_tot: float = 0.0     # gain on the fine all-class deficit (outline polish)
    sigma: float = 2.5          # fine bump width (voxels); the plans' unit spacing is 2.8-5.1
    settle: int = 1             # scale k_fine by how settled the body is (1 - relative coarse deficit)
    periods: str = ""           # per-plan animation period override "mass:8,time:16"
    fine_look: int = 0          # looks from the fine field (the nearest wanted units of the element) instead of the grid
    vel_pre: float = 0.0        # extra persistence on the fine term (smooths it: 0 = none)


def _periods(cfg):
    d = {}
    for kv in filter(None, cfg.periods.split(",")):
        k, v = kv.split(":")
        d[k] = int(v)
    return d


class Oracle2(hb.OracleField):
    def __init__(self, targets, cfg):
        super().__init__(targets, cfg)
        self.per = _periods(cfg)

    def frame_of(self, sw):
        cfg = self.cfg
        kinds = [sn.KINDS[int(g)] for g in sw.gplan]
        out = []
        for c, k in zip(sw.clock, kinds):
            p = self.per.get(k, cfg.period)
            out.append(int(c // p) % len(self.targets[k].frames) if cfg.animate else 0)
        return kinds, out

    def __call__(self, sw, centres, live, train=False):
        kinds, frames = self.frame_of(sw)
        return self.pf.field(kinds, frames, centres)


class Boid2(hb.FieldBoid):
    """FieldBoid + the fine morphogen layer (see module doc)."""

    def __init__(self, world, cfg: Cfg, targets=None):
        targets = targets or sn.load_targets()
        super().__init__(world, cfg, Oracle2(targets, cfg), targets)

    def fine_targets(self, sw, centres):
        """Per sample: target unit positions (placed at the grid centre) and their class (elem*3+domain)."""
        kinds, frames = self.field_fn.frame_of(sw)
        out = []
        for b, (k, f) in enumerate(zip(kinds, frames)):
            fr = self.targets[k].frames[f]
            p = fr["p"] - fr["p"].mean(0) + centres[b]
            cls = fr["elem"] * 3 + sw.dmap[b][fr["slot"]]
            out.append((p, cls, fr))
        return out

    def step(self, sw, gen=None, train=False):
        cfg = self.cfg
        sw = hb.HSwarm.lift(sw)
        pos0 = sw.pos.clone()
        out = super().step(sw, gen, train)
        if not cfg.k_fine and not cfg.k_fine_tot:
            return out
        # The fine layer is applied as an extra displacement on top of the coarse step, on the tadpoles
        # that were live through it (eggs laid this step are untouched).
        B, N, _ = out.pos.shape
        live = (sw.active & sw.hatched & out.active & out.hatched)
        lf = live.float()
        cen = (pos0 * lf[..., None]).sum(1) / lf.sum(1).clamp(min=1)[:, None]
        if cfg.quant:
            cen = torch.round(cen / cfg.cell) * cfg.cell
        tg = self.fine_targets(out, cen)
        s2 = 2 * cfg.sigma ** 2
        disp = torch.zeros_like(out.pos)
        for b in range(B):
            m = live[b].nonzero().squeeze(1)
            if len(m) < 2:
                continue
            p, tcls, fr = tg[b]
            x = out.pos[b, m]
            xc = out.elem[b, m] * 3 + out.dom[b, m]
            # wanted: bumps of the plan's units of the same class
            dt = x[:, None] - p[None]                               # [n,M,3]
            wt = torch.exp(-(dt * dt).sum(-1) / s2)                 # [n,M]
            same_t = (xc[:, None] == tcls[None]).float()
            # actual: bumps of the live tadpoles of the same class (self excluded: zero gradient anyway)
            dx = x[:, None] - x[None]
            wx = torch.exp(-(dx * dx).sum(-1) / s2)
            same_x = (xc[:, None] == xc[None]).float()
            # grad of a bump at x from a centre c: -(x-c)/sigma^2 * w
            g_want = -(wt[..., None] * dt * same_t[..., None]).sum(1)
            g_have = -(wx[..., None] * dx * same_x[..., None]).sum(1)
            g = (g_want - g_have) / cfg.sigma ** 2
            gt = (-(wt[..., None] * dt).sum(1) + (wx[..., None] * dx).sum(1)) / cfg.sigma ** 2
            k = cfg.k_fine
            if cfg.settle:
                want_n = float(len(p)); have_n = float(len(m))
                k = k * max(0.0, min(1.0, have_n / want_n))
            d = k * g + cfg.k_fine_tot * gt
            dn = d.norm(dim=-1, keepdim=True)
            vmax = torch.tensor(self.world.vmax)[out.elem[b, m]][:, None]
            d = d * (vmax / dn.clamp(min=1e-6)).clamp(max=1.0)
            disp[b, m] = d
        if cfg.vel_pre:
            prev = out.s[..., 15:18]
            disp = cfg.vel_pre * prev + (1 - cfg.vel_pre) * disp
            out.s[..., 15:18] = disp * lf[..., None]
        out.pos = out.pos + disp * lf[..., None]
        return out


def make(**kw):
    cfg = Cfg(**{k: (type(getattr(Cfg, k))(v) if not isinstance(getattr(Cfg, k), str) else v) for k, v in kw.items()})
    return Boid2(sn.World(), cfg)
