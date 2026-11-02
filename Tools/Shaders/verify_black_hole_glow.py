#!/usr/bin/env python3
"""
Prove the MOBILE look of holes and waves (BlackHoleGlow.shader, Docs/BLACK_HOLE.md §5.4) is cheap, warps nothing,
draws what it says, and compiles for every API a phone uses. The Meerkat water tube's recipe (MeerkatWaterTube.shader,
Docs/Shaders/WATERY_GLASS.md): one forward pass, one small mesh per hole / wave / ripple, no screen copy, no depth
texture, no texture sample at all.

S. STATIC (the shader and its wiring). One pass, LightMode UniversalForward, ZWrite Off, ZTest LEqual, premultiplied
   blending, #pragma target 3.0, no GrabPass / _CameraOpaqueTexture / depth / texture sample, URP Core.hlsl only.
   The C# side: the Mobile look is the default; no lens is built, no lens pass draws, no prism or hull is stretched
   (BlackHoleWarp gated on SpaceWarp), and the registry's driver and the Stoat's ripple draw through BlackHoleGlow.

A. THE PIXEL (clang++). The shipped fragment, translated mechanically to C++:
     1. EDGE: in every mode the colour eases to exactly 0 at the mesh's outer edge (no disc rim ever shows) and has
        no step anywhere across the band.
     2. BLACK HOLE: a fully dark core (alpha 1, no light) with the photon ring at 1.04 shadows glowing at its glow.
     3. WHITE HOLE: a bright soft core, never brighter than the display's white, half opaque at its centre.
     4. RIPPLE PARITY: the gravitational ripple's waveform is BlackHoleWaves.Strain (the C# source, extracted and
        translated) at every radius, age and angle — the wiggle, drawn, without moving anything.
     5. NO BLOOM: with the shipped config, a wave's brightest crest stays under the bloom threshold (0.2).
     6. NEGATIVE CONTROLS: the edge fade dropped must FAIL 1; the ripple's flat core moved (60 -> 50) must FAIL 4.

B. COMPILE. B1 glslang (HLSL) against a declarations-only mock of URP Core.hlsl; B2 DXC against the REAL URP
   library for D3D11, Vulkan, Metal and GLES 3.0 (GLES: DXC -> SPIR-V -> spirv-cross --es 300 -> glslangValidator
   validates the ES 3.00 shader a phone would get; the one macro in API/GLES3.hlsl DXC's preprocessor rejects,
   ERROR_ON_UNSUPPORTED_FUNCTION's `#error`, is overlaid with an undefined symbol, so a use still fails). SKIPPED
   loudly without dxc / the URP checkout / spirv-cross (--require-real fails it). Negative control: without the
   Core.hlsl include it must FAIL.

C. COST (DXC, D3D11 DXIL). The fragment's instruction count against the Studio lens's two full-screen fragments
   (printed for the report; the glow must be cheaper per pixel, and it covers a small disc, not the screen).

Exit 0 on pass. Usage:  python3 Tools/Shaders/verify_black_hole_glow.py [--keep] [--require-real]
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import verify_black_hole_lens as lens  # noqa: E402  (shared helpers: the C++ shim, build_and_run, the real toolchain)

ROOT = lens.ROOT
SHADER = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/BlackHoleGlow.shader")
GLOW_CS = os.path.join(ROOT, "Assets/_Scripts/Controller/Environment/BlackHole/BlackHoleGlow.cs")
WAVES_CS = os.path.join(ROOT, "Assets/_Scripts/Controller/Environment/BlackHole/BlackHoleWaves.cs")
CONFIG_CS = os.path.join(ROOT, "Assets/_Scripts/ScriptableObjects/BlackHoleConfigSO.cs")
CONFIG_ASSET = os.path.join(ROOT, "Assets/Resources/BlackHoleConfig.asset")
HOLE_DIR = os.path.join(ROOT, "Assets/_Scripts/Controller/Environment/BlackHole")
RIPPLE_CS = os.path.join(ROOT, "Assets/_Scripts/Controller/Vessel/R_VesselActions/StoatRipple.cs")
BLOOM_THRESHOLD = 0.2
GLES3_API = "Packages/com.unity.render-pipelines.core/ShaderLibrary/API/GLES3.hlsl"


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def program(shader):
    programs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", shader, re.S)
    assert len(programs) == 1, f"expected ONE pass, found {len(programs)}"
    return programs[0]


# ---------------------------------------------------------------------------------------------------------
# S. static

def static_checks(shader):
    fails = []

    def need(cond, what):
        print(f"   {'ok  ' if cond else 'FAIL'} {what}")
        if not cond:
            fails.append(what)

    code = re.sub(r"//[^\n]*", "", shader)
    need(code.count("Pass\n") + code.count("Pass\r\n") == 1 and len(re.findall(r"\bPass\s*\{", code)) == 1, "one pass")
    need('"LightMode" = "UniversalForward"' in code, "LightMode UniversalForward (drawn by the forward renderer, no custom pass)")
    need(re.search(r"\bZWrite Off\b", code) is not None, "ZWrite Off")
    need(re.search(r"\bZTest LEqual\b", code) is not None, "ZTest LEqual (the hardware depth test, no depth texture)")
    need(re.search(r"\bBlend One OneMinusSrcAlpha\b", code) is not None, "premultiplied blending")
    need("#pragma target 3.0" in code, "#pragma target 3.0 (GLES 3.0)")
    for banned in ("GrabPass", "_CameraOpaqueTexture", "_CameraDepthTexture", "SampleSceneDepth", "DeclareDepthTexture",
                   "DeclareOpaqueTexture", "SAMPLE_TEXTURE", "TEXTURE2D", "tex2D", "sampler", ".Sample("):
        need(banned not in code, f"no {banned}")
    includes = re.findall(r'#include "([^"]+)"', code)
    need(includes == ["Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"], f"URP Core.hlsl only {includes}")
    need(re.search(r"\bhalf4 BlackHoleGlowFrag\(", code) is not None, "half precision colour out")

    config = read(CONFIG_CS)
    asset = read(CONFIG_ASSET)
    need(re.search(r"HoleLook look = HoleLook\.Mobile;", config) is not None, "the Mobile look is the code default")
    need(re.search(r"^  look: 0$", asset, re.M) is not None, "the shipped config is the Mobile look")
    need(re.search(r"public bool SpaceWarp => look == HoleLook\.StudioLens;", config) is not None, "SpaceWarp only in the Studio lens look")
    hole = read(os.path.join(HOLE_DIR, "BlackHole.cs"))
    need("if (BlackHoleGlow.Active(config)) return;" in hole and "config.LensEnabled && config.SpaceWarp" in hole,
         "BlackHole builds no lens in the Mobile look")
    pass_cs = read(os.path.join(HOLE_DIR, "BlackHoleLensPass.cs"))
    need("if (!BlackHoleRegistry.Config.SpaceWarp) return;" in pass_cs, "no full-screen lens / wave pass in the Mobile look")
    warp = read(os.path.join(HOLE_DIR, "BlackHoleWarp.cs"))
    need("bool enabled = config.SpaceWarp && config.WarpEnabled" in warp, "no prism stretched by a hole in the Mobile look")
    need("config.SpaceWarp && config.WarpEnabled && config.GwPrism" in warp, "no prism stretched by a wave in the Mobile look")
    need("!config.SpaceWarp || !config.WarpEnabled" in warp, "no hull stretched (VesselLogStretch) in the Mobile look")
    registry = read(os.path.join(HOLE_DIR, "BlackHoleRegistry.cs"))
    need("BlackHoleGlow.DrawHoles(_warpHoles, config);" in registry and "BlackHoleGlow.DrawWaves(config);" in registry,
         "the registry's driver draws the holes and waves")
    ripple = read(RIPPLE_CS)
    need("BlackHoleGlow.DrawRing(" in ripple and "if (_acquired || _mobile) return;" in ripple,
         "the Stoat's ripple draws through BlackHoleGlow and takes no lens pass")
    glow = read(GLOW_CS)
    need("Graphics.RenderMesh(" in glow and "ShadowCastingMode.Off" in glow, "RenderMesh, no shadows (the Meerkat tube's draw)")
    return not fails


# ---------------------------------------------------------------------------------------------------------
# A. the pixel

def frag_to_cpp(shader):
    prog = program(shader)
    m = re.search(r"half4 BlackHoleGlowFrag\(Varyings i\) : SV_Target\s*\{.*?\n            \}", prog, re.S)
    assert m, "BlackHoleGlowFrag not found"
    body = m.group(0)
    body = body.replace("half4 BlackHoleGlowFrag(Varyings i) : SV_Target", "static float4 glow_frag()")
    body = body.replace("half3 light = 0;", "float3 light(0.0f);")
    body = body.replace("half shade = 0;", "float shade = 0.0f;")
    body = body.replace("(half)", "(float)").replace("1.0h", "1.0f")
    body = re.sub(r"\bhalf4\(", "float4(", body)
    body = re.sub(r"\bhalf\b", "float", body)
    body = body.replace("_Colour.rgb", "_Colour.xyz()").replace("_Axis.xyz", "_Axis.xyz()")
    body = body.replace("i.offsetWS", "g_offset").replace("i.fade", "g_fade")
    assert "g_offset" in body and "g_fade" in body and "half" not in body, "the shipped fragment changed shape"
    return body


def strain_to_cpp(waves_cs):
    m = re.search(r"public static float Strain\(Vector3 at, in Wave w, float now, BlackHoleConfigSO config\)\s*\{(.*?)\n        \}",
                  waves_cs, re.S)
    assert m, "BlackHoleWaves.Strain not found"
    body = m.group(1)
    body = body.replace("Vector3 d", "float3 d").replace("d.magnitude", "length(d)")
    body = body.replace("Vector3.Dot(", "dot(").replace("Mathf.PI", "3.14159265f")
    body = re.sub(r"Mathf\.(Max|Min|Exp|Sin)\(", lambda k: k.group(1).lower() + "(", body)
    body = body.replace("config.GwSpeed", "cfg_speed").replace("config.GwRingdown", "cfg_tau").replace("config.GwPeriod", "cfg_period")
    body = body.replace("w.Centre", "w_centre").replace("w.T0", "w_t0").replace("w.Axis", "w_axis").replace("w.Mass", "w_mass")
    body = re.sub(r"(?<![\d.eE+-])(\d+)f\b", r"\1.0f", body)   # C# 2f -> C++ 2.0f (0.01f, 1e-6f are already C++)
    body = body.replace("FlatRadius", "60.0f")
    assert "Mathf" not in body and "config." not in body and "w." not in body, "BlackHoleWaves.Strain changed shape:\n" + body
    return ("static float cfg_speed, cfg_tau, cfg_period; static float3 w_centre, w_axis; static float w_t0, w_mass;\n"
            "static float csharp_strain(float3 at, float now)\n{" + body + "\n}\n")


HARNESS = r"""
#include "shim.h"
#include <cstdio>
#include <random>
static inline float4 operator*(float4 a, float b){ return float4(a.x*b,a.y*b,a.z*b,a.w*b); }
static inline float3 operator*(float3 a, float3 b);
static float4 _Centre, _Radii, _Shape, _Wave, _Axis, _Colour; static float _Fade, _Mode;
static float3 g_offset; static float g_fade = 1.0f;
#include "glow_frag.h"
#include "strain.h"

static float4 at_r(float r){ g_offset = float3(r, 0.0f, 0.0f); return glow_frag(); }
static int fails = 0;
#define CHECK(cond, ...) do { if (!(cond)) { if (fails < 6) { printf("   FAIL: "); printf(__VA_ARGS__); printf("\n"); } fails++; } } while (0)

// sweep the band: the value at the outer edge is 0 and nothing steps
static void edge(const char *what){
    const int N = 4000; float inner = _Radii.x, outer = _Radii.y; float4 prev = at_r(inner); float worst = 0.0f;
    for (int k = 1; k <= N; k++){
        float4 c = at_r(inner + (outer - inner) * k / N);
        float d = std::max(std::max(std::fabs(c.x-prev.x), std::fabs(c.y-prev.y)), std::max(std::fabs(c.z-prev.z), std::fabs(c.w-prev.w)));
        worst = std::max(worst, d); prev = c;
    }
    float4 e = at_r(outer);
    CHECK(std::fabs(e.x) + std::fabs(e.y) + std::fabs(e.z) + std::fabs(e.w) < 1e-6f, "%s: not 0 at the outer edge (%g %g %g a %g)", what, e.x, e.y, e.z, e.w);
    CHECK(worst < 0.02f, "%s: steps %g between neighbouring pixels", what, worst);
    printf("   %-34s edge %.1e  largest step %.4f\n", what, std::fabs(e.x) + std::fabs(e.w), worst);
}

int main(){
    _Colour = float4(1.0f, 0.8f, 0.55f, 1.0f); _Fade = 1.0f;
    // the shipped config's numbers (the harness defines them from the asset)
    const float shadow = SHADOW_SIZE * 10.0f, w = RING_WIDTH;
    printf("1. edge, 2-3. cores\n");
    _Mode = 0; _Shape = float4(shadow, w, RING_GLOW, HALO_GLOW); _Radii = float4(0, shadow * (1.04f + 3.0f * w + 0.6f), 0, 0);
    edge("black hole");
    float4 c = at_r(0.0f);
    CHECK(c.w > 0.999f && c.x + c.y + c.z < 1e-6f, "black hole: the core is not fully dark (%g %g %g a %g)", c.x, c.y, c.z, c.w);
    float4 ring = at_r(1.04f * shadow);
    CHECK(std::fabs(ring.x - _Colour.x * (RING_GLOW + HALO_GLOW * std::exp(-3.0f * 0.04f))) < 0.02f && ring.w < 0.01f,
          "black hole: the photon ring at 1.04 shadows glows %g (a %g)", ring.x, ring.w);
    printf("   black hole core a %.3f, ring light %.3f\n", c.w, ring.x);

    const float core = WHITE_CORE_SIZE * 10.0f;
    _Mode = 1; _Shape = float4(core, 0, WHITE_BRIGHTNESS, 0.5f); _Radii = float4(0, core * 1.6f, 0, 0); _Colour = float4(1, 0.98f, 0.94f, 1);
    edge("white hole");
    c = at_r(0.0f);
    CHECK(c.x <= 1.0f && c.x > 0.95f && std::fabs(c.w - 0.5f) < 1e-4f, "white hole: centre %g a %g", c.x, c.w);
    printf("   white hole centre light %.3f a %.3f\n", c.x, c.w);

    _Mode = 2; _Shape = float4(80.0f, 3.2f, 0.15f, 0.0f); _Radii = float4(80.0f - 8.0f, 80.0f + 8.0f, 0, 0);
    edge("light ring (the Stoat's ripple)");
    c = at_r(80.0f);
    CHECK(std::fabs(c.x - 0.15f) < 1e-4f, "ring: the front glows %g, not its brightness", c.x);

    printf("4. ripple parity with BlackHoleWaves.Strain, 5. bloom\n");
    cfg_speed = GW_SPEED; cfg_tau = GW_RINGDOWN; cfg_period = GW_PERIOD;
    std::mt19937 rng(7); std::uniform_real_distribution<float> U(0.0f, 1.0f);
    float worst = 0.0f; int lit = 0;
    for (int n = 0; n < 200000; n++){
        float3 axis(U(rng) - 0.5f, U(rng) - 0.5f, U(rng) - 0.5f); float al = length(axis); axis = axis * (1.0f / al);
        float3 dir(U(rng) - 0.5f, U(rng) - 0.5f, 0.0f); float dl = length(dir); if (dl < 1e-3f) continue; dir = dir * (1.0f / dl);
        float r = 0.5f + 600.0f * U(rng), age = 0.01f + 9.0f * U(rng), mass = 0.1f + 1.4f * U(rng);
        w_centre = float3(0.0f); w_axis = axis; w_t0 = 0.0f; w_mass = mass;
        float strain = csharp_strain(dir * r, age);
        _Mode = 3; _Colour = float4(1, 1, 1, 1); _Shape = float4(0, 0, 1, 1); _Radii = float4(0, 1e6f, 0, 0);
        _Wave = float4(age, cfg_speed, cfg_period, cfg_tau); _Axis = float4(axis, 1.0f);
        g_offset = dir * r; float4 px = glow_frag();
        float s = px.x - px.w;   // crests light, troughs shade
        worst = std::max(worst, std::fabs(s * mass - strain));
        if (std::fabs(strain) > 1e-3f) lit++;
    }
    CHECK(worst < 2e-4f && lit > 1000, "ripple: off BlackHoleWaves.Strain by %g (%d lit samples)", worst, lit);
    printf("   ripple vs Strain: worst %.2e over 200000 samples (%d lit)\n", worst, lit);

    // 5. the brightest crest of the heaviest wave the glow draws (mass clamped to 1.5), the shipped glow
    float glow = WAVE_GLOW * GW_GLOW * 1.5f, peak = 0.0f;
    _Mode = 3; _Shape = float4(0, 0, glow, 0); _Colour = float4(0.78f, 0.88f, 1, 1); _Radii = float4(0, 1e6f, 0, 0); _Axis = float4(0, 0, 0, 0);
    for (int k = 0; k < 4000; k++){ _Wave = float4(0.01f + k * 0.002f, cfg_speed, cfg_period, cfg_tau); for (int j = 1; j < 120; j++) peak = std::max(peak, at_r(j * 0.5f).z); }
    CHECK(peak < BLOOM, "a wave's crest reaches %g: it would bloom (threshold %g)", peak, BLOOM);
    printf("   brightest wave crest %.3f (bloom threshold %.2f)\n", peak, BLOOM);

    // the shell's light ring (BlackHoleGlow.DrawWaves)
    CHECK(WAVE_GLOW < BLOOM, "a shell's ring glows %g: it would bloom", WAVE_GLOW);
    printf(fails ? "PIXEL: %d FAILED\n" : "PIXEL: all passed\n", fails);
    return fails ? 1 : 0;
}
"""


def asset_value(asset, name):
    m = re.search(rf"^  {name}: ([-0-9.eE]+)$", asset, re.M)
    assert m, f"{name} not in BlackHoleConfig.asset"
    return float(m.group(1))


def harness_flags():
    asset = read(CONFIG_ASSET)
    values = {"SHADOW_SIZE": "shadowSize", "RING_WIDTH": "photonRingWidth", "RING_GLOW": "photonRingGlow",
              "HALO_GLOW": "mobileHaloGlow", "WHITE_CORE_SIZE": "whiteCoreSize", "WHITE_BRIGHTNESS": "whiteCoreBrightness",
              "GW_SPEED": "gwSpeed", "GW_RINGDOWN": "gwRingdown", "GW_PERIOD": "gwPeriod", "GW_GLOW": "gwGlow",
              "WAVE_GLOW": "mobileWaveGlow"}
    flags = [f"-D{k}={asset_value(asset, v)}f" for k, v in values.items()]
    return flags + [f"-DBLOOM={BLOOM_THRESHOLD}f"]


def run_pixel(work, shader, waves_cs, label):
    with open(os.path.join(work, "glow_frag.h"), "w") as f:
        f.write(frag_to_cpp(shader))
    with open(os.path.join(work, "strain.h"), "w") as f:
        f.write(strain_to_cpp(waves_cs))
    harness = HARNESS.replace("static inline float3 operator*(float3 a, float3 b);\n", "")
    return lens.build_and_run(work, harness, harness_flags(), label)


# ---------------------------------------------------------------------------------------------------------
# B. compile

GLOW_MOCK = r"""// mock: URP Core.hlsl, declarations only (what BlackHoleGlow.shader may use from it)
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
float4x4 unity_MatrixV;
#define UNITY_MATRIX_V unity_MatrixV
float4x4 unity_MatrixVP;
float3 _WorldSpaceCameraPos;
float4 TransformWorldToHClip(float3 positionWS) { return mul(unity_MatrixVP, float4(positionWS, 1.0)); }
"""


def glslang_glow(work, prog, stage, entry):
    mock = os.path.join(work, "mock", lens.URP + "Core.hlsl")
    os.makedirs(os.path.dirname(mock), exist_ok=True)
    with open(mock, "w") as f:
        f.write(GLOW_MOCK)
    return lens.glslang_compile(work, prog, stage, entry)


def find_spirv_cross():
    return os.environ.get("SPIRV_CROSS") or shutil.which("spirv-cross")


def gles3_overlay(work, root):
    src = os.path.join(root, GLES3_API)
    dst = os.path.join(work, "gles3_overlay", GLES3_API)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    text = read(src)
    old = "#define ERROR_ON_UNSUPPORTED_FUNCTION(funcName) #error #funcName is not supported on GLES 3.0"
    assert old in text, "API/GLES3.hlsl changed: update the overlay"
    with open(dst, "w") as f:
        f.write(text.replace(old, "#define ERROR_ON_UNSUPPORTED_FUNCTION(funcName) funcName##_is_not_supported_on_GLES3"))
    return os.path.join(work, "gles3_overlay")


def dxc_run(work, toolchain, prog, profile, entry, stage_define, api, extra=(), out=None):
    dxc, root = toolchain
    path = os.path.join(work, f"glow_{api}_{profile}.hlsl")
    with open(path, "w") as f:
        f.write(re.sub(r"#pragma[^\n]*\n", "\n", prog))
    cmd = [dxc, "-T", profile, "-E", entry, "-HV", "2018", *extra, "-I", root, "-D", api, "-D", stage_define]
    for d in lens.REAL_DEFINES:
        cmd += ["-D", d]
    cmd += ["-Fo", out or os.devnull, path]
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.returncode, [line for line in (r.stdout + r.stderr).splitlines() if "error" in line]


def gles3_compile(work, toolchain, prog, profile, entry, stage_define, stage):
    overlay = gles3_overlay(work, toolchain[1])
    spv = os.path.join(work, f"glow_gles3_{stage}.spv")
    rc, errors = dxc_run(work, toolchain, prog, profile, entry, stage_define, "SHADER_API_GLES3",
                         extra=("-spirv", "-I", overlay), out=spv)
    if rc != 0:
        return rc, errors
    essl = os.path.join(work, f"glow_gles3.{stage}")
    r = subprocess.run([find_spirv_cross(), "--es", "--version", "300", spv, "--output", essl], capture_output=True, text=True)
    if r.returncode != 0:
        return r.returncode, [r.stderr.strip()]
    r = subprocess.run(["glslangValidator", essl], capture_output=True, text=True)
    return r.returncode, [line for line in (r.stdout + r.stderr).splitlines() if "ERROR" in line]


def dxil_count(work, toolchain, prog, entry):
    """Instructions in the fragment's DXIL (D3D11), all branches: a size, not a timing."""
    dxc, root = toolchain
    path = os.path.join(work, f"cost_{entry}.hlsl")
    with open(path, "w") as f:
        f.write(re.sub(r"#pragma[^\n]*\n", "\n", prog))
    cmd = [dxc, "-T", "ps_6_0", "-E", entry, "-HV", "2018", "-O3", "-I", root, "-D", "SHADER_API_D3D11",
           "-D", "SHADER_STAGE_FRAGMENT"] + sum((["-D", d] for d in lens.REAL_DEFINES), []) + ["-Fc", os.path.join(work, entry + ".ll"), path]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        return None, None
    ll = read(os.path.join(work, entry + ".ll"))
    fn = ll[ll.index("define void @" + entry):]
    fn = fn[:fn.index("\n}")]
    insts = [l for l in fn.splitlines() if l.startswith("  ") and not l.strip().startswith(";")]
    samples = len([l for l in insts if "dx.op.sample" in l or "dx.op.textureLoad" in l])
    return len(insts), samples


# ---------------------------------------------------------------------------------------------------------

def main():
    keep = "--keep" in sys.argv
    require_real = "--require-real" in sys.argv
    for tool in ("clang++", "glslangValidator"):
        if shutil.which(tool) is None:
            print(f"{tool} not found", file=sys.stderr)
            return 2
    work = tempfile.mkdtemp(prefix="verify_black_hole_glow_")
    ok = True
    try:
        shader = read(SHADER)
        waves_cs = read(WAVES_CS)
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(lens.SHIM)

        print("S. static: the shader and its wiring")
        ok &= static_checks(shader)

        print("\nA. the pixel (the shipped fragment, translated to C++)")
        rc, out = run_pixel(work, shader, waves_cs, "pixel")
        sys.stdout.write(out)
        ok &= rc == 0

        mutated = shader.replace("return half4(light, saturate(shade)) * (i.fade * edge);",
                                 "return half4(light, saturate(shade)) * i.fade;")
        assert mutated != shader, "the edge fade moved: update the negative control"
        rc, out = run_pixel(work, mutated, waves_cs, "control_edge")
        fired = rc is not None and rc != 0 and "outer edge" in out
        print(f"6a. negative control [edge fade dropped]: {'FIRED' if fired else 'DID NOT FIRE'}")
        ok &= fired
        mutated = shader.replace("min(1.0, 60.0 / max(r, 1e-3))", "min(1.0, 50.0 / max(r, 1e-3))")
        assert mutated != shader, "the ripple's flat core moved: update the negative control"
        rc, out = run_pixel(work, mutated, waves_cs, "control_ripple")
        fired = rc is not None and rc != 0 and "Strain" in out
        print(f"6b. negative control [ripple flat core 60 -> 50]: {'FIRED' if fired else 'DID NOT FIRE'}")
        ok &= fired

        prog = program(shader)
        print("\nB1. glslang (HLSL) against the URP Core.hlsl mock")
        for stage, entry in (("vert", "BlackHoleGlowVert"), ("frag", "BlackHoleGlowFrag")):
            rc, msg = glslang_glow(work, prog, stage, entry)
            print(f"   {stage}: {'ok' if rc == 0 else 'FAIL ' + msg[:600]}")
            ok &= rc == 0
        broken = prog.replace('#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"', "")
        rc, _ = glslang_glow(work, broken, "vert", "BlackHoleGlowVert")
        print(f"   negative control [no Core.hlsl]: {'FIRED' if rc != 0 else 'DID NOT FIRE'}")
        ok &= rc != 0

        print("\nB2. DXC against the real URP library")
        toolchain, why = lens.find_real_toolchain()
        if toolchain is None:
            print(f"   SKIPPED: {why}")
            ok &= not require_real
        else:
            stages = (("vs_6_0", "BlackHoleGlowVert", "SHADER_STAGE_VERTEX", "vert"),
                      ("ps_6_0", "BlackHoleGlowFrag", "SHADER_STAGE_FRAGMENT", "frag"))
            for api in lens.REAL_APIS:
                for profile, entry, define, _ in stages:
                    extra = ("-spirv",) if api == "SHADER_API_VULKAN" else ()
                    rc, errors = dxc_run(work, toolchain, prog, profile, entry, define, api, extra=extra)
                    print(f"   {api:18s} {profile}: {'ok' if rc == 0 else 'FAIL ' + ' | '.join(errors[:4])}")
                    ok &= rc == 0
            if find_spirv_cross() is None:
                print("   SHADER_API_GLES3: SKIPPED (spirv-cross not found: PATH or $SPIRV_CROSS)")
                ok &= not require_real
            else:
                for profile, entry, define, stage in stages:
                    rc, errors = gles3_compile(work, toolchain, prog, profile, entry, define, stage)
                    print(f"   {'SHADER_API_GLES3':18s} {profile}: {'ok (ESSL 3.00 validated)' if rc == 0 else 'FAIL ' + ' | '.join(errors[:4])}")
                    ok &= rc == 0
            rc, _ = dxc_run(work, toolchain, broken, "vs_6_0", "BlackHoleGlowVert", "SHADER_STAGE_VERTEX", "SHADER_API_VULKAN", extra=("-spirv",))
            print(f"   negative control [no Core.hlsl]: {'FIRED' if rc != 0 else 'DID NOT FIRE'}")
            ok &= rc != 0

            shutil.copy(lens.HLSL, os.path.join(work, "BlackHoleLens.hlsl"))
            print("\nC. cost per pixel (DXIL instructions, all branches; D3D11)")
            glow_n, glow_s = dxil_count(work, toolchain, prog, "BlackHoleGlowFrag")
            lens_progs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", read(lens.SHADER), re.S)
            lens_n, lens_s = dxil_count(work, toolchain, lens_progs[0], "BlackHoleLensFrag")
            light_n, light_s = dxil_count(work, toolchain, lens_progs[1], "BlackHoleWaveLightFrag")
            print(f"   Mobile glow   BlackHoleGlowFrag      {glow_n} instructions, {glow_s} texture reads   (a small disc / band per hole or wave)")
            print(f"   Studio lens   BlackHoleLensFrag      {lens_n} instructions, {lens_s} texture reads   (EVERY pixel, + a full-screen copy)")
            print(f"   Studio lens   BlackHoleWaveLightFrag {light_n} instructions, {light_s} texture reads   (EVERY pixel while a wave lives, + a copy)")
            cheaper = glow_n is not None and lens_n is not None and glow_n < lens_n and glow_s == 0
            print(f"   {'ok  ' if cheaper else 'FAIL'} the glow is cheaper per pixel and reads no texture")
            ok &= cheaper
    finally:
        if keep:
            print(f"\n(work kept in {work})")
        else:
            shutil.rmtree(work, ignore_errors=True)
    print("\nALL PASSED" if ok else "\nFAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
