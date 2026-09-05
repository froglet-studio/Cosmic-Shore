#!/usr/bin/env python3
"""
Author the OBJECTIVE ICON set - one glyph per ScoringMetric.

The icon is a property of the METRIC, not of the mode. `ScoringMetric` is already the
platform's single answer to "what is this mode scored on" (it drives the HUD readout, the
turn monitor's remaining count, the end condition and the scoreboard secondary), so keying
the icon off it means a new mode that picks an existing metric gets its objective icon for
free, and a new metric is the only thing that ever needs new art.

STYLE - matched to the in-game vessel-HUD ability icons (bullet/fire/missile/swap-weapon on
the Sparrow, the Squirrel's boost-ring cross-section) and to Docs/STYLE_FOUNDATION.md:

  * Line-weight monochrome. Pure white (255,255,255) with the shape in the ALPHA channel,
    because every consumer tints at runtime - a colour baked in here would fight the tint.
  * Angular. Zero corner radius anywhere (Style Foundation section 5); strokes are quads
    with butt caps and mitred joins, never round.
  * Form disambiguates before hue does (section 1.2): the three crystal metrics share one
    silhouette and are told apart by FILL - hollow (elemental family), hollow + centre mark
    (a specific element), solid (omni, the combined one). That reading survives being seen
    small, in peripheral vision, and by a player who cannot separate the tints.
  * 256x256 with a 24px margin, so a glyph never touches its own edge when a layout crops.

SAMPLING - a HARD 0/1 shape function evaluated on a 4x4 grid per pixel, so every soft edge
is real coverage rather than a feathered distance field. This is the same quality bar the
offline lamp icons were rebuilt to (measured there at 14.4x more accurate per edge pixel
than a fixed-width feather).

Usage:
    python3 Tools/Build/author_objective_icons.py            # write the PNGs + .meta files
    python3 Tools/Build/author_objective_icons.py --check    # verify, non-zero on drift
"""

import argparse
import hashlib
import math
import os
import struct
import sys
import zlib

import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(REPO, "Assets", "_Graphics", "UI", "Objectives")

SIZE = 256          # texture edge, px
SS = 4              # supersamples per axis
MARGIN = 24         # px of clear space the artwork never enters
W = 0.115           # default stroke weight, in normalised units (2 = full canvas)

# Deterministic guid, stable across runs and machines (asset-surgery skill section 3).
def guid_for(name):
    return hashlib.md5(f"CosmicShore/ObjectiveIcons/{name}".encode()).hexdigest()


# ---------------------------------------------------------------------------
# Drawing. Everything is expressed in a normalised space: x,y in [-1,1], y UP.
# A shape is a boolean mask over the supersample grid; shapes union with `|`.
# ---------------------------------------------------------------------------

# Shape space is [-1,1] on both axes and maps to the canvas INSIDE the margin, so a glyph
# authored to |coord| <= 1 is margin-clean by construction and the assert below is a check
# on the shapes rather than on this mapping.
ART = 1.0 - MARGIN / (SIZE * 0.5)                        # 0.8125 at 256px / 24px margin

_N = SIZE * SS
_lin = ((np.arange(_N) + 0.5) / _N * 2.0 - 1.0) / ART    # pixel centres, in shape units
GX, GY = np.meshgrid(_lin, -_lin)                        # -_lin => y up


def fill(pts):
    """Even-odd fill of a closed polygon given as [(x, y), ...]."""
    inside = np.zeros(GX.shape, dtype=bool)
    n = len(pts)
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        if y0 == y1:
            continue
        crosses = ((y0 > GY) != (y1 > GY))
        xint = x0 + (GY - y0) * (x1 - x0) / (y1 - y0)
        inside ^= crosses & (GX < xint)
    return inside


def _quad(a, b, w):
    """The rectangle covering segment a->b at width w (butt caps)."""
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    ln = math.hypot(dx, dy)
    if ln < 1e-9:
        return None
    nx, ny = -dy / ln * w * 0.5, dx / ln * w * 0.5
    return [(ax + nx, ay + ny), (bx + nx, by + ny), (bx - nx, by - ny), (ax - nx, ay - ny)]


def stroke(pts, w=W, closed=False):
    """Polyline stroke: butt-capped quads plus mitre wedges at the interior joints."""
    mask = np.zeros(GX.shape, dtype=bool)
    segs = list(zip(pts, pts[1:])) + ([(pts[-1], pts[0])] if closed else [])
    for a, b in segs:
        q = _quad(a, b, w)
        if q:
            mask |= fill(q)
    # Joins: a square of side w centred on each interior vertex, rotated to bisect the
    # corner. Fills the wedge a butt cap leaves open without rounding it.
    joints = pts[1:-1] if not closed else pts
    for jx, jy in joints:
        h = w * 0.5
        mask |= fill([(jx - h, jy - h), (jx + h, jy - h), (jx + h, jy + h), (jx - h, jy + h)])
    return mask


def ring(pts, w=W):
    return stroke(list(pts), w, closed=True)


def ngon(n, r, rot=0.0, cx=0.0, cy=0.0):
    return [(cx + r * math.cos(rot + i * 2 * math.pi / n),
             cy + r * math.sin(rot + i * 2 * math.pi / n)) for i in range(n)]


def scaled(pts, s, cx=0.0, cy=0.0):
    return [(cx + (x - cx) * s, cy + (y - cy) * s) for x, y in pts]


# ---------------------------------------------------------------------------
# The glyphs. One function per ScoringMetric member.
# ---------------------------------------------------------------------------

# The shared crystal silhouette: a brilliant cut - flat table, crown shoulders, pavilion to
# a culet. It is the faceted read every collectable in the game already wears, and it stays
# a gem when it is filled solid (which is what tells the omni apart from the other two).
CRYSTAL = [(-0.30, 0.86), (0.30, 0.86), (0.62, 0.30), (0.34, -0.50),
           (0.00, -0.92), (-0.34, -0.50), (-0.62, 0.30)]
GIRDLE_L, GIRDLE_R = (-0.62, 0.30), (0.62, 0.30)


def crystals():
    """Crystals - the shared silhouette, hollow, with its crown and pavilion facets."""
    m = ring(CRYSTAL)
    m |= stroke([GIRDLE_L, GIRDLE_R], w=W * 0.8)              # the girdle
    m |= stroke([(-0.30, 0.86), (-0.42, 0.30)], w=W * 0.7)    # crown facets
    m |= stroke([(0.30, 0.86), (0.42, 0.30)], w=W * 0.7)
    m |= stroke([(-0.24, 0.30), (0.00, -0.92)], w=W * 0.7)    # pavilion facets
    m |= stroke([(0.24, 0.30), (0.00, -0.92)], w=W * 0.7)
    return m


def omni_crystals():
    """OmniCrystals - the same silhouette SOLID. Combined = filled (form, not hue).

    The girdle is knocked back OUT of the fill so the crown/pavilion break survives: without
    it a solid gem reads as an undifferentiated blob once it is down at HUD size, and the
    whole point of the fill is that it stays recognisably the SAME crystal as the other two.
    """
    return fill(CRYSTAL) & ~stroke([GIRDLE_L, GIRDLE_R], w=W * 0.5)


def elemental_crystals():
    """ElementalCrystals - the silhouette carrying one element mark, the shared rhombus."""
    m = ring(CRYSTAL)
    m |= stroke([GIRDLE_L, GIRDLE_R], w=W * 0.8)
    m |= fill([(0.00, 0.14), (0.26, -0.22), (0.00, -0.58), (-0.26, -0.22)])
    return m


def jousts():
    """Jousts - two lances meeting head-on, with the impact spark."""
    m = stroke([(-0.92, 0.46), (-0.24, 0.00), (-0.92, -0.46)])
    m |= stroke([(0.92, 0.46), (0.24, 0.00), (0.92, -0.46)])
    for ang in (55, 125, 235, 305):
        a = math.radians(ang)
        m |= stroke([(0.30 * math.cos(a), 0.30 * math.sin(a)),
                     (0.70 * math.cos(a), 0.70 * math.sin(a))], w=W * 0.72)
    return m


def goals():
    """Goals - the platform's SWITCH: a ring, and the thing that threaded it."""
    m = ring(ngon(8, 0.86, rot=math.pi / 8), w=W)
    # A solid dart at the centre, mid-thread. Solid because an outline inside an outline
    # closes up at HUD size; the ring's own gap is what says "passing through".
    m |= fill([(0.46, 0.00), (-0.26, 0.42), (-0.08, 0.00), (-0.26, -0.42)])
    return m


# The prism as the trail lays it: a sheared box, long axis along flight.
PRISM = [(-0.90, 0.26), (0.90, 0.54), (0.90, -0.26), (-0.90, -0.54)]


def prisms_destroyed():
    """PrismsDestroyed - one prism split along a jagged crack, the halves thrown apart."""
    crack_l = [(0.06, 0.40), (-0.12, 0.12), (0.08, -0.12), (-0.10, -0.44)]
    crack_r = [(x + 0.16, y) for x, y in crack_l]
    left = fill([(-0.90, 0.26), crack_l[0], crack_l[1], crack_l[2], crack_l[3], (-0.90, -0.54)])
    right = fill([crack_r[0], (0.90, 0.54), (0.90, -0.26), crack_r[3], crack_r[2], crack_r[1]])
    m = left | right
    for pts in (((0.10, 0.72), (0.30, 0.92)),
                ((-0.34, -0.72), (-0.18, -0.90)),
                ((0.46, -0.66), (0.62, -0.84))):
        m |= stroke(list(pts), w=W * 0.8)
    return m


def prisms_remaining():
    """PrismsRemaining - a standing run of prism mass, intact."""
    m = ring(PRISM, w=W * 0.9)
    m |= stroke([(-0.30, 0.35), (-0.30, -0.45)], w=W * 0.72)
    m |= stroke([(0.30, 0.44), (0.30, -0.36)], w=W * 0.72)
    return m


def lifeforms_killed():
    """LifeformsKilled - an angular creature, struck. The fauna read plus the hunt."""
    body = fill([(0.92, 0.00), (0.16, 0.40), (-0.30, 0.28), (-0.30, -0.28), (0.16, -0.40)])
    tail = fill([(-0.36, 0.34), (-0.92, 0.58), (-0.64, 0.00), (-0.92, -0.58), (-0.36, -0.34)])
    # A gap between body and tail: fused, the two silhouettes read as one plain arrowhead.
    creature = (body | tail) & ~stroke([(-0.33, 0.62), (-0.33, -0.62)], w=W * 0.42)
    # The strike is a KNOCKOUT through the body plus two ticks beyond its silhouette, so it
    # stays legible against a solid glyph at HUD size where an overlaid line would vanish.
    slash = stroke([(-0.16, 0.92), (0.40, -0.92)], w=W * 1.5)
    m = (creature & ~slash)
    m |= stroke([(-0.16, 0.92), (-0.05, 0.66)], w=W * 0.8) & ~creature
    m |= stroke([(0.29, -0.66), (0.40, -0.92)], w=W * 0.8) & ~creature
    return m


def combat_points():
    """CombatPoints - a gunnery reticle: four brackets closing on a struck centre."""
    m = np.zeros(GX.shape, dtype=bool)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m |= stroke([(sx * 0.94, sy * 0.42), (sx * 0.94, sy * 0.94), (sx * 0.42, sy * 0.94)],
                        w=W * 0.9)
    m |= fill(ngon(4, 0.30, rot=math.pi / 2))
    return m


def volume_destroyed():
    """VolumeDestroyed - a solid MASSED shape with a bite taken out of it.

    Deliberately reads against prisms_destroyed rather than beside it: that glyph is one prism
    cracked in two (a COUNT - things), this one is a body of mass with a wedge missing (a
    QUANTITY - how much). The distinction is the whole reason the metric exists, and at HUD size
    "solid with a piece gone" survives where "two prisms instead of one" would not.
    """
    # A chunky hexagonal mass - a volume, not an outline. Sized to clear the 24px margin the
    # builder asserts on (|x|,|y| <= 0.8125 in this normalised space), chips included.
    body = fill(ngon(6, 0.74, rot=math.pi / 6))
    # The bite: a wedge cut out of the upper right, taken well past the silhouette so the
    # opening is unambiguous rather than a notch. Subtracted, so it may leave the canvas.
    bite = fill([(0.06, 0.06), (0.54, 1.30), (1.40, 0.34), (1.40, -0.24)])
    m = body & ~bite
    # Two chips thrown clear of the bite, so the missing volume reads as REMOVED rather than
    # as a glyph that was always this shape.
    m |= fill(ngon(3, 0.13, rot=0.5, cx=0.60, cy=0.60))
    m |= fill(ngon(3, 0.09, rot=1.9, cx=0.68, cy=0.16))
    return m


GLYPHS = [
    ("objective_crystals", crystals),
    ("objective_omni_crystals", omni_crystals),
    ("objective_elemental_crystals", elemental_crystals),
    ("objective_jousts", jousts),
    ("objective_goals", goals),
    ("objective_prisms_destroyed", prisms_destroyed),
    ("objective_prisms_remaining", prisms_remaining),
    ("objective_lifeforms_killed", lifeforms_killed),
    ("objective_combat_points", combat_points),
    ("objective_volume_destroyed", volume_destroyed),
]


# ---------------------------------------------------------------------------
# Render + PNG
# ---------------------------------------------------------------------------

def render(mask):
    """Box-downsample the supersample mask to per-pixel coverage, then to RGBA bytes."""
    keep = SIZE * SS
    m = mask[:keep, :keep].astype(np.float32)
    cov = m.reshape(SIZE, SS, SIZE, SS).mean(axis=(1, 3))
    alpha = np.clip(np.rint(cov * 255.0), 0, 255).astype(np.uint8)
    rgba = np.empty((SIZE, SIZE, 4), dtype=np.uint8)
    rgba[..., 0:3] = 255
    rgba[..., 3] = alpha
    return rgba


def encode_png(rgba):
    raw = b"".join(b"\x00" + rgba[y].tobytes() for y in range(rgba.shape[0]))

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", rgba.shape[1], rgba.shape[0], 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 1
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 256
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def build():
    """name -> (png bytes, meta text). Also validates the margin invariant."""
    out = {}
    for name, fn in GLYPHS:
        rgba = render(fn())
        a = rgba[..., 3]
        # The artwork must never enter the margin, or a cropping layout clips the glyph.
        assert a[:MARGIN].max() == 0 and a[-MARGIN:].max() == 0, f"{name}: artwork in v-margin"
        assert a[:, :MARGIN].max() == 0 and a[:, -MARGIN:].max() == 0, f"{name}: artwork in h-margin"
        # A glyph that covers nothing (or everything) is a bug in its shape function.
        ink = float((a > 0).mean())
        assert 0.04 < ink < 0.60, f"{name}: implausible ink coverage {ink:.3f}"
        g = guid_for(name)
        out[name] = (encode_png(rgba),
                     META.format(guid=g, sprite_id=guid_for(name + "/sprite")))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="verify on-disk output, non-zero on drift")
    args = ap.parse_args()

    built = build()
    folder_meta = FOLDER_META.format(guid=guid_for("__folder__"))
    drift = []

    if args.check:
        if not os.path.isdir(OUT_DIR):
            print(f"FAIL missing {OUT_DIR}")
            return 1
        got = open(OUT_DIR + ".meta", "rb").read() if os.path.exists(OUT_DIR + ".meta") else b""
        if got != folder_meta.encode():
            drift.append("Objectives.meta")
        for name, (png, meta) in built.items():
            for ext, want in ((".png", png), (".png.meta", meta.encode())):
                p = os.path.join(OUT_DIR, name + ext)
                have = open(p, "rb").read() if os.path.exists(p) else b""
                if have != want:
                    drift.append(name + ext)
        for d in drift:
            print(f"DRIFT {d}")
        print(("FAIL: %d file(s) differ - re-run without --check" % len(drift)) if drift
              else "OK: %d objective icons match" % len(built))
        return 1 if drift else 0

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "w", newline="\n") as f:
        f.write(folder_meta)
    for name, (png, meta) in built.items():
        with open(os.path.join(OUT_DIR, name + ".png"), "wb") as f:
            f.write(png)
        with open(os.path.join(OUT_DIR, name + ".png.meta"), "w", newline="\n") as f:
            f.write(meta)
        print(f"wrote {name}.png  guid={guid_for(name)}")
    print(f"\n{len(built)} icons -> {os.path.relpath(OUT_DIR, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
