using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Orchestrates the per-episode training loop inside a running game scene.
    ///
    /// Design notes:
    ///  - The runner does not spawn vessels itself. It assumes whatever spawning
    ///    logic the game uses (single-player, ServerPlayerVesselInitializerWithAI,
    ///    etc.) has already produced playable vessels and that those vessels are
    ///    in gameData.Players.
    ///  - On every episode, the runner finds the AI players, installs a
    ///    TrainingPilot on each, hands them genomes, and watches for end
    ///    conditions. When the episode ends, fitness is harvested, the
    ///    population is updated, and MiniGameControllerBase.RequestReplay
    ///    restarts the match. HexRace reloads the scene; the launcher
    ///    attaches the next genomes to the new roster.
    ///  - This component lives in the game scene. A scene reload and a domain
    ///    reload both destroy it. The population that survives is the one
    ///    already committed to TrainingSessionStateSO. An open checkout is
    ///    abandoned and is not a fitness row.
    ///  - Every public surface is synchronous and deterministic — no UniTask, no
    ///    coroutines for control flow — so that the editor window can drive it
    ///    step-by-step in either edit-time tests or play mode.
    /// </summary>
    public class TrainingSessionRunner : MonoBehaviour
    {
        // ── Configuration ──────────────────────────────
        [Header("References")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] CellRuntimeDataSO cellData;
        [SerializeField] TrainingScenarioSO scenario;
        [SerializeField] TrainingSessionStateSO state;
        [SerializeField] TrainingArchiveSO archive;
        [SerializeField] TrainingTelemetrySO telemetry;

        [Header("Population Override")]
        [SerializeField] bool overrideScenarioDefaults;
        [SerializeField] int populationSize = 24;
        [SerializeField] int eliteCount = 4;

        [Header("Behavior")]
        [SerializeField] bool startOnEnable;
        [SerializeField] int targetEpisodes = -1;        // -1 = run until stopped
        [SerializeField] float watchdogTimeoutSeconds = 180f;
        [SerializeField] bool deployBestToArchive = true;
        [SerializeField] int deployEverySeconds = 300;

        // ── Runtime ────────────────────────────────────
        readonly List<TrainingPilot> _activePilots = new();
        readonly Dictionary<TrainingPilot, TrainingFitness> _fitnessByPilot = new();
        readonly Dictionary<TrainingPilot, List<IFitnessComponent>> _fitnessComponents = new();
        readonly Dictionary<TrainingPilot, int> _populationIndices = new();
        readonly Dictionary<TrainingPilot, IRoundStats> _roundStatsByPilot = new();

        bool _running;
        bool _episodeActive;
        float _episodeStartTime;
        float _watchdogStartTime;
        int _lastDeploySecond;
        bool _waitingToStartEpisode;
        float _restartAt;
        bool _replayHandoff;
        int _checkoutsThisEpisode;
        float _episodeWallStart;
        TrainingScheduleSO _schedule;

        public bool IsRunning => _running;
        public TrainingSessionStateSO State => state;
        public TrainingScenarioSO Scenario => scenario;
        public TrainingArchiveSO Archive => archive;
        public TrainingTelemetrySO Telemetry => telemetry;
        public int TargetEpisodes => targetEpisodes;
        public float WatchdogTimeoutSeconds => watchdogTimeoutSeconds;

        /// <summary>
        /// Copies the operator limits from TrainingControlSO onto this runner.
        /// A non-positive episode target runs until Stop. A non-positive watchdog
        /// keeps the 180s default so an older control asset cannot wedge matches at 0s.
        /// </summary>
        public void ApplyOperatorLimits(int episodes, float watchdogSeconds)
        {
            targetEpisodes = episodes;
            watchdogTimeoutSeconds = watchdogSeconds > 0f ? watchdogSeconds : 180f;
        }

        /// <summary>
        /// Null clears the queue and the runner uses <see cref="TargetEpisodes"/>.
        /// A bound schedule advances on the slot's own caps. The global target
        /// does not end the night.
        /// </summary>
        public void BindSchedule(TrainingScheduleSO schedule)
        {
            _schedule = schedule;
        }

        // ── Public API ─────────────────────────────────
        public void Configure(TrainingScenarioSO scn, TrainingSessionStateSO st, TrainingArchiveSO arch, TrainingTelemetrySO tel, GameDataSO gd, CellRuntimeDataSO cd)
        {
            scenario = scn; state = st; archive = arch; telemetry = tel; gameData = gd; cellData = cd;
        }

        public void StartSession()
        {
            if (_running)
            {
                Debug.LogWarning("[Training] StartSession called while already running.");
                return;
            }
            if (gameData == null)
            {
                Debug.LogError("[Training] No GameDataSO assigned; cannot start.");
                return;
            }
            if (scenario == null || state == null)
            {
                Debug.LogError("[Training] Scenario / State not assigned.");
                return;
            }

            PolicyBootstrap.EnsureInitialized();
            EnsureStateInitialized();
            state.EpisodesRequested = targetEpisodes;
            // Stands TrainingDeploymentService and any prefab bridge down for
            // the whole play session, including the scene reload between matches.
            gameData.IsTraining = true;

            _running = true;
            _waitingToStartEpisode = false;
            _restartAt = 0f;
            _lastDeploySecond = (int)Time.realtimeSinceStartup;

            HookGameDataEvents();

            if (telemetry != null)
            {
                telemetry.IsRunning = true;
                telemetry.ActiveScenario = scenario.Key;
                telemetry.EpisodesPlanned = targetEpisodes;
                telemetry.OnSessionStarted?.Raise();
                telemetry.RaiseAnyChange();
            }

            StartNextEpisode();
        }

        public void StopSession()
        {
            if (!_running) return;
            _running = false;

            // CRITICAL: do NOT record the in-progress episode. Its fitness is a
            // fraction of what a complete run would produce and would poison both
            // the rolling-mean fitness on the genome and the hall-of-fame best.
            // Just unwind any active pilots and unhook events.
            if (_episodeActive)
            {
                _episodeActive = false;
                for (int i = 0; i < _activePilots.Count; i++)
                {
                    var pilot = _activePilots[i];
                    if (pilot != null) pilot.EndEpisode();
                }
                _activePilots.Clear();
                // None of this episode's genomes returned fitness. The committed
                // cursor never moved, so the next rollout re-serves them. Do not
                // RecordEpisode.
                if (state != null && state.Population != null)
                    state.Population.AbandonCheckout(_checkoutsThisEpisode);
                _checkoutsThisEpisode = 0;
                Trace("[Training] Halt abandoned the in-flight rollout. Completed evaluations stay on the session asset.");
            }

            if (gameData != null) gameData.IsTraining = false;

            UnhookGameDataEvents();
            PersistState(forceSave: true);

            if (telemetry != null)
            {
                telemetry.IsRunning = false;
                telemetry.OnSessionStopped?.Raise();
                telemetry.RaiseAnyChange();
            }
        }

        /// <summary>
        /// Flushes the last committed generation. Refuses to write while a
        /// checkout is still open, so a mid-match save cannot persist a cursor
        /// or a fitness value the episode has not finished.
        /// </summary>
        void PersistState(bool forceSave)
        {
            if (state != null && state.Population != null && state.Population.HasOpenCheckout)
                return;
#if UNITY_EDITOR
            if (state != null) UnityEditor.EditorUtility.SetDirty(state);
            if (archive != null) UnityEditor.EditorUtility.SetDirty(archive);
            if (forceSave) UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        public void DeployBestToArchive()
        {
            if (archive == null || state?.HallOfFameBest == null) return;
            // The trained vector is always the intensity-4 entry. A scenario intensity
            // below 4 only dithers the match that produced the fitness; it does not
            // file a second, mutated genome.
            archive.Upsert(scenario.Vessel, scenario.GameMode, ArchiveDeployment.TrainedIntensity,
                           state.HallOfFameBest, state.HallOfFameBestFitness, state.Population.Generation,
                           notes: $"Auto-deploy after {state.EpisodesCompleted} episodes");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(archive);
#endif
            if (telemetry != null)
            {
                telemetry.OnArchiveDeployed?.Raise();
                telemetry.RaiseAnyChange();
            }
        }

        // ── Lifecycle ──────────────────────────────────
        void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
            // Auto-find any unassigned references from the project. Lets a runner
            // dropped into a scene work without anyone wiring four serialized fields,
            // which is what the editor window's Quick Setup relies on.
            AutoResolveReferences();
            if (startOnEnable) StartSession();
        }

#if UNITY_EDITOR
        void OnBeforeAssemblyReload()
        {
            // Serialize the commit before Unity snapshots the domain. A replay
            // handoff already wrote the finished episode; stopping it here would
            // clear IsTraining while the next scene is still coming up.
            if (_replayHandoff) return;
            StopSession();
        }
#endif

        void AutoResolveReferences()
        {
#if UNITY_EDITOR
            if (gameData == null) gameData = FirstAssetOfType<GameDataSO>();
            if (cellData == null) cellData = FirstAssetOfType<CellRuntimeDataSO>();
            if (scenario == null) scenario = FirstAssetOfType<TrainingScenarioSO>();
            if (state == null) state = FirstAssetOfType<TrainingSessionStateSO>();
            if (archive == null) archive = FirstAssetOfType<TrainingArchiveSO>();
            if (telemetry == null) telemetry = FirstAssetOfType<TrainingTelemetrySO>();
#endif
        }

#if UNITY_EDITOR
        static T FirstAssetOfType<T>() where T : ScriptableObject
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids == null || guids.Length == 0) return null;
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        }
#endif

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
#endif
            // Scene reload destroys this runner after a completed rollout has
            // already been recorded. StopSession would clear IsTraining before
            // the new scene's pilots spawn, and the deployment service would
            // install archive genomes on top of the next checkout.
            if (_replayHandoff)
            {
                UnhookGameDataEvents();
                _running = false;
                return;
            }
            StopSession();
        }

        void EnsureStateInitialized()
        {
            if (state.Population == null || state.Population.PopulationSize == 0
                || state.ScenarioKey != scenario.Key)
            {
                state.ResetForScenario(scenario.Key, scenario);
                if (overrideScenarioDefaults)
                {
                    state.Population.ConfiguredSize = populationSize;
                    state.Population.EliteCount = eliteCount;
                }
            }
            if (state.HallOfFameBest == null)
                state.HallOfFameBest = TrainingGenome.FromRegistryDefaults();
        }

        // ── Game data event wiring ─────────────────────
        bool _eventsHooked;
        void HookGameDataEvents()
        {
            if (_eventsHooked || gameData == null) return;
            // OnMiniGameEnd fires when the controller declares the game over by its
            // own rules (e.g. HexRace winner detected). That's our primary signal
            // to end the episode. We also listen to OnMiniGameTurnEnd as a fallback
            // for game modes that wrap up at the turn boundary instead.
            if (gameData.OnMiniGameTurnEnd != null) gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
            if (gameData.OnMiniGameEnd != null) gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;
            _eventsHooked = true;
        }

        void UnhookGameDataEvents()
        {
            if (!_eventsHooked || gameData == null) return;
            if (gameData.OnMiniGameTurnEnd != null) gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
            if (gameData.OnMiniGameEnd != null) gameData.OnMiniGameEnd.OnRaised -= HandleMiniGameEnd;
            _eventsHooked = false;
        }

        void HandleTurnEnd()
        {
            // Multi-round controllers fire turn-end before MiniGameEnd. We only
            // close the episode on the latter so each rollout sees a complete game.
            // This handler exists so the subscription doesn't go stale.
        }

        void HandleMiniGameEnd()
        {
            if (!_running || !_episodeActive) return;
            EndEpisodeInternal(timedOut: false, force: false);
        }

        // ── Per-frame ──────────────────────────────────
        void Update()
        {
            if (!_running) return;

            if (_waitingToStartEpisode)
            {
                if (Time.unscaledTime >= _restartAt)
                {
                    _waitingToStartEpisode = false;
                    StartNextEpisode();
                }
                return;
            }

            if (!_episodeActive) return;

            // Watchdog is wall-clock. The episode cap stays on Time.time, so a
            // faster simulation still flies a full cap of game time, and a
            // frozen timeScale still ends a wedged match. A timeout is a finished
            // episode (TimedOut), not a dropped partial.
            if (Time.unscaledTime - _watchdogStartTime > watchdogTimeoutSeconds)
            {
                Debug.LogWarning($"[Training] Watchdog timeout after {watchdogTimeoutSeconds}s, force-ending episode.");
                EndEpisodeInternal(timedOut: true, force: true);
                return;
            }

            // Per-frame fitness sampling.
            for (int i = 0; i < _activePilots.Count; i++)
            {
                var pilot = _activePilots[i];
                if (!_fitnessComponents.TryGetValue(pilot, out var components)) continue;
                foreach (var c in components) c.OnFrame(pilot.GetCurrentContext());
            }

            // Hard episode cap (separate from watchdog so the scenario can still run).
            float t = Time.time - _episodeStartTime;
            if (t >= scenario.MaxEpisodeSeconds)
            {
                EndEpisodeInternal(timedOut: false, force: true);
                return;
            }

            // Early exit conditions if any pilot reaches the threshold.
            if (t >= scenario.MinEpisodeSeconds && CheckEarlyExitConditions())
            {
                EndEpisodeInternal(timedOut: false, force: true);
            }

            // Periodic flush of the last COMMITTED generation. PersistState
            // refuses while a checkout is open, so this cannot write a partial.
            int now = (int)Time.realtimeSinceStartup;
            if (now - _lastDeploySecond >= deployEverySeconds)
            {
                _lastDeploySecond = now;
                PersistState(forceSave: true);
            }
        }

        bool CheckEarlyExitConditions()
        {
            // Golf modes write Score in AssignScores at the domain objective.
            // An individual stat gate closes the rollout before that write.
            if (FitnessProfileSO.ScoreIsGolf(scenario.GameMode)) return false;
            if (scenario.EarlyExitConditions == null || scenario.EarlyExitConditions.Count == 0)
                return false;
            foreach (var pilot in _activePilots)
            {
                if (!_roundStatsByPilot.TryGetValue(pilot, out var stats) || stats == null) continue;
                foreach (var cond in scenario.EarlyExitConditions)
                {
                    if (Matches(stats, cond)) return true;
                }
            }
            return false;
        }

        static bool Matches(IRoundStats stats, TrainingScenarioSO.EarlyExit cond)
        {
            switch (cond.Kind)
            {
                case TrainingScenarioSO.TerminationKind.CrystalsAtLeast: return stats.CrystalsCollected >= cond.IntegerThreshold;
                case TrainingScenarioSO.TerminationKind.ScoreAtLeast: return stats.Score >= cond.FloatThreshold;
                case TrainingScenarioSO.TerminationKind.EnemyCollisionsAtLeast: return stats.SkimmerShipCollisions >= cond.IntegerThreshold;
                case TrainingScenarioSO.TerminationKind.VolumeCreatedAtLeast: return stats.VolumeCreated >= cond.FloatThreshold;
                default: return false;
            }
        }

        // ── Episode start ──────────────────────────────
        void StartNextEpisode()
        {
            if (!_running) return;
            if (_episodeActive) return;
            if (gameData == null || gameData.Players == null || gameData.Players.Count == 0)
            {
                // Players haven't spawned yet. Try again next frame.
                _waitingToStartEpisode = true;
                _restartAt = Time.unscaledTime + 0.1f;
                return;
            }

            _activePilots.Clear();
            _fitnessByPilot.Clear();
            _fitnessComponents.Clear();
            _populationIndices.Clear();
            _roundStatsByPilot.Clear();
            _checkoutsThisEpisode = 0;

            for (int i = 0; i < gameData.Players.Count; i++)
            {
                var player = gameData.Players[i];
                if (player == null || player.Vessel == null) continue;
                if (!ShouldTrainPlayer(player)) continue;

                var vessel = player.Vessel;
                // IVessel exposes Transform via ITransform; that's the safe way to
                // reach the GameObject without assuming the implementation is a
                // MonoBehaviour.
                var go = vessel.Transform != null ? vessel.Transform.gameObject : null;
                if (go == null) continue;

                var pilot = go.GetComponent<TrainingPilot>();
                if (pilot == null) pilot = go.AddComponent<TrainingPilot>();

                pilot.BindVessel(vessel, gameData, cellData);
                pilot.TargetMode = scenario.TargetMode;
                pilot.Intensity = scenario.Intensity;

                var genome = state.Population.Checkout(out int popIdx);
                _checkoutsThisEpisode++;
                pilot.LoadGenome(genome);
                pilot.PopulationIndex = popIdx;
                pilot.BeginEpisode(popIdx);

                // Fall back to an in-memory racing recipe so a scenario without a
                // FitnessProfile asset still trains usefully. This is what makes
                // Quick Setup → Press Play → Press Start work without any manual wiring.
                var fitnessProfile = scenario.FitnessProfile != null
                    ? scenario.FitnessProfile
                    : EnsureFallbackFitnessProfile();
                var components = fitnessProfile.Build();
                _fitnessComponents[pilot] = components;
                _populationIndices[pilot] = popIdx;
                _roundStatsByPilot[pilot] = player.RoundStats;

                var initialCtx = pilot.GetCurrentContextOrNull();
                if (initialCtx != null)
                    foreach (var c in components) c.OnEpisodeStart(initialCtx);

                _activePilots.Add(pilot);
            }

            if (_activePilots.Count == 0)
            {
                Debug.LogWarning("[Training] No trainable players in scene; will retry shortly.");
                _waitingToStartEpisode = true;
                _restartAt = Time.unscaledTime + 0.5f;
                return;
            }

            _episodeActive = true;
            _episodeStartTime = Time.time;
            _watchdogStartTime = Time.unscaledTime;
            _episodeWallStart = Time.unscaledTime;

            // HexRace (and the other domain modes) sit on the Ready button until a
            // human presses it. An all-AI rollout has nobody to press it, so the
            // ships stay stationary, the pilot never builds a context, and the
            // 120s cap records crystals 0 / time 0. This is the same public button
            // the HUD wires — not a score write and not a mode rule change.
            PressMatchReady();

            Trace($"[Training] Rollout start. generation={state.Population.Generation} " +
                  $"evaluations={state.EpisodesCompleted} pilots={_activePilots.Count}");

            if (telemetry != null)
            {
                telemetry.OnEpisodeStarted?.Raise();
                telemetry.RaiseAnyChange();
            }
        }

        void PressMatchReady()
        {
            if (gameData == null || gameData.LocalPlayer == null)
            {
                Trace("[Training] Match ready not pressed — local player is not on the roster yet.");
                return;
            }

            var controller = FindAnyObjectByType<MiniGameControllerBase>();
            if (controller == null)
            {
                Trace("[Training] Match ready not pressed — no game controller in the scene.");
                return;
            }

            Trace("[Training] Pressing match ready.");
            controller.OnReadyClicked();
        }

        bool ShouldTrainPlayer(IPlayer player)
        {
            if (player == null || player.Vessel == null) return false;

            // Train everything that's running on AI. This includes:
            //   1. Players spawned with IsInitializedAsAI (the AI backfill pipeline).
            //   2. The host's player when the auto-launcher has flipped its vessel
            //      onto autopilot for AI-vs-AI training (so all 3 vessels in a HexRace
            //      session train, not just the 2 spawned as AI).
            if (player.IsInitializedAsAI) return true;
            // Same-scene retry: BindVessel already cleared AutoPilotEnabled.
            // The TrainingPilot we added is the marker that this seat is ours.
            var vesselGo = player.Vessel.Transform != null ? player.Vessel.Transform.gameObject : null;
            if (vesselGo != null && vesselGo.GetComponent<TrainingPilot>() != null) return true;
            var status = player.Vessel.VesselStatus;
            return status != null && status.AIPilot != null && status.AutoPilotEnabled;
        }

        FitnessProfileSO _fallbackProfile;
        FitnessProfileSO EnsureFallbackFitnessProfile()
        {
            // Build once per session. Picks a recipe based on the scenario's game mode so
            // the fallback isn't completely off-target if the user forgot to assign one.
            if (_fallbackProfile != null) return _fallbackProfile;
            _fallbackProfile = ScriptableObject.CreateInstance<FitnessProfileSO>();
            _fallbackProfile.name = "Fallback Fitness (in-memory)";
            var mode = scenario != null ? scenario.GameMode : GameModes.Random;
            if (mode == GameModes.OnlineDuelForTheCell || mode == GameModes.DuelForTheCell)
                _fallbackProfile.ApplyCellularCaptureDefaults();
            else
                _fallbackProfile.ApplyFor(mode);
            return _fallbackProfile;
        }

        // ── Episode end ────────────────────────────────
        void EndEpisodeInternal(bool timedOut, bool force)
        {
            if (!_episodeActive) return;
            _episodeActive = false;

            int recorded = 0;
            for (int i = 0; i < _activePilots.Count; i++)
            {
                var pilot = _activePilots[i];
                // Harvest while the episode is still active. EndEpisode clears
                // GetCurrentContextOrNull, and a null context skips Evaluate so
                // every rollout records total=0.00 with an empty breakdown.
                var ctx = pilot.GetCurrentContextOrNull();
                pilot.EndEpisode();

                if (!_fitnessComponents.TryGetValue(pilot, out var components)) continue;
                _roundStatsByPilot.TryGetValue(pilot, out var stats);

                var fitness = new TrainingFitness
                {
                    EpisodeSeconds = Time.time - _episodeStartTime,
                    TimedOut = timedOut
                };
                if (ctx != null)
                {
                    // The pilot stamps EpisodeTime only on frames it actually ticks.
                    // A ship held stationary (ready screen, countdown) never ticks, so
                    // the context clock stays 0 while this runner's clock is the
                    // episode. TimePenalty reads the context. Carry the runner clock
                    // across before Evaluate.
                    ctx.EpisodeTime = Mathf.Max(ctx.EpisodeTime, fitness.EpisodeSeconds);

                    // Match weights from the active profile (asset or fallback) so the
                    // breakdown reported in telemetry matches what selection actually used.
                    var profile = scenario.FitnessProfile != null ? scenario.FitnessProfile : EnsureFallbackFitnessProfile();
                    var entries = profile.Entries;
                    for (int c = 0; c < components.Count; c++)
                    {
                        var raw = components[c].Evaluate(ctx, stats);
                        var kind = c < entries.Count ? entries[c].Kind : FitnessProfileSO.ComponentKind.ObjectiveProgress;
                        raw = FitnessProfileSO.SignedRaw(scenario.GameMode, kind, raw);
                        var weight = c < entries.Count ? entries[c].Weight : 1f;
                        fitness.Add(components[c].Label, raw, weight);
                    }
                }

                _fitnessByPilot[pilot] = fitness;

                int idx = _populationIndices[pilot];
                state.Population.ReturnFitness(idx, fitness, pilot.Genome);
                state.RecordEpisode(fitness, pilot.Genome);
                recorded++;

                var domain = stats != null ? stats.Domain.ToString() : "?";
                var crystals = stats != null ? stats.CrystalsCollected : 0;
                Trace($"[Training] Rollout recorded. generation={state.Population.Generation} " +
                      $"evaluations={state.EpisodesCompleted} domain={domain} crystals={crystals} " +
                      $"lineage={pilot.Genome.Lineage} {fitness.Summarize()}");
            }

            if (telemetry != null)
            {
                telemetry.EpisodesCompleted = state.EpisodesCompleted;
                telemetry.Generation = state.Population.Generation;
                telemetry.CurrentBestFitness = state.HallOfFameBestFitness;

                var lastPilot = _activePilots.Count > 0 ? _activePilots[_activePilots.Count - 1] : null;
                if (lastPilot != null && _fitnessByPilot.TryGetValue(lastPilot, out var lastFit))
                {
                    telemetry.LastEpisodeFitness = lastFit.Total;
                    telemetry.LastEpisodeBreakdown = lastFit.Summarize();
                }
                telemetry.OnEpisodeEnded?.Raise();
                telemetry.RaiseAnyChange();
            }

            // Save state and deploy after every completed episode. This is what makes
            // "interrupt at any time and keep everything but the in-progress match" work:
            // by the time the next match starts, the previous one is durable on disk.
            if (deployBestToArchive) DeployBestToArchive();
            PersistState(forceSave: true);

            _checkoutsThisEpisode = 0;

            if (!_running) return;

            if (ScheduleIsBound())
            {
                if (state != null)
                {
                    state.ScheduleSlotEpisodes += recorded;
                    state.ScheduleSlotElapsedSeconds += System.Math.Max(0d, Time.unscaledTime - _episodeWallStart);
                }
                var decision = TrainingSchedule.AfterEpisode(
                    _schedule,
                    state != null ? state.ScheduleIndex : 0,
                    state != null ? state.ScheduleSlotEpisodes : 0,
                    state != null ? state.ScheduleSlotElapsedSeconds : 0d);
                if (decision.Action != TrainingSchedule.Action.Continue)
                {
                    LeaveSlot(decision);
                    return;
                }
            }
            else if (targetEpisodes > 0 && state.EpisodesCompleted >= targetEpisodes)
            {
                Trace($"[Training] Target episodes ({targetEpisodes}) reached. Stopping.");
                if (deployBestToArchive) DeployBestToArchive();
                StopSession();
                return;
            }

            // Do not checkout the next genomes on this runner. HexRace's replay
            // destroys the scene, including this component. The launcher builds
            // a new runner once the replacement roster exists.
            RequestMatchReplay();
        }

        bool ScheduleIsBound()
        {
            return _schedule != null && _schedule.Count > 0;
        }

        /// <summary>
        /// The best genome for the slot that just finished is already filed
        /// under that scenario's archive key. Moving the cursor does not
        /// rewrite any other bucket.
        /// </summary>
        void LeaveSlot(TrainingSchedule.Decision decision)
        {
            Trace($"[Training] Schedule {decision.Action} at slot " +
                  $"{(state != null ? state.ScheduleIndex : 0)}: {decision.Reason}");
            TrainingSchedule.Apply(state, decision);
            PersistState(forceSave: true);
            if (decision.Action == TrainingSchedule.Action.Finished)
            {
                Trace("[Training] Schedule finished. Completed slots stay in their archive buckets.");
                StopSession();
                return;
            }
            HandoffToScenario();
        }

        /// <summary>
        /// A wedged scenario. The recorded rows of every finished slot stay.
        /// This slot retries or is skipped. The queue is not stopped.
        /// </summary>
        public void NoteScheduleFailure(string reason)
        {
            if (!ScheduleIsBound() || state == null)
            {
                Debug.LogError($"[Training] {reason} The loop is stopped.");
                _running = false;
                UnhookGameDataEvents();
                return;
            }

            var decision = TrainingSchedule.AfterFailure(
                _schedule, state.ScheduleIndex, state.ScheduleSlotAttempts);
            Debug.LogWarning($"[Training] {reason} Queue {decision.Action}: {decision.Reason}");
            TrainingSchedule.Apply(state, decision);
            PersistState(forceSave: true);
            if (decision.Action == TrainingSchedule.Action.Finished)
            {
                StopSession();
                return;
            }
            HandoffToScenario();
        }

        void HandoffToScenario()
        {
            _replayHandoff = true;
            _running = false;
            UnhookGameDataEvents();
            var launcher = FindAnyObjectByType<TrainingAutoLauncher>();
            if (launcher == null)
            {
                _replayHandoff = false;
                Debug.LogError("[Training] No auto-launcher. The schedule cursor is saved. " +
                               "Press Learn the queue to resume this slot.");
                if (gameData != null) gameData.IsTraining = false;
                return;
            }
            launcher.Relaunch();
        }

        void RequestMatchReplay()
        {
            var controller = FindAnyObjectByType<MiniGameControllerBase>();
            if (controller == null)
            {
                NoteScheduleFailure("No MiniGameControllerBase in the scene. The completed rollout is saved.");
                return;
            }

            _replayHandoff = true;
            _waitingToStartEpisode = false;
            Trace($"[Training] Requesting replay via {controller.GetType().Name}.RequestReplay");
            controller.RequestReplay();
        }

        static void Trace(string message)
        {
            CSDebug.LogVerbose(CSLogChannel.AITraining, message);
        }
    }
}
