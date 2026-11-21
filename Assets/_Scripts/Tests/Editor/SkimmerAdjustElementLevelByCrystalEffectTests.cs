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
    /// The four elemental crystals (Charge / Mass / Space / Time) are powerups that raise the
    /// collecting vessel's matching element level by exactly ONE PETAL (Garrett, 2026-10-26:
    /// every elemental crystal is one size and one petal). If the gain or the elemental gating
    /// drifts, crystal pickups silently stop powering up vessels (or over-buff them), and a
    /// petal knocked out of a hull stops being returned whole.
    ///
    /// <para>The reward used to be <c>lossyScale x levelPerUnitScale</c>, capped at 0.5, so a
    /// heart paid what its lifeform's authored size said. Both fields were retired with that
    /// mapping; <c>ElementalCrystalSizeTests</c> holds the one size.</para>
    /// </summary>
    [TestFixture]
    public class SkimmerAdjustElementLevelByCrystalEffectTests
    {
        #region One Petal

        [Test]
        public void LevelGainPerCrystal_IsExactlyOnePetal()
        {
            Assert.AreEqual(ResourceSystem.PetalNormalized,
                SkimmerAdjustElementLevelByCrystalEffectSO.LevelGainPerCrystal, 1e-7f,
                "Every elemental crystal grants exactly one petal (one integer element level).");
            Assert.AreEqual(0.1f, SkimmerAdjustElementLevelByCrystalEffectSO.LevelGainPerCrystal, 1e-7f,
                "One petal is 0.1 normalized: ten petals fill an element's resting band.");
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
