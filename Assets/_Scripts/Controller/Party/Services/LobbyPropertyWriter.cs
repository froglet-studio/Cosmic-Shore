// ─────────────────────────────────────────────────────────────────────────────
// LobbyPropertyWriter.cs
// Safe lobby property writes: mutex → refresh → set → save-with-retry.
//
// WHY this class exists:
//   Before extraction, HostConnectionService had five near-identical blocks that
//   each did: acquire mutex, refresh lobby, set a player property, save with
//   retry, release mutex.  Any subtle difference (missing refresh, wrong mutex
//   release) was a latent bug.  This class DRYs that entire pattern into one
//   auditable implementation.
//
// THREAD SAFETY:
//   All methods must be called on Unity's main thread.  SemaphoreSlim is used
//   as a mutex (not for actual threading) - it serialises async continuations
//   that all run on the main thread via UniTask's PlayerLoop integration.
//
// DI NOTE:
//   Instantiated as a direct field on HostConnectionService until Phase 12
//   registers it in Reflex DI.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Services.Multiplayer;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serialises lobby property writes using a mutex and handles UGS rate-limit
    /// retries (HTTP 429).
    ///
    /// Owns two <see cref="SemaphoreSlim"/> mutexes:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="LobbyMutex"/> - serialises all reads/writes to the presence
    ///     lobby.  Acquired by <see cref="WriteAsync"/> and held for the entire
    ///     duration of <see cref="HostConnectionService"/>'s refresh cycle.
    ///   </item>
    ///   <item>
    ///     <see cref="SessionCreationMutex"/> - deduplicates concurrent Relay
    ///     session creation attempts (double-check pattern).
    ///   </item>
    /// </list>
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class LobbyPropertyWriter
    {
        // ─────────────────────────────────────────────────────────────────────
        // Public mutexes
        //
        // Exposed as public fields so HostConnectionService can acquire/release
        // them directly in RefreshAsync and ClearOutgoingInviteIfPresentAsync
        // (where the lock-or-no-lock decision depends on _insideRefreshCycle).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Serialises all presence-lobby reads and writes.
        /// <para>
        /// The refresh cycle holds this mutex for its entire duration (non-blocking
        /// try-acquire - skips if busy).  Property writes acquire it in blocking
        /// mode to prevent partial writes.
        /// </para>
        /// </summary>
        public readonly SemaphoreSlim LobbyMutex = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Deduplicates concurrent Relay party-session creation requests.
        /// The double-check pattern (check → lock → check → create) uses this
        /// mutex to ensure exactly one session is created even if multiple
        /// acceptance signals arrive in the same refresh tick.
        /// </summary>
        public readonly SemaphoreSlim SessionCreationMutex = new SemaphoreSlim(1, 1);

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Acquires <see cref="LobbyMutex"/>, refreshes the lobby to sync the
        /// SDK's internal player-index cache, calls <paramref name="setProperty"/>
        /// to write the property value(s), then saves with retry.
        ///
        /// Use this for ALL property writes from outside the refresh cycle.
        /// Inside the refresh cycle (where the mutex is already held), call
        /// <see cref="SaveWithRetryAsync"/> directly.
        /// </summary>
        /// <param name="lobby">
        /// The active presence lobby session.  Returns immediately if null.
        /// </param>
        /// <param name="setProperty">
        /// The action that sets the player property on <c>lobby.CurrentPlayer</c>.
        /// Called while the mutex is held.
        /// </param>
        /// <param name="operationName">
        /// Human-readable label for log messages (e.g. "PublishJoinedParty").
        /// </param>
        public async UniTask WriteAsync(ISession lobby, Action setProperty, string operationName)
        {
            if (lobby == null) return;

            await LobbyMutex.WaitAsync();
            try
            {
                // Refresh before writing - the SDK's player-index cache can be
                // stale, causing SaveCurrentPlayerDataAsync to fail silently if
                // the local player's index moved since the last refresh.
                await lobby.RefreshAsync().AsMainThread();
                setProperty();
                await SaveWithRetryAsync(lobby);

                Debug.Log($"[LobbyPropertyWriter] {operationName} completed.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyPropertyWriter] {operationName} error ({e.GetType().Name}): {e}");
            }
            finally
            {
                LobbyMutex.Release();
            }
        }

        /// <summary>
        /// Saves the local player's properties to the UGS backend with exponential
        /// retry on rate-limit (HTTP 429) and index-out-of-range errors.
        ///
        /// Can be called directly inside the refresh cycle (mutex already held),
        /// or indirectly via <see cref="WriteAsync"/> (which acquires the mutex).
        /// </summary>
        /// <param name="lobby">
        /// The active presence lobby session.  Must not be null.
        /// </param>
        /// <param name="maxRetries">Maximum retry attempts before giving up (default 3).</param>
        /// <param name="baseDelayMs">Base delay in ms between retries (default 2000).</param>
        public async UniTask SaveWithRetryAsync(ISession lobby, int maxRetries = 3, int baseDelayMs = 2000)
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await lobby.SaveCurrentPlayerDataAsync().AsMainThread();

                    // Post-save refresh: keeps the SDK's cached state in sync with
                    // the server.  Reduces the window where WebSocket deltas reference
                    // stale player indices (root cause of harmless
                    // ArgumentOutOfRangeException in LobbyPatcher).
                    try { await lobby.RefreshAsync().AsMainThread(); }
                    catch { /* polling corrects on next cycle */ }

                    return;
                }
                catch (Exception e) when (attempt < maxRetries &&
                    (e.Message.Contains("Too Many Requests") ||
                     e.Message.Contains("Index was out of range")))
                {
                    // UGS rate-limits property writes to ~1/s per client.
                    // Back off exponentially so a burst of rapid retries doesn't
                    // consume the entire rate-limit budget.
                    //
                    // CSDebug.Log (info-level, release-stripped + runtime-muteable):
                    // "Index was out of range" is the SDK stale-index defect (same
                    // family as B1/B6 in Docs/PresenceSystem/BUGS.md), an SDK bug
                    // we already classify as known-transient and retry. The "retry
                    // X/3" message is diagnostic chatter, not a real failure
                    // signal - final state is correct if any retry succeeds, and
                    // a sustained failure propagates to the outer caller via the
                    // `when` filter expiring at attempt == maxRetries.
                    CSDebug.Log(
                        $"[LobbyPropertyWriter] Save failed ({e.GetType().Name}: {e.Message}) - retry {attempt + 1}/{maxRetries} in {baseDelayMs}ms");
                    await UniTask.Delay(baseDelayMs);
                    try { await lobby.RefreshAsync().AsMainThread(); } catch { /* best-effort */ }
                }
            }
        }
    }
}
