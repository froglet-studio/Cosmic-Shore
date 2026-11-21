using System;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The whole of what a peer needs to know about the round it is waiting on: WHICH mode was
    /// drawn, at WHAT intensity, and WHEN it launches.
    ///
    /// <para><b>Why the draw has to travel at all.</b> Before the hub previewed anything, the draw
    /// was host-only on purpose - a client learned the mode from the scene that loaded, and the
    /// intensity from the existing config sync, so no shared RNG or seed was needed. A hub that
    /// stands the upcoming arena and lets people fly it turns that inside out: every peer has to
    /// build the same world several seconds BEFORE the load, so the pick has to be on the wire
    /// first. It is replicated as STATE rather than announced as an RPC for the reason
    /// <c>ArcadeConfigSyncManager.LobbySnapshot</c> already records - a peer still inside Netcode
    /// scene synchronization when the host draws would have the message deferred and dropped, and
    /// would then sit in front of a hub with no arena in it.</para>
    ///
    /// <para><b>The index, not the asset.</b> <c>SO_ArcadeGame</c> is a project asset with no
    /// network identity, so the pick travels as its position in <c>MaelstromDataSO.GameQueue</c> -
    /// the one ordering every peer already shares, and the same handle
    /// <c>MaelstromDataSO.CurrentGameIndex</c> has always used. A queue edited between builds would
    /// make an index mean two things, which is why <see cref="ResolveGame"/> range-checks rather
    /// than indexing blind.</para>
    ///
    /// <para><b>The deadline is server time.</b> <c>NetworkManager.ServerTime</c> is synchronised,
    /// so a single absolute instant renders the same countdown on every machine without anyone
    /// ticking a local clock or trusting the order two peers entered the hub in.</para>
    /// </summary>
    public struct MaelstromRoundTicket : INetworkSerializable, IEquatable<MaelstromRoundTicket>
    {
        /// <summary>Index into <c>MaelstromDataSO.GameQueue</c>, or -1 when nothing is drawn yet.</summary>
        public short GameIndex;

        /// <summary>The rolled per-game intensity, 1..4. 0 while nothing is drawn.</summary>
        public byte Intensity;

        /// <summary>
        /// <c>NetworkManager.ServerTime.Time</c> at which the round launches, or 0 while the
        /// countdown has not been armed. Never a duration: a duration has to be started somewhere,
        /// and two peers do not enter the hub on the same frame.
        /// </summary>
        public double StartServerTime;

        /// <summary>Nothing drawn, nothing armed - what a hub opens on and what a launch clears to.</summary>
        public static MaelstromRoundTicket None => new() { GameIndex = -1, Intensity = 0, StartServerTime = 0d };

        public bool HasGame => GameIndex >= 0;

        public bool IsArmed => StartServerTime > 0d;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref GameIndex);
            serializer.SerializeValue(ref Intensity);
            serializer.SerializeValue(ref StartServerTime);
        }

        public bool Equals(MaelstromRoundTicket other) =>
            GameIndex == other.GameIndex &&
            Intensity == other.Intensity &&
            StartServerTime.Equals(other.StartServerTime);

        public override bool Equals(object obj) => obj is MaelstromRoundTicket other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(GameIndex, Intensity, StartServerTime);

        public override string ToString() =>
            HasGame ? $"game #{GameIndex} @ intensity {Intensity} (starts {StartServerTime:0.0})" : "no round drawn";
    }
}
