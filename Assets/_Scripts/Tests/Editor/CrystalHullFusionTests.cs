using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pure half of a crystal fusing onto a vessel hull (CRYSTAL_HULL_FUSION.md): how a crystal
    /// splits into rigid plates, where on a hull each plate lands, and the beat sequence.
    ///
    /// Each assertion is about something the animation depends on: a plate must reconstruct its
    /// own vertices exactly (or frame 0 is not the crystal), the contact plate must stay at the
    /// contact and the far plate must wrap to the antipode (or the crystal does not read as closing
    /// round the hull), and a plate must land on the outer skin, never an inner face.
    /// </summary>
    public class CrystalHullFusionTests
    {
        // ── Plates ────────────────────────────────────────────────────────────────────────────

        /// <summary>N unwelded unit-ish cubes on a ring of radius 2 - one vertex per triangle
        /// corner, like the charge crystal's edge-arc twin.</summary>
        static (Vector3[] verts, Vector3[] normals, int[] tris) Ring(int cubes)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int c = 0; c < cubes; c++)
            {
                float a = c * Mathf.PI * 2f / cubes;
                Vector3 centre = new(Mathf.Cos(a) * 2f, 0f, Mathf.Sin(a) * 2f);
                var corners = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    corners[i] = centre + new Vector3((i & 1) == 0 ? -0.2f : 0.2f, (i & 2) == 0 ? -0.2f : 0.2f, (i & 4) == 0 ? -0.2f : 0.2f);
                int[,] faces = { { 0, 1, 3, 2 }, { 4, 6, 7, 5 }, { 0, 4, 5, 1 }, { 2, 3, 7, 6 }, { 0, 2, 6, 4 }, { 1, 5, 7, 3 } };
                for (int f = 0; f < 6; f++)
                {
                    Vector3 n = Vector3.Cross(corners[faces[f, 1]] - corners[faces[f, 0]], corners[faces[f, 2]] - corners[faces[f, 0]]).normalized;
                    foreach (var k in new[] { 0, 1, 2, 0, 2, 3 })
                    {
                        tris.Add(verts.Count);
                        verts.Add(corners[faces[f, k]]);
                        normals.Add(n);
                    }
                }
            }
            return (verts.ToArray(), normals.ToArray(), tris.ToArray());
        }

        [Test]
        public void Plates_UnweldedSolids_AreGroupedByPosition()
        {
            var (verts, normals, tris) = Ring(6);
            var plates = CrystalHullFusionGeometry.BuildPlates(verts, normals, new[] { tris });

            Assert.IsNotNull(plates);
            Assert.AreEqual(6, plates.PlateCount, "every separate cube is one plate, though no vertex is shared");
            for (int v = 0; v < verts.Length; v++)
                Assert.AreEqual(v / 36, plates.VertexPlate[v], $"vertex {v} sits in cube {v / 36}");
        }

        [Test]
        public void Plates_FrameAndLocal_ReconstructEveryVertex()
        {
            var (verts, normals, tris) = Ring(5);
            var plates = CrystalHullFusionGeometry.BuildPlates(verts, normals, new[] { tris });

            for (int v = 0; v < verts.Length; v++)
            {
                int p = plates.VertexPlate[v];
                Vector3 rebuilt = plates.Centroids[p] + plates.Frames[p] * plates.LocalPositions[v];
                Assert.Less((rebuilt - verts[v]).magnitude, 1e-5f, "frame 0 must BE the crystal");
                Vector3 normal = plates.Frames[p] * plates.LocalNormals[v];
                Assert.Less((normal - normals[v]).magnitude, 1e-4f);
            }
        }

        [Test]
        public void Plates_RadialPointsFromCentreThroughCentroid()
        {
            var (verts, normals, tris) = Ring(8);
            var plates = CrystalHullFusionGeometry.BuildPlates(verts, normals, new[] { tris });

            for (int p = 0; p < plates.PlateCount; p++)
            {
                Vector3 expected = (plates.Centroids[p] - plates.Centre).normalized;
                Assert.Greater(Vector3.Dot(plates.Radials[p], expected), 0.9999f);
                Assert.Greater(Vector3.Dot(plates.Frames[p] * Vector3.forward, plates.Radials[p]), 0.9999f,
                    "a plate's frame faces out along its radial - that axis is what lands on the hull normal");
            }
            Assert.Greater(plates.Thickness, 0.39f);
            Assert.Less(plates.Thickness, 0.41f, "a 0.4 cube is 0.4 thick along its radial");
        }

        [Test]
        public void Plates_NoTriangles_ReturnsNull()
        {
            Assert.IsNull(CrystalHullFusionGeometry.BuildPlates(new[] { Vector3.zero }, null, new[] { new int[0] }));
        }

        // ── Hull spots ────────────────────────────────────────────────────────────────────────

        [Test]
        public void HullSpot_PicksOutermostOutwardVertex_NotAnInnerFace()
        {
            var verts = new[]
            {
                new Vector3(0f, 0f, 1f),   // inner face, facing in
                new Vector3(0f, 0f, 2f),   // outer skin
                new Vector3(0f, 0f, 1.5f), // between, facing out but nearer
                new Vector3(0f, 0f, -2f),
            };
            var normals = new[] { Vector3.back, Vector3.forward, Vector3.forward, Vector3.back };

            int spot = CrystalHullFusionGeometry.SelectHullSpot(verts, normals, Vector3.zero, Vector3.one * 2f,
                Vector3.forward, Mathf.Cos(10f * Mathf.Deg2Rad), 1);
            Assert.AreEqual(1, spot);
        }

        [Test]
        public void HullSpot_DirectionIsInNormalisedSpace()
        {
            // The same 45-degree direction lands on a different vertex once the hull is wide: in a
            // 4:1 hull the diagonal of the BOX is (4,1), not (1,1). That is what spreads plates by
            // area over a flat hull instead of piling them on its tips.
            var verts = new[] { new Vector3(1f, 1f, 0f), new Vector3(4f, 1f, 0f) };
            var normals = new[] { new Vector3(1f, 1f, 0f).normalized, new Vector3(1f, 1f, 0f).normalized };
            Vector3 diagonal = new Vector3(1f, 1f, 0f).normalized;
            float cone = Mathf.Cos(5f * Mathf.Deg2Rad);

            Assert.AreEqual(0, CrystalHullFusionGeometry.SelectHullSpot(verts, normals, Vector3.zero,
                Vector3.one, diagonal, cone, 1));
            Assert.AreEqual(1, CrystalHullFusionGeometry.SelectHullSpot(verts, normals, Vector3.zero,
                new Vector3(4f, 1f, 1f), diagonal, cone, 1));
        }

        [Test]
        public void HullSpot_EmptyHull_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, CrystalHullFusionGeometry.SelectHullSpot(new Vector3[0], null, Vector3.zero,
                Vector3.one, Vector3.up, 0.9f, 1));
        }

        // ── Wrap ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Wrap_ContactPlateStays_FarPlateGoesToAntipode_EquatorHolds()
        {
            Vector3 pole = Vector3.up; // the hull side the crystal came from

            Assert.Less((CrystalHullFusionGeometry.WrapDirection(-pole, pole) - pole).magnitude, 1e-6f,
                "the plate that touched the hull lands at the contact");
            Assert.Less((CrystalHullFusionGeometry.WrapDirection(pole, pole) + pole).magnitude, 1e-6f,
                "the plate on the crystal's far side wraps to the hull's far side");
            Assert.Less((CrystalHullFusionGeometry.WrapDirection(Vector3.right, pole) - Vector3.right).magnitude, 1e-6f);

            var r = new Vector3(0.3f, -0.8f, 0.52f).normalized;
            float before = Vector3.Angle(r, -pole), after = Vector3.Angle(CrystalHullFusionGeometry.WrapDirection(r, pole), pole);
            Assert.AreEqual(before, after, 1e-3f, "every plate keeps its angular distance from the contact");
        }

        [Test]
        public void SlerpDirection_EndsExact_AndAntipodeTurnsAboutTheHint()
        {
            Vector3 a = Vector3.up, b = Vector3.right;
            Assert.Less((CrystalHullFusionGeometry.SlerpDirection(a, b, 1f, Vector3.forward) - b).magnitude, 1e-5f);
            Assert.Less((CrystalHullFusionGeometry.SlerpDirection(a, b, 0f, Vector3.forward) - a).magnitude, 1e-5f);

            Vector3 mid = CrystalHullFusionGeometry.SlerpDirection(Vector3.up, Vector3.down, 0.5f, Vector3.forward);
            Assert.AreEqual(1f, mid.magnitude, 1e-4f);
            Assert.Less(Mathf.Abs(mid.y), 1e-4f, "half way round an antipodal pair is the equator");
            Assert.Less(Mathf.Abs(mid.z), 1e-4f, "...in the plane the hint chose (axis up x forward)");
        }

        [Test]
        public void Spots_AreSpreadEvenly_AndStartAtTheContact()
        {
            // A 7x7 grid on a plane facing +z: 4 spots must take the four corners after the seed.
            var verts = new List<Vector3>();
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    verts.Add(new Vector3(x, y, 1f));
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.forward;

            int centre = 3 * 7 + 3;
            var spots = CrystalHullFusionGeometry.FarthestPointSpots(verts.ToArray(), normals, Vector3.zero,
                centre, 5, 1, out float spacing);

            Assert.AreEqual(centre, spots[0], "the contact is always the first spot");
            var corners = new HashSet<int> { 0, 6, 42, 48 };
            for (int k = 1; k < 5; k++) Assert.IsTrue(corners.Contains(spots[k]), $"spot {k} = {spots[k]} is a corner");
            Assert.AreEqual(Mathf.Sqrt(18f), spacing, 1e-4f,
                "the last corner's nearest neighbour is the centre seed, half a diagonal away");
        }

        [Test]
        public void Spots_SkipInwardFacingVertices()
        {
            var verts = new[] { new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, -1f) };
            var normals = new[] { Vector3.forward, Vector3.forward, Vector3.back }; // [1] faces IN
            var spots = CrystalHullFusionGeometry.FarthestPointSpots(verts, normals, Vector3.zero, 0, 2, 1, out _);
            Assert.AreEqual(2, spots[1], "the far vertex is further, but it is an inner face");
        }

        [Test]
        public void Assignment_IsTheOptimum_NotTheGreedyMatch()
        {
            // Greedy takes (0,0)=0 first and is then forced into (1,1)=100: total 100.
            // The optimum crosses: (0,1)+(1,0) = 1 + 1.
            var cost = new float[,] { { 0f, 1f }, { 1f, 100f } };
            var assignment = CrystalHullFusionGeometry.AssignMinCost(cost);
            Assert.AreEqual(1, assignment[0]);
            Assert.AreEqual(0, assignment[1]);
        }

        [Test]
        public void Assignment_IsAPermutation()
        {
            var rng = new System.Random(7);
            const int n = 60;
            var cost = new float[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    cost[i, j] = (float)rng.NextDouble();
            var assignment = CrystalHullFusionGeometry.AssignMinCost(cost);
            var seen = new HashSet<int>(assignment);
            Assert.AreEqual(n, seen.Count, "every plate gets its own spot");
        }

        // ── Beats ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Entry_ResolvesBeatsInOrder()
        {
            var e = new CrystalHullFusionConfigSO.Entry
            {
                approachSeconds = 0.2f, wrapSeconds = 0.3f, holdSeconds = 0.2f, sinkSeconds = 0.3f,
            };
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Approach, e.Resolve(0.1f, out float u));
            Assert.AreEqual(0.5f, u, 1e-5f);
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Wrap, e.Resolve(0.35f, out u));
            Assert.AreEqual(0.5f, u, 1e-5f);
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Hold, e.Resolve(0.6f, out _));
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Sink, e.Resolve(0.85f, out _));
            Assert.AreEqual(CrystalHullFusionConfigSO.Phase.Done, e.Resolve(1.01f, out _));
            Assert.AreEqual(0.5f, e.ClampSeconds, 1e-6f, "the pickup sound plays as the plates clamp");
        }

        [Test]
        public void Entry_EveryPlateFinishesItsWrap_ByTheEndOfTheBeat()
        {
            var e = new CrystalHullFusionConfigSO.Entry { wrapStagger = 0.6f };
            for (float delay = 0f; delay <= 1f; delay += 0.125f)
            {
                Assert.AreEqual(1f, e.PlateWrapProgress(1f, delay), 1e-5f, $"delay {delay} must have landed by the clamp");
                Assert.AreEqual(0f, e.PlateWrapProgress(delay * 0.6f, delay), 1e-5f, "and not move before its turn");
            }
        }

        [Test]
        public void ShippedConfig_FusesChargeOntoTheSquirrel()
        {
            var config = Resources.Load<CrystalHullFusionConfigSO>(CrystalHullFusionConfigSO.ResourcePath);
            Assert.IsNotNull(config, "Resources/CrystalHullFusionConfig is the opt-in - without it nothing fuses");
            Assert.IsTrue(config.TryGet(VesselClassType.Squirrel, Element.Charge, out var entry));
            Assert.Greater(entry.TotalSeconds, 0f);
            Assert.IsFalse(config.TryGet(VesselClassType.Squirrel, Element.Mass, out _),
                "only the pair under test fuses; every other pickup keeps the generic capture");
        }
    }
}
