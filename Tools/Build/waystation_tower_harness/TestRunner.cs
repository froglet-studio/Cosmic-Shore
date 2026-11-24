// Runs every [Test] of the compiled SHIPPED Waystation tests (WaystationTowerTests, WaystationHopPlanTests) by reflection
// and prints a pass / fail / inconclusive line per test. Exit 0 = none failed and at least one passed. The tests read
// TigerMoth.prefab and TigerMothDropAction.asset by repo-relative path, so run.sh runs this from the repo root.
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

public static class TestRunner
{
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        int pass = 0, fail = 0, inconclusive = 0;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes().Where(t => t.Namespace == "CosmicShore.Tests").OrderBy(t => t.Name))
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                try { m.Invoke(Activator.CreateInstance(type), null); pass++; Console.WriteLine($"PASS {type.Name}.{m.Name}"); }
                catch (TargetInvocationException e) when (e.InnerException is NUnit.Framework.InconclusiveException)
                {
                    inconclusive++;
                    Console.WriteLine($"INCONCLUSIVE {type.Name}.{m.Name}: {e.InnerException.Message}");
                }
                catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        Console.WriteLine($"{pass} passed, {fail} failed, {inconclusive} inconclusive");
        return fail == 0 && pass > 0 ? 0 : 1;
    }
}
