#!/usr/bin/env python3
"""Compile the Stoat's annihilation-ripple shader (Assets/_Graphics/Materials/Graphs/StoatRipple.shader) headless.

    python3 Tools/Shaders/verify_stoat_ripple.py

Both stages are compiled with glslang against the URP mock that verify_black_hole_lens.py keeps: since 2026-11-21 the
ripple is a full-screen triangle drawn by its own pass (StoatRipplePass) from its own copy of the camera colour, on the
same library surface as the lens (GetFullScreenTriangleVertexPosition, GetNormalizedScreenSpaceUV, a scene copy through
sampler_LinearClamp). It also holds that the ripple paints no colour of its own (Yash: "no domain color"). A negative
control plants an undeclared name in the fragment stage and requires the compile to FAIL, so a green run is evidence the
mock is really checking. Exit 0 = pass. Needs glslangValidator (`apt-get install glslang-tools`; the Vulkan SDK on
Windows). R_VesselActions/STOAT_DIPOLE.md section 1c.
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


def main() -> int:
    if shutil.which("glslangValidator") is None:
        print("glslangValidator not found", file=sys.stderr)
        return 2
    text = open(SHADER, encoding="utf-8").read()
    programs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", text, re.S)
    assert len(programs) == 1, f"expected 1 pass, found {len(programs)}"
    assert '"LightMode" = "StoatRipple"' in text, "the ripple is drawn by StoatRipplePass alone (LightMode StoatRipple)"
    prog = programs[0]
    assert "Tint" not in prog and "_Color" not in prog, "the ripple paints no colour of its own (no tint, no rim)"
    vert = re.search(r"#pragma vertex (\w+)", prog).group(1)
    frag = re.search(r"#pragma fragment (\w+)", prog).group(1)
    work = tempfile.mkdtemp(prefix="verify_stoat_ripple_")
    ok = True
    try:
        lens.write_mock_library(work)
        for stage, entry in (("vert", vert), ("frag", frag)):
            rc, out = lens.glslang_compile(work, prog, stage, entry)
            print(f"{'compiled' if rc == 0 else 'COMPILE FAIL'} {entry} [{stage}]" + ("" if rc == 0 else "\n" + out))
            ok &= rc == 0
        rc, _ = lens.glslang_compile(work, prog.replace("a.x * profile", "_StoatRippleUndeclared * profile"), "frag", frag)
        print(f"negative control [undeclared uniform]: {'FIRED' if rc != 0 else 'DID NOT FIRE'}")
        ok &= rc != 0
    finally:
        shutil.rmtree(work, ignore_errors=True)
    print("PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
