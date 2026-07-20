using System.Collections.Generic;
using Unity.Netcode;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Multiplayer Cellular Duel: two 120s rounds (NetworkTimeBasedTurnMonitor), vessel
    /// ownership swap between rounds, scored by cumulative volume activity via
    /// CellularDuelScoringRuleSO. End-game runs the base SyncFinalResults template at the
    /// final round's end.
    /// NOTE: this controller is also a leftover on the dead mode-32 co-op WildlifeBlitz
    /// scene (player-unreachable, in no game list - AUDIT 1.7, fate is decision D3). That
    /// scene wires no rule, so its end-game is inert under HasEndGame=false - acceptable
    /// for unreachable content; D3 decides prune-or-revive.
    /// </summary>
    public class MultiplayerCellularDuelController : MultiplayerDomainGamesController
    {
        // Duel ends through OnTurnEndedCustom → the base SyncFinalResults template at the
        // final round; suppress the base turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        // ── Mid-turn centerline feed (replaces the legacy NetworkScoreTracker's three
        // volume strategies, Y1.1). Server-only: RoundStats.Score suppresses the local
        // OnScoreChanged while spawned, so only a server write (→ n_Score replication)
        // reaches the MiniGameHUD centerline on every peer. B15
        // (Docs/ScoringSystem/BUGS.md): detach from THIS record only - never by iterating
        // gameData.RoundStatsList - and never gate cleanup on turn-end alone.
        readonly List<IRoundStats> _scoreFeedStats = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
            StopScoreFeed();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            StopScoreFeed(); // B15: destruction paths that bypass despawn must still detach
            base.OnDestroy();
        }

        /// <summary>
        /// Runs at every round's turn start - the Contains guard makes re-subscription a
        /// no-op. The rule-null case is the leftover co-op WildlifeBlitz scene, which keeps
        /// its legacy Score source untouched.
        /// </summary>
        void RefreshScoreFeed()
        {
            if (rule == null) return;
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

        // ── Server-authoritative game end (shared SyncFinalResults template) ─────

        /// <summary>
        /// Called at EVERY turn end (both rounds). Only the final round produces results:
        /// the round counters have not been incremented yet at this point in
        /// SyncTurnEnd_ClientRpc, hence the +1s. Winner = active domain with the highest
        /// summed volume activity (rule.ResolveWinner); score assignment, roster snapshot,
        /// and the canonical results tail are owned by the base SyncFinalResults template.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || FinalResultsSent) return;
            if (rule == null) return; // leftover co-op WildlifeBlitz scene (see class doc)

            bool finalTurnOfRound = gameData.TurnsTakenThisRound + 1 >= numberOfTurnsPerRound;
            bool finalRound = gameData.RoundsPlayed + 1 >= numberOfRounds;
            if (!finalTurnOfRound || !finalRound) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            SyncFinalResults(winningDomain, 0f);
        }

        protected override void SetupNewRound()
        {
            // Latch check BEFORE the swap: with HasEndGame=false the base flow routes
            // the final round's end back through here, and a post-game vessel swap must
            // not happen (the base re-checks the latch for its own body).
            if (FinalResultsSent) return;

            bool allowSwap = gameData.RoundsPlayed > 0;
            if (allowSwap)
            {
                if (IsServer)
                    ChangeOwnershipOfVessels();

                gameData.SwapVessels();
            }

            base.SetupNewRound();
        }

        protected override void OnResetForReplay()
        {
            if (IsServer)
                ChangeOwnershipOfVessels();
            gameData.SwapVessels();
            base.OnResetForReplay();
        }

        void ChangeOwnershipOfVessels()
        {
            var player0 = gameData.Players[0];
            var player1 = gameData.Players[1];

            // swap the vessel types from player.NetDefaultVesselType.Value
            if (!player0.Vessel.Transform.TryGetComponent(out NetworkObject no0))
            {
                CSDebug.LogError("No network object found in vessel. This should not happen!");
                return;
            }

            if (!player1.Vessel.Transform.TryGetComponent(out NetworkObject no1))
            {
                CSDebug.LogError("No network object found in vessel. This should not happen!");
                return;
            }

            var no0_OwnerClientId = no0.OwnerClientId;
            no0.ChangeOwnership(no1.OwnerClientId);
            no1.ChangeOwnership(no0_OwnerClientId);
        }
    }
}
