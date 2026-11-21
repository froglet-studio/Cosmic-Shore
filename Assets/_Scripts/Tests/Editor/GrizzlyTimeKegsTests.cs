using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The powder-keg contract of Grizzly Time (<see cref="GrizzlyTimeKegs"/>, Docs/GrizzlyTime/POWDER_KEGS_SPEC.md §7):
    /// the kegs are a pure function of the course, they never crowd a ring's mouth, a blast in the mouth cannot open one,
    /// the chalk marks a lane a blast does not clear and a mouth-threader never meets, the blow point is on the keg's exit
    /// side, the bank shot is worth taking on a real corner, a Rush through the core opens the keg and a pass beside it
    /// does not, and the ladder adds one demand a level inside the prism budget. Asserted over a 400-seed sweep of every intensity, on the shipped
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

        /// <summary>The blow point is on the keg's EXIT side: a Grizzly is launched away from its bomb
        /// (<c>GrizzlyTriggerBombExecutor.LaunchSelf</c>: direction = hull - bomb), so with the bomb in the keg the launch
        /// of a hull at the blow point is the keg's exit lane, toward the next ring. Until 2026-11-20 the point stood on
        /// the entry side and this test measured only its distance, so it passed on a point that throws the hull away
        /// from the next ring.</summary>
        [Test]
        public void Every_keg_can_be_blown_from_its_exit_side_inside_its_credit_radius()
        {
            foreach (var (i, seed, lap, kegs) in Sweep())
                foreach (var keg in kegs)
                {
                    Vector3 stand = GrizzlyTimeKegs.BlowPoint(keg);
                    float d = Vector3.Distance(stand, keg.Centre);
                    Assert.Less(d, GrizzlyTimeKegs.FullReach, "the blow point is inside a full blast");
                    Assert.Less(d, GrizzlyTimeKegs.CreditRadius(i), "the blow point is inside the credit radius");
                    Assert.Greater(d, GrizzlyTimeKegs.HoopRadius + 6f, "the blow point is clear of the hoop");

                    Vector3 launch = (stand - keg.Centre).normalized;   // away from a bomb in the keg
                    Vector3 next = lap[(keg.Ring + 1) % lap.Count].Position;
                    Assert.Less(Vector3.Angle(launch, keg.ExitDir), 1f, $"I{i} seed {seed} ring {keg.Ring}: the launch is the exit lane");
                    Assert.Less(Vector3.Distance(stand, next), Vector3.Distance(keg.Centre, next),
                                $"I{i} seed {seed} ring {keg.Ring}: the blow point is on the side nearer the next ring");
                    Assert.Greater(Vector3.Dot(next - stand, keg.ExitDir), 0f,
                                   $"I{i} seed {seed} ring {keg.Ring}: the next ring is ahead of the blow point, down the launch");
                    Assert.Greater(Vector3.Dot(GrizzlyTimeKegs.ThrowLanding(keg) - stand, keg.ExitDir), 0f,
                                   $"I{i} seed {seed} ring {keg.Ring}: the throw carries down the exit lane");
                    Assert.IsTrue(GrizzlyTimeKegs.IsBankShot(keg.Centre, stand, launch * 300f, keg.ExitDir),
                                  $"I{i} seed {seed} ring {keg.Ring}: the launch from the blow point reads as a bank shot");
                }
        }

        /// <summary>A hull standing at the blow point is outside every ring's mouth by more than its own half-width, so
        /// the throw never starts inside a mouth.</summary>
        [Test]
        public void The_blow_point_stands_outside_every_mouth()
        {
            foreach (var (i, seed, lap, kegs) in Sweep())
                foreach (var keg in kegs)
                    foreach (var gate in lap)
                        Assert.Greater(Vector3.Distance(GrizzlyTimeKegs.BlowPoint(keg), gate.Position), gate.Radius + HullMargin,
                                       $"I{i} seed {seed} ring {keg.Ring}: the blow point crowds a mouth");
        }

        /// <summary>The bank shot is the I2 demand: on a corner over 60 deg, a pilot who flies straight past the keg and
        /// blows it from there (thrown the way it came in) lands farther from the next ring than one who gets round to the
        /// exit side - always, and by at least <see cref="MedianBankShotWorth"/> u (two seconds of cruise) at the median
        /// from I2 up. Both stand <see cref="GrizzlyTimeKegs.BlowStandOff"/> u past the core, on the side their launch
        /// points. Measured 2026-11-20 (400 seeds): medians 198 / 243 / 281 / 335 u at I1-I4, the least 39 u.</summary>
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
                    Vector3 straight = keg.Centre + approach * (GrizzlyTimeKegs.BlowStandOff + carry);
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
            Vector3 stand = new Vector3(0, 0, GrizzlyTimeKegs.BlowStandOff);
            Assert.IsTrue(GrizzlyTimeKegs.IsBankShot(keg, stand, exit * 300f, exit), "down the lane");
            Assert.IsFalse(GrizzlyTimeKegs.IsBankShot(keg, stand, Vector3.right * 300f, exit), "across it");
            Assert.IsFalse(GrizzlyTimeKegs.IsBankShot(keg, new Vector3(0, 0, 400), exit * 300f, exit), "too far to be its blast");
            Assert.IsFalse(GrizzlyTimeKegs.IsBankShot(keg, stand, Vector3.zero, exit), "no push");
        }

        /// <summary>A Rush pushes the hull down the lane too, but it is not a bomb's launch: 80 u/s (120 at the peak of its
        /// ease), against the 300 u/s a launch from inside a full blast reaches. A Rush alone must never read as a bank
        /// shot, whatever the transformer's own ceiling is.</summary>
        [Test]
        public void A_Rush_alone_is_not_a_bank_shot()
        {
            Vector3 keg = Vector3.zero, exit = Vector3.forward;
            Vector3 stand = new Vector3(0, 0, GrizzlyTimeKegs.BlowStandOff);
            float rushPeak = RushAsset("magnitude") * 1.5f;
            Assert.Greater(GrizzlyTimeKegs.BankShotMinPush, rushPeak, "the least bank-shot push is above a Rush's peak");
            Assert.Less(GrizzlyTimeKegs.BankShotMinPush, GrizzlyTimeCourse.VelocityModifierCeiling, "and under a launch's ceiling");
            Assert.IsFalse(GrizzlyTimeKegs.IsBankShot(keg, stand, exit * rushPeak, exit), "a Rush down the lane");
            Assert.IsTrue(GrizzlyTimeKegs.IsBankShot(keg, stand, exit * GrizzlyTimeCourse.VelocityModifierCeiling, exit), "a launch down the lane");
        }

        // ── Rush: the ram ─────────────────────────────────────────────────

        const string RushAssetPath = "Assets/_SO_Assets/VesselActions/Grizzly/GrizzlyRushAction.asset";

        static float RushAsset(string key)
        {
            Assert.IsTrue(File.Exists(RushAssetPath), $"{RushAssetPath} is missing");
            var m = Regex.Match(File.ReadAllText(RushAssetPath), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {RushAssetPath}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The mode READS the Rush's ram (it never changes the vessel): the two numbers it restates are the
        /// shipped asset's. A retune of the Rush moves them here, or every peer but the rammer's opens kegs by the old
        /// reach.</summary>
        [Test]
        public void Ram_constants_match_the_shipped_Rush()
        {
            const string why = " moved - GrizzlyTimeKegs builds the reach every peer opens a rammed keg by from it. Update the constant.";
            Assert.AreEqual(GrizzlyTimeKegs.RamRadius, RushAsset("ramRadius"), 1e-4f, "Rush ramRadius" + why);
            Assert.AreEqual(GrizzlyTimeKegs.RamSeconds, RushAsset("duration"), 1e-4f, "Rush duration" + why);
            Assert.AreEqual(GrizzlyTimeKegs.CoreCube * 0.5f, GrizzlyTimeKegs.RamReach - GrizzlyTimeKegs.RamRadius, 1e-4f,
                            "the mode's reach is the ram's radius plus half a core cube, its stated tolerance");
        }

        /// <summary>A hull that Rushes through the core opens the keg from anywhere along the core's line; one at the blow
        /// point, at the AI's pass point, threading the ring's mouth or flying the ring-to-ring line does not, so a Rush
        /// spent for pace on the racing line never opens a keg by accident (the nearest a ring-to-ring line comes to a core
        /// is 34 u over 400 seeds, at I4, against the mode's reach of 19: the ram's 16 plus its tolerance).</summary>
        [Test]
        public void A_Rush_through_the_core_opens_the_keg_and_a_pass_beside_it_does_not()
        {
            float reach = GrizzlyTimeKegs.RamReach;
            Assert.Less(reach, GrizzlyTimeKegs.AIKegPassOffset, "the AI's pass beside a keg is outside the ram");
            Assert.Less(GrizzlyTimeKegs.RamCreditReach, GrizzlyTimeKegs.BlowStandOff, "a hull at the blow point did not ram the keg");
            foreach (var (i, seed, lap, kegs) in Sweep())
            {
                int n = lap.Count;
                foreach (var keg in kegs)
                {
                    Vector3 across = GrizzlyTimeKegs.Lateral(keg);
                    Assert.IsTrue(GrizzlyTimeKegs.RamOpens(keg.Centre, keg, reach), "in the core");
                    Assert.IsTrue(GrizzlyTimeKegs.RamOpens(keg.Centre + across * GrizzlyTimeKegs.CoreSpacing - keg.ExitDir * (reach - 1f), keg, reach),
                                  "just short of an end block, coming down the barrel");
                    Assert.IsFalse(GrizzlyTimeKegs.RamOpens(keg.Centre - keg.ExitDir * (reach + 1f), keg, reach), "a hull not there yet");
                    Assert.IsFalse(GrizzlyTimeKegs.RamOpens(GrizzlyTimeKegs.BlowPoint(keg), keg, reach), "at the blow point");
                    Assert.IsFalse(GrizzlyTimeKegs.RamOpens(keg.Centre + keg.ExitDir * GrizzlyTimeKegs.AIKegPassOffset, keg, reach), "at the AI's pass point");
                    Assert.IsFalse(GrizzlyTimeKegs.RamOpens(lap[keg.Ring].Position, keg, reach), $"I{i} seed {seed}: in its ring's mouth");
                    for (int r = 0; r < n; r++)
                        Assert.Greater(SegmentDistance(keg.Centre, lap[r].Position, lap[(r + 1) % n].Position), reach,
                                       $"I{i} seed {seed} ring {keg.Ring}: the ring-to-ring line of leg {r} runs through the ram's reach");
                }
            }
        }

        // ── AI: Rush on a straight ────────────────────────────────────────

        /// <summary>The Energy resource's gain per tick, read off the shipped Grizzly: the first Resource block of the
        /// prefab named Energy.</summary>
        static float PrefabEnergyGain()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/Grizzly.prefab";
            Assert.IsTrue(File.Exists(prefab), $"{prefab} is missing");
            var m = Regex.Match(File.ReadAllText(prefab), @"Name: Energy\b[\s\S]{0,400}?resourceGainRate: ([-0-9.eE]+)");
            Assert.IsTrue(m.Success, "the Grizzly's Energy resource was not found in its prefab");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The AI's Rush is one pure rule: lined up, a straight ahead, no launch pushing it, the Energy there
        /// with a tick to spare, its last press cooled down. Each condition alone refuses the press.</summary>
        [Test]
        public void The_AI_rushes_only_on_a_lined_up_straight_outside_a_launch()
        {
            const float cost = 0.25f, tick = 0.05f, cooldown = 2f;
            float nose = GrizzlyTimeKegs.AIRushNoseDegrees - 1f, run = GrizzlyTimeKegs.AIRushMinRun + 1f;
            float ready = cooldown + GrizzlyTimeKegs.AIRushCooldownMargin + 0.01f;
            float full = cost + tick;
            Assert.IsTrue(GrizzlyTimeKegs.AIShouldRush(nose, run, 0f, full, cost, tick, ready, cooldown), "lined up on a straight");
            Assert.IsTrue(GrizzlyTimeKegs.AIShouldRush(0f, 600f, 0f, 1f, cost, tick, float.PositiveInfinity, cooldown), "its first press");
            Assert.IsFalse(GrizzlyTimeKegs.AIShouldRush(GrizzlyTimeKegs.AIRushNoseDegrees + 1f, run, 0f, full, cost, tick, ready, cooldown), "nose off the aim");
            Assert.IsFalse(GrizzlyTimeKegs.AIShouldRush(nose, GrizzlyTimeKegs.AIRushMinRun - 1f, 0f, full, cost, tick, ready, cooldown), "no straight left");
            Assert.IsFalse(GrizzlyTimeKegs.AIShouldRush(nose, run, GrizzlyTimeKegs.AIRushMaxPush + 1f, full, cost, tick, ready, cooldown), "mid-launch");
            Assert.IsFalse(GrizzlyTimeKegs.AIShouldRush(nose, run, 0f, cost - 0.01f, cost, 0f, ready, cooldown), "no Energy");
            Assert.IsFalse(GrizzlyTimeKegs.AIShouldRush(nose, run, 0f, full, cost, tick, cooldown, cooldown), "inside its cooldown");

            // One tick of headroom: every peer runs the press against its own Energy, which gains once a second on its
            // own phase, so at exactly the cost a peer one tick behind would refuse it.
            Assert.IsFalse(GrizzlyTimeKegs.AIShouldRush(nose, run, 0f, cost, cost, tick, ready, cooldown), "at exactly the cost, a peer a tick behind refuses");
            Assert.IsTrue(GrizzlyTimeKegs.AIShouldRush(nose, run, 0f, cost, cost, 0f, ready, cooldown), "no headroom asked, the cost is enough");
            Assert.AreEqual(tick, PrefabEnergyGain(), 1e-4f, "the Grizzly's Energy gain per tick (the headroom this test stands for)");

            // The straight it asks for is longer than a Rush covers (cruise plus the burst, one second), so the burst is
            // over before the aim point; and a launch's push is far above the hold, a Rush's own never under it mid-burst.
            float rushRun = GrizzlyTimeCourse.GrizzlyThrottleScaler * RushAsset("duration") + RushAsset("magnitude") * RushAsset("duration");
            Assert.Greater(GrizzlyTimeKegs.AIRushMinRun, rushRun, "the least straight is longer than one Rush");
            Assert.Less(GrizzlyTimeKegs.AIRushMaxPush, RushAsset("magnitude") * 0.5f, "the hold is under a Rush's own push at its weakest");
            Assert.Greater(RushAsset("cooldownSeconds"), 0f, "the Rush has a cooldown the AI waits out");
        }

        [Test]
        public void Toast_ids_are_the_reserved_300_to_302()
        {
            Assert.AreEqual(300, (int)GameToastSituation.GrizzlyTimeHint, "GrizzlyTimeHint");
            Assert.AreEqual(301, (int)GameToastSituation.GrizzlyTimeBankShot, "GrizzlyTimeBankShot");
            Assert.AreEqual(302, (int)GameToastSituation.GrizzlyTimeRamKey, "GrizzlyTimeRamKey");
        }
    }
}
