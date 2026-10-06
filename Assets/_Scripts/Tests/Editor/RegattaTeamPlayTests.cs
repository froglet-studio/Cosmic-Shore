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
    /// Regatta's team play: who an AI seat flies (<see cref="AIHullSeating"/> - opponents pinned
    /// to the card's hull, allies picked by their teammates) and how a team scores
    /// (<see cref="RegattaScoringRuleSO"/> - the SUM of every pilot's gates, the race ending at
    /// the first finisher). The lobby chips, the AI backfill and the arena hull backstop all read
    /// the seating answer, so it is pinned here once rather than trusted three times.
    /// </summary>
    [TestFixture]
    public class RegattaTeamPlayTests
    {
        // ── AI seating ───────────────────────────────────────────────────────────────────────

        [Test]
        public void OpponentSeat_IsADomainNoHumanFlies_WhenTheCardPinsAHull()
        {
            var humans = new HashSet<Domains> { Domains.Jade };
            Assert.IsTrue(AIHullSeating.IsOpponentSeat(VesselClassType.Squirrel, Domains.Ruby, humans));
            Assert.IsFalse(AIHullSeating.IsOpponentSeat(VesselClassType.Squirrel, Domains.Jade, humans),
                "an AI on a human's team is an ally, whatever the card pins");
            Assert.IsFalse(AIHullSeating.IsOpponentSeat(VesselClassType.Random, Domains.Ruby, humans),
                "a card that pins nothing keeps its ordinary mixed draw for every seat");
        }

        [Test]
        public void Cycle_StepsThroughAutoThenTheRosterInCardOrder_AndWraps()
        {
            var roster = new List<VesselClassType> { VesselClassType.Manta, VesselClassType.Rhino, VesselClassType.Urchin };

            Assert.IsTrue(AIHullSeating.TryCycle(roster, VesselClassType.Random, +1, null, out var next));
            Assert.AreEqual(VesselClassType.Manta, next);
            Assert.IsTrue(AIHullSeating.TryCycle(roster, VesselClassType.Urchin, +1, null, out next));
            Assert.AreEqual(VesselClassType.Random, next, "past the last hull is back to auto");
            Assert.IsTrue(AIHullSeating.TryCycle(roster, VesselClassType.Random, -1, null, out next));
            Assert.AreEqual(VesselClassType.Urchin, next);
        }

        [Test]
        public void Cycle_SkipsTakenHulls_ButNeverAuto()
        {
            var roster = new List<VesselClassType> { VesselClassType.Manta, VesselClassType.Rhino, VesselClassType.Urchin };
            var taken = new HashSet<VesselClassType> { VesselClassType.Rhino, VesselClassType.Urchin };

            Assert.IsTrue(AIHullSeating.TryCycle(roster, VesselClassType.Manta, +1, taken, out var next));
            Assert.AreEqual(VesselClassType.Random, next);

            var all = new HashSet<VesselClassType>(roster);
            Assert.IsFalse(AIHullSeating.TryCycle(roster, VesselClassType.Random, +1, all, out _),
                "every hull taken and already on auto: nothing to step to");
        }

        // ── team-sum scoring ─────────────────────────────────────────────────────────────────

        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        RegattaScoringRuleSO MakeRule()
        {
            var rule = Track(ScriptableObject.CreateInstance<RegattaScoringRuleSO>());
            // metric is authored on the asset (SwitchesThreaded); set it the way the asset does.
            typeof(ScoringRuleSO).GetField("metric", System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, ScoringMetric.SwitchesThreaded);
            return rule;
        }

        GameDataSO MakeRace(int target, params (string name, Domains domain, int gates)[] pilots)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = 2;
            gameData.SwitchTargetCount = target;
            foreach (var (name, domain, gates) in pilots)
                gameData.RoundStatsList.Add(new FakeStats { Name = name, Domain = domain, SwitchesThreaded = gates });
            return gameData;
        }

        [Test]
        public void TeamValue_IsTheSumOfEveryPilot()
        {
            var rule = MakeRule();
            var race = MakeRace(24, ("A", Domains.Jade, 10), ("B", Domains.Jade, 7), ("C", Domains.Ruby, 12));
            Assert.AreEqual(17, rule.DomainValue(race, Domains.Jade));
            Assert.AreEqual(12, rule.DomainValue(race, Domains.Ruby));
        }

        [Test]
        public void Race_DoesNotEnd_UntilAPilotFliesTheWholeCourse()
        {
            var rule = MakeRule();
            // Jade's SUM is past the course length, but nobody has finished it.
            var race = MakeRace(24, ("A", Domains.Jade, 20), ("B", Domains.Jade, 20), ("C", Domains.Ruby, 23));
            Assert.IsFalse(rule.IsObjectiveReached(race, out _));
        }

        [Test]
        public void Race_EndsAtTheFirstFinisher_AndTheHigherTeamTotalWins()
        {
            var rule = MakeRule();
            // Ruby's ace finishes, but Jade's two pilots have threaded more between them.
            var race = MakeRace(24, ("A", Domains.Jade, 18), ("B", Domains.Jade, 15), ("C", Domains.Ruby, 24), ("D", Domains.Ruby, 4));
            Assert.IsTrue(rule.IsObjectiveReached(race, out var winner));
            Assert.AreEqual(Domains.Jade, winner);
            Assert.AreEqual(Domains.Jade, rule.ResolveWinner(race), "the winner and the resolver agree");
        }

        [Test]
        public void ATie_GoesToTheTeamThatPutTheFinisherAcross()
        {
            var rule = MakeRule();
            var race = MakeRace(24, ("A", Domains.Jade, 20), ("B", Domains.Jade, 10), ("C", Domains.Ruby, 24), ("D", Domains.Ruby, 6));
            Assert.IsTrue(rule.IsObjectiveReached(race, out var winner));
            Assert.AreEqual(Domains.Ruby, winner, "30 v 30 - Ruby got there first");
        }

        [Test]
        public void Scores_AreEachPilotsOwnGates_SoDomainTotalsAreTheTeamSums()
        {
            var rule = MakeRule();
            var race = MakeRace(24, ("A", Domains.Jade, 18), ("B", Domains.Jade, 15), ("C", Domains.Ruby, 24));
            rule.AssignScores(race, Domains.Jade, 99f);
            race.CalculateDomainStats(golfRules: false);

            float jade = 0f, ruby = 0f;
            foreach (var d in race.DomainStatsList)
            {
                if (d.Domain == Domains.Jade) jade = d.Score;
                if (d.Domain == Domains.Ruby) ruby = d.Score;
            }
            Assert.AreEqual(33f, jade);
            Assert.AreEqual(24f, ruby);
        }

        private sealed class FakeStats : IRoundStats
        {
        // The interface, stubbed (copied from RaceRankToastDriverTests). Only Name, Domain
        // and SwitchesThreaded are read by the rule; the rest exist so the stub compiles. A NetworkBehaviour (the real RoundStats) is deliberately NOT
        // used - adding one in an edit-mode test drags network lifecycle into a test
        // about ranking arithmetic.
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
