// ─────────────────────────────────────────────────────────────────────────────
// IPresenceLobbyService.cs
// Contract for the presence lobby — the global player-discovery session.
//
// KEY CONSTRAINT: implementors must NOT start a NetworkManager or create a
// Relay allocation.  The presence lobby is a lobby-only UGS session that
// coexists safely with any active NetworkManager state.  Relay is
// IPartySessionService's responsibility.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages the UGS lobby-only session used for player discovery and invite
    /// property exchange.  No Relay, no NetworkManager interaction.
    ///
    /// Lifetime: extracted from <see cref="HostConnectionService"/> in Phase 7.
    /// Implemented by <c>PresenceLobbyService</c> in the Services folder.
    /// </summary>
    public interface IPresenceLobbyService
    {
        /// <summary>
        /// The active presence lobby session, or <c>null</c> if not yet joined.
        /// </summary>
        ISession ActiveLobby { get; }

        /// <summary>
        /// Joins an existing presence lobby or creates one if none exists.
        /// Safe to call multiple times — returns early if already joined.
        /// </summary>
        /// <param name="maxPlayers">
        /// Maximum simultaneous players in the global lobby (typically 100).
        /// </param>
        UniTask JoinOrCreateAsync(int maxPlayers);

        /// <summary>
        /// Leaves the presence lobby gracefully (delete if host, leave if client).
        /// Safe to call even if not currently in a lobby — returns immediately.
        /// </summary>
        UniTask LeaveAsync();

        /// <summary>
        /// Refreshes the lobby's player list and properties from the UGS backend.
        /// Must be called on a timer to detect new players and incoming invites.
        /// </summary>
        /// <remarks>
        /// Rate-limited by UGS to approximately 1 call per second per client.
        /// The refresh scheduler (LobbyRefreshScheduler) manages the interval.
        /// </remarks>
        UniTask RefreshAsync();

        /// <summary>
        /// Writes the given set of player properties to the local player's lobby
        /// record.  Acquires the lobby mutex internally — do NOT call while already
        /// holding the mutex.
        /// </summary>
        /// <param name="properties">
        /// Key-value pairs to write (e.g. displayName, invite_payloads).
        /// </param>
        /// <param name="operationName">
        /// Human-readable name for this write — appears in log output for diagnostics.
        /// </param>
        UniTask SavePropertiesAsync(
            Dictionary<string, PlayerProperty> properties,
            string operationName);
    }
}
