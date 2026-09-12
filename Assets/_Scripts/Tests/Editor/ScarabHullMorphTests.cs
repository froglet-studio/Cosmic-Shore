using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab's procedural elemental morph lattice, proven on the exact shipped arithmetic
    /// (<see cref="ScarabHullForm.BakeMorphSet"/> / <see cref="ScarabHullForm.BlendPart"/> are
    /// pure, so these run in edit-mode NUnit AND in the offline harness). The load-bearing
    /// claims: the four extremes are the SAME MESH as the base (else the blend path corrupts
    /// silently), a corner weight reconstructs its extreme EXACTLY (the local-frame delta +
    /// blended pivot composition), the baked bounds interval contains every one of the 16 weight
    /// corners (what lets animated writes skip bounds recalculation forever), and the morph
    /// channels' defaults are a literal no-op on the base build.
    /// </summary>
    public class ScarabHullMorphTests
    {
        static ScarabHullForm.MorphSet Bake() =>
            ScarabHullForm.BakeMorphSet(ScarabHullForm.Settings.Default);

        [Test]
        public void MorphElementOrderMatchesTheFleetConfig()
        {
            // ScarabHullForm deliberately owns its own copy of the morph element order (the pure
            // core must not reference ScriptableObject/DOTween); this is the tie that keeps the
            // two from drifting.
            CollectionAssert.AreEqual(VesselElementalMorphConfigSO.MorphElements,
                                      ScarabHullForm.MorphElements,
                                      "morph element order diverged from the fleet's");
        }

        [Test]
        public void ExtremesShareBaseTopologyAndBakeSucceeds()
        {
            // BakeMorphSet throws on any part-roster / vertex-count / triangle-list divergence,
            // so a successful bake IS the topology assertion — then spot-check shape.
            var set = Bake();
            Assert.AreEqual(13, set.BaseParts.Count, "part roster");
            Assert.AreEqual(ScarabHullForm.MorphElements.Length, set.Deltas.Length, "one delta table per element");
            foreach (var perElement in set.Deltas)
                Assert.AreEqual(set.BaseParts.Count, perElement.Length, "deltas cover every part");
        }

        [Test]
        public void DefaultMorphChannelsAreANoOp()
        {
            // The channels ship at their documented rest values, and a zero-weight blend
            // reproduces the emitted base geometry exactly — together these pin "no element
            // levels" to the pre-morph hull, bit for bit.
            var s = ScarabHullForm.Settings.Default;
            Assert.AreEqual(0f, s.PronotumKeel, 0f);
            Assert.AreEqual(0f, s.ElytraSerration, 0f);
            Assert.AreEqual(0.10f, s.ShellTailPinch, 0f);
            Assert.AreEqual(0f, s.LegSocketAftShift, 0f);
            Assert.AreEqual(0f, s.LegSocketInboard, 0f);

            var set = Bake();
            var weights = new float[4];
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            for (int p = 0; p < set.BaseParts.Count; p++)
            {
                var pivot = ScarabHullForm.BlendPart(set, p, weights, verts, normals);
                Assert.AreEqual(0f, (pivot - set.BaseParts[p].Pivot).magnitude, 0f,
                                $"{set.BaseParts[p].Name} pivot at zero weights");
                for (int i = 0; i < verts.Count; i++)
                    Assert.AreEqual(0f, (verts[i] - set.BaseLocalVerts[p][i]).magnitude, 0f,
                                    $"{set.BaseParts[p].Name} vert {i} at zero weights");
            }
        }

        [Test]
        public void CornerWeightsReconstructExtremesExactly()
        {
            var s = ScarabHullForm.Settings.Default;
            var set = Bake();
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();

            for (int e = 0; e < ScarabHullForm.MorphElements.Length; e++)
            {
                var weights = new float[4];
                weights[e] = 1f;
                var extreme = ScarabHullForm.Generate(
                    ScarabHullForm.ApplyElementExtreme(s, ScarabHullForm.MorphElements[e]));

                for (int p = 0; p < set.BaseParts.Count; p++)
                {
                    var pivot = ScarabHullForm.BlendPart(set, p, weights, verts, normals);
                    Assert.AreEqual(0f, (pivot - extreme[p].Pivot).magnitude, 1e-5f,
                                    $"{ScarabHullForm.MorphElements[e]} pivot on {extreme[p].Name}");
                    for (int i = 0; i < verts.Count; i++)
                    {
                        var expected = extreme[p].Verts[i] - extreme[p].Pivot;
                        Assert.AreEqual(0f, (verts[i] - expected).magnitude, 1e-5f,
                                        $"{ScarabHullForm.MorphElements[e]} vert {i} on {extreme[p].Name}");
                    }
                }
            }
        }

        [Test]
        public void BoundsIntervalContainsAllSixteenWeightCorners()
        {
            var set = Bake();
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();

            for (int mask = 0; mask < 16; mask++)
            {
                var weights = new float[4];
                for (int e = 0; e < 4; e++) weights[e] = (mask >> e & 1) == 1 ? 1f : 0f;

                for (int p = 0; p < set.BaseParts.Count; p++)
                {
                    ScarabHullForm.BlendPart(set, p, weights, verts, normals);
                    var min = set.BoundsMin[p];
                    var max = set.BoundsMax[p];
                    foreach (var v in verts)
                    {
                        Assert.IsTrue(v.x >= min.x - 1e-4f && v.x <= max.x + 1e-4f
                                      && v.y >= min.y - 1e-4f && v.y <= max.y + 1e-4f
                                      && v.z >= min.z - 1e-4f && v.z <= max.z + 1e-4f,
                                      $"corner {mask} escapes bounds on {set.BaseParts[p].Name}");
                    }
                }
            }
        }

        [Test]
        public void BlendedGeometryIsFiniteAcrossTheLattice()
        {
            // Mid-lattice sweep (every pair at half weight plus the all-on centre): the profile
            // functions clamp before Pow, so nothing here may go NaN — the 2026-08-15 incident
            // class, now covering the morphed arithmetic too.
            var set = Bake();
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var combos = new List<float[]> { new[] { 0.5f, 0.5f, 0.5f, 0.5f }, new[] { 1f, 1f, 1f, 1f } };
            for (int a = 0; a < 4; a++)
                for (int b = a + 1; b < 4; b++)
                {
                    var w = new float[4];
                    w[a] = 1f; w[b] = 1f;
                    combos.Add(w);
                }

            foreach (var weights in combos)
                for (int p = 0; p < set.BaseParts.Count; p++)
                {
                    ScarabHullForm.BlendPart(set, p, weights, verts, normals);
                    for (int i = 0; i < verts.Count; i++)
                    {
                        Assert.IsTrue(Finite(verts[i]), $"NaN vert on {set.BaseParts[p].Name}");
                        Assert.IsTrue(Finite(normals[i]), $"NaN normal on {set.BaseParts[p].Name}");
                        Assert.AreEqual(1f, normals[i].magnitude, 1e-3f,
                                        $"blended normal not renormalized on {set.BaseParts[p].Name}");
                    }
                }
        }

        [Test]
        public void SpaceReachesOnlyTheHorn()
        {
            // Space's extreme touches HornLength/HornCurve alone, and the horn sits outside the
            // carapace fit, so nothing else may move — the isolation that makes the horn's growth
            // read as REACH rather than the whole ship changing. (Charge/Mass/Time legitimately
            // ripple hull-wide through the authored-extents fit.)
            var set = Bake();
            int spaceIndex = System.Array.IndexOf(ScarabHullForm.MorphElements, Element.Space);
            for (int p = 0; p < set.BaseParts.Count; p++)
            {
                bool isHorn = set.BaseParts[p].Name == "horn";
                Assert.AreEqual(isHorn, set.Deltas[spaceIndex][p].Any,
                                $"Space delta presence on {set.BaseParts[p].Name}");
            }
        }

        [Test]
        public void EveryElementVisiblyMovesTheHull()
        {
            // A morph that measures under a tenth of a unit at its own extreme is a dead display
            // — the fleet's shape keys move whole silhouettes. Floor: 0.3 world units of max
            // vertex travel per element (the hull is 9 long).
            var set = Bake();
            for (int e = 0; e < ScarabHullForm.MorphElements.Length; e++)
            {
                float maxTravel = 0f;
                for (int p = 0; p < set.BaseParts.Count; p++)
                {
                    var d = set.Deltas[e][p];
                    foreach (var v in d.VertDeltas)
                        maxTravel = Mathf.Max(maxTravel, v.magnitude);
                    maxTravel = Mathf.Max(maxTravel, d.PivotDelta.magnitude);
                }
                Assert.Greater(maxTravel, 0.3f,
                               $"{ScarabHullForm.MorphElements[e]} extreme barely moves the hull");
            }
        }

        [Test]
        public void TimeExtremeNarrowsTheStern()
        {
            // The direction test for the tail pinch — the first cut RAISED ShellTailPinch,
            // which walks the tail cross-section toward the sine arch's peak and WIDENED the
            // stern 20% while the doc said "tapered". Measure the rearmost ring of a wing case
            // in both builds: Time's must be narrower AND lower.
            var s = ScarabHullForm.Settings.Default;
            var baseParts = ScarabHullForm.Generate(s);
            var timeParts = ScarabHullForm.Generate(
                ScarabHullForm.ApplyElementExtreme(s, Element.Time));

            int idx = baseParts.FindIndex(p => p.Name == "elytron.r");
            Assert.GreaterOrEqual(idx, 0, "elytron.r missing");
            float baseW = 0f, timeW = 0f, baseH = 0f, timeH = 0f;
            int ring = s.WidthSegments + 1; // verts in the tail ring (i = 0)
            for (int j = 0; j < ring; j++)
            {
                baseW = Mathf.Max(baseW, Mathf.Abs(baseParts[idx].Verts[j].x));
                timeW = Mathf.Max(timeW, Mathf.Abs(timeParts[idx].Verts[j].x));
                baseH = Mathf.Max(baseH, baseParts[idx].Verts[j].y);
                timeH = Mathf.Max(timeH, timeParts[idx].Verts[j].y);
            }
            Assert.Less(timeW, baseW * 0.85f, "Time's stern is not meaningfully narrower");
            Assert.Less(timeH, baseH, "Time's stern is not lower");
        }

        [Test]
        public void SpaceExtremeCannotFlipTheHornGate()
        {
            // A hull authored horn-less sits at or under the 0.001 feature gate; the Space
            // extreme multiplies HornLength and could carry it ACROSS the gate, giving the
            // extreme a part the base lacks — which the bake's topology assert turns into a
            // throw inside Awake. The extreme is gate-invariant instead.
            var s = ScarabHullForm.Settings.Default;
            s.HornLength = 0.0008f;
            var set = ScarabHullForm.BakeMorphSet(s);   // must not throw
            Assert.IsFalse(set.BaseParts.Exists(p => p.Name == "horn"),
                           "gate should be closed at 0.0008");
        }

        static bool Finite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
}
