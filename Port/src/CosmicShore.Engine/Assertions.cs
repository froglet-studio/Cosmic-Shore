using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Assertions
{
    /// <summary>Original contract: assertion failures raise this (not a hard crash).</summary>
    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    /// <summary>
    /// Original contract (UnityEngine.Assertions.Assert): equality asserts route
    /// through EqualityComparer, reference-type NotEqual uses the comparer too, and
    /// a failure logs an assertion error rather than throwing in release players —
    /// here failures always throw AssertionException, the strict development-build
    /// posture (fail loud, per project policy).
    /// </summary>
    public static class Assert
    {
        public static void IsTrue(bool condition, string message = null)
        {
            if (!condition) Fail(message ?? "Assert.IsTrue failed.");
        }

        public static void IsFalse(bool condition, string message = null)
        {
            if (condition) Fail(message ?? "Assert.IsFalse failed.");
        }

        public static void IsNull<T>(T value, string message = null) where T : class
        {
            if (!IsNativeNull(value)) Fail(message ?? $"Assert.IsNull failed. Value: {value}");
        }

        public static void IsNotNull<T>(T value, string message = null) where T : class
        {
            if (IsNativeNull(value)) Fail(message ?? "Assert.IsNotNull failed.");
        }

        public static void AreEqual<T>(T expected, T actual, string message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                Fail(message ?? $"Assert.AreEqual failed. Expected: {expected}, Actual: {actual}");
        }

        public static void AreNotEqual<T>(T expected, T actual, string message = null)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
                Fail(message ?? $"Assert.AreNotEqual failed. Both values: {actual}");
        }

        // Engine Objects compare fake-null once destroyed; honor that here so
        // Assert.IsNull/IsNotNull behave like the original over scene objects.
        static bool IsNativeNull(object value)
            => value is null || (value is Object obj && obj.IsDestroyed);

        static void Fail(string message) => throw new AssertionException(message);
    }
}
