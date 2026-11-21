using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Orca helm's charge (Garrett 2026-11-20: "the helm seat should be able to join the fight by charging things like
    /// a real orca"; Crew/ORCA_CREW.md "The helm's charge"): the shipped config charges faster than the old x5 burn, rams
    /// rival hulls inside one radius (a Strike only: Garrett chose no refill and no plough) and lets its AI charge prey.
    /// </summary>
    public class OrcaChargeTests
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/OrcaCrew/OrcaCrewConfig.asset";

        static OrcaCrewConfigSO Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<OrcaCrewConfigSO>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            return config;
        }

        [Test]
        public void TheShippedChargeOutrunsTheOldBurn()
        {
            var config = Config();
            Assert.Greater(config.ChargeMultiplier, 5f, "the charge is no faster than the x5 burn it replaced");
            Assert.Greater(config.BoostTank, 0f);
        }

        [Test]
        public void TheChargeRamsRivalsWithinOneRadius()
        {
            var config = Config();
            Assert.Greater(config.ChargeRamReach, 0f, "the charge rams nothing");
            Assert.Less(config.ChargeRamReach, config.HullRadius * 2f, "a ram reaching past two hull radii is not a ram");
            Assert.Greater(config.ChargeRamPetals, 0f, "a ram that takes no petals scores nothing");
            Assert.Greater(config.ChargeRamCooldown, 0f, "a ram with no cooldown strikes every frame of contact");
            Assert.Greater(config.AIRamRange, 0f, "the AI helm never charges prey");
            Assert.IsNotNull(config.OnCombatHitLanded, "a ram is a scored hit, so it needs the hit channel");
        }
    }
}
