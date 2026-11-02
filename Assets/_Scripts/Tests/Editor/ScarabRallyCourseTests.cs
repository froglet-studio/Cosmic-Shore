using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab Rally course contract: Switchback's walk on the Scarab's cut
    /// (<see cref="ScarabRallyCourse"/>) generates at every intensity, its hairpins are where the
    /// ladder says, and the vessel numbers the cut is stated against are still the shipped Scarab's.
    /// Swept over 400 seeds, because a generated course is fine on the seed you looked at.
    /// </summary>
    public class ScarabRallyCourseTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Gates = 24;   // EndConditionOverridesSO.DefaultScarabRallyGateTarget
        const int Seeds = 400;

        static SwitchbackCourseSettings Settings(int intensity)
        {
            var s = ScarabRallyCourse.ForIntensity(intensity);
            s.GateCount = Gates;
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            s.FirstGateDistance = 620f;
            return s;
        }

        static List<RaceGate> Course(int intensity, int seed) => SwitchbackCourse.Generate(seed, Settings(intensity));

        static float AssetFloat(string path, string key)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing");
            var m = Regex.Match(File.ReadAllText(path), $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        static float FieldInitializer(string path, string field)
        {
            var m = Regex.Match(File.ReadAllText(path), $@"float {field} = ([0-9.]+)f;");
            Assert.IsTrue(m.Success, $"{field} initializer not found in {path}");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void Course_constants_match_the_shipped_Scarab()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/Scarab.prefab";
            const string transformer = "Assets/_Scripts/Controller/Vessel/ScarabVesselTransformer.cs";
            const string why = " moved - ScarabRallyCourse is cut against it; update the constant and re-read SCARABRALLY.md.";
            Assert.AreEqual(ScarabRallyCourse.TurnScaler,
                Mathf.Min(AssetFloat(prefab, "PitchScaler"), AssetFloat(prefab, "YawScaler")), 1e-4f, "Scarab Pitch/YawScaler" + why);
            Assert.AreEqual(ScarabRallyCourse.RotationThrottleScaler, AssetFloat(prefab, "RotationThrottleScaler"), 1e-4f, "RotationThrottleScaler" + why);
            Assert.AreEqual(ScarabRallyCourse.JukeSpeed, AssetFloat(prefab, "jukeSpeed"), 1e-4f, "jukeSpeed" + why);
            Assert.AreEqual(ScarabRallyCourse.JukeDurationSeconds, AssetFloat(prefab, "jukeDurationSeconds"), 1e-4f, "jukeDurationSeconds" + why);
            Assert.AreEqual(ScarabRallyCourse.AccelerationPerSecond, FieldInitializer(transformer, "accelerationPerSecond"), 1e-4f, "accelerationPerSecond" + why);
            Assert.AreEqual(ScarabRallyCourse.CoastDragPerSecond, FieldInitializer(transformer, "coastDragPerSecond"), 1e-4f, "coastDragPerSecond" + why);
            Assert.AreEqual(ScarabRallyCourse.TopSpeed, FieldInitializer(transformer, "baseTopSpeed"), 1e-4f, "baseTopSpeed" + why);
        }

        [Test]
        public void Full_throttle_circle_is_about_a_hundred_units()
        {
            Assert.AreEqual(101.8f, ScarabRallyCourse.FullThrottleRadius, 0.5f);
            Assert.AreEqual(40f, ScarabRallyCourse.JukeOffset, 0.01f);
        }

        [Test]
        public void Every_intensity_generates_the_whole_course()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Course(intensity, seed);
                    Assert.IsNotNull(c, $"i{intensity} seed {seed}: the walk failed");
                    Assert.AreEqual(Gates, c.Count, $"i{intensity} seed {seed}: gate count");
                }
        }

        [Test]
        public void Mouths_shrink_without_a_cliff_and_stay_jukeable()
        {
            for (int i = 2; i <= 4; i++)
            {
                float prev = ScarabRallyCourse.RingRadiusForIntensity(i - 1), cur = ScarabRallyCourse.RingRadiusForIntensity(i);
                Assert.Less(cur, prev, $"i{i}: mouths must tighten");
                Assert.GreaterOrEqual(cur / prev, 0.68f, $"i{i}: a step tighter than 0.68x is a cliff");
            }
            Assert.Greater(ScarabRallyCourse.RingRadiusForIntensity(4), 0.5f * ScarabRallyCourse.JukeOffset,
                "the tightest mouth must stay wider than half a juke, so one juke always puts you in it");
        }

        [Test]
        public void Hairpins_appear_from_intensity_two_and_are_tighter_than_the_full_throttle_circle()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var s = Settings(intensity);
                int hairpins = 0, corners = 0;
                float shortestLeg = float.MaxValue;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var c = Course(intensity, seed);
                    for (int g = 1; g < c.Count - 1; g++)
                    {
                        float turn = RaceCourseGeometry.Angle(c[g].Position - c[g - 1].Position, c[g + 1].Position - c[g].Position);
                        corners++;
                        if (turn >= 89f) hairpins++;
                        shortestLeg = Mathf.Min(shortestLeg, (c[g].Position - c[g - 1].Position).magnitude);
                    }
                }
                if (intensity == 1) Assert.AreEqual(0, hairpins, "I1 teaches the throttle: no hairpins");
                else Assert.Greater(hairpins, corners / 5, $"i{intensity}: the drift is not demanded");
                // A lift can always be wound back up before the next ring: 216^2 / (2 x 90) = 259 u.
                float windUp = ScarabRallyCourse.TopSpeed * ScarabRallyCourse.TopSpeed / (2f * ScarabRallyCourse.AccelerationPerSecond);
                Assert.Greater(shortestLeg, windUp, $"i{intensity}: a leg shorter than a wind-up makes a lift cost the next ring");
                Assert.GreaterOrEqual(s.MinStep, 360f, "the AI needs ~360 u of approach");
            }
        }
    }
}
