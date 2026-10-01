"""Direction "sort": EMERGENT CELL SORTING for the tadpole swarm.

No tadpole is ever assigned a place. Each one knows only:
  * its own TYPE = (element, domain ROLE). A role is which body REGION its domain plays in the
    current plan; the swarm picks the domain -> role mapping from its own census (the scorer's
    slot -> domain permutation is free, so the regions are what matter, not which team is which).
  * a positional-information code for its type in the current plan: a small mixture of Gaussian
    "morphogen wells" in body coordinates (Wolpert's French flag, a few wells per type instead of
    one flag). K = 1 well per type is the pure flag; more wells sharpen the shape.
  * its NEIGHBOURS (within R): collision, a (type x type) differential-adhesion matrix
    (Steinberg: same-type pull, other-type tension) and Potts-style neighbour SWAPS - two touching
    tadpoles that would each sit better in the other's spot slide past one another.
  * the body's centre (where the code's origin is) - by default the swarm centroid; with
    `local_centre=1` every tadpole carries its own estimate, relaxed toward its neighbours'
    estimates (a consensus/diffusion morphogen: no global quantity at all).

Composition reuses the evo idea of a lay HOMEOSTAT (production gating only): a hatched parent lays
when its own (element, role) class is short of the plan's count; a parent whose own class is full
may (share `p_cross`) lay an egg of its OWN DOMAIN but of the element that domain's region lacks
most - "a bit of reproduction of different elements here and there". Domain always breeds true.
Optional relaxed behaviour `molt` (off for the own-plan tier): a tadpole of a SURPLUS class
re-forms its crystal into a deficit element of its own domain (lateral-inhibition flavoured: only
when its own type crowds its neighbourhood). Nothing dies on a clock.

model(sw, gen) -> Swarm, model.world = swarm_nca.World(); per-sample memory on the model, reset at
sw.clock == 0 (the yardstick convention). Parameters: SortCfg (a flat genome view: GENES).
"""
import math
import os
import sys
from dataclasses import dataclass, field, asdict, fields

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
from field_swarm import invert_state  # noqa: E402  (pure helper: attributes -> state channels)

A, DIE = sn.A, sn.DIE
H_ROLE = 12          # hidden channels used by this model
H_EST = slice(13, 16)  # local_centre: the tadpole's own estimate of the body centre
H_VEL = slice(16, 19)  # velocity (inertia)
H_AGE = 19           # steps since birth (grow-in)
H_FATE = 20          # 1 + the well (within its type) this tadpole committed to; 0 = uncommitted
H_FKEY = 21          # the (plan, type) key its fate was taken under: a new plan or role re-fates


@dataclass
class SortCfg:
    # positional information
    K: int = 4                 # max wells per type (1 = a pure French flag per type)
    per_well: int = 6          # a type gets min(K, n_type // per_well) wells (>= 1)
    cov_scale: float = 0.8     # well width multiplier (x the type's own spread)
    k_well: float = 0.25       # chemotaxis gain up the own-type morphogen (log-density gradient)
    well_clip: float = 1.5     # cap on the chemotaxis step
    # neighbours
    r0: float = 2.4            # collision spacing
    k_rep: float = 0.35
    R_adh: float = 4.5         # adhesion radius
    a_same: float = 0.04       # adhesion: same type
    a_elem: float = 0.0        # same element, other role
    a_role: float = 0.02       # same role (domain region), other element
    a_other: float = -0.02     # neither (negative: interfacial tension)
    swap: float = 0.5          # Potts swaps: gain toward the partner's spot (0 = off)
    r_swap: float = 3.6
    swap_margin: float = 0.3
    # motion
    inertia: float = 0.5
    vmax: tuple = (0.8, 0.8, 0.8, 2.0)
    local_centre: int = 0      # 1: centre from neighbour consensus, no swarm-wide average
    centre_relax: float = 0.04  # leak toward own position (small: the estimate is mostly the neighbours')
    centre_iters: int = 3       # consensus rounds per step (a tadpole only talks to neighbours each round)
    lap_every: int = 0          # >0: every this many steps the runners' fates advance one well along their
    lap_clip: float = 2.0       # chemotaxis cap for lap runners (Time's top speed)
    lap_elems: tuple = (3,)     #     type's loop (Time runs laps; occupancy - and so the score - is unchanged)
    fate: int = 1              # 1: each tadpole commits to ONE well of its type (the most under-occupied at birth)
    swirl_time: float = 0.0    # Time runners circulate (rad/step about the body's major axis); look only
    # composition
    dwell: int = 12            # steps a new majority must hold before the plan switches
    lay_rate: float = 0.06     # eggs per step as a share of the headcount
    lay_max: int = 5
    r_bud: float = 2.6
    fill_tol: float = 0.15     # a parent breeds true only while its class is within this of its region's least-filled class
    p_cross: float = 0.5       # a full-class parent lays its domain's most-needed element instead
    hatch_steps: int = 3
    over: float = 1.0          # lay up to over x the plan's class count
    molt: int = 0              # relaxed: surplus tadpoles re-form into a deficit element of their domain
    molt_rate: float = 0.03
    transfer: int = 1          # (with molt) an overflow/orphan tadpole may join another region
    role_every: int = 10       # re-pick the domain -> role mapping every this many steps (sticky)
    seed: int = 0


GENES = [  # (name, lo, hi, log-scale) - the CMA-ES search space (unit cube -> cfg)
    ("cov_scale", 0.3, 1.6, True), ("k_well", 0.05, 1.0, True), ("well_clip", 0.3, 3.0, True),
    ("r0", 2.0, 2.9, False), ("k_rep", 0.1, 0.8, True),
    ("a_same", -0.05, 0.15, False), ("a_elem", -0.1, 0.1, False), ("a_role", -0.1, 0.1, False),
    ("a_other", -0.15, 0.05, False), ("R_adh", 3.0, 7.0, False),
    ("swap", 0.0, 1.5, False), ("r_swap", 2.6, 5.0, False), ("swap_margin", 0.0, 2.0, False),
    ("inertia", 0.0, 0.85, False), ("lay_rate", 0.02, 0.15, True), ("p_cross", 0.0, 1.0, False),
    ("over", 0.9, 1.15, False),
]


def cfg_from_vec(z, base: SortCfg | None = None):
    """z in R^len(GENES) (CMA space, 0 = base value) -> SortCfg."""
    base = base or SortCfg()
    d = asdict(base)
    for (n, lo, hi, lg), v in zip(GENES, z):
        b = d[n]
        if lg:
            val = math.exp(math.log(max(b, 1e-6)) + 0.5 * v)
        else:
            val = b + 0.15 * (hi - lo) * v
        d[n] = float(min(hi, max(lo, val)))
    d["vmax"] = tuple(d["vmax"])
    return SortCfg(**d)


# ------------------------------------------------------------------ the code

def _kmeans(P, K, rng, iters=40):
    c = P[rng.choice(len(P), K, replace=False)]
    for _ in range(iters):
        a = ((P[:, None] - c[None]) ** 2).sum(-1).argmin(1)
        c = np.array([P[a == k].mean(0) if (a == k).any() else c[k] for k in range(K)])
    return a, c


class PlanCode:
    """One plan's positional-information code: per (element, slot) type, counts, wells, look."""

    def __init__(self, T: sn.Target, cfg: SortCfg):
        rng = np.random.default_rng(1234)
        fr = T.frames[0]
        P = fr["p"].numpy().astype(np.float64)
        P = P - P.mean(0)
        el, sl = fr["elem"].numpy(), fr["slot"].numpy()
        self.kind, self.n, self.nslots = T.kind, len(P), T.slots
        self.counts = np.zeros((4, 3), int)
        self.wells = {}            # (e, s) -> (w [K], mu [K,3], inv [K,3,3], logdet [K])
        self.state = {}            # (e, s) -> state vector (look)
        self.next = {}
        for e in range(4):
            for s in range(3):
                m = (el == e) & (sl == s)
                n = int(m.sum())
                self.counts[e, s] = n
                if n == 0:
                    continue
                Q = P[m]
                K = max(1, min(cfg.K, n // max(1, cfg.per_well)))
                a, _ = _kmeans(Q, K, rng) if K > 1 else (np.zeros(n, int), None)
                w, mu, inv, ld = [], [], [], []
                for k in range(K):
                    R = Q[a == k]
                    if len(R) == 0:
                        continue
                    cov = (np.cov(R.T) if len(R) > 2 else np.zeros((3, 3))) * cfg.cov_scale + 1.0 * np.eye(3)
                    w.append(len(R) / n); mu.append(R.mean(0)); inv.append(np.linalg.inv(cov))
                    ld.append(float(np.linalg.slogdet(cov)[1]))
                self.wells[(e, s)] = (np.array(w), np.array(mu), np.array(inv), np.array(ld))
                # a loop through the wells (greedy nearest-neighbour tour): next well along it
                M = np.array(mu); order = [0]; left = set(range(1, len(M)))
                while left:
                    j = min(left, key=lambda q: np.linalg.norm(M[q] - M[order[-1]])); order.append(j); left.remove(j)
                nxt = np.zeros(len(M), int)
                for a_, b_ in zip(order, order[1:] + order[:1]):
                    nxt[a_] = b_
                self.next[(e, s)] = nxt
                h = fr["h"][torch.tensor(m)].mean(0).numpy()
                f = fr["f"][torch.tensor(m)].mean(0).numpy(); f = f / max(np.linalg.norm(f), 1e-6)
                sp = fr["sp"][torch.tensor(m)].mean(0).numpy()
                tier = int(np.bincount(fr["tier"][torch.tensor(m)].numpy()).argmax())
                self.state[(e, s)] = invert_state(e, h, tier, f, sp)
        ev, evec = np.linalg.eigh(P.T @ P)
        self.axis = evec[:, -1]
        self.slot_mix = self.counts.sum(0)

    def energy_grad_fate(self, x, e, s, k):
        """-log density of well k of type (e,s) (a committed fate) and its gradient; k [n] ints."""
        w, mu, inv, ld = self.wells[(e, s)]
        d = x - mu[k]
        Md = np.einsum("nij,nj->ni", inv[k], d)
        return 0.5 * (d * Md).sum(1), Md

    def energy_grad(self, x, e, s):
        """-log density of type (e,s) at x [n,3] (body coords) and its gradient."""
        if (e, s) not in self.wells:
            return np.zeros(len(x)), np.zeros_like(x)
        w, mu, inv, ld = self.wells[(e, s)]
        d = x[:, None, :] - mu[None]                                      # [n,K,3]
        Md = np.einsum("kij,nkj->nki", inv, d)                            # [n,K,3]
        q = (d * Md).sum(-1)                                              # [n,K]
        lp = np.log(w)[None] - 0.5 * q - 0.5 * ld[None]
        mx = lp.max(1, keepdims=True)
        r = np.exp(lp - mx); Z = r.sum(1, keepdims=True); r = r / Z
        E = -(mx[:, 0] + np.log(Z[:, 0]))
        g = (r[..., None] * Md).sum(1)                                    # dE/dx
        return E, g


class SortSwarm:
    stateless = False

    def __init__(self, cfg: SortCfg | None = None, world: sn.World | None = None):
        self.cfg = cfg or SortCfg()
        self.world = world or sn.World()
        self.codes = {k: PlanCode(T, self.cfg) for k, T in sn.load_targets().items()}
        self.mem = {}
        self.predators = []       # [(centre, radius, velocity)] like field; startle = flee (look/probe)
        self.stats = {}

    def eval(self):
        return self

    # ------------------------------------------------------------- bookkeeping
    def _reset(self, b, sw):
        al = (sw.active[b] & sw.hatched[b]).numpy()
        c = np.bincount(sw.elem[b].numpy()[al], minlength=4)
        kind = sn.PLAN_OF[int(np.argmax(c))]
        self.mem[b] = dict(plan=kind, cand=kind, cand_n=0, perm=None, t=0,
                           rng=np.random.default_rng(self.cfg.seed * 1000 + b + 17 * int(sw.elem[b].sum())))

    def _pick_perm(self, code: PlanCode, elem, dom, old):
        """perm[slot] = domain: the region map that best matches the census to the plan's classes."""
        cen = np.zeros((4, 3), int)
        np.add.at(cen, (elem, dom), 1)
        tot = max(1, cen.sum()); scale = tot / max(1, code.n)
        best = None
        for perm in sn.PERMS[code.nslots]:
            cost = sum(abs(cen[e, perm[s]] - code.counts[e, s] * scale) for e in range(4) for s in range(code.nslots))
            if old is not None and tuple(old) == tuple(perm):
                cost -= 2.0                                  # sticky
            if best is None or cost < best[0]:
                best = (cost, perm)
        return np.array(best[1])

    # ------------------------------------------------------------- step
    def __call__(self, sw, gen=None, **_):
        sw = sw.clone()
        for b in range(sw.B):
            if int(sw.clock[b]) == 0 or b not in self.mem:
                self._reset(b, sw)
            self._step(sw, b)
        sw.clock = sw.clock + 1
        sw.since = sw.since + 1
        return sw

    def _step(self, sw, b):
        cfg, m = self.cfg, self.mem[b]
        rng = m["rng"]
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        pos = sw.pos[b].numpy(); S = sw.s[b].numpy()
        elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy()
        idx = np.nonzero(act & hat)[0]
        if len(idx) == 0:
            return
        # --- plan from element ratios, with dwell hysteresis
        counts = np.bincount(elem[idx], minlength=4)
        cur = sn.MAJOR[m["plan"]]; top = int(np.argmax(counts))
        maj = cur if counts[cur] == counts[top] else top
        contested = False
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
        if m["perm"] is None or m["t"] % cfg.role_every == 0:
            m["perm"] = self._pick_perm(code, elem[idx], dom[idx], m["perm"])
        perm = m["perm"]
        role_of_dom = np.full(3, -1)
        for s_, d_ in enumerate(perm):
            role_of_dom[d_] = s_
        m["t"] += 1

        # --- hatching (eggs become tadpoles after hatch_steps)
        eggs = np.nonzero(act & ~hat)[0]
        if len(eggs):
            S[eggs, H_AGE] += 1
            ready = eggs[S[eggs, H_AGE] >= cfg.hatch_steps]
            if len(ready):
                sw.hatched[b, torch.as_tensor(ready)] = True
                hat = sw.hatched[b].numpy()

        idx = np.nonzero(act & hat)[0]
        role = self._eff_role(S, idx, dom, role_of_dom, m)  # -1: no region in this plan
        # --- centre (positional-information origin)
        if cfg.local_centre:
            est = S[idx, H_EST]
            fresh = np.abs(est).sum(1) == 0
            est[fresh] = pos[idx][fresh]
            cen_i = est
        else:
            c0 = pos[idx].mean(0)
            cen_i = np.broadcast_to(c0, (len(idx), 3))
        xb = pos[idx] - cen_i                              # body coords
        # --- own-type chemotaxis
        E = np.zeros(len(idx)); G = np.zeros((len(idx), 3))
        MU = xb.copy(); INV = np.zeros((len(idx), 3, 3))     # each tadpole's effective well (orphans: none)
        pid = sn.KINDS.index(m["plan"])
        for e in range(4):
            for s in range(3):
                sel = (elem[idx] == e) & (role == s)
                if not sel.any() or (e, s) not in code.wells:
                    continue
                if cfg.fate:
                    js = idx[sel]
                    key = 1 + pid * 16 + e * 4 + s
                    stale = (S[js, H_FATE] < 1) | (S[js, H_FKEY] != key)
                    if stale.any():
                        w = code.wells[(e, s)][0]
                        occ = np.bincount((S[js[~stale], H_FATE] - 1).astype(int), minlength=len(w)).astype(float)
                        need = w * sel.sum() - occ
                        for j in js[stale]:
                            f = int(np.argmax(need + 1e-3 * rng.random(len(w))))
                            S[j, H_FATE] = f + 1; S[j, H_FKEY] = key; need[f] -= 1
                    if cfg.lap_every and e in cfg.lap_elems:
                        # staggered: each runner advances on its own phase, so only ~1/lap_every of
                        # them are in transit at any step and every well stays occupied
                        fz = (S[js, H_FATE] >= 1) & (((m["t"] + js * 7) % cfg.lap_every) == 0)
                        S[js[fz], H_FATE] = code.next[(e, s)][(S[js[fz], H_FATE] - 1).astype(int)] + 1
                    kk = (S[js, H_FATE] - 1).astype(int)
                    E[sel], G[sel] = code.energy_grad_fate(xb[sel], e, s, kk)
                else:
                    E[sel], G[sel] = code.energy_grad(xb[sel], e, s)
                    w_, mu_, inv_, _ = code.wells[(e, s)]
                    dd = xb[sel][:, None] - mu_[None]
                    kk = (np.einsum("kij,nkj->nki", inv_, dd) * dd).sum(-1).argmin(1)
                MU[sel] = code.wells[(e, s)][1][kk]; INV[sel] = code.wells[(e, s)][2][kk]
        # orphans (no role / class not in plan): follow the plan's whole body (all types)
        orph = np.array([(elem[i], r) not in code.wells for i, r in zip(idx, role)]) if len(idx) else np.zeros(0, bool)
        if orph.any():
            Eb = np.full(orph.sum(), np.inf); Gb = np.zeros((orph.sum(), 3))
            for key in code.wells:
                e2, g2 = code.energy_grad(xb[orph], *key)
                better = e2 < Eb
                Eb[better], Gb[better] = e2[better], g2[better]
            E[orph], G[orph] = Eb, Gb
        step = -cfg.k_well * G
        nrm = np.linalg.norm(step, axis=1, keepdims=True)
        clip = np.full((len(idx), 1), cfg.well_clip)
        if cfg.lap_every:
            clip[np.isin(elem[idx], cfg.lap_elems)] = cfg.lap_clip      # runners sprint between wells
        step = step * np.minimum(1, clip / np.maximum(nrm, 1e-9))
        # --- neighbours: collision, adhesion, swaps
        P = pos[idx]
        dx = P[None] - P[:, None]                          # j - i
        d = np.linalg.norm(dx, axis=-1) + np.eye(len(idx)) * 1e3
        rep = np.clip(cfg.r0 - d, 0, None) / d
        f_col = -cfg.k_rep * (rep[..., None] * dx).sum(1)
        tid = elem[idx] * 4 + (role + 1)
        same_t = tid[:, None] == tid[None]
        same_e = elem[idx][:, None] == elem[idx][None]
        same_r = role[:, None] == role[None]
        Aij = np.where(same_t, cfg.a_same, np.where(same_e, cfg.a_elem, np.where(same_r, cfg.a_role, cfg.a_other)))
        near = (d < cfg.R_adh) & (d > cfg.r0 * 0.9)
        f_adh = ((Aij * near / d)[..., None] * dx).sum(1)
        f_swap = np.zeros_like(step)
        if cfg.swap > 0 and len(idx) > 1:
            ii, jj = np.nonzero(np.triu(d < cfg.r_swap, 1))
            if len(ii):
                # energy of each at the other's spot (each tadpole's own effective well)
                def e_at(a, y):
                    dd = y - MU[a]
                    return 0.5 * (dd * np.einsum("nij,nj->ni", INV[a], dd)).sum(1)
                Eij = e_at(ii, xb[jj]); Eji = e_at(jj, xb[ii])
                Eii = e_at(ii, xb[ii]); Ejj = e_at(jj, xb[jj])
                gain = (Eii + Ejj) - (Eij + Eji)
                go = gain > cfg.swap_margin
                if go.any():
                    gi, gj = ii[go], jj[go]
                    v = P[gj] - P[gi]
                    np.add.at(f_swap, gi, cfg.swap * v * 0.5)
                    np.add.at(f_swap, gj, -cfg.swap * v * 0.5)
                    # the two slide past one another: cancel their mutual collision this step
                    r = (np.clip(cfg.r0 - d[gi, gj], 0, None) / d[gi, gj])[:, None] * v
                    np.add.at(f_col, gi, cfg.k_rep * r)
                    np.add.at(f_col, gj, -cfg.k_rep * r)
        vel = S[idx, H_VEL]
        want = step + f_col + f_adh + f_swap
        if cfg.swirl_time:
            tm = elem[idx] == 3
            if tm.any():
                want[tm] += cfg.swirl_time * np.cross(code.axis, xb[tm])
        if self.predators:
            want += self._flee(P, elem[idx])
        v = cfg.inertia * vel + (1 - cfg.inertia) * want
        vmax = np.array(cfg.vmax)[elem[idx]][:, None]
        sp = np.linalg.norm(v, axis=1, keepdims=True)
        v = v * np.minimum(1, vmax / np.maximum(sp, 1e-9))
        pos[idx] += v
        S[idx, H_VEL] = v
        if cfg.local_centre:
            # consensus: the estimate moves with the body and relaxes toward neighbours' estimates
            # consensus morphogen: each estimate averages its neighbours' (and its own) estimates and
            # leaks toward its own position; the fixed point is a smoothed centroid of the connected body
            nb = (d < sn.World().R).astype(float) + np.eye(len(idx))
            c = np.array(cen_i)
            for _ in range(cfg.centre_iters):
                # leak toward the IMPLIED centre: where I am minus where my fated well says I should be
                # (orphans/no well: MU = own body coords, i.e. no opinion beyond the neighbours')
                implied = P + v - MU
                c = (1 - cfg.centre_relax) * (nb @ c) / nb.sum(1, keepdims=True) + cfg.centre_relax * implied
            S[idx, H_EST] = c
        # --- look: the type's code state (grow-in alpha)
        for i, (e, r) in enumerate(zip(elem[idx], role)):
            key = (int(e), int(r))
            st = code.state.get(key)
            if st is None:
                for s2 in range(3):
                    st = code.state.get((int(e), s2))
                    if st is not None:
                        break
            if st is None:
                st = invert_state(int(e), [1, 1, 1] if e != 2 else [2, .3, .3], 0, [0, 0, 1], [.3, 0])
            j = idx[i]
            keep = S[j, H_ROLE:H_FKEY + 1].copy()
            S[j] = st
            S[j, H_ROLE:H_FKEY + 1] = keep
        # --- composition
        if not contested:
            self._lay(sw, b, code, perm, role_of_dom, rng)
            if cfg.molt:
                self._molt(sw, b, code, perm, role_of_dom, rng)

    def _flee(self, P, el):
        out = np.zeros_like(P)
        for c, r, v in self.predators:
            dx = P - np.asarray(c)
            dd = np.linalg.norm(dx, axis=1, keepdims=True)
            w = np.clip(1 - dd / (2.2 * r), 0, 1)
            out += w * dx / np.maximum(dd, 1e-6) * np.array([0.6, 0.5, 1.4, 2.0])[el][:, None]
        return out

    def _eff_role(self, S, idx, dom, role_of_dom, m):
        """A tadpole's region: its domain's region, unless it TRANSFERRED to another region of the
        current plan (an overflow or orphan tadpole; channel H_ROLE = 1 + 4*plan + region)."""
        r = role_of_dom[dom[idx]].copy()
        pid = sn.KINDS.index(m["plan"])
        h = S[idx, H_ROLE].astype(int) - 1
        ok = (h >= 0) & (h // 4 == pid)
        r[ok] = h[ok] % 4
        return r

    def _census(self, sw, b, role_of_dom, include_eggs=True):
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        sel = act if include_eggs else act & hat
        idx = np.nonzero(sel)[0]
        el = sw.elem[b].numpy()[sel]; r = self._eff_role(sw.s[b].numpy(), idx, sw.dom[b].numpy(), role_of_dom, self.mem[b])
        cen = np.zeros((4, 4), int)                      # role -1 -> column 3
        np.add.at(cen, (el, np.where(r < 0, 3, r)), 1)
        return cen

    def _lay(self, sw, b, code, perm, role_of_dom, rng):
        cfg = self.cfg
        cen = self._census(sw, b, role_of_dom)
        want = np.zeros((4, 4)); want[:, :3] = np.ceil(code.counts * cfg.over)
        deficit = want - cen
        if deficit[:, :3].clip(min=0).sum() <= 0:
            return
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        idx = np.nonzero(act & hat)[0]
        free = list(np.nonzero(~act)[0])
        room = int(math.ceil(code.n * cfg.over)) - int(act.sum())        # the body has a size: total capped
        fill = np.where(want > 0, cen / np.maximum(want, 1), 9.0)        # lowest fill is laid first
        nlay = min(cfg.lay_max, len(free), room, int(rng.poisson(max(cfg.lay_rate * len(idx), 0.2))))
        if nlay <= 0:
            return
        elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy(); pos = sw.pos[b].numpy(); S = sw.s[b].numpy()
        ec = np.bincount(elem[act], minlength=4)                         # eggs included
        maj = sn.MAJOR[code.kind]
        laid = 0
        for i in rng.permutation(idx):
            if laid >= nlay or not free:
                break
            r = role_of_dom[dom[i]]
            if r < 0:
                continue
            e = int(elem[i])
            low = int(np.argmin(fill[:, r]))                 # its region's least-filled element
            if deficit[e, r] > 0 and fill[e, r] <= fill[low, r] + cfg.fill_tol:
                ce = e                                       # breeds true
            elif rng.random() < cfg.p_cross and deficit[low, r] > 0:
                ce = low                                     # its domain's most-needed element
            else:
                continue
            if ce != maj and ec[ce] + 1 >= ec[maj]:
                if rng.random() < cfg.p_cross and deficit[maj, r] > 0:
                    ce = maj                                 # the plan's element keeps its lead
                else:
                    continue
            ec[ce] += 1
            j = free.pop(0)
            dirn = rng.normal(size=3); dirn /= max(np.linalg.norm(dirn), 1e-6)
            pos[j] = pos[i] + cfg.r_bud * dirn
            S[j] = 0.0; S[j, A] = 0.2
            S[j, H_EST] = S[i, H_EST]                        # a newborn inherits its parent's sense of the centre
            elem[j] = ce; dom[j] = dom[i]
            sw.active[b, j] = True; sw.hatched[b, j] = False
            deficit[ce, r] -= 1; cen[ce, r] += 1
            fill[ce, r] = cen[ce, r] / max(want[ce, r], 1)
            laid += 1

    def _molt(self, sw, b, code, perm, role_of_dom, rng):
        """Relaxed: a tadpole of a SURPLUS class re-forms its crystal into the least-filled element of
        its region; if its region has no deficit (a domain the plan has too many of, or a domain the
        plan has no region for) it may also TRANSFER to the neediest region (domain never changes,
        so it pays the region's colour, but no longer the element)."""
        cfg = self.cfg
        cen = self._census(sw, b, role_of_dom, include_eggs=False)
        want = np.zeros((4, 4)); want[:, :3] = code.counts
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy(); S = sw.s[b].numpy()
        idx = np.nonzero(act & hat)[0]
        roles = self._eff_role(S, idx, dom, role_of_dom, self.mem[b])
        pid = sn.KINDS.index(self.mem[b]["plan"])
        order = rng.permutation(len(idx))
        for i, r in zip(idx[order], roles[order]):
            if rng.random() > cfg.molt_rate:
                continue
            e = int(elem[i]); rc = 3 if r < 0 else int(r)
            if cen[e, rc] <= want[e, rc]:
                continue                                       # its class is not in surplus
            deficit = want - cen
            if r >= 0 and deficit[:, r].max() > 0:
                r2 = int(r)
            elif cfg.transfer and deficit[:, :3].max() > 0:
                r2 = int(np.unravel_index(np.argmax(deficit[:, :3]), (4, 3))[1])
                S[i, H_ROLE] = 1 + 4 * pid + r2
            else:
                continue
            fl = np.where(want[:, r2] > 0, cen[:, r2] / np.maximum(want[:, r2], 1), 9.0)
            fl[deficit[:, r2] <= 0] = 9.0
            ne = int(np.argmin(fl))
            ec = np.bincount(elem[idx], minlength=4); maj = sn.MAJOR[code.kind]
            if ne != maj and ec[ne] + 1 >= ec[maj]:
                continue
            elem[i] = ne
            cen[e, rc] -= 1; cen[ne, r2] += 1


def load(path):
    import json
    d = json.load(open(path))
    c = d["cfg"]; c["vmax"] = tuple(c["vmax"])
    return SortSwarm(SortCfg(**c))
