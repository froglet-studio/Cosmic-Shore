using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The tournament's AI seats, on the wire: up to <see cref="MaxSeats"/> bots, each a NAME and
    /// a DOMAIN, dealt once by the host and replayed into every round.
    ///
    /// <para><b>Why it has to travel at all.</b> The hub's roster is what a player reads before
    /// deciding to ready up, and a Maelstrom fills every empty seat with a bot - so a solo player
    /// looking at a one-name list is being told the field is one pilot when it is four. The bots
    /// have no <c>Player</c> object in the hub (the hub spawns none: it is not a match), and
    /// <c>MaelstromDataSO.MaelstromAISeats</c> is per-peer runtime state the host alone deals, so
    /// without this a client has no way to know they exist.</para>
    ///
    /// <para><b>State, not an announcement</b> - the same argument
    /// <see cref="MaelstromRoundTicket"/> records. A peer still inside Netcode scene
    /// synchronization when the host deals would have an RPC deferred and dropped, and would then
    /// ready up against a roster it could not see. Replicated per <c>Player</c> and written
    /// identically onto every one of them, so any copy is as good as any other.</para>
    ///
    /// <para><b>Why FOUR fixed slots rather than a list.</b> A <c>NetworkVariable</c> carries an
    /// unmanaged struct, and the bound is real rather than arbitrary: a Maelstrom seats
    /// <c>MaelstromDataSO.SeatCount</c> pilots, which is clamped to 4, and a session always has at
    /// least the local human - so three is the true maximum and the fourth slot is slack. Raising
    /// SeatCount past 4 would need a slot here, which <see cref="MaxSeats"/> is named for so the
    /// compiler-free failure (a silently dropped bot) becomes a findable one.</para>
    /// </summary>
    public struct MaelstromRosterTicket : INetworkSerializable, IEquatable<MaelstromRosterTicket>
    {
        /// <summary>How many slots below are filled. Anything past this is stale and ignored.</summary>
        public const int MaxSeats = 4;

        public byte Count;

        public FixedString32Bytes Name0;
        public FixedString32Bytes Name1;
        public FixedString32Bytes Name2;
        public FixedString32Bytes Name3;

        // Domains as bytes: the enum's underlying type is not part of its contract, and a
        // NetworkVariable that serialized the enum directly would change wire format if it moved.
        public byte Domain0;
        public byte Domain1;
        public byte Domain2;
        public byte Domain3;

        public static MaelstromRosterTicket None => default;

        /// <summary>Nobody dealt yet - drawn as "no AI", never as "an empty field".</summary>
        public bool HasSeats => Count > 0;

        public string NameAt(int i) => i switch
        {
            0 => Name0.ToString(),
            1 => Name1.ToString(),
            2 => Name2.ToString(),
            3 => Name3.ToString(),
            _ => string.Empty,
        };

        public Domains DomainAt(int i) => (Domains)(i switch
        {
            0 => Domain0,
            1 => Domain1,
            2 => Domain2,
            3 => Domain3,
            _ => (byte)Domains.Blue,
        });

        /// <summary>
        /// Pack the host's dealt seats. Seats past <see cref="MaxSeats"/> are DROPPED and said so,
        /// rather than silently truncated - a bot that exists on the host and on nobody else is
        /// the exact shape of a defect nobody attributes to this struct.
        /// </summary>
        public static MaelstromRosterTicket From(IReadOnlyList<MaelstromAISeat> seats)
        {
            var t = None;
            if (seats == null || seats.Count == 0) return t;

            int n = seats.Count;
            if (n > MaxSeats)
            {
                CSDebug.LogWarning(
                    $"[Maelstrom] {n} AI seats were dealt but MaelstromRosterTicket carries " +
                    $"{MaxSeats}. The extra bots will not reach clients - raise MaxSeats (and add " +
                    "the matching slots) if SeatCount is meant to go this high.");
                n = MaxSeats;
            }

            t.Count = (byte)n;
            for (int i = 0; i < n; i++)
            {
                var seat = seats[i];
                string name = seat != null && !string.IsNullOrEmpty(seat.Name) ? seat.Name : $"AI {i + 1}";
                var domain = (byte)(seat != null ? seat.Domain : Domains.Blue);
                switch (i)
                {
                    case 0: t.Name0 = name; t.Domain0 = domain; break;
                    case 1: t.Name1 = name; t.Domain1 = domain; break;
                    case 2: t.Name2 = name; t.Domain2 = domain; break;
                    case 3: t.Name3 = name; t.Domain3 = domain; break;
                }
            }
            return t;
        }

        /// <summary>Unpack into the shape <c>MaelstromDataSO.MaelstromAISeats</c> holds.</summary>
        public void CopyInto(List<MaelstromAISeat> seats)
        {
            if (seats == null) return;
            seats.Clear();
            for (int i = 0; i < Count && i < MaxSeats; i++)
                seats.Add(new MaelstromAISeat { Name = NameAt(i), Domain = DomainAt(i) });
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Count);
            serializer.SerializeValue(ref Name0);
            serializer.SerializeValue(ref Name1);
            serializer.SerializeValue(ref Name2);
            serializer.SerializeValue(ref Name3);
            serializer.SerializeValue(ref Domain0);
            serializer.SerializeValue(ref Domain1);
            serializer.SerializeValue(ref Domain2);
            serializer.SerializeValue(ref Domain3);
        }

        public bool Equals(MaelstromRosterTicket other) =>
            Count == other.Count &&
            Name0.Equals(other.Name0) && Name1.Equals(other.Name1) &&
            Name2.Equals(other.Name2) && Name3.Equals(other.Name3) &&
            Domain0 == other.Domain0 && Domain1 == other.Domain1 &&
            Domain2 == other.Domain2 && Domain3 == other.Domain3;

        public override bool Equals(object obj) => obj is MaelstromRosterTicket o && Equals(o);

        public override int GetHashCode() =>
            HashCode.Combine(Count, Name0, Name1, Name2, Name3,
                             HashCode.Combine(Domain0, Domain1, Domain2, Domain3));

        public override string ToString()
        {
            if (Count == 0) return "no AI seats";
            var parts = new string[Count];
            for (int i = 0; i < Count; i++) parts[i] = $"{NameAt(i)}/{DomainAt(i)}";
            return string.Join(", ", parts);
        }
    }
}
