using System;
using System.Collections.Generic;
using System.Text;

namespace CosmicShore.Core
{
    /// <summary>
    /// Invite attribution for <c>party_invite_sent</c>, <c>party_invite_received</c> and
    /// <c>party_joined</c> (Shombith approved 2026-11-07: who brought whom).
    ///
    /// <para><b>The invite id is DERIVED, not carried.</b> The invite travels as one line of a UGS
    /// presence-lobby player property (InviteService: <c>target|sender|session|name|avatar</c>), and a
    /// new field there is a party-protocol change every client has to understand. Both ends already
    /// know the same three facts - who sent it, to whom, into which party session - so each computes
    /// the same id from them on its own: the sender when it is sent, the invitee when it arrives.
    /// The same sender inviting the same player into the same party session again gets the same id,
    /// which is one invite relation for the funnel.</para>
    ///
    /// <para>The id is a hash, so it names no player; <c>inviter_id</c> on <c>party_joined</c> is the
    /// inviter's UGS id (P1), sent only to Froglet Analytics.</para>
    ///
    /// Pure; no Unity calls, so the edit-mode tests drive it directly.
    /// </summary>
    public sealed class InviteAttribution
    {
        /// <summary>Invites remembered for matching a later join; older ones are forgotten first.</summary>
        public const int MaxRemembered = 8;

        readonly List<(string SessionId, string InviteId, string InviterId)> _received = new();

        /// <summary>
        /// The invite id both ends compute: 16 hex digits of an FNV-1a 64 hash of
        /// "sender|target|session". Null when any part is missing (nothing to agree on).
        /// </summary>
        public static string InviteIdFor(string senderId, string targetId, string partySessionId)
        {
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(partySessionId))
                return null;

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (byte b in Encoding.UTF8.GetBytes(senderId + "|" + targetId + "|" + partySessionId))
            {
                hash ^= b;
                hash *= prime;
            }

            return hash.ToString("x16");
        }

        /// <summary>Remembers an invite that arrived, so a join into its party can be attributed to it.</summary>
        public void RememberReceived(string partySessionId, string inviteId, string inviterId)
        {
            if (string.IsNullOrEmpty(partySessionId) || string.IsNullOrEmpty(inviteId))
                return;

            _received.RemoveAll(r => r.SessionId == partySessionId);
            _received.Add((partySessionId, inviteId, inviterId ?? string.Empty));
            if (_received.Count > MaxRemembered)
                _received.RemoveAt(0);
        }

        /// <summary>
        /// The invite behind a join into <paramref name="joinedSessionId"/>, or false for a join that
        /// no remembered invite led to (a presence join, a rejoin): then party_joined says nothing.
        /// A matched invite is forgotten, so one invite attributes one join.
        /// </summary>
        public bool TryTakeForJoin(string joinedSessionId, out string inviteId, out string inviterId)
        {
            inviteId = null;
            inviterId = null;
            if (string.IsNullOrEmpty(joinedSessionId))
                return false;

            for (int i = _received.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_received[i].SessionId, joinedSessionId, StringComparison.Ordinal))
                    continue;

                inviteId = _received[i].InviteId;
                inviterId = _received[i].InviterId;
                _received.RemoveAt(i);
                return true;
            }

            return false;
        }
    }
}
