using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

// Prints the C# mask's and perimeter's values on the grid the Node side evaluates the lab's twins on
// (cases.txt). One line per case: "<kind> <values...>", invariant culture, round-trip floats.
public static class Driver
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static void Add(StringBuilder sb, float v) => sb.Append(' ').Append(v.ToString("R", Inv));
    static void Add(StringBuilder sb, Vector3 v) { Add(sb, v.x); Add(sb, v.y); Add(sb, v.z); }

    public static int Main(string[] args)
    {
        var sb = new StringBuilder();
        foreach (var line in System.IO.File.ReadAllLines(args[0]))
        {
            var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f.Length == 0) continue;
            float F(int i) => float.Parse(f[i], Inv);
            int I(int i) => int.Parse(f[i], Inv);
            switch (f[0])
            {
                case "shape":
                {
                    // shape style facets inner feather roundness fill rimStrength rimWidth aspect
                    var s = new SilenceVignetteMask.Shape
                    {
                        Style = I(1), Facets = I(2), Inner = F(3), Feather = F(4),
                        Roundness = F(5), Fill = F(6), RimStrength = F(7), RimWidth = F(8),
                    };
                    float aspect = F(9);
                    sb.Append("shape");
                    for (int y = 0; y <= 16; y++)
                    for (int x = 0; x <= 16; x++)
                    {
                        float u = x / 16f, v = y / 16f;
                        Add(sb, SilenceVignetteMask.Distance(u, v, aspect, s));
                        Add(sb, SilenceVignetteMask.Alpha(u, v, aspect, s));
                    }
                    sb.AppendLine();
                    break;
                }
                case "breathe":
                    sb.Append("breathe"); Add(sb, SilenceVignetteMask.Breathe(F(1), F(2), F(3))); sb.AppendLine();
                    break;
                case "opacity":
                    sb.Append("opacity"); Add(sb, SilenceVignetteMask.Opacity(F(1), F(2), F(3), F(4), F(5))); sb.AppendLine();
                    break;
                case "layout":
                {
                    // layout shape count seed size reach spin pullDrift weight pulse time aspect fov
                    var s = new SilencePerimeter.Settings { Shape = I(1), Count = I(2), Seed = I(3), Size = F(4), Reach = F(5), Spin = F(6), PullDrift = F(7) };
                    sb.Append("layout");
                    var pull = SilencePerimeter.PullPoint(s, F(10));
                    Add(sb, pull.x); Add(sb, pull.y);
                    for (int i = 0; i < s.Count; i++)
                    {
                        var p = SilencePerimeter.Layout(i, s, F(8), F(9), F(10), F(11), F(12));
                        Add(sb, p.Position); Add(sb, p.Right); Add(sb, p.Up); Add(sb, p.Forward); Add(sb, p.Presence); Add(sb, p.Seed);
                    }
                    sb.AppendLine();
                    break;
                }
                case "geometry":
                {
                    // geometry shape count seed i
                    var s = new SilencePerimeter.Settings { Shape = I(1), Count = I(2), Seed = I(3) };
                    var pos = new List<Vector3>(); var nor = new List<Vector3>(); var bar = new List<Vector3>();
                    SilencePerimeter.Geometry(s, I(4), pos, nor, bar);
                    sb.Append("geometry");
                    for (int j = 0; j < pos.Count; j++) { Add(sb, pos[j]); Add(sb, nor[j]); Add(sb, bar[j]); }
                    sb.AppendLine();
                    break;
                }
            }
        }
        Console.Write(sb.ToString());
        return 0;
    }
}
