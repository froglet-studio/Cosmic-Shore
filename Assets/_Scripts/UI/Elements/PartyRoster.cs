using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// THE order a party is drawn in, and the one place it is decided.
    ///
    /// <para><b>The problem this solves.</b> <c>HostConnectionDataSO.PartyMembers</c> is a
    /// PER-DEVICE list: <c>PartyMemberService.SeedLocalPlayer</c> puts the local player at
    /// index 0 on every machine, and <c>SyncFromSession</c> appends everyone else in whatever
    /// order <c>ISession.Players</c> happens to enumerate. So the host saw itself first, each
    /// client saw ITSELF first, and no two devices agreed on the order of the rest - the party
    /// panel showed four different seatings of the same four people.</para>
    ///
    /// <para><b>The fix is to order by something already replicated</b>, rather than to publish
    /// a new ordering property and hope every peer converges on it. Netcode's
    /// <c>OwnerClientId</c> is exactly that: the server assigns it in CONNECTION order, the
    /// host is always <c>NetworkManager.ServerClientId</c> (0), and every peer reads the same
    /// value off the same replicated <c>Player</c> object. Sorting ascending therefore puts the
    /// host first and the clients in join order, identically everywhere, with nothing new on
    /// the wire.</para>
    ///
    /// <para><b>Degrading is part of the contract.</b> A party member whose <c>Player</c> has
    /// not network-spawned yet (or has already despawned) has no client id, so it sorts LAST
    /// behind everyone who has one, and ties there break on the ordinal player id - which is
    /// still identical on every device. A transiently-unresolved member therefore moves to the
    /// back and then into its real seat, instead of scrambling the seats that ARE known.</para>
    ///
    /// Pure static: no Unity types, no UGS types, no Netcode types - the caller supplies the
    /// client-id lookup, which is what keeps this testable in edit mode.
    /// </summary>
    public static class PartyRoster
    {
        /// <summary>
        /// Sorts last: "this member has no replicated client id right now". Deliberately
        /// <see cref="ulong.MaxValue"/> so the natural ascending sort needs no special case.
        /// </summary>
        public const ulong UnknownClientId = ulong.MaxValue;

        /// <summary>One drawn seat: who sits there, and the two facts the UI styles on.</summary>
        public readonly struct Seat
        {
            /// <summary>
            /// The seated pilot. Named <c>Member</c> rather than <c>Player</c> so it cannot be
            /// read as the Netcode <c>Player</c> component, which is a different thing entirely
            /// and is exactly what supplies the client id this seat was sorted on.
            /// </summary>
            public readonly PartyPlayerData Member;

            /// <summary>This seat is the local user (no kick button, local identity fields win).</summary>
            public readonly bool IsLocal;

            /// <summary>This seat is the party host (always the first seat when it is resolvable).</summary>
            public readonly bool IsHost;

            public Seat(PartyPlayerData member, bool isLocal, bool isHost)
            {
                Member  = member;
                IsLocal = isLocal;
                IsHost  = isHost;
            }

            public string PlayerId => Member.PlayerId;
        }

        // Sort scratch. Main-thread only (all party UI is), reused so a repopulate on every
        // member event does not allocate.
        struct Key
        {
            public ulong           ClientId;
            public string          PlayerId;
            public PartyPlayerData Member;
        }

        static readonly List<Key> Scratch = new();

        static readonly Comparison<Key> ByClientThenId = (a, b) =>
        {
            int c = a.ClientId.CompareTo(b.ClientId);
            return c != 0 ? c : string.CompareOrdinal(a.PlayerId, b.PlayerId);
        };

        /// <summary>
        /// Builds the synced seating for a party.
        /// </summary>
        /// <param name="members">
        /// The local <c>PartyMembers</c> list. May or may not contain the local player, may be
        /// in any order, and may contain duplicates - all three are normalised here. Typed
        /// <c>IList</c> rather than <c>IReadOnlyList</c> because SOAP's <c>ScriptableList&lt;T&gt;</c>
        /// implements only the former; nothing here writes to it.
        /// </param>
        /// <param name="localPlayer">
        /// The local player's identity, always seated even when <paramref name="members"/> has
        /// not caught up with it yet (the panel must never fail to show you).
        /// </param>
        /// <param name="hostClientId">
        /// The client id that means "host" - <c>NetworkManager.ServerClientId</c>. Pass
        /// <see cref="UnknownClientId"/> when there is no live network; no seat is then marked
        /// host, and the ordering falls back to ordinal player id (still identical everywhere).
        /// </param>
        /// <param name="clientIdFor">
        /// UGS player id → replicated Netcode owner client id, or <see cref="UnknownClientId"/>
        /// when that player has no live <c>Player</c> object on this machine.
        /// </param>
        /// <param name="result">Filled with the seating, first seat first. Cleared on entry.</param>
        public static void Build(
            IList<PartyPlayerData> members,
            PartyPlayerData localPlayer,
            ulong hostClientId,
            Func<string, ulong> clientIdFor,
            List<Seat> result)
        {
            if (result == null) return;
            result.Clear();
            Scratch.Clear();

            string localId = localPlayer.PlayerId;

            if (!string.IsNullOrEmpty(localId))
                Add(localPlayer, localId, clientIdFor);

            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var m  = members[i];
                    var id = m.PlayerId;
                    if (string.IsNullOrEmpty(id)) continue;

                    // The local entry is seeded above from HostConnectionDataSO's own live
                    // identity fields, which resolve from the cloud profile before the copy
                    // inside PartyMembers does - so the list's stale duplicate is dropped
                    // rather than overwriting the fresher name/avatar.
                    if (id == localId) continue;

                    if (Contains(id)) continue;   // duplicate row from an in-flight sync
                    Add(m, id, clientIdFor);
                }
            }

            Scratch.Sort(ByClientThenId);

            for (int i = 0; i < Scratch.Count; i++)
            {
                var k = Scratch[i];
                bool isHost = hostClientId != UnknownClientId &&
                              k.ClientId   != UnknownClientId &&
                              k.ClientId   == hostClientId;
                result.Add(new Seat(k.Member, k.PlayerId == localId, isHost));
            }

            Scratch.Clear();
        }

        static void Add(PartyPlayerData member, string id, Func<string, ulong> clientIdFor)
        {
            Scratch.Add(new Key
            {
                ClientId = clientIdFor != null ? clientIdFor(id) : UnknownClientId,
                PlayerId = id,
                Member   = member,
            });
        }

        static bool Contains(string id)
        {
            for (int i = 0; i < Scratch.Count; i++)
                if (Scratch[i].PlayerId == id) return true;
            return false;
        }
    }
}
