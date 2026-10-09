using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Drives the entire game flow from Bootstrap → Auth → Menu → Game scene
    /// without any human input, then keeps the loop running match-after-match.
    ///
    /// Lifecycle:
    ///   1. The editor's Learn button (or any other entry point) creates a
    ///      DontDestroyOnLoad GameObject with this component, hands it the
    ///      TrainingControlSO holding the active scenario, and presses Play.
    ///   2. The Bootstrap scene runs as normal: AppManager configures DI,
    ///      starts auth, waits for splash, transitions to MainMenu.
    ///   3. We watch OnClientReady while a menu scene is loaded, configure
    ///      GameDataSO for an all-AI match, and call InvokeGameLaunch().
    ///   4. When the game scene loads we ensure a TrainingSessionRunner exists
    ///      and start it. The runner looks for AI vessels and trains them.
    ///   5. After every match the runner records fitness and calls
    ///      MiniGameControllerBase.RequestReplay. HexRace reloads the scene.
    ///      This object survives that reload and attaches the next genomes
    ///      once the replacement roster exists.
    ///   6. The host seat is flipped onto autopilot before the runner starts,
    ///      so the host slot trains with the backfilled AI. The human input
    ///      poll is paused; TrainingPilot still writes the sticks.
    ///
    /// The auto-launcher is intentionally idempotent: re-creating it inside
    /// the same play session is a no-op as long as the existing one already
    /// did the launch.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class TrainingAutoLauncher : MonoBehaviour
    {
        public TrainingControlSO Control;

        // Cached references resolved at Start. We don't [Inject] these because
        // the launcher is added at runtime by the editor hook, after AppManager
        // has already finished its DI registration.
        GameDataSO _gameData;
        CellRuntimeDataSO _cellData;
        ApplicationStateDataVariable _appState;

        // Serialized so a domain reload in Play mode does not forget that Learn
        // already launched, and does not start a second match from the menu.
        [SerializeField] bool _hasLaunched;
        bool _hasSpawnedRunner;
        TrainingSessionRunner _runner;
        bool _batchApplied;
        bool _disabledCamera;
        float _savedTimeScale = 1f;
        bool _savedAudioPause;

        Coroutine _launchCo;
        Coroutine _rosterCo;
        Coroutine _safetyCo;

        public TrainingScenarioSO Scenario => Control != null ? Control.Scenario : null;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDestroy()
        {
            RestoreBatch();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnhookAppState();
            UnhookGameDataEvents();
        }

        void Start()
        {
            ResolveProjectAssets();
            HookGameDataEvents();
            HookAppState();
            if (TryResumeAfterDomainReload()) return;
            Trace($"[TrainingAutoLauncher] Started. " +
                  $"GameData={(_gameData != null ? _gameData.name : "null")}, " +
                  $"AppState={(_appState != null ? _appState.name : "null")}, " +
                  $"Scenario={(Scenario != null ? Scenario.Key : "null")}");
        }

        /// <summary>
        /// A script recompile keeps this DontDestroyOnLoad object and drops the
        /// scene runner. The runner's reload callback already abandoned the open
        /// rollout and flushed the last commit. Resume by replaying the match,
        /// not by scoring the interrupted one.
        /// </summary>
        bool TryResumeAfterDomainReload()
        {
            if (!_hasLaunched || Control == null || Control.HumanPlaysThisLaunch) return false;
            string scene = SceneManager.GetActiveScene().name;
            if (_gameData != null && !string.IsNullOrEmpty(_gameData.SceneName) && scene == _gameData.SceneName)
            {
                Trace("[Training] Domain reload in the match. The open rollout was dropped. Requesting a clean replay.");
                ApplyBatchIfTraining();
                var controller = FindAnyObjectByType<MiniGameControllerBase>();
                if (controller == null)
                {
                    Debug.LogWarning("[Training] Domain reload found no match controller. The session asset is intact. Press Learn to resume.");
                    return true;
                }
                controller.RequestReplay();
                return true;
            }
            if (IsMenuScene(scene))
            {
                Trace("[Training] Domain reload in the menu. Launching again. The session asset is unchanged.");
                _launchCo = StartCoroutine(ConfigureAndLaunch());
                return true;
            }
            return false;
        }

        // ── Reference resolution ───────────────────────
        void ResolveProjectAssets()
        {
            if (_gameData == null) _gameData = FindFirstAssetOfType<GameDataSO>();
            if (_cellData == null) _cellData = FindFirstAssetOfType<CellRuntimeDataSO>();
            if (_appState == null) _appState = FindFirstAssetOfType<ApplicationStateDataVariable>();
        }

        static T FindFirstAssetOfType<T>() where T : ScriptableObject
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids == null || guids.Length == 0) return null;
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            return null;
#endif
        }

        // ── App state events ───────────────────────────
        // AppState.MainMenu transitions BEFORE Menu_Main scene loads and BEFORE
        // MainMenuController.Start runs. Configuring + launching from here races
        // MainMenuController.ConfigureMenuGameData (which writes its own values
        // to GameDataSO and stomps ours) and InvokeGameLaunch fires before
        // SceneLoader has its OnLaunchGame listener live for the new scene.
        //
        // The right primary trigger is gameData.OnClientReady — see HookGameDataEvents.
        // The AppState handler stays as a SAFETY NET: if OnClientReady never fires
        // within 12s of MainMenu (e.g. misconfigured menu prefab), we launch anyway.
        void HookAppState()
        {
            if (_appState == null || _appState.Value == null || _appState.Value.OnStateChanged == null)
            {
                Debug.LogWarning("[TrainingAutoLauncher] ApplicationStateData.OnStateChanged not wired; will rely on OnClientReady only.");
                return;
            }
            _appState.Value.OnStateChanged.OnRaised += HandleAppStateChanged;
            if (_appState.Value.State == ApplicationState.MainMenu)
                HandleAppStateChanged(ApplicationState.MainMenu);
        }

        void UnhookAppState()
        {
            if (_appState == null || _appState.Value == null || _appState.Value.OnStateChanged == null) return;
            _appState.Value.OnStateChanged.OnRaised -= HandleAppStateChanged;
        }

        void HandleAppStateChanged(ApplicationState state)
        {
            if (_hasLaunched) return;
            if (state != ApplicationState.MainMenu) return;
            if (Scenario == null)
            {
                Debug.LogError("[TrainingAutoLauncher] No scenario assigned on TrainingControlSO; cannot launch.");
                return;
            }

            Trace("[TrainingAutoLauncher] AppState=MainMenu — waiting for OnClientReady (12s safety timeout).");
            if (_safetyCo == null) _safetyCo = StartCoroutine(SafetyTimeout());
        }

        IEnumerator SafetyTimeout()
        {
            yield return new WaitForSeconds(12f);
            if (_hasLaunched) yield break;
            Debug.LogWarning("[TrainingAutoLauncher] OnClientReady didn't fire within 12s; launching anyway.");
            _hasLaunched = true;
            yield return ConfigureAndLaunch();
        }

        // ── Game data events ───────────────────────────
        void HookGameDataEvents()
        {
            if (_gameData == null)
            {
                Debug.LogError("[TrainingAutoLauncher] GameDataSO not found in project; auto-launch will not work.");
                return;
            }
            if (_gameData.OnClientReady == null)
            {
                Debug.LogWarning("[TrainingAutoLauncher] GameDataSO.OnClientReady is null; falling back to AppState-only trigger.");
                return;
            }
            _gameData.OnClientReady.OnRaised += HandleClientReady;
        }

        void UnhookGameDataEvents()
        {
            if (_gameData == null || _gameData.OnClientReady == null) return;
            _gameData.OnClientReady.OnRaised -= HandleClientReady;
        }

        void HandleClientReady()
        {
            if (_hasLaunched) return;
            // OnClientReady fires every match (game scenes raise it after the local
            // player vessel spawns). Only treat the menu invocation as the launch
            // trigger; in-game invocations are a no-op for us.
            string activeScene = SceneManager.GetActiveScene().name;
            if (!IsMenuScene(activeScene))
            {
                Trace($"[TrainingAutoLauncher] OnClientReady in '{activeScene}' (not menu) — ignoring.");
                return;
            }
            if (Scenario == null)
            {
                Debug.LogError("[TrainingAutoLauncher] No scenario assigned on TrainingControlSO; cannot launch.");
                return;
            }

            _hasLaunched = true;
            Trace($"[TrainingAutoLauncher] OnClientReady in '{activeScene}' — configuring + launching {Scenario.Key}.");
            _launchCo = StartCoroutine(ConfigureAndLaunch());
        }

        static bool IsMenuScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName)
                && sceneName.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── Launch ─────────────────────────────────────
        /// <summary>
        /// Loads the scenario the schedule cursor names. The runner does not
        /// call SceneManager.LoadScene. This raises the same OnLaunchGame the
        /// first Learn uses, so a later slot is a real mode launch.
        /// </summary>
        public void Relaunch()
        {
            if (_launchCo != null) StopCoroutine(_launchCo);
            _launchCo = StartCoroutine(ConfigureAndLaunch());
        }

        IEnumerator ConfigureAndLaunch()
        {
            // OnClientReady fires after MainMenuController.HandleMenuReady finishes
            // its own work, so by here MainMenuController has already written its
            // menu defaults into GameDataSO. Two yields gives any same-frame listeners
            // time to settle before we overwrite.
            yield return null;
            yield return null;

            if (!AlignScenarioToSchedule()) yield break;

            ConfigureGameData();
            yield return null;

            if (_gameData.OnLaunchGame == null)
            {
                Debug.LogError("[TrainingAutoLauncher] GameDataSO.OnLaunchGame is null; cannot launch. Check the GameDataSO inspector.");
                yield break;
            }
            _gameData.InvokeGameLaunch();
            Trace($"[TrainingAutoLauncher] Launched {_gameData.GameMode} (scene='{_gameData.SceneName}') " +
                  $"with {_gameData.SelectedPlayerCount?.Value} players, {_gameData.RequestedAIBackfillCount} AI backfill, " +
                  $"vessel={_gameData.selectedVesselClass?.Value}, intensity={_gameData.SelectedIntensity?.Value}.");
        }

        /// <summary>
        /// Points the control asset at the cursor's scenario. A finished queue
        /// does not launch. A slot with no scenario is skipped and logged.
        /// </summary>
        bool AlignScenarioToSchedule()
        {
            var schedule = Control != null ? Control.Schedule : null;
            if (schedule == null || schedule.Count == 0) return true;
            var state = Control.State;
            int index = state != null ? state.ScheduleIndex : 0;
            if (index < 0) index = 0;

            for (int guard = 0; guard < schedule.Count + 1; guard++)
            {
                if (index >= schedule.Count)
                {
                    Debug.LogWarning("[Training] Schedule is already finished. " +
                                     "Restart the queue to run it again. Completed slots stay archived.");
                    return false;
                }
                var slot = schedule.Get(index);
                if (slot != null && slot.Scenario != null)
                {
                    Control.Scenario = slot.Scenario;
                    if (state != null) state.ScheduleIndex = index;
                    return true;
                }

                Debug.LogWarning($"[Training] Schedule slot {index} has no scenario. Skipping.");
                var decision = TrainingSchedule.AfterFailure(schedule, index, 0);
                TrainingSchedule.Apply(state, decision);
                index = state != null ? state.ScheduleIndex : index + 1;
                if (decision.Action == TrainingSchedule.Action.Finished) return false;
            }
            return false;
        }

        void ConfigureGameData()
        {
            if (_gameData == null)
            {
                Debug.LogError("[TrainingAutoLauncher] GameDataSO not found in project; cannot configure launch.");
                return;
            }

            int totalPlayers = Mathf.Max(2, Scenario.OpponentCount);

            bool humanMatch = Control != null && Control.HumanPlaysThisLaunch;
            _gameData.GameMode = Scenario.GameMode;
            _gameData.SceneName = ResolveSceneName(Scenario.GameMode);
            _gameData.IsTraining = !humanMatch;
            _gameData.IsMultiplayerMode = IsNetworked(Scenario.GameMode);

            if (_gameData.selectedVesselClass != null) _gameData.selectedVesselClass.Value = Scenario.Vessel;
            if (_gameData.SelectedPlayerCount != null) _gameData.SelectedPlayerCount.Value = totalPlayers;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = Mathf.Clamp(Scenario.Intensity, 1, 4);

            // The host keeps one seat. Learn autopilots that seat after spawn.
            // Play against trained AI leaves it human.
            _gameData.RequestedAIBackfillCount = Mathf.Max(0, totalPlayers - 1);
            _gameData.RequestedDomainCount = 3;
        }

        static bool IsNetworked(GameModes mode)
        {
            // SkimRaceController, JoustController, etc. all extend
            // MultiplayerMiniGameControllerBase which is a NetworkBehaviour, so
            // they require a host. The single-player DuelForTheCell/WildlifeBlitz
            // scenes were retired in 2026-09; CoOpWildlifeBlitz scene deleted (BH-5.7);
            // GameModes.Freestyle (7), MultiplayerFreestyle (28) and OnlineDuelForTheCell (29)
            // are retired.
            if (TrainingModeCatalog.TryGet(mode, out _)) return true;
            switch (mode)
            {
                case GameModes.Multiplayer2v2CoOpVsAI:
                    return true;
                default:
                    return false;
            }
        }

        static string ResolveSceneName(GameModes mode)
        {
            // Mirrors the scene table documented in Docs/SCENES.md so the launcher
            // doesn't depend on resolving an SO_ArcadeGame asset (which would
            // require its own discovery mechanism).
            if (TrainingModeCatalog.TryGet(mode, out var row)) return row.SceneName;
            switch (mode)
            {
                default: return "MinigameSkimRace";
            }
        }

        // ── Game scene wiring ──────────────────────────
        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_gameData == null) return;
            if (string.IsNullOrEmpty(_gameData.SceneName)) return;
            if (scene.name != _gameData.SceneName) return;

            _hasSpawnedRunner = false;
            if (_rosterCo != null) StopCoroutine(_rosterCo);
            _rosterCo = StartCoroutine(StartRunnerWhenRosterReady());
        }

        /// <summary>
        /// Waits until the existing spawn pipeline has a vessel for every
        /// requested seat, flips the host onto autopilot once, then starts
        /// the runner. A parallel flip after StartSession re-enables AIPilot
        /// on a vessel the training pilot already owns.
        /// </summary>
        IEnumerator StartRunnerWhenRosterReady()
        {
            if (_gameData == null) yield break;

            int expected = _gameData.SelectedPlayerCount != null
                ? Mathf.Max(1, _gameData.SelectedPlayerCount.Value)
                : 1;
            float deadline = Time.unscaledTime + 60f;
            while (Time.unscaledTime < deadline && CountReadyVessels() < expected)
                yield return new WaitForSeconds(0.25f);

            int ready = CountReadyVessels();
            if (ready < expected)
                Debug.LogWarning($"[TrainingAutoLauncher] Roster deadline: {ready}/{expected} vessels. Starting with whoever is present.");

            if (Control != null && Control.HumanPlaysThisLaunch)
            {
                Trace("[TrainingAutoLauncher] Human match. The host keeps the sticks. " +
                      "TrainingDeploymentService installs the archive on the AI seats.");
                yield break;
            }

            ApplyBatchIfTraining();
            FlipHostOntoAutopilot();
            yield return null;

            int completed = Control != null && Control.State != null ? Control.State.EpisodesCompleted : 0;
            float gap = Scenario != null ? Scenario.DelayBetweenEpisodes : 0f;
            if (completed > 0 && gap > 0f)
                yield return new WaitForSeconds(gap);

            if (_hasSpawnedRunner) yield break;
            _hasSpawnedRunner = true;

            _runner = FindAnyObjectByType<TrainingSessionRunner>();
            if (_runner == null)
            {
                var go = new GameObject("[Training Runner]");
                _runner = go.AddComponent<TrainingSessionRunner>();
            }

            _runner.Configure(Control.Scenario, Control.State, Control.Archive, Control.Telemetry,
                              _gameData, _cellData);
            if (Control != null)
            {
                bool queued = Control.Schedule != null && Control.Schedule.Count > 0;
                _runner.BindSchedule(queued ? Control.Schedule : null);
                // A queue uses each slot's own caps. The single-scenario target
                // would end the night at the first mode.
                int episodes = queued ? -1 : Control.TargetEpisodes;
                _runner.ApplyOperatorLimits(episodes, Control.WatchdogSeconds);
            }
            _runner.StartSession();
        }

        int CountReadyVessels()
        {
            if (_gameData == null || _gameData.Players == null) return 0;
            int n = 0;
            for (int i = 0; i < _gameData.Players.Count; i++)
            {
                var p = _gameData.Players[i];
                if (p != null && p.Vessel != null) n++;
            }
            return n;
        }

        void FlipHostOntoAutopilot()
        {
            if (_gameData == null || _gameData.Players == null) return;
            for (int i = 0; i < _gameData.Players.Count; i++)
            {
                var p = _gameData.Players[i];
                if (p == null || p.Vessel == null || p.IsInitializedAsAI) continue;
                var vs = p.Vessel.VesselStatus;
                if (vs == null || vs.AIPilot == null) continue;
                if (!vs.AutoPilotEnabled)
                    p.Vessel.ToggleAIPilot(true);
                // Stops the local keyboard from writing sticks. TrainingPilot
                // writes them itself and does not treat this pause as idle.
                if (p.InputController != null) p.InputController.SetPause(true);
                Trace($"[TrainingAutoLauncher] Host seat '{p.Name}' is on autopilot for this rollout.");
            }
        }

        void ApplyBatchIfTraining()
        {
            if (_batchApplied || Control == null || Control.HumanPlaysThisLaunch) return;
            float scale = TrainingControlSO.ResolveTimeScale(Control.SimulationTimeScale);
            bool wantScale = scale > 1f;
            bool scaleOk = wantScale && !PartyHasRemotes();
            if (wantScale && !scaleOk)
                Debug.LogWarning("[Training] Overnight time scale stays at 1 while another client is connected.");
            if (!scaleOk && !Control.MuteAudio && !Control.DisableCameraRendering) return;

            _savedTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            _savedAudioPause = AudioListener.pause;
            if (scaleOk) Time.timeScale = scale;
            if (Control.MuteAudio) AudioListener.pause = true;
            if (Control.DisableCameraRendering && Camera.main != null)
            {
                Camera.main.enabled = false;
                _disabledCamera = true;
            }
            _batchApplied = true;
        }

        void RestoreBatch()
        {
            if (!_batchApplied) return;
            Time.timeScale = _savedTimeScale <= 0f ? 1f : _savedTimeScale;
            if (Control != null && Control.MuteAudio)
                AudioListener.pause = _savedAudioPause;
            if (_disabledCamera && Camera.main != null)
                Camera.main.enabled = true;
            _batchApplied = false;
            _disabledCamera = false;
        }

        static bool PartyHasRemotes()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm != null && nm.IsListening && nm.ConnectedClientsIds != null && nm.ConnectedClientsIds.Count > 1;
        }

        static void Trace(string message)
        {
            CSDebug.LogVerbose(CSLogChannel.AITraining, message);
        }
    }
}
