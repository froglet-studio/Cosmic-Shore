#if UNITY_EDITOR
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
    /// Uses the Default threshold table:
    ///   Quiet 1000/800, Settled 4000/3500, Restless 8000/7500, Frozen 10000/9500,
    ///   Rabid 15000/14000.
    /// </summary>
    [TestFixture]
    public class CellPhaseRulesTests
    {
        static CellPhaseThresholds T => CellPhaseThresholds.Default;

        // ---- Climbing ----

        [Test]
        public void Sprout_AtZero_StaysSprout()
        {
            Assert.AreEqual(CellPhase.Sprout, CellPhaseRules.Compute(0, CellPhase.Sprout, T));
        }

        [Test]
        public void Sprout_JustBelowQuietEnter_StaysSprout()
        {
            Assert.AreEqual(CellPhase.Sprout, CellPhaseRules.Compute(999, CellPhase.Sprout, T));
        }

        [Test]
        public void Sprout_AtQuietEnter_ClimbsToQuiet()
        {
            Assert.AreEqual(CellPhase.Quiet, CellPhaseRules.Compute(1000, CellPhase.Sprout, T));
        }

        [Test]
        public void Quiet_AtSettledEnter_ClimbsToSettled()
        {
            Assert.AreEqual(CellPhase.Settled, CellPhaseRules.Compute(4000, CellPhase.Quiet, T));
        }

        [Test]
        public void Frozen_AtRabidEnter_ClimbsToRabid()
        {
            Assert.AreEqual(CellPhase.Rabid, CellPhaseRules.Compute(15000, CellPhase.Frozen, T));
        }

        // ---- Hysteresis: counts inside the band hold the current phase ----

        [Test]
        public void Quiet_InsideBand_StaysQuiet()
        {
            // 900 is between QuietExit (800) and SettledEnter (4000): no climb, no descend.
            Assert.AreEqual(CellPhase.Quiet, CellPhaseRules.Compute(900, CellPhase.Quiet, T));
        }

        [Test]
        public void Quiet_AtExitThreshold_StaysQuiet()
        {
            // Descend is strict (count < Exit), so exactly QuietExit holds Quiet.
            Assert.AreEqual(CellPhase.Quiet, CellPhaseRules.Compute(800, CellPhase.Quiet, T));
        }

        [Test]
        public void Hysteresis_NoChatterAcrossOscillation()
        {
            // A count oscillating within Quiet's band must never leave Quiet.
            var phase = CellPhase.Quiet;
            foreach (var count in new[] { 850, 999, 801, 950, 800 })
            {
                phase = CellPhaseRules.Compute(count, phase, T);
                Assert.AreEqual(CellPhase.Quiet, phase, $"chattered at count {count}");
            }
        }

        [Test]
        public void Climb_RequiresEnter_NotExit()
        {
            // From Quiet at 3999 (>= SettledExit 3500 but < SettledEnter 4000) we must NOT
            // climb to Settled — climbing keys off Enter, descending off Exit.
            Assert.AreEqual(CellPhase.Quiet, CellPhaseRules.Compute(3999, CellPhase.Quiet, T));
        }

        // ---- Descending ----

        [Test]
        public void Quiet_BelowQuietExit_DropsToSprout()
        {
            Assert.AreEqual(CellPhase.Sprout, CellPhaseRules.Compute(799, CellPhase.Quiet, T));
        }

        [Test]
        public void Settled_BelowSettledExit_DropsToQuiet_StopsAtBand()
        {
            // 900 < SettledExit (3500) so it leaves Settled, but 900 > QuietExit (800) so it
            // stops at Quiet rather than collapsing further.
            Assert.AreEqual(CellPhase.Quiet, CellPhaseRules.Compute(900, CellPhase.Settled, T));
        }

        // ---- Multi-step transitions resolve in one call ----

        [Test]
        public void Spike_FromSprout_JumpsToRabid()
        {
            Assert.AreEqual(CellPhase.Rabid, CellPhaseRules.Compute(15000, CellPhase.Sprout, T));
        }

        [Test]
        public void Crash_FromRabid_DropsToSprout()
        {
            Assert.AreEqual(CellPhase.Sprout, CellPhaseRules.Compute(0, CellPhase.Rabid, T));
        }

        // ---- Threshold table invariants ----

        [Test]
        public void Default_EnterAlwaysAboveExit_PerPhase()
        {
            // The hysteresis band must be positive at every boundary, else the cell can
            // chatter. (Sprout has no band — it's the floor.)
            Assert.Greater(T.QuietEnter, T.QuietExit);
            Assert.Greater(T.SettledEnter, T.SettledExit);
            Assert.Greater(T.RestlessEnter, T.RestlessExit);
            Assert.Greater(T.FrozenEnter, T.FrozenExit);
            Assert.Greater(T.RabidEnter, T.RabidExit);
        }

        [Test]
        public void Default_EntersMonotonicAscending()
        {
            Assert.Less(T.QuietEnter, T.SettledEnter);
            Assert.Less(T.SettledEnter, T.RestlessEnter);
            Assert.Less(T.RestlessEnter, T.FrozenEnter);
            Assert.Less(T.FrozenEnter, T.RabidEnter);
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
        public void GetEnterThreshold_SproutIsZero()
        {
            Assert.AreEqual(0, T.GetEnterThreshold(CellPhase.Sprout));
        }

        [Test]
        public void GetEnterThreshold_MatchesTablePerPhase()
        {
            Assert.AreEqual(T.QuietEnter, T.GetEnterThreshold(CellPhase.Quiet));
            Assert.AreEqual(T.SettledEnter, T.GetEnterThreshold(CellPhase.Settled));
            Assert.AreEqual(T.RestlessEnter, T.GetEnterThreshold(CellPhase.Restless));
            Assert.AreEqual(T.FrozenEnter, T.GetEnterThreshold(CellPhase.Frozen));
            Assert.AreEqual(T.RabidEnter, T.GetEnterThreshold(CellPhase.Rabid));
        }
    }
}
#endif
