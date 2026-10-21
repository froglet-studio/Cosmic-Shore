#!/usr/bin/env python3
"""Draw the phone-control glyphs and touch-button icons (white on transparent, tinted in game).

    python3 Tools/Build/touch_glyph_art.py           # write any missing or changed PNG + .meta
    python3 Tools/Build/touch_glyph_art.py --check   # exit 1 if a PNG would change (drift gate)

Thumb-lift glyphs name the touch gesture a card's ability is on (Docs/Reports/MOBILE_VESSELS.md):
a landscape phone with two thumbs at its bottom corners; a lifted thumb is hollow with an arrow up.
The button icons are for TouchControlProfile.extraButtons (fire, ring, zoom, drift, bomb, squad,
recall, hold, dipole, missile) and the crew seats' own buttons (the Goby's primer and flares, the
Pistol's ghost and snap) and the disc and rim every button is drawn on. GUIDs are derived from the file name, so a re-run never re-keys an asset.
"""
import hashlib
import io
import math
import os
import sys

from PIL import Image, ImageDraw

OUT = "Assets/_Graphics/Design Assests/HUD UI/Touch"
SIZE = 256
SS = 4  # supersampling
W = 255

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
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
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


def guid_for(name):
    return hashlib.md5(("touch-glyph:" + name).encode()).hexdigest()


def canvas():
    img = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def s(v):
    return int(round(v * SS))


def line(d, pts, width):
    d.line([(s(x), s(y)) for x, y in pts], fill=(W, W, W, 255), width=s(width), joint="curve")
    r = width / 2
    for x, y in (pts[0], pts[-1]):
        d.ellipse([s(x - r), s(y - r), s(x + r), s(y + r)], fill=(W, W, W, 255))


def circle(d, cx, cy, r, width=None):
    box = [s(cx - r), s(cy - r), s(cx + r), s(cy + r)]
    if width is None:
        d.ellipse(box, fill=(W, W, W, 255))
    else:
        d.ellipse(box, outline=(W, W, W, 255), width=s(width))


def arrow(d, x, y_from, y_to, width=12, head=20):
    line(d, [(x, y_from), (x, y_to + head * 0.4)], width)
    d.polygon([(s(x - head), s(y_to + head)), (s(x + head), s(y_to + head)), (s(x), s(y_to))],
              fill=(W, W, W, 255))


def arrow_h(d, x_from, x_to, y, width=12, head=20):
    dirn = 1 if x_to > x_from else -1
    line(d, [(x_from, y), (x_to - dirn * head * 0.4, y)], width)
    d.polygon([(s(x_to - dirn * head), s(y - head)), (s(x_to - dirn * head), s(y + head)), (s(x_to), s(y))],
              fill=(W, W, W, 255))


def phone(d):
    d.rounded_rectangle([s(16), s(60), s(240), s(220)], radius=s(26), outline=(W, W, W, 255), width=s(9))


THUMB_Y = 182
THUMB_L = 66
THUMB_R = 190


def thumb(d, x, lifted):
    if lifted:
        circle(d, x, THUMB_Y, 24, width=8)
        arrow(d, x, THUMB_Y - 30, 14, width=10, head=18)
    else:
        circle(d, x, THUMB_Y, 24)


def g_lift(left, right, divider=False):
    def draw(d):
        phone(d)
        thumb(d, THUMB_L, left)
        thumb(d, THUMB_R, right)
        if divider:
            line(d, [(128, 120), (128, 200)], 8)
    return draw


def g_apart(d):
    phone(d)
    circle(d, THUMB_L + 20, 140, 22)
    circle(d, THUMB_R - 20, 140, 22)
    arrow_h(d, THUMB_L + 2, 30, 140, width=10, head=16)
    arrow_h(d, THUMB_R - 2, 226, 140, width=10, head=16)


def i_fire(d):
    circle(d, 128, 128, 78, width=14)
    for a in range(4):
        ang = a * math.pi / 2
        line(d, [(128 + math.cos(ang) * 50, 128 + math.sin(ang) * 50),
                 (128 + math.cos(ang) * 110, 128 + math.sin(ang) * 110)], 14)
    circle(d, 128, 128, 18)


def i_ring(d):
    circle(d, 128, 128, 52, width=18)
    for a in range(8):
        ang = a * math.pi / 4
        line(d, [(128 + math.cos(ang) * 76, 128 + math.sin(ang) * 76),
                 (128 + math.cos(ang) * 112, 128 + math.sin(ang) * 112)], 12)


def i_zoom(d):
    circle(d, 108, 108, 62, width=16)
    line(d, [(152, 152), (214, 214)], 26)
    line(d, [(80, 108), (136, 108)], 12)
    line(d, [(108, 80), (108, 136)], 12)


def i_drift(d):
    pts = [(48 + t * 1.6, 200 - 120 * math.sin(t / 100 * math.pi / 2)) for t in range(0, 101, 5)]
    line(d, pts, 16)
    d.polygon([(s(208), s(64)), (s(176), s(70)), (s(200), s(100))], fill=(W, W, W, 255))
    for i, x in enumerate((40, 70, 100)):
        line(d, [(x, 220), (x + 18, 220)], 8)


def i_bomb(d):
    circle(d, 116, 146, 70)
    line(d, [(160, 92), (186, 60)], 12)
    for a in range(6):
        ang = a * math.pi / 3
        line(d, [(196 + math.cos(ang) * 8, 50 + math.sin(ang) * 8),
                 (196 + math.cos(ang) * 30, 50 + math.sin(ang) * 30)], 8)


def i_squad(d):
    for cx, cy in ((128, 80), (76, 170), (180, 170)):
        circle(d, cx, cy, 30)
    line(d, [(128, 112), (90, 146)], 8)
    line(d, [(128, 112), (166, 146)], 8)


def i_recall(d):
    pts = [(196, 200), (196, 110)] + [(128 + 68 * math.cos(t), 110 - 68 * math.sin(t))
                                      for t in [i * math.pi / 20 for i in range(0, 21)]]
    line(d, pts, 16)
    d.polygon([(s(30), s(110)), (s(90), s(110)), (s(60), s(160))], fill=(W, W, W, 255))


def i_hold(d):
    d.rounded_rectangle([s(70), s(50), s(110), s(206)], radius=s(12), fill=(W, W, W, 255))
    d.rounded_rectangle([s(146), s(50), s(186), s(206)], radius=s(12), fill=(W, W, W, 255))


def i_dipole(d):
    circle(d, 70, 128, 36)
    circle(d, 186, 128, 36, width=14)
    line(d, [(106, 128), (150, 128)], 10)
    for k in (-1, 1):
        pts = [(70 + t * 1.16, 128 + k * 60 * math.sin(t / 100 * math.pi)) for t in range(0, 101, 5)]
        line(d, pts, 6)


def i_missile(d):
    # a rocket climbing up and to the right: body, nose cone, two swept fins at the tail, its plume behind
    k = math.sqrt(0.5)
    dx, dy, px, py = k, -k, k, k               # along the body, and across it
    ax, ay, bx, by = 78, 178, 166, 90          # tail and the nose's base
    def at(t, u):
        return (s(ax + dx * t + px * u), s(ay + dy * t + py * u))
    line(d, [(ax, ay), (bx, by)], 34)
    d.polygon([at(124, -17), at(124, 17), at(168, 0)], fill=(W, W, W, 255))
    for side in (-1, 1):
        d.polygon([at(4, side * 14), at(44, side * 14), at(-8, side * 46)], fill=(W, W, W, 255))
    for i, r in ((1, 11), (2, 8), (3, 5)):
        circle(d, ax - dx * 24 * i, ay - dy * 24 * i, r)


def i_primer(d):
    # the Goby's primer, an oil bomb: a drop, with a splash ring under it
    cx, cy, r, tip = 128, 140, 64, 26
    a = math.acos(r / (cy - tip))   # where the drop's sides leave the round, from straight up
    circle(d, cx, cy, r)
    d.polygon([(s(cx), s(tip)), (s(cx + r * math.sin(a)), s(cy - r * math.cos(a))),
               (s(cx - r * math.sin(a)), s(cy - r * math.cos(a)))], fill=(W, W, W, 255))
    d.ellipse([s(58), s(218), s(198), s(240)], outline=(W, W, W, 255), width=s(8))


def i_flares(d):
    # heat-seeking flares: three trails fanning out from one point, each ending in a spark
    for ang in (-40, 0, 40):
        a = math.radians(ang - 90)
        pts = [(128 + math.cos(a) * r + 12 * math.sin(r / 30) * math.cos(a + math.pi / 2),
                214 + math.sin(a) * r + 12 * math.sin(r / 30) * math.sin(a + math.pi / 2)) for r in range(0, 151, 10)]
        line(d, pts, 10)
        circle(d, pts[-1][0], pts[-1][1], 18)


def i_ghost(d):
    # the Pistol's ghost: its outline dashed, passing through
    for i in range(12):
        a0, a1 = i * math.pi / 6, i * math.pi / 6 + math.pi / 10
        line(d, [(128 + 84 * math.cos(a0 + (a1 - a0) * k / 4), 128 + 84 * math.sin(a0 + (a1 - a0) * k / 4))
                 for k in range(5)], 14)
    circle(d, 128, 128, 22)


def i_snap(d):
    # the Pistol's Snap: a vortex ring leaving the claw
    d.ellipse([s(118), s(58), s(214), s(198)], outline=(W, W, W, 255), width=s(22))
    d.pieslice([s(26), s(70), s(126), s(186)], 300, 60, fill=(W, W, W, 255))


def i_disc(d):
    circle(d, 128, 128, 122)


def i_disc_ring(d):
    circle(d, 128, 128, 118, width=10)


ART = {
    "TouchIcon_Disc": i_disc,
    "TouchIcon_DiscRing": i_disc_ring,
    "TouchGlyph_LiftLeftThumb": g_lift(True, False),
    "TouchGlyph_LiftRightThumb": g_lift(False, True),
    "TouchGlyph_LiftEitherThumb": g_lift(True, True, divider=True),
    "TouchGlyph_LiftBothThumbs": g_lift(True, True),
    "TouchGlyph_ThumbsApart": g_apart,
    "TouchIcon_Fire": i_fire,
    "TouchIcon_Ring": i_ring,
    "TouchIcon_Zoom": i_zoom,
    "TouchIcon_Drift": i_drift,
    "TouchIcon_Bomb": i_bomb,
    "TouchIcon_Squad": i_squad,
    "TouchIcon_Recall": i_recall,
    "TouchIcon_Hold": i_hold,
    "TouchIcon_Dipole": i_dipole,
    "TouchIcon_Missile": i_missile,
    "TouchIcon_Primer": i_primer,
    "TouchIcon_Flares": i_flares,
    "TouchIcon_Ghost": i_ghost,
    "TouchIcon_Snap": i_snap,
}


def render(draw):
    img, d = canvas()
    draw(d)
    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def main():
    check = "--check" in sys.argv
    os.makedirs(OUT, exist_ok=True)
    drift = []
    for name, draw in ART.items():
        png = render(draw)
        path = os.path.join(OUT, name + ".png")
        old = open(path, "rb").read() if os.path.exists(path) else None
        if old != png:
            drift.append(path)
            if not check:
                open(path, "wb").write(png)
        meta = path + ".meta"
        g = guid_for(name)
        text = META.format(guid=g, sprite_id=hashlib.md5(("sprite:" + name).encode()).hexdigest())
        if not os.path.exists(meta) or open(meta).read() != text:
            if check:
                drift.append(meta)
            else:
                open(meta, "w").write(text)
    folder_meta = OUT + ".meta"
    if not os.path.exists(folder_meta) and not check:
        open(folder_meta, "w").write(
            "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid_for("folder"))
    if check:
        if drift:
            print("touch_glyph_art: out of date:\n  " + "\n  ".join(drift))
            return 1
        print("touch_glyph_art: OK (%d sprites)" % len(ART))
        return 0
    print("touch_glyph_art: wrote %d file(s)" % len(drift))
    for name in ART:
        print("  %-28s guid %s" % (name, guid_for(name)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
