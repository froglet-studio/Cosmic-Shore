// Runs every [Test] of the compiled SHIPPED Palisade arena tests (PalisadeArenaTests) by reflection and prints a
// pass/fail line per test. Exit 0 = all passed. One more mode reads the shipped C#, never a copy:
//   --census     one line per rung: lanes, posts by kind, frame stones, tips, rubble by kind, the straight to wind on.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;

public static class Runner
{
    static int Count(List<PalisadeArenaPart> parts, int piece, PrismKind kind) =>
        parts.Count(p => p.Piece == piece && p.Kind == kind);

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--parts")
        {
            // One JSON line per prism, every rung: what Tools/Build/palisade_arena.py --parity compares its twin with.
            static string V(UnityEngine.Vector3 v) => $"[{v.x:R},{v.y:R},{v.z:R}]";
            for (int rung = 1; rung <= 4; rung++)
            {
                var parts = new List<PalisadeArenaPart>();
                PalisadeArena.Lay(PalisadeArena.ForIntensity(rung), parts, new List<PalisadeLane>());
                foreach (var p in parts)
                    Console.WriteLine($"{{\"intensity\":{rung},\"pos\":{V(p.Position)},\"fwd\":{V(p.Forward)},\"up\":{V(p.Up)}," +
                                      $"\"scale\":{V(p.Scale)},\"kind\":{(int)p.Kind},\"piece\":{p.Piece},\"lane\":{p.Lane}}}");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "--census")
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                var parts = new List<PalisadeArenaPart>();
                var lanes = new List<PalisadeLane>();
                PalisadeArena.Lay(PalisadeArena.ForIntensity(rung), parts, lanes);
                Console.WriteLine($"I{rung}: {parts.Count} prisms, {lanes.Count} lanes" +
                    $" | posts plain {Count(parts, PalisadeArena.Post, PrismKind.Plain)} shielded {Count(parts, PalisadeArena.Post, PrismKind.Shielded)}" +
                    $" | frame stones {Count(parts, PalisadeArena.Frame, PrismKind.Shielded)} tips {Count(parts, PalisadeArena.Frame, PrismKind.Danger)}" +
                    $" | rubble shielded {Count(parts, PalisadeArena.Rubble, PrismKind.Shielded)} danger {Count(parts, PalisadeArena.Rubble, PrismKind.Danger)}" +
                    $" | straight {lanes.Sum(l => l.StraightLength):0} u ({lanes[0].StraightLength:0} a lane)");
            }
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
        return fail == 0 ? 0 : 1;
    }
}
