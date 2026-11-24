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
            public List<BreakwaterBarrierMover> Movers = new();
        }

        static Laid Barriers(int intensity, int seed)
        {
            var laid = new Laid { Course = Course(intensity, seed) };
            if (laid.Course == null) return null;
            BreakwaterBarriers.Lay(laid.Course, LeadIn, intensity, Pads, laid.Lap, laid.Parts, laid.Movers);
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
                    if (part.Kind != BreakwaterBarriers.Curtain && part.Kind != BreakwaterBarriers.Lock) continue;
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
        public void A_broken_prism_grows_back_only_after_the_field_has_passed()
        {
            // A wall never closes on a round already in the air: the field is "at" a barrier past a rocket's reach.
            const float rocketReach = 229f;
            Assert.IsTrue(BreakwaterBarriers.FieldAt(rocketReach), "a hull a rocket's reach away is still at the barrier");
            Assert.IsTrue(BreakwaterBarriers.FieldAt(0f));
            Assert.IsFalse(BreakwaterBarriers.FieldAt(BreakwaterBarriers.RegrowClear));
            Assert.IsFalse(BreakwaterBarriers.Regrows(0f));
            Assert.IsFalse(BreakwaterBarriers.Regrows(BreakwaterBarriers.RegrowSeconds - 0.01f));
            Assert.IsTrue(BreakwaterBarriers.Regrows(BreakwaterBarriers.RegrowSeconds));
            // Short enough that the next lap meets the wall: I4 laps run from about 33 s.
            Assert.Less(BreakwaterBarriers.RegrowSeconds, 20f);
        }

        [Test]
        public void A_mover_is_laid_whole_and_names_only_its_own_parts()
        {
            int turnstiles = 0, shutters = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Barriers(4, seed);
                if (laid == null) continue;
                var moved = new HashSet<int>();
                foreach (var mover in laid.Movers)
                {
                    Assert.IsTrue(BreakwaterBarriers.Moves(mover.Kind), $"seed {seed}");
                    Assert.Greater(mover.Count, 0, $"seed {seed}");
                    Assert.LessOrEqual(mover.First + mover.Count, laid.Parts.Count, $"seed {seed}");
                    for (int i = mover.First; i < mover.First + mover.Count; i++)
                    {
                        Assert.AreEqual(mover.Kind, laid.Parts[i].Kind, $"seed {seed} part {i}");
                        Assert.IsTrue(moved.Add(i), $"seed {seed} part {i}: in two movers");
                    }
                    if (mover.Kind == BreakwaterBarriers.Shutter) shutters++; else turnstiles++;
                }
                for (int i = 0; i < laid.Parts.Count; i++)
                    Assert.AreEqual(BreakwaterBarriers.Moves(laid.Parts[i].Kind), moved.Contains(i), $"seed {seed} part {i}");
            }
            Assert.Greater(turnstiles, 0, "no turnstile stood on any course");
            Assert.Greater(shutters, 0, "no shutter stood on any course");
        }

        [Test]
        public void A_mover_keeps_its_clearances_through_its_whole_cycle()
        {
            float reach = BreakwaterCourse.StationReach(BreakwaterCourseSettings.PortRadiusForIntensity(4));
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Barriers(4, seed);
                if (laid == null) continue;
                foreach (var mover in laid.Movers)
                {
                    float period = BreakwaterBarriers.Period(mover);
                    for (int i = mover.First; i < mover.First + mover.Count; i++)
                        for (int k = 0; k < 40; k++)
                        {
                            BreakwaterBarriers.Pose(laid.Parts[i], mover, k * period / 40f + 0.37f, out var p, out _, out _);
                            foreach (var gate in laid.Course)
                                Assert.GreaterOrEqual((p - gate.Position).magnitude, reach, $"seed {seed}: a mover in a station's reach");
                            for (int leg = 0; leg < laid.Lap.Count; leg++)
                                if (leg != laid.Parts[i].Leg)
                                    Assert.GreaterOrEqual(SpearlineRuins.LegDistance(laid.Lap, leg, p),
                                        BreakwaterRuins.ClearRadius - 4f, $"seed {seed}: a mover in leg {leg}'s lane");
                        }
                }
            }
        }

        [Test]
        public void A_turnstile_never_sweeps_the_thread()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Barriers(4, seed);
                if (laid == null) continue;
                foreach (var mover in laid.Movers)
                {
                    if (mover.Kind != BreakwaterBarriers.Turnstile) continue;
                    for (int i = mover.First; i < mover.First + mover.Count; i++)
                    {
                        var part = laid.Parts[i];
                        var a = laid.Lap[part.Leg];
                        var b = laid.Lap[(part.Leg + 1) % laid.Lap.Count];
                        for (int k = 0; k < 24; k++)
                        {
                            BreakwaterBarriers.Pose(part, mover, k * 0.41f, out var p, out _, out _);
                            // The blade's long side points outward, so its inner end is half its length nearer.
                            Assert.GreaterOrEqual(BreakwaterBarriers.LegDistance(a, b, p) - 0.5f * part.Scale.z,
                                BreakwaterBarriers.ThreadRadius, $"seed {seed} leg {part.Leg}: a blade in the thread");
                        }
                    }
                }
            }
        }

        [Test]
        public void A_shutter_is_shut_across_the_line_and_open_round_the_thread()
        {
            Assert.AreEqual(0f, BreakwaterBarriers.ShutterOpening(0f), 1e-4f);
            Assert.AreEqual(0f, BreakwaterBarriers.ShutterOpening(BreakwaterBarriers.ShutterPeriod), 1e-4f);
            float openAt = BreakwaterBarriers.ShutterShut + BreakwaterBarriers.ShutterMove + 0.5f * BreakwaterBarriers.ShutterOpen;
            Assert.AreEqual(1f, BreakwaterBarriers.ShutterOpening(openAt), 1e-4f);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Barriers(4, seed);
                if (laid == null) continue;
                foreach (var mover in laid.Movers)
                {
                    if (mover.Kind != BreakwaterBarriers.Shutter) continue;
                    float nearestShut = float.MaxValue, nearestOpen = float.MaxValue;
                    for (int i = mover.First; i < mover.First + mover.Count; i++)
                    {
                        var part = laid.Parts[i];
                        var a = laid.Lap[part.Leg];
                        var b = laid.Lap[(part.Leg + 1) % laid.Lap.Count];
                        // mover.Rate is the shutter's step out of time with the clock.
                        BreakwaterBarriers.Pose(part, mover, -mover.Rate, out var shut, out _, out _);
                        BreakwaterBarriers.Pose(part, mover, openAt - mover.Rate, out var open, out _, out _);
                        nearestShut = Mathf.Min(nearestShut, BreakwaterBarriers.LegDistance(a, b, shut));
                        nearestOpen = Mathf.Min(nearestOpen, BreakwaterBarriers.LegDistance(a, b, open));
                    }
                    Assert.Less(nearestShut, 0.75f * BreakwaterBarriers.PlatePitch, $"seed {seed}: shut, the line is open");
                    Assert.GreaterOrEqual(nearestOpen - 0.5f * BreakwaterBarriers.PlateSize, BreakwaterBarriers.ThreadRadius,
                        $"seed {seed}: open, a plate is in the thread");
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
