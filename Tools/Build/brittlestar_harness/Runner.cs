// Runs every [Test] in the compiled assembly by reflection and prints a pass/fail line per test. Exit 0 = all passed.
// `--obj <dir>` instead writes the SHIPPED hull as OBJ (base.obj, flames omitted, plus one per element extreme; two
// materials, body and accent) for Tools/Build/render_brittlestar_card_icon.py.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Gameplay;
public static class Runner
{
    static void WriteObj(string path, List<BrittleStarHullForm.Part> parts)
    {
        var sb = new StringBuilder();
        int offset = 1;
        foreach (var part in parts)
        {
            if (part.Kind == BrittleStarHullForm.PartKind.Flame) continue;
            sb.Append("o ").Append(part.Name).Append('\n');
            foreach (var v in part.Verts)
                sb.Append(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}\n", v.x, v.y, v.z));
            foreach (var (list, mat) in new[] { (part.Body, "body"), (part.Accent, "accent") })
            {
                sb.Append("usemtl ").Append(mat).Append('\n');
                for (int t = 0; t < list.Count; t += 3)
                    sb.Append($"f {list[t] + offset} {list[t + 1] + offset} {list[t + 2] + offset}\n");
            }
            offset += part.Verts.Count;
        }
        File.WriteAllText(path, sb.ToString());
    }

    public static int Main(string[] args)
    {
        int oi = Array.IndexOf(args, "--obj");
        if (oi >= 0 && oi + 1 < args.Length)
        {
            Directory.CreateDirectory(args[oi + 1]);
            var s = BrittleStarHullForm.Defaults;
            WriteObj(Path.Combine(args[oi + 1], "base.obj"), BrittleStarHullForm.Generate(s));
            foreach (var e in BrittleStarHullForm.MorphElements)
                WriteObj(Path.Combine(args[oi + 1], e.ToString().ToLowerInvariant() + ".obj"),
                         BrittleStarHullForm.Generate(BrittleStarHullForm.Extreme(s, e)));
            Console.WriteLine("wrote obj files to " + args[oi + 1]);
            return 0;
        }
        int pass = 0, fail = 0;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes().Where(t => t.GetCustomAttribute<NUnit.Framework.TestFixtureAttribute>() != null))
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                try { m.Invoke(Activator.CreateInstance(type), null); pass++; Console.WriteLine($"PASS {type.Name}.{m.Name}"); }
                catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 && pass > 0 ? 0 : 1;
    }
}
