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
    ///   3. We watch ApplicationStateData.OnStateChanged. When the app reaches
    ///      MainMenu we override GameDataSO with all-AI settings and call
    ///      InvokeGameLaunch() — the same entry point ArcadeGameConfigureModal
    ///      uses, so the rest of the pipeline (SceneLoader, host start, AI
    ///      backfill) runs unchanged.
    ///   4. When the game scene loads we ensure a TrainingSessionRunner exists
    ///      and start it. The runner looks for AI vessels and trains them.
    ///   5. After every match the runner saves state, persists, and calls
    ///      ResetForReplay; the same cycle restarts in place.
    ///   6. If the host's player is human-controlled (the default for HexRace),
    ///      we flip its vessel onto autopilot so all 3 racers are AI. The
    ///      runner's relaxed ShouldTrainPlayer picks it up alongside the
    ///      backfilled AI players.
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

        bool _hasLaunched;
        bool _hasSpawnedRunner;
        TrainingSessionRunner _runner;

        Coroutine _launchCo;
        Coroutine _hostAutopilotCo;
        Coroutine _safetyCo;

        public TrainingScenarioSO Scenario => Control != null ? Control.Scenario : null;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnhookAppState();
            UnhookGameDataEvents();
        }

        void Start()
        {
            ResolveProjectAssets();
            HookGameDataEvents();
            HookAppState();
            Debug.Log($"[TrainingAutoLauncher] Started. " +
                      $"GameData={(_gameData != null ? _gameData.name : "null")}, " +
                      $"AppState={(_appState != null ? _appState.name : "null")}, " +
                      $"Scenario={(Scenario != null ? Scenario.Key : "null")}");
        }

        // ── Reference resolution ───────────────────
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

        // ── App state events ───────────────────────
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

            Debug.Log("[TrainingAutoLauncher] AppState=MainMenu — waiting for OnClientReady (12s safety timeout).");
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

        // ── Game data events ───────────────────────
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
                Debug.Log($"[TrainingAutoLauncher] OnClientReady in '{activeScene}' (not menu) — ignoring.");
                return;
            }
            if (Scenario == null)
            {
                Debug.LogError("[TrainingAutoLauncher] No scenario assigned on TrainingControlSO; cannot launch.");
                return;
            }

            _hasLaunched = true;
            Debug.Log($"[TrainingAutoLauncher] OnClientReady in '{activeScene}' — configuring + launching {Scenario.Key}.");
            _launchCo = StartCoroutine(ConfigureAndLaunch());
        }

        static bool IsMenuScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName)
                && sceneName.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── Launch ─────────────────────────────
        IEnumerator ConfigureAndLaunch()
        {
            // OnClientReady fires after MainMenuController.HandleMenuReady finishes
            // its own work, so by here MainMenuController has already written its
            // menu defaults into GameDataSO. Two yields gives any same-frame listeners
            // time to settle before we overwrite.
            yield return null;
            yield return null;

            ConfigureGameData();
            yield return null;

            if (_gameData.OnLaunchGame == null)
            {
                Debug.LogError("[TrainingAutoLauncher] GameDataSO.OnLaunchGame is null; cannot launch. Check the GameDataSO inspector.");
                yield break;
            }
            _gameData.InvokeGameLaunch();
            Debug.Log($"[TrainingAutoLauncher] Launched {_gameData.GameMode} (scene='{_gameData.SceneName}') " +
                      $"with {_gameData.SelectedPlayerCount?.Value} players, {_gameData.RequestedAIBackfillCount} AI backfill, " +
                      $"vessel={_gameData.selectedVesselClass?.Value}, intensity={_gameData.SelectedIntensity?.Value}.");
        }

        void ConfigureGameData()
        {
            if (_gameData == null)
            {
                Debug.LogError("[TrainingAutoLauncher] GameDataSO not found in project; cannot configure launch.");
                return;
            }

            // FIRST CHOICE: the platform's own launch path. Every playable mode has an
            // SO_ArcadeGame card, and SyncFromArcadeGame is exactly what the arcade
            // configure modal calls — it writes SceneName, GameMode, IsMultiplayerMode
            // and the domain caps in one authoritative sweep, so the trainer can never
            // drift from what the real launch button does. A NEW MODE IS PICKED UP
            // AUTOMATICALLY the moment its card asset exists.
            var card = FindArcadeGameCard(Scenario.GameMode);
            if (card != null)
            {
                _gameData.SyncFromArcadeGame(card);
            }
            else
            {
                // Fallback for a mode with no card: the small hand table. If you land
                // here for a shipping mode, the real fix is the missing card asset.
                Debug.LogWarning($"[TrainingAutoLauncher] No SO_ArcadeGame card found for {Scenario.GameMode}; using fallback scene table.");
                _gameData.GameMode = Scenario.GameMode;
                _gameData.SceneName = FallbackSceneName(Scenario.GameMode);
                _gameData.IsMultiplayerMode = true;
            }

            int totalPlayers = Mathf.Max(2, Scenario.OpponentCount);
            _gameData.IsTraining = true;

            if (_gameData.selectedVesselClass != null) _gameData.selectedVesselClass.Value = Scenario.Vessel;
            if (_gameData.SelectedPlayerCount != null) _gameData.SelectedPlayerCount.Value = totalPlayers;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = Mathf.Clamp(Scenario.Intensity, 1, 4);

            // RequestedAIBackfillCount = total - 1 (for the host's human player slot).
            // The host's vessel is autopiloted post-spawn so all players still train.
            _gameData.RequestedAIBackfillCount = Mathf.Max(0, totalPlayers - 1);
            _gameData.RequestedDomainCount = 3;
        }

        /// <summary>
        /// Finds the SO_ArcadeGame card for a mode. Editor-only asset search — the
        /// trainer is an editor tool; a runtime build would carry the card reference
        /// on the TrainingControlSO instead.
        /// </summary>
        static CosmicShore.ScriptableObjects.SO_ArcadeGame FindArcadeGameCard(GameModes mode)
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:SO_ArcadeGame");
            foreach (var guid in guids)
            {
                var card = UnityEditor.AssetDatabase.LoadAssetAtPath<CosmicShore.ScriptableObjects.SO_ArcadeGame>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (card != null && card.Mode == mode && !string.IsNullOrEmpty(card.SceneName))
                    return card;
            }
#endif
            return null;
        }

        static string FallbackSceneName(GameModes mode)
        {
            switch (mode)
            {
                case GameModes.HexRace: return "MinigameHexRace";
                case GameModes.MultiplayerFreestyle: return "MinigameFreestyleMultiplayer_Gameplay";
                case GameModes.MultiplayerCrystalCapture: return "MinigameCrystalCaptureMultiplayer_Gameplay";
                case GameModes.MultiplayerCellularDuel: return "MinigameDuelForCellMultiplayer_Gameplay";
                case GameModes.MultiplayerJoust: return "MinigameJoust_Gameplay";
                case GameModes.MultiplayerWildlifeBlitzGame: return "MinigameWildlifeBlitzMultuplayerCoOp";
                default: return "MinigameHexRace";
            }
        }

        // ── Game scene wiring ──────────────────────
        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_gameData == null) return;
            if (string.IsNullOrEmpty(_gameData.SceneName)) return;
            if (scene.name != _gameData.SceneName) return;

            _hasSpawnedRunner = false;
            // Wait a frame so MultiplayerMiniGameControllerBase.OnNetworkSpawn has a
            // chance to land — that's what owns the per-scene game config sync.
            StartCoroutine(SpawnRunnerAfterDelay());

            if (_hostAutopilotCo != null) StopCoroutine(_hostAutopilotCo);
            _hostAutopilotCo = StartCoroutine(EnsureHostAutopilot());
        }

        IEnumerator SpawnRunnerAfterDelay()
        {
            yield return null;
            yield return null;

            if (_hasSpawnedRunner) yield break;
            _hasSpawnedRunner = true;

            // Reuse an existing runner if the user dropped one into the scene by hand.
            _runner = FindAnyObjectByType<TrainingSessionRunner>();
            if (_runner == null)
            {
                var go = new GameObject("[Training Runner]");
                _runner = go.AddComponent<TrainingSessionRunner>();
            }

            _runner.Configure(Control.Scenario, Control.State, Control.Archive, Control.Telemetry,
                              _gameData, _cellData);
            _runner.StartSession();
        }

        /// <summary>
        /// Polls gameData.Players for the host's human-controlled player and flips its
        /// vessel onto autopilot so the runner can train it alongside the backfilled AI.
        /// Cooperatively yields until the player and vessel both exist and the
        /// AIPilot component is attached.
        /// </summary>
        IEnumerator EnsureHostAutopilot()
        {
            if (_gameData == null) yield break;

            float deadline = Time.unscaledTime + 30f;
            while (Time.unscaledTime < deadline)
            {
                yield return new WaitForSeconds(0.25f);
                bool flippedAny = false;
                for (int i = 0; i < _gameData.Players.Count; i++)
                {
                    var p = _gameData.Players[i];
                    if (p == null || p.Vessel == null) continue;
                    if (p.IsInitializedAsAI) continue;
                    var vs = p.Vessel.VesselStatus;
                    if (vs == null || vs.AIPilot == null) continue;
                    if (vs.AutoPilotEnabled) continue;

                    p.Vessel.ToggleAIPilot(true);
                    if (p.InputController != null) p.InputController.SetPause(true);
                    flippedAny = true;
                    Debug.Log($"[TrainingAutoLauncher] Flipped host player '{p.Name}' onto autopilot for AI-vs-AI training.");
                }
                if (flippedAny) yield break;
            }
        }
    }
}
