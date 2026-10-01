"""Direction "evo", track (b): a COMPACT hand-designed swarm rule with ~25 evolved parameters.

No neural network. Every tadpole runs the same few lines:

  * The swarm knows its element mix (as a game Cell does). Its "body plan" is the plan of its locked
    majority element (hysteresis margin, B3 in evo_model).
  * A body plan is stored as a tiny DESCRIPTOR, not a template: per animation frame, one Gaussian per
    (element, domain-slot) group - mean + Cholesky factor (~10 numbers per group, 5-6 groups per plan,
    8 frames). It is fitted once from the plan data (swarm_targets), exactly as the four mixes are.
  * Each tadpole carries a private random "anchor" u ~ N(0, I) (hidden channels 12-14, drawn at hatch).
    Its home is  centroid + scale * (mu_g + L_g u)  for its group g in the current frame, scale following
    the swarm's own size (n / plan size)^(gamma). It swims toward home (gain, element top speed), plus a
    swirl about the plan's long axis (Time swirls hardest), plus jitter. Collision and the membrane are
    the world's, unchanged.
  * Visual channels (prism extents, Charge's tier, facing, spindle) take the plan's per-element average,
    eased in at rate `vis_rate` (so a switch visibly re-dresses the swarm).
  * Laying: the homeostat of evo_model (lay more of what the plan is short of; a share of eggs choose the
    scarcest element); domain breeds true.

Everything evolved is in GENES below; everything else is read from the plan data.
"""
import math
import os
import sys

import numpy as np
import torch
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402

A, DIE, FAC, PR, TI, SP = sn.A, sn.DIE, sn.FAC, sn.PR, sn.TI, sn.SP
U = slice(12, 15)            # anchor
UFLAG = 15                   # 1 once the anchor is drawn

_T = sn.load_targets()
_D0 = np.array([np.array(_T[sn.PLAN_OF[e]].mix, float) / sum(_T[sn.PLAN_OF[e]].mix) for e in range(4)])

GENES = [  # name, init, note
    ("gain", 0.0, "home pull = 0.15 * exp(gain) per step"),
    ("gamma", 0.0, "scale exponent = 0.33 * exp(gamma)"),
    ("scale0", 0.0, "overall scale = exp(scale0)"),
    ("spread", 0.0, "anchor spread = exp(spread)"),
    ("swirl_t", 0.0, "Time swirl (rad/step) = 0.02 * exp(swirl_t)"),
    ("swirl_o", -1.0, "other swirl = 0.02 * exp(swirl_o)"),
    ("jit", -1.0, "jitter = 0.2 * exp(jit)"),
    ("period", 0.0, "steps per plan frame = 8 * exp(period)"),
    ("vis_rate", 0.0, "visual easing = sigmoid(vis_rate)"),
    ("k_lay", 2.0, "homeostat gate slope"),
    ("b_lay", 0.0, "homeostat gate bias"),
    ("p_egg", -1.0, "share of chosen eggs = sigmoid"),
    ("beta_egg", 2.0, "egg choice sharpness"),
    ("lock", -1.5, "lock margin = 0.25 * sigmoid"),
    ("cap", 1.0, "stop laying above cap * plan size: cap = 0.5 + exp(cap)"),
] + [(f"D{i}", 0.0, "desired share table offset") for i in range(16)]
NAMES = [g[0] for g in GENES]
DIM = len(GENES)


def default_genome():
    return np.array([g[1] for g in GENES], float)


def G(g, name):
    return float(g[NAMES.index(name)])


def desired_table(g):
    z = np.log(_D0 + 0.02) + np.array([G(g, f"D{i}") for i in range(16)]).reshape(4, 4)
    z = np.exp(z - z.max(1, keepdims=True))
    return z / z.sum(1, keepdims=True)


def _inv_prism(h_mean, elem):
    raw = torch.zeros(1, 3, requires_grad=True)
    opt = torch.optim.Adam([raw], lr=0.1)
    e = torch.tensor([elem])
    for _ in range(300):
        loss = ((sn.prism_h(raw, e) - h_mean) ** 2).sum()
        opt.zero_grad(); loss.backward(); opt.step()
    return raw.detach()[0]


def build_descriptors():
    """Per plan: per frame, per group (elem, slot): mean, chol; per element: visual raw channels."""
    out = {}
    for k in sn.KINDS:
        T = _T[k]
        frames = []
        for fr in T.frames:
            p = fr["p"] - fr["p"].mean(0)
            groups = {}
            for e in range(4):
                for d in range(3):
                    m = (fr["elem"] == e) & (fr["slot"] == d)
                    if int(m.sum()) == 0:
                        continue
                    q = p[m]
                    mu = q.mean(0)
                    cov = ((q - mu).T @ (q - mu)) / max(len(q) - 1, 1) + 0.5 * torch.eye(3)
                    groups[(e, d)] = (mu, torch.linalg.cholesky(cov), int(m.sum()))
            frames.append(groups)
        # long axis for the swirl
        p0 = T.frames[0]["p"] - T.frames[0]["p"].mean(0)
        evals, evecs = torch.linalg.eigh(p0.T @ p0)
        vis = {}
        f0 = T.frames[0]
        for e in range(4):
            m = f0["elem"] == e
            if int(m.sum()) == 0:
                m = torch.ones_like(m)
            raw = torch.zeros(sn.C)
            raw[PR] = _inv_prism(f0["h"][m].mean(0), e)
            tier = F.one_hot(f0["tier"][m], 3).float().mean(0)
            raw[TI] = torch.log(tier + 0.02)
            fm = f0["f"][m].mean(0)
            raw[FAC] = fm / fm.norm().clamp(min=1e-3)
            sp = f0["sp"][m].mean(0)
            raw[SP.start] = torch.logit((sp[0] / 0.6).clamp(0.02, 0.98))
            raw[SP.start + 1] = torch.atanh((sp[1] / 0.5).clamp(-0.98, 0.98))
            vis[e] = raw
        out[k] = dict(frames=frames, axis=evecs[:, -1], vis=vis, n=T.n, slots=T.slots)
    return out


_DESC = None


def descriptors():
    global _DESC
    if _DESC is None:
        p = os.path.join(HERE, "runs", "evo_compact_desc.pt")
        if os.path.isfile(p):
            _DESC = torch.load(p, weights_only=False)
        else:
            _DESC = build_descriptors()
            os.makedirs(os.path.dirname(p), exist_ok=True)
            torch.save(_DESC, p)
    return _DESC


class CompactRule:
    """model(sw, gen) -> Swarm; model.world. Batched over B swarms."""

    def __init__(self, genome, world=None):
        self.genome = np.asarray(genome, float)
        self.world = world or sn.World()
        self.D = torch.tensor(desired_table(self.genome), dtype=torch.float32)
        self.desc = descriptors()
        self.locked = None
        g = self.genome
        # dense per-plan tables: [plan, frame, elem, slot] -> mu [3], L [3,3]
        K = len(sn.KINDS)
        self.mu = torch.zeros(K, 8, 4, 3, 3); self.L = torch.zeros(K, 8, 4, 3, 3, 3)
        self.vis = torch.zeros(K, 4, sn.C)
        for pi, k in enumerate(sn.KINDS):
            d = self.desc[k]
            for fi, groups in enumerate(d["frames"]):
                for e in range(4):
                    for s in range(3):
                        key = (e, s) if (e, s) in groups else None
                        if key is None:   # fall back: same element any slot, else the whole-plan mean
                            alts = [kk for kk in groups if kk[0] == e] or list(groups)
                            key = max(alts, key=lambda kk: groups[kk][2])
                        mu, Lc, _ = groups[key]
                        self.mu[pi, fi, e, s] = mu; self.L[pi, fi, e, s] = Lc
            for e in range(4):
                self.vis[pi, e] = d["vis"][e]
        self.axis = torch.stack([self.desc[k]["axis"] for k in sn.KINDS])
        self.plan_n = torch.tensor([float(self.desc[k]["n"]) for k in sn.KINDS])
        self.nslot = torch.tensor([self.desc[k]["slots"] for k in sn.KINDS])

    def _shares(self, sw):
        hb = (sw.hatched & sw.active).float()
        cnt = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1)
        return cnt, cnt / cnt.sum(1, keepdim=True).clamp(min=1)

    @torch.no_grad()
    def __call__(self, sw, gen=None):
        g, W = self.genome, self.world
        B, N, _ = sw.pos.shape
        if self.locked is None or self.locked.shape[0] != B:
            self.locked = torch.full((B,), -1, dtype=torch.long)
        self.locked[sw.clock == 0] = -1
        cnt, share = self._shares(sw)
        n = cnt.sum(1)
        top = cnt.argmax(1)
        margin = 0.25 / (1 + math.exp(-G(g, "lock")))
        cur = self.locked.clamp(min=0)
        lead = cnt.gather(1, top[:, None])[:, 0] - cnt.gather(1, cur[:, None])[:, 0]
        self.locked = torch.where((self.locked < 0) | (lead > margin * n), top, self.locked)
        plan = torch.tensor([sn.KINDS.index(sn.PLAN_OF[int(e)]) for e in self.locked])     # [B]

        pos, s = sw.pos.clone(), sw.s.clone()
        live = sw.active & sw.hatched
        # anchors for the newly hatched
        need = live & (s[:, :, UFLAG] < 0.5)
        if need.any():
            s[need, U] = torch.randn(int(need.sum()), 3, generator=gen)
            s[need, UFLAG] = 1.0
        w = live.float()
        cen = (w[:, :, None] * pos).sum(1) / w.sum(1).clamp(min=1)[:, None]
        period = 8 * math.exp(G(g, "period"))
        fi = (sw.clock.float() / period).long() % 8                                          # [B]
        slot = sw.dom.clamp(max=2)
        slot = torch.minimum(slot, (self.nslot[plan] - 1)[:, None])
        pb = plan[:, None].expand(B, N); fb = fi[:, None].expand(B, N)
        mu = self.mu[pb, fb, sw.elem, slot]                                                    # [B,N,3]
        Lc = self.L[pb, fb, sw.elem, slot]                                                     # [B,N,3,3]
        u = s[:, :, U] * math.exp(G(g, "spread"))
        scale = math.exp(G(g, "scale0")) * (n / self.plan_n[plan]).clamp(min=0.1) ** (0.33 * math.exp(G(g, "gamma")))
        home = cen[:, None] + scale[:, None, None] * (mu + (Lc @ u[..., None])[..., 0])
        gain = min(0.15 * math.exp(G(g, "gain")), 1.0)
        v = gain * (home - pos)
        # swirl about the plan's long axis through the centroid
        ax = self.axis[plan][:, None].expand(B, N, 3)
        rel = pos - cen[:, None]
        om = torch.where(sw.elem == 3, 0.02 * math.exp(G(g, "swirl_t")), 0.02 * math.exp(G(g, "swirl_o")))
        v = v + om[..., None] * torch.cross(ax, rel, dim=-1)
        v = v + 0.2 * math.exp(G(g, "jit")) * torch.randn(B, N, 3, generator=gen)
        vmax = torch.tensor(W.vmax)[sw.elem][..., None]
        sp = v.norm(dim=-1, keepdim=True)
        v = v * torch.clamp(vmax / sp.clamp(min=1e-6), max=1.0)
        pos = pos + v * live[..., None]
        # visuals eased toward the plan's per-element dress; eggs hatch
        r = 1 / (1 + math.exp(-G(g, "vis_rate")))
        tgt = self.vis[pb, sw.elem]
        keepch = torch.zeros(sn.C, dtype=torch.bool); keepch[FAC] = keepch[PR] = keepch[TI] = keepch[SP] = True
        s[..., keepch] = s[..., keepch] + r * (tgt[..., keepch] - s[..., keepch])
        s[..., A] = torch.where(sw.active, torch.ones_like(s[..., A]), s[..., A])
        s[..., DIE] = 0.0
        # collision + membrane (the world's)
        flat = pos.reshape(B * N, 3)
        gi, gj = sn.edges(sn.Swarm(pos, s, sw.elem, sw.dom, sw.active, sw.hatched, sw.deaths), W.R)
        dxc = flat[gj] - flat[gi]
        rr = (dxc * dxc).sum(-1).clamp(min=1e-8).sqrt()
        ov = (W.r0 - rr).clamp(min=0) / W.r0
        flat = flat + torch.zeros(B * N, 3).index_add(0, gi, -(W.rep * W.r0 * 0.5) * ov[:, None] * dxc / rr[:, None])
        rad = flat.norm(dim=-1, keepdim=True).clamp(min=1e-6)
        flat = flat - 0.5 * (rad - W.membrane).clamp(min=0) * flat / rad
        pos = flat.view(B, N, 3)
        hatched = sw.hatched | (sw.active & (s[..., A] > 0.1))
        out = sn.Swarm(pos, s * sw.active[..., None], sw.elem.clone(), sw.dom.clone(), sw.active.clone(), hatched,
                       sw.deaths.clone(), sw.mutants.clone(), sw.age.clone(), sw.clock + 1, sw.plan.clone(), sw.since + 1, sw.bw)
        self._lay(out, gi, gj, gen, plan)
        return out

    def _lay(self, sw, gi, gj, gen, plan):
        g, W = self.genome, self.world
        B, N, _ = sw.pos.shape
        cnt, share = self._shares(sw)
        n = cnt.sum(1)
        deficit = self.D[self.locked.clamp(min=0)] - share
        de = deficit.gather(1, sw.elem)
        b0 = G(g, "b_lay")
        mult = (torch.sigmoid(G(g, "k_lay") * de / 0.1 + b0) / torch.sigmoid(torch.tensor(b0))).reshape(-1).clamp(max=1 / W.p_bud)
        cap = (0.5 + math.exp(G(g, "cap"))) * self.plan_n[plan]
        room = (n < cap)
        pchoose = 1 / (1 + math.exp(-G(g, "p_egg")))
        probs = torch.softmax(G(g, "beta_egg") * deficit / 0.1, 1)
        pos = sw.pos.reshape(B * N, 3)
        dxl = pos[gj] - pos[gi]
        close = ((dxl * dxl).sum(-1) < W.r_lay ** 2).float()
        ncl = torch.zeros(B * N).index_add(0, gi, close)
        cen = torch.zeros(B * N, 3).index_add(0, gi, dxl * close[:, None])
        ok = (sw.hatched.reshape(-1) & sw.active.reshape(-1) & (ncl < W.k_bud)
              & (torch.rand(B * N, generator=gen) <= W.p_bud * mult)).view(B, N) & room[:, None]
        for b in range(B):
            parents = ok[b].nonzero().squeeze(1)
            free = (~sw.active[b]).nonzero().squeeze(1)
            kk = min(len(parents), len(free), max(0, int(cap[b] - n[b]) + 1))
            if kk <= 0:
                continue
            parents = parents[torch.randperm(len(parents), generator=gen)[:kk]]
            slots = free[:kk]
            away = -cen.view(B, N, 3)[b, parents]
            away = away / away.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            noise = torch.randn(kk, 3, generator=gen)
            dirn = away + 0.6 * noise / noise.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            dirn = dirn / dirn.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            sw.pos[b, slots] = sw.pos[b, parents] + W.r_bud * dirn
            sw.s[b, slots] = 0.0
            e = sw.elem[b, parents].clone()
            crossed = torch.rand(kk, generator=gen) < pchoose
            chosen = torch.multinomial(probs[b], kk, replacement=True, generator=gen)
            e = torch.where(crossed, chosen, e)
            sw.elem[b, slots] = e
            sw.dom[b, slots] = sw.dom[b, parents]
            sw.active[b, slots] = True
            sw.hatched[b, slots] = False
            sw.age[b, slots] = 0


def evaluate(genome, seeds=(1,)):
    import evo_model as em
    torch.set_num_threads(1)
    model = CompactRule(genome)
    fs, ps, ms = [], [], []
    for s in seeds:
        summ = em.fast_rollout(model, s)
        f, p, m = em.fitness(summ)
        fs.append(f); ps.append(p); ms.append(m)
    return float(np.mean(fs)), ps, ms
