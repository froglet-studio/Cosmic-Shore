using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Breakwater's garden ruins (BREAKWATER.md "The ruins"): only I4 has them, the lane down every leg of the circuit
    /// and the reach of every station stay clear of every standing ruin, the way in (the pads, the start gate, its leg
    /// into the circuit) is clear, every other leg is bare, a wheel is laid whole or not at all, the colours share the
    /// prisms out, and the lay is a pure function of the course. Runs offline too:
    /// Tools/Build/breakwater_ruins_harness/run.sh.
    /// </summary>
    public class BreakwaterRuinsTests
    {
        const int Seeds = 100;
        const int LeadIn = 1;
        const int Stations = 15;      // the shipped target: a start gate and fourteen stations

        static Vector3[] Pads => BreakwaterCourse.SpawnPadRing(Vector3.zero, BreakwaterCourseSettings.DefaultSpawnRingRadius);

        /// <summary>The course as the controller receives it (BreakwaterCourseSource), or null where the walk fails.</summary>
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
            public List<SpearlineRuinPart> Parts = new();
            public List<SpearlineRuinWheel> Wheels = new();
            public List<int> Ruin = new();
        }

        static Laid Ruins(int intensity, int seed)
        {
            var laid = new Laid { Course = Course(intensity, seed) };
            if (laid.Course == null) return null;
            BreakwaterRuins.Lay(laid.Course, LeadIn, intensity, Pads, laid.Lap, laid.Parts, laid.Wheels, laid.Ruin);
            return laid;
        }

        static bool Turns(List<SpearlineRuinWheel> wheels, int part)
        {
            foreach (var wheel in wheels)
                if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
            return false;
        }

        static int Standing(List<int> ruin)
        {
            int n = 0;
            foreach (int r in ruin)
                if (r >= 0) n++;
            return n;
        }

        static int RuinCount(List<int> ruin)
        {
            int top = -1;
            foreach (int r in ruin) top = Mathf.Max(top, r);
            return top + 1;
        }

        [Test]
        public void The_walk_stands_on_enough_seeds_to_test()
        {
            int stood = 0;
            for (int seed = 1; seed <= Seeds; seed++)
                if (Course(4, seed) != null) stood++;
            Assert.GreaterOrEqual(stood, Seeds / 2, $"only {stood} of {Seeds} I4 courses stood");
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var laid = Ruins(i, seed);
                    if (laid == null) continue;
                    Assert.AreEqual(0, laid.Parts.Count + laid.Wheels.Count + laid.Ruin.Count, $"I{i} seed {seed}");
                }
                var four = Ruins(4, seed);
                if (four == null) continue;
                Assert.Greater(Standing(four.Ruin), 0, $"I4 seed {seed}");
                Assert.AreEqual(four.Parts.Count, four.Ruin.Count, $"seed {seed}: every part is numbered");
                Assert.AreEqual(four.Course.Count - LeadIn, four.Lap.Count, $"seed {seed}: the lap is the circuit");
            }
        }

        [Test]
        public void The_lane_down_every_leg_of_the_circuit_is_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                if (laid == null) continue;
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Ruin[i] < 0 || Turns(laid.Wheels, i)) continue;
                    // Every leg, the closing one (the last station back to the first) too.
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(laid.Lap, laid.Parts[i]),
                        BreakwaterRuins.ClearRadius - 1e-3f, $"seed {seed} part {i} (leg {laid.Parts[i].Leg})");
                }
            }
        }

        [Test]
        public void No_ruin_stands_inside_a_stations_reach()
        {
            float port = BreakwaterCourseSettings.PortRadiusForIntensity(4);
            float reach = BreakwaterCourse.StationReach(port);
            Assert.AreEqual(reach - port + BreakwaterRuins.StationKeep, BreakwaterRuins.RingMargin(4), 1e-3f);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                if (laid == null) continue;
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Ruin[i] < 0 || Turns(laid.Wheels, i)) continue;
                    // Against the station's own bounding reach, every station and the start gate, not the margin alone.
                    foreach (var gate in laid.Course)
                        Assert.GreaterOrEqual((laid.Parts[i].Position - gate.Position).magnitude, reach,
                            $"seed {seed} part {i}: inside a station's reach");
                }
            }
        }

        [Test]
        public void The_way_in_from_the_pads_is_clear()
        {
            var pads = Pads;
            var settings = BreakwaterRuins.Settings(4);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                if (laid == null) continue;
                Vector3 start = laid.Course[0].Position;
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Ruin[i] < 0) continue;
                    var part = laid.Parts[i];
                    Assert.IsFalse(BreakwaterRuins.ReachesTheStart(part, laid.Course, LeadIn, pads, settings, 0f),
                        $"seed {seed} part {i}");
                    // And measured here, without the rule's own sampler: the part's centre against every pad's run.
                    foreach (var pad in pads)
                    {
                        Vector3 run = start - pad;
                        float t = Mathf.Clamp01(Vector3.Dot(part.Position - pad, run) / run.sqrMagnitude);
                        Assert.GreaterOrEqual((part.Position - (pad + run * t)).magnitude, BreakwaterRuins.PadKeep,
                            $"seed {seed} part {i}: on the run from a pad to the start gate");
                    }
                }
            }
        }

        [Test]
        public void Every_other_leg_is_bare()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                if (laid == null) continue;
                for (int i = 0; i < laid.Parts.Count; i++)
                    if (laid.Ruin[i] >= 0)
                        Assert.AreEqual(0, laid.Parts[i].Leg % 2, $"seed {seed} part {i}: a ruin on odd leg {laid.Parts[i].Leg}");
            }
        }

        [Test]
        public void A_wheel_is_laid_whole_or_not_at_all()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                if (laid == null) continue;
                foreach (var wheel in laid.Wheels)
                {
                    int standing = 0;
                    for (int i = wheel.First; i < wheel.First + wheel.Count && i < laid.Parts.Count; i++)
                        if (laid.Ruin[i] >= 0) standing++;
                    Assert.IsTrue(standing == 0 || standing == wheel.Count, $"seed {seed}: a wheel with {standing} of {wheel.Count}");
                }
            }
        }

        [Test]
        public void There_are_enough_ruins_to_read_as_ruins()
        {
            int fewest = int.MaxValue, most = 0, courses = 0;
            long prisms = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                if (laid == null) continue;
                courses++;
                int count = RuinCount(laid.Ruin);
                fewest = Mathf.Min(fewest, count);
                most = Mathf.Max(most, count);
                prisms += Standing(laid.Ruin);
            }
            long mean = prisms / Mathf.Max(1, courses);
            // Thirteen legs in the list the generator walks; the seven even ones may carry a ruin.
            Assert.GreaterOrEqual(fewest, MinRuins, $"fewest ruins on a course: {fewest}");
            Assert.LessOrEqual(most, 7, $"most ruins on a course: {most}");
            Assert.GreaterOrEqual(mean, MinMeanPrisms, $"mean ruin prisms a course: {mean}");
            Assert.LessOrEqual(mean, MaxMeanPrisms, $"mean ruin prisms a course: {mean}");
        }

        // Measured 2026-11-24 over the I4 courses of seeds 1-100 (Tools/Build/breakwater_ruins_harness/run.sh
        // --measure): 7 ruins on every course, 744 prisms a course (675 to 816), no wheel (none fits these legs).
        const int MinRuins = 7;
        const int MinMeanPrisms = 600, MaxMeanPrisms = 900;

        [Test]
        public void The_colours_share_the_prisms_out()
        {
            var slotOf = new List<int>();
            var inSlot = new List<int>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                if (laid == null) continue;
                BreakwaterRuins.ShareOut(laid.Ruin, 3, slotOf, inSlot);
                Assert.AreEqual(RuinCount(laid.Ruin), slotOf.Count, $"seed {seed}: every ruin has a colour");
                int total = 0, biggestRuin = 0;
                var perRuin = new int[slotOf.Count];
                foreach (int r in laid.Ruin)
                    if (r >= 0) perRuin[r]++;
                foreach (int n in perRuin) biggestRuin = Mathf.Max(biggestRuin, n);
                foreach (int n in inSlot) total += n;
                Assert.AreEqual(Standing(laid.Ruin), total, $"seed {seed}: every prism is counted once");
                // Greedy fill: no colour is ahead of another by more than one whole ruin.
                int low = int.MaxValue, high = 0;
                foreach (int n in inSlot) { low = Mathf.Min(low, n); high = Mathf.Max(high, n); }
                Assert.LessOrEqual(high - low, biggestRuin, $"seed {seed}: {high} against {low}");
            }
            BreakwaterRuins.ShareOut(new List<int> { 0, 0, 1 }, 0, slotOf, inSlot);
            Assert.AreEqual(0, slotOf.Count, "no colours, no share");
        }

        [Test]
        public void The_ruins_are_the_same_for_the_same_course()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var a = Ruins(4, seed);
                var b = Ruins(4, seed);
                if (a == null) continue;
                Assert.AreEqual(a.Parts.Count, b.Parts.Count, $"seed {seed}");
                for (int i = 0; i < a.Parts.Count; i++)
                {
                    Assert.AreEqual(a.Parts[i].Position, b.Parts[i].Position, $"seed {seed} part {i}");
                    Assert.AreEqual(a.Ruin[i], b.Ruin[i], $"seed {seed} part {i}");
                }
            }
        }
    }
}
