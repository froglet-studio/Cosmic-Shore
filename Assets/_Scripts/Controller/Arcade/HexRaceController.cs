using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

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

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        private bool _trackSpawned;
        private bool _arenaBuildAnnounced;
        private CancellationTokenSource _seedPollCts;
        private readonly NetworkVariable<int> _netTrackSeed = new(0);

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // HexRace handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // the base SyncFinalResults template, which broadcasts the canonical results tail.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-7HR] [HexRaceController] OnNetworkSpawn - IsServer={IsServer}, Intensity={Intensity}</color>");
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;

            // HexRaceController owns the track lifecycle (seed generation, spawning, replay reset).
            // Prevent SegmentSpawner from auto-resetting on OnResetForReplay.
            if (segmentSpawner) segmentSpawner.ExternalResetControl = true;

            // The track builds only after the netcode seed arrives — announce the pending build
            // so the connecting screen's arena-ready gate holds through the seed wait (an
            // absence-of-activity check would misread "nothing laying yet" as "arena done" and
            // release the player into a still-materializing track).
            if (segmentSpawner)
            {
                _arenaBuildAnnounced = true;
                PrismTrailBuilder.BeginArenaBuild();
            }

            // Listen for seed changes so late-joining clients can spawn the track
            _netTrackSeed.OnValueChanged += OnTrackSeedChanged;

            if (IsServer)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00CED1>[FLOW-7HR] [HexRaceController] Server: SpawnTrackEarly() starting...</color>");
                // Server generates the seed after a short delay for intensity sync
                SpawnTrackEarly().Forget();
            }
            else if (_netTrackSeed.Value != 0)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-7HR] [HexRaceController] Client: track seed already set ({_netTrackSeed.Value}), spawning track locally</color>");
                // Client joined after the server already set the seed - spawn immediately
                SpawnTrackLocally(_netTrackSeed.Value);
            }
            else
            {
                // Seed not yet available - start polling fallback.
                // Covers the race condition where OnValueChanged doesn't fire for
                // initial sync and the ClientRpc was sent before this client spawned.
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00CED1>[FLOW-7HR] [HexRaceController] Client: seed not yet available, starting poll fallback</color>");
                StartSeedPoll();
            }
        }

        public override void OnNetworkDespawn()
        {
            CancelSeedPoll();
            ReleaseArenaBuildAnnouncement();
            _netTrackSeed.OnValueChanged -= OnTrackSeedChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>Close the BeginArenaBuild bracket exactly once — after the track spawns, on
        /// seed-poll timeout, or on despawn (whichever comes first), so a failed seed sync can
        /// never wedge the connecting screen on a build that will not happen.</summary>
        void ReleaseArenaBuildAnnouncement()
        {
            if (!_arenaBuildAnnounced) return;
            _arenaBuildAnnounced = false;
            PrismTrailBuilder.EndArenaBuild();
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
                        CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-7HR] [HexRaceController] Client poll: seed arrived ({_netTrackSeed.Value}), spawning track</color>");
                        SpawnTrackLocally(_netTrackSeed.Value);
                        return;
                    }
                }

                Debug.LogWarning("[HexRaceController] Client poll: timed out after 5s waiting for track seed.");
                ReleaseArenaBuildAnnouncement();
            }
            catch (System.OperationCanceledException)
            {
                // Network despawn or object destroyed - expected
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
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-7HR] [HexRaceController] SpawnTrackLocally SKIPPED - _trackSpawned={_trackSpawned}, segmentSpawner={segmentSpawner != null}</color>");
                return;
            }
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-7HR] [HexRaceController] SpawnTrackLocally - seed={trackSeed}, Intensity={Intensity}</color>");
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
            // The build has executed — laid prisms are now covered by the gate's grow watch.
            ReleaseArenaBuildAnnouncement();
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
        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
        /// ExecuteServerTurnEnd → SetupNewRound, so FinalResultsSent latches in time to
        /// suppress the Ready button. Score assignment, roster snapshot, and the canonical
        /// results tail are owned by the base SyncFinalResults template.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || FinalResultsSent) return;

            // Domain-aggregated end + scoring delegated to the mode's ScoringRule: the first
            // active domain whose summed metric reaches the target wins together. Teammates
            // (human and AI on the same domain) finish the race as a team.
            if (!rule.IsObjectiveReached(gameData, out var winningDomain))
                return;

            // (_raceEnded retired: the shared SyncFinalResults template owns the latch as
            //  FinalResultsSent, checked above and set inside SyncFinalResults.)
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-10] [HexRaceController] Objective reached - domain {winningDomain} wins. Broadcasting final scores.</color>");

            // Winner = finish time (the server's elapsed-time Score feed); losers get the
            // DOMAIN crystal-deficit sentinel (the rule owns this).
            float finishTime = gameData.LocalRoundStats?.Score ?? 0f;
            SyncFinalResults(winningDomain, finishTime);
        }

        // OnResetForReplayCustom removed - HexRace uses UseSceneReloadForReplay = true,
        // which performs a full scene reload. All race state, track, and environment objects
        // are destroyed with the scene and re-initialized fresh via OnNetworkSpawn.
    }
}