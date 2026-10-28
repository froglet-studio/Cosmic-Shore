#!/usr/bin/env python3
"""Compile the Meerkat's wormhole tube shader (Assets/_Graphics/Materials/Graphs/MeerkatWaterTube.shader) headless.

    python3 Tools/Shaders/verify_meerkat_water_tube.py

Both stages, in both keyword variants (the single tunnel, and MEERKAT_TUBE_NETWORK for the omni crystal's network), are
compiled with glslang (HLSL front end, Vulkan target) against a small mock of the URP library surface the shader uses
(Core.hlsl's transforms and screen UV, DeclareOpaqueTexture.hlsl's SampleSceneColor). Static checks: every Property is in
the UnityPerMaterial CBUFFER (SRP Batcher), the material assets enable the right keyword, and no domain colour survives.
A negative control plants an undeclared name and requires the compile to FAIL. Exit 0 = pass. Needs glslangValidator.
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SHADER = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "Graphs", "MeerkatWaterTube.shader")
MATS = {
    os.path.join(ROOT, "Assets", "Resources", "MeerkatWaterTube.mat"): False,
    os.path.join(ROOT, "Assets", "Resources", "MeerkatWaterTubeNetwork.mat"): True,
}

URP = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/"
MOCK = {
    URP + "Core.hlsl": r"""// mock: the URP Core.hlsl surface MeerkatWaterTube uses
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
#define TEXTURE2D_X(t) Texture2D t
#define SAMPLER(s) SamplerState s
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_MatrixV;
float4x4 unity_MatrixVP;
float3 _WorldSpaceCameraPos;
float4 _ScaledScreenParams;
float3 TransformObjectToWorld(float3 p) { return mul(unity_ObjectToWorld, float4(p, 1.0)).xyz; }
float4 TransformWorldToHClip(float3 p) { return mul(unity_MatrixVP, float4(p, 1.0)); }
float3 TransformObjectToWorldNormal(float3 n) { return normalize(mul(n, (float3x3)unity_WorldToObject)); }
float3 TransformWorldToViewDir(float3 d, bool doNormalize = false)
{
    float3 r = mul((float3x3)unity_MatrixV, d);
    return doNormalize ? normalize(r) : r;
}
float3 GetWorldSpaceViewDir(float3 p) { return _WorldSpaceCameraPos - p; }
float2 GetNormalizedScreenSpaceUV(float4 positionCS) { return positionCS.xy / _ScaledScreenParams.xy; }
SamplerState sampler_LinearClamp;
""",
    URP + "DeclareOpaqueTexture.hlsl": r"""// mock: URP DeclareOpaqueTexture.hlsl
TEXTURE2D_X(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);
float3 SampleSceneColor(float2 uv) { return _CameraOpaqueTexture.SampleLevel(sampler_CameraOpaqueTexture, uv, 0).rgb; }
""",
}


def compile_stage(work, program, stage, entry, defines):
    body = re.sub(r"#pragma[^\n]*\n", "\n", program)
    path = os.path.join(work, f"tube_{stage}.hlsl")
    with open(path, "w") as f:
        f.write(body)
    cmd = ["glslangValidator", "-D", "-V", "--target-env", "vulkan1.1", "-S", stage, "-e", entry,
           "-I" + os.path.join(work, "mock"), "-o", os.devnull] + [f"-D{d}" for d in defines] + [path]
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.returncode, (r.stdout + r.stderr).strip()


def static_checks(text):
    ok = True
    props = re.search(r"Properties\s*\{(.*?)\n    \}", text, re.S).group(1)
    names = re.findall(r"^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(", props, re.M)
    cbuf = re.search(r"CBUFFER_START\(UnityPerMaterial\)(.*?)CBUFFER_END", text, re.S).group(1)
    missing = [n for n in names if not re.search(r"\b" + n + r"\b", cbuf)]
    print(f"SRP Batcher: {len(names)} properties, {'all in UnityPerMaterial' if not missing else 'MISSING ' + str(missing)}")
    ok &= not missing
    domain = re.search(r"_Domain\b", text)
    print(f"no domain colour in the shader: {'yes' if not domain else 'NO'}")
    ok &= not domain
    assert "#pragma shader_feature_local_fragment _ MEERKAT_TUBE_NETWORK" in text
    for path, network in MATS.items():
        if not os.path.exists(path):
            print(f"MISSING material {os.path.relpath(path, ROOT)}")
            ok = False
            continue
        mat = open(path, encoding="utf-8").read()
        has = "MEERKAT_TUBE_NETWORK" in re.search(r"m_ValidKeywords:(.*?)\n  m_InvalidKeywords", mat, re.S).group(1)
        good = has == network and "9afb482b928e45ebbafd69cb4b24934c" in mat
        print(f"{os.path.basename(path)}: keyword {'on' if has else 'off'}, shader guid ok: {good}")
        ok &= good
    return ok


def main() -> int:
    if shutil.which("glslangValidator") is None:
        print("glslangValidator not found", file=sys.stderr)
        return 2
    text = open(SHADER, encoding="utf-8").read()
    programs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", text, re.S)
    assert len(programs) == 1, f"expected 1 pass, found {len(programs)}"
    prog = programs[0]
    vert = re.search(r"#pragma vertex (\w+)", prog).group(1)
    frag = re.search(r"#pragma fragment (\w+)", prog).group(1)
    ok = static_checks(text)
    work = tempfile.mkdtemp(prefix="verify_meerkat_tube_")
    try:
        for rel, src in MOCK.items():
            path = os.path.join(work, "mock", rel)
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w") as f:
                f.write(src)
        for variant, defines in (("tunnel", []), ("network", ["MEERKAT_TUBE_NETWORK"])):
            for stage, entry in (("vert", vert), ("frag", frag)):
                rc, out = compile_stage(work, prog, stage, entry, defines)
                print(f"{'compiled' if rc == 0 else 'COMPILE FAIL'} {entry} [{stage}, {variant}]" + ("" if rc == 0 else "\n" + out))
                ok &= rc == 0
        rc, _ = compile_stage(work, prog.replace("_Iri);", "_IriUndeclared);"), "frag", frag, ["MEERKAT_TUBE_NETWORK"])
        print(f"negative control [undeclared uniform in the network variant]: {'FIRED' if rc != 0 else 'DID NOT FIRE'}")
        ok &= rc != 0
    finally:
        shutil.rmtree(work, ignore_errors=True)
    print("PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
