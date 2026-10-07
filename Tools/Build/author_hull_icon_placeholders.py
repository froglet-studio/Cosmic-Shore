#!/usr/bin/env python3
"""Author PLACEHOLDER ability-icon sprites for the Rhino, Serpent and Scarab rows.

Three hulls had no art of their own on the four-icon ability row:

  Rhino    bound 0/4 icons, and no Rhino ability sprite existed at all.
  Serpent  bound 1/4 (Time, the fuel-pellet icon from author_serpent_pellet_icon.py);
           the Sniper Shot and the Scope - both shipped abilities - drew LOCKED.
  Scarab   bound 4/4, but every sprite was the SPARROW's (missiles / swap weapon /
           bullet / boost from HUD UI/New_Sparrow), which match none of its abilities.

These are deliberate PLACEHOLDERS - every file is named `*-PLACEHOLDER.png` (the
Squirrel's HuntIcon-PLACEHOLDER convention), so a grep for PLACEHOLDER finds every one the
art pass owes. Clean white silhouettes in the house icon language, 128 px like the Manta's
and Butterfly's (Tools/Build/author_manta_icon_placeholders.py is the schema this copies),
for the art pass to replace 1:1 by overwriting the PNG and keeping the .meta guid. White
matters: the Scarab's view TINTS its icons as gauges (ready / recharging, switch charges,
ball energy), and a white source is the only one a multiply tint reproduces exactly.

Each icon names the ACT, read from the hull's ElementalAbilityMapSO entry:

  Rhino    Mass   Trail Slabs        three trail slabs, growing   (Mass = size)
           Time   Ramp Spool         a ramp wedge under a wind-up arrow   (Time = rate)
  Serpent  Charge Sniper Shot        one long round with its tracer streaks
           Space  Scope              a reticle: ring, split crosshair, centre dot
  Scarab   Charge Cavitation Blast   a bubble throwing three shock arcs
           Mass   Switch             a ball about to thread a ring
           Space  Ball Forge         a ball with crystals converging on it
           Time   Throttle           a speed dial with its needle

OPEN DESIGN SLOTS GET NO ICON - the Rhino's Charge and Space and the Serpent's Mass. Their
map entries are `(open design slot)` with no ability behind them, and the contract for that
is a LOCKED card (VesselHUDView.omniAbilitySprite's tooltip: "an ability that does not exist
is not the same as one the player has not unlocked, and the locked card says the first").
Drawing a picture for one would be inventing an ability (/vessel skill section 3).

Pure-python PNG raster (no PIL), donor-cloned sprite .meta (Icon_Generate.png.meta) with
maxTextureSize dropped to 128, deterministic guids, idempotent, --check. Each hull's folder
.meta is emitted too - a folder without one gets a fresh guid re-minted on every other
machine (asset-surgery rule). The shared AbilityIcons/ parent folder .meta is OWNED by
author_manta_icon_placeholders.py and is only asserted present here, never written.

    python3 Tools/Build/author_hull_icon_placeholders.py           # (re)author
    python3 Tools/Build/author_hull_icon_placeholders.py --check   # fail on drift
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
ICON_ROOT = "Assets/_Graphics/Icons/AbilityIcons"
DONOR_META = "Assets/_Graphics/Icons/Icon_Generate.png.meta"


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/HullIconPlaceholders/{name}".encode()).hexdigest()


# -- Coverage primitives (math coords, y UP, canvas 0..128) -------------------

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


def arc(px, py, cx, cy, r, t, a0, a1):
    """Ring segment from a0 to a1 degrees, counter-clockwise; a0 may be negative."""
    if abs(math.hypot(px - cx, py - cy) - r) > t:
        return False
    ang = math.degrees(math.atan2(py - cy, px - cx))
    for wrap in (-360.0, 0.0, 360.0):
        if a0 <= ang + wrap <= a1:
            return True
    return False


def triangle(px, py, a, b, c):
    def side(p, q):
        return (q[0] - p[0]) * (py - p[1]) - (q[1] - p[1]) * (px - p[0])
    d1, d2, d3 = side(a, b), side(b, c), side(c, a)
    return (d1 >= 0 and d2 >= 0 and d3 >= 0) or (d1 <= 0 and d2 <= 0 and d3 <= 0)


def box(px, py, x0, y0, x1, y1, r=0.0):
    """Axis-aligned box with corner radius r."""
    cx, cy = min(max(px, x0 + r), x1 - r), min(max(py, y0 + r), y1 - r)
    return math.hypot(px - cx, py - cy) <= r if r > 0 else (x0 <= px <= x1 and y0 <= py <= y1)


def ellipse_ring(px, py, cx, cy, rx, ry, t):
    outer = ((px - cx) / (rx + t)) ** 2 + ((py - cy) / (ry + t)) ** 2 <= 1.0
    inner = ((px - cx) / (rx - t)) ** 2 + ((py - cy) / (ry - t)) ** 2 <= 1.0
    return outer and not inner


def arrowhead(px, py, ex, ey, ang_deg, length, half_width):
    """A filled arrowhead whose base is centred on (ex, ey), pointing along ang_deg."""
    a = math.radians(ang_deg)
    tx, ty = math.cos(a), math.sin(a)
    nx, ny = -ty, tx
    tip = (ex + length * tx, ey + length * ty)
    return triangle(px, py, tip,
                    (ex + half_width * nx, ey + half_width * ny),
                    (ex - half_width * nx, ey - half_width * ny))


def rotated(fn, deg, cx=64.0, cy=64.0):
    """Draw fn turned by deg about (cx, cy)."""
    a = math.radians(-deg)
    ca, sa = math.cos(a), math.sin(a)

    def wrapped(px, py):
        dx, dy = px - cx, py - cy
        return fn(cx + dx * ca - dy * sa, cy + dx * sa + dy * ca)
    return wrapped


# -- Rhino ---------------------------------------------------------------------

def rhino_trail_slabs(px, py):
    """Three trail slabs in a row, each larger than the last - Mass grows the slab."""
    return (box(px, py, 10, 50, 28, 78, 3)
            or box(px, py, 38, 40, 62, 88, 3.5)
            or box(px, py, 72, 26, 118, 102, 4))


def rhino_ramp_spool(px, py):
    """A ramp wedge with a wind-up arrow over it - Time spools the ramp faster."""
    if triangle(px, py, (10, 20), (118, 20), (118, 66)):
        return True
    cx, cy, r, t = 52, 78, 22, 5.5
    if arc(px, py, cx, cy, r, t, -10.0, 250.0):
        return True
    # Arrowhead at the -10 degree end, pointing clockwise (down the tangent).
    a = math.radians(-10.0)
    ex, ey = cx + r * math.cos(a), cy + r * math.sin(a)
    return arrowhead(px, py, ex, ey, -10.0 - 90.0, 14, 12)


# -- Serpent -------------------------------------------------------------------

def _sniper_round(px, py):
    if capsule(px, py, 44, 64, 84, 64, 8):                       # the case
        return True
    if triangle(px, py, (84, 56), (84, 72), (110, 64)):          # the ogive
        return True
    for y, x0 in ((64, 12), (52, 22), (76, 22)):                 # tracer streaks
        if capsule(px, py, x0, y, 34 if y == 64 else 36, y, 3):
            return True
    return False


serpent_sniper_shot = rotated(_sniper_round, 30.0)
serpent_sniper_shot.__doc__ = "One long round with its tracer streaks - the hitscan rifle."


def serpent_scope(px, py):
    """A reticle: ring, split crosshair, centre dot - the magnified view down the nose."""
    if ring(px, py, 64, 64, 40, 5):
        return True
    if circle(px, py, 64, 64, 4.5):
        return True
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        if capsule(px, py, 64 + 14 * dx, 64 + 14 * dy, 64 + 54 * dx, 64 + 54 * dy, 3.5):
            return True
    return False


# -- Scarab --------------------------------------------------------------------

def scarab_cavitation_blast(px, py):
    """A collapsing bubble throwing three shock arcs - the mantis-shrimp punch."""
    cx, cy = 34, 64
    if ring(px, py, cx, cy, 14, 5):
        return True
    for r, t in ((32, 5.0), (50, 4.5), (68, 4.0)):
        if arc(px, py, cx, cy, r, t, -48.0, 48.0):
            return True
    return False


def scarab_switch(px, py):
    """A ball about to thread a ring - the switch a ball has to pass through."""
    if ellipse_ring(px, py, 86, 64, 18, 44, 6.5):
        return True
    if circle(px, py, 38, 64, 14):
        return True
    for y, x0 in ((64, 6), (50, 12), (78, 12)):                  # motion lines
        if capsule(px, py, x0, y, 18, y, 3):
            return True
    return False


def scarab_ball_forge(px, py):
    """A ball with four crystals converging on it - the skimmer forges crystals into balls."""
    if circle(px, py, 64, 54, 30) and not circle(px, py, 52, 66, 9):   # ball + highlight
        return True
    for ang in (35.0, 75.0, 105.0, 145.0):
        a = math.radians(ang)
        dx, dy = 64 + 50 * math.cos(a), 54 + 50 * math.sin(a)
        if abs(px - dx) + abs(py - dy) <= 8.5:                   # a crystal (diamond)
            return True
    return False


def scarab_throttle(px, py):
    """A speed dial with its needle - Time raises the throttle ramp's top speed."""
    cx, cy = 64, 46
    if arc(px, py, cx, cy, 46, 6.5, -20.0, 200.0):
        return True
    for ang in (-20.0, 35.0, 90.0, 145.0, 200.0):                # ticks
        a = math.radians(ang)
        if capsule(px, py, cx + 30 * math.cos(a), cy + 30 * math.sin(a),
                   cx + 36 * math.cos(a), cy + 36 * math.sin(a), 2.5):
            return True
    if capsule(px, py, cx, cy, cx + 38 * math.cos(math.radians(30)),
               cy + 38 * math.sin(math.radians(30)), 4.5):       # the needle, near the top
        return True
    return circle(px, py, cx, cy, 9)


# hull -> {file name: coverage function}
HULLS = {
    "Rhino": {
        "Rhino_TrailSlabs-PLACEHOLDER.png": rhino_trail_slabs,
        "Rhino_RampSpool-PLACEHOLDER.png": rhino_ramp_spool,
    },
    "Serpent": {
        "Serpent_SniperShot-PLACEHOLDER.png": serpent_sniper_shot,
        "Serpent_Scope-PLACEHOLDER.png": serpent_scope,
    },
    "Scarab": {
        "Scarab_CavitationBlast-PLACEHOLDER.png": scarab_cavitation_blast,
        "Scarab_Switch-PLACEHOLDER.png": scarab_switch,
        "Scarab_BallForge-PLACEHOLDER.png": scarab_ball_forge,
        "Scarab_Throttle-PLACEHOLDER.png": scarab_throttle,
    },
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
                    mx = x + (sx + 0.5) / SS
                    my = SIZE - (y + (sy + 0.5) / SS)          # top-down -> y up
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


def coverage(png_fn) -> float:
    """Fraction of the canvas an icon covers (centre samples) - a blank or flooded icon is a bug."""
    n = sum(1 for y in range(SIZE) for x in range(SIZE) if png_fn(x + 0.5, y + 0.5))
    return n / (SIZE * SIZE)


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


def sprite_guid(name: str) -> str:
    """The guid a hull's placeholder is authored under - what the row author binds."""
    return guid_for(name)


def main() -> int:
    assert os.path.exists(os.path.join(ROOT, ICON_ROOT + ".meta")), (
        f"{ICON_ROOT}.meta is missing - run author_manta_icon_placeholders.py, which owns it")

    for hull, icons in HULLS.items():
        for name, fn in icons.items():
            c = coverage(fn)
            assert 0.06 <= c <= 0.6, f"{name}: coverage {c:.3f} - blank or flooded silhouette"

    writes = {}
    for hull, icons in HULLS.items():
        out_dir = f"{ICON_ROOT}/{hull}"
        writes[out_dir + ".meta"] = folder_meta(guid_for(f"{hull}.folder"))
        for name, fn in icons.items():
            writes[f"{out_dir}/{name}"] = render(fn)
            writes[f"{out_dir}/{name}.meta"] = sprite_meta(guid_for(name))

    drift = []
    for rel, want in writes.items():
        path = os.path.join(ROOT, rel)
        binary = isinstance(want, bytes)
        have = None
        if os.path.exists(path):
            with open(path, "rb" if binary else "r", **({} if binary else {"encoding": "utf-8"})) as f:
                have = f.read()
        if have != want:
            drift.append(rel)
            if not CHECK:
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "wb" if binary else "w",
                          **({} if binary else {"encoding": "utf-8", "newline": "\n"})) as f:
                    f.write(want)

    if CHECK:
        if drift:
            print("DRIFT:\n  " + "\n  ".join(drift))
            return 1
        print("check clean")
        return 0
    print(f"wrote {len(drift)} file(s)")
    for hull, icons in HULLS.items():
        for name in icons:
            print(f"  {hull:8s} {name}: guid {guid_for(name)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
