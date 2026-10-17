#!/usr/bin/env python3
"""
Compile the SHIPPED lattice shader maths with clang and render it (asset-surgery §4.5c): the real
Assets/_Graphics/Materials/Shaders/UI/UILattice.cginc and the real fragment body of Lattice.shader, through a
short listed set of substitutions, into a C++ harness that renders frames to PNG and asserts the dials drive.

    python3 Tools/Build/lattice_preview.py                 # compile, assert, render PNGs to the scratch dir
    python3 Tools/Build/lattice_preview.py --out DIR       # render into DIR
    python3 Tools/Build/lattice_preview.py --check         # compile + assert only (no PNGs)

What it proves: the include and the fragment compile (-Wall -Werror); pulses appear only when lines carry them;
reveal 0 shows nothing, reveal 1 equals the plain draw, reveal 0.5 shows a disc; the far layer adds coverage; the
edge fade empties the corners. The rim (ElementRim.shader) and the presence orb and party bonds (Presence.shader,
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
static inline float sin_(float x) { return std::sin(x); }
static inline float3 lerp(float3 a, float3 b, float t) { return a + (b - a) * t; }
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
"""

UNIFORMS = r"""
struct v2f { float4 vertex; float4 color; float2 texcoord; float4 worldPosition; };
float4 _Time;
fixed4 _Color, _LineColor, _ChargeColor, _MassColor, _SpaceColor, _TimeColor;
float _LineWidth, _NodeSize, _NearSpacing, _NearAlpha, _NearDrift, _FarSpacing, _FarAlpha, _FarDrift;
float _PulseDensity, _PulseSpeed, _PulseRepeat, _PulseLength, _TwinkleDensity, _FadeStart, _Reveal;
"""

MAIN = r"""
#undef min
#undef max
#undef abs
#undef floor
#undef sin
#undef atan2
#undef cos
static float4 col(float r, float g, float b) { return (float4){r, g, b, 1}; }
static void defaults() {
    _Color = col(1, 1, 1); _LineColor = col(0.263f, 0.298f, 0.537f);
    _ChargeColor = col(1, 0.827f, 0.302f); _MassColor = col(0.498f, 0.659f, 1);
    _SpaceColor = col(0.690f, 0.486f, 1); _TimeColor = col(0.373f, 0.941f, 0.784f);
    _LineWidth = 1.1f; _NodeSize = 2.4f; _NearSpacing = 130; _NearAlpha = 0.42f; _NearDrift = 55;
    _FarSpacing = 72; _FarAlpha = 0.2f; _FarDrift = 90; _PulseDensity = 0.22f; _PulseSpeed = 240;
    _PulseRepeat = 1400; _PulseLength = 120; _TwinkleDensity = 0.05f; _FadeStart = 0.55f; _Reveal = 1;
}
// Render W x H pixels of a 1920 x 1080 canvas; returns stats and optionally writes a PPM over the menu's deep blue.
struct Stats { double coverage; double elementPixels; double cornerAlpha; double centreAlpha; };
static Stats render(int W, int H, float t, float vertexAlpha, const char* ppm) {
    _Time = (float4){t / 20, t, t * 2, t * 3};
    FILE* f = ppm ? std::fopen(ppm, "wb") : nullptr;
    if (f) std::fprintf(f, "P6 %d %d 255\n", W, H);
    Stats s = {0, 0, 0, 0};
    int corner = 0, centre = 0;
    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) {
            v2f IN;
            float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
            IN.texcoord = (float2){u, v};
            IN.worldPosition = (float4){u * 1920, v * 1080, 0, 1};
            IN.color = (float4){1, 1, 1, vertexAlpha};
            float4 c = frag(IN);
            s.coverage += c.w;
            float sat = std::fmax(c.x, std::fmax(c.y, c.z)) - std::fmin(c.x, std::fmin(c.y, c.z));
            if (c.w > 0.05f && sat > 0.35f) s.elementPixels += 1;
            float du = u - 0.5f, dv = v - 0.5f;
            if (du * du + dv * dv > 0.36f) { s.cornerAlpha += c.w; corner++; }
            if (du * du + dv * dv < 0.01f) { s.centreAlpha += c.w; centre++; }
            if (f) {
                float bg[3] = {0.008f, 0.016f, 0.10f};
                float rgb[3] = {c.x, c.y, c.z};
                unsigned char px[3];
                for (int k = 0; k < 3; k++) {
                    float o = bg[k] * (1 - c.w) + rgb[k] * c.w;
                    px[k] = (unsigned char)(o < 0 ? 0 : (o > 1 ? 255 : o * 255));
                }
                std::fwrite(px, 1, 3, f);
            }
        }
    if (f) std::fclose(f);
    s.coverage /= (double)W * H; s.elementPixels /= (double)W * H;
    s.cornerAlpha /= corner ? corner : 1; s.centreAlpha /= centre ? centre : 1;
    return s;
}
int main(int argc, char** argv) {
    const char* out = argc > 1 ? argv[1] : nullptr;
    char path[1024];
    auto P = [&](const char* name) -> const char* { if (!out) return nullptr; std::snprintf(path, sizeof path, "%s/%s", out, name); return path; };
    defaults();
    Stats a = render(480, 270, 3.0f, 1.0f, P("lattice_t3.ppm"));
    Stats b = render(480, 270, 9.0f, 1.0f, P("lattice_t9.ppm"));
    Stats half = render(480, 270, 3.0f, 0.5f, P("lattice_reveal_half.ppm"));
    Stats none = render(480, 270, 3.0f, 0.0f, nullptr);
    _PulseDensity = 0; _TwinkleDensity = 0;
    Stats calm = render(480, 270, 3.0f, 1.0f, nullptr);
    defaults(); _FarAlpha = 0;
    Stats nofar = render(480, 270, 3.0f, 1.0f, nullptr);
    defaults(); _Reveal = 0;
    Stats plain = render(480, 270, 3.0f, 1.0f, nullptr);
    std::printf("full %.4f el %.5f corner %.4f centre %.4f | t9 el %.5f | half %.4f centre %.4f corner %.4f | none %.5f | "
                "calm el %.5f | nofar %.4f | plain %.4f\n", a.coverage, a.elementPixels, a.cornerAlpha, a.centreAlpha,
                b.elementPixels, half.coverage, half.centreAlpha, half.cornerAlpha, none.coverage, calm.elementPixels,
                nofar.coverage, plain.coverage);
    int bad = 0;
    auto need = [&](bool ok, const char* what) { if (!ok) { std::printf("FAIL: %s\n", what); bad++; } };
    need(a.coverage > 0.02 && a.coverage < 0.6, "the lattice covers a sensible share of the panel");
    need(a.elementPixels > 0.0005, "element energy (pulses / twinkles) is visible");
    need(calm.elementPixels < a.elementPixels * 0.2, "with no pulses and no twinkles the element colour goes away");
    need(none.coverage < 1e-4, "reveal 0 draws nothing");
    need(std::fabs(plain.coverage - a.coverage) < a.coverage * 0.02, "reveal 1 equals the plain draw");
    need(half.centreAlpha > 0.02 && half.cornerAlpha < 1e-3, "reveal 0.5 is a disc: lattice at the centre, none at the corners");
    need(nofar.coverage < a.coverage, "the far layer adds coverage");
    need(a.cornerAlpha < a.centreAlpha * 0.5, "the edge fade empties the corners");
    return bad ? 1 : 0;
}
"""


RIM_UNIFORMS = r"""
struct v2f { float4 vertex; float4 color; float2 texcoord; float4 worldPosition; float4 rimUV; };
float4 _Time;
sampler2D _MainTex;
fixed4 _Color, _TextureSampleAdd, _ElementColor, _ChargeColor, _MassColor, _SpaceColor, _TimeColor;
float _Omni, _Speed, _Tail, _BaseLevel;
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
    if out:
        from PIL import Image
        for name in ("lattice_t3", "lattice_t9", "lattice_reveal_half"):
            ppm = os.path.join(out, name + ".ppm")
            Image.open(ppm).save(os.path.join(out, name + ".png"))
            os.remove(ppm)
        print(f"rendered to {out}")
    build_rim(work)
    build_presence(work)
    print("OK: the lattice, the rim, the presence orb and the bonds compile and their dials drive")


if __name__ == "__main__":
    main()
