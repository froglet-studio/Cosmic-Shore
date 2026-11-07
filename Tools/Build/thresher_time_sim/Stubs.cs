// Copied from ../thresher_chain_harness/Stubs.cs (plus RotateTowards, Angle, Rad2Deg, which the shipped solver
// file now uses) so this probe does not depend on that harness's own state.
// REAL maths, not type-check stubs: ThresherChainSolver is pure arithmetic over Vector3/Mathf, so a
// faithful Vector3 + Mathf lets the SHIPPED solver and the SHIPPED edit-mode tests be executed
// here rather than transcribed. The NUnit half is the minimum the test file uses.
using System;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() => this = normalized;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = t < 0 ? 0 : (t > 1 ? 1 : t); return a + (b - a) * t; }
        public static Vector3 ClampMagnitude(Vector3 v, float max) { float m = v.magnitude; return m > max ? v * (max / m) : v; }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { float d = Dot(n, n); return d < 1e-15f ? v : v - n * (Dot(v, n) / d); }
        public static float Angle(Vector3 a, Vector3 b) { float d = (float)Math.Sqrt(a.sqrMagnitude * b.sqrMagnitude); return d < 1e-15f ? 0f : (float)(Math.Acos(Math.Max(-1.0, Math.Min(1.0, Dot(a, b) / d))) * 180.0 / Math.PI); }
        public static Vector3 RotateTowards(Vector3 current, Vector3 target, float maxRadians, float maxMag)
        {
            Vector3 a = current.normalized, b = target.normalized;
            float ang = (float)Math.Acos(Math.Max(-1.0, Math.Min(1.0, Dot(a, b))));
            if (ang <= maxRadians || ang < 1e-6f) return b * current.magnitude;
            Vector3 axis = Cross(a, b);
            if (axis.sqrMagnitude < 1e-12f) axis = Math.Abs(a.x) < 0.9f ? Cross(a, right) : Cross(a, up);
            axis = axis.normalized;
            float c = (float)Math.Cos(maxRadians), s = (float)Math.Sin(maxRadians);
            Vector3 r = a * c + Cross(axis, a) * s + axis * (Dot(axis, a) * (1 - c));
            return r * current.magnitude;
        }
        public override string ToString() => $"({x:F2},{y:F2},{z:F2})";
    }
    public static class Mathf
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float PI = (float)Math.PI;
        public const float Rad2Deg = (float)(180.0 / Math.PI);
        public static float Abs(float f) => f < 0 ? -f : f;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Atan(float v) => (float)Math.Atan(v);
        public static float Acos(float v) => (float)Math.Acos(v);
        public static float Asin(float v) => (float)Math.Asin(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = t * t * (3f - 2f * t); return a + (b - a) * t; }
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static float MoveTowards(float a, float b, float d) => Abs(b - a) <= d ? b : a + (b > a ? d : -d);
        public static bool Approximately(float a, float b) => Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), 1.1E-44f * 8f);
    }
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float f) { } }
}
