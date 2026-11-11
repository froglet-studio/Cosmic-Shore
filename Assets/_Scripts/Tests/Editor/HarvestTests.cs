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
    /// Harvest (GameModes.Harvest = 88, the Thresher's Space game, HARVEST.md): the scoring rule races domains to the
    /// prism target on hostile prisms destroyed, golf-timed; every reveal line is an authored field; the sheaves'
    /// colours are shared fairly between the three playable domains.
    /// </summary>
    [TestFixture]
    public class HarvestTests
    {
        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        HarvestScoringRuleSO MakeRule()
        {
            var rule = Track(ScriptableObject.CreateInstance<HarvestScoringRuleSO>());
            typeof(ScoringRuleSO).GetField("metric", System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, ScoringMetric.PrismsDestroyed);
            return rule;
        }

        GameDataSO MakeMatch(int target, params (string name, Domains domain, int prisms)[] pilots)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = 3;
            gameData.PrismTargetCount = target;
            foreach (var (name, domain, prisms) in pilots)
                gameData.RoundStatsList.Add(new FakeStats { Name = name, Domain = domain, HostilePrismsDestroyed = prisms });
            return gameData;
        }

        [Test]
        public void ADomainsSummedPrisms_ReachTheTarget_AndWin()
        {
            var rule = MakeRule();
            var match = MakeMatch(750, ("A", Domains.Jade, 400), ("B", Domains.Jade, 350), ("C", Domains.Ruby, 700));
            Assert.IsTrue(rule.IsObjectiveReached(match, out var winner),
                "two Jade pilots' 400 + 350 = 750 is the target: the race is a DOMAIN race");
            Assert.AreEqual(Domains.Jade, winner);
        }

        [Test]
        public void OneShort_DoesNotEndTheTurn()
        {
            var rule = MakeRule();
            var match = MakeMatch(750, ("A", Domains.Jade, 749), ("C", Domains.Ruby, 0));
            Assert.IsFalse(rule.IsObjectiveReached(match, out _));
        }

        [Test]
        public void AnUnresolvedTarget_NeverEndsTheTurn()
        {
            var rule = MakeRule();
            var match = MakeMatch(0, ("A", Domains.Jade, 5));
            Assert.IsFalse(rule.IsObjectiveReached(match, out _),
                "a target of 0 means the monitor has not synced yet, not that any prism wins");
        }

        [Test]
        public void Winners_ScoreTheirTime_LosersTheirDomainsPrismsLeft()
        {
            var rule = MakeRule();
            var match = MakeMatch(750, ("A", Domains.Jade, 750), ("C", Domains.Ruby, 500));
            rule.AssignScores(match, Domains.Jade, 81.5f);
            Assert.AreEqual(81.5f, match.RoundStatsList[0].Score, 1e-4f);
            Assert.IsFalse(GolfScoreSentinels.IsFinishTime(match.RoundStatsList[1].Score),
                "a loser carries the remaining-prisms sentinel, never a time");
            Assert.AreEqual(250, rule.Remaining(match, Domains.Ruby));
        }

        [Test]
        public void TheReveal_ReadsTheAuthoredWords()
        {
            var rule = MakeRule();
            typeof(HarvestScoringRuleSO).GetField("victoryLabel", System.Reflection.BindingFlags.Instance |
                                                                  System.Reflection.BindingFlags.NonPublic)
                                        .SetValue(rule, "REAPED");
            var match = MakeMatch(750, ("A", Domains.Jade, 750));
            match.RoundStatsList[0].Score = 70f;
            var reveal = rule.BuildReveal(match, match.RoundStatsList[0], true);
            Assert.AreEqual("REAPED", reveal.Label, "the reveal label is the authored field, not a literal");
            Assert.IsTrue(reveal.FormatAsTime, "a win reveals the finish time");
        }

        [TestCase(1, 12)]
        [TestCase(2, 16)]
        [TestCase(2, 20)]
        public void TheSheavesColours_AreSharedFairly(int pattern, int sheaves)
        {
            var counts = new Dictionary<Domains, int> { [Domains.Jade] = 0, [Domains.Ruby] = 0, [Domains.Gold] = 0 };
            for (int s = 0; s < sheaves; s++)
            {
                var d = SpawnableHarvest.DomainFor(pattern, s);
                if (d != Domains.Blue) counts[d]++;
            }
            int min = int.MaxValue, max = 0;
            foreach (var c in counts.Values) { min = Math.Min(min, c); max = Math.Max(max, c); }
            Assert.LessOrEqual(max - min, 1, "no domain may have more than one sheaf more of its colour than another");
            Assert.Greater(min, 0, "every playable domain has a sheaf of its own colour to leave alone");
        }

        [Test]
        public void PatternZero_IsAllNeutral()
        {
            for (int s = 0; s < 8; s++)
                Assert.AreEqual(Domains.Blue, SpawnableHarvest.DomainFor(0, s));
        }

        [Test]
        public void Bales_AreNeutralUnlessTheRungColoursAQuarterOfEverything()
        {
            for (int b = 0; b < 8; b++)
            {
                Assert.AreEqual(Domains.Blue, SpawnableHarvest.BaleDomainFor(0, b));
                Assert.AreEqual(Domains.Blue, SpawnableHarvest.BaleDomainFor(1, b), "I2's bales stay neutral");
                Assert.AreEqual(SpawnableHarvest.DomainFor(2, b), SpawnableHarvest.BaleDomainFor(2, b));
            }
        }

        [Test]
        public void TeamSpaceCrystals_AreLaidAtIntensityThreeOnly()
        {
            var settings = ScriptableObject.CreateInstance<HarvestSettingsSO>();
            try
            {
                Assert.AreEqual(Element.Space, settings.teamCrystalElement, "the team crystals are Space");
                Assert.AreEqual(0, settings.TeamCrystalsPerTeam(1));
                Assert.AreEqual(0, settings.TeamCrystalsPerTeam(2));
                Assert.Greater(settings.TeamCrystalsPerTeam(3), 0, "I3 lays Space crystals for each team present");
                Assert.AreEqual(0, settings.TeamCrystalsPerTeam(4), "I4's extra is its creatures, not crystals");
            }
            finally { Object.DestroyImmediate(settings); }
        }

        private sealed class FakeStats : IRoundStats
        {
        // The interface, stubbed (copied from RegattaTeamPlayTests). Only Name, Domain,
        // HostilePrismsDestroyed and Score are read by the rule under test; the rest
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
