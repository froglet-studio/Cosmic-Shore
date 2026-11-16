using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The powder-keg contract of Grizzly Time (<see cref="GrizzlyTimeKegs"/>, Docs/GrizzlyTime/POWDER_KEGS_SPEC.md §7):
    /// the kegs are a pure function of the course, they never crowd a ring's mouth, a blast in the mouth cannot open one,
    /// the chalk marks a lane a blast does not clear and a mouth-threader never meets, the bank shot is worth taking on a
    /// real corner, and the ladder adds one demand a level inside the prism budget. Asserted over a 400-seed sweep of every intensity, on the shipped
    /// circuit (<see cref="GrizzlyTimeCourse.ForIntensity"/>), because a generated course is exactly the kind of thing
    /// that is fine on the seed you looked at. Runs offline in Tools/Build/grizzly_time_course_harness.
    /// </summary>
    public class GrizzlyTimeKegsTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;
        /// <summary>A hull's half-width plus a margin (u): the closest a chalk dot may sit to a ring-to-ring line.</summary>
        const float HullMargin = 15f;

        static List<RaceGate> Circuit(int intensity, int seed)
        {
            var s = GrizzlyTimeCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        static List<GrizzlyTimeKegs.Keg> Kegs(List<RaceGate> lap)
        {
            var kegs = new List<GrizzlyTimeKegs.Keg>();
            GrizzlyTimeKegs.KegsFor(lap, kegs);
            return kegs;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float t = d.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude) : 0f;
            return (a + d * t - p).magnitude;
        }

        static IEnumerable<(int intensity, int seed, List<RaceGate> lap, List<GrizzlyTimeKegs.Keg> kegs)> Sweep()
        {
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var lap = Circuit(i, seed);
                    yield return (i, seed, lap, Kegs(lap));
                }
        }

        [Test]
        public void Kegs_are_a_pure_function_of_the_course()
        {
            var parts = new List<GrizzlyTimeKegs.Part>();
            var again = new List<GrizzlyTimeKegs.Part>();
            for (int i = 1; i <= 4; i++)
                for (int seed = 1; seed <= 40; seed++)
                {
                    var a = Kegs(Circuit(i, seed));
                    var b = Kegs(Circuit(i, seed));
                    Assert.AreEqual(a.Count, b.Count, $"I{i} seed {seed}: keg count");
                    for (int k = 0; k < a.Count; k++)
                    {
                        Assert.IsTrue(a[k].Centre == b[k].Centre && a[k].ExitDir == b[k].ExitDir && a[k].Ring == b[k].Ring,
                                      $"I{i} seed {seed} keg {k} moved between two builds");
                        GrizzlyTimeKegs.Build(a[k], i, parts);
                        GrizzlyTimeKegs.Build(b[k], i, again);
                        for (int p = 0; p < parts.Count; p++)
                            Assert.IsTrue(parts[p].Position == again[p].Position && parts[p].Kind == again[p].Kind,
                                          $"I{i} seed {seed} keg {k} part {p} differs");
                    }
                }
        }

        [Test]
        public void Every_ring_gets_its_kegs_core_first()
        {
            var parts = new List<GrizzlyTimeKegs.Part>();
            foreach (var (i, seed, lap, kegs) in Sweep())
            {
                Assert.AreEqual(lap.Count, kegs.Count, $"I{i} seed {seed}: one keg per ring");
                foreach (var keg in kegs)
                {
                    GrizzlyTimeKegs.Build(keg, i, parts);
                    Assert.AreEqual(GrizzlyTimeKegs.PrismsPerKeg, parts.Count, "prisms per keg");
                    for (int p = 0; p < parts.Count; p++)
                        Assert.AreEqual(p < GrizzlyTimeKegs.CoreBlocks, parts[p].Core, $"part {p}: the core is listed first");
                    Assert.AreEqual(GrizzlyTimeKegs.CoreKind(i), parts[0].Kind, $"I{i}: core armour");
                }
            }
        }

        /// <summary>A keg stands on its corner's INSIDE: nearer the chord between the previous and next rings than the ring
        /// itself is, so the bank shot points down the next leg instead of across the corner. Read from the corner's own
        /// geometry, not from <see cref="GrizzlyTimeKegs.InsideNormal"/>.</summary>
        [Test]
        public void Every_keg_sits_on_its_corners_inside()
        {
            int corners = 0;
            foreach (var (i, seed, lap, kegs) in Sweep())
            {
                int n = lap.Count;
                foreach (var keg in kegs)
                {
                    Vector3 prev = lap[(keg.Ring - 1 + n) % n].Position, at = lap[keg.Ring].Position;
                    Vector3 next = lap[(keg.Ring + 1) % n].Position;
                    if (RaceCourseGeometry.Angle(at - prev, next - at) < 30f) continue;
                    corners++;
                    Assert.Less(SegmentDistance(keg.Centre, prev, next), SegmentDistance(at, prev, next),
                                $"I{i} seed {seed} ring {keg.Ring}: the keg is on the corner's outside");
                }
            }
            Assert.Greater(corners, Seeds, "the sweep holds corners over 30 deg");
        }

        [Test]
        public void No_keg_or_chalk_prism_is_within_the_mouth_plus_25u_of_any_ring_centre()
        {
            var parts = new List<GrizzlyTimeKegs.Part>();
            foreach (var (i, seed, lap, kegs) in Sweep())
            {
                float keepOut = lap[0].Radius + 25f;
                foreach (var keg in kegs)
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        if (pass == 0) GrizzlyTimeKegs.Build(keg, i, parts);
                        else if (GrizzlyTimeKegs.HasChalk(i)) GrizzlyTimeKegs.Chalk(keg, lap, parts);
                        else continue;
                        foreach (var part in parts)
                            foreach (var gate in lap)
                                Assert.Greater(Vector3.Distance(part.Position, gate.Position), keepOut,
                                               $"I{i} seed {seed}: a keg prism crowds a ring's mouth");
                    }
                }
            }
        }

        [Test]
        public void A_full_blast_at_the_ring_centre_cannot_open_a_keg()
        {
            Assert.Greater(GrizzlyTimeKegs.KegOffset - GrizzlyTimeKegs.HoopRadius, GrizzlyTimeKegs.FullReach,
                           "the keg's nearest prism must sit beyond a full blast at the ring centre");
            var parts = new List<GrizzlyTimeKegs.Part>();
            foreach (var (i, seed, lap, kegs) in Sweep())
                foreach (var keg in kegs)
                {
                    GrizzlyTimeKegs.Build(keg, i, parts);
                    foreach (var gate in lap)
                        for (int p = 0; p < GrizzlyTimeKegs.CoreBlocks; p++)
                            Assert.Greater(Vector3.Distance(parts[p].Position, gate.Position), GrizzlyTimeKegs.FullReach,
                                           $"I{i} seed {seed}: a blast at a ring centre reaches a keg core");
                }
        }

        [Test]
        public void Every_chalk_rail_starts_beyond_one_full_blast_of_its_keg_and_clears_its_exit_line()
        {
            var dots = new List<GrizzlyTimeKegs.Part>();
            int lanes = 0, whole = 0;
            foreach (var (i, seed, lap, kegs) in Sweep())
            {
                if (!GrizzlyTimeKegs.HasChalk(i)) continue;
                foreach (var keg in kegs)
                {
                    GrizzlyTimeKegs.Chalk(keg, lap, dots);
                    Assert.LessOrEqual(dots.Count, GrizzlyTimeKegs.ChalkPerKeg, "chalk dots per keg");
                    lanes++;
                    if (dots.Count == GrizzlyTimeKegs.ChalkPerKeg) whole++;
                    Vector3 next = lap[(keg.Ring + 1) % lap.Count].Position;
                    foreach (var dot in dots)
                    {
                        Assert.Greater(Vector3.Distance(dot.Position, keg.Centre),
                                       GrizzlyTimeKegs.FullReach + GrizzlyTimeKegs.ChalkDotSize,
                                       $"I{i} seed {seed}: a blast at the keg clears its own chalk");
                        Assert.GreaterOrEqual(SegmentDistance(dot.Position, keg.Centre, next), 60f,
                                              $"I{i} seed {seed}: a chalk dot sits in its own exit lane");
                    }
                }
            }
            Assert.GreaterOrEqual(whole / (float)lanes, 0.95f, "share of exit lanes that keep all their chalk");
        }

        [Test]
        public void Chalk_never_blocks_the_ring_to_ring_line()
        {
            var dots = new List<GrizzlyTimeKegs.Part>();
            foreach (var (i, seed, lap, kegs) in Sweep())
            {
                if (!GrizzlyTimeKegs.HasChalk(i)) continue;
                foreach (var keg in kegs)
                {
                    GrizzlyTimeKegs.Chalk(keg, lap, dots);
                    foreach (var dot in dots)
                        for (int r = 0; r < lap.Count; r++)
                        {
                            float d = SegmentDistance(dot.Position, lap[r].Position, lap[(r + 1) % lap.Count].Position);
                            Assert.Greater(d, GrizzlyTimeKegs.ChalkDotSize + HullMargin,
                                           $"I{i} seed {seed}: a chalk dot sits on the ring-to-ring line of leg {r}");
                        }
                }
            }
        }

        [Test]
        public void Every_keg_can_be_blown_from_its_exit_side_inside_its_credit_radius()
        {
            foreach (var (i, seed, lap, kegs) in Sweep())
                foreach (var keg in kegs)
                {
                    float d = Vector3.Distance(GrizzlyTimeKegs.BlowPoint(keg), keg.Centre);
                    Assert.Less(d, GrizzlyTimeKegs.FullReach, "the blow point is inside a full blast");
                    Assert.Less(d, GrizzlyTimeKegs.CreditRadius(i), "the blow point is inside the credit radius");
                    Assert.Greater(d, GrizzlyTimeKegs.HoopRadius + 6f, "the blow point is clear of the hoop");
                }
        }

        /// <summary>The bank shot is the I2 demand: on a corner over 60 deg, blowing the keg straight from where you came
        /// in lands you farther from the next ring than blowing it from the exit side - always, and by at least
        /// <see cref="MedianBankShotWorth"/> u (two seconds of cruise) at the median from I2 up. Measured 2026-11-11 on the
        /// first cut: medians 101 / 137 / 157 / 277 u at I1-I4, the least 31 u.</summary>
        const float MedianBankShotWorth = 100f;

        [Test]
        public void Every_corner_over_60deg_punishes_the_straight_blow()
        {
            float carry = GrizzlyTimeCourse.CarryAfterLaunch(1f);
            int corners = 0;
            var byLevel = new List<float>[5];
            for (int l = 1; l <= 4; l++) byLevel[l] = new List<float>();
            foreach (var (i, seed, lap, kegs) in Sweep())
            {
                int n = lap.Count;
                foreach (var keg in kegs)
                {
                    Vector3 prev = lap[(keg.Ring - 1 + n) % n].Position;
                    Vector3 at = lap[keg.Ring].Position;
                    Vector3 next = lap[(keg.Ring + 1) % n].Position;
                    if (RaceCourseGeometry.Angle(at - prev, next - at) <= 60f) continue;
                    corners++;
                    Vector3 approach = (keg.Centre - prev).normalized;
                    Vector3 straight = keg.Centre - approach * 60f + approach * carry;
                    float waste = Vector3.Distance(straight, next) - Vector3.Distance(GrizzlyTimeKegs.ThrowLanding(keg), next);
                    Assert.Greater(waste, 0f, $"I{i} seed {seed} ring {keg.Ring}: the straight blow is as good as the bank shot");
                    byLevel[i].Add(waste);
                }
            }
            Assert.Greater(corners, Seeds, "the sweep holds corners over 60 deg");
            for (int l = 2; l <= 4; l++)
            {
                var sorted = byLevel[l].OrderBy(w => w).ToList();
                Assert.GreaterOrEqual(sorted[sorted.Count / 2], MedianBankShotWorth, $"I{l}: the median bank shot's worth");
            }
        }

        [Test]
        public void Prism_budget_holds()
        {
            for (int i = 1; i <= 4; i++)
                Assert.LessOrEqual(GrizzlyTimeKegs.PrismsPerLap(i), GrizzlyTimeKegs.MaxPrismsPerLap, $"I{i}: prisms per lap");
            Assert.AreEqual(8 * 15, GrizzlyTimeKegs.PrismsPerLap(1), "I1: 8 kegs of 15");
            Assert.AreEqual(8 * 21, GrizzlyTimeKegs.PrismsPerLap(4), "I4: 8 kegs of 15 with 6 chalk dots each");
        }

        /// <summary>One new demand per level: I2 adds the chalk (the bank shot), I3 shields the core (two keys), I4 is the
        /// tightest credit and the fastest regrow (break your own); the credit radius and the regrow only ever tighten.</summary>
        [Test]
        public void Ladder_adds_one_demand_per_level()
        {
            Assert.IsFalse(GrizzlyTimeKegs.HasChalk(1), "I1 has no chalk");
            Assert.IsTrue(GrizzlyTimeKegs.HasChalk(2) && GrizzlyTimeKegs.CoreKind(2) == PrismKind.Plain, "I2 adds the chalk only");
            Assert.IsTrue(GrizzlyTimeKegs.CoreKind(3) == PrismKind.Shielded, "I3 adds the shield");
            Assert.IsTrue(GrizzlyTimeKegs.CoreKind(4) == PrismKind.Shielded && GrizzlyTimeKegs.RegrowSeconds(4) < GrizzlyTimeKegs.RegrowSeconds(3),
                          "I4 keeps the shield and regrows fastest");
            for (int i = 2; i <= 4; i++)
            {
                Assert.Less(GrizzlyTimeKegs.CreditRadius(i), GrizzlyTimeKegs.CreditRadius(i - 1), $"I{i}: credit radius shrinks");
                Assert.LessOrEqual(GrizzlyTimeKegs.RegrowSeconds(i), GrizzlyTimeKegs.RegrowSeconds(i - 1), $"I{i}: regrow");
                Assert.Greater(GrizzlyTimeKegs.CreditRadius(i), GrizzlyTimeKegs.HoopRadius + 60f, $"I{i}: credit beside the keg");
            }
        }

        /// <summary>The mouth is the safe line, so it narrows gently: no step over 15%, and never so tight that the kegs
        /// are the only way through.</summary>
        [Test]
        public void Mouth_ladder_has_no_step_over_15pct()
        {
            for (int i = 2; i <= 4; i++)
            {
                float a = GrizzlyTimeCourse.ForIntensity(i - 1).RingRadius, b = GrizzlyTimeCourse.ForIntensity(i).RingRadius;
                Assert.LessOrEqual(b, a, $"I{i}: the mouth never widens");
                Assert.LessOrEqual((a - b) / a, 0.15f, $"I{i}: mouth step {a} -> {b}");
            }
            Assert.GreaterOrEqual(GrizzlyTimeCourse.ForIntensity(4).RingRadius, 60f, "I4 mouth");
        }

        [Test]
        public void Bank_shot_reads_the_push_against_the_lane()
        {
            Vector3 keg = Vector3.zero, exit = Vector3.forward;
            Assert.IsTrue(GrizzlyTimeKegs.IsBankShot(keg, new Vector3(0, 0, -60), exit * 300f, exit), "down the lane");
            Assert.IsFalse(GrizzlyTimeKegs.IsBankShot(keg, new Vector3(0, 0, -60), Vector3.right * 300f, exit), "across it");
            Assert.IsFalse(GrizzlyTimeKegs.IsBankShot(keg, new Vector3(0, 0, -400), exit * 300f, exit), "too far to be its blast");
            Assert.IsFalse(GrizzlyTimeKegs.IsBankShot(keg, new Vector3(0, 0, -60), Vector3.zero, exit), "no push");
        }

        [Test]
        public void Toast_ids_are_the_reserved_300_and_301()
        {
            Assert.AreEqual(300, (int)GameToastSituation.GrizzlyTimeHint, "GrizzlyTimeHint");
            Assert.AreEqual(301, (int)GameToastSituation.GrizzlyTimeBankShot, "GrizzlyTimeBankShot");
        }
    }
}
