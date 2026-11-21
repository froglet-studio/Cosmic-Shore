using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The voyage content's places on the real HyperSea (<see cref="HyperSea"/>, Docs/Voyages/VOYAGE_BOARD.md): each
    /// charted location's position in light years, and the course an Ark sails along a job's lanes, cell by cell round
    /// the reefs, with what the crew wants to know before setting out (how far, how long light takes, the thickest water,
    /// how rough). Pure C#; the Voyage window's cards draw it (<c>VoyageMapGraphic</c>).
    /// </summary>
    public sealed class VoyageChart
    {
        /// <summary>Cells around a charted place an Ark may berth in, even in reef (the board's berth).</summary>
        public const int BerthCells = 3;
        /// <summary>How far from its start a leg's course may stray, in cells (the board's 55-cell range plus six).</summary>
        public const int MaxLegCells = 61;

        public readonly HyperSeaChart Sea;
        readonly VoyageContent _content;
        readonly Dictionary<(string, string), Leg> _legs = new();

        /// <summary>One lane sailed on the real sea.</summary>
        public sealed class Leg
        {
            public List<(int q, int r)> Cells;
            public HyperSeaCourse Course;
        }

        /// <summary>A job's whole route, its legs joined.</summary>
        public sealed class Voyage
        {
            /// <summary>The course drawn flat, in light years, every leg's cells in order.</summary>
            public readonly List<(double x, double y)> Points = new();
            /// <summary>Each point's height above the midplane in light years: a leg climbs evenly from where it starts
            /// to where it ends (as <see cref="HyperSeaChart.Measure"/> sails it). The window's edge view draws it.</summary>
            public readonly List<double> Heights = new();
            public int Cells;
            public double LightYears => Cells * HyperSea.CellLy;
            public double Minutes => Cells * HyperSea.MinutesPerCell;
            public double ThickestDensity;
            public SeaKind ThickestSea => HyperSea.Sea(ThickestDensity);
            /// <summary>The roughest leg's intensity, I1 to I4.</summary>
            public int Intensity = 1;
        }

        static VoyageChart _last;

        /// <summary>The chart of <paramref name="content"/>, kept while the same content is asked for (the sea's
        /// densities and the legs' courses are cached on it). Null when no location is charted.</summary>
        public static VoyageChart For(VoyageContent content)
        {
            if (content == null || !IsCharted(content)) return null;
            if (_last == null || !ReferenceEquals(_last._content, content)) _last = new VoyageChart(content);
            return _last;
        }

        /// <summary>True when at least one location has a place on the real sea.</summary>
        public static bool IsCharted(VoyageContent content)
        {
            if (content?.Locations == null) return false;
            foreach (var l in content.Locations)
                if (l != null && l.Charted) return true;
            return false;
        }

        public VoyageChart(VoyageContent content)
        {
            _content = content;
            var places = new List<HyperSeaPlace>();
            foreach (var l in content?.Locations ?? new List<VoyageLocation>())
                if (l != null && l.Charted) places.Add(new HyperSeaPlace(l.GalaxyX, l.GalaxyY, BerthCells));
            Sea = new HyperSeaChart(places);
        }

        /// <summary>The leg between two charted locations, or null when either is not charted or no course reaches.</summary>
        public Leg LegBetween(string fromId, string toId)
        {
            var key = (fromId, toId);
            if (_legs.TryGetValue(key, out var leg)) return leg;
            var a = _content.Location(fromId);
            var b = _content.Location(toId);
            if (a == null || b == null || !a.Charted || !b.Charted) return _legs[key] = null;
            var cells = Sea.FindCourse(HyperSea.CellOf(a.GalaxyX, a.GalaxyY), HyperSea.CellOf(b.GalaxyX, b.GalaxyY),
                safe: false, maxSteps: MaxLegCells);
            leg = cells == null ? null : new Leg { Cells = cells, Course = Sea.Measure(cells, a.GalaxyZ, b.GalaxyZ) };
            return _legs[key] = leg;
        }

        /// <summary>The voyage along <paramref name="route"/> (location ids in order, as <see cref="VoyageRules.Route"/>
        /// gives them), or null when the route is shorter than one leg or any leg cannot be sailed.</summary>
        public Voyage Along(IReadOnlyList<string> route)
        {
            if (route == null || route.Count < 2) return null;
            var v = new Voyage();
            for (int i = 1; i < route.Count; i++)
            {
                var leg = LegBetween(route[i - 1], route[i]);
                if (leg == null) return null;
                int from = v.Points.Count == 0 ? 0 : 1;   // a leg starts where the last one ended
                double z0 = _content.Location(route[i - 1]).GalaxyZ, z1 = _content.Location(route[i]).GalaxyZ;
                int n = leg.Course.Points.Count;
                for (int p = from; p < n; p++)
                {
                    v.Points.Add(leg.Course.Points[p]);
                    v.Heights.Add(n > 1 ? z0 + (z1 - z0) * p / (n - 1) : z1);
                }
                v.Cells += leg.Course.Cells;
                v.ThickestDensity = Math.Max(v.ThickestDensity, leg.Course.ThickestDensity);
                v.Intensity = Math.Max(v.Intensity, leg.Course.Intensity);
            }
            return v;
        }
    }
}
