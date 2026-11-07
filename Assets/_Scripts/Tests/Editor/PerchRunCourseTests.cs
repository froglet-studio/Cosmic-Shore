using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Perch Run, the Goby's Time race (PERCH_RUN.md): every seed at every intensity walks a full valid course, every
    /// pilot starts the same distance behind ring 0, every peer lays the same coral from the rings, every blocker blocks
    /// its leg's centre line and leaves a rim shot, every hairpin punishes a full charge, every double is one dart, the AI's dart threads every ring, each level adds one thing, and the course's Goby numbers are
    /// GobyPistolConfig.asset's. Runs offline too: Tools/Build/perch_run_harness/run.sh.
    /// </summary>
    public class PerchRunCourseTests
    {
        const int Seeds = 400;

        static List<RaceGate> Course(int intensity, int seed) => PerchRunCourse.Generate(seed, PerchRunCourse.ForIntensity(intensity));

        static List<Coral> CoralOf(List<RaceGate> rings, int intensity)
        {
            var coral = new List<Coral>();
            PerchRunCourse.CoralFor(rings, PerchRunCourse.ForIntensity(intensity), coral);
            return coral;
        }

        [Test]
        public void Every_seed_walks_a_full_valid_course()
        {
            for (int i = 1; i <= 4; i++)
            {
                var s = PerchRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    Assert.IsNotNull(rings, $"I{i} seed {seed}: no course");
                    Assert.IsTrue(PerchRunCourse.Valid(s, rings, out string why), $"I{i} seed {seed}: {why}");
                    // measured here, not through Valid: no leg passes through a ring that is not its own
                    for (int k = 1; k < rings.Count; k++)
                        for (int j = 0; j < rings.Count; j++)
                            if (j != k && j != k - 1)
                                Assert.Greater(PerchRunCourse.DistanceToSegment(rings[j].Position, rings[k - 1].Position, rings[k].Position),
                                    rings[j].Radius + 20f, $"I{i} seed {seed}: leg {k} passes through ring {j}");
                }
            }
        }

        [Test]
        public void The_course_is_the_same_for_the_same_seed()
        {
            for (int i = 1; i <= 4; i++)
            {
                var a = Course(i, 77);
                var b = Course(i, 77);
                for (int k = 0; k < a.Count; k++)
                    Assert.IsTrue(a[k].Position == b[k].Position && a[k].Axis == b[k].Axis, $"I{i}: ring {k} differs");
                Assert.IsFalse(Course(i, 78)[3].Position == a[3].Position, $"I{i}: a different seed walks the same course");
            }
        }

        [Test]
        public void Every_pilot_starts_the_same_distance_behind_ring_zero_facing_it()
        {
            foreach (int count in new[] { 1, 2, 4, 6 })
            {
                var poses = PerchRunCourse.StartPoses(count);
                foreach (var p in poses)
                {
                    Assert.AreEqual(-PerchRunCourse.StartStandoff, Vector3.Dot(p.position - PerchRunCourse.StartRing, PerchRunCourse.StartAxis), 0.01f);
                    Assert.Less(Vector3.Angle(p.rotation * Vector3.forward, PerchRunCourse.StartAxis), 0.01f);
                }
            }
        }

        [Test]
        public void Every_peer_lays_the_same_coral_wherever_the_cell_is()
        {
            var offset = new Vector3(3000f, -1200f, 450f);
            var a = new List<Coral>();
            var b = new List<Coral>();
            for (int i = 1; i <= 4; i++)
            {
                var s = PerchRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= 40; seed++)
                {
                    var rings = Course(i, seed);
                    var moved = new List<RaceGate>();
                    foreach (var r in rings) moved.Add(new RaceGate(r.Position + offset, r.Axis, r.Radius));
                    PerchRunCourse.CoralFor(rings, s, a);
                    PerchRunCourse.CoralFor(moved, s, b);
                    Assert.AreEqual(a.Count, b.Count);
                    for (int k = 0; k < a.Count; k++)
                        Assert.Less((b[k].Position - offset - a[k].Position).magnitude, 0.01f, $"I{i} seed {seed}: coral {k} moved");
                }
            }
        }

        /// <summary>A blocker is a real decision: a dart at the ring's centre from the ring before stops on it, short of the
        /// ring, and a rim shot from the same place threads the ring without touching any coral.</summary>
        [Test]
        public void Every_blocker_stops_the_centre_line_and_leaves_a_rim_shot()
        {
            int blockers = 0;
            for (int i = 3; i <= 4; i++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    var coral = CoralOf(rings, i);
                    foreach (var c in coral)
                    {
                        blockers++;
                        Vector3 from = rings[c.Ring - 1].Position, to = rings[c.Ring].Position;
                        float len = (to - from).magnitude;
                        Assert.IsTrue(PerchRunCourse.FirstCoral(from, (to - from) / len, len, coral, out float hit), $"I{i} seed {seed}: blocker {c.Ring} is off its line");
                        Assert.Less(hit, len - rings[c.Ring].Radius, $"I{i} seed {seed}: blocker {c.Ring} stops the dart too late to matter");
                        Assert.IsTrue(PerchRunCourse.TryRimShot(from, rings, c.Ring, coral, out Vector3 through), $"I{i} seed {seed}: no rim shot at {c.Ring}");
                        Vector3 d = (through - from).normalized;
                        Assert.IsTrue(PerchRunCourse.Threads(from, through + d * PerchRunCourse.Overshoot, rings[c.Ring]), $"I{i} seed {seed}: the rim shot misses ring {c.Ring}");
                    }
                }
            Assert.Greater(blockers, 1000, "too few blockers to mean anything");
        }

        /// <summary>A hairpin is a length decision: from the ring before, a FULL dart through the hairpin's ring leaves the
        /// next ring out of one dart's reach, and a dart charged to just past the mouth leaves it in reach.</summary>
        [Test]
        public void Every_hairpin_punishes_a_full_charge()
        {
            int hairpins = 0;
            for (int i = 2; i <= 4; i++)
            {
                var s = PerchRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    for (int k = 1; k + 1 < rings.Count; k++)
                    {
                        if (!PerchRunCourse.IsHairpin(k, s)) continue;
                        hairpins++;
                        Vector3 from = rings[k - 1].Position, dir = (rings[k].Position - from).normalized;
                        Vector3 full = from + dir * PerchRunCourse.DartRange;
                        Vector3 judged = rings[k].Position + dir * PerchRunCourse.Overshoot;
                        Vector3 after = rings[k + 1].Position;
                        Assert.Greater((after - full).magnitude, PerchRunCourse.DartRange, $"I{i} seed {seed}: a full charge through hairpin {k} costs nothing");
                        Assert.Less((after - judged).magnitude, PerchRunCourse.DartRange - 10f, $"I{i} seed {seed}: a judged dart through hairpin {k} still leaves two");
                        Assert.Greater(Vector3.Angle(rings[k].Axis, rings[k + 1].Axis), 90f, $"I{i} seed {seed}: hairpin {k} is not one");
                    }
                }
            }
            Assert.Greater(hairpins, 2000, "too few hairpins to mean anything");
        }

        /// <summary>The AI's dart from the ring before (or the start line) threads the next ring and never stops on coral
        /// first.</summary>
        [Test]
        public void The_AI_dart_threads_every_ring()
        {
            for (int i = 1; i <= 4; i++)
            {
                var s = PerchRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    var coral = CoralOf(rings, i);
                    for (int k = 0; k < rings.Count; k++)
                    {
                        Vector3 from = k == 0 ? PerchRunCourse.StartPoses(1)[0].position : rings[k - 1].Position;
                        Vector3 aim = PerchRunCourse.PlanDart(from, rings, k, coral, out float length);
                        Assert.Greater(length, 0f, $"I{i} seed {seed}: no dart at ring {k}");
                        Vector3 dir = (aim - from).normalized;
                        float go = length;
                        if (PerchRunCourse.FirstCoral(from, dir, length, coral, out float hit)) go = hit;
                        Assert.IsTrue(PerchRunCourse.Threads(from, from + dir * go, rings[k]), $"I{i} seed {seed}: the AI's dart misses ring {k}");
                        Assert.AreEqual(length, go, 0.01f, $"I{i} seed {seed}: the AI's dart at ring {k} stops on coral");
                    }
                }
            }
        }

        /// <summary>A double is one dart for two rings: from the ring before it, the AI's full dart threads both and
        /// stops on no coral first.</summary>
        [Test]
        public void Every_double_is_one_dart()
        {
            int doubles = 0;
            var s = PerchRunCourse.ForIntensity(4);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var rings = Course(4, seed);
                var coral = CoralOf(rings, 4);
                for (int k = 1; k + 1 < rings.Count; k++)
                {
                    if (!PerchRunCourse.StartsDouble(k, s)) continue;
                    doubles++;
                    Vector3 from = rings[k - 1].Position;
                    Vector3 aim = PerchRunCourse.PlanDart(from, rings, k, coral, out float length);
                    Vector3 dir = (aim - from).normalized;
                    float go = length;
                    if (PerchRunCourse.FirstCoral(from, dir, length, coral, out float hit)) go = hit;
                    Vector3 stop = from + dir * go;
                    Assert.IsTrue(PerchRunCourse.Threads(from, stop, rings[k]) && PerchRunCourse.Threads(from, stop, rings[k + 1]),
                        $"seed {seed}: the double at {k} takes two darts");
                }
            }
            Assert.Greater(doubles, 400, "too few doubles to mean anything");
        }

        [Test]
        public void Each_level_adds_one_thing()
        {
            var s = new PerchRunSettings[4];
            for (int i = 0; i < 4; i++) s[i] = PerchRunCourse.ForIntensity(i + 1);
            Assert.AreEqual(0, s[0].Period, "I1 is open darts");
            Assert.GreaterOrEqual(s[1].HairpinSlot, 0); Assert.Less(s[1].BlockerSlot, 0); Assert.Less(s[1].DoubleSlot, 0);
            Assert.GreaterOrEqual(s[2].HairpinSlot, 0); Assert.GreaterOrEqual(s[2].BlockerSlot, 0); Assert.Less(s[2].DoubleSlot, 0);
            Assert.GreaterOrEqual(s[3].HairpinSlot, 0); Assert.GreaterOrEqual(s[3].BlockerSlot, 0); Assert.GreaterOrEqual(s[3].DoubleSlot, 0);
            for (int i = 1; i < 4; i++)
            {
                Assert.AreNotEqual(s[i].HairpinSlot, s[i].BlockerSlot, $"I{i + 1}: a hairpin and a blocker share a ring");
                Assert.AreNotEqual(s[i].HairpinSlot, s[i].DoubleSlot, $"I{i + 1}: a hairpin and a double share a ring");
            }
            Assert.Greater(s[1].HairpinMin, 90f, "a hairpin turns back");
            for (int i = 0; i < 3; i++)
            {
                Assert.Greater(s[i].RingRadius, s[i + 1].RingRadius, $"I{i + 2}: the mouths shrink");
                Assert.GreaterOrEqual(s[i].CoralSize, s[i + 1].CoralSize);
                Assert.LessOrEqual(s[i].MaxLeg, s[i + 1].MaxLeg);
                Assert.LessOrEqual(s[i + 1].MaxLeg, PerchRunCourse.DartRange - PerchRunCourse.Overshoot, "a leg is one dart");
            }
        }

        [Test]
        public void The_course_is_cut_for_the_shipped_goby()
        {
            string path = Path.Combine("Assets", "_SO_Assets", "VesselActions", "GobyPistol", "GobyPistolConfig.asset");
            Assert.IsTrue(File.Exists(path), $"{path} missing");
            string yaml = File.ReadAllText(path);
            float Field(string name)
            {
                var m = Regex.Match(yaml, $@"^  {name}: ([0-9.]+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"{name} not found in GobyPistolConfig.asset");
                return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Assert.AreEqual(PerchRunCourse.DartRange, Field("dartRange"), 0.01f, "dartRange");
            Assert.AreEqual(PerchRunCourse.DartMinRange, Field("dartMinRange"), 0.01f, "dartMinRange");
            Assert.AreEqual(PerchRunCourse.DartChargeSeconds, Field("dartChargeSeconds"), 0.001f, "dartChargeSeconds");
            Assert.AreEqual(PerchRunCourse.DartSeconds, Field("dartSeconds"), 0.001f, "dartSeconds");
            Assert.AreEqual(PerchRunCourse.DartCooldown, Field("dartCooldown"), 0.001f, "dartCooldown");
            Assert.AreEqual(PerchRunCourse.HullRadius, Field("hullRadius"), 0.001f, "hullRadius");
            Assert.AreEqual(PerchRunCourse.AiCruiseSpeed, Field("aiCruiseSpeed"), 0.01f, "aiCruiseSpeed");
            Assert.AreEqual(PerchRunCourse.AiTurnRate, Field("aiTurnRate"), 0.01f, "aiTurnRate");
            Assert.AreEqual(PerchRunCourse.PerchSeconds, Field("perchSeconds"), 0.001f, "perchSeconds");
            Assert.AreEqual(PerchRunCourse.IslandMinLength, Field("islandMinLength"), 0.01f, "islandMinLength");
        }
    }
}
