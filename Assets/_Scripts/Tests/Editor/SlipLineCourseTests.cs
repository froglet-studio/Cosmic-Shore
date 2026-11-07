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
    /// Slip Line, the Honeyguide's Time race (SLIP_LINE.md): every seed at every intensity walks a full valid course, every
    /// pilot starts the same distance behind ring 0, every peer lays the same rails from the rings, every rail is a
    /// slip-able trail that threads its ring inside one slip, the line flown ring to ring never touches a rail, a long rail
    /// is worth a charge and a short one is worth less, a chain is one ride for two rings, each level adds one thing, and
    /// the course's Honeyguide numbers are HoneyguideConfig.asset's and Honeyguide.prefab's. Runs offline too:
    /// Tools/Build/slip_line_harness/run.sh.
    /// </summary>
    public class SlipLineCourseTests
    {
        const int Seeds = 400;

        static List<RaceGate> Course(int intensity, int seed) => SlipLineCourse.Generate(seed, SlipLineCourse.ForIntensity(intensity));

        static List<Rail> RailsOf(List<RaceGate> rings, int intensity)
        {
            var rails = new List<Rail>();
            SlipLineCourse.RailsFor(rings, SlipLineCourse.ForIntensity(intensity), rails);
            return rails;
        }

        [Test]
        public void Every_seed_walks_a_full_valid_course()
        {
            for (int i = 1; i <= 4; i++)
            {
                var s = SlipLineCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    Assert.IsNotNull(rings, $"I{i} seed {seed}: no course");
                    Assert.IsTrue(SlipLineCourse.Valid(s, rings, out string why), $"I{i} seed {seed}: {why}");
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
                foreach (var p in SlipLineCourse.StartPoses(count))
                {
                    Assert.AreEqual(-SlipLineCourse.StartStandoff, Vector3.Dot(p.position - SlipLineCourse.StartRing, SlipLineCourse.StartAxis), 0.01f);
                    Assert.Less(Vector3.Angle(p.rotation * Vector3.forward, SlipLineCourse.StartAxis), 0.01f);
                }
        }

        /// <summary>The rails come from the broadcast rings alone, so a peer whose cell sits elsewhere lays the same rails,
        /// colours and lengths included.</summary>
        [Test]
        public void Every_peer_lays_the_same_rails_wherever_the_cell_is()
        {
            var offset = new Vector3(3000f, -1200f, 450f);
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= 60; seed++)
                {
                    var rings = Course(i, seed);
                    var moved = new List<RaceGate>();
                    foreach (var r in rings) moved.Add(new RaceGate(r.Position + offset, r.Axis, r.Radius));
                    var a = RailsOf(rings, i);
                    var b = RailsOf(moved, i);
                    Assert.AreEqual(a.Count, b.Count);
                    for (int k = 0; k < a.Count; k++)
                    {
                        Assert.AreEqual(a[k].Colour, b[k].Colour, $"I{i} seed {seed}: rail {k}'s colour moved");
                        Assert.Less((b[k].Start - offset - a[k].Start).magnitude, 0.05f, $"I{i} seed {seed}: rail {k} moved");
                        Assert.Less((b[k].End - offset - a[k].End).magnitude, 0.05f, $"I{i} seed {seed}: rail {k} moved");
                    }
                }
        }

        /// <summary>Every rail is a trail the slip takes (six prisms or more, of a Dynamo colour), a ride from its start lands
        /// inside one slip's reach, and that ride crosses its ring inside the mouth before it lands.</summary>
        [Test]
        public void Every_rail_is_one_slip_through_its_ring()
        {
            var pts = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    foreach (var rail in RailsOf(rings, i))
                    {
                        SlipLineCourse.PrismsOf(rail, pts);
                        Assert.GreaterOrEqual(pts.Count, SlipLineCourse.MinSlipTrailLength, $"I{i} seed {seed}: rail {rail.Ring} too short a trail");
                        Assert.IsTrue(rail.Colour >= 1 && rail.Colour <= 3, $"I{i} seed {seed}: rail {rail.Ring} has no Dynamo colour");
                        Assert.LessOrEqual((pts[pts.Count - 1] - pts[0]).magnitude, SlipLineCourse.SlipReach, $"I{i} seed {seed}: rail {rail.Ring} outruns a slip");
                        Assert.IsTrue(SlipLineCourse.Threads(pts[0], pts[pts.Count - 1], rings[rail.Ring]), $"I{i} seed {seed}: the ride on rail {rail.Ring} misses its ring");
                        float past = Vector3.Dot(rail.End - rings[rail.Ring].Position, SlipLineCourse.LegDirection(rings, rail.Ring));
                        Assert.Greater(past, 20f, $"I{i} seed {seed}: rail {rail.Ring} lands on its ring, not past it");
                    }
                }
        }

        /// <summary>Flying ring to ring through the centres never slips by accident: every such line clears every rail by
        /// more than the slip's catch.</summary>
        [Test]
        public void The_line_flown_ring_to_ring_never_touches_a_rail()
        {
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= 100; seed++)
                {
                    var rings = Course(i, seed);
                    var rails = RailsOf(rings, i);
                    for (int k = 0; k < rings.Count; k++)
                        foreach (var rail in rails)
                            Assert.Greater(SlipLineCourse.SegmentDistance(SlipLineCourse.LegStart(rings, k), rings[k].Position, rail.Start, rail.End),
                                SlipLineCourse.ContactRadius, $"I{i} seed {seed}: the line into ring {k} touches rail {rail.Ring}");
                }
        }

        /// <summary>A long rail is worth a charge from the ring before; a short one is worth clearly less, so where a charge
        /// goes is a choice.</summary>
        [Test]
        public void A_long_rail_is_worth_a_charge_and_a_short_one_less()
        {
            for (int i = 2; i <= 4; i++)
            {
                float longSum = 0f, shortSum = 0f;
                int longs = 0, shorts = 0;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed);
                    foreach (var rail in RailsOf(rings, i))
                    {
                        if (rail.Chained) continue;
                        Vector3 after = rail.Ring + 1 < rings.Count ? rings[rail.Ring + 1].Position : rings[rail.Ring].Position;
                        float save = SlipLineCourse.Saving(SlipLineCourse.LegStart(rings, rail.Ring), rail, rings[rail.Ring], after);
                        if (rail.Length >= SlipLineCourse.LongMin) { longSum += save; longs++; Assert.Greater(save, SlipLineCourse.MinSaving, $"I{i} seed {seed}: long rail {rail.Ring} is not worth a charge"); }
                        else { shortSum += save; shorts++; }
                    }
                }
                Assert.Greater(shorts, 1000, $"I{i}: too few short rails");
                Assert.Greater(longSum / longs, 2f * (shortSum / shorts), $"I{i}: a long rail is not clearly worth more than a short one");
            }
        }

        /// <summary>A chain is one ride for two rings: the second rail starts within the slip's catch of where the first
        /// ride lands, inside the aim cone of the heading it lands on, and its ride threads the next ring.</summary>
        [Test]
        public void Every_chain_is_one_ride_for_two_rings()
        {
            int chains = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var rings = Course(4, seed);
                var rails = RailsOf(rings, 4);
                for (int a = 1; a < rails.Count; a++)
                {
                    if (!rails[a].Chained) continue;
                    chains++;
                    var first = rails[a - 1];
                    Vector3 land = first.End + first.Direction * SlipLineCourse.ExitOffset;
                    Assert.Less((rails[a].Start - land).magnitude, SlipLineCourse.ContactRadius, $"seed {seed}: chain {rails[a].Ring} starts off the landing");
                    Assert.Less(Vector3.Angle(first.Direction, rails[a].Direction), SlipLineCourse.AimCone, $"seed {seed}: chain {rails[a].Ring} turns out of the aim cone");
                    Assert.AreNotEqual(first.Colour, rails[a].Colour, $"seed {seed}: chain {rails[a].Ring} is one colour");
                    Assert.IsTrue(SlipLineCourse.Threads(rails[a].Start, rails[a].End, rings[rails[a].Ring]), $"seed {seed}: chain {rails[a].Ring} misses its ring");
                }
            }
            Assert.Greater(chains, 400, "too few chains to mean anything");
        }

        [Test]
        public void Each_level_adds_one_thing()
        {
            var s = new SlipLineSettings[4];
            for (int i = 0; i < 4; i++) s[i] = SlipLineCourse.ForIntensity(i + 1);
            Assert.AreEqual(3, s[0].Lanes, "I1: a rail of every colour on every leg");
            Assert.AreEqual(0f, s[0].ShortShare, 0f, "I1: every rail long");
            for (int i = 1; i < 4; i++) Assert.AreEqual(1, s[i].Lanes, $"I{i + 1}: one rail a leg");
            Assert.Greater(s[1].ShortShare, 0f, "I2: short rails");
            Assert.AreEqual(0f, s[1].DetourMax, 0f, "I2: rails on their legs");
            Assert.Greater(s[2].DetourMin, 0f, "I3: rails lean");
            Assert.AreEqual(0, s[2].ChainEvery, "I3: no chains");
            Assert.Greater(s[3].ChainEvery, 0, "I4: chains");
            for (int i = 0; i < 3; i++) Assert.Greater(s[i].RingRadius, s[i + 1].RingRadius, $"I{i + 2}: the mouths shrink");
        }

        [Test]
        public void The_course_is_cut_for_the_shipped_honeyguide()
        {
            string cfg = File.ReadAllText(Path.Combine("Assets", "_SO_Assets", "VesselActions", "Honeyguide", "HoneyguideConfig.asset"));
            string hull = File.ReadAllText(Path.Combine("Assets", "_Prefabs", "Spacevessels", "Honeyguide.prefab"));
            float Field(string yaml, string name)
            {
                var m = Regex.Match(yaml, $@"^  {name}: ([0-9.]+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"{name} not found");
                return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Assert.AreEqual(SlipLineCourse.SlipReach, Field(cfg, "slipReach"), 0.01f, "slipReach");
            Assert.AreEqual(SlipLineCourse.ZipSpeed, Field(cfg, "zipSpeed"), 0.01f, "zipSpeed");
            Assert.AreEqual(SlipLineCourse.ZipRamp, Field(cfg, "zipRamp"), 0.001f, "zipRamp");
            Assert.AreEqual(SlipLineCourse.ExitOffset, Field(cfg, "exitOffset"), 0.01f, "exitOffset");
            Assert.AreEqual(SlipLineCourse.ContactRadius, Field(cfg, "contactRadius"), 0.01f, "contactRadius");
            Assert.AreEqual(SlipLineCourse.AimCone, Field(cfg, "aimCone"), 0.01f, "aimCone");
            Assert.AreEqual(SlipLineCourse.MinSlipTrailLength, Field(cfg, "minSlipTrailLength"), 0.01f, "minSlipTrailLength");
            Assert.AreEqual(SlipLineCourse.CruiseSpeed, Field(hull, "MinimumSpeed") + Field(hull, "ThrottleScaler"), 0.01f, "the airframe's top speed");
        }
    }
}
