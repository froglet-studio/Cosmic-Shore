using System.Linq;
using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.Game.Analytics;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Unified Wildlife Blitz controller — handles both solo (1 player) and co-op multiplayer
    /// (up to 3 players, same team). Follows the HexRace/Joust pattern:
    ///   1 player selected  → IsMultiplayerMode = false → ServerPlayerVesselInitializer runs
    ///                         locally with no AI opponents (solo blitz)
    ///   2-3 players selected → online co-op, all Jade domain
    ///
    /// Win condition : all lifeforms in the cell destroyed.
    /// Score         : elapsed time (golf rules — lower is better). 999 on timeout.
    /// </summary>
    public class WildlifeBlitzController : MultiplayerDomainGamesController
    {
        [Header("Wildlife Blitz")]
        [SerializeField] private AllLifeFormsDestroyedTurnMonitor lifeFormMonitor;
        [SerializeField] private TimeBasedTurnMonitor timeMonitor;

        private bool _resultsSent;
        private float _turnStartTime;

        // Authoritative results — set by server, read by EndGameController & Scoreboard
        public bool DidWin { get; private set; }
        public float FinishTime { get; private set; }
        public bool ResultsReady { get; private set; }

        protected override bool UseGolfRules => true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _resultsSent = false;
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            _turnStartTime = Time.time;
            OnCountdownTimerEnded_ClientRpc();
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc()
        {
            _turnStartTime = Time.time;
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        #region Turn End / Result Calculation

        /// <summary>
        /// Called when the turn ends (all lifeforms killed OR timer expired).
        /// Server calculates results and syncs to all clients.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer) return;
            if (_resultsSent) return;

            CalculateResults_Server();
            SyncResults_Authoritative();
            _resultsSent = true;
        }

        void CalculateResults_Server()
        {
            float elapsed = Time.time - _turnStartTime;
            bool allKilled = lifeFormMonitor != null && lifeFormMonitor.CheckForEndOfTurn();

            DidWin = allKilled;
            FinishTime = elapsed;

            // All players share the same team score
            float teamScore = allKilled ? elapsed : 999f;

            foreach (var stats in gameData.RoundStatsList)
                stats.Score = teamScore;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            // Report stats to UGS analytics
            if (UGSStatsManager.Instance)
            {
                var localName = gameData.LocalPlayer?.Name;
                var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
                if (localStats != null)
                {
                    int kills = localStats.BlocksDestroyed;
                    int crystals = localStats.ElementalCrystalsCollected;
                    int finalScore = (int)localStats.Score;

                    UGSStatsManager.Instance.ReportBlitzStats(
                        GameModes.WildlifeBlitz,
                        gameData.SelectedIntensity.Value,
                        crystals, kills, finalScore
                    );
                }
            }

            Debug.Log($"[WildlifeBlitz] Results: Win={allKilled} Time={elapsed:F2}s " +
                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");
        }

        void SyncResults_Authoritative()
        {
            var list = gameData.RoundStatsList;
            int count = list.Count;

            var names = new FixedString64Bytes[count];
            var scores = new float[count];
            var domains = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(list[i].Name);
                scores[i] = list[i].Score;
                domains[i] = (int)list[i].Domain;
            }

            SyncResults_ClientRpc(names, scores, domains, DidWin, FinishTime);
        }

        [ClientRpc]
        void SyncResults_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            bool didWin,
            float finishTime)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    Debug.LogWarning($"[WildlifeBlitz] Client could not match RoundStats for '{sName}'.");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
            }

            DidWin = didWin;
            FinishTime = finishTime;
            ResultsReady = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            Debug.Log($"[WildlifeBlitz] Client synced. Win={DidWin} Time={FinishTime:F2}s " +
                      $"Order=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        #endregion

        #region Per-Player Kill Tracking

        /// <summary>
        /// Reports a lifeform kill for per-player tracking.
        /// </summary>
        public void ReportLifeFormKill(string playerName)
        {
            ReportLifeFormKill_ServerRpc(playerName);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportLifeFormKill_ServerRpc(string playerName)
        {
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats != null)
                stats.BlocksDestroyed++;

            NotifyKillCount_ClientRpc(playerName, stats?.BlocksDestroyed ?? 0);
        }

        [ClientRpc]
        void NotifyKillCount_ClientRpc(string playerName, int killCount)
        {
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats)) return;
            stats.BlocksDestroyed = killCount;
        }

        #endregion

        #region Reset

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _resultsSent = false;
            DidWin = false;
            FinishTime = 0f;
            ResultsReady = false;
            _turnStartTime = 0f;

            if (lifeFormMonitor) lifeFormMonitor.ResetMonitor();
            if (timeMonitor) timeMonitor.ResetMonitor();

            foreach (var s in gameData.RoundStatsList)
            {
                s.Score = 0f;
                s.BlocksDestroyed = 0;
            }

            gameData.InvokeTurnStarted();
        }

        #endregion
    }
}
