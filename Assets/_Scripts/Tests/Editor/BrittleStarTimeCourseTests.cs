using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Brittle Star Time's ladder: four intensities, four MOVEMENT questions (BrittleStarTimeCourse). Each rung is tested
    /// against the claim it makes about Speed Mode and the turn, in the hull's own numbers, which are read back off the
    /// shipped assets. The liveness half (a closed lap of rings inside the shell, gate 0 on the spawn pole, no mouths
    /// overlapping) holds on every rung over 400 seeds.
    /// </summary>
    public class BrittleStarTimeCourseTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 400;

        static List<RaceGate> Lap(int intensity, int seed)
        {
            if (BrittleStarTimeCourse.BuildLap(intensity, seed, Inner, Outer, BrittleStarTimeCourse.GatesPerLap, out var lap))
                return lap;
            return HeadlongCircuit.Generate(seed, GearboxSettings());
        }

        static HeadlongCircuitSettings GearboxSettings()
        {
            var s = BrittleStarTimeCourse.Gearbox();
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return s;
        }

        static float CornerRadius(List<RaceGate> c, int i)
        {
            int n = c.Count;
            return RaceCourseGeometry.CornerRadius(c[(i - 1 + n) % n].Position, c[i].Position, c[(i + 1) % n].Position);
        }

        static string Asset(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            return File.ReadAllText(path);
        }

        static float AssetFloat(string path, string key)
        {
            var m = Regex.Match(Asset(path), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        const string Prefab = "Assets/_Prefabs/Spacevessels/BrittleStar.prefab";
        const string Config = "Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset";
        const string Why = " moved - BrittleStarTimeCourse is cut against it; update the constant and re-run the sweep.";

        [Test]
        public void Course_constants_match_the_shipped_BrittleStar()
        {
            Assert.AreEqual(BrittleStarTimeCourse.ThrottleScaler, AssetFloat(Prefab, "DefaultThrottleScaler"), 1e-4f, "DefaultThrottleScaler" + Why);
            Assert.AreEqual(BrittleStarTimeCourse.MinimumSpeed, AssetFloat(Prefab, "DefaultMinimumSpeed"), 1e-4f, "DefaultMinimumSpeed" + Why);
            Assert.AreEqual(BrittleStarTimeCourse.RotationThrottleScaler, AssetFloat(Prefab, "RotationThrottleScaler"), 1e-4f, "RotationThrottleScaler" + Why);
            Assert.AreEqual(BrittleStarTimeCourse.TurnScaler, Mathf.Min(AssetFloat(Prefab, "PitchScaler"), AssetFloat(Prefab, "YawScaler")), 1e-4f, "Pitch/YawScaler" + Why);
            var m = Regex.Match(Asset(Config), @"speedModeMultiplier:\s*\n\s+Enabled: \d\s*\n\s+Value: ([0-9.]+)\s*\n\s+Min: [0-9.]+\s*\n\s+Max: ([0-9.]+)");
            Assert.IsTrue(m.Success, "speedModeMultiplier Value/Max not found");
            Assert.AreEqual(BrittleStarTimeCourse.SpeedModeMultiplier, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-4f, "speedModeMultiplier.Value" + Why);
            Assert.AreEqual(BrittleStarTimeCourse.SpeedModeMultiplierAtTime10, float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-4f, "speedModeMultiplier.Max" + Why);
            Assert.AreEqual(BrittleStarTimeCourse.SpeedRollPullDegreesPerSecond, AssetFloat(Config, "speedRollPullDegreesPerSecond"), 1e-4f, "speedRollPullDegreesPerSecond" + Why);
        }

        [Test]
        public void Every_rung_lays_its_own_shape_and_the_Gearbox_is_the_fallback()
        {
            Assert.IsFalse(BrittleStarTimeCourse.BuildLap(1, 1, Inner, Outer, BrittleStarTimeCourse.GatesPerLap, out _),
                "I1 is the Gearbox - Headlong's solver, built by the course source");
            for (int intensity = 2; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                    Assert.IsTrue(BrittleStarTimeCourse.BuildLap(intensity, seed, Inner, Outer, BrittleStarTimeCourse.GatesPerLap, out _),
                        $"i{intensity} seed {seed}: the rung's shape must fit the shipped shell");
            Assert.IsFalse(BrittleStarTimeCourse.BuildLap(3, 1, Inner, Outer, 9, out _), "a lap that is not 8 rings flies the Gearbox");
            Assert.IsFalse(BrittleStarTimeCourse.BuildLap(4, 1, 900f, 1000f, BrittleStarTimeCourse.GatesPerLap, out _),
                "a shell the shape cannot fit flies the Gearbox");
        }

        [Test]
        public void Every_intensity_yields_a_closed_lap_of_rings_inside_the_shell()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                float mouth = BrittleStarTimeCourse.MouthRadius(intensity);
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Lap(intensity, seed);
                    Assert.AreEqual(BrittleStarTimeCourse.GatesPerLap, c.Count, $"i{intensity} seed {seed}: gate count");
                    Assert.Less(RaceCourseGeometry.Angle(c[0].Position, Vector3.up), 0.1f, $"i{intensity}: gate 0 is off the spawn pole");
                    for (int i = 0; i < c.Count; i++)
                    {
                        Assert.AreEqual(mouth, c[i].Radius, 1e-4f, $"i{intensity}: ring {i}'s mouth");
                        Assert.AreEqual(1f, c[i].Axis.magnitude, 1e-3f, $"i{intensity}: ring {i}'s axis is not unit");
                        float r = c[i].Position.magnitude;
                        Assert.GreaterOrEqual(r, Inner - 0.01f, $"i{intensity} seed {seed}: ring {i} inside the nucleus");
                        Assert.LessOrEqual(r, Outer + 0.01f, $"i{intensity} seed {seed}: ring {i} outside the membrane");
                        for (int j = i + 1; j < c.Count; j++)
                            Assert.GreaterOrEqual((c[i].Position - c[j].Position).magnitude, 2f * mouth - 0.01f,
                                $"i{intensity} seed {seed}: rings {i} and {j} overlap");
                    }
                }
            }
        }

        [Test]
        public void Every_ring_faces_on_along_the_lap()
        {
            // A ring faces the way the course flows: its axis never points back, away from the next ring.
            for (int intensity = 2; intensity <= 4; intensity++)
                for (int seed = 1; seed <= 40; seed++)
                {
                    var c = Lap(intensity, seed);
                    for (int i = 0; i < c.Count; i++)
                    {
                        Vector3 next = c[(i + 1) % c.Count].Position - c[i].Position;
                        // An apex ring of a U-turn faces across, square to the chord to the next apex: never backwards.
                        Assert.GreaterOrEqual(Vector3.Dot(c[i].Axis, next.normalized), -1e-3f, $"i{intensity} seed {seed}: ring {i} faces backwards");
                    }
                }
        }

        [Test]
        public void I1_Gearbox_asks_hold_Speed_or_shift_down_twice_a_lap()
        {
            var s = GearboxSettings();
            Assert.Greater(s.CornerFloorRadius, 1.5f * BrittleStarTimeCourse.CruiseRadius,
                "the floor must sit well above the cruise circle, so a pilot who shifts down holds every corner");
            var counts = new List<int>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Lap(1, seed);
                for (int i = 0; i < c.Count; i++)
                    Assert.GreaterOrEqual(CornerRadius(c, i), s.CornerFloorRadius - 0.01f, $"seed {seed}: corner {i} is sharper than the floor");
                counts.Add(Enumerable.Range(0, c.Count).Count(i => CornerRadius(c, i) < BrittleStarTimeCourse.SpeedModeRadius));
            }
            counts.Sort();
            Assert.AreEqual(2, counts[counts.Count / 2], "a median Gearbox lap has two corners Speed Mode cannot hold");
        }

        [Test]
        public void I2_Feather_slalom_bends_between_the_cruise_circle_and_Speed_Modes()
        {
            float mouth = BrittleStarTimeCourse.MouthRadius(2);
            var lap = BrittleStarTimeCourse.Slalom();
            for (int i = 0; i < lap.Count; i++)
            {
                // Alternately outside and inside the loop: a weave, not a ring.
                float off = (lap[i].Position - LoopPoint(i)).magnitude;
                Assert.AreEqual(BrittleStarTimeCourse.SlalomAmplitude, off, 0.5f, $"ring {i} is not on the weave");
            }
            // Even brushing every rim the weave is tighter than Speed Mode's circle: flat out never flies it.
            float rimLine = BrittleStarTimeCourse.SlalomBendRadius(mouth);
            Assert.Less(rimLine, BrittleStarTimeCourse.SpeedModeRadius, $"rim line {rimLine:F0} u: Speed Mode would hold the weave");
            // Through the half-mouth line the pilot actually flies it is wider than the cruise circle, but well under Speed
            // Mode's: the speed that holds it sits between the two, which is what feathering A reaches.
            float flown = BrittleStarTimeCourse.SlalomBendRadius(0.5f * mouth);
            Assert.Greater(flown, 1.5f * BrittleStarTimeCourse.CruiseRadius, $"flown line {flown:F0} u: too tight even at cruise");
            float feather = BrittleStarTimeCourse.SpeedForRadius(flown);
            Assert.Greater(feather, 2f * BrittleStarTimeCourse.CruiseSpeed, $"feather speed {feather:F0} u/s: cruise would be enough");
            Assert.Less(feather, 0.75f * BrittleStarTimeCourse.SpeedModeSpeed, $"feather speed {feather:F0} u/s: Speed Mode would be enough");
        }

        static Vector3 LoopPoint(int i)
        {
            float t = 2f * Mathf.PI * i / BrittleStarTimeCourse.GatesPerLap, r = BrittleStarTimeCourse.SlalomLoopRadius;
            return new Vector3(r * Mathf.Sin(t), r * (1f - Mathf.Cos(t)), 0f);
        }

        [Test]
        public void I3_Coast_in_hairpins_need_Speed_Mode_let_go_before_each_turn()
        {
            float mouth = BrittleStarTimeCourse.MouthRadius(3);
            float flyable = BrittleStarTimeCourse.HairpinFlyableRadius(mouth);
            Assert.Less(flyable, BrittleStarTimeCourse.SpeedModeRadius, "Speed Mode would hold the hairpin");
            Assert.Greater(0.5f * BrittleStarTimeCourse.HairpinWidth, BrittleStarTimeCourse.CruiseRadius,
                "the hairpin must be flyable at cruise through its centre");
            // There is no brake: the only way down is to let go and coast. The coast must be long enough to be a decision
            // and short enough that the straight is still mostly flown in Speed Mode.
            float apexSpeed = BrittleStarTimeCourse.SpeedForRadius(flyable);
            float coast = BrittleStarTimeCourse.CoastDistance(BrittleStarTimeCourse.SpeedModeSpeed, apexSpeed);
            Assert.Greater(coast, 80f, $"coast {coast:F0} u: letting go at the turn would do");
            Assert.Less(coast, BrittleStarTimeCourse.HairpinStraight / 3f, $"coast {coast:F0} u: the straight is all coasting");
            // The rings: three down each straight, one at each apex facing across.
            var lap = BrittleStarTimeCourse.Hairpins(1f);
            Assert.AreEqual(1f, Mathf.Abs(lap[3].Axis.y), 1e-4f, "the far apex ring faces across the hairpin");
            Assert.AreEqual(1f, Mathf.Abs(lap[7].Axis.y), 1e-4f, "the near apex ring faces across the hairpin");
        }

        [Test]
        public void I4_Corkscrew_holds_at_Speed_only_rolling()
        {
            float mouth = BrittleStarTimeCourse.MouthRadius(4);
            float rolling = BrittleStarTimeCourse.SpeedRollRadiusAtTime5, flat = BrittleStarTimeCourse.SpeedModeRadiusAtTime5;
            Assert.Less(rolling + 5f, flat, "Speed Roll must tighten Speed Mode's turn");
            // Every line through the coil, rim to centre, is tighter than Speed Mode's circle at Time 5 and wider than
            // Speed Mode rolling and pulling: holding Speed through it takes the roll.
            for (float slack = 0f; slack <= mouth; slack += mouth / 4f)
            {
                float bend = BrittleStarTimeCourse.CoilBendRadius(slack);
                Assert.Less(bend, flat, $"slack {slack:F0}: bend {bend:F0} u is wide enough to fly flat");
                Assert.Greater(bend, rolling + 10f, $"slack {slack:F0}: bend {bend:F0} u is too tight even rolling");
            }
            // The coil is a coil to any line through it: a pilot brushing the rims is still well off the axis, and the rings
            // sample it a quarter turn apart, so no lazier spiral threads them instead.
            Assert.Greater(BrittleStarTimeCourse.CoilRadius - mouth, 40f, "a straight line along the axis threads the coil");
            Assert.LessOrEqual(BrittleStarTimeCourse.CoilStepDegrees, 90f, "a coarser step aliases into a lazier spiral");
            // The return leg is flown flat out.
            Assert.Greater(BrittleStarTimeCourse.ReturnRadius, flat, "the return leg must hold Speed Mode at Time 5");
        }

        [Test]
        public void I4_starts_the_hull_at_Time_5_where_Speed_Roll_is_live()
        {
            string card = Asset("Assets/_SO_Assets/Games/ArcadeGameBrittleStarTime.asset");
            var m = Regex.Match(card, @"StartingElements:\s*\n\s+- Class: 16\s*\n\s+Intensity: 4\s*\n\s+Levels:\s*\n(?:\s+\w+: [-0-9.]+\s*\n)*?\s+Time: ([-0-9.]+)");
            Assert.IsTrue(m.Success, "the card has no intensity-4 starting-elements row for the Brittle Star (class 16)");
            float time = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(0.5f, time, 1e-4f, "I4 must start at Time 5 (0.5): Speed Roll unlocks at level 5");
            var map = Asset("Assets/Resources/ElementalAbilityMaps/BrittleStar.asset");
            Assert.IsTrue(Regex.IsMatch(map, @"- Element: 4\s*\n(?:\s+[^\n]*\n)*?\s+UnlockLevel: 5"),
                "Time's upgrade must unlock at level 5");
            Assert.IsTrue(Regex.IsMatch(map, @"UpgradeLabel: Speed Roll"), "Time 5 must still be Speed Roll");
        }

        [Test]
        public void Mouths_shrink_level_by_level()
        {
            for (int i = 2; i <= 4; i++)
                Assert.Less(BrittleStarTimeCourse.MouthRadius(i), BrittleStarTimeCourse.MouthRadius(i - 1), $"i{i}: mouths must tighten");
        }
    }
}
