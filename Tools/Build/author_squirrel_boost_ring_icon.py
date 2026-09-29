#!/usr/bin/env python3
"""
Author the Squirrel's TIME ability icon: a true cross-section of the boost ring.

The Squirrel's Time ability is the Boost Ring (SquirrelTubeActionSO) - it lays a ring of
`segments` cube prisms of uniform world scale `prismScale` at `radius` around the flight
axis, and the pilot rockets through the hollow centre. The icon is that ring seen ENDWISE:
the exact orthographic cross-section, cut perpendicular to the tube axis.

The geometry is READ FROM THE AUTHORED ASSET, never restated here, so the icon cannot drift
from the ability (CLAUDE.md: "One authored number per displayed quantity"). Retune the
ability's segments/radius/prismScale and `--check` fails until the icon is regenerated.

The cross-section follows BoostRingBuilder.LayRing exactly:

    angle_i  = i * 2*pi / segments
    radial_i = (cos, sin, 0)
    pos_i    = radial_i * radius
    rot_i    = LookRotation(forward = +z, up = radial_i)

so, viewed down +z, each prism is a square of side `prismScale` centred at `radius`, with
its local +y along the radial and its local +x along the tangential - which is why the ring
alternates square-reading and diamond-reading prisms at 45 degree steps. That alternation is
not styling; it is what the ability actually builds.

Output is a pure-white silhouette with an alpha channel, matching the Squirrel's other
ability icons (all 148x148 pure white) - the HUD view tints it at runtime, so any colour
baked in here would fight the domain tint.

Usage:
    python3 Tools/Build/author_squirrel_boost_ring_icon.py            # write the PNG
    python3 Tools/Build/author_squirrel_boost_ring_icon.py --check    # verify, non-zero on drift
"""

import argparse
import math
import os
import re
import struct
import sys
import zlib

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

ACTION_ASSET = os.path.join(
    REPO, "Assets/_SO_Assets/VesselActions/Squirrel/SquirrelTubeAction.asset")
ICON_PNG = os.path.join(
    REPO, "Assets/_Graphics/Design Assests/HUD UI/Squirrel/BoostRingCrossSectionIcon.png")

SIZE = 148          # px, matching the Squirrel's other ability icons
FILL = 0.94         # fraction of the half-canvas the figure's outermost point reaches
SUPERSAMPLE = 4     # analytic AA is used for edges; this only guards against thin slivers


# ---------------------------------------------------------------- authored input

def read_ring_spec(path):
    """Pull segments / radius / prismScale straight out of the authored SO asset."""
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()

    def field(name, cast):
        m = re.search(r"^\s*%s:\s*(-?[0-9.]+)\s*$" % re.escape(name), text, re.M)
        if not m:
            raise SystemExit("[boost-ring-icon] '%s' not found in %s" % (name, path))
        return cast(m.group(1))

    segments = max(3, field("segments", lambda s: int(float(s))))
    radius = field("radius", float)
    prism_scale = field("prismScale", float)
    return segments, radius, prism_scale


# ---------------------------------------------------------------- geometry

def ring_prisms(segments, radius, prism_scale):
    """The cross-section: one oriented square per prism, mirroring BoostRingBuilder.LayRing."""
    half = prism_scale * 0.5
    out = []
    for i in range(segments):
        angle = i * (2.0 * math.pi / segments)
        radial = (math.cos(angle), math.sin(angle))            # prism local +y
        tangential = (math.sin(angle), -math.cos(angle))       # prism local +x
        centre = (radial[0] * radius, radial[1] * radius)
        out.append((centre, tangential, radial, half))
    return out


def box_sdf(px, py, prism):
    """Exact signed distance to one oriented square (negative inside)."""
    (cx, cy), (ux, uy), (vx, vy), half = prism
    dx, dy = px - cx, py - cy
    # into the prism's own axes
    lx = abs(dx * ux + dy * uy) - half
    ly = abs(dx * vx + dy * vy) - half
    outside = math.hypot(max(lx, 0.0), max(ly, 0.0))
    inside = min(max(lx, ly), 0.0)
    return outside + inside


def min_separation(prisms):
    """Smallest gap between any two squares - proves the ring reads as N distinct prisms."""
    best = float("inf")
    for i in range(len(prisms)):
        for j in range(i + 1, len(prisms)):
            # sample the boundary of j against i's SDF and vice versa
            for a, b in ((i, j), (j, i)):
                (cx, cy), (ux, uy), (vx, vy), half = prisms[b]
                for t in range(64):
                    s = -half + 2.0 * half * t / 63.0
                    for corner in ((s, -half), (s, half), (-half, s), (half, s)):
                        px = cx + corner[0] * ux + corner[1] * vx
                        py = cy + corner[0] * uy + corner[1] * vy
                        best = min(best, box_sdf(px, py, prisms[a]))
    return best


# ---------------------------------------------------------------- raster

def render(segments, radius, prism_scale, size=SIZE):
    prisms = ring_prisms(segments, radius, prism_scale)

    # Outermost reach = ring radius + the square's half-diagonal.
    extent = radius + prism_scale * 0.5 * math.sqrt(2.0)
    scale = (size * 0.5 * FILL) / extent          # px per world unit
    half_px = size * 0.5
    inv = 1.0 / scale                              # world units per px

    px_buf = bytearray(size * size * 4)
    # Analytic 1px-wide edge: alpha = clamp(0.5 - d/pixel, 0, 1) on the union SDF.
    for y in range(size):
        # +y world is UP; image rows run downward
        wy = (half_px - (y + 0.5)) * inv
        for x in range(size):
            wx = ((x + 0.5) - half_px) * inv
            d = min(box_sdf(wx, wy, p) for p in prisms)
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
    return px_buf, prisms


def build_png(size, px):
    """Encode the RGBA buffer as PNG bytes. Never touches disk - --check compares in
    memory, because writing a scratch file into Assets/ churns Unity's asset database
    and strands a stray file if the run dies before cleaning up."""
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

    segments, radius, prism_scale = read_ring_spec(ACTION_ASSET)
    px, prisms = render(segments, radius, prism_scale)

    gap = min_separation(prisms)
    coverage = sum(px[i * 4 + 3] for i in range(SIZE * SIZE)) / (255.0 * SIZE * SIZE)

    print("[boost-ring-icon] authored ring: segments=%d radius=%g prismScale=%g"
          % (segments, radius, prism_scale))
    print("[boost-ring-icon] min gap between prisms: %.3f world units (%.2f px)"
          % (gap, gap * (SIZE * 0.5 * FILL) / (radius + prism_scale * 0.5 * math.sqrt(2.0))))
    print("[boost-ring-icon] ink coverage: %.1f%%" % (coverage * 100.0))

    if gap <= 0.0:
        raise SystemExit("[boost-ring-icon] FAIL: prisms overlap - the ring would read as a "
                         "solid annulus, not as %d prisms." % segments)

    if args.ascii:
        ramp = " .:-=+*#%@"
        for y in range(0, SIZE, 3):
            print("".join(ramp[min(9, px[(y * SIZE + x) * 4 + 3] * 10 // 256)]
                          for x in range(0, SIZE, 2)))

    if args.check:
        if not os.path.exists(ICON_PNG):
            raise SystemExit("[boost-ring-icon] FAIL: %s missing." % ICON_PNG)
        with open(ICON_PNG, "rb") as fh:
            have = fh.read()
        if have != build_png(SIZE, px):
            raise SystemExit("[boost-ring-icon] FAIL: %s is stale - the ability was retuned. "
                             "Re-run without --check." % os.path.relpath(ICON_PNG, REPO))
        print("[boost-ring-icon] OK: icon matches the authored ability.")
        return

    write_png(ICON_PNG, build_png(SIZE, px))
    print("[boost-ring-icon] wrote %s" % os.path.relpath(ICON_PNG, REPO))


if __name__ == "__main__":
    main()
