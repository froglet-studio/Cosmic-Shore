// TraceDriver.cs - runs the SHIPPED KiteNavigatorCore (compiled from its real path by trace.sh) over the scripted
// thumbs in a scenarios file and prints one JSON row per frame, the same shape the Crew Vessel Lab's
// window.__lab.parityTrace returns. parity.cjs compares the two.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using CosmicShore.Gameplay;

public static class TraceDriver
{
    public static int Main(string[] args)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
        var sb = new StringBuilder("[");
        bool firstScn = true;
        foreach (var scn in doc.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            var p = scn.GetProperty("P");
            var s = new KiteSettings
            {
                BoardRadius = p.GetProperty("planeRange").GetSingle(),
                Anchor = p.GetProperty("anchor").GetSingle(),
                KiteSpeed = p.GetProperty("kiteSpeed").GetSingle(),
                KiteAccel = p.GetProperty("kiteAccel").GetSingle(),
                TailLength = p.GetProperty("tailLen").GetSingle(),
            };
            float dt = scn.GetProperty("dt").GetSingle();
            var core = new KiteNavigatorCore();
            core.Reset(new KiteVec(0f, s.Anchor * s.BoardRadius * 0.5f));
            var loop = new List<KiteVec>();
            if (!firstScn) sb.Append(',');
            firstScn = false;
            sb.Append('[');
            bool firstRow = true;
            foreach (var a in scn.GetProperty("aims").EnumerateArray())
            {
                core.Move(s, a[0].GetSingle(), a[1].GetSingle(), a[2].GetInt32() != 0, dt);
                bool closed = core.AdvanceTail(s, loop);
                if (!firstRow) sb.Append(',');
                firstRow = false;
                sb.Append($"{{\"x\":{core.Position.X:R},\"z\":{core.Position.Z:R},\"vx\":{core.Velocity.X:R},\"vz\":{core.Velocity.Z:R},\"tail\":{core.Tail.Count},\"loop\":{(closed ? loop.Count : 0)}}}");
            }
            sb.Append(']');
        }
        sb.Append(']');
        Console.WriteLine(sb.ToString());
        return 0;
    }
}
