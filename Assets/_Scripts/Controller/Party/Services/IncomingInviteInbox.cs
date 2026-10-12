// ─────────────────────────────────────────────────────────────────────────────
// IncomingInviteInbox.cs
// The recipient-side INBOX of party invites: every invite addressed to the local
// player that is still open, keyed by its sender.
//
// WHY this class exists (R13 item 2, "an invite you miss is gone forever"):
//   HostConnectionService used to remember ONE incoming invite (_lastFiredInvite +
//   a resolved flag). With two friends inviting at once the refresh loop's
//   duplicate check compared each line against whichever invite it had seen LAST,
//   so the two invites took turns being "new": OnInviteReceived re-fired for both
//   on every 0.75-1.5 s tick, the popup bounced between the two senders (its
//   auto-hide timer reset each time), the invite sound replayed each tick, and the
//   friends panel's rehydrate-on-open could only ever read the latest one. A
//   decline, which resolves "the" invite, also dropped the other sender's.
//
//   Holding every open invite in one place, with the dedup done per SENDER, makes
//   the popup, the friends panel's Requests rows and the unread badge three views
//   of the same list - none of them can disagree about what is still answerable.
//
// LIFETIME:
//   Pure C# - no Unity types. Created as a field on HostConnectionService, which
//   is its single writer; UI reads it through the service's accessors. Time is a
//   plain float the caller supplies (Time.unscaledTime in the service, a hand-set
//   clock in tests).
//
// THREAD SAFETY:
//   Main-thread only.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every open party invite addressed to the local player, one per sender.
    ///
    /// <para>An entry is PENDING while it is unresolved and younger than
    /// <see cref="LifetimeSeconds"/> (the host's own outgoing-invite timeout, so both sides
    /// clear together). Resolved entries (accepted, declined, suppressed because we are already
    /// in that sender's party) are kept, not deleted, until the sender's invite line is gone:
    /// that is what stops a line that is still on the wire from re-surfacing as a new invite,
    /// and what lets a later line from the same sender count as a NEW invite once the old one
    /// is over (the "re-invite after a decline" and "3rd player can never get in" fixes).</para>
    ///
    /// <para>An entry is also UNSEEN until the friends panel (the inbox's on-screen form) has
    /// been opened; <see cref="UnseenCount"/> is what the party panel's badge shows.</para>
    /// </summary>
    public sealed class IncomingInviteInbox
    {
        /// <summary>
        /// Consecutive refresh ticks on which a sender had no line for us before their entry is
        /// dropped. Two, so a single stale lobby snapshot (a presence-lobby converge mid-tick)
        /// cannot flicker a live invite.
        /// </summary>
        public const int MissTicksBeforeWithdrawn = 2;

        /// <summary>What <see cref="Observe"/> decided about a line the refresh loop found.</summary>
        public enum Observation
        {
            /// <summary>A new open invite: the caller raises OnInviteReceived.</summary>
            New = 0,
            /// <summary>The same open invite seen again: nothing to raise.</summary>
            Duplicate = 1,
            /// <summary>
            /// A line for an invite already resolved (the PENDING to real-session-id transition
            /// of an accepted invite, or a declined one still on the wire): the record is updated
            /// silently so the accept flow can read the real id, and nothing is raised.
            /// </summary>
            Refreshed = 2,
            /// <summary>
            /// A line for the party we are already a member of: not an invite. Recorded as
            /// resolved so it is never raised.
            /// </summary>
            Suppressed = 3,
        }

        sealed class Entry
        {
            public PartyInviteData Invite;
            public float ObservedAt;
            public bool Resolved;
            public bool Seen;
            public int MissTicks;
        }

        // Oldest first: the order the invites arrived, which is the order the panel lists them.
        readonly List<Entry> _entries = new();
        readonly float _lifetimeSeconds;

        /// <param name="lifetimeSeconds">
        /// Seconds an invite stays pending after it was first observed. 0 or less means it never
        /// expires on its own (the sender's line going away still ends it).
        /// </param>
        public IncomingInviteInbox(float lifetimeSeconds)
        {
            _lifetimeSeconds = Math.Max(0f, lifetimeSeconds);
        }

        /// <summary>Seconds an invite stays pending after it was first observed; 0 = never expires.</summary>
        public float LifetimeSeconds => _lifetimeSeconds;

        // ─────────────────────────────────────────────────────────────────────
        // Writes (HostConnectionService only)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Records a line the refresh loop found addressed to us and says whether it is new.
        /// Dedup is per SENDER: the same sender's same session seen again is a
        /// <see cref="Observation.Duplicate"/>; the same sender with a different session id is a
        /// new invite (they formed a new party) and replaces their old record.
        /// </summary>
        /// <param name="invite">The parsed line.</param>
        /// <param name="now">The caller's clock.</param>
        /// <param name="alreadyInSenderSession">
        /// True when the local player is a client of the very session the line names, in which
        /// case the line is a stale re-appearance, not an invite.
        /// </param>
        public Observation Observe(PartyInviteData invite, float now, bool alreadyInSenderSession)
        {
            var entry = Find(invite.HostPlayerId);

            if (alreadyInSenderSession)
            {
                if (entry == null)
                {
                    entry = new Entry { ObservedAt = now };
                    _entries.Add(entry);
                }
                entry.Invite    = invite;
                entry.Resolved  = true;
                entry.MissTicks = 0;
                return Observation.Suppressed;
            }

            if (entry != null)
            {
                entry.MissTicks = 0;

                if (entry.Resolved)
                {
                    entry.Invite = invite;
                    return Observation.Refreshed;
                }

                if (entry.Invite.PartySessionId == invite.PartySessionId)
                    return Observation.Duplicate;

                // Same sender, a new party: a new invite, so a fresh clock and unread again.
                entry.Invite     = invite;
                entry.ObservedAt = now;
                entry.Seen       = false;
                return Observation.New;
            }

            _entries.Add(new Entry { Invite = invite, ObservedAt = now });
            return Observation.New;
        }

        /// <summary>
        /// One refresh tick's bookkeeping, run after every line has been
        /// <see cref="Observe"/>d: drops a sender's record once their line has been gone for
        /// <see cref="MissTicksBeforeWithdrawn"/> ticks, and ends an invite that outlived
        /// <see cref="LifetimeSeconds"/> while its line is still on the wire.
        /// </summary>
        /// <param name="senderStillInviting">Whether this tick found a line from that sender.</param>
        /// <param name="hold">
        /// True for an invite whose record must be left alone this tick: a join to it is in
        /// flight (the line is cleared as the corroboration lands) or we are inside that sender's
        /// party (a stale re-appearance there is answered by <see cref="Observe"/>).
        /// </param>
        /// <param name="now">The caller's clock.</param>
        /// <param name="withdrawn">
        /// Receives every invite that was still PENDING and is now over (withdrawn by the sender
        /// or expired), so the caller can tell the UI. A resolved record that is dropped is not
        /// reported: nothing on screen is waiting on it.
        /// </param>
        /// <returns>How many invites were added to <paramref name="withdrawn"/>.</returns>
        public int Sweep(Func<string, bool> senderStillInviting, Func<PartyInviteData, bool> hold,
                         float now, List<PartyInviteData> withdrawn)
        {
            int ended = 0;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                bool stillInviting = senderStillInviting != null && senderStillInviting(entry.Invite.HostPlayerId);

                if (stillInviting)
                {
                    entry.MissTicks = 0;

                    // Outlived its lifetime while the line lingers (our clock started later than
                    // the sender's, so this is rare): end it, but keep the record resolved so the
                    // lingering line is not read as a new invite next tick.
                    if (!entry.Resolved && HasExpired(entry, now))
                    {
                        entry.Resolved = true;
                        withdrawn?.Add(entry.Invite);
                        ended++;
                    }
                    continue;
                }

                if (hold != null && hold(entry.Invite))
                    continue;

                if (++entry.MissTicks < MissTicksBeforeWithdrawn)
                    continue;

                bool wasPending = IsPending(entry, now);
                _entries.RemoveAt(i);

                if (wasPending)
                {
                    withdrawn?.Add(entry.Invite);
                    ended++;
                }
            }

            return ended;
        }

        /// <summary>
        /// Ends one sender's invite (a Decline). Returns true when it was pending.
        /// The record stays until the sender's line is gone (see the class summary).
        /// </summary>
        public bool Resolve(string senderPlayerId)
        {
            var entry = Find(senderPlayerId);
            if (entry == null) return false;

            bool wasPending = !entry.Resolved;
            entry.Resolved = true;
            return wasPending;
        }

        /// <summary>
        /// Ends every open invite: the local player accepted one, joined a party directly, went
        /// to spectate, or left their party. Whatever else was waiting is moot, since they are
        /// leaving for a different party. Returns true when any was pending.
        /// </summary>
        public bool ResolveAll()
        {
            bool anyPending = false;
            foreach (var entry in _entries)
            {
                anyPending |= !entry.Resolved;
                entry.Resolved = true;
            }
            return anyPending;
        }

        /// <summary>The inbox has been looked at (the friends panel opened): nothing is unseen.</summary>
        public void MarkAllSeen()
        {
            foreach (var entry in _entries)
                entry.Seen = true;
        }

        /// <summary>Forgets everything (Menu_Main reloaded).</summary>
        public void Clear() => _entries.Clear();

        // ─────────────────────────────────────────────────────────────────────
        // Reads
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>How many invites are still open.</summary>
        public int PendingCount(float now)
        {
            int count = 0;
            foreach (var entry in _entries)
                if (IsPending(entry, now)) count++;
            return count;
        }

        /// <summary>How many open invites the player has not had on screen in the friends panel yet.</summary>
        public int UnseenCount(float now)
        {
            int count = 0;
            foreach (var entry in _entries)
                if (IsPending(entry, now) && !entry.Seen) count++;
            return count;
        }

        /// <summary>Copies every open invite into <paramref name="into"/>, oldest first.</summary>
        public void CopyPending(List<PartyInviteData> into, float now)
        {
            if (into == null) return;
            into.Clear();
            foreach (var entry in _entries)
                if (IsPending(entry, now)) into.Add(entry.Invite);
        }

        /// <summary>The most recently received open invite, or null when nothing is open.</summary>
        public PartyInviteData? Newest(float now)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (IsPending(_entries[i], now)) return _entries[i].Invite;
            return null;
        }

        /// <summary>The longest-waiting open invite, or null when nothing is open.</summary>
        public PartyInviteData? Oldest(float now)
        {
            foreach (var entry in _entries)
                if (IsPending(entry, now)) return entry.Invite;
            return null;
        }

        /// <summary>Whether that sender's invite is still open.</summary>
        public bool IsPending(string senderPlayerId, float now)
        {
            var entry = Find(senderPlayerId);
            return entry != null && IsPending(entry, now);
        }

        /// <summary>That sender's open invite, when there is one.</summary>
        public bool TryGetPending(string senderPlayerId, float now, out PartyInviteData invite)
        {
            var entry = Find(senderPlayerId);
            if (entry != null && IsPending(entry, now))
            {
                invite = entry.Invite;
                return true;
            }
            invite = default;
            return false;
        }

        /// <summary>
        /// Seconds that sender's invite stays open from <paramref name="now"/>: 0 when it is not
        /// open, <see cref="float.PositiveInfinity"/> when invites never expire.
        /// </summary>
        public float SecondsRemaining(string senderPlayerId, float now)
        {
            var entry = Find(senderPlayerId);
            if (entry == null || !IsPending(entry, now)) return 0f;
            if (_lifetimeSeconds <= 0f) return float.PositiveInfinity;
            return Math.Max(0f, entry.ObservedAt + _lifetimeSeconds - now);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        Entry Find(string senderPlayerId)
        {
            if (string.IsNullOrEmpty(senderPlayerId)) return null;
            foreach (var entry in _entries)
                if (entry.Invite.HostPlayerId == senderPlayerId) return entry;
            return null;
        }

        bool HasExpired(Entry entry, float now)
            => _lifetimeSeconds > 0f && now - entry.ObservedAt >= _lifetimeSeconds;

        bool IsPending(Entry entry, float now)
            => !entry.Resolved && !HasExpired(entry, now);
    }
}
