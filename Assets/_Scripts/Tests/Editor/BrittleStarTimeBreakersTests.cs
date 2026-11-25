using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Brittle Star Time's breakers (BRITTLESTARTIME.md section 0b): only I4 has them; every ring has a disc across its
    /// way in, so a line that threads the mouth from beyond the disc breaks through it; no slab stands on another leg's
    /// line or by a let-go post; the ruins keep off the slabs; and the lay is a pure function of the lap. The slabs are
    /// Headlong's (<see cref="HeadlongHedges.Build"/>). The same file runs headless through
    /// <c>Tools/Build/brittle_star_time_course_harness</c>.
    /// </summary>
    public class BrittleStarTimeBreakersTests
    {
        const float Inner = 480f, Outer = 1080f;
        const int Seeds = 60;

        static List<RaceGate> Course(int intensity, int seed)
        {
            if (BrittleStarTimeCourse.BuildLap(intensity, seed, Inner, Outer, BrittleStarTimeCourse.GatesPerLap, out var lap))
                return lap;
            var s = BrittleStarTimeCourse.Gearbox();
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        static float Top(int intensity) =>
            intensity >= 4 ? BrittleStarTimeCourse.SpeedModeSpeedAtTime5 : BrittleStarTimeCourse.SpeedModeSpeed;

        static float Turn(int intensity) => BrittleStarTimeCourse.CornerRadiusAt(Top(intensity));

        sealed class Laid
        {
            public List<RaceGate> Course;
            public List<BrittleStarTimeLine.Post> Posts;
            public readonly List<HeadlongHedges.Hedge> Hedges = new();
            public readonly List<HeadlongHedges.Part> Slabs = new();
            public readonly List<int> Owner = new();
        }

        static Laid Breakers(int intensity, int seed)
        {
            var laid = new Laid { Course = Course(intensity, seed) };
            laid.Posts = BrittleStarTimeLine.Marks(laid.Course, Top(intensity));
            BrittleStarTimeBreakers.Place(laid.Course, intensity, Turn(intensity), laid.Hedges);
            BrittleStarTimeBreakers.Build(laid.Course, laid.Hedges, laid.Posts, laid.Slabs, laid.Owner);
            return laid;
        }

        [Test]
        public void Only_I4_carries_breakers()
        {
            for (int intensity = 1; intensity <= 3; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                    Assert.AreEqual(0, Breakers(intensity, seed).Hedges.Count, $"I{intensity} seed {seed}");
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Breakers(4, seed);
                Assert.GreaterOrEqual(laid.Hedges.Count, laid.Course.Count, $"seed {seed}: a disc for every ring");
                Assert.Greater(laid.Slabs.Count, 0, $"seed {seed}");
            }
        }

        /// <summary>The disc stands across the ring's way in: the straight that comes down that way from beyond the disc
        /// to the ring's centre crosses the disc, and through standing slabs (the hole a cut would leave is never the
        /// centre).</summary>
        [Test]
        public void Every_ring_is_reached_through_its_disc()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Breakers(4, seed);
                int n = laid.Course.Count;
                for (int k = 0; k < n; k++)
                {
                    Vector3 ring = laid.Course[k].Position;
                    Vector3 way = BrittleStarTimeBreakers.WayIn(laid.Course, k, Turn(4), out _, out _);
                    int disc = laid.Hedges.FindIndex(h => (h.Centre - (ring - way * BrittleStarTimeBreakers.RingLead)).sqrMagnitude < 1e-2f);
                    Assert.GreaterOrEqual(disc, 0, $"seed {seed} ring {k}: no disc before it");
                    Vector3 from = ring - way * (BrittleStarTimeBreakers.RingLead + 60f);
                    Assert.IsTrue(HeadlongHedges.Crosses(laid.Hedges[disc], from, ring, out _, out var at),
                                  $"seed {seed} ring {k}: the way in misses its disc");
                    bool slabThere = false;
                    for (int i = 0; i < laid.Slabs.Count && !slabThere; i++)
                        slabThere = laid.Owner[i] == disc && (laid.Slabs[i].Position - at).magnitude <= HeadlongHedges.Pitch;
                    Assert.IsTrue(slabThere, $"seed {seed} ring {k}: the disc has a hole where the line crosses it");
                }
            }
        }

        [Test]
        public void No_slab_stands_on_another_leg_or_by_a_let_go_post()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Breakers(4, seed);
                for (int i = 0; i < laid.Slabs.Count; i++)
                {
                    Vector3 p = laid.Slabs[i].Position;
                    Assert.GreaterOrEqual(BrittleStarTimeBreakers.OtherLegDistance(laid.Course, laid.Hedges[laid.Owner[i]].Leg, p),
                                          BrittleStarTimeBreakers.LegClearance, $"seed {seed} slab {i}");
                    foreach (var post in laid.Posts)
                        Assert.GreaterOrEqual((post.Position - p).magnitude, BrittleStarTimeBreakers.PostClearance,
                                              $"seed {seed} slab {i}");
                }
            }
        }

        [Test]
        public void The_ruins_keep_off_the_breakers()
        {
            int ruins = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Breakers(4, seed);
                var parts = new List<SpearlineRuinPart>();
                var wheels = new List<SpearlineRuinWheel>();
                BrittleStarTimeRuins.Lay(laid.Course, 4, Turn(4), laid.Posts, parts, wheels, laid.Slabs);
                ruins += parts.Count;
                foreach (var part in parts)
                    Assert.IsFalse(BrittleStarTimeBreakers.Near(laid.Slabs, part.Position, BrittleStarTimeRuins.BreakerClearance),
                                   $"seed {seed}: a ruin prism stands in a breaker");
            }
            Assert.Greater(ruins, 0, "the breakers left no ruin standing on any course");
        }

        [Test]
        public void The_lay_is_a_function_of_the_lap()
        {
            var a = Breakers(4, 7);
            var b = Breakers(4, 7);
            Assert.AreEqual(a.Slabs.Count, b.Slabs.Count);
            for (int i = 0; i < a.Slabs.Count; i++) Assert.AreEqual(a.Slabs[i].Position, b.Slabs[i].Position);
        }
    }
}
