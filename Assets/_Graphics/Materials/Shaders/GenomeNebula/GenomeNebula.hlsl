// GenomeNebula.hlsl - the Amoebius-genome "minimal__tuned" sky, ported line for line.
//
// Source: /mnt/project-files/amoebius-genome/seeds/amoebius_like/minimal__tuned.json (118 zlib bytes for the WHOLE
// genome; the sky is its nodes 0-3: two tex.perlin and two tex.distort). The lab compiles those nodes to GLSL in
// agenome/glsl.py; this file is the same math in HLSL so Unity draws the same picture:
//   t0 = perlin(freq 3, 4 oct)        dark green -> lavender-blue
//   t1 = perlin(freq 6, 3 oct)        near-black -> grey (the warp field)
//   t2 = t0(uv + (t1(uv).rg - .5) * 2 * warp0)
//   t3 = t2(uv + (t1(uv).rg - .5) * 2 * warp1)      <- the sky
// then the lab's sky multiplier, fog mix, and (optionally) its grade: exposure, ACES fit, saturation, contrast.
//
// Written in the small HLSL subset Tools/Build/genome_nebula_preview.py compiles as C++ (no swizzles beyond
// helper functions), so the preview, the lab and Unity can be compared pixel for pixel. Change one, run the tool.
#ifndef GENOME_NEBULA_INCLUDED
#define GENOME_NEBULA_INCLUDED

// ---- genome parameters (material Vectors: raw linear values, never sRGB-converted like Color props) ----
float4 _ColA0, _ColB0;      // t0 colour ramp (linear RGB)
float4 _Noise0;             // t0: x freq (integer cells), y octaves, z persistence, w seed (already *7.31)
float  _Gamma0;
float4 _ColA1, _ColB1;      // t1 colour ramp (the warp field)
float4 _Noise1;
float  _Gamma1;
float4 _Warp;               // x warp amount of node 2, y of node 3 (the genome's tex.distort amount)
float4 _Drift;              // x,y: uv/s the warp field slides (0 = the lab's static sky); z: t0 slide; w unused
float  _SkyIntensity;       // the out node's `sky` gene
float4 _FogColor;           // post.grade fog colour
float  _FogMix;             // 1 - exp(-fog * far * 0.5), as the lab's scene pass mixes the sky
float4 _Grade;              // x exposure, y saturation, z contrast, w 1 = bake the lab's grade, 0 = leave it to URP post
float  _SphereScale;        // sphere mapping only: uv units per unit of direction

// ---- hashes and noise, exactly as agenome/glsl.py PRELUDE ----
float gn_h21(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
    p3 += dot(p3, float3(p3.y, p3.z, p3.x) + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float gn_h31(float3 p3)
{
    p3 = frac(p3 * 0.1031);
    p3 += dot(p3, float3(p3.z, p3.y, p3.x) + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

float2 gn_mod(float2 a, float m) { return a - m * floor(a / m); }   // GLSL mod, not HLSL fmod

// periodic 2D value noise (period `per` cells), so the equirect seam at u = 0/1 matches
float gn_vn2p(float2 p, float per, float sd)
{
    float2 i = floor(p);
    float2 fr = p - i;
    float2 u = fr * fr * (3.0 - 2.0 * fr);
    float a = gn_h21(gn_mod(i, per) + sd);
    float b = gn_h21(gn_mod(i + float2(1, 0), per) + sd);
    float c = gn_h21(gn_mod(i + float2(0, 1), per) + sd);
    float d = gn_h21(gn_mod(i + float2(1, 1), per) + sd);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float gn_fbm2(float2 uv, float4 n)
{
    float s = 0.0, a = 1.0, w = 0.0, fq = n.x;
    [unroll] for (int i = 0; i < 8; i++)
    {
        if (i < (int)n.y)
        {
            s += a * gn_vn2p(uv * fq, fq, n.w + i * 17.0);
            w += a; a *= n.z; fq *= 2.0;
        }
    }
    return s / w;
}

// 3D value noise for the seam- and pole-free sphere mapping (not in the lab; same octave rules)
float gn_vn3(float3 p)
{
    float3 i = floor(p);
    float3 fr = p - i;
    float3 u = fr * fr * (3.0 - 2.0 * fr);
    float x00 = lerp(gn_h31(i), gn_h31(i + float3(1, 0, 0)), u.x);
    float x10 = lerp(gn_h31(i + float3(0, 1, 0)), gn_h31(i + float3(1, 1, 0)), u.x);
    float x01 = lerp(gn_h31(i + float3(0, 0, 1)), gn_h31(i + float3(1, 0, 1)), u.x);
    float x11 = lerp(gn_h31(i + float3(0, 1, 1)), gn_h31(i + float3(1, 1, 1)), u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

float gn_fbm3(float3 p, float4 n)
{
    float s = 0.0, a = 1.0, w = 0.0, fq = n.x;
    [unroll] for (int i = 0; i < 8; i++)
    {
        if (i < (int)n.y)
        {
            s += a * gn_vn3(p * fq + n.w * 0.013 + i * 17.0);
            w += a; a *= n.z; fq *= 2.0;
        }
    }
    return s / w;
}

float3 gn_ramp(float x, float g, float4 a, float4 b)
{
    x = pow(saturate((x - 0.5) * 1.6 + 0.5), g);
    return lerp(a.xyz, b.xyz, x);
}

// ---- the four genome nodes ----
float3 gn_t0(float2 uv) { return gn_ramp(gn_fbm2(frac(uv), _Noise0), _Gamma0, _ColA0, _ColB0); }
float3 gn_t1(float2 uv) { return gn_ramp(gn_fbm2(frac(uv), _Noise1), _Gamma1, _ColA1, _ColB1); }

float3 gn_sky_uv(float2 uv, float t)
{
    float2 slide = float2(_Drift.x, _Drift.y) * t;
    float3 b = gn_t1(uv + slide);                                        // node 3's warp sample
    float2 uv3 = uv + (float2(b.x, b.y) - 0.5) * (2.0 * _Warp.y);
    float3 b2 = gn_t1(uv3 + slide);                                      // node 2's warp sample
    float2 uv2 = uv3 + (float2(b2.x, b2.y) - 0.5) * (2.0 * _Warp.x);
    return gn_t0(uv2 + float2(_Drift.z * t, 0.0));
}

float3 gn_s0(float3 p) { return gn_ramp(gn_fbm3(p, _Noise0), _Gamma0, _ColA0, _ColB0); }
float3 gn_s1(float3 p) { return gn_ramp(gn_fbm3(p, _Noise1), _Gamma1, _ColA1, _ColB1); }

float3 gn_sky_sphere(float3 dir, float t)
{
    float3 p = dir * _SphereScale;
    float3 slide = float3(_Drift.x, _Drift.y, _Drift.x) * t;
    float3 b = gn_s1(p + slide);
    float3 p3 = p + (b - 0.5) * (2.0 * _Warp.y);
    float3 b2 = gn_s1(p3 + slide);
    float3 p2 = p3 + (b2 - 0.5) * (2.0 * _Warp.x);
    return gn_s0(p2 + float3(_Drift.z * t, 0.0, 0.0));
}

// the lab's equirect mapping (agenome/glsl.py scene_shader SKY)
float2 gn_equirect(float3 rd)
{
    return float2(atan2(rd.z, rd.x) / 6.2831853 + 0.5, asin(clamp(rd.y, -1.0, 1.0)) / 3.14159265 + 0.5);
}

float3 gn_aces(float3 c) { return c * (2.51 * c + 0.03) / (c * (2.43 * c + 0.59) + 0.14); }

// sky colour -> what the lab's composite pass would put on screen (minus screen-space vignette/aberration/feedback).
// Returns LINEAR light: the lab ends with pow(c, 1/2.2), which a Linear-space Unity project's sRGB target does for us.
float3 gn_finish(float3 sky)
{
    float3 c = lerp(sky * _SkyIntensity, _FogColor.xyz, _FogMix);
    if (_Grade.w > 0.5)
    {
        c = gn_aces(c * _Grade.x);
        float l = dot(c, float3(0.299, 0.587, 0.114));
        c = lerp(float3(l, l, l), c, _Grade.y);
        c = saturate((c - 0.5) * _Grade.z + 0.5);
    }
    return c;
}

#endif
