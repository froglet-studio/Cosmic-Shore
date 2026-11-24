using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Switchback's garden ruins (SWITCHBACK.md "The I4 ruins"): the ruins Spearline lays round its racing line
    /// (<see cref="SpearlineRuins"/>, SPEARLINE.md §2a), laid round Switchback's I4 course from its own gates. The
    /// generator is CALLED, not copied; this class only chooses which of its ruins a Switchback course keeps.
    ///
    /// <para>An I4 leg here is not open space: it carries the gauntlet (keys, tunnels, doors and a block field,
    /// <see cref="SwitchbackBlastRun"/>). So a ruin prism stands only where it is clear of all of that:</para>
    /// <list type="bullet">
    /// <item>outside the generator's own lane and ring margin (its <c>Clear</c>);</item>
    /// <item><see cref="RouteLane"/> off the way a pilot really flies each leg (ring, key to key, ring), which is
    /// also where the path rays run;</item>
    /// <item><see cref="PrismClearance"/> from every Blast Run prism, movers' travel included.</item>
    /// </list>
    /// <para>What is left of a ruin fronts its gauntlet block: the pilot flies through it on the way to the first key.
    /// At most <see cref="PerCourse"/> ruins a course, <see cref="LegGap"/> legs apart, each kind once, and no wheel:
    /// a wheel sweeps the line, and the line here is inside a tunnel (the gauntlet has movers of its own).</para>
    ///
    /// <para>Pure: a function of the course, the rung and the layout, so every peer lays the same ruins and the
    /// edit-mode tests hold it (<c>SwitchbackRuinsTests</c>). Decoration: nothing here reads or writes a vessel.</para>
    /// </summary>
    public static class SwitchbackRuins
    {
        /// <summary>Ruins a course, at most: the six standing kinds, each once.</summary>
        public const int PerCourse = 6;

        /// <summary>Legs from one ruin to the next, at least, so they are spread along the course.</summary>
        public const int LegGap = 3;

        /// <summary>A ruin with fewer prisms than this left standing is not laid: it would read as debris.</summary>
        public const int MinParts = 30;

        /// <summary>No ruin prism reaches nearer than this (u) to the flown route of any leg: ring, keys, ring.</summary>
        public const float RouteLane = 60f;

        /// <summary>No ruin prism's centre line comes nearer than this (u) to a Blast Run prism's centre: a field cube's
        /// half diagonal (19), a ruin plate's (15) and a slider's travel (24) fit inside it.</summary>
        public const float PrismClearance = 60f;

        /// <summary>Every ruin prism of <paramref name="course"/> on this rung, appended to <paramref name="into"/>
        /// (each part's <c>Leg</c> names its ruin). Nothing below I4 (the generator's own rule).</summary>
        public static void Lay(IReadOnlyList<RaceGate> course, int intensity, SwitchbackFeatureLayout layout,
                               List<SpearlineRuinPart> into)
        {
            var settings = SpearlineRuins.ForIntensity(intensity);
            if (settings.PerLap <= 0 || course == null || course.Count < 3) return;
            // Every leg the generator would ruin; this course keeps PerCourse of them, below.
            settings.PerLap = int.MaxValue;
            var all = new List<SpearlineRuinPart>(4096);
            var wheels = new List<SpearlineRuinWheel>();
            SpearlineRuins.Lay(course, settings, all, wheels);
            if (all.Count == 0) return;

            var route = Route(course, layout);
            var grid = new PrismGrid(layout);
            int kindsUsed = 0, laid = 0, lastLeg = -LegGap;
            int i = 0;
            while (i < all.Count && laid < PerCourse)
            {
                int leg = all[i].Leg, first = i;
                while (i < all.Count && all[i].Leg == leg) i++;
                if (leg - lastLeg < LegGap || IsWheel(wheels, first)) continue;
                // A wheel that did not fit was laid as a pergola (the generator's rule).
                int kind = SpearlineRuins.KindOf(leg, course[0].Position);
                if (kind == SpearlineRuins.Wheel) kind = SpearlineRuins.Pergola;
                if ((kindsUsed & (1 << kind)) != 0) continue;

                int start = into.Count;
                for (int k = first; k < i; k++)
                    if (Stands(all[k], route, grid)) into.Add(all[k]);
                if (into.Count - start < MinParts)
                {
                    into.RemoveRange(start, into.Count - start);
                    continue;
                }
                kindsUsed |= 1 << kind;
                lastLeg = leg;
                laid++;
            }
        }

        static bool IsWheel(List<SpearlineRuinWheel> wheels, int first)
        {
            for (int w = 0; w < wheels.Count; w++)
                if (wheels[w].First == first) return true;
            return false;
        }

        /// <summary>The flown route of every leg as segments: ring to ring, by way of the leg's keys in order.</summary>
        public static List<(Vector3 a, Vector3 b)> Route(IReadOnlyList<RaceGate> course, SwitchbackFeatureLayout layout)
        {
            var segments = new List<(Vector3, Vector3)>(course.Count * 3);
            var keys = new List<BlastRunStation>(4);
            for (int gate = 1; gate < course.Count; gate++)
            {
                keys.Clear();
                if (layout != null)
                    foreach (var station in layout.Stations)
                        if (station.Gate == gate) keys.Add(station);
                keys.Sort((x, y) => x.Stage.CompareTo(y.Stage));
                Vector3 from = course[gate - 1].Position;
                foreach (var station in keys)
                {
                    segments.Add((from, station.Key));
                    from = station.Key;
                }
                segments.Add((from, course[gate].Position));
            }
            return segments;
        }

        static bool Stands(in SpearlineRuinPart part, List<(Vector3 a, Vector3 b)> route, PrismGrid grid)
        {
            int samples = Samples(part);
            for (int k = 0; k < samples; k++)
            {
                Vector3 p = Point(part, k, samples);
                if (RouteDistance(route, p) < RouteLane || grid.Nearest(p) < PrismClearance) return false;
            }
            return true;
        }

        /// <summary>How near <paramref name="part"/> comes to the flown route, along its whole length.</summary>
        public static float PartRouteDistance(in SpearlineRuinPart part, List<(Vector3 a, Vector3 b)> route)
        {
            float best = float.MaxValue;
            int samples = Samples(part);
            for (int k = 0; k < samples; k++) best = Mathf.Min(best, RouteDistance(route, Point(part, k, samples)));
            return best;
        }

        /// <summary>How near <paramref name="part"/>'s centre line comes to any Blast Run prism's centre (brute force:
        /// what the tests measure the grid against).</summary>
        public static float PartPrismDistance(in SpearlineRuinPart part, SwitchbackFeatureLayout layout)
        {
            float best = float.MaxValue;
            int samples = Samples(part);
            for (int k = 0; k < samples; k++)
            {
                Vector3 p = Point(part, k, samples);
                for (int i = 0; i < layout.Prisms.Count; i++)
                    best = Mathf.Min(best, (layout.Prisms[i].Position - p).sqrMagnitude);
            }
            return best == float.MaxValue ? best : Mathf.Sqrt(best);
        }

        static float RouteDistance(List<(Vector3 a, Vector3 b)> route, Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < route.Count; i++)
                best = Mathf.Min(best, SwitchbackFeatures.DistanceToSegment(p, route[i].a, route[i].b));
            return best;
        }

        // A long prism (the maze's bars) is tested along its length, a point every 10 u or so; a short one at its centre.
        static int Samples(in SpearlineRuinPart part) => part.Scale.z <= 16f ? 1 : 1 + Mathf.CeilToInt(part.Scale.z / 10f);

        static Vector3 Point(in SpearlineRuinPart part, int k, int samples) =>
            samples == 1 ? part.Position : part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);

        /// <summary>The Blast Run prisms in cells of <see cref="PrismClearance"/>, so a point asks its 27 cells only.</summary>
        sealed class PrismGrid
        {
            readonly Dictionary<Vector3Int, List<Vector3>> _cells = new();

            public PrismGrid(SwitchbackFeatureLayout layout)
            {
                if (layout == null) return;
                for (int i = 0; i < layout.Prisms.Count; i++)
                {
                    Vector3 p = layout.Prisms[i].Position;
                    var cell = Cell(p);
                    if (!_cells.TryGetValue(cell, out var list)) _cells[cell] = list = new List<Vector3>(8);
                    list.Add(p);
                }
            }

            static Vector3Int Cell(Vector3 p) => new(Mathf.FloorToInt(p.x / PrismClearance),
                Mathf.FloorToInt(p.y / PrismClearance), Mathf.FloorToInt(p.z / PrismClearance));

            /// <summary>The distance to the nearest prism centre, exact below <see cref="PrismClearance"/>
            /// (float.MaxValue when none is that near).</summary>
            public float Nearest(Vector3 p)
            {
                var c = Cell(p);
                float best = float.MaxValue;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (!_cells.TryGetValue(new Vector3Int(c.x + x, c.y + y, c.z + z), out var list)) continue;
                    for (int i = 0; i < list.Count; i++) best = Mathf.Min(best, (list[i] - p).sqrMagnitude);
                }
                return best == float.MaxValue ? best : Mathf.Sqrt(best);
            }
        }
    }
}
