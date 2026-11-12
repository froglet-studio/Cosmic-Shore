// The NUnit surface the SHIPPED Blast Run EditMode tests use (SwitchbackBlastRunTests, SwitchbackBlastPlannerTests), so
// `run.sh tests` runs them headless against the Skim Race harness's Unity shim. NUnit's semantics: [Values] parameters
// expand to their cartesian product, failures throw, messages are appended.
using System;
using System.Collections;
using System.Collections.Generic;
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ValuesAttribute : Attribute
    {
        public readonly object[] Values;
        public ValuesAttribute(params object[] values) { Values = values; }
    }

    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public sealed class RangeConstraint
    {
        public readonly double Lo, Hi;
        public RangeConstraint(double lo, double hi) { Lo = lo; Hi = hi; }
    }

    public static class Is
    {
        public static RangeConstraint InRange(double lo, double hi) => new(lo, hi);
    }

    public static class Assert
    {
        static void Fail(string what, string msg) => throw new AssertionException(msg == null ? what : $"{what} - {msg}");
        public static void IsTrue(bool c, string msg = null) { if (!c) Fail("expected true", msg); }
        public static void IsFalse(bool c, string msg = null) { if (c) Fail("expected false", msg); }
        public static void IsNull(object o, string msg = null) { if (o != null) Fail($"expected null, was {o}", msg); }
        public static void IsNotNull(object o, string msg = null) { if (o == null) Fail("expected not null", msg); }
        public static void AreEqual(float e, float a, float d, string msg = null) { if (!(Math.Abs(e - a) <= d)) Fail($"expected {e} +- {d}, was {a}", msg); }
        public static void AreEqual(double e, double a, double d, string msg = null) { if (!(Math.Abs(e - a) <= d)) Fail($"expected {e} +- {d}, was {a}", msg); }
        public static void AreEqual(object e, object a, string msg = null)
        {
            if (Equals(e, a)) return;
            if (e is IConvertible && a is IConvertible && !(e is Enum) && !(a is Enum)
                && Convert.ToDouble(e) == Convert.ToDouble(a)) return;
            Fail($"expected {e}, was {a}", msg);
        }
        public static void AreNotEqual(object e, object a, string msg = null) { if (Equals(e, a)) Fail($"expected not {e}", msg); }
        public static void Greater(double a, double b, string msg = null) { if (!(a > b)) Fail($"expected {a} > {b}", msg); }
        public static void Less(double a, double b, string msg = null) { if (!(a < b)) Fail($"expected {a} < {b}", msg); }
        public static void GreaterOrEqual(double a, double b, string msg = null) { if (!(a >= b)) Fail($"expected {a} >= {b}", msg); }
        public static void LessOrEqual(double a, double b, string msg = null) { if (!(a <= b)) Fail($"expected {a} <= {b}", msg); }
        public static void IsEmpty(IEnumerable c, string msg = null)
        {
            foreach (var _ in c) { Fail("expected empty", msg); return; }
        }
        public static void IsNotEmpty(IEnumerable c, string msg = null)
        {
            foreach (var _ in c) return;
            Fail("expected not empty", msg);
        }
        public static void That(double actual, RangeConstraint r, string msg = null)
        {
            if (!(actual >= r.Lo && actual <= r.Hi)) Fail($"expected {actual} in [{r.Lo}, {r.Hi}]", msg);
        }
        public static void Fail(string msg) => throw new AssertionException(msg);
    }
}

// BlastRunKeyCrystals (compiled by `run.sh tests` for its own test) clears itself on domain reload.
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : System.Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }
}
