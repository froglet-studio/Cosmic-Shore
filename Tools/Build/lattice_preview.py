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
edge fade empties the corners. What it does not: Unity's own shader compile (no editor here) - the UI plumbing
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
typedef float4 half4;
typedef int sampler2D;
static inline float4 tex2D(sampler2D, float2) { return (float4){1, 1, 1, 1}; }
#define min hmin
#define max hmax
#define abs habs
#define floor floor_
#define sin sin_
#define atan2 atan2_
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
    print("OK: the lattice and the rim compile and their dials drive")


if __name__ == "__main__":
    main()
