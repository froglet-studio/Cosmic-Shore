// Runs every [Test] in the shipped TetherMathTests by reflection; exits 1 on any failure.
using System;
using System.Linq;
using System.Reflection;

static class Runner
{
    static int Main()
    {
        var fixture = typeof(CosmicShore.Tests.TetherMathTests);
        var tests = fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null).ToList();
        int failed = 0;
        foreach (var m in tests)
        {
            try { m.Invoke(Activator.CreateInstance(fixture), null); Console.WriteLine($"  PASS  {m.Name}"); }
            catch (TargetInvocationException e)
            {
                failed++;
                Console.WriteLine($"  FAIL  {m.Name}: {e.InnerException?.Message}");
            }
        }
        Console.WriteLine(failed == 0 ? $"ALL GREEN ({tests.Count} tests)" : $"{failed} of {tests.Count} FAILED");
        return failed == 0 ? 0 : 1;
    }
}
