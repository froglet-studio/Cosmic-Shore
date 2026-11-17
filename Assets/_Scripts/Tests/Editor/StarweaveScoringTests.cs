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
    /// Starweave (the Brittle Star's Mass game, STARWEAVE.md): the scoring rule and the settings ladders. The score is a
    /// live stock (VolumeRemaining), so the rule is decided only by the clock; the AI ladders are read by the seat's AI
    /// level, never the intensity.
    /// </summary>
    [TestFixture]
    public class StarweaveScoringTests
    {
        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        StarweaveScoringRuleSO MakeRule()
        {
            var rule = Track(ScriptableObject.CreateInstance<StarweaveScoringRuleSO>());
            typeof(ScoringRuleSO).GetField("metric", System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, ScoringMetric.VolumeRemaining);
            typeof(ScoringRuleSO).GetField("golfRules", System.Reflection.BindingFlags.Instance |
                                                        System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, false);
            return rule;
        }

        GameDataSO MakeMatch(int domainCount, params (string name, Domains domain, float volume, int prisms)[] pilots)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = domainCount;
            foreach (var (name, domain, volume, prisms) in pilots)
                gameData.RoundStatsList.Add(new FakeStats
                    { Name = name, Domain = domain, VolumeRemaining = volume, PrismsRemaining = prisms });
            return gameData;
        }

        [Test]
        public void Rule_NeverEndsTheRoundEarly()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 1e9f, 9), ("B", Domains.Ruby, 0f, 0));
            Assert.IsFalse(rule.IsObjectiveReached(match, out var winner), "a live stock is decided by the clock");
            Assert.AreEqual(Domains.Blue, winner);
        }

        [Test]
        public void Rule_MostVolumeStanding_Wins_SummedByDomain()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 400f, 1), ("B", Domains.Jade, 400f, 1), ("C", Domains.Ruby, 700f, 1));
            Assert.AreEqual(Domains.Jade, rule.ResolveWinner(match), "two pilots' 800 beats one pilot's 700");
        }

        [Test]
        public void Rule_Results_ReadTheAuthoredTemplates()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 900f, 150), ("B", Domains.Ruby, 300f, 50));
            rule.AssignScores(match, Domains.Jade, 90f);
            Assert.AreEqual(900f, match.RoundStatsList[0].Score, 1e-3f);
            var rows = rule.BuildResults(match);
            Assert.AreEqual("A", rows[0].Name);
            Assert.AreEqual("900 Volume", rows[0].ScoreText);
            Assert.AreEqual("150 prisms standing", rows[0].Secondary);
            Assert.AreEqual("50 prisms standing", rows[1].Secondary);
        }

        [Test]
        public void Settings_CrystalLadder_IsScarcityAndClamps()
        {
            var settings = Track(ScriptableObject.CreateInstance<StarweaveSettingsSO>());
            Assert.AreEqual(24, settings.CrystalCountFor(1));
            Assert.AreEqual(6, settings.CrystalCountFor(4));
            Assert.AreEqual(24, settings.CrystalCountFor(0), "below I1 clamps to I1");
            Assert.AreEqual(6, settings.CrystalCountFor(9), "above I4 clamps to I4");
        }

        [Test]
        public void Settings_AiLadders_ReadByDifficulty()
        {
            var settings = Track(ScriptableObject.CreateInstance<StarweaveSettingsSO>());
            Assert.AreEqual(1f, StarweaveSettingsSO.Rung(settings.aiRaidDeficitFraction, (int)AIDifficulty.Easy),
                "Easy never raids");
            Assert.Less(StarweaveSettingsSO.Rung(settings.aiRaidDeficitFraction, (int)AIDifficulty.Legendary),
                        StarweaveSettingsSO.Rung(settings.aiRaidDeficitFraction, (int)AIDifficulty.Medium));
            Assert.AreEqual(0f, StarweaveSettingsSO.Rung(null, 2));
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
        public int Lives { get; set; }
        public bool IsEliminated { get; set; }
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
        public event Action<IRoundStats> OnLivesChanged;
        public event Action<IRoundStats> OnEliminatedChanged;
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
