// ─────────────────────────────────────────────────────────────────────────────
// IInviteService.cs
// Contract for building, tracking, serialising, and parsing invite payloads.
//
// WHY a separate service?
//   Invite payload management is a self-contained concern: building the
//   composite string that gets stored in a lobby player property, tracking
//   which players have outstanding invites, serializing/deserializing the
//   INVITE_LINE_SEPARATOR-delimited format, and expiring timed-out invites.
//   Extracting it here makes the format independently testable without
//   spinning up a UGS lobby.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds, tracks, serialises, and parses outgoing invite payloads.
    /// Implementors own the in-memory map of targetPlayerId → payload string and
    /// the serialised composite property value written to the lobby.
    ///
    /// Lifetime: extracted from <see cref="HostConnectionService"/> in Phase 5.
    /// Implemented by <c>InviteService</c> in the Services folder.
    /// </summary>
    public interface IInviteService
    {
        /// <summary>
        /// Number of currently tracked outgoing invites.
        /// Zero means no one has been invited (or all invites have timed out).
        /// </summary>
        int OutgoingCount { get; }

        /// <summary>
        /// Read-only set of player IDs that have been invited and not yet resolved.
        /// </summary>
        IReadOnlyCollection<string> OutgoingTargets { get; }

        /// <summary>
        /// True if the given player ID is currently in the outgoing invite list.
        /// </summary>
        bool Contains(string targetPlayerId);

        /// <summary>
        /// Builds and stores an outgoing invite payload for <paramref name="targetPlayerId"/>.
        /// If the player is already invited, refreshes the timeout instead of adding a duplicate.
        /// </summary>
        /// <param name="targetPlayerId">UGS player ID of the recipient.</param>
        /// <param name="sessionId">
        /// The party session ID to embed.  Pass <c>"PENDING"</c> before the Relay
        /// session exists; call <see cref="UpdatePayloadsWithRealSessionId"/> once
        /// the real ID is known.
        /// </param>
        /// <param name="localPlayerId">Sender's UGS player ID.</param>
        /// <param name="localDisplayName">Sender's display name (stored in payload).</param>
        /// <param name="localAvatarId">Sender's avatar ID (stored in payload).</param>
        /// <param name="expiresAtUnscaledTime">
        /// When this invite should be considered expired (<c>Time.unscaledTime</c> basis).
        /// </param>
        void AddOrRefresh(
            string targetPlayerId,
            string sessionId,
            string localPlayerId,
            string localDisplayName,
            int    localAvatarId,
            float  expiresAtUnscaledTime);

        /// <summary>
        /// Removes the expired invite for <paramref name="targetPlayerId"/>
        /// from the tracking map.
        /// </summary>
        void Remove(string targetPlayerId);

        /// <summary>
        /// Refreshes only the expiry time for an already-tracked invite without
        /// changing the payload content.  Used for idempotent re-clicks on an
        /// already-invited player.
        /// </summary>
        void RefreshTimeout(string targetPlayerId, float newExpiresAtUnscaledTime);

        /// <summary>
        /// Replaces the <c>"PENDING"</c> sentinel in all outgoing payloads with the
        /// real Relay session ID, once the host has created the session.
        /// </summary>
        /// <param name="realSessionId">The live UGS session ID (not "PENDING").</param>
        /// <returns>
        /// How many entries were actually patched. Under the eager per-user Relay design every
        /// invite is sent with a real id already, so this is normally 0 - and a 0 means there is
        /// nothing to republish, which the caller must honour rather than writing the unchanged
        /// composite back to the lobby on every refresh tick.
        /// </returns>
        int UpdatePayloadsWithRealSessionId(string realSessionId);

        /// <summary>
        /// Serialises all outgoing invites into a single newline-delimited string
        /// suitable for storage in a lobby player property.
        /// </summary>
        string SerializeAll();

        /// <summary>
        /// Removes all invites whose expiry time has passed and returns the
        /// player IDs that were removed, so callers can perform side effects
        /// (UI notification, lobby property republish) for each one.
        /// Returns an empty list if nothing expired.
        /// </summary>
        System.Collections.Generic.IReadOnlyList<string> RemoveExpired();
    }
}
