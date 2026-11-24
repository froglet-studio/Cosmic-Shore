#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The in-game voyage screens' rules (Frogma Rev 134 / Rev 137, Docs/MENU_REDESIGN_B.md sections 227 and 230): the detour
    /// vote (Vote B), one award per seat (Awards A), the rival's open hull and rout, and the pause chart's layout (Pause A).
    /// Pure rules, so the headless voyage harness runs them too (Tools/Build/voyage_depth_harness).
    /// </summary>
    public class VoyageScreensTests
    {
        static VoyageVote Vote(params bool[] human) => new(human.Length, human) { Host = 0, Asker = 1 };

        [Test]
        public void ASoloPressDecidesAtOnce()
        {
            var v = Vote(true, false, false);
            Assert.IsTrue(v.Solo);
            Assert.IsTrue(v.Cast(0, VoyageVote.Detour));
            Assert.IsTrue(v.Closed);
            Assert.AreEqual(VoyageVote.Detour, v.Outcome);
            Assert.AreEqual(VoyageVoteEnd.Solo, v.End);
        }

        [Test]
        public void NoPressInFiveSecondsPressesOn()
        {
            var v = Vote(true, false, false);
            v.Tick(4.9f);
            Assert.IsFalse(v.Closed);
            v.Tick(0.2f);
            Assert.IsTrue(v.Closed);
            Assert.AreEqual(VoyageVote.PressOn, v.Outcome);
            Assert.AreEqual(VoyageVoteEnd.NoPress, v.End);
        }

        [Test]
        public void APartyVoteWaitsForEveryHumanThenTheMostPressesWin()
        {
            var v = Vote(true, true, true);
            v.Cast(0, VoyageVote.Detour);
            v.Cast(1, VoyageVote.Detour);
            Assert.IsFalse(v.Closed, "one human has not pressed");
            v.Cast(2, VoyageVote.PressOn);
            Assert.IsTrue(v.Closed);
            Assert.AreEqual(VoyageVote.Detour, v.Outcome);
            Assert.AreEqual(VoyageVoteEnd.Majority, v.End);
        }

        [Test]
        public void ATieGoesToTheHostsPress()
        {
            // Rev 134 VoteTie: one each, and the third human did not press.
            var v = Vote(true, true, true);
            v.Cast(0, VoyageVote.Detour);
            v.Cast(1, VoyageVote.PressOn);
            v.Tick(5f);
            Assert.AreEqual(VoyageVote.Detour, v.Outcome);
            Assert.AreEqual(VoyageVoteEnd.HostBrokeTie, v.End);
        }

        [Test]
        public void ATieTheHostDidNotPressPressesOn()
        {
            var v = Vote(true, true, false, true);
            v.Cast(1, VoyageVote.Detour);
            v.Cast(3, VoyageVote.PressOn);
            v.Tick(5f);
            Assert.AreEqual(VoyageVote.PressOn, v.Outcome);
            Assert.AreEqual(VoyageVoteEnd.NoPress, v.End);
        }

        [Test]
        public void AnAiVoteCountsButNeverClosesTheVote()
        {
            var v = Vote(true, true, false);
            Assert.IsTrue(v.CastAi(2, VoyageVote.PressOn));
            Assert.IsFalse(v.Cast(2, VoyageVote.Detour), "an AI seat is not pressed by a human");
            v.Cast(0, VoyageVote.Detour);
            Assert.IsFalse(v.Closed);
            v.Cast(1, VoyageVote.PressOn);
            Assert.IsTrue(v.Closed);
            Assert.AreEqual(VoyageVote.PressOn, v.Outcome, "two to one with the AI");
        }

        [Test]
        public void APressAfterTheVoteClosedDoesNotCount()
        {
            var v = Vote(true, false);
            v.Cast(0, VoyageVote.PressOn);
            Assert.IsFalse(v.Cast(0, VoyageVote.Detour));
            Assert.AreEqual(VoyageVote.PressOn, v.Outcome);
        }

        [Test]
        public void EverySeatGetsOneAwardAndNoAwardTwice()
        {
            var seats = new List<VoyageAwardScore>
            {
                new() { Crystals = 19, Kills = 2, Guarded = 1 },
                new() { Crystals = 5, Kills = 1, Guarded = 9 },
                new() { Crystals = 6, Kills = 4, Guarded = 2, RivalHull = 0.4f },
            };
            var awards = VoyageAwards.Pick(seats);
            CollectionAssert.AreEqual(new[] { VoyageAward.TopSalvager, VoyageAward.WallOfTheVoyage, VoyageAward.RivalBreaker }, awards);
            Assert.AreEqual(75, VoyageAwards.GuardedPercent(seats, 1));
        }

        [Test]
        public void ASeatWithNothingSailedWithHer()
        {
            var seats = new List<VoyageAwardScore> { new() { Crystals = 3 }, new() };
            var awards = VoyageAwards.Pick(seats);
            Assert.AreEqual(VoyageAward.TopSalvager, awards[0]);
            Assert.AreEqual(VoyageAward.Crew, awards[1]);
        }

        [Test]
        public void ARunawayRoutsOnlyWhenAllThreeAreSilencedAtOnce()
        {
            var crew = new VoyageRivalFix { Kind = VoyageRivalKind.Runaway };
            crew.Escorts.Add(new VoyageEscortFix { State = VoyageEscortState.Silenced, SilencedSeconds = 6f });
            crew.Escorts.Add(new VoyageEscortFix { State = VoyageEscortState.Silenced, SilencedSeconds = 2f });
            crew.Escorts.Add(new VoyageEscortFix { State = VoyageEscortState.Guarding });
            Assert.IsFalse(crew.AllSilenced);
            Assert.AreEqual(2, crew.Silenced);
            Assert.AreEqual(2f, crew.OpenSeconds, 1e-4f, "her hull is open until the first escort recovers");
            crew.Escorts[2] = new VoyageEscortFix { State = VoyageEscortState.Silenced, SilencedSeconds = 4f };
            Assert.IsTrue(crew.AllSilenced);
        }

        [Test]
        public void TheChartFitsTheLegAndSpreadsItsCells()
        {
            var places = new List<VoyageChartLayout.Point> { new(0f, 0f), new(10f, 5f), new(20f, 0f) };
            var fit = VoyageChartLayout.Fit(places, 100f, 100f, 1000f, 1000f);
            Assert.AreEqual(100f, fit[0].X, 1e-3f);
            Assert.AreEqual(1100f, fit[2].X, 1e-3f);
            Assert.Less(fit[1].Y, fit[0].Y, "chart y up is screen y down");
            Assert.AreEqual(0.5f / 3f, VoyageChartLayout.CellShare(1, 3), 1e-5f);
            Assert.AreEqual(2.5f / 3f, VoyageChartLayout.CellShare(3, 3), 1e-5f);
            var line = new List<VoyageChartLayout.Point> { new(0f, 0f), new(100f, 0f) };
            var mid = VoyageChartLayout.Along(line, 0.5f);
            Assert.AreEqual(50f, mid.X, 1e-3f);
            var port = VoyageChartLayout.Beside(line, 0.5f, 90f, 10f);
            Assert.AreEqual(50f, port.X, 1e-3f);
            Assert.AreEqual(-10f, port.Y, 1e-3f, "port is up the screen on a lane sailing right");
        }

        [Test]
        public void AOnePlaceLegSitsInTheMiddle()
        {
            var fit = VoyageChartLayout.Fit(new List<VoyageChartLayout.Point> { new(4f, 4f) }, 0f, 0f, 200f, 100f);
            Assert.AreEqual(100f, fit[0].X, 1e-3f);
            Assert.AreEqual(50f, fit[0].Y, 1e-3f);
        }
    }
}
#endif
