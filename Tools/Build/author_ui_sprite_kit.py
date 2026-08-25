#!/usr/bin/env python3
"""Author the Cosmic Shore UI 9-slice sprite kit (UI redesign task T7).

Writes, under ``Assets/_Graphics/UI/SpriteKit/``, one white+alpha PNG and one
``.meta`` per kit entry, plus the demonstration scene
``Assets/_Scenes/Game_TestDesign/UISpriteKitTestScene.unity``.

Everything is derived from the table in :data:`KIT` - there are no hand-edited
pixels and no hand-edited YAML, so ``--check`` can prove the committed files are
exactly what this table says they are.

    python3 Tools/Build/author_ui_sprite_kit.py            # write
    python3 Tools/Build/author_ui_sprite_kit.py --check    # verify, write nothing

Spec: ``Docs/STYLE_FOUNDATION.md`` Sec.5 (geometry) and Sec.10 (components).

No colour is baked.  Every sprite is RGB 255,255,255 with the shape carried
entirely in alpha, so a single asset serves every state in Sec.10.1 / Sec.10.6
under an ``Image.color`` tint driven from ``UIThemeSO`` (task T4).
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from ui_sprite_kit_geometry import (  # noqa: E402
    ORIENT_DEFAULT, ORIENT_FLIPPED, banner_cap, encode_png, hexagon,
    measure_border, parallelogram, rasterize, slivered_rect,
)

REPO = Path(__file__).resolve().parents[2]
SPRITE_DIR = REPO / "Assets/_Graphics/UI/SpriteKit"
SCENE_PATH = REPO / "Assets/_Scenes/Game_TestDesign/UISpriteKitTestScene.unity"

# --------------------------------------------------------------------------
# The kit
# --------------------------------------------------------------------------
# Sec.5 sliver sizes: 14 px on large surfaces, 10 px on buttons and chips.
SLIVER_LARGE = 14
SLIVER_CHIP = 10
#: Sec.5 names one button sliver (10).  The 14 px-text button is a second
#: button size the spec's type scale requires but its geometry table does not
#: cover, so its sliver is scaled with the type: 10 * 14/18 = 7.8 -> 8, which
#: also lands on the Sec.5 spacing scale.  Recorded in the feedback queue.
SLIVER_CHIP_SMALL = 8

HAIRLINE = 1   # Sec.5
STROKE = 2     # Sec.5 emphasis stroke


def _rect(w, h, sliver, corners, stroke=0.0):
    return lambda: (w, h, slivered_rect(w, h, sliver, corners), stroke)


def _hex(w, h, point, stroke=0.0):
    return lambda: (w, h, hexagon(w, h, point), stroke)


def _para(w, h, slant, stroke=0.0):
    return lambda: (w, h, parallelogram(w, h, slant), stroke)


def _cap(w, h, side):
    return lambda: (w, h, banner_cap(w, h, side), 0.0)


#: ``name -> (builder, slice_mode, nominal_inset, group, component, note)``
#:
#: The 9-slice border is NOT in this table.  It is measured off the rasterised
#: shape by :func:`ui_sprite_kit_geometry.measure_border`, because the correct
#: inset is a property of the pixels, not a number a designer picks: a filled
#: sliver needs exactly its sliver width, while the same shape outlined needs
#: more, since eroding the polygon pushes the mitre where the diagonal meets
#: the straight edge back past the sliver line.  ``nominal_inset`` is the Sec.5
#: design quantity (sliver / hex point run / banner slant) and is asserted to
#: be a lower bound on the measured border.
#:
#: ``slice_mode``:
#:   ``both``       insets on all four sides - scales freely in width AND height
#:   ``horizontal`` left/right only - the authored height is the shape
#:   ``none``       no scalable interior - uniform scale only
KIT: dict[str, tuple] = {}


def _add(name, builder, mode, nominal, group, component, note):
    KIT[name] = (builder, mode, nominal, group, component, note)


# -- Buttons (Sec.10.1) ----------------------------------------------------
# The sliver sits on the short ends: one cut per end, on opposite corners.
# Insets on all four sides keep both cut corners unstretched, so the 45 degree
# diagonal survives a change of height as well as of width - buttons only
# lengthen today, but nothing in the asset depends on that staying true.
for _suffix, _corners in (("", ORIENT_DEFAULT), ("_Flipped", ORIENT_FLIPPED)):
    _add(f"UIKit_Button_Fill{_suffix}", _rect(64, 48, SLIVER_CHIP, _corners),
         "both", SLIVER_CHIP, "Buttons", "Sec.10.1 opaque button",
         "18 px caps text; authored height 48")
    _add(f"UIKit_Button_Border1{_suffix}",
         _rect(64, 48, SLIVER_CHIP, _corners, HAIRLINE),
         "both", SLIVER_CHIP, "Buttons", "Sec.10.1 transparent button",
         "1 px hairline frame")
    _add(f"UIKit_ButtonSmall_Fill{_suffix}",
         _rect(52, 36, SLIVER_CHIP_SMALL, _corners),
         "both", SLIVER_CHIP_SMALL, "Buttons",
         "Sec.10.1 opaque button, small",
         "14 px caps text; authored height 36")
    _add(f"UIKit_ButtonSmall_Border1{_suffix}",
         _rect(52, 36, SLIVER_CHIP_SMALL, _corners, HAIRLINE),
         "both", SLIVER_CHIP_SMALL, "Buttons",
         "Sec.10.1 transparent button, small", "1 px hairline frame")

# -- Panel / popup (Sec.10.3) ---------------------------------------------
for _suffix, _corners in (("", ORIENT_DEFAULT), ("_Flipped", ORIENT_FLIPPED)):
    _add(f"UIKit_Panel_Fill{_suffix}", _rect(64, 64, SLIVER_LARGE, _corners),
         "both", SLIVER_LARGE, "Panels", "Sec.10.3 popup body",
         "tint 00010A at opacity")
    _add(f"UIKit_Panel_Border1{_suffix}",
         _rect(64, 64, SLIVER_LARGE, _corners, HAIRLINE),
         "both", SLIVER_LARGE, "Panels", "Sec.10.3 popup frame",
         "1 px E6E9FF border")

# -- Card (Sec.10.6, Sec.10.13) -------------------------------------------
for _suffix, _corners in (("", ORIENT_DEFAULT), ("_Flipped", ORIENT_FLIPPED)):
    _add(f"UIKit_Card_Fill{_suffix}", _rect(80, 80, SLIVER_LARGE, _corners),
         "both", SLIVER_LARGE, "Cards",
         "Sec.10.6 Daily Deals / Arcade Explore",
         "state is tint + glow, not geometry")
    _add(f"UIKit_Card_Border2{_suffix}",
         _rect(80, 80, SLIVER_LARGE, _corners, STROKE),
         "both", SLIVER_LARGE, "Cards", "Sec.10.13 selected card frame",
         "2 px emphasis stroke")

# -- Currency pill (Sec.10.4) ---------------------------------------------
for _suffix, _corners in (("", ORIENT_DEFAULT), ("_Flipped", ORIENT_FLIPPED)):
    _add(f"UIKit_CurrencyPill_Fill{_suffix}",
         _rect(56, 32, SLIVER_CHIP, _corners),
         "both", SLIVER_CHIP, "Pill", "Sec.10.4 currency bar body", "")
    _add(f"UIKit_CurrencyPill_Border1{_suffix}",
         _rect(56, 32, SLIVER_CHIP, _corners, HAIRLINE),
         "both", SLIVER_CHIP, "Pill", "Sec.10.4 currency bar frame",
         "1 px light border")

# -- Hexagons (Sec.10.5, Sec.10.7, Sec.10.12) -----------------------------
# A hexagon carries no sliver, and its two points ARE the shape: they span the
# full height, so there is no horizontal band to stretch vertically.  Insets
# are horizontal only, which lets a tile widen for a longer label with the
# points unchanged, and fixes the authored height.
_add("UIKit_HexTile_Fill", _hex(56, 48, 14), "horizontal", 14, "Hex",
     "Sec.10.5 tab nav / Sec.10.12 port side nav", "inactive dim fill")
_add("UIKit_HexTile_Border2", _hex(56, 48, 14, STROKE), "horizontal", 14,
     "Hex", "Sec.10.5 / Sec.10.12 active tile", "2 px white border")
_add("UIKit_HexHandle_Fill", _hex(28, 24, 7), "horizontal", 7, "Hex",
     "Sec.10.7 settings slider handle", "solid handle")

# -- End-of-game banner (Sec.10.9) ----------------------------------------
# Sec.10.9 calls this an ANGLED banner, not a slivered one: its ends are a
# shallower 2:1 rake across the full height, a different element from the
# 45 degree corner sliver on purpose.
BANNER_H = 64
BANNER_SLANT = 32
_add("UIKit_Banner_Fill", _para(128, BANNER_H, BANNER_SLANT), "horizontal",
     BANNER_SLANT, "Banner", "Sec.10.9 VICTORY / DEFEAT body",
     "team (Light) tint")
_add("UIKit_BannerCap_Left", _cap(BANNER_SLANT, BANNER_H, "left"), "none",
     0, "Banner", "Sec.10.9 left end cap",
     "triangle: no scalable interior, uniform scale only")
_add("UIKit_BannerCap_Right", _cap(BANNER_SLANT, BANNER_H, "right"), "none",
     0, "Banner", "Sec.10.9 right end cap",
     "triangle: no scalable interior, uniform scale only")


# --------------------------------------------------------------------------
# Derived per-sprite data
# --------------------------------------------------------------------------

_RASTER_CACHE: dict[str, list[list[int]]] = {}


def kit_raster(name: str) -> list[list[int]]:
    """The alpha grid for one kit entry (memoised - rasterising is the cost)."""
    if name not in _RASTER_CACHE:
        w, h, region, stroke = KIT[name][0]()
        _RASTER_CACHE[name] = rasterize(w, h, region, stroke)
    return _RASTER_CACHE[name]


def kit_border(name: str) -> tuple[int, int, int, int]:
    """Measured Unity sprite border ``(left, bottom, right, top)``."""
    border = measure_border(kit_raster(name), KIT[name][1])
    nominal = KIT[name][2]
    for side in border:
        if side and side < nominal:
            raise AssertionError(
                f"{name}: measured inset {border} is smaller than its "
                f"nominal {nominal} px feature - the sliver would be stretched")
    return border


def sprite_size(name: str) -> tuple[int, int]:
    a = kit_raster(name)
    return len(a[0]), len(a)


# --------------------------------------------------------------------------
# Unity asset emission
# --------------------------------------------------------------------------

def guid_for(unity_path: str) -> str:
    """Deterministic asset GUID, so re-running the tool never churns refs."""
    return hashlib.md5(f"CosmicShore/T7/{unity_path}".encode()).hexdigest()


# Unity writes this same constant into every single-sprite texture meta in the
# project (checked across Assets/_Graphics); it is the default sprite's id.
DEFAULT_SPRITE_ID = "5e97eb03825dee720800000000000000"

_PLATFORMS = ("DefaultTexturePlatform", "Standalone", "Server", "Android",
              "iPhone")


def _platform_block(target: str) -> str:
    return f"""  - serializedVersion: 3
    buildTarget: {target}
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
"""


def png_meta(unity_path: str, border: tuple[int, int, int, int]) -> str:
    """A Sprite (2D and UI) importer meta for one kit PNG.

    Deliberate settings, all of which the kit depends on:
      * ``textureCompression: 0`` - block compression would chew the 1 px
        frames and the antialiased diagonal, which is the entire shape.
      * ``enableMipMap: 0`` - UI sprites are drawn at one depth.
      * ``alphaIsTransparency: 1`` with white RGB - no dark fringe on the cut.
      * ``spritePixelsToUnits: 100`` and ``spriteMeshType: 1`` match every
        other sprite in the project, including the 9-sliced shipped buttons.
    """
    left, bottom, right, top = border
    plat = "".join(_platform_block(t) for t in _PLATFORMS)
    return f"""fileFormatVersion: 2
guid: {guid_for(unity_path)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
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
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
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
  spriteBorder: {{x: {left}, y: {bottom}, z: {right}, w: {top}}}
  spriteGenerateFallbackPhysicsShape: 0
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
  cookieLightType: 0
  platformSettings:
{plat}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {DEFAULT_SPRITE_ID}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  spritePackingTag: 
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def folder_meta(unity_path: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid_for(unity_path)}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def scene_meta(unity_path: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid_for(unity_path)}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def build_sprite(name: str) -> bytes:
    return encode_png(kit_raster(name))


def unity_path_for(name: str) -> str:
    return f"Assets/_Graphics/UI/SpriteKit/{name}.png"


# --------------------------------------------------------------------------
# Emit
# --------------------------------------------------------------------------

def planned_files() -> dict[Path, bytes]:
    """Every file this tool owns, as ``absolute path -> exact bytes``."""
    out: dict[Path, bytes] = {}
    for folder in ("Assets/_Graphics/UI", "Assets/_Graphics/UI/SpriteKit"):
        out[REPO / (folder + ".meta")] = folder_meta(folder).encode()
    for name in KIT:
        up = unity_path_for(name)
        out[REPO / up] = build_sprite(name)
        out[REPO / (up + ".meta")] = png_meta(up, kit_border(name)).encode()

    from ui_sprite_kit_scene import build_scene  # noqa: E402
    scene_rel = SCENE_PATH.relative_to(REPO).as_posix()
    entries = [
        {"name": n, "group": KIT[n][3], "component": KIT[n][4],
         "note": KIT[n][5], "border": kit_border(n), "size": sprite_size(n)}
        for n in KIT
    ]
    out[SCENE_PATH] = build_scene(entries, guid_for, unity_path_for).encode()
    out[REPO / (scene_rel + ".meta")] = scene_meta(scene_rel).encode()
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true",
                    help="verify on-disk files match this table; write nothing")
    ap.add_argument("--table", action="store_true",
                    help="print the kit inventory as markdown and exit")
    args = ap.parse_args()

    if args.table:
        print("| Sprite | Source | Border (L,B,R,T) | Min size | Scales | "
              "Component |")
        print("|---|---|---|---|---|---|")
        for name in KIT:
            mode = KIT[name][1]
            b = kit_border(name)
            w, h = sprite_size(name)
            axes = {"both": "width + height", "horizontal": "width only",
                    "none": "uniform only"}[mode]
            mn = (f"{b[0] + b[2]}x{b[3] + b[1]}" if mode == "both"
                  else f"{b[0] + b[2]}x{h}" if mode == "horizontal"
                  else f"{w}x{h}")
            print(f"| `{name}` | {w}x{h} | {b[0]},{b[1]},{b[2]},{b[3]} | "
                  f"{mn} | {axes} | {KIT[name][4]} |")
        return 0

    files = planned_files()
    if args.check:
        bad = []
        for path, want in sorted(files.items()):
            rel = path.relative_to(REPO)
            if not path.exists():
                bad.append(f"MISSING  {rel}")
            elif path.read_bytes() != want:
                bad.append(f"DRIFTED  {rel}")
        if bad:
            print("\n".join(bad))
            print(f"\n{len(bad)} file(s) out of date - "
                  f"re-run without --check", file=sys.stderr)
            return 1
        print(f"OK  {len(files)} file(s) match the kit table "
              f"({len(KIT)} sprites)")
        return 0

    for path, data in sorted(files.items()):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    print(f"wrote {len(files)} file(s): {len(KIT)} sprites + metas + test scene")
    print(f"  sprites -> {SPRITE_DIR.relative_to(REPO)}")
    print(f"  scene   -> {SCENE_PATH.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
