using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Blast Run AI's planner (<see cref="SwitchbackBlastPlanner"/>, SWITCHBACK.md "AI: the blast planner"), headless:
    /// plan selection (open gate -> Direct, near key -> Blast, far / blocked / gone key -> Thread), the aim (the Legendary
    /// slot covers the lane at contact), the difficulty ladder, determinism, and re-planning when the key is taken.
    /// Real layouts are the shipped shell at the picker's fixed seeds (45_501-45_504).
    /// </summary>
    public class SwitchbackBlastPlannerTests
    {
        const float Inner = 480f, Outer = 1080f, ClearRadius = Inner / 1.22f + 30f;
        const int Gates = 20;
        const float Speed = 120f;

        sealed class World : ISwitchbackAIWorld
        {
            public readonly HashSet<int> Gone = new();
            public readonly HashSet<int> KeysGone = new();
            public bool PrismStanding(int k) => !Gone.Contains(k);
            public bool KeyAvailable(int station) => !KeysGone.Contains(station);
        }

        static List<RaceGate> Course(int intensity, int seed)
        {
            var s = SwitchbackCourseSettings.ForIntensity(intensity);
            s.GateCount = Gates; s.InnerRadius = Inner; s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up; s.FirstGateDistance = 620f;
            return SwitchbackCourse.Generate(seed, s);
        }

        static (List<RaceGate> course, SwitchbackBlastPlanner planner) Fixed(int intensity)
        {
            var course = Course(intensity, SwitchbackBlastRun.FixedSeed(intensity));
            var layout = SwitchbackBlastRun.Layout(course, intensity, Vector3.zero, ClearRadius);
            return (course, new SwitchbackBlastPlanner(layout, course.Count, intensity));
        }

        /// <summary>Where a pilot stands just upstream of station s's key, on the fire line.</summary>
        static Vector3 UpstreamOfKey(BlastRunStation st, float by) => st.Key - (st.Door - st.Key).normalized * by;

        static SwitchbackAISkill Level(int level) => SwitchbackAISkill.ForLevel(level);

        [Test]
        public void AnOpenGate_IsFlownDirect([Values(1, 2, 3, 4)] int intensity)
        {
            var (course, planner) = Fixed(intensity);
            var world = new World();
            int open = -1;
            for (int g = 0; g < course.Count && open < 0; g++) if (planner.StationFor(g) < 0) open = g;
            Assert.GreaterOrEqual(open, 0, "every gate blocked?");
            var d = planner.Decide(open, course[open].Position - course[open].Axis * 300f, Speed, course[open].Position, 0f,
                                   Level(4), world, 1);
            Assert.AreEqual(SwitchbackAIPlan.Direct, d.Plan);
        }

        [Test]
        public void ABlockedGate_WithItsKeyClose_IsBlasted([Values(1, 2, 3, 4)] int intensity)
        {
            // Just upstream of the key on the leg line a perfect pilot blasts, unless threading is honestly cheaper (an
            // almost-empty lane) or something stands between it and the key (I4's dense shell can put the last gate's
            // field there): every refusal must say which, and at least half the stations must be blasted.
            var (course, planner) = Fixed(intensity);
            var world = new World();
            Assert.Greater(planner.Layout.Stations.Count, 0);
            int blasts = 0;
            foreach (var st in planner.Layout.Stations)
            {
                Vector3 self = UpstreamOfKey(st, 120f);
                var d = planner.Decide(st.Gate, self, Speed, course[st.Gate].Position, 0f, Level(4), world, 7);
                Assert.IsTrue(d.Optimal);
                Assert.Greater(planner.ObstacleSize(d.Station), 0);
                if (d.Plan == SwitchbackAIPlan.Blast) { blasts++; Assert.Less(d.CostBlast, d.CostThread); continue; }
                if (d.Why == "key-blocked")
                    Assert.GreaterOrEqual(planner.PathHits(world, self, st.Key, SwitchbackBlastPlanner.BlockedLane, 0f), SwitchbackBlastPlanner.BlockedHits);
                else
                {
                    Assert.AreEqual("cheapest", d.Why, $"gate {st.Gate}");
                    Assert.LessOrEqual(d.CostThread, d.CostBlast, $"gate {st.Gate}");
                    Assert.LessOrEqual(d.LaneHits, 1, $"gate {st.Gate}: threaded a crowded lane");
                }
            }
            Assert.GreaterOrEqual(blasts, (planner.Layout.Stations.Count + 1) / 2,
                                  $"I{intensity}: blasted {blasts} of {planner.Layout.Stations.Count} stations");
        }

        [Test]
        public void ABlockedGate_WithItsKeyFar_IsThreaded()
        {
            var (course, planner) = Fixed(3);
            var world = new World();
            var st = planner.Layout.Stations[0];
            var skill = Level(2);
            var d = planner.Decide(st.Gate, UpstreamOfKey(st, skill.CrystalSearchRadius + 100f), Speed, course[st.Gate].Position, 0f,
                                   skill, world, 3);
            Assert.AreEqual(SwitchbackAIPlan.Thread, d.Plan);
            Assert.AreEqual("key-far", d.Why);
        }

        [Test]
        public void ABlockedGate_WithTheWayToItsKeyBlocked_IsThreaded()
        {
            // Synthetic station: a key 300 u up a leg, a field round the line, and a wall of prisms across the pilot's way to the key.
            var layout = new SwitchbackFeatureLayout();
            Vector3 ring = new(0f, 0f, 0f), flow = Vector3.forward;
            Vector3 door = ring - flow * 8f, key = ring - flow * 300f;
            layout.Stations.Add(new BlastRunStation(0, key, 300f, door, flow, 18f, false, BlastRunWorld.Blocks, 0));
            for (int i = 0; i < 12; i++)   // field beside the line
                layout.Prisms.Add(new SwitchbackFeaturePrism(door - flow * (40f + 20f * i) + Vector3.right * 40f, Quaternion.identity,
                                                             Vector3.one * 10f, PrismKind.Plain));
            Vector3 self = key - flow * 150f + Vector3.right * 100f;
            for (int i = 1; i <= 4; i++)   // the wall between the pilot and the key
                layout.Prisms.Add(new SwitchbackFeaturePrism(Vector3.Lerp(self, key, i / 5f), Quaternion.identity,
                                                             Vector3.one * 10f, PrismKind.Plain));
            var planner = new SwitchbackBlastPlanner(layout, 1, 26f, 18f, 3 * 24f, SwitchbackBlastSlot.Default);
            var d = planner.Decide(0, self, Speed, ring, 0f, Level(4), new World(), 5);
            Assert.AreEqual(SwitchbackAIPlan.Thread, d.Plan);
            Assert.AreEqual("key-blocked", d.Why);

            // Control: without the wall it blasts, so the wall is what made it thread.
            layout.Prisms.RemoveRange(12, 4);
            planner = new SwitchbackBlastPlanner(layout, 1, 26f, 18f, 3 * 24f, SwitchbackBlastSlot.Default);
            Assert.AreEqual(SwitchbackAIPlan.Blast, planner.Decide(0, self, Speed, ring, 0f, Level(4), new World(), 5).Plan);
        }

        [Test]
        public void AGateWhoseObstacleIsAlreadyBlasted_IsFlownDirect()
        {
            var (course, planner) = Fixed(2);
            var world = new World();
            var st = planner.Layout.Stations[0];
            for (int k = 0; k < planner.Layout.Prisms.Count; k++) world.Gone.Add(k);
            var d = planner.Decide(st.Gate, UpstreamOfKey(st, 120f), Speed, course[st.Gate].Position, 0f, Level(4), world, 1);
            Assert.AreEqual(SwitchbackAIPlan.Direct, d.Plan);
            Assert.AreEqual("cleared", d.Why);
        }

        [Test]
        public void WhenARivalTakesTheKey_TheReplanThreads()
        {
            var (course, planner) = Fixed(3);
            var world = new World();
            var st = planner.Layout.Stations[0];
            Vector3 self = UpstreamOfKey(st, 120f);
            Assert.AreEqual(SwitchbackAIPlan.Blast, planner.Decide(st.Gate, self, Speed, course[st.Gate].Position, 0f, Level(4), world, 9).Plan);
            world.KeysGone.Add(0);
            var d = planner.Decide(st.Gate, self, Speed, course[st.Gate].Position, 0.5f, Level(4), world, 9);
            Assert.AreEqual(SwitchbackAIPlan.Thread, d.Plan);
            Assert.AreEqual("key-gone", d.Why);
        }

        [Test]
        public void TheLegendarySlot_CoversTheLaneAtContact([Values(1, 2, 3, 4)] int intensity)
        {
            var (course, planner) = Fixed(intensity);
            var world = new World();
            float sum = 0f, ideal = 0f; int n = 0;
            foreach (var st in planner.Layout.Stations)
            {
                planner.SolveAim(planner.StationFor(st.Gate), world, 0f, out Vector3 f, out Vector3 u);
                Assert.Less(Vector3.Angle(f, (st.Door - st.Key).normalized), 0.01f, "the nose is on the door");
                Assert.IsTrue(planner.Slot.Covers(st.Key, f, u, st.Door, 0f), "the door is in the slot");
                var d = planner.Decide(st.Gate, UpstreamOfKey(st, 120f), Speed, course[st.Gate].Position, 0f, Level(4), world, (uint)st.Gate);
                if (d.Plan != SwitchbackAIPlan.Blast) continue;   // see ABlockedGate_WithItsKeyClose_IsBlasted
                Assert.Less(d.AimErrorDegrees, 1.2f);
                Assert.IsTrue(planner.Slot.Covers(st.Key, d.FireForward, d.FireUp, st.Door, 0f), $"gate {st.Gate}: the erred slot misses the door");
                ideal += planner.LaneCoverage(planner.StationFor(st.Gate), world, course[st.Gate].Position, 0f, f, u);
                sum += d.Coverage; n++;
            }
            // The slot is narrow at its tip, so no aim covers the lane next to the key: Legendary must get what a perfect aim gets.
            Assert.GreaterOrEqual(sum / n, 0.95f * ideal / n, $"I{intensity}: Legendary covers {sum / n:P0} of the lane, a perfect aim {ideal / n:P0}");
            Assert.GreaterOrEqual(ideal / n, 0.6f, $"I{intensity}: a perfect aim covers only {ideal / n:P0} of the lane");
        }

        [Test]
        public void TheRollSolve_LiesAlongTheObstaclesLongAxis()
        {
            // A slab of prisms spread along +x across a leg flowing +z: the gape must lie along x.
            var layout = new SwitchbackFeatureLayout();
            Vector3 flow = Vector3.forward, door = -flow * 8f, key = -flow * 300f;
            layout.Stations.Add(new BlastRunStation(0, key, 300f, door, flow, 18f, false, BlastRunWorld.Blocks, 0));
            for (int i = -5; i <= 5; i++)
                layout.Prisms.Add(new SwitchbackFeaturePrism(new Vector3(12f * i, 2f, -100f), Quaternion.identity, Vector3.one * 6f, PrismKind.Plain));
            var planner = new SwitchbackBlastPlanner(layout, 1, 26f, 18f, 3 * 24f, SwitchbackBlastSlot.Default);
            planner.SolveAim(0, new World(), 0f, out _, out Vector3 up);
            Assert.Greater(Mathf.Abs(Vector3.Dot(up, Vector3.right)), 0.99f);
        }

        static readonly string[] Names = { "", "Easy", "Medium", "Hard", "Legendary" };

        struct LadderStats { public int Blasts, Decisions, Suboptimal; public float Coverage; public int Blasted; public float Regret; }

        static LadderStats Ladder(int level)
        {
            var st = new LadderStats();
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var (course, planner) = Fixed(intensity);
                var world = new World();
                var skill = Level(level);
                foreach (var station in planner.Layout.Stations)
                    for (int seat = 0; seat < 8; seat++)
                        foreach (float by in new[] { 60f, 140f, 260f, 400f })
                        {
                            var d = planner.Decide(station.Gate, UpstreamOfKey(station, by), Speed, course[station.Gate].Position,
                                                   seat * 0.37f, skill, world, (uint)(seat * 104729 + station.Gate));
                            st.Decisions++;
                            if (!d.Optimal) st.Suboptimal++;
                            // Regret against what a perfect pilot at the same spot could do (Legendary's view of the gate).
                            var r = planner.Decide(station.Gate, UpstreamOfKey(station, by), Speed, course[station.Gate].Position,
                                                   seat * 0.37f, Level(4), world, (uint)(seat * 104729 + station.Gate));
                            float best = Mathf.Min(r.CostThread, r.CostBlast);
                            float chosen = d.Plan == SwitchbackAIPlan.Blast ? d.CostBlast / skill.BlastBias : r.CostThread;
                            st.Regret += chosen - best;
                            if (d.Plan == SwitchbackAIPlan.Blast) st.Blasts++;
                            // Aim quality on the same shots for every level (all levels search at least 140 u).
                            if (by <= 140f && !float.IsInfinity(d.CostBlast)) { st.Coverage += d.Coverage; st.Blasted++; }
                        }
            }
            if (st.Blasted > 0) st.Coverage /= st.Blasted;
            st.Regret /= st.Decisions;
            return st;
        }

        [Test]
        public void EveryStepUpTheDifficultyLadder_IsSmarter()
        {
            var s = new LadderStats[5];
            for (int l = 1; l <= 4; l++) s[l] = Ladder(l);
            for (int l = 1; l <= 3; l++)
            {
                string at = $"{Names[l]} -> {Names[l + 1]}";
                Assert.Less(s[l].Blasts, s[l + 1].Blasts, $"{at}: blasts {s[l].Blasts} -> {s[l + 1].Blasts}");
                Assert.GreaterOrEqual(s[l].Regret, s[l + 1].Regret, $"{at}: regret {s[l].Regret:F3} -> {s[l + 1].Regret:F3}");
                Assert.LessOrEqual(s[l].Coverage, s[l + 1].Coverage + 1e-4f, $"{at}: coverage {s[l].Coverage:P1} -> {s[l + 1].Coverage:P1}");
            }
            Assert.Less(s[1].Blasts, s[1].Decisions / 4, "Easy rarely blasts");
            Assert.AreEqual(0, s[4].Suboptimal, "Legendary always takes the cheapest plan");
            Assert.Less(s[4].Regret, 1e-4f);
        }

        [Test]
        public void ThePlanner_IsDeterministic([Values(1, 2, 3, 4)] int level)
        {
            var (course, planner) = Fixed(4);
            var (course2, planner2) = Fixed(4);
            var world = new World();
            foreach (var st in planner.Layout.Stations)
            {
                var a = planner.Decide(st.Gate, UpstreamOfKey(st, 150f), Speed, course[st.Gate].Position, 1.3f, Level(level), world, 42);
                var b = planner2.Decide(st.Gate, UpstreamOfKey(st, 150f), Speed, course2[st.Gate].Position, 1.3f, Level(level), world, 42);
                Assert.AreEqual(a.Plan, b.Plan);
                Assert.AreEqual(a.CostThread, b.CostThread);
                Assert.AreEqual(a.CostBlast, b.CostBlast);
                Assert.AreEqual(a.FireForward, b.FireForward);
                Assert.AreEqual(a.FireUp, b.FireUp);
                Assert.AreEqual(a.Coverage, b.Coverage);
            }
        }

        [Test]
        public void TheMovers_AreReadAtThePilotsTime()
        {
            // I4 has sliders and rotors: the thread lane's count must be a function of the clock (and the same for the same clock).
            var (course, planner) = Fixed(4);
            var world = new World();
            bool varies = false;
            foreach (var st in planner.Layout.Stations)
            {
                Vector3 a = st.Key, b = course[st.Gate].Position;
                int at0 = planner.LaneHits(planner.StationFor(st.Gate), world, a, b, 100f, 0f, 0f);
                Assert.AreEqual(at0, planner.LaneHits(planner.StationFor(st.Gate), world, a, b, 100f, 0f, 0f));
                for (float t = 0.25f; t < 3f && !varies; t += 0.25f)
                    varies |= planner.LaneHits(planner.StationFor(st.Gate), world, a, b, 100f, t, 0f) != at0;
            }
            Assert.IsTrue(varies, "no mover ever changed a lane count: the planner is not posing the movers");
        }

        [Test]
        public void TheSkillTable_ReadsUnsetAsMedium()
        {
            Assert.AreEqual(SwitchbackAISkill.ForLevel(2).DecisionQuality, SwitchbackAISkill.ForLevel(0).DecisionQuality);
            Assert.AreEqual(SwitchbackAISkill.ForLevel(2).DecisionQuality, SwitchbackAISkill.ForLevel(9).DecisionQuality);
        }
    }
}
