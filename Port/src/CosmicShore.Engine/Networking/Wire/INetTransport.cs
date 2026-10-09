using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Networking
{
    internal enum NetEventKind { Connected, Data, Disconnected }

    /// <summary>
    /// How a frame travels. <see cref="Reliable"/>: arrives, whole, in order (the contract's default).
    /// <see cref="Unreliable"/>: may be lost or arrive out of order, never resent and never holds anything
    /// back - for state where only the newest value matters (a transform whose prefab opts in with
    /// UseUnreliableDeltas). A transport without such a channel sends it reliably.
    /// </summary>
    public enum NetChannel : byte { Reliable = 0, Unreliable = 1 }

    /// <summary>One thing a transport received: a peer connected, a frame arrived, or a peer left.</summary>
    internal readonly struct NetEvent
    {
        public readonly NetEventKind Kind;
        public readonly int Peer;
        public readonly byte[] Payload;
        public readonly NetChannel Channel;
        public NetEvent(NetEventKind k, int peer, byte[] payload, NetChannel channel = NetChannel.Reliable) { Kind = k; Peer = peer; Payload = payload; Channel = channel; }
    }

    /// <summary>
    /// The seam between <see cref="NetDriver"/> (replication) and whatever carries its bytes.
    /// The contract every implementation keeps:
    ///  • frames are reliable, ordered and arrive whole (one Send = one Data event); a frame sent on
    ///    <see cref="NetChannel.Unreliable"/> arrives whole or not at all, in any order, and may not be
    ///    delivered reliably only by a transport that has no such channel;
    ///  • events reach the driver only through <see cref="Poll"/>, which the main thread drains once per frame;
    ///  • a client addresses the server as peer 0; a server addresses each client by the id its Connected event carried;
    ///  • a client whose connect fails reports a Disconnected event (it never throws from Connect);
    ///  • a listen that cannot bind throws, so the driver can retry on another port.
    /// TCP (<see cref="NetSocket"/>) is the first implementation; an internet relay is the next (C6, gate G2).
    /// </summary>
    internal interface INetTransport : IDisposable
    {
        bool IsServer { get; }
        /// <summary>The port a server listens on (0 when the transport has no port, or on a client).</summary>
        int ListenPort { get; }
        int PeerCount { get; }
        /// <summary>Queue a frame to one peer (0 = the server, from a client).</summary>
        void Send(int peer, byte[] payload, int length = -1);
        /// <summary>Queue a frame on a channel. A transport with no unreliable channel (TCP) sends it reliably.</summary>
        void Send(int peer, byte[] payload, int length, NetChannel channel) => Send(peer, payload, length);
        void Disconnect(int peer);
        bool Poll(out NetEvent e);
    }

    /// <summary>Opens a transport as a server or a client. <see cref="NetDriver.TransportFactory"/> picks the implementation.</summary>
    internal interface INetTransportFactory
    {
        INetTransport Listen(string address, int port);
        INetTransport Connect(string address, int port, int timeoutMs);
    }

    /// <summary>Picks the transport every player of a session must share (docs/MULTIPLAYER.md §6.6).</summary>
    public static class NetTransports
    {
        /// <summary>
        /// What a networked player uses when nothing is chosen: Froglet's UDP. Chosen 2026-10-08 after the
        /// five-player party harness passed 14/14 on it, and 13/14 with every player on a simulated 4G line
        /// (the one failure a game-side defect, not the transport). TCP stays one switch away.
        /// </summary>
        public const string Default = "udp";

        /// <summary>
        /// Selects the driver's transport by name: "udp" (Froglet's, the <see cref="Default"/>) or "tcp".
        /// Empty means the default. Returns the name in use. Call before <see cref="NetSimulator.Install"/>,
        /// which wraps whatever is chosen.
        /// </summary>
        public static string Select(string name)
        {
            var n = (name ?? "").Trim().ToLowerInvariant();
            if (n.Length == 0) n = Default;
            switch (n)
            {
                case "udp": NetDriver.TransportFactory = new UdpTransportFactory(); return "udp";
                case "tcp": NetDriver.TransportFactory = new TcpTransportFactory(); return "tcp";
                default:
                    Func<INetTransportFactory> make;
                    lock (s_registered)
                        if (!s_registered.TryGetValue(n, out make)) make = null;
                    if (make != null) { NetDriver.TransportFactory = make(); return n; }
                    Console.WriteLine($"[net] unknown transport '{name}' ({string.Join(", ", Names)}): using {Default}");
                    return Select(Default);
            }
        }

        static readonly Dictionary<string, Func<INetTransportFactory>> s_registered = new();

        /// <summary>
        /// Adds a transport <see cref="Select"/> can name, from an assembly the engine does not reference
        /// (CosmicShore.Online registers "relay": docs/RELAY.md). Registering a name again replaces it.
        /// </summary>
        internal static void Register(string name, Func<INetTransportFactory> make)
        {
            if (string.IsNullOrWhiteSpace(name) || make == null) throw new ArgumentException("a transport needs a name and a factory");
            lock (s_registered) s_registered[name.Trim().ToLowerInvariant()] = make;
        }

        /// <summary>Every name <see cref="Select"/> accepts.</summary>
        public static IReadOnlyList<string> Names
        {
            get
            {
                var list = new List<string> { "udp", "tcp" };
                lock (s_registered) list.AddRange(s_registered.Keys);
                return list;
            }
        }
    }

    /// <summary>The default factory: TCP.</summary>
    internal sealed class TcpTransportFactory : INetTransportFactory
    {
        public INetTransport Listen(string address, int port) => NetSocket.Listen(address, port);
        public INetTransport Connect(string address, int port, int timeoutMs) => NetSocket.Connect(address, port, timeoutMs);
    }
}
