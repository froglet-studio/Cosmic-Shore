// Runs every [Test] and every [TestCase] row of the compiled fixtures by reflection and prints a pass/fail line per test.
// Exit 0 = all passed.
using System;
using System.Linq;
using System.Reflection;
public static class Runner
{
    public static int Main(string[] args)
    {
        int pass = 0, fail = 0;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes().Where(t => t.GetCustomAttribute<NUnit.Framework.TestFixtureAttribute>() != null))
            foreach (var m in type.GetMethods())
            {
                var rows = m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().ToList();
                bool plain = m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null;
                if (!plain && rows.Count == 0) continue;
                var ps = m.GetParameters();
                var calls = plain ? new[] { new object[0] }
                    : rows.Select(r => r.Arguments.Select((a, i) => Convert.ChangeType(a, ps[i].ParameterType)).ToArray()).ToArray();
                for (int c = 0; c < calls.Length; c++)
                {
                    string name = $"{type.Name}.{m.Name}" + (plain ? "" : $"#{c}");
                    try { m.Invoke(Activator.CreateInstance(type), calls[c]); pass++; Console.WriteLine($"PASS {name}"); }
                    catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {name}: {e.InnerException?.Message}"); }
                }
            }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 && pass > 0 ? 0 : 1;
    }
}
