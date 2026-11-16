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
                        // Aimed at a thorn's centre you pass clear of it; aimed through it you anchor (the rim mark is
                        // inside the 12 u the sling catches).
                        Assert.IsTrue(best >= ThornlineCourse.CrystalPickupReach + 1f && best <= ThornlineCourse.AnchorReach,
                            $"I{i} seed {seed}: a crystal {best:F1} u off the nearest centre line");
                    }
                }
            }
        }

        [Test]
        public void The_shot_through_a_crystal_anchors_and_the_centre_shot_misses_it()
        {
            // 2026-11-17: the rim mark sat 0.375 x the thorn's side off its centre (17 / 15 u at I2 / I3), outside the 12 u
            // the sling catches, so the shot the crystal asks for anchored on nothing. Now it is inside, and it still
            // anchors from where a pilot really sits: on the last thorn's FACE, up to ~30 u off its centre.
            var thorns = new List<Vector3>();
            var crystals = new List<Vector3>();
            for (int i = 2; i <= 4; i++)
            {
                var s = ThornlineCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    Course(i, seed, thorns);
                    ThornlineCourse.PlaceCrystals(thorns, s, crystals);
                    foreach (var c in crystals)
                    {
                        int leg = NearestLeg(thorns, c);
                        Vector3 a = thorns[leg - 1], b = thorns[leg];
                        Vector3 rim = ThornlineCourse.RimMark(thorns, leg);
                        Assert.GreaterOrEqual(ThornlineCourse.AnchorAlong(a, rim - a, b, s.ThornSize), 0f,
                            $"I{i} seed {seed}: the shot through the crystal does not anchor on thorn {leg}");
                        Assert.LessOrEqual(ThornlineCourse.DistanceToSegment(c, a, rim), ThornlineCourse.CrystalPickupReach,
                            $"I{i} seed {seed}: the rim shot misses its crystal");
                        Assert.Greater(ThornlineCourse.DistanceToSegment(c, a, b), ThornlineCourse.CrystalPickupReach,
                            $"I{i} seed {seed}: the centre shot takes the crystal");
                        // From the face: four seats round the departure thorn, half its side plus the hull off its centre.
                        Vector3 along = (b - a).normalized;
                        Vector3 u = RaceCourseGeometry.SafeNormalize(RaceCourseGeometry.Perpendicular(along), Vector3.right);
                        Vector3 v = Vector3.Cross(along, u);
                        float off = 0.5f * s.ThornSize + ThornlineCourse.HullRadius;
                        foreach (var seat in new[] { u, -u, v, -v })
                            Assert.IsTrue(ThornlineAIPlan.TryCrystalShot(a + seat * off, c, b, out _),
                                $"I{i} seed {seed}: from a face {off:F0} u off thorn {leg - 1}, the shot through the crystal misses thorn {leg}");
                    }
                }
            }
        }

        static int NearestLeg(List<Vector3> thorns, Vector3 p)
        {
            int best = 1; float bd = float.MaxValue;
            for (int j = 1; j < thorns.Count; j++)
            {
                float d = ThornlineCourse.DistanceToSegment(p, thorns[j - 1], thorns[j]);
                if (d < bd) { bd = d; best = j; }
            }
            return best;
        }

        [Test]
        public void Every_lane_anchors_and_keeps_the_target_off_the_lance_line()
        {
            var thorns = new List<Vector3>();
            float count = ThornlineCourse.LanceRadiusMin + ThornlineCourse.LanceCountPad;
            Assert.Greater(ThornlineCourse.LaneOffset, count, "a lane must clear the lance count at Charge 0");
            Assert.Less(ThornlineCourse.LaneOffset, ThornlineCourse.AnchorReach - 3f, "a lane must anchor with room");
            Assert.Less(ThornlineCourse.LaneOffset, ThornlineCourse.AccidentMargin, "a lane must not open an accidental skip");
            for (int i = 1; i <= 4; i++)
            {
                var s = ThornlineCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    Course(i, seed, thorns);
                    for (int k = 1; k < thorns.Count; k++)
                    {
                        int clear = 0;
                        for (int lane = 0; lane < ThornlineCourse.LaneCount; lane++)
                        {
                            Vector3 p = ThornlineCourse.LanePoint(thorns[k - 1], thorns[k], lane);
                            Assert.AreEqual(ThornlineCourse.LaneOffset, (p - thorns[k]).magnitude, 0.01f);
                            Assert.GreaterOrEqual(ThornlineCourse.AnchorAlong(thorns[k - 1], p - thorns[k - 1], thorns[k], s.ThornSize), 0f,
                                $"I{i} seed {seed}: lane {lane} of thorn {k} does not anchor");
                            Assert.Greater(ThornlineCourse.DistanceToSegment(thorns[k], thorns[k - 1], thorns[k - 1] + (p - thorns[k - 1]).normalized * 2400f), count,
                                $"I{i} seed {seed}: lane {lane} of thorn {k} counts its own target toward the lance");
                            if (ThornlineCourse.LaneClear(thorns[k - 1], p, thorns, k, null)) clear++;
                        }
                        Assert.GreaterOrEqual(clear, ThornlineCourse.LaneCount / 2, $"I{i} seed {seed}: thorn {k} has {clear} clear lanes");
                    }
                }
            }
        }

        [Test]
        public void Every_seat_is_the_same_sling_from_the_start_thorn_and_its_line_is_clear()
        {
            for (int count = 1; count <= 8; count++)
            {
                var poses = ThornlineCourse.StartPoses(count);
                Assert.AreEqual(count, poses.Length);
                foreach (var pose in poses)
                {
                    Vector3 to = ThornlineCourse.StartThorn - pose.position;
                    Assert.AreEqual(ThornlineCourse.StartSeatDistance, to.magnitude, 0.01f, "every seat is one distance from the start thorn");
                    Assert.Less(Vector3.Angle(pose.rotation * Vector3.forward, to), 0.1f, "every seat faces the start thorn");
                    Assert.Less(to.magnitude, ThornlineCourse.SlingRange, "the start thorn is in sling range");
                    Assert.Greater(to.magnitude, ThornlineCourse.AiSlingMinDistance, "an AI can sling to it on the gun");
                    Assert.Greater(ThornlineCourse.DistanceToSegment(Vector3.zero, pose.position, ThornlineCourse.StartThorn),
                        ThornlineCourse.DefaultInnerRadius + ThornlineCourse.CoreMargin, "a seat's line grazes the nucleus");
                }
            }
            var thorns = new List<Vector3>();
            for (int i = 1; i <= 4; i++)
            {
                var s = ThornlineCourse.ForIntensity(i);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    Course(i, seed, thorns);
                    Assert.IsTrue(ThornlineCourse.SeatsClear(s, thorns, out string why), $"I{i} seed {seed}: {why}");
                }
            }
        }

        [Test]
        public void A_broken_thorn_waits_only_for_a_hull_inside_it()
        {
            var c = new Vector3(100f, 200f, 300f);
            const float size = 40f;
            Vector3 face = c + Vector3.right * (size * 0.5f + ThornlineCourse.HullRadius + 2f);
            Assert.IsFalse(ThornlineCourse.RegrowBlocked(c, size, face), "a hull hanging off the face holds the regrow");
            Assert.IsTrue(ThornlineCourse.RegrowBlocked(c, size, c + Vector3.right * (size * 0.5f - 1f)), "a hull inside the box lets it grow through");
            // The sphere it replaced (size + 30 u from the centre) held the regrow off for a hull at the face.
            Assert.Less((face - c).magnitude, size + 30f);
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
            Assert.AreEqual(ThornlineCourse.AnchorReach, Field("dockQueryReach"), 1e-4f, "dockQueryReach moved: the anchor disc, the lanes and the rim mark are cut for it");
            Assert.AreEqual(ThornlineCourse.SlingRangeOfLance, Field("lanceRange"), 1e-4f, "lanceRange moved: the lane check reads the autopilot's lance line");
            Assert.AreEqual(ThornlineCourse.AiSlingMinDistance, Field("aiSlingMinDistance"), 1e-4f, "aiSlingMinDistance moved");
            Assert.AreEqual(ThornlineCourse.AiSlingDegrees, Field("aiSlingDegrees"), 1e-4f, "aiSlingDegrees moved");
            Assert.AreEqual(ThornlineCourse.AiDockSeconds, Field("aiDockSeconds"), 1e-4f, "aiDockSeconds moved");
            var lance = Regex.Match(yaml, @"lanceRadius:\s*\n(?:\s+\w+: [^\n]*\n)*?\s+Min: ([0-9.]+)\s*\n\s+Max: ([0-9.]+)");
            Assert.IsTrue(lance.Success, "lanceRadius's Min/Max not found");
            Assert.AreEqual(ThornlineCourse.LanceRadiusMin, float.Parse(lance.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), "lanceRadius Min moved");
            Assert.AreEqual(ThornlineCourse.LanceRadiusMax, float.Parse(lance.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), "lanceRadius Max moved");
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
