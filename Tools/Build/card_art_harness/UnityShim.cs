// The UnityEngine surface the shipped arena and course generators touch, re-implemented so the
// harness can COMPILE AND RUN them outside the editor and photograph what they emit.
//
// Two classes of member live here and they are held to different standards:
//
//   * GEOMETRY (Vector3, Quaternion, Mathf) is FAITHFUL. The card art is a picture of what the
//     generator really lays, so a rotation that is merely "total and non-throwing" (the Cleave
//     harness's LookRotation returns identity - fine for counting, wrong for drawing) would
//     photograph a world of axis-aligned prisms and call it the arena. Every construction below
//     follows Unity's documented one (LookRotation: z along forward, x = up x z, y = z x x;
//     Euler: Z, then X, then Y; banker's rounding in RoundToInt).
//   * ENGINE OBJECTS (GameObject, Transform, MonoBehaviour, Random) are INERT STAND-INS. The
//     generators reference them from code paths the harness never runs (SpawnLeafObjects, the
//     async lay); they exist so the file compiles, and any path that did reach them would be a
//     harness bug, not a measurement. UnityEngine.Random is NOT Unity's generator - no arena the
//     harness runs draws from it (they draw from the base's seeded System.Random), and a
//     generator that started to would be caught by the negative-control count check.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0f; }

        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : i == 2 ? z : throw new IndexOutOfRangeException();
            set { if (i == 0) x = value; else if (i == 1) y = value; else if (i == 2) z = value; else throw new IndexOutOfRangeException(); }
        }

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);

        // Unity returns ZERO (not NaN) below kEpsilon rather than dividing through.
        public const float kEpsilon = 1e-5f;
        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > kEpsilon ? new Vector3(x / m, y / m, z / m) : zero;
            }
        }
        public void Normalize() => this = normalized;
        public static Vector3 Normalize(Vector3 v) => v.normalized;

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) =>
            new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float SqrMagnitude(Vector3 a) => a.sqrMagnitude;
        public static float Magnitude(Vector3 a) => a.magnitude;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public void Scale(Vector3 b) { x *= b.x; y *= b.y; z *= b.z; }
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public static Vector3 Project(Vector3 v, Vector3 n)
        {
            float d = Dot(n, n);
            return d < Mathf.Epsilon ? zero : n * (Dot(v, n) / d);
        }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
        {
            float d = Dot(n, n);
            return d < Mathf.Epsilon ? v : v - n * (Dot(v, n) / d);
        }
        public static Vector3 Reflect(Vector3 d, Vector3 n) => -2f * Dot(n, d) * n + d;
        public static Vector3 ClampMagnitude(Vector3 v, float max) =>
            v.sqrMagnitude > max * max ? v.normalized * max : v;
        public static float Angle(Vector3 a, Vector3 b)
        {
            float denom = (float)Math.Sqrt(a.sqrMagnitude * b.sqrMagnitude);
            if (denom < 1e-15f) return 0f;
            float dot = Mathf.Clamp(Dot(a, b) / denom, -1f, 1f);
            return (float)Math.Acos(dot) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float a = Angle(from, to);
            float s = Math.Sign(Dot(axis, Cross(from, to)));
            return a * (s == 0 ? 1f : s);
        }
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            float ma = a.magnitude, mb = b.magnitude;
            var na = a.normalized; var nb = b.normalized;
            float dot = Mathf.Clamp(Dot(na, nb), -1f, 1f);
            float th = (float)Math.Acos(dot) * t;
            var rel = (nb - na * dot).normalized;
            var dir = na * (float)Math.Cos(th) + rel * (float)Math.Sin(th);
            return dir * (ma + (mb - ma) * t);
        }
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d)
        {
            var v = t - c; float m = v.magnitude;
            return m <= d || m == 0f ? t : c + v / m * d;
        }
        public static Vector3 RotateTowards(Vector3 c, Vector3 t, float maxRad, float maxMag)
        {
            float ang = Angle(c, t) * Mathf.Deg2Rad;
            if (ang < 1e-6f) return MoveTowards(c, t, maxMag);
            var axis = Cross(c, t);
            if (axis.sqrMagnitude < 1e-12f) axis = Cross(c, Math.Abs(c.x) < 0.9f ? right : up);
            var q = Quaternion.AngleAxis(Math.Min(ang, maxRad) * Mathf.Rad2Deg, axis);
            float mag = Mathf.MoveTowards(c.magnitude, t.magnitude, maxMag);
            return (q * c).normalized * mag;
        }
        public static void OrthoNormalize(ref Vector3 n, ref Vector3 t)
        {
            n = n.normalized;
            t = (t - n * Dot(t, n)).normalized;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator *(float f, Vector3 a) => a * f;
        public static Vector3 operator /(Vector3 a, float f) => new Vector3(a.x / f, a.y / f, a.z / f);
        // Unity compares within 1e-5 on the squared distance, not exactly.
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public bool Equals(Vector3 o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0f);

        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }

    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);
        public static Vector2 up => new Vector2(0f, 1f);
        public static Vector2 right => new Vector2(1f, 0f);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : zero; } }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float f) => new Vector2(a.x * f, a.y * f);
        public static Vector2 operator *(float f, Vector2 a) => a * f;
        public static Vector2 operator /(Vector2 a, float f) => new Vector2(a.x / f, a.y / f);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public bool Equals(Vector2 o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Vector2 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y);
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public bool Equals(Vector3Int o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3Int v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public static bool operator ==(Vector3Int a, Vector3Int b) => a.Equals(b);
        public static bool operator !=(Vector3Int a, Vector3Int b) => !a.Equals(b);
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public static Quaternion AngleAxis(float angleDeg, Vector3 axis)
        {
            var n = axis.normalized;
            float half = angleDeg * Mathf.Deg2Rad * 0.5f;
            float s = (float)Math.Sin(half);
            return new Quaternion(n.x * s, n.y * s, n.z * s, (float)Math.Cos(half));
        }

        /// <summary>Unity applies Z, then X, then Y (in world terms: q = qY * qX * qZ).</summary>
        public static Quaternion Euler(float ex, float ey, float ez) =>
            AngleAxis(ey, Vector3.up) * AngleAxis(ex, Vector3.right) * AngleAxis(ez, Vector3.forward);
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);

        public static Quaternion Inverse(Quaternion q)
        {
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (n < 1e-20f) return identity;
            return new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n);
        }

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static float Angle(Quaternion a, Quaternion b)
        {
            float d = Math.Min(Math.Abs(Dot(a, b)), 1f);
            return d > 0.999999f ? 0f : (float)Math.Acos(d) * 2f * Mathf.Rad2Deg;
        }

        public Quaternion normalized
        {
            get
            {
                float m = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
                return m < 1e-20f ? identity : new Quaternion(x / m, y / m, z / m, w / m);
            }
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => SlerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            float d = Dot(a, b);
            if (d < 0f) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); d = -d; }
            if (d > 0.9995f)
                return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
            float th = (float)Math.Acos(d);
            float s = (float)Math.Sin(th);
            float wa = (float)Math.Sin((1f - t) * th) / s, wb = (float)Math.Sin(t * th) / s;
            return new Quaternion(a.x * wa + b.x * wb, a.y * wa + b.y * wb, a.z * wa + b.z * wb, a.w * wa + b.w * wb);
        }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => Slerp(a, b, t);

        /// <summary>Unity's construction: z along forward, x = up x z, y = z x x. Where up is
        /// parallel to forward Unity silently invents a pose; this answers with the shortest arc
        /// from +z, which is what the preview's photograph needs (a real pose, not an exception)
        /// and is named here so nobody mistakes it for a measurement.</summary>
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 zAxis = forward.normalized;
            if (zAxis.sqrMagnitude < 1e-12f) return identity;
            Vector3 xAxis = Vector3.Cross(up, zAxis);
            if (xAxis.sqrMagnitude < 1e-12f) return FromToRotation(Vector3.forward, zAxis);
            xAxis = xAxis.normalized;
            Vector3 yAxis = Vector3.Cross(zAxis, xAxis);
            return FromBasis(xAxis, yAxis, zAxis);
        }
        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            var a = from.normalized; var b = to.normalized;
            float d = Vector3.Dot(a, b);
            if (d > 0.999999f) return identity;
            if (d < -0.999999f)
            {
                var axis = Vector3.Cross(Vector3.right, a);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, a);
                return AngleAxis(180f, axis);
            }
            var c = Vector3.Cross(a, b);
            return new Quaternion(c.x, c.y, c.z, 1f + d).normalized;
        }

        static Quaternion FromBasis(Vector3 X, Vector3 Y, Vector3 Z)
        {
            // Rotation matrix columns are X, Y, Z.
            float m00 = X.x, m01 = Y.x, m02 = Z.x;
            float m10 = X.y, m11 = Y.y, m12 = Z.y;
            float m20 = X.z, m21 = Y.z, m22 = Z.z;
            float tr = m00 + m11 + m22;
            if (tr > 0f)
            {
                float s = (float)Math.Sqrt(tr + 1f) * 2f;
                return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            if (m00 > m11 && m00 > m22)
            {
                float s = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f;
                return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            if (m11 > m22)
            {
                float s = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f;
                return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            {
                float s = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f;
                return new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }
        }

        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float nx = q.x * 2f, ny = q.y * 2f, nz = q.z * 2f;
            float xx = q.x * nx, yy = q.y * ny, zz = q.z * nz;
            float xy = q.x * ny, xz = q.x * nz, yz = q.y * nz;
            float wx = q.w * nx, wy = q.w * ny, wz = q.w * nz;
            return new Vector3(
                (1f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1f - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1f - (xx + yy)) * v.z);
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color black => new Color(0, 0, 0);
        public static Color clear => new Color(0, 0, 0, 0);
    }

    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public static readonly float Epsilon = float.Epsilon;

        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Log(float f, float b) => (float)Math.Log(f, b);
        public static float Log10(float f) => (float)Math.Log10(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int f) => Math.Abs(f);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(float a, float b, float c) => Max(Max(a, b), c);
        public static float Max(params float[] v) { float m = v[0]; foreach (var f in v) if (f > m) m = f; return m; }
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Max(params int[] v) { int m = v[0]; foreach (var f in v) if (f > m) m = f; return m; }
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Min(float a, float b, float c) => Min(Min(a, b), c);
        public static float Min(params float[] v) { float m = v[0]; foreach (var f in v) if (f < m) m = f; return m; }
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Min(params int[] v) { int m = v[0]; foreach (var f in v) if (f < m) m = f; return m; }
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t); t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
        public static float Repeat(float t, float l) => Clamp(t - (float)Math.Floor(t / l) * l, 0f, l);
        public static float PingPong(float t, float l) { t = Repeat(t, l * 2f); return l - Math.Abs(t - l); }
        public static float DeltaAngle(float c, float t) { float d = Repeat(t - c, 360f); if (d > 180f) d -= 360f; return d; }

        // Unity: (int)Math.Round(f) - banker's rounding, so round(0.5) == 0 and round(1.5) == 2.
        public static int RoundToInt(float f) => (int)Math.Round((double)f, MidpointRounding.ToEven);
        public static float Round(float f) => (float)Math.Round((double)f, MidpointRounding.ToEven);
        public static int FloorToInt(float f) => (int)Math.Floor((double)f);
        public static int CeilToInt(float f) => (int)Math.Ceiling((double)f);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);
        public static bool Approximately(float a, float b) =>
            Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8);
        public static float PerlinNoise(float x, float y) => 0.5f; // never reached by a photographed arena
    }

    /// <summary>NOT Unity's generator (see the header). Deterministic so a stray caller cannot
    /// make the picture flicker between runs, and loud so it cannot hide.</summary>
    public static class Random
    {
        static System.Random _r = new System.Random(1);
        public static int Draws;
        public static void InitState(int seed) => _r = new System.Random(seed);
        public static float value { get { Draws++; return (float)_r.NextDouble(); } }
        public static float Range(float a, float b) { Draws++; return a + (float)_r.NextDouble() * (b - a); }
        public static int Range(int a, int b) { Draws++; return b <= a ? a : _r.Next(a, b); }
        public static Vector3 onUnitSphere { get { Draws++; return new Vector3((float)_r.NextDouble() * 2 - 1, (float)_r.NextDouble() * 2 - 1, (float)_r.NextDouble() * 2 - 1).normalized; } }
        public static Vector3 insideUnitSphere => onUnitSphere * value;
        public static Quaternion rotation => Quaternion.LookRotation(onUnitSphere, Vector3.up);
    }

    public static class Debug
    {
        public static void Log(object o) => Console.Error.WriteLine($"[log] {o}");
        public static void LogWarning(object o) => Console.Error.WriteLine($"[warn] {o}");
        // An authoring guard firing is a FAILURE of the arena, not a note: Program counts these
        // and exits non-zero, so a picture is never taken of an arena that refused to build.
        public static void LogError(object o) { Console.Error.WriteLine($"[error] {o}"); Harness.Faults++; }
        public static void Assert(bool c, object o = null) { if (!c) LogError(o ?? "assertion failed"); }
    }

    public static class Harness { public static int Faults; }

    public static class Application { public static bool isPlaying => false; public static bool isEditor => false; }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class MinAttribute : Attribute { public MinAttribute(float a) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class SpaceAttribute : Attribute { public SpaceAttribute() { } public SpaceAttribute(float h) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class TextAreaAttribute : Attribute { public TextAreaAttribute() { } public TextAreaAttribute(int a, int b) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class ContextMenu : Attribute { public ContextMenu(string s) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; public int order; }

    // ── inert engine objects ────────────────────────────────────────────────────────────
    public class Object
    {
        public string name = "";
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
        public static void Destroy(Object o) { }
        public static T Instantiate<T>(T original, Transform parent) where T : Object { Debug.LogError("Instantiate reached from the card-art harness"); return original; }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform;
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public class Transform : Component
    {
        public Vector3 position, localPosition, localScale = Vector3.one;
        public Quaternion rotation = Quaternion.identity, localRotation = Quaternion.identity;
        public Transform parent;
        public void SetParent(Transform p, bool w = true) { parent = p; }
    }
    public class GameObject : Object
    {
        public Transform transform = new Transform();
        public GameObject() { transform.gameObject = this; }
        public GameObject(string n) : this() { name = n; }
        public void SetActive(bool b) { }
        public T AddComponent<T>() where T : Component, new() => new T();
    }
}

namespace UnityEngine.Serialization
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class FormerlySerializedAsAttribute : Attribute { public FormerlySerializedAsAttribute(string s) { } }
}
