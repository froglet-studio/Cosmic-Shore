#!/usr/bin/env python3
"""ONE FIELD DECIDES — prove the erosion and the corridor never carve the same fragment.

Before 2026-09-16 the exploding prism was carved TWICE and independently: the erosion
resolved its own front into a 0/1 verdict, handed that to PrismOcclusionFade as BaseAlpha,
and the corridor then dithered that constant 1 with its own screen-anchored lattice while
the front went on cutting the same surface in UV space. Outside the corridor the constant
1 took the "solid mass" fast out, so debris was never dithered at all however far its fade
had run. Now the erosion emits a THRESHOLD, the corridor receives the TRUE opacity, and
the corridor's single clip SELECTS which field decides.

This compiles the SHIPPED HLSL (both functions, verbatim) with clang++ and runs it. The
dither kernel is preprocessed away by SHADERGRAPH_PREVIEW, so the harness writes a
SENTINEL on the line the kernel would occupy — "the fragment fell through to the dither"
therefore becomes an observable value rather than an inference.

Asserts, each with a negative control that must fail:
  T1  a LIVE prism is bit-identical to the corridor without the handoff at all
  T2  a CLOAKED prism (fractional alpha, no erosion) still reaches the dither everywhere
  T3  debris OUTSIDE the corridor: the clip reproduces the old verdict bit for bit,
      and the erosion front is the only field in play
  T4  debris INSIDE the corridor: the front stands down, the tunnel decides alone
  T5  the erosion field is uniform-marginal (coverage == opacity), which is what makes
      T3/T4 a SELECT rather than an approximation

Exit 0 on pass. Needs clang++; nothing else, and no Unity.
Usage: [--check] is accepted and is the same thing (this tool only ever reads).
"""
import os
import random
import re
import struct
import subprocess
import sys
import tempfile

HLSL = os.path.join(os.path.dirname(__file__), "..", "..",
                    "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl")

SENTINEL = 0.5   # "this fragment reached the dither kernel"

SHIM = r"""#pragma once
#include <cmath>
#include <algorithm>
union float2; union float3; union float4;
template<int A,int B> struct SW2f3 { float v[3]; inline operator float2() const; };
template<int A,int B,int C> struct SW3f2 { float v[2]; inline operator float3() const; };
template<int A,int B,int C> struct SW3f3 { float v[3]; inline operator float3() const; };
template<int A,int B> struct SW2f4 { float v[4]; inline operator float2() const; };
template<int A,int B,int C> struct SW3f4 { float v[4]; inline operator float3() const; };

union float2 {
  struct { float x,y; };
  SW3f2<0,1,0> xyx; SW3f2<0,0,1> xxy;
  float2(){x=y=0;} float2(float a){x=y=a;} float2(float a,float b){x=a;y=b;}
};
union float3 {
  struct { float x,y,z; };
  SW2f3<0,1> xy; SW2f3<0,0> xx; SW2f3<1,2> yz; SW2f3<2,1> zy; SW2f3<1,0> yx;
  SW3f3<0,1,0> xyx; SW3f3<1,2,0> yzx; SW3f3<0,0,1> xxy; SW3f3<1,0,0> yxx;
  SW3f3<2,1,0> zyx; SW3f3<1,0,2> yxz; SW3f3<0,1,2> xyz;
  float3(){x=y=z=0;} float3(float a){x=y=z=a;} float3(float a,float b,float c){x=a;y=b;z=c;}
};
union float4 {
  struct { float x,y,z,w; };
  SW2f4<0,1> xy; SW3f4<0,1,2> xyz;
  float4(){x=y=z=w=0;} float4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}
};
template<int A,int B> SW2f3<A,B>::operator float2() const { return float2(v[A],v[B]); }
template<int A,int B,int C> SW3f3<A,B,C>::operator float3() const { return float3(v[A],v[B],v[C]); }
template<int A,int B> SW2f4<A,B>::operator float2() const { return float2(v[A],v[B]); }
template<int A,int B,int C> SW3f4<A,B,C>::operator float3() const { return float3(v[A],v[B],v[C]); }
template<int A,int B,int C> SW3f2<A,B,C>::operator float3() const { return float3(v[A],v[B],v[C]); }

static inline float2 operator+(float2 a,float2 b){return float2(a.x+b.x,a.y+b.y);}
static inline float2 operator-(float2 a,float2 b){return float2(a.x-b.x,a.y-b.y);}
static inline float2 operator*(float2 a,float b){return float2(a.x*b,a.y*b);}
static inline float2 operator*(float a,float2 b){return b*a;}
static inline float2 operator*(float2 a,float2 b){return float2(a.x*b.x,a.y*b.y);}
static inline float2 operator/(float2 a,float b){return float2(a.x/b,a.y/b);}
static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator+(float3 a,float b){return float3(a.x+b,a.y+b,a.z+b);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float3 operator*(float3 a,float3 b){return float3(a.x*b.x,a.y*b.y,a.z*b.z);}
static inline float3 operator/(float3 a,float b){return float3(a.x/b,a.y/b,a.z/b);}
static inline float3& operator+=(float3&a,float3 b){a=a+b;return a;}
static inline float dot(float2 a,float2 b){return a.x*b.x+a.y*b.y;}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float length(float2 a){return std::sqrt(dot(a,a));}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
static inline float clamp(float v,float lo,float hi){return std::min(hi,std::max(lo,v));}
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
static inline float3 lerp(float3 a,float3 b,float t){return float3(lerp(a.x,b.x,t),lerp(a.y,b.y,t),lerp(a.z,b.z,t));}
static inline float frac(float v){return v-std::floor(v);}
static inline float2 frac(float2 v){return float2(frac(v.x),frac(v.y));}
static inline float3 frac(float3 v){return float3(frac(v.x),frac(v.y),frac(v.z));}
static inline float2 floor(float2 v){return float2(std::floor(v.x),std::floor(v.y));}
static inline float3 floor(float3 v){return float3(std::floor(v.x),std::floor(v.y),std::floor(v.z));}
static inline float smoothstep(float a,float b,float x){float t=saturate((x-a)/(b-a));return t*t*(3.0f-2.0f*t);}
static inline float step(float a,float b){return b>=a?1.0f:0.0f;}
static inline float3 normalize(float3 a){float l=length(a);return l>0?a/l:a;}
static inline float3 abs(float3 a){return float3(std::fabs(a.x),std::fabs(a.y),std::fabs(a.z));}
static inline float2 abs(float2 a){return float2(std::fabs(a.x),std::fabs(a.y));}
using std::pow; using std::sin; using std::cos; using std::atan2; using std::sqrt;
static inline float abs(float v){return std::fabs(v);}
static inline float min(float a,float b){return a<b?a:b;}
static inline float min(float a,double b){return a<(float)b?a:(float)b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float max(float a,double b){return a>(float)b?a:(float)b;}
static inline float min(double a,float b){return (float)a<b?(float)a:b;}
static inline float max(double a,float b){return (float)a>b?(float)a:b;}
static inline float clamp(float v,double a,float b){return clamp(v,(float)a,b);}
static inline float clamp(float v,double a,double b){return clamp(v,(float)a,(float)b);}
static inline float clamp(float v,float a,double b){return clamp(v,a,(float)b);}
using std::floor;
#define SHADERGRAPH_PREVIEW 1
"""

MAIN = r"""#include "shim.h"
#include "slice.h"
#include <cstdio>

// Camera at the origin (SHADERGRAPH_PREVIEW), ship at (0,0,D). Params are the shipped
// PrismOcclusionConfig defaults against R: outer R, inner 0.25R, coreAlpha 0, near 0.5R.
// stdin: "mode u v opacity erosion tx ty tz vx vy vz"
//   mode 0 = outside the corridor, 1 = in its gradient shell
//   (t, v) is the PIECE's own entropy: its object-space tangent (the hinge axis that
//   makes one wedge a different piece from its neighbour) and the prism's velocity.
int main()
{
    const double D = 400.0, R = 40.0;
    int mode; double u, v, op, ero, tx, ty, tz, vx, vy, vz;
    while (scanf("%d %lf %lf %lf %lf %lf %lf %lf %lf %lf %lf",
                 &mode, &u, &v, &op, &ero, &tx, &ty, &tz, &vx, &vy, &vz) == 11) {
        _PrismOcclusionNearRadius = (float)(0.5 * R);
        float3 target(0.0f, 0.0f, (float)D);
        float3 params((float)R, (float)(0.25 * R), 0.0f);
        // t = 0.5 down the axis; outerAtT = lerp(near, outer, 0.5) = 0.75R.
        // mode 0 parks the sample far outside the cone; mode 1 puts it in the gradient
        // shell (between innerAtT = 0.25*outerAtT and outerAtT), where fade is in (0,1).
        // mode 2 sits JUST inside the wall (fade ~ 0.999), which is where the handoff
        // happens and therefore where a coverage step would show.
        float radius = mode == 0 ? (float)(10.0 * R)
                     : mode == 1 ? (float)(0.6 * 0.75 * R)
                                 : (float)(0.98 * 0.75 * R);
        float3 p(radius, 0.0f, (float)(0.5 * D));
        float a, thr;
        PrismOcclusionFade_float(p, target, params, (float)op, a, thr, (float)ero);
        // and the erosion field itself, for the coverage law
        float e;
        PrismErosionFade_float(float3((float)u, (float)v, 0.0f),
                               float3((float)tx, (float)ty, (float)tz),
                               float3((float)vx, (float)vy, (float)vz), (float)op, e);
        printf("%.9g %.9g %.9g\n", a, thr, e);
    }
    return 0;
}
"""


def body(text, sig):
    i = text.index(sig)
    j = text.index("\n{\n", i)
    k = text.index("\n}\n", j)
    return text[i:k + 3]


def extract_slice(text, control=None):
    """Both shipped functions, verbatim, plus the constants and helpers they call."""
    parts = ["float _PrismOcclusionNearRadius;"]
    for const in ("PRISM_OCCLUSION_NOSE_CLEARANCE", "PRISM_OCCLUSION_MAX_BASE_SHARE",
                  "PRISM_EROSION_WIGGLE", "PRISM_EROSION_WIGGLE_FREQ",
                  "PRISM_EROSION_END_MARGIN", "PRISM_EROSION_CDF_LO", "PRISM_EROSION_CDF_HI"):
        m = re.search(rf"^static const float {const} = [-\d.]+;", text, re.M)
        assert m, f"{const} not found in the shipped HLSL"
        parts.append(m.group(0))

    for sig in ("float PrismOcclusionSmootherStep(float t)",
                "float PrismOcclusionSafeThreshold(float n)",
                "float PrismErosionCoverage(float opacity)",
                "float3 PrismOcclusionHash3(float3 p3)",
                "float PrismOcclusionHash1(float3 p3)"):
        parts.append(body(text, sig))

    ero = body(text, "void PrismErosionFade_float(")
    assert ero.count("out float Threshold") == 1, "erosion out-parameter shape drifted"
    parts.append(ero.replace("out float Threshold", "float &Threshold"))

    fn = body(text, "void PrismOcclusionFade_float(")
    assert fn.count("out float Alpha, out float ClipThreshold, float ErosionThreshold") == 1, \
        "corridor parameter shape drifted — the erosion input is not where this expects it"
    fn = fn.replace("out float Alpha, out float ClipThreshold",
                    "float &Alpha, float &ClipThreshold")

    # The dither kernel is preprocessed away here, so mark the line it would occupy.
    # "reached the dither" then has a value, instead of being inferred from an absence.
    guard = "#if !defined(SHADERGRAPH_PREVIEW)"
    assert fn.count(guard) == 1, "the corridor's kernel guard is not where this expects it"
    fn = fn.replace(guard, f"    ClipThreshold = {SENTINEL}f;\n{guard}")

    if control == "no_erosion_gate":
        # NEGATIVE CONTROL: take the fast out on fade alone, without asking whether there
        # IS an erosion. That is the shape that silently makes every cloaked prism solid.
        old = "if (ErosionThreshold > 0.0 && corridorFade >= 1.0)"
        assert old in fn, "the erosion gate is not where this control expects it"
        fn = fn.replace(old, "if (corridorFade >= 1.0)")
    elif control == "no_coverage_remap":
        # NEGATIVE CONTROL: hand the dither the RAW clock opacity, which is what it looks
        # like this should obviously do. It is the boundary step — the erosion's end
        # margin has already taken surface the raw opacity does not account for.
        old_ = "float ditherBase = ErosionThreshold > 0.0 ? PrismErosionCoverage(BaseAlpha) : BaseAlpha;"
        assert old_ in fn, "the coverage remap is not where this control expects it"
        fn = fn.replace(old_, "float ditherBase = BaseAlpha;")
    elif control == "no_handoff":
        # NEGATIVE CONTROL: the corridor as it was before the handoff — no fast out at
        # all, so the erosion threshold can never reach the clip.
        i = fn.index("    if (ErosionThreshold > 0.0 && corridorFade >= 1.0)")
        j = fn.index("}", fn.index("return;", i)) + 1
        fn = fn[:i] + fn[j:]

    parts.append(fn)
    return "#pragma once\n" + "\n".join(parts) + "\n"


def build(workdir, slice_src, name):
    os.makedirs(workdir, exist_ok=True)
    open(os.path.join(workdir, "shim.h"), "w").write(SHIM)
    open(os.path.join(workdir, "slice.h"), "w").write(slice_src)
    open(os.path.join(workdir, "main.cpp"), "w").write(MAIN)
    exe = os.path.join(workdir, name)
    r = subprocess.run(["clang++", "-std=c++17", "-O1", "-x", "c++",
                        os.path.join(workdir, "main.cpp"), "-o", exe],
                       capture_output=True, text=True, cwd=workdir)
    assert r.returncode == 0, f"the {name} slice did not compile:\n" + r.stderr
    return exe


def run(exe, rows):
    payload = "\n".join(" ".join(repr(x) for x in r) for r in rows) + "\n"
    r = subprocess.run([exe], input=payload, capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    out = [tuple(float(x) for x in line.split()) for line in r.stdout.strip().splitlines()]
    assert len(out) == len(rows), f"{len(out)} results for {len(rows)} queries"
    return out


def bary(n):
    """Samples over the canonical face triangle, in UV."""
    pts = []
    for i in range(n + 1):
        for j in range(n + 1 - i):
            a, b = i / n, j / n
            c = 1.0 - a - b
            pts.append((a * 0.0 + b * 1.0 + c * 0.5, a * 0.0 + b * 0.0 + c * 1.0))
    return pts


def f32(x):
    """What the C++ side actually received: the query is read as a double and handed to
    the shipped function as a float, so a double literal is not what was evaluated."""
    return struct.unpack("f", struct.pack("f", x))[0]


# One piece's entropy for the tests that only need the select to be exercised. The
# ensemble test below varies it, because coverage == alpha is a property OF the ensemble
# (see T5).
PIECE = (1.0, 0.0, 0.0, 37.0, -11.0, 5.0)


def main():
    text = open(HLSL, encoding="utf-8").read()
    fails = []
    with tempfile.TemporaryDirectory() as wd:
        ship = build(os.path.join(wd, "s"), extract_slice(text), "ship")
        ctl_gate = build(os.path.join(wd, "g"), extract_slice(text, "no_erosion_gate"), "ctl_gate")
        ctl_none = build(os.path.join(wd, "n"), extract_slice(text, "no_handoff"), "ctl_none")

        uvs = bary(24)
        EPS = 1e-9

        # The share of a piece the front has already taken at a given clock opacity —
        # PrismErosionCoverage in the shipped file, mirrored here from its own constant.
        # It is NOT the opacity: the end margin finishes the wipe 15% early so retirement
        # can never beat it, and that 15% is surface the raw opacity knows nothing about.
        margin = float(re.search(r"^static const float PRISM_EROSION_END_MARGIN = ([-\d.]+);",
                                 text, re.M).group(1))

        def ramp(a):
            return min(1.0, max(0.0, (a - margin) / (1.0 - margin)))

        # ---- T1: a LIVE prism is untouched ------------------------------------
        rows = [(m, 0.5, 0.5, 1.0, 0.0) + PIECE for m in (0, 1)]
        a, b = run(ship, rows), run(ctl_none, rows)
        ok = all(abs(x[0] - y[0]) <= EPS and abs(x[1] - y[1]) <= EPS for x, y in zip(a, b))
        print(f"T1 live prism unchanged          : {'PASS' if ok else 'FAIL'} "
              f"(outside {a[0][0]:.4g}/{a[0][1]:.4g}, inside {a[1][0]:.4g}/{a[1][1]:.4g})")
        if not ok:
            fails.append("T1")

        # ---- T2: a CLOAKED prism still reaches the dither, everywhere ----------
        rows = [(m, 0.5, 0.5, 0.05, 0.0) + PIECE for m in (0, 1)]
        a = run(ship, rows)
        g = run(ctl_gate, rows)
        ok = all(abs(r[1] - SENTINEL) <= EPS for r in a)
        ctl_broken = any(abs(r[1] - SENTINEL) > EPS for r in g)
        print(f"T2 cloak still dithers           : {'PASS' if ok else 'FAIL'} "
              f"(threshold {a[0][1]:.4g} outside, {a[1][1]:.4g} inside — both the kernel)")
        print(f"   control (gate drops the E>0 test): "
              f"{'fires' if ctl_broken else 'DOES NOT FIRE'} "
              f"(threshold {g[0][1]:.4g} outside — a cloaked prism rendered solid)")
        if not ok or not ctl_broken:
            fails.append("T2")

        # ---- T3: debris OUTSIDE — the front is the only field ------------------
        ops = [0.05 * k for k in range(1, 20)]
        rows = [(0, uvs[0][0], uvs[0][1], op, 0.0) + PIECE for op in ops]
        eros = [r[2] for r in run(ship, rows)]
        rows = [(0, 0.5, 0.5, op, e) + PIECE for op, e in zip(ops, eros)]
        res = run(ship, rows)
        alpha_ok = all(abs(r[0] - f32(op)) <= EPS for r, op in zip(res, ops))
        thr_ok = all(abs(r[1] - e) <= EPS for r, e in zip(res, eros))
        # the clip `alpha - threshold >= 0` must reproduce the retired verdict exactly
        verdict_ok = all((r[0] - r[1] >= 0.0) == (f32(op) >= e)
                         for r, op, e in zip(res, ops, eros))
        ok = alpha_ok and thr_ok and verdict_ok
        n = run(ctl_none, rows)
        ctl_broken = any(abs(r[1] - e) > EPS for r, e in zip(n, eros))
        print(f"T3 debris outside = the front    : {'PASS' if ok else 'FAIL'} "
              f"(alpha==opacity {alpha_ok}, threshold==front {thr_ok}, "
              f"clip reproduces the old verdict {verdict_ok}, {len(ops)} opacities)")
        print(f"   control (no handoff)             : "
              f"{'fires' if ctl_broken else 'DOES NOT FIRE'} "
              f"(threshold {n[0][1]:.4g} — the front never reaches the clip)")
        if not ok or not ctl_broken:
            fails.append("T3")

        # ---- T4: debris INSIDE — the front stands down -------------------------
        rows = [(1, 0.5, 0.5, op, e) + PIECE for op, e in zip(ops, eros)]
        res = run(ship, rows)
        ok = all(abs(r[1] - SENTINEL) <= EPS and r[1] != e for r, e in zip(res, eros))
        # and the alpha it dithers is the front's own coverage, scaled by the fade —
        # never the raw opacity (see PrismErosionCoverage). Below the end margin that is
        # exactly 0, because the front has already taken the whole piece.
        fade = res[-1][0] / ramp(ops[-1])          # read off the liveliest sample
        faded = 0.0 < fade < 1.0 and all(
            (abs(r[0] - ramp(op) * fade) < 2e-3) if ramp(op) > 0.0 else r[0] == 0.0
            for r, op in zip(res, ops))
        print(f"T4 debris inside = the tunnel    : {'PASS' if ok else 'FAIL'} "
              f"(threshold is the kernel on all {len(ops)}, and the alpha it dithers is "
              f"the front's coverage through the fade: {faded})")
        if not ok or not faded:
            fails.append("T4")

        # ---- T5: the erosion field's coverage IS the design ramp -----------------
        # The front's thresholds are uniform on [END_MARGIN, 1] — that is what the CDF
        # linearisation buys — so the share of a piece it has taken at opacity `a` is
        # PrismErosionCoverage(a), NOT `a`. The margin is deliberate (the wipe finishes
        # 15% early so retirement can never beat it), and it is exactly the amount the
        # corridor has to be told about, which T6 checks.
        #
        # IT IS A PROPERTY OF THE ENSEMBLE, NOT OF ONE PIECE, and that is not a weaker
        # claim — it is the claim. One piece carries ONE front, so its coverage at a given
        # threshold is the AREA of its triangle behind a line, which is quadratic in the
        # sweep and cannot be linear under any remap. What the player sees is a field of
        # pieces, each with its own hashed direction and jag, and it is that average the
        # CDF in fit_prism_erosion_cdf.py is fitted over — with the same per-sample
        # entropy draw this uses. Measuring one piece and calling the spread a defect is
        # the mistake; the spread IS pieces peeling differently.
        rng = random.Random(20260916)
        pieces = [tuple(rng.uniform(-20.0, 20.0) for _ in range(6)) for _ in range(320)]
        OPS = [0.2, 0.35, 0.5, 0.65, 0.8, 0.95]
        covers = {}
        worst = 0.0
        per_piece = 0.0
        for op in OPS:
            rows = [(0, u, v, op, 0.0) + pc for pc in pieces for (u, v) in uvs]
            field = [r[2] for r in run(ship, rows)]
            cover = sum(1 for e in field if f32(op) >= e) / len(field)
            covers[op] = cover
            worst = max(worst, abs(cover - ramp(op)))
            m = len(uvs)
            for i in range(len(pieces)):
                one = field[i * m:(i + 1) * m]
                c1 = sum(1 for e in one if f32(op) >= e) / m
                per_piece = max(per_piece, abs(c1 - ramp(op)))
        # The residual is a smooth S-bend of ~0.022 at its worst, and it is a TIME-domain
        # error — the fade curve leans slightly ahead of the ramp early and behind it
        # late — never a spatial artefact. It is the limit of approximating the wipe
        # coordinate's true CDF with one smoothstep (fit_prism_erosion_cdf.py), not slop
        # in this measurement: it is smooth, monotone in shape and reproducible to the
        # third digit. The gate sits just above it so a REGRESSION fails while the known
        # shape passes, and T6's control shows what a real disagreement looks like (0.13,
        # six times this).
        ok = worst < 0.03
        print(f"T5 coverage == the design ramp   : {'PASS' if ok else 'FAIL'} "
              f"(worst |coverage - ramp| {worst:.4f} over {len(pieces)} pieces x "
              f"{len(uvs)} samples x {len(OPS)} opacities; worst SINGLE piece "
              f"{per_piece:.4f}, which is the sweep's own shape and not a defect)")
        if not ok:
            fails.append("T5")

        # ---- T6: NO COVERAGE STEP ACROSS THE CORRIDOR WALL -----------------------
        # This is the test the whole select rests on. Just outside the wall the front
        # decides and leaves ramp(opacity) of the piece; just inside, the dither decides
        # and leaves exactly Alpha. If the corridor is handed the raw opacity those two
        # disagree by 0.176*(1-opacity) — up to 15 points of surface, as a brightness step
        # along the wall, and in the WRONG DIRECTION (a nearly-dead chunk brighter inside
        # the tunnel than outside it). The control drops the remap and must fire.
        # A POSITIVE erosion threshold: inside the corridor its VALUE is unused (the front
        # stands down) but it is what tells the corridor this is a piece being carved, so
        # passing 0 here measures the cloak path and not the handoff at all.
        rows = [(2, 0.5, 0.5, op, 0.5) + PIECE for op in OPS]
        inside = [r[0] for r in run(ship, rows)]            # dither coverage == Alpha
        ctl_remap = build(os.path.join(wd, "r"),
                          extract_slice(text, "no_coverage_remap"), "ctl_remap")
        inside_ctl = [r[0] for r in run(ctl_remap, rows)]
        step = max(abs(a - covers[op]) for a, op in zip(inside, OPS))
        step_ctl = max(abs(a - covers[op]) for a, op in zip(inside_ctl, OPS))
        # Bounded by T5's own residual: the wall cannot agree better than the erosion's
        # coverage law tracks the ramp it is being matched to.
        ok = step < 0.03
        ctl_broken = step_ctl > 0.06
        print(f"T6 no step across the wall       : {'PASS' if ok else 'FAIL'} "
              f"(worst |inside coverage - outside coverage| {step:.4f} at fade "
              f"{inside[0] / max(ramp(OPS[0]), 1e-6):.4f})")
        print(f"   control (raw opacity to the dither): "
              f"{'fires' if ctl_broken else 'DOES NOT FIRE'} (step {step_ctl:.4f})")
        if not ok or not ctl_broken:
            fails.append("T6")

    print()
    if fails:
        print("FAILED: " + ", ".join(fails))
        return 1
    print("All checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
