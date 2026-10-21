#!/usr/bin/env python3
"""
Compile SilencePerimeter.shader's three passes (Docs/ElementalAbilitySystem/SILENCE.md §5) before an editor ever
opens it: each pass's vertex and fragment stages, with the shader's HLSLINCLUDE block, through glslang in HLSL
mode, against a declarations-only mock of the URP calls the shader makes (Core.hlsl's screen UV, full-screen
triangle, space transforms, texture macros). This catches a typo, a missing declaration or a reserved word
(an early port named a variable `line`, which HLSL reserves for geometry shaders).

It does NOT prove the warp and shard pixels match the lab's GLSL (the lab ran that GLSL in WebGL), nor compile
against the real URP library for every platform; the editor does that. Negative control: the shard pass with the
HLSLINCLUDE's SampleScene removed must FAIL.

THE EDGE'S MASK (clang++ + node). The edge pass works SilenceVignetteMask out per pixel in HLSL, a fourth copy of
that math. Its functions are lifted out of the shader, compiled as C++ and run on a grid of points and shapes
against the lab's <mask-twin> (which the parity gate holds to the C#): they must agree to 1e-4. Negative control:
the HLSL with its feather doubled must FAIL.

Needs glslangValidator (apt: glslang-tools). Exit 0 on pass.
Usage: python3 Tools/Shaders/verify_silence_perimeter.py
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SHADER = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "Graphs", "SilencePerimeter.shader")

# Only what the shader calls; a name it uses that is not here fails, as it would without the include.
MOCK = r"""
#define TEXTURE2D(name) Texture2D name
#define SAMPLE_TEXTURE2D_LOD(tex, smp, uv, lod) tex.SampleLevel(smp, uv, lod)
SamplerState sampler_LinearClamp;
#define PI 3.14159265359
float4 _ScreenParams;
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_MatrixV;
float4x4 unity_MatrixVP;
float2 GetNormalizedScreenSpaceUV(float4 positionCS) { return positionCS.xy / _ScreenParams.xy; }
float4 GetFullScreenTriangleVertexPosition(uint vertexID)
{
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    return float4(uv * 2.0 - 1.0, 0.0, 1.0);
}
float3 TransformObjectToWorld(float3 p) { return mul(unity_ObjectToWorld, float4(p, 1.0)).xyz; }
float3 TransformWorldToView(float3 p) { return mul(unity_MatrixV, float4(p, 1.0)).xyz; }
float4 TransformWorldToHClip(float3 p) { return mul(unity_MatrixVP, float4(p, 1.0)); }
float3 TransformWorldToViewDir(float3 d, bool doNormalize) { float3 r = mul((float3x3)unity_MatrixV, d); return doNormalize ? normalize(r) : r; }
float3 TransformObjectToWorldNormal(float3 n) { return normalize(mul(n, (float3x3)unity_WorldToObject)); }
"""

CORE = '#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"'


def blocks(text):
    include = re.search(r"HLSLINCLUDE(.*?)ENDHLSL", text, re.S).group(1)
    passes = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", text, re.S)
    return include, passes


def compile_pass(include, program, workdir, tag):
    vert = re.search(r"#pragma vertex (\w+)", program).group(1)
    frag = re.search(r"#pragma fragment (\w+)", program).group(1)
    body = re.sub(r"#pragma .*", "", program)
    src = MOCK + include.replace(CORE, "") + body
    path = os.path.join(workdir, tag + ".hlsl")
    with open(path, "w") as f:
        f.write(src)
    errors = []
    for stage, entry in (("vert", vert), ("frag", frag)):
        r = subprocess.run(["glslangValidator", "-D", "-S", stage, "-e", entry, "-V", "-o", os.devnull, path],
                           capture_output=True, text=True)
        if r.returncode != 0:
            errors.append(f"{tag} {stage} ({entry}):\n" + (r.stdout + r.stderr).strip())
    return errors


def main():
    if not shutil.which("glslangValidator"):
        print("SKIPPED: glslangValidator not found (apt install glslang-tools)")
        return 2
    text = open(SHADER).read()
    include, passes = blocks(text)
    if len(passes) != 3:
        print(f"FAIL: expected 3 passes, found {len(passes)}")
        return 1
    failed = False
    with tempfile.TemporaryDirectory() as tmp:
        for tag, program in zip(("warp", "shard", "edge"), passes):
            errors = compile_pass(include, program, tmp, tag)
            if errors:
                failed = True
                print("FAIL  " + "\n".join(errors))
            else:
                print(f"ok    {tag} pass compiles (vertex + fragment)")
        broken = re.sub(r"float3 SampleScene\(float2 uv\)\s*\{.*?\}", "", include, flags=re.S)
        if broken == include:
            failed = True
            print("FAIL  control: SampleScene is gone from the HLSLINCLUDE; update the control")
        elif compile_pass(broken, passes[1], tmp, "control"):
            print("ok    control: the shard pass without SampleScene fails to compile")
        else:
            failed = True
            print("FAIL  control: the shard pass compiled without SampleScene (the check is not checking)")
    failed = check_mask(text) or failed
    print("FAILED" if failed else "PASS")
    return 1 if failed else 0


CPP_PRELUDE = r'''
#include <cmath>
#include <cstdio>
struct float4 { float x, y, z, w; };
static const float PI = 3.14159265359f;
static float saturate(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
static float lerp(float a, float b, float t) { return a + (b - a) * t; }
using std::sqrt; using std::floor; using std::cos; using std::atan2; using std::round; using std::abs; using std::fmax;
static float max(float a, float b) { return a > b ? a : b; }
float4 _SPEdgeShape, _SPEdgeShape2;
'''

# style facets inner feather roundness fill rimStrength rimWidth aspect
MASK_CASES = [
    (1, 4, 0.93, 0.07, 0.0, 1.0, 0.3, 0.015, 16 / 9), (1, 4, 0.6, 0.07, 0.0, 1.0, 0.3, 0.015, 16 / 9),
    (0, 10, 0.55, 0.5, 0.25, 1.0, 0.35, 0.04, 16 / 9), (1, 10, 0.62, 0.25, 0.25, 0.85, 0.6, 0.025, 2.1667),
    (1, 6, 0.4, 0.3, 0.6, 0.7, 0.8, 0.08, 4 / 3), (0, 10, 0.9, 0.0, 0.5, 1.0, 0.5, 0.1, 1.0),
]


def check_mask(text):
    if not shutil.which("clang++") or not shutil.which("node"):
        print("SKIPPED: the edge mask check needs clang++ and node")
        return False
    # The mask's functions: one block in the HLSLINCLUDE, from SilenceRepeat to the end of SilenceMaskAlpha.
    m = re.search(r"(float SilenceRepeat\(.*?float SilenceMaskAlpha\(.*?\n        \}\n)", text, re.S)
    if not m:
        print("FAIL  edge mask: the mask functions are gone from the shader")
        return True
    funcs = [m.group(1)]
    hlsl = "\n".join(funcs)
    pts = [(x / 24, y / 24) for y in range(25) for x in range(25)]
    lab = open(os.path.join(ROOT, "Docs", "Studios", "SilenceVignetteLab.html")).read()
    twin = re.search(r"// <mask-twin>[^\n]*\n(.*?)// </mask-twin>", lab, re.S).group(1)
    js = twin + "\nconst cases = " + repr([list(c) for c in MASK_CASES]) + ", pts = " + repr([list(p) for p in pts]) + ";\n" + \
        "const out = [];\nfor (const c of cases) { const s = { style: c[0], facets: c[1], inner: c[2], feather: c[3], roundness: c[4], fill: c[5], rimStrength: c[6], rimWidth: c[7] };\n" + \
        "  for (const p of pts) out.push(MASK.alpha(p[0], p[1], c[8], s)); }\nconsole.log(out.join(' '));\n"
    ref = [float(v) for v in subprocess.run(["node", "-e", js], capture_output=True, text=True, check=True).stdout.split()]

    def run(source, tag):
        main = "int main() {\n"
        for c in MASK_CASES:
            main += "  _SPEdgeShape = float4{%rf, %rf, %rf, %rf}; _SPEdgeShape2 = float4{%rf, %rf, %rf, %rf};\n" % tuple(float(x) for x in c[:8])
            for (u, v) in pts:
                main += "  printf(\"%%.7f \", SilenceMaskAlpha(%rf, %rf, %rf));\n" % (float(u), float(v), float(c[8]))
        main += "  return 0;\n}\n"
        with tempfile.TemporaryDirectory() as tmp:
            src = os.path.join(tmp, tag + ".cpp")
            exe = os.path.join(tmp, tag)
            with open(src, "w") as f:
                f.write(CPP_PRELUDE + source + "\n" + main)
            b = subprocess.run(["clang++", "-std=c++17", "-O1", "-o", exe, src], capture_output=True, text=True)
            if b.returncode != 0:
                return None, b.stderr.strip()
            return [float(v) for v in subprocess.run([exe], capture_output=True, text=True).stdout.split()], ""

    got, err = run(hlsl, "edge")
    if got is None:
        print("FAIL  edge mask: the HLSL does not compile as C++:\n" + err)
        return True
    worst = max(abs(a - b) for a, b in zip(got, ref))
    if len(got) != len(ref) or worst > 1e-4:
        print(f"FAIL  edge mask: HLSL vs the lab's twin differ (worst {worst:.2e})")
        return True
    print(f"ok    edge mask: HLSL matches the lab's twin on {len(ref)} points (worst {worst:.1e})")
    broken = hlsl.replace("inner + max(0.0, feather)", "inner + 2.0 * max(0.0, feather)")
    if broken == hlsl:
        print("FAIL  control: the edge's feather term is gone; update the control")
        return True
    bad, _ = run(broken, "control")
    if bad is not None and max(abs(a - b) for a, b in zip(bad, ref)) > 1e-4:
        print("ok    control: the HLSL with its feather doubled disagrees")
        return False
    print("FAIL  control: the doubled feather was not caught")
    return True


if __name__ == "__main__":
    sys.exit(main())
