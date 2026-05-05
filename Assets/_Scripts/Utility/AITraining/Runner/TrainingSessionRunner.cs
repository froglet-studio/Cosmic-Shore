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
    ///    population is updated, and gameData.ResetForReplay restarts the scene.
    ///  - The runner survives editor domain reloads via SerializeReference on
    ///    its mutable state and HideInInspector on the bookkeeping fields.
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

        public bool IsRunning => _running;
        public TrainingSessionStateSO State => state;
        public TrainingScenarioSO Scenario => scenario;
        public TrainingArchiveSO Archive => archive;
        public TrainingTelemetrySO Telemetry => telemetry;

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

            _running = true;
            _waitingToStartEpisode = false;
            _restartAt = 0f;
            _lastDeploySecond = (int)Time.realtimeSinceStartup;

            HookGameDataEvents();

            if (telemetry != null)
            {
                telemetry.IsRunning = true;
                telemetry.ActiveScenario = scenario.Key;
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
            }

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
        /// Marks the session-state asset dirty and flushes to disk. Called after
        /// every completed episode so that an interrupt never costs more than the
        /// last in-flight match. forceSave = true forces an immediate AssetDatabase
        /// save; otherwise the save piggybacks on Unity's next batch flush so we
        /// don't IO-thrash during overnight runs.
        /// </summary>
        void PersistState(bool forceSave)
        {
#if UNITY_EDITOR
            if (state != null) UnityEditor.EditorUtility.SetDirty(state);
            if (archive != null) UnityEditor.EditorUtility.SetDirty(archive);
            if (forceSave) UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        public void DeployBestToArchive()
        {
            if (archive == null || state?.HallOfFameBest == null) return;
            archive.Upsert(scenario.Vessel, scenario.GameMode, scenario.Intensity,
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
            // Auto-find any unassigned references from the project. Lets a runner
            // dropped into a scene work without anyone wiring four serialized fields,
            // which is what the editor window's Quick Setup relies on.
            AutoResolveReferences();
            if (startOnEnable) StartSession();
        }

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

            // Watchdog timeout — if the game scene wedged for any reason, force-end the episode.
            if (Time.time - _watchdogStartTime > watchdogTimeoutSeconds)
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

            // Periodic full AssetDatabase flush. Per-episode persistence already marks
            // assets dirty; this just makes sure they hit the disk every few minutes
            // even on a long overnight run with no other editor activity.
            int now = (int)Time.realtimeSinceStartup;
            if (now - _lastDeploySecond >= deployEverySeconds)
            {
                _lastDeploySecond = now;
                PersistState(forceSave: true);
            }
        }

        bool CheckEarlyExitConditions()
        {
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
            _watchdogStartTime = Time.time;

            if (telemetry != null)
            {
                telemetry.OnEpisodeStarted?.Raise();
                telemetry.RaiseAnyChange();
            }
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
            switch (scenario != null ? scenario.GameMode : CosmicShore.Data.GameModes.Random)
            {
                case CosmicShore.Data.GameModes.MultiplayerJoust:
                    _fallbackProfile.ApplyJoustDefaults();
                    break;
                case CosmicShore.Data.GameModes.MultiplayerCrystalCapture:
                case CosmicShore.Data.GameModes.MultiplayerCellularDuel:
                case CosmicShore.Data.GameModes.CellularDuel:
                    _fallbackProfile.ApplyCellularCaptureDefaults();
                    break;
                case CosmicShore.Data.GameModes.Freestyle:
                case CosmicShore.Data.GameModes.MultiplayerFreestyle:
                    _fallbackProfile.ApplyFreestyleDefaults();
                    break;
                default:
                    _fallbackProfile.ApplyRacingDefaults();
                    break;
            }
            return _fallbackProfile;
        }

        // ── Episode end ────────────────────────────────
        void EndEpisodeInternal(bool timedOut, bool force)
        {
            if (!_episodeActive) return;
            _episodeActive = false;

            for (int i = 0; i < _activePilots.Count; i++)
            {
                var pilot = _activePilots[i];
                pilot.EndEpisode();

                if (!_fitnessComponents.TryGetValue(pilot, out var components)) continue;
                _roundStatsByPilot.TryGetValue(pilot, out var stats);

                var fitness = new TrainingFitness
                {
                    EpisodeSeconds = Time.time - _episodeStartTime,
                    TimedOut = timedOut
                };
                var ctx = pilot.GetCurrentContextOrNull();
                if (ctx != null)
                {
                    // Match weights from the active profile (asset or fallback) so the
                    // breakdown reported in telemetry matches what selection actually used.
                    var profile = scenario.FitnessProfile != null ? scenario.FitnessProfile : EnsureFallbackFitnessProfile();
                    var entries = profile.Entries;
                    for (int c = 0; c < components.Count; c++)
                    {
                        var raw = components[c].Evaluate(ctx, stats);
                        var weight = c < entries.Count ? entries[c].Weight : 1f;
                        fitness.Add(components[c].Label, raw, weight);
                    }
                }

                _fitnessByPilot[pilot] = fitness;

                int idx = _populationIndices[pilot];
                state.Population.ReturnFitness(idx, fitness, pilot.Genome);
                state.RecordEpisode(fitness, pilot.Genome);

                Debug.Log($"[Training] Episode ended for {pilot.Genome.Lineage}: {fitness.Summarize()}");
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
            PersistState(forceSave: false);

            if (force && gameData != null && _running)
                gameData.InvokeGameTurnConditionsMet();    // Same path AITrainingController used.

            if (!_running) return;

            // Decide whether we're done.
            if (targetEpisodes > 0 && state.EpisodesCompleted >= targetEpisodes)
            {
                Debug.Log($"[Training] Target episodes ({targetEpisodes}) reached. Stopping.");
                if (deployBestToArchive) DeployBestToArchive();
                StopSession();
                return;
            }

            // Schedule the next episode.
            _waitingToStartEpisode = true;
            _restartAt = Time.unscaledTime + Mathf.Max(0.05f, scenario.DelayBetweenEpisodes);

            if (scenario.UseResetForReplay && gameData != null)
                gameData.ResetForReplay();
        }
    }
}
