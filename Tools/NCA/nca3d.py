"""3D animated Neural CA — the swim, with one more spatial dimension.

The cell is the Growing-NCA cell (Mordvintsev et al., Distill 2020) with one more axis
and nothing else changed:
  perceive  = [identity, Sobel_x, Sobel_y, Sobel_z] depthwise over 16 channels -> 64
              (3D Sobel = derivative [-1,0,1] along one axis, smoothing [1,2,1] along the
              other two, /32 so a unit ramp reads 1 — the same normalisation as the 2D /8)
  update    = 1x1x1 conv 64->128, ReLU, 128->16, last layer zero-init  (10,384 params)
  stochastic= each cell fires with p = 0.5
  alive     = max alpha over the 3x3x3 neighbourhood > 0.1, before AND after

The target is the Noto lizard INFLATED into a body (a pillow: half-thickness grows with the
square root of distance from the silhouette edge, so the spine is thick and the toes thin),
coloured from the emoji, animated by a HELICAL travelling wave: the body is displaced along
its in-plane normal by sin(phase) and along depth by cos(phase), so the tail traces a circle.
That motion does not exist in 2D.

Training is animated_nca's two-stage recipe (clock from birth, then pool + damage with
phase-free consecutive-frame matching), with spherical cuts. See animated_nca.py for why
stage 1 exists (phase-free training alone collapses to a still frame).

    python3 Tools/NCA/nca3d.py selftest
    python3 Tools/NCA/nca3d.py target --out /tmp/swim3d.gif      # render the target loop
    python3 Tools/NCA/nca3d.py bench
    python3 Tools/NCA/nca3d.py train
    python3 Tools/NCA/nca3d.py figures --run Tools/NCA/runs/lizard3d_swim

A second target in the game's own vocabulary: --target whale voxelises the prism humpback
(whale_target.py's prisms.json, its own 8-frame swim) with prism_frames(), D axis = UP. CPU recipe
(one thread is far faster than four on a shared box), warm-started from the lizard:

    OMP_NUM_THREADS=1 nohup python3 Tools/NCA/nca3d.py train --target whale --out Tools/NCA/runs/whale3d_swim \
        --voxel-scale 0.5 --pad-xy 3 --init3d Tools/NCA/results/lizard3d_swim/model.pt --threads 1 \
        --batch-size 4 --damage-n 1 --pool-size 256 --steps 6000 --clock-steps 800 --lr-drop-step 4000 \
        > Tools/NCA/runs/whale3d_swim.log 2>&1 &
    python3 Tools/NCA/nca3d.py train --out Tools/NCA/runs/whale3d_swim --resume --threads 1   # after a restart
    python3 Tools/NCA/export_whale_creature.py     # -> flight/creatures/nca_whale.js (window.NcaWhale)
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
from growing_nca import emoji_canvas, write_gif  # noqa: E402


# ----------------------------------------------------------------- model ---

class CA3D(nn.Module):
    """State is [B, D, H, W, C] (depth, height, width, channels-last)."""

    def __init__(self, channel_n=16, hidden=128, fire_rate=0.5):
        super().__init__()
        self.channel_n, self.hidden, self.fire_rate = channel_n, hidden, fire_rate
        self.w1 = nn.Parameter(torch.empty(hidden, channel_n * 4))
        self.b1 = nn.Parameter(torch.zeros(hidden))
        self.w2 = nn.Parameter(torch.zeros(channel_n, hidden))
        self.b2 = nn.Parameter(torch.zeros(channel_n))
        nn.init.xavier_uniform_(self.w1)
        self.register_buffer("_stencil", self.stencil(), persistent=False)

    @staticmethod
    def perceive(x):
        """[B,D,H,W,C] -> [B,D,H,W,4C], feature 4*c + k for k in (identity, gx, gy, gz).
        Zero padded — the volume is not toroidal."""
        p = F.pad(x, (0, 0, 1, 1, 1, 1, 1, 1))              # pad W, H, D
        sm = lambda t, ax: t.narrow(ax, 0, t.shape[ax] - 2) + 2 * t.narrow(ax, 1, t.shape[ax] - 2) + t.narrow(ax, 2, t.shape[ax] - 2)
        df = lambda t, ax: t.narrow(ax, 2, t.shape[ax] - 2) - t.narrow(ax, 0, t.shape[ax] - 2)
        D_, H_, W_ = 1, 2, 3                                # axes of the padded tensor
        sz = sm(p, D_)                                      # [B, D, H+2, W+2, C]
        gx = df(sm(sz, H_), W_) / 32.0
        gy = df(sm(sz, W_), H_) / 32.0
        gz = df(sm(sm(p, H_), W_), D_) / 32.0
        return torch.stack([x, gx, gy, gz], -1).flatten(-2)

    @staticmethod
    def alive(x):
        """max alpha over the 3x3x3 neighbourhood > 0.1, as three 1-D max passes (the same
        result as max_pool3d with -inf padding, several times faster on CPU)."""
        a = x[..., 3]
        for ax in (1, 2, 3):
            n = a.shape[ax]
            p = F.pad(a.movedim(ax, -1), (1, 1), value=-1e9)
            a = torch.maximum(torch.maximum(p[..., :n], p[..., 1:n + 1]), p[..., 2:]).movedim(-1, ax)
        return (a > 0.1).unsqueeze(-1)

    @staticmethod
    def stencil():
        """[27, 4]: the perception as weights over the 3x3x3 neighbourhood (dz, dy, dx major-to-minor)."""
        sm, df = torch.tensor([1.0, 2.0, 1.0]), torch.tensor([-1.0, 0.0, 1.0])
        e = torch.tensor([0.0, 1.0, 0.0])
        o = lambda z, y, x: torch.einsum("i,j,k->ijk", z, y, x).reshape(27)
        return torch.stack([o(e, e, e), o(sm, sm, df) / 32, o(sm, df, sm) / 32, o(df, sm, sm) / 32], 1)

    def forward(self, x, fire_rate=None, fire_mask=None):
        """One step, cropped to the living region. Every non-zero cell is within 1 of a cell
        with alpha > 0.1, perception reads 1 further, and a cell can only come alive next to
        one that is — so outside bbox(alpha > 0.1) + 2 the state is zero before AND after the
        step, and computing there is pure waste. The crop is exact (selftest)."""
        B, D, H, W, C = x.shape
        rate = self.fire_rate if fire_rate is None else fire_rate
        live = (x[..., 3] > 0.1).any(0)
        if not bool(live.any()):
            return x * 0
        lo = [max(0, int(torch.nonzero(live.any(dim=[j for j in range(3) if j != i]))[0]) - 2) for i in range(3)]
        hi = [min(n, int(torch.nonzero(live.any(dim=[j for j in range(3) if j != i]))[-1]) + 3) for i, n in enumerate((D, H, W))]
        xc = x[:, lo[0]:hi[0], lo[1]:hi[1], lo[2]:hi[2]]
        fc = (fire_mask[:, lo[0]:hi[0], lo[1]:hi[1], lo[2]:hi[2]] if fire_mask is not None
              else torch.rand(xc.shape[:-1] + (1,)) <= rate)
        yc = self._step(xc, fc)
        return F.pad(yc, (0, 0, lo[2], W - hi[2], lo[1], H - hi[1], lo[0], D - hi[0]))

    def _step(self, x, fire):
        """Perception is evaluated ONLY at cells that are alive and fire (a gather of their
        27 neighbours times the fixed stencil); every other cell's update is multiplied by
        zero anyway. Identical to dense_reference_step (selftest)."""
        B, D, H, W, C = x.shape
        pre = self.alive(x)
        idx = (pre & fire).reshape(-1).nonzero().squeeze(1)
        bb, rem = idx // (D * H * W), idx % (D * H * W)
        zz, rem = rem // (H * W), rem % (H * W)
        yy, xx = rem // W, rem % W
        Dp, Hp, Wp = D + 2, H + 2, W + 2
        base = ((bb * Dp + zz + 1) * Hp + yy + 1) * Wp + xx + 1
        off = torch.tensor([(dz * Hp + dy) * Wp + dx for dz in (-1, 0, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1)])
        flat = F.pad(x, (0, 0, 1, 1, 1, 1, 1, 1)).reshape(-1, C)
        nb = flat.index_select(0, (base[:, None] + off[None]).reshape(-1)).view(-1, 27, C)
        y = torch.einsum("nkc,kf->ncf", nb, self._stencil).reshape(-1, 4 * C)
        d = F.linear(torch.relu(F.linear(y, self.w1, self.b1)), self.w2, self.b2)
        dx = torch.zeros(B * D * H * W, C, dtype=x.dtype).index_copy(0, idx, d)
        x = x + dx.view(B, D, H, W, C)
        return x * (pre & self.alive(x)).to(x.dtype)

    def dense_reference_step(self, x, fire_mask):
        pre = self.alive(x)
        dx = F.linear(torch.relu(F.linear(self.perceive(x), self.w1, self.b1)), self.w2, self.b2)
        x = x + dx * fire_mask.to(x.dtype)
        return x * (pre & self.alive(x)).to(x.dtype)


def make_seed(n, d, h, w, c=16):
    x = torch.zeros(n, d, h, w, c)
    x[:, d // 2, h // 2, w // 2, 3:] = 1.0
    return x


def sphere_damage(n, d, h, w, rng):
    """1 - random ball. Centre in the inner half of the volume, radius 0.1-0.4 of the half-width
    (the 2D disc cut's rule, in voxel units so the ball stays round in an anisotropic grid)."""
    zz, yy, xx = np.mgrid[0:d, 0:h, 0:w].astype(np.float32)
    half = w / 2
    out = np.ones((n, d, h, w, 1), np.float32)
    for i in range(n):
        c = np.array([d, h, w]) / 2 + (rng.random(3) - 0.5) * np.array([d, h, w]) / 2
        r = (rng.random() * 0.3 + 0.1) * half
        m = (zz - c[0]) ** 2 + (yy - c[1]) ** 2 + (xx - c[2]) ** 2 < r * r
        out[i, m] = 0
    return torch.from_numpy(out)


# ---------------------------------------------------------------- target ---

def _edt(mask):
    """Exact-enough Euclidean distance to the background, via a two-pass 8-neighbour chamfer."""
    H, W = mask.shape
    d = np.where(mask, 1e9, 0.0)
    r2 = math.sqrt(2)
    for y in range(H):
        for x in range(W):
            if d[y, x]:
                if y: d[y, x] = min(d[y, x], d[y - 1, x] + 1, d[y - 1, x - 1] + r2 if x else 1e9, d[y - 1, x + 1] + r2 if x < W - 1 else 1e9)
                if x: d[y, x] = min(d[y, x], d[y, x - 1] + 1)
    for y in range(H - 1, -1, -1):
        for x in range(W - 1, -1, -1):
            if d[y, x]:
                if y < H - 1: d[y, x] = min(d[y, x], d[y + 1, x] + 1, d[y + 1, x - 1] + r2 if x else 1e9, d[y + 1, x + 1] + r2 if x < W - 1 else 1e9)
                if x < W - 1: d[y, x] = min(d[y, x], d[y, x + 1] + 1)
    return d


def _trilinear(vol, z, y, x):
    """Sample vol [D,H,W,C] at float coords (zero outside)."""
    D, H, W = vol.shape[:3]
    z0, y0, x0 = np.floor(z).astype(int), np.floor(y).astype(int), np.floor(x).astype(int)
    fz, fy, fx = (z - z0)[..., None], (y - y0)[..., None], (x - x0)[..., None]
    out = np.zeros(z.shape + (vol.shape[3],), np.float32)
    for dz, wz in ((0, 1 - fz), (1, fz)):
        for dy, wy in ((0, 1 - fy), (1, fy)):
            for dx, wx in ((0, 1 - fx), (1, fx)):
                zz, yy, xx = z0 + dz, y0 + dy, x0 + dx
                ok = (zz >= 0) & (zz < D) & (yy >= 0) & (yy < H) & (xx >= 0) & (xx < W)
                v = np.zeros_like(out)
                v[ok] = vol[zz[ok], yy[ok], xx[ok]]
                out += v * wz * wy * wx
    return out


def swim3d_frames(name="lizard", frames=8, size=32, depth=12, pad_xy=6, pad_z=5, ss=3,
                  thickness=0.85, amp=2.2, wavelength=0.9):
    """Premultiplied RGBA volumes [K, D, H, W, 4].

    Built at `ss` x supersampling and box-filtered down, so the body's edges are fractional
    alpha exactly as the 2D target's antialiased edges are. `thickness` is the peak
    half-thickness as a fraction of the body's own max in-plane half-width; `amp` is in
    target voxels (the 2D swim's ~1.9 px, scaled). Head still, tail widest (env = s^1.3)."""
    S = size * ss
    img = emoji_canvas(name).resize((S, S), Image.LANCZOS)
    a = np.asarray(img, np.float32) / 255.0
    mask = a[..., 3] > 0.5
    dist = _edt(mask)
    half = thickness * dist.max() * np.sqrt(np.clip(dist / dist.max(), 0, 1))  # pillow, in px
    Dz = depth * ss
    zc = (Dz - 1) / 2
    # Body axis (same construction as the 2D swim), in supersampled pixels.
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    w = a[..., 3]
    m = w.sum()
    cx, cy = (w * xx).sum() / m, (w * yy).sum() / m
    ev, evec = np.linalg.eigh(np.cov(np.stack([(xx - cx).ravel(), (yy - cy).ravel()]), aweights=w.ravel()))
    u = evec[:, 1]
    s = (xx - cx) * u[0] + (yy - cy) * u[1]
    lo, hi = s[mask].min(), s[mask].max()
    q = (hi - lo) * 0.25
    if w[s > hi - q].sum() > w[s < lo + q].sum():
        u, s, lo, hi = -u, -s, -hi, -lo
    n = np.array([-u[1], u[0]])
    # Rest volume at supersampling: colour from the emoji, occupancy |z - zc| < half(x, y).
    zg = np.arange(Dz, dtype=np.float32)[:, None, None]
    occ = np.clip(half[None] - np.abs(zg - zc) + 0.5, 0, 1) * mask[None]
    rgb = np.where(a[..., 3:] > 1e-3, a[..., :3], 0)
    rest = np.concatenate([np.broadcast_to(rgb[None], (Dz, S, S, 3)) * occ[..., None], occ[..., None]], -1).astype(np.float32)
    Z, Y, X = np.mgrid[0:Dz, 0:S, 0:S].astype(np.float32)
    sn = np.clip(((X - cx) * u[0] + (Y - cy) * u[1] - lo) / (hi - lo), 0, 1)
    env = sn ** 1.3
    A = amp * ss
    out = []
    for k in range(frames):
        ph = 2 * np.pi * (sn / wavelength - k / frames)
        dn, dz = A * env * np.sin(ph), A * env * np.cos(ph)      # helical: side and depth, 90 deg apart
        f = _trilinear(rest, Z - dz, Y - dn * n[1], X - dn * n[0])
        f = f.reshape(depth, ss, size, ss, size, ss, 4).mean((1, 3, 5))  # box filter down
        f = np.pad(f, ((pad_z, pad_z), (pad_xy, pad_xy), (pad_xy, pad_xy), (0, 0)))
        out.append(f)
    return np.stack(out).astype(np.float32)


# PRISM palette of the flight cell (Tools/Ecology/flight/src/60_stakes.js ELEMENT_COLOUR). The whale
# designer's three domains are drawn in it: Jade (back, fins, flukes) -> space blue, Ruby (mouth, eye)
# -> mass coral, Gold (belly, pleats) -> charge gold. Tier only shades: plain 1.0, danger 0.8,
# shield (octahedron) 1.0 - the octahedron's shape is what tells it apart.
PRISM_ELEMENT = {"mass": (0.98, 0.42, 0.30), "charge": (1.0, 0.84, 0.25),
                 "space": (0.42, 0.62, 1.0), "time": (0.62, 1.0, 0.55)}
PRISM_DOMAIN_ELEMENT = ("space", "mass", "charge")      # dom 0 Jade, 1 Ruby, 2 Gold
PRISM_TIER_SHADE = (1.0, 0.8, 1.0)
PRISM_TARGETS = {"whale": "results/whale_target/prisms.json"}   # target name -> prisms.json (relative to HERE)
PRISM_SHIELD = 3.0                                      # an octahedron reaches 3x its half-extents


def _solidify(occ, rgb, r):
    """Thicken a fine-grid shell by r cells, fill its inside, colour every voxel by its nearest shell voxel."""
    from scipy import ndimage
    shell = occ > 0.5
    rr = max(1, int(round(r)))
    zz, yy, xx = np.mgrid[-rr:rr + 1, -rr:rr + 1, -rr:rr + 1]
    ball = zz * zz + yy * yy + xx * xx <= r * r + 1e-6
    body = ndimage.binary_dilation(shell, ball)
    body = ndimage.binary_fill_holes(ndimage.binary_closing(body, ball, iterations=2) | body)
    _, ind = ndimage.distance_transform_edt(~shell, return_indices=True)
    col = rgb[ind[0], ind[1], ind[2]]
    return body.astype(np.float32), col * body[..., None]


def prism_frames(path, scale=0.6, pad=4, ss=3, solid=True, thicken=0.35):
    """Voxelise a prism creature (whale_target.py's prisms.json: per frame a list of
    {p, h, R, dom, tier}, whale frame +x head, +y up, +z right) into premultiplied RGBA volumes
    [K, D, H, W, 4] with D = whale y (UP: the flight creature maps D to world up), H = whale z,
    W = whale x. Boxes (tier 0/1) are |local_i| <= h_i, shield octahedra sum |local_i| / (3 h_i)
    <= 1, local = R^T (q - p) (R's columns are the prism's axes). `scale` is grid voxels per
    designer voxel; ss x ss x ss supersampling box-filtered down gives fractional edge alpha.
    The designer's whale is a SHELL of thin plates around a few core prisms; at a training scale
    of ~0.6 a plate is under a voxel thick, so with `solid` the shell is thickened by `thicken`
    grid voxels (fins and flukes stay >= ~1 voxel), its gaps closed and its inside filled, and
    every filled voxel takes the colour of the nearest prism (_solidify)."""
    d = json.load(open(path))
    frames = d["frames"]
    allp = [(np.array(q["p"]), (PRISM_SHIELD if q["tier"] == 2 else 1.0) * np.linalg.norm(q["h"]))
            for f in frames for q in f]
    lo = np.min([p - r for p, r in allp], 0)
    hi = np.max([p + r for p, r in allp], 0)
    n = np.ceil((hi - lo) * scale).astype(int) + 2 * pad        # x, y, z cells
    origin = (lo + hi) / 2 - n / (2 * scale)                    # whale units at the grid's corner
    Wn, Dn, Hn = int(n[0]), int(n[1]), int(n[2])
    f = 1.0 / (scale * ss)
    out = []
    for fr in frames:
        occ = np.zeros((Dn * ss, Hn * ss, Wn * ss), np.float32)
        rgb = np.zeros((Dn * ss, Hn * ss, Wn * ss, 3), np.float32)
        for q in fr:
            p, h = np.array(q["p"]), np.array(q["h"])
            M = np.array(q["R"]).reshape(3, 3)
            reach = PRISM_SHIELD * h.max() if q["tier"] == 2 else np.linalg.norm(h)
            # fine-grid index range of the bounding sphere, per whale axis
            a = np.maximum(np.floor((p - reach - origin) / f).astype(int), 0)
            b = np.minimum(np.ceil((p + reach - origin) / f).astype(int) + 1, [Wn * ss, Dn * ss, Hn * ss])
            xs = origin[0] + (np.arange(a[0], b[0]) + 0.5) * f
            ys = origin[1] + (np.arange(a[1], b[1]) + 0.5) * f
            zs = origin[2] + (np.arange(a[2], b[2]) + 0.5) * f
            Y, Z, X = np.meshgrid(ys, zs, xs, indexing="ij")          # D(y), H(z), W(x) order
            dlt = np.stack([X - p[0], Y - p[1], Z - p[2]], -1)
            loc = dlt @ M                                              # local_i = sum_j d_j R[j, i]
            inside = (np.abs(loc) / (PRISM_SHIELD * h)).sum(-1) <= 1 if q["tier"] == 2 else (np.abs(loc) <= h).all(-1)
            col = np.array(PRISM_ELEMENT[PRISM_DOMAIN_ELEMENT[q["dom"]]]) * PRISM_TIER_SHADE[q["tier"]]
            sl = (slice(a[1], b[1]), slice(a[2], b[2]), slice(a[0], b[0]))
            occ[sl] = np.maximum(occ[sl], inside)
            rgb[sl][inside] = col
        if solid:
            occ, rgb = _solidify(occ, rgb, thicken * ss)
        vol = np.concatenate([rgb * occ[..., None], occ[..., None]], -1)
        out.append(vol.reshape(Dn, ss, Hn, ss, Wn, ss, 4).mean((1, 3, 5)))
    return np.stack(out).astype(np.float32)


# ---------------------------------------------------------------- render ---

def _blur3(a):
    """Separable [1,2,1]/4 blur along all three axes (for smooth shading normals)."""
    for ax in range(3):
        p = np.pad(a, [(1, 1) if i == ax else (0, 0) for i in range(3)])
        sl = lambda o: [slice(o, o + a.shape[ax]) if i == ax else slice(None) for i in range(3)]
        a = (p[tuple(sl(0))] + 2 * p[tuple(sl(1))] + p[tuple(sl(2))]) / 4
    return a


def render(vol, az=0.9, tilt=0.95, px=160, zoom=0.40, bg=(1.0, 1.0, 1.0), light=(-0.35, -0.55, 0.75)):
    """Front-to-back compositing of a premultiplied RGBA volume [D,H,W,4] (numpy or torch):
    orthographic camera looking DOWN the depth axis, tilted by `tilt` toward azimuth `az`
    (so a turntable is az sweeping 0..2pi), trilinear samples, Lambert shading from the
    gradient of a lightly blurred alpha. Returns [px, px, 3] in [0, 1]."""
    v = vol.detach().numpy() if torch.is_tensor(vol) else vol
    v = np.clip(np.asarray(v[..., :4], np.float32), 0, 1)
    D, H, W = v.shape[:3]
    R = zoom * math.sqrt(D * D + H * H + W * W)
    # volume coords are (x=W, y=H, z=D); the lizard lies in the x-y plane
    fwd = np.array([math.sin(tilt) * math.cos(az), math.sin(tilt) * math.sin(az), -math.cos(tilt)])
    hint = np.array([-math.cos(az), -math.sin(az), 0.0]) if tilt > 1e-3 else np.array([0.0, -1.0, 0.0])
    right = np.cross(fwd, hint); right /= np.linalg.norm(right)
    up = np.cross(right, fwd)
    g = np.linspace(-R, R, px)
    gu, gv = np.meshgrid(g, -g)
    centre = np.array([W / 2, H / 2, D / 2])
    origin = centre + gu[..., None] * right + gv[..., None] * up - fwd * R * 1.6
    ab = _blur3(v[..., 3])
    gz, gy, gx = np.gradient(ab)
    grad = np.stack([gz, gy, gx], -1)
    L = np.array(light) / np.linalg.norm(light)
    col = np.zeros((px, px, 3), np.float32)
    T = np.ones((px, px), np.float32)
    dt = 0.6
    for i in range(int(R * 3.2 / dt)):
        pnt = origin + fwd * (i * dt) - 0.5
        z, y, x = pnt[..., 2], pnt[..., 1], pnt[..., 0]
        if (z.max() < -1) or (z.min() > D) or (T.max() < 0.01):
            if T.max() < 0.01:
                break
            continue
        s = _trilinear(v, z, y, x)
        a = s[..., 3]
        if a.max() < 1e-3:
            continue
        gr = _trilinear(grad, z, y, x)
        nrm = -np.stack([gr[..., 2], gr[..., 1], gr[..., 0]], -1)
        nn_ = np.linalg.norm(nrm, axis=-1, keepdims=True)
        lam = np.clip((nrm / np.maximum(nn_, 1e-5)) @ L, 0, 1)[..., None]
        shade = np.where(nn_ > 1e-3, 0.5 + 0.6 * lam, 1.0)
        al = 1 - (1 - np.clip(a, 0, 1)) ** (dt * 1.6)
        rgb = np.where(s[..., 3:] > 1e-4, s[..., :3] / np.maximum(s[..., 3:], 1e-4), 0) * shade
        col += (T * al)[..., None] * rgb
        T *= 1 - al
    return np.clip(col + T[..., None] * np.array(bg), 0, 1)


# --------------------------------------------------------------- training ---

@dataclass
class Config3D:
    target: str = "lizard"
    frames: int = 8
    size: int = 32
    depth: int = 12
    pad_xy: int = 6
    pad_z: int = 5
    thickness: float = 0.85
    amp: float = 2.2
    wavelength: float = 0.9
    period: int = 8
    window: int = 5
    min_seed_check: int = 64
    clock_steps: int = 1500
    clock_min_iter: int = 96
    clock_max_iter: int = 128
    init2d: str = ""            # optional warm start from a 2D model.pt: identity/gx/gy weights copied, gz = 0
    init3d: str = ""            # optional warm start from a 3D model.pt with the same channel/hidden layout
    prisms: str = ""            # prism target (whale_target.py prisms.json); default for target "whale"
    voxel_scale: float = 0.6    # prism target: grid voxels per designer voxel
    blowup_factor: float = 20.0 # guard: loss > factor x median(last 50) rolls back to the last good snapshot
    channel_n: int = 16
    hidden: int = 128
    batch_size: int = 8
    pool_size: int = 512
    fire_rate: float = 0.5
    lr: float = 2e-3
    lr_drop_step: int = 2000
    steps: int = 6000
    min_iter: int = 64
    max_iter: int = 96
    damage_n: int = 3
    seed: int = 0
    threads: int = 0


def build_frames(cfg):
    if cfg.prisms or cfg.target in PRISM_TARGETS:
        path = cfg.prisms or os.path.join(HERE, PRISM_TARGETS[cfg.target])
        return prism_frames(path, cfg.voxel_scale, cfg.pad_xy)
    return swim3d_frames(cfg.target, cfg.frames, cfg.size, cfg.depth, cfg.pad_xy, cfg.pad_z,
                         thickness=cfg.thickness, amp=cfg.amp, wavelength=cfg.wavelength)


def frame_mse(x, frames):
    """[B,D,H,W,C] x [K,D,H,W,4] -> [B,K]."""
    B, K = x.shape[0], frames.shape[0]
    xs = x[..., :4].reshape(B, 1, -1)
    fs = frames.reshape(1, K, -1)
    return ((xs - fs) ** 2).mean(-1)


def rollout_checkpointed(ca, x, n, checks):
    """n steps with per-step activation checkpointing (memory ~ one state per step, not
    one full perception volume per step). The fire mask's RNG state is replayed exactly."""
    snaps = []
    for i in range(1, n + 1):
        x = checkpoint(ca, x, use_reentrant=False)
        if i in checks:
            snaps.append(x)
    return x, snaps


def train(cfg: Config3D, out_dir: str, resume: bool = False):
    """`resume` continues a run from its last checkpoint in out_dir (the container this runs
    in can be restarted). The checkpoint is the model, the optimiser moments and the step; the
    POOL is not saved (512 volumes, ~2.8 GB) and is rebuilt by growing seeds with the
    checkpoint model for random lengths, so the lizards come back at scattered phases."""
    os.makedirs(out_dir, exist_ok=True)
    start = 0
    if resume:
        cfg = Config3D(**{**asdict(cfg), **json.load(open(os.path.join(out_dir, "config.json"))),
                          "threads": cfg.threads})
        st = os.path.join(out_dir, "state.json")
        start = json.load(open(st))["step"] if os.path.isfile(st) else \
            len(np.load(os.path.join(out_dir, "loss.npy"))) - 1   # loss.npy is saved with model.pt
    if cfg.threads:
        torch.set_num_threads(cfg.threads)
    torch.manual_seed(cfg.seed + start)
    rng = np.random.default_rng(cfg.seed + start)
    if resume:
        fr_np = np.load(os.path.join(out_dir, "frames.npy")).astype(np.float32)
    else:
        fr_np = build_frames(cfg)
        np.save(os.path.join(out_dir, "frames.npy"), fr_np.astype(np.float16))
        with open(os.path.join(out_dir, "config.json"), "w") as f:
            json.dump(asdict(cfg), f, indent=2)
    K, D, H, W = fr_np.shape[:4]
    frames = torch.from_numpy(fr_np)

    ca = CA3D(cfg.channel_n, cfg.hidden, cfg.fire_rate)
    if resume:
        ca.load_state_dict(torch.load(os.path.join(out_dir, "model.pt")))
    elif cfg.init2d:
        sd = torch.load(cfg.init2d)
        with torch.no_grad():
            w1 = torch.zeros(cfg.hidden, cfg.channel_n, 4)
            w1[:, :, :3] = sd["w1"].view(cfg.hidden, cfg.channel_n, 3)   # 2D order 3c+k -> 3D order 4c+k
            ca.w1.copy_(w1.view(cfg.hidden, -1)); ca.b1.copy_(sd["b1"])
            ca.w2.copy_(sd["w2"]); ca.b2.copy_(sd["b2"])
    elif cfg.init3d:
        ca.load_state_dict(torch.load(cfg.init3d))       # same 16-ch / 128-hidden layout (e.g. the lizard)
    opt = torch.optim.Adam(ca.parameters(), lr=cfg.lr, eps=1e-7)
    sched = torch.optim.lr_scheduler.MultiStepLR(opt, [cfg.lr_drop_step], 0.1)
    seed = make_seed(1, D, H, W, cfg.channel_n)
    pool = seed.repeat(cfg.pool_size, 1, 1, 1, 1)
    J, P = cfg.window, cfg.period
    shift = torch.arange(J)
    log, t0 = [], time.time()
    rollbacks, good = 0, None
    if resume and os.path.isfile(os.path.join(out_dir, "state.json")):
        rollbacks = json.load(open(os.path.join(out_dir, "state.json"))).get("rollbacks", 0)
    if resume:
        if os.path.isfile(os.path.join(out_dir, "opt.pt")):
            opt.load_state_dict(torch.load(os.path.join(out_dir, "opt.pt")))
        for _ in range(start):
            sched.step()
        log = list(np.load(os.path.join(out_dir, "loss.npy")))[:start]
        if start >= cfg.clock_steps:
            with torch.no_grad():
                for b in range(0, cfg.pool_size, 32):
                    x = seed.repeat(min(32, cfg.pool_size - b), 1, 1, 1, 1)
                    for _ in range(int(rng.integers(150, 400))):
                        x = ca(x)
                    pool[b:b + len(x)] = x
        print(f"[3d {cfg.target}] resumed at step {start} (lr {opt.param_groups[0]['lr']:.1e}, "
              f"pool rebuilt in {(time.time() - t0) / 60:.1f}m)", flush=True)
        t0 = time.time()

    for step in range(start + 1 if resume else 0, cfg.steps + 1):
        clock = step < cfg.clock_steps
        if clock:
            idx = None
            x0 = seed.repeat(cfg.batch_size, 1, 1, 1, 1)
            n = int(rng.integers(cfg.clock_min_iter, cfg.clock_max_iter + 1))
        else:
            idx = rng.choice(cfg.pool_size, cfg.batch_size, replace=False)
            x0 = pool[idx].clone()
            with torch.no_grad():
                order = torch.argsort(frame_mse(x0, frames).min(1).values, descending=True)
            x0, idx = x0[order], idx[order.numpy()]
            x0[:1] = seed
            if cfg.damage_n:
                x0[-cfg.damage_n:] *= sphere_damage(cfg.damage_n, D, H, W, rng)
            n = int(rng.integers(cfg.min_iter, cfg.max_iter + 1))

        checks = [n - (J - 1 - j) * P for j in range(J)]
        x, snaps = rollout_checkpointed(ca, x0, n, set(checks))
        B = x0.shape[0]
        err = torch.stack([frame_mse(s, frames) for s in snaps], 1)       # [B, J, K]
        born = torch.tensor([(c // P) % K for c in checks])
        seed_valid = torch.tensor([float(c >= cfg.min_seed_check) for c in checks])
        seed_cost = (err[:, shift, born] * seed_valid).sum(-1) / seed_valid.sum().clamp(min=1)
        if clock:
            loss = seed_cost.mean()
        else:
            kk = (torch.arange(K)[:, None] + shift[None]) % K
            cost = err[:, shift[None, :].expand(K, J), kk].mean(-1)
            free = cost[torch.arange(B), cost.detach().argmin(1)]
            loss = torch.cat([seed_cost[:1], free[1:]]).mean()

        L = float(loss)
        # guard (as particle_nca.py): a non-finite or exploding loss, or a batch that died out,
        # rolls the model and optimiser back to the last healthy snapshot instead of stepping
        recent = [v for v in log[-50:] if math.isfinite(v)]
        med = float(np.median(recent)) if len(recent) >= 10 else float("inf")
        extinct = not bool((x.detach()[..., 3] > 0.1).any())
        if not math.isfinite(L) or L > cfg.blowup_factor * med or extinct:
            rollbacks += 1
            if good is not None:
                ca.load_state_dict(good[0]); opt.load_state_dict(good[1])
            print(f"[3d {cfg.target}] step {step}: loss {L:.4g} (median {med:.4g}), extinct={extinct} -> "
                  f"rolled back to step {good[2] if good else '-'} (rollback {rollbacks})", flush=True)
            log.append(med if math.isfinite(med) else (log[-1] if log else 1.0))
            sched.step()
            continue

        opt.zero_grad(set_to_none=True)
        loss.backward()
        for prm in ca.parameters():
            if prm.grad is not None:
                prm.grad /= prm.grad.norm() + 1e-8
        opt.step()
        sched.step()
        xd = x.detach()
        with torch.no_grad():   # one bad sample (non-finite, exploded or extinct) must not poison the pool
            bad = ~torch.isfinite(xd).flatten(1).all(1) | (xd.abs().flatten(1).amax(1) > 50) | \
                  ~(xd[..., 3] > 0.1).flatten(1).any(1)
            if bool(bad.any()):
                xd = xd.clone(); xd[bad] = seed[0]
        if clock:
            if step == cfg.clock_steps - 1:
                pool[:] = xd[rng.integers(0, B, cfg.pool_size)]
        else:
            pool[idx] = xd

        log.append(L)
        if step % 25 == 0:
            good = ({k: v.clone() for k, v in ca.state_dict().items()},
                    __import__("copy").deepcopy(opt.state_dict()), step)
            dt = time.time() - t0
            spi = dt / (step - start + 1)
            json.dump({"step": step, "steps": cfg.steps, "phase": "clock" if clock else "pool", "loss": L,
                       "median50": med if math.isfinite(med) else None, "s_per_it": round(spi, 2),
                       "eta_h": round((cfg.steps - step) * spi / 3600, 2), "rollbacks": rollbacks,
                       "pid": os.getpid(), "updated": time.strftime("%Y-%m-%d %H:%M:%S")},
                      open(os.path.join(out_dir, "status.json"), "w"), indent=1)
        if step % 25 == 0:
            dt = time.time() - t0
            print(f"[3d {cfg.target}] {'clock' if clock else 'pool '} step {step:5d}  loss {L:.5f}  "
                  f"log10 {math.log10(L):+.3f}  {dt/(step - start + 1):.2f}s/it  elapsed {dt/60:.1f}m", flush=True)
        if step % 50 == 0 or step == cfg.steps:
            torch.save(ca.state_dict(), os.path.join(out_dir, "model.pt"))
            torch.save(opt.state_dict(), os.path.join(out_dir, "opt.pt"))
            np.save(os.path.join(out_dir, "loss.npy"), np.array(log))
            json.dump({"step": step, "rollbacks": rollbacks}, open(os.path.join(out_dir, "state.json"), "w"))
        if step % 250 == 0 or step == cfg.steps:
            ims = [render(x.detach()[i], px=96) for i in range(min(B, 8))]
            Image.fromarray((np.concatenate(ims, 1) * 255).astype(np.uint8)).save(
                os.path.join(out_dir, f"batch_{step:05d}.png"))

    export(ca, cfg, fr_np, os.path.join(out_dir, "weights.json"))
    return ca


def export(ca, cfg, fr_np, path):
    K, D, H, W = fr_np.shape[:4]
    data = {"channel_n": cfg.channel_n, "hidden": cfg.hidden, "fire_rate": cfg.fire_rate,
            "w1": ca.w1.detach().numpy().round(6).tolist(), "b1": ca.b1.detach().numpy().round(6).tolist(),
            "w2": ca.w2.detach().numpy().round(6).tolist(), "b2": ca.b2.detach().numpy().round(6).tolist(),
            "D": D, "H": H, "W": W, "frames": K, "period": cfg.period,
            "perception_order": "per-channel [identity, sobel_x, sobel_y, sobel_z] -> index 4*c+k",
            "target": f"{cfg.target} {'prism swim' if cfg.prisms or cfg.target in PRISM_TARGETS else 'helical swim'} (3D)",
            "experiment": "animated3d",
            "axes": "D=up (world y), H=world z, W=world x" if cfg.prisms or cfg.target in PRISM_TARGETS else "D=depth"}
    with open(path, "w") as f:
        json.dump(data, f)


# ---------------------------------------------------------------- figures ---

def load_run(run_dir):
    cfg = Config3D(**json.load(open(os.path.join(run_dir, "config.json"))))
    ca = CA3D(cfg.channel_n, cfg.hidden, cfg.fire_rate)
    ca.load_state_dict(torch.load(os.path.join(run_dir, "model.pt")))
    return cfg, ca, torch.from_numpy(np.load(os.path.join(run_dir, "frames.npy")).astype(np.float32))


@torch.no_grad()
def figures(run_dir, seed=1, horizon=2000):
    torch.manual_seed(seed)
    cfg, ca, frames = load_run(run_dir)
    K, D, H, W = frames.shape[:4]
    P = cfg.period
    fig = os.path.join(run_dir, "figures")
    os.makedirs(fig, exist_ok=True)

    x = make_seed(1, D, H, W, cfg.channel_n)
    table, keep = [], {}
    for t in range(horizon + 1):
        table.append(frame_mse(x, frames)[0].numpy())
        if t in (0, 16, 32, 48, 64, 96) or 200 <= t < 200 + K * P:
            keep[t] = x[0].clone()
        x = ca(x)
    table = np.stack(table)
    best, berr = table.argmin(1), table.min(1)
    tt = np.arange(horizon + 1)
    ph = np.unwrap(best * 2 * np.pi / K) * K / (2 * np.pi)
    fit = np.polyfit(tt[200:], ph[200:], 1)
    pred = np.round(np.polyval(fit, tt)).astype(int) % K
    mean_frame = frames.mean(0, keepdim=True)
    static_err = float(((mean_frame - frames) ** 2).mean())

    # growth strip + one loop (automaton over target), rendered at one view
    grow = [render(keep[t]) for t in (0, 16, 32, 48, 64, 96)]
    Image.fromarray((np.concatenate(grow, 1) * 255).astype(np.uint8)).save(os.path.join(fig, "growth_strip.png"))
    ts = list(range(200, 200 + K * P, P))
    top = np.concatenate([render(keep[t]) for t in ts], 1)
    bot = np.concatenate([render(frames[best[t]]) for t in ts], 1)
    Image.fromarray((np.concatenate([top, np.ones((4, top.shape[1], 3)), bot], 0) * 255).astype(np.uint8)).save(
        os.path.join(fig, "loop_strip.png"))

    # turntable GIF: the swimming automaton while the camera orbits, beside its matching target
    x = make_seed(1, D, H, W, cfg.channel_n)
    for _ in range(200):
        x = ca(x)
    gif = []
    for i in range(3 * K * P):
        if i % 2 == 0:
            e = frame_mse(x, frames)[0]
            yaw = 0.9 + 2 * math.pi * i / (3 * K * P)
            gif.append(np.concatenate([render(x[0], az=yaw, px=144), np.ones((144, 3, 3)),
                                       render(frames[int(e.argmin())], az=yaw, px=144)], 1))
        x = ca(x)
    write_gif(gif, os.path.join(fig, "turntable.gif"), scale=1, ms=50)

    # damage while swimming: a ball through the tail half at step 400
    x = make_seed(1, D, H, W, cfg.channel_n)
    for _ in range(400):
        x = ca(x)
    zz, yy, xx = np.mgrid[0:D, 0:H, 0:W]
    ball = torch.from_numpy(((zz - D / 2) ** 2 + (yy - H * 0.68) ** 2 + (xx - W * 0.68) ** 2 < (0.3 * W) ** 2)[None, ..., None])
    x = x * (~ball).float()
    seq, rec, rbest = [render(x[0])], [], []
    for i in range(600):
        x = ca(x)
        e = frame_mse(x, frames)[0]
        rec.append(float(e.min()))
        rbest.append(int(e.argmin()))
        if i + 1 in (25, 50, 100, 200, 600):
            seq.append(render(x[0]))
    Image.fromarray((np.concatenate(seq, 1) * 255).astype(np.uint8)).save(os.path.join(fig, "damage.png"))
    rph = np.unwrap(np.array(rbest[200:]) * 2 * np.pi / K) * K / (2 * np.pi)
    rslope = float(np.polyfit(np.arange(len(rph)), rph, 1)[0])

    summary = {
        "grid_dhw": [D, H, W], "frames": K, "period_target_steps_per_frame": P,
        "measured_steps_per_frame": float(1 / fit[0]) if abs(fit[0]) > 1e-6 else None,
        "distinct_frames_visited_after_200": int(len(set(best[200:].tolist()))),
        "best_frame_error_mean_after_200": float(berr[200:].mean()),
        "clock_predicted_frame_error_mean": float(table[np.arange(200, horizon + 1), pred[200:]].mean()),
        "best_worst_frame_margin_log10": float(np.mean(np.log10(table[200:].max(1) / table[200:].min(1)))),
        "static_best_image_error": static_err,
        "best_frame_error_at": {str(t): float(berr[t]) for t in (64, 96, 200, 1000, horizon)},
        "after_damage_error_at_600": rec[-1],
        "after_damage_steps_per_frame": float(1 / rslope) if abs(rslope) > 1e-6 else None,
    }
    np.save(os.path.join(fig, "phase.npy"), np.stack([best, berr]))
    with open(os.path.join(fig, "summary.json"), "w") as f:
        json.dump(summary, f, indent=2)
    print(json.dumps(summary, indent=2))


# -------------------------------------------------------------- selftest ---

def selftest():
    torch.manual_seed(0)
    ca = CA3D()
    nn.init.normal_(ca.w2, std=0.05)
    x = torch.rand(2, 8, 10, 12, 16)
    x[..., 3] = (x[..., 3] > 0.7).float() * x[..., 3]
    fire = torch.rand(2, 8, 10, 12, 1) <= 0.5
    a, b = ca(x, fire_mask=fire), ca.dense_reference_step(x, fire)
    d = float((a - b).abs().max())
    ga = torch.autograd.grad(a.square().sum(), ca.w1)[0]
    gb = torch.autograd.grad(ca.dense_reference_step(x, fire).square().sum(), ca.w1)[0]
    dg = float((ga - gb).abs().max() / gb.abs().max())
    assert d < 1e-5 and dg < 1e-4, (d, dg)
    # Sobel orientation + normalisation: a unit ramp along each axis reads 1 on that axis only.
    for ax, k in ((3, 1), (2, 2), (1, 3)):  # W->gx, H->gy, D->gz
        shape = [1, 6, 6, 6, 16]
        r = torch.arange(6.0).view([6 if i == ax else 1 for i in range(5)]).expand(shape)
        pr = CA3D.perceive(r)[0, 3, 3, 3].view(16, 4)[0]
        want = torch.zeros(4); want[0] = 3.0; want[k] = 1.0
        assert torch.allclose(pr, want, atol=1e-6), (ax, pr)
    # Negative control: skipping dead-cell zeroing must differ from the reference.
    leak = x + F.linear(torch.relu(F.linear(ca.perceive(x), ca.w1, ca.b1)), ca.w2, ca.b2) * fire
    assert float((leak - b).abs().max()) > 1e-3
    # alive() == max_pool3d with -inf padding.
    ref_alive = (F.max_pool3d(x[..., 3].unsqueeze(1), 3, 1, 1) > 0.1).squeeze(1).unsqueeze(-1)
    assert torch.equal(CA3D.alive(x), ref_alive)
    # Crop exactness: a small live blob in a big empty volume, many steps, vs uncropped dense.
    big = torch.zeros(2, 14, 18, 20, 16)
    big[:, 6:9, 7:10, 9:12] = torch.rand(2, 3, 3, 3, 16)
    fm = [torch.rand(2, 14, 18, 20, 1) <= 0.5 for _ in range(6)]
    p, q = big.clone(), big.clone()
    for f in fm:
        p, q = ca(p, fire_mask=f), ca.dense_reference_step(q, f)
    dc = float((p - q).abs().max())
    assert dc < 1e-5, f"crop not exact: {dc}"
    # Checkpointed rollout == plain rollout (same RNG), value and gradient.
    torch.manual_seed(5); y1 = x.clone()
    for _ in range(4): y1 = ca(y1)
    torch.manual_seed(5); y2, _ = rollout_checkpointed(ca, x.clone(), 4, set())
    assert float((y1 - y2).abs().max()) < 1e-6
    print(f"selftest OK  sparse==dense {d:.1e}  grad {dg:.1e}  crop==dense over 6 steps {dc:.1e}  "
          "sobel axes OK  alive==max_pool3d  checkpoint replay OK")


# -------------------------------------------------------------------- cli ---

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("selftest")
    tg = sub.add_parser("target")
    tg.add_argument("--out", required=True)
    tg.add_argument("--target", default="lizard")
    b = sub.add_parser("bench")
    b.add_argument("--threads", type=int, default=0)
    b.add_argument("--target", default="lizard")
    b.add_argument("--voxel-scale", type=float, default=0.6)
    b.add_argument("--pad-xy", type=int, default=6)
    b.add_argument("--batch-size", type=int, default=8)
    b.add_argument("--iters", type=int, default=80)
    t = sub.add_parser("train")
    t.add_argument("--out", default=None)
    t.add_argument("--resume", action="store_true", help="continue the run in --out from its last checkpoint")
    for k, v in asdict(Config3D()).items():
        t.add_argument(f"--{k.replace('_', '-')}", type=type(v), default=v)
    fg = sub.add_parser("figures")
    fg.add_argument("--run", required=True)
    args = ap.parse_args()

    if args.cmd == "selftest":
        selftest()
    elif args.cmd == "target":
        cfg = Config3D(target=args.target, **({"pad_xy": 3, "voxel_scale": 0.5} if args.target in PRISM_TARGETS else {}))
        fr = build_frames(cfg)
        K = len(fr)
        diffs = [float(((fr[i] - fr[(i + 1) % K]) ** 2).mean()) for i in range(K)]
        m = fr.mean(0, keepdims=True)
        print(f"grid {fr.shape[1:4]} (D,H,W), {K} frames, occupied voxels {(fr[0, ..., 3] > 0.5).sum()}, "
              f"consecutive MSE {np.mean(diffs):.2e}, still-image floor {((fr - m) ** 2).mean():.2e}")
        if args.target in PRISM_TARGETS:      # D is UP here: flip it so the render's camera sees the whale upright
            views = [np.concatenate([render(f[::-1], az=1.2, tilt=0.9), render(f[::-1], az=1.5708, tilt=1.5)], 0)
                     for f in fr]
            Image.fromarray((np.concatenate(views, 1) * 255).astype(np.uint8)).save(
                os.path.splitext(args.out)[0] + ".png")                          # all K frames side by side
        else:
            views = [np.concatenate([render(f, az=0.9), render(f, az=0.9, tilt=1.45)], 1) for f in fr]
        write_gif(views, args.out, scale=1, ms=cfg.period * 33)
    elif args.cmd == "bench":
        if args.threads:
            torch.set_num_threads(args.threads)
        cfg = Config3D(target=args.target, voxel_scale=args.voxel_scale, pad_xy=args.pad_xy)
        fr = torch.from_numpy(build_frames(cfg))
        ca = CA3D()
        nn.init.normal_(ca.w2, std=0.01)
        x0 = torch.zeros((args.batch_size,) + tuple(fr.shape[1:4]) + (16,))
        x0[..., :4] = fr[0]
        x0[..., 4:] = x0[..., 3:4] * 0.5
        print(f"grid {tuple(fr.shape[1:4])}, alive fraction {float(CA3D.alive(x0).float().mean()):.3f}")
        opt = torch.optim.Adam(ca.parameters(), 2e-3)
        for rep in range(2):
            t0 = time.time()
            x, snaps = rollout_checkpointed(ca, x0, args.iters, {args.iters})
            loss = x[..., :4].pow(2).mean()
            opt.zero_grad(); loss.backward(); opt.step()
            print(f"threads={torch.get_num_threads()} {args.iters} steps fwd+bwd (checkpointed), batch {args.batch_size}: {time.time()-t0:.2f}s")
    elif args.cmd == "train":
        cfg = Config3D(**{k: getattr(args, k) for k in asdict(Config3D())})
        out = args.out or os.path.join(HERE, "runs", f"{cfg.target}3d_swim")
        train(cfg, out, resume=args.resume)
        figures(out)
    elif args.cmd == "figures":
        figures(args.run)


if __name__ == "__main__":
    main()
