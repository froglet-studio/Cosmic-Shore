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
    interp: int = 0             # fine targets move CONTINUOUSLY between the plan's frames (no 8-step jumps)
    lock: int = 0               # a plan, once committed, holds this many steps (composition settles first); 0 = off
    dmap_low: int = 0           # a plan with k < 3 slots maps them onto domains 0..k-1 (the yardstick's loss only
                                # considers those for a k-slot plan; any other domain scores as a mismatch)
    dmap_space: int = 0         # on a plan change, map slots to domains by WHERE each domain already sits (overlap on the grid)
    lay_major: float = 0.0      # laying pressure x (class want / plan's largest class want)^lay_major: the plan's major breeds fastest
    k_ff: float = 0.0           # feed-forward: a tadpole takes this share of its nearby same-class units' own velocity


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

    def frame_of(self, sw, frac=False):
        cfg = self.cfg
        kinds = [sn.KINDS[int(g)] for g in sw.gplan]
        out = []
        for c, k in zip(sw.clock, kinds):
            p = self.per.get(k, cfg.period)
            f = int(c // p) % len(self.targets[k].frames) if cfg.animate else 0
            out.append((f, (float(c) % p) / p if cfg.animate else 0.0, p) if frac else f)
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
        kinds, frames = self.field_fn.frame_of(sw, frac=True)
        out = []
        for b, (k, (f, a, per)) in enumerate(zip(kinds, frames)):
            frs = self.targets[k].frames
            fr = frs[f]
            p0 = fr["p"] - fr["p"].mean(0)
            nx = frs[(f + 1) % len(frs)]
            p1 = nx["p"] - nx["p"].mean(0)
            vel = (p1 - p0) / per                               # units keep their index across frames
            p = (p0 + a * (p1 - p0) if self.cfg.interp else p0) + centres[b]
            cls = fr["elem"] * 3 + sw.dmap[b][fr["slot"]]
            out.append((p, cls, vel))
        return out

    def low_dmap(self, sw, live):
        """Re-map every sample whose plan has k < 3 slots so its slots use domains 0..k-1 (the larger
        slot to the more numerous of them); no-op when the map already does."""
        dcnt = (F.one_hot(sw.dom, 3).float() * live.float()[..., None]).sum(1)
        for b in range(sw.B):
            if int(sw.gplan[b]) < 0:
                continue
            T = self.targets[sn.KINDS[int(sw.gplan[b])]]
            k = sum(1 for c in T.slot_mix if c > 0)
            if k >= 3 or set(sw.dmap[b, :k].tolist()) == set(range(k)):
                continue
            sm = T.slot_mix[:k]
            big = int(torch.tensor(sm).argmax())
            dd = sorted(range(k), key=lambda d: -float(dcnt[b, d]))
            p = [0, 0, 0]
            order = sorted(range(k), key=lambda s_: -sm[s_])
            for s_, d in zip(order, dd):
                p[s_] = d
            p[2] = [d for d in range(3) if d not in p[:k]][0] if k == 2 else p[2]
            if k == 1:
                p = [0, 1, 2]
            sw.dmap[b] = torch.tensor(p)

    def spatial_dmap(self, sw, live, changed):
        """For samples whose plan just changed: the slot->domain perm maximising the overlap between
        each domain's actual density and its slot's wanted density, among perms that are within 10% of
        the count-optimal coverage (counts still matter: a domain cannot take a slot it cannot fill)."""
        cfg = self.cfg
        lf = live.float()
        cen = (sw.pos * lf[..., None]).sum(1) / lf.sum(1).clamp(min=1)[:, None]
        cen = torch.round(cen / cfg.cell) * cfg.cell
        D = self.field_fn(sw, cen, live)
        frame = hc.GridFrame(cen, cfg.G, cfg.cell)
        Ad = hc.blur(hc.splat(frame, sw.pos, F.one_hot(sw.dom, 3).float(), live), 1)      # [B,3,G^3]
        Ds = D[:, :hc.NCLS].reshape(sw.B, 4, 3, *D.shape[2:]).sum(1)                        # [B,3 slots,...]
        dcnt = (F.one_hot(sw.dom, 3).float() * lf[..., None]).sum(1)
        for b in changed:
            T = self.targets[sn.KINDS[int(sw.gplan[b])]]
            sh = torch.tensor(T.slot_mix + [0] * (3 - len(T.slot_mix)), dtype=torch.float)[:3]
            sh = sh / sh.sum(); n = float(dcnt[b].sum())
            cov = {p: sum(min(float(dcnt[b, p[s]]), float(sh[s]) * n) for s in range(3)) for p in hb.PERM3}
            best_cov = max(cov.values())
            best, arg = -1.0, None
            for p in hb.PERM3:
                if cov[p] < 0.9 * best_cov:
                    continue
                ov = sum(float(torch.minimum(Ad[b, p[s]], Ds[b, s]).sum()) for s in range(3))
                if ov > best + 1e-6:
                    best, arg = ov, p
            sw.dmap[b] = torch.tensor(arg)

    def step(self, sw, gen=None, train=False):
        cfg = self.cfg
        sw = hb.HSwarm.lift(sw)
        # the plan decision is taken HERE (the coarse step's own decide_plan is then a no-op: hyst = inf)
        if not hasattr(self, "_chg") or self._chg.shape[0] != sw.B:
            self._chg = torch.full((sw.B,), -10 ** 9, dtype=torch.long)
        self._chg[sw.clock == 0] = -10 ** 9
        live0 = sw.active & sw.hatched
        old_p, old_d = sw.gplan.clone(), sw.dmap.clone()
        hb.decide_plan(sw, live0, cfg, self.targets)
        for b in range(sw.B):
            if int(old_p[b]) >= 0 and int(sw.gplan[b]) != int(old_p[b]):
                if cfg.lock and int(sw.clock[b]) - int(self._chg[b]) < cfg.lock:
                    sw.gplan[b] = old_p[b]; sw.dmap[b] = old_d[b]
                else:
                    self._chg[b] = int(sw.clock[b])
        if cfg.dmap_low:
            self.low_dmap(sw, live0)
        if cfg.dmap_space and not bool((sw.clock == 0).all()):
            live0 = sw.active & sw.hatched
            old = sw.gplan.clone()
            hb.decide_plan(sw, live0, cfg, self.targets)
            changed = [b for b in range(sw.B) if int(old[b]) >= 0 and int(old[b]) != int(sw.gplan[b])]
            if changed:
                self.spatial_dmap(sw, live0, changed)
        pos0 = sw.pos.clone()
        h0 = cfg.hyst; cfg.hyst = 1e9
        try:
            out = super().step(sw, gen, train)
        finally:
            cfg.hyst = h0
        if not cfg.k_fine and not cfg.k_fine_tot:
            return out
        # The fine layer is applied as an extra displacement on top of the coarse step, on the tadpoles
        # that were live through it (eggs laid this step are untouched).
        live = (sw.active & sw.hatched & out.active & out.hatched)
        lf = live.float()
        disp = self.fine_disp(out, pos0, live)
        if cfg.vel_pre:
            prev = out.s[..., 15:18]
            disp = cfg.vel_pre * prev + (1 - cfg.vel_pre) * disp
            out.s[..., 15:18] = disp * lf[..., None]
        out.pos = out.pos + disp * lf[..., None]
        return out

    def _lay(self, sw, parents_ok, have, want, gd, gen):
        if not self.cfg.lay_major:
            return super()._lay(sw, parents_ok, have, want, gd, gen)
        # scale the relative deficit by how big the class is in the plan: rel' = rel x (want/max want)^a,
        # implemented by shrinking `have` toward 0 for small classes is wrong; instead scale p_lay per class
        w = (want / want.max(1, keepdim=True).values.clamp(min=1)).clamp(min=0) ** self.cfg.lay_major
        rel = ((want - have) / have.clamp(min=1.0)).clamp(0, 1) * w
        have2 = want - rel * have.clamp(min=1.0)                 # so the parent class recomputes rel' exactly
        return super()._lay(sw, parents_ok, have2, want, gd, gen)

    def fine_disp(self, out, pos0, live, k_scale=1.0):
        """The fine layer's displacement for every live tadpole of `out` (grid centre from pos0)."""
        cfg = self.cfg
        B, N, _ = out.pos.shape
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
            d = k_scale * k * g + cfg.k_fine_tot * gt
            if cfg.k_ff:
                wv = wt * same_t
                d = d + k_scale * cfg.k_ff * (wv @ fr) / wv.sum(1, keepdim=True).clamp(min=0.3)
            dn = d.norm(dim=-1, keepdim=True)
            vmax = torch.tensor(self.world.vmax)[out.elem[b, m]][:, None]
            d = d * (vmax / dn.clamp(min=1e-6)).clamp(max=1.0)
            disp[b, m] = d
        return disp


def make(**kw):
    cfg = Cfg(**{k: (type(getattr(Cfg, k))(v) if not isinstance(getattr(Cfg, k), str) else v) for k, v in kw.items()})
    return Boid2(sn.World(), cfg)
