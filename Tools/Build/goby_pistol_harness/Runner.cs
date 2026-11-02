// Runs every [Test] in the compiled assembly by reflection and prints a pass/fail line per test. Exit 0 = all passed.
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
                var cases = m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Select(c => c.Args).ToList();
                if (m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null) cases.Add(null);
                foreach (var caseArgs in cases)
                {
                    string name = caseArgs == null ? m.Name : $"{m.Name}({string.Join(", ", caseArgs)})";
                    try { m.Invoke(Activator.CreateInstance(type), caseArgs); pass++; Console.WriteLine($"PASS {type.Name}.{name}"); }
                    catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {type.Name}.{name}: {e.InnerException?.Message}"); }
                }
            }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 && pass > 0 ? 0 : 1;
    }
}
