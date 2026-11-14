#!/usr/bin/env python3
"""
Compiles the blast look's two shaders (Assets/_Graphics/Materials/Graphs/BlastFacet.shader and BlastShell.shader,
Assets/_Scripts/Controller/Projectiles/BlastLook/BLAST_LOOK.md) the way verify_silence_perimeter.py compiles its
shader: every pass's vertex and fragment stage, with the shader's HLSLINCLUDE block, through glslang in HLSL mode,
against a declarations-only mock of the URP calls the shaders make. A name the shaders use that the mock lacks
fails, as it would without the include.

It also runs the negative control: a copy with a misspelt property in the fragment must FAIL, so a pass that
compiles nothing cannot read as green.

Needs glslangValidator (apt: glslang-tools). Exit 0 on pass.
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GRAPHS = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "Graphs")
SHADERS = ("BlastFacet.shader", "BlastShell.shader")

MOCK = r"""
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
#define UNITY_VERTEX_INPUT_INSTANCE_ID
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(input)
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output)
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_MatrixVP;
float3 _WorldSpaceCameraPos;
float3 TransformObjectToWorld(float3 p) { return mul(unity_ObjectToWorld, float4(p, 1.0)).xyz; }
float4 TransformWorldToHClip(float3 p) { return mul(unity_MatrixVP, float4(p, 1.0)); }
float3 TransformObjectToWorldNormal(float3 n) { return normalize(mul(n, (float3x3)unity_WorldToObject)); }
float3 GetCameraPositionWS() { return _WorldSpaceCameraPos; }
"""

CORE = '#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"'


def blocks(text):
    m = re.search(r"HLSLINCLUDE(.*?)ENDHLSL", text, re.S)
    include = m.group(1) if m else ""
    passes = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", text, re.S)
    return include, passes


def compile_pass(include, program, workdir, tag):
    vert = re.search(r"#pragma vertex (\w+)", program).group(1)
    frag = re.search(r"#pragma fragment (\w+)", program).group(1)
    body = re.sub(r"#pragma .*", "", program).replace(CORE, "")
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


def compile_shader(text, workdir, name):
    include, passes = blocks(text)
    if not passes:
        return [f"{name}: no HLSLPROGRAM pass found"]
    errors = []
    for i, program in enumerate(passes):
        errors += compile_pass(include, program, workdir, f"{name}-pass{i}")
    return errors


def main():
    if not shutil.which("glslangValidator"):
        print("SKIPPED: glslangValidator not found (apt install glslang-tools)")
        return 0
    failures = []
    with tempfile.TemporaryDirectory() as workdir:
        for name in SHADERS:
            with open(os.path.join(GRAPHS, name), encoding="utf-8") as f:
                text = f.read()
            errs = compile_shader(text, workdir, name.split(".")[0])
            print(f"{name}: {'OK' if not errs else 'FAILED'} ({len(blocks(text)[1])} pass(es))")
            failures += errs
            # Negative control: a misspelt property in the fragment must not compile.
            broken = re.sub(r"(_BrightColor)\.rgb", r"\1Typo.rgb", text, count=1)
            assert broken != text, f"{name}: negative control found nothing to break"
            if not compile_shader(broken, workdir, name.split(".")[0] + "-control"):
                failures.append(f"{name}: the negative control compiled - the check proves nothing")
    for f in failures:
        print(f)
    print("RESULT:", "PASS" if not failures else "FAIL")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
