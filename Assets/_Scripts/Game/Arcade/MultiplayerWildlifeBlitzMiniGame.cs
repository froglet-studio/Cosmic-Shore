using System.Linq;
using CosmicShore.Game.Arcade.Scoring;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Multiplayer co-op Wildlife Blitz controller (up to 3 players, same team).
    /// All players cooperate to kill all lifeforms. Win condition: all lifeforms dead.
    /// Score = elapsed time (golf rules — lower is better).
    /// </summary>
    public class MultiplayerWildlifeBlitzMiniGame : MultiplayerDomainGamesController
    {
        [Header("Wildlife Blitz")]
        [SerializeField] private AllLifeFormsDestroyedTurnMonitor lifeFormMonitor;
        [SerializeField] private TimeBasedTurnMonitor timeMonitor;

        private bool _resultsSent;
        private float _turnStartTime;

        // Single source of truth — set by server, read by EndGameController
        public bool DidCoOpWin { get; private set; }
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

        /// <summary>
        /// Called when the turn ends (all lifeforms killed or timer expired).
        /// Server calculates co-op results and syncs to all clients.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer) return;
            if (_resultsSent) return;

            CalculateCoOpResults_Server();
            SyncCoOpResults_Authoritative();
            _resultsSent = true;
        }

        void CalculateCoOpResults_Server()
        {
            float elapsed = Time.time - _turnStartTime;
            bool allKilled = lifeFormMonitor != null && lifeFormMonitor.CheckForEndOfTurn();

            DidCoOpWin = allKilled;
            FinishTime = elapsed;

            // In co-op, all players share the same score (team score)
            float teamScore = allKilled ? elapsed : 999f;

            foreach (var stats in gameData.RoundStatsList)
            {
                stats.Score = teamScore;
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            Debug.Log($"[WildlifeBlitzMP] Co-op results: Win={allKilled} Time={elapsed:F2}s " +
                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");
        }

        void SyncCoOpResults_Authoritative()
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

            SyncCoOpResults_ClientRpc(names, scores, domains, DidCoOpWin, FinishTime);
        }

        [ClientRpc]
        void SyncCoOpResults_ClientRpc(
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
                    Debug.LogError($"[WildlifeBlitzMP] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
            }

            // Authoritative results — EndGameController reads these
            DidCoOpWin = didWin;
            FinishTime = finishTime;
            ResultsReady = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            Debug.Log($"[WildlifeBlitzMP] Client synced. Win={DidCoOpWin} Time={FinishTime:F2}s " +
                      $"Order=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        /// <summary>
        /// Reports a lifeform kill from a specific player to the server.
        /// Used for per-player kill tracking in the co-op scoreboard.
        /// </summary>
        public void ReportLifeFormKill(string playerName)
        {
            ReportLifeFormKill_ServerRpc(playerName);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportLifeFormKill_ServerRpc(string playerName)
        {
            // Track individual kills in BlocksDestroyed (reused stat field for co-op kill count)
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

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _resultsSent = false;
            DidCoOpWin = false;
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
    }
}
