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

        protected override bool UseGolfRules => true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
        }

        /// <summary>
        /// Called by monitor when server detects a collision directly.
        /// </summary>
        public void NotifyCollision(string playerName, int collisionCount)
        {
            if (!IsServer) return;
            
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats != null) stats.JoustCollisions = collisionCount;

            // Sync the updated count down to all clients
            NotifyCollision_ClientRpc(playerName, collisionCount);
        }

        /// <summary>
        /// Called by client when it detects a high-speed collision the server missed.
        /// ServerRpc lets the client report upward so server stays authoritative.
        /// </summary>
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
                Debug.LogError($"[JoustController] ServerRpc: could not find stats for '{playerName}'");
                return;
            }

            // Take the higher count — client may have detected more than server at high speed
            if (collisionCount <= stats.JoustCollisions) return;

            stats.JoustCollisions = collisionCount;
            Debug.Log($"[JoustController] Server accepted client collision report: " +
                      $"{playerName} now has {collisionCount} jousts");

            // Sync authoritative count to all clients
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

            foreach (var stats in gameData.RoundStatsList)
            {
                int left = Mathf.Max(0, collisionsNeeded - stats.JoustCollisions);
                if (left == 0)
                {
                    if (stats.Score > 0f && stats.Score < 99999f) continue;
                    stats.Score = currentTime;
                }
                else
                {
                    stats.Score = 99999f;
                }
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
        }

        void SyncJoustResults_Authoritative()
        {
            var list = gameData.RoundStatsList;
            int count = list.Count;

            var names = new FixedString64Bytes[count];
            var scores = new float[count];
            var collisions = new int[count];
            var domains = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(list[i].Name);
                scores[i] = list[i].Score;
                collisions[i] = list[i].JoustCollisions;
                domains[i] = (int)list[i].Domain;
            }

            SyncJoustResults_ClientRpc(names, scores, collisions, domains);
        }

        [ClientRpc]
        void SyncJoustResults_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] collisions,
            int[] domains)
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
                stat.Score = scores[i];
                stat.JoustCollisions = collisions[i];
                stat.Domain = (Domains)domains[i];
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;

            if (joustTurnMonitor)
                joustTurnMonitor.ResetMonitor();

            foreach (var s in gameData.RoundStatsList)
            {
                s.JoustCollisions = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}