#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Domain fauna buff compositing tests — validates ResourceSystem.CompositeEffectiveLevel,
    /// HeldFaunaContribution, and ComputeUnfeltIncrease: the pure layer math behind the
    /// "living fauna hearts empower their domain" mechanic and the maintained-mechanism law.
    ///
    /// WHY THIS MATTERS:
    /// Two promises live here. (1) Value symmetry: a living fauna grants its domain the SAME
    /// elemental value its dropped crystal grants on collection, so killing and collecting
    /// your own domain's fauna is net zero for you and a pure loss for your allies. (2) The
    /// maintained-mechanism law: no held layer may SUSTAIN an element above level 10 — the
    /// 10..15 overcharge band belongs to transients only, so pool increases above the ceiling
    /// arrive as temporary spikes that drain back, keeping headroom for the next reward.
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
        public void FaunaBuff_ComebackYieldsToIt()
        {
            // base + held fauna buff = 1.0 → the comeback bonus (fill-to-10 charity)
            // has no room left and contributes nothing.
            float effective = ResourceSystem.CompositeEffectiveLevel(0.5f, 0f, 0.5f, 0.5f);
            Assert.AreEqual(1.0f, effective, Tolerance,
                "The comeback layer must yield to fauna-buffed power at the sustained ceiling.");
        }

        [Test]
        public void ZeroFaunaBuff_IsIdentity()
        {
            float withoutLayer = ResourceSystem.CompositeEffectiveLevel(0.4f, 0.1f, 0f, 0.2f);
            Assert.AreEqual(0.7f, withoutLayer, Tolerance,
                "A zero fauna buff must leave the existing base/temp/comeback compositing unchanged.");
        }

        #endregion

        #region The maintained-mechanism law (sustained max 10, transients own 10..15)

        [Test]
        public void SustainedFaunaBuff_NeverHoldsAboveLevel10()
        {
            // A saturated pool sustains exactly the ceiling, not the overcharge band.
            Assert.AreEqual(1.0f,
                ResourceSystem.CompositeEffectiveLevel(0f, 0f, 1.3f, 0f), Tolerance,
                "A held fauna pool must sustain at most level 10.");
            // A full base leaves the held layer no room at all.
            Assert.AreEqual(1.0f,
                ResourceSystem.CompositeEffectiveLevel(1.0f, 0f, 0.5f, 0f), Tolerance,
                "The held layer must not stack past the ceiling on a full base.");
        }

        [Test]
        public void BaseOvercharge_IsNotExtendedByHeldBuff()
        {
            // Base above 10 (crystal overcharge, draining via RecoverBaseLevels) gets no
            // additional held contribution — the pool waits below the ceiling.
            Assert.AreEqual(1.2f,
                ResourceSystem.CompositeEffectiveLevel(1.2f, 0f, 0.6f, 0f), Tolerance,
                "Held fauna power must not ride on top of base overcharge.");
        }

        [Test]
        public void TemporarySpike_RidesAboveTheCeiling_AndClampsAt15()
        {
            // Temporary effects (including converted over-ceiling pool increases) are the
            // only path into the 10..15 band...
            Assert.AreEqual(1.3f,
                ResourceSystem.CompositeEffectiveLevel(0f, 0.3f, 1.3f, 0f), Tolerance,
                "A temporary spike must be felt above the sustained ceiling.");
            // ...and 15 is the hard top.
            Assert.AreEqual(1.5f,
                ResourceSystem.CompositeEffectiveLevel(0f, 0.8f, 1.3f, 0f), Tolerance,
                "The overcharge band must clamp at level 15.");
        }

        [Test]
        public void HeldContribution_FillsOnlyTheRoomBelowTheCeiling()
        {
            Assert.AreEqual(0.4f, ResourceSystem.HeldFaunaContribution(0f, 0.4f), Tolerance);
            Assert.AreEqual(0.3f, ResourceSystem.HeldFaunaContribution(0.7f, 0.5f), Tolerance);
            Assert.AreEqual(0f, ResourceSystem.HeldFaunaContribution(1.0f, 0.5f), Tolerance);
            Assert.AreEqual(0f, ResourceSystem.HeldFaunaContribution(1.2f, 0.5f), Tolerance);
            // A deficit base leaves extra room — fauna power can offset debuffed elements.
            Assert.AreEqual(0.5f, ResourceSystem.HeldFaunaContribution(-0.3f, 0.5f), Tolerance);
        }

        [Test]
        public void UnfeltIncrease_IsTheOverCeilingPortion()
        {
            // Fully below the ceiling → the held layer expresses all of it, nothing to spike.
            Assert.AreEqual(0f, ResourceSystem.ComputeUnfeltIncrease(0f, 0.2f, 0.4f), Tolerance);
            // Straddling the ceiling → only the part above it spikes.
            Assert.AreEqual(0.1f, ResourceSystem.ComputeUnfeltIncrease(0f, 0.9f, 1.1f), Tolerance);
            // Saturated → the whole increase spikes.
            Assert.AreEqual(0.1f, ResourceSystem.ComputeUnfeltIncrease(0f, 1.3f, 1.4f), Tolerance);
            // Full base → no room at all, the whole increase spikes.
            Assert.AreEqual(0.2f, ResourceSystem.ComputeUnfeltIncrease(1.0f, 0f, 0.2f), Tolerance);
            // Decreases never spike.
            Assert.AreEqual(0f, ResourceSystem.ComputeUnfeltIncrease(0f, 1.4f, 1.3f), Tolerance);
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
            // must produce the identical effective level. (Values below the sustained
            // ceiling, where both sides are fully expressed.)
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
        public void OwnDomainKillAndCollect_IsNetZero_AtSaturation()
        {
            // With the pool saturated above the ceiling, the buffer absorbs the swap: the
            // held fill re-balances around the collected base gain and the sustained level
            // stays pinned at 10 on both sides.
            float whileAlive = ResourceSystem.CompositeEffectiveLevel(0.2f, 0f, 1.3f, 0f);
            float afterKillAndCollect = ResourceSystem.CompositeEffectiveLevel(0.3f, 0f, 1.2f, 0f);
            Assert.AreEqual(1.0f, whileAlive, Tolerance);
            Assert.AreEqual(whileAlive, afterKillAndCollect, Tolerance,
                "At pool saturation the kill-and-collect swap must still be net zero.");
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
