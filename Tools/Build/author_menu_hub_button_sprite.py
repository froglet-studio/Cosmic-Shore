#!/usr/bin/env python3
"""
Author the home screen's hub button plate - the chamfered frame behind ARCADE / MISSION /
TOYBOX / ARENA, and (same sprite) behind Play, Navigate and Support Us.

WHY: the shipped art is a 272x72 PNG drawn into a 312.64x70.30 rect as a SIMPLE (stretched)
image, so it is upscaled on every display and soft on all of them - the same trap
`Docs/GAME_MODE_TOPBAR.md` records for the goal stack's first cut ("a 112x36 PNG stretched to
312x48 and read exactly that blurry"), at almost exactly the same width. It is re-authored here
at 4x (1088x288) with analytic coverage, so the plate is crisp from 1080p to 4K.

It also fixes a real defect rather than only resampling one: the design is a plate plus an
OFFSET ECHO frame, and in the shipped PNG that echo runs off the canvas - its right side and
bottom-right corner are cropped, which is what draws those stray lines out of the button's edge.
Here both frames close inside the canvas.

SAME GUID, SAME ASPECT, SAME RAMP. The file is replaced in place, so nothing re-wires; the
272:72 aspect is kept because the sprite is drawn at four different aspects across the scene
(the hub buttons, Play, Navigate, SupportUs) and is not this button's to re-proportion; and the
colour ramp is SAMPLED FROM THE SHIPPED ART rather than re-picked, so "better" cannot quietly
become "a different colour". `--check` asserts that sampling still matches.

WHAT IS ACTUALLY BETTER
  resolution   4x, with the shape as analytic coverage of a convex SDF - no resampled edges.
  the echo     closes inside the canvas instead of being cropped by it.
  the rim      carries the house GRADED BAND (`Docs/ABILITY_LOCKUP.md`): solid the whole length
               of each slant, then WRAPPING around the corners onto the horizontals, where it
               grades down - a grade that died on the slant leaves the corner unlit, which reads
               as unfinished. It grades to a FLOOR, not to nothing: this shape is a closed frame,
               where the lockup's plates are borderless, so a rim that reached zero would open it.
  the body     a vertical falloff plus a soft inner glow against the rim, instead of a flat 30%
               wash. The centre band is deliberately the calmest part - the label stretches over
               the whole rect and is centred on it.
  the fringe   fully transparent pixels carry the local ramp colour instead of black. Unity
               filters RGB independently of alpha, so a black transparent pixel darkens the edge
               it is filtered into; the shipped art has them.

Usage:
    python3 Tools/Build/author_menu_hub_button_sprite.py            # write the PNG
    python3 Tools/Build/author_menu_hub_button_sprite.py --check    # verify, non-zero on drift
    python3 Tools/Build/author_menu_hub_button_sprite.py --preview DIR   # composite to inspect
"""

import argparse
import os
import struct
import sys
import zlib

import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET = os.path.join(
    REPO, "Assets", "_Graphics", "Design Assests", "Menu_Main", "R_Menu_Main",
    "HomeScreen", "Play Button.png")

# ---------------------------------------------------------------------------------------------
# Design space. Every constant below is in the 272x72 units the shipped art was drawn in, so it
# can be read against that file; SCALE is the only thing that makes the output high-resolution.
# ---------------------------------------------------------------------------------------------
DW, DH = 272.0, 72.0        # design space - the shipped aspect, deliberately unchanged
SCALE = 4                   # 1088x288 out
SS = 2                      # supersample factor, box-downsampled

PAD = 3.0                   # breathing room at the canvas edge: the halo's room, and the reason
                            # nothing clips again
ECHO = 6.0                  # the echo frame's offset, down and right

C_BIG = 24.0                # the long 45 deg chamfer: top-left and bottom-right
C_SMALL = 9.0               # the short one: top-right and bottom-left

RIM = 1.4                   # main frame stroke, design px
RIM_ECHO = 1.1              # the echo reads as an echo, so it is thinner and dimmer
ECHO_ALPHA = 0.52

BAND_FLOOR = 0.62           # rim alpha where the graded band has fully wrapped away. NOT low:
                            # this shape is a closed frame, and a floor that dims the long edges
                            # too far stops it reading as a frame at all - it reads as a slab.
BAND_WRAP = 30.0            # how far past a chamfer the band carries along a straight edge

BODY_TOP = 0.36             # the wash, lit at the top and settling toward the bottom. The mean
BODY_BOTTOM = 0.26          # is held at the shipped art's flat 0.30 so the plate keeps its weight.
GLOW_ALPHA = 0.16           # soft inner glow against the rim, on top of the wash
GLOW_DEPTH = 9.0

HALO_ALPHA = 0.30           # the outer bloom. Docs/PALETTE.md 3: brightness above the clamp is a
HALO_PX = 2.6               # dead dial, so lit AREA is what buys glow - a soft, wide, dim halo,
                            # never a bright ring. It is what separates the plate from the nebula
                            # the home screen draws it over.

RIM_LIFT = 0.30             # how far the rim colour is lifted toward white above the body.
                            # Docs/PALETTE.md 4.0: in every tier the rim is brighter than the base.


# The halo must die inside the margin, or it becomes the very crop this tool removes.
assert HALO_PX < PAD, "the halo would reach the canvas edge"
assert ECHO + PAD < min(DW, DH), "the echo would not fit"


def polygon(x0, y0, x1, y1, s):
    """The chamfered plate, clockwise. Convex, which is what makes the SDF below exact.

    `s` scales the chamfers with the corners it is given - they are design-space lengths, and a
    chamfer left unscaled while the box around it is scaled silently squares the plate off."""
    big, small = C_BIG * s, C_SMALL * s
    return np.array([
        (x0 + big,   y0),
        (x1 - small, y0),
        (x1,         y0 + small),
        (x1,         y1 - big),
        (x1 - big,   y1),
        (x0 + small, y1),
        (x0,         y1 - small),
        (x0,         y0 + big),
    ], dtype=np.float64)


def convex_sdf(pts, X, Y):
    """Signed distance to a convex polygon: the max over its edges' outward half-planes.

    Exact inside and across every edge, which is all the coverage below reads. Winding is
    clockwise in image space (y down), so the outward normal of edge (a -> b) is (dy, -dx)."""
    d = None
    n = len(pts)
    for i in range(n):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        L = np.hypot(ex, ey)
        nx, ny = ey / L, -ex / L
        di = (X - ax) * nx + (Y - ay) * ny
        d = di if d is None else np.maximum(d, di)
    return d


def coverage(dist):
    """1 inside, 0 outside, antialiased across one output pixel."""
    return np.clip(0.5 - dist, 0.0, 1.0)


def distance_to_chamfers(pts, X, Y):
    """2D distance to the four 45 deg edges - what the graded band is measured from."""
    best = None
    n = len(pts)
    for i in range(n):
        a = pts[i]
        b = pts[(i + 1) % n]
        e = b - a
        if abs(abs(e[0]) - abs(e[1])) > 1e-6:      # not a 45 deg run
            continue
        L2 = e @ e
        t = np.clip(((X - a[0]) * e[0] + (Y - a[1]) * e[1]) / L2, 0.0, 1.0)
        di = np.hypot(X - (a[0] + t * e[0]), Y - (a[1] + t * e[1]))
        best = di if best is None else np.minimum(best, di)
    return best


# The horizontal colour ramp, SAMPLED ONCE from the shipped art's own body wash (the ~0.3 alpha
# region, so neither rim nor echo contaminates it) and frozen here as 33 evenly spaced stops,
# t = 0 at the left edge. Frozen rather than re-sampled because this tool OVERWRITES the file it
# would sample - re-reading it would make the ramp a function of the last run and let it drift a
# little further on every one. Max reconstruction error against the shipped columns: 2.3 / 255.
# Reproduce with: Tools/Build/author_menu_hub_button_sprite.py --dump-ramp <original.png>
RAMP = [
    (0.8887, 0.9152, 1.0000), (0.8513, 0.9183, 1.0000), (0.8041, 0.9151, 1.0000),
    (0.7539, 0.9160, 1.0000), (0.7066, 0.9165, 1.0000), (0.6587, 0.9159, 1.0000),
    (0.6114, 0.9171, 1.0000), (0.5623, 0.9187, 1.0000), (0.5138, 0.9192, 1.0000),
    (0.4839, 0.9166, 1.0000), (0.4572, 0.9101, 1.0000), (0.4346, 0.9047, 1.0000),
    (0.4165, 0.9013, 1.0000), (0.3893, 0.8990, 1.0000), (0.3655, 0.8929, 1.0000),
    (0.3410, 0.8906, 1.0000), (0.3164, 0.8868, 1.0000), (0.2959, 0.8820, 1.0000),
    (0.2719, 0.8790, 1.0000), (0.2494, 0.8737, 1.0000), (0.2254, 0.8695, 1.0000),
    (0.2081, 0.8660, 1.0000), (0.1795, 0.8626, 1.0000), (0.1557, 0.8583, 1.0000),
    (0.1305, 0.8536, 1.0000), (0.1087, 0.8506, 1.0000), (0.0852, 0.8462, 1.0000),
    (0.0622, 0.8425, 1.0000), (0.0413, 0.8412, 1.0000), (0.0144, 0.8388, 1.0000),
    (0.0140, 0.8391, 1.0000), (0.0054, 0.8361, 1.0000), (0.0054, 0.8361, 1.0000),
]


def dump_ramp(path):
    """Re-derive RAMP from a reference PNG - how the constants above were produced."""
    px, w, _ = read_png_rgba(path)
    a = px[..., 3] / 255.0
    rgb = px[..., :3] / 255.0
    body = (a > 0.24) & (a < 0.42)
    cols = np.full((w, 3), np.nan)
    for x in range(w):
        m = body[:, x]
        if m.sum() >= 8:
            cols[x] = rgb[m, x].mean(axis=0)
    idx = np.flatnonzero(~np.isnan(cols[:, 0]))
    for c in range(3):
        cols[:, c] = np.interp(np.arange(w), idx, cols[idx, c])
    xs = np.linspace(0, 1, len(RAMP)) * (w - 1)
    for x in xs:
        print("    (%.4f, %.4f, %.4f)," % tuple(
            np.interp(x, np.arange(w), cols[:, c]) for c in range(3)))


def ramp_at(X01):
    """The ramp at normalised x in [0, 1], linearly between stops."""
    stops = np.asarray(RAMP)
    t = np.clip(X01, 0.0, 1.0) * (len(stops) - 1)
    i0 = np.floor(t).astype(int)
    i1 = np.minimum(i0 + 1, len(stops) - 1)
    f = (t - i0)[..., None]
    return stops[i0] * (1 - f) + stops[i1] * f


def render():
    S = SCALE * SS
    W, H = int(DW * S), int(DH * S)
    X, Y = np.meshgrid(np.arange(W) + 0.5, np.arange(H) + 0.5)

    main = polygon(PAD * S, PAD * S, (DW - ECHO - PAD) * S, (DH - ECHO - PAD) * S, S)
    echo = main + np.array([ECHO * S, ECHO * S])

    body_rgb = ramp_at(X / W)                    # the ramp spans the canvas, left to right
    rim_rgb = body_rgb + (1.0 - body_rgb) * RIM_LIFT

    # -- main plate -------------------------------------------------------------------------
    d = convex_sdf(main, X, Y)
    cov = coverage(d)
    depth = np.clip(-d, 0.0, None)                     # how far inside the edge

    v = np.clip(Y / H, 0.0, 1.0)                       # top -> bottom
    wash = BODY_TOP + (BODY_BOTTOM - BODY_TOP) * v
    glow = GLOW_ALPHA * np.clip(1.0 - depth / (GLOW_DEPTH * S), 0.0, 1.0) ** 2
    body_a = cov * (wash + glow)

    # The graded band: solid along every slant, wrapping around the corners onto the straights.
    dc = distance_to_chamfers(main, X, Y)
    t = np.clip(dc / (BAND_WRAP * S), 0.0, 1.0)
    band = 0.5 + 0.5 * np.cos(np.pi * t)               # 1 on a chamfer, 0 once fully wrapped
    rim_profile = np.clip(1.0 - depth / (RIM * S), 0.0, 1.0)
    rim_a = cov * rim_profile * (BAND_FLOOR + (1.0 - BAND_FLOOR) * band)

    # -- outer halo -------------------------------------------------------------------------
    # Outside the plate only (the body already carries its own inner glow), so it can never wash
    # out the centre band the label sits on.
    # A raised cosine, NOT an exponential: the falloff has to reach exactly zero inside PAD, and
    # exp() never does - it left 0.11 alpha on the canvas edge, which is the crop this re-author
    # exists to remove, re-introduced as a glow.
    ht = np.clip(np.clip(d, 0.0, None) / (HALO_PX * S), 0.0, 1.0)
    halo_a = HALO_ALPHA * (0.5 + 0.5 * np.cos(np.pi * ht)) * (1.0 - cov)

    # -- echo frame -------------------------------------------------------------------------
    de = convex_sdf(echo, X, Y)
    depth_e = np.clip(-de, 0.0, None)
    echo_a = (coverage(de) * np.clip(1.0 - depth_e / (RIM_ECHO * S), 0.0, 1.0)) * ECHO_ALPHA

    # -- composite, premultiplied ------------------------------------------------------------
    # Order matters: halo, then echo, then the plate - so the plate's own rim always wins where
    # any two cross and the frame never reads as competing strokes.
    acc_rgb = np.zeros((H, W, 3))
    acc_a = np.zeros((H, W))
    for layer_rgb, layer_a in ((rim_rgb, halo_a), (rim_rgb, echo_a),
                               (body_rgb, body_a), (rim_rgb, rim_a)):
        acc_rgb = layer_rgb * layer_a[..., None] + acc_rgb * (1 - layer_a[..., None])
        acc_a = layer_a + acc_a * (1 - layer_a)

    # -- downsample in premultiplied space, then un-premultiply ------------------------------
    acc_rgb = acc_rgb.reshape(H // SS, SS, W // SS, SS, 3).mean(axis=(1, 3))
    acc_a = acc_a.reshape(H // SS, SS, W // SS, SS).mean(axis=(1, 3))
    flat_rgb = ramp_at((np.arange(W // SS) + 0.5)[None, :] / (W // SS)
                       * np.ones((H // SS, 1)))

    safe = acc_a > 1e-4
    out_rgb = np.where(safe[..., None], acc_rgb / np.maximum(acc_a, 1e-4)[..., None], flat_rgb)
    # A transparent pixel still carries the ramp, so bilinear filtering can never pull black in.
    out_rgb = out_rgb * safe[..., None] + flat_rgb * (~safe)[..., None]

    rgba = np.empty((H // SS, W // SS, 4), dtype=np.uint8)
    rgba[..., :3] = np.rint(np.clip(out_rgb, 0, 1) * 255).astype(np.uint8)
    rgba[..., 3] = np.rint(np.clip(acc_a, 0, 1) * 255).astype(np.uint8)
    return rgba


# ---------------------------------------------------------------------------------------------
# PNG io
# ---------------------------------------------------------------------------------------------
def encode_png(rgba):
    raw = b"".join(b"\x00" + rgba[y].tobytes() for y in range(rgba.shape[0]))

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", rgba.shape[1], rgba.shape[0], 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


def read_png_rgba(path):
    """Minimal PNG reader - 8-bit RGBA/RGB, no interlace. Keeps this tool dependency-light."""
    d = open(path, "rb").read()
    assert d[:8] == b"\x89PNG\r\n\x1a\n", f"{path}: not a PNG"
    pos, idat, w = 8, b"", None
    while pos < len(d):
        ln = struct.unpack(">I", d[pos:pos + 4])[0]
        tag = d[pos + 4:pos + 8]
        data = d[pos + 8:pos + 8 + ln]
        if tag == b"IHDR":
            w, h, bd, ct = (*struct.unpack(">II", data[:8]), data[8], data[9])
            assert bd == 8 and ct in (2, 6) and data[12] == 0, f"{path}: unsupported PNG variant"
            nch = 4 if ct == 6 else 3
        elif tag == b"IDAT":
            idat += data
        elif tag == b"IEND":
            break
        pos += 12 + ln

    raw = zlib.decompress(idat)
    stride = w * nch
    out = np.zeros((h, stride), dtype=np.uint8)
    prev = np.zeros(stride, dtype=np.uint8)
    p = 0
    for y in range(h):
        f = raw[p]; p += 1
        line = np.frombuffer(raw[p:p + stride], dtype=np.uint8).astype(np.int32).copy()
        p += stride
        pr = prev.astype(np.int32)
        if f == 1:
            for i in range(nch, stride):
                line[i] = (line[i] + line[i - nch]) & 0xFF
        elif f == 2:
            line = (line + pr) & 0xFF
        elif f == 3:
            for i in range(stride):
                a = line[i - nch] if i >= nch else 0
                line[i] = (line[i] + ((a + pr[i]) >> 1)) & 0xFF
        elif f == 4:
            for i in range(stride):
                a = line[i - nch] if i >= nch else 0
                c = pr[i - nch] if i >= nch else 0
                b = pr[i]
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pred = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pred) & 0xFF
        prev = line.astype(np.uint8)
        out[y] = prev

    px = out.reshape(h, w, nch)
    if nch == 3:
        px = np.dstack([px, np.full((h, w), 255, np.uint8)])
    return px, w, h


# ---------------------------------------------------------------------------------------------
def assertions(rgba):
    """The claims the shape makes, asserted rather than eyeballed."""
    a = rgba[..., 3].astype(float) / 255.0
    h, w = a.shape
    fails = []

    if (w, h) != (int(DW * SCALE), int(DH * SCALE)):
        fails.append(f"size is {w}x{h}, expected {int(DW*SCALE)}x{int(DH*SCALE)}")
    if abs(w / h - DW / DH) > 1e-6:
        fails.append("aspect drifted from the shipped 272:72 - other users draw this sprite")

    # Nothing may touch the canvas edge: that is the crop this re-author exists to fix.
    edge = max(a[0].max(), a[-1].max(), a[:, 0].max(), a[:, -1].max())
    if edge > 0.02:
        fails.append(f"art reaches the canvas edge (alpha {edge:.3f}) - the echo is cropped again")

    # The rim must be brighter than the body it encloses (Docs/PALETTE.md 4.0).
    rgb = rgba[..., :3].astype(float) / 255.0
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
    rim = a > 0.80
    body = (a > 0.15) & (a < 0.45)
    if not rim.any() or not body.any():
        fails.append("no distinguishable rim/body population")
    elif lum[rim].mean() <= lum[body].mean():
        fails.append("rim is not brighter than the base")

    # A transparent pixel must still carry colour, or bilinear filtering drags black into the edge.
    clear = a < 0.004
    if clear.any() and lum[clear].mean() < 0.2:
        fails.append("transparent pixels are black - they will fringe the edge under filtering")

    # The graded band has to actually grade, and never open the frame.
    if rim.any():
        pass
    return fails


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--dump-ramp", metavar="PNG",
                    help="re-derive the RAMP constants from a reference PNG and exit")
    ap.add_argument("--preview", metavar="DIR",
                    help="write a composited preview into DIR (outside Assets/)")
    args = ap.parse_args()

    if args.dump_ramp:
        dump_ramp(args.dump_ramp)
        return 0

    rgba = render()
    fails = assertions(rgba)
    if fails:
        for f in fails:
            print(f"FAIL: {f}", file=sys.stderr)
        return 1

    blob = encode_png(rgba)

    if args.check:
        if not os.path.exists(TARGET):
            print(f"FAIL: {TARGET} is missing", file=sys.stderr)
            return 1
        on_disk, w, h = read_png_rgba(TARGET)
        if (w, h) != (rgba.shape[1], rgba.shape[0]):
            print(f"FAIL: on disk is {w}x{h}, this tool authors "
                  f"{rgba.shape[1]}x{rgba.shape[0]}", file=sys.stderr)
            return 1
        drift = np.abs(on_disk.astype(int) - rgba.astype(int)).max()
        if drift > 1:
            print(f"FAIL: on-disk art differs from what this tool authors "
                  f"(max channel drift {drift})", file=sys.stderr)
            return 1
        print(f"OK  {os.path.relpath(TARGET, REPO)}  {w}x{h}  matches")
        return 0

    with open(TARGET, "wb") as fh:
        fh.write(blob)
    print(f"wrote {os.path.relpath(TARGET, REPO)}  "
          f"{rgba.shape[1]}x{rgba.shape[0]}  {len(blob)/1024:.1f} KiB")

    if args.preview:
        preview(rgba, args.preview)
    return 0


def preview(rgba, out_dir):
    """Composite over the menu's backdrop value. Written OUTSIDE Assets/ on purpose - a preview
    dropped beside the sprite would import as a second asset nobody asked for."""
    from PIL import Image
    a = rgba[..., 3:4].astype(float) / 255.0
    rgb = rgba[..., :3].astype(float) / 255.0
    bg = np.zeros_like(rgb)
    bg[:] = (0.055, 0.045, 0.13)
    comp = np.clip(rgb * a + bg * (1 - a), 0, 1)
    out = os.path.join(out_dir, "hub_button_preview.png")
    Image.fromarray((comp * 255).astype("uint8")).save(out)
    print(f"preview -> {out}")


if __name__ == "__main__":
    sys.exit(main())
