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
                // Lantern Towers: every post segment, every hop's shelf / laid depth / far edge, every crown gem
                var lanterns = new List<WaystationLantern>();
                WaystationTower.Lanterns(gates, intensity, lanterns);
                foreach (var l in lanterns)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "L {0} {1} {2} {3:R} {4:R} {5:R} {6}",
                        intensity, seed, l.Gate, l.Position.x, l.Position.y, l.Position.z, l.Deep ? 1 : 0));
                var ts = WaystationTowerSettings.ForIntensity(intensity);
                for (int i = 1; i < gates.Count; i++)
                    if (WaystationTower.HasShelf(gates, i, ts))
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "S {0} {1} {2} {3:R} {4:R} {5:R} {6:R}",
                            intensity, seed, i, WaystationTower.ShelfDepth(gates, i, ts), WaystationTower.LaidDepth(gates, i, ts),
                            WaystationTower.FarEdge(gates, i, ts), WaystationHopPlan.AiTargetDepth(gates, i, ts, i % 4)));
                var gems = new List<Vector3>();
                WaystationTower.CrownGems(gates, intensity, gems);
                foreach (var g in gems)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "C {0} {1} {2:R} {3:R} {4:R}",
                        intensity, seed, g.x, g.y, g.z));
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
