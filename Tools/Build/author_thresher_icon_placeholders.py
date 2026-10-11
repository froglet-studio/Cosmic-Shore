#!/usr/bin/env python3
"""Author the Thresher ability row's PLACEHOLDER icon sprites.

White silhouettes on a transparent 128 px canvas in the house icon language (the schema of
Tools/Build/author_butterfly_icon_placeholders.py, which this copies), multiply-tinted at
runtime and replaced 1:1 by the art pass. Each names the ACT:

  Charge  Wrecking Ball  the studded ball, with an impact burst
  Mass    Heavy Iron     a weight with its handle
  Space   Winch          a run of chain links ending in the ball
  Time    Plant          a pivot, the orbit round it, and the hull on it

Pure-python PNG raster (no PIL), donor-cloned sprite .meta, deterministic guids, idempotent,
--check. The folder .meta is emitted too.
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
OUT_DIR = "Assets/_Graphics/Icons/AbilityIcons/Thresher"
DONOR_META = "Assets/_Graphics/Icons/Icon_Generate.png.meta"


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/ThresherIcons/{name}".encode()).hexdigest()


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


def wrecking_ball(px, py):
    """The studded ball, an impact burst off its upper right."""
    if circle(px, py, 52, 52, 30):
        return True                                    # the ball
    for cx, cy in ((52, 86), (52, 18), (86, 52), (18, 52)):
        if circle(px, py, cx, cy, 7.0):
            return True                                # studs
    for ax, ay, bx, by in ((88, 88, 112, 112), (96, 74, 120, 80), (74, 96, 80, 120)):
        if capsule(px, py, ax, ay, bx, by, 4.5):
            return True                                # the crack
    return False


def heavy_iron(px, py):
    """A weight with its handle."""
    if ellipse(px, py, 64, 50, 38, 32):
        return True                                    # the bell
    if ring(px, py, 64, 92, 17.0, 6.0) and py > 80:
        return True                                    # the handle
    return False


def winch(px, py):
    """A run of chain links ending in the ball."""
    for i in range(4):
        cx, cy = 20 + i * 17, 108 - i * 17
        if ring(px, py, cx, cy, 10.0, 3.6):
            return True                                # links
    if circle(px, py, 98, 30, 20):
        return True                                    # the ball
    return False


def plant(px, py):
    """A pivot, the orbit round it, and the hull riding it."""
    if circle(px, py, 64, 64, 12):
        return True                                    # the planted ball
    if arc(px, py, 64, 64, 44.0, 5.0, 40.0, 340.0):
        return True                                    # the orbit
    if tri(px, py, 108, 64, 92, 84, 92, 44):
        return True                                    # the hull, on the orbit
    if capsule(px, py, 64, 64, 96, 64, 2.5):
        return True                                    # the chain to it
    return False


ICONS = {
    "Thresher_WreckingBall.png": wrecking_ball,
    "Thresher_HeavyIron.png": heavy_iron,
    "Thresher_Winch.png": winch,
    "Thresher_Plant.png": plant,
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


FOLDER_METAS = {OUT_DIR: guid_for("Thresher.folder")}


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
