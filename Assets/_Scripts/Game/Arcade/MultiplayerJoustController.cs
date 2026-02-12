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
        /// Called by monitor when a collision occurs (server authoritative).
        /// </summary>
        public void NotifyCollision(string playerName, int collisionCount)
        {
            if (!IsServer) return;

            // Update server truth immediately too (important!)
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats != null) stats.JoustCollisions = collisionCount;

            NotifyCollision_ClientRpc(playerName, collisionCount);
        }

        [ClientRpc]
        void NotifyCollision_ClientRpc(string playerName, int collisionCount)
        {
            // This runs on clients (and host client too, which is fine)
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats)) return;
            stats.JoustCollisions = collisionCount;
        }

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer) return;

            // Prevent double-send if something ends turn twice
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
                int current = stats.JoustCollisions;
                int left = Mathf.Max(0, collisionsNeeded - current);

                if (left == 0)
                {
                    // Winner time (only set once)
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

            FixedString64Bytes[] names = new FixedString64Bytes[count];
            float[] scores = new float[count];
            int[] collisions = new int[count];
            int[] domains = new int[count];

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
        void SyncJoustResults_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] collisions, int[] domains)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == n);
                if (stat == null) continue;

                stat.Score = scores[i];
                stat.JoustCollisions = collisions[i];
                stat.Domain = (Domains)domains[i];
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            // Only now trigger endgame pipeline everywhere
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

            // If you want HUD refresh:
            gameData.InvokeTurnStarted();
        }
    }
}
