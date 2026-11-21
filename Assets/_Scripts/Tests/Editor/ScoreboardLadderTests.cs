#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rev 8 scoreboard's layout rules (Docs/MENU_REDESIGN_B.md §49, option B): team headers only when a domain fields
    /// two or more pilots, the team's shared score said once, and each pilot's share of the team's count.
    /// </summary>
    public class ScoreboardLadderTests
    {
        static ScoreResult R(int rank, string name, Domains d, string score, string secondary) =>
            new(rank, name, d, 0f, score, secondary);

        // The owner's Skim Race: Jade 3 v Ruby 1, the race time is the team's.
        static List<ScoreResult> SkimRace() => new()
        {
            R(1, "FriendlyAI", Domains.Jade, "00:44:93", "12 Crystals"),
            R(2, "Shomboss", Domains.Jade, "00:44:93", "8 Crystals"),
            R(3, "DragonDragsJoin", Domains.Jade, "00:44:93", "0 Crystals"),
            R(4, "Barrett", Domains.Ruby, "15 Crystals Left", "5 Crystals"),
        };

        [Test]
        public void TeamsGetAHeaderAboveTheirRows()
        {
            var e = ScoreboardLadder.Build(SkimRace(), Domains.Jade);
            Assert.AreEqual(6, e.Count);
            Assert.IsTrue(e[0].IsHeader);
            Assert.AreEqual(Domains.Jade, e[0].Domain);
            Assert.IsTrue(e[0].Won);
            Assert.IsTrue(e[4].IsHeader);
            Assert.AreEqual(Domains.Ruby, e[4].Domain);
            Assert.IsFalse(e[4].Won);
            Assert.AreEqual(3, e[5].ResultIndex);
        }

        [Test]
        public void ASharedTeamTimeIsSaidOnceInTheHeader()
        {
            var e = ScoreboardLadder.Build(SkimRace(), Domains.Jade);
            Assert.AreEqual("00:44:93", e[0].SharedScore);
            for (int i = 1; i <= 3; i++) Assert.IsFalse(e[i].ShowsScore, "a Jade row repeats the team time");
            // A one-pilot team has nothing to share: its row keeps its own score.
            Assert.IsNull(e[4].SharedScore);
            Assert.IsTrue(e[5].ShowsScore);
        }

        [Test]
        public void EachRowCarriesItsShareOfTheTeamCount()
        {
            var e = ScoreboardLadder.Build(SkimRace(), Domains.Jade);
            Assert.AreEqual(0.6f, e[1].Share, 1e-4f);
            Assert.AreEqual(0.4f, e[2].Share, 1e-4f);
            Assert.AreEqual(0f, e[3].Share, 1e-4f);
            Assert.AreEqual(1f, e[5].Share, 1e-4f);
        }

        [Test]
        public void EveryPilotOnTheirOwnDomainIsAPlainLadder()
        {
            var e = ScoreboardLadder.Build(new List<ScoreResult>
            {
                R(1, "A", Domains.Jade, "12", null), R(2, "B", Domains.Ruby, "9", null), R(3, "C", Domains.Gold, "4", null),
            }, Domains.Blue);
            Assert.AreEqual(3, e.Count);
            foreach (var entry in e)
            {
                Assert.IsFalse(entry.IsHeader);
                Assert.IsTrue(entry.ShowsScore);
                Assert.AreEqual(-1f, entry.Share);
            }
            Assert.IsTrue(e[0].Won, "with no winning domain set, the first result's domain won");
        }

        [Test]
        public void DifferentScoresStayOnTheirRows()
        {
            var e = ScoreboardLadder.Build(new List<ScoreResult>
            {
                R(1, "A", Domains.Jade, "12 Crystals", null), R(2, "B", Domains.Jade, "7 Crystals", null),
            }, Domains.Jade);
            Assert.IsNull(e[0].SharedScore);
            Assert.IsTrue(e[1].ShowsScore);
            Assert.AreEqual(-1f, e[1].Share, "no secondary line: no share line");
        }

        [Test]
        public void ALeadingNumberIsReadAndNothingElse()
        {
            Assert.IsTrue(ScoreboardLadder.TryLeadingNumber("12 Crystals", out var a));
            Assert.AreEqual(12, a);
            Assert.IsTrue(ScoreboardLadder.TryLeadingNumber("  7", out var b));
            Assert.AreEqual(7, b);
            Assert.IsFalse(ScoreboardLadder.TryLeadingNumber("Jousts: 3", out _));
            Assert.IsFalse(ScoreboardLadder.TryLeadingNumber(null, out _));
        }

        // Rev 23 D, the board: Ruby (Kai, Kestrel) beats Jade (Bob, Cassian) - listed loser-first to prove the winner
        // still takes the left.
        static List<ScoreResult> TwoVTwo() => new()
        {
            R(3, "Bob", Domains.Jade, "7", "9 Crystals"),
            R(4, "Cassian", Domains.Jade, "5", "7 Crystals"),
            R(1, "Kai", Domains.Ruby, "9", "14 Crystals"),
            R(2, "Kestrel", Domains.Ruby, "8", "12 Crystals"),
        };

        [Test]
        public void Versus_PutsTheWinnerOnTheLeftWithItsRows()
        {
            var v = ScoreboardLadder.BuildVersus(TwoVTwo(), Domains.Ruby);
            Assert.IsNotNull(v);
            Assert.AreEqual(Domains.Ruby, v[0].Domain);
            Assert.IsTrue(v[0].Won);
            Assert.IsFalse(v[1].Won);
            CollectionAssert.AreEqual(new[] { 2, 3 }, v[0].Rows.ConvertAll(r => r.ResultIndex));
            CollectionAssert.AreEqual(new[] { 0, 1 }, v[1].Rows.ConvertAll(r => r.ResultIndex));
        }

        [Test]
        public void Versus_AddsWholeNumberScoresAndSplitsTheCount()
        {
            var v = ScoreboardLadder.BuildVersus(TwoVTwo(), Domains.Ruby);
            Assert.AreEqual("17", v[0].Score);
            Assert.AreEqual("12", v[1].Score);
            Assert.IsTrue(v[0].Rows[0].ShowsScore, "members with their own scores keep them");
            Assert.AreEqual(26f / 42f, v[0].Share, 1e-4f);
            Assert.AreEqual(16f / 42f, v[1].Share, 1e-4f);
        }

        [Test]
        public void Versus_SaysASharedTeamTimeOnceAndLeavesATimeUnadded()
        {
            var v = ScoreboardLadder.BuildVersus(SkimRace(), Domains.Jade);
            Assert.IsNotNull(v, "3 v 1 is still two teams");
            Assert.AreEqual("00:44:93", v[0].Score);
            Assert.IsFalse(v[0].Rows[0].ShowsScore);
            Assert.IsNull(v[1].Score, "a lone time is not added up");
        }

        [Test]
        public void Versus_StaysTheLadderForFreeForAllOneVOneAndThreeTeams()
        {
            var ffa = new List<ScoreResult> { R(1, "A", Domains.Jade, "3", ""), R(2, "B", Domains.Ruby, "2", ""), R(3, "C", Domains.Gold, "1", "") };
            Assert.IsNull(ScoreboardLadder.BuildVersus(ffa, Domains.Jade));
            var oneVOne = new List<ScoreResult> { R(1, "A", Domains.Jade, "3", ""), R(2, "B", Domains.Ruby, "2", "") };
            Assert.IsNull(ScoreboardLadder.BuildVersus(oneVOne, Domains.Jade));
            var three = new List<ScoreResult> { R(1, "A", Domains.Jade, "3", ""), R(2, "B", Domains.Jade, "2", ""),
                                                R(3, "C", Domains.Ruby, "1", ""), R(4, "D", Domains.Gold, "0", "") };
            Assert.IsNull(ScoreboardLadder.BuildVersus(three, Domains.Jade));
        }

        [Test]
        public void Versus_HidesTheShareBarWhenALineHasNoNumber()
        {
            var r = TwoVTwo();
            r[1] = R(4, "Cassian", Domains.Jade, "5", "Left early");
            var v = ScoreboardLadder.BuildVersus(r, Domains.Ruby);
            Assert.AreEqual(-1f, v[0].Share);
            Assert.AreEqual(-1f, v[1].Share);
        }
    }
}
#endif
