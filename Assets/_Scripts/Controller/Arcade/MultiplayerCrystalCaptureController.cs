using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class MultiplayerCrystalCaptureController : MultiplayerDomainGamesController
    {
        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;

        // Crystal Capture handles end-game through OnTurnEndedCustom (server-side winner
        // detection) → the base SyncFinalResults template, which broadcasts the canonical
        // results tail. Suppress the base controller's turn→round→game flow so we don't get
        // a duplicate InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        // Mid-turn centerline feed (replaces the legacy NetworkScoreTracker's
        // CrystalsCollectedScoring): server-only per-stats subscription writing the rule
        // metric into Score - a spawned peer's local write does not raise OnScoreChanged;
        // only server writes replicate via n_Score to every HUD. B15: detach from THIS
        // record list only (never by iterating gameData.RoundStatsList at teardown), with
        // OnNetworkDespawn + OnDestroy nets (precedent: NetworkCrystalCollisionTurnMonitor).
        readonly List<IRoundStats> _scoreFeedStats = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;

            if (IsServer)
                gameData.OnMiniGameTurnStarted.OnRaised += StartScoreFeed;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                gameData.OnMiniGameTurnStarted.OnRaised -= StartScoreFeed;
            StopScoreFeed();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            StopScoreFeed(); // B15: destruction paths that bypass despawn must still detach
            base.OnDestroy();
        }

        /// <summary>
        /// Turn-start roster snapshot suffices: the server roster is complete before any
        /// turn starts, and the Contains guard makes re-subscription a no-op.
        /// </summary>
        void StartScoreFeed()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
                stats.OnCrystalsCollectedChanged += FeedScore;
                _scoreFeedStats.Add(stats);
            }
        }

        void StopScoreFeed()
        {
            foreach (var stats in _scoreFeedStats)
            {
                if (stats == null) continue;
                stats.OnCrystalsCollectedChanged -= FeedScore;
            }
            _scoreFeedStats.Clear();
        }

        void FeedScore(IRoundStats stats)
        {
            if (FinalResultsSent) return;
            stats.Score = rule.LiveMetric(stats);
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
        /// ExecuteServerTurnEnd → SetupNewRound, so FinalResultsSent latches in time to
        /// suppress the Ready button. Winning domain (highest crystal sum, Jade→Ruby→Gold
        /// tie-break) delegated to the rule; score assignment, roster snapshot, and the
        /// canonical results tail are owned by the base SyncFinalResults template.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || FinalResultsSent) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            SyncFinalResults(winningDomain, 0f);
        }

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();

            foreach (var s in gameData.RoundStatsList)
            {
                s.CrystalsCollected = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
