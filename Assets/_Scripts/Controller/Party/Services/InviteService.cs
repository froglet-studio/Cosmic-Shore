// ─────────────────────────────────────────────────────────────────────────────
// InviteService.cs
// Builds, tracks, serialises, and parses outgoing invite payloads.
//
// WHY this class exists:
//   Before extraction, all invite payload management lived scattered across
//   HostConnectionService: the OutgoingInviteTracker inner class, BuildInvitePayload,
//   PublishInvitePayloadsToCurrentPlayer, and ExpireOutgoingInvites.  Every call
//   site needed to know the payload format, separator characters, and timeout
//   constant.  Centralising these in one place means:
//     1. The invite line format is defined and validated in one place.
//     2. Timeout and separator constants don't leak into orchestration code.
//     3. ParseLine (internal static) can be tested independently without
//        spinning up a UGS lobby.
//
// PENDING PROTOCOL:
//   When the local player sends an invite before a Relay session exists (the
//   lazy-creation model), the session id field carries the sentinel string
//   "PENDING".  Once the host creates the Relay session, all outgoing payloads
//   are patched via UpdatePayloadsWithRealSessionId.  Recipients poll and
//   retry until the real id appears.
//
// PAYLOAD FORMAT (per invite line):
//   targetPlayerId|localPlayerId|sessionId|localDisplayName|localAvatarId
//   Fields are separated by '|'.  Multiple invites are joined by '\n'.
//
// LIFETIME:
//   Pure C# - no MonoBehaviour.  Instantiated as a field on
//   HostConnectionService for Phases 5-11.  Phase 12 registers it in Reflex DI.
//
// THREAD SAFETY:
//   Main-thread only.  All public methods must be called from Unity's main thread.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds, tracks, serialises, and parses outgoing invite payloads.
    ///
    /// Owns the in-memory dictionary of targetPlayerId → invite entry and the
    /// serialised composite property value that gets written to the lobby player.
    ///
    /// <para>
    /// Does NOT write to the UGS lobby - that is
    /// <see cref="HostConnectionService"/>'s responsibility via
    /// <see cref="LobbyPropertyWriter"/>.
    /// </para>
    ///
    /// Lifetime: pure C# - no MonoBehaviour.  Created as a field on
    /// <see cref="HostConnectionService"/>; will be DI-registered in Phase 12.
    /// Thread-safety: main-thread only.
    /// </summary>
    public sealed class InviteService : IInviteService
    {
        // ─────────────────────────────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Placeholder session id written into payloads before the host's Relay
        /// session is created.  Replaced by the real id via
        /// <see cref="UpdatePayloadsWithRealSessionId"/> once the session is live.
        /// </summary>
        internal const string PENDING_SESSION_ID = PartyLobbyKeys.PendingSessionId;

        /// <summary>
        /// Separator between the fields of one invite line.
        /// Format: targetId|senderPlayerId|sessionId|senderDisplayName|senderAvatarId
        /// </summary>
        internal const char FIELD_SEPARATOR = '|';

        /// <summary>Separator between multiple invite lines in the composite property.</summary>
        internal const char LINE_SEPARATOR = '\n';

        // ─────────────────────────────────────────────────────────────────────
        // Private state
        // ─────────────────────────────────────────────────────────────────────

        private sealed class Entry
        {
            /// <summary>Pre-built serialised invite line (targetId|senderPlayerId|sessionId|...).</summary>
            public string Payload;
            /// <summary>Stored separately so UpdatePayloadsWithRealSessionId can patch it.</summary>
            public string SessionId;
            public float  ExpiresAt;
        }

        private readonly Dictionary<string, Entry> _entries = new();

        // ─────────────────────────────────────────────────────────────────────
        // IInviteService - query properties
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public int OutgoingCount => _entries.Count;

        /// <inheritdoc/>
        public IReadOnlyCollection<string> OutgoingTargets => _entries.Keys;

        /// <inheritdoc/>
        public bool Contains(string targetPlayerId) => _entries.ContainsKey(targetPlayerId);

        // ─────────────────────────────────────────────────────────────────────
        // IInviteService - mutation
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Builds the payload string internally.  The format is:
        /// <c>targetPlayerId|localPlayerId|sessionId|localDisplayName|localAvatarId</c>
        /// If the player is already tracked, the payload and expiry are refreshed
        /// in-place without creating a new entry.
        /// </remarks>
        public void AddOrRefresh(
            string targetPlayerId,
            string sessionId,
            string localPlayerId,
            string localDisplayName,
            int    localAvatarId,
            float  expiresAtUnscaledTime)
        {
            string payload = BuildPayload(targetPlayerId, sessionId, localPlayerId, localDisplayName, localAvatarId);

            if (_entries.TryGetValue(targetPlayerId, out var existing))
            {
                existing.Payload   = payload;
                existing.SessionId = sessionId;
                existing.ExpiresAt = expiresAtUnscaledTime;
            }
            else
            {
                _entries[targetPlayerId] = new Entry
                {
                    Payload   = payload,
                    SessionId = sessionId,
                    ExpiresAt = expiresAtUnscaledTime,
                };
            }

            Debug.Log($"[InviteService] AddOrRefresh → target={targetPlayerId}, sessionId={sessionId}, total={OutgoingCount}");
        }

        /// <inheritdoc/>
        public void Remove(string targetPlayerId)
        {
            if (_entries.Remove(targetPlayerId))
                Debug.Log($"[InviteService] Remove → {targetPlayerId}, remaining={OutgoingCount}");
        }

        /// <inheritdoc/>
        public void RefreshTimeout(string targetPlayerId, float newExpiresAtUnscaledTime)
        {
            if (_entries.TryGetValue(targetPlayerId, out var entry))
            {
                entry.ExpiresAt = newExpiresAtUnscaledTime;
                Debug.Log($"[InviteService] RefreshTimeout → {targetPlayerId}");
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Patches only entries whose SessionId is <see cref="PENDING_SESSION_ID"/>.
        /// The payload string is updated in-place by replacing the PENDING sentinel
        /// with the real session id.
        /// </remarks>
        public void UpdatePayloadsWithRealSessionId(string realSessionId)
        {
            int patched = 0;
            foreach (var entry in _entries.Values)
            {
                if (entry.SessionId != PENDING_SESSION_ID) continue;
                entry.Payload   = entry.Payload.Replace(PENDING_SESSION_ID, realSessionId);
                entry.SessionId = realSessionId;
                patched++;
            }
            Debug.Log($"[InviteService] UpdatePayloadsWithRealSessionId → patched {patched}/{OutgoingCount} entries with {realSessionId}");
        }

        /// <inheritdoc/>
        public string SerializeAll()
        {
            if (_entries.Count == 0) return string.Empty;
            var lines = new List<string>(_entries.Count);
            foreach (var entry in _entries.Values)
                lines.Add(entry.Payload);
            return string.Join(LINE_SEPARATOR.ToString(), lines);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Removes all expired entries from the tracker and returns their player
        /// IDs.  Callers (typically <see cref="HostConnectionService"/>) use the
        /// returned list to fire UI events and republish the lobby property.
        /// Returns an empty list if nothing expired.
        /// </remarks>
        public IReadOnlyList<string> RemoveExpired()
        {
            float now = Time.unscaledTime;
            List<string> removed = null;

            foreach (var kv in _entries)
            {
                if (now >= kv.Value.ExpiresAt)
                {
                    removed ??= new List<string>();
                    removed.Add(kv.Key);
                }
            }

            if (removed == null) return Array.Empty<string>();

            foreach (var id in removed)
                _entries.Remove(id);

            Debug.Log($"[InviteService] RemoveExpired → {removed.Count} expired, {OutgoingCount} remaining");
            return removed;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Static parse - kept internal so HostConnectionService can wrap it for
        // test-compatibility (tests reflect on ParseInviteLine on HCS, not here).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses one invite line into its components.
        ///
        /// Expected format: <c>targetPlayerId|senderPlayerId|sessionId|senderDisplayName|senderAvatarId</c>
        ///
        /// Returns null if the line is empty, malformed, or the avatarId is not an integer.
        ///
        /// The returned <see cref="PartyInviteData"/> uses:
        /// <c>parts[1]</c> = HostPlayerId, <c>parts[2]</c> = PartySessionId,
        /// <c>parts[3]</c> = HostDisplayName, <c>parts[4]</c> = HostAvatarId.
        /// </summary>
        /// <param name="line">A single invite line from the composite property value.</param>
        internal static (string targetId, PartyInviteData invite)? ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var parts = line.Split(FIELD_SEPARATOR);
            if (parts.Length < 5) return null;
            if (!int.TryParse(parts[4], out int avatarId)) return null;

            return (parts[0], new PartyInviteData(parts[1], parts[2], parts[3], avatarId));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildPayload(
            string targetPlayerId,
            string sessionId,
            string localPlayerId,
            string localDisplayName,
            int    localAvatarId)
        {
            return $"{targetPlayerId}{FIELD_SEPARATOR}" +
                   $"{localPlayerId}{FIELD_SEPARATOR}" +
                   $"{sessionId}{FIELD_SEPARATOR}" +
                   $"{localDisplayName}{FIELD_SEPARATOR}" +
                   $"{localAvatarId}";
        }
    }
}
