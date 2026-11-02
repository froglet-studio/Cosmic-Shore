// PrismEffectCommon.hlsl - the helpers every PROXY prism effect shader shares (Docs/Shaders/PRISM_EFFECTS.md).
//
// Included by PrismPaperBurn.shader (the Grizzly claw's paper burn) and PrismLaserBurn.shader (Fulmen's flameless burn),
// inside their HLSLINCLUDE, AFTER URP's Core.hlsl. Pure functions only: no properties, no CBUFFER, no textures, no _Time -
// so including it can never change a material's layout or its SRP-batcher compatibility. Everything here is cheap on a
// GLES 3.0 phone (ALU only, float maths that also holds at half where the callers use half).
//
// Adding a helper: keep it pure, prefix NEW names with Prism (the old names below stay as they are because shipped shaders
// call them), and run Tools/Shaders/verify_prism_burn.py - it compiles every includer for D3D11 / Vulkan / Metal / GLES 3.0.

#ifndef PRISM_EFFECT_COMMON_INCLUDED
#define PRISM_EFFECT_COMMON_INCLUDED

// --- hashes (Dave Hoskins, "Hash without Sine") ---------------------------------------------------------------------
float3 Hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// --- value noise: 0..1, smooth, one octave = 8 hashes ---------------------------------------------------------------
float ValueNoise(float3 p)
{
    float3 i = floor(p), f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float a = Hash31(i), b = Hash31(i + float3(1, 0, 0)), c = Hash31(i + float3(0, 1, 0)), d = Hash31(i + float3(1, 1, 0));
    float e = Hash31(i + float3(0, 0, 1)), g = Hash31(i + float3(1, 0, 1)), h = Hash31(i + float3(0, 1, 1)), k = Hash31(i + 1.0);
    return lerp(lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y), lerp(lerp(e, g, u.x), lerp(h, k, u.x), u.y), u.z);
}

// --- the seeded ignition point: a point on the unit cube's (-0.5..0.5) SURFACE, fixed by seed -------------------------
float3 PrismIgnitionPoint(float seed)
{
    float3 origin = (Hash33(seed.xxx * 17.13 + 3.7) - 0.5);
    return origin / max(max(abs(origin.x), abs(origin.y)), max(abs(origin.z), 1e-3)) * 0.5;
}

// --- 4x4 ordered dither, 0..1 ---------------------------------------------------------------------------------------
static const float kBayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
float Bayer4(float2 pixel)
{
    uint2 p = (uint2)pixel & 3u;
    return (kBayer4[p.x + p.y * 4u] + 0.5) / 16.0;
}

// Screen-door coverage: keeps the pixels with dither < coverage, or (invert) the complement - two proxies crossfading with
// complementary masks never overlap or gap. Opaque, so no sorting and no overdraw blend.
void PrismDitherClip(float2 pixel, float coverage, bool invert)
{
    float d = Bayer4(pixel);
    float keep = invert ? (d - (1.0 - coverage)) : (coverage - d);
    clip(keep);
}

#endif // PRISM_EFFECT_COMMON_INCLUDED
