"""Prism particles: the collision automaton's 3D particles constrained to Cosmic Shore's prism vocabulary.

Every live particle IS a prism, and its visible state can only say what a prism can say:

  domain   Jade | Ruby | Gold                                   channels 16-18 (logits)
  tier     plain box | danger box | shield octahedron | super-shield stella octangula
                                                                channels 19-22 (logits)
  scale    three half-extents (x, y, z)                         channels 23-25
  rotation a 6D (two-column) rotation                           channels 26-31
  presence alpha                                                channel 3 (the alive/budding channel, as before)

Channels 0-2 and 4-15 are free hidden state (the 16-channel particle model's layout is kept, so a
2D particle model warm-starts the first 16 channels unchanged).

Colours are the live palette (`OriginalColorSetSO`): each (domain, tier) pair is one representative
screen colour, the prism's base face lifted 35% toward its fresnel rim, linear -> gamma. A danger prism
is the domain's SHIELDED base under the domain-independent danger rim, exactly as `PALETTE.md` §2.1
composes it. Twelve appearances, and nothing between them: colour and tier are STRAIGHT-THROUGH
one-hots (the forward pass renders the argmax; the gradient flows through the softmax), so a trained
rule can only ever show a prism the game could draw.

Shapes are the game's exact containment tests, as gauges (inside iff <= 1) in half-extent units:
  box      max |u|
  shield   (|u|_1) / 3                          `OctahedronMeshGenerator`, circumscribing scale 3
  stella   min over the two tetrahedra of max of the four face forms, at the same scale 3
           (`StellatedOctahedronMeshGenerator.ContainsPointLocal`)
so shielding a prism really does grow its footprint 4.5x and super-shielding it 13.5x, and a rule
that shields a particle pays for the volume in the loss.

Rendering is solid-prism compositing on the voxel grid: each prism's soft occupancy k in [0, 1]
(a C1 smoothstep ramp EDGE voxels wide across the surface: the fraction of a voxel a flat face
covers is linear in the centre's distance to it over exactly one voxel, which is how the box-filtered
target was built - a logistic's long tail triples a small prism's rendered volume) is combined as a UNION (1 - prod(1 - k a)), and the
colour is the occupancy-weighted mean of the prisms covering the voxel - overlapping prisms do not
add brightness, they occlude.
"""
import colorsys
import math

import numpy as np
import torch
import torch.nn.functional as F

DOMAINS = ("Jade", "Ruby", "Gold")
TIERS = ("plain", "danger", "shield", "super")
CH_DOMAIN, CH_TIER, CH_SCALE, CH_ROT = slice(16, 19), slice(19, 23), slice(23, 26), slice(26, 32)
CHANNELS = 32

H0 = 0.9          # resting half-extent, voxels (a 1.8-voxel prism)
SPAN = 0.4        # half-extent = H0 * exp(SPAN * tanh(c)): 0.60 .. 1.34 voxels per axis
EDGE = 1.0        # surface ramp width, voxels: the exact box filter of a flat face
SHIELD = 3.0      # the game's CIRCUMSCRIBING_SCALE (both shield shapes)
REACH = (math.sqrt(3), math.sqrt(3), SHIELD, SHIELD * math.sqrt(3))   # farthest point, in half-extents
FACE = (1.0, 1.0, SHIELD / math.sqrt(3), SHIELD / math.sqrt(3))       # centre-to-face distance, half-extents
HUE_ROT = 0.10    # target recolour: rotate the emoji's hue before the nearest-colour match

# Live palette, OriginalColorSetSO (linear HDR): (base face, fresnel rim) per domain per tier.
_PAL = {
    "Jade": {"plain": ((0.0, 0.16, 0.34), (0.0, 0.5885, 1.1353)),
             "shield": ((0.0868, 0.2367, 0.4843), (0.4094, 0.6419, 1.2168)),
             "super": ((0.0353, 0.2039, 0.3686), (1.0128, 1.1210, 1.4980))},
    "Ruby": {"plain": ((0.1191, 0.0068, 0.3082), (0.5490, 0.0, 1.4980)),
             "shield": ((0.3346, 0.1639, 0.4751), (0.9042, 0.4972, 1.1930)),
             "super": ((0.2118, 0.1216, 0.7098), (0.8383, 0.7302, 1.4980))},
    "Gold": {"plain": ((0.2390, 0.1248, 0.0), (0.9964, 0.4049, 0.0186)),
             "shield": ((0.3134, 0.2099, 0.0821), (0.8445, 0.5967, 0.3828)),
             "super": ((0.5961, 0.2863, 0.0039), (1.4980, 1.1187, 0.5323))},
}
_DANGER_RIM = (1.4979, 0.0058, 0.0068)


def palette_pairs():
    """[3, 4, 2, 3] linear (base, rim) for every (domain, tier)."""
    out = np.zeros((3, 4, 2, 3), np.float32)
    for i, d in enumerate(DOMAINS):
        for j, t in enumerate(TIERS):
            base, rim = (_PAL[d]["shield"][0], _DANGER_RIM) if t == "danger" else _PAL[d][t]
            out[i, j] = base, rim
    return out


def palette_colours(lift=0.35):
    """[3, 4, 3] representative display colour of each prism appearance."""
    p = palette_pairs()
    c = np.clip((1 - lift) * p[..., 0, :] + lift * p[..., 1, :], 0, 1)
    return (c ** (1 / 2.2)).astype(np.float32)


COLOURS = torch.from_numpy(palette_colours())       # [3, 4, 3]


def _lab(c):
    c = np.asarray(c, np.float64)
    lin = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.9505, 1.0, 1.089])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def quantise(frames, hue_rot=HUE_ROT):
    """Premultiplied RGBA frames [..., 4] -> the same frames with every coloured voxel repainted in
    the nearest prism appearance (CIELAB, after rotating the emoji's hue by `hue_rot` turns - without
    it every green lands on Gold). Alpha is untouched. Returns (frames, index [...]: 0..11 or -1)."""
    a = frames[..., 3]
    rgb = frames[..., :3] / np.maximum(a[..., None], 1e-6)
    live = a > 1e-3
    src = np.clip(rgb[live], 0, 1)
    hsv = np.array([colorsys.rgb_to_hsv(*c) for c in src]) if len(src) else np.zeros((0, 3))
    rot = np.array([colorsys.hsv_to_rgb((h + hue_rot) % 1, s, v) for h, s, v in hsv]) if len(src) else src
    pal = palette_colours().reshape(12, 3)
    idx = np.argmin(((_lab(rot)[:, None] - _lab(pal)[None]) ** 2).sum(-1), 1)
    out = frames.copy()
    out[..., :3] = 0
    q = np.full(a.shape, -1, np.int64)
    q[live] = idx
    out[live, :3] = pal[idx] * a[live][:, None]
    return out.astype(np.float32), q


def prism_target(cfg):
    from nca3d import Config3D, build_frames as b3
    fr = b3(Config3D(frames=cfg.frames, period=cfg.period, amp=cfg.amp))   # [K, 22, 44, 44, 4]
    return quantise(fr)[0]


# ------------------------------------------------------------------ decode ---

def _st(logits):
    """Straight-through one-hot: forward = argmax one-hot, backward = softmax."""
    p = F.softmax(logits, -1)
    hard = F.one_hot(p.argmax(-1), p.shape[-1]).to(p.dtype)
    return hard + p - p.detach()


def rotation(s):
    """[..., 3, 3] rotation (columns = the prism's local axes in world) from channels 26-31.
    Zero channels are the identity."""
    a = s[..., 26:29] + torch.tensor([1.0, 0.0, 0.0], device=s.device)
    b = s[..., 29:32] + torch.tensor([0.0, 1.0, 0.0], device=s.device)
    r1 = a / a.norm(dim=-1, keepdim=True).clamp(min=1e-6)
    b = b - (r1 * b).sum(-1, keepdim=True) * r1
    r2 = b / b.norm(dim=-1, keepdim=True).clamp(min=1e-6)
    r3 = torch.cross(r1, r2, dim=-1)
    return torch.stack([r1, r2, r3], -1)


def half_extents(s):
    return H0 * torch.exp(SPAN * torch.tanh(s[..., 23:26]))


def decode(s):
    """-> colour [...,3] (straight-through), tier one-hot [...,4] (straight-through), half-extents
    [...,3], rotation [...,3,3], presence [...]."""
    pd, pt = _st(s[..., 16:19]), _st(s[..., 19:23])
    colour = torch.einsum("...d,...t,dtc->...c", pd, pt, COLOURS.to(s.device))
    return colour, pt, half_extents(s), rotation(s), s[..., 3].clamp(0, 1)


def gauges(u):
    """[..., 3] half-extent-normalised local coords -> [..., 4] gauges (inside iff <= 1) for
    plain, danger, shield, super."""
    au = u.abs()
    box = au.amax(-1)
    octa = au.sum(-1) / SHIELD
    v = u / SHIELD
    f = torch.stack([v[..., 0] + v[..., 1] + v[..., 2], v[..., 0] - v[..., 1] - v[..., 2],
                     -v[..., 0] + v[..., 1] - v[..., 2], -v[..., 0] - v[..., 1] + v[..., 2]], -1)
    stella = torch.minimum((-f).amax(-1), f.amax(-1))
    return torch.stack([box, box, octa, stella], -1)


# ------------------------------------------------------------------ render ---

_OFFS = {}


def _offsets(rad, device=None):
    """Integer offsets within a ball of radius `rad` (voxels), nearest first."""
    key = (rad, str(device))
    if key not in _OFFS:
        r = int(math.ceil(rad))
        g = torch.stack(torch.meshgrid(*[torch.arange(-r, r + 1, dtype=torch.float32, device=device)] * 3, indexing="ij"), -1).reshape(-1, 3)
        g = g[(g ** 2).sum(-1) <= rad * rad]
        _OFFS[key] = g[torch.argsort((g ** 2).sum(-1))]
    return _OFFS[key]


def tier_radius(t):
    """Voxel-offset radius that covers any tier-t prism at the largest scale (+ edge, + cell diagonal)."""
    return round(H0 * math.exp(SPAN) * REACH[t] + EDGE / 2 + 0.9, 1)


def prism_splat(st, grid):
    """Premultiplied RGBA [B, D, H, W, 4] of the particles drawn as solid prisms. Positions are
    (x, y, z) in voxels, cell k's centre at k + 0.5. Differentiable in position, presence, colour,
    tier, scale and rotation. Each particle is rasterised over the offset ball of ITS tier's reach,
    so the cost is the box's for box prisms and only the (rare) stellae pay for their size."""
    B, N, _ = st.pos.shape
    D, H, W = grid
    size = D * H * W
    act = st.active.reshape(-1)
    pos = st.pos.reshape(-1, 3)
    s = st.s.reshape(B * N, -1)
    live = act.nonzero().squeeze(1)
    colour, pt, h, R, a = decode(s[live])
    tier = pt.detach().argmax(-1)
    bidx = torch.div(live, N, rounding_mode="floor")
    dev = pos.device
    logT = torch.zeros(B * size, device=dev)
    wsum = torch.zeros(B * size, device=dev)
    csum = torch.zeros(B * size, 3, device=dev)
    for t in range(4):
        sel = (tier == t).nonzero().squeeze(1)
        if len(sel) == 0:
            continue
        off = _offsets(tier_radius(t), pos.device)                       # [O, 3]
        p = pos[live[sel]]                                               # [n, 3]
        cells = torch.floor(p.detach())[:, None, :] + off[None]          # [n, O, 3]
        dx = cells + 0.5 - p[:, None, :]
        u = torch.einsum("noc,ncj->noj", dx, R[sel]) / h[sel][:, None, :]    # local, in half-extents
        d = (gauges(u) * pt[sel][:, None, :]).sum(-1)                   # straight-through tier blend
        face = (pt[sel] * torch.tensor(FACE, device=pos.device)).sum(-1)   # [n]
        hg = h[sel].prod(-1) ** (1 / 3)
        x = (0.5 + (1 - d) * (face * hg)[:, None] / EDGE).clamp(0, 1)   # signed depth below the surface, in ramps
        k = x * x * (3 - 2 * x)                                          # [n, O] soft occupancy
        ka = (k * a[sel][:, None]).clamp(max=0.999)
        ci = cells.long()
        inb = (ci[..., 0] >= 0) & (ci[..., 0] < W) & (ci[..., 1] >= 0) & (ci[..., 1] < H) & \
              (ci[..., 2] >= 0) & (ci[..., 2] < D) & (ka.detach() > 1e-4)
        flat = ((ci[..., 2] * H + ci[..., 1]) * W + ci[..., 0]) + (bidx[sel] * size)[:, None]
        f, kk = flat[inb], ka[inb]
        logT = logT.index_add(0, f, torch.log1p(-kk))
        wsum = wsum.index_add(0, f, kk)
        csum = csum.index_add(0, f, kk[:, None] * colour[sel][:, None, :].expand(-1, off.shape[0], -1)[inb])
    A = 1 - torch.exp(logT)
    rgb = csum / (wsum[:, None] + 1e-6) * A[:, None]
    return torch.cat([rgb, A[:, None]], 1).view(B, D, H, W, 4)


def prism_table(st, b=0):
    """Numpy per-prism rows for sample b: pos(3) half-extents(3) rotation(9, row-major) domain tier alpha."""
    act = st.active[b]
    s = st.s[b][act].detach().cpu()
    pos = st.pos[b][act].detach().cpu()
    with torch.no_grad():
        h, R = half_extents(s), rotation(s)
        dom, tier = s[:, 16:19].argmax(-1), s[:, 19:23].argmax(-1)
    return np.concatenate([pos.numpy(), h.numpy(), R.reshape(-1, 9).numpy(),
                           dom[:, None].float().numpy(), tier[:, None].float().numpy(),
                           s[:, 3:4].clamp(0, 1).numpy()], 1).astype(np.float32)


# ---------------------------------------------------------------- raytrace ---
# Exact ray-convex intersection of every prism, for figures. Each shape is one or two convex
# pieces given as planes n . u <= c in half-extent coordinates u (the same shapes the gauges test).

_EPS = np.array([(1, 1, 1), (1, -1, -1), (-1, 1, -1), (-1, -1, 1)], np.float32)
_BOX = [(np.concatenate([np.eye(3), -np.eye(3)]), np.ones(6))]
_OCT = [(np.array([(a, b, c) for a in (1, -1) for b in (1, -1) for c in (1, -1)], np.float32), np.full(8, SHIELD))]
_STELLA = [(-_EPS, np.full(4, SHIELD)), (_EPS, np.full(4, SHIELD))]
_PIECES = (_BOX, _BOX, _OCT, _STELLA)


def _camera(grid, az, tilt, px, zoom):
    D, H, W = grid
    R = zoom * math.sqrt(D * D + H * H + W * W)
    fwd = np.array([math.sin(tilt) * math.cos(az), math.sin(tilt) * math.sin(az), -math.cos(tilt)])
    hint = np.array([-math.cos(az), -math.sin(az), 0.0]) if tilt > 1e-3 else np.array([0.0, -1.0, 0.0])
    right = np.cross(fwd, hint); right /= np.linalg.norm(right)
    up = np.cross(right, fwd)
    return np.array([W / 2, H / 2, D / 2]), R, fwd, right, up


def raytrace(table, grid, az=0.9, tilt=0.95, px=240, zoom=0.40, bg=(1.0, 1.0, 1.0),
             light=(-0.35, -0.55, 0.75), min_alpha=0.1):
    """[px, px, 3] image of the prisms in `table` (rows from `prism_table`), each drawn as its exact
    solid with its tier's base face lit Lambert and its fresnel rim toward grazing angles - the
    palette pair the game's prism shader composes. Same orthographic camera as nca3d.render.
    Rasterised: each convex piece intersects only the rays inside its projected bounding circle,
    against a depth buffer."""
    t = table[table[:, 17] > min_alpha]
    centre, R, fwd, right, up = _camera(grid, az, tilt, px, zoom)
    g = np.linspace(-R, R, px)
    pix = (px - 1) / (2 * R)
    pairs = palette_pairs()
    L = np.array(light) / np.linalg.norm(light)
    depth = np.full((px, px), np.inf)
    nbuf = np.zeros((px, px, 3)); cbase = np.zeros((px, px, 3)); crim = np.zeros((px, px, 3))
    for row in t:
        pos, h, Rm = row[0:3], row[3:6], row[6:15].reshape(3, 3)
        tier, dom = int(row[16]), int(row[15])
        reach = REACH[tier] * float(h.max())
        u0, v0 = float((pos - centre) @ right), float((pos - centre) @ up)
        c0, c1 = max(0, int((u0 - reach + R) * pix)), min(px - 1, int(math.ceil((u0 + reach + R) * pix)))
        r0, r1 = max(0, int((-v0 - reach + R) * pix)), min(px - 1, int(math.ceil((-v0 + reach + R) * pix)))
        if c0 > c1 or r0 > r1:
            continue
        gu, gv = np.meshgrid(g[c0:c1 + 1], -g[r0:r1 + 1])
        o = centre + gu[..., None] * right + gv[..., None] * up - fwd * R * 1.6       # [r, c, 3]
        a = ((o - pos) @ Rm) / h                                                        # u of ray origins
        b = (fwd @ Rm) / h
        for n, c in _PIECES[tier]:
            na, nb = a @ n.T, n @ b                                                     # [r, c, k], [k]
            with np.errstate(divide="ignore", invalid="ignore"):
                q = (c - na) / nb
            enter = np.where(nb < 0, q, -np.inf); exitt = np.where(nb > 0, q, np.inf)
            bad = ((np.abs(nb) < 1e-9) & (na > c)).any(-1)
            t_in, k_in, t_out = enter.max(-1), enter.argmax(-1), exitt.min(-1)
            hit = (t_in <= t_out) & ~bad & (t_out > 0)
            t_in = np.where(hit, np.maximum(t_in, 0), np.inf)
            reg = depth[r0:r1 + 1, c0:c1 + 1]
            win = t_in < reg
            if not win.any():
                continue
            reg[win] = t_in[win]
            nw = (Rm @ (n[k_in[win]] / h).T).T
            nbuf[r0:r1 + 1, c0:c1 + 1][win] = nw / np.linalg.norm(nw, axis=-1, keepdims=True).clip(1e-9)
            cbase[r0:r1 + 1, c0:c1 + 1][win] = pairs[dom, tier, 0]
            crim[r0:r1 + 1, c0:c1 + 1][win] = pairs[dom, tier, 1]
    img = np.broadcast_to(np.array(bg, np.float64), (px, px, 3)).copy()
    m = np.isfinite(depth)
    n = nbuf[m]
    lam = np.clip(n @ L, 0, 1)[:, None]
    fres = (1 - np.abs(n @ fwd))[:, None] ** 3
    lin = cbase[m] * (0.45 + 0.9 * lam) + crim[m] * (0.25 + 0.75 * fres) * 0.55
    img[m] = np.clip(lin, 0, 1) ** (1 / 2.2)
    return img.astype(np.float32)
