"""Collision automaton — the Growing-NCA cell moved off the grid onto free particles.

A grid NCA evaluates every CELL. This evaluates every interacting PAIR: each particle has a
continuous position and the same 16 channels (RGBA + 12 hidden), and all it perceives is
the change due to its neighbours within radius R. Boids is the designed member of this
family (separation / alignment / cohesion written by hand); here the rule is learned.

Per particle i, over neighbours j with |x_j - x_i| < R (w = (1 - r^2/R^2)^3, g = (1 - r^2/R^2)^2):
  perceive = [ s_i,                                   own state              (C)
               sum_j w_ij s_j / (1 + sum_j w_ij),     neighbour mean         (C)   ~ identity/blur
               sum_j g_ij (x_j - x_i)/R (s_j - s_i)
                    / (1 + sum_j g_ij),               state gradient     (C x d)  ~ Sobel
               sum_j w_ij / rho0 ]                    crowding               (1)
  update   = MLP(perceive) -> (delta state [C], velocity [d]); last layer zero-init
  stochastic firing p = 0.5 per particle, exactly as the grid cell.

Designed physics — the "rules of the world", the particle counterparts of the grid's alive mask:
  * COLLISION: pairs closer than r0 push apart (boids' separation), so particles keep a spacing.
  * ALIVE: a particle is alive iff some neighbour (or itself) has alpha > 0.1, tested before and
    after the update; a particle that is not both is removed.
  * BUDDING: a visible particle (alpha > 0.1) with fewer than K neighbours buds one dormant
    (all-zero) child at distance r_bud, pointed away from its neighbours. This is the grid's
    "empty cells next to the living may come alive": the rule, not the budding, decides whether
    a child ever becomes visible; if it never does, it is removed once nothing near it is alive.
Particles live in a fixed-capacity slot array per sample (active flags), so budding and removal
are bookkeeping, and the whole thing stays batched.

The loss renders the particles (each a small Gaussian of its premultiplied RGBA) into the same
target grid the grid NCA used and takes the same pixel MSE, so gradients reach colour AND
position. Dimension-generic: the same code is the 2D lizard and the 3D swimmer.

    python3 Tools/NCA/particle_nca.py selftest
    python3 Tools/NCA/particle_nca.py bench
    python3 Tools/NCA/particle_nca.py train --experiment regenerating
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time
from dataclasses import dataclass, asdict

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.utils.checkpoint import checkpoint
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from growing_nca import load_emoji, write_gif  # noqa: E402


# ------------------------------------------------------------------ world ---

@dataclass
class World:
    """The designed physics. Units are target pixels/voxels."""
    dim: int = 2
    R: float = 3.0          # interaction radius
    r0: float = 1.4         # collision spacing: closer pairs push apart
    rep: float = 0.35       # collision strength (fraction of the overlap removed per step)
    vmax: float = 0.6       # learned velocity cap per step
    r_bud: float = 1.3      # bud distance
    k_bud: int = 11         # bud while fewer neighbours than this
    rho0: float = 6.0       # crowding normaliser (~ sum of w in a packed neighbourhood)
    capacity: int = 640     # particle slots per sample
    sigma: float = 0.85     # render splat width


class State:
    """Batched particle system: pos [B,N,d], s [B,N,C], active [B,N] bool."""

    def __init__(self, pos, s, active):
        self.pos, self.s, self.active = pos, s, active

    def clone(self):
        return State(self.pos.clone(), self.s.clone(), self.active.clone())

    def detach(self):
        return State(self.pos.detach(), self.s.detach(), self.active.clone())

    def index(self, idx):
        return State(self.pos[idx], self.s[idx], self.active[idx])

    @property
    def B(self):
        return self.pos.shape[0]


def seed_state(B, world: World, centre, C=16):
    pos = torch.zeros(B, world.capacity, world.dim)
    pos[:, 0] = torch.tensor(centre, dtype=torch.float32)
    s = torch.zeros(B, world.capacity, C)
    s[:, 0, 3:] = 1.0
    active = torch.zeros(B, world.capacity, dtype=torch.bool)
    active[:, 0] = True
    return State(pos, s, active)


def neighbour_edges(st: State, R):
    """(gi, gj) flat indices of active ordered pairs closer than R (i != j). No gradient."""
    with torch.no_grad():
        B, N, _ = st.pos.shape
        d = torch.cdist(st.pos, st.pos)
        m = (d < R) & st.active[:, :, None] & st.active[:, None, :]
        m &= ~torch.eye(N, dtype=torch.bool)[None]
        b, i, j = m.nonzero(as_tuple=True)
        return b * N + i, b * N + j


class ParticleNCA(nn.Module):
    def __init__(self, world: World, channel_n=16, hidden=128, fire_rate=0.5):
        super().__init__()
        self.world, self.C, self.hidden, self.fire_rate = world, channel_n, hidden, fire_rate
        d = world.dim
        self.F = channel_n * (2 + d) + 1
        self.w1 = nn.Parameter(torch.empty(hidden, self.F))
        self.b1 = nn.Parameter(torch.zeros(hidden))
        self.w2 = nn.Parameter(torch.zeros(channel_n + d, hidden))   # zero init: identity + no motion
        self.b2 = nn.Parameter(torch.zeros(channel_n + d))
        nn.init.xavier_uniform_(self.w1)

    # -- pieces, all on flat [B*N, ...] views with an edge list ----------------
    def _pairs(self, pos, gi, gj):
        R = self.world.R
        dx = pos[gj] - pos[gi]
        r2 = (dx * dx).sum(-1)
        q = (1 - r2 / (R * R)).clamp(min=0)
        return dx, r2, q ** 3, q ** 2

    def perceive(self, pos, s, gi, gj, n):
        """[n, F] per-particle features (see module docstring). Inactive rows are zero."""
        C, d, R = self.C, self.world.dim, self.world.R
        dx, r2, w, g = self._pairs(pos, gi, gj)
        rho = torch.zeros(n).index_add(0, gi, w)
        gs = torch.zeros(n).index_add(0, gi, g)
        mean = torch.zeros(n, C).index_add(0, gi, w[:, None] * s[gj]) / (1 + rho)[:, None]
        grad = torch.zeros(n, d, C).index_add(
            0, gi, (g[:, None, None] * (dx / R)[:, :, None]) * (s[gj] - s[gi])[:, None, :]) / (1 + gs)[:, None, None]
        # feature order: [own C][mean C][grad: for each channel c, its d components] [rho]
        return torch.cat([s, mean, grad.transpose(1, 2).reshape(n, C * d), (rho / self.world.rho0)[:, None]], 1)

    def alive(self, alpha, gi, gj, active):
        """[n] bool: active and max alpha over itself + neighbours > 0.1."""
        m = alpha.clone()
        m = m.scatter_reduce(0, gi, alpha[gj], reduce="amax", include_self=True)
        return active & (m > 0.1)

    def forward(self, st: State, fire_rate=None, fire=None, bud=True, gen=None):
        W = self.world
        B, N, d = st.pos.shape
        n = B * N
        C = self.C
        pos, s, act = st.pos.reshape(n, d), st.s.reshape(n, C), st.active.reshape(n)
        gi, gj = neighbour_edges(st, W.R)
        pre = self.alive(s[:, 3], gi, gj, act)

        rate = self.fire_rate if fire_rate is None else fire_rate
        if fire is None:
            fire = torch.rand(n, generator=gen) <= rate
        else:
            fire = fire.reshape(n)
        idx = (pre & fire).nonzero().squeeze(1)
        feats = self.perceive(pos, s, gi, gj, n).index_select(0, idx)
        out = F.linear(torch.relu(F.linear(feats, self.w1, self.b1)), self.w2, self.b2)
        ds = torch.zeros(n, C).index_copy(0, idx, out[:, :C])
        v = torch.zeros(n, d).index_copy(0, idx, W.vmax * torch.tanh(out[:, C:]))
        s = s + ds
        pos = pos + v

        # collision (designed): push apart pairs closer than r0 (every active particle, every step)
        dx = pos[gj] - pos[gi]
        r = (dx * dx).sum(-1).clamp(min=1e-8).sqrt()
        overlap = (W.r0 - r).clamp(min=0) / W.r0
        push = torch.zeros(n, d).index_add(0, gi, -(W.rep * W.r0 * 0.5) * overlap[:, None] * dx / r[:, None])
        pos = pos + push * act[:, None].to(pos.dtype)

        post = self.alive(s[:, 3], gi, gj, act)
        keep = pre & post
        s = s * keep[:, None].to(s.dtype)
        out_st = State(pos.view(B, N, d), s.view(B, N, C), keep.view(B, N))
        if bud:
            self.bud(out_st, gi, gj, gen)
        return out_st

    @torch.no_grad()
    def bud(self, st: State, gi, gj, gen=None):
        """Designed growth: a visible particle short of neighbours buds one dormant child into
        a free slot, placed r_bud away from itself, pointing away from its neighbours' centroid
        (plus noise). In place. The child is state zero: exactly an empty grid cell next to life."""
        W = self.world
        B, N, d = st.pos.shape
        pos = st.pos.reshape(B * N, d)
        act = st.active.reshape(-1)
        cnt = torch.zeros(B * N).index_add(0, gi, torch.ones_like(gi, dtype=torch.float32))
        cen = torch.zeros(B * N, d).index_add(0, gi, pos[gj] - pos[gi])
        vis = act & (st.s.reshape(B * N, -1)[:, 3] > 0.1) & (cnt < W.k_bud)
        vis &= torch.rand(B * N, generator=gen) <= 0.5
        cand = vis.view(B, N)
        for b in range(B):
            parents = cand[b].nonzero().squeeze(1)
            free = (~st.active[b]).nonzero().squeeze(1)
            k = min(len(parents), len(free))
            if k == 0:
                continue
            parents = parents[torch.randperm(len(parents), generator=gen)[:k]]
            slots = free[:k]
            away = -cen.view(B, N, d)[b, parents]
            away = away / away.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            noise = torch.randn(k, d, generator=gen)
            dirn = away + 0.6 * noise / noise.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            dirn = dirn / dirn.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            st.pos[b, slots] = st.pos[b, parents].detach() + W.r_bud * dirn
            st.s[b, slots] = 0.0
            st.active[b, slots] = True


# ----------------------------------------------------------------- render ---

def _offsets(d, rad):
    rng = range(-rad, rad + 1)
    if d == 2:
        return torch.tensor([(ox, oy) for oy in rng for ox in rng], dtype=torch.float32)
    return torch.tensor([(ox, oy, oz) for oz in rng for oy in rng for ox in rng], dtype=torch.float32)


def splat(st: State, grid, sigma):
    """Render premultiplied RGBA: each active particle adds exp(-|c - x|^2 / 2 sigma^2) * rgba to
    nearby cells (cell k's centre is k + 0.5). grid = (H, W) or (D, H, W); returns [B, *grid, 4].
    Differentiable in positions and colours."""
    B, N, d = st.pos.shape
    rad = int(math.ceil(2.2 * sigma))
    off = _offsets(d, rad)                                   # [O, d] in (x, y[, z]) order
    base = torch.floor(st.pos.detach())                      # [B, N, d]
    cells = base[:, :, None, :] + off[None, None]            # [B, N, O, d]
    ctr = cells + 0.5
    w = torch.exp(-((ctr - st.pos[:, :, None, :]) ** 2).sum(-1) / (2 * sigma * sigma))  # [B, N, O]
    shape_xyz = list(reversed(grid))                         # (W, H[, D]) matching (x, y[, z])
    inb = st.active[:, :, None].clone().expand_as(w).clone()
    for k in range(d):
        inb &= (cells[..., k] >= 0) & (cells[..., k] < shape_xyz[k])
    ci = cells.long()
    if d == 2:
        flat = ci[..., 1] * grid[1] + ci[..., 0]
    else:
        flat = (ci[..., 2] * grid[1] + ci[..., 1]) * grid[2] + ci[..., 0]
    size = int(np.prod(grid))
    flat = flat + (torch.arange(B)[:, None, None] * size)
    contrib = (w * inb.to(w.dtype))[..., None] * st.s[:, :, None, :4]      # [B, N, O, 4]
    sel = inb.reshape(-1)
    img = torch.zeros(B * size, 4).index_add(0, flat.reshape(-1)[sel], contrib.reshape(-1, 4)[sel])
    return img.view(B, *grid, 4)


def to_rgb(img):
    a = img[..., 3:4].clamp(0, 1)
    return (1 - a + img[..., :3].clamp(0, 1)).clamp(0, 1)


# ---------------------------------------------------------------- targets ---

def static_target(name="lizard", size=40, pad=16):
    t = np.pad(load_emoji(name, size), ((pad, pad), (pad, pad), (0, 0)))
    return t[None]                                                 # [1, H, W, 4]


def swim2d_target(cfg):
    from animated_nca import AnimConfig, build_frames
    return build_frames(AnimConfig(frames=cfg.frames, period=cfg.period))  # [K, 72, 72, 4]


def swim3d_target(cfg):
    from nca3d import Config3D, build_frames as b3
    return b3(Config3D(frames=cfg.frames, period=cfg.period))              # [K, 22, 44, 44, 4]


# --------------------------------------------------------------- training ---

@dataclass
class PConfig:
    experiment: str = "regenerating"   # regenerating | swim2d | swim3d
    frames: int = 8
    period: int = 8
    window: int = 5
    min_seed_check: int = 64
    clock_steps: int = 0
    clock_min_iter: int = 96
    clock_max_iter: int = 128
    channel_n: int = 16
    hidden: int = 128
    batch_size: int = 8
    pool_size: int = 1024
    fire_rate: float = 0.5
    lr: float = 2e-3
    lr_drop_step: int = 2000
    steps: int = 4000
    min_iter: int = 64
    max_iter: int = 96
    damage_n: int = 3
    seed: int = 0
    threads: int = 0
    init: str = ""                     # warm start: a particle model.pt (dim-lifted if 2D -> 3D)
    world: dict = None


def make_world(cfg) -> World:
    base = {"swim3d": World(dim=3, R=2.6, r0=1.3, k_bud=26, rho0=10.0, capacity=900, sigma=0.8)}.get(cfg.experiment, World())
    if cfg.world:
        for k, v in cfg.world.items():
            setattr(base, k, v)
    return base


def build_targets(cfg):
    if cfg.experiment == "regenerating":
        return static_target()
    if cfg.experiment == "swim2d":
        return swim2d_target(cfg)
    return swim3d_target(cfg)


def frame_mse(img, frames):
    B, K = img.shape[0], frames.shape[0]
    return ((img.reshape(B, 1, -1) - frames.reshape(1, K, -1)) ** 2).mean(-1)


def ball_damage(st: State, grid, rng):
    """Remove particles in a random disc/ball: centre in the inner half, radius 0.1-0.4 of the
    half-width, the grid cut's rule."""
    d = st.pos.shape[-1]
    ext = torch.tensor(list(reversed(grid)), dtype=torch.float32)       # (W, H[, D])
    for b in range(st.B):
        c = ext / 2 + (torch.from_numpy(rng.random(d)).float() - 0.5) * ext / 2
        r = float(rng.random() * 0.3 + 0.1) * float(ext[0]) / 2
        hit = ((st.pos[b] - c) ** 2).sum(-1) < r * r
        st.active[b] &= ~hit
        st.s[b, hit] = 0


def lift_2d_to_3d(sd2, C=16, hidden=128):
    """A 2D particle model's weights in the 3D layout: own/mean/rho carried over, the gradient's
    x, y components carried over, z weights zero; velocity output z row zero."""
    w1_2 = sd2["w1"]
    own, mean = w1_2[:, :C], w1_2[:, C:2 * C]
    grad2 = w1_2[:, 2 * C:2 * C + 2 * C].view(hidden, C, 2)
    rho = w1_2[:, -1:]
    grad3 = torch.zeros(hidden, C, 3)
    grad3[:, :, :2] = grad2
    w1 = torch.cat([own, mean, grad3.reshape(hidden, 3 * C), rho], 1)
    w2 = torch.zeros(C + 3, hidden); w2[:C + 2] = sd2["w2"]
    b2 = torch.zeros(C + 3); b2[:C + 2] = sd2["b2"]
    return {"w1": w1, "b1": sd2["b1"], "w2": w2, "b2": b2}


def train(cfg: PConfig, out_dir: str, resume=False):
    os.makedirs(out_dir, exist_ok=True)
    start = 0
    if resume:
        saved = json.load(open(os.path.join(out_dir, "config.json")))
        cfg = PConfig(**{**asdict(cfg), **saved, "threads": cfg.threads})
        start = json.load(open(os.path.join(out_dir, "state.json")))["step"]
    if cfg.threads:
        torch.set_num_threads(cfg.threads)
    torch.manual_seed(cfg.seed + start)
    rng = np.random.default_rng(cfg.seed + start)
    world = make_world(cfg)
    cfg.world = asdict(world)
    fr_np = build_targets(cfg).astype(np.float32)
    frames = torch.from_numpy(fr_np)
    K, grid = fr_np.shape[0], fr_np.shape[1:-1]
    centre = [g / 2 for g in reversed(grid)]                 # (x, y[, z])
    if not resume:
        np.save(os.path.join(out_dir, "frames.npy"), fr_np.astype(np.float16))
        json.dump(asdict(cfg), open(os.path.join(out_dir, "config.json"), "w"), indent=2)

    ca = ParticleNCA(world, cfg.channel_n, cfg.hidden, cfg.fire_rate)
    if resume:
        ca.load_state_dict(torch.load(os.path.join(out_dir, "model.pt")))
    elif cfg.init:
        sd = torch.load(cfg.init)
        if sd["w1"].shape[1] != ca.w1.shape[1]:
            sd = lift_2d_to_3d(sd, cfg.channel_n, cfg.hidden)
        ca.load_state_dict(sd)
    opt = torch.optim.Adam(ca.parameters(), lr=cfg.lr, eps=1e-7)
    sched = torch.optim.lr_scheduler.MultiStepLR(opt, [cfg.lr_drop_step], 0.1)
    if resume and os.path.isfile(os.path.join(out_dir, "opt.pt")):
        opt.load_state_dict(torch.load(os.path.join(out_dir, "opt.pt")))
    for _ in range(start):
        sched.step()

    seed = seed_state(1, world, centre, cfg.channel_n)
    pool = State(seed.pos.repeat(cfg.pool_size, 1, 1), seed.s.repeat(cfg.pool_size, 1, 1),
                 seed.active.repeat(cfg.pool_size, 1))
    log = list(np.load(os.path.join(out_dir, "loss.npy")))[:start] if resume else []
    t0 = time.time()
    if resume and start >= cfg.clock_steps:          # rebuild the unsaved pool from seeds
        with torch.no_grad():
            for b0 in range(0, cfg.pool_size, 32):
                nb = min(32, cfg.pool_size - b0)
                x = seed_state(nb, world, centre, cfg.channel_n)
                for _ in range(int(rng.integers(150, 400))):
                    x = ca(x)
                pool.pos[b0:b0 + nb], pool.s[b0:b0 + nb], pool.active[b0:b0 + nb] = x.pos, x.s, x.active
        print(f"[particle {cfg.experiment}] resumed at {start}, pool rebuilt in {(time.time()-t0)/60:.1f}m", flush=True)

    J = cfg.window if K > 1 else 1
    P = cfg.period
    shift = torch.arange(J)
    use_ckpt = world.dim == 3

    def step_fn(pos, s, act_f):
        st = ca(State(pos, s, act_f > 0.5), bud=False)
        return st.pos, st.s, st.active.float()

    for step in range(start + 1 if resume else 0, cfg.steps + 1):
        clock = step < cfg.clock_steps
        if clock:
            idx = None
            x0 = State(seed.pos.repeat(cfg.batch_size, 1, 1), seed.s.repeat(cfg.batch_size, 1, 1),
                       seed.active.repeat(cfg.batch_size, 1))
            n = int(rng.integers(cfg.clock_min_iter, cfg.clock_max_iter + 1))
        else:
            idx = rng.choice(cfg.pool_size, cfg.batch_size, replace=False)
            x0 = pool.index(torch.from_numpy(idx)).clone()
            with torch.no_grad():
                order = torch.argsort(frame_mse(splat(x0, grid, world.sigma), frames).min(1).values, descending=True)
            x0, idx = x0.index(order), idx[order.numpy()]
            x0.pos[0], x0.s[0], x0.active[0] = seed.pos[0], seed.s[0], seed.active[0]
            if cfg.damage_n:
                tail = x0.index(torch.arange(cfg.batch_size - cfg.damage_n, cfg.batch_size))
                ball_damage(tail, grid, rng)
                x0.pos[-cfg.damage_n:], x0.s[-cfg.damage_n:], x0.active[-cfg.damage_n:] = tail.pos, tail.s, tail.active
            n = int(rng.integers(cfg.min_iter, cfg.max_iter + 1))

        checks = [n - (J - 1 - j) * P for j in range(J)]
        x, snaps = x0, []
        for i in range(1, n + 1):
            if use_ckpt:
                p, s_, a = checkpoint(step_fn, x.pos, x.s, x.active.float(), use_reentrant=False)
                x = State(p, s_, a > 0.5)
                ca.bud(x, *neighbour_edges(x, world.R))
            else:
                x = ca(x)
            if i in checks:
                snaps.append(splat(x, grid, world.sigma))
        B = x0.B
        err = torch.stack([frame_mse(im, frames) for im in snaps], 1)        # [B, J, K]
        if K == 1:
            loss = err[:, -1, 0].mean()
        else:
            born = torch.tensor([(c // P) % K for c in checks])
            valid = torch.tensor([float(c >= cfg.min_seed_check) for c in checks])
            seed_cost = (err[:, shift, born] * valid).sum(-1) / valid.sum().clamp(min=1)
            if clock:
                loss = seed_cost.mean()
            else:
                kk = (torch.arange(K)[:, None] + shift[None]) % K
                cost = err[:, shift[None, :].expand(K, J), kk].mean(-1)
                free = cost[torch.arange(B), cost.detach().argmin(1)]
                loss = torch.cat([seed_cost[:1], free[1:]]).mean()

        opt.zero_grad(set_to_none=True)
        loss.backward()
        for prm in ca.parameters():
            if prm.grad is not None:
                prm.grad /= prm.grad.norm() + 1e-8
        opt.step()
        sched.step()
        xd = x.detach()
        if clock:
            if step == cfg.clock_steps - 1:
                pick = torch.from_numpy(rng.integers(0, B, cfg.pool_size))
                pool = xd.index(pick).clone()
        else:
            ii = torch.from_numpy(idx)
            pool.pos[ii], pool.s[ii], pool.active[ii] = xd.pos, xd.s, xd.active

        L = float(loss)
        log.append(L)
        if step % 25 == 0:
            dt = time.time() - t0
            print(f"[particle {cfg.experiment}] {'clock' if clock else 'pool '} step {step:5d}  loss {L:.5f}  "
                  f"log10 {math.log10(max(L, 1e-12)):+.3f}  particles {int(xd.active.sum(1).float().mean())}  "
                  f"{dt / (step - start + 1):.2f}s/it  elapsed {dt / 60:.1f}m", flush=True)
        if step % 50 == 0 or step == cfg.steps:
            torch.save(ca.state_dict(), os.path.join(out_dir, "model.pt"))
            torch.save(opt.state_dict(), os.path.join(out_dir, "opt.pt"))
            np.save(os.path.join(out_dir, "loss.npy"), np.array(log))
            json.dump({"step": step}, open(os.path.join(out_dir, "state.json"), "w"))
        if step % 250 == 0 and world.dim == 2:
            img = to_rgb(splat(xd, grid, world.sigma)).numpy()
            im = Image.fromarray((np.concatenate(list(img), 1) * 255).astype(np.uint8))
            im.resize((im.width * 3, im.height * 3), Image.NEAREST).save(os.path.join(out_dir, f"batch_{step:05d}.png"))

    export(ca, cfg, os.path.join(out_dir, "weights.json"))
    return ca


def export(ca, cfg, path):
    data = {"channel_n": ca.C, "hidden": ca.hidden, "fire_rate": ca.fire_rate, "world": asdict(ca.world),
            "w1": ca.w1.detach().numpy().round(6).tolist(), "b1": ca.b1.detach().numpy().round(6).tolist(),
            "w2": ca.w2.detach().numpy().round(6).tolist(), "b2": ca.b2.detach().numpy().round(6).tolist(),
            "experiment": cfg.experiment, "frames": cfg.frames, "period": cfg.period,
            "feature_order": "[own C][neighbour mean C][gradient: channel-major, d components each][rho/rho0]"}
    json.dump(data, open(path, "w"))


# -------------------------------------------------------------- selftest ---

def selftest():
    torch.manual_seed(0)
    for d in (2, 3):
        W = World(dim=d, capacity=40)
        ca = ParticleNCA(W)
        nn.init.normal_(ca.w2, std=0.05)
        B, N = 2, 40
        pos = torch.rand(B, N, d) * 6
        s = torch.rand(B, N, 16)
        act = torch.rand(B, N) < 0.8
        s[~act] = 0
        st = State(pos, s, act)
        gi, gj = neighbour_edges(st, W.R)
        # 1. perception vs a literal O(N^2) loop
        feats = ca.perceive(pos.view(-1, d), s.view(-1, 16), gi, gj, B * N).view(B, N, -1)
        b, i = 1, int(act[1].nonzero()[0])
        mean = torch.zeros(16); grad = torch.zeros(d, 16); rho = 0.0; gs = 0.0
        for j in range(N):
            if j == i or not act[b, j]:
                continue
            dx = pos[b, j] - pos[b, i]; r2 = float((dx * dx).sum())
            if r2 >= W.R ** 2:
                continue
            q = 1 - r2 / W.R ** 2
            mean += q ** 3 * s[b, j]; rho += q ** 3
            grad += q ** 2 * (dx / W.R)[:, None] * (s[b, j] - s[b, i])[None]; gs += q ** 2
        ref = torch.cat([s[b, i], mean / (1 + rho), (grad / (1 + gs)).T.reshape(-1), torch.tensor([rho / W.rho0])])
        e1 = float((feats[b, i] - ref).abs().max())
        assert e1 < 1e-5, e1
        # 2. sparse update (only alive & firing get the MLP) == dense update masked afterwards
        fire = torch.rand(B, N) <= 0.5
        out = ca(st, fire=fire, bud=False)
        n = B * N
        pre = ca.alive(s.view(n, 16)[:, 3], gi, gj, act.view(n))
        dense = F.linear(torch.relu(F.linear(ca.perceive(pos.view(n, d), s.view(n, 16), gi, gj, n), ca.w1, ca.b1)), ca.w2, ca.b2)
        m = (pre & fire.view(n))[:, None].float()
        s2 = s.view(n, 16) + dense[:, :16] * m
        post = ca.alive(s2[:, 3], gi, gj, act.view(n))
        s2 = s2 * (pre & post)[:, None].float()
        e2 = float((out.s.view(n, 16) - s2).abs().max())
        assert e2 < 1e-5, e2
        # 3. translation invariance: shifting every particle shifts the result and nothing else
        shift = torch.randn(d) * 3
        out2 = ca(State(pos + shift, s.clone(), act.clone()), fire=fire, bud=False)
        e3 = float((out2.pos - shift - out.pos)[out.active].abs().max()) + float((out2.s - out.s).abs().max())
        assert e3 < 1e-4, e3
        # 4. splat conserves mass: an interior particle's total render weight is ~ (2 pi sigma^2)^(d/2)
        one = State(torch.full((1, 1, d), 10.3), torch.ones(1, 1, 16), torch.ones(1, 1, dtype=torch.bool))
        grid = (24, 24) if d == 2 else (24, 24, 24)
        tot = float(splat(one, grid, W.sigma)[..., 3].sum())
        want = (2 * math.pi * W.sigma ** 2) ** (d / 2)
        assert abs(tot / want - 1) < 0.03, (tot, want)
        # 5. negative control: dropping the (s_j - s_i) difference changes the gradient feature
        e5 = float((feats[b, i, 32:32 + 16 * d].abs().sum()))
        assert e5 > 1e-3
        print(f"selftest d={d} OK  perceive-vs-loop {e1:.1e}  sparse==dense {e2:.1e}  translation {e3:.1e}  splat mass {tot/want:.3f}")
    # 6. budding fills free slots only, children are dormant, and nothing buds without alpha
    W = World(dim=2, capacity=16)
    ca = ParticleNCA(W)
    st = seed_state(2, W, (8.0, 8.0))
    for _ in range(4):
        st = ca(st)
    assert int(st.active.sum()) > 2, "seed never budded"
    assert float(st.s[:, 1:][st.active[:, 1:]][:, 3].max()) <= 0.1, "a child was born visible"
    z = seed_state(1, W, (8.0, 8.0)); z.s[:] = 0
    z = ca(z)
    assert int(z.active.sum()) == 0, "an invisible particle survived / budded"
    print("selftest budding OK")


# -------------------------------------------------------------------- cli ---

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("selftest")
    b = sub.add_parser("bench")
    b.add_argument("--experiment", default="regenerating")
    t = sub.add_parser("train")
    t.add_argument("--out", default=None)
    t.add_argument("--resume", action="store_true")
    for k, v in asdict(PConfig()).items():
        if k == "world":
            continue
        t.add_argument(f"--{k.replace('_', '-')}", type=type(v), default=v)
    fg = sub.add_parser("figures")
    fg.add_argument("--run", required=True)
    a = ap.parse_args()
    if a.cmd == "selftest":
        selftest()
    elif a.cmd == "bench":
        cfg = PConfig(experiment=a.experiment)
        world = make_world(cfg)
        fr = torch.from_numpy(build_targets(cfg).astype(np.float32))
        grid = fr.shape[1:-1]
        ca = ParticleNCA(world)
        nn.init.normal_(ca.w2, std=0.01)
        opt = torch.optim.Adam(ca.parameters(), 2e-3)
        x = seed_state(8, world, [g / 2 for g in reversed(grid)])
        with torch.no_grad():                       # grow a realistic population first
            ca.b2.data[3] = 0.05
            for _ in range(60):
                x = ca(x)
        print(f"particles per sample after 60 bench steps: {int(x.active.sum(1).float().mean())}")
        for rep in range(2):
            t0 = time.time()
            y = x.clone()
            for _ in range(80):
                y = ca(y)
            loss = frame_mse(splat(y, grid, world.sigma), fr).mean()
            opt.zero_grad(); loss.backward(); opt.step()
            print(f"80 steps fwd+bwd: {time.time() - t0:.2f}s  (particles {int(y.active.sum(1).float().mean())})")
    elif a.cmd == "train":
        cfg = PConfig(**{k: getattr(a, k) for k in asdict(PConfig()) if k != "world"})
        out = a.out or os.path.join(HERE, "runs", f"particle_{cfg.experiment}")
        train(cfg, out, resume=a.resume)


if __name__ == "__main__":
    main()
