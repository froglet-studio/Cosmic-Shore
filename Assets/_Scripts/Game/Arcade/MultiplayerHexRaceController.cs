// =======================================================
// MultiplayerHexRaceController.cs  (FINAL / FIXED)
// - Ends race on first finisher (does NOT wait for all players to report)
// - Computes losers as "10000 + crystalsLeft"
// - Syncs snapshot (scores + crystals + domains) to ALL clients
// - Triggers WinnerCalculated + MiniGameEnd only after snapshot is applied
// - Receives authoritative CrystalsCollected from the server-side monitor sync
// =======================================================

using System.Linq;
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

        [Header("Race Rules")]
        [Tooltip("Optional override. If 0, uses networked target from monitor.")]
        [SerializeField] int crystalsToFinishOverride = 0;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        private bool _raceEnded;

        // Networked so server+clients always agree on target.
        private readonly NetworkVariable<int> _netCrystalsToFinish = new NetworkVariable<int>(0);

        protected override bool UseGolfRules => true; // lower is better

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
            InitializeEnvironment_ClientRpc(currentSeed);

            base.OnCountdownTimerEnded();
        }

        [ClientRpc]
        void InitializeEnvironment_ClientRpc(int syncedSeed)
        {
            if (!segmentSpawner) return;

            segmentSpawner.Seed = syncedSeed;
            segmentSpawner.NumberOfSegments = scaleNumberOfSegmentsWithIntensity ? baseNumberOfSegments * Intensity : baseNumberOfSegments;
            segmentSpawner.StraightLineLength = scaleLengthWithIntensity ? baseStraightLineLength / Intensity : baseStraightLineLength;

            ApplyHelixIntensity();
            segmentSpawner.Initialize();
        }

        void ApplyHelixIntensity()
        {
            if (!helix) return;
            var radius = Intensity / helixIntensityScaling;
            helix.firstOrderRadius = radius;
            helix.secondOrderRadius = radius;
        }

        // Called by local finish logic when you "complete" the race
        public void ReportLocalPlayerFinished(float finishTimeSeconds)
        {
            string myName = gameData.LocalPlayer.Vessel.VesselStatus.PlayerName;
            ReportPlayerFinished_ServerRpc(finishTimeSeconds, myName);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportPlayerFinished_ServerRpc(float finishTimeSeconds, string playerName)
        {
            if (_raceEnded) return;
            _raceEnded = true;

            // 1) Winner time
            var winnerStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (winnerStats != null)
                winnerStats.Score = finishTimeSeconds;

            // 2) Losers: 10000 + crystalsLeft
            int crystalsToFinish = ResolveCrystalsToFinishTarget();

            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Name == playerName) continue;

                int collected = stats.CrystalsCollected; // server must know this (monitor sync)
                int crystalsLeft = Mathf.Max(0, crystalsToFinish - collected);

                stats.Score = 10000f + crystalsLeft;
            }

            // 3) Sort + domain stats
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            // 4) Sync snapshot + trigger endgame
            SyncFinalScoresSnapshot();
        }

        int ResolveCrystalsToFinishTarget()
        {
            if (_netCrystalsToFinish.Value > 0) return _netCrystalsToFinish.Value;
            if (crystalsToFinishOverride > 0) return crystalsToFinishOverride;

            // Last-resort fallback. Prefer using the monitor to set _netCrystalsToFinish.
            return 39;
        }

        // Called by server-side monitor to set authoritative target
        public void SetCrystalsToFinishServer(int value)
        {
            if (!IsServer) return;
            _netCrystalsToFinish.Value = Mathf.Max(1, value);
        }

        // Called by server-side monitor on stat change (authoritative)
        public void NotifyCrystalsCollected(string playerName, int crystalsCollected)
        {
            if (!IsServer) return;

            var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stat != null)
                stat.CrystalsCollected = crystalsCollected;
        }

        void SyncFinalScoresSnapshot()
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            FixedString64Bytes[] nameArray = new FixedString64Bytes[count];
            float[] scoreArray = new float[count];
            int[] domainArray = new int[count];
            int[] crystalsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                crystalsArray[i] = statsList[i].CrystalsCollected;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, crystalsArray);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] domains, int[] crystalsCollected)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null) continue;

                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CrystalsCollected = crystalsCollected[i];
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();

            _raceEnded = false;

            foreach (var s in gameData.RoundStatsList)
            {
                s.Score = 0f;
                s.CrystalsCollected = 0;
            }

            if (IsServer)
                _netCrystalsToFinish.Value = 0;

            RaiseToggleReadyButtonEvent(true);
        }
    }
}
