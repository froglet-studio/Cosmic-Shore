"""lite_posinfo2 - posinfo2 (learned G2 rule + designed molting controller) made lighter.

A SUBCLASS of posinfo2_rule.PosInfo2Rule. Every lever is a flag, default OFF; with every flag off the step
is posinfo2's step, consuming the generator in the same order (bit-identical rollouts).

  prof        accumulate per-phase wall time in self.prof (perf_counter, ProfilerMarker-style).
  homeo_every m > 1: the composition controller (scaled quota + guarded molting + laying) runs every m-th
              step only. Its per-call rates (molt probability, the per-eligible laying draw) are scaled by m
              so births / molts PER UNIT TIME are unchanged; the per-call deficit caps already bound a
              burst. (lever b, amortised bookkeeping)
  frame_every m > 1: the body frame (centroid, principal axes, per-domain / per-element centroids, the
              census `glob`) is recomputed every m-th step; between, the cached frame-relative quantities
              are reused with each tadpole's live position. (lever b)
  fire_k      k > 1: FRACTIONAL UPDATE. posinfo2 already fires a random half of the swarm per step
              (fire_rate 0.5; a tadpole that does not fire does not move). fire_k = k fires a 1/k share of
              that half each step - round-robin by slot (slot % k == clock % k) - and runs the network
              ONLY on those rows. A fired tadpole's velocity is remembered; a non-firing tadpole COASTS on
              its last velocity times `coast` (default 0.5 = the expected fire rate, so the mean drift per
              unit time matches the base rule), then collision + membrane run on everyone. State channels
              are updated only on firing steps (their effect is per-update, so ds is scaled by k).
  half        run the MLP in bfloat16 (the C# analogue is float16 weights).

Cost accounting (multiply-adds per network evaluation): F*H + H*H + H*35 with F = 252, H = hidden.
"""
from __future__ import annotations

import time

import torch
import torch.nn.functional as F

import posinfo2_rule as p2
import posinfo_rule as pr
import swarm_nca as sn
from swarm_nca import Swarm, C, A, S_MAX, edges


class _T:
    def __init__(self, rule):
        self.r = rule
        self.t = time.perf_counter()

    def __call__(self, name):
        if self.r.prof is None:
            return
        t = time.perf_counter()
        self.r.prof[name] = self.r.prof.get(name, 0.0) + (t - self.t)
        self.t = t


class LitePosInfo2(p2.PosInfo2Rule):
    def __init__(self, world, homeo_every=1, frame_every=1, fire_k=1, coast=0.5, half=0, **kw):
        super().__init__(world, **kw)
        self.homeo_every, self.frame_every, self.fire_k, self.coast, self.half = homeo_every, frame_every, fire_k, coast, half
        self.prof = None
        self._vel = None
        self._frame = None
        self.net_rows = 0

    def mlp(self, f):
        if self.half:
            h = torch.relu(F.linear(f.bfloat16(), self.w1.bfloat16(), self.b1.bfloat16()))
            h = torch.relu(F.linear(h, self.w2.bfloat16(), self.b2.bfloat16())) + h
            out = F.linear(h, self.w3.bfloat16(), self.b3.bfloat16()).float()
        else:
            out = sn.SwarmRule.mlp(self, f)
        if self.no_die:
            out = out.clone(); out[:, sn.DIE] = 0.0
        return out

    # ---------------------------------------------------------- the body frame, optionally amortised ---
    @torch.no_grad()
    def _frame_feats(self, sw, clock):
        if self.frame_every <= 1 or self._frame is None or self._frame[0] != sw.pos.shape or clock % self.frame_every == 0:
            c, V, rms, ratio, m = pr.body_frame(sw)
            self._frame = (sw.pos.shape, c, V, rms, ratio)
        return self._frame[1:]

    @torch.no_grad()
    def posfeat_lite(self, sw, clock):
        B, N, _ = sw.pos.shape
        c, V, rms, ratio = self._frame_feats(sw, clock)
        m = (sw.active & sw.hatched).to(sw.pos.dtype)
        rel = ((sw.pos - c[:, None]) @ V.transpose(1, 2) / rms[:, None, None]).clamp(-4, 4)
        r = rel.norm(dim=-1, keepdim=True)
        shape = torch.cat([ratio, (rms / 20)[:, None]], 1)[:, None].expand(B, N, 3)
        doh = F.one_hot(sw.dom, 3).to(sw.pos.dtype) * m[:, :, None]
        dc = doh.sum(1)
        dsum = doh.transpose(1, 2) @ rel
        dmean = dsum / dc.clamp(min=1)[:, :, None]
        tot = dc.sum(1).clamp(min=1)
        own = torch.gather(dmean, 1, sw.dom[:, :, None].expand(B, N, 3))
        ownc = torch.gather(dc, 1, sw.dom)
        osum = dsum.sum(1, keepdim=True) - torch.gather(dsum, 1, sw.dom[:, :, None].expand(B, N, 3))
        oth = osum / (tot[:, None] - ownc).clamp(min=1)[:, :, None]
        eoh = F.one_hot(sw.elem, 4).to(sw.pos.dtype) * m[:, :, None]
        ec = eoh.sum(1)
        emean = (eoh.transpose(1, 2) @ rel) / ec.clamp(min=1)[:, :, None]
        eown = torch.gather(emean, 1, sw.elem[:, :, None].expand(B, N, 3))
        dshare = (ownc / tot[:, None])[:, :, None]
        return torch.cat([rel, rel.abs(), r, shape, own, oth, eown, dshare], -1).reshape(B * N, pr.P_BASE)

    # ------------------------------------------------------------------------------------- step ---
    def _step(self, sw: Swarm, gen=None, bud=True, fire=None):
        T = _T(self)
        W = self.world
        B, N, _ = sw.pos.shape
        n = B * N
        clock = int(sw.clock.reshape(-1)[0]) if torch.is_tensor(sw.clock) else int(sw.clock)
        pos, s = sw.pos.reshape(n, 3), sw.s.reshape(n, C)
        elem, dom, act = sw.elem.reshape(n), sw.dom.reshape(n), sw.active.reshape(n)
        hatched = sw.hatched.reshape(n)
        gi, gj = edges(sw, W.R)
        T("edges")
        x = torch.cat([s, F.one_hot(elem, 4).to(s.dtype), hatched[:, None].to(s.dtype)], 1)
        k = self.fire_k
        if k > 1:          # round-robin by slot: a 1/k share re-steers this step (no random draw)
            fire = act & ((torch.arange(n) % N) % k == clock % k)
        else:
            fire = act & ((torch.rand(n, generator=gen) <= self.fire_rate) if fire is None else fire.reshape(n))
        idx = fire.nonzero().squeeze(1)
        e = fire[gi]
        with torch.no_grad():
            hb = (sw.hatched & sw.active).float()
            cnt = hb.sum(1).clamp(min=1)
            mixe = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1) / cnt[:, None]
            glob = torch.cat([(cnt / 100)[:, None], mixe], 1)
            glob = glob[:, None, :].expand(B, N, self.G).reshape(n, self.G)
            pf = self.posfeat(sw, s, gi, gj) if self.frame_every <= 1 else self.posfeat_lite(sw, clock)
        T("frame")
        if k > 1:          # perceive only the firing rows (cost proportional to the update share)
            ge, je = gi[e], gj[e]
            sub = torch.full((n,), -1, dtype=torch.long); sub[idx] = torch.arange(len(idx))
            per = self._perceive_rows(pos, x, dom, ge, je, idx, sub)
            feats = torch.cat([per, glob.index_select(0, idx), pf.index_select(0, idx)], 1)
        else:
            feats = torch.cat([self.perceive(pos, x, dom, gi[e], gj[e], n), glob, pf], 1).index_select(0, idx)
        T("perceive")
        out = self.mlp(feats)
        self.net_rows += len(idx)
        T("mlp")
        vmax = torch.tensor(W.vmax)[elem].index_select(0, idx)[:, None]
        vf = vmax * torch.tanh(out[:, C:])
        if k > 1:
            if self._vel is None or self._vel.shape[0] != n:
                self._vel = torch.zeros(n, 3)
            self._vel = self._vel * act[:, None].to(pos.dtype)
            self._vel.index_copy_(0, idx, vf)
            v = self._vel * self.coast
            v = v * (~fire)[:, None].to(pos.dtype) + torch.zeros(n, 3).index_copy(0, idx, vf * self.coast * 1.0)
            # a firing tadpole moves by coast*vf this step (same mean as fire_rate 0.5 with full vf)
            ds = torch.zeros(n, C).index_copy(0, idx, out[:, :C] * (k * self.fire_rate))
        else:
            v = torch.zeros(n, 3).index_copy(0, idx, vf)
            ds = torch.zeros(n, C).index_copy(0, idx, out[:, :C])
        s = (s + ds).clamp(-S_MAX, S_MAX)
        pos = pos + v
        dxc = pos[gj] - pos[gi]
        r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - r).clamp(min=0) / W.r0
        pos = pos + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = pos.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        pos = pos - 0.5 * (rad - W.membrane).clamp(min=0) * pos / rad
        T("physics")
        with torch.no_grad():
            hat2 = hatched | (act & (s[:, A] > 0.1))
            died = act & hatched & (s[:, sn.DIE] > sn.DIE_AT)
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
        T("hatch")
        m = self.homeo_every
        if bud and (m <= 1 or clock % m == 0):
            if m > 1:
                pm, pb = self.p_molt, W.p_bud
                self.p_molt = min(1.0, pm * m)
                W.p_bud = min(1.0, pb * m)
                try:
                    (self.homeo_lay if self.homeo else self.lay)(out_sw, gi, gj, gen)
                finally:
                    self.p_molt, W.p_bud = pm, pb
            else:
                (self.homeo_lay if self.homeo else self.lay)(out_sw, gi, gj, gen)
        T("homeo")
        return out_sw

    def _perceive_rows(self, pos, x, dom, ge, je, idx, sub):
        """SwarmRule.perceive restricted to the firing rows: same math, scattered into len(idx) rows."""
        R = self.world.R
        nr = len(idx)
        gi_r = sub[ge]
        dx = pos[je] - pos[ge]
        q = (1 - (dx * dx).sum(-1) / (R * R)).clamp(min=0)
        w, g = q ** 3, q ** 2
        same = (dom[ge] == dom[je]).to(pos.dtype)
        X = self.X
        ws, wo = w * same, w * (1 - same)
        rs = torch.zeros(nr).index_add(0, gi_r, ws); ro = torch.zeros(nr).index_add(0, gi_r, wo)
        ms = torch.zeros(nr, X).index_add(0, gi_r, ws[:, None] * x[je]) / (1 + rs)[:, None]
        mo = torch.zeros(nr, X).index_add(0, gi_r, wo[:, None] * x[je]) / (1 + ro)[:, None]
        u = dx / R
        gs = torch.zeros(nr).index_add(0, gi_r, g)
        grad = torch.zeros(nr, 3, X).index_add(0, gi_r, (g[:, None, None] * u[:, :, None]) * (x[je] - x[ge])[:, None, :])
        grad = grad / (1 + gs)[:, None, None]
        gsame = torch.zeros(nr, 3).index_add(0, gi_r, g[:, None] * u * (same - 1)[:, None]) / (1 + gs)[:, None]
        rho = (rs + ro) / self.world.rho0
        return torch.cat([x.index_select(0, idx), ms, mo, grad.reshape(nr, 3 * X), gsame, rho[:, None],
                          (rs / self.world.rho0)[:, None]], 1)


def macs_per_eval(rule):
    H = rule.hidden
    return rule.w1.shape[1] * H + H * H + H * (C + 3)


def load(path, **flags):
    st = torch.load(path, weights_only=False, map_location=sn.DEVICE)
    w = dict(st["world"]); w["vmax"] = tuple(w["vmax"])
    rule = LitePosInfo2(sn.World(**w), hidden=st["hidden"], morph=st.get("morph", 0), morph_iters=st.get("morph_iters", 4),
                        homeo=st.get("homeo", 4), no_die=st.get("no_die", 1), p_molt=st.get("p_molt", 0.1), **flags)
    rule.load_state_dict(st["rule"])
    return rule


def from_spec(spec):
    """'path[?flag=v&flag=v]' -> a LitePosInfo2."""
    path, _, q = spec.partition("?")
    flags = {}
    for kv in filter(None, q.split("&")):
        k, v = kv.split("=")
        flags[k] = float(v) if k == "coast" else int(v)
    return load(path, **flags)
