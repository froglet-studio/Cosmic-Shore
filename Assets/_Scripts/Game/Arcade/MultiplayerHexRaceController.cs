using System.Linq;
using CosmicShore.Game.IO;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerHexRaceController : MultiplayerDomainGamesController
    {
        [Header("Course")]
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] int baseNumberOfSegments = 10;
        [SerializeField] int baseStraightLineLength = 400;
        [SerializeField] bool scaleNumberOfSegmentsWithIntensity = true;
        [SerializeField] bool scaleLengthWithIntensity = true;

        [Header("Helix")]
        [SerializeField] SpawnableHelix helix;
        [SerializeField] float helixIntensityScaling = 1.3f;

        [Header("Seed")]
        [SerializeField] int seed = 0;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);
        private bool _raceEnded;
        private int _scoresReceived;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            this.numberOfRounds = 1;
            this.numberOfTurnsPerRound = 1; 
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
            int currentSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
            InitializeEnvironment_ClientRpc(currentSeed);
        }

        [ClientRpc]
        void InitializeEnvironment_ClientRpc(int syncedSeed)
        {
            if (segmentSpawner)
            {
                segmentSpawner.Seed = syncedSeed;
                segmentSpawner.NumberOfSegments = scaleNumberOfSegmentsWithIntensity ? baseNumberOfSegments * Intensity : baseNumberOfSegments;
                segmentSpawner.StraightLineLength = scaleLengthWithIntensity ? baseStraightLineLength / Intensity : baseStraightLineLength;
                ApplyHelixIntensity();
                segmentSpawner.Initialize();
            }
            
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        void ApplyHelixIntensity()
        {
            if (!helix) return;
            var radius = Intensity / helixIntensityScaling;
            helix.firstOrderRadius = radius;
            helix.secondOrderRadius = radius;
        }

        #region Score & End Game Logic

        public void ReportLocalPlayerFinished(float finalScore)
        {
            string myName = gameData.LocalPlayer.Vessel.VesselStatus.PlayerName;
            ReportPlayerScore_ServerRpc(finalScore, myName);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportPlayerScore_ServerRpc(float score, string playerName)
        {
            var playerStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (playerStats != null)
            {
                playerStats.Score = score;
            }

            if (!_raceEnded)
            {
                _raceEnded = true;
                NotifyRaceEnded_ClientRpc();
            }

            _scoresReceived++;
            if (_scoresReceived >= gameData.SelectedPlayerCount.Value)
            {
                EndRaceAndSyncScores();
            }
        }

        [ClientRpc]
        void NotifyRaceEnded_ClientRpc()
        {
            gameData.InvokeGameTurnConditionsMet();
        }

        void EndRaceAndSyncScores()
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;
                
            FixedString64Bytes[] nameArray = new FixedString64Bytes[count];
            float[] scoreArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
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
                {
                    stat.Score = scores[i];
                }
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _raceEnded = false;
            _scoresReceived = 0;
        }

        #endregion
    }
}