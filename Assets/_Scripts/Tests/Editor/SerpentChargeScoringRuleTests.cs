#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="SerpentChargeScoringRuleSO"/> scores SNIPES and nothing else: a Charge game is about
    /// hitting other pilots (Garrett, 2026-10-16). A sniped pilot is the sniper's Debuff-class hit,
    /// paid into CombatPoints by <see cref="CombatHitScoring.Credit"/>; prisms the round destroys
    /// are still counted platform-wide and must pay nothing here.
    /// </summary>
    [TestFixture]
    public class SerpentChargeScoringRuleTests
    {
        const int Snipe = 1;

        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        SerpentChargeScoringRuleSO MakeRule()
        {
            var rule = Track(ScriptableObject.CreateInstance<SerpentChargeScoringRuleSO>());
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ScoringRuleSO).GetField("metric", flags).SetValue(rule, ScoringMetric.CombatPoints);
            typeof(SerpentChargeScoringRuleSO).GetField("snipePoints", flags).SetValue(rule, Snipe);
            return rule;
        }

        GameDataSO MakeMatch(int target, params FakeStats[] pilots)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = 2;
            gameData.CombatPointTargetCount = target;
            foreach (var p in pilots) gameData.RoundStatsList.Add(p);
            return gameData;
        }

        [Test]
        public void ASnipe_IsPricedAtSnipePoints_AndNothingElsePays()
        {
            var rule = MakeRule();
            Assert.AreEqual(Snipe, rule.PointsForCombatHit(CombatHitClass.Debuff));
            foreach (CombatHitClass c in Enum.GetValues(typeof(CombatHitClass)))
                if (c != CombatHitClass.Debuff)
                    Assert.AreEqual(0, rule.PointsForCombatHit(c), $"{c} must pay nothing - the Serpent fires only the rifle");
        }

        [Test]
        public void ACreditedSnipe_LandsInCombatPoints_AndTheRawCount()
        {
            var rule = MakeRule();
            var shooter = new FakeStats { Name = "A", Domain = Domains.Jade };
            CombatHitScoring.Credit(shooter, CombatHitClass.Debuff, rule);
            Assert.AreEqual(Snipe, shooter.CombatPoints);
            Assert.AreEqual(1, shooter.DebuffHitsLanded);
        }

        [Test]
        public void PrismsPayNothing_OnlySnipesScore()
        {
            var rule = MakeRule();
            var match = MakeMatch(8,
                new FakeStats { Name = "A", Domain = Domains.Jade, HostilePrismsDestroyed = 5000, CombatPoints = 2 },
                new FakeStats { Name = "B", Domain = Domains.Jade, CombatPoints = 1 },
                new FakeStats { Name = "C", Domain = Domains.Ruby, HostilePrismsDestroyed = 9000 });
            Assert.AreEqual(3, rule.DomainValue(match, Domains.Jade));
            Assert.AreEqual(0, rule.DomainValue(match, Domains.Ruby),
                "a forest destroyed is not a pilot hit - the Charge game scores pilots only");
            Assert.AreEqual(5, rule.Remaining(match, Domains.Jade));
        }

        [Test]
        public void TheRaceEnds_WhenADomainReachesTheSnipeTarget()
        {
            var rule = MakeRule();
            var match = MakeMatch(8,
                new FakeStats { Name = "A", Domain = Domains.Jade, CombatPoints = 5 },
                new FakeStats { Name = "B", Domain = Domains.Jade, CombatPoints = 3 },
                new FakeStats { Name = "C", Domain = Domains.Ruby, CombatPoints = 7 });
            Assert.IsTrue(rule.IsObjectiveReached(match, out var winner));
            Assert.AreEqual(Domains.Jade, winner, "teammates pool their snipes");
            Assert.AreEqual(Domains.Jade, rule.ResolveWinner(match));
        }

        [Test]
        public void AnUnresolvedTarget_NeverEndsTheRace()
        {
            var rule = MakeRule();
            var match = MakeMatch(0, new FakeStats { Name = "A", Domain = Domains.Jade, CombatPoints = 50 });
            Assert.IsFalse(rule.IsObjectiveReached(match, out var winner));
            Assert.AreEqual(Domains.Blue, winner);
        }

        private sealed class FakeStats : IRoundStats
        {
        // The interface, stubbed (copied from ScoringRuleFieldedDomainTests). Only Name, Domain,
        // HostilePrismsDestroyed, CombatPoints and DebuffHitsLanded are read by the rule under
        // test; the rest exist so the stub compiles.
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
        public int LifeformsKilled { get; set; }
        public int BulletHitsLanded { get; set; }
        public int MissileHitsLanded { get; set; }
        public int DebuffHitsLanded { get; set; }
        public int StrikeHitsLanded { get; set; }
        public int CombatPoints { get; set; }
        public int SwitchesThreaded { get; set; }
        public int FusesBeaten { get; set; }
        public float FullSpeedStraightAbilityActiveTime { get; set; }
        public float RightStickAbilityActiveTime { get; set; }
        public float LeftStickAbilityActiveTime { get; set; }
        public float FlipAbilityActiveTime { get; set; }
        public float Button1AbilityActiveTime { get; set; }
        public float Button2AbilityActiveTime { get; set; }
        public float Button3AbilityActiveTime { get; set; }

#pragma warning disable 67   // never raised: the driver polls, it does not subscribe
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
        public event Action<IRoundStats> OnLifeformsKilledChanged;
        public event Action<IRoundStats> OnBulletHitsLandedChanged;
        public event Action<IRoundStats> OnMissileHitsLandedChanged;
        public event Action<IRoundStats> OnDebuffHitsLandedChanged;
        public event Action<IRoundStats> OnStrikeHitsLandedChanged;
        public event Action<IRoundStats> OnCombatPointsChanged;
        public event Action<IRoundStats> OnSwitchesThreadedChanged;
        public event Action<IRoundStats> OnFusesBeatenChanged;
        public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;
#pragma warning restore 67
        }
    }
}
#endif
