using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        [Header("Scoring")]
        [Tooltip("Drag HexRaceScoringRule.asset - the per-mode scoring strategy (end condition, scores, results).")]
        [SerializeField] ScoringRuleSO rule;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        private bool _raceEnded;
        private bool _trackSpawned;
        private bool _arenaBuildAnnounced;
        private CancellationTokenSource _seedPollCts;
        private readonly NetworkVariable<int> _netTrackSeed = new(0);

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // HexRace handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        // ── Stripped-performance branch: Skim Race trail ─────────────────────
        // Trails are strip-disabled globally, but in Skim Race the trail IS the mechanic (skim
        // your own laps for boost). Enable the capped-trail mode for this scene's lifetime with
        // the race-sized cap: the track is ~4000u per circuit and the Squirrel lays a prism every
        // 5–7u ⇒ ~600–800 prisms/lap, so 2000 guarantees the player still has AT LEAST two full
        // laps of trail to skim after finishing lap one. Past the cap the oldest prism implodes
        // in place via Prism.Consume (visible transition — never a silent despawn).
        // Awake/OnDestroy (not network spawn/despawn) so the flags are set before any vessel
        // starts its trail spawner and always restore on scene exit.
        void Awake()
        {
            if (!CosmicShore.Utility.PerfStrip.Enabled) return;
            CosmicShore.Utility.PerfStrip.CappedTrailLimit = CosmicShore.Utility.PerfStrip.SkimRaceTrailPrisms;
            CosmicShore.Utility.PerfStrip.CappedTrailActive = true;
        }

        void OnDestroy()
        {
            if (!CosmicShore.Utility.PerfStrip.Enabled) return;
            CosmicShore.Utility.PerfStrip.CappedTrailActive = false;
        }

        public override void OnNetworkSpawn()
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-7HR] [HexRaceController] OnNetworkSpawn - IsServer={IsServer}, Intensity={Intensity}</color>");
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
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
        /// Server-side winner detection, mirroring MultiplayerJoustController.OnTurnEndedCustom().
        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound,
        /// so _raceEnded is set in time to suppress the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _raceEnded) return;

            // Domain-aggregated end + scoring delegated to the mode's ScoringRule: the first
            // active domain whose summed metric reaches the target wins together. Teammates
            // (human and AI on the same domain) finish the race as a team.
            if (!rule.IsObjectiveReached(gameData, out var winningDomain))
                return;

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-10] [HexRaceController] Objective reached - domain {winningDomain} wins. Broadcasting final scores.</color>");
            _raceEnded = true;

            float finishTime = gameData.LocalRoundStats?.Score ?? 0f;

            // Representative winner-name = best individual contributor on the winning
            // domain. Used for the WinnerName legacy field (display strings only -
            // VICTORY/DEFEAT attribution is via WinnerDomain).
            var winnerRep = gameData.RoundStatsList
                .Where(s => s.Domain == winningDomain)
                .OrderByDescending(s => s.CrystalsCollected)
                .FirstOrDefault();
            string winnerName = winnerRep?.Name ?? "";

            // Winner = finish time; losers = DOMAIN crystal-deficit sentinel (the rule owns this).
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            SyncFinalScoresSnapshot(winnerName, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the race just ended.
        /// HasEndGame=false causes ExecuteServerRoundEnd to call SetupNewRound instead of
        /// ExecuteServerGameEnd - this override prevents the Ready button from appearing.
        /// After replay reset, _raceEnded is cleared so new rounds work normally.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_raceEnded) return;
            base.SetupNewRound();
        }

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
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
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] crystalsCollected,
            FixedString64Bytes winnerName,
            int winnerDomain)
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

            // Authoritative winner - written to gameData, consumed by EndGameControllers
            // OnWinnerCalculated (below) is the "results ready" signal.
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // OnResetForReplayCustom removed - HexRace uses UseSceneReloadForReplay = true,
        // which performs a full scene reload. All race state, track, and environment objects
        // are destroyed with the scene and re-initialized fresh via OnNetworkSpawn.
    }
}