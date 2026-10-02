#!/usr/bin/env python3
"""Compile the SHIPPED SwarmMemberInstanced.hlsl with clang and prove the GPU draws a swarm member exactly
where its PROXY would stand (Docs/SWARM_FAUNA.md §14).

Why it matters: a living member is drawn by the swarm's shader; when it dies, its proxy's own visuals take
over (the skeleton prism, the released heart). If the two poses disagree the death POPS - a body prism
jumping sideways is exactly the continuity-of-existence break the law forbids. So the reference here is the
proxy's transform chain, written independently in Python from what the C# glue does:

    root  at lerp(PrevPos, CurPos, a), rotated Quaternion.LookRotation(face, up)
          (up = the body's BY, or BZ when |face . BY| > 0.98 - SwarmFauna.Face)
    body  prism child at localPosition (0, 0, PrismZ), localScale Scale        (SwarmTadpoleFauna.SetShape)
    heart crystal root at the member root, world scale = HeartWorldScale[element], its models under it

  T1  body vertices, random members / alphas / facings - shader == proxy chain (max error)
  T2  heart vertices in their element's draw == proxy chain; a heart in the OTHER element's draw is collapsed
  T3  molt: the element switches at the midpoint and the heart shrinks to ~0 there (no pop on the switch)
  T4  bloom: a newborn is ~0 at its birth tick and full after BirthBloomSeconds
  T5  a dead slot is collapsed (not drawn)
  T6  NEGATIVE CONTROL: the same check against a shader whose PrismZ sign is flipped must FAIL

    python3 Tools/Shaders/verify_swarm_member_pose.py
"""
import ctypes
import math
import os
import random
import re
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/SwarmMemberInstanced.hlsl")

SHIM = r"""
#include <cmath>
#include <cstdint>
using std::sqrt; using std::abs; using std::pow;
typedef unsigned int uint;
using float3 = float __attribute__((ext_vector_type(3)));
using float4 = float __attribute__((ext_vector_type(4)));
template<class A, class B, class C> static inline float3 mk3(A a, B b, C c) { return float3{(float)a, (float)b, (float)c}; }
template<class A, class B, class C, class D> static inline float4 mk4(A a, B b, C c, D d) { return float4{(float)a, (float)b, (float)c, (float)d}; }
static inline float4 mk4(float3 v, float w) { return float4{v.x, v.y, v.z, w}; }
static inline float max(float a, float b) { return a > b ? a : b; }
static inline float min(float a, float b) { return a < b ? a : b; }
static inline float3 max(float3 a, float3 b) { return float3{max(a.x, b.x), max(a.y, b.y), max(a.z, b.z)}; }
static inline float saturate(float v) { return min(1.0f, max(0.0f, v)); }
static inline float lerp(float a, float b, float t) { return a + (b - a) * t; }
static inline float3 lerp(float3 a, float3 b, float t) { return a + (b - a) * t; }
static inline float dot(float3 a, float3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
static inline float length(float3 a) { return sqrt(dot(a, a)); }
static inline float3 normalize(float3 a) { return a / length(a); }
static inline float3 cross(float3 a, float3 b) { return float3{a.y*b.z - a.z*b.y, a.z*b.x - a.x*b.z, a.x*b.y - a.y*b.x}; }
struct float4x4 { float m[4][4]; };
struct float3x3 { float m[3][3]; };
static inline float3x3 to3x3(const float4x4& a) { float3x3 r; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) r.m[i][j] = a.m[i][j]; return r; }
static inline float4 mul(const float4x4& a, float4 v) { float4 r; for (int i = 0; i < 4; i++) r[i] = a.m[i][0]*v.x + a.m[i][1]*v.y + a.m[i][2]*v.z + a.m[i][3]*v.w; return r; }
static inline float3 mul(const float3x3& a, float3 v) { float3 r; for (int i = 0; i < 3; i++) r[i] = a.m[i][0]*v.x + a.m[i][1]*v.y + a.m[i][2]*v.z; return r; }
template<class T> struct StructuredBuffer { T* p; T operator[](uint i) const { return p[i]; } };
static float3 _WorldSpaceCameraPos;
"""

ABI = r"""
extern "C" {
static SwarmInstance g_inst[8];
static uint g_heart[8];
void set_inst(int i, const float* f, uint flags)
{
    SwarmInstance s;
    s.PrevPos = mk3(f[0], f[1], f[2]); s.BirthTick = f[3];
    s.CurPos = mk3(f[4], f[5], f[6]); s.Flags = flags;
    s.PrevFace = mk3(f[8], f[9], f[10]); s.PrevMolt = f[11];
    s.CurFace = mk3(f[12], f[13], f[14]); s.CurMolt = f[15];
    s.Scale = mk3(f[16], f[17], f[18]); s.PrismZ = f[19];
    g_inst[i] = s;
    _SwarmInstances.p = g_inst; _SwarmHeartIdx.p = g_heart;
}
void set_heart(int k, uint slot) { g_heart[k] = slot; }
void set_frame(float part, float base, float elem, const float* local16, float alpha, float clock, float bloom,
               const float* up, const float* upAlt, const float* heartScale)
{
    _SwarmPart = part; _SwarmBase = base; _SwarmHeartElement = elem;
    for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) _SwarmMeshLocal.m[i][j] = local16[i * 4 + j];
    _SwarmAlpha = alpha; _SwarmClock = clock; _SwarmBloomTicks = bloom;
    _SwarmUp = mk4(up[0], up[1], up[2], 0); _SwarmUpAlt = mk4(upAlt[0], upAlt[1], upAlt[2], 0);
    _SwarmHeartScale = mk4(heartScale[0], heartScale[1], heartScale[2], heartScale[3]);
}
void set_spread(const float* plain, const float* danger, const float* shield, const float* cam)
{
    _SwarmSpreadPlain = mk4(plain[0], plain[1], plain[2], plain[3]);
    _SwarmSpreadDanger = mk4(danger[0], danger[1], danger[2], danger[3]);
    _SwarmSpreadShield = mk4(shield[0], shield[1], shield[2], shield[3]);
    _WorldSpaceCameraPos = mk3(cam[0], cam[1], cam[2]);
}
int pose_nt(uint id, float x, float y, float z, float nx, float ny, float nz, float tx, float ty, float tz, float* out)
{
    SwarmMemberVertex v = SwarmMemberPose(id, mk3(x, y, z), mk3(nx, ny, nz), mk3(tx, ty, tz));
    out[0] = v.positionWS.x; out[1] = v.positionWS.y; out[2] = v.positionWS.z;
    return v.visible ? 1 : 0;
}
int pose(uint id, float x, float y, float z, float* out)
{
    SwarmMemberVertex v = SwarmMemberPose(id, mk3(x, y, z), mk3(0, 0, 1), mk3(0, 0, 0));
    out[0] = v.positionWS.x; out[1] = v.positionWS.y; out[2] = v.positionWS.z;
    return v.visible ? 1 : 0;
}
}
"""


def translate(text):
    text = re.sub(r"#ifndef SWARM_MEMBER_INSTANCED_INCLUDED|#define SWARM_MEMBER_INSTANCED_INCLUDED|#endif", "", text)
    text = re.sub(r"\(float3x3\)(\w+)", r"to3x3(\1)", text)
    text = re.sub(r"\bout float3\b", "float3&", text)
    text = re.sub(r"\bout float\b", "float&", text)
    text = re.sub(r"\bout int\b", "int&", text)
    text = re.sub(r"\bfloat3\s*\(", "mk3(", text)
    text = re.sub(r"\bfloat4\s*\(", "mk4(", text)
    text = re.sub(r'(?<![\w.])(\d+\.\d*(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?|\d+[eE][+-]?\d+)(?![fF\w.])', r"\1f", text)
    return text


def build(text, tag):
    d = tempfile.mkdtemp(prefix="swarm_pose_")
    src = os.path.join(d, f"pose_{tag}.cpp")
    with open(src, "w") as fh:
        fh.write(SHIM + translate(text) + ABI)
    lib = os.path.join(d, f"pose_{tag}.so")
    subprocess.run(["clang++", "-std=c++17", "-O2", "-shared", "-fPIC", "-Wall", "-Werror", "-Wno-unused-function",
                    "-Wno-unused-variable", "-o", lib, src], check=True)
    L = ctypes.CDLL(lib)
    F = ctypes.POINTER(ctypes.c_float)
    L.set_inst.argtypes = [ctypes.c_int, F, ctypes.c_uint]
    L.set_heart.argtypes = [ctypes.c_int, ctypes.c_uint]
    L.set_frame.argtypes = [ctypes.c_float, ctypes.c_float, ctypes.c_float, F, ctypes.c_float, ctypes.c_float,
                            ctypes.c_float, F, F, F]
    L.pose.argtypes = [ctypes.c_uint, ctypes.c_float, ctypes.c_float, ctypes.c_float, F]
    L.pose.restype = ctypes.c_int
    L.set_spread.argtypes = [F, F, F, F]
    L.pose_nt.argtypes = [ctypes.c_uint] + [ctypes.c_float] * 9 + [F]
    L.pose_nt.restype = ctypes.c_int
    return L


def farr(xs):
    return (ctypes.c_float * len(xs))(*xs)


# ── the PROXY's chain, independently ───────────────────────────────────────────────────────────────
def sub(a, b): return [a[i] - b[i] for i in range(3)]
def add(a, b): return [a[i] + b[i] for i in range(3)]
def mulv(a, s): return [x * s for x in a]
def dot(a, b): return sum(a[i] * b[i] for i in range(3))
def norm(a): l = math.sqrt(dot(a, a)); return [x / l for x in a]
def cross(a, b): return [a[1]*b[2] - a[2]*b[1], a[2]*b[0] - a[0]*b[2], a[0]*b[1] - a[1]*b[0]]
def lerp(a, b, t): return [a[i] + (b[i] - a[i]) * t for i in range(3)]


def look_rotation(fw, up, up_alt):
    """SwarmFauna.Face + Unity's Quaternion.LookRotation as an orthonormal basis (right, up', forward)."""
    z = norm(fw)
    if abs(dot(z, norm(up))) > 0.98:
        up = up_alt
    x = norm(cross(up, z))
    y = cross(z, x)
    return x, y, z


def proxy_body(inst, a, up, up_alt, v):
    p = lerp(inst["prev"], inst["cur"], a)
    x, y, z = look_rotation(lerp(inst["pface"], inst["cface"], a), up, up_alt)
    sx, sy, sz = inst["scale"]
    lp = [v[0] * sx, v[1] * sy, v[2] * sz + inst["pz"]]
    return add(p, add(add(mulv(x, lp[0]), mulv(y, lp[1])), mulv(z, lp[2])))


def proxy_heart(inst, a, up, up_alt, v, hs):
    p = lerp(inst["prev"], inst["cur"], a)
    x, y, z = look_rotation(lerp(inst["pface"], inst["cface"], a), up, up_alt)
    lp = mulv(v, hs)
    return add(p, add(add(mulv(x, lp[0]), mulv(y, lp[1])), mulv(z, lp[2])))


def rand_unit(rng):
    while True:
        v = [rng.uniform(-1, 1) for _ in range(3)]
        l = math.sqrt(dot(v, v))
        if 0.2 < l <= 1: return [x / l for x in v]


def pack(alive, tier, frm, to):
    return (1 if alive else 0) | ((tier & 3) << 1) | ((frm & 3) << 3) | ((to & 3) << 5)


IDENT = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]


def set_member(L, i, inst, flags):
    f = inst["prev"] + [inst["birth"]] + inst["cur"] + [0] + inst["pface"] + [inst["pm"]] + inst["cface"] + [inst["cm"]] \
        + inst["scale"] + [inst["pz"]]
    L.set_inst(i, farr(f), flags)


def member(rng):
    cur = [rng.uniform(-900, 900) for _ in range(3)]
    face = rand_unit(rng)
    pface = norm(add(face, mulv(rand_unit(rng), 0.3)))
    return dict(prev=add(cur, [rng.uniform(-3, 3) for _ in range(3)]), cur=cur, pface=pface, cface=face,
                scale=[rng.uniform(0.5, 3), rng.uniform(0.3, 2), rng.uniform(1, 10)], pz=-rng.uniform(2, 9),
                birth=-1000.0, pm=0.0, cm=0.0)


def check(L, n=4000, seed=1):
    rng = random.Random(seed)
    worst_body = worst_heart = 0.0
    leaks = 0
    hs = [2.298, 1.737, 2.298, 1.737]
    for _ in range(n):
        m = member(rng)
        up, up_alt = rand_unit(rng), rand_unit(rng)
        if rng.random() < 0.1: up = list(m["cface"])        # force the |face . up| > 0.98 fallback
        a = rng.random()
        tier, elem = rng.randrange(3), rng.randrange(4)
        set_member(L, 0, m, pack(True, tier, elem, elem))
        L.set_heart(0, 0)
        v = [rng.uniform(-0.5, 0.5) for _ in range(3)]
        out = (ctypes.c_float * 3)()
        L.set_frame(0, 0, 0, farr(IDENT), a, 0, 8, farr(up), farr(up_alt), farr(hs))
        if not L.pose(0, *v, out): leaks += 1
        ref = proxy_body(m, a, up, up_alt, v)
        worst_body = max(worst_body, math.dist(ref, list(out)) / max(1.0, math.dist(ref, lerp(m["prev"], m["cur"], a))))
        # heart in its own element's draw
        L.set_frame(1, 0, elem, farr(IDENT), a, 0, 8, farr(up), farr(up_alt), farr(hs))
        vis = L.pose(0, *v, out)
        ref = proxy_heart(m, a, up, up_alt, v, hs[elem])
        if not vis: leaks += 1
        worst_heart = max(worst_heart, math.dist(ref, list(out)))
        # ... and collapsed in another element's draw
        L.set_frame(1, 0, (elem + 1) % 4, farr(IDENT), a, 0, 8, farr(up), farr(up_alt), farr(hs))
        if L.pose(0, *v, out): leaks += 1
    return worst_body, worst_heart, leaks


SPREAD = {0: [0.1, 0.1, 0.1, 100000.0], 1: [0.2, 0.2, 0.2, 100000.0], 2: [0.09, 0.29, 0.8, 100000.0]}


def ref_spread(v, n, t, scale, sq, sp):
    """Independent transcription of the three subgraphs (see the HLSL's SwarmPrismSpread comment)."""
    far = [sp[0] * 50, sp[1] * 35, sp[2] * 20]
    mx = max(sp[3], 1e-6)
    eff = far if sq > mx else [-7 + (far[i] + 7) * (sq / mx) for i in range(3)]
    eff = [max(eff[i], sp[i]) for i in range(3)]
    sc = [max(x, 1e-4) for x in scale]
    return [v[i] + eff[i] / sc[i] * n[i] + (eff[i] - sp[i]) / sc[i] * t[i] * 0.5 for i in range(3)]


def spread_check(L, n=3000, seed=11):
    rng = random.Random(seed)
    L.set_spread(farr(SPREAD[0]), farr(SPREAD[1]), farr(SPREAD[2]), farr([0, 0, 0]))
    hs = [2.298, 1.737, 2.298, 1.737]
    worst, opened = 0.0, 0.0
    out = (ctypes.c_float * 3)()
    for k in range(n):
        m = member(rng)
        up, up_alt = rand_unit(rng), rand_unit(rng)
        a = rng.random()
        tier = rng.randrange(3)
        set_member(L, 0, m, pack(True, tier, 1, 1))
        # the camera at a near, mid or far distance from the body
        p = lerp(m["prev"], m["cur"], a)
        x, y, z = look_rotation(lerp(m["pface"], m["cface"], a), up, up_alt)
        centre = add(p, mulv(z, m["pz"]))
        dist = [30.0, 200.0, 600.0][k % 3]
        cam = add(centre, mulv(rand_unit(rng), dist))
        L.set_spread(farr(SPREAD[0]), farr(SPREAD[1]), farr(SPREAD[2]), farr(cam))
        L.set_frame(0, 0, 0, farr(IDENT), a, 0, 8, farr(up), farr(up_alt), farr(hs))
        v = [rng.uniform(-0.5, 0.5) for _ in range(3)]
        nrm, tan = rand_unit(rng), rand_unit(rng)
        L.pose_nt(0, *v, *nrm, *tan, out)
        sq = dot(sub(centre, cam), sub(centre, cam))
        vs = ref_spread(v, nrm, tan, m["scale"], sq, SPREAD[tier])
        ref = proxy_body(m, a, up, up_alt, vs)
        worst = max(worst, math.dist(ref, list(out)))
        if dist >= 600.0:
            opened = max(opened, math.dist(ref, proxy_body(m, a, up, up_alt, v)))
    return worst, opened, True


def main():
    text = open(HLSL).read()
    L = build(text, "shipped")
    ok = True
    wb, wh, leaks = check(L)
    t1 = wb < 1e-4 and wh < 1e-3 and leaks == 0
    print(f"T1/T2 body worst rel err {wb:.2e}, heart worst {wh:.2e} world units, visibility mistakes {leaks}: "
          f"{'OK' if t1 else 'FAIL'}")
    ok &= t1

    # T3 molt: from Mass (1) to Time (3), sweep the progress
    hs = [2.298, 1.737, 2.298, 1.737]
    m = member(random.Random(5)); out = (ctypes.c_float * 3)()
    up, alt = [0, 1, 0], [0, 0, 1]
    shown, sizes = [], []
    for k in range(0, 21):
        mt = k / 20.0
        m["pm"] = m["cm"] = mt if mt > 0 else 0.0
        set_member(L, 0, m, pack(True, 0, 1, 3))
        L.set_heart(0, 0)
        vis = []
        for e in range(4):
            L.set_frame(1, 0, e, farr(IDENT), 0.5, 0, 8, farr(up), farr(alt), farr(hs))
            if L.pose(0, 0.5, 0, 0, out):
                vis.append(e)
                L.pose(0, 0, 0, 0, out); c = list(out)
                L.pose(0, 0.5, 0, 0, out)
                sizes.append((mt, math.dist(c, list(out)) / (0.5 * hs[e])))
        shown.append((mt, vis))
    switch_ok = all((v == [1] if mt < 0.5 else v == [3]) for mt, v in shown)
    mid = [s for mt, s in sizes if abs(mt - 0.5) < 0.051]
    t3 = switch_ok and max(mid) < 0.11 and abs(sizes[-1][1] - 1.0) < 1e-3
    print(f"T3 molt: Mass until the midpoint then Time {switch_ok}, size at the midpoint {max(mid):.3f}, "
          f"after {sizes[-1][1]:.3f}: {'OK' if t3 else 'FAIL'}")
    ok &= t3

    # T4 bloom
    m = member(random.Random(6)); m["birth"] = 40.0
    set_member(L, 0, m, pack(True, 0, 1, 1))
    def body_extent(clock):
        L.set_frame(0, 0, 0, farr(IDENT), 0.5, clock, 8, farr(up), farr(alt), farr(hs))
        L.pose(0, 0, 0, 0.5, out); a = list(out); L.pose(0, 0, 0, -0.5, out)
        return math.dist(a, list(out)) / m["scale"][2]
    t4 = body_extent(40.0) < 0.002 and abs(body_extent(48.0) - 1.0) < 1e-4 and abs(body_extent(100.0) - 1.0) < 1e-4 \
        and 0.4 < body_extent(44.0) < 0.6
    print(f"T4 bloom: at birth {body_extent(40.0):.4f}, half-way {body_extent(44.0):.3f}, grown {body_extent(48.0):.4f}: "
          f"{'OK' if t4 else 'FAIL'}")
    ok &= t4

    # T5 dead slot
    set_member(L, 0, m, pack(False, 0, 1, 1))
    L.set_frame(0, 0, 0, farr(IDENT), 0.5, 100, 8, farr(up), farr(alt), farr(hs))
    t5 = L.pose(0, 0, 0, 0, out) == 0
    print(f"T5 a dead slot is not drawn: {'OK' if t5 else 'FAIL'}")
    ok &= t5

    # T6 negative control
    broken = text.replace("lp.z * s.Scale.z + s.PrismZ", "lp.z * s.Scale.z - s.PrismZ")
    assert broken != text, "negative control did not apply"
    wb2, _, _ = check(build(broken, "broken"), n=400)
    t6 = wb2 > 1e-2
    print(f"T6 negative control (PrismZ sign flipped): body worst rel err {wb2:.2e} - {'fires' if t6 else 'DID NOT FIRE'}")
    ok &= t6

    # T7 the prism SPREAD (BlockGraph's DistanceSpreadAndColors -> SpreadSubGraph -> TangentSlider), against an
    # independent transcription of those three subgraphs, at near / mid / far camera distances and every tier
    worst, opened, ok7 = spread_check(L)
    t7 = worst < 1e-3 and opened > 1.0
    print(f"T7 prism spread: worst error {worst:.2e} world units, far-camera face separation {opened:.2f} u: "
          f"{'OK' if t7 else 'FAIL'}")
    ok &= t7
    # T8 negative control: a shader that ignores the spread (the round-7 shipped behaviour) must FAIL T7
    closed = text.replace("positionOS = SwarmPrismSpread(", "float3 _unused = SwarmPrismSpread(")
    assert closed != text, "negative control did not apply"
    w8, _, _ = spread_check(build(closed, "closed"))
    t8 = w8 > 0.1
    print(f"T8 negative control (spread ignored): worst error {w8:.2e} - {'fires' if t8 else 'DID NOT FIRE'}")
    ok &= t8

    print("OK" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
