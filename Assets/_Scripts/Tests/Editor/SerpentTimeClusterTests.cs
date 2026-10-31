using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Serpent Time's ring clusters (<see cref="SerpentTimeCluster"/>): every ring of a gate lies in the gate's plane,
    /// faces the gate's way, stays clear of its neighbours, and stays clear of every OTHER gate's rings on the real
    /// circuits (swept over seeds and all four intensities), so threading one ring can never thread another gate's.
    /// </summary>
    public class SerpentTimeClusterTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 120;

        [Test]
        public void Default_layouts_never_overlap_and_stay_near_the_gate()
        {
            for (int k = 2; k <= 3; k++)
            {
                SerpentTimeCluster.Layout(k, out float scale, out float spread);
                Assert.IsTrue(SerpentTimeCluster.IsSeparated(k, scale, spread), $"{k} rings overlap");
                Assert.IsTrue(SerpentTimeCluster.FitsWithin(scale, spread, 1.3f), $"{k} rings spread past 1.3 mouths");
            }

            // Negative control: rings too big for their spacing are caught.
            Assert.IsFalse(SerpentTimeCluster.IsSeparated(3, 0.7f, 0.65f));
        }

        [Test]
        public void Rings_lie_in_the_gate_plane_and_face_its_way()
        {
            var gate = new RaceGate(new Vector3(100f, -40f, 300f), new Vector3(0.3f, 0.2f, 1f).normalized, 68f);
            for (int k = 2; k <= 3; k++)
            for (int slot = 0; slot < k; slot++)
            {
                var ring = SerpentTimeCluster.Ring(gate, 5, slot, k);
                Assert.AreEqual(0f, Vector3.Dot(ring.Position - gate.Position, gate.Axis), 1e-3f, "off the gate's plane");
                Assert.AreEqual(1f, Vector3.Dot(ring.Axis, gate.Axis), 1e-5f, "turned from the gate's flow");
                SerpentTimeCluster.Layout(k, out float scale, out float spread);
                Assert.AreEqual(spread * gate.Radius, Vector3.Distance(ring.Position, gate.Position), 1e-2f);
                Assert.AreEqual(scale * gate.Radius, ring.Radius, 1e-3f);
            }

            // A gate that faces straight up still gets a basis (the cross with world up degenerates).
            var up = new RaceGate(Vector3.zero, Vector3.up, 40f);
            var r = SerpentTimeCluster.Ring(up, 0, 1, 3);
            Assert.IsFalse(float.IsNaN(r.Position.x));
            Assert.AreEqual(0f, r.Position.y, 1e-3f);
        }

        [Test]
        public void One_ring_means_the_plain_gate()
        {
            var gate = new RaceGate(Vector3.one, Vector3.forward, 50f);
            var ring = SerpentTimeCluster.Ring(gate, 3, 0, 1);
            Assert.AreEqual(gate.Position, ring.Position);
            Assert.AreEqual(gate.Radius, ring.Radius);
            Assert.AreEqual(3, SerpentTimeCluster.RingsFor(1, SerpentTimeCluster.DefaultRingsByIntensity));
            Assert.AreEqual(2, SerpentTimeCluster.RingsFor(4, SerpentTimeCluster.DefaultRingsByIntensity));
            Assert.AreEqual(1, SerpentTimeCluster.RingsFor(2, null));
        }

        [Test]
        public void No_ring_reaches_another_gates_ring_on_the_real_circuits()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                int k = SerpentTimeCluster.RingsFor(intensity, SerpentTimeCluster.DefaultRingsByIntensity);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var s = SerpentTimeCourse.ForIntensity(intensity);
                    s.InnerRadius = Inner;
                    s.OuterRadius = Outer;
                    s.FirstGateDirection = Vector3.up;
                    var course = HeadlongCircuit.Generate(seed, s);
                    if (course == null || course.Count == 0) continue;

                    for (int a = 0; a < course.Count; a++)
                    for (int b = a + 1; b < course.Count; b++)
                    for (int i = 0; i < k; i++)
                    for (int j = 0; j < k; j++)
                    {
                        var ra = SerpentTimeCluster.Ring(course[a], a, i, k);
                        var rb = SerpentTimeCluster.Ring(course[b], b, j, k);
                        Assert.Greater(Vector3.Distance(ra.Position, rb.Position), ra.Radius + rb.Radius,
                            $"I{intensity} seed {seed}: gate {a + 1} ring {i} touches gate {b + 1} ring {j}");
                    }
                }
            }
        }

        [Test]
        public void Cover_leaves_the_right_number_of_rings_open_and_moves_the_open_one()
        {
            for (int rings = 1; rings <= 3; rings++)
            for (int covered = 0; covered <= rings; covered++)
            for (int gate = 0; gate < 16; gate++)
            {
                int n = 0;
                for (int slot = 0; slot < rings; slot++) if (SerpentTimeCluster.IsCovered(gate, slot, rings, covered)) n++;
                Assert.AreEqual(covered, n, $"gate {gate}: {covered} of {rings} should be covered");
            }

            // The open ring of a 3-ring, 2-covered gate is not the same slot on every gate.
            var open = new HashSet<int>();
            for (int gate = 0; gate < 16; gate++)
                for (int slot = 0; slot < 3; slot++)
                    if (!SerpentTimeCluster.IsCovered(gate, slot, 3, 2)) open.Add(slot);
            Assert.Greater(open.Count, 1, "the open ring never moves");
        }

        [Test]
        public void Danger_slabs_take_their_share_and_none_or_all_at_the_ends()
        {
            int danger = 0, total = 0;
            for (int gate = 0; gate < 16; gate++)
            for (int slot = 0; slot < 3; slot++)
            for (int cell = 0; cell < 100; cell++)
            {
                Assert.IsFalse(SerpentTimeCluster.IsDangerSlab(gate, slot, cell, 0f));
                Assert.IsTrue(SerpentTimeCluster.IsDangerSlab(gate, slot, cell, 1f));
                if (SerpentTimeCluster.IsDangerSlab(gate, slot, cell, 0.15f)) danger++;
                total++;
            }
            Assert.AreEqual(0.15f, danger / (float)total, 0.03f, "the I1 share is mostly plain prisms");
            // The same slab answers the same way every time (every peer, every regrowth).
            Assert.AreEqual(SerpentTimeCluster.IsDangerSlab(4, 1, 37, 0.5f), SerpentTimeCluster.IsDangerSlab(4, 1, 37, 0.5f));
        }

        [Test]
        public void Cover_cells_lie_in_the_mouth_inside_the_rim()
        {
            var ring = new RaceGate(new Vector3(10f, 20f, 30f), new Vector3(0.4f, -0.3f, 1f).normalized, 45f);
            var cells = new List<Vector3>();
            SerpentTimeCluster.CoverCells(ring, 7f, 2f, cells);
            Assert.Greater(cells.Count, 60, "a 45 u mouth should take dozens of 7 u slabs");
            foreach (var c in cells)
            {
                Vector3 d = c - ring.Position;
                Assert.AreEqual(0f, Vector3.Dot(d, ring.Axis), 1e-3f, "off the ring's plane");
                Assert.LessOrEqual(d.magnitude, ring.Radius - 2f, "past the trimmed rim");
            }

            // The covered area reaches most of the mouth: no ring is left with an open lane round its edge.
            float coveredArea = cells.Count * 49f;
            Assert.Greater(coveredArea, 0.7f * Mathf.PI * ring.Radius * ring.Radius);

            // A mouth smaller than one slab still gets one, at its centre.
            SerpentTimeCluster.CoverCells(new RaceGate(Vector3.zero, Vector3.forward, 3f), 7f, 2f, cells);
            Assert.AreEqual(1, cells.Count);
        }
    }
}
