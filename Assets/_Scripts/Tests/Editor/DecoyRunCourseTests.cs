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
    /// Decoy Run, the Jacks' Time race (DECOY_RUN.md): every seed at every intensity walks a full valid course of flats,
    /// every pilot starts with its board on flat 0, every peer derives the same flats and chalk from the rings, the AI's
    /// cycle threads every group in one cycle once its board lies on the group's flat, each level adds one thing, and the
    /// course's Jacks numbers are JacksConfig.asset's. Runs offline too: Tools/Build/decoy_run_harness/run.sh.
    /// </summary>
    public class DecoyRunCourseTests
    {
        const int Seeds = 400;

        static List<RaceGate> Course(int intensity, int seed) => DecoyRunCourse.Generate(seed, DecoyRunCourse.ForIntensity(intensity));

        [Test]
        public void Every_seed_walks_a_full_valid_course()
        {
            for (int i = 1; i <= 4; i++)
            {
                var s = DecoyRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    Assert.IsNotNull(rings, $"I{i} seed {seed}: no course");
                    Assert.IsTrue(DecoyRunCourse.Valid(s, rings, out string why), $"I{i} seed {seed}: {why}");
                    // measured here, not through Valid: no leg passes through a ring that is not its own
                    for (int k = 1; k < rings.Count; k++)
                        for (int j = 0; j < rings.Count; j++)
                            if (j != k && j != k - 1)
                                Assert.Greater(DecoyRunCourse.DistanceToSegment(rings[j].Position, rings[k - 1].Position, rings[k].Position),
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
                Assert.IsFalse(Course(i, 78)[2].Position == a[2].Position, $"I{i}: a different seed walks the same course");
            }
        }

        [Test]
        public void Every_pilot_starts_level_on_flat_zero_the_same_distance_behind_the_line()
        {
            foreach (int count in new[] { 1, 2, 3, 4, 6 })
            {
                var poses = DecoyRunCourse.StartPoses(count);
                Assert.AreEqual(count, poses.Length);
                for (int k = 0; k < count; k++)
                {
                    var p = poses[k];
                    Assert.Less(Mathf.Abs(Vector3.Dot(p.position - DecoyRunCourse.StartRing, DecoyRunCourse.StartFlatNormal)), 0.01f,
                        $"{count} pilots: slot {k} is off the equator");
                    Assert.Less(Vector3.Angle(p.rotation * Vector3.up, DecoyRunCourse.StartFlatNormal), 0.1f, $"slot {k}: up");
                    Assert.Less(Vector3.Angle(p.rotation * Vector3.forward, DecoyRunCourse.StartAxis), 0.1f, $"slot {k}: facing");
                    Assert.AreEqual(-DecoyRunCourse.StartStandoff, Vector3.Dot(p.position - DecoyRunCourse.StartRing, DecoyRunCourse.StartAxis),
                        0.01f, $"slot {k}: standoff");
                    // the start ring is on every pilot's board, inside the mouth's on-board band
                    Assert.Less(Mathf.Abs(Vector3.Dot(DecoyRunCourse.StartRing - p.position, p.rotation * Vector3.up)), 1f);
                    for (int j = 0; j < k; j++)
                        Assert.GreaterOrEqual((poses[j].position - p.position).magnitude, DecoyRunCourse.StartSpacing - 0.01f);
                }
            }
        }

        [Test]
        public void Every_peer_derives_the_same_flats_and_chalk_wherever_the_cell_is()
        {
            var offset = new Vector3(1234f, -567f, 89f);
            var chalk = new List<Vector3>();
            var moved = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
            {
                var s = DecoyRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= 60; seed++)
                {
                    var rings = Course(i, seed);
                    var shifted = new List<RaceGate>();
                    foreach (var g in rings) shifted.Add(new RaceGate(g.Position + offset, g.Axis, g.Radius));
                    for (int k = 0; k < rings.Count; k++)
                        Assert.Less((DecoyRunCourse.FlatNormal(rings, k, s) - DecoyRunCourse.FlatNormal(shifted, k, s)).magnitude, 1e-4f);
                    DecoyRunCourse.ChalkFor(rings, s, DecoyRunCourse.StartStandoff, chalk);
                    DecoyRunCourse.ChalkFor(shifted, s, DecoyRunCourse.StartStandoff, moved);
                    Assert.AreEqual(chalk.Count, moved.Count);
                    for (int k = 0; k < chalk.Count; k++) Assert.Less((moved[k] - offset - chalk[k]).magnitude, 0.01f);
                }
            }
        }

        [Test]
        public void Every_flat_holds_its_rings_and_its_hinge()
        {
            for (int i = 1; i <= 4; i++)
            {
                var s = DecoyRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    int flats = DecoyRunCourse.FlatOf(rings.Count - 1, s) + 1;
                    Assert.AreEqual(s.TiltEachGroup ? (s.RingCount + s.Group - 1) / s.Group : 1, flats, $"I{i} seed {seed}: flats");
                    for (int k = 1; k < rings.Count; k++)
                    {
                        if (DecoyRunCourse.FlatOf(k, s) == DecoyRunCourse.FlatOf(k - 1, s)) continue;
                        // the hinge: the last ring of the old flat, and the line it is flown along, lie on the new flat
                        Vector3 n = DecoyRunCourse.FlatNormal(rings, k, s);
                        Assert.Less(Mathf.Abs(Vector3.Dot(rings[k - 1].Position - rings[k].Position, n)), 1f, $"I{i} seed {seed}: hinge point {k}");
                        Assert.Less(Mathf.Abs(Vector3.Dot(rings[k - 1].Axis, n)), 0.02f, $"I{i} seed {seed}: hinge line {k}");
                        float roll = Vector3.Angle(n, DecoyRunCourse.FlatNormal(rings, k - 1, s));
                        Assert.IsTrue(roll >= s.TiltMin - 0.5f && roll <= s.TiltMax + 0.5f, $"I{i} seed {seed}: roll {roll:F1} at {k}");
                    }
                }
            }
        }

        /// <summary>The chalk is two rails beside each leg: never in a mouth, never on a leg a hull flies, and on the
        /// leg's flat (a dot is as far above the board as the leg's own ends are: zero).</summary>
        [Test]
        public void The_chalk_keeps_off_every_mouth_and_every_leg()
        {
            var chalk = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
            {
                var s = DecoyRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= 60; seed++)
                {
                    var rings = Course(i, seed);
                    DecoyRunCourse.ChalkFor(rings, s, DecoyRunCourse.StartStandoff, chalk);
                    Assert.Greater(chalk.Count, rings.Count * 6, $"I{i} seed {seed}: too little chalk");
                    Vector3 start = rings[0].Position - rings[0].Axis * DecoyRunCourse.StartStandoff;
                    foreach (var c in chalk)
                        for (int j = 0; j < rings.Count; j++)
                        {
                            var r = rings[j];
                            Assert.Greater((c - r.Position).magnitude, r.Radius + DecoyRunCourse.ChalkSize, $"I{i} seed {seed}: chalk in a mouth");
                            Vector3 from = j == 0 ? start : rings[j - 1].Position;
                            Assert.GreaterOrEqual(DecoyRunCourse.DistanceToSegment(c, from, r.Position), r.Radius + DecoyRunCourse.ChalkSize - 0.01f,
                                $"I{i} seed {seed}: chalk on leg {j}");
                        }
                }
            }
        }

        /// <summary>
        /// The AI flies the race group by group: with its board on the group's flat, one cycle threads the whole group (a
        /// door at I1, a skewer at I2-I3 in one zip, a skewer and a corner at I4 in two zips with the second decoy). The
        /// take-off threads nothing (it is a teleport), so every ring is threaded by a zip: zip 1 from the take-off to
        /// decoy 0, zip 2 from decoy 0 to decoy 1. The cycle starts where the last one ended, and no run is long.
        /// </summary>
        [Test]
        public void One_cycle_takes_each_group_once_the_board_lies_on_its_flat()
        {
            for (int i = 1; i <= 4; i++)
            {
                var s = DecoyRunCourse.ForIntensity(i);
                int zips = s.StartTimeLevel >= 5 ? 2 : 1;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    Vector3 hull = DecoyRunCourse.StartPoses(1)[0].position;
                    int next = 0, cycles = 0;
                    while (next < rings.Count)
                    {
                        Vector3 up = DecoyRunCourse.FlatNormal(rings, next, s);
                        int want = DecoyRunCourse.RingsACycleTakes(next, zips, s);
                        int group = Mathf.Min(s.Group, rings.Count - next);
                        if (DecoyRunCourse.StartsGroup(next, s)) Assert.AreEqual(group, want, $"I{i} seed {seed}: ring {next}'s group is not one cycle");
                        int onBoard = DecoyRunCourse.RingsOnBoard(hull, up, rings, next, rings.Count, want, s);
                        Assert.AreEqual(want, onBoard, $"I{i} seed {seed}: ring {next} not on the board");
                        int made = DecoyRunCourse.PlanCycle(hull, up, rings, next, rings.Count, zips, want, s, out var plan, out float run);
                        Assert.AreEqual(want, made, $"I{i} seed {seed}: the cycle at ring {next} took {made}");
                        Vector3 m = 0.5f * (plan.Jack0To + plan.Jack1To);
                        Vector3 d0 = plan.Jack0Lays == 0 ? plan.Jack0Via : plan.Jack1Via;
                        int first = DecoyRunCourse.StartsSkewer(next, s) && want >= 2 ? 2 : 1;
                        for (int j = 0; j < first; j++)
                            Assert.IsTrue(DecoyRunCourse.Threads(m, d0, rings[next + j]), $"I{i} seed {seed}: zip 1 misses ring {next + j}");
                        Vector3 end = d0;
                        if (made > first)
                        {
                            Assert.AreEqual(1, plan.SecondTravel, $"I{i} seed {seed}: no second zip");
                            Vector3 d1 = plan.Jack0Lays == 1 ? plan.Jack0Via : plan.Jack1Via;
                            Assert.IsTrue(DecoyRunCourse.Threads(d0, d1, rings[next + first]), $"I{i} seed {seed}: zip 2 misses ring {next + first}");
                            end = d1;
                        }
                        Assert.Less(run, 6f, $"I{i} seed {seed}: the jacks run {run:F1} s at ring {next}");
                        hull = end;
                        next += made;
                        cycles++;
                    }
                    Assert.AreEqual((rings.Count + s.Group - 1) / s.Group, cycles, $"I{i} seed {seed}: one cycle a group");
                }
            }
        }

        /// <summary>A skewer is a line: the zip from behind its first ring to past its second threads both, and its rings
        /// face down that line (within the skewer's bend), so the line crosses both mouths square.</summary>
        [Test]
        public void Every_skewer_is_one_straight_zip()
        {
            for (int i = 2; i <= 4; i++)
            {
                var s = DecoyRunCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    for (int k = 0; k + 1 < rings.Count; k++)
                    {
                        if (!DecoyRunCourse.StartsSkewer(k, s)) continue;
                        Vector3 line = (rings[k + 1].Position - rings[k].Position).normalized;
                        Assert.Less(Vector3.Angle(line, rings[k].Axis), 0.5f, $"I{i} seed {seed}: skewer {k} faces off its line");
                        Assert.Less(Vector3.Angle(line, rings[k + 1].Axis), 0.5f, $"I{i} seed {seed}: skewer {k}'s second ring faces off its line");
                        if (k > 0) Assert.Greater(Vector3.Angle(rings[k - 1].Axis, rings[k].Axis), s.LinkBendMin - s.SkewerBendMax - 1f,
                            $"I{i} seed {seed}: the turn into skewer {k} is soft");
                    }
                }
            }
        }

        [Test]
        public void Each_level_adds_one_thing()
        {
            var s = new DecoyRunSettings[4];
            for (int i = 0; i < 4; i++) s[i] = DecoyRunCourse.ForIntensity(i + 1);
            Assert.AreEqual(1, s[0].Group); Assert.IsFalse(s[0].TiltEachGroup);
            Assert.AreEqual(2, s[1].Group); Assert.IsFalse(s[1].TiltEachGroup);
            Assert.AreEqual(2, s[2].Group); Assert.IsTrue(s[2].TiltEachGroup);
            Assert.AreEqual(3, s[3].Group); Assert.IsTrue(s[3].TiltEachGroup);
            Assert.AreEqual(5, s[3].StartTimeLevel, "I4 starts with the second decoy");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0, s[i].StartTimeLevel, $"I{i + 1}: one decoy");
                Assert.Greater(s[i].RingRadius, s[i + 1].RingRadius, $"I{i + 2}: the mouths shrink");
                Assert.LessOrEqual(s[i].TiltMax, s[i + 1].TiltMax);
            }
            Assert.AreEqual(0f, s[2].CornerBendMax, "I3 has no corner inside a group");
            Assert.Greater(s[3].CornerBendMin, 120f, "I4's corner is a hairpin: the second zip doubles back");
        }

        [Test]
        public void The_course_is_cut_for_the_shipped_jacks()
        {
            string path = Path.Combine("Assets", "_SO_Assets", "VesselActions", "Jacks", "JacksConfig.asset");
            Assert.IsTrue(File.Exists(path), $"{path} missing");
            string yaml = File.ReadAllText(path);
            float Field(string name)
            {
                var m = Regex.Match(yaml, $@"^  {name}: ([0-9.]+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"{name} not found in JacksConfig.asset");
                return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Assert.AreEqual(DecoyRunCourse.JackSpeed, Field("dogSpeed") * Field("labScale"), 0.01f, "dogSpeed x labScale");
            Assert.AreEqual(DecoyRunCourse.TravelSpeed, Field("travelSpeed"), 0.01f, "travelSpeed");
            Assert.AreEqual(DecoyRunCourse.TravelMinSeconds, Field("travelMinSeconds"), 0.001f, "travelMinSeconds");
            Assert.AreEqual(DecoyRunCourse.TravelMaxSeconds, Field("travelMaxSeconds"), 0.001f, "travelMaxSeconds");
            Assert.AreEqual(DecoyRunCourse.TravelGuard, Field("teleportGuard"), 0.001f, "teleportGuard");
        }

    }
}
