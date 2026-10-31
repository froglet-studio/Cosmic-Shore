using System.Collections.Generic;
using CosmicShore.Data;
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
            Assert.AreEqual(2, SerpentTimeCluster.RingsFor(1, SerpentTimeCluster.DefaultRingsByIntensity));
            Assert.AreEqual(1, SerpentTimeCluster.RingsFor(4, SerpentTimeCluster.DefaultRingsByIntensity));
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
        public void Guard_domain_is_always_a_playable_domain_and_spread_over_the_course()
        {
            var seen = new HashSet<Domains>();
            for (int gate = 0; gate < 16; gate++)
            for (int slot = 0; slot < 2; slot++)
            {
                var d = SerpentTimeCluster.GuardDomain(gate, slot);
                Assert.IsTrue(d == Domains.Jade || d == Domains.Ruby || d == Domains.Gold, $"gate {gate} ring {slot}: {d} - a guard is never Blue (bait to every school)");
                Assert.AreEqual(d, SerpentTimeCluster.GuardDomain(gate, slot), "the same on every peer");
                seen.Add(d);
            }
            Assert.AreEqual(3, seen.Count, "all three playable domains guard some ring");
        }

        [Test]
        public void Guards_mix_their_placements_and_keep_the_start_clear()
        {
            Assert.AreEqual(SerpentTimeCluster.GuardPlacement.Beside, SerpentTimeCluster.PlacementFor(0),
                "gate 0's guard stands beside its ring: the start line is behind gate 0");
            var seen = new HashSet<SerpentTimeCluster.GuardPlacement>();
            for (int gate = 0; gate < 8; gate++) seen.Add(SerpentTimeCluster.PlacementFor(gate));
            Assert.AreEqual(3, seen.Count, "a lap mixes approach, beside and door guards");

            var ring = new RaceGate(new Vector3(0f, 800f, 0f), Vector3.forward, 60f);
            var hearts = new Vector3[2];
            var range = new Vector2(150f, 300f);
            for (int gate = 0; gate < 16; gate++)
            {
                Assert.AreEqual(1, SerpentTimeCluster.GuardHearts(ring, gate, 0, SerpentTimeCluster.GuardPlacement.Approach,
                                                                   range, 0.8f, 70f, hearts));
                Vector3 off = hearts[0] - ring.Position;
                Assert.That(-off.z, Is.InRange(150f - 1e-3f, 300f + 1e-3f), "an approach guard is 150-300 u before the ring");
                Assert.AreEqual(0f, new Vector2(off.x, off.y).magnitude, 1e-3f, "...on the line through it");

                SerpentTimeCluster.GuardHearts(ring, gate, 0, SerpentTimeCluster.GuardPlacement.Beside, range, 0.8f, 70f, hearts);
                off = hearts[0] - ring.Position;
                Assert.AreEqual(0f, off.z, 1e-3f, "a beside guard is in the ring's plane");
                Assert.AreEqual(48f, off.magnitude, 1e-2f, "...0.8 of the mouth off its centre");

                Assert.AreEqual(2, SerpentTimeCluster.GuardHearts(ring, gate, 0, SerpentTimeCluster.GuardPlacement.Door,
                                                                   range, 0.8f, 70f, hearts));
                Vector3 mid = (hearts[0] + hearts[1]) * 0.5f - ring.Position;
                Assert.AreEqual(0f, new Vector2(mid.x, mid.y).magnitude, 1e-3f, "a door is centred on the line");
                Assert.AreEqual(140f, Vector3.Distance(hearts[0], hearts[1]), 1e-2f, "...its two hearts 2 x 70 u apart");
                Assert.AreEqual(0f, (hearts[0] - hearts[1]).z, 1e-3f, "...across the line, not along it");
            }
        }

        [Test]
        public void A_round_fells_a_guard_only_through_its_heart()
        {
            var origin = Vector3.zero;
            var nose = Vector3.forward;
            // Straight through the heart, and just inside the hit radius of it.
            Assert.IsTrue(SerpentTimeCluster.RoundPassesHeart(origin, nose, 3000f, 0.5f, new Vector3(0f, 0f, 400f), 9f));
            Assert.IsTrue(SerpentTimeCluster.RoundPassesHeart(origin, nose, 3000f, 0.5f, new Vector3(8.5f, 0f, 200f), 9f));
            // Through the plant's plates but wide of the heart: no.
            Assert.IsFalse(SerpentTimeCluster.RoundPassesHeart(origin, nose, 3000f, 0.5f, new Vector3(30f, 0f, 200f), 9f));
            // Behind the muzzle, or past the round's range: no.
            Assert.IsFalse(SerpentTimeCluster.RoundPassesHeart(origin, nose, 3000f, 0.5f, new Vector3(0f, 0f, -50f), 9f));
            Assert.IsFalse(SerpentTimeCluster.RoundPassesHeart(origin, nose, 3000f, 0.5f, new Vector3(0f, 0f, 3200f), 9f));
            // Far out the cone is the wider: 2000 u at 0.5 deg is ~17.5 u, so 15 u off the line still counts.
            Assert.IsTrue(SerpentTimeCluster.RoundPassesHeart(origin, nose, 3000f, 0.5f, new Vector3(15f, 0f, 2000f), 9f));
        }
    }
}
