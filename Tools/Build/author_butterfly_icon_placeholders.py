#!/usr/bin/env python3
"""Author the Butterfly ability row's PLACEHOLDER icon sprites.

The Butterfly shipped binding ZERO ability icons, so all four of its lockup cards render
LOCKED -- which is honest, and which leaves the Fold's recharge veil sweeping over a bare
plate with nothing under it. That is indistinguishable from a recharge nobody is driving
(the Serpent's report, one vessel over), and it is what the playtest asked to fix.

These are deliberate PLACEHOLDERS in the house icon language, 128 px like the Manta's
(Tools/Build/author_manta_icon_placeholders.py, the schema this tool copies), for the art
pass to replace 1:1. Each names the ACT rather than the vessel -- three of the four could
otherwise be "a wing" and be unreadable at the 40 px they are drawn at:

  Charge  Scale Dust    one wing, shedding a plume of motes
  Mass    Spread Wings  the whole butterfly, symmetric and wide
  Space   Wingreach     a wing with reach arcs sweeping off it
  Time    Fold          here (a filled disc), the leap, there (an open ring)

The Fold's icon is drawn as its GATES on purpose: a fold leaves a pair of portals standing,
so "here -> there" is what the ability now literally does.

Pure-python PNG raster (no PIL), donor-cloned sprite .meta, deterministic guids, idempotent,
--check. The folder .meta is emitted too -- a folder without one gets a fresh guid re-minted
on every other machine (asset-surgery rule).
"""
import hashlib
import math
import os
import re
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is not the project root: {ROOT}"
CHECK = "--check" in sys.argv
OUT_DIR = "Assets/_Graphics/Icons/AbilityIcons/Butterfly"
DONOR_META = "Assets/_Graphics/Icons/Icon_Generate.png.meta"


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/ButterflyIcons/{name}".encode()).hexdigest()


# -- Coverage functions (math coords, y UP, canvas 0..128) --------------------

def seg_dist(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    wx, wy = px - ax, py - ay
    c2 = vx * vx + vy * vy
    t = 0.0 if c2 <= 0 else max(0.0, min(1.0, (vx * wx + vy * wy) / c2))
    return math.hypot(px - (ax + t * vx), py - (ay + t * vy))


def circle(px, py, cx, cy, r):
    return math.hypot(px - cx, py - cy) <= r


def ring(px, py, cx, cy, r, t):
    return abs(math.hypot(px - cx, py - cy) - r) <= t


def capsule(px, py, ax, ay, bx, by, w):
    return seg_dist(px, py, ax, ay, bx, by) <= w


def ellipse(px, py, cx, cy, rx, ry, rot_deg=0.0):
    a = math.radians(-rot_deg)
    dx, dy = px - cx, py - cy
    ux = dx * math.cos(a) - dy * math.sin(a)
    uy = dx * math.sin(a) + dy * math.cos(a)
    return (ux / rx) ** 2 + (uy / ry) ** 2 <= 1.0


def arc(px, py, cx, cy, r, t, a0, a1):
    if abs(math.hypot(px - cx, py - cy) - r) > t:
        return False
    ang = math.degrees(math.atan2(py - cy, px - cx)) % 360.0
    return a0 <= ang <= a1


def tri(px, py, ax, ay, bx, by, cx, cy):
    def side(x1, y1, x2, y2, x3, y3):
        return (x2 - x1) * (y3 - y1) - (y2 - y1) * (x3 - x1)
    d1 = side(ax, ay, bx, by, px, py)
    d2 = side(bx, by, cx, cy, px, py)
    d3 = side(cx, cy, ax, ay, px, py)
    return (d1 >= 0 and d2 >= 0 and d3 >= 0) or (d1 <= 0 and d2 <= 0 and d3 <= 0)


def scale_dust(px, py):
    """One wing, shedding a plume of motes down and to the right."""
    if ellipse(px, py, 46, 84, 28, 15, 30):
        return True                                    # upper wing
    if ellipse(px, py, 50, 60, 18, 10, 12):
        return True                                    # lower lobe
    for cx, cy, r in ((78, 44, 9.0), (96, 29, 7.0), (112, 16, 5.0)):
        if circle(px, py, cx, cy, r):
            return True                                # the dust, falling away
    return False


def spread_wings(px, py):
    """The whole butterfly: symmetric, wide, wings up and out."""
    if capsule(px, py, 64, 34, 64, 92, 5.0):
        return True                                    # body
    if ellipse(px, py, 40, 76, 25, 14, 34) or ellipse(px, py, 88, 76, 25, 14, -34):
        return True                                    # upper wings
    if ellipse(px, py, 45, 44, 17, 10, -22) or ellipse(px, py, 83, 44, 17, 10, 22):
        return True                                    # lower wings
    return False


def wingreach(px, py):
    """A wing with reach arcs sweeping clear of it -- the passive that dissolves what passes."""
    if ellipse(px, py, 38, 44, 26, 14, 28):
        return True                                    # the wing
    if ellipse(px, py, 42, 24, 16, 9, 10):
        return True                                    # lower lobe
    for r in (54.0, 76.0):
        if arc(px, py, 38, 38, r, 6.5, 14.0, 70.0):
            return True                                # the reach
    return False


def fold(px, py):
    """Two gates and the leap between them -- what a fold actually leaves standing."""
    if ring(px, py, 22, 64, 16.0, 5.5) or ring(px, py, 106, 64, 16.0, 5.5):
        return True                                    # the pair
    if capsule(px, py, 46, 64, 62, 64, 4.5):
        return True                                    # the leap
    if tri(px, py, 82, 64, 62, 80, 62, 48):
        return True                                    # pointing into the far gate
    return False


ICONS = {
    "Butterfly_ScaleDust.png": scale_dust,
    "Butterfly_SpreadWings.png": spread_wings,
    "Butterfly_Wingreach.png": wingreach,
    "Butterfly_Fold.png": fold,
}

SIZE, SS = 128, 3  # canvas, supersample factor


def coverage(fn):
    """Fraction of the canvas the silhouette covers -- the one number that separates a
    readable placeholder from a blob or a hairline."""
    hit = 0
    for y in range(SIZE):
        for x in range(SIZE):
            if fn(x + 0.5, SIZE - (y + 0.5)):
                hit += 1
    return hit / (SIZE * SIZE)


def render(fn) -> bytes:
    rows = []
    for y in range(SIZE):
        row = bytearray([0])  # filter byte
        for x in range(SIZE):
            hit = 0
            for sy in range(SS):
                for sx in range(SS):
                    mx = x + (sx + 0.5) / SS
                    my = SIZE - (y + (sy + 0.5) / SS)
                    if fn(mx, my):
                        hit += 1
            row += bytes((255, 255, 255, round(255 * hit / (SS * SS))))
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(tag, data):
        c = tag + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c))

    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


FOLDER_METAS = {OUT_DIR: guid_for("Butterfly.folder")}


def folder_meta(guid: str) -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {guid}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


def sprite_meta(guid: str) -> str:
    donor = open(os.path.join(ROOT, DONOR_META), encoding="utf-8").read()
    assert "textureType: 8" in donor and "spriteMode: 1" in donor, "donor is not a sprite meta"
    out = re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {guid}", donor, count=1, flags=re.M)
    out = re.sub(r"maxTextureSize: \d+", "maxTextureSize: 128", out)
    assert out.count(guid) == 1
    return out


def main() -> int:
    # A placeholder that covers 2% of its box is a hairline and one that covers 60% is a
    # blob; both read as "no icon" at the 40 px a lockup card draws.
    for name, fn in ICONS.items():
        c = coverage(fn)
        assert 0.08 <= c <= 0.45, f"{name}: coverage {c:.3f} outside the readable band"

    writes = {}
    for folder, g in FOLDER_METAS.items():
        writes[folder + ".meta"] = folder_meta(g)
    for name, fn in ICONS.items():
        writes[f"{OUT_DIR}/{name}"] = render(fn)
        writes[f"{OUT_DIR}/{name}.meta"] = sprite_meta(guid_for(name))

    drift = []
    for rel, want in writes.items():
        path = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        mode = "rb" if isinstance(want, bytes) else "r"
        have = open(path, mode).read() if os.path.exists(path) else None
        if have != want:
            drift.append(rel)
            if not CHECK:
                with open(path, "wb" if isinstance(want, bytes) else "w") as f:
                    f.write(want)
    if CHECK:
        if drift:
            print("DRIFT:\n  " + "\n  ".join(drift))
            return 1
        print("check clean")
        return 0
    print(f"wrote {len(drift)} file(s)")
    for name, fn in ICONS.items():
        print(f"  {name}: guid {guid_for(name)}  coverage {coverage(fn):.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
