// ─────────────────────────────────────────────────────────────────────────────
// INetworkTransitionService.cs
// Contract for NetworkManager lifecycle transitions during party operations.
//
// WHY a separate service?
//   PartyInviteController currently contains both the orchestration logic
//   (what to do when accepting an invite) and the low-level Netcode mechanics
//   (how to shut down NM, how to wait for client connection, how to detect
//   scene sync).  Extracting the "how" into this service makes each piece
//   independently testable without spinning up a full Netcode environment.
//
// KEY CONSTRAINT: this service only deals with NetworkManager lifecycle.
//   It does NOT create UGS sessions, send invites, or update SOAP state.
// ─────────────────────────────────────────────────────────────────────────────

using System.Threading;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages <c>NetworkManager</c> lifecycle during party invite transitions:
    /// shutdown, wait-for-connect, wait-for-scene-sync, and stale-reference cleanup.
    ///
    /// Lifetime: extracted from <see cref="PartyInviteController"/> in Phase 11.
    /// Implemented by <c>NetworkTransitionService</c> in the Services folder.
    /// </summary>
    public interface INetworkTransitionService
    {
        /// <summary>
        /// Shuts down the active <c>NetworkManager</c> and waits for it to fully
        /// stop (i.e. <c>IsListening == false</c>).
        /// </summary>
        /// <param name="timeoutSeconds">
        /// Maximum seconds to wait for shutdown.  Returns false if exceeded.
        /// </param>
        /// <param name="ct">Cancellation token - cancels the wait if triggered.</param>
        /// <returns>
        /// <c>true</c> if shutdown completed within the timeout; <c>false</c> if
        /// it timed out or NM was not running.
        /// </returns>
        UniTask<bool> ShutdownAsync(float timeoutSeconds, CancellationToken ct);

        /// <summary>
        /// Waits until the <c>NetworkManager</c> reports <c>IsConnectedClient == true</c>
        /// or the timeout expires.
        /// </summary>
        /// <param name="timeoutSeconds">
        /// Maximum seconds to wait for the client to connect.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// <c>true</c> if connected within the timeout; <c>false</c> otherwise.
        /// </returns>
        UniTask<bool> WaitForClientConnectionAsync(float timeoutSeconds, CancellationToken ct);

        /// <summary>
        /// Waits until the Netcode scene manager loads a scene matching
        /// <paramref name="sceneName"/> or until the timeout expires.
        /// Used to confirm that the host's <c>nm.SceneManager.LoadScene</c>
        /// has replicated to this client.
        /// </summary>
        /// <param name="sceneName">
        /// The scene name to wait for (typically <c>"Menu_Main"</c>).
        /// </param>
        /// <param name="timeoutSeconds">Maximum seconds to wait.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// <c>true</c> if the scene loaded within the timeout; <c>false</c> otherwise.
        /// </returns>
        UniTask<bool> WaitForSceneSyncAsync(string sceneName, float timeoutSeconds, CancellationToken ct);

        /// <summary>
        /// Clears stale <c>GameDataSO</c> references left over from a prior
        /// Netcode session: <c>LocalPlayer</c>, <c>Players</c>, and <c>Vessels</c>.
        /// Must be called before starting a new host or client to prevent the old
        /// vessel from persisting in the game data.
        /// </summary>
        void ClearStaleReferences();
    }
}
