using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;
using NVec = System.Numerics.Vector3;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Locks the nested gyroid's acceptance guarantees on the shipped default config (Docs/ECOSYSTEM.md §58):
    /// the budget, ONE connected component, every bond inside the Urchin's reach, zero interpenetration, a growth
    /// order that hangs every prism off one already standing, and that the FIBERS - not the sheets - are what
    /// make it one object. Tools/Build/nested_gyroid_harness runs the same rule headless with negative controls
    /// and the ride model; this is the in-editor twin.
    ///
    /// Under an Editor/ folder deliberately (see CLAUDE.md): NUnit attributes in Assembly-CSharp break IL2CPP.
    /// </summary>
    public class NestedGyroidLatticeTests
    {
        static NestedGyroidLattice s_lattice;

        static NestedGyroidLattice Default => s_lattice ??= NestedGyroidBuilder.BuildNow(new NestedGyroidSettings());

        static IEnumerable<int> Neighbours(NestedGyroidLattice l, int i)
        {
            for (int e = l.AdjStart[i]; e < l.AdjStart[i + 1]; e++) yield return l.Adj[e];
        }

        [Test]
        public void StaysWithinBudget_AndGrowsBothSheetsAndFibers()
        {
            var l = Default;
            Assert.LessOrEqual(l.Count, new NestedGyroidSettings().PrismBudget);
            Assert.Greater(l.Stats.SheetPrisms, 0);
            Assert.Greater(l.Stats.FiberPrisms, 0, "no fibers: the stack would be separate shells");
            Assert.AreEqual(7, l.Stats.SheetsGrown);
        }

        [Test]
        public void IsOneConnectedComponent()
        {
            Assert.AreEqual(1, Default.Stats.Components);
        }

        [Test]
        public void WithoutFiberLinks_FallsApartIntoShells()
        {
            // The negative control for the claim the species exists to make.
            var l = Default;
            var parent = new int[l.Count];
            for (int i = 0; i < l.Count; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            for (int i = 0; i < l.Count; i++)
                foreach (int j in Neighbours(l, i))
                    if (l.Kind[i] == NestedGyroidPrismKind.Sheet && l.Kind[j] == NestedGyroidPrismKind.Sheet && l.Sheet[i] == l.Sheet[j])
                        parent[Find(i)] = Find(j);
            var roots = new HashSet<int>();
            for (int i = 0; i < l.Count; i++) if (l.Kind[i] == NestedGyroidPrismKind.Sheet) roots.Add(Find(i));
            Assert.GreaterOrEqual(roots.Count, l.Stats.SheetsGrown);
        }

        [Test]
        public void EveryBondIsInsideTheUrchinsReach()
        {
            var l = Default;
            Assert.Less(l.Stats.MaxEdgeOverReach, 1f);
            for (int i = 0; i < l.Count; i++)
                foreach (int j in Neighbours(l, i))
                    Assert.Less(NVec.Distance(l.Position[i], l.Position[j]), Mathf.Min(l.Reach[i], l.Reach[j]));
        }

        [Test]
        public void NoPrismInterpenetratesAnother()
        {
            var l = Default;
            int overlaps = 0;
            for (int i = 0; i < l.Count; i++)
            {
                var xi = NVec.Cross(l.Up[i], l.Forward[i]);
                float ri = 0.5f * l.Size[i].Length();
                for (int j = i + 1; j < l.Count; j++)
                {
                    float rj = 0.5f * l.Size[j].Length();
                    if (NVec.DistanceSquared(l.Position[i], l.Position[j]) > (ri + rj) * (ri + rj)) continue;
                    var xj = NVec.Cross(l.Up[j], l.Forward[j]);
                    if (NestedGyroidBuilder.ObbOverlap(l.Position[i], xi, l.Up[i], l.Forward[i], 0.5f * l.Size[i],
                                                       l.Position[j], xj, l.Up[j], l.Forward[j], 0.5f * l.Size[j]))
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

        [Test]
        public void GrowthOrder_HangsEveryPrismOffOneAlreadyStanding()
        {
            var l = Default;
            Assert.AreEqual(-1, l.Parent[0], "the first prism grows out of the heart");
            Assert.IsTrue(l.LimbBond[0]);
            for (int i = 1; i < l.Count; i++)
            {
                Assert.Less(l.Parent[i], i);
                Assert.GreaterOrEqual(l.Parent[i], 0);
                CollectionAssert.Contains(new List<int>(Neighbours(l, i)), l.Parent[i], "a parent must be a real bond");
            }
        }

        [Test]
        public void EveryPrismPointsUpTheStack()
        {
            // The layered ride reads +z as "up G": a strut on a negative-t gap must not point down it.
            var l = Default;
            float scale = new NestedGyroidSettings().CellSize / (2f * Mathf.PI);
            for (int i = 0; i < l.Count; i++)
                Assert.Greater(NVec.Dot(l.Forward[i], NestedGyroidBuilder.Grad(l.Position[i] / scale)), 0f);
        }

        [Test]
        public void TMax_IsClampedBelowTheCriticalValues()
        {
            var s = new NestedGyroidSettings { TMax = 2f }.Sanitized();
            Assert.Less(s.TMax, Mathf.Sqrt(2f));
        }

        [Test]
        public void ConfigDefaults_GrowTheMeasuredLattice()
        {
            // The SO's defaults and the settings' defaults are one lattice - the harness measures the latter.
            var so = ScriptableObject.CreateInstance<NestedGyroidConfigSO>();
            try { Assert.AreEqual(new NestedGyroidSettings().Sanitized().Key(), so.ToSettings().Key()); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var a = Default;
            var b = NestedGyroidBuilder.BuildNow(new NestedGyroidSettings());
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.Position[i], b.Position[i]);
                Assert.AreEqual(a.Parent[i], b.Parent[i]);
            }
        }
    }
}
