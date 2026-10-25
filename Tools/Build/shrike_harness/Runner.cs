// Runs every [Test] in the compiled assembly by reflection and prints a pass/fail line per test. Exit 0 = all passed.
// `--obj <dir>` instead writes the SHIPPED hull as OBJ (base.obj, flame omitted, plus one per element extreme; two
// materials, body and accent); its card consumer, render_shrike_card_icon.py, was retired 2026-10-18 (the card is
// rendered from Shrike.fbx by Tools/Build/vessel_model/render_cards.py).
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Gameplay;
public static class Runner
{
    static void WriteObj(string path, ShrikeHullForm.MeshData m)
    {
        var sb = new StringBuilder("o ShrikeHull\n");
        foreach (var v in m.Verts)
            sb.Append(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}\n", v.x, v.y, v.z));
        foreach (var (list, mat) in new[] { (m.Body, "body"), (m.Accent, "accent") })
        {
            sb.Append("usemtl ").Append(mat).Append('\n');
            for (int t = 0; t < list.Count; t += 3)
                sb.Append($"f {list[t] + 1} {list[t + 1] + 1} {list[t + 2] + 1}\n");
        }
        File.WriteAllText(path, sb.ToString());
    }

    public static int Main(string[] args)
    {
        int oi = Array.IndexOf(args, "--obj");
        if (oi >= 0 && oi + 1 < args.Length)
        {
            Directory.CreateDirectory(args[oi + 1]);
            var s = ShrikeHullForm.Defaults;
            WriteObj(Path.Combine(args[oi + 1], "base.obj"), ShrikeHullForm.Hull(s));
            string[] names = { "charge", "mass", "space", "time" };
            for (int e = 0; e < 4; e++)
                WriteObj(Path.Combine(args[oi + 1], names[e] + ".obj"), ShrikeHullForm.Hull(ShrikeHullForm.Morph(s,
                    e == 0 ? 1f : 0f, e == 1 ? 1f : 0f, e == 2 ? 1f : 0f, e == 3 ? 1f : 0f)));
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
