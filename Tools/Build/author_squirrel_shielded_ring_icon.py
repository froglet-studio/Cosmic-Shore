#!/usr/bin/env python3
"""
Author the Squirrel's OMNI CRYSTAL ability icon: a true cross-section of the shielded ring.

Collecting an omni crystal in a Squirrel fires `SquirrelVesselExplosionByCrystalEffect`, whose
one AOE prefab is `AOEShieldedRingSpawner` - a `SpawnableRings` laying ONE ring of
`prismsPerRing` prisms at `ringRadius`, each of `prismScale`, with `isShielded` set. So the
pilot leaves a ring of SHIELDED prisms behind them. The icon is that ring seen ENDWISE, the
exact orthographic cross-section, cut perpendicular to the ring axis - the sibling of
`author_squirrel_boost_ring_icon.py`, which does the same for the Boost Ring's DANGER prisms.

The pair is meant to read as a pair: same ring, same count, and the prisms rotated 45 degrees.
That rotation is not styling - it is what a shield IS. `PrismStateManager.ActivateShield`
engages the CIRCUMSCRIBING octahedron (`OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE` on the
box HALF-extents), and an octahedron's cross-section perpendicular to one axis is exactly the
box's square turned 45 degrees and grown to the octahedron's own reach. A shielded prism
therefore reads as a DIAMOND where a bare one reads as a SQUARE, and the icon says "shielded"
by drawing the armour rather than by being told to.

Every number is READ from the shipped asset or the shipped C#, never restated here, so the
icon cannot drift from the ability (CLAUDE.md: "One authored number per displayed quantity").
Retune the ring or the shield scale and `--check` fails until the icon is regenerated.

The cross-section follows BoostRingBuilder.LayRing exactly:

    angle_i  = i * 2*pi / prismsPerRing
    radial_i = (cos, sin, 0)
    pos_i    = radial_i * ringRadius
    rot_i    = LookRotation(forward = +z, up = radial_i)

so, viewed down +z, each prism's local +y is radial and its local +x tangential. The shield's
own cross-section is |x| / h + |y| / h <= 1 in those axes, with

    h = CIRCUMSCRIBING_SCALE * 0.5 * prismScale.x

which is the same shape as a square of half-side h / sqrt(2) rotated 45 degrees - the form the
renderer writes it as, and the form this tool rasterises.

Output is a pure-white silhouette with an alpha channel, matching every other Squirrel ability
icon (all 148x148 pure white) - the HUD view tints it at runtime in the pilot's own domain's
SHIELDED base-face colour, so any colour baked in here would fight that tint.

Usage:
    python3 Tools/Build/author_squirrel_shielded_ring_icon.py            # write the PNG
    python3 Tools/Build/author_squirrel_shielded_ring_icon.py --check    # verify, non-zero on drift
    python3 Tools/Build/author_squirrel_shielded_ring_icon.py --ascii    # preview in the terminal
"""

import argparse
import math
import os
import re
import struct
import sys
import zlib

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# The prefab the crystal effect spawns, and the base prefab it is a variant of. A variant
# carries only its OVERRIDES, so the authored value of a field it does not override lives
# upstairs - reading either file alone gets a different (wrong) ring.
RING_PREFAB = os.path.join(REPO, "Assets/_Prefabs/Projectile/AOEShieldedRingSpawner.prefab")
BASE_PREFAB = os.path.join(REPO, "Assets/_Prefabs/Projectile/AOERingSpawner.prefab")
OCTA_CS = os.path.join(REPO, "Assets/_Scripts/Utility/OctahedronMeshGenerator.cs")
ICON_PNG = os.path.join(
    REPO, "Assets/_Graphics/Design Assests/HUD UI/Squirrel/ShieldedRingCrossSectionIcon.png")

SIZE = 148          # px, matching every other Squirrel ability icon
FILL = 0.94         # fraction of the half-canvas the figure's outermost point reaches


# ---------------------------------------------------------------- authored input

def read_octahedron_scale(path):
    """The shield's reach, in multiples of the prism's HALF-extents, out of the shipped C#."""
    with open(path, "r", encoding="utf-8") as fh:
        m = re.search(r"CIRCUMSCRIBING_SCALE\s*=\s*([0-9.]+)f", fh.read())
    if not m:
        raise SystemExit("[shielded-ring-icon] CIRCUMSCRIBING_SCALE not found in %s" % path)
    return float(m.group(1))


def read_ring_spec(variant_path, base_path):
    """prismsPerRing / ringRadius / prismScale.x, taking the variant's override where it has one."""
    with open(base_path, "r", encoding="utf-8") as fh:
        base = fh.read()
    with open(variant_path, "r", encoding="utf-8") as fh:
        variant = fh.read()

    # The variant's overrides, as a flat propertyPath -> value map.
    overrides = dict(re.findall(r"propertyPath: (\S+)\n\s*value: (\S*)\n", variant))

    def field(name, cast, base_pattern=None):
        if name in overrides and overrides[name] != "":
            return cast(overrides[name])
        pat = base_pattern or (r"^\s*%s:\s*(-?[0-9.]+)\s*$" % re.escape(name))
        m = re.search(pat, base, re.M)
        if not m:
            raise SystemExit("[shielded-ring-icon] '%s' not found in %s"
                             % (name, os.path.relpath(base_path, REPO)))
        return cast(m.group(1))

    prisms = max(3, field("prismsPerRing", lambda s: int(float(s))))
    radius = field("ringRadius", float)
    # prismScale is a Vector3; the CROSS-SECTION is x by y and the long axis is z, so the
    # cross-section is square whenever x == y, which every shipped ring authors.
    sx = field("prismScale.x", float,
               r"^\s*prismScale: \{x: (-?[0-9.]+), y: -?[0-9.]+, z: -?[0-9.]+\}\s*$")
    sy = field("prismScale.y", float,
               r"^\s*prismScale: \{x: -?[0-9.]+, y: (-?[0-9.]+), z: -?[0-9.]+\}\s*$")

    shielded = field("isShielded", lambda s: int(float(s)))
    if shielded != 1:
        raise SystemExit("[shielded-ring-icon] FAIL: %s no longer lays SHIELDED prisms "
                         "(isShielded=%d). This icon draws the shield's own cross-section, so it "
                         "would be describing an ability that does not exist."
                         % (os.path.relpath(variant_path, REPO), shielded))

    return prisms, radius, sx, sy


# ---------------------------------------------------------------- geometry

def shield_prisms(prisms, radius, sx, sy, shield_scale):
    """
    One oriented square per shielded prism, mirroring BoostRingBuilder.LayRing and then
    ActivateShield. Each entry is (centre, axis_u, axis_v, half_u, half_v).

    The bare prism's cross-section is the box: axes (tangential, radial), half-extents
    (sx/2, sy/2). Its SHIELD is the circumscribing octahedron, whose cross-section is the
    diamond |x|/(k*sx/2) + |y|/(k*sy/2) <= 1 in those same axes - identical to a box of
    half-extents (k*sx/2, k*sy/2) / sqrt(2) with its axes rotated 45 degrees. Rasterising the
    rotated box rather than the diamond inequality is what makes "each square turned 45
    degrees" literally true of the emitted figure and lets one SDF serve both icons.
    """
    hx = shield_scale * 0.5 * sx / math.sqrt(2.0)
    hy = shield_scale * 0.5 * sy / math.sqrt(2.0)
    out = []
    for i in range(prisms):
        angle = i * (2.0 * math.pi / prisms)
        radial = (math.cos(angle), math.sin(angle))            # prism local +y
        tangential = (math.sin(angle), -math.cos(angle))       # prism local +x
        centre = (radial[0] * radius, radial[1] * radius)
        # The 45 degree turn, in the prism's OWN frame: u = (t + r)/sqrt2, v = (r - t)/sqrt2.
        rt2 = math.sqrt(2.0)
        u = ((tangential[0] + radial[0]) / rt2, (tangential[1] + radial[1]) / rt2)
        v = ((radial[0] - tangential[0]) / rt2, (radial[1] - tangential[1]) / rt2)
        out.append((centre, u, v, hx, hy))
    return out


def box_sdf(px, py, prism):
    """Exact signed distance to one oriented box (negative inside)."""
    (cx, cy), (ux, uy), (vx, vy), hu, hv = prism
    dx, dy = px - cx, py - cy
    lu = abs(dx * ux + dy * uy) - hu
    lv = abs(dx * vx + dy * vy) - hv
    outside = math.hypot(max(lu, 0.0), max(lv, 0.0))
    inside = min(max(lu, lv), 0.0)
    return outside + inside


def min_separation(prisms):
    """Smallest gap between any two figures - proves the ring reads as N distinct prisms."""
    best = float("inf")
    for i in range(len(prisms)):
        for j in range(i + 1, len(prisms)):
            for a, b in ((i, j), (j, i)):
                (cx, cy), (ux, uy), (vx, vy), hu, hv = prisms[b]
                for t in range(64):
                    su = -hu + 2.0 * hu * t / 63.0
                    sv = -hv + 2.0 * hv * t / 63.0
                    for cu, cv in ((su, -hv), (su, hv), (-hu, sv), (hu, sv)):
                        px = cx + cu * ux + cv * vx
                        py = cy + cu * uy + cv * vy
                        best = min(best, box_sdf(px, py, prisms[a]))
    return best


def extent_of(prisms):
    """Distance from the ring centre to the outermost point of the outermost figure."""
    far = 0.0
    for (cx, cy), (ux, uy), (vx, vy), hu, hv in prisms:
        for cu, cv in ((-hu, -hv), (-hu, hv), (hu, -hv), (hu, hv)):
            far = max(far, math.hypot(cx + cu * ux + cv * vx, cy + cu * uy + cv * vy))
    return far


# ---------------------------------------------------------------- raster

def render(prisms_geom, size=SIZE):
    extent = extent_of(prisms_geom)
    scale = (size * 0.5 * FILL) / extent          # px per world unit
    half_px = size * 0.5
    inv = 1.0 / scale                              # world units per px

    px_buf = bytearray(size * size * 4)
    # Analytic 1px-wide edge: alpha = clamp(0.5 - d/pixel, 0, 1) on the union SDF.
    for y in range(size):
        wy = (half_px - (y + 0.5)) * inv           # +y world is UP; image rows run downward
        for x in range(size):
            wx = ((x + 0.5) - half_px) * inv
            d = min(box_sdf(wx, wy, p) for p in prisms_geom)
            a = 0.5 - d * scale
            if a <= 0.0:
                continue
            if a > 1.0:
                a = 1.0
            o = (y * size + x) * 4
            px_buf[o] = 255
            px_buf[o + 1] = 255
            px_buf[o + 2] = 255
            px_buf[o + 3] = int(round(a * 255.0))
    return px_buf, scale


def build_png(size, px):
    """Encode the RGBA buffer as PNG bytes. Never touches disk - --check compares in memory,
    because writing a scratch file into Assets/ churns Unity's asset database and strands a
    stray file if the run dies before cleaning up."""
    raw = bytearray()
    stride = size * 4
    for y in range(size):
        raw.append(0)                       # filter: none
        raw += px[y * stride:(y + 1) * stride]
    comp = zlib.compress(bytes(raw), 9)

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    blob = b"\x89PNG\r\n\x1a\n"
    blob += chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
    blob += chunk(b"IDAT", comp)
    blob += chunk(b"IEND", b"")
    return blob


def write_png(path, blob):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as fh:
        fh.write(blob)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="verify the committed PNG matches the authored ability; do not write")
    ap.add_argument("--ascii", action="store_true", help="print an ASCII preview")
    args = ap.parse_args()

    shield_scale = read_octahedron_scale(OCTA_CS)
    prisms, radius, sx, sy = read_ring_spec(RING_PREFAB, BASE_PREFAB)
    geom = shield_prisms(prisms, radius, sx, sy, shield_scale)
    px, scale = render(geom)

    gap = min_separation(geom)
    coverage = sum(px[i * 4 + 3] for i in range(SIZE * SIZE)) / (255.0 * SIZE * SIZE)

    print("[shielded-ring-icon] authored ring: prisms=%d radius=%g prismScale=(%g, %g)"
          % (prisms, radius, sx, sy))
    print("[shielded-ring-icon] shield reach: %g x half-extents -> diamond half-diagonal %g"
          % (shield_scale, shield_scale * 0.5 * sx))
    print("[shielded-ring-icon] min gap between shields: %.3f world units (%.2f px)"
          % (gap, gap * scale))
    print("[shielded-ring-icon] ink coverage: %.1f%%" % (coverage * 100.0))

    if gap <= 0.0:
        raise SystemExit("[shielded-ring-icon] FAIL: shields overlap - the ring would read as a "
                         "solid annulus, not as %d shielded prisms. Note this is also true IN "
                         "GAME: two octahedra that interpenetrate on screen are the fit "
                         "fit_shield_clearance.py exists to prevent." % prisms)

    if args.ascii:
        ramp = " .:-=+*#%@"
        for y in range(0, SIZE, 3):
            print("".join(ramp[min(9, px[(y * SIZE + x) * 4 + 3] * 10 // 256)]
                          for x in range(0, SIZE, 2)))

    if args.check:
        if not os.path.exists(ICON_PNG):
            raise SystemExit("[shielded-ring-icon] FAIL: %s missing." % ICON_PNG)
        with open(ICON_PNG, "rb") as fh:
            have = fh.read()
        if have != build_png(SIZE, px):
            raise SystemExit("[shielded-ring-icon] FAIL: %s is stale - the ability or the shield "
                             "scale was retuned. Re-run without --check."
                             % os.path.relpath(ICON_PNG, REPO))
        print("[shielded-ring-icon] OK: icon matches the authored ability.")
        return

    write_png(ICON_PNG, build_png(SIZE, px))
    print("[shielded-ring-icon] wrote %s" % os.path.relpath(ICON_PNG, REPO))


if __name__ == "__main__":
    main()
