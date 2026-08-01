// MultiplayerJoustController.cs
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class MultiplayerJoustController : MultiplayerDomainGamesController
    {
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true; // match HexRace / CrystalCapture

        // Joust handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // the base SyncFinalResults template, which broadcasts the canonical results tail.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeMiniGameEnd from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        // Mid-turn centerline feed (replaces the legacy NetworkScoreTracker's
        // TimePlayedScoring): a spawned peer's LOCAL Score write does not raise
        // OnScoreChanged - only a SERVER write replicates via n_Score and reaches every
        // peer's HUD - so the elapsed-time tick must be a server-side write (the same
        // expression the winner's finishTime uses in OnTurnEndedCustom).
        CancellationTokenSource _scoreFeedCts;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;

            if (IsServer)
            {
                _scoreFeedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    this.GetCancellationTokenOnDestroy());
                RunScoreFeedAsync(_scoreFeedCts.Token).Forget();
            }
        }

        public override void OnNetworkDespawn()
        {
            _scoreFeedCts?.Cancel();
            _scoreFeedCts?.Dispose();
            _scoreFeedCts = null;
            base.OnNetworkDespawn();
        }

        async UniTaskVoid RunScoreFeedAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (gameData.IsTurnRunning && !FinalResultsSent)
                {
                    float elapsed = Time.time - gameData.TurnStartTime;
                    // Re-read the live roster each tick - no cached stats refs (B15).
                    var statsList = gameData.RoundStatsList;
                    for (int i = 0; i < statsList.Count; i++)
                    {
                        var stats = statsList[i];
                        if (stats != null)
                            stats.Score = elapsed;
                    }
                }

                await UniTask.Delay(250, DelayType.UnscaledDeltaTime, cancellationToken: token)
                    .SuppressCancellationThrow();
            }
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Domain-aggregated winner: the active domain with the highest summed
        /// JoustCollisions wins. The turn monitor already guarantees the turn
        /// only ends when a domain's sum reaches the joust target. Winner and
        /// all teammates (same Domain) get elapsed time as score; other teams
        /// get the golf loser sentinel (the rule owns this). Score assignment,
        /// roster snapshot, and the canonical results tail are owned by the base
        /// SyncFinalResults template.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || FinalResultsSent) return;

            float currentTime = Time.time - gameData.TurnStartTime;
            Domains winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            CSDebug.Log($"[JoustController] Objective reached. Domain={winningDomain} Time={currentTime:F2}s " +
                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}({s.Domain}):{s.JoustCollisions}j"))}]");

            SyncFinalResults(winningDomain, currentTime);
        }

        // ── Replay ───────────────────────────────────────────────────────
        // OnResetForReplayCustom removed - Joust uses UseSceneReloadForReplay = true, which
        // performs a full network scene reload. FinalResultsSent and all per-player stats
        // are re-initialized fresh via OnNetworkSpawn + a rebuilt RoundStatsList.
    }
}
