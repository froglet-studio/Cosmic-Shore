// REAL maths, not type-check stubs: FlailChainSolver is pure arithmetic over Vector3/Mathf, so a
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
        public static Vector3 ClampMagnitude(Vector3 v, float max) { float m = v.magnitude; return m > max ? v * (max / m) : v; }
        public override string ToString() => $"({x:F2},{y:F2},{z:F2})";
    }
    public static class Mathf
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public static float Abs(float f) => f < 0 ? -f : f;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static float MoveTowards(float a, float b, float d) => Abs(b - a) <= d ? b : a + (b > a ? d : -d);
        public static bool Approximately(float a, float b) => Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), 1.1E-44f * 8f);
    }
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float f) { } }
}
namespace NUnit.Framework
{
    using System;
    public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TestCaseAttribute : Attribute { public object[] Args; public TestCaseAttribute(params object[] a) { Args = a; } }
    public sealed class AssertionException : Exception { public AssertionException(string m) : base(m) { } }
    public static class Assert
    {
        static void Fail(string m) => throw new AssertionException(m);
        public static void Less(float a, float b, string m = "") { if (!(a < b)) Fail($"expected {a} < {b}. {m}"); }
        public static void Greater(float a, float b, string m = "") { if (!(a > b)) Fail($"expected {a} > {b}. {m}"); }
        public static void LessOrEqual(float a, float b, string m = "") { if (!(a <= b)) Fail($"expected {a} <= {b}. {m}"); }
        public static void GreaterOrEqual(float a, float b, string m = "") { if (!(a >= b)) Fail($"expected {a} >= {b}. {m}"); }
        public static void LessOrEqual(int a, int b, string m = "") { if (!(a <= b)) Fail($"expected {a} <= {b}. {m}"); }
        public static void AreEqual(float e, float a, float d, string m = "") { if (Math.Abs(e - a) > d) Fail($"expected {e} got {a} (±{d}). {m}"); }
        public static void AreEqual(object e, object a, string m = "") { if (!Equals(e, a)) Fail($"expected {e} got {a}. {m}"); }
        public static void IsTrue(bool c, string m = "") { if (!c) Fail("expected true. " + m); }
        public static void IsFalse(bool c, string m = "") { if (c) Fail("expected false. " + m); }
    }
}
