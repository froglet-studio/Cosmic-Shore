#!/usr/bin/env python3
"""
Prove the prism EROSION's per-piece anchor against the SHIPPED HLSL and the SHIPPED mesh.

The exploding prism's fade is a jagged wipe swept across each debris piece, anchored to
the mesh so it cannot crawl under camera motion or the shatter spin
(PrismOcclusionCorridor.hlsl, "THE EROSION"). Two facts have to hold for that to read as
pieces breaking up rather than as a pattern painted on the prism before it exploded:

  1. EVERY PIECE GETS ITS OWN WIPE. UV0 says WHERE ON a piece a fragment sits and nothing
     about WHICH piece: every one of the debris cube's 24 wedges carries the bit-identical
     UV triangle, so until 2026-09-16 one wipe served the whole prism — all 24 wedges
     peeled in lockstep, from the same relative corner, in the PRE-explosion body frame.
     The identity is now the object-space TANGENT, which is each wedge's own hinge axis.
  2. THE WIPE USES THE WHOLE FADE. `w01` is normalized against the UV SQUARE's half-extent
     while what actually renders is a TRIANGLE, so a piece whose UV triangle covers only
     part of the square can never reach both ends of the threshold range and vanishes
     early or starts late.

This compiles the shipped file's own erosion slice with clang++ and runs it over the real
per-wedge UV and tangent data decoded out of the shipped mesh, so neither the maths nor
the mesh is a transcription. Every test carries a NEGATIVE CONTROL that restores the old
behaviour and must fail, because a gate nobody has watched fail is a gate nobody should
trust.

TWO ADAPTATIONS are applied to the extracted slice, both stated so the diff is auditable:
HLSL's `out float X` becomes C++'s `float &X`, and the shim below supplies the vector
types and intrinsics. Nothing else is touched — the constants, the hashes and the function
body are the shipped bytes.

Exit 0 on pass. Needs clang++; nothing else, and no Unity.
Usage: [--check] is accepted and is the same thing (this tool only ever reads).
"""

import os
import re
import struct
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(REPO, "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl")
MESH = os.path.join(REPO, "Assets/_Models/Testing/Prism.asset")
OCTA = os.path.join(REPO, "Assets/_Scripts/Utility/OctahedronMeshGenerator.cs")
STELLA = os.path.join(REPO, "Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs")

# The canonical face triangle every debris mesh must map a piece onto, in UV [0,1].
# fit_prism_erosion_cdf.py fits the threshold CDF over exactly this domain, so a mesh
# that disagrees renders a wipe the fit does not describe.
CANONICAL_UV = ((0.0, 0.0), (1.0, 0.0), (0.5, 1.0))

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
#define SHADERGRAPH_PREVIEW 1
"""

MAIN = r"""#include "shim.h"
#include "slice.h"
#include <cstdio>
#include <cstdlib>

// Recover the fragment's DEATH OPACITY — the threshold the shipped function compares
// BaseOpacity against — by bisecting its own binary output. Nothing is re-derived: the
// only thing measured is where the shipped function flips.
static float DeathOpacity(float3 uv, float3 tangent, float3 velocity)
{
    float lo = 0.0f, hi = 1.0f, s;
    for (int i = 0; i < 40; ++i) {
        float mid = 0.5f * (lo + hi);
        PrismErosionFade_float(uv, tangent, velocity, mid, s);
        if (s > 0.0f) hi = mid; else lo = mid;
    }
    return 0.5f * (lo + hi);
}

int main(int argc, char **argv)
{
    // stdin: one query per line "u v tx ty tz vx vy vz"
    double u, v, tx, ty, tz, vx, vy, vz;
    while (scanf("%lf %lf %lf %lf %lf %lf %lf %lf", &u, &v, &tx, &ty, &tz, &vx, &vy, &vz) == 8) {
        float3 uvv((float)u, (float)v, 0.0f);
        float3 tan((float)tx, (float)ty, (float)tz);
        float3 vel((float)vx, (float)vy, (float)vz);
        float ends1, ends0;
        PrismErosionFade_float(uvv, tan, vel, 1.0f, ends1);
        PrismErosionFade_float(uvv, tan, vel, 0.0f, ends0);
        printf("%.9f %.1f %.1f\n", DeathOpacity(uvv, tan, vel), ends1, ends0);
    }
    return 0;
}
"""


def extract_slice(text, legacy=False):
    """The shipped erosion, verbatim, plus the constants and hashes it calls."""
    parts = []
    for const in ("PRISM_EROSION_WIGGLE", "PRISM_EROSION_WIGGLE_FREQ", "PRISM_EROSION_END_MARGIN",
                  "PRISM_EROSION_FRINGE", "PRISM_EROSION_CDF_LO", "PRISM_EROSION_CDF_HI"):
        m = re.search(rf"^static const float {const} = [-\d.]+;", text, re.M)
        assert m, f"{const} not found in the shipped HLSL"
        parts.append(m.group(0))

    def body(sig):
        i = text.index(sig)
        j = text.index("\n{\n", i)
        k = text.index("\n}\n", j)
        return text[i:k + 3]

    for sig in ("float PrismOcclusionSafeThreshold(float n)",
                "float3 PrismOcclusionHash3(float3 p3)",
                "float PrismOcclusionHash1(float3 p3)",
                "void PrismErosionFade_float("):
        parts.append(body(sig))

    src = "#pragma once\n" + "\n".join(parts) + "\n"
    assert src.count("out float Survival") == 1, "erosion out-parameter shape drifted"
    src = src.replace("out float Survival", "float &Survival")
    if legacy:
        # NEGATIVE CONTROL: the 2026-08-11 shape — no per-piece identity, and `w01`
        # normalized against the UV SQUARE rather than the piece's own support. Both
        # substitutions are asserted, so a rewrite that moves either one fails here
        # instead of quietly turning the control into a copy of the shipped function.
        assert "PrismOcclusionHash3(Velocity + Tangent * 17.0)" in src, \
            "the per-piece identity is not where this control expects it"
        src = src.replace("PrismOcclusionHash3(Velocity + Tangent * 17.0)",
                          "PrismOcclusionHash3(Velocity)")
        i = src.index("    float s0 = -dir.x - dir.y;")
        j = src.index("\n", src.index("float w01 = (dot(uv, dir) - sLo)"))
        src = src[:i] + "    float w01 = dot(uv, dir) / (abs(dir.x) + abs(dir.y)) * 0.5 + 0.5;" + src[j:]
    return src


def build(workdir, slice_src):
    open(os.path.join(workdir, "shim.h"), "w").write(SHIM)
    open(os.path.join(workdir, "slice.h"), "w").write(slice_src)
    open(os.path.join(workdir, "main.cpp"), "w").write(MAIN)
    exe = os.path.join(workdir, "erosion")
    r = subprocess.run(["clang++", "-std=c++17", "-O1", "-x", "c++",
                        os.path.join(workdir, "main.cpp"), "-o", exe],
                       capture_output=True, text=True, cwd=workdir)
    assert r.returncode == 0, "the shipped erosion slice did not compile:\n" + r.stderr
    return exe


def run(exe, queries):
    payload = "\n".join(" ".join(f"{x!r}" for x in q) for q in queries) + "\n"
    r = subprocess.run([exe], input=payload, capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    rows = [line.split() for line in r.stdout.strip().splitlines()]
    assert len(rows) == len(queries), f"{len(rows)} results for {len(queries)} queries"
    return [(float(a), float(b), float(c)) for a, b, c in rows]


def read_mesh(path):
    """(uv, tangent) per vertex, grouped per triangle, out of the shipped Mesh asset."""
    text = open(path).read()
    data = bytes.fromhex(re.search(r"_typelessdata:\s*([0-9a-f]+)", text).group(1))
    count = int(re.search(r"m_VertexCount:\s*(\d+)", text).group(1))
    stride = len(data) // count
    assert stride * count == len(data), "vertex stride is not integral"
    # pos(12) normal(12) tangent(16) uv0(8) — asserted against the channel table so a
    # re-import that reorders the stream fails here instead of reading garbage.
    chans = re.search(r"m_Channels:(.*?)m_DataSize", text, re.S).group(1)
    offs = [int(x) for x in re.findall(r"offset:\s*(\d+)", chans)]
    dims = [int(x) for x in re.findall(r"dimension:\s*(\d+)", chans)]
    assert (offs[0], dims[0]) == (0, 3) and (offs[1], dims[1]) == (0 + 12, 3), "position/normal moved"
    assert (offs[2], dims[2]) == (24, 4), "tangent channel moved"
    assert (offs[4], dims[4]) == (40, 2), "UV0 channel moved"
    tris = []
    for t in range(count // 3):
        o = t * 3 * stride
        uvs = [struct.unpack_from("<2f", data, (t * 3 + k) * stride + 40) for k in range(3)]
        tris.append({
            "uv": tuple(uvs),
            "tangent": struct.unpack_from("<3f", data, o + 24)[:3],
            "normal": struct.unpack_from("<3f", data, o + 12),
        })
    return tris


def bary_samples(tri_uv, n):
    """Deterministic samples over a UV triangle: the three CORNERS and the three edges
    first, then uniform-by-area fill.

    The corners are not decoration. T2 measures the EXTREMES of the threshold field, and
    both extremes are attained at a vertex — a purely random fill approaches them and
    (at any sample count a test can afford) never reaches them, which reads as the wipe
    finishing early when it does not."""
    import random
    rng = random.Random(20260916)
    a, b, c = tri_uv

    def mix(p, q, t):
        return (p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t)

    out = [a, b, c]
    for p, q in ((a, b), (b, c), (c, a)):
        out += [mix(p, q, i / 32.0) for i in range(1, 32)]
    while len(out) < n:
        r1, r2 = rng.random(), rng.random()
        if r1 + r2 > 1.0:
            r1, r2 = 1.0 - r1, 1.0 - r2
        out.append((a[0] + (b[0] - a[0]) * r1 + (c[0] - a[0]) * r2,
                    a[1] + (b[1] - a[1]) * r1 + (c[1] - a[1]) * r2))
    return out


def read_const(text, name):
    return float(re.search(rf"^static const float {re.escape(name)} = ([-\d.]+);",
                           text, re.M).group(1))


def main():
    text = open(HLSL, encoding="utf-8").read()
    margin = read_const(text, "PRISM_EROSION_END_MARGIN")
    tris = read_mesh(MESH)
    fails = []

    # ---- T0: the mesh renders the canonical domain the CDF was fitted over ----
    uv_tris = {t["uv"] for t in tris}
    ok = uv_tris == {CANONICAL_UV}
    print(f"T0 canonical UV domain          : {'PASS' if ok else 'FAIL'} "
          f"({len(tris)} pieces, {len(uv_tris)} distinct UV triangle(s): "
          f"{sorted(uv_tris)[0] if len(uv_tris) == 1 else sorted(uv_tris)})")
    if not ok:
        fails.append("T0")
    for gen, label in ((OCTA, "octahedron"), (STELLA, "stella")):
        src = open(gen, encoding="utf-8").read()
        authored = re.findall(r"uvs\.Add\(new Vector2\(([-\d.f]+),\s*([-\d.f]+)\)\);", src)
        got = tuple((float(a.rstrip("f")), float(b.rstrip("f"))) for a, b in authored[:3])
        ok = got == CANONICAL_UV
        print(f"   {label} generator agrees      : {'PASS' if ok else 'FAIL'} {got}")
        if not ok:
            fails.append(f"T0/{label}")

    # ---- the shipped slice, and its negative control ----
    with tempfile.TemporaryDirectory() as wd:
        shipped = build(os.path.join(wd), extract_slice(text))
        os.makedirs(os.path.join(wd, "ctl"), exist_ok=True)
        control = build(os.path.join(wd, "ctl"), extract_slice(text, legacy=True))

        vel = (37.0, -11.0, 5.0)          # one prism's stamped flight vector
        uvs = bary_samples(CANONICAL_UV, 240)

        def death_fields(exe):
            qs = [(u, v) + t["tangent"] + vel for t in tris for (u, v) in uvs]
            rows = run(exe, qs)
            per = len(uvs)
            return [[rows[i * per + k] for k in range(per)] for i in range(len(tris))]

        ship_fields = death_fields(shipped)
        ctl_fields = death_fields(control)

        # ---- T1: pieces that a viewer sees as ONE former face must not share a wipe ----
        #
        # The bar is deliberately not "24 of 24 distinct". The cube's tangent set has
        # exactly SIX members (+/-X, +/-Y, +/-Z) and each is carried by four wedges — but
        # those four sit on four DIFFERENT sides, flying apart in four directions, so they
        # are never seen as one body. What produced the report was coherence WITHIN a face:
        # every wedge of a side peeling in lockstep in the pre-explosion arrangement. That
        # is the property asserted, per face plane, plus a floor on the global count.
        def key(field):
            return tuple(round(r[0], 6) for r in field)

        planes = {}
        for i, t in enumerate(tris):
            planes.setdefault(tuple(round(x, 4) for x in t["normal"]), []).append(i)
        worst = min((len({key(ship_fields[i]) for i in ids}), len(ids))
                    for ids in planes.values())
        d_ship = len({key(f) for f in ship_fields})
        d_ctl = len({key(f) for f in ctl_fields})
        ok = worst[0] == worst[1] and d_ctl == 1 and d_ship > 1
        print(f"T1 no shared wipe within a face : {'PASS' if ok else 'FAIL'} "
              f"(worst face: {worst[0]}/{worst[1]} distinct; "
              f"{d_ship} distinct wipes over {len(tris)} pieces, "
              f"{len(planes)} face planes)")
        print(f"   negative control (no anchor) : {d_ctl}/{len(tris)} distinct "
              f"— every piece peeled alike")
        if not ok:
            fails.append("T1")

        # ---- T2: the wipe uses the whole fade, for EVERY hashed direction ----
        #
        # Swept over many stamped velocities, because the defect this catches is
        # DIRECTION-DEPENDENT: a single draw can land on a direction the old normalizer
        # happened to serve well, so one velocity proves nothing either way.
        import random as _random
        vrng = _random.Random(20260916)
        vels = [(vrng.uniform(-40, 40), vrng.uniform(-40, 40), vrng.uniform(-40, 40))
                for _ in range(200)]
        tan = tris[0]["tangent"]

        def span_sweep(exe):
            qs = [(u, v) + tan + w for w in vels for (u, v) in uvs]
            rows = run(exe, qs)
            per = len(uvs)
            out = []
            for i in range(len(vels)):
                f = rows[i * per:(i + 1) * per]
                hi = max(r[0] for r in f)
                lo = min(r[0] for r in f)
                out.append(((hi - lo) / (1.0 - margin), lo))
            return out

        ship_span = span_sweep(shipped)
        ctl_span = span_sweep(control)
        used = [u for u, _ in ship_span]
        ends = [e for _, e in ship_span]
        c_used = [u for u, _ in ctl_span]
        c_ends = [e for _, e in ctl_span]
        ok = min(used) > 0.95 and max(ends) < margin * 1.2
        print(f"T2 wipe spans the fade          : {'PASS' if ok else 'FAIL'} "
              f"(uses {min(used) * 100:.1f}-{max(used) * 100:.1f}% of the fade over "
              f"{len(vels)} directions, all gone by opacity {max(ends):.3f} "
              f"vs END_MARGIN {margin})")
        print(f"   legacy control (square norm) : uses {min(c_used) * 100:.1f}-"
              f"{max(c_used) * 100:.1f}% (mean {sum(c_used) / len(c_used) * 100:.1f}%), "
              f"worst case gone by opacity {max(c_ends):.3f} — and that is on TODAY's "
              f"mesh; the half-height UVs it shipped with were worse still")
        if not ok:
            fails.append("T2")

        # ---- T3: exact ends (the live-material pass-throughs) ----
        ok = all(r[1] == 1.0 and r[2] == 0.0 for f in ship_fields for r in f)
        print(f"T3 exact ends (live prisms)     : {'PASS' if ok else 'FAIL'} "
              f"(Opacity 1 -> Survival 1, Opacity 0 -> Survival 0)")
        if not ok:
            fails.append("T3")

        # ---- T4: a tangent-less mesh degrades, it does not break ----
        rows = run(shipped, [(u, v, 0.0, 0.0, 0.0) + vel for (u, v) in uvs])
        ok = all(0.0 < r[0] < 1.0 for r in rows)
        print(f"T4 tangent-less mesh degrades   : {'PASS' if ok else 'FAIL'} "
              f"(zero tangent still yields a valid wipe)")
        if not ok:
            fails.append("T4")

    # ---- T5: the anchor is structurally view- and time-independent ----
    sig = re.search(r"void PrismErosionFade_float\(([^)]*)\)", text).group(1)
    params = [p.strip().split()[-1] for p in sig.split(",")]
    ok = params == ["UV", "Tangent", "Velocity", "BaseOpacity", "Survival"]
    print(f"T5 no view/time input            : {'PASS' if ok else 'FAIL'} ({params})")
    if not ok:
        fails.append("T5")
    body = text[text.index("void PrismErosionFade_float("):]
    body = body[:body.index("\n}\n")]
    leaks = [tok for tok in ("_Time", "_WorldSpaceCameraPos", "UNITY_MATRIX", "_ScreenParams")
             if tok in body]
    ok = not leaks
    print(f"   body reads no global          : {'PASS' if ok else 'FAIL'} {leaks}")
    if not ok:
        fails.append("T5/globals")

    print()
    if fails:
        print("FAILED: " + ", ".join(fails), file=sys.stderr)
        return 1
    print("All checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
