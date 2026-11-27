// Runs every [Test] of the compiled SHIPPED Riposte tests (RiposteDuelTests) by reflection and prints a pass/fail line
// per test. Exit 0 = all passed.
using System;
using System.Linq;
using System.Reflection;

public static class Runner
{
    public static int Main()
    {
        int failed = 0, ran = 0;
        foreach (var type in typeof(Runner).Assembly.GetTypes().OrderBy(t => t.FullName))
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                         .Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null).OrderBy(m => m.Name))
            {
                ran++;
                try
                {
                    method.Invoke(Activator.CreateInstance(type), null);
                    Console.WriteLine($"ok    {type.Name}.{method.Name}");
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine($"FAIL  {type.Name}.{method.Name}: {e.InnerException?.Message}");
                }
            }
        Console.WriteLine($"{ran - failed} of {ran} passed");
        return failed == 0 && ran > 0 ? 0 : 1;
    }
}
