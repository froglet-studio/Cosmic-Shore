#!/usr/bin/env python3
"""Author the Manta ability row's PLACEHOLDER icon sprites (Sting / Yastri / Kabloom / Soar).

The four icons authored by the spec remake shipped with empty sprite slots, which a UGUI
Image renders as a bare white box — unreadable on the lockup card (playtest 2026-08-26:
"no placeholder icons, just white boxes"). These are deliberate PLACEHOLDERS: clean white
silhouettes in the house icon language (bomb / turn arrow / bloom / triple chevron), sized
128 px like the petal sprites, for the art polish pass to replace 1:1.

Pure-python PNG raster (no PIL), donor-cloned sprite .meta (Icon_Generate.png.meta is the
schema donor) with maxTextureSize dropped to 128, deterministic guids, idempotent, --check.
The folder .meta is emitted too — a folder without one gets a fresh guid re-minted on every
other machine (asset-surgery rule).
"""
import hashlib
import math
import os
import re
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK = "--check" in sys.argv
OUT_DIR = "Assets/_Graphics/Icons/AbilityIcons/Manta"
DONOR_META = "Assets/_Graphics/Icons/Icon_Generate.png.meta"


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/MantaRemake/{name}".encode()).hexdigest()


# ── Coverage functions (math coords, y UP, canvas 0..128) ────────────────────

def seg_dist(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    wx, wy = px - ax, py - ay
    c1 = vx * wx + vy * wy
    c2 = vx * vx + vy * vy
    t = 0.0 if c2 <= 0 else max(0.0, min(1.0, c1 / c2))
    dx, dy = px - (ax + t * vx), py - (ay + t * vy)
    return math.hypot(dx, dy)


def circle(px, py, cx, cy, r):
    return math.hypot(px - cx, py - cy) <= r


def capsule(px, py, ax, ay, bx, by, w):
    return seg_dist(px, py, ax, ay, bx, by) <= w


def sting(px, py):
    """A bomb: round body, neck cap, short fuse, spark at the tip."""
    if circle(px, py, 64, 52, 31):
        return True
    if capsule(px, py, 56, 84, 72, 84, 7):                       # the neck cap
        return True
    if capsule(px, py, 64, 90, 74, 102, 5) or capsule(px, py, 74, 102, 88, 106, 5):
        return True                                              # the fuse
    sx, sy = 92, 110                                             # the spark
    for ang in (20, 110, 200, 290):
        a = math.radians(ang)
        if capsule(px, py, sx, sy, sx + 11 * math.cos(a), sy + 11 * math.sin(a), 3.4):
            return True
    return False


def yastri(px, py):
    """A hard-turn arrow: a thick arc sweeping over the top, arrowhead on the right."""
    cx, cy, R, t = 64, 56, 34, 9
    dx, dy = px - cx, py - cy
    r = math.hypot(dx, dy)
    if abs(r - R) <= t:
        ang = math.degrees(math.atan2(dy, dx)) % 360.0
        if 20.0 <= ang <= 175.0:
            return True
    # Arrowhead at the 20-degree end, pointing along the clockwise tangent.
    a = math.radians(20)
    ex, ey = cx + R * math.cos(a), cy + R * math.sin(a)
    tx, ty = math.sin(a), -math.cos(a)          # clockwise tangent
    nx, ny = math.cos(a), math.sin(a)           # outward normal
    tipx, tipy = ex + 20 * tx, ey + 20 * ty
    b1x, b1y = ex + 15 * nx, ey + 15 * ny
    b2x, b2y = ex - 15 * nx, ey - 15 * ny
    def side(x1, y1, x2, y2, x3, y3):
        return (x2 - x1) * (y3 - y1) - (y2 - y1) * (x3 - x1)
    d1 = side(tipx, tipy, b1x, b1y, px, py)
    d2 = side(b1x, b1y, b2x, b2y, px, py)
    d3 = side(b2x, b2y, tipx, tipy, px, py)
    return (d1 >= 0 and d2 >= 0 and d3 >= 0) or (d1 <= 0 and d2 <= 0 and d3 <= 0)


def kabloom(px, py):
    """A six-petal bloom around a core — the flower the crystal cash-out plays."""
    if circle(px, py, 64, 64, 12):
        return True
    for i in range(6):
        a = math.radians(90 + i * 60)
        if circle(px, py, 64 + 29 * math.cos(a), 64 + 29 * math.sin(a), 15):
            return True
    return False


def soar(px, py):
    """Three climbing chevrons — the analog boost."""
    for tip_y in (96, 70, 44):
        if capsule(px, py, 64, tip_y, 38, tip_y - 18, 6.5): return True
        if capsule(px, py, 64, tip_y, 90, tip_y - 18, 6.5): return True
    return False


ICONS = {
    "Manta_Sting.png": sting,
    "Manta_Yastri.png": yastri,
    "Manta_Kabloom.png": kabloom,
    "Manta_Soar.png": soar,
}

SIZE, SS = 128, 3  # canvas, supersample factor


def render(fn) -> bytes:
    rows = []
    for y in range(SIZE):
        row = bytearray([0])  # filter byte
        for x in range(SIZE):
            hit = 0
            for sy in range(SS):
                for sx in range(SS):
                    # pixel (x, y) top-down -> math coords y up
                    mx = x + (sx + 0.5) / SS
                    my = SIZE - (y + (sy + 0.5) / SS)
                    if fn(mx, my):
                        hit += 1
            a = round(255 * hit / (SS * SS))
            row += bytes((255, 255, 255, a))
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(tag, data):
        c = tag + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c))

    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


FOLDER_METAS = {
    "Assets/_Graphics/Icons/AbilityIcons": guid_for("AbilityIcons.folder"),
    OUT_DIR: guid_for("AbilityIcons.Manta.folder"),
}


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
    drift = []
    writes = {}
    for folder, g in FOLDER_METAS.items():
        writes[folder + ".meta"] = folder_meta(g)
    for name, fn in ICONS.items():
        writes[f"{OUT_DIR}/{name}"] = render(fn)
        writes[f"{OUT_DIR}/{name}.meta"] = sprite_meta(guid_for(name))

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
            print("DRIFT:\n  " + "\n  ".join(drift)); return 1
        print("check clean"); return 0
    print(f"wrote {len(drift)} file(s)")
    for name in ICONS:
        print(f"  {name}: guid {guid_for(name)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
