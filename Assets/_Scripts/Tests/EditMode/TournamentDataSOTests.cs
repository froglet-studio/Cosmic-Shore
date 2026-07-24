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
    /// TournamentDataSO Tests - validates the per-DOMAIN {2,1,0} placement scoring fold that
    /// powers the Tournament/Shuffle meta-mode.
    ///
    /// WHY THIS MATTERS:
    /// Standings are reduced LOCALLY on every peer from the already-synced GameDataSO.Results
    /// (no extra networking), so the fold MUST be deterministic and identical everywhere:
    ///   • domain placement = the mode rule's TEAM-total order when supplied
    ///     (ScoringRuleSO.ResolvePlacementOrder, passed by TournamentController); the results-only
    ///     fallback is best (lowest) player Rank per domain, ties → enum order Jade→Ruby→Gold,
    ///   • placement crystals {2,1,0} awarded by place - the LAST-placed domain of a round always
    ///     earns the table's last entry (0), whatever the domain count, so losing never pays,
    ///   • accumulated across games; final sort: points desc, best single placement, enum order.
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
            // production means they are never null there - here we supply test doubles).
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
            // FALLBACK path (no explicit placement supplied): Jade has the worst AND the best
            // player; its best (rank 1) decides its place → 1st.
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
        public void RecordResults_ExplicitPlacementOrder_OverridesRankDerivedOrder()
        {
            // The 2v2 "Scurry" regression: results are ranked per-PLAYER, and a losing-team player
            // ties the top individual score (rank 1 lands on Jade), but the TEAM totals say Ruby won
            // (12+8 vs 12+5). The mode rule's team-total order must beat the rank-derived order.
            var results = new[]
            {
                Row(1, Domains.Jade),   // 12 crystals (tie, listed first)
                Row(2, Domains.Ruby),   // 12 crystals
                Row(3, Domains.Ruby),   // 8 crystals
                Row(4, Domains.Jade),   // 5 crystals
            };

            _data.RecordResults(results, domainPlacementOrder: new[] { Domains.Ruby, Domains.Jade });

            Assert.AreEqual(2, Standing(Domains.Ruby).TotalPoints, "Ruby (20 total) wins the round.");
            Assert.AreEqual(0, Standing(Domains.Jade).TotalPoints, "Jade (17 total) is last → 0.");
            Assert.AreEqual(Domains.Ruby, _data.History[0].WinningDomain,
                "The round card's winner is the team-total winner, not the top individual's team.");
        }

        [Test]
        public void RecordResults_ExplicitPlacementOrder_SanitizedAgainstResults()
        {
            // A domain the order names but nobody fielded (Gold) is dropped; a domain the order
            // missed but that played (Ruby) is appended - no team is ever dropped from standings.
            var results = new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby) };

            _data.RecordResults(results, domainPlacementOrder: new[] { Domains.Gold, Domains.Jade });

            Assert.IsNull(Standing(Domains.Gold), "Gold fielded nobody → no standings row.");
            Assert.AreEqual(2, Standing(Domains.Jade).TotalPoints, "Jade keeps 1st from the order.");
            Assert.AreEqual(0, Standing(Domains.Ruby).TotalPoints, "Ruby appended last → 0.");
            CollectionAssert.AreEqual(new[] { Domains.Jade, Domains.Ruby }, _data.History[0].DomainOrder);
        }

        [Test]
        public void RecordResults_TwoDomains_LoserEarnsNothing()
        {
            // "Win a game = 2 points, lose = nothing": with only two domains the loser is LAST and
            // must earn the table's last entry (0), not the 2nd-place 1 - otherwise a team could
            // race to the win target on losses alone.
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby) });

            Assert.AreEqual(2, Standing(Domains.Jade).TotalPoints, "Winner earns 2.");
            Assert.AreEqual(0, Standing(Domains.Ruby).TotalPoints, "Loser (last place) earns 0.");

            // Three straight wins reach the default race target (6); the loser stays at 0.
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby) });
            _data.RecordResults(new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby) });

            Assert.AreEqual(6, Standing(Domains.Jade).TotalPoints);
            Assert.AreEqual(0, Standing(Domains.Ruby).TotalPoints);
            Assert.IsTrue(_data.IsShuffleComplete, "Race to 6 = exactly three dominant finishes.");
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
            Assert.AreEqual(Domains.Gold, sorted[2].Domain, "Gold last - never placed better than 2nd.");
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
        public void ResolveWinTarget_OverridesSerializedWinTarget_ForShuffleComplete()
        {
            // The End Game Conditions tool is the authority: TournamentController stamps the resolved
            // race-to-N target via ResolveWinTarget. A tool value of 3 must end the shuffle at 3 even
            // though the serialized fallback WinTarget is still 6.
            _data.ResolveWinTarget(3);
            Assert.AreEqual(3, _data.EffectiveWinTarget, "Resolved tool value wins over the serialized fallback.");

            var game = new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Gold) };

            _data.RecordResults(game);                                   // Jade 2
            Assert.IsFalse(_data.IsShuffleComplete, "Jade has 2 (< 3).");
            _data.RecordResults(game);                                   // Jade 4
            Assert.IsTrue(_data.IsShuffleComplete, "Jade reached 4 (>= the resolved target 3).");
        }

        [Test]
        public void EffectiveWinTarget_FallsBackToSerializedWinTarget_WhenUnresolved()
        {
            // Pure data SO (no TournamentController run) → no tool value resolved → use the field.
            Assert.AreEqual(_data.WinTarget, _data.EffectiveWinTarget,
                "Until ResolveWinTarget runs, EffectiveWinTarget mirrors the serialized fallback.");

            _data.ResolveWinTarget(0);   // non-positive clears the override
            Assert.AreEqual(_data.WinTarget, _data.EffectiveWinTarget,
                "A non-positive resolve clears the override (back to the serialized fallback).");
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
                "IntensityCeiling persists - Play Again routes through ResetRuntime and must keep it.");
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
        public void PointsForPlacement_LastPlaceAlwaysEarnsLastTableEntry()
        {
            // 3 domains: identical to the raw table {2,1,0}.
            Assert.AreEqual(2, _data.PointsForPlacement(1, 3));
            Assert.AreEqual(1, _data.PointsForPlacement(2, 3));
            Assert.AreEqual(0, _data.PointsForPlacement(3, 3));

            // 2 domains: the loser is LAST → the table's last entry (0), not the 2nd-place 1.
            Assert.AreEqual(2, _data.PointsForPlacement(1, 2));
            Assert.AreEqual(0, _data.PointsForPlacement(2, 2), "2-domain loser earns nothing.");

            // Degenerate single-domain round keeps 1st-place points (guarded, never last-mapped).
            Assert.AreEqual(2, _data.PointsForPlacement(1, 1));
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
            // FALLBACK path: Jade holds the worst AND the best player; its best (rank 1) makes it
            // 1st → 2. Ruby is LAST of the two placed domains → 0 (last place never pays).
            var results = new[] { Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Jade) };

            Assert.AreEqual(2, _data.CrystalsForDomain(results, Domains.Jade));
            Assert.AreEqual(0, _data.CrystalsForDomain(results, Domains.Ruby), "2-domain loser earns 0.");
        }

        [Test]
        public void CrystalsForDomain_WithExplicitPlacement_MatchesTheFold()
        {
            // Same Scurry-regression shape as the RecordResults test: the explicit team-total order
            // must drive the Scoreboard's reward badge/wallet exactly like the standings fold.
            var results = new[]
            {
                Row(1, Domains.Jade), Row(2, Domains.Ruby), Row(3, Domains.Ruby), Row(4, Domains.Jade),
            };
            var placement = new[] { Domains.Ruby, Domains.Jade };

            Assert.AreEqual(2, _data.CrystalsForDomain(results, Domains.Ruby, placement), "Team-total winner earns 2.");
            Assert.AreEqual(0, _data.CrystalsForDomain(results, Domains.Jade, placement), "Team-total loser earns 0.");
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
