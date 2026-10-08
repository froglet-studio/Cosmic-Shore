// REAL maths, Unity-shaped. The Tether's physics (TetherMath, AutoTetherRig, LongTetherRope) is
// pure arithmetic over Vector3 and Mathf, so a faithful pair lets the SHIPPED files and the
// SHIPPED edit-mode tests be executed here rather than transcribed. Only the members those
// files use are implemented; a new call in them fails this compile, which is the point.
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        // Unity returns zero below 1e-5; matched so a degenerate normalize behaves the same.
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b)
            => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public static float Abs(float f) => Math.Abs(f);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);   // Unity clamps t
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
    }
}

// The slice of NUnit the Tether's tests use. Assertion failures throw, the runner reports them.
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute { }

    public sealed class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public static class Assert
    {
        static void Fail(string what, string message) => throw new AssertionException(what + (string.IsNullOrEmpty(message) ? "" : " — " + message));
        public static void AreEqual(float expected, float actual, float delta, string message = null)
        { if (!(Math.Abs(expected - actual) <= delta)) Fail($"expected {expected} ±{delta}, got {actual}", message); }
        public static void Greater(float a, float b, string message = null) { if (!(a > b)) Fail($"{a} is not > {b}", message); }
        public static void Greater(int a, int b, string message = null) { if (!(a > b)) Fail($"{a} is not > {b}", message); }
        public static void GreaterOrEqual(float a, float b, string message = null) { if (!(a >= b)) Fail($"{a} is not >= {b}", message); }
        public static void Less(float a, float b, string message = null) { if (!(a < b)) Fail($"{a} is not < {b}", message); }
        public static void LessOrEqual(float a, float b, string message = null) { if (!(a <= b)) Fail($"{a} is not <= {b}", message); }
        public static void IsTrue(bool c, string message = null) { if (!c) Fail("expected true", message); }
        public static void IsFalse(bool c, string message = null) { if (c) Fail("expected false", message); }
    }
}
