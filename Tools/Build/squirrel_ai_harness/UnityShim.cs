// The UnityEngine / Unity.Mathematics surface the SHIPPED Squirrel skim-racing brain and the
// shipped ShieldShellMath touch, re-implemented so the harness can COMPILE AND RUN them outside
// the editor.
//
// GEOMETRY is FAITHFUL, not merely total: the harness measures how a control loop behaves, and a
// Slerp or an AngleAxis that is "close enough" would tune the brain against a vessel the game does
// not ship. Every construction follows Unity's own (AngleAxis about a normalised axis, Slerp along
// the SHORTER arc, LookRotation z = forward / x = up x z / y = z x x, Unity's float32 arithmetic).
// Adapted from Tools/Build/card_art_harness/UnityShim.cs, which states the same standard.
using System;

namespace UnityEngine
{
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : i == 2 ? z : throw new IndexOutOfRangeException();
            set { if (i == 0) x = value; else if (i == 1) y = value; else if (i == 2) z = value; else throw new IndexOutOfRangeException(); }
        }

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);

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
        public static Vector3 positiveInfinity => new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) =>
            new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float SqrMagnitude(Vector3 a) => a.sqrMagnitude;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public static Vector3 Project(Vector3 v, Vector3 n)
        {
            float d = Dot(n, n);
            return d < 1e-15f ? zero : n * (Dot(v, n) / d);
        }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
        {
            float d = Dot(n, n);
            return d < 1e-15f ? v : v - n * (Dot(v, n) / d);
        }
        public static Vector3 ClampMagnitude(Vector3 v, float max) =>
            v.sqrMagnitude > max * max ? v.normalized * max : v;
        public static float Angle(Vector3 a, Vector3 b)
        {
            float d = (float)Math.Sqrt(a.sqrMagnitude * b.sqrMagnitude);
            if (d < 1e-15f) return 0f;
            return (float)Math.Acos(Mathf.Clamp(Dot(a, b) / d, -1f, 1f)) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float a = Angle(from, to);
            float s = Math.Sign(Dot(axis, Cross(from, to)));
            return a * (s == 0f ? 1f : s);
        }
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            float ma = a.magnitude, mb = b.magnitude;
            if (ma < 1e-12f || mb < 1e-12f) return LerpUnclamped(a, b, t);
            Vector3 na = a / ma, nb = b / mb;
            float d = Mathf.Clamp(Dot(na, nb), -1f, 1f);
            float th = (float)Math.Acos(d);
            float m = ma + (mb - ma) * t;
            if (th < 1e-5f) return LerpUnclamped(na, nb, t).normalized * m;
            float s = (float)Math.Sin(th);
            Vector3 dir = (na * (float)Math.Sin((1f - t) * th) + nb * (float)Math.Sin(t * th)) / s;
            return dir * m;
        }
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d)
        {
            Vector3 v = t - c; float m = v.magnitude;
            return m <= d || m < 1e-12f ? t : c + v / m * d;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator *(float f, Vector3 a) => a * f;
        public static Vector3 operator /(Vector3 a, float f) => new Vector3(a.x / f, a.y / f, a.z / f);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public bool Equals(Vector3 o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float f) => new Vector2(a.x * f, a.y * f);
        public static Vector2 operator *(float f, Vector2 a) => a * f;
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public override string ToString() => $"({x:F2}, {y:F2})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public static Quaternion AngleAxis(float angleDeg, Vector3 axis)
        {
            var n = axis.normalized;
            if (n.sqrMagnitude < 1e-12f) return identity;
            float half = angleDeg * Mathf.Deg2Rad * 0.5f;
            float s = (float)Math.Sin(half);
            return new Quaternion(n.x * s, n.y * s, n.z * s, (float)Math.Cos(half));
        }

        public static Quaternion Euler(float ex, float ey, float ez) =>
            AngleAxis(ey, Vector3.up) * AngleAxis(ex, Vector3.right) * AngleAxis(ez, Vector3.forward);

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

        public void ToAngleAxis(out float angleDeg, out Vector3 axis)
        {
            var q = normalized;
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            float s = (float)Math.Sqrt(Math.Max(0f, 1f - q.w * q.w));
            angleDeg = 2f * (float)Math.Acos(Mathf.Clamp(q.w, -1f, 1f)) * Mathf.Rad2Deg;
            axis = s < 1e-6f ? Vector3.right : new Vector3(q.x / s, q.y / s, q.z / s);
        }

        static Quaternion FromBasis(Vector3 X, Vector3 Y, Vector3 Z)
        {
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
        public override string ToString() => $"({x:F4}, {y:F4}, {z:F4}, {w:F4})";
    }

    public struct Pose
    {
        public Vector3 position; public Quaternion rotation;
        public Pose(Vector3 p, Quaternion r) { position = p; rotation = r; }
    }

    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public const float Infinity = float.PositiveInfinity;
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
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int f) => Math.Abs(f);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(float a, float b, float c) => Max(Max(a, b), c);
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Min(float a, float b, float c) => Min(Min(a, b), c);
        public static int Min(int a, int b) => a < b ? a : b;
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
        public static float DeltaAngle(float c, float t) { float d = Repeat(t - c, 360f); if (d > 180f) d -= 360f; return d; }
        public static int RoundToInt(float f) => (int)Math.Round((double)f, MidpointRounding.ToEven);
        public static float Round(float f) => (float)Math.Round((double)f, MidpointRounding.ToEven);
        public static int FloorToInt(float f) => (int)Math.Floor((double)f);
        public static int CeilToInt(float f) => (int)Math.Ceiling((double)f);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);
        public static bool Approximately(float a, float b) =>
            Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8);
    }

    public static class Debug
    {
        public static void Log(object o) => Console.Error.WriteLine($"[log] {o}");
        public static void LogWarning(object o) => Console.Error.WriteLine($"[warn] {o}");
        public static void LogError(object o) => Console.Error.WriteLine($"[error] {o}");
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)] public class MinAttribute : Attribute { public MinAttribute(float a) { } }
}

namespace Unity.Mathematics
{
    // Just enough to compile the SHIPPED ShieldShellMath.cs unchanged (the harness uses it as the
    // ground truth for every prism contact - skimmer and hull alike).
    public struct float3
    {
        public float x, y, z;
        public float3(float a, float b, float c) { x = a; y = b; z = c; }
        public float3(float a) { x = y = z = a; }
        public static float3 operator +(float3 a, float3 b) => new float3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static float3 operator -(float3 a, float3 b) => new float3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static float3 operator -(float3 a) => new float3(-a.x, -a.y, -a.z);
        public static float3 operator *(float3 a, float s) => new float3(a.x * s, a.y * s, a.z * s);
        public static float3 operator *(float s, float3 a) => new float3(a.x * s, a.y * s, a.z * s);
        public static float3 operator *(float3 a, float3 b) => new float3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static float3 operator /(float3 a, float s) => new float3(a.x / s, a.y / s, a.z / s);
    }
    public struct float4
    {
        public float x, y, z, w;
        public float4(float a, float b, float c, float d) { x = a; y = b; z = c; w = d; }
        public static float4 operator +(float4 a, float4 b) => new float4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        public static float4 operator -(float4 a, float4 b) => new float4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        public static float4 operator *(float4 a, float s) => new float4(a.x * s, a.y * s, a.z * s, a.w * s);
    }
    public struct quaternion
    {
        public float x, y, z, w;
        public quaternion(float a, float b, float c, float d) { x = a; y = b; z = c; w = d; }
        public static quaternion identity => new quaternion(0f, 0f, 0f, 1f);
    }
    public static class math
    {
        public static float3 abs(float3 a) => new float3(Math.Abs(a.x), Math.Abs(a.y), Math.Abs(a.z));
        public static float abs(float a) => Math.Abs(a);
        public static float4 abs(float4 a) => new float4(Math.Abs(a.x), Math.Abs(a.y), Math.Abs(a.z), Math.Abs(a.w));
        public static float cmax(float3 a) => Math.Max(a.x, Math.Max(a.y, a.z));
        public static float cmin(float3 a) => Math.Min(a.x, Math.Min(a.y, a.z));
        public static float cmax(float4 a) => Math.Max(Math.Max(a.x, a.y), Math.Max(a.z, a.w));
        public static float cmin(float4 a) => Math.Min(Math.Min(a.x, a.y), Math.Min(a.z, a.w));
        public static float dot(float3 a, float3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float3 cross(float3 a, float3 b) => new float3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float lengthsq(float3 a) => dot(a, a);
        public static float length(float3 a) => (float)Math.Sqrt(dot(a, a));
        public static float distancesq(float3 a, float3 b) => lengthsq(a - b);
        public static float max(float a, float b) => Math.Max(a, b);
        public static float min(float a, float b) => Math.Min(a, b);
        public static float3 max(float3 a, float3 b) => new float3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public static float3 min(float3 a, float3 b) => new float3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static float4 max(float4 a, float4 b) => new float4(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z), Math.Max(a.w, b.w));
        public static float4 min(float4 a, float4 b) => new float4(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z), Math.Min(a.w, b.w));
        public static float clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v));
        public static float3 clamp(float3 v, float a, float b) => new float3(clamp(v.x, a, b), clamp(v.y, a, b), clamp(v.z, a, b));
        public static float3 mul(quaternion q, float3 v)
        {
            float3 u = new float3(q.x, q.y, q.z);
            return u * (2f * dot(u, v)) + v * (q.w * q.w - dot(u, u)) + cross(u, v) * (2f * q.w);
        }
    }
}
