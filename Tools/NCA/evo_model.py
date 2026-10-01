"""Gradient-free search over the swarm rule (direction "evo").

The model is the trained G2 rule (results/swarm_coevo_g2/rule.pt) wrapped by a GENOME: a flat float
vector that (1) perturbs the rule's output layer and (2) switches whole BEHAVIOURS on and off and tunes
them. The behaviours are actuators the backprop rule never had, so the search can add or subtract
them, not only re-weight what is there:

  B1  lay homeostat      a parent lays more when its element is SHORT of the share its swarm's current
                         majority plan wants, less when it is over (production gating, never a cull)
  B2  egg choice         a share of eggs take the element the swarm is shortest of (domain breeds true)
  B3  majority lock      the "current majority" the homeostats steer by only changes when a new
                         element leads by a margin (hysteresis: no flip-flopping on a near tie)
  B4  sated rest         a full swarm (no free slots) stops laying pressure entirely (no-op today;
                         kept as an explicit gene so ablation can show it)
  B5  output re-weight   per-output-channel gain and bias on the MLP's last layer (motion, hatch,
                         death, prism, laying gate...) - the "fiddle with weights" half

A behaviour is ON when its switch gene is > 0, so CMA-ES can delete it by pushing the gene negative.
The desired-share table D[majority, element] is evolved too, initialised from the plans' own mixes.

Everything is a model in the shared yardstick's sense: `model(sw, gen) -> Swarm`, `model.world`.
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

BASE = os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt")
NOUT = sn.C + 3                                   # MLP outputs: 32 state deltas + 3 velocity

# --------------------------------------------------------------- genome ---
# name -> (size, init)
_T = sn.load_targets()
_D0 = np.array([np.array(_T[sn.PLAN_OF[e]].mix, float) / sum(_T[sn.PLAN_OF[e]].mix) for e in range(4)])  # by majority ELEMENT
LAYOUT = [
    ("sw_lay", 1, 1.0),        # B1 on/off
    ("k_lay", 1, 2.0),         # B1: gate = sigmoid(k_lay * deficit/0.1 + b_lay) relative to sigmoid(b_lay)
    ("b_lay", 1, 0.0),
    ("sw_egg", 1, 1.0),        # B2 on/off
    ("p_egg", 1, -1.0),        # B2: share of eggs that are chosen = sigmoid(p_egg)
    ("beta_egg", 1, 2.0),      # B2: choice = softmax(beta * deficit/0.1)
    ("sw_lock", 1, 1.0),       # B3 on/off
    ("lock", 1, -1.5),         # B3: margin = 0.25 * sigmoid(lock) of the headcount
    ("D", 16, 0.0),            # desired share table offsets (logits added to log D0)
    ("sw_out", 1, -1.0),       # B5 on/off (off at start: begin from the behaviour genes alone)
    ("g_out", NOUT, 0.0),      # B5 per-output gain (w3 row scale = 1 + 0.5 * tanh(g))
    ("b_out", NOUT, 0.0),      # B5 per-output bias offset (x 0.05)
    ("sw_swirl", 1, 0.5),      # B6 on/off: per-element swirl about the plan's long axis (zero rate = no-op)
    ("swirl", 4, 0.0),         # B6: rad/step = 0.05 * tanh(gene) * exp(swirl_gain), per element C M S T
    ("swirl_gain", 1, 0.0),    # B6: lifts the swirl cap (stage 3: the dragonfly's runners need laps)
]
SLICES, DIM = {}, 0
for _n, _s, _ in LAYOUT:
    SLICES[_n] = slice(DIM, DIM + _s); DIM += _s


def default_genome():
    g = np.zeros(DIM)
    for n, s, v in LAYOUT:
        g[SLICES[n]] = v
    return g


def pad(g):
    """An older (shorter) genome, with the newer genes at their defaults (all no-ops)."""
    g = np.asarray(g, float)
    if len(g) < DIM:
        g = np.concatenate([g, default_genome()[len(g):]])
    return g


def _plan_axes():
    ax = []
    for e in range(4):
        p = _T[sn.PLAN_OF[e]].frames[0]["p"]; p = p - p.mean(0)
        ax.append(torch.linalg.eigh(p.T @ p)[1][:, -1])
    return torch.stack(ax)


_AXES = _plan_axes()


def gene(g, name):
    v = g[SLICES[name]]
    return float(v[0]) if len(v) == 1 else v


def describe(g):
    D = desired_table(g)
    g = pad(g)
    on = {k: gene(g, k) > 0 for k in ("sw_lay", "sw_egg", "sw_lock", "sw_out", "sw_swirl")}
    lines = [f"behaviours on: {[k for k, v in on.items() if v]}",
             f"lay gate k={gene(g,'k_lay'):.2f} b={gene(g,'b_lay'):.2f} | egg share={1/(1+math.exp(-gene(g,'p_egg'))):.3f} "
             f"beta={gene(g,'beta_egg'):.2f} | lock margin={0.25/(1+math.exp(-gene(g,'lock'))):.3f}",
             "desired share D[majority -> C M S T]:"]
    for e in range(4):
        lines.append(f"  {sn.ELEMENTS[e]:6s} majority: " + " ".join(f"{x:.2f}" for x in D[e]) +
                     f"   (plan {sn.PLAN_OF[e]}: " + " ".join(f"{x:.2f}" for x in _D0[e]) + ")")
    return "\n".join(lines)


def desired_table(g):
    z = np.log(_D0 + 0.02) + g[SLICES["D"]].reshape(4, 4)
    z = np.exp(z - z.max(1, keepdims=True))
    return z / z.sum(1, keepdims=True)


# ---------------------------------------------------------------- model ---

class EvoRule(sn.SwarmRule):
    """The G2 rule + a genome. Extra per-swarm state (the locked majority) lives on the model and is
    reset whenever a swarm's clock is 0 (the yardstick's convention)."""

    def __init__(self, genome, base=BASE):
        st = torch.load(base, weights_only=False, map_location="cpu")
        w = st["world"]; w["vmax"] = tuple(w["vmax"])
        super().__init__(sn.World(**w), hidden=st["hidden"])
        self.load_state_dict(st["rule"])
        self.genome = pad(genome)
        g = self.genome
        if gene(g, "sw_out") > 0:
            with torch.no_grad():
                gain = 1 + 0.5 * torch.tanh(torch.tensor(g[SLICES["g_out"]], dtype=torch.float32))
                self.w3.mul_(gain[:, None])
                self.b3.add_(0.05 * torch.tensor(g[SLICES["b_out"]], dtype=torch.float32))
        self.D = torch.tensor(desired_table(g), dtype=torch.float32)
        self.locked = None
        self.eval()

    def forward(self, sw, gen=None, bud=True, fire=None):
        with torch.no_grad():
            if self.locked is None or self.locked.shape[0] != sw.B:
                self.locked = torch.full((sw.B,), -1, dtype=torch.long)
            self.locked[sw.clock == 0] = -1
            self._update_lock(sw)
            out = self._step(sw, gen, bud, fire)
            g = self.genome
            if gene(g, "sw_swirl") > 0:
                rate = 0.05 * math.exp(gene(g, "swirl_gain")) * torch.tanh(torch.tensor(g[SLICES["swirl"]], dtype=torch.float32))
                if float(rate.abs().max()) > 1e-4:
                    live = (sw.active & sw.hatched & out.active & out.hatched)
                    w = live.float()
                    cen = (w[:, :, None] * out.pos).sum(1) / w.sum(1).clamp(min=1)[:, None]
                    ax = _AXES[self.locked.clamp(min=0)][:, None].expand_as(out.pos)
                    om = rate[out.elem][..., None]
                    out.pos = out.pos + live[..., None] * om * torch.cross(ax, out.pos - cen[:, None], dim=-1)
            return out

    def _shares(self, sw):
        hb = (sw.hatched & sw.active).float()
        cnt = (hb[:, :, None] * F.one_hot(sw.elem, 4).float()).sum(1)       # [B,4]
        return cnt, cnt / cnt.sum(1, keepdim=True).clamp(min=1)

    def _update_lock(self, sw):
        g = self.genome
        cnt, _ = self._shares(sw)
        n = cnt.sum(1)
        top = cnt.argmax(1)
        if gene(g, "sw_lock") > 0:
            margin = 0.25 / (1 + math.exp(-gene(g, "lock")))
            cur = self.locked.clamp(min=0)
            lead = cnt.gather(1, top[:, None])[:, 0] - cnt.gather(1, cur[:, None])[:, 0]
            flip = (self.locked < 0) | (lead > margin * n)
            self.locked = torch.where(flip, top, self.locked)
        else:
            self.locked = top

    @torch.no_grad()
    def _lay(self, sw, gi, gj, gen, q, pe=None):
        g = self.genome
        W = self.world
        B, N, _ = sw.pos.shape
        cnt, share = self._shares(sw)
        want = self.D[self.locked.clamp(min=0)]                              # [B,4]
        deficit = want - share                                                # >0: short of it
        mult = torch.ones(B * N)
        if gene(g, "sw_lay") > 0:
            k, b0 = gene(g, "k_lay"), gene(g, "b_lay")
            de = deficit.gather(1, sw.elem)                                    # [B,N] the parent's element
            mult = (torch.sigmoid(k * de / 0.1 + b0) / torch.sigmoid(torch.tensor(b0))).reshape(-1).clamp(max=1.0 / max(W.p_bud, 1e-3))
        qq = (torch.ones(B * N) if q is None else q) * mult
        pchoose = 1 / (1 + math.exp(-gene(g, "p_egg"))) if gene(g, "sw_egg") > 0 else 0.0
        probs = torch.softmax(gene(g, "beta_egg") * deficit / 0.1, 1)       # [B,4]
        laid = []
        pos = sw.pos.reshape(B * N, 3)
        dxl = pos[gj] - pos[gi]
        close = ((dxl * dxl).sum(-1) < W.r_lay ** 2).float()
        ncl = torch.zeros(B * N).index_add(0, gi, close)
        cen = torch.zeros(B * N, 3).index_add(0, gi, dxl * close[:, None])
        ok = (sw.hatched.reshape(-1) & sw.active.reshape(-1) & (ncl < W.k_bud)
              & (torch.rand(B * N, generator=gen) <= W.p_bud * qq)).view(B, N)
        for b in range(B):
            parents = ok[b].nonzero().squeeze(1)
            free = (~sw.active[b]).nonzero().squeeze(1)
            kk = min(len(parents), len(free))
            if kk == 0:
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
            if pchoose > 0:
                crossed = torch.rand(kk, generator=gen) < pchoose
                chosen = torch.multinomial(probs[b], kk, replacement=True, generator=gen)
                e = torch.where(crossed, chosen, e)
            mut = torch.rand(kk, generator=gen) < W.p_mut
            if mut.any():
                e[mut] = (e[mut] + torch.randint(1, 4, (int(mut.sum()),), generator=gen)) % 4
                sw.mutants[b] += int(mut.sum())
            sw.elem[b, slots] = e
            sw.dom[b, slots] = sw.dom[b, parents]
            sw.active[b, slots] = True
            sw.hatched[b, slots] = False
            sw.age[b, slots] = 0
            laid.append(b)
        return laid

    def lay(self, sw, gi, gj, gen=None):
        W = self.world
        q = torch.sigmoid(W.lay_gain * sw.s[:, :, sn.LAY].reshape(-1) + W.lay_bias) if W.learned_lay else None
        self._lay(sw, gi, gj, gen, q)


# ------------------------------------------------------------- fitness ---

def liveliness(sw, p8, a8, T):
    """Per plan: mean over its elements of min(1, measured 8-step heart displacement / the plan's per-frame
    speed for that element) - a swarm whose runners run laps scores 1, a jittering blob ~0."""
    a = a8 & sw.active & sw.hatched
    d = (sw.pos - p8).norm(dim=-1)
    out = []
    for b, k in enumerate(sn.KINDS):
        sc, wsum = 0.0, 0.0
        for e in range(4):
            tgt = T[k].speed[e]
            sel = a[b] & (sw.elem[b] == e)
            if tgt < 0.3 or int(sel.sum()) == 0:
                continue
            w = tgt                                 # fast elements (Time) weigh most
            sc += w * min(1.0, float(d[b][sel].mean()) / tgt); wsum += w
        out.append(sc / wsum if wsum else 0.0)
    return out


def _strike_b(sw, b, gen, frac=1.0):
    """swarm_probe.strike for sample b of a batch (same geometry)."""
    m = sw.active[b] & sw.hatched[b]
    p = sw.pos[b][m]
    if len(p) < 4:
        return 0
    c = p.mean(0)
    rms = ((p - c) ** 2).sum(-1).mean().sqrt()
    d = torch.randn(3, generator=gen); d = d / d.norm().clamp(min=1e-6)
    hit = m & (((sw.pos[b] - (c + frac * rms * d)) ** 2).sum(-1) < (frac * rms) ** 2)
    sw.active[b, hit] = False; sw.hatched[b, hit] = False; sw.s[b, hit] = 0.0
    return int(hit.sum())


@torch.no_grad()
def fast_probe(model, sw, gen, regrow=120, L=None):
    """The vessel-strike probe on an already grown batch (cloned): heal per plan in [-1, 1]."""
    L = L or sn.LossCfg()
    T = sn.load_targets()
    model.locked = None if not hasattr(model, "locked") else model.locked
    saved = None if getattr(model, "locked", None) is None else model.locked.clone()
    p = sw.clone()
    sc = lambda b, k: sn.swarm_loss(sn.decode(p, b), T[k], L)[1]["sink"]
    before = [sc(b, k) for b, k in enumerate(sn.KINDS)]
    for b in range(p.B):
        _strike_b(p, b, gen)
    cut = [sc(b, k) for b, k in enumerate(sn.KINDS)]
    for _ in range(regrow):
        p = model(p, gen)
    rec = [sc(b, k) for b, k in enumerate(sn.KINDS)]
    if saved is not None:
        model.locked = saved
    heal = []
    for b0, c, r in zip(before, cut, rec):
        span = c - b0
        heal.append(max(-1.0, min(1.0, (c - r) / span)) if span > 0.3 else (1.0 if r <= c + 0.3 else -1.0))
    return dict(before=before, cut=cut, rec=rec, heal=heal)


@torch.no_grad()
def fast_rollout(model, seed, steps=240, switch_steps=240, L=None, probe=False):
    """The yardstick's protocol (grow, score, cull to SWITCH_TO, run, score) for all four plans in ONE
    batch, without the viewer frames or the geometry table. Returns a summary tests_passed() accepts."""
    L = L or sn.LossCfg()
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[k] for k in sn.KINDS], model.world, gen)
    for t in range(steps):
        if t == steps - 8:
            p8, a8 = sw.pos.clone(), (sw.active & sw.hatched).clone()
        sw = model(sw, gen)
    summ = {"cross": {}, "census": {}, "switch": {}}
    summ["live"] = liveliness(sw, p8, a8, T)
    for b, k in enumerate(sn.KINDS):
        x = sn.decode(sw, b)
        summ["cross"][k] = {k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS}
        summ["census"][k] = sn.census(sw, b, T[k])
    if probe:
        summ["probe"] = fast_probe(model, sw, gen, L=L)
    done = [sn.lose_majority(sw, b, gen, to=sn.SWITCH_TO[k]) is not None for b, k in enumerate(sn.KINDS)]
    for _ in range(switch_steps):
        sw = model(sw, gen)
    for b, k in enumerate(sn.KINDS):
        new = sn.PLAN_OF[sn.SWITCH_TO[k]]
        x = sn.decode(sw, b)
        summ["switch"][k] = dict(to=new, done=done[b], cross={k2: round(sn.swarm_loss(x, T[k2], L)[1]["sink"], 2) for k2 in sn.KINDS},
                                 majority=sn.majority_plan(sw, b, k), census=sn.census(sw, b, T[new]))
    return summ


def margins(summ):
    """Per test, a margin in [-1, 1]: (best other - wanted) / (best other + wanted); -1 if too small."""
    out = []
    rows = [(summ["cross"][k], k, summ["census"][k]["n"]) for k in sn.KINDS] + \
           [(v["cross"], v["to"], v["census"]["n"]) for v in summ["switch"].values()]
    for row, want, n in rows:
        if n < sn.MIN_TEST_BODY or row[want] >= 99.9:
            out.append(-1.0); continue
        o = min(v for kk, v in row.items() if kk != want)
        out.append(max(-1.0, min(1.0, (o - row[want]) / (o + row[want]))))
    return out


def fitness(summ):
    p, _ = sn.tests_passed(summ)
    m = margins(summ)
    return p + 0.5 * float(np.mean(m)), p, m


_CACHE = {}


W_HEAL = float(os.environ.get("EVO_W_HEAL", "0"))
W_LIVE = float(os.environ.get("EVO_W_LIVE", "0"))


def evaluate(genome, seeds=(1,), make=None):
    torch.set_num_threads(1)
    model = (make or EvoRule)(genome)
    fs, ps, ms = [], [], []
    for s in seeds:
        summ = fast_rollout(model, s, probe=W_HEAL > 0)
        f, p, m = fitness(summ)
        if W_HEAL > 0:
            f += W_HEAL * float(np.mean(summ["probe"]["heal"]))
            m = m + summ["probe"]["heal"]
        if W_LIVE > 0:
            f += W_LIVE * float(np.mean(summ["live"]))
        fs.append(f); ps.append(p); ms.append(m)
    return float(np.mean(fs)), ps, ms
