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
    }
}
#endif
