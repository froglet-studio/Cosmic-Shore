#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The real HyperSea (<see cref="HyperSea"/>, <see cref="HyperSeaChart"/>, <see cref="VoyageChart"/>) against the
    /// Voyage Board it was ported from (https://claude.ai/artifact/25CvRSxBrjcxEUngEiCzEn, Docs/Voyages/VOYAGE_BOARD.md).
    /// Every golden number is the page's own output, printed by running its functions in node
    /// (<c>node Docs/Voyages/VoyageBoard/golden.js</c>), so a drift in either the page or the port fails here.
    /// </summary>
    [TestFixture]
    public class HyperSeaTests
    {
        static void Near(double expected, double actual, string what)
        {
            double tol = Math.Max(1e-12, Math.Abs(expected) * 1e-9);
            Assert.That(actual, Is.EqualTo(expected).Within(tol), what);
        }

        [Test]
        public void Hash_MatchesThePage()
        {
            Near(0.0607239515520632, HyperSea.Hash(3, 7), "hash(3, 7)");
            Near(0.0328648390714079, HyperSea.Hash(-5, 12), "hash(-5, 12)");
            Near(0.806917699985206, HyperSea.Hash(100000, -3), "hash(100000, -3)");
        }

        [Test]
        public void ValueNoise_MatchesThePage()
        {
            Near(0.298666345806792, HyperSea.ValueNoise(1.3, -2.7), "vnoise(1.3, -2.7)");
            Near(0.751919793423440, HyperSea.ValueNoise(-17.25, 40.5), "vnoise(-17.25, 40.5)");
        }

        // x, y, column, at z 0, at z 240, arm profile (width 1,100)
        static readonly double[][] DensityGoldens =
        {
            new[] { 900.0, 2100, 0.0343423832831808, 0.0373423832831808, 0.0250196626531202, 0 },
            new[] { 0.0, 26000, 0.0150904954915188, 0.0180904954915188, 0.0126757297608525, 0.7 },
            new[] { -12000.0, 15000, 0.375111698534035, 0.378111698534035, 0.243514264570673, 0.0210996126197570 },
            new[] { 5000.0, -3000, 0.729981569497023, 0.732981569497023, 0.471049866276809, 0 },
            new[] { 0.0, 0, 1.64579780791066, 1.64879780791066, 1.05825327775332, 0 },
            new[] { 1120.0, 1520, 2.52231844234625, 2.50469807911625, 1.49455988360272, 0 },
            new[] { 40000.0, 1000, 1.18035816824569, 1.18335816824569, 0.759822508802241, 0.425985750733558 },
            new[] { -3200.0, 9800, 0.847913964830633, 0.850913964830633, 0.546665805325288, 0.186566408860603 },
            new[] { 60000.0, 0, 0.0123082399248166, 0.0153082399248166, 0.0108918020558830, 0.724931483799384 },
        };

        [Test]
        public void Density_MatchesThePage_ThroughTheColumnAndAtHeight()
        {
            foreach (var g in DensityGoldens)
            {
                double x = g[0], y = g[1];
                Near(g[2], HyperSea.Density(x, y), $"column at ({x}, {y})");
                Near(g[3], HyperSea.Density(x, y, 0), $"midplane at ({x}, {y})");
                Near(g[4], HyperSea.Density(x, y, 240), $"240 ly up at ({x}, {y})");
                Near(g[5], HyperSea.ArmProfile(x, y, Math.Sqrt(x * x + y * y), 1100), $"arm at ({x}, {y})");
            }
        }

        [Test]
        public void TheSeaThinsAwayFromTheMidplane()
        {
            Assert.Less(HyperSea.Density(1120, 1520, 400), HyperSea.Density(1120, 1520, 0));
            Assert.Less(HyperSea.Density(0, 0, 1000), HyperSea.Density(0, 0, 0));
        }

        [Test]
        public void RealPositions_MatchThePage()
        {
            var (x, y) = HyperSea.FromGalactic(180.1, 153, -22.3);   // the Hyades
            Near(0.247063599183202, x, "Hyades x");
            Near(26141.5568713093, y, "Hyades y");
            Assert.AreEqual(7.0, HyperSea.HeightOf(153, -22.3), "Hyades height");
            Assert.AreEqual(-480.0, HyperSea.HeightOf(650, -57), "Helix height");
        }

        [Test]
        public void SeaNamesDangerAndColour_MatchThePage()
        {
            // density, sea, danger, r, g, b
            var cases = new (double n, SeaKind sea, double danger, double r, double g, double b)[]
            {
                (0.01, SeaKind.Spray, 0, 36.6666666666667, 45.5555555555556, 65.3333333333333),
                (0.3, SeaKind.Froth, 0.129409129247696, 29.4858583018689, 89.4292915093446, 113.856062735983),
                (2, SeaKind.Water, 0.870926996097583, 40.7170220102391, 64.6414889948805, 143.344777729600),
                (40, SeaKind.Thick, 2.04185399219517, 105.102999566398, 44.7345500650403, 141.224250108400),
                (500, SeaKind.Reef, 3, 237.881475148147, 125.783550209574, 87.4742501084005),
            };
            foreach (var c in cases)
            {
                Assert.AreEqual(c.sea, HyperSea.Sea(c.n), $"sea of {c.n}");
                Near(c.danger, HyperSea.Danger(c.n), $"danger of {c.n}");
                var (r, g, b) = HyperSea.SeaColor(c.n);
                Near(c.r, r, $"red of {c.n}");
                Near(c.g, g, $"green of {c.n}");
                Near(c.b, b, $"blue of {c.n}");
            }
        }

        [Test]
        public void Cells_MatchThePage()
        {
            Assert.AreEqual((-15, 121), HyperSea.CellOf(900, 2100));
            Assert.AreEqual((-84, 45), HyperSea.CellOf(-1234.5, 777.7));
            Assert.AreEqual((0, 1), HyperSea.CellOf(10, 8.66));
            var (cx, cy) = HyperSea.CellCentre(-15, 121);
            Assert.AreEqual((-15, 121), HyperSea.CellOf(cx, cy), "a cell's centre lies in it");
        }

        // The board's places around Tumult (proposed), each with its height above the midplane.
        static readonly Dictionary<string, (double x, double y, double z)> Places = new()
        {
            ["tumult"] = (900, 2100, 30),
            ["tollmoor"] = (1300, 2360, 98),
            ["lantern"] = (560, 1690, -34),
            ["hollow"] = (1520, 1760, -149),
            ["verdigris"] = (430, 2360, -112),
            ["saltmarch"] = (1120, 1520, 145),
        };

        static HyperSeaChart Chart()
        {
            var places = new List<HyperSeaPlace>();
            foreach (var p in Places.Values) places.Add(new HyperSeaPlace(p.x, p.y, VoyageChart.BerthCells));
            return new HyperSeaChart(places);
        }

        // from, to, safe, cells in the course (start included), the middle cell, measured steps, layers, planar steps,
        // max danger, average danger, thickest water, course intensity
        static readonly object[][] Legs =
        {
            new object[] { "tumult", "tollmoor", false, 28, (-9, 129), 27, 5, 27, 0.370889415612516, 0.0667909367539800, 0.386186493882319, 1 },
            new object[] { "tumult", "lantern", false, 30, (-21, 112), 29, 4, 29, 0.993796007165879, 0.546794386382214, 2.73874198170016, 2 },
            new object[] { "lantern", "verdigris", false, 39, (-28, 117), 38, 5, 38, 0.977985303819606, 0.698017987781353, 2.56356531822679, 2 },
            new object[] { "hollow", "saltmarch", false, 28, (24, 89), 27, 19, 27, 0.860626399047782, 0.716754740574742, 1.78442371332679, 2 },
            new object[] { "tollmoor", "saltmarch", false, 49, (-3, 112), 48, 3, 48, 1.00919576319770, 0.424727319839843, 2.84879998978251, 2 },
            new object[] { "verdigris", "saltmarch", false, 63, (-16, 105), 62, 16, 62, 1.96534539516396, 0.954996482125528, 32.8889826005592, 3 },
            new object[] { "tumult", "lantern", true, 30, (-18, 109), 29, 4, 29, 0.993796007165879, 0.557689394358132, 2.73874198170016, 2 },
            new object[] { "tollmoor", "saltmarch", true, 49, (-3, 112), 48, 3, 48, 0.982305926995808, 0.422441276202071, 2.65940427976817, 2 },
            new object[] { "verdigris", "saltmarch", true, 66, (-14, 115), 65, 16, 65, 1.19985211816773, 0.448581058000166, 4.63983304384067, 2 },
        };

        [Test]
        public void Courses_MatchThePage_RoundTheReefs()
        {
            var chart = Chart();
            foreach (var leg in Legs)
            {
                string from = (string)leg[0], to = (string)leg[1];
                bool safe = (bool)leg[2];
                string what = $"{from} to {to} ({(safe ? "safe" : "quick")})";
                var a = Places[from];
                var b = Places[to];
                var cells = chart.FindCourse(HyperSea.CellOf(a.x, a.y), HyperSea.CellOf(b.x, b.y), safe, 61);
                Assert.IsNotNull(cells, what);
                Assert.AreEqual((int)leg[3], cells.Count, what + ": cells");
                Assert.AreEqual(((int, int))leg[4], cells[cells.Count / 2], what + ": middle cell");
                var m = chart.Measure(cells, a.z, b.z);
                Assert.AreEqual((int)leg[5], m.Cells, what + ": steps");
                Assert.AreEqual((int)leg[6], m.Layers, what + ": layers");
                Assert.AreEqual((int)leg[7], m.FlatSteps, what + ": planar steps");
                Near((double)leg[8], m.MaxDanger, what + ": max danger");
                Near((double)leg[9], m.AverageDanger, what + ": average danger");
                Near((double)leg[10], m.ThickestDensity, what + ": thickest water");
                Assert.AreEqual((int)leg[11], m.Intensity, what + ": intensity");
                Assert.AreEqual(m.Cells * HyperSea.CellLy, m.LightYears, what + ": light years");
            }
        }

        [Test]
        public void TheSafeCourse_IsNeverRougherThanTheQuick()
        {
            var chart = Chart();
            var a = Places["verdigris"];
            var b = Places["saltmarch"];
            var quick = chart.Measure(chart.FindCourse(HyperSea.CellOf(a.x, a.y), HyperSea.CellOf(b.x, b.y)), a.z, b.z);
            var safe = chart.Measure(chart.FindCourse(HyperSea.CellOf(a.x, a.y), HyperSea.CellOf(b.x, b.y), true), a.z, b.z);
            Assert.LessOrEqual(safe.MaxDanger, quick.MaxDanger);
            Assert.GreaterOrEqual(safe.Cells, quick.Cells);
        }

        [Test]
        public void TheHeartAndSol_AreClosedToEveryArk()
        {
            var chart = Chart();
            Assert.IsFalse(chart.Passable(0, 0), "the Heart");
            var (sq, sr) = HyperSea.CellOf(0, HyperSea.SolRadius);
            Assert.IsFalse(chart.Passable(sq, sr), "Sol");
        }

        static VoyageContent ChartedContent()
        {
            var c = new VoyageContent();
            void Loc(string id, string place)
            {
                var p = Places[place];
                c.Locations.Add(new VoyageLocation
                {
                    Id = id, Name = id, Charted = true, GalaxyX = (float)p.x, GalaxyY = (float)p.y, GalaxyZ = (float)p.z,
                });
            }
            Loc("harbour", "tumult");
            Loc("tollmoor", "tollmoor");
            Loc("saltmarch", "saltmarch");
            c.Lanes.Add(new VoyageLane { From = "harbour", To = "tollmoor" });
            c.Lanes.Add(new VoyageLane { From = "tollmoor", To = "saltmarch" });
            return c;
        }

        [Test]
        public void AJobsRoute_JoinsItsLegs()
        {
            var chart = new VoyageChart(ChartedContent());
            var voyage = chart.Along(new[] { "harbour", "tollmoor", "saltmarch" });
            Assert.IsNotNull(voyage);
            Assert.AreEqual(27 + 48, voyage.Cells, "the two legs' cells");
            Assert.AreEqual(1500.0, voyage.LightYears);
            Near(2.84879998978251, voyage.ThickestDensity, "the thickest water of either leg");
            Assert.AreEqual(SeaKind.Water, voyage.ThickestSea);
            Assert.AreEqual(2, voyage.Intensity, "the roughest leg");
            Assert.AreEqual(28 + 49 - 1, voyage.Points.Count, "the legs share their meeting cell");
        }

        [Test]
        public void UnchartedContent_HasNoChart()
        {
            var c = ChartedContent();
            foreach (var l in c.Locations) l.Charted = false;
            Assert.IsNull(VoyageChart.For(c));
            Assert.IsNull(new VoyageChart(ChartedContent()).Along(new[] { "harbour" }), "one place is no voyage");
            Assert.IsNull(new VoyageChart(ChartedContent()).Along(new[] { "harbour", "nowhere" }), "an unknown place");
        }
    }
}
#endif
