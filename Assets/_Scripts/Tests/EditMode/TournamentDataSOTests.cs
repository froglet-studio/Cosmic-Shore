#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Obvious.Soap;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// TournamentDataSO Tests — validates the per-DOMAIN {2,1,0} placement scoring fold that
    /// powers the Tournament/Shuffle meta-mode.
    ///
    /// WHY THIS MATTERS:
    /// Standings are reduced LOCALLY on every peer from the already-synced GameDataSO.Results
    /// (no extra networking), so the fold MUST be deterministic and identical everywhere:
    ///   • a domain's place each game = the best (lowest) player Rank on that domain,
    ///   • domains ordered by that best rank (ties → enum order Jade→Ruby→Gold),
    ///   • placement crystals {2,1,0} awarded by place and accumulated across games,
    ///   • final sort: points desc, then best single placement, then enum order.
    /// A divergence here desyncs the leaderboard between host and clients.
    /// </summary>
    [TestFixture]
    public class TournamentDataSOTests
    {
        TournamentDataSO _data;
        readonly List<ScriptableEventNoParam> _events = new();

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<TournamentDataSO>();
            // RecordResults raises these SOAP events; wire throwaway instances (fail-loud in
            // production means they are never null there — here we supply test doubles).
            _data.OnTournamentStarted   = MakeEvent();
            _data.OnGameResultRecorded  = MakeEvent();
            _data.OnStandingsChanged    = MakeEvent();
            _data.OnTournamentCompleted = MakeEvent();
            // Be explicit and independent of the asset default.
            _data.PointsByPlace = new List<int> { 2, 1, 0 };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var e in _events) if (e) Object.DestroyImmediate(e);
            _events.Clear();
            Object.DestroyImmediate(_data);
        }

        ScriptableEventNoParam MakeEvent()
        {
            var e = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            _events.Add(e);
            return e;
        }

        // One ranked player row (only Rank + Domain matter to the fold).
        static ScoreResult Row(int rank, Domains domain) =>
            new ScoreResult(rank, $"P{rank}", domain, 0f, string.Empty, null);

        TournamentDomainStanding Standing(Domains d) =>
            _data.Standings.Find(s => s.Domain == d);

        [Test]
        public void RecordResults_AwardsByDomainPlacement_2_1_0()
        {
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) });

            Assert.AreEqual(2, Standing(Domains.Jade).TotalPoints, "1st-place domain gets 2.");
            Assert.AreEqual(1, Standing(Domains.Ruby).TotalPoints, "2nd-place domain gets 1.");
            Assert.AreEqual(0, Standing(Domains.Gold).TotalPoints, "3rd-place domain gets 0.");
        }

        [Test]
        public void RecordResults_DomainPlace_UsesBestPlayerRank()
        {
            // Jade has the worst AND the best player; its best (rank 1) decides its place → 1st.
            _data.RecordResults(new[]
            {
                Row(1, Domains.Jade),
                Row(2, Domains.Ruby),
                Row(3, Domains.Gold),
                Row(4, Domains.Jade),
            });

            Assert.AreEqual(2, Standing(Domains.Jade).TotalPoints, "Jade is 1st via its rank-1 player.");
            Assert.AreEqual(1, Standing(Domains.Ruby).TotalPoints);
            Assert.AreEqual(0, Standing(Domains.Gold).TotalPoints);
        }

        [Test]
        public void RecordResults_AccumulatesAcrossGames_AndTracksHistory()
        {
            // Game 1: Jade 1st, Ruby 2nd, Gold 3rd
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) });
            // Game 2: Ruby 1st, Gold 2nd, Jade 3rd
            _data.RecordResults(new[] { Row(1, Domains.Ruby), Row(2, Domains.Gold), Row(3, Domains.Jade) });

            Assert.AreEqual(2, Standing(Domains.Jade).TotalPoints, "Jade: 2 + 0");
            Assert.AreEqual(3, Standing(Domains.Ruby).TotalPoints, "Ruby: 1 + 2");
            Assert.AreEqual(1, Standing(Domains.Gold).TotalPoints, "Gold: 0 + 1");

            CollectionAssert.AreEqual(new[] { 1, 3 }, Standing(Domains.Jade).Placements);
            CollectionAssert.AreEqual(new[] { 2, 1 }, Standing(Domains.Ruby).Placements);
            CollectionAssert.AreEqual(new[] { 3, 2 }, Standing(Domains.Gold).Placements);
        }

        [Test]
        public void RecordResults_RecordsPerRoundHistory()
        {
            _data.RecordResults(
                new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) },
                playerSnapshots: null, modeDisplayName: "Skim Race", intensity: 2);
            _data.RecordResults(
                new[] { Row(1, Domains.Ruby), Row(2, Domains.Gold), Row(3, Domains.Jade) },
                playerSnapshots: null, modeDisplayName: "Joust", intensity: 3);

            Assert.AreEqual(2, _data.History.Count, "One history record per recorded game.");

            var r1 = _data.History[0];
            Assert.AreEqual(1, r1.RoundNumber);
            Assert.AreEqual("Skim Race", r1.ModeDisplayName);
            Assert.AreEqual(2, r1.Intensity);
            CollectionAssert.AreEqual(new[] { Domains.Jade, Domains.Ruby, Domains.Gold }, r1.DomainOrder);
            Assert.AreEqual(Domains.Jade, r1.WinningDomain, "DomainOrder[0] is the round winner.");
            Assert.AreEqual(3, r1.Players.Count, "Snapshot rebuilt from results when none supplied.");
            Assert.AreEqual("P1", r1.Players[0].Name);
            Assert.AreEqual(1, r1.Players[0].Rank, "Snapshot preserves per-round rank.");

            var r2 = _data.History[1];
            Assert.AreEqual(2, r2.RoundNumber);
            Assert.AreEqual("Joust", r2.ModeDisplayName);
            Assert.AreEqual(Domains.Ruby, r2.WinningDomain);
        }

        [Test]
        public void ResetRuntime_ClearsHistory()
        {
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby) });
            Assert.AreEqual(1, _data.History.Count);

            _data.ResetRuntime();

            Assert.AreEqual(0, _data.History.Count, "History clears for a fresh shuffle.");
        }

        [Test]
        public void BuildSortedStandings_OrdersByPointsDescending()
        {
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) });
            _data.RecordResults(new[] { Row(1, Domains.Ruby), Row(2, Domains.Gold), Row(3, Domains.Jade) });

            var sorted = _data.BuildSortedStandings();

            Assert.AreEqual(Domains.Ruby, sorted[0].Domain, "Ruby leads with 3.");
            Assert.AreEqual(Domains.Jade, sorted[1].Domain, "Jade 2.");
            Assert.AreEqual(Domains.Gold, sorted[2].Domain, "Gold 1.");
        }

        [Test]
        public void BuildSortedStandings_TiebreakBestPlacement_ThenEnumOrder()
        {
            // Construct an all-tie-on-points finish (every domain = 2):
            //   G1: Jade 1st, Gold 2nd, Ruby 3rd
            //   G2: Ruby 1st, Gold 2nd, Jade 3rd
            // Totals: Jade 2 (best place 1), Ruby 2 (best place 1), Gold 2 (best place 2).
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Gold), Row(3, Domains.Ruby) });
            _data.RecordResults(new[] { Row(1, Domains.Ruby), Row(2, Domains.Gold), Row(3, Domains.Jade) });

            Assert.AreEqual(2, Standing(Domains.Jade).TotalPoints);
            Assert.AreEqual(2, Standing(Domains.Ruby).TotalPoints);
            Assert.AreEqual(2, Standing(Domains.Gold).TotalPoints);

            var sorted = _data.BuildSortedStandings();

            // Points tie → best placement: Jade(1) & Ruby(1) above Gold(2); Jade<Ruby by enum order.
            Assert.AreEqual(Domains.Jade, sorted[0].Domain);
            Assert.AreEqual(Domains.Ruby, sorted[1].Domain);
            Assert.AreEqual(Domains.Gold, sorted[2].Domain, "Gold last — never placed better than 2nd.");
        }

        [Test]
        public void RecordResults_NullOrEmpty_IsNoOp()
        {
            Assert.DoesNotThrow(() => _data.RecordResults(null));
            Assert.DoesNotThrow(() => _data.RecordResults(new ScoreResult[0]));
            Assert.AreEqual(0, _data.Standings.Count, "No standings created from null/empty results.");
            Assert.AreEqual(0, _data.GamesPlayed, "Null/empty results don't count as a game played.");
        }

        [Test]
        public void RecordResults_IncrementsGamesPlayed()
        {
            Assert.AreEqual(0, _data.GamesPlayed);
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby) });
            Assert.AreEqual(1, _data.GamesPlayed);
            _data.RecordResults(new[] { Row(1, Domains.Ruby), Row(2, Domains.Jade) });
            Assert.AreEqual(2, _data.GamesPlayed);
        }

        [Test]
        public void IsShuffleComplete_FalseUntilADomainReachesWinTarget()
        {
            // Default WinTarget = 6. Jade wins each game (+2): 2, 4, 6.
            var game = new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) };

            _data.RecordResults(game);
            Assert.IsFalse(_data.IsShuffleComplete, "Jade has 2 (< 6).");
            _data.RecordResults(game);
            Assert.IsFalse(_data.IsShuffleComplete, "Jade has 4 (< 6).");
            _data.RecordResults(game);
            Assert.IsTrue(_data.IsShuffleComplete, "Jade reached 6 (race target).");
        }

        [Test]
        public void IsShuffleComplete_TrueAtGameCap_EvenWithNoWinner()
        {
            _data.WinTarget = 100;   // unreachable within the cap
            _data.MaxGames = 2;

            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) });
            Assert.IsFalse(_data.IsShuffleComplete, "1 game played, cap is 2.");

            _data.RecordResults(new[] { Row(1, Domains.Ruby), Row(2, Domains.Gold), Row(3, Domains.Jade) });
            Assert.IsTrue(_data.IsShuffleComplete, "Hit the 2-game cap, so the shuffle ends.");
        }

        [Test]
        public void ResetRuntime_ClearsStandingsAndGamesPlayed_KeepsIntensityCeiling()
        {
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) });
            _data.IsActive = true;
            _data.CurrentGameIndex = 2;
            _data.IntensityCeiling = 4;

            _data.ResetRuntime();

            Assert.AreEqual(0, _data.Standings.Count);
            Assert.AreEqual(0, _data.CurrentGameIndex);
            Assert.AreEqual(0, _data.GamesPlayed, "GamesPlayed resets for a fresh shuffle.");
            Assert.IsFalse(_data.IsActive);
            Assert.AreEqual(4, _data.IntensityCeiling,
                "IntensityCeiling persists — Play Again routes through ResetRuntime and must keep it.");
        }

        [Test]
        public void PointsForPlace_MapsTableAndZeroesOutOfRange()
        {
            Assert.AreEqual(2, _data.PointsForPlace(1));
            Assert.AreEqual(1, _data.PointsForPlace(2));
            Assert.AreEqual(0, _data.PointsForPlace(3));
            Assert.AreEqual(0, _data.PointsForPlace(4), "Beyond the table scores 0.");
            Assert.AreEqual(0, _data.PointsForPlace(0), "Non-positive place scores 0.");
        }

        [Test]
        public void CrystalsForDomain_ReturnsThisGamePlacementReward()
        {
            var results = new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) };

            Assert.AreEqual(2, _data.CrystalsForDomain(results, Domains.Jade), "1st-place domain earns 2.");
            Assert.AreEqual(1, _data.CrystalsForDomain(results, Domains.Ruby), "2nd-place domain earns 1.");
            Assert.AreEqual(0, _data.CrystalsForDomain(results, Domains.Gold), "3rd-place domain earns 0.");
        }

        [Test]
        public void CrystalsForDomain_UsesBestPlayerRank()
        {
            // Jade holds the worst AND the best player; its best (rank 1) makes it 1st → 2.
            var results = new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Jade) };

            Assert.AreEqual(2, _data.CrystalsForDomain(results, Domains.Jade));
            Assert.AreEqual(1, _data.CrystalsForDomain(results, Domains.Ruby));
        }

        [Test]
        public void CrystalsForDomain_DomainAbsentOrNullResults_ReturnsZero()
        {
            var results = new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby) };

            Assert.AreEqual(0, _data.CrystalsForDomain(results, Domains.Gold), "Gold didn't play → 0.");
            Assert.AreEqual(0, _data.CrystalsForDomain(null, Domains.Jade), "Null results → 0.");
        }
    }
}
#endif
