// Faithful re-implementations of the UnityEngine surface the four Cleave arena generators use.
//
// This is a SHIM, not a stub: every function here has to produce the same number Unity's does,
// because the arena sources compiled beside it are the shipped ones and their prism counts are
// what the cell configs' PhaseThresholds ride on. Where Unity's behaviour is subtle it is
// reproduced and commented rather than approximated - Mathf.RoundToInt's banker's rounding and
// Vector3.normalized's epsilon are both load-bearing for the counts.
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);

        // Unity returns ZERO (not NaN) below kEpsilon rather than dividing through.
        const float kEpsilon = 1e-5f;
        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > kEpsilon ? new Vector3(x / m, y / m, z / m) : zero;
            }
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator *(float f, Vector3 a) => a * f;
        public static Vector3 operator /(Vector3 a, float f) => new Vector3(a.x / f, a.y / f, a.z / f);

        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public override int GetHashCode() => HashCode.Combine(x, y);
    }

    /// <summary>Only the parts the arenas use. Rotations do not affect prism COUNTS or VOLUMES,
    /// but AngleAxis does - every pane and sheet normal is built from one.</summary>
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

        /// <summary>Counts do not depend on the stored rotation, so this only has to be total and
        /// non-throwing. It is NOT a faithful LookRotation and must never be used for geometry.</summary>
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => identity;
    }

    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;

        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int f) => Math.Abs(f);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        // Unity: (int)Math.Round(f) - Math.Round's default is MidpointRounding.ToEven, so
        // round(0.5) == 0 and round(1.5) == 2. Several rib/hoop counts land exactly on .5.
        public static int RoundToInt(float f) => (int)Math.Round((double)f, MidpointRounding.ToEven);
        public static int FloorToInt(float f) => (int)Math.Floor((double)f);
        public static int CeilToInt(float f) => (int)Math.Ceiling((double)f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static bool Approximately(float a, float b) =>
            Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8);
    }

    public static class Debug
    {
        public static void Log(object o) => Console.Error.WriteLine($"[log] {o}");
        public static void LogWarning(object o) => Console.Error.WriteLine($"[warn] {o}");
        // An authoring guard firing is a FAILURE of the arena, not a note - Program.cs counts these
        // and exits non-zero, so a band that grew past the envelope cannot be measured and shipped.
        public static void LogError(object o) { Console.Error.WriteLine($"[error] {o}"); Harness.Faults++; }
    }

    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.All)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.All)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.All)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }

    public static class Harness { public static int Faults; }
}

namespace CosmicShore.Utility
{
    /// <summary>Shim for the project logger. An arena's authoring guard firing (e.g.
    /// SpawnableRibcage.AssertNoGapScale) is a FAILURE of the ladder, not a note, so an error
    /// here counts a fault and Program.cs exits non-zero - a table that has drifted past what a
    /// generator can honour cannot be measured and shipped.</summary>
    public static class CSDebug
    {
        public static void Log(object o) => Console.Error.WriteLine($"[log] {o}");
        public static void LogWarning(object o) => Console.Error.WriteLine($"[warn] {o}");
        public static void LogError(object o) { Console.Error.WriteLine($"[error] {o}"); UnityEngine.Harness.Faults++; }
    }
}
