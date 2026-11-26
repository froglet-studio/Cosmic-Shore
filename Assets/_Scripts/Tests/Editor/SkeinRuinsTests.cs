using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Skein's garden ruins (SKEIN.md section 20): Spearline's ruins laid from Skein's rings. Only the hardest rung has
    /// them; the lane down every leg and the space round every ring stay clear of every standing ruin; no ruin reaches
    /// the cable (a rail prism or a launch's path) and no wheel sweeps it; a wheel is laid whole or not at all; the
    /// ruins stand on every third leg, seven of them, every kind once; the lay is a pure function of the rings and the
    /// cable; and there is enough left to read as ruins. The shipped cable (the arena prefabs' seed) and six others.
    /// The same file runs headless through <c>Tools/Build/skein_ruins_harness</c>.
    /// </summary>
    public class SkeinRuinsTests
    {
        /// <summary>SpawnableSkein1-4.prefab's seed (their cableSeed is 0, so the seed is the cable's).</summary>
        const int ShippedSeed = 20260909;
        const int Cables = 7;

        sealed class Lay
        {
            public SkeinCourse.SkeinBuild Build;
            public SkeinCourseSettings Settings;
            public List<RaceGate> Rings;
            public readonly List<Vector3> KeepOff = new();
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();
            public readonly List<bool> Laid = new();

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }

            public int LaidCount()
            {
                int n = 0;
                foreach (bool laid in Laid) if (laid) n++;
                return n;
            }

            public HashSet<int> LaidLegs()
            {
                var legs = new HashSet<int>();
                for (int i = 0; i < Parts.Count; i++) if (Laid[i]) legs.Add(Parts[i].Leg);
                return legs;
            }
        }

        static readonly Dictionary<int, SkeinCourse.SkeinBuild> Cache = new();

        /// <summary>The cable the arena lays at this rung from <paramref name="seed"/> (its retry ladder).</summary>
        static SkeinCourse.SkeinBuild Cable(int intensity, int seed)
        {
            int key = seed * 8 + intensity;
            if (Cache.TryGetValue(key, out var build)) return build;
            var settings = SkeinCourseSettings.ForIntensity(intensity);
            for (int attempt = 0; attempt < 6 && build == null; attempt++)
                build = SkeinCourse.BuildAll(unchecked(seed + attempt * 7919), settings);
            Assert.IsNotNull(build, $"I{intensity} seed {seed}: no cable");
            return Cache[key] = build;
        }

        static Lay Build(int intensity, int cable)
        {
            int seed = ShippedSeed + cable;
            var lay = new Lay { Build = Cable(intensity, seed), Settings = SkeinCourseSettings.ForIntensity(intensity) };
            lay.Rings = new List<RaceGate>(lay.Build.Gates.Count);
            foreach (var gate in lay.Build.Gates) lay.Rings.Add(new RaceGate(gate.Position, gate.Axis, gate.Radius));
            Assert.IsTrue(SkeinRuins.TryKeepOff(seed, lay.Settings, Vector3.zero, lay.KeepOff));
            SkeinRuins.Lay(lay.Rings, intensity, lay.KeepOff, lay.Parts, lay.Wheels, lay.Laid);
            return lay;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>The nearest the cable comes to the part's surface (its centre line less half its thickness), with
        /// the part standing at <paramref name="position"/> along <paramref name="forward"/>. Measured from the build
        /// itself, not from the generator's keep-off list: every rail prism, and each launch as one straight segment.</summary>
        static float CableGap(Lay lay, in SpearlineRuinPart part, Vector3 position, Vector3 forward)
        {
            Vector3 half = forward * (0.5f * part.Scale.z);
            Vector3 a = position - half, b = position + half;
            float best = float.MaxValue;
            for (int r = 0; r < lay.Build.Positions.Count; r++)
            {
                var rail = lay.Build.Positions[r];
                for (int k = 0; k < rail.Length; k++) best = Mathf.Min(best, SegmentDistance(rail[k], a, b));
                if (!lay.Build.Rails[r].Launches) continue;
                Vector3 from = rail[rail.Length - 1];
                Vector3 to = from + lay.Build.Rotations[r][rail.Length - 1] * Vector3.forward * lay.Settings.EndAimMax;
                for (int k = 0; k <= 8; k++)
                    best = Mathf.Min(best, SegmentDistance(a + (b - a) * (k / 8f), from, to));
            }
            return best - 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            for (int cable = 0; cable < Cables; cable++)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var lay = Build(i, cable);
                    Assert.AreEqual(0, lay.Parts.Count, $"I{i} cable {cable}");
                    Assert.AreEqual(0, lay.Wheels.Count, $"I{i} cable {cable}");
                }
                Assert.Greater(Build(4, cable).LaidCount(), 0, $"I4 cable {cable}");
            }
        }

        [Test]
        public void The_lane_down_every_leg_is_clear_of_every_standing_ruin()
        {
            for (int cable = 0; cable < Cables; cable++)
            {
                var lay = Build(4, cable);
                for (int i = 0; i < lay.Parts.Count; i++)
                {
                    if (!lay.Laid[i] || lay.Turns(i)) continue;
                    var part = lay.Parts[i];
                    Vector3 half = part.Forward * (0.5f * part.Scale.z);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lay.Rings, part.Position), SpearlineRuins.LaneRadius - 0.01f,
                        $"cable {cable}: a ruin prism stands in a lane");
                    // A short stone is placed by its centre (SpearlineRuins.Stands), as in Spearline.
                    if (part.Scale.z <= 16f) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lay.Rings, part.Position + half), SpearlineRuins.LaneRadius - 0.01f,
                        $"cable {cable}: a ruin prism reaches into a lane");
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lay.Rings, part.Position - half), SpearlineRuins.LaneRadius - 0.01f,
                        $"cable {cable}: a ruin prism reaches into a lane");
                }
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            for (int cable = 0; cable < Cables; cable++)
            {
                var lay = Build(4, cable);
                for (int i = 0; i < lay.Parts.Count; i++)
                {
                    if (!lay.Laid[i]) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lay.Rings, lay.Parts[i].Position),
                        SpearlineRuins.RingMargin - 0.01f, $"cable {cable}: a ruin prism stands at a ring");
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_pinned_mouth()
        {
            // A pilot who threads a pinned ring must not be asked for a narrower pass between rings.
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, SkeinCourseSettings.ForIntensity(4).GateMouthMax);
        }

        [Test]
        public void No_standing_ruin_reaches_the_cable()
        {
            for (int cable = 0; cable < Cables; cable++)
            {
                var lay = Build(4, cable);
                for (int i = 0; i < lay.Parts.Count; i++)
                {
                    if (!lay.Laid[i] || lay.Turns(i)) continue;
                    // 1 u for the generator sampling the prism's length and the launch's path in steps.
                    Assert.GreaterOrEqual(CableGap(lay, lay.Parts[i], lay.Parts[i].Position, lay.Parts[i].Forward),
                        SkeinRuins.CableKeep - 1f, $"cable {cable}: a ruin prism stands on the cable");
                }
            }
        }

        [Test]
        public void The_keep_is_over_the_ride_envelope_the_cable_is_proved_against()
        {
            Assert.Greater(SkeinRuins.CableKeep, SkeinCourseSettings.ForIntensity(4).RayClearance);
        }

        [Test]
        public void A_wheel_is_whole_or_absent_and_never_sweeps_the_cable()
        {
            for (int cable = 0; cable < Cables; cable++)
            {
                var lay = Build(4, cable);
                foreach (var wheel in lay.Wheels)
                {
                    bool first = lay.Laid[wheel.First];
                    for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                        Assert.AreEqual(first, lay.Laid[i], $"cable {cable}: a wheel laid in part");
                    if (!first) continue;
                    // A whole turn at 25 deg/s is 14.4 s.
                    for (float seconds = 0f; seconds < 14.4f; seconds += 0.3f)
                    {
                        for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                        {
                            SpearlineRuins.Pose(lay.Parts[i], wheel, seconds, out var position, out var forward, out _);
                            Assert.Greater(CableGap(lay, lay.Parts[i], position, forward), 0f,
                                $"cable {cable}: a wheel sweeps the cable at {seconds} s");
                        }
                    }
                }
            }
        }

        [Test]
        public void A_rail_in_a_wheels_sweep_is_seen_at_any_angle()
        {
            // Few cables leave room for a wheel, so the sweep rule is held on a wheel of one spoke stone.
            var wheel = new SpearlineRuinWheel(new Vector3(100f, 0f, 0f), Vector3.forward, 25f, 0, 1);
            var stone = new SpearlineRuinPart(new Vector3(200f, 0f, 0f), Vector3.right, Vector3.forward, new Vector3(11f, 11f, 29f));
            SpearlineRuins.Pose(stone, wheel, 120f / 25f, out var where, out _, out _);
            Assert.IsTrue(SkeinRuins.SweepReachesCable(stone, wheel, new List<Vector3> { where }, SkeinRuins.CableKeep),
                "a rail where the stone will be a third of a turn on");
            Assert.IsFalse(SkeinRuins.SweepReachesCable(stone, wheel, new List<Vector3> { new Vector3(100f, 0f, 0f) }, SkeinRuins.CableKeep),
                "a rail through the hub");
            Assert.IsFalse(SkeinRuins.SweepReachesCable(stone, wheel, new List<Vector3> { where + Vector3.forward * 80f }, SkeinRuins.CableKeep),
                "a rail well past the wheel's plane");
        }

        [Test]
        public void Seven_ruins_stand_on_every_third_leg_every_kind_once()
        {
            int ruins = SpearlineRuins.ForIntensity(4).PerLap;
            Assert.AreEqual(SpearlineRuins.KindCount, ruins);
            for (int cable = 0; cable < Cables; cable++)
            {
                var lay = Build(4, cable);
                var kinds = new HashSet<int>();
                foreach (int leg in lay.LaidLegs())
                {
                    Assert.AreEqual(0, leg % SkeinRuins.LegStride, $"cable {cable}: a ruin on leg {leg}");
                    Assert.Less(SkeinRuins.RuinOf(leg), ruins, $"cable {cable}: an eighth ruin on leg {leg}");
                    Assert.IsTrue(kinds.Add(SpearlineRuins.KindOf(leg, lay.Rings[0].Position)), $"cable {cable}: a kind twice");
                }
            }
            Assert.AreEqual(ruins, Build(4, 0).LaidLegs().Count, "the shipped cable carries every ruin");
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_rings_and_the_cable()
        {
            for (int cable = 0; cable < Cables; cable++)
            {
                var first = Build(4, cable);
                var second = Build(4, cable);
                Assert.AreEqual(first.Parts.Count, second.Parts.Count);
                Assert.AreEqual(first.Wheels.Count, second.Wheels.Count);
                for (int i = 0; i < first.Parts.Count; i++)
                {
                    Assert.AreEqual(first.Parts[i].Position, second.Parts[i].Position);
                    Assert.AreEqual(first.Parts[i].Scale, second.Parts[i].Scale);
                    Assert.AreEqual(first.Laid[i], second.Laid[i]);
                }
            }
        }

        [Test]
        public void There_is_enough_left_to_read_as_ruins()
        {
            // Measured (skein_ruins_harness --measure): the shipped cable keeps 215 ruin prisms of 298 on 7 legs, the
            // six cables after it 198 to 279, each on 7 legs. No cable leaves room for a wheel (a pergola stands in for it).
            Assert.GreaterOrEqual(Build(4, 0).LaidCount(), FloorShipped, "the shipped cable");
            for (int cable = 0; cable < Cables; cable++)
            {
                var lay = Build(4, cable);
                Assert.GreaterOrEqual(lay.LaidLegs().Count, FloorLegs, $"cable {cable}: legs with a ruin");
                Assert.GreaterOrEqual(lay.LaidCount(), FloorAny, $"cable {cable}: ruin prisms");
            }
        }

        const int FloorShipped = 200, FloorLegs = 7, FloorAny = 180;

        [Test]
        public void The_shipped_ruins_are_the_mass_the_phase_ladder_counts()
        {
            // Every laid prism counts in Cell.LiveVolume, so Tools/Build/author_skein_assets.py adds these two numbers
            // (its RUINS table) to the I4 cell's phase baseline. If this fails the ruins changed: re-measure
            // (skein_ruins_harness --measure, cable 0), put the new numbers here AND there, and re-run the generator.
            const int prisms = 215;
            const float volume = 439125f;
            var lay = Build(4, 0);
            float laidVolume = 0f;
            for (int i = 0; i < lay.Parts.Count; i++)
                if (lay.Laid[i]) laidVolume += lay.Parts[i].Scale.x * lay.Parts[i].Scale.y * lay.Parts[i].Scale.z;
            Assert.AreEqual(prisms, lay.LaidCount());
            Assert.AreEqual(volume, laidVolume, 0.005f * volume);
        }
    }
}
