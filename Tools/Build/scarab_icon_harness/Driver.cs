using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

/// <summary>
/// Runs the SHIPPED ScarabHullForm.Generate at the settings passed on the command line (the
/// prefab's authored proportions, read off Scarab.prefab by render_scarab_card_icons.py) and
/// prints the parts as JSON: name, pivot, hull-space vertices, normals, and the two submesh
/// triangle lists. Nothing here is a model OF the hull; it is the hull.
/// </summary>
public static class Driver
{
    static readonly string[] Keys =
    {
        "length", "width", "domeHeight", "bellyDepth", "seamFraction", "elytraFront", "pronotumFront",
        "pronotumSwell", "striaeCount", "striaeDepth", "lengthSegments", "widthSegments", "hornLength",
        "hornCurve", "hornSides", "legLength", "legThickness", "abdomenHeight", "antennaLength", "antennaThickness",
    };

    public static int Main(string[] args)
    {
        var ci = CultureInfo.InvariantCulture;
        var values = new Dictionary<string, float>();
        foreach (var a in args)
        {
            int eq = a.IndexOf('=');
            if (eq <= 0) { Console.Error.WriteLine($"bad arg '{a}' (want key=value)"); return 2; }
            values[a.Substring(0, eq)] = float.Parse(a.Substring(eq + 1), ci);
        }
        foreach (var k in Keys)
            if (!values.ContainsKey(k)) { Console.Error.WriteLine($"missing setting '{k}'"); return 2; }

        var s = ScarabHullForm.Settings.Default;   // morph channels stay at rest
        s.Length = values["length"]; s.Width = values["width"]; s.DomeHeight = values["domeHeight"];
        s.BellyDepth = values["bellyDepth"]; s.SeamFraction = values["seamFraction"]; s.ElytraFront = values["elytraFront"];
        s.PronotumFront = values["pronotumFront"]; s.PronotumSwell = values["pronotumSwell"];
        s.StriaeCount = (int)values["striaeCount"]; s.StriaeDepth = values["striaeDepth"];
        s.LengthSegments = (int)values["lengthSegments"]; s.WidthSegments = (int)values["widthSegments"];
        s.HornLength = values["hornLength"]; s.HornCurve = values["hornCurve"]; s.HornSides = (int)values["hornSides"];
        s.LegLength = values["legLength"]; s.LegThickness = values["legThickness"]; s.AbdomenHeight = values["abdomenHeight"];
        s.AntennaLength = values["antennaLength"]; s.AntennaThickness = values["antennaThickness"];

        var parts = ScarabHullForm.Generate(s);
        var sb = new StringBuilder();
        sb.Append("{\"parts\":[");
        for (int p = 0; p < parts.Count; p++)
        {
            var part = parts[p];
            if (p > 0) sb.Append(',');
            sb.Append("{\"name\":\"").Append(part.Name).Append("\",\"pivot\":[")
              .Append(part.Pivot.x.ToString("R", ci)).Append(',').Append(part.Pivot.y.ToString("R", ci)).Append(',').Append(part.Pivot.z.ToString("R", ci))
              .Append("],\"verts\":[");
            for (int i = 0; i < part.Verts.Count; i++)
            {
                var v = part.Verts[i];
                if (i > 0) sb.Append(',');
                sb.Append(v.x.ToString("R", ci)).Append(',').Append(v.y.ToString("R", ci)).Append(',').Append(v.z.ToString("R", ci));
            }
            sb.Append("],\"normals\":[");
            for (int i = 0; i < part.Normals.Count; i++)
            {
                var v = part.Normals[i];
                if (i > 0) sb.Append(',');
                sb.Append(v.x.ToString("R", ci)).Append(',').Append(v.y.ToString("R", ci)).Append(',').Append(v.z.ToString("R", ci));
            }
            sb.Append("],\"chassis\":[").Append(string.Join(",", part.Chassis)).Append("],\"shell\":[")
              .Append(string.Join(",", part.Shell)).Append("]}");
        }
        sb.Append("]}");
        Console.Out.Write(sb.ToString());
        return 0;
    }
}
