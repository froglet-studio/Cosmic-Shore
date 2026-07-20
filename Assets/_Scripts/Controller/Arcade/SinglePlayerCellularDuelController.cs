using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cellular Duel game mode (single-player scene).
    /// Features: 2-player duel with vessel swapping between rounds. Scored by
    /// CellularDuelScoringRuleSO (same asset as the multiplayer duel): each player's
    /// Score is their cumulative volume activity, fed locally from the volume stat
    /// events; EndGame runs the rule-driven results tail (the SP twin of the domain
    /// base's SyncFinalResults - no RPC, single machine).
    /// </summary>
    public class SinglePlayerCellularDuelController : SinglePlayerMiniGameControllerBase
    {
        [Header("Scoring")]
        [Tooltip("Drag CellularDuelScoringRule.asset - the per-mode scoring strategy (winner, scores, results). Shared with the multiplayer duel.")]
        [SerializeField] ScoringRuleSO rule;

        // ── Mid-turn centerline feed (replaces the legacy offline ScoreTracker's three
        // volume strategies, Y1.1). SP players are not network-spawned, so local Score
        // writes raise OnScoreChanged directly. B15 discipline still applies: detach from
        // THIS record only, never by iterating gameData.RoundStatsList, with an OnDestroy
        // safety net.
        readonly List<IRoundStats> _scoreFeedStats = new();

        protected override bool ShouldResetPlayersOnTurnEnd => true;

        protected override void Start()
        {
            gameData.ScoringRule = rule;
            gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
            base.Start();
        }

        protected override void OnDisable()
        {
            if (gameData != null)
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
            StopScoreFeed();
            base.OnDisable();
        }

        public override void OnDestroy()
        {
            StopScoreFeed(); // B15: destruction paths that bypass OnDisable ordering must still detach
            base.OnDestroy();
        }

        void RefreshScoreFeed()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
                stats.OnVolumeCreatedChanged += FeedScore;
                stats.OnHostileVolumeDestroyedChanged += FeedScore;
                stats.OnFriendlyVolumeDestroyedChanged += FeedScore;
                _scoreFeedStats.Add(stats);
            }
        }

        void StopScoreFeed()
        {
            foreach (var stats in _scoreFeedStats)
            {
                if (stats == null) continue;
                stats.OnVolumeCreatedChanged -= FeedScore;
                stats.OnHostileVolumeDestroyedChanged -= FeedScore;
                stats.OnFriendlyVolumeDestroyedChanged -= FeedScore;
            }
            _scoreFeedStats.Clear();
        }

        void FeedScore(IRoundStats stats) => stats.Score = rule.LiveMetric(stats);

        /// <summary>
        /// Rule-driven end-game: the SP twin of the domain base's SyncFinalResults tail.
        /// WinnerName/WinnerDomain are derived by SetResults from Results[0] (the
        /// team-major top row - the winning domain's best player).
        /// </summary>
        protected override void EndGame()
        {
            if (!ShowEndGameSequence) return;

            var winningDomain = rule.ResolveWinner(gameData);
            rule.AssignScores(gameData, winningDomain, 0f);
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnTurnEndedCustom()
        {
            gameData.SwapVessels();
            base.OnTurnEndedCustom();
        }

        protected override void SetupNewRound()
        {
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewRound();
        }

        protected override void OnResetForReplay()
        {
            gameData.SwapVessels();
            base.OnResetForReplay();
        }
    }
}
