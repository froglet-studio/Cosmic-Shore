using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Multiplayer Joust Controller with proper replay reset handling.
    /// Manages joust-specific game logic including collision tracking and scoring.
    /// </summary>
    public class MultiplayerJoustController : MultiplayerDomainGamesController
    {
        [Header("Joust Specific")]
        [SerializeField] public JoustCollisionTurnMonitor joustTurnMonitor;
        [SerializeField] private Transform obstaclesContainer; // Container for obstacles/environment
        
        private bool turnEndedByMonitor = false;
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            
            // Subscribe to joust-specific events if needed
            if (joustTurnMonitor != null && IsServer)
            {
                // Subscribe to collision events or other joust-specific events here
            }
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from joust-specific events
            if (joustTurnMonitor != null && IsServer)
            {
                // Unsubscribe here
            }
            
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Called by NetworkJoustCollisionTurnMonitor when a collision occurs.
        /// Synchronizes collision count to all clients.
        /// </summary>
        public void NotifyCollision(string playerName, int collisionCount)
        {
            if (!IsServer)
                return;

            NotifyCollision_ClientRpc(playerName, collisionCount);
        }

        [ClientRpc]
        void NotifyCollision_ClientRpc(string playerName, int collisionCount)
        {
            if (IsServer)
                return;

            // Update the client's local round stats
            if (gameData.TryGetRoundStats(playerName, out IRoundStats stats))
            {
                stats.JoustCollisions = collisionCount;
                Debug.Log($"[Client] Updated {playerName} collision count to {collisionCount}");
            }
        }

        /// <summary>
        /// Called by NetworkJoustCollisionTurnMonitor when turn ends.
        /// Records the winner name for scoring.
        /// </summary>
        public void OnTurnEndedByMonitor(string winnerName)
        {
            if (!IsServer)
                return;

            turnEndedByMonitor = true;
            
            Debug.Log($"[Joust] Turn ended by monitor. Winner: {winnerName ?? "None"}");
            
            // Sync to clients
            OnTurnEndedByMonitor_ClientRpc(winnerName);
        }

        [ClientRpc]
        void OnTurnEndedByMonitor_ClientRpc(string winnerName)
        {
            if (IsServer)
                return;

            Debug.Log($"[Client] Turn ended. Winner: {winnerName ?? "None"}");
        }

        protected override bool UseGolfRules => true; // Lowest time wins in joust

        /// <summary>
        /// Called when turn ends - calculate final scores for joust.
        /// Winners get their time, losers get a penalty score.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            
            if (!IsServer)
                return;
            
            CalculateJoustScores();
        }

        /// <summary>
        /// Calculate final scores for all players.
        /// Winners: Score = completion time
        /// Losers: Score = 99999 (high penalty to sort them to bottom)
        /// </summary>
        void CalculateJoustScores()
        {
            if (joustTurnMonitor == null)
            {
                Debug.LogError("[MultiplayerJoustController] JoustTurnMonitor is null!");
                return;
            }

            int collisionsNeeded = joustTurnMonitor.CollisionsNeeded;
            float currentTime = Time.time - gameData.TurnStartTime;

            foreach (var stats in gameData.RoundStatsList)
            {
                int currentCollisions = stats.JoustCollisions;
                int collisionsLeft = Mathf.Max(0, collisionsNeeded - currentCollisions);

                if (collisionsLeft == 0)
                {
                    // Winner: Use their completion time as score
                    if (stats.Score == 0f || stats.Score >= 99999f)
                    {
                        stats.Score = currentTime;
                        Debug.Log($"[Joust] {stats.Name} WON with time: {stats.Score:F2}s");
                    }
                }
                else
                {
                    // Loser: Penalty score so they sort to bottom
                    stats.Score = 99999f;
                    Debug.Log($"[Joust] {stats.Name} LOST with {collisionsLeft} jousts remaining");
                }
            }

            // Sort with golf rules (lowest score wins)
            gameData.SortRoundStats(UseGolfRules);
            
            // Calculate domain stats for team-based scoring if needed
            gameData.CalculateDomainStats(UseGolfRules);
        }

        /// <summary>
        /// Called on all clients when "Play Again" is pressed.
        /// Reset all game-specific environment elements here.
        /// </summary>
        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            
            // Reset turn ended flag
            turnEndedByMonitor = false;
            
            // Reset joust-specific elements
            if (joustTurnMonitor != null)
            {
                joustTurnMonitor.ResetMonitor();
            }
            
            // Reset obstacles if needed
            ResetObstacles();
            
            // Reset any UI elements specific to joust
            ResetJoustUI();
            
            // Refresh HUD to show reset collision counts
            RefreshHUD();
            
            Debug.Log("[MultiplayerJoustController] Environment reset for replay");
        }

        void RefreshHUD()
        {
            // Trigger HUD refresh by raising the turn started event
            // This will cause the MultiplayerJoustHUD to update all player cards
            gameData.InvokeTurnStarted();
        }

        void ResetObstacles()
        {
            // Reset obstacle positions/states
            if (obstaclesContainer != null)
            {
                foreach (Transform obstacle in obstaclesContainer)
                {
                    // Reset obstacle to initial state
                    obstacle.gameObject.SetActive(true);
                    
                    // If obstacles have specific reset methods, call them here
                    var obstacleComponent = obstacle.GetComponent<IResettable>();
                    obstacleComponent?.Reset();
                }
            }
        }

        void ResetJoustUI()
        {
            // Reset any joust-specific UI elements
            // The scoreboard will automatically hide via OnResetForReplay event
            // Reset collision counters, timers, progress bars, etc. if you have them
            
            // Example: If you have a UI controller reference
            // joustUIController?.ResetUI();
        }

        /// <summary>
        /// Override this if you need custom round end behavior for joust.
        /// </summary>
        protected override void OnRoundEndedCustom()
        {
            base.OnRoundEndedCustom();
            
            if (!IsServer)
                return;
            
            // Any joust-specific round end logic here
            Debug.Log("[MultiplayerJoustController] Round ended");
        }
    }

    /// <summary>
    /// Optional interface for resettable objects like obstacles.
    /// </summary>
    public interface IResettable
    {
        void Reset();
    }
}