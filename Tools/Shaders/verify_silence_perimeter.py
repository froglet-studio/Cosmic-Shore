#!/usr/bin/env python3
"""
Compile SilencePerimeter.shader's two passes (Docs/ElementalAbilitySystem/SILENCE.md §5) before an editor ever
opens it: each pass's vertex and fragment stages, with the shader's HLSLINCLUDE block, through glslang in HLSL
mode, against a declarations-only mock of the URP calls the shader makes (Core.hlsl's screen UV, full-screen
triangle, space transforms, texture macros). This catches a typo, a missing declaration or a reserved word
(an early port named a variable `line`, which HLSL reserves for geometry shaders).

It does NOT prove the pixels match the lab's GLSL (the lab ran that GLSL in WebGL), nor compile against the real
URP library for every platform; the editor does that. Negative control: the shard pass with the HLSLINCLUDE's
SampleScene removed must FAIL.

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
    if len(passes) != 2:
        print(f"FAIL: expected 2 passes, found {len(passes)}")
        return 1
    failed = False
    with tempfile.TemporaryDirectory() as tmp:
        for tag, program in zip(("warp", "shard"), passes):
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
    print("FAILED" if failed else "PASS")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
