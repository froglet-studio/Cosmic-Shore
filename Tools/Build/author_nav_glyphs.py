#!/usr/bin/env python3
"""
Author the top nav's GLYPH sprites (five cropped from the old nav, plus the drawn CODEX) - the old Hangar / Ark / Home / Port / Profile icons
without their baked-in words - so the style-B nav can sit each icon BESIDE its authored label.

WHERE THEY COME FROM: the pre-redesign nav (Menu_Main before 99ef3ccd9) drew each tab as one
sprite with the icon ABOVE the word, baked together (`Design Assests/Menu_Main/R_Menu_Main/
HomeScreen/*.png`, about 88x120). The player-facing word now lives in a serialized label (the
LOCKED text rule), so only the icon is wanted. Each source is cropped to the rows above the first
fully-empty row after the glyph starts - the gap between the icon and its word - then trimmed to
its alpha bounds and padded to a square so every tab's icon lands on the same footprint.

Pure WHITE with the shape in ALPHA, like every other tinted kit sprite: the nav paints them
(active / inactive) from the theme, so one asset serves both states. The ACTIVE source art is
used because it is the crisper, fully-lit drawing; the inactive art is the same glyph in grey.

Usage:
    python3 Tools/Build/author_nav_glyphs.py              # write the PNGs + .meta
    python3 Tools/Build/author_nav_glyphs.py --check      # verify, non-zero on drift
    python3 Tools/Build/author_nav_glyphs.py --self-test  # prove the word is cropped away
"""

import argparse
import hashlib
import os
import struct
import sys
import zlib

import numpy as np
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC_DIR = os.path.join(REPO, "Assets", "_Graphics", "Design Assests", "Menu_Main", "R_Menu_Main", "HomeScreen")
OUT_DIR = os.path.join(REPO, "Assets", "_Graphics", "UI", "NavGlyphs")

# tab key -> the lit (active) source sprite of the old nav
SOURCES = (
    ("hangar", "hangar (1).png"),
    ("ark", "store (1).png"),
    ("home", "home.png"),
    ("port", "port (2).png"),
    ("profile", "arcade (2).png"),      # despite the name, this is the lit Profile art
)
ALPHA_ON = 20          # a row counts as inked above this alpha
PAD = 2                # transparent margin so bilinear filtering never clips the edge


def guid_for(name):
    return hashlib.md5(f"CosmicShore/NavGlyphs/{name}".encode()).hexdigest()


def load_alpha(path):
    return np.asarray(Image.open(path).convert("RGBA"))[..., 3]


def glyph_rows(alpha):
    """(top, bottom) rows of the glyph: from the first inked row to the first empty row after it."""
    inked = alpha.max(axis=1) > ALPHA_ON
    top = int(np.argmax(inked))
    bottom = top
    while bottom < len(inked) and inked[bottom]:
        bottom += 1
    return top, bottom


def render(alpha):
    top, bottom = glyph_rows(alpha)
    g = alpha[top:bottom]
    cols = np.where(g.max(axis=0) > 0)[0]
    rows = np.where(g.max(axis=1) > 0)[0]
    g = g[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]
    side = max(g.shape) + PAD * 2
    out = np.zeros((side, side), dtype=np.uint8)
    y0, x0 = (side - g.shape[0]) // 2, (side - g.shape[1]) // 2
    out[y0:y0 + g.shape[0], x0:x0 + g.shape[1]] = g
    rgba = np.empty((side, side, 4), dtype=np.uint8)
    rgba[..., 0:3] = 255
    rgba[..., 3] = out
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
    enableMipMap: 0
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
  maxTextureSize: 128
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
    maxTextureSize: 128
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
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


def codex_alpha(size=70, ss=8):
    """The CODEX glyph (Docs/MENU_REDESIGN_B.md section 84; Shombith, 2026-10-26: "Codex ... invent an icon it will be a
    beastiary or like pokedex for showing elements, ethirions and ecology"): an open book, solid like the other tabs,
    an ethirion's diamond cut out of the left page and a leaf (ecology) out of the right. Drawn, not cropped - the old
    nav had no codex art."""
    from PIL import ImageDraw
    n = size * ss
    im = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(im)
    u = n / 70.0
    P = lambda pts: [(x * u, y * u) for x, y in pts]
    # the two pages: each rises to its outer top corner, with a dip at the spine
    d.polygon(P([(3, 16), (20, 12), (33, 17), (35, 19), (35, 60), (33, 58), (20, 54), (3, 58)]), fill=255)
    d.polygon(P([(67, 16), (50, 12), (37, 17), (35, 19), (35, 60), (37, 58), (50, 54), (67, 58)]), fill=255)
    # the spine: a thin gap between the pages, and the cover's lip under them
    d.polygon(P([(34.2, 19), (35.8, 19), (35.8, 60), (34.2, 60)]), fill=0)
    d.polygon(P([(3, 61), (20, 57.5), (35, 63), (50, 57.5), (67, 61), (67, 64), (50, 60.5), (35, 66), (20, 60.5), (3, 64)]), fill=255)
    # the ethirion (a crystal's diamond) on the left page
    d.polygon(P([(19, 23), (26, 34), (19, 46), (12, 34)]), fill=0)
    # the leaf (ecology) on the right page, with its vein left solid
    import math
    (ax, ay), (bx, by), w = (42.5, 46), (59, 24), 7.0       # the leaf: pointed at both ends, along a diagonal
    lx, ly = bx - ax, by - ay
    ln = math.hypot(lx, ly)
    nx, ny = -ly / ln, lx / ln
    edge = lambda sgn: [(ax + lx * t + sgn * nx * w * math.sin(math.pi * t) ** 0.8,
                         ay + ly * t + sgn * ny * w * math.sin(math.pi * t) ** 0.8) for t in [i / 24 for i in range(25)]]
    d.polygon(P(edge(1) + edge(-1)[::-1]), fill=0)
    d.line(P([(ax + lx * 0.12, ay + ly * 0.12), (ax + lx * 0.8, ay + ly * 0.8)]), fill=255, width=max(1, int(1.6 * u)))
    a = np.asarray(im.resize((size, size), Image.LANCZOS))
    return a


def render_drawn(alpha):
    side = alpha.shape[0] + PAD * 2
    out = np.zeros((side, side), dtype=np.uint8)
    out[PAD:PAD + alpha.shape[0], PAD:PAD + alpha.shape[1]] = alpha
    rgba = np.empty((side, side, 4), dtype=np.uint8)
    rgba[..., 0:3] = 255
    rgba[..., 3] = out
    return rgba


def build_all():
    out = []
    for key, src in SOURCES:
        rgba = render(load_alpha(os.path.join(SRC_DIR, src)))
        name = f"nav_{key}"
        out.append((name, encode_png(rgba), META.format(guid=guid_for(name), sprite_id=guid_for(name + "/sprite"))))
    rgba = render_drawn(codex_alpha())
    out.append(("nav_codex", encode_png(rgba), META.format(guid=guid_for("nav_codex"), sprite_id=guid_for("nav_codex/sprite"))))
    return out


def self_test():
    failures = []
    for key, src in SOURCES:
        alpha = load_alpha(os.path.join(SRC_DIR, src))
        top, bottom = glyph_rows(alpha)
        inked = np.where(alpha.max(axis=1) > ALPHA_ON)[0]
        # the word sits below the glyph: there must be ink after the crop, or nothing was cut away
        if not (inked[-1] > bottom + 4):
            failures.append(f"{key}: no word found below the glyph (crop rows {top}-{bottom})")
        if bottom - top < 30:
            failures.append(f"{key}: glyph too short ({bottom - top}px) - the gap search stopped early")
        g = render(alpha)
        if g.shape[0] != g.shape[1] or g[..., 3].max() == 0:
            failures.append(f"{key}: output is not a filled square")
    codex = render_drawn(codex_alpha())
    if codex.shape[:2] != (74, 74) or (codex[..., 3] > 128).mean() < 0.3:
        failures.append("codex: the drawn glyph is not a solid 74 px tab glyph like the others")
    # negative control: a sprite with NO gap must be reported as having no word to cut away
    fake = np.zeros((40, 40), dtype=np.uint8)
    fake[2:38, 5:35] = 255
    t, b = glyph_rows(fake)
    inked = np.where(fake.max(axis=1) > ALPHA_ON)[0]
    if inked[-1] > b + 4:
        failures.append("negative control: a gapless sprite was read as glyph + word")
    for f in failures:
        print("FAIL", f)
    print("FAIL: self-test" if failures else f"OK: self-test ({len(SOURCES)} glyphs + negative control)")
    return 1 if failures else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    built = build_all()
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
            drift.append("NavGlyphs.meta")
        for d in drift:
            print("DRIFT", d)
        print("FAIL: re-run without --check" if drift else "OK: nav glyphs match")
        return 1 if drift else 0
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "wb") as f:
        f.write(folder_meta)
    for name, png, meta in built:
        p = os.path.join(OUT_DIR, name + ".png")
        open(p, "wb").write(png)
        with open(p + ".meta", "w", newline="\n") as f:
            f.write(meta)
        print(f"wrote {name}.png  guid={guid_for(name)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
