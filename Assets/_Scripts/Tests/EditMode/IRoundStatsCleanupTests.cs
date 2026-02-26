using System;
using NUnit.Framework;
using CosmicShore.Data;

namespace CosmicShore.Tests
{
    /// <summary>
    /// IRoundStats.Cleanup Tests — Validates the stat reset mechanism.
    ///
    /// WHY THIS MATTERS:
    /// IRoundStats.Cleanup() is called between rounds and on replay. It zeroes out
    /// all 30+ stat properties. If a new property is added to IRoundStats but not
    /// added to Cleanup(), that stat will carry over between rounds — score from
    /// round 1 bleeds into round 2, abilities show wrong active time, etc.
    /// This is one of the most common bugs in the stats system.
    /// </summary>
    [TestFixture]
    public class IRoundStatsCleanupTests
    {
        /// <summary>
        /// Concrete implementation of IRoundStats for testing Cleanup().
        /// No Netcode dependency — just plain properties.
        /// </summary>
        class TestRoundStats : IRoundStats
        {
            public event Action<IRoundStats> OnAnyStatChanged;
            public event Action OnScoreChanged;
            public event Action<IRoundStats> OnBlocksCreatedChanged;
            public event Action<IRoundStats> OnBlocksDestroyedChanged;
            public event Action<IRoundStats> OnBlocksRestoredChanged;
            public event Action<IRoundStats> OnPrismsStolenChanged;
            public event Action<IRoundStats> OnPrismsRemainingChanged;
            public event Action<IRoundStats> OnFriendlyPrismsDestroyedChanged;
            public event Action<IRoundStats> OnHostilePrismsDestroyedChanged;
            public event Action<IRoundStats> OnVolumeCreatedChanged;
            public event Action<IRoundStats> OnTotalVolumeDestroyedChanged;
            public event Action<IRoundStats> OnFriendlyVolumeDestroyedChanged;
            public event Action<IRoundStats> OnHostileVolumeDestroyedChanged;
            public event Action<IRoundStats> OnVolumeRestoredChanged;
            public event Action<IRoundStats> OnVolumeStolenChanged;
            public event Action<IRoundStats> OnVolumeRemainingChanged;
            public event Action<IRoundStats> OnCrystalsCollectedChanged;
            public event Action<IRoundStats> OnOmniCrystalsCollectedChanged;
            public event Action<IRoundStats> OnElementalCrystalsCollectedChanged;
            public event Action<IRoundStats> OnChargeCrystalValueChanged;
            public event Action<IRoundStats> OnMassCrystalValueChanged;
            public event Action<IRoundStats> OnSpaceCrystalValueChanged;
            public event Action<IRoundStats> OnTimeCrystalValueChanged;
            public event Action<IRoundStats> OnSkimmerShipCollisionsChanged;
            public event Action<IRoundStats> OnJoustCollisionChanged;
            public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;

            public string Name { get; set; }
            public Domains Domain { get; set; }
            public float Score { get; set; }
            public int BlocksCreated { get; set; }
            public int BlocksDestroyed { get; set; }
            public int BlocksRestored { get; set; }
            public int PrismStolen { get; set; }
            public int PrismsRemaining { get; set; }
            public int FriendlyPrismsDestroyed { get; set; }
            public int HostilePrismsDestroyed { get; set; }
            public float VolumeCreated { get; set; }
            public float TotalVolumeDestroyed { get; set; }
            public float VolumeRestored { get; set; }
            public float VolumeStolen { get; set; }
            public float VolumeRemaining { get; set; }
            public float FriendlyVolumeDestroyed { get; set; }
            public float HostileVolumeDestroyed { get; set; }
            public int CrystalsCollected { get; set; }
            public int OmniCrystalsCollected { get; set; }
            public int ElementalCrystalsCollected { get; set; }
            public float ChargeCrystalValue { get; set; }
            public float MassCrystalValue { get; set; }
            public float SpaceCrystalValue { get; set; }
            public float TimeCrystalValue { get; set; }
            public int SkimmerShipCollisions { get; set; }
            public int JoustCollisions { get; set; }
            public float FullSpeedStraightAbilityActiveTime { get; set; }
            public float RightStickAbilityActiveTime { get; set; }
            public float LeftStickAbilityActiveTime { get; set; }
            public float FlipAbilityActiveTime { get; set; }
            public float Button1AbilityActiveTime { get; set; }
            public float Button2AbilityActiveTime { get; set; }
            public float Button3AbilityActiveTime { get; set; }
        }

        TestRoundStats _stats;

        [SetUp]
        public void SetUp()
        {
            _stats = new TestRoundStats
            {
                Name = "TestPlayer",
                Domain = Domains.Jade,
                Score = 999f,
                BlocksCreated = 50,
                BlocksDestroyed = 30,
                BlocksRestored = 10,
                PrismStolen = 5,
                PrismsRemaining = 15,
                FriendlyPrismsDestroyed = 3,
                HostilePrismsDestroyed = 7,
                VolumeCreated = 100f,
                TotalVolumeDestroyed = 80f,
                VolumeRestored = 20f,
                VolumeStolen = 15f,
                VolumeRemaining = 45f,
                FriendlyVolumeDestroyed = 10f,
                HostileVolumeDestroyed = 70f,
                CrystalsCollected = 25,
                OmniCrystalsCollected = 5,
                ElementalCrystalsCollected = 20,
                ChargeCrystalValue = 1.5f,
                MassCrystalValue = 2.5f,
                SpaceCrystalValue = 3.5f,
                TimeCrystalValue = 4.5f,
                SkimmerShipCollisions = 12,
                JoustCollisions = 8,
                FullSpeedStraightAbilityActiveTime = 10f,
                RightStickAbilityActiveTime = 20f,
                LeftStickAbilityActiveTime = 15f,
                FlipAbilityActiveTime = 5f,
                Button1AbilityActiveTime = 3f,
                Button2AbilityActiveTime = 4f,
                Button3AbilityActiveTime = 6f,
            };
        }

        [Test]
        public void Cleanup_ZerosScore()
        {
            _stats.Cleanup();
            Assert.AreEqual(0f, _stats.Score);
        }

        [Test]
        public void Cleanup_ZerosPrismCounts()
        {
            _stats.Cleanup();

            Assert.AreEqual(0, _stats.BlocksCreated);
            Assert.AreEqual(0, _stats.BlocksDestroyed);
            Assert.AreEqual(0, _stats.BlocksRestored);
            Assert.AreEqual(0, _stats.PrismStolen);
            Assert.AreEqual(0, _stats.PrismsRemaining);
            Assert.AreEqual(0, _stats.FriendlyPrismsDestroyed);
            Assert.AreEqual(0, _stats.HostilePrismsDestroyed);
        }

        [Test]
        public void Cleanup_ZerosVolumes()
        {
            _stats.Cleanup();

            Assert.AreEqual(0f, _stats.VolumeCreated);
            Assert.AreEqual(0f, _stats.TotalVolumeDestroyed);
            Assert.AreEqual(0f, _stats.VolumeRestored);
            Assert.AreEqual(0f, _stats.VolumeStolen);
            Assert.AreEqual(0f, _stats.VolumeRemaining);
            Assert.AreEqual(0f, _stats.FriendlyVolumeDestroyed);
            Assert.AreEqual(0f, _stats.HostileVolumeDestroyed);
        }

        [Test]
        public void Cleanup_ZerosCrystals()
        {
            _stats.Cleanup();

            Assert.AreEqual(0, _stats.CrystalsCollected);
            Assert.AreEqual(0, _stats.OmniCrystalsCollected);
            Assert.AreEqual(0, _stats.ElementalCrystalsCollected);
            Assert.AreEqual(0f, _stats.ChargeCrystalValue);
            Assert.AreEqual(0f, _stats.MassCrystalValue);
            Assert.AreEqual(0f, _stats.SpaceCrystalValue);
            Assert.AreEqual(0f, _stats.TimeCrystalValue);
        }

        [Test]
        public void Cleanup_ZerosCollisions()
        {
            _stats.Cleanup();

            Assert.AreEqual(0, _stats.SkimmerShipCollisions);
            Assert.AreEqual(0, _stats.JoustCollisions);
        }

        [Test]
        public void Cleanup_ZerosAbilityTimes()
        {
            _stats.Cleanup();

            Assert.AreEqual(0f, _stats.FullSpeedStraightAbilityActiveTime);
            Assert.AreEqual(0f, _stats.RightStickAbilityActiveTime);
            Assert.AreEqual(0f, _stats.LeftStickAbilityActiveTime);
            Assert.AreEqual(0f, _stats.FlipAbilityActiveTime);
            Assert.AreEqual(0f, _stats.Button1AbilityActiveTime);
            Assert.AreEqual(0f, _stats.Button2AbilityActiveTime);
            Assert.AreEqual(0f, _stats.Button3AbilityActiveTime);
        }

        [Test]
        public void Cleanup_PreservesNameAndDomain()
        {
            // Cleanup should NOT reset identity fields — those persist between rounds.
            _stats.Cleanup();

            Assert.AreEqual("TestPlayer", _stats.Name,
                "Player name should survive cleanup.");
            Assert.AreEqual(Domains.Jade, _stats.Domain,
                "Domain assignment should survive cleanup.");
        }

        [Test]
        public void Cleanup_CalledTwice_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _stats.Cleanup();
                _stats.Cleanup();
            });
        }

        [Test]
        public void Cleanup_AfterCleanup_CanAccumulateNewStats()
        {
            _stats.Cleanup();

            _stats.Score = 50f;
            _stats.CrystalsCollected = 10;

            Assert.AreEqual(50f, _stats.Score);
            Assert.AreEqual(10, _stats.CrystalsCollected);
        }

        #region DomainStats Struct

        [Test]
        public void DomainStats_StructEquality()
        {
            var a = new DomainStats { Domain = Domains.Ruby, Score = 100f };
            var b = new DomainStats { Domain = Domains.Ruby, Score = 100f };

            Assert.AreEqual(a, b);
        }

        [Test]
        public void DomainStats_DifferentDomains_AreNotEqual()
        {
            var a = new DomainStats { Domain = Domains.Ruby, Score = 100f };
            var b = new DomainStats { Domain = Domains.Jade, Score = 100f };

            Assert.AreNotEqual(a, b);
        }

        #endregion
    }
}
