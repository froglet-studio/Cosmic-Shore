using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Switchback's fun types (<see cref="SwitchbackFunType"/>, SWITCHBACK.md "Fun types"): Baseline is the shipped
    /// game bit for bit; Plan 1's course ladder keeps every contract <see cref="SwitchbackCourseTests"/> holds the
    /// shipped one to and is monotonic; Plan 3's par is sane; Plan 2's features sit where they may.
    /// </summary>
    public class SwitchbackFunTypeTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Gates = 26;
        const int Seeds = 400;
        const float BoostTurnRadius = 180.7f;
        const float ClearRadius = Inner / 1.22f + 30f;

        static SwitchbackCourseSettings Settings(int intensity, SwitchbackFunType funType)
        {
            var s = SwitchbackCourseSettings.ForIntensity(intensity, funType);
            s.GateCount = Gates;
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            s.FirstGateDistance = 620f;
            return s;
        }

        static List<RaceGate> Course(int intensity, SwitchbackFunType funType, int seed) =>
            SwitchbackCourse.Generate(seed, Settings(intensity, funType));

        const SwitchbackFunType Plan1 = SwitchbackFunType.Plan1_LevelShapes;

        // ── Baseline is unchanged ────────────────────────────────────────

        [Test]
        public void BaselineSettingsAreTheShippedLadder([Values(1, 2, 3, 4)] int intensity)
        {
            var shipped = SwitchbackCourseSettings.ForIntensity(intensity);
            foreach (var t in new[] { SwitchbackFunType.Baseline, SwitchbackFunType.Plan2_LiveFeatures, SwitchbackFunType.Plan3_ParMedals })
                Assert.AreEqual(shipped, SwitchbackCourseSettings.ForIntensity(intensity, t),
                    $"{t} at intensity {intensity} does not fly the shipped course settings.");
            Assert.AreEqual(0, shipped.HairpinRun);
            Assert.AreEqual(SwitchbackCornerStyle.None, shipped.CornerStyle);
            Assert.AreEqual(0, shipped.NeedleEvery);
        }

        [Test]
        public void BaselineCoursesAreTheShippedCourses()
        {
            // Pinned from origin/bleeding-edge before the fun types landed (seed 1, gate 5). A change here means the
            // Baseline generator moved: every player's Classic course changed.
            var pinned = new[]
            {
                new Vector3(-133.40958f, -1054.5127f, 146.38174f),
                new Vector3(-178.04498f, 393.6011f, -356.27908f),
                new Vector3(-456.21957f, 146.80603f, -96.71422f),
                new Vector3(-352.98938f, 532.8166f, -328.54282f),
            };
            for (int i = 1; i <= 4; i++)
            {
                var c = Course(i, SwitchbackFunType.Baseline, 1);
                Assert.Less((c[5].Position - pinned[i - 1]).magnitude, 1f, $"intensity {i}'s Baseline course moved.");
                foreach (var g in c) Assert.AreEqual(SwitchbackCourseSettings.RingRadiusForIntensity(i), g.Radius, 1e-4f);
            }
        }

        [Test]
        public void HairpinRunOfOneIsTheShippedRule()
        {
            for (int every = 1; every <= 4; every++)
            {
                var a = new SwitchbackCourseSettings { GateCount = Gates, HairpinEvery = every, HairpinRun = 0 };
                var b = a;
                b.HairpinRun = 1;
                for (int i = 0; i < Gates; i++)
                {
                    bool shipped = i >= 1 && i < Gates - 1 && i % every == 0;
                    Assert.AreEqual(shipped, SwitchbackCourse.IsHairpinCorner(i, a), $"every {every} gate {i}");
                    Assert.AreEqual(shipped, SwitchbackCourse.IsHairpinCorner(i, b), $"every {every} gate {i} (run 1)");
                }
            }
        }

        // ── Plan 1: every shipped contract still holds ───────────────────

        [Test]
        public void Plan1_EverySeedProducesAFullCourseInsideTheShell([Values(1, 2, 3, 4)] int intensity)
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, Plan1, seed);
                Assert.IsNotNull(c, $"intensity {intensity} seed {seed} produced no course.");
                Assert.AreEqual(Gates, c.Count);
                Assert.AreEqual(0f, c[0].Position.x, 0.01f);
                Assert.AreEqual(0f, c[0].Position.z, 0.01f);
                foreach (var g in c)
                {
                    float r = g.Position.magnitude;
                    Assert.That(r, Is.InRange(Inner - 0.01f, Outer + 0.01f), $"intensity {intensity} seed {seed}: gate at {r:F0}.");
                }
            }
        }

        [Test]
        public void Plan1_CornersRespectTheCapTheBandAndTheTurningCircle([Values(1, 2, 3, 4)] int intensity)
        {
            var s = Settings(intensity, Plan1);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, Plan1, seed);
                for (int i = 1; i < c.Count - 1; i++)
                {
                    Vector3 inbound = (c[i].Position - c[i - 1].Position).normalized;
                    Vector3 outbound = (c[i + 1].Position - c[i].Position).normalized;
                    float turn = SwitchbackCourse.Angle(inbound, outbound);
                    if (SwitchbackCourse.IsHairpinCorner(i, s))
                    {
                        Assert.That(turn, Is.InRange(s.HairpinMinDegrees - 0.05f, s.HairpinMaxDegrees + 0.05f),
                            $"intensity {intensity} seed {seed} gate {i}: hairpin {turn:F1}.");
                        float present = SwitchbackCourse.Angle(inbound, c[i].Axis);
                        if (present > 90f) present = 180f - present;
                        Assert.LessOrEqual(present, s.AxisJitterDegrees + 0.05f, $"seed {seed} hairpin gate {i} presents {present:F1}.");
                        continue;
                    }
                    Assert.LessOrEqual(turn, s.MaxTurnDegrees + 0.05f, $"intensity {intensity} seed {seed} gate {i}: {turn:F1} deg.");
                    float leg = (c[i + 1].Position - c[i].Position).magnitude;
                    Assert.Greater(leg, 2f * BoostTurnRadius * Mathf.Sin(turn * Mathf.Deg2Rad),
                        $"intensity {intensity} seed {seed} gate {i}: leg {leg:F0} inside the boosted turning circle.");
                }
            }
        }

        [Test]
        public void Plan1_NoGateStandsEdgeOnAndNoMouthsOverlap([Values(1, 2, 3, 4)] int intensity)
        {
            var s = Settings(intensity, Plan1);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, Plan1, seed);
                for (int i = 1; i < c.Count; i++)
                {
                    float present = SwitchbackCourse.Angle((c[i].Position - c[i - 1].Position).normalized, c[i].Axis);
                    if (present > 90f) present = 180f - present;
                    Assert.LessOrEqual(present, s.MaxPresentDegrees + 0.05f, $"intensity {intensity} seed {seed} gate {i}.");
                }
                for (int i = 0; i < c.Count; i++)
                    for (int j = i + 1; j < c.Count; j++)
                        Assert.Greater((c[i].Position - c[j].Position).magnitude, 2f * s.RingRadius,
                            $"intensity {intensity} seed {seed}: gates {i} and {j} overlap.");
            }
        }

        [Test]
        public void Plan1_LadderIsMonotonic()
        {
            for (int i = 1; i < 4; i++)
            {
                var lo = SwitchbackCourseSettings.ForIntensity(i, Plan1);
                var hi = SwitchbackCourseSettings.ForIntensity(i + 1, Plan1);
                Assert.Less(hi.RingRadius, lo.RingRadius, $"ring radius {i}->{i + 1}");
                Assert.Greater(hi.MaxTurnDegrees, lo.MaxTurnDegrees, $"turn cap {i}->{i + 1}");
                Assert.Greater(hi.AxisJitterDegrees, lo.AxisJitterDegrees, $"jitter {i}->{i + 1}");
                Assert.Greater(hi.HairpinShare, lo.HairpinShare, $"hairpin share {i}->{i + 1}");
                Assert.GreaterOrEqual(hi.HairpinMinDegrees, lo.HairpinMinDegrees, $"hairpin floor {i}->{i + 1}");
            }
            Assert.AreEqual(0f, SwitchbackCourseSettings.ForIntensity(1, Plan1).HairpinShare);
            Assert.AreEqual(1f / 3f, SwitchbackCourseSettings.ForIntensity(2, Plan1).HairpinShare, 1e-4f);
            Assert.AreEqual(1f / 2f, SwitchbackCourseSettings.ForIntensity(3, Plan1).HairpinShare, 1e-4f);
            Assert.AreEqual(2f / 3f, SwitchbackCourseSettings.ForIntensity(4, Plan1).HairpinShare, 1e-4f);
        }

        [Test]
        public void Plan1_EveryLegClearsTheAiApproachRun([Values(1, 2, 3, 4)] int intensity)
        {
            Assert.GreaterOrEqual(SwitchbackCourseSettings.ForIntensity(intensity, Plan1).MinStep, 360f);
        }

        [Test]
        public void Plan1_IntensityFourHasBackToBackHairpins()
        {
            var s = Settings(4, Plan1);
            int pairs = 0;
            for (int i = 1; i < Gates - 2; i++)
                if (SwitchbackCourse.IsHairpinCorner(i, s) && SwitchbackCourse.IsHairpinCorner(i + 1, s)) pairs++;
            Assert.GreaterOrEqual(pairs, 6, "intensity 4 has too few back-to-back hairpins.");
            Assert.IsFalse(SwitchbackCourse.IsHairpinCorner(1, s), "the first corner should be an ordinary one.");
        }

        [Test]
        public void Plan1_NeedleRingsAreTightButFlyable([Values(1, 2, 3, 4)] int intensity)
        {
            var s = Settings(intensity, Plan1);
            var c = Course(intensity, Plan1, 7);
            int needles = 0;
            for (int i = 0; i < c.Count; i++)
            {
                bool needle = SwitchbackCourse.IsNeedleGate(i, s);
                if (needle)
                {
                    needles++;
                    Assert.IsFalse(SwitchbackCourse.IsHairpinCorner(i, s), $"gate {i} is a hairpin AND a needle.");
                    Assert.Less(c[i].Radius, s.RingRadius + 1e-4f);
                    Assert.GreaterOrEqual(c[i].Radius, SwitchbackCourseSettings.NeedleMinRadius - 1e-4f);
                }
                else Assert.AreEqual(s.RingRadius, c[i].Radius, 1e-4f, $"gate {i} lost its ring radius.");
            }
            if (intensity == 1) Assert.AreEqual(0, needles, "intensity 1 has a needle.");
            else Assert.GreaterOrEqual(needles, 3, $"intensity {intensity} has only {needles} needles.");
        }

        /// <summary>Plan 1's corner patterns, measured on the courses: I1's corners bend the same way more often than
        /// not and I2's alternate (the wall steer and wobble make it a tendency, not a rule).</summary>
        [Test]
        public void Plan1_CornerPatternsShowOnTheCourse()
        {
            float SenseAgreement(int intensity)
            {
                var s = Settings(intensity, Plan1);
                double sum = 0; int n = 0;
                for (int seed = 1; seed <= 100; seed++)
                {
                    var c = Course(intensity, Plan1, seed);
                    for (int i = 2; i < c.Count - 1; i++)
                    {
                        if (SwitchbackCourse.IsHairpinCorner(i, s) || SwitchbackCourse.IsHairpinCorner(i - 1, s)) continue;
                        Vector3 a = Vector3.Cross(c[i - 1].Position - c[i - 2].Position, c[i].Position - c[i - 1].Position).normalized;
                        Vector3 b = Vector3.Cross(c[i].Position - c[i - 1].Position, c[i + 1].Position - c[i].Position).normalized;
                        sum += Vector3.Dot(a, b); n++;
                    }
                }
                return n > 0 ? (float)(sum / n) : 0f;
            }
            // Measured (box harness, 2026-10-28): I1 ~ +0.90, I2 ~ -0.07. S-bends alternate, but a zig-zag runs
            // straight out of the shell and the wall steer (always toward the middle) bends it back the same way, so
            // the alternation shows as "much less same-way than I1", not as a strong negative.
            float arcs = SenseAgreement(1), sBends = SenseAgreement(2);
            Assert.Greater(arcs, 0.6f, $"intensity 1's corners do not flow as arcs ({arcs:F2}).");
            Assert.Less(sBends, 0.1f, $"intensity 2's corners do not alternate as S-bends ({sBends:F2}).");
            Assert.Greater(arcs - sBends, 0.6f, "the I1 and I2 corner patterns fly alike.");

            // I3: the leg into a hairpin is the long one.
            var s3 = Settings(3, Plan1);
            double into = 0, other = 0; int ni = 0, no = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                var c = Course(3, Plan1, seed);
                for (int i = 1; i < c.Count; i++)
                {
                    float leg = (c[i].Position - c[i - 1].Position).magnitude;
                    if (SwitchbackCourse.IsHairpinCorner(i, s3)) { into += leg; ni++; } else { other += leg; no++; }
                }
            }
            Assert.Greater(into / ni, other / no + 100.0, "intensity 3's legs into hairpins are not the long ones.");
        }

        // ── Plan 3: par ──────────────────────────────────────────────────

        [Test]
        public void Par_IsSaneForEveryWorld([Values(SwitchbackFunType.Baseline, SwitchbackFunType.Plan1_LevelShapes)] SwitchbackFunType t,
                                            [Values(1, 2, 3, 4)] int intensity)
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var c = Course(intensity, t, seed);
                float par = SwitchbackPar.ParSeconds(c, Vector3.zero);
                float polyline = c[0].Position.magnitude;
                for (int i = 1; i < c.Count; i++) polyline += (c[i].Position - c[i - 1].Position).magnitude;
                Assert.GreaterOrEqual(par, polyline / SwitchbackPar.DolphinBoostSpeed, "par is faster than boosting the whole line.");
                Assert.LessOrEqual(par, polyline / SwitchbackPar.DolphinCruiseSpeed, "par is slower than cruising the whole line.");
                Assert.That(par, Is.InRange(20f, 300f), $"{t} I{intensity} seed {seed}: par {par:F1}s.");
                float prev = 0f;
                for (int g = 0; g < c.Count; g++)
                {
                    float at = SwitchbackPar.ParAtGate(c, Vector3.zero, g);
                    Assert.GreaterOrEqual(at, prev, "par to a later gate is shorter.");
                    prev = at;
                }
                Assert.AreEqual(par, prev, 1e-3f);
            }
        }

        [Test]
        public void Par_HairpinsCostTime()
        {
            // A straight course and the same legs folded into a hairpin: same length, the hairpin's par is longer.
            var straight = new List<RaceGate>
            {
                new(new Vector3(0, 0, 0), Vector3.forward, 20f),
                new(new Vector3(0, 0, 500), Vector3.forward, 20f),
                new(new Vector3(0, 0, 1000), Vector3.forward, 20f),
            };
            var hairpin = new List<RaceGate>
            {
                new(new Vector3(0, 0, 0), Vector3.forward, 20f),
                new(new Vector3(0, 0, 500), Vector3.forward, 20f),
                new(new Vector3(500f * Mathf.Sin(30f * Mathf.Deg2Rad), 0, 500f - 500f * Mathf.Cos(30f * Mathf.Deg2Rad)), Vector3.back, 20f),
            };
            float a = SwitchbackPar.ParSeconds(straight, Vector3.zero);
            float b = SwitchbackPar.ParSeconds(hairpin, Vector3.zero);
            Assert.AreEqual(1000f / SwitchbackPar.ParSpeed, a, 1e-3f);
            Assert.AreEqual(a + 150f / SwitchbackPar.DolphinTurnRate, b, 0.05f);
        }

        [Test]
        public void Medals_FollowTheBands()
        {
            Assert.AreEqual(SwitchbackMedal.Gold, SwitchbackPar.MedalFor(59f, 60f));
            Assert.AreEqual(SwitchbackMedal.Gold, SwitchbackPar.MedalFor(60f, 60f));
            Assert.AreEqual(SwitchbackMedal.Silver, SwitchbackPar.MedalFor(72f, 60f));
            Assert.AreEqual(SwitchbackMedal.Bronze, SwitchbackPar.MedalFor(90f, 60f));
            Assert.AreEqual(SwitchbackMedal.None, SwitchbackPar.MedalFor(91f, 60f));
            Assert.AreEqual(SwitchbackMedal.None, SwitchbackPar.MedalFor(0f, 60f));
            Assert.AreEqual("+1.5", SwitchbackPar.FormatDelta(11.5f, 10f));
            Assert.AreEqual("-0.5", SwitchbackPar.FormatDelta(9.5f, 10f));
            Assert.AreEqual("1:02.5", SwitchbackPar.FormatTime(62.5f));
        }

        [Test]
        public void WorldSeedsAndPersonalBestKeysAreDistinct()
        {
            var seeds = new HashSet<int>();
            var keys = new HashSet<string>();
            foreach (SwitchbackFunType t in System.Enum.GetValues(typeof(SwitchbackFunType)))
                for (int i = 1; i <= 4; i++)
                {
                    int seed = SwitchbackFunTypes.WorldSeed(t, i);
                    Assert.AreNotEqual(0, seed);
                    Assert.IsTrue(seeds.Add(seed), $"{t} I{i} shares a seed.");
                    Assert.IsTrue(keys.Add(SwitchbackFunTypes.PersonalBestKey(t, i)), $"{t} I{i} shares a PB key.");
                }
            Assert.AreEqual(6, SwitchbackFunTypes.PickerOrder.Length);
            Assert.AreEqual(SwitchbackFunType.BlastRun, SwitchbackFunTypes.PickerOrder[5], "Blast Run is appended; the first five keep their order.");
            Assert.AreEqual(SwitchbackFunType.BlastRun, SwitchbackFunTypes.Sanitize(5));
            Assert.AreEqual(SwitchbackFunType.Baseline, SwitchbackFunTypes.Sanitize(6));
            Assert.AreEqual(SwitchbackFunType.Baseline, SwitchbackFunTypes.Sanitize(99));
        }

        // ── Plan 2: live features ────────────────────────────────────────

        static SwitchbackFeatureLayout Live(SwitchbackFunType t, int intensity, int seed, out List<RaceGate> course)
        {
            course = Course(intensity, t, seed);
            return SwitchbackLiveFeatures.Layout(course, Settings(intensity, t), intensity, Vector3.zero, ClearRadius);
        }

        [Test]
        public void Live_FeaturesFollowTheLadder([Values(SwitchbackFunType.Plan2_LiveFeatures, SwitchbackFunType.Combined)] SwitchbackFunType t,
                                                 [Values(1, 2, 3, 4)] int intensity)
        {
            int ribbons = 0, shortcuts = 0, moving = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                var l = Live(t, intensity, seed, out _);
                ribbons += l.Ribbons.Count; shortcuts += l.Shortcuts.Count; moving += l.MovingClusters.Count;
            }
            Assert.AreEqual(intensity <= 3, ribbons > 0, $"{t} I{intensity}: {ribbons} ribbons.");
            Assert.AreEqual(intensity >= 2, shortcuts > 0, $"{t} I{intensity}: {shortcuts} shortcuts.");
            Assert.AreEqual(intensity == 4, moving > 0, $"{t} I{intensity}: {moving} moving clusters.");
        }

        [Test]
        public void Live_PrismsAndShortcutsKeepTheirClearances([Values(SwitchbackFunType.Plan2_LiveFeatures, SwitchbackFunType.Combined)] SwitchbackFunType t,
                                                               [Values(1, 2, 3, 4)] int intensity)
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var l = Live(t, intensity, seed, out var c);
                foreach (var p in l.Prisms)
                {
                    Assert.GreaterOrEqual(p.Position.magnitude, ClearRadius, $"seed {seed}: a prism in the nucleus.");
                    foreach (var g in c)
                        Assert.GreaterOrEqual((p.Position - g.Position).magnitude, g.Radius + SwitchbackFeatures.MouthClearance - 0.01f,
                            $"seed {seed}: a prism in a mouth.");
                }
                foreach (var m in l.MovingClusters)
                    for (int j = 0; j < m.Count; j++)
                    {
                        var laid = l.Prisms[m.FirstPrism + j];
                        Assert.AreEqual(PrismKind.Danger, laid.Kind);
                        Assert.Less(m.DisplacementAt(0f).magnitude, 1e-3f, "the sweep does not start at the laid pose.");
                        for (float time = 0f; time < SwitchbackLiveFeatures.MovingPeriod; time += 0.25f)
                        {
                            Vector3 at = laid.Position + m.DisplacementAt(time);
                            foreach (var g in c)
                                Assert.GreaterOrEqual((at - g.Position).magnitude, g.Radius + SwitchbackFeatures.MouthClearance - 0.5f,
                                    $"seed {seed}: a moving prism sweeps into a mouth.");
                        }
                    }
                var s = Settings(intensity, t);
                foreach (var sc in l.Shortcuts)
                {
                    Assert.IsTrue(SwitchbackCourse.IsHairpinCorner(sc.SkipsGate, s), "a shortcut beside a non-hairpin.");
                    foreach (var g in c)
                        Assert.GreaterOrEqual((sc.Ring.Position - g.Position).magnitude,
                            SwitchbackLiveFeatures.ShortcutGateClearanceRings * g.Radius, $"seed {seed}: a shortcut crowds a gate.");
                }
            }
        }

        [Test]
        public void Live_ARibbonSkimCompletesOnlyEndToEnd()
        {
            var ribbon = new SwitchbackRibbon(1, Vector3.zero, new Vector3(0, 0, 300), Vector3.right);
            Vector3 lane = new(-8f, 0, 0);   // 8 u inside the wall line

            var full = new SwitchbackRibbonSkim();
            int bursts = 0;
            for (float z = -20f; z <= 320f; z += 5f)
                if (full.Step(ribbon, lane + new Vector3(0, 0, z), z / 300f)) bursts++;
            Assert.AreEqual(1, bursts, "a full skim gave no (or more than one) burst.");

            var late = new SwitchbackRibbonSkim();
            for (float z = 150f; z <= 320f; z += 5f)
                Assert.IsFalse(late.Step(ribbon, lane + new Vector3(0, 0, z), z / 300f), "a skim joined halfway paid out.");

            var broken = new SwitchbackRibbonSkim();
            bool paid = false;
            for (float z = 0f; z <= 320f; z += 5f)
            {
                Vector3 p = lane + new Vector3(z > 100f && z < 200f ? -60f : 0f, 0, z);   // pull off for 100 u
                paid |= broken.Step(ribbon, p, z / 100f);
            }
            Assert.IsFalse(paid, "a skim that left the wall for a second paid out.");
        }
    }
}
