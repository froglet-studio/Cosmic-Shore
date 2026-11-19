// A minimal NUnit surface for Tools/Build/spearline_harness (copied from headlong_course_harness): the Assert calls the Spearline tests use,
// with NUnit's semantics (delta comparisons inclusive, messages appended). Failures throw; Runner.cs reports them.
using System;
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }

    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public static class Assert
    {
        internal static void Fail(string what, string msg) => throw new AssertionException(msg == null ? what : $"{what} — {msg}");
        public static void IsTrue(bool c, string msg = null) { if (!c) Fail("expected true", msg); }
        public static void IsFalse(bool c, string msg = null) { if (c) Fail("expected false", msg); }
        public static void IsNull(object o, string msg = null) { if (o != null) Fail($"expected null, was {o}", msg); }
        public static void IsNotNull(object o, string msg = null) { if (o == null) Fail("expected not null", msg); }
        public static void AreEqual(float e, float a, float d, string msg = null) { if (!(Math.Abs(e - a) <= d)) Fail($"expected {e} ± {d}, was {a}", msg); }
        public static void AreEqual(double e, double a, double d, string msg = null) { if (!(Math.Abs(e - a) <= d)) Fail($"expected {e} ± {d}, was {a}", msg); }
        public static void AreEqual(object e, object a, string msg = null) { if (!Equals(e, a)) Fail($"expected {e}, was {a}", msg); }
        public static void AreNotEqual(object e, object a, string msg = null) { if (Equals(e, a)) Fail($"expected not {e}", msg); }
        public static void Greater(double a, double b, string msg = null) { if (!(a > b)) Fail($"expected {a} > {b}", msg); }
        public static void Less(double a, double b, string msg = null) { if (!(a < b)) Fail($"expected {a} < {b}", msg); }
        public static void GreaterOrEqual(double a, double b, string msg = null) { if (!(a >= b)) Fail($"expected {a} >= {b}", msg); }
        public static void LessOrEqual(double a, double b, string msg = null) { if (!(a <= b)) Fail($"expected {a} <= {b}", msg); }
    }

    public static class CollectionAssert
    {
        public static void AreEqual(System.Collections.IEnumerable e, System.Collections.IEnumerable a, string msg = null)
        {
            var ea = e.GetEnumerator(); var aa = a.GetEnumerator(); int i = 0;
            while (true)
            {
                bool me = ea.MoveNext(), ma = aa.MoveNext();
                if (!me && !ma) return;
                if (me != ma) Assert.Fail($"collections differ in length at {i}", msg);
                if (!Equals(ea.Current, aa.Current)) Assert.Fail($"collections differ at {i}: {ea.Current} vs {aa.Current}", msg);
                i++;
            }
        }
        public static void Contains(System.Collections.IEnumerable c, object item, string msg = null)
        {
            foreach (var x in c) if (Equals(x, item)) return;
            Assert.Fail($"collection does not contain {item}", msg);
        }
    }
}
