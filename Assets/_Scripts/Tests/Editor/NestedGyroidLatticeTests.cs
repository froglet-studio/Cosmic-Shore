using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;
using NVec = System.Numerics.Vector3;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Locks the nested gyroid's guarantees on the shipped default config (Docs/ECOSYSTEM.md §58). A PLANT is one
    /// octagon tile of the gyroid flora's tiling on every nested sheet, every prism hanging off its crystal through
    /// spindles; a COLONY is plants at tiles of one shared periodic frame, born one at a time at random open tiles.
    /// Tools/Build/nested_gyroid_harness runs the same rule headless with negative controls, grown colonies and the
    /// ride model; this is the in-editor twin.
    ///
    /// Under an Editor/ folder deliberately (see CLAUDE.md): NUnit attributes in Assembly-CSharp break IL2CPP.
    /// </summary>
    public class NestedGyroidLatticeTests
    {
        static NestedGyroidPeriod s_period;

        static NestedGyroidPeriod Default => s_period ??= NestedGyroidBuilder.BuildNow(new NestedGyroidSettings());

        static IEnumerable<NestedGyroidLattice> AllPlants()
        {
            for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++) yield return Default.Plant(o);
        }

        // ------------------------------------------------------------------ the template

        [Test]
        public void Template_IsTheMeasuredGyroidFloraCell()
        {
            Assert.AreEqual(576, NestedGyroidTemplate.SiteCount);
            Assert.AreEqual(120f, NestedGyroidTemplate.Period, 0.5f);
            int danger = 0;
            for (int k = 0; k < NestedGyroidTemplate.SiteCount; k++)
            {
                var q = NestedGyroidTemplate.Position[k] * (2f * Mathf.PI);
                Assert.Less(Mathf.Abs(NestedGyroidBuilder.G(q)), 0.15f, "a template site is off the gyroid");
                if (NestedGyroidTemplate.IsDangerType(NestedGyroidTemplate.BlockType[k])) danger++;
            }
            Assert.AreEqual(192, danger);
        }

        [Test]
        public void Template_TilesThePeriodIntoTwentyFourOctagons()
        {
            // The gyroid flora's "24 prisms a lifeform": every site owned by one octagon, 23-25 each, 8 of them its ring.
            Assert.AreEqual(24, NestedGyroidTemplate.OctagonCount);
            var owned = new int[NestedGyroidTemplate.OctagonCount];
            var ring = new int[NestedGyroidTemplate.OctagonCount];
            for (int k = 0; k < NestedGyroidTemplate.SiteCount; k++)
            {
                int o = NestedGyroidTemplate.SiteOwner[k];
                owned[o]++;
                if (NestedGyroidTemplate.IsDangerType(NestedGyroidTemplate.BlockType[k])) ring[o]++;
            }
            for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++)
            {
                Assert.That(owned[o], Is.InRange(23, 25), $"octagon {o}");
                Assert.AreEqual(8, ring[o], $"octagon {o} does not own its own ring");
            }
        }

        [Test]
        public void Template_NeighbourTableIsSymmetric()
        {
            for (int o = 0; o < NestedGyroidTemplate.OctagonCount; o++)
            {
                var t = new NestedGyroidColony.Tile(0, 0, 0, o);
                var n = t.Neighbors().ToList();
                Assert.AreEqual(4, n.Count);
                foreach (var m in n) CollectionAssert.Contains(m.Neighbors().ToList(), t, $"{m} does not list {t} back");
            }
        }

        // ------------------------------------------------------------------ one plant

        [Test]
        public void EveryPlant_IsOneTileOnEverySheet_WithinBudget()
        {
            var budget = new NestedGyroidSettings().PrismBudget;
            foreach (var l in AllPlants())
            {
                Assert.LessOrEqual(l.Count, budget);
                Assert.AreEqual(0, l.Stats.TruncatedByBudget, $"plant {l.Octagon} was cut by the budget");
                Assert.AreEqual(7, l.Stats.SheetsGrown);
                Assert.Greater(l.Stats.FiberPrisms, 0, $"plant {l.Octagon} has no struts: its sheets would be separate shells");
                var seen = new HashSet<(int, int)>();
                for (int i = 0; i < l.Count; i++)
                {
                    if (l.Kind[i] != NestedGyroidPrismKind.Sheet) continue;
                    Assert.IsTrue(seen.Add((l.Sheet[i], l.Site[i])), "a site appears twice on one sheet");
                    Assert.AreEqual(l.Octagon, NestedGyroidTemplate.SiteOwner[l.Site[i]], "a plate outside the plant's tile");
                }
                Assert.Less(l.Position.Take(l.Count).Max(p => p.Length()), 0.5f * new NestedGyroidSettings().CellSize,
                            "a plant is one tile, never a cube of the lattice");
            }
        }

        [Test]
        public void EveryPrism_HangsOffTheCrystal()
        {
            foreach (var l in AllPlants())
            {
                Assert.AreEqual(l.Count, l.Stats.RootedPrisms, $"plant {l.Octagon}");
                int roots = 0;
                for (int i = 0; i < l.Count; i++)
                {
                    Assert.Less(l.Parent[i], i, "a parent must be laid first");
                    if (l.Parent[i] >= 0) continue;
                    roots++;
                    Assert.IsTrue(l.DangerRing[i] && l.Rank[i] == 0, "the crystal's own limbs land on its octagon ring");
                }
                Assert.GreaterOrEqual(roots, 8);
            }
        }

        [Test]
        public void TheOctagonRing_IsDangerOnEverySheet()
        {
            foreach (var l in AllPlants())
                for (int sheet = 0; sheet < l.SheetCount; sheet++)
                {
                    int ring = 0;
                    for (int i = 0; i < l.Count; i++)
                        if (l.Kind[i] == NestedGyroidPrismKind.Sheet && l.Sheet[i] == sheet && l.DangerRing[i]) ring++;
                    Assert.GreaterOrEqual(ring, 6, $"plant {l.Octagon} sheet {sheet}");
                }
        }

        [Test]
        public void WithoutCrossSheetLimbs_APlantFallsApartIntoShells()
        {
            // The negative control for the claim the species exists to make: the columns and struts are the stack.
            var l = Default.Plant(0);
            var parent = Enumerable.Range(0, l.Count).ToArray();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            for (int i = 0; i < l.Count; i++)
            {
                int j = l.Parent[i];
                if (j >= 0 && l.Kind[i] == NestedGyroidPrismKind.Sheet && l.Kind[j] == NestedGyroidPrismKind.Sheet && l.Sheet[i] == l.Sheet[j])
                    parent[Find(i)] = Find(j);
            }
            var roots = new HashSet<int>();
            for (int i = 0; i < l.Count; i++) if (l.Kind[i] == NestedGyroidPrismKind.Sheet) roots.Add(Find(i));
            Assert.GreaterOrEqual(roots.Count, l.Stats.SheetsGrown);
        }

        [Test]
        public void EveryPrismPointsUpTheStack()
        {
            // The layered ride reads +z as "up G": a strut on a negative-t gap must not point down it.
            float cell = new NestedGyroidSettings().CellSize;
            float scale = cell / (2f * Mathf.PI);
            foreach (var l in AllPlants())
            {
                var centre = NestedGyroidTemplate.OctagonCenter[l.Octagon] * cell;
                for (int i = 0; i < l.Count; i++)
                    Assert.Greater(NVec.Dot(l.Forward[i], NestedGyroidBuilder.Grad((l.Position[i] + centre) / scale)), 0f);
            }
        }

        // ------------------------------------------------------------------ plants side by side

        [Test]
        public void APlantAndItsNeighbours_NeverInterpenetrate()
        {
            Assert.AreEqual(0, Default.Stats.RemainingOverlaps);
            float cell = new NestedGyroidSettings().CellSize;
            var tiles = new List<NestedGyroidColony.Tile> { new NestedGyroidColony.Tile(0, 0, 0, 0) };
            tiles.AddRange(tiles[0].Neighbors());
            var pos = new List<NVec>(); var fwd = new List<NVec>(); var up = new List<NVec>(); var size = new List<NVec>();
            foreach (var t in tiles)
            {
                var l = Default.Plant(t.Octagon);
                var c = NestedGyroidLattice.TileCenter(t.X, t.Y, t.Z, t.Octagon, cell);
                for (int i = 0; i < l.Count; i++) { pos.Add(l.Position[i] + c); fwd.Add(l.Forward[i]); up.Add(l.Up[i]); size.Add(l.Size[i]); }
            }
            int overlaps = 0;
            for (int i = 0; i < pos.Count; i++)
            {
                var xi = NVec.Cross(up[i], fwd[i]);
                float ri = 0.5f * size[i].Length();
                for (int j = i + 1; j < pos.Count; j++)
                {
                    float rj = 0.5f * size[j].Length();
                    if (NVec.DistanceSquared(pos[i], pos[j]) > (ri + rj) * (ri + rj)) continue;
                    Assert.Greater(NVec.DistanceSquared(pos[i], pos[j]), 1e-4f, "two plants laid the same prism");
                    var xj = NVec.Cross(up[j], fwd[j]);
                    if (NestedGyroidBuilder.ObbOverlap(pos[i], xi, up[i], fwd[i], 0.5f * size[i], pos[j], xj, up[j], fwd[j], 0.5f * size[j]))
                        overlaps++;
                }
            }
            Assert.AreEqual(0, overlaps);
        }

        [Test]
        public void ObbOverlap_SeesAnOverlap()
        {
            // The checker's own negative control: two unit boxes half a unit apart DO overlap; two units apart do not.
            var x = NVec.UnitX; var y = NVec.UnitY; var z = NVec.UnitZ; var h = new NVec(0.5f);
            Assert.IsTrue(NestedGyroidBuilder.ObbOverlap(NVec.Zero, x, y, z, h, new NVec(0.5f, 0, 0), x, y, z, h));
            Assert.IsFalse(NestedGyroidBuilder.ObbOverlap(NVec.Zero, x, y, z, h, new NVec(2f, 0, 0), x, y, z, h));
        }

        // ------------------------------------------------------------------ the colony book

        [Test]
        public void Colony_GrowsTileByTile_NeverClaimingTwice()
        {
            Random.InitState(7);
            var book = NestedGyroidColony.Found(null, null, Vector3.zero, Quaternion.identity, 240f);
            var founder = new NestedGyroidColony.Tile(0, 0, 0, 3);
            Assert.IsTrue(book.TryClaim(founder, null));
            Assert.IsFalse(book.TryClaim(founder, null), "a claimed tile cannot be claimed again");
            var members = new List<NestedGyroidColony.Tile> { founder };
            for (int birth = 0; birth < 40; birth++)
            {
                foreach (var m in members) book.ContributeNeighbors(m);
                Assert.IsTrue(book.TryPopRandom(out var tile));
                Assert.IsFalse(book.IsClaimed(tile));
                Assert.IsTrue(members.Any(m => m.Neighbors().Contains(tile)), "a birth must border a living plant");
                Assert.IsTrue(book.TryClaim(tile, null));
                members.Add(tile);
            }
            Assert.AreEqual(41, book.Members);
            Assert.AreEqual(members.Count, members.Distinct().Count());
        }

        [Test]
        public void Colony_ASeedJoinsAGrowingFounder_AndADeathFreesItsTile()
        {
            Random.InitState(3);
            var book = NestedGyroidColony.Found(null, null, Vector3.zero, Quaternion.identity, 240f);
            var founder = new NestedGyroidColony.Tile(0, 0, 0, 5);
            book.TryClaim(founder, null);
            Assert.AreEqual(0, book.OpenTiles, "an immature founder has offered nothing yet");
            Assert.IsTrue(book.TryAnyOpenTile(out var joined));
            CollectionAssert.Contains(founder.Neighbors().ToList(), joined, "a seed joins BESIDE the founder");
            Assert.IsFalse(book.TryAnyOpenTile(out _, t => false), "a tile the plant refuses (a control-zone nucleus) is never offered");

            book.TryClaim(joined, null);
            book.Release(joined, null);
            Assert.IsFalse(book.IsClaimed(joined));
            Assert.IsTrue(book.TryPopRandom(out var back) && back.Equals(joined), "the freed tile is open lattice again");
        }

        // ------------------------------------------------------------------ settings

        [Test]
        public void TMax_IsClampedBelowTheCriticalValues()
        {
            var s = new NestedGyroidSettings { TMax = 2f }.Sanitized();
            Assert.Less(s.TMax, Mathf.Sqrt(2f));
        }

        [Test]
        public void ConfigDefaults_GrowTheMeasuredPeriod()
        {
            // The SO's defaults and the settings' defaults are one period - the harness measures the latter. The
            // leaf goes through CellSize / NestedGyroidTemplate.Period (119.99), so it lands a hair off 18 x 6.8 x 3.
            var so = ScriptableObject.CreateInstance<NestedGyroidConfigSO>();
            try
            {
                var fromSo = so.ToSettings(NestedGyroidConfigSO.TemplateTimeLeaf);
                var expected = new NestedGyroidSettings();
                Assert.AreEqual(expected.Leaf.X, fromSo.Leaf.X, 0.01f);
                Assert.AreEqual(expected.Leaf.Y, fromSo.Leaf.Y, 0.01f);
                Assert.AreEqual(expected.Leaf.Z, fromSo.Leaf.Z, 0.01f);
                expected.Leaf = fromSo.Leaf;
                Assert.AreEqual(expected.Sanitized().Key(), fromSo.Key());
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var a = Default.Plant(0);
            var b = NestedGyroidBuilder.BuildNow(new NestedGyroidSettings()).Plant(0);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.Position[i], b.Position[i]);
                Assert.AreEqual(a.Parent[i], b.Parent[i]);
            }
        }
    }
}
