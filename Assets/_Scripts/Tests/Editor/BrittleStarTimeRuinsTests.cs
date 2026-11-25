using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Brittle Star Time's garden ruins (BRITTLESTARTIME.md section 0a): only I4 has them; the lane down the flown line,
    /// the space round every ring and the let-go gates stay clear of every standing ruin; the corkscrew's one long leg
    /// (the return straight) carries the ruin and the coil carries none; and the lay is a pure function of the lap.
    /// The ruins are Spearline's (<see cref="SpearlineRuins"/>, held by <c>SpearlineRuinsTests</c>); these tests hold
    /// what this mode adds: the lap it hands over and the posts it keeps off. The same file runs headless through
    /// <c>Tools/Build/brittle_star_time_course_harness</c>.
    /// </summary>
    public class BrittleStarTimeRuinsTests
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

        static float TurnRadius(int intensity) => BrittleStarTimeCourse.CornerRadiusAt(Top(intensity));

        sealed class Laid
        {
            public List<RaceGate> Course, Lap;
            public List<BrittleStarTimeLine.Post> Posts;
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }
        }

        static Laid Ruins(int intensity, int seed)
        {
            var laid = new Laid { Course = Course(intensity, seed) };
            laid.Posts = BrittleStarTimeLine.Marks(laid.Course, Top(intensity));
            laid.Lap = BrittleStarTimeRuins.Lay(laid.Course, intensity, TurnRadius(intensity), laid.Posts, laid.Parts, laid.Wheels);
            return laid;
        }

        static bool IsCorkscrew(int seed) =>
            BrittleStarTimeCourse.BuildLap(4, seed, Inner, Outer, BrittleStarTimeCourse.GatesPerLap, out _);

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed).Parts.Count, $"I{i} seed {seed}");
                Assert.Greater(Ruins(4, seed).Parts.Count, 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void The_lane_down_the_flown_line_is_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Turns(i)) continue;
                    var part = laid.Parts[i];
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(laid.Lap, part), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism reaches into the lane");
                    // Both ends of the prism, checked without the generator's own sampler.
                    Vector3 half = part.Forward * (0.5f * part.Scale.z);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(laid.Lap, part.Position + half), SpearlineRuins.LaneRadius - 0.01f);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(laid.Lap, part.Position - half), SpearlineRuins.LaneRadius - 0.01f);
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_mouth()
        {
            // A pilot who threads a ring anywhere in its mouth is not asked for a narrower pass between rings.
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, BrittleStarTimeCourse.MouthRadius(4));
        }

        [Test]
        public void No_ruin_stands_near_a_ring_of_the_course()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                foreach (var part in laid.Parts)
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(laid.Course, part.Position), SpearlineRuins.RingMargin - 0.01f,
                        $"seed {seed}: a ruin prism stands at a ring");
            }
        }

        [Test]
        public void No_ruin_stands_in_a_let_go_gate()
        {
            int withPosts = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                if (laid.Posts.Count > 0) withPosts++;
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Turns(i)) continue;
                    Assert.GreaterOrEqual(BrittleStarTimeRuins.PostDistance(laid.Posts, laid.Parts[i]),
                        BrittleStarTimeRuins.PostClearance - 0.01f, $"seed {seed}: a ruin prism stands at a let-go post");
                }
                foreach (var wheel in laid.Wheels)
                    foreach (var post in laid.Posts)
                        Assert.GreaterOrEqual((post.Position - wheel.Centre).magnitude,
                            SpearlineRuins.WheelRadius + BrittleStarTimeRuins.PostClearance - 0.01f, $"seed {seed}: a wheel sweeps a let-go post");
            }
            Assert.Greater(withPosts, 0, "no I4 course stood a let-go gate: this test held nothing");
        }

        [Test]
        public void A_post_standing_in_a_ruin_clears_the_prisms_round_it()
        {
            // On the shipped courses the let-go gates and the ruin happen to stand apart, so the test above cannot show
            // the clearing works: stand a post in the middle of a ruin and see it cleared.
            for (int seed = 1; seed <= 10; seed++)
            {
                var course = Course(4, seed);
                var open = new List<SpearlineRuinPart>();
                BrittleStarTimeRuins.Lay(course, 4, TurnRadius(4), null, open, new List<SpearlineRuinWheel>());
                var at = open[open.Count / 2];
                var posts = new List<BrittleStarTimeLine.Post> { new(at.Position, at.Forward, at.Up, 0) };
                var kept = new List<SpearlineRuinPart>();
                BrittleStarTimeRuins.Lay(course, 4, TurnRadius(4), posts, kept, new List<SpearlineRuinWheel>());
                Assert.Less(kept.Count, open.Count, $"seed {seed}: nothing was cleared");
                Assert.Greater(kept.Count, 0, $"seed {seed}: one post cleared the whole ruin");
                foreach (var part in kept)
                    Assert.GreaterOrEqual(BrittleStarTimeRuins.PostDistance(posts, part), BrittleStarTimeRuins.PostClearance - 0.01f);
            }
        }

        [Test]
        public void The_corkscrew_carries_its_ruin_on_the_return_straight_and_none_on_the_coil()
        {
            int corkscrews = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                if (!IsCorkscrew(seed)) continue;
                corkscrews++;
                var laid = Ruins(4, seed);
                // The flown lap: the eight rings and the point the turn onto the return straight ends.
                Assert.AreEqual(BrittleStarTimeCourse.GatesPerLap + 1, laid.Lap.Count, $"seed {seed}");
                int leg = laid.Parts[0].Leg;
                foreach (var part in laid.Parts) Assert.AreEqual(leg, part.Leg, $"seed {seed}: more than one ruin");
                var from = laid.Lap[leg];
                var to = laid.Lap[leg + 1];
                Assert.AreEqual(0f, from.Radius, "the ruin's leg does not start at the turn's end");
                Assert.AreEqual(laid.Course[BrittleStarTimeCourse.GatesPerLap - 1].Position, to.Position,
                    "the ruin's leg does not end at the near return ring");
                Assert.GreaterOrEqual((to.Position - from.Position).magnitude, SpearlineRuins.MinLegLength);
                // Straight: the leg's line is its chord.
                Vector3 chord = (to.Position - from.Position).normalized;
                Assert.AreEqual(1f, Vector3.Dot(from.Axis, chord), 1e-3f);
                Assert.AreEqual(1f, Vector3.Dot(to.Axis, chord), 1e-3f);
                // Enough to read as a ruin, and inside the prism budget of one Spearline ruin.
                Assert.GreaterOrEqual(laid.Parts.Count, 30, $"seed {seed}: too little to read as a ruin");
                Assert.LessOrEqual(laid.Parts.Count, 260, $"seed {seed}: over one ruin's prism budget");
            }
            Assert.Greater(corkscrews, Seeds / 2, "the corkscrew fits too few seeds for this test to hold much");
        }

        [Test]
        public void The_ruins_kind_changes_from_race_to_race()
        {
            var kinds = new HashSet<int>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                if (!IsCorkscrew(seed)) continue;
                var laid = Ruins(4, seed);
                kinds.Add(SpearlineRuins.KindOf(laid.Parts[0].Leg, laid.Lap[0].Position));
            }
            Assert.GreaterOrEqual(kinds.Count, 4, "the same ruin stands on nearly every course");
        }

        [Test]
        public void The_flown_lap_keeps_every_ring_in_order_from_ring_one()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var course = Course(4, seed);
                var lap = BrittleStarTimeRuins.RuinLap(course, TurnRadius(4));
                int next = 1;
                foreach (var gate in lap)
                {
                    Assert.AreEqual(1f, gate.Axis.magnitude, 1e-3f);
                    if (gate.Radius <= 0f) continue;   // a turn's end
                    Assert.AreEqual(course[next % course.Count].Position, gate.Position, $"seed {seed}");
                    next++;
                }
                Assert.AreEqual(course.Count + 1, next, $"seed {seed}: a ring is missing");
                Assert.AreEqual(course[0].Position, lap[lap.Count - 1].Position, "the lap does not close on ring 0");
            }
        }

        [Test]
        public void A_turn_ends_on_its_circle_pointing_at_the_target()
        {
            float r = TurnRadius(4);
            var from = new Vector3(10f, 20f, 30f);
            Vector3 flow = Vector3.down;
            foreach (var to in new[] { new Vector3(-800f, 20f, 30f), new Vector3(-500f, -300f, 200f), new Vector3(400f, 600f, 30f) })
            {
                Assert.IsTrue(BrittleStarTimeRuins.TurnExit(from, flow, to, r, out var exit));
                Vector3 d = to - from;
                Vector3 n = (d - flow * Vector3.Dot(d, flow)).normalized;
                Vector3 centre = from + n * r;
                Assert.AreEqual(r, (exit - centre).magnitude, 0.01f, "the exit is not on the turning circle");
                // Tangent: the straight to the target leaves square to the circle's radius.
                Assert.AreEqual(0f, Vector3.Dot((exit - centre).normalized, (to - exit).normalized), 1e-3f);
                // And it is the tangent the turn reaches first: the hull is still going forward round the circle.
                Assert.Greater(Vector3.Dot(Vector3.Cross(exit - centre, to - exit), Vector3.Cross(from - centre, flow)), 0f);
            }
            Assert.IsFalse(BrittleStarTimeRuins.TurnExit(from, flow, from + flow * 500f, r, out _), "dead ahead needs no turn");
            Assert.IsFalse(BrittleStarTimeRuins.TurnExit(from, flow, from + Vector3.left * r, r, out _), "inside the circle");
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_lap()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var first = Ruins(4, seed);
                var second = Ruins(4, seed);
                Assert.AreEqual(first.Parts.Count, second.Parts.Count);
                Assert.AreEqual(first.Wheels.Count, second.Wheels.Count);
                for (int i = 0; i < first.Parts.Count; i++)
                {
                    Assert.AreEqual(first.Parts[i].Position, second.Parts[i].Position);
                    Assert.AreEqual(first.Parts[i].Scale, second.Parts[i].Scale);
                }
            }
        }

        [Test]
        public void A_kept_wheel_still_names_its_own_prisms()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                foreach (var wheel in laid.Wheels)
                {
                    Assert.Greater(wheel.Count, 12);
                    Assert.LessOrEqual(wheel.First + wheel.Count, laid.Parts.Count);
                    for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                    {
                        Vector3 d = laid.Parts[i].Position - wheel.Centre;
                        Assert.AreEqual(0f, Vector3.Dot(d, wheel.Axis), 0.01f, $"seed {seed}: a wheel names a prism off its plane");
                        Assert.LessOrEqual(d.magnitude, SpearlineRuins.WheelRadius + 0.5f);
                    }
                }
            }
        }
    }
}
