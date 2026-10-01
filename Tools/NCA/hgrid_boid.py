"""HGRID designed boid: a collision automaton whose every behaviour comes from a grid field.

The grid (see hgrid_core) is a coarse 3D field riding on the swarm. Its channels say, per cell, how
many tadpoles of each (element, slot) class SHOULD be there, and what they should look like. The
boid rule is designed and short:

  * MOVE   up the gradient of its own class's DEFICIT (wanted density - actual density, both on the
           grid), plus a weaker pull from the all-class deficit, plus designed collision; velocity
           has persistence (hidden state 12-14), a little noise, and each element's top speed.
  * LAY    a hatched tadpole lays an egg (its own domain; its own element, except a share `p_cross`
           that takes the element with the largest deficit there) into its class's deficit, with a
           probability proportional to that deficit. A class with no deficit does not breed - this
           is what steers the element MIX after a cull, with no element ever told to die.
  * LOOK   prism / Charge tier / facing / spindle are eased toward the field's attribute channels.
  * STARVE (optional) a tadpole whose class is in surplus in the whole swarm AND sits in an
           overfull cell accumulates hunger on its death channel and withers to a crystal.

Where the field comes from is the hierarchy's upper level, and is pluggable (`field_fn`):

  * OracleField (this file): a DESIGNED morphogen. The plan is the majority element's (with an
    optional hysteresis margin); the field is that plan's rasterised target, placed at the swarm's
    centroid. This is the upper bound of the design: what the boid layer can do with a perfect grid.
  * hgrid_nca.LearnedField: a learned 3D NCA that must PRODUCE that field from the splatted swarm.

Plan and slot->domain map live on the swarm (HSwarm), so pools, rollouts and probes carry them.
"""
from __future__ import annotations

import itertools
from dataclasses import dataclass, asdict

import torch
import torch.nn.functional as F

import swarm_nca as sn
import hgrid_core as hc

PERM3 = list(itertools.permutations(range(3)))


class HSwarm(sn.Swarm):
    """A Swarm plus the grid layer's per-sample state: gplan [B] (plan index the grid expresses),
    dmap [B,3] (slot -> domain), grid [B,K,G,G,G] (learned field state; None for the oracle)."""

    XF = ("gplan", "dmap")

    def __init__(self, *a, gplan=None, dmap=None, grid=None, gcen=None):
        super().__init__(*a)
        B = self.pos.shape[0]
        self.gplan = gplan if gplan is not None else torch.full((B,), -1, dtype=torch.long)
        self.dmap = dmap if dmap is not None else torch.arange(3).repeat(B, 1)
        self.grid = grid
        self.gcen = gcen

    @staticmethod
    def lift(sw):
        if isinstance(sw, HSwarm):
            return sw
        return HSwarm(*[getattr(sw, a) for a in sn.Swarm.FIELDS])

    def _mk(self, base, f):
        return HSwarm(*[getattr(base, a) for a in sn.Swarm.FIELDS], gplan=f(self.gplan), dmap=f(self.dmap),
                      grid=None if self.grid is None else f(self.grid), gcen=None if self.gcen is None else f(self.gcen))

    def detach(self):
        return self._mk(sn.Swarm.detach(self), lambda x: x.detach() if x.dtype.is_floating_point else x)

    def clone(self):
        return self._mk(sn.Swarm.clone(self), lambda x: x.clone())

    def index(self, idx):
        return self._mk(sn.Swarm.index(self, idx), lambda x: x[idx])

    @staticmethod
    def cat(xs):
        xs = [HSwarm.lift(x) for x in xs]
        base = sn.Swarm.cat(xs)
        g = None if any(x.grid is None for x in xs) else torch.cat([x.grid for x in xs])
        c = None if any(x.gcen is None for x in xs) else torch.cat([x.gcen for x in xs])
        return HSwarm(*[getattr(base, a) for a in sn.Swarm.FIELDS], gplan=torch.cat([x.gplan for x in xs]),
                      dmap=torch.cat([x.dmap for x in xs]), grid=g, gcen=c)


@dataclass
class BoidCfg:
    G: int = 16
    cell: float = 6.0
    quant: int = 1             # the grid snaps to whole cells (a learned grid keeps its state when the swarm drifts)
    k_class: float = 3.0       # gain on the own-class deficit gradient
    k_total: float = 1.0       # gain on the all-class deficit gradient
    k_home: float = 0.15       # a tadpole outside every wanted cell drifts toward the centroid
    persist: float = 0.6       # velocity persistence
    noise: float = 0.05
    p_lay: float = 0.1         # laying probability at full growth pressure
    p_cross: float = 0.25      # share of eggs that take the most-wanted element of the parent's domain (domain breeds true)
    hatch_steps: int = 2
    ease: float = 0.3          # look easing per step
    starve: int = 1
    starve_rate: float = 0.04
    starve_tol: float = 0.15   # a class is in surplus when the swarm holds this much more of it than the plan
    starve_local: float = 0.0  # ... and the tadpole sits where its class is not wanted (local class deficit below this)
    hyst: float = 0.0          # the plan switches when another element leads the current plan's by > hyst * n
    animate: int = 1           # cycle the target's 8 frames (period steps each)
    period: int = 8


def domain_map(counts, shares):
    """counts [3] (live per domain), shares [3] (plan slot shares) -> slot->domain perm maximising
    sum_s min(count[perm[s]], share[s] * n). Ties keep the identity (first in PERM3)."""
    n = float(counts.sum())
    best, arg = -1.0, PERM3[0]
    for p in PERM3:
        v = sum(min(float(counts[p[s]]), float(shares[s]) * n) for s in range(3))
        if v > best + 1e-6:
            best, arg = v, p
    return torch.tensor(arg)


class OracleField:
    """The designed upper level: majority element (with hysteresis) -> that plan's target field."""

    def __init__(self, targets, cfg: BoidCfg):
        self.targets, self.cfg = targets, cfg
        self.pf = hc.PlanFields(targets, cfg.G, cfg.cell)

    def __call__(self, sw: HSwarm, centres, live, train=False):
        cfg = self.cfg
        kinds = [sn.KINDS[int(g)] for g in sw.gplan]
        frames = [int(c // cfg.period) % len(self.targets[k].frames) if cfg.animate else 0 for c, k in zip(sw.clock, kinds)]
        return self.pf.field(kinds, frames, centres)


def decide_plan(sw: HSwarm, live, cfg: BoidCfg, targets):
    """In place: each sample's grid plan = its majority element's plan, switching only when another
    element leads the current plan's element by more than hyst x headcount; remap domains on change."""
    B = sw.B
    oh = F.one_hot(sw.elem, 4).float() * live[..., None].float()
    cnt = oh.sum(1)                                            # [B,4]
    dcnt = (F.one_hot(sw.dom, 3).float() * live[..., None].float()).sum(1)
    for b in range(B):
        n = float(cnt[b].sum())
        if n == 0:
            continue
        maj = int(cnt[b].argmax())
        cur = int(sw.gplan[b])
        if cur < 0:
            new = sn.KINDS.index(sn.PLAN_OF[maj])
        else:
            ce = sn.MAJOR[sn.KINDS[cur]]
            new = sn.KINDS.index(sn.PLAN_OF[maj]) if float(cnt[b, maj]) > float(cnt[b, ce]) + cfg.hyst * n else cur
        if new != cur:
            sw.gplan[b] = new
            T = targets[sn.KINDS[new]]
            sh = torch.tensor(T.slot_mix + [0] * (3 - len(T.slot_mix)), dtype=torch.float)[:3]
            sw.dmap[b] = domain_map(dcnt[b], sh / sh.sum())
    return cnt


class FieldBoid:
    """model(sw, gen) -> HSwarm. `field_fn(sw, centres, live) -> [B, FIELD_C, G,G,G]`."""

    def __init__(self, world: sn.World, cfg: BoidCfg, field_fn, targets=None):
        self.world, self.cfg, self.field_fn = world, cfg, field_fn
        self.targets = targets or sn.load_targets()
        self.last_field = None

    def __call__(self, sw, gen=None):
        return self.step(sw, gen)

    def step(self, sw, gen=None, train=False):
        W, cfg = self.world, self.cfg
        sw = HSwarm.lift(sw)
        if bool((sw.clock == 0).all()) and not train:
            sw.gplan = torch.full((sw.B,), -1, dtype=torch.long)
        B, N, _ = sw.pos.shape
        live = sw.active & sw.hatched
        lf = live.float()
        cnt_all = lf.sum(1).clamp(min=1)
        centres = (sw.pos * lf[..., None]).sum(1) / cnt_all[:, None]
        centres = centres.detach()
        if cfg.quant:
            centres = torch.round(centres / cfg.cell) * cfg.cell
        decide_plan(sw, live, cfg, self.targets)
        frame = hc.GridFrame(centres, cfg.G, cfg.cell)
        D = self.field_fn(sw, centres, live)                                      # [B, FIELD_C, G^3]
        self.last_field = D
        # wanted density per (elem, slot) re-indexed to (elem, domain) through the slot->domain map
        Dc = D[:, :hc.NCLS].reshape(B, 4, 3, *D.shape[2:])
        inv = torch.zeros(B, 3, dtype=torch.long)                                 # domain -> slot
        for b in range(B):
            for s in range(3):
                inv[b, sw.dmap[b, s]] = s
        Dd = torch.stack([Dc[b][:, inv[b]] for b in range(B)])                    # [B,4,3(dom),G,G,G]
        Dd = Dd.reshape(B, 12, *D.shape[2:])
        cls = sw.elem * 3 + sw.dom                                                # [B,N]
        Av = F.one_hot(cls, 12).float()
        A = hc.blur(hc.splat(frame, sw.pos, Av, live), 1)
        Def = Dd - A                                                              # class deficit
        Dtot, Atot = Dd.sum(1, keepdim=True), A.sum(1, keepdim=True)
        defs = torch.cat([Def, Dtot - Atot, Dtot], 1)                            # 14
        gdef = hc.grad(frame, defs[:, :13])                                       # [B,13,3,G^3]
        samp = hc.sample(frame, torch.cat([defs, gdef.flatten(1, 2)], 1), sw.pos)   # [B,N,14+39]
        ar = torch.arange(B)[:, None]
        d_own = torch.gather(samp[..., :12], 2, cls[..., None])[..., 0]
        d_tot = samp[..., 12]; want_tot = samp[..., 13]
        gd = samp[..., 14:].reshape(B, N, 13, 3)
        g_own = torch.gather(gd, 2, cls[..., None, None].expand(B, N, 1, 3))[:, :, 0]
        g_tot = gd[:, :, 12]
        # a class the plan has no room for anywhere follows the all-class field only
        wanted_cls = Dd.flatten(2).sum(-1)                                        # [B,12]
        niche = torch.gather(wanted_cls, 1, cls) > 0.5
        v = cfg.k_class * g_own * niche[..., None].float() + cfg.k_total * g_tot
        cvec = centres[:, None, :] - sw.pos
        home = (want_tot < 0.05).float()[..., None] * cfg.k_home * cvec / cvec.norm(dim=-1, keepdim=True).clamp(min=1)
        v = v + home
        if cfg.noise:
            v = v + cfg.noise * torch.randn(v.shape, generator=gen)
        s = sw.s.clone()
        vel = cfg.persist * s[..., 12:15] + (1 - cfg.persist) * v
        vmax = torch.tensor(W.vmax)[sw.elem][..., None]
        sp = vel.norm(dim=-1, keepdim=True)
        vel = vel * (vmax / sp.clamp(min=1e-6)).clamp(max=1.0)
        pos = sw.pos + vel * lf[..., None]
        s[..., 12:15] = vel * lf[..., None]
        # collision (designed, as swarm_nca)
        n = B * N
        gi, gj = sn.edges(sw, W.R)
        P = pos.reshape(n, 3)
        dxc = P[gj] - P[gi]
        r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - r).clamp(min=0) / W.r0
        P = P + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = P.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        P = P - 0.5 * (rad - W.membrane).clamp(min=0) * P / rad
        pos = P.view(B, N, 3)
        # look: ease toward the field's attributes for the tadpole's element
        rows = hc.sample(frame, D, pos)                                           # [B,N,FIELD_C]
        pr, tl, fa, spn = hc.attr_at(rows.reshape(n, -1), sw.elem.reshape(n))
        tgt = torch.cat([pr, tl, 5 * fa, spn], 1).view(B, N, 11)
        cur = torch.cat([s[..., sn.PR], s[..., sn.TI], s[..., sn.FAC], s[..., sn.SP]], -1)
        cur = cur + cfg.ease * (tgt - cur) * lf[..., None]
        s[..., sn.PR], s[..., sn.TI], s[..., sn.FAC], s[..., sn.SP] = cur[..., :3], cur[..., 3:6], cur[..., 6:9], cur[..., 9:11]
        # hatching (eggs bloom in over hatch_steps)
        egg = sw.active & ~sw.hatched
        age = sw.age + egg.long()
        s[..., sn.A] = torch.where(egg, (age.float() / cfg.hatch_steps).clamp(max=1.0) * 0.1,
                                   torch.where(sw.active, torch.ones_like(s[..., sn.A]), s[..., sn.A]))
        hatched = sw.hatched | (egg & (age >= cfg.hatch_steps))
        age = age * (sw.active & ~hatched).long()
        # starvation: surplus class (whole swarm) in an overfull cell
        deaths = sw.deaths.clone()
        active = sw.active.clone()
        if cfg.starve:
            have = torch.zeros(B, 12).scatter_add(1, cls, lf)
            surplus = have > (1 + cfg.starve_tol) * wanted_cls + 1.0                  # [B,12]
            sur_i = torch.gather(surplus, 1, cls) & live & (d_own < self.cfg.starve_local)
            s[..., sn.DIE] = torch.where(sur_i, s[..., sn.DIE] + cfg.starve_rate, (s[..., sn.DIE] - cfg.starve_rate).clamp(min=0))
            died = live & (s[..., sn.DIE] > sn.DIE_AT)
            deaths = deaths + died.sum(1)
            active = active & ~died
            hatched = hatched & ~died
            s = s * (~died)[..., None].float()
        out = HSwarm(pos, s, sw.elem.clone(), sw.dom.clone(), active, hatched, deaths, sw.mutants.clone(), age,
                     sw.clock + 1, sw.plan.clone(), sw.since + 1, sw.bw, gplan=sw.gplan.clone(), dmap=sw.dmap.clone(),
                     grid=sw.grid, gcen=sw.gcen)
        have = torch.zeros(B, 12).scatter_add(1, cls, lf)
        self._lay(out, live & active, have, wanted_cls, gd, gen)
        return out

    @torch.no_grad()
    def _lay(self, sw: HSwarm, parents_ok, have, want, gd, gen):
        """Growth pressure per class = its relative deficit in the whole swarm, (want - have) / have,
        read off the grid (want = the field's integral). A class with room breeds true; a share
        p_cross of births (and every birth from a class with no room) takes the class of the parent's
        own domain with the largest relative deficit - so a surplus element can still feed the plan."""
        W, cfg = self.world, self.cfg
        B, N, _ = sw.pos.shape
        rel = ((want - have) / have.clamp(min=1.0)).clamp(0, 1)                 # [B,12]
        cls = sw.elem * 3 + sw.dom
        own = torch.gather(rel, 1, cls)                                         # [B,N]
        rel_d = rel.view(B, 4, 3)                                               # elem x domain
        best_rel, best_e = rel_d.max(1)                                         # per domain [B,3]
        alt = torch.gather(best_rel, 1, sw.dom)
        alt_e = torch.gather(best_e, 1, sw.dom)
        p = cfg.p_lay * torch.maximum(own, cfg.p_cross * alt)
        ok = parents_ok & (torch.rand(B, N, generator=gen) < p)
        cross_coin = torch.rand(B, N, generator=gen) < cfg.p_cross
        child_e = torch.where((own <= 0) | (cross_coin & (alt > own)), alt_e, sw.elem)
        for b in range(B):
            par = ok[b].nonzero().squeeze(1)
            free = (~sw.active[b]).nonzero().squeeze(1)
            k = min(len(par), len(free))
            if k == 0:
                continue
            par = par[torch.randperm(len(par), generator=gen)[:k]]
            slots = free[:k]
            e = child_e[b, par]
            d = sw.dom[b, par]
            g = gd[b, par, e * 3 + d]                                           # the egg's class deficit gradient
            g = g / g.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            nz = torch.randn(k, 3, generator=gen)
            dirn = g + 0.7 * nz / nz.norm(dim=-1, keepdim=True)
            dirn = dirn / dirn.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            sw.pos[b, slots] = sw.pos[b, par] + W.r_bud * dirn
            sw.s[b, slots] = 0.0
            sw.elem[b, slots] = e
            sw.dom[b, slots] = d
            sw.active[b, slots] = True
            sw.hatched[b, slots] = False
            sw.age[b, slots] = 0


def make_oracle(cfg=None, world=None):
    cfg = cfg or BoidCfg()
    world = world or sn.World()
    targets = sn.load_targets()
    return FieldBoid(world, cfg, OracleField(targets, cfg), targets)
