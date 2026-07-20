using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Blitz on the networked single-host model: co-op clear-the-cell, playable
    /// by a party of one (host) or up to three (AI teammates backfill via
    /// ServerPlayerVesselInitializerWithAI). The TEAM's summed blitz composite (fed
    /// server-side by WildlifeBlitzScoreKeeper) races the cell's CellEndGameScore
    /// (published by WildlifeBlitzObjectiveTurnMonitor); the loss clock is the scene's
    /// NetworkTimeBasedTurnMonitor. End-game runs the base SyncFinalResults template:
    /// objective met → the co-op domain wins with the shared clear time; clock expired →
    /// a Blue no-winner DNF end.
    /// </summary>
    public class MultiplayerWildlifeBlitzController : MultiplayerDomainGamesController
    {
        [Header("Blitz")]
        [Tooltip("The server-authoritative blitz composite keeper (kills/crystals/volume → Score).")]
        [SerializeField] WildlifeBlitzScoreKeeper scoreTracker;

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true; // lifeforms/crystals don't reset in place

        // End-game runs through OnTurnEndedCustom → the base SyncFinalResults template;
        // suppress the base turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;

            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
            base.OnNetworkDespawn();
        }

        void HandleTurnStarted()
        {
            if (scoreTracker) scoreTracker.StartTracking();
        }

        void HandleTurnEnded()
        {
            if (scoreTracker) scoreTracker.StopTracking();
        }

        /// <summary>
        /// Server-side end detection. The turn ends either because the objective monitor
        /// saw the team sum reach the target (WIN - the whole team shares the clear time)
        /// or because the time monitor expired (LOSS - a Blue no-winner DNF). Score
        /// assignment, roster snapshot, and the canonical results tail are owned by the
        /// base SyncFinalResults template.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || FinalResultsSent) return;

            bool cleared = rule.IsObjectiveReached(gameData, out var winningDomain);
            if (cleared)
                SyncFinalResults(winningDomain, Time.time - gameData.TurnStartTime);
            else
                SyncFinalResults(Domains.Blue, 0f);
        }
    }
}
