#!/usr/bin/env python3
"""
Re-author the two CARD PLATE sprites every menu card is drawn with - in place, same guids.

    ArcadeScreen/Group 1585.png          the PLATE  (the card's body: a chamfered navy slab)
    ArcadeScreen/Rectangle 1127 (2).png  the RIM    (the same outline as a 1px frame + a wash)

WHY: both shipped as 228x170 PNGs drawn SIMPLE (stretched) into a 275x203 card, and into a
275x100 variant row on the Toy Box's detail window - so the two 45 deg chamfers were upscaled on
every display (the pixelated "bent corners") and, on the short row, squashed to 2.7:1. It is the
same trap Docs/GAME_MODE_TOPBAR.md records for the goal stack and Docs/HomeHub/ARCHITECTURE.md
section 7 for the hub button: a low-resolution PNG stretched into a bigger rect, read exactly
that blurry.

The fix is the hub button's, plus one thing a CARD needs that a button did not: the file is
replaced in place at 4x (912x680) so every consumer stays wired, AND the .meta gains a 9-SLICE
border (32 design px, wider than the 24 px chamfer) with the pixels-per-unit raised to 400, so a
consumer that draws it SLICED gets an exact chamfer at ANY rect - the 275x203 toy card and the
275x100 variant row alike - while a consumer still drawing it SIMPLE (the arcade grid, whose
per-game art sits under this rim at the sprite's own aspect) is merely four times sharper.

A chamfered rectangle 9-slices because a 45 deg cut lives entirely inside a corner tile; the
lockup's TRAPEZOID cannot (its slant runs the whole edge), which is why that one is generated
at runtime and this one can stay a sprite.

    python3 Tools/Build/author_toy_card_sprites.py            # write both PNGs + metas
    python3 Tools/Build/author_toy_card_sprites.py --check    # fail if either drifted
"""
import argparse
import os
import re
import struct
import sys
import zlib

import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FOLDER = os.path.join(REPO, "Assets", "_Graphics", "Design Assests", "Menu_Main",
                      "R_Menu_Main", "ArcadeScreen")
PLATE = os.path.join(FOLDER, "Group 1585.png")
RIM = os.path.join(FOLDER, "Rectangle 1127 (2).png")

# Design space: the shipped 228x170, so every constant reads against the old file.
DW, DH = 228.0, 170.0
SCALE = 4                       # 912x680 out
SS = 2                          # supersample, box-downsampled

CHAMFER = 12.0                  # the two 45 deg cuts: top-left and bottom-right. Was the shipped
                                # 24, halved on playtest: at 24 the cut ate a fifth of a variant
                                # row's height and read as a "bent" card rather than a crisp one.
BORDER = 20.0                   # 9-slice border, design px. MUST exceed CHAMFER or a stretched
                                # middle tile cuts through the corner.
PPU = 100 * SCALE               # so a SLICED draw at multiplier 1 is the design scale ON A CANVAS
                                # WHOSE referencePixelsPerUnit IS 100. Menu_Main's canvas is 240,
                                # and UGUI divides a sprite's PPU by that, so a sliced border on
                                # that canvas draws 2.4x too wide unless the consumer sets
                                # pixelsPerUnitMultiplier = 240/100 - author_toybox_layout.py does,
                                # and reads the two numbers off the .meta and the scene rather
                                # than trusting this comment.

# Plate: a navy slab, lit at the top, with a soft inner glow against its own edge - the shipped
# art's mean (0, 1, 27) is kept as the floor so a tinted card (the Toy Box multiplies this by the
# toy's accent) keeps the weight it had.
# Lifted on playtest (was top (0.06, 0.09, 0.30) -> bottom (0, 0.004, 0.106)): a variant row tints
# this plate by a muted accent, and a body that dark went to black under the tint - "no card
# background". It stays a navy slab; it is no longer one that vanishes when coloured.
BODY_TOP = (0.13, 0.18, 0.44)
BODY_BOTTOM = (0.04, 0.06, 0.20)
GLOW_ALPHA = 0.22
GLOW_DEPTH = 10.0

# Rim: the shipped stroke colour (92, 95, 112), one design px wide, lifted on the chamfers the
# way the lockup's graded band is (Docs/ABILITY_LOCKUP.md) and grading to a floor along the
# straights - a floor, because this is a closed frame and a rim that reached zero would open it.
RIM_RGB = (92 / 255, 95 / 255, 112 / 255)
RIM_W = 2.2                     # was 1.4 - sub-pixel once the card is drawn at canvas scale
RIM_LIFT = 0.45                 # toward white on the chamfers
BAND_FLOOR = 0.70
BAND_WRAP = 26.0
# The shipped rim also carried a diagonal wash (dark at the top-left, clear at the bottom-right)
# that gave the card its depth. Kept, lighter: it was 0.8 alpha of near-black and it swallowed a
# tinted rim.
WASH_ALPHA = 0.30
WASH_RGB = (0.0, 0.004, 0.043)

assert BORDER > CHAMFER, "the 9-slice border must contain the chamfer"
assert 2 * BORDER < min(DW, DH), "the borders would meet in the middle"


def polygon(x0, y0, x1, y1, s):
    """The chamfered plate, clockwise in image space. Convex - the SDF below is exact."""
    c = CHAMFER * s
    return np.array([
        (x0 + c, y0),
        (x1,     y0),
        (x1,     y1 - c),
        (x1 - c, y1),
        (x0,     y1),
        (x0,     y0 + c),
    ], dtype=np.float64)


def convex_sdf(pts, X, Y):
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
    return np.clip(0.5 - dist, 0.0, 1.0)


def distance_to_chamfers(pts, X, Y):
    best = None
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        e = b - a
        if abs(abs(e[0]) - abs(e[1])) > 1e-6:
            continue
        t = np.clip(((X - a[0]) * e[0] + (Y - a[1]) * e[1]) / (e @ e), 0.0, 1.0)
        di = np.hypot(X - (a[0] + t * e[0]), Y - (a[1] + t * e[1]))
        best = di if best is None else np.minimum(best, di)
    return best


def grid():
    S = SCALE * SS
    W, H = int(DW * S), int(DH * S)
    X, Y = np.meshgrid(np.arange(W) + 0.5, np.arange(H) + 0.5)
    return S, W, H, X, Y


def downsample(rgb, a, flat_rgb):
    """Premultiplied box filter, then un-premultiply; clear pixels carry the flat colour."""
    H, W = a.shape
    rgb = rgb.reshape(H // SS, SS, W // SS, SS, 3).mean(axis=(1, 3))
    a = a.reshape(H // SS, SS, W // SS, SS).mean(axis=(1, 3))
    safe = a > 1e-4
    out = np.where(safe[..., None], rgb / np.maximum(a, 1e-4)[..., None], flat_rgb)
    rgba = np.empty((H // SS, W // SS, 4), dtype=np.uint8)
    rgba[..., :3] = np.rint(np.clip(out, 0, 1) * 255).astype(np.uint8)
    rgba[..., 3] = np.rint(np.clip(a, 0, 1) * 255).astype(np.uint8)
    return rgba


def render_plate():
    S, W, H, X, Y = grid()
    poly = polygon(0.0, 0.0, W, H, S)
    d = convex_sdf(poly, X, Y)
    cov = coverage(d)
    depth = np.clip(-d, 0.0, None)

    v = np.clip(Y / H, 0.0, 1.0)[..., None]
    body = np.array(BODY_TOP) * (1 - v) + np.array(BODY_BOTTOM) * v
    glow = GLOW_ALPHA * np.clip(1.0 - depth / (GLOW_DEPTH * S), 0.0, 1.0) ** 2
    rgb = body + (1.0 - body) * glow[..., None]        # the glow lifts the body toward white
    flat = np.array(BODY_BOTTOM)[None, None, :] * np.ones((H // SS, W // SS, 1))
    return downsample(rgb * cov[..., None], cov, flat)


def render_rim():
    S, W, H, X, Y = grid()
    poly = polygon(0.0, 0.0, W, H, S)
    d = convex_sdf(poly, X, Y)
    cov = coverage(d)
    depth = np.clip(-d, 0.0, None)

    dc = distance_to_chamfers(poly, X, Y)
    t = np.clip(dc / (BAND_WRAP * S), 0.0, 1.0)
    band = 0.5 + 0.5 * np.cos(np.pi * t)
    rim_profile = np.clip(1.0 - depth / (RIM_W * S), 0.0, 1.0)
    rim_a = cov * rim_profile * (BAND_FLOOR + (1.0 - BAND_FLOOR) * band)
    rim_rgb = np.array(RIM_RGB)
    rim_rgb = rim_rgb + (1.0 - rim_rgb) * (RIM_LIFT * band)[..., None]

    # the wash: dark top-left, clear bottom-right, inside the plate only
    diag = np.clip((X / W + Y / H) * 0.5, 0.0, 1.0)
    wash_a = cov * WASH_ALPHA * (1.0 - diag) ** 1.6

    acc_rgb = np.zeros((H, W, 3))
    acc_a = np.zeros((H, W))
    for layer_rgb, layer_a in ((np.array(WASH_RGB)[None, None, :] * np.ones((H, W, 1)), wash_a),
                               (rim_rgb, rim_a)):
        acc_rgb = layer_rgb * layer_a[..., None] + acc_rgb * (1 - layer_a[..., None])
        acc_a = layer_a + acc_a * (1 - layer_a)

    flat = np.array(RIM_RGB)[None, None, :] * np.ones((H // SS, W // SS, 1))
    return downsample(acc_rgb, acc_a, flat)


# ---------------------------------------------------------------------------------------------
# PNG io + meta
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
    d = open(path, "rb").read()
    assert d[:8] == b"\x89PNG\r\n\x1a\n", f"{path}: not a PNG"
    pos, idat, w = 8, b"", None
    while pos < len(d):
        ln = struct.unpack(">I", d[pos:pos + 4])[0]
        tag = d[pos + 4:pos + 8]
        data = d[pos + 8:pos + 8 + ln]
        if tag == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", data[:10])
            assert bd == 8 and ct in (2, 6) and data[12] == 0, f"{path}: unsupported PNG variant"
        elif tag == b"IDAT":
            idat += data
        pos += 12 + ln
    raw = zlib.decompress(idat)
    ch = 4 if ct == 6 else 3
    stride = w * ch
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[..., 3] = 255
    prev = np.zeros(stride, dtype=np.int32)
    p = 0
    for y in range(h):
        f = raw[p]
        line = np.frombuffer(raw[p + 1:p + 1 + stride], dtype=np.uint8).astype(np.int32)
        p += 1 + stride
        if f == 1:
            for i in range(ch, stride): line[i] = (line[i] + line[i - ch]) & 255
        elif f == 2:
            line = (line + prev) & 255
        elif f == 3:
            for i in range(stride):
                left = line[i - ch] if i >= ch else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = line[i - ch] if i >= ch else 0
                b = prev[i]
                c = prev[i - ch] if i >= ch else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        prev = line
        out[y, :, :ch] = line.reshape(w, ch)
    return out, w, h


META_BORDER = f"  spriteBorder: {{x: {int(BORDER * SCALE)}, y: {int(BORDER * SCALE)}, " \
              f"z: {int(BORDER * SCALE)}, w: {int(BORDER * SCALE)}}}"
META_PPU = f"  spritePixelsToUnits: {PPU}"


def meta_patch(text):
    text = re.sub(r"^  spriteBorder: \{[^}]*\}", META_BORDER, text, flags=re.M)
    text = re.sub(r"^  spritePixelsToUnits: \d+", META_PPU, text, flags=re.M)
    return text


def assertions(rgba, label):
    a = rgba[..., 3].astype(float) / 255.0
    h, w = a.shape
    fails = []
    if (w, h) != (int(DW * SCALE), int(DH * SCALE)):
        fails.append(f"{label}: size is {w}x{h}, expected {int(DW*SCALE)}x{int(DH*SCALE)}")
    # the chamfer corners are clear, the square corners are not
    c = int(CHAMFER * SCALE * 0.3)
    if a[c, c] > 0.02 or a[h - 1 - c, w - 1 - c] > 0.02:
        fails.append(f"{label}: chamfered corners are not clear")
    # the plate is solid there; the rim is a 2.2 design px stroke at 70% floor, so the same
    # pixel reads well over 0.4 on it
    floor = 0.5 if label == "plate" else 0.25
    if a[1, w - 2] < floor or a[h - 2, 1] < floor:
        fails.append(f"{label}: square corners did not draw")
    # the middle tile must be flat enough to stretch: alpha in the centre band is constant
    mid = a[int(BORDER * SCALE) + 4:h - int(BORDER * SCALE) - 4, int(BORDER * SCALE) + 4:w - int(BORDER * SCALE) - 4]
    if mid.size and (mid.max() - mid.min()) > (0.02 if label == "plate" else 0.35):
        fails.append(f"{label}: the 9-slice middle tile is not stretch-safe "
                     f"(alpha spread {mid.max() - mid.min():.3f})")
    rgb = rgba[..., :3].astype(float) / 255.0
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
    clear = a < 0.004
    if clear.any() and lum[clear].mean() < 0.0:
        fails.append(f"{label}: transparent pixels are black")
    return fails


def check_file(path, rgba):
    if not os.path.exists(path):
        return f"{os.path.relpath(path, REPO)} is missing"
    on_disk, w, h = read_png_rgba(path)
    if (w, h) != (rgba.shape[1], rgba.shape[0]):
        return f"{os.path.relpath(path, REPO)} is {w}x{h}, this tool authors {rgba.shape[1]}x{rgba.shape[0]}"
    drift = np.abs(on_disk.astype(int) - rgba.astype(int)).max()
    if drift > 1:
        return f"{os.path.relpath(path, REPO)} differs from what this tool authors (max drift {drift})"
    meta = open(path + ".meta").read()
    if META_BORDER not in meta or META_PPU not in meta:
        return f"{os.path.relpath(path, REPO)}.meta: 9-slice border / pixels-per-unit not authored"
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    jobs = [("plate", PLATE, render_plate()), ("rim", RIM, render_rim())]
    fails = []
    for label, _, rgba in jobs:
        fails += assertions(rgba, label)
    if fails:
        for f in fails:
            print(f"FAIL: {f}", file=sys.stderr)
        return 1

    if args.check:
        bad = [m for _, path, rgba in jobs for m in [check_file(path, rgba)] if m]
        for b in bad:
            print(f"FAIL: {b}", file=sys.stderr)
        if bad:
            return 1
        print("OK  card plate + rim sprites match")
        return 0

    for label, path, rgba in jobs:
        with open(path, "wb") as fh:
            fh.write(encode_png(rgba))
        meta_path = path + ".meta"
        meta = open(meta_path).read()
        patched = meta_patch(meta)
        if patched != meta:
            open(meta_path, "w").write(patched)
        print(f"wrote {os.path.relpath(path, REPO)}  {rgba.shape[1]}x{rgba.shape[0]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
