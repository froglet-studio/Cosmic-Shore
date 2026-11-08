#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The HyperSea's cells in 3D (<see cref="HyperSeaCells"/>) against the Voyage Board they were ported from
    /// (Docs/Voyages/VOYAGE_BOARD.md §4c). Every golden is the page's own output, printed by running cells.js in node
    /// (<c>node Docs/Voyages/VoyageBoard/cells_golden.js</c>): the nucleus of the cell a point is in, the slice's snap to a
    /// layer of nuclei, the size fades, and ten plane and upright sections polygon by polygon.
    /// </summary>
    [TestFixture]
    public class HyperSeaCellsTests
    {
        static void Near(double expected, double actual, string what) =>
            Assert.That(actual, Is.EqualTo(expected).Within(Math.Max(1e-12, Math.Abs(expected) * 1e-9)), what);

        static readonly (double x, double y, double z, double cx, double cy, double cz)[] CellAtGoldens =
        {
            (0, 0, 0, 0.00000000000000, 0.00000000000000, -5.00000000000000),
            (7, -3, -5, 0.00000000000000, 0.00000000000000, -5.00000000000000),
            (9.9, 9.9, 4.9, 10.0000000000000, 10.0000000000000, 5.00000000000000),
            (12, 3, 2, 10.0000000000000, 10.0000000000000, 5.00000000000000),
            (-31, 47, 18, -30.0000000000000, 50.0000000000000, 25.0000000000000),
            (900, 2100, 30, 900.000000000000, 2100.00000000000, 35.0000000000000),
            (-14.2, 26003.4, 65.5, -10.0000000000000, 26010.0000000000, 65.0000000000000),
            (5, 5, 5, 10.0000000000000, 10.0000000000000, 5.00000000000000),
        };

        [Test]
        public void CellAt_MatchesThePage()
        {
            foreach (var g in CellAtGoldens)
            {
                var c = HyperSeaCells.CellAt(g.x, g.y, g.z);
                string at = $"cellAt {g.x},{g.y},{g.z}";
                Near(g.cx, c.x, at + " x");
                Near(g.cy, c.y, at + " y");
                Near(g.cz, c.z, at + " z");
            }
        }

        [Test]
        public void Heart_IsTheCentralNucleus()
        {
            Assert.IsTrue(HyperSeaCells.IsHeart(HyperSeaCells.CellAt(1, -2, -4)), "the point beside Sgr A* is in the Heart's cell");
            Assert.IsFalse(HyperSeaCells.IsHeart(HyperSeaCells.CellAt(10, 10, 5)), "a neighbour is not");
        }

        static readonly (double focus, double slice)[] SliceGoldens =
        {
            (-5, -5.00000000000000),
            (0, 5.00000000000000),
            (30, 35.0000000000000),
            (34.9, 35.0000000000000),
            (65, 65.0000000000000),
            (-17.6, -15.0000000000000),
            (600, 600.000000000000),
            (-620, -600.000000000000),
        };

        [Test]
        public void Slice_SnapsToALayerOfNuclei()
        {
            foreach (var g in SliceGoldens) Near(g.slice, HyperSeaCells.SliceZ(g.focus, 0), $"slice at focus {g.focus}");
            Near(37.5, HyperSeaCells.SliceZ(30, 2.5), "the reader's offset adds to the snapped layer");
        }

        static readonly (double px, double membrane, double nucleus)[] FadeGoldens =
        {
            (10, 0.00000000000000, 0.00000000000000),
            (22, 0.00000000000000, 0.00000000000000),
            (30, 0.363636363636364, 0.225000000000000),
            (44, 1.00000000000000, 0.796666666666666),
            (60, 1.00000000000000, 1.00000000000000),
            (100, 1.00000000000000, 1.00000000000000),
        };

        [Test]
        public void Fades_MatchThePage()
        {
            foreach (var g in FadeGoldens)
            {
                Near(g.membrane, HyperSeaCells.MembraneAlpha(g.px), $"membrane at {g.px} px");
                Near(g.nucleus, HyperSeaCells.NucleusAlpha(g.px), $"nucleus at {g.px} px");
            }
        }

        static void Section((double, double, double) c, (double, double, double) o, (double, double, double) u,
            (double, double, double) v, (double, double)[] poly, double cu = 0, double cv = 0, double h = 0)
        {
            var got = new List<(double u, double v)>();
            bool hit = HyperSeaCells.TrySection(c, o, u, v, got, out var s);
            string at = $"section of {c} by the plane through {o}";
            if (poly == null)
            {
                Assert.IsFalse(hit, at + " misses");
                return;
            }
            Assert.IsTrue(hit, at + " cuts it");
            Assert.AreEqual(poly.Length, got.Count, at + " corners");
            for (int i = 0; i < poly.Length; i++)
            {
                Near(poly[i].Item1, got[i].u, $"{at} corner {i} u");
                Near(poly[i].Item2, got[i].v, $"{at} corner {i} v");
            }
            Near(cu, s.Cu, at + " cu");
            Near(cv, s.Cv, at + " cv");
            Near(h, s.H, at + " h");
        }

        [Test]
        public void Sections_MatchThePage()
        {
            Section((0,0,-5), (0,0,-5), (1,0,0), (0,1,0), new (double, double)[] { (10.0000000000000,-5.00000000000000), (10.0000000000000,-5.00000000000000), (10.0000000000000,5.00000000000000), (10.0000000000000,5.00000000000000), (5.00000000000000,10.0000000000000), (5.00000000000000,10.0000000000000), (-5.00000000000000,10.0000000000000), (-5.00000000000000,10.0000000000000), (-10.0000000000000,5.00000000000000), (-10.0000000000000,5.00000000000000), (-10.0000000000000,-5.00000000000000), (-10.0000000000000,-5.00000000000000), (-5.00000000000000,-10.0000000000000), (-5.00000000000000,-10.0000000000000), (5.00000000000000,-10.0000000000000), (5.00000000000000,-10.0000000000000) }, 0.00000000000000, 0.00000000000000, 0.00000000000000);
            Section((0,0,-5), (0,0,0), (1,0,0), (0,1,0), new (double, double)[] { (10.0000000000000,0.00000000000000), (10.0000000000000,0.00000000000000), (0.00000000000000,10.0000000000000), (0.00000000000000,10.0000000000000), (-10.0000000000000,0.00000000000000), (-10.0000000000000,0.00000000000000), (0.00000000000000,-10.0000000000000), (0.00000000000000,-10.0000000000000) }, 0.00000000000000, 0.00000000000000, -5.00000000000000);
            Section((10,10,5), (0,0,0), (1,0,0), (0,1,0), new (double, double)[] { (20.0000000000000,10.0000000000000), (20.0000000000000,10.0000000000000), (10.0000000000000,20.0000000000000), (10.0000000000000,20.0000000000000), (0.00000000000000,10.0000000000000), (0.00000000000000,10.0000000000000), (10.0000000000000,0.00000000000000), (10.0000000000000,0.00000000000000) }, 10.0000000000000, 10.0000000000000, 5.00000000000000);
            Section((10,10,5), (0,0,-5), (1,0,0), (0,1,0), new (double, double)[] { (15.0000000000000,10.0000000000000), (10.0000000000000,15.0000000000000), (5.00000000000000,10.0000000000000), (10.0000000000000,5.00000000000000) }, 10.0000000000000, 10.0000000000000, 10.0000000000000);
            Section((0,0,-5), (0,0,9), (1,0,0), (0,1,0), null);
            Section((20,0,-5), (0,0,7.5), (1,0,0), (0,1,0), null);
            Section((0,0,-5), (0,3,0), (1,0,0), (0,0,1), new (double, double)[] { (10.0000000000000,-7.00000000000000), (10.0000000000000,-3.00000000000000), (2.00000000000000,5.00000000000000), (-2.00000000000000,5.00000000000000), (-10.0000000000000,-3.00000000000000), (-10.0000000000000,-7.00000000000000), (-2.00000000000000,-15.0000000000000), (2.00000000000000,-15.0000000000000) }, 0.00000000000000, -5.00000000000000, 3.00000000000000);
            Section((10,10,5), (0,4,0), (1,0,0), (0,0,1), new (double, double)[] { (19.0000000000000,5.00000000000000), (10.0000000000000,14.0000000000000), (1.00000000000000,5.00000000000000), (10.0000000000000,-4.00000000000000) }, 10.0000000000000, 5.00000000000000, -6.00000000000000);
            Section((900,2100,35), (0,0,35), (1,0,0), (0,1,0), new (double, double)[] { (910.000000000000,2095.00000000000), (910.000000000000,2095.00000000000), (910.000000000000,2105.00000000000), (910.000000000000,2105.00000000000), (905.000000000000,2110.00000000000), (905.000000000000,2110.00000000000), (895.000000000000,2110.00000000000), (895.000000000000,2110.00000000000), (890.000000000000,2105.00000000000), (890.000000000000,2105.00000000000), (890.000000000000,2095.00000000000), (890.000000000000,2095.00000000000), (895.000000000000,2090.00000000000), (895.000000000000,2090.00000000000), (905.000000000000,2090.00000000000), (905.000000000000,2090.00000000000) }, 900.000000000000, 2100.00000000000, 0.00000000000000);
            Section((0,0,-5), (0,0,20), (1,0,0), (0,1,0), null);
        }

        [Test]
        public void SliceCrossesBothLattices()
        {
            var nuclei = new List<(double x, double y, double z)>();
            HyperSeaCells.NucleiInSlice(0, -15, -15, 15, 15, nuclei);
            Assert.IsTrue(nuclei.Contains((0, 0, -5)), "the Heart's layer");
            Assert.IsTrue(nuclei.Contains((10, 10, 5)), "the offset layer");
            var c = HyperSeaCells.NearestInSlice(1, 1, -5);
            Assert.AreEqual((0.0, 0.0, -5.0), c, "the cell named at the Heart");
        }
    }
}
#endif
