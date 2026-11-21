// Runs every [Test] in the compiled assembly by reflection and prints a PASS/FAIL line per test. Exit 0 = all passed.
// (The Brittle Star harness's runner without its OBJ export; NUnitShim.cs and Stubs.cs are shared from there.)
using System;
using System.Linq;
using System.Reflection;
public static class Runner
{
    public static int Main(string[] args)
    {
        int failed = 0, ran = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetCustomAttributes(typeof(NUnit.Framework.TestFixtureAttribute), false).Length > 0))
        {
            object fixture = Activator.CreateInstance(type);
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute), false).Length > 0))
            {
                ran++;
                try { m.Invoke(fixture, null); Console.WriteLine($"PASS {type.Name}.{m.Name}"); }
                catch (TargetInvocationException e) { failed++; Console.WriteLine($"FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        }
        Console.WriteLine($"{ran - failed}/{ran} passed");
        return failed == 0 && ran > 0 ? 0 : 1;
    }
}
