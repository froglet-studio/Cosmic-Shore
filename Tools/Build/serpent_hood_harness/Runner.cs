// Runs every [Test] of the compiled SHIPPED SerpentHoodLayoutTests by reflection (a pass/fail line per test; exit 0 =
// all passed), or with --parts dumps every prism of every cover at intensities 1-4 as JSON lines, which
// Tools/Build/serpent_hood_budget.py --parity holds its Python mirror to. Reads the shipped C#, never a copy.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--parts")
        {
            for (int i = 1; i <= 4; i++)
            {
                var covers = new List<SerpentHoodLayout.Cover>();
                SerpentHoodLayout.Covers(Vector3.zero, i, covers);
                foreach (var c in covers)
                {
                    var parts = new List<SerpentineGate.Part>();
                    float reach = SerpentHoodLayout.Build(c, i, parts);
                    Console.WriteLine($"{{\"i\":{i},\"cover\":{c.Index},\"shutter\":{(c.Shutter ? "true" : "false")},\"c\":{V(c.Centre)},\"axis\":{V(c.Axis)},\"reach\":{F(reach)},\"parts\":[" +
                        string.Join(",", parts.Select(p => $"[{V(p.Position)},{V(p.Rotation * Vector3.forward)},{V(p.Rotation * Vector3.up)},{V(p.Scale)},{(int)p.Kind},{(p.Core ? 1 : 0)}]")) + "]}");
                }
            }
            for (int k = 0; k < 24; k++) Console.WriteLine($"{{\"field\":{k},\"p\":{V(SerpentHoodLayout.FieldSlot(Vector3.zero, k, 24, 380f))}}}");
            return 0;
        }

        int pass = 0, fail = 0;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes().Where(t => t.Namespace == "CosmicShore.Tests").OrderBy(t => t.Name))
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                try { m.Invoke(Activator.CreateInstance(type), null); pass++; Console.WriteLine($"PASS {type.Name}.{m.Name}"); }
                catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 && pass > 0 ? 0 : 1;
    }
}
