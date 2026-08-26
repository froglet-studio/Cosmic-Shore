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
    ///
    /// <para>A LIFEFORM HEART is the biggest single consumer of this mapping, and since
    /// Docs/ECOSYSTEM.md §39 its scale is AUTHORED PER LIFEFORM (per species × element) rather
    /// than produced by a level curve - the retired one was 3.5 world at level 1, ×1.05 per
    /// level. The shipped authored band is <b>1.04 (SchwarzP Charge) … 4.60 (Shark)</b>, so
    /// every heart pays 0.104 … 0.460 element levels and the whole band sits UNDER the 0.5 cap
    /// (which saturates at world scale 5.0). Bigger lifeform, bigger heart, bigger reward - and
    /// nothing clips, which is the property <c>LifeformHeartSizeTests</c> pins against the
    /// assets. This suite pins the pure mapping the band rides on.</para>
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
            // The unit case, then the two ends of the authored lifeform heart band and a
            // point inside it. A heart at world scale 1 pays exactly one integer element
            // level; the smallest shipped heart pays 0.104 and the largest 0.460.
            Assert.AreEqual(0.1f,
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(1f, 0.1f, 0.5f), 0.0001f);
            Assert.AreEqual(0.1041f,                                       // SchwarzP Charge, the smallest
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(1.041f, 0.1f, 0.5f), 0.0001f);
            Assert.AreEqual(0.2666f,                                       // Brittlestar, mid-band
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(2.666f, 0.1f, 0.5f), 0.0001f);
            Assert.AreEqual(0.46f,                                         // Shark, the largest
                SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(4.6f, 0.1f, 0.5f), 0.0001f);
        }

        [Test]
        public void ComputeLevelGain_BiggerCrystal_GrantsBiggerBoost()
        {
            // Since §39 this IS the ecology's reward gradient: heart size is authored from
            // the lifeform's own measured body, so a bigger kill pays more.
            float small = SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(1.041f, 0.1f, 0.5f);
            float large = SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(4.6f, 0.1f, 0.5f);

            Assert.Greater(large, small,
                "A larger crystal must grant a larger element level boost.");
        }

        [Test]
        public void ComputeLevelGain_IsCappedAtMaxGain()
        {
            // The cap is the backstop for a crystal that is NOT a lifeform heart (the
            // Wanderway conveyor's pickups, Dog Fight's arena scatter) or for an authored
            // heart that escapes its band. No shipped heart reaches it - the authored band
            // tops out at 4.60 against saturation at 5.0 - and that headroom is what
            // ElementalCrystalSetSO.MaxSafeHeartWorldScale exists to hold.
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
