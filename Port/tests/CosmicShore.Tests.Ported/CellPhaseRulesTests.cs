using NUnit.Framework;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="CellPhaseRules.Compute"/> — the pure phase-resolution
    /// function that is the spine of the cell ecology (Docs/ECOSYSTEM.md §1). It must:
    ///   - climb to the next phase when LiveBlockCount meets that phase's UpEnter,
    ///   - descend when count falls below the current phase's DownExit,
    ///   - and HOLD inside the hysteresis band (Exit..Enter) so the cell never chatters
    ///     on the boundary (which would make flora/fauna gating flicker).
    /// The ladder is the 3-phase Calm → Restless → Frenzy. Uses the Default table:
    ///   Restless 8000/7500, Frenzy 15000/14000.
    /// </summary>
    [TestFixture]
    public class CellPhaseRulesTests
    {
        static CellPhaseThresholds T => CellPhaseThresholds.Default;

        // ---- Climbing ----

        [Test]
        public void Calm_AtZero_StaysCalm()
        {
            Assert.AreEqual(CellPhase.Calm, CellPhaseRules.Compute(0, CellPhase.Calm, T));
        }

        [Test]
        public void Calm_JustBelowRestlessEnter_StaysCalm()
        {
            Assert.AreEqual(CellPhase.Calm, CellPhaseRules.Compute(7999, CellPhase.Calm, T));
        }

        [Test]
        public void Calm_AtRestlessEnter_ClimbsToRestless()
        {
            Assert.AreEqual(CellPhase.Restless, CellPhaseRules.Compute(8000, CellPhase.Calm, T));
        }

        [Test]
        public void Restless_AtFrenzyEnter_ClimbsToFrenzy()
        {
            Assert.AreEqual(CellPhase.Frenzy, CellPhaseRules.Compute(15000, CellPhase.Restless, T));
        }

        // ---- Hysteresis: counts inside the band hold the current phase ----

        [Test]
        public void Restless_InsideBand_StaysRestless()
        {
            // 9000 is between RestlessExit (7500) and FrenzyEnter (15000): no climb, no descend.
            Assert.AreEqual(CellPhase.Restless, CellPhaseRules.Compute(9000, CellPhase.Restless, T));
        }

        [Test]
        public void Restless_AtExitThreshold_StaysRestless()
        {
            // Descend is strict (count < Exit), so exactly RestlessExit holds Restless.
            Assert.AreEqual(CellPhase.Restless, CellPhaseRules.Compute(7500, CellPhase.Restless, T));
        }

        [Test]
        public void Hysteresis_NoChatterAcrossOscillation()
        {
            // A count oscillating within Restless's band must never leave Restless.
            var phase = CellPhase.Restless;
            foreach (var count in new[] { 7600, 14999, 7501, 9500, 7500 })
            {
                phase = CellPhaseRules.Compute(count, phase, T);
                Assert.AreEqual(CellPhase.Restless, phase, $"chattered at count {count}");
            }
        }

        [Test]
        public void Climb_RequiresEnter_NotExit()
        {
            // From Restless at 14999 (>= FrenzyExit 14000 but < FrenzyEnter 15000) we must NOT
            // climb to Frenzy — climbing keys off Enter, descending off Exit.
            Assert.AreEqual(CellPhase.Restless, CellPhaseRules.Compute(14999, CellPhase.Restless, T));
        }

        // ---- Descending ----

        [Test]
        public void Restless_BelowRestlessExit_DropsToCalm()
        {
            Assert.AreEqual(CellPhase.Calm, CellPhaseRules.Compute(7499, CellPhase.Restless, T));
        }

        [Test]
        public void Frenzy_BelowFrenzyExit_DropsToRestless_StopsAtBand()
        {
            // 9000 < FrenzyExit (14000) so it leaves Frenzy, but 9000 > RestlessExit (7500) so
            // it stops at Restless rather than collapsing further.
            Assert.AreEqual(CellPhase.Restless, CellPhaseRules.Compute(9000, CellPhase.Frenzy, T));
        }

        // ---- Multi-step transitions resolve in one call ----

        [Test]
        public void Spike_FromCalm_JumpsToFrenzy()
        {
            Assert.AreEqual(CellPhase.Frenzy, CellPhaseRules.Compute(15000, CellPhase.Calm, T));
        }

        [Test]
        public void Crash_FromFrenzy_DropsToCalm()
        {
            Assert.AreEqual(CellPhase.Calm, CellPhaseRules.Compute(0, CellPhase.Frenzy, T));
        }

        // ---- Threshold table invariants ----

        [Test]
        public void Default_EnterAlwaysAboveExit_PerPhase()
        {
            // The hysteresis band must be positive at every boundary, else the cell can
            // chatter. (Calm has no band — it's the floor.)
            Assert.Greater(T.RestlessEnter, T.RestlessExit);
            Assert.Greater(T.FrenzyEnter, T.FrenzyExit);
        }

        [Test]
        public void Default_EntersMonotonicAscending()
        {
            Assert.Less(T.RestlessEnter, T.FrenzyEnter);
        }

        [Test]
        public void Default_IsNotAllZero_LegacySubstitutionGuard()
        {
            // Cell substitutes Default when a CellConfig's thresholds deserialize as struct
            // zero; Default itself must therefore NOT read as all-zero.
            Assert.IsFalse(CellPhaseThresholds.Default.IsAllZero);
        }

        [Test]
        public void DefaultStruct_IsAllZero()
        {
            Assert.IsTrue(default(CellPhaseThresholds).IsAllZero);
        }

        [Test]
        public void GetEnterThreshold_CalmIsZero()
        {
            Assert.AreEqual(0, T.GetEnterThreshold(CellPhase.Calm));
        }

        [Test]
        public void GetEnterThreshold_MatchesTablePerPhase()
        {
            Assert.AreEqual(T.RestlessEnter, T.GetEnterThreshold(CellPhase.Restless));
            Assert.AreEqual(T.FrenzyEnter, T.GetEnterThreshold(CellPhase.Frenzy));
        }
    }
}
