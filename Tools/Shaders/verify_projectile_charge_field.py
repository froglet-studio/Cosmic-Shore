#!/usr/bin/env python3
"""Prove the SHIPPED ProjectileChargeField.hlsl draws ONE STROKE per round — by compiling and
running it, and by running the version it replaced beside it.

The 2026-08-25 pass answers a playtest note: *"tone it down so each projectile reads less like a
sphere, but together they build a spherical effect over time — a single arc on each projectile,
and after many projectiles those arcs stochastically fill in the circle as an after-image."*

That is a claim about a DISTRIBUTION over rounds, and it is exactly the kind of claim static
reading cannot settle. Three things have to hold at once, and two of them pull against each other:

  1. ONE ROUND, AT ONE INSTANT, IS NOT A SPHERE. Its lit set must be a small fraction of the
     visible hemisphere, and it must be a CURVE — lit fragments lying close to one plane through
     the centre, i.e. on one great circle — not a blob and not a scatter.
  2. THE VOLLEY IS. The union of what a burst lights must climb toward covering the sphere. If
     consecutive rounds landed on the same great circle (they differ only in world radius, which
     is simultaneously their identity AND their progress — see the shader header) the strokes
     would stack instead of accumulating and nothing would ever fill in.
  3. NO ROUND IS EVER FULLY DARK. Continuity of existence: between strokes the rim whisper has to
     keep the shell present, or a round pops out of and back into existence mid-flight.

What is measured is the shipped file, not a paraphrase of it: the HLSL is translated to
compilable C++ with the smallest possible edit set (swizzle accessors, `out` -> reference, and one
rename that wraps ChargeFBM1D in a call counter so the cost claim is measured too), compiled with
clang++, and executed. The previous revision is pulled from git and run through the identical
harness so the before/after is one measurement, not two.

Usage:  python3 Tools/Shaders/verify_projectile_charge_field.py [--keep]
Exit 0 on pass. Needs clang++ and git; nothing else, and no Unity.
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

# ── The Sparrow's real stream, from SPARROW_SPRAY_ACCURACY.md ────────────────────────────────
LAUNCH_HIT_RADIUS = 0.825      # dart collider radius x its largest lossy component
FLIGHT_SECONDS = 0.30
VOLLEYS_PER_SECOND = 90.0
ROUNDS_IN_FLIGHT = int(round(FLIGHT_SECONDS * VOLLEYS_PER_SECOND))   # 27 distinct radii
GROWTH_AT_REST = 3.0           # FullAutoAction.asset, Mass level 0
GROWTH_AT_MASS_10 = 6.0

# ── Material values, so the harness measures what SHIPS, not what the shader defaults to ─────
NEW_MAT = dict(
    _CrackleColorA=(1.4979111, 0.0058463, 0.0068495, 1.0),
    _CrackleColorB=(0.10, 0.35, 1.0, 1.0),
    _FresnelRimColor=(0.25, 0.55, 1.0, 1.0),
    _ArcCount=1.0, _ArcSpan=5.0, _ArcSharpness=0.038, _ArcWander=0.26,
    _ArcWanderScale=2.6, _ArcIntensity=1.6, _TipGlow=0.9, _CoreThreshold=0.7,
    _CrackleRate=3.5, _StrikeTime=0.25, _HoldTime=0.5, _FadeShape=1.0,
    _FresnelRimIntensity=0.05, _FresnelRimPower=3.5,
    _ChargeReferenceRadius=4.95, _ChargeFloor=0.4, _PhaseByRadius=0.43, _PhaseSpeed=2.6,
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

# Every uniform either revision can reference. Declaring the union lets one shim compile both.
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
typedef float3 half3; typedef float half;

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
// abs/round/max/min resolve to the <cmath> float overloads, which is what HLSL means by them.

// ── Material uniforms, set from the shipped .mat by the driver ──
__UNIFORMS__

// ── The counted FBM. The shipped function is renamed and wrapped so the per-fragment cost
//    claim is a measurement rather than an assertion; nothing about its maths is touched. ──
static long g_fbmCalls = 0;
static long g_fragments = 0;
float ChargeFBM1D_shipped(float x, int octaves);
static inline float ChargeFBM1D(float x, int octaves){ g_fbmCalls++; return ChargeFBM1D_shipped(x, octaves); }
"""

DRIVER = r"""
// ── Driver: sample the shell over the unit sphere. Emits only what the harness needs —
//    per round, the count of VISIBLE samples, the peak alpha, and the indices of the LIT
//    ones. (Printing every sample is ~1% signal and 99% I/O, and at 65M fragments that I/O
//    is the whole runtime.) ──
int main()
{
    const int N = __SAMPLES__;
    const float LIT = __LIT__f;
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

    float t, worldRadius;
    while (std::scanf("%f %f", &t, &worldRadius) == 2) {
        float phase  = t * _PhaseSpeed + worldRadius * _PhaseByRadius;
        float charge = saturate(worldRadius / std::max(_ChargeReferenceRadius, 1e-3f));
        int visible = 0; float peak = 0.0f;
        std::vector<int> lit;
        for (int i = 0; i < N; i++) {
            float3 posOS = dirs[i] * 0.5f;          // built-in sphere, object radius 0.5
            float3 nrmOS = dirs[i];
            float3 viewOS = camOS - posOS;
            if (dot(normalize(nrmOS), normalize(viewOS)) <= 0.0f) continue;   // Cull Back
            float3 em; float alpha;
            g_fragments++;
            ProjectileChargeField_float(posOS, nrmOS, viewOS, phase, charge, em, alpha);
            visible++;
            if (alpha > peak) peak = alpha;
            if (alpha >= LIT) lit.push_back(i);
        }
        std::printf("R %d %.6f %d", visible, peak, (int)lit.size());
        for (size_t k = 0; k < lit.size(); k++) std::printf(" %d", lit[k]);
        std::printf("\n");
    }
    std::fprintf(stderr, "FBM %ld FRAG %ld\n", g_fbmCalls, g_fragments);
    return 0;
}
"""


def translate(hlsl_src, mat):
    """Smallest edit set that makes the shipped HLSL compilable C++."""
    src = hlsl_src
    # `out` parameters become references.
    src = re.sub(r"\bout\s+(float3|float|half3|half)\s+(\w+)", r"\1 &\2", src)
    # Swizzles on the colour float4s.
    src = src.replace(".rgb", ".v3()").replace(".xyz", ".v3()")
    # Count FBM calls without touching its maths.
    src = src.replace("float ChargeFBM1D(float x, int octaves)\n",
                      "float ChargeFBM1D_shipped(float x, int octaves)\n")
    if "float ChargeFBM1D_shipped" not in src:
        raise SystemExit("ChargeFBM1D definition not found in the expected shape")
    # Include guards are harmless; the #define of PCF_TAU is valid C++.
    uniforms = []
    for name in ALL_UNIFORMS:
        v = mat.get(name)
        if v is None:
            v = (0.0, 0.0, 0.0, 0.0) if name.endswith("Color") or "Color" in name else 0.0
        if isinstance(v, tuple):
            uniforms.append("static float4 %s = float4(%.7ff,%.7ff,%.7ff,%.7ff);" % ((name,) + v))
        else:
            uniforms.append("static float %s = %.7ff;" % (name, v))
    shim = SHIM.replace("__UNIFORMS__", "\n".join(uniforms))
    return shim + "\n" + src + "\n"


def build(tmp, tag, hlsl_src, mat, samples):
    cpp = os.path.join(tmp, "%s.cpp" % tag)
    exe = os.path.join(tmp, tag)
    with open(cpp, "w", encoding="utf-8") as f:
        f.write(translate(hlsl_src, mat))
        f.write(DRIVER.replace("__SAMPLES__", str(samples)).replace("__LIT__", repr(LIT)))
    r = subprocess.run(["clang++", "-O2", "-std=c++17", "-o", exe, cpp],
                       capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-6000:])
        raise SystemExit("clang++ failed to build the %s shell" % tag)
    return exe


def run(exe, states, samples):
    """-> (list of per-round (visible, peak, lit_index_set), fbm_calls, fragments)"""
    stdin = "".join("%.9f %.9f\n" % (t, r) for (t, r) in states)
    r = subprocess.run([exe], input=stdin, capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit("shell executable failed: %s" % r.stderr[-2000:])
    rounds = []
    for line in r.stdout.splitlines():
        p = line.split()
        if not p or p[0] != "R":
            continue
        visible, peak, n = int(p[1]), float(p[2]), int(p[3])
        rounds.append((visible, peak, set(int(x) for x in p[4:4 + n])))
    m = re.search(r"FBM (\d+) FRAG (\d+)", r.stderr)
    return rounds, int(m.group(1)), int(m.group(2))


def fib_dirs(n):
    ga = math.pi * (3.0 - math.sqrt(5.0))
    out = []
    for i in range(n):
        z = 1.0 - 2.0 * (i + 0.5) / n
        rr = math.sqrt(max(0.0, 1.0 - z * z))
        a = ga * i
        out.append((rr * math.cos(a), rr * math.sin(a), z))
    return out



def coverage(rec):
    visible, _peak, lit = rec
    return (len(lit) / max(1, visible)), lit, visible


def planarity(lit_idx, dirs):
    """RMS |dot(dir, n)| for the best-fit plane through the origin.

    A great-circle stroke sits in one plane -> small. A blob or a many-seeded scatter -> ~0.5.
    Reported as an angle so it is comparable to the shader's own _ArcSharpness."""
    pts = [dirs[i] for i in lit_idx]
    if len(pts) < 8:
        return None, None
    c = [[0.0] * 3 for _ in range(3)]
    for p in pts:
        for a in range(3):
            for b in range(3):
                c[a][b] += p[a] * p[b]
    n = len(pts)
    for a in range(3):
        for b in range(3):
            c[a][b] /= n
    # Smallest eigenvector by inverse power iteration on the 3x3 covariance (shifted).
    tr = c[0][0] + c[1][1] + c[2][2]
    m = [[tr * 1.001 * (1 if a == b else 0) - c[a][b] for b in range(3)] for a in range(3)]
    v = [0.3, 0.5, 0.81]
    for _ in range(200):
        w = [sum(m[a][b] * v[b] for b in range(3)) for a in range(3)]
        ln = math.sqrt(sum(x * x for x in w)) or 1.0
        v = [x / ln for x in w]
    rms = math.sqrt(sum((sum(p[a] * v[a] for a in range(3))) ** 2 for p in pts) / n)
    return rms, math.degrees(math.asin(min(1.0, rms)))


def stream_states(t0, growth, count=ROUNDS_IN_FLIGHT):
    """One instant of a sustained burst: `count` rounds spread across their flight."""
    out = []
    for k in range(count):
        p = (k + 0.5) / count
        out.append((t0, LAUNCH_HIT_RADIUS * (1.0 + (growth - 1.0) * p)))
    return out


def main():
    keep = "--keep" in sys.argv
    if not shutil.which("clang++"):
        raise SystemExit("clang++ not found")
    new_src = open(HLSL, encoding="utf-8").read()
    old_src = subprocess.run(["git", "-C", ROOT, "show", "HEAD:%s" % HLSL_REL],
                             capture_output=True, text=True)
    if old_src.returncode != 0:
        raise SystemExit("could not read the previous revision from git")
    old_src = old_src.stdout

    SAMPLES = 20000
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
        header = "   %-9s %10s %10s   %10s %10s" % ("", "new cov", "old cov", "new planar", "old planar")
        print(header)
        rows = []
        states = stream_states(1.7, GROWTH_AT_REST)
        nr, nfbm, nfrag = run(new_exe, states, SAMPLES)
        orr, ofbm, ofrag = run(old_exe, states, SAMPLES)
        new_covs, old_covs, new_ang, old_ang = [], [], [], []
        for k in (0, 6, 13, 20, 26):
            ncov, nidx, _ = coverage(nr[k]); ocov, oidx, _ = coverage(orr[k])
            _, na = planarity(nidx, dirs); _, oa = planarity(oidx, dirs)
            new_covs.append(ncov); old_covs.append(ocov)
            if na is not None: new_ang.append(na)
            if oa is not None: old_ang.append(oa)
            print("   round %-3d %9.2f%% %9.2f%%   %9s %9s" % (
                k, ncov * 100, ocov * 100,
                ("%.1f deg" % na) if na is not None else "-",
                ("%.1f deg" % oa) if oa is not None else "-"))
        mean_new = sum(new_covs) / len(new_covs)
        mean_old = sum(old_covs) / len(old_covs)
        print("\n   mean lit coverage: %.2f%% -> %.2f%%  (%.1fx less of the shell lit at once)"
              % (mean_old * 100, mean_new * 100, mean_old / max(mean_new, 1e-6)))
        if new_ang and old_ang:
            print("   mean planarity:    %.1f deg -> %.1f deg  (a stroke on one great circle "
                  "vs a scatter over the shell)" % (
                      sum(old_ang) / len(old_ang), sum(new_ang) / len(new_ang)))
        # Raw lit AREA is deliberately NOT the pass criterion, and finding that out is half
        # of what this harness is for: the old shell lit *few* pixels *everywhere* (15 thin
        # filaments from 3 seeds scattered over the whole sphere), so it read as a ball while
        # measuring a smaller lit fraction than a single fat stroke would. What separates the
        # two is SHAPE — whether the lit set is one curve on one plane — and how much of its
        # own shell one round paints. Area only has to not go UP.
        if mean_new > mean_old:
            failures.append("a single round now lights MORE of the shell than before "
                            "(%.2f%% vs %.2f%%)" % (mean_new * 100, mean_old * 100))
        ang_new = sum(new_ang) / len(new_ang) if new_ang else 99.0
        ang_old = sum(old_ang) / len(old_ang) if old_ang else 0.0
        if ang_new > 8.0:
            failures.append("the lit set is not a great-circle stroke (mean planarity %.1f deg)"
                            % ang_new)
        if ang_new > ang_old * 0.5:
            failures.append("the lit set is no more curve-like than the old scatter "
                            "(%.1f deg vs %.1f deg)" % (ang_new, ang_old))

        # ── 2. The burst fills the sphere ────────────────────────────────────────────────
        print("\n== 2. THE BURST ASSEMBLES THE SPHERE ==")
        print("   union of what a sustained burst lights, in the shell's own object space\n")
        fill_at_1s = 0.0
        print("   %-34s %12s %12s" % ("", "new", "old"))
        for label, secs in (("one round, its whole flight", -1.0),
                            ("one instant of the stream (27)", -2.0),
                            ("0.25 s of fire", 0.25),
                            ("1.0 s of fire", 1.0),
                            ("3.0 s of fire", 3.0)):
            if secs == 0.0:
                st = [stream_states(1.7, GROWTH_AT_REST)[13]]
            elif secs == -1.0:
                st = [(1.7 + FLIGHT_SECONDS * (j / 24.0),
                       LAUNCH_HIT_RADIUS * (1.0 + (GROWTH_AT_REST - 1.0) * (j / 24.0)))
                      for j in range(25)]
            elif secs == -2.0:
                st = stream_states(1.7, GROWTH_AT_REST)
            else:
                st = []
                steps = max(1, int(secs * 40))
                for s in range(steps):
                    st += stream_states(1.7 + s * (secs / steps), GROWTH_AT_REST)
            nrows, _, _ = run(new_exe, st, SAMPLES)
            orows, _, _ = run(old_exe, st, SAMPLES)
            nu, ou = set(), set()
            for r in nrows: nu |= coverage(r)[1]
            for r in orows: ou |= coverage(r)[1]
            vis = coverage(nrows[0])[2]
            print("   %-34s %11.1f%% %11.1f%%" % (label, 100.0 * len(nu) / vis, 100.0 * len(ou) / vis))
            fill = len(nu) / vis
            if label == "one round, its whole flight":
                # The round must NOT assemble the sphere by itself — that is the job the
                # player's eye is supposed to do across a burst.
                if fill > 0.5 or fill > (len(ou) / vis) * 0.7:
                    failures.append("one round paints %.0f%% of its own shell over its flight "
                                    "(old %.0f%%) — it is still drawing the sphere itself"
                                    % (fill * 100, 100.0 * len(ou) / vis))
            if label == "3.0 s of fire":
                if fill < 0.70:
                    failures.append("a sustained burst only fills %.0f%% of the sphere" % (fill * 100))
                if fill <= fill_at_1s:
                    failures.append("the union stops growing — strokes are stacking, not accumulating")
            if label == "1.0 s of fire":
                fill_at_1s = fill

        # ── 3. Never fully dark ──────────────────────────────────────────────────────────
        print("\n== 3. A ROUND IS ALWAYS SHOWING SOMETHING (continuity of existence) ==")
        worst, worst_at, showing, total = 1.0, None, 0, 0
        for s_i in range(120):
            st = stream_states(1.7 + s_i * 0.004, GROWTH_AT_REST)
            rows, _, _ = run(new_exe, st, 800)
            for k, rec in enumerate(rows):
                total += 1
                if rec[1] >= LIT:
                    showing += 1
                if rec[1] < worst:
                    worst, worst_at = rec[1], (s_i, k)
        duty = showing / max(1, total)
        print("   over 0.48 s x 27 rounds (%d round-states):" % total)
        print("      showing a stroke (peak alpha >= %.2f): %.1f%% of the time" % (LIT, duty * 100))
        print("      dimmest any round ever gets:           peak alpha %.4f (round %d @ step %d)"
              % (worst, worst_at[1], worst_at[0]))
        if worst <= 0.0:
            failures.append("a round goes completely dark mid-flight")
        if duty < 0.70:
            failures.append("a round is only showing a stroke %.0f%% of the time — the stream "
                            "will read as twinkling" % (duty * 100))

        # ── 4. Cost ──────────────────────────────────────────────────────────────────────
        print("\n== 4. COST ==")
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
    print("PASS: one round is a stroke, the burst is the sphere, and nothing ever pops.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
