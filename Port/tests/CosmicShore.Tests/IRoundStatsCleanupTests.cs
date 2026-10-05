using System;
using CosmicShore.Data;

namespace CosmicShore.Tests;

/// <summary>
/// IRoundStats.Cleanup Tests — Validates the stat reset mechanism.
/// Ported from Assets/_Scripts/Tests/EditMode/IRoundStatsCleanupTests.cs (NUnit → xunit;
/// [SetUp] becomes the constructor).
///
/// WHY THIS MATTERS:
/// IRoundStats.Cleanup() is called between rounds and on replay. It zeroes out
/// all 30+ stat properties. If a new property is added to IRoundStats but not
/// added to Cleanup(), that stat will carry over between rounds — score from
/// round 1 bleeds into round 2, abilities show wrong active time, etc.
/// This is one of the most common bugs in the stats system.
/// </summary>
public class IRoundStatsCleanupTests
{
    /// <summary>
    /// Concrete implementation of IRoundStats for testing Cleanup().
    /// No networking dependency — just plain properties.
    /// </summary>
    class TestRoundStats : IRoundStats
    {
#pragma warning disable CS0067 // Interface-required events unused in test mock
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
        public event Action<IRoundStats> OnGoalsScoredChanged;
        public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;
#pragma warning restore CS0067

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
        public int GoalsScored { get; set; }
        public float FullSpeedStraightAbilityActiveTime { get; set; }
        public float RightStickAbilityActiveTime { get; set; }
        public float LeftStickAbilityActiveTime { get; set; }
        public float FlipAbilityActiveTime { get; set; }
        public float Button1AbilityActiveTime { get; set; }
        public float Button2AbilityActiveTime { get; set; }
        public float Button3AbilityActiveTime { get; set; }
    }

    readonly IRoundStats _stats;

    public IRoundStatsCleanupTests()
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
            VolumeStolen = 10f,
            VolumeRemaining = 40f,
            FriendlyVolumeDestroyed = 30f,
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
            GoalsScored = 4, // AstroLeague goal stat added upstream (bleeding-edge merge c833c580)
            FullSpeedStraightAbilityActiveTime = 10f,
            RightStickAbilityActiveTime = 20f,
            LeftStickAbilityActiveTime = 15f,
            FlipAbilityActiveTime = 5f,
            Button1AbilityActiveTime = 3f,
            Button2AbilityActiveTime = 4f,
            Button3AbilityActiveTime = 6f,
        };
    }

    [Fact]
    public void Cleanup_ZerosScore()
    {
        _stats.Cleanup();
        Assert.Equal(0f, _stats.Score);
    }

    [Fact]
    public void Cleanup_ZerosPrismCounts()
    {
        _stats.Cleanup();

        Assert.Equal(0, _stats.BlocksCreated);
        Assert.Equal(0, _stats.BlocksDestroyed);
        Assert.Equal(0, _stats.BlocksRestored);
        Assert.Equal(0, _stats.PrismStolen);
        Assert.Equal(0, _stats.PrismsRemaining);
        Assert.Equal(0, _stats.FriendlyPrismsDestroyed);
        Assert.Equal(0, _stats.HostilePrismsDestroyed);
    }

    [Fact]
    public void Cleanup_ZerosVolumes()
    {
        _stats.Cleanup();

        Assert.Equal(0f, _stats.VolumeCreated);
        Assert.Equal(0f, _stats.TotalVolumeDestroyed);
        Assert.Equal(0f, _stats.VolumeRestored);
        Assert.Equal(0f, _stats.VolumeStolen);
        Assert.Equal(0f, _stats.VolumeRemaining);
        Assert.Equal(0f, _stats.FriendlyVolumeDestroyed);
        Assert.Equal(0f, _stats.HostileVolumeDestroyed);
    }

    [Fact]
    public void Cleanup_ZerosCrystals()
    {
        _stats.Cleanup();

        Assert.Equal(0, _stats.CrystalsCollected);
        Assert.Equal(0, _stats.OmniCrystalsCollected);
        Assert.Equal(0, _stats.ElementalCrystalsCollected);
        Assert.Equal(0f, _stats.ChargeCrystalValue);
        Assert.Equal(0f, _stats.MassCrystalValue);
        Assert.Equal(0f, _stats.SpaceCrystalValue);
        Assert.Equal(0f, _stats.TimeCrystalValue);
    }

    [Fact]
    public void Cleanup_ZerosCollisions()
    {
        _stats.Cleanup();

        Assert.Equal(0, _stats.SkimmerShipCollisions);
        Assert.Equal(0, _stats.JoustCollisions);
        Assert.Equal(0, _stats.GoalsScored);
    }

    [Fact]
    public void Cleanup_ZerosAbilityTimes()
    {
        _stats.Cleanup();

        Assert.Equal(0f, _stats.FullSpeedStraightAbilityActiveTime);
        Assert.Equal(0f, _stats.RightStickAbilityActiveTime);
        Assert.Equal(0f, _stats.LeftStickAbilityActiveTime);
        Assert.Equal(0f, _stats.FlipAbilityActiveTime);
        Assert.Equal(0f, _stats.Button1AbilityActiveTime);
        Assert.Equal(0f, _stats.Button2AbilityActiveTime);
        Assert.Equal(0f, _stats.Button3AbilityActiveTime);
    }

    [Fact]
    public void Cleanup_PreservesNameAndDomain()
    {
        // Cleanup should NOT reset identity fields — those persist between rounds.
        _stats.Cleanup();

        Assert.Equal("TestPlayer", _stats.Name);
        Assert.Equal(Domains.Jade, _stats.Domain);
    }
}
