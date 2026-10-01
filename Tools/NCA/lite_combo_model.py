"""LITE COMBO: the held combo (G8) with cost-cutting flags, each default OFF so every one is measured alone
against the hold (results/hold/combo.json).

  frac_k     FRACTIONAL UPDATE (the game's old trick). Each step only the tadpoles with slot % frac_k ==
             step % frac_k recompute the FINE layer (the O(n x M) plan-unit pull + O(n^2) same-class push
             and the migrant search, the dominant per-step cost); every other live tadpole re-applies its
             last fine displacement (coasts). The displacement is a per-step velocity, so the dynamics per
             unit time are unchanged; nothing is multiplied by k. The coarse grid step, collisions, laying and
             the molt clock still run for everybody every step (they are O(grid) / O(N) and cheap).
  molt_every BOOKKEEPING AMORTISED: the molt controller (census, ratio target, quota loop) runs every m steps
             with its clock rate scaled by m, so the molt rate per unit time is unchanged.
  fine_ease  SMOOTHING: the applied fine displacement is a low-pass of the computed one,
             d = ease * d_new + (1 - ease) * d_prev (1 = off). Addresses the brief's lurch finding (the
             corrector snapping to a new target right after a cull) and hides the fractional update's
             staleness.
  vec        VECTORISED census: the per-sample `want` bincount loop is replaced by a cached per-(plan, dmap)
             table, shared by the molt, the proxy and the transfer2 code paths (pure bookkeeping, bit-exact).

    model = lite_combo_model.load("results/lite_combo/params.json")
"""
from __future__ import annotations

import json
from dataclasses import dataclass

import torch

import swarm_nca as sn
import combo_model as cm


@dataclass
class LiteCfg(cm.ComboCfg):
    frac_k: int = 1
    molt_every: int = 1
    fine_ease: float = 1.0
    vec: int = 0
    coast: float = 1.0      # a coasting tadpole re-applies coast x its last fine displacement (decays while stale)
    mig_frac: int = 0       # also restrict the migrant search to this step's share (off: everyone, every step)


class LiteComboBoid(cm.ComboBoid):
    def __init__(self, world, cfg: LiteCfg, targets=None):
        super().__init__(world, cfg, targets)
        self._t = 0
        self._dcache = None     # [B,N,3] last fine displacement per tadpole
        self._want_tab = {}

    # ---- bookkeeping -----------------------------------------------------------------------------
    def _want(self, out):
        B = out.B
        want = torch.zeros(B, 12)
        for b in range(B):
            g = int(out.gplan[b])
            if g < 0:
                continue
            key = (g, tuple(out.dmap[b].tolist()))
            w = self._want_tab.get(key)
            if w is None:
                fr = self.targets[sn.KINDS[g]].frames[0]
                w = torch.bincount(fr["elem"] * 3 + out.dmap[b][fr["slot"]], minlength=12).float()
                self._want_tab[key] = w
            want[b] = w
        return want

    def starve_staggered(self, out, ch=sn.DIE):
        m = max(1, self.cfg.molt_every)
        if m == 1:
            return super().starve_staggered(out, ch)
        if self._t % m:
            self._filler = None
            return
        r0 = self.cfg.molt_rate
        self.cfg.molt_rate = r0 * m
        try:
            super().starve_staggered(out, ch)
        finally:
            self.cfg.molt_rate = r0

    # ---- fractional fine layer --------------------------------------------------------------------
    def fine_disp(self, out, pos0, live, k_scale=1.0):
        cfg = self.cfg
        k = max(1, cfg.frac_k)
        B, N, _ = out.pos.shape
        if self._dcache is None or self._dcache.shape != out.pos.shape:
            self._dcache = torch.zeros_like(out.pos)
            fresh = live
        else:
            fresh = live & (self._dcache.abs().sum(-1) == 0)      # newly hatched / never computed
        if k > 1:
            phase = (torch.arange(N) % k) == (self._t % k)
            sel = live & (phase[None] | fresh)
            self._sel = sel
            dnew = super().fine_disp(out, pos0, sel, k_scale) if bool(sel.any()) else torch.zeros_like(out.pos)
            # the fine field's centre and the "settle" gain see only the selected subset; restore them from the
            # whole live body: recompute with the full live set for the centre but rows only for sel is what
            # _fine_disp_sel does
        else:
            sel = live
            self._sel = sel
            dnew = super().fine_disp(out, pos0, live, k_scale)
        e = cfg.fine_ease
        prev = self._dcache
        if e < 1.0:
            blend = e * dnew + (1 - e) * prev
            d = torch.where((sel & ~fresh)[..., None], blend, torch.where(sel[..., None], dnew, prev))
        else:
            d = torch.where(sel[..., None], dnew, prev)
        d = d * live[..., None].float()
        self._dcache = d.detach().clone()
        return d

    def step(self, sw, gen=None, train=False):
        if bool((sw.clock == 0).all()):
            self._dcache, self._t = None, 0          # a new rollout: forget the last one's coasting state
        out = super().step(sw, gen, train)
        self._t += 1
        return out


class LiteComboBoidSub(LiteComboBoid):
    """frac_k with the subset computed against the WHOLE live body (centre, settle gain and the same-class
    push see everyone; only the rows of the selected tadpoles are evaluated). This is the faithful
    fractional update; LiteComboBoid's simple version is kept for the ablation."""

    def _fine_rows(self, out, pos0, live, rows, k_scale):
        cfg = self.cfg
        B, N, _ = out.pos.shape
        lf = live.float()
        cen = self.fine_centre(pos0, lf)
        tg = self.fine_targets(out, cen)
        disp = torch.zeros_like(out.pos)
        vm = torch.tensor(self.world.vmax)
        for b in range(B):
            m = live[b].nonzero().squeeze(1)
            r = rows[b].nonzero().squeeze(1)
            if len(m) < 2 or len(r) == 0:
                continue
            p, tcls, fr, sig = tg[b]
            s2 = 2 * sig ** 2
            xall = out.pos[b, m]
            xcall = out.elem[b, m] * 3 + out.dom[b, m]
            x = out.pos[b, r]
            xc = out.elem[b, r] * 3 + out.dom[b, r]
            dt = x[:, None] - p[None]
            wt = torch.exp(-(dt * dt).sum(-1) / s2)
            same_t = (xc[:, None] == tcls[None]).float()
            dx = x[:, None] - xall[None]
            wx = torch.exp(-(dx * dx).sum(-1) / s2)
            same_x = (xc[:, None] == xcall[None]).float()
            g_want = -(wt[..., None] * dt * same_t[..., None]).sum(1)
            g_have = -(wx[..., None] * dx * same_x[..., None]).sum(1)
            g = (g_want - g_have) / sig ** 2
            kf = cfg.k_fine
            if cfg.settle:
                kf = kf * max(0.0, min(1.0, float(len(m)) / float(len(p))))
            d = k_scale * kf * g
            if cfg.k_fine_tot:
                gt = (-(wt[..., None] * dt).sum(1) + (wx[..., None] * dx).sum(1)) / sig ** 2
                d = d + cfg.k_fine_tot * gt
            if cfg.k_ff:
                wv = wt * same_t
                d = d + k_scale * cfg.k_ff * (wv @ fr) / wv.sum(1, keepdim=True).clamp(min=0.3)
            dn = d.norm(dim=-1, keepdim=True)
            vmax = vm[out.elem[b, r]][:, None]
            disp[b, r] = d * (vmax / dn.clamp(min=1e-6)).clamp(max=1.0)
        return disp

    def fine_disp(self, out, pos0, live, k_scale=1.0):
        cfg = self.cfg
        k = max(1, cfg.frac_k)
        if k == 1 or cfg.wscale_max > 1.0 or cfg.transfer:
            return super().fine_disp(out, pos0, live, k_scale)
        B, N, _ = out.pos.shape
        if self._dcache is None or self._dcache.shape != out.pos.shape:
            self._dcache = torch.zeros_like(out.pos)
        fresh = live & (self._dcache.abs().sum(-1) == 0)
        phase = (torch.arange(N) % k) == (self._t % k)
        sel = live & (phase[None] | fresh)
        self._sel = sel
        dnew = self._with_proxy(lambda o, p0, lv, ks: self._fine_rows(o, p0, lv, sel, ks), out, pos0, live, k_scale)
        prev = self._dcache
        e = cfg.fine_ease
        if e < 1.0:
            d = torch.where((sel & ~fresh)[..., None], e * dnew + (1 - e) * prev, torch.where(sel[..., None], dnew, prev))
        else:
            d = torch.where(sel[..., None], dnew, cfg.coast * prev)
        d = d * live[..., None].float()
        self._dcache = d.detach().clone()
        return d

    def migrate(self, out, pos0, live):
        k = max(1, self.cfg.frac_k)
        if k == 1 or not self.cfg.mig_frac:
            return super().migrate(out, pos0, live)
        # migrants are re-chosen only for this step's share; the search is O(k_mig x M), cheap, but the
        # O(n x M) own_want sweep is the cost, so restrict rows to the selected share
        sel = getattr(self, "_sel", live)
        return super().migrate(out, pos0, sel)


def load(path):
    d = json.load(open(path))
    cls = LiteComboBoidSub if d.get("model", "").endswith("Sub") else LiteComboBoid
    return cls(sn.World(), LiteCfg(**d["cfg"]))


def make(sub=True, **kw):
    cfg = LiteCfg(**kw)
    return (LiteComboBoidSub if sub else LiteComboBoid)(sn.World(), cfg)
