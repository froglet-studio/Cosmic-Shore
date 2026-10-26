#!/usr/bin/env python3
"""
Author the STYLE-B COMPONENT SPRITE KIT (UI redesign task T7) - white + alpha, tinted at runtime.

Every shape the style-B menu redesign draws comes from here (Docs/MENU_REDESIGN_B.md, item 0):

    kit_sliver14_{fill,line1,line2}[_flip]   panels, cards, popups, list tiles   (§5: 14 px)
    kit_sliver10_{fill,line1,line2}[_flip]   buttons, chips, small tiles, rings  (§5: 10 px)
    kit_hex_{fill,line1,line2}               icon-only nav tiles (§10.5, §10.12) 88 x 76
    kit_hex_handle                           the settings slider handle (§10.7)  32 x 28
    kit_banner_body / _cap_left / _cap_right the end-game header (§10.9) - authored so T7 closes;
                                             the end-game screens are OUT of the menu redesign
    kit_fade_v                               a vertical alpha ramp, clear at the top to solid at the
                                             bottom: the scrim under a hero frame's title (item 2)
    kit_fade_h                               a horizontal alpha ramp, solid at the left to clear at the
                                             right: the scrim behind a feature card's copy (item 3)
    kit_oct_fill                             the avatar octagon (29% corner cuts), 48 x 48: the
                                             party seat's domain ring and every avatar backing (item 3)
    kit_grid                                 the blueprint grid tile (revision 2, §13.4): a 1-unit line on
                                             its top and left edges every 24 units; drawn Tiled, tinted
                                             Neutral Dark, under a panel's content
    kit_bracket                              a 22-unit corner bracket (1-unit L, top-left); the other
                                             three corners flip it by scale
    kit_spark / kit_spark_v                  the travelling highlight (KitSpark): a 48 x 2 streak (and its
                                             2 x 48 vertical twin) clear at both ends, solid in the middle
    kit_oct_line2                            the octagon's 2-unit outline, 48 x 48: an open seat's ring
                                             (section 74, the configure pedestals)
    kit_pool / kit_pool_rim                  the pool of light a configure pedestal's pilot stands on, 120 x 30:
                                             a soft ellipse falling off to clear, and its 1.5-unit rim

THE SLIVER (Docs/STYLE_FOUNDATION.md §5): a 45 deg cut on TWO OPPOSITE corners. Normal = top-left
and bottom-right; `_flip` = top-right and bottom-left (§5 "flippable"). `fill` is the whole
silhouette; `line1`/`line2` are the outline band at the §5 hairline (1) and emphasis stroke (2).
A slivered surface is a `fill` Image tinted the surface colour with a `line` Image on top tinted
the border colour - the band's outer edge IS the silhouette, so the two always register.

UNITS. One canvas unit (the 1920x1080 reference, Menu_Main's `m_ReferencePixelsPerUnit: 240`) is
TWO texels, so the kit stays crisp at 1440p and 4K. That makes every sprite PPU 480: a sliced
Image draws its border at `spriteBorder / (spritePPU / canvasReferencePPU)`, so a 36-texel border
renders as 18 canvas units - the same arithmetic `author_ui_frame_sprites.py` documents.

WHAT 9-SLICES. The slivers are sliced: each cut sits wholly inside its own corner tile, and the
stretch band is uniform (asserted), so a sliver holds its 14/10 px cut at ANY width or height -
the "slivers hold their shape at any width" criterion. The hexagons are drawn SIMPLE at their
design size: a hexagon's slanted sides run the full height, so no 9-slice preserves them. The
banner slices horizontally only; its slant is authored for its 56-unit design height.

    python3 Tools/Build/author_ui_kit_sprites.py              # write the PNGs + .meta
    python3 Tools/Build/author_ui_kit_sprites.py --check      # fail if any file drifted
    python3 Tools/Build/author_ui_kit_sprites.py --self-test  # prove the shape asserts fire
"""

import argparse
import hashlib
import math
import os
import re
import struct
import sys
import zlib

import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(REPO, "Assets", "_Graphics", "UI", "Kit")

TEXELS_PER_UNIT = 2          # one 1920-reference canvas unit = two texels
PPU = 240 * TEXELS_PER_UNIT  # Menu_Main's CanvasScaler.m_ReferencePixelsPerUnit is 240
SUPERSAMPLE = 8

SLIVERS = (14, 10)           # §5 sliverLarge / sliverSmall, in canvas units
LINES = (1, 2)               # §5 hairline / stroke, in canvas units

HEX_W, HEX_H = 88, 76        # §10.12 nav tile, canvas units (the approved kit mockup)
HANDLE_W, HANDLE_H = 32, 28  # §10.7 slider handle, canvas units
BANNER_H, BANNER_SLANT = 56, 14
FADE_W, FADE_H = 4, 64       # texels; a 1-D ramp, stretched to any size
OCT = 48                     # the avatar octagon, canvas units (drawn simple, like the hexagons)
OCT_CUT = 0.29               # corner cut as a fraction of the side - the mockup's clip-path
GRID = 24                    # blueprint grid pitch, canvas units (§5 spacing scale)
BRACKET = 22                 # corner bracket arm, canvas units
SPARK_W, SPARK_H = 48, 2     # spark streak, canvas units
POOL_W, POOL_H = 120, 30     # a pedestal's pool of light, canvas units (drawn simple)
POOL_RIM = 1.5               # the pool's rim, canvas units
# The outline sprites carry a soft INNER glow under the band (the owner, 2026-10-17: "make the
# borders glow everywhere in the menu"). Baked into the texture, so every kit border glows in its
# own tint at zero runtime cost. It runs GLOW_U units inward from the band's inner edge, inside the
# 9-slice corner tile, so a sliced outline still holds its cut at any size; it never reaches the
# sprite's centre (an outline stays hollow) and never reaches the 0.5 alpha a band texel has.
GLOW_U = 6.0
GLOW_PEAK = {"line1": 0.32, "line2": 0.42}
GLOW_STEP = 0.25


def guid_for(name):
    return hashlib.md5(f"CosmicShore/UIKit/{name}".encode()).hexdigest()


# --------------------------------------------------------------------------------------------
# Geometry. Every predicate takes x, y in CANVAS UNITS, y counting DOWN from the top edge.
# --------------------------------------------------------------------------------------------

def grid(w_tex, h_tex):
    n = SUPERSAMPLE
    step = 1.0 / n
    xs = (np.arange(w_tex * n) * step + step / 2) / TEXELS_PER_UNIT
    ys = (np.arange(h_tex * n) * step + step / 2) / TEXELS_PER_UNIT
    return xs[None, :], ys[:, None]


def downsample(mask, w_tex, h_tex):
    n = SUPERSAMPLE
    return mask.reshape(h_tex, n, w_tex, n).mean(axis=(1, 3))


def sliver_mask(xs, ys, w, h, cut, flip, inset=0.0):
    """Points inside the sliver rect [0,w]x[0,h] with `cut`-unit 45 deg cuts on two opposite
    corners, offset INWARD by `inset` units perpendicular to every edge. A perpendicular inset
    moves a straight edge by `inset` and a 45 deg edge's x+y line by inset*sqrt(2)."""
    d = cut + inset * math.sqrt(2.0)
    box = (xs >= inset) & (xs <= w - inset) & (ys >= inset) & (ys <= h - inset)
    if not flip:
        cuts = ((xs + ys) >= d) & (((w - xs) + (h - ys)) >= d)
    else:
        cuts = (((w - xs) + ys) >= d) & ((xs + (h - ys)) >= d)
    return box & cuts


def convex_mask(xs, ys, verts, inset=0.0):
    """Points inside a convex polygon (clockwise in y-down space) at least `inset` from every
    edge - so `inset=0` is the polygon and `inset=w` its inner offset."""
    out = np.ones(np.broadcast(xs, ys).shape, dtype=bool)
    for (x0, y0), (x1, y1) in zip(verts, verts[1:] + verts[:1]):
        ex, ey = x1 - x0, y1 - y0
        ln = math.hypot(ex, ey)
        # inward normal for a clockwise loop in y-down coordinates
        nx, ny = -ey / ln, ex / ln
        out &= ((xs - x0) * nx + (ys - y0) * ny) >= inset
    return out


def hex_verts(w, h):
    # flat-top: (25%,0) (75%,0) (100%,50%) (75%,100%) (25%,100%) (0,50%) - clockwise, y down
    return [(0.25 * w, 0.0), (0.75 * w, 0.0), (w, 0.5 * h),
            (0.75 * w, h), (0.25 * w, h), (0.0, 0.5 * h)]


# --------------------------------------------------------------------------------------------
# Sprite set
# --------------------------------------------------------------------------------------------

def sliver_border_units(cut):
    # The corner tile must hold the cut AND the 2-unit band's inner cut (it meets the inset edge
    # cut + 2*sqrt2 - 2 = cut + 0.83 units from the corner), plus antialiasing. Kept this tight on
    # purpose: a sliced Image shrinks its borders once the rect is smaller than both together, so
    # cut + 2 lets a 24-unit keycap keep its full 10-unit cut.
    return cut + 2


def inner_glow(xs, ys, inside_at, width, kind):
    """The soft glow just inside an outline band: `inside_at(t)` is the shape inset by t units; a
    point's distance in from the band's inner edge is how many GLOW_STEP insets still hold it.
    Falls off quadratically to zero at GLOW_U."""
    depth = np.zeros(np.broadcast(xs, ys).shape)
    t = GLOW_STEP
    while t <= GLOW_U:
        depth += inside_at(width + t)
        t += GLOW_STEP
    d = depth * GLOW_STEP                          # units in from the band's inner edge
    inside = inside_at(width)
    glow = np.where(inside, (1.0 - np.clip(d / GLOW_U, 0.0, 1.0)) ** 2, 0.0)
    return glow * GLOW_PEAK[kind]


def build_sliver(cut, kind, flip):
    border_u = sliver_border_units(cut)
    border_tex = border_u * TEXELS_PER_UNIT
    side_tex = 2 * border_tex + 4               # a 4-texel uniform stretch band
    side_u = side_tex / TEXELS_PER_UNIT
    xs, ys = grid(side_tex, side_tex)
    outer = sliver_mask(xs, ys, side_u, side_u, cut, flip)
    if kind == "fill":
        mask = outer
    else:
        width = LINES[0] if kind == "line1" else LINES[1]
        mask = outer & ~sliver_mask(xs, ys, side_u, side_u, cut, flip, inset=width)
        # Inside the four 9-slice corner tiles the glow follows the 45 deg cut (inset depth); along the
        # stretched edges it is measured from the straight edges only, so every stretched row and
        # column stays identical (the slice-safety assert). At a tile's inner boundary the two agree to
        # within the glow's faint tail.
        corner = ((xs < border_u) | (xs > side_u - border_u)) & ((ys < border_u) | (ys > side_u - border_u))
        follow = inner_glow(xs, ys, lambda t: sliver_mask(xs, ys, side_u, side_u, cut, flip, inset=t), width, kind)
        box = np.minimum(np.minimum(xs, side_u - xs), np.minimum(ys, side_u - ys)) - width
        straight = np.where(box >= 0, (1.0 - np.clip(box / GLOW_U, 0.0, 1.0)) ** 2, 0.0) * GLOW_PEAK[kind]
        glow = np.where(corner, follow, straight) * sliver_mask(xs, ys, side_u, side_u, cut, flip, inset=width)
        return downsample(np.maximum(mask.astype(float), glow), side_tex, side_tex), border_tex
    alpha = downsample(mask, side_tex, side_tex)
    return alpha, border_tex


def build_hex(w, h, kind):
    w_tex, h_tex = w * TEXELS_PER_UNIT, h * TEXELS_PER_UNIT
    xs, ys = grid(w_tex, h_tex)
    verts = hex_verts(w, h)
    outer = convex_mask(xs, ys, verts)
    if kind == "fill":
        mask = outer
    else:
        width = LINES[0] if kind == "line1" else LINES[1]
        mask = outer & ~convex_mask(xs, ys, verts, inset=width)
        glow = inner_glow(xs, ys, lambda t: convex_mask(xs, ys, verts, inset=t), width, kind)
        return downsample(np.maximum(mask.astype(float), glow), w_tex, h_tex), 0
    return downsample(mask, w_tex, h_tex), 0


def build_banner(part):
    h_tex = BANNER_H * TEXELS_PER_UNIT
    if part == "body":
        border_u = BANNER_SLANT + 2
        w_tex = 2 * border_u * TEXELS_PER_UNIT + 4
        w = w_tex / TEXELS_PER_UNIT
        xs, ys = grid(w_tex, h_tex)
        verts = [(BANNER_SLANT, 0.0), (w, 0.0), (w - BANNER_SLANT, BANNER_H), (0.0, BANNER_H)]
        return downsample(convex_mask(xs, ys, verts), w_tex, h_tex), border_u * TEXELS_PER_UNIT
    cap_w = BANNER_H // 2
    w_tex = cap_w * TEXELS_PER_UNIT
    xs, ys = grid(w_tex, h_tex)
    if part == "cap_left":       # points LEFT
        verts = [(cap_w, 0.0), (cap_w, BANNER_H), (0.0, BANNER_H / 2)]   # clockwise, y down
    else:                        # points RIGHT
        verts = [(0.0, 0.0), (cap_w, BANNER_H / 2), (0.0, BANNER_H)]
    return downsample(convex_mask(xs, ys, verts), w_tex, h_tex), 0


def build_fade():
    """Alpha rises linearly from 0 at the top row to 1 at the bottom row (texel centres)."""
    ramp = (np.arange(FADE_H) + 0.5) / FADE_H
    return np.repeat(ramp[:, None], FADE_W, axis=1), 0


def build_fade_h():
    """Alpha falls linearly from 1 at the left column to 0 at the right column."""
    ramp = 1.0 - (np.arange(FADE_H) + 0.5) / FADE_H
    return np.repeat(ramp[None, :], FADE_W, axis=0), 0


def oct_verts(side, cut=OCT_CUT):
    c = cut * side
    return [(c, 0.0), (side - c, 0.0), (side, c), (side, side - c),
            (side - c, side), (c, side), (0.0, side - c), (0.0, c)]


def build_oct():
    t = OCT * TEXELS_PER_UNIT
    xs, ys = grid(t, t)
    return downsample(convex_mask(xs, ys, oct_verts(OCT)), t, t), 0


def build_oct_line(width=2.0):
    t = OCT * TEXELS_PER_UNIT
    xs, ys = grid(t, t)
    verts = oct_verts(OCT)
    band = convex_mask(xs, ys, verts) & ~convex_mask(xs, ys, verts, inset=width)
    return downsample(band, t, t), 0


def ellipse_r(xs, ys, w, h, inset=0.0):
    """Normalised ellipse radius (1 on the edge) of the w x h ellipse centred in its box, shrunk by `inset` units."""
    a, b = w / 2 - inset, h / 2 - inset
    return np.sqrt(((xs - w / 2) / a) ** 2 + ((ys - h / 2) / b) ** 2)


def build_pool():
    """Alpha falls from solid at the centre to clear at the ellipse's edge (a squared falloff: light, not a disc)."""
    w, h = POOL_W * TEXELS_PER_UNIT, POOL_H * TEXELS_PER_UNIT
    xs, ys = grid(w, h)
    r = ellipse_r(xs, ys, POOL_W, POOL_H)
    return downsample(np.clip(1.0 - r, 0.0, 1.0) ** 2 / 1.0, w, h), 0


def build_pool_rim():
    w, h = POOL_W * TEXELS_PER_UNIT, POOL_H * TEXELS_PER_UNIT
    xs, ys = grid(w, h)
    band = (ellipse_r(xs, ys, POOL_W, POOL_H) <= 1.0) & (ellipse_r(xs, ys, POOL_W, POOL_H, POOL_RIM) > 1.0)
    return downsample(band, w, h), 0


def build_grid():
    """A 1-unit line along the top and the left edge of one 24-unit tile; tiled, they make the grid."""
    t = GRID * TEXELS_PER_UNIT
    a = np.zeros((t, t))
    a[:TEXELS_PER_UNIT, :] = 1.0
    a[:, :TEXELS_PER_UNIT] = 1.0
    return a, 0


def build_bracket():
    """A top-left L: a 1-unit line along the top edge and down the left edge, 22 units each way."""
    t = BRACKET * TEXELS_PER_UNIT
    a = np.zeros((t, t))
    a[:TEXELS_PER_UNIT, :] = 1.0
    a[:, :TEXELS_PER_UNIT] = 1.0
    return a, 0


def build_spark():
    """Alpha follows a raised-cosine bump along x: clear at both ends, solid in the middle."""
    w, h = SPARK_W * TEXELS_PER_UNIT, SPARK_H * TEXELS_PER_UNIT
    x = (np.arange(w) + 0.5) / w
    bump = 0.5 - 0.5 * np.cos(2 * np.pi * x)
    return np.repeat(bump[None, :], h, axis=0), 0


def sprite_set():
    """(name, alpha, 9-slice border in texels as (left, bottom, right, top))."""
    out = []
    for cut in SLIVERS:
        for kind in ("fill", "line1", "line2"):
            for flip in (False, True):
                a, b = build_sliver(cut, kind, flip)
                name = f"kit_sliver{cut}_{kind}" + ("_flip" if flip else "")
                out.append((name, a, (b, b, b, b), dict(cut=cut, kind=kind, flip=flip)))
    for kind in ("fill", "line1", "line2"):
        a, _ = build_hex(HEX_W, HEX_H, kind)
        out.append((f"kit_hex_{kind}", a, (0, 0, 0, 0), dict(hex=kind)))
    a, _ = build_hex(HANDLE_W, HANDLE_H, "fill")
    out.append(("kit_hex_handle", a, (0, 0, 0, 0), dict(hex="handle")))
    a, b = build_banner("body")
    out.append(("kit_banner_body", a, (b, 0, b, 0), dict(banner="body")))
    for part in ("cap_left", "cap_right"):
        a, _ = build_banner(part)
        out.append((f"kit_banner_{part}", a, (0, 0, 0, 0), dict(banner=part)))
    a, _ = build_fade()
    out.append(("kit_fade_v", a, (0, 0, 0, 0), dict(fade=True)))
    a, _ = build_fade_h()
    out.append(("kit_fade_h", a, (0, 0, 0, 0), dict(fade_h=True)))
    a, _ = build_oct()
    out.append(("kit_oct_fill", a, (0, 0, 0, 0), dict(oct=True)))
    a, _ = build_oct_line()
    out.append(("kit_oct_line2", a, (0, 0, 0, 0), dict(oct_line=True)))
    a, _ = build_pool()
    out.append(("kit_pool", a, (0, 0, 0, 0), dict(pool=True)))
    a, _ = build_pool_rim()
    out.append(("kit_pool_rim", a, (0, 0, 0, 0), dict(pool_rim=True)))
    a, _ = build_grid()
    out.append(("kit_grid", a, (0, 0, 0, 0), dict(grid=True)))
    a, _ = build_bracket()
    out.append(("kit_bracket", a, (0, 0, 0, 0), dict(bracket=True)))
    a, _ = build_spark()
    out.append(("kit_spark", a, (0, 0, 0, 0), dict(spark=True)))
    out.append(("kit_spark_v", a.T.copy(), (0, 0, 0, 0), dict(spark_v=True)))
    return out


# --------------------------------------------------------------------------------------------
# Invariants - the shape's claims, measured off the rendered alpha
# --------------------------------------------------------------------------------------------

def edge_crossing(row):
    """Index (in texels, fractional) where alpha first reaches 0.5 scanning from the left."""
    for i, v in enumerate(row):
        if v >= 0.5:
            prev = row[i - 1] if i > 0 else 0.0
            return i - (v - 0.5) / max(v - prev, 1e-9) + 0.5
    return float("nan")


def check_sprite(name, alpha, border, meta):
    h, w = alpha.shape
    if "cut" in meta:
        cut, kind, flip = meta["cut"], meta["kind"], meta["flip"]
        b = border[0]
        # the cut corners are empty, the square corners are solid
        empty = [(0, 0), (h - 1, w - 1)] if not flip else [(0, w - 1), (h - 1, 0)]
        solid = [(0, w - 1), (h - 1, 0)] if not flip else [(0, 0), (h - 1, w - 1)]
        for r, c in empty:
            assert alpha[r, c] == 0, f"{name}: cut corner ({r},{c}) must be empty"
        for r, c in solid:
            assert alpha[r, c] > 0.99, f"{name}: square corner ({r},{c}) must be solid"
        # the cut measures `cut` canvas units along the edge it starts on (top row, from the
        # cut side) - +-0.6 texel for the 45 deg antialiasing ramp
        top = alpha[0] if not flip else alpha[0][::-1]
        measured = edge_crossing(top) / TEXELS_PER_UNIT
        assert abs(measured - cut) <= 0.6 / TEXELS_PER_UNIT + 0.26, \
            f"{name}: cut measures {measured:.2f} units, expected {cut}"
        # the cut lives inside its own 9-slice corner tile
        assert cut * TEXELS_PER_UNIT + 2 <= b, f"{name}: the cut does not fit the {b}-texel border"
        # the stretch band is uniform in both directions, so slicing preserves the shape
        mid_rows = alpha[b:h - b, :]
        assert np.allclose(mid_rows, mid_rows[0:1, :]), f"{name}: vertical stretch band varies"
        mid_cols = alpha[:, b:w - b]
        assert np.allclose(mid_cols, mid_cols[:, 0:1]), f"{name}: horizontal stretch band varies"
        if kind != "fill":
            width = LINES[0] if kind == "line1" else LINES[1]
            row = alpha[h // 2]
            band = (row[: w // 2] >= 0.5).sum() / TEXELS_PER_UNIT
            assert abs(band - width) <= 0.05, f"{name}: band is {band:.3f} units, expected {width}"
            assert row[w // 2] == 0, f"{name}: an outline must be hollow"
            # the inner glow: present right inside the band, softer than it, faded out by GLOW_U
            just_in = row[width * TEXELS_PER_UNIT]
            assert 0.15 <= just_in < 0.5, f"{name}: no inner glow under the band ({just_in:.2f})"
            past = int((width + GLOW_U) * TEXELS_PER_UNIT) + 1
            assert past < w // 2 and np.all(row[past: w // 2] == 0), f"{name}: the glow runs past {GLOW_U} units"
        else:
            assert alpha[h // 2, w // 2] == 1.0, f"{name}: a fill must be solid inside"
    elif "hex" in meta:
        assert alpha[0, 0] == 0 and alpha[-1, -1] == 0 and alpha[0, -1] == 0, \
            f"{name}: a hexagon's bounding corners must be empty"
        assert alpha[h // 2, 0] > 0.4 and alpha[h // 2, -1] > 0.4, \
            f"{name}: a flat-top hexagon touches both side edges at mid-height"
        if meta["hex"] in ("line1", "line2"):
            assert alpha[h // 2, w // 2] == 0, f"{name}: an outline must be hollow"
    elif meta.get("fade"):
        assert np.allclose(alpha, alpha[:, 0:1]), f"{name}: every column of the ramp must match"
        col = alpha[:, 0]
        assert np.all(np.diff(col) > 0), f"{name}: the ramp must rise from top to bottom"
        assert col[0] < 0.05 and col[-1] > 0.95, f"{name}: the ramp must run clear to solid"
    elif meta.get("fade_h"):
        assert np.allclose(alpha, alpha[0:1, :]), f"{name}: every row of the ramp must match"
        row = alpha[0]
        assert np.all(np.diff(row) < 0), f"{name}: the ramp must fall from left to right"
        assert row[0] > 0.95 and row[-1] < 0.05, f"{name}: the ramp must run solid to clear"
    elif meta.get("oct"):
        for r, c in ((0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)):
            assert alpha[r, c] == 0, f"{name}: an octagon's bounding corners must be empty"
        for r, c in ((0, w // 2), (h - 1, w // 2), (h // 2, 0), (h // 2, w - 1)):
            assert alpha[r, c] > 0.4, f"{name}: an octagon touches all four sides at their middle"
        assert alpha[h // 2, w // 2] == 1.0, f"{name}: a fill must be solid inside"
        # the corner cut runs OCT_CUT of the side along the top edge
        measured = edge_crossing(alpha[0]) / w
        assert abs(measured - OCT_CUT) <= 0.02, f"{name}: the corner cut is {measured:.3f} of the side"
    elif meta.get("oct_line"):
        for r, c in ((0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)):
            assert alpha[r, c] == 0, f"{name}: an octagon's bounding corners must be empty"
        assert alpha[h // 2, w // 2] == 0, f"{name}: an outline must be hollow"
        band = (alpha[h // 2, : w // 2] >= 0.5).sum() / TEXELS_PER_UNIT
        assert abs(band - 2.0) <= 0.05, f"{name}: band is {band:.3f} units, expected 2"
    elif meta.get("pool"):
        assert alpha.shape == (POOL_H * TEXELS_PER_UNIT, POOL_W * TEXELS_PER_UNIT), f"{name}: wrong size"
        assert alpha[h // 2, w // 2] > 0.9, f"{name}: the pool must be bright at its centre"
        for r, c in ((0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1), (h // 2, 0), (0, w // 2)):
            assert alpha[r, c] < 0.02, f"{name}: the pool must fade to clear at its edge ({r},{c})"
        row = alpha[h // 2, w // 2:]
        assert np.all(np.diff(row) <= 1e-9), f"{name}: the pool must fall off from its centre"
    elif meta.get("pool_rim"):
        assert alpha[h // 2, w // 2] == 0, f"{name}: a rim must be hollow"
        band = (alpha[h // 2, : w // 2] >= 0.5).sum() / TEXELS_PER_UNIT
        assert abs(band - POOL_RIM) <= 0.3, f"{name}: rim is {band:.3f} units, expected {POOL_RIM}"
    elif meta.get("grid") or meta.get("bracket"):
        t = TEXELS_PER_UNIT
        assert alpha.shape == ((GRID if meta.get("grid") else BRACKET) * t,) * 2, f"{name}: wrong tile size"
        assert np.all(alpha[:t, :] == 1.0) and np.all(alpha[:, :t] == 1.0), f"{name}: the top and left lines must be solid"
        assert np.all(alpha[t:, t:] == 0.0), f"{name}: everything off the two lines must be clear"
    elif meta.get("spark"):
        assert np.allclose(alpha, alpha[0:1, :]), f"{name}: every row of the streak must match"
        row = alpha[0]
        assert np.allclose(row, row[::-1], atol=1e-6), f"{name}: the streak must be symmetric"
        assert row[0] < 0.05 and row[-1] < 0.05, f"{name}: the streak must be clear at both ends"
        assert row[len(row) // 2] > 0.95, f"{name}: the streak must be solid in the middle"
    elif meta.get("spark_v"):
        check_sprite(name, alpha.T, border, dict(spark=True))
    elif meta.get("banner") == "body":
        b = border[0]
        mid = alpha[:, b:w - b]
        assert np.allclose(mid, mid[:, 0:1]), f"{name}: the banner stretch band varies"
        assert alpha[0, 0] == 0 and alpha[-1, -1] == 0, f"{name}: the slant corners must be empty"


# --------------------------------------------------------------------------------------------
# Encoding
# --------------------------------------------------------------------------------------------

def encode_png(alpha):
    h, w = alpha.shape
    rgba = np.full((h, w, 4), 255, dtype=np.uint8)
    rgba[..., 3] = np.clip(np.rint(alpha * 255.0), 0, 255).astype(np.uint8)
    raw = b"".join(b"\x00" + rgba[y].tobytes() for y in range(h))

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


# Cloned from author_ui_frame_sprites.py's META (itself Unity's own TextureImporter output).
# The trailing spaces on the empty-value lines are DELIBERATE: Unity writes `key: ` for an empty
# scalar, so a file emitted without them is rewritten on first import and `--check` then reports
# drift nobody caused.
META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {max_size}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: {bl}, y: {bb}, z: {br}, w: {bt}}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def unity_empty_scalars(text):
    """Re-add the trailing space Unity writes after an EMPTY scalar (`key: `). A bare `key:` is a
    PARENT when the next line is indented deeper, or is a `- ` item at the same indent (Unity's
    block-sequence spelling); otherwise it is an empty scalar. Without this a file is rewritten by
    Unity on first import and `--check` reports drift nobody caused."""
    lines = text.split("\n")
    out = []
    for i, line in enumerate(lines):
        if re.match(r"^\s*[^\s#][^:]*:$", line) and not line.lstrip().startswith("- "):
            indent = len(line) - len(line.lstrip())
            nxt = lines[i + 1] if i + 1 < len(lines) else ""
            n_indent = len(nxt) - len(nxt.lstrip())
            parent = nxt.strip() != "" and (n_indent > indent or
                                            (n_indent == indent and nxt.lstrip().startswith("- ")))
            if not parent:
                line += " "
        out.append(line)
    return "\n".join(out)


def render_files():
    files = {}
    for name, alpha, border, meta in sprite_set():
        check_sprite(name, alpha, border, meta)
        bl, bb, br, bt = border
        max_size = 128 if max(alpha.shape) <= 128 else 256
        files[name + ".png"] = encode_png(alpha)
        files[name + ".png.meta"] = unity_empty_scalars(META.format(
            guid=guid_for(name), sprite_id=guid_for(name + "/sprite"), ppu=PPU,
            bl=bl, bb=bb, br=br, bt=bt, max_size=max_size)).encode()
    return files


def assert_guids_unique(files):
    """Exactly one .meta may OWN each guid this script mints."""
    mine = {guid_for(n[:-len(".png.meta")]) for n in files if n.endswith(".png.meta")}
    mine.add(guid_for("__folder__"))
    own_dir = os.path.abspath(OUT_DIR)
    for root, _dirs, names in os.walk(os.path.join(REPO, "Assets")):
        if os.path.abspath(root).startswith(own_dir):
            continue
        for n in names:
            if not n.endswith(".meta") or os.path.join(root, n) == OUT_DIR + ".meta":
                continue
            with open(os.path.join(root, n), "r", errors="ignore") as fh:
                head = fh.read(200)
            for g in mine:
                if f"guid: {g}" in head:
                    sys.exit(f"guid {g} is already owned by {os.path.join(root, n)}")


def self_test():
    """Every shape assert must fire on the defect it exists for. A gate nobody has watched FAIL
    is a gate nobody should trust."""
    global SUPERSAMPLE
    controls = []

    def expect_fail(label, fn):
        try:
            fn()
        except AssertionError as e:
            controls.append((label, True, str(e)))
            return
        controls.append((label, False, "no assertion fired"))

    # 1. a sliced sliver whose cut overruns its corner tile
    a, b = build_sliver(14, "fill", False)
    expect_fail("cut larger than its 9-slice tile",
                lambda: check_sprite("ctl", a, (14 * TEXELS_PER_UNIT,) * 4,
                                     dict(cut=14, kind="fill", flip=False)))
    # 2. a cut 3 units short of spec
    a, b = build_sliver(11, "fill", False)
    expect_fail("cut measures 11 when 14 is claimed",
                lambda: check_sprite("ctl", a, (b,) * 4, dict(cut=14, kind="fill", flip=False)))
    # 3. an outline whose band is the wrong width
    a, b = build_sliver(10, "line2", False)
    expect_fail("2-unit band checked as a 1-unit line",
                lambda: check_sprite("ctl", a, (b,) * 4, dict(cut=10, kind="line1", flip=False)))
    # 4. a flipped sprite checked as normal (wrong corners cut)
    a, b = build_sliver(10, "fill", True)
    expect_fail("flipped sprite checked as normal",
                lambda: check_sprite("ctl", a, (b,) * 4, dict(cut=10, kind="fill", flip=False)))
    # 5. a fill checked as an outline (not hollow)
    a, b = build_sliver(14, "fill", False)
    expect_fail("solid fill checked as an outline",
                lambda: check_sprite("ctl", a, (b,) * 4, dict(cut=14, kind="line1", flip=False)))
    # 5b. an outline with no inner glow (the band alone)
    a, b = build_sliver(14, "line1", False)
    bare = np.where(a >= 0.5, a, 0.0)
    expect_fail("outline without its inner glow",
                lambda: check_sprite("ctl", bare, (b,) * 4, dict(cut=14, kind="line1", flip=False)))
    # 6. a fade ramp drawn upside down (solid at the top)
    a, _ = build_fade()
    expect_fail("fade ramp upside down",
                lambda: check_sprite("ctl", a[::-1], (0, 0, 0, 0), dict(fade=True)))
    # 7. a horizontal fade drawn right to left
    a, _ = build_fade_h()
    expect_fail("horizontal fade mirrored",
                lambda: check_sprite("ctl", a[:, ::-1], (0, 0, 0, 0), dict(fade_h=True)))
    # 8. a square checked as the octagon (corners not cut)
    t = OCT * TEXELS_PER_UNIT
    expect_fail("square checked as the octagon",
                lambda: check_sprite("ctl", np.ones((t, t)), (0, 0, 0, 0), dict(oct=True)))

    # 9. a grid tile with ink inside the cell
    a, _ = build_grid()
    a2 = a.copy(); a2[20, 20] = 1.0
    expect_fail("grid tile with ink inside the cell", lambda: check_sprite("ctl", a2, (0, 0, 0, 0), dict(grid=True)))
    # 10. a spark that is solid at one end (a ramp, not a streak)
    a, _ = build_fade_h()
    expect_fail("ramp checked as a spark", lambda: check_sprite("ctl", a, (0, 0, 0, 0), dict(spark=True)))
    # 11. a bracket missing its left arm
    a, _ = build_bracket()
    a3 = a.copy(); a3[:, :TEXELS_PER_UNIT] = 0.0
    expect_fail("bracket missing its left arm", lambda: check_sprite("ctl", a3, (0, 0, 0, 0), dict(bracket=True)))

    # 12. a solid octagon checked as the outline
    a, _ = build_oct()
    expect_fail("octagon fill checked as its outline", lambda: check_sprite("ctl", a, (0, 0, 0, 0), dict(oct_line=True)))
    # 13. a pool drawn as a hard disc (no falloff to its edge)
    w, h = POOL_W * TEXELS_PER_UNIT, POOL_H * TEXELS_PER_UNIT
    xs, ys = grid(w, h)
    disc = downsample(ellipse_r(xs, ys, POOL_W, POOL_H) <= 1.0, w, h)
    expect_fail("a hard disc checked as the pool", lambda: check_sprite("ctl", disc, (0, 0, 0, 0), dict(pool=True)))

    ok = True
    for label, fired, msg in controls:
        ok &= fired
        print(f"  {'FIRES' if fired else 'SILENT'}  {label}: {msg}")
    print("OK: every negative control fires" if ok else "FAIL: a shape defect slipped through")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    files = render_files()
    files_meta = unity_empty_scalars(FOLDER_META.format(guid=guid_for("__folder__"))).encode()
    folder_meta_path = OUT_DIR + ".meta"

    if args.check:
        drift = []
        for name, want in files.items():
            p = os.path.join(OUT_DIR, name)
            have = open(p, "rb").read() if os.path.exists(p) else b""
            if have != want:
                drift.append(os.path.relpath(p, REPO))
        have = open(folder_meta_path, "rb").read() if os.path.exists(folder_meta_path) else b""
        if have != files_meta:
            drift.append(os.path.relpath(folder_meta_path, REPO))
        extra = sorted(set(os.listdir(OUT_DIR)) - set(files)) if os.path.isdir(OUT_DIR) else []
        if drift or extra:
            for d in drift:
                print("DRIFT:", d)
            for e in extra:
                print("UNKNOWN FILE in the kit folder:", e)
            return 1
        print(f"OK: {len(files) // 2} kit sprites match the generator")
        return 0

    assert_guids_unique(files)
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(folder_meta_path, "wb") as fh:
        fh.write(files_meta)
    for name, data in files.items():
        with open(os.path.join(OUT_DIR, name), "wb") as fh:
            fh.write(data)
    print(f"wrote {len(files) // 2} sprites to {os.path.relpath(OUT_DIR, REPO)}")
    for name, alpha, border, _ in sprite_set():
        h, w = alpha.shape
        print(f"  {name:28s} {w:4d}x{h:<4d} texels  ({w / TEXELS_PER_UNIT:g}x{h / TEXELS_PER_UNIT:g} "
              f"units)  border {border}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
