using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Serpent's fuel pellets refill only while its wall eats its own trail
    /// (R_VesselActions/SERPENT_FUEL_PELLETS.md, "The wall refills the tank"): one pellet per 2 s of
    /// building, nothing on a clock, and a pellet burns 6 s at rest.
    /// </summary>
    public class SerpentPelletRefillTests
    {
        const float MaxGap = 0.4f;
        const float SecondsPerPellet = 2f;
        const float Pellet = 0.25f;

        [Test]
        public void Two_seconds_of_steady_building_pays_one_pellet()
        {
            // Own trail prisms landing every frame at 60 fps for 2 s.
            float fuel = 0f;
            for (int i = 0; i < 120; i++)
                fuel += ConsumeBoostActionExecutor.WallBuildingFuel(1f / 60f, MaxGap, SecondsPerPellet, Pellet);
            Assert.AreEqual(Pellet, fuel, 1e-4f);
        }

        [Test]
        public void A_pause_longer_than_the_gap_pays_nothing()
        {
            Assert.AreEqual(0f, ConsumeBoostActionExecutor.WallBuildingFuel(MaxGap + 0.01f, MaxGap, SecondsPerPellet, Pellet));
            Assert.AreEqual(0f, ConsumeBoostActionExecutor.WallBuildingFuel(float.PositiveInfinity, MaxGap, SecondsPerPellet, Pellet));
        }

        [Test]
        public void Landings_in_the_same_frame_pay_once()
        {
            Assert.AreEqual(0f, ConsumeBoostActionExecutor.WallBuildingFuel(0f, MaxGap, SecondsPerPellet, Pellet));
        }

        [Test]
        public void A_fifth_pellet_tank_still_gets_one_whole_pellet_per_two_seconds()
        {
            float fuel = ConsumeBoostActionExecutor.WallBuildingFuel(0.4f, MaxGap, SecondsPerPellet, 0.2f) * 5f;
            Assert.AreEqual(0.2f, fuel, 1e-4f);
        }

        static string BoostResourceBlock()
        {
            const string prefab = "Assets/_Prefabs/Spacevessels/Serpent.prefab";
            Assert.IsTrue(File.Exists(prefab), $"{prefab} is missing");
            var m = Regex.Match(File.ReadAllText(prefab), @"- Name: Boost\n(?:    .*\n)+");
            Assert.IsTrue(m.Success, "the Serpent's Boost resource was not found");
            return m.Value;
        }

        [Test]
        public void The_shipped_tank_has_no_clock_refill()
        {
            var m = Regex.Match(BoostResourceBlock(), @"^\s+resourceGainRate: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, "resourceGainRate not found on the Boost resource");
            Assert.AreEqual(0f, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                "the Serpent's pellets refill only from its wall eating its trail, never on a clock");
        }

        [Test]
        public void The_shipped_pellet_burns_six_seconds_and_refills_one_per_two_seconds_of_building()
        {
            const string asset = "Assets/_SO_Assets/VesselActions/Serpent/ConsumeBoostAction.asset";
            string text = File.ReadAllText(asset);
            Assert.AreEqual(6f, Read(text, "boostDuration"), 1e-4f);
            Assert.AreEqual(2f, Read(text, "secondsOfWallBuildingPerPellet"), 1e-4f);
        }

        static float Read(string text, string key)
        {
            var m = Regex.Match(text, $@"^\s+{key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{key} not found");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
