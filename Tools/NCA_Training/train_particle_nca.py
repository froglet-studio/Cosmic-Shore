"""
train_particle_nca.py — Train a learned CONTINUOUS automaton (Particle NCA)
that converges to a specified, animated 3D shape.

This is the continuous-space generalization of Growing Neural Cellular
Automata (distill.pub/2020/growing-ca/): instead of pixels on a 2D grid,
the automaton is a population of particles in continuous 3D space. Each
particle carries hidden state channels and updates via a SHARED, LOCAL,
LEARNED rule:

    perception (kNN neighborhood statistics + animation phase)
        -> dense(hidden, ReLU) -> dense(3 + C, linear)
        -> [delta_position(3), delta_state(C)]

The perception operator is the continuous analog of the NCA's
identity + Sobel filters:

    own state            h_i                                   (C values)
    "Laplacian"          weighted mean of (h_j - h_i)          (C values)
    "Sobel gradient"     weighted sum of (h_j - h_i) * dir_ij  (3C values)
    center-of-mass dir   weighted mean of (p_j - p_i)          (3 values)
    density              sum of hat-kernel weights / K         (1 value)
    animation phase      sin(2*pi*t), cos(2*pi*t)              (2 values)
                                                  total = 5C + 6

Time conditioning via the phase input lets a single rule produce a looping
ANIMATION rather than a static shape: the target point cloud is a function
of phase, and the pool-based curriculum (sample pool + seed reinjection +
damage, exactly as in the Distill paper) trains the rule to grow the shape
from a seed, track the animation, and self-heal after damage.

Everything in the model is deliberately replicable in an HLSL compute
shader (see Assets/_Scripts/Controller/Automata/ParticleNCA/): fixed-
function perception, two dense layers, tanh-bounded position step,
stochastic fire mask.

Usage:
    # Train against a built-in procedural animated target:
    python train_particle_nca.py --target sphere_torus --output weights.bytes \
        --steps 3000 --gif convergence.gif

    # Built-in targets: pulse_sphere | sphere_torus | jelly | helix
    # Or a .npz with array 'frames' of shape [F, M, 3] (a looping sequence):
    python train_particle_nca.py --target my_animation.npz --output weights.bytes

    # Re-render a GIF from a saved checkpoint without retraining:
    python train_particle_nca.py --target sphere_torus --render-only \
        --checkpoint weights.bytes.ckpt.pt --gif convergence.gif

Output .bytes layout (all little-endian), self-describing for Unity:
    char[4]  magic            'PNCA'
    int32    version          1
    int32    channelCount     C
    int32    kNeighbors       K
    int32    hiddenSize       H
    int32    perceptionDim    5C + 6
    int32    outputDim        3 + C
    int32    stepsPerLoop     automaton steps per animation loop
    float32  perceptionRadius
    float32  stepScale        max position step per update
    float32  fireRate
    float32  reserved         0
    float32[perceptionDim * H]  weights1  (row-major: [input, output])
    float32[H]                  biases1
    float32[H * outputDim]      weights2
    float32[outputDim]          biases2
"""

import argparse
import math
import os
import struct
import time

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

MAGIC = b'PNCA'
VERSION = 1


# ── Animated 3D targets ─────────────────────────────────────────────────
# Each generator maps phase in [0,1) -> point cloud [M, 3] in a roughly
# unit-radius volume. The animation must loop (phase 0 == phase 1).

def fibonacci_sphere(m):
    """Evenly distributed unit-sphere points."""
    i = np.arange(m, dtype=np.float64) + 0.5
    phi = np.arccos(1.0 - 2.0 * i / m)
    theta = np.pi * (1.0 + 5.0 ** 0.5) * i
    return np.stack([
        np.sin(phi) * np.cos(theta),
        np.sin(phi) * np.sin(theta),
        np.cos(phi),
    ], axis=-1).astype(np.float32)


def target_pulse_sphere(phase, m):
    """A breathing sphere: radius oscillates, slightly egg-shaped at peak."""
    pts = fibonacci_sphere(m)
    breathe = 0.75 + 0.25 * math.sin(2.0 * math.pi * phase)
    stretch = 1.0 + 0.15 * math.sin(2.0 * math.pi * phase)
    out = pts * breathe
    out[:, 1] *= stretch
    return out


def target_sphere_torus(phase, m):
    """Smooth looping morph between a sphere and a torus.

    Both shapes are parametrized on the same (u, v) grid so each target
    point travels a continuous path through the morph — the animation is
    a genuine topology-bending deformation, not a crossfade.
    """
    side = int(math.ceil(math.sqrt(m)))
    u = (np.arange(side) + 0.5) / side
    v = (np.arange(side) + 0.5) / side
    uu, vv = np.meshgrid(u, v)
    uu = uu.flatten()[:m]
    vv = vv.flatten()[:m]

    # Sphere: u -> latitude, v -> longitude
    lat = uu * np.pi
    lon = vv * 2.0 * np.pi
    sphere = np.stack([
        np.sin(lat) * np.cos(lon),
        np.cos(lat),
        np.sin(lat) * np.sin(lon),
    ], axis=-1) * 0.9

    # Torus: u -> tube angle, v -> ring angle
    ring_r, tube_r = 0.7, 0.3
    ta = uu * 2.0 * np.pi
    ra = vv * 2.0 * np.pi
    torus = np.stack([
        (ring_r + tube_r * np.cos(ta)) * np.cos(ra),
        tube_r * np.sin(ta),
        (ring_r + tube_r * np.cos(ta)) * np.sin(ra),
    ], axis=-1)

    blend = 0.5 - 0.5 * math.cos(2.0 * math.pi * phase)  # 0 -> 1 -> 0
    return ((1.0 - blend) * sphere + blend * torus).astype(np.float32)


def target_jelly(phase, m):
    """A swimming jellyfish: pulsing bell + four swaying tendrils."""
    m_bell = int(m * 0.7)
    m_tend = m - m_bell

    # Bell: hemisphere whose profile undulates with the swim pulse.
    pts = fibonacci_sphere(m_bell * 2)
    pts = pts[pts[:, 1] > 0.0][:m_bell]
    while pts.shape[0] < m_bell:
        pts = np.concatenate([pts, pts[: m_bell - pts.shape[0]]], axis=0)
    pulse = math.sin(2.0 * math.pi * phase)
    flare = 1.0 + 0.2 * pulse                      # bell opens and closes
    squash = 1.0 - 0.25 * pulse                    # and flattens as it opens
    bob = 0.12 * math.sin(2.0 * math.pi * phase + 0.8)
    bell = pts.copy()
    bell[:, 0] *= 0.8 * flare
    bell[:, 2] *= 0.8 * flare
    bell[:, 1] = bell[:, 1] * 0.55 * squash + 0.15 + bob

    # Tendrils: four hanging curves that sway behind the pulse.
    per = m_tend // 4
    tendrils = []
    for k in range(4):
        n = per if k < 3 else m_tend - 3 * per
        s = (np.arange(n) + 0.5) / max(n, 1)       # 0 at bell rim -> 1 at tip
        ang = k * math.pi / 2.0 + 0.3
        sway = 0.25 * np.sin(2.0 * math.pi * phase - s * 2.2) * s
        x = 0.45 * math.cos(ang) + sway * math.cos(ang + 1.5)
        z = 0.45 * math.sin(ang) + sway * math.sin(ang + 1.5)
        y = 0.1 + bob - s * 1.1
        tendrils.append(np.stack([x * np.ones(n) if np.isscalar(x) else x,
                                  y,
                                  z * np.ones(n) if np.isscalar(z) else z], axis=-1))
    return np.concatenate([bell] + tendrils, axis=0).astype(np.float32)


def target_helix(phase, m):
    """A rotating double helix."""
    per = m // 2
    out = []
    for strand in range(2):
        n = per if strand == 0 else m - per
        s = (np.arange(n) + 0.5) / n
        ang = s * 4.0 * np.pi + strand * np.pi + phase * 2.0 * np.pi
        out.append(np.stack([
            0.55 * np.cos(ang),
            (s - 0.5) * 2.0,
            0.55 * np.sin(ang),
        ], axis=-1))
    return np.concatenate(out, axis=0).astype(np.float32)


BUILTIN_TARGETS = {
    'pulse_sphere': target_pulse_sphere,
    'sphere_torus': target_sphere_torus,
    'jelly': target_jelly,
    'helix': target_helix,
}


class AnimatedTarget:
    """Wraps a phase -> [M, 3] generator, precomputing a dense frame bank."""

    def __init__(self, name_or_path, m_points, frame_bank=96, device='cpu'):
        if name_or_path in BUILTIN_TARGETS:
            gen = BUILTIN_TARGETS[name_or_path]
            frames = np.stack([gen(f / frame_bank, m_points) for f in range(frame_bank)])
        elif name_or_path.endswith('.npz'):
            data = np.load(name_or_path)
            frames = data['frames'].astype(np.float32)  # [F, M, 3]
            # Normalize into a unit-ish volume
            center = frames.reshape(-1, 3).mean(axis=0)
            frames = frames - center
            scale = np.abs(frames).max()
            frames = frames / max(scale, 1e-6)
        else:
            raise ValueError(
                f"Unknown target '{name_or_path}'. "
                f"Use one of {list(BUILTIN_TARGETS)} or a .npz with 'frames' [F, M, 3].")
        self.frames = torch.from_numpy(frames).to(device)  # [F, M, 3]
        self.frame_count = self.frames.shape[0]

    def at_phase(self, phase):
        """phase: tensor [B] in [0,1) -> [B, M, 3] (nearest frame)."""
        idx = (phase * self.frame_count).long() % self.frame_count
        return self.frames[idx]


# ── Particle NCA model ──────────────────────────────────────────────────

class ParticleNCA(nn.Module):
    def __init__(self, channel_count=8, k_neighbors=12, hidden_size=96,
                 perception_radius=1.0, step_scale=0.05, fire_rate=0.5,
                 steps_per_loop=96, state_clamp=4.0):
        super().__init__()
        self.channel_count = channel_count
        self.k_neighbors = k_neighbors
        self.hidden_size = hidden_size
        self.perception_radius = perception_radius
        self.step_scale = step_scale
        self.fire_rate = fire_rate
        self.steps_per_loop = steps_per_loop
        self.state_clamp = state_clamp

        self.perception_dim = 5 * channel_count + 6
        self.output_dim = 3 + channel_count

        self.fc1 = nn.Linear(self.perception_dim, hidden_size)
        self.fc2 = nn.Linear(hidden_size, self.output_dim)
        # Zero-initialize the output layer ("do nothing" initial rule),
        # exactly as in the Growing NCA paper.
        nn.init.zeros_(self.fc2.weight)
        nn.init.zeros_(self.fc2.bias)

    def perceive(self, p, h, phase):
        """p: [B,N,3], h: [B,N,C], phase: [B] -> perception [B,N,5C+6]."""
        b, n, _ = p.shape
        k = self.k_neighbors

        dist = torch.cdist(p, p)                                   # [B,N,N]
        dist = dist + torch.eye(n, device=p.device) * 1e9          # exclude self
        nd, idx = dist.topk(k, dim=-1, largest=False)              # [B,N,K]

        batch_idx = torch.arange(b, device=p.device).view(b, 1, 1)
        pj = p[batch_idx, idx]                                     # [B,N,K,3]
        hj = h[batch_idx, idx]                                     # [B,N,K,C]

        d = pj - p.unsqueeze(2)                                    # [B,N,K,3]
        r = nd.clamp_min(1e-6).unsqueeze(-1)                       # [B,N,K,1]
        direction = d / r                                          # unit offsets
        w = (1.0 - nd / self.perception_radius).clamp(0.0, 1.0).unsqueeze(-1)
        wsum = w.sum(dim=2).clamp_min(1e-6)                        # [B,N,1]

        hd = hj - h.unsqueeze(2)                                   # [B,N,K,C]
        lap = (hd * w).sum(dim=2) / wsum                           # [B,N,C]
        grad = (hd.unsqueeze(-1) * direction.unsqueeze(3) * w.unsqueeze(3)).sum(dim=2) \
            / wsum.unsqueeze(-1)                                   # [B,N,C,3]
        mu = (d * w).sum(dim=2) / wsum                             # [B,N,3]
        density = w.sum(dim=2) / k                                 # [B,N,1]

        two_pi_t = 2.0 * math.pi * phase
        ph = torch.stack([torch.sin(two_pi_t), torch.cos(two_pi_t)], dim=-1)
        ph = ph.view(b, 1, 2).expand(b, n, 2)

        return torch.cat([h, lap, grad.flatten(2), mu, density, ph], dim=-1)

    def step(self, p, h, phase):
        """One automaton step. Returns (p', h', phase')."""
        percep = self.perceive(p, h, phase)
        x = F.relu(self.fc1(percep))
        out = self.fc2(x)

        dp = torch.tanh(out[..., :3]) * self.step_scale
        dh = out[..., 3:]

        # The fire mask stays stochastic in eval too — the deployed automaton
        # is asynchronous by design, so train and runtime see the same regime.
        fire = (torch.rand(p.shape[0], p.shape[1], 1, device=p.device)
                <= self.fire_rate).float()

        p = p + dp * fire
        h = (h + dh * fire).clamp(-self.state_clamp, self.state_clamp)
        phase = (phase + 1.0 / self.steps_per_loop) % 1.0
        return p, h, phase

    def rollout(self, p, h, phase, steps):
        for _ in range(steps):
            p, h, phase = self.step(p, h, phase)
        return p, h, phase


def make_seed(batch, n_particles, channel_count, device, radius=0.12):
    """Tight gaussian ball of particles at the origin, vitality channel lit."""
    p = torch.randn(batch, n_particles, 3, device=device) * radius
    h = torch.zeros(batch, n_particles, channel_count, device=device)
    h[..., 0] = 1.0
    return p, h


def chamfer(a, b):
    """Symmetric Chamfer distance. a: [B,N,3], b: [B,M,3] -> [B]."""
    d = torch.cdist(a, b)                                          # [B,N,M]
    return d.min(dim=2).values.pow(2).mean(dim=1) \
        + d.min(dim=1).values.pow(2).mean(dim=1)


def scatter_damage(p, h, center, radius, scatter_radius=0.6):
    """Continuous-automata damage: particles inside the sphere get blasted
    to random positions and their state wiped. (Particle count is conserved
    — the analog of zeroing pixels in the 2D NCA.)

    scatter_radius must keep blasted particles within perceptual contact of
    the remaining body: the hat-kernel weights vanish beyond the perception
    radius, and an informationally disconnected particle cannot learn to
    rejoin the collective."""
    mask = ((p - center).norm(dim=-1) < radius).unsqueeze(-1)      # [N,1] or [B,N,1]
    blast = torch.randn_like(p) * scatter_radius
    p = torch.where(mask, blast, p)
    h = torch.where(mask, torch.zeros_like(h), h)
    return p, h


# ── Export ──────────────────────────────────────────────────────────────

def export_weights(model, output_path):
    """Write the self-describing PNCA .bytes file for Unity."""
    state = model.state_dict()
    w1 = state['fc1.weight'].cpu().numpy().T.astype(np.float32)   # [in, out]
    b1 = state['fc1.bias'].cpu().numpy().astype(np.float32)
    w2 = state['fc2.weight'].cpu().numpy().T.astype(np.float32)
    b2 = state['fc2.bias'].cpu().numpy().astype(np.float32)

    header = struct.pack(
        '<4siiiiiiiffff',
        MAGIC, VERSION,
        model.channel_count, model.k_neighbors, model.hidden_size,
        model.perception_dim, model.output_dim, model.steps_per_loop,
        model.perception_radius, model.step_scale, model.fire_rate, 0.0)

    os.makedirs(os.path.dirname(output_path) or '.', exist_ok=True)
    with open(output_path, 'wb') as f:
        f.write(header)
        f.write(w1.tobytes())
        f.write(b1.tobytes())
        f.write(w2.tobytes())
        f.write(b2.tobytes())

    total = len(header) + 4 * (w1.size + b1.size + w2.size + b2.size)
    print(f"Exported {total} bytes to {output_path}")
    print(f"  header: C={model.channel_count} K={model.k_neighbors} "
          f"H={model.hidden_size} perception={model.perception_dim} "
          f"out={model.output_dim} stepsPerLoop={model.steps_per_loop}")


# ── Rendering ───────────────────────────────────────────────────────────

def render_gif(model, target, args, gif_path):
    """Render the full story: grow from seed -> animate -> damage -> heal."""
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from PIL import Image

    device = next(model.parameters()).device
    model.eval()

    p, h = make_seed(1, args.particles, model.channel_count, device)
    phase = torch.zeros(1, device=device)

    spl = model.steps_per_loop
    grow_steps = spl * 2          # two loops to grow + settle into the animation
    anim_steps = spl * 2          # two clean loops of the animation
    heal_steps = int(spl * 2)     # recovery after damage
    sample_every = 3

    frames = []
    labels = []

    def capture(label):
        frames.append((p[0].detach().cpu().numpy().copy(),
                       h[0, :, 0].detach().cpu().numpy().copy(),
                       target.at_phase(phase)[0].cpu().numpy().copy()))
        labels.append(label)

    with torch.no_grad():
        for s in range(grow_steps):
            p, h, phase = model.step(p, h, phase)
            if s % sample_every == 0:
                capture('GROW' if s < spl else 'ANIMATE')
        for s in range(anim_steps):
            p, h, phase = model.step(p, h, phase)
            if s % sample_every == 0:
                capture('ANIMATE')
        p, h = scatter_damage(p, h, torch.tensor([0.4, 0.3, 0.0], device=device), 0.65)
        capture('DAMAGE')
        for s in range(heal_steps):
            p, h, phase = model.step(p, h, phase)
            if s % sample_every == 0:
                capture('SELF-HEAL')

    print(f"Rendering {len(frames)} frames to {gif_path} ...")
    images = []
    lim = 1.4
    for i, ((pts, vit, tgt), label) in enumerate(zip(frames, labels)):
        fig = plt.figure(figsize=(8, 4.2), facecolor='#0a0a14')
        for col, (cloud, color_by, title) in enumerate([
                (pts, vit, f'Particle NCA — {label}'),
                (tgt, None, 'Specified target animation')]):
            ax = fig.add_subplot(1, 2, col + 1, projection='3d', facecolor='#0a0a14')
            if color_by is not None:
                sc = ax.scatter(cloud[:, 0], cloud[:, 2], cloud[:, 1],
                                c=color_by, cmap='spring', s=6, alpha=0.9,
                                vmin=-1, vmax=2)
            else:
                ax.scatter(cloud[:, 0], cloud[:, 2], cloud[:, 1],
                           c='#3ad6c0', s=4, alpha=0.55)
            ax.set_xlim(-lim, lim); ax.set_ylim(-lim, lim); ax.set_zlim(-lim, lim)
            ax.set_axis_off()
            ax.set_title(title, color='#d0d0e8', fontsize=10)
            ax.view_init(elev=18, azim=35 + i * 0.6)
        fig.tight_layout(pad=0.2)
        fig.canvas.draw()
        buf = np.asarray(fig.canvas.buffer_rgba())
        images.append(Image.fromarray(buf[..., :3]))
        plt.close(fig)

    images[0].save(gif_path, save_all=True, append_images=images[1:],
                   duration=55, loop=0)
    print(f"Wrote {gif_path}")


# ── Training ────────────────────────────────────────────────────────────

def train(args):
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    print(f"Training on {device}")

    model = ParticleNCA(
        channel_count=args.channels, k_neighbors=args.k, hidden_size=args.hidden,
        perception_radius=args.radius, step_scale=args.step_scale,
        fire_rate=args.fire_rate, steps_per_loop=args.steps_per_loop).to(device)

    target = AnimatedTarget(args.target, args.target_points,
                            frame_bank=args.steps_per_loop, device=device)
    print(f"Target '{args.target}': {target.frame_count} frames x "
          f"{target.frames.shape[1]} points")

    optimizer = torch.optim.Adam(model.parameters(), lr=args.lr)

    # Sample pool (Distill curriculum): persists rollout endpoints so the
    # rule learns long-term stability across all phases of the animation.
    pool_p, pool_h = make_seed(args.pool, args.particles, args.channels, device)
    pool_phase = torch.zeros(args.pool, device=device)

    os.makedirs(os.path.dirname(args.output) or '.', exist_ok=True)
    ckpt_path = args.output + '.ckpt.pt'
    t0 = time.time()
    model.train()

    for it in range(args.steps):
        if it == int(args.steps * 0.6):
            for pg in optimizer.param_groups:
                pg['lr'] = args.lr * 0.3

        idx = np.random.choice(args.pool, args.batch, replace=False)
        p = pool_p[idx].clone()
        h = pool_h[idx].clone()
        phase = pool_phase[idx].clone()

        # Canonical Distill curriculum: rank the batch by current loss.
        # The WORST entry is replaced with a fresh seed (this both keeps
        # "grow from nothing" trained and continuously purges poisoned pool
        # entries); the BEST entry receives damage (damaging an already-bad
        # state would compound and poison the pool).
        with torch.no_grad():
            pre_loss = chamfer(p, target.at_phase(phase))
            order = pre_loss.argsort(descending=True)
        worst = order[0].item()
        sp, sh = make_seed(1, args.particles, args.channels, device)
        p[worst], h[worst] = sp[0], sh[0]
        phase[worst] = float(np.random.rand())  # seed may start at any phase

        if args.damage and it > args.steps // 6 and args.batch > 1:
            best = order[-1].item()
            with torch.no_grad():
                center = torch.randn(3, device=device) * 0.4
                radius = 0.25 + 0.35 * float(np.random.rand())
                p[best], h[best] = scatter_damage(p[best], h[best], center, radius)

        # Rollout with dense supervision: Chamfer at the last three
        # checkpoints (final, -8, -16 steps), weighted toward the end. The
        # animated target moves during the rollout, so per-checkpoint losses
        # teach continuous TRACKING — end-only loss converges far too slowly
        # at CPU-scale iteration budgets.
        n_steps = np.random.randint(args.rollout_min, args.rollout_max + 1)
        out_p, out_h, out_phase = p, h, phase
        chamfer_loss = 0.0
        per_sample = None
        for s in range(n_steps):
            out_p, out_h, out_phase = model.step(out_p, out_h, out_phase)
            remaining = n_steps - 1 - s
            if remaining == 16 or remaining == 8:
                chamfer_loss = chamfer_loss + 0.2 * chamfer(
                    out_p, target.at_phase(out_phase)).mean()
            elif remaining == 0:
                per_sample = chamfer(out_p, target.at_phase(out_phase))
                chamfer_loss = chamfer_loss + 0.6 * per_sample.mean()

        # Keep hidden states bounded (overflow loss, as in later NCA work).
        overflow = out_h.abs().clamp_min(1.0).sub(1.0).mean()
        loss = chamfer_loss + 0.01 * overflow

        optimizer.zero_grad()
        loss.backward()
        with torch.no_grad():
            for prm in model.parameters():
                if prm.grad is not None:
                    prm.grad /= (prm.grad.norm() + 1e-8)
        optimizer.step()

        with torch.no_grad():
            pool_p[idx] = out_p.detach()
            pool_h[idx] = out_h.detach()
            pool_phase[idx] = out_phase.detach()

        if it % 50 == 0:
            elapsed = time.time() - t0
            print(f"iter {it:5d}/{args.steps}  loss {loss.item():.5f}  "
                  f"chamfer {per_sample.mean().item():.5f}  "
                  f"({elapsed:.0f}s, {elapsed / max(it, 1):.2f}s/it)", flush=True)

        if it % 500 == 0 and it > 0:
            # Ground-truth convergence signal: grow from a fresh seed for
            # 1.5 loops with no grad and measure chamfer against the target.
            with torch.no_grad():
                ep, eh = make_seed(1, args.particles, args.channels, device)
                ephase = torch.zeros(1, device=device)
                ep, eh, ephase = model.rollout(ep, eh, ephase,
                                               int(args.steps_per_loop * 1.5))
                eval_chamfer = chamfer(ep, target.at_phase(ephase)).item()
            print(f"  [eval] from-seed chamfer after 1.5 loops: {eval_chamfer:.5f}",
                  flush=True)

        if it % 250 == 0 or it == args.steps - 1:
            torch.save({'model': model.state_dict(), 'iter': it,
                        'args': vars(args)}, ckpt_path)

    export_weights(model, args.output)
    torch.save({'model': model.state_dict(), 'iter': args.steps,
                'args': vars(args)}, ckpt_path)
    print(f"Checkpoint: {ckpt_path}")

    if args.gif:
        render_gif(model, target, args, args.gif)


def main():
    ap = argparse.ArgumentParser(description='Train a Particle NCA (learned continuous automaton) against an animated 3D target')
    ap.add_argument('--target', type=str, required=True,
                    help=f"Built-in target {list(BUILTIN_TARGETS)} or .npz path with 'frames' [F, M, 3]")
    ap.add_argument('--output', type=str, default=None,
                    help='Output .bytes path for Unity (required unless --render-only)')
    ap.add_argument('--gif', type=str, default=None, help='Optional convergence GIF path')
    ap.add_argument('--steps', type=int, default=3000, help='Training iterations')
    ap.add_argument('--particles', type=int, default=256, help='Automaton particle count')
    ap.add_argument('--target-points', type=int, default=384, help='Target cloud sample count')
    ap.add_argument('--channels', type=int, default=8, help='Hidden state channels C')
    ap.add_argument('--k', type=int, default=12, help='kNN neighbors')
    ap.add_argument('--hidden', type=int, default=96, help='MLP hidden size')
    ap.add_argument('--radius', type=float, default=1.0, help='Perception radius')
    ap.add_argument('--step-scale', type=float, default=0.08, help='Max position delta per step')
    ap.add_argument('--fire-rate', type=float, default=0.5, help='Stochastic update probability')
    ap.add_argument('--steps-per-loop', type=int, default=96, help='Automaton steps per animation loop')
    ap.add_argument('--rollout-min', type=int, default=32, help='Min BPTT rollout length')
    ap.add_argument('--rollout-max', type=int, default=56, help='Max BPTT rollout length')
    ap.add_argument('--pool', type=int, default=64, help='Sample pool size')
    ap.add_argument('--batch', type=int, default=4, help='Batch size')
    ap.add_argument('--lr', type=float, default=1e-3, help='Learning rate')
    ap.add_argument('--damage', action='store_true', default=True, help='Damage curriculum (self-healing)')
    ap.add_argument('--no-damage', dest='damage', action='store_false')
    ap.add_argument('--seed', type=int, default=42, help='RNG seed')
    ap.add_argument('--render-only', action='store_true', help='Skip training; render GIF from --checkpoint')
    ap.add_argument('--checkpoint', type=str, default=None, help='Checkpoint .pt for --render-only')

    args = ap.parse_args()

    if not args.render_only and not args.output:
        ap.error('--output is required for training')

    if args.render_only:
        if not args.checkpoint or not args.gif:
            ap.error('--render-only requires --checkpoint and --gif')
        device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
        ckpt = torch.load(args.checkpoint, map_location=device, weights_only=False)
        saved = ckpt.get('args', {})
        model = ParticleNCA(
            channel_count=saved.get('channels', args.channels),
            k_neighbors=saved.get('k', args.k),
            hidden_size=saved.get('hidden', args.hidden),
            perception_radius=saved.get('radius', args.radius),
            step_scale=saved.get('step_scale', args.step_scale),
            fire_rate=saved.get('fire_rate', args.fire_rate),
            steps_per_loop=saved.get('steps_per_loop', args.steps_per_loop)).to(device)
        model.load_state_dict(ckpt['model'])
        target = AnimatedTarget(args.target, args.target_points,
                                frame_bank=model.steps_per_loop, device=device)
        render_gif(model, target, args, args.gif)
        return

    train(args)


if __name__ == '__main__':
    main()
