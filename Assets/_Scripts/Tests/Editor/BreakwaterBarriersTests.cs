using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Breakwater's barriers (BREAKWATER.md "The barriers"): only I4 has them, they stand on the legs the ruins leave
    /// bare, a curtain or a lock really is across its leg's flown line, a minefield leaves the thread open, no prism
    /// comes into a station's reach, another leg's lane or the way in from the pads, the prism count stays in budget
    /// and the lay is a pure function of the course. Runs offline too: Tools/Build/breakwater_ruins_harness/run.sh.
    /// </summary>
    public class BreakwaterBarriersTests
    {
        const int Seeds = 100;
        const int LeadIn = 1;
        const int Stations = 15;
        const int PrismBudget = 700;

        static Vector3[] Pads => BreakwaterCourse.SpawnPadRing(Vector3.zero, BreakwaterCourseSettings.DefaultSpawnRingRadius);

        static List<RaceGate> Course(int intensity, int seed)
        {
            var settings = BreakwaterCourseSettings.ForIntensity(intensity);
            settings.StationCount = Stations;
            var stations = BreakwaterCourse.Generate(seed, settings);
            if (stations == null || stations.Count < Stations) return null;
            var gates = new List<RaceGate>(stations.Count);
            foreach (var s in stations) gates.Add(new RaceGate(s.Position, s.Axis, s.PortRadius));
            return gates;
        }

        sealed class Laid
        {
            public List<RaceGate> Course, Lap = new();
            public List<BreakwaterBarrierPart> Parts = new();
        }

        static Laid Barriers(int intensity, int seed)
        {
            var laid = new Laid { Course = Course(intensity, seed) };
            if (laid.Course == null) return null;
            BreakwaterBarriers.Lay(laid.Course, LeadIn, intensity, Pads, laid.Lap, laid.Parts);
            return laid;
        }

        static float Reach(in BreakwaterBarrierPart part) => 0.5f * part.Scale.magnitude;

        [Test]
        public void Only_the_hardest_rung_has_barriers_and_every_I4_course_has_some()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var laid = Barriers(i, seed);
                    if (laid == null) continue;
                    Assert.AreEqual(0, laid.Parts.Count, $"I{i} seed {seed}");
                }
                var four = Barriers(4, seed);
                if (four == null) continue;
                var legs = new HashSet<int>();
                foreach (var part in four.Parts) legs.Add(part.Leg);
                Assert.GreaterOrEqual(legs.Count, 3, $"I4 seed {seed}: barriers standing");
                Assert.LessOrEqual(four.Parts.Count, PrismBudget, $"I4 seed {seed}: prisms");
            }
        }

        [Test]
        public void Barriers_stand_only_on_the_legs_the_ruins_leave_bare()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Barriers(4, seed);
                if (laid == null) continue;
                foreach (var part in laid.Parts)
                    Assert.IsTrue(BreakwaterRuins.OnBareLeg(part.Leg), $"seed {seed} leg {part.Leg}");
            }
        }

        [Test]
        public void A_curtain_and_a_lock_stand_across_their_legs_flown_line()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Barriers(4, seed);
                if (laid == null) continue;
                var crossed = new Dictionary<int, bool>();
                foreach (var part in laid.Parts)
                {
                    if (part.Danger) continue;
                    var a = laid.Lap[part.Leg];
                    var b = laid.Lap[(part.Leg + 1) % laid.Lap.Count];
                    bool onLine = BreakwaterBarriers.LegDistance(a, b, part.Position) < 0.5f * BreakwaterBarriers.PlatePitch;
                    crossed[part.Leg] = (crossed.TryGetValue(part.Leg, out bool was) && was) || onLine;
                }
                foreach (var pair in crossed)
                    Assert.IsTrue(pair.Value, $"seed {seed} leg {pair.Key}: the line is open through the wall");
            }
        }

        [Test]
        public void A_minefield_leaves_the_thread_open()
        {
            int mines = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Barriers(4, seed);
                if (laid == null) continue;
                foreach (var part in laid.Parts)
                {
                    if (!part.Danger) continue;
                    mines++;
                    var a = laid.Lap[part.Leg];
                    var b = laid.Lap[(part.Leg + 1) % laid.Lap.Count];
                    Assert.GreaterOrEqual(BreakwaterBarriers.LegDistance(a, b, part.Position) - Reach(part),
                        BreakwaterBarriers.ThreadRadius, $"seed {seed} leg {part.Leg}: a mine in the thread");
                }
            }
            Assert.Greater(mines, 0, "no minefield stood on any course");
        }

        [Test]
        public void No_barrier_prism_is_in_a_stations_reach_another_legs_lane_or_the_way_in()
        {
            float reach = BreakwaterCourse.StationReach(BreakwaterCourseSettings.PortRadiusForIntensity(4));
            var settings = BreakwaterRuins.Settings(4);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Barriers(4, seed);
                if (laid == null) continue;
                foreach (var part in laid.Parts)
                {
                    foreach (var gate in laid.Course)
                        Assert.GreaterOrEqual((part.Position - gate.Position).magnitude, reach, $"seed {seed}: in a station's reach");
                    for (int leg = 0; leg < laid.Lap.Count; leg++)
                        if (leg != part.Leg)
                            Assert.GreaterOrEqual(SpearlineRuins.LegDistance(laid.Lap, leg, part.Position),
                                BreakwaterRuins.ClearRadius, $"seed {seed}: leg {part.Leg}'s barrier in leg {leg}'s lane");
                    var asRuin = new SpearlineRuinPart(part.Position, part.Forward, part.Up, part.Scale, part.Leg);
                    Assert.IsFalse(BreakwaterRuins.ReachesTheStart(asRuin, laid.Course, LeadIn, Pads, settings, 0f),
                        $"seed {seed}: in the way in");
                }
            }
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_course()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var a = Barriers(4, seed);
                var b = Barriers(4, seed);
                if (a == null) continue;
                Assert.AreEqual(a.Parts.Count, b.Parts.Count, $"seed {seed}");
                for (int i = 0; i < a.Parts.Count; i++)
                {
                    Assert.AreEqual(a.Parts[i].Position, b.Parts[i].Position, $"seed {seed} part {i}");
                    Assert.AreEqual(a.Parts[i].Kind, b.Parts[i].Kind, $"seed {seed} part {i}");
                }
            }
        }
    }
}
