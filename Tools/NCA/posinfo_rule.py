"""PosInfoRule - the learned tadpole rule (swarm_nca.SwarmRule) with BODY-FRAME POSITIONAL INFORMATION.

G2 senses neighbours only, so it can grow an outline but cannot know WHERE inside the body it sits;
the diagnosis (runs/diag_terms.py) showed the outline is fine (pos-only loss 2.6-4.5) and the gap to
the 8 bar is SORTING - which element and which domain sits where. This rule appends, per tadpole,
a block of positional inputs (Wolpert's positional information, read off the swarm's own shape):

  q        position relative to the swarm centroid, in the swarm's principal-axis frame, / RMS radius
           (axes sorted by variance; each axis sign chosen so the third moment along it is positive,
           i.e. the long tail points +; a mirror-symmetric axis has a noisy sign, so |q| is given too)
  |q|      sign-free copy (for the symmetric axes)
  r        |q| norm
  shape    the frame's two variance ratios (s2/s1, s3/s1) and rms/20 (size)
  dom_off  the centroid of MY domain minus the swarm centroid, same frame, / rms  (domain-neutral: a
           tadpole sees where its own team has gathered, never which team it is)
  oth_off  the centroid of the OTHER domains, same frame
  elem_off the centroid of MY element, same frame
  dshare   my domain's share of the swarm
  morph    MORPH diffused morphogen channels read at my position (optional, `morph` > 0): the swarm
           secretes hidden channels 12..12+MORPH, which diffuse over the particle graph (a local field)

All of it is computed WITHOUT gradient each step (like the population signals G2 already gets).
The new input columns of w1 are zero-initialised, so at step 0 the rule equals G2 exactly.

It is STATELESS (the frame is recomputed every step from positions), so swarm_eval batches it.
"""
from __future__ import annotations

import torch
import torch.nn.functional as F

import swarm_nca as sn
from swarm_nca import Swarm, C, A, S_MAX, edges

P_BASE = 3 + 3 + 1 + 3 + 3 + 3 + 3 + 1     # q, |q|, r, shape(2)+size, dom_off, oth_off, elem_off, dshare


@torch.no_grad()
def body_frame(sw: Swarm):
    """Per sample: centroid c [B,3], axes V [B,3,3] (rows = axes, sign-fixed), rms [B], sv ratios [B,2]."""
    B, N, _ = sw.pos.shape
    m = (sw.active & sw.hatched).to(sw.pos.dtype)                     # [B,N]
    cnt = m.sum(1).clamp(min=1)
    c = (m[:, :, None] * sw.pos).sum(1) / cnt[:, None]
    d = (sw.pos - c[:, None]) * m[:, :, None]
    cov = d.transpose(1, 2) @ d / cnt[:, None, None]                  # [B,3,3]
    cov = cov + 1e-4 * torch.eye(3)
    ev, vec = torch.linalg.eigh(cov)                                  # ascending
    ev, vec = ev.flip(-1), vec.flip(-1)                               # descending; columns = axes
    V = vec.transpose(1, 2)                                           # rows = axes
    q = d @ V.transpose(1, 2)                                         # [B,N,3]
    skew = (m[:, :, None] * q ** 3).sum(1)                            # [B,3]
    sgn = torch.where(skew >= 0, 1.0, -1.0)
    V = V * sgn[:, :, None]
    rms = ev.clamp(min=0).sum(-1).sqrt().clamp(min=1.0)
    ratio = (ev[:, 1:] / ev[:, :1].clamp(min=1e-6)).clamp(0, 1)
    return c, V, rms, ratio, m


class PosInfoRule(sn.SwarmRule):
    stateless = True

    def __init__(self, world: sn.World, hidden=192, fire_rate=0.5, morph=0, morph_iters=4):
        self.morph, self.morph_iters = morph, morph_iters
        super().__init__(world, hidden=hidden, fire_rate=fire_rate)
        P = P_BASE + morph
        self.P = P
        old = self.w1.data
        self.w1 = torch.nn.Parameter(torch.cat([old, torch.zeros(hidden, P)], 1))
        self.F = old.shape[1] + P

    # --------------------------------------------------------------- features ---
    @torch.no_grad()
    def posfeat(self, sw: Swarm, s, gi, gj):
        B, N, _ = sw.pos.shape
        c, V, rms, ratio, m = body_frame(sw)
        rel = (sw.pos - c[:, None]) @ V.transpose(1, 2) / rms[:, None, None]    # [B,N,3]
        rel = rel.clamp(-4, 4)
        r = rel.norm(dim=-1, keepdim=True)
        shape = torch.cat([ratio, (rms / 20)[:, None]], 1)[:, None].expand(B, N, 3)
        # per-domain / per-element centroids, in frame
        doh = F.one_hot(sw.dom, 3).to(sw.pos.dtype) * m[:, :, None]          # [B,N,3]
        dc = doh.sum(1)                                                       # [B,3]
        dsum = doh.transpose(1, 2) @ rel                                      # [B,3dom,3]
        dmean = dsum / dc.clamp(min=1)[:, :, None]
        tot = dc.sum(1).clamp(min=1)
        own = torch.gather(dmean, 1, sw.dom[:, :, None].expand(B, N, 3))      # [B,N,3]
        ownc = torch.gather(dc, 1, sw.dom)                                    # [B,N]
        osum = dsum.sum(1, keepdim=True) - torch.gather(dsum, 1, sw.dom[:, :, None].expand(B, N, 3))
        oth = osum / (tot[:, None] - ownc).clamp(min=1)[:, :, None]
        eoh = F.one_hot(sw.elem, 4).to(sw.pos.dtype) * m[:, :, None]
        ec = eoh.sum(1)
        emean = (eoh.transpose(1, 2) @ rel) / ec.clamp(min=1)[:, :, None]
        eown = torch.gather(emean, 1, sw.elem[:, :, None].expand(B, N, 3))
        dshare = (ownc / tot[:, None])[:, :, None]
        feats = [rel, rel.abs(), r, shape, own, oth, eown, dshare]
        return torch.cat(feats, -1).reshape(B * N, P_BASE)

    def diffuse(self, u, gi, gj, sw):
        """A local morphogen field: secreted hidden channels averaged over the neighbour graph a few
        times (each pass reaches one perception radius further)."""
        n = u.shape[0]
        alive = (sw.active & sw.hatched).reshape(n).to(u.dtype)[:, None].detach()
        u = torch.tanh(u) * alive
        w = alive[gj, 0]
        deg = torch.zeros(n).index_add(0, gi, w) + 1
        for _ in range(self.morph_iters):
            u = (u + torch.zeros(n, u.shape[1]).index_add(0, gi, w[:, None] * u[gj])) / deg[:, None] * alive
        return u

    # ------------------------------------------------------------------- step ---
    def _step(self, sw: Swarm, gen=None, bud=True, fire=None):
        W = self.world
        B, N, _ = sw.pos.shape
        n = B * N
        pos, s = sw.pos.reshape(n, 3), sw.s.reshape(n, C)
        elem, dom, act = sw.elem.reshape(n), sw.dom.reshape(n), sw.active.reshape(n)
        hatched = sw.hatched.reshape(n)
        gi, gj = edges(sw, W.R)
        x = torch.cat([s, F.one_hot(elem, 4).to(s.dtype), hatched[:, None].to(s.dtype)], 1)
        fire = act & ((torch.rand(n, generator=gen) <= self.fire_rate) if fire is None else fire.reshape(n))
        idx = fire.nonzero().squeeze(1)
        e = fire[gi]
        with torch.no_grad():
            hb = (sw.hatched & sw.active).float()
            cnt = hb.sum(1).clamp(min=1)
            mixe = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1) / cnt[:, None]
            glob = torch.cat([(cnt / 100)[:, None], mixe], 1)
            glob = glob[:, None, :].expand(B, N, self.G).reshape(n, self.G)
            pf = self.posfeat(sw, s, gi, gj)
        if self.morph:                     # the morphogen field carries a gradient back to its secretion
            pf = torch.cat([pf, self.diffuse(s[:, 12:12 + self.morph], gi, gj, sw)], 1)
        feats = torch.cat([self.perceive(pos, x, dom, gi[e], gj[e], n), glob, pf], 1).index_select(0, idx)
        out = self.mlp(feats)
        ds = torch.zeros(n, C).index_copy(0, idx, out[:, :C])
        vmax = torch.tensor(W.vmax)[elem].index_select(0, idx)[:, None]
        v = torch.zeros(n, 3).index_copy(0, idx, vmax * torch.tanh(out[:, C:]))
        s = (s + ds).clamp(-S_MAX, S_MAX)
        pos = pos + v
        dxc = pos[gj] - pos[gi]
        r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - r).clamp(min=0) / W.r0
        pos = pos + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = pos.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        pos = pos - 0.5 * (rad - W.membrane).clamp(min=0) * pos / rad
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
        if bud:
            self.lay(out_sw, gi, gj, gen)
        return out_sw


def from_g2(path, morph=0, **kw):
    """Warm start: G2's weights, the positional columns at zero (step 0 == G2)."""
    st = torch.load(path, weights_only=False, map_location=sn.DEVICE)
    w = st["world"]; w["vmax"] = tuple(w["vmax"])
    rule = PosInfoRule(sn.World(**w), hidden=st["hidden"], morph=morph, **kw)
    sd = dict(st["rule"])
    v = rule.w1.data.clone(); v.zero_(); v[:, :sd["w1"].shape[1]] = sd["w1"]; sd["w1"] = v
    rule.load_state_dict(sd)
    return rule


def save(rule, path, step=0, extra=None):
    torch.save(dict(rule={k: v.cpu() for k, v in rule.state_dict().items()}, world=sn.asdict(rule.world),
                    hidden=rule.hidden, morph=rule.morph, morph_iters=rule.morph_iters, step=step, **(extra or {})), path)


def load(path):
    st = torch.load(path, weights_only=False, map_location=sn.DEVICE)
    w = st["world"]; w["vmax"] = tuple(w["vmax"])
    rule = PosInfoRule(sn.World(**w), hidden=st["hidden"], morph=st.get("morph", 0), morph_iters=st.get("morph_iters", 4))
    rule.load_state_dict(st["rule"])
    return rule


if __name__ == "__main__":
    torch.set_num_threads(4)
    T = sn.load_targets()
    g2 = sn.load_rule("results/swarm_coevo_g2/rule.pt")
    pr = from_g2("results/swarm_coevo_g2/rule.pt", morph=4)
    out = []
    for m in (g2, pr):
        gen = sn.make_gen(3); sw = sn.seed_swarm([T[k] for k in sn.KINDS], m.world, gen)
        with torch.no_grad():
            for _ in range(60):
                sw = m(sw, gen)
        out.append(sw)
    print("step-equivalence max |dpos|", float((out[0].pos - out[1].pos).abs().max()),
          "same alive", bool(torch.equal(out[0].active, out[1].active)))
