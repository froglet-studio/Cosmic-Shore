#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Drumfire's scoring contract - the two halves that make it the odd one out of the family:
    /// it is scored in VOLUME (the platform's only float-backed metric) and its objective is
    /// never reached, because the CLOCK ends it.
    ///
    /// <para>The rule is loaded from its SHIPPED ASSET rather than
    /// <c>CreateInstance</c>d, deliberately: <c>metric</c> and <c>golfRules</c> are serialized
    /// fields, so a fresh instance carries the class defaults (Crystals, golf) and a test built
    /// on one would pass while the thing that actually ships was authored wrong. What is under
    /// test here is the asset <c>Tools/Build/author_drumfire_assets.py</c> writes.</para>
    /// </summary>
    [TestFixture]
    public class DrumfireScoringTests
    {
        const string RulePath = "Assets/_SO_Assets/Scoring Rules/DrumfireScoringRule.asset";

        ScoringRuleSO _rule;
        GameDataSO _gameData;

        [SetUp]
        public void SetUp()
        {
            _rule = AssetDatabase.LoadAssetAtPath<ScoringRuleSO>(RulePath);
            Assert.IsNotNull(_rule, $"Drumfire's scoring rule is missing from {RulePath}. " +
                                    "Re-run Tools/Build/author_drumfire_assets.py.");

            _gameData = ScriptableObject.CreateInstance<GameDataSO>();
            _gameData.RoundStatsList = new List<IRoundStats>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_gameData);

        static IRoundStats Pilot(string name, Domains domain, float volume, int prisms = 0) =>
            new FakePilot { Name = name, Domain = domain, HostileVolumeDestroyed = volume, HostilePrismsDestroyed = prisms };

        void Roster(int domains, params IRoundStats[] pilots)
        {
            _gameData.RequestedDomainCount = domains;
            _gameData.RoundStatsList.Clear();
            _gameData.RoundStatsList.AddRange(pilots);
        }

        // -- The authored asset ------------------------------------------------

        [Test]
        public void ShippedRule_ScoresVolumeAndIsNotGolf()
        {
            Assert.AreEqual(ScoringMetric.VolumeDestroyed, _rule.Metric,
                "Drumfire's rule is not on the volume metric, so the drum's shielded ribs would " +
                "be worth exactly as much as its skin and the aiming lesson would not reach the score.");
            Assert.IsFalse(_rule.GolfRules,
                "Drumfire is a points mode - golf rules would rank the pilot who tore out the LEAST.");
        }

        [Test]
        public void ShippedRule_IsTheDrumfireType() =>
            Assert.IsInstanceOf<DrumfireScoringRuleSO>(_rule,
                "The asset at the Drumfire path is not a DrumfireScoringRuleSO.");

        // -- Only the clock ends a Drumfire turn -------------------------------

        [Test]
        public void Objective_IsNeverReached_NoMatterHowMuchVolumeIsTornOut()
        {
            // Every sibling rule answers true here once its target is met, and that is exactly
            // the trap: this mode has no target, so a rule that ever answered true would end the
            // match early and hand the win to whoever crossed an invented threshold first.
            Roster(2, Pilot("A", Domains.Jade, 5_000_000f), Pilot("B", Domains.Ruby, 1f));

            Assert.IsFalse(_rule.IsObjectiveReached(_gameData, out var winner),
                "A Drumfire turn reported its objective reached. Only the clock may end it.");
            Assert.AreEqual(Domains.Blue, winner,
                "An unreached objective must report the no-team sentinel, not a live domain.");
        }

        [Test]
        public void Objective_ReportsNoRemainingCount()
        {
            // Remaining() is max(0, target - sum); with a target of 0 the goal row draws the
            // running count with no denominator, which is the honest readout for a mode you
            // cannot finish.
            Roster(2, Pilot("A", Domains.Jade, 400f));
            Assert.AreEqual(0, _rule.Remaining(_gameData, Domains.Jade));
            Assert.AreEqual(0, _rule.Remaining(_gameData, Domains.Ruby));
        }

        // -- Volume is read, rounded, and summed per domain --------------------

        [Test]
        public void Metric_RoundsTheFloatVolumeToTheSharedIntContract()
        {
            // VolumeDestroyed is the only float-backed metric; it is rounded ONCE, in
            // ScoringMetrics.Read, so every downstream consumer keeps the int contract.
            Assert.AreEqual(413, ScoringMetrics.Read(Pilot("A", Domains.Jade, 412.6f), ScoringMetric.VolumeDestroyed));
            Assert.AreEqual(412, ScoringMetrics.Read(Pilot("A", Domains.Jade, 412.4f), ScoringMetric.VolumeDestroyed));
            Assert.AreEqual(0, ScoringMetrics.Read(Pilot("A", Domains.Jade, 0f), ScoringMetric.VolumeDestroyed));
        }

        [Test]
        public void Winner_IsTheDomainWithTheLARGEST_SUM_SoTeammatesCombine()
        {
            // Two pilots each tearing out 300 must beat one tearing out 500 - the mode is a
            // DOMAIN race, and a rule that compared best-individual would make a teammate worthless.
            Roster(2,
                Pilot("solo", Domains.Jade, 500f),
                Pilot("pair-a", Domains.Ruby, 300f),
                Pilot("pair-b", Domains.Ruby, 300f));

            Assert.AreEqual(Domains.Ruby, _rule.ResolveWinner(_gameData));
        }

        [Test]
        public void Winner_BreaksTiesByDomainOrderSoEveryPeerAgrees()
        {
            Roster(3,
                Pilot("a", Domains.Jade, 250f),
                Pilot("b", Domains.Ruby, 250f),
                Pilot("c", Domains.Gold, 250f));

            Assert.AreEqual(Domains.Jade, _rule.ResolveWinner(_gameData),
                "A tie must resolve identically on every machine - by ActiveDomains order.");
        }

        // -- Scores are the raw metric, not a sentinel -------------------------

        [Test]
        public void AssignScores_GivesEveryPilotTheVolumeTheyToreOut()
        {
            // No golf sentinel encoding: this is a points mode, so the raw metric IS the ranking
            // and two teammates are ordered by their own contribution rather than tied.
            var winner = Pilot("winner", Domains.Jade, 900.4f);
            var mate = Pilot("mate", Domains.Jade, 120f);
            var loser = Pilot("loser", Domains.Ruby, 640f);
            Roster(2, winner, mate, loser);

            _rule.AssignScores(_gameData, Domains.Jade, finishTime: 75f);

            Assert.AreEqual(900f, winner.Score, 0.5f);
            Assert.AreEqual(120f, mate.Score, 0.5f);
            Assert.AreEqual(640f, loser.Score, 0.5f,
                "A losing pilot's score was overwritten with a penalty - Drumfire has none.");
            Assert.Greater(winner.Score, mate.Score,
                "Teammates were flattened to one score, so the scoreboard cannot separate them.");
        }

        [Test]
        public void AssignScores_IgnoresFinishTime()
        {
            // finishTime is in the shared signature but means nothing to a points mode; passing
            // a different one must not move a single score.
            var pilot = Pilot("a", Domains.Jade, 700f);
            Roster(2, pilot, Pilot("b", Domains.Ruby, 100f));

            _rule.AssignScores(_gameData, Domains.Jade, 10f);
            float atTen = pilot.Score;
            _rule.AssignScores(_gameData, Domains.Jade, 9999f);

            Assert.AreEqual(atTen, pilot.Score, "Match length leaked into a points score.");
        }

        // -- Results are identical on every peer -------------------------------

        [Test]
        public void Results_RankByVolumeDescendingWithADeterministicTieBreak()
        {
            Roster(2,
                Pilot("low", Domains.Ruby, 100f, prisms: 4),
                Pilot("high", Domains.Jade, 900f, prisms: 40),
                // Same score as "high" - broken on prisms, then ordinal name.
                Pilot("high-fewer-prisms", Domains.Ruby, 900f, prisms: 9));

            _rule.AssignScores(_gameData, Domains.Jade, 75f);
            var results = _rule.BuildResults(_gameData);

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual("high", results[0].Name,
                "A score tie was not broken on prisms smashed, so two peers could rank differently.");
            Assert.AreEqual("high-fewer-prisms", results[1].Name);
            Assert.AreEqual("low", results[2].Name);
        }

        [Test]
        public void PlacementOrder_RanksDomainsByTeamTotalForMaelstromStandings()
        {
            Roster(3,
                Pilot("j", Domains.Jade, 100f),
                Pilot("r1", Domains.Ruby, 90f),
                Pilot("r2", Domains.Ruby, 90f),
                Pilot("g", Domains.Gold, 150f));

            CollectionAssert.AreEqual(
                new[] { Domains.Ruby, Domains.Gold, Domains.Jade },
                _rule.ResolvePlacementOrder(_gameData));
        }

        [Test]
        public void Drumfire_PaysNothingForGunnery() =>
            // Counted platform-wide, scored only where a mode says so - and this mode's score is
            // what a pilot did to the drum, not to another pilot.
            Assert.AreEqual(0, _rule.PointsForCombatHit(CombatHitClass.Bullet));

        // -- Test double -------------------------------------------------------

        class FakePilot : IRoundStats
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
            public event Action<IRoundStats> OnLifeformsKilledChanged;
            public event Action<IRoundStats> OnBulletHitsLandedChanged;
            public event Action<IRoundStats> OnMissileHitsLandedChanged;
            public event Action<IRoundStats> OnDebuffHitsLandedChanged;
            public event Action<IRoundStats> OnCombatPointsChanged;
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
            public int LifeformsKilled { get; set; }
            public int BulletHitsLanded { get; set; }
            public int MissileHitsLanded { get; set; }
            public int DebuffHitsLanded { get; set; }
            public int CombatPoints { get; set; }
            public float FullSpeedStraightAbilityActiveTime { get; set; }
            public float RightStickAbilityActiveTime { get; set; }
            public float LeftStickAbilityActiveTime { get; set; }
            public float FlipAbilityActiveTime { get; set; }
            public float Button1AbilityActiveTime { get; set; }
            public float Button2AbilityActiveTime { get; set; }
            public float Button3AbilityActiveTime { get; set; }
        }
    }
}
#endif
