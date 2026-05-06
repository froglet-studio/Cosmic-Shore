// ─────────────────────────────────────────────────────────────────────────────
// NetworkTransitionService.cs
// Manages NetworkManager lifecycle during party invite transitions.
//
// WHY this class exists:
//   PartyInviteController previously contained both the orchestration logic
//   (what to do when accepting an invite) and the low-level Netcode mechanics
//   (how to shut down NM, how to wait for client connection, how to detect
//   scene sync).  Extracting the "how" here makes each piece independently
//   testable without spinning up a full Netcode environment, and removes three
//   duplicated inline shutdown patterns (one in PartyInviteController, one in
//   HostConnectionService.CreatePartySessionCoreAsync).
//
// KEY CONSTRAINT: this service only deals with NetworkManager lifecycle.
//   It does NOT create UGS sessions, send invites, or update SOAP state.
//   Those are IPartySessionService's and HostConnectionService's jobs.
//
// LIFETIME:
//   Pure C# — no MonoBehaviour.  Instantiated as a field on
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
    /// Lifetime: pure C# — no MonoBehaviour.  Created as a field on
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
            if (nm == null || !nm.IsListening)
            {
                Debug.Log("[NetworkTransitionService] NetworkManager not running — skipping shutdown.");
                return true;
            }

            Debug.Log("[NetworkTransitionService] Shutting down NetworkManager...");
            nm.Shutdown();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await UniTask.WaitUntil(
                    () => nm == null || !nm.IsListening,
                    cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning(
                    $"[NetworkTransitionService] NetworkManager shutdown timed out after {timeoutSeconds}s — forcing.");
                return false;
            }

            // Brief settle delay for transport cleanup.  Transport cleanup is
            // effectively instant once NetworkManager.IsListening flips false;
            // we only need enough time for any queued send buffers to drain
            // before we open a new Relay client on top.
            await UniTask.Delay(50, DelayType.UnscaledDeltaTime, cancellationToken: ct);
            Debug.Log("[NetworkTransitionService] NetworkManager shutdown complete.");
            return true;
        }

        /// <inheritdoc/>
        public async UniTask<bool> WaitForClientConnectionAsync(float timeoutSeconds, CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await UniTask.WaitUntil(
                    () =>
                    {
                        var nm = NetworkManager.Singleton;
                        return nm != null && nm.IsConnectedClient;
                    },
                    cancellationToken: timeoutCts.Token);
                Debug.Log("[NetworkTransitionService] Netcode client connected.");
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning(
                    $"[NetworkTransitionService] Client connection not confirmed after {timeoutSeconds}s — proceeding anyway.");
                return false;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Waits for the first Single-mode Netcode scene-load event after the
        /// client connects.  This is the host-driven Menu_Main reload that happens
        /// automatically because the client's scene handle differs from the host's
        /// (ClientSynchronizationMode = Single).  The <paramref name="sceneName"/>
        /// parameter is used for logging only — any Single-mode load is accepted
        /// because Netcode may reload a scene with a different internal handle
        /// while keeping the same name visible in the event.
        ///
        /// Fail-soft: if nothing fires within <paramref name="timeoutSeconds"/>,
        /// logs a warning and returns <c>false</c> — edge cases exist where Netcode
        /// decides scenes already match and skips the reload entirely.
        /// </remarks>
        public async UniTask<bool> WaitForSceneSyncAsync(
            string sceneName, float timeoutSeconds, CancellationToken ct)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null)
            {
                Debug.LogWarning("[NetworkTransitionService] No SceneManager — skipping scene-sync wait.");
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
                    $"[NetworkTransitionService] Scene-sync not observed in {timeoutSeconds}s — " +
                    "proceeding (host may not have triggered a reload).");
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
    }
}
