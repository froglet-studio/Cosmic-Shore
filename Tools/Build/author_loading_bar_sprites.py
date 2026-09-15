#!/usr/bin/env python3
"""
Author the loading bar's two sprites - the TRACK and the FILL of the connecting screen's
progress slider.

WHY SPRITES AT ALL: the default UGUI slider is a grey rectangle inside a grey rectangle, which
reads as a placeholder. A capsule with a lit fill reads as an instrument. Both are pure WHITE
with the shape in ALPHA, like every other tinted HUD sprite in the project, so the controller
paints them (a cool blue by default) without a second asset per colour.

BOTH ARE 9-SLICED. A progress bar is stretched to whatever width the panel gives it, and a
capsule stretched without a border turns its round caps into ellipses - the one thing that makes
a bar look cheap. The border is the corner radius exactly, so the caps are carried through
un-stretched and only the flat middle scales.

SHAPES
  track: the capsule at a low flat alpha, with a slightly brighter 1px rim, so the empty part of
         the bar is a visible channel rather than a hole.
  fill:  the same capsule at full alpha with a soft highlight along the upper third, so the bar
         reads as lit rather than as a flat block. The highlight is analytic (a raised cosine),
         so there is no banding to dither.

Usage:
    python3 Tools/Build/author_loading_bar_sprites.py            # write the PNGs + .meta
    python3 Tools/Build/author_loading_bar_sprites.py --check    # verify, non-zero on drift
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
OUT_DIR = os.path.join(REPO, "Assets", "_Graphics", "UI", "Loading")

W, H = 64, 24              # a short capsule; the border carries the caps, the middle stretches
RADIUS = H / 2.0           # a full capsule - radius is half the height
BORDER = int(math.ceil(RADIUS))

TRACK_BODY = 0.22          # the empty channel: present, never competing with the fill
TRACK_RIM = 0.42           # a touch brighter at the very edge so the channel has a lip
RIM_PX = 1.2

FILL_BODY = 0.90
FILL_HILITE = 1.00         # peak alpha along the upper third
HILITE_CENTRE = 0.32       # 0 = top edge, 1 = bottom edge
HILITE_WIDTH = 0.42


def guid_for(name):
    return hashlib.md5(f"CosmicShore/Loading/{name}".encode()).hexdigest()


def capsule_distance():
    """Signed distance to the capsule, in pixels. Negative inside."""
    x = np.arange(W) + 0.5
    y = np.arange(H) + 0.5
    X, Y = np.meshgrid(x, y)
    px = np.abs(X - W / 2.0) - (W / 2.0 - RADIUS)
    py = np.abs(Y - H / 2.0) - (H / 2.0 - RADIUS)
    qx = np.maximum(px, 0.0)
    qy = np.maximum(py, 0.0)
    outside = np.sqrt(qx * qx + qy * qy)
    inside = np.minimum(np.maximum(px, py), 0.0)
    return outside + inside - RADIUS


def coverage(dist):
    """1 inside, 0 outside, antialiased over one pixel across the boundary."""
    return np.clip(0.5 - dist, 0.0, 1.0)


def render_track():
    d = capsule_distance()
    cov = coverage(d)
    # Rim: a band just inside the edge. `-d` is depth into the shape.
    depth = np.clip(-d, 0.0, None)
    rim = np.clip(1.0 - depth / RIM_PX, 0.0, 1.0)
    alpha = cov * (TRACK_BODY + (TRACK_RIM - TRACK_BODY) * rim)
    return to_rgba(alpha)


def render_fill():
    d = capsule_distance()
    cov = coverage(d)
    y = (np.arange(H) + 0.5) / H
    _, Y = np.meshgrid(np.arange(W), y)
    t = np.clip(np.abs(Y - HILITE_CENTRE) / HILITE_WIDTH, 0.0, 1.0)
    hilite = 0.5 + 0.5 * np.cos(np.pi * t)        # 1 at the centre line, 0 at the edges
    alpha = cov * (FILL_BODY + (FILL_HILITE - FILL_BODY) * hilite)
    return to_rgba(alpha)


def to_rgba(alpha):
    rgba = np.empty((H, W, 4), dtype=np.uint8)
    rgba[..., 0:3] = 255
    rgba[..., 3] = np.rint(np.clip(alpha, 0.0, 1.0) * 255.0).astype(np.uint8)
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

def build(name, rgba):
    a = rgba[..., 3]
    # The invariants the shape claims, asserted rather than eyeballed.
    assert a[0, 0] == 0 and a[-1, 0] == 0 and a[0, -1] == 0 and a[-1, -1] == 0, \
        f"{name}: the capsule's corners must be empty, or the 9-slice caps square off"
    assert a[H // 2, W // 2] > 0, f"{name}: the capsule must be solid through the middle"
    # A 9-slice only stretches the MIDDLE column, so every row of it must be constant across
    # the stretch region or the bar bands as it grows.
    mid = a[:, BORDER:W - BORDER]
    assert (mid.max(axis=1) == mid.min(axis=1)).all(), \
        f"{name}: the stretch region must be constant per row"

    meta = META.format(guid=guid_for(name), sprite_id=guid_for(name + "/sprite"))
    # 9-slice: the border is the corner radius exactly, so the round caps are never stretched.
    meta = meta.replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}",
                        f"spriteBorder: {{x: {BORDER}, y: 0, z: {BORDER}, w: 0}}")
    # A crisp UI capsule wants no mip chain and no compression artefacts on its edge.
    meta = meta.replace("enableMipMap: 1", "enableMipMap: 0")
    meta = meta.replace("textureCompression: 1", "textureCompression: 0")
    return encode_png(rgba), meta


SPRITES = (
    ("loading_bar_track", render_track),
    ("loading_bar_fill", render_fill),
)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    built = [(name, *build(name, render())) for name, render in SPRITES]
    folder_meta = FOLDER_META.format(guid=guid_for("__folder__")).encode()

    if args.check:
        drift = []
        for name, png, meta in built:
            p = os.path.join(OUT_DIR, name + ".png")
            for path, want in ((p, png), (p + ".meta", meta.encode())):
                have = open(path, "rb").read() if os.path.exists(path) else b""
                if have != want:
                    drift.append(os.path.basename(path))
        have = open(OUT_DIR + ".meta", "rb").read() if os.path.exists(OUT_DIR + ".meta") else b""
        if have != folder_meta:
            drift.append("Loading.meta")
        for d in drift:
            print("DRIFT", d)
        print("FAIL: re-run without --check" if drift else "OK: loading bar sprites match")
        return 1 if drift else 0

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "w", newline="\n") as f:
        f.write(FOLDER_META.format(guid=guid_for("__folder__")))
    for name, png, meta in built:
        p = os.path.join(OUT_DIR, name + ".png")
        open(p, "wb").write(png)
        with open(p + ".meta", "w", newline="\n") as f:
            f.write(meta)
        print(f"wrote {name}.png ({W}x{H}, border {BORDER})  guid={guid_for(name)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
