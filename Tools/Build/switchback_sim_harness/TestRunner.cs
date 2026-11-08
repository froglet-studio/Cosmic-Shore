// Runs every [Test] of the compiled SHIPPED Blast Run EditMode tests by reflection ([Values] parameters expanded to their
// cartesian product) and prints one PASS / FAIL line per case. Exit 0 = every case passed. `run.sh tests [filter]` builds it.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class TestRunner
{
    static IEnumerable<object[]> Cases(MethodInfo m)
    {
        var ps = m.GetParameters();
        IEnumerable<object[]> acc = new[] { Array.Empty<object>() };
        foreach (var p in ps)
        {
            var values = p.GetCustomAttribute<ValuesAttribute>()?.Values ?? Array.Empty<object>();
            acc = acc.SelectMany(prefix => values.Select(v => prefix.Append(Convert.ChangeType(v, p.ParameterType)).ToArray())).ToList();
        }
        return acc;
    }

    public static int Main(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : null;
        int pass = 0, fail = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().OrderBy(t => t.FullName))
        foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null))
        {
            string name = $"{type.Name}.{m.Name}";
            if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            foreach (var args0 in Cases(m))
            {
                string label = args0.Length == 0 ? name : $"{name}({string.Join(", ", args0)})";
                var started = DateTime.UtcNow;
                try
                {
                    m.Invoke(m.IsStatic ? null : Activator.CreateInstance(type), args0);
                    pass++;
                    Console.WriteLine($"PASS {label} ({(DateTime.UtcNow - started).TotalSeconds:F1} s)");
                }
                catch (TargetInvocationException e)
                {
                    fail++;
                    Console.WriteLine($"FAIL {label}: {e.InnerException?.Message}");
                }
            }
        }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 && pass > 0 ? 0 : 1;
    }
}
