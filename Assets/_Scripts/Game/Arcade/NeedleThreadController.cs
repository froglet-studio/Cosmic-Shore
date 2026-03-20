using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class NeedleThreadController : MultiplayerDomainGamesController
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
        [Tooltip("Volume of hostile prisms that must be destroyed to win. If 0, uses networked value.")]
        [SerializeField] float volumeToFinishOverride = 0f;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        private bool _raceEnded;
        private bool _trackSpawned;
        private readonly NetworkVariable<int> _netTrackSeed = new(0);
        private readonly NetworkVariable<float> _netVolumeToFinish = new(0f);

        public string WinnerName { get; private set; } = "";
        public bool RaceResultsReady { get; private set; } = false;

        protected override bool UseGolfRules => true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;

            _netTrackSeed.OnValueChanged += OnTrackSeedChanged;

            if (IsServer)
            {
                SpawnTrackEarly().Forget();
            }
            else if (_netTrackSeed.Value != 0)
            {
                SpawnTrackLocally(_netTrackSeed.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _netTrackSeed.OnValueChanged -= OnTrackSeedChanged;
            base.OnNetworkDespawn();
        }

        private void OnTrackSeedChanged(int previousValue, int newValue)
        {
            if (newValue != 0)
                SpawnTrackLocally(newValue);
        }

        private async UniTaskVoid SpawnTrackEarly()
        {
            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime);
            if (!IsServer || _trackSpawned) return;

            int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
            _netTrackSeed.Value = generatedSeed;
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            if (_netTrackSeed.Value == 0)
            {
                int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
                _netTrackSeed.Value = generatedSeed;
            }

            base.OnCountdownTimerEnded();
        }

        private void SpawnTrackLocally(int trackSeed)
        {
            if (_trackSpawned || !segmentSpawner) return;
            segmentSpawner.Seed = trackSeed;
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
                CSDebug.LogError($"[NeedleThread] Could not find RoundStats for winner '{playerName}'. " +
                               $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                return;
            }

            winnerStats.Score = finishTimeSeconds;

            float volumeTarget = ResolveVolumeToFinishTarget();
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Name == playerName) continue;
                int volumeLeft = Mathf.Max(0, Mathf.CeilToInt(volumeTarget - stats.HostileVolumeDestroyed));
                stats.Score = 10000f + volumeLeft;
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            SyncFinalScoresSnapshot(playerName);
        }

        float ResolveVolumeToFinishTarget()
        {
            if (_netVolumeToFinish.Value > 0f) return _netVolumeToFinish.Value;
            if (volumeToFinishOverride > 0f) return volumeToFinishOverride;
            return 500f;
        }

        public void SetVolumeToFinishServer(float value)
        {
            if (!IsServer) return;
            _netVolumeToFinish.Value = Mathf.Max(1f, value);
        }

        public void NotifyVolumeDestroyed(string playerName, float volume)
        {
            if (!IsServer) return;
            var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stat != null)
                stat.HostileVolumeDestroyed = volume;
        }

        void SyncFinalScoresSnapshot(string winnerName)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var volumeArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].HostileVolumeDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray,
                new FixedString64Bytes(winnerName));
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumeDestroyed,
            FixedString64Bytes winnerName)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[NeedleThread] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.HostileVolumeDestroyed = volumeDestroyed[i];
            }

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
                s.HostileVolumeDestroyed = 0f;
            }

            if (IsServer)
            {
                _netVolumeToFinish.Value = 0f;
                _netTrackSeed.Value = 0;

                SpawnTrackEarly().Forget();
            }

            RaiseToggleReadyButtonEvent(true);
        }
    }
}
