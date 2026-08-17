#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// SkimmerAdjustElementLevelByCrystalEffectSO Tests - Validates the elemental crystal powerup.
    ///
    /// WHY THIS MATTERS:
    /// The four elemental crystals (Charge / Mass / Space / Time) are powerups that increase
    /// the collecting vessel's matching element level proportionally to the crystal's scale.
    /// If the scale → level mapping or the elemental gating drifts, crystal pickups silently
    /// stop powering up vessels (or over-buff them), breaking elemental progression in
    /// HexRace, Wildlife Blitz, and freestyle.
    /// </summary>
    [TestFixture]
    public class SkimmerAdjustElementLevelByCrystalEffectTests
    {
        SkimmerAdjustElementLevelByCrystalEffectSO _effect;

        [SetUp]
        public void SetUp()
        {
            _effect = ScriptableObject.CreateInstance<SkimmerAdjustElementLevelByCrystalEffectSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_effect);
        }

        #region Default Values

        [Test]
        public void Default_LevelPerUnitScale_IsOneIntegerLevelPerUnit()
        {
            // 0.1 normalized = one integer level (one petal tick) per unit of crystal scale.
            Assert.AreEqual(0.1f, _effect.LevelPerUnitScale, 0.0001f,
                "Default level-per-unit-scale should be 0.1 (one integer level per unit of scale).");
        }

        [Test]
        public void Default_MaxLevelGainPerCrystal_CapsAtHalfRange()
        {
            Assert.AreEqual(0.5f, _effect.MaxLevelGainPerCrystal, 0.0001f,
                "Default max gain should be 0.5 so a single crystal can't max an element.");
        }

        [Test]
        public void Default_MaxGain_IsGreaterThanPerUnitGain()
        {
            Assert.Greater(_effect.MaxLevelGainPerCrystal, _effect.LevelPerUnitScale,
                "Cap must exceed the single-unit gain or scale would never matter.");
        }

        #endregion

        #region Scale → Level Mapping

        [Test]
        public void ComputeLevelGain_ScalesLinearlyWithCrystalScale()
        {
            // Typical flora/fauna crystal scales: tadpole ~1.3, gyroid ~4.
            Assert.AreEqual(0.1f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(1f, 0.1f, 0.5f), 0.0001f);
            Assert.AreEqual(0.13f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(1.3f, 0.1f, 0.5f), 0.0001f);
            Assert.AreEqual(0.3f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(3f, 0.1f, 0.5f), 0.0001f);
            Assert.AreEqual(0.4f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(4f, 0.1f, 0.5f), 0.0001f);
        }

        [Test]
        public void ComputeLevelGain_BiggerCrystal_GrantsBiggerBoost()
        {
            float small = SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(1.3f, 0.1f, 0.5f);
            float large = SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(4f, 0.1f, 0.5f);

            Assert.Greater(large, small,
                "A larger crystal must grant a larger element level boost.");
        }

        [Test]
        public void ComputeLevelGain_IsCappedAtMaxGain()
        {
            // AssembledFlora grows crystals every spawn cycle - runaway scale must not
            // grant more than the configured cap.
            Assert.AreEqual(0.5f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(5f, 0.1f, 0.5f), 0.0001f);
            Assert.AreEqual(0.5f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(50f, 0.1f, 0.5f), 0.0001f);
        }

        [Test]
        public void ComputeLevelGain_NegativeScale_UsesMagnitude()
        {
            // Mirrored/flipped transforms produce negative lossy scale; the powerup
            // must never become a debuff because of that.
            Assert.AreEqual(0.2f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(-2f, 0.1f, 0.5f), 0.0001f);
        }

        [Test]
        public void ComputeLevelGain_ZeroScale_GrantsNothing()
        {
            Assert.AreEqual(0f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(0f, 0.1f, 0.5f), 0.0001f);
        }

        #endregion

        #region Elemental Gating

        [Test]
        public void CrystalProperties_AllFourElementalTypes_AreElemental()
        {
            // Execute() gates on IsElemental - all four powerup crystals must pass it.
            foreach (var element in new[] { Element.Charge, Element.Mass, Element.Space, Element.Time })
            {
                var properties = new CrystalProperties { Element = element };
                Assert.IsTrue(properties.IsElemental,
                    $"{element} crystal must be elemental so it grants its powerup.");
            }
        }

        [Test]
        public void CrystalProperties_NoneAndOmni_AreNotElemental()
        {
            // Omni / unset crystals must not feed the element-specific powerup path.
            foreach (var element in new[] { Element.None, Element.Omni })
            {
                var properties = new CrystalProperties { Element = element };
                Assert.IsFalse(properties.IsElemental,
                    $"{element} crystal must not grant an element-specific powerup.");
            }
        }

        #endregion
    }
}
#endif
