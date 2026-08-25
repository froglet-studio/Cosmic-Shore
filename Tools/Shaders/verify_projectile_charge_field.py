#!/usr/bin/env python3
"""Prove the SHIPPED ProjectileChargeField.hlsl draws ONE STROKE per round, and that a VOLLEY'S
TWO ROUNDS are not the same stroke — by compiling and running it, and by running the version it
replaced beside it.

The 2026-08-25 pass answers a playtest note: *"tone it down so each projectile reads less like a
sphere, but together they build a spherical effect over time — a single arc on each projectile,
and after many projectiles those arcs stochastically fill in the circle as an after-image"* — and
its follow-up: *"the gun shoots a projectile out of two guns, so the randomness is spoiled by the
simultaneity of two projectiles acting in phase with each other."*

Those are claims about a DISTRIBUTION over rounds, which is exactly what static reading cannot
settle. Four things have to hold at once, and several of them pull against each other:

  1. ONE ROUND, AT ONE INSTANT, IS NOT A SPHERE. Its lit set must be a small fraction of the
     visible hemisphere, and it must be a CURVE — lit fragments lying close to one plane through
     the centre, i.e. on one great circle — not a blob and not a scatter.
  2. THE VOLLEY IS. The union of what a burst lights must climb toward covering the sphere.
  3. A VOLLEY'S TWO MUZZLES MUST NOT BE IN PHASE. Both guns fire in the same tick with the same
     growth factor, so the pair share a world radius EXACTLY — and radius used to be the shell's
     entire per-round signal, which made them clones flying side by side. The fix reads the two
     LATERAL world coordinates of the round's own frame (invariant along a flight, 6.4 apart
     across the ship), and it has to hold at EVERY ROLL ANGLE: the round's frame comes from a
     world-up LookRotation and does not roll with the ship, so the muzzle separation migrates
     from one lateral axis to the other as the pilot rolls.
  4. NO ROUND IS EVER FULLY DARK. Continuity of existence: between strokes the rim whisper has to
     keep the shell present, or a round pops out of and back into existence mid-flight.

What is measured is the shipped file, not a paraphrase of it. The HLSL is translated to compilable
C++ with the smallest possible edit set (swizzle accessors, `out` -> reference, and one rename that
wraps ChargeFBM1D in a call counter so the cost claim is measured too), compiled with clang++, and
executed — INCLUDING `ProjectileChargeFieldPhase_float`, which is why that function lives in the
include rather than inline in the vertex shader. The previous revision is pulled from git and run
through the identical harness, so the before/after is one measurement rather than two.

Usage:  python3 Tools/Shaders/verify_projectile_charge_field.py [--keep]
Exit 0 on pass. Needs clang++ and git; nothing else, and no Unity. Takes ~5 minutes.
"""

import math
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL_REL = "Assets/_Graphics/Materials/Graphs/ProjectileChargeField.hlsl"
HLSL = os.path.join(ROOT, HLSL_REL)
BASELINE_REV = "c3f2f856~1"   # the crackling-ball shell this pass replaced

# ── The Sparrow's real stream, from SPARROW_SPRAY_ACCURACY.md and Sparrow.prefab ─────────────
LAUNCH_HIT_RADIUS = 0.825        # dart collider radius x its largest lossy component
FLIGHT_SECONDS = 0.30
VOLLEYS_PER_SECOND = 90.0
ROUNDS_IN_FLIGHT = int(round(FLIGHT_SECONDS * VOLLEYS_PER_SECOND))   # 27 distinct radii
GROWTH_AT_REST = 3.0             # FullAutoAction.asset, Mass level 0
MUZZLE_HALF_SPACING = 3.2        # Sparrow.prefab: LeftGun/RightGun local x = -/+3.2
MUZZLE_SPEED = 375.0             # SPACE 0 muzzle speed
SHIP_POS = (312.0, -140.0, 455.0)   # somewhere in a 1200-radius arena, off every axis
SHIP_FORWARD = (0.31, 0.12, 0.94)

# ── Material values, so the harness measures what SHIPS, not what the shader defaults to ─────
NEW_MAT = dict(
    _CrackleColorA=(1.4979111, 0.0058463, 0.0068495, 1.0),
    _CrackleColorB=(0.10, 0.35, 1.0, 1.0),
    _FresnelRimColor=(0.25, 0.55, 1.0, 1.0),
    _ArcCount=1.0, _ArcSpan=1.45, _ArcSharpness=0.055, _ArcTiltRange=0.55, _ArcStartSpread=0.9, _ArcWander=0.26,
    _ArcWanderScale=2.6, _ArcIntensity=1.25, _TipGlow=0.9, _CoreThreshold=0.7,
    _CrackleRate=3.5, _StrikeTime=0.25, _HoldTime=0.042, _FadeShape=3.9,
    _FresnelRimIntensity=0.014, _FresnelRimPower=3.5,
    _ChargeReferenceRadius=4.95, _ChargeFloor=0.4, _PhaseByRadius=0.43, _PhaseSpeed=2.6,
    _SeedSpin=6.283, _SeedTilt=6.283, _SeedWobble=40.0, _PhaseBySeed=1.0,
)
OLD_MAT = dict(
    _CrackleColorA=(1.4979111, 0.0058463, 0.0068495, 1.0),
    _CrackleColorB=(0.10, 0.35, 1.0, 1.0),
    _FresnelRimColor=(0.25, 0.55, 1.0, 1.0),
    _ArcSeeds=3.0, _ArcDensity=5.0, _ArcSharpness=0.12, _ArcIntensity=1.0, _ArcReach=1.0,
    _CoreThreshold=0.75, _RingThickness=0.9, _CenterFillAmount=0.12, _RippleSpeed=1.6,
    _CrackleRate=6.0, _FresnelRimIntensity=0.18, _FresnelRimPower=2.5,
    _ChargeReferenceRadius=4.95, _ChargeFloor=0.35, _PhaseByRadius=1.7, _PhaseSpeed=1.0,
)
ALL_UNIFORMS = sorted(set(NEW_MAT) | set(OLD_MAT))

LIT = 0.15   # above the rim whisper's ceiling in BOTH revisions, so this measures arcs

SHIM = r"""// Minimal HLSL->C++ shim so a SHIPPED ProjectileChargeField.hlsl can be compiled and executed
// by clang++ (/asset-surgery 4.5c). Only what the file actually uses.
#include <cmath>
#include <cstdio>
#include <algorithm>
#include <vector>

struct float3 {
    float x=0,y=0,z=0;
    float3(){}
    explicit float3(float a):x(a),y(a),z(a){}
    float3(float a,float b,float c):x(a),y(b),z(c){}
    float3 v3() const { return *this; }
};
struct float4 {
    float x=0,y=0,z=0,w=0;
    float4(){}
    float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){}
    float3 v3() const { return float3(x,y,z); }
};
struct float2 {
    float x=0,y=0;
    float2(){}
    float2(float a,float b):x(a),y(b){}
};
typedef float3 half3; typedef float half; typedef float2 half2;

static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float3 operator/(float3 a,float b){return float3(a.x/b,a.y/b,a.z/b);}
static inline float3& operator+=(float3&a,float3 b){a=a+b;return a;}
static inline float3& operator*=(float3&a,float b){a=a*b;return a;}

static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float3 cross(float3 a,float3 b){
    return float3(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float3 normalize(float3 a){float l=length(a); return l>1e-20f?a/l:float3(0,0,1);}
static inline float max(float a,float b){return a>b?a:b;}
static inline float min(float a,float b){return a<b?a:b;}
static inline float abs(float v){return v<0.0f?-v:v;}
static inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
static inline float clamp(float v,float lo,float hi){return std::min(hi,std::max(lo,v));}
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
static inline float3 lerp(float3 a,float3 b,float t){return a+(b-a)*t;}
static inline float frac(float v){return v-std::floor(v);}
static inline float step(float e,float v){return v>=e?1.0f:0.0f;}
static inline float smoothstep(float e0,float e1,float v){
    float t = saturate((v-e0)/((e1-e0)!=0.0f?(e1-e0):1e-20f));
    return t*t*(3.0f-2.0f*t);
}
// Deliberately NOT `using namespace std`: unqualified pow/exp/sqrt/sin/cos/atan2/acos/floor/
// round resolve to the <cmath> float overloads, which is what HLSL means by them.

// ── Material uniforms, set from the shipped .mat by the driver ──
__UNIFORMS__

// ── The counted FBM. The shipped function is renamed and wrapped so the per-fragment cost
//    claim is a measurement rather than an assertion; nothing about its maths is touched. ──
static long g_fbmCalls = 0;
static long g_fragments = 0;
float ChargeFBM1D_shipped(float x, int octaves);
static inline float ChargeFBM1D(float x, int octaves){ g_fbmCalls++; return ChargeFBM1D_shipped(x, octaves); }
"""

# Entry-point adapters. The shipped revision resolves phase in the include (so the harness can
# compile and run it) and hands the fragment a `Lateral` identity; the baseline revision did
# neither — it resolved phase inline in its VERTEX shader and had no per-round signal beyond world
# radius, which IS the defect being measured. These two wrappers are the entire difference, so the
# driver below is identical for both.
ADAPT_NEW = r"""
static inline void PCF_Phase(float3 ax,float3 ay,float3 org,float t,float seed,float &ph,float &ch)
{ ProjectileChargeFieldPhase_float(ax, seed, t, ph, ch); }
static inline void PCF_Sample(float3 p,float3 n,float3 v,float ph,float ch,float seed,float3 va,float3 &em,float &a)
{ ProjectileChargeField_float(p,n,v,ph,ch,seed,va,em,a); }
"""
ADAPT_OLD = r"""
// The baseline's vertex shader, transcribed. What it IGNORES (AxisY, OriginWS) is the defect.
static inline void PCF_Phase(float3 ax,float3 ay,float3 org,float t,float seed,float &ph,float &ch)
{
    float worldRadius = 0.5f * length(ax);
    ph  = t * _PhaseSpeed + worldRadius * _PhaseByRadius;
    ch  = saturate(worldRadius / max(_ChargeReferenceRadius, 1e-3f));
}
static inline void PCF_Sample(float3 p,float3 n,float3 v,float ph,float ch,float seed,float3 va,float3 &em,float &a)
{ ProjectileChargeField_float(p,n,v,ph,ch,em,a); }
"""

DRIVER = r"""
// ── Driver. Each stdin line is one round POSE:
//      t radius  ox oy oz  rx ry rz  ux uy uz
//    (time, hit radius, world position, and the round's own right / up axes). The driver
//    rebuilds the object-to-world columns the vertex shader reads and calls the SHIPPED phase
//    resolver, then samples the shell over the unit sphere. It emits only what the harness
//    needs — visible-sample count, peak alpha, the phase, and the indices of the LIT samples.
//    (Printing every sample is ~1% signal and 99% I/O, and at 65M fragments that I/O is the
//    whole runtime.) ──
int main()
{
    const int N = __SAMPLES__;
    const float LIT = __LIT__f;
    const bool RENDER = __RENDER__;
    std::vector<float3> dirs; dirs.reserve(N);
    const float ga = (float)(M_PI * (3.0 - std::sqrt(5.0)));   // Fibonacci sphere
    for (int i = 0; i < N; i++) {
        float z = 1.0f - 2.0f * ((float)i + 0.5f) / (float)N;
        float r = std::sqrt(std::max(0.0f, 1.0f - z*z));
        float a = ga * (float)i;
        dirs.push_back(float3(r*std::cos(a), r*std::sin(a), z));
    }

    // Camera far along +z in OBJECT space: near-orthographic, so `visible` is simply the
    // front hemisphere — which is what `Cull Back` actually draws.
    const float3 camOS = float3(0.0f, 0.0f, 60.0f);

    float t, rad, ox, oy, oz, rx, ry, rz, ux, uy, uz, seed;
    while (std::scanf("%f %f %f %f %f %f %f %f %f %f %f %f",
                      &t,&rad,&ox,&oy,&oz,&rx,&ry,&rz,&ux,&uy,&uz,&seed) == 12) {
        float dia = 2.0f * rad;
        float phase, charge;
        PCF_Phase(float3(rx,ry,rz) * dia, float3(ux,uy,uz) * dia,
                  float3(ox,oy,oz), t, seed, phase, charge);
        int visible = 0; float peak = 0.0f;
        std::vector<int> lit;
        if (RENDER) {
            for (int i = 0; i < N; i++) {
                float3 posOS = dirs[i] * 0.5f;      // built-in sphere, object radius 0.5
                float3 nrmOS = dirs[i];
                float3 viewOS = camOS - posOS;
                if (dot(normalize(nrmOS), normalize(viewOS)) <= 0.0f) continue;   // Cull Back
                float3 em; float alpha;
                g_fragments++;
                PCF_Sample(posOS, nrmOS, viewOS, phase, charge, seed, camOS, em, alpha);
                visible++;
                if (alpha > peak) peak = alpha;
                if (alpha >= LIT) lit.push_back(i);
            }
        }
        std::printf("R %d %.6f %.9f %d", visible, peak, phase, (int)lit.size());
        for (size_t k = 0; k < lit.size(); k++) std::printf(" %d", lit[k]);
        std::printf("\n");
    }
    std::fprintf(stderr, "FBM %ld FRAG %ld\n", g_fbmCalls, g_fragments);
    return 0;
}
"""


# ── HLSL -> C++ ──────────────────────────────────────────────────────────────────────────────
def translate(hlsl_src, mat):
    src = hlsl_src
    # Longest-first alternation: `float` would otherwise match the prefix of `float2` and then
    # fail on the missing whitespace, silently leaving an `out` in the C++.
    src = re.sub(r"\bout\s+(float4|float3|float2|float|half4|half3|half2|half)\s+(\w+)",
                 r"\1 &\2", src)
    src = src.replace(".rgb", ".v3()").replace(".xyz", ".v3()")
    src = src.replace("float ChargeFBM1D(float x, int octaves)\n",
                      "float ChargeFBM1D_shipped(float x, int octaves)\n")
    if "float ChargeFBM1D_shipped" not in src:
        raise SystemExit("ChargeFBM1D definition not found in the expected shape")
    src += ADAPT_NEW if "ProjectileChargeFieldPhase_float" in hlsl_src else ADAPT_OLD
    uniforms = []
    for name in ALL_UNIFORMS:
        v = mat.get(name, (0.0, 0.0, 0.0, 0.0) if "Color" in name else 0.0)
        if isinstance(v, tuple):
            uniforms.append("static float4 %s = float4(%.7ff,%.7ff,%.7ff,%.7ff);" % ((name,) + v))
        else:
            uniforms.append("static float %s = %.7ff;" % (name, v))
    return SHIM.replace("__UNIFORMS__", "\n".join(uniforms)) + "\n" + src + "\n"


def build(tmp, tag, hlsl_src, mat, samples, render=True):
    cpp = os.path.join(tmp, "%s.cpp" % tag)
    exe = os.path.join(tmp, tag)
    with open(cpp, "w", encoding="utf-8") as f:
        f.write(translate(hlsl_src, mat))
        f.write(DRIVER.replace("__SAMPLES__", str(samples))
                      .replace("__LIT__", repr(LIT))
                      .replace("__RENDER__", "true" if render else "false"))
    r = subprocess.run(["clang++", "-O2", "-std=c++17", "-o", exe, cpp],
                       capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-6000:])
        raise SystemExit("clang++ failed to build the %s shell" % tag)
    return exe


def run(exe, poses):
    """-> (list of (visible, peak, phase, lit_index_set), fbm_calls, fragments)"""
    stdin = "".join(
        "%.9f %.9f %.9f %.9f %.9f %.9f %.9f %.9f %.9f %.9f %.9f %.9f\n"
        % (t, rad, o[0], o[1], o[2], r[0], r[1], r[2], u[0], u[1], u[2], sd)
        for (t, rad, o, r, u, sd) in poses)
    p = subprocess.run([exe], input=stdin, capture_output=True, text=True)
    if p.returncode != 0:
        raise SystemExit("shell executable failed: %s" % p.stderr[-2000:])
    out = []
    for line in p.stdout.splitlines():
        f = line.split()
        if not f or f[0] != "R":
            continue
        n = int(f[4])
        out.append((int(f[1]), float(f[2]), float(f[3]), set(int(x) for x in f[5:5 + n])))
    m = re.search(r"FBM (\d+) FRAG (\d+)", p.stderr)
    return out, int(m.group(1)), int(m.group(2))


# ── Round poses: where a round actually is, and in which frame ───────────────────────────────
def _n(v):
    l = math.sqrt(sum(x * x for x in v)) or 1.0
    return tuple(x / l for x in v)


def _cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


_SEED_STATE = [20260825]


def round_seed():
    """A per-SHOT random number, exactly what Projectile.StampChargeFieldSeed writes. Drawn from
    a fixed sequence so the two revisions under test see the identical stream."""
    _SEED_STATE[0] = (_SEED_STATE[0] * 1103515245 + 12345) & 0x7fffffff
    return (_SEED_STATE[0] >> 8) / float(1 << 23)


def round_pose(t, progress, growth, side=-1.0, roll=0.0,
               ship_pos=SHIP_POS, forward=SHIP_FORWARD):
    """One round in flight.

    Its frame is a world-up LookRotation of its aim (`SafeLookRotation`), so it does NOT roll
    with the ship — which is why the muzzle separation, which IS along the ship's right, has to
    be resolved into both lateral axes."""
    F = _n(forward)
    R = _n(_cross((0.0, 1.0, 0.0), F))
    U = _cross(F, R)
    ship_right = tuple(R[i] * math.cos(roll) + U[i] * math.sin(roll) for i in range(3))
    travel = MUZZLE_SPEED * FLIGHT_SECONDS * progress
    org = tuple(ship_pos[i] + F[i] * travel + ship_right[i] * MUZZLE_HALF_SPACING * side
                for i in range(3))
    return (t, LAUNCH_HIT_RADIUS * (1.0 + (growth - 1.0) * progress), org, R, U, round_seed())


def stream(t0, growth, count=ROUNDS_IN_FLIGHT, side=-1.0, roll=0.0):
    """One instant of a sustained burst: `count` rounds spread across their flight."""
    return [round_pose(t0, (k + 0.5) / count, growth, side, roll) for k in range(count)]


def fib_dirs(n):
    ga = math.pi * (3.0 - math.sqrt(5.0))
    out = []
    for i in range(n):
        z = 1.0 - 2.0 * (i + 0.5) / n
        rr = math.sqrt(max(0.0, 1.0 - z * z))
        out.append((rr * math.cos(ga * i), rr * math.sin(ga * i), z))
    return out


def coverage(rec):
    visible, _peak, _phase, lit = rec
    return (len(lit) / max(1, visible)), lit, visible


def planarity(lit_idx, dirs):
    """RMS |dot(dir, n)| for the best-fit plane through the origin, as an angle.

    A great-circle stroke sits in one plane -> small. A blob or a many-seeded scatter -> large."""
    pts = [dirs[i] for i in lit_idx]
    if len(pts) < 8:
        return None
    c = [[0.0] * 3 for _ in range(3)]
    for p in pts:
        for a in range(3):
            for b in range(3):
                c[a][b] += p[a] * p[b]
    n = len(pts)
    for a in range(3):
        for b in range(3):
            c[a][b] /= n
    tr = c[0][0] + c[1][1] + c[2][2]
    m = [[tr * 1.001 * (1 if a == b else 0) - c[a][b] for b in range(3)] for a in range(3)]
    v = [0.3, 0.5, 0.81]
    for _ in range(200):
        w = [sum(m[a][b] * v[b] for b in range(3)) for a in range(3)]
        ln = math.sqrt(sum(x * x for x in w)) or 1.0
        v = [x / ln for x in w]
    rms = math.sqrt(sum((sum(p[a] * v[a] for a in range(3))) ** 2 for p in pts) / n)
    return math.degrees(math.asin(min(1.0, rms)))


SYNCED = 0.12   # |cycle difference| below which two strokes read as the same stroke


def main():
    keep = "--keep" in sys.argv
    if not shutil.which("clang++"):
        raise SystemExit("clang++ not found")
    new_src = open(HLSL, encoding="utf-8").read()
    # Pinned to the revision BEFORE this pass began, not to HEAD~1 — once the pass has more than
    # one commit, HEAD~1 is an intermediate of the very thing under test and the "before" column
    # silently becomes a comparison against a half-finished design.
    old = subprocess.run(["git", "-C", ROOT, "show", "%s:%s" % (BASELINE_REV, HLSL_REL)],
                         capture_output=True, text=True)
    if old.returncode != 0:
        raise SystemExit("could not read baseline %s from git" % BASELINE_REV)
    old_src = old.stdout

    SAMPLES = 8000
    dirs = fib_dirs(SAMPLES)
    tmp = tempfile.mkdtemp(prefix="chargefield_")
    failures = []
    try:
        new_exe = build(tmp, "new", new_src, NEW_MAT, SAMPLES)
        old_exe = build(tmp, "old", old_src, OLD_MAT, SAMPLES)
        print("built both revisions from the shipped HLSL (clang++ -O2)\n")

        # ── 1. One round, one instant ────────────────────────────────────────────────────
        print("== 1. ONE ROUND AT ONE INSTANT (resting Mass, 3x growth) ==")
        print("   lit = alpha >= %.2f over the front hemisphere `Cull Back` actually draws\n" % LIT)
        print("   %-9s %10s %10s   %10s %10s" % ("", "new cov", "old cov", "new planar", "old planar"))
        poses = stream(1.7, GROWTH_AT_REST)
        nr, nfbm, nfrag = run(new_exe, poses)
        orr, ofbm, ofrag = run(old_exe, poses)
        new_covs, old_covs, new_ang, old_ang = [], [], [], []
        for k in (0, 6, 13, 20, 26):
            ncov, nidx, _ = coverage(nr[k]); ocov, oidx, _ = coverage(orr[k])
            na, oa = planarity(nidx, dirs), planarity(oidx, dirs)
            new_covs.append(ncov); old_covs.append(ocov)
            if na is not None: new_ang.append(na)
            if oa is not None: old_ang.append(oa)
            print("   round %-3d %9.2f%% %9.2f%%   %9s %9s" % (
                k, ncov * 100, ocov * 100,
                ("%.1f deg" % na) if na is not None else "-",
                ("%.1f deg" % oa) if oa is not None else "-"))
        mean_new = sum(new_covs) / len(new_covs)
        mean_old = sum(old_covs) / len(old_covs)
        ang_new = sum(new_ang) / len(new_ang) if new_ang else 99.0
        ang_old = sum(old_ang) / len(old_ang) if old_ang else 0.0
        print("\n   mean lit coverage: %.2f%% -> %.2f%%" % (mean_old * 100, mean_new * 100))
        print("   mean planarity:    %.1f deg -> %.1f deg  (a stroke on one great circle "
              "vs a scatter over the shell)" % (ang_old, ang_new))
        # Raw lit AREA is deliberately NOT the pass criterion, and finding that out is half of
        # what this harness is for: the old shell lit *few* pixels *everywhere* (15 thin filaments
        # from 3 seeds scattered over the whole sphere), so it read as a ball while measuring a
        # smaller lit fraction than one fat stroke would. What separates the two is SHAPE.
        # Deliberately no "must not light more than before" guard. It was here as a cheap
        # tone-down check and it was wrong twice over: raw lit AREA does not distinguish a
        # coherent bolt from a scatter (that is what planarity is for), and holding area below
        # the baseline is what drove the stroke down to 2 px, which — rendered at true pixel
        # density — was invisible past ~40 units and left every round a plain identical disc.
        if mean_new > mean_old * 2.5:
            failures.append("a single round lights %.1fx more of the shell than the baseline "
                            "(%.2f%% vs %.2f%%) — this is a tone-DOWN"
                            % (mean_new / max(mean_old, 1e-6), mean_new * 100, mean_old * 100))
        if ang_new > 8.0:
            failures.append("the lit set is not a great-circle stroke (mean planarity %.1f deg)"
                            % ang_new)
        if ang_new > ang_old * 0.5:
            failures.append("the lit set is no more curve-like than the old scatter "
                            "(%.1f deg vs %.1f deg)" % (ang_new, ang_old))

        # ── 2. The burst fills the sphere ────────────────────────────────────────────────
        print("\n== 2. THE BURST ASSEMBLES THE SPHERE ==")
        print("   union of what a sustained burst lights, in the shell's own object space\n")
        print("   %-34s %12s %12s" % ("", "new", "old"))
        fill_at_1s = 0.0
        for label, secs in (("one round, its whole flight", -1.0),
                            ("one frozen frame (27 x 2 muzzles)", -2.0),
                            ("0.25 s of fire", 0.25),
                            ("1.0 s of fire", 1.0),
                            ("3.0 s of fire", 3.0)):
            if secs == -1.0:
                st = [round_pose(1.7 + FLIGHT_SECONDS * (j / 24.0), j / 24.0, GROWTH_AT_REST)
                      for j in range(25)]
            elif secs == -2.0:
                st = stream(1.7, GROWTH_AT_REST, side=-1.0) + stream(1.7, GROWTH_AT_REST, side=+1.0)
            else:
                st, steps = [], max(1, int(secs * 40))
                for s_i in range(steps):
                    tt = 1.7 + s_i * (secs / steps)
                    st += stream(tt, GROWTH_AT_REST, side=-1.0)
                    st += stream(tt, GROWTH_AT_REST, side=+1.0)
            nrows, _, _ = run(new_exe, st)
            orows, _, _ = run(old_exe, st)
            nu, ou = set(), set()
            for r in nrows: nu |= r[3]
            for r in orows: ou |= r[3]
            vis = nrows[0][0]
            fill = len(nu) / vis
            print("   %-34s %11.1f%% %11.1f%%" % (label, fill * 100, 100.0 * len(ou) / vis))
            # No guard on the whole-flight row any more. It was written when the circle was
            # oriented in OBJECT space; the stroke is now anchored to the VIEW, so a round's
            # successive discharges necessarily land on the face you are looking at and the row
            # measures the discharge rate rather than the design. What matters is that at any
            # INSTANT a round is one stroke, which test 1 measures.
            if label == "1.0 s of fire":
                fill_at_1s = fill
            if label == "3.0 s of fire":
                if fill < 0.70:
                    failures.append("a sustained burst only fills %.0f%% of the sphere" % (fill * 100))
                # "still growing" only means anything below saturation; once every round in a
                # burst is an independent stroke the union tops out inside the first quarter
                # second, which is the goal rather than a stall.
                if fill <= fill_at_1s and fill < 0.99:
                    failures.append("the union stops growing — strokes are stacking, not accumulating")

        # ── 3. The two muzzles do not draw the same stroke ───────────────────────────────
        print("\n== 3. A VOLLEY'S TWO ROUNDS ARE DIFFERENT STROKES ==")
        print("   Both guns fire in one tick with the same growth factor, so the pair share a")
        print("   world radius EXACTLY — and radius used to be the shell's entire per-round")
        print("   signal. Measured on the OBSERVABLE: the overlap (Jaccard) of the two rounds'")
        print("   lit sets. Swept over every 10 deg of roll, because the round's frame comes")
        print("   from a world-up LookRotation and does NOT roll with the ship, so the 6.4-unit")
        print("   muzzle separation migrates from one lateral axis to the other as you roll.\n")
        pair_poses, roll_of = [], []
        for ri in range(36):
            roll = ri * math.pi / 18.0
            for si in range(3):
                sp = (SHIP_POS[0] * (0.2 + 0.4 * si), SHIP_POS[1] * (1.0 - 0.3 * si),
                      SHIP_POS[2] * (0.15 + 0.45 * si))
                for k in (2, 8, 14, 20, 25):
                    pr01 = (k + 0.5) / ROUNDS_IN_FLIGHT
                    tt = 1.7 + 0.013 * k
                    pair_poses.append(round_pose(tt, pr01, GROWTH_AT_REST, -1.0, roll, ship_pos=sp))
                    pair_poses.append(round_pose(tt, pr01, GROWTH_AT_REST, +1.0, roll, ship_pos=sp))
                    roll_of.append(ri)
        # NEGATIVE CONTROL: the shipped shader with the circle spin switched off, i.e. the
        # lateral read feeding the phase offset ALONE. It is the intermediate that was built
        # first and rejected, and it is here so the reason stays measured rather than asserted.
        no_seed = dict(NEW_MAT)
        for k in ("_SeedSpin", "_SeedTilt", "_SeedWobble", "_PhaseBySeed"):
            no_seed[k] = 0.0
        nospin_exe = build(tmp, "nospin", new_src, no_seed, SAMPLES)
        print("   %-14s %14s %16s %26s"
              % ("", "median overlap", "same stroke (>50%)", "worst 10-deg roll bucket"))
        results = {}
        for exe, rt, tag in ((new_exe, NEW_MAT["_CrackleRate"], "shipped"),
                             (nospin_exe, NEW_MAT["_CrackleRate"], "no seed"),
                             (old_exe, OLD_MAT["_CrackleRate"], "baseline")):
            rows, _, _ = run(exe, pair_poses)
            overlaps, by_roll, deltas = [], {}, []
            for j, ri in enumerate(roll_of):
                a, b = rows[2 * j][3], rows[2 * j + 1][3]
                if not a and not b:
                    continue                     # both between strokes; nothing to compare
                ov = len(a & b) / max(1, len(a | b))
                overlaps.append(ov)
                by_roll.setdefault(ri, []).append(ov)
                d = abs(rows[2 * j][2] - rows[2 * j + 1][2]) * rt
                deltas.append(abs(d - round(d)))
            overlaps.sort()
            if not overlaps:
                print("   %-14s (nothing lit — cannot compare)" % tag)
                results[tag] = (1.0, 1.0, 1.0, 0.0)
                continue
            med = overlaps[len(overlaps) // 2]
            same = sum(1 for o in overlaps if o > 0.5) / len(overlaps)
            worst_ri, worst_med = max(
                ((ri, sorted(v)[len(v) // 2]) for ri, v in by_roll.items()), key=lambda kv: kv[1])
            results[tag] = (med, same, worst_med, sorted(deltas)[len(deltas) // 2])
            print("   %-14s %13.1f%% %15.1f%% %19d deg %6.1f%%"
                  % (tag, med * 100, same * 100, worst_ri * 10, worst_med * 100))
        print("\n   The 'no seed' row is this exact shader with the per-round seed's four")
        print("   contributions zeroed — i.e. the state that shipped and was reported as still")
        print("   reading identical. It is kept as a permanent negative control, because three")
        print("   passes of geometry-derived identity all measured decorrelated and all still")
        print("   looked the same.")
        med, same, worst_med, _ = results["shipped"]
        if med > 0.35:
            failures.append("a volley's two rounds overlap %.0f%% of the time (median)" % (med * 100))
        if same > 0.25:
            failures.append("a volley's two rounds draw the same stroke %.0f%% of the time" % (same * 100))
        if worst_med > 0.6:
            failures.append("at some roll angle the pair still overlaps %.0f%% (median) — a "
                            "lateral axis is being lost to the roll" % (worst_med * 100))

        # ── 4. Never fully dark ──────────────────────────────────────────────────────────
        print("\n== 4. A ROUND IS ALWAYS SHOWING SOMETHING (continuity of existence) ==")
        worst, worst_at, showing, total = 1.0, None, 0, 0
        small = build(tmp, "small", new_src, NEW_MAT, 800)
        dark_poses = []
        for s_i in range(120):
            for side in (-1.0, +1.0):
                dark_poses += stream(1.7 + s_i * 0.004, GROWTH_AT_REST, side=side)
        rows, _, _ = run(small, dark_poses)
        for j, rec in enumerate(rows):
            total += 1
            if rec[1] >= LIT:
                showing += 1
            if rec[1] < worst:
                worst, worst_at = rec[1], (j // (2 * ROUNDS_IN_FLIGHT), j % ROUNDS_IN_FLIGHT)
        duty = showing / max(1, total)
        print("   over 0.48 s x 27 rounds x 2 muzzles (%d round-states):" % total)
        print("      showing a stroke (peak alpha >= %.2f): %.1f%% of the time" % (LIT, duty * 100))
        print("      dimmest any round ever gets:           peak alpha %.4f (round %d @ step %d)"
              % (worst, worst_at[1], worst_at[0]))
        if worst <= 0.0:
            failures.append("a round goes completely dark mid-flight")
        # No duty FLOOR any more: the shell is deliberately sparse, and "most rounds are dark
        # most of the time" is the mechanism by which 54 simultaneous shells stay quiet. The
        # requirement is only that a round is never *fully* dark — the rim whisper — and that is
        # the `worst` check above. A duty CEILING is the real guard, and test 6 owns it.
        if duty > 0.85:
            failures.append("a round is showing a stroke %.0f%% of the time — at 54 shells on "
                            "screen that sums into a solid rope" % (duty * 100))

        # ── 6. The cumulative light of a full-auto stream ────────────────────────────────
        # Imported here rather than at module scope because the renderer imports THIS module;
        # by the time a test runs, this one is fully loaded and the cycle is harmless.
        import render_projectile_charge_field as RND
        print("\n== 6. FULL AUTO IS NOT OVERWHELMING ==")
        print("   A Sparrow keeps %d shells on screen at once and their emission SUMS. This"
              % (ROUNDS_IN_FLIGHT * 2))
        print("   renders the live stream — every round at its true position, size and seed —")
        print("   and totals the LINEAR light the frame emits, before any tonemap.\n")
        BW, BH = 1100, 420
        bfov = 2 * math.degrees(math.atan(math.tan(math.radians(30.0)) * BH / 1080.0))
        budget = []
        for tag, src, mat in (("baseline (crackling ball)", old_src, OLD_MAT),
                              ("shipped", new_src, NEW_MAT)):
            RND._SEED[0] = 20260825
            ex = RND.build(tmp, "budget_" + tag.split()[0], src, mat)
            buf = RND.render(ex, BW, BH, bfov, RND.stream_rounds(2.4, cam_back=15.0))
            budget.append((tag, RND.render.last_light, RND.screen_stats(buf)))
        base_light = budget[0][1]
        print("   %-28s %12s %9s %9s" % ("", "light", "vs base", "screen >20%"))
        for tag, light, st in budget:
            print("   %-28s %12.0f %8.2fx %8.2f%%" % (tag, light, light / base_light, st[1] * 100))
        ratio = budget[1][1] / max(base_light, 1e-9)
        print("\n   The first cut of the one-stroke design measured 2.11x here — it toned down a")
        print("   single round and made a burst WORSE, which no per-round metric could see.")
        # 0.06x, not a round number: it has to be tight enough to CATCH the previous shipped
        # value (0.078x, itself already a 26x cut and still called overtuned in playtest). A
        # ceiling that the thing it replaced would pass is not a gate.
        if ratio > 0.06:
            failures.append("a full-auto stream emits %.3fx the baseline's light — over the 0.06x "
                            "budget, which is the value playtest settled on" % ratio)

        # ── 5. Cost ──────────────────────────────────────────────────────────────────────
        print("\n== 5. COST ==")
        print("   FBM evaluations per fragment: %.2f -> %.2f   (worst case %d -> %d)"
              % (ofbm / ofrag, nfbm / nfrag, 15, 1))
        if nfbm / nfrag > ofbm / ofrag:
            failures.append("the new shell costs more FBM per fragment than the old one")

    finally:
        if keep:
            print("\nkept: %s" % tmp)
        else:
            shutil.rmtree(tmp, ignore_errors=True)

    print()
    if failures:
        for f in failures:
            print("FAIL: %s" % f)
        return 1
    print("PASS: one round is a stroke, a volley's two rounds are different strokes, "
          "the burst is the sphere, and nothing ever pops.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
