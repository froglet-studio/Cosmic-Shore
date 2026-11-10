// The slice of HLSL that Assets/_Graphics/Materials/Shaders/GenomeNebula/GenomeNebula.hlsl uses, in C++, so
// Tools/Build/genome_nebula_preview.py can compile the SHIPPED include and run it per pixel (previews, parity with
// the Amoebius-genome lab, and a CPU cost comparison against HyperSeaSkybox). float32 throughout, no fast-math.
#pragma once
#include <cmath>

struct float2
{
    float x, y;
    float2() : x(0), y(0) {}
    float2(float s) : x(s), y(s) {}
    float2(float a, float b) : x(a), y(b) {}
};
struct float3
{
    float x, y, z;
    float3() : x(0), y(0), z(0) {}
    float3(float s) : x(s), y(s), z(s) {}
    float3(float a, float b, float c) : x(a), y(b), z(c) {}
    float3& operator+=(const float3& o) { x += o.x; y += o.y; z += o.z; return *this; }
};
struct float4
{
    float x, y, z, w;
    float4() : x(0), y(0), z(0), w(0) {}
    float4(float a, float b, float c, float d) : x(a), y(b), z(c), w(d) {}
    float4(const float3& v, float d) : x(v.x), y(v.y), z(v.z), w(d) {}
    float3 xyz_() const { return float3(x, y, z); }
};
using half = float; using half3 = float3; using half4 = float4;

#define OP2(op) \
inline float2 operator op(const float2& a, const float2& b) { return float2(a.x op b.x, a.y op b.y); } \
inline float2 operator op(const float2& a, float b) { return float2(a.x op b, a.y op b); } \
inline float2 operator op(float a, const float2& b) { return float2(a op b.x, a op b.y); } \
inline float3 operator op(const float3& a, const float3& b) { return float3(a.x op b.x, a.y op b.y, a.z op b.z); } \
inline float3 operator op(const float3& a, float b) { return float3(a.x op b, a.y op b, a.z op b); } \
inline float3 operator op(float a, const float3& b) { return float3(a op b.x, a op b.y, a op b.z); }
OP2(+) OP2(-) OP2(*) OP2(/)

#define F1(name, expr) \
inline float h_##name(float v) { return expr; } \
inline float2 h_##name(const float2& v) { return float2(h_##name(v.x), h_##name(v.y)); } \
inline float3 h_##name(const float3& v) { return float3(h_##name(v.x), h_##name(v.y), h_##name(v.z)); }
F1(floor, std::floor(v))
F1(frac, v - std::floor(v))
F1(saturate, v < 0.0f ? 0.0f : (v > 1.0f ? 1.0f : v))
F1(sin, std::sin(v))
F1(cos, std::cos(v))

inline float h_dot(const float3& a, const float3& b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
inline float h_lerp(float a, float b, float t) { return a + (b - a) * t; }
inline float3 h_lerp(const float3& a, const float3& b, float t) { return a + (b - a) * t; }
inline float h_pow(float a, float b) { return std::pow(a, b); }
inline float h_atan2(float y, float x) { return std::atan2(y, x); }
inline float h_asin(float v) { return std::asin(v); }
inline float h_clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
inline float3 h_normalize(const float3& v) { return v * (1.0f / std::sqrt(h_dot(v, v))); }
