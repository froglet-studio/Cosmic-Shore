#!/usr/bin/env python3
"""
Author the top bar's team-colour GLOW sprite.

One sprite, tinted per domain at runtime by DomainScorePanel and breathed with DOTween, so
each score column sits in its own team-coloured light instead of on a flat plate. A plate
draws a boundary the three-column arrangement already states; light does not - it says
"this column is Jade" without adding an edge.

SHAPE: brightest along the BOTTOM, falling away upward and inward from the sides, so the
glow reads as light rising off the column's accent strip rather than as a rectangle with
soft corners. Horizontally it is a raised-cosine window (zero at both edges, so a column of
glows never shows a seam); vertically it is an exponential falloff from the bottom edge.
Both are analytic, so the sprite has no banding to dither and needs no supersampling.

Pure white with the shape in ALPHA, like every other tinted HUD sprite in the project.

Usage:
    python3 Tools/Build/author_topbar_glow.py            # write the PNG + .meta
    python3 Tools/Build/author_topbar_glow.py --check    # verify, non-zero on drift
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
OUT_DIR = os.path.join(REPO, "Assets", "_Graphics", "UI", "TopBar")
NAME = "topbar_domain_glow"

W, H = 256, 128          # the column is wider than it is tall; match that so the sprite
                         # is sampled near 1:1 rather than stretched
EDGE_SOFT = 0.30         # fraction of the width the side falloff occupies
RISE = 2.6               # vertical falloff rate; higher = light hugs the bottom edge
PEAK = 0.85              # alpha at the brightest pixel, so the tint has headroom to punch


def guid_for(name):
    return hashlib.md5(f"CosmicShore/TopBar/{name}".encode()).hexdigest()


def render():
    x = (np.arange(W) + 0.5) / W                 # 0..1 across
    y = (np.arange(H) + 0.5) / H                 # 0..1 down (0 = top)
    X, Y = np.meshgrid(x, y)

    # Horizontal: raised cosine over the outer EDGE_SOFT on each side, flat in the middle.
    # Zero exactly at both edges, so adjacent columns' glows never butt into a visible seam.
    t = np.clip(np.minimum(X, 1.0 - X) / EDGE_SOFT, 0.0, 1.0)
    horiz = 0.5 - 0.5 * np.cos(np.pi * t)

    # Vertical: exponential rise from the bottom edge, normalised so the bottom row is 1.
    # PNG row 0 is the TOP, so `up` must grow WITH the row index for the light to sit at the
    # bottom edge. Writing this the intuitive way round makes an upside-down sprite that looks
    # plausible in isolation and wrong the moment it sits above the accent strip.
    up = Y                                        # ~0 at top, ~1 at bottom
    vert = (np.exp(RISE * up) - 1.0) / (math.exp(RISE) - 1.0)

    alpha = np.clip(horiz * vert * PEAK, 0.0, 1.0)
    rgba = np.empty((H, W, 4), dtype=np.uint8)
    rgba[..., 0:3] = 255
    rgba[..., 3] = np.rint(alpha * 255.0).astype(np.uint8)
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
    mipMapsPreserveCoverage: 0
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
    rgba = render()
    a = rgba[..., 3]
    # The invariants the shape claims, asserted rather than eyeballed.
    assert a[:, 0].max() == 0 and a[:, -1].max() == 0, "glow must reach zero at both side edges"
    assert a[-1].max() > a[0].max(), "glow must be brightest at the BOTTOM"
    # Sampled at pixel CENTRES, so the brightest pixel sits a half-texel short of the edge and
    # lands a couple of counts under the authored peak. Assert the band, not the exact value -
    # an exact test here fails on correct output and teaches nothing.
    assert abs(int(a.max()) - round(PEAK * 255)) <= 4, f"peak alpha {a.max()} != authored {PEAK}"
    assert (np.diff(a[:, W // 2].astype(int)) >= 0).all(), "centre column must rise monotonically"
    return encode_png(rgba), META.format(guid=guid_for(NAME), sprite_id=guid_for(NAME + "/sprite"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    png, meta = build()
    p = os.path.join(OUT_DIR, NAME + ".png")

    if args.check:
        drift = []
        for path, want in ((p, png), (p + ".meta", meta.encode()),
                           (OUT_DIR + ".meta", FOLDER_META.format(guid=guid_for("__folder__")).encode())):
            have = open(path, "rb").read() if os.path.exists(path) else b""
            if have != want:
                drift.append(os.path.basename(path))
        for d in drift:
            print("DRIFT", d)
        print("FAIL: re-run without --check" if drift else "OK: top bar glow matches")
        return 1 if drift else 0

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "w", newline="\n") as f:
        f.write(FOLDER_META.format(guid=guid_for("__folder__")))
    open(p, "wb").write(png)
    with open(p + ".meta", "w", newline="\n") as f:
        f.write(meta)
    print(f"wrote {NAME}.png ({W}x{H})  guid={guid_for(NAME)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
