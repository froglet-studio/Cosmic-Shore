#!/usr/bin/env python3
"""
Prove the prism BURN shaders (Docs/Shaders/PRISM_EFFECTS.md) are mobile-safe and compile for every API a phone uses.

  PrismLaserBurn.shader  Fulmen's flameless burn: red-hot line peels the skin to domain-coloured ash, ash crumbles away.
  PrismPaperBurn.shader  the Grizzly claw's paper burn (both tiers: _BURN_CURL on / off).
  PrismEffectCommon.hlsl the shared noise / hash / dither include both use.

S. STATIC. Laser: #pragma target 3.0 (GLES 3.0), opaque alpha-clip (AlphaTest queue, ZWrite On, no Blend), no
   GrabPass / opaque / depth texture / texture sample, no HDR colour property, no vertex displacement, one colour pass +
   DepthOnly, both include PrismEffectCommon.hlsl and neither redefines its helpers. The LDR ember: every colour default
   is <= 1 (no bloom-fed light expansion: the ember is a thin line, not a light source).
A. THE PIXEL (clang++). The laser's burn maths, translated mechanically from the shader's HLSLINCLUDE to C++:
   1. at progress 0 nothing is clipped (the whole skin shows); 2. at progress 1 EVERY point is gone (the proxy
   vanishes exactly at burnout - never a leftover shell); 3. the skin front leads the ash front at every progress (red-hot
   first, then ash, then nothing); 4. a NO-HOLES (convert) burn never clips. Negative control: the ash lag removed from the
   span (span = 1) must FAIL 2.
B. COMPILE. DXC against the real URP library for D3D11, Vulkan, Metal and GLES 3.0 (DXC -> SPIR-V -> spirv-cross
   --es 300 -> glslangValidator), every pass x every keyword variant. Negative control: an undefined helper must FAIL.
C. COST. Fragment DXIL instructions per shader/variant (printed).

Usage:  python3 Tools/Shaders/verify_prism_burn.py [--keep] [--require-real] [--dump-dxil DIR]
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import verify_black_hole_lens as lens  # noqa: E402

ROOT = lens.ROOT
BURN_DIR = os.path.join(ROOT, "Assets/_Graphics/Materials/PrismBurn")
LASER = os.path.join(BURN_DIR, "PrismLaserBurn.shader")
PAPER = os.path.join(BURN_DIR, "PrismPaperBurn.shader")
COMMON = os.path.join(BURN_DIR, "PrismEffectCommon.hlsl")
GLES3_API = "Packages/com.unity.render-pipelines.core/ShaderLibrary/API/GLES3.hlsl"


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def passes(shader):
    """[(name, program source with the HLSLINCLUDE prepended, vertex entry, fragment entry)]"""
    inc = re.search(r"HLSLINCLUDE(.*?)ENDHLSL", shader, re.S)
    inc = inc.group(1) if inc else ""
    out = []
    for m in re.finditer(r'Name "([^"]+)".*?HLSLPROGRAM(.*?)ENDHLSL', shader, re.S):
        prog = m.group(2)
        v = re.search(r"#pragma vertex (\w+)", prog).group(1)
        f = re.search(r"#pragma fragment (\w+)", prog).group(1)
        out.append((m.group(1), inc + "\n" + prog, v, f))
    return out


def keywords(shader):
    """Every variant Unity builds: the material keyword (if any) x instancing off / on."""
    m = re.search(r"#pragma shader_feature_local (\w+)", shader)
    base = [[], [m.group(1)]] if m else [[]]
    # INSTANCING_ON: our per-instance _Burn/_Ash arrays. UNITY_DONT_INSTANCE_OBJECT_MATRICES because DXC's preprocessor
    # will not expand UnityInstancing.hlsl's unity_Builtins token-paste (Unity's own compiler does); the object matrices
    # are Unity's code, not ours - what this variant proves is OUR instanced-property access.
    return [k + inst for k in base for inst in ([], ["INSTANCING_ON", "UNITY_DONT_INSTANCE_OBJECT_MATRICES"])]


def static_checks(laser, paper):
    fails = []

    def need(c, what):
        print(f"   {'ok  ' if c else 'FAIL'} {what}")
        if not c:
            fails.append(what)

    code = re.sub(r"//[^\n]*", "", laser)
    need("#pragma target 3.0" in code, "laser: #pragma target 3.0 (GLES 3.0)")
    need('"Queue" = "AlphaTest"' in code and "ZWrite On" in code and not re.search(r"\bBlend\b", code),
         "laser: opaque alpha-clip (AlphaTest, ZWrite On, no blending)")
    for banned in ("GrabPass", "_CameraOpaqueTexture", "_CameraDepthTexture", "SampleSceneDepth", "TEXTURE2D", "tex2D",
                   "sampler", ".Sample(", "[HDR]", "_Time"):
        need(banned not in code, f"laser: no {banned}")
    need("Displace" not in code and "positionOS.xyz + " not in code, "laser: no vertex displacement (24-vertex cube)")
    need(len(passes(laser)) == 2, "laser: one colour pass + DepthOnly")
    for col in re.findall(r'_\w+ \("[^"]*", Color\) = \(([^)]*)\)', laser):
        vals = [float(x) for x in col.split(",")[:3]]
        need(max(vals) <= 1.0, f"laser: LDR colour default ({col})")
    for name, src in (("laser", laser), ("paper", paper)):
        c = re.sub(r"//[^\n]*", "", src)
        need('#include "PrismEffectCommon.hlsl"' in c, f"{name}: includes PrismEffectCommon.hlsl")
        for fn in ("float Hash31(", "float3 Hash33(", "float ValueNoise(", "float Bayer4("):
            need(fn not in c, f"{name}: does not redefine {fn.split('(')[0].split()[-1]}")
    return not fails


# ---------------------------------------------------------------------------------------------------------
# A. the laser's burn maths in C++

PIXEL = r"""
#include "shim.h"
#include <cstdio>
#include <cmath>
#include <random>
static float _NoiseFreq = NOISE_FREQ, _NoiseAmount = NOISE_AMOUNT, _AshLag = ASH_LAG;
// the HLSL intrinsics the shared helpers use that the lens shim lacks
static inline float frac(float x){ return x - std::floor(x); }
static inline float3 frac(float3 v){ return float3(frac(v.x), frac(v.y), frac(v.z)); }
static inline float3 floor(float3 v){ return float3(std::floor(v.x), std::floor(v.y), std::floor(v.z)); }
static inline float3& operator+=(float3& a, float b){ a = float3(a.x + b, a.y + b, a.z + b); return a; }
#include "common.h"
#include "laser.h"
static int fails = 0;
#define CHECK(c, ...) do { if (!(c)) { if (fails < 6) { printf("   FAIL: "); printf(__VA_ARGS__); printf("\n"); } fails++; } } while (0)
int main(){
    std::mt19937 rng(7); std::uniform_real_distribution<float> u(-0.5f, 0.5f), s(1.0f, 62.0f);
    int n = 0;
    for (int k = 0; k < 20000; k++){
        float3 p(u(rng), u(rng), u(rng)); int ax = k % 3; float side = (k & 8) ? 0.5f : -0.5f;
        if (ax == 0) p.x = side; else if (ax == 1) p.y = side; else p.z = side;   // on the cube's surface
        float seed = s(rng);
        float4 b0(0.0f, seed, 1.0f, 0.0f), b1(1.0f, seed, 1.0f, 0.0f);
        CHECK(LaserAshFront(p, b0) < 0.0f && LaserSkinFront(p, b0) <= 0.0f, "progress 0 clips (%g %g %g)", p.x, p.y, p.z);
        CHECK(LaserAshFront(p, b1) > 0.0f, "progress 1 leaves a point (%g %g %g) ash front %g", p.x, p.y, p.z, LaserAshFront(p, b1));
        for (float t = 0.0f; t <= 1.0f; t += 0.05f){
            float4 b(t, seed, 1.0f, 0.0f);
            CHECK(LaserSkinFront(p, b) >= LaserAshFront(p, b) - 1e-5f, "ash leads skin at t %g", t);
            float4 nh(t, seed, 1.0f, 2.0f);
            CHECK(LaserAshFront(p, nh) < 0.0f, "a NO-HOLES burn clips at t %g", t);
        }
        n++;
    }
    printf("   %d surface points x 21 progress steps: %s\n", n, fails ? "FAILED" : "0 at start, gone at 1, skin leads ash, no-holes never clips");
    return fails ? 1 : 0;
}
"""


def laser_to_cpp(laser):
    inc = re.search(r"HLSLINCLUDE(.*?)ENDHLSL", laser, re.S).group(1)
    field = re.search(r"(float LaserField\(float3 posOS, float seed\)\s*\{.*?\n        \})", inc, re.S).group(1)
    fronts = re.search(r"(float2 LaserFronts\(float field, float4 burn\)\s*\{.*?\n        \})", inc, re.S).group(1)
    fns = re.findall(r"(float \w+\(float3 posOS, float4 burn\) \{[^\n]*\})", inc)
    body = field + "\n" + fronts + "\n" + "\n".join(fns)
    body = body.replace("float3 posOS", "float3 posOS").replace("seed.xxx", "float3(seed, seed, seed)")
    body = re.sub(r"(?<![\w.])(\d+\.\d+)(?![\w.f])", r"\1f", body)
    return "static " + re.sub(r"\n(float2? \w+\()", r"\nstatic \1", body)


def common_to_cpp():
    src = read(COMMON)
    keep = []
    for name in ("Hash33", "Hash31", "ValueNoise", "PrismIgnitionPoint"):
        m = re.search(r"((?:float|float3) " + name + r"\(.*?\n\})", src, re.S)
        keep.append(m.group(1))
    body = "\n".join(keep)
    body = body.replace("p.yxz", "float3(p.y,p.x,p.z)").replace("p.zyx", "float3(p.z,p.y,p.x)")
    body = body.replace("(p.xxy + p.yxx) * p.zyx", "(float3(p.x,p.x,p.y) + float3(p.y,p.x,p.x)) * float3(p.z,p.y,p.x)")
    body = body.replace("(p.xxy + p.yxx) * float3(p.z,p.y,p.x)", "(float3(p.x,p.x,p.y) + float3(p.y,p.x,p.x)) * float3(p.z,p.y,p.x)")
    body = body.replace("i + 1.0", "i + float3(1.0f,1.0f,1.0f)").replace("seed.xxx", "float3(seed, seed, seed)")
    body = re.sub(r"(?<![\w.])(\d+\.\d+)(?![\w.f])", r"\1f", body)
    return "\n".join("static " + b if b.startswith(("float", "float3")) else b for b in body.split("\n"))


def material_floats():
    mat = read(os.path.join(BURN_DIR, "PrismLaserBurn.mat"))
    def f(n, d):
        m = re.search(r"- " + n + r": ([-\d.e]+)", mat)
        return float(m.group(1)) if m else d
    return f("_NoiseFreq", 3.2), f("_NoiseAmount", 0.42), f("_AshLag", 0.35)


def run_pixel(work, laser, label):
    nf, na, lag = material_floats()
    with open(os.path.join(work, "common.h"), "w") as f:
        f.write(common_to_cpp())
    with open(os.path.join(work, "laser.h"), "w") as f:
        f.write(laser_to_cpp(laser))
    return lens.build_and_run(work, PIXEL, [f"-DNOISE_FREQ={nf}f", f"-DNOISE_AMOUNT={na}f", f"-DASH_LAG={lag}f"], label)


# ---------------------------------------------------------------------------------------------------------
# B / C. compile

def overlay(work, root):
    src = os.path.join(root, GLES3_API)
    dst = os.path.join(work, "gles3_overlay", GLES3_API)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    old = "#define ERROR_ON_UNSUPPORTED_FUNCTION(funcName) #error #funcName is not supported on GLES 3.0"
    text = read(src)
    assert old in text
    with open(dst, "w") as f:
        f.write(text.replace(old, "#define ERROR_ON_UNSUPPORTED_FUNCTION(funcName) funcName##_is_not_supported_on_GLES3"))
    return os.path.join(work, "gles3_overlay")


def dxc(work, toolchain, prog, profile, entry, stage, api, kws, extra=(), out=None, listing=None):
    exe, root = toolchain
    path = os.path.join(work, f"p_{abs(hash((prog, entry, api, tuple(kws))))}.hlsl")
    with open(path, "w") as f:
        f.write(re.sub(r"#pragma[^\n]*\n", "\n", prog))
    cmd = [exe, "-T", profile, "-E", entry, "-HV", "2018", "-O3", *extra, "-I", root, "-I", BURN_DIR, "-D", api, "-D", stage]
    for d in lens.REAL_DEFINES + list(kws):
        cmd += ["-D", d]
    cmd += ["-Fo", out or os.devnull]
    if listing:
        cmd += ["-Fc", listing]
    cmd.append(path)
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.returncode, [l for l in (r.stdout + r.stderr).splitlines() if "error" in l]


def gles3(work, toolchain, prog, profile, entry, stage, kws, tag):
    ov = overlay(work, toolchain[1])
    spv = os.path.join(work, tag + ".spv")
    rc, err = dxc(work, toolchain, prog, profile, entry, stage, "SHADER_API_GLES3", kws, extra=("-spirv", "-I", ov), out=spv)
    if rc:
        return rc, err
    essl = os.path.join(work, tag + (".vert" if profile.startswith("vs") else ".frag"))
    r = subprocess.run([shutil.which("spirv-cross") or os.environ.get("SPIRV_CROSS"), "--es", "--version", "300", spv,
                        "--output", essl], capture_output=True, text=True)
    if r.returncode:
        return r.returncode, [r.stderr.strip()]
    r = subprocess.run(["glslangValidator", essl], capture_output=True, text=True)
    return r.returncode, [l for l in (r.stdout + r.stderr).splitlines() if "ERROR" in l]


def count(listing, entry):
    ll = read(listing)
    fn = ll[ll.index("define void @" + entry):]
    fn = fn[:fn.index("\n}")]
    return len([l for l in fn.splitlines() if l.startswith("  ") and not l.strip().startswith(";")])


def main():
    keep = "--keep" in sys.argv
    require_real = "--require-real" in sys.argv
    dump = sys.argv[sys.argv.index("--dump-dxil") + 1] if "--dump-dxil" in sys.argv else None
    work = tempfile.mkdtemp(prefix="verify_prism_burn_")
    ok = True
    try:
        laser, paper = read(LASER), read(PAPER)
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(lens.SHIM)
        print("S. static")
        ok &= static_checks(laser, paper)

        print("\nA. the laser burn's maths (C++)")
        rc, out = run_pixel(work, laser, "pixel")
        sys.stdout.write(out)
        ok &= rc == 0
        bad = laser.replace("float span = 1.05 / max(1.0 - _AshLag, 0.05);", "float span = 1.0;")
        assert bad != laser, "the span moved: update the negative control"
        rc, out = run_pixel(work, bad, "control")
        fired = rc not in (None, 0) and "progress 1" in out
        print(f"   negative control [ash lag left out of the span]: {'FIRED' if fired else 'DID NOT FIRE'}")
        ok &= fired

        print("\nB. DXC against the real URP library (every pass x keyword variant)")
        toolchain, why = lens.find_real_toolchain()
        if toolchain is None:
            print(f"   SKIPPED: {why}")
            return 1 if require_real else (0 if ok else 1)
        costs = []
        for label, src in (("PrismLaserBurn", laser), ("PrismPaperBurn", paper)):
            for kws in keywords(src):
                for pname, prog, v, fr in passes(src):
                    tag = f"{label}/{pname}{'+' + '+'.join(kws) if kws else ''}"
                    for api in lens.REAL_APIS:
                        for profile, entry, st in (("vs_6_0", v, "SHADER_STAGE_VERTEX"), ("ps_6_0", fr, "SHADER_STAGE_FRAGMENT")):
                            extra = ("-spirv",) if api == "SHADER_API_VULKAN" else ()
                            rc, err = dxc(work, toolchain, prog, profile, entry, st, api, kws, extra=extra)
                            if rc:
                                print(f"   FAIL {tag} {api} {profile}: {' | '.join(err[:3])}")
                            ok &= rc == 0
                    for profile, entry, st in (("vs_6_0", v, "SHADER_STAGE_VERTEX"), ("ps_6_0", fr, "SHADER_STAGE_FRAGMENT")):
                        rc, err = gles3(work, toolchain, prog, profile, entry, st, kws, re.sub(r"\W", "_", tag + profile))
                        if rc:
                            print(f"   FAIL {tag} GLES3 {profile}: {' | '.join(err[:3])}")
                        ok &= rc == 0
                    lst = os.path.join(work, re.sub(r"\W", "_", tag) + ".ll")
                    rc, _ = dxc(work, toolchain, prog, "ps_6_0", fr, "SHADER_STAGE_FRAGMENT", "SHADER_API_D3D11", kws, listing=lst)
                    if rc == 0:
                        costs.append((tag, count(lst, fr)))
                        if dump:
                            os.makedirs(dump, exist_ok=True)
                            shutil.copy(lst, os.path.join(dump, re.sub(r"\W", "_", tag) + ".ll"))
                    print(f"   ok   {tag}: D3D11 / Vulkan / Metal / GLES 3.0 (ESSL 3.00 validated)" if rc == 0 else f"   FAIL {tag}")
        p = passes(laser)[0]
        rc, _ = dxc(work, toolchain, p[1].replace("LaserField(", "LaserFieldMissing(", 1), "ps_6_0", p[3],
                    "SHADER_STAGE_FRAGMENT", "SHADER_API_VULKAN", [], extra=("-spirv",))
        print(f"   negative control [undefined helper]: {'FIRED' if rc else 'DID NOT FIRE'}")
        ok &= rc != 0
        print("\nC. fragment cost (DXIL instructions, D3D11, all branches)")
        for tag, n in costs:
            print(f"   {tag:40s} {n}")
    finally:
        if keep:
            print(f"(work kept in {work})")
        else:
            shutil.rmtree(work, ignore_errors=True)
    print("\nALL PASSED" if ok else "\nFAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
