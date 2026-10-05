using System;
using System.Runtime.CompilerServices;

namespace Unity.Mathematics
{
    /// <summary>
    /// Hand-written half of <c>Unity.Mathematics.math</c>: constants, hashing, directions and the
    /// quaternion / matrix functions. The componentwise scalar/vector functions (abs, min, dot,
    /// lerp, normalizesafe, cross, ...) are generated into <c>math.componentwise.gen.cs</c>.
    /// Everything runs as plain managed C# — there is no Burst, so float results follow .NET's
    /// IEEE-754 single-precision rules (MathF), which is what Burst's FloatMode.Default targets.
    /// </summary>
    public static partial class math
    {
        public const float E = 2.71828182845904523536f;
        public const double E_DBL = 2.71828182845904523536;
        public const float LOG2E = 1.44269504088896340736f;
        public const float LOG10E = 0.434294481903251827651f;
        public const float LN2 = 0.693147180559945309417f;
        public const float LN10 = 2.30258509299404568402f;
        public const float PI = 3.14159265358979323846f;
        public const double PI_DBL = 3.14159265358979323846;
        public const float PI2 = PI * 2f;
        public const double PI2_DBL = PI_DBL * 2.0;
        public const float PIHALF = PI * 0.5f;
        public const float TAU = PI2;
        public const float TODEGREES = 57.29577951308232f;
        public const float TORADIANS = 0.017453292519943296f;
        public const float SQRT2 = 1.41421356237309504880f;
        public const float EPSILON = 1.1920928955078125e-7f;
        public const double EPSILON_DBL = 2.2204460492503131e-16;
        public const float INFINITY = float.PositiveInfinity;
        public const double INFINITY_DBL = double.PositiveInfinity;
        public const float NAN = float.NaN;
        public const double NAN_DBL = double.NaN;
        public const float FLT_MIN_NORMAL = 1.175494351e-38f;
        public const double DBL_MIN_NORMAL = 2.2250738585072014e-308;

        // ---- direction helpers (Unity: left-handed, +y up, +z forward)
        public static float3 up() => new float3(0f, 1f, 0f);
        public static float3 down() => new float3(0f, -1f, 0f);
        public static float3 forward() => new float3(0f, 0f, 1f);
        public static float3 back() => new float3(0f, 0f, -1f);
        public static float3 left() => new float3(-1f, 0f, 0f);
        public static float3 right() => new float3(1f, 0f, 0f);

        // ---- component constructors (math.float3(...) style factory functions)
        public static float2 float2(float x, float y) => new float2(x, y);
        public static float2 float2(float v) => new float2(v);
        public static float3 float3(float x, float y, float z) => new float3(x, y, z);
        public static float3 float3(float2 xy, float z) => new float3(xy, z);
        public static float3 float3(float v) => new float3(v);
        public static float4 float4(float x, float y, float z, float w) => new float4(x, y, z, w);
        public static float4 float4(float3 xyz, float w) => new float4(xyz, w);
        public static float4 float4(float v) => new float4(v);
        public static int2 int2(int x, int y) => new int2(x, y);
        public static int3 int3(int x, int y, int z) => new int3(x, y, z);
        public static int4 int4(int x, int y, int z, int w) => new int4(x, y, z, w);
        public static uint2 uint2(uint x, uint y) => new uint2(x, y);
        public static uint3 uint3(uint x, uint y, uint z) => new uint3(x, y, z);
        public static uint4 uint4(uint x, uint y, uint z, uint w) => new uint4(x, y, z, w);
        public static bool2 bool2(bool x, bool y) => new bool2(x, y);
        public static bool3 bool3(bool x, bool y, bool z) => new bool3(x, y, z);
        public static bool4 bool4(bool x, bool y, bool z, bool w) => new bool4(x, y, z, w);
        public static double3 double3(double x, double y, double z) => new double3(x, y, z);
        public static quaternion quaternion(float x, float y, float z, float w) => new quaternion(x, y, z, w);
        public static quaternion quaternion(float4 value) => new quaternion(value);
        public static quaternion quaternion(float3x3 m) => new quaternion(m);
        public static float3x3 float3x3(float3 c0, float3 c1, float3 c2) => new float3x3(c0, c1, c2);
        public static float3x3 float3x3(quaternion q) => new float3x3(q);
        public static float4x4 float4x4(float4 c0, float4 c1, float4 c2, float4 c3) => new float4x4(c0, c1, c2, c3);
        public static float4x4 float4x4(float3x3 rotation, float3 translation) => new float4x4(rotation, translation);
        public static float4x4 float4x4(quaternion rotation, float3 translation) => new float4x4(rotation, translation);

        // ---- integer helpers
        public static bool ispow2(int x) => x > 0 && (x & (x - 1)) == 0;
        public static bool ispow2(uint x) => x > 0 && (x & (x - 1)) == 0;
        public static int ceilpow2(int x) { x -= 1; x |= x >> 1; x |= x >> 2; x |= x >> 4; x |= x >> 8; x |= x >> 16; return x + 1; }
        public static uint ceilpow2(uint x) { x -= 1; x |= x >> 1; x |= x >> 2; x |= x >> 4; x |= x >> 8; x |= x >> 16; return x + 1; }
        public static int lzcnt(uint x) => System.Numerics.BitOperations.LeadingZeroCount(x);
        public static int lzcnt(int x) => System.Numerics.BitOperations.LeadingZeroCount((uint)x);
        public static int tzcnt(uint x) => x == 0 ? 32 : System.Numerics.BitOperations.TrailingZeroCount(x);
        public static int tzcnt(int x) => tzcnt((uint)x);
        public static int countbits(uint x) => System.Numerics.BitOperations.PopCount(x);
        public static int countbits(int x) => System.Numerics.BitOperations.PopCount((uint)x);
        public static int floorlog2(int x) => 31 - lzcnt((uint)x);
        public static int ceillog2(int x) => 32 - lzcnt((uint)x - 1u);

        // ---- hashing: a stable, well-mixed 32-bit hash (not bit-identical to Unity's; the game
        // only uses hashes as keys/seeds, never persists them).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Mix(uint h)
        {
            h ^= h >> 16; h *= 0x7feb352du;
            h ^= h >> 15; h *= 0x846ca68bu;
            h ^= h >> 16; return h;
        }
        public static uint hash(uint v) => Mix(v * 0x9E3779B1u + 0x68E31DA4u);
        public static uint hash(int v) => hash((uint)v);
        public static uint hash(float v) => hash(asuint(v));
        public static uint hash(uint2 v) => Mix(hash(v.x) ^ (v.y * 0x85EBCA77u));
        public static uint hash(uint3 v) => Mix(hash(v.xy) ^ (v.z * 0xC2B2AE3Du));
        public static uint hash(uint4 v) => Mix(hash(v.xyz) ^ (v.w * 0x27D4EB2Fu));
        public static uint hash(int2 v) => hash((uint2)v);
        public static uint hash(int3 v) => hash((uint3)v);
        public static uint hash(int4 v) => hash((uint4)v);
        public static uint hash(float2 v) => hash(asuint(v));
        public static uint hash(float3 v) => hash(asuint(v));
        public static uint hash(float4 v) => hash(asuint(v));
        public static uint hash(quaternion q) => hash(q.value);

        // ---- quaternion functions
        public static float dot(quaternion a, quaternion b) => dot(a.value, b.value);
        public static float length(quaternion q) => MathF.Sqrt(dot(q.value, q.value));
        public static float lengthsq(quaternion q) => dot(q.value, q.value);
        public static quaternion conjugate(quaternion q) => new quaternion(q.value * new float4(-1f, -1f, -1f, 1f));
        public static quaternion inverse(quaternion q)
        {
            float4 x = q.value;
            return new quaternion(rcp(dot(x, x)) * x * new float4(-1f, -1f, -1f, 1f));
        }
        public static quaternion normalize(quaternion q) => new quaternion(rsqrt(dot(q.value, q.value)) * q.value);
        public static quaternion normalizesafe(quaternion q) => normalizesafe(q, Mathematics.quaternion.identity);
        public static quaternion normalizesafe(quaternion q, quaternion defaultvalue)
        {
            float len = dot(q.value, q.value);
            return len > FLT_MIN_NORMAL ? new quaternion(q.value * rsqrt(len)) : defaultvalue;
        }

        /// <summary>Hamilton product a*b (apply b, then a).</summary>
        public static quaternion mul(quaternion a, quaternion b)
        {
            float4 x = a.value, y = b.value;
            return new quaternion(
                x.w * y.x + x.x * y.w + x.y * y.z - x.z * y.y,
                x.w * y.y - x.x * y.z + x.y * y.w + x.z * y.x,
                x.w * y.z + x.x * y.y - x.y * y.x + x.z * y.w,
                x.w * y.w - x.x * y.x - x.y * y.y - x.z * y.z);
        }

        /// <summary>Rotates <paramref name="v"/> by the (unit) quaternion.</summary>
        public static float3 mul(quaternion q, float3 v)
        {
            float3 u = q.value.xyz;
            float3 t = 2f * cross(u, v);
            return v + q.value.w * t + cross(u, t);
        }
        public static float3 rotate(quaternion q, float3 v) => mul(q, v);

        public static quaternion nlerp(quaternion q1, quaternion q2, float t)
        {
            float d = dot(q1, q2);
            float4 b = d < 0f ? -q2.value : q2.value;
            return normalize(new quaternion(lerp(q1.value, b, t)));
        }

        public static quaternion slerp(quaternion q1, quaternion q2, float t)
        {
            float d = dot(q1, q2);
            float4 b = q2.value;
            if (d < 0f) { d = -d; b = -b; }
            if (d < 0.9995f)
            {
                float angle = MathF.Acos(d);
                float w1 = MathF.Sin((1f - t) * angle), w2 = MathF.Sin(t * angle);
                float invSin = 1f / MathF.Sin(angle);
                return new quaternion(q1.value * (w1 * invSin) + b * (w2 * invSin));
            }
            return nlerp(q1, new quaternion(b), t);
        }

        /// <summary>Angle in radians between two unit quaternions.</summary>
        public static float angle(quaternion q1, quaternion q2)
        {
            float diff = MathF.Sqrt(lengthsq(mul(q1, conjugate(q2)).value.xyz));
            return 2f * MathF.Atan2(diff, MathF.Abs(dot(q1, q2)));
        }

        public static float3 forward(quaternion q) => mul(q, new float3(0f, 0f, 1f));
        public static float3 up(quaternion q) => mul(q, new float3(0f, 1f, 0f));
        public static float3 right(quaternion q) => mul(q, new float3(1f, 0f, 0f));

        // ---- matrix functions
        public static float3 mul(float3x3 a, float3 b) => a.c0 * b.x + a.c1 * b.y + a.c2 * b.z;
        public static float4 mul(float4x4 a, float4 b) => a.c0 * b.x + a.c1 * b.y + a.c2 * b.z + a.c3 * b.w;
        public static float3 mul(float3 a, float3x3 b) => new float3(dot(a, b.c0), dot(a, b.c1), dot(a, b.c2));
        public static float4 mul(float4 a, float4x4 b) => new float4(dot(a, b.c0), dot(a, b.c1), dot(a, b.c2), dot(a, b.c3));
        public static float3x3 mul(float3x3 a, float3x3 b) => new float3x3(mul(a, b.c0), mul(a, b.c1), mul(a, b.c2));
        public static float4x4 mul(float4x4 a, float4x4 b) => new float4x4(mul(a, b.c0), mul(a, b.c1), mul(a, b.c2), mul(a, b.c3));

        /// <summary>Transforms a point (w = 1) by an affine/projective matrix.</summary>
        public static float3 transform(float4x4 a, float3 b) => (a.c0 * b.x + a.c1 * b.y + a.c2 * b.z + a.c3).xyz;
        /// <summary>Rotates/scales a direction (w = 0) — the 3x3 part of the matrix.</summary>
        public static float3 rotate(float4x4 a, float3 b) => (a.c0 * b.x + a.c1 * b.y + a.c2 * b.z).xyz;

        public static float3x3 transpose(float3x3 v) => new float3x3(
            v.c0.x, v.c0.y, v.c0.z,
            v.c1.x, v.c1.y, v.c1.z,
            v.c2.x, v.c2.y, v.c2.z);

        public static float4x4 transpose(float4x4 v) => new float4x4(
            v.c0.x, v.c0.y, v.c0.z, v.c0.w,
            v.c1.x, v.c1.y, v.c1.z, v.c1.w,
            v.c2.x, v.c2.y, v.c2.z, v.c2.w,
            v.c3.x, v.c3.y, v.c3.z, v.c3.w);

        public static float determinant(float3x3 m) =>
            m.c0.x * (m.c1.y * m.c2.z - m.c2.y * m.c1.z)
            - m.c1.x * (m.c0.y * m.c2.z - m.c2.y * m.c0.z)
            + m.c2.x * (m.c0.y * m.c1.z - m.c1.y * m.c0.z);

        public static float3x3 inverse(float3x3 m)
        {
            float3 r0 = cross(m.c1, m.c2), r1 = cross(m.c2, m.c0), r2 = cross(m.c0, m.c1);
            float invDet = 1f / dot(m.c0, r0);
            // rows of the inverse are r0,r1,r2 scaled; build as columns via transpose.
            return transpose(new float3x3(r0 * invDet, r1 * invDet, r2 * invDet));
        }

        public static float determinant(float4x4 m) => Det4(m, out _);

        public static float4x4 inverse(float4x4 m)
        {
            float det = Det4(m, out float4x4 adjT);
            return adjT * (1f / det);
        }

        /// <summary>Cofactor expansion; returns the determinant and the adjugate (already transposed into the inverse layout).</summary>
        static float Det4(float4x4 m, out float4x4 adjugate)
        {
            // Row-major scalar copy: a[r,c].
            float a00 = m.c0.x, a01 = m.c1.x, a02 = m.c2.x, a03 = m.c3.x;
            float a10 = m.c0.y, a11 = m.c1.y, a12 = m.c2.y, a13 = m.c3.y;
            float a20 = m.c0.z, a21 = m.c1.z, a22 = m.c2.z, a23 = m.c3.z;
            float a30 = m.c0.w, a31 = m.c1.w, a32 = m.c2.w, a33 = m.c3.w;

            float s0 = a00 * a11 - a10 * a01, s1 = a00 * a12 - a10 * a02, s2 = a00 * a13 - a10 * a03;
            float s3 = a01 * a12 - a11 * a02, s4 = a01 * a13 - a11 * a03, s5 = a02 * a13 - a12 * a03;
            float c5 = a22 * a33 - a32 * a23, c4 = a21 * a33 - a31 * a23, c3 = a21 * a32 - a31 * a22;
            float c2 = a20 * a33 - a30 * a23, c1 = a20 * a32 - a30 * a22, c0 = a20 * a31 - a30 * a21;

            float det = s0 * c5 - s1 * c4 + s2 * c3 + s3 * c2 - s4 * c1 + s5 * c0;

            // Inverse (before 1/det) in row-major b[r,c].
            float b00 = a11 * c5 - a12 * c4 + a13 * c3;
            float b01 = -a01 * c5 + a02 * c4 - a03 * c3;
            float b02 = a31 * s5 - a32 * s4 + a33 * s3;
            float b03 = -a21 * s5 + a22 * s4 - a23 * s3;
            float b10 = -a10 * c5 + a12 * c2 - a13 * c1;
            float b11 = a00 * c5 - a02 * c2 + a03 * c1;
            float b12 = -a30 * s5 + a32 * s2 - a33 * s1;
            float b13 = a20 * s5 - a22 * s2 + a23 * s1;
            float b20 = a10 * c4 - a11 * c2 + a13 * c0;
            float b21 = -a00 * c4 + a01 * c2 - a03 * c0;
            float b22 = a30 * s4 - a31 * s2 + a33 * s0;
            float b23 = -a20 * s4 + a21 * s2 - a23 * s0;
            float b30 = -a10 * c3 + a11 * c1 - a12 * c0;
            float b31 = a00 * c3 - a01 * c1 + a02 * c0;
            float b32 = -a30 * s3 + a31 * s1 - a32 * s0;
            float b33 = a20 * s3 - a21 * s1 + a22 * s0;

            adjugate = new float4x4(
                b00, b01, b02, b03,
                b10, b11, b12, b13,
                b20, b21, b22, b23,
                b30, b31, b32, b33);
            return det;
        }

        /// <summary>Inverse of an orthonormal rigid transform (rotation + translation).</summary>
        public static float4x4 fastinverse(float4x4 m)
        {
            float3x3 r = transpose(new float3x3(m.c0.xyz, m.c1.xyz, m.c2.xyz));
            float3 t = -mul(r, m.c3.xyz);
            return new float4x4(r, t);
        }

        public static float4x4 orthonormalize(float4x4 m) => m;
    }
}
