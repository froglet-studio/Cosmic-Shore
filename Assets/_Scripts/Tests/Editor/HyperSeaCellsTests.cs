#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The HyperSea's cells in 3D (<see cref="HyperSeaCells"/>) against the Voyage Board they were ported from
    /// (Docs/Voyages/VOYAGE_BOARD.md §4c, §4d: the wet foam). Every golden is the page's own output, printed by running cells.js in node
    /// (<c>node Docs/Voyages/VoyageBoard/cells_golden.js</c>): the nucleus of the cell a point is in, the slice's snap to a
    /// layer of nuclei, the size fades, ten plane and upright sections polygon by polygon with their middles and nucleus
    /// radii, the cells a slice crosses, the cell a view names, and a wet outline.
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
            (9.9, 9.9, 4.9, 10.3776032311210, 8.40328440946803, 6.80769713684678),
            (12, 3, 2, 10.3776032311210, 8.40328440946803, 6.80769713684678),
            (-31, 47, 18, -33.0157261355139, 46.3162900733679, 7.68732756759680),
            (900, 2100, 30, 899.740523090700, 2100.25306929836, 36.8692176526856),
            (-14.2, 26003.4, 65.5, -9.25508949261054, 26009.8018814969, 68.1994377532193),
            (5, 5, 5, 10.3776032311210, 8.40328440946803, 6.80769713684678),
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
            (double, double, double) v, (double, double)[] poly, double cu = 0, double cv = 0, double h = 0, double mu = 0,
            double mv = 0, double mh = 0, double nucleusR = 0)
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
            Near(mu, s.Mu, at + " middle u");
            Near(mv, s.Mv, at + " middle v");
            Near(mh, s.Mh, at + " middle h");
            Near(nucleusR, s.NucleusR, at + " nucleus radius");
        }

        [Test]
        public void Sections_MatchThePage()
        {
            Section((0,0,-5), (0,0,-5), (1,0,0), (0,1,0), new (double, double)[] { (1.69340125118006,-10.6975663092086), (6.53606388001617,-11.7252317443331), (10.0826997479121,4.15941478554593), (4.17633810519926,9.43278633224613), (-5.76621095037516,10.2249993290731), (-11.7927545878204,2.67597580150962), (-11.7151732505515,0.754794608321337) }, 0.00000000000000, 0.00000000000000, 0.00000000000000, 0.301579787520530, 0.277702210805835, -0.396467210031338, 3.20708078542677);
            Section((0,0,-5), (0,0,0), (1,0,0), (0,1,0), new (double, double)[] { (3.83775092074266,-8.84640268272799), (9.81980450376762,-0.0806641213667785), (10.0290327610939,0.856425852198092), (3.41356858100618,9.02616748004209), (-2.35496775132155,9.48579905234761), (-9.38206172368880,2.44473536081305) }, 0.00000000000000, 0.00000000000000, -5.00000000000000, 0.301579787520530, 0.277702210805835, -5.39646721003134, 3.20708078542677);
            Section((10,10,5), (0,0,0), (1,0,0), (0,1,0), new (double, double)[] { (10.0290327610939,0.856425852198092), (16.8434452131379,5.45457471465784), (16.5923032008771,9.55272496658071), (10.8746883320183,14.0283691995325), (3.41356858100619,9.02616748004209) }, 10.3776032311210, 8.40328440946803, 6.80769713684678, 9.79679006486769, 7.81591761079162, 6.38855300695706, 2.81275983385860);
            Section((10,10,5), (0,0,-5), (1,0,0), (0,1,0), null);
            Section((0,0,-5), (0,0,9), (1,0,0), (0,1,0), null);
            Section((20,0,-5), (0,0,7.5), (1,0,0), (0,1,0), null);
            Section((0,0,-5), (0,3,0), (1,0,0), (0,0,1), new (double, double)[] { (-11.7654938078989,-4.69721792155409), (-2.64596667938980,-16.6291573121994), (0.869956606840955,-15.8274584425494), (9.56845583230734,-6.86734875040125), (10.2700375156357,-1.73737329482772), (1.05225541582768,6.36399348263033), (-2.44211459867641,6.36942186477990), (-11.4939438263847,-2.65925064499194) }, 0.00000000000000, -5.00000000000000, 3.00000000000000, 0.301579787520530, -5.39646721003134, 2.72229778919416, 3.20708078542677);
            Section((10,10,5), (0,4,0), (1,0,0), (0,0,1), new (double, double)[] { (0.102366273437049,6.48715790723888), (10.3824680994769,-2.54786532292615), (17.1536168723589,1.45923393194002), (17.9364117297368,6.62718800542920), (10.6074492904190,15.5037174797654), (9.35456741148661,15.6778360490402) }, 10.3776032311210, 6.80769713684678, -4.40328440946803, 9.79679006486769, 6.38855300695706, -3.81591761079162, 2.81275983385860);
            Section((900,2100,35), (0,0,35), (1,0,0), (0,1,0), new (double, double)[] { (886.666162170175,2100.04250011997), (895.178395011365,2090.30289148179), (898.148873647853,2088.30029824457), (905.272454539966,2089.06110611243), (911.647472404786,2097.21247743105), (907.967791725274,2108.87511064583), (906.398122453091,2110.54693031473), (898.410132834552,2109.28844100066), (891.396627012756,2106.13124121512), (886.677909179074,2101.90228246682) }, 899.740523090700, 2100.25306929836, 1.86921765268565, 899.838016737539, 2099.82390974257, 1.22273690722535, 3.31395147780790);
            Section((0,0,-5), (0,0,20), (1,0,0), (0,1,0), null);
        }

        [Test]
        public void SliceCrossings_MatchThePage()
        {
            var nuclei = new List<(double x, double y, double z)>();
            HyperSeaCells.NucleiInSlice(0, -30, -30, 30, 30, nuclei);
            Assert.AreEqual(290, nuclei.Count, "the cells a slice may cross over a box, both lattices and their knots");
            Assert.IsTrue(nuclei.Contains((0, 0, -5)), "the Heart, which never drifts");
            Near(-23.2271049671222, nuclei[0].x, "first x"); Near(-38.8007909791006, nuclei[0].y, "first y"); Near(-21.6282055063667, nuclei[0].z, "first z");
            Near(31.4717477953892, nuclei[nuclei.Count - 1].x, "last x"); Near(27.7018840961093, nuclei[nuclei.Count - 1].y, "last y"); Near(21.5672969225324, nuclei[nuclei.Count - 1].z, "last z");
            Near(0.00000000000000, HyperSeaCells.NearestInSlice(1, 1, -5).x, "nearest x at 1,1");
            Near(0.00000000000000, HyperSeaCells.NearestInSlice(1, 1, -5).y, "nearest y at 1,1");
            Near(-5.00000000000000, HyperSeaCells.NearestInSlice(1, 1, -5).z, "nearest z at 1,1");
            Near(32.3174687856147, HyperSeaCells.NearestInSlice(33, -12, 5).x, "nearest x at 33,-12");
            Near(-11.2697125688963, HyperSeaCells.NearestInSlice(33, -12, 5).y, "nearest y at 33,-12");
            Near(6.99215224494343, HyperSeaCells.NearestInSlice(33, -12, 5).z, "nearest z at 33,-12");
            Near(899.740523090700, HyperSeaCells.NearestInSlice(900, 2100, 35).x, "nearest x at 900,2100");
            Near(2100.25306929836, HyperSeaCells.NearestInSlice(900, 2100, 35).y, "nearest y at 900,2100");
            Near(36.8692176526856, HyperSeaCells.NearestInSlice(900, 2100, 35).z, "nearest z at 900,2100");
        }

        [Test]
        public void WetOutline_MatchesThePage()
        {
            var poly = new List<(double u, double v)>();
            Assert.IsTrue(HyperSeaCells.TrySection((0, 0, -5), (0, 0, -5), (1, 0, 0), (0, 1, 0), poly, out _), "the Heart's cell at its own height");
            var wet = new List<(double u, double v)>();
            Assert.IsTrue(HyperSeaCells.TryWetOutline(poly, wet, 0), "a full cell is not a sliver");
            var expected = new (double, double)[] { (1.85307252628986,-10.3736562134532), (6.26994146020581,-11.3109636169944), (9.69634029470293,4.03516617576192), (4.03110992678956,9.09324870352507), (-5.60867763109172,9.86133794777400), (-11.4377716677424,2.55964552267610), (-11.3716261669755,0.921655039902152) };
            Assert.AreEqual(expected.Length, wet.Count, "corners");
            for (int i = 0; i < expected.Length; i++)
            {
                Near(expected[i].Item1, wet[i].u, $"corner {i} u");
                Near(expected[i].Item2, wet[i].v, $"corner {i} v");
            }
            Assert.IsTrue(HyperSeaCells.TryWetOutline(poly, wet, 3), "rounded");
            Assert.AreEqual(expected.Length * 4, wet.Count, "each corner sampled four times");
        }

        [Test]
        public void Knots_ShareTheirLatticePoint()
        {
            // (-60, 80, -25) is a lattice point that splits into a knot (cells_golden.js "near" lines): its small cells
            // read the same sea as their parent
            var parent = HyperSeaCells.CellAt(-56.0778801634220, 80.8711132044495, -27.0664555750451);
            var child = HyperSeaCells.CellAt(-54.8931689168562, 78.9670648156591, -22.3748610704652);
            Assert.AreNotEqual(parent, child, "two cells");
            Assert.AreEqual(HyperSeaCells.KeyOf(parent), HyperSeaCells.KeyOf(child), "one lattice point");
            Assert.AreEqual(HyperSeaCells.LatticePoint(parent), HyperSeaCells.LatticePoint(child), "one sea");
        }
    }
}
#endif
