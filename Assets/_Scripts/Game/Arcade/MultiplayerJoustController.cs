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
        [SerializeField] private JoustCollisionTurnMonitor joustTurnMonitor;

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

            if (joustTurnMonitor)
            {
                joustTurnMonitor.StartMonitor();
            }
        }

        void OnDestroy()
        {
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

            FixedString64Bytes[] nameArray  = new FixedString64Bytes[count];
            float[]              scoreArray = new float[count];
            int collisionsNeeded = joustTurnMonitor ? joustTurnMonitor.CollisionsNeeded : 0;

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);

                if (statsList[i].Name == winnerName)
                {
                    scoreArray[i] = winnerTime;
                }
                else
                {
                    int joustsAchieved = statsList[i].JoustCollisions;
                    int joustsShortOfWinning = collisionsNeeded - joustsAchieved;
            
                    // Higher score = worse (more jousts short of winning)
                    scoreArray[i] = 10000f + joustsShortOfWinning;
                }
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat != null)
                    stat.Score = scores[i];
            }

            gameData.SortRoundStats(UseGolfRules);
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