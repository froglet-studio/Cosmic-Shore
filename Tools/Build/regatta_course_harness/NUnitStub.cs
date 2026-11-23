// A minimal NUnit stub: just the attributes and asserts RegattaCourseFeaturesTests uses, so features.sh can compile and
// RUN that EditMode test file offline (no Unity, no NUnit package). A failed assert throws; FeaturesHarness reports it.
using System;
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute {}
    public class AssertionException : Exception { public AssertionException(string m) : base(m) {} }
    public static class Assert
    {
        static void Fail(string what, string msg) => throw new AssertionException(what + (string.IsNullOrEmpty(msg) ? "" : ": " + msg));
        public static void IsTrue(bool c, string m = null) { if (!c) Fail("expected true", m); }
        public static void IsFalse(bool c, string m = null) { if (c) Fail("expected false", m); }
        public static void AreEqual(object e, object a, string m = null) { if (!Equals(e, a)) Fail($"expected {e} but was {a}", m); }
        public static void AreNotEqual(object e, object a, string m = null) { if (Equals(e, a)) Fail($"expected not {e}", m); }
        public static void Less(float a, float b, string m = null) { if (!(a < b)) Fail($"expected {a} < {b}", m); }
        public static void LessOrEqual(float a, float b, string m = null) { if (!(a <= b)) Fail($"expected {a} <= {b}", m); }
        public static void Greater(float a, float b, string m = null) { if (!(a > b)) Fail($"expected {a} > {b}", m); }
        public static void GreaterOrEqual(float a, float b, string m = null) { if (!(a >= b)) Fail($"expected {a} >= {b}", m); }
    }
}
