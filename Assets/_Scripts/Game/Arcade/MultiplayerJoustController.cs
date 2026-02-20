// MultiplayerJoustController.cs
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerJoustController : MultiplayerDomainGamesController
    {
        [Header("Joust Specific")]
        [SerializeField] public JoustCollisionTurnMonitor joustTurnMonitor;

        private bool _finalResultsSent;

        // Single source of truth — set by server, read by EndGameController
        public string WinnerName { get; private set; } = "";
        public bool ResultsReady { get; private set; } = false;

        protected override bool UseGolfRules => true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
        }

        public void NotifyCollision(string playerName, int collisionCount)
        {
            if (!IsServer) return;
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats != null) stats.JoustCollisions = collisionCount;
            NotifyCollision_ClientRpc(playerName, collisionCount);
        }

        public void ReportCollisionToServer(string playerName, int collisionCount)
        {
            ReportCollision_ServerRpc(playerName, collisionCount);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportCollision_ServerRpc(string playerName, int collisionCount)
        {
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats == null)
            {
                Debug.LogError($"[JoustController] ServerRpc: no stats for '{playerName}'");
                return;
            }
            if (collisionCount <= stats.JoustCollisions) return;
            stats.JoustCollisions = collisionCount;
            NotifyCollision_ClientRpc(playerName, collisionCount);
        }

        [ClientRpc]
        void NotifyCollision_ClientRpc(string playerName, int collisionCount)
        {
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats)) return;
            stats.JoustCollisions = collisionCount;
        }

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer) return;
            if (_finalResultsSent) return;

            CalculateJoustScores_Server();
            SyncJoustResults_Authoritative();
            _finalResultsSent = true;
        }

        void CalculateJoustScores_Server()
        {
            if (!joustTurnMonitor)
            {
                Debug.LogError("[MultiplayerJoustController] JoustTurnMonitor is null!");
                return;
            }

            int collisionsNeeded = joustTurnMonitor.CollisionsNeeded;
            float currentTime = Time.time - gameData.TurnStartTime;

            // Find the winner first — whoever completed all jousts
            string winnerName = "";
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.JoustCollisions >= collisionsNeeded)
                {
                    winnerName = stats.Name;
                    break;
                }
            }

            Debug.Log($"[JoustController] Calculating scores. Winner='{winnerName}' Time={currentTime:F2}s " +
                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.JoustCollisions}j"))}]");

            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Name == winnerName)
                    stats.Score = currentTime;
                else
                    stats.Score = 99999f;
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
        }

        void SyncJoustResults_Authoritative()
        {
            // Winner is index 0 after ascending sort
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
                new FixedString64Bytes(winnerName));
        }

        [ClientRpc]
        void SyncJoustResults_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] collisions,
            int[] domains,
            FixedString64Bytes winnerName)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == n);
                if (stat == null)
                {
                    Debug.LogError($"[JoustController] Client could not match '{n}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score         = scores[i];
                stat.JoustCollisions = collisions[i];
                stat.Domain        = (Domains)domains[i];
            }

            // Authoritative winner — EndGameController reads this, not RoundStatsList[0]
            WinnerName   = winnerName.ToString();
            ResultsReady = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            Debug.Log($"[JoustController] Client synced. Winner='{WinnerName}' " +
                      $"Order=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            WinnerName   = "";
            ResultsReady = false;

            if (joustTurnMonitor) joustTurnMonitor.ResetMonitor();

            foreach (var s in gameData.RoundStatsList)
            {
                s.JoustCollisions = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}