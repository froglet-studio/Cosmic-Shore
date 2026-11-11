using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Manta's CHARGE row is the shared fuse (Garrett, 2026-11-10): a plant resets the fuse to
    /// <c>fuseSeconds</c> times <c>fuseScale</c> at the planter's Charge. It used to be the petals a bloom knocks out
    /// of a rival, and a petal is a whole unit, too coarse a dial to tune.
    ///
    /// <para>Locks what a generator re-run or a hand edit could silently move: the curve's load-bearing values, that it
    /// is continuous in Charge, how a mode's override composes with it, and the two shipped assets (the fuse curve on
    /// MantaStingConfig.asset, and the bloom's flat bite on MantaBombDebuffByExplosionEffect.asset).</para>
    /// </summary>
    public class MantaFuseChargeTests
    {
        const string StingConfigPath = "Assets/_SO_Assets/VesselActions/Manta/MantaStingConfig.asset";
        const string BombDebuffPath =
            "Assets/_SO_Assets/Effects/Vessel Explosion Effects/MantaBombDebuffByExplosionEffect.asset";

        const float Rest = 0f;        // normalized: Charge 0
        const float Full = 1f;        // normalized: Charge 10
        const float Deficit = -0.5f;  // normalized: Charge -5, the band's floor
        const float Overcharge = 1.5f; // normalized: Charge 15, the band's ceiling

        MantaStingConfigSO _config;

        [SetUp]
        public void Fresh() => _config = ScriptableObject.CreateInstance<MantaStingConfigSO>();

        [TearDown]
        public void Clear()
        {
            MantaBombRules.FuseSecondsOverride = null;
            if (_config) Object.DestroyImmediate(_config);
        }

        [Test]
        public void TheFuseIsTheAuthoredLengthAtRest_AndHalfAgainAtChargeTen()
        {
            Assert.AreEqual(20f, _config.FuseSecondsAtLevel(Rest), 1e-4f, "Charge 0");
            Assert.AreEqual(25f, _config.FuseSecondsAtLevel(0.5f), 1e-4f, "Charge 5");
            Assert.AreEqual(30f, _config.FuseSecondsAtLevel(Full), 1e-4f, "Charge 10");
        }

        [Test]
        public void ADrainedChargeShortensTheFuse_AndOverchargeLengthensIt()
        {
            Assert.AreEqual(15f, _config.FuseSecondsAtLevel(Deficit), 1e-4f, "Charge -5");
            Assert.AreEqual(35f, _config.FuseSecondsAtLevel(Overcharge), 1e-4f, "Charge 15");
        }

        [Test]
        public void TheFuseMovesWithEveryTenthOfALevel_NotInWholePetals()
        {
            // The reason Charge left the petals: between Charge 0 and 10 the fuse takes every value on the way, so
            // each hundredth of the band moves it by a hundredth of the 10 s span, and it never stands still.
            float previous = float.NegativeInfinity;
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                float fuse = _config.FuseSecondsAtLevel(t);
                Assert.AreEqual(20f + 10f * t, fuse, 1e-3f, $"normalized Charge {t:0.00}");
                Assert.Greater(fuse, previous, $"normalized Charge {t:0.00} did not lengthen the fuse");
                previous = fuse;
            }
        }

        [Test]
        public void AModeOverrideReplacesTheRestingLength_AndChargeStillScalesIt()
        {
            MantaBombRules.FuseSecondsOverride = 30f;   // Bloomrush's intensity-1 rung
            Assert.AreEqual(30f, _config.FuseSecondsAtLevel(Rest), 1e-4f, "Charge 0 under the override");
            Assert.AreEqual(45f, _config.FuseSecondsAtLevel(Full), 1e-4f, "Charge 10 under the override");
        }

        [Test]
        public void TheFuseNeverRunsShorterThanOneSecond()
        {
            MantaBombRules.FuseSecondsOverride = 1f;
            Assert.AreEqual(1f, _config.FuseSecondsAtLevel(Deficit), 1e-4f,
                "a 1 s resting fuse at Charge -5 would be 0.75 s; the clamp holds it at 1 s");
        }

        [Test]
        public void WithNoVesselToRead_TheFuseIsTheRestingLength()
        {
            Assert.AreEqual(20f, _config.FuseSecondsFor(null), 1e-4f);
        }

        [Test]
        public void TheShippedConfigRunsTheFuseFrom20sAtRestTo30sAtChargeTen()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<MantaStingConfigSO>(StingConfigPath);
            Assert.IsNotNull(shipped, $"{StingConfigPath} is missing - author_manta_kit_assets.py writes it");
            Assert.AreEqual(20f, shipped.FuseSecondsAtLevel(Rest), 1e-4f, "Charge 0");
            Assert.AreEqual(30f, shipped.FuseSecondsAtLevel(Full), 1e-4f, "Charge 10");
        }

        [Test]
        public void TheShippedBloomTakesFlatPetals_ChargeNoLongerScalesThem()
        {
            var bite = AssetDatabase.LoadAssetAtPath<VesselElementalDebuffByExplosionEffectSO>(BombDebuffPath);
            Assert.IsNotNull(bite, $"{BombDebuffPath} is missing - author_manta_kit_assets.py writes it");

            var atFullCharge = new SerializedObject(bite).FindProperty("magnitudeAtFullCharge");
            Assert.IsNotNull(atFullCharge, "VesselElementalDebuffByExplosionEffectSO.magnitudeAtFullCharge was renamed");
            Assert.AreEqual(1f, atFullCharge.floatValue, 1e-6f,
                "Charge moved from the bloom's petals to the fuse (2026-11-10): the bite must not scale with it");
            Assert.AreEqual(1f, VesselElementalDebuffByExplosionEffectSO.ChargeScale(atFullCharge.floatValue, 10),
                1e-6f, "a Charge 10 Manta's bloom bites exactly as hard as a Charge 0 one");
        }
    }
}
