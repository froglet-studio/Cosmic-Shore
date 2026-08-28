#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="CellPhaseRules.Compute"/> - the pure phase-resolution
    /// function that is the spine of the cell ecology (Docs/ECOSYSTEM.md §1).
    /// VOLUME is the spine (locked invariant): the ladder climbs when LiveVolume meets
    /// a phase's UpEnter volume, descends below the current phase's DownExit volume,
    /// and HOLDS inside the hysteresis band (Exit..Enter) so the cell never chatters
    /// on the boundary (which would make flora/fauna gating flicker). COUNT is only
    /// the Frenzy perf backstop. The ladder is the 3-phase Calm → Restless → Frenzy.
    /// Uses the Default table: counts Restless 8000/7500, Frenzy 15000/14000; volume
    /// fields derived ×NominalPrismVolume (16) → Restless 128000/120000,
    /// Frenzy 240000/224000.
    /// </summary>
    [TestFixture]
    public class CellPhaseRulesTests
    {
        static CellPhaseThresholds T => CellPhaseThresholds.Default;

        // Pure volume-ladder probe: count 0 keeps the backstop out of the way.
        static CellPhase ComputeV(float volume, CellPhase current) =>
            CellPhaseRules.Compute(volume, 0, current, T);

        // ---- Climbing (volume) ----

        [Test]
        public void Calm_AtZero_StaysCalm()
        {
            Assert.AreEqual(CellPhase.Calm, ComputeV(0f, CellPhase.Calm));
        }

        [Test]
        public void Calm_JustBelowRestlessEnter_StaysCalm()
        {
            Assert.AreEqual(CellPhase.Calm, ComputeV(T.RestlessEnterVolume - 1f, CellPhase.Calm));
        }

        [Test]
        public void Calm_AtRestlessEnter_ClimbsToRestless()
        {
            Assert.AreEqual(CellPhase.Restless, ComputeV(T.RestlessEnterVolume, CellPhase.Calm));
        }

        [Test]
        public void Restless_AtFrenzyEnter_ClimbsToFrenzy()
        {
            Assert.AreEqual(CellPhase.Frenzy, ComputeV(T.FrenzyEnterVolume, CellPhase.Restless));
        }

        // ---- Hysteresis: volumes inside the band hold the current phase ----

        [Test]
        public void Restless_InsideBand_StaysRestless()
        {
            // Between RestlessExit and FrenzyEnter: no climb, no descend.
            float mid = (T.RestlessExitVolume + T.FrenzyEnterVolume) * 0.5f;
            Assert.AreEqual(CellPhase.Restless, ComputeV(mid, CellPhase.Restless));
        }

        [Test]
        public void Restless_AtExitThreshold_StaysRestless()
        {
            // Descend is strict (volume < Exit), so exactly RestlessExit holds Restless.
            Assert.AreEqual(CellPhase.Restless, ComputeV(T.RestlessExitVolume, CellPhase.Restless));
        }

        [Test]
        public void Hysteresis_NoChatterAcrossOscillation()
        {
            // A volume oscillating within Restless's band must never leave Restless.
            var phase = CellPhase.Restless;
            foreach (var volume in new[]
                     {
                         T.RestlessExitVolume + 1f,
                         T.FrenzyEnterVolume - 1f,
                         T.RestlessExitVolume,
                         (T.RestlessExitVolume + T.FrenzyEnterVolume) * 0.5f,
                     })
            {
                phase = CellPhaseRules.Compute(volume, 0, phase, T);
                Assert.AreEqual(CellPhase.Restless, phase, $"chattered at volume {volume}");
            }
        }

        [Test]
        public void Climb_RequiresEnter_NotExit()
        {
            // From Restless just under FrenzyEnter (but >= FrenzyExit) we must NOT climb
            // to Frenzy - climbing keys off Enter, descending off Exit.
            Assert.AreEqual(CellPhase.Restless, ComputeV(T.FrenzyEnterVolume - 1f, CellPhase.Restless));
        }

        // ---- Descending ----

        [Test]
        public void Restless_BelowRestlessExit_DropsToCalm()
        {
            Assert.AreEqual(CellPhase.Calm, ComputeV(T.RestlessExitVolume - 1f, CellPhase.Restless));
        }

        [Test]
        public void Frenzy_BelowFrenzyExit_DropsToRestless_StopsAtBand()
        {
            // Below FrenzyExit so it leaves Frenzy, above RestlessExit so it stops at
            // Restless rather than collapsing further.
            float mid = (T.RestlessExitVolume + T.FrenzyExitVolume) * 0.5f;
            Assert.AreEqual(CellPhase.Restless, ComputeV(mid, CellPhase.Frenzy));
        }

        // ---- Multi-step transitions resolve in one call ----

        [Test]
        public void Spike_FromCalm_JumpsToFrenzy()
        {
            Assert.AreEqual(CellPhase.Frenzy, ComputeV(T.FrenzyEnterVolume, CellPhase.Calm));
        }

        [Test]
        public void Crash_FromFrenzy_DropsToCalm()
        {
            Assert.AreEqual(CellPhase.Calm, ComputeV(0f, CellPhase.Frenzy));
        }

        // ---- Count backstop (perf): count alone forces and holds Frenzy ----

        [Test]
        public void CountBackstop_ForcesFrenzy_AtLowVolume()
        {
            // Many tiny prisms: volume far below the ladder, count at FrenzyEnter.
            Assert.AreEqual(CellPhase.Frenzy,
                CellPhaseRules.Compute(0f, T.FrenzyEnter, CellPhase.Calm, T));
        }

        [Test]
        public void CountBackstop_HoldsFrenzy_InsideCountBand()
        {
            // In Frenzy via the backstop: count between FrenzyExit and FrenzyEnter holds it.
            Assert.AreEqual(CellPhase.Frenzy,
                CellPhaseRules.Compute(0f, T.FrenzyExit, CellPhase.Frenzy, T));
        }

        [Test]
        public void CountBackstop_Releases_BelowCountExit()
        {
            // Count falls through the band and volume is calm → the cell descends.
            Assert.AreEqual(CellPhase.Calm,
                CellPhaseRules.Compute(0f, T.FrenzyExit - 1, CellPhase.Frenzy, T));
        }

        [Test]
        public void CountBackstop_BelowEnter_DoesNotForceFrenzy()
        {
            Assert.AreEqual(CellPhase.Calm,
                CellPhaseRules.Compute(0f, T.FrenzyEnter - 1, CellPhase.Calm, T));
        }

        // ---- Threshold table invariants ----

        [Test]
        public void Default_EnterAlwaysAboveExit_PerPhase()
        {
            // The hysteresis band must be positive at every boundary, else the cell can
            // chatter. (Calm has no band - it's the floor.)
            Assert.Greater(T.RestlessEnterVolume, T.RestlessExitVolume);
            Assert.Greater(T.FrenzyEnterVolume, T.FrenzyExitVolume);
            Assert.Greater(T.FrenzyEnter, T.FrenzyExit);
        }

        [Test]
        public void Default_EntersMonotonicAscending()
        {
            Assert.Less(T.RestlessEnterVolume, T.FrenzyEnterVolume);
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

        // ---- Legacy migration: counts derive the volume scale ----

        [Test]
        public void DerivedVolumeScale_FromLegacyCountAsset()
        {
            // A pre-volume asset (count fields only) derives volume = count × 16.
            var legacy = new CellPhaseThresholds
            {
                RestlessEnter = 700, RestlessExit = 500,
                FrenzyEnter = 1200, FrenzyExit = 950,
            }.WithDerivedVolumeScale();

            Assert.AreEqual(700 * CellPhaseThresholds.NominalPrismVolume, legacy.RestlessEnterVolume);
            Assert.AreEqual(500 * CellPhaseThresholds.NominalPrismVolume, legacy.RestlessExitVolume);
            Assert.AreEqual(1200 * CellPhaseThresholds.NominalPrismVolume, legacy.FrenzyEnterVolume);
            Assert.AreEqual(950 * CellPhaseThresholds.NominalPrismVolume, legacy.FrenzyExitVolume);
        }

        [Test]
        public void DerivedVolumeScale_ExplicitVolumesUntouched()
        {
            var authored = new CellPhaseThresholds
            {
                RestlessEnter = 700, RestlessExit = 500,
                FrenzyEnter = 1200, FrenzyExit = 950,
                RestlessEnterVolume = 5000f, RestlessExitVolume = 4000f,
                FrenzyEnterVolume = 9000f, FrenzyExitVolume = 8000f,
            }.WithDerivedVolumeScale();

            Assert.AreEqual(5000f, authored.RestlessEnterVolume);
            Assert.AreEqual(9000f, authored.FrenzyEnterVolume);
        }

        [Test]
        public void GetEnterVolume_CalmIsZero()
        {
            Assert.AreEqual(0f, T.GetEnterVolume(CellPhase.Calm));
        }

        [Test]
        public void GetEnterVolume_MatchesTablePerPhase()
        {
            Assert.AreEqual(T.RestlessEnterVolume, T.GetEnterVolume(CellPhase.Restless));
            Assert.AreEqual(T.FrenzyEnterVolume, T.GetEnterVolume(CellPhase.Frenzy));
        }
    }
}
#endif
