#!/usr/bin/env python3
"""Bake the HyperSea sky OFFLINE into an equirectangular panorama the strip can afford.

WHY THIS EXISTS
---------------
The authored sky (Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader) is a 767-line
procedural fragment shader - two 3x3x3 Voronoi searches and ~20 octaves of value noise PER
PIXEL - one of the largest GPU costs on a budget phone. The MobileLow device tier swaps it for
this bake: PlatformProfile_MobileLow.asset lists HyperSeaSkybox.mat -> StaticHyperSeaSkybox.mat in
its skybox replacements (Docs/PLATFORM_UNIFICATION.md, Step 4). Desktop and MobileHigh keep the
procedural sky.

Originally written on the Android strip branch (claude/android-performance-stripped-dap5z2), where
an editor-side cubemap bake had been tried first and retired. This one needs no editor: the sky is pure math with no
textures, so it COMPILES the shipped shader's CGINCLUDE block as C++ (a small HLSL shim, see
static_skybox_harness/hlsl_shim.h), evaluates it per texel with the shipped MATERIAL's values, and
writes the result as one PNG. The runtime cost becomes one texture sample per pixel
(StaticSkyPanorama.shader), and inside a cell the opaque membrane early-z rejects even that.

  * The shader is transpiled, not transcribed: anything new it starts using fails to compile here.
  * Material colours are linearized exactly as Unity uploads them in a Linear project
    (m_ActiveColorSpace 1) - only properties the shader declares as Color, never Vector.
  * MobileLow renders LDR (its profile turns HDR off), so clamping to [0, 1] and storing 8-bit
    sRGB is what the procedural shader would have put on screen anyway.
  * Static: time 0 (the authored drift and twinkle are animation; a bake has none).
  * 3x3 supersampled in linear light per texel; a foreground star smaller than a texel is
    therefore averaged, not point-sampled - fainter than the procedural's per-pixel sparkle.

Usage:
  python3 Tools/Build/bake_static_skybox.py            # bake + author the assets
  python3 Tools/Build/bake_static_skybox.py --check    # fail if the bake is stale
  python3 Tools/Build/bake_static_skybox.py --preview OUT.png --size 1024   # look at it
"""

import argparse
import hashlib
import inspect
import json
import math
import os
import re
import struct
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
assert (ROOT / "Assets").is_dir(), f"ROOT resolved to {ROOT}, which has no Assets/"

SHADER = ROOT / "Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader"
MATERIAL = ROOT / "Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat"
SHIM = ROOT / "Tools/Build/static_skybox_harness/hlsl_shim.h"
STAMP = ROOT / "Tools/Build/static_skybox_bake.json"

PANO_SHADER = ROOT / "Assets/_Graphics/Materials/Shaders/StaticSkyPanorama.shader"
PANO_PNG = ROOT / "Assets/_Graphics/Skyboxes/StaticHyperSeaSky.png"
PANO_MAT = ROOT / "Assets/_Graphics/Skyboxes/StaticHyperSeaSkybox.mat"  # a PlatformProfileSO skybox swap

SHADER_GUID = "ab68fa1bd374aa8ce953141437b86c68"
PNG_GUID = "1b949062bcfb1f515313a19f5c981d1c"
MAT_GUID = "9852d08c40940fd6ab90ce838bbe2a1c"

WIDTH, HEIGHT, SUPERSAMPLE = 4096, 2048, 3

INTRINSICS = ("frac", "floor", "dot", "cross", "length", "normalize", "lerp", "saturate",
              "smoothstep", "exp", "pow", "sin", "cos", "atan2", "sqrt", "abs", "max", "min")


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8")


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


# ------------------------------------------------------------------------- shader -> C++

def transpile(shader_text: str) -> str:
    m = re.search(r"^\s*CGINCLUDE\s*$(.*?)^\s*ENDCG\s*$", shader_text, re.S | re.M)
    if not m:
        sys.exit("no CGINCLUDE block in HyperSeaSkybox.shader")
    body = m.group(1)
    body = re.sub(r'#include\s+"UnityCG\.cginc"', "", body)
    body = re.sub(r"struct\s+(appdata|v2f)\s*\{.*?\};", "", body, flags=re.S)
    body = re.sub(r"(?<=[\w\)\]])\.(xyz|yxz|rgb)\b", r".\1()", body)
    body = re.sub(r"\b(" + "|".join(INTRINSICS) + r")\s*\(", r"h_\1(", body)
    leftover = re.findall(r"\.(xy|zw|rg|ba|xz|yz|xw)\b", body)
    if leftover:
        sys.exit(f"the shader now uses swizzles this shim does not know: {sorted(set(leftover))}")
    return body


def srgb_to_linear(c: float) -> float:
    # Mathf.GammaToLinearSpace: the sRGB curve, extended for HDR values above 1.
    if c <= 0.04045:
        return c / 12.92
    if c < 1.0:
        return ((c + 0.055) / 1.055) ** 2.4
    return c ** 2.2


def material_inits(shader_text: str, mat_text: str) -> str:
    """C++ assignments for every shader property, from the material (default when absent)."""
    props = {}
    for name, kind, default in re.findall(
            r"^\s*(?:\[[^\]]*\])*\s*(_\w+)\s*\(\s*\"[^\"]*\"\s*,\s*(Color|Vector|Float|Range\([^)]*\))\s*\)\s*=\s*([^\n]+)",
            shader_text, re.M):
        props[name] = (kind.split("(")[0], default.strip())

    floats = dict(re.findall(r"^\s*- (_\w+): (-?[\d.eE+-]+)\s*$", mat_text, re.M))
    colors = {n: tuple(float(v) for v in vals) for n, *vals in re.findall(
        r"^\s*- (_\w+): \{r: (-?[\d.eE+-]+), g: (-?[\d.eE+-]+), b: (-?[\d.eE+-]+), a: (-?[\d.eE+-]+)\}",
        mat_text, re.M)}

    out = []
    for name, (kind, default) in props.items():
        if kind in ("Color", "Vector"):
            if name in colors:
                v = list(colors[name])
            else:
                v = [float(x) for x in re.findall(r"-?[\d.]+", default)][:4]
                v += [0.0] * (4 - len(v))
            if kind == "Color":
                v = [srgb_to_linear(c) for c in v[:3]] + [v[3]]
            out.append(f"    {name} = float4({v[0]!r}f, {v[1]!r}f, {v[2]!r}f, {v[3]!r}f);")
        else:
            val = float(floats[name]) if name in floats else float(re.findall(r"-?[\d.]+", default)[0])
            out.append(f"    {name} = {val!r}f;")
    return "\n".join(out)


MAIN = r"""
#include <thread>
#include <vector>
#include <cstdio>
#include <cstdint>
#include <cstdlib>

static inline float linear_to_srgb(float c)
{
    c = c < 0.0f ? 0.0f : (c > 1.0f ? 1.0f : c);
    return c <= 0.0031308f ? c * 12.92f : 1.055f * std::pow(c, 1.0f / 2.4f) - 0.055f;
}

// Deterministic triangular dither of +-1 LSB, so the dark nebula gradients do not band.
static inline float dither(uint32_t x, uint32_t y, uint32_t ch)
{
    uint32_t h = x * 73856093u ^ y * 19349663u ^ ch * 83492791u;
    h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
    float a = (h & 0xffffu) / 65535.0f, b = (h >> 16) / 65535.0f;
    return a + b - 1.0f;
}

int main(int argc, char** argv)
{
    const int W = std::atoi(argv[1]), H = std::atoi(argv[2]), SS = std::atoi(argv[3]);
    init_material();
    _Time = float4(0, 0, 0, 0);
    std::vector<uint8_t> rows((size_t)H * W * 3);
    const float PI = 3.14159265358979f;

    auto work = [&](int y0, int step) {
        for (int r = y0; r < H; r += step)
        for (int i = 0; i < W; i++)
        {
            float acc[3] = {0, 0, 0};
            for (int sy = 0; sy < SS; sy++)
            for (int sx = 0; sx < SS; sx++)
            {
                // PNG row r is texture v = 1 - (r + 0.5)/H (Unity's v runs bottom-up).
                float u = (i + (sx + 0.5f) / SS) / W;
                float v = 1.0f - (r + (sy + 0.5f) / SS) / H;
                // Inverse of StaticSkyPanorama.shader: u = 0.5 - atan2(z,x)/2pi, v = 1 - acos(y)/pi.
                float lon = (0.5f - u) * 2.0f * PI;
                float lat = (1.0f - v) * PI;
                float3 dir(std::sin(lat) * std::cos(lon), std::cos(lat), std::sin(lat) * std::sin(lon));
                float4 c = hyperSeaFrag(dir);
                acc[0] += h_saturate(c.x); acc[1] += h_saturate(c.y); acc[2] += h_saturate(c.z);
            }
            for (int ch = 0; ch < 3; ch++)
            {
                float s = linear_to_srgb(acc[ch] / (SS * SS)) * 255.0f + 0.5f * dither(i, r, ch);
                int q = (int)std::floor(s + 0.5f);
                rows[((size_t)r * W + i) * 3 + ch] = (uint8_t)(q < 0 ? 0 : (q > 255 ? 255 : q));
            }
        }
    };
    unsigned n = std::thread::hardware_concurrency(); if (n == 0) n = 4;
    std::vector<std::thread> pool;
    for (unsigned t = 0; t < n; t++) pool.emplace_back(work, (int)t, (int)n);
    for (auto& t : pool) t.join();
    fwrite(rows.data(), 1, rows.size(), stdout);
    return 0;
}
"""


def render(width: int, height: int, ss: int) -> bytes:
    shader_text, mat_text = read(SHADER), read(MATERIAL)
    cpp = (f'#include "{SHIM}"\nfloat4 _Time;\n' + transpile(shader_text)
           + "\nvoid init_material()\n{\n" + material_inits(shader_text, mat_text) + "\n}\n"
           + MAIN)
    with tempfile.TemporaryDirectory() as tmp:
        src, exe = Path(tmp) / "sky.cpp", Path(tmp) / "sky"
        src.write_text(cpp, encoding="utf-8")
        cc = os.environ.get("CXX", "clang++")
        r = subprocess.run([cc, "-std=c++17", "-O2", "-pthread", "-w", str(src), "-o", str(exe)],
                           capture_output=True, text=True)
        if r.returncode != 0:
            sys.exit("the transpiled shader did not compile:\n" + r.stderr[-4000:])
        r = subprocess.run([str(exe), str(width), str(height), str(ss)], capture_output=True)
        if r.returncode != 0 or len(r.stdout) != width * height * 3:
            sys.exit(f"render failed (exit {r.returncode}, {len(r.stdout)} bytes)")
        return r.stdout


# ------------------------------------------------------------------------- PNG

def png_bytes(width: int, height: int, rgb: bytes) -> bytes:
    stride = width * 3
    raw = bytearray()
    prev = bytes(stride)
    for y in range(height):
        row = rgb[y * stride:(y + 1) * stride]
        # Filter 2 (Up): sky rows are close to the row above, and it costs one subtraction.
        raw.append(2)
        raw += bytes((row[i] - prev[i]) & 0xFF for i in range(stride))
        prev = row

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


# ------------------------------------------------------------------------- authored assets

PANO_SHADER_SRC = r'''// The baked HyperSea sky: ONE texture sample per pixel, where HyperSeaSkybox.shader runs two
// 3x3x3 Voronoi searches and ~20 octaves of noise. The panorama is written OFFLINE by
// Tools/Build/bake_static_skybox.py, which inverts exactly the mapping below - change one and the
// other must change with it. tex2Dlod at level 0: the equirect seam is a derivative discontinuity,
// and the texture is imported without mips, so there is nothing to select between.
Shader "CosmicShore/StaticSkyPanorama"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (equirectangular, baked)", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.viewDir);
                float2 uv = float2(0.5 - atan2(d.z, d.x) * (0.5 / UNITY_PI),
                                   1.0 - acos(clamp(d.y, -1.0, 1.0)) / UNITY_PI);
                return tex2Dlod(_MainTex, float4(uv, 0, 0));
            }
            ENDCG
        }
    }

    Fallback Off
}
'''

SHADER_META = f"""fileFormatVersion: 2
guid: {SHADER_GUID}
ShaderImporter:
  externalObjects: {{}}
  defaultTextures: []
  nonModifiableTextures: []
  preprocessorOverride: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def platform(target: str) -> str:
    return f"""  - serializedVersion: 3
    buildTarget: {target}
    maxTextureSize: {WIDTH}
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
"""


PNG_META = f"""fileFormatVersion: 2
guid: {PNG_GUID}
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
  maxTextureSize: {WIDTH}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 1
    wrapW: 1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 0
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
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
{platform("DefaultTexturePlatform")}{platform("Standalone")}{platform("Android")}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

MAT = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: StaticHyperSeaSkybox
  m_Shader: {{fileID: 4800000, guid: {SHADER_GUID}, type: 3}}
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
    - _MainTex:
        m_Texture: {{fileID: 2800000, guid: {PNG_GUID}, type: 3}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats: []
    m_Colors: []
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""

MAT_META = f"""fileFormatVersion: 2
guid: {MAT_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

AUTHORED = {
    PANO_SHADER: PANO_SHADER_SRC,
    Path(str(PANO_SHADER) + ".meta"): SHADER_META,
    Path(str(PANO_PNG) + ".meta"): PNG_META,
    PANO_MAT: MAT,
    Path(str(PANO_MAT) + ".meta"): MAT_META,
}


def inputs() -> dict:
    """Everything that can move a PIXEL - the files, plus the code that transpiles, renders and
    encodes - and nothing that cannot. Hashing this whole file made a docstring edit read as a
    stale bake, which is how a comment-only change staled the Garland measurement."""
    out = {str(p.relative_to(ROOT)): sha(p) for p in (SHADER, MATERIAL, SHIM)}
    code = "".join(inspect.getsource(f) for f in (transpile, srgb_to_linear, material_inits, render, png_bytes))
    code += MAIN + repr((WIDTH, HEIGHT, SUPERSAMPLE, INTRINSICS))
    out["bake code"] = hashlib.sha256(code.encode("utf-8")).hexdigest()
    return out


def check() -> int:
    fails = []
    for path, text in AUTHORED.items():
        if not path.exists() or read(path) != text:
            fails.append(f"{path.relative_to(ROOT)} differs from what this tool authors")
    if not STAMP.exists():
        fails.append(f"{STAMP.relative_to(ROOT)} missing - run the bake")
    else:
        stamp = json.loads(read(STAMP))
        if stamp.get("inputs") != inputs():
            fails.append("the sky shader, its material, the shim or this tool changed since the bake - re-run it")
        if not PANO_PNG.exists() or sha(PANO_PNG) != stamp.get("png"):
            fails.append(f"{PANO_PNG.relative_to(ROOT)} is not the baked panorama")
    for f in fails:
        print("FAIL:", f)
    print("OK" if not fails else "")
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--preview", type=Path, help="render a small PNG here instead of baking")
    ap.add_argument("--size", type=int, default=1024, help="preview width")
    args = ap.parse_args()

    if args.check:
        return check()

    if args.preview:
        w = args.size
        args.preview.write_bytes(png_bytes(w, w // 2, render(w, w // 2, 2)))
        print(f"wrote preview {args.preview} ({w}x{w // 2})")
        return 0

    rgb = render(WIDTH, HEIGHT, SUPERSAMPLE)
    PANO_PNG.write_bytes(png_bytes(WIDTH, HEIGHT, rgb))
    for path, text in AUTHORED.items():
        path.write_text(text, encoding="utf-8")
    STAMP.write_text(json.dumps({"inputs": inputs(), "png": sha(PANO_PNG),
                                 "size": [WIDTH, HEIGHT], "supersample": SUPERSAMPLE}, indent=1) + "\n",
                     encoding="utf-8")
    mean = sum(rgb) / len(rgb)
    print(f"baked {PANO_PNG.relative_to(ROOT)} {WIDTH}x{HEIGHT} "
          f"({PANO_PNG.stat().st_size / 1e6:.1f} MB, mean sRGB level {mean:.1f}/255)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
