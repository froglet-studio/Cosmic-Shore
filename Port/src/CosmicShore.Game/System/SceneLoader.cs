// Ported verbatim from Assets/_Scripts/System/SceneLoader.cs
// (UI-shell arc 2026-07-08). Mechanical substitutions (README):
// Cysharp.Threading.Tasks → System.Threading.Tasks + CosmicShore.Engine.Tasks
// (UniTaskVoid → Task + .Forget(); UniTask.Delay(TimeSpan, DelayType.UnscaledDeltaTime)
// → GameTask.Delay(seconds, unscaledTime: true)); Obvious.Soap → CosmicShore.Engine.Soap;
// Reflex.Attributes → CosmicShore.Engine.Injection; Unity.Netcode →
// CosmicShore.Engine.Networking; UnityEngine.SceneManagement →
// CosmicShore.Engine.SceneManagement; UnityEngine → CosmicShore.Engine.
//
// LIVE: the full launch / return / session-end flow — SOAP subscriptions
// (OnLaunchGame, OnClickToMainMenuButton, OnActiveSessionEnd, sceneLoaded),
// LaunchGame (pause reset, PlayerPrefs modal-state clear, splash cover +
// FadeFromSplashOnReady arm, the MPPM client-defer guard, the Tournament
// splash-dwell read, the async load), ArmSplashFadeOnNextClientReady (the
// helper PartyInviteController re-arms on invite accept — un-carried this
// iteration), ReturnToMainMenu, LoadSceneAsync (server Netcode load through
// the NetworkManager.SceneManager placeholder / local fallback),
// ClearPlayerVesselReferences (AI despawn ordering), HandleActiveSessionEnd,
// and the quit cleanup.
//
// Deviations: NONE remaining — the file is fully live.
// • (RESTORED 2026-07-08) bootstrap arc — ApplicationStateMachine ported: the
//   [Inject] field + both `_appStateMachine?.TransitionTo(...)` calls are live again.

using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using System.Threading.Tasks;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Engine;

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
    /// Subscribes to SOAP events in code — no per-scene EventListenerNoParam wiring needed.
    ///
    /// Note: This is a plain MonoBehaviour (not NetworkBehaviour). Network-aware
    /// config sync is handled by MultiplayerMiniGameControllerBase.OnNetworkSpawn().
    /// Replay / restart is owned by MiniGameControllerBase.RequestReplay() — both
    /// the scoreboard and the pause menu call it directly.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] float waitBeforeLoading = 0.5f;

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
                Debug.LogError("[SceneLoader] gameData was not injected — check AppManager DI registration.");
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
                ArmSplashFadeOnNextClientReady();
            }
        }

        /// <summary>
        /// Subscribes <see cref="FadeFromSplashOnReady"/> to <see cref="GameDataSO.OnClientReady"/>
        /// so the next time the local player's vessel finishes initialization the splash
        /// overlay fades back to transparent. Idempotent — safe to call repeatedly.
        ///
        /// Called automatically on Menu_Main load via <see cref="OnSceneLoaded"/>. Called
        /// manually by <see cref="Gameplay.PartyInviteController.AcceptInviteAsync"/> after
        /// it sets the splash opaque, because accepting an invite does not trigger a scene
        /// reload on the joining client (the host's Menu_Main is already loaded), so
        /// <see cref="OnSceneLoaded"/> never fires and the fade subscription would otherwise
        /// stay unarmed — leaving the joining client stuck on the splash (Bug B).
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

            // Clear any saved modal return state so no stale modal reopens after the game.
            // The ScreenSwitcher in Menu_Main reads these keys on Start() and would
            // otherwise restore whatever modal was open when the game launched.
            PlayerPrefs.DeleteKey("ReturnToModal");
            PlayerPrefs.Save();

            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LaunchGame — Scene={gameData.SceneName}, Mode={gameData.GameMode}, " +
                      $"IsMultiplayer={gameData.IsMultiplayerMode}, Vessel={gameData.selectedVesselClass.Value}, " +
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
                Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LaunchGame deferring scene load to server — " +
                          $"IsListening={nm.IsListening}, IsServer={nm.IsServer}, IsClient={nm.IsClient}. " +
                          $"Server will replicate scene via Netcode.</color>");
                return;
            }

            // Game config sync to clients is now handled by
            // MultiplayerMiniGameControllerBase.SyncGameConfigToClients_ClientRpc()
            // in the game scene's OnNetworkSpawn, rather than here before scene load.

            // Tournament (Maelstrom): hold the loading splash long enough to read the between-game running
            // standings before the next game loads. Zero outside that window — normal launches, the first
            // game, and the load into the final results summary are not delayed. Host-only: clients returned
            // at the defer guard above and follow the host's held scene load, so their splash holds too.
            float minSplashDwell = TournamentController.Instance != null
                ? TournamentController.Instance.MinLoadSplashDwellSeconds
                : 0f;

            LoadSceneAsync(gameData.SceneName, minSplashDwell).Forget();
        }

        void FadeFromSplashOnReady()
        {
            Debug.Log("<color=#FFFFFF><b>[FLOW-8] [SceneLoader] FadeFromSplashOnReady — OnClientReady fired!</b></color>");
            gameData.OnClientReady.OnRaised -= FadeFromSplashOnReady;
            _sceneTransitionManager?.FadeFromBlack().Forget();
        }

        /// <summary>
        /// Load the main menu scene.
        /// Subscribed to EventOnClickToMainMenuButton and called on session end.
        /// </summary>
        public void ReturnToMainMenu()
        {
            _appStateMachine?.TransitionTo(ApplicationState.MainMenu);

            // Clear stale return-to-screen/modal state so Menu_Main starts clean
            // on HOME with no modals open. These keys are set by ScreenSwitcher
            // during normal menu navigation but become stale when a scene
            // transition destroys modal GameObjects without proper ModalWindowOut().
            PlayerPrefs.DeleteKey("ReturnToScreen");
            PlayerPrefs.DeleteKey("ReturnToModal");
            PlayerPrefs.Save();

            // Show the loading splash immediately so the transition is covered ASAP — e.g. the
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
                Debug.Log($"<color=#FF8C00>[SceneLoader] ReturnToMainMenu deferring to server — " +
                          $"IsListening={nm.IsListening}, IsServer={nm.IsServer}, IsClient={nm.IsClient}.</color>");
                return;
            }

            LoadSceneAsync(menuScene).Forget();
        }

        async Task LoadSceneAsync(string sceneName, float minSplashDwell = 0f)
        {
            Debug.Log($"<color=#FF8C00>[FLOW-3] [SceneLoader] LoadSceneAsync — sceneName={sceneName}</color>");
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
            await GameTask.Delay(wait, unscaledTime: true);

            if (isServer && nm.SceneManager != null)
            {
                nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                // Defensive fallback: no active server (should not happen under the
                // always-hosted model). Load locally so a scene transition never hangs.
                Debug.LogWarning("[SceneLoader] No active server — falling back to local scene load.");
                SceneManager.LoadScene(sceneName);
            }
        }

        void ClearPlayerVesselReferences()
        {
            Debug.Log($"<color=#00FFFF>[DESPAWN] ClearPlayerVesselReferences — Players={gameData.Players.Count}, Vessels={gameData.Vessels.Count}</color>");

            foreach (var player in gameData.Players)
            {
                if (player is Player netPlayer && netPlayer.IsSpawned)
                    netPlayer.NetVesselId.Value = 0;
            }

            // Explicitly despawn AI Player NetworkObjects so they don't persist
            // into Menu_Main. Human players survive (destroyWithScene=false from
            // connection approval) but AI players must be removed.
            // Must happen BEFORE vessel despawn — AI player destruction after vessel
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
                Debug.Log($"<color=#FF8C00>[SceneLoader] HandleActiveSessionEnd deferring to server — " +
                          $"IsListening={nm.IsListening}, IsServer={nm.IsServer}, IsClient={nm.IsClient}.</color>");
                return;
            }

            // Genuine session end — a client lost its connection (OnClientDisconnect)
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
