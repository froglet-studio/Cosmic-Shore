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
    /// Glasshouse (the Angler's Mass game, GLASSHOUSE.md): the garden arithmetic and the scoring rule. The score is
    /// a pilot's VolumeRemaining - the trail <c>StatsManager</c> counts, plus their share of their domain's garden, which
    /// the controller adds by DELTAS through <see cref="GlasshouseGarden.ShareBook"/>. These hold the arithmetic that
    /// keeps the two producers from overwriting each other, and the rule that a live stock is decided only by the clock.
    /// </summary>
    [TestFixture]
    public class GlasshouseScoringTests
    {
        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        GlasshouseScoringRuleSO MakeRule()
        {
            var rule = Track(ScriptableObject.CreateInstance<GlasshouseScoringRuleSO>());
            typeof(ScoringRuleSO).GetField("metric", System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, ScoringMetric.VolumeRemaining);
            typeof(ScoringRuleSO).GetField("golfRules", System.Reflection.BindingFlags.Instance |
                                                        System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, false);
            return rule;
        }

        GameDataSO MakeMatch(int domainCount, params (string name, Domains domain, float volume)[] pilots)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = domainCount;
            foreach (var (name, domain, volume) in pilots)
                gameData.RoundStatsList.Add(new FakeStats { Name = name, Domain = domain, VolumeRemaining = volume });
            return gameData;
        }

        // ── The garden arithmetic ──

        [Test]
        public void PlantVolume_IsPrismsTimesLeaf_AndNeverNegative()
        {
            Assert.AreEqual(160f * 75f, GlasshouseGarden.PlantVolume(160, 75f), 1e-3f);
            Assert.AreEqual(0f, GlasshouseGarden.PlantVolume(0, 75f));
            Assert.AreEqual(0f, GlasshouseGarden.PlantVolume(-3, 75f));
            Assert.AreEqual(0f, GlasshouseGarden.PlantVolume(10, 0f));
        }

        [Test]
        public void Share_SplitsTheGardenEvenly_AndAnEmptyDomainSharesNothing()
        {
            Assert.AreEqual(500f, GlasshouseGarden.Share(1000f, 2), 1e-3f);
            Assert.AreEqual(1000f, GlasshouseGarden.Share(1000f, 1), 1e-3f);
            Assert.AreEqual(0f, GlasshouseGarden.Share(1000f, 0));
            Assert.AreEqual(0f, GlasshouseGarden.Share(-5f, 2));
        }

        [Test]
        public void Slot_IsThePlayableDomainsOnly()
        {
            Assert.AreEqual(0, GlasshouseGarden.Slot(Domains.Jade));
            Assert.AreEqual(1, GlasshouseGarden.Slot(Domains.Ruby));
            Assert.AreEqual(2, GlasshouseGarden.Slot(Domains.Gold));
            Assert.AreEqual(-1, GlasshouseGarden.Slot(Domains.Blue), "Blue is no team: its plants are nobody's garden");
        }

        [Test]
        public void ShareBook_MovesTheStatByTheChangeOnly()
        {
            var book = new GlasshouseGarden.ShareBook();
            Assert.AreEqual(300f, book.Settle("A", 300f), 1e-3f, "first sample credits the whole share");
            Assert.AreEqual(200f, book.Settle("A", 500f), 1e-3f, "growth credits only what grew");
            Assert.AreEqual(-350f, book.Settle("A", 150f), 1e-3f, "grazing takes back what was eaten");
            Assert.AreEqual(0f, book.Settle("A", 150f), 1e-3f, "nothing moved, nothing credited");
            Assert.AreEqual(150f, book.Credited("A"), 1e-3f);
        }

        [Test]
        public void ShareBook_ComposesWithTheTrailCount()
        {
            // The stat is written by TWO producers: StatsManager's trail (+= / -= per prism) and the garden (+= delta).
            // Interleaving them in any order must leave "trail standing + garden standing".
            var book = new GlasshouseGarden.ShareBook();
            float stat = 0f;
            stat += 82.5f;                       // a trail prism laid
            stat += book.Settle("A", 1000f);     // the garden grows
            stat += 82.5f;                       // another trail prism
            stat -= 82.5f;                       // a grazer eats one
            stat += book.Settle("A", 600f);      // the garden is grazed
            Assert.AreEqual(82.5f + 600f, stat, 1e-3f);
        }

        [Test]
        public void ShareBook_KeepsPilotsApart_AndClears()
        {
            var book = new GlasshouseGarden.ShareBook();
            book.Settle("A", 100f);
            Assert.AreEqual(50f, book.Settle("B", 50f), 1e-3f);
            Assert.AreEqual(100f, book.Credited("A"), 1e-3f);
            book.Clear();
            Assert.AreEqual(0f, book.Credited("A"));
            Assert.AreEqual(100f, book.Settle("A", 100f), 1e-3f, "a new round credits from zero");
            Assert.AreEqual(0f, book.Settle(null, 10f), "an unnamed pilot is never credited");
        }

        [Test]
        public void BedFor_SitsBetweenTheNucleusAndTheMembrane_OnADifferentDirectionPerSeat()
        {
            var beds = new List<Vector3>();
            for (int seat = 0; seat < 4; seat++)
            {
                var bed = GlasshouseGardener.BedFor(seat, Vector3.zero, 392f, 1200f, 0.35f);
                Assert.AreEqual(392f + 0.35f * (1200f - 392f), bed.magnitude, 0.5f, $"seat {seat}");
                foreach (var other in beds)
                    Assert.Greater(Vector3.Distance(bed, other), 200f, $"seat {seat} shares a bed");
                beds.Add(bed);
            }
        }

        // ── The rule ──

        [Test]
        public void Rule_IsDecidedOnlyByTheClock()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 1e9f), ("B", Domains.Ruby, 0f));
            Assert.IsFalse(rule.IsObjectiveReached(match, out var winner),
                "a live stock must never end the round on a number true for one frame");
            Assert.AreEqual(Domains.Blue, winner);
        }

        [Test]
        public void Rule_MostVolumeStanding_Wins_SummedByDomain()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 400f), ("B", Domains.Jade, 400f), ("C", Domains.Ruby, 700f));
            Assert.AreEqual(Domains.Jade, rule.ResolveWinner(match), "two pilots' 800 beats one pilot's 700");
        }

        [Test]
        public void Rule_AssignsEachPilotTheVolumeTheyHold()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 1234.4f), ("B", Domains.Ruby, 99f));
            rule.AssignScores(match, Domains.Jade, 90f);
            Assert.AreEqual(1234f, match.RoundStatsList[0].Score, 1e-3f);
            Assert.AreEqual(99f, match.RoundStatsList[1].Score, 1e-3f);
        }

        [Test]
        public void Rule_Results_ReadTheAuthoredTemplates_AndTheGardenBreakdown()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 900f), ("B", Domains.Ruby, 300f));
            rule.AssignScores(match, Domains.Jade, 90f);
            var rows = rule.BuildResults(match, name => name == "A" ? 600.4f : 0f);
            Assert.AreEqual("A", rows[0].Name);
            Assert.AreEqual("900 Volume", rows[0].ScoreText);
            Assert.AreEqual("600 grown", rows[0].Secondary);
            Assert.AreEqual("0 grown", rows[1].Secondary);

            var blank = rule.BuildResults(match);
            Assert.IsTrue(string.IsNullOrEmpty(blank[0].Secondary),
                "with no snapshot the breakdown is blank rather than a guess");
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
