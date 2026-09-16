#!/usr/bin/env python3
"""
Prove the occlusion corridor keeps a see-through corridor on EVERY vessel in the fleet.

The corridor ends short of the ship by a NOSE CLEARANCE so the mass the pilot is about
to hit reads solid and the impact lands visibly (PrismOcclusionCorridor.hlsl, "THE NOSE
CLEARANCE"). That clearance is written in HULL RADII while the corridor's LENGTH is the
camera distance, and the fleet's authored camera distances span 6.72 (Urchin) to 250
(Serpent) — so what the clearance costs is the ratio

    rho = cameraDistance / hullRadius

and nothing else. The file's own degenerate-case note says the corridor is only lost when
the camera sits inside one hull radius (rho <= 1). That was an analysis of `tSolid` alone
and it missed the AXIAL GRADE: `baseBand` is a second (outerRadius - innerRadius)/axisLen
subtracted from the same end, so the FULLY-CLEAR region actually vanishes at rho = 1.75,
and it is still under half the corridor at rho = 3.5. On a close-camera hull that is most
of the tunnel — the corridor is nearly inert there, which reads in play as debris and
trail mass sitting solid in front of the ship while the same mass dissolves for a
long-camera hull.

This compiles the SHIPPED corridor's stage 1 with clang++ (SHADERGRAPH_PREVIEW defined,
which is what puts the camera at the origin and preprocesses the dither away) and sweeps
rho, so the numbers are the shader's own, not a transcription.

Asserts, each with a negative control that must fail:
  T1  every rho > 1 keeps a non-empty fully-clear corridor          (the file's own claim)
  T2  the clearance + its grade never take more than MAX_BASE_SHARE
  T3  BIT-IDENTICAL to the pre-shrink formula wherever it does not bite (rho >= 3.5)

Exit 0 on pass. Needs clang++; nothing else, and no Unity.
Usage: [--check] is accepted and is the same thing (this tool only ever reads).
"""

import os
import re
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(REPO, "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl")

# Authored camera distances, |followOffset| from Assets/_SO_Assets/Camera/*.asset. Read
# here only to report where each hull lands on the rho axis; nothing is asserted against
# them, because the hull RADIUS is measured in the editor (PrismOcclusionCorridor
# .MeasureCircumscribedRadius) and is not readable from assets out of Unity.
CAMERA_DISTANCE = {
    "Urchin": 6.72, "Squirrel": 17.0, "Dolphin": 20.0, "Manta": 30.0,
    "Scarab": 50.0, "Sparrow": 51.0, "Rhino": 70.0, "Serpent": 250.0,
}

SHIM = r"""#pragma once
#include <cmath>
#include <algorithm>
struct float3 {
  float x,y,z;
  float3():x(0),y(0),z(0){}
  float3(float a):x(a),y(a),z(a){}
  float3(float a,float b,float c):x(a),y(b),z(c){}
};
static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
static inline float clamp(float v,float lo,float hi){return std::min(hi,std::max(lo,v));}
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
static inline float min(float a,float b){return a<b?a:b;}
static inline float min(float a,double b){return a<(float)b?a:(float)b;}
static inline float min(double a,float b){return (float)a<b?(float)a:b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float max(float a,double b){return a>(float)b?a:(float)b;}
using std::sqrt;
#define SHADERGRAPH_PREVIEW 1
"""

MAIN = r"""#include "shim.h"
#include "slice.h"
#include <cstdio>

// stdin: "D R t" — camera at the origin (SHADERGRAPH_PREVIEW), ship at (0,0,D), the
// sample ON THE AXIS at (0,0,t*D). Params/near are the shipped PrismOcclusionConfig
// defaults expressed against R: outer = R, inner = 0.25R, coreAlpha = 0, near = 0.5R.
int main()
{
    double D, R, t;
    while (scanf("%lf %lf %lf", &D, &R, &t) == 3) {
        _PrismOcclusionNearRadius = (float)(0.5 * R);
        float3 target(0.0f, 0.0f, (float)D);
        float3 p(0.0f, 0.0f, (float)(t * D));
        float3 params((float)R, (float)(0.25 * R), 0.0f);
        float a, thr;
        PrismOcclusionFade_float(p, target, params, 1.0f, a, thr);
        printf("%.9g\n", a);
    }
    return 0;
}
"""

MAX_BASE_SHARE_NAME = "PRISM_OCCLUSION_MAX_BASE_SHARE"


def extract_slice(text, legacy=False):
    """The shipped corridor's stage 1, verbatim, plus what it calls."""
    parts = ["float _PrismOcclusionNearRadius;"]
    for const in ("PRISM_OCCLUSION_NOSE_CLEARANCE", MAX_BASE_SHARE_NAME):
        m = re.search(rf"^static const float {const} = [-\d.]+;", text, re.M)
        if const == MAX_BASE_SHARE_NAME and m is None:
            continue  # pre-fix tree; the legacy control below is then the shipped shape
        assert m, f"{const} not found in the shipped HLSL"
        parts.append(m.group(0))

    def body(sig):
        i = text.index(sig)
        j = text.index("\n{\n", i)
        k = text.index("\n}\n", j)
        return text[i:k + 3]

    parts.append(body("float PrismOcclusionSmootherStep(float t)"))
    fn = body("void PrismOcclusionFade_float(")
    assert fn.count("out float Alpha, out float ClipThreshold") == 1, \
        "corridor out-parameter shape drifted"
    fn = fn.replace("out float Alpha, out float ClipThreshold",
                    "float &Alpha, float &ClipThreshold")
    if legacy:
        # NEGATIVE CONTROL: the pre-2026-09-16 base, where the clearance and its grade
        # were each an unbounded fraction of the corridor. Asserted so a rewrite that
        # moves either line fails here rather than turning the control into a copy.
        assert "float shrink = min(1.0," in fn, "the base-share shrink is not where this control expects it"
        fn = fn.replace("float shrink = min(1.0, " + MAX_BASE_SHARE_NAME +
                        " / max(baseShare, 1e-4));", "float shrink = 1.0;")
    parts.append(fn)
    return "#pragma once\n" + "\n".join(parts) + "\n"


def build(workdir, slice_src, name):
    open(os.path.join(workdir, "shim.h"), "w").write(SHIM)
    open(os.path.join(workdir, "slice.h"), "w").write(slice_src)
    open(os.path.join(workdir, "main.cpp"), "w").write(MAIN)
    exe = os.path.join(workdir, name)
    r = subprocess.run(["clang++", "-std=c++17", "-O1", "-x", "c++",
                        os.path.join(workdir, "main.cpp"), "-o", exe],
                       capture_output=True, text=True, cwd=workdir)
    assert r.returncode == 0, "the shipped corridor slice did not compile:\n" + r.stderr
    return exe


def run(exe, queries):
    payload = "\n".join(f"{d!r} {r!r} {t!r}" for d, r, t in queries) + "\n"
    p = subprocess.run([exe], input=payload, capture_output=True, text=True)
    assert p.returncode == 0, p.stderr
    out = p.stdout.strip().splitlines()
    assert len(out) == len(queries), f"{len(out)} results for {len(queries)} queries"
    return [float(v) for v in out]


STEPS = 2001


def clear_fraction(exe, rho, R=6.0):
    """The largest t at which the corridor is FULLY clear on its own axis, /1."""
    D = rho * R
    ts = [i / (STEPS - 1.0) for i in range(STEPS)]
    a = run(exe, [(D, R, t) for t in ts])
    last = -1
    for i, v in enumerate(a):
        if v <= 1e-6:
            last = i
    return (last / (STEPS - 1.0)) if last >= 0 else 0.0


def main():
    text = open(HLSL).read()
    fixed = MAX_BASE_SHARE_NAME in text
    with tempfile.TemporaryDirectory() as wd:
        shipped = build(os.path.join(wd), extract_slice(text), "shipped")
        legacy = None
        if fixed:
            os.makedirs(os.path.join(wd, "legacy"), exist_ok=True)
            legacy = build(os.path.join(wd, "legacy"), extract_slice(text, legacy=True), "legacy")

        rhos = [1.25, 1.5, 1.75, 2.0, 2.5, 2.833, 3.0, 3.5, 4.0, 5.0, 6.0, 8.5, 11.7, 41.7]
        print("rho = camera distance in hull radii | fully-clear fraction of the corridor")
        bad = []
        for rho in rhos:
            s = clear_fraction(shipped, rho)
            l = clear_fraction(legacy, rho) if legacy else s
            mark = "" if s > 0.0 else "   <-- NO see-through corridor at all"
            print(f"  rho {rho:6.3f}   shipped {s:5.3f}   pre-fix {l:5.3f}{mark}")
            if rho > 1.0 and s <= 0.0:
                bad.append(rho)

        print()
        print("fleet camera distances (hull radius is measured in-editor; the rho each hull")
        print("lands on is cameraDistance / that radius):")
        for k, v in sorted(CAMERA_DISTANCE.items(), key=lambda kv: kv[1]):
            print(f"  {k:9s} camera {v:7.2f}   rho at hull radius 6 = {v / 6.0:6.2f}")
        print()

        ok = True
        if not fixed:
            print("T1 clear corridor for every rho > 1 : FAIL (pre-fix tree — "
                  f"{len(bad)} of {len(rhos)} sampled rho have none)")
            return 1

        if bad:
            print(f"T1 clear corridor for every rho > 1 : FAIL at rho {bad}")
            ok = False
        else:
            print("T1 clear corridor for every rho > 1 : PASS")
            lost = [r for r in rhos if r > 1.0 and clear_fraction(legacy, r) <= 0.0]
            print(f"   negative control (pre-fix)       : {len(lost)} of "
                  f"{len([r for r in rhos if r > 1.0])} sampled rho had NO clear corridor "
                  f"(worst {max(lost) if lost else 0})")
            assert lost, "the negative control did not reproduce the defect"

        share = float(re.search(rf"{MAX_BASE_SHARE_NAME} = ([\d.]+);", text).group(1))
        worst = min(clear_fraction(shipped, r) for r in rhos)
        if worst < 1.0 - share - 1e-3:
            print(f"T2 base share never exceeds {share}       : FAIL (worst clear {worst:.3f})")
            ok = False
        else:
            print(f"T2 base share never exceeds {share}       : PASS "
                  f"(worst clear fraction {worst:.3f})")

        # T3 — bit-identical wherever the shrink does not bite. With the shipped
        # clearance 1.0 and inner 0.25, the base share is 1.75/rho, so the shrink is
        # inert for rho >= 1.75 / share.
        inert_rho = 1.75 / share
        qs = []
        for rho in [inert_rho, inert_rho * 1.01, 4.0, 6.0, 12.0, 40.0]:
            D = rho * 6.0
            qs += [(D, 6.0, i / 400.0) for i in range(401)]
        a, b = run(shipped, qs), run(legacy, qs)
        drift = sum(1 for x, y in zip(a, b) if x != y)
        if drift:
            print(f"T3 bit-identical for rho >= {inert_rho:.2f}      : FAIL "
                  f"({drift} of {len(qs)} samples differ)")
            ok = False
        else:
            print(f"T3 bit-identical for rho >= {inert_rho:.2f}      : PASS "
                  f"({len(qs)} samples, exact)")
            near = run(legacy, [(1.5 * 6.0, 6.0, i / 400.0) for i in range(401)])
            far = run(shipped, [(1.5 * 6.0, 6.0, i / 400.0) for i in range(401)])
            assert any(x != y for x, y in zip(near, far)), \
                "the negative control did not differ where the shrink DOES bite"

        return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
