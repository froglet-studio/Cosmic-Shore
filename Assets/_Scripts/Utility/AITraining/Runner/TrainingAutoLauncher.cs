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
        }

        void Start()
        {
            ResolveProjectAssets();
            HookAppState();
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
        void HookAppState()
        {
            if (_appState == null || _appState.Value == null || _appState.Value.OnStateChanged == null) return;
            _appState.Value.OnStateChanged.OnRaised += HandleAppStateChanged;

            // If we joined late (state already MainMenu by the time we listen),
            // dispatch ourselves so the launch can proceed without waiting for
            // a state machine that's already settled.
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

            _hasLaunched = true;
            _launchCo = StartCoroutine(ConfigureAndLaunch());
        }

        // ── Launch ─────────────────────────────────────
        IEnumerator ConfigureAndLaunch()
        {
            // Give MainMenuController a frame to finish its own initialisation.
            // It writes default vessel/intensity into GameDataSO during Initialising,
            // and we want to overwrite those AFTER it's done so our values stick.
            yield return null;
            yield return null;

            ConfigureGameData();

            // One more frame so listeners see the configured values before launch.
            yield return null;
            _gameData.InvokeGameLaunch();
            Debug.Log($"[TrainingAutoLauncher] Launched {_gameData.GameMode} ({_gameData.SceneName}) " +
                      $"with {_gameData.SelectedPlayerCount.Value} players, {_gameData.RequestedAIBackfillCount} AI backfill.");
        }

        void ConfigureGameData()
        {
            if (_gameData == null)
            {
                Debug.LogError("[TrainingAutoLauncher] GameDataSO not found in project; cannot configure launch.");
                return;
            }

            int totalPlayers = Mathf.Max(2, Scenario.OpponentCount);

            _gameData.GameMode = Scenario.GameMode;
            _gameData.SceneName = ResolveSceneName(Scenario.GameMode);
            _gameData.IsTraining = true;
            _gameData.IsMultiplayerMode = IsNetworked(Scenario.GameMode);

            if (_gameData.selectedVesselClass != null) _gameData.selectedVesselClass.Value = Scenario.Vessel;
            if (_gameData.SelectedPlayerCount != null) _gameData.SelectedPlayerCount.Value = totalPlayers;
            if (_gameData.SelectedIntensity != null) _gameData.SelectedIntensity.Value = Mathf.Clamp(Scenario.Intensity, 1, 4);

            // RequestedAIBackfillCount = total - 1 (for the host's human player slot).
            // The host's vessel is autopiloted post-spawn so all players still train.
            _gameData.RequestedAIBackfillCount = Mathf.Max(0, totalPlayers - 1);
            _gameData.RequestedDomainCount = 3;
        }

        static bool IsNetworked(GameModes mode)
        {
            // HexRaceController, MultiplayerJoustController, etc. all extend
            // MultiplayerMiniGameControllerBase which is a NetworkBehaviour, so
            // they require a host. Solo arcade controllers (Freestyle,
            // CellularDuel, WildlifeBlitz) are local-only.
            switch (mode)
            {
                case GameModes.HexRace:
                case GameModes.MultiplayerFreestyle:
                case GameModes.MultiplayerCellularDuel:
                case GameModes.Multiplayer2v2CoOpVsAI:
                case GameModes.MultiplayerWildlifeBlitzGame:
                case GameModes.MultiplayerJoust:
                case GameModes.MultiplayerCrystalCapture:
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
            switch (mode)
            {
                case GameModes.HexRace: return "MinigameHexRace";
                case GameModes.MultiplayerFreestyle: return "MinigameFreestyleMultiplayer_Gameplay";
                case GameModes.MultiplayerCrystalCapture: return "MinigameCrystalCaptureMultiplayer_Gameplay";
                case GameModes.MultiplayerCellularDuel: return "MinigameDuelForCellMultiplayer_Gameplay";
                case GameModes.MultiplayerJoust: return "MinigameJoust_Gameplay";
                case GameModes.MultiplayerWildlifeBlitzGame: return "MinigameWildlifeBlitzMultuplayerCoOp";
                case GameModes.Freestyle: return "MinigameFreestyle";
                case GameModes.CellularDuel: return "MinigameCellularDuel";
                case GameModes.WildlifeBlitz: return "MinigameWildlifeBlitz";
                default: return "MinigameHexRace";
            }
        }

        // ── Game scene wiring ──────────────────────────
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
