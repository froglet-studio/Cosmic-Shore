// MultiplayerJoustController.cs
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class MultiplayerJoustController : MultiplayerDomainGamesController
    {
        private bool _finalResultsSent;
        private Domains _winningDomain = Domains.Blue;

        protected override bool UseGolfRules => true;

        // Joust handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // SyncJoustResults_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeMiniGameEnd from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
        }

        // ── Server-authoritative game end ─────────────────────────────────

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;

            CalculateJoustScores_Server();
            _finalResultsSent = true;
            SyncJoustResults_Authoritative();
        }

        /// <summary>
        /// Domain-aggregated winner: the active domain with the highest summed
        /// JoustCollisions wins. The turn monitor already guarantees the turn
        /// only ends when a domain's sum reaches the joust target. Winner and
        /// all teammates (same Domain) get elapsed time as score; other teams
        /// get 99999f (golf: lower = better).
        /// </summary>
        void CalculateJoustScores_Server()
        {
            float currentTime = Time.time - gameData.TurnStartTime;

            Domains winningDomain = Domains.Blue;
            int bestDomainSum = -1;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                int sum = gameData.SumJoustCollisionsByDomain(d);
                if (sum > bestDomainSum)
                {
                    bestDomainSum = sum;
                    winningDomain = d;
                }
            }

            // Representative winner-name = best individual contributor on the winning
            // domain. Used for the WinnerName legacy field (display strings only —
            // VICTORY/DEFEAT attribution is via WinnerDomain).
            var winnerRep = winningDomain == Domains.Blue
                ? null
                : gameData.RoundStatsList
                    .Where(s => s.Domain == winningDomain)
                    .OrderByDescending(s => s.JoustCollisions)
                    .FirstOrDefault();
            string winnerName = winnerRep?.Name ?? "";

            CSDebug.Log($"[JoustController] Calculating scores. Winner='{winnerName}' Domain={winningDomain} " +
                      $"DomainSum={bestDomainSum} Time={currentTime:F2}s " +
                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}({s.Domain}):{s.JoustCollisions}j"))}]");

            foreach (var stats in gameData.RoundStatsList)
            {
                if (winningDomain != Domains.Blue && stats.Domain == winningDomain)
                    stats.Score = currentTime;
                else
                    stats.Score = 99999f;
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _winningDomain = winningDomain;
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the game just ended.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ───────────────────────────────────────────────────

        void SyncJoustResults_Authoritative()
        {
            // Winner is index 0 after ascending sort (golf rules)
            string winnerName = gameData.RoundStatsList.Count > 0
                ? gameData.RoundStatsList[0].Name
                : "";

            var list = gameData.RoundStatsList;
            int count = list.Count;

            var names      = new FixedString64Bytes[count];
            var scores     = new float[count];
            var collisions = new int[count];
            var domains    = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i]      = new FixedString64Bytes(list[i].Name);
                scores[i]     = list[i].Score;
                collisions[i] = list[i].JoustCollisions;
                domains[i]    = (int)list[i].Domain;
            }

            SyncJoustResults_ClientRpc(names, scores, collisions, domains,
                new FixedString64Bytes(winnerName), (int)_winningDomain);
        }

        [ClientRpc]
        void SyncJoustResults_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] collisions,
            int[] domains,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == n);
                if (stat == null)
                {
                    CSDebug.LogError($"[JoustController] Client could not match '{n}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score           = scores[i];
                stat.JoustCollisions = collisions[i];
                stat.Domain          = (Domains)domains[i];
            }

            // Authoritative winner — written to gameData, consumed by EndGameControllers
            // OnWinnerCalculated (below) is the "results ready" signal.
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            CSDebug.Log($"[JoustController] Client synced. Winner='{gameData.WinnerName}' Domain={gameData.WinnerDomain} " +
                      $"Order=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}({s.Domain}):{s.Score:F1}"))}]");

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _winningDomain = Domains.Blue;

            foreach (var s in gameData.RoundStatsList)
            {
                s.JoustCollisions = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
