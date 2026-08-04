// ─────────────────────────────────────────────────────────────────────────────
// NetworkTransitionService.cs
// Manages NetworkManager lifecycle during party invite transitions.
//
// WHY this class exists:
//   PartyInviteController previously contained both the orchestration logic
//   (what to do when accepting an invite) and the low-level Netcode mechanics
//   (how to shut down NM, how to wait for client connection, how to detect
//   scene sync).  Extracting the "how" here makes each piece independently
//   testable without spinning up a full Netcode environment, and removes the
//   duplicated inline shutdown patterns that previously lived in
//   PartyInviteController and HostConnectionService.
//
// KEY CONSTRAINT: this service only deals with NetworkManager lifecycle.
//   It does NOT create UGS sessions, send invites, or update SOAP state.
//   Those are IPartySessionService's and HostConnectionService's jobs.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Instantiated as a field on
//   PartyInviteController for Phase 11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only.  WaitForClientConnectionAsync and WaitForSceneSyncAsync
//   use UniTask.WaitUntil / TaskCompletionSource, both of which require
//   Unity's PlayerLoop on the main thread.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages <c>NetworkManager</c> lifecycle during party invite transitions:
    /// shutdown, wait-for-connect, wait-for-scene-sync, and stale-reference cleanup.
    ///
    /// <para>
    /// All three wait methods are fail-soft: they log a warning and return
    /// <c>false</c> on timeout rather than throwing, so the caller's flow can
    /// decide whether to proceed or abort.
    /// </para>
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="PartyInviteController"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class NetworkTransitionService : INetworkTransitionService
    {
        // ─────────────────────────────────────────────────────────────────────
        // Dependencies
        // ─────────────────────────────────────────────────────────────────────

        private readonly GameDataSO _gameData;

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Creates the network transition service.</summary>
        /// <param name="gameData">
        /// Runtime game data container.  Used only by <see cref="ClearStaleReferences"/>
        /// to reset player/vessel refs after a transport swap.
        /// </param>
        public NetworkTransitionService(GameDataSO gameData)
        {
            _gameData = gameData;
        }

        // ─────────────────────────────────────────────────────────────────────
        // INetworkTransitionService
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async UniTask<bool> ShutdownAsync(float timeoutSeconds, CancellationToken ct)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || IsFullyReset(nm))
            {
                Debug.Log("[NetworkTransitionService] NetworkManager not running - skipping shutdown.");
                return true;
            }

            LogNetworkState(nm, "before Shutdown");
            Debug.Log("[NetworkTransitionService] Shutting down NetworkManager...");
            nm.Shutdown();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                // STRONG reset gate. !IsListening flipping false does NOT mean NGO has
                // finished its teardown - the transport and the Multiplayer SDK's
                // network handler can still be detaching. Wait until the NM reports
                // fully idle (not listening, not serving, not a client, no shutdown in
                // progress) so the subsequent client-start (JoinSessionByIdAsync) cannot
                // race a half-reset NetworkManager. That race is the intermittent
                // party-join bounce - see Docs/PartySystem/ARCHITECTURE.md.
                await UniTask.WaitUntil(
                    () =>
                    {
                        var n = NetworkManager.Singleton;
                        return n == null || IsFullyReset(n);
                    },
                    cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning(
                    $"[NetworkTransitionService] NetworkManager shutdown timed out after {timeoutSeconds}s - forcing.");
                LogNetworkState(NetworkManager.Singleton, "after shutdown timeout");
                CosmicShore.Utility.CSDebug.Log($"[NetworkTransitionService] NetDiag: class=Timeout | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                return false;
            }

            // One PlayerLoop tick so any teardown work queued for end-of-frame
            // (transport dispose, SDK handler unbind) completes before a new client is
            // started on top. This is frame sequencing, not thread marshaling.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            LogNetworkState(NetworkManager.Singleton, "after Shutdown settled");
            Debug.Log("[NetworkTransitionService] NetworkManager shutdown complete.");
            return true;
        }

        /// <summary>
        /// True when the NetworkManager has fully torn down: not listening, not acting
        /// as server or client, and not mid-shutdown. A stronger signal than
        /// <c>!IsListening</c> alone - used to gate the host→client transition so a
        /// client-start cannot race an in-progress shutdown.
        /// </summary>
        private static bool IsFullyReset(NetworkManager nm) =>
            !nm.IsListening && !nm.IsServer && !nm.IsClient && !nm.ShutdownInProgress;

        /// <inheritdoc/>
        public async UniTask<bool> WaitForClientConnectionAsync(float timeoutSeconds, CancellationToken ct)
        {
            var nm = NetworkManager.Singleton;
            LogNetworkState(nm, "before wait-for-connect");

            // TEMP DIAGNOSTICS (party-join race): capture why the client fails to bind.
            // DisconnectReason is the key signal - it distinguishes a local transport
            // race from a Relay allocation-propagation drop on the host side.
            void OnConnected(ulong clientId) =>
                Debug.Log($"[NetTransition][diag] OnClientConnected clientId={clientId}");
            void OnDisconnected(ulong clientId) =>
                Debug.LogWarning(
                    $"[NetTransition][diag] OnClientDisconnect clientId={clientId} " +
                    $"reason='{NetworkManager.Singleton?.DisconnectReason}'");
            if (nm != null)
            {
                nm.OnClientConnectedCallback  += OnConnected;
                nm.OnClientDisconnectCallback += OnDisconnected;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await UniTask.WaitUntil(
                    () =>
                    {
                        var n = NetworkManager.Singleton;
                        return n != null && n.IsConnectedClient;
                    },
                    cancellationToken: timeoutCts.Token);
                Debug.Log("[NetworkTransitionService] Netcode client connected.");
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning(
                    $"[NetworkTransitionService] Client connection not confirmed after {timeoutSeconds}s - proceeding anyway.");
                LogNetworkState(NetworkManager.Singleton, "after connect timeout");
                CosmicShore.Utility.CSDebug.Log($"[NetworkTransitionService] NetDiag: class=Timeout | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                return false;
            }
            finally
            {
                var n = NetworkManager.Singleton;
                if (n != null)
                {
                    n.OnClientConnectedCallback  -= OnConnected;
                    n.OnClientDisconnectCallback -= OnDisconnected;
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Waits for the first Single-mode Netcode scene-load event after the
        /// client connects.  This is the host-driven Menu_Main reload that happens
        /// automatically because the client's scene handle differs from the host's
        /// (ClientSynchronizationMode = Single).  The <paramref name="sceneName"/>
        /// parameter is used for logging only - any Single-mode load is accepted
        /// because Netcode may reload a scene with a different internal handle
        /// while keeping the same name visible in the event.
        ///
        /// Fail-soft: if nothing fires within <paramref name="timeoutSeconds"/>,
        /// logs a warning and returns <c>false</c> - edge cases exist where Netcode
        /// decides scenes already match and skips the reload entirely.
        /// </remarks>
        public async UniTask<bool> WaitForSceneSyncAsync(
            string sceneName, float timeoutSeconds, CancellationToken ct)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null)
            {
                Debug.LogWarning("[NetworkTransitionService] No SceneManager - skipping scene-sync wait.");
                return false;
            }

            var tcs = new UniTaskCompletionSource<string>();
            void Handler(string loadedScene, LoadSceneMode mode,
                         List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
            {
                if (mode == LoadSceneMode.Single)
                    tcs.TrySetResult(loadedScene);
            }
            nm.SceneManager.OnLoadEventCompleted += Handler;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                var loaded = await tcs.Task.AttachExternalCancellation(timeoutCts.Token);
                Debug.Log($"[NetworkTransitionService] Client scene-sync completed: {loaded}");
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning(
                    $"[NetworkTransitionService] Scene-sync not observed in {timeoutSeconds}s - " +
                    "proceeding (host may not have triggered a reload).");
                CosmicShore.Utility.CSDebug.Log($"[NetworkTransitionService] NetDiag: class=Timeout | {CosmicShore.Utility.NetworkDiagnostics.GetSnapshot()}");
                return false;
            }
            finally
            {
                var nmNow = NetworkManager.Singleton;
                if (nmNow != null && nmNow.SceneManager != null)
                    nmNow.SceneManager.OnLoadEventCompleted -= Handler;
            }
        }

        /// <inheritdoc/>
        public void ClearStaleReferences()
        {
            if (_gameData == null) return;
            _gameData.ResetRuntimeDataForPartyJoin();
            Debug.Log("[NetworkTransitionService] Cleared stale runtime references for party join.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEMP DIAGNOSTICS (party-join race) - remove once root-caused.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Snapshots NetworkManager + transport state at a labelled phase of the
        /// host→client transition. Temporary instrumentation for the intermittent
        /// party-join bounce; compare a failing run against a passing run to pin
        /// which race fired (NM-reset vs SDK-handler-detach vs Relay-propagation).
        /// </summary>
        private static void LogNetworkState(NetworkManager nm, string phase)
        {
            if (nm == null)
            {
                Debug.Log($"[NetTransition][diag] {phase}: NetworkManager == null");
                return;
            }

            string transport = nm.NetworkConfig != null && nm.NetworkConfig.NetworkTransport != null
                ? nm.NetworkConfig.NetworkTransport.GetType().Name
                : "null";
            Debug.Log(
                $"[NetTransition][diag] {phase}: IsListening={nm.IsListening} IsServer={nm.IsServer} " +
                $"IsClient={nm.IsClient} IsConnectedClient={nm.IsConnectedClient} " +
                $"ShutdownInProgress={nm.ShutdownInProgress} transport={transport}");
        }
    }
}
