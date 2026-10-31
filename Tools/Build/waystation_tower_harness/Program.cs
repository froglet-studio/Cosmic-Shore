// Emit the SHIPPED WaystationTower's course (Garrett's clusters plus the Drop Towers) and its wall tiles for every
// seed and intensity, so the offline model can be compared against the code the game runs.
using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Gameplay;
using UnityEngine;

static class Program
{
    static int Main(string[] argv)
    {
        int seeds = argv.Length > 0 ? int.Parse(argv[0]) : 20;
        int clusters = argv.Length > 1 ? int.Parse(argv[1]) : 5;
        var tiles = new List<WaystationTowerTile>();
        for (int intensity = 1; intensity <= 4; intensity++)
        {
            var s = WaystationCourseSettings.ForIntensity(intensity);
            s.InnerRadius = 478f;
            s.OuterRadius = 1080f;
            s.RingTarget = clusters * s.RingsPerCluster;
            s.FirstClusterDirection = Vector3.up;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var gates = WaystationTower.Build(seed, s, intensity, out int used);
                WaystationTower.Walls(gates, intensity, tiles);
                int danger = 0;
                foreach (var t in tiles) if (t.Danger) danger++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "T {0} {1} {2} {3} {4}",
                    intensity, seed, used, tiles.Count, danger));
                for (int i = 0; i < gates.Count; i++)
                {
                    var g = gates[i];
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "G {0} {1} {2} {3:R} {4:R} {5:R} {6:R} {7:R} {8:R} {9:R}",
                        intensity, seed, i, g.Position.x, g.Position.y, g.Position.z,
                        g.Axis.x, g.Axis.y, g.Axis.z, g.Radius));
                }
            }
        }
        return 0;
    }
}
