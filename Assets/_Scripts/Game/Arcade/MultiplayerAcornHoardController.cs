// =======================================================
// MultiplayerAcornHoardController.cs
// Acorn Hoard: Players race to steal the most prism volume.
// - Environment is a dense arena of neutral prism clusters
// - Win condition: first player to steal enough volume, OR
//   highest volume stolen when time expires
// - Leverages the Squirrel's steal, drift, and boost mechanics
// =======================================================

using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerAcornHoardController : MultiplayerDomainGamesController
    {
        [Header("Arena")]
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] int baseNumberOfSegments = 20;
        [SerializeField] int baseStraightLineLength = 200;
        [SerializeField] bool scaleSegmentsWithIntensity = true;

        [Header("Seed")]
        [SerializeField] int seed = 0;

        [Header("Acorn Hoard Rules")]
        [SerializeField] NetworkVolumeStolenTurnMonitor volumeMonitor;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        private bool _gameEnded;

        protected override bool UseGolfRules => true; // lower time = better

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            int currentSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
            InitializeArena_ClientRpc(currentSeed);

            base.OnCountdownTimerEnded();
        }

        [ClientRpc]
        void InitializeArena_ClientRpc(int syncedSeed)
        {
            if (!segmentSpawner) return;

            segmentSpawner.Seed = syncedSeed;
            segmentSpawner.NumberOfSegments = scaleSegmentsWithIntensity
                ? baseNumberOfSegments * Intensity
                : baseNumberOfSegments;
            segmentSpawner.StraightLineLength = baseStraightLineLength;

            segmentSpawner.Initialize();
        }

        /// <summary>
        /// Called when a player reaches the volume threshold first.
        /// Server scores all players: winner gets finish time, losers get penalty + remaining volume.
        /// </summary>
        public void ReportLocalPlayerFinished(float finishTimeSeconds)
        {
            string myName = gameData.LocalPlayer.Vessel.VesselStatus.PlayerName;
            ReportPlayerFinished_ServerRpc(finishTimeSeconds, myName);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportPlayerFinished_ServerRpc(float finishTimeSeconds, string playerName)
        {
            if (_gameEnded) return;
            _gameEnded = true;

            float threshold = volumeMonitor != null ? volumeMonitor.VolumeThreshold : 5000f;

            // Winner: finish time as score
            var winnerStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (winnerStats != null)
                winnerStats.Score = finishTimeSeconds;

            // Losers: penalty + remaining volume to steal
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Name == playerName) continue;

                float stolen = stats.VolumeStolen;
                float remaining = Mathf.Max(0, threshold - stolen);
                stats.Score = 10000f + remaining;
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            SyncFinalScoresSnapshot();
        }

        /// <summary>
        /// Called by NetworkVolumeStolenTurnMonitor to update server-side tracking.
        /// </summary>
        public void NotifyVolumeStolen(string playerName, float volumeStolen)
        {
            if (!IsServer) return;

            var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stat != null)
                stat.VolumeStolen = volumeStolen;
        }

        void SyncFinalScoresSnapshot()
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            FixedString64Bytes[] nameArray = new FixedString64Bytes[count];
            float[] scoreArray = new float[count];
            int[] domainArray = new int[count];
            float[] volumeArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].VolumeStolen;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumeStolen)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null) continue;

                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.VolumeStolen = volumeStolen[i];
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();

            _gameEnded = false;

            foreach (var s in gameData.RoundStatsList)
            {
                s.Score = 0f;
                s.VolumeStolen = 0f;
                s.PrismStolen = 0;
            }

            RaiseToggleReadyButtonEvent(true);
        }
    }
}
