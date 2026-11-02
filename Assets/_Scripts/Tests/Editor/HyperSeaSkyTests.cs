#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The deep zoom's sky (<see cref="HyperSeaSky"/>, <see cref="HyperSeaSkyCatalog"/>) against the Voyage Board it was
    /// ported from (Docs/Voyages/VOYAGE_BOARD.md §4b). Every golden is the page's own output, printed by running
    /// deepzoom.js in node (<c>node Docs/Voyages/VoyageBoard/sky_golden.js</c>), so a drift in the page or the port fails
    /// here: the luminosity function, the star column, star colours, seven model tiles star by star (counts, positions,
    /// Tumult's invented names) and the view state of five views. A star's magnitude, colour index and level are kept as
    /// floats, so they hold to 1e-5.
    /// </summary>
    [TestFixture]
    public class HyperSeaSkyTests
    {
        const string Table = "Assets/Resources/Voyages/HyperSeaSky.txt";

        static void Near(double expected, double actual, string what)
        {
            double tol = Math.Max(1e-12, Math.Abs(expected) * 1e-9);
            Assert.That(actual, Is.EqualTo(expected).Within(tol), what);
        }

        static void NearF(double expected, double actual, string what) =>
            Assert.That(actual, Is.EqualTo(expected).Within(Math.Max(1e-5, Math.Abs(expected) * 1e-6)), what);

        static HyperSeaSkyCatalog s_Catalog;

        static HyperSeaSkyCatalog Catalog()
        {
            if (s_Catalog != null) return s_Catalog;
            s_Catalog = new HyperSeaSkyCatalog(File.ReadAllText(Table));
            while (!s_Catalog.Step(20000)) { }
            return s_Catalog;
        }

        [Test]
        public void LuminosityFunction_MatchesThePage()
        {
            Near(-10.0000000000000, HyperSeaSky.LogF(-9.0), "logF(-9)");
            Near(-7.03333333333333, HyperSeaSky.LogF(-5.3), "logF(-5.3)");
            Near(-3.00000000000000, HyperSeaSky.LogF(0.0), "logF(0)");
            Near(-1.27100000000000, HyperSeaSky.LogF(4.83), "logF(4.83)");
            Near(-0.752000000000000, HyperSeaSky.LogF(7.7), "logF(7.7)");
            Near(-0.256666666666667, HyperSeaSky.LogF(12.0), "logF(12)");
            Near(-0.0140000000000000, HyperSeaSky.LogF(16.0), "logF(16)");
            Near(-9.00000000000000, HyperSeaSky.Mof(1e-10), "Mof(1e-10)");
            Assert.AreEqual(0, HyperSeaSky.LevelOfF(1e-10), "levelOfF(1e-10)");
            Near(-4.00000000000000, HyperSeaSky.Mof(0.000001), "Mof(0.000001)");
            Assert.AreEqual(5, HyperSeaSky.LevelOfF(0.000001), "levelOfF(0.000001)");
            Near(0.954242509439325, HyperSeaSky.Mof(0.003), "Mof(0.003)");
            Assert.AreEqual(11, HyperSeaSky.LevelOfF(0.003), "levelOfF(0.003)");
            Near(8.00882853426270, HyperSeaSky.Mof(0.2), "Mof(0.2)");
            Assert.AreEqual(14, HyperSeaSky.LevelOfF(0.2), "levelOfF(0.2)");
            Near(14.8658039085473, HyperSeaSky.Mof(0.9), "Mof(0.9)");
            Assert.AreEqual(15, HyperSeaSky.LevelOfF(0.9), "levelOfF(0.9)");
            Near(0.00000000000000, HyperSeaSky.FL(-1), "FL(-1)");
            Near(1.13378684807256e-9, HyperSeaSky.FL(0), "FL(0)");
            Near(0.00000116099773242630, HyperSeaSky.FL(5), "FL(5)");
            Near(0.00118886167800454, HyperSeaSky.FL(10), "FL(10)");
            Near(1.00000000000000, HyperSeaSky.FL(16), "FL(16)");
        }

        [Test]
        public void StarColumn_MatchesThePage()
        {
            Near(9.00000000012809, HyperSeaSky.Sigma(0, 26000).all, "sigma(0, 26000)");
            Near(1.28089988150082e-10, HyperSeaSky.Sigma(0, 26000).bulge, "bulge(0, 26000)");
            Near(256.751867594662, HyperSeaSky.Sigma(900, 2100).all, "sigma(900, 2100)");
            Near(187.075493030953, HyperSeaSky.Sigma(900, 2100).bulge, "bulge(900, 2100)");
            Near(379.676374563709, HyperSeaSky.Sigma(0, 0).all, "sigma(0, 0)");
            Near(310.000000000000, HyperSeaSky.Sigma(0, 0).bulge, "bulge(0, 0)");
            Near(12.1810099315278, HyperSeaSky.Sigma(-12000, 15000).all, "sigma(-12000, 15000)");
            Near(0.0996906996772495, HyperSeaSky.Sigma(-12000, 15000).bulge, "bulge(-12000, 15000)");
            Near(0.176822469093847, HyperSeaSky.Sigma(47000, 0).all, "sigma(47000, 0)");
            Near(4.38231471401185e-87, HyperSeaSky.Sigma(47000, 0).bulge, "bulge(47000, 0)");
            Near(187.649268235594, HyperSeaSky.Sigma(3000, -1500).all, "sigma(3000, -1500)");
            Near(120.815915623400, HyperSeaSky.Sigma(3000, -1500).bulge, "bulge(3000, -1500)");
        }

        [Test]
        public void StarColour_MatchesThePage()
        {
            Assert.AreEqual((155, 176, 255), ToInts(HyperSeaSky.BvColor(-0.4)), "bv(-0.4)");
            Assert.AreEqual((186, 203, 255), ToInts(HyperSeaSky.BvColor(-0.13)), "bv(-0.13)");
            Assert.AreEqual((250, 246, 248), ToInts(HyperSeaSky.BvColor(0.42)), "bv(0.42)");
            Assert.AreEqual((255, 238, 219), ToInts(HyperSeaSky.BvColor(0.65)), "bv(0.65)");
            Assert.AreEqual((255, 221, 180), ToInts(HyperSeaSky.BvColor(1.0)), "bv(1)");
            Assert.AreEqual((255, 190, 120), ToInts(HyperSeaSky.BvColor(1.55)), "bv(1.55)");
            Assert.AreEqual((255, 170, 100), ToInts(HyperSeaSky.BvColor(2.3)), "bv(2.3)");
        }

        static (int, int, int) ToInts((byte r, byte g, byte b) c) => (c.r, c.g, c.b);

        // level, i, j, star count; then the first stars: q, x, y, z, M, ci, level, colour, Tumult's, name, designation
        [Test]
        public void ModelTiles_MatchThePage()
        {
            var names = Catalog().Names;
            {
                var t = HyperSeaSky.GenerateTile(0, 0, 0, names);
                Assert.AreEqual(62, t.Count, "tile 0/0/0 count");
                Star(t, LINES_0_0_0);
            }
            {
                var t = HyperSeaSky.GenerateTile(4, 8, 10, names);
                Assert.AreEqual(181, t.Count, "tile 4/8/10 count");
                Star(t, LINES_4_8_10);
            }
            {
                var t = HyperSeaSky.GenerateTile(8, 129, 131, names);
                Assert.AreEqual(4539, t.Count, "tile 8/129/131 count");
                Star(t, LINES_8_129_131);
            }
            {
                var t = HyperSeaSky.GenerateTile(9, 234, 321, names);
                Assert.AreEqual(392, t.Count, "tile 9/234/321 count");
                Star(t, LINES_9_234_321);
            }
            {
                var t = HyperSeaSky.GenerateTile(12, 2074, 2109, names);
                Assert.AreEqual(4173, t.Count, "tile 12/2074/2109 count");
                Star(t, LINES_12_2074_2109);
            }
            {
                var t = HyperSeaSky.GenerateTile(13, 4096, 5617, names);
                Assert.AreEqual(124, t.Count, "tile 13/4096/5617 count");
                Star(t, LINES_13_4096_5617);
            }
            {
                var t = HyperSeaSky.GenerateTile(14, 8472, 11702, names);
                Assert.AreEqual(111, t.Count, "tile 14/8472/11702 count");
                Star(t, LINES_14_8472_11702);
            }
        }

        static void Star(System.Collections.Generic.List<SkyObject> t,
            (int q, double x, double y, double z, double M, double ci, double lvl, (int, int, int) col, bool tumult, string name,
                string desig)[] stars)
        {
            foreach (var s in stars)
            {
                var o = t[s.q];
                string at = $"star {s.q}";
                Near(s.x, o.X, at + " x");
                Near(s.y, o.Y, at + " y");
                Near(s.z, o.Z, at + " z");
                NearF(s.M, o.M, at + " M");
                NearF(s.ci, o.Ci, at + " ci");
                NearF(s.lvl, o.Level, at + " level");
                Assert.AreEqual(s.col, ((int)o.R, (int)o.G, (int)o.B), at + " colour");
                Assert.AreEqual(s.tumult, o.Kind == SkyKind.TumultStar, at + " Tumult's");
                Assert.AreEqual(s.name, o.Name, at + " name");
                Assert.AreEqual(s.desig, o.Desig, at + " designation");
            }
        }

        static readonly (int, double, double, double, double, double, double, (int, int, int), bool, string, string)[] LINES_0_0_0 =
        {
            (0, -3172.48312290758, 30123.8468056545, 337.136459685426, -8.10177954449229, -0.256957969022915, -0.302267358638346, (163, 184, 255), false, "", ""),
            (1, -1967.37510152161, 5777.69630122930, 861.747594250477, -8.28560221922253, -0.233108175662346, -0.543052436551079, (170, 191, 255), false, "", ""),
            (2, 4476.64204519242, -5496.12752627581, 2034.89628276008, -9.11663706317028, -0.252120755705982, -0.932573359459639, (163, 184, 255), false, "", ""),
        };

        static readonly (int, double, double, double, double, double, double, (int, int, int), bool, string, string)[] LINES_4_8_10 =
        {
            (0, 1372.53450579010, 20365.2635653270, 177.427115793201, -5.08592467589868, -0.292294869455509, 3.25761689641513, (163, 184, 255), false, "", ""),
            (1, 7808.59961640090, 20232.9203661066, 93.8273545151952, -5.21724307645079, -0.294043600885197, 3.14973136992194, (163, 184, 255), false, "", ""),
            (2, 44.5412821136415, 20473.1066248496, 432.643418857110, -4.71206229636182, -0.266119628283195, 3.71570773911662, (163, 184, 255), false, "", ""),
        };

        static readonly (int, double, double, double, double, double, double, (int, int, int), bool, string, string)[] LINES_8_129_131 =
        {
            (0, 1037.73983990322, 1911.58066762000, 5698.41309623854, -2.31292498395279, 1.69592293882743, 5.03964287368581, (255, 187, 117), true, "Rosell", "TC 8-0"),
            (1, 685.170101241965, 1752.42505789356, 680.356114299204, -2.29471279480953, 1.24968071421608, 5.07702966243960, (255, 210, 161), true, "Torar", "TC 8-1"),
            (2, 560.437341609941, 1717.12125371414, 10.6362357965287, -2.17064257376469, -0.299013241101056, 5.37805899046361, (163, 184, 255), true, "Sala Shoal", "TC 8-2"),
        };

        static readonly (int, double, double, double, double, double, double, (int, int, int), bool, string, string)[] LINES_9_234_321 =
        {
            (0, -5757.83347968354, 18031.6532999950, -426.907842586677, -1.10178758459889, -0.0601787584598890, 8.92841752106324, (186, 203, 255), false, "", ""),
            (1, -5750.76733103742, 18012.7195962905, 484.407613777312, -1.68900277604534, 1.23467338029295, 8.30841686530039, (255, 210, 161), false, "", ""),
            (2, -5803.32303371506, 17902.0354295426, -151.864853219224, -1.06201021068953, 1.13133854570333, 8.98754341248423, (255, 215, 171), false, "", ""),
        };

        static readonly (int, double, double, double, double, double, double, (int, int, int), bool, string, string)[] LINES_12_2074_2109 =
        {
            (0, 905.470591383164, 2114.97680281013, 462.762139619075, 2.76831887648323, 0.365247831472484, 11.7383931339718, (250, 246, 248), false, "", ""),
            (1, 913.958782168720, 2090.64725062376, 4076.47852654541, 1.41539877221010, 1.18803997098003, 8.52425785781816, (255, 210, 161), true, "Corane", "TC 12-1"),
            (2, 896.795790171154, 2117.18104782562, -347.422949182604, 1.49804338731743, 1.13586991538759, 8.55995323951356, (255, 215, 171), true, "Tamar", "TC 12-2"),
        };

        static readonly (int, double, double, double, double, double, double, (int, int, int), bool, string, string)[] LINES_13_4096_5617 =
        {
            (0, 11.6845105554830, 26005.0412059900, 358.004705850566, 4.43019413290425, 0.636038826580850, 12.3790039478336, (255, 244, 234), false, "", ""),
            (1, 14.0939579114274, 25996.7357025766, -83.3267765235723, 3.80952942284970, 0.521429413427455, 12.1430038628168, (253, 245, 241), false, "", ""),
            (2, 7.28200489618303, 26007.0150524012, -1692.60521985019, 4.47240702846476, 0.644481405692953, 12.4000812529121, (255, 244, 234), false, "", ""),
        };

        static readonly (int, double, double, double, double, double, double, (int, int, int), bool, string, string)[] LINES_14_8472_11702 =
        {
            (0, 2395.00811950819, 29994.8609531503, -2.82461455933896, 9.26593839083416, 1.50127507126673, 13.8948416737840, (255, 195, 130), false, "", ""),
            (1, 2397.50815581004, 29994.4535892553, 208.801632881106, 5.73466019894334, 0.896932039788668, 13.0205894692335, (255, 226, 190), false, "", ""),
            (2, 2392.60405761286, 30000.2764560651, 1320.51424086573, 9.22211278427922, 1.49776902274234, 13.8804670434911, (255, 195, 130), false, "", ""),
        };

        [Test]
        public void ViewState_MatchesThePage()
        {
            var places = Catalog().Landmarks;
            {
                var v = HyperSeaSky.ViewOf(0, 4000, 110000, 0.6, places);
                Near(0.816905067601576, v.Lcut, "Lcut at 0,4000 w 110000");
                Near(275000.000000000, v.Zwin, "zwin at w 110000");
                Assert.AreEqual(false, v.Slab, "slab at w 110000");
                Near(0.00000000000000, v.Zf, "focus height at 0,4000 w 110000");
                Assert.AreEqual(0, HyperSeaSky.TierOf(110000), "tier at w 110000 (Galaxy)");
            }
            {
                var v = HyperSeaSky.ViewOf(900, 2100, 4500, 0.6, places);
                Near(4.03422372723224, v.Lcut, "Lcut at 900,2100 w 4500");
                Near(11250.0000000000, v.Zwin, "zwin at w 4500");
                Assert.AreEqual(false, v.Slab, "slab at w 4500");
                Near(0.00000000000000, v.Zf, "focus height at 900,2100 w 4500");
                Assert.AreEqual(2, HyperSeaSky.TierOf(4500), "tier at w 4500 (Sector)");
            }
            {
                var v = HyperSeaSky.ViewOf(0, 26000, 280, 0.6, places);
                Near(10.6784931833058, v.Lcut, "Lcut at 0,26000 w 280");
                Near(700.000000000000, v.Zwin, "zwin at w 280");
                Assert.AreEqual(true, v.Slab, "slab at w 280");
                Near(65.0000000000000, v.Zf, "focus height at 0,26000 w 280");
                Assert.AreEqual(4, HyperSeaSky.TierOf(280), "tier at w 280 (Neighbourhood)");
            }
            {
                var v = HyperSeaSky.ViewOf(900, 2100, 50, 0.6, places);
                Near(11.9875080611299, v.Lcut, "Lcut at 900,2100 w 50");
                Near(125.000000000000, v.Zwin, "zwin at w 50");
                Assert.AreEqual(true, v.Slab, "slab at w 50");
                Near(30.0000000000000, v.Zf, "focus height at 900,2100 w 50");
                Assert.AreEqual(5, HyperSeaSky.TierOf(50), "tier at w 50 (Cell)");
            }
            {
                var v = HyperSeaSky.ViewOf(0, 26000, 10, 0.6, places);
                Near(14.8581017074408, v.Lcut, "Lcut at 0,26000 w 10");
                Near(30.0000000000000, v.Zwin, "zwin at w 10");
                Assert.AreEqual(true, v.Slab, "slab at w 10");
                Near(65.0000000000000, v.Zf, "focus height at 0,26000 w 10");
                Assert.AreEqual(6, HyperSeaSky.TierOf(10), "tier at w 10 (System)");
            }
        }

        [Test]
        public void Table_HoldsTheBoardsSky()
        {
            var c = Catalog();
            Assert.AreEqual(97293, c.StarCount, "catalogue stars");
            Assert.AreEqual(5799, c.Clusters.Count, "clusters");
            Assert.AreEqual(48, c.Landmarks.Count, "landmarks");
            Assert.AreEqual(30, c.Names.First.Count, "first syllables");
            Assert.IsTrue(c.Sources["stars"].Contains("CC BY-SA 4.0"), "the HYG credit");
            var sol = c.Landmarks.Single(p => p.Id == "sol");
            Assert.AreEqual((0.0, 26000.0, 65.0), (sol.X, sol.Y, sol.Z), "Sol");
            var pleiades = c.Clusters.Where(p => p.Name == "Pleiades").ToList();
            Assert.AreEqual(0, pleiades.Count, "the Pleiades are a landmark, not a second cluster");
            Assert.IsTrue(c.Clusters.Any(p => p.Name == "Jewel Box" && p.Famous), "famous clusters keep their names");
            Assert.IsTrue(c.Stars.Values.SelectMany(l => l).Any(s => s.Name == "Deneb" && s.Rank == 3), "Deneb");
        }
    }
}
#endif
