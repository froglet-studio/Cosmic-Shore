using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Re-runs the boot chain in place so a player who started (or fell back to) an OFFLINE
    /// session can get back online WITHOUT restarting the app.
    ///
    /// <para><b>What it does:</b> tears the offline local host down, clears the offline flag,
    /// resets the auth facade so <c>OnSignedIn</c> can raise again, then loads the
    /// Authentication scene - which is the boot chain: sign in → wait for the Relay host →
    /// load Menu_Main through Netcode scene management. If the network is still unreachable,
    /// that same flow falls back to <see cref="OfflineModeService"/> exactly as it does at
    /// cold boot, so a failed retry lands the player back in a working offline menu rather
    /// than stranding them.</para>
    ///
    /// <para><b>Why the Authentication scene and not Bootstrap.</b> Bootstrap is where the
    /// persistent layer is BUILT - ~15 DontDestroyOnLoad roots (SceneLoader, MultiplayerSetup,
    /// SceneTransitionManager, UGSDataService, AudioSystem, CameraManager, PartyServices, the
    /// splash canvas, NetworkManager …), and only <c>AppManager</c> guards against a second
    /// copy of itself (<c>_hasBootstrapped</c>). Re-loading that scene would spawn a duplicate
    /// of every other one; <c>UGSDataService.Awake</c> alone would clobber <c>Instance</c> and
    /// then null it on the duplicate's destroy. Bootstrap's remaining jobs - platform config
    /// and DI registration - are session-scoped and already done. The Authentication scene is
    /// the part of the boot chain that actually needs re-running, and it is loaded and
    /// unloaded routinely, so this reuses a proven path instead of making a
    /// build-the-world scene re-entrant. See Docs/OFFLINE_MODE.md §7.</para>
    ///
    /// <para>Pure C# lazy DI singleton (registered in <c>AppManager.InstallBindings</c>).</para>
    /// </summary>
    public class ReconnectService
    {
        const float SHUTDOWN_TIMEOUT_SECONDS = 5f;

        readonly GameDataSO _gameData;
        readonly SceneNameListSO _sceneNames;
        readonly AuthenticationServiceFacade _authFacade;
        readonly INetworkTransitionService _networkTransition;
        readonly ApplicationStateMachine _appStateMachine;
        readonly SceneTransitionManager _sceneTransition;
        readonly OfflineModeService _offlineMode;

        /// <summary>
        /// Raised when a reconnect attempt starts and again when it resolves (true = in
        /// flight). UI uses it to disable the retry control while an attempt is running.
        /// </summary>
        public event Action<bool> OnReconnectingChanged;

        /// <summary>True while an attempt is in flight. Retry controls read this.</summary>
        public bool IsReconnecting { get; private set; }

        public ReconnectService(
            GameDataSO gameData,
            SceneNameListSO sceneNames,
            AuthenticationServiceFacade authFacade,
            INetworkTransitionService networkTransition,
            ApplicationStateMachine appStateMachine,
            SceneTransitionManager sceneTransition,
            OfflineModeService offlineMode)
        {
            _gameData = gameData;
            _sceneNames = sceneNames;
            _authFacade = authFacade;
            _networkTransition = networkTransition;
            _appStateMachine = appStateMachine;
            _sceneTransition = sceneTransition;
            _offlineMode = offlineMode;
        }

        /// <summary>
        /// True when a reconnect is worth offering: this session is offline (or has no live
        /// host at all). Online sessions hide the control rather than offering a pointless
        /// retry.
        /// </summary>
        public bool CanReconnect
        {
            get
            {
                if (IsReconnecting) return false;
                if (_gameData == null) return false;
                if (_gameData.IsOfflineSession) return true;

                var nm = NetworkManager.Singleton;
                return nm == null || !nm.IsListening;
            }
        }

        /// <summary>
        /// Switches the session to OFFLINE at the player's request (the menu's online/offline
        /// toggle), as opposed to the automatic fallback. Records the preference so it survives
        /// the app, then re-runs the boot chain - the Authentication scene reads the preference
        /// and goes straight to the local host without touching UGS.
        ///
        /// <para>
        /// It routes through the same boot chain as <see cref="ReconnectAsync"/> rather than
        /// swapping the host underneath a live Menu_Main, because the player object and its
        /// vessel belong to the host being torn down: the spawn chain has to run again on the
        /// new host, and the boot chain is the proven path that does it.
        /// </para>
        /// </summary>
        public UniTask<bool> GoOfflineAsync(CancellationToken ct = default)
        {
            if (_offlineMode != null)
                _offlineMode.OfflinePreferred = true;

            return RunBootChainAsync("Go offline requested", ct);
        }

        /// <summary>
        /// Runs the reconnect. Safe to call repeatedly - concurrent calls collapse to one.
        /// Never throws at the caller: a failure leaves the offline session intact and the
        /// player in the menu, with the retry control live again.
        /// </summary>
        public UniTask<bool> ReconnectAsync(CancellationToken ct = default)
        {
            // The player asked to come back online, so a previously recorded "stay offline"
            // choice no longer applies - clear it BEFORE the boot chain reads it.
            if (_offlineMode != null)
                _offlineMode.OfflinePreferred = false;

            return RunBootChainAsync("Reconnect requested", ct);
        }

        async UniTask<bool> RunBootChainAsync(string reason, CancellationToken ct)
        {
            if (IsReconnecting) return false;

            IsReconnecting = true;
            OnReconnectingChanged?.Invoke(true);

            try
            {
                CSDebug.Log($"[ReconnectService] {reason} - re-running the boot chain.");

                // Cover the screen for the whole transition. The overlay is released on the
                // far side by SceneLoader.FadeFromSplashOnReady when the menu vessel spawns,
                // exactly as at cold boot.
                _sceneTransition?.SetFadeImmediate(1f);

                // A paused game (pause menu) would otherwise stall scaled-time work across
                // the transition.
                PauseSystem.TogglePauseGame(false);

                // 1. Leave the party layer BEFORE touching Netcode. UGS lobby and session
                //    membership is SERVER-side: shutting the transport down does not release
                //    it, so a re-join while still a member is refused ("player is already a
                //    member of the lobby") and HCS never finishes initialising. Order matters -
                //    the leave calls need a live transport to reach UGS, so they go first.
                var hcs = HostConnectionService.Instance;
                if (hcs != null)
                {
                    try
                    {
                        await hcs.ResetPartyLayerAsync().AttachExternalCancellation(ct);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception e)
                    {
                        // Fail-soft: a stale membership costs us the online attempt, not the
                        // switch. The boot chain still runs and still falls back to offline.
                        CSDebug.LogWarning($"[ReconnectService] Party teardown failed: {e.Message} - continuing.");
                    }
                }

                // 2. Tear down whatever host is running - the offline loopback host, or a
                //    half-dead online one. Netcode must be fully reset before the auth scene
                //    tries to bring a Relay host up on top.
                await _networkTransition.ShutdownAsync(SHUTDOWN_TIMEOUT_SECONDS, ct);
                _networkTransition.ClearStaleReferences();

                // 3. Drop the SESSION latch so the boot chain starts from a clean slate and
                //    every offline stand-down (matchmaking, party session creation) re-arms.
                //    Done AFTER shutdown so nothing races to create a Relay session against
                //    the host still going down. This clears what the session IS, never the
                //    player's recorded PREFERENCE - the auth scene reads that next and will
                //    put us straight back offline when it is set.
                _gameData.IsOfflineSession = false;

                // 4. Re-arm auth. Without clearing the success latch a successful sign-in
                //    would not re-raise OnSignedIn, and nothing downstream would start.
                _authFacade?.ResetForReconnect();

                // 5. Hand back to the boot chain. The auth scene signs in, waits for the
                //    Relay host, and loads Menu_Main - or falls back to offline again.
                _appStateMachine?.TransitionTo(ApplicationState.Authenticating);

                string authScene = _sceneNames != null ? _sceneNames.AuthenticationScene : "Authentication";
                CSDebug.Log($"[ReconnectService] Loading {authScene} to re-run the boot chain.");

                if (_sceneTransition != null)
                    await _sceneTransition.LoadSceneAsync(authScene);
                else
                    await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(authScene).ToUniTask();

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[ReconnectService] Reconnect failed: {e.Message}");
                return false;
            }
            finally
            {
                IsReconnecting = false;
                OnReconnectingChanged?.Invoke(false);
            }
        }
    }
}
