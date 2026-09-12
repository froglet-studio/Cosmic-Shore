#!/usr/bin/env python3
"""
Author the three CUT-CORNER UI FRAMES the menu is built out of, first-party.

WHY: the three frames the project draws - the tab-button outline in the settings panel, and the
filled plate behind the party panel, the friends panel and the Authentication username field -
arrived in `Assets/Shift - Complete Sci-Fi UI/` with no licence document and no recoverable
purchase record, and that pack also ships a `Resources/` folder into the player whether or not
anything references it. It is three textures and one switch prefab, with zero first-party code
coupling, so replacing is cheaper than chasing a receipt.

WHAT THEY ARE: a 128x128 square with TWO DIAGONALLY-OPPOSITE corners cut at 45 deg - top-left
and bottom-right - drawn PURE WHITE with the shape in ALPHA, exactly like every other tinted
sprite in this project. Every one of the nine shipped usages draws them `type: Sliced` with a
runtime tint (`SO_ColorSet`-ish navies, and white on the tab buttons), so the colour is never in
the file and the 9-slice border is what has to be right.

    frame_cut_filled   the solid plate.                    border 26, alpha 1 inside
    frame_cut_outline  the same silhouette as a 6 px band. border 30, hollow

THE BORDER AND THE PPU ARE THE CONTRACT, and the PPU is why there are THREE files for two
images. The vendor shipped `Cut Frame Filled Big (200ppu).png` and `Cut Frame Filled Big
(300ppu).png` as BYTE-IDENTICAL PNGs differing only in their `.meta`, because a 9-sliced sprite
draws its border at `spriteBorder / (spritePPU / canvas.referencePixelsPerUnit)` - Menu_Main's
canvas is 240, so the same 26 px border renders 31.2 UI units at 200 PPU and 20.8 at 300. That
is a real difference on screen, so both survive here as two assets over one image:

    frame_cut_filled_200   PPU 200, border 26   <- ArcadeLobbyList (the party panel)
    frame_cut_filled_300   PPU 300, border 26   <- FriendListPanel, UsernameInputField
    frame_cut_outline_200  PPU 200, border 30   <- the settings panel's four tab buttons

GEOMETRY, SOLVED against the assets being replaced rather than eyeballed. Each chamfer is fitted
on its own corner by inverting the 45 deg coverage curve over a 0.002 px sweep, and the band
width likewise; a first pass read the alpha ramps by hand and put the bottom-right chamfer 3 px
out, which the fit caught. The two chamfers are NOT the same size (25.752 px top-left against
24.376 px bottom-right - the source art was drawn larger and downsampled), and both are
reproduced at their fitted values so the silhouette does not shift. Both sit inside their own
9-slice corner tile, which is what lets a 45 deg cut 9-slice at all - the same rule
`Tools/Build/author_toy_card_sprites.py` records.

MEASURED, `--verify-vendor` against the pack before it was removed:

    sprite                             coverage delta   pixels off by >0.25   max |alpha delta|
    Cut Frame Filled Big (200/300ppu)  -0.008 %         0 / 16384             0.022
    Cut Frame Big - 6px (200ppu)       -0.159 %         0 / 16384             0.103

    python3 Tools/Build/author_ui_frame_sprites.py                  # write the PNGs + .meta
    python3 Tools/Build/author_ui_frame_sprites.py --check          # fail if any drifted
    python3 Tools/Build/author_ui_frame_sprites.py --verify-vendor  # measure against Shift
                                                                    # (only while the pack exists)
    python3 Tools/Build/author_ui_frame_sprites.py --self-test      # prove --verify-vendor fails
                                                                    # on a frame that really differs
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
OUT_DIR = os.path.join(REPO, "Assets", "_Graphics", "UI", "Frames")
VENDOR_DIR = os.path.join(REPO, "Assets", "Shift - Complete Sci-Fi UI",
                          "Textures", "Border", "Cut")

SIZE = 128
# Chamfer lines in continuous texture coordinates, where pixel (i, j) covers [i, i+1] x [j, j+1]
# and j counts DOWN from the top row. Fitted from the shipped alpha ramps (the 45 deg coverage
# curve inverted at three alphas per corner, agreeing to ~0.01 px).
CHAMFER_TL = 25.752       # kept where x + y >= this
CHAMFER_BR = 24.376       # kept where (2 * SIZE - x - y) >= this
BAND_PX = 5.940           # the outline frame's width, fitted (the art is nominally "6px")
SUPERSAMPLE = 8


def guid_for(name):
    return hashlib.md5(f"CosmicShore/UIFrames/{name}".encode()).hexdigest()


def coverage(inner_inset=None):
    """Analytic alpha for the cut-corner silhouette, supersampled.

    `inner_inset` None -> the filled plate. A number -> a hollow band of that PERPENDICULAR
    width, produced as the silhouette minus the same silhouette offset inward by that distance.
    A perpendicular offset moves a straight edge by w and a 45 deg edge's x+y line by w*sqrt(2),
    which is not the same as insetting the coordinates - getting that wrong made the band 7.4 %
    too heavy on the first pass."""
    n = SUPERSAMPLE
    step = 1.0 / n
    off = step * 0.5
    coords = np.arange(SIZE * n, dtype=np.float64) * step + off
    xs = coords[None, :]
    ys = coords[:, None]

    def silhouette(lo, hi, tl, br):
        return ((xs >= lo) & (xs <= hi) & (ys >= lo) & (ys <= hi)
                & ((xs + ys) >= tl) & ((2.0 * SIZE - xs - ys) >= br))

    out = silhouette(0.0, float(SIZE), CHAMFER_TL, CHAMFER_BR)
    if inner_inset is not None:
        i = inner_inset
        d = i * math.sqrt(2.0)
        out = out & ~silhouette(i, SIZE - i, CHAMFER_TL + d, CHAMFER_BR + d)
    return out.reshape(SIZE, n, SIZE, n).mean(axis=(1, 3))


def render(alpha):
    rgba = np.full((SIZE, SIZE, 4), 255, dtype=np.uint8)
    rgba[..., 3] = np.clip(np.rint(alpha * 255.0), 0, 255).astype(np.uint8)
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
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: {b}, y: {b}, z: {b}, w: {b}}}
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

# name, inner inset (None = filled), 9-slice border, PPU, the vendor file it replaces
SPRITES = (
    ("frame_cut_filled_200", None, 26, 200, "Cut Frame Filled Big (200ppu).png"),
    ("frame_cut_filled_300", None, 26, 300, "Cut Frame Filled Big (300ppu).png"),
    ("frame_cut_outline_200", BAND_PX, 30, 200, "Cut Frame Big - 6px (200ppu).png"),
)


def build(name, inset, border, ppu):
    rgba = render(coverage(inset))
    a = rgba[..., 3]

    # The invariants the shape claims, asserted rather than eyeballed.
    assert a[0, 0] == 0 and a[-1, -1] == 0, f"{name}: the two cut corners must be empty"
    assert a[0, -1] == 255 and a[-1, 0] == 255, f"{name}: the two SQUARE corners must be solid"
    # The chamfer has to live inside its own 9-slice corner tile, or a sliced draw stretches it.
    assert CHAMFER_TL <= border + 1 and CHAMFER_BR <= border + 1, \
        f"{name}: chamfer {max(CHAMFER_TL, CHAMFER_BR):.2f}px does not fit border {border}px"
    # A 9-slice stretches only the middle band, so every row of it must be constant across x.
    mid = a[border:SIZE - border, border:SIZE - border]
    assert mid.min() == mid.max(), f"{name}: the stretch region must be uniform"

    meta = META.format(guid=guid_for(name), sprite_id=guid_for(name + "/sprite"),
                       ppu=ppu, b=border)
    return encode_png(rgba), meta


# --------------------------------------------------------------------------------------------
# --verify-vendor : the proof, run while the pack still exists
# --------------------------------------------------------------------------------------------

def read_png_alpha(path):
    d = open(path, "rb").read()
    pos, idat, w, h = 8, b"", None, None
    while pos < len(d):
        ln = struct.unpack(">I", d[pos:pos + 4])[0]
        typ = d[pos + 4:pos + 8]
        if typ == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", d[pos + 8:pos + 18])
            assert bd == 8 and ct == 6, f"{path}: expected 8-bit RGBA"
        elif typ == b"IDAT":
            idat += d[pos + 8:pos + 8 + ln]
        pos += 12 + ln
    raw = zlib.decompress(idat)
    stride = w * 4
    out = np.zeros((h, stride), np.uint8)
    prev = np.zeros(stride, np.uint8)
    p = 0
    for y in range(h):
        f = raw[p]
        p += 1
        line = np.frombuffer(raw[p:p + stride], np.uint8).copy()
        p += stride
        if f == 1:
            for i in range(4, stride):
                line[i] = (int(line[i]) + int(line[i - 4])) & 255
        elif f == 2:
            line = (line.astype(int) + prev.astype(int)).astype(np.uint8)
        elif f == 3:
            for i in range(stride):
                a = int(line[i - 4]) if i >= 4 else 0
                line[i] = (int(line[i]) + (a + int(prev[i])) // 2) & 255
        elif f == 4:
            for i in range(stride):
                a = int(line[i - 4]) if i >= 4 else 0
                b = int(prev[i])
                c = int(prev[i - 4]) if i >= 4 else 0
                pp = a + b - c
                pa, pb, pc = abs(pp - a), abs(pp - b), abs(pp - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (int(line[i]) + pr) & 255
        out[y] = line
        prev = line
    return out.reshape(h, w, 4)[..., 3].astype(np.float64) / 255.0


def compare(mine_alpha, vendor_path, label):
    if not os.path.exists(vendor_path):
        print(f"  {label:22s} SKIP - vendor file is gone (already removed)")
        return True
    v = read_png_alpha(vendor_path)
    d = np.abs(v - mine_alpha)
    # A 45 deg edge one pixel out moves ~1.0 of alpha on the few pixels ON the edge, so the
    # measures that matter are the AREA (does the plate cover the same region) and how many
    # pixels disagree by more than a quarter of a step.
    area_v, area_m = v.sum(), mine_alpha.sum()
    bad = int((d > 0.25).sum())
    ok = bad == 0 and d.max() <= 0.15 and abs(area_v - area_m) <= 0.005 * max(area_v, 1.0)
    print(f"  {label:22s} {'OK' if ok else 'MISMATCH'}   "
          f"coverage {area_v:9.2f} -> {area_m:9.2f} px "
          f"({100 * (area_m - area_v) / area_v:+.3f}%)   "
          f"max |delta| {d.max():.3f}   pixels off by >0.25: {bad} / {v.size}")
    return ok


def verify_vendor():
    ok = True
    for name, inset, border, ppu, vendor in SPRITES:
        a = coverage(inset)
        ok &= compare(a, os.path.join(VENDOR_DIR, vendor), vendor)
    return 0 if ok else 1


def self_test():
    """A gate nobody has watched FAIL is a gate nobody should trust."""
    vp = os.path.join(VENDOR_DIR, "Cut Frame Filled Big (200ppu).png")
    if not os.path.exists(vp):
        print("SKIP: the vendor pack is gone, so there is nothing to prove against")
        return 0
    global CHAMFER_TL, CHAMFER_BR
    keep = (CHAMFER_TL, CHAMFER_BR)
    controls = (
        ("chamfer 4 px too big", (CHAMFER_TL + 4, CHAMFER_BR + 4)),
        ("chamfer 4 px too small", (CHAMFER_TL - 4, CHAMFER_BR - 4)),
        ("one corner square", (0.0, CHAMFER_BR)),
    )
    ok = True
    for label, (tl, br) in controls:
        CHAMFER_TL, CHAMFER_BR = tl, br
        caught = not compare(coverage(None), vp, label)
        ok &= caught
    CHAMFER_TL, CHAMFER_BR = keep
    print("OK: every negative control fires" if ok
          else "FAIL: a real difference slipped through --verify-vendor")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--verify-vendor", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        print("negative controls for --verify-vendor")
        return self_test()
    if args.verify_vendor:
        print("Shift - Complete Sci-Fi UI -> first-party cut-corner frames")
        return verify_vendor()

    built = [(n, *build(n, i, b, p)) for n, i, b, p, _ in SPRITES]
    folder_meta = FOLDER_META.format(guid=guid_for("__folder__")).encode()

    if args.check:
        drift = []
        for name, png, meta in built:
            p = os.path.join(OUT_DIR, name + ".png")
            for path, want in ((p, png), (p + ".meta", meta.encode())):
                have = open(path, "rb").read() if os.path.exists(path) else b""
                if have != want:
                    drift.append(os.path.relpath(path, REPO))
        have = open(OUT_DIR + ".meta", "rb").read() if os.path.exists(OUT_DIR + ".meta") else b""
        if have != folder_meta:
            drift.append("Assets/_Graphics/UI/Frames.meta")
        for d in drift:
            print("DRIFT", d)
        print("FAIL: re-run without --check" if drift else "OK: UI frame sprites match")
        return 1 if drift else 0

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "wb") as f:
        f.write(folder_meta)
    for (name, png, meta), (_, _, border, ppu, _) in zip(built, SPRITES):
        p = os.path.join(OUT_DIR, name + ".png")
        open(p, "wb").write(png)
        with open(p + ".meta", "w", newline="\n") as f:
            f.write(meta)
        print(f"wrote {name}.png ({SIZE}x{SIZE}, border {border}, {ppu} ppu)  "
              f"guid={guid_for(name)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
