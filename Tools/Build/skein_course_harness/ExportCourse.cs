// ExportCourse.cs - writes Skein's cable and rings, as the GAME builds them, for Tools/Build/skein_urchin_sim.py.
// Compiled by export_course.sh against race_course_source_harness's UnityEngine stub and the SHIPPED
// SkeinCourse.cs, so every rail and ring here is SkeinCourse.BuildAll's own output - nothing is retyped.
// Args: one "intensity:seed:strandCount" per course (export_course.sh reads them off SpawnableSkein<i>.prefab).
// The seed ladder is SpawnableSkein.BuildEnvironment's: the first of seed + attempt x 7919 (six attempts)
// that lays a full course, which SkeinCourseSource.Build walks identically for the rings.
using System;
using System.Globalization;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

public static class ExportCourse
{
    static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static string A(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
    static string U(Vector3 v) => "[" + A(v.x) + "," + A(v.y) + "," + A(v.z) + "]";

    static int IntensityForStrandCount(int n) => n <= 5 ? 1 : n == 6 ? 2 : n == 7 ? 3 : 4;   // SpawnableSkein

    public static int Main(string[] args)
    {
        var sb = new StringBuilder();
        sb.Append("{\"generated_by\": \"Tools/Build/skein_course_harness/export_course.sh from SkeinCourse.BuildAll (Assets/_Scripts/Controller/Arcade/Skein/SkeinCourse.cs). Do not edit.\",\n");
        sb.Append(" \"courses\": [");
        bool first = true;
        foreach (var arg in args)
        {
            var parts = arg.Split(':');
            int intensity = int.Parse(parts[0]), seed = int.Parse(parts[1]), strands = int.Parse(parts[2]);
            var s = SkeinCourseSettings.ForIntensity(IntensityForStrandCount(strands));
            s.StrandCount = strands;
            SkeinCourse.SkeinBuild b = null;
            int used = seed;
            for (int attempt = 0; attempt < 6 && b == null; attempt++)
            {
                used = unchecked(seed + attempt * 7919);
                b = SkeinCourse.BuildAll(used, s);
            }
            if (b == null) { Console.Error.WriteLine($"no course for {arg}"); return 1; }
            SkeinCourse.StartPose(s, out var start, out var startAxis);
            sb.Append(first ? "\n" : ",\n");
            first = false;
            sb.Append("  {\"intensity\": " + intensity + ", \"seed\": " + seed + ", \"cableSeed\": " + used +
                      ", \"strands\": " + strands + ", \"gateMouth\": " + F(s.GateMouth) + ", \"collarMouth\": " + F(s.CollarMouth) +
                      ", \"prismSpacing\": " + F(s.PrismSpacing) + ", \"start\": " + V(start) + ", \"startAxis\": " + U(startAxis) + ",\n");
            sb.Append("   \"gates\": [");
            for (int g = 0; g < b.Gates.Count; g++)
            {
                var gt = b.Gates[g];
                sb.Append((g > 0 ? "," : "") + "\n    {\"p\": " + V(gt.Position) + ", \"a\": " + U(gt.Axis) + ", \"r\": " + F(gt.Radius) + ", \"strand\": " + gt.Strand + "}");
            }
            sb.Append("],\n   \"rails\": [");
            for (int r = 0; r < b.Rails.Count; r++)
            {
                var rl = b.Rails[r];
                var pos = b.Positions[r];
                sb.Append((r > 0 ? "," : "") + "\n    {\"strand\": " + rl.Strand + ", \"domain\": \"" + rl.Domain + "\", \"launches\": " +
                          (rl.Launches ? "true" : "false") + ", \"seamNext\": " + b.SeamNext[r] + ", \"pts\": [");
                for (int k = 0; k < pos.Length; k++)
                    sb.Append((k > 0 ? "," : "") + F(pos[k].x) + "," + F(pos[k].y) + "," + F(pos[k].z));
                sb.Append("]}");
            }
            sb.Append("]}");
        }
        sb.Append("\n ]\n}\n");
        Console.Out.Write(sb.ToString());
        return 0;
    }
}
