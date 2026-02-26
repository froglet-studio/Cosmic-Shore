using System.Linq;
using CosmicShore.Utility.ClassExtensions;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

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
        private readonly NetworkVariable<int> _netTrackSeed = new(0);
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

            // Listen for seed changes so late-joining clients can spawn the track
            _netTrackSeed.OnValueChanged += OnTrackSeedChanged;

            // In party mode, skip autonomous track spawning.
            // PartyGameController will call PartyMode_Activate → SpawnTrackForParty
            if (IsPartyMode)
            {
                CSDebug.Log("[HexRace] OnNetworkSpawn — PARTY MODE, skipping autonomous track spawn.");
                return;
            }

            if (IsServer)
            {
                // Server generates the seed after a short delay for intensity sync
                SpawnTrackEarly().Forget();
            }
            else if (_netTrackSeed.Value != 0)
            {
                // Client joined after the server already set the seed — spawn immediately
                SpawnTrackLocally(_netTrackSeed.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _netTrackSeed.OnValueChanged -= OnTrackSeedChanged;
            base.OnNetworkDespawn();
        }

        // ==================== Party Mode API ====================

        public override void PartyMode_Activate()
        {
            base.PartyMode_Activate();

            // In party mode, spawn the track when the environment is activated
            if (this.IsServerSafe())
            {
                _raceEnded = false;
                _trackSpawned = false;
                SpawnTrackEarly().Forget();
            }
        }

        public override void PartyMode_Deactivate()
        {
            base.PartyMode_Deactivate();
            _raceEnded = false;
            _trackSpawned = false;
            WinnerName = "";
            RaceResultsReady = false;
        }

        // ==================== Track Generation ====================

        /// <summary>
        /// Called on all clients when the server writes a new seed to the NetworkVariable.
        /// </summary>
        private void OnTrackSeedChanged(int previousValue, int newValue)
        {
            if (newValue != 0)
                SpawnTrackLocally(newValue);
        }

        /// <summary>
        /// Generates and stores the track seed shortly after network spawn,
        /// so the track is visible before players click ready.
        /// </summary>
        private async UniTaskVoid SpawnTrackEarly()
        {
            // Small delay to ensure all clients have joined and intensity is synced
            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime);
            if (!this.IsServerSafe() || _trackSpawned) return;

            int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);

            // In party mode after SetActive toggling, IsSpawned may be unreliable
            // so the NetworkVariable write could fail. Spawn the track locally as fallback.
            if (IsSpawned)
            {
                _netTrackSeed.Value = generatedSeed;
            }
            else
            {
                SpawnTrackLocally(generatedSeed);
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!this.IsServerSafe()) return;

            // Ensure track seed is set for any edge case where early spawn was missed
            if (_netTrackSeed.Value == 0)
            {
                int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
                _netTrackSeed.Value = generatedSeed;
            }

            base.OnCountdownTimerEnded();
        }

        /// <summary>
        /// Spawns the track locally using the given seed. Guards against double-spawning.
        /// </summary>
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

        // ==================== Turn End / Scoring ====================

        /// <summary>
        /// In party mode, ReportPlayerFinished_ServerRpc is skipped (RPC table may be
        /// broken after SetActive toggling). This override sets proper scores for ALL
        /// players so the sort in ExecuteServerGameEnd produces the correct winner.
        /// Mirrors the logic in ReportPlayerFinished_ServerRpc: the first player to
        /// collect all crystals gets their race time; all others get a penalty score.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!this.IsServerSafe() || !IsPartyMode) return;
            if (_raceEnded) return;
            _raceEnded = true;

            int crystalsToFinish = ResolveCrystalsToFinishTarget();
            float currentTime = Time.time - gameData.TurnStartTime;

            // Find the winner — first player who collected all crystals
            string winnerName = "";
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.CrystalsCollected >= crystalsToFinish)
                {
                    winnerName = stats.Name;
                    break;
                }
            }

            CSDebug.Log($"[HexRace] OnTurnEndedCustom (party). Winner='{winnerName}' Time={currentTime:F2}s " +
                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.CrystalsCollected}c/{s.Score:F1}s"))}]");

            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Name == winnerName)
                {
                    // Winner keeps their race time (already set by HexRaceScoreTracker.Update)
                    // but cap it to currentTime in case Update hasn't run this frame
                    if (stats.Score <= 0f)
                        stats.Score = currentTime;
                }
                else
                {
                    int crystalsLeft = Mathf.Max(0, crystalsToFinish - stats.CrystalsCollected);
                    stats.Score = 10000f + crystalsLeft;
                }
            }
        }

        // ==================== Race Finish ====================

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
                CSDebug.LogError($"[HexRace] Could not find RoundStats for winner '{playerName}'. " +
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
            if (!this.IsServerSafe()) return;
            int clamped = Mathf.Max(1, value);
            if (IsSpawned)
                _netCrystalsToFinish.Value = clamped;
        }

        public void NotifyCrystalsCollected(string playerName, int crystalsCollected)
        {
            if (!this.IsServerSafe()) return;
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
                    CSDebug.LogError($"[HexRace] Client could not match RoundStats for '{sName}'. " +
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
            {
                _netCrystalsToFinish.Value = 0;
                _netTrackSeed.Value = 0;

                // Re-generate the track for the replay
                SpawnTrackEarly().Forget();
            }

            RaiseToggleReadyButtonEvent(true);
        }
    }
}