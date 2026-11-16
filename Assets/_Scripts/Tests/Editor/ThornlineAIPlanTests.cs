using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Thornline's AI plan (ThornlineAIPlan, THORNLINE.md section 4): every aim it returns anchors on the thorn it means
    /// and keeps that thorn off the autopilot's lance line, a broken thorn is never a target, the rows rise with the
    /// difficulty, the snipe and the crystal are the rows' own, and a stalled AI is pointed at a live thorn in range.
    /// The whole-race claims (every difficulty finishes every rung, in order) are run.sh --ai's.
    /// </summary>
    public class ThornlineAIPlanTests
    {
        const int Seeds = 100;

        sealed class Course
        {
            public readonly List<Vector3> Thorns = new();
            public readonly List<float> Sizes = new();
            public readonly List<bool> Alive = new();
            public readonly List<Vector3> Crystals = new();
            public readonly List<int> Legs = new();
            public readonly List<bool> Live = new();
            public List<RaceGate> Rings;
            public ThornlineSettings S;

            public Course(int intensity, int seed)
            {
                S = ThornlineCourse.ForIntensity(intensity);
                var chain = new List<Vector3>();
                Rings = ThornlineCourse.Generate(seed, S, chain);
                var laid = new List<Thorn>();
                ThornlineCourse.Lay(chain, S, laid);
                foreach (var t in laid) { Thorns.Add(t.Position); Sizes.Add(t.Size); Alive.Add(true); }
                ThornlineCourse.PlaceCrystals(chain, S, Crystals);
                foreach (var c in Crystals)
                {
                    int best = 1; float bd = float.MaxValue;
                    for (int j = 1; j < chain.Count; j++)
                    {
                        float d = ThornlineCourse.DistanceToSegment(c, chain[j - 1], chain[j]);
                        if (d < bd) { bd = d; best = j; }
                    }
                    Legs.Add(best); Live.Add(true);
                }
            }

            public ThornlineAIPlan.Inputs At(int threaded, Vector3 self, int level, int seat = 0, bool stalled = false) => new()
            {
                Thorns = Thorns, Sizes = Sizes, Alive = Alive, Rings = Rings, Crystals = Crystals, CrystalLegs = Legs,
                CrystalLive = Live, Self = self, Threaded = threaded, Seat = seat, Row = ThornlineAIPlan.RowFor(level),
                Stalled = stalled,
            };

            /// <summary>Where a pilot sits after docking on thorn k: on its face toward where it came from.</summary>
            public Vector3 DockedOn(int k)
            {
                Vector3 from = k == 0 ? ThornlineCourse.StartPoses(1)[0].position : Thorns[k - 1];
                return Thorns[k] - (Thorns[k] - from).normalized * (0.5f * Sizes[k] + ThornlineCourse.HullRadius);
            }
        }

        [Test]
        public void Rows_rise_with_difficulty()
        {
            for (int l = 2; l <= 4; l++)
                Assert.Greater(ThornlineAIPlan.RowFor(l).NoseLeadGain, ThornlineAIPlan.RowFor(l - 1).NoseLeadGain, $"level {l}");
            Assert.IsFalse(ThornlineAIPlan.RowFor(2).Snipes);
            Assert.IsTrue(ThornlineAIPlan.RowFor(3).Snipes);
            Assert.IsFalse(ThornlineAIPlan.RowFor(3).Crystals);
            Assert.IsTrue(ThornlineAIPlan.RowFor(4).Crystals);
            Assert.AreEqual(ThornlineAIPlan.RowFor(2).NoseLeadGain, ThornlineAIPlan.RowFor(0).NoseLeadGain, "an unknown level is Medium");
        }

        [Test]
        public void Every_aim_anchors_on_the_thorn_it_means_and_keeps_it_off_the_lance_line()
        {
            float count = ThornlineCourse.LanceRadiusMin + ThornlineCourse.LanceCountPad;
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = new Course(i, seed);
                    for (int k = 0; k < c.Rings.Count; k++)
                        for (int level = 1; level <= 4; level++)
                        {
                            Vector3 self = c.DockedOn(k);
                            var kind = ThornlineAIPlan.Choose(c.At(k, self, level, seat: level), out Vector3 aim);
                            int want = kind == ThornlineAim.Snipe ? k + 2 : kind == ThornlineAim.Back ? k : k + 1;
                            Assert.AreNotEqual(ThornlineAim.None, kind);
                            Assert.AreNotEqual(ThornlineAim.Hold, kind, $"I{i} seed {seed} ring {k}: nothing to aim at with every thorn standing");
                            Assert.GreaterOrEqual(ThornlineCourse.AnchorAlong(self, aim - self, c.Thorns[want], c.Sizes[want]), 0f,
                                $"I{i} seed {seed} ring {k} level {level}: the {kind} aim does not anchor on thorn {want}");
                            if (kind != ThornlineAim.Crystal)
                                Assert.Greater(ThornlineCourse.DistanceToSegment(c.Thorns[want], self, self + (aim - self).normalized * 2400f), count,
                                    $"I{i} seed {seed} ring {k} level {level}: the {kind} aim counts its own thorn toward the lance");
                            if (kind == ThornlineAim.Snipe) Assert.IsTrue(ThornlineAIPlan.RowFor(level).Snipes, "only a sniping row snipes");
                            if (kind == ThornlineAim.Crystal) Assert.IsTrue(ThornlineAIPlan.RowFor(level).Crystals, "only a crystal row aims through crystals");
                        }
                }
        }

        [Test]
        public void Two_seats_on_one_leg_fly_different_lines()
        {
            int same = 0, legs = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = new Course(1, seed);
                for (int k = 1; k < c.Rings.Count; k++)
                {
                    Vector3 self = c.DockedOn(k);
                    ThornlineAIPlan.Choose(c.At(k, self, 2, seat: 0), out Vector3 a);
                    ThornlineAIPlan.Choose(c.At(k, self, 2, seat: 1), out Vector3 b);
                    legs++;
                    if ((a - b).magnitude < 1f) same++;
                }
            }
            Assert.Less(same, legs / 10, $"{same} of {legs} legs put seats 0 and 1 on one line");
        }

        [Test]
        public void A_broken_thorn_is_never_a_target()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = new Course(4, seed);
                for (int k = 0; k + 2 < c.Rings.Count; k++)
                {
                    for (int j = 0; j < c.Alive.Count; j++) c.Alive[j] = true;
                    c.Alive[k + 1] = false;
                    Vector3 self = c.DockedOn(k);
                    var kind = ThornlineAIPlan.Choose(c.At(k, self, 4), out Vector3 aim);
                    if (kind == ThornlineAim.Hold) continue;   // nothing else to aim at: it waits on the spot, no press
                    Assert.Less(ThornlineCourse.AnchorAlong(self, aim - self, c.Thorns[k + 1], c.Sizes[k + 1]), 0f,
                        $"seed {seed} ring {k}: aims at the broken thorn {k + 1} ({kind})");
                }
            }
        }

        [Test]
        public void A_stalled_ai_is_pointed_at_a_live_thorn_in_sling_range()
        {
            int rescued = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = new Course(2, seed);
                for (int k = 1; k + 1 < c.Rings.Count; k++)
                {
                    // Lost between thorns: a little off the line of leg k+1.
                    Vector3 self = Vector3.Lerp(c.Thorns[k], c.Thorns[k + 1], 0.5f) + Vector3.up * 150f;
                    var kind = ThornlineAIPlan.Choose(c.At(k, self, 1, stalled: true), out Vector3 aim);
                    if (kind != ThornlineAim.Rescue) continue;
                    rescued++;
                    float d = (aim - self).magnitude;
                    Assert.GreaterOrEqual(d, ThornlineCourse.AiSlingMinDistance - ThornlineCourse.AnchorReach);
                    Assert.LessOrEqual(d, ThornlineCourse.SlingRange);
                }
            }
            Assert.Greater(rescued, 0, "the rescue never fired");
        }
    }
}
