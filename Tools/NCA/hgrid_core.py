"""HGRID core: a coarse 3D grid that rides on the swarm (the "morphogen" layer of the hierarchy).

The direction (cece/swarm-x-hgrid): a cellular automaton controlling a collision automaton. Space
around a swarm is partitioned into a G^3 grid that follows the swarm's centroid. Each step the
tadpoles SPLAT into it (trilinear), the grid updates, and every tadpole SAMPLES its cell; the
sampled channels drive its behaviour. This file holds only the shared geometry:

  * GridFrame  - where the grid sits (centre per sample, cell size, resolution)
  * splat()    - particles -> grid (trilinear, mass-conserving)
  * sample()   - grid -> particles (trilinear)
  * grad()     - central-difference spatial gradient of a field (per voxel unit)
  * blur()     - a separable [1,2,1]/4 smoothing (one cell)
  * PlanFields - every body plan rasterised into "what should be in each cell": per (element, slot)
                 unit density, plus per-element attribute fields (raw prism, Charge tier, facing,
                 spindle) stored as density-weighted sums so they can be divided back out.

Nothing here is learned. Nothing here edits swarm_nca.py (other sessions depend on it).
"""
from __future__ import annotations

import math

import numpy as np
import torch
import torch.nn.functional as F

import swarm_nca as sn

G_DEFAULT = 24
CELL_DEFAULT = 4.0


class GridFrame:
    """A cube of G^3 cells of size `cell` centred on `centre` [B,3] (world voxels)."""

    def __init__(self, centre, G=G_DEFAULT, cell=CELL_DEFAULT):
        self.centre, self.G, self.cell = centre, G, cell

    def to_grid(self, pos):
        """world [B,N,3] -> continuous cell coordinates in [0, G-1] (cell centres at integers)."""
        return (pos - self.centre[:, None, :]) / self.cell + (self.G - 1) / 2.0

    def to_world(self, idx):
        return (idx - (self.G - 1) / 2.0) * self.cell + self.centre[:, None, :]


def splat(frame: GridFrame, pos, vals, mask=None):
    """Trilinear splat. pos [B,N,3] world, vals [B,N,K] -> [B,K,G,G,G] (grid index order z,y,x is
    (x,y,z) here: dim 2 = x, dim 3 = y, dim 4 = z). Outside points are dropped (their weight leaks)."""
    B, N, _ = pos.shape
    K = vals.shape[-1]
    G = frame.G
    u = frame.to_grid(pos)
    if mask is not None:
        vals = vals * mask[..., None].to(vals.dtype)
    i0 = torch.floor(u).long()
    fr = u - i0.to(u.dtype)
    out = torch.zeros(B, K, G * G * G, dtype=vals.dtype)
    bidx = torch.arange(B)[:, None].expand(B, N)
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                ix, iy, iz = i0[..., 0] + dx, i0[..., 1] + dy, i0[..., 2] + dz
                w = ((fr[..., 0] if dx else 1 - fr[..., 0]) * (fr[..., 1] if dy else 1 - fr[..., 1])
                     * (fr[..., 2] if dz else 1 - fr[..., 2]))
                ok = (ix >= 0) & (ix < G) & (iy >= 0) & (iy < G) & (iz >= 0) & (iz < G)
                flat = (ix.clamp(0, G - 1) * G + iy.clamp(0, G - 1)) * G + iz.clamp(0, G - 1)
                wv = (w * ok.to(w.dtype))[..., None] * vals                   # [B,N,K]
                lin = (bidx * G ** 3 + flat).reshape(-1)
                tmp = torch.zeros(B * G ** 3, K, dtype=vals.dtype).index_add(0, lin, wv.reshape(-1, K))
                out = out + tmp.view(B, G ** 3, K).transpose(1, 2)
    return out.view(B, K, G, G, G)


def sample(frame: GridFrame, grid, pos):
    """Trilinear sample of grid [B,K,G,G,G] at world pos [B,N,3] -> [B,N,K] (zero outside)."""
    G = frame.G
    u = frame.to_grid(pos)
    # grid_sample wants (x->W=dim4, y->H=dim3, z->D=dim2) normalised to [-1,1]; our dims are (x,y,z)
    nrm = u / (G - 1) * 2 - 1
    g = torch.stack([nrm[..., 2], nrm[..., 1], nrm[..., 0]], -1)[:, :, None, None, :]   # [B,N,1,1,3]
    s = F.grid_sample(grid, g, mode="bilinear", padding_mode="zeros", align_corners=True)  # [B,K,N,1,1]
    return s[..., 0, 0].transpose(1, 2)


def grad(frame: GridFrame, field):
    """Central differences of [B,K,G,G,G] -> [B,K,3,G,G,G], per world voxel."""
    p = F.pad(field, (1, 1, 1, 1, 1, 1), mode="replicate")
    gx = (p[:, :, 2:, 1:-1, 1:-1] - p[:, :, :-2, 1:-1, 1:-1]) / (2 * frame.cell)
    gy = (p[:, :, 1:-1, 2:, 1:-1] - p[:, :, 1:-1, :-2, 1:-1]) / (2 * frame.cell)
    gz = (p[:, :, 1:-1, 1:-1, 2:] - p[:, :, 1:-1, 1:-1, :-2]) / (2 * frame.cell)
    return torch.stack([gx, gy, gz], 2)


_K1 = torch.tensor([0.25, 0.5, 0.25])


def blur(field, times=1):
    B, K = field.shape[:2]
    x = field
    for _ in range(times):
        for d in range(3):
            shape = [1, 1, 1, 1, 1]; shape[2 + d] = 3
            k = _K1.view(shape).expand(K, 1, *shape[2:]).contiguous()
            pad = [0, 0, 0, 0, 0, 0]; pad[2 * (2 - d)] = 1; pad[2 * (2 - d) + 1] = 1
            x = F.conv3d(F.pad(x, pad), k, groups=K)
    return x


# ----------------------------------------------------------- plan fields ---

NCLS = 12            # density classes: element (4) x slot (3)
ATTR = 12 + 3 + 12 + 8   # per element: raw prism 3 (x4) | Charge tier logits 3 | facing 3 (x4) | spindle raw 2 (x4)
FIELD_C = NCLS + ATTR


def _logit(x, lo, hi):
    s = ((x - lo) / max(hi - lo, 1e-6)).clamp(0.02, 0.98)
    return torch.log(s / (1 - s))


def raw_prism(h, elem):
    """Inverse of swarm_nca.prism_h (up to the Mass max-ratio clamp): half-extents -> raw state."""
    out = torch.zeros_like(h)
    for e in range(4):
        m = elem == e
        if not m.any():
            continue
        x = h[m]
        if e == 0:
            r = _logit(x, 0.3, 2.2)
        elif e == 1:
            r = _logit(x, 0.5, 1.8)
        elif e == 2:
            L = x[:, :1]; cmax = (L / 4).clamp(0.25, 0.42)
            s1 = ((x[:, 1:] - 0.25) / (cmax - 0.25).clamp(min=1e-3)).clamp(0.02, 0.98)
            r = torch.cat([_logit(L, 0.9, 3.0), torch.log(s1 / (1 - s1))], 1)
        else:
            L = x[:, :1]; cmax = (L / 1.5).clamp(0.3, 0.6)
            s1 = ((x[:, 1:] - 0.3) / (cmax - 0.3).clamp(min=1e-3)).clamp(0.02, 0.98)
            r = torch.cat([_logit(L, 0.45, 1.2), torch.log(s1 / (1 - s1))], 1)
        out[m] = r
    return out


def raw_spindle(sp):
    a = (sp[:, 0] / 0.6).clamp(0.02, 0.98)
    b = (sp[:, 1] / 0.5).clamp(-0.98, 0.98)
    return torch.stack([torch.log(a / (1 - a)), 0.5 * torch.log((1 + b) / (1 - b))], 1)


def unit_values(t):
    """Per target unit: [M, FIELD_C] values to splat (density one-hot | attribute numerators)."""
    e, sl = t["elem"], t["slot"]
    M = len(e)
    v = torch.zeros(M, FIELD_C)
    v[torch.arange(M), e * 3 + sl] = 1.0
    o = NCLS
    rp = raw_prism(t["h"], e)
    for k in range(3):
        v[torch.arange(M), o + e * 3 + k] = rp[:, k]
    o += 12
    tl = torch.log(F.one_hot(t["tier"], 3).float() * 0.9 + 0.1 / 3)
    v[:, o:o + 3] = tl * (e == 0)[:, None].float()
    o += 3
    for k in range(3):
        v[torch.arange(M), o + e * 3 + k] = t["f"][:, k]
    o += 12
    rs = raw_spindle(t["sp"])
    for k in range(2):
        v[torch.arange(M), o + e * 2 + k] = rs[:, k]
    return v


class PlanFields:
    """Every plan's every frame rasterised onto a grid centred on the plan's centroid (cached), so the
    field for a swarm is a copy placed at that swarm's centroid. Offsets below one cell are applied
    by splatting at the exact position (cheap: <= 192 points)."""

    def __init__(self, targets, G=G_DEFAULT, cell=CELL_DEFAULT, smooth=1):
        self.targets, self.G, self.cell, self.smooth = targets, G, cell, smooth
        self.vals = {}
        self.cent = {}
        for k, T in targets.items():
            self.vals[k] = [unit_values(fr) for fr in T.frames]
            self.cent[k] = [fr["p"] - fr["p"].mean(0) for fr in T.frames]

    def field(self, kinds, frames, centres):
        """kinds/frames lists (len B) -> [B, FIELD_C, G,G,G] placed at centres [B,3]."""
        B = len(kinds)
        M = max(len(self.cent[k][f]) for k, f in zip(kinds, frames))
        pos = torch.zeros(B, M, 3); val = torch.zeros(B, M, FIELD_C); mask = torch.zeros(B, M, dtype=torch.bool)
        for b, (k, f) in enumerate(zip(kinds, frames)):
            p = self.cent[k][f]; n = len(p)
            pos[b, :n] = p + centres[b]; val[b, :n] = self.vals[k][f]; mask[b, :n] = True
        fr = GridFrame(centres, self.G, self.cell)
        g = splat(fr, pos, val, mask)
        return blur(g, self.smooth) if self.smooth else g


def attr_at(fs, elem):
    """fs [N, FIELD_C] sampled field rows, elem [N] -> per-unit target state (raw prism 3, tier 3,
    facing 3, spindle 2), divided by the unit's element density. Units with no density get zeros."""
    N = fs.shape[0]
    ar = torch.arange(N)
    dens = fs[:, :NCLS].view(N, 4, 3).sum(-1)[ar, elem].clamp(min=1e-3)
    o = NCLS
    pr = torch.stack([fs[ar, o + elem * 3 + k] for k in range(3)], 1) / dens[:, None]
    o += 12
    dc = fs[:, :3].sum(-1).clamp(min=1e-3)
    tl = fs[:, o:o + 3] / dc[:, None]
    o += 3
    fa = torch.stack([fs[ar, o + elem * 3 + k] for k in range(3)], 1)
    o += 12
    spn = torch.stack([fs[ar, o + elem * 2 + k] for k in range(2)], 1) / dens[:, None]
    has = (fs[:, :NCLS].view(N, 4, 3).sum(-1)[ar, elem] > 1e-3).to(fs.dtype)[:, None]
    return pr * has, tl, fa, spn * has
