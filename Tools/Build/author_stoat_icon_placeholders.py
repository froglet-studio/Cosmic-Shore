#!/usr/bin/env python3
"""Author the Stoat ability row's PLACEHOLDER icon sprites.

White silhouettes on a transparent 128 px canvas in the house icon language (the schema of
the Thresher's placeholder tool, which this copied - retired 2026-10-13 when its art pass,
Tools/Build/author_thresher_icons.py, landed: anti-aliased 8x renders, the model for this one's art
pass), multiply-tinted at runtime and replaced 1:1 by the art pass. Each names the ACT (R_VesselActions/STOAT_DIPOLE.md):

  Charge  Ripple        two wavefronts thrown out of the point where a pair met
  Mass    Mass / Time   a thick slab of trail beside an orbit arrow (the B switch)
  Space   Field Dipole  a sink (solid) and a source (ring) with the field arcing between
  Time    Pathfinder    a dotted, curving line ending in an arrowhead

The Mass card's icon follows the MODE B is in (StoatHUDView, 2026-10-26), one icon per mode:
  Mass mode    a thick slab of trail and the path it flies (the field trajectory)
  Time mode    an orbit round a solid attractor, arrowed (the catch sling)
  Drive mode   the warp pair: a black hole ahead, a white hole behind, the hull between them on its axis

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
OUT_DIR = "Assets/_Graphics/Icons/AbilityIcons/Stoat"
DONOR_META = "Assets/_Graphics/Icons/Icon_Generate.png.meta"


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/StoatIcons/{name}".encode()).hexdigest()


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


def ripple(px, py):
    """Two wavefronts thrown out of the point where a pair met."""
    if circle(px, py, 64, 64, 9):
        return True                                    # the meeting point
    if ring(px, py, 64, 64, 30.0, 5.0):
        return True                                    # the first front
    if arc(px, py, 64, 64, 52.0, 4.5, 20.0, 160.0) or arc(px, py, 64, 64, 52.0, 4.5, 200.0, 340.0):
        return True                                    # the second, breaking up as it spreads
    return False


def modes(px, py):
    """The B switch: a thick slab of trail (Mass) beside an orbit arrow (Time)."""
    if 14 <= px <= 50 and 28 <= py <= 100:
        return True                                    # the thick trail
    if arc(px, py, 88, 64, 24.0, 5.0, 60.0, 330.0):
        return True                                    # the orbit
    if tri(px, py, 100, 98, 118, 84, 98, 76):
        return True                                    # its arrowhead
    return False


def field_dipole(px, py):
    """A sink (solid) and a source (ring), the field arcing between them."""
    if circle(px, py, 30, 64, 14):
        return True                                    # the sink
    if ring(px, py, 98, 64, 13.0, 4.5):
        return True                                    # the source
    if arc(px, py, 64, 46, 38.0, 3.5, 25.0, 155.0):
        return True                                    # a field line over
    if arc(px, py, 64, 82, 38.0, 3.5, 205.0, 335.0):
        return True                                    # and under
    return False


def pathfinder(px, py):
    """A dotted, curving line ending in an arrowhead."""
    for cx, cy in ((14, 20), (36, 26), (57, 37), (75, 53), (89, 72)):
        if circle(px, py, cx, cy, 8.0):
            return True                                # the dots
    if tri(px, py, 108, 116, 86, 96, 110, 84):
        return True                                    # the arrowhead
    return False


def mode_mass(px, py):
    """Mass mode: the thick trail, and the dotted path the field flies."""
    if 12 <= px <= 46 and 22 <= py <= 106:
        return True                                    # the thick trail
    for cx, cy in ((64, 30), (78, 46), (89, 64), (96, 83)):
        if circle(px, py, cx, cy, 7.0):
            return True                                # the path
    if tri(px, py, 102, 118, 88, 98, 112, 96):
        return True                                    # its arrowhead
    return False


def mode_time(px, py):
    """Time mode: an orbit round a solid attractor, arrowed."""
    if circle(px, py, 64, 64, 12):
        return True                                    # the attractor
    if arc(px, py, 64, 64, 40.0, 6.0, 60.0, 330.0):
        return True                                    # the orbit
    if tri(px, py, 98, 90, 89, 108, 77, 89):
        return True                                    # its arrowhead
    return False


def mode_drive(px, py):
    """Drive mode: the warp pair riding with the hull - the black hole ahead, the white hole behind, the hull between."""
    if circle(px, py, 96, 96, 19):
        return True                                    # the black hole, ahead
    if ring(px, py, 32, 32, 15.0, 6.0):
        return True                                    # the white hole, behind
    if tri(px, py, 76, 76, 42, 62, 62, 42):
        return True                                    # the hull, nose toward the black hole
    return False


ICONS = {
    "Stoat_Ripple.png": ripple,
    "Stoat_Modes.png": modes,
    "Stoat_FieldDipole.png": field_dipole,
    "Stoat_Pathfinder.png": pathfinder,
    "Stoat_ModeMass.png": mode_mass,
    "Stoat_ModeTime.png": mode_time,
    "Stoat_ModeDrive.png": mode_drive,
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


FOLDER_METAS = {OUT_DIR: guid_for("Stoat.folder")}


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
