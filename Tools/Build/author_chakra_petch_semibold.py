#!/usr/bin/env python3
"""
Author the Chakra Petch SemiBold TMP font asset - the BUTTON and LABEL face of
Docs/STYLE_FOUNDATION.md §4 (UI redesign task T5; style-B menu redesign decision D3).

WHY IT IS NEW: §4 sets every button and caps label in Chakra Petch SemiBold, and until this the
project carried only Chakra Petch REGULAR. The TTF is the Google Fonts release (SIL OFL 1.1,
https://github.com/google/fonts/tree/main/ofl/chakrapetch), the same "Version 1.000" build as the
Regular already shipped beside TMP. The TTF and its OFL.txt are committed as-is; this script owns
only the files Unity would otherwise generate: the TMP font asset and the three .meta files.

WHY DYNAMIC: a STATIC TMP asset carries a baked SDF atlas, which only TMP's Font Asset Creator can
render faithfully. A DYNAMIC asset carries no glyphs at all - TMP rasterises each glyph from the
TTF the first time it is drawn, with TMP's own SDF code - so nothing here can be subtly off in the
atlas. It is donor-cloned from the shipped dynamic `Bangers SDF.asset` (1x1 atlas, empty tables),
with T5's creation settings: SDFAA, sampling 90, padding 9, 1024 x 1024.

THE ONE COST OF DYNAMIC: the first time the editor draws a glyph it writes that glyph into this
asset, so after a play session the asset shows as modified. That is a one-time fill (97 glyphs
cover every string the menu can show); commit the filled asset, or set Atlas Population Mode to
Static in the font asset's inspector once it is filled. `--check` reports the filled asset as
drift on purpose - re-run without `--check` only to reset it.

FACE METRICS come from the TTF's own tables (hhea, OS/2, hmtx), read with fontTools when present
and pinned below otherwise, scaled to the 90 pt sampling size the same way TMP derived the Regular
asset's (LineHeight = 1.3 em, Ascent = 0.992 em, Descent = -0.308 em, ...).

    python3 Tools/Build/author_chakra_petch_semibold.py           # write the asset + metas
    python3 Tools/Build/author_chakra_petch_semibold.py --check   # fail if any drifted
"""

import argparse
import hashlib
import math
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIR = os.path.join(REPO, "Assets", "_Graphics", "Fonts", "ChakraPetch")
TTF = "ChakraPetch-SemiBold.ttf"
OFL = "OFL.txt"
ASSET = "ChakraPetch-SemiBold SDF.asset"
TTF_MD5 = "48b8e73ed63a12cbfc31dae112f36bd9"   # pinned Google Fonts release; see main()

POINT_SIZE = 90
PADDING = 9
ATLAS = 1024

# Read from ChakraPetch-SemiBold.ttf (unitsPerEm 1000). Pinned so the script runs without
# fontTools; verified against the file when fontTools is available.
UPM = 1000
HHEA_ASCENT, HHEA_DESCENT, HHEA_GAP = 992, -308, 0
CAP_HEIGHT, X_HEIGHT = 700, 498
SPACE_ADVANCE = 263

TMP_FONT_ASSET_SCRIPT = "71c1514a6bd24e1e882cebbe1904ce04"   # TMP_FontAsset (from shipped assets)
TMP_SDF_SHADER = "68e6db2ebdc24f95958faec2be5558d6"          # TextMeshPro/Distance Field

MATERIAL_ID = 4519786210045101501
ATLAS_ID = 4519786210045101502


def guid_for(name):
    return hashlib.md5(f"CosmicShore/Fonts/ChakraPetchSemiBold/{name}".encode()).hexdigest()


def verify_metrics():
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        print("  NOTE: fontTools missing - pinned face metrics NOT re-verified this run")
        return
    f = TTFont(os.path.join(DIR, TTF))
    hh, os2 = f["hhea"], f["OS/2"]
    space = f["hmtx"][f.getBestCmap()[32]][0]
    got = (f["head"].unitsPerEm, hh.ascent, hh.descent, hh.lineGap, os2.sCapHeight, os2.sxHeight,
           space)
    want = (UPM, HHEA_ASCENT, HHEA_DESCENT, HHEA_GAP, CAP_HEIGHT, X_HEIGHT, SPACE_ADVANCE)
    assert got == want, f"TTF metrics {got} != pinned {want}"


def face_info():
    s = POINT_SIZE / UPM
    ascent = HHEA_ASCENT * s
    descent = HHEA_DESCENT * s
    mean = math.ceil(X_HEIGHT * s)
    return dict(
        line=(HHEA_ASCENT - HHEA_DESCENT + HHEA_GAP) * s,
        ascent=ascent, descent=descent,
        cap=math.ceil(CAP_HEIGHT * s), mean=mean,
        underline=-0.1 * POINT_SIZE, thickness=0.05 * POINT_SIZE,
        strike=mean * 0.4, tab=math.ceil(SPACE_ADVANCE * s))


def g(v):
    """Unity's float spelling: integers bare, others shortest round-trip."""
    return str(int(v)) if float(v).is_integer() else repr(float(round(v, 6)))


def font_asset():
    fi = face_info()
    weights = "".join("  - regularTypeface: {fileID: 0}\n    italicTypeface: {fileID: 0}\n"
                      for _ in range(10))
    ttf_guid = guid_for(TTF)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &{MATERIAL_ID}
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: ChakraPetch-SemiBold SDF Material
  m_Shader: {{fileID: 4800000, guid: {TMP_SDF_SHADER}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BumpMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _Cube:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _FaceTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {{fileID: {ATLAS_ID}}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _OutlineTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _Ambient: 0.5
    - _Bevel: 0.5
    - _BevelClamp: 0
    - _BevelOffset: 0
    - _BevelRoundness: 0
    - _BevelWidth: 0
    - _BumpFace: 0
    - _BumpOutline: 0
    - _ColorMask: 15
    - _CullMode: 0
    - _Diffuse: 0.5
    - _FaceDilate: 0
    - _FaceUVSpeedX: 0
    - _FaceUVSpeedY: 0
    - _GlowInner: 0.05
    - _GlowOffset: 0
    - _GlowOuter: 0.05
    - _GlowPower: 0.75
    - _GradientScale: {PADDING + 1}
    - _LightAngle: 3.1416
    - _MaskSoftnessX: 0
    - _MaskSoftnessY: 0
    - _OutlineSoftness: 0
    - _OutlineUVSpeedX: 0
    - _OutlineUVSpeedY: 0
    - _OutlineWidth: 0
    - _PerspectiveFilter: 0.875
    - _Reflectivity: 10
    - _ScaleRatioA: 0.9
    - _ScaleRatioB: 0.73125
    - _ScaleRatioC: 0.73125
    - _ScaleX: 1
    - _ScaleY: 1
    - _ShaderFlags: 0
    - _Sharpness: 0
    - _SpecularPower: 2
    - _Stencil: 0
    - _StencilComp: 8
    - _StencilOp: 0
    - _StencilReadMask: 255
    - _StencilWriteMask: 255
    - _TextureHeight: {ATLAS}
    - _TextureWidth: {ATLAS}
    - _UnderlayDilate: 0
    - _UnderlayOffsetX: 0
    - _UnderlayOffsetY: 0
    - _UnderlaySoftness: 0
    - _VertexOffsetX: 0
    - _VertexOffsetY: 0
    - _WeightBold: 0.75
    - _WeightNormal: 0
    m_Colors:
    - _ClipRect: {{r: -32767, g: -32767, b: 32767, a: 32767}}
    - _EnvMatrixRotation: {{r: 0, g: 0, b: 0, a: 0}}
    - _FaceColor: {{r: 1, g: 1, b: 1, a: 1}}
    - _GlowColor: {{r: 0, g: 1, b: 0, a: 0.5}}
    - _MaskCoord: {{r: 0, g: 0, b: 32767, a: 32767}}
    - _OutlineColor: {{r: 0, g: 0, b: 0, a: 1}}
    - _ReflectFaceColor: {{r: 0, g: 0, b: 0, a: 1}}
    - _ReflectOutlineColor: {{r: 0, g: 0, b: 0, a: 1}}
    - _SpecularColor: {{r: 1, g: 1, b: 1, a: 1}}
    - _UnderlayColor: {{r: 0, g: 0, b: 0, a: 0.5}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TMP_FONT_ASSET_SCRIPT}, type: 3}}
  m_Name: ChakraPetch-SemiBold SDF
  m_EditorClassIdentifier:
  m_Version: 1.1.0
  m_FaceInfo:
    m_FaceIndex: 0
    m_FamilyName: Chakra Petch
    m_StyleName: SemiBold
    m_PointSize: {POINT_SIZE}
    m_Scale: 1
    m_UnitsPerEM: {UPM}
    m_LineHeight: {g(fi['line'])}
    m_AscentLine: {g(fi['ascent'])}
    m_CapLine: {g(fi['cap'])}
    m_MeanLine: {g(fi['mean'])}
    m_Baseline: 0
    m_DescentLine: {g(fi['descent'])}
    m_SuperscriptOffset: {g(fi['ascent'])}
    m_SuperscriptSize: 0.5
    m_SubscriptOffset: {g(fi['descent'])}
    m_SubscriptSize: 0.5
    m_UnderlineOffset: {g(fi['underline'])}
    m_UnderlineThickness: {g(fi['thickness'])}
    m_StrikethroughOffset: {g(fi['strike'])}
    m_StrikethroughThickness: {g(fi['thickness'])}
    m_TabWidth: {g(fi['tab'])}
  m_Material: {{fileID: {MATERIAL_ID}}}
  m_SourceFontFileGUID: {ttf_guid}
  m_CreationSettings:
    sourceFontFileName:
    sourceFontFileGUID: {ttf_guid}
    faceIndex: 0
    pointSizeSamplingMode: 1
    pointSize: {POINT_SIZE}
    padding: {PADDING}
    paddingMode: 0
    packingMode: 4
    atlasWidth: {ATLAS}
    atlasHeight: {ATLAS}
    characterSetSelectionMode: 7
    characterSequence:
    referencedFontAssetGUID:
    referencedTextAssetGUID:
    fontStyle: 0
    fontStyleModifier: 0
    renderMode: 4165
    includeFontFeatures: 1
  m_SourceFontFile: {{fileID: 12800000, guid: {ttf_guid}, type: 3}}
  m_SourceFontFilePath:
  m_AtlasPopulationMode: 1
  InternalDynamicOS: 0
  m_GlyphTable: []
  m_CharacterTable: []
  m_AtlasTextures:
  - {{fileID: {ATLAS_ID}}}
  m_AtlasTextureIndex: 0
  m_IsMultiAtlasTexturesEnabled: 0
  m_GetFontFeatures: 1
  m_ClearDynamicDataOnBuild: 0
  m_AtlasWidth: {ATLAS}
  m_AtlasHeight: {ATLAS}
  m_AtlasPadding: {PADDING}
  m_AtlasRenderMode: 4165
  m_UsedGlyphRects: []
  m_FreeGlyphRects:
  - m_X: 0
    m_Y: 0
    m_Width: {ATLAS - 1}
    m_Height: {ATLAS - 1}
  m_FontFeatureTable:
    m_MultipleSubstitutionRecords: []
    m_LigatureSubstitutionRecords: []
    m_GlyphPairAdjustmentRecords: []
    m_MarkToBaseAdjustmentRecords: []
    m_MarkToMarkAdjustmentRecords: []
  m_ShouldReimportFontFeatures: 0
  m_FallbackFontAssetTable: []
  m_FontWeightTable:
{weights}  fontWeights:
{weights}  normalStyle: 0
  normalSpacingOffset: 0
  boldStyle: 0.75
  boldSpacing: 7
  italicStyle: 35
  tabSize: 10
  m_fontInfo:
    Name:
    PointSize: 0
    Scale: 0
    CharacterCount: 0
    LineHeight: 0
    Baseline: 0
    Ascender: 0
    CapHeight: 0
    Descender: 0
    CenterLine: 0
    SuperscriptOffset: 0
    SubscriptOffset: 0
    SubSize: 0
    Underline: 0
    UnderlineThickness: 0
    strikethrough: 0
    strikethroughThickness: 0
    TabWidth: 0
    Padding: 0
    AtlasWidth: 0
    AtlasHeight: 0
  m_glyphInfoList: []
  m_KerningTable:
    kerningPairs: []
  fallbackFontAssets: []
  atlas: {{fileID: 0}}
--- !u!28 &{ATLAS_ID}
Texture2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: ChakraPetch-SemiBold SDF Atlas
  m_ImageContentsHash:
    serializedVersion: 2
    Hash: 00000000000000000000000000000000
  m_IsAlphaChannelOptional: 0
  serializedVersion: 3
  m_Width: 1
  m_Height: 1
  m_CompleteImageSize: 1
  m_MipsStripped: 0
  m_TextureFormat: 1
  m_MipCount: 1
  m_IsReadable: 1
  m_IsPreProcessed: 0
  m_IgnoreMipmapLimit: 0
  m_MipmapLimitGroupName:
  m_StreamingMipmaps: 0
  m_StreamingMipmapsPriority: 0
  m_VTOnly: 0
  m_AlphaIsTransparency: 0
  m_ImageCount: 1
  m_TextureDimension: 2
  m_TextureSettings:
    serializedVersion: 2
    m_FilterMode: 1
    m_Aniso: 1
    m_MipBias: 0
    m_WrapU: 0
    m_WrapV: 0
    m_WrapW: 0
  m_LightmapFormat: 0
  m_ColorSpace: 0
  m_PlatformBlob:
  image data: 1
  _typelessdata: 00
  m_StreamData:
    serializedVersion: 2
    offset: 0
    size: 0
    path:
"""


# Metas. Trailing spaces on empty values are deliberate (Unity's own spelling).
ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TTF_META = """fileFormatVersion: 2
guid: {guid}
TrueTypeFontImporter:
  externalObjects: {{}}
  serializedVersion: 4
  fontSize: 16
  forceTextureCase: -2
  characterSpacing: 0
  characterPadding: 1
  includeFontData: 1
  fontNames:
  - Chakra Petch
  fallbackFontReferences: []
  customCharacters:
  fontRenderingMode: 0
  ascentCalculationMode: 1
  useLegacyBoundsCalculation: 0
  shouldRoundAdvanceValue: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
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


def unity_empty_scalars(text):
    """Re-add the trailing space Unity writes after an EMPTY scalar (`key: `). A bare `key:` is a
    PARENT when the next line is indented deeper, or is a `- ` item at the same indent (Unity's
    block-sequence spelling); otherwise it is an empty scalar. Without this a file is rewritten by
    Unity on first import and `--check` reports drift nobody caused."""
    lines = text.split("\n")
    out = []
    for i, line in enumerate(lines):
        if re.match(r"^\s*[^\s#][^:]*:$", line) and not line.lstrip().startswith("- "):
            indent = len(line) - len(line.lstrip())
            nxt = lines[i + 1] if i + 1 < len(lines) else ""
            n_indent = len(nxt) - len(nxt.lstrip())
            parent = nxt.strip() != "" and (n_indent > indent or
                                            (n_indent == indent and nxt.lstrip().startswith("- ")))
            if not parent:
                line += " "
        out.append(line)
    return "\n".join(out)


def outputs():
    return {p: unity_empty_scalars(t.decode()).encode() for p, t in _raw_outputs().items()}


def _raw_outputs():
    return {
        os.path.join(DIR, ASSET): font_asset().encode(),
        os.path.join(DIR, ASSET + ".meta"): ASSET_META.format(guid=guid_for(ASSET)).encode(),
        os.path.join(DIR, TTF + ".meta"): TTF_META.format(guid=guid_for(TTF)).encode(),
        os.path.join(DIR, OFL + ".meta"): TEXT_META.format(guid=guid_for(OFL)).encode(),
        DIR + ".meta": FOLDER_META.format(guid=guid_for("__folder__")).encode(),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    for f in (TTF, OFL):
        if not os.path.exists(os.path.join(DIR, f)):
            sys.exit(f"missing {f} in {os.path.relpath(DIR, REPO)} - copy it from "
                     "https://github.com/google/fonts/tree/main/ofl/chakrapetch first")
    md5 = hashlib.md5(open(os.path.join(DIR, TTF), "rb").read()).hexdigest()
    assert md5 == TTF_MD5, f"{TTF} is not the pinned Google Fonts release (md5 {md5})"
    verify_metrics()

    out = outputs()
    if args.check:
        drift = [p for p, want in out.items()
                 if not os.path.exists(p) or open(p, "rb").read() != want]
        for p in drift:
            print("DRIFT:", os.path.relpath(p, REPO))
        if not drift:
            print("OK: Chakra Petch SemiBold asset matches the generator")
        return 1 if drift else 0

    for p, data in out.items():
        with open(p, "wb") as fh:
            fh.write(data)
    print(f"wrote {len(out)} files under {os.path.relpath(DIR, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
