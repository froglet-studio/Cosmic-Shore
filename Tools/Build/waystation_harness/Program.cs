// Emit the SHIPPED WaystationCourse's gates for a seed and intensity, so the offline model can be
// compared against the code the game runs rather than against a transcription of it.
using System;
using System.Globalization;
using CosmicShore.Gameplay;
using UnityEngine;

static class Program
{
    static int Main(string[] argv)
    {
        int seeds = argv.Length > 0 ? int.Parse(argv[0]) : 40;
        int target = argv.Length > 1 ? int.Parse(argv[1]) : 24;
        float inner = argv.Length > 2 ? float.Parse(argv[2], CultureInfo.InvariantCulture) : 478f;
        float outer = argv.Length > 3 ? float.Parse(argv[3], CultureInfo.InvariantCulture) : 1080f;

        for (int intensity = 1; intensity <= 4; intensity++)
        {
            var s = WaystationCourseSettings.ForIntensity(intensity);
            s.InnerRadius = inner;
            s.OuterRadius = outer;
            s.RingTarget = Mathf.Max(s.RingsPerCluster, target);
            s.FirstClusterDirection = Vector3.up;

            for (int seed = 1; seed <= seeds; seed++)
            {
                var gates = WaystationCourse.Generate(seed, s);
                for (int i = 0; i < gates.Count; i++)
                {
                    var g = gates[i];
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0} {1} {2} {3:R} {4:R} {5:R} {6:R} {7:R} {8:R} {9:R}",
                        intensity, seed, i,
                        g.Position.x, g.Position.y, g.Position.z,
                        g.Axis.x, g.Axis.y, g.Axis.z, g.Radius));
                }
            }
        }
        return 0;
    }
}
