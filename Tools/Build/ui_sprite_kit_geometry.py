#!/usr/bin/env python3
"""Geometry + PNG codec for the Cosmic Shore UI 9-slice sprite kit (T7).

Pure standard library on purpose: this is asset-authoring code that has to run
identically on a build machine, in CI and on a designer's laptop, and it must be
able to prove what it wrote without a Unity editor in the loop.

Coordinate system throughout: pixel space, x to the right, y DOWN from the top
-left corner, matching PNG row order.  A Unity sprite border is
``{x=left, y=bottom, z=right, w=top}``.

The shape language is Style Foundation v0.3.1 Sec.5: a *corner sliver* is a 45
degree diagonal cut on TWO OPPOSITE corners, flippable.  It is not a rounded
rect and it is not a single chamfer (Sec.5 corrects v0.1/v0.2 on exactly that
point, and the shipped `Button_Flat_*.png` art is the superseded single-chamfer
shape).
"""

from __future__ import annotations

import math
import struct
import zlib
from typing import Iterable, Sequence

# --------------------------------------------------------------------------
# Convex geometry
# --------------------------------------------------------------------------

# Coverage is computed EXACTLY - the pixel square is clipped against the convex
# region and the remaining area is the alpha.  An earlier supersampled version
# was translation-DEPENDENT: a 45 degree edge passes through sample centres, so
# whether an on-line sample counted as inside was decided by float noise, and
# the same sliver rasterised at x=54 and at x=118 differed by 1-2/255.  That is
# invisible on screen and fatal to an exactness proof, so it is gone.

#: Coverage is snapped to this many decimals before scaling to 8 bits, which
#: puts the snap grid far above double-precision noise (~1e-13 here) and far
#: below one alpha step (1/255 ~ 4e-3).  Without it, a true coverage of exactly
#: 0.5 lands on the 127.5 rounding boundary and wobbles with coordinate
#: magnitude.
COVERAGE_SNAP = 9


class Convex:
    """A convex region as an intersection of half planes ``n . p <= d``.

    Normals are unit length, which is what makes :meth:`erode` a plain
    subtraction on ``d`` and therefore what makes the outline variants exact.
    """

    __slots__ = ("planes",)

    def __init__(self, planes: Sequence[tuple[float, float, float]]):
        self.planes = tuple(planes)

    @classmethod
    def from_polygon(cls, verts: Sequence[tuple[float, float]]) -> "Convex":
        """Build from a convex polygon's vertices, in either winding order.

        Coincident vertices are dropped rather than rejected: a shape reaching
        its minimum legal size legitimately degenerates (a hexagon at exactly
        twice its point run is a rhombus), and the verifier tests exactly that
        size.
        """
        pts: list[tuple[float, float]] = []
        for v in verts:
            if not pts or (abs(v[0] - pts[-1][0]) > 1e-9
                           or abs(v[1] - pts[-1][1]) > 1e-9):
                pts.append((float(v[0]), float(v[1])))
        if len(pts) > 1 and (abs(pts[0][0] - pts[-1][0]) < 1e-9
                             and abs(pts[0][1] - pts[-1][1]) < 1e-9):
            pts.pop()
        if len(pts) < 3:
            raise ValueError(f"degenerate polygon: {verts}")

        n = len(pts)
        cx = sum(p[0] for p in pts) / n
        cy = sum(p[1] for p in pts) / n
        planes = []
        for i in range(n):
            ax, ay = pts[i]
            bx, by = pts[(i + 1) % n]
            ex, ey = bx - ax, by - ay
            length = math.hypot(ex, ey)
            # Either perpendicular works; pick the one the centroid is behind.
            nx, ny = ey / length, -ex / length
            d = nx * ax + ny * ay
            if nx * cx + ny * cy > d:
                nx, ny, d = -nx, -ny, -d
            planes.append((nx, ny, d))
        return cls(planes)

    def contains(self, x: float, y: float) -> bool:
        for nx, ny, d in self.planes:
            if nx * x + ny * y > d:
                return False
        return True

    def erode(self, t: float) -> "Convex":
        """Offset every edge inward by ``t`` pixels."""
        return Convex([(nx, ny, d - t) for nx, ny, d in self.planes])

    def row_span(self, py: int) -> tuple[int, int] | None:
        """Columns that the row band ``[py, py+1]`` can possibly touch."""
        lo, hi = -1e30, 1e30
        for nx, ny, d in self.planes:
            # Worst case over the row band, so the span is conservative.
            rhs = d - min(ny * py, ny * (py + 1))
            if nx > 1e-12:
                hi = min(hi, rhs / nx)
            elif nx < -1e-12:
                lo = max(lo, rhs / nx)
            elif rhs < 0:
                return None
        if hi < lo:
            return None
        return int(math.floor(lo)) - 1, int(math.ceil(hi)) + 1

    def coverage(self, px: int, py: int) -> float:
        """Exact fraction of pixel ``(px, py)`` inside the region."""
        poly = [(float(px), float(py)), (px + 1.0, float(py)),
                (px + 1.0, py + 1.0), (float(px), py + 1.0)]
        for nx, ny, d in self.planes:
            if not poly:
                return 0.0
            out: list[tuple[float, float]] = []
            m = len(poly)
            for i in range(m):
                ax, ay = poly[i]
                bx, by = poly[(i + 1) % m]
                da = nx * ax + ny * ay - d
                db = nx * bx + ny * by - d
                if da <= 0.0:
                    out.append((ax, ay))
                if (da < 0.0 < db) or (db < 0.0 < da):
                    t = da / (da - db)
                    out.append((ax + t * (bx - ax), ay + t * (by - ay)))
            poly = out
        if len(poly) < 3:
            return 0.0
        area = 0.0
        for i in range(len(poly)):
            ax, ay = poly[i]
            bx, by = poly[(i + 1) % len(poly)]
            area += ax * by - bx * ay
        return min(1.0, max(0.0, abs(area) * 0.5))


# --------------------------------------------------------------------------
# Shape constructors
# --------------------------------------------------------------------------

# Corner keys for the sliver.
TL, TR, BR, BL = "TL", "TR", "BR", "BL"

#: The two legal sliver orientations.  ``default`` matches the studio art's
#: unflipped naming (``Button_Flat_White`` cuts the top-right corner); the
#: opposite corner is the Sec.5 correction that art predates.
ORIENT_DEFAULT = (TR, BL)
ORIENT_FLIPPED = (TL, BR)


def slivered_rect(w: float, h: float, sliver: float,
                  corners: Iterable[str]) -> Convex:
    """A ``w`` x ``h`` rect with a 45 degree cut of ``sliver`` px on ``corners``.

    Vertices are emitted in clockwise screen order so the polygon stays convex
    whichever pair of opposite corners is cut.
    """
    corners = set(corners)
    unknown = corners - {TL, TR, BR, BL}
    if unknown:
        raise ValueError(f"unknown corner(s) {sorted(unknown)}")
    if sliver * 2 > min(w, h):
        raise ValueError(f"sliver {sliver} does not fit in {w}x{h}")
    verts: list[tuple[float, float]] = []
    # top-left
    verts += [(0.0, sliver), (sliver, 0.0)] if TL in corners else [(0.0, 0.0)]
    # top-right
    verts += [(w - sliver, 0.0), (w, sliver)] if TR in corners else [(w, 0.0)]
    # bottom-right
    verts += [(w, h - sliver), (w - sliver, h)] if BR in corners else [(w, h)]
    # bottom-left
    verts += [(sliver, h), (0.0, h - sliver)] if BL in corners else [(0.0, h)]
    return Convex.from_polygon(verts)


def hexagon(w: float, h: float, point: float) -> Convex:
    """Flat-top hexagon: flat edges top and bottom, vertices left and right.

    ``point`` is the horizontal run of each end vertex.  The flat edges are what
    the 9-slice stretches, so the two points survive any width unchanged.
    """
    if point * 2 > w:
        raise ValueError(f"point {point} does not fit in width {w}")
    return Convex.from_polygon([
        (0.0, h / 2.0), (point, 0.0), (w - point, 0.0),
        (w, h / 2.0), (w - point, h), (point, h),
    ])


def parallelogram(w: float, h: float, slant: float) -> Convex:
    """Banner body: both ends slanted by ``slant`` px over the full height."""
    if slant * 2 > w:
        raise ValueError(f"slant {slant} does not fit in width {w}")
    return Convex.from_polygon([
        (slant, 0.0), (w, 0.0), (w - slant, h), (0.0, h),
    ])


def banner_cap(w: float, h: float, side: str) -> Convex:
    """The triangle the banner's slant cuts away, as a detached end cap.

    ``side='left'`` is the piece removed from the body's top-left; ``'right'``
    the piece removed from its bottom-right.  Together they tile back into the
    banner's bounding rect, which is what makes them read as the same object.
    """
    if side == "left":
        return Convex.from_polygon([(w, 0.0), (0.0, 0.0), (0.0, h)])
    if side == "right":
        return Convex.from_polygon([(0.0, h), (w, h), (w, 0.0)])
    raise ValueError(f"side must be 'left' or 'right', got {side!r}")


# --------------------------------------------------------------------------
# Rasterisation
# --------------------------------------------------------------------------

def rasterize(w: int, h: int, outer: Convex, stroke: float = 0.0
              ) -> list[list[int]]:
    """Return an ``h`` x ``w`` alpha grid, 0-255.

    ``stroke`` of 0 fills the region; a positive value draws only an inward
    outline of that thickness, computed as the exact difference of two
    coverages so the diagonal keeps its antialiasing on both edges.

    The result is invariant under integer translation of the shape, which is
    what lets the verifier demand a byte-exact match between a 9-sliced
    composition and a native raster.
    """
    inner = outer.erode(stroke) if stroke > 0 else None
    rows: list[list[int]] = []
    for y in range(h):
        row = [0] * w
        span = outer.row_span(y)
        if span is not None:
            x_lo = max(0, span[0])
            x_hi = min(w, span[1] + 1)
            for x in range(x_lo, x_hi):
                cov = outer.coverage(x, y)
                if inner is not None:
                    cov -= inner.coverage(x, y)
                if cov <= 0.0:
                    continue
                cov = round(cov, COVERAGE_SNAP)
                row[x] = min(255, int(math.floor(cov * 255.0 + 0.5)))
        rows.append(row)
    return rows


def encode_png(alpha: Sequence[Sequence[int]]) -> bytes:
    """Encode an alpha grid as an 8-bit RGBA PNG whose RGB is white everywhere.

    RGB stays 255 even where alpha is 0.  Transparent black would bleed dark
    fringes into the diagonal under bilinear filtering, which is precisely the
    edge the sliver is made of.
    """
    h = len(alpha)
    w = len(alpha[0])
    raw = bytearray()
    for row in alpha:
        raw.append(0)  # filter type 0 (None) - keeps the file byte-stable
        for a in row:
            raw += b"\xff\xff\xff" + bytes((a,))

    def chunk(tag: bytes, data: bytes) -> bytes:
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    # Fixed compression level so the bytes are reproducible for --check.
    idat = zlib.compress(bytes(raw), 9)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", idat) + chunk(b"IEND", b""))


def decode_png(blob: bytes) -> list[list[int]]:
    """Minimal decoder for the 8-bit RGBA PNGs this module writes.

    Exists so the verifier reads the SHIPPED file rather than re-deriving it
    from the same code that wrote it.
    """
    if blob[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a PNG")
    pos, w, h, idat = 8, 0, 0, bytearray()
    while pos < len(blob):
        (length,) = struct.unpack(">I", blob[pos:pos + 4])
        tag = blob[pos + 4:pos + 8]
        data = blob[pos + 8:pos + 8 + length]
        if tag == b"IHDR":
            w, h, depth, color = struct.unpack(">IIBB", data[:10])
            if depth != 8 or color != 6:
                raise ValueError(f"expected 8-bit RGBA, got depth={depth} color={color}")
        elif tag == b"IDAT":
            idat += data
        elif tag == b"IEND":
            break
        pos += 12 + length

    raw = zlib.decompress(bytes(idat))
    stride = w * 4
    out: list[list[int]] = []
    prev = bytearray(stride)
    p = 0
    for _ in range(h):
        ft = raw[p]
        line = bytearray(raw[p + 1:p + 1 + stride])
        p += 1 + stride
        for i in range(stride):
            a = line[i - 4] if i >= 4 else 0
            b = prev[i]
            c = prev[i - 4] if i >= 4 else 0
            if ft == 0:
                pass
            elif ft == 1:
                line[i] = (line[i] + a) & 0xFF
            elif ft == 2:
                line[i] = (line[i] + b) & 0xFF
            elif ft == 3:
                line[i] = (line[i] + (a + b) // 2) & 0xFF
            elif ft == 4:
                pp = a + b - c
                pa, pb, pc = abs(pp - a), abs(pp - b), abs(pp - c)
                pred = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pred) & 0xFF
            else:
                raise ValueError(f"bad PNG filter type {ft}")
        out.append([line[i * 4 + 3] for i in range(w)])
        prev = line
    return out


# --------------------------------------------------------------------------
# Border measurement
# --------------------------------------------------------------------------

def measure_border(alpha: Sequence[Sequence[int]], mode: str
                   ) -> tuple[int, int, int, int]:
    """Derive the smallest CORRECT 9-slice border for an alpha grid.

    A 9-slice is exact when every column it stretches horizontally is identical
    to its neighbours, and likewise every row it stretches vertically.  So the
    right inset is not a design number to be guessed at - it is the largest run
    of identical columns (and rows) around the middle, and this measures it.

    Guessing is what goes wrong: the nominal sliver is the correct inset for a
    filled shape, and *too small* for an outlined one, because eroding a
    polygon pushes the mitre where the diagonal meets the straight edge back
    past the sliver line by ``stroke * (sqrt(2) - 1)``.  Measured that way, a
    1 px frame on a 10 px sliver needs an 11 px inset.

    ``mode`` is ``'both'`` (insets on all four sides), ``'horizontal'`` (left
    and right only - the shape's authored height is fixed) or ``'none'``.
    Returns Unity's ``(left, bottom, right, top)``.
    """
    if mode == "none":
        return (0, 0, 0, 0)
    h, w = len(alpha), len(alpha[0])

    def run(count: int, same) -> tuple[int, int]:
        mid = count // 2
        lo = mid
        while lo > 0 and same(lo - 1, lo):
            lo -= 1
        hi = mid
        while hi < count - 1 and same(hi, hi + 1):
            hi += 1
        return lo, hi

    col_lo, col_hi = run(w, lambda a, b: all(alpha[y][a] == alpha[y][b]
                                             for y in range(h)))
    left, right = col_lo, w - (col_hi + 1)
    if mode == "horizontal":
        return (left, 0, right, 0)

    row_lo, row_hi = run(h, lambda a, b: list(alpha[a]) == list(alpha[b]))
    top, bottom = row_lo, h - (row_hi + 1)
    return (left, bottom, right, top)
