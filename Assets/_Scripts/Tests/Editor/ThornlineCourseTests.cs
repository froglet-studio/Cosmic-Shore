using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Thornline, the Shrike's Time race (THORNLINE.md): every seed at every intensity walks a full chain, every peer
    /// rebuilds the thorns from the broadcast rings, no line between two thorns skips a ring by accident, every snipe
    /// threads both its rings and clears its near thorn, each level adds one thing, and the course's Shrike numbers are
    /// the shipped ShrikeConfig.asset's. Runs offline too: Tools/Build/thornline_course_harness/run.sh.
    /// </summary>
    public class ThornlineCourseTests
    {
        const int Seeds = 400;

        static List<RaceGate> Course(int intensity, int seed, List<Vector3> thorns) =>
            ThornlineCourse.Generate(seed, ThornlineCourse.ForIntensity(intensity), thorns);

        [Test]
        public void Every_seed_walks_a_full_valid_chain()
        {
            var thorns = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
            {
                var s = ThornlineCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed, thorns);
                    Assert.IsNotNull(rings, $"I{i} seed {seed}: no chain");
                    Assert.AreEqual(s.RingCount, rings.Count, $"I{i} seed {seed}: ring count");
                    Assert.AreEqual(s.RingCount + 1, thorns.Count, $"I{i} seed {seed}: thorn count");
                    Assert.IsTrue(ThornlineCourse.Valid(s, thorns, out string why), $"I{i} seed {seed}: {why}");
                }
            }
        }

        [Test]
        public void Every_peer_rebuilds_the_thorns_from_the_rings()
        {
            var thorns = new List<Vector3>();
            var rebuilt = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed, thorns);
                    // The broadcast moves the course to the cell: the rebuild must not care where the cell is.
                    var offset = new Vector3(1234f, -567f, 89f);
                    var moved = new List<RaceGate>();
                    foreach (var g in rings) moved.Add(new RaceGate(g.Position + offset, g.Axis, g.Radius));
                    ThornlineCourse.ThornsFor(moved, rebuilt);
                    Assert.AreEqual(thorns.Count, rebuilt.Count);
                    for (int k = 0; k < thorns.Count; k++)
                        Assert.Less((rebuilt[k] - offset - thorns[k]).magnitude, 0.05f, $"I{i} seed {seed}: thorn {k}");
                }
        }

        [Test]
        public void Every_leg_is_one_sling_inside_the_shell()
        {
            var thorns = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    Course(i, seed, thorns);
                    for (int k = 0; k < thorns.Count; k++)
                    {
                        float r = thorns[k].magnitude;
                        Assert.IsTrue(r >= 650f && r <= 1050f, $"I{i} seed {seed}: thorn {k} at r {r:F0}");
                        if (k == 0) continue;
                        float leg = (thorns[k] - thorns[k - 1]).magnitude;
                        Assert.LessOrEqual(leg, 1500f, $"I{i} seed {seed}: leg {k} is {leg:F0} u (sling range 1600)");
                        Assert.GreaterOrEqual(ThornlineCourse.DistanceToSegment(Vector3.zero, thorns[k - 1], thorns[k]), 540f,
                            $"I{i} seed {seed}: leg {k} grazes the nucleus");
                    }
                }
        }

        [Test]
        public void No_leg_docks_on_a_stranger()
        {
            // A sling docks on the FIRST block it touches, so a leg must clear every thorn but its own two.
            var thorns = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
            {
                float clear = ThornlineCourse.HalfDiagonal(ThornlineCourse.ForIntensity(i).ThornSize) + 3f + 10f;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    Course(i, seed, thorns);
                    for (int k = 1; k < thorns.Count; k++)
                        for (int o = 0; o < thorns.Count; o++)
                            if (o != k && o != k - 1)
                                Assert.GreaterOrEqual(ThornlineCourse.DistanceToSegment(thorns[o], thorns[k - 1], thorns[k]), clear,
                                    $"I{i} seed {seed}: leg {k} passes thorn {o}");
                }
            }
        }

        [Test]
        public void No_ring_is_skipped_by_accident_and_every_snipe_threads_both()
        {
            var thorns = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
            {
                var s = ThornlineCourse.ForIntensity(i);
                int snipes = 0;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var rings = Course(i, seed, thorns);
                    for (int a = 0; a < thorns.Count; a++)
                        for (int c = a + 2; c < thorns.Count; c++)
                        {
                            if ((thorns[c] - thorns[a]).magnitude > ThornlineCourse.SlingRange) continue;
                            float worst = 0f;
                            for (int k = a + 1; k <= c; k++)
                                worst = Mathf.Max(worst, ThornlineCourse.DistanceToSegment(rings[k - 1].Position, thorns[a], thorns[c]));
                            bool snipe = c - a == 2 && ThornlineCourse.IsSkipBend(a + 1, s.RingCount, s.SkipBends);
                            if (snipe)
                            {
                                snipes++;
                                Assert.LessOrEqual(worst, s.RingRadius - 10f, $"I{i} seed {seed}: snipe {a}->{c} misses a ring");
                                Assert.GreaterOrEqual(ThornlineCourse.DistanceToSegment(thorns[a + 1], thorns[a], thorns[c]),
                                    ThornlineCourse.HalfDiagonal(s.ThornSize) + 3f + 10f, $"I{i} seed {seed}: snipe {a}->{c} docks on its near thorn");
                            }
                            else
                            {
                                Assert.GreaterOrEqual(worst, s.RingRadius + 10f, $"I{i} seed {seed}: {a}->{c} skips by accident");
                            }
                        }
                }
                Assert.AreEqual(s.SkipBends * Seeds, snipes, $"I{i}: every designated snipe is in range");
            }
        }

        [Test]
        public void Each_level_adds_one_thing()
        {
            var s1 = ThornlineCourse.ForIntensity(1);
            var s2 = ThornlineCourse.ForIntensity(2);
            var s3 = ThornlineCourse.ForIntensity(3);
            var s4 = ThornlineCourse.ForIntensity(4);
            // I1: the sling. Every thorn the fast x2.5 reload, nothing else.
            Assert.AreEqual(0, s1.PlainEvery);
            Assert.AreEqual(0, s1.Crystals);
            Assert.AreEqual(0, s1.SkipBends);
            // I2: the reload. Plain thorns, and the rim crystals that pay for them.
            Assert.Greater(s2.PlainEvery, 0);
            Assert.Greater(s2.Crystals, 0);
            Assert.AreEqual(0, s2.SkipBends);
            // I3: the snipe.
            Assert.Greater(s3.SkipBends, 0);
            // I4: the larder. Every thorn plain.
            Assert.AreEqual(1, s4.PlainEvery);
            var all = new[] { s1, s2, s3, s4 };
            for (int i = 1; i < 4; i++)
            {
                Assert.LessOrEqual(all[i].ThornSize, all[i - 1].ThornSize, $"I{i + 1}: thorns never grow");
                Assert.LessOrEqual(all[i].RingRadius, all[i - 1].RingRadius, $"I{i + 1}: rings never grow");
                Assert.GreaterOrEqual(all[i].MaxLeg, all[i - 1].MaxLeg, $"I{i + 1}: legs never shorten");
            }
        }

        [Test]
        public void A_snipe_is_worth_taking_and_a_crystal_is_worth_aiming_for()
        {
            // At Time 0: a docked leg on plain mass costs 1 + 5/1.6 s, so a snipe saves a whole leg's reload.
            float cd = ThornlineCourse.CooldownAtTime(0f);
            float plain = ThornlineCourse.LegSeconds(cd, false);
            float shielded = ThornlineCourse.LegSeconds(cd, true);
            Assert.Greater(plain - shielded, 0.9f, "super-shielded thorns must reload clearly faster");
            Assert.Greater(plain, 3.5f, "a plain reload must be worth skipping");
            // A crystal resets the sling: the leg after it costs the one-second sling only.
            Assert.Greater(plain - ThornlineCourse.SlingSeconds, 2.5f);
        }

        [Test]
        public void Crystals_sit_on_the_rim_line_not_the_centre_line()
        {
            var thorns = new List<Vector3>();
            var crystals = new List<Vector3>();
            for (int i = 2; i <= 4; i++)
            {
                var s = ThornlineCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    Course(i, seed, thorns);
                    ThornlineCourse.PlaceCrystals(thorns, s, crystals);
                    Assert.AreEqual(s.Crystals, crystals.Count, $"I{i} seed {seed}: crystal count");
                    foreach (var c in crystals)
                    {
                        float best = float.MaxValue;
                        for (int k = 1; k < thorns.Count; k++)
                            best = Mathf.Min(best, ThornlineCourse.DistanceToSegment(c, thorns[k - 1], thorns[k]));
                        // Aimed at a thorn's centre you pass 13-17 u wide of it; aimed at its rim you fly through it.
                        Assert.IsTrue(best >= 0.3f * 0.5f * s.ThornSize && best <= s.ThornSize,
                            $"I{i} seed {seed}: a crystal {best:F1} u off the nearest centre line");
                    }
                }
            }
        }

        [Test]
        public void Courses_are_a_function_of_the_seed()
        {
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= 50; seed++)
                {
                    Course(i, seed, a);
                    Course(i, seed, b);
                    for (int k = 0; k < a.Count; k++) Assert.AreEqual(a[k], b[k]);
                }
        }

        [Test]
        public void Course_constants_match_the_shipped_Shrike()
        {
            string path = Path.Combine("Assets", "_SO_Assets", "VesselActions", "Shrike", "ShrikeConfig.asset");
            Assert.IsTrue(File.Exists(path), $"{path} not found (run from the project root)");
            string yaml = File.ReadAllText(path);
            float Field(string name)
            {
                var m = Regex.Match(yaml, $@"^  {name}: ([0-9.]+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"ShrikeConfig.asset has no '{name}'");
                return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            Assert.AreEqual(ThornlineCourse.SlingRange, Field("slingRange"), "slingRange moved");
            Assert.AreEqual(ThornlineCourse.SlingSeconds, Field("slingSeconds"), "slingSeconds moved");
            Assert.AreEqual(ThornlineCourse.DockBonus, Field("dockBonus"), 1e-4f, "dockBonus moved");
            Assert.AreEqual(ThornlineCourse.DockBonusShielded, Field("dockBonusShielded"), 1e-4f, "dockBonusShielded moved");
            Assert.AreEqual(ThornlineCourse.HullRadius, Field("hullRadius"), 1e-4f, "hullRadius moved");
            var cd = Regex.Match(yaml, @"slingCooldown:\s*\n(?:\s+\w+: [^\n]*\n)*?\s+Min: ([0-9.]+)\s*\n\s+Max: ([0-9.]+)");
            Assert.IsTrue(cd.Success, "slingCooldown's Min/Max not found");
            Assert.AreEqual(ThornlineCourse.SlingCooldownTimeZero, float.Parse(cd.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual(ThornlineCourse.SlingCooldownTimeTen, float.Parse(cd.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        [Test]
        public void Thorn_kinds_follow_the_ladder()
        {
            var thorns = new List<Vector3>();
            var laid = new List<Thorn>();
            for (int i = 1; i <= 4; i++)
            {
                var s = ThornlineCourse.ForIntensity(i);
                Course(i, 7, thorns);
                ThornlineCourse.Lay(thorns, s, laid);
                Assert.AreEqual(PrismKind.SuperShielded, laid[0].Kind, "the start thorn is always the fast reload");
                int plain = 0;
                for (int k = 1; k < laid.Count; k++) if (laid[k].Kind == PrismKind.Plain) plain++;
                int expect = s.PlainEvery == 0 ? 0 : s.PlainEvery == 1 ? laid.Count - 1 : (laid.Count - 1) / s.PlainEvery;
                Assert.AreEqual(expect, plain, $"I{i}: plain thorns");
            }
        }
    }
}
