using System.Linq;
using Obvious.Soap;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerJoustController : MultiplayerDomainGamesController
    {
        [Header("Events")]
        [SerializeField] private ScriptableEventString onJoustCollision;

        [Header("Turn Monitor")]
        [SerializeField] public JoustCollisionTurnMonitor joustTurnMonitor;

        private bool _gameEnded;
        private float _gameStartTime;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            this.numberOfRounds = 1;
            this.numberOfTurnsPerRound = 1;
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
            _gameStartTime = Time.time;
            InitializeJoustGame_ClientRpc();
        }

        [ClientRpc]
        void InitializeJoustGame_ClientRpc()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn();
            if (joustTurnMonitor) joustTurnMonitor.StartMonitor();
        }

        // --- Live Sync: Critical for Client Victory Check ---
        public void NotifyCollision(string playerName, int currentCollisions)
        {
            if (!IsServer) return;
            UpdateCollisionCount_ClientRpc(playerName, currentCollisions);
        }

        [ClientRpc]
        void UpdateCollisionCount_ClientRpc(string playerName, int count)
        {
            var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stat != null) stat.JoustCollisions = count;
        }

        public void OnTurnEndedByMonitor(string winnerName)
        {
            if (!IsServer) return;
            HandlePlayerWon(winnerName);
        }

        void HandlePlayerWon(string winnerName)
        {
            if (_gameEnded) return;
            _gameEnded = true;

            float elapsedTime = Time.time - _gameStartTime;
            CalculateFinalScores(winnerName, elapsedTime);
        }

        void CalculateFinalScores(string winnerName, float winnerTime)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;
            int needed = joustTurnMonitor.CollisionsNeeded;
    
            FixedString64Bytes[] nameArray = new FixedString64Bytes[count];
            float[] scoreArray = new float[count];
            int[] collisionArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);

                if (statsList[i].Name == winnerName)
                {
                    // Winner: Score is accurate Time, Collisions = needed (to guarantee didWin = true)
                    scoreArray[i] = winnerTime;
                    collisionArray[i] = needed; // Force winner to show as winner
                }
                else
                {
                    // Loser: Score is high to force bottom sorting
                    scoreArray[i] = 99999f;
                    collisionArray[i] = statsList[i].JoustCollisions; // Actual collision count
                }
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, collisionArray);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] collisions)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat != null)
                {
                    stat.Score = scores[i];
                    stat.JoustCollisions = collisions[i];
                    Debug.Log($"[SyncFinalScores] {sName}: Score={scores[i]}, Collisions={collisions[i]}");
                }
            }

            // Simple Sort: Lowest Score (Time) wins. Losers (99999) go to bottom.
            gameData.RoundStatsList.Sort((a, b) => a.Score.CompareTo(b.Score));

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _gameEnded = false;
            _gameStartTime = 0f;
        }

        protected override bool UseGolfRules => true;
    }
}