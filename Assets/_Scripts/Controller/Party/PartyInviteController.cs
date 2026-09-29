using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thin orchestrator for party invite transitions.  Sequences the Netcode
    /// host→client handoff when a player accepts an invite, and the local-host
    /// restart on leave or failed transition.
    ///
    /// <para>
    /// NM lifecycle mechanics (shutdown, wait-for-connect, wait-for-scene-sync)
    /// live in <see cref="NetworkTransitionService"/>, injected via Reflex DI.
    /// This class owns only the accept/decline/leave orchestration and the
    /// <see cref="_transitioning"/> guard (test-reflected - must stay here).
    /// </para>
    ///
    /// Place on the same persistent GameObject as <see cref="HostConnectionService"/>.
    /// Lifetime: DontDestroyOnLoad MonoBehaviour.
    /// Thread-safety: main-thread only.
    /// </summary>
    public class PartyInviteController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector / Injected fields
        // ─────────────────────────────────────────────────────────────────────

        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Tooltip("Optional. Best-effort toast shown when a join fails and the client bounces back to its own menu. May be suppressed during the scene reload.")]
        [SerializeField] private ToastChannel bounceToastChannel;

        [Header("Timing")]
        [Tooltip("Max time (seconds) to wait for NetworkManager shutdown.")]
        [SerializeField] private float shutdownTimeoutSeconds = 2f;

        [Tooltip("Max time (seconds) to wait for client connection after joining party session.")]
        [SerializeField] private float connectionTimeoutSeconds = 8f;

        [Tooltip("Max seconds to wait for the local player's vessel to initialise (OnClientReady fires) after joining. On timeout the client bounces back to its own solo menu.")]
        [SerializeField] private float joinReadyTimeoutSeconds = 10f;

        [Inject] private GameDataSO gameData;
        [Inject] private SceneTransitionManager _sceneTransitionManager;
        [Inject] private SceneLoader _sceneLoader;
        [Inject] private SceneNameListSO _sceneNames;

        // ─────────────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────────────

        private CancellationTokenSource _cts;

        // _transitioning is reflected by tests - field name must not change.
        private bool _transitioning;

        /// <summary>
        /// True while a host-to-client transition is in progress.
        /// UI should disable invite buttons during this time.
        /// </summary>
        public bool IsTransitioning => _transitioning;

        // ─────────────────────────────────────────────────────────────────────
        // Services (injected via Reflex DI)
        // ─────────────────────────────────────────────────────────────────────

        [Inject] private INetworkTransitionService _networkTransition;

        // ─────────────────────────────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────────────────────────────

        public static PartyInviteController Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // [Inject] fields (_networkTransition, gameData) are populated by Reflex
            // between Awake and Start - do not access them here.
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (Instance == this)
                Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Accept Invite (Recipient Side)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Accept-invite flow:
        ///   1.  Shutdown local NetworkManager host.
        ///   1b. Clear stale SOAP refs the shutdown left behind.
        ///   2.  Join the inviter's party session via UGS (Relay transport auto-configures).
        ///   3.  Wait for Netcode client connection (honored - bounce on failure).
        ///   4.  Gate on OnClientReady (the local vessel initialised). The client-pull
        ///       retry loop in ClientPlayerVesselInitializer drives convergence; this is
        ///       the terminal watchdog. On timeout, bounce back to the player's own solo
        ///       menu so the splash can never stay stuck.
        ///   5.  Raise OnPartyJoinCompleted so Party Area UI refreshes.
        /// </summary>
        public async UniTask AcceptInviteAsync(PartyInviteData invite)
        {
            if (_transitioning)
            {
                CSDebug.LogWarning("[PartyInviteController] Already transitioning - ignoring duplicate accept.");
                return;
            }

            _transitioning = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // TODO(NetworkMonitor.BoostPolling): if a NetworkMonitor.BoostPolling(int s)
            // hook is added (mirroring LobbyRefreshScheduler.Boost()), call it here
            // to tighten reachability polling for the duration of the accept flow.
            // Improves the `sinceChange=...` resolution in any NetDiag log emitted
            // by a catch in this flow. Exception classification works without it.

            // Cover the screen immediately so the user does not see the old menu
            // state during NM shutdown + UGS join + Relay connect.
            // SceneLoader.OnSceneLoaded re-arms this on Menu_Main load; it is
            // idempotent at alpha=1, so calling it here is always safe.
            _sceneTransitionManager?.SetFadeImmediate(1f);

            // Bug B fix: arm the splash fade-out trigger before the join flow starts.
            // SceneLoader only auto-subscribes FadeFromSplashOnReady when Menu_Main
            // loads (via OnSceneLoaded), but accepting an invite does NOT trigger a
            // scene reload on the joining client - Netcode just synchronises
            // NetworkObjects against the host's already-loaded Menu_Main. Without
            // re-arming here, the joining client's OnClientReady raise (after their
            // Player+Vessel pair is initialised) has no subscriber and the splash
            // stays opaque forever.
            _sceneLoader?.ArmSplashFadeOnNextClientReady();

            // Unpause immediately - ScreenSwitcher pauses on non-HOME screens,
            // and the accept flow needs Update() ticking so the UGS SDK's
            // internal lobby state stays synchronized with WebSocket deltas.
            // Without this, LobbyPatcher crashes with ArgumentOutOfRangeException.
            PauseSystem.TogglePauseGame(false);

            try
            {
                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Starting direct-join accept flow");

                // Step 1: Shutdown the local NetworkManager.
                // .AsMainThread() guarantees each cross-thread await resumes on
                // Unity's main thread (see UniTaskExtensions.cs).
                await _networkTransition.ShutdownAsync(shutdownTimeoutSeconds, ct).AsMainThread();

                // Step 1b: Clear stale SOAP references the NM shutdown left behind.
                // Player.OnNetworkDespawn removes from gameData.Players but leaves
                // LocalPlayer and Vessels pointing at destroyed objects.
                _networkTransition.ClearStaleReferences();

                // Step 2: Join the inviter's party session via HostConnectionService.
                if (HostConnectionService.Instance == null)
                {
                    CSDebug.LogError("[PartyInviteController] HostConnectionService not available.");
                    return;
                }

                await HostConnectionService.Instance.AcceptInviteAsync(invite).AsMainThread();

                // gameData.ActiveSession IS HCS.PartySession (single backing field
                // - see Docs/PartySystem/ARCHITECTURE.md Q4). The accept
                // path inside HCS already updated the shared ref via PartySessionService.

                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Joined party session via UGS.");

                // Step 3: Wait for Netcode client connection - HONOR the result.
                // A false here means Netcode never connected (a real failure), so
                // bounce immediately rather than proceeding into a guaranteed hang.
                bool connected = await _networkTransition
                    .WaitForClientConnectionAsync(connectionTimeoutSeconds, ct).AsMainThread();
                if (!connected)
                {
                    CSDebug.LogError("[PartyInviteController] Netcode client never connected - bouncing to solo menu.");
                    await BounceToSoloMenuAsync("Couldn't join - returned to your menu.");
                    return;
                }
                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Netcode client connected.");

                // Step 4: GATE ON THE TRUE SUCCESS SIGNAL.
                // OnClientReady fires from ClientPlayerVesselInitializer.InitializePair
                // once the LOCAL player's vessel is wired. The client-pull retry loop
                // (ClientPlayerVesselInitializer) drives convergence even if the host's
                // one-shot bootstrap RPC was dropped; this is the terminal watchdog.
                // On timeout, bounce back to the player's own solo menu - the splash
                // can never stay stuck.
                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Awaiting client-ready (local vessel initialized)");
                bool ready = await WaitForClientReadyAsync(joinReadyTimeoutSeconds, ct);
                if (!ready)
                {
                    CSDebug.LogError("[PartyInviteController] OnClientReady never fired within budget - bouncing to solo menu.");
                    await BounceToSoloMenuAsync("Couldn't join - returned to your menu.");
                    return;
                }

                // Step 5: success - refresh the joiner's party UI. Isolated try/catch
                // so a listener throwing can't roll the succeeded join into recovery.
                try
                {
                    connectionData.OnPartyJoinCompleted.Raise();
                    HostConnectionService.Instance?.ForceRefreshNow();
                }
                catch (Exception postEx)
                {
                    CSDebug.LogWarning(
                        $"[PartyInviteController] Post-accept signal failed " +
                        $"({postEx.GetType().Name}): {postEx.Message} - " +
                        "accept already succeeded, continuing.");
                }

                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Accept flow completed successfully.");
            }
            catch (OperationCanceledException)
            {
                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Accept flow cancelled.");
            }
            catch (Exception e)
            {
                // Timeout / cancel continuations can land on the thread pool.
                // Yield one frame on PlayerLoop.Update to land on Unity's main thread
                // before touching SOAP / GameObjects in the recovery path.
                await UniTask.Yield(PlayerLoopTiming.Update);
                CSDebug.LogError($"[PartyInviteController] Accept flow failed " +
                               $"({e.GetType().Name}): {e}");
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
                _transitioning = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Direct join / Spectate (no invite)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Spectate")]
        [Tooltip("Max seconds, after the Netcode client connects, to wait for the watched match's " +
                 "scene to sync and its first vessel to bind before the spectator bounces back to " +
                 "its own solo menu. Covers the host-driven scene load plus the roster pull.")]
        [SerializeField] private float spectateReadyTimeoutSeconds = 45f;

        /// <summary>
        /// The online row's JOIN button: join <paramref name="target"/>'s party directly, with no
        /// invite. The same shutdown → join → connect → client-ready → bounce sequence as
        /// <see cref="AcceptInviteAsync"/> (kept as a sibling rather than a refactor of that
        /// hardened path), differing only in the session join it performs
        /// (<see cref="HostConnectionService.JoinPartyDirectAsync"/>).
        /// </summary>
        public UniTask JoinPartyAsync(PartyPlayerData target) =>
            RunClientJoinAsync(
                $"direct-join -> {target.DisplayName}",
                expectLocalVessel: true,
                joinSession: () => HostConnectionService.Instance.JoinPartyDirectAsync(target),
                afterConnected: null);

        /// <summary>
        /// The online row's SPECTATE button: connect to the match <paramref name="target"/> is
        /// playing as a viewer. Arms the spectator approval payload BEFORE the join so the host
        /// mints no Player object for us (<see cref="SpectatorSession"/>), joins the session with
        /// the spectator property (<see cref="HostConnectionService.JoinAsSpectatorAsync"/>), and
        /// then hands over to <see cref="SpectatorController"/>, which owns the camera, the overlay
        /// and the exit. The success gate is "watching a vessel", never OnClientReady - a
        /// spectator has no local vessel, so that event never fires for it.
        /// </summary>
        public UniTask SpectateAsync(PartyPlayerData target) =>
            RunClientJoinAsync(
                $"spectate -> {target.DisplayName}",
                expectLocalVessel: false,
                joinSession: () =>
                {
                    SpectatorSession.BeginLocal(target);
                    return HostConnectionService.Instance.JoinAsSpectatorAsync(target.PartySessionId);
                },
                afterConnected: ct =>
                {
                    var controller = SpectatorController.Begin(target, gameData, _sceneTransitionManager, _sceneNames);
                    return controller.WaitUntilWatchingAsync(spectateReadyTimeoutSeconds, ct);
                });

        /// <summary>
        /// Shared body of the two no-invite joins. Mirrors <see cref="AcceptInviteAsync"/> step
        /// for step; the two flags say what a SUCCESSFUL join looks like (a local vessel, or a
        /// spectator watching one). Every failure lands in the same bounce.
        /// </summary>
        private async UniTask RunClientJoinAsync(
            string label,
            bool expectLocalVessel,
            Func<UniTask> joinSession,
            Func<CancellationToken, UniTask<bool>> afterConnected)
        {
            if (_transitioning)
            {
                CSDebug.LogWarning($"[PartyInviteController] Already transitioning - ignoring {label}.");
                return;
            }

            _transitioning = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _sceneTransitionManager?.SetFadeImmediate(1f);
            // Only a join that ENDS in a local vessel may arm the OnClientReady fade: for a
            // spectator that event never comes, and SpectatorController fades the veil itself
            // once it is watching something.
            if (expectLocalVessel)
                _sceneLoader?.ArmSplashFadeOnNextClientReady();
            PauseSystem.TogglePauseGame(false);

            try
            {
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] Starting {label}");

                await _networkTransition.ShutdownAsync(shutdownTimeoutSeconds, ct).AsMainThread();
                _networkTransition.ClearStaleReferences();

                if (HostConnectionService.Instance == null)
                {
                    CSDebug.LogError("[PartyInviteController] HostConnectionService not available.");
                    await BounceToSoloMenuAsync("Couldn't join - returned to your menu.");
                    return;
                }

                await joinSession().AsMainThread();
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] {label}: joined session via UGS.");

                bool connected = await _networkTransition
                    .WaitForClientConnectionAsync(connectionTimeoutSeconds, ct).AsMainThread();
                if (!connected)
                {
                    CSDebug.LogError($"[PartyInviteController] {label}: Netcode client never connected - bouncing to solo menu.");
                    await BounceToSoloMenuAsync("Couldn't join - returned to your menu.");
                    return;
                }

                bool ready = expectLocalVessel
                    ? await WaitForClientReadyAsync(joinReadyTimeoutSeconds, ct)
                    : afterConnected == null || await afterConnected(ct);
                if (!ready)
                {
                    CSDebug.LogError($"[PartyInviteController] {label}: never became ready within budget - bouncing to solo menu.");
                    await BounceToSoloMenuAsync("Couldn't join - returned to your menu.");
                    return;
                }

                if (expectLocalVessel)
                {
                    try
                    {
                        connectionData.OnPartyJoinCompleted.Raise();
                        HostConnectionService.Instance?.ForceRefreshNow();
                    }
                    catch (Exception postEx)
                    {
                        CSDebug.LogWarning(
                            $"[PartyInviteController] Post-join signal failed " +
                            $"({postEx.GetType().Name}): {postEx.Message} - join already succeeded, continuing.");
                    }
                }

                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] {label} completed successfully.");
            }
            catch (OperationCanceledException)
            {
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] {label} cancelled.");
            }
            catch (Exception e)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                CSDebug.LogError($"[PartyInviteController] {label} failed ({e.GetType().Name}): {e}");
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
                _transitioning = false;
            }
        }

        /// <summary>
        /// Decline the pending invite. Dismisses the popup and clears the invite.
        /// </summary>
        public async UniTask DeclineInviteAsync()
        {
            if (HostConnectionService.Instance != null)
                await HostConnectionService.Instance.DeclineInviteAsync();
        }

        /// <summary>
        /// Client-side "Leave Lobby": disconnects from the host's party session and
        /// returns to Menu_Main, then restarts a local host so the player can send or
        /// accept new invites.
        /// </summary>
        public async UniTask LeavePartyAndReturnToMenuAsync()
        {
            if (_transitioning)
            {
                CSDebug.LogWarning("[PartyInviteController] Already transitioning - ignoring leave lobby.");
                return;
            }

            _transitioning = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // Cover the screen immediately - NM shutdown + solo Relay creation
            // takes 1-3s and the user should not see the frozen lava-lamp state.
            _sceneTransitionManager?.SetFadeImmediate(1f);
            PauseSystem.TogglePauseGame(false);

            try
            {
                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Starting leave-lobby flow");

                // A spectator leaves through this very path (its overlay's close button, the
                // watched match ending). Disarm before the solo session below starts a host.
                SpectatorSession.EndLocal();

                // Mirror cold-boot exactly: tear down vessel/Player/SOAP refs →
                // leave UGS session → shut down NM → load Menu_Main locally (NM is
                // down, so Unity SceneManager, not Netcode) → recreate solo Relay
                // session. EnsurePartySessionAsync auto-starts NM via the UGS SDK
                // and the persistent host Player respawns into the freshly-loaded
                // Menu_Main, where the (also freshly-mounted) scene-placed
                // ServerPlayerVesselInitializer catches it exactly once. One vessel,
                // no orphan, no band-aid. See Docs/PartySystem/BUGS.md B3.b for
                // the architectural rationale this replaces.
                if (gameData != null)
                {
                    gameData.DestroyPlayerAndVessel();
                    gameData.ResetRuntimeData();
                }

                var hcs = HostConnectionService.Instance;
                if (hcs != null)
                    await hcs.LeavePartySessionAsync().AsMainThread();

                await _networkTransition.ShutdownAsync(shutdownTimeoutSeconds, ct).AsMainThread();
                _networkTransition.ClearStaleReferences();

                await SceneManager.LoadSceneAsync(_sceneNames.MainMenuScene, LoadSceneMode.Single)
                    .ToUniTask(cancellationToken: ct);

                if (hcs != null)
                    await hcs.EnsurePartySessionAsync().AsMainThread();

                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Leave-lobby flow completed.");
            }
            catch (OperationCanceledException)
            {
                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Leave-lobby flow cancelled.");
            }
            catch (Exception e)
            {
                // Timeout / cancel continuations can land on the thread pool.
                // Yield onto PlayerLoop.Update to land on Unity's main thread.
                await UniTask.Yield(PlayerLoopTiming.Update);
                CSDebug.LogError($"[PartyInviteController] Leave-lobby flow failed " +
                               $"({e.GetType().Name}): {e}");
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                await RecoverFromFailedTransitionAsync();
            }
            finally
            {
                _transitioning = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Host-side Transition
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Previously transitioned the local host from local-only to Relay-based.
        /// Now a no-op: the Relay-backed party session is created at startup by
        /// <see cref="HostConnectionService"/>, so no transition is needed.
        /// Kept for API compatibility.
        /// </summary>
        public UniTask TransitionToPartyHostAsync()
        {
            if (HostConnectionService.Instance?.PartySession != null)
            {
                CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Party session already active - no transition needed.");
                return UniTask.CompletedTask;
            }

            CSDebug.LogWarning("[PartyInviteController] No party session at invite time - invites may fail.");
            return UniTask.CompletedTask;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal: Error Recovery
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Waits for the true end-to-end join success signal - <see cref="GameDataSO.OnClientReady"/>,
        /// raised once the local player's vessel is initialised. Returns false on timeout
        /// (the caller then bounces to the solo menu). Re-checks the resolved local pair
        /// around the subscribe to close the race where OnClientReady fires first.
        /// </summary>
        private async UniTask<bool> WaitForClientReadyAsync(float timeoutSeconds, CancellationToken ct)
        {
            if (gameData.LocalPlayer?.Vessel != null) return true;

            var tcs = new UniTaskCompletionSource();
            void OnReady() => tcs.TrySetResult();
            gameData.OnClientReady.OnRaised += OnReady;
            try
            {
                if (gameData.LocalPlayer?.Vessel != null) return true; // re-check after subscribe

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                await tcs.Task.AttachExternalCancellation(timeoutCts.Token);
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return false; // budget elapsed without OnClientReady - terminal timeout
            }
            finally
            {
                gameData.OnClientReady.OnRaised -= OnReady;
            }
        }

        /// <summary>
        /// Public entry for HOST-LOSS recovery: a client whose host left/disconnected
        /// (or whose transport failed) bounces to its OWN solo menu + host, exactly like a
        /// failed join. Routed here from <c>MultiplayerSetup.OnClientDisconnect</c> /
        /// <c>OnTransportFailure</c> (genuine per-machine Netcode signals) instead of the
        /// <c>OnSessionEnded → SceneLoader.HandleActiveSessionEnd</c> path, whose
        /// "defer to server" guard waits on the now-dead server and hangs the client.
        /// Works from the lava-lamp menu AND any game scene (recovery always returns to
        /// Menu_Main). Idempotent via <see cref="_transitioning"/> so the
        /// OnClientDisconnect + OnTransportFailure double-fire on a hard host drop runs a
        /// single recovery. See Docs/PartySystem/BUGS.md B10.
        /// </summary>
        public async UniTask HandleHostLossAsync(string reason)
        {
            if (_transitioning)
            {
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] HandleHostLoss ignored - already transitioning ({reason}).");
                return;
            }

            _transitioning = true;
            try
            {
                // Hygiene: clear our stale joined_party presence property (it still points at
                // the dead host's session) so no future host sees a dangling "I'm in your
                // party" claim - the same clear the deliberate Leave path performs.
                // Fire-and-forget: the presence lobby is a separate UGS session that survives
                // the recovery teardown, and B8 fix 1 already makes a stale value inert, so
                // recovery must not block on the write. See Docs/PartySystem/BUGS.md B10.
                HostConnectionService.Instance?.ClearJoinedPartyAsync().Forget();

                await BounceToSoloMenuAsync(reason);
            }
            finally
            {
                _transitioning = false;
            }
        }

        /// <summary>
        /// Terminal "bounce": return the player to their own functional solo menu via
        /// <see cref="RecoverFromFailedTransitionAsync"/>, then surface the notice toast.
        /// Guarantees a failed join / host-loss never ends in a permanent splash hang.
        /// </summary>
        private async UniTask BounceToSoloMenuAsync(string toastMessage)
        {
            CSDebug.LogWarning($"[PartyInviteController] Bouncing to solo menu: {toastMessage}");
            await RecoverFromFailedTransitionAsync();
            // Show the notice AFTER recovery. ToastService is a scene-bound MonoBehaviour
            // (it subscribes to the channel in OnEnable), so it is destroyed + recreated by
            // the Menu_Main reload - and is absent entirely in a game scene. A toast raised
            // before recovery is therefore silently dropped (the channel event has no
            // subscriber). Raising it here lands on the fresh menu's live ToastService.
            // See Docs/PartySystem/BUGS.md B10.
            bounceToastChannel?.ShowPrefix(toastMessage);
        }

        /// <summary>
        /// Restarts the local NetworkManager host so the user returns to a functional
        /// menu state after a failed transition.
        /// </summary>
        private async UniTask RecoverFromFailedTransitionAsync()
        {
            // Recovery may be entered from a thread-pool continuation; land on
            // PlayerLoop.Update to guarantee main thread before touching anything.
            await UniTask.Yield(PlayerLoopTiming.Update);
            CSDebug.LogVerbose(CSLogChannel.Party, "[PartyInviteController] Attempting recovery - recreating solo Relay session");

            // A spectate that failed or lost its host must not leave the approval payload armed:
            // the solo session recreated below would otherwise present it to ITSELF on start.
            SpectatorSession.EndLocal();

            try
            {
                // Failed-transition cleanup: explicitly destroy the local
                // player+vessel left behind by the half-completed accept. The
                // decomposed sequence below then mirrors cold-boot exactly - see
                // LeavePartyAndReturnToMenuAsync for the architectural rationale.
                if (gameData != null)
                {
                    gameData.DestroyPlayerAndVessel();
                    gameData.ResetRuntimeData();
                }

                var hcs = HostConnectionService.Instance;
                if (hcs != null)
                    await hcs.LeavePartySessionAsync().AsMainThread();

                await _networkTransition.ShutdownAsync(shutdownTimeoutSeconds, CancellationToken.None).AsMainThread();
                _networkTransition.ClearStaleReferences();

                await SceneManager.LoadSceneAsync(_sceneNames.MainMenuScene, LoadSceneMode.Single)
                    .ToUniTask();

                if (hcs != null)
                    await hcs.EnsurePartySessionAsync().AsMainThread();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[PartyInviteController] Recovery failed ({e.GetType().Name}): {e}");
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyInviteController] NetDiag: class={CosmicShore.Utility.NetworkDiagnostics.ClassifyException(e)} | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
            }
        }
    }
}
