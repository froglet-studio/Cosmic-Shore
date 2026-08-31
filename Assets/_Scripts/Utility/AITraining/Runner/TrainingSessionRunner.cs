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
    ///  - The runner does not spawn vessels. Whatever spawning the game already
    ///    does (ServerPlayerVesselInitializerWithAI, single-player adapters) is
    ///    what produces the vessels; the runner finds the AI-controlled ones in
    ///    gameData.Players and attaches a TrainingModulator to each.
    ///  - Per episode: check out genomes, apply them to the real AIPilots, watch
    ///    for the match to end, harvest fitness from RoundStats, update the
    ///    population, persist, ResetForReplay, repeat.
    ///  - INTERRUPT SAFETY IS THE CONTRACT: after every completed episode the
    ///    session state and archive are marked dirty and the best genomes are
    ///    deployed; stopping (the window's Stop, Unity's Play toggle, a crash)
    ///    loses at most the in-flight match, and an in-flight match is NEVER
    ///    recorded — a partial fitness would poison the rolling mean.
    /// </summary>
    public class TrainingSessionRunner : MonoBehaviour
    {
        // ── Configuration ──────────────────────────
        [Header("References")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] CellRuntimeDataSO cellData;
        [SerializeField] TrainingScenarioSO scenario;
        [SerializeField] TrainingSessionStateSO state;
        [SerializeField] TrainingArchiveSO archive;
        [SerializeField] TrainingTelemetrySO telemetry;

        [Header("Behavior")]
        [SerializeField] bool startOnEnable;
        [SerializeField] int targetEpisodes = -1;        // -1 = run until stopped
        [SerializeField] float watchdogTimeoutSeconds = 180f;
        [SerializeField] bool deployBestToArchive = true;
        [Tooltip("Seconds between forced AssetDatabase flushes during long unattended runs.")]
        [SerializeField] int forceSaveEverySeconds = 300;

        // ── Runtime ──────────────────────────────
        readonly List<TrainingModulator> _activeModulators = new();
        readonly Dictionary<TrainingModulator, TrainingFitness> _fitnessByModulator = new();
        readonly Dictionary<TrainingModulator, List<IFitnessComponent>> _fitnessComponents = new();
        readonly Dictionary<TrainingModulator, int> _populationIndices = new();
        readonly Dictionary<TrainingModulator, IRoundStats> _roundStatsByModulator = new();

        bool _running;
        bool _episodeActive;
        float _episodeStartTime;
        float _watchdogStartTime;
        int _lastForceSaveSecond;
        bool _waitingToStartEpisode;
        float _restartAt;

        public bool IsRunning => _running;
        public TrainingSessionStateSO State => state;
        public TrainingScenarioSO Scenario => scenario;
        public TrainingArchiveSO Archive => archive;
        public TrainingTelemetrySO Telemetry => telemetry;

        // ── Public API ─────────────────────────────
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
            if (gameData == null || scenario == null || state == null)
            {
                Debug.LogError("[Training] GameData / Scenario / State not assigned; cannot start.");
                return;
            }

            PilotTuningGenes.EnsureRegistered();
            EnsureStateInitialized();

            _running = true;
            _waitingToStartEpisode = false;
            _restartAt = 0f;
            _lastForceSaveSecond = (int)Time.realtimeSinceStartup;

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
            // fraction of a complete run's and would poison the rolling mean and
            // the hall of fame. Unwind the modulators and keep everything that
            // already completed.
            if (_episodeActive)
            {
                _episodeActive = false;
                for (int i = 0; i < _activeModulators.Count; i++)
                    if (_activeModulators[i] != null) _activeModulators[i].EndEpisode();
                _activeModulators.Clear();
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
        /// Deploys the hall-of-fame champion AND the current population's top
        /// genomes into the archive. The archive's own roster logic keeps the
        /// behaviorally distinct ones, which is what gives deployed matches
        /// personality variety instead of four copies of the champion.
        /// </summary>
        public void DeployBestToArchive()
        {
            if (archive == null || state == null) return;

            if (state.HallOfFameBest != null)
                archive.Upsert(scenario.Vessel, scenario.GameMode, scenario.Intensity,
                               state.HallOfFameBest, state.HallOfFameBestFitness, state.Population.Generation,
                               notes: $"Auto-deploy after {state.EpisodesCompleted} episodes");

            foreach (var contender in state.Population.GetTopN(3))
            {
                if (contender == null || contender.EvaluationCount == 0) continue;
                archive.Upsert(scenario.Vessel, scenario.GameMode, scenario.Intensity,
                               contender, contender.Fitness, state.Population.Generation);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(archive);
#endif
            if (telemetry != null)
            {
                telemetry.OnArchiveDeployed?.Raise();
                telemetry.RaiseAnyChange();
            }
        }

        /// <summary>
        /// Marks the state/archive assets dirty; optionally flushes to disk now.
        /// Called after every completed episode so an interrupt never costs more
        /// than the in-flight match.
        /// </summary>
        void PersistState(bool forceSave)
        {
#if UNITY_EDITOR
            if (state != null) UnityEditor.EditorUtility.SetDirty(state);
            if (archive != null) UnityEditor.EditorUtility.SetDirty(archive);
            if (forceSave) UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        // ── Lifecycle ──────────────────────────────
        void OnEnable()
        {
            AutoResolveReferences();
            if (startOnEnable) StartSession();
        }

        void OnDisable()
        {
            StopSession();
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
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif

        void EnsureStateInitialized()
        {
            if (state.Population == null || state.Population.PopulationSize == 0
                || state.ScenarioKey != scenario.Key)
            {
                state.ResetForScenario(scenario.Key, scenario);
            }
            if (state.HallOfFameBest == null)
                state.HallOfFameBest = TrainingGenome.FromRegistryDefaults();
        }

        // ── Game data event wiring ───────────────────
        bool _eventsHooked;
        void HookGameDataEvents()
        {
            if (_eventsHooked || gameData == null) return;
            if (gameData.OnMiniGameEnd != null) gameData.OnMiniGameEnd.OnRaised += HandleMiniGameEnd;
            _eventsHooked = true;
        }

        void UnhookGameDataEvents()
        {
            if (!_eventsHooked || gameData == null) return;
            if (gameData.OnMiniGameEnd != null) gameData.OnMiniGameEnd.OnRaised -= HandleMiniGameEnd;
            _eventsHooked = false;
        }

        void HandleMiniGameEnd()
        {
            if (!_running || !_episodeActive) return;
            EndEpisodeInternal(timedOut: false, force: false);
        }

        // ── Per-frame ──────────────────────────────
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

            // Watchdog — if the scene wedged, force-end and keep the loop alive.
            if (Time.time - _watchdogStartTime > watchdogTimeoutSeconds)
            {
                Debug.LogWarning($"[Training] Watchdog timeout after {watchdogTimeoutSeconds}s, force-ending episode.");
                EndEpisodeInternal(timedOut: true, force: true);
                return;
            }

            // Per-frame fitness sampling off each trainee's observation.
            for (int i = 0; i < _activeModulators.Count; i++)
            {
                var m = _activeModulators[i];
                if (m == null || !_fitnessComponents.TryGetValue(m, out var components)) continue;
                var obs = m.Observation;
                for (int c = 0; c < components.Count; c++) components[c].OnFrame(obs);
            }

            float t = Time.time - _episodeStartTime;
            if (t >= scenario.MaxEpisodeSeconds)
            {
                EndEpisodeInternal(timedOut: false, force: true);
                return;
            }

            if (t >= scenario.MinEpisodeSeconds && CheckEarlyExitConditions())
            {
                EndEpisodeInternal(timedOut: false, force: true);
                return;
            }

            // Periodic full flush so hours-long runs hit the disk even with no
            // other editor activity.
            int now = (int)Time.realtimeSinceStartup;
            if (now - _lastForceSaveSecond >= forceSaveEverySeconds)
            {
                _lastForceSaveSecond = now;
                PersistState(forceSave: true);
            }
        }

        bool CheckEarlyExitConditions()
        {
            if (scenario.EarlyExitConditions == null || scenario.EarlyExitConditions.Count == 0)
                return false;
            foreach (var m in _activeModulators)
            {
                if (!_roundStatsByModulator.TryGetValue(m, out var stats) || stats == null) continue;
                foreach (var cond in scenario.EarlyExitConditions)
                    if (Matches(stats, cond)) return true;
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

        // ── Episode start ──────────────────────────
        void StartNextEpisode()
        {
            if (!_running || _episodeActive) return;
            if (gameData == null || gameData.Players == null || gameData.Players.Count == 0)
            {
                _waitingToStartEpisode = true;
                _restartAt = Time.unscaledTime + 0.1f;
                return;
            }

            _activeModulators.Clear();
            _fitnessByModulator.Clear();
            _fitnessComponents.Clear();
            _populationIndices.Clear();
            _roundStatsByModulator.Clear();

            for (int i = 0; i < gameData.Players.Count; i++)
            {
                var player = gameData.Players[i];
                if (player == null || player.Vessel == null) continue;
                if (!ShouldTrainPlayer(player)) continue;

                var vessel = player.Vessel;
                var go = vessel.Transform != null ? vessel.Transform.gameObject : null;
                if (go == null) continue;

                var modulator = go.GetComponent<TrainingModulator>();
                if (modulator == null) modulator = go.AddComponent<TrainingModulator>();
                if (!modulator.BindVessel(vessel)) continue;

                var genome = state.Population.Checkout(out int popIdx);
                modulator.ApplyGenome(genome, scenario.Intensity);
                modulator.BeginEpisode(popIdx);

                var fitnessProfile = scenario.FitnessProfile != null
                    ? scenario.FitnessProfile
                    : EnsureFallbackFitnessProfile();
                var components = fitnessProfile.Build();
                _fitnessComponents[modulator] = components;
                _populationIndices[modulator] = popIdx;
                _roundStatsByModulator[modulator] = player.RoundStats;

                var obs = modulator.Observation;
                foreach (var c in components) c.OnEpisodeStart(obs);

                _activeModulators.Add(modulator);
                Debug.Log($"[Training] {player.Name} flies genome {popIdx} " +
                          $"as '{modulator.PersonalityName}' (gen {state.Population.Generation}).");
            }

            if (_activeModulators.Count == 0)
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

            // Train everything that flies on AI:
            //   1. Players spawned as AI (the backfill pipeline).
            //   2. The host's player when the auto-launcher flipped its vessel onto
            //      autopilot for AI-vs-AI training.
            if (player.IsInitializedAsAI) return true;
            var status = player.Vessel.VesselStatus;
            return status != null && status.AIPilot != null && status.AutoPilotEnabled;
        }

        FitnessProfileSO _fallbackProfile;
        FitnessProfileSO EnsureFallbackFitnessProfile()
        {
            if (_fallbackProfile != null) return _fallbackProfile;
            _fallbackProfile = ScriptableObject.CreateInstance<FitnessProfileSO>();
            _fallbackProfile.name = "Fallback Fitness (in-memory)";
            switch (scenario != null ? scenario.GameMode : GameModes.Random)
            {
                case GameModes.MultiplayerJoust:
                    _fallbackProfile.ApplyJoustDefaults();
                    break;
                case GameModes.DogFight:
                case GameModes.Salvo:
                    _fallbackProfile.ApplyGunneryDefaults();
                    break;
                case GameModes.WildlifeBlitz:
                case GameModes.MultiplayerWildlifeBlitzGame:
                case GameModes.WildlifeLiberation:
                    _fallbackProfile.ApplyHuntDefaults();
                    break;
                case GameModes.AstroLeague:
                case GameModes.ScarabScramble:
                    _fallbackProfile.ApplyCourtDefaults();
                    break;
                case GameModes.MultiplayerCrystalCapture:
                case GameModes.MultiplayerCellularDuel:
                case GameModes.CellularDuel:
                case GameModes.Rampage:
                case GameModes.Ribcage:
                    _fallbackProfile.ApplyCellularCaptureDefaults();
                    break;
                case GameModes.Multiplayer2v2CoOpVsAI:
                    _fallbackProfile.ApplyCoOpTeammateDefaults();
                    break;
                case GameModes.MultiplayerFreestyle:
                    _fallbackProfile.ApplyFreestyleDefaults();
                    break;
                default:
                    _fallbackProfile.ApplyRacingDefaults();
                    break;
            }
            return _fallbackProfile;
        }

        // ── Episode end ────────────────────────────
        void EndEpisodeInternal(bool timedOut, bool force)
        {
            if (!_episodeActive) return;
            _episodeActive = false;

            for (int i = 0; i < _activeModulators.Count; i++)
            {
                var m = _activeModulators[i];
                if (m == null) continue;
                m.EndEpisode();

                if (!_fitnessComponents.TryGetValue(m, out var components)) continue;
                _roundStatsByModulator.TryGetValue(m, out var stats);

                var fitness = new TrainingFitness
                {
                    EpisodeSeconds = Time.time - _episodeStartTime,
                    TimedOut = timedOut
                };

                var profile = scenario.FitnessProfile != null ? scenario.FitnessProfile : EnsureFallbackFitnessProfile();
                var entries = profile.Entries;
                var obs = m.Observation;
                for (int c = 0; c < components.Count; c++)
                {
                    var raw = components[c].Evaluate(obs, stats);
                    var weight = c < entries.Count ? entries[c].Weight : 1f;
                    fitness.Add(components[c].Label, raw, weight);
                }

                _fitnessByModulator[m] = fitness;

                int idx = _populationIndices[m];
                state.Population.ReturnFitness(idx, fitness, m.Genome);
                state.RecordEpisode(fitness, m.Genome);

                Debug.Log($"[Training] Episode ended for '{m.PersonalityName}' " +
                          $"(genome {idx}): {fitness.Summarize()}");
            }

            if (telemetry != null)
            {
                telemetry.EpisodesCompleted = state.EpisodesCompleted;
                telemetry.Generation = state.Population.Generation;
                telemetry.CurrentBestFitness = state.HallOfFameBestFitness;

                var last = _activeModulators.Count > 0 ? _activeModulators[_activeModulators.Count - 1] : null;
                if (last != null && _fitnessByModulator.TryGetValue(last, out var lastFit))
                {
                    telemetry.LastEpisodeFitness = lastFit.Total;
                    telemetry.LastEpisodeBreakdown = lastFit.Summarize();
                }
                telemetry.OnEpisodeEnded?.Raise();
                telemetry.RaiseAnyChange();
            }

            // Durable BEFORE the next match starts: this is the "interrupt any
            // time, keep everything completed" contract.
            if (deployBestToArchive) DeployBestToArchive();
            PersistState(forceSave: false);

            if (force && gameData != null && _running)
                gameData.InvokeGameTurnConditionsMet();

            if (!_running) return;

            if (targetEpisodes > 0 && state.EpisodesCompleted >= targetEpisodes)
            {
                Debug.Log($"[Training] Target episodes ({targetEpisodes}) reached. Stopping.");
                if (deployBestToArchive) DeployBestToArchive();
                StopSession();
                return;
            }

            _waitingToStartEpisode = true;
            _restartAt = Time.unscaledTime + Mathf.Max(0.05f, scenario.DelayBetweenEpisodes);

            if (scenario.UseResetForReplay && gameData != null)
                gameData.ResetForReplay();
        }
    }
}
