// The slice of HLSL that Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader uses, in C++,
// so Tools/Build/bake_static_skybox.py can compile the SHIPPED shader's fragment math and run it
// per texel. float32 throughout (no fast-math) so the hashes behave as they do on a GPU.
//
// The transpiler renames every intrinsic call to h_<name> (std::pow and friends would otherwise
// make mixed float/double calls ambiguous) and turns the three swizzles the shader reads
// (.xyz .yxz .rgb) into methods. Anything else the shader starts using fails to COMPILE here,
// which is the point: the bake can never silently diverge from the shader.
#pragma once
#include <cmath>

struct float3
{
    float x, y, z;
    float3() : x(0), y(0), z(0) {}
    float3(float s) : x(s), y(s), z(s) {}
    float3(float a, float b, float c) : x(a), y(b), z(c) {}
    float3 yxz() const { return float3(y, x, z); }
    float3 xyz() const { return *this; }
    float3 rgb() const { return *this; }
    float3& operator+=(const float3& o) { x += o.x; y += o.y; z += o.z; return *this; }
    float3& operator-=(const float3& o) { x -= o.x; y -= o.y; z -= o.z; return *this; }
    float3& operator*=(const float3& o) { x *= o.x; y *= o.y; z *= o.z; return *this; }
};

inline float3 operator+(const float3& a, const float3& b) { return float3(a.x + b.x, a.y + b.y, a.z + b.z); }
inline float3 operator-(const float3& a, const float3& b) { return float3(a.x - b.x, a.y - b.y, a.z - b.z); }
inline float3 operator*(const float3& a, const float3& b) { return float3(a.x * b.x, a.y * b.y, a.z * b.z); }
inline float3 operator/(const float3& a, const float3& b) { return float3(a.x / b.x, a.y / b.y, a.z / b.z); }
inline float3 operator+(const float3& a, float b) { return a + float3(b); }
inline float3 operator-(const float3& a, float b) { return a - float3(b); }
inline float3 operator*(const float3& a, float b) { return a * float3(b); }
inline float3 operator/(const float3& a, float b) { return a / float3(b); }
inline float3 operator+(float a, const float3& b) { return float3(a) + b; }
inline float3 operator-(float a, const float3& b) { return float3(a) - b; }
inline float3 operator*(float a, const float3& b) { return float3(a) * b; }
inline float3 operator/(float a, const float3& b) { return float3(a) / b; }
inline float3 operator-(const float3& a) { return float3(-a.x, -a.y, -a.z); }

struct float4
{
    float x, y, z, w;
    float4() : x(0), y(0), z(0), w(0) {}
    float4(float a, float b, float c, float d) : x(a), y(b), z(c), w(d) {}
    float4(const float3& v, float d) : x(v.x), y(v.y), z(v.z), w(d) {}
    float3 xyz() const { return float3(x, y, z); }
    float3 rgb() const { return float3(x, y, z); }
};

using half = float;
using half3 = float3;
using half4 = float4;

inline float h_frac(float v) { return v - std::floor(v); }
inline float3 h_frac(const float3& v) { return float3(h_frac(v.x), h_frac(v.y), h_frac(v.z)); }
inline float h_floor(float v) { return std::floor(v); }
inline float3 h_floor(const float3& v) { return float3(std::floor(v.x), std::floor(v.y), std::floor(v.z)); }
inline float h_dot(const float3& a, const float3& b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
inline float3 h_cross(const float3& a, const float3& b)
{
    return float3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
}
inline float h_length(const float3& v) { return std::sqrt(h_dot(v, v)); }
inline float3 h_normalize(const float3& v) { return v * (1.0f / std::sqrt(h_dot(v, v))); }
inline float h_lerp(float a, float b, float t) { return a + (b - a) * t; }
inline float3 h_lerp(const float3& a, const float3& b, float t) { return a + (b - a) * t; }
inline float h_saturate(float v) { return v < 0.0f ? 0.0f : (v > 1.0f ? 1.0f : v); }
inline float h_smoothstep(float e0, float e1, float x)
{
    float t = h_saturate((x - e0) / (e1 - e0));
    return t * t * (3.0f - 2.0f * t);
}
inline float h_exp(float v) { return std::exp(v); }
inline float h_pow(float a, float b) { return std::pow(a, b); }
inline float h_sin(float v) { return std::sin(v); }
inline float h_cos(float v) { return std::cos(v); }
inline float h_atan2(float y, float x) { return std::atan2(y, x); }
inline float h_sqrt(float v) { return std::sqrt(v); }
inline float h_abs(float v) { return std::fabs(v); }
inline float h_max(float a, float b) { return a > b ? a : b; }
inline float h_min(float a, float b) { return a < b ? a : b; }
