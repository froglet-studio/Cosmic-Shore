using System;
using NUnit.Framework;
using CosmicShore.Data;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Training Game Progress Tests — Validates the tier progression system.
    ///
    /// WHY THIS MATTERS:
    /// TrainingGameProgress tracks player achievement tiers (1-4) and reward claims.
    /// If the tier logic is wrong, players could skip tiers, lose progress, or
    /// claim rewards they haven't earned. These tests verify:
    /// - New progress starts fresh (no tiers satisfied/claimed)
    /// - Satisfying a tier updates the current intensity
    /// - Claiming a tier doesn't affect satisfaction state
    /// - Tier indices are 1-based (matching UI display) not 0-based
    /// </summary>
    [TestFixture]
    public class TrainingGameProgressTests
    {
        TrainingGameProgress _progress;

        [SetUp]
        public void SetUp()
        {
            _progress = new TrainingGameProgress();
        }

        #region Initialization

        [Test]
        public void NewProgress_CurrentIntensity_IsOne()
        {
            Assert.AreEqual(1, _progress.CurrentIntensity,
                "A fresh TrainingGameProgress should start at intensity 1.");
        }

        [Test]
        public void NewProgress_HasFourTiers()
        {
            Assert.AreEqual(4, _progress.Progress.Length,
                "Progress array should always have exactly 4 tiers.");
        }

        [Test]
        public void NewProgress_NoTiersSatisfied()
        {
            for (int tier = 1; tier <= 4; tier++)
            {
                Assert.IsFalse(_progress.IsTierSatisfied(tier),
                    $"Tier {tier} should not be satisfied on a fresh progress.");
            }
        }

        [Test]
        public void NewProgress_NoTiersClaimed()
        {
            for (int tier = 1; tier <= 4; tier++)
            {
                Assert.IsFalse(_progress.IsTierClaimed(tier),
                    $"Tier {tier} should not be claimed on a fresh progress.");
            }
        }

        #endregion

        #region SatisfyTier

        [Test]
        public void SatisfyTier_MarksTierAsSatisfied()
        {
            _progress.SatisfyTier(1);

            Assert.IsTrue(_progress.IsTierSatisfied(1));
        }

        [Test]
        public void SatisfyTier_DoesNotAffectOtherTiers()
        {
            _progress.SatisfyTier(2);

            Assert.IsFalse(_progress.IsTierSatisfied(1), "Tier 1 should not be affected.");
            Assert.IsTrue(_progress.IsTierSatisfied(2), "Tier 2 should be satisfied.");
            Assert.IsFalse(_progress.IsTierSatisfied(3), "Tier 3 should not be affected.");
            Assert.IsFalse(_progress.IsTierSatisfied(4), "Tier 4 should not be affected.");
        }

        [Test]
        public void SatisfyTier_UpdatesCurrentIntensity_WhenHigher()
        {
            // Start at intensity 1 (default)
            _progress.SatisfyTier(3);

            Assert.AreEqual(3, _progress.CurrentIntensity,
                "Satisfying tier 3 should update intensity to 3.");
        }

        [Test]
        public void SatisfyTier_DoesNotLowerCurrentIntensity()
        {
            _progress.SatisfyTier(3);
            _progress.SatisfyTier(1);

            Assert.AreEqual(3, _progress.CurrentIntensity,
                "Satisfying a lower tier should not decrease intensity.");
        }

        [Test]
        public void SatisfyTier_AllFourTiers_IntensityIsFour()
        {
            _progress.SatisfyTier(1);
            _progress.SatisfyTier(2);
            _progress.SatisfyTier(3);
            _progress.SatisfyTier(4);

            Assert.AreEqual(4, _progress.CurrentIntensity);

            for (int tier = 1; tier <= 4; tier++)
                Assert.IsTrue(_progress.IsTierSatisfied(tier));
        }

        [Test]
        public void SatisfyTier_DoesNotClaimTier()
        {
            _progress.SatisfyTier(2);

            Assert.IsFalse(_progress.IsTierClaimed(2),
                "Satisfying a tier should not automatically claim it.");
        }

        #endregion

        #region ClaimTier

        [Test]
        public void ClaimTier_MarksTierAsClaimed()
        {
            _progress.ClaimTier(1);

            Assert.IsTrue(_progress.IsTierClaimed(1));
        }

        [Test]
        public void ClaimTier_DoesNotAffectOtherTiers()
        {
            _progress.ClaimTier(3);

            Assert.IsFalse(_progress.IsTierClaimed(1));
            Assert.IsFalse(_progress.IsTierClaimed(2));
            Assert.IsTrue(_progress.IsTierClaimed(3));
            Assert.IsFalse(_progress.IsTierClaimed(4));
        }

        [Test]
        public void ClaimTier_DoesNotSatisfyTier()
        {
            _progress.ClaimTier(2);

            Assert.IsFalse(_progress.IsTierSatisfied(2),
                "Claiming a tier should not automatically satisfy it.");
        }

        [Test]
        public void ClaimTier_DoesNotChangeCurrentIntensity()
        {
            int initialIntensity = _progress.CurrentIntensity;
            _progress.ClaimTier(4);

            Assert.AreEqual(initialIntensity, _progress.CurrentIntensity,
                "Claiming a tier should not change the current intensity.");
        }

        #endregion

        #region Combined Workflows

        [Test]
        public void SatisfyThenClaim_BothFlagsSet()
        {
            _progress.SatisfyTier(2);
            _progress.ClaimTier(2);

            Assert.IsTrue(_progress.IsTierSatisfied(2));
            Assert.IsTrue(_progress.IsTierClaimed(2));
        }

        [Test]
        public void ProgressiveTierCompletion_TracksCorrectly()
        {
            // Simulate a player progressing through tiers in order
            for (int tier = 1; tier <= 4; tier++)
            {
                _progress.SatisfyTier(tier);
                _progress.ClaimTier(tier);

                Assert.AreEqual(tier, _progress.CurrentIntensity);
                Assert.IsTrue(_progress.IsTierSatisfied(tier));
                Assert.IsTrue(_progress.IsTierClaimed(tier));
            }
        }

        #endregion
    }
}
