#!/usr/bin/env python3
"""Compile the Stoat's annihilation-ripple shader (Assets/_Graphics/Materials/Graphs/StoatRipple.shader) headless.

    python3 Tools/Shaders/verify_stoat_ripple.py

Both stages are compiled with glslang against the per-file URP mock that verify_black_hole_lens.py keeps (the ripple
is drawn by the same pass, BlackHoleLensPass, and leans on the same library surface: GetNormalizedScreenSpaceUV,
ComputeScreenPos, _BlackHoleSceneColor through sampler_LinearClamp). A negative control plants an undeclared name in
the fragment stage and requires the compile to FAIL, so a green run is evidence the mock is really checking. Exit 0 =
pass. Needs glslangValidator (`apt-get install glslang-tools`). R_VesselActions/STOAT_DIPOLE.md section 1c.
"""
import os
import re
import shutil
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import verify_black_hole_lens as lens  # noqa: E402  (the mock library and the glslang driver)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SHADER = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "Graphs", "StoatRipple.shader")


MESH_MOCK = r"""
// mock (verify_stoat_ripple.py): what a mesh pass adds from URP Core.hlsl
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
#define UNITY_REVERSED_Z 1
#define UNITY_VERTEX_INPUT_INSTANCE_ID
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(v)
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)
#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)
float4x4 unity_ObjectToWorld;
float4x4 unity_MatrixVP;
#define UNITY_MATRIX_VP unity_MatrixVP
float3 _WorldSpaceCameraPos;
float3 TransformObjectToWorld(float3 positionOS) { return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz; }
float4 TransformWorldToHClip(float3 positionWS) { return mul(unity_MatrixVP, float4(positionWS, 1.0)); }
float4 _ProjectionParams;
float4 ComputeScreenPos(float4 positionCS) { return positionCS; }
"""


def main() -> int:
    if shutil.which("glslangValidator") is None:
        print("glslangValidator not found", file=sys.stderr)
        return 2
    text = open(SHADER, encoding="utf-8").read()
    programs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", text, re.S)
    assert len(programs) == 1, f"expected 1 pass, found {len(programs)}"
    assert '"LightMode" = "BlackHoleLens"' in text, "the ripple must be drawn by BlackHoleLensPass (LightMode BlackHoleLens)"
    prog = programs[0]
    vert = re.search(r"#pragma vertex (\w+)", prog).group(1)
    frag = re.search(r"#pragma fragment (\w+)", prog).group(1)
    work = tempfile.mkdtemp(prefix="verify_stoat_ripple_")
    ok = True
    try:
        lens.write_mock_library(work)
        # The lens's mock declares only what the full-screen lens uses; the ripple is a MESH (object -> world -> clip,
        # instanced, stereo) with a per-material CBUFFER, so its own declarations go on top. Without them every run
        # failed to parse at CBUFFER_START (found 2026-10-29) and the negative control "fired" on that, not on the uniform.
        with open(os.path.join(work, "mock", lens.URP + "Core.hlsl"), "a") as f:
            f.write(MESH_MOCK)
        for stage, entry in (("vert", vert), ("frag", frag)):
            rc, out = lens.glslang_compile(work, prog, stage, entry)
            print(f"{'compiled' if rc == 0 else 'COMPILE FAIL'} {entry} [{stage}]" + ("" if rc == 0 else "\n" + out))
            ok &= rc == 0
        rc, _ = lens.glslang_compile(work, prog.replace("_RippleBend * profile", "_RippleBendUndeclared * profile"), "frag", frag)
        print(f"negative control [undeclared uniform]: {'FIRED' if rc != 0 else 'DID NOT FIRE'}")
        ok &= rc != 0
    finally:
        shutil.rmtree(work, ignore_errors=True)
    print("PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
