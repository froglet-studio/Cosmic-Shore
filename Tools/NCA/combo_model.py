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
    ratio: int = 0              # molt toward the plan's element RATIOS in an overfull / orphan domain (see ratio_target)
    transfer2: int = 0          # (with orphan_proxy) overfull-class extras steer to their element's deficit in another region
    grow_scale: float = 0.0     # >0: a body holding more than the plan scales its fine targets by (n/plan)^(1/3) (cap 1+this)
    orphan_proxy: int = 0       # orphans steer by their element's best slot (see _proxy_dom)
    wscale_max: float = 1.0     # >1: an overfull domain's wanted fine density is scaled up (cap) so extras spread
    lay_cap: float = 0.0        # >0: no laying once live headcount >= lay_cap x plan headcount
    cache: int = 1              # (cost) exact cache of the coarse plan field per (plan, frame, cell-snapped centre)


class CachedOracle(hm.Oracle2):
    """Exact cache of the coarse plan field. With quant=1 the grid centre snaps to whole cells, and the
    field is a pure function of (plan, frame, centre) - so a swarm that holds still between frames reuses
    the same field instead of re-splatting ~80 channels of the plan every step. Bit-identical output."""

    def __init__(self, targets, cfg, size=256):
        super().__init__(targets, cfg)
        self.cache, self.size, self.hits, self.miss = {}, size, 0, 0

    def __call__(self, sw, centres, live, train=False):
        kinds, frames = self.frame_of(sw)
        outs = []
        for b, (k, f) in enumerate(zip(kinds, frames)):
            key = (k, f, tuple(round(float(x), 3) for x in centres[b]))
            g = self.cache.get(key)
            if g is None:
                self.miss += 1
                g = self.pf.field([k], [f], centres[b:b + 1])[0]
                if len(self.cache) >= self.size:
                    self.cache.pop(next(iter(self.cache)))
                self.cache[key] = g
            else:
                self.hits += 1
            outs.append(g)
        return torch.stack(outs)


class ComboBoid(hm.Boid2):
    def __init__(self, world, cfg: ComboCfg, targets=None):
        super().__init__(world, cfg, targets)
        if cfg.cache:
            self.field_fn = CachedOracle(self.targets, cfg)
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
        if cfg.ratio:
            want = self.ratio_target(want, have)
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

    @staticmethod
    def ratio_target(want, have):
        """Headcount is not a goal; the plan's element RATIOS are. A domain holding more tadpoles than its
        slot wants gets the slot's element mix scaled up to its own size; a domain the plan has no slot for
        gets the plan's overall element mix at its size. Below the slot's size the target is the plan's own
        count (growth fills it), so nothing molts during an ordinary grow."""
        B = want.shape[0]
        w = want.view(B, 4, 3); h = have.view(B, 4, 3)
        wd, hd = w.sum(1), h.sum(1)                                                            # [B,3]
        mix = w.sum(2); mix = mix / mix.sum(1, keepdim=True).clamp(min=1e-6)                  # [B,4]
        scale = (hd / wd.clamp(min=1e-6)).clamp(min=1.0)
        t = torch.where(wd[:, None, :] > 0.5, w * scale[:, None, :], mix[:, :, None] * hd[:, None, :])
        return t.reshape(B, 12)

    # --- transfer: a stranded surplus tadpole follows the outline only ------------------------------
    def _scale_of(self, out, live):
        """Per sample, per class: wanted-density multiplier = how overfull the class's domain is (>= 1)."""
        B = out.B
        cls = out.elem * 3 + out.dom
        have = torch.zeros(B, 12).scatter_add(1, cls, live.float())
        ws = torch.ones(B, 12)
        for b in range(B):
            if int(out.gplan[b]) < 0:
                continue
            fr = self.targets[sn.KINDS[int(out.gplan[b])]].frames[0]
            w = torch.bincount(fr["elem"] * 3 + out.dmap[b][fr["slot"]], minlength=12).float().view(4, 3)
            hd, wd = have[b].view(4, 3).sum(0), w.sum(0)
            sc = (hd / wd.clamp(min=1e-6)).clamp(min=1.0, max=self.cfg.wscale_max)
            ws[b] = sc[None, :].expand(4, 3).reshape(12)
        return ws

    def _fine_any(self, out, pos0, live, k_scale=1.0):
        if self.cfg.wscale_max > 1.0:
            self._wscale = self._scale_of(out, live)
            return self._fine_disp_w(out, pos0, live, k_scale)
        return hm.Boid2.fine_disp(self, out, pos0, live, k_scale)

    def _fine_disp_w(self, out, pos0, live, k_scale=1.0):
        """The fine layer's displacement for every live tadpole of `out` (grid centre from pos0)."""
        cfg = self.cfg
        B, N, _ = out.pos.shape
        lf = live.float()
        cen = (pos0 * lf[..., None]).sum(1) / lf.sum(1).clamp(min=1)[:, None]
        if cfg.quant:
            cen = torch.round(cen / cfg.cell) * cfg.cell
        tg = self.fine_targets(out, cen)
        disp = torch.zeros_like(out.pos)
        for b in range(B):
            m = live[b].nonzero().squeeze(1)
            if len(m) < 2:
                continue
            p, tcls, fr, sig = tg[b]
            s2 = 2 * sig ** 2
            x = out.pos[b, m]
            xc = out.elem[b, m] * 3 + out.dom[b, m]
            # wanted: bumps of the plan's units of the same class
            dt = x[:, None] - p[None]                               # [n,M,3]
            wt = torch.exp(-(dt * dt).sum(-1) / s2)                 # [n,M]
            same_t = (xc[:, None] == tcls[None]).float() * self._wscale[b][tcls][None]
            # actual: bumps of the live tadpoles of the same class (self excluded: zero gradient anyway)
            dx = x[:, None] - x[None]
            wx = torch.exp(-(dx * dx).sum(-1) / s2)
            same_x = (xc[:, None] == xc[None]).float()
            # grad of a bump at x from a centre c: -(x-c)/sigma^2 * w
            g_want = -(wt[..., None] * dt * same_t[..., None]).sum(1)
            g_have = -(wx[..., None] * dx * same_x[..., None]).sum(1)
            g = (g_want - g_have) / sig ** 2
            gt = (-(wt[..., None] * dt).sum(1) + (wx[..., None] * dx).sum(1)) / sig ** 2
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


    def fine_disp(self, out, pos0, live, k_scale=1.0):
        d = self._with_proxy(self._fine_any, out, pos0, live, k_scale)
        f = getattr(self, "_filler", None)
        if f is not None and f.shape == live.shape and bool(f.any()):
            # outline-only for fillers: the all-class fine gradient instead of the own-class one
            cfg = self.cfg
            k0, kt0 = cfg.k_fine, cfg.k_fine_tot
            cfg.k_fine, cfg.k_fine_tot = 0.0, max(k0, 1.0)
            try:
                dt = self._fine_any(out, pos0, live & f, k_scale)
            finally:
                cfg.k_fine, cfg.k_fine_tot = k0, kt0
            d = torch.where(f[..., None], dt, d)
        return d

    def fine_targets(self, sw, centres):
        tg = super().fine_targets(sw, centres)
        if not self.cfg.grow_scale:
            return tg
        live = sw.active & sw.hatched
        out = []
        for b, (p, cls, vel, sig) in enumerate(tg):
            n, m = float(live[b].sum()), float(len(p))
            k = max(1.0, n / max(m, 1.0)) ** (1 / 3)
            k = min(k, 1 + self.cfg.grow_scale)
            out.append((centres[b] + (p - centres[b]) * k, cls, vel * k, sig * k))
        return out

    def _proxy_dom(self, out, live):
        """Orphans (class the plan wants none of) borrow the domain whose slot wants their ELEMENT most,
        for steering only: they fill their element's places instead of drifting with no fine target."""
        B, N = out.elem.shape
        cls = out.elem * 3 + out.dom
        want = torch.zeros(B, 12)
        for b in range(B):
            if int(out.gplan[b]) < 0:
                continue
            fr = self.targets[sn.KINDS[int(out.gplan[b])]].frames[0]
            want[b] = torch.bincount(fr["elem"] * 3 + out.dmap[b][fr["slot"]], minlength=12).float()
        orphan = (torch.gather(want, 1, cls) < 0.5) & live
        we = want.view(B, 4, 3)
        best_d = we.argmax(2)                                                                   # [B,4]
        has = we.max(2).values > 0.5
        pd = torch.gather(best_d, 1, out.elem)
        ok = orphan & torch.gather(has, 1, out.elem)
        res = torch.where(ok, pd, out.dom)
        if self.cfg.transfer2:
            # region transfer: extras of an overfull class steer to their element's unfilled sites in another
            # domain's region (they keep their domain). Stable choice: the highest slot indices transfer.
            have = torch.zeros(B, 12).scatter_add(1, cls, live.float())
            for b in range(B):
                if int(out.gplan[b]) < 0:
                    continue
                h = have[b].view(4, 3).clone(); w = want[b].view(4, 3)
                for e in range(4):
                    for d in range(3):
                        ex = int(h[e, d] - w[e, d])
                        if ex <= 0 or w[e, d] < 0.5:
                            continue
                        idx = (live[b] & (out.elem[b] == e) & (out.dom[b] == d)).nonzero().squeeze(1)
                        for i in idx.flip(0)[:ex].tolist():
                            dd = int((w[e] - h[e]).argmax())
                            if float(w[e, dd] - h[e, dd]) < 0.5:
                                break
                            res[b, i] = dd; h[e, dd] += 1; h[e, d] -= 1
        return res

    def _with_proxy(self, fn, out, pos0, live, *a):
        if not self.cfg.orphan_proxy:
            return fn(out, pos0, live, *a)
        d0 = out.dom
        out.dom = self._proxy_dom(out, live)
        try:
            return fn(out, pos0, live, *a)
        finally:
            out.dom = d0

    def migrate(self, out, pos0, live):
        return self._with_proxy(super().migrate, out, pos0, live)

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
