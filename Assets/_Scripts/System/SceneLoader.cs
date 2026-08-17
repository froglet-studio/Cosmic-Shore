using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persistent scene-loading service.
    ///
    /// Handles:
    ///   - Launching gameplay scenes (local + network-aware)
    ///   - Returning to the main menu
    ///   - Application quit cleanup
    ///
    /// Lives on a DontDestroyOnLoad root in the Bootstrap scene.
    /// Registered as a DI singleton via AppManager.
    /// Subscribes to SOAP events in code - no per-scene EventListenerNoParam wiring needed.
    ///
    /// Note: This is a plain MonoBehaviour (not NetworkBehaviour). Network-aware
    /// config sync is handled by MultiplayerMiniGameControllerBase.OnNetworkSpawn().
    /// Replay / restart is owned by MiniGameControllerBase.RequestReplay() - both
    /// the scoreboard and the pause menu call it directly.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] float waitBeforeLoading = 0.5f;

        [SerializeField, Tooltip("How long the opaque splash keeps covering the screen after the menu " +
            "vessel is ready when RETURNING from a game scene, so end-of-session cleanup (despawn " +
            "stragglers, pooled-prism churn, the covered GC) finishes behind the veil instead of " +
            "visibly clearing on screen. Game launches, replays, and the first boot into the menu " +
            "are not delayed.")]
        float menuReturnSettleSeconds = 1.5f;

        // Load Time Insights span: LoadScene call → OnSceneLoaded (server / local path).
        int _sceneLoadSpan = -1;

        // Name of the scene whose load most recently completed. Lets OnSceneLoaded tell a
        // game→menu RETURN (hold the veil while cleanup settles) apart from first boot /
        // auth→menu, which should fade in as fast as they always have.
        string _lastLoadedSceneName;
        bool _holdVeilForMenuSettle;

        [Header("SOAP Events (wired in Bootstrap inspector)")]
        [SerializeField] ScriptableEventNoParam _onClickToMainMenuButton;
        [SerializeField] ScriptableEventNoParam _onActiveSessionEnd;

        [Inject] GameDataSO gameData;
        [Inject] SceneNameListSO _sceneNames;
        [Inject] ApplicationStateMachine _appStateMachine;
        [Inject] SceneTransitionManager _sceneTransitionManager;

        #region Unity Lifecycle

        void OnEnable()
        {
            PauseSystem.TogglePauseGame(false);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            if (!gameData)
            {
                Debug.LogError("[SceneLoader] gameData was not injected - check AppManager DI registration.");
                return;
            }

            gameData.OnLaunchGame.OnRaised += LaunchGame;
            gameData.InvokeSceneTransition(true);

            // Subscribe to SOAP events that were previously wired via per-scene EventListenerNoParam.
            if (_onClickToMainMenuButton)
                _onClickToMainMenuButton.OnRaised += ReturnToMainMenu;
            if (_onActiveSessionEnd)
                _onActiveSessionEnd.OnRaised += HandleActiveSessionEnd;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (!gameData) return;

            gameData.OnLaunchGame.OnRaised -= LaunchGame;
            gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;

            if (_onClickToMainMenuButton)
                _onClickToMainMenuButton.OnRaised -= ReturnToMainMenu;
            if (_onActiveSessionEnd)
                _onActiveSessionEnd.OnRaised -= HandleActiveSessionEnd;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LoadInsights.End(_sceneLoadSpan);
            _sceneLoadSpan = -1;

            if (!gameData) return;
            gameData.InvokeSceneTransition(true);

            // Whenever Menu_Main finishes loading (initial auth→menu or game→menu return),
            // ensure the splash is opaque and subscribe FadeFromSplashOnReady so it fades
            // out only once the vessel spawns (OnClientReady).  LaunchGame already handles
            // this for menu→game transitions; this covers the two menu-entry paths that
            // LaunchGame never sees.
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            if (scene.name == menuScene)
            {
                _sceneTransitionManager?.SetFadeImmediate(1f);

                // Game→menu return (host AND clients — both run sceneLoaded): hold the
                // fade-in for a short settle window so any leftover teardown from the
                // previous session clears behind the veil instead of on screen.
                _holdVeilForMenuSettle = IsGameplayScene(_lastLoadedSceneName);

                ArmSplashFadeOnNextClientReady();
            }

            _lastLoadedSceneName = scene.name;
        }

        /// <summary>
        /// True for any scene that is not one of the three core app scenes — i.e. a
        /// gameplay scene the player is returning FROM when the menu loads next.
        /// </summary>
        bool IsGameplayScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;

            if (_sceneNames != null)
            {
                return sceneName != _sceneNames.MainMenuScene
                    && sceneName != _sceneNames.BootstrapScene
                    && sceneName != _sceneNames.AuthenticationScene;
            }

            return sceneName != "Menu_Main" && sceneName != "Bootstrap" && sceneName != "Authentication";
        }

        /// <summary>
        /// Subscribes <see cref="FadeFromSplashOnReady"/> to <see cref="GameDataSO.OnClientReady"/>
        /// so the next time the local player's vessel finishes initialization the splash
        /// overlay fades back to transparent. Idempotent - safe to call repeatedly.
        ///
        /// Called automatically on Menu_Main load via <see cref="OnSceneLoaded"/>. Called
        /// manually by <see cref="Gameplay.PartyInviteController.AcceptInviteAsync"/> after
        /// it sets the splash opaque, because accepting an invite does not trigger a scene
        /// reload on the joining client (the host's Menu_Main is already loaded), so
        /// <see cref="OnSceneLoaded"/> never fires and the fade subscription would otherwise
        /// stay unarmed - leaving the joining client stuck on the splash (Bug B).
        /// </summary>
        public void ArmSplashFadeOnNextClientReady()
        {
            if (!gameData) return;
            gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
            gameData.OnClientReady.OnRaised += FadeFromSplashOnReady;
        }

        #endregion

        #region Scene Loading

        /// <summary>
        /// Automatically decides local vs network scene loading based on whether a host/server is running.
        /// </summary>
        void LaunchGame()
        {
            PauseSystem.TogglePauseGame(false);

            // A game launch must never inherit a menu-return settle hold that was armed
            // but not yet consumed - game entries fade in on their own flow.
            _holdVeilForMenuSettle = false;

            // Clear any saved modal return state so no stale modal reopens after the game.
            // The ScreenSwitcher in Menu_Main reads these keys on Start() and would
            // otherwise restore whatever modal was open when the game launched.
            PlayerPrefs.DeleteKey("ReturnToModal");
            PlayerPrefs.Save();

            // No IsMultiplayer field in the trace: C5 retired GameDataSO.IsMultiplayerMode
            // because every mode runs the networked single-host model.
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF8C00>[FLOW-3] [SceneLoader] LaunchGame - Scene={gameData.SceneName}, Mode={gameData.GameMode}, " +
                      $"Vessel={gameData.selectedVesselClass.Value}, " +
                      $"Intensity={gameData.SelectedIntensity.Value}, PlayerCount={gameData.SelectedPlayerCount.Value}, " +
                      $"AIBackfill={gameData.RequestedAIBackfillCount}</color>");

            _appStateMachine?.TransitionTo(ApplicationState.LoadingGame);

            // Show splash overlay during scene transition.
            _sceneTransitionManager?.SetFadeImmediate(1f);
            gameData.OnClientReady.OnRaised += FadeFromSplashOnReady;

            var nm = NetworkManager.Singleton;

            // In multiplayer, only the server initiates scene loads.
            // Clients receive the scene transition via Netcode's scene management.
            // Without this guard, shared SOAP events (e.g. in MPPM) cause the client
            // to call SceneManager.LoadScene() locally, which races with the server's
            // network load and destroys AI NetworkObjects before they can replicate.
            if (nm != null && nm.IsListening && !nm.IsServer)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF8C00>[FLOW-3] [SceneLoader] LaunchGame deferring scene load to server - " +
                          $"IsListening={nm.IsListening}, IsServer={nm.IsServer}, IsClient={nm.IsClient}. " +
                          $"Server will replicate scene via Netcode.</color>");
                LoadInsights.Mark("SceneLoader deferred scene load to server (client waits for Netcode scene pull)");
                return;
            }

            // Game config sync to clients is now handled by
            // MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()
            // in the game scene's OnNetworkSpawn, rather than here before scene load.

            // Tournament (Maelstrom): hold the loading splash long enough to read the between-game running
            // standings before the next game loads. Zero outside that window - normal launches, the first
            // game, and the load into the final results summary are not delayed. Host-only: clients returned
            // at the defer guard above and follow the host's held scene load, so their splash holds too.
            float minSplashDwell = TournamentController.Instance != null
                ? TournamentController.Instance.MinLoadSplashDwellSeconds
                : 0f;

            LoadSceneAsync(gameData.SceneName, minSplashDwell).Forget();
        }

        void FadeFromSplashOnReady()
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFFFFF><b>[FLOW-8] [SceneLoader] FadeFromSplashOnReady - OnClientReady fired!</b></color>");
            gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
            FadeFromSplashWhenSettled().Forget();
        }

        async UniTaskVoid FadeFromSplashWhenSettled()
        {
            // Game→menu return only: hold the opaque veil for a short settle window so
            // the previous session's cleanup (late despawns, pooled-prism churn, painting
            // regrow snap) finishes out of sight instead of visibly clearing after the
            // fade. Unscaled - the return path can run at timeScale 0 (pause menu).
            if (_holdVeilForMenuSettle)
            {
                _holdVeilForMenuSettle = false;
                await UniTask.Delay(
                    TimeSpan.FromSeconds(menuReturnSettleSeconds),
                    DelayType.UnscaledDeltaTime);
            }

            // Runs on EVERY peer (clients never reach LoadSceneAsync's pre-load
            // collect — they defer scene loads to the server) with the splash still
            // fully opaque and the NEW scene loaded, so the old scene's managed heap
            // is unreachable here — the most effective covered moment to take the
            // full GC and reset the mid-gameplay collection clock. After the settle
            // window above so the settle churn is collected too.
            GC.Collect();

            _sceneTransitionManager?.FadeFromBlack().Forget();
        }

        /// <summary>
        /// Load the main menu scene.
        /// Subscribed to EventOnClickToMainMenuButton and called on session end.
        /// </summary>
        public void ReturnToMainMenu()
        {
            // The pause menu's Main Menu button fires this with Time.timeScale still 0.
            // Restore it (mirrors LaunchGame) so nothing scaled-time can stall during the
            // transition; the screen is covered by SetFadeImmediate below within the same
            // frame, so no un-paused gameplay is ever visible.
            PauseSystem.TogglePauseGame(false);

            _appStateMachine?.TransitionTo(ApplicationState.MainMenu);

            // Clear stale return-to-screen/modal state so Menu_Main starts clean
            // on HOME with no modals open. These keys are set by ScreenSwitcher
            // during normal menu navigation but become stale when a scene
            // transition destroys modal GameObjects without proper ModalWindowOut().
            PlayerPrefs.DeleteKey("ReturnToScreen");
            PlayerPrefs.DeleteKey("ReturnToModal");
            PlayerPrefs.Save();

            // Show the loading splash immediately so the transition is covered ASAP - e.g. the
            // Tournament summary's Main Menu button, which otherwise left the summary on-screen during
            // the async load (OnSceneLoaded only re-arms this once Menu_Main has finished loading). The
            // idempotent helper arms the fade-back on the next OnClientReady (the menu autopilot vessel).
            // Done before the client-defer guard so clients fade too.
            _sceneTransitionManager?.SetFadeImmediate(1f);
            ArmSplashFadeOnNextClientReady();

            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            var nm = NetworkManager.Singleton;

            // Clients rely on the server's Netcode scene management for transitions.
            if (nm != null && nm.IsListening && !nm.IsServer)
            {
                Debug.Log($"<color=#FF8C00>[SceneLoader] ReturnToMainMenu deferring to server - " +
                          $"IsListening={nm.IsListening}, IsServer={nm.IsServer}, IsClient={nm.IsClient}.</color>");
                return;
            }

            // Cover every connected client's screen BEFORE anything despawns. Clients
            // never run this method (the SOAP raise is host-local), so without this they
            // watch the vessels pop out and the scene tear down uncovered until their
            // own Menu_Main sceneLoaded finally sets the fade. The RPC and the despawn
            // messages share the reliable channel, so the veil always lands first.
            NotifyClientsOfMenuReturn();

            LoadSceneAsync(menuScene).Forget();
        }

        /// <summary>
        /// Asks the active scene's multiplayer game controller (if any) to broadcast the
        /// opaque transition veil to all clients ahead of the return-to-menu teardown.
        /// One-shot scene lookup on a transition boundary - not a hot path. Menu / tool /
        /// single-player scenes have no such controller and skip silently.
        /// </summary>
        void NotifyClientsOfMenuReturn()
        {
            var controller = FindAnyObjectByType<MultiplayerMiniGameControllerBase>(FindObjectsInactive.Include);
            if (controller != null)
                controller.BroadcastReturnToMenuVeil();
        }

        async UniTaskVoid LoadSceneAsync(string sceneName, float minSplashDwell = 0f)
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync - sceneName={sceneName}</color>");
            gameData.InvokeSceneTransition(false);

            var nm = NetworkManager.Singleton;
            bool isServer = nm != null && nm.IsServer;

            if (isServer)
                ClearPlayerVesselReferences();

            gameData.ResetRuntimeData();

            // Normally a short cover for the fade; a between-game tournament transition extends it
            // (minSplashDwell) so the running standings on the splash are readable before the next scene
            // loads. Unscaled so a paused / zero timescale can't stall the hold.
            float wait = Mathf.Max(waitBeforeLoading, minSplashDwell);
            using (LoadInsights.Measure(LoadInsightCategory.ScriptedDelay,
                       $"SceneLoader splash cover before load ({wait:F1}s)", isWait: true))
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(wait),
                    DelayType.UnscaledDeltaTime
                );
            }

            _sceneLoadSpan = LoadInsights.Begin(LoadInsightCategory.SceneLoad,
                $"Scene load → activation ({sceneName})");

            // The splash is opaque here — take a full blocking GC now so the heap
            // that accumulated over the last session (conserved prisms, pool-refill
            // churn) is collected behind cover instead of surfacing mid-gameplay as
            // a ~20ms GC.Collect spike inside a pool-refill Instantiate. Incremental
            // GC (enabled project-wide) spreads minor work but still blocks fully
            // when the heap must expand; this resets that clock at every transition.
            GC.Collect();

            if (isServer && nm.SceneManager != null)
            {
                nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                // Defensive fallback: no active server (should not happen under the
                // always-hosted model). Load locally so a scene transition never hangs.
                Debug.LogWarning("[SceneLoader] No active server - falling back to local scene load.");
                SceneManager.LoadScene(sceneName);
            }
        }

        void ClearPlayerVesselReferences()
        {
            Debug.Log($"<color=#00FFFF>[DESPAWN] ClearPlayerVesselReferences - Players={gameData.Players.Count}, Vessels={gameData.Vessels.Count}</color>");

            foreach (var player in gameData.Players)
            {
                if (player is Player netPlayer && netPlayer.IsSpawned)
                    netPlayer.NetVesselId.Value = 0;
            }

            // Explicitly despawn AI Player NetworkObjects so they don't persist
            // into Menu_Main. Human players survive (destroyWithScene=false from
            // connection approval) but AI players must be removed.
            // Must happen BEFORE vessel despawn - AI player destruction after vessel
            // despawn causes MissingReferenceException when VesselAnimation.Update()
            // accesses the destroyed Player on the same frame.
            for (int i = gameData.Players.Count - 1; i >= 0; i--)
            {
                if (gameData.Players[i] is Player aiPlayer
                    && aiPlayer.IsSpawned
                    && aiPlayer.NetIsAI.Value)
                {
                    aiPlayer.NetworkObject.Despawn(true);
                }
            }

            // Despawn all vessels and destroy their GameObjects. Using destroy=true
            // ensures VesselAnimation.Update() cannot run with stale Player references
            // during the scene transition.
            for (int i = gameData.Vessels.Count - 1; i >= 0; i--)
            {
                var vessel = gameData.Vessels[i];
                if (vessel is VesselController vc && vc.IsSpawned)
                    vc.NetworkObject.Despawn(true);
            }

            gameData.Vessels.Clear();
        }

        #endregion

        #region Session End

        void HandleActiveSessionEnd()
        {
            // Clients rely on the server for session cleanup and scene transitions.
            // Without this guard, shared SOAP events in MPPM cause the client to
            // call ResetAllData() on the shared GameDataSO, wiping server state.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer)
            {
                Debug.Log($"<color=#FF8C00>[SceneLoader] HandleActiveSessionEnd deferring to server - " +
                          $"IsListening={nm.IsListening}, IsServer={nm.IsServer}, IsClient={nm.IsClient}.</color>");
                return;
            }

            // Genuine session end - a client lost its connection (OnClientDisconnect)
            // or the transport failed (OnTransportFailure). The host's deliberate
            // "Main Menu" return does NOT route here; it goes straight through
            // ReturnToMainMenu(), which keeps the live Relay so the whole party
            // reloads Menu_Main together. Here the session is already gone, so we
            // return to the menu and fully reset local game state.
            ReturnToMainMenu();
            gameData.ResetAllData();
        }

        #endregion

        void OnApplicationQuit()
        {
            if (gameData) gameData.ResetAllData();
        }
    }
}
