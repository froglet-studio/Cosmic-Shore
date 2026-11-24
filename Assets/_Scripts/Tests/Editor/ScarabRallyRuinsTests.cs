using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Scarab Rally's garden ruins (SCARABRALLY.md "The ruins"): only I4 has them, Spearline's clear lane and ring margin
    /// hold on this course's rings, no ruin prism stands in Switchback's block field or in the sight line to the next ring, at most seven ruins stand and they
    /// are spread along the whole chain, and the lay is a pure function of the course. The same file runs headless
    /// through <c>Tools/Build/scarab_rally_ruins_harness</c>.
    /// </summary>
    public class ScarabRallyRuinsTests
    {
        const float Inner = 480f, Outer = 1080f;
        const int Gates = 24;   // EndConditionOverridesSO.DefaultScarabRallyGateTarget
        const int Budget = 7;   // SpearlineRuins.ForIntensity(4).PerLap
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 42, 101, 102, 103 };

        sealed class Lay
        {
            public List<RaceGate> Course;
            public List<SwitchbackFeaturePrism> Field;
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();
            public readonly List<bool> Laid = new();

            public int Count
            {
                get
                {
                    int n = 0;
                    foreach (bool laid in Laid)
                        if (laid) n++;
                    return n;
                }
            }

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }
        }

        static List<RaceGate> Course(int intensity, int seed)
        {
            var s = ScarabRallyCourse.ForIntensity(intensity);
            s.GateCount = Gates;
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            s.FirstGateDistance = 620f;
            return SwitchbackCourse.Generate(seed, s);
        }

        /// <summary>The lay the race makes: the course, the field ScarabRallyController lays on it (Blast Run where the
        /// rung has it, no gauntlet), and the ruins round both.</summary>
        static Lay Ruins(int intensity, int seed, bool withField = true)
        {
            var lay = new Lay { Course = Course(intensity, seed) };
            Assert.IsNotNull(lay.Course, $"I{intensity} seed {seed}: no course");
            lay.Field = withField && ScarabRallyCourse.LaysBlastRun(intensity)
                ? SwitchbackBlastRun.Layout(lay.Course, intensity, Vector3.zero,
                                            Inner / 1.22f + ScarabRallyRuins.FieldNucleusMargin, false).Prisms
                : null;
            ScarabRallyRuins.Lay(lay.Course, intensity, lay.Field, lay.Parts, lay.Wheels, lay.Laid);
            Assert.AreEqual(lay.Parts.Count, lay.Laid.Count);
            return lay;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            foreach (int seed in Seeds)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed).Parts.Count, $"I{i} seed {seed}");
                Assert.Greater(Ruins(4, seed).Count, 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void The_lane_down_every_leg_is_clear_of_every_standing_ruin()
        {
            foreach (int seed in Seeds)
            {
                var lay = Ruins(4, seed);
                for (int i = 0; i < lay.Parts.Count; i++)
                {
                    if (!lay.Laid[i]) continue;
                    var part = lay.Parts[i];
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(lay.Course, part), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism reaches into a lane");
                    // The ends of a long bar, checked here without the generator's own sampler.
                    if (part.Scale.z <= 16f) continue;
                    Vector3 half = part.Forward * (0.5f * part.Scale.z);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lay.Course, part.Position + half), SpearlineRuins.LaneRadius - 0.01f);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lay.Course, part.Position - half), SpearlineRuins.LaneRadius - 0.01f);
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_mouth()
        {
            // A pilot who threads the 22 u mouth on its centre line is never asked for a narrower pass between rings.
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, ScarabRallyCourse.RingRadiusForIntensity(4));
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            foreach (int seed in Seeds)
            {
                var lay = Ruins(4, seed);
                for (int i = 0; i < lay.Parts.Count; i++)
                    if (lay.Laid[i])
                        Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lay.Course, lay.Parts[i].Position),
                            SpearlineRuins.RingMargin - 0.01f, $"seed {seed}: a ruin prism stands at a ring");
            }
        }

        [Test]
        public void No_ruin_prism_stands_in_the_block_field()
        {
            // Every laid part against EVERY field prism, without the generator's own near list.
            foreach (int seed in Seeds)
            {
                var lay = Ruins(4, seed);
                Assert.Greater(lay.Field.Count, 0, $"seed {seed}: I4 lays a field");
                for (int i = 0; i < lay.Parts.Count; i++)
                {
                    if (!lay.Laid[i]) continue;
                    var part = lay.Parts[i];
                    float half = 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
                    int samples = 1 + Mathf.CeilToInt(part.Scale.z / 10f);
                    foreach (var prism in lay.Field)
                    {
                        float reach = half + 0.5f * Mathf.Max(prism.Scale.x, Mathf.Max(prism.Scale.y, prism.Scale.z))
                                    + ScarabRallyRuins.FieldKeep;
                        for (int k = 0; k < samples; k++)
                        {
                            float along = samples == 1 ? 0f : (k / (float)(samples - 1) - 0.5f) * part.Scale.z;
                            Assert.GreaterOrEqual((part.Position + part.Forward * along - prism.Position).magnitude,
                                reach - 0.01f, $"seed {seed}: a ruin prism stands in the block field");
                        }
                    }
                }
            }
        }

        [Test]
        public void The_field_takes_prisms_out_and_never_puts_one_in()
        {
            foreach (int seed in Seeds)
            {
                var bare = Ruins(4, seed, withField: false);
                var cut = Ruins(4, seed);
                Assert.AreEqual(bare.Parts.Count, cut.Parts.Count, "the candidates do not depend on the field");
                Assert.Less(cut.Count, bare.Count, $"seed {seed}: the field cut nothing");
            }
        }

        [Test]
        public void At_most_seven_ruins_stand_each_big_enough_to_read()
        {
            Assert.AreEqual(Budget, SpearlineRuins.ForIntensity(4).PerLap);
            int total = 0;
            foreach (int seed in Seeds)
            {
                var lay = Ruins(4, seed);
                int ruins = ScarabRallyRuins.RuinCount(lay.Parts, lay.Laid);
                Assert.LessOrEqual(ruins, Budget, $"seed {seed}");
                Assert.GreaterOrEqual(ruins, 5, $"seed {seed}: too few ruins stand");
                var perLeg = new int[lay.Course.Count];
                for (int i = 0; i < lay.Parts.Count; i++)
                    if (lay.Laid[i]) perLeg[lay.Parts[i].Leg]++;
                foreach (int n in perLeg)
                    if (n > 0) Assert.GreaterOrEqual(n, ScarabRallyRuins.MinRuinPrisms, $"seed {seed}: a scrap was laid");
                Assert.GreaterOrEqual(lay.Count, 300, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(lay.Count, 1000, $"seed {seed}: over Spearline's prism budget");
                total += lay.Count;
            }
            float mean = total / (float)Seeds.Length;
            Assert.GreaterOrEqual(mean, 400f, $"mean {mean}");
            Assert.LessOrEqual(mean, 800f, $"mean {mean}");
        }

        [Test]
        public void The_ruins_are_spread_along_the_whole_chain()
        {
            foreach (int seed in Seeds)
            {
                var lay = Ruins(4, seed);
                int firstLeg = int.MaxValue, lastLeg = -1;
                for (int i = 0; i < lay.Parts.Count; i++)
                {
                    if (!lay.Laid[i]) continue;
                    firstLeg = Mathf.Min(firstLeg, lay.Parts[i].Leg);
                    lastLeg = Mathf.Max(lastLeg, lay.Parts[i].Leg);
                    Assert.Less(lay.Parts[i].Leg, lay.Course.Count - 1, "an open chain has no closing leg");
                }
                Assert.LessOrEqual(firstLeg, 3, $"seed {seed}: no ruin near the start");
                Assert.GreaterOrEqual(lastLeg, lay.Course.Count - 6, $"seed {seed}: no ruin near the finish");
            }
        }

        [Test]
        public void No_wheel_turns_here()
        {
            foreach (int seed in Seeds)
            {
                var lay = Ruins(4, seed);
                for (int i = 0; i < lay.Parts.Count; i++)
                    if (lay.Turns(i)) Assert.IsFalse(lay.Laid[i], $"seed {seed}: a wheel sweeps the sight line");
            }
        }

        [Test]
        public void The_next_ring_stays_in_sight_from_the_ring_you_leave()
        {
            // The straight line between consecutive rings, as wide as the field keeps it, without the generator's helper.
            foreach (int seed in Seeds)
            {
                var lay = Ruins(4, seed);
                for (int i = 0; i < lay.Parts.Count; i++)
                {
                    if (!lay.Laid[i]) continue;
                    var part = lay.Parts[i];
                    float half = 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
                    Vector3 end = part.Forward * (0.5f * part.Scale.z);
                    for (int leg = 0; leg + 1 < lay.Course.Count; leg++)
                    {
                        var a = lay.Course[leg];
                        var b = lay.Course[leg + 1];
                        float bore = SwitchbackBlastRun.SightBore(4, false, b.Radius, a.Radius);
                        Assert.GreaterOrEqual(bore, ScarabRallyCourse.RingRadiusForIntensity(4) + SwitchbackBlastRun.SightMargin - 0.01f);
                        foreach (var p in new[] { part.Position, part.Position + end, part.Position - end })
                            Assert.GreaterOrEqual(SwitchbackFeatures.DistanceToSegment(p, a.Position, b.Position), bore + half - 0.01f,
                                $"seed {seed}: a ruin prism stands in the sight line of leg {leg}");
                    }
                }
            }
        }

        [Test]
        public void Every_laid_part_is_turned_and_thick()
        {
            var lay = Ruins(4, 1);
            for (int i = 0; i < lay.Parts.Count; i++)
            {
                if (!lay.Laid[i]) continue;
                var part = lay.Parts[i];
                Assert.AreEqual(1f, part.Forward.magnitude, 1e-3f);
                Assert.AreEqual(1f, part.Up.magnitude, 1e-3f);
                Assert.GreaterOrEqual(part.Scale.x, 8f);
                Assert.GreaterOrEqual(part.Scale.y, 8f);
                Assert.GreaterOrEqual(part.Scale.z, 5f);
            }
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_course()
        {
            foreach (int seed in Seeds)
            {
                var first = Ruins(4, seed);
                var second = Ruins(4, seed);
                Assert.AreEqual(first.Parts.Count, second.Parts.Count);
                for (int i = 0; i < first.Parts.Count; i++)
                {
                    Assert.AreEqual(first.Laid[i], second.Laid[i]);
                    Assert.AreEqual(first.Parts[i].Position, second.Parts[i].Position);
                    Assert.AreEqual(first.Parts[i].Scale, second.Parts[i].Scale);
                }
            }
        }

        [Test]
        public void The_field_is_laid_out_with_Switchbacks_own_nucleus_margin()
        {
            const string path = "Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs";
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            var m = Regex.Match(File.ReadAllText(path), @"const float NucleusMargin = ([0-9.]+)f;");
            Assert.IsTrue(m.Success, "SwitchbackController.NucleusMargin not found");
            Assert.AreEqual(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                ScarabRallyRuins.FieldNucleusMargin, 1e-4f,
                "SwitchbackController.NucleusMargin moved - ScarabRallyRuins.FieldNucleusMargin lays the field out again with it.");
        }
    }
}
