using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
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
        [Tooltip("Shared crystal target resolved by the turn monitor. " +
                 "Read by this controller for loser score calculation.")]
        [SerializeField] IntVariable crystalTargetVariable;

        [Header("SOAP Race Results")]
        [Tooltip("Server-authoritative winner name. Written by SyncFinalScores_ClientRpc, " +
                 "read by EndGameController.")]
        [SerializeField] StringVariable raceWinnerName;
        [Tooltip("True once final scores have been synced to all clients.")]
        [SerializeField] BoolVariable raceResultsReady;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        private bool _raceEnded;
        private bool _trackSpawned;
        private CancellationTokenSource _seedPollCts;
        private readonly NetworkVariable<int> _netTrackSeed = new(0);

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // HexRace handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            Debug.Log($"<color=#00CED1>[FLOW-7HR] [HexRaceController] OnNetworkSpawn — IsServer={IsServer}, Intensity={Intensity}</color>");
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;

            // Clear SOAP race results (fresh state for scene reload replays)
            if (raceWinnerName) raceWinnerName.Value = "";
            if (raceResultsReady) raceResultsReady.Value = false;

            // HexRaceController owns the track lifecycle (seed generation, spawning, replay reset).
            // Prevent SegmentSpawner from auto-resetting on OnResetForReplay.
            if (segmentSpawner) segmentSpawner.ExternalResetControl = true;

            // Listen for seed changes so late-joining clients can spawn the track
            _netTrackSeed.OnValueChanged += OnTrackSeedChanged;

            if (IsServer)
            {
                Debug.Log("<color=#00CED1>[FLOW-7HR] [HexRaceController] Server: SpawnTrackEarly() starting...</color>");
                // Server generates the seed after a short delay for intensity sync
                SpawnTrackEarly().Forget();
            }
            else if (_netTrackSeed.Value != 0)
            {
                Debug.Log($"<color=#00CED1>[FLOW-7HR] [HexRaceController] Client: track seed already set ({_netTrackSeed.Value}), spawning track locally</color>");
                // Client joined after the server already set the seed — spawn immediately
                SpawnTrackLocally(_netTrackSeed.Value);
            }
            else
            {
                // Seed not yet available — start polling fallback.
                // Covers the race condition where OnValueChanged doesn't fire for
                // initial sync and the ClientRpc was sent before this client spawned.
                Debug.Log("<color=#00CED1>[FLOW-7HR] [HexRaceController] Client: seed not yet available, starting poll fallback</color>");
                StartSeedPoll();
            }
        }

        public override void OnNetworkDespawn()
        {
            CancelSeedPoll();
            _netTrackSeed.OnValueChanged -= OnTrackSeedChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Called on all clients when the server writes a new seed to the NetworkVariable.
        /// </summary>
        private void OnTrackSeedChanged(int previousValue, int newValue)
        {
            if (newValue != 0)
                SpawnTrackLocally(newValue);
        }

        void StartSeedPoll()
        {
            CancelSeedPoll();
            _seedPollCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            WaitForTrackSeed(_seedPollCts.Token).Forget();
        }

        void CancelSeedPoll()
        {
            _seedPollCts?.Cancel();
            _seedPollCts?.Dispose();
            _seedPollCts = null;
        }

        /// <summary>
        /// Client-side fallback: polls _netTrackSeed until it becomes non-zero.
        /// Covers the race condition where OnValueChanged doesn't fire for
        /// initial sync and the ClientRpc was sent before this client spawned.
        /// </summary>
        private async UniTaskVoid WaitForTrackSeed(CancellationToken ct)
        {
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    await UniTask.Delay(100, DelayType.UnscaledDeltaTime, cancellationToken: ct);

                    if (_trackSpawned)
                        return;

                    if (_netTrackSeed.Value != 0)
                    {
                        Debug.Log($"<color=#00CED1>[FLOW-7HR] [HexRaceController] Client poll: seed arrived ({_netTrackSeed.Value}), spawning track</color>");
                        SpawnTrackLocally(_netTrackSeed.Value);
                        return;
                    }
                }

                Debug.LogWarning("[HexRaceController] Client poll: timed out after 5s waiting for track seed.");
            }
            catch (System.OperationCanceledException)
            {
                // Network despawn or object destroyed — expected
            }
        }

        /// <summary>
        /// Generates and stores the track seed shortly after network spawn,
        /// so the track is visible before players click ready.
        /// </summary>
        private async UniTaskVoid SpawnTrackEarly()
        {
            // Small delay to ensure all clients have joined and intensity is synced
            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime);
            if (!IsServer || _trackSpawned) return;

            int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
            _netTrackSeed.Value = generatedSeed;
            SpawnTrack_ClientRpc(generatedSeed);
        }

        [ClientRpc]
        private void SpawnTrack_ClientRpc(int trackSeed)
        {
            SpawnTrackLocally(trackSeed);
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            // Ensure track seed is set for any edge case where early spawn was missed
            if (_netTrackSeed.Value == 0)
            {
                int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
                _netTrackSeed.Value = generatedSeed;
            }

            SpawnTrack_ClientRpc(_netTrackSeed.Value);
            base.OnCountdownTimerEnded();
        }

        /// <summary>
        /// Spawns the track locally using the given seed. Guards against double-spawning.
        /// </summary>
        private void SpawnTrackLocally(int trackSeed)
        {
            if (_trackSpawned || !segmentSpawner)
            {
                Debug.Log($"<color=#00CED1>[FLOW-7HR] [HexRaceController] SpawnTrackLocally SKIPPED — _trackSpawned={_trackSpawned}, segmentSpawner={segmentSpawner != null}</color>");
                return;
            }
            Debug.Log($"<color=#00CED1>[FLOW-7HR] [HexRaceController] SpawnTrackLocally — seed={trackSeed}, Intensity={Intensity}</color>");
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

        // ── Server-authoritative race end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection, mirroring MultiplayerJoustController.OnTurnEndedCustom().
        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound,
        /// so _raceEnded is set in time to suppress the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _raceEnded) return;

            int target = ResolveCrystalsToFinishTarget();
            var winner = gameData.RoundStatsList.FirstOrDefault(s => s.CrystalsCollected >= target);
            if (winner == null) return;

            _raceEnded = true;

            // All players share the same elapsed time since turn start.
            // The score tracker updates LocalRoundStats.Score every frame with elapsed time.
            float finishTime = gameData.LocalRoundStats?.Score ?? 0f;
            winner.Score = finishTime;

            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Name == winner.Name) continue;
                int crystalsLeft = Mathf.Max(0, target - stats.CrystalsCollected);
                stats.Score = 10000f + crystalsLeft;
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            SyncFinalScoresSnapshot(winner.Name);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the race just ended.
        /// HasEndGame=false causes ExecuteServerRoundEnd to call SetupNewRound instead of
        /// ExecuteServerGameEnd — this override prevents the Ready button from appearing.
        /// After replay reset, _raceEnded is cleared so new rounds work normally.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_raceEnded) return;
            base.SetupNewRound();
        }

        int ResolveCrystalsToFinishTarget()
        {
            if (crystalTargetVariable != null && crystalTargetVariable.Value > 0)
                return crystalTargetVariable.Value;
            return 39;
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

            // Authoritative winner — written to SOAP variables, consumed by EndGameController
            if (raceWinnerName) raceWinnerName.Value = winnerName.ToString();
            if (raceResultsReady) raceResultsReady.Value = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // OnResetForReplayCustom removed — HexRace uses UseSceneReloadForReplay = true,
        // which performs a full scene reload. All race state, track, and environment objects
        // are destroyed with the scene and re-initialized fresh via OnNetworkSpawn.
    }
}