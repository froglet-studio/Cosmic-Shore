#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Domain fauna buff compositing tests — validates ResourceSystem.CompositeEffectiveLevel,
    /// the pure layer math behind the "living fauna hearts empower their domain" mechanic.
    ///
    /// WHY THIS MATTERS:
    /// The design's balance promise is exact value symmetry: a living fauna grants its domain
    /// the SAME elemental value its dropped crystal grants on collection, so killing and
    /// collecting your own domain's fauna is net zero for you (and a pure loss for your
    /// allies), while killing opposing fauna denies and steals that value. If the buff layer
    /// drifted from the collect formula, or left residue after revocation, that economy
    /// silently breaks.
    /// </summary>
    [TestFixture]
    public class DomainFaunaBuffTests
    {
        const float Tolerance = 0.0001f;

        #region Layer compositing

        [Test]
        public void FaunaBuff_LiftsEffectiveLevel()
        {
            // One level-1 tadpole heart (scale 1 → 0.1 normalized) = one integer level.
            float effective = ResourceSystem.CompositeEffectiveLevel(0f, 0f, 0.1f, 0f);
            Assert.AreEqual(0.1f, effective, Tolerance,
                "A living fauna heart must lift its domain's effective element level.");
        }

        [Test]
        public void FaunaBuff_RemovalRestoresExactly()
        {
            // Death revokes the buff with zero residue — the base level is untouched.
            float withBuff = ResourceSystem.CompositeEffectiveLevel(0.3f, 0f, 0.2f, 0f);
            float afterDeath = ResourceSystem.CompositeEffectiveLevel(0.3f, 0f, 0f, 0f);
            Assert.AreEqual(0.5f, withBuff, Tolerance);
            Assert.AreEqual(0.3f, afterDeath, Tolerance,
                "Revoking the fauna buff must restore the pre-buff effective level exactly.");
        }

        [Test]
        public void FaunaBuff_CountsAsEarned_ComebackYieldsToIt()
        {
            // earned = base + fauna buff = 1.0 → the comeback bonus (fill-to-10 charity)
            // has no room left and contributes nothing.
            float effective = ResourceSystem.CompositeEffectiveLevel(0.5f, 0f, 0.5f, 0.5f);
            Assert.AreEqual(1.0f, effective, Tolerance,
                "The comeback layer must yield to fauna-buffed earned power at its ceiling.");
        }

        [Test]
        public void FaunaBuff_CanReachOverchargeBand()
        {
            // Unlike comeback charity, domain fauna power is earned — it may exceed level 10.
            float effective = ResourceSystem.CompositeEffectiveLevel(1.0f, 0f, 0.5f, 0f);
            Assert.AreEqual(1.5f, effective, Tolerance,
                "Fauna buffs stack into the overcharge band like crystal-earned progress.");
        }

        [Test]
        public void FaunaBuff_ClampsAtMaxLevel()
        {
            float effective = ResourceSystem.CompositeEffectiveLevel(1.2f, 0f, 0.6f, 0f);
            Assert.AreEqual(1.5f, effective, Tolerance,
                "The composited level must clamp at the element range maximum.");
        }

        [Test]
        public void ZeroFaunaBuff_IsIdentity()
        {
            float withoutLayer = ResourceSystem.CompositeEffectiveLevel(0.4f, 0.1f, 0f, 0.2f);
            Assert.AreEqual(0.7f, withoutLayer, Tolerance,
                "A zero fauna buff must leave the existing base/temp/comeback compositing unchanged.");
        }

        #endregion

        #region Value symmetry (the economy promise)

        [Test]
        public void OwnDomainKillAndCollect_IsNetZero()
        {
            // The buff a living heart grants equals the collect gain of its dropped crystal
            // (same formula, same scale). So for the killer-collector:
            //   before: base, buff = gain (fauna alive)
            //   after:  base + gain (crystal collected), buff = 0 (fauna dead)
            // must produce the identical effective level.
            foreach (float crystalScale in new[] { 1f, 1.3f, 2.0736f, 4f })
            {
                float gain = SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(
                    crystalScale, 0.1f, 0.5f);

                float whileAlive = ResourceSystem.CompositeEffectiveLevel(0.2f, 0f, gain, 0f);
                float afterKillAndCollect = ResourceSystem.CompositeEffectiveLevel(0.2f + gain, 0f, 0f, 0f);

                Assert.AreEqual(whileAlive, afterKillAndCollect, Tolerance,
                    $"Killing + collecting an own-domain fauna (crystal scale {crystalScale}) " +
                    "must be net zero for the collector.");
            }
        }

        [Test]
        public void AllyWithoutTheCrystal_LosesTheBuffOutright()
        {
            // Teammates who don't collect the drop just lose the heart's value.
            float gain = SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain(1.3f, 0.1f, 0.5f);
            float whileAlive = ResourceSystem.CompositeEffectiveLevel(0.2f, 0f, gain, 0f);
            float afterAllyKilledIt = ResourceSystem.CompositeEffectiveLevel(0.2f, 0f, 0f, 0f);

            Assert.AreEqual(gain, whileAlive - afterAllyKilledIt, Tolerance,
                "An ally who doesn't collect the drop must lose exactly the heart's value.");
        }

        #endregion
    }
}
#endif
