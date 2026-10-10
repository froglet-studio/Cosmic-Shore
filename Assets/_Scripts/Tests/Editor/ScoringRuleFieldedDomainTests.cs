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
    /// <see cref="ScoringRuleSO.ResolveWinner"/> ranks only domains somebody actually flies for.
    /// The active set is the contiguous slice <c>ActiveDomains[0..RequestedDomainCount)</c>, so
    /// two pilots on Ruby and Gold widen the count to 3 and leave Jade EMPTY. An empty domain sums
    /// to 0 and used to win every all-zero tie by enum order: a winner with no pilot to name. In
    /// Bloomrush the controller then found no representative, sent no final results, and the
    /// timed round restarted.
    /// </summary>
    [TestFixture]
    public class ScoringRuleFieldedDomainTests
    {
        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        T MakeRule<T>(ScoringMetric metric) where T : ScoringRuleSO
        {
            var rule = Track(ScriptableObject.CreateInstance<T>());
            typeof(ScoringRuleSO).GetField("metric", System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, metric);
            return rule;
        }

        GameDataSO MakeMatch(int domainCount, params (string name, Domains domain, float volume, int fuses)[] pilots)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = domainCount;
            foreach (var (name, domain, volume, fuses) in pilots)
                gameData.RoundStatsList.Add(new FakeStats
                {
                    Name = name, Domain = domain, HostileVolumeDestroyed = volume, FusesBeaten = fuses,
                });
            return gameData;
        }

        [Test]
        public void Bloomrush_AllZeroTie_GoesToAFieldedDomain_NotEmptyJade()
        {
            var rule = MakeRule<BloomrushScoringRuleSO>(ScoringMetric.VolumeDestroyed);
            var match = MakeMatch(3, ("A", Domains.Ruby, 0f, 0), ("B", Domains.Gold, 0f, 0));
            Assert.AreEqual(Domains.Ruby, rule.ResolveWinner(match),
                "Jade has no pilot; the enum-order tie-break must start at the first FIELDED domain");
        }

        [Test]
        public void Bloomrush_RealLead_StillWins()
        {
            var rule = MakeRule<BloomrushScoringRuleSO>(ScoringMetric.VolumeDestroyed);
            var match = MakeMatch(3, ("A", Domains.Ruby, 10f, 0), ("B", Domains.Gold, 40f, 0));
            Assert.AreEqual(Domains.Gold, rule.ResolveWinner(match));
        }

        [Test]
        public void Bloomrush_VolumeTie_BreaksOnFusesBeaten()
        {
            var rule = MakeRule<BloomrushScoringRuleSO>(ScoringMetric.VolumeDestroyed);
            var match = MakeMatch(3, ("A", Domains.Ruby, 20f, 1), ("B", Domains.Gold, 20f, 3));
            Assert.AreEqual(Domains.Gold, rule.ResolveWinner(match));
        }

        [Test]
        public void BaseRule_AllZeroTie_GoesToAFieldedDomain()
        {
            var rule = MakeRule<RampageScoringRuleSO>(ScoringMetric.VolumeDestroyed);
            var match = MakeMatch(3, ("A", Domains.Ruby, 0f, 0), ("B", Domains.Gold, 0f, 0));
            Assert.AreEqual(Domains.Ruby, rule.ResolveWinner(match));
        }

        [Test]
        public void BaseRule_NobodyFielded_FallsBackToTheFirstActiveDomain()
        {
            var rule = MakeRule<RampageScoringRuleSO>(ScoringMetric.VolumeDestroyed);
            var match = MakeMatch(3);
            Assert.AreEqual(GameDataSO.ActiveDomains[0], rule.ResolveWinner(match),
                "no roster at all keeps the old enum-order answer rather than the Blue sentinel");
        }

        private sealed class FakeStats : IRoundStats
        {
        // The interface, stubbed (copied from RegattaTeamPlayTests). Only Name, Domain,
        // HostileVolumeDestroyed and FusesBeaten are read by the rules under test; the rest
        // exist so the stub compiles.
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
