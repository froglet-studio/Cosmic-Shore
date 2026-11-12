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
    /// Breach, the Orca's Time race (BREACH.md): every seed at every intensity walks a full valid course, every pilot starts
    /// the same distance behind ring 0, every peer lays the same walls from the rings, every wall shuts its ring's line
    /// and is rounded on the inside of the turn, the dog-leg round a wall is a cruise turn and not a burning one, the burn
    /// coasts hard turns and walls, each level adds one thing, and the course's Orca numbers are Orca.prefab's,
    /// OrcaCrewConfig.asset's and the danger slow's. Runs offline too: Tools/Build/breach_harness/run.sh.
    /// </summary>
    public class BreachCourseTests
    {
        const int Seeds = 400;

        static List<RaceGate> Course(int intensity, int seed) => BreachCourse.Generate(seed, BreachCourse.ForIntensity(intensity));

        static List<(Vector3 position, int ring)> WallOf(List<RaceGate> rings, int intensity)
        {
            var wall = new List<(Vector3, int)>();
            BreachCourse.WallsFor(rings, BreachCourse.ForIntensity(intensity), wall);
            return wall;
        }

        [Test]
        public void Every_seed_walks_a_full_valid_course()
        {
            for (int i = 1; i <= 4; i++)
            {
                var s = BreachCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    Assert.IsNotNull(rings, $"I{i} seed {seed}: no course");
                    Assert.IsTrue(BreachCourse.Valid(s, rings, out string why), $"I{i} seed {seed}: {why}");
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
                foreach (var p in BreachCourse.StartPoses(count))
                {
                    Assert.AreEqual(-BreachCourse.StartStandoff, Vector3.Dot(p.position - BreachCourse.StartRing, BreachCourse.StartAxis), 0.01f);
                    Assert.Less(Vector3.Angle(p.rotation * Vector3.forward, BreachCourse.StartAxis), 0.01f);
                }
        }

        /// <summary>The walls come from the broadcast rings alone, so a peer whose cell sits elsewhere lays the same walls.</summary>
        [Test]
        public void Every_peer_lays_the_same_walls_wherever_the_cell_is()
        {
            var offset = new Vector3(3000f, -1200f, 450f);
            for (int i = 3; i <= 4; i++)
                for (int seed = 1; seed <= 60; seed++)
                {
                    var rings = Course(i, seed);
                    var moved = new List<RaceGate>();
                    foreach (var g in rings) moved.Add(new RaceGate(g.Position + offset, g.Axis, g.Radius));
                    var a = WallOf(rings, i);
                    var b = WallOf(moved, i);
                    Assert.AreEqual(a.Count, b.Count, $"I{i} seed {seed}: wall count");
                    for (int w = 0; w < a.Count; w++)
                    {
                        Assert.AreEqual(a[w].ring, b[w].ring, $"I{i} seed {seed}: prism {w}'s ring");
                        Assert.Less((a[w].position + offset - b[w].position).magnitude, 0.01f, $"I{i} seed {seed}: prism {w} moved");
                    }
                }
        }

        /// <summary>Every wall shuts its ring's line: it stands square across the ring's axis, wider than the ring, with no
        /// hole a hull fits through; the line round it passes clear of its edge.</summary>
        [Test]
        public void Every_wall_shuts_the_line_into_its_ring()
        {
            for (int i = 3; i <= 4; i++)
            {
                var s = BreachCourse.ForIntensity(i);
                for (int seed = 1; seed <= 60; seed++)
                {
                    var rings = Course(i, seed);
                    var wall = WallOf(rings, i);
                    int walled = 0;
                    for (int k = 1; k < rings.Count; k++)
                    {
                        if (!BreachCourse.IsWalled(k, s)) continue;
                        walled++;
                        var g = rings[k];
                        Vector3 c = BreachCourse.WallCentre(rings, k, s);
                        float wr = BreachCourse.WallRadius(g, s);
                        Assert.Greater(wr, g.Radius, $"I{i} seed {seed}: wall {k} is no wider than its ring");
                        Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(g.Axis, Vector3.up), Vector3.forward);
                        Vector3 e2 = Vector3.Cross(g.Axis, e1);
                        foreach (var w in wall)
                            if (w.ring == k) Assert.Less(Mathf.Abs(Vector3.Dot(w.position - c, g.Axis)), 0.01f, $"I{i} seed {seed}: wall {k} is not square to its ring");
                        float reach = BreachCourse.WallSpacing * 0.6f;
                        for (int a = 0; a < 36; a++)
                            for (float r = 0f; r < wr - 4f; r += 7f)
                            {
                                Vector3 pt = c + (e1 * Mathf.Cos(a * 10f * Mathf.Deg2Rad) + e2 * Mathf.Sin(a * 10f * Mathf.Deg2Rad)) * r;
                                bool covered = false;
                                foreach (var w in wall) if (w.ring == k && (w.position - pt).magnitude <= reach) { covered = true; break; }
                                Assert.IsTrue(covered, $"I{i} seed {seed}: wall {k} is open at {r:F0} u, {a * 10} degrees");
                            }
                        Vector3 round = BreachCourse.RoundPoint(rings, k, s);
                        foreach (var w in wall)
                            if (w.ring == k) Assert.Greater((w.position - round).magnitude, BreachCourse.WallClear - BreachCourse.WallPrismSize * 0.5f,
                                $"I{i} seed {seed}: the line round wall {k} touches it");
                    }
                    Assert.GreaterOrEqual(walled, 4, $"I{i} seed {seed}: only {walled} walls");
                }
            }
        }

        /// <summary>The line rounds every wall on the inside of the turn into its leg (away from where the leg before
        /// carried the hull, the side a turn swings out to).</summary>
        [Test]
        public void Every_wall_is_rounded_on_the_inside_of_the_turn()
        {
            for (int i = 3; i <= 4; i++)
            {
                var s = BreachCourse.ForIntensity(i);
                for (int seed = 1; seed <= 100; seed++)
                {
                    var rings = Course(i, seed);
                    for (int k = 1; k < rings.Count; k++)
                    {
                        if (!BreachCourse.IsWalled(k, s)) continue;
                        Vector3 side = BreachCourse.RoundSide(rings, k);
                        Assert.Less(Mathf.Abs(Vector3.Dot(side, rings[k].Axis)), 1e-3f, $"I{i} seed {seed}: wall {k}'s side is not in its plane");
                        Assert.LessOrEqual(Vector3.Dot(side, rings[k - 1].Axis), 1e-3f, $"I{i} seed {seed}: wall {k} is rounded on the outside");
                    }
                }
            }
        }

        /// <summary>The dog-leg back from a wall's edge into its ring is a turn the hull makes at cruise and cannot make
        /// burning, a reaction after it passes the wall.</summary>
        [Test]
        public void Every_wall_is_a_cruise_turn_and_not_a_burning_one()
        {
            for (int i = 3; i <= 4; i++)
            {
                var s = BreachCourse.ForIntensity(i);
                float need = s.WallMargin + BreachCourse.WallClear + 4f;   // from the round point to 4 u inside the mouth
                float cruise = BreachCourse.ArcReach(BreachCourse.CruiseSpeed, s.WallAhead - BreachCourse.CruiseSpeed * BreachCourse.ReactionSeconds);
                float burn = BreachCourse.ArcReach(BreachCourse.BoostSpeed, s.WallAhead - BreachCourse.BoostSpeed * BreachCourse.ReactionSeconds);
                Assert.LessOrEqual(need, cruise, $"I{i}: at cruise the hull cannot swing back in ({need:F0} u needed, {cruise:F0} reachable)");
                Assert.Greater(need, burn, $"I{i}: burning, the hull still swings back in ({need:F0} u needed, {burn:F0} reachable)");
            }
        }

        /// <summary>The burn: never into a hard turn's last stretch, never off the aim point, never round a wall, never
        /// started on a near-empty tank; on a straight with the nose on the aim point, it burns.</summary>
        [Test]
        public void The_burn_coasts_hard_turns_and_walls()
        {
            int hard = 0, gaps = 0;
            for (int i = 1; i <= 4; i++)
            {
                var s = BreachCourse.ForIntensity(i);
                for (int seed = 1; seed <= 60; seed++)
                {
                    var rings = Course(i, seed);
                    for (int k = 1; k < rings.Count; k++)
                    {
                        Vector3 axis = rings[k].Axis;
                        Vector3 aim = BreachCourse.IsWalled(k, s) ? BreachCourse.RoundPoint(rings, k, s) + axis * 0f : rings[k].Position;
                        // far out on the line into the aim point, nose on it: burn
                        Assert.IsTrue(BreachCourse.BurnWanted(aim - axis * 260f, axis, rings, k, s), $"I{i} seed {seed}: no burn on the straight into {k}");
                        // no burn with too little in the tank to start one
                        Assert.IsFalse(BreachCourse.BurnWanted(aim - axis * 260f, axis, rings, k, s, BreachCourse.MinBurnStart * 0.5f, false),
                            $"I{i} seed {seed}: a burn started on an empty tank");
                        // nose 20 degrees off: no burn
                        Vector3 side = RaceCourseGeometry.SafeNormalize(Vector3.Cross(axis, Vector3.up), Vector3.forward);
                        Assert.IsFalse(BreachCourse.BurnWanted(aim - axis * 260f, RaceCourseGeometry.RotateAbout(axis, side, 20f * Mathf.Deg2Rad), rings, k, s),
                            $"I{i} seed {seed}: a burn off the aim point");
                        if (BreachCourse.TurnAfter(rings, k) > BreachCourse.HardTurnDeg)
                        {
                            hard++;
                            Assert.IsFalse(BreachCourse.BurnWanted(aim - axis * (BreachCourse.HardTurnStop - 20f), axis, rings, k, s),
                                $"I{i} seed {seed}: a burn into the hard turn at {k}");
                        }
                        if (BreachCourse.IsWalled(k, s))
                        {
                            gaps++;
                            // near the wall, and past it: coast
                            Assert.IsFalse(BreachCourse.BurnWanted(aim - axis * (BreachCourse.WallStop - 20f), axis, rings, k, s), $"I{i} seed {seed}: a burn at wall {k}");
                            Vector3 past = BreachCourse.WallCentre(rings, k, s) + axis * 20f;
                            Assert.IsFalse(BreachCourse.BurnWanted(past, (rings[k].Position - past).normalized, rings, k, s), $"I{i} seed {seed}: a burn round wall {k}");
                        }
                    }
                }
            }
            Assert.Greater(hard, 100, "too few hard turns sampled");
            Assert.Greater(gaps, 100, "too few walls sampled");
        }

        [Test]
        public void Each_level_adds_one_thing()
        {
            var s1 = BreachCourse.ForIntensity(1);
            var s2 = BreachCourse.ForIntensity(2);
            var s3 = BreachCourse.ForIntensity(3);
            var s4 = BreachCourse.ForIntensity(4);
            Assert.AreEqual(0, s1.SharpEvery, "I1 has hard turns");
            Assert.AreEqual(0, s1.WallEvery, "I1 has walls");
            Assert.Greater(s2.SharpEvery, 0, "I2 adds hard turns");
            Assert.AreEqual(0, s2.WallEvery, "I2 has walls");
            Assert.Greater(s3.WallEvery, 0, "I3 adds walls");
            Assert.Greater(s4.SharpEvery, 0, "I4 keeps the hard turns");
            Assert.Greater(s4.WallEvery, 0, "I4 keeps the walls");
            Assert.Greater(s4.WallMargin, s3.WallMargin, "I4's walls are not wider");
            Assert.Less(s4.WallAhead, s3.WallAhead, "I4's walls are not nearer their rings");
            Assert.Less(s2.RingRadius, s1.RingRadius);
            Assert.Less(s3.RingRadius, s2.RingRadius);
            Assert.Less(s4.RingRadius, s3.RingRadius);
            // at I4 the hard turns and the walls take turns: a wall is never met coming out of a hard turn, and is left on an ordinary leg
            int both = 0;
            for (int k = 1; k < 16; k++)
            {
                if (BreachCourse.IsWalled(k, s4)) Assert.IsFalse(BreachCourse.IsSharp(k, s4) || BreachCourse.IsSharp(k + 1, s4), $"I4 wall {k} meets a hard turn");
                if (BreachCourse.IsWalled(k, s4) && BreachCourse.IsSharp(k - 1, s4)) both++;
            }
            Assert.GreaterOrEqual(both, 4, "I4's walls do not follow its hard turns' rings");
        }

        [Test]
        public void The_course_is_cut_for_the_shipped_orca()
        {
            string hull = File.ReadAllText(Path.Combine("Assets", "_Prefabs", "Spacevessels", "Orca.prefab"));
            string cfg = File.ReadAllText(Path.Combine("Assets", "_SO_Assets", "VesselActions", "OrcaCrew", "OrcaCrewConfig.asset"));
            string slow = File.ReadAllText(Path.Combine("Assets", "_SO_Assets", "Effects", "Vessel Prism Effects", "SparrowVesselChangeSpeedByPrism.asset"));
            float Field(string yaml, string name)
            {
                var m = Regex.Match(yaml, $@"^  {name}: ([0-9.]+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"{name} not found");
                return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Assert.AreEqual(BreachCourse.MinimumSpeed, Field(hull, "DefaultMinimumSpeed"), 0.01f, "DefaultMinimumSpeed");
            Assert.AreEqual(BreachCourse.ThrottleScaler, Field(hull, "DefaultThrottleScaler"), 0.01f, "DefaultThrottleScaler");
            Assert.AreEqual(BreachCourse.BoostMultiplier, Field(hull, "boostMultiplier"), 0.01f, "boostMultiplier");
            Assert.AreEqual(BreachCourse.TurnScaler, Field(hull, "YawScaler"), 0.01f, "YawScaler");
            Assert.AreEqual(BreachCourse.TurnScaler, Field(hull, "PitchScaler"), 0.01f, "PitchScaler");
            Assert.AreEqual(BreachCourse.RotationThrottleScaler, Field(hull, "RotationThrottleScaler"), 0.0001f, "RotationThrottleScaler");
            Assert.AreEqual(BreachCourse.BoostTank, Field(cfg, "boostTank"), 0.01f, "boostTank");
            var refill = Regex.Match(cfg, @"^  boostRefill:\s*\n(?:    .*\n)*?    Value: ([0-9.]+)", RegexOptions.Multiline);
            Assert.IsTrue(refill.Success, "boostRefill not found");
            Assert.AreEqual(BreachCourse.BoostRefill, float.Parse(refill.Groups[1].Value, CultureInfo.InvariantCulture), 0.001f, "boostRefill");
            Assert.GreaterOrEqual(Field(slow, "maxSlowStrength") * Field(slow, "dangerSlowMultiplier"), 1f, "a danger prism no longer stops the hull");
            Assert.AreEqual(BreachCourse.DangerRecovery, Field(slow, "speedModifierDuration") * Field(slow, "dangerSlowDurationMultiplier"), 0.01f, "the danger slow's recovery");
        }
    }
}
