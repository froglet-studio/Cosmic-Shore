#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;
using FakeStats = CosmicShore.Tests.ScoringRuleFieldedDomainTests.FakeStats;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Mass-Space volume books (Garrett, 2026-11-18): VC is the volume a pilot increased in their own domain, VD the
    /// volume they reduced from an opposing domain, and with two teams the difference of the team totals is the
    /// difference of the volume they have left.
    /// </summary>
    public class VolumeLedgerTests
    {
        const Domains Jade = Domains.Jade, Ruby = Domains.Ruby;

        [Test]
        public void ASteal_CountsAsBoth_ForTheThief_AndAsALossForTheVictim()
        {
            var l = new VolumeLedger();
            l.AddCreated("ann", 10f);                      // the thief's VC (StatsManager.CreditPrismSteal)
            l.BookLoss("bo", Ruby, "ann", Jade, 10f);      // the victim's loss and the thief's VD (DebitPrismSteal)
            Assert.AreEqual(10f, l.Get("ann").Created);
            Assert.AreEqual(10f, l.Get("ann").Reduced);
            Assert.AreEqual(20f, l.Get("ann").Total);
            Assert.AreEqual(10f, l.Get("bo").Lost);
        }

        [Test]
        public void Growing_IsCreated_AndShrinking_IsLost()
        {
            var l = new VolumeLedger();
            l.AddDelta("ann", 4f);
            l.AddDelta("ann", -1.5f);
            Assert.AreEqual(4f, l.Get("ann").Created);
            Assert.AreEqual(1.5f, l.Get("ann").Lost);
            Assert.AreEqual(2.5f, l.Get("ann").Remaining);
        }

        [Test]
        public void DestroyingYourOwnTeamsVolume_IsNoContribution()
        {
            var l = new VolumeLedger();
            l.BookLoss("bo", Ruby, "cy", Ruby, 6f);
            Assert.AreEqual(0f, l.Get("cy").Reduced, "a teammate's prism is not opposing volume");
            Assert.AreEqual(6f, l.Get("bo").Lost);
            l.BookLoss("bo", Ruby, null, Domains.Blue, 2f);
            Assert.AreEqual(8f, l.Get("bo").Lost, "fauna or the arena: the victim still loses it, nobody gains it");
        }

        [Test]
        public void ShieldExtra_IsTheShellLessTheBox()
        {
            Assert.AreEqual(0f, VolumeLedger.ShieldExtra(2f, false, false));
            Assert.AreEqual(2f * (OctahedronMeshGenerator.SHIELD_TO_BOX_VOLUME_RATIO - 1f),
                VolumeLedger.ShieldExtra(2f, true, false), 1e-4f);
            Assert.AreEqual(2f * (StellatedOctahedronMeshGenerator.SUPER_SHIELD_TO_BOX_VOLUME_RATIO - 1f),
                VolumeLedger.ShieldExtra(2f, true, true), 1e-4f, "super-shield wins over shield");
        }

        [Test]
        public void TwoTeams_TheDifferenceOfTotals_IsTheDifferenceOfVolumeLeft_WhenOnlyPilotsMovedVolume()
        {
            var l = new VolumeLedger();
            // Jade lays 30 and 20, Ruby lays 25; Jade destroys 8 of Ruby's, Ruby steals 5 of Jade's, a Jade shield adds 3.
            l.AddCreated("ann", 30f); l.AddCreated("cy", 20f); l.AddCreated("bo", 25f);
            l.BookLoss("bo", Ruby, "ann", Jade, 8f);
            l.AddCreated("bo", 5f); l.BookLoss("cy", Jade, "bo", Ruby, 5f);
            l.AddDelta("ann", 3f);

            // What the field holds at the end: Jade 30+3 and 20-5, Ruby 25-8+5.
            l.CountOwned(new[] { "ann", "cy", "bo" }, new[]
            {
                new KeyValuePair<string, float>("ann", 33f),
                new KeyValuePair<string, float>("cy", 15f),
                new KeyValuePair<string, float>("bo", 22f),
            });

            var jade = l.Sum(new[] { "ann", "cy" });
            var ruby = l.Sum(new[] { "bo" });
            Assert.IsTrue(VolumeLedger.Balances(jade.Total - ruby.Total, jade.Counted - ruby.Counted),
                $"totals differ by {jade.Total - ruby.Total}, volume left by {jade.Counted - ruby.Counted}");
            Assert.IsTrue(VolumeLedger.Balances(jade.Remaining, jade.Counted), "each team's own books balance too");
        }

        [Test]
        public void TwoTeams_ALossNoPilotCaused_ShowsAsAGap()
        {
            var l = new VolumeLedger();
            l.AddCreated("ann", 30f); l.AddCreated("bo", 30f);
            l.BookLoss("ann", Jade, null, Domains.Blue, 10f);   // fauna ate 10 of Jade's
            l.CountOwned(new[] { "ann", "bo" }, new[]
            {
                new KeyValuePair<string, float>("ann", 20f),
                new KeyValuePair<string, float>("bo", 30f),
            });
            var jade = l.Get("ann");
            var ruby = l.Get("bo");
            Assert.IsFalse(VolumeLedger.Balances(jade.Total - ruby.Total, jade.Counted - ruby.Counted),
                "negative control: the check must catch volume that moved without a pilot");
            Assert.IsTrue(VolumeLedger.Balances(jade.Remaining, jade.Counted), "but Jade's own books still balance");
        }

        [Test]
        public void CountOwned_CountsEveryRosteredPilot_AndIgnoresTheRest()
        {
            var l = new VolumeLedger();
            l.CountOwned(new[] { "ann", "bo" }, new[]
            {
                new KeyValuePair<string, float>("ann", 2f),
                new KeyValuePair<string, float>("ann", 3f),
                new KeyValuePair<string, float>("flora", 99f),
            });
            Assert.AreEqual(5f, l.Get("ann").Counted);
            Assert.IsTrue(l.Get("bo").HasCount);
            Assert.AreEqual(0f, l.Get("bo").Counted, "a pilot who owns nothing counts 0");
            Assert.IsFalse(l.TryGet("flora", out _), "unrostered volume is not in the books");
        }

        [Test]
        public void SuperShieldsBroken_CountOnlyOnOpposingVolume()
        {
            var l = new VolumeLedger();
            l.BookLoss("bo", Ruby, "ann", Jade, 2f, superShielded: true);
            l.BookLoss("cy", Jade, "ann", Jade, 2f, superShielded: true);
            Assert.AreEqual(1, l.Get("ann").SuperShieldsBroken);
        }

        [Test]
        public void FormatVolume_StaysShort()
        {
            Assert.AreEqual("950", BroodRushScoringRuleSO.FormatVolume(950.4f));
            Assert.AreEqual("12.3k", BroodRushScoringRuleSO.FormatVolume(12345f));
            Assert.AreEqual("-12.3k", BroodRushScoringRuleSO.FormatVolume(-12345f));
            Assert.AreEqual("12.3M", BroodRushScoringRuleSO.FormatVolume(12345678f));
        }

        [Test]
        public void BroodRush_EveryTeammateScoresTheirOwnBooks_AndTheBroodsAreTheTeams()
        {
            var rule = ScriptableObject.CreateInstance<BroodRushScoringRuleSO>();
            var gameData = ScriptableObject.CreateInstance<GameDataSO>();
            try
            {
                typeof(ScoringRuleSO).GetField("metric", System.Reflection.BindingFlags.Instance |
                                                         System.Reflection.BindingFlags.NonPublic)
                                     .SetValue(rule, ScoringMetric.Goals);
                gameData.RequestedDomainCount = 2;
                // The controller carries all three Jade broods on the first roster entry, the team's counter.
                gameData.RoundStatsList.Add(new FakeStats { Name = "ann", Domain = Jade, GoalsScored = 3 });
                gameData.RoundStatsList.Add(new FakeStats { Name = "cy", Domain = Jade });
                gameData.RoundStatsList.Add(new FakeStats { Name = "bo", Domain = Ruby, GoalsScored = 1 });
                gameData.VolumeLedger.AddCreated("ann", 100f);
                gameData.VolumeLedger.AddCreated("cy", 300f);
                gameData.VolumeLedger.BookLoss("bo", Ruby, "cy", Jade, 50f);
                gameData.VolumeLedger.AddCreated("bo", 500f);

                Assert.AreEqual(Jade, rule.ResolveWinner(gameData), "the race is still the domain's broods");
                rule.AssignScores(gameData, Jade, 0f);
                Assert.AreEqual(100f, gameData.RoundStatsList[0].Score, 1e-3f, "ann: her VC, not the team's 3 broods");
                Assert.AreEqual(350f, gameData.RoundStatsList[1].Score, 1e-3f, "cy: VC 300 + VD 50, not 0 broods");
                Assert.AreEqual(500f, gameData.RoundStatsList[2].Score, 1e-3f);

                gameData.WinnerDomain = Jade;
                var results = rule.BuildResults(gameData);
                Assert.AreEqual(new[] { "cy", "ann", "bo" }, results.ConvertAll(r => r.Name).ToArray(),
                    "the winning team first, best score leading it");
                Assert.AreEqual("350", results[0].ScoreText);
                Assert.AreEqual("100", results[1].ScoreText);
                Assert.AreEqual(2, results[0].Rank, "the rank is the VC + VD order across every pilot");
                Assert.AreEqual(3, results[1].Rank);
                Assert.AreEqual(1, results[2].Rank);
                foreach (var r in results)
                    StringAssert.DoesNotContain("Brood", r.ScoreText + r.Secondary, "no pilot carries the team's broods");
                StringAssert.StartsWith("3 Broods", rule.TeamLine(gameData, Jade));
                StringAssert.StartsWith("1 Brood\n", rule.TeamLine(gameData, Ruby));
            }
            finally
            {
                Object.DestroyImmediate(rule);
                Object.DestroyImmediate(gameData);
            }
        }
    }
}
#endif
