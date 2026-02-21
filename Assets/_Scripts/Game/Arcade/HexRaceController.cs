using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class HexRaceController : MultiplayerDomainGamesController
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
        private bool _trackSpawned;
        private readonly NetworkVariable<int> _netCrystalsToFinish = new(0);

        // Single source of truth for who won — set authoritatively by server, read by end game controller
        public string WinnerName { get; private set; } = "";
        public bool RaceResultsReady { get; private set; } = false;

        protected override bool UseGolfRules => true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;

            // Spawn the track early so it's visible during connecting screen / cinematic
            if (IsServer)
                SpawnTrackEarly().Forget();
        }

        /// <summary>
        /// Generates and broadcasts the track seed shortly after network spawn,
        /// so the track is visible before players click ready.
        /// </summary>
        private async UniTaskVoid SpawnTrackEarly()
        {
            // Small delay to ensure all clients have joined and intensity is synced
            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime);
            if (!IsServer || _trackSpawned) return;

            int currentSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
            InitializeEnvironment_ClientRpc(currentSeed);
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
            // Track is already spawned early; just start the game
            base.OnCountdownTimerEnded();
        }

        [ClientRpc]
        void InitializeEnvironment_ClientRpc(int syncedSeed)
        {
            if (!segmentSpawner) return;
            segmentSpawner.Seed = syncedSeed;
            segmentSpawner.NumberOfSegments = scaleNumberOfSegmentsWithIntensity
                ? baseNumberOfSegments * Intensity
                : baseNumberOfSegments;
            segmentSpawner.StraightLineLength = scaleLengthWithIntensity
                ? baseStraightLineLength / Intensity
                : baseStraightLineLength;
            ApplyHelixIntensity();
            segmentSpawner.Initialize();
            _trackSpawned = true;
        }

        void ApplyHelixIntensity()
        {
            if (!helix) return;
            var radius = Intensity / helixIntensityScaling;
            helix.firstOrderRadius = radius;
            helix.secondOrderRadius = radius;
        }

        // Only called by the winner's ScoreTracker
        public void ReportLocalPlayerFinished(float finishTimeSeconds)
        {
            string myName = gameData.LocalPlayer.Name;
            ReportPlayerFinished_ServerRpc(finishTimeSeconds, myName);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportPlayerFinished_ServerRpc(float finishTimeSeconds, string playerName)
        {
            if (_raceEnded) return;
            _raceEnded = true;

            var winnerStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (winnerStats == null)
            {
                Debug.LogError($"[HexRace] Could not find RoundStats for winner '{playerName}'. " +
                               $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                return;
            }

            winnerStats.Score = finishTimeSeconds;

            int crystalsToFinish = ResolveCrystalsToFinishTarget();
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Name == playerName) continue;
                int crystalsLeft = Mathf.Max(0, crystalsToFinish - stats.CrystalsCollected);
                stats.Score = 10000f + crystalsLeft;
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            SyncFinalScoresSnapshot(playerName);
        }

        int ResolveCrystalsToFinishTarget()
        {
            if (_netCrystalsToFinish.Value > 0) return _netCrystalsToFinish.Value;
            if (crystalsToFinishOverride > 0) return crystalsToFinishOverride;
            return 39;
        }

        public void SetCrystalsToFinishServer(int value)
        {
            if (!IsServer) return;
            _netCrystalsToFinish.Value = Mathf.Max(1, value);
        }

        public void NotifyCrystalsCollected(string playerName, int crystalsCollected)
        {
            if (!IsServer) return;
            var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stat != null)
                stat.CrystalsCollected = crystalsCollected;
        }

        void SyncFinalScoresSnapshot(string winnerName)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var crystalsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                crystalsArray[i] = statsList[i].CrystalsCollected;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, crystalsArray,
                new FixedString64Bytes(winnerName));
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] crystalsCollected,
            FixedString64Bytes winnerName)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    Debug.LogError($"[HexRace] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CrystalsCollected = crystalsCollected[i];
            }

            // Authoritative winner — single source of truth consumed by EndGameController
            WinnerName = winnerName.ToString();
            RaceResultsReady = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _raceEnded = false;
            _trackSpawned = false;
            WinnerName = "";
            RaceResultsReady = false;

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