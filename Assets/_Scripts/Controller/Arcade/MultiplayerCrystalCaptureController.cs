using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class MultiplayerCrystalCaptureController : MultiplayerDomainGamesController
    {
        private bool _finalResultsSent;

        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;

        // Crystal Capture handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection, mirroring HexRace/Joust pattern.
        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound,
        /// so _finalResultsSent is set in time to suppress the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;

            var winner = DetermineWinner();
            if (winner == null) return;

            // Per-player Score still tracks individual contribution (for the
            // scoreboard's secondary stat); domain aggregation is what determines
            // the winner and is computed below in CalculateDomainStats.
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.CrystalsCollected;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            SyncFinalScoresSnapshot(winner.Name, winner.Domain);
        }

        /// <summary>
        /// Winning team = active domain with the highest aggregate CrystalsCollected.
        /// Returns the best individual contributor on that team as the representative
        /// "winner name" used for legacy display fields. Victory/defeat attribution
        /// in end-game screens uses WinnerDomain, not WinnerName.
        /// </summary>
        IRoundStats DetermineWinner()
        {
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0)
                return null;

            Domains winningDomain = Domains.Blue;
            int bestDomainSum = -1;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                int sum = gameData.SumCrystalsCollectedByDomain(d);
                if (sum > bestDomainSum)
                {
                    bestDomainSum = sum;
                    winningDomain = d;
                }
            }

            if (winningDomain == Domains.Blue) return null;

            return gameData.RoundStatsList
                .Where(s => s.Domain == winningDomain)
                .OrderByDescending(s => s.CrystalsCollected)
                .FirstOrDefault();
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the game just ended.
        /// HasEndGame=false causes ExecuteServerRoundEnd to call SetupNewRound instead of
        /// ExecuteServerGameEnd — this override prevents the Ready button from appearing.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ───────────────────────────────────────────────────

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var crystalsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                crystalsArray[i] = statsList[i].CrystalsCollected;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, crystalsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] crystalsCollected,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[CrystalCapture] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CrystalsCollected = crystalsCollected[i];
            }

            // Authoritative winner — written to gameData, consumed by EndGameControllers
            // OnWinnerCalculated (below) is the "results ready" signal.
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(BuildResults());
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        /// <summary>
        /// Builds the single ranked results list for this game from the synced per-player
        /// stats (R10). CrystalCapture isn't golf — Score IS the crystal count — so ScoreText
        /// matches MultiplayerCrystalCaptureScoreboard.FormatPlayerScore ("N Crystals") and
        /// there is no secondary line. Runs on host + every client, so all peers agree.
        /// </summary>
        List<ScoreResult> BuildResults()
        {
            var rows = gameData.RoundStatsList.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score} Crystals",
                null
            )).ToList();
            return ScoreResultBuilder.Build(rows, UseGolfRules);
        }

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;

            foreach (var s in gameData.RoundStatsList)
            {
                s.CrystalsCollected = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
