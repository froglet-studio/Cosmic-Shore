#!/usr/bin/env python3
"""
Compile the SHIPPED style-B UI shader maths with clang and render it (asset-surgery §4.5c): each real include and
the real fragment body of its shader, through a short listed set of substitutions, into a C++ harness that renders
frames to PNG and asserts the dials drive.

    python3 Tools/Build/lattice_preview.py                 # compile, assert, render PNGs to the scratch dir
    python3 Tools/Build/lattice_preview.py --out DIR       # render into DIR
    python3 Tools/Build/lattice_preview.py --check         # compile + assert only (no PNGs)

What it proves: every include and fragment compiles (-Wall -Werror). The crystal lattice (Lattice.shader) was retired for
the sea on 2026-10-25 (Docs/MENU_REDESIGN_B.md section 70) and its harness with it. The rim (ElementRim.shader) and the
presence orb and party bonds (Presence.shader, Bond.shader on UIPresence.cginc, section 45.4): the orb draws exactly _Online dots
on its orbit and turns them, a bond lights only in its member's colour and only for a seated member, the bond
dashes flow outward, and the molecule's bond stays on its line. The sea lines (SeaLines.shader on UISeaLines.cginc,
section 67) are thin, move slowly, loop without a seam, hold still at 0 seconds and fade at the window's top and
bottom. What it does not: Unity's own shader compile (no editor here) - the UI plumbing
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
RIM_CGINC = os.path.join(SHADER_DIR, "UIElementRim.cginc")
RIM_SHADER = os.path.join(SHADER_DIR, "ElementRim.shader")
PRESENCE_CGINC = os.path.join(SHADER_DIR, "UIPresence.cginc")
PRESENCE_SHADER = os.path.join(SHADER_DIR, "Presence.shader")
BOND_SHADER = os.path.join(SHADER_DIR, "Bond.shader")
SEA_CGINC = os.path.join(SHADER_DIR, "UISeaLines.cginc")
SEA_SHADER = os.path.join(SHADER_DIR, "SeaLines.shader")
DRIFT_CGINC = os.path.join(SHADER_DIR, "UICardDrift.cginc")
HULL_CGINC = os.path.join(SHADER_DIR, "UIHullLit.cginc")

# The whole translation: HLSL out-params to C++ references, and the vector constructor spellings.
SUBS = [(r"\bout float\b", "float&"),
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
static inline float3 saturate(float3 v) { return (float3){saturate(v.x), saturate(v.y), saturate(v.z)}; }
static inline float hmin(float a, float b) { return a < b ? a : b; }
static inline float hmax(float a, float b) { return a > b ? a : b; }
static inline float habs(float a) { return a < 0 ? -a : a; }
static inline float step(float e, float x) { return x >= e ? 1.0f : 0.0f; }
static inline float smoothstep(float a, float b, float x) { float t = saturate((x - a) / (b - a)); return t * t * (3 - 2 * t); }
static inline float dot(float2 a, float2 b) { return a.x * b.x + a.y * b.y; }
static inline float length(float2 a) { return std::sqrt(dot(a, a)); }
static inline float floor_(float x) { return std::floor(x); }
static inline float sin_(float x) { return std::sin(x); }
static inline float3 lerp(float3 a, float3 b, float t) { return a + (b - a) * t; }
static inline float lerp(float a, float b, float t) { return a + (b - a) * t; }
static inline float atan2_(float y, float x) { return std::atan2(y, x); }
static inline float cos_(float x) { return std::cos(x); }
static inline float sqrt_(float x) { return std::sqrt(x); }
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
#define sqrt sqrt_
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


SEA_UNIFORMS = r"""
struct v2f { float4 vertex; float4 color; float2 texcoord; float4 worldPosition; };
float4 _Time;
fixed4 _Color, _LineColor, _LineColorMid, _LineColorEnd, _WashTop, _WashBottom, _GlowColor;
float _Spacing, _Loop, _Amplitude, _FarAlpha, _FarWidth, _FarSeconds, _MidAlpha, _MidWidth, _MidSeconds;
float _NearAlpha, _NearWidth, _NearSeconds, _FadeTop, _FadeBottom;
"""

SEA_MAIN = r"""
#undef min
#undef max
#undef abs
#undef floor
#undef sin
#undef atan2
#undef cos
#undef sqrt
static void defaults() {   // Sea_Window.mat (author_ui_lattice.py SEA)
    _Color = (float4){1, 1, 1, 1};
    _LineColor = (float4){0.286f, 0.722f, 1, 1}; _LineColorMid = (float4){0.498f, 0.596f, 1, 1}; _LineColorEnd = (float4){0.612f, 0.549f, 1, 1};
    _Spacing = 46; _Loop = 480; _Amplitude = 9;
    _FarAlpha = 0.2f; _FarWidth = 0.9f; _FarSeconds = 90; _MidAlpha = 0.3f; _MidWidth = 1.1f; _MidSeconds = 68;
    _NearAlpha = 0.42f; _NearWidth = 1.5f; _NearSeconds = 45; _FadeTop = 0.2f; _FadeBottom = 0.2f;
    _WashTop = (float4){0.031f, 0.086f, 0.329f, 0.75f}; _WashBottom = (float4){0, 0.008f, 0.055f, 0}; _GlowColor = (float4){0.239f, 0.549f, 1, 0.16f};
}
// The line-only checks run without the wash, so coverage measures the lines alone.
static void noWash() { _WashTop.w = 0; _WashBottom.w = 0; _GlowColor.w = 0; }
// A W x H render of a 1920 x 1080 window at time t: mean alpha, optional PPM over the 95 % veil's near-black, and
// the per-pixel alphas for comparing frames.
static double render(int W, int H, float t, float vertexAlpha, const char* ppm, float* keep) {
    _Time = (float4){t / 20, t, t * 2, t * 3};
    FILE* f = ppm ? std::fopen(ppm, "wb") : nullptr;
    if (f) std::fprintf(f, "P6 %d %d 255\n", W, H);
    double sum = 0;
    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) {
            v2f IN;
            float u = (x + 0.5f) / W, v = 1 - (y + 0.5f) / H;
            IN.texcoord = (float2){u, v};
            IN.worldPosition = (float4){u * 1920, v * 1080, 0, 1};
            IN.color = (float4){1, 1, 1, vertexAlpha};
            float4 c = frag(IN);
            sum += c.w;
            if (keep) keep[y * W + x] = c.w;
            if (f) {
                float bg[3] = {0.0f, 0.004f, 0.04f};
                unsigned char px[3];
                float rgb[3] = {c.x, c.y, c.z};
                for (int k = 0; k < 3; k++) {
                    float o = bg[k] * (1 - c.w) + rgb[k] * c.w;
                    px[k] = (unsigned char)(o < 0 ? 0 : (o > 1 ? 255 : o * 255));
                }
                std::fwrite(px, 1, 3, f);
            }
        }
    if (f) std::fclose(f);
    return sum / ((double)W * H);
}
static double diff(const float* a, const float* b, int n) {
    double d = 0;
    for (int i = 0; i < n; i++) d += std::fabs(a[i] - b[i]);
    return d / n;
}
int main(int argc, char** argv) {
    const char* out = argc > 1 ? argv[1] : nullptr;
    char path[1024];
    auto P = [&](const char* name) -> const char* { if (!out) return nullptr; std::snprintf(path, sizeof path, "%s/%s", out, name); return path; };
    // Full size: the lines are about one canvas unit wide, so a smaller render would miss them.
    const int W = 1920, H = 1080, N = W * H;
    static float f0[N], f1[N], fLoop[N], fNearLoop[N], tmp[N];
    defaults();
    render(W, H, 10.0f, 1.0f, P("sea_t10.ppm"), nullptr);
    render(W, H, 25.0f, 1.0f, P("sea_t25.ppm"), nullptr);
    // The wash: strong at the top, clear at the bottom, nothing where the window is hidden.
    static float washed[N];
    render(W, H, 10.0f, 1.0f, nullptr, washed);
    double washTop = 0, washBottom = 0;
    for (int x = 0; x < W; x++) { washTop += washed[2 * W + x]; washBottom += washed[(H - 3) * W + x]; }
    washTop /= W; washBottom /= W;
    defaults(); noWash();
    double cover = render(W, H, 10.0f, 1.0f, nullptr, f0);
    render(W, H, 10.0f + 1.0f / 60.0f, 1.0f, nullptr, f1);
    // One loop of every layer at once: 90, 68 and 45 s share no small common period, so hold the far and middle
    // layers still and run the near one exactly one loop.
    defaults(); noWash(); _FarSeconds = 0; _MidSeconds = 0;
    render(W, H, 10.0f, 1.0f, nullptr, fLoop);
    render(W, H, 10.0f + _NearSeconds, 1.0f, nullptr, fNearLoop);
    double loopDiff = diff(fLoop, fNearLoop, N);
    // Still: every layer at 0 seconds draws the same frame at any time.
    defaults(); noWash(); _FarSeconds = 0; _MidSeconds = 0; _NearSeconds = 0;
    render(W, H, 3.0f, 1.0f, nullptr, tmp);
    render(W, H, 30.0f, 1.0f, nullptr, fLoop);
    double stillDiff = diff(tmp, fLoop, N);
    defaults();
    double hidden = render(W, H, 10.0f, 0.0f, nullptr, nullptr);
    noWash();
    double half = render(W, H, 10.0f, 0.5f, nullptr, nullptr);
    // The edges: the top and bottom rows are faded out, the middle rows carry lines.
    double top = 0, mid = 0;
    for (int x = 0; x < W; x++) top += f0[x] + f0[(H - 1) * W + x];
    for (int y = H / 2 - 100; y < H / 2 + 100; y++) for (int x = 0; x < W; x++) mid += f0[y * W + x];
    mid /= 200.0 * W; top /= 2.0 * W;
    double frame = diff(f0, f1, N);
    std::printf("sea: cover %.4f | one frame apart %.5f | near loop %.6f | still %.6f | hidden %.6f | half %.4f | top %.5f mid %.4f\n",
                cover, frame, loopDiff, stillDiff, hidden, half, top, mid);
    int bad = 0;
    auto need = [&](bool ok, const char* what) { if (!ok) { std::printf("FAIL: %s\n", what); bad++; } };
    need(cover > 0.003 && cover < 0.02, "the sea lines are thin, and brighter than rev 12's (a small share of the window)");
    need(frame > 1e-6, "the lines move");
    need(frame < cover * 0.5, "and slowly: from one frame to the next a line moves a fraction of its width");
    need(loopDiff < 2e-3, "a layer that slides one whole loop lands back where it started (no seam)");
    need(stillDiff < 1e-6, "0 seconds holds every layer still");
    need(hidden < 1e-7, "a hidden window draws nothing");
    need(std::fabs(half - cover * 0.5) < cover * 0.02, "the vertex alpha fades the lines");
    need(top < 1e-6 && mid > cover, "the top and bottom fade out, the middle carries the lines");
    std::printf("sea wash: top %.3f bottom %.3f\n", washTop, washBottom);
    need(washTop > 0.5 && washBottom < 0.02, "the wash is deep at the top and clear at the bottom (Rev 13)");
    return bad ? 1 : 0;
}
"""


DRIFT_MAIN = r"""
#undef min
#undef max
#undef abs
#undef floor
#undef sin
#undef atan2
#undef cos
#undef sqrt
int main() {
    // Card_ArtDrift.mat: reach (5, 3), 40 s. The path stays inside the reach, comes back after one loop sideways,
    // moves a little between frames, and holds still at 0 seconds.
    float2 reach = (float2){5.0f, 3.0f};
    float mx = 0, my = 0, step_ = 0;
    float2 prev = CardDriftOffset(0.0f, 40.0f, reach);
    for (int i = 1; i <= 40 * 60 * 3; i++) {
        float2 o = CardDriftOffset(i / 60.0f, 40.0f, reach);
        mx = std::fmax(mx, std::fabs(o.x)); my = std::fmax(my, std::fabs(o.y));
        step_ = std::fmax(step_, std::fabs(o.x - prev.x) + std::fabs(o.y - prev.y));
        prev = o;
    }
    float2 a = CardDriftOffset(7.0f, 40.0f, reach), b = CardDriftOffset(47.0f, 40.0f, reach);
    float2 still = CardDriftOffset(13.0f, 0.0f, reach);
    std::printf("card drift: reach x %.3f y %.3f | biggest frame step %.4f | loop x %.5f | still %.5f\n", mx, my, step_,
                std::fabs(a.x - b.x), std::fabs(still.x) + std::fabs(still.y));
    int ok = mx <= 5.0001f && my <= 3.0001f && mx > 4.9f && my > 2.9f && step_ < 0.05f && std::fabs(a.x - b.x) < 1e-3f
             && still.x == 0 && still.y == 0;
    if (!ok) { std::printf("FAIL: the card drift leaves its reach, jumps, does not loop or will not hold still\n"); return 1; }
    return 0;
}
"""


HULL_MAIN = r"""
#undef min
#undef max
#undef abs
#undef floor
#undef sin
#undef atan2
#undef cos
#undef sqrt
static float lum(float3 c) { return 0.2126f * c.x + 0.7152f * c.y + 0.0722f * c.z; }
int main() {
    // Hull_Lit.mat: gain 1.3, lift (0.14, 0.17, 0.30), white edge at 0.6. The card body is about (0.0, 0.016, 0.12).
    float3 lift = (float3){0.14f, 0.17f, 0.30f}, white = (float3){1, 1, 1};
    float3 black = HullLit((float3){0, 0, 0}, 1, 1, 1.3f, lift, white, 0.6f);
    float3 trim = HullLit((float3){0.9f, 0.92f, 1.0f}, 1, 1, 1.3f, lift, white, 0.6f);
    float3 edge = HullLit((float3){0, 0, 0}, 1, 0, 1.3f, lift, white, 0.6f);
    float body = lum((float3){0.0f, 0.016f, 0.12f});
    std::printf("hull lit: black -> %.3f (card %.3f) | trim -> %.3f | edge -> %.3f\n", lum(black), body, lum(trim), lum(edge));
    int ok = lum(black) > body + 0.1f && lum(trim) > 0.95f && lum(edge) > lum(black) + 0.3f && lum(black) < 0.35f;
    if (!ok) { std::printf("FAIL: the lit hull does not stand off the card, or its darks wash out\n"); return 1; }
    return 0;
}
"""


def build_hull(work):
    inc = translate(open(HULL_CGINC, encoding="utf-8").read())
    src = os.path.join(work, "hull.cpp")
    open(src, "w").write(SHIM + inc + HULL_MAIN)
    exe = os.path.join(work, "hull")
    r = subprocess.run(["clang++", "-std=c++17", "-O2", "-Wall", "-Werror", "-Wno-unused-function", src, "-o", exe],
                       capture_output=True, text=True)
    if r.returncode:
        sys.stderr.write(r.stderr)
        raise SystemExit("FAIL: the hull light maths does not compile")
    r = subprocess.run([exe], capture_output=True, text=True)
    sys.stdout.write(r.stdout)
    if r.returncode:
        raise SystemExit("FAIL: the lit hull does not read on the card")


def build_drift(work):
    inc = translate(open(DRIFT_CGINC, encoding="utf-8").read())
    src = os.path.join(work, "drift.cpp")
    open(src, "w").write(SHIM + inc + DRIFT_MAIN)
    exe = os.path.join(work, "drift")
    r = subprocess.run(["clang++", "-std=c++17", "-O2", "-Wall", "-Werror", "-Wno-unused-function", src, "-o", exe],
                       capture_output=True, text=True)
    if r.returncode:
        sys.stderr.write(r.stderr)
        raise SystemExit("FAIL: the card drift maths does not compile")
    r = subprocess.run([exe], capture_output=True, text=True)
    sys.stdout.write(r.stdout)
    if r.returncode:
        raise SystemExit("FAIL: the card drift does not stay in its reach")


def build_sea(work, out):
    inc = translate(open(SEA_CGINC, encoding="utf-8").read())
    frag = translate(extract_frag(open(SEA_SHADER, encoding="utf-8").read()))
    src = os.path.join(work, "sea.cpp")
    open(src, "w").write(SHIM + inc + SEA_UNIFORMS + frag + SEA_MAIN)
    exe = os.path.join(work, "sea")
    r = subprocess.run(["clang++", "-std=c++17", "-O2", "-Wall", "-Werror", "-Wno-unused-function", src, "-o", exe],
                       capture_output=True, text=True)
    if r.returncode:
        sys.stderr.write(r.stderr)
        raise SystemExit("FAIL: the sea lines shader maths does not compile")
    r = subprocess.run([exe] + ([out] if out else []), capture_output=True, text=True)
    sys.stdout.write(r.stdout)
    if r.returncode:
        raise SystemExit("FAIL: the sea lines dials do not drive as asserted")
    if out:
        from PIL import Image
        for name in ("sea_t10", "sea_t25"):
            ppm = os.path.join(out, name + ".ppm")
            Image.open(ppm).save(os.path.join(out, name + ".png"))
            os.remove(ppm)


def extract_frag(shader_text):
    m = re.search(r"fixed4 frag\(v2f IN\) : SV_Target\n(\s*)\{", shader_text)
    assert m, "frag() not found in the shader"
    i, depth = m.end() - 1, 0
    for j in range(i, len(shader_text)):
        if shader_text[j] == "{":
            depth += 1
        elif shader_text[j] == "}":
            depth -= 1
            if depth == 0:
                return "fixed4 frag(v2f IN)\n" + shader_text[i:j + 1]
    raise AssertionError("unbalanced frag() in the shader")


def translate(src):
    for pat, rep in SUBS:
        src = re.sub(pat, rep, src)
    return src


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
    out = None if args.check else (args.out or work)
    if out:
        os.makedirs(out, exist_ok=True)
    build_rim(work)
    build_presence(work)
    build_sea(work, out)
    build_drift(work)
    build_hull(work)
    print("OK: the rim, the presence orb, the bonds, the sea lines, the card drift and the lit hull compile and their dials drive")


if __name__ == "__main__":
    main()
