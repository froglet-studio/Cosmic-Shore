"""Direction "evofate": GIVE THE EVOLVED RULE A FATE.

The evolved rule (results/evo: the trained G2 rule + a CMA-ES behaviour genome) has the organic motion
the lead liked but does not SORT: its outline is right, its elements and domains sit in the wrong
places (own-plan losses 15-25 against a bar of 8). Direction "sort" found the missing actuator: FATE -
a newborn commits to one positional-information well of its (element, region) type, the one its type
under-occupies, and steers by it. This model bolts exactly that onto the evolved rule:

  motion      G2's learned dynamics (state channels + velocity), unchanged weights. Its velocity is
              scaled by `mix` (1 = G2's own speed).
  fate        each hatched tadpole holds a sticky discrete fate = one well of its type in the current
              plan's code (sort_model.PlanCode, K wells per type). A stale fate (new tadpole, new plan,
              new region) is re-chosen by a census of under-occupied wells. A small DESIGNED steering
              term pulls it toward its well: pos += pull * clip(-k_well * grad E_fate).
  adhesion    optional differential adhesion between types (sort's Steinberg term), scaled by `adh`.
  look        0: G2 writes the visual channels (its own look). 1: after G2 the visual channels (facing,
              prism, tier, spindle) are set to the tadpole's type look (sort's code), the hidden
              channels stay G2's.
  composition DESIGNED (the known failure is learning it): sort's joint (element x region) lay homeostat
              with a headcount cap, cross-laying, and molting + region transfer for switches. G2's own
              laying is switched off. G2's learned death is suppressed (`no_death`): nothing dies on its
              own; only the yardstick's cull (players eating members) removes tadpoles.

All per-swarm memory (plan, role map, fates) lives on the model keyed by batch index and is reset when
a sample's clock is 0 (the yardstick convention). The hidden state channels belong to G2, so fates are
NOT stored in sw.s (G2 rewrites every channel every step).

model(sw, gen) -> Swarm; model.world is the G2 world.
"""
import math
import os
import sys
from dataclasses import dataclass, asdict

import numpy as np
import torch
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import evo_model as em  # noqa: E402
import sort_model as sm  # noqa: E402

A, DIE, C = sn.A, sn.DIE, sn.C
VIS = slice(1, 12)                       # facing, prism, tier, spindle
EVO_GENOME = os.path.join(HERE, "results", "evo", "genome.npy")
SORT_PARAMS = os.path.join(HERE, "results", "sort", "params.json")


@dataclass
class FateCfg:
    # fate steering
    pull: float = 1.0          # 0 = pure evo motion (no fate); scales the designed well step
    k_well: float = 0.41       # chemotaxis gain (sort's published value)
    well_clip: float = 0.52    # cap on the well step (voxels/step)
    mix: float = 1.0           # scale on G2's learned velocity
    e0: float = 0.0            # dead zone: no pull while the fated well's energy (0.5 Mahalanobis^2) is under e0, full by 2 e0
    f_inertia: float = 0.0     # momentum on the designed (fate + adhesion) move: v = f_inertia v + (1 - f_inertia) move
    adh: float = 0.0           # scale on sort's differential-adhesion force (0 = off)
    k_rep: float = 0.0         # extra collision (sort-style) on top of G2's own
    look: int = 1              # 1: visual channels = the type's look; 0: G2's own visuals
    look_mode: int = 0         # look=1: 0 type look, 1 the fated WELL's look
    swirl: int = 1             # keep the evo genome's per-element swirl
    no_death: int = 1          # suppress G2's learned death
    # code (sort's published K=12, per_well=4)
    K: int = 12
    per_well: int = 4
    cov_scale: float = 0.89
    # composition (sort's published values)
    comp: str = "sort"         # "sort" joint homeostat + molting | "evo" the evo genome's homeostat (G2 lay)
    dwell: int = 12
    lay_rate: float = 0.084
    lay_max: int = 5
    r_bud: float = 2.6
    fill_tol: float = 0.15
    p_cross: float = 0.466
    over: float = 0.94
    molt: int = 1
    molt_rate: float = 0.03
    transfer: int = 1
    role_every: int = 10
    seed: int = 0


GENES = [  # (name, lo, hi, log) CMA space -> cfg, 0 = base value
    ("pull", 0.05, 4.0, True), ("k_well", 0.05, 2.0, True), ("well_clip", 0.1, 3.0, True),
    ("mix", 0.0, 1.5, False), ("adh", 0.0, 2.0, False), ("k_rep", 0.0, 0.5, False),
    ("cov_scale", 0.3, 1.6, True), ("lay_rate", 0.02, 0.2, True), ("p_cross", 0.0, 1.0, False),
    ("over", 0.85, 1.15, False),
]


def cfg_from_vec(z, base=None):
    base = base or FateCfg()
    d = asdict(base)
    for (n, lo, hi, lg), v in zip(GENES, z):
        b = d[n]
        if lg:
            val = math.exp(math.log(max(b, 1e-6)) + 0.5 * v)
        else:
            val = b + 0.15 * (hi - lo) * v
        d[n] = float(min(hi, max(lo, val)))
    return FateCfg(**d)


class EvoFate(em.EvoRule):
    stateless = False

    def __init__(self, cfg: FateCfg | None = None, genome=None):
        super().__init__(np.load(EVO_GENOME) if genome is None else genome)
        self.cfg = cfg or FateCfg()
        scfg = sm.SortCfg(K=self.cfg.K, per_well=self.cfg.per_well, cov_scale=self.cfg.cov_scale)
        self.codes = {k: sm.PlanCode(T, scfg) for k, T in sn.load_targets().items()}
        self.sort_adh = None
        try:
            import json
            p = json.load(open(SORT_PARAMS))["cfg"]
            self.sort_adh = dict(R_adh=p["R_adh"], a_same=p["a_same"], a_elem=p["a_elem"], a_role=p["a_role"],
                                 a_other=p["a_other"], r0=p["r0"])
        except Exception:
            self.sort_adh = dict(R_adh=4.5, a_same=0.04, a_elem=0.0, a_role=0.02, a_other=-0.02, r0=2.4)
        self.mem = {}
        self.predators = []

    # ------------------------------------------------------------ memory
    def _reset(self, b, sw):
        N = sw.pos.shape[1]
        al = (sw.active[b] & sw.hatched[b]).numpy()
        c = np.bincount(sw.elem[b].numpy()[al], minlength=4)
        kind = sn.PLAN_OF[int(np.argmax(c))]
        self.mem[b] = dict(plan=kind, cand=kind, cand_n=0, perm=None, t=0,
                           fate=np.full(N, -1), fkey=np.zeros(N, int), troll=np.full(N, -1), fv=np.zeros((N, 3)),
                           rng=np.random.default_rng(self.cfg.seed * 1000 + b + 17 * int(sw.elem[b].sum())))

    def _pick_perm(self, code, elem, dom, old):
        return sm.SortSwarm._pick_perm(None, code, elem, dom, old)

    def _eff_role(self, m, idx, dom, role_of_dom):
        r = role_of_dom[dom[idx]].copy()
        pid = sn.KINDS.index(m["plan"])
        h = m["troll"][idx]
        ok = (h >= 0) & (h // 4 == pid)
        r[ok] = h[ok] % 4
        return r

    def _plan_and_roles(self, b, sw):
        cfg, m = self.cfg, self.mem[b]
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy()
        idx = np.nonzero(act & hat)[0]
        contested = False
        if len(idx):
            counts = np.bincount(elem[idx], minlength=4)
            cur = sn.MAJOR[m["plan"]]; top = int(np.argmax(counts))
            maj = cur if counts[cur] == counts[top] else top
            if maj != cur:
                cand = sn.PLAN_OF[maj]
                m["cand_n"] = m["cand_n"] + 1 if m["cand"] == cand else 1
                m["cand"] = cand
                if m["cand_n"] >= cfg.dwell:
                    m["plan"], m["perm"], m["cand_n"] = cand, None, 0
                else:
                    contested = True
            else:
                m["cand"], m["cand_n"] = m["plan"], 0
        code = self.codes[m["plan"]]
        if len(idx) and (m["perm"] is None or m["t"] % cfg.role_every == 0):
            m["perm"] = self._pick_perm(code, elem[idx], dom[idx], m["perm"])
        if m["perm"] is None:
            m["perm"] = np.arange(code.nslots)
        role_of_dom = np.full(3, -1)
        for s_, d_ in enumerate(m["perm"]):
            role_of_dom[d_] = s_
        m["t"] += 1
        return code, role_of_dom, contested

    # ------------------------------------------------------------ G2 step (no death, scaled velocity)
    def _g2(self, sw, gen):
        W, cfg = self.world, self.cfg
        B, N, _ = sw.pos.shape
        n = B * N
        pos, s = sw.pos.reshape(n, 3), sw.s.reshape(n, C)
        elem, dom, act = sw.elem.reshape(n), sw.dom.reshape(n), sw.active.reshape(n)
        hatched = sw.hatched.reshape(n)
        gi, gj = sn.edges(sw, W.R)
        x = torch.cat([s, F.one_hot(elem, 4).to(s.dtype), hatched[:, None].to(s.dtype)], 1)
        fire = act & (torch.rand(n, generator=gen) <= self.fire_rate)
        idx = fire.nonzero().squeeze(1)
        e = fire[gi]
        hb = (sw.hatched & sw.active).float()
        cnt = hb.sum(1).clamp(min=1)
        mixe = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1) / cnt[:, None]
        glob = torch.cat([(cnt / 100)[:, None], mixe], 1)[:, None, :].expand(B, N, self.G).reshape(n, self.G)
        feats = torch.cat([self.perceive(pos, x, dom, gi[e], gj[e], n), glob], 1).index_select(0, idx)
        out = self.mlp(feats)
        ds = torch.zeros(n, C).index_copy(0, idx, out[:, :C])
        vmax = torch.tensor(W.vmax)[elem].index_select(0, idx)[:, None]
        v = torch.zeros(n, 3).index_copy(0, idx, cfg.mix * vmax * torch.tanh(out[:, C:]))
        s = (s + ds).clamp(-sn.S_MAX, sn.S_MAX)
        if cfg.no_death:
            s[:, DIE] = s[:, DIE].clamp(max=sn.DIE_AT - 0.5)
        pos = pos + v
        dxc = pos[gj] - pos[gi]
        r = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - r).clamp(min=0) / W.r0
        pos = pos + torch.zeros(n, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / r[:, None])
        rad = pos.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        pos = pos - 0.5 * (rad - W.membrane).clamp(min=0) * pos / rad
        hat2 = hatched | (act & (s[:, A] > 0.1))
        died = act & hatched & (s[:, DIE] > sn.DIE_AT)
        alive = hat2 & ~died
        near = alive.float().clone().scatter_reduce(0, gi, alive[gj].float(), reduce="amax", include_self=True) > 0
        age = sw.age.reshape(n) + (act & ~hat2).long()
        gone_egg = act & ~hat2 & (~near | (age > W.egg_life))
        keep = act & ~died & ~gone_egg
        new_hatched = hat2 & keep
        deaths = sw.deaths + died.view(B, N).sum(1)
        s = s * keep[:, None].to(s.dtype)
        out_sw = sn.Swarm(pos.view(B, N, 3), s.view(B, N, C), sw.elem.clone(), sw.dom.clone(), keep.view(B, N),
                          new_hatched.view(B, N), deaths, sw.mutants.clone(), (age * keep.long()).view(B, N), sw.clock + 1,
                          sw.plan.clone(), sw.since + 1, sw.bw * keep.view(B, N).to(sw.bw.dtype))
        return out_sw, gi, gj

    # ------------------------------------------------------------ the step
    @torch.no_grad()
    def forward(self, sw, gen=None, bud=True, fire=None):
        cfg = self.cfg
        for b in range(sw.B):
            if int(sw.clock[b]) == 0 or b not in self.mem:
                self._reset(b, sw)
        if self.locked is None or self.locked.shape[0] != sw.B:
            self.locked = torch.full((sw.B,), -1, dtype=torch.long)
        self.locked[sw.clock == 0] = -1
        self._update_lock(sw)
        ctx = [self._plan_and_roles(b, sw) for b in range(sw.B)]
        out, gi, gj = self._g2(sw, gen)
        if cfg.swirl and em.gene(self.genome, "sw_swirl") > 0:
            rate = 0.05 * torch.tanh(torch.tensor(self.genome[em.SLICES["swirl"]], dtype=torch.float32))
            if float(rate.abs().max()) > 1e-4:
                live = out.active & out.hatched
                w = live.float()
                cen = (w[:, :, None] * out.pos).sum(1) / w.sum(1).clamp(min=1)[:, None]
                ax = em._AXES[self.locked.clamp(min=0)][:, None].expand_as(out.pos)
                om = rate[out.elem][..., None]
                out.pos = out.pos + live[..., None] * om * torch.cross(ax, out.pos - cen[:, None], dim=-1)
        for b in range(out.B):
            code, role_of_dom, contested = ctx[b]
            self._fate_step(out, b, code, role_of_dom)
            if cfg.comp == "sort" and bud and not contested:
                self._lay_sort(out, b, code, role_of_dom)
                if cfg.molt:
                    self._molt(out, b, code, role_of_dom)
        if cfg.comp == "evo" and bud:
            em.EvoRule.lay(self, out, gi, gj, gen)
        return out

    def _fate_step(self, sw, b, code, role_of_dom):
        cfg, m = self.cfg, self.mem[b]
        rng = m["rng"]
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        idx = np.nonzero(act & hat)[0]
        if len(idx) == 0:
            return
        pos = sw.pos[b].numpy(); S = sw.s[b].numpy()
        elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy()
        role = self._eff_role(m, idx, dom, role_of_dom)
        c0 = pos[idx].mean(0)
        xb = pos[idx] - c0
        G = np.zeros((len(idx), 3)); E = np.zeros(len(idx))
        pid = sn.KINDS.index(m["plan"])
        fate, fkey = m["fate"], m["fkey"]
        for e in range(4):
            for s in range(3):
                sel = (elem[idx] == e) & (role == s)
                if not sel.any() or (e, s) not in code.wells:
                    continue
                js = idx[sel]
                key = 1 + pid * 16 + e * 4 + s
                stale = (fate[js] < 0) | (fkey[js] != key)
                if stale.any():
                    w = code.wells[(e, s)][0]
                    occ = np.bincount(fate[js[~stale]], minlength=len(w)).astype(float)
                    need = w * sel.sum() - occ
                    for j in js[stale]:
                        f = int(np.argmax(need + 1e-3 * rng.random(len(w))))
                        fate[j] = f; fkey[j] = key; need[f] -= 1
                E[sel], G[sel] = code.energy_grad_fate(xb[sel], e, s, fate[js])
        orph = np.array([(elem[i], r) not in code.wells for i, r in zip(idx, role)])
        if orph.any():
            Eb = np.full(orph.sum(), np.inf); Gb = np.zeros((orph.sum(), 3))
            for key in code.wells:
                e2, g2 = code.energy_grad(xb[orph], *key)
                better = e2 < Eb
                Eb[better], Gb[better] = e2[better], g2[better]
            G[orph] = Gb; E[orph] = Eb
        step = -cfg.k_well * G
        if cfg.e0 > 0:
            step = step * np.clip((E - cfg.e0) / cfg.e0, 0, 1)[:, None]
        nrm = np.linalg.norm(step, axis=1, keepdims=True)
        step = step * np.minimum(1, cfg.well_clip / np.maximum(nrm, 1e-9))
        move = cfg.pull * step
        if cfg.adh > 0 or cfg.k_rep > 0:
            a = self.sort_adh
            P = pos[idx]
            dx = P[None] - P[:, None]
            d = np.linalg.norm(dx, axis=-1) + np.eye(len(idx)) * 1e3
            if cfg.k_rep > 0:
                rep = np.clip(a["r0"] - d, 0, None) / d
                move += -cfg.k_rep * (rep[..., None] * dx).sum(1)
            if cfg.adh > 0:
                tid = elem[idx] * 4 + (role + 1)
                same_t = tid[:, None] == tid[None]
                same_e = elem[idx][:, None] == elem[idx][None]
                same_r = role[:, None] == role[None]
                Aij = np.where(same_t, a["a_same"], np.where(same_e, a["a_elem"], np.where(same_r, a["a_role"], a["a_other"])))
                near = (d < a["R_adh"]) & (d > a["r0"] * 0.9)
                move += cfg.adh * ((Aij * near / d)[..., None] * dx).sum(1)
        if cfg.f_inertia > 0:
            fv = m["fv"]
            move = cfg.f_inertia * fv[idx] + (1 - cfg.f_inertia) * move
            fv[idx] = move
        pos[idx] += move
        if cfg.look:
            for i, (e, r) in enumerate(zip(elem[idx], role)):
                key = (int(e), int(r))
                st = code.state.get(key)
                j = idx[i]
                if cfg.look_mode and st is not None and fate[j] >= 0:
                    st = code.wstate[key][fate[j]]
                if st is None:
                    for s2 in range(3):
                        st = code.state.get((int(e), s2))
                        if st is not None:
                            break
                if st is None:
                    continue
                S[j, VIS] = st[VIS]

    # ------------------------------------------------------------ composition (sort's, fates on the model)
    def _census(self, sw, b, role_of_dom, include_eggs=True):
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        sel = act if include_eggs else act & hat
        idx = np.nonzero(sel)[0]
        el = sw.elem[b].numpy()[sel]
        r = self._eff_role(self.mem[b], idx, sw.dom[b].numpy(), role_of_dom)
        cen = np.zeros((4, 4), int)
        np.add.at(cen, (el, np.where(r < 0, 3, r)), 1)
        return cen

    def _lay_sort(self, sw, b, code, role_of_dom):
        cfg, m = self.cfg, self.mem[b]
        rng = m["rng"]
        cen = self._census(sw, b, role_of_dom)
        want = np.zeros((4, 4)); want[:, :3] = np.ceil(code.counts * cfg.over)
        deficit = want - cen
        if deficit[:, :3].clip(min=0).sum() <= 0:
            return
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        idx = np.nonzero(act & hat)[0]
        free = list(np.nonzero(~act)[0])
        room = int(math.ceil(code.n * cfg.over)) - int(act.sum())
        fill = np.where(want > 0, cen / np.maximum(want, 1), 9.0)
        nlay = min(cfg.lay_max, len(free), room, int(rng.poisson(max(cfg.lay_rate * len(idx), 0.2))))
        if nlay <= 0:
            return
        elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy(); pos = sw.pos[b].numpy(); S = sw.s[b].numpy()
        ec = np.bincount(elem[act], minlength=4)
        maj = sn.MAJOR[code.kind]
        laid = 0
        for i in rng.permutation(idx):
            if laid >= nlay or not free:
                break
            r = role_of_dom[dom[i]]
            if r < 0:
                continue
            e = int(elem[i])
            low = int(np.argmin(fill[:, r]))
            if deficit[e, r] > 0 and fill[e, r] <= fill[low, r] + cfg.fill_tol:
                ce = e
            elif rng.random() < cfg.p_cross and deficit[low, r] > 0:
                ce = low
            else:
                continue
            if ce != maj and ec[ce] + 1 >= ec[maj]:
                if rng.random() < cfg.p_cross and deficit[maj, r] > 0:
                    ce = maj
                else:
                    continue
            ec[ce] += 1
            j = free.pop(0)
            dirn = rng.normal(size=3); dirn /= max(np.linalg.norm(dirn), 1e-6)
            pos[j] = pos[i] + cfg.r_bud * dirn
            S[j] = 0.0; S[j, A] = 0.2
            elem[j] = ce; dom[j] = dom[i]
            sw.active[b, j] = True; sw.hatched[b, j] = False; sw.age[b, j] = 0
            m["fate"][j] = -1; m["fkey"][j] = 0; m["troll"][j] = -1; m["fv"][j] = 0
            deficit[ce, r] -= 1; cen[ce, r] += 1
            fill[ce, r] = cen[ce, r] / max(want[ce, r], 1)
            laid += 1

    def _molt(self, sw, b, code, role_of_dom):
        cfg, m = self.cfg, self.mem[b]
        rng = m["rng"]
        cen = self._census(sw, b, role_of_dom, include_eggs=False)
        want = np.zeros((4, 4)); want[:, :3] = code.counts
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy()
        idx = np.nonzero(act & hat)[0]
        roles = self._eff_role(m, idx, dom, role_of_dom)
        pid = sn.KINDS.index(m["plan"])
        order = rng.permutation(len(idx))
        for i, r in zip(idx[order], roles[order]):
            if rng.random() > cfg.molt_rate:
                continue
            e = int(elem[i]); rc = 3 if r < 0 else int(r)
            if cen[e, rc] <= want[e, rc]:
                continue
            deficit = want - cen
            if r >= 0 and deficit[:, r].max() > 0:
                r2 = int(r)
            elif cfg.transfer and deficit[:, :3].max() > 0:
                r2 = int(np.unravel_index(np.argmax(deficit[:, :3]), (4, 3))[1])
                m["troll"][i] = 4 * pid + r2
            else:
                continue
            fl = np.where(want[:, r2] > 0, cen[:, r2] / np.maximum(want[:, r2], 1), 9.0)
            fl[deficit[:, r2] <= 0] = 9.0
            ne = int(np.argmin(fl))
            ec = np.bincount(elem[idx], minlength=4); maj = sn.MAJOR[code.kind]
            if ne != maj and ec[ne] + 1 >= ec[maj]:
                continue
            elem[i] = ne
            m["fate"][i] = -1
            cen[e, rc] -= 1; cen[ne, r2] += 1


def load(path):
    import json
    d = json.load(open(path))
    return EvoFate(FateCfg(**d["cfg"]))
