#!/usr/bin/env python3
"""
Build TextMeshPro SDF font assets for the Cosmic Shore type system, outside Unity.

This is the SOURCE; the .asset files under Assets/_Graphics/Fonts/ are the build.
Re-run with --check to prove the committed assets still match this generator
(asset-surgery §5: "a generator that has drifted behind its own output is a loaded gun").

    python3 Tools/Build/build_tmp_font_assets.py --verify-donor
    python3 Tools/Build/build_tmp_font_assets.py --build
    python3 Tools/Build/build_tmp_font_assets.py --check

Every metric formula was measured off TMP-generated assets already in this repo;
see tmp_font_lib.py's docstring and Docs/FONTS.md.
"""
import argparse, os, re, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import freetype
from tmp_font_lib import (face_info, render_glyph, ShelfPacker, tmp_hash,
                          stable_guid, stable_file_id, SS_DEFAULT)

ROOT     = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Fonts")
TMP_RES  = os.path.join(ROOT, "Assets", "Unity Assests", "TextMesh Pro", "Resources")
DONOR    = os.path.join(TMP_RES, "Fonts & Materials", "ChakraPetch-Regular SDF.asset")

POINT_SIZE, PADDING, ATLAS_W, ATLAS_H = 90, 9, 1024, 1024
RENDER_MODE   = 4165          # SDFAA = 1X | SDFAA | NO_HINTING | 8BIT
SDF_SHADER    = "68e6db2ebdc24f95958faec2be5558d6"      # TMP_SDF.shader (project default)
MULTI_ATLAS   = 1

# ---- charset: ASCII + Latin-1 Supplement + the explicitly required symbols -----
# Style Foundation §4 v0.2 dropped two of the originally-listed symbols:
#   U+2011 non-breaking hyphen — no use case in this UI; the regular hyphen is fine.
#   U+2715 ✕                   — it is the close/kick icon, so it ships as an icon
#                                 sprite. It also existed in only one of the three
#                                 families, which would have rendered inconsistently.
# Do not re-add either without a §4 revision.
EXPLICIT = [0x00D7, 0x00B7, 0x2014, 0x2013, 0x2026,
            0x2190, 0x2192, 0x2191, 0x2193, 0x002B, 0x2212]
CHARSET  = sorted(set(list(range(0x20, 0x7F)) + list(range(0xA0, 0x100)) + EXPLICIT))
# TMP's own conventional extras: zero-width space keeps <nobr>/wrapping well behaved.
CHARSET  = sorted(set(CHARSET + [0x200B]))

WEIGHT_NAME = {300: "Light", 400: "Regular", 500: "Medium", 600: "SemiBold", 700: "Bold"}
# key = folder name under Assets/_Graphics/Fonts/ (Style Foundation T5 spells these
# without spaces); value = (file stem, weights, human-readable family name).
FAMILIES = {
    "ChakraPetch":   ("ChakraPetch",   [400, 500, 600, 700], "Chakra Petch"),
    "SpaceGrotesk":  ("SpaceGrotesk",  [300, 400, 500, 600], "Space Grotesk"),
    "JetBrainsMono": ("JetBrainsMono", [400, 500, 700],      "JetBrains Mono"),
}


def asset_name(stem):
    return f"{stem} SDF"


# ------------------------------------------------------------------ emission
def fmt(v):
    """Serialize a float the way Unity's YAML writer does: the SHORTEST decimal that
    round-trips through float32. Truncating to a fixed width instead writes noise like
    `_GlowOuter: 0.0500000007`, which parses to the same value but is unreviewable."""
    if isinstance(v, int) or (v == int(v) and abs(v) < 1e15):
        return str(int(v))
    f = np.float32(v)
    for prec in range(1, 10):
        s = "%.*g" % (prec, float(f))
        if np.float32(float(s)) == f:
            return s
    return repr(float(f))


def build_font_asset(ttf_path, name, point_size=POINT_SIZE, padding=PADDING,
                     atlas_w=ATLAS_W, atlas_h=ATLAS_H, charset=CHARSET, ss=SS_DEFAULT,
                     src_font_guid="", fallbacks=(), progress=None):
    """Render every glyph, pack, and return (yaml_text, stats)."""
    fi   = face_info(ttf_path, point_size)
    face = freetype.Face(ttf_path)
    cmap = {c for c, _ in face.get_chars()}

    glyphs, chars, missing = [], [], []
    tiles = []
    for cp in charset:
        if cp not in cmap:
            missing.append(cp); continue
        m, tile = render_glyph(face, cp, point_size, padding, ss)
        gi = len(glyphs) + 1
        glyphs.append(dict(index=gi, **m))
        tiles.append(tile)
        chars.append((cp, gi))
        if progress: progress(cp)

    # ---- pack (tallest first keeps shelves tight; ties broken by glyph index
    #      so the packing is deterministic and --check is meaningful)
    order = sorted(range(len(glyphs)),
                   key=lambda i: (-(glyphs[i]['h'] + 2 * padding), glyphs[i]['index']))
    atlases, packers = [], []
    for i in order:
        g, tile = glyphs[i], tiles[i]
        if tile is None:
            g.update(rx=0, ry=0, atlas=0); continue
        pw, ph = g['w'] + 2 * padding, g['h'] + 2 * padding
        for ai, pk in enumerate(packers):
            pos = pk.place(pw, ph)
            if pos: break
        else:
            ai, pk = len(packers), ShelfPacker(atlas_w, atlas_h)
            packers.append(pk); atlases.append(np.zeros((atlas_h, atlas_w), np.uint8))
            pos = pk.place(pw, ph)
            if not pos:
                raise SystemExit(f"{name}: glyph U+{chars[i][0]:04X} ({pw}x{ph}) exceeds atlas")
        px, py = pos
        atl = atlases[ai]
        # tile row 0 is the TOP of the glyph box; Unity texture row 0 is the BOTTOM.
        atl[py:py + ph, px:px + pw] = np.flipud(tile)
        g.update(rx=px + padding, ry=py + padding, atlas=ai)

    stats = dict(glyphs=len(glyphs), atlases=len(atlases), missing=missing,
                 fill=[float((a > 0).mean()) for a in atlases])

    # ---- ids
    mono_id = 11400000
    mat_id  = stable_file_id(name + "|material")
    tex_ids = [stable_file_id(f"{name}|atlas{i}") for i in range(len(atlases))]
    atlas_name = f"{name} Atlas"

    L = []
    a = L.append
    a("%YAML 1.1"); a("%TAG !u! tag:unity3d.com,2011:")
    a(f"--- !u!114 &{mono_id}"); a("MonoBehaviour:")
    for k in ("m_ObjectHideFlags: 0", "m_CorrespondingSourceObject: {fileID: 0}",
              "m_PrefabInstance: {fileID: 0}", "m_PrefabAsset: {fileID: 0}",
              "m_GameObject: {fileID: 0}", "m_Enabled: 1", "m_EditorHideFlags: 0",
              "m_Script: {fileID: 11500000, guid: 71c1514a6bd24e1e882cebbe1904ce04, type: 3}"):
        a("  " + k)
    a(f"  m_Name: {name}"); a("  m_EditorClassIdentifier: ")
    a("  m_Version: 1.1.0")
    a("  m_FaceInfo:")
    a("    m_FaceIndex: 0")
    a(f"    m_FamilyName: {fi['familyName']}")
    a(f"    m_StyleName: {fi['styleName']}")
    a(f"    m_PointSize: {point_size}")
    a("    m_Scale: 1")
    a(f"    m_UnitsPerEM: {fi['unitsPerEM']}")
    for key, fld in (("m_LineHeight", 'lineHeight'), ("m_AscentLine", 'ascentLine'),
                     ("m_CapLine", 'capLine'), ("m_MeanLine", 'meanLine'),
                     ("m_Baseline", 'baseline'), ("m_DescentLine", 'descentLine'),
                     ("m_SuperscriptOffset", 'superscriptOffset'), ("m_SuperscriptSize", 'superscriptSize'),
                     ("m_SubscriptOffset", 'subscriptOffset'), ("m_SubscriptSize", 'subscriptSize'),
                     ("m_UnderlineOffset", 'underlineOffset'), ("m_UnderlineThickness", 'underlineThickness'),
                     ("m_StrikethroughOffset", 'strikethroughOffset'),
                     ("m_StrikethroughThickness", 'strikethroughThickness'),
                     ("m_TabWidth", 'tabWidth')):
        a(f"    {key}: {fmt(fi[fld])}")
    a(f"  m_Material: {{fileID: {mat_id}}}")
    a(f"  m_SourceFontFileGUID: {src_font_guid}")
    a("  m_CreationSettings:")
    a("    sourceFontFileName: ")
    a(f"    sourceFontFileGUID: {src_font_guid}")
    a("    faceIndex: 0"); a("    pointSizeSamplingMode: 0")
    a(f"    pointSize: {point_size}"); a(f"    padding: {padding}")
    a("    paddingMode: 1"); a("    packingMode: 0")
    a(f"    atlasWidth: {atlas_w}"); a(f"    atlasHeight: {atlas_h}")
    a("    characterSetSelectionMode: 0")
    a(f"    characterSequence: {charset_sequence(charset)}")
    a("    referencedFontAssetGUID: "); a("    referencedTextAssetGUID: ")
    a("    fontStyle: 0"); a("    fontStyleModifier: 0")
    a(f"    renderMode: {RENDER_MODE}"); a("    includeFontFeatures: 0")
    a("  m_SourceFontFile: {fileID: 0}")
    a("  m_SourceFontFilePath: ")
    a("  m_AtlasPopulationMode: 0")
    a("  InternalDynamicOS: 0")
    a("  m_GlyphTable:")
    for g in glyphs:
        a(f"  - m_Index: {g['index']}")
        a("    m_Metrics:")
        a(f"      m_Width: {fmt(g['width'])}");        a(f"      m_Height: {fmt(g['height'])}")
        a(f"      m_HorizontalBearingX: {fmt(g['bearingX'])}")
        a(f"      m_HorizontalBearingY: {fmt(g['bearingY'])}")
        a(f"      m_HorizontalAdvance: {fmt(g['advance'])}")
        a("    m_GlyphRect:")
        a(f"      m_X: {g['rx']}"); a(f"      m_Y: {g['ry']}")
        a(f"      m_Width: {g['w']}"); a(f"      m_Height: {g['h']}")
        a("    m_Scale: 1")
        a(f"    m_AtlasIndex: {g['atlas']}")
        a("    m_ClassDefinitionType: 0")
    a("  m_CharacterTable:")
    for cp, gi in chars:
        a("  - m_ElementType: 1"); a(f"    m_Unicode: {cp}")
        a(f"    m_GlyphIndex: {gi}"); a("    m_Scale: 1")
    a("  m_AtlasTextures:")
    for t in tex_ids:
        a(f"  - {{fileID: {t}}}")
    a(f"  m_AtlasTextureIndex: {len(atlases) - 1}")
    a(f"  m_IsMultiAtlasTexturesEnabled: {MULTI_ATLAS}")
    a("  m_GetFontFeatures: 1")
    a("  m_ClearDynamicDataOnBuild: 0")
    a(f"  m_AtlasWidth: {atlas_w}"); a(f"  m_AtlasHeight: {atlas_h}")
    a(f"  m_AtlasPadding: {padding}"); a(f"  m_AtlasRenderMode: {RENDER_MODE}")
    a("  m_UsedGlyphRects: []"); a("  m_FreeGlyphRects: []")
    a("  m_FontFeatureTable:"); a("    m_MultipleSubstitutionRecords: []")
    a("    m_LigatureRecords: []"); a("    m_GlyphPairAdjustmentRecords: []")
    a("    m_MarkToBaseAdjustmentRecords: []"); a("    m_MarkToMarkAdjustmentRecords: []")
    a("  m_ShouldReimportFontFeatures: 0")
    if fallbacks:
        a("  m_FallbackFontAssetTable:")
        for fid, guid in fallbacks:
            a(f"  - {{fileID: {fid}, guid: {guid}, type: 2}}")
    else:
        a("  m_FallbackFontAssetTable: []")
    a("  m_FontWeightTable:")
    for _ in range(10):
        a("  - regularTypeface: {fileID: 0}"); a("    italicTypeface: {fileID: 0}")
    a("  fontWeights: []")
    a("  normalStyle: 0"); a("  normalSpacingOffset: 0")
    a("  boldStyle: 0.75"); a("  boldSpacing: 7")
    a("  italicStyle: 35"); a("  tabSize: 10")
    a("  m_fontInfo:")
    for k in ("Name: ", "PointSize: 0", "Scale: 0", "CharacterCount: 0", "LineHeight: 0",
              "Baseline: 0", "Ascender: 0", "CapHeight: 0", "Descender: 0", "CenterLine: 0",
              "SuperscriptOffset: 0", "SubscriptOffset: 0", "SubSize: 0", "Underline: 0",
              "UnderlineThickness: 0", "strikethrough: 0", "strikethroughThickness: 0",
              "TabWidth: 0", "Padding: 0", "AtlasWidth: 0", "AtlasHeight: 0"):
        a("    " + k)
    a("  m_glyphInfoList: []")
    a("  m_KerningTable:"); a("    kerningPairs: []")
    a("  fallbackFontAssets: []")
    a("  atlas: {fileID: 0}")

    # ---- material (base preset: no outline, no glow, no bevel) ----
    a(f"--- !u!21 &{mat_id}"); a("Material:")
    a("  serializedVersion: 8")
    for k in ("m_ObjectHideFlags: 0", "m_CorrespondingSourceObject: {fileID: 0}",
              "m_PrefabInstance: {fileID: 0}", "m_PrefabAsset: {fileID: 0}"):
        a("  " + k)
    a(f"  m_Name: {name} Material")
    a(f"  m_Shader: {{fileID: 4800000, guid: {SDF_SHADER}, type: 3}}")
    for k in ("m_Parent: {fileID: 0}", "m_ModifiedSerializedProperties: 0",
              "m_ValidKeywords: []", "m_InvalidKeywords: []", "m_LightmapFlags: 4",
              "m_EnableInstancingVariants: 0", "m_DoubleSidedGI: 0",
              "m_CustomRenderQueue: -1", "stringTagMap: {}", "disabledShaderPasses: []",
              "m_LockedProperties: "):
        a("  " + k)
    a("  m_SavedProperties:"); a("    serializedVersion: 3"); a("    m_TexEnvs:")
    for tex in ("_BumpMap", "_Cube", "_FaceTex"):
        a(f"    - {tex}:"); a("        m_Texture: {fileID: 0}")
        a("        m_Scale: {x: 1, y: 1}"); a("        m_Offset: {x: 0, y: 0}")
    a("    - _MainTex:"); a(f"        m_Texture: {{fileID: {tex_ids[0]}}}")
    a("        m_Scale: {x: 1, y: 1}"); a("        m_Offset: {x: 0, y: 0}")
    a("    - _OutlineTex:"); a("        m_Texture: {fileID: 0}")
    a("        m_Scale: {x: 1, y: 1}"); a("        m_Offset: {x: 0, y: 0}")
    a("    m_Ints: []"); a("    m_Floats:")
    grad = padding + 1
    floats = [("_Ambient", 0.5), ("_Bevel", 0.5), ("_BevelClamp", 0), ("_BevelOffset", 0),
              ("_BevelRoundness", 0), ("_BevelWidth", 0), ("_BumpFace", 0), ("_BumpOutline", 0),
              ("_ColorMask", 15), ("_CullMode", 0), ("_Diffuse", 0.5), ("_FaceDilate", 0),
              ("_FaceUVSpeedX", 0), ("_FaceUVSpeedY", 0), ("_GlowInner", 0.05),
              ("_GlowOffset", 0), ("_GlowOuter", 0.05), ("_GlowPower", 0.75),
              ("_GradientScale", grad), ("_LightAngle", 3.1416), ("_MaskSoftnessX", 0),
              ("_MaskSoftnessY", 0), ("_OutlineSoftness", 0), ("_OutlineUVSpeedX", 0),
              ("_OutlineUVSpeedY", 0), ("_OutlineWidth", 0), ("_PerspectiveFilter", 0.875),
              ("_Reflectivity", 10),
              ("_ScaleRatioA", padding / grad), ("_ScaleRatioB", 1), ("_ScaleRatioC", 0.73125),
              ("_ScaleX", 1), ("_ScaleY", 1), ("_ShaderFlags", 0), ("_Sharpness", 0),
              ("_SpecularPower", 2), ("_Stencil", 0), ("_StencilComp", 8), ("_StencilOp", 0),
              ("_StencilReadMask", 255), ("_StencilWriteMask", 255),
              ("_TextureHeight", atlas_h), ("_TextureWidth", atlas_w),
              ("_UnderlayDilate", 0), ("_UnderlayOffsetX", 0), ("_UnderlayOffsetY", 0),
              ("_UnderlaySoftness", 0), ("_VertexOffsetX", 0), ("_VertexOffsetY", 0),
              ("_WeightBold", 0.75), ("_WeightNormal", 0)]
    for k, v in floats:
        a(f"    - {k}: {fmt(v)}")
    a("    m_Colors:")
    for k, v in (("_ColorOffset", "{r: 0, g: 0, b: 0, a: 0}"),
                 ("_ClipRect", "{r: -32767, g: -32767, b: 32767, a: 32767}"),
                 ("_EnvMatrixRotation", "{r: 0, g: 0, b: 0, a: 0}"),
                 ("_FaceColor", "{r: 1, g: 1, b: 1, a: 1}"),
                 ("_GlowColor", "{r: 0, g: 1, b: 0, a: 0.5}"),
                 ("_OutlineColor", "{r: 0, g: 0, b: 0, a: 1}"),
                 ("_ReflectFaceColor", "{r: 0, g: 0, b: 0, a: 1}"),
                 ("_ReflectOutlineColor", "{r: 0, g: 0, b: 0, a: 1}"),
                 ("_SpecularColor", "{r: 1, g: 1, b: 1, a: 1}"),
                 ("_UnderlayColor", "{r: 0, g: 0, b: 0, a: 0.5}")):
        a(f"    - {k}: {v}")
    a("  m_BuildTextureStacks: []")
    a("  m_AllowLocking: 1")

    # ---- atlas texture(s) ----
    for i, (tid, atl) in enumerate(zip(tex_ids, atlases)):
        tname = atlas_name if i == 0 else f"{atlas_name} {i}"
        a(f"--- !u!28 &{tid}"); a("Texture2D:")
        for k in ("m_ObjectHideFlags: 0", "m_CorrespondingSourceObject: {fileID: 0}",
                  "m_PrefabInstance: {fileID: 0}", "m_PrefabAsset: {fileID: 0}"):
            a("  " + k)
        a(f"  m_Name: {tname}")
        a("  m_ImageContentsHash:"); a("    serializedVersion: 2")
        a("    Hash: 00000000000000000000000000000000")
        a("  m_IsAlphaChannelOptional: 0"); a("  serializedVersion: 3")
        a(f"  m_Width: {atlas_w}"); a(f"  m_Height: {atlas_h}")
        a(f"  m_CompleteImageSize: {atlas_w * atlas_h}")
        a("  m_MipsStripped: 0")
        a("  m_TextureFormat: 1")            # Alpha8
        a("  m_MipCount: 1"); a("  m_IsReadable: 0"); a("  m_IsPreProcessed: 0")
        a("  m_IgnoreMipmapLimit: 1"); a("  m_MipmapLimitGroupName: ")
        a("  m_StreamingMipmaps: 0"); a("  m_StreamingMipmapsPriority: 0")
        a("  m_VTOnly: 0"); a("  m_AlphaIsTransparency: 0")
        a("  m_ImageCount: 1"); a("  m_TextureDimension: 2")
        a("  m_TextureSettings:"); a("    serializedVersion: 2")
        a("    m_FilterMode: 1"); a("    m_Aniso: 1"); a("    m_MipBias: 0")
        a("    m_WrapU: 1"); a("    m_WrapV: 1"); a("    m_WrapW: 0")
        a("  m_LightmapFormat: 0"); a("  m_ColorSpace: 0"); a("  m_PlatformBlob: ")
        a(f"  image data: {atlas_w * atlas_h}")
        a("  _typelessdata: " + atl.tobytes().hex())
        a("  m_StreamData:"); a("    serializedVersion: 2")
        a("    offset: 0"); a("    size: 0"); a("    path: ")
    return "\n".join(L) + "\n", stats


def charset_sequence(charset):
    """Compact 'a-b,c' form TMP writes into m_CreationSettings.characterSequence."""
    out, i = [], 0
    cs = sorted(charset)
    while i < len(cs):
        j = i
        while j + 1 < len(cs) and cs[j + 1] == cs[j] + 1:
            j += 1
        out.append(f"{cs[i]}" if j == i else f"{cs[i]}-{cs[j]}")
        i = j + 1
    return ",".join(out)


# ---------------------------------------------------------------- donor check
FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                       "fixtures", "chakrapetch-regular-sdf-donor.json.gz")


def verify_donor():
    """Re-prove the generator against a real TMP-authored asset.

    The donor is a frozen snapshot (see the fixture's own _comment) rather than a live
    asset: the ChakraPetch-Regular SDF it was taken from was a duplicate of a font the
    project now owns, sitting in the package-reimport-exposed TMP folder, and was deleted
    under T5 cleanup. The snapshot keeps this proof reproducible without keeping the
    duplicate. It carries the atlas too, so the SDF pixels are checked, not just the tables.
    """
    import gzip, json, base64, zlib
    with gzip.open(FIXTURE, "rt", encoding="utf-8") as f:
        fx = json.load(f)
    pad, pt, aw = fx["padding"], fx["pointSize"], fx["atlasWidth"]
    cs = []
    for part in fx["characterSequence"].split(","):
        part = part.strip()
        if "-" in part.strip("-") and not part.startswith("-"):
            lo, hi = part.split("-"); cs += list(range(int(lo), int(hi) + 1))
        else:
            cs.append(int(part))
    ttf = os.path.join(FONT_DIR, "ChakraPetch", "ChakraPetch-Regular.ttf")
    got, stats = build_font_asset(ttf, "ChakraPetch-Regular SDF", pt, pad, aw, aw,
                                  sorted(set(cs)),
                                  src_font_guid=stable_guid("ttf/ChakraPetch-Regular"))
    ok = True

    fg = {k: float(v) for k, v in re.findall(
        r"^    (m_\w+): (-?[\d.E-]+)$", got.split("m_FaceInfo:")[1].split("  m_Material:")[0], re.M)}
    bad = [(k, v, fg.get(k)) for k, v in fx["face"].items() if abs(v - fg.get(k, 1e9)) > 0.002]
    print(f"FaceInfo  : {len(fx['face']) - len(bad)}/{len(fx['face'])} fields match")
    for k, r, g in bad:
        print(f"    MISMATCH {k}: donor={r} generated={g}"); ok = False

    def tables(txt):
        gl = {int(b[0]): [round(float(x), 4) for x in b[1:6]] + [int(x) for x in b[6:]]
              for b in re.findall(
            r"- m_Index: (\d+)\n    m_Metrics:\n      m_Width: (-?[\d.E-]+)\n      m_Height: (-?[\d.E-]+)\n"
            r"      m_HorizontalBearingX: (-?[\d.E-]+)\n      m_HorizontalBearingY: (-?[\d.E-]+)\n"
            r"      m_HorizontalAdvance: (-?[\d.E-]+)\n    m_GlyphRect:\n      m_X: (-?\d+)\n"
            r"      m_Y: (-?\d+)\n      m_Width: (-?\d+)\n      m_Height: (-?\d+)", txt)}
        ch = {int(u): int(i) for u, i in re.findall(
            r"- m_ElementType: 1\n    m_Unicode: (\d+)\n    m_GlyphIndex: (\d+)", txt)}
        return gl, ch

    ggl, gch = tables(got)
    dgl = {int(k): v for k, v in fx["glyphTable"].items()}
    dch = {int(k): v for k, v in fx["charTable"].items()}
    shared = sorted(set(dch) & set(gch))
    # metrics + rect SIZE (rect position is the packer's, and need not match)
    gbad = [u for u in shared
            if [round(x, 4) for x in dgl[dch[u]][:5]] + dgl[dch[u]][7:9] !=
               ggl[gch[u]][:5] + ggl[gch[u]][7:9]]
    print(f"Glyphs    : {len(shared) - len(gbad)}/{len(shared)} metric+rect-size match")
    for u in gbad[:8]:
        print(f"    MISMATCH U+{u:04X} donor={dgl[dch[u]]} generated={ggl[gch[u]]}"); ok = False
    if set(dch) != set(gch):
        print(f"    CHAR TABLE differs: {sorted(set(dch) ^ set(gch))}"); ok = False
    else:
        print(f"Chars     : {len(dch)} identical")

    kg = re.findall(r"^  (\w+):", got.split("MonoBehaviour:")[1].split("--- !u!")[0], re.M)
    if kg != fx["monoKeys"]:
        print(f"    KEY PARITY differs: donor-only {sorted(set(fx['monoKeys']) - set(kg))} "
              f"generated-only {sorted(set(kg) - set(fx['monoKeys']))}"); ok = False
    else:
        print(f"Keys      : {len(kg)} top-level keys, same names AND same order")

    # ---- SDF pixels, against the donor's own atlas ----
    atlas = np.frombuffer(zlib.decompress(base64.b64decode(fx["atlasZlibB64"])),
                          dtype=np.uint8).reshape(fx["atlasHeight"], aw)
    face = freetype.Face(ttf)
    errs = []
    for u in shared:
        _, _, _, _, _, rx, ry, rw, rh = dgl[dch[u]]
        if not rw or not rh:
            continue
        ref = atlas[ry - pad:ry + rh + pad, rx - pad:rx + rw + pad].astype(int)
        _, tile = render_glyph(face, u, pt, pad)
        if tile is None or np.flipud(tile).shape != ref.shape:
            continue
        errs.append(np.abs(np.flipud(tile).astype(int) - ref).mean())
    mean = float(np.mean(errs))
    step = 2.0 * (pad + 1) / 255.0
    print(f"SDF pixels: mean |dalpha| = {mean:.3f}/255 over {len(errs)} glyphs "
          f"= {mean / 255 * 2 * (pad + 1):.4f} px (one alpha step = {step:.4f} px)")
    if mean / 255 * 2 * (pad + 1) > step:
        print("    SDF error exceeds one alpha quantisation step"); ok = False

    print(f"Atlases   : {stats['atlases']}  fill={['%.1f%%' % (f * 100) for f in stats['fill']]}")
    print("\nDONOR MODEL VALIDATED" if ok else "\nDONOR MODEL **NOT** VALIDATED")
    return 0 if ok else 1


# ------------------------------------------------------------------ fetch/build
LIB_SANS       = "8f586378b4e144a9851e7b34d9b748ee"      # LiberationSans SDF
LIB_SANS_DYN   = "2e498d1c8094910479dc3e1b768306a4"      # LiberationSans SDF - Fallback (DYNAMIC)
GH             = "https://raw.githubusercontent.com/google/fonts/main"
OFL_DIR        = {"ChakraPetch": "ofl/chakrapetch",
                  "SpaceGrotesk": "ofl/spacegrotesk",
                  "JetBrainsMono": "ofl/jetbrainsmono"}
# Space Grotesk and JetBrains Mono ship variable-only in google/fonts; Google Fonts'
# own per-weight STATIC instances come from gstatic (woff wrapper, unwrapped to ttf).
CSS_QUERY      = {"SpaceGrotesk": "Space+Grotesk", "JetBrainsMono": "JetBrains+Mono"}
UA_LEGACY      = ("Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 "
                  "(KHTML, like Gecko) Chrome/27.0.1453.116 Safari/537.36")


def _meta(guid, body):
    return f"fileFormatVersion: 2\nguid: {guid}\n{body}"


def folder_meta(guid):
    return _meta(guid, "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                       "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def ttf_meta(guid, family):
    return _meta(guid,
        "TrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n"
        "  fontSize: 16\n  forceTextureCase: -2\n  characterSpacing: 0\n"
        "  characterPadding: 1\n  includeFontData: 1\n  fontNames:\n"
        f"  - {family}\n  fallbackFontReferences: []\n  customCharacters: \n"
        "  fontRenderingMode: 0\n  ascentCalculationMode: 1\n"
        "  useLegacyBoundsCalculation: 0\n  shouldRoundAdvanceValue: 1\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def asset_meta(guid):
    return _meta(guid, "NativeFormatImporter:\n  externalObjects: {}\n"
                       "  mainObjectFileID: 11400000\n  userData: \n"
                       "  assetBundleName: \n  assetBundleVariant: \n")


def text_meta(guid):
    return _meta(guid, "TextScriptImporter:\n  externalObjects: {}\n  userData: \n"
                       "  assetBundleName: \n  assetBundleVariant: \n")


def fetch():
    import io, urllib.request, re as _re
    from fontTools.ttLib import TTFont as _TT

    def get(url, ua=None):
        req = urllib.request.Request(url, headers={'User-Agent': ua or 'Mozilla/5.0'})
        return urllib.request.urlopen(req, timeout=90).read()

    for family, (stem, weights, display) in FAMILIES.items():
        d = os.path.join(FONT_DIR, family)
        os.makedirs(d, exist_ok=True)
        with open(d + ".meta", "w") as f:
            f.write(folder_meta(stable_guid(f"dir/{family}")))
        blobs = {}
        if family == "ChakraPetch":
            for w in weights:
                blobs[w] = get(f"{GH}/{OFL_DIR[family]}/{stem}-{WEIGHT_NAME[w]}.ttf")
        else:
            css = get("https://fonts.googleapis.com/css2?family="
                      f"{CSS_QUERY[family]}:wght@{';'.join(map(str, weights))}",
                      UA_LEGACY).decode()
            faces = _re.findall(r"font-weight:\s*(\d+);\s*src:\s*url\(([^)]+)\)", css)
            assert len(faces) == len(weights), f"{family}: {len(faces)} faces vs {len(weights)}"
            for ws, url in faces:
                blobs[int(ws)] = get(url)
        for w, data in blobs.items():
            path = os.path.join(d, f"{stem}-{WEIGHT_NAME[w]}.ttf")
            # recalcTimestamp=False: fontTools otherwise stamps head.modified with the
            # save time, so every --fetch rewrote these files with new bytes (and a new
            # checkSumAdjustment) for no change in the font. recalcBBoxes=False keeps the
            # source's own glyph bboxes, so unwrapping changes the container and nothing else.
            fo = _TT(io.BytesIO(data), recalcTimestamp=False, recalcBBoxes=False)
            if fo.flavor:                          # woff wrapper -> plain ttf
                fo.flavor = None                   # (outlines untouched; only the container)
                fo.save(path)
            else:                                  # already a TTF: ship upstream's bytes
                with open(path, "wb") as raw:      # verbatim, so the shipped OFL font is
                    raw.write(data)                # bit-for-bit what google/fonts serves
            fam = fo['name'].getDebugName(1) or display
            with open(path + ".meta", "w") as f:
                f.write(ttf_meta(stable_guid(f"ttf/{stem}-{WEIGHT_NAME[w]}"), fam))
            print(f"  {os.path.relpath(path, ROOT)}")
        lic = os.path.join(d, "OFL.txt")
        with open(lic, "wb") as f:
            f.write(get(f"{GH}/{OFL_DIR[family]}/OFL.txt"))
        with open(lic + ".meta", "w") as f:
            f.write(text_meta(stable_guid(f"ofl/{family}")))
        print(f"  {os.path.relpath(lic, ROOT)}")


def fallbacks_for(family, weight):
    """Space Grotesk -> Chakra Petch (weight-matched). Liberation Sans and the
    DYNAMIC overflow fallback are global, set in TMP Settings, so every family
    inherits them without duplicating the tail of the chain on 11 assets."""
    if family != "SpaceGrotesk":
        return []
    cp = weight if weight in FAMILIES["ChakraPetch"][1] else 400    # CP has no Light
    return [(11400000, stable_guid(f"asset/ChakraPetch-{WEIGHT_NAME[cp]} SDF"))]


def all_targets():
    for family, (stem, weights, _display) in FAMILIES.items():
        for w in weights:
            base = f"{stem}-{WEIGHT_NAME[w]}"
            yield dict(family=family, weight=w, stem=base,
                       ttf=os.path.join(FONT_DIR, family, base + ".ttf"),
                       out=os.path.join(FONT_DIR, family, asset_name(base) + ".asset"),
                       name=asset_name(base))


def build(write=True):
    results, diffs = [], []
    for tg in all_targets():
        if not os.path.exists(tg['ttf']):
            raise SystemExit(f"missing {tg['ttf']} -- run --fetch first")
        guid = stable_guid(f"asset/{tg['name']}")
        src  = stable_guid(f"ttf/{tg['stem']}")
        yaml, stats = build_font_asset(
            tg['ttf'], tg['name'], src_font_guid=src,
            fallbacks=fallbacks_for(tg['family'], tg['weight']))
        results.append((tg, stats))
        if write:
            with open(tg['out'], "w") as f:
                f.write(yaml)
            with open(tg['out'] + ".meta", "w") as f:
                f.write(asset_meta(guid))
        else:
            cur = open(tg['out']).read() if os.path.exists(tg['out']) else None
            if cur != yaml:
                diffs.append(tg['name'])
        miss = " ".join(f"U+{c:04X}" for c in stats['missing'])
        print(f"  {tg['name']:34s} glyphs={stats['glyphs']:3d} atlas={stats['atlases']} "
              f"fill={stats['fill'][0]*100:5.1f}%  missing: {miss or '-'}")
    return results, diffs


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--verify-donor", action="store_true")
    ap.add_argument("--fetch", action="store_true")
    ap.add_argument("--build", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    if args.verify_donor:
        sys.exit(verify_donor())
    if args.fetch:
        print("fetching fonts + licences:"); fetch()
    if args.build:
        print("building font assets:"); build(write=True)
    if args.check:
        print("checking committed assets against this generator:")
        _, diffs = build(write=False)
        if diffs:
            print("\nOUT OF DATE:", ", ".join(diffs)); sys.exit(1)
        print("\nall committed assets match the generator")
    if not (args.fetch or args.build or args.check):
        print("nothing to do; pass --verify-donor, --fetch, --build or --check")
