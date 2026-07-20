// MultiplayerJoustController.cs
using System.Linq;
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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
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
