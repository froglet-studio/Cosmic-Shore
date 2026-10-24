#!/usr/bin/env python3
"""
Compile the SHIPPED lattice shader maths with clang and render it (asset-surgery §4.5c): the real
Assets/_Graphics/Materials/Shaders/UI/UILattice.cginc and the real fragment body of Lattice.shader, through a
short listed set of substitutions, into a C++ harness that renders frames to PNG and asserts the dials drive.

    python3 Tools/Build/lattice_preview.py                 # compile, assert, render PNGs to the scratch dir
    python3 Tools/Build/lattice_preview.py --out DIR       # render into DIR
    python3 Tools/Build/lattice_preview.py --check         # compile + assert only (no PNGs)

What it proves for the frosted hex lattice (rev 11 option H, section 62): the include and the fragment compile
(-Wall -Werror); about two outlines per cell across the frame (Amoebius's radius 34, inset 0.92); the edge fade
empties the corners; still is still; a hidden window draws nothing; the frost is opaque and is the world tinted
halfway to navy, and a panel material draws none; the ring (H2) ripples outward at its set speed; the tide (H4)
lights a band that moves; grow on open (H6) starts empty and ends on the still frame; the vertex-alpha reveal is
a disc. The rim (ElementRim.shader) and the presence orb and party bonds (Presence.shader,
Bond.shader on UIPresence.cginc, section 45.4) go through the same translation: the orb draws exactly _Online dots
on its orbit and turns them, a bond lights only in its member's colour and only for a seated member, the bond
dashes flow outward, and the molecule's bond stays on its line. What it does not: Unity's own shader compile (no editor here) - the UI plumbing
(stencil, clip rect, UnityCG) is Unity's standard UI/Default block, copied from SilhouetteHolo.shader.
"""
import argparse
import os
import re
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
SHADER_DIR = os.path.join(REPO, "Assets/_Graphics/Materials/Shaders/UI")
CGINC = os.path.join(SHADER_DIR, "UILattice.cginc")
SHADER = os.path.join(SHADER_DIR, "Lattice.shader")
RIM_CGINC = os.path.join(SHADER_DIR, "UIElementRim.cginc")
RIM_SHADER = os.path.join(SHADER_DIR, "ElementRim.shader")
PRESENCE_CGINC = os.path.join(SHADER_DIR, "UIPresence.cginc")
PRESENCE_SHADER = os.path.join(SHADER_DIR, "Presence.shader")
BOND_SHADER = os.path.join(SHADER_DIR, "Bond.shader")

# The whole translation: HLSL out-params to C++ references, and the vector constructor spellings.
SUBS = [(r"\bout float\b", "float&"),
        (r"\bout float2\b", "float2&"),
        (r"\bfloat2\(", "mk2("),
        (r"\bfloat3\(", "mk3("),
        (r"\bfloat4\(", "mk4(")]

SHIM = r"""
#include <cmath>
#include <cstdio>
#include <cstdlib>
typedef float float2 __attribute__((ext_vector_type(2)));
typedef float float3 __attribute__((ext_vector_type(3)));
typedef float float4 __attribute__((ext_vector_type(4)));
typedef float4 fixed4;
static inline float2 mk2(float a, float b) { return (float2){a, b}; }
static inline float3 mk3(float a, float b, float c) { return (float3){a, b, c}; }
static inline float4 mk4(float3 c, float a) { return (float4){c.x, c.y, c.z, a}; }
static inline float4 mk4(float a, float b, float c, float d) { return (float4){a, b, c, d}; }
static inline float frac(float x) { return x - std::floor(x); }
static inline float saturate(float x) { return x < 0 ? 0 : (x > 1 ? 1 : x); }
static inline float hmin(float a, float b) { return a < b ? a : b; }
static inline float hmax(float a, float b) { return a > b ? a : b; }
static inline float habs(float a) { return a < 0 ? -a : a; }
static inline float step(float e, float x) { return x >= e ? 1.0f : 0.0f; }
static inline float smoothstep(float a, float b, float x) { float t = saturate((x - a) / (b - a)); return t * t * (3 - 2 * t); }
static inline float dot(float2 a, float2 b) { return a.x * b.x + a.y * b.y; }
static inline float length(float2 a) { return std::sqrt(dot(a, a)); }
static inline float floor_(float x) { return std::floor(x); }
static inline float2 floor_(float2 v) { return (float2){std::floor(v.x), std::floor(v.y)}; }
static inline float2 habs(float2 v) { return (float2){v.x < 0 ? -v.x : v.x, v.y < 0 ? -v.y : v.y}; }
static inline float2 hmax(float2 v, float m) { return (float2){v.x > m ? v.x : m, v.y > m ? v.y : m}; }
static inline float exp_(float x) { return std::exp(x); }
static inline float sin_(float x) { return std::sin(x); }
static inline float3 lerp(float3 a, float3 b, float t) { return a + (b - a) * t; }
static inline float lerp(float a, float b, float t) { return a + (b - a) * t; }
static inline float atan2_(float y, float x) { return std::atan2(y, x); }
static inline float cos_(float x) { return std::cos(x); }
typedef float4 half4;
typedef int sampler2D;
static inline float4 tex2D(sampler2D, float2) { return (float4){1, 1, 1, 1}; }
#define min hmin
#define max hmax
#define abs habs
#define floor floor_
#define sin sin_
#define atan2 atan2_
#define cos cos_
#define exp exp_
"""

UNIFORMS = r"""
struct v2f { float4 vertex; float4 color; float2 texcoord; float4 worldPosition; float4 motion; float4 screenPos; };
float4 _Time;
fixed4 _Color, _FrostTint, _LineColor, _GlowColor;
float _Frost, _FrostAmount, _LineAlpha, _LineWidth, _HexRadius, _HexInset, _FadeStart, _Motion;
float _RingAlpha, _RingWavelength, _RingPeriod, _TideAlpha, _TidePeriod, _TideWidth, _GrowSeconds, _Reveal;
float4 g_world = (float4){1, 0, 0, 1};
static inline float4 tex2D_world(int, float2) { return g_world; }
#define tex2D tex2D_world
int _FrogletBackdropBlur;
float _FrogletBackdropBlurOn;
"""

MAIN = r"""
#undef min
#undef max
#undef abs
#undef floor
#undef sin
#undef atan2
#undef cos
#undef tex2D
static const int SW = 1920, SH = 1080;
static void defaults() {
    _Color = (float4){1, 1, 1, 1}; _Frost = 1; _FrostTint = (float4){0, 0.004f, 0.039f, 1}; _FrostAmount = 0.5f;
    _LineColor = (float4){0.345f, 0.4f, 0.69f, 1}; _LineAlpha = 0.4f; _LineWidth = 1; _HexRadius = 34; _HexInset = 0.92f;
    _FadeStart = 0.6f; _Motion = 0; _GlowColor = (float4){0.498f, 0.847f, 1, 1}; _RingAlpha = 0.85f; _RingWavelength = 862;
    _RingPeriod = 5.2f; _TideAlpha = 0.18f; _TidePeriod = 14; _TideWidth = 0.16f; _GrowSeconds = 1.2f; _Reveal = 0;
    _FrogletBackdropBlurOn = 0; g_world = (float4){1, 0, 0, 1};
}
// One pixel of a full-screen 1920 x 1080 backdrop; motion: 0 = none (the material's), else the motion + 1.
static float4 at(float x, float y, float t, float motion, float opened, float alpha = 1) {
    _Time = (float4){t / 20, t, t * 2, t * 3};
    v2f IN;
    IN.color = (float4){1, 1, 1, alpha};
    IN.texcoord = (float2){x / SW, y / SH};
    IN.worldPosition = (float4){x, y, 0, 1};
    IN.motion = (float4){motion, opened, (float)SW, (float)SH};
    IN.screenPos = (float4){x / SW, y / SH, 0, 1};
    return frag(IN);
}
// The mean alpha over a coarse sample of the frame, its centre and its corners.
struct Stats { double mean, centre, corner; };
static Stats scan(float t, float motion, float opened, float alpha = 1) {
    Stats s = {0, 0, 0}; int n = 0, nc = 0, nk = 0;
    for (int y = 1; y < SH; y += 3)
        for (int x = 1; x < SW; x += 3) {
            float a = at(x + 0.5f, y + 0.5f, t, motion, opened, alpha).w;
            s.mean += a; n++;
            float dx = x / (float)SW - 0.5f, dy = y / (float)SH - 0.5f;
            if (dx * dx + dy * dy < 0.01f) { s.centre += a; nc++; }
            if (dx * dx + dy * dy > 0.42f) { s.corner += a; nk++; }
        }
    s.mean /= n; s.centre /= nc ? nc : 1; s.corner /= nk ? nk : 1;
    return s;
}
// Line crossings along a row (alpha rising above 0.1).
static int crossings(float y, float t) {
    int c = 0; bool on = false;
    for (int x = 0; x < SW; x++) { bool l = at(x + 0.5f, y, t, 0, 0).w > 0.1f; if (l && !on) c++; on = l; }
    return c;
}
// Where the ring is brightest along the centre row, right of the centre (canvas units from the centre).
static float ringRadius(float t) {
    float best = -1; int bx = 0;
    for (int x = SW / 2; x < SW; x++) {
        float4 c = at(x + 0.5f, SH / 2 + 0.5f, t, 2, 0);
        float b = c.w * c.z;      // the glow is light blue: alpha times blue on the lines
        if (b > best) { best = b; bx = x; }
    }
    return bx - SW / 2.0f;
}
int main() {
    int bad = 0;
    auto need = [&](bool ok, const char* what) { if (!ok) { std::printf("FAIL: %s\n", what); bad++; } };

    defaults();                      // frost off (no live blur): hexagons only
    Stats still = scan(3, 0, 0);
    int cross = crossings(510.5f, 3);   // y = 510 is a row of cell centres (rows 1.5 r = 51 apart)
    float4 gap = at(SW / 2 + 0.5f, SH / 2 + 0.5f + 17, 3, 0, 0);
    std::printf("hex: mean %.4f centre %.4f corner %.4f | crossings %d along a row of cell centres | between lines a %.3f\n",
                still.mean, still.centre, still.corner, cross, gap.w);
    need(still.mean > 0.01 && still.mean < 0.2, "the hexagons cover a sensible share of the frame");
    need(cross >= 50 && cross <= 80, "about two outlines per cell across 1920 units (Amoebius's radius 34, inset 0.92)");
    need(still.corner < still.centre * 0.6, "the edge fade empties the corners");
    Stats later = scan(9, 0, 0);
    need(std::fabs(later.mean - still.mean) < 1e-6, "still is still");
    need(scan(3, 0, 0, 0).mean < 1e-6, "a hidden window (alpha 0) draws nothing");

    _FrogletBackdropBlurOn = 1;      // the blur is live: frosted glass under the hexagons
    float4 f = at(SW / 2 + 0.5f, SH / 2 + 17.5f, 3, 0, 0);
    std::printf("frost: a %.3f rgb %.3f %.3f %.3f (world red, tint navy, amount 0.5)\n", f.w, f.x, f.y, f.z);
    need(f.w > 0.99f, "the frost is opaque inside the window");
    need(std::fabs(f.x - 0.5f) < 0.06f && f.z < 0.2f, "the frost is the world tinted halfway to navy");
    _Frost = 0; float4 nf = at(SW / 2 + 0.5f, SH / 2 + 17.5f, 3, 0, 0); _Frost = 1;
    need(nf.w < 0.05f, "a panel material (_Frost 0) draws no frost");
    _FrogletBackdropBlurOn = 0;

    // H2: the ring moves out at wavelength / period canvas units per second.
    float r0 = ringRadius(1.0f), r1 = ringRadius(2.0f);
    float moved = r1 - r0; if (moved < 0) moved += 862;
    std::printf("ring: radius %.0f -> %.0f (moved %.0f, want ~%.0f)\n", r0, r1, moved, 862 / 5.2f);
    need(std::fabs(moved - 862 / 5.2f) < 60, "the ring ripples outward at its set speed");
    Stats ring = scan(1, 2, 0);
    need(ring.mean > still.mean, "the ring brightens the lines it passes");

    // H4: the tide lights a band that crosses the rect.
    _FrogletBackdropBlurOn = 1; g_world = (float4){0, 0, 0, 1};
    float tc = 0;                                  // at t = 0 the band is at the middle
    float mid = at(300.5f, SH * 0.5f + 17, tc, 3, 0).z, low = at(300.5f, SH * 0.08f + 17, tc, 3, 0).z;
    float midLater = at(300.5f, SH * 0.5f + 17, 3.5f, 3, 0).z;
    std::printf("tide: blue at middle %.3f, near the bottom %.3f, middle a quarter-period later %.3f\n", mid, low, midLater);
    need(mid > low + 0.05f, "the tide lights its band");
    need(mid > midLater + 0.03f, "the tide band moves");
    _FrogletBackdropBlurOn = 0; g_world = (float4){1, 0, 0, 1};

    // H6: grow on open - nothing at the moment of opening, the full still frame once _GrowSeconds have passed.
    Stats g0 = scan(10.0f, 4, 10.0f), g1 = scan(12.0f, 4, 10.0f);
    std::printf("grow: at open %.4f, %.1f s later %.4f (still %.4f)\n", g0.mean, 2.0f, g1.mean, still.mean);
    need(g0.mean < still.mean * 0.1, "grow starts empty");
    need(std::fabs(g1.mean - still.mean) < still.mean * 0.02, "grow ends on the still frame and holds");

    // The vertex-alpha reveal still crystallises the hexagons in.
    _Reveal = 1;
    Stats half = scan(3, 0, 0, 0.5f);
    need(half.corner < 1e-3 && half.centre > 0.005, "reveal 0.5 is a disc: hexagons at the centre, none at the corners");
    return bad ? 1 : 0;
}
"""


RIM_UNIFORMS = r"""
struct v2f { float4 vertex; float4 color; float2 texcoord; float4 worldPosition; float4 rimUV; };
float4 _Time;
sampler2D _MainTex;
fixed4 _Color, _TextureSampleAdd, _ElementColor, _ChargeColor, _MassColor, _SpaceColor, _TimeColor;
float _Omni, _Speed, _Tail, _BaseLevel, _Linear, _Breathe, _BreatheSeconds;
"""

RIM_MAIN = r"""
#undef min
#undef max
#undef abs
#undef floor
#undef sin
#undef atan2
#undef cos
static float4 C(float r, float g, float b) { return (float4){r, g, b, 1}; }
// Walk the rim of a 600 x 400 panel: the level and colour at 360 points round it.
static void walk(float t, float* level, float4* colour) {
    _Time = (float4){t / 20, t, t * 2, t * 3};
    for (int i = 0; i < 360; i++) {
        float a = (i + 0.5f) / 360.0f * 6.2831853f - 3.14159265f;
        v2f IN;
        IN.texcoord = (float2){0.5f, 0.5f};
        IN.color = (float4){1, 1, 1, 1};
        IN.rimUV = (float4){0.5f + 0.5f * std::cos(a), 0.5f + 0.5f * std::sin(a), 600, 400};
        float4 c = frag(IN);
        level[i] = c.w; colour[i] = c;
    }
}
static int argmax(const float* v) { int k = 0; for (int i = 1; i < 360; i++) if (v[i] > v[k]) k = i; return k; }
int main() {
    _ChargeColor = C(1, 0.827f, 0.302f); _MassColor = C(0.498f, 0.659f, 1); _SpaceColor = C(0.690f, 0.486f, 1);
    _TimeColor = C(0.373f, 0.941f, 0.784f); _Color = C(1, 1, 1); _TextureSampleAdd = (float4){0, 0, 0, 0};
    _ElementColor = _SpaceColor; _Omni = 0; _Speed = 0.12f; _Tail = 0.22f; _BaseLevel = 0.45f;
    _Linear = 0; _Breathe = 0; _BreatheSeconds = 1.8f;
    float l0[360], l2[360]; float4 c0[360], c2[360];
    walk(0.5f, l0, c0); walk(2.5f, l2, c2);
    int h0 = argmax(l0), h2 = argmax(l2);
    float moved = ((h2 - h0 + 360) % 360) / 360.0f;               // turns between t 0.5 and 2.5
    int far = (h0 + 180) % 360;
    float farLevel = l0[far];
    // element mode keeps the element's hue: blue over red, like Space
    bool hueOk = c0[far].z > c0[far].x * 0.9f && c0[far].z > c0[far].y;
    _Omni = 1; _BaseLevel = 0.7f;
    float lo[360]; float4 co[360]; walk(0.5f, lo, co);
    int warm = 0, cool = 0, teal = 0, violet = 0;
    for (int i = 0; i < 360; i++) {
        float4 c = co[i];
        if (c.x > c.z * 1.4f) warm++;                                 // Charge yellow
        if (c.y > c.x * 1.5f && c.y > 0.55f * c.w) teal++;            // Time teal
        if (c.z > c.y * 1.25f && c.x > c.y * 1.1f) violet++;          // Space violet
        if (c.z > c.x * 1.4f && c.y > c.x * 1.1f) cool++;             // Mass blue
    }
    std::printf("rim: head moved %.3f turns (want %.3f) | far level %.3f (base 0.45) | omni warm %d mass %d space %d time %d\n",
                moved, _Speed * 2.0f, farLevel, warm, cool, violet, teal);
    int bad = 0;
    auto need = [&](bool ok, const char* what) { if (!ok) { std::printf("FAIL: %s\n", what); bad++; } };
    need(std::fabs(moved - 0.24f) < 0.03f, "the comet travels at its set speed");
    need(std::fabs(farLevel - 0.45f) < 0.03f, "away from the comet the rim sits at its base light");
    need(hueOk, "element mode stays in the element's colour");
    need(warm > 20 && cool > 20 && violet > 20 && teal > 20, "omni shows all four elements round the rim");
    // Rev 7, the selection: it breathes, and on an underline the comet runs along the line.
    _Omni = 0; _BaseLevel = 0.72f; _Breathe = 0.35f; _BreatheSeconds = 2.0f;
    float lb[360], lq[360]; float4 cb[360], cq[360];
    walk(0.5f, lb, cb); walk(1.5f, lq, cq);                         // sin = 1 (dimmest) and sin = -1 (brightest)
    int quiet = (argmax(lb) + 180) % 360;
    float ratio = lb[quiet] / std::fmax(lq[quiet], 1e-4f);
    _Breathe = 0; _Linear = 1; _Speed = 0.25f;
    auto headX = [&](float t) { _Time = (float4){t / 20, t, t * 2, t * 3}; int k = 0; float best = -1;
        for (int i = 0; i < 200; i++) { v2f IN; IN.texcoord = (float2){0.5f, 0.5f}; IN.color = (float4){1, 1, 1, 1};
            IN.rimUV = (float4){(i + 0.5f) / 200, 0.5f, 400, 4}; float l = frag(IN).w; if (l > best) { best = l; k = i; } }
        return (k + 0.5f) / 200; };
    float u1 = headX(0.4f), u2 = headX(1.6f);
    std::printf("selection: breath dims the rim to %.2f of its peak (want ~0.79: alpha follows 60%% of the breath) | underline head %.3f -> %.3f (want +0.300)\n",
                ratio, u1, u2);
    need(ratio > 0.72f && ratio < 0.86f, "the selection rim breathes");
    need(std::fabs((u2 - u1) - 0.30f) < 0.02f, "on an underline the comet runs along the line at its speed");
    return bad ? 1 : 0;
}
"""


PRESENCE_UNIFORMS = r"""
struct v2f { float4 vertex; float4 color; float2 texcoord; float4 worldPosition; };
float4 _Time;
fixed4 _Color, _SpaceColor, _Bond0, _Bond1, _Bond2;
float _GlowAlpha, _InnerRadius, _InnerAlpha, _OuterRadius, _OuterAlpha, _RingWidth, _Soft, _Dashes, _SpinSeconds;
float _Online, _DotSize, _MemberRadius, _BondWidth, _BondPeriod, _BondSpeed;
float4 _BondAngles;
"""

PRESENCE_MAIN = r"""
#undef min
#undef max
#undef abs
#undef floor
#undef sin
#undef atan2
#undef cos
static float4 C(float r, float g, float b, float a) { return (float4){r, g, b, a}; }
static void defaults() {
    _Color = C(1, 1, 1, 1); _SpaceColor = C(0.690f, 0.486f, 1, 1);
    _Bond0 = _Bond1 = _Bond2 = C(0, 0, 0, 0);
    _GlowAlpha = 0.32f; _InnerRadius = 0.66f; _InnerAlpha = 0.34f; _OuterRadius = 0.92f; _OuterAlpha = 0.55f;
    _RingWidth = 0.012f; _Soft = 0.03f; _Dashes = 28; _SpinSeconds = 40; _Online = 0; _DotSize = 0.055f;
    _MemberRadius = 0.66f; _BondAngles = C(135, 45, 270, 0); _BondWidth = 0.022f; _BondPeriod = 0.16f; _BondSpeed = 0.22f;
}
static float4 at(float x, float y, float t) {   // x, y in -1..1 over the orb
    _Time = (float4){t / 20, t, t * 2, t * 3};
    v2f IN; IN.texcoord = (float2){x * 0.5f + 0.5f, y * 0.5f + 0.5f}; IN.color = (float4){1, 1, 1, 1};
    return frag(IN);
}
// Walk the orbit: how many separate bright dots it carries at time t, and the angle (turns) of the first one.
static int dots(float t, float* first) {
    int runs = 0; bool on = false; bool wrapStart = false; *first = -1;
    for (int i = 0; i < 1440; i++) {
        float a = i / 1440.0f * 6.2831853f;
        float4 c = at(_OuterRadius * std::cos(a), _OuterRadius * std::sin(a), t);
        bool lit = c.w > 0.3f && c.y > 0.7f;
        if (lit && !on) { runs++; if (*first < 0) *first = i / 1440.0f; if (i == 0) wrapStart = true; }
        on = lit;
    }
    if (on && wrapStart && runs > 1) runs--;   // a dot straddling angle 0 counted twice
    return runs;
}
// The angle (turns) of a single dot on the orbit, as the brightness-weighted circular mean.
static float dotAngle(float t) {
    float sx = 0, sy = 0;
    for (int i = 0; i < 1440; i++) {
        float a = i / 1440.0f * 6.2831853f;
        float4 c = at(_OuterRadius * std::cos(a), _OuterRadius * std::sin(a), t);
        float w = (c.w > 0.3f && c.y > 0.7f) ? c.w : 0;
        sx += w * std::cos(a); sy += w * std::sin(a);
    }
    float turns = std::atan2(sy, sx) / 6.2831853f;
    return turns < 0 ? turns + 1 : turns;
}
// How red the bond along `deg` is at radius 0.45 (between your avatar and the member's), summed over the dash cycle.
static float redness(float deg) {
    float a = deg * 0.01745329f, red = 0;
    for (int k = 0; k < 16; k++) {
        float4 c = at(0.45f * std::cos(a), 0.45f * std::sin(a), k * 0.04f);
        red += c.w * (c.x - c.z);
    }
    return red;
}
// How much the orb covers along `deg` at radius 0.45, summed over the dash cycle (glow alone where there is no bond).
static float cover(float deg) {
    float a = deg * 0.01745329f, sum = 0;
    for (int k = 0; k < 16; k++) sum += at(0.45f * std::cos(a), 0.45f * std::sin(a), k * 0.04f).w;
    return sum;
}
int main() {
    defaults();
    float f0, f1, fs;
    int none = dots(0, &f0);
    _Online = 3; int three = dots(0, &f0); int threeLater = dots(10, &f1);
    _Online = 8; int eight = dots(0, &fs);
    _Online = 1;
    float moved = dotAngle(10) - dotAngle(0); if (moved < 0) moved += 1;
    _Online = 0;
    float bare = redness(135);
    float emptyCover = cover(45), offBond = cover(0);
    _Bond0 = C(0.9f, 0.15f, 0.3f, 1);
    float lit = redness(135), other = redness(45);
    float4 centre = at(0, 0, 0), corner = at(0.98f, 0.98f, 0);
    // The bond's dashes flow outward: the brightest place along one dash period moves out with time.
    auto peak = [](float t) { int k = 0; float best = -1;
        for (int i = 0; i < 160; i++) { float b = PresenceBond(0, i * 0.001f, t, 0.022f, 0.03f, 0.16f, 0.22f); if (b > best) { best = b; k = i; } }
        return k * 0.001f; };
    float shift = peak(0.1f) - peak(0); if (shift < 0) shift += 0.16f;
    std::printf("orb: dots %d / %d / %d (online 0 / 3 / 8) | turned %.3f turns in 10 s (want %.3f) | bond red %.2f, empty %.2f, "
                "other seat %.2f | centre a %.2f corner a %.3f | dash moved %.4f (want %.4f)\n",
                none, three, eight, moved, 10.0f / 40.0f, lit, bare, other, centre.w, corner.w, shift, 0.1f * 0.22f);
    int bad = 0;
    auto need = [&](bool ok, const char* what) { if (!ok) { std::printf("FAIL: %s\n", what); bad++; } };
    need(none == 0 && three == 3 && eight == 8, "the orbit carries exactly one dot per player online");
    need(threeLater == 3 && std::fabs(moved - 0.25f) < 0.01f, "the dots turn with the orbit at its set speed");
    need(lit > 2.0f && bare < 0.05f, "a seated member's bond lights in their colour; an empty seat has none");
    need(other < 0.05f, "a bond lights only towards its own seat");
    need(std::fabs(emptyCover - offBond) < 0.05f, "an empty seat's bond draws nothing at all");
    need(centre.w > corner.w * 4 && corner.w < 0.02f, "the glow sits in the middle and the corners stay clear");
    need(std::fabs(shift - 0.022f) < 0.003f, "the bond's dashes flow outward at their set speed");
    // The shipped orb since 2026-10-24: no orbiting dots, a still ring, solid still bonds.
    defaults(); _SpinSeconds = 0; _DotSize = 0; _BondPeriod = 0; _BondSpeed = 0; _Online = 3;
    _Bond0 = C(0.9f, 0.15f, 0.3f, 1);
    float g0, stillDots = dots(0, &g0);
    float4 r0 = at(0.9f, 0.1f, 0), r1 = at(0.9f, 0.1f, 10);
    float b0 = PresenceBond(0, 0.05f, 0, 0.022f, 0.03f, 0, 0), b1 = PresenceBond(0, 0.05f, 7.3f, 0.022f, 0.03f, 0, 0);
    std::printf("still orb: dots %.0f | ring a %.4f -> %.4f | bond %.3f -> %.3f\n", stillDots, r0.w, r1.w, b0, b1);
    need(stillDots == 0, "dot size 0 draws no orbiting dots");
    need(r0.w == r1.w, "spin 0 holds the ring still");
    need(b0 > 0.5f && b0 == b1, "bond period 0 draws a solid, still bond");
    return bad ? 1 : 0;
}
"""

BOND_UNIFORMS = r"""
struct v2f { float4 vertex; float4 color; float2 texcoord; float4 worldPosition; float4 rimUV; };
float4 _Time;
float4 _MainTex_ST;
fixed4 _Color;
float _Width, _Soft, _Period, _Speed;
"""

BOND_MAIN = r"""
#undef min
#undef max
#undef abs
#undef floor
#undef sin
#undef atan2
#undef cos
int main() {
    _Color = (float4){1, 1, 1, 1}; _Width = 1.4f; _Soft = 1; _Period = 18; _Speed = 26; _Time = (float4){0, 0, 0, 0};
    float core = 0, edge = 0, hueOk = 1;
    for (int i = 0; i < 120; i++) {   // a 120 x 14 bond, tinted Time teal
        v2f IN; IN.color = (float4){0.373f, 0.941f, 0.784f, 1}; IN.texcoord = (float2){0, 0};
        IN.rimUV = (float4){(i + 0.5f) / 120, 0.5f, 120, 14};
        float4 c = frag(IN); core += c.w;
        if (c.w > 0.05f && !(c.y > c.x && c.y > c.z)) hueOk = 0;
        IN.rimUV.y = 0.0f; edge += frag(IN).w;
    }
    std::printf("bond: core %.2f edge %.3f hue %s\n", core / 120, edge / 120, hueOk > 0 ? "kept" : "lost");
    int bad = 0;
    auto need = [&](bool ok, const char* what) { if (!ok) { std::printf("FAIL: %s\n", what); bad++; } };
    need(core / 120 > 0.4f, "the bond's line is lit along its length");
    need(edge / 120 < 0.01f, "the bond stays on its line (its rect's edge is clear)");
    need(hueOk > 0, "the bond keeps its member's colour");
    return bad ? 1 : 0;
}
"""


def extract_frag(shader_text):
    m = re.search(r"fixed4 frag\(v2f IN\) : SV_Target\n(\s*)\{", shader_text)
    assert m, "frag() not found in Lattice.shader"
    i, depth = m.end() - 1, 0
    for j in range(i, len(shader_text)):
        if shader_text[j] == "{":
            depth += 1
        elif shader_text[j] == "}":
            depth -= 1
            if depth == 0:
                return "fixed4 frag(v2f IN)\n" + shader_text[i:j + 1]
    raise AssertionError("unbalanced frag() in Lattice.shader")


def translate(src):
    for pat, rep in SUBS:
        src = re.sub(pat, rep, src)
    return src


def build(work):
    inc = translate(open(CGINC, encoding="utf-8").read())
    frag = translate(extract_frag(open(SHADER, encoding="utf-8").read()))
    cpp = SHIM + inc + UNIFORMS + frag + MAIN
    src = os.path.join(work, "lattice.cpp")
    open(src, "w").write(cpp)
    exe = os.path.join(work, "lattice")
    r = subprocess.run(["clang++", "-std=c++17", "-O2", "-Wall", "-Werror", "-Wno-unused-function", src, "-o", exe],
                       capture_output=True, text=True)
    if r.returncode:
        sys.stderr.write(r.stderr)
        raise SystemExit("FAIL: the lattice shader maths does not compile")
    return exe


def build_rim(work):
    inc = translate(open(RIM_CGINC, encoding="utf-8").read())
    frag = translate(extract_frag(open(RIM_SHADER, encoding="utf-8").read()))
    src = os.path.join(work, "rim.cpp")
    open(src, "w").write(SHIM + inc + RIM_UNIFORMS + frag + RIM_MAIN)
    exe = os.path.join(work, "rim")
    r = subprocess.run(["clang++", "-std=c++17", "-O2", "-Wall", "-Werror", "-Wno-unused-function", src, "-o", exe],
                       capture_output=True, text=True)
    if r.returncode:
        sys.stderr.write(r.stderr)
        raise SystemExit("FAIL: the rim shader maths does not compile")
    r = subprocess.run([exe], capture_output=True, text=True)
    sys.stdout.write(r.stdout)
    if r.returncode:
        raise SystemExit("FAIL: the rim dials do not drive as asserted")


def build_presence(work):
    inc = translate(open(PRESENCE_CGINC, encoding="utf-8").read())
    for name, shader, uniforms, main_ in (("presence", PRESENCE_SHADER, PRESENCE_UNIFORMS, PRESENCE_MAIN),
                                          ("bond", BOND_SHADER, BOND_UNIFORMS, BOND_MAIN)):
        frag = translate(extract_frag(open(shader, encoding="utf-8").read()))
        src = os.path.join(work, f"{name}.cpp")
        open(src, "w").write(SHIM + inc + uniforms + frag + main_)
        exe = os.path.join(work, name)
        r = subprocess.run(["clang++", "-std=c++17", "-O2", "-Wall", "-Werror", "-Wno-unused-function", src, "-o", exe],
                           capture_output=True, text=True)
        if r.returncode:
            sys.stderr.write(r.stderr)
            raise SystemExit(f"FAIL: the {name} shader maths does not compile")
        r = subprocess.run([exe], capture_output=True, text=True)
        sys.stdout.write(r.stdout)
        if r.returncode:
            raise SystemExit(f"FAIL: the {name} dials do not drive as asserted")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--out", default=None)
    args = ap.parse_args()
    work = tempfile.mkdtemp(prefix="lattice_")
    exe = build(work)
    out = None if args.check else (args.out or work)
    if out:
        os.makedirs(out, exist_ok=True)
    r = subprocess.run([exe] + ([out] if out else []), capture_output=True, text=True)
    sys.stdout.write(r.stdout)
    if r.returncode:
        raise SystemExit("FAIL: the lattice dials do not drive as asserted")
    build_rim(work)
    build_presence(work)
    print("OK: the lattice, the rim, the presence orb and the bonds compile and their dials drive")


if __name__ == "__main__":
    main()
