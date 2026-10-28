// [TestCase] for the meerkat kit harness: the brittlestar shim has [Test] only, and MeerkatWormholeMathTests uses
// [TestCase(...)] rows. Runner.cs invokes each row with its arguments converted to the method's parameter types.
using System;
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TestCaseAttribute : Attribute
    {
        public object[] Arguments { get; }
        public TestCaseAttribute(params object[] args) => Arguments = args;
    }
}
